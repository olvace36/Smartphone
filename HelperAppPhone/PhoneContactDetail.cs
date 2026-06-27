using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace Smartphone
{
    public partial class PhoneMenu
    {
        public void DrawPhoneContactDetail(SpriteBatch b)
        {
            Rectangle bounds = GetPhoneContentBounds();
            float uiScale = ModEntry.GetActivePhoneUiScale();
            int headerHeight = ScaleUiValue(80);

            // Re-render the standard background behind everything
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.45f);
            DrawPhoneScreenBackground(b, 0, applyBackgroundImage: false);

            string title = "Contact Info";
            float titleScale = 0.55f * uiScale;
            Vector2 titleSize = Game1.dialogueFont.MeasureString(title) * titleScale;
            b.DrawString(Game1.dialogueFont, title, new Vector2(bounds.X + (bounds.Width - titleSize.X) / 2, bounds.Y + (headerHeight - titleSize.Y) / 2), Color.Black, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 1f);

            b.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Y + headerHeight, bounds.Width, ScaleUiValue(2)), Color.LightGray);

            int contentY = bounds.Y + headerHeight + ScaleUiValue(25);
            NPC targetNpc = null;
            if (phoneAppSelectedContactDetail.IsNpc && ModEntry.NpcNumbers.TryGetValue(phoneAppSelectedContactDetail.Number, out string npcName))
            {
                targetNpc = Game1.getCharacterFromName(npcName);
            }

            if (targetNpc != null)
            {
                // Portrait 
                int portraitSize = ScaleUiValue(128);
                int portraitX = bounds.X + (bounds.Width - portraitSize) / 2;

                // Optional framing border beneath portrait
                Textures.DrawCard(b, portraitX - ScaleUiValue(4), contentY - ScaleUiValue(4), portraitSize + ScaleUiValue(8), portraitSize + ScaleUiValue(8), Color.White);

                if (targetNpc.Portrait != null)
                {
                    b.Draw(targetNpc.Portrait, new Rectangle(portraitX, contentY, portraitSize, portraitSize), new Rectangle(0, 0, 64, 64), Color.White);
                }

                contentY += portraitSize + ScaleUiValue(20);

                // NPC Details Layout
                float nameScale = 1.1f * uiScale;
                Vector2 nameSz = Game1.dialogueFont.MeasureString(targetNpc.displayName) * nameScale;
                b.DrawString(Game1.dialogueFont, targetNpc.displayName, new Vector2(bounds.X + (bounds.Width - nameSz.X) / 2, contentY), Color.Black, 0f, Vector2.Zero, nameScale, SpriteEffects.None, 1f);

                contentY += (int)nameSz.Y + ScaleUiValue(25);

                float detailScale = 0.85f * uiScale;
                string address = "Address: " + (string.IsNullOrEmpty(targetNpc.DefaultMap) ? "Unknown" : targetNpc.DefaultMap);
                b.DrawString(Game1.smallFont, address, new Vector2(bounds.X + ScaleUiValue(30), contentY), Color.DarkSlateGray, 0f, Vector2.Zero, detailScale, SpriteEffects.None, 1f);
                contentY += ScaleUiValue(30);

                string ageStr = targetNpc.Age == 0 ? "Adult" : (targetNpc.Age == 1 ? "Teen" : "Child");
                string age = "Age: " + ageStr;
                b.DrawString(Game1.smallFont, age, new Vector2(bounds.X + ScaleUiValue(30), contentY), Color.DarkSlateGray, 0f, Vector2.Zero, detailScale, SpriteEffects.None, 1f);
                contentY += ScaleUiValue(30);

                string seasonName = string.IsNullOrEmpty(targetNpc.Birthday_Season) ? "" : Utility.capitalizeFirstLetter(targetNpc.Birthday_Season);
                string birthday = "Birthday: " + (string.IsNullOrEmpty(seasonName) ? "Unknown" : $"{seasonName} {targetNpc.Birthday_Day}");
                b.DrawString(Game1.smallFont, birthday, new Vector2(bounds.X + ScaleUiValue(30), contentY), Color.DarkSlateGray, 0f, Vector2.Zero, detailScale, SpriteEffects.None, 1f);

                contentY += ScaleUiValue(55);
            }
            else
            {
                // Fallback Layout For Generic Created Contacts
                float nameScale = 1.1f * uiScale;
                Vector2 nameSz = Game1.dialogueFont.MeasureString(phoneAppSelectedContactDetail.Name) * nameScale;
                b.DrawString(Game1.dialogueFont, phoneAppSelectedContactDetail.Name, new Vector2(bounds.X + (bounds.Width - nameSz.X) / 2, contentY + ScaleUiValue(30)), Color.Black, 0f, Vector2.Zero, nameScale, SpriteEffects.None, 1f);

                contentY += (int)nameSz.Y + ScaleUiValue(60);

                float numScale = 0.9f * uiScale;
                Vector2 numSz = Game1.smallFont.MeasureString(phoneAppSelectedContactDetail.Number) * numScale;
                b.DrawString(Game1.smallFont, phoneAppSelectedContactDetail.Number, new Vector2(bounds.X + (bounds.Width - numSz.X) / 2, contentY), Color.DarkGray, 0f, Vector2.Zero, numScale, SpriteEffects.None, 1f);

                contentY += ScaleUiValue(110);
            }

            // Card / Nav Bar
            int btnSize = ScaleUiValue(75);
            Rectangle navBar = new Rectangle(bounds.X + ScaleUiValue(10), contentY, bounds.Width - ScaleUiValue(20), btnSize);

            // Draw Card Background
            Textures.DrawCard(b, navBar.X, navBar.Y, navBar.Width, navBar.Height, Color.White * 0.9f);

            // Title "Phone" on the left
            float navBarTitleScale = 0.9f * uiScale;
            Vector2 titleSz = Game1.smallFont.MeasureString("Phone") * navBarTitleScale;
            b.DrawString(Game1.smallFont, "Phone", new Vector2(navBar.X + ScaleUiValue(15), navBar.Y + (navBar.Height - titleSz.Y) / 2), Color.Black, 0f, Vector2.Zero, navBarTitleScale, SpriteEffects.None, 1f);

            // Buttons on the right edge of the card
            Rectangle favBtn = new Rectangle(navBar.X + navBar.Width - btnSize, navBar.Y, btnSize, btnSize);
            Rectangle callBtn = new Rectangle(favBtn.X - ScaleUiValue(10) - btnSize, navBar.Y, btnSize, btnSize);

            // Call Button
            Textures.DrawCard(b, callBtn.X, callBtn.Y, callBtn.Width, callBtn.Height, Color.LimeGreen);
            float btnTxtScale = 0.9f * uiScale;
            float callBtnTxtScale = btnTxtScale;
            Vector2 callSz = Game1.smallFont.MeasureString("Call") * callBtnTxtScale;
            if (callSz.X > callBtn.Width - ScaleUiValue(8))
            {
                callBtnTxtScale *= (callBtn.Width - ScaleUiValue(8)) / callSz.X;
                callSz = Game1.smallFont.MeasureString("Call") * callBtnTxtScale;
            }
            b.DrawString(Game1.smallFont, "Call", new Vector2(callBtn.X + (callBtn.Width - callSz.X) / 2, callBtn.Y + (callBtn.Height - callSz.Y) / 2), Color.White, 0f, Vector2.Zero, callBtnTxtScale, SpriteEffects.None, 1f);

            // Toggle Favorite Status Button
            bool isFav = phoneAppFavoriteNumbers.Contains(phoneAppSelectedContactDetail.Number);
            Color favBtnColor = isFav ? Color.LightCoral : Color.LightSkyBlue;
            string favTxt = isFav ? "Unpin" : "Pin";

            Textures.DrawCard(b, favBtn.X, favBtn.Y, favBtn.Width, favBtn.Height, favBtnColor);
            float favBtnTxtScale = btnTxtScale;
            Vector2 favSz = Game1.smallFont.MeasureString(favTxt) * favBtnTxtScale;
            if (favSz.X > favBtn.Width - ScaleUiValue(8))
            {
                favBtnTxtScale *= (favBtn.Width - ScaleUiValue(8)) / favSz.X;
                favSz = Game1.smallFont.MeasureString(favTxt) * favBtnTxtScale;
            }
            b.DrawString(Game1.smallFont, favTxt, new Vector2(favBtn.X + (favBtn.Width - favSz.X) / 2, favBtn.Y + (favBtn.Height - favSz.Y) / 2), Color.Black, 0f, Vector2.Zero, favBtnTxtScale, SpriteEffects.None, 1f);

            Texture2D frameTex = Textures.PhoneEmpty;
            if (frameTex != null && !frameTex.IsDisposed)
            {
                b.Draw(frameTex, new Vector2(ModEntry.currentMenuX, ModEntry.currentMenuY), null, Color.White, 0f, Vector2.Zero, uiScale, SpriteEffects.None, 0.99f);
            }
        }

        public void ReceiveLeftClickPhoneContactDetail(int x, int y)
        {
            Rectangle bounds = GetPhoneContentBounds();
            int headerHeight = ScaleUiValue(80);

            int contentY = bounds.Y + headerHeight + ScaleUiValue(25);
            NPC targetNpc = null;
            if (phoneAppSelectedContactDetail.IsNpc && ModEntry.NpcNumbers.TryGetValue(phoneAppSelectedContactDetail.Number, out string npcName))
            {
                targetNpc = Game1.getCharacterFromName(npcName);
            }

            // Sync Hitboxes with Y offsets matching the drawing loop logic
            if (targetNpc != null)
            {
                contentY += ScaleUiValue(128) + ScaleUiValue(20);
                contentY += (int)(Game1.dialogueFont.MeasureString(targetNpc.displayName).Y * 1.1f * ModEntry.GetActivePhoneUiScale()) + ScaleUiValue(25);
                contentY += ScaleUiValue(30) * 2 + ScaleUiValue(55);
            }
            else
            {
                contentY += ScaleUiValue(30);
                contentY += (int)(Game1.dialogueFont.MeasureString(phoneAppSelectedContactDetail.Name).Y * 1.1f * ModEntry.GetActivePhoneUiScale()) + ScaleUiValue(60);
                contentY += ScaleUiValue(110);
            }

            int btnSize = ScaleUiValue(75);
            Rectangle navBar = new Rectangle(bounds.X + ScaleUiValue(10), contentY, bounds.Width - ScaleUiValue(20), btnSize);
            Rectangle favBtn = new Rectangle(navBar.X + navBar.Width - btnSize, navBar.Y, btnSize, btnSize);
            Rectangle callBtn = new Rectangle(favBtn.X - ScaleUiValue(10) - btnSize, navBar.Y, btnSize, btnSize);

            if (callBtn.Contains(x, y))
            {
                Game1.playSound("bigSelect");
                ExecuteCallAction(phoneAppSelectedContactDetail.Name, phoneAppSelectedContactDetail.Number, phoneAppSelectedContactDetail.IsNpc);
                return;
            }

            if (favBtn.Contains(x, y))
            {
                Game1.playSound("coin");
                if (phoneAppFavoriteNumbers.Contains(phoneAppSelectedContactDetail.Number))
                {
                    phoneAppFavoriteNumbers.Remove(phoneAppSelectedContactDetail.Number);
                }
                else
                {
                    phoneAppFavoriteNumbers.Add(phoneAppSelectedContactDetail.Number);
                }
                SavePhoneAppData(); // Flushes favorite adjustments back to phone_app_data.json instantly
                return;
            }
        }
    }
}