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
                // Ensure UpdateNpcNumbers has populated ModEntry.NpcNumbers and local JSON is loaded
                UpdateNpcNumbers();

                // Load existing contacts
                List<PhoneMenu.Contact> contactsList = new();
                List<PhoneMenu.RecentCall> recentsList = new();
                List<string> favoritesList = new();
                List<PhoneMenu.NpcPhoneInfo> npcPhoneList = new();

                string saveName = ModEntry.GetActiveSaveFolderName();
                if (string.IsNullOrWhiteSpace(saveName)) return;

                string folderPath = Path.Combine(ModEntry.SHelper.DirectoryPath, "userdata", saveName);
                string filePath = Path.Combine(folderPath, "phone_app_data.json");

                if (File.Exists(filePath))
                {
                    try
                    {
                        string json = File.ReadAllText(filePath);
                        var data = Newtonsoft.Json.Linq.JObject.Parse(json);
                        if (data["Contacts"] != null) contactsList = data["Contacts"].ToObject<List<PhoneMenu.Contact>>() ?? new();
                        else if (data["CustomContacts"] != null) contactsList = data["CustomContacts"].ToObject<List<PhoneMenu.Contact>>() ?? new();
                        if (data["RecentCalls"] != null) recentsList = data["RecentCalls"].ToObject<List<PhoneMenu.RecentCall>>() ?? new();
                        if (data["FavoriteNumbers"] != null) favoritesList = data["FavoriteNumbers"].ToObject<List<string>>() ?? new();
                        if (data["NpcPhoneList"] != null) npcPhoneList = data["NpcPhoneList"].ToObject<List<PhoneMenu.NpcPhoneInfo>>() ?? new();
                    }
                    catch (Exception ex)
                    {
                        ModEntry.SMonitor.Log($"Error reading contacts JSON while executing phonebook unlock: {ex.Message}", LogLevel.Error);
                    }
                }

                bool changesMade = false;

                // Sync all NPCs in npcPhoneList to the contactsList and set their HasSharedNumber flag
                foreach (var npcInfo in npcPhoneList)
                {
                    string npcName = npcInfo.NpcName;
                    int requiredHearts = ModEntry.Config.FriendshipRequirement == "Friend" ? 250 : 1;
                    int currentHearts = Game1.player.getFriendshipLevelForNPC(npcName);

                    if (currentHearts >= requiredHearts)
                    {
                        // Add the contact if it does not exist already
                        if (!contactsList.Any(c => c.Number == npcInfo.PhoneNumber))
                        {
                            contactsList.Add(new PhoneMenu.Contact
                            {
                                Name = npcInfo.NpcName,
                                Number = npcInfo.PhoneNumber
                            });
                            changesMade = true;
                        }

                        // Set the HasSharedNumber flag to true
                        if (!npcInfo.HasSharedNumber)
                        {
                            npcInfo.HasSharedNumber = true;
                            changesMade = true;
                        }
                    }
                }

                if (changesMade)
                {
                    try
                    {
                        Directory.CreateDirectory(folderPath);
                        var data = new { RecentCalls = recentsList, Contacts = contactsList, FavoriteNumbers = favoritesList, NpcPhoneList = npcPhoneList };
                        string json = Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);
                        File.WriteAllText(filePath, json);

                        if (ModEntry.phoneMenu != null)
                        {
                            PhoneMenu.phoneAppDataLoaded = false;
                        }

                        ModEntry.NotifyContactableNpcsChanged();
                    }
                    catch (Exception ex)
                    {
                        ModEntry.SMonitor.Log($"Error saving updated phonebook contacts list: {ex.Message}", LogLevel.Error);
                    }
                }

                // Remove the flag so it doesn't execute repeatedly
                Game1.player.modData.Remove("d5a1lamdtd.Smartphone.BoughtAllNumbers");

                NotificationManager.AddNotification(
                    ModEntry.SHelper.Translation.Get("ui.phone.all_contacts_loaded_notification")
                );
            }
        }
    }
}
