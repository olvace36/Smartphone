using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace Smartphone
{
    public enum AppStoreState
    {
        List,
        Detail
    }

    public partial class PhoneMenu
    {
        private AppStoreState appStoreCurrentState = AppStoreState.List;
        private AppStoreMod appStoreSelectedMod = null;

        private int appStoreCurrentPage = 0;
        private int appStoreItemsPerPage = 8;

        private int appStoreScrollOffset = 0;
        private int appStoreMaxVisibleItems = 8;
        private int appStoreItemHeight = 90;

        private int appStoreDetailScrollOffset = 0;
        private int appStoreMaxDetailScroll = 0;

        private string appStoreCurrentSort = "Latest";
        private bool appStoreDropdownOpen = false;
        private readonly string[] appStoreSortOptions = { "Latest", "Endorsement", "Alphabet", "Official", "Installed" };
        private Rectangle appStoreSortButtonRect;

        private string appStoreCurrentType = "App";
        private bool appStoreTypeDropdownOpen = false;
        private readonly string[] appStoreTypeOptions = { "App", "Theme", "Translation" };
        private Rectangle appStoreTypeButtonRect;

        private Rectangle appStoreGoToModButtonRect;


        private ClickableTextureComponent appStorePrevPageButton;
        private ClickableTextureComponent appStoreNextPageButton;

        private void DrawAppStore(SpriteBatch b)
        {
            if (appStoreCurrentState == AppStoreState.List)
            {
                AppStoreManager.FetchIconsForPage(appStoreCurrentPage, appStoreItemsPerPage);

                int startIndex = appStoreCurrentPage * appStoreItemsPerPage;
                int endIndex = Math.Min(startIndex + appStoreItemsPerPage, AppStoreManager.Mods.Count);
                int pageItemCount = endIndex - startIndex;

                // Pagination logic
                int totalPages = (int)Math.Ceiling((double)AppStoreManager.Mods.Count / appStoreItemsPerPage);

                // Draw Mods List
                int currentY = PhoneY(130);

                int scaledItemHeight = ScaleUiValue(appStoreItemHeight);
                int scaledSlotWidth = width - ScaleUiValue(50);
                int slotX = PhoneX(35);

                string appStoreHoverText = null;
                int mouseX = Game1.getMouseX();
                int mouseY = Game1.getMouseY();

                for (int i = appStoreScrollOffset; i < Math.Min(appStoreScrollOffset + appStoreMaxVisibleItems, pageItemCount); i++)
                {
                    int modIndex = startIndex + i;
                    if (modIndex >= AppStoreManager.Mods.Count) break;

                    var mod = AppStoreManager.Mods[modIndex];

                    // No background to match Messenger app style
                    Rectangle slotRect = new Rectangle(slotX, currentY, scaledSlotWidth, scaledItemHeight);

                    int iconSize = ScaleUiValue(82);
                    int iconX = slotX + ScaleUiValue(10);
                    int iconY = currentY + ScaleUiValue(15);

                    if (mod.IconTexture == null && mod.IconBytes != null)
                    {
                        try
                        {
                            using (var stream = new MemoryStream(mod.IconBytes))
                            {
                                mod.IconTexture = Texture2D.FromStream(Game1.graphics.GraphicsDevice, stream);
                            }
                        }
                        catch (Exception ex)
                        {
                            ModEntry.SMonitor?.Log($"Failed to decode icon for {mod.Name}: {ex.Message}");
                            mod.IconBytes = null;
                        }
                    }

                    if (mod.IconTexture != null)
                    {
                        b.Draw(mod.IconTexture, new Rectangle(iconX, iconY, iconSize, iconSize), Color.White);
                    }
                    else
                    {
                        b.Draw(Game1.staminaRect, new Rectangle(iconX, iconY, iconSize, iconSize), Color.Gray);
                    }

                    int textX = iconX + iconSize + ScaleUiValue(15);
                    int maxTextWidth = slotRect.Width - (iconSize + ScaleUiValue(45));

                    // Check mod status for icons
                    var modInfo = ModEntry.SHelper.ModRegistry.Get(mod.UniqueID);
                    bool isInstalled = modInfo != null;
                    bool isUpToDate = false;
                    if (isInstalled)
                    {
                        string currentVersion = modInfo.Manifest.Version.ToString();
                        isUpToDate = currentVersion == mod.LatestVersion;
                    }

                    bool isNew = false;
                    if (DateTime.TryParse(mod.PublishedAt, out DateTime pubDate))
                    {
                        if ((DateTime.UtcNow - pubDate).TotalDays <= 10)
                        {
                            isNew = true;
                        }
                    }

                    // Determine icon dimensions
                    int rightPadding = ScaleUiValue(25);
                    int iconW = 0;
                    Rectangle iconSourceRect = Rectangle.Empty;

                    if (isInstalled)
                    {
                        iconSourceRect = isUpToDate ? new Rectangle(341, 410, 23, 9) : new Rectangle(317, 410, 23, 9);
                        iconW = ScaleUiValue(iconSourceRect.Width * 3);
                    }
                    else if (isNew)
                    {
                        iconSourceRect = new Rectangle(410, 501, 9, 9);
                        iconW = ScaleUiValue(iconSourceRect.Width * 3);
                    }

                    // Mod Name
                    int actualMaxNameWidth = slotRect.Width - (iconSize + ScaleUiValue(30)) - iconW - rightPadding;
                    if (actualMaxNameWidth < ScaleUiValue(100)) actualMaxNameWidth = ScaleUiValue(100);

                    bool shouldLoopName = mod.Name.Length > 20 || MeasurePhoneText(Game1.smallFont, mod.Name, 1f).X > actualMaxNameWidth;
                    if (shouldLoopName)
                    {
                        DrawLoopingAppStoreText(b, Game1.smallFont, mod.Name, new Vector2(textX, iconY), 1f, actualMaxNameWidth, Color.DarkBlue);
                    }
                    else
                    {
                        DrawPhoneText(b, Game1.smallFont, mod.Name, new Vector2(textX, iconY), Color.DarkBlue);
                    }

                    // Draw icons aligned to right edge
                    if (iconSourceRect != Rectangle.Empty)
                    {
                        int iconDrawY = iconY + ScaleUiValue(2);
                        int h = ScaleUiValue(iconSourceRect.Height * 3);
                        Rectangle destRect = new Rectangle(slotRect.Right - rightPadding - iconW, iconDrawY, iconW, h);
                        b.Draw(Game1.mouseCursors, destRect, iconSourceRect, Color.White);

                        if (destRect.Contains(mouseX, mouseY))
                        {
                            if (isInstalled)
                                appStoreHoverText = isUpToDate ? ModEntry.SHelper.Translation.Get("ui.appstore.up_to_date") : ModEntry.SHelper.Translation.Get("ui.appstore.new_version_available");
                            else
                                appStoreHoverText = ModEntry.SHelper.Translation.Get("ui.appstore.new_exclamation");
                        }
                    }

                    string authorText = ModEntry.SHelper.Translation.Get("ui.appstore.by_author", new { author = mod.Author });
                    if (!string.IsNullOrWhiteSpace(mod.TotalEndorsement))
                    {
                        authorText += " - " + ModEntry.SHelper.Translation.Get("ui.appstore.endorsements", new { count = mod.TotalEndorsement });
                    }
                    DrawPhoneText(b, Game1.smallFont, authorText, new Vector2(textX, iconY + ScaleUiValue(25)), Color.DarkSlateGray, 0.7f);

                    // ShortDescription
                    if (mod.ShortDescription.Length > 45 || MeasurePhoneText(Game1.smallFont, mod.ShortDescription, 0.65f).X > maxTextWidth)
                    {
                        DrawLoopingAppStoreText(b, Game1.smallFont, mod.ShortDescription, new Vector2(textX, iconY + ScaleUiValue(45)), 0.65f, maxTextWidth, Color.Black);
                    }
                    else
                    {
                        DrawPhoneText(b, Game1.smallFont, mod.ShortDescription, new Vector2(textX, iconY + ScaleUiValue(45)), Color.Black, 0.65f);
                    }

                    currentY += scaledItemHeight;
                }

                int pageY = yPositionOnScreen + height - ScaleUiValue(120);

                // Draw Pagination Controls
                if (totalPages > 1)
                {
                    string pageText = $"{appStoreCurrentPage + 1} / {totalPages}";
                    Vector2 pageTextSize = MeasurePhoneText(Game1.smallFont, pageText);

                    int buttonSize = ScaleUiValue(64);
                    float buttonScale = (float)buttonSize / 64f;
                    int oldTotalWidth = buttonSize + ScaleUiValue(40) + (int)pageTextSize.X + buttonSize;
                    int origStartX = xPositionOnScreen + width - ScaleUiValue(35) - oldTotalWidth;
                    int startX = origStartX - ScaleUiValue(10);
                    int spacing = ScaleUiValue(5);

                    int textY = pageY + ScaleUiValue(15);
                    // Shift the button up slightly so its visual center matches the text baseline
                    int pageButtonY = textY + (int)pageTextSize.Y / 2 - buttonSize / 2 - ScaleUiValue(10);

                    if (appStorePrevPageButton == null)
                        appStorePrevPageButton = new ClickableTextureComponent(Rectangle.Empty, Game1.mouseCursors, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 44), 1f);


                    appStorePrevPageButton.bounds = new Rectangle(startX, pageButtonY, buttonSize, buttonSize);
                    appStorePrevPageButton.scale = buttonScale;

                    if (appStoreNextPageButton == null)
                        appStoreNextPageButton = new ClickableTextureComponent(Rectangle.Empty, Game1.mouseCursors, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 33), 1f);


                    appStoreNextPageButton.bounds = new Rectangle(startX + buttonSize + spacing + (int)pageTextSize.X + spacing, pageButtonY, buttonSize, buttonSize);
                    appStoreNextPageButton.scale = buttonScale;

                    if (appStoreCurrentPage > 0)
                        appStorePrevPageButton.draw(b);
                    if (appStoreCurrentPage < totalPages - 1)
                        appStoreNextPageButton.draw(b);

                    DrawPhoneText(b, Game1.smallFont, pageText, new Vector2(startX + buttonSize + spacing, textY), Color.Black);
                }

                // Draw Sort Dropdown at bottom left
                string sortButtonText = ModEntry.SHelper.Translation.Get("ui.appstore.sort") + ": " + ModEntry.SHelper.Translation.Get("ui.appstore.sort." + appStoreCurrentSort.ToLower());
                Vector2 sortButtonSize = MeasurePhoneText(Game1.smallFont, sortButtonText);
                int sortButtonWidth = (int)sortButtonSize.X + ScaleUiValue(20);
                int sortButtonHeight = (int)sortButtonSize.Y + ScaleUiValue(10);
                int sortButtonX = PhoneX(75);
                int sortButtonY = pageY; // Match baseline a bit with text

                appStoreSortButtonRect = new Rectangle(sortButtonX, sortButtonY, sortButtonWidth, sortButtonHeight);
                IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), appStoreSortButtonRect.X, appStoreSortButtonRect.Y, appStoreSortButtonRect.Width, appStoreSortButtonRect.Height, new Color(255, 255, 255, 220), 1f, false);
                DrawPhoneText(b, Game1.smallFont, sortButtonText, new Vector2(appStoreSortButtonRect.X + ScaleUiValue(10), appStoreSortButtonRect.Y + ScaleUiValue(8)), Color.Black);

                if (appStoreDropdownOpen)
                {
                    int maxOptionWidth = sortButtonWidth;
                    foreach (string opt in appStoreSortOptions)
                    {
                        int optW = (int)MeasurePhoneText(Game1.smallFont, ModEntry.SHelper.Translation.Get("ui.appstore.sort." + opt.ToLower())).X + ScaleUiValue(20);
                        if (optW > maxOptionWidth) maxOptionWidth = optW;
                    }
                    int dropY = appStoreSortButtonRect.Top - appStoreSortOptions.Length * sortButtonHeight;

                    IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), appStoreSortButtonRect.X, dropY, maxOptionWidth, appStoreSortOptions.Length * sortButtonHeight, Color.White, 1f, false);
                    for (int i = 0; i < appStoreSortOptions.Length; i++)
                    {
                        DrawPhoneText(b, Game1.smallFont, ModEntry.SHelper.Translation.Get("ui.appstore.sort." + appStoreSortOptions[i].ToLower()), new Vector2(appStoreSortButtonRect.X + ScaleUiValue(10), dropY + i * sortButtonHeight + ScaleUiValue(8)), Color.Black);
                    }
                }

                // Draw Type Dropdown at top right
                string typeButtonText = ModEntry.SHelper.Translation.Get("ui.appstore.type") + ": " + ModEntry.SHelper.Translation.Get("ui.appstore.type." + appStoreCurrentType.ToLower());
                Vector2 typeButtonSize = MeasurePhoneText(Game1.smallFont, typeButtonText);
                int typeButtonWidth = (int)typeButtonSize.X + ScaleUiValue(20);
                int typeButtonHeight = (int)typeButtonSize.Y + ScaleUiValue(10);
                int typeButtonX = PhoneX(width - 50 - typeButtonWidth);
                int typeButtonY = pageY;

                appStoreTypeButtonRect = new Rectangle(typeButtonX, typeButtonY, typeButtonWidth, typeButtonHeight);
                IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), appStoreTypeButtonRect.X, appStoreTypeButtonRect.Y, appStoreTypeButtonRect.Width, appStoreTypeButtonRect.Height, new Color(255, 255, 255, 220), 1f, false);
                DrawPhoneText(b, Game1.smallFont, typeButtonText, new Vector2(appStoreTypeButtonRect.X + ScaleUiValue(10), appStoreTypeButtonRect.Y + ScaleUiValue(8)), Color.Black);

                if (appStoreTypeDropdownOpen)
                {
                    int dropY = appStoreTypeButtonRect.Bottom;
                    int maxOptionWidth = typeButtonWidth;
                    foreach (string opt in appStoreTypeOptions)
                    {
                        int optW = (int)MeasurePhoneText(Game1.smallFont, ModEntry.SHelper.Translation.Get("ui.appstore.type." + opt.ToLower())).X + ScaleUiValue(20);
                        if (optW > maxOptionWidth) maxOptionWidth = optW;
                    }

                    IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), appStoreTypeButtonRect.X, dropY, maxOptionWidth, appStoreTypeOptions.Length * typeButtonHeight, Color.White, 1f, false);
                    for (int i = 0; i < appStoreTypeOptions.Length; i++)
                    {
                        DrawPhoneText(b, Game1.smallFont, ModEntry.SHelper.Translation.Get("ui.appstore.type." + appStoreTypeOptions[i].ToLower()), new Vector2(appStoreTypeButtonRect.X + ScaleUiValue(10), dropY + i * typeButtonHeight + ScaleUiValue(8)), Color.Black);
                    }
                }

                if (appStoreHoverText != null)
                {
                    IClickableMenu.drawHoverText(b, appStoreHoverText, Game1.smallFont);
                }
            }
            else if (appStoreCurrentState == AppStoreState.Detail && appStoreSelectedMod != null)
            {
                Rectangle phoneContentBounds = GetPhoneContentBounds();
                b.End();
                b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState() { ScissorTestEnable = true });
                Rectangle previousScissor = Game1.graphics.GraphicsDevice.ScissorRectangle;
                Game1.graphics.GraphicsDevice.ScissorRectangle = phoneContentBounds;

                int initialDetailY = phoneContentBounds.Y + ScaleUiValue(20);
                int detailY = initialDetailY - appStoreDetailScrollOffset;
                int paddingX = PhoneX(50);
                int contentWidth = width - ScaleUiValue(100);

                // NOTE: button will be drawn outside the scissored region later so it remains static


                int targetIconWidth = ScaleUiValue(360);
                int targetIconHeight = ScaleUiValue(360);

                if (appStoreSelectedMod.IconTexture != null)
                {
                    float aspectRatio = (float)appStoreSelectedMod.IconTexture.Width / appStoreSelectedMod.IconTexture.Height;
                    if (aspectRatio > 1)
                        targetIconHeight = (int)(targetIconWidth / aspectRatio);
                    else
                        targetIconWidth = (int)(targetIconHeight * aspectRatio);

                    b.Draw(appStoreSelectedMod.IconTexture, new Rectangle(xPositionOnScreen + width / 2 - targetIconWidth / 2, detailY, targetIconWidth, targetIconHeight), Color.White);
                }
                else
                {
                    b.Draw(Game1.staminaRect, new Rectangle(xPositionOnScreen + width / 2 - targetIconWidth / 2, detailY, targetIconWidth, targetIconHeight), Color.Gray);
                }
                detailY += targetIconHeight + ScaleUiValue(15);


                string nameTextWrapped = Game1.parseText(appStoreSelectedMod.Name, Game1.dialogueFont, GetPhoneScaledWrapWidth(contentWidth));
                Vector2 nameSize = MeasurePhoneText(Game1.dialogueFont, nameTextWrapped);
                DrawPhoneText(b, Game1.dialogueFont, nameTextWrapped, new Vector2(xPositionOnScreen + width / 2 - nameSize.X / 2, detailY), Color.DarkBlue);
                detailY += (int)nameSize.Y + ScaleUiValue(5);

                string authorText = ModEntry.SHelper.Translation.Get("ui.appstore.by_author", new { author = appStoreSelectedMod.Author });
                Vector2 authorSize = MeasurePhoneText(Game1.smallFont, authorText);
                DrawPhoneText(b, Game1.smallFont, authorText, new Vector2(xPositionOnScreen + width / 2 - authorSize.X / 2, detailY), Color.DarkSlateGray);
                detailY += ScaleUiValue(40);

                string endorsementText = ModEntry.SHelper.Translation.Get("ui.appstore.endorsements_detail", new { count = appStoreSelectedMod.TotalEndorsement });
                DrawPhoneText(b, Game1.smallFont, endorsementText, new Vector2(paddingX, detailY), Color.DarkRed, 0.7f);
                detailY += ScaleUiValue(25);

                var modInfo = ModEntry.SHelper.ModRegistry.Get(appStoreSelectedMod.UniqueID);
                if (modInfo != null)
                {
                    string currentVersion = modInfo.Manifest.Version.ToString();
                    bool isUpToDate = currentVersion == appStoreSelectedMod.LatestVersion;
                    string versionText = isUpToDate ? ModEntry.SHelper.Translation.Get("ui.appstore.latest_version_installed") : ModEntry.SHelper.Translation.Get("ui.appstore.new_version_available");
                    Color versionColor = isUpToDate ? Color.Green : Color.Red;
                    DrawPhoneText(b, Game1.smallFont, versionText, new Vector2(paddingX, detailY), versionColor, 0.7f);
                    detailY += ScaleUiValue(25);
                }

                detailY += ScaleUiValue(15);

                string shortDescWrapped = Game1.parseText(appStoreSelectedMod.ShortDescription, Game1.smallFont, GetPhoneScaledWrapWidth(contentWidth, 0.8f));
                DrawPhoneText(b, Game1.smallFont, shortDescWrapped, new Vector2(paddingX, detailY), Color.DarkSlateGray, 0.8f);
                detailY += ScaleUiValue(30) + (int)MeasurePhoneText(Game1.smallFont, shortDescWrapped, 0.8f).Y;

                b.Draw(Game1.staminaRect, new Rectangle(paddingX, detailY, contentWidth, 2), new Color(0, 0, 0, 90));
                detailY += ScaleUiValue(20);

                string[] lines = appStoreSelectedMod.FullDescription.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    bool isCenter = false;
                    bool isBold = false;
                    string content = line.Trim();

                    if (content.StartsWith("[center]") && content.Contains("[/center]"))
                    {
                        isCenter = true;
                        content = content.Replace("[center]", "").Replace("[/center]", "").Trim();
                    }


                    if (content.StartsWith("[img]") && content.EndsWith("[/img]"))
                    {
                        string imgUrl = content.Substring(5, content.Length - 11).Trim();
                        Texture2D imgTex = AppStoreManager.GetOrFetchDescriptionImage(imgUrl);
                        if (imgTex != null)
                        {
                            int imgWidth = imgTex.Width;
                            int imgHeight = imgTex.Height;
                            if (imgWidth > contentWidth)
                            {
                                float ratio = (float)contentWidth / imgWidth;
                                imgWidth = contentWidth;
                                imgHeight = (int)(imgHeight * ratio);
                            }
                            int imgX = isCenter ? xPositionOnScreen + width / 2 - imgWidth / 2 : paddingX;
                            b.Draw(imgTex, new Rectangle(imgX, detailY, imgWidth, imgHeight), Color.White);
                            detailY += imgHeight + ScaleUiValue(15);
                        }
                        else
                        {
                            int pHeight = ScaleUiValue(100);
                            b.Draw(Game1.staminaRect, new Rectangle(paddingX, detailY, contentWidth, pHeight), Color.LightGray * 0.5f);
                            detailY += pHeight + ScaleUiValue(15);
                        }
                        continue;
                    }

                    if (content.Contains("[b]") && content.Contains("[/b]"))
                    {
                        isBold = true;
                        content = content.Replace("[b]", "").Replace("[/b]", "");
                    }

                    float textScale = isBold ? 1.0f : 0.8f;
                    string wrappedText = Game1.parseText(content, Game1.smallFont, GetPhoneScaledWrapWidth(contentWidth, textScale));
                    Vector2 textSize = MeasurePhoneText(Game1.smallFont, wrappedText, textScale);


                    int textX = isCenter ? xPositionOnScreen + width / 2 - (int)textSize.X / 2 : paddingX;
                    DrawPhoneText(b, Game1.smallFont, wrappedText, new Vector2(textX, detailY), Color.Black, textScale);
                    detailY += (int)textSize.Y + ScaleUiValue(15);
                }

                // appStoreMaxDetailScroll = Math.Max(0, detailY + appStoreDetailScrollOffset - (initialDetailY + phoneContentBounds.Height));
                appStoreMaxDetailScroll = Math.Max(0, detailY + appStoreDetailScrollOffset + ScaleUiValue(20) - (phoneContentBounds.Y + phoneContentBounds.Height));



                b.End();
                Game1.graphics.GraphicsDevice.ScissorRectangle = previousScissor;
                b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

                // Draw a static "Go to Mod" button outside the scissored content so it does not scroll
                string goToModText = ModEntry.SHelper.Translation.Get("ui.appstore.go_to_mod");
                Vector2 goToModSize = MeasurePhoneText(Game1.smallFont, goToModText);
                int buttonWidth = (int)goToModSize.X + ScaleUiValue(20);
                int buttonHeight = (int)goToModSize.Y + ScaleUiValue(10);
                int buttonX = PhoneX(width - 40 - buttonWidth);
                int buttonY = PhoneY(height - 120);

                appStoreGoToModButtonRect = new Rectangle(buttonX, buttonY, buttonWidth, buttonHeight);
                IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), appStoreGoToModButtonRect.X, appStoreGoToModButtonRect.Y, appStoreGoToModButtonRect.Width, appStoreGoToModButtonRect.Height, new Color(255, 255, 255, 220), 1f, false);
                DrawPhoneText(b, Game1.smallFont, goToModText, new Vector2(appStoreGoToModButtonRect.X + ScaleUiValue(10), appStoreGoToModButtonRect.Y + ScaleUiValue(8)), Color.Black);
            }
        }

        private void DrawLoopingAppStoreText(SpriteBatch b, SpriteFont font, string text, Vector2 position, float scale, int maxWidth, Color color)
        {
            if (string.IsNullOrEmpty(text))
                return;

            if (MeasurePhoneText(font, text, scale).X <= maxWidth)
            {
                DrawPhoneText(b, font, text, position, color, scale);
                return;
            }

            string marqueeSource = text + new string(' ', 5);
            Rectangle clipRect = new Rectangle(
                (int)Math.Floor(position.X),
                (int)Math.Floor(position.Y),
                Math.Max(1, maxWidth),
                Math.Max(1, (int)Math.Ceiling(font.LineSpacing * GetPhoneTextScale(scale) + 2f)));

            Rectangle viewportBounds = Game1.graphics.GraphicsDevice.Viewport.Bounds;
            clipRect = Rectangle.Intersect(clipRect, viewportBounds);
            if (clipRect.Width <= 0 || clipRect.Height <= 0)
                return;

            float marqueeWidth = MeasurePhoneText(font, marqueeSource, scale).X;
            if (marqueeWidth <= 0f)
            {
                DrawPhoneText(b, font, text, position, color, scale);
                return;
            }

            float scrollOffset = (float)((appLabelMarqueeElapsedSeconds * 45f) % marqueeWidth);
            Vector2 drawPos = new Vector2(position.X - scrollOffset, position.Y);

            b.End();

            Rectangle previousScissorRect = Game1.graphics.GraphicsDevice.ScissorRectangle;
            Game1.graphics.GraphicsDevice.ScissorRectangle = clipRect;

            RasterizerState scissorRasterizer = new RasterizerState { ScissorTestEnable = true };
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, scissorRasterizer);

            DrawPhoneText(b, font, marqueeSource, drawPos, color, scale);
            DrawPhoneText(b, font, marqueeSource, new Vector2(drawPos.X + marqueeWidth, drawPos.Y), color, scale);

            b.End();
            Game1.graphics.GraphicsDevice.ScissorRectangle = previousScissorRect;
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
        }

        private string TruncateAppStoreText(SpriteFont font, string text, float scale, int maxWidth)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (MeasurePhoneText(font, text, scale).X <= maxWidth) return text;


            string result = text;
            while (result.Length > 0 && MeasurePhoneText(font, result + "...", scale).X > maxWidth)
            {
                result = result.Substring(0, result.Length - 1);
            }
            return result + "...";
        }

        private void ReceiveLeftClickAppStore(int x, int y)
        {
            if (appStoreCurrentState == AppStoreState.List)
            {
                if (appStoreTypeDropdownOpen)
                {
                    int dropY = appStoreTypeButtonRect.Bottom;
                    int maxOptionWidth = appStoreTypeButtonRect.Width;
                    foreach (string opt in appStoreTypeOptions)
                    {
                        int optW = (int)MeasurePhoneText(Game1.smallFont, ModEntry.SHelper.Translation.Get("ui.appstore.type." + opt.ToLower())).X + ScaleUiValue(20);
                        if (optW > maxOptionWidth) maxOptionWidth = optW;
                    }

                    Rectangle dropRect = new Rectangle(appStoreTypeButtonRect.X, dropY, maxOptionWidth, appStoreTypeOptions.Length * appStoreTypeButtonRect.Height);
                    if (dropRect.Contains(x, y))
                    {
                        int index = (y - dropY) / appStoreTypeButtonRect.Height;
                        if (index >= 0 && index < appStoreTypeOptions.Length)
                        {
                            Game1.playSound("smallSelect");
                            appStoreCurrentType = appStoreTypeOptions[index];
                            AppStoreManager.ApplySortAndTypeFilter(appStoreCurrentSort, appStoreCurrentType);
                            appStoreCurrentPage = 0;
                            appStoreScrollOffset = 0;
                        }
                    }
                    appStoreTypeDropdownOpen = false;
                    return;
                }

                if (appStoreDropdownOpen)
                {
                    int maxOptionWidth = appStoreSortButtonRect.Width;
                    foreach (string opt in appStoreSortOptions)
                    {
                        int optW = (int)MeasurePhoneText(Game1.smallFont, ModEntry.SHelper.Translation.Get("ui.appstore.sort." + opt.ToLower())).X + ScaleUiValue(20);
                        if (optW > maxOptionWidth) maxOptionWidth = optW;
                    }
                    int dropY = appStoreSortButtonRect.Top - appStoreSortOptions.Length * appStoreSortButtonRect.Height;

                    Rectangle dropRect = new Rectangle(appStoreSortButtonRect.X, dropY, maxOptionWidth, appStoreSortOptions.Length * appStoreSortButtonRect.Height);
                    if (dropRect.Contains(x, y))
                    {
                        int index = (y - dropY) / appStoreSortButtonRect.Height;
                        if (index >= 0 && index < appStoreSortOptions.Length)
                        {
                            Game1.playSound("smallSelect");
                            appStoreCurrentSort = appStoreSortOptions[index];
                            AppStoreManager.ApplySortAndTypeFilter(appStoreCurrentSort, appStoreCurrentType);
                            appStoreCurrentPage = 0;
                            appStoreScrollOffset = 0;
                        }
                    }
                    appStoreDropdownOpen = false;
                    return;
                }

                if (appStoreTypeButtonRect.Contains(x, y))
                {
                    Game1.playSound("smallSelect");
                    appStoreTypeDropdownOpen = true;
                    return;
                }

                if (appStoreSortButtonRect.Contains(x, y))
                {
                    Game1.playSound("smallSelect");
                    appStoreDropdownOpen = true;
                    return;
                }

                int totalPages = (int)Math.Ceiling((double)AppStoreManager.Mods.Count / appStoreItemsPerPage);

                if (totalPages > 1)
                {
                    if (appStoreCurrentPage > 0 && appStorePrevPageButton != null && appStorePrevPageButton.containsPoint(x, y))
                    {
                        Game1.playSound("shwip");
                        appStoreCurrentPage--;
                        appStoreScrollOffset = 0;
                        return;
                    }
                    if (appStoreCurrentPage < totalPages - 1 && appStoreNextPageButton != null && appStoreNextPageButton.containsPoint(x, y))
                    {
                        Game1.playSound("shwip");
                        appStoreCurrentPage++;
                        appStoreScrollOffset = 0;
                        return;
                    }
                }

                int startIndex = appStoreCurrentPage * appStoreItemsPerPage;
                int endIndex = Math.Min(startIndex + appStoreItemsPerPage, AppStoreManager.Mods.Count);
                int pageItemCount = endIndex - startIndex;

                int currentY = PhoneY(130);
                int scaledItemHeight = ScaleUiValue(appStoreItemHeight);
                int scaledSlotWidth = width - ScaleUiValue(50);
                int slotX = PhoneX(25);

                for (int i = appStoreScrollOffset; i < Math.Min(appStoreScrollOffset + appStoreMaxVisibleItems, pageItemCount); i++)
                {
                    int modIndex = startIndex + i;
                    if (modIndex >= AppStoreManager.Mods.Count) break;

                    Rectangle slotRect = new Rectangle(slotX, currentY, scaledSlotWidth, scaledItemHeight - ScaleUiValue(10));
                    if (slotRect.Contains(x, y))
                    {
                        Game1.playSound("smallSelect");
                        appStoreSelectedMod = AppStoreManager.Mods[modIndex];
                        appStoreCurrentState = AppStoreState.Detail;
                        appStoreDetailScrollOffset = 0;
                        return;
                    }

                    currentY += scaledItemHeight;
                }
            }
            else if (appStoreCurrentState == AppStoreState.Detail)
            {
                if (appStoreGoToModButtonRect.Contains(x, y) && appStoreSelectedMod != null && !string.IsNullOrWhiteSpace(appStoreSelectedMod.ModURL))
                {
                    Game1.playSound("smallSelect");
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = appStoreSelectedMod.ModURL,
                        UseShellExecute = true
                    });
                    return;
                }
            }
        }

        private bool TryHandleAppStoreBackButton()
        {
            if (appStoreCurrentState == AppStoreState.Detail)
            {
                appStoreCurrentState = AppStoreState.List;
                appStoreSelectedMod = null;
                appStoreDetailScrollOffset = 0;
                return true;
            }
            return false;
        }

        private void ReceiveScrollWheelActionAppStore(int direction)
        {
            if (appStoreCurrentState == AppStoreState.Detail)
            {
                int scrollAmount = ScaleUiValue(30);
                if (direction > 0)
                {
                    appStoreDetailScrollOffset -= scrollAmount;
                }
                else if (direction < 0)
                {
                    appStoreDetailScrollOffset += scrollAmount;
                }
                appStoreDetailScrollOffset = Math.Max(0, Math.Min(appStoreDetailScrollOffset, appStoreMaxDetailScroll));
                return;
            }

            if (appStoreCurrentState != AppStoreState.List) return;

            int startIndex = appStoreCurrentPage * appStoreItemsPerPage;
            int endIndex = Math.Min(startIndex + appStoreItemsPerPage, AppStoreManager.Mods.Count);
            int pageItemCount = endIndex - startIndex;

            if (direction > 0 && appStoreScrollOffset > 0)
            {
                appStoreScrollOffset--;
            }
            else if (direction < 0 && appStoreScrollOffset < Math.Max(0, pageItemCount - appStoreMaxVisibleItems))
            {
                appStoreScrollOffset++;
            }
        }

        private void ResetAppStoreState()
        {
            appStoreCurrentState = AppStoreState.List;
            appStoreSelectedMod = null;
            appStoreCurrentPage = 0;
            appStoreScrollOffset = 0;
            appStoreDetailScrollOffset = 0;
            appStoreMaxDetailScroll = 0;
            appStoreDropdownOpen = false;
            appStoreTypeDropdownOpen = false;
            appStoreCurrentSort = "Latest";
            appStoreCurrentType = "App";
            AppStoreManager.ApplySortAndTypeFilter(appStoreCurrentSort, appStoreCurrentType);
        }
    }
}
