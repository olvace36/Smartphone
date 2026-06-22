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

        private const int NotificationViewportYOffsetBase = 126;
        private const int NotificationViewportHeightBase = 800;
        private const int NotificationBubbleTextWrapWidthBase = 485;
        private const int NotificationBubbleXOffsetBase = 50;
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

        public void OpenNotification()
        {
            NotificationManager.ResetUnreadNotification();
            notificationHistory = NotificationManager.GetNotificationList();
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
            removeButton.bounds = new Rectangle(
                this.xPositionOnScreen + ScaleUiValue(335),
                this.yPositionOnScreen + ScaleUiValue(66),
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

            // Show newest notifications first and animate their pixel offset.
            for (int i = notificationMessages.Count - 1; i >= 0; i--)
            {
                string msg = notificationMessages[i];
                List<string> wrappedLines = SplitNotificationIntoLines(
                    msg,
                    font,
                    GetPhoneScaledWrapWidth(NotificationBubbleTextWrapWidth));

                int lineHeight = GetPhoneScaledLineHeight(font);
                int bubbleHeight = Math.Max(1, wrappedLines.Count) * lineHeight + NotificationBubbleInnerPadding;
                int bubbleWidth = 0;

                foreach (var line in wrappedLines)
                    bubbleWidth = Math.Max(bubbleWidth, (int)Math.Ceiling(MeasurePhoneText(font, line).X) + NotificationBubbleHorizontalPadding);

                Rectangle bubbleRect = new Rectangle(xPositionOnScreen + NotificationBubbleXOffset, messageY, bubbleWidth, bubbleHeight);

                int bubbleTop = bubbleRect.Y;
                int bubbleBottom = bubbleRect.Bottom + NotificationBubbleSpacing;
                if (bubbleBottom < visibleTop)
                {
                    messageY += bubbleHeight + NotificationBubbleSpacing;
                    continue;
                }

                if (bubbleTop > visibleBottom)
                    break;

                IClickableMenu.drawTextureBox(
                    b,
                    Game1.menuTexture,
                    new Rectangle(0, 256, 60, 60), // source rect for 9-slice
                    bubbleRect.X - ScaleUiValue(5),
                    bubbleRect.Y,
                    bubbleRect.Width + ScaleUiValue(12),
                    bubbleRect.Height + ScaleUiValue(10),
                    new Color(0, 0, 0, 100),
                    1f,
                    false
                );

                int textY = bubbleRect.Y + NotificationBubbleTextTopPadding;
                foreach (var line in wrappedLines)
                {
                    Vector2 linePos = new Vector2(bubbleRect.X + NotificationBubbleTextLeftPadding, textY);
                    DrawPhoneText(b, font, line, linePos, Color.White);
                    textY += lineHeight;
                }

                messageY += bubbleHeight + NotificationBubbleSpacing;
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
            int lineHeight = GetPhoneScaledLineHeight(font);
            int totalHeight = 0;

            foreach (string msg in msgList)
            {
                List<string> wrappedLines = SplitNotificationIntoLines(
                    msg,
                    font,
                    GetPhoneScaledWrapWidth(NotificationBubbleTextWrapWidth));
                int bubbleHeight = Math.Max(1, wrappedLines.Count) * lineHeight + NotificationBubbleInnerPadding;
                totalHeight += bubbleHeight + NotificationBubbleSpacing;
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
