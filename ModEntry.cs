
using System;
using System.Diagnostics.Metrics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Smartphone.Data;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.GameData.Characters;
using StardewValley.Menus;
using StardewValley.Objects;

namespace Smartphone
{
    /// <summary>The mod entry point.</summary>
    public class ModApi : ISmartPhoneApi
    {
        private readonly IMonitor monitor;

        public ModApi(IMonitor monitor)
        {
            this.monitor = monitor;
        }

        public List<string> GetPhoneNpcList(string playerId = "")
        {
            return ModEntry.GetPhoneNpcListByPlayerId(playerId);
        }

        public void SendSmartphoneMessageFromNPC(string npcName, string message, string playerId = "")
        {
            ModEntry.RouteSmartphoneMessageFromNpc(npcName, message, playerId);
        }

        public void SendSmartphoneMessageFromPlayer(string npcName, string message, string playerId = "")
        {
            ModEntry.RouteSmartphoneMessageFromPlayer(npcName, message, playerId);
        }

        public void SendSmartphoneNotification(string message, string notificationName = "", string playerId = "")
        {
            ModEntry.RouteSmartphoneNotification(message, notificationName, playerId);
        }


        public string? CreateStardewConnectPostFromNpc(string npcName, string postText, string attachedImageFile = "")
        {
            return StardewConnectManager.AddNpcPost(npcName, postText, attachedImageFile);
        }

        public bool AddStardewConnectCommentFromNpc(string postId, string npcName, string commentText)
        {
            return StardewConnectManager.AddNpcComment(postId, npcName, commentText);
        }

        public bool SetStardewConnectPostLikedFromNpc(string postId, string npcName, bool liked)
        {
            return StardewConnectManager.SetPostLike(postId, npcName, liked);
        }

        public bool RegisterUnlimitedEvent(
            string ownerModId,
            string eventType,
            Action<string> triggerEvent,
            int minimumHeartLevel = 0,
            string toolDescription = "")
        {
            return ModEntry.RegisterUnlimitedEventInternal(
                ownerModId,
                eventType,
                triggerEvent,
                minimumHeartLevel,
                toolDescription);
        }

        public bool UnregisterUnlimitedEvent(string ownerModId, string eventType)
        {
            return ModEntry.UnregisterUnlimitedEventInternal(ownerModId, eventType);
        }

        public bool RegisterPhoneApp(
            string ownerModId,
            string appId,
            string displayName,
            Texture2D iconTexture,
            Action onClick,
            bool closePhoneOnLaunch = true,
            int sortOrder = 0,
            Rectangle? sourceRect = null,
            Func<bool>? isVisible = null,
            Func<int>? getBadgeCount = null)
        {
            return ModEntry.RegisterPhoneAppInternal(
                ownerModId,
                appId,
                displayName,
                iconTexture,
                onClick,
                closePhoneOnLaunch,
                sortOrder,
                sourceRect,
                isVisible,
                getBadgeCount);
        }

        public bool UnregisterPhoneApp(string ownerModId, string appId)
        {
            return ModEntry.UnregisterPhoneAppInternal(ownerModId, appId);
        }

        public bool RegisterPhoneAppGroup(
            string ownerModId,
            string groupId,
            string displayName,
            Texture2D iconTexture,
            int sortOrder = 0,
            Rectangle? sourceRect = null,
            Func<bool>? isVisible = null,
            Func<int>? getBadgeCount = null)
        {
            return ModEntry.RegisterPhoneAppGroupInternal(
                ownerModId,
                groupId,
                displayName,
                iconTexture,
                sortOrder,
                sourceRect,
                isVisible,
                getBadgeCount);
        }

        public bool UnregisterPhoneAppGroup(string ownerModId, string groupId)
        {
            return ModEntry.UnregisterPhoneAppGroupInternal(ownerModId, groupId);
        }

        public bool RegisterPhoneAppGroupItem(
            string ownerModId,
            string groupId,
            string itemId,
            string displayName,
            Texture2D iconTexture,
            Action onClick,
            bool closePhoneOnLaunch = true,
            int sortOrder = 0,
            Rectangle? sourceRect = null,
            Func<bool>? isVisible = null,
            Func<int>? getBadgeCount = null)
        {
            return ModEntry.RegisterPhoneAppGroupItemInternal(
                ownerModId,
                groupId,
                itemId,
                displayName,
                iconTexture,
                onClick,
                closePhoneOnLaunch,
                sortOrder,
                sourceRect,
                isVisible,
                getBadgeCount);
        }

        public bool UnregisterPhoneAppGroupItem(string ownerModId, string groupId, string itemId)
        {
            return ModEntry.UnregisterPhoneAppGroupItemInternal(ownerModId, groupId, itemId);
        }

        public bool OpenPhoneHomeScreen()
        {
            return ModEntry.OpenPhoneHomeScreenInternal();
        }

        public bool OpenPhoneAppGroup(string ownerModId, string groupId)
        {
            return ModEntry.OpenPhoneAppGroupInternal(ownerModId, groupId);
        }

        public bool RegisterChatQuickActionButton(
            string ownerModId,
            string actionId,
            Texture2D iconTexture,
            Action<string> onClick,
            bool closePhoneOnLaunch = false,
            int sortOrder = 0,
            Rectangle? sourceRect = null,
            List<string>? npcNames = null)
        {
            return ModEntry.RegisterChatQuickActionButtonInternal(
                ownerModId,
                actionId,
                iconTexture,
                onClick,
                closePhoneOnLaunch,
                sortOrder,
                sourceRect,
                npcNames);
        }

        public bool UnregisterChatQuickActionButton(string ownerModId, string actionId)
        {
            return ModEntry.UnregisterChatQuickActionButtonInternal(ownerModId, actionId);
        }

    }

    public partial class ModEntry : Mod
    {


        public static ModEntry Instance;

        public static IMonitor SMonitor;
        public static IModHelper SHelper;
        public static IUnlimitedEventExpansionApi iUnlimitedEventExpansionApi;

        private ModApi apiInstance;

        public override object GetApi()
        {
            return this.apiInstance ??= new ModApi(Monitor);
        }

        public static ModConfig Config;

        public static JToken eventString;

        public static bool takeScreenshot = false;
        public static int currentMenuX;
        public static int currentMenuY;
        public static bool cameraLandscapeMode = false;
        public static bool cameraSquareMode = false;
        public static bool cameraFlashMode = false;
        public static float cameraZoomFactor = 1f;

        public static bool pendingInitNotification = false;
        public static bool pendingPhoneOsInitialization = false;
        public static bool hasNewVersionAvailable = false;

        public static Dictionary<string, GiftMemory> GiftMemories = new();
        public static List<RecentEvent> RecentEvents = new();
        public static bool isTodayEventAdded = false;
        public static Dictionary<(string season, int day), List<NPC>> NpcBirthdaysByDate = new();
        public static Dictionary<string, string> NpcCharacteristicsShort = new();
        public static Dictionary<string, string> NpcCharacteristicsMinimal = new();
        public static Dictionary<string, string> NpcCharacteristicsLong = new();

        public static int lastTimeReceiveMessage = 300;

        public static List<string> FarmCropNames = new();
        public static List<string> FarmTreeNames = new();

        public static Dictionary<string, List<string>> npcMessagesToday = new();
        public static Dictionary<string, string> npcConversationSummary = new();

        private static readonly object SaveFolderNameLock = new();
        private static string activeSaveFolderName = string.Empty;

        public static PhoneMenu phoneMenu;
        private Dictionary<string, Dictionary<string, AreaData>> areaTags;
        public static string GetActiveSaveFolderName()
        {
            lock (SaveFolderNameLock)
            {
                if (string.IsNullOrWhiteSpace(activeSaveFolderName))
                    activeSaveFolderName = ResolveSaveFolderNameFromContext();

                return activeSaveFolderName;
            }
        }

        public static string GetSaveDataPath(string fileName = "")
        {
            string normalizedFileName = (fileName ?? string.Empty)
                .Trim()
                .TrimStart('/', '\\');

            if (string.IsNullOrWhiteSpace(normalizedFileName))
                return $"./userdata/{GetActiveSaveFolderName()}";

            return $"./userdata/{GetActiveSaveFolderName()}/{normalizedFileName}";
        }

        public static void RefreshActiveSaveFolderName()
        {
            string resolved = ResolveSaveFolderNameFromContext();
            lock (SaveFolderNameLock)
                activeSaveFolderName = resolved;
        }

        public static void SetActiveSaveFolderName(string saveFolderName)
        {
            string normalizedSaveFolderName = NormalizeSaveFolderName(saveFolderName);
            if (string.IsNullOrWhiteSpace(normalizedSaveFolderName))
                return;

            lock (SaveFolderNameLock)
                activeSaveFolderName = normalizedSaveFolderName;
        }

        public static void ClearActiveSaveFolderName()
        {
            lock (SaveFolderNameLock)
                activeSaveFolderName = string.Empty;
        }

        private static string ResolveSaveFolderNameFromContext()
        {
            string constantsSaveFolder = NormalizeSaveFolderName(Constants.SaveFolderName);
            if (!string.IsNullOrWhiteSpace(constantsSaveFolder))
                return constantsSaveFolder;

            if (Context.IsWorldReady && Context.IsMultiplayer && Game1.MasterPlayer != null)
            {
                string masterPlayerName = NormalizeSaveFolderName(Game1.MasterPlayer.Name);
                long masterPlayerId = Game1.MasterPlayer.UniqueMultiplayerID;

                if (!string.IsNullOrWhiteSpace(masterPlayerName) && masterPlayerId > 0)
                    return $"{masterPlayerName}_{masterPlayerId}";

                if (masterPlayerId > 0)
                    return $"_{masterPlayerId}";
            }

            if (Context.IsWorldReady && Game1.player != null)
            {
                string playerName = NormalizeSaveFolderName(Game1.player.Name);
                long playerId = Game1.player.UniqueMultiplayerID;

                if (!string.IsNullOrWhiteSpace(playerName) && playerId > 0)
                    return $"{playerName}_{playerId}";

                if (playerId > 0)
                    return $"_{playerId}";
            }

            return "default";
        }

        private static string NormalizeSaveFolderName(string saveFolderName)
        {
            string normalizedValue = (saveFolderName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedValue))
                return string.Empty;

            char[] invalidChars = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(normalizedValue.Length);
            foreach (char character in normalizedValue)
            {
                if (character == '/'
                    || character == '\\'
                    || Array.IndexOf(invalidChars, character) >= 0)
                {
                    continue;
                }

                builder.Append(character);
            }

            return builder.ToString().Trim();
        }



        // =========================================================================================
        // =========================================================================================
        // =========================================================================================



        public static List<NPC> GetNpcsWithBirthdayToday()
        {
            int today = Game1.dayOfMonth;
            string season = Game1.currentSeason;

            return NpcBirthdaysByDate.TryGetValue((season, today), out var list)
                ? list
                : new List<NPC>();
        }


        public static void CheckSendNewMessage()
        {
            int timePassed = Game1.timeOfDay - lastTimeReceiveMessage;
            int baseChance = (timePassed - 100) / 50;
            baseChance = Math.Min(baseChance, 15);

            if (Game1.random.NextDouble() < baseChance / 100.0)
            {
                if (phoneMenu == null)
                    phoneMenu = new PhoneMenu();

                phoneMenu.UpdateNpcList(true);

                List<string> npcCandidates = phoneMenu.messageableNpcList
                    .Select(entry => entry.name)
                    .Where(name => !MessageManager.IsPlayerConversationKey(name))
                    .ToList();

                if (npcCandidates.Count == 0)
                    return;

                double power = 1.4;
                int maxValue = Math.Min(npcCandidates.Count, 20);
                if (maxValue < 1)
                    return;

                double rand = Game1.random.NextDouble();
                int result = (int)(Math.Pow(rand, power) * maxValue);

                int counter = 0;

                while (counter < 3)
                {
                    string npcName = npcCandidates[Math.Min(result + counter, maxValue - 1)];
                    NPC npc = Game1.getCharacterFromName(npcName, mustBeVillager: false);

                    if (npc == null)
                    {
                        counter++;
                        continue;
                    }

                    long currentTime = (long)Game1.player.millisecondsPlayed / 1000;
                    long lastTime = MessageManager.latestAdd.TryGetValue(npcName, out long time) ? time : 0;

                    if (currentTime - lastTime > 180 && !npcMessagesToday.ContainsKey(npcName))
                    {
                        bool talkedToToday = Game1.player.friendshipData.TryGetValue(npcName, out Friendship friendship)
                                             && friendship.TalkedToToday;

                        if (!talkedToToday && !Config.DisableDailyMessage)
                        {
                            // Mark as talked to
                            if (friendship != null)
                                friendship.TalkedToToday = true;


                            npc.checkForNewCurrentDialogue(Game1.player.getFriendshipHeartLevelForNPC(npcName));

                            if (npc.CurrentDialogue != null)
                            {
                                Task.Run(async () =>
                                {
                                    await PhoneDialogueRuntime.DeliverDialogueSequenceAsync(
                                        npcName,
                                        npc.CurrentDialogue,
                                        useRandomDelay: false,
                                        minDelayMs: 0,
                                        maxDelayMs: 1);

                                    npc.CurrentDialogue?.Clear();
                                });
                            }
                        }
                        else
                        {
                            if (iUnlimitedEventExpansionApi != null && Game1.timeOfDay < 1900 && Game1.random.NextDouble() < 0.3 && Game1.player.getFriendshipHeartLevelForNPC(npcName) >= 3 && iUnlimitedEventExpansionApi.CanScheduleNewEvent()) // 30% chance to trigger invite event if conditions are met
                            {
                                Task.Run(async () =>
                                {
                                    if (!TryConsumeAiCallSlot())
                                        return;

                                    string messages = await SendMessageToAssistant(npcName, type: "invite");
                                    if (!string.IsNullOrWhiteSpace(messages)
                                        && !messages.StartsWith("SYSTEM:", StringComparison.OrdinalIgnoreCase))
                                    {
                                        MessageManager.AddMessage(npcName, $"{npcName}:" + messages);
                                        lastTimeReceiveMessage = Game1.timeOfDay;
                                    }
                                });
                            }
                            else
                            {
                                Task.Run(async () =>
                                {
                                    if (!TryConsumeAiCallSlot())
                                        return;

                                    string messages = await SendMessageToAssistant(npcName, type: "text");
                                    if (!string.IsNullOrWhiteSpace(messages)
                                        && !messages.StartsWith("SYSTEM:", StringComparison.OrdinalIgnoreCase))
                                    {
                                        MessageManager.AddMessage(npcName, $"{npcName}:" + messages);
                                        lastTimeReceiveMessage = Game1.timeOfDay;
                                    }
                                });
                            }
                        }

                        break;
                    }

                    counter++;
                }
            }
        }


        public static void FirstDailyText(string npcName, string message)
        {
            NPC npc = Game1.getCharacterFromName(npcName);
            bool talkedToToday = Game1.player.friendshipData.TryGetValue(npcName, out Friendship friendship) && friendship.TalkedToToday;
            if (talkedToToday)
            {
                Task.Run(async () =>
                {
                    if (!TryConsumeAiCallSlot())
                        return;

                    string reply = await ModEntry.SendMessageToAssistant(npcName, text: message);
                    if (!string.IsNullOrWhiteSpace(reply)
                        && !reply.StartsWith("SYSTEM:", StringComparison.OrdinalIgnoreCase))
                        MessageManager.AddMessage(npcName, $"{npcName}:" + reply);
                    lastTimeReceiveMessage = Game1.timeOfDay;
                });
            }
            else
            {
                if (friendship != null)
                    friendship.TalkedToToday = true;

                npc.checkForNewCurrentDialogue(Game1.player.getFriendshipHeartLevelForNPC(npcName));

                if (npc.CurrentDialogue != null)
                {
                    Task.Run(async () =>
                    {
                        await PhoneDialogueRuntime.DeliverDialogueSequenceAsync(
                            npcName,
                            npc.CurrentDialogue,
                            useRandomDelay: true,
                            minDelayMs: 3000,
                            maxDelayMs: 5000);

                        npc.CurrentDialogue?.Clear();
                    });
                }
            }

        }



        public static void CheckCurrentEvent()
        {
            if (Game1.currentSeason == "spring" && Game1.dayOfMonth == 24 && Game1.player.dancePartner.TryGetVillager() != null && !isTodayEventAdded)
            {
                RecentEvents.Add(new RecentEvent
                {
                    Description = $"Player and {Game1.player.dancePartner.TryGetVillager().Name} danced together at the Flower Dance",
                    DaysRemaining = 7
                });
                isTodayEventAdded = true;
            }
            else if (Game1.CurrentEvent != null && Game1.CurrentEvent.isFestival && !isTodayEventAdded && !(Game1.currentSeason == "spring" && Game1.dayOfMonth == 24))
            {
                string festivalId = Game1.CurrentEvent.FestivalName;
                RecentEvents.Add(new RecentEvent
                {
                    Description = $"Player joined {festivalId} event with everyone in the town.",
                    DaysRemaining = 5
                });
                isTodayEventAdded = true;
            }
        }
    }
}