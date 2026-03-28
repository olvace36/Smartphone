using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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
            PhoneBackground = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.ImagesConstants.PhoneBackground));
            PhoneEmpty = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.ImagesConstants.PhoneEmpty));
            Background = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.ImagesConstants.Background));

            AppText = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.ImagesConstants.AppText));
            AppCamera = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.ImagesConstants.AppCamera));
            AppPhoto = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.ImagesConstants.AppPhoto));
            AppSetting = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.ImagesConstants.AppSetting));
            AppGame = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.ImagesConstants.AppGame));
            AppNotification = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.ImagesConstants.AppNotification));

            GameDarts = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.ImagesConstants.GameDarts));
            GameCart = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.ImagesConstants.GameCart));
            GameCrane = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.ImagesConstants.GameCrane));
            GameJack = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.ImagesConstants.GameJack));
            GamePirate = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.ImagesConstants.GamePirate));
            GameSpin = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.ImagesConstants.GameSpin));
        }
    }
}
