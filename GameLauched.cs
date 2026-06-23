using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ContentPatcher;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp.Processing;
using Smartphone.Data;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Delegates;
using StardewValley.Extensions;
using StardewValley.GameData.Characters;
using StardewValley.GameData.Crops;
using StardewValley.GameData.LocationContexts;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Minigames;
using StardewValley.Monsters;
using StardewValley.Network;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using StardewValley.Triggers;
using xTile.Dimensions;
using xTile.Tiles;
using static StardewValley.Minigames.MineCart;

namespace Smartphone
{
    /// <summary>The mod entry point.</summary>
    public partial class ModEntry
    {
        internal const int PhoneFrameBaseWidth = 600;
        internal const int PhoneFrameBaseHeight = 1000;
        internal const int PhoneDefaultMenuOffsetX = 400;
        internal const int PhoneDefaultMenuOffsetY = 500;
        internal const int PhoneFrameContentOffsetX = 40;
        internal const int PhoneFrameContentOffsetY = 116;
        internal const float PhoneSmallUiScale = 0.75f;

        private const int CameraViewportOffsetX = PhoneFrameContentOffsetX;
        private const int CameraViewportOffsetY = PhoneFrameContentOffsetY;
        private const int CameraViewportWidth = 520;
        private const int CameraViewportHeight = 810;
        private const int HudPhoneMaxHeight = 150;
        private const int HudPhoneMinHeight = 96;
        private const int HudPhoneRightMargin = 18;
        private const int HudPhoneTopMargin = 12;
        private const int HudPhoneBottomMargin = 12;
        private const int HudPhoneAboveEnergyOffset = 188;
        private const int HudPhoneBadgeMinimumSize = 20;
        private const int HudPhoneFrameContentOffsetX = 40;
        private const int HudPhoneFrameContentOffsetY = 116;
        private string hudPhoneBackgroundImagePath = string.Empty;
        private Texture2D? hudPhoneBackgroundImage;
        private int hudPhoneBackgroundImageTargetWidth = 0;
        private int hudPhoneBackgroundImageTargetHeight = 0;

        // *************************** ENTRY ***************************
        //


        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            Config = Helper.ReadConfig<ModConfig>();


            ModEntry.Instance = this;


            SMonitor = Monitor;
            SHelper = helper;


            Textures.LoadTextures();
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.GameLoop.GameLaunched += OnGameLauched;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.Saving += OnSaving;
            helper.Events.GameLoop.TimeChanged += OnTimeChange;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.ReturnedToTitle += OnReturnedToTitle;
            helper.Events.Display.WindowResized += OnWindowResized;
            helper.Events.Display.Rendered += OnRendered;
            helper.Events.Display.RenderedHud += OnRenderedHud;

            // dev tool: prepare for grid overlay
            solidPixel = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1);
            solidPixel.SetData(new[] { Color.White });


        }





        //
        // ***************************  END OF ENTRY ***************************
        //

        private void OnGameLauched(object sender, StardewModdingAPI.Events.GameLaunchedEventArgs e)
        {
            this.Monitor.Log("Loading Smartphone", LogLevel.Info);
            var api = this.Helper.ModRegistry.GetApi<IContentPatcherAPI>("Pathoschild.ContentPatcher");

            ConfigMenu(api, this.ModManifest, Helper);

            AppStoreManager.Initialize();

        }



        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;


            bool canOpenPhoneMenu = Game1.activeClickableMenu == null && Game1.currentMinigame == null;

            if (e.Button == Config.ModKey && canOpenPhoneMenu)
            {
                OpenPhoneFromHudTrigger();
                return;
            }

            if (e.Button == SButton.MouseLeft
                && canOpenPhoneMenu
                && ShouldDrawHudPhoneIcon()
                && GetHudPhoneIconBounds().Contains(Game1.getMouseX(true), Game1.getMouseY(true)))
            {
                OpenPhoneFromHudTrigger();
                Helper.Input.Suppress(e.Button);
                return;
            }

            // DEVTOOL
            // if (e.Button == SButton.O && Game1.activeClickableMenu == null && true)
            // {
            //     isGridVisible = !isGridVisible;
            //     Game1.chatBox.addInfoMessage($"Grid {(isGridVisible ? "enabled" : "disabled")}.");
            //     ToggleGrid(e);
            //     firstClickTile = null;
            //     return;
            // }
            // if (e.Button == SButton.MouseLeft)
            // {
            //     var tile = e.Cursor.Tile;
            //     Game1.chatBox.addErrorMessage((IsWalkableWarpTile(Game1.currentLocation, (int)tile.X, (int)tile.Y) && IsWalkableWarpTile(Game1.currentLocation, (int)tile.X, (int)tile.Y - 1)).ToString());
            // }
        }


        private void OnSaving(object sender, SavingEventArgs e)
        {
            if (phoneMenu != null)
                phoneMenu.ClosePhoneMenu();
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            RefreshActiveSaveFolderName();
            RefreshInitStateForCurrentSave();
            hasNewVersionAvailable = false;

            LoadImageTags();

            string targetModId = this.ModManifest.UniqueID;
            var modInfo = this.Helper.ModRegistry.Get(targetModId);

            if (modInfo != null)
            {
                Task.Run(async () =>
                {
                    bool hasNewerVersion = await CheckForNewerVersion(modInfo);
                    hasNewVersionAvailable = hasNewerVersion;

                    if (hasNewerVersion && !Config.DisableUpdateWarning)
                    {
                        DelayedAction.functionAfterDelay(() =>
                        {
                            try
                            {
                                SMonitor.Log($"Smartphone: Newer version available", LogLevel.Warn);
                                Game1.drawLetterMessage(ModEntry.SHelper.Translation.Get("mail.update_warning"));

                                NotificationManager.AddNotification(ModEntry.SHelper.Translation.Get("notification.update_warning"));
                            }
                            catch (Exception ex)
                            {
                                SMonitor.Log($"Smartphone: Unable to notify about newer version: {ex}", LogLevel.Trace);
                            }
                        }, 10000);
                    }
                });
            }


        }



        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            NotificationManager.LoadNotificationData();
            PhoneMenu.RefreshCalendarData();
        }



        private void ApplySavedPhoneTheme()
        {
            string resolvedThemeName = AssetHelper.ResolvePhoneThemeName(ModEntry.currentPhoneTheme);
            ModEntry.currentPhoneTheme = resolvedThemeName;

            AssetHelper.SetCurrentPhoneTheme(resolvedThemeName);
            Textures.LoadTextures();

            if (phoneMenu != null)
                phoneMenu.ReloadThemeTextures();
        }

        private void OnReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
        {
            ClearActiveSaveFolderName();

            pendingInitNotification = false;
            pendingPhoneOsInitialization = false;
            hasNewVersionAvailable = false;

            DisposeHudPhoneBackgroundImage();
            hudPhoneBackgroundImagePath = string.Empty;
            hudPhoneBackgroundImageTargetWidth = 0;
            hudPhoneBackgroundImageTargetHeight = 0;
        }

        private void OnTimeChange(object sender, TimeChangedEventArgs e)
        {
            if (pendingInitNotification && Context.IsPlayerFree)
            {
                Game1.drawLetterMessage(ModEntry.SHelper.Translation.Get("mail.first_time"));

                NotificationManager.AddNotification(ModEntry.SHelper.Translation.Get("notification.first_time"));

                pendingInitNotification = false;
            }
        }


        private void OnWindowResized(object sender, WindowResizedEventArgs e)
        {
            if (phoneMenu == null)
                return;

            phoneMenu.ResetToDefaultPosition();
        }

        private void OnRenderedHud(object sender, RenderedHudEventArgs e)
        {
            if (!ShouldDrawHudPhoneIcon())
                return;

            DrawHudPhoneIcon(e.SpriteBatch);
        }

        private static bool ShouldDrawHudPhoneIcon()
        {
            return Config.ShowPhoneIcon
                && Context.IsWorldReady
                && Game1.displayHUD
                && Game1.activeClickableMenu == null
                && Game1.currentMinigame == null;
        }

        internal static float GetConfiguredPhoneUiScale()
        {
            return Config?.UseSmallPhoneSize == true
                ? PhoneSmallUiScale
                : 1f;
        }

        internal static float GetActivePhoneUiScale()
        {
            if (phoneMenu != null)
                return phoneMenu.PhoneUiScale;

            return GetConfiguredPhoneUiScale();
        }

        internal static int ScalePhoneUiValue(int baseValue, float scale)
        {
            return (int)Math.Round(baseValue * scale);
        }

        private static float ResolvePhoneUiScale(float? scale)
        {
            return scale ?? GetConfiguredPhoneUiScale();
        }

        internal static int GetScaledPhoneFrameWidth(float? scale = null)
        {
            return Math.Max(1, ScalePhoneUiValue(PhoneFrameBaseWidth, ResolvePhoneUiScale(scale)));
        }

        internal static int GetScaledPhoneFrameHeight(float? scale = null)
        {
            return Math.Max(1, ScalePhoneUiValue(PhoneFrameBaseHeight, ResolvePhoneUiScale(scale)));
        }

        internal static int GetScaledPhoneDefaultMenuOffsetX(float? scale = null)
        {
            return ScalePhoneUiValue(PhoneDefaultMenuOffsetX, ResolvePhoneUiScale(scale));
        }

        internal static int GetScaledPhoneDefaultMenuOffsetY(float? scale = null)
        {
            return ScalePhoneUiValue(PhoneDefaultMenuOffsetY, ResolvePhoneUiScale(scale));
        }

        internal static int GetScaledPhoneContentOffsetX(float? scale = null)
        {
            return ScalePhoneUiValue(PhoneFrameContentOffsetX, ResolvePhoneUiScale(scale));
        }

        internal static int GetScaledPhoneContentOffsetY(float? scale = null)
        {
            return ScalePhoneUiValue(PhoneFrameContentOffsetY, ResolvePhoneUiScale(scale));
        }

        internal static int GetScaledCameraViewportWidth(float? scale = null)
        {
            return Math.Max(1, ScalePhoneUiValue(CameraViewportWidth, ResolvePhoneUiScale(scale)));
        }

        internal static int GetScaledCameraViewportHeight(float? scale = null)
        {
            return Math.Max(1, ScalePhoneUiValue(CameraViewportHeight, ResolvePhoneUiScale(scale)));
        }

        internal static void EnsurePhoneMenuUsesCurrentScale()
        {
            float configuredScale = GetConfiguredPhoneUiScale();
            if (phoneMenu == null || !phoneMenu.UsesPhoneUiScale(configuredScale))
                phoneMenu = new PhoneMenu();
        }

        public static void OpenPhoneFromHudTrigger()
        {
            EnsurePhoneMenuUsesCurrentScale();

            phoneMenu.OpenLockScreen();
            Game1.activeClickableMenu = phoneMenu;
        }

        private Microsoft.Xna.Framework.Rectangle GetHudPhoneIconBounds()
        {
            Texture2D? frameTexture = Textures.PhoneEmpty;
            if (frameTexture == null || frameTexture.IsDisposed)
                return Microsoft.Xna.Framework.Rectangle.Empty;

            int viewportWidth = Math.Max(1, Game1.uiViewport.Width);
            int viewportHeight = Math.Max(1, Game1.uiViewport.Height);

            int iconHeight = Math.Clamp(viewportHeight / 7, HudPhoneMinHeight, HudPhoneMaxHeight);
            int iconWidth = Math.Max(1, (int)Math.Round(frameTexture.Width * (iconHeight / (float)Math.Max(1, frameTexture.Height))));

            int x = viewportWidth - iconWidth - HudPhoneRightMargin;
            int aboveEnergyOffset = Math.Max(HudPhoneAboveEnergyOffset, viewportHeight / 5);
            int y = viewportHeight - iconHeight - aboveEnergyOffset;

            int configuredOffsetX = Math.Clamp(Config?.HudPhoneIconOffsetX ?? 0, -2000, 2000);
            int configuredOffsetY = Math.Clamp(Config?.HudPhoneIconOffsetY ?? 0, -2000, 2000);

            x += configuredOffsetX;
            y += configuredOffsetY;

            x = Math.Clamp(x, HudPhoneTopMargin, Math.Max(HudPhoneTopMargin, viewportWidth - iconWidth - HudPhoneTopMargin));
            y = Math.Clamp(y, HudPhoneTopMargin, Math.Max(HudPhoneTopMargin, viewportHeight - iconHeight - HudPhoneBottomMargin));

            return new Microsoft.Xna.Framework.Rectangle(x, y, iconWidth, iconHeight);
        }

        private void DrawHudPhoneIcon(SpriteBatch spriteBatch)
        {
            Texture2D? frameTexture = Textures.PhoneEmpty;
            Texture2D? backgroundTexture = Textures.PhoneBackground;
            if (frameTexture == null || backgroundTexture == null || frameTexture.IsDisposed || backgroundTexture.IsDisposed)
                return;

            Microsoft.Xna.Framework.Rectangle iconBounds = GetHudPhoneIconBounds();
            if (iconBounds.Width <= 0 || iconBounds.Height <= 0)
                return;

            RefreshHudPhoneBackgroundImage();

            float iconScale = iconBounds.Height / (float)Math.Max(1, frameTexture.Height);
            Microsoft.Xna.Framework.Rectangle contentBounds = new Microsoft.Xna.Framework.Rectangle(
                iconBounds.X + (int)Math.Round(HudPhoneFrameContentOffsetX * iconScale),
                iconBounds.Y + (int)Math.Round(HudPhoneFrameContentOffsetY * iconScale),
                Math.Max(1, (int)Math.Round(backgroundTexture.Width * iconScale)),
                Math.Max(1, (int)Math.Round(backgroundTexture.Height * iconScale)));

            spriteBatch.Draw(backgroundTexture, contentBounds, Color.White);

            if (hudPhoneBackgroundImage != null && !hudPhoneBackgroundImage.IsDisposed)
                spriteBatch.Draw(hudPhoneBackgroundImage, contentBounds, Color.White * 0.8f);

            string timeText = Game1.getTimeOfDayString(Game1.timeOfDay);
            float textScale = Math.Clamp(iconScale * 0.56f, 0.35f, 0.8f);
            Vector2 textSize = Game1.smallFont.MeasureString(timeText) * textScale;
            Vector2 textPosition = new Vector2(
                contentBounds.Center.X - (textSize.X / 2f),
                contentBounds.Y + Math.Max(4f, 15f * iconScale));

            spriteBatch.DrawString(
                Game1.smallFont,
                timeText,
                textPosition + new Vector2(1f, 1f),
                new Color(0, 0, 0, 175),
                0f,
                Vector2.Zero,
                textScale,
                SpriteEffects.None,
                1f);

            spriteBatch.DrawString(
                Game1.smallFont,
                timeText,
                textPosition,
                Color.White,
                0f,
                Vector2.Zero,
                textScale,
                SpriteEffects.None,
                1f);

            spriteBatch.Draw(frameTexture, iconBounds, Color.White);

            if (HasAnyHudPhoneAlert())
                DrawHudPhoneAlertBadge(spriteBatch, iconBounds);

            if (iconBounds.Contains(Game1.getMouseX(), Game1.getMouseY()))
                DrawHudPhoneIconHoverOutline(spriteBatch, iconBounds);
        }

        private static bool HasAnyHudPhoneAlert()
        {
            if (NotificationManager.GetUnreadNotification() > 0)
                return true;

            return false;
        }

        private static void DrawHudPhoneAlertBadge(SpriteBatch spriteBatch, Microsoft.Xna.Framework.Rectangle iconBounds)
        {
            int badgeSize = Math.Max(HudPhoneBadgeMinimumSize, iconBounds.Width / 3);
            int badgeX = iconBounds.Right - badgeSize + 6;
            int badgeY = Math.Max(2, iconBounds.Y - (badgeSize / 2) + 8);

            IClickableMenu.drawTextureBox(
                spriteBatch,
                Game1.menuTexture,
                new Microsoft.Xna.Framework.Rectangle(0, 256, 60, 60),
                badgeX,
                badgeY,
                badgeSize,
                badgeSize,
                new Color(222, 44, 44, 235),
                1f,
                false);

            const string alertSymbol = "!";
            float textScale = Math.Clamp(badgeSize / 28f, 0.75f, 1.15f);
            Vector2 textSize = Game1.smallFont.MeasureString(alertSymbol) * textScale;
            Vector2 textPosition = new Vector2(
                badgeX + ((badgeSize - textSize.X) / 2f),
                badgeY + ((badgeSize - textSize.Y) / 2f) - 1f);

            spriteBatch.DrawString(
                Game1.smallFont,
                alertSymbol,
                textPosition + new Vector2(1f, 1f),
                new Color(0, 0, 0, 175),
                0f,
                Vector2.Zero,
                textScale,
                SpriteEffects.None,
                1f);

            spriteBatch.DrawString(
                Game1.smallFont,
                alertSymbol,
                textPosition,
                Color.White,
                0f,
                Vector2.Zero,
                textScale,
                SpriteEffects.None,
                1f);
        }

        private static void DrawHudPhoneIconHoverOutline(SpriteBatch spriteBatch, Microsoft.Xna.Framework.Rectangle iconBounds)
        {
            Microsoft.Xna.Framework.Rectangle outlineBounds = new Microsoft.Xna.Framework.Rectangle(
                iconBounds.X - 2,
                iconBounds.Y - 2,
                iconBounds.Width + 4,
                iconBounds.Height + 4);

            Color outlineColor = Color.White * 0.65f;

            spriteBatch.Draw(Game1.staminaRect, new Microsoft.Xna.Framework.Rectangle(outlineBounds.X, outlineBounds.Y, outlineBounds.Width, 2), outlineColor);
            spriteBatch.Draw(Game1.staminaRect, new Microsoft.Xna.Framework.Rectangle(outlineBounds.X, outlineBounds.Bottom - 2, outlineBounds.Width, 2), outlineColor);
            spriteBatch.Draw(Game1.staminaRect, new Microsoft.Xna.Framework.Rectangle(outlineBounds.X, outlineBounds.Y, 2, outlineBounds.Height), outlineColor);
            spriteBatch.Draw(Game1.staminaRect, new Microsoft.Xna.Framework.Rectangle(outlineBounds.Right - 2, outlineBounds.Y, 2, outlineBounds.Height), outlineColor);
        }

        private void RefreshHudPhoneBackgroundImage()
        {
            Texture2D? backgroundTexture = Textures.PhoneBackground;
            int targetWidth = Math.Max(1, backgroundTexture?.Width ?? 520);
            int targetHeight = Math.Max(1, backgroundTexture?.Height ?? 810);

            string imagePath = (ModEntry.currentPhoneBackground ?? string.Empty).Trim();
            bool pathChanged = !string.Equals(hudPhoneBackgroundImagePath, imagePath, StringComparison.OrdinalIgnoreCase);
            bool sizeChanged = hudPhoneBackgroundImageTargetWidth != targetWidth || hudPhoneBackgroundImageTargetHeight != targetHeight;

            if (!pathChanged && !sizeChanged)
                return;

            DisposeHudPhoneBackgroundImage();

            hudPhoneBackgroundImagePath = imagePath;
            hudPhoneBackgroundImageTargetWidth = targetWidth;
            hudPhoneBackgroundImageTargetHeight = targetHeight;

            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                return;

            try
            {
                using FileStream stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using Texture2D sourceImage = Texture2D.FromStream(Game1.graphics.GraphicsDevice, stream);
                hudPhoneBackgroundImage = FitTextureToTargetSize(sourceImage, targetWidth, targetHeight);
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed to load HUD phone background image '{imagePath}': {ex.Message}", LogLevel.Trace);
            }
        }

        private void DisposeHudPhoneBackgroundImage()
        {
            if (hudPhoneBackgroundImage != null && !hudPhoneBackgroundImage.IsDisposed)
                hudPhoneBackgroundImage.Dispose();

            hudPhoneBackgroundImage = null;
        }

        private static Texture2D FitTextureToTargetSize(Texture2D sourceTexture, int targetWidth, int targetHeight)
        {
            int safeTargetWidth = Math.Max(1, targetWidth);
            int safeTargetHeight = Math.Max(1, targetHeight);

            int sourceWidth = Math.Max(1, sourceTexture.Width);
            int sourceHeight = Math.Max(1, sourceTexture.Height);

            Color[] sourceData = new Color[sourceWidth * sourceHeight];
            sourceTexture.GetData(sourceData);

            Color[] targetData = new Color[safeTargetWidth * safeTargetHeight];

            float scale = Math.Min(safeTargetWidth / (float)sourceWidth, safeTargetHeight / (float)sourceHeight);
            int drawWidth = Math.Max(1, (int)Math.Round(sourceWidth * scale));
            int drawHeight = Math.Max(1, (int)Math.Round(sourceHeight * scale));
            int drawOffsetX = (safeTargetWidth - drawWidth) / 2;
            int drawOffsetY = (safeTargetHeight - drawHeight) / 2;

            for (int drawY = 0; drawY < drawHeight; drawY++)
            {
                int sourceY = Math.Min(sourceHeight - 1, (int)(drawY * (sourceHeight / (float)drawHeight)));
                int targetY = drawOffsetY + drawY;

                for (int drawX = 0; drawX < drawWidth; drawX++)
                {
                    int sourceX = Math.Min(sourceWidth - 1, (int)(drawX * (sourceWidth / (float)drawWidth)));
                    int targetX = drawOffsetX + drawX;
                    targetData[targetY * safeTargetWidth + targetX] = sourceData[sourceY * sourceWidth + sourceX];
                }
            }

            Texture2D fittedTexture = new Texture2D(Game1.graphics.GraphicsDevice, safeTargetWidth, safeTargetHeight);
            fittedTexture.SetData(targetData);
            return fittedTexture;
        }


        private static async Task<(bool IsLatest, string? LatestVersion, string? LatestUrl)> CheckForModUpdate(IModInfo modInfo)
        {
            if (modInfo?.Manifest == null)
                return (true, null, null);

            var request = new
            {
                mods = new[]
                {
                    new
                    {
                        id = modInfo.Manifest.UniqueID,
                        updateKeys = modInfo.Manifest.UpdateKeys,
                        installedVersion = modInfo.Manifest.Version.ToString(),
                        isBroken = false
                    }
                },
                apiVersion = Constants.ApiVersion.ToString(),
                gameVersion = Game1.version.ToString(),
                platform = Constants.TargetPlatform.ToString(),
                includeExtendedMetadata = false
            };
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            using var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(request, jsonOptions),
                Encoding.UTF8,
                "application/json"
            );

            using HttpResponseMessage response = await new HttpClient().PostAsync($"https://smapi.io/api/v{Constants.ApiVersion}/mods", content);
            response.EnsureSuccessStatusCode();

            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            JsonElement result = document.RootElement[0];

            if (result.TryGetProperty("suggestedUpdate", out JsonElement suggestedUpdate) && suggestedUpdate.ValueKind == JsonValueKind.Object)
            {
                return (
                    false,
                    suggestedUpdate.GetProperty("version").GetString(),
                    suggestedUpdate.GetProperty("url").GetString()
                );
            }

            return (true, null, null);
        }

        public static async Task<bool> CheckForNewerVersion(IModInfo? modInfo)
        {
            if (modInfo?.Manifest == null)
                return false;

            var update = await CheckForModUpdate(modInfo);
            return !update.IsLatest;
        }

        public static void RefreshInitStateForCurrentSave()
        {
            string saveFolderPath = Path.Combine(
                SHelper.DirectoryPath,
                "userdata",
                GetActiveSaveFolderName());

            bool isFirstTimeForSave = !Directory.Exists(saveFolderPath);
            if (isFirstTimeForSave)
            {
                Directory.CreateDirectory(saveFolderPath);
            }

            pendingInitNotification = isFirstTimeForSave;
            pendingPhoneOsInitialization = isFirstTimeForSave;
        }

    }
}
