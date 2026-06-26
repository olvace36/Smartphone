using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace Smartphone
{
    public partial class PhoneMenu
    {
        // --- Cached Data Fields ---
        private static string cachedLine1 = "";
        private static string cachedLine2 = "";
        private static string? cachedEventText = null;
        private static readonly List<NPC> cachedBirthdayNPCs = new();

        /// <summary>
        /// Gathers all calendar, festival, and birthday data. 
        /// Call this once per day or when a save is loaded.
        /// </summary>
        public static void RefreshCalendarData()
        {
            try
            {
                // 1. Fetch current translated Stardew calendar values
                string seasonName = Utility.getSeasonNameFromNumber(Utility.getSeasonNumber(Game1.currentSeason));
                string dayOfWeek = Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth);

                cachedLine1 = $"{dayOfWeek}. {Game1.dayOfMonth}";
                cachedLine2 = $"{seasonName}, Year {Game1.year}";

                // 2. Discover if any event or festival is active today
                cachedEventText = null;
                if (Utility.isFestivalDay(Game1.dayOfMonth, Game1.season))
                {
                    try
                    {
                        var data = Game1.content.Load<Dictionary<string, string>>("Data\\Festivals\\" + Game1.currentSeason + Game1.dayOfMonth);
                        if (data != null)
                        {
                            foreach (var kvp in data)
                            {
                                if (!string.IsNullOrEmpty(kvp.Value))
                                {
                                    string[] split = kvp.Value.Split('/');
                                    if (split.Length > 0 && !string.IsNullOrEmpty(split[0]))
                                    {
                                        cachedEventText = split[0];
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch { }

                    if (string.IsNullOrEmpty(cachedEventText))
                    {
                        cachedEventText = "Festival day";
                    }
                }
                else if (Game1.currentSeason == "fall" && Game1.dayOfMonth == 16)
                {
                    cachedEventText = "Stardew Fair";
                }
                else if (Game1.currentSeason == "winter" && Game1.dayOfMonth >= 15 && Game1.dayOfMonth <= 17)
                {
                    cachedEventText = "Night Market";
                }
                else if (Game1.currentSeason == "spring" && Game1.dayOfMonth >= 15 && Game1.dayOfMonth <= 17)
                {
                    cachedEventText = "Desert Festival";
                }
                else if (Game1.currentSeason == "summer" && Game1.dayOfMonth >= 20 && Game1.dayOfMonth <= 21)
                {
                    cachedEventText = "Trout Derby";
                }
                else if (Game1.currentSeason == "winter" && Game1.dayOfMonth >= 9 && Game1.dayOfMonth <= 10)
                {
                    cachedEventText = "SquidFest";
                }

                // 3. Scan for any active villager birthdays today
                cachedBirthdayNPCs.Clear();
                foreach (NPC npc in Utility.getAllVillagers())
                {
                    if (npc != null && npc.isBirthday())
                    {
                        if (!cachedBirthdayNPCs.Contains(npc) && npc.Portrait != null)
                        {
                            cachedBirthdayNPCs.Add(npc);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor?.Log($"Error refreshing calendar data data: {ex.Message}", StardewModdingAPI.LogLevel.Error);
            }
        }

        /// <summary>Draws right-aligned calendar details inside the 4x2 widget space.</summary>
        private void DrawCalendarWidget4x2(SpriteBatch b, Rectangle rect)
        {
            try
            {
                // 4. Compute UI-scale adaptive metrics using native font scale limits
                float baseTextScale = GetPhoneTextScale() * phoneUiScale;
                float textScaleDate = GetPhoneTextScale(0.90f) * phoneUiScale;
                float textScaleSub = GetPhoneTextScale(0.8f) * phoneUiScale;
                float textScaleEvent = GetPhoneTextScale(0.7f) * phoneUiScale;

                float spacing = 3f * baseTextScale;
                float paddingRight = 22f * baseTextScale;

                float portraitSize = 42f * baseTextScale;
                float portraitSpacing = 6f * baseTextScale;

                // --- Word Wrap Logic for Long Festival/Event Names ---
                List<string> eventLines = new List<string>();
                bool hasEvent = !string.IsNullOrEmpty(cachedEventText);

                if (hasEvent && cachedEventText != null)
                {
                    if (cachedEventText.Length < 25)
                    {
                        eventLines.Add(cachedEventText);
                    }
                    else
                    {
                        string[] words = cachedEventText.Split(' ');
                        string currentLine = "";
                        foreach (string word in words)
                        {
                            if (currentLine.Length == 0)
                            {
                                currentLine = word;
                            }
                            // Wrap if adding the next word exceeds 25 characters
                            else if (currentLine.Length + 1 + word.Length <= 25)
                            {
                                currentLine += " " + word;
                            }
                            else
                            {
                                eventLines.Add(currentLine);
                                currentLine = word;
                            }
                        }
                        if (!string.IsNullOrEmpty(currentLine))
                        {
                            eventLines.Add(currentLine);
                        }
                    }
                }

                // 5. Measure total layout block height dynamically to determine vertical centering
                float totalBlockHeight = 0f;
                totalBlockHeight += Game1.smallFont.MeasureString(cachedLine1).Y * textScaleDate;
                totalBlockHeight += spacing;
                totalBlockHeight += Game1.smallFont.MeasureString(cachedLine2).Y * textScaleSub;

                // Account for every line generated by the wrapped event text
                foreach (string line in eventLines)
                {
                    totalBlockHeight += spacing;
                    totalBlockHeight += Game1.smallFont.MeasureString(line).Y * textScaleEvent;
                }

                bool hasBirthdays = cachedBirthdayNPCs.Count > 0;
                if (hasBirthdays)
                {
                    totalBlockHeight += spacing;
                    totalBlockHeight += portraitSize;
                }

                // 6. Execute drawing via a precise running-Y coordinate anchor
                float currentY = rect.Y + (rect.Height - totalBlockHeight) / 2f;

                // --- Base Widget Backdrop Texture Loading & Weather Tinting ---
                Texture2D widgetTex = Textures.GetAppTexture("builtin:calendar", AppSize.Size4x2);
                if (widgetTex != null)
                {
                    b.Draw(widgetTex, rect, Color.White);
                }

                // Render animated elements and overlays behind typography
                DrawWidgetWeatherOverlay(b, rect, widgetTex);

                // Line 1: Day & Date Row
                float posX1 = rect.Right - (Game1.smallFont.MeasureString(cachedLine1).X * textScaleDate) - paddingRight;
                DrawShadowedText(b, Game1.smallFont, cachedLine1, new Vector2(posX1, currentY), Color.Black, Color.White * 0.6f, textScaleDate);
                currentY += (Game1.smallFont.MeasureString(cachedLine1).Y * textScaleDate) + spacing;

                // Line 2: Season & Year Row
                float posX2 = rect.Right - (Game1.smallFont.MeasureString(cachedLine2).X * textScaleSub) - paddingRight;
                DrawShadowedText(b, Game1.smallFont, cachedLine2, new Vector2(posX2, currentY), Color.Black, Color.White * 0.6f, textScaleSub);
                currentY += (Game1.smallFont.MeasureString(cachedLine2).Y * textScaleSub) + spacing;

                // Line 3: Custom Wrapped Event Row(s)
                foreach (string line in eventLines)
                {
                    float posXEvent = rect.Right - (Game1.smallFont.MeasureString(line).X * textScaleEvent) - paddingRight;
                    DrawShadowedText(b, Game1.smallFont, line, new Vector2(posXEvent, currentY), new Color(150, 20, 20), Color.White * 0.6f, textScaleEvent);
                    currentY += (Game1.smallFont.MeasureString(line).Y * textScaleEvent) + spacing;
                }

                // Line 4: Birthday Portrait Row
                if (hasBirthdays)
                {
                    float totalRowWidth = (portraitSize * cachedBirthdayNPCs.Count) + (portraitSpacing * (cachedBirthdayNPCs.Count - 1));
                    float portraitStartX = rect.Right - totalRowWidth - paddingRight;

                    for (int i = 0; i < cachedBirthdayNPCs.Count; i++)
                    {
                        NPC npc = cachedBirthdayNPCs[i];
                        Rectangle destRect = new Rectangle(
                            (int)MathF.Floor(portraitStartX + i * (portraitSize + portraitSpacing)),
                            (int)MathF.Floor(currentY),
                            (int)MathF.Floor(portraitSize),
                            (int)MathF.Floor(portraitSize)
                        );

                        b.Draw(npc.Portrait, destRect, new Rectangle(0, 0, 64, 64), Color.White);
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor?.Log($"Error rendering 4x2 calendar widget content: {ex.Message}", StardewModdingAPI.LogLevel.Trace);
            }
        }

        /// <summary> Draws an animated weather backing layout entirely bounded within the widget space. </summary>
        private void DrawWidgetWeatherOverlay(SpriteBatch b, Rectangle rect, Texture2D? widgetTex)
        {
            try
            {
                if (Game1.staminaRect == null || widgetTex == null || Game1.mouseCursors == null) return;

                int timeMs = (int)Game1.currentGameTime.TotalGameTime.TotalMilliseconds;
                float uiScale = GetPhoneTextScale();

                // 1. Green Rain (Special Storm Weather)
                if (Game1.isGreenRain)
                {
                    b.Draw(widgetTex, rect, new Color(30, 160, 60, 110));

                    // Distributed pseudo-randomized green rain streaks (/) - Smooth reduced velocity
                    for (int i = 0; i < 12; i++)
                    {
                        float speed = 0.20f + ((i * 23) % 5) * 0.04f;
                        int xOffset = (i * 67) % rect.Width;
                        int yOffset = (i * 109) % rect.Height;

                        int posX = rect.X + (int)((xOffset + timeMs * 0.06f * speed) % rect.Width);
                        int posY = rect.Y + (int)((yOffset + timeMs * 0.24f * speed) % rect.Height);

                        b.Draw(Game1.staminaRect,
                               new Rectangle(posX, posY, 3, 20),
                               null,
                               Color.LimeGreen * 0.8f,
                               0.28f,
                               Vector2.Zero,
                               SpriteEffects.None,
                               0f);
                    }
                }
                // 2. Lightning / Storm Days
                else if (Game1.isLightning)
                {
                    b.Draw(widgetTex, rect, new Color(25, 30, 55, 150));

                    // Fast driving slanted storm lines - Smooth reduced velocity
                    for (int i = 0; i < 15; i++)
                    {
                        float speed = 0.25f + ((i * 13) % 6) * 0.06f;
                        int xOffset = (i * 71) % rect.Width;
                        int yOffset = (i * 131) % rect.Height;

                        int posX = rect.X + (int)((xOffset + timeMs * 0.09f * speed) % rect.Width);
                        int posY = rect.Y + (int)((yOffset + timeMs * 0.31f * speed) % rect.Height);

                        b.Draw(Game1.staminaRect,
                               new Rectangle(posX, posY, 3, 24),
                               null,
                               new Color(40, 95, 230) * 0.8f,
                               0.32f,
                               Vector2.Zero,
                               SpriteEffects.None,
                               0f);
                    }

                    // Lightning flash sequence
                    bool isFlash = (timeMs % 4500 < 120) && ((timeMs / 4500) % 2 == 0 || (timeMs / 4500) % 3 == 0);
                    if (isFlash)
                    {
                        b.Draw(widgetTex, rect, Color.White * 0.45f);
                    }
                }
                // 3. Standard Rain
                else if (Game1.isRaining)
                {
                    b.Draw(widgetTex, rect, new Color(40, 65, 110, 130));

                    // Randomized deep blue distinct rain lines - Speed increased by ~30%
                    for (int i = 0; i < 12; i++)
                    {
                        float speed = 0.19f + ((i * 19) % 5) * 0.04f;
                        int xOffset = (i * 53) % rect.Width;
                        int yOffset = (i * 127) % rect.Height;

                        // Increased multipliers from 0.07f and 0.22f
                        int posX = rect.X + (int)((xOffset + timeMs * 0.09f * speed) % rect.Width);
                        int posY = rect.Y + (int)((yOffset + timeMs * 0.29f * speed) % rect.Height);

                        b.Draw(Game1.staminaRect,
                               new Rectangle(posX, posY, 3, 18),
                               null,
                               new Color(30, 80, 210) * 0.75f,
                               0.28f,
                               Vector2.Zero,
                               SpriteEffects.None,
                               0f);
                    }
                }
                // 4. Winter Snow Days (Changed back to the clean, non-flickering old geometric way)
                else if (Game1.isSnowing)
                {
                    // Deep violet atmospheric backing mask overlay (leaves snow pixels highly visible)
                    b.Draw(widgetTex, rect, new Color(35, 40, 70, 110));

                    int snowParticleCount = 27; // Increased by 50% from 18

                    for (int i = 0; i < snowParticleCount; i++)
                    {
                        int xOffset = (i * 61) % rect.Width;
                        int yOffset = (i * 79) % rect.Height;

                        // Soft organic side-to-side drift motion calculations
                        float sway = MathF.Sin((timeMs * 0.0015f) + i * 2f) * 8f;
                        int posX = rect.X + (int)((xOffset + sway) % rect.Width);
                        if (posX < rect.X) posX += rect.Width;

                        // Steady gentle falling speed
                        int posY = rect.Y + (int)((yOffset + timeMs * 0.04f) % rect.Height);

                        // Alternate particle block size indices safely to eliminate animation/rotation flickering
                        int size = (i % 2 == 0) ? 3 : 4;

                        // Render stable solid white snowflakes
                        b.Draw(Game1.staminaRect, new Rectangle(posX, posY, size, size), Color.White * 0.85f);
                    }
                }
                // 5. Windy / Debris Weather (Drifting Leaves)
                else if (Game1.isDebrisWeather)
                {
                    b.Draw(widgetTex, rect, new Color(65, 75, 60, 95));

                    int srcY = 1184;
                    if (Game1.currentSeason == "summer") srcY = 1200;
                    else if (Game1.currentSeason == "fall") srcY = 1216;

                    int debrisParticleCount = 10;

                    for (int i = 0; i < debrisParticleCount; i++)
                    {
                        int xOffset = (i * 79) % rect.Width;
                        int yOffset = (i * 61) % rect.Height;

                        int posX = rect.X + (int)((xOffset + timeMs * 0.22f) % rect.Width);
                        int posY = rect.Y + (int)((yOffset + MathF.Sin(timeMs * 0.0025f + i) * 6f) % rect.Height);

                        int frame = (timeMs / 110 + i) % 4;
                        Rectangle sourceRect = new Rectangle(352 + (frame * 16), srcY, 16, 16);

                        float pScale = 1.50f * uiScale;
                        float rotation = (timeMs * 0.0025f) + i * 0.7f;

                        b.Draw(Game1.mouseCursors,
                               new Vector2(posX, posY),
                               sourceRect,
                               Color.White * 0.85f,
                               rotation,
                               new Vector2(8, 8),
                               pScale,
                               SpriteEffects.None,
                               0f);
                    }
                }
            }
            catch
            {
                // Fail silently to safeguard UI drawing cycle pipeline threads
            }
        }
    }
}