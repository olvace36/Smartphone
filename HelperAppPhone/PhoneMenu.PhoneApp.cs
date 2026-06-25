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
using StardewValley.GameData.Shops;

namespace Smartphone
{
    public partial class PhoneMenu : IClickableMenu
    {
        // App State Properties
        private int phoneAppCurrentTab = 0; // 0 = Contacts, 1 = Recents, 2 = Keypad
        private int phoneAppContactsScroll = 0;
        private int phoneAppRecentsScroll = 0;
        private string phoneAppKeypadBuffer = "";
        private bool phoneAppIsAddingContact = false;
        private string phoneAppNewContactName = "";

        private List<RecentCall> phoneAppRecentCalls = new();
        private List<CustomContact> phoneAppCustomContacts = new();
        private bool phoneAppDataLoaded = false;

        public class RecentCall
        {
            public string Name { get; set; } = "";
            public string Number { get; set; } = "";
            public string TimeText { get; set; } = "";
        }

        public class CustomContact
        {
            public string Name { get; set; } = "";
            public string Number { get; set; } = "";
        }

        public class ContactItem
        {
            public string Name { get; set; } = "";
            public string Number { get; set; } = "";
            public bool IsNpc { get; set; }
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
                    if (data["CustomContacts"] != null)
                        phoneAppCustomContacts = data["CustomContacts"].ToObject<List<CustomContact>>() ?? new();
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

                var data = new { RecentCalls = phoneAppRecentCalls, CustomContacts = phoneAppCustomContacts };
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

            foreach (var contact in phoneAppCustomContacts)
            {
                list.Add(new ContactItem { Name = contact.Name, Number = contact.Number, IsNpc = false });
            }

            foreach (var npc in Utility.getAllCharacters())
            {
                if (npc != null && npc.CanSocialize && !string.IsNullOrWhiteSpace(npc.Name))
                {
                    if (!list.Any(i => i.IsNpc && i.Name == npc.Name))
                    {
                        list.Add(new ContactItem { Name = npc.Name, Number = npc.Name, IsNpc = true });
                    }
                }
            }

            return list.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public void DrawPhoneApp(SpriteBatch b)
        {
            EnsurePhoneAppDataLoaded();
            Rectangle bounds = GetPhoneContentBounds();

            // Guarantee background black overlay transparency matrix context matching vanilla
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.45f);

            // Draw baseline screen backgrounds
            DrawPhoneScreenBackground(b, 0, applyBackgroundImage: true);

            int headerHeight = ScaleUiValue(60);
            int tabHeight = ScaleUiValue(70);
            int contentY = bounds.Y + headerHeight + ScaleUiValue(5);
            int contentHeight = bounds.Height - headerHeight - tabHeight - ScaleUiValue(10);
            Rectangle clipArea = new Rectangle(bounds.X, contentY, bounds.Width, contentHeight);

            // --- HEADER ---
            string title = phoneAppCurrentTab == 0 ? "Contacts" : (phoneAppCurrentTab == 1 ? "Recents" : "Keypad");
            if (phoneAppIsAddingContact) title = "New Contact";

            Vector2 titleSize = Game1.dialogueFont.MeasureString(title) * 0.55f;
            b.DrawString(Game1.dialogueFont, title, new Vector2(bounds.X + (bounds.Width - titleSize.X) / 2, bounds.Y + ScaleUiValue(12)), Color.Black, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 1f);
            b.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Y + headerHeight, bounds.Width, ScaleUiValue(2)), Color.LightGray);

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
                // Hardware Scissor Viewport Restriction Matrix Processing Layers
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
                        if (contact.IsNpc)
                        {
                            var character = Game1.getCharacterFromName(contact.Name);
                            // Fix 1: Evaluated lower-case npc.displayName localization mapping property
                            if (character != null) displayNameStr = character.displayName;
                        }

                        b.DrawString(Game1.smallFont, displayNameStr, new Vector2(cardRect.X + ScaleUiValue(15), cardRect.Y + (cardRect.Height - Game1.smallFont.LineSpacing * 0.85f) / 2), Color.Black, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 1f);

                        if (!contact.IsNpc)
                        {
                            Vector2 labelSize = Game1.smallFont.MeasureString(contact.Number) * 0.65f;
                            b.DrawString(Game1.smallFont, contact.Number, new Vector2(cardRect.Right - labelSize.X - ScaleUiValue(15), cardRect.Y + (cardRect.Height - labelSize.Y) / 2), Color.Gray, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 1f);
                        }
                        currentY += rowHeight + ScaleUiValue(6);
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
                        // Fix 1: Evaluated lower-case npc.displayName localization mapping property
                        if (character != null) nameText = character.displayName;

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

                    string[,] numbers = { { "1", "2", "3" }, { "4", "5", "6" }, { "7", "8", "9" }, { "*", "0", "#" } };

                    for (int r = 0; r < 4; r++)
                    {
                        for (int c = 0; c < 3; c++)
                        {
                            Rectangle keyRect = new Rectangle(keyStartX + c * (btnSize + spaceX), keyStartY + r * (btnSize + spaceY), btnSize, btnSize);
                            Textures.DrawCard(b, keyRect.X, keyRect.Y, keyRect.Width, keyRect.Height, Color.LightGray * 0.75f);

                            string textVal = numbers[r, c];
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

            // Draw outer phone empty hardware bezel asset shell securely over current viewport matrix context
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
                    Game1.playSound("shwip");
                }
                return;
            }

            int headerHeight = ScaleUiValue(60);
            int contentY = bounds.Y + headerHeight + ScaleUiValue(5);
            int contentHeight = bounds.Height - headerHeight - tabHeight - ScaleUiValue(10);
            Rectangle clipArea = new Rectangle(bounds.X, contentY, bounds.Width, contentHeight);

            if (phoneAppCurrentTab == 0) // Contacts Click Actions
            {
                var list = GetSortedContacts();
                int currentY = contentY - phoneAppContactsScroll;
                foreach (var contact in list)
                {
                    Rectangle rowRect = new Rectangle(bounds.X + ScaleUiValue(15), currentY, bounds.Width - ScaleUiValue(30), ScaleUiValue(55));
                    if (clipArea.Contains(x, y) && rowRect.Contains(x, y))
                    {
                        Game1.playSound("bigSelect");
                        ExecuteCallAction(contact.Name, contact.Number, contact.IsNpc);
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
                        bool isNpc = Utility.getAllCharacters().Any(npc => npc != null && npc.Name == call.Name);
                        ExecuteCallAction(call.Name, call.Number, isNpc);
                        return;
                    }
                    currentY += ScaleUiValue(55) + ScaleUiValue(6);
                }
            }
            else if (phoneAppCurrentTab == 2) // Keypad Matrix Clicks
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
                            phoneAppCustomContacts.Add(new CustomContact { Name = phoneAppNewContactName.Trim(), Number = phoneAppKeypadBuffer });
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
                            phoneAppNewContactName = "";
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
                    string[,] numbers = { { "1", "2", "3" }, { "4", "5", "6" }, { "7", "8", "9" }, { "*", "0", "#" } };

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
                            bool isNpcCall = false;

                            var npcMatch = Utility.getAllCharacters().FirstOrDefault(n => n != null && string.Equals(n.Name, phoneAppKeypadBuffer, StringComparison.OrdinalIgnoreCase));
                            if (npcMatch != null)
                            {
                                destinationName = npcMatch.Name;
                                isNpcCall = true;
                            }
                            else
                            {
                                var customMatch = phoneAppCustomContacts.FirstOrDefault(cc => cc.Number == phoneAppKeypadBuffer);
                                if (customMatch != null) destinationName = customMatch.Name;
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

            phoneAppRecentCalls.Insert(0, new RecentCall { Name = name, Number = number, TimeText = timestamp });
            if (phoneAppRecentCalls.Count > 40) phoneAppRecentCalls.RemoveAt(phoneAppRecentCalls.Count - 1);
            SavePhoneAppData();

            ClosePhoneMenu();

            if (isNpc)
            {
                NPC targetNpc = Game1.getCharacterFromName(name);
                if (targetNpc != null)
                {
                    if (name == "Robin" || name == "Clint" || name == "Pierre" || name == "Marnie" || name == "Gus" || name == "Marlon")
                    {
                        List<Response> options = new List<Response>
                        {
                            new Response("chat", "Chat"),
                            new Response("shop", "Check Store Inventory"),
                            new Response("hang", "Hang Up")
                        };
                        // Fix 1: Evaluated lower-case npc.displayName localization mapping property
                        Game1.currentLocation.createQuestionDialogue($"Calling {targetNpc.displayName}...", options.ToArray(), (farmer, answer) =>
                        {
                            if (answer == "chat")
                            {
                                LaunchNpcPhoneDialogue(targetNpc);
                            }
                            else if (answer == "shop")
                            {
                                string stringShopId = name switch
                                {
                                    "Robin" => "Carpenter",
                                    "Clint" => "Blacksmith",
                                    "Marnie" => "AnimalShop",
                                    "Gus" => "Saloon",
                                    "Marlon" => "AdventureGuild",
                                    _ => "SeedShop"
                                };

                                // Fix 2: Loaded dynamic Data/Shops assets using DataLoader matching vanilla 1.6 definitions 
                                var shops = DataLoader.Shops(Game1.content);
                                if (shops != null && shops.TryGetValue(stringShopId, out var shopData))
                                {
                                    var ownerData = (shopData.Owners != null && shopData.Owners.Count > 0) ? shopData.Owners[0] : null;
                                    Game1.activeClickableMenu = new ShopMenu(stringShopId, shopData, ownerData, targetNpc);
                                }
                            }
                        });
                    }
                    else
                    {
                        LaunchNpcPhoneDialogue(targetNpc);
                    }
                }
            }
            else
            {
                Game1.activeClickableMenu = new DialogueBox($"Calling {name} ({number})... No answer available.");
            }
        }

        private void LaunchNpcPhoneDialogue(NPC npc)
        {
            // if (npc.CurrentDialogue.Count == 0) npc.loadCurrentDialogue();
            if (npc.CurrentDialogue.Count > 0) Game1.drawDialogue(npc);
            // Fix 1: Evaluated lower-case npc.displayName localization mapping property
            else Game1.activeClickableMenu = new DialogueBox($"{npc.displayName}: Hello! Thanks for calling.");
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
            if (!phoneAppIsAddingContact) return;

            if (key == Keys.Back)
            {
                if (phoneAppNewContactName.Length > 0)
                {
                    phoneAppNewContactName = phoneAppNewContactName.Substring(0, phoneAppNewContactName.Length - 1);
                    Game1.playSound("thudStep");
                }
                return;
            }

            string character = key.ToString();
            if (character.Length == 1 && phoneAppNewContactName.Length < 15)
            {
                bool isShift = Game1.oldKBState.IsKeyDown(Keys.LeftShift) || Game1.oldKBState.IsKeyDown(Keys.RightShift);
                phoneAppNewContactName += isShift ? character.ToUpper() : character.ToLower();
                Game1.playSound("cowboy_monster_hit");
            }
            else if (key == Keys.Space && phoneAppNewContactName.Length < 15)
            {
                phoneAppNewContactName += " ";
                Game1.playSound("cowboy_monster_hit");
            }
        }
    }
}