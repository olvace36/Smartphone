using System.Collections.Specialized;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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

        private sealed class TextEditSnapshot
        {
            public string Text { get; init; } = "";
            public int CursorIndex { get; init; }
            public int SelectionAnchorIndex { get; init; }
        }

        private sealed class PhoneTextInputSubscriber : IKeyboardSubscriber
        {
            private readonly PhoneMenu owner;

            public bool Selected { get; set; }

            public PhoneTextInputSubscriber(PhoneMenu owner)
            {
                this.owner = owner;
            }

            public void RecieveTextInput(char inputChar)
            {
                if (!Selected)
                    return;

                owner.TryApplyComposedTextInput(inputChar.ToString());
            }

            public void RecieveTextInput(string text)
            {
                if (!Selected)
                    return;

                owner.TryApplyComposedTextInput(text);
            }

            public void RecieveCommandInput(char command)
            {
                // Command keys are handled through receiveKeyPress to avoid duplicate edits.
            }

            public void RecieveSpecialInput(Keys key)
            {
            }
        }

        private enum EditableTextFieldKind
        {
            None,
            Search,
            Chat,
            SocialPost,
            SocialComment,
            ProfileAge,
            ProfileBirthday,
            ProfileDescription,
            PhotoAlbumName
        }

        internal enum RootLandingState
        {
            Initializing,
            LockScreen,
            Home
        }

        private bool isDragging = false;
        private int dragOffsetX;
        private int dragOffsetY;
        internal RootLandingState rootLandingState = RootLandingState.Home;
        private bool lockScreenUnlockAnimating = false;
        private double lockScreenUnlockElapsedSeconds = 0d;
        private Rectangle lockScreenTapBounds = Rectangle.Empty;
        private int lockScreenInitializationProgressPercent = 0;
        private double lockScreenInitializationElapsedSeconds = 0d;
        private double lockScreenInitializationNextTickSeconds = 0d;



        private Texture2D texturePhoneBackground = Textures.PhoneBackground;
        private Texture2D texturePhoneCapture = Textures.PhoneEmpty;
        private Texture2D texturePortraitBackground = Textures.Background;

        private Texture2D textureAppCamera = Textures.AppCamera;
        private Texture2D textureAppText = Textures.AppText;
        private Texture2D textureAppPhoto = Textures.AppPhoto;
        private Texture2D textureAppSocial = Textures.AppSocial;
        private Texture2D textureAppSetting = Textures.AppSetting;
        private Texture2D textureAppNotification = Textures.AppNotification;
        private Texture2D textureAppAppStore = Textures.AppAppStore;
        private Texture2D textureAppCalendar = Textures.AppCalendar;

        private int scrollOffset = 0;
        private int maxVisibleNPCs = 7;
        int visibleSlots = 7;
        int spacing = 95;

        public static string selectedNpc = null;
        private Task<string> pendingKeyboardTask = null;
        private EditableTextFieldKind pendingKeyboardField = EditableTextFieldKind.None;
        private bool isScrolling = false;
        private int lastScrollMouseY = 0;
        private int touchScrollStartY = 0;
        private bool hasTouchScrolled = false;
        private string currentMessage = "";
        private int currentMessageCursorIndex = 0;
        private int currentMessageSelectionAnchorIndex = 0;
        private readonly List<TextEditSnapshot> currentMessageUndoHistory = new();
        private float chatScrollOffset = 0f;
        public static List<string> notificationHistory = new();
        private float notificationScrollOffset = 0f;
        private float notificationScrollTarget = 0f;
        int maxBubbleWidth = 400;
        private const float ChatScrollPixelsPerWheelNotch = 48f;
        private const float ChatScrollLerpSpeed = 16f;
        private const int ScrollDrawOverscanBase = 72;
        private const int NotificationViewportYOffsetBase = 126;
        private const int NotificationViewportHeightBase = 800;
        private const int TextWrapCacheMaxEntries = 4096;
        private const int ScrollWheelNotchDelta = 120;
        private const float ControllerScrollNotchBoost = 1.5f;
        private const float PhoneGlobalTextScale = 0.85f;
        private const int NotificationBubbleTextWrapWidthBase = 485;
        private const int NotificationBubbleXOffsetBase = 50;
        private const int NotificationBubbleTextLeftPaddingBase = 10;
        private const int NotificationBubbleTextTopPaddingBase = 15;
        private const int NotificationBubbleHorizontalPaddingBase = 20;
        private const int NotificationBubbleInnerPaddingBase = 10;
        private const int NotificationBubbleSpacingBase = 20;

        private int ScrollDrawOverscan => Math.Max(1, ScaleUiValue(ScrollDrawOverscanBase));
        private int NotificationViewportYOffset => ScaleUiValue(NotificationViewportYOffsetBase);
        private int NotificationViewportHeight => Math.Max(1, ScaleUiValue(NotificationViewportHeightBase));
        private int NotificationBubbleTextWrapWidth => Math.Max(1, ScaleUiValue(NotificationBubbleTextWrapWidthBase));
        private int NotificationBubbleXOffset => ScaleUiValue(NotificationBubbleXOffsetBase);
        private int NotificationBubbleTextLeftPadding => Math.Max(1, ScaleUiValue(NotificationBubbleTextLeftPaddingBase));
        private int NotificationBubbleTextTopPadding => Math.Max(1, ScaleUiValue(NotificationBubbleTextTopPaddingBase));
        private int NotificationBubbleHorizontalPadding => Math.Max(1, ScaleUiValue(NotificationBubbleHorizontalPaddingBase));
        private int NotificationBubbleInnerPadding => Math.Max(1, ScaleUiValue(NotificationBubbleInnerPaddingBase));
        private int NotificationBubbleSpacing => Math.Max(1, ScaleUiValue(NotificationBubbleSpacingBase));

        private readonly Dictionary<string, List<string>> textWrapCache = new(StringComparer.Ordinal);

        private List<string> phoneSoundList = new();
        private Dictionary<string, ClickableTextureComponent> phoneSoundButton = new();
        private List<string> phoneTextColorList = new();
        private Dictionary<string, ClickableTextureComponent> phoneTextColorButton = new();
        private List<string> phoneThemeList = new();
        private Dictionary<string, ClickableTextureComponent> phoneThemeButton = new();
        private readonly Dictionary<string, Rectangle> phoneThemeHoverBounds = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> phoneThemeReadmeCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Color> phoneTextColorMap = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Rectangle> settingOptionBounds = new();


        // buttons
        private ClickableTextureComponent okButton;
        private ClickableTextureComponent backButton;
        private ClickableTextureComponent lockButton;
        private ClickableTextureComponent homeButton;
        private ClickableTextureComponent removeButton;
        private ClickableTextureComponent captureButton;
        private ClickableTextureComponent photoNextButton;
        private ClickableTextureComponent photoPreviousButton;
        private Rectangle cameraZoomOutButtonBounds = Rectangle.Empty;
        private Rectangle cameraZoomInButtonBounds = Rectangle.Empty;
        private Rectangle cameraFlashButtonBounds = Rectangle.Empty;
        private Rectangle cameraRotateButtonBounds = Rectangle.Empty;
        private Rectangle cameraSquareButtonBounds = Rectangle.Empty;
        private double cameraCaptureFlashRemainingSeconds = 0d;
        private int cameraZoomHoldDirection = 0;
        private double cameraZoomHoldElapsedSeconds = 0d;
        private bool cameraZoomHoldTriggered = false;

        // apps
        public static string? currentApp = null;
        private string? appAtClickStart = null;

        private const string BuiltinAppNotificationId = "builtin:notification";
        private const string BuiltinAppCameraId = "builtin:camera";
        private const string BuiltinAppStoreId = "builtin:appstore";
        private const string BuiltinAppPhotoId = "builtin:photo";
        private const string BuiltinAppSettingId = "builtin:setting";
        private const string BuiltinAppCalendarId = "builtin:calendar";
        private const string ExternalGroupAppState = "appExternalGroup";
        private const string SettingMenuMainState = "settingMain";
        private const string SettingMenuSoundState = "settingSound";
        private const string SettingMenuTextColorState = "settingTextColor";
        private const string SettingMenuThemeState = "settingTheme";
        private const string SettingMenuOptionTextColor = "textColor";
        private const string SettingMenuOptionSound = "sound";
        private const string SettingMenuOptionTheme = "theme";
        private const string SettingMenuOptionPhoneSetting = "phoneSetting";
        private const string ThemeReadmeFileName = "readme.txt";
        private const int SettingsTitleXOffsetBase = 105;
        private const int SettingsTitleYOffsetBase = 67;
        private const int SettingsMainOptionsStartYBase = 260;
        private const int SettingsListStartYBase = 150;
        private const int SettingsOptionRowXOffsetBase = 90;
        private const int SettingsOptionRowWidthBase = 430;
        private const int SettingsOptionRowHeightBase = 66;
        private const int SettingsOptionTextXPaddingBase = 20;
        private const int SettingsOptionTextYOffsetBase = 15;
        private const int SettingsOptionArrowSizeBase = 32;
        private const int SettingsOptionArrowRightPaddingBase = 42;
        private const int SettingsOptionArrowYOffsetBase = 17;
        private const int SettingsListNameXOffsetBase = 100;
        private const int SettingsListNameYOffsetBase = 15;
        private const int SettingsColorPreviewXOffsetBase = 220;
        private const int SettingsColorPreviewYOffsetBase = 24;
        private const int SettingsColorPreviewOuterWidthBase = 40;
        private const int SettingsColorPreviewOuterHeightBase = 24;
        private const int SettingsColorPreviewInnerXOffsetBase = 2;
        private const int SettingsColorPreviewInnerYOffsetBase = 2;
        private const int SettingsColorPreviewInnerWidthBase = 36;
        private const int SettingsColorPreviewInnerHeightBase = 20;
        private const int SettingsCheckboxXOffsetBase = 370;
        private const int SettingsCheckboxYOffsetBase = 20;
        private const int SettingsCheckboxSizeBase = 35;
        private const int SettingsThemeHoverXOffsetBase = -8;
        private const int SettingsThemeHoverYOffsetBase = 8;
        private const int SettingsThemeHoverWidthBase = 420;
        private const int SettingsThemeHoverHeightBase = 58;

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
        private const double CameraCaptureFlashDurationSeconds = 0.5d;
        private const float CameraCaptureFlashMaxOpacity = 0.9f;
        private const int CameraOverlayMarginBase = 16;
        private const int CameraToolAreaHeightBase = 138;
        private const int CameraControlButtonHeightBase = 44;
        private const int CameraModeButtonWidthBase = 53;
        private const int CameraZoomButtonWidthBase = 44;
        private const int CameraControlButtonSpacingBase = 8;
        private const int CameraCaptureGroupGapBase = 14;
        private const int CameraCaptureButtonWidthBase = 118;
        private const int CameraCaptureButtonHeightBase = 64;
        private const int CameraCaptureButtonMinWidthBase = 80;
        private const int CameraCaptureButtonMinHeightBase = 44;
        private const double CameraZoomHoldInitialDelaySeconds = 0.24d;
        private const double CameraZoomHoldIntervalSeconds = 0.065d;
        private const string CameraFlashButtonLabel = "FLASH";
        private const string CameraLandscapeButtonLabel = "LAND";
        private const string CameraSquareButtonLabel = "SQR";
        private static readonly Rectangle CameraZoomMinusIconSource = new Rectangle(177, 345, 7, 8);
        private static readonly Rectangle CameraZoomPlusIconSource = new Rectangle(184, 345, 7, 8);
        private static readonly Rectangle CameraFlashIconSource = new Rectangle(193, 373, 9, 9);
        private static readonly Rectangle CameraSquareIconSource = new Rectangle(155, 384, 9, 9);
        private static readonly Rectangle CameraLandscapeIconSource = new Rectangle(67, 243, 9, 10);

        private int CameraOverlayMargin => Math.Max(1, ScaleUiValue(CameraOverlayMarginBase));
        private int CameraToolAreaHeight => Math.Max(1, ScaleUiValue(CameraToolAreaHeightBase));
        private int CameraControlButtonHeight => Math.Max(1, ScaleUiValue(CameraControlButtonHeightBase));
        private int CameraModeButtonWidth => Math.Max(1, ScaleUiValue(CameraModeButtonWidthBase));
        private int CameraZoomButtonWidth => Math.Max(1, ScaleUiValue(CameraZoomButtonWidthBase));
        private int CameraControlButtonSpacing => Math.Max(1, ScaleUiValue(CameraControlButtonSpacingBase));
        private int CameraCaptureGroupGap => Math.Max(1, ScaleUiValue(CameraCaptureGroupGapBase));
        private int CameraCaptureButtonWidth => Math.Max(1, ScaleUiValue(CameraCaptureButtonWidthBase));
        private int CameraCaptureButtonHeight => Math.Max(1, ScaleUiValue(CameraCaptureButtonHeightBase));
        private int CameraCaptureButtonMinWidth => Math.Max(1, ScaleUiValue(CameraCaptureButtonMinWidthBase));
        private int CameraCaptureButtonMinHeight => Math.Max(1, ScaleUiValue(CameraCaptureButtonMinHeightBase));

        private readonly Dictionary<string, Rectangle> homeAppClickBounds = new();
        private Rectangle homeAppPrevPageBounds = Rectangle.Empty;
        private Rectangle homeAppNextPageBounds = Rectangle.Empty;
        private int homeAppPage = 0;
        private double appLabelMarqueeElapsedSeconds = 0d;
        private readonly RasterizerState appLabelScissorRasterizer = new() { ScissorTestEnable = true };

        private readonly Dictionary<string, Rectangle> externalGroupItemClickBounds = new();
        private string currentExternalGroupId = "";
        private string currentExternalGroupName = "";
        private readonly Dictionary<int, Texture2D> lockScreenWeatherIconSoftCache = new();

        private List<string> capturedImages;
        private Texture2D phoneBackgroundImage = null;


        // this textBox remains hidden for compatibility with existing menu behavior.
        private TextBox textBox;
        private readonly PhoneTextInputSubscriber textInputSubscriber;

        private string currentSettingMenuState = SettingMenuMainState;

        private int textProfileAgeCursorIndex = 0;
        private int textProfileAgeSelectionAnchorIndex = 0;
        private int textProfileBirthdayCursorIndex = 0;
        private int textProfileBirthdaySelectionAnchorIndex = 0;
        private int textProfileDescriptionCursorIndex = 0;
        private int textProfileDescriptionSelectionAnchorIndex = 0;

        private readonly List<TextEditSnapshot> textProfileAgeUndoHistory = new();
        private readonly List<TextEditSnapshot> textProfileBirthdayUndoHistory = new();
        private readonly List<TextEditSnapshot> textProfileDescriptionUndoHistory = new();

        private EditableTextFieldKind activeTextInputRepeatField = EditableTextFieldKind.None;
        private Keys? activeTextInputRepeatKey = null;
        private double textInputRepeatElapsedSeconds = 0d;
        private bool textInputRepeatTriggered = false;
        private double textCursorBlinkElapsedSeconds = 0d;

        private const double TextInputRepeatInitialDelaySeconds = 0.45d;
        private const double TextInputRepeatIntervalSeconds = 0.05d;
        private const int TextUndoHistoryLimit = 128;
        private const int ProfileDescriptionMaxLength = 160;
        private const double LockScreenInitializationDurationSeconds = 7d;
        private const double LockScreenInitializationProgressIntervalMinSeconds = 0.06d;
        private const double LockScreenInitializationProgressIntervalMaxSeconds = 0.45d;
        private const float LockScreenInitializationTitleTextScale = 1.08f;
        private const float LockScreenInitializationProgressTextScale = 1.22f;
        private const double LockScreenUnlockDurationSeconds = 0.24d;
        private const float LockScreenTimeTextScale = 1.45f;
        private const float LockScreenDateTextScale = 1.2f;
        private const float LockScreenHintTextScale = 1.05f;
        private const float LockScreenWeatherLabelTextScale = 0.95f * 0.75f;
        private const float LockScreenWeatherPanelTopSpacing = 24f;
        private const float LockScreenWeatherColumnHalfSpacing = 88f;
        private const float LockScreenWeatherIconScale = 4f;
        private const float LockScreenWeatherIconSoftCornerRadius = 6f;
        private const float LockScreenWeatherIconSoftCornerFeather = 2f;
        private const int LockScreenWeatherIconWidth = 12;
        private const int LockScreenWeatherIconHeight = 8;
        private const int LockScreenWeatherIconStartX = 317;
        private const int LockScreenWeatherIconStartY = 421;
        private static readonly Rectangle LockScreenGreenRainWeatherIconSource = new Rectangle(243, 293, 12, 8);

        private bool forcedFreeControllerCursor = false;
        private readonly float phoneUiScale;

        internal float PhoneUiScale => phoneUiScale;

        internal bool UsesPhoneUiScale(float scale)
        {
            return Math.Abs(phoneUiScale - scale) < 0.001f;
        }

        private int ScaleUiValue(int baseValue)
        {
            return ModEntry.ScalePhoneUiValue(baseValue, phoneUiScale);
        }

        private float ScaleUiValue(float baseValue)
        {
            return baseValue * phoneUiScale;
        }

        private float GetPhoneTextScale(float localScale = 1f)
        {
            float globalScale = phoneUiScale < 0.999f
                ? PhoneGlobalTextScale
                : 1f;

            return Math.Max(0.01f, localScale * globalScale);
        }

        private int GetPhoneScaledWrapWidth(int maxWidth, float localScale = 1f)
        {
            float safeScale = GetPhoneTextScale(localScale);
            return Math.Max(1, (int)Math.Floor(maxWidth / safeScale));
        }

        private int GetPhoneScaledLineHeight(SpriteFont font, float localScale = 1f, int extraPadding = 4)
        {
            int baseLineHeight = (int)font.MeasureString("A").Y + extraPadding;
            return Math.Max(1, (int)Math.Ceiling(baseLineHeight * GetPhoneTextScale(localScale)));
        }

        private Vector2 MeasurePhoneText(SpriteFont font, string text, float localScale = 1f)
        {
            return font.MeasureString(text ?? string.Empty) * GetPhoneTextScale(localScale);
        }

        private void DrawPhoneText(SpriteBatch b, SpriteFont font, string text, Vector2 position, Color color, float localScale = 1f, float layerDepth = 1f)
        {
            b.DrawString(
                font,
                text ?? string.Empty,
                position,
                color,
                0f,
                Vector2.Zero,
                GetPhoneTextScale(localScale),
                SpriteEffects.None,
                layerDepth);
        }

        private int PhoneX(int baseOffset)
        {
            return xPositionOnScreen + ScaleUiValue(baseOffset);
        }

        private int PhoneY(int baseOffset)
        {
            return yPositionOnScreen + ScaleUiValue(baseOffset);
        }

        private Rectangle PhoneRect(int baseX, int baseY, int baseWidth, int baseHeight)
        {
            return new Rectangle(
                PhoneX(baseX),
                PhoneY(baseY),
                Math.Max(1, ScaleUiValue(baseWidth)),
                Math.Max(1, ScaleUiValue(baseHeight)));
        }

        private Rectangle GetPhoneFrameBounds()
        {
            return new Rectangle(xPositionOnScreen, yPositionOnScreen, width, height);
        }

        public PhoneMenu() : base(
            Game1.uiViewport.Width / 2 - ModEntry.GetScaledPhoneDefaultMenuOffsetX(),
            Game1.uiViewport.Height / 2 - ModEntry.GetScaledPhoneDefaultMenuOffsetY(),
            ModEntry.GetScaledPhoneFrameWidth(),
            ModEntry.GetScaledPhoneFrameHeight(),
            true)
        {
            phoneUiScale = ModEntry.GetConfiguredPhoneUiScale();
            this.upperRightCloseButton = null;

            spacing = Math.Max(1, ScaleUiValue(spacing));
            maxBubbleWidth = Math.Max(1, ScaleUiValue(maxBubbleWidth));




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

            textInputSubscriber = new PhoneTextInputSubscriber(this);

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

            AssetHelper.SetCurrentPhoneTheme(ModEntry.currentPhoneTheme);
            ModEntry.currentPhoneTheme = AssetHelper.CurrentPhoneThemeName;
            RefreshPhoneThemeList();
            ReloadThemeTextures();

            ApplyPhoneBackground(ModEntry.currentPhoneBackground);
            EnsureFreeControllerCursor();

        }

        protected override void cleanupBeforeExit()
        {
            SetPhoneTextInputFocus(false);
            DisposeLockScreenWeatherIconSoftCache();
            RestoreControllerCursorSetting();
            base.cleanupBeforeExit();
        }


        public override void draw(SpriteBatch b)
        {
            okButton = new ClickableTextureComponent(
                new Rectangle(
                    this.xPositionOnScreen + ScaleUiValue(490),
                    this.yPositionOnScreen + ScaleUiValue(850),
                    ScaleUiValue(64),
                    ScaleUiValue(64)),
                Game1.mouseCursors,
                new Rectangle(128, 256, 64, 64),
                ScaleUiValue(1f));
            removeButton = new ClickableTextureComponent(
                new Rectangle(
                    this.xPositionOnScreen + ScaleUiValue(335),
                    this.yPositionOnScreen + ScaleUiValue(66),
                    ScaleUiValue(32),
                    ScaleUiValue(26 * 2)),
                Game1.mouseCursors,
                new Rectangle(564, 102, 16, 26),
                ScaleUiValue(1.7f));
            captureButton = new ClickableTextureComponent(
                new Rectangle(
                    this.xPositionOnScreen + ScaleUiValue(130),
                    this.yPositionOnScreen + ScaleUiValue(65),
                    ScaleUiValue(100),
                    ScaleUiValue(40)),
                Game1.mouseCursors2,
                new Rectangle(72, 32, 18, 15),
                ScaleUiValue(3.25f));

            photoNextButton = new ClickableTextureComponent(
                new Rectangle(
                    this.xPositionOnScreen + ScaleUiValue(500),
                    this.yPositionOnScreen + ScaleUiValue(500),
                    ScaleUiValue(64),
                    ScaleUiValue(64)),
                Game1.mouseCursors,
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 33),
                ScaleUiValue(1f));

            photoPreviousButton = new ClickableTextureComponent(
                new Rectangle(
                    this.xPositionOnScreen + ScaleUiValue(35),
                    this.yPositionOnScreen + ScaleUiValue(500),
                    ScaleUiValue(64),
                    ScaleUiValue(64)),
                Game1.mouseCursors,
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 44),
                ScaleUiValue(1f));


            // global
            backButton = new ClickableTextureComponent(
                new Rectangle(
                    this.xPositionOnScreen + ScaleUiValue(132),
                    this.yPositionOnScreen + ScaleUiValue(925),
                    ScaleUiValue(64),
                    ScaleUiValue(64)),
                null,
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 44),
                ScaleUiValue(1f));

            lockButton = new ClickableTextureComponent(
                new Rectangle(
                    this.xPositionOnScreen + ScaleUiValue(405),
                    this.yPositionOnScreen + ScaleUiValue(925),
                    ScaleUiValue(64),
                    ScaleUiValue(64)),
                null,
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 44),
                ScaleUiValue(1f));

            homeButton = new ClickableTextureComponent(
                new Rectangle(
                    this.xPositionOnScreen + ScaleUiValue(265),
                    this.yPositionOnScreen + ScaleUiValue(925),
                    ScaleUiValue(64),
                    ScaleUiValue(64)),
                null,
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 33),
                ScaleUiValue(1.3f));


            cameraZoomOutButtonBounds = Rectangle.Empty;
            cameraZoomInButtonBounds = Rectangle.Empty;
            cameraFlashButtonBounds = Rectangle.Empty;
            cameraRotateButtonBounds = Rectangle.Empty;
            cameraSquareButtonBounds = Rectangle.Empty;
            if (GetActiveEditableTextField() == EditableTextFieldKind.None)
                SetPhoneTextInputFocus(false);
            else
                SetPhoneTextInputFocus(true);


            if (currentApp == null)
            {
                DrawRootPhoneScreen(b);
                lockButton.draw(b, Color.Tan, 1f);
                homeButton.draw(b, Color.Tan, 1f);

            }
            else if (currentApp == ExternalGroupAppState)
            {
                b.Draw(Game1.staminaRect, GetUiViewportBounds(), Color.Black * 0.6f);
                DrawPhoneScreenBackground(b, xOffset: 0, applyBackgroundImage: true);
                DrawPhoneFrame(b);
                backButton.draw(b, Color.Tan, 1f);
                lockButton.draw(b, Color.Tan, 1f);
                homeButton.draw(b, Color.Tan, 1f);

                string title = string.IsNullOrWhiteSpace(currentExternalGroupName)
                    ? "App Group"
                    : currentExternalGroupName;
                DrawPhoneText(
                    b,
                    Game1.dialogueFont,
                    title,
                    new Vector2(xPositionOnScreen + ScaleUiValue(105), yPositionOnScreen + ScaleUiValue(125)),
                    Color.Black);

                DrawExternalGroupItems(b);
            }
            else if (currentApp == "appCamera")
            {
                Rectangle phoneRect = ModEntry.GetPhoneCameraPreviewBounds(xPositionOnScreen, yPositionOnScreen);
                Rectangle captureRect = ModEntry.GetPlayerPhotoCaptureBounds(xPositionOnScreen, yPositionOnScreen);
                Rectangle toolAreaBounds = Rectangle.Empty;
                bool hideCameraOverlayButtons = ModEntry.IsPlayerCaptureCursorHidden();
                if (hideCameraOverlayButtons)
                {
                    captureButton.bounds = Rectangle.Empty;
                    cameraZoomOutButtonBounds = Rectangle.Empty;
                    cameraZoomInButtonBounds = Rectangle.Empty;
                    cameraFlashButtonBounds = Rectangle.Empty;
                    cameraRotateButtonBounds = Rectangle.Empty;
                    cameraSquareButtonBounds = Rectangle.Empty;
                }
                else
                {
                    int minToolAreaHeight = Math.Max(CameraCaptureButtonMinHeight, CameraControlButtonHeight) + (CameraOverlayMargin * 2);
                    int toolAreaHeight = Math.Clamp(CameraToolAreaHeight, minToolAreaHeight, Math.Max(minToolAreaHeight, phoneRect.Height));
                    toolAreaBounds = new Rectangle(phoneRect.X, phoneRect.Bottom - toolAreaHeight, phoneRect.Width, toolAreaHeight);

                    int maxCaptureWidth = Math.Max(CameraCaptureButtonMinWidth, toolAreaBounds.Width - (CameraOverlayMargin * 2));
                    int maxCaptureHeight = Math.Max(CameraCaptureButtonMinHeight, toolAreaBounds.Height - (CameraOverlayMargin * 2));
                    int captureWidth = Math.Clamp(CameraCaptureButtonWidth, CameraCaptureButtonMinWidth, maxCaptureWidth);
                    int captureHeight = Math.Clamp(CameraCaptureButtonHeight, CameraCaptureButtonMinHeight, maxCaptureHeight);

                    captureButton.bounds = new Rectangle(
                        phoneRect.Center.X - (captureWidth / 2),
                        toolAreaBounds.Center.Y - (captureHeight / 2),
                        captureWidth,
                        captureHeight);

                    int controlY = toolAreaBounds.Center.Y - (CameraControlButtonHeight / 2);

                    int leftSquareX = captureButton.bounds.Left - CameraCaptureGroupGap - CameraModeButtonWidth;
                    int leftRotateX = leftSquareX - CameraControlButtonSpacing - CameraModeButtonWidth;
                    int rightZoomOutX = captureButton.bounds.Right + CameraCaptureGroupGap;
                    int rightZoomInX = rightZoomOutX + CameraZoomButtonWidth + CameraControlButtonSpacing;
                    int rightFlashX = rightZoomInX + CameraZoomButtonWidth + CameraControlButtonSpacing;

                    cameraRotateButtonBounds = new Rectangle(leftRotateX, controlY, CameraModeButtonWidth, CameraControlButtonHeight);
                    cameraSquareButtonBounds = new Rectangle(leftSquareX, controlY, CameraModeButtonWidth, CameraControlButtonHeight);
                    cameraZoomOutButtonBounds = new Rectangle(rightZoomOutX, controlY, CameraZoomButtonWidth, CameraControlButtonHeight);
                    cameraZoomInButtonBounds = new Rectangle(rightZoomInX, controlY, CameraZoomButtonWidth, CameraControlButtonHeight);
                    cameraFlashButtonBounds = new Rectangle(rightFlashX, controlY, CameraModeButtonWidth, CameraControlButtonHeight);
                }

                Rectangle uiViewportBounds = GetUiViewportBounds();
                int viewportWidth = uiViewportBounds.Width;
                int viewportHeight = uiViewportBounds.Height;

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

                    DrawPhoneFrame(b);
                    backButton.draw(b, Color.Tan, 1f);
                    lockButton.draw(b, Color.Tan, 1f);
                    homeButton.draw(b, Color.Tan, 1f);

                    b.End();
                    b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
                }
                else
                {
                    DrawPhoneFrame(b);
                    backButton.draw(b, Color.Tan, 1f);
                    lockButton.draw(b, Color.Tan, 1f);
                    homeButton.draw(b, Color.Tan, 1f);
                }

                if (!hideCameraOverlayButtons)
                {
                    if (toolAreaBounds.Width > 0 && toolAreaBounds.Height > 0)
                        b.Draw(Game1.staminaRect, toolAreaBounds, new Color(0, 0, 0, 50));

                    DrawCameraCaptureButton(b, captureButton.bounds);
                    DrawCameraControlButton(b, cameraZoomOutButtonBounds, "-", false);
                    DrawCameraControlButton(b, cameraZoomInButtonBounds, "+", false);
                    DrawCameraControlButton(b, cameraFlashButtonBounds, CameraFlashButtonLabel, ModEntry.cameraFlashMode);
                    DrawCameraControlButton(b, cameraRotateButtonBounds, CameraLandscapeButtonLabel, ModEntry.cameraLandscapeMode);
                    DrawCameraControlButton(b, cameraSquareButtonBounds, CameraSquareButtonLabel, ModEntry.cameraSquareMode);
                }

                DrawCaptureOutline(b, captureRect, new Color(255, 255, 255, 220));
                DrawCameraCaptureFlash(b, phoneRect);
            }
            else if (currentApp == "appPhoto")
            {
                DrawPhotoApp(b);
            }

            else if (currentApp == "appSetting")
            {
                DrawSettingMenu(b);
            }
            else if (currentApp == "appStore")
            {
                b.Draw(Game1.staminaRect, GetUiViewportBounds(), Color.Black * 0.6f);
                DrawPhoneScreenBackground(b, xOffset: 0);
                DrawPhoneFrame(b);
                backButton.draw(b, Color.Tan, 1f);
                lockButton.draw(b, Color.Tan, 1f);
                homeButton.draw(b, Color.Tan, 1f);
                DrawAppStore(b);
            }
            else if (currentApp == "appNotification")
            {
                b.Draw(Game1.staminaRect, GetUiViewportBounds(), Color.Black * 0.6f);
                DrawPhoneScreenBackground(b, xOffset: 0);
                DrawPhoneFrame(b);
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
                backButton.draw(b, Color.Tan, 1f);
                lockButton.draw(b, Color.Tan, 1f);
                homeButton.draw(b, Color.Tan, 1f);

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
                    List<string> wrappedLines = SplitNotificationIntoLines(
                        msg,
                        font,
                        GetPhoneScaledWrapWidth(NotificationBubbleTextWrapWidth));

                    int lineHeight = GetPhoneScaledLineHeight(font);
                    int bubbleHeight = Math.Max(1, wrappedLines.Count) * lineHeight + NotificationBubbleInnerPadding;
                    int bubbleWidth = 0;

                    foreach (var line in wrappedLines)
                        bubbleWidth = Math.Max(bubbleWidth, (int)Math.Ceiling(MeasurePhoneText(font, line).X) + NotificationBubbleHorizontalPadding);

                    Rectangle bubbleRect = new Rectangle(xPositionOnScreen + NotificationBubbleXOffset, messageY, bubbleWidth, bubbleHeight);

                    int bubbleTop = bubbleRect.Y;
                    int bubbleBottom = bubbleRect.Bottom + NotificationBubbleSpacing;
                    if (bubbleBottom < visibleTop)
                    {
                        messageY += bubbleHeight + NotificationBubbleSpacing;
                        continue;
                    }

                    if (bubbleTop > visibleBottom)
                        break;

                    IClickableMenu.drawTextureBox(
                        b,
                        Game1.menuTexture,
                        new Rectangle(0, 256, 60, 60), // source rect for 9-slice
                        bubbleRect.X - ScaleUiValue(5),
                        bubbleRect.Y,
                        bubbleRect.Width + ScaleUiValue(12),
                        bubbleRect.Height + ScaleUiValue(10),
                        new Color(0, 0, 0, 100),
                        1f,
                        false
                    );

                    int textY = bubbleRect.Y + NotificationBubbleTextTopPadding;
                    foreach (var line in wrappedLines)
                    {
                        Vector2 linePos = new Vector2(bubbleRect.X + NotificationBubbleTextLeftPadding, textY);
                        DrawPhoneText(b, font, line, linePos, Color.White);
                        textY += lineHeight;
                    }

                    messageY += bubbleHeight + NotificationBubbleSpacing;
                }

                // Reset clipping
                b.End();
                b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

            }


            // base
            base.draw(b);

            bool suppressCursorForPendingCapture = currentApp == "appCamera" && ModEntry.IsPlayerCaptureCursorHidden();
            if (!suppressCursorForPendingCapture)
                drawMouse(b);



        }
        public override void releaseLeftClick(int x, int y)
        {
            base.releaseLeftClick(x, y);

            if (!hasTouchScrolled && currentApp == appAtClickStart)
            {
                if (currentApp == "appPhoto")
                {
                    ReleaseLeftClickPhotoApp(x, y);
                }
            }

            ResetCameraZoomHoldState();
            isDragging = false;
            isScrolling = false;
        }

        public override void update(GameTime time)
        {
            base.update(time);
            UpdateAndroidKeyboard();
            EnsureFreeControllerCursor();
            UpdateLockScreenInitialization(time);
            UpdateLockScreenUnlockAnimation(time);
            UpdateCameraZoomHold(time);

            appLabelMarqueeElapsedSeconds += time.ElapsedGameTime.TotalSeconds;
            if (appLabelMarqueeElapsedSeconds >= 1_000_000d)
                appLabelMarqueeElapsedSeconds %= 1_000_000d;

            textCursorBlinkElapsedSeconds += time.ElapsedGameTime.TotalSeconds;
            if (textCursorBlinkElapsedSeconds >= 1_000_000d)
                textCursorBlinkElapsedSeconds %= 1_000_000d;

            UpdateTextInputRepeat(time);
            ModEntry.UpdatePlayerCaptureTimers(time.ElapsedGameTime.TotalSeconds);

            if (cameraCaptureFlashRemainingSeconds > 0d)
            {
                cameraCaptureFlashRemainingSeconds = Math.Max(0d, cameraCaptureFlashRemainingSeconds - time.ElapsedGameTime.TotalSeconds);
            }

            if (isDragging)
            {
                xPositionOnScreen = Game1.getMouseX() - dragOffsetX;
                yPositionOnScreen = Game1.getMouseY() - dragOffsetY;
                ClampPhoneMenuToViewport();
            }

            ModEntry.currentMenuX = xPositionOnScreen;
            ModEntry.currentMenuY = yPositionOnScreen;

            if (currentApp == "appNotification")
            {
                float lerpAmount = (float)(time.ElapsedGameTime.TotalSeconds * ChatScrollLerpSpeed);
                lerpAmount = Math.Clamp(lerpAmount, 0f, 1f);

                ClampNotificationScroll();
                notificationScrollOffset = MathHelper.Lerp(notificationScrollOffset, notificationScrollTarget, lerpAmount);

                if (Math.Abs(notificationScrollOffset - notificationScrollTarget) <= 0.5f)
                    notificationScrollOffset = notificationScrollTarget;
            }
            else if (currentApp == "appPhoto")
            {
                float lerpAmount = (float)(time.ElapsedGameTime.TotalSeconds * PhotoScrollLerpSpeed);
                lerpAmount = Math.Clamp(lerpAmount, 0f, 1f);
                UpdatePhotoScroll(lerpAmount);
            }


        }
        public override void leftClickHeld(int x, int y)
        {
            base.leftClickHeld(x, y);

            if (cameraZoomHoldDirection != 0)
            {
                isDragging = false;
                isScrolling = false;
                return;
            }

            if (!isDragging && !isScrolling)
            {
                bool photoAllowsScroll = currentApp == "appPhoto" && photoDetailIndex < 0;
                if (currentApp != null && currentApp != "appCamera" && (currentApp != "appPhoto" || photoAllowsScroll) && GetPhoneContentBounds().Contains(x, y))
                {
                    isScrolling = true;
                    lastScrollMouseY = y;
                }
                else if (GetActivePhoneDragBounds().Contains(x, y) && !GetPhoneContentBounds().Contains(x, y))
                {
                    isDragging = true;
                    dragOffsetX = x - xPositionOnScreen;
                    dragOffsetY = y - yPositionOnScreen;
                }
            }

            if (isScrolling)
            {
                if (Math.Abs(y - touchScrollStartY) > 5)
                    hasTouchScrolled = true;

                int deltaY = y - lastScrollMouseY;
                lastScrollMouseY = y;
                if (deltaY != 0)
                {
                    ApplyTouchScrollDelta(-deltaY);
                }
            }
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            lastScrollMouseY = y;
            touchScrollStartY = y;
            hasTouchScrolled = false;
            isScrolling = false;
            appAtClickStart = currentApp;


            if (HandleAndroidKeyboardTap(x, y))
                return;

            if (HandleLockScreenTap(x, y))
                return;

            if (IsHomeLandingInteractive())
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

            bool hideCameraOverlayButtons = currentApp == "appCamera" && ModEntry.IsPlayerCaptureCursorHidden();

            if (currentApp == "appCamera" && !hideCameraOverlayButtons)
            {
                if (cameraZoomOutButtonBounds.Contains(x, y))
                {
                    AdjustCameraZoom(-CameraZoomStep);
                    BeginCameraZoomHold(-1);
                    return;
                }

                if (cameraZoomInButtonBounds.Contains(x, y))
                {
                    AdjustCameraZoom(CameraZoomStep);
                    BeginCameraZoomHold(1);
                    return;
                }

                if (cameraFlashButtonBounds.Contains(x, y))
                {
                    ModEntry.cameraFlashMode = !ModEntry.cameraFlashMode;
                    Game1.playSound("smallSelect");
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

            if (currentApp == "appCamera" && !hideCameraOverlayButtons && IsCameraCaptureButtonPressed(x, y))
            {
                ModEntry.QueuePlayerPhotoCapture(ModEntry.GetPlayerPhotoCaptureBounds(xPositionOnScreen, yPositionOnScreen));
                Game1.playSound("cameraNoise");
                TriggerCameraCaptureFlash();
                return;
            }

            if (IsBackButtonPressed(x, y))
            {
                if (currentApp == "appSetting")
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
                else if (currentApp == "appPhoto")
                {
                    if (HandlePhotoAppBackButton())
                        return;
                    ClosePhotoApp();
                    currentApp = null;
                    return;
                }
                else if (new List<string> { "appCamera", "appNotification", "appStore", ExternalGroupAppState }.Contains(currentApp))
                {
                    if (currentApp == ExternalGroupAppState)
                        ClearCurrentExternalGroup();
                    else if (currentApp == "appStore")
                    {
                        if (TryHandleAppStoreBackButton())
                            return;

                        AppStoreManager.DisposeTextures();
                        ResetAppStoreState();
                    }


                    currentApp = null;
                    return;
                }
            }

            if (IsLockButtonPressed(x, y))
            {
                ClosePhoneMenu();
                return;
            }

            if (IsHomeButtonPressed(x, y))
            {
                ClosePhoneMenu();
                ModEntry.OpenPhoneFromHudTrigger();
                return;

            }

            if (currentApp == "appPhoto")
            {
                ReceiveLeftClickPhotoApp(x, y);
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







            if (currentApp == "appSetting")
            {
                if (currentSettingMenuState == SettingMenuMainState)
                {
                    foreach (KeyValuePair<string, Rectangle> option in settingOptionBounds)
                    {
                        if (!option.Value.Contains(x, y))
                            continue;

                        if (option.Key == SettingMenuOptionPhoneSetting)
                        {
                            var configMenu = ModEntry.SHelper?.ModRegistry?.GetApi<Smartphone.Data.IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
                            if (configMenu != null)
                            {
                                Game1.playSound("smallSelect");
                                ClosePhoneMenu();
                                configMenu.OpenModMenu(ModEntry.Instance.ModManifest);
                            }
                            return;
                        }

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
                        ModEntry.currentPhoneSound = button.name;
                        return;
                    }
                }
                else if (currentSettingMenuState == SettingMenuTextColorState)
                {
                    foreach (var button in phoneTextColorButton.Values)
                    {
                        if (!button.containsPoint(x, y))
                            continue;

                        ModEntry.currentPhoneTextColor = button.name;
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
            else if (currentApp == "appStore")
            {
                ReceiveLeftClickAppStore(x, y);
            }
        }


        public override void receiveScrollWheelAction(int direction)
        {
            if (IsHomeLandingInteractive())
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
            else if (currentApp == "appStore")
            {
                ReceiveScrollWheelActionAppStore(direction);
            }
            else if (currentApp == "appPhoto" && photoDetailIndex < 0)
            {
                HandlePhotoScrollWheel(direction);
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
            if (currentApp == "appPhoto" && HandlePhotoAlbumNameKeyPress(key))
                return;

            if (key == Keys.Escape)
            {
                if (currentApp != null)
                {


                    if (currentApp == "appSetting" && currentSettingMenuState != SettingMenuMainState)
                    {
                        currentSettingMenuState = SettingMenuMainState;
                        scrollOffset = 0;
                        return;
                    }

                    if (currentApp == "appPhoto")
                    {
                        if (HandlePhotoAppBackButton())
                            return;
                        ClosePhotoApp();
                        currentApp = null;
                        return;
                    }

                    if (currentApp == ExternalGroupAppState)
                        ClearCurrentExternalGroup();
                    else if (currentApp == "appStore")
                    {
                        if (TryHandleAppStoreBackButton())
                            return;

                        AppStoreManager.DisposeTextures();
                        ResetAppStoreState();
                    }

                    currentApp = null;
                    return;
                }

                ResetEditableTextFieldState(EditableTextFieldKind.Search);
                exitThisMenu();
                return;
            }

            else if (currentApp == "appPhoto")
            {
                if (HandlePhotoAlbumNameKeyPress(key))
                    return;
            }
        }

        private EditableTextFieldKind GetActiveEditableTextField()
        {
            if (currentApp == "appPhoto" && photoAlbumCreationOpen)
                return EditableTextFieldKind.PhotoAlbumName;

            return EditableTextFieldKind.None;
        }

        private void SetPhoneTextInputFocus(bool focused)
        {
            if (Game1.keyboardDispatcher == null)
                return;

            if (focused)
            {
                if (!ReferenceEquals(Game1.keyboardDispatcher.Subscriber, textInputSubscriber))
                    Game1.keyboardDispatcher.Subscriber = textInputSubscriber;

                return;
            }

            if (ReferenceEquals(Game1.keyboardDispatcher.Subscriber, textInputSubscriber))
                Game1.keyboardDispatcher.Subscriber = null;
        }

        private bool IsEditableTextFieldAcceptingComposedInput(EditableTextFieldKind field)
        {
            return field switch
            {
                EditableTextFieldKind.PhotoAlbumName => currentApp == "appPhoto" && photoAlbumCreationOpen,
                _ => false
            };
        }

        private bool TryApplyComposedTextInput(string? inputText)
        {
            EditableTextFieldKind field = GetActiveEditableTextField();
            if (!IsEditableTextFieldAcceptingComposedInput(field))
                return false;

            string normalizedText = (inputText ?? "").Trim();
            if (normalizedText.Length == 0)
                return false;

            if (!TryApplyEditableTextInsertionToField(field, normalizedText))
                return false;

            if (field == EditableTextFieldKind.ProfileAge
                || field == EditableTextFieldKind.ProfileBirthday
                || field == EditableTextFieldKind.ProfileDescription)
            {
                NormalizeProfileFieldState(field);
            }

            Game1.playSound("coin");
            return true;
        }

        private bool TryApplyEditableTextInsertionToField(EditableTextFieldKind field, string insertionText)
        {
            if (string.IsNullOrEmpty(insertionText))
                return false;

            switch (field)
            {


                case EditableTextFieldKind.ProfileAge:
                    ApplyEditableTextInsertion(
                        field,
                        ref ModEntry.currentPlayerAge,
                        ref textProfileAgeCursorIndex,
                        ref textProfileAgeSelectionAnchorIndex,
                        insertionText);
                    return true;

                case EditableTextFieldKind.ProfileBirthday:
                    ApplyEditableTextInsertion(
                        field,
                        ref ModEntry.currentPlayerBirthDate,
                        ref textProfileBirthdayCursorIndex,
                        ref textProfileBirthdaySelectionAnchorIndex,
                        insertionText);
                    return true;

                case EditableTextFieldKind.ProfileDescription:
                    ApplyEditableTextInsertion(
                        field,
                        ref ModEntry.currentPlayerProfile,
                        ref textProfileDescriptionCursorIndex,
                        ref textProfileDescriptionSelectionAnchorIndex,
                        insertionText);
                    return true;

                case EditableTextFieldKind.PhotoAlbumName:
                    ApplyEditableTextInsertion(
                        field,
                        ref photoNewAlbumName,
                        ref photoNewAlbumNameCursorIndex,
                        ref photoNewAlbumNameSelectionAnchorIndex,
                        insertionText);
                    return true;

                default:
                    return false;
            }
        }


        private void UpdateTextInputRepeat(GameTime time)
        {
        }

        private List<TextEditSnapshot> GetTextUndoHistory(EditableTextFieldKind field)
        {
            return field switch
            {
                EditableTextFieldKind.Search or EditableTextFieldKind.Chat => currentMessageUndoHistory,
                EditableTextFieldKind.ProfileAge => textProfileAgeUndoHistory,
                EditableTextFieldKind.ProfileBirthday => textProfileBirthdayUndoHistory,
                EditableTextFieldKind.ProfileDescription => textProfileDescriptionUndoHistory,
                EditableTextFieldKind.PhotoAlbumName => photoAlbumNameUndoHistory,
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


        private void TriggerAndroidKeyboard(EditableTextFieldKind field, string currentText)
        {
            if (Constants.TargetPlatform != GamePlatform.Android) return;

            pendingKeyboardField = field;

            try
            {
                Type keyboardInputType = typeof(Microsoft.Xna.Framework.Input.Keyboard).Assembly.GetType("Microsoft.Xna.Framework.Input.KeyboardInput");
                if (keyboardInputType != null)
                {
                    var showMethod = keyboardInputType.GetMethod("Show", new[] { typeof(string), typeof(string), typeof(string), typeof(bool) });
                    if (showMethod != null)
                    {
                        pendingKeyboardTask = (Task<string>)showMethod.Invoke(null, new object[] { "Input", "Enter text", currentText, false });
                    }
                }
            }
            catch (Exception)
            {
                pendingKeyboardTask = null;
                pendingKeyboardField = EditableTextFieldKind.None;
            }
        }

        private void UpdateAndroidKeyboard()
        {
            if (pendingKeyboardTask != null && pendingKeyboardTask.IsCompleted)
            {
                if (!pendingKeyboardTask.IsFaulted && pendingKeyboardTask.Result != null)
                {
                    string result = pendingKeyboardTask.Result;
                    EditableTextFieldKind field = pendingKeyboardField;
                    SetEditableTextFieldState(field, result, result.Length, result.Length, clearUndoHistory: false);


                }
                pendingKeyboardTask = null;
                pendingKeyboardField = EditableTextFieldKind.None;
            }
        }

        private bool HandleAndroidKeyboardTap(int x, int y)
        {
            if (Constants.TargetPlatform != GamePlatform.Android)
                return false;



            if (currentApp == "appPhoto" && photoAlbumCreationOpen)
            {
                if (photoAlbumInputAreaBounds.Contains(x, y))
                {
                    TriggerAndroidKeyboard(EditableTextFieldKind.PhotoAlbumName, photoNewAlbumName);
                    return true;
                }
            }

            return false;
        }

        private void ApplyTouchScrollDelta(int pixelDelta)
        {
            if (currentApp == "appSetting")
            {
                // Simple accumulation for settings
                chatScrollOffset += pixelDelta;
                while (chatScrollOffset >= 60)
                {
                    chatScrollOffset -= 60;
                    scrollOffset = Math.Min(scrollOffset + 1, Math.Max(0, 20)); // Approximate max scroll
                }
                while (chatScrollOffset <= -60)
                {
                    chatScrollOffset += 60;
                    scrollOffset = Math.Max(0, scrollOffset - 1);
                }
            }
            else if (currentApp == "appNotification")
            {
                float maxScroll = CalculateNotificationScrollToBottomOffset(notificationHistory);
                notificationScrollTarget = Math.Clamp(notificationScrollTarget + pixelDelta, 0f, maxScroll);
            }

            else if (currentApp == "appStore")
            {
                if (appStoreCurrentState == AppStoreState.Detail)
                {
                    appStoreDetailScrollOffset = Math.Max(0, Math.Min(appStoreDetailScrollOffset + pixelDelta, appStoreMaxDetailScroll));
                }
            }
            else if (currentApp == "appPhoto" && photoDetailIndex < 0)
            {
                ApplyPhotoTouchScrollDelta(pixelDelta);
            }
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



                case EditableTextFieldKind.ProfileAge:
                    ModEntry.currentPlayerAge = safeText;
                    textProfileAgeCursorIndex = safeCursorIndex;
                    textProfileAgeSelectionAnchorIndex = safeSelectionAnchorIndex;
                    break;

                case EditableTextFieldKind.ProfileBirthday:
                    ModEntry.currentPlayerBirthDate = safeText;
                    textProfileBirthdayCursorIndex = safeCursorIndex;
                    textProfileBirthdaySelectionAnchorIndex = safeSelectionAnchorIndex;
                    break;

                case EditableTextFieldKind.ProfileDescription:
                    ModEntry.currentPlayerProfile = safeText;
                    textProfileDescriptionCursorIndex = safeCursorIndex;
                    textProfileDescriptionSelectionAnchorIndex = safeSelectionAnchorIndex;
                    break;

                case EditableTextFieldKind.PhotoAlbumName:
                    photoNewAlbumName = safeText;
                    photoNewAlbumNameCursorIndex = safeCursorIndex;
                    photoNewAlbumNameSelectionAnchorIndex = safeSelectionAnchorIndex;
                    break;
            }

            if (clearUndoHistory)
                ClearTextUndoHistory(field);
        }

        private void ResetEditableTextFieldState(EditableTextFieldKind field, bool clearUndoHistory = true)
        {
            SetEditableTextFieldState(field, "", 0, 0, clearUndoHistory);
        }

        private static string NormalizeProfileBirthdayText(string? text)
        {
            string digitsOnly = new string((text ?? string.Empty).Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(digitsOnly))
                return string.Empty;

            if (!int.TryParse(digitsOnly, out int parsedValue))
                return string.Empty;

            return Math.Clamp(parsedValue, 1, 28).ToString();
        }

        private static string NormalizeProfileDescriptionText(string? text)
        {
            string safeText = text ?? string.Empty;
            return safeText.Length <= ProfileDescriptionMaxLength
                ? safeText
                : safeText[..ProfileDescriptionMaxLength];
        }

        private void NormalizeProfileFieldState(EditableTextFieldKind field)
        {
            switch (field)
            {
                case EditableTextFieldKind.ProfileAge:
                    {
                        string normalizedAge = (ModEntry.currentPlayerAge ?? string.Empty).Trim();
                        if (!string.Equals(normalizedAge, ModEntry.currentPlayerAge, StringComparison.Ordinal))
                            SetEditableTextFieldState(field, normalizedAge, normalizedAge.Length, normalizedAge.Length, clearUndoHistory: false);

                        break;
                    }

                case EditableTextFieldKind.ProfileBirthday:
                    {
                        string normalizedBirthday = NormalizeProfileBirthdayText(ModEntry.currentPlayerBirthDate);
                        if (!string.Equals(normalizedBirthday, ModEntry.currentPlayerBirthDate, StringComparison.Ordinal))
                            SetEditableTextFieldState(field, normalizedBirthday, normalizedBirthday.Length, normalizedBirthday.Length, clearUndoHistory: false);

                        break;
                    }

                case EditableTextFieldKind.ProfileDescription:
                    {
                        string normalizedDescription = NormalizeProfileDescriptionText(ModEntry.currentPlayerProfile);
                        if (!string.Equals(normalizedDescription, ModEntry.currentPlayerProfile, StringComparison.Ordinal))
                            SetEditableTextFieldState(field, normalizedDescription, normalizedDescription.Length, normalizedDescription.Length, clearUndoHistory: false);

                        break;
                    }
            }
        }

        private static (int Start, int End) GetSelectionRange(int cursorIndex, int selectionAnchorIndex, int textLength)
        {
            int safeCursorIndex = Math.Clamp(cursorIndex, 0, textLength);
            int safeSelectionAnchorIndex = Math.Clamp(selectionAnchorIndex, 0, textLength);
            return safeCursorIndex < safeSelectionAnchorIndex
                ? (safeCursorIndex, safeSelectionAnchorIndex)
                : (safeSelectionAnchorIndex, safeCursorIndex);
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



        public void OpenNotification()
        {
            NotificationManager.resetUnreadNotication();
            notificationHistory = NotificationManager.getNoticationList();
            notificationScrollOffset = 0f;
            notificationScrollTarget = 0f;
        }

        private void DrawSettingMenu(SpriteBatch b)
        {
            b.Draw(Game1.staminaRect, GetUiViewportBounds(), Color.Black * 0.6f);
            DrawPhoneScreenBackground(b, xOffset: 0);
            DrawPhoneFrame(b);
            backButton.draw(b, Color.Tan, 1f);
            lockButton.draw(b, Color.Tan, 1f);
            homeButton.draw(b, Color.Tan, 1f);

            string title = currentSettingMenuState switch
            {
                SettingMenuSoundState => ModEntry.SHelper.Translation.Get("ui.setting.sound"),
                SettingMenuTextColorState => ModEntry.SHelper.Translation.Get("ui.setting.text_color"),
                SettingMenuThemeState => ModEntry.SHelper.Translation.Get("ui.setting.theme"),
                _ => ModEntry.SHelper.Translation.Get("ui.setting.title")
            };

            DrawPhoneText(
                b,
                Game1.dialogueFont,
                title,
                new Vector2(PhoneX(SettingsTitleXOffsetBase), PhoneY(SettingsTitleYOffsetBase)),
                Color.Black);

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
                DrawThemeSettingTooltipIfHovered(b);
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
            phoneThemeHoverBounds.Clear();

            int yStart = PhoneY(SettingsMainOptionsStartYBase);
            DrawSettingOptionRow(b, SettingMenuOptionTextColor, ModEntry.SHelper.Translation.Get("ui.setting.text_color"), yStart);
            DrawSettingOptionRow(b, SettingMenuOptionSound, ModEntry.SHelper.Translation.Get("ui.setting.sound"), yStart + spacing);
            DrawSettingOptionRow(b, SettingMenuOptionTheme, ModEntry.SHelper.Translation.Get("ui.setting.theme"), yStart + spacing * 2);
            DrawSettingOptionRow(b, SettingMenuOptionPhoneSetting, ModEntry.SHelper.Translation.Get("ui.setting.phone_setting"), yStart + spacing * 3);
        }

        private void DrawSettingOptionRow(SpriteBatch b, string optionId, string displayText, int rowY)
        {
            int rowX = PhoneX(SettingsOptionRowXOffsetBase);
            int rowWidth = Math.Max(1, ScaleUiValue(SettingsOptionRowWidthBase));
            int rowHeight = Math.Max(1, ScaleUiValue(SettingsOptionRowHeightBase));

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

            DrawPhoneText(
                b,
                Game1.dialogueFont,
                displayText,
                new Vector2(
                    rowBounds.X + ScaleUiValue(SettingsOptionTextXPaddingBase),
                    rowBounds.Y + ScaleUiValue(SettingsOptionTextYOffsetBase)),
                Color.Black);

            b.Draw(
                Game1.mouseCursors,
                new Rectangle(
                    rowBounds.Right - ScaleUiValue(SettingsOptionArrowRightPaddingBase),
                    rowBounds.Y + ScaleUiValue(SettingsOptionArrowYOffsetBase),
                    Math.Max(1, ScaleUiValue(SettingsOptionArrowSizeBase)),
                    Math.Max(1, ScaleUiValue(SettingsOptionArrowSizeBase))),
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 33),
                Color.White);
        }

        private void DrawSoundSettingList(SpriteBatch b)
        {
            phoneSoundButton.Clear();
            phoneTextColorButton.Clear();
            phoneThemeButton.Clear();
            phoneThemeHoverBounds.Clear();
            settingOptionBounds.Clear();
            scrollOffset = Math.Max(0, Math.Min(scrollOffset, Math.Max(0, phoneSoundList.Count - maxVisibleNPCs)));

            int yStart = PhoneY(SettingsListStartYBase);
            for (int i = 0; i < visibleSlots; i++)
            {
                int index = i + scrollOffset;
                if (index >= phoneSoundList.Count)
                    break;

                string soundString = phoneSoundList[index];
                int y = yStart + i * spacing;

                int nameX = PhoneX(SettingsListNameXOffsetBase);
                DrawPhoneText(
                    b,
                    Game1.dialogueFont,
                    soundString,
                    new Vector2(nameX, y + ScaleUiValue(SettingsListNameYOffsetBase)),
                    Color.Black);

                Rectangle rect = new Rectangle(218, 428, 7, 7);
                if (ModEntry.currentPhoneSound == soundString)
                    rect = new Rectangle(211, 428, 7, 7);

                ClickableTextureComponent selectButton = new ClickableTextureComponent(
                    name: soundString,
                    bounds: new Rectangle(
                        nameX + ScaleUiValue(SettingsCheckboxXOffsetBase),
                        y + ScaleUiValue(SettingsCheckboxYOffsetBase),
                        Math.Max(1, ScaleUiValue(SettingsCheckboxSizeBase)),
                        Math.Max(1, ScaleUiValue(SettingsCheckboxSizeBase))),
                    label: null,
                    hoverText: "",
                    texture: Game1.mouseCursors,
                    sourceRect: rect,
                    scale: ScaleUiValue(5f)
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
            phoneThemeHoverBounds.Clear();
            settingOptionBounds.Clear();
            scrollOffset = Math.Max(0, Math.Min(scrollOffset, Math.Max(0, phoneTextColorList.Count - maxVisibleNPCs)));

            int yStart = PhoneY(SettingsListStartYBase);
            for (int i = 0; i < visibleSlots; i++)
            {
                int index = i + scrollOffset;
                if (index >= phoneTextColorList.Count)
                    break;

                string colorName = phoneTextColorList[index];
                int y = yStart + i * spacing;

                int nameX = PhoneX(SettingsListNameXOffsetBase);
                DrawPhoneText(
                    b,
                    Game1.dialogueFont,
                    colorName,
                    new Vector2(nameX, y + ScaleUiValue(SettingsListNameYOffsetBase)),
                    Color.Black);

                if (!TryGetHomeTextColor(colorName, out Color previewColor))
                    previewColor = Color.Black;

                b.Draw(
                    Game1.staminaRect,
                    new Rectangle(
                        nameX + ScaleUiValue(SettingsColorPreviewXOffsetBase),
                        y + ScaleUiValue(SettingsColorPreviewYOffsetBase),
                        Math.Max(1, ScaleUiValue(SettingsColorPreviewOuterWidthBase)),
                        Math.Max(1, ScaleUiValue(SettingsColorPreviewOuterHeightBase))),
                    Color.Black * 0.5f);
                b.Draw(
                    Game1.staminaRect,
                    new Rectangle(
                        nameX + ScaleUiValue(SettingsColorPreviewXOffsetBase + SettingsColorPreviewInnerXOffsetBase),
                        y + ScaleUiValue(SettingsColorPreviewYOffsetBase + SettingsColorPreviewInnerYOffsetBase),
                        Math.Max(1, ScaleUiValue(SettingsColorPreviewInnerWidthBase)),
                        Math.Max(1, ScaleUiValue(SettingsColorPreviewInnerHeightBase))),
                    previewColor);

                Rectangle rect = new Rectangle(218, 428, 7, 7);
                if (string.Equals(ModEntry.currentPhoneTextColor, colorName, StringComparison.OrdinalIgnoreCase))
                    rect = new Rectangle(211, 428, 7, 7);

                ClickableTextureComponent selectButton = new ClickableTextureComponent(
                    name: colorName,
                    bounds: new Rectangle(
                        nameX + ScaleUiValue(SettingsCheckboxXOffsetBase),
                        y + ScaleUiValue(SettingsCheckboxYOffsetBase),
                        Math.Max(1, ScaleUiValue(SettingsCheckboxSizeBase)),
                        Math.Max(1, ScaleUiValue(SettingsCheckboxSizeBase))),
                    label: null,
                    hoverText: "",
                    texture: Game1.mouseCursors,
                    sourceRect: rect,
                    scale: ScaleUiValue(5f)
                );

                phoneTextColorButton[colorName] = selectButton;
                selectButton.draw(b);
            }
        }

        private void DrawThemeSettingList(SpriteBatch b)
        {
            phoneThemeButton.Clear();
            phoneThemeHoverBounds.Clear();
            phoneSoundButton.Clear();
            phoneTextColorButton.Clear();
            settingOptionBounds.Clear();

            RefreshPhoneThemeList();
            scrollOffset = Math.Max(0, Math.Min(scrollOffset, Math.Max(0, phoneThemeList.Count - maxVisibleNPCs)));

            int yStart = PhoneY(SettingsListStartYBase);
            for (int i = 0; i < visibleSlots; i++)
            {
                int index = i + scrollOffset;
                if (index >= phoneThemeList.Count)
                    break;

                string themeName = phoneThemeList[index];
                int y = yStart + i * spacing;
                string themeReadmeText = GetThemeReadmeTooltipText(themeName);

                int nameX = PhoneX(SettingsListNameXOffsetBase);
                DrawPhoneText(
                    b,
                    Game1.dialogueFont,
                    themeName,
                    new Vector2(nameX, y + ScaleUiValue(SettingsListNameYOffsetBase)),
                    Color.Black);
                phoneThemeHoverBounds[themeName] = new Rectangle(
                    nameX + ScaleUiValue(SettingsThemeHoverXOffsetBase),
                    y + ScaleUiValue(SettingsThemeHoverYOffsetBase),
                    Math.Max(1, ScaleUiValue(SettingsThemeHoverWidthBase)),
                    Math.Max(1, ScaleUiValue(SettingsThemeHoverHeightBase)));

                Rectangle rect = new Rectangle(218, 428, 7, 7);
                if (string.Equals(ModEntry.currentPhoneTheme, themeName, StringComparison.OrdinalIgnoreCase))
                    rect = new Rectangle(211, 428, 7, 7);

                ClickableTextureComponent selectButton = new ClickableTextureComponent(
                    name: themeName,
                    bounds: new Rectangle(
                        nameX + ScaleUiValue(SettingsCheckboxXOffsetBase),
                        y + ScaleUiValue(SettingsCheckboxYOffsetBase),
                        Math.Max(1, ScaleUiValue(SettingsCheckboxSizeBase)),
                        Math.Max(1, ScaleUiValue(SettingsCheckboxSizeBase))),
                    label: null,
                    hoverText: themeReadmeText,
                    texture: Game1.mouseCursors,
                    sourceRect: rect,
                    scale: ScaleUiValue(5f)
                );

                phoneThemeButton[themeName] = selectButton;
                selectButton.draw(b);
            }
        }

        private void DrawThemeSettingTooltipIfHovered(SpriteBatch b)
        {
            if (phoneThemeHoverBounds.Count == 0)
                return;

            int mouseX = Game1.getMouseX();
            int mouseY = Game1.getMouseY();

            foreach (KeyValuePair<string, Rectangle> hoverEntry in phoneThemeHoverBounds)
            {
                if (!hoverEntry.Value.Contains(mouseX, mouseY))
                    continue;

                string tooltipText = GetThemeReadmeTooltipText(hoverEntry.Key);
                if (string.IsNullOrWhiteSpace(tooltipText))
                    return;

                IClickableMenu.drawHoverText(b, tooltipText, Game1.smallFont);
                return;
            }
        }

        private string GetThemeReadmeTooltipText(string themeName)
        {
            string safeThemeName = (themeName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(safeThemeName))
                return "";

            if (phoneThemeReadmeCache.TryGetValue(safeThemeName, out string? cachedText))
                return cachedText;

            string tooltipText = "";

            try
            {
                string? modFolderPath = ModEntry.Instance?.Helper?.DirectoryPath ?? ModEntry.SHelper?.DirectoryPath;
                if (!string.IsNullOrWhiteSpace(modFolderPath))
                {
                    string readmePath = Path.Combine(modFolderPath, AssetHelper.GetPhoneThemesRootPath(), safeThemeName, ThemeReadmeFileName);
                    if (File.Exists(readmePath))
                        tooltipText = (File.ReadAllText(readmePath) ?? "").Trim();
                }
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor.Log($"Failed to load theme readme for '{safeThemeName}': {ex.Message}", LogLevel.Trace);
            }

            phoneThemeReadmeCache[safeThemeName] = tooltipText;
            return tooltipText;
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
            ModEntry.currentPhoneTheme = AssetHelper.CurrentPhoneThemeName;
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
            textureAppNotification = Textures.AppNotification;
            textureAppAppStore = Textures.AppAppStore;
        }

        private Color GetCurrentHomeTextColor()
        {
            if (TryGetHomeTextColor(ModEntry.currentPhoneTextColor, out Color color))
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
            ModEntry.currentPhoneBackground = "";
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

                ModEntry.currentPhoneBackground = imagePath;
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor.Log($"Failed to load phone background image '{imagePath}': {ex.Message}", LogLevel.Warn);
                ResetPhoneBackgroundToDefault();
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

        private static Rectangle GetUiViewportBounds()
        {
            int viewportWidth = Math.Max(1, Game1.uiViewport.Width);
            int viewportHeight = Math.Max(1, Game1.uiViewport.Height);
            return new Rectangle(0, 0, viewportWidth, viewportHeight);
        }

        private void EnsureFreeControllerCursor()
        {
            if (forcedFreeControllerCursor || Game1.options == null)
                return;

            if (!Game1.options.SnappyMenus)
                return;

            if (TrySetSnappyMenusOption(false))
            {
                forcedFreeControllerCursor = true;
                return;
            }

            ModEntry.SMonitor.Log("Unable to disable controller-style menu snapping for the smartphone UI.", LogLevel.Trace);
        }

        private void RestoreControllerCursorSetting()
        {
            if (!forcedFreeControllerCursor)
                return;

            if (!TrySetSnappyMenusOption(true))
                ModEntry.SMonitor.Log("Unable to restore controller-style menu snapping after closing the smartphone UI.", LogLevel.Trace);

            forcedFreeControllerCursor = false;
        }

        private static bool TrySetSnappyMenusOption(bool value)
        {
            if (Game1.options == null)
                return false;

            object options = Game1.options;
            Type optionsType = options.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            try
            {
                PropertyInfo? snappyProperty = optionsType.GetProperty("SnappyMenus", flags);
                if (snappyProperty?.CanWrite == true)
                {
                    snappyProperty.SetValue(options, value);
                    return true;
                }

                foreach (string fieldName in new[] { "snappyMenus", "_snappyMenus", "<SnappyMenus>k__BackingField" })
                {
                    FieldInfo? field = optionsType.GetField(fieldName, flags);
                    if (field?.FieldType != typeof(bool))
                        continue;

                    field.SetValue(options, value);
                    return true;
                }

                foreach (string methodName in new[] { "set_SnappyMenus", "SetSnappyMenus", "setSnappyMenus" })
                {
                    MethodInfo? method = optionsType.GetMethod(methodName, flags, binder: null, types: new[] { typeof(bool) }, modifiers: null);
                    if (method == null)
                        continue;

                    method.Invoke(options, new object[] { value });
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private bool IsHomeLandingInteractive()
        {
            return currentApp == null
                && rootLandingState == RootLandingState.Home
                && !lockScreenUnlockAnimating;
        }

        private void DrawRootPhoneScreen(SpriteBatch b)
        {
            homeAppClickBounds.Clear();
            homeAppPrevPageBounds = Rectangle.Empty;
            homeAppNextPageBounds = Rectangle.Empty;
            lockScreenTapBounds = Rectangle.Empty;

            b.Draw(Game1.staminaRect, GetUiViewportBounds(), Color.Black * 0.6f);

            if (lockScreenUnlockAnimating)
            {
                DrawHomeLandingScreen(b, xOffset: 0, drawApps: true);

                float progress = GetLockScreenUnlockProgress();
                int swipeOffset = (int)Math.Round(-GetPhoneContentBounds().Width * progress);
                DrawLockScreenScreen(b, swipeOffset);
                DrawPhoneFrame(b);
                return;
            }

            if (rootLandingState == RootLandingState.Initializing)
            {
                DrawLockScreenInitializationScreen(b, xOffset: 0);
                DrawPhoneFrame(b);
                return;
            }

            if (rootLandingState == RootLandingState.LockScreen)
            {
                DrawLockScreenScreen(b, xOffset: 0);
                DrawPhoneFrame(b);
                return;
            }

            DrawHomeLandingScreen(b, xOffset: 0, drawApps: true);
            DrawPhoneFrame(b);
        }

        private void DrawHomeLandingScreen(SpriteBatch b, int xOffset, bool drawApps)
        {
            DrawPhoneScreenBackground(b, xOffset, applyBackgroundImage: true);

            if (!drawApps)
                return;

            if (xOffset == 0)
            {
                DrawHomeApps(b);
                return;
            }

            int originalX = xPositionOnScreen;
            xPositionOnScreen = originalX + xOffset;
            try
            {
                DrawHomeApps(b);
            }
            finally
            {
                xPositionOnScreen = originalX;
            }
        }

        private void DrawLockScreenScreen(SpriteBatch b, int xOffset)
        {
            Rectangle contentBounds = GetPhoneContentBounds();
            lockScreenTapBounds = contentBounds;
            if (contentBounds.Width <= 0 || contentBounds.Height <= 0)
                return;

            DrawWithinPhoneContentClip(b, () =>
            {
                DrawPhoneScreenBackground(b, xOffset, applyBackgroundImage: true);

                string timeText = Game1.getTimeOfDayString(Game1.timeOfDay);
                string dateText = BuildLockScreenDateText();

                Vector2 timeSize = Game1.dialogueFont.MeasureString(timeText) * LockScreenTimeTextScale;
                Vector2 dateSize = Game1.smallFont.MeasureString(dateText) * LockScreenDateTextScale;

                float centerX = contentBounds.Center.X + xOffset;
                float topY = contentBounds.Top + 82f;

                Vector2 timePosition = new Vector2(centerX - (timeSize.X / 2f), topY);
                Vector2 datePosition = new Vector2(centerX - (dateSize.X / 2f), topY + timeSize.Y + 10f);

                DrawShadowedText(
                    b,
                    Game1.dialogueFont,
                    timeText,
                    timePosition,
                    Color.White,
                    new Color(0, 0, 0, 180),
                    LockScreenTimeTextScale);

                DrawShadowedText(
                    b,
                    Game1.smallFont,
                    dateText,
                    datePosition,
                    Color.White,
                    new Color(0, 0, 0, 180),
                    LockScreenDateTextScale);

                DrawLockScreenWeatherSummary(
                    b,
                    contentBounds,
                    centerX,
                    datePosition.Y + dateSize.Y + LockScreenWeatherPanelTopSpacing);

                bool showUpdateHint = ModEntry.hasNewVersionAvailable;
                string hintText = showUpdateHint
                    ? ModEntry.SHelper.Translation.Get("ui.lockscreen.update_available")
                    : ModEntry.SHelper.Translation.Get("ui.lockscreen.tap_to_unlock");
                Vector2 hintSize = Game1.smallFont.MeasureString(hintText) * LockScreenHintTextScale;
                Vector2 hintPosition = new Vector2(
                    centerX - (hintSize.X / 2f),
                    contentBounds.Bottom - hintSize.Y - 28f);

                if (showUpdateHint)
                {
                    Rectangle hintBoxBounds = new Rectangle(
                        (int)Math.Round(hintPosition.X - 22f),
                        (int)Math.Round(hintPosition.Y - 8f),
                        (int)Math.Round(hintSize.X + 44f),
                        (int)Math.Round(hintSize.Y + 18f));

                    IClickableMenu.drawTextureBox(
                        b,
                        Game1.menuTexture,
                        new Rectangle(0, 256, 60, 60),
                        hintBoxBounds.X,
                        hintBoxBounds.Y,
                        hintBoxBounds.Width,
                        hintBoxBounds.Height,
                        new Color(180, 36, 36, 165),
                        1f,
                        false);
                }

                DrawShadowedText(
                    b,
                    Game1.smallFont,
                    hintText,
                    hintPosition,
                    showUpdateHint ? Color.White : Color.White * 0.9f,
                    showUpdateHint ? new Color(32, 0, 0, 185) : new Color(0, 0, 0, 170),
                    LockScreenHintTextScale);
            });
        }

        private void DrawLockScreenInitializationScreen(SpriteBatch b, int xOffset)
        {
            Rectangle contentBounds = GetPhoneContentBounds(xOffset);
            if (contentBounds.Width <= 0 || contentBounds.Height <= 0)
                return;

            DrawWithinPhoneContentClip(b, () =>
            {
                b.Draw(Game1.staminaRect, contentBounds, Color.Black);

                string titleText = ModEntry.SHelper.Translation.Get("ui.lockscreen.initializing");
                string progressText = $"{Math.Clamp(lockScreenInitializationProgressPercent, 0, 100)}%";

                Vector2 titleSize = Game1.dialogueFont.MeasureString(titleText) * LockScreenInitializationTitleTextScale;
                Vector2 progressSize = Game1.dialogueFont.MeasureString(progressText) * LockScreenInitializationProgressTextScale;

                float centerX = contentBounds.Center.X;
                float centerY = contentBounds.Center.Y;

                Vector2 titlePosition = new Vector2(centerX - (titleSize.X / 2f), centerY - titleSize.Y - 6f);
                Vector2 progressPosition = new Vector2(centerX - (progressSize.X / 2f), centerY + 10f);

                DrawShadowedText(
                    b,
                    Game1.dialogueFont,
                    titleText,
                    titlePosition,
                    Color.White,
                    new Color(0, 0, 0, 180),
                    LockScreenInitializationTitleTextScale);

                DrawShadowedText(
                    b,
                    Game1.dialogueFont,
                    progressText,
                    progressPosition,
                    new Color(215, 240, 255),
                    new Color(0, 0, 0, 185),
                    LockScreenInitializationProgressTextScale);
            });
        }

        private void DrawPhoneFrame(SpriteBatch b)
        {
            b.Draw(texturePhoneCapture, GetPhoneFrameBounds(), Color.White);
        }

        private void DrawPhoneScreenBackground(SpriteBatch b, int xOffset, bool applyBackgroundImage = false)
        {
            Rectangle contentBounds = GetPhoneContentBounds(xOffset);
            b.Draw(texturePhoneBackground, contentBounds, Color.White);

            if (applyBackgroundImage && phoneBackgroundImage != null)
            {
                b.Draw(phoneBackgroundImage, contentBounds, Color.White * 0.8f);
            }
        }

        private void DrawWithinPhoneContentClip(SpriteBatch b, Action drawAction)
        {
            Rectangle clipRect = GetPhoneContentBounds();
            Rectangle viewportBounds = Game1.graphics.GraphicsDevice.Viewport.Bounds;
            clipRect = Rectangle.Intersect(clipRect, viewportBounds);
            if (clipRect.Width <= 0 || clipRect.Height <= 0)
                return;

            b.End();

            Rectangle previousScissorRect = Game1.graphics.GraphicsDevice.ScissorRectangle;
            Game1.graphics.GraphicsDevice.ScissorRectangle = clipRect;

            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, appLabelScissorRasterizer);
            drawAction();
            b.End();

            Game1.graphics.GraphicsDevice.ScissorRectangle = previousScissorRect;
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
        }

        public Rectangle GetPhoneContentBounds(int xOffset = 0)
        {
            return new Rectangle(
                xPositionOnScreen + ModEntry.GetScaledPhoneContentOffsetX(phoneUiScale) + xOffset,
                yPositionOnScreen + ModEntry.GetScaledPhoneContentOffsetY(phoneUiScale),
                Math.Max(1, ScaleUiValue(texturePhoneBackground.Width)),
                Math.Max(1, ScaleUiValue(texturePhoneBackground.Height)));
        }

        private static void DrawShadowedText(SpriteBatch b, SpriteFont font, string text, Vector2 position, Color textColor, Color shadowColor, float scale = 1f)
        {
            Vector2 shadowOffset = new Vector2(2f, 2f);
            b.DrawString(font, text, position + shadowOffset, shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
            b.DrawString(font, text, position, textColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
        }

        private void DrawLockScreenWeatherSummary(SpriteBatch b, Rectangle contentBounds, float centerX, float topY)
        {
            float leftCenterX = centerX - LockScreenWeatherColumnHalfSpacing;
            float rightCenterX = centerX + LockScreenWeatherColumnHalfSpacing;

            string todayLabel = ModEntry.SHelper.Translation.Get("ui.weather.today");
            string forecastLabel = ModEntry.SHelper.Translation.Get("ui.weather.forecast");

            Vector2 todayLabelSize = Game1.smallFont.MeasureString(todayLabel) * LockScreenWeatherLabelTextScale;
            Vector2 forecastLabelSize = Game1.smallFont.MeasureString(forecastLabel) * LockScreenWeatherLabelTextScale;

            Vector2 todayLabelPosition = new Vector2(leftCenterX - (todayLabelSize.X / 2f), topY);
            Vector2 forecastLabelPosition = new Vector2(rightCenterX - (forecastLabelSize.X / 2f), topY);

            DrawShadowedText(
                b,
                Game1.smallFont,
                todayLabel,
                todayLabelPosition,
                Color.White,
                new Color(0, 0, 0, 170),
                LockScreenWeatherLabelTextScale);

            DrawShadowedText(
                b,
                Game1.smallFont,
                forecastLabel,
                forecastLabelPosition,
                Color.White,
                new Color(0, 0, 0, 170),
                LockScreenWeatherLabelTextScale);

            int todayIconIndex = GetTodayWeatherIconIndex();
            int forecastIconIndex = GetForecastWeatherIconIndex(GetForecastWeatherId(), GetTomorrowSeason());

            float iconTopY = topY + Math.Max(todayLabelSize.Y, forecastLabelSize.Y) + 4f;
            float iconWidth = LockScreenWeatherIconWidth * LockScreenWeatherIconScale;

            DrawLockScreenWeatherIcon(b, new Vector2(leftCenterX - (iconWidth / 2f), iconTopY), todayIconIndex);
            DrawLockScreenWeatherIcon(b, new Vector2(rightCenterX - (iconWidth / 2f), iconTopY), forecastIconIndex);

            int dividerX = (int)Math.Round(centerX);
            int dividerY = (int)Math.Round(topY + 4f);
            int dividerBottom = (int)Math.Round(iconTopY + (LockScreenWeatherIconHeight * LockScreenWeatherIconScale) + 6f);
            int dividerHeight = Math.Max(0, dividerBottom - dividerY);

            if (dividerHeight > 0)
                b.Draw(Game1.staminaRect, new Rectangle(dividerX, dividerY, 1, dividerHeight), Color.White * 0.35f);
        }

        private void DrawLockScreenWeatherIcon(SpriteBatch b, Vector2 position, int iconIndex)
        {
            Texture2D iconTexture = GetOrCreateLockScreenSoftWeatherIconTexture(iconIndex);
            b.Draw(iconTexture, position, Color.White);
        }

        private Texture2D GetOrCreateLockScreenSoftWeatherIconTexture(int iconIndex)
        {
            int safeIconIndex = iconIndex == 999
                ? 999
                : Math.Clamp(iconIndex, 0, 7);

            if (lockScreenWeatherIconSoftCache.TryGetValue(safeIconIndex, out Texture2D? cachedTexture)
                && cachedTexture != null
                && !cachedTexture.IsDisposed)
            {
                return cachedTexture;
            }

            Texture2D sourceTexture;
            Rectangle sourceRect;

            if (safeIconIndex == 999)
            {
                sourceTexture = Game1.mouseCursors_1_6;
                sourceRect = LockScreenGreenRainWeatherIconSource;
            }
            else
            {
                sourceTexture = Game1.mouseCursors;
                sourceRect = new Rectangle(
                    LockScreenWeatherIconStartX + (LockScreenWeatherIconWidth * safeIconIndex),
                    LockScreenWeatherIconStartY,
                    LockScreenWeatherIconWidth,
                    LockScreenWeatherIconHeight);
            }

            Texture2D iconTexture = BuildLockScreenSoftWeatherIconTexture(sourceTexture, sourceRect);
            lockScreenWeatherIconSoftCache[safeIconIndex] = iconTexture;
            return iconTexture;
        }

        private static Texture2D BuildLockScreenSoftWeatherIconTexture(Texture2D sourceTexture, Rectangle sourceRect)
        {
            int sourceWidth = Math.Max(1, sourceRect.Width);
            int sourceHeight = Math.Max(1, sourceRect.Height);
            float iconScale = Math.Max(0.01f, LockScreenWeatherIconScale);

            int targetWidth = Math.Max(1, (int)Math.Round(sourceWidth * iconScale));
            int targetHeight = Math.Max(1, (int)Math.Round(sourceHeight * iconScale));

            Color[] sourceData = new Color[sourceWidth * sourceHeight];
            sourceTexture.GetData(0, sourceRect, sourceData, 0, sourceData.Length);

            Color[] targetData = new Color[targetWidth * targetHeight];

            for (int y = 0; y < targetHeight; y++)
            {
                int sourceY = Math.Clamp((int)(y / iconScale), 0, sourceHeight - 1);
                for (int x = 0; x < targetWidth; x++)
                {
                    int sourceX = Math.Clamp((int)(x / iconScale), 0, sourceWidth - 1);
                    Color sourceColor = sourceData[(sourceY * sourceWidth) + sourceX];

                    float cornerAlpha = GetRoundedCornerAlpha(
                        x,
                        y,
                        targetWidth,
                        targetHeight,
                        LockScreenWeatherIconSoftCornerRadius,
                        LockScreenWeatherIconSoftCornerFeather);

                    byte alpha = (byte)Math.Clamp((int)Math.Round(sourceColor.A * cornerAlpha), 0, 255);
                    targetData[(y * targetWidth) + x] = new Color(sourceColor.R, sourceColor.G, sourceColor.B, alpha);
                }
            }

            Texture2D output = new Texture2D(Game1.graphics.GraphicsDevice, targetWidth, targetHeight);
            output.SetData(targetData);
            return output;
        }

        private static float GetRoundedCornerAlpha(
            int pixelX,
            int pixelY,
            int width,
            int height,
            float cornerRadius,
            float feather)
        {
            float safeRadius = Math.Clamp(cornerRadius, 0f, Math.Min(width, height) / 2f);
            if (safeRadius <= 0f)
                return 1f;

            float safeFeather = Math.Max(0.001f, feather);

            float centerX = width / 2f;
            float centerY = height / 2f;
            float pointX = pixelX + 0.5f;
            float pointY = pixelY + 0.5f;

            float qx = Math.Abs(pointX - centerX) - (centerX - safeRadius);
            float qy = Math.Abs(pointY - centerY) - (centerY - safeRadius);

            float outsideX = Math.Max(qx, 0f);
            float outsideY = Math.Max(qy, 0f);
            float outsideDistance = MathF.Sqrt((outsideX * outsideX) + (outsideY * outsideY));
            float insideDistance = Math.Min(Math.Max(qx, qy), 0f);
            float signedDistance = outsideDistance + insideDistance - safeRadius;

            if (signedDistance >= 0f)
                return 0f;

            if (signedDistance <= -safeFeather)
                return 1f;

            return 1f - ((signedDistance + safeFeather) / safeFeather);
        }

        private void DisposeLockScreenWeatherIconSoftCache()
        {
            foreach (Texture2D iconTexture in lockScreenWeatherIconSoftCache.Values)
            {
                if (iconTexture != null && !iconTexture.IsDisposed)
                    iconTexture.Dispose();
            }

            lockScreenWeatherIconSoftCache.Clear();
        }

        private static int GetTodayWeatherIconIndex()
        {
            if (Game1.weatherIcon == 999)
                return 999;

            return Math.Clamp(Game1.weatherIcon, 0, 7);
        }

        private static string GetForecastWeatherId()
        {
            string weatherId = Game1.currentLocation?.GetWeather()?.WeatherForTomorrow;
            if (string.IsNullOrWhiteSpace(weatherId))
                weatherId = Game1.weatherForTomorrow;

            return string.IsNullOrWhiteSpace(weatherId)
                ? Game1.weather_sunny
                : weatherId;
        }

        private static Season GetTomorrowSeason()
        {
            WorldDate tomorrow = new WorldDate(Game1.Date);
            tomorrow.TotalDays++;
            return tomorrow.Season;
        }

        private static int GetForecastWeatherIconIndex(string weatherId, Season season)
        {
            string safeWeatherId = weatherId?.Trim() ?? "";

            if (string.Equals(safeWeatherId, Game1.weather_green_rain, StringComparison.OrdinalIgnoreCase))
                return 999;

            if (string.Equals(safeWeatherId, Game1.weather_festival, StringComparison.OrdinalIgnoreCase))
                return 1;

            if (string.Equals(safeWeatherId, Game1.weather_lightning, StringComparison.OrdinalIgnoreCase))
                return 5;

            if (string.Equals(safeWeatherId, Game1.weather_snow, StringComparison.OrdinalIgnoreCase))
                return 7;

            if (string.Equals(safeWeatherId, Game1.weather_rain, StringComparison.OrdinalIgnoreCase))
                return 4;

            if (string.Equals(safeWeatherId, Game1.weather_debris, StringComparison.OrdinalIgnoreCase))
            {
                return season switch
                {
                    Season.Spring => 3,
                    Season.Fall => 6,
                    Season.Winter => 7,
                    _ => 2
                };
            }

            if (string.Equals(safeWeatherId, Game1.weather_wedding, StringComparison.OrdinalIgnoreCase))
                return 0;

            return 2;
        }

        private static string BuildLockScreenDateText()
        {
            string rawSeason = Game1.currentSeason;
            string seasonName = string.IsNullOrWhiteSpace(rawSeason)
                ? "Spring"
                : char.ToUpperInvariant(rawSeason[0]) + rawSeason.Substring(1).ToLowerInvariant();

            int day = Math.Max(1, Game1.dayOfMonth);
            int year = Math.Max(1, Game1.year);
            return $"{seasonName} {day}, Year {year}";
        }

        private void BeginLockScreenInitializationSequence()
        {
            rootLandingState = RootLandingState.Initializing;
            lockScreenUnlockAnimating = false;
            lockScreenUnlockElapsedSeconds = 0d;
            lockScreenTapBounds = Rectangle.Empty;

            lockScreenInitializationProgressPercent = 0;
            lockScreenInitializationElapsedSeconds = 0d;
            lockScreenInitializationNextTickSeconds = GetNextLockScreenInitializationTickSeconds();
        }

        private static double GetNextLockScreenInitializationTickSeconds()
        {
            double minDelay = Math.Max(0.01d, LockScreenInitializationProgressIntervalMinSeconds);
            double maxDelay = Math.Max(minDelay, LockScreenInitializationProgressIntervalMaxSeconds);
            return minDelay + (Random.Shared.NextDouble() * (maxDelay - minDelay));
        }

        private void UpdateLockScreenInitialization(GameTime time)
        {
            if (rootLandingState != RootLandingState.Initializing)
                return;

            lockScreenInitializationElapsedSeconds += time.ElapsedGameTime.TotalSeconds;
            double cappedElapsedSeconds = Math.Min(lockScreenInitializationElapsedSeconds, LockScreenInitializationDurationSeconds);
            int maxProgressForElapsedTime = (int)Math.Floor((cappedElapsedSeconds / LockScreenInitializationDurationSeconds) * 100d);
            int visibleMaxProgress = Math.Min(99, maxProgressForElapsedTime);

            lockScreenInitializationNextTickSeconds -= time.ElapsedGameTime.TotalSeconds;
            while (lockScreenInitializationNextTickSeconds <= 0d
                && lockScreenInitializationProgressPercent < visibleMaxProgress)
            {
                int remainingToVisibleMax = Math.Max(1, visibleMaxProgress - lockScreenInitializationProgressPercent);
                int progressStep = Random.Shared.Next(1, Math.Min(8, remainingToVisibleMax) + 1);

                lockScreenInitializationProgressPercent = Math.Min(
                    visibleMaxProgress,
                    lockScreenInitializationProgressPercent + progressStep);

                lockScreenInitializationNextTickSeconds += GetNextLockScreenInitializationTickSeconds();
            }

            if (lockScreenInitializationElapsedSeconds < LockScreenInitializationDurationSeconds)
                return;

            lockScreenInitializationProgressPercent = 100;
            lockScreenInitializationElapsedSeconds = LockScreenInitializationDurationSeconds;
            lockScreenInitializationNextTickSeconds = 0d;
            rootLandingState = RootLandingState.LockScreen;
            lockScreenTapBounds = Rectangle.Empty;
            ModEntry.pendingPhoneOsInitialization = false;
        }

        private bool HandleLockScreenTap(int x, int y)
        {
            if (currentApp != null
                || rootLandingState != RootLandingState.LockScreen
                || lockScreenUnlockAnimating)
            {
                return false;
            }

            Rectangle tapBounds = lockScreenTapBounds;
            if (tapBounds.Width <= 0 || tapBounds.Height <= 0)
                tapBounds = GetPhoneContentBounds();

            if (!tapBounds.Contains(x, y))
                return false;

            lockScreenUnlockAnimating = true;
            lockScreenUnlockElapsedSeconds = 0d;
            Game1.playSound("shwip");
            return true;
        }

        private float GetLockScreenUnlockProgress()
        {
            if (!lockScreenUnlockAnimating || LockScreenUnlockDurationSeconds <= 0d)
                return 0f;

            return (float)Math.Clamp(lockScreenUnlockElapsedSeconds / LockScreenUnlockDurationSeconds, 0d, 1d);
        }

        private void UpdateLockScreenUnlockAnimation(GameTime time)
        {
            if (!lockScreenUnlockAnimating)
                return;

            lockScreenUnlockElapsedSeconds += time.ElapsedGameTime.TotalSeconds;
            if (lockScreenUnlockElapsedSeconds < LockScreenUnlockDurationSeconds)
                return;

            lockScreenUnlockAnimating = false;
            lockScreenUnlockElapsedSeconds = 0d;
            rootLandingState = RootLandingState.Home;
            lockScreenTapBounds = Rectangle.Empty;
        }

        private List<HomeAppEntry> BuildHomeAppsSnapshot()
        {
            var apps = new List<HomeAppEntry>
            {
                new HomeAppEntry
                {
                    Id = BuiltinAppNotificationId,
                    DisplayName = ModEntry.SHelper.Translation.Get("app.notification.name"),
                    IconTexture = textureAppNotification,
                    GetBadgeCount = () => Math.Max(0, NotificationManager.getUnreadNotication())
                },
                new HomeAppEntry
                {
                    Id = BuiltinAppStoreId,
                    DisplayName = ModEntry.SHelper.Translation.Get("app.appstore.name"),
                    IconTexture = textureAppAppStore
                },
                new HomeAppEntry
                {
                    Id = BuiltinAppStoreId,
                    DisplayName = ModEntry.SHelper.Translation.Get("app.appstore.name"),
                    IconTexture = textureAppAppStore
                },
                new HomeAppEntry
                {
                    Id = BuiltinAppCameraId,
                    DisplayName = ModEntry.SHelper.Translation.Get("app.camera.name"),
                    IconTexture = textureAppCamera
                },
                new HomeAppEntry
                {
                    Id = BuiltinAppPhotoId,
                    DisplayName = ModEntry.SHelper.Translation.Get("app.photos.name"),
                    IconTexture = textureAppPhoto
                },
                new HomeAppEntry
                {
                    Id = BuiltinAppSettingId,
                    DisplayName = ModEntry.SHelper.Translation.Get("app.settings.name"),
                    IconTexture = textureAppSetting
                },
                new HomeAppEntry
                {
                    Id = BuiltinAppCalendarId,
                    DisplayName = ModEntry.SHelper.Translation.Get("app.calendar.name"),
                    IconTexture = textureAppCalendar
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
            int lineHeight = GetPhoneScaledLineHeight(font);
            int totalHeight = 0;

            foreach (string msg in msgList)
            {
                List<string> wrappedLines = SplitNotificationIntoLines(
                    msg,
                    font,
                    GetPhoneScaledWrapWidth(NotificationBubbleTextWrapWidth));
                int bubbleHeight = Math.Max(1, wrappedLines.Count) * lineHeight + NotificationBubbleInnerPadding;
                totalHeight += bubbleHeight + NotificationBubbleSpacing;
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
                    xPositionOnScreen + ScaleUiValue(HomeAppStartX + col * HomeAppSpacingX),
                    yPositionOnScreen + ScaleUiValue(HomeAppStartY + row * HomeAppSpacingY),
                    ScaleUiValue(84),
                    ScaleUiValue(84));

                DrawAppIcon(b, app.IconTexture, appBounds, app.SourceRect);
                DrawAppLabel(b, appBounds, app.DisplayName);
                homeAppClickBounds[app.Id] = appBounds;

                int badgeCount = GetHomeAppBadgeCount(app);
                if (badgeCount > 0)
                    DrawAppBadge(b, appBounds, badgeCount);
            }

            if (totalPages <= 1)
                return;

            homeAppPrevPageBounds = new Rectangle(
                xPositionOnScreen + ScaleUiValue(40),
                yPositionOnScreen + ScaleUiValue(790),
                ScaleUiValue(64),
                ScaleUiValue(64));
            homeAppNextPageBounds = new Rectangle(
                xPositionOnScreen + ScaleUiValue(496),
                yPositionOnScreen + ScaleUiValue(790),
                ScaleUiValue(64),
                ScaleUiValue(64));

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
            Vector2 pageTextSize = MeasurePhoneText(Game1.smallFont, pageText);
            Vector2 pageTextPosition = new Vector2(
                xPositionOnScreen + ScaleUiValue(300) - pageTextSize.X / 2f,
                yPositionOnScreen + ScaleUiValue(812) - pageTextSize.Y / 2f);
            DrawPhoneText(b, Game1.smallFont, pageText, pageTextPosition, GetCurrentHomeTextColor());
        }

        private void DrawExternalGroupItems(SpriteBatch b)
        {
            externalGroupItemClickBounds.Clear();

            if (string.IsNullOrWhiteSpace(currentExternalGroupId))
                return;

            List<RegisteredPhoneAppGroupItem> items = ModEntry.GetRegisteredPhoneAppGroupItemsSnapshot(currentExternalGroupId);
            if (items.Count == 0)
            {
                DrawPhoneText(
                    b,
                    Game1.smallFont,
                    "No app in this group yet.",
                    new Vector2(xPositionOnScreen + ScaleUiValue(110), yPositionOnScreen + ScaleUiValue(260)),
                    Color.Black);
                return;
            }

            int visibleCount = Math.Min(9, items.Count);
            for (int i = 0; i < visibleCount; i++)
            {
                RegisteredPhoneAppGroupItem item = items[i];
                int col = i % ExternalGroupColumns;
                int row = i / ExternalGroupColumns;

                Rectangle itemBounds = new Rectangle(
                    xPositionOnScreen + ScaleUiValue(ExternalGroupStartX + col * ExternalGroupSpacingX),
                    yPositionOnScreen + ScaleUiValue(ExternalGroupStartY + row * ExternalGroupSpacingY),
                    ScaleUiValue(84),
                    ScaleUiValue(84));

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
                case BuiltinAppStoreId:
                    currentApp = "appStore";
                    return true;

                case BuiltinAppCameraId:
                    currentApp = "appCamera";
                    return true;

                case BuiltinAppPhotoId:
                    OpenPhotoApp();
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

                default:
                    return ModEntry.TryInvokeRegisteredPhoneApp(appId, this);
            }
        }



        private static string GetCurrentSaveFolderName()
        {
            return ModEntry.GetActiveSaveFolderName();
        }

        private static string GetCaptureFolderPath(string photoFolderName)
        {
            return Path.Combine(
                ModEntry.Instance.Helper.DirectoryPath,
                "userdata",
                GetCurrentSaveFolderName(),
                photoFolderName);
        }

        private void BeginCameraZoomHold(int direction)
        {
            cameraZoomHoldDirection = Math.Sign(direction);
            cameraZoomHoldElapsedSeconds = 0d;
            cameraZoomHoldTriggered = false;
        }

        private void ResetCameraZoomHoldState()
        {
            cameraZoomHoldDirection = 0;
            cameraZoomHoldElapsedSeconds = 0d;
            cameraZoomHoldTriggered = false;
        }

        private void UpdateCameraZoomHold(GameTime time)
        {
            if (cameraZoomHoldDirection == 0)
                return;

            bool hideCameraOverlayButtons = currentApp == "appCamera" && ModEntry.IsPlayerCaptureCursorHidden();
            if (currentApp != "appCamera" || hideCameraOverlayButtons)
            {
                ResetCameraZoomHoldState();
                return;
            }

            MouseState mouseState = Mouse.GetState();
            if (mouseState.LeftButton != ButtonState.Pressed)
            {
                ResetCameraZoomHoldState();
                return;
            }

            int mouseX = Game1.getMouseX();
            int mouseY = Game1.getMouseY();
            Rectangle zoomBounds = cameraZoomHoldDirection > 0
                ? cameraZoomInButtonBounds
                : cameraZoomOutButtonBounds;

            if (!zoomBounds.Contains(mouseX, mouseY))
            {
                ResetCameraZoomHoldState();
                return;
            }

            cameraZoomHoldElapsedSeconds += time.ElapsedGameTime.TotalSeconds;

            if (!cameraZoomHoldTriggered)
            {
                if (cameraZoomHoldElapsedSeconds < CameraZoomHoldInitialDelaySeconds)
                    return;

                cameraZoomHoldElapsedSeconds -= CameraZoomHoldInitialDelaySeconds;
                cameraZoomHoldTriggered = true;
            }

            while (cameraZoomHoldElapsedSeconds >= CameraZoomHoldIntervalSeconds)
            {
                cameraZoomHoldElapsedSeconds -= CameraZoomHoldIntervalSeconds;

                if (!AdjustCameraZoom(cameraZoomHoldDirection * CameraZoomStep, playSound: false))
                {
                    ResetCameraZoomHoldState();
                    return;
                }
            }
        }

        private bool AdjustCameraZoom(float delta, bool playSound = true)
        {
            float currentZoom = ModEntry.cameraZoomFactor;
            float targetZoom = Math.Clamp(currentZoom + delta, CameraZoomMin, CameraZoomMax);
            targetZoom = (float)Math.Round(targetZoom / CameraZoomStep) * CameraZoomStep;
            targetZoom = Math.Clamp(targetZoom, CameraZoomMin, CameraZoomMax);

            if (Math.Abs(targetZoom - currentZoom) <= 0.0001f)
            {
                if (playSound)
                    Game1.playSound("cancel");
                return false;
            }

            ModEntry.cameraZoomFactor = targetZoom;
            if (playSound)
                Game1.playSound("drumkit6");

            return true;
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

        public void ResetToDefaultPosition()
        {
            isDragging = false;
            xPositionOnScreen = Game1.uiViewport.Width / 2 - ModEntry.GetScaledPhoneDefaultMenuOffsetX(phoneUiScale);
            yPositionOnScreen = Game1.uiViewport.Height / 2 - ModEntry.GetScaledPhoneDefaultMenuOffsetY(phoneUiScale);
            ClampPhoneMenuToViewport();
            ModEntry.currentMenuX = xPositionOnScreen;
            ModEntry.currentMenuY = yPositionOnScreen;
        }

        private bool IsCameraCaptureButtonPressed(int x, int y)
        {
            return captureButton?.bounds.Contains(x, y) == true;
        }

        private bool IsBackButtonPressed(int x, int y)
        {
            if (backButton.containsPoint(x, y) && !(currentApp == "appCamera" && ModEntry.cameraLandscapeMode))
                return true;

            if (currentApp == "appCamera" && ModEntry.cameraLandscapeMode)
                return GetLandscapeRotatedBounds(backButton.bounds).Contains(x, y);

            return false;
        }

        private bool IsLockButtonPressed(int x, int y)
        {
            if (lockButton.containsPoint(x, y) && !(currentApp == "appCamera" && ModEntry.cameraLandscapeMode))
                return true;

            if (currentApp == "appCamera" && ModEntry.cameraLandscapeMode)
                return GetLandscapeRotatedBounds(lockButton.bounds).Contains(x, y);

            return false;
        }

        private bool IsHomeButtonPressed(int x, int y)
        {
            if (homeButton.containsPoint(x, y) && !(currentApp == "appCamera" && ModEntry.cameraLandscapeMode))
                return true;

            if (currentApp == "appCamera" && ModEntry.cameraLandscapeMode)
                return GetLandscapeRotatedBounds(homeButton.bounds).Contains(x, y);

            return false;
        }

        private void DrawCameraControlButton(SpriteBatch b, Rectangle bounds, string label, bool active)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            if (TryGetCameraControlIconSource(label, out Texture2D iconTexture, out Rectangle iconSource, out bool useDarkInactiveStyle, out float iconScaleFactor))
            {
                if (useDarkInactiveStyle)
                {
                    Color iconBoxColor = active
                        ? new Color(95, 145, 185, 110)
                        : new Color(20, 20, 20, 95);

                    IClickableMenu.drawTextureBox(
                        b,
                        Game1.menuTexture,
                        new Rectangle(0, 256, 60, 60),
                        bounds.X,
                        bounds.Y,
                        bounds.Width,
                        bounds.Height,
                        iconBoxColor,
                        1f,
                        false);
                }

                int baseIconSize = Math.Max(1, Math.Min(bounds.Width, bounds.Height) - (useDarkInactiveStyle ? 16 : 20));
                int iconSize = Math.Max(1, (int)Math.Round(baseIconSize * iconScaleFactor));

                Rectangle iconBounds = new Rectangle(
                    bounds.Center.X - (iconSize / 2),
                    bounds.Center.Y - (iconSize / 2),
                    iconSize,
                    iconSize);

                Color iconColor = useDarkInactiveStyle
                    ? (active ? new Color(255, 255, 255, 220) : new Color(48, 48, 48, 175))
                    : new Color(255, 255, 255, 180);

                Rectangle shadowBounds = new Rectangle(iconBounds.X + 1, iconBounds.Y + 2, iconBounds.Width, iconBounds.Height);
                b.Draw(iconTexture, shadowBounds, iconSource, new Color(0, 0, 0, 70));
                b.Draw(iconTexture, iconBounds, iconSource, iconColor);
                return;
            }

            Color boxColor = active
                ? new Color(120, 200, 255, 145)
                : new Color(25, 25, 25, 120);

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

            Vector2 textSize = MeasurePhoneText(Game1.smallFont, label);
            Vector2 textPosition = new Vector2(
                bounds.X + ((bounds.Width - textSize.X) / 2f),
                bounds.Y + ((bounds.Height - textSize.Y) / 2f) + 5f);

            DrawPhoneText(b, Game1.smallFont, label, textPosition, new Color(250, 250, 250, 235));
        }

        private static bool TryGetCameraControlIconSource(
            string label,
            out Texture2D texture,
            out Rectangle sourceRect,
            out bool useDarkInactiveStyle,
            out float iconScaleFactor)
        {
            if (string.Equals(label, "+", StringComparison.Ordinal))
            {
                texture = Game1.mouseCursors;
                sourceRect = CameraZoomPlusIconSource;
                useDarkInactiveStyle = false;
                iconScaleFactor = 1.5f;
                return true;
            }

            if (string.Equals(label, "-", StringComparison.Ordinal))
            {
                texture = Game1.mouseCursors;
                sourceRect = CameraZoomMinusIconSource;
                useDarkInactiveStyle = false;
                iconScaleFactor = 1.5f;
                return true;
            }

            if (string.Equals(label, CameraFlashButtonLabel, StringComparison.Ordinal))
            {
                texture = Game1.mouseCursors;
                sourceRect = CameraFlashIconSource;
                useDarkInactiveStyle = true;
                iconScaleFactor = 1.25f;
                return true;
            }

            if (string.Equals(label, CameraSquareButtonLabel, StringComparison.Ordinal))
            {
                texture = Game1.mouseCursors;
                sourceRect = CameraSquareIconSource;
                useDarkInactiveStyle = true;
                iconScaleFactor = 1.25f;
                return true;
            }

            if (string.Equals(label, CameraLandscapeButtonLabel, StringComparison.Ordinal))
            {
                texture = Game1.mouseCursors2;
                sourceRect = CameraLandscapeIconSource;
                useDarkInactiveStyle = true;
                // Slightly stretch the 9x10 icon into a near-square destination for visual consistency.
                iconScaleFactor = 1.22f;
                return true;
            }

            texture = Game1.mouseCursors;
            sourceRect = Rectangle.Empty;
            useDarkInactiveStyle = false;
            iconScaleFactor = 1f;
            return false;
        }

        private void DrawCameraCaptureButton(SpriteBatch b, Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            if (captureButton?.texture == null)
                return;

            Rectangle sourceRect = captureButton.sourceRect;
            float scaleX = bounds.Width / (float)Math.Max(1, sourceRect.Width);
            float scaleY = bounds.Height / (float)Math.Max(1, sourceRect.Height);
            float iconScale = Math.Min(scaleX, scaleY);

            int iconWidth = Math.Max(1, (int)Math.Round(sourceRect.Width * iconScale));
            int iconHeight = Math.Max(1, (int)Math.Round(sourceRect.Height * iconScale));

            Rectangle iconBounds = new Rectangle(
                bounds.X + ((bounds.Width - iconWidth) / 2),
                bounds.Y + ((bounds.Height - iconHeight) / 2),
                iconWidth,
                iconHeight);

            Rectangle shadowBounds = new Rectangle(iconBounds.X + 2, iconBounds.Y + 3, iconBounds.Width, iconBounds.Height);
            b.Draw(
                captureButton.texture,
                shadowBounds,
                sourceRect,
                new Color(0, 0, 0, 70),
                0f,
                Vector2.Zero,
                SpriteEffects.None,
                1f);

            b.Draw(
                captureButton.texture,
                iconBounds,
                sourceRect,
                Color.White * 0.92f,
                0f,
                Vector2.Zero,
                SpriteEffects.None,
                1f);
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

        private void TriggerCameraCaptureFlash()
        {
            cameraCaptureFlashRemainingSeconds = CameraCaptureFlashDurationSeconds;
        }

        private void DrawCameraCaptureFlash(SpriteBatch b, Rectangle cameraPreviewBounds)
        {
            if (ModEntry.takeScreenshot || cameraCaptureFlashRemainingSeconds <= 0d || CameraCaptureFlashDurationSeconds <= 0d)
                return;

            Rectangle flashBounds = Rectangle.Intersect(cameraPreviewBounds, GetUiViewportBounds());
            if (flashBounds.Width <= 0 || flashBounds.Height <= 0)
                return;

            float progress = (float)(cameraCaptureFlashRemainingSeconds / CameraCaptureFlashDurationSeconds);
            progress = Math.Clamp(progress, 0f, 1f);
            float opacity = CameraCaptureFlashMaxOpacity * progress * progress;
            if (opacity <= 0f)
                return;

            b.Draw(Game1.staminaRect, flashBounds, Color.White * opacity);
        }

        private void DrawAppIcon(SpriteBatch b, Texture2D texture, Rectangle bounds, Rectangle? sourceRect)
        {
            Rectangle textureBounds = new Rectangle(0, 0, texture.Width, texture.Height);
            Rectangle source = sourceRect.HasValue
                ? Rectangle.Intersect(sourceRect.Value, textureBounds)
                : textureBounds;

            if (source.Width <= 0 || source.Height <= 0)
                source = textureBounds;

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
            float labelScale = GetPhoneTextScale(AppLabelFontScale);
            float labelWidth = labelFont.MeasureString(name).X * labelScale;

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
            float labelScale = GetPhoneTextScale(AppLabelFontScale);
            Vector2 labelSize = labelFont.MeasureString(label) * labelScale;
            Vector2 labelPos = new Vector2(
                appBounds.X + (appBounds.Width - labelSize.X) / 2f,
                appBounds.Bottom + 4);

            b.DrawString(labelFont, label, labelPos, GetCurrentHomeTextColor(), 0f, Vector2.Zero, labelScale, SpriteEffects.None, 1f);
        }

        private void DrawMarqueeAppLabel(SpriteBatch b, Rectangle appBounds, string appName, float viewportWidth)
        {
            SpriteFont labelFont = Game1.smallFont;
            string marqueeSource = appName + new string(' ', AppLabelTrailingSpaces);
            float labelScale = GetPhoneTextScale(AppLabelFontScale);
            float viewportHeight = labelFont.LineSpacing * labelScale;
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

            float marqueeWidth = labelFont.MeasureString(marqueeSource).X * labelScale;
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
            b.DrawString(labelFont, marqueeSource, drawPos, textColor, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 1f);
            b.DrawString(labelFont, marqueeSource, new Vector2(drawPos.X + marqueeWidth, drawPos.Y), textColor, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 1f);

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
            DrawPhoneText(b, Game1.smallFont, badgeText, badgeTextPosition, Color.White);
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

        public void OpenHomeScreen()
        {
            if (currentApp == ExternalGroupAppState)
                ClearCurrentExternalGroup();

            selectedNpc = null;
            scrollOffset = 0;
            homeAppPage = 0;
            currentSettingMenuState = SettingMenuMainState;
            ResetEditableTextFieldState(EditableTextFieldKind.Search);
            currentApp = null;
            rootLandingState = RootLandingState.Home;
            lockScreenUnlockAnimating = false;
            lockScreenUnlockElapsedSeconds = 0d;
            lockScreenTapBounds = Rectangle.Empty;
            lockScreenInitializationProgressPercent = 0;
            lockScreenInitializationElapsedSeconds = 0d;
            lockScreenInitializationNextTickSeconds = 0d;
        }

        public void OpenLockScreen()
        {
            OpenHomeScreen();

            if (ModEntry.pendingPhoneOsInitialization)
            {
                BeginLockScreenInitializationSequence();
                return;
            }

            rootLandingState = RootLandingState.LockScreen;
        }

        public void OpenRegisteredAppGroup(string groupCompositeId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(groupCompositeId))
                return;

            currentExternalGroupId = groupCompositeId;
            currentExternalGroupName = (displayName?.Length > 8)
                ? displayName.Substring(0, 8) + "..."
                : (displayName ?? "");
            currentApp = ExternalGroupAppState;
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
            AppStoreManager.DisposeTextures();
            if (currentApp == "appStore")
            {
                ResetAppStoreState();
            }
            if (currentApp == "appPhoto")
            {
                ClosePhotoApp();
            }

            SetPhoneTextInputFocus(false);
            currentMessage = null;
            lockScreenUnlockAnimating = false;
            lockScreenUnlockElapsedSeconds = 0d;
            lockScreenTapBounds = Rectangle.Empty;
            lockScreenInitializationProgressPercent = 0;
            lockScreenInitializationElapsedSeconds = 0d;
            lockScreenInitializationNextTickSeconds = 0d;

            if (currentApp != null)
            {
                if (currentApp == ExternalGroupAppState)
                    ClearCurrentExternalGroup();

                if (currentApp != null)
                    currentApp = null;
            }

            ResetEditableTextFieldState(EditableTextFieldKind.Search);
            RestoreControllerCursorSetting();
            exitThisMenu();
        }

    }
}
