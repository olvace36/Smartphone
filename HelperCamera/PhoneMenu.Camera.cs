using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace Smartphone
{
    public partial class PhoneMenu
    {
        // Camera fields
        private ClickableTextureComponent captureButton;
        private Rectangle cameraZoomOutButtonBounds = Rectangle.Empty;
        private Rectangle cameraZoomInButtonBounds = Rectangle.Empty;
        private Rectangle cameraFlashButtonBounds = Rectangle.Empty;
        private Rectangle cameraRotateButtonBounds = Rectangle.Empty;
        private Rectangle cameraSquareButtonBounds = Rectangle.Empty;
        private double cameraCaptureFlashRemainingSeconds = 0d;
        private int cameraZoomHoldDirection = 0;
        private double cameraZoomHoldElapsedSeconds = 0d;
        private bool cameraZoomHoldTriggered = false;

        // Camera constants
        private const float CameraZoomStep = 0.05f;
        private const float CameraZoomMin = 1f;
        private const float CameraZoomMax = 2f;
        private const double CameraCaptureFlashDurationSeconds = 0.5d;
        private const float CameraCaptureFlashMaxOpacity = 0.9f;
        private const int CameraOverlayMarginBase = 16;
        private const int CameraToolAreaHeightBase = 138;
        private const int CameraControlButtonHeightBase = 44;
        private const int CameraModeButtonWidthBase = 53;
        private const int CameraZoomButtonWidthBase = 44;
        private const int CameraControlButtonSpacingBase = 8;
        private const int CameraCaptureGroupGapBase = 14;
        private const int CameraCaptureButtonWidthBase = 118;
        private const int CameraCaptureButtonHeightBase = 64;
        private const int CameraCaptureButtonMinWidthBase = 80;
        private const int CameraCaptureButtonMinHeightBase = 44;
        private const double CameraZoomHoldInitialDelaySeconds = 0.24d;
        private const double CameraZoomHoldIntervalSeconds = 0.065d;
        private const string CameraFlashButtonLabel = "FLASH";
        private const string CameraLandscapeButtonLabel = "LAND";
        private const string CameraSquareButtonLabel = "SQR";
        private static readonly Rectangle CameraZoomMinusIconSource = new Rectangle(177, 345, 7, 8);
        private static readonly Rectangle CameraZoomPlusIconSource = new Rectangle(184, 345, 7, 8);
        private static readonly Rectangle CameraFlashIconSource = new Rectangle(193, 373, 9, 9);
        private static readonly Rectangle CameraSquareIconSource = new Rectangle(155, 384, 9, 9);
        private static readonly Rectangle CameraLandscapeIconSource = new Rectangle(67, 243, 9, 10);

        // Camera properties
        private int CameraOverlayMargin => Math.Max(1, ScaleUiValue(CameraOverlayMarginBase));
        private int CameraToolAreaHeight => Math.Max(1, ScaleUiValue(CameraToolAreaHeightBase));
        private int CameraControlButtonHeight => Math.Max(1, ScaleUiValue(CameraControlButtonHeightBase));
        private int CameraModeButtonWidth => Math.Max(1, ScaleUiValue(CameraModeButtonWidthBase));
        private int CameraZoomButtonWidth => Math.Max(1, ScaleUiValue(CameraZoomButtonWidthBase));
        private int CameraControlButtonSpacing => Math.Max(1, ScaleUiValue(CameraControlButtonSpacingBase));
        private int CameraCaptureGroupGap => Math.Max(1, ScaleUiValue(CameraCaptureGroupGapBase));
        private int CameraCaptureButtonWidth => Math.Max(1, ScaleUiValue(CameraCaptureButtonWidthBase));
        private int CameraCaptureButtonHeight => Math.Max(1, ScaleUiValue(CameraCaptureButtonHeightBase));
        private int CameraCaptureButtonMinWidth => Math.Max(1, ScaleUiValue(CameraCaptureButtonMinWidthBase));
        private int CameraCaptureButtonMinHeight => Math.Max(1, ScaleUiValue(CameraCaptureButtonMinHeightBase));

        private void DrawCameraApp(SpriteBatch b)
        {
            if (captureButton == null)
            {
                captureButton = new ClickableTextureComponent(
                    Rectangle.Empty,
                    Game1.mouseCursors2,
                    new Rectangle(72, 32, 18, 15),
                    ScaleUiValue(3.25f));
            }
            Rectangle phoneRect = ModEntry.GetPhoneCameraPreviewBounds(xPositionOnScreen, yPositionOnScreen);
            Rectangle captureRect = ModEntry.GetPlayerPhotoCaptureBounds(xPositionOnScreen, yPositionOnScreen);
            Rectangle toolAreaBounds = Rectangle.Empty;
            bool hideCameraOverlayButtons = ModEntry.IsPlayerCaptureCursorHidden();
            if (hideCameraOverlayButtons)
            {
                captureButton.bounds = Rectangle.Empty;
                cameraZoomOutButtonBounds = Rectangle.Empty;
                cameraZoomInButtonBounds = Rectangle.Empty;
                cameraFlashButtonBounds = Rectangle.Empty;
                cameraRotateButtonBounds = Rectangle.Empty;
                cameraSquareButtonBounds = Rectangle.Empty;
            }
            else
            {
                int minToolAreaHeight = Math.Max(CameraCaptureButtonMinHeight, CameraControlButtonHeight) + (CameraOverlayMargin * 2);
                int toolAreaHeight = Math.Clamp(CameraToolAreaHeight, minToolAreaHeight, Math.Max(minToolAreaHeight, phoneRect.Height));
                toolAreaBounds = new Rectangle(phoneRect.X, phoneRect.Bottom - toolAreaHeight, phoneRect.Width, toolAreaHeight);

                int maxCaptureWidth = Math.Max(CameraCaptureButtonMinWidth, toolAreaBounds.Width - (CameraOverlayMargin * 2));
                int maxCaptureHeight = Math.Max(CameraCaptureButtonMinHeight, toolAreaBounds.Height - (CameraOverlayMargin * 2));
                int captureWidth = Math.Clamp(CameraCaptureButtonWidth, CameraCaptureButtonMinWidth, maxCaptureWidth);
                int captureHeight = Math.Clamp(CameraCaptureButtonHeight, CameraCaptureButtonMinHeight, maxCaptureHeight);

                captureButton.bounds = new Rectangle(
                    phoneRect.Center.X - (captureWidth / 2),
                    toolAreaBounds.Center.Y - (captureHeight / 2),
                    captureWidth,
                    captureHeight);

                int controlY = toolAreaBounds.Center.Y - (CameraControlButtonHeight / 2);

                int leftSquareX = captureButton.bounds.Left - CameraCaptureGroupGap - CameraModeButtonWidth;
                int leftRotateX = leftSquareX - CameraControlButtonSpacing - CameraModeButtonWidth;
                int rightZoomOutX = captureButton.bounds.Right + CameraCaptureGroupGap;
                int rightZoomInX = rightZoomOutX + CameraZoomButtonWidth + CameraControlButtonSpacing;
                int rightFlashX = rightZoomInX + CameraZoomButtonWidth + CameraControlButtonSpacing;

                cameraRotateButtonBounds = new Rectangle(leftRotateX, controlY, CameraModeButtonWidth, CameraControlButtonHeight);
                cameraSquareButtonBounds = new Rectangle(leftSquareX, controlY, CameraModeButtonWidth, CameraControlButtonHeight);
                cameraZoomOutButtonBounds = new Rectangle(rightZoomOutX, controlY, CameraZoomButtonWidth, CameraControlButtonHeight);
                cameraZoomInButtonBounds = new Rectangle(rightZoomInX, controlY, CameraZoomButtonWidth, CameraControlButtonHeight);
                cameraFlashButtonBounds = new Rectangle(rightFlashX, controlY, CameraModeButtonWidth, CameraControlButtonHeight);
            }

            Rectangle uiViewportBounds = GetUiViewportBounds();
            int viewportWidth = uiViewportBounds.Width;
            int viewportHeight = uiViewportBounds.Height;

            int topShadeHeight = Math.Max(0, phoneRect.Top);
            int bottomShadeY = Math.Clamp(phoneRect.Bottom, 0, viewportHeight);
            int bottomShadeHeight = Math.Max(0, viewportHeight - bottomShadeY);
            int leftShadeWidth = Math.Max(0, phoneRect.Left);
            int rightShadeX = Math.Clamp(phoneRect.Right, 0, viewportWidth);
            int rightShadeWidth = Math.Max(0, viewportWidth - rightShadeX);
            int centerBandY = Math.Clamp(phoneRect.Top, 0, viewportHeight);
            int centerBandBottom = Math.Clamp(phoneRect.Bottom, 0, viewportHeight);
            int centerBandHeight = Math.Max(0, centerBandBottom - centerBandY);

            if (!hideCameraOverlayButtons)
            {
                if (topShadeHeight > 0)
                    b.Draw(Game1.staminaRect, new Rectangle(0, 0, viewportWidth, topShadeHeight), Color.Black * 0.4f);
                if (bottomShadeHeight > 0)
                    b.Draw(Game1.staminaRect, new Rectangle(0, bottomShadeY, viewportWidth, bottomShadeHeight), Color.Black * 0.4f);
                if (leftShadeWidth > 0 && centerBandHeight > 0)
                    b.Draw(Game1.staminaRect, new Rectangle(0, centerBandY, leftShadeWidth, centerBandHeight), Color.Black * 0.4f);
                if (rightShadeWidth > 0 && centerBandHeight > 0)
                    b.Draw(Game1.staminaRect, new Rectangle(rightShadeX, centerBandY, rightShadeWidth, centerBandHeight), Color.Black * 0.4f);

                Rectangle topShade = Rectangle.Intersect(
                    new Rectangle(phoneRect.X, phoneRect.Y, phoneRect.Width, Math.Max(0, captureRect.Y - phoneRect.Y)),
                    phoneRect);
                Rectangle bottomShade = Rectangle.Intersect(
                    new Rectangle(phoneRect.X, captureRect.Bottom, phoneRect.Width, Math.Max(0, phoneRect.Bottom - captureRect.Bottom)),
                    phoneRect);
                Rectangle leftCaptureShade = Rectangle.Intersect(
                    new Rectangle(phoneRect.X, captureRect.Y, Math.Max(0, captureRect.X - phoneRect.X), captureRect.Height),
                    phoneRect);
                Rectangle rightCaptureShade = Rectangle.Intersect(
                    new Rectangle(captureRect.Right, captureRect.Y, Math.Max(0, phoneRect.Right - captureRect.Right), captureRect.Height),
                    phoneRect);

                if (topShade.Width > 0 && topShade.Height > 0)
                    b.Draw(Game1.staminaRect, topShade, Color.Black * 0.35f);
                if (bottomShade.Width > 0 && bottomShade.Height > 0)
                    b.Draw(Game1.staminaRect, bottomShade, Color.Black * 0.35f);
                if (leftCaptureShade.Width > 0 && leftCaptureShade.Height > 0)
                    b.Draw(Game1.staminaRect, leftCaptureShade, Color.Black * 0.35f);
                if (rightCaptureShade.Width > 0 && rightCaptureShade.Height > 0)
                    b.Draw(Game1.staminaRect, rightCaptureShade, Color.Black * 0.35f);
            }

            if (!hideCameraOverlayButtons)
            {
                if (ModEntry.cameraLandscapeMode)
                {
                    Matrix landscapeTransform = CreateCameraLandscapeRotationMatrix();

                    b.End();
                    b.Begin(
                        SpriteSortMode.Deferred,
                        BlendState.AlphaBlend,
                        SamplerState.PointClamp,
                        null,
                        null,
                        null,
                        landscapeTransform);

                    DrawPhoneFrame(b);
                    backButton.draw(b, Color.Tan, 1f);
                    lockButton.draw(b, Color.Tan, 1f);
                    homeButton.draw(b, Color.Tan, 1f);

                    b.End();
                    b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
                }
                else
                {
                    DrawPhoneFrame(b);
                    backButton.draw(b, Color.Tan, 1f);
                    lockButton.draw(b, Color.Tan, 1f);
                    homeButton.draw(b, Color.Tan, 1f);
                }
            }

            if (!hideCameraOverlayButtons)
            {
                if (toolAreaBounds.Width > 0 && toolAreaBounds.Height > 0)
                    b.Draw(Game1.staminaRect, toolAreaBounds, new Color(0, 0, 0, 50));

                DrawCameraCaptureButton(b, captureButton.bounds);
                DrawCameraControlButton(b, cameraZoomOutButtonBounds, "-", false);
                DrawCameraControlButton(b, cameraZoomInButtonBounds, "+", false);
                DrawCameraControlButton(b, cameraFlashButtonBounds, CameraFlashButtonLabel, ModEntry.cameraFlashMode);
                DrawCameraControlButton(b, cameraRotateButtonBounds, CameraLandscapeButtonLabel, ModEntry.cameraLandscapeMode);
                DrawCameraControlButton(b, cameraSquareButtonBounds, CameraSquareButtonLabel, ModEntry.cameraSquareMode);
            }

            if (!hideCameraOverlayButtons)
            {
                DrawCaptureOutline(b, captureRect, new Color(255, 255, 255, 220));
            }

            DrawCameraCaptureFlash(b, phoneRect);
        }

        private void UpdateCameraApp(GameTime time)
        {
            UpdateCameraZoomHold(time);
            if (cameraCaptureFlashRemainingSeconds > 0d)
            {
                cameraCaptureFlashRemainingSeconds = Math.Max(0d, cameraCaptureFlashRemainingSeconds - time.ElapsedGameTime.TotalSeconds);
            }
        }

        private void ReceiveLeftClickCameraApp(int x, int y)
        {
            bool hideCameraOverlayButtons = ModEntry.IsPlayerCaptureCursorHidden();
            if (!hideCameraOverlayButtons)
            {
                if (cameraZoomOutButtonBounds.Contains(x, y))
                {
                    AdjustCameraZoom(-CameraZoomStep);
                    BeginCameraZoomHold(-1);
                    return;
                }

                if (cameraZoomInButtonBounds.Contains(x, y))
                {
                    AdjustCameraZoom(CameraZoomStep);
                    BeginCameraZoomHold(1);
                    return;
                }

                if (cameraFlashButtonBounds.Contains(x, y))
                {
                    ModEntry.cameraFlashMode = !ModEntry.cameraFlashMode;
                    Game1.playSound("smallSelect");
                    return;
                }

                if (cameraRotateButtonBounds.Contains(x, y))
                {
                    ModEntry.cameraLandscapeMode = !ModEntry.cameraLandscapeMode;
                    Game1.playSound("smallSelect");
                    return;
                }

                if (cameraSquareButtonBounds.Contains(x, y))
                {
                    ModEntry.cameraSquareMode = !ModEntry.cameraSquareMode;
                    Game1.playSound("smallSelect");
                    return;
                }
            }

            if (!hideCameraOverlayButtons && IsCameraCaptureButtonPressed(x, y))
            {
                ModEntry.QueuePlayerPhotoCapture(ModEntry.GetPlayerPhotoCaptureBounds(xPositionOnScreen, yPositionOnScreen));
                Game1.playSound("cameraNoise");
                TriggerCameraCaptureFlash();
                return;
            }
        }

        private bool HandleCameraAppBackButton()
        {
            currentApp = null;
            return true;
        }

        private void BeginCameraZoomHold(int direction)
        {
            cameraZoomHoldDirection = Math.Sign(direction);
            cameraZoomHoldElapsedSeconds = 0d;
            cameraZoomHoldTriggered = false;
        }

        private void ResetCameraZoomHoldState()
        {
            cameraZoomHoldDirection = 0;
            cameraZoomHoldElapsedSeconds = 0d;
            cameraZoomHoldTriggered = false;
        }

        private void UpdateCameraZoomHold(GameTime time)
        {
            if (cameraZoomHoldDirection == 0)
                return;

            bool hideCameraOverlayButtons = currentApp == "appCamera" && ModEntry.IsPlayerCaptureCursorHidden();
            if (currentApp != "appCamera" || hideCameraOverlayButtons)
            {
                ResetCameraZoomHoldState();
                return;
            }

            MouseState mouseState = Mouse.GetState();
            if (mouseState.LeftButton != ButtonState.Pressed)
            {
                ResetCameraZoomHoldState();
                return;
            }

            int mouseX = Game1.getMouseX();
            int mouseY = Game1.getMouseY();
            Rectangle zoomBounds = cameraZoomHoldDirection > 0
                ? cameraZoomInButtonBounds
                : cameraZoomOutButtonBounds;

            if (!zoomBounds.Contains(mouseX, mouseY))
            {
                ResetCameraZoomHoldState();
                return;
            }

            cameraZoomHoldElapsedSeconds += time.ElapsedGameTime.TotalSeconds;

            if (!cameraZoomHoldTriggered)
            {
                if (cameraZoomHoldElapsedSeconds < CameraZoomHoldInitialDelaySeconds)
                    return;

                cameraZoomHoldElapsedSeconds -= CameraZoomHoldInitialDelaySeconds;
                cameraZoomHoldTriggered = true;
            }

            while (cameraZoomHoldElapsedSeconds >= CameraZoomHoldIntervalSeconds)
            {
                cameraZoomHoldElapsedSeconds -= CameraZoomHoldIntervalSeconds;

                if (!AdjustCameraZoom(cameraZoomHoldDirection * CameraZoomStep, playSound: false))
                {
                    ResetCameraZoomHoldState();
                    return;
                }
            }
        }

        private bool AdjustCameraZoom(float delta, bool playSound = true)
        {
            float currentZoom = ModEntry.cameraZoomFactor;
            float targetZoom = Math.Clamp(currentZoom + delta, CameraZoomMin, CameraZoomMax);
            targetZoom = (float)Math.Round(targetZoom / CameraZoomStep) * CameraZoomStep;
            targetZoom = Math.Clamp(targetZoom, CameraZoomMin, CameraZoomMax);

            if (Math.Abs(targetZoom - currentZoom) <= 0.0001f)
            {
                if (playSound)
                    Game1.playSound("cancel");
                return false;
            }

            ModEntry.cameraZoomFactor = targetZoom;
            if (playSound)
                Game1.playSound("drumkit6");

            return true;
        }

        private void DrawCameraControlButton(SpriteBatch b, Rectangle bounds, string label, bool active)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            if (TryGetCameraControlIconSource(label, out Texture2D iconTexture, out Rectangle iconSource, out bool useDarkInactiveStyle, out float iconScaleFactor))
            {
                if (useDarkInactiveStyle)
                {
                    Color iconBoxColor = active
                        ? new Color(95, 145, 185, 110)
                        : new Color(20, 20, 20, 95);

                    Textures.DrawCard(
                        b,
                        bounds.X,
                        bounds.Y,
                        bounds.Width,
                        bounds.Height,
                        iconBoxColor,
                        1f,
                        false);
                }

                int baseIconSize = Math.Max(1, Math.Min(bounds.Width, bounds.Height) - (useDarkInactiveStyle ? 16 : 20));
                int iconSize = Math.Max(1, (int)Math.Round(baseIconSize * iconScaleFactor));

                Rectangle iconBounds = new Rectangle(
                    bounds.Center.X - (iconSize / 2),
                    bounds.Center.Y - (iconSize / 2),
                    iconSize,
                    iconSize);

                Color iconColor = useDarkInactiveStyle
                    ? (active ? new Color(255, 255, 255, 220) : new Color(48, 48, 48, 175))
                    : new Color(255, 255, 255, 180);

                Rectangle shadowBounds = new Rectangle(iconBounds.X + 1, iconBounds.Y + 2, iconBounds.Width, iconBounds.Height);
                b.Draw(iconTexture, shadowBounds, iconSource, new Color(0, 0, 0, 70));
                b.Draw(iconTexture, iconBounds, iconSource, iconColor);
                return;
            }

            Color boxColor = active
                ? new Color(120, 200, 255, 145)
                : new Color(25, 25, 25, 120);

            Textures.DrawCard(
                b,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                boxColor,
                1f,
                false);

            Vector2 textSize = MeasurePhoneText(Game1.smallFont, label);
            Vector2 textPosition = new Vector2(
                bounds.X + ((bounds.Width - textSize.X) / 2f),
                bounds.Y + ((bounds.Height - textSize.Y) / 2f) + 5f);

            DrawPhoneText(b, Game1.smallFont, label, textPosition, new Color(250, 250, 250, 235));
        }

        private static bool TryGetCameraControlIconSource(
            string label,
            out Texture2D texture,
            out Rectangle sourceRect,
            out bool useDarkInactiveStyle,
            out float iconScaleFactor)
        {
            if (string.Equals(label, "+", StringComparison.Ordinal))
            {
                texture = Game1.mouseCursors;
                sourceRect = CameraZoomPlusIconSource;
                useDarkInactiveStyle = false;
                iconScaleFactor = 1.5f;
                return true;
            }

            if (string.Equals(label, "-", StringComparison.Ordinal))
            {
                texture = Game1.mouseCursors;
                sourceRect = CameraZoomMinusIconSource;
                useDarkInactiveStyle = false;
                iconScaleFactor = 1.5f;
                return true;
            }

            if (string.Equals(label, CameraFlashButtonLabel, StringComparison.Ordinal))
            {
                texture = Game1.mouseCursors;
                sourceRect = CameraFlashIconSource;
                useDarkInactiveStyle = true;
                iconScaleFactor = 1.25f;
                return true;
            }

            if (string.Equals(label, CameraSquareButtonLabel, StringComparison.Ordinal))
            {
                texture = Game1.mouseCursors;
                sourceRect = CameraSquareIconSource;
                useDarkInactiveStyle = true;
                iconScaleFactor = 1.25f;
                return true;
            }

            if (string.Equals(label, CameraLandscapeButtonLabel, StringComparison.Ordinal))
            {
                texture = Game1.mouseCursors2;
                sourceRect = CameraLandscapeIconSource;
                useDarkInactiveStyle = true;
                // Slightly stretch the 9x10 icon into a near-square destination for visual consistency.
                iconScaleFactor = 1.22f;
                return true;
            }

            texture = Game1.mouseCursors;
            sourceRect = Rectangle.Empty;
            useDarkInactiveStyle = false;
            iconScaleFactor = 1f;
            return false;
        }

        private void DrawCameraCaptureButton(SpriteBatch b, Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            if (captureButton?.texture == null)
                return;

            Rectangle sourceRect = captureButton.sourceRect;
            float scaleX = bounds.Width / (float)Math.Max(1, sourceRect.Width);
            float scaleY = bounds.Height / (float)Math.Max(1, sourceRect.Height);
            float iconScale = Math.Min(scaleX, scaleY);

            int iconWidth = Math.Max(1, (int)Math.Round(sourceRect.Width * iconScale));
            int iconHeight = Math.Max(1, (int)Math.Round(sourceRect.Height * iconScale));

            Rectangle iconBounds = new Rectangle(
                bounds.X + ((bounds.Width - iconWidth) / 2),
                bounds.Y + ((bounds.Height - iconHeight) / 2),
                iconWidth,
                iconHeight);

            Rectangle shadowBounds = new Rectangle(iconBounds.X + 2, iconBounds.Y + 3, iconBounds.Width, iconBounds.Height);
            b.Draw(
                captureButton.texture,
                shadowBounds,
                sourceRect,
                new Color(0, 0, 0, 70),
                0f,
                Vector2.Zero,
                SpriteEffects.None,
                1f);

            b.Draw(
                captureButton.texture,
                iconBounds,
                sourceRect,
                Color.White * 0.92f,
                0f,
                Vector2.Zero,
                SpriteEffects.None,
                1f);
        }

        private void DrawCaptureOutline(SpriteBatch b, Rectangle bounds, Color color)
        {
            if (bounds.Width <= 1 || bounds.Height <= 1)
                return;

            b.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Y, bounds.Width, 2), color);
            b.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2), color);
            b.Draw(Game1.staminaRect, new Rectangle(bounds.X, bounds.Y, 2, bounds.Height), color);
            b.Draw(Game1.staminaRect, new Rectangle(bounds.Right - 2, bounds.Y, 2, bounds.Height), color);
        }

        private void TriggerCameraCaptureFlash()
        {
            cameraCaptureFlashRemainingSeconds = CameraCaptureFlashDurationSeconds;
        }

        private void DrawCameraCaptureFlash(SpriteBatch b, Rectangle cameraPreviewBounds)
        {
            if (ModEntry.takeScreenshot || cameraCaptureFlashRemainingSeconds <= 0d || CameraCaptureFlashDurationSeconds <= 0d)
                return;

            Rectangle flashBounds = Rectangle.Intersect(cameraPreviewBounds, GetUiViewportBounds());
            if (flashBounds.Width <= 0 || flashBounds.Height <= 0)
                return;

            float progress = (float)(cameraCaptureFlashRemainingSeconds / CameraCaptureFlashDurationSeconds);
            progress = Math.Clamp(progress, 0f, 1f);
            float opacity = CameraCaptureFlashMaxOpacity * progress * progress;
            if (opacity <= 0f)
                return;

            b.Draw(Game1.staminaRect, flashBounds, Color.White * opacity);
        }

        private bool IsCameraCaptureButtonPressed(int x, int y)
        {
            return captureButton?.bounds.Contains(x, y) == true;
        }

        public void HandleCameraAppKeyPress(Keys key)
        {
            if (key == Keys.Space || key == Keys.Enter)
            {
                bool hideCameraOverlayButtons = ModEntry.IsPlayerCaptureCursorHidden();
                if (!hideCameraOverlayButtons)
                {
                    ModEntry.QueuePlayerPhotoCapture(ModEntry.GetPlayerPhotoCaptureBounds(xPositionOnScreen, yPositionOnScreen));
                    Game1.playSound("cameraNoise");
                    TriggerCameraCaptureFlash();
                }
            }
        }
    }
}
