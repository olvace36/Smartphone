using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace Smartphone
{
    public static class Textures
    {
        public static Texture2D PhoneBackground;
        public static Texture2D PhoneEmpty;
        public static Texture2D Background;
        public static Texture2D GroupBackground;
        public static Texture2D CardTexture;
        public static Texture2D DockedMenuBackground;

        public static Texture2D AppCamera;
        public static Texture2D AppPhoto;
        public static Texture2D AppSetting;
        public static Texture2D AppNotification;
        public static Texture2D AppAppStore;
        public static Texture2D AppCalendar;

        // In-memory cache to keep CPU/RAM footprint flat and eliminate draw-loop lag
        public static readonly Dictionary<string, Texture2D> AppTextureCache = new(StringComparer.OrdinalIgnoreCase);

        public static void LoadTextures()
        {
            // Clear cache when user swaps themes so textures refresh instantly
            AppTextureCache.Clear();

            try
            {
                string phonePath = AssetHelper.GetComponentThemeFolderPath("phone");

                PhoneEmpty = TryLoadWithFallback(Path.Combine(phonePath, "phone_edge.png"), Path.Combine(AssetHelper.GetPhoneThemesRootPath(), "phone", "default", "phone_edge.png"));
                PhoneBackground = TryLoadWithFallback(Path.Combine(phonePath, "phone_screen.png"), Path.Combine(AssetHelper.GetPhoneThemesRootPath(), "phone", "default", "phone_screen.png"));
                Background = TryLoadWithFallback(Path.Combine(phonePath, "background.png"), Path.Combine(AssetHelper.GetPhoneThemesRootPath(), "phone", "default", "background.png"));
                GroupBackground = TryLoadWithFallback(Path.Combine(phonePath, "group_background.png"), Path.Combine(AssetHelper.GetPhoneThemesRootPath(), "phone", "default", "group_background.png"));
                CardTexture = TryLoadWithFallback(Path.Combine(phonePath, "card_texture.png"), Path.Combine(AssetHelper.GetPhoneThemesRootPath(), "phone", "default", "card_texture.png"));
                DockedMenuBackground = TryLoadWithFallback(Path.Combine(phonePath, "docked_menu.png"), Path.Combine(AssetHelper.GetPhoneThemesRootPath(), "phone", "default", "docked_menu.png"));

                // Pre-populate standard 1x1 slots
                AppCamera = GetAppTexture("builtin:camera", AppSize.Size1x1);
                AppPhoto = GetAppTexture("builtin:photo", AppSize.Size1x1);
                AppSetting = GetAppTexture("builtin:setting", AppSize.Size1x1);
                AppNotification = GetAppTexture("builtin:notification", AppSize.Size1x1);
                AppAppStore = GetAppTexture("builtin:appstore", AppSize.Size1x1);
                AppCalendar = GetAppTexture("builtin:calendar", AppSize.Size1x1);
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor?.Log($"Error resolving core smartphone graphic updates: {ex.Message}", LogLevel.Error);
            }
        }

        public static void DrawCard(SpriteBatch b, int x, int y, int width, int height, Color color, float scale = 1f, bool drawShadow = false, float draw_layer = -1f)
        {
            // Fallback to vanilla menu asset if no theme texture is loaded
            Texture2D texture = CardTexture ?? Game1.menuTexture;

            // If using vanilla texture, match its exact source region (0, 256, 60, 60)
            Rectangle sourceRect = CardTexture != null ? CardTexture.Bounds : new Rectangle(0, 256, 60, 60);

            // Exact game formula: cuts the asset into 3 equal columns/rows
            int num = sourceRect.Width / 3;

            // Exact game layer-depth calculation for sorting loops
            float layerDepth = draw_layer - 0.03f;
            if (draw_layer < 0f)
            {
                draw_layer = 0.8f - (float)y * 1E-06f;
                layerDepth = 0.77f;
            }

            // --- 1. DRAW VANILLA DROP SHADOW LAYER ---
            if (drawShadow)
            {
                Color shadowColor = Color.Black * 0.4f;

                // Corners Shadows
                b.Draw(texture, new Vector2(x + width - (int)((float)num * scale) - 8, y + 8), new Rectangle(sourceRect.X + num * 2, sourceRect.Y, num, num), shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, layerDepth);
                b.Draw(texture, new Vector2(x - 8, y + height - (int)((float)num * scale) + 8), new Rectangle(sourceRect.X, num * 2 + sourceRect.Y, num, num), shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, layerDepth);
                b.Draw(texture, new Vector2(x + width - (int)((float)num * scale) - 8, y + height - (int)((float)num * scale) + 8), new Rectangle(sourceRect.X + num * 2, num * 2 + sourceRect.Y, num, num), shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, layerDepth);

                // Edge Shadows
                b.Draw(texture, new Rectangle(x + (int)((float)num * scale) - 8, y + 8, width - (int)((float)num * scale) * 2, (int)((float)num * scale)), new Rectangle(sourceRect.X + num, sourceRect.Y, num, num), shadowColor, 0f, Vector2.Zero, SpriteEffects.None, layerDepth);
                b.Draw(texture, new Rectangle(x + (int)((float)num * scale) - 8, y + height - (int)((float)num * scale) + 8, width - (int)((float)num * scale) * 2, (int)((float)num * scale)), new Rectangle(sourceRect.X + num, num * 2 + sourceRect.Y, num, num), shadowColor, 0f, Vector2.Zero, SpriteEffects.None, layerDepth);
                b.Draw(texture, new Rectangle(x - 8, y + (int)((float)num * scale) + 8, (int)((float)num * scale), height - (int)((float)num * scale) * 2), new Rectangle(sourceRect.X, num + sourceRect.Y, num, num), shadowColor, 0f, Vector2.Zero, SpriteEffects.None, layerDepth);
                b.Draw(texture, new Rectangle(x + width - (int)((float)num * scale) - 8, y + (int)((float)num * scale) + 8, (int)((float)num * scale), height - (int)((float)num * scale) * 2), new Rectangle(sourceRect.X + num * 2, num + sourceRect.Y, num, num), shadowColor, 0f, Vector2.Zero, SpriteEffects.None, layerDepth);

                // Center Fill Shadow
                b.Draw(texture, new Rectangle((int)((float)num * scale / 2f) + x - 8, (int)((float)num * scale / 2f) + y + 8, width - (int)((float)num * scale), height - (int)((float)num * scale)), new Rectangle(num + sourceRect.X, num + sourceRect.Y, num, num), shadowColor, 0f, Vector2.Zero, SpriteEffects.None, layerDepth);
            }

            // --- 2. DRAW MAIN FOREGROUND CARD BOX ---
            // Center Fill
            b.Draw(texture, new Rectangle((int)((float)num * scale) + x, (int)((float)num * scale) + y, width - (int)((float)num * scale * 2f), height - (int)((float)num * scale * 2f)), new Rectangle(num + sourceRect.X, num + sourceRect.Y, num, num), color, 0f, Vector2.Zero, SpriteEffects.None, draw_layer);

            // Corners
            b.Draw(texture, new Vector2(x, y), new Rectangle(sourceRect.X, sourceRect.Y, num, num), color, 0f, Vector2.Zero, scale, SpriteEffects.None, draw_layer);
            b.Draw(texture, new Vector2(x + width - (int)((float)num * scale), y), new Rectangle(sourceRect.X + num * 2, sourceRect.Y, num, num), color, 0f, Vector2.Zero, scale, SpriteEffects.None, draw_layer);
            b.Draw(texture, new Vector2(x, y + height - (int)((float)num * scale)), new Rectangle(sourceRect.X, num * 2 + sourceRect.Y, num, num), color, 0f, Vector2.Zero, scale, SpriteEffects.None, draw_layer);
            b.Draw(texture, new Vector2(x + width - (int)((float)num * scale), y + height - (int)((float)num * scale)), new Rectangle(sourceRect.X + num * 2, num * 2 + sourceRect.Y, num, num), color, 0f, Vector2.Zero, scale, SpriteEffects.None, draw_layer);

            // Edges
            b.Draw(texture, new Rectangle(x + (int)((float)num * scale), y, width - (int)((float)num * scale) * 2, (int)((float)num * scale)), new Rectangle(sourceRect.X + num, sourceRect.Y, num, num), color, 0f, Vector2.Zero, SpriteEffects.None, draw_layer);
            b.Draw(texture, new Rectangle(x + (int)((float)num * scale), y + height - (int)((float)num * scale), width - (int)((float)num * scale) * 2, (int)((float)num * scale)), new Rectangle(sourceRect.X + num, num * 2 + sourceRect.Y, num, num), color, 0f, Vector2.Zero, SpriteEffects.None, draw_layer);
            b.Draw(texture, new Rectangle(x, y + (int)((float)num * scale), (int)((float)num * scale), height - (int)((float)num * scale) * 2), new Rectangle(sourceRect.X, num + sourceRect.Y, num, num), color, 0f, Vector2.Zero, SpriteEffects.None, draw_layer);
            b.Draw(texture, new Rectangle(x + width - (int)((float)num * scale), y + (int)((float)num * scale), (int)((float)num * scale), height - (int)((float)num * scale) * 2), new Rectangle(sourceRect.X + num * 2, num + sourceRect.Y, num, num), color, 0f, Vector2.Zero, SpriteEffects.None, draw_layer);
        }

        // Convenient overload signature for passing standard Rectangle bounding layout blocks directly
        public static void DrawCard(SpriteBatch b, Rectangle bounds, Color color)
        {
            DrawCard(b, bounds.X, bounds.Y, bounds.Width, bounds.Height, color, 1f, false, -1f);
        }

        private static Texture2D TryLoadWithFallback(string primaryPath, string fallbackPath)
        {
            string absPrimary = Path.Combine(ModEntry.Instance.Helper.DirectoryPath, primaryPath);
            if (File.Exists(absPrimary))
            {
                try { return ModEntry.Instance.Helper.ModContent.Load<Texture2D>(primaryPath); } catch { }
            }
            try { return ModEntry.Instance.Helper.ModContent.Load<Texture2D>(fallbackPath); } catch { return null; }
        }

        public static string GetComponentFromAppId(string appId)
        {
            return appId switch
            {
                "builtin:appstore" => "app_appstore",
                "builtin:calendar" => "app_calendar",
                "builtin:camera" => "app_camera",
                "builtin:notification" => "app_notification",
                "builtin:photo" => "app_photo",
                "builtin:setting" => "app_setting",
                _ => appId.Replace("builtin:", "app_")
            };
        }

        public static string GetSizeString(AppSize size)
        {
            return size switch
            {
                AppSize.Size1x1 => "1x1",
                AppSize.Size2x1 => "2x1",
                AppSize.Size2x2 => "2x2",
                AppSize.Size3x2 => "3x2",
                AppSize.Size4x2 => "4x2",
                AppSize.Size4x3 => "4x3",
                AppSize.Size4x4 => "4x4",
                _ => "1x1"
            };
        }

        public static Texture2D GetAppTexture(string appId, AppSize size)
        {
            string cacheKey = $"{appId}_{size}";
            if (AppTextureCache.TryGetValue(cacheKey, out Texture2D? cachedTex))
            {
                return cachedTex;
            }

            string component = GetComponentFromAppId(appId);
            string sizeStr = GetSizeString(size);

            string relativePath = Path.Combine(AssetHelper.GetPhoneThemesRootPath(), component, AssetHelper.GetComponentTheme(component), sizeStr + ".png");
            string absolutePath = Path.Combine(ModEntry.Instance.Helper.DirectoryPath, relativePath);

            if (!File.Exists(absolutePath))
            {
                relativePath = Path.Combine(AssetHelper.GetPhoneThemesRootPath(), component, AssetHelper.GetComponentTheme(component), "1x1.png");
                absolutePath = Path.Combine(ModEntry.Instance.Helper.DirectoryPath, relativePath);
            }

            if (!File.Exists(absolutePath))
            {
                relativePath = Path.Combine(AssetHelper.GetPhoneThemesRootPath(), component, AssetHelper.DefaultPhoneThemeName, "1x1.png");
                absolutePath = Path.Combine(ModEntry.Instance.Helper.DirectoryPath, relativePath);
            }

            Texture2D? loadedTex = null;
            if (File.Exists(absolutePath))
            {
                try
                {
                    loadedTex = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(relativePath);
                }
                catch
                {
                    loadedTex = null;
                }
            }

            if (loadedTex == null)
            {
                var regApp = ModEntry.GetRegisteredPhoneAppsSnapshot().FirstOrDefault(a => a.CompositeId.Equals(appId, StringComparison.OrdinalIgnoreCase));
                if (regApp != null && regApp.ThemedIconTextures != null)
                {
                    string currentTheme = AssetHelper.GetComponentTheme(component);
                    if (regApp.ThemedIconTextures.TryGetValue(currentTheme, out var themedTex))
                    {
                        loadedTex = themedTex;
                    }
                    else if (regApp.ThemedIconTextures.TryGetValue("default", out var defaultTex))
                    {
                        loadedTex = defaultTex;
                    }
                }
            }

            if (loadedTex != null)
            {
                AppTextureCache[cacheKey] = loadedTex;
            }
            return loadedTex!;
        }
    }
}