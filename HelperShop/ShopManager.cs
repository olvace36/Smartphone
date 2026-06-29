using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Objects;
using StardewValley.GameData.Shops;

namespace Smartphone
{
    public class ShopManager
    {
        private readonly IModHelper helper;
        private readonly IMonitor monitor;

        // Unique identification strings for your item and shop listing
        public const string ItemId = "d5a1lamdtd.Smartphone.PhoneBook";
        public const string ShopTexturePath = "Mods/Smartphone/PhoneBookIcon";

        public ShopManager(IModHelper helper, IMonitor monitor)
        {
            this.helper = helper;
            this.monitor = monitor;
        }

        public void Initialize()
        {
            helper.Events.Content.AssetRequested += OnAssetRequested;
        }

        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            // 1. Create the Phone Book Item Definition
            if (e.NameWithoutLocale.IsEquivalentTo("Data/Objects"))
            {
                e.Edit(asset =>
                {
                    var objectDataDict = asset.AsDictionary<string, ObjectData>().Data;

                    if (!objectDataDict.ContainsKey(ItemId))
                    {
                        objectDataDict[ItemId] = new ObjectData
                        {
                            Name = "PhoneBook",
                            DisplayName = helper.Translation.Get("item.phonebook.name"),
                            Description = helper.Translation.Get("item.phonebook.description"),
                            Price = 0, // Sell price if player drops it in the shipping bin
                            Type = "Basic",
                            Category = StardewValley.Object.furnitureCategory,
                            Texture = ShopTexturePath,
                            SpriteIndex = 0
                        };
                    }
                });
            }

            // 2. Inject the Item into Robin's Shop (Data/Shops -> "Carpenter")
            if (e.NameWithoutLocale.IsEquivalentTo("Data/Shops"))
            {
                e.Edit(asset =>
                {
                    var shops = asset.AsDictionary<string, ShopData>().Data;

                    // Robin's internal shop ID is "Carpenter"
                    if (shops.TryGetValue("Carpenter", out var carpenterShop))
                    {
                        string shopEntryId = "d5a1lamdtd.Smartphone.PhoneBook_RobinStock";

                        // Guard against duplicate injections
                        if (!carpenterShop.Items.Exists(item => item.Id == shopEntryId))
                        {
                            carpenterShop.Items.Add(new ShopItemData
                            {
                                Id = shopEntryId,
                                ItemId = $"(O){ItemId}", // (O) qualifies it as an Object type in 1.6
                                Price = 5000,           // Customize the gold purchase cost here
                                AvailableStock = 1
                            });
                        }
                    }
                });
            }

            // 3. Dynamically generate the 16x16 icon from Cursors_1_6 at (7, 261, 13, 13)
            // 3. Dynamically generate the 16x16 icon from Cursors_1_6 at (7, 261, 13, 13)
            if (e.NameWithoutLocale.IsEquivalentTo(ShopTexturePath))
            {
                e.LoadFrom(() =>
                {
                    try
                    {
                        // Load the native 1.6 sheet containing your target sub-texture
                        Texture2D cursors = Game1.content.Load<Texture2D>("LooseSprites\\Cursors_1_6");

                        // Stardew Valley menu objects expect items to be mapped on a 16x16 pixel canvas
                        Texture2D canvasIcon = new Texture2D(Game1.graphics.GraphicsDevice, 16, 16);

                        // Extract the exact 13x13 pixel payload requested
                        Color[] extractedPixels = new Color[13 * 13];
                        cursors.GetData(0, new Rectangle(7, 261, 13, 13), extractedPixels, 0, extractedPixels.Length);

                        // Position and center the 13x13 texture cleanly into the 16x16 standard grid
                        Color[] completeCanvasPixels = new Color[16 * 16];
                        for (int y = 0; y < 13; y++)
                        {
                            for (int x = 0; x < 13; x++)
                            {
                                // Apply a 1-pixel X and Y padding offset to center the 13x13 asset smoothly
                                int canvasIndex = (y + 1) * 16 + (x + 1);
                                completeCanvasPixels[canvasIndex] = extractedPixels[y * 13 + x];
                            }
                        }

                        canvasIcon.SetData(completeCanvasPixels);
                        return canvasIcon;
                    }
                    catch (Exception ex)
                    {
                        monitor.Log($"Failed to dynamically construct PhoneBook texture from Cursors_1_6: {ex.Message}", LogLevel.Error);
                        // Return an empty 16x16 fall-back texture to prevent a hard game crash
                        return new Texture2D(Game1.graphics.GraphicsDevice, 16, 16);
                    }
                }, AssetLoadPriority.Exclusive); // Sets load priority rule explicitly
            }
        }
    }
}