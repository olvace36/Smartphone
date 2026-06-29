using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace Smartphone
{
    public partial class PhoneMenu
    {
        public static List<string> notificationHistory = new();
        private float notificationScrollOffset = 0f;
        private float notificationScrollTarget = 0f;
        private ClickableTextureComponent removeButton;
        private int readCountAtOpen = 0;

        private const int NotificationViewportYOffsetBase = 176;
        private const int NotificationViewportHeightBase = 800;
        private const int NotificationBubbleTextWrapWidthBase = 485;
        private const int NotificationBubbleXOffsetBase = 100;
        private const int NotificationBubbleTextLeftPaddingBase = 10;
        private const int NotificationBubbleTextTopPaddingBase = 15;
        private const int NotificationBubbleHorizontalPaddingBase = 20;
        private const int NotificationBubbleInnerPaddingBase = 10;
        private const int NotificationBubbleSpacingBase = 20;

        private int NotificationViewportYOffset => ScaleUiValue(NotificationViewportYOffsetBase);
        private int NotificationViewportHeight => Math.Max(1, ScaleUiValue(NotificationViewportHeightBase));
        private int NotificationBubbleTextWrapWidth => Math.Max(1, ScaleUiValue(NotificationBubbleTextWrapWidthBase));
        private int NotificationBubbleXOffset => ScaleUiValue(NotificationBubbleXOffsetBase);
        private int NotificationBubbleTextLeftPadding => Math.Max(1, ScaleUiValue(NotificationBubbleTextLeftPaddingBase));
        private int NotificationBubbleTextTopPadding => Math.Max(1, ScaleUiValue(NotificationBubbleTextTopPaddingBase));
        private int NotificationBubbleHorizontalPadding => Math.Max(1, ScaleUiValue(NotificationBubbleHorizontalPaddingBase));
        private int NotificationBubbleInnerPadding => Math.Max(1, ScaleUiValue(NotificationBubbleInnerPaddingBase));
        private int NotificationBubbleSpacing => Math.Max(1, ScaleUiValue(NotificationBubbleSpacingBase));

        private int NotificationCardPadding => ScaleUiValue(10);
        private int NotificationTextPaddingLeft => ScaleUiValue(15);
        private int NotificationTextPaddingRight => ScaleUiValue(15);
        private int NotificationTextPaddingTop => ScaleUiValue(15);
        private int NotificationTextPaddingBottom => ScaleUiValue(15);

        private int GetNotificationWrapWidthBase()
        {
            int bgWidth = texturePhoneBackground != null ? texturePhoneBackground.Width : 520;
            return bgWidth - 50;
        }

        private int GetNotificationCardHeight(bool hasTitle, int wrappedLinesCount, int titleLineHeight, int messageLineHeight)
        {
            int textHeight = (hasTitle ? titleLineHeight + ScaleUiValue(4) : 0) + Math.Max(1, wrappedLinesCount) * messageLineHeight;
            return textHeight + ScaleUiValue(30);
        }

        public void OpenNotification()
        {
            notificationHistory = NotificationManager.GetNotificationList();
            readCountAtOpen = Math.Max(0, notificationHistory.Count - NotificationManager.GetUnreadNotification());
            NotificationManager.ResetUnreadNotification();
            notificationScrollOffset = 0f;
            notificationScrollTarget = 0f;
        }

        private void DrawNotificationApp(SpriteBatch b)
        {
            if (removeButton == null)
            {
                removeButton = new ClickableTextureComponent(
                    Rectangle.Empty,
                    Game1.mouseCursors,
                    new Rectangle(564, 102, 16, 26),
                    ScaleUiValue(1.7f));
            }
            removeButton.scale = ScaleUiValue(1.7f);
            removeButton.bounds = new Rectangle(
                this.xPositionOnScreen + ScaleUiValue(385),
                this.yPositionOnScreen + ScaleUiValue(116),
                ScaleUiValue(32),
                ScaleUiValue(26 * 2));

            b.Draw(Game1.staminaRect, GetUiViewportBounds(), Color.Black * 0.6f);
            DrawPhoneScreenBackground(b, xOffset: 0);
            DrawPhoneFrame(b);
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
            backButton.draw(b, Color.Tan, 1f);
            lockButton.draw(b, Color.Tan, 1f);
            homeButton.draw(b, Color.Tan, 1f);

            b.End();
            Rectangle notificationClipRect = new Rectangle(xPositionOnScreen, yPositionOnScreen + NotificationViewportYOffset, width, NotificationViewportHeight);
            Game1.graphics.GraphicsDevice.ScissorRectangle = notificationClipRect;

            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState() { ScissorTestEnable = true });

            // Draw messages within clipped region
            List<string> notificationMessages = notificationHistory;

            int messageY = yPositionOnScreen + NotificationViewportYOffset - (int)MathF.Floor(notificationScrollOffset);
            SpriteFont font = Game1.smallFont;
            int visibleTop = notificationClipRect.Top - ScrollDrawOverscan;
            int visibleBottom = notificationClipRect.Bottom + ScrollDrawOverscan;

            Rectangle contentBounds = GetPhoneContentBounds();
            int cardX = contentBounds.X + NotificationCardPadding;
            int cardWidth = contentBounds.Width - 2 * NotificationCardPadding;
            int wrapWidthBase = GetNotificationWrapWidthBase();

            // Show newest notifications first and animate their pixel offset.
            for (int i = notificationMessages.Count - 1; i >= 0; i--)
            {
                string rawMsg = notificationMessages[i];
                string msg = rawMsg;
                string title = "";

                if (rawMsg.Contains("::"))
                {
                    var parts = rawMsg.Split(new[] { "::" }, 2, StringSplitOptions.None);
                    title = parts[0];
                    msg = parts[1];
                }

                int wrapWidth = (int)Math.Round(GetPhoneScaledWrapWidth(wrapWidthBase) / 0.75f);
                List<string> wrappedLines = SplitNotificationIntoLines(
                    msg,
                    font,
                    wrapWidth);

                int titleLineHeight = GetPhoneScaledLineHeight(font, 0.85f);
                int messageLineHeight = GetPhoneScaledLineHeight(font, 0.75f);
                int cardHeight = GetNotificationCardHeight(!string.IsNullOrEmpty(title), wrappedLines.Count, titleLineHeight, messageLineHeight);

                int bubbleTop = messageY;
                int bubbleBottom = messageY + cardHeight + NotificationBubbleSpacing;
                if (bubbleBottom < visibleTop)
                {
                    messageY += cardHeight + NotificationBubbleSpacing;
                    continue;
                }

                if (bubbleTop > visibleBottom)
                    break;

                bool isUnread = i >= readCountAtOpen;
                Color cardColor = isUnread ? new Color(160, 220, 160) : new Color(0, 0, 0, 100);
                Color textColor = isUnread ? new Color(30, 45, 30) : Color.White;

                Textures.DrawCard(
                    b,
                    cardX,
                    messageY,
                    cardWidth,
                    cardHeight,
                    cardColor,
                    1f,
                    false
                );

                int textY = messageY + NotificationTextPaddingTop;
                if (!string.IsNullOrEmpty(title))
                {
                    Vector2 titlePos = new Vector2(cardX + NotificationTextPaddingLeft, textY);
                    DrawPhoneText(b, font, title, titlePos, isUnread ? new Color(10, 25, 10) : Color.LightGray, 0.85f);
                    textY += titleLineHeight + ScaleUiValue(4);
                }

                foreach (var line in wrappedLines)
                {
                    Vector2 linePos = new Vector2(cardX + NotificationTextPaddingLeft, textY);
                    DrawPhoneText(b, font, line, linePos, textColor, 0.75f);
                    textY += messageLineHeight;
                }

                messageY += cardHeight + NotificationBubbleSpacing;
            }

            // Reset clipping
            b.End();
            b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
        }

        private void UpdateNotificationApp(GameTime time)
        {
            float lerpAmount = (float)(time.ElapsedGameTime.TotalSeconds * ChatScrollLerpSpeed);
            lerpAmount = Math.Clamp(lerpAmount, 0f, 1f);

            ClampNotificationScroll();
            notificationScrollOffset = MathHelper.Lerp(notificationScrollOffset, notificationScrollTarget, lerpAmount);

            if (Math.Abs(notificationScrollOffset - notificationScrollTarget) <= 0.5f)
                notificationScrollOffset = notificationScrollTarget;
        }

        private bool ReceiveLeftClickNotificationApp(int x, int y)
        {
            if (removeButton.containsPoint(x, y))
            {
                NotificationManager.ClearNotification();
                notificationHistory = NotificationManager.GetNotificationList();
                readCountAtOpen = 0;
                notificationScrollOffset = 0f;
                notificationScrollTarget = 0f;
                return true;
            }
            return false;
        }

        private void ReceiveScrollWheelActionNotificationApp(int direction)
        {
            float wheelSteps = direction / 120f;
            float maxScroll = CalculateNotificationScrollToBottomOffset(notificationHistory);
            notificationScrollTarget = Math.Clamp(
                notificationScrollTarget - wheelSteps * ChatScrollPixelsPerWheelNotch,
                0f,
                maxScroll);
            notificationScrollOffset = Math.Clamp(notificationScrollOffset, 0f, maxScroll);
        }

        private void ApplyTouchScrollDeltaNotificationApp(int pixelDelta)
        {
            float maxScroll = CalculateNotificationScrollToBottomOffset(notificationHistory);
            notificationScrollTarget = Math.Clamp(notificationScrollTarget + pixelDelta, 0f, maxScroll);
        }

        private int CalculateNotificationContentHeight(List<string> msgList)
        {
            if (msgList == null || msgList.Count == 0)
                return 0;

            SpriteFont font = Game1.smallFont;
            int titleLineHeight = GetPhoneScaledLineHeight(font, 0.85f);
            int messageLineHeight = GetPhoneScaledLineHeight(font, 0.75f);
            int totalHeight = 0;
            int wrapWidthBase = GetNotificationWrapWidthBase();

            foreach (string rawMsg in msgList)
            {
                string msg = rawMsg;
                string title = "";

                if (rawMsg.Contains("::"))
                {
                    var parts = rawMsg.Split(new[] { "::" }, 2, StringSplitOptions.None);
                    title = parts[0];
                    msg = parts[1];
                }

                int wrapWidth = (int)Math.Round(GetPhoneScaledWrapWidth(wrapWidthBase) / 0.75f);
                List<string> wrappedLines = SplitNotificationIntoLines(
                    msg,
                    font,
                    wrapWidth);
                int cardHeight = GetNotificationCardHeight(!string.IsNullOrEmpty(title), wrappedLines.Count, titleLineHeight, messageLineHeight);
                totalHeight += cardHeight + NotificationBubbleSpacing;
            }

            return totalHeight;
        }

        private float CalculateNotificationScrollToBottomOffset(List<string> msgList)
        {
            int contentHeight = CalculateNotificationContentHeight(msgList);
            return Math.Max(0f, contentHeight - NotificationViewportHeight);
        }

        private void ClampNotificationScroll()
        {
            ClampNotificationScroll(notificationHistory);
        }

        private void ClampNotificationScroll(List<string> msgList)
        {
            float maxScroll = CalculateNotificationScrollToBottomOffset(msgList);
            notificationScrollTarget = Math.Clamp(notificationScrollTarget, 0f, maxScroll);
            notificationScrollOffset = Math.Clamp(notificationScrollOffset, 0f, maxScroll);
        }

        private List<string> SplitNotificationIntoLines(string text, SpriteFont font, int maxWidth)
        {
            return GetWrappedLinesCached(text, font, maxWidth);
        }
    }
}
