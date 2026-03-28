public class AssetHelper
{
    private const string AssetFolderName = "assets";

    public static string GetBarAssetsFolderPath()
    {
        return Path.Combine(AssetFolderName, "images");
    }


    public class ImagesConstants
    {
        public const string PhoneBackground = "phone_background.png";
        public const string PhoneEmpty = "phone_empty.png";
        public const string Background = "background.png";

        public const string AppText = "appText.png";
        public const string AppCamera = "appCamera.png";
        public const string AppPhoto = "appPhoto.png";
        public const string AppSetting = "appSetting.png";
        public const string AppGame = "appGame.png";
        public const string AppNotification = "appNotification.png";

        public const string GameDarts = "gameDarts.png";
        public const string GameCart = "gameCart.png";
        public const string GameCrane = "gameCrane.png";
        public const string GamePirate = "gamePirate.png";
        public const string GameJack = "gameJack.png";
        public const string GameSpin = "gameSpin.png";
    }

}