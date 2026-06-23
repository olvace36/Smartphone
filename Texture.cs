using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;

namespace Smartphone
{
    public static class Textures
    {
        public static Texture2D PhoneBackground;
        public static Texture2D PhoneEmpty;
        public static Texture2D Background;
        public static Texture2D GroupBackground;

        public static Texture2D AppCamera;
        public static Texture2D AppPhoto;
        public static Texture2D AppSetting;
        public static Texture2D AppNotification;
        public static Texture2D AppAppStore;
        public static Texture2D AppCalendar;

        // In-memory cache to keep CPU/RAM footprint flat and eliminate draw-loop lag
        private static readonly Dictionary<string, Texture2D> AppTextureCache = new(StringComparer.OrdinalIgnoreCase);

        public static void LoadTextures()
        {
            // Clear cache when user swaps themes so textures refresh instantly
            AppTextureCache.Clear();

            try
            {
                string phonePath = AssetHelper.GetComponentThemeFolderPath("phone");

                PhoneEmpty = TryLoadWithFallback(Path.Combine(phonePath, "default.png"), Path.Combine(AssetHelper.GetPhoneThemesRootPath(), "phone", "default", "default.png"));
                PhoneBackground = TryLoadWithFallback(Path.Combine(phonePath, "default_background.png"), Path.Combine(AssetHelper.GetPhoneThemesRootPath(), "phone", "default", "default_background.png"));
                Background = TryLoadWithFallback(Path.Combine(phonePath, "background.png"), Path.Combine(AssetHelper.GetPhoneThemesRootPath(), "phone", "default", "background.png"));
                GroupBackground = TryLoadWithFallback(Path.Combine(phonePath, "group_background.png"), Path.Combine(AssetHelper.GetPhoneThemesRootPath(), "phone", "default", "group_background.png"));

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
            try
            {
                loadedTex = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(relativePath);
            }
            catch
            {
                loadedTex = null;
            }

            AppTextureCache[cacheKey] = loadedTex!;
            return loadedTex!;
        }
    }
}