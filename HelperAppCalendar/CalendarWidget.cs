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
                float baseTextScale = GetPhoneTextScale();
                float textScaleDate = GetPhoneTextScale(0.90f);
                float textScaleSub = GetPhoneTextScale(0.8f);
                float textScaleEvent = GetPhoneTextScale(0.7f);

                float spacing = 3f * baseTextScale;
                float paddingRight = 22f * baseTextScale;

                float portraitSize = 42f * baseTextScale;
                float portraitSpacing = 6f * baseTextScale;

                // 5. Measure total layout block height dynamically to determine vertical centering
                float totalBlockHeight = 0f;
                totalBlockHeight += Game1.smallFont.MeasureString(cachedLine1).Y * textScaleDate;
                totalBlockHeight += spacing;
                totalBlockHeight += Game1.smallFont.MeasureString(cachedLine2).Y * textScaleSub;

                bool hasEvent = !string.IsNullOrEmpty(cachedEventText);
                string? fullEventLine = hasEvent ? $"{cachedEventText}" : null;
                if (hasEvent && fullEventLine != null)
                {
                    totalBlockHeight += spacing;
                    totalBlockHeight += Game1.smallFont.MeasureString(fullEventLine).Y * textScaleEvent;
                }

                bool hasBirthdays = cachedBirthdayNPCs.Count > 0;
                if (hasBirthdays)
                {
                    totalBlockHeight += spacing;
                    totalBlockHeight += portraitSize;
                }

                // 6. Execute drawing via a precise running-Y coordinate anchor
                float currentY = rect.Y + (rect.Height - totalBlockHeight) / 2f;

                // Line 1: Day & Date Row
                float posX1 = rect.Right - (Game1.smallFont.MeasureString(cachedLine1).X * textScaleDate) - paddingRight;
                DrawShadowedText(b, Game1.smallFont, cachedLine1, new Vector2(posX1, currentY), Color.Black, Color.White * 0.6f, textScaleDate);
                currentY += (Game1.smallFont.MeasureString(cachedLine1).Y * textScaleDate) + spacing;

                // Line 2: Season & Year Row
                float posX2 = rect.Right - (Game1.smallFont.MeasureString(cachedLine2).X * textScaleSub) - paddingRight;
                DrawShadowedText(b, Game1.smallFont, cachedLine2, new Vector2(posX2, currentY), Color.Black, Color.White * 0.6f, textScaleSub);
                currentY += (Game1.smallFont.MeasureString(cachedLine2).Y * textScaleSub) + spacing;

                // Line 3: Custom Event Row
                if (hasEvent && fullEventLine != null)
                {
                    float posXEvent = rect.Right - (Game1.smallFont.MeasureString(fullEventLine).X * textScaleEvent) - paddingRight;
                    DrawShadowedText(b, Game1.smallFont, fullEventLine, new Vector2(posXEvent, currentY), new Color(150, 20, 20), Color.White * 0.6f, textScaleEvent);
                    currentY += (Game1.smallFont.MeasureString(fullEventLine).Y * textScaleEvent) + spacing;
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
    }
}