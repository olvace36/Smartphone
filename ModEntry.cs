
using System;
using System.Diagnostics.Metrics;
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

        public List<string> GetPhoneNpcList()
        {
            // Ensure phoneMenu exists before accessing it (lazy init)
            if (ModEntry.phoneMenu == null)
                ModEntry.phoneMenu = new PhoneMenu();

            ModEntry.phoneMenu.UpdateNpcList();
            List<string> npcNames = ModEntry.phoneMenu.messageableNpcList
                .Select(npc => npc.name)
                .ToList();
            return npcNames;
        }

        public void SendSmartphoneMessageFromNPC(string npcName, string message)
        {
            if (Game1.getCharacterFromName(npcName) == null) return;
            MessageManager.AddMessage(npcName, $"{npcName}: " + message);
        }

        public void SendSmartphoneMessageFromPlayer(string npcName, string message)
        {
            if (Game1.getCharacterFromName(npcName) == null) return;
            MessageManager.AddMessage(npcName, $"PLAYER: {message}", isFromPlayer: true);
        }

        public void SendSmartphoneNotification(string message, string notificationName = "")
        {
            if (ModEntry.phoneMenu == null) return;
            NotificationManager.addNotification(message, notificationName);
        }

        public string? CreateStardewConnectPostFromPlayer(string postText, string attachedImageFile = "")
        {
            return StardewConnectManager.AddPlayerPost(postText, attachedImageFile);
        }

        public string? CreateStardewConnectPostFromPlayerWithImages(string postText, IEnumerable<string>? attachedImageFiles = null)
        {
            return StardewConnectManager.AddPlayerPostWithAttachments(postText, attachedImageFiles);
        }

        public string? CreateStardewConnectPostFromNpc(string npcName, string postText, string attachedImageFile = "")
        {
            return StardewConnectManager.AddNpcPost(npcName, postText, attachedImageFile);
        }

        public bool AddStardewConnectCommentFromPlayer(string postId, string commentText)
        {
            return StardewConnectManager.AddPlayerComment(postId, commentText);
        }

        public bool AddStardewConnectCommentFromNpc(string postId, string npcName, string commentText)
        {
            return StardewConnectManager.AddNpcComment(postId, npcName, commentText);
        }

        public bool SetStardewConnectPostLiked(string postId, string actorName, bool liked)
        {
            return StardewConnectManager.SetPostLike(postId, actorName, liked);
        }

        public bool RegisterUnlimitedEvent(
            string ownerModId,
            string eventType,
            string displayName,
            Action<string> triggerEvent,
            int minimumHeartLevel = 0,
            string toolDescription = "")
        {
            return ModEntry.RegisterUnlimitedEventInternal(
                ownerModId,
                eventType,
                displayName,
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

    }

    public partial class ModEntry : Mod
    {


        public static ModEntry Instance;

        public static IMonitor SMonitor;
        public static IModHelper SHelper;
        private static IUnlimitedEventExpansionApi iUnlimitedEventExpansionApi;

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
        public static float cameraZoomFactor = 1f;

        public static bool pendingInitNotification = false;

        public static Dictionary<string, GiftMemory> GiftMemories = new();
        public static List<RecentEvent> RecentEvents = new();
        public static List<(string NpcName, string EventType, string TimeOfDay)> PendingUnlimitedEvents = new();

        public static bool isTodayEventAdded = false;
        public static Dictionary<(string season, int day), List<NPC>> NpcBirthdaysByDate = new();
        public static Dictionary<string, string> NpcCharacteristics = new();

        public static int lastTimeReceiveMessage = 300;

        public static List<string> FarmCropNames = new();
        public static List<string> FarmTreeNames = new();

        public static Dictionary<string, List<string>> npcMessagesToday = new();
        public static Dictionary<string, string> npcConversationSummary = new();


        public static Dictionary<string, List<string>> npcAges;
        public static Dictionary<string, string> npcToAgeGroup;

        public static PhoneMenu phoneMenu;
        private Dictionary<string, Dictionary<string, AreaData>> areaTags;


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
                PhoneMenu phoneMenu = new PhoneMenu();

                double power = 1.4;
                int maxValue = Math.Min(phoneMenu.messageableNpcList.Count, 20);
                if (maxValue < 1)
                    return;

                double rand = Game1.random.NextDouble();
                int result = (int)(Math.Pow(rand, power) * maxValue);

                int counter = 0;

                while (counter < 3)
                {
                    string npcName = phoneMenu.messageableNpcList[Math.Min(result + counter, maxValue - 1)].name;
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

                        if (!talkedToToday)
                        {
                            // Mark as talked to
                            if (friendship != null)
                                friendship.TalkedToToday = true;


                            npc.checkForNewCurrentDialogue(Game1.player.getFriendshipHeartLevelForNPC(npcName));

                            if (npc.CurrentDialogue != null)
                            {
                                foreach (var dialogue in npc.CurrentDialogue)
                                {
                                    foreach (var piece in dialogue.dialogues)
                                    {
                                        string parsed = piece.Text;
                                        parsed = dialogue.ReplacePlayerEnteredStrings(parsed);

                                        parsed = parsed.Split('$')[0].Trim();

                                        MessageManager.AddMessage(npcName, $"{npcName}: " + parsed);

                                        lastTimeReceiveMessage = Game1.timeOfDay;
                                    }
                                }
                                npc.CurrentDialogue?.Clear();
                            }
                        }
                        else
                        {
                            if (Game1.random.NextDouble() < 0.3 && Game1.player.getFriendshipHeartLevelForNPC(npcName) >= 3)
                            {
                                Task.Run(async () =>
                                {
                                    string messages = await SendMessageToAssistant(npcName, type: "invite");
                                    MessageManager.AddMessage(npcName, $"{npcName}:" + messages);
                                    lastTimeReceiveMessage = Game1.timeOfDay;
                                });
                            }
                            else
                            {
                                Task.Run(async () =>
                                {
                                    string messages = await SendMessageToAssistant(npcName, type: "text");
                                    MessageManager.AddMessage(npcName, $"{npcName}:" + messages);
                                    lastTimeReceiveMessage = Game1.timeOfDay;
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
                    string reply = await ModEntry.SendMessageToAssistant(npcName, message, 1);
                    if (reply != null && reply != "")
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
                        Random rng = new Random();

                        foreach (var dialogue in npc.CurrentDialogue)
                        {
                            foreach (var piece in dialogue.dialogues)
                            {
                                int delay = rng.Next(3000, 5000);
                                await Task.Delay(delay);

                                string parsed = piece.Text;
                                parsed = dialogue.ReplacePlayerEnteredStrings(parsed);
                                parsed = parsed.Split('$')[0].Trim();

                                MessageManager.AddMessage(npcName, $"{npcName}: " + parsed);
                            }
                        }

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