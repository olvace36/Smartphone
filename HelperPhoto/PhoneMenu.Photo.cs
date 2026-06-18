using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace Smartphone
{
    public partial class PhoneMenu
    {
        private void OpenPhotoApp()
        {
            ApplyPhoneBackground(MessageManager.currentPhoneBackground);

            string userCaptureFolderPath = GetCaptureFolderPath("photo_player");

            capturedImages = Directory.Exists(userCaptureFolderPath)
                ? Directory.GetFiles(userCaptureFolderPath)
                    .Where(p => p.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .ToList()
                : new List<string>();

            currentApp = "appPhoto";

            if (capturedImages.Count > 0)
            {
                currentImageIndex = 0;
                LoadImageAtIndex(currentImageIndex);
            }
            else
            {
                currentDisplayedImage?.Dispose();
                currentDisplayedImage = null;
                currentDisplayedImageIsSquare = false;
                currentImageIndex = -1;
            }
        }

        private void DrawPhotoApp(SpriteBatch b)
        {
            b.Draw(Game1.staminaRect, GetUiViewportBounds(), Color.Black * 0.6f);
            DrawPhoneScreenBackground(b, xOffset: 0);
            DrawPhoneFrame(b);
            backButton.draw(b, Color.Tan, 1f);
            lockButton.draw(b, Color.Tan, 1f);
            homeButton.draw(b, Color.Tan, 1f);

            if (currentDisplayedImage != null)
            {
                Rectangle photoContentBounds = GetPhoneContentBounds();
                b.Draw(currentDisplayedImage, photoContentBounds, Color.White);

                // Draw delete button
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

                if (currentImageIndex >= 0 && currentImageIndex < capturedImages.Count)
                {
                    string displayName = GetPhotoDisplayName(capturedImages[currentImageIndex]);

                    Vector2 namePos = new Vector2(photoContentBounds.X, photoContentBounds.Bottom - ScaleUiValue(50));
                    float photoNameViewportWidth = Math.Max(1f, photoContentBounds.Width - ScaleUiValue(8));
                    DrawLoopingPhotoName(b, displayName, namePos, photoNameViewportWidth);

                    // Draw heart (favourite) button
                    var rect = new Rectangle(218, 428, 7, 7);
                    if (IsSameFilePath(MessageManager.currentPhoneBackground, capturedImages[currentImageIndex]))
                        rect = new Rectangle(211, 428, 7, 7);

                    heartButton = new ClickableTextureComponent(
                        name: capturedImages[currentImageIndex],
                        bounds: new Rectangle(
                            xPositionOnScreen + ScaleUiValue(500),
                            yPositionOnScreen + ScaleUiValue(136),
                            ScaleUiValue(35),
                            ScaleUiValue(35)),
                        label: null,
                        hoverText: "",
                        texture: Game1.mouseCursors,
                        sourceRect: rect,
                        scale: ScaleUiValue(5f)
                    );
                    heartButton.draw(b);
                }
            }

            // Draw next/previous buttons
            photoNextButton.draw(b);
            photoPreviousButton.draw(b);
        }

        private void ReceiveLeftClickPhotoApp(int x, int y)
        {
            if (removeButton.containsPoint(x, y))
            {
                if (currentImageIndex >= 0 && currentImageIndex < capturedImages.Count)
                {
                    string fileToDelete = capturedImages[currentImageIndex];
                    string imageName = Path.GetFileName(fileToDelete);
                    bool deletedBackgroundImage = IsSameFilePath(MessageManager.currentPhoneBackground, fileToDelete);

                    // Remove file from disk
                    try
                    {
                        File.Delete(fileToDelete);
                    }
                    catch (Exception ex)
                    {
                        ModEntry.SMonitor.Log($"Failed to delete image: {ex.Message}", LogLevel.Warn);
                    }

                    // Remove from list
                    capturedImages.RemoveAt(currentImageIndex);
                    ModEntry.RemoveImageTags(imageName);

                    // Choose next image
                    if (capturedImages.Count == 0)
                    {
                        currentDisplayedImage?.Dispose();
                        currentDisplayedImage = null;
                        currentDisplayedImageIsSquare = false;
                        currentImageIndex = -1;
                    }
                    else if (currentImageIndex > 0)
                    {
                        currentImageIndex--; // Go to previous (left)
                        LoadImageAtIndex(currentImageIndex);
                    }
                    else
                    {
                        // Stay at index 0 if no left
                        LoadImageAtIndex(currentImageIndex);
                    }

                    if (deletedBackgroundImage)
                        ResetPhoneBackgroundToDefault();

                    Game1.playSound("trashcan");
                }
                return;
            }

            if (photoNextButton.containsPoint(x, y))
            {
                if (currentImageIndex > 0)
                {
                    currentImageIndex--;
                    LoadImageAtIndex(currentImageIndex);
                    Game1.playSound("shwip");
                }
                return;
            }

            if (photoPreviousButton.containsPoint(x, y))
            {
                if (currentImageIndex < capturedImages.Count - 1)
                {
                    currentImageIndex++;
                    LoadImageAtIndex(currentImageIndex);
                    Game1.playSound("shwip");
                }
                return;
            }

            if (heartButton != null
                && currentDisplayedImage != null
                && currentImageIndex >= 0
                && currentImageIndex < capturedImages.Count
                && heartButton.containsPoint(x, y))
            {
                if (IsSameFilePath(MessageManager.currentPhoneBackground, heartButton.name))
                    ResetPhoneBackgroundToDefault();
                else
                    ApplyPhoneBackground(heartButton.name);
            }
        }

        private string GetPhotoDisplayName(string photoPath)
        {
            string filename = Path.GetFileName(photoPath);
            if (ModEntry.ImageMetadataStore.TryGetValue(filename, out var metadata) && metadata != null)
            {
                string loc = string.IsNullOrWhiteSpace(metadata.Location) ? "Unknown Location" : metadata.Location;
                string ts = string.IsNullOrWhiteSpace(metadata.TimeString) ? "" : metadata.TimeString;
                if (!string.IsNullOrEmpty(ts))
                {
                    return $"{loc} ({ts})";
                }
                return loc;
            }

            // Fallback to filename parsing
            string rawName = Path.GetFileNameWithoutExtension(photoPath);
            int underscoreIndex = rawName.IndexOf('_');
            string displayName = underscoreIndex >= 0 ? rawName.Substring(0, underscoreIndex) : rawName;
            return displayName.Replace("-", " ");
        }
    }
}
