using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Extensions;
using StardewValley.Locations;

namespace Smartphone
{
    public partial class ModEntry
    {
        private const float NpcCaptureZoomMin = 0.5f;
        private const float NpcCaptureZoomMax = 2f;

        public static Microsoft.Xna.Framework.Rectangle GetPhoneCameraViewportBounds(int menuX, int menuY)
        {
            return new Microsoft.Xna.Framework.Rectangle(
                menuX + CameraViewportOffsetX,
                menuY + CameraViewportOffsetY,
                CameraViewportWidth,
                CameraViewportHeight);
        }

        public static Microsoft.Xna.Framework.Rectangle GetPhoneCameraPreviewBounds(int menuX, int menuY)
        {
            Microsoft.Xna.Framework.Rectangle portraitViewport = GetPhoneCameraViewportBounds(menuX, menuY);
            if (!cameraLandscapeMode)
                return portraitViewport;

            int previewWidth = portraitViewport.Height;
            int previewHeight = portraitViewport.Width;

            return new Microsoft.Xna.Framework.Rectangle(
                portraitViewport.Center.X - (previewWidth / 2),
                portraitViewport.Center.Y - (previewHeight / 2),
                previewWidth,
                previewHeight);
        }

        public static Microsoft.Xna.Framework.Rectangle GetPlayerPhotoCaptureBounds(int menuX, int menuY)
        {
            Microsoft.Xna.Framework.Rectangle viewportBounds = GetPhoneCameraPreviewBounds(menuX, menuY);
            return BuildCaptureBounds(viewportBounds, cameraLandscapeMode, cameraSquareMode);
        }

        public static Microsoft.Xna.Framework.Rectangle GetCurrentPlayerPhotoCaptureBounds()
        {
            return GetPlayerPhotoCaptureBounds(currentMenuX, currentMenuY);
        }

        private static Microsoft.Xna.Framework.Rectangle BuildCaptureBounds(Microsoft.Xna.Framework.Rectangle viewportBounds, bool landscape, bool square)
        {
            (int captureWidth, int captureHeight) = GetCaptureDimensions(landscape, square);

            float zoom = Math.Clamp(cameraZoomFactor, 1f, 2f);
            captureWidth = Math.Max(1, (int)Math.Round(captureWidth / zoom));
            captureHeight = Math.Max(1, (int)Math.Round(captureHeight / zoom));

            captureWidth = Math.Min(captureWidth, viewportBounds.Width);
            captureHeight = Math.Min(captureHeight, viewportBounds.Height);

            int captureX = viewportBounds.X + ((viewportBounds.Width - captureWidth) / 2);
            int captureY = viewportBounds.Y + ((viewportBounds.Height - captureHeight) / 2);

            return new Microsoft.Xna.Framework.Rectangle(captureX, captureY, captureWidth, captureHeight);
        }

        private static (int Width, int Height) GetCaptureDimensions(bool landscape, bool square)
        {
            if (square)
                return (CameraViewportWidth, CameraViewportWidth);

            if (landscape)
                return (CameraViewportHeight, CameraViewportWidth);

            return (CameraViewportWidth, CameraViewportHeight);
        }

        private static Microsoft.Xna.Framework.Rectangle ClampCaptureBoundsToBackBuffer(Microsoft.Xna.Framework.Rectangle requestedBounds, int backBufferWidth, int backBufferHeight)
        {
            int clampedWidth = Math.Max(1, Math.Min(requestedBounds.Width, backBufferWidth));
            int clampedHeight = Math.Max(1, Math.Min(requestedBounds.Height, backBufferHeight));
            int clampedX = Math.Clamp(requestedBounds.X, 0, Math.Max(0, backBufferWidth - clampedWidth));
            int clampedY = Math.Clamp(requestedBounds.Y, 0, Math.Max(0, backBufferHeight - clampedHeight));

            return new Microsoft.Xna.Framework.Rectangle(clampedX, clampedY, clampedWidth, clampedHeight);
        }

        private void OnRendered(object sender, RenderedEventArgs e)
        {
            // dev tool grid drawing
            if (isGridVisible && Context.IsWorldReady)
            {
                DrawGrid();
            }

            if (takeScreenshot)
            {
                takeScreenshot = false;

                GraphicsDevice graphics = Game1.graphics.GraphicsDevice;

                int backBufferWidth = graphics.PresentationParameters.BackBufferWidth;
                int backBufferHeight = graphics.PresentationParameters.BackBufferHeight;

                // Create a RenderTarget the same size as the back buffer
                using RenderTarget2D copyTarget = new RenderTarget2D(graphics, backBufferWidth, backBufferHeight);

                // Copy current back buffer into our render target
                graphics.SetRenderTarget(copyTarget);
                graphics.Clear(Color.Black);

                // Draw the backbuffer content into the render target
                graphics.SetRenderTarget(null);
                Color[] rawData = new Color[backBufferWidth * backBufferHeight];
                graphics.GetBackBufferData(rawData);

                Microsoft.Xna.Framework.Rectangle requestedCaptureBounds = GetCurrentPlayerPhotoCaptureBounds();
                Microsoft.Xna.Framework.Rectangle captureBounds = ClampCaptureBoundsToBackBuffer(requestedCaptureBounds, backBufferWidth, backBufferHeight);

                int cropX = captureBounds.X;
                int cropY = captureBounds.Y;
                int cropWidth = captureBounds.Width;
                int cropHeight = captureBounds.Height;

                // Extract pixel data from the cropped region
                Color[] croppedData = new Color[cropWidth * cropHeight];
                for (int y = 0; y < cropHeight; y++)
                {
                    for (int x = 0; x < cropWidth; x++)
                    {
                        int srcIndex = (cropY + y) * backBufferWidth + (cropX + x);
                        int destIndex = y * cropWidth + x;
                        croppedData[destIndex] = rawData[srcIndex];
                    }
                }

                // Create cropped texture
                using Texture2D croppedTexture = new Texture2D(graphics, cropWidth, cropHeight);
                croppedTexture.SetData(croppedData);

                SaveCapturedPhoto(croppedTexture, Game1.currentLocation?.Name, BuildImageTags(captureBounds).ToList(), true);
            }
        }

        private static string CaptureNpcPhoto(NPC npc, Vector2 captureCenter, bool landscape = false, bool square = false, List<NPC>? visibleNpcAtTarget = null, float zoomLevel = 1f, int? captureTimeOfDay = null)
        {
            if (!Context.IsWorldReady || npc == null || npc.currentLocation == null || Game1.graphics?.GraphicsDevice == null || Game1.game1 == null)
                return "";

            GraphicsDevice graphics = Game1.graphics.GraphicsDevice;
            GameLocation targetLocation = npc.currentLocation;
            int effectiveCaptureTime = NormalizeCaptureTimeOfDay(captureTimeOfDay);
            var renderStateSnapshot = new PhotoRenderStateSnapshot();
            int temporarySpriteCount = targetLocation.temporarySprites.Count;
            (int baseCaptureWidth, int baseCaptureHeight) = GetCaptureDimensions(landscape, square);
            (int captureWidth, int captureHeight) = GetZoomedCaptureDimensions(baseCaptureWidth, baseCaptureHeight, zoomLevel);
            var captureBounds = new Microsoft.Xna.Framework.Rectangle(0, 0, captureWidth, captureHeight);
            List<string> tags = new List<string>();

            using RenderTarget2D renderTarget = new RenderTarget2D(graphics, captureWidth, captureHeight);

            try
            {
                Game1.currentLocation = targetLocation;
                Game1.viewport = BuildNpcCaptureViewport(targetLocation, captureCenter, captureWidth, captureHeight);
                Game1.timeOfDay = effectiveCaptureTime;
                PrepareLocationRenderState(targetLocation, captureWidth, captureHeight);

                graphics.SetRenderTarget(renderTarget);
                graphics.Clear(Color.Black);

                Game1.game1.DrawWorld(Game1.currentGameTime, renderTarget);
                tags = BuildImageTags(captureBounds, npc: npc).ToList();
            }
            catch (Exception ex)
            {
                RecoverWorldDrawStateAfterCaptureFailure();
                SMonitor.Log($"Failed to capture off-screen NPC photo for {npc.Name}: {ex}", LogLevel.Error);
                return "";
            }
            finally
            {
                graphics.SetRenderTarget(null);
                RestoreTemporarySpritesAfterCapture(targetLocation, temporarySpriteCount);
                renderStateSnapshot.Restore();
            }

            if (visibleNpcAtTarget != null)
            {
                foreach (var character in visibleNpcAtTarget)
                {
                    character.IsInvisible = false;
                }
            }

            return SaveCapturedPhoto(renderTarget, targetLocation.Name, tags, false);
        }

        private static (int Width, int Height) GetZoomedCaptureDimensions(int baseCaptureWidth, int baseCaptureHeight, float zoomLevel)
        {
            float safeZoomLevel = zoomLevel;
            if (float.IsNaN(safeZoomLevel) || float.IsInfinity(safeZoomLevel))
                safeZoomLevel = 1f;

            safeZoomLevel = Math.Clamp(safeZoomLevel, NpcCaptureZoomMin, NpcCaptureZoomMax);

            int captureWidth = Math.Max(1, (int)Math.Round(baseCaptureWidth / safeZoomLevel));
            int captureHeight = Math.Max(1, (int)Math.Round(baseCaptureHeight / safeZoomLevel));
            return (captureWidth, captureHeight);
        }

        private static int NormalizeCaptureTimeOfDay(int? captureTimeOfDay)
        {
            int fallbackTime = Game1.timeOfDay > 0 ? Game1.timeOfDay : 600;
            int rawTime = captureTimeOfDay ?? fallbackTime;

            int hour = Math.Clamp(rawTime / 100, 6, 26);
            int minute = Math.Clamp(rawTime % 100, 0, 59);
            return (hour * 100) + minute;
        }

        private static void RecoverWorldDrawStateAfterCaptureFailure()
        {
            try
            {
                Game1.spriteBatch?.End();
            }
            catch (InvalidOperationException)
            {
            }
            catch (Exception ex)
            {
                SMonitor.Log($"SpriteBatch recovery failed after NPC photo capture error: {ex}", LogLevel.Trace);
            }

            object? mapDisplayDevice = TryGetStaticMemberValue(typeof(Game1), "mapDisplayDevice", "MapDisplayDevice");
            if (mapDisplayDevice != null)
            {
                try
                {
                    TryInvokeNoArgumentMethod(mapDisplayDevice, "EndScene");
                }
                catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException)
                {
                }
                catch (InvalidOperationException)
                {
                }
                catch (Exception ex)
                {
                    SMonitor.Log($"Map display device recovery failed after NPC photo capture error: {ex}", LogLevel.Trace);
                }
            }

            try
            {
                Game1.graphics?.GraphicsDevice?.SetRenderTarget(null);
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Render target recovery failed after NPC photo capture error: {ex}", LogLevel.Trace);
            }
        }

        private static object? TryGetStaticMemberValue(Type sourceType, params string[] memberNames)
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;

            foreach (string memberName in memberNames)
            {
                PropertyInfo? property = sourceType.GetProperty(memberName, flags);
                if (property != null)
                    return property.GetValue(null);

                FieldInfo? field = sourceType.GetField(memberName, flags);
                if (field != null)
                    return field.GetValue(null);
            }

            return null;
        }

        private static bool TrySetStaticMemberValue(Type sourceType, object? value, params string[] memberNames)
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;

            foreach (string memberName in memberNames)
            {
                PropertyInfo? property = sourceType.GetProperty(memberName, flags);
                if (property?.CanWrite == true)
                {
                    property.SetValue(null, value);
                    return true;
                }

                FieldInfo? field = sourceType.GetField(memberName, flags);
                if (field != null)
                {
                    field.SetValue(null, value);
                    return true;
                }
            }

            return false;
        }

        private static bool TryInvokeNoArgumentMethod(object source, string methodName)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;

            MethodInfo? method = source.GetType().GetMethod(methodName, flags, Type.DefaultBinder, Type.EmptyTypes, null);
            if (method == null)
                return false;

            method.Invoke(source, null);
            return true;
        }

        private static string SaveCapturedPhoto(Texture2D capturedTexture, string? locationName, IEnumerable<string> tags, bool isPlayerPhoto)
        {
            string folderPath;
            if (isPlayerPhoto)
                folderPath = Path.Combine(SHelper.DirectoryPath, "userdata", GetCurrentSaveFolderName(), "player_photo");
            else
                folderPath = Path.Combine(SHelper.DirectoryPath, "userdata", GetCurrentSaveFolderName(), "npc_photo");
            Directory.CreateDirectory(folderPath);

            string resolvedLocationName = string.IsNullOrWhiteSpace(locationName)
                ? "UnknownLocation"
                : locationName;

            string filename = $"{resolvedLocationName}-{Game1.currentSeason}-Y{Game1.year}-D{Game1.dayOfMonth:D2}_{Game1.random.Next(0, 9999999)}.png";
            string path = Path.Combine(folderPath, filename);

            using Texture2D opaqueTexture = CreateOpaqueTexture(capturedTexture);
            using FileStream fs = new FileStream(path, FileMode.Create);
            opaqueTexture.SaveAsPng(fs, opaqueTexture.Width, opaqueTexture.Height);

            SetImageTags(filename, (tags ?? Enumerable.Empty<string>()).ToList());
            EnforcePhotoRetention(folderPath, GetPhotoRetentionLimit(isPlayerPhoto), path);
            if (isPlayerPhoto)
                Game1.addHUDMessage(new HUDMessage("Photo saved!", HUDMessage.newQuest_type));
            return path;
        }

        private static int GetPhotoRetentionLimit(bool isPlayerPhoto)
        {
            int configuredLimit = isPlayerPhoto
                ? Config?.PlayerMaxPhoto ?? 100
                : Config?.NpcMaxPhoto ?? 200;

            return Math.Clamp(configuredLimit, 1, 500);
        }

        private static void EnforcePhotoRetention(string folderPath, int maxPhotos, string keepPhotoPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                    return;

                int safeLimit = Math.Clamp(maxPhotos, 1, 500);
                string[] photoPaths = Directory.GetFiles(folderPath, "*.png");
                if (photoPaths.Length <= safeLimit)
                    return;

                int overflow = photoPaths.Length - safeLimit;
                var deletedPhotoNames = new List<string>();

                foreach (string photoPath in photoPaths
                             .OrderBy(path => File.GetLastWriteTimeUtc(path))
                             .ThenBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    if (deletedPhotoNames.Count >= overflow)
                        break;

                    if (!string.IsNullOrWhiteSpace(keepPhotoPath)
                        && string.Equals(photoPath, keepPhotoPath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        File.Delete(photoPath);
                        string? deletedPhotoName = Path.GetFileName(photoPath);
                        if (!string.IsNullOrWhiteSpace(deletedPhotoName))
                            deletedPhotoNames.Add(deletedPhotoName);
                    }
                    catch (Exception ex)
                    {
                        SMonitor.Log($"Failed to delete old photo '{photoPath}': {ex}", LogLevel.Warn);
                    }
                }

                RemoveImageTagsForDeletedPhotos(deletedPhotoNames);
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed to enforce photo retention for '{folderPath}': {ex}", LogLevel.Warn);
            }
        }

        private static void RemoveImageTagsForDeletedPhotos(IEnumerable<string> deletedPhotoNames)
        {
            bool hasChanges = false;

            foreach (string photoName in deletedPhotoNames ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(photoName))
                    continue;

                if (ImageTags.Remove(photoName))
                    hasChanges = true;
            }

            if (hasChanges)
                SaveImageTags();
        }

        private static xTile.Dimensions.Rectangle BuildNpcCaptureViewport(GameLocation location, Vector2 captureCenter, int captureWidth, int captureHeight)
        {
            object? map = GetMemberValue(location, "Map", "map");

            int mapWidth = map == null ? -1 : TryReadIntMember(map, "DisplayWidth", "displayWidth");
            int mapHeight = map == null ? -1 : TryReadIntMember(map, "DisplayHeight", "displayHeight");

            mapWidth = Math.Max(captureWidth, mapWidth);
            mapHeight = Math.Max(captureHeight, mapHeight);

            Vector2 pixelCenter = (captureCenter * Game1.tileSize) + new Vector2(Game1.tileSize / 2f, Game1.tileSize / 2f);
            int viewX = (int)Math.Round(pixelCenter.X) - (captureWidth / 2);
            int viewY = (int)Math.Round(pixelCenter.Y) - (captureHeight / 2);

            viewX = Math.Clamp(viewX, 0, Math.Max(0, mapWidth - captureWidth));
            viewY = Math.Clamp(viewY, 0, Math.Max(0, mapHeight - captureHeight));

            return new xTile.Dimensions.Rectangle(viewX, viewY, captureWidth, captureHeight);
        }

        private sealed class PhotoRenderStateSnapshot
        {
            private readonly xTile.Dimensions.Rectangle viewport;
            private readonly Viewport graphicsViewport;
            private readonly GameLocation currentLocation;
            private readonly int timeOfDay;
            private readonly Color ambientLight;
            private readonly Color outdoorLight;
            private readonly bool drawLighting;
            private readonly string? viewingLocation;
            private readonly bool screenGlow;
            private readonly bool screenGlowHold;
            private readonly bool screenGlowUp;
            private readonly float screenGlowAlpha;
            private readonly float screenGlowRate;
            private readonly float screenGlowMax;
            private readonly Color screenGlowColor;
            private readonly RenderTarget2D? lightmap;
            private readonly Dictionary<string, LightSource> lightSources = new Dictionary<string, LightSource>(StringComparer.Ordinal);

            public PhotoRenderStateSnapshot()
            {
                viewport = Game1.viewport;
                graphicsViewport = Game1.graphics.GraphicsDevice.Viewport;
                currentLocation = Game1.currentLocation;
                timeOfDay = Game1.timeOfDay;
                ambientLight = Game1.ambientLight;
                outdoorLight = Game1.outdoorLight;
                drawLighting = Game1.drawLighting;
                viewingLocation = Game1.player?.viewingLocation?.Value;
                screenGlow = Game1.screenGlow;
                screenGlowHold = Game1.screenGlowHold;
                screenGlowUp = Game1.screenGlowUp;
                screenGlowAlpha = Game1.screenGlowAlpha;
                screenGlowRate = Game1.screenGlowRate;
                screenGlowMax = Game1.screenGlowMax;
                screenGlowColor = Game1.screenGlowColor;
                lightmap = Game1.lightmap;

                foreach (KeyValuePair<string, LightSource> lightSource in Game1.currentLightSources)
                    lightSources[lightSource.Key] = lightSource.Value;
            }

            public void Restore()
            {
                RenderTarget2D? captureLightmap = Game1.lightmap;
                if (!ReferenceEquals(captureLightmap, lightmap))
                {
                    if (TrySetStaticMemberValue(typeof(Game1), lightmap, "_lightmap") && captureLightmap != null)
                        captureLightmap.Dispose();
                }

                Game1.graphics.GraphicsDevice.Viewport = graphicsViewport;
                Game1.viewport = viewport;
                Game1.currentLocation = currentLocation;
                Game1.timeOfDay = timeOfDay;
                Game1.ambientLight = ambientLight;
                Game1.outdoorLight = outdoorLight;
                Game1.drawLighting = drawLighting;
                if (Game1.player?.viewingLocation != null)
                    Game1.player.viewingLocation.Value = viewingLocation;
                Game1.screenGlow = screenGlow;
                Game1.screenGlowHold = screenGlowHold;
                Game1.screenGlowUp = screenGlowUp;
                Game1.screenGlowAlpha = screenGlowAlpha;
                Game1.screenGlowRate = screenGlowRate;
                Game1.screenGlowMax = screenGlowMax;
                Game1.screenGlowColor = screenGlowColor;

                Game1.currentLightSources.Clear();
                foreach (KeyValuePair<string, LightSource> lightSource in lightSources)
                    Game1.currentLightSources[lightSource.Key] = lightSource.Value;
            }
        }

        private static void PrepareLocationRenderState(GameLocation targetLocation, int captureWidth, int captureHeight)
        {
            SetViewingLocationForCapture(targetLocation);
            targetLocation.setUpLocationSpecificFlair();
            AllocateLightmapForCapture(captureWidth, captureHeight);

            Game1.currentLightSources.Clear();

            RefreshAmbientLightForCapture(targetLocation);
            RefreshOutdoorLightForCapture(targetLocation);
            if (!targetLocation.ignoreLights.Value)
                AddMapLightsForCapture(targetLocation);
            AddSharedLightsForCapture(targetLocation);
            RefreshDrawLightingForCapture(targetLocation);
        }

        private static void SetViewingLocationForCapture(GameLocation targetLocation)
        {
            if (Game1.player?.viewingLocation == null)
                return;

            string targetViewName = string.IsNullOrWhiteSpace(targetLocation.Name)
                ? targetLocation.NameOrUniqueName
                : targetLocation.Name;
            Game1.player.viewingLocation.Value = targetViewName;
        }

        private static void RefreshAmbientLightForCapture(GameLocation targetLocation)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo? updateAmbientLighting = typeof(GameLocation).GetMethod("_updateAmbientLighting", flags);
            if (updateAmbientLighting != null)
            {
                updateAmbientLighting.Invoke(targetLocation, null);
                return;
            }

            if (targetLocation.IsOutdoors && !targetLocation.ignoreOutdoorLighting.Value)
                Game1.ambientLight = targetLocation.IsRainingHere() ? new Color(255, 200, 80) : Color.White;
        }

        private static void RefreshOutdoorLightForCapture(GameLocation targetLocation)
        {
            if (Game1.timeOfDay >= Game1.getTrulyDarkTime(targetLocation))
            {
                int interpolatedTime = (int)((float)(Game1.timeOfDay - Game1.timeOfDay % 100) + ((Game1.timeOfDay % 100) / 10f) * 16.66f);
                float darkness = Math.Min(0.93f, 0.75f + ((interpolatedTime - Game1.getTrulyDarkTime(targetLocation)) + ((float)Game1.gameTimeInterval / Game1.realMilliSecondsPerGameTenMinutes * 16.6f)) * 0.000625f);
                Game1.outdoorLight = (targetLocation.IsRainingHere() ? Game1.ambientLight : Game1.eveningColor) * darkness;
                return;
            }

            if (Game1.timeOfDay >= Game1.getStartingToGetDarkTime(targetLocation))
            {
                int interpolatedTime = (int)((float)(Game1.timeOfDay - Game1.timeOfDay % 100) + ((Game1.timeOfDay % 100) / 10f) * 16.66f);
                float darkness = Math.Min(0.93f, 0.3f + ((interpolatedTime - Game1.getStartingToGetDarkTime(targetLocation)) + ((float)Game1.gameTimeInterval / Game1.realMilliSecondsPerGameTenMinutes * 16.6f)) * 0.00225f);
                Game1.outdoorLight = (targetLocation.IsRainingHere() ? Game1.ambientLight : Game1.eveningColor) * darkness;
                return;
            }

            Game1.outdoorLight = targetLocation.IsRainingHere()
                ? Game1.ambientLight * 0.3f
                : Game1.ambientLight;
        }

        private static void RefreshDrawLightingForCapture(GameLocation targetLocation)
        {
            bool shouldDrawLighting = (targetLocation.IsOutdoors && !Game1.outdoorLight.Equals(Color.White))
                || !Game1.ambientLight.Equals(Color.White);

            if (targetLocation is MineShaft mineShaft && !mineShaft.getLightingColor(Game1.currentGameTime).Equals(Color.White))
                shouldDrawLighting = true;

            if (Game1.player.hasBuff("26"))
                shouldDrawLighting = true;

            Game1.drawLighting = shouldDrawLighting;
        }

        private static void AllocateLightmapForCapture(int captureWidth, int captureHeight)
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            MethodInfo? allocateLightmap = typeof(Game1).GetMethod("allocateLightmap", flags);
            if (allocateLightmap == null)
                return;

            if (Game1.lightmap != null && !TrySetStaticMemberValue(typeof(Game1), null, "_lightmap"))
                return;

            allocateLightmap.Invoke(null, new object[] { captureWidth, captureHeight });
        }

        private static void RestoreTemporarySpritesAfterCapture(GameLocation targetLocation, int temporarySpriteCount)
        {
            while (targetLocation.temporarySprites.Count > temporarySpriteCount)
                targetLocation.temporarySprites.RemoveAt(targetLocation.temporarySprites.Count - 1);
        }

        private static void AddMapLightsForCapture(GameLocation targetLocation)
        {
            string lightIdPrefix = $"{targetLocation.NameOrUniqueName}_MapLight_";
            AddMapPropertyLights(targetLocation, lightIdPrefix, "Light", LightSource.LightContext.MapLight);

            if (!Game1.isTimeToTurnOffLighting(targetLocation) && !targetLocation.IsRainingHere())
            {
                AddMapPropertyLights(targetLocation, lightIdPrefix, "WindowLight", LightSource.LightContext.WindowLight);

                foreach (Vector2 lightGlow in targetLocation.lightGlows)
                {
                    Game1.currentLightSources.Add(new LightSource(
                        $"{lightIdPrefix}_{lightGlow.X}_{lightGlow.Y}_Glow",
                        6,
                        lightGlow,
                        1f,
                        LightSource.LightContext.WindowLight,
                        0L,
                        targetLocation.NameOrUniqueName));
                }
            }
        }

        private static void AddMapPropertyLights(GameLocation targetLocation, string lightIdPrefix, string propertyName, LightSource.LightContext context)
        {
            string[] propertyValues = targetLocation.GetMapPropertySplitBySpaces(propertyName);
            for (int i = 0; i + 2 < propertyValues.Length; i += 3)
            {
                if (!int.TryParse(propertyValues[i], out int tileX)
                    || !int.TryParse(propertyValues[i + 1], out int tileY)
                    || !int.TryParse(propertyValues[i + 2], out int textureIndex))
                {
                    continue;
                }

                Vector2 position = new Vector2((tileX * 64) + 32, (tileY * 64) + 32);
                string suffix = context == LightSource.LightContext.WindowLight ? "_Window" : string.Empty;

                Game1.currentLightSources.Add(new LightSource(
                    $"{lightIdPrefix}_{tileX}_{tileY}{suffix}",
                    textureIndex,
                    position,
                    1f,
                    context,
                    0L,
                    targetLocation.NameOrUniqueName));
            }
        }

        private static void AddSharedLightsForCapture(GameLocation targetLocation)
        {
            foreach (KeyValuePair<string, LightSource> sharedLight in targetLocation.sharedLights.Pairs)
                Game1.currentLightSources[sharedLight.Key] = sharedLight.Value;
        }

        private static Texture2D CreateOpaqueTexture(Texture2D source)
        {
            Color[] rawData = new Color[source.Width * source.Height];
            source.GetData(rawData);

            for (int i = 0; i < rawData.Length; i++)
            {
                Color pixel = rawData[i];
                if (pixel.A != byte.MaxValue)
                    rawData[i] = new Color(pixel.R, pixel.G, pixel.B, byte.MaxValue);
            }

            Texture2D opaqueTexture = new Texture2D(source.GraphicsDevice, source.Width, source.Height);
            opaqueTexture.SetData(rawData);
            return opaqueTexture;
        }
    }
}
