using System;
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
        public static string currentPlayerAvatar = "";
        public static string currentPhoneTextColor = "Black";
        public static string currentPhoneTheme = AssetHelper.DefaultPhoneThemeName;

        private static IModHelper Helper => ModEntry.SHelper;

        private static bool IsNpcMessageable(string npc)
        {
            NPC? targetNpc = Game1.getCharacterFromName(npc, mustBeVillager: false);
            if (targetNpc == null)
                return false;

            if (!targetNpc.IsVillager || !targetNpc.CanSocialize || targetNpc.IsMonster)
                return false;

            string requirement = ModEntry.Config?.NpcMessageRequirement ?? ModConfig.NpcRequirementFriend;
            if (string.Equals(requirement, ModConfig.NpcRequirementNoRequirement, StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(requirement, ModConfig.NpcRequirementMeet, StringComparison.OrdinalIgnoreCase))
                return Game1.player.friendshipData.ContainsKey(targetNpc.Name);

            return Game1.player.getFriendshipHeartLevelForNPC(targetNpc.Name) >= 1;
        }

        private static int GetMaxMessagesPerNpc()
        {
            int configuredMax = ModEntry.Config?.MaxMessage ?? 1000;
            return Math.Max(1, configuredMax);
        }

        private static void TrimMessageListToMax(List<string> messages, int maxMessages)
        {
            if (messages == null || messages.Count <= maxMessages)
                return;

            int removeCount = messages.Count - maxMessages;
            messages.RemoveRange(0, removeCount);
        }

        private static void TrimMessagesForNpc(string npc)
        {
            int maxMessages = GetMaxMessagesPerNpc();

            npcMessages.TryGetValue(npc, out List<string>? historicalMessages);
            ModEntry.npcMessagesToday.TryGetValue(npc, out List<string>? todaysMessages);

            int totalCount = (historicalMessages?.Count ?? 0) + (todaysMessages?.Count ?? 0);
            int overflow = totalCount - maxMessages;
            if (overflow <= 0)
                return;

            if (historicalMessages != null && historicalMessages.Count > 0)
            {
                int removeFromHistorical = Math.Min(overflow, historicalMessages.Count);
                historicalMessages.RemoveRange(0, removeFromHistorical);
                overflow -= removeFromHistorical;
            }

            if (overflow > 0 && todaysMessages != null && todaysMessages.Count > 0)
            {
                int removeFromToday = Math.Min(overflow, todaysMessages.Count);
                todaysMessages.RemoveRange(0, removeFromToday);
            }
        }

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

        public static void AddMessage(string npc, string message, bool addCount = true, bool isFromPlayer = false, bool notify = true)
        {
            if (!IsNpcMessageable(npc))
                return;
            
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
            TrimMessagesForNpc(npc);

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

            if (!isFromPlayer && notify)
            {
                string npcDisplayName = npc;
                NPC? targetNpc = Game1.getCharacterFromName(npc, mustBeVillager: false);
                if (targetNpc != null && !string.IsNullOrWhiteSpace(targetNpc.displayName))
                    npcDisplayName = targetNpc.displayName;

                if (ModEntry.Config?.notifyMessage ?? true)
                    Game1.addHUDMessage(new HUDMessage($"A new message from {npcDisplayName}", HUDMessage.newQuest_type));

                DelayedAction.playSoundAfterDelay(MessageManager.currentPhoneSound, 0);
                DelayedAction.playSoundAfterDelay(MessageManager.currentPhoneSound, 1500);
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

            int maxMessages = GetMaxMessagesPerNpc();
            foreach (string npc in mergedMessages.Keys.ToList())
                TrimMessageListToMax(mergedMessages[npc], maxMessages);

            // Write the merged result
            Helper.Data.WriteJsonFile($"./userdata/{Constants.SaveFolderName}/npcMessages", mergedMessages);
            Helper.Data.WriteJsonFile($"./userdata/{Constants.SaveFolderName}/unreadCounts", unreadCounts);
            Helper.Data.WriteJsonFile($"./userdata/{Constants.SaveFolderName}/latestAdd", latestAdd);
            Helper.Data.WriteJsonFile($"./userdata/{Constants.SaveFolderName}/favouriteNpc", favouriteNpc);
            Helper.Data.WriteJsonFile($"./userdata/{Constants.SaveFolderName}/currentPhoneSound", currentPhoneSound);
            Helper.Data.WriteJsonFile($"./userdata/{Constants.SaveFolderName}/currentPhoneBackground", currentPhoneBackground);
            Helper.Data.WriteJsonFile($"./userdata/{Constants.SaveFolderName}/currentPlayerAvatar", currentPlayerAvatar);
            Helper.Data.WriteJsonFile($"./userdata/{Constants.SaveFolderName}/currentPhoneTextColor", currentPhoneTextColor);
            Helper.Data.WriteJsonFile($"./userdata/{Constants.SaveFolderName}/currentPhoneTheme", currentPhoneTheme);
        }

        public static void LoadData()
        {
            npcMessages = Helper.Data.ReadJsonFile<Dictionary<string, List<string>>>($"./userdata/{Constants.SaveFolderName}/npcMessages")
                          ?? new Dictionary<string, List<string>>();

            int maxMessages = GetMaxMessagesPerNpc();
            foreach (string npc in npcMessages.Keys.ToList())
                TrimMessageListToMax(npcMessages[npc], maxMessages);

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
            currentPlayerAvatar = Helper.Data.ReadJsonFile<string>($"./userdata/{Constants.SaveFolderName}/currentPlayerAvatar")
                        ?? "";
            currentPhoneTextColor = Helper.Data.ReadJsonFile<string>($"./userdata/{Constants.SaveFolderName}/currentPhoneTextColor")
                        ?? "Black";
            currentPhoneTheme = Helper.Data.ReadJsonFile<string>($"./userdata/{Constants.SaveFolderName}/currentPhoneTheme")
                        ?? AssetHelper.DefaultPhoneThemeName;

        }

    }
}
