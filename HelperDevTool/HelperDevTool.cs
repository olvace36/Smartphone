using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Extensions;
using StardewValley.Menus;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Events;
namespace Smartphone
{
    public partial class ModEntry
    {
        private bool isGridVisible = false;
        private Texture2D solidPixel;
        private Color gridColor = Color.Black;

        // Variables to track our clicks and data
        private Vector2? firstClickTile = null;
        private Vector2 secondClickTile;
        private string tempAreaName = "";

        // This dictionary holds all our locations and areas





        public void ToggleGrid(ButtonPressedEventArgs e)
        {
            // Only capture clicks if the grid is visible, we are in a world, and no menu is open
            if (isGridVisible && Context.IsWorldReady && Game1.activeClickableMenu == null)
            {
                // Listen for Left Click
                if (e.Button == SButton.MouseLeft)
                {
                    if (firstClickTile == null)
                    {
                        // First Click
                        firstClickTile = e.Cursor.Tile;
                        Game1.playSound("dwop"); // Play a sound so you know it registered
                        Monitor.Log($"Start Tile captured: {firstClickTile.Value.X}, {firstClickTile.Value.Y}", LogLevel.Info);
                    }
                    else
                    {
                        // Second Click
                        secondClickTile = e.Cursor.Tile;
                        Game1.playSound("bigSelect");

                        var descriptionMenu = new StardewValley.Menus.NamingMenu(OnNameEntered, "Enter Area name:", "");
                        descriptionMenu.xPositionOnScreen = 10000;

                        // 2. Override the default character limit (set it to 1000, or however high you need)
                        descriptionMenu.textBox.textLimit = 10000;

                        // 3. (Optional) Make the text box visually wider so you can see more of your long description!
                        // The default is usually around 400 pixels.
                        descriptionMenu.textBox.Width = 8000;

                        // 4. Now show the menu to the player
                        Game1.activeClickableMenu = descriptionMenu;

                        // Open the first popup to get the Area Name
                        Game1.activeClickableMenu = descriptionMenu;
                    }
                }

                // Right click to cancel the current selection
                if (e.Button == SButton.MouseRight && firstClickTile != null)
                {
                    firstClickTile = null;
                    Game1.playSound("cancel");
                    Monitor.Log("Selection canceled.", LogLevel.Info);
                }
            }
        }

        // Put this class at the bottom of your file!
        public class AreaData
        {
            public int startX { get; set; }
            public int startY { get; set; }
            public int endX { get; set; }
            public int endY { get; set; }
            public string description { get; set; }
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public List<string>? ownerNpc { get; set; }
        }
        // Callback for when you press Enter on the Name menu
        private void OnNameEntered(string name)
        {
            if (name == ".")
            {
                firstClickTile = null;
                Game1.exitActiveMenu();
                return;
            }
            tempAreaName = name;

            // 1. Create the menu, but don't assign it to the screen just yet
            var descriptionMenu = new StardewValley.Menus.NamingMenu(OnDescriptionEntered, "Enter Area Description:", "");

            // 2. Override the default character limit (set it to 1000, or however high you need)
            descriptionMenu.textBox.textLimit = 10000;

            // 3. (Optional) Make the text box visually wider so you can see more of your long description!
            // The default is usually around 400 pixels.
            descriptionMenu.textBox.Width = 8000;

            // 4. Now show the menu to the player
            Game1.activeClickableMenu = descriptionMenu;
        }
        private void OnDescriptionEntered(string description)
        {
            if (description == ".")
            {

                firstClickTile = null;
                Game1.exitActiveMenu();
                Game1.playSound("money"); // Success sound!
            }
            string locationName = Game1.currentLocation.Name;

            // Make sure the location exists in our dictionary
            if (!areaTags.ContainsKey(locationName))
            {
                areaTags[locationName] = new Dictionary<string, AreaData>();
            }

            try
            {
                // Calculate start and end, ensuring start is always top-left and end is bottom-right 
                // (no matter which order you clicked the corners)
                int startX = (int)Math.Min(firstClickTile.Value.X, secondClickTile.X);
                int startY = (int)Math.Min(firstClickTile.Value.Y, secondClickTile.Y);
                int endX = (int)Math.Max(firstClickTile.Value.X, secondClickTile.X);
                int endY = (int)Math.Max(firstClickTile.Value.Y, secondClickTile.Y);

                // Create the new area data
                AreaData newArea = new AreaData
                {
                    startX = startX,
                    startY = startY,
                    endX = endX,
                    endY = endY,
                    description = description
                };

                // Add to dictionary and save to JSON
                areaTags[locationName][tempAreaName] = newArea;
                Helper.Data.WriteJsonFile("areas.json", areaTags);

                Monitor.Log($"Saved {tempAreaName} in {locationName} to areas.json!", LogLevel.Info);

                // Clean up and close menus
                firstClickTile = null;
                Game1.exitActiveMenu();
                Game1.playSound("money"); // Success sound!
            }
            catch
            {

            }
        }

        public void DrawGrid()
        {
            SpriteBatch spriteBatch = Game1.spriteBatch;
            int tileSize = Game1.tileSize; 
            xTile.Dimensions.Rectangle viewport = Game1.viewport;

            // 1. Prevent negative coordinates by using Math.Max(0, ...)
            int startX = Math.Max(0, viewport.X / tileSize);
            int endX = (viewport.X + viewport.Width) / tileSize + 1;
            int startY = Math.Max(0, viewport.Y / tileSize);
            int endY = (viewport.Y + viewport.Height) / tileSize + 1;

            // Variables for your new customizations
            int lineThickness = 3;
            float textSize = 1.0f; // Increased from 0.5f for bigger text
            gridColor = Color.DarkGreen;
            for (int x = startX; x <= endX; x++)
            {
                for (int y = startY; y <= endY; y++)
                {
                    Vector2 screenPos = new Vector2(
                        x * tileSize - viewport.X,
                        y * tileSize - viewport.Y
                    );

                    // 2. Make lines 4 times thicker by passing 'lineThickness' to the Rectangle
                    // Top edge
                    spriteBatch.Draw(solidPixel, new Rectangle((int)screenPos.X, (int)screenPos.Y, tileSize, lineThickness), gridColor); 

                    // Left edge
                    spriteBatch.Draw(solidPixel, new Rectangle((int)screenPos.X, (int)screenPos.Y, lineThickness, tileSize), gridColor);

                    // 3. Draw X coordinates on the Y0 row, with new formatting
                    if (y == 0)
                    {
                        spriteBatch.DrawString(Game1.dialogueFont, $"{x}", new Vector2(screenPos.X + 8, screenPos.Y + 8), Color.Yellow, 0f, Vector2.Zero, textSize, SpriteEffects.None, 0.99f);
                    }
                    
                    // 4. Draw Y coordinates on the X0 column, with new formatting
                    if (x == 0)
                    {
                        // Shifted the Y text down a bit (screenPos.Y + 36) so X-0 and Y-0 don't overlap each other in the top left corner!
                        spriteBatch.DrawString(Game1.dialogueFont, $"{y}", new Vector2(screenPos.X + 8, screenPos.Y + 36), Color.Yellow, 0f, Vector2.Zero, textSize, SpriteEffects.None, 0.99f);
                    }
                }
            }
        }
    }
}