using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace Smartphone
{
    /// <summary>
    /// Partial class implementation handling the lock screen rendering, OS initialization,
    /// unlock animation, and weather summary display on the lock screen.
    /// </summary>
    public partial class PhoneMenu
    {
        #region Lock Screen State Fields

        /// <summary>Whether the phone is currently playing the sliding transition unlock animation.</summary>
        private bool lockScreenUnlockAnimating = false;

        /// <summary>Time elapsed since the start of the lock screen unlock animation, in seconds.</summary>
        private double lockScreenUnlockElapsedSeconds = 0d;

        /// <summary>Interactive touch/click boundaries for unlocking the phone screen.</summary>
        private Rectangle lockScreenTapBounds = Rectangle.Empty;

        /// <summary>Simulated operating system boot/initialization progress percentage (0-100).</summary>
        private int lockScreenInitializationProgressPercent = 0;

        /// <summary>Time elapsed since the start of the OS boot sequence, in seconds.</summary>
        private double lockScreenInitializationElapsedSeconds = 0d;

        /// <summary>Calculated timestamp for the next boot progress percentage increment.</summary>
        private double lockScreenInitializationNextTickSeconds = 0d;

        /// <summary>Soft corner cache for weather textures to match the UI style.</summary>
        private readonly Dictionary<int, Texture2D> lockScreenWeatherIconSoftCache = new();

        #endregion

        #region Lock Screen Constants

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

        /// <summary>Source rectangle for the Stardew Valley 1.6 green rain weather icon.</summary>
        private static readonly Rectangle LockScreenGreenRainWeatherIconSource = new Rectangle(243, 293, 12, 8);

        #endregion

        #region Lock Screen Drawing Methods

        /// <summary>
        /// Draws the main lock screen containing the current time, date, weather, and unlock hint.
        /// </summary>
        /// <param name="b">The active SpriteBatch to draw with.</param>
        /// <param name="xOffset">Horizontal offset used for screen transition animations.</param>
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

                    Textures.DrawCard(
                        b,
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

        /// <summary>
        /// Draws the black screen and progress loader for the OS boot sequence.
        /// </summary>
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

        /// <summary>
        /// Draws today's weather condition next to tomorrow's forecast on the lock screen.
        /// </summary>
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

        /// <summary>
        /// Renders a specific weather icon.
        /// </summary>
        private void DrawLockScreenWeatherIcon(SpriteBatch b, Vector2 position, int iconIndex)
        {
            Texture2D iconTexture = GetOrCreateLockScreenSoftWeatherIconTexture(iconIndex);
            b.Draw(iconTexture, position, Color.White);
        }

        #endregion

        #region Weather Icon Cache Utilities

        /// <summary>
        /// Resolves a weather icon texture, pulling it from cache or generating a rounded version.
        /// </summary>
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

        /// <summary>
        /// Generates a softened, rounded weather icon texture from a base tilesheet region.
        /// </summary>
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

        /// <summary>
        /// Computes corner feathering alpha value for creating rounded corner textures.
        /// </summary>
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

        /// <summary>
        /// Disposes all dynamically generated soft corner weather textures.
        /// </summary>
        private void DisposeLockScreenWeatherIconSoftCache()
        {
            foreach (Texture2D iconTexture in lockScreenWeatherIconSoftCache.Values)
            {
                if (iconTexture != null && !iconTexture.IsDisposed)
                    iconTexture.Dispose();
            }

            lockScreenWeatherIconSoftCache.Clear();
        }

        #endregion

        #region Weather Info Parsers

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

        /// <summary>
        /// Generates the localized, formatted date string displayed on the lock screen.
        /// </summary>
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

        #endregion

        #region Lock Screen Initialization Sequence

        /// <summary>
        /// Triggers the OS initialization and booting screen loader state.
        /// </summary>
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

        /// <summary>
        /// Calculates a random tick interval for increments in the OS loading progress.
        /// </summary>
        private static double GetNextLockScreenInitializationTickSeconds()
        {
            double minDelay = Math.Max(0.01d, LockScreenInitializationProgressIntervalMinSeconds);
            double maxDelay = Math.Max(minDelay, LockScreenInitializationProgressIntervalMaxSeconds);
            return minDelay + (Random.Shared.NextDouble() * (maxDelay - minDelay));
        }

        /// <summary>
        /// Logic updates simulating OS booting progress bar increases.
        /// </summary>
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

        #endregion

        #region Touch Interaction & Animations

        /// <summary>
        /// Dispatches tap inputs for unlocking the phone if clicked within content bounds.
        /// </summary>
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

        /// <summary>
        /// Returns the progress multiplier (0.0 to 1.0) of the unlock slide animation.
        /// </summary>
        private float GetLockScreenUnlockProgress()
        {
            if (!lockScreenUnlockAnimating || LockScreenUnlockDurationSeconds <= 0d)
                return 0f;

            return (float)Math.Clamp(lockScreenUnlockElapsedSeconds / LockScreenUnlockDurationSeconds, 0d, 1d);
        }

        /// <summary>
        /// Updates the unlock slide animation timing every frame.
        /// </summary>
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

        #endregion
    }
}
