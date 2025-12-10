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


        public static Texture2D GameDarts;
        public static Texture2D GameCrane;
        public static Texture2D GameCart;
        public static Texture2D GameJack;
        public static Texture2D GamePirate;
        public static Texture2D GameSpin;

        public static void LoadTextures()
        {
            PhoneBackground = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.BarsConstants.PhoneBackground));
            PhoneEmpty = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.BarsConstants.PhoneEmpty));
            Background = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.BarsConstants.Background));

            AppText = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.BarsConstants.AppText));
            AppCamera = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.BarsConstants.AppCamera));
            AppPhoto = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.BarsConstants.AppPhoto));
            AppSetting = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.BarsConstants.AppSetting));
            AppGame = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.BarsConstants.AppGame));

            GameDarts = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.BarsConstants.GameDarts));
            GameCart = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.BarsConstants.GameCart));
            GameCrane = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.BarsConstants.GameCrane));
            GameJack = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.BarsConstants.GameJack));
            GamePirate = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.BarsConstants.GamePirate));
            GameSpin = ModEntry.Instance.Helper.ModContent.Load<Texture2D>(Path.Combine(AssetHelper.GetBarAssetsFolderPath(), AssetHelper.BarsConstants.GameSpin));
        }
    }
}
