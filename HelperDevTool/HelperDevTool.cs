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
using System.Text.Json;
using Microsoft.Xna.Framework.Content;
using xTile.Dimensions;
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
                    spriteBatch.Draw(solidPixel, new Microsoft.Xna.Framework.Rectangle((int)screenPos.X, (int)screenPos.Y, tileSize, lineThickness), gridColor); 

                    // Left edge
                    spriteBatch.Draw(solidPixel, new Microsoft.Xna.Framework.Rectangle((int)screenPos.X, (int)screenPos.Y, lineThickness, tileSize), gridColor);

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

        private void LogOutNPCDialogue()
        {
            // 1. Core Mods
            SMonitor.Log("\n\n\nCALLINGGGGGGGGGGGGGG**********************************************************************************\n\n\n", LogLevel.Error);
            var vanillaNPCs = new HashSet<string> { "Abigail", "Alex", "Caroline", "Clint", "Demetrius", "Dwarf", "Elliott", "Emily", "Evelyn", "George", "Gus", "Haley", "Harvey", "Jas", "Jodi", "Kent", "Krobus", "Leah", "Leo", "Lewis", "Linus", "Marnie", "Maru", "Pam", "Penny", "Pierre", "Robin", "Sam", "Sandy", "Sebastian", "Shane", "Vincent", "Willy", "Wizard" };
            var sveNPCs = new HashSet<string> { "Sophia", "Victor", "Olivia", "Susan", "MarlonFay", "Andy", "Apples", "Lance", "Morgan", "Gunther", "MorrisTod", "Scarlett", "Claire", "Martin", "Alesia", "Isaac", "Camilla" };
            var rsvNPCs = new HashSet<string> { "Jeric", "Kenneth", "Kiwi", "Lenny", "Maddie", "Pika", "Shiro", "Ysabelle", "Alissa", "Anton", "Ariana", "Blair", "Carmen", "Corine", "Daia", "Ezekiel", "Faye", "Flor", "Freddie", "Ian", "Jio", "Keahi", "Kiarra", "Kimpo", "Kurane", "Lola", "Maive", "Malaya", "Olga", "Paula", "Philip", "Raeriyala", "Richard", "Sable", "Sean", "Trinnie", "Undine", "Yuuma" };

            // 2. Extracted Expansion Mods
            var sunberryNPCs = new HashSet<string> { "Aicha", "Amina", "Ari", "Blake", "Derya", "Diala", "Elias", "Ezra", "Iman", "Jumana", "Lyenne", "Maia", "Miyoung", "Moon", "Nadia", "Ophelia", "Pan", "Raccoon", "Reihana", "Silas", "Ripley", "Lani", "Wren", "Jonghyuk", "Spanner", "Sterling" };
            var vmvNPCs = new HashSet<string> { "Abdon", "Adelaide", "Aloise", "Aster", "Celestine", "Chandra", "Charline", "Chloe", "Claretie", "Djamel", "Felicity", "Gavin", "Helia", "Lotti", "Maddy", "Maelle", "Mariam", "Moira", "Naveen", "Odalis", "Priya", "Rayan" };
            var lunaNPCs = new HashSet<string> { "Bianka", "Dianna", "Esmeralda", "Lunna", "Raphael", "Salvador" };
            var eastScarpNPCs = new HashSet<string> { "Aideen", "Beatrice", "Cameron", "Corwin", "CorwinLK", "Dale", "Eyvinder", "Jacob", "Eloise", "Jade", "Jasper", "Jessie", "Juliet", "Kataryna", "Keanu", "Kennedy", "Lexi", "Oliver", "Josephine", "Rosa", "Tori", "ToriLK", "Tristan", "Vivienne", "VivienneLK", "Mateo", "Sen", "Hector", "Nora" };
            var zuzuNPCs = new HashSet<string> { "Bill", "Buddy", "Cal", "David", "Giovanni", "Gwen", "Hazel", "Kristoff", "Max", "Sadie", "Selena" };

            // 3. Local Routing Function
            string GetModFolderName(string npcName)
            {
                // Check custom mods first
                if (sunberryNPCs.Contains(npcName)) return "SunberryVillage";
                if (vmvNPCs.Contains(npcName)) return "VisitMountVapius";
                if (lunaNPCs.Contains(npcName)) return "LunaAstray";
                if (eastScarpNPCs.Contains(npcName)) return "EastScarp";
                if (zuzuNPCs.Contains(npcName)) return "DowntownZuzu";

                // Check core/large expansions
                if (sveNPCs.Contains(npcName)) return "SVE";
                if (rsvNPCs.Contains(npcName)) return "RSV";
                if (vanillaNPCs.Contains(npcName)) return "Vanilla";

                return "OtherCustomMods";
            }

            // 4. Execution Loop
            // 4. Execution Loop
            foreach (var npc in Utility.getAllVillagers().OfType<NPC>().Where(n => n.IsVillager))
            {
                string folderName = GetModFolderName(npc.Name);

                // Use local variables instead of an anonymous object to store the data temporarily
                Dictionary<string, string> stdDialogue = new Dictionary<string, string>();
                Dictionary<string, string> marDialogue = new Dictionary<string, string>();
                bool hasAnyDialogue = false;

                // Fetch Standard Dialogue
                try
                {
                    stdDialogue = SHelper.GameContent.Load<Dictionary<string, string>>($"Characters\\Dialogue\\{npc.Name}");
                    hasAnyDialogue = true;
                }
                catch (ContentLoadException)
                {
                    // Silently ignore if they don't have standard dialogue
                }

                // Fetch Marriage Dialogue
                try
                {
                    marDialogue = SHelper.GameContent.Load<Dictionary<string, string>>($"Characters\\Dialogue\\MarriageDialogue{npc.Name}");
                    hasAnyDialogue = true;
                }
                catch (ContentLoadException)
                {
                    // Silently ignore if they aren't marriageable
                }

                // Save into JSON file only if we actually found something
                if (hasAnyDialogue)
                {
                    // Create the anonymous object right here, using the populated dictionaries
                    var extractedData = new
                    {
                        StandardDialogue = stdDialogue,
                        MarriageDialogue = marDialogue
                    };

                    SHelper.Data.WriteJsonFile($"NpcDialogue/{folderName}/{npc.Name}.json", extractedData);
                }
            }
        }
    }
}