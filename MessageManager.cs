using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using Smartphone;
using StardewModdingAPI.Utilities;

namespace Smartphone
{
    public static class MessageManager
    {
        public static Dictionary<string, List<string>> npcMessages = new();
        public static Dictionary<string, int> unreadCounts = new(); // new counter
        public static Dictionary<string, long> latestAdd = new(); // Unix timestamp per NPC
        public static List<string> favouriteNpc = new(); // List of favourite NPCs
        public static string currentPhoneSound = "getNewSpecialItem";
        public static string currentPhoneBackground = "";

        private static IModHelper Helper => ModEntry.SHelper;

        public static List<string> GetMessages(string npc)
        {
            if (!npcMessages.ContainsKey(npc))
                npcMessages[npc] = new List<string>();

            if (npcMessages.ContainsKey(npc) && ModEntry.npcMessagesToday.ContainsKey(npc))
            {
                return npcMessages[npc].Concat(ModEntry.npcMessagesToday[npc]).ToList(); 
            }
            return npcMessages[npc];
        }

        public static int GetUnreadCount(string npc)
        {
            return unreadCounts.TryGetValue(npc, out int count) ? count : 0;
        }
        public static void SetUnreadCount(string npc, int newCount = 0)
        {
            if (unreadCounts.ContainsKey(npc))
                unreadCounts[npc] = newCount;
        }

        public static void AddMessage(string npc, string message, bool addCount = true)
        {
            if (!npcMessages.ContainsKey(npc))
                npcMessages[npc] = new List<string>();
            //npcMessages[npc].Add(message);


            if (!ModEntry.npcMessagesToday.ContainsKey(npc))
            {
                ModEntry.npcMessagesToday[npc] = new List<string>();
                Game1.player.changeFriendship(10, Game1.getCharacterFromName(npc));
            }

            if (ModEntry.npcMessagesToday[npc].Count == 0)
                ModEntry.npcMessagesToday[npc].Add($"SYSTEM: ---{SDate.Now().DayOfWeek}, {SDate.Now().Season} {SDate.Now().Day:00}-Y{SDate.Now().Year}---");

            ModEntry.npcMessagesToday[npc].Add(message);

            if (addCount)
            {
                if (!unreadCounts.ContainsKey(npc))
                    unreadCounts[npc] = 0;
                unreadCounts[npc] = Math.Min(9, unreadCounts[npc] + 1);

                latestAdd[npc] = (long)Game1.MasterPlayer.millisecondsPlayed / 1000;
            }

            if (PhoneMenu.selectedNpc != null && PhoneMenu.currentApp == "appText")
            {
                PhoneMenu.messageHistory = GetMessages(PhoneMenu.selectedNpc);
            }
        }


        public static void ClearMessages(string npc)
        {
            //if (npcMessages.ContainsKey(npc))
            //    npcMessages[npc].Clear();

            if (ModEntry.npcMessagesToday.ContainsKey(npc))
                ModEntry.npcMessagesToday[npc].Clear();

            if (unreadCounts.ContainsKey(npc))
                unreadCounts[npc] = 0;
        }

        public static List<string> GetNpcsSortedByLatestMessage()
        {
            return latestAdd
                .OrderByDescending(kvp => kvp.Value)
                .Select(kvp => kvp.Key)
                .ToList();
        }

        public static Dictionary<string, long> GetLatestAddDictionary()
        {
            return latestAdd;
        }


        public static void SaveData()
        {
            // Create a merged dictionary
            var mergedMessages = new Dictionary<string, List<string>>();

            // Merge key-by-key
            foreach (var kvp in npcMessages)
            {
                var key = kvp.Key;
                var baseList = new List<string>(kvp.Value); // Clone the original list

                if (ModEntry.npcMessagesToday.TryGetValue(key, out var todaysList))
                {
                    baseList.AddRange(todaysList); // Append today's messages
                }

                mergedMessages[key] = baseList;
            }

            // Add any keys that exist only in npcMessagesToday
            foreach (var kvp in ModEntry.npcMessagesToday)
            {
                if (!mergedMessages.ContainsKey(kvp.Key))
                {
                    mergedMessages[kvp.Key] = new List<string>(kvp.Value);
                }
            }

            // Write the merged result
            Helper.Data.WriteJsonFile($"./userdata/{Constants.SaveFolderName}/npcMessages", mergedMessages);
            Helper.Data.WriteJsonFile($"./userdata/{Constants.SaveFolderName}/unreadCounts", unreadCounts);
            Helper.Data.WriteJsonFile($"./userdata/{Constants.SaveFolderName}/latestAdd", latestAdd);
            Helper.Data.WriteJsonFile($"./userdata/{Constants.SaveFolderName}/favouriteNpc", favouriteNpc);
            Helper.Data.WriteJsonFile($"./userdata/{Constants.SaveFolderName}/currentPhoneSound", currentPhoneSound);
            Helper.Data.WriteJsonFile($"./userdata/{Constants.SaveFolderName}/currentPhoneBackground", currentPhoneBackground);
        }

        public static void LoadData()
        {
            npcMessages = Helper.Data.ReadJsonFile<Dictionary<string, List<string>>>($"./userdata/{Constants.SaveFolderName}/npcMessages")
                          ?? new Dictionary<string, List<string>>();
            unreadCounts = Helper.Data.ReadJsonFile<Dictionary<string, int>>($"./userdata/{Constants.SaveFolderName}/unreadCounts")
                          ?? new Dictionary<string, int>();
            latestAdd = Helper.Data.ReadJsonFile<Dictionary<string, long>>($"./userdata/{Constants.SaveFolderName}/latestAdd")
                         ?? new Dictionary<string, long>();
            favouriteNpc = Helper.Data.ReadJsonFile<List<string>>($"./userdata/{Constants.SaveFolderName}/favouriteNpc")
                         ?? new List<string>();
            currentPhoneSound = Helper.Data.ReadJsonFile<string>($"./userdata/{Constants.SaveFolderName}/currentPhoneSound")
                        ?? "getNewSpecialItem";
            currentPhoneBackground = Helper.Data.ReadJsonFile<string>($"./userdata/{Constants.SaveFolderName}/currentPhoneBackground")
                        ?? "";

        }

    }
}
