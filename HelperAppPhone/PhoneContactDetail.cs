using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace Smartphone
{
    public partial class PhoneMenu
    {
        public int phoneAppContactDetailScroll = 0;
        public int phoneAppContactDetailMaxScroll = 0;

        public void DrawPhoneContactDetail(SpriteBatch b)
        {
            Rectangle bounds = GetPhoneContentBounds();
            float uiScale = ModEntry.GetActivePhoneUiScale();
            int headerHeight = ScaleUiValue(80);

            // Re-render the standard background behind everything
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.45f);
            DrawPhoneScreenBackground(b, 0, applyBackgroundImage: false);

            string title = ModEntry.SHelper.Translation.Get("ui.phone.contact_detail.title");
            float titleScale = 0.55f * uiScale;
            Vector2 titleSize = Game1.dialogueFont.MeasureString(title) * titleScale;
            b.DrawString(Game1.dialogueFont, title, new Vector2(bounds.X + (bounds.Width - titleSize.X) / 2, bounds.Y + (headerHeight - titleSize.Y) / 2), Color.Black, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 1f);

            b.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Y + headerHeight, bounds.Width, ScaleUiValue(2)), Color.LightGray);

            // Setup scrolling clip area
            int clipY = bounds.Y + headerHeight + ScaleUiValue(2);
            int clipHeight = bounds.Height - headerHeight - ScaleUiValue(2);
            Rectangle clipArea = new Rectangle(bounds.X, clipY, bounds.Width, clipHeight);

            Rectangle originalScissor = b.GraphicsDevice.ScissorRectangle;
            b.End();
            RasterizerState scissorState = new RasterizerState { ScissorTestEnable = true };
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, scissorState);
            b.GraphicsDevice.ScissorRectangle = Rectangle.Intersect(originalScissor, clipArea);

            int contentY = clipY + ScaleUiValue(23) - phoneAppContactDetailScroll;

            NPC targetNpc = null;
            if (phoneAppSelectedContactDetail != null)
            {
                targetNpc = Game1.getCharacterFromName(phoneAppSelectedContactDetail.Name);
            }

            if (targetNpc == null)
            {
                phoneAppViewingContactDetail = false;
                return;
            }

            int portraitSize = ScaleUiValue(128);
            int portraitX = bounds.X + (bounds.Width - portraitSize) / 2;

            Textures.DrawCard(b, portraitX - ScaleUiValue(4), contentY - ScaleUiValue(4), portraitSize + ScaleUiValue(8), portraitSize + ScaleUiValue(8), Color.White);

            if (targetNpc.Portrait != null)
            {
                b.Draw(targetNpc.Portrait, new Rectangle(portraitX, contentY, portraitSize, portraitSize), new Rectangle(0, 0, 64, 64), Color.White);
            }

            contentY += portraitSize + ScaleUiValue(20);

            float nameScale = 1.1f * uiScale;
            Vector2 nameSz = Game1.dialogueFont.MeasureString(targetNpc.displayName) * nameScale;
            b.DrawString(Game1.dialogueFont, targetNpc.displayName, new Vector2(bounds.X + (bounds.Width - nameSz.X) / 2, contentY), Color.Black, 0f, Vector2.Zero, nameScale, SpriteEffects.None, 1f);

            contentY += (int)nameSz.Y + ScaleUiValue(25);

            float detailScale = 0.85f * uiScale;
            string mapName = string.IsNullOrEmpty(targetNpc.DefaultMap) ? ModEntry.SHelper.Translation.Get("ui.phone.contact_detail.unknown") : targetNpc.DefaultMap;
            string mapDisplayName = Game1.getLocationFromName(mapName)?.DisplayName;
            string address = ModEntry.SHelper.Translation.Get("ui.phone.contact_detail.address", new { address = mapDisplayName });
            b.DrawString(Game1.smallFont, address, new Vector2(bounds.X + ScaleUiValue(30), contentY), Color.DarkSlateGray, 0f, Vector2.Zero, detailScale, SpriteEffects.None, 1f);
            contentY += ScaleUiValue(30);

            string ageStr = targetNpc.Age == 0 ? ModEntry.SHelper.Translation.Get("ui.phone.contact_detail.age.adult") : (targetNpc.Age == 1 ? ModEntry.SHelper.Translation.Get("ui.phone.contact_detail.age.teen") : ModEntry.SHelper.Translation.Get("ui.phone.contact_detail.age.child"));
            string age = ModEntry.SHelper.Translation.Get("ui.phone.contact_detail.age", new { age = ageStr });
            b.DrawString(Game1.smallFont, age, new Vector2(bounds.X + ScaleUiValue(30), contentY), Color.DarkSlateGray, 0f, Vector2.Zero, detailScale, SpriteEffects.None, 1f);
            contentY += ScaleUiValue(30);

            string seasonName = string.IsNullOrEmpty(targetNpc.Birthday_Season) ? "" : Utility.capitalizeFirstLetter(targetNpc.Birthday_Season);
            string bdayVal = string.IsNullOrEmpty(seasonName) ? ModEntry.SHelper.Translation.Get("ui.phone.contact_detail.unknown") : $"{seasonName} {targetNpc.Birthday_Day}";
            string birthday = ModEntry.SHelper.Translation.Get("ui.phone.contact_detail.birthday", new { birthday = bdayVal });
            b.DrawString(Game1.smallFont, birthday, new Vector2(bounds.X + ScaleUiValue(30), contentY), Color.DarkSlateGray, 0f, Vector2.Zero, detailScale, SpriteEffects.None, 1f);

            contentY += ScaleUiValue(55);

            int btnSize = ScaleUiValue(75);
            Rectangle navBar = new Rectangle(bounds.X + ScaleUiValue(10), contentY, bounds.Width - ScaleUiValue(20), btnSize);

            Textures.DrawCard(b, navBar.X, navBar.Y, navBar.Width, navBar.Height, Color.White * 0.9f);

            float navBarTitleScale = 0.9f * uiScale;
            string phoneLabel = ModEntry.SHelper.Translation.Get("ui.phone.phone");
            Vector2 titleSz2 = Game1.smallFont.MeasureString(phoneLabel) * navBarTitleScale;
            b.DrawString(Game1.smallFont, phoneLabel, new Vector2(navBar.X + ScaleUiValue(15), navBar.Y + (navBar.Height - titleSz2.Y) / 2), Color.Black, 0f, Vector2.Zero, navBarTitleScale, SpriteEffects.None, 1f);

            Rectangle favBtn = new Rectangle(navBar.X + navBar.Width - btnSize, navBar.Y, btnSize, btnSize);
            Rectangle callBtn = new Rectangle(favBtn.X - ScaleUiValue(10) - btnSize, navBar.Y, btnSize, btnSize);

            Textures.DrawCard(b, callBtn.X, callBtn.Y, callBtn.Width, callBtn.Height, Color.LimeGreen);
            float btnTxtScale = 0.9f * uiScale;
            float callBtnTxtScale = btnTxtScale;
            string callLabel = ModEntry.SHelper.Translation.Get("ui.phone.call");
            Vector2 callSz = Game1.smallFont.MeasureString(callLabel) * callBtnTxtScale;
            if (callSz.X > callBtn.Width - ScaleUiValue(8))
            {
                callBtnTxtScale *= (callBtn.Width - ScaleUiValue(8)) / callSz.X;
                callSz = Game1.smallFont.MeasureString(callLabel) * callBtnTxtScale;
            }
            b.DrawString(Game1.smallFont, callLabel, new Vector2(callBtn.X + (callBtn.Width - callSz.X) / 2, callBtn.Y + (callBtn.Height - callSz.Y) / 2), Color.White, 0f, Vector2.Zero, callBtnTxtScale, SpriteEffects.None, 1f);

            bool isFav = phoneAppFavoriteNumbers.Contains(phoneAppSelectedContactDetail.Name);
            Color favBtnColor = isFav ? Color.LightCoral : Color.LightSkyBlue;
            string favTxt = isFav ? ModEntry.SHelper.Translation.Get("ui.phone.unpin") : ModEntry.SHelper.Translation.Get("ui.phone.pin");

            Textures.DrawCard(b, favBtn.X, favBtn.Y, favBtn.Width, favBtn.Height, favBtnColor);
            float favBtnTxtScale = btnTxtScale;
            Vector2 favSz = Game1.smallFont.MeasureString(favTxt) * favBtnTxtScale;
            if (favSz.X > favBtn.Width - ScaleUiValue(8))
            {
                favBtnTxtScale *= (favBtn.Width - ScaleUiValue(8)) / favSz.X;
                favSz = Game1.smallFont.MeasureString(favTxt) * favBtnTxtScale;
            }
            b.DrawString(Game1.smallFont, favTxt, new Vector2(favBtn.X + (favBtn.Width - favSz.X) / 2, favBtn.Y + (favBtn.Height - favSz.Y) / 2), Color.Black, 0f, Vector2.Zero, favBtnTxtScale, SpriteEffects.None, 1f);

            contentY += btnSize;

            foreach (var card in ModEntry.ContactActionCardsManager.Cards)
            {
                if (card.AvailableNpcNames != null && card.AvailableNpcNames.Count > 0)
                {
                    if (!card.AvailableNpcNames.Exists(name => name.Equals(targetNpc.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }
                }

                contentY += ScaleUiValue(10);
                Rectangle customNavBar = new Rectangle(bounds.X + ScaleUiValue(10), contentY, bounds.Width - ScaleUiValue(20), btnSize);
                Textures.DrawCard(b, customNavBar.X, customNavBar.Y, customNavBar.Width, customNavBar.Height, Color.White * 0.9f);

                Vector2 customTitleSz = Game1.smallFont.MeasureString(card.Title) * navBarTitleScale;
                b.DrawString(Game1.smallFont, card.Title, new Vector2(customNavBar.X + ScaleUiValue(15), customNavBar.Y + (customNavBar.Height - customTitleSz.Y) / 2), Color.Black, 0f, Vector2.Zero, navBarTitleScale, SpriteEffects.None, 1f);

                int currentX = customNavBar.Right;
                for (int i = card.Buttons.Count - 1; i >= 0; i--)
                {
                    var btn = card.Buttons[i];
                    currentX -= btnSize;
                    Rectangle actionBtn = new Rectangle(currentX, customNavBar.Y, btnSize, btnSize);
                    Textures.DrawCard(b, actionBtn.X, actionBtn.Y, actionBtn.Width, actionBtn.Height, btn.BackgroundColor);

                    float actionTxtScale = 0.9f * uiScale;
                    Vector2 sz = Game1.smallFont.MeasureString(btn.Text) * actionTxtScale;
                    if (sz.X > actionBtn.Width - ScaleUiValue(8))
                    {
                        actionTxtScale *= (actionBtn.Width - ScaleUiValue(8)) / sz.X;
                        sz = Game1.smallFont.MeasureString(btn.Text) * actionTxtScale;
                    }
                    b.DrawString(Game1.smallFont, btn.Text, new Vector2(actionBtn.X + (actionBtn.Width - sz.X) / 2, actionBtn.Y + (actionBtn.Height - sz.Y) / 2), btn.TextColor, 0f, Vector2.Zero, actionTxtScale, SpriteEffects.None, 1f);

                    currentX -= ScaleUiValue(10);
                }
                contentY += btnSize;
            }

            contentY += ScaleUiValue(20);

            int totalContentHeight = contentY + phoneAppContactDetailScroll - clipY;
            phoneAppContactDetailMaxScroll = Math.Max(0, totalContentHeight - clipHeight);

            b.End();
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, null);
            b.GraphicsDevice.ScissorRectangle = originalScissor;

            Texture2D frameTex = Textures.PhoneEmpty;
            if (frameTex != null && !frameTex.IsDisposed)
            {
                b.Draw(frameTex, new Vector2(ModEntry.currentMenuX, ModEntry.currentMenuY), null, Color.White, 0f, Vector2.Zero, uiScale, SpriteEffects.None, 0.99f);
            }
        }

        public void ReleaseLeftClickPhoneContactDetail(int x, int y)
        {
            Rectangle bounds = GetPhoneContentBounds();
            int headerHeight = ScaleUiValue(80);

            int clipY = bounds.Y + headerHeight + ScaleUiValue(2);
            int clipHeight = bounds.Height - headerHeight - ScaleUiValue(2);
            Rectangle clipArea = new Rectangle(bounds.X, clipY, bounds.Width, clipHeight);

            if (!clipArea.Contains(x, y)) return;

            int contentY = clipY + ScaleUiValue(23) - phoneAppContactDetailScroll;
            NPC targetNpc = null;
            if (phoneAppSelectedContactDetail != null)
            {
                targetNpc = Game1.getCharacterFromName(phoneAppSelectedContactDetail.Name);
            }

            if (targetNpc == null)
            {
                phoneAppViewingContactDetail = false;
                return;
            }

            // Sync Hitboxes with Y offsets matching the drawing loop logic
            contentY += ScaleUiValue(128) + ScaleUiValue(20);
            contentY += (int)(Game1.dialogueFont.MeasureString(targetNpc.displayName).Y * 1.1f * ModEntry.GetActivePhoneUiScale()) + ScaleUiValue(25);
            contentY += ScaleUiValue(30) * 2 + ScaleUiValue(55);

            int btnSize = ScaleUiValue(75);
            Rectangle navBar = new Rectangle(bounds.X + ScaleUiValue(10), contentY, bounds.Width - ScaleUiValue(20), btnSize);
            Rectangle favBtn = new Rectangle(navBar.X + navBar.Width - btnSize, navBar.Y, btnSize, btnSize);
            Rectangle callBtn = new Rectangle(favBtn.X - ScaleUiValue(10) - btnSize, navBar.Y, btnSize, btnSize);

            if (callBtn.Contains(x, y))
            {
                Game1.playSound("bigSelect");
                ExecuteCallAction(phoneAppSelectedContactDetail.Name);
                return;
            }

            if (favBtn.Contains(x, y))
            {
                Game1.playSound("coin");
                if (phoneAppFavoriteNumbers.Contains(phoneAppSelectedContactDetail.Name))
                {
                    phoneAppFavoriteNumbers.Remove(phoneAppSelectedContactDetail.Name);
                }
                else
                {
                    phoneAppFavoriteNumbers.Add(phoneAppSelectedContactDetail.Name);
                }
                SavePhoneAppData();
                return;
            }

            contentY += btnSize;

            foreach (var card in ModEntry.ContactActionCardsManager.Cards)
            {
                if (card.AvailableNpcNames != null && card.AvailableNpcNames.Count > 0)
                {
                    if (!card.AvailableNpcNames.Exists(name => name.Equals(targetNpc.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }
                }

                contentY += ScaleUiValue(10);
                Rectangle customNavBar = new Rectangle(bounds.X + ScaleUiValue(10), contentY, bounds.Width - ScaleUiValue(20), btnSize);

                int currentX = customNavBar.Right;
                for (int i = card.Buttons.Count - 1; i >= 0; i--)
                {
                    var btn = card.Buttons[i];
                    currentX -= btnSize;
                    Rectangle actionBtn = new Rectangle(currentX, customNavBar.Y, btnSize, btnSize);

                    if (actionBtn.Contains(x, y))
                    {
                        Game1.playSound("bigSelect");
                        btn.OnClick?.Invoke(targetNpc.Name);
                        return;
                    }

                    currentX -= ScaleUiValue(10);
                }
                contentY += btnSize;
            }
        }
    }
}