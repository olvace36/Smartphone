using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace Smartphone
{
    public partial class PhoneMenu : IClickableMenu
    {
        // App State Properties
        // App State Properties
        private int phoneAppCurrentTab = 0; // 0 = Contacts, 1 = Recents
        private int phoneAppContactsScroll = 0;
        private int phoneAppRecentsScroll = 0;
        private string phoneAppSearchQuery = ""; // Active search filter query string

        // Form & Configuration Navigation States
        private bool phoneAppClickHandledOnPress = false;

        // Details View State
        private bool phoneAppViewingContactDetail = false;
        private ContactItem phoneAppSelectedContactDetail = null;

        private List<RecentCall> phoneAppRecentCalls = new();
        private List<string> phoneAppFavoriteNumbers = new(); // Stores favorites by NPC Name
        internal static bool phoneAppDataLoaded = false;

        public class RecentCall
        {
            public string Name { get; set; } = "";
            public string TimeText { get; set; } = "";
        }

        public class Contact
        {
            public string Name { get; set; } = "";
        }

        public class ContactItem
        {
            public string Name { get; set; } = "";
            public bool IsNpc { get; set; }
        }

        private void EnsurePhoneAppDataLoaded()
        {
            if (phoneAppDataLoaded) return;
            phoneAppDataLoaded = true;

            UpdateNpcNumbers();

            try
            {
                string folderPath = Path.Combine(ModEntry.SHelper.DirectoryPath, "userdata", ModEntry.GetActiveSaveFolderName());
                string filePath = Path.Combine(folderPath, "phone_app_data.json");

                phoneAppRecentCalls.Clear();
                phoneAppFavoriteNumbers.Clear();

                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var data = Newtonsoft.Json.Linq.JObject.Parse(json);

                    bool migrated = false;

                    if (data["NpcPhoneList"] != null)
                    {
                        data.Remove("NpcPhoneList");
                        migrated = true;
                    }
                    if (data["Contacts"] != null)
                    {
                        data.Remove("Contacts");
                        migrated = true;
                    }
                    if (data["CustomContacts"] != null)
                    {
                        data.Remove("CustomContacts");
                        migrated = true;
                    }

                    if (data["FavoriteNumbers"] != null)
                    {
                        var rawFavorites = data["FavoriteNumbers"].ToObject<List<string>>() ?? new();
                        foreach (var fav in rawFavorites)
                        {
                            if (!int.TryParse(fav, out _) && !fav.StartsWith("0"))
                            {
                                phoneAppFavoriteNumbers.Add(fav);
                            }
                            else
                            {
                                migrated = true;
                            }
                        }
                    }

                    if (data["RecentCalls"] != null)
                    {
                        var rawCalls = data["RecentCalls"].ToObject<List<Newtonsoft.Json.Linq.JContainer>>() ?? new();
                        foreach (var callObj in rawCalls)
                        {
                            string rawName = callObj["Name"]?.ToString() ?? "";
                            string rawTime = callObj["TimeText"]?.ToString() ?? "";

                            if (!string.IsNullOrEmpty(rawName) && !int.TryParse(rawName, out _) && !rawName.StartsWith("0"))
                            {
                                phoneAppRecentCalls.Add(new RecentCall { Name = rawName, TimeText = rawTime });
                            }
                            else
                            {
                                migrated = true;
                            }
                        }

                        if (phoneAppRecentCalls.Count > 100)
                        {
                            phoneAppRecentCalls.RemoveRange(100, phoneAppRecentCalls.Count - 100);
                            migrated = true;
                        }
                    }

                    if (migrated)
                    {
                        Directory.CreateDirectory(folderPath);
                        var cleanData = new { RecentCalls = phoneAppRecentCalls, FavoriteNumbers = phoneAppFavoriteNumbers };
                        string cleanJson = Newtonsoft.Json.JsonConvert.SerializeObject(cleanData, Newtonsoft.Json.Formatting.Indented);
                        File.WriteAllText(filePath, cleanJson);
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor?.Log($"Error loading phone application data: {ex.Message}", LogLevel.Error);
            }
        }

        public List<Contact> GetContacts()
        {
            EnsurePhoneAppDataLoaded();
            var list = new List<Contact>();
            foreach (var npcName in ModEntry.ContactableNpcs)
            {
                list.Add(new Contact { Name = npcName });
            }
            return list;
        }

        private void SavePhoneAppData()
        {
            try
            {
                string folderPath = Path.Combine(ModEntry.SHelper.DirectoryPath, "userdata", ModEntry.GetActiveSaveFolderName());
                Directory.CreateDirectory(folderPath);
                string filePath = Path.Combine(folderPath, "phone_app_data.json");

                var data = new { RecentCalls = phoneAppRecentCalls, FavoriteNumbers = phoneAppFavoriteNumbers };
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(filePath, json);
                ModEntry.NotifyContactableNpcsChanged();
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor?.Log($"Error saving phone application data: {ex.Message}", LogLevel.Error);
            }
        }

        private List<ContactItem> GetSortedContacts()
        {
            EnsurePhoneAppDataLoaded();
            var list = new List<ContactItem>();

            foreach (var npcName in ModEntry.ContactableNpcs)
            {
                list.Add(new ContactItem
                {
                    Name = npcName,
                    IsNpc = true
                });
            }

            var sorted = list
                .OrderByDescending(c => phoneAppFavoriteNumbers.Contains(c.Name))
                .ThenBy(c =>
                {
                    string nameText = c.Name;
                    var character = Game1.getCharacterFromName(nameText);
                    if (character != null) nameText = character.displayName;
                    return nameText;
                }, StringComparer.OrdinalIgnoreCase).ToList();

            // Filter on-the-fly based on search box input query string if active
            if (phoneAppCurrentTab == 0 && !string.IsNullOrEmpty(phoneAppSearchQuery))
            {
                sorted = sorted.Where(c =>
                {
                    string nameText = c.Name;
                    var character = Game1.getCharacterFromName(nameText);
                    if (character != null) nameText = character.displayName;
                    return nameText.Contains(phoneAppSearchQuery, StringComparison.OrdinalIgnoreCase);
                }).ToList();
            }

            return sorted;
        }

        public void DrawPhoneApp(SpriteBatch b)
        {
            if (phoneAppViewingContactDetail)
            {
                DrawPhoneContactDetail(b);
                return;
            }

            EnsurePhoneAppDataLoaded();
            Rectangle bounds = GetPhoneContentBounds();
            float uiScale = ModEntry.GetActivePhoneUiScale();

            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.45f);
            DrawPhoneScreenBackground(b, 0, applyBackgroundImage: false);

            int headerHeight = ScaleUiValue(80);
            int tabHeight = ScaleUiValue(70);
            int contentY = bounds.Y + headerHeight + ScaleUiValue(5);
            int contentHeight = bounds.Height - headerHeight - tabHeight - ScaleUiValue(10);
            Rectangle clipArea = new Rectangle(bounds.X, contentY, bounds.Width, contentHeight);

            // --- HEADER TITLE PROCESSING ---
            string title = phoneAppCurrentTab == 0 ? ModEntry.SHelper.Translation.Get("ui.phone.contacts") : ModEntry.SHelper.Translation.Get("ui.phone.recents");

            if (phoneAppCurrentTab == 0)
            {
                Rectangle searchBoxRect = new Rectangle(bounds.X + ScaleUiValue(15), bounds.Y + ScaleUiValue(10), bounds.Width - ScaleUiValue(30), ScaleUiValue(60));
                Textures.DrawCard(b, searchBoxRect.X, searchBoxRect.Y, searchBoxRect.Width, searchBoxRect.Height, Color.White);

                string printableSearch = phoneAppSearchQuery;
                if ((DateTime.UtcNow.Millisecond / 500) % 2 == 0) printableSearch += "|";

                float searchScale = 0.9f * uiScale;
                Vector2 searchTxtSz = Game1.smallFont.MeasureString(printableSearch) * searchScale;
                b.DrawString(Game1.smallFont, printableSearch, new Vector2(searchBoxRect.X + ScaleUiValue(12), searchBoxRect.Y + (searchBoxRect.Height - searchTxtSz.Y) / 2), Color.Black, 0f, Vector2.Zero, searchScale, SpriteEffects.None, 1f);
            }
            else
            {
                float titleScale = 0.55f * uiScale;
                Vector2 titleSize = Game1.dialogueFont.MeasureString(title) * titleScale;
                b.DrawString(Game1.dialogueFont, title, new Vector2(bounds.X + (bounds.Width - titleSize.X) / 2, bounds.Y + (headerHeight - titleSize.Y) / 2), Color.Black, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 1f);
            }

            // Draw line divider under header
            b.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Y + headerHeight, bounds.Width, ScaleUiValue(2)), Color.LightGray);

            // --- BOTTOM TAB BAR LAYOUT ---
            Rectangle tabBackground = new Rectangle(bounds.X, bounds.Y + bounds.Height - tabHeight, bounds.Width, tabHeight);
            b.Draw(Game1.staminaRect, tabBackground, Color.Black * 0.08f);
            b.Draw(Game1.staminaRect, new Rectangle(bounds.X, tabBackground.Y, bounds.Width, ScaleUiValue(1)), Color.LightGray);

            int tabCellWidth = bounds.Width / 2;
            Color colorContacts = phoneAppCurrentTab == 0 ? Color.DodgerBlue : Color.DimGray;
            Color colorRecents = phoneAppCurrentTab == 1 ? Color.DodgerBlue : Color.DimGray;

            float tabScale = 0.85f * uiScale;
            string contactsLabel = ModEntry.SHelper.Translation.Get("ui.phone.contacts");
            string recentsLabel = ModEntry.SHelper.Translation.Get("ui.phone.recents");
            Vector2 sContacts = Game1.smallFont.MeasureString(contactsLabel) * tabScale;
            Vector2 sRecents = Game1.smallFont.MeasureString(recentsLabel) * tabScale;

            b.DrawString(Game1.smallFont, contactsLabel, new Vector2(bounds.X + (tabCellWidth - sContacts.X) / 2, tabBackground.Y + (tabHeight - sContacts.Y) / 2), colorContacts, 0f, Vector2.Zero, tabScale, SpriteEffects.None, 1f);
            b.DrawString(Game1.smallFont, recentsLabel, new Vector2(bounds.X + tabCellWidth + (tabCellWidth - sRecents.X) / 2, tabBackground.Y + (tabHeight - sRecents.Y) / 2), colorRecents, 0f, Vector2.Zero, tabScale, SpriteEffects.None, 1f);

            int rowHeight = (int)(ScaleUiValue(55) * 1.25f);

            // --- MAIN LIST SCROLLING AREA ---
            if (phoneAppCurrentTab == 0 || phoneAppCurrentTab == 1)
            {
                Rectangle originalScissor = b.GraphicsDevice.ScissorRectangle;
                b.End();

                RasterizerState scissorState = new RasterizerState { ScissorTestEnable = true };
                b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, scissorState);
                b.GraphicsDevice.ScissorRectangle = Rectangle.Intersect(originalScissor, clipArea);

                if (phoneAppCurrentTab == 0) // Contacts View
                {
                    var contacts = GetSortedContacts();
                    int currentY = contentY - phoneAppContactsScroll;
                    foreach (var contact in contacts)
                    {
                        Rectangle cardRect = new Rectangle(bounds.X + ScaleUiValue(15), currentY, bounds.Width - ScaleUiValue(30), rowHeight);
                        Textures.DrawCard(b, cardRect.X, cardRect.Y, cardRect.Width, cardRect.Height, Color.White * 0.85f);

                        string displayNameStr = contact.Name;
                        var character = Game1.getCharacterFromName(contact.Name);
                        if (character != null) displayNameStr = character.displayName;

                        int nameOffsetX = ScaleUiValue(15);

                        // Draw favorite heart icon if favorited
                        if (phoneAppFavoriteNumbers.Contains(contact.Name))
                        {
                            int heartSize = ScaleUiValue(20);
                            b.Draw(Game1.mouseCursors, new Rectangle(cardRect.X + nameOffsetX, cardRect.Y + (cardRect.Height - heartSize) / 2, heartSize, heartSize), new Rectangle(211, 428, 7, 6), Color.White);
                            nameOffsetX += heartSize + ScaleUiValue(8);
                        }

                        float nameScale = 0.85f * uiScale;
                        b.DrawString(Game1.smallFont, displayNameStr, new Vector2(cardRect.X + nameOffsetX, cardRect.Y + (cardRect.Height - Game1.smallFont.LineSpacing * nameScale) / 2), Color.Black, 0f, Vector2.Zero, nameScale, SpriteEffects.None, 1f);

                        currentY += rowHeight + ScaleUiValue(6);
                    }

                    if (contacts.Count == 0)
                    {
                        float phScale = 1f * uiScale;
                        string noContactText = ModEntry.SHelper.Translation.Get("ui.phone.no_contacts_added");
                        Vector2 noContactSz = Game1.smallFont.MeasureString(noContactText) * phScale;
                        b.DrawString(Game1.smallFont, noContactText, new Vector2(bounds.X + (bounds.Width - noContactSz.X) / 2, contentY + ScaleUiValue(60)), Color.Gray, 0f, Vector2.Zero, phScale, SpriteEffects.None, 1f);
                    }
                }
                else // Recents View
                {
                    int currentY = contentY - phoneAppRecentsScroll;
                    foreach (var call in phoneAppRecentCalls)
                    {
                        Rectangle cardRect = new Rectangle(bounds.X + ScaleUiValue(15), currentY, bounds.Width - ScaleUiValue(30), rowHeight);
                        Textures.DrawCard(b, cardRect.X, cardRect.Y, cardRect.Width, cardRect.Height, Color.White * 0.85f);

                        string nameText = call.Name;
                        var character = Game1.getCharacterFromName(call.Name);
                        if (character != null) nameText = character.displayName;

                        float recNameScale = 0.85f * uiScale;
                        float recTimeScale = 0.65f * uiScale;
                        b.DrawString(Game1.smallFont, nameText, new Vector2(cardRect.X + ScaleUiValue(15), cardRect.Y + ScaleUiValue(10)), Color.Black, 0f, Vector2.Zero, recNameScale, SpriteEffects.None, 1f);
                        b.DrawString(Game1.smallFont, call.TimeText, new Vector2(cardRect.X + ScaleUiValue(15), cardRect.Y + ScaleUiValue(32)), Color.Gray, 0f, Vector2.Zero, recTimeScale, SpriteEffects.None, 1f);

                        currentY += rowHeight + ScaleUiValue(6);
                    }

                    if (phoneAppRecentCalls.Count == 0)
                    {
                        float phScale = 1f * uiScale;
                        string noCallText = ModEntry.SHelper.Translation.Get("ui.phone.no_recent_calls");
                        Vector2 noCallSz = Game1.smallFont.MeasureString(noCallText) * phScale;
                        b.DrawString(Game1.smallFont, noCallText, new Vector2(bounds.X + (bounds.Width - noCallSz.X) / 2, contentY + ScaleUiValue(60)), Color.Gray, 0f, Vector2.Zero, phScale, SpriteEffects.None, 1f);
                    }
                }

                b.End();
                b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, null);
                b.GraphicsDevice.ScissorRectangle = originalScissor;
            }

            Texture2D frameTex = Textures.PhoneEmpty;
            if (frameTex != null && !frameTex.IsDisposed)
            {
                b.Draw(frameTex, new Vector2(ModEntry.currentMenuX, ModEntry.currentMenuY), null, Color.White, 0f, Vector2.Zero, uiScale, SpriteEffects.None, 0.99f);
            }
        }

        public void ReceiveLeftClickPhoneApp(int x, int y)
        {
            if (phoneAppViewingContactDetail)
            {
                return;
            }

            Rectangle bounds = GetPhoneContentBounds();
            int tabHeight = ScaleUiValue(70);

            if (y >= bounds.Bottom - tabHeight && y <= bounds.Bottom && x >= bounds.X && x <= bounds.Right)
            {
                int clickedIndex = (x - bounds.X) / (bounds.Width / 2);
                if (clickedIndex >= 0 && clickedIndex <= 1)
                {
                    phoneAppCurrentTab = clickedIndex;
                    phoneAppViewingContactDetail = false;
                    phoneAppSearchQuery = "";
                    Game1.playSound("shwip");
                }
                phoneAppClickHandledOnPress = true;
                return;
            }
        }


        public void ReceiveScrollWheelActionPhoneApp(int direction)
        {
            if (phoneAppViewingContactDetail)
            {
                int scrollScaleContact = ScaleUiValue(40);
                phoneAppContactDetailScroll = Math.Clamp(phoneAppContactDetailScroll + (direction > 0 ? -scrollScaleContact : scrollScaleContact), 0, phoneAppContactDetailMaxScroll);
                return;
            }

            int scrollScale = ScaleUiValue(40);
            int rowHeight = (int)(ScaleUiValue(55) * 1.25f);
            if (phoneAppCurrentTab == 0)
            {
                int maxScroll = Math.Max(0, GetSortedContacts().Count * (rowHeight + ScaleUiValue(6)) - ScaleUiValue(500));
                phoneAppContactsScroll = Math.Clamp(phoneAppContactsScroll + (direction > 0 ? -scrollScale : scrollScale), 0, maxScroll);
            }
            else if (phoneAppCurrentTab == 1)
            {
                int maxScroll = Math.Max(0, phoneAppRecentCalls.Count * (rowHeight + ScaleUiValue(6)) - ScaleUiValue(500));
                phoneAppRecentsScroll = Math.Clamp(phoneAppRecentsScroll + (direction > 0 ? -scrollScale : scrollScale), 0, maxScroll);
            }
        }

        public void ApplyTouchScrollDeltaPhoneApp(int pixelDelta)
        {
            if (phoneAppViewingContactDetail)
            {
                phoneAppContactDetailScroll = Math.Clamp(phoneAppContactDetailScroll + pixelDelta, 0, phoneAppContactDetailMaxScroll);
                return;
            }

            int rowHeight = (int)(ScaleUiValue(55) * 1.25f);
            if (phoneAppCurrentTab == 0)
            {
                int maxScroll = Math.Max(0, GetSortedContacts().Count * (rowHeight + ScaleUiValue(6)) - ScaleUiValue(500));
                phoneAppContactsScroll = Math.Clamp(phoneAppContactsScroll + pixelDelta, 0, maxScroll);
            }
            else if (phoneAppCurrentTab == 1)
            {
                int maxScroll = Math.Max(0, phoneAppRecentCalls.Count * (rowHeight + ScaleUiValue(6)) - ScaleUiValue(500));
                phoneAppRecentsScroll = Math.Clamp(phoneAppRecentsScroll + pixelDelta, 0, maxScroll);
            }
        }

        public void ReleaseLeftClickPhoneApp(int x, int y)
        {
            if (phoneAppClickHandledOnPress)
            {
                phoneAppClickHandledOnPress = false;
                return;
            }

            if (phoneAppViewingContactDetail)
            {
                ReleaseLeftClickPhoneContactDetail(x, y);
                return;
            }

            Rectangle bounds = GetPhoneContentBounds();
            int tabHeight = ScaleUiValue(70);
            int headerHeight = ScaleUiValue(80);
            int contentY = bounds.Y + headerHeight + ScaleUiValue(5);
            int contentHeight = bounds.Height - headerHeight - tabHeight - ScaleUiValue(10);
            Rectangle clipArea = new Rectangle(bounds.X, contentY, bounds.Width, contentHeight);
            int rowHeight = (int)(ScaleUiValue(55) * 1.25f);

            if (phoneAppCurrentTab == 0) // Contacts List
            {
                var list = GetSortedContacts();
                int currentY = contentY - phoneAppContactsScroll;
                foreach (var item in list)
                {
                    Rectangle rowRect = new Rectangle(bounds.X + ScaleUiValue(15), currentY, bounds.Width - ScaleUiValue(30), rowHeight);
                    if (clipArea.Contains(x, y) && rowRect.Contains(x, y))
                    {
                        Game1.playSound("smallSelect");
                        phoneAppSelectedContactDetail = item;
                        phoneAppContactDetailScroll = 0;
                        phoneAppViewingContactDetail = true;
                        return;
                    }
                    currentY += rowHeight + ScaleUiValue(6);
                }
            }
            else if (phoneAppCurrentTab == 1) // Recents List
            {
                int currentY = contentY - phoneAppRecentsScroll;
                foreach (var call in phoneAppRecentCalls)
                {
                    Rectangle rowRect = new Rectangle(bounds.X + ScaleUiValue(15), currentY, bounds.Width - ScaleUiValue(30), rowHeight);
                    if (clipArea.Contains(x, y) && rowRect.Contains(x, y))
                    {
                        Game1.playSound("bigSelect");
                        ExecuteCallAction(call.Name);
                        return;
                    }
                    currentY += rowHeight + ScaleUiValue(6);
                }
            }
        }

        public void HandlePhoneAppKeyPress(Keys key)
        {
            if (phoneAppViewingContactDetail) return;

            if (phoneAppCurrentTab == 0)
            {
                if (key == Keys.Back)
                {
                    if (phoneAppSearchQuery.Length > 0)
                    {
                        phoneAppSearchQuery = phoneAppSearchQuery.Substring(0, phoneAppSearchQuery.Length - 1);
                        Game1.playSound("thudStep");
                    }
                    return;
                }

                string character = key.ToString();
                if (key >= Keys.D0 && key <= Keys.D9) character = (key - Keys.D0).ToString();
                else if (key >= Keys.NumPad0 && key <= Keys.NumPad9) character = (key - Keys.NumPad0).ToString();

                if (character.Length == 1)
                {
                    if (phoneAppSearchQuery.Length < 20)
                    {
                        bool isShift = Game1.oldKBState.IsKeyDown(Keys.LeftShift) || Game1.oldKBState.IsKeyDown(Keys.RightShift);
                        phoneAppSearchQuery += isShift ? character.ToUpper() : character.ToLower();
                    }
                }
                else if (key == Keys.Space && phoneAppSearchQuery.Length < 20)
                {
                    phoneAppSearchQuery += " ";
                }
            }
        }

        public static void UpdateNpcNumbers()
        {
            try
            {
                ModEntry.ContactableNpcs.Clear();

                HashSet<string> blacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (ModEntry.Config != null && !string.IsNullOrWhiteSpace(ModEntry.Config.BlacklistNpc))
                {
                    foreach (var item in ModEntry.Config.BlacklistNpc.Split(','))
                    {
                        string trimmed = item.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                            blacklist.Add(trimmed);
                    }
                }

                int requiredHearts = ModEntry.Config.FriendshipRequirement == "Friend" ? 250 : 1;

                Utility.ForEachVillager(npc =>
                {
                    if (npc != null && npc.CanSocialize)
                    {
                        string npcName = npc.Name;

                        if (blacklist.Contains(npcName))
                            return true;

                        int currentHearts = Game1.player.getFriendshipLevelForNPC(npcName);
                        if (currentHearts >= requiredHearts)
                        {
                            ModEntry.ContactableNpcs.Add(npcName);
                        }
                    }
                    return true;
                });
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor?.Log($"Error updating NPC numbers list: {ex.Message}", LogLevel.Error);
            }
        }



        private void ExecuteCallAction(string npcName)
        {
            string rawSeason = Game1.currentSeason;
            string readableSeason = Utility.getSeasonNameFromNumber(Utility.getSeasonNumber(rawSeason));
            string timeStrVal = Game1.getTimeOfDayString(Game1.timeOfDay);
            string timestamp = ModEntry.SHelper.Translation.Get("ui.phone.time_format", new { seasonName = readableSeason, day = Game1.dayOfMonth, time = timeStrVal });

            phoneAppRecentCalls.Insert(0, new RecentCall { Name = npcName, TimeText = timestamp });
            if (phoneAppRecentCalls.Count > 100) phoneAppRecentCalls.RemoveAt(phoneAppRecentCalls.Count - 1);
            SavePhoneAppData();

            ClosePhoneMenu();

            NPC targetNpc = Game1.getCharacterFromName(npcName);
            if (targetNpc != null)
            {
                ModEntry.LaunchNpcPhoneDialogue(targetNpc);
            }
            else
            {
                Game1.activeClickableMenu = new DialogueBox(ModEntry.SHelper.Translation.Get("ui.phone.calling_no_answer", new { name = npcName, number = "" }));
            }
        }
    }
}