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
        private int phoneAppCurrentTab = 0; // 0 = Contacts, 1 = Recents, 2 = Keypad
        private int phoneAppContactsScroll = 0;
        private int phoneAppRecentsScroll = 0;
        private string phoneAppKeypadBuffer = "";
        private string phoneAppSearchQuery = ""; // Active search filter query string

        // Form & Configuration Navigation States
        private bool phoneAppIsAddingContact = false;
        private bool phoneAppIsEditingContacts = false;
        private bool phoneAppIsEditingExistingContact = false;
        private bool phoneAppIsConfirmingDelete = false;
        private bool phoneAppClickHandledOnPress = false;

        // Details View State
        private bool phoneAppViewingContactDetail = false;
        private ContactItem phoneAppSelectedContactDetail = null;

        private int phoneAppEditingContactIndex = -1;
        private int phoneAppDeletingContactIndex = -1;
        private int phoneAppActiveField = 0; // 0 = Name, 1 = Number
        private string phoneAppNewContactName = "";

        private List<RecentCall> phoneAppRecentCalls = new();
        private List<Contact> phoneAppContacts = new();
        private List<string> phoneAppFavoriteNumbers = new(); // Stores favorites by Number
        internal static bool phoneAppDataLoaded = false;

        public class RecentCall
        {
            public string Name { get; set; } = "";
            public string Number { get; set; } = "";
            public string TimeText { get; set; } = "";
        }

        public class Contact
        {
            public string Name { get; set; } = "";
            public string Number { get; set; } = "";
        }

        public class ContactItem
        {
            public string Name { get; set; } = "";
            public string Number { get; set; } = "";
            public bool IsNpc { get; set; }
            public Contact OriginalContact { get; set; }
        }

        private void EnsurePhoneAppDataLoaded()
        {
            if (phoneAppDataLoaded) return;
            phoneAppDataLoaded = true;

            try
            {
                string folderPath = Path.Combine(ModEntry.SHelper.DirectoryPath, "userdata", ModEntry.GetActiveSaveFolderName());
                string filePath = Path.Combine(folderPath, "phone_app_data.json");

                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var data = Newtonsoft.Json.Linq.JObject.Parse(json);

                    if (data["RecentCalls"] != null)
                        phoneAppRecentCalls = data["RecentCalls"].ToObject<List<RecentCall>>() ?? new();

                    if (data["Contacts"] != null)
                        phoneAppContacts = data["Contacts"].ToObject<List<Contact>>() ?? new();
                    else if (data["CustomContacts"] != null)
                        phoneAppContacts = data["CustomContacts"].ToObject<List<Contact>>() ?? new();

                    if (data["FavoriteNumbers"] != null)
                        phoneAppFavoriteNumbers = data["FavoriteNumbers"].ToObject<List<string>>() ?? new();
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
            return phoneAppContacts;
        }

        private void SavePhoneAppData()
        {
            try
            {
                string folderPath = Path.Combine(ModEntry.SHelper.DirectoryPath, "userdata", ModEntry.GetActiveSaveFolderName());
                Directory.CreateDirectory(folderPath);
                string filePath = Path.Combine(folderPath, "phone_app_data.json");

                var data = new { RecentCalls = phoneAppRecentCalls, Contacts = phoneAppContacts, FavoriteNumbers = phoneAppFavoriteNumbers };
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

            foreach (var contact in phoneAppContacts)
            {
                bool isNpc = ModEntry.NpcNumbers.ContainsKey(contact.Number);
                list.Add(new ContactItem
                {
                    Name = contact.Name,
                    Number = contact.Number,
                    IsNpc = isNpc,
                    OriginalContact = contact
                });
            }

            var sorted = list
                .OrderByDescending(c => phoneAppFavoriteNumbers.Contains(c.Number))
                .ThenBy(c =>
                {
                    string nameText = c.Name;
                    if (c.IsNpc && ModEntry.NpcNumbers.TryGetValue(c.Number, out string npcInternal))
                    {
                        if (string.Equals(c.Name, npcInternal, StringComparison.OrdinalIgnoreCase))
                        {
                            var character = Game1.getCharacterFromName(npcInternal);
                            if (character != null) nameText = character.displayName;
                        }
                    }
                    return nameText;
                }, StringComparer.OrdinalIgnoreCase).ToList();

            // Filter on-the-fly based on search box input query string if active
            if (phoneAppCurrentTab == 0 && !phoneAppIsAddingContact && !phoneAppIsEditingExistingContact && !phoneAppIsConfirmingDelete && !string.IsNullOrEmpty(phoneAppSearchQuery))
            {
                sorted = sorted.Where(c =>
                {
                    string nameText = c.Name;
                    if (c.IsNpc && ModEntry.NpcNumbers.TryGetValue(c.Number, out string npcInternal))
                    {
                        if (string.Equals(c.Name, npcInternal, StringComparison.OrdinalIgnoreCase))
                        {
                            var character = Game1.getCharacterFromName(npcInternal);
                            if (character != null) nameText = character.displayName;
                        }
                    }
                    return nameText.Contains(phoneAppSearchQuery, StringComparison.OrdinalIgnoreCase) || c.Number.Contains(phoneAppSearchQuery, StringComparison.OrdinalIgnoreCase);
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
            string title = phoneAppCurrentTab == 0 ? ModEntry.SHelper.Translation.Get("ui.phone.contacts") : (phoneAppCurrentTab == 1 ? ModEntry.SHelper.Translation.Get("ui.phone.recents") : ModEntry.SHelper.Translation.Get("ui.phone.keypad"));
            if (phoneAppIsAddingContact) title = ModEntry.SHelper.Translation.Get("ui.phone.new_contact");
            if (phoneAppIsEditingExistingContact) title = ModEntry.SHelper.Translation.Get("ui.phone.edit_contact");

            if (phoneAppCurrentTab == 0 && !phoneAppIsAddingContact && !phoneAppIsEditingExistingContact && !phoneAppIsConfirmingDelete)
            {
                Rectangle searchBoxRect = new Rectangle(bounds.X + ScaleUiValue(15), bounds.Y + ScaleUiValue(10), bounds.Width - ScaleUiValue(115), ScaleUiValue(60));
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

            // --- HEADERS ACTION DRAW BUTTONS ---
            Rectangle topActionBtn = new Rectangle(bounds.Right - ScaleUiValue(85), bounds.Y + ScaleUiValue(10), ScaleUiValue(70), ScaleUiValue(60));
            if (phoneAppCurrentTab == 0 && !phoneAppIsAddingContact && !phoneAppIsEditingExistingContact && !phoneAppIsConfirmingDelete)
            {
                Color btnColor = phoneAppIsEditingContacts ? Color.LightGreen : Color.LightGray;
                Textures.DrawCard(b, topActionBtn.X, topActionBtn.Y, topActionBtn.Width, topActionBtn.Height, btnColor);
                string btnText = phoneAppIsEditingContacts ? ModEntry.SHelper.Translation.Get("ui.reorder.done") : ModEntry.SHelper.Translation.Get("ui.phone.edit");
                float btnScale = 0.75f * uiScale;
                Vector2 sz = Game1.smallFont.MeasureString(btnText) * btnScale;
                b.DrawString(Game1.smallFont, btnText, new Vector2(topActionBtn.X + (topActionBtn.Width - sz.X) / 2, topActionBtn.Y + (topActionBtn.Height - sz.Y) / 2), Color.Black, 0f, Vector2.Zero, btnScale, SpriteEffects.None, 1f);
            }
            else if (phoneAppCurrentTab == 2 && !phoneAppIsAddingContact && phoneAppKeypadBuffer.Length > 0)
            {
                Textures.DrawCard(b, topActionBtn.X, topActionBtn.Y, topActionBtn.Width, topActionBtn.Height, Color.LightSkyBlue);
                string btnText = ModEntry.SHelper.Translation.Get("ui.phone.add");
                float btnScale = 0.75f * uiScale;
                Vector2 sz = Game1.smallFont.MeasureString(btnText) * btnScale;
                b.DrawString(Game1.smallFont, btnText, new Vector2(topActionBtn.X + (topActionBtn.Width - sz.X) / 2, topActionBtn.Y + (topActionBtn.Height - sz.Y) / 2), Color.Black, 0f, Vector2.Zero, btnScale, SpriteEffects.None, 1f);
            }

            // --- BOTTOM TAB BAR LAYOUT ---
            Rectangle tabBackground = new Rectangle(bounds.X, bounds.Y + bounds.Height - tabHeight, bounds.Width, tabHeight);
            b.Draw(Game1.staminaRect, tabBackground, Color.Black * 0.08f);
            b.Draw(Game1.staminaRect, new Rectangle(bounds.X, tabBackground.Y, bounds.Width, ScaleUiValue(1)), Color.LightGray);

            int tabCellWidth = bounds.Width / 3;
            Color colorContacts = phoneAppCurrentTab == 0 ? Color.DodgerBlue : Color.DimGray;
            Color colorRecents = phoneAppCurrentTab == 1 ? Color.DodgerBlue : Color.DimGray;
            Color colorKeypad = phoneAppCurrentTab == 2 ? Color.DodgerBlue : Color.DimGray;

            float tabScale = 0.85f * uiScale;
            string contactsLabel = ModEntry.SHelper.Translation.Get("ui.phone.contacts");
            string recentsLabel = ModEntry.SHelper.Translation.Get("ui.phone.recents");
            string keypadLabel = ModEntry.SHelper.Translation.Get("ui.phone.keypad");
            Vector2 sContacts = Game1.smallFont.MeasureString(contactsLabel) * tabScale;
            Vector2 sRecents = Game1.smallFont.MeasureString(recentsLabel) * tabScale;
            Vector2 sKeypad = Game1.smallFont.MeasureString(keypadLabel) * tabScale;

            b.DrawString(Game1.smallFont, contactsLabel, new Vector2(bounds.X + (tabCellWidth - sContacts.X) / 2, tabBackground.Y + (tabHeight - sContacts.Y) / 2), colorContacts, 0f, Vector2.Zero, tabScale, SpriteEffects.None, 1f);
            b.DrawString(Game1.smallFont, recentsLabel, new Vector2(bounds.X + tabCellWidth + (tabCellWidth - sRecents.X) / 2, tabBackground.Y + (tabHeight - sRecents.Y) / 2), colorRecents, 0f, Vector2.Zero, tabScale, SpriteEffects.None, 1f);
            b.DrawString(Game1.smallFont, keypadLabel, new Vector2(bounds.X + tabCellWidth * 2 + (bounds.Width - tabCellWidth * 2 - sKeypad.X) / 2, tabBackground.Y + (tabHeight - sKeypad.Y) / 2), colorKeypad, 0f, Vector2.Zero, tabScale, SpriteEffects.None, 1f);

            int rowHeight = (int)(ScaleUiValue(55) * 1.25f);

            // --- MAIN LIST SCROLLING AREA ---
            if (phoneAppCurrentTab == 0 || phoneAppCurrentTab == 1)
            {
                if (phoneAppIsConfirmingDelete)
                {
                    Rectangle confBox = new Rectangle(bounds.X + ScaleUiValue(20), contentY + ScaleUiValue(50), bounds.Width - ScaleUiValue(40), ScaleUiValue(200));
                    Textures.DrawCard(b, confBox.X, confBox.Y, confBox.Width, confBox.Height, Color.White);

                    string confText = ModEntry.SHelper.Translation.Get("ui.phone.delete_contact_confirm");
                    float cfScale = 1f * uiScale;
                    Vector2 txSz = Game1.smallFont.MeasureString(confText) * cfScale;
                    b.DrawString(Game1.smallFont, confText, new Vector2(confBox.X + (confBox.Width - txSz.X) / 2, confBox.Y + ScaleUiValue(30)), Color.Black, 0f, Vector2.Zero, cfScale, SpriteEffects.None, 1f);

                    Rectangle btnYes = new Rectangle(confBox.X + ScaleUiValue(20), confBox.Y + ScaleUiValue(110), confBox.Width / 2 - ScaleUiValue(30), ScaleUiValue(50));
                    Rectangle btnNo = new Rectangle(confBox.X + confBox.Width / 2 + ScaleUiValue(10), confBox.Y + ScaleUiValue(110), confBox.Width / 2 - ScaleUiValue(30), ScaleUiValue(50));

                    Textures.DrawCard(b, btnYes.X, btnYes.Y, btnYes.Width, btnYes.Height, Color.LightCoral);
                    Textures.DrawCard(b, btnNo.X, btnNo.Y, btnNo.Width, btnNo.Height, Color.LightGray);

                    float ynScale = 0.9f * uiScale;
                    string yesText = ModEntry.SHelper.Translation.Get("ui.button.yes");
                    string noText = ModEntry.SHelper.Translation.Get("ui.button.no");
                    Vector2 ySz = Game1.smallFont.MeasureString(yesText) * ynScale;
                    Vector2 nSz = Game1.smallFont.MeasureString(noText) * ynScale;

                    b.DrawString(Game1.smallFont, yesText, new Vector2(btnYes.X + (btnYes.Width - ySz.X) / 2, btnYes.Y + (btnYes.Height - ySz.Y) / 2), Color.Black, 0f, Vector2.Zero, ynScale, SpriteEffects.None, 1f);
                    b.DrawString(Game1.smallFont, noText, new Vector2(btnNo.X + (btnNo.Width - nSz.X) / 2, btnNo.Y + (btnNo.Height - nSz.Y) / 2), Color.Black, 0f, Vector2.Zero, ynScale, SpriteEffects.None, 1f);
                }
                else if (phoneAppIsEditingExistingContact)
                {
                    int fX = bounds.X + ScaleUiValue(30);
                    int fW = bounds.Width - ScaleUiValue(60);
                    float labelScale = 0.95f * uiScale;
                    float inputTxtScale = 0.9f * uiScale;

                    b.DrawString(Game1.smallFont, ModEntry.SHelper.Translation.Get("ui.phone.edit_contact_name_label"), new Vector2(fX, contentY + ScaleUiValue(15)), Color.Black, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 1f);
                    Rectangle nameFieldRect = new Rectangle(fX, contentY + ScaleUiValue(45), fW, ScaleUiValue(60));
                    Textures.DrawCard(b, nameFieldRect.X, nameFieldRect.Y, nameFieldRect.Width, nameFieldRect.Height, phoneAppActiveField == 0 ? Color.LightCyan : Color.White);

                    string printableName = phoneAppNewContactName;
                    if (phoneAppActiveField == 0 && (DateTime.UtcNow.Millisecond / 500) % 2 == 0) printableName += "|";
                    Vector2 pNameSz = Game1.smallFont.MeasureString(printableName) * inputTxtScale;
                    b.DrawString(Game1.smallFont, printableName, new Vector2(nameFieldRect.X + ScaleUiValue(12), nameFieldRect.Y + (nameFieldRect.Height - pNameSz.Y) / 2), Color.Black, 0f, Vector2.Zero, inputTxtScale, SpriteEffects.None, 1f);

                    b.DrawString(Game1.smallFont, ModEntry.SHelper.Translation.Get("ui.phone.edit_contact_number_label"), new Vector2(fX, contentY + ScaleUiValue(120)), Color.Black, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 1f);
                    Rectangle numFieldRect = new Rectangle(fX, contentY + ScaleUiValue(150), fW, ScaleUiValue(60));
                    Textures.DrawCard(b, numFieldRect.X, numFieldRect.Y, numFieldRect.Width, numFieldRect.Height, phoneAppActiveField == 1 ? Color.LightCyan : Color.White);

                    string printableNum = phoneAppKeypadBuffer;
                    if (phoneAppActiveField == 1 && (DateTime.UtcNow.Millisecond / 500) % 2 == 0) printableNum += "|";
                    Vector2 pNumSz = Game1.smallFont.MeasureString(printableNum) * inputTxtScale;
                    b.DrawString(Game1.smallFont, printableNum, new Vector2(numFieldRect.X + ScaleUiValue(12), numFieldRect.Y + (numFieldRect.Height - pNumSz.Y) / 2), Color.Black, 0f, Vector2.Zero, inputTxtScale, SpriteEffects.None, 1f);

                    Rectangle btnSave = new Rectangle(fX, contentY + ScaleUiValue(235), fW / 2 - ScaleUiValue(8), ScaleUiValue(45));
                    Rectangle btnCancel = new Rectangle(fX + fW / 2 + ScaleUiValue(8), contentY + ScaleUiValue(235), fW / 2 - ScaleUiValue(8), ScaleUiValue(45));

                    Textures.DrawCard(b, btnSave.X, btnSave.Y, btnSave.Width, btnSave.Height, Color.PaleGreen);
                    Textures.DrawCard(b, btnCancel.X, btnCancel.Y, btnCancel.Width, btnCancel.Height, Color.LightCoral);

                    float actScale = 0.9f * uiScale;
                    string saveText = ModEntry.SHelper.Translation.Get("ui.button.save");
                    string cancelText = ModEntry.SHelper.Translation.Get("ui.button.cancel");
                    Vector2 sSz = Game1.smallFont.MeasureString(saveText) * actScale;
                    Vector2 cSz = Game1.smallFont.MeasureString(cancelText) * actScale;
                    b.DrawString(Game1.smallFont, saveText, new Vector2(btnSave.X + (btnSave.Width - sSz.X) / 2, btnSave.Y + (btnSave.Height - sSz.Y) / 2), Color.Black, 0f, Vector2.Zero, actScale, SpriteEffects.None, 1f);
                    b.DrawString(Game1.smallFont, cancelText, new Vector2(btnCancel.X + (btnCancel.Width - cSz.X) / 2, btnCancel.Y + (btnCancel.Height - cSz.Y) / 2), Color.Black, 0f, Vector2.Zero, actScale, SpriteEffects.None, 1f);
                }
                else
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
                            if (contact.IsNpc && ModEntry.NpcNumbers.TryGetValue(contact.Number, out string npcInternalName))
                            {
                                if (string.Equals(contact.Name, npcInternalName, StringComparison.OrdinalIgnoreCase))
                                {
                                    var character = Game1.getCharacterFromName(npcInternalName);
                                    if (character != null) displayNameStr = character.displayName;
                                }
                            }

                            int nameOffsetX = ScaleUiValue(15);

                            // Draw favorite heart icon if favorited
                            if (phoneAppFavoriteNumbers.Contains(contact.Number))
                            {
                                int heartSize = ScaleUiValue(20);
                                b.Draw(Game1.mouseCursors, new Rectangle(cardRect.X + nameOffsetX, cardRect.Y + (cardRect.Height - heartSize) / 2, heartSize, heartSize), new Rectangle(211, 428, 7, 6), Color.White);
                                nameOffsetX += heartSize + ScaleUiValue(8);
                            }

                            float nameScale = 0.85f * uiScale;
                            b.DrawString(Game1.smallFont, displayNameStr, new Vector2(cardRect.X + nameOffsetX, cardRect.Y + (cardRect.Height - Game1.smallFont.LineSpacing * nameScale) / 2), Color.Black, 0f, Vector2.Zero, nameScale, SpriteEffects.None, 1f);

                            if (phoneAppIsEditingContacts)
                            {
                                Rectangle delBtn = new Rectangle(cardRect.Right - ScaleUiValue(75), cardRect.Y + ScaleUiValue(8), ScaleUiValue(65), cardRect.Height - ScaleUiValue(16));
                                Textures.DrawCard(b, delBtn.X, delBtn.Y, delBtn.Width, delBtn.Height, Color.Tomato);
                                float delScale = 0.7f * uiScale;
                                string delText = ModEntry.SHelper.Translation.Get("ui.photo.delete");
                                Vector2 delSz = Game1.smallFont.MeasureString(delText) * delScale;
                                b.DrawString(Game1.smallFont, delText, new Vector2(delBtn.X + (delBtn.Width - delSz.X) / 2, delBtn.Y + (delBtn.Height - delSz.Y) / 2), Color.White, 0f, Vector2.Zero, delScale, SpriteEffects.None, 1f);
                            }
                            else
                            {
                                float numScale = 0.65f * uiScale;
                                Vector2 labelSize = Game1.smallFont.MeasureString(contact.Number) * numScale;
                                b.DrawString(Game1.smallFont, contact.Number, new Vector2(cardRect.Right - labelSize.X - ScaleUiValue(15), cardRect.Y + (cardRect.Height - labelSize.Y) / 2), Color.Gray, 0f, Vector2.Zero, numScale, SpriteEffects.None, 1f);
                            }
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
                            var existingContact = phoneAppContacts.FirstOrDefault(c => c.Number == call.Number);
                            if (existingContact != null)
                            {
                                nameText = existingContact.Name;
                                if (ModEntry.NpcNumbers.TryGetValue(call.Number, out string npcInternal) && string.Equals(existingContact.Name, npcInternal, StringComparison.OrdinalIgnoreCase))
                                {
                                    var character = Game1.getCharacterFromName(npcInternal);
                                    if (character != null) nameText = character.displayName;
                                }
                            }
                            else if (ModEntry.NpcNumbers.TryGetValue(call.Number, out string npcName))
                            {
                                var character = Game1.getCharacterFromName(npcName);
                                if (character != null) nameText = character.displayName;
                            }

                            float recNameScale = 0.85f * uiScale;
                            float recTimeScale = 0.65f * uiScale;
                            b.DrawString(Game1.smallFont, nameText, new Vector2(cardRect.X + ScaleUiValue(15), cardRect.Y + ScaleUiValue(10)), Color.Black, 0f, Vector2.Zero, recNameScale, SpriteEffects.None, 1f);
                            b.DrawString(Game1.smallFont, call.TimeText, new Vector2(cardRect.X + ScaleUiValue(15), cardRect.Y + ScaleUiValue(32)), Color.Gray, 0f, Vector2.Zero, recTimeScale, SpriteEffects.None, 1f);

                            if (!string.IsNullOrEmpty(call.Number) && call.Number != call.Name)
                            {
                                float recNumScale = 0.7f * uiScale;
                                Vector2 numSize = Game1.smallFont.MeasureString(call.Number) * recNumScale;
                                b.DrawString(Game1.smallFont, call.Number, new Vector2(cardRect.Right - numSize.X - ScaleUiValue(15), cardRect.Y + (cardRect.Height - numSize.Y) / 2), Color.DimGray, 0f, Vector2.Zero, recNumScale, SpriteEffects.None, 1f);
                            }
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
            }
            else if (phoneAppCurrentTab == 2) // Keypad Grid & Adding
            {
                if (phoneAppIsAddingContact)
                {
                    int fX = bounds.X + ScaleUiValue(30);
                    int fW = bounds.Width - ScaleUiValue(60);
                    float labelScale = 0.95f * uiScale;
                    float inputTxtScale = 0.9f * uiScale;

                    b.DrawString(Game1.smallFont, ModEntry.SHelper.Translation.Get("ui.phone.contact_name_label"), new Vector2(fX, contentY + ScaleUiValue(20)), Color.Black, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 1f);
                    Rectangle fieldRect = new Rectangle(fX, contentY + ScaleUiValue(50), fW, ScaleUiValue(60));
                    Textures.DrawCard(b, fieldRect.X, fieldRect.Y, fieldRect.Width, fieldRect.Height, Color.White);

                    string printable = phoneAppNewContactName;
                    if ((DateTime.UtcNow.Millisecond / 500) % 2 == 0) printable += "|";
                    Vector2 pStrSz = Game1.smallFont.MeasureString(printable) * inputTxtScale;
                    b.DrawString(Game1.smallFont, printable, new Vector2(fieldRect.X + ScaleUiValue(12), fieldRect.Y + (fieldRect.Height - pStrSz.Y) / 2), Color.Black, 0f, Vector2.Zero, inputTxtScale, SpriteEffects.None, 1f);

                    b.DrawString(Game1.smallFont, ModEntry.SHelper.Translation.Get("ui.phone.number_format", new { number = phoneAppKeypadBuffer }), new Vector2(fX, contentY + ScaleUiValue(125)), Color.DarkSlateGray, 0f, Vector2.Zero, 0.9f * uiScale, SpriteEffects.None, 1f);

                    Rectangle btnSave = new Rectangle(fX, contentY + ScaleUiValue(195), fW / 2 - ScaleUiValue(8), ScaleUiValue(45));
                    Rectangle btnCancel = new Rectangle(fX + fW / 2 + ScaleUiValue(8), contentY + ScaleUiValue(195), fW / 2 - ScaleUiValue(8), ScaleUiValue(45));

                    Textures.DrawCard(b, btnSave.X, btnSave.Y, btnSave.Width, btnSave.Height, Color.PaleGreen);
                    Textures.DrawCard(b, btnCancel.X, btnCancel.Y, btnCancel.Width, btnCancel.Height, Color.LightCoral);

                    float actScale = 0.9f * uiScale;
                    string saveText = ModEntry.SHelper.Translation.Get("ui.button.save");
                    string cancelText = ModEntry.SHelper.Translation.Get("ui.button.cancel");
                    Vector2 sSz = Game1.smallFont.MeasureString(saveText) * actScale;
                    Vector2 cSz = Game1.smallFont.MeasureString(cancelText) * actScale;
                    b.DrawString(Game1.smallFont, saveText, new Vector2(btnSave.X + (btnSave.Width - sSz.X) / 2, btnSave.Y + (btnSave.Height - sSz.Y) / 2), Color.Black, 0f, Vector2.Zero, actScale, SpriteEffects.None, 1f);
                    b.DrawString(Game1.smallFont, cancelText, new Vector2(btnCancel.X + (btnCancel.Width - cSz.X) / 2, btnCancel.Y + (btnCancel.Height - cSz.Y) / 2), Color.Black, 0f, Vector2.Zero, actScale, SpriteEffects.None, 1f);
                }
                else
                {
                    Rectangle dispBox = new Rectangle(bounds.X + ScaleUiValue(25), contentY + ScaleUiValue(15), bounds.Width - ScaleUiValue(50), ScaleUiValue(55));
                    Textures.DrawCard(b, dispBox.X, dispBox.Y, dispBox.Width, dispBox.Height, Color.White * 0.45f);

                    float kpBufferScale = 0.65f * uiScale;
                    Vector2 bufSz = Game1.dialogueFont.MeasureString(phoneAppKeypadBuffer) * kpBufferScale;
                    b.DrawString(Game1.dialogueFont, phoneAppKeypadBuffer, new Vector2(dispBox.X + (dispBox.Width - bufSz.X) / 2, dispBox.Y + (dispBox.Height - bufSz.Y) / 2), Color.Black, 0f, Vector2.Zero, kpBufferScale, SpriteEffects.None, 1f);

                    int btnSize = ScaleUiValue(65);
                    int spaceX = ScaleUiValue(35);
                    int spaceY = ScaleUiValue(15);
                    int totalGridWidth = (3 * btnSize) + (2 * spaceX);
                    int keyStartX = bounds.X + (bounds.Width - totalGridWidth) / 2;
                    int keyStartY = dispBox.Bottom + ScaleUiValue(20);
                    string[,] numbers = { { "1", "2", "3" }, { "4", "5", "6" }, { "7", "8", "9" }, { "", "0", "+" } };

                    for (int r = 0; r < 4; r++)
                    {
                        for (int c = 0; c < 3; c++)
                        {
                            string textVal = numbers[r, c];
                            if (string.IsNullOrWhiteSpace(textVal))
                                continue;
                            Rectangle keyRect = new Rectangle(keyStartX + c * (btnSize + spaceX), keyStartY + r * (btnSize + spaceY), btnSize, btnSize);
                            Textures.DrawCard(b, keyRect.X, keyRect.Y, keyRect.Width, keyRect.Height, Color.LightGray * 0.75f);

                            float kpDigitScale = 0.6f * uiScale;
                            Vector2 keySz = Game1.dialogueFont.MeasureString(textVal) * kpDigitScale;
                            b.DrawString(Game1.dialogueFont, textVal, new Vector2(keyRect.X + (keyRect.Width - keySz.X) / 2, keyRect.Y + (keyRect.Height - keySz.Y) / 2), Color.Black, 0f, Vector2.Zero, kpDigitScale, SpriteEffects.None, 1f);
                        }
                    }

                    int actionRowY = keyStartY + 4 * (btnSize + spaceY) + ScaleUiValue(5);
                    Rectangle callBtn = new Rectangle(keyStartX + btnSize + spaceX, actionRowY, btnSize, btnSize);
                    Textures.DrawCard(b, callBtn.X, callBtn.Y, callBtn.Width, callBtn.Height, Color.LimeGreen);
                    float callTxtScale = 0.85f * uiScale;
                    string callBtnLabel = ModEntry.SHelper.Translation.Get("ui.phone.call");
                    Vector2 callTextSz = Game1.smallFont.MeasureString(callBtnLabel) * callTxtScale;
                    b.DrawString(Game1.smallFont, callBtnLabel, new Vector2(callBtn.X + (callBtn.Width - callTextSz.X) / 2, callBtn.Y + (callBtn.Height - callTextSz.Y) / 2), Color.White, 0f, Vector2.Zero, callTxtScale, SpriteEffects.None, 1f);

                    if (phoneAppKeypadBuffer.Length > 0)
                    {
                        Rectangle clearBtn = new Rectangle(keyStartX + (2 * (btnSize + spaceX)), actionRowY + (btnSize - ScaleUiValue(45)) / 2, btnSize, ScaleUiValue(45));
                        Textures.DrawCard(b, clearBtn.X, clearBtn.Y, clearBtn.Width, clearBtn.Height, Color.LightGray);
                        float backTxtScale = 0.8f * uiScale;
                        Vector2 backTextSz = Game1.smallFont.MeasureString("X") * backTxtScale;
                        b.DrawString(Game1.smallFont, "X", new Vector2(clearBtn.X + (clearBtn.Width - backTextSz.X) / 2, clearBtn.Y + (clearBtn.Height - backTextSz.Y) / 2), Color.Black, 0f, Vector2.Zero, backTxtScale, SpriteEffects.None, 1f);
                    }
                }
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
                int clickedIndex = (x - bounds.X) / (bounds.Width / 3);
                if (clickedIndex >= 0 && clickedIndex <= 2)
                {
                    phoneAppCurrentTab = clickedIndex;
                    phoneAppIsAddingContact = false;
                    phoneAppIsEditingExistingContact = false;
                    phoneAppIsConfirmingDelete = false;
                    phoneAppViewingContactDetail = false;
                    phoneAppSearchQuery = "";
                    Game1.playSound("shwip");
                }
                phoneAppClickHandledOnPress = true;
                return;
            }

            int headerHeight = ScaleUiValue(80);
            int contentY = bounds.Y + headerHeight + ScaleUiValue(5);
            int contentHeight = bounds.Height - headerHeight - tabHeight - ScaleUiValue(10);
            Rectangle clipArea = new Rectangle(bounds.X, contentY, bounds.Width, contentHeight);

            if (phoneAppCurrentTab == 0) // Contacts Screen Handlers
            {
                if (phoneAppIsConfirmingDelete)
                {
                    Rectangle confBox = new Rectangle(bounds.X + ScaleUiValue(20), contentY + ScaleUiValue(50), bounds.Width - ScaleUiValue(40), ScaleUiValue(200));
                    Rectangle btnYes = new Rectangle(confBox.X + ScaleUiValue(20), confBox.Y + ScaleUiValue(110), confBox.Width / 2 - ScaleUiValue(30), ScaleUiValue(50));
                    Rectangle btnNo = new Rectangle(confBox.X + confBox.Width / 2 + ScaleUiValue(10), confBox.Y + ScaleUiValue(110), confBox.Width / 2 - ScaleUiValue(30), ScaleUiValue(50));

                    if (btnYes.Contains(x, y))
                    {
                        if (phoneAppDeletingContactIndex >= 0 && phoneAppDeletingContactIndex < phoneAppContacts.Count)
                        {
                            phoneAppContacts.RemoveAt(phoneAppDeletingContactIndex);
                            SavePhoneAppData();
                            Game1.playSound("trashcan");
                        }
                        phoneAppIsConfirmingDelete = false;
                        phoneAppDeletingContactIndex = -1;
                        phoneAppClickHandledOnPress = true;
                        return;
                    }
                    if (btnNo.Contains(x, y))
                    {
                        phoneAppIsConfirmingDelete = false;
                        phoneAppDeletingContactIndex = -1;
                        Game1.playSound("cancel");
                        phoneAppClickHandledOnPress = true;
                        return;
                    }
                    phoneAppClickHandledOnPress = true;
                    return;
                }

                if (phoneAppIsEditingExistingContact)
                {
                    int fX = bounds.X + ScaleUiValue(30);
                    int fW = bounds.Width - ScaleUiValue(60);
                    Rectangle nameFieldRect = new Rectangle(fX, contentY + ScaleUiValue(45), fW, ScaleUiValue(60));
                    Rectangle numFieldRect = new Rectangle(fX, contentY + ScaleUiValue(150), fW, ScaleUiValue(60));
                    Rectangle btnSave = new Rectangle(fX, contentY + ScaleUiValue(235), fW / 2 - ScaleUiValue(8), ScaleUiValue(45));
                    Rectangle btnCancel = new Rectangle(fX + fW / 2 + ScaleUiValue(8), contentY + ScaleUiValue(235), fW / 2 - ScaleUiValue(8), ScaleUiValue(45));

                    if (nameFieldRect.Contains(x, y))
                    {
                        phoneAppActiveField = 0;
                        Game1.playSound("smallSelect");
                        phoneAppClickHandledOnPress = true;
                        return;
                    }
                    if (numFieldRect.Contains(x, y))
                    {
                        phoneAppActiveField = 1;
                        Game1.playSound("smallSelect");
                        phoneAppClickHandledOnPress = true;
                        return;
                    }
                    if (btnSave.Contains(x, y))
                    {
                        if (!string.IsNullOrWhiteSpace(phoneAppNewContactName) && !string.IsNullOrWhiteSpace(phoneAppKeypadBuffer))
                        {
                            if (phoneAppEditingContactIndex >= 0 && phoneAppEditingContactIndex < phoneAppContacts.Count)
                            {
                                string newNum = phoneAppKeypadBuffer.Trim();
                                var currentContact = phoneAppContacts[phoneAppEditingContactIndex];

                                phoneAppContacts.RemoveAll(c => c != currentContact && c.Number == newNum);
                                phoneAppEditingContactIndex = phoneAppContacts.IndexOf(currentContact);

                                if (phoneAppEditingContactIndex >= 0)
                                {
                                    phoneAppContacts[phoneAppEditingContactIndex].Name = phoneAppNewContactName.Trim();
                                    phoneAppContacts[phoneAppEditingContactIndex].Number = newNum;
                                }
                                SavePhoneAppData();
                                Game1.playSound("coin");
                            }
                            phoneAppIsEditingExistingContact = false;
                            phoneAppEditingContactIndex = -1;
                        }
                        else Game1.playSound("cancel");
                        phoneAppClickHandledOnPress = true;
                        return;
                    }
                    if (btnCancel.Contains(x, y))
                    {
                        phoneAppIsEditingExistingContact = false;
                        phoneAppEditingContactIndex = -1;
                        Game1.playSound("cancel");
                        phoneAppClickHandledOnPress = true;
                        return;
                    }
                    phoneAppClickHandledOnPress = true;
                    return;
                }

                Rectangle topActionBtn = new Rectangle(bounds.Right - ScaleUiValue(85), bounds.Y + ScaleUiValue(10), ScaleUiValue(70), ScaleUiValue(60));
                if (topActionBtn.Contains(x, y))
                {
                    phoneAppIsEditingContacts = !phoneAppIsEditingContacts;
                    Game1.playSound("shwip");
                    phoneAppClickHandledOnPress = true;
                    return;
                }

            }
            else if (phoneAppCurrentTab == 2) // Keypad Dialer Click Controls
            {
                if (phoneAppIsAddingContact)
                {
                    int fX = bounds.X + ScaleUiValue(30);
                    int fW = bounds.Width - ScaleUiValue(60);
                    Rectangle btnSave = new Rectangle(fX, contentY + ScaleUiValue(195), fW / 2 - ScaleUiValue(8), ScaleUiValue(45));
                    Rectangle btnCancel = new Rectangle(fX + fW / 2 + ScaleUiValue(8), contentY + ScaleUiValue(195), fW / 2 - ScaleUiValue(8), ScaleUiValue(45));

                    if (btnSave.Contains(x, y))
                    {
                        if (!string.IsNullOrWhiteSpace(phoneAppNewContactName))
                        {
                            string targetNum = phoneAppKeypadBuffer.Trim();
                            phoneAppContacts.RemoveAll(c => c.Number == targetNum);
                            phoneAppContacts.Add(new Contact { Name = phoneAppNewContactName.Trim(), Number = targetNum });

                            SavePhoneAppData();
                            phoneAppIsAddingContact = false;
                            phoneAppCurrentTab = 0;
                            Game1.playSound("coin");
                        }
                        else Game1.playSound("cancel");
                        phoneAppClickHandledOnPress = true;
                        return;
                    }
                    if (btnCancel.Contains(x, y))
                    {
                        phoneAppIsAddingContact = false;
                        Game1.playSound("cancel");
                        phoneAppClickHandledOnPress = true;
                        return;
                    }
                    phoneAppClickHandledOnPress = true;
                    return;
                }
                else
                {
                    Rectangle dispBox = new Rectangle(bounds.X + ScaleUiValue(25), contentY + ScaleUiValue(15), bounds.Width - ScaleUiValue(50), ScaleUiValue(55));

                    Rectangle topActionBtn = new Rectangle(bounds.Right - ScaleUiValue(85), bounds.Y + ScaleUiValue(10), ScaleUiValue(70), ScaleUiValue(60));
                    if (phoneAppKeypadBuffer.Length > 0 && topActionBtn.Contains(x, y))
                    {
                        phoneAppIsAddingContact = true;
                        phoneAppActiveField = 0;

                        if (ModEntry.NpcNumbers.TryGetValue(phoneAppKeypadBuffer, out string npcName))
                        {
                            var character = Game1.getCharacterFromName(npcName);
                            phoneAppNewContactName = character != null ? character.displayName : npcName;
                        }
                        else
                        {
                            phoneAppNewContactName = "";
                        }

                        Game1.playSound("smallSelect");
                        phoneAppClickHandledOnPress = true;
                        return;
                    }

                    int btnSize = ScaleUiValue(65);
                    int spaceX = ScaleUiValue(35);
                    int spaceY = ScaleUiValue(15);
                    int totalGridWidth = (3 * btnSize) + (2 * spaceX);
                    int keyStartX = bounds.X + (bounds.Width - totalGridWidth) / 2;
                    int keyStartY = dispBox.Bottom + ScaleUiValue(20);
                    string[,] numbers = { { "1", "2", "3" }, { "4", "5", "6" }, { "7", "8", "9" }, { "", "0", "+" } };

                    for (int r = 0; r < 4; r++)
                    {
                        for (int c = 0; c < 3; c++)
                        {
                            Rectangle keyRect = new Rectangle(keyStartX + c * (btnSize + spaceX), keyStartY + r * (btnSize + spaceY), btnSize, btnSize);
                            if (keyRect.Contains(x, y))
                            {
                                if (phoneAppKeypadBuffer.Length < 15)
                                {
                                    phoneAppKeypadBuffer += numbers[r, c];
                                    Game1.playSound("clank");
                                }
                                phoneAppClickHandledOnPress = true;
                                return;
                            }
                        }
                    }

                    int actionRowY = keyStartY + 4 * (btnSize + spaceY) + ScaleUiValue(5);
                    Rectangle callBtn = new Rectangle(keyStartX + btnSize + spaceX, actionRowY, btnSize, btnSize);
                    if (callBtn.Contains(x, y))
                    {
                        if (phoneAppKeypadBuffer.Length > 0)
                        {
                            Game1.playSound("bigSelect");
                            string destinationName = phoneAppKeypadBuffer;
                            var customMatch = phoneAppContacts.FirstOrDefault(cc => cc.Number == phoneAppKeypadBuffer);
                            bool isNpcCall = ModEntry.NpcNumbers.TryGetValue(phoneAppKeypadBuffer, out string npcName);

                            if (customMatch != null)
                            {
                                destinationName = customMatch.Name;
                            }
                            else if (isNpcCall)
                            {
                                var character = Game1.getCharacterFromName(npcName);
                                destinationName = character != null ? character.displayName : npcName;
                            }

                            ExecuteCallAction(destinationName, phoneAppKeypadBuffer, isNpcCall);
                        }
                        else Game1.playSound("cancel");
                        phoneAppClickHandledOnPress = true;
                        return;
                    }

                    if (phoneAppKeypadBuffer.Length > 0)
                    {
                        Rectangle clearBtn = new Rectangle(keyStartX + (2 * (btnSize + spaceX)), actionRowY + (btnSize - ScaleUiValue(45)) / 2, btnSize, ScaleUiValue(45));
                        if (clearBtn.Contains(x, y))
                        {
                            phoneAppKeypadBuffer = phoneAppKeypadBuffer.Substring(0, phoneAppKeypadBuffer.Length - 1);
                            Game1.playSound("thudStep");
                            phoneAppClickHandledOnPress = true;
                            return;
                        }
                    }
                }
            }
        }

        private void ExecuteCallAction(string name, string number, bool isNpc)
        {
            string rawSeason = Game1.currentSeason;
            string readableSeason = Utility.getSeasonNameFromNumber(Utility.getSeasonNumber(rawSeason));
            string timeStrVal = Game1.getTimeOfDayString(Game1.timeOfDay);
            string timestamp = ModEntry.SHelper.Translation.Get("ui.phone.time_format", new { seasonName = readableSeason, day = Game1.dayOfMonth, time = timeStrVal });

            bool reallyNpc = ModEntry.NpcNumbers.TryGetValue(number, out string npcInternalName);
            string savedName = reallyNpc ? npcInternalName : name;

            phoneAppRecentCalls.Insert(0, new RecentCall { Name = savedName, Number = number, TimeText = timestamp });
            if (phoneAppRecentCalls.Count > 40) phoneAppRecentCalls.RemoveAt(phoneAppRecentCalls.Count - 1);
            SavePhoneAppData();

            ClosePhoneMenu();

            if (reallyNpc)
            {
                NPC targetNpc = Game1.getCharacterFromName(npcInternalName);
                if (targetNpc != null)
                {
                    ModEntry.LaunchNpcPhoneDialogue(targetNpc);
                }
            }
            else
            {
                Game1.activeClickableMenu = new DialogueBox(ModEntry.SHelper.Translation.Get("ui.phone.calling_no_answer", new { name = name, number = number }));
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
                if (phoneAppIsConfirmingDelete || phoneAppIsEditingExistingContact)
                    return;

                Rectangle topActionBtn = new Rectangle(bounds.Right - ScaleUiValue(85), bounds.Y + ScaleUiValue(10), ScaleUiValue(70), ScaleUiValue(60));
                if (topActionBtn.Contains(x, y))
                    return;

                var list = GetSortedContacts();
                int currentY = contentY - phoneAppContactsScroll;
                foreach (var item in list)
                {
                    Rectangle rowRect = new Rectangle(bounds.X + ScaleUiValue(15), currentY, bounds.Width - ScaleUiValue(30), rowHeight);
                    if (clipArea.Contains(x, y) && rowRect.Contains(x, y))
                    {
                        int origIdx = phoneAppContacts.IndexOf(item.OriginalContact);
                        if (phoneAppIsEditingContacts)
                        {
                            Rectangle delBtn = new Rectangle(rowRect.Right - ScaleUiValue(75), rowRect.Y + ScaleUiValue(8), ScaleUiValue(65), rowRect.Height - ScaleUiValue(16));
                            if (delBtn.Contains(x, y))
                            {
                                if (origIdx >= 0)
                                {
                                    phoneAppIsConfirmingDelete = true;
                                    phoneAppDeletingContactIndex = origIdx;
                                    Game1.playSound("smallSelect");
                                }
                                return;
                            }

                            if (origIdx >= 0)
                            {
                                phoneAppIsEditingExistingContact = true;
                                phoneAppEditingContactIndex = origIdx;
                                phoneAppNewContactName = item.OriginalContact.Name;
                                phoneAppKeypadBuffer = item.OriginalContact.Number;
                                phoneAppActiveField = 0;
                                Game1.playSound("bigSelect");
                            }
                        }
                        else
                        {
                            Game1.playSound("smallSelect");
                            phoneAppSelectedContactDetail = item;
                            phoneAppContactDetailScroll = 0;
                            phoneAppViewingContactDetail = true;
                        }
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
                        bool isNpc = ModEntry.NpcNumbers.ContainsKey(call.Number);
                        ExecuteCallAction(call.Name, call.Number, isNpc);
                        return;
                    }
                    currentY += rowHeight + ScaleUiValue(6);
                }
            }
        }


        public void HandlePhoneAppKeyPress(Keys key)
        {
            if (phoneAppViewingContactDetail) return;

            if (phoneAppCurrentTab == 0 && !phoneAppIsAddingContact && !phoneAppIsEditingExistingContact && !phoneAppIsConfirmingDelete)
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
                return;
            }

            if (phoneAppCurrentTab == 2 && !phoneAppIsAddingContact && !phoneAppIsEditingExistingContact)
            {
                if (key == Keys.Enter)
                {
                    if (phoneAppKeypadBuffer.Length > 0)
                    {
                        Game1.playSound("bigSelect");
                        string destinationName = phoneAppKeypadBuffer;
                        var customMatch = phoneAppContacts.FirstOrDefault(cc => cc.Number == phoneAppKeypadBuffer);
                        bool isNpcCall = ModEntry.NpcNumbers.TryGetValue(phoneAppKeypadBuffer, out string npcName);

                        if (customMatch != null)
                        {
                            destinationName = customMatch.Name;
                        }
                        else if (isNpcCall)
                        {
                            var character = Game1.getCharacterFromName(npcName);
                            destinationName = character != null ? character.displayName : npcName;
                        }

                        ExecuteCallAction(destinationName, phoneAppKeypadBuffer, isNpcCall);
                    }
                    else
                    {
                        Game1.playSound("cancel");
                    }
                    return;
                }

                if (key == Keys.Back)
                {
                    if (phoneAppKeypadBuffer.Length > 0)
                    {
                        phoneAppKeypadBuffer = phoneAppKeypadBuffer.Substring(0, phoneAppKeypadBuffer.Length - 1);
                        Game1.playSound("thudStep");
                    }
                    return;
                }

                string inputChar = "";
                if (key >= Keys.D0 && key <= Keys.D9) inputChar = (key - Keys.D0).ToString();
                else if (key >= Keys.NumPad0 && key <= Keys.NumPad9) inputChar = (key - Keys.NumPad0).ToString();
                else if (key == Keys.OemPlus || key == Keys.Add) inputChar = "+";

                bool isShift = Game1.oldKBState.IsKeyDown(Keys.LeftShift) || Game1.oldKBState.IsKeyDown(Keys.RightShift);
                if (isShift && key == Keys.D8) inputChar = "*";
                if (isShift && key == Keys.D3) inputChar = "#";

                if (inputChar.Length == 1)
                {
                    if (phoneAppKeypadBuffer.Length < 15)
                    {
                        phoneAppKeypadBuffer += inputChar;
                        Game1.playSound("clank");
                    }
                }
                return;
            }

            if (!phoneAppIsAddingContact && !phoneAppIsEditingExistingContact) return;

            if (key == Keys.Back)
            {
                if (phoneAppActiveField == 0 && phoneAppNewContactName.Length > 0)
                {
                    phoneAppNewContactName = phoneAppNewContactName.Substring(0, phoneAppNewContactName.Length - 1);
                    Game1.playSound("thudStep");
                }
                else if (phoneAppActiveField == 1 && phoneAppKeypadBuffer.Length > 0)
                {
                    phoneAppKeypadBuffer = phoneAppKeypadBuffer.Substring(0, phoneAppKeypadBuffer.Length - 1);
                    Game1.playSound("thudStep");
                }
                return;
            }

            string inputCharForEdit = key.ToString();
            if (key >= Keys.D0 && key <= Keys.D9) inputCharForEdit = (key - Keys.D0).ToString();
            else if (key >= Keys.NumPad0 && key <= Keys.NumPad9) inputCharForEdit = (key - Keys.NumPad0).ToString();
            else if (key == Keys.OemPlus || key == Keys.Add) inputCharForEdit = "+";

            if (inputCharForEdit.Length == 1)
            {
                if (phoneAppActiveField == 0 && phoneAppNewContactName.Length < 15)
                {
                    bool isShift = Game1.oldKBState.IsKeyDown(Keys.LeftShift) || Game1.oldKBState.IsKeyDown(Keys.RightShift);
                    phoneAppNewContactName += isShift ? inputCharForEdit.ToUpper() : inputCharForEdit.ToLower();
                }
                else if (phoneAppActiveField == 1 && phoneAppKeypadBuffer.Length < 15)
                {
                    phoneAppKeypadBuffer += inputCharForEdit;
                }
            }
            else if (key == Keys.Space && phoneAppActiveField == 0 && phoneAppNewContactName.Length < 15)
            {
                phoneAppNewContactName += " ";
            }
        }

        public static void AssignNpcNumber()
        {
            ModEntry.NpcNumbers.Clear();
            HashSet<string> existingNumbers = new HashSet<string>();

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

            Utility.ForEachVillager(npc =>
            {
                if (npc != null && npc.CanSocialize)
                {
                    if (blacklist.Contains(npc.Name))
                    {
                        if (npc.modData.ContainsKey("d5a1lamdtd.Smartphone.PhoneNumber"))
                        {
                            npc.modData.Remove("d5a1lamdtd.Smartphone.PhoneNumber");
                        }
                        return true;
                    }

                    if (npc.modData.TryGetValue("d5a1lamdtd.Smartphone.PhoneNumber", out string existingNum) && !string.IsNullOrWhiteSpace(existingNum))
                    {
                        existingNumbers.Add(existingNum);
                        ModEntry.NpcNumbers[existingNum] = npc.Name;
                    }
                }
                return true;
            });

            Random rand = new Random();
            Utility.ForEachVillager(npc =>
            {
                if (npc != null && npc.CanSocialize)
                {
                    if (blacklist.Contains(npc.Name))
                        return true;

                    if (!npc.modData.ContainsKey("d5a1lamdtd.Smartphone.PhoneNumber") || string.IsNullOrWhiteSpace(npc.modData["d5a1lamdtd.Smartphone.PhoneNumber"]))
                    {
                        string newNumber;
                        do
                        {
                            newNumber = "0" + rand.Next(0, 100000).ToString("D5");
                        } while (existingNumbers.Contains(newNumber));

                        existingNumbers.Add(newNumber);
                        npc.modData["d5a1lamdtd.Smartphone.PhoneNumber"] = newNumber;
                        ModEntry.NpcNumbers[newNumber] = npc.Name;
                    }
                }
                return true;
            });
            ModEntry.NotifyContactableNpcsChanged();
        }
    }
}