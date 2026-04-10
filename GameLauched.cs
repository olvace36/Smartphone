using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using ContentPatcher;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Smartphone.Data;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Delegates;
using StardewValley.Extensions;
using StardewValley.GameData.Characters;
using StardewValley.GameData.Crops;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Minigames;
using StardewValley.Monsters;
using StardewValley.Network;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using StardewValley.Triggers;
using xTile.Dimensions;
using xTile.Tiles;
using static StardewValley.Minigames.MineCart;
using System.Text.Json;

namespace Smartphone
{
    /// <summary>The mod entry point.</summary>
    public partial class ModEntry
    {
        private const int CameraViewportOffsetX = 40;
        private const int CameraViewportOffsetY = 116;
        private const int CameraViewportWidth = 520;
        private const int CameraViewportHeight = 810;

        // Image-tagging state and helpers moved to ImageTagging.cs.

        // *************************** ENTRY ***************************
        //


        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            Config = Helper.ReadConfig<ModConfig>();

            ModEntry.Instance = this;


            SMonitor = Monitor;
            SHelper = helper;


            Textures.LoadTextures();
            helper.Events.GameLoop.GameLaunched += OnGameLauched;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.Saving += OnSaving;
            helper.Events.GameLoop.DayEnding += OnDayEnding;
            helper.Events.GameLoop.TimeChanged += OnTimeChange;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondUpdateTicked;
            helper.Events.Display.Rendered += OnRendered;


            var harmony = new Harmony(this.ModManifest.UniqueID);
            harmony.PatchAll();


            // dev tool: prepare for grid overlay
            solidPixel = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1);
            solidPixel.SetData(new[] { Color.White });


        }



        [HarmonyPatch(typeof(NPC), nameof(NPC.receiveGift))]
        [HarmonyPatch(new[] { typeof(StardewValley.Object), typeof(Farmer), typeof(bool), typeof(float), typeof(bool) })]
        public static class NPCReceiveGiftPatch
        {
            public static void Postfix(NPC __instance, StardewValley.Object o)
            {
                if (__instance != null && o != null)
                {
                    GiftMemories[__instance.Name] = new GiftMemory
                    {
                        GiftName = o.DisplayName,
                        DaysRemaining = 3
                    };
                }
            }
        }



        //
        // ***************************  END OF ENTRY ***************************
        //

        private void OnGameLauched(object sender, StardewModdingAPI.Events.GameLaunchedEventArgs e)
        {
            this.Monitor.Log("Loading Smartphone", LogLevel.Info);
            var api = this.Helper.ModRegistry.GetApi<IContentPatcherAPI>("Pathoschild.ContentPatcher");

            ConfigMenu(api, this.ModManifest, Helper);

            iUnlimitedEventExpansionApi = SHelper.ModRegistry.GetApi<IUnlimitedEventExpansionApi>("d5a1lamdtd.UnlimitedEventExpansion");

            if (iUnlimitedEventExpansionApi == null)
            {
                Monitor.Log("UnlimitedEventExpansion is either not installed or failed to load.", LogLevel.Info);
                return;
            }

        }       // **** Config Handle ****

        private bool CanTriggerScheduledUnlimitedEvents()
        {
            return iUnlimitedEventExpansionApi == null || !iUnlimitedEventExpansionApi.IsAnEventPending();
        }

        private static bool TryNormalizeEventTime(string? eventTime, out string normalizedTime)
        {
            normalizedTime = string.Empty;
            if (string.IsNullOrWhiteSpace(eventTime))
                return false;

            if (!int.TryParse(eventTime.Trim(), out int parsedTime))
                return false;

            int hour = parsedTime / 100;
            int minute = parsedTime % 100;

            // Validation: 6am to 11pm (2300)
            if (hour < 6 || hour > 23 || minute > 59)
                return false;

            // Round the minutes down to the nearest 10 (e.g., 15 becomes 10)
            int normalizedMinute = (minute / 10) * 10;

            // Reconstruct the time (e.g., 600 + 10 = 610)
            int finalTime = (hour * 100) + normalizedMinute;

            normalizedTime = $"{finalTime:0000}";
            return true;
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;
            if (e.Button == Config.ModKey && Game1.activeClickableMenu == null && Game1.currentMinigame == null)
            {
                if (phoneMenu == null)
                    phoneMenu = new PhoneMenu();

                Game1.activeClickableMenu = phoneMenu;
            }

            // DEVTOOL
            if (e.Button == SButton.O && Game1.activeClickableMenu == null && false)
            {
                isGridVisible = !isGridVisible;
                firstClickTile = null;
                return;
            }
        }


        private void OnSaving(object sender, SavingEventArgs e)
        {
            MessageManager.SaveData();
            NotificationManager.SaveNoticationData();
            StardewConnectManager.SaveData();
            SaveImageTags();

            if (phoneMenu != null)
                phoneMenu.ClosePhoneMenu();
            Helper.Data.WriteJsonFile($"./userdata/{Constants.SaveFolderName}/RecentEventMemory", RecentEvents);
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            IndoorAreasByLocation = SHelper.Data.ReadJsonFile<Dictionary<string, Dictionary<string, AreaData>>>("assets/indoor_area.json") 
                        ?? new Dictionary<string, Dictionary<string, AreaData>>();

            OutdoorAreasByLocation = SHelper.Data.ReadJsonFile<Dictionary<string, Dictionary<string, AreaData>>>("assets/outdoor_area.json") 
                        ?? new Dictionary<string, Dictionary<string, AreaData>>();

            areaTags = new Dictionary<string, Dictionary<string, AreaData>>(IndoorAreasByLocation);
            foreach (var kvp in OutdoorAreasByLocation)
            {
                areaTags[kvp.Key] = kvp.Value; 
            }

            LoadImageTags();

            string npc_characteristic = Helper.ModContent.GetInternalAssetName("assets/npc_characteristic.json").BaseName;
            NpcCharacteristics = Helper.ModContent.Load<Dictionary<string, string>>(npc_characteristic);

            string npcAgesAsset = Helper.ModContent.GetInternalAssetName("assets/npc_age.json").BaseName;
            npcAges = Helper.ModContent.Load<Dictionary<string, List<string>>>(npcAgesAsset);
            npcToAgeGroup = npcAges
                .SelectMany(kvp => kvp.Value.Select(npc => new { npc, group = kvp.Key }))
                .ToDictionary(x => x.npc, x => x.group);



            foreach (var npc in Utility.getAllVillagers())
            {
                if (!string.IsNullOrEmpty(npc.Birthday_Season) && npc.Birthday_Day > 0)
                {
                    var key = (npc.Birthday_Season, npc.Birthday_Day);
                    if (!NpcBirthdaysByDate.ContainsKey(key))
                        NpcBirthdaysByDate[key] = new List<NPC>();

                    NpcBirthdaysByDate[key].Add(npc);
                }
            }


            string targetModId = this.ModManifest.UniqueID;
            var modInfo = this.Helper.ModRegistry.Get(targetModId);

            if (modInfo != null)
            {
                Task.Run(async () =>
                {
                    bool hasNewerVersion = await HasNewerVersion(modInfo);

                    if (hasNewerVersion)
                    {
                        DelayedAction.functionAfterDelay(() =>
                        {
                            try
                            {
                                SMonitor.Log($"Smartphone: Newer version available", LogLevel.Error);
                                Game1.drawLetterMessage("=== Smartphone ===^^Newer version of Smartphone are available. Your current version may be outdated and no longer working.^^" +
                                    "Please note that your data, including conversation, summary, setting, memory, everything,... are saved in        $$/Mods/Smartphone/Userdata$$ folder.^    ]] YOU MUST COPY IT WHEN UPDATE THE MOD [[^^");

                                NotificationManager.addNotification("=== Smartphone ===^^Newer version of Smartphone are available. Your current version may be outdated and no longer working.^^" +
                                    "Please note that your data, including conversation, summary, setting, memory, everything,... are saved in        $$/Mods/Smartphone/Userdata$$ folder.^    ]] YOU MUST COPY IT WHEN UPDATE THE MOD [[^^");
                            }
                            catch (Exception ex)
                            {
                                SMonitor.Log($"Smartphone: Unable to notify about newer version: {ex}", LogLevel.Trace);
                            }
                        }, 10000);
                    }
                });
            }
        }



        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            // Reset daily variables
            isTodayEventAdded = false;
            lastTimeReceiveMessage = 600;
            npcMessagesToday.Clear();
            FarmCropNames.Clear();
            FarmTreeNames.Clear();
            PendingUnlimitedEvents.Clear();
            ResetTriggeredUnlimitedEventTag();

            MessageManager.LoadData();
            ApplySavedPhoneTheme();
            NotificationManager.LoadNoticationData();
            StardewConnectManager.LoadData();
            GiftMemories = Helper.Data.ReadJsonFile<Dictionary<string, GiftMemory>>($"./userdata/{Constants.SaveFolderName}/GiftMemoryData")
                   ?? new Dictionary<string, GiftMemory>();
            RecentEvents = Helper.Data.ReadJsonFile<List<RecentEvent>>($"./userdata/{Constants.SaveFolderName}/RecentEventMemory")
                   ?? new List<RecentEvent>();
            npcConversationSummary = Helper.Data.ReadJsonFile<Dictionary<string, string>>($"./userdata/{Constants.SaveFolderName}/npcConversationSummary")
                   ?? new Dictionary<string, string>();


            foreach (var terrainFeature in Game1.getFarm().terrainFeatures.Values)
            {
                if (terrainFeature is HoeDirt hoeDirt && hoeDirt.crop != null)
                {
                    string harvestIndex = hoeDirt.crop.indexOfHarvest.Value;
                    string cropName = new StardewValley.Object(harvestIndex, 1).DisplayName;
                    FarmCropNames.Add(cropName);
                }
                else if (terrainFeature is FruitTree fruitTree)
                {
                    string treeName = fruitTree.GetDisplayName();
                    FarmTreeNames.Add(treeName);
                }
            }

            // send conversationSummary to iModApi
            if (iUnlimitedEventExpansionApi != null)
                iUnlimitedEventExpansionApi.SendNpcConversationSummary(npcConversationSummary);
        }

        private void ApplySavedPhoneTheme()
        {
            string resolvedThemeName = AssetHelper.ResolvePhoneThemeName(MessageManager.currentPhoneTheme);
            MessageManager.currentPhoneTheme = resolvedThemeName;

            AssetHelper.SetCurrentPhoneTheme(resolvedThemeName);
            Textures.LoadTextures();

            if (phoneMenu != null)
                phoneMenu.ReloadThemeTextures();
        }

        private void OnDayEnding(object sender, DayEndingEventArgs e)
        {
            // gift memory
            var keysToRemove = new List<string>();

            foreach (var entry in GiftMemories)
            {
                entry.Value.DaysRemaining--;
                if (entry.Value.DaysRemaining <= 0)
                    keysToRemove.Add(entry.Key);
            }

            foreach (var key in keysToRemove)
                GiftMemories.Remove(key);


            // event memory
            foreach (var evt in RecentEvents)
                evt.DaysRemaining--;

            RecentEvents = RecentEvents
                .Where(evt => evt.DaysRemaining > 0)
                .ToList();


            // chat summary
            foreach (var kvp in npcMessagesToday)
            {
                string npcName = kvp.Key;
                List<string> messages = kvp.Value
                    .Skip(Math.Max(0, kvp.Value.Count - 30))
                    .ToList();
                if (messages.Count == 0)
                    continue;

                string messageList = string.Join("\n", messages);
                Task.Run(async () =>
                {
                    string result = await SummaryConversation(npcName, messageList);
                    if (!npcConversationSummary.ContainsKey(npcName))
                        npcConversationSummary.Add(npcName, result);
                    else
                        npcConversationSummary[npcName] = result;

                    Helper.Data.WriteJsonFile($"./userdata/{Constants.SaveFolderName}/npcConversationSummary", npcConversationSummary);
                });
            }
        }

        private void OnTimeChange(object sender, TimeChangedEventArgs e)
        {
            if (e.NewTime % 10 == 0)
                RandomStardewSocialEvent();

            if (Game1.timeOfDay < 2300)
                CheckSendNewMessage();

            if (Config.OpenAIKey == "" && e.NewTime % 400 == 0)
            {
                // gpt-5.4 variant support reasoning effor: none, low, medium, high, xhigh
                // gpt-5 variant support reasoning effort: minimal, low, medium, high
                Task.Run(async () =>
                {
                    var (premium, regular) = await GetOpenAIUsage();
                    if (regular > 15000000)
                    {
                        chatModel = "gpt-5-nano";
                        chatReasoningEffort = new { effort = "minimal", summary = "auto" };

                        summaryModel = "gpt-5-nano";
                        summaryReasoningEffort = new { effort = "minimal", summary = "auto" };
                    }
                    else if (regular > 10000000)
                    {
                        chatModel = "gpt-5-mini";
                        chatReasoningEffort = new { effort = "minimal", summary = "auto" };

                        summaryModel = "gpt-5-mini";
                        summaryReasoningEffort = new { effort = "low", summary = "auto" };
                    }
                    else
                    {
                        chatModel = "gpt-5.4-mini";
                        chatReasoningEffort = new { effort = "none", summary = "auto" };

                        summaryModel = "gpt-5.4-mini";
                        summaryReasoningEffort = new { effort = "low", summary = "auto" };
                    }
                });
            }

            if (pendingInitNotification && isPlayerFree())
            {
                Game1.drawLetterMessage("=== Smartphone ===^^Looks like this is your first time here, or you have recently updated the mod and >> LOST YOUR DATA @@^^" +
                    "Please note that your data, including conversation, summary, setting, memory, everything,... are saved in        $$/Mods/Smartphone/Userdata$$ folder.^    >> YOU MUST COPY IT WHEN UPDATE THE MOD @@^^" +
                    "Thanks for trying out the mod, HaPyke +++");

                NotificationManager.addNotification("=== Smartphone ===^^Looks like this is your first time here, or you have recently updated the mod and >> LOST YOUR DATA @@^^" +
                    "Please note that your data, including conversation, summary, setting, memory, everything,... are saved in        $$/Mods/Smartphone/Userdata$$ folder.^    >> YOU MUST COPY IT WHEN UPDATE THE MOD @@^^" +
                    "Thanks for trying out the mod, HaPyke +++");

                pendingInitNotification = false;
            }

            if (PendingUnlimitedEvents.Count > 0 && isPlayerFree() && e.NewTime < 2500 && CanTriggerScheduledUnlimitedEvents())
            {
                foreach (var scheduledEvent in PendingUnlimitedEvents.ToList())
                {
                    if (!int.TryParse(scheduledEvent.TimeOfDay, out int eventTime))
                    {
                        PendingUnlimitedEvents.Remove(scheduledEvent);
                        continue;
                    }

                    if (e.NewTime >= eventTime)
                    {
                        bool eventTriggered = TryTriggerRegisteredUnlimitedEvent(scheduledEvent.EventType, scheduledEvent.NpcName);
                        if (!eventTriggered)
                        {
                            SMonitor.Log(
                                $"Unable to trigger event '{scheduledEvent.EventType}' for '{scheduledEvent.NpcName}'.",
                                LogLevel.Warn);
                        }
                        else
                        {
                            RememberTriggeredUnlimitedEvent(scheduledEvent.EventType, scheduledEvent.NpcName);
                        }

                        PendingUnlimitedEvents.Remove(scheduledEvent);
                        break;
                    }
                }
            }
        }

        private void OnOneSecondUpdateTicked(object sender, OneSecondUpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            if (e.IsMultipleOf(6000))
                CheckCurrentEvent();

        }



        private bool isPlayerFree()
        {
            return Game1.timeOfDay > 600
            && Game1.player.CanMove
            && !(Game1.player.isRidingHorse()
                || Game1.currentLocation == null
                || Game1.eventUp
                || Game1.isFestival()
                || Game1.IsFading()
                || Game1.activeClickableMenu != null
                || Game1.dialogueUp
                || Game1.player.UsingTool);
        }


        private static void sendPlayerGift(string qualifiedItemId, string npcName)
        {
            FarmHouse home = Utility.getHomeOfFarmer(Game1.player);
            var baseTile = home.getEntryLocation();

            // Try to find nearest valid placement tile
            Vector2? placeTile = null;
            for (int radius = 2; radius <= 10 && placeTile == null; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        Vector2 check = new Vector2(baseTile.X + dx, baseTile.Y + dy);
                        if (home.isTileOnMap(check) && home.isTilePlaceable(check) && home.isTileLocationOpen(check)
                            && !home.objects.ContainsKey(check))
                        {
                            placeTile = check;
                            break;
                        }
                    }
                }
            }

            if (placeTile == null)
            {
                return;
            }

            // Collect items
            List<Item> allItems = new List<Item>();
            var item = new StardewValley.Object(qualifiedItemId, 1);

            for (var i = 0; i < Game1.random.Next(1, 4); i++)
                allItems.Add(item);

            if (allItems.Count == 0)
                return;

            // Create and place the giftbox
            Chest giftbox = new Chest(allItems, placeTile.Value, giftbox: true, giftboxIndex: 0, giftboxIsStarterGift: false);
            home.objects[placeTile.Value] = giftbox;

            MessageManager.AddMessage(npcName, $"{npcName}: Supprise!!! I just send you a gift of {item.Name}!");
        }

        private static async Task<(bool IsLatest, string? LatestVersion, string? LatestUrl)> CheckLatestAsync(IModInfo modInfo)
        {
            if (modInfo?.Manifest == null)
                return (true, null, null);

            var request = new
            {
                mods = new[]
                {
            new
            {
                id = modInfo.Manifest.UniqueID,
                updateKeys = modInfo.Manifest.UpdateKeys,
                installedVersion = modInfo.Manifest.Version.ToString(),
                isBroken = false
            }
        },
                apiVersion = Constants.ApiVersion.ToString(),
                gameVersion = Game1.version.ToString(),
                platform = Constants.TargetPlatform.ToString(),
                includeExtendedMetadata = false
            };

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            using var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(request, jsonOptions),
                Encoding.UTF8,
                "application/json"
            );

            using HttpResponseMessage response = await new HttpClient().PostAsync($"https://smapi.io/api/v{Constants.ApiVersion}/mods", content);
            response.EnsureSuccessStatusCode();

            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            JsonElement result = document.RootElement[0];

            if (result.TryGetProperty("suggestedUpdate", out JsonElement suggestedUpdate) && suggestedUpdate.ValueKind == JsonValueKind.Object)
            {
                return (
                    false,
                    suggestedUpdate.GetProperty("version").GetString(),
                    suggestedUpdate.GetProperty("url").GetString()
                );
            }

            return (true, null, null);
        }

        public static async Task<bool> HasNewerVersion(IModInfo? modInfo)
        {
            if (modInfo?.Manifest == null)
                return false;

            var update = await CheckLatestAsync(modInfo);
            return !update.IsLatest;
        }

    }
}
