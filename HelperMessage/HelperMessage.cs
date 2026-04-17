using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;
using TextCopy;
using System.Text;
using StardewModdingAPI;

namespace Smartphone
{
    public partial class PhoneMenu
    {
        private const string TextAppState = "appText";

        private bool IsTextAppOpen()
        {
            return currentApp == TextAppState;
        }

        private void DrawTextApp(SpriteBatch b)
        {
            b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height), Color.Black * 0.6f);
            b.Draw(texturePhoneCapture, new Vector2(xPositionOnScreen, yPositionOnScreen), Color.White);
            b.Draw(texturePhoneBackground, new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 116), Color.White);
            backButton.draw(b);


            if (selectedNpc == null)
            {
                chatPhotoButtonBounds = Rectangle.Empty;
                favourityNpcButton.Clear();
                socialProfileNpcButtonBounds.Clear();
                int yStart = yPositionOnScreen + 150;
                for (int i = 0; i < visibleSlots; i++)
                {
                    int index = i + scrollOffset;
                    if (index >= messageableNpcList.Count) break;

                    var npc = messageableNpcList[index];
                    int y = yStart + i * spacing;



                    // === Draw scaled background box behind portrait ===
                    int portraitSize = 56; // Target display size
                    float scale = portraitSize / 64f * 1.5f; // Your texture is 64x64

                    Vector2 boxPosition = new Vector2(xPositionOnScreen + 100, y);

                    int unread = MessageManager.GetUnreadCount(npc.name);
                    if (unread > 0)
                    {
                        string number = Math.Min(unread, 9).ToString(); // display max 9
                        int digitSize = 8;
                        int spacingBetweenDigits = 0;
                        int totalWidth = number.Length * (digitSize + spacingBetweenDigits);

                        int numberX = (int)boxPosition.X - totalWidth - 41;
                        int numberY = (int)boxPosition.Y + 15;

                        int digitWidth = 8;
                        int digitHeight = 8;

                        int digitsPerRow = 6;

                        for (int j = 0; j < number.Length; j++)
                        {
                            char c = number[j];
                            if (char.IsDigit(c))
                            {
                                int digit = c - '0';

                                int row = digit / digitsPerRow;
                                int col = digit % digitsPerRow;

                                Rectangle sourceRect = new Rectangle(
                                    512 + col * digitWidth,     // X offset starts at 512
                                    128 + row * digitHeight,    // Y offset starts at 128
                                    digitWidth,
                                    digitHeight
                                );

                                b.Draw(
                                    Game1.mouseCursors,
                                    new Vector2(numberX, numberY),
                                    sourceRect,
                                    Color.White,
                                    0f,
                                    Vector2.Zero,
                                    5f, // Scale it up to be visible
                                    SpriteEffects.None,
                                    1f
                                );
                            }
                        }
                    }



                    b.Draw(
                        texturePortraitBackground,
                        position: boxPosition,
                        sourceRectangle: null,
                        color: Color.White,
                        rotation: 0f,
                        origin: Vector2.Zero,
                        scale: scale,
                        effects: SpriteEffects.None,
                        layerDepth: 0f
                    );
                    var realNpc = Game1.getCharacterFromName(npc.name);

                    // === Draw NPC portrait ===
                    if (realNpc.Portrait != null)
                    {
                        Rectangle portraitRect = new Rectangle((int)boxPosition.X + 9, (int)boxPosition.Y + 10, 67, 67);
                        Rectangle sourceRect = new Rectangle(0, 0, 64, 64);
                        b.Draw(realNpc.Portrait, portraitRect, sourceRect, Color.White);
                    }

                    // === Draw NPC name ===
                    int nameX = (int)boxPosition.X + portraitSize + 40;
                    npc.bounds = new Rectangle(nameX, y, 200, portraitSize);
                    b.DrawString(Game1.dialogueFont, npc.name, new Vector2(nameX, y + 15), Color.Black);


                    var rect = new Rectangle(218, 428, 7, 7);
                    if (MessageManager.favouriteNpc.Contains(npc.name))
                        rect = new Rectangle(211, 428, 7, 7);

                    heartButton = new ClickableTextureComponent(
                    name: npc.name,
                    bounds: new Rectangle(nameX + 280, y + 20, 35, 35),
                        label: null,
                        hoverText: "",
                    texture: Game1.mouseCursors,
                        sourceRect: rect,
                        scale: 5f
                    );

                    favourityNpcButton[npc.name] = heartButton;
                    heartButton.draw(b);

                    Rectangle profileButtonBounds = new Rectangle(nameX + 330, y + 20, 35, 35);
                    socialProfileNpcButtonBounds[npc.name] = profileButtonBounds;
                    DrawOpenSocialProfileButton(b, profileButtonBounds);

                }

                // Draw input box
                int inputX = xPositionOnScreen + 100;
                int inputY = yPositionOnScreen + 835;
                int inputWidth = 400;

                int fontHeight = (int)Game1.smallFont.MeasureString("A").Y;
                int inputHeight = fontHeight + 30;

                IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                    inputX, inputY, inputWidth, inputHeight,
                    Color.White, 1f, false);

                DrawEditableTextInput(b, new Rectangle(inputX, inputY, inputWidth, inputHeight), currentMessage, currentMessageCursorIndex, currentMessageSelectionAnchorIndex);

                // enable ghost textBox. do not remove
                textBox.Selected = true;
                Game1.keyboardDispatcher.Subscriber = textBox;
            }
            else
            {
                b.DrawString(Game1.dialogueFont, $"{selectedNpc}", new Vector2(xPositionOnScreen + 105, yPositionOnScreen + 65), Color.Black);

                backButton.draw(b);
                b.Draw(
                    removeButton.texture,
                    new Vector2(removeButton.bounds.X, removeButton.bounds.Y),
                    removeButton.sourceRect,
                    Color.White * 0.8f,
                    0f,
                    Vector2.Zero,
                    removeButton.scale,
                    SpriteEffects.None,
                    1f
                );


                // Save old spritebatch state and apply clipping
                b.End();

                Rectangle chatClipRect = new Rectangle(xPositionOnScreen, yPositionOnScreen + ChatViewportYOffset, width, ChatViewportHeight);
                Game1.graphics.GraphicsDevice.ScissorRectangle = chatClipRect;

                b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState() { ScissorTestEnable = true });

                // Draw messages within clipped region
                List<ChatMessageEntry> chatMessages = BuildChatEntries(messageHistory);
                chatPhotoHoverEntries.Clear();
                chatPhotoNavigationEntries.Clear();

                int messageY = yPositionOnScreen + ChatViewportYOffset - (int)MathF.Floor(chatScrollOffset);
                SpriteFont font = Game1.smallFont;
                int lineHeight = (int)font.MeasureString("A").Y + 4;
                int visibleTop = chatClipRect.Top - ScrollDrawOverscan;
                int visibleBottom = chatClipRect.Bottom + ScrollDrawOverscan;

                foreach (ChatMessageEntry entry in chatMessages)
                {
                    int bubbleHeight = CalculateChatEntryHeight(entry, font, lineHeight);
                    int bubbleWidth = CalculateChatEntryWidth(entry, font);

                    Rectangle bubbleRect = entry.IsPlayer
                        ? new Rectangle(xPositionOnScreen + width - bubbleWidth - 50, messageY, bubbleWidth, bubbleHeight)
                        : new Rectangle(xPositionOnScreen + 50, messageY, bubbleWidth, bubbleHeight);

                    int bubbleTop = bubbleRect.Y;
                    int bubbleBottom = bubbleRect.Bottom + 10;
                    if (bubbleBottom < visibleTop)
                    {
                        messageY += bubbleHeight + 10;
                        continue;
                    }

                    if (bubbleTop > visibleBottom)
                        break;
                    // Draw photo
                    if (entry.IsPhoto)
                    {
                        IClickableMenu.drawTextureBox(
                            b,
                            Game1.menuTexture,
                            new Rectangle(0, 256, 60, 60),
                            bubbleRect.X - 5,
                            bubbleRect.Y,
                            bubbleRect.Width + 12,
                            bubbleRect.Height + 10,
                            new Color(255, 255, 255, 200),
                            1f,
                            false
                        );

                        Point groupSize = GetChatPhotoGroupDrawSize(entry);
                        Rectangle imageBounds = new Rectangle(
                            bubbleRect.X + 10,
                            bubbleRect.Y + 10,
                            groupSize.X,
                            groupSize.Y);

                        string activePhotoPath = GetActiveChatPhotoPath(entry);
                        if (TryGetChatImageTexture(activePhotoPath, out Texture2D imageTexture))
                        {
                            Rectangle drawBounds = GetScaledDrawBoundsInArea(imageTexture, imageBounds);
                            b.Draw(imageTexture, drawBounds, Color.White);
                        }
                        else
                        {
                            IClickableMenu.drawTextureBox(
                                b,
                                Game1.menuTexture,
                                new Rectangle(0, 256, 60, 60),
                                imageBounds.X,
                                imageBounds.Y,
                                imageBounds.Width,
                                imageBounds.Height,
                                new Color(210, 210, 210, 210),
                                1f,
                                false);

                            string missingText = "Image unavailable";
                            Vector2 size = font.MeasureString(missingText);
                            Vector2 pos = new Vector2(
                                imageBounds.X + (imageBounds.Width - size.X) / 2f,
                                imageBounds.Y + (imageBounds.Height - size.Y) / 2f);
                            b.DrawString(font, missingText, pos, Color.DarkSlateGray);
                        }

                        if (entry.PhotoPaths.Count > 1)
                        {
                            Rectangle previousBounds = new Rectangle(
                                imageBounds.X + 6,
                                imageBounds.Y + (imageBounds.Height / 2) - 20,
                                40,
                                40);
                            Rectangle nextBounds = new Rectangle(
                                imageBounds.Right - 46,
                                imageBounds.Y + (imageBounds.Height / 2) - 20,
                                40,
                                40);

                            DrawSocialImageNavButton(b, previousBounds, isNext: false);
                            DrawSocialImageNavButton(b, nextBounds, isNext: true);

                            chatPhotoNavigationEntries.Add(new ChatPhotoNavigationEntry
                            {
                                GroupId = entry.PhotoGroupId,
                                PhotoCount = entry.PhotoPaths.Count,
                                PreviousBounds = previousBounds,
                                NextBounds = nextBounds
                            });

                            int currentPhotoIndex = GetChatPhotoGroupIndex(entry.PhotoGroupId, entry.PhotoPaths.Count);
                            string indexLabel = $"{currentPhotoIndex + 1}/{entry.PhotoPaths.Count}";
                            Vector2 indexSize = font.MeasureString(indexLabel);
                            Vector2 indexPos = new Vector2(
                                imageBounds.X + (imageBounds.Width - indexSize.X) / 2f,
                                imageBounds.Bottom - indexSize.Y - 4);
                            b.DrawString(font, indexLabel, indexPos, Color.White);
                        }

                        if (!string.IsNullOrWhiteSpace(entry.PhotoTag))
                        {
                            chatPhotoHoverEntries.Add(new ChatPhotoHoverEntry
                            {
                                Bounds = imageBounds,
                                TagText = entry.PhotoTag
                            });
                        }
                    }
                    // Draw message
                    else if (!entry.IsSystem)
                    {
                        List<string> wrappedLines = SplitTextIntoLines(entry.Text, font, maxBubbleWidth);

                        IClickableMenu.drawTextureBox(
                            b,
                            Game1.menuTexture,
                            new Rectangle(0, 256, 60, 60),
                            bubbleRect.X - 5,
                            bubbleRect.Y,
                            bubbleRect.Width + 12,
                            bubbleRect.Height + 10,
                            new Color(255, 255, 255, 200),
                            1f,
                            false
                        );

                        int textY = bubbleRect.Y + 15;
                        foreach (string line in wrappedLines)
                        {
                            Vector2 linePos = new Vector2(bubbleRect.X + 10, textY);
                            b.DrawString(font, line, linePos, Color.Black);
                            textY += lineHeight;
                        }
                    }
                    // Draw system message
                    else
                    {
                        int textY = bubbleRect.Y + 15;
                        Vector2 linePos = new Vector2(bubbleRect.X + 10, textY);
                        b.DrawString(font, entry.Text, linePos, Color.Black);
                    }

                    messageY += bubbleHeight + 10;
                }

                // Reset clipping
                b.End();
                b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
                DrawChatImageTagTooltipIfHovered(b, chatClipRect);

                if (selectedNpc != null && (ModEntry.npcMessagesToday.ContainsKey(selectedNpc) && ModEntry.npcMessagesToday[selectedNpc].Count > 0 || ModEntry.Config.DisableDailyMessage))
                {
                    okButton.draw(b);
                    // Draw input box
                    int inputY = yPositionOnScreen + 850;

                    int fontHeight = (int)Game1.smallFont.MeasureString("A").Y;
                    int inputHeight = fontHeight + 30;

                    chatPhotoButtonBounds = new Rectangle(
                        xPositionOnScreen + 50,
                        inputY,
                        ChatAttachmentButtonWidth,
                        inputHeight);

                    DrawChatAttachmentButton(b, chatPhotoButtonBounds, chatSelectedPhotos.Count);

                    int inputX = chatPhotoButtonBounds.Right + 8;
                    int inputWidth = 370;

                    IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                        inputX, inputY, inputWidth, inputHeight,
                        Color.White, 1f, false);

                    DrawEditableTextInput(b, new Rectangle(inputX, inputY, inputWidth, inputHeight), currentMessage, currentMessageCursorIndex, currentMessageSelectionAnchorIndex);

                    // enable ghost textBox. do not remove
                    textBox.Selected = true;
                    Game1.keyboardDispatcher.Subscriber = textBox;
                }
                else
                {
                    chatPhotoButtonBounds = Rectangle.Empty;
                    string firstMessage = Game1.timeOfDay < 1200 ? $"Good morning {selectedNpc}" : Game1.timeOfDay < 1800 ? $"Good afternoon {selectedNpc}" : $"Good evening {selectedNpc}";
                    float maxWidth = Game1.smallFont.MeasureString(firstMessage).X + 20f;
                    int firstMessageLineHeight = (int)(Game1.smallFont.MeasureString("A").Y + 20);

                    Vector2 position = new Vector2(xPositionOnScreen + 300 - (maxWidth + 20) / 2, yPositionOnScreen + 850);
                    firstMessageBounds = new Rectangle(
                        (int)position.X,
                        (int)position.Y,
                        (int)maxWidth + 20,
                        firstMessageLineHeight + 10
                    );

                    IClickableMenu.drawTextureBox(
                        Game1.spriteBatch,
                        Game1.menuTexture,
                        new Rectangle(0, 256, 60, 60),
                        firstMessageBounds.X,
                        firstMessageBounds.Y,
                        firstMessageBounds.Width,
                        firstMessageBounds.Height,
                        Color.White,
                        1f,
                        false
                    );

                    Utility.drawTextWithShadow(
                        Game1.spriteBatch,
                        firstMessage,
                        Game1.smallFont,
                        new Vector2(firstMessageBounds.X + 20, firstMessageBounds.Y + 20),
                        Game1.textColor
                    );

                }


                if (currentSuggestion.Item1 != "" && ModEntry.Config.HelperOption == "Minimal")
                {
                    string suggestionText = currentSuggestion.Item1;
                    string[] lines = suggestionText.Split('\n');

                    // Measure the width of the longest line
                    float maxWidth = lines.Max(line => Game1.smallFont.MeasureString(line).X) + 15f;

                    // Get font line height (all lines same height in pixel font)
                    int suggestionLineHeight = (int)(Game1.smallFont.MeasureString("A").Y + 5);

                    Vector2 position = new Vector2(xPositionOnScreen + 600, yPositionOnScreen + 800);
                    messageSuggestionBounds = new Rectangle(
                        (int)position.X,
                        (int)position.Y,
                        (int)maxWidth + 20,
                        suggestionLineHeight * lines.Length + 10
                    );

                    IClickableMenu.drawTextureBox(
                        Game1.spriteBatch,
                        Game1.menuTexture,
                        new Rectangle(0, 256, 60, 60),
                        messageSuggestionBounds.X,
                        messageSuggestionBounds.Y,
                        messageSuggestionBounds.Width,
                        messageSuggestionBounds.Height,
                        Color.White,
                        1f,
                        false
                    );

                    for (int i = 0; i < lines.Length; i++)
                    {
                        Utility.drawTextWithShadow(
                            Game1.spriteBatch,
                            lines[i],
                            Game1.smallFont,
                            new Vector2(messageSuggestionBounds.X + 15, messageSuggestionBounds.Y + 10 + i * suggestionLineHeight),
                            Game1.textColor
                        );
                    }
                }
                else if (ModEntry.Config.HelperOption == "Always" && currentMessage != "")
                {
                    string[] lines = { "Type one of these value",
                        "to start event manually:",
                        "  [Trigger Picnic Event]",
                        "  [Trigger Dinner Event]",
                        "  [Trigger Campfire Event]",
                        "  [Trigger Birthday Event]"
                    };

                    float maxWidth = lines.Max(line => Game1.smallFont.MeasureString(line).X) + 15f;

                    int helperLineHeight = (int)(Game1.smallFont.MeasureString("A").Y + 5);

                    Vector2 position = new Vector2(xPositionOnScreen + 600, yPositionOnScreen + 650);
                    messageSuggestionBounds = new Rectangle(
                        (int)position.X,
                        (int)position.Y,
                        (int)maxWidth + 20,
                        helperLineHeight * lines.Length + 10
                    );

                    IClickableMenu.drawTextureBox(
                        Game1.spriteBatch,
                        Game1.menuTexture,
                        new Rectangle(0, 256, 60, 60),
                        messageSuggestionBounds.X,
                        messageSuggestionBounds.Y,
                        messageSuggestionBounds.Width,
                        messageSuggestionBounds.Height,
                        Color.White,
                        1f,
                        false
                    );

                    for (int i = 0; i < lines.Length; i++)
                    {
                        Utility.drawTextWithShadow(
                            Game1.spriteBatch,
                            lines[i],
                            Game1.smallFont,
                            new Vector2(messageSuggestionBounds.X + 15, messageSuggestionBounds.Y + 10 + i * helperLineHeight),
                            Game1.textColor
                        );
                    }
                }

                if (chatPhotoPickerOpen)
                    DrawChatPhotoPickerMenu(b);
            }
        }
        private void DrawOpenSocialProfileButton(SpriteBatch b, Rectangle bounds)
        {
            IClickableMenu.drawTextureBox(
                b,
                Game1.mouseCursors,
                new Rectangle(80, 0, 13, 13),
                bounds.X - 3,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                new Color(255, 255, 255, 220),
                3f,
                false);
        }
        private void UpdateTextChatScroll(GameTime time)
        {
            ClampChatScroll();

            float lerpAmount = (float)(time.ElapsedGameTime.TotalSeconds * ChatScrollLerpSpeed);
            lerpAmount = Math.Clamp(lerpAmount, 0f, 1f);
            chatScrollOffset = MathHelper.Lerp(chatScrollOffset, chatScrollTarget, lerpAmount);

            if (Math.Abs(chatScrollOffset - chatScrollTarget) <= 0.5f)
                chatScrollOffset = chatScrollTarget;
        }

        private bool HandleTextPhotoPickerModalClick(int x, int y)
        {
            if (!IsTextAppOpen() || selectedNpc == null || !chatPhotoPickerOpen)
                return false;

            HandleChatPhotoPickerClick(x, y);
            return true;
        }

        private bool HandleTextSuggestionClick(int x, int y)
        {
            if (selectedNpc == null || currentSuggestion.Item1 == "" || !messageSuggestionBounds.Contains(x, y))
                return false;

            string suggestedMessage = currentSuggestion.Item2.Replace("\n", " ");
            SetEditableTextFieldState(EditableTextFieldKind.Chat, suggestedMessage, suggestedMessage.Length, suggestedMessage.Length);
            currentSuggestion = new("", "");
            RegisterTextInputActivity(selectedNpc);
            Game1.playSound("smallSelect");
            SnapChatScrollToBottom();
            return true;
        }

        private bool HandleTextFirstMessageClick(int x, int y)
        {
            if (selectedNpc == null || !firstMessageBounds.Contains(x, y))
                return false;

            if (ModEntry.npcMessagesToday.ContainsKey(selectedNpc) && ModEntry.npcMessagesToday[selectedNpc].Count > 0)
                return false;

            string firstMessage = Game1.timeOfDay < 1200
                ? $"Good morning {selectedNpc}"
                : Game1.timeOfDay < 1800
                    ? $"Good afternoon {selectedNpc}"
                    : $"Good evening {selectedNpc}";

            MessageManager.AddMessage(selectedNpc, $"PLAYER: {firstMessage}", isFromPlayer: true);
            ModEntry.FirstDailyText(selectedNpc, firstMessage);
            messageHistory = MessageManager.GetMessages(selectedNpc);
            SnapChatScrollToBottom();
            return true;
        }

        private bool HandleTextBackButtonClick()
        {
            if (!IsTextAppOpen())
                return false;

            if (chatPhotoPickerOpen)
            {
                CloseChatPhotoPicker(clearSelection: false);
                Game1.playSound("bigDeSelect");
                return true;
            }

            if (selectedNpc != null)
            {
                MessageManager.SetUnreadCount(selectedNpc);
                currentSuggestion = new("", "");
                selectedNpc = null;
                ResetEditableTextFieldState(EditableTextFieldKind.Search);
                CloseChatPhotoPicker(clearSelection: true);
                UpdateNpcList();
                return true;
            }

            UpdateNpcList();
            currentApp = null;
            ResetEditableTextFieldState(EditableTextFieldKind.Search);
            CloseChatPhotoPicker(clearSelection: true);
            return true;
        }

        private bool HandleTextRemoveButtonClick(int x, int y)
        {
            if (!IsTextAppOpen() || !removeButton.containsPoint(x, y))
                return false;
            messageHistory = MessageManager.GetMessages(selectedNpc);
            return true;
        }

        private bool HandleTextNpcListOrChatClick(int x, int y)
        {
            if (!IsTextAppOpen())
                return false;

            if (selectedNpc == null)
            {
                foreach (var button in favourityNpcButton.Values)
                {
                    if (!button.containsPoint(x, y))
                        continue;

                    if (MessageManager.favouriteNpc.Contains(button.name))
                        MessageManager.favouriteNpc.Remove(button.name);
                    else
                        MessageManager.favouriteNpc.Add(button.name);

                    DelayedAction.functionAfterDelay(() => UpdateNpcList(), 200);
                    Game1.playSound("smallSelect");
                    return true;
                }

                foreach (KeyValuePair<string, Rectangle> profileButton in socialProfileNpcButtonBounds)
                {
                    if (!profileButton.Value.Contains(x, y))
                        continue;

                    OpenSocialApp();
                    OpenSocialProfile(profileButton.Key, actorIsPlayer: false);
                    Game1.playSound("smallSelect");
                    return true;
                }

                int yStart = yPositionOnScreen + 150;

                for (int i = 0; i < visibleSlots; i++)
                {
                    int index = i + scrollOffset;
                    if (index >= messageableNpcList.Count)
                        break;

                    Rectangle slot = new Rectangle(xPositionOnScreen + 40, yStart + i * spacing, 400, 60);
                    if (!slot.Contains(x, y))
                        continue;

                    ResetEditableTextFieldState(EditableTextFieldKind.Chat);
                    selectedNpc = messageableNpcList[index].name;
                    MessageManager.SetUnreadCount(selectedNpc);
                    messageHistory = MessageManager.GetMessages(selectedNpc);
                    OpenChat(selectedNpc);
                    return true;
                }

                return false;
            }

            if (TryHandleChatPhotoNavigationClick(x, y))
                return true;

            if (chatPhotoButtonBounds.Contains(x, y))
            {
                OpenChatPhotoPicker();
                Game1.playSound("smallSelect");
                return true;
            }

            if (okButton.containsPoint(x, y))
            {
                if (!string.IsNullOrWhiteSpace(currentMessage) || chatSelectedPhotos.Count > 0)
                    onPlayerSend();

                return true;
            }

            return false;
        }

        private bool HandleTextScrollWheel(int direction)
        {
            if (!IsTextAppOpen())
                return false;

            if (chatPhotoPickerOpen)
                return true;

            if (selectedNpc != null)
            {
                float wheelSteps = direction / 120f;
                chatScrollTarget = Math.Clamp(
                    chatScrollTarget - wheelSteps * ChatScrollPixelsPerWheelNotch,
                    0f,
                    CalculateScrollToBottomOffset(messageHistory));

                return true;
            }

            scrollOffset -= direction / 120;
            scrollOffset = Math.Max(0, Math.Min(scrollOffset, messageableNpcList.Count - maxVisibleNPCs));
            return true;
        }

        private bool HandleTextKeyPress(Keys key)
        {
            if (!IsTextAppOpen())
                return false;

            if (key == Keys.Escape)
            {
                if (selectedNpc != null)
                {
                    if (chatPhotoPickerOpen)
                    {
                        CloseChatPhotoPicker(clearSelection: false);
                        return true;
                    }

                    MessageManager.SetUnreadCount(selectedNpc);
                    selectedNpc = null;
                    ResetEditableTextFieldState(EditableTextFieldKind.Search);
                    currentSuggestion = new("", "");
                    CloseChatPhotoPicker(clearSelection: true);
                    return true;
                }

                CloseChatPhotoPicker(clearSelection: true);
                ResetEditableTextFieldState(EditableTextFieldKind.Search);
                currentApp = null;
                return true;
            }

            if (selectedNpc == null)
                return HandleSearchInputKey(key, isRepeat: false);

            if (chatPhotoPickerOpen)
                return true;

                return HandleChatInputKey(key, isRepeat: false);
        }

        private bool ApplyRepeatableTextInputKey(EditableTextFieldKind field, Keys key)
        {
            return field switch
            {
                EditableTextFieldKind.Search => HandleSearchInputKey(key, isRepeat: true),
                EditableTextFieldKind.Chat => HandleChatInputKey(key, isRepeat: true),
                EditableTextFieldKind.SocialPost => HandleSocialPostInputKey(key, isRepeat: true),
                EditableTextFieldKind.SocialComment => HandleSocialCommentInputKey(key, isRepeat: true),
                _ => false
            };
        }

        private bool HandleSearchInputKey(Keys key, bool isRepeat)
        {
            bool handled = TryApplyEditableTextKeyToField(
                EditableTextFieldKind.Search,
                key,
                allowEnter: false,
                allowPaste: !isRepeat,
                out bool textChanged,
                out _);

            if (handled)
            {
                if (textChanged)
                    UpdateNpcList();

                if (!isRepeat)
                {
                    if (IsRepeatableTextInputKey(key))
                        BeginTextInputRepeat(EditableTextFieldKind.Search, key);
                    else
                        ResetTextInputRepeatState();

                    if (textChanged && ShouldPlayTypingSound(key, allowPaste: !isRepeat))
                        Game1.playSound("coin");
                }

                return true;
            }

            if (!isRepeat)
            {
                Game1.playSound("coin");
                UpdateNpcList();
                return true;
            }

            return false;
        }

        private bool HandleChatInputKey(Keys key, bool isRepeat)
        {
            bool handled = TryApplyEditableTextKeyToField(
                EditableTextFieldKind.Chat,
                key,
                allowEnter: true,
                allowPaste: !isRepeat,
                out bool textChanged,
                out bool submitted);

            if (!handled)
                return false;

            if (key != Keys.Enter && (!IsControlDown() || key == Keys.V))
                RegisterTextInputActivity(selectedNpc);

            if (textChanged && ShouldPlayTypingSound(key, allowPaste: !isRepeat))
                Game1.playSound("coin");

            if (submitted)
            {
                if (!string.IsNullOrWhiteSpace(currentMessage) || chatSelectedPhotos.Count > 0)
                    onPlayerSend();
            }

            if (!isRepeat)
            {
                if (IsRepeatableTextInputKey(key))
                    BeginTextInputRepeat(EditableTextFieldKind.Chat, key);
                else
                    ResetTextInputRepeatState();
            }

            return true;
        }

        private bool TryApplyEditableTextKeyToField(
            EditableTextFieldKind field,
            Keys key,
            bool allowEnter,
            bool allowPaste,
            out bool textChanged,
            out bool submitted)
        {
            textChanged = false;
            submitted = false;

            return field switch
            {
                EditableTextFieldKind.Search => TryApplyEditableTextKey(
                    EditableTextFieldKind.Search,
                    ref currentMessage,
                    ref currentMessageCursorIndex,
                    ref currentMessageSelectionAnchorIndex,
                    key,
                    allowEnter,
                    allowPaste,
                    out textChanged,
                    out submitted),
                EditableTextFieldKind.Chat => TryApplyEditableTextKey(
                    EditableTextFieldKind.Chat,
                    ref currentMessage,
                    ref currentMessageCursorIndex,
                    ref currentMessageSelectionAnchorIndex,
                    key,
                    allowEnter,
                    allowPaste,
                    out textChanged,
                    out submitted),
                EditableTextFieldKind.SocialPost => TryApplyEditableTextKey(
                    EditableTextFieldKind.SocialPost,
                    ref socialPostDraft,
                    ref socialPostDraftCursorIndex,
                    ref socialPostDraftSelectionAnchorIndex,
                    key,
                    allowEnter,
                    allowPaste,
                    out textChanged,
                    out submitted),
                EditableTextFieldKind.SocialComment => TryApplyEditableTextKey(
                    EditableTextFieldKind.SocialComment,
                    ref socialCommentDraft,
                    ref socialCommentDraftCursorIndex,
                    ref socialCommentDraftSelectionAnchorIndex,
                    key,
                    allowEnter,
                    allowPaste,
                    out textChanged,
                    out submitted),
                _ => false
            };
        }

        private bool TryApplyEditableTextKey(
            EditableTextFieldKind field,
            ref string text,
            ref int cursorIndex,
            ref int selectionAnchorIndex,
            Keys key,
            bool allowEnter,
            bool allowPaste,
            out bool textChanged,
            out bool submitted)
        {
            textChanged = false;
            submitted = false;

            string safeText = text ?? "";
            cursorIndex = Math.Clamp(cursorIndex, 0, safeText.Length);
            selectionAnchorIndex = Math.Clamp(selectionAnchorIndex, 0, safeText.Length);

            KeyboardState keyboardState = Keyboard.GetState();
            bool ctrlDown = keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);
            bool shiftDown = keyboardState.IsKeyDown(Keys.LeftShift) || keyboardState.IsKeyDown(Keys.RightShift);

            if (allowPaste && ctrlDown && key == Keys.V)
            {
                string pastedText = NormalizePastedText(GetClipboardTextSafely());
                if (pastedText.Length == 0)
                    return true;

                ApplyEditableTextInsertion(field, ref text, ref cursorIndex, ref selectionAnchorIndex, pastedText);
                textChanged = true;
                return true;
            }

            if (ctrlDown && key == Keys.A)
            {
                cursorIndex = safeText.Length;
                selectionAnchorIndex = 0;
                return true;
            }

            if (ctrlDown && key == Keys.C)
            {
                if (TryGetEditableTextSelection(safeText, cursorIndex, selectionAnchorIndex, out int selectionStart, out int selectionEnd))
                {
                    string selectedText = safeText.Substring(selectionStart, selectionEnd - selectionStart);
                    try
                    {
                        ClipboardService.SetText(selectedText);
                    }
                    catch
                    {
                        // Clipboard access can fail on some platforms or when unavailable.
                    }
                }

                return true;
            }

            if (ctrlDown && key == Keys.Z)
                return TryUndoTextInput(field);

            if (ctrlDown)
                return true;

            switch (key)
            {
                case Keys.Back:
                    textChanged = ApplyEditableTextDelete(field, ref text, ref cursorIndex, ref selectionAnchorIndex, deleteForward: false);
                    if (textChanged)
                        SetEditableTextFieldState(field, text, cursorIndex, selectionAnchorIndex, clearUndoHistory: false);
                    return true;

                case Keys.Delete:
                    textChanged = ApplyEditableTextDelete(field, ref text, ref cursorIndex, ref selectionAnchorIndex, deleteForward: true);
                    if (textChanged)
                        SetEditableTextFieldState(field, text, cursorIndex, selectionAnchorIndex, clearUndoHistory: false);
                    return true;

                case Keys.Left:
                    if (shiftDown)
                    {
                        if (cursorIndex == selectionAnchorIndex)
                            selectionAnchorIndex = cursorIndex;

                        if (cursorIndex > 0)
                            cursorIndex--;
                    }
                    else
                    {
                        if (cursorIndex > 0)
                            cursorIndex--;

                        selectionAnchorIndex = cursorIndex;
                    }

                    return true;

                case Keys.Right:
                    if (shiftDown)
                    {
                        if (cursorIndex == selectionAnchorIndex)
                            selectionAnchorIndex = cursorIndex;

                        if (cursorIndex < safeText.Length)
                            cursorIndex++;
                    }
                    else
                    {
                        if (cursorIndex < safeText.Length)
                            cursorIndex++;

                        selectionAnchorIndex = cursorIndex;
                    }

                    return true;

                case Keys.Home:
                    if (shiftDown)
                    {
                        if (cursorIndex == selectionAnchorIndex)
                            selectionAnchorIndex = cursorIndex;
                    }
                    else
                    {
                        cursorIndex = 0;
                        selectionAnchorIndex = cursorIndex;
                        return true;
                    }

                    cursorIndex = 0;
                    return true;

                case Keys.End:
                    if (shiftDown)
                    {
                        if (cursorIndex == selectionAnchorIndex)
                            selectionAnchorIndex = cursorIndex;
                    }
                    else
                    {
                        cursorIndex = safeText.Length;
                        selectionAnchorIndex = cursorIndex;
                        return true;
                    }

                    cursorIndex = safeText.Length;
                    return true;

                case Keys.Enter:
                    if (allowEnter)
                        submitted = true;

                    return true;
            }

            char input = GetCharFromKey(key, shiftDown);
            if (input == '\0')
                return false;

            ApplyEditableTextInsertion(field, ref text, ref cursorIndex, ref selectionAnchorIndex, input.ToString());
            textChanged = true;
            return true;
        }

        private static bool ShouldPlayTypingSound(Keys key, bool allowPaste)
        {
            if (allowPaste && key == Keys.V && IsControlDown())
                return false;

            return key != Keys.Back
                && key != Keys.Delete
                && key != Keys.Left
                && key != Keys.Right
                && key != Keys.Home
                && key != Keys.End
                && key != Keys.Enter;
        }

        private static bool IsControlDown()
        {
            KeyboardState keyboardState = Keyboard.GetState();
            return keyboardState.IsKeyDown(Keys.LeftControl) || keyboardState.IsKeyDown(Keys.RightControl);
        }

        private static string GetClipboardTextSafely()
        {
            try
            {
                return ClipboardService.GetText() ?? "";
            }
            catch
            {
                return "";
            }
        }

        private static string NormalizePastedText(string text)
        {
            return (text ?? "")
                .Replace("\r\n", " ")
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Replace('\t', ' ');
        }

        private void onPlayerSend()
        {
            if (string.IsNullOrWhiteSpace(selectedNpc))
                return;

            string playerMessage = (currentMessage ?? "").Trim();
            bool hasTextMessage = !string.IsNullOrWhiteSpace(playerMessage);
            List<string> selectedPhotoPaths = chatSelectedPhotos
                .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(ChatPhotoPickerMaxCount)
                .ToList();

            if (!hasTextMessage && selectedPhotoPaths.Count == 0)
                return;

            if (hasTextMessage)
                MessageManager.AddMessage(selectedNpc, $"PLAYER: {playerMessage}", isFromPlayer: true);

            if (selectedPhotoPaths.Count > 0)
            {
                string photoPayload = BuildPhotoPayload(selectedPhotoPaths);
                MessageManager.AddMessage(selectedNpc, $"{PlayerPhotoPrefix} {photoPayload}", isFromPlayer: true);

                string photoTag = BuildCombinedPhotoTagText(selectedPhotoPaths);
                if (!string.IsNullOrWhiteSpace(photoTag))
                    MessageManager.AddMessage(selectedNpc, $"{PlayerPhotoTagPrefix} {photoTag}", addCount: false, isFromPlayer: true);
            }

            IEnumerable<string>? extraContextLines = selectedPhotoPaths.Count > 0
                ? BuildPlayerPhotoPromptLines(selectedPhotoPaths)
                : null;

            messageHistory = MessageManager.GetMessages(selectedNpc);
            sendTextMessage(selectedNpc, hasTextMessage ? playerMessage : "", extraContextLines);
            ResetEditableTextFieldState(EditableTextFieldKind.Chat);
            chatSelectedPhotos.Clear();
            SnapChatScrollToBottom();
        }

        public static string sendTextMessage(string npcName, string userMessage, IEnumerable<string>? extraContextLines = null)
        {
            if (string.IsNullOrWhiteSpace(npcName))
                return "";

            string payload = BuildPendingMessagePayload(userMessage, extraContextLines);

            lock (replyQueueLock)
            {
                if (!pendingMessages.TryGetValue(npcName, out List<string>? queue))
                {
                    queue = new List<string>();
                    pendingMessages[npcName] = queue;
                }

                queue.Add(payload);
                lastInputActivityUtc[npcName] = DateTime.UtcNow;

                SchedulePendingReplyLocked(npcName);
            }

            return "";
        }

        private static string BuildPendingMessagePayload(string userMessage, IEnumerable<string>? extraContextLines)
        {
            var payloadBuilder = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(userMessage))
                payloadBuilder.Append(userMessage.Trim());

            if (extraContextLines != null)
            {
                foreach (string? rawLine in extraContextLines)
                {
                    string safeLine = (rawLine ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(safeLine))
                        continue;

                    if (payloadBuilder.Length > 0)
                        payloadBuilder.Append('\n');

                    payloadBuilder.Append(safeLine);
                }
            }

            if (payloadBuilder.Length == 0)
                payloadBuilder.Append("[Player sent image(s)]");

            return payloadBuilder.ToString();
        }

        public static void ClearPendingQueuedChatReplies()
        {
            lock (replyQueueLock)
            {
                foreach (CancellationTokenSource cts in replyTimers.Values)
                {
                    try
                    {
                        cts.Cancel();
                    }
                    catch
                    {
                        // ignore cancellation race during day transition
                    }

                    cts.Dispose();
                }

                replyTimers.Clear();
                pendingMessages.Clear();
                lastInputActivityUtc.Clear();
            }
        }

        
        private static void RegisterTextInputActivity(string npcName)
        {
            if (string.IsNullOrWhiteSpace(npcName))
                return;

            lock (replyQueueLock)
            {
                lastInputActivityUtc[npcName] = DateTime.UtcNow;

                if (pendingMessages.TryGetValue(npcName, out List<string>? queue) && queue.Count > 0)
                    SchedulePendingReplyLocked(npcName);
            }
        }

        private static void SchedulePendingReplyLocked(string npcName)
        {
            if (replyTimers.TryGetValue(npcName, out CancellationTokenSource? previousToken))
            {
                previousToken.Cancel();
                previousToken.Dispose();
            }

            var cts = new CancellationTokenSource();
            replyTimers[npcName] = cts;

            _ = WaitForReplyInactivity(npcName, cts);
        }

        private static async Task WaitForReplyInactivity(string npcName, CancellationTokenSource cts)
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    TimeSpan remainingDelay = GetRemainingReplyDelay(npcName);
                    if (remainingDelay > TimeSpan.Zero)
                        await Task.Delay(remainingDelay, cts.Token);

                    if (cts.IsCancellationRequested)
                        return;

                    if (GetRemainingReplyDelay(npcName) > TimeSpan.Zero)
                        continue;

                    await SendBatchMessage(npcName);
                    return;
                }
            }
            catch (TaskCanceledException)
            {
                // timer reset due to new activity
            }
            finally
            {
                lock (replyQueueLock)
                {
                    if (replyTimers.TryGetValue(npcName, out CancellationTokenSource? activeCts) && ReferenceEquals(activeCts, cts))
                        replyTimers.Remove(npcName);
                }

                cts.Dispose();
            }
        }

        private static TimeSpan GetRemainingReplyDelay(string npcName)
        {
            lock (replyQueueLock)
            {
                if (!lastInputActivityUtc.TryGetValue(npcName, out DateTime lastInputUtc))
                    return TimeSpan.Zero;

                TimeSpan elapsed = DateTime.UtcNow - lastInputUtc;
                return elapsed >= ReplyInactivityDelay
                    ? TimeSpan.Zero
                    : ReplyInactivityDelay - elapsed;
            }
        }

        // private static (string ReplyText, int PhotoCount) ExtractNpcPhotoDirective(string rawReply)
        // {
        //     if (string.IsNullOrWhiteSpace(rawReply))
        //         return ("", 0);

        //     int photoCount = 0;
        //     string cleanedReply = rawReply;
        //     int scanStart = 0;

        //     while (true)
        //     {
        //         int tokenStart = cleanedReply.IndexOf(NpcPhotoCommandPrefix, scanStart, StringComparison.OrdinalIgnoreCase);
        //         if (tokenStart < 0)
        //             break;

        //         int tokenEnd = cleanedReply.IndexOf(']', tokenStart);
        //         if (tokenEnd < 0)
        //             break;

        //         string token = cleanedReply.Substring(tokenStart, tokenEnd - tokenStart + 1);
        //         photoCount = Math.Max(photoCount, ParseNpcPhotoDirectiveCount(token));

        //         cleanedReply = cleanedReply.Remove(tokenStart, tokenEnd - tokenStart + 1);
        //         scanStart = tokenStart;
        //     }

        //     return (cleanedReply.Trim(), Math.Clamp(photoCount, 0, ChatPhotoPickerMaxCount));
        // }

        // private static int ParseNpcPhotoDirectiveCount(string token)
        // {
        //     if (string.IsNullOrWhiteSpace(token))
        //         return 0;

        //     int colonIndex = token.IndexOf(':');
        //     if (colonIndex < 0)
        //         return 1;

        //     int endIndex = token.IndexOf(']', colonIndex + 1);
        //     if (endIndex <= colonIndex)
        //         return 1;

        //     string countText = token.Substring(colonIndex + 1, endIndex - colonIndex - 1).Trim();
        //     if (!int.TryParse(countText, out int requestedCount))
        //         return 1;

        //     return Math.Clamp(requestedCount, 1, ChatPhotoPickerMaxCount);
        // }

        private static string ResolveChatImageTagFromPath(string imagePath)
        {
            string fileName = Path.GetFileName((imagePath ?? "").Trim());
            if (string.IsNullOrWhiteSpace(fileName) || ModEntry.ImageTags == null)
                return "";

            if (!ModEntry.ImageTags.TryGetValue(fileName, out string? rawTag) || string.IsNullOrWhiteSpace(rawTag))
                return "";

            return string.Join("; ", rawTag
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(tag => tag.Trim())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static List<string> SplitPhotoTagParts(string tagText)
        {
            return (tagText ?? "")
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(tag => tag.Trim())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string BuildCombinedPhotoTagText(IEnumerable<string> photoPaths)
        {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string photoPath in photoPaths ?? Enumerable.Empty<string>())
            {
                string rawTagText = ResolveChatImageTagFromPath(photoPath);
                foreach (string tagPart in SplitPhotoTagParts(rawTagText))
                    tags.Add(tagPart);
            }

            return string.Join("; ", tags);
        }

        private static string BuildPhotoPayload(IEnumerable<string> photoPaths)
        {
            return string.Join("||", (photoPaths ?? Enumerable.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static IEnumerable<string> BuildPlayerPhotoPromptLines(IEnumerable<string> photoPaths)
        {
            string combinedTags = BuildCombinedPhotoTagText(photoPaths);
            if (string.IsNullOrWhiteSpace(combinedTags))
                yield return "Attached photo tags: (none)";
            else
                yield return $"Attached photo tags: {combinedTags}";
        }

        private static async Task SendBatchMessage(string npcName)
        {
            await SendBatchMessage(npcName, consumeAiSlotNow: true);
        }

        private static async Task SendBatchMessage(string npcName, bool consumeAiSlotNow)
        {
            if (consumeAiSlotNow)
            {
                await ModEntry.RunAiActionWithQueueAsync(
                    () => SendBatchMessage(npcName, consumeAiSlotNow: false),
                    queueKey: $"chat:{npcName}",
                    highPriority: true);

                return;
            }

            List<string> messages;
            lock (replyQueueLock)
            {
                if (!pendingMessages.TryGetValue(npcName, out List<string>? queue) || queue.Count == 0)
                    return;

                messages = new List<string>(queue);
                queue.Clear();
            }

            string merged = string.Join("\n", messages.Where(text => !string.IsNullOrWhiteSpace(text)));
            int counter = messages.Count;
            string cleanedReply = await ModEntry.SendMessageToAssistant(npcName, merged, counter);

            if (!string.IsNullOrWhiteSpace(cleanedReply))
                MessageManager.AddMessage(npcName, $"{npcName}: {cleanedReply}");

            // if (requestedPhotoCount > 0)
            // {
            //     List<string> npcPhotos = ModEntry.CaptureNpcPhotosForMessage(npcName, requestedPhotoCount);
            //     List<string> validNpcPhotos = npcPhotos
            //         .Where(path => !string.IsNullOrWhiteSpace(path))
            //         .Distinct(StringComparer.OrdinalIgnoreCase)
            //         .Take(ChatPhotoPickerMaxCount)
            //         .ToList();

            //     if (validNpcPhotos.Count > 0)
            //     {
            //         string photoPayload = BuildPhotoPayload(validNpcPhotos);
            //         MessageManager.AddMessage(npcName, $"{NpcPhotoPrefix} {photoPayload}", addCount: false, isFromPlayer: false, notify: false);

            //         string imageTag = BuildCombinedPhotoTagText(validNpcPhotos);
            //         if (!string.IsNullOrWhiteSpace(imageTag))
            //             MessageManager.AddMessage(npcName, $"{NpcPhotoTagPrefix} {imageTag}", addCount: false, isFromPlayer: false, notify: false);
            //     }
            // }

            if (selectedNpc == npcName)
                messageHistory = MessageManager.GetMessages(npcName);
        }


        private List<ChatMessageEntry> BuildChatEntries(List<string> rawMessages)
        {
            var entries = new List<ChatMessageEntry>();
            if (rawMessages == null || rawMessages.Count == 0)
                return entries;

            var activeGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < rawMessages.Count; i++)
            {
                string rawMessage = rawMessages[i] ?? "";
                if (string.IsNullOrWhiteSpace(rawMessage))
                    continue;

                if (TryParseChatPhotoMessage(rawMessage, out bool isPlayerPhoto, out List<string> photoPaths))
                {
                    string photoTag = "";
                    if (i + 1 < rawMessages.Count && TryParseChatPhotoTagMessage(rawMessages[i + 1], isPlayerPhoto, out string parsedTag))
                    {
                        photoTag = NormalizePhotoTagText(parsedTag);
                        i++;
                    }

                    string groupId = BuildChatPhotoGroupId(isPlayerPhoto, photoPaths, i);
                    activeGroupIds.Add(groupId);

                    // Ensure index remains valid after loading/syncing history.
                    GetChatPhotoGroupIndex(groupId, photoPaths.Count);

                    entries.Add(new ChatMessageEntry
                    {
                        IsPlayer = isPlayerPhoto,
                        IsPhoto = true,
                        PhotoGroupId = groupId,
                        PhotoPaths = photoPaths,
                        PhotoTag = photoTag
                    });

                    continue;
                }

                if (TryParseChatPhotoTagMessage(rawMessage, expectedPlayerTag: null, out _))
                    continue;

                bool isSystem = rawMessage.StartsWith("SYSTEM: ", StringComparison.OrdinalIgnoreCase)
                    && rawMessage.EndsWith("---", StringComparison.Ordinal);
                bool isPlayer = rawMessage.StartsWith("PLAYER:", StringComparison.OrdinalIgnoreCase);
                string text = rawMessage.Contains(':')
                    ? rawMessage[(rawMessage.IndexOf(':') + 1)..].Trim()
                    : rawMessage.Trim();

                entries.Add(new ChatMessageEntry
                {
                    IsSystem = isSystem,
                    IsPlayer = isPlayer,
                    Text = text
                });
            }

            foreach (string staleGroupId in chatPhotoGroupIndices.Keys.Where(key => !activeGroupIds.Contains(key)).ToList())
                chatPhotoGroupIndices.Remove(staleGroupId);

            return entries;
        }

        private static bool TryParseChatPhotoMessage(string rawMessage, out bool isPlayerPhoto, out List<string> photoPaths)
        {
            string safeMessage = rawMessage ?? "";
            if (safeMessage.StartsWith(PlayerPhotoPrefix, StringComparison.OrdinalIgnoreCase))
            {
                isPlayerPhoto = true;
                photoPaths = safeMessage.Substring(PlayerPhotoPrefix.Length)
                    .Split("||", StringSplitOptions.RemoveEmptyEntries)
                    .Select(path => path.Trim())
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return photoPaths.Count > 0;
            }

            if (safeMessage.StartsWith(NpcPhotoPrefix, StringComparison.OrdinalIgnoreCase))
            {
                isPlayerPhoto = false;
                photoPaths = safeMessage.Substring(NpcPhotoPrefix.Length)
                    .Split("||", StringSplitOptions.RemoveEmptyEntries)
                    .Select(path => path.Trim())
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return photoPaths.Count > 0;
            }

            isPlayerPhoto = false;
            photoPaths = new List<string>();
            return false;
        }

        private static bool TryParseChatPhotoTagMessage(string rawMessage, bool? expectedPlayerTag, out string tagText)
        {
            string safeMessage = rawMessage ?? "";
            if (safeMessage.StartsWith(PlayerPhotoTagPrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (expectedPlayerTag.HasValue && !expectedPlayerTag.Value)
                {
                    tagText = "";
                    return false;
                }

                tagText = safeMessage.Substring(PlayerPhotoTagPrefix.Length).Trim();
                return true;
            }

            if (safeMessage.StartsWith(NpcPhotoTagPrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (expectedPlayerTag.HasValue && expectedPlayerTag.Value)
                {
                    tagText = "";
                    return false;
                }

                tagText = safeMessage.Substring(NpcPhotoTagPrefix.Length).Trim();
                return true;
            }

            tagText = "";
            return false;
        }

        private int CalculateChatEntryHeight(ChatMessageEntry entry, SpriteFont font, int lineHeight)
        {
            if (entry.IsPhoto)
            {
                Point drawSize = GetChatPhotoGroupDrawSize(entry);
                return drawSize.Y + 20;
            }

            List<string> wrappedLines = entry.IsSystem
                ? new List<string> { entry.Text }
                : SplitTextIntoLines(entry.Text, font, maxBubbleWidth);

            return Math.Max(1, wrappedLines.Count) * lineHeight + 10;
        }

        private int CalculateChatEntryWidth(ChatMessageEntry entry, SpriteFont font)
        {
            if (entry.IsPhoto)
            {
                Point drawSize = GetChatPhotoGroupDrawSize(entry);
                return drawSize.X + 20;
            }

            List<string> wrappedLines = entry.IsSystem
                ? new List<string> { entry.Text }
                : SplitTextIntoLines(entry.Text, font, maxBubbleWidth);

            int bubbleWidth = 120;
            foreach (string line in wrappedLines)
                bubbleWidth = Math.Max(bubbleWidth, (int)Math.Ceiling(font.MeasureString(line).X) + 20);

            return bubbleWidth;
        }

        private static string BuildChatPhotoGroupId(bool isPlayerPhoto, List<string> photoPaths, int messageIndex)
        {
            string owner = isPlayerPhoto ? "player" : "npc";
            return owner + ":" + messageIndex + ":" + string.Join("||", photoPaths.Select(path => (path ?? "").Trim()));
        }

        private static string NormalizePhotoTagText(string photoTag)
        {
            return string.Join("; ", SplitPhotoTagParts(photoTag));
        }

        private int GetChatPhotoGroupIndex(string groupId, int photoCount)
        {
            if (photoCount <= 0)
                return 0;

            if (!chatPhotoGroupIndices.TryGetValue(groupId, out int currentIndex))
                currentIndex = 0;

            currentIndex = Math.Clamp(currentIndex, 0, photoCount - 1);
            chatPhotoGroupIndices[groupId] = currentIndex;
            return currentIndex;
        }

        private string GetActiveChatPhotoPath(ChatMessageEntry entry)
        {
            if (entry.PhotoPaths == null || entry.PhotoPaths.Count == 0)
                return "";

            int index = GetChatPhotoGroupIndex(entry.PhotoGroupId, entry.PhotoPaths.Count);
            return entry.PhotoPaths[index];
        }

        private Point GetChatPhotoGroupDrawSize(ChatMessageEntry entry)
        {
            if (entry.PhotoPaths == null || entry.PhotoPaths.Count == 0)
                return new Point(220, 120);

            int maxWidth = 0;
            int maxHeight = 0;

            foreach (string photoPath in entry.PhotoPaths)
            {
                Point currentSize = GetChatPhotoDrawSize(photoPath);
                maxWidth = Math.Max(maxWidth, currentSize.X);
                maxHeight = Math.Max(maxHeight, currentSize.Y);
            }

            if (maxWidth <= 0 || maxHeight <= 0)
                return new Point(220, 120);

            return new Point(maxWidth, maxHeight);
        }

        private Rectangle GetScaledDrawBoundsInArea(Texture2D texture, Rectangle targetArea)
        {
            if (texture == null || targetArea.Width <= 0 || targetArea.Height <= 0)
                return targetArea;

            float widthScale = targetArea.Width / (float)Math.Max(1, texture.Width);
            float heightScale = targetArea.Height / (float)Math.Max(1, texture.Height);
            float scale = Math.Min(widthScale, heightScale);
            scale = Math.Clamp(scale, 0.01f, 100f);

            int drawWidth = Math.Max(1, (int)Math.Round(texture.Width * scale));
            int drawHeight = Math.Max(1, (int)Math.Round(texture.Height * scale));

            return new Rectangle(
                targetArea.X + (targetArea.Width - drawWidth) / 2,
                targetArea.Y + (targetArea.Height - drawHeight) / 2,
                drawWidth,
                drawHeight);
        }

        private Point GetChatPhotoDrawSize(string photoPath)
        {
            if (!TryGetChatImageTexture(photoPath, out Texture2D texture))
                return new Point(220, 120);

            float scaleX = ChatImageMaxWidth / (float)Math.Max(1, texture.Width);
            float scaleY = ChatImageMaxHeight / (float)Math.Max(1, texture.Height);
            float scale = Math.Min(ChatImageScale, Math.Min(scaleX, scaleY));
            scale = Math.Clamp(scale, 0.1f, 1f);

            return new Point(
                Math.Max(64, (int)Math.Round(texture.Width * scale)),
                Math.Max(64, (int)Math.Round(texture.Height * scale)));
        }

        private bool TryGetChatImageTexture(string imagePath, out Texture2D texture)
        {
            texture = null!;

            string resolvedPath = (imagePath ?? "").Trim();
            if (string.IsNullOrWhiteSpace(resolvedPath))
                return false;

            if (!Path.IsPathRooted(resolvedPath))
            {
                string playerPath = Path.Combine(GetCaptureFolderPath(PlayerPhotoFolderName), resolvedPath);
                string npcPath = Path.Combine(GetCaptureFolderPath(NpcPhotoFolderName), resolvedPath);

                if (File.Exists(playerPath))
                    resolvedPath = playerPath;
                else if (File.Exists(npcPath))
                    resolvedPath = npcPath;
            }

            if (chatImageCache.TryGetValue(resolvedPath, out Texture2D? cachedTexture) && cachedTexture != null)
            {
                texture = cachedTexture;
                return true;
            }

            if (chatFailedImagePaths.Contains(resolvedPath))
                return false;

            if (!File.Exists(resolvedPath))
            {
                chatFailedImagePaths.Add(resolvedPath);
                return false;
            }

            try
            {
                using FileStream stream = new FileStream(resolvedPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                Texture2D loadedTexture = Texture2D.FromStream(Game1.graphics.GraphicsDevice, stream);
                chatImageCache[resolvedPath] = loadedTexture;
                texture = loadedTexture;
                return true;
            }
            catch (Exception)
            {
                chatFailedImagePaths.Add(resolvedPath);
                return false;
            }
        }

        private void DrawChatImageTagTooltipIfHovered(SpriteBatch b, Rectangle viewport)
        {
            if (!(ModEntry.Config?.ShowMessageImageTags ?? true))
                return;

            int mouseX = Game1.getMouseX();
            int mouseY = Game1.getMouseY();

            foreach (ChatPhotoHoverEntry hoverEntry in chatPhotoHoverEntries)
            {
                if (string.IsNullOrWhiteSpace(hoverEntry.TagText))
                    continue;

                if (!IsPointOnVisibleBounds(hoverEntry.Bounds, mouseX, mouseY, viewport))
                    continue;

                DrawSocialTagTooltip(b, hoverEntry.TagText, mouseX, mouseY);
                return;
            }
        }

        private void DrawChatAttachmentButton(SpriteBatch b, Rectangle bounds, int selectedCount)
        {
            IClickableMenu.drawTextureBox(
                b,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                new Color(255, 255, 255, 220),
                1f,
                false);

            Rectangle iconBounds = new Rectangle(bounds.X + 8, bounds.Y + 6, Math.Max(16, bounds.Width - 16), Math.Max(16, bounds.Height - 12));
            b.Draw(textureAppPhoto, iconBounds, Color.White);

            if (selectedCount > 0)
            {
                string badgeText = Math.Min(ChatPhotoPickerMaxCount, selectedCount).ToString();
                Rectangle badgeBounds = new Rectangle(bounds.Right - 2, bounds.Y - 10, 24, 18);
                b.Draw(Game1.staminaRect, badgeBounds, new Color(215, 48, 48, 235));

                Vector2 badgeSize = Game1.smallFont.MeasureString(badgeText);
                Vector2 badgePos = new Vector2(
                    badgeBounds.X + (badgeBounds.Width - badgeSize.X) / 2f,
                    badgeBounds.Y + (badgeBounds.Height - badgeSize.Y) / 2f - 1f);
                b.DrawString(Game1.smallFont, badgeText, badgePos, Color.White);
            }
        }

        private void OpenChatPhotoPicker()
        {
            EnsureChatPhotoCandidatesLoaded();
            chatPhotoPickerOpen = true;
        }

        private void CloseChatPhotoPicker(bool clearSelection)
        {
            chatPhotoPickerOpen = false;
            chatPhotoPickerPrevBounds = Rectangle.Empty;
            chatPhotoPickerNextBounds = Rectangle.Empty;
            chatPhotoPickerToggleBounds = Rectangle.Empty;
            chatPhotoPickerCancelBounds = Rectangle.Empty;
            chatPhotoPickerSendBounds = Rectangle.Empty;

            if (clearSelection)
            {
                chatSelectedPhotos.Clear();
                chatPhotoCandidates.Clear();
                chatPhotoCandidateIndex = -1;
            }
        }

        private void EnsureChatPhotoCandidatesLoaded()
        {
            chatPhotoCandidates.Clear();

            string playerCaptureFolder = GetCaptureFolderPath(PlayerPhotoFolderName);

            if (Directory.Exists(playerCaptureFolder))
            {
                chatPhotoCandidates.AddRange(Directory.GetFiles(playerCaptureFolder, "*.png")
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(path => File.GetCreationTime(path)));
            }

            if (chatPhotoCandidates.Count == 0)
                chatPhotoCandidateIndex = -1;
            else
                chatPhotoCandidateIndex = Math.Clamp(chatPhotoCandidateIndex, 0, chatPhotoCandidates.Count - 1);

            chatSelectedPhotos.RemoveAll(selectedPath =>
                !chatPhotoCandidates.Any(candidate => string.Equals(candidate, selectedPath, StringComparison.OrdinalIgnoreCase)));
        }

        private void MoveChatPhotoCandidate(int delta)
        {
            if (chatPhotoCandidates.Count == 0)
            {
                chatPhotoCandidateIndex = -1;
                return;
            }

            chatPhotoCandidateIndex += delta;
            if (chatPhotoCandidateIndex < 0)
                chatPhotoCandidateIndex = chatPhotoCandidates.Count - 1;
            else if (chatPhotoCandidateIndex >= chatPhotoCandidates.Count)
                chatPhotoCandidateIndex = 0;
        }

        private void ToggleCurrentChatPhotoSelection()
        {
            if (chatPhotoCandidateIndex < 0 || chatPhotoCandidateIndex >= chatPhotoCandidates.Count)
                return;

            string candidatePath = chatPhotoCandidates[chatPhotoCandidateIndex];
            int existingIndex = chatSelectedPhotos.FindIndex(path => string.Equals(path, candidatePath, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                chatSelectedPhotos.RemoveAt(existingIndex);
                return;
            }

            if (chatSelectedPhotos.Count >= ChatPhotoPickerMaxCount)
                return;

            chatSelectedPhotos.Add(candidatePath);
        }

        private bool IsChatPhotoSelected(string imagePath)
        {
            return chatSelectedPhotos.Any(path => string.Equals(path, imagePath, StringComparison.OrdinalIgnoreCase));
        }

        private bool TryHandleChatPhotoNavigationClick(int x, int y)
        {
            foreach (ChatPhotoNavigationEntry navEntry in chatPhotoNavigationEntries)
            {
                if (navEntry.PhotoCount <= 1)
                    continue;

                if (navEntry.PreviousBounds.Contains(x, y))
                {
                    int currentIndex = GetChatPhotoGroupIndex(navEntry.GroupId, navEntry.PhotoCount);
                    int nextIndex = currentIndex - 1;
                    if (nextIndex < 0)
                        nextIndex = navEntry.PhotoCount - 1;

                    chatPhotoGroupIndices[navEntry.GroupId] = nextIndex;
                    Game1.playSound("shwip");
                    return true;
                }

                if (navEntry.NextBounds.Contains(x, y))
                {
                    int currentIndex = GetChatPhotoGroupIndex(navEntry.GroupId, navEntry.PhotoCount);
                    int nextIndex = (currentIndex + 1) % navEntry.PhotoCount;

                    chatPhotoGroupIndices[navEntry.GroupId] = nextIndex;
                    Game1.playSound("shwip");
                    return true;
                }
            }

            return false;
        }

        private void HandleChatPhotoPickerClick(int x, int y)
        {
            if (chatPhotoPickerPrevBounds.Contains(x, y))
            {
                MoveChatPhotoCandidate(-1);
                Game1.playSound("shwip");
                return;
            }

            if (chatPhotoPickerNextBounds.Contains(x, y))
            {
                MoveChatPhotoCandidate(1);
                Game1.playSound("shwip");
                return;
            }

            if (chatPhotoPickerToggleBounds.Contains(x, y))
            {
                ToggleCurrentChatPhotoSelection();
                Game1.playSound("smallSelect");
                return;
            }

            if (chatPhotoPickerCancelBounds.Contains(x, y))
            {
                CloseChatPhotoPicker(clearSelection: true);
                Game1.playSound("bigDeSelect");
                return;
            }

            if (chatPhotoPickerSendBounds.Contains(x, y))
            {
                bool confirmed = TryConfirmChatPhotoSelection();
                Game1.playSound(confirmed ? "smallSelect" : "cancel");

                if (confirmed)
                    CloseChatPhotoPicker(clearSelection: false);
            }
        }

        private bool TryConfirmChatPhotoSelection()
        {
            List<string> validPhotos = chatSelectedPhotos
                .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                .Take(ChatPhotoPickerMaxCount)
                .ToList();

            chatSelectedPhotos.Clear();
            chatSelectedPhotos.AddRange(validPhotos);
            return true;
        }

        private void DrawChatPhotoPickerMenu(SpriteBatch b)
        {
            b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height), Color.Black * 0.35f);

            Rectangle panelBounds = new Rectangle(xPositionOnScreen + 65, yPositionOnScreen + 180, 470, 600);
            IClickableMenu.drawTextureBox(
                b,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                panelBounds.X,
                panelBounds.Y,
                panelBounds.Width,
                panelBounds.Height,
                new Color(255, 255, 255, 240),
                1f,
                false);

            string title = $"Send photos ({chatSelectedPhotos.Count}/{ChatPhotoPickerMaxCount})";
            b.DrawString(Game1.dialogueFont, title, new Vector2(panelBounds.X + 20, panelBounds.Y + 14), Color.Black);

            chatPhotoPickerPrevBounds = Rectangle.Empty;
            chatPhotoPickerNextBounds = Rectangle.Empty;
            chatPhotoPickerToggleBounds = Rectangle.Empty;
            chatPhotoPickerCancelBounds = new Rectangle(panelBounds.Right - 190, panelBounds.Bottom - 80, 96, 48);
            chatPhotoPickerSendBounds = new Rectangle(panelBounds.Right - 84, panelBounds.Bottom - 86, 64, 64);

            Rectangle previewBounds = new Rectangle(panelBounds.X + 30, panelBounds.Y + 80, panelBounds.Width - 60, 330);
            IClickableMenu.drawTextureBox(
                b,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                previewBounds.X,
                previewBounds.Y,
                previewBounds.Width,
                previewBounds.Height,
                new Color(255, 255, 255, 220),
                1f,
                false);

            if (chatPhotoCandidates.Count == 0)
            {
                b.DrawString(Game1.smallFont, "No photos found.", new Vector2(previewBounds.X + 20, previewBounds.Y + 20), Color.Black);
            }
            else
            {
                chatPhotoCandidateIndex = Math.Clamp(chatPhotoCandidateIndex, 0, chatPhotoCandidates.Count - 1);
                string currentPath = chatPhotoCandidates[chatPhotoCandidateIndex];

                if (TryGetChatImageTexture(currentPath, out Texture2D previewTexture))
                {
                    float scale = Math.Min(
                        (previewBounds.Width - 20) / (float)Math.Max(1, previewTexture.Width),
                        (previewBounds.Height - 20) / (float)Math.Max(1, previewTexture.Height));
                    scale = Math.Clamp(scale, 0.1f, 1f);

                    int drawWidth = Math.Max(1, (int)Math.Round(previewTexture.Width * scale));
                    int drawHeight = Math.Max(1, (int)Math.Round(previewTexture.Height * scale));
                    Rectangle drawRect = new Rectangle(
                        previewBounds.X + (previewBounds.Width - drawWidth) / 2,
                        previewBounds.Y + (previewBounds.Height - drawHeight) / 2,
                        drawWidth,
                        drawHeight);

                    b.Draw(previewTexture, drawRect, Color.White);
                }
                else
                {
                    b.DrawString(Game1.smallFont, "Unable to load this image.", new Vector2(previewBounds.X + 20, previewBounds.Y + 20), Color.Black);
                }

                if (chatPhotoCandidates.Count > 1)
                {
                    chatPhotoPickerPrevBounds = new Rectangle(previewBounds.X + 8, previewBounds.Y + previewBounds.Height / 2 - 20, 40, 40);
                    chatPhotoPickerNextBounds = new Rectangle(previewBounds.Right - 48, previewBounds.Y + previewBounds.Height / 2 - 20, 40, 40);
                    DrawSocialImageNavButton(b, chatPhotoPickerPrevBounds, isNext: false);
                    DrawSocialImageNavButton(b, chatPhotoPickerNextBounds, isNext: true);
                }

                bool selected = IsChatPhotoSelected(currentPath);
                chatPhotoPickerToggleBounds = new Rectangle(panelBounds.X + 168, previewBounds.Bottom + 18, 132, 46);
                IClickableMenu.drawTextureBox(
                    b,
                    Game1.menuTexture,
                    new Rectangle(0, 256, 60, 60),
                    chatPhotoPickerToggleBounds.X,
                    chatPhotoPickerToggleBounds.Y,
                    chatPhotoPickerToggleBounds.Width,
                    chatPhotoPickerToggleBounds.Height,
                    selected ? new Color(200, 240, 200, 230) : new Color(255, 255, 255, 220),
                    1f,
                    false);

                string toggleLabel = selected ? "Selected" : "Select";
                Vector2 toggleSize = Game1.smallFont.MeasureString(toggleLabel);
                b.DrawString(
                    Game1.smallFont,
                    toggleLabel,
                    new Vector2(chatPhotoPickerToggleBounds.X + (chatPhotoPickerToggleBounds.Width - toggleSize.X) / 2f, chatPhotoPickerToggleBounds.Y + 10),
                    Color.Black);

                string fileName = Path.GetFileName(currentPath);
                string visibleFileName = GetTailTextToFit(fileName, Game1.smallFont, panelBounds.Width - 40);
                b.DrawString(Game1.smallFont, visibleFileName, new Vector2(panelBounds.X + 20, previewBounds.Bottom + 78), Color.Black);
            }

            IClickableMenu.drawTextureBox(
                b,
                Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                chatPhotoPickerCancelBounds.X,
                chatPhotoPickerCancelBounds.Y,
                chatPhotoPickerCancelBounds.Width,
                chatPhotoPickerCancelBounds.Height,
                new Color(255, 255, 255, 220),
                1f,
                false);

            string cancelText = "Cancel";
            Vector2 cancelSize = Game1.smallFont.MeasureString(cancelText);
            b.DrawString(
                Game1.smallFont,
                cancelText,
                new Vector2(
                    chatPhotoPickerCancelBounds.X + (chatPhotoPickerCancelBounds.Width - cancelSize.X) / 2f,
                    chatPhotoPickerCancelBounds.Y + (chatPhotoPickerCancelBounds.Height - cancelSize.Y) / 2f + 2f),
                Color.Black);

            var sendButton = new ClickableTextureComponent(
                chatPhotoPickerSendBounds,
                Game1.mouseCursors,
                new Rectangle(128, 256, 64, 64),
                1f);
            sendButton.draw(b);
        }

    }
}