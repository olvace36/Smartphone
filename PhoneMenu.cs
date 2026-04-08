using System.Diagnostics.Metrics;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Characters;
using StardewValley.GameData.HomeRenovations;
using StardewValley.Menus;


namespace Smartphone
{
    public partial class PhoneMenu : IClickableMenu
    {
        private sealed class HomeAppEntry
        {
            public string Id { get; init; } = "";
            public string DisplayName { get; init; } = "";
            public Texture2D IconTexture { get; init; } = null!;
            public Rectangle? SourceRect { get; init; }
            public Func<int>? GetBadgeCount { get; init; }
        }

        private bool isDragging = false;
        private int dragOffsetX;
        private int dragOffsetY;

        private static Dictionary<string, List<string>> pendingMessages = new();
        private static Dictionary<string, CancellationTokenSource> replyTimers = new();
        private static TimeSpan delay = TimeSpan.FromSeconds(20);
        Texture2D furnitureTexture = Game1.content.Load<Texture2D>("TileSheets\\furniture");



        private Texture2D texturePhoneBackground = Textures.PhoneBackground;
        private Texture2D texturePhoneCapture = Textures.PhoneEmpty;
        private Texture2D texturePortraitBackground = Textures.Background;

        private Texture2D textureAppCamera = Textures.AppCamera;
        private Texture2D textureAppText = Textures.AppText;
        private Texture2D textureAppPhoto = Textures.AppPhoto;
        private Texture2D textureAppSocial = Textures.AppSocial;
        private Texture2D textureAppSetting = Textures.AppSetting;
        private Texture2D textureAppGame = Textures.AppGame;
        private Texture2D textureAppNotification = Textures.AppNotification;

        private Texture2D textureGameDarts = Textures.GameDarts;
        private Texture2D textureGameJack = Textures.GameJack;
        private Texture2D textureGameCart = Textures.GameCart;
        private Texture2D textureGameCrane = Textures.GameCrane;
        private Texture2D textureGamePirate = Textures.GamePirate;
        private Texture2D textureGameSpin = Textures.GameSpin;

        public List<ClickableComponent> messageableNpcList;
        private int scrollOffset = 0;
        private int maxVisibleNPCs = 7;
        int visibleSlots = 7;
        int spacing = 95;

        public static string selectedNpc = null;
        private string currentMessage = "";
        public static List<string> messageHistory = new();
        private float chatScrollOffset = 0f;
        private float chatScrollTarget = 0f;
        public static List<string> notificationHistory = new();
        private float notificationScrollOffset = 0f;
        private float notificationScrollTarget = 0f;
        int maxBubbleWidth = 400;

        private const int ChatViewportYOffset = 125;
        private const int ChatViewportHeight = 715;
        private const float ChatScrollPixelsPerWheelNotch = 48f;
        private const float ChatScrollLerpSpeed = 16f;
        private const int NotificationViewportYOffset = 126;
        private const int NotificationViewportHeight = 800;

        private Dictionary<string, ClickableTextureComponent> favourityNpcButton = new();
        private readonly Dictionary<string, Rectangle> socialProfileNpcButtonBounds = new(StringComparer.OrdinalIgnoreCase);
        private List<string> phoneSoundList = new();
        private Dictionary<string, ClickableTextureComponent> phoneSoundButton = new();
        private List<string> phoneTextColorList = new();
        private Dictionary<string, ClickableTextureComponent> phoneTextColorButton = new();
        private List<string> phoneThemeList = new();
        private Dictionary<string, ClickableTextureComponent> phoneThemeButton = new();
        private readonly Dictionary<string, Color> phoneTextColorMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Rectangle> settingOptionBounds = new();


        // buttons
        private ClickableTextureComponent okButton;
        private ClickableTextureComponent backButton;
        private ClickableTextureComponent removeButton;
        private ClickableTextureComponent captureButton;
        private ClickableTextureComponent photoNextButton;
        private ClickableTextureComponent photoPreviousButton;
        private ClickableTextureComponent heartButton;
        private Rectangle photoAvatarButtonBounds = Rectangle.Empty;
        private Rectangle cameraZoomOutButtonBounds = Rectangle.Empty;
        private Rectangle cameraZoomInButtonBounds = Rectangle.Empty;
        private Rectangle cameraRotateButtonBounds = Rectangle.Empty;
        private Rectangle cameraSquareButtonBounds = Rectangle.Empty;

        // apps
        public static string? currentApp = null;

        private const string BuiltinAppNotificationId = "builtin:notification";
        private const string BuiltinAppTextId = "builtin:text";
        private const string BuiltinAppCameraId = "builtin:camera";
        private const string BuiltinAppPhotoId = "builtin:photo";
        private const string BuiltinAppSocialId = "builtin:social";
        private const string BuiltinAppSettingId = "builtin:setting";
        private const string BuiltinAppCalendarId = "builtin:calendar";
        private const string BuiltinAppGameId = "builtin:game";
        private const string ExternalGroupAppState = "appExternalGroup";
        private const string SettingMenuMainState = "settingMain";
        private const string SettingMenuSoundState = "settingSound";
        private const string SettingMenuTextColorState = "settingTextColor";
        private const string SettingMenuThemeState = "settingTheme";
        private const string SettingMenuOptionTextColor = "textColor";
        private const string SettingMenuOptionSound = "sound";
        private const string SettingMenuOptionTheme = "theme";

        private const int HomeAppsPerPage = 20;
        private const int HomeAppsColumns = 4;
        private const int HomeAppStartX = 80;
        private const int HomeAppStartY = 176;
        private const int HomeAppSpacingX = 120;
        private const int HomeAppSpacingY = 120;
        private const int AppLabelTrailingSpaces = 5;
        private const float AppLabelFontScale = 0.8f;
        private const float AppLabelMarqueePixelsPerSecond = 45f;

        private const int ExternalGroupColumns = 3;
        private const int ExternalGroupStartX = 140;
        private const int ExternalGroupStartY = 250;
        private const int ExternalGroupSpacingX = 120;
        private const int ExternalGroupSpacingY = 120;

        private const float CameraZoomStep = 0.05f;
        private const float CameraZoomMin = 1f;
        private const float CameraZoomMax = 2f;

        private readonly Dictionary<string, Rectangle> homeAppClickBounds = new();
        private Rectangle homeAppPrevPageBounds = Rectangle.Empty;
        private Rectangle homeAppNextPageBounds = Rectangle.Empty;
        private int homeAppPage = 0;
        private double appLabelMarqueeElapsedSeconds = 0d;
        private readonly RasterizerState appLabelScissorRasterizer = new() { ScissorTestEnable = true };

        private readonly Dictionary<string, Rectangle> externalGroupItemClickBounds = new();
        private string currentExternalGroupId = "";
        private string currentExternalGroupName = "";


        private ClickableTextureComponent gamePirate;
        private ClickableTextureComponent gameJack;
        private ClickableTextureComponent gameDarts;
        private ClickableTextureComponent gameCart;
        private ClickableTextureComponent gameCrane;
        private ClickableTextureComponent gameSpin;


        private string userCaptureFolderPath = Path.Combine(ModEntry.Instance.Helper.DirectoryPath, "userdata", Constants.SaveFolderName, "player_photo");
        private string npcCaptureFolderPath = Path.Combine(ModEntry.Instance.Helper.DirectoryPath, "userdata", Constants.SaveFolderName, "npc_photo");
        private List<string> capturedImages;
        private int currentImageIndex = -1;
        private Texture2D currentDisplayedImage = null;
        private bool currentDisplayedImageIsSquare = false;
        private Texture2D phoneBackgroundImage = null;


        // this textBox is hidden. it is created only to disable the game function key
        private TextBox textBox;

        private (string, string) currentSuggestion = ("", "");
        private Rectangle messageSuggestionBounds = Rectangle.Empty;
        private Rectangle firstMessageBounds = Rectangle.Empty;
        private string currentSettingMenuState = SettingMenuMainState;

        public PhoneMenu() : base(Game1.uiViewport.Width / 2 - 400, Game1.uiViewport.Height / 2 - 500, 600, 1000, true)
        {
            this.upperRightCloseButton = null;

            messageableNpcList = new List<ClickableComponent>();
            Dictionary<string, long> latestTimestamps = MessageManager.GetLatestAddDictionary();
            List<NPC> villagers = Utility.getAllVillagers()
            .Where(n =>
                n.IsVillager &&
                n.CanSocialize &&
                !n.IsMonster &&
                Game1.player.getFriendshipHeartLevelForNPC(n.Name) >= 1
                //|| n.Name == "Lewis" || n.Name == "Robin"
            )
            .OrderByDescending(npc => MessageManager.favouriteNpc.Contains(npc.Name))
            .ThenByDescending(npc => latestTimestamps.TryGetValue(npc.Name, out long ts) ? ts : 0)
            .ThenBy(npc => npc.Name)
            .ToList();

            for (int i = 0; i < villagers.Count; i++)
            {
                messageableNpcList.Add(new ClickableComponent(new Rectangle(0, 0, 0, 0), villagers[i].Name));
            }


            textBox = new TextBox(
                Game1.content.Load<Texture2D>("LooseSprites\\textBox"),
                null,
                Game1.smallFont,
                Game1.textColor
            )
            {
                X = -100,
                Y = -100,
                Width = 1,
                Height = 1,
                Text = ""
            };

            phoneSoundList = new List<string>
            {
                "getNewSpecialItem",
                "crystal",
                "phone",
                "achievement",
                "cacklingWitch",
                "dog_bark",
                "Duck",
                "cat",
                "explosion",
                "goldenWalnut",
                "machine_bell",
                "Meteorite",
                "thunder",
                "UFO",
                "yoba"
            };

            phoneTextColorList = new List<string>
            {
                "Black",
                "Red",
                "Green",
                "Blue",
                "Yellow",
                "Orange",
                "Purple",
                "White"
            };

            phoneTextColorMap["Black"] = Color.Black;
            phoneTextColorMap["Red"] = Color.Red;
            phoneTextColorMap["Green"] = Color.Green;
            phoneTextColorMap["Blue"] = Color.Blue;
            phoneTextColorMap["Yellow"] = Color.Yellow;
            phoneTextColorMap["Orange"] = Color.Orange;
            phoneTextColorMap["Purple"] = Color.MediumPurple;
            phoneTextColorMap["White"] = Color.White;

            AssetHelper.SetCurrentPhoneTheme(MessageManager.currentPhoneTheme);
            MessageManager.currentPhoneTheme = AssetHelper.CurrentPhoneThemeName;
            RefreshPhoneThemeList();
            ReloadThemeTextures();

            delay = TimeSpan.FromSeconds(Game1.random.NextInt64(10, 15));

            ApplyPhoneBackground(MessageManager.currentPhoneBackground);

        }


        public override void draw(SpriteBatch b)
        {
            okButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 490, this.yPositionOnScreen + 850, 64, 64),
                Game1.mouseCursors,
                new Rectangle(128, 256, 64, 64),
                1f);
            removeButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 335, this.yPositionOnScreen + 66, 32, 26 * 2),
                Game1.mouseCursors,
                new Rectangle(564, 102, 16, 26),
                1.7f);
            captureButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 130, this.yPositionOnScreen + 65, 100, 40),
                Game1.mouseCursors2,
                new Rectangle(72, 32, 18, 15),
                3.25f);

            photoNextButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 500, this.yPositionOnScreen + 500, 64, 64),
                Game1.mouseCursors,
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 33),
                1f);

            photoPreviousButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 35, this.yPositionOnScreen + 500, 64, 64),
                Game1.mouseCursors,
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 44),
                1f);


            // minigame app
            gameDarts = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 140, this.yPositionOnScreen + 370, 84, 84),
                textureGameDarts,
                new Rectangle(0, 0, 84, 84),
                1f);

            gamePirate = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 260, this.yPositionOnScreen + 370, 84, 84),
                textureGamePirate,
                new Rectangle(0, 0, 84, 84),
                1f);

            gameCart = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 380, this.yPositionOnScreen + 370, 84, 84),
                textureGameCart,
                new Rectangle(0, 0, 84, 84),
                1f);

            gameSpin = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 140, this.yPositionOnScreen + 490, 84, 84),
                textureGameSpin,
                new Rectangle(0, 0, 84, 84),
                1f);

            gameJack = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 260, this.yPositionOnScreen + 490, 84, 84),
                textureGameJack,
                new Rectangle(0, 0, 84, 84),
                1f);

            gameCrane = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 380, this.yPositionOnScreen + 490, 84, 84),
                textureGameCrane,
                new Rectangle(0, 0, 84, 84),
                1f);


            // global
            backButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 40, this.yPositionOnScreen + 59, 64, 64),
                Game1.mouseCursors,
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 44),
                0.95f);

            cameraZoomOutButtonBounds = Rectangle.Empty;
            cameraZoomInButtonBounds = Rectangle.Empty;
            cameraRotateButtonBounds = Rectangle.Empty;
            cameraSquareButtonBounds = Rectangle.Empty;


            if (currentApp == null)
            {
                b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height), Color.Black * 0.6f);
                b.Draw(texturePhoneCapture, new Vector2(xPositionOnScreen, yPositionOnScreen), Color.White);
                b.Draw(texturePhoneBackground, new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 116), Color.White);
                if (phoneBackgroundImage != null)
                {
                    Vector2 imagePosition = new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 116);
                    b.Draw(phoneBackgroundImage, imagePosition, Color.White * 0.8f);
                }

                DrawHomeApps(b);

            }
            else if (currentApp == "appGame")
            {
                b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height), Color.Black * 0.6f);
                b.Draw(texturePhoneCapture, new Vector2(xPositionOnScreen, yPositionOnScreen), Color.White);
                b.Draw(texturePhoneBackground, new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 116), Color.White);
                backButton.draw(b);

                gameCart.draw(b);
                gameDarts.draw(b);
                gameJack.draw(b);
                gamePirate.draw(b);
                gameCrane.draw(b);
                gameSpin.draw(b);
            }
            else if (currentApp == ExternalGroupAppState)
            {
                b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height), Color.Black * 0.6f);
                b.Draw(texturePhoneCapture, new Vector2(xPositionOnScreen, yPositionOnScreen), Color.White);
                b.Draw(texturePhoneBackground, new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 116), Color.White);
                backButton.draw(b);

                string title = string.IsNullOrWhiteSpace(currentExternalGroupName)
                    ? "App Group"
                    : currentExternalGroupName;
                b.DrawString(Game1.dialogueFont, title, new Vector2(xPositionOnScreen + 105, yPositionOnScreen + 125), Color.Black);

                DrawExternalGroupItems(b);
            }
            else if (currentApp == "appCamera")
            {
                Rectangle phoneRect = ModEntry.GetPhoneCameraPreviewBounds(xPositionOnScreen, yPositionOnScreen);
                Rectangle captureRect = ModEntry.GetPlayerPhotoCaptureBounds(xPositionOnScreen, yPositionOnScreen);

                Rectangle zoomOutDrawBounds = new Rectangle(xPositionOnScreen + 255, yPositionOnScreen + 65, 45, 45);
                Rectangle zoomInDrawBounds = new Rectangle(xPositionOnScreen + 305, yPositionOnScreen + 65, 45, 45);
                Rectangle rotateDrawBounds = new Rectangle(xPositionOnScreen + 355, yPositionOnScreen + 65, 95, 45);
                Rectangle squareDrawBounds = new Rectangle(xPositionOnScreen + 455, yPositionOnScreen + 65, 95, 45);

                if (ModEntry.cameraLandscapeMode)
                {
                    cameraZoomOutButtonBounds = GetLandscapeRotatedBounds(zoomOutDrawBounds);
                    cameraZoomInButtonBounds = GetLandscapeRotatedBounds(zoomInDrawBounds);
                    cameraRotateButtonBounds = GetLandscapeRotatedBounds(rotateDrawBounds);
                    cameraSquareButtonBounds = GetLandscapeRotatedBounds(squareDrawBounds);
                }
                else
                {
                    cameraZoomOutButtonBounds = zoomOutDrawBounds;
                    cameraZoomInButtonBounds = zoomInDrawBounds;
                    cameraRotateButtonBounds = rotateDrawBounds;
                    cameraSquareButtonBounds = squareDrawBounds;
                }

                int viewportWidth = Game1.viewport.Width;
                int viewportHeight = Game1.viewport.Height;

                int topShadeHeight = Math.Max(0, phoneRect.Top);
                int bottomShadeY = Math.Clamp(phoneRect.Bottom, 0, viewportHeight);
                int bottomShadeHeight = Math.Max(0, viewportHeight - bottomShadeY);
                int leftShadeWidth = Math.Max(0, phoneRect.Left);
                int rightShadeX = Math.Clamp(phoneRect.Right, 0, viewportWidth);
                int rightShadeWidth = Math.Max(0, viewportWidth - rightShadeX);
                int centerBandY = Math.Clamp(phoneRect.Top, 0, viewportHeight);
                int centerBandBottom = Math.Clamp(phoneRect.Bottom, 0, viewportHeight);
                int centerBandHeight = Math.Max(0, centerBandBottom - centerBandY);

                if (topShadeHeight > 0)
                    b.Draw(Game1.staminaRect, new Rectangle(0, 0, viewportWidth, topShadeHeight), Color.Black * 0.4f);
                if (bottomShadeHeight > 0)
                    b.Draw(Game1.staminaRect, new Rectangle(0, bottomShadeY, viewportWidth, bottomShadeHeight), Color.Black * 0.4f);
                if (leftShadeWidth > 0 && centerBandHeight > 0)
                    b.Draw(Game1.staminaRect, new Rectangle(0, centerBandY, leftShadeWidth, centerBandHeight), Color.Black * 0.4f);
                if (rightShadeWidth > 0 && centerBandHeight > 0)
                    b.Draw(Game1.staminaRect, new Rectangle(rightShadeX, centerBandY, rightShadeWidth, centerBandHeight), Color.Black * 0.4f);

                Rectangle topShade = Rectangle.Intersect(
                    new Rectangle(phoneRect.X, phoneRect.Y, phoneRect.Width, Math.Max(0, captureRect.Y - phoneRect.Y)),
                    phoneRect);
                Rectangle bottomShade = Rectangle.Intersect(
                    new Rectangle(phoneRect.X, captureRect.Bottom, phoneRect.Width, Math.Max(0, phoneRect.Bottom - captureRect.Bottom)),
                    phoneRect);
                Rectangle leftCaptureShade = Rectangle.Intersect(
                    new Rectangle(phoneRect.X, captureRect.Y, Math.Max(0, captureRect.X - phoneRect.X), captureRect.Height),
                    phoneRect);
                Rectangle rightCaptureShade = Rectangle.Intersect(
                    new Rectangle(captureRect.Right, captureRect.Y, Math.Max(0, phoneRect.Right - captureRect.Right), captureRect.Height),
                    phoneRect);

                if (topShade.Width > 0 && topShade.Height > 0)
                    b.Draw(Game1.staminaRect, topShade, Color.Black * 0.35f);
                if (bottomShade.Width > 0 && bottomShade.Height > 0)
                    b.Draw(Game1.staminaRect, bottomShade, Color.Black * 0.35f);
                if (leftCaptureShade.Width > 0 && leftCaptureShade.Height > 0)
                    b.Draw(Game1.staminaRect, leftCaptureShade, Color.Black * 0.35f);
                if (rightCaptureShade.Width > 0 && rightCaptureShade.Height > 0)
                    b.Draw(Game1.staminaRect, rightCaptureShade, Color.Black * 0.35f);

                if (ModEntry.cameraLandscapeMode)
                {
                    Matrix landscapeTransform = CreateCameraLandscapeRotationMatrix();

                    b.End();
                    b.Begin(
                        SpriteSortMode.Deferred,
                        BlendState.AlphaBlend,
                        SamplerState.PointClamp,
                        null,
                        null,
                        null,
                        landscapeTransform);

                    b.Draw(texturePhoneCapture, new Vector2(xPositionOnScreen, yPositionOnScreen), Color.White);
                    captureButton.draw(b);
                    backButton.draw(b);

                    DrawCameraControlButton(b, zoomOutDrawBounds, "-", false);
                    DrawCameraControlButton(b, zoomInDrawBounds, "+", false);
                    DrawCameraControlButton(b, rotateDrawBounds, "LAND", ModEntry.cameraLandscapeMode);
                    DrawCameraControlButton(b, squareDrawBounds, "SQR", ModEntry.cameraSquareMode);

                    b.End();
                    b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
                }
                else
                {
                    b.Draw(texturePhoneCapture, new Vector2(xPositionOnScreen, yPositionOnScreen), Color.White);
                    captureButton.draw(b);
                    backButton.draw(b);

                    DrawCameraControlButton(b, cameraZoomOutButtonBounds, "-", false);
                    DrawCameraControlButton(b, cameraZoomInButtonBounds, "+", false);
                    DrawCameraControlButton(b, cameraRotateButtonBounds, "LAND", ModEntry.cameraLandscapeMode);
                    DrawCameraControlButton(b, cameraSquareButtonBounds, "SQR", ModEntry.cameraSquareMode);
                }

                DrawCaptureOutline(b, captureRect, new Color(255, 255, 255, 220));
            }
            else if (currentApp == "appPhoto")
            {
                b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height), Color.Black * 0.6f);
                b.Draw(texturePhoneCapture, new Vector2(xPositionOnScreen, yPositionOnScreen), Color.White);
                b.Draw(texturePhoneBackground, new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 116), Color.White);
                backButton.draw(b);
                photoAvatarButtonBounds = Rectangle.Empty;
                //okButton.draw(b);

                if (currentDisplayedImage != null)
                {
                    Vector2 imagePosition = new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 116);
                    b.Draw(currentDisplayedImage, imagePosition, Color.White);

                    b.Draw(
                        removeButton.texture,
                        new Vector2(removeButton.bounds.X, removeButton.bounds.Y),
                        removeButton.sourceRect,
                        Color.White * 0.8f,
                        0f,
                        Vector2.Zero,
                        removeButton.scale,
                        SpriteEffects.None,
                        1f
                    );

                    if (currentImageIndex >= 0 && currentImageIndex < capturedImages.Count)
                    {
                        string rawName = Path.GetFileNameWithoutExtension(capturedImages[currentImageIndex]);

                        // Clean for display: remove random ID after "_", replace dashes with spaces
                        int underscoreIndex = rawName.IndexOf('_');
                        string displayName = underscoreIndex >= 0 ? rawName.Substring(0, underscoreIndex) : rawName;
                        displayName = displayName.Replace("-", " ");

                        Vector2 namePos = new Vector2(imagePosition.X, imagePosition.Y + currentDisplayedImage.Height - 50);
                        b.DrawString(Game1.dialogueFont, displayName, namePos, Color.White);

                        var rect = new Rectangle(218, 428, 7, 7);
                        if (IsSameFilePath(MessageManager.currentPhoneBackground, capturedImages[currentImageIndex]))
                            rect = new Rectangle(211, 428, 7, 7);

                        heartButton = new ClickableTextureComponent(
                        name: capturedImages[currentImageIndex],
                        bounds: new Rectangle(xPositionOnScreen + 500, yPositionOnScreen + 136, 35, 35),
                        label: null,
                        hoverText: "",
                        texture: Game1.mouseCursors,
                            sourceRect: rect,
                            scale: 5f
                        );
                        heartButton.draw(b);

                        if (currentDisplayedImageIsSquare)
                        {
                            bool avatarSelected = IsSameFilePath(MessageManager.currentPlayerAvatar, capturedImages[currentImageIndex]);
                            photoAvatarButtonBounds = new Rectangle(xPositionOnScreen + 390, yPositionOnScreen + 126, 102, 42);

                            IClickableMenu.drawTextureBox(
                                b,
                                Game1.menuTexture,
                                new Rectangle(0, 256, 60, 60),
                                photoAvatarButtonBounds.X,
                                photoAvatarButtonBounds.Y,
                                photoAvatarButtonBounds.Width,
                                photoAvatarButtonBounds.Height,
                                avatarSelected ? new Color(200, 240, 200, 230) : new Color(255, 255, 255, 230),
                                1f,
                                false);

                            b.DrawString(
                                Game1.smallFont,
                                "Avatar",
                                new Vector2(photoAvatarButtonBounds.X + 12, photoAvatarButtonBounds.Y + 10),
                                Color.Black);
                        }
                    }
                }



                // Draw buttons
                photoNextButton.draw(b);
                photoPreviousButton.draw(b);
            }
            else if (currentApp == "appText")
            {
                b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height), Color.Black * 0.6f);
                b.Draw(texturePhoneCapture, new Vector2(xPositionOnScreen, yPositionOnScreen), Color.White);
                b.Draw(texturePhoneBackground, new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 116), Color.White);
                backButton.draw(b);


                if (selectedNpc == null)
                {
                    favourityNpcButton.Clear();
                    socialProfileNpcButtonBounds.Clear();
                    int yStart = yPositionOnScreen + 150;
                    for (int i = 0; i < visibleSlots; i++)
                    {
                        int index = i + scrollOffset;
                        if (index >= messageableNpcList.Count) break;

                        var npc = messageableNpcList[index];
                        int y = yStart + i * spacing;



                        // === Draw scaled background box behind portrait ===
                        int portraitSize = 56; // Target display size
                        float scale = portraitSize / 64f * 1.5f; // Your texture is 64x64

                        Vector2 boxPosition = new Vector2(xPositionOnScreen + 100, y);

                        int unread = MessageManager.GetUnreadCount(npc.name);
                        if (unread > 0)
                        {
                            string number = Math.Min(unread, 9).ToString(); // display max 9
                            int digitSize = 8;
                            int spacingBetweenDigits = 0;
                            int totalWidth = number.Length * (digitSize + spacingBetweenDigits);

                            int numberX = (int)boxPosition.X - totalWidth - 41;
                            int numberY = (int)boxPosition.Y + 15;

                            int digitWidth = 8;
                            int digitHeight = 8;

                            int digitsPerRow = 6;

                            for (int j = 0; j < number.Length; j++)
                            {
                                char c = number[j];
                                if (char.IsDigit(c))
                                {
                                    int digit = c - '0';

                                    int row = digit / digitsPerRow;
                                    int col = digit % digitsPerRow;

                                    Rectangle sourceRect = new Rectangle(
                                        512 + col * digitWidth,     // X offset starts at 512
                                        128 + row * digitHeight,    // Y offset starts at 128
                                        digitWidth,
                                        digitHeight
                                    );

                                    //Game1.chatBox.addErrorMessage(unread.ToString());
                                    b.Draw(
                                        Game1.mouseCursors,
                                        new Vector2(numberX, numberY),
                                        sourceRect,
                                        Color.White,
                                        0f,
                                        Vector2.Zero,
                                        5f, // Scale it up to be visible
                                        SpriteEffects.None,
                                        1f
                                    );
                                }
                            }
                        }



                        b.Draw(
                            texturePortraitBackground,
                            position: boxPosition,
                            sourceRectangle: null,
                            color: Color.White,
                            rotation: 0f,
                            origin: Vector2.Zero,
                            scale: scale,
                            effects: SpriteEffects.None,
                            layerDepth: 0f
                        );
                        var realNpc = Game1.getCharacterFromName(npc.name);

                        // === Draw NPC portrait ===
                        if (realNpc.Portrait != null)
                        {
                            Rectangle portraitRect = new Rectangle((int)boxPosition.X + 9, (int)boxPosition.Y + 10, 67, 67);
                            Rectangle sourceRect = new Rectangle(0, 0, 64, 64);
                            b.Draw(realNpc.Portrait, portraitRect, sourceRect, Color.White);
                        }

                        // === Draw NPC name ===
                        int nameX = (int)boxPosition.X + portraitSize + 40;
                        npc.bounds = new Rectangle(nameX, y, 200, portraitSize);
                        b.DrawString(Game1.dialogueFont, npc.name, new Vector2(nameX, y + 15), Color.Black);


                        var rect = new Rectangle(218, 428, 7, 7);
                        if (MessageManager.favouriteNpc.Contains(npc.name))
                            rect = new Rectangle(211, 428, 7, 7);

                        heartButton = new ClickableTextureComponent(
                        name: npc.name,
                        bounds: new Rectangle(nameX + 280, y + 20, 35, 35),
                            label: null,
                            hoverText: "",
                        texture: Game1.mouseCursors,
                            sourceRect: rect,
                            scale: 5f
                        );

                        favourityNpcButton[npc.name] = heartButton;
                        heartButton.draw(b);

                        Rectangle profileButtonBounds = new Rectangle(nameX + 330, y + 20, 35, 35);
                        socialProfileNpcButtonBounds[npc.name] = profileButtonBounds;
                        DrawOpenSocialProfileButton(b, profileButtonBounds);

                    }

                    // Draw input box
                    int inputX = xPositionOnScreen + 100;
                    int inputY = yPositionOnScreen + 835;
                    int inputWidth = 400;

                    int fontHeight = (int)Game1.smallFont.MeasureString("A").Y;
                    int inputHeight = fontHeight + 30;

                    IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                        inputX, inputY, inputWidth, inputHeight,
                        Color.White, 1f, false);

                    string visibleText = currentMessage;
                    Vector2 textSize = Game1.smallFont.MeasureString(currentMessage);
                    int maxInputWidth = 420;

                    while (textSize.X > maxInputWidth - 20 && visibleText.Length > 1)
                    {
                        visibleText = visibleText.Substring(1);
                        textSize = Game1.smallFont.MeasureString(visibleText);
                    }

                    b.DrawString(Game1.smallFont, visibleText, new Vector2(inputX + 15, inputY + 20), Color.Black);

                    // enable ghost textBox. do not remove
                    textBox.Selected = true;
                    Game1.keyboardDispatcher.Subscriber = textBox;
                }
                else
                {
                    b.DrawString(Game1.dialogueFont, $"{selectedNpc}", new Vector2(xPositionOnScreen + 105, yPositionOnScreen + 65), Color.Black);

                    backButton.draw(b);
                    b.Draw(
                        removeButton.texture,
                        new Vector2(removeButton.bounds.X, removeButton.bounds.Y),
                        removeButton.sourceRect,
                        Color.White * 0.8f,
                        0f,
                        Vector2.Zero,
                        removeButton.scale,
                        SpriteEffects.None,
                        1f
                    );


                    // Save old spritebatch state and apply clipping
                    b.End();

                    Rectangle chatClipRect = new Rectangle(xPositionOnScreen, yPositionOnScreen + ChatViewportYOffset, width, ChatViewportHeight);
                    Game1.graphics.GraphicsDevice.ScissorRectangle = chatClipRect;

                    b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState() { ScissorTestEnable = true });

                    // Draw messages within clipped region
                    List<string> chatMessages = messageHistory.ToList();
                    ClampChatScroll(chatMessages);

                    int messageY = yPositionOnScreen + ChatViewportYOffset - (int)MathF.Floor(chatScrollOffset);
                    SpriteFont font = Game1.smallFont;

                    foreach (string msg in chatMessages)
                    {
                        bool fromSystem = msg.StartsWith("SYSTEM: ") && msg.EndsWith("---");
                        string text = msg.Substring(msg.IndexOf(":") + 1).Trim();
                        List<string> wrappedLines = SplitTextIntoLines(text, font, maxBubbleWidth);

                        int lineHeight = (int)font.MeasureString("A").Y + 4;
                        int bubbleHeight = wrappedLines.Count * lineHeight + 10;
                        int bubbleWidth = 0;

                        foreach (var line in wrappedLines)
                            bubbleWidth = Math.Max(bubbleWidth, (int)font.MeasureString(line).X + 20);

                        Rectangle bubbleRect = msg.StartsWith("PLAYER:")
                            ? new Rectangle(xPositionOnScreen + width - bubbleWidth - 50, messageY, bubbleWidth, bubbleHeight)
                            : new Rectangle(xPositionOnScreen + 50, messageY, bubbleWidth, bubbleHeight);

                        if (!fromSystem)
                        {
                            IClickableMenu.drawTextureBox(
                                b,
                                Game1.menuTexture,
                                new Rectangle(0, 256, 60, 60), // source rect for 9-slice
                                bubbleRect.X - 5,
                                bubbleRect.Y,
                                bubbleRect.Width + 12,
                                bubbleRect.Height + 10,
                                new Color(255, 255, 255, 200),
                                1f,
                                false
                            );

                            int textY = bubbleRect.Y + 15;
                            foreach (var line in wrappedLines)
                            {
                                Vector2 linePos = new Vector2(bubbleRect.X + 10, textY);
                                b.DrawString(font, line, linePos, Color.Black);
                                textY += lineHeight;
                            }
                        }
                        else
                        {
                            int textY = bubbleRect.Y + 15;
                            Vector2 linePos = new Vector2(bubbleRect.X + 10, textY);
                            b.DrawString(font, text, linePos, Color.Black);
                        }

                        messageY += bubbleHeight + 10;
                    }

                    // Reset clipping
                    b.End();
                    b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

                    if (selectedNpc != null && ModEntry.npcMessagesToday.ContainsKey(selectedNpc) && ModEntry.npcMessagesToday[selectedNpc].Count > 0)
                    {
                        okButton.draw(b);
                        // Draw input box
                        int inputX = xPositionOnScreen + 50;
                        int inputY = yPositionOnScreen + 850;
                        int inputWidth = 430;

                        int fontHeight = (int)Game1.smallFont.MeasureString("A").Y;
                        int inputHeight = fontHeight + 30;

                        IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                            inputX, inputY, inputWidth, inputHeight,
                            Color.White, 1f, false);

                        string visibleText = currentMessage;
                        Vector2 textSize = Game1.smallFont.MeasureString(currentMessage);
                        int maxInputWidth = 420;

                        while (textSize.X > maxInputWidth - 20 && visibleText.Length > 1)
                        {
                            visibleText = visibleText.Substring(1);
                            textSize = Game1.smallFont.MeasureString(visibleText);
                        }

                        b.DrawString(Game1.smallFont, visibleText, new Vector2(inputX + 15, inputY + 20), Color.Black);

                        // enable ghost textBox. do not remove
                        textBox.Selected = true;
                        Game1.keyboardDispatcher.Subscriber = textBox;
                    }
                    else
                    {
                        string firstMessage = Game1.timeOfDay < 1200 ? $"Good morning {selectedNpc}" : Game1.timeOfDay < 1800 ? $"Good afternoon {selectedNpc}" : $"Good evening {selectedNpc}";
                        float maxWidth = Game1.smallFont.MeasureString(firstMessage).X + 20f;
                        int lineHeight = (int)(Game1.smallFont.MeasureString("A").Y + 20);

                        Vector2 position = new Vector2(xPositionOnScreen + 300 - (maxWidth + 20) / 2, yPositionOnScreen + 850);
                        firstMessageBounds = new Rectangle(
                            (int)position.X,
                            (int)position.Y,
                            (int)maxWidth + 20,
                            lineHeight + 10
                        );

                        IClickableMenu.drawTextureBox(
                            Game1.spriteBatch,
                            Game1.menuTexture,
                            new Rectangle(0, 256, 60, 60),
                            firstMessageBounds.X,
                            firstMessageBounds.Y,
                            firstMessageBounds.Width,
                            firstMessageBounds.Height,
                            Color.White,
                            1f,
                            false
                        );

                        Utility.drawTextWithShadow(
                            Game1.spriteBatch,
                            firstMessage,
                            Game1.smallFont,
                            new Vector2(firstMessageBounds.X + 20, firstMessageBounds.Y + 20),
                            Game1.textColor
                        );

                    }


                    if (currentSuggestion.Item1 != "" && ModEntry.Config.HelperOption == "Minimal")
                    {
                        string suggestionText = currentSuggestion.Item1;
                        string[] lines = suggestionText.Split('\n');

                        // Measure the width of the longest line
                        float maxWidth = lines.Max(line => Game1.smallFont.MeasureString(line).X) + 15f;

                        // Get font line height (all lines same height in pixel font)
                        int lineHeight = (int)(Game1.smallFont.MeasureString("A").Y + 5);

                        Vector2 position = new Vector2(xPositionOnScreen + 600, yPositionOnScreen + 800);
                        messageSuggestionBounds = new Rectangle(
                            (int)position.X,
                            (int)position.Y,
                            (int)maxWidth + 20,
                            lineHeight * lines.Length + 10
                        );

                        IClickableMenu.drawTextureBox(
                            Game1.spriteBatch,
                            Game1.menuTexture,
                            new Rectangle(0, 256, 60, 60),
                            messageSuggestionBounds.X,
                            messageSuggestionBounds.Y,
                            messageSuggestionBounds.Width,
                            messageSuggestionBounds.Height,
                            Color.White,
                            1f,
                            false
                        );

                        for (int i = 0; i < lines.Length; i++)
                        {
                            Utility.drawTextWithShadow(
                                Game1.spriteBatch,
                                lines[i],
                                Game1.smallFont,
                                new Vector2(messageSuggestionBounds.X + 15, messageSuggestionBounds.Y + 10 + i * lineHeight),
                                Game1.textColor
                            );
                        }
                    }
                    else if (ModEntry.Config.HelperOption == "Always" && currentMessage != "")
                    {
                        string[] lines = { "Type one of these value",
                            "to start event manually:",
                            "  [Trigger Picnic Event]",
                            "  [Trigger Dinner Event]",
                            "  [Trigger Campfire Event]",
                            "  [Trigger Birthday Event]"
                        };

                        float maxWidth = lines.Max(line => Game1.smallFont.MeasureString(line).X) + 15f;

                        int lineHeight = (int)(Game1.smallFont.MeasureString("A").Y + 5);

                        Vector2 position = new Vector2(xPositionOnScreen + 600, yPositionOnScreen + 650);
                        messageSuggestionBounds = new Rectangle(
                            (int)position.X,
                            (int)position.Y,
                            (int)maxWidth + 20,
                            lineHeight * lines.Length + 10
                        );

                        IClickableMenu.drawTextureBox(
                            Game1.spriteBatch,
                            Game1.menuTexture,
                            new Rectangle(0, 256, 60, 60),
                            messageSuggestionBounds.X,
                            messageSuggestionBounds.Y,
                            messageSuggestionBounds.Width,
                            messageSuggestionBounds.Height,
                            Color.White,
                            1f,
                            false
                        );

                        for (int i = 0; i < lines.Length; i++)
                        {
                            Utility.drawTextWithShadow(
                                Game1.spriteBatch,
                                lines[i],
                                Game1.smallFont,
                                new Vector2(messageSuggestionBounds.X + 15, messageSuggestionBounds.Y + 10 + i * lineHeight),
                                Game1.textColor
                            );
                        }
                    }
                }
            }
            else if (currentApp == SocialAppState)
            {
                DrawSocialApp(b);
            }
            else if (currentApp == "appSetting")
            {
                DrawSettingMenu(b);
            }
            else if (currentApp == "appNotification")
            {
                b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height), Color.Black * 0.6f);
                b.Draw(texturePhoneCapture, new Vector2(xPositionOnScreen, yPositionOnScreen), Color.White);
                b.Draw(texturePhoneBackground, new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 116), Color.White);
                b.Draw(
                        removeButton.texture,
                        new Vector2(removeButton.bounds.X, removeButton.bounds.Y),
                        removeButton.sourceRect,
                        Color.White * 0.8f,
                        0f,
                        Vector2.Zero,
                        removeButton.scale,
                        SpriteEffects.None,
                        1f
                    );
                backButton.draw(b);

                b.End();
                Rectangle notificationClipRect = new Rectangle(xPositionOnScreen, yPositionOnScreen + NotificationViewportYOffset, width, NotificationViewportHeight);
                Game1.graphics.GraphicsDevice.ScissorRectangle = notificationClipRect;

                b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState() { ScissorTestEnable = true });

                // Draw messages within clipped region
                List<string> notificationMessages = notificationHistory.ToList();
                notificationMessages.Reverse();
                ClampNotificationScroll(notificationMessages);

                int messageY = yPositionOnScreen + NotificationViewportYOffset - (int)MathF.Floor(notificationScrollOffset);
                SpriteFont font = Game1.smallFont;

                // Show newest notifications first and animate their pixel offset.
                foreach (string msg in notificationMessages)
                {
                    List<string> wrappedLines = SplitNotificationIntoLines(msg, font, 485);

                    int lineHeight = (int)font.MeasureString("A").Y + 4;
                    int bubbleHeight = wrappedLines.Count * lineHeight + 10;
                    int bubbleWidth = 0;

                    foreach (var line in wrappedLines)
                        bubbleWidth = Math.Max(bubbleWidth, (int)font.MeasureString(line).X + 20);

                    Rectangle bubbleRect = new Rectangle(xPositionOnScreen + 50, messageY, bubbleWidth, bubbleHeight);

                    IClickableMenu.drawTextureBox(
                        b,
                        Game1.menuTexture,
                        new Rectangle(0, 256, 60, 60), // source rect for 9-slice
                        bubbleRect.X - 5,
                        bubbleRect.Y,
                        bubbleRect.Width + 12,
                        bubbleRect.Height + 10,
                        new Color(0, 0, 0, 100),
                        1f,
                        false
                    );

                    int textY = bubbleRect.Y + 15;
                    foreach (var line in wrappedLines)
                    {
                        Vector2 linePos = new Vector2(bubbleRect.X + 10, textY);
                        b.DrawString(font, line, linePos, Color.White);
                        textY += lineHeight;
                    }

                    messageY += bubbleHeight + 20;
                }

                // Reset clipping
                b.End();
                b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

            }


            // base
            base.draw(b);
            drawMouse(b);

           

        }
        public override void releaseLeftClick(int x, int y)
        {
            base.releaseLeftClick(x, y);
            isDragging = false;
        }

        public override void update(GameTime time)
        {
            base.update(time);

            appLabelMarqueeElapsedSeconds += time.ElapsedGameTime.TotalSeconds;
            if (appLabelMarqueeElapsedSeconds >= 1_000_000d)
                appLabelMarqueeElapsedSeconds %= 1_000_000d;

            if (isDragging)
            {
                xPositionOnScreen = Game1.getMouseX() - dragOffsetX;
                yPositionOnScreen = Game1.getMouseY() - dragOffsetY;
                ClampPhoneMenuToViewport();
            }

            ModEntry.currentMenuX = xPositionOnScreen;
            ModEntry.currentMenuY = yPositionOnScreen;

            if (currentApp == "appText" && selectedNpc != null)
            {
                ClampChatScroll();

                float lerpAmount = (float)(time.ElapsedGameTime.TotalSeconds * ChatScrollLerpSpeed);
                lerpAmount = Math.Clamp(lerpAmount, 0f, 1f);
                chatScrollOffset = MathHelper.Lerp(chatScrollOffset, chatScrollTarget, lerpAmount);

                if (Math.Abs(chatScrollOffset - chatScrollTarget) <= 0.5f)
                    chatScrollOffset = chatScrollTarget;
            }
            else if (currentApp == SocialAppState)
            {
                float lerpAmount = (float)(time.ElapsedGameTime.TotalSeconds * SocialScrollLerpSpeed);
                lerpAmount = Math.Clamp(lerpAmount, 0f, 1f);

                if (socialCreateMenuOpen)
                {
                    // create menu has no scrollable viewport
                }
                else if (socialProfileMenuOpen)
                {
                    ClampSocialProfileScroll();
                    socialProfileScrollOffset = MathHelper.Lerp(socialProfileScrollOffset, socialProfileScrollTarget, lerpAmount);

                    if (Math.Abs(socialProfileScrollOffset - socialProfileScrollTarget) <= 0.5f)
                        socialProfileScrollOffset = socialProfileScrollTarget;
                }
                else if (string.IsNullOrWhiteSpace(selectedSocialPostId))
                {
                    ClampSocialFeedScroll(StardewConnectManager.GetPostsSnapshot());
                    socialFeedScrollOffset = MathHelper.Lerp(socialFeedScrollOffset, socialFeedScrollTarget, lerpAmount);

                    if (Math.Abs(socialFeedScrollOffset - socialFeedScrollTarget) <= 0.5f)
                        socialFeedScrollOffset = socialFeedScrollTarget;
                }
                else
                {
                    StardewConnectPost? selectedPost = StardewConnectManager.GetPost(selectedSocialPostId);
                    if (selectedPost != null)
                    {
                        ClampSocialDetailScroll(selectedPost);
                        socialDetailScrollOffset = MathHelper.Lerp(socialDetailScrollOffset, socialDetailScrollTarget, lerpAmount);

                        if (Math.Abs(socialDetailScrollOffset - socialDetailScrollTarget) <= 0.5f)
                            socialDetailScrollOffset = socialDetailScrollTarget;
                    }
                }
            }
            else if (currentApp == "appNotification")
            {
                float lerpAmount = (float)(time.ElapsedGameTime.TotalSeconds * ChatScrollLerpSpeed);
                lerpAmount = Math.Clamp(lerpAmount, 0f, 1f);

                ClampNotificationScroll();
                notificationScrollOffset = MathHelper.Lerp(notificationScrollOffset, notificationScrollTarget, lerpAmount);

                if (Math.Abs(notificationScrollOffset - notificationScrollTarget) <= 0.5f)
                    notificationScrollOffset = notificationScrollTarget;
            }


        }

        public override void leftClickHeld(int x, int y)
        {
            base.leftClickHeld(x, y);
            if (GetActivePhoneDragBounds().Contains(x, y))
            {
                isDragging = true;
                dragOffsetX = x - xPositionOnScreen;
                dragOffsetY = y - yPositionOnScreen;
            }
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            if (selectedNpc != null && currentSuggestion.Item1 != "" && messageSuggestionBounds.Contains(x, y))
            {
                currentMessage = currentSuggestion.Item2.Replace("\n", " ");
                currentSuggestion = new ("", "");
                Game1.playSound("smallSelect"); // optional
                SnapChatScrollToBottom();
                return;
            }
            if(selectedNpc != null && firstMessageBounds.Contains(x, y) && (!ModEntry.npcMessagesToday.ContainsKey(selectedNpc) || ModEntry.npcMessagesToday[selectedNpc].Count == 0))
            {
                string firstMessage = Game1.timeOfDay < 1200 ? $"Good morning {selectedNpc}" : Game1.timeOfDay < 1800 ? $"Good afternoon {selectedNpc}" : $"Good evening {selectedNpc}";
                MessageManager.AddMessage(selectedNpc, $"PLAYER: {firstMessage}", isFromPlayer: true);
                ModEntry.FirstDailyText(selectedNpc, firstMessage);
                messageHistory = MessageManager.GetMessages(selectedNpc);
                SnapChatScrollToBottom();

            }

            if (currentApp == null)
            {
                if (homeAppPrevPageBounds.Contains(x, y) && TryChangeHomeAppPage(-1))
                {
                    Game1.playSound("shwip");
                    return;
                }

                if (homeAppNextPageBounds.Contains(x, y) && TryChangeHomeAppPage(1))
                {
                    Game1.playSound("shwip");
                    return;
                }

                foreach (KeyValuePair<string, Rectangle> app in homeAppClickBounds)
                {
                    if (!app.Value.Contains(x, y))
                        continue;

                    bool opened = TryHandleHomeAppClick(app.Key);
                    Game1.playSound(opened ? "smallSelect" : "cancel");
                    return;
                }
            }

            if (currentApp == ExternalGroupAppState)
            {
                foreach (KeyValuePair<string, Rectangle> item in externalGroupItemClickBounds)
                {
                    if (!item.Value.Contains(x, y))
                        continue;

                    bool opened = ModEntry.TryInvokeRegisteredPhoneAppGroupItem(currentExternalGroupId, item.Key, this);
                    Game1.playSound(opened ? "smallSelect" : "cancel");
                    return;
                }
            }

            if (currentApp == SocialAppState && HandleSocialLeftClick(x, y))
                return;



            if (currentApp == "appCamera")
            {
                if (cameraZoomOutButtonBounds.Contains(x, y))
                {
                    AdjustCameraZoom(-CameraZoomStep);
                    return;
                }

                if (cameraZoomInButtonBounds.Contains(x, y))
                {
                    AdjustCameraZoom(CameraZoomStep);
                    return;
                }

                if (cameraRotateButtonBounds.Contains(x, y))
                {
                    ModEntry.cameraLandscapeMode = !ModEntry.cameraLandscapeMode;
                    Game1.playSound("smallSelect");
                    return;
                }

                if (cameraSquareButtonBounds.Contains(x, y))
                {
                    ModEntry.cameraSquareMode = !ModEntry.cameraSquareMode;
                    Game1.playSound("smallSelect");
                    return;
                }
            }

            if (currentApp == "appCamera" && IsCameraCaptureButtonPressed(x, y))
            {
                ModEntry.takeScreenshot = true;
                return;
            }

            if (IsBackButtonPressed(x, y))
            {
                if (currentApp == "appText")
                {
                    if (selectedNpc != null)
                    {
                        MessageManager.SetUnreadCount(selectedNpc);
                        currentSuggestion = new("", "");
                        selectedNpc = null;
                        currentMessage = "";
                        UpdateNpcList();
                        return;
                    }
                    else
                    {
                        UpdateNpcList();
                        currentApp = null;
                        currentMessage = "";
                        return;
                    }
                }
                else if (currentApp == "appSetting")
                {
                    if (currentSettingMenuState != SettingMenuMainState)
                    {
                        currentSettingMenuState = SettingMenuMainState;
                        scrollOffset = 0;
                        return;
                    }

                    currentApp = null;
                    return;
                }
                else if (currentApp == SocialAppState)
                {
                    HandleSocialBackNavigation();
                    return;
                }
                else if ( new List<string> { "appCamera", "appPhoto", "appGame", "appNotification", ExternalGroupAppState }.Contains(currentApp))
                {
                    if (currentApp == ExternalGroupAppState)
                        ClearCurrentExternalGroup();

                    
                    currentApp = null;
                    return;
                }
            }

            if (removeButton.containsPoint(x, y) && currentApp == "appText")
            {
                MessageManager.ClearMessages(selectedNpc);
                messageHistory = MessageManager.GetMessages(selectedNpc);
                return;
            }
            else if (removeButton.containsPoint(x, y) && currentApp == "appPhoto")
            {
                if (currentImageIndex >= 0 && currentImageIndex < capturedImages.Count)
                {
                    string fileToDelete = capturedImages[currentImageIndex];
                    string imageName = Path.GetFileName(fileToDelete);
                    bool deletedBackgroundImage = IsSameFilePath(MessageManager.currentPhoneBackground, fileToDelete);
                    bool deletedAvatarImage = IsSameFilePath(MessageManager.currentPlayerAvatar, fileToDelete);

                    // Remove file from disk
                    try
                    {
                        File.Delete(fileToDelete);
                    }
                    catch (Exception ex)
                    {
                        ModEntry.SMonitor.Log($"Failed to delete image: {ex.Message}", LogLevel.Warn);
                    }

                    // Remove from list
                    capturedImages.RemoveAt(currentImageIndex);
                    ModEntry.RemoveImageTags(imageName);

                    // Choose next image
                    if (capturedImages.Count == 0)
                    {
                        currentDisplayedImage = null;
                        currentDisplayedImageIsSquare = false;
                        currentImageIndex = -1;
                    }
                    else if (currentImageIndex > 0)
                    {
                        currentImageIndex--; // Go to previous (left)
                        LoadImageAtIndex(currentImageIndex);
                    }
                    else
                    {
                        // Stay at index 0 if no left
                        LoadImageAtIndex(currentImageIndex);
                    }

                    if (deletedBackgroundImage)
                        ResetPhoneBackgroundToDefault();

                    if (deletedAvatarImage)
                        MessageManager.currentPlayerAvatar = "";

                    Game1.playSound("trashcan");
                }

                return;
            }
            else if (removeButton.containsPoint(x, y) && currentApp == "appNotification")
            {
                NotificationManager.clearNotification();
                notificationHistory = NotificationManager.getNoticationList();
                notificationScrollOffset = 0f;
                notificationScrollTarget = 0f;
                return;
            }





            if (photoNextButton.containsPoint(x, y) && currentApp == "appPhoto")
            {
                if (currentImageIndex > 0)
                {
                    currentImageIndex--;
                    LoadImageAtIndex(currentImageIndex);
                    Game1.playSound("shwip");
                    return;
                }
            }
            else if (photoPreviousButton.containsPoint(x, y) && currentApp == "appPhoto")
            {
                if (currentImageIndex < capturedImages.Count - 1)
                {
                    currentImageIndex++;
                    LoadImageAtIndex(currentImageIndex);
                    Game1.playSound("shwip");
                    return;
                }
            }
            else if (heartButton != null
                && currentDisplayedImage != null
                && currentImageIndex >= 0
                && currentImageIndex < capturedImages.Count
                && heartButton.containsPoint(x, y)
                && currentApp == "appPhoto")
            {
                if (IsSameFilePath(MessageManager.currentPhoneBackground, heartButton.name))
                    ResetPhoneBackgroundToDefault();
                else
                    ApplyPhoneBackground(heartButton.name);

                return;
            }
            else if (currentApp == "appPhoto"
                && currentDisplayedImage != null
                && currentDisplayedImageIsSquare
                && currentImageIndex >= 0
                && currentImageIndex < capturedImages.Count
                && photoAvatarButtonBounds.Contains(x, y))
            {
                string currentImagePath = capturedImages[currentImageIndex];
                if (IsSameFilePath(MessageManager.currentPlayerAvatar, currentImagePath))
                    MessageManager.currentPlayerAvatar = "";
                else
                    MessageManager.currentPlayerAvatar = currentImagePath;

                Game1.playSound("smallSelect");
                return;
            }

            if (currentApp == "appSetting")
            {
                if (currentSettingMenuState == SettingMenuMainState)
                {
                    foreach (KeyValuePair<string, Rectangle> option in settingOptionBounds)
                    {
                        if (!option.Value.Contains(x, y))
                            continue;

                        currentSettingMenuState = option.Key switch
                        {
                            SettingMenuOptionTextColor => SettingMenuTextColorState,
                            SettingMenuOptionTheme => SettingMenuThemeState,
                            _ => SettingMenuSoundState
                        };
                        scrollOffset = 0;
                        Game1.playSound("smallSelect");
                        return;
                    }
                }
                else if (currentSettingMenuState == SettingMenuSoundState)
                {
                    foreach (var button in phoneSoundButton.Values)
                    {
                        if (!button.containsPoint(x, y))
                            continue;

                        DelayedAction.playSoundAfterDelay(button.name, 0);
                        DelayedAction.playSoundAfterDelay(button.name, 1500);
                        MessageManager.currentPhoneSound = button.name;
                        return;
                    }
                }
                else if (currentSettingMenuState == SettingMenuTextColorState)
                {
                    foreach (var button in phoneTextColorButton.Values)
                    {
                        if (!button.containsPoint(x, y))
                            continue;

                        MessageManager.currentPhoneTextColor = button.name;
                        Game1.playSound("smallSelect");
                        return;
                    }
                }
                else if (currentSettingMenuState == SettingMenuThemeState)
                {
                    foreach (var button in phoneThemeButton.Values)
                    {
                        if (!button.containsPoint(x, y))
                            continue;

                        ApplyPhoneThemeSelection(button.name);
                        Game1.playSound("smallSelect");
                        return;
                    }
                }
            }
            if (selectedNpc == null && currentApp == "appText")
            {
                foreach (var button in favourityNpcButton.Values)
                {
                    if (button.containsPoint(x, y))
                    {
                        if (MessageManager.favouriteNpc.Contains(button.name))
                            MessageManager.favouriteNpc.Remove(button.name);
                        else
                            MessageManager.favouriteNpc.Add(button.name);

                        DelayedAction.functionAfterDelay(UpdateNpcList, 200);
                        Game1.playSound("smallSelect");
                        return;
                    }
                }

                foreach (KeyValuePair<string, Rectangle> profileButton in socialProfileNpcButtonBounds)
                {
                    if (!profileButton.Value.Contains(x, y))
                        continue;

                    OpenSocialApp();
                    OpenSocialProfile(profileButton.Key, actorIsPlayer: false);
                    Game1.playSound("smallSelect");
                    return;
                }

                int yStart = yPositionOnScreen + 150;

                for (int i = 0; i < visibleSlots; i++)
                {
                    int index = i + scrollOffset;
                    if (index >= messageableNpcList.Count) break;

                    Rectangle slot = new Rectangle(xPositionOnScreen + 40, yStart + i * spacing, 400, 60);
                    if (slot.Contains(x, y))
                    {
                        currentMessage = "";
                        selectedNpc = messageableNpcList[index].name;
                        MessageManager.SetUnreadCount(selectedNpc);
                        messageHistory = MessageManager.GetMessages(selectedNpc);
                        OpenChat(selectedNpc);
                        return;
                    }
                }
            }
            else if (currentApp == "appText" && selectedNpc != null)
            {
                if (okButton.containsPoint(x, y))
                {
                    if (!string.IsNullOrWhiteSpace(currentMessage))
                    {
                        onPlayerSend();
                    }
                }
            }


            
            else if (gameCart.containsPoint(x, y) && currentApp == "appGame")
            {
                exitThisMenu();
                int type = Game1.dayOfMonth % 2 == 0 ? 3 : 2;
                Game1.currentMinigame = new StardewValley.Minigames.MineCart(0, type);
                return;
            }
            else if (gameJack.containsPoint(x, y) && currentApp == "appGame")
            {
                exitThisMenu();
                int bet = Game1.dayOfMonth % 2 == 0 ? 1000 : 100;
                int costToStart = 100; // Always 100G to start
                int playerGold = Game1.player.Money;
                int playerClubCoins = Game1.player.clubCoins;

                // Check if player has enough gold and coins
                if (playerGold < costToStart)
                {
                    Game1.addHUDMessage(new HUDMessage($"You don't have enough 100 Gold!", 3));
                    return;
                }
                if (playerClubCoins < bet)
                {
                    Game1.addHUDMessage(new HUDMessage($"You don't have enough Casino Coins for today bet: {bet}!", 3));
                    return;
                }

                // Confirmation message
                string question = $"Play Calico Jack?\nCost to start: {costToStart}G\nToday's bet: {bet} Casino Coins";

                // Show confirmation dialog
                Game1.activeClickableMenu = new ConfirmationDialog(
                    question,
                    onConfirm: (Farmer who) =>
                    {
                        Game1.player.Money -= costToStart;

                        bool highStakes = bet == 1000;
                        Game1.activeClickableMenu = null;
                        Game1.currentMinigame = new StardewValley.Minigames.CalicoJack(highStakes: highStakes);
                    },
                    onCancel: (Farmer who) =>
                    {
                        Game1.activeClickableMenu = null;
                    }
                );

                return;
            }
            else if (gameDarts.containsPoint(x, y) && currentApp == "appGame")
            {
                exitThisMenu();
                Game1.currentMinigame = new StardewValley.Minigames.Darts();
                return;
            }
            else if (gamePirate.containsPoint(x, y) && currentApp == "appGame")
            {
                exitThisMenu();
                Game1.currentMinigame = new StardewValley.Minigames.AbigailGame();
                return;
            }
            else if (gameSpin.containsPoint(x, y) && currentApp == "appGame")
            {
                exitThisMenu();
                if (Game1.player.clubCoins < 10)
                {
                    Game1.addHUDMessage(new HUDMessage($"You don't have enough Casino Coins!", 3));
                    return;
                }
                Game1.currentMinigame = new StardewValley.Minigames.Slots();
                return;
            }
            else if (gameCrane.containsPoint(x, y) && currentApp == "appGame")
            {
                exitThisMenu();
                string question = $"Play Crane Game?\nCost to start: 750G";

                // Show confirmation dialog
                Game1.activeClickableMenu = new ConfirmationDialog(
                    question,
                    onConfirm: (Farmer who) =>
                    {
                        Game1.player.Money -= 750;

                        Game1.activeClickableMenu = null;
                        Game1.currentMinigame = new StardewValley.Minigames.CraneGame();
                    },
                    onCancel: (Farmer who) =>
                    {
                        Game1.activeClickableMenu = null;
                    }
                );
                return;
            }


        }


        public override void receiveScrollWheelAction(int direction)
        {
            if (selectedNpc != null && currentApp == "appText")
            {
                float wheelSteps = direction / 120f;
                chatScrollTarget = Math.Clamp(
                    chatScrollTarget - wheelSteps * ChatScrollPixelsPerWheelNotch,
                    0f,
                    CalculateScrollToBottomOffset(messageHistory));
            }
            else if (currentApp == "appText")
            {
                scrollOffset -= direction / 120;
                scrollOffset = Math.Max(0, Math.Min(scrollOffset, messageableNpcList.Count - maxVisibleNPCs));
            }
            else if (currentApp == SocialAppState)
            {
                HandleSocialScroll(direction);
            }
            else if (currentApp == null)
            {
                if (TryChangeHomeAppPage(direction > 0 ? -1 : 1))
                    Game1.playSound("shwip");
            }
            else if (currentApp == "appSetting")
            {
                if (currentSettingMenuState == SettingMenuSoundState)
                {
                    scrollOffset -= direction / 120;
                    scrollOffset = Math.Max(0, Math.Min(scrollOffset, phoneSoundList.Count - maxVisibleNPCs));
                }
                else if (currentSettingMenuState == SettingMenuTextColorState)
                {
                    scrollOffset -= direction / 120;
                    scrollOffset = Math.Max(0, Math.Min(scrollOffset, phoneTextColorList.Count - maxVisibleNPCs));
                }
                else if (currentSettingMenuState == SettingMenuThemeState)
                {
                    scrollOffset -= direction / 120;
                    scrollOffset = Math.Max(0, Math.Min(scrollOffset, phoneThemeList.Count - maxVisibleNPCs));
                }
            }
            else if (currentApp == "appNotification")
            {
                float wheelSteps = direction / 120f;
                float maxScroll = CalculateNotificationScrollToBottomOffset(notificationHistory);
                notificationScrollTarget = Math.Clamp(
                    notificationScrollTarget - wheelSteps * ChatScrollPixelsPerWheelNotch,
                    0f,
                    maxScroll);
                notificationScrollOffset = Math.Clamp(notificationScrollOffset, 0f, maxScroll);
            }
        }


        public override void receiveKeyPress(Keys key)
        {
            if (key == Keys.Escape)
            {
                if (currentApp == "appText" && selectedNpc != null)
                {
                    MessageManager.SetUnreadCount(selectedNpc);
                    selectedNpc = null;
                    currentSuggestion = new("", "");
                    return;
                }
                
                if (currentApp != null)
                {
                    if (currentApp == SocialAppState)
                    {
                        HandleSocialBackNavigation();
                        return;
                    }

                    if (currentApp == "appSetting" && currentSettingMenuState != SettingMenuMainState)
                    {
                        currentSettingMenuState = SettingMenuMainState;
                        scrollOffset = 0;
                        return;
                    }

                    if (currentApp == ExternalGroupAppState)
                        ClearCurrentExternalGroup();

                    currentApp = null;
                    return;
                }

                currentMessage = "";
                exitThisMenu();
                return;
            }
            else if (currentApp == SocialAppState)
            {
                if (HandleSocialTyping(key))
                    return;
            }
            else if (selectedNpc == null && currentApp == "appText")
            {
                if (key == Keys.Back && currentMessage.Length > 0)
                {
                    currentMessage = currentMessage[..^1];
                    UpdateNpcList();
                }
                else
                {
                    Game1.playSound("coin");

                    bool shiftPressed = Keyboard.GetState().IsKeyDown(Keys.LeftShift) || Keyboard.GetState().IsKeyDown(Keys.RightShift);
                    char c = GetCharFromKey(key, shiftPressed);

                    if (c != '\0') currentMessage += c;
                    UpdateNpcList();
                }
            }
            else if (selectedNpc != null && currentApp == "appText")
            {
                if (key == Keys.Back && currentMessage.Length > 0)
                    currentMessage = currentMessage[..^1];
                else if (key == Keys.Enter)
                {

                    if (!string.IsNullOrWhiteSpace(currentMessage))
                    {
                        onPlayerSend();
                    }
                }
                else
                {
                    Game1.playSound("coin");

                    bool shiftPressed = Keyboard.GetState().IsKeyDown(Keys.LeftShift) || Keyboard.GetState().IsKeyDown(Keys.RightShift);
                    char c = GetCharFromKey(key, shiftPressed);

                    if (c != '\0') currentMessage += c;
                }
            }
        }

        private void onPlayerSend()
        {
            if (currentMessage.ToLower().Contains("dinner"))
                currentSuggestion = new($"Want to invite\n{selectedNpc} for dinner?", "[start dinner event]");
            else if (currentMessage.ToLower().Contains("birthday") && ModEntry.GetNpcsWithBirthdayToday().Contains(Game1.getCharacterFromName(selectedNpc)))
                currentSuggestion = new($"Want to celebrate\n{selectedNpc}'s birthday'?", "[start NPC birthday event]");
            else
                currentSuggestion = new("", "");


            MessageManager.AddMessage(selectedNpc, $"PLAYER: {currentMessage}", isFromPlayer: true);
            messageHistory = MessageManager.GetMessages(selectedNpc);
            string reply = sendTextMessage(selectedNpc, currentMessage);
            currentMessage = "";
            SnapChatScrollToBottom();
        }


        public static string sendTextMessage(string npcName, string userMessage)
        {
            if (!pendingMessages.ContainsKey(npcName))
                pendingMessages[npcName] = new List<string>();

            pendingMessages[npcName].Add(userMessage);

            // Cancel old timer if exists
            if (replyTimers.ContainsKey(npcName))
                replyTimers[npcName].Cancel();

            var cts = new CancellationTokenSource();
            replyTimers[npcName] = cts;

            // Start delayed task
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delay, cts.Token);
                    await SendBatchMessage(npcName);
                }
                catch (TaskCanceledException) { /* Timer reset */ }
            });

            return ""; // no immediate reply
        }

        private static async Task SendBatchMessage(string npcName)
        {
            if (!pendingMessages.TryGetValue(npcName, out var messages) || messages.Count == 0)
                return;

            // Combine player messages into one
            string merged = string.Join("\n", messages);
            int counter = messages.Count;

            // Clear the queue
            pendingMessages[npcName].Clear();

            // Call assistant (simulate with delay if needed)

            string reply = await ModEntry.SendMessageToAssistant(npcName, merged, counter);

            // Add reply to message history
            if (reply != null && reply != "")
            { 
                MessageManager.AddMessage(npcName, $"{npcName}: {reply}");
                if (selectedNpc == npcName)
                    messageHistory = MessageManager.GetMessages(npcName);
            }
        }



        private char GetCharFromKey(Keys key, bool shift)
        {
            switch (key)
            {
                // Letters
                case >= Keys.A and <= Keys.Z:
                    return shift ? key.ToString()[0] : char.ToLower(key.ToString()[0]);

                // Top row numbers
                case Keys.D0: return shift ? ')' : '0';
                case Keys.D1: return shift ? '!' : '1';
                case Keys.D2: return shift ? '@' : '2';
                case Keys.D3: return shift ? '#' : '3';
                case Keys.D4: return shift ? '$' : '4';
                case Keys.D5: return shift ? '%' : '5';
                case Keys.D6: return shift ? '^' : '6';
                case Keys.D7: return shift ? '&' : '7';
                case Keys.D8: return shift ? '*' : '8';
                case Keys.D9: return shift ? '(' : '9';

                // Numpad numbers
                case Keys.NumPad0: return '0';
                case Keys.NumPad1: return '1';
                case Keys.NumPad2: return '2';
                case Keys.NumPad3: return '3';
                case Keys.NumPad4: return '4';
                case Keys.NumPad5: return '5';
                case Keys.NumPad6: return '6';
                case Keys.NumPad7: return '7';
                case Keys.NumPad8: return '8';
                case Keys.NumPad9: return '9';

                // Symbols
                case Keys.OemPeriod: return shift ? '>' : '.';
                case Keys.OemComma: return shift ? '<' : ',';
                case Keys.OemQuestion: return shift ? '?' : '/';
                case Keys.OemSemicolon: return shift ? ':' : ';';
                case Keys.OemQuotes: return shift ? '"' : '\'';
                case Keys.OemOpenBrackets: return shift ? '{' : '[';
                case Keys.OemCloseBrackets: return shift ? '}' : ']';
                case Keys.OemPipe: return shift ? '|' : '\\';
                case Keys.OemMinus: return shift ? '_' : '-';
                case Keys.OemPlus: return shift ? '+' : '=';
                case Keys.Space: return ' ';

                default: return '\0';
            }
        }


        private List<string> SplitTextIntoLines(string text, SpriteFont font, int maxWidth)
        {
            List<string> lines = new();
            // Treat caret '^' and newline '\n' as explicit line breaks.
            string[] explicitLines = text.Split(new[] { '^', '\n' }, StringSplitOptions.None);

            foreach (var explicitLine in explicitLines)
            {
                if (explicitLine == "")
                {
                    lines.Add("");
                    continue;
                }

                string[] words = explicitLine.Split(' ');
                string line = "";

                foreach (var word in words)
                {
                    string test = string.IsNullOrEmpty(line) ? word : line + " " + word;
                    if (font.MeasureString(test).X > maxWidth)
                    {
                        if (!string.IsNullOrEmpty(line))
                            lines.Add(line);

                        // If single word itself is longer than the max width, split it character-wise.
                        if (font.MeasureString(word).X > maxWidth)
                        {
                            string partial = "";
                            foreach (char c in word)
                            {
                                string testPartial = partial + c;
                                if (font.MeasureString(testPartial).X > maxWidth)
                                {
                                    if (!string.IsNullOrEmpty(partial))
                                        lines.Add(partial);
                                    partial = c.ToString();
                                }
                                else
                                {
                                    partial = testPartial;
                                }
                            }
                            line = partial;
                        }
                        else
                        {
                            line = word;
                        }
                    }
                    else
                    {
                        line = test;
                    }
                }

                if (!string.IsNullOrEmpty(line))
                    lines.Add(line);
            }

            return lines;
        }

        private List<string> SplitNotificationIntoLines(string text, SpriteFont font, int maxWidth)
        {
            List<string> lines = new();
            // Treat caret '^' and newline '\n' as explicit line breaks.
            string[] explicitLines = text.Split(new[] { '^', '\n' }, StringSplitOptions.None);

            foreach (var explicitLine in explicitLines)
            {
                if (explicitLine == "")
                {
                    lines.Add("");
                    continue;
                }

                string[] words = explicitLine.Split(' ');
                string line = "";

                foreach (var word in words)
                {
                    string test = string.IsNullOrEmpty(line) ? word : line + " " + word;
                    if (font.MeasureString(test).X > maxWidth)
                    {
                        if (!string.IsNullOrEmpty(line))
                            lines.Add(line);

                        if (font.MeasureString(word).X > maxWidth)
                        {
                            string partial = "";
                            foreach (char c in word)
                            {
                                string testPartial = partial + c;
                                if (font.MeasureString(testPartial).X > maxWidth)
                                {
                                    if (!string.IsNullOrEmpty(partial))
                                        lines.Add(partial);
                                    partial = c.ToString();
                                }
                                else
                                {
                                    partial = testPartial;
                                }
                            }
                            line = partial;
                        }
                        else
                        {
                            line = word;
                        }
                    }
                    else
                    {
                        line = test;
                    }
                }

                if (!string.IsNullOrEmpty(line))
                    lines.Add(line);
            }

            return lines;
        }

        public void OpenChat(string npcName)
        {
            selectedNpc = npcName;
            messageHistory = MessageManager.GetMessages(npcName);
            SnapChatScrollToBottom();
        }

        public void OpenNotification()
        {
            NotificationManager.resetUnreadNotication();
            notificationHistory = NotificationManager.getNoticationList();
            notificationScrollOffset = 0f;
            notificationScrollTarget = 0f;
        }

        private void DrawSettingMenu(SpriteBatch b)
        {
            b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height), Color.Black * 0.6f);
            b.Draw(texturePhoneCapture, new Vector2(xPositionOnScreen, yPositionOnScreen), Color.White);
            b.Draw(texturePhoneBackground, new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 116), Color.White);
            backButton.draw(b);

            string title = currentSettingMenuState switch
            {
                SettingMenuSoundState => "Sound",
                SettingMenuTextColorState => "Text Color",
                SettingMenuThemeState => "Theme",
                _ => "Settings"
            };

            b.DrawString(Game1.dialogueFont, title, new Vector2(xPositionOnScreen + 105, yPositionOnScreen + 67), Color.Black);

            if (currentSettingMenuState == SettingMenuSoundState)
            {
                DrawSoundSettingList(b);
                return;
            }

            if (currentSettingMenuState == SettingMenuTextColorState)
            {
                DrawTextColorSettingList(b);
                return;
            }

            if (currentSettingMenuState == SettingMenuThemeState)
            {
                DrawThemeSettingList(b);
                return;
            }

            DrawSettingMainOptions(b);
        }

        private void DrawSettingMainOptions(SpriteBatch b)
        {
            settingOptionBounds.Clear();
            phoneSoundButton.Clear();
            phoneTextColorButton.Clear();
            phoneThemeButton.Clear();

            int yStart = yPositionOnScreen + 260;
            DrawSettingOptionRow(b, SettingMenuOptionTextColor, "Text Color", yStart);
            DrawSettingOptionRow(b, SettingMenuOptionSound, "Sound", yStart + spacing);
            DrawSettingOptionRow(b, SettingMenuOptionTheme, "Theme", yStart + spacing * 2);
        }

        private void DrawSettingOptionRow(SpriteBatch b, string optionId, string displayText, int rowY)
        {
            int rowX = xPositionOnScreen + 90;
            int rowWidth = 430;
            int rowHeight = 66;

            Rectangle rowBounds = new Rectangle(rowX, rowY, rowWidth, rowHeight);
            settingOptionBounds[optionId] = rowBounds;

            IClickableMenu.drawTextureBox(
                b,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                rowBounds.X,
                rowBounds.Y,
                rowBounds.Width,
                rowBounds.Height,
                new Color(255, 255, 255, 220),
                1f,
                false);

            b.DrawString(Game1.dialogueFont, displayText, new Vector2(rowBounds.X + 20, rowBounds.Y + 15), Color.Black);

            b.Draw(
                Game1.mouseCursors,
                new Rectangle(rowBounds.Right - 42, rowBounds.Y + 17, 32, 32),
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 33),
                Color.White);
        }

        private void DrawSoundSettingList(SpriteBatch b)
        {
            phoneSoundButton.Clear();
            phoneTextColorButton.Clear();
            phoneThemeButton.Clear();
            settingOptionBounds.Clear();
            scrollOffset = Math.Max(0, Math.Min(scrollOffset, Math.Max(0, phoneSoundList.Count - maxVisibleNPCs)));

            int yStart = yPositionOnScreen + 150;
            for (int i = 0; i < visibleSlots; i++)
            {
                int index = i + scrollOffset;
                if (index >= phoneSoundList.Count)
                    break;

                string soundString = phoneSoundList[index];
                int y = yStart + i * spacing;

                int nameX = xPositionOnScreen + 100;
                b.DrawString(Game1.dialogueFont, soundString, new Vector2(nameX, y + 15), Color.Black);

                Rectangle rect = new Rectangle(218, 428, 7, 7);
                if (MessageManager.currentPhoneSound == soundString)
                    rect = new Rectangle(211, 428, 7, 7);

                ClickableTextureComponent selectButton = new ClickableTextureComponent(
                    name: soundString,
                    bounds: new Rectangle(nameX + 370, y + 20, 35, 35),
                    label: null,
                    hoverText: "",
                    texture: Game1.mouseCursors,
                    sourceRect: rect,
                    scale: 5f
                );

                phoneSoundButton[soundString] = selectButton;
                selectButton.draw(b);
            }
        }

        private void DrawTextColorSettingList(SpriteBatch b)
        {
            phoneTextColorButton.Clear();
            phoneSoundButton.Clear();
            phoneThemeButton.Clear();
            settingOptionBounds.Clear();
            scrollOffset = Math.Max(0, Math.Min(scrollOffset, Math.Max(0, phoneTextColorList.Count - maxVisibleNPCs)));

            int yStart = yPositionOnScreen + 150;
            for (int i = 0; i < visibleSlots; i++)
            {
                int index = i + scrollOffset;
                if (index >= phoneTextColorList.Count)
                    break;

                string colorName = phoneTextColorList[index];
                int y = yStart + i * spacing;

                int nameX = xPositionOnScreen + 100;
                b.DrawString(Game1.dialogueFont, colorName, new Vector2(nameX, y + 15), Color.Black);

                if (!TryGetHomeTextColor(colorName, out Color previewColor))
                    previewColor = Color.Black;

                b.Draw(Game1.staminaRect, new Rectangle(nameX + 220, y + 24, 40, 24), Color.Black * 0.5f);
                b.Draw(Game1.staminaRect, new Rectangle(nameX + 222, y + 26, 36, 20), previewColor);

                Rectangle rect = new Rectangle(218, 428, 7, 7);
                if (string.Equals(MessageManager.currentPhoneTextColor, colorName, StringComparison.OrdinalIgnoreCase))
                    rect = new Rectangle(211, 428, 7, 7);

                ClickableTextureComponent selectButton = new ClickableTextureComponent(
                    name: colorName,
                    bounds: new Rectangle(nameX + 370, y + 20, 35, 35),
                    label: null,
                    hoverText: "",
                    texture: Game1.mouseCursors,
                    sourceRect: rect,
                    scale: 5f
                );

                phoneTextColorButton[colorName] = selectButton;
                selectButton.draw(b);
            }
        }

        private void DrawThemeSettingList(SpriteBatch b)
        {
            phoneThemeButton.Clear();
            phoneSoundButton.Clear();
            phoneTextColorButton.Clear();
            settingOptionBounds.Clear();

            RefreshPhoneThemeList();
            scrollOffset = Math.Max(0, Math.Min(scrollOffset, Math.Max(0, phoneThemeList.Count - maxVisibleNPCs)));

            int yStart = yPositionOnScreen + 150;
            for (int i = 0; i < visibleSlots; i++)
            {
                int index = i + scrollOffset;
                if (index >= phoneThemeList.Count)
                    break;

                string themeName = phoneThemeList[index];
                int y = yStart + i * spacing;

                int nameX = xPositionOnScreen + 100;
                b.DrawString(Game1.dialogueFont, themeName, new Vector2(nameX, y + 15), Color.Black);

                Rectangle rect = new Rectangle(218, 428, 7, 7);
                if (string.Equals(MessageManager.currentPhoneTheme, themeName, StringComparison.OrdinalIgnoreCase))
                    rect = new Rectangle(211, 428, 7, 7);

                ClickableTextureComponent selectButton = new ClickableTextureComponent(
                    name: themeName,
                    bounds: new Rectangle(nameX + 370, y + 20, 35, 35),
                    label: null,
                    hoverText: "",
                    texture: Game1.mouseCursors,
                    sourceRect: rect,
                    scale: 5f
                );

                phoneThemeButton[themeName] = selectButton;
                selectButton.draw(b);
            }
        }

        private void RefreshPhoneThemeList()
        {
            phoneThemeList = AssetHelper.GetAvailablePhoneThemeNames();
            if (phoneThemeList.Count == 0)
                phoneThemeList = new List<string> { AssetHelper.DefaultPhoneThemeName };
        }

        private void ApplyPhoneThemeSelection(string themeName)
        {
            AssetHelper.SetCurrentPhoneTheme(themeName);
            Textures.LoadTextures();
            MessageManager.currentPhoneTheme = AssetHelper.CurrentPhoneThemeName;
            ReloadThemeTextures();
        }

        public void ReloadThemeTextures()
        {
            texturePhoneBackground = Textures.PhoneBackground;
            texturePhoneCapture = Textures.PhoneEmpty;
            texturePortraitBackground = Textures.Background;

            textureAppCamera = Textures.AppCamera;
            textureAppText = Textures.AppText;
            textureAppPhoto = Textures.AppPhoto;
            textureAppSocial = Textures.AppSocial;
            textureAppSetting = Textures.AppSetting;
            textureAppGame = Textures.AppGame;
            textureAppNotification = Textures.AppNotification;

            textureGameDarts = Textures.GameDarts;
            textureGameJack = Textures.GameJack;
            textureGameCart = Textures.GameCart;
            textureGameCrane = Textures.GameCrane;
            textureGamePirate = Textures.GamePirate;
            textureGameSpin = Textures.GameSpin;
        }

        private Color GetCurrentHomeTextColor()
        {
            if (TryGetHomeTextColor(MessageManager.currentPhoneTextColor, out Color color))
                return color;

            return Color.Black;
        }

        private bool TryGetHomeTextColor(string colorName, out Color color)
        {
            if (string.IsNullOrWhiteSpace(colorName))
            {
                color = Color.Black;
                return false;
            }

            if (phoneTextColorMap.TryGetValue(colorName, out color))
                return true;

            color = Color.Black;
            return false;
        }

        private bool IsSameFilePath(string left, string right)
        {
            return string.Equals(left ?? "", right ?? "", StringComparison.OrdinalIgnoreCase);
        }

        private void ResetPhoneBackgroundToDefault()
        {
            MessageManager.currentPhoneBackground = "";
            phoneBackgroundImage?.Dispose();
            phoneBackgroundImage = null;
        }

        private void ApplyPhoneBackground(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                ResetPhoneBackgroundToDefault();
                return;
            }

            if (!File.Exists(imagePath))
            {
                ResetPhoneBackgroundToDefault();
                return;
            }

            try
            {
                using (FileStream stream = new FileStream(imagePath, FileMode.Open))
                using (Texture2D fullImage = Texture2D.FromStream(Game1.graphics.GraphicsDevice, stream))
                {
                    phoneBackgroundImage?.Dispose();
                    phoneBackgroundImage = CropTexture(fullImage);
                }

                MessageManager.currentPhoneBackground = imagePath;
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor.Log($"Failed to load phone background image '{imagePath}': {ex.Message}", LogLevel.Warn);
                ResetPhoneBackgroundToDefault();
            }
        }

        private int CalculateChatContentHeight(List<string> msgList)
        {
            if (msgList == null || msgList.Count == 0)
                return 0;

            List<string> snapshot = msgList.ToList();
            if (snapshot.Count == 0)
                return 0;

            SpriteFont font = Game1.smallFont;
            int lineHeight = (int)font.MeasureString("A").Y + 4;
            int totalHeight = 0;

            foreach (string msg in snapshot)
            {
                if (string.IsNullOrEmpty(msg))
                {
                    totalHeight += lineHeight + 10;
                    continue;
                }

                string text = msg.Contains(':') ? msg[(msg.IndexOf(':') + 1)..].Trim() : msg;
                List<string> wrappedLines = SplitTextIntoLines(text, font, maxBubbleWidth);
                int bubbleHeight = Math.Max(1, wrappedLines.Count) * lineHeight + 10;
                totalHeight += bubbleHeight + 10;
            }

            return totalHeight;
        }

        private float CalculateScrollToBottomOffset(List<string> msgList)
        {
            int contentHeight = CalculateChatContentHeight(msgList);
            return Math.Max(0f, contentHeight - ChatViewportHeight);
        }

        private void ClampChatScroll()
        {
            ClampChatScroll(messageHistory);
        }

        private void ClampChatScroll(List<string> msgList)
        {
            float maxScroll = CalculateScrollToBottomOffset(msgList);
            chatScrollTarget = Math.Clamp(chatScrollTarget, 0f, maxScroll);
            chatScrollOffset = Math.Clamp(chatScrollOffset, 0f, maxScroll);
        }

        private void SnapChatScrollToBottom()
        {
            float bottomOffset = CalculateScrollToBottomOffset(messageHistory);
            chatScrollOffset = bottomOffset;
            chatScrollTarget = bottomOffset;
        }


        private void LoadImageAtIndex(int index)
        {
            if (index < 0 || index >= capturedImages.Count)
            {
                currentDisplayedImage?.Dispose();
                currentDisplayedImage = null;
                currentDisplayedImageIsSquare = false;
                return;
            }

            using (FileStream stream = new FileStream(capturedImages[index], FileMode.Open))
            using (Texture2D sourceImage = Texture2D.FromStream(Game1.graphics.GraphicsDevice, stream))
            {
                currentDisplayedImage?.Dispose();
                currentDisplayedImage = CropTexture(sourceImage);
                currentDisplayedImageIsSquare = sourceImage.Width == sourceImage.Height;
            }
        }

        public void UpdateNpcList()
        {
            messageableNpcList = new List<ClickableComponent>();
            Dictionary<string, long> latestTimestamps = MessageManager.GetLatestAddDictionary();
            string filter = currentMessage?.ToLower() ?? "";

            List<NPC> villagers = Utility.getAllVillagers()
            .Where(n =>
                (n.IsVillager &&
                 n.CanSocialize &&
                 !n.IsMonster &&
                 Game1.player.getFriendshipHeartLevelForNPC(n.Name) >= 1
                 //|| n.Name == "Lewis" || n.Name == "Robin"
                 )
                 && n.Name.ToLower().Contains(filter)
            )
            .OrderByDescending(npc => MessageManager.favouriteNpc.Contains(npc.Name))
            .ThenByDescending(npc => latestTimestamps.TryGetValue(npc.Name, out long ts) ? ts : 0)
            .ThenBy(npc => npc.Name)
            .ToList();

            for (int i = 0; i < villagers.Count; i++)
            {
                messageableNpcList.Add(new ClickableComponent(new Rectangle(0, 0, 0, 0), villagers[i].Name));
            }
        }
        private Texture2D CropTexture(Texture2D source, int x = 0, int y = 0, int width = 520, int height = 810)
        {
            int targetWidth = Math.Max(1, width);
            int targetHeight = Math.Max(1, height);

            int sourceX = Math.Clamp(x, 0, Math.Max(0, source.Width - 1));
            int sourceY = Math.Clamp(y, 0, Math.Max(0, source.Height - 1));
            int sourceWidth = Math.Max(1, Math.Min(width, source.Width - sourceX));
            int sourceHeight = Math.Max(1, Math.Min(height, source.Height - sourceY));

            // For the phone gallery/background path, fit the full image inside the viewport
            // so portrait, landscape, and square photos all load safely.
            if (x == 0 && y == 0 && width == 520 && height == 810)
            {
                sourceX = 0;
                sourceY = 0;
                sourceWidth = source.Width;
                sourceHeight = source.Height;
            }

            Color[] sourceData = new Color[source.Width * source.Height];
            source.GetData(sourceData);

            Color[] targetData = new Color[targetWidth * targetHeight];

            float scale = Math.Min(targetWidth / (float)sourceWidth, targetHeight / (float)sourceHeight);
            int drawWidth = Math.Max(1, (int)Math.Round(sourceWidth * scale));
            int drawHeight = Math.Max(1, (int)Math.Round(sourceHeight * scale));
            int drawOffsetX = (targetWidth - drawWidth) / 2;
            int drawOffsetY = (targetHeight - drawHeight) / 2;

            for (int drawY = 0; drawY < drawHeight; drawY++)
            {
                int srcY = sourceY + Math.Min(sourceHeight - 1, (int)(drawY * (sourceHeight / (float)drawHeight)));
                int dstY = drawOffsetY + drawY;

                for (int drawX = 0; drawX < drawWidth; drawX++)
                {
                    int srcX = sourceX + Math.Min(sourceWidth - 1, (int)(drawX * (sourceWidth / (float)drawWidth)));
                    int dstX = drawOffsetX + drawX;
                    targetData[dstY * targetWidth + dstX] = sourceData[srcY * source.Width + srcX];
                }
            }

            Texture2D fittedTexture = new Texture2D(Game1.graphics.GraphicsDevice, targetWidth, targetHeight);
            fittedTexture.SetData(targetData);
            return fittedTexture;
        }

        private List<HomeAppEntry> BuildHomeAppsSnapshot()
        {
            var apps = new List<HomeAppEntry>
            {
                new HomeAppEntry
                {
                    Id = BuiltinAppNotificationId,
                    DisplayName = "Notification",
                    IconTexture = textureAppNotification,
                    SourceRect = new Rectangle(0, 0, 84, 84),
                    GetBadgeCount = () => Math.Max(0, NotificationManager.getUnreadNotication())
                },
                new HomeAppEntry
                {
                    Id = BuiltinAppTextId,
                    DisplayName = "Messages",
                    IconTexture = textureAppText,
                    SourceRect = new Rectangle(0, 0, 84, 84),
                    GetBadgeCount = () => MessageManager.unreadCounts.Count(pair => pair.Value > 0)
                },
                new HomeAppEntry
                {
                    Id = BuiltinAppCameraId,
                    DisplayName = "Camera",
                    IconTexture = textureAppCamera,
                    SourceRect = new Rectangle(0, 0, 84, 84)
                },
                new HomeAppEntry
                {
                    Id = BuiltinAppPhotoId,
                    DisplayName = "Photos",
                    IconTexture = textureAppPhoto,
                    SourceRect = new Rectangle(0, 0, 84, 84)
                },
                new HomeAppEntry
                {
                    Id = BuiltinAppSocialId,
                    DisplayName = "StardewConnect",
                    IconTexture = textureAppSocial,
                    SourceRect = new Rectangle(0, 0, 84, 84),
                    GetBadgeCount = () => GetTotalSocialNotificationCount()
                },
                new HomeAppEntry
                {
                    Id = BuiltinAppSettingId,
                    DisplayName = "Settings",
                    IconTexture = textureAppSetting,
                    SourceRect = new Rectangle(0, 0, 84, 84)
                },
                new HomeAppEntry
                {
                    Id = BuiltinAppCalendarId,
                    DisplayName = "Calendar",
                    IconTexture = furnitureTexture,
                    SourceRect = new Rectangle(417, 698, 15, 16)
                },
                new HomeAppEntry
                {
                    Id = BuiltinAppGameId,
                    DisplayName = "Games",
                    IconTexture = textureAppGame,
                    SourceRect = new Rectangle(0, 0, 84, 84)
                }
            };

            foreach (RegisteredPhoneApp app in ModEntry.GetRegisteredPhoneAppsSnapshot())
            {
                apps.Add(new HomeAppEntry
                {
                    Id = app.CompositeId,
                    DisplayName = app.DisplayName,
                    IconTexture = app.IconTexture,
                    SourceRect = app.SourceRect,
                    GetBadgeCount = () => GetRegisteredAppBadgeCount(app)
                });
            }

            return apps;
        }

        private int CalculateNotificationContentHeight(List<string> msgList)
        {
            if (msgList == null || msgList.Count == 0)
                return 0;

            List<string> snapshot = msgList.ToList();
            if (snapshot.Count == 0)
                return 0;

            SpriteFont font = Game1.smallFont;
            int lineHeight = (int)font.MeasureString("A").Y + 4;
            int totalHeight = 0;

            foreach (string msg in snapshot)
            {
                List<string> wrappedLines = SplitNotificationIntoLines(msg, font, 485);
                int bubbleHeight = Math.Max(1, wrappedLines.Count) * lineHeight + 10;
                totalHeight += bubbleHeight + 20;
            }

            return totalHeight;
        }

        private float CalculateNotificationScrollToBottomOffset(List<string> msgList)
        {
            int contentHeight = CalculateNotificationContentHeight(msgList);
            return Math.Max(0f, contentHeight - NotificationViewportHeight);
        }

        private void ClampNotificationScroll()
        {
            ClampNotificationScroll(notificationHistory);
        }

        private void ClampNotificationScroll(List<string> msgList)
        {
            float maxScroll = CalculateNotificationScrollToBottomOffset(msgList);
            notificationScrollTarget = Math.Clamp(notificationScrollTarget, 0f, maxScroll);
            notificationScrollOffset = Math.Clamp(notificationScrollOffset, 0f, maxScroll);
        }

        private void DrawHomeApps(SpriteBatch b)
        {
            homeAppClickBounds.Clear();
            homeAppPrevPageBounds = Rectangle.Empty;
            homeAppNextPageBounds = Rectangle.Empty;

            List<HomeAppEntry> apps = BuildHomeAppsSnapshot();
            if (apps.Count == 0)
            {
                homeAppPage = 0;
                return;
            }

            int totalPages = (int)Math.Ceiling(apps.Count / (double)HomeAppsPerPage);
            homeAppPage = Math.Clamp(homeAppPage, 0, Math.Max(0, totalPages - 1));

            List<HomeAppEntry> pageApps = apps
                .Skip(homeAppPage * HomeAppsPerPage)
                .Take(HomeAppsPerPage)
                .ToList();

            for (int i = 0; i < pageApps.Count; i++)
            {
                HomeAppEntry app = pageApps[i];
                int col = i % HomeAppsColumns;
                int row = i / HomeAppsColumns;

                Rectangle appBounds = new Rectangle(
                    xPositionOnScreen + HomeAppStartX + col * HomeAppSpacingX,
                    yPositionOnScreen + HomeAppStartY + row * HomeAppSpacingY,
                    84,
                    84);

                DrawAppIcon(b, app.IconTexture, appBounds, app.SourceRect);
                DrawAppLabel(b, appBounds, app.DisplayName);
                homeAppClickBounds[app.Id] = appBounds;

                int badgeCount = GetHomeAppBadgeCount(app);
                if (badgeCount > 0)
                    DrawAppBadge(b, appBounds, badgeCount);
            }

            if (totalPages <= 1)
                return;

            homeAppPrevPageBounds = new Rectangle(xPositionOnScreen + 40, yPositionOnScreen + 790, 64, 64);
            homeAppNextPageBounds = new Rectangle(xPositionOnScreen + 496, yPositionOnScreen + 790, 64, 64);

            if (homeAppPage > 0)
            {
                b.Draw(
                    Game1.mouseCursors,
                    homeAppPrevPageBounds,
                    Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 44),
                    Color.White);
            }

            if (homeAppPage < totalPages - 1)
            {
                b.Draw(
                    Game1.mouseCursors,
                    homeAppNextPageBounds,
                    Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 33),
                    Color.White);
            }

            string pageText = $"{homeAppPage + 1}/{totalPages}";
            Vector2 pageTextSize = Game1.smallFont.MeasureString(pageText);
            Vector2 pageTextPosition = new Vector2(
                xPositionOnScreen + 300 - pageTextSize.X / 2f,
                yPositionOnScreen + 812 - pageTextSize.Y / 2f);
            b.DrawString(Game1.smallFont, pageText, pageTextPosition, GetCurrentHomeTextColor());
        }

        private void DrawExternalGroupItems(SpriteBatch b)
        {
            externalGroupItemClickBounds.Clear();

            if (string.IsNullOrWhiteSpace(currentExternalGroupId))
                return;

            List<RegisteredPhoneAppGroupItem> items = ModEntry.GetRegisteredPhoneAppGroupItemsSnapshot(currentExternalGroupId);
            if (items.Count == 0)
            {
                b.DrawString(Game1.smallFont, "No app in this group yet.", new Vector2(xPositionOnScreen + 110, yPositionOnScreen + 260), Color.Black);
                return;
            }

            int visibleCount = Math.Min(9, items.Count);
            for (int i = 0; i < visibleCount; i++)
            {
                RegisteredPhoneAppGroupItem item = items[i];
                int col = i % ExternalGroupColumns;
                int row = i / ExternalGroupColumns;

                Rectangle itemBounds = new Rectangle(
                    xPositionOnScreen + ExternalGroupStartX + col * ExternalGroupSpacingX,
                    yPositionOnScreen + ExternalGroupStartY + row * ExternalGroupSpacingY,
                    84,
                    84);

                DrawAppIcon(b, item.IconTexture, itemBounds, item.SourceRect);
                DrawAppLabel(b, itemBounds, item.DisplayName);
                externalGroupItemClickBounds[item.CompositeId] = itemBounds;

                int badgeCount = GetRegisteredGroupItemBadgeCount(item);
                if (badgeCount > 0)
                    DrawAppBadge(b, itemBounds, badgeCount);
            }
        }

        private bool TryHandleHomeAppClick(string appId)
        {
            switch (appId)
            {
                case BuiltinAppNotificationId:
                    OpenNotification();
                    currentApp = "appNotification";
                    return true;

                case BuiltinAppTextId:
                    UpdateNpcList();
                    currentApp = "appText";
                    return true;

                case BuiltinAppCameraId:
                    currentApp = "appCamera";
                    return true;

                case BuiltinAppPhotoId:
                    OpenPhotoApp();
                    return true;

                case BuiltinAppSocialId:
                    OpenSocialApp();
                    return true;

                case BuiltinAppSettingId:
                    currentSettingMenuState = SettingMenuMainState;
                    scrollOffset = 0;
                    currentApp = "appSetting";
                    return true;

                case BuiltinAppCalendarId:
                    ClosePhoneMenu();
                    Game1.activeClickableMenu = new Billboard();
                    return true;

                case BuiltinAppGameId:
                    currentApp = "appGame";
                    return true;

                default:
                    return ModEntry.TryInvokeRegisteredPhoneApp(appId, this);
            }
        }

        public void OpenCameraApp()
        {
            if (currentApp == ExternalGroupAppState)
                ClearCurrentExternalGroup();

            currentApp = "appCamera";
        }

        private void OpenPhotoApp()
        {
            ApplyPhoneBackground(MessageManager.currentPhoneBackground);

            capturedImages = Directory.Exists(userCaptureFolderPath)
                ? Directory.GetFiles(userCaptureFolderPath, "*.png")
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .ToList()
                : new List<string>();

            currentApp = "appPhoto";

            if (capturedImages.Count > 0)
            {
                currentImageIndex = 0;
                LoadImageAtIndex(currentImageIndex);
            }
            else
            {
                currentDisplayedImage?.Dispose();
                currentDisplayedImage = null;
                currentDisplayedImageIsSquare = false;
                currentImageIndex = -1;
            }
        }

        private void AdjustCameraZoom(float delta)
        {
            float currentZoom = ModEntry.cameraZoomFactor;
            float targetZoom = Math.Clamp(currentZoom + delta, CameraZoomMin, CameraZoomMax);
            targetZoom = (float)Math.Round(targetZoom / CameraZoomStep) * CameraZoomStep;
            targetZoom = Math.Clamp(targetZoom, CameraZoomMin, CameraZoomMax);

            if (Math.Abs(targetZoom - currentZoom) <= 0.0001f)
            {
                Game1.playSound("cancel");
                return;
            }

            ModEntry.cameraZoomFactor = targetZoom;
            Game1.playSound("drumkit6");
        }

        private Matrix CreateCameraLandscapeRotationMatrix()
        {
            Point pivot = GetCameraLandscapeRotationPivot();
            return Matrix.CreateTranslation(-pivot.X, -pivot.Y, 0f)
                * Matrix.CreateRotationZ(-MathHelper.PiOver2)
                * Matrix.CreateTranslation(pivot.X, pivot.Y, 0f);
        }

        private Point GetCameraLandscapeRotationPivot()
        {
            Rectangle cameraViewport = ModEntry.GetPhoneCameraViewportBounds(xPositionOnScreen, yPositionOnScreen);
            return cameraViewport.Center;
        }

        private static Point RotatePointClockwise90(Point point, Point pivot)
        {
            int deltaX = point.X - pivot.X;
            int deltaY = point.Y - pivot.Y;
            return new Point(pivot.X + deltaY, pivot.Y - deltaX);
        }

        private Rectangle GetLandscapeRotatedBounds(Rectangle bounds)
        {
            if (!ModEntry.cameraLandscapeMode)
                return bounds;

            Point pivot = GetCameraLandscapeRotationPivot();

            Point topLeft = RotatePointClockwise90(new Point(bounds.Left, bounds.Top), pivot);
            Point topRight = RotatePointClockwise90(new Point(bounds.Right - 1, bounds.Top), pivot);
            Point bottomLeft = RotatePointClockwise90(new Point(bounds.Left, bounds.Bottom - 1), pivot);
            Point bottomRight = RotatePointClockwise90(new Point(bounds.Right - 1, bounds.Bottom - 1), pivot);

            int minX = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
            int maxX = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
            int minY = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
            int maxY = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));

            return new Rectangle(minX, minY, (maxX - minX) + 1, (maxY - minY) + 1);
        }

        private Rectangle GetActivePhoneDragBounds()
        {
            Rectangle defaultBounds = new Rectangle(xPositionOnScreen, yPositionOnScreen, width, height);

            if (currentApp == "appCamera" && ModEntry.cameraLandscapeMode)
                return GetLandscapeRotatedBounds(defaultBounds);

            return defaultBounds;
        }

        private void ClampPhoneMenuToViewport()
        {
            Rectangle activeBounds = GetActivePhoneDragBounds();

            int leftOffset = activeBounds.Left - xPositionOnScreen;
            int rightOffset = activeBounds.Right - xPositionOnScreen;
            int topOffset = activeBounds.Top - yPositionOnScreen;
            int bottomOffset = activeBounds.Bottom - yPositionOnScreen;

            int minX = -leftOffset;
            int maxX = Game1.uiViewport.Width - rightOffset;
            int minY = -topOffset;
            int maxY = Game1.uiViewport.Height - bottomOffset;

            xPositionOnScreen = minX > maxX
                ? (minX + maxX) / 2
                : Math.Clamp(xPositionOnScreen, minX, maxX);

            yPositionOnScreen = minY > maxY
                ? (minY + maxY) / 2
                : Math.Clamp(yPositionOnScreen, minY, maxY);
        }

        private bool IsCameraCaptureButtonPressed(int x, int y)
        {
            Rectangle hitBounds = GetLandscapeRotatedBounds(captureButton.bounds);
            return hitBounds.Contains(x, y);
        }

        private bool IsBackButtonPressed(int x, int y)
        {
            if (backButton.containsPoint(x, y))
                return true;

            if (currentApp == "appCamera" && ModEntry.cameraLandscapeMode)
                return GetLandscapeRotatedBounds(backButton.bounds).Contains(x, y);

            return false;
        }

        private void DrawCameraControlButton(SpriteBatch b, Rectangle bounds, string label, bool active)
        {
            Color boxColor = active
                ? new Color(160, 220, 255, 230)
                : new Color(255, 255, 255, 220);

            IClickableMenu.drawTextureBox(
                b,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                boxColor,
                1f,
                false);

            Vector2 textSize = Game1.smallFont.MeasureString(label);
            Vector2 textPosition = new Vector2(
                bounds.X + ((bounds.Width - textSize.X) / 2f),
                bounds.Y + ((bounds.Height - textSize.Y) / 2f) + 5f);

            b.DrawString(Game1.smallFont, label, textPosition, Color.Black);
        }

        private void DrawCaptureOutline(SpriteBatch b, Rectangle bounds, Color color)
        {
            if (bounds.Width <= 1 || bounds.Height <= 1)
                return;

            b.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Y, bounds.Width, 2), color);
            b.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2), color);
            b.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Y, 2, bounds.Height), color);
            b.Draw(Game1.staminaRect, new Rectangle(bounds.Right - 2, bounds.Y, 2, bounds.Height), color);
        }

        private void DrawOpenSocialProfileButton(SpriteBatch b, Rectangle bounds)
        {
            IClickableMenu.drawTextureBox(
                b,
                Game1.mouseCursors,
                new Rectangle(80, 0, 13, 13),
                bounds.X - 3,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                new Color(255, 255, 255, 220),
                3f,
                false);

            Color iconColor = new Color(35, 35, 35);

            Rectangle lensBounds = new Rectangle(bounds.X + 8, bounds.Y + 7, 14, 14);
            // b.Draw(Game1.staminaRect, new Rectangle(lensBounds.X, lensBounds.Y, lensBounds.Width, 2), iconColor);
            // b.Draw(Game1.staminaRect, new Rectangle(lensBounds.X, lensBounds.Bottom - 2, lensBounds.Width, 2), iconColor);
            // b.Draw(Game1.staminaRect, new Rectangle(lensBounds.X, lensBounds.Y, 2, lensBounds.Height), iconColor);
            // b.Draw(Game1.staminaRect, new Rectangle(lensBounds.Right - 2, lensBounds.Y, 2, lensBounds.Height), iconColor);

            // b.Draw(Game1.staminaRect, new Rectangle(lensBounds.Right - 1, lensBounds.Bottom - 1, 7, 2), iconColor);
            // b.Draw(Game1.staminaRect, new Rectangle(lensBounds.Right + 4, lensBounds.Bottom - 1, 2, 6), iconColor);
        }

        private void DrawAppIcon(SpriteBatch b, Texture2D texture, Rectangle bounds, Rectangle? sourceRect)
        {
            Rectangle source = sourceRect ?? new Rectangle(0, 0, texture.Width, texture.Height);
            float scale = Math.Min(bounds.Width / (float)source.Width, bounds.Height / (float)source.Height);

            int drawWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
            int drawHeight = Math.Max(1, (int)Math.Round(source.Height * scale));

            Rectangle drawRect = new Rectangle(
                bounds.X + (bounds.Width - drawWidth) / 2,
                bounds.Y + (bounds.Height - drawHeight) / 2,
                drawWidth,
                drawHeight);

            b.Draw(texture, drawRect, source, Color.White);
        }

        private void DrawAppLabel(SpriteBatch b, Rectangle appBounds, string appName)
        {
            SpriteFont labelFont = Game1.smallFont;
            string name = appName ?? "";
            float viewportWidth = 95f;
            float labelWidth = labelFont.MeasureString(name).X * AppLabelFontScale;

            if (labelWidth <= viewportWidth)
            {
                DrawStaticAppLabel(b, appBounds, name);
                return;
            }

            DrawMarqueeAppLabel(b, appBounds, name, viewportWidth);
        }

        private void DrawStaticAppLabel(SpriteBatch b, Rectangle appBounds, string label)
        {
            SpriteFont labelFont = Game1.smallFont;
            Vector2 labelSize = labelFont.MeasureString(label) * AppLabelFontScale;
            Vector2 labelPos = new Vector2(
                appBounds.X + (appBounds.Width - labelSize.X) / 2f,
                appBounds.Bottom + 4);

            b.DrawString(labelFont, label, labelPos, GetCurrentHomeTextColor(), 0f, Vector2.Zero, AppLabelFontScale, SpriteEffects.None, 1f);
        }

        private void DrawMarqueeAppLabel(SpriteBatch b, Rectangle appBounds, string appName, float viewportWidth)
        {
            SpriteFont labelFont = Game1.smallFont;
            string marqueeSource = appName + new string(' ', AppLabelTrailingSpaces);
            float viewportHeight = labelFont.LineSpacing * AppLabelFontScale;
            Vector2 viewportPos = new Vector2(
                appBounds.X + (appBounds.Width - viewportWidth) / 2f,
                appBounds.Bottom + 4);

            Rectangle clipRect = new Rectangle(
                (int)Math.Floor(viewportPos.X),
                (int)Math.Floor(viewportPos.Y),
                Math.Max(1, (int)Math.Ceiling(viewportWidth)),
                Math.Max(1, (int)Math.Ceiling(viewportHeight + 2f)));

            Rectangle viewportBounds = Game1.graphics.GraphicsDevice.Viewport.Bounds;
            clipRect = Rectangle.Intersect(clipRect, viewportBounds);
            if (clipRect.Width <= 0 || clipRect.Height <= 0)
                return;

            float marqueeWidth = labelFont.MeasureString(marqueeSource).X * AppLabelFontScale;
            if (marqueeWidth <= 0f)
            {
                DrawStaticAppLabel(b, appBounds, appName);
                return;
            }

            float scrollOffset = (float)((appLabelMarqueeElapsedSeconds * AppLabelMarqueePixelsPerSecond) % marqueeWidth);
            Vector2 drawPos = new Vector2(viewportPos.X - scrollOffset, viewportPos.Y);

            b.End();

            Rectangle previousScissorRect = Game1.graphics.GraphicsDevice.ScissorRectangle;
            Game1.graphics.GraphicsDevice.ScissorRectangle = clipRect;

            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, appLabelScissorRasterizer);

            Color textColor = GetCurrentHomeTextColor();
            b.DrawString(labelFont, marqueeSource, drawPos, textColor, 0f, Vector2.Zero, AppLabelFontScale, SpriteEffects.None, 1f);
            b.DrawString(labelFont, marqueeSource, new Vector2(drawPos.X + marqueeWidth, drawPos.Y), textColor, 0f, Vector2.Zero, AppLabelFontScale, SpriteEffects.None, 1f);

            b.End();
            Game1.graphics.GraphicsDevice.ScissorRectangle = previousScissorRect;
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
        }

        private void DrawAppBadge(SpriteBatch b, Rectangle appBounds, int badgeCount)
        {
            string badgeText = Math.Min(99, badgeCount).ToString();
            Vector2 badgeTextSize = Game1.smallFont.MeasureString(badgeText);

            int badgeWidth = Math.Max(32, (int)badgeTextSize.X + 18);
            int badgeHeight = Math.Max(24, (int)badgeTextSize.Y + 6);
            int badgeX = appBounds.Right - (badgeWidth / 2) - 10;
            int badgeY = appBounds.Y - (badgeHeight / 2) + 10;

            IClickableMenu.drawTextureBox(
                b,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                badgeX,
                badgeY,
                badgeWidth,
                badgeHeight,
                new Color(255, 0, 0, 150),
                1f,
                false);

            Vector2 badgeTextPosition = new Vector2(
                badgeX + (badgeWidth - badgeTextSize.X) / 2f,
                badgeY + (badgeHeight - badgeTextSize.Y) / 2f);
            b.DrawString(Game1.smallFont, badgeText, badgeTextPosition, Color.White);
        }

        private int GetHomeAppBadgeCount(HomeAppEntry app)
        {
            if (app.GetBadgeCount == null)
                return 0;

            try
            {
                return Math.Max(0, app.GetBadgeCount.Invoke());
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor.Log($"Badge callback failed for app '{app.Id}': {ex.Message}", LogLevel.Warn);
                return 0;
            }
        }

        private int GetRegisteredAppBadgeCount(RegisteredPhoneApp app)
        {
            if (app.GetBadgeCount == null)
                return 0;

            try
            {
                return Math.Max(0, app.GetBadgeCount.Invoke());
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor.Log($"Badge callback failed for smartphone app '{app.CompositeId}': {ex.Message}", LogLevel.Warn);
                return 0;
            }
        }

        private int GetRegisteredGroupItemBadgeCount(RegisteredPhoneAppGroupItem item)
        {
            if (item.GetBadgeCount == null)
                return 0;

            try
            {
                return Math.Max(0, item.GetBadgeCount.Invoke());
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor.Log($"Badge callback failed for smartphone app-group item '{item.CompositeId}': {ex.Message}", LogLevel.Warn);
                return 0;
            }
        }

        private bool TryChangeHomeAppPage(int delta)
        {
            List<HomeAppEntry> apps = BuildHomeAppsSnapshot();
            int totalPages = (int)Math.Ceiling(apps.Count / (double)HomeAppsPerPage);
            if (totalPages <= 1)
                return false;

            int nextPage = Math.Clamp(homeAppPage + delta, 0, totalPages - 1);
            if (nextPage == homeAppPage)
                return false;

            homeAppPage = nextPage;
            return true;
        }

        public void OpenRegisteredAppGroup(string groupCompositeId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(groupCompositeId))
                return;

            currentExternalGroupId = groupCompositeId;
            currentExternalGroupName = (displayName?.Length > 8)
                ? displayName.Substring(0, 8) + "..."
                : (displayName ?? ""); currentApp = ExternalGroupAppState;
            externalGroupItemClickBounds.Clear();
        }

        private void ClearCurrentExternalGroup()
        {
            currentExternalGroupId = "";
            currentExternalGroupName = "";
            externalGroupItemClickBounds.Clear();
        }

        public void ClosePhoneMenu()
        {
            if (currentApp == "appText" && selectedNpc != null)
            {
                MessageManager.SetUnreadCount(selectedNpc);
                selectedNpc = null;
                currentSuggestion = new("", "");
            }

            if (currentApp != null)
            {
                if (currentApp == ExternalGroupAppState)
                    ClearCurrentExternalGroup();

                if (currentApp == SocialAppState)
                    CloseSocialApp();

                if (currentApp != null)
                    currentApp = null;
            }

            currentMessage = "";
            exitThisMenu();
        }

    }
}
