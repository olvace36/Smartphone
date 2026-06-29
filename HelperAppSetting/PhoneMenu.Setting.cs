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
    public partial class PhoneMenu
    {
        private string currentSettingMenuState = SettingMenuMainState;

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
        private float settingScrollOffset = 0f;
        private float settingScrollTarget = 0f;
        private int mainSpacing => Math.Max(1, ScaleUiValue(74));
        private int listSpacing => Math.Max(1, ScaleUiValue(60));

        private const string SettingMenuMainState = "settingMain";
        private const string SettingMenuSoundState = "settingSound";
        private const string SettingMenuTextColorState = "settingTextColor";
        private const string SettingMenuThemeState = "settingTheme";
        private const string SettingMenuThemeComponentListState = "settingThemeComponentList";

        private const string SettingMenuOptionTextColor = "textColor";
        private const string SettingMenuOptionSound = "sound";
        private const string SettingMenuOptionTheme = "theme";
        private const string SettingMenuOptionPhoneSetting = "phoneSetting";
        private const string ThemeReadmeFileName = "readme.txt";

        private string currentSelectedThemeComponent = "phone";
        private readonly Dictionary<string, Rectangle> themeComponentRowBounds = new(StringComparer.OrdinalIgnoreCase);

        private static List<string> GetThemeComponentKeys()
        {
            var keys = new List<string>
            {
                "phone", "app_appstore", "app_calendar", "app_camera", "app_notification", "app_photo", "app_setting"
            };

            foreach (var app in ModEntry.GetRegisteredPhoneAppsSnapshot())
            {
                string compKey = Textures.GetComponentFromAppId(app.CompositeId);
                if (!keys.Contains(compKey))
                {
                    keys.Add(compKey);
                }
            }

            return keys;
        }

        private static string GetFriendlyComponentName(string compKey)
        {

            return ModEntry.GetRegisteredAppDisplayName(compKey);
        }

        private const int SettingsTitleXOffsetBase = 155;
        private const int SettingsTitleYOffsetBase = 117;
        private const int SettingsMainOptionsStartYBase = 180;
        private const int SettingsListStartYBase = 200;
        private const int SettingsOptionRowXOffsetBase = 140;
        private const int SettingsOptionRowWidthBase = 430;
        private const int SettingsOptionRowHeightBase = 58;
        private const int SettingsOptionTextXPaddingBase = 20;
        private const int SettingsOptionTextYOffsetBase = 10;
        private const int SettingsOptionArrowSizeBase = 32;
        private const int SettingsOptionArrowRightPaddingBase = 42;
        private const int SettingsOptionArrowYOffsetBase = 13;
        private const int SettingsListNameXOffsetBase = 150;
        private const int SettingsListNameYOffsetBase = 63;
        private const int SettingsColorPreviewXOffsetBase = 220;
        private const int SettingsColorPreviewYOffsetBase = 65;
        private const int SettingsColorPreviewOuterWidthBase = 40;
        private const int SettingsColorPreviewOuterHeightBase = 20;
        private const int SettingsColorPreviewInnerXOffsetBase = 2;
        private const int SettingsColorPreviewInnerYOffsetBase = 2;
        private const int SettingsColorPreviewInnerWidthBase = 36;
        private const int SettingsColorPreviewInnerHeightBase = 16;
        private const int SettingsCheckboxXOffsetBase = 420;
        private const int SettingsCheckboxYOffsetBase = 60;
        private const int SettingsCheckboxSizeBase = 30;
        private const int SettingsThemeHoverXOffsetBase = -8;
        private const int SettingsThemeHoverYOffsetBase = 3;
        private const int SettingsThemeHoverWidthBase = 420;
        private const int SettingsThemeHoverHeightBase = 44;

        private void InitSettingApp()
        {
            phoneSoundList = new List<string>
            {
                "getNewSpecialItem", "crystal", "phone", "achievement", "cacklingWitch",
                "dog_bark", "Duck", "cat", "explosion", "goldenWalnut", "machine_bell",
                "Meteorite", "thunder", "UFO", "yoba"
            };

            phoneTextColorList = new List<string>
            {
                "Black", "Red", "Green", "Blue", "Yellow", "Orange", "Purple", "White"
            };

            phoneTextColorMap["Black"] = Color.Black;
            phoneTextColorMap["Red"] = Color.Red;
            phoneTextColorMap["Green"] = Color.Green;
            phoneTextColorMap["Blue"] = Color.Blue;
            phoneTextColorMap["Yellow"] = Color.Yellow;
            phoneTextColorMap["Orange"] = Color.Orange;
            phoneTextColorMap["Purple"] = Color.MediumPurple;
            phoneTextColorMap["White"] = Color.White;
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
                SettingMenuThemeComponentListState => currentSelectedThemeComponent switch
                {
                    "phone" => "Phone",
                    "app_appstore" => "AppStore",
                    "app_calendar" => "Calendar",
                    "app_camera" => "Camera",
                    "app_notification" => "Notification",
                    "app_photo" => "Photo",
                    "app_setting" => "Settings",
                    _ => GetFriendlyComponentName(currentSelectedThemeComponent)
                },
                _ => ModEntry.SHelper.Translation.Get("ui.setting.title")
            };

            DrawPhoneText(
                b,
                Game1.dialogueFont,
                title,
                new Vector2(PhoneX(SettingsTitleXOffsetBase), PhoneY(SettingsTitleYOffsetBase)),
                Color.Black,
                localScale: 1f * PhoneUiScale);

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
                DrawThemeComponentOptionsList(b);
                return;
            }

            if (currentSettingMenuState == SettingMenuThemeComponentListState)
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
            themeComponentRowBounds.Clear();

            int yStart = PhoneY(SettingsMainOptionsStartYBase);
            DrawSettingOptionRow(b, SettingMenuOptionTextColor, ModEntry.SHelper.Translation.Get("ui.setting.text_color"), yStart);
            DrawSettingOptionRow(b, SettingMenuOptionSound, ModEntry.SHelper.Translation.Get("ui.setting.sound"), yStart + mainSpacing);
            DrawSettingOptionRow(b, SettingMenuOptionTheme, ModEntry.SHelper.Translation.Get("ui.setting.theme"), yStart + mainSpacing * 2);
            DrawSettingOptionRow(b, SettingMenuOptionPhoneSetting, ModEntry.SHelper.Translation.Get("ui.setting.phone_setting"), yStart + mainSpacing * 3);
        }

        private void DrawSettingOptionRow(SpriteBatch b, string optionId, string displayText, int rowY)
        {
            int rowX = PhoneX(SettingsOptionRowXOffsetBase);
            int rowWidth = Math.Max(1, ScaleUiValue(SettingsOptionRowWidthBase));
            int rowHeight = Math.Max(1, ScaleUiValue(SettingsOptionRowHeightBase));

            Rectangle rowBounds = new Rectangle(rowX, rowY, rowWidth, rowHeight);
            settingOptionBounds[optionId] = rowBounds;

            Textures.DrawCard(
                b,
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
                Color.Black,
                localScale: 0.85f * PhoneUiScale);

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

        private void DrawThemeComponentOptionsList(SpriteBatch b)
        {
            themeComponentRowBounds.Clear();
            settingOptionBounds.Clear();
            phoneThemeButton.Clear();

            int yStart = PhoneY(SettingsListStartYBase);
            int itemSpacing = listSpacing;

            b.End();
            Rectangle settingsClipRect = new Rectangle(
                GetPhoneContentBounds().X,
                PhoneY(SettingsListStartYBase),
                GetPhoneContentBounds().Width,
                ScaleUiValue(665)
            );
            Rectangle previousScissorRect = Game1.graphics.GraphicsDevice.ScissorRectangle;
            Game1.graphics.GraphicsDevice.ScissorRectangle = Rectangle.Intersect(settingsClipRect, Game1.graphics.GraphicsDevice.Viewport.Bounds);

            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState() { ScissorTestEnable = true });

            int visibleTop = settingsClipRect.Top - ScrollDrawOverscan;
            int visibleBottom = settingsClipRect.Bottom + ScrollDrawOverscan;

            var themeKeys = GetThemeComponentKeys();
            for (int i = 0; i < themeKeys.Count; i++)
            {
                string compKey = themeKeys[i];
                int y = yStart + i * itemSpacing - (int)MathF.Floor(settingScrollOffset);

                if (y + itemSpacing < visibleTop) continue;
                if (y > visibleBottom) break;

                int nameX = PhoneX(SettingsListNameXOffsetBase);
                string friendlyName = compKey switch
                {
                    "phone" => "Phone",
                    "app_appstore" => "AppStore",
                    "app_calendar" => "Calendar",
                    "app_camera" => "Camera",
                    "app_notification" => "Notification",
                    "app_photo" => "Photo",
                    "app_setting" => "Settings",
                    _ => GetFriendlyComponentName(compKey)
                };

                Rectangle rowBounds = new Rectangle(nameX - ScaleUiValue(10), y + ScaleUiValue(54), ScaleUiValue(380), ScaleUiValue(55));
                themeComponentRowBounds[compKey] = rowBounds;

                Textures.DrawCard(
                    b,
                    rowBounds.X,
                    rowBounds.Y,
                    rowBounds.Width,
                    rowBounds.Height,
                    Color.White,
                    1f,
                    false);

                DrawPhoneText(
                    b,
                    Game1.smallFont,
                    friendlyName,
                    new Vector2(nameX + ScaleUiValue(10), y + ScaleUiValue(SettingsListNameYOffsetBase)),
                    Color.Black);

                string activeTheme = AssetHelper.GetComponentTheme(compKey);
                Vector2 sizeText = Game1.smallFont.MeasureString(activeTheme) * GetPhoneTextScale();
                DrawPhoneText(
                    b,
                    Game1.smallFont,
                    activeTheme,
                    new Vector2(rowBounds.Right - sizeText.X - ScaleUiValue(15), y + ScaleUiValue(SettingsListNameYOffsetBase)),
                    Color.DarkBlue * 0.7f);
            }

            b.End();
            Game1.graphics.GraphicsDevice.ScissorRectangle = previousScissorRect;
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
        }

        private void DrawSoundSettingList(SpriteBatch b)
        {
            phoneSoundButton.Clear();
            phoneTextColorButton.Clear();
            phoneThemeButton.Clear();
            phoneThemeHoverBounds.Clear();
            settingOptionBounds.Clear();

            int yStart = PhoneY(SettingsListStartYBase);
            int itemSpacing = listSpacing;

            b.End();
            Rectangle settingsClipRect = new Rectangle(
                GetPhoneContentBounds().X,
                PhoneY(SettingsListStartYBase),
                GetPhoneContentBounds().Width,
                ScaleUiValue(665)
            );
            Rectangle previousScissorRect = Game1.graphics.GraphicsDevice.ScissorRectangle;
            Game1.graphics.GraphicsDevice.ScissorRectangle = Rectangle.Intersect(settingsClipRect, Game1.graphics.GraphicsDevice.Viewport.Bounds);

            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState() { ScissorTestEnable = true });

            int visibleTop = settingsClipRect.Top - ScrollDrawOverscan;
            int visibleBottom = settingsClipRect.Bottom + ScrollDrawOverscan;

            for (int i = 0; i < phoneSoundList.Count; i++)
            {
                string soundString = phoneSoundList[i];
                int y = yStart + i * itemSpacing - (int)MathF.Floor(settingScrollOffset);

                if (y + itemSpacing < visibleTop)
                    continue;
                if (y > visibleBottom)
                    break;

                int nameX = PhoneX(SettingsListNameXOffsetBase);
                DrawPhoneText(
                    b,
                    Game1.smallFont,
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
                    scale: ScaleUiValue(4.3f)
                );

                phoneSoundButton[soundString] = selectButton;
                selectButton.draw(b);
            }

            b.End();
            Game1.graphics.GraphicsDevice.ScissorRectangle = previousScissorRect;
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
        }

        private void DrawTextColorSettingList(SpriteBatch b)
        {
            phoneTextColorButton.Clear();
            phoneSoundButton.Clear();
            phoneThemeButton.Clear();
            phoneThemeHoverBounds.Clear();
            settingOptionBounds.Clear();

            int yStart = PhoneY(SettingsListStartYBase);
            int itemSpacing = listSpacing;

            b.End();
            Rectangle settingsClipRect = new Rectangle(
                GetPhoneContentBounds().X,
                PhoneY(SettingsListStartYBase),
                GetPhoneContentBounds().Width,
                ScaleUiValue(665)
            );
            Rectangle previousScissorRect = Game1.graphics.GraphicsDevice.ScissorRectangle;
            Game1.graphics.GraphicsDevice.ScissorRectangle = Rectangle.Intersect(settingsClipRect, Game1.graphics.GraphicsDevice.Viewport.Bounds);

            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState() { ScissorTestEnable = true });

            int visibleTop = settingsClipRect.Top - ScrollDrawOverscan;
            int visibleBottom = settingsClipRect.Bottom + ScrollDrawOverscan;

            for (int i = 0; i < phoneTextColorList.Count; i++)
            {
                string colorName = phoneTextColorList[i];
                int y = yStart + i * itemSpacing - (int)MathF.Floor(settingScrollOffset);

                if (y + itemSpacing < visibleTop)
                    continue;
                if (y > visibleBottom)
                    break;

                int nameX = PhoneX(SettingsListNameXOffsetBase);
                DrawPhoneText(
                    b,
                    Game1.smallFont,
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
                    scale: ScaleUiValue(4.3f)
                );

                phoneTextColorButton[colorName] = selectButton;
                selectButton.draw(b);
            }

            b.End();
            Game1.graphics.GraphicsDevice.ScissorRectangle = previousScissorRect;
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
        }

        private void DrawThemeSettingList(SpriteBatch b)
        {
            phoneThemeButton.Clear();
            phoneThemeHoverBounds.Clear();
            phoneSoundButton.Clear();
            phoneTextColorButton.Clear();
            settingOptionBounds.Clear();

            phoneThemeList = AssetHelper.GetAvailableThemeNamesForComponent(currentSelectedThemeComponent);
            if (phoneThemeList.Count == 0)
                phoneThemeList = new List<string> { AssetHelper.DefaultPhoneThemeName };

            int yStart = PhoneY(SettingsListStartYBase);
            int itemSpacing = listSpacing;

            b.End();
            Rectangle settingsClipRect = new Rectangle(
                GetPhoneContentBounds().X,
                PhoneY(SettingsListStartYBase),
                GetPhoneContentBounds().Width,
                ScaleUiValue(665)
            );
            Rectangle previousScissorRect = Game1.graphics.GraphicsDevice.ScissorRectangle;
            Game1.graphics.GraphicsDevice.ScissorRectangle = Rectangle.Intersect(settingsClipRect, Game1.graphics.GraphicsDevice.Viewport.Bounds);

            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState() { ScissorTestEnable = true });

            int visibleTop = settingsClipRect.Top - ScrollDrawOverscan;
            int visibleBottom = settingsClipRect.Bottom + ScrollDrawOverscan;

            for (int i = 0; i < phoneThemeList.Count; i++)
            {
                string themeName = phoneThemeList[i];
                int y = yStart + i * itemSpacing - (int)MathF.Floor(settingScrollOffset);
                string themeReadmeText = GetThemeReadmeTooltipText(themeName);

                if (y + itemSpacing < visibleTop)
                    continue;
                if (y > visibleBottom)
                    break;

                int nameX = PhoneX(SettingsListNameXOffsetBase);
                DrawPhoneText(
                    b,
                    Game1.smallFont,
                    themeName,
                    new Vector2(nameX, y + ScaleUiValue(SettingsListNameYOffsetBase)),
                    Color.Black);
                phoneThemeHoverBounds[themeName] = new Rectangle(
                    nameX + ScaleUiValue(SettingsThemeHoverXOffsetBase),
                    y + ScaleUiValue(SettingsThemeHoverYOffsetBase),
                    Math.Max(1, ScaleUiValue(SettingsThemeHoverWidthBase)),
                    Math.Max(1, ScaleUiValue(SettingsThemeHoverHeightBase)));

                Rectangle rect = new Rectangle(218, 428, 7, 7);
                if (string.Equals(AssetHelper.GetComponentTheme(currentSelectedThemeComponent), themeName, StringComparison.OrdinalIgnoreCase))
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
                    scale: ScaleUiValue(4.3f)
                );

                phoneThemeButton[themeName] = selectButton;
                selectButton.draw(b);
            }

            b.End();
            Game1.graphics.GraphicsDevice.ScissorRectangle = previousScissorRect;
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
        }

        private void DrawThemeSettingTooltipIfHovered(SpriteBatch b)
        {
            if (phoneThemeHoverBounds.Count == 0)
                return;

            int mouseX = Game1.getMouseX();
            int mouseY = Game1.getMouseY();

            Rectangle settingsClipRect = new Rectangle(
                GetPhoneContentBounds().X,
                PhoneY(SettingsListStartYBase),
                GetPhoneContentBounds().Width,
                ScaleUiValue(665)
            );
            if (!settingsClipRect.Contains(mouseX, mouseY))
                return;

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

            string cacheKey = $"{currentSelectedThemeComponent}_{safeThemeName}";
            if (phoneThemeReadmeCache.TryGetValue(cacheKey, out string? cachedText))
                return cachedText;

            string tooltipText = "";

            try
            {
                string? modFolderPath = ModEntry.Instance?.Helper?.DirectoryPath ?? ModEntry.SHelper?.DirectoryPath;
                if (!string.IsNullOrWhiteSpace(modFolderPath))
                {
                    string readmePath = Path.Combine(modFolderPath, AssetHelper.GetPhoneThemesRootPath(), currentSelectedThemeComponent, safeThemeName, ThemeReadmeFileName);
                    if (File.Exists(readmePath))
                        tooltipText = (File.ReadAllText(readmePath) ?? "").Trim();
                }
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor.Log($"Failed to load theme readme for '{safeThemeName}': {ex.Message}", LogLevel.Trace);
            }

            phoneThemeReadmeCache[cacheKey] = tooltipText;
            return tooltipText;
        }

        private void RefreshPhoneThemeList()
        {
            phoneThemeList = AssetHelper.GetAvailableThemeNamesForComponent(currentSelectedThemeComponent);
            if (phoneThemeList.Count == 0)
                phoneThemeList = new List<string> { AssetHelper.DefaultPhoneThemeName };
        }

        private void ApplyPhoneThemeSelection(string themeName)
        {
            AssetHelper.SetComponentTheme(currentSelectedThemeComponent, themeName);
            Textures.LoadTextures();
            ModEntry.currentPhoneTheme = AssetHelper.GetComponentTheme("phone");
            ReloadThemeTextures();
        }

        public void ReloadThemeTextures()
        {
            textWrapCache.Clear();

            texturePhoneBackground = Textures.PhoneBackground;
            texturePhoneCapture = Textures.PhoneEmpty;
            texturePortraitBackground = Textures.Background;

            textureAppCamera = Textures.AppCamera;
            textureAppPhoto = Textures.AppPhoto;
            textureAppSetting = Textures.AppSetting;
            textureAppNotification = Textures.AppNotification;
            textureAppAppStore = Textures.AppAppStore;
            textureAppCalendar = Textures.AppCalendar;
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

        private void ReceiveLeftClickSettingApp(int x, int y)
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
                    settingScrollOffset = 0f;
                    settingScrollTarget = 0f;
                    Game1.playSound("smallSelect");
                    return;
                }
            }
            else if (currentSettingMenuState == SettingMenuThemeState)
            {
                foreach (var kv in themeComponentRowBounds)
                {
                    if (kv.Value.Contains(x, y))
                    {
                        currentSelectedThemeComponent = kv.Key;
                        currentSettingMenuState = SettingMenuThemeComponentListState;
                        settingScrollOffset = 0f;
                        settingScrollTarget = 0f;
                        Game1.playSound("smallSelect");
                        return;
                    }
                }
            }
            else
            {
                Rectangle settingsClipRect = new Rectangle(
                    GetPhoneContentBounds().X,
                    PhoneY(SettingsListStartYBase),
                    GetPhoneContentBounds().Width,
                    ScaleUiValue(665)
                );
                if (!settingsClipRect.Contains(x, y))
                    return;

                if (currentSettingMenuState == SettingMenuSoundState)
                {
                    foreach (var button in phoneSoundButton.Values)
                    {
                        if (!button.containsPoint(x, y))
                            continue;

                        DelayedAction.playSoundAfterDelay(button.name, 0);
                        DelayedAction.playSoundAfterDelay(button.name, 1500);
                        ModEntry.currentPhoneSound = button.name;

                        AssetHelper.SaveSettings(); // <--- Add this line here
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

                        AssetHelper.SaveSettings(); // <--- Add this line here
                        Game1.playSound("smallSelect");
                        return;
                    }
                }
                else if (currentSettingMenuState == SettingMenuThemeComponentListState)
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
        }

        private void ReceiveScrollWheelActionSettingApp(int direction)
        {
            float wheelSteps = direction / 120f;
            float maxScroll = GetSettingMaxScroll();
            settingScrollTarget = Math.Clamp(
                settingScrollTarget - wheelSteps * ChatScrollPixelsPerWheelNotch,
                0f,
                maxScroll);
            settingScrollOffset = Math.Clamp(settingScrollOffset, 0f, maxScroll);
        }

        private void ApplyTouchScrollDeltaSettingApp(int pixelDelta)
        {
            float maxScroll = GetSettingMaxScroll();
            settingScrollTarget = Math.Clamp(settingScrollTarget + pixelDelta, 0f, maxScroll);
        }

        private float GetSettingMaxScroll()
        {
            int itemCount = currentSettingMenuState switch
            {
                SettingMenuSoundState => phoneSoundList.Count,
                SettingMenuTextColorState => phoneTextColorList.Count,
                SettingMenuThemeState => GetThemeComponentKeys().Count,
                SettingMenuThemeComponentListState => phoneThemeList.Count,
                _ => 0
            };

            int totalHeight = itemCount * listSpacing;
            int viewportHeight = ScaleUiValue(665);
            return Math.Max(0f, totalHeight - viewportHeight);
        }

        private void UpdateSettingApp(GameTime time)
        {
            float lerpAmount = (float)(time.ElapsedGameTime.TotalSeconds * ChatScrollLerpSpeed);
            lerpAmount = Math.Clamp(lerpAmount, 0f, 1f);

            float maxScroll = GetSettingMaxScroll();
            settingScrollTarget = Math.Clamp(settingScrollTarget, 0f, maxScroll);
            settingScrollOffset = MathHelper.Lerp(settingScrollOffset, settingScrollTarget, lerpAmount);

            if (Math.Abs(settingScrollOffset - settingScrollTarget) <= 0.5f)
                settingScrollOffset = settingScrollTarget;
        }

        private bool HandleSettingAppBackButton()
        {
            if (currentSettingMenuState == SettingMenuThemeComponentListState)
            {
                currentSettingMenuState = SettingMenuThemeState;
                settingScrollOffset = 0f;
                settingScrollTarget = 0f;
                return true;
            }
            if (currentSettingMenuState != SettingMenuMainState)
            {
                currentSettingMenuState = SettingMenuMainState;
                settingScrollOffset = 0f;
                settingScrollTarget = 0f;
                return true;
            }
            currentApp = null;
            return true;
        }
    }
}