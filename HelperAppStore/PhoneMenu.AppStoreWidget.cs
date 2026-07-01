using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace Smartphone
{
    public partial class PhoneMenu
    {
        private bool appStoreWidgetWasMenuOpen = false;
        private int appStoreWidgetDisplayMode = 0; // 0: new apps, 1: apps with newer version

        /// <summary>
        /// Renders widget layout for the AppStore application.
        /// Disables randomization if only one data type is available.
        /// </summary>
        public void DrawAppStoreWidget(SpriteBatch b, Rectangle rect, AppSize size)
        {
            if (size == AppSize.Size2x1)
            {
                try
                {
                    // 1. Scan the App Cache for Stats
                    int newAppsCount = 0;
                    int updatesCount = 0;

                    if (AppStoreManager.AllMods != null)
                    {
                        foreach (var mod in AppStoreManager.AllMods)
                        {
                            if (mod == null) continue;

                            // Check if app is new (published within 10 days)
                            if (DateTime.TryParse(mod.PublishedAt, out DateTime pubDate))
                            {
                                if ((DateTime.UtcNow - pubDate).TotalDays <= 10)
                                {
                                    newAppsCount++;
                                }
                            }

                            // Check if installed app has an update available
                            var modInfo = AppStoreManager.GetInstalledMod(mod.UniqueID, mod.UpdateKey);
                            if (modInfo != null)
                            {
                                string currentVersion = modInfo.Manifest.Version.ToString();
                                if (AppStoreManager.IsNewerVersion(currentVersion, mod.LatestVersion))
                                {
                                    updatesCount++;
                                }
                            }
                        }
                    }

                    // 2. Open Session Tracking & Base Randomization Choice
                    bool isCurrentlyOpen = Game1.activeClickableMenu is PhoneMenu;
                    if (isCurrentlyOpen && !appStoreWidgetWasMenuOpen)
                    {
                        // Default to random choice on initialization when both are present or empty
                        appStoreWidgetDisplayMode = Game1.random.Next(2);
                    }
                    appStoreWidgetWasMenuOpen = isCurrentlyOpen;

                    // 3. Conditional Type Enforcements (Disables Randomization if only 1 type has data)
                    if (newAppsCount == 0 && updatesCount > 0)
                    {
                        appStoreWidgetDisplayMode = 1; // Force "apps with newer version"
                    }
                    else if (updatesCount == 0 && newAppsCount > 0)
                    {
                        appStoreWidgetDisplayMode = 0; // Force "new apps"
                    }

                    // 4. Backdrop Rendering
                    Texture2D widgetTex = Textures.GetAppTexture("appStore", AppSize.Size2x1);
                    if (widgetTex != null)
                    {
                        b.Draw(widgetTex, rect, Color.White);
                    }

                    // 5. Construct Display Text
                    string text = "";
                    if (appStoreWidgetDisplayMode == 0)
                    {
                        text = ModEntry.SHelper.Translation.Get("ui.appstore.widget.new_apps", new { count = newAppsCount });
                    }
                    else
                    {
                        text = ModEntry.SHelper.Translation.Get("ui.appstore.widget.updates_available", new { count = updatesCount });
                    }
                    // 6. Layout Alignment (Right 50% Quadrant Box Bounds, shifted left slightly)
                    int shiftLeft = ScaleUiValue(13); // Shift bounds to the left to give more horizontal room
                    int rightHalfWidth = rect.Width / 2 + shiftLeft;
                    Rectangle rightHalfRect = new Rectangle(rect.X + rect.Width / 2 - shiftLeft, rect.Y, rightHalfWidth, rect.Height);

                    // Word wrap text comfortably inside the local bounds area padding
                    int paddingX = ScaleUiValue(8); // Reduce padding to allow more width
                    int unscaledMaxWidth = (int)Math.Max(1, (rightHalfWidth - paddingX * 2) / phoneUiScale);
                    int wrapWidth = GetPhoneScaledWrapWidth(unscaledMaxWidth, 0.8f);
                    string wrappedText = Game1.parseText(text, Game1.smallFont, wrapWidth);
                    string[] lines = wrappedText.Split('\n');

                    int lineHeight = GetPhoneScaledLineHeight(Game1.smallFont, 0.8f, 0);
                    float totalHeight = lines.Length * lineHeight;
                    float startY = rightHalfRect.Y + (rightHalfRect.Height - totalHeight) / 2f + ScaleUiValue(3);

                    // Render lines centered within the right quadrant zone
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i];
                        Vector2 lineSize = MeasurePhoneText(Game1.smallFont, line, 0.8f);
                        float startX = rightHalfRect.X + (rightHalfRect.Width - lineSize.X) / 2f;

                        DrawPhoneText(b, Game1.smallFont, line, new Vector2(startX, startY + i * lineHeight), Color.Black, 0.8f);
                    }
                }
                catch (Exception ex)
                {
                    ModEntry.SMonitor?.Log($"Error rendering 2x1 AppStore widget content: {ex.Message}", StardewModdingAPI.LogLevel.Trace);
                }
            }
        }

        public static int GetAppStoreBadgeCount()
        {
            try
            {
                int updatesCount = 0;
                if (AppStoreManager.AllMods != null)
                {
                    foreach (var mod in AppStoreManager.AllMods)
                    {
                        if (mod == null) continue;

                        var modInfo = AppStoreManager.GetInstalledMod(mod.UniqueID, mod.UpdateKey);
                        if (modInfo != null)
                        {
                            string currentVersion = modInfo.Manifest.Version.ToString();
                            if (AppStoreManager.IsNewerVersion(currentVersion, mod.LatestVersion))
                            {
                                updatesCount++;
                            }
                        }
                    }
                }
                return updatesCount;
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor?.Log($"Error calculating AppStore badge count: {ex.Message}", StardewModdingAPI.LogLevel.Trace);
                return 0;
            }
        }
    }
}