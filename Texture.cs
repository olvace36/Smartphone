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

        public static Texture2D AppText;
        public static Texture2D AppCamera;
        public static Texture2D AppPhoto;
        public static Texture2D AppSetting;
        public static Texture2D AppGame;
        public static Texture2D AppNotification;


        public static Texture2D GameDarts;
        public static Texture2D GameCrane;
        public static Texture2D GameCart;
        public static Texture2D GameJack;
        public static Texture2D GamePirate;
        public static Texture2D GameSpin;

        public static void LoadTextures()
        {
            try
            {
                LoadTexturesFromThemeFolder(AssetHelper.GetPhoneThemeFolderPath());
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor?.Log(
                    $"Failed to load phone theme '{AssetHelper.CurrentPhoneThemeName}', falling back to '{AssetHelper.DefaultPhoneThemeName}'. {ex.Message}",
                    LogLevel.Warn);

                AssetHelper.SetCurrentPhoneTheme(AssetHelper.DefaultPhoneThemeName);

                try
                {
                    LoadTexturesFromThemeFolder(AssetHelper.GetPhoneThemeFolderPath());
                }
                catch (Exception fallbackEx)
                {
                    ModEntry.SMonitor?.Log($"Failed to load fallback phone theme assets: {fallbackEx}", LogLevel.Error);
                    throw;
                }
            }
        }

        private static void LoadTexturesFromThemeFolder(string themeFolderPath)
        {
            PhoneBackground = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(themeFolderPath, AssetHelper.ImagesConstants.PhoneBackground));
            PhoneEmpty = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(themeFolderPath, AssetHelper.ImagesConstants.PhoneEmpty));
            Background = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(themeFolderPath, AssetHelper.ImagesConstants.Background));

            AppText = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(themeFolderPath, AssetHelper.ImagesConstants.AppText));
            AppCamera = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(themeFolderPath, AssetHelper.ImagesConstants.AppCamera));
            AppPhoto = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(themeFolderPath, AssetHelper.ImagesConstants.AppPhoto));
            AppSetting = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(themeFolderPath, AssetHelper.ImagesConstants.AppSetting));
            AppGame = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(themeFolderPath, AssetHelper.ImagesConstants.AppGame));
            AppNotification = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(themeFolderPath, AssetHelper.ImagesConstants.AppNotification));

            GameDarts = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(themeFolderPath, AssetHelper.ImagesConstants.GameDarts));
            GameCart = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(themeFolderPath, AssetHelper.ImagesConstants.GameCart));
            GameCrane = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(themeFolderPath, AssetHelper.ImagesConstants.GameCrane));
            GameJack = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(themeFolderPath, AssetHelper.ImagesConstants.GameJack));
            GamePirate = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(themeFolderPath, AssetHelper.ImagesConstants.GamePirate));
            GameSpin = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(themeFolderPath, AssetHelper.ImagesConstants.GameSpin));
        }
    }
}
