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
        internal const string PlayerConversationPrefix = "@player:";
        internal const string PlayerPhotoMessagePrefix = "PlayerPhoto:";
        internal const string PlayerPhotoTagMessagePrefix = "PlayerPhotoTag:";
        internal const string NpcPhotoMessagePrefix = "NpcPhoto:";
        internal const string NpcPhotoTagMessagePrefix = "NpcPhotoTag:";

        private sealed class PhoneSettingData
        {
            public string CurrentPhoneBackground { get; set; } = "";
            public string CurrentPhoneSound { get; set; } = "getNewSpecialItem";
            public string CurrentPhoneTextColor { get; set; } = "Black";
            public string CurrentPhoneTheme { get; set; } = AssetHelper.DefaultPhoneThemeName;
            public string CurrentPlayerAvatar { get; set; } = "";
            public string CurrentPlayerBirthDate { get; set; } = "1";
            public string CurrentPlayerBirthSeason { get; set; } = "Spring";
            public string CurrentPlayerAge { get; set; } = "Adult";
            public string CurrentPlayerProfile { get; set; } = "";
            public List<string> FavouriteNpc { get; set; } = new();
            public Dictionary<string, long> LatestAdd { get; set; } = new();
            public Dictionary<string, int> UnreadCounts { get; set; } = new();
            public int UnreadNotification { get; set; }
            public int LastPhoneOpenedDay { get; set; }
        }

        private const string PhoneSettingFileName = "phoneSetting";

        public static Dictionary<string, List<string>> npcMessages = new();
        public static Dictionary<string, int> unreadCounts = new(); // new counter
        public static Dictionary<string, long> latestAdd = new(); // Unix timestamp per NPC
        public static List<string> favouriteNpc = new(); // List of favourite NPCs
        public static string currentPhoneSound = "getNewSpecialItem";
        public static string currentPhoneBackground = "";
        public static string currentPlayerAvatar = "";
        public static string currentPlayerBirthDate = "1";
        public static string currentPlayerBirthSeason = "Spring";
        public static string currentPlayerAge = "Adult";
        public static string currentPlayerProfile = "";
        public static string currentPhoneTextColor = "Black";
        public static string currentPhoneTheme = AssetHelper.DefaultPhoneThemeName;
        public static int lastPhoneOpenedDay = 0;

        private static IModHelper Helper => ModEntry.SHelper;

        public static bool IsPlayerBirthdayToday()
        {
            if (!Context.IsWorldReady)
                return false;

            string playerBirthSeason = NormalizePlayerBirthSeason(currentPlayerBirthSeason);
            string playerBirthDate = NormalizePlayerBirthDate(currentPlayerBirthDate);

            return string.Equals(playerBirthSeason, Game1.currentSeason, StringComparison.OrdinalIgnoreCase)
                && string.Equals(playerBirthDate, Game1.dayOfMonth.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNpcMessageable(string npc)
        {
            NPC? targetNpc = Game1.getCharacterFromName(npc, mustBeVillager: false);
            if (targetNpc == null)
                return false;

            if (!targetNpc.IsVillager || !targetNpc.CanSocialize || targetNpc.IsMonster)
                return false;

            string requirement = ModEntry.Config?.NpcMessageRequirement ?? ModConfig.NpcRequirementFriend;

            if (string.Equals(requirement, ModConfig.NpcRequirementMeet, StringComparison.OrdinalIgnoreCase))
                return Game1.player.friendshipData.ContainsKey(targetNpc.Name);

            return Game1.player.getFriendshipHeartLevelForNPC(targetNpc.Name) >= 1;
        }

        public static string BuildPlayerConversationKey(string playerName)
        {
            string normalizedPlayerName = (playerName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedPlayerName))
                return string.Empty;

            return PlayerConversationPrefix + normalizedPlayerName;
        }

        public static bool TryGetPlayerNameFromConversationKey(string conversationKey, out string playerName)
        {
            playerName = string.Empty;
            string normalizedKey = (conversationKey ?? string.Empty).Trim();

            if (!normalizedKey.StartsWith(PlayerConversationPrefix, StringComparison.OrdinalIgnoreCase))
                return false;

            string extractedName = normalizedKey.Substring(PlayerConversationPrefix.Length).Trim();
            if (string.IsNullOrWhiteSpace(extractedName))
                return false;

            playerName = extractedName;
            return true;
        }

        public static bool IsPlayerConversationKey(string conversationKey)
        {
            return TryGetPlayerNameFromConversationKey(conversationKey, out _);
        }

        public static string GetConversationDisplayName(string conversationKey)
        {
            if (TryGetPlayerNameFromConversationKey(conversationKey, out string playerName))
                return playerName;

            string normalizedNpcName = (conversationKey ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedNpcName))
                return string.Empty;

            NPC? targetNpc = Game1.getCharacterFromName(normalizedNpcName, mustBeVillager: false);
            if (targetNpc != null && !string.IsNullOrWhiteSpace(targetNpc.displayName))
                return targetNpc.displayName;

            return normalizedNpcName;
        }

        private static bool IsConversationMessageable(string conversationKey)
        {
            if (TryGetPlayerNameFromConversationKey(conversationKey, out string playerName))
            {
                string localPlayerName = (Game1.player?.Name ?? "Player").Trim();
                if (string.IsNullOrWhiteSpace(playerName)
                    || string.Equals(playerName, localPlayerName, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return true;
            }

            return IsNpcMessageable(conversationKey);
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
            if (npc != null && !npcMessages.ContainsKey(npc))
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

        public static void AddMessage(string npc, string message, bool addCount = true, bool isFromPlayer = false, bool notify = true, bool allowNonNpc = false)
        {
            if (!allowNonNpc && !IsConversationMessageable(npc))
                return;

            bool isPlayerConversation = IsPlayerConversationKey(npc);
            
            if (!npcMessages.ContainsKey(npc))
                npcMessages[npc] = new List<string>();
            //npcMessages[npc].Add(message);

            if (!isPlayerConversation)
            {
                if (!ModEntry.npcMessagesToday.ContainsKey(npc))
                {
                    ModEntry.npcMessagesToday[npc] = new List<string>();
                    Game1.player.changeFriendship(10, Game1.getCharacterFromName(npc));
                }

                if (ModEntry.npcMessagesToday[npc].Count == 0)
                    ModEntry.npcMessagesToday[npc].Add($"SYSTEM: ---{SDate.Now().DayOfWeek}, {SDate.Now().Season} {SDate.Now().Day:00}-Y{SDate.Now().Year}---");

                ModEntry.npcMessagesToday[npc].Add(message);
            }
            else
            {
                npcMessages[npc].Add(message);
                TrimMessageListToMax(npcMessages[npc], GetMaxMessagesPerNpc());
            }

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
                string npcDisplayName = GetConversationDisplayName(npc);
                if (string.IsNullOrWhiteSpace(npcDisplayName))
                    npcDisplayName = npc;

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

            if (IsPlayerConversationKey(npc) && npcMessages.ContainsKey(npc))
                npcMessages[npc].Clear();

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

        public static int GetCurrentPhoneUsageDayNumber()
        {
            if (!Context.IsWorldReady)
                return 0;

            SDate today = SDate.Now();
            int year = Math.Max(1, today.Year);
            int day = Math.Clamp(today.Day, 1, 28);
            int seasonIndex = ResolveSeasonIndex(today.Season);

            return ((year - 1) * 112) + (seasonIndex * 28) + day;
        }

        public static void MarkPhoneOpenedToday()
        {
            int currentDayNumber = GetCurrentPhoneUsageDayNumber();
            if (currentDayNumber <= 0)
                return;

            if (lastPhoneOpenedDay == currentDayNumber)
            {
                if (ModEntry.IsAiDisabledForPhoneInactivityToday)
                    ModEntry.IsAiDisabledForPhoneInactivityToday = false;

                return;
            }

            lastPhoneOpenedDay = currentDayNumber;
            ModEntry.IsAiDisabledForPhoneInactivityToday = false;
            SavePhoneSettingData();
        }

        private static int ResolveSeasonIndex(Season season)
        {
            return season switch
            {
                Season.Spring => 0,
                Season.Summer => 1,
                Season.Fall => 2,
                Season.Winter => 3,
                _ => 0
            };
        }

        private static string NormalizePlayerBirthDate(string? birthDate)
        {
            string digitsOnly = new string((birthDate ?? string.Empty).Where(char.IsDigit).ToArray());
            if (string.IsNullOrWhiteSpace(digitsOnly))
                return "1";

            if (!int.TryParse(digitsOnly, out int parsedValue))
                return "1";

            return Math.Clamp(parsedValue, 1, 28).ToString();
        }

        private static string NormalizePlayerBirthSeason(string? season)
        {
            string safeSeason = (season ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(safeSeason))
                return "Spring";

            return safeSeason.ToLowerInvariant() switch
            {
                "spring" => "Spring",
                "summer" => "Summer",
                "fall" => "Fall",
                "winter" => "Winter",
                _ => "Spring"
            };
        }

        private static string NormalizePlayerAge(string? age)
        {
            string safeAge = (age ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(safeAge) ? "Adult" : safeAge;
        }



        private static string GetPhoneSettingDataPath()
        {
            return ModEntry.GetSaveDataPath(PhoneSettingFileName);
        }

        private static void SavePhoneSettingData()
        {
            currentPlayerBirthDate = NormalizePlayerBirthDate(currentPlayerBirthDate);
            currentPlayerBirthSeason = NormalizePlayerBirthSeason(currentPlayerBirthSeason);
            currentPlayerAge = NormalizePlayerAge(currentPlayerAge);
            currentPlayerProfile = currentPlayerProfile ?? string.Empty;

            PhoneSettingData data = new()
            {
                CurrentPhoneBackground = currentPhoneBackground ?? "",
                CurrentPhoneSound = string.IsNullOrWhiteSpace(currentPhoneSound) ? "getNewSpecialItem" : currentPhoneSound,
                CurrentPhoneTextColor = string.IsNullOrWhiteSpace(currentPhoneTextColor) ? "Black" : currentPhoneTextColor,
                CurrentPhoneTheme = string.IsNullOrWhiteSpace(currentPhoneTheme) ? AssetHelper.DefaultPhoneThemeName : currentPhoneTheme,
                CurrentPlayerAvatar = currentPlayerAvatar ?? "",
                CurrentPlayerBirthDate = currentPlayerBirthDate,
                CurrentPlayerBirthSeason = currentPlayerBirthSeason,
                CurrentPlayerAge = currentPlayerAge,
                CurrentPlayerProfile = currentPlayerProfile,
                FavouriteNpc = favouriteNpc?.ToList() ?? new List<string>(),
                LatestAdd = latestAdd?.ToDictionary(pair => pair.Key, pair => pair.Value) ?? new Dictionary<string, long>(),
                UnreadCounts = unreadCounts?.ToDictionary(pair => pair.Key, pair => pair.Value) ?? new Dictionary<string, int>(),
                UnreadNotification = Math.Max(0, NotificationManager.unreadNotification),
                LastPhoneOpenedDay = Math.Max(0, lastPhoneOpenedDay)
            };

            Helper.Data.WriteJsonFile(GetPhoneSettingDataPath(), data);
        }

        private static void LoadPhoneSettingData()
        {
            PhoneSettingData data = Helper.Data.ReadJsonFile<PhoneSettingData>(GetPhoneSettingDataPath()) ?? new PhoneSettingData();

            unreadCounts = data.UnreadCounts ?? new Dictionary<string, int>();
            latestAdd = data.LatestAdd ?? new Dictionary<string, long>();
            favouriteNpc = data.FavouriteNpc ?? new List<string>();
            currentPhoneSound = string.IsNullOrWhiteSpace(data.CurrentPhoneSound) ? "getNewSpecialItem" : data.CurrentPhoneSound;
            currentPhoneBackground = data.CurrentPhoneBackground ?? "";
            currentPlayerAvatar = data.CurrentPlayerAvatar ?? "";
            if (currentPlayerAvatar.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                currentPlayerAvatar = System.IO.Path.ChangeExtension(currentPlayerAvatar, ".jpg");
            currentPlayerBirthDate = NormalizePlayerBirthDate(data.CurrentPlayerBirthDate);
            currentPlayerBirthSeason = NormalizePlayerBirthSeason(data.CurrentPlayerBirthSeason);
            currentPlayerAge = NormalizePlayerAge(data.CurrentPlayerAge);
            currentPlayerProfile = data.CurrentPlayerProfile ?? string.Empty;
            currentPhoneTextColor = string.IsNullOrWhiteSpace(data.CurrentPhoneTextColor) ? "Black" : data.CurrentPhoneTextColor;
            currentPhoneTheme = string.IsNullOrWhiteSpace(data.CurrentPhoneTheme) ? AssetHelper.DefaultPhoneThemeName : data.CurrentPhoneTheme;
            lastPhoneOpenedDay = Math.Max(0, data.LastPhoneOpenedDay);
            NotificationManager.unreadNotification = Math.Max(0, data.UnreadNotification);
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
            Helper.Data.WriteJsonFile(ModEntry.GetSaveDataPath("npcMessages"), mergedMessages);
            SavePhoneSettingData();
        }

        public static void LoadData()
        {
            npcMessages = Helper.Data.ReadJsonFile<Dictionary<string, List<string>>>(ModEntry.GetSaveDataPath("npcMessages"))
                          ?? new Dictionary<string, List<string>>();

            int maxMessages = GetMaxMessagesPerNpc();
            foreach (string npc in npcMessages.Keys.ToList())
                TrimMessageListToMax(npcMessages[npc], maxMessages);

            LoadPhoneSettingData();

        }

    }
}
