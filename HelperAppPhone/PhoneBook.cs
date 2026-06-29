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
        public static void CheckPhoneBookAction()
        {
            // Check if the player bought the item yesterday
            if (Game1.player.modData.TryGetValue("d5a1lamdtd.Smartphone.BoughtAllNumbers", out string bought) && bought == "true")
            {
                // 1. Ensure ModEntry.NpcNumbers is fully populated for the day
                if (ModEntry.NpcNumbers.Count == 0)
                {
                    Utility.ForEachVillager(npc =>
                    {
                        if (npc != null && npc.CanSocialize)
                        {
                            if (npc.modData.TryGetValue("d5a1lamdtd.Smartphone.PhoneNumber", out string number) && !string.IsNullOrWhiteSpace(number))
                            {
                                ModEntry.NpcNumbers[number] = npc.Name;
                            }
                        }
                        return true;
                    });
                }

                // 2. Load the existing contacts array from phone_app_data.json directly
                List<PhoneMenu.Contact> contactsList = new();
                List<PhoneMenu.RecentCall> recentsList = new();
                List<string> favoritesList = new();

                string saveName = ModEntry.GetActiveSaveFolderName();
                string folderPath = Path.Combine(ModEntry.SHelper.DirectoryPath, "userdata", saveName);
                string filePath = Path.Combine(folderPath, "phone_app_data.json");

                if (File.Exists(filePath))
                {
                    try
                    {
                        string json = File.ReadAllText(filePath);
                        var data = Newtonsoft.Json.Linq.JObject.Parse(json);
                        if (data["Contacts"] != null) contactsList = data["Contacts"].ToObject<List<PhoneMenu.Contact>>() ?? new();
                        if (data["RecentCalls"] != null) recentsList = data["RecentCalls"].ToObject<List<PhoneMenu.RecentCall>>() ?? new();
                        if (data["FavoriteNumbers"] != null) favoritesList = data["FavoriteNumbers"].ToObject<List<string>>() ?? new();
                    }
                    catch (Exception ex)
                    {
                        ModEntry.SMonitor.Log($"Error reading contacts JSON while executing phonebook unlock: {ex.Message}", LogLevel.Error);
                    }
                }

                // 3. Sync all available NPCs to the loaded contacts list and set their dialogue bypass flags
                bool changesMade = false;
                foreach (var pair in ModEntry.NpcNumbers)
                {
                    string number = pair.Key;
                    string npcInternalName = pair.Value;

                    // Add the contact if it does not exist already
                    if (!contactsList.Any(c => c.Number == number))
                    {
                        contactsList.Add(new PhoneMenu.Contact
                        {
                            Name = npcInternalName,
                            Number = number
                        });
                        changesMade = true;
                    }

                    // Set the mail flag so they don't prompt number-sharing dialogue
                    string phoneFlag = $"d5a1lamdtd.Smartphone_HasPhone_{npcInternalName}";
                    if (!Game1.player.mailReceived.Contains(phoneFlag))
                    {
                        Game1.player.mailReceived.Add(phoneFlag);
                    }
                }

                // 4. Save the synchronized contact changes back to disk
                if (changesMade)
                {
                    try
                    {
                        Directory.CreateDirectory(folderPath);
                        var data = new { RecentCalls = recentsList, Contacts = contactsList, FavoriteNumbers = favoritesList };
                        string json = Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);
                        File.WriteAllText(filePath, json);

                        if (ModEntry.phoneMenu != null)
                        {
                            PhoneMenu.phoneAppDataLoaded = false;
                        }

                        // Notify internal API events that contacts array updated
                        ModEntry.NotifyContactableNpcsChanged();
                    }
                    catch (Exception ex)
                    {
                        ModEntry.SMonitor.Log($"Error saving updated phonebook contacts list: {ex.Message}", LogLevel.Error);
                    }
                }

                // 6. Remove the flag from the player so it doesn't execute repeatedly on subsequent days
                Game1.player.modData.Remove("d5a1lamdtd.Smartphone.BoughtAllNumbers");

                // Send a native notification to the user's phone or HUD to show it completed successfully
                NotificationManager.AddNotification(
                    ModEntry.SHelper.Translation.Get("ui.phone.all_contacts_loaded_notification")
                );
            }
        }
    }
}
