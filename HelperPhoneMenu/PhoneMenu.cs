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

        partial void InitPhotoTextInput();

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

        private Texture2D texturePhoneBackground = Textures.PhoneBackground;
        private Texture2D texturePhoneCapture = Textures.PhoneEmpty;
        private Texture2D texturePortraitBackground = Textures.Background;

        private Texture2D textureAppCamera = Textures.AppCamera;
        private Texture2D textureAppPhoto = Textures.AppPhoto;
        private Texture2D textureAppSetting = Textures.AppSetting;
        private Texture2D textureAppNotification = Textures.AppNotification;
        private Texture2D textureAppAppStore = Textures.AppAppStore;
        private Texture2D textureAppCalendar = Textures.AppCalendar;



        private bool isScrolling = false;
        private int lastScrollMouseY = 0;
        private int touchScrollStartY = 0;
        private bool hasTouchScrolled = false;
        private const float ChatScrollPixelsPerWheelNotch = 48f;
        private const float ChatScrollLerpSpeed = 16f;
        private const int ScrollDrawOverscanBase = 72;
        private const int TextWrapCacheMaxEntries = 4096;
        private const float PhoneGlobalTextScale = 0.85f;

        private int ScrollDrawOverscan => Math.Max(1, ScaleUiValue(ScrollDrawOverscanBase));

        private readonly Dictionary<string, List<string>> textWrapCache = new(StringComparer.Ordinal);



        private ClickableTextureComponent backButton;
        private ClickableTextureComponent lockButton;
        private ClickableTextureComponent homeButton;


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



        private readonly Dictionary<string, Rectangle> homeAppClickBounds = new();
        private Rectangle homeAppPrevPageBounds = Rectangle.Empty;
        private Rectangle homeAppNextPageBounds = Rectangle.Empty;
        private int homeAppPage = 0;
        private double appLabelMarqueeElapsedSeconds = 0d;
        private readonly RasterizerState appLabelScissorRasterizer = new() { ScissorTestEnable = true };

        private readonly Dictionary<string, Rectangle> externalGroupItemClickBounds = new();
        private string currentExternalGroupId = "";
        private string currentExternalGroupName = "";


        private List<string> capturedImages;
        private Texture2D phoneBackgroundImage = null;

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

        private Rectangle GetPhoneFrameBounds()
        {
            return new Rectangle(xPositionOnScreen, yPositionOnScreen, width, height);
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

        public PhoneMenu() : base(
            Game1.uiViewport.Width / 2 - ModEntry.GetScaledPhoneDefaultMenuOffsetX(),
            Game1.uiViewport.Height / 2 - ModEntry.GetScaledPhoneDefaultMenuOffsetY(),
            ModEntry.GetScaledPhoneFrameWidth(),
            ModEntry.GetScaledPhoneFrameHeight(),
            true)
        {
            phoneUiScale = ModEntry.GetConfiguredPhoneUiScale();
            this.upperRightCloseButton = null;




            InitPhotoTextInput();

            InitSettingApp();

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


        private void UpdateSystemButtonsBounds()
        {
            int buttonY = this.yPositionOnScreen + ScaleUiValue(925);
            int buttonSize = ScaleUiValue(64);

            if (backButton == null)
            {
                backButton = new ClickableTextureComponent(
                    new Rectangle(this.xPositionOnScreen + ScaleUiValue(132), buttonY, buttonSize, buttonSize),
                    null,
                    Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 44),
                    ScaleUiValue(1f));
            }
            else
            {
                backButton.bounds = new Rectangle(this.xPositionOnScreen + ScaleUiValue(132), buttonY, buttonSize, buttonSize);
                backButton.scale = ScaleUiValue(1f);
            }

            if (lockButton == null)
            {
                lockButton = new ClickableTextureComponent(
                    new Rectangle(this.xPositionOnScreen + ScaleUiValue(405), buttonY, buttonSize, buttonSize),
                    null,
                    Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 44),
                    ScaleUiValue(1f));
            }
            else
            {
                lockButton.bounds = new Rectangle(this.xPositionOnScreen + ScaleUiValue(405), buttonY, buttonSize, buttonSize);
                lockButton.scale = ScaleUiValue(1f);
            }

            if (homeButton == null)
            {
                homeButton = new ClickableTextureComponent(
                    new Rectangle(this.xPositionOnScreen + ScaleUiValue(265), buttonY, buttonSize, buttonSize),
                    null,
                    Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 33),
                    ScaleUiValue(1.3f));
            }
            else
            {
                homeButton.bounds = new Rectangle(this.xPositionOnScreen + ScaleUiValue(265), buttonY, buttonSize, buttonSize);
                homeButton.scale = ScaleUiValue(1.3f);
            }
        }

        public override void draw(SpriteBatch b)
        {
            UpdateSystemButtonsBounds();

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
                DrawCameraApp(b);
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
                DrawAppStoreApp(b);
            }
            else if (currentApp == "appNotification")
            {
                DrawNotificationApp(b);
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
                else if (currentApp == "appStore")
                {
                    ReleaseLeftClickAppStore(x, y);
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

            appLabelMarqueeElapsedSeconds += time.ElapsedGameTime.TotalSeconds;
            if (appLabelMarqueeElapsedSeconds >= 1_000_000d)
                appLabelMarqueeElapsedSeconds %= 1_000_000d;

            textCursorBlinkElapsedSeconds += time.ElapsedGameTime.TotalSeconds;
            if (textCursorBlinkElapsedSeconds >= 1_000_000d)
                textCursorBlinkElapsedSeconds %= 1_000_000d;

            UpdateTextInputRepeat(time);
            ModEntry.UpdatePlayerCaptureTimers(time.ElapsedGameTime.TotalSeconds);

            if (currentApp == "appCamera")
            {
                UpdateCameraApp(time);
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
                UpdateNotificationApp(time);
            }
            else if (currentApp == "appPhoto")
            {
                float lerpAmount = (float)(time.ElapsedGameTime.TotalSeconds * PhotoScrollLerpSpeed);
                lerpAmount = Math.Clamp(lerpAmount, 0f, 1f);
                UpdatePhotoScroll(lerpAmount);
            }
            else if (currentApp == "appSetting")
            {
                UpdateSettingApp(time);
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

            if (IsBackButtonPressed(x, y))
            {
                if (HandleBackButtonAction())
                    return;
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

            if (currentApp == "appCamera")
            {
                ReceiveLeftClickCameraApp(x, y);
                return;
            }

            if (currentApp == "appPhoto")
            {
                ReceiveLeftClickPhotoApp(x, y);
                return;
            }


            else if (currentApp == "appNotification")
            {
                if (ReceiveLeftClickNotificationApp(x, y))
                    return;
            }







            else if (currentApp == "appSetting")
            {
                ReceiveLeftClickSettingApp(x, y);
                return;
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
                ReceiveScrollWheelActionSettingApp(direction);
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
                ReceiveScrollWheelActionNotificationApp(direction);
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
                    if (HandleBackButtonAction())
                        return;
                }

                exitThisMenu();
                return;
            }

            else if (currentApp == "appPhoto")
            {
                if (HandlePhotoAlbumNameKeyPress(key))
                    return;
            }
        }




        // SplitNotificationIntoLines moved to partial class

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



        private List<HomeAppEntry> BuildHomeAppsSnapshot()
        {
            var apps = new List<HomeAppEntry>
            {
                new HomeAppEntry
                {
                    Id = BuiltinAppNotificationId,
                    DisplayName = ModEntry.SHelper.Translation.Get("app.notification.name"),
                    IconTexture = textureAppNotification,
                    GetBadgeCount = () => Math.Max(0, NotificationManager.GetUnreadNotification())
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

        // Notification sizing/scrolling moved to partial class

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
                    settingScrollOffset = 0f;
                    settingScrollTarget = 0f;
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



        private bool IsSystemButtonPressed(ClickableTextureComponent button, int x, int y)
        {
            if (button == null)
                return false;

            if (button.containsPoint(x, y) && !(currentApp == "appCamera" && ModEntry.cameraLandscapeMode))
                return true;

            if (currentApp == "appCamera" && ModEntry.cameraLandscapeMode)
                return GetLandscapeRotatedBounds(button.bounds).Contains(x, y);

            return false;
        }

        private bool IsBackButtonPressed(int x, int y) => IsSystemButtonPressed(backButton, x, y);
        private bool IsLockButtonPressed(int x, int y) => IsSystemButtonPressed(lockButton, x, y);
        private bool IsHomeButtonPressed(int x, int y) => IsSystemButtonPressed(homeButton, x, y);

        private bool HandleBackButtonAction()
        {
            if (currentApp == "appSetting")
            {
                return HandleSettingAppBackButton();
            }
            if (currentApp == "appPhoto")
            {
                if (HandlePhotoAppBackButton())
                    return true;
                ClosePhotoApp();
                currentApp = null;
                return true;
            }
            if (currentApp == "appStore")
            {
                return HandleAppStoreBackButton();
            }
            if (currentApp == "appCamera")
            {
                return HandleCameraAppBackButton();
            }
            if (currentApp == ExternalGroupAppState)
            {
                ClearCurrentExternalGroup();
                currentApp = null;
                return true;
            }
            if (currentApp != null)
            {
                currentApp = null;
                return true;
            }
            return false;
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

            settingScrollOffset = 0f;
            settingScrollTarget = 0f;
            homeAppPage = 0;
            currentSettingMenuState = SettingMenuMainState;
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
            CloseAppStoreApp();
            if (currentApp == "appPhoto")
            {
                ClosePhotoApp();
            }

            SetPhoneTextInputFocus(false);
            lockScreenUnlockAnimating = false;
            lockScreenUnlockElapsedSeconds = 0d;
            lockScreenTapBounds = Rectangle.Empty;
            lockScreenInitializationProgressPercent = 0;
            lockScreenInitializationElapsedSeconds = 0d;
            lockScreenInitializationNextTickSeconds = 0d;

            if (currentApp == ExternalGroupAppState)
                ClearCurrentExternalGroup();

            currentApp = null;

            RestoreControllerCursorSetting();
            exitThisMenu();
        }

    }
}
