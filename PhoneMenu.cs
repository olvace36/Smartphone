using System.Diagnostics.Metrics;
using System.Text;
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

        private sealed class ChatMessageEntry
        {
            public bool IsSystem { get; init; }
            public bool IsPlayer { get; init; }
            public bool IsPhoto { get; init; }
            public string Text { get; init; } = "";
            public string PhotoGroupId { get; init; } = "";
            public List<string> PhotoPaths { get; init; } = new();
            public string PhotoTag { get; init; } = "";
        }

        private sealed class ChatPhotoHoverEntry
        {
            public Rectangle Bounds { get; init; }
            public string TagText { get; init; } = "";
        }

        private sealed class ChatPhotoNavigationEntry
        {
            public string GroupId { get; init; } = "";
            public int PhotoCount { get; init; }
            public Rectangle PreviousBounds { get; init; }
            public Rectangle NextBounds { get; init; }
        }

        private sealed class TextEditSnapshot
        {
            public string Text { get; init; } = "";
            public int CursorIndex { get; init; }
            public int SelectionAnchorIndex { get; init; }
        }

        private enum EditableTextFieldKind
        {
            None,
            Search,
            Chat,
            SocialPost,
            SocialComment
        }

        private bool isDragging = false;
        private int dragOffsetX;
        private int dragOffsetY;

        private static readonly Dictionary<string, List<string>> pendingMessages = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, CancellationTokenSource> replyTimers = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, DateTime> lastInputActivityUtc = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object replyQueueLock = new();
        private static readonly TimeSpan ReplyInactivityDelay = TimeSpan.FromSeconds(7);
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
        private int currentMessageCursorIndex = 0;
        private int currentMessageSelectionAnchorIndex = 0;
        private readonly List<TextEditSnapshot> currentMessageUndoHistory = new();
        public static List<string> messageHistory = new();
        private float chatScrollOffset = 0f;
        private float chatScrollTarget = 0f;
        public static List<string> notificationHistory = new();
        private float notificationScrollOffset = 0f;
        private float notificationScrollTarget = 0f;
        int maxBubbleWidth = 400;

        private const int ChatImageMaxWidth = 260;
        private const int ChatImageMaxHeight = 240;
        private const float ChatImageScale = 0.5f;
        private const int ChatPhotoPickerMaxCount = 5;
        private const int ChatAttachmentButtonWidth = 52;
        private const string PlayerPhotoPrefix = "PlayerPhoto:";
        private const string PlayerPhotoTagPrefix = "PlayerPhotoTag:";
        private const string NpcPhotoPrefix = "NpcPhoto:";
        private const string NpcPhotoTagPrefix = "NpcPhotoTag:";
        private const string NpcPhotoCommandPrefix = "[NPC_SEND_PHOTO";

        private const int ChatViewportYOffset = 125;
        private const int ChatViewportHeight = 715;
        private const float ChatScrollPixelsPerWheelNotch = 48f;
        private const float ChatScrollLerpSpeed = 16f;
        private const int ScrollDrawOverscan = 72;
        private const int NotificationViewportYOffset = 126;
        private const int NotificationViewportHeight = 800;
        private const int TextWrapCacheMaxEntries = 4096;

        private readonly Dictionary<string, List<string>> textWrapCache = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Texture2D> chatImageCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> chatFailedImagePaths = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<ChatPhotoHoverEntry> chatPhotoHoverEntries = new();
        private readonly List<ChatPhotoNavigationEntry> chatPhotoNavigationEntries = new();
        private readonly Dictionary<string, int> chatPhotoGroupIndices = new(StringComparer.OrdinalIgnoreCase);

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


        private const string PlayerPhotoFolderName = "player_photo";
        private const string NpcPhotoFolderName = "npc_photo";
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
        private Rectangle chatPhotoButtonBounds = Rectangle.Empty;
        private Rectangle chatPhotoPickerPrevBounds = Rectangle.Empty;
        private Rectangle chatPhotoPickerNextBounds = Rectangle.Empty;
        private Rectangle chatPhotoPickerToggleBounds = Rectangle.Empty;
        private Rectangle chatPhotoPickerCancelBounds = Rectangle.Empty;
        private Rectangle chatPhotoPickerSendBounds = Rectangle.Empty;
        private bool chatPhotoPickerOpen = false;
        private readonly List<string> chatPhotoCandidates = new();
        private readonly List<string> chatSelectedPhotos = new();
        private int chatPhotoCandidateIndex = -1;
        private string currentSettingMenuState = SettingMenuMainState;

        private EditableTextFieldKind activeTextInputRepeatField = EditableTextFieldKind.None;
        private Keys? activeTextInputRepeatKey = null;
        private double textInputRepeatElapsedSeconds = 0d;
        private bool textInputRepeatTriggered = false;
        private double textCursorBlinkElapsedSeconds = 0d;

        private const double TextInputRepeatInitialDelaySeconds = 0.45d;
        private const double TextInputRepeatIntervalSeconds = 0.05d;
        private const int TextUndoHistoryLimit = 128;

        public PhoneMenu() : base(Game1.uiViewport.Width / 2 - 400, Game1.uiViewport.Height / 2 - 500, 600, 1000, true)
        {
            this.upperRightCloseButton = null;

            messageableNpcList = new List<ClickableComponent>();
            Dictionary<string, long> latestTimestamps = MessageManager.GetLatestAddDictionary();
            List<NPC> villagers = Utility.getAllVillagers()
            .Where(n => !n.IsInvisible && n.CanSocialize && CanMessageNpc(n))
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
            else if (currentApp == TextAppState)
            {
                DrawTextApp(b);
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
                List<string> notificationMessages = notificationHistory;

                int messageY = yPositionOnScreen + NotificationViewportYOffset - (int)MathF.Floor(notificationScrollOffset);
                SpriteFont font = Game1.smallFont;
                int visibleTop = notificationClipRect.Top - ScrollDrawOverscan;
                int visibleBottom = notificationClipRect.Bottom + ScrollDrawOverscan;

                // Show newest notifications first and animate their pixel offset.
                for (int i = notificationMessages.Count - 1; i >= 0; i--)
                {
                    string msg = notificationMessages[i];
                    List<string> wrappedLines = SplitNotificationIntoLines(msg, font, 485);

                    int lineHeight = (int)font.MeasureString("A").Y + 4;
                    int bubbleHeight = wrappedLines.Count * lineHeight + 10;
                    int bubbleWidth = 0;

                    foreach (var line in wrappedLines)
                        bubbleWidth = Math.Max(bubbleWidth, (int)font.MeasureString(line).X + 20);

                    Rectangle bubbleRect = new Rectangle(xPositionOnScreen + 50, messageY, bubbleWidth, bubbleHeight);

                    int bubbleTop = bubbleRect.Y;
                    int bubbleBottom = bubbleRect.Bottom + 20;
                    if (bubbleBottom < visibleTop)
                    {
                        messageY += bubbleHeight + 20;
                        continue;
                    }

                    if (bubbleTop > visibleBottom)
                        break;

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

            textCursorBlinkElapsedSeconds += time.ElapsedGameTime.TotalSeconds;
            if (textCursorBlinkElapsedSeconds >= 1_000_000d)
                textCursorBlinkElapsedSeconds %= 1_000_000d;

            UpdateTextInputRepeat(time);

            if (isDragging)
            {
                xPositionOnScreen = Game1.getMouseX() - dragOffsetX;
                yPositionOnScreen = Game1.getMouseY() - dragOffsetY;
                ClampPhoneMenuToViewport();
            }

            ModEntry.currentMenuX = xPositionOnScreen;
            ModEntry.currentMenuY = yPositionOnScreen;

            if (currentApp == TextAppState && selectedNpc != null)
            {
                UpdateTextChatScroll(time);
            }
            else if (currentApp == SocialAppState)
            {
                float lerpAmount = (float)(time.ElapsedGameTime.TotalSeconds * SocialScrollLerpSpeed);
                lerpAmount = Math.Clamp(lerpAmount, 0f, 1f);

                if (socialCreateMenuOpen)
                {
                    // create menu has no scrollable viewport
                }
                else if (socialNotificationMenuOpen)
                {
                    ClampSocialNotificationScroll();
                    socialNotificationScrollOffset = MathHelper.Lerp(socialNotificationScrollOffset, socialNotificationScrollTarget, lerpAmount);

                    if (Math.Abs(socialNotificationScrollOffset - socialNotificationScrollTarget) <= 0.5f)
                        socialNotificationScrollOffset = socialNotificationScrollTarget;
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

            if (HandleTextPhotoPickerModalClick(x, y))
                return;

            if (HandleTextSuggestionClick(x, y))
                return;

            if (HandleTextFirstMessageClick(x, y))
                return;

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
                if (currentApp == TextAppState)
                {
                    if (HandleTextBackButtonClick())
                        return;
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
                else if (new List<string> { "appCamera", "appPhoto", "appGame", "appNotification", ExternalGroupAppState }.Contains(currentApp))
                {
                    if (currentApp == ExternalGroupAppState)
                        ClearCurrentExternalGroup();


                    currentApp = null;
                    return;
                }
            }

            if (HandleTextRemoveButtonClick(x, y))
            {
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
            if (HandleTextNpcListOrChatClick(x, y))
            {
                return;
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
            if (HandleTextScrollWheel(direction))
                return;

            if (currentApp == SocialAppState)
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
            if (HandleTextKeyPress(key))
                return;

            if (key == Keys.Escape)
            {
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

                ResetEditableTextFieldState(EditableTextFieldKind.Search);
                exitThisMenu();
                return;
            }
            else if (currentApp == SocialAppState)
            {
                if (HandleSocialTyping(key))
                    return;
            }
        }

        private EditableTextFieldKind GetActiveEditableTextField()
        {
            if (currentApp == TextAppState && chatPhotoPickerOpen)
                return EditableTextFieldKind.None;

            if (currentApp == TextAppState)
                return selectedNpc == null ? EditableTextFieldKind.Search : EditableTextFieldKind.Chat;

            if (currentApp == SocialAppState)
            {
                if (socialCreateMenuOpen)
                    return EditableTextFieldKind.SocialPost;

                if (!string.IsNullOrWhiteSpace(selectedSocialPostId))
                    return EditableTextFieldKind.SocialComment;
            }

            return EditableTextFieldKind.None;
        }

        private void BeginTextInputRepeat(EditableTextFieldKind field, Keys key)
        {
            activeTextInputRepeatField = field;
            activeTextInputRepeatKey = key;
            textInputRepeatElapsedSeconds = 0d;
            textInputRepeatTriggered = false;
        }

        private void ResetTextInputRepeatState()
        {
            activeTextInputRepeatField = EditableTextFieldKind.None;
            activeTextInputRepeatKey = null;
            textInputRepeatElapsedSeconds = 0d;
            textInputRepeatTriggered = false;
        }

        private static bool IsRepeatableTextInputKey(Keys key)
        {
            return key is Keys.Back or Keys.Delete or Keys.Left or Keys.Right or Keys.Home or Keys.End;
        }

        private void UpdateTextInputRepeat(GameTime time)
        {
            if (!activeTextInputRepeatKey.HasValue || activeTextInputRepeatField == EditableTextFieldKind.None)
                return;

            EditableTextFieldKind currentField = GetActiveEditableTextField();
            if (currentField != activeTextInputRepeatField)
            {
                ResetTextInputRepeatState();
                return;
            }

            Keys repeatKey = activeTextInputRepeatKey.Value;
            KeyboardState keyboardState = Keyboard.GetState();
            if (!keyboardState.IsKeyDown(repeatKey))
            {
                ResetTextInputRepeatState();
                return;
            }

            textInputRepeatElapsedSeconds += time.ElapsedGameTime.TotalSeconds;

            if (!textInputRepeatTriggered)
            {
                if (textInputRepeatElapsedSeconds < TextInputRepeatInitialDelaySeconds)
                    return;

                textInputRepeatElapsedSeconds -= TextInputRepeatInitialDelaySeconds;
                textInputRepeatTriggered = true;

                if (!ApplyRepeatableTextInputKey(currentField, repeatKey))
                {
                    ResetTextInputRepeatState();
                    return;
                }
            }

            while (textInputRepeatElapsedSeconds >= TextInputRepeatIntervalSeconds)
            {
                textInputRepeatElapsedSeconds -= TextInputRepeatIntervalSeconds;

                if (!ApplyRepeatableTextInputKey(currentField, repeatKey))
                {
                    ResetTextInputRepeatState();
                    return;
                }
            }
        }

        private List<TextEditSnapshot> GetTextUndoHistory(EditableTextFieldKind field)
        {
            return field switch
            {
                EditableTextFieldKind.Search or EditableTextFieldKind.Chat => currentMessageUndoHistory,
                EditableTextFieldKind.SocialPost => socialPostDraftUndoHistory,
                EditableTextFieldKind.SocialComment => socialCommentDraftUndoHistory,
                _ => currentMessageUndoHistory
            };
        }

        private void PushTextUndoSnapshot(EditableTextFieldKind field, string text, int cursorIndex, int selectionAnchorIndex)
        {
            List<TextEditSnapshot> history = GetTextUndoHistory(field);
            history.Add(new TextEditSnapshot
            {
                Text = text ?? "",
                CursorIndex = cursorIndex,
                SelectionAnchorIndex = selectionAnchorIndex
            });

            if (history.Count > TextUndoHistoryLimit)
                history.RemoveAt(0);
        }

        private bool TryUndoTextInput(EditableTextFieldKind field)
        {
            List<TextEditSnapshot> history = GetTextUndoHistory(field);
            if (history.Count == 0)
                return true;

            TextEditSnapshot snapshot = history[^1];
            history.RemoveAt(history.Count - 1);
            SetEditableTextFieldState(field, snapshot.Text, snapshot.CursorIndex, snapshot.SelectionAnchorIndex, clearUndoHistory: false);
            return true;
        }

        private void ClearTextUndoHistory(EditableTextFieldKind field)
        {
            GetTextUndoHistory(field).Clear();
        }

        private void SetEditableTextFieldState(
            EditableTextFieldKind field,
            string text,
            int cursorIndex,
            int selectionAnchorIndex,
            bool clearUndoHistory = true)
        {
            string safeText = text ?? "";
            int safeCursorIndex = Math.Clamp(cursorIndex, 0, safeText.Length);
            int safeSelectionAnchorIndex = Math.Clamp(selectionAnchorIndex, 0, safeText.Length);

            switch (field)
            {
                case EditableTextFieldKind.Search:
                case EditableTextFieldKind.Chat:
                    currentMessage = safeText;
                    currentMessageCursorIndex = safeCursorIndex;
                    currentMessageSelectionAnchorIndex = safeSelectionAnchorIndex;
                    break;

                case EditableTextFieldKind.SocialPost:
                    socialPostDraft = safeText;
                    socialPostDraftCursorIndex = safeCursorIndex;
                    socialPostDraftSelectionAnchorIndex = safeSelectionAnchorIndex;
                    break;

                case EditableTextFieldKind.SocialComment:
                    socialCommentDraft = safeText;
                    socialCommentDraftCursorIndex = safeCursorIndex;
                    socialCommentDraftSelectionAnchorIndex = safeSelectionAnchorIndex;
                    break;
            }

            if (clearUndoHistory)
                ClearTextUndoHistory(field);
        }

        private void ResetEditableTextFieldState(EditableTextFieldKind field, bool clearUndoHistory = true)
        {
            SetEditableTextFieldState(field, "", 0, 0, clearUndoHistory);
        }

        private static (int Start, int End) GetSelectionRange(int cursorIndex, int selectionAnchorIndex, int textLength)
        {
            int safeCursorIndex = Math.Clamp(cursorIndex, 0, textLength);
            int safeSelectionAnchorIndex = Math.Clamp(selectionAnchorIndex, 0, textLength);
            return safeCursorIndex < safeSelectionAnchorIndex
                ? (safeCursorIndex, safeSelectionAnchorIndex)
                : (safeSelectionAnchorIndex, safeCursorIndex);
        }

        private static bool HasSelection(int cursorIndex, int selectionAnchorIndex, int textLength)
        {
            (int start, int end) = GetSelectionRange(cursorIndex, selectionAnchorIndex, textLength);
            return start != end;
        }

        private static int MeasureTextSubstringWidth(SpriteFont font, string text, int startIndex, int length)
        {
            if (length <= 0)
                return 0;

            string safeText = text ?? "";
            int safeStartIndex = Math.Clamp(startIndex, 0, safeText.Length);
            int safeLength = Math.Clamp(length, 0, safeText.Length - safeStartIndex);
            if (safeLength <= 0)
                return 0;

            return (int)Math.Round(font.MeasureString(safeText.Substring(safeStartIndex, safeLength)).X);
        }

        private static int MeasureTextPrefixWidth(SpriteFont font, string text, int length)
        {
            return MeasureTextSubstringWidth(font, text, 0, length);
        }

        private void ApplyEditableTextInsertion(
            EditableTextFieldKind field,
            ref string text,
            ref int cursorIndex,
            ref int selectionAnchorIndex,
            string insertionText)
        {
            string safeText = text ?? "";
            string safeInsertionText = insertionText ?? "";
            int safeCursorIndex = Math.Clamp(cursorIndex, 0, safeText.Length);
            int safeSelectionAnchorIndex = Math.Clamp(selectionAnchorIndex, 0, safeText.Length);
            (int selectionStart, int selectionEnd) = GetSelectionRange(safeCursorIndex, safeSelectionAnchorIndex, safeText.Length);

            PushTextUndoSnapshot(field, safeText, safeCursorIndex, safeSelectionAnchorIndex);

            if (selectionStart != selectionEnd)
                safeText = safeText.Remove(selectionStart, selectionEnd - selectionStart);

            int insertIndex = selectionStart;
            safeText = safeText.Insert(insertIndex, safeInsertionText);

            text = safeText;
            cursorIndex = insertIndex + safeInsertionText.Length;
            selectionAnchorIndex = cursorIndex;
        }

        private bool ApplyEditableTextDelete(
            EditableTextFieldKind field,
            ref string text,
            ref int cursorIndex,
            ref int selectionAnchorIndex,
            bool deleteForward)
        {
            string safeText = text ?? "";
            int safeCursorIndex = Math.Clamp(cursorIndex, 0, safeText.Length);
            int safeSelectionAnchorIndex = Math.Clamp(selectionAnchorIndex, 0, safeText.Length);
            (int selectionStart, int selectionEnd) = GetSelectionRange(safeCursorIndex, safeSelectionAnchorIndex, safeText.Length);

            if (selectionStart != selectionEnd)
            {
                PushTextUndoSnapshot(field, safeText, safeCursorIndex, safeSelectionAnchorIndex);
                safeText = safeText.Remove(selectionStart, selectionEnd - selectionStart);
                text = safeText;
                cursorIndex = selectionStart;
                selectionAnchorIndex = selectionStart;
                return true;
            }

            if (!deleteForward)
            {
                if (safeCursorIndex <= 0)
                    return false;

                PushTextUndoSnapshot(field, safeText, safeCursorIndex, safeSelectionAnchorIndex);
                safeText = safeText.Remove(safeCursorIndex - 1, 1);
                text = safeText;
                cursorIndex = safeCursorIndex - 1;
                selectionAnchorIndex = cursorIndex;
                return true;
            }

            if (safeCursorIndex >= safeText.Length)
                return false;

            PushTextUndoSnapshot(field, safeText, safeCursorIndex, safeSelectionAnchorIndex);
            safeText = safeText.Remove(safeCursorIndex, 1);
            text = safeText;
            cursorIndex = safeCursorIndex;
            selectionAnchorIndex = cursorIndex;
            return true;
        }

        private static bool TryGetEditableTextSelection(string text, int cursorIndex, int selectionAnchorIndex, out int selectionStart, out int selectionEnd)
        {
            string safeText = text ?? "";
            (selectionStart, selectionEnd) = GetSelectionRange(cursorIndex, selectionAnchorIndex, safeText.Length);
            return selectionStart != selectionEnd;
        }

        private static int GetVisibleWindowStart(string text, SpriteFont font, int maxWidth, int cursorIndex)
        {
            string safeText = text ?? "";
            int safeCursorIndex = Math.Clamp(cursorIndex, 0, safeText.Length);

            if (safeText.Length == 0 || font.MeasureString(safeText).X <= maxWidth)
                return 0;

            int visibleStart = safeCursorIndex;
            while (visibleStart > 0)
            {
                string candidate = safeText.Substring(visibleStart - 1, safeCursorIndex - (visibleStart - 1));
                if (font.MeasureString(candidate).X > maxWidth)
                    break;

                visibleStart--;
            }

            return visibleStart;
        }

        private static int GetVisibleWindowEnd(string text, SpriteFont font, int maxWidth, int visibleStart, int cursorIndex)
        {
            string safeText = text ?? "";
            int safeCursorIndex = Math.Clamp(cursorIndex, 0, safeText.Length);

            if (safeText.Length == 0 || font.MeasureString(safeText).X <= maxWidth)
                return safeText.Length;

            int visibleEnd = safeCursorIndex;
            while (visibleEnd < safeText.Length)
            {
                string candidate = safeText.Substring(visibleStart, visibleEnd - visibleStart + 1);
                if (font.MeasureString(candidate).X > maxWidth)
                    break;

                visibleEnd++;
            }

            return visibleEnd;
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
            return GetWrappedLinesCached(text, font, maxWidth);
        }

        private List<string> SplitNotificationIntoLines(string text, SpriteFont font, int maxWidth)
        {
            return GetWrappedLinesCached(text, font, maxWidth);
        }

        private List<string> GetWrappedLinesCached(string text, SpriteFont font, int maxWidth)
        {
            string safeText = text ?? "";
            string cacheKey = $"{maxWidth}|{safeText}";

            if (textWrapCache.TryGetValue(cacheKey, out List<string>? cachedLines))
                return new List<string>(cachedLines);

            List<string> wrappedLines = WrapTextIntoLinesCore(safeText, font, maxWidth);

            if (textWrapCache.Count >= TextWrapCacheMaxEntries)
                textWrapCache.Clear();

            textWrapCache[cacheKey] = wrappedLines;
            return new List<string>(wrappedLines);
        }

        private static List<string> WrapTextIntoLinesCore(string text, SpriteFont font, int maxWidth)
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

                        // If a single word is longer than the max width, split it character-wise.
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
                                    partial += c;
                                }
                            }

                            if (!string.IsNullOrEmpty(partial))
                                lines.Add(partial);
                            line = "";
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
            CloseChatPhotoPicker(clearSelection: true);
            ResetEditableTextFieldState(EditableTextFieldKind.Chat);
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
            textWrapCache.Clear();

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

            SpriteFont font = Game1.smallFont;
            int lineHeight = (int)font.MeasureString("A").Y + 4;
            int totalHeight = 0;

            foreach (ChatMessageEntry entry in BuildChatEntries(msgList))
            {
                int bubbleHeight = CalculateChatEntryHeight(entry, font, lineHeight);
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

        public void UpdateNpcList(bool bypassFilter = false)
        {
            messageableNpcList.Clear();
            Dictionary<string, long> latestTimestamps = MessageManager.GetLatestAddDictionary();
            string filter = currentMessage?.ToLower() ?? "";

            List<NPC> villagers = Utility.getAllVillagers()
            .Where(n => !n.IsInvisible && n.CanSocialize && CanMessageNpc(n) 
                && (bypassFilter || n.Name.ToLower().Contains(filter))
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

        private static bool CanMessageNpc(NPC npc)
        {
            if (npc == null)
                return false;

            string requirement = ModEntry.Config?.NpcMessageRequirement ?? ModConfig.NpcRequirementFriend;

            if (string.Equals(requirement, ModConfig.NpcRequirementNoRequirement, StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(requirement, ModConfig.NpcRequirementMeet, StringComparison.OrdinalIgnoreCase))
                return Game1.player.friendshipData.ContainsKey(npc.Name);

            return Game1.player.getFriendshipHeartLevelForNPC(npc.Name) >= 1;
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

            SpriteFont font = Game1.smallFont;
            int lineHeight = (int)font.MeasureString("A").Y + 4;
            int totalHeight = 0;

            foreach (string msg in msgList)
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

            string userCaptureFolderPath = GetCaptureFolderPath(PlayerPhotoFolderName);

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

        private static string GetCurrentSaveFolderName()
        {
            return string.IsNullOrWhiteSpace(Constants.SaveFolderName)
                ? "default"
                : Constants.SaveFolderName;
        }

        private static string GetCaptureFolderPath(string photoFolderName)
        {
            return Path.Combine(
                ModEntry.Instance.Helper.DirectoryPath,
                "userdata",
                GetCurrentSaveFolderName(),
                photoFolderName);
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
            CloseChatPhotoPicker(clearSelection: true);
            currentMessage = null;

            if (currentApp == "appText" && selectedNpc != null)
            {
                MessageManager.SetUnreadCount(selectedNpc);
                selectedNpc = null;
                currentSuggestion = new("", "");
                ResetEditableTextFieldState(EditableTextFieldKind.Search);
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

            ResetEditableTextFieldState(EditableTextFieldKind.Search);
            exitThisMenu();
        }

    }
}
