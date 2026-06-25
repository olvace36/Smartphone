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

        // Form & Configuration Navigation States
        private bool phoneAppIsAddingContact = false;
        private bool phoneAppIsEditingContacts = false;
        private bool phoneAppIsEditingExistingContact = false;
        private bool phoneAppIsConfirmingDelete = false;

        private int phoneAppEditingContactIndex = -1;
        private int phoneAppDeletingContactIndex = -1;
        private int phoneAppActiveField = 0; // 0 = Name, 1 = Number
        private string phoneAppNewContactName = "";

        private List<RecentCall> phoneAppRecentCalls = new();
        private List<Contact> phoneAppContacts = new();
        private bool phoneAppDataLoaded = false;

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
                }
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor?.Log($"Error loading phone application data: {ex.Message}", LogLevel.Error);
            }
        }

        private void SavePhoneAppData()
        {
            try
            {
                string folderPath = Path.Combine(ModEntry.SHelper.DirectoryPath, "userdata", ModEntry.GetActiveSaveFolderName());
                Directory.CreateDirectory(folderPath);
                string filePath = Path.Combine(folderPath, "phone_app_data.json");

                var data = new { RecentCalls = phoneAppRecentCalls, Contacts = phoneAppContacts };
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(filePath, json);
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

            return list.OrderBy(c =>
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
        }

        public void DrawPhoneApp(SpriteBatch b)
        {
            EnsurePhoneAppDataLoaded();
            Rectangle bounds = GetPhoneContentBounds();

            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.45f);
            DrawPhoneScreenBackground(b, 0, applyBackgroundImage: true);

            int headerHeight = ScaleUiValue(60);
            int tabHeight = ScaleUiValue(70);
            int contentY = bounds.Y + headerHeight + ScaleUiValue(5);
            int contentHeight = bounds.Height - headerHeight - tabHeight - ScaleUiValue(10);
            Rectangle clipArea = new Rectangle(bounds.X, contentY, bounds.Width, contentHeight);

            // --- HEADER TITLE PROCESSING ---
            string title = phoneAppCurrentTab == 0 ? "Contacts" : (phoneAppCurrentTab == 1 ? "Recents" : "Keypad");
            if (phoneAppIsAddingContact) title = "New Contact";
            if (phoneAppIsEditingExistingContact) title = "Edit Contact";

            Vector2 titleSize = Game1.dialogueFont.MeasureString(title) * 0.55f;
            b.DrawString(Game1.dialogueFont, title, new Vector2(bounds.X + (bounds.Width - titleSize.X) / 2, bounds.Y + ScaleUiValue(12)), Color.Black, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 1f);
            b.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Y + headerHeight, bounds.Width, ScaleUiValue(2)), Color.LightGray);

            // --- HEADERS ACTION DRAW BUTTONS ---
            Rectangle editToggleBtn = new Rectangle(bounds.Right - ScaleUiValue(85), bounds.Y + ScaleUiValue(12), ScaleUiValue(70), ScaleUiValue(35));
            if (phoneAppCurrentTab == 0 && !phoneAppIsAddingContact && !phoneAppIsEditingExistingContact && !phoneAppIsConfirmingDelete)
            {
                Color btnColor = phoneAppIsEditingContacts ? Color.LightGreen : Color.LightGray;
                Textures.DrawCard(b, editToggleBtn.X, editToggleBtn.Y, editToggleBtn.Width, editToggleBtn.Height, btnColor);
                string btnText = phoneAppIsEditingContacts ? "Done" : "Edit";
                Vector2 sz = Game1.smallFont.MeasureString(btnText) * 0.75f;
                b.DrawString(Game1.smallFont, btnText, new Vector2(editToggleBtn.X + (editToggleBtn.Width - sz.X) / 2, editToggleBtn.Y + (editToggleBtn.Height - sz.Y) / 2), Color.Black, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 1f);
            }

            // --- BOTTOM TAB BAR LAYOUT ---
            Rectangle tabBackground = new Rectangle(bounds.X, bounds.Y + bounds.Height - tabHeight, bounds.Width, tabHeight);
            b.Draw(Game1.staminaRect, tabBackground, Color.Black * 0.08f);
            b.Draw(Game1.staminaRect, new Rectangle(bounds.X, tabBackground.Y, bounds.Width, ScaleUiValue(1)), Color.LightGray);

            int tabCellWidth = bounds.Width / 3;
            Color colorContacts = phoneAppCurrentTab == 0 ? Color.DodgerBlue : Color.DimGray;
            Color colorRecents = phoneAppCurrentTab == 1 ? Color.DodgerBlue : Color.DimGray;
            Color colorKeypad = phoneAppCurrentTab == 2 ? Color.DodgerBlue : Color.DimGray;

            Vector2 sContacts = Game1.smallFont.MeasureString("Contacts") * 0.85f;
            Vector2 sRecents = Game1.smallFont.MeasureString("Recents") * 0.85f;
            Vector2 sKeypad = Game1.smallFont.MeasureString("Keypad") * 0.85f;

            b.DrawString(Game1.smallFont, "Contacts", new Vector2(bounds.X + (tabCellWidth - sContacts.X) / 2, tabBackground.Y + (tabHeight - sContacts.Y) / 2), colorContacts, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 1f);
            b.DrawString(Game1.smallFont, "Recents", new Vector2(bounds.X + tabCellWidth + (tabCellWidth - sRecents.X) / 2, tabBackground.Y + (tabHeight - sRecents.Y) / 2), colorRecents, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 1f);
            b.DrawString(Game1.smallFont, "Keypad", new Vector2(bounds.X + tabCellWidth * 2 + (bounds.Width - tabCellWidth * 2 - sKeypad.X) / 2, tabBackground.Y + (tabHeight - sKeypad.Y) / 2), colorKeypad, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 1f);

            // --- CORE TABS RENDER CONTENT ---
            int rowHeight = ScaleUiValue(55);

            if (phoneAppCurrentTab == 0 || phoneAppCurrentTab == 1)
            {
                if (phoneAppIsConfirmingDelete)
                {
                    Rectangle confBox = new Rectangle(bounds.X + ScaleUiValue(20), contentY + ScaleUiValue(50), bounds.Width - ScaleUiValue(40), ScaleUiValue(200));
                    Textures.DrawCard(b, confBox.X, confBox.Y, confBox.Width, confBox.Height, Color.White);

                    string confText = "Delete this contact?";
                    Vector2 txSz = Game1.smallFont.MeasureString(confText);
                    b.DrawString(Game1.smallFont, confText, new Vector2(confBox.X + (confBox.Width - txSz.X) / 2, confBox.Y + ScaleUiValue(30)), Color.Black);

                    Rectangle btnYes = new Rectangle(confBox.X + ScaleUiValue(20), confBox.Y + ScaleUiValue(110), confBox.Width / 2 - ScaleUiValue(30), ScaleUiValue(50));
                    Rectangle btnNo = new Rectangle(confBox.X + confBox.Width / 2 + ScaleUiValue(10), confBox.Y + ScaleUiValue(110), confBox.Width / 2 - ScaleUiValue(30), ScaleUiValue(50));

                    Textures.DrawCard(b, btnYes.X, btnYes.Y, btnYes.Width, btnYes.Height, Color.LightCoral);
                    Textures.DrawCard(b, btnNo.X, btnNo.Y, btnNo.Width, btnNo.Height, Color.LightGray);

                    Vector2 ySz = Game1.smallFont.MeasureString("Yes") * 0.9f;
                    Vector2 nSz = Game1.smallFont.MeasureString("No") * 0.9f;

                    b.DrawString(Game1.smallFont, "Yes", new Vector2(btnYes.X + (btnYes.Width - ySz.X) / 2, btnYes.Y + (btnYes.Height - ySz.Y) / 2), Color.Black, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 1f);
                    b.DrawString(Game1.smallFont, "No", new Vector2(btnNo.X + (btnNo.Width - nSz.X) / 2, btnNo.Y + (btnNo.Height - nSz.Y) / 2), Color.Black, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 1f);
                }
                else if (phoneAppIsEditingExistingContact)
                {
                    int fX = bounds.X + ScaleUiValue(30);
                    int fW = bounds.Width - ScaleUiValue(60);

                    b.DrawString(Game1.smallFont, "Edit Contact Name:", new Vector2(fX, contentY + ScaleUiValue(20)), Color.Black, 0f, Vector2.Zero, 0.95f, SpriteEffects.None, 1f);
                    Rectangle nameFieldRect = new Rectangle(fX, contentY + ScaleUiValue(50), fW, ScaleUiValue(45));
                    Textures.DrawCard(b, nameFieldRect.X, nameFieldRect.Y, nameFieldRect.Width, nameFieldRect.Height, phoneAppActiveField == 0 ? Color.LightCyan : Color.White);

                    string printableName = phoneAppNewContactName;
                    if (phoneAppActiveField == 0 && (DateTime.UtcNow.Millisecond / 500) % 2 == 0) printableName += "|";
                    b.DrawString(Game1.smallFont, printableName, new Vector2(nameFieldRect.X + ScaleUiValue(10), nameFieldRect.Y + ScaleUiValue(12)), Color.Black, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 1f);

                    b.DrawString(Game1.smallFont, "Edit Contact Number:", new Vector2(fX, contentY + ScaleUiValue(115)), Color.Black, 0f, Vector2.Zero, 0.95f, SpriteEffects.None, 1f);
                    Rectangle numFieldRect = new Rectangle(fX, contentY + ScaleUiValue(145), fW, ScaleUiValue(45));
                    Textures.DrawCard(b, numFieldRect.X, numFieldRect.Y, numFieldRect.Width, numFieldRect.Height, phoneAppActiveField == 1 ? Color.LightCyan : Color.White);

                    string printableNum = phoneAppKeypadBuffer;
                    if (phoneAppActiveField == 1 && (DateTime.UtcNow.Millisecond / 500) % 2 == 0) printableNum += "|";
                    b.DrawString(Game1.smallFont, printableNum, new Vector2(numFieldRect.X + ScaleUiValue(10), numFieldRect.Y + ScaleUiValue(12)), Color.Black, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 1f);

                    Rectangle btnSave = new Rectangle(fX, contentY + ScaleUiValue(225), fW / 2 - ScaleUiValue(8), ScaleUiValue(45));
                    Rectangle btnCancel = new Rectangle(fX + fW / 2 + ScaleUiValue(8), contentY + ScaleUiValue(225), fW / 2 - ScaleUiValue(8), ScaleUiValue(45));

                    Textures.DrawCard(b, btnSave.X, btnSave.Y, btnSave.Width, btnSave.Height, Color.PaleGreen);
                    Textures.DrawCard(b, btnCancel.X, btnCancel.Y, btnCancel.Width, btnCancel.Height, Color.LightCoral);

                    Vector2 sSz = Game1.smallFont.MeasureString("Save") * 0.9f;
                    Vector2 cSz = Game1.smallFont.MeasureString("Cancel") * 0.9f;
                    b.DrawString(Game1.smallFont, "Save", new Vector2(btnSave.X + (btnSave.Width - sSz.X) / 2, btnSave.Y + (btnSave.Height - sSz.Y) / 2), Color.Black, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 1f);
                    b.DrawString(Game1.smallFont, "Cancel", new Vector2(btnCancel.X + (btnCancel.Width - cSz.X) / 2, btnCancel.Y + (btnCancel.Height - cSz.Y) / 2), Color.Black, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 1f);
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

                            b.DrawString(Game1.smallFont, displayNameStr, new Vector2(cardRect.X + ScaleUiValue(15), cardRect.Y + (cardRect.Height - Game1.smallFont.LineSpacing * 0.85f) / 2), Color.Black, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 1f);

                            if (phoneAppIsEditingContacts)
                            {
                                Rectangle delBtn = new Rectangle(cardRect.Right - ScaleUiValue(75), cardRect.Y + ScaleUiValue(8), ScaleUiValue(65), cardRect.Height - ScaleUiValue(16));
                                Textures.DrawCard(b, delBtn.X, delBtn.Y, delBtn.Width, delBtn.Height, Color.Tomato);
                                Vector2 delSz = Game1.smallFont.MeasureString("Delete") * 0.7f;
                                b.DrawString(Game1.smallFont, "Delete", new Vector2(delBtn.X + (delBtn.Width - delSz.X) / 2, delBtn.Y + (delBtn.Height - delSz.Y) / 2), Color.White, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 1f);
                            }
                            else
                            {
                                Vector2 labelSize = Game1.smallFont.MeasureString(contact.Number) * 0.65f;
                                b.DrawString(Game1.smallFont, contact.Number, new Vector2(cardRect.Right - labelSize.X - ScaleUiValue(15), cardRect.Y + (cardRect.Height - labelSize.Y) / 2), Color.Gray, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 1f);
                            }
                            currentY += rowHeight + ScaleUiValue(6);
                        }

                        if (contacts.Count == 0)
                        {
                            Vector2 noContactSz = Game1.smallFont.MeasureString("No Contacts Added");
                            b.DrawString(Game1.smallFont, "No Contacts Added", new Vector2(bounds.X + (bounds.Width - noContactSz.X) / 2, contentY + ScaleUiValue(60)), Color.Gray, 0f, Vector2.Zero, 1f, SpriteEffects.None, 1f);
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

                            b.DrawString(Game1.smallFont, nameText, new Vector2(cardRect.X + ScaleUiValue(15), cardRect.Y + ScaleUiValue(6)), Color.Black, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 1f);
                            b.DrawString(Game1.smallFont, call.TimeText, new Vector2(cardRect.X + ScaleUiValue(15), cardRect.Y + ScaleUiValue(30)), Color.Gray, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 1f);

                            if (!string.IsNullOrEmpty(call.Number) && call.Number != call.Name)
                            {
                                Vector2 numSize = Game1.smallFont.MeasureString(call.Number) * 0.7f;
                                b.DrawString(Game1.smallFont, call.Number, new Vector2(cardRect.Right - numSize.X - ScaleUiValue(15), cardRect.Y + (cardRect.Height - numSize.Y) / 2), Color.DimGray, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 1f);
                            }
                            currentY += rowHeight + ScaleUiValue(6);
                        }

                        if (phoneAppRecentCalls.Count == 0)
                        {
                            Vector2 noCallSz = Game1.smallFont.MeasureString("No Recent Calls");
                            b.DrawString(Game1.smallFont, "No Recent Calls", new Vector2(bounds.X + (bounds.Width - noCallSz.X) / 2, contentY + ScaleUiValue(60)), Color.Gray, 0f, Vector2.Zero, 1f, SpriteEffects.None, 1f);
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

                    b.DrawString(Game1.smallFont, "Contact Name:", new Vector2(fX, contentY + ScaleUiValue(20)), Color.Black, 0f, Vector2.Zero, 0.95f, SpriteEffects.None, 1f);
                    Rectangle fieldRect = new Rectangle(fX, contentY + ScaleUiValue(50), fW, ScaleUiValue(45));
                    Textures.DrawCard(b, fieldRect.X, fieldRect.Y, fieldRect.Width, fieldRect.Height, Color.White);

                    string printable = phoneAppNewContactName;
                    if ((DateTime.UtcNow.Millisecond / 500) % 2 == 0) printable += "|";
                    b.DrawString(Game1.smallFont, printable, new Vector2(fieldRect.X + ScaleUiValue(10), fieldRect.Y + ScaleUiValue(12)), Color.Black, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 1f);

                    b.DrawString(Game1.smallFont, $"Number: {phoneAppKeypadBuffer}", new Vector2(fX, contentY + ScaleUiValue(115)), Color.DarkSlateGray, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 1f);

                    Rectangle btnSave = new Rectangle(fX, contentY + ScaleUiValue(175), fW / 2 - ScaleUiValue(8), ScaleUiValue(45));
                    Rectangle btnCancel = new Rectangle(fX + fW / 2 + ScaleUiValue(8), contentY + ScaleUiValue(175), fW / 2 - ScaleUiValue(8), ScaleUiValue(45));

                    Textures.DrawCard(b, btnSave.X, btnSave.Y, btnSave.Width, btnSave.Height, Color.PaleGreen);
                    Textures.DrawCard(b, btnCancel.X, btnCancel.Y, btnCancel.Width, btnCancel.Height, Color.LightCoral);

                    Vector2 sSz = Game1.smallFont.MeasureString("Save") * 0.9f;
                    Vector2 cSz = Game1.smallFont.MeasureString("Cancel") * 0.9f;
                    b.DrawString(Game1.smallFont, "Save", new Vector2(btnSave.X + (btnSave.Width - sSz.X) / 2, btnSave.Y + (btnSave.Height - sSz.Y) / 2), Color.Black, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 1f);
                    b.DrawString(Game1.smallFont, "Cancel", new Vector2(btnCancel.X + (btnCancel.Width - cSz.X) / 2, btnCancel.Y + (btnCancel.Height - cSz.Y) / 2), Color.Black, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 1f);
                }
                else
                {
                    Rectangle dispBox = new Rectangle(bounds.X + ScaleUiValue(25), contentY + ScaleUiValue(15), bounds.Width - ScaleUiValue(50), ScaleUiValue(55));
                    Textures.DrawCard(b, dispBox.X, dispBox.Y, dispBox.Width, dispBox.Height, Color.White * 0.45f);

                    Vector2 bufSz = Game1.dialogueFont.MeasureString(phoneAppKeypadBuffer) * 0.65f;
                    b.DrawString(Game1.dialogueFont, phoneAppKeypadBuffer, new Vector2(dispBox.X + (dispBox.Width - bufSz.X) / 2, dispBox.Y + (dispBox.Height - bufSz.Y) / 2), Color.Black, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 1f);

                    if (phoneAppKeypadBuffer.Length > 0)
                    {
                        Rectangle btnAdd = new Rectangle(dispBox.Right - ScaleUiValue(55), dispBox.Y + ScaleUiValue(8), ScaleUiValue(48), ScaleUiValue(38));
                        Textures.DrawCard(b, btnAdd.X, btnAdd.Y, btnAdd.Width, btnAdd.Height, Color.LightSkyBlue);
                        Vector2 addSz = Game1.smallFont.MeasureString("Add") * 0.7f;
                        b.DrawString(Game1.smallFont, "Add", new Vector2(btnAdd.X + (btnAdd.Width - addSz.X) / 2, btnAdd.Y + (btnAdd.Height - addSz.Y) / 2), Color.Black, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 1f);
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
                            string textVal = numbers[r, c];
                            if (string.IsNullOrWhiteSpace(textVal))
                                continue;
                            Rectangle keyRect = new Rectangle(keyStartX + c * (btnSize + spaceX), keyStartY + r * (btnSize + spaceY), btnSize, btnSize);
                            Textures.DrawCard(b, keyRect.X, keyRect.Y, keyRect.Width, keyRect.Height, Color.LightGray * 0.75f);

                            Vector2 keySz = Game1.dialogueFont.MeasureString(textVal) * 0.6f;
                            b.DrawString(Game1.dialogueFont, textVal, new Vector2(keyRect.X + (keyRect.Width - keySz.X) / 2, keyRect.Y + (keyRect.Height - keySz.Y) / 2), Color.Black, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 1f);
                        }
                    }

                    int actionRowY = keyStartY + 4 * (btnSize + spaceY) + ScaleUiValue(5);
                    Rectangle callBtn = new Rectangle(keyStartX + btnSize + spaceX, actionRowY, btnSize, btnSize);
                    Textures.DrawCard(b, callBtn.X, callBtn.Y, callBtn.Width, callBtn.Height, Color.LimeGreen);
                    Vector2 callTextSz = Game1.smallFont.MeasureString("Call") * 0.85f;
                    b.DrawString(Game1.smallFont, "Call", new Vector2(callBtn.X + (callBtn.Width - callTextSz.X) / 2, callBtn.Y + (callBtn.Height - callTextSz.Y) / 2), Color.White, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 1f);

                    if (phoneAppKeypadBuffer.Length > 0)
                    {
                        Rectangle clearBtn = new Rectangle(keyStartX + (2 * (btnSize + spaceX)), actionRowY + (btnSize - ScaleUiValue(45)) / 2, btnSize, ScaleUiValue(45));
                        Textures.DrawCard(b, clearBtn.X, clearBtn.Y, clearBtn.Width, clearBtn.Height, Color.LightGray);
                        Vector2 backTextSz = Game1.smallFont.MeasureString("<") * 0.8f;
                        b.DrawString(Game1.smallFont, "<", new Vector2(clearBtn.X + (clearBtn.Width - backTextSz.X) / 2, clearBtn.Y + (clearBtn.Height - backTextSz.Y) / 2), Color.Black, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 1f);
                    }
                }
            }

            Texture2D frameTex = Textures.PhoneEmpty;
            if (frameTex != null && !frameTex.IsDisposed)
            {
                float uiScale = ModEntry.GetActivePhoneUiScale();
                b.Draw(frameTex, new Vector2(ModEntry.currentMenuX, ModEntry.currentMenuY), null, Color.White, 0f, Vector2.Zero, uiScale, SpriteEffects.None, 0.99f);
            }
        }

        public void ReceiveLeftClickPhoneApp(int x, int y)
        {
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
                    Game1.playSound("shwip");
                }
                return;
            }

            int headerHeight = ScaleUiValue(60);
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
                        return;
                    }
                    if (btnNo.Contains(x, y))
                    {
                        phoneAppIsConfirmingDelete = false;
                        phoneAppDeletingContactIndex = -1;
                        Game1.playSound("cancel");
                        return;
                    }
                    return;
                }

                if (phoneAppIsEditingExistingContact)
                {
                    int fX = bounds.X + ScaleUiValue(30);
                    int fW = bounds.Width - ScaleUiValue(60);
                    Rectangle nameFieldRect = new Rectangle(fX, contentY + ScaleUiValue(50), fW, ScaleUiValue(45));
                    Rectangle numFieldRect = new Rectangle(fX, contentY + ScaleUiValue(145), fW, ScaleUiValue(45));
                    Rectangle btnSave = new Rectangle(fX, contentY + ScaleUiValue(225), fW / 2 - ScaleUiValue(8), ScaleUiValue(45));
                    Rectangle btnCancel = new Rectangle(fX + fW / 2 + ScaleUiValue(8), contentY + ScaleUiValue(225), fW / 2 - ScaleUiValue(8), ScaleUiValue(45));

                    if (nameFieldRect.Contains(x, y))
                    {
                        phoneAppActiveField = 0;
                        Game1.playSound("smallSelect");
                        return;
                    }
                    if (numFieldRect.Contains(x, y))
                    {
                        phoneAppActiveField = 1;
                        Game1.playSound("smallSelect");
                        return;
                    }
                    if (btnSave.Contains(x, y))
                    {
                        if (!string.IsNullOrWhiteSpace(phoneAppNewContactName) && !string.IsNullOrWhiteSpace(phoneAppKeypadBuffer))
                        {
                            if (phoneAppEditingContactIndex >= 0 && phoneAppEditingContactIndex < phoneAppContacts.Count)
                            {
                                phoneAppContacts[phoneAppEditingContactIndex].Name = phoneAppNewContactName.Trim();
                                phoneAppContacts[phoneAppEditingContactIndex].Number = phoneAppKeypadBuffer.Trim();
                                SavePhoneAppData();
                                Game1.playSound("coin");
                            }
                            phoneAppIsEditingExistingContact = false;
                            phoneAppEditingContactIndex = -1;
                        }
                        else Game1.playSound("cancel");
                        return;
                    }
                    if (btnCancel.Contains(x, y))
                    {
                        phoneAppIsEditingExistingContact = false;
                        phoneAppEditingContactIndex = -1;
                        Game1.playSound("cancel");
                        return;
                    }
                    return;
                }

                Rectangle editToggleBtn = new Rectangle(bounds.Right - ScaleUiValue(85), bounds.Y + ScaleUiValue(12), ScaleUiValue(70), ScaleUiValue(35));
                if (editToggleBtn.Contains(x, y))
                {
                    phoneAppIsEditingContacts = !phoneAppIsEditingContacts;
                    Game1.playSound("shwip");
                    return;
                }

                var list = GetSortedContacts();
                int currentY = contentY - phoneAppContactsScroll;
                foreach (var item in list)
                {
                    Rectangle rowRect = new Rectangle(bounds.X + ScaleUiValue(15), currentY, bounds.Width - ScaleUiValue(30), ScaleUiValue(55));
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
                            Game1.playSound("bigSelect");
                            ExecuteCallAction(item.Name, item.Number, item.IsNpc);
                        }
                        return;
                    }
                    currentY += ScaleUiValue(55) + ScaleUiValue(6);
                }
            }
            else if (phoneAppCurrentTab == 1) // Recents Click Actions
            {
                int currentY = contentY - phoneAppRecentsScroll;
                foreach (var call in phoneAppRecentCalls)
                {
                    Rectangle rowRect = new Rectangle(bounds.X + ScaleUiValue(15), currentY, bounds.Width - ScaleUiValue(30), ScaleUiValue(55));
                    if (clipArea.Contains(x, y) && rowRect.Contains(x, y))
                    {
                        Game1.playSound("bigSelect");
                        bool isNpc = ModEntry.NpcNumbers.ContainsKey(call.Number);
                        ExecuteCallAction(call.Name, call.Number, isNpc);
                        return;
                    }
                    currentY += ScaleUiValue(55) + ScaleUiValue(6);
                }
            }
            else if (phoneAppCurrentTab == 2) // Keypad Dialer Click Controls
            {
                if (phoneAppIsAddingContact)
                {
                    int fX = bounds.X + ScaleUiValue(30);
                    int fW = bounds.Width - ScaleUiValue(60);
                    Rectangle btnSave = new Rectangle(fX, contentY + ScaleUiValue(175), fW / 2 - ScaleUiValue(8), ScaleUiValue(45));
                    Rectangle btnCancel = new Rectangle(fX + fW / 2 + ScaleUiValue(8), contentY + ScaleUiValue(175), fW / 2 - ScaleUiValue(8), ScaleUiValue(45));

                    if (btnSave.Contains(x, y))
                    {
                        if (!string.IsNullOrWhiteSpace(phoneAppNewContactName))
                        {
                            phoneAppContacts.Add(new Contact { Name = phoneAppNewContactName.Trim(), Number = phoneAppKeypadBuffer });
                            SavePhoneAppData();
                            phoneAppIsAddingContact = false;
                            phoneAppCurrentTab = 0;
                            Game1.playSound("coin");
                        }
                        else Game1.playSound("cancel");
                        return;
                    }
                    if (btnCancel.Contains(x, y))
                    {
                        phoneAppIsAddingContact = false;
                        Game1.playSound("cancel");
                        return;
                    }
                }
                else
                {
                    Rectangle dispBox = new Rectangle(bounds.X + ScaleUiValue(25), contentY + ScaleUiValue(15), bounds.Width - ScaleUiValue(50), ScaleUiValue(55));

                    if (phoneAppKeypadBuffer.Length > 0)
                    {
                        Rectangle btnAdd = new Rectangle(dispBox.Right - ScaleUiValue(55), dispBox.Y + ScaleUiValue(8), ScaleUiValue(48), ScaleUiValue(38));
                        if (btnAdd.Contains(x, y))
                        {
                            phoneAppIsAddingContact = true;
                            phoneAppActiveField = 0;

                            // Pre-populate box with NPC name if number matches an NPC, while leaving it customizable
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
                            return;
                        }
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
                        return;
                    }

                    if (phoneAppKeypadBuffer.Length > 0)
                    {
                        Rectangle clearBtn = new Rectangle(keyStartX + (2 * (btnSize + spaceX)), actionRowY + (btnSize - ScaleUiValue(45)) / 2, btnSize, ScaleUiValue(45));
                        if (clearBtn.Contains(x, y))
                        {
                            phoneAppKeypadBuffer = phoneAppKeypadBuffer.Substring(0, phoneAppKeypadBuffer.Length - 1);
                            Game1.playSound("thudStep");
                        }
                    }
                }
            }
        }

        private void ExecuteCallAction(string name, string number, bool isNpc)
        {
            string rawSeason = Game1.currentSeason;
            string readableSeason = rawSeason.Length > 0 ? char.ToUpper(rawSeason[0]) + rawSeason.Substring(1) : "";
            string timestamp = $"{readableSeason} {Game1.dayOfMonth}, {Game1.getTimeOfDayString(Game1.timeOfDay)}";

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
                Game1.activeClickableMenu = new DialogueBox($"Calling {name} ({number})... No answer available.");
            }
        }

        public void ReceiveScrollWheelActionPhoneApp(int direction)
        {
            int scrollScale = ScaleUiValue(40);
            if (phoneAppCurrentTab == 0)
            {
                int maxScroll = Math.Max(0, GetSortedContacts().Count * ScaleUiValue(61) - ScaleUiValue(500));
                phoneAppContactsScroll = Math.Clamp(phoneAppContactsScroll + (direction > 0 ? -scrollScale : scrollScale), 0, maxScroll);
            }
            else if (phoneAppCurrentTab == 1)
            {
                int maxScroll = Math.Max(0, phoneAppRecentCalls.Count * ScaleUiValue(61) - ScaleUiValue(500));
                phoneAppRecentsScroll = Math.Clamp(phoneAppRecentsScroll + (direction > 0 ? -scrollScale : scrollScale), 0, maxScroll);
            }
        }

        public void HandlePhoneAppKeyPress(Keys key)
        {
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

            string character = key.ToString();
            if (key >= Keys.D0 && key <= Keys.D9) character = (key - Keys.D0).ToString();
            else if (key >= Keys.NumPad0 && key <= Keys.NumPad9) character = (key - Keys.NumPad0).ToString();

            if (character.Length == 1)
            {
                if (phoneAppActiveField == 0 && phoneAppNewContactName.Length < 15)
                {
                    bool isShift = Game1.oldKBState.IsKeyDown(Keys.LeftShift) || Game1.oldKBState.IsKeyDown(Keys.RightShift);
                    phoneAppNewContactName += isShift ? character.ToUpper() : character.ToLower();
                    Game1.playSound("cowboy_monster_hit");
                }
                else if (phoneAppActiveField == 1 && phoneAppKeypadBuffer.Length < 15)
                {
                    phoneAppKeypadBuffer += character;
                    Game1.playSound("cowboy_monster_hit");
                }
            }
            else if (key == Keys.Space && phoneAppActiveField == 0 && phoneAppNewContactName.Length < 15)
            {
                phoneAppNewContactName += " ";
                Game1.playSound("cowboy_monster_hit");
            }
        }

        public static void AssignNpcNumber()
        {
            // Clear and fully re-build static NPC speed tracking cache context
            ModEntry.NpcNumbers.Clear();
            HashSet<string> existingNumbers = new HashSet<string>();

            Utility.ForEachVillager(npc =>
            {
                if (npc != null && npc.CanSocialize && npc.modData.TryGetValue("d5a1lamdtd.Smartphone.PhoneNumber", out string existingNum) && !string.IsNullOrWhiteSpace(existingNum))
                {
                    existingNumbers.Add(existingNum);
                    ModEntry.NpcNumbers[existingNum] = npc.Name;
                }
                return true;
            });

            Random rand = new Random();
            Utility.ForEachVillager(npc =>
            {
                if (npc != null && npc.CanSocialize)
                {
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
        }
    }
}