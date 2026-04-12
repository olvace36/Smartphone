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

            // if (e.Button == SButton.MouseLeft)
            // {
            //     var tile = e.Cursor.Tile;
            //     Game1.chatBox.addErrorMessage((IsWalkableWarpTile(Game1.currentLocation, (int)tile.X, (int)tile.Y) && IsWalkableWarpTile(Game1.currentLocation, (int)tile.X, (int)tile.Y - 1)).ToString());
            // }
            if (e.Button == Config.ModKey && Game1.activeClickableMenu == null && Game1.currentMinigame == null)
            {
                if (phoneMenu == null)
                    phoneMenu = new PhoneMenu();

                Game1.activeClickableMenu = phoneMenu;
            }

            // DEVTOOL
            if (e.Button == SButton.O && Game1.activeClickableMenu == null && true)
            {
                isGridVisible = !isGridVisible;
                Game1.chatBox.addInfoMessage($"Grid {(isGridVisible ? "enabled" : "disabled")}.");
                ToggleGrid(e);
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
            CreateRandomNpc();
            // if (e.NewTime % 300 == 0)
            //     RandomStardewSocialEvent();

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





























        public static void CreateRandomNpc(string npcName = "Lewis")
        {
            NPC? dummyNpc = CreateDummyNpc(npcName);
            if (dummyNpc == null)
                return;


            Dictionary<string, Dictionary<string, AreaData>>? customAreaTags = Instance?.areaTags;
            if (customAreaTags == null || customAreaTags.Count == 0)
                return;

            bool sveInstalled = SHelper?.ModRegistry?.Get("FlashShifter.StardewValleyExpandedCP") != null;
            bool rsvInstalled = SHelper?.ModRegistry?.Get("Rafseazz.RSVCP") != null;

            List<string> remainingLocations = customAreaTags
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Key)
                                && entry.Value != null
                                && entry.Value.Count > 0)
                .Select(entry => entry.Key)
                .ToList();

            const int maxLocationAttempts = 3;

            for (int attempt = 0; attempt < maxLocationAttempts && remainingLocations.Count > 0; attempt++)
            {
                int randomLocationIndex = Game1.random.Next(remainingLocations.Count);
                string locationName = remainingLocations[randomLocationIndex];
                remainingLocations.RemoveAt(randomLocationIndex);

                if (!customAreaTags.TryGetValue(locationName, out Dictionary<string, AreaData>? areasByName)
                    || areasByName == null
                    || areasByName.Count == 0)
                {
                    continue;
                }

                GameLocation targetLocation = Game1.getLocationFromName(locationName);
                if (targetLocation == null || targetLocation.farmers.Count > 0)
                    continue;

                List<KeyValuePair<string, AreaData>> candidateAreas = areasByName
                    .Where(entry => entry.Value != null && !string.IsNullOrWhiteSpace(entry.Key))
                    .Where(entry => !entry.Key.StartsWith("SVE_", StringComparison.OrdinalIgnoreCase) || sveInstalled)
                    .Where(entry => !entry.Key.StartsWith("RSV_", StringComparison.OrdinalIgnoreCase) || rsvInstalled)
                    .ToList();

                if (candidateAreas.Count == 0)
                    continue;

                KeyValuePair<string, AreaData> selectedArea = candidateAreas[Game1.random.Next(candidateAreas.Count)];
                if (!TryFindRandomWalkableInteriorTile(targetLocation, selectedArea.Value, out Vector2 targetTile))
                    continue;

                HashSet<string> ownerNpcNameSet = BuildOwnerNpcNameSet(selectedArea.Value.ownerNpc, npcName);
                bool ownerNpcRequired = ownerNpcNameSet.Count > 0;

                int additionalNpcCount = DetermineAdditionalNpcCount(selectedArea.Value, ownerNpcRequired);
                List<NPC> additionalDummyNpcs = CreateAdditionalDummyNpcs(npcName, additionalNpcCount, ownerNpcNameSet);

                if (ownerNpcRequired && !additionalDummyNpcs.Any(npc => ownerNpcNameSet.Contains(npc.Name)))
                    continue;

                var occupiedTiles = new HashSet<(int X, int Y)>
                {
                    ((int)targetTile.X, (int)targetTile.Y)
                };

                List<NPC> visibleNpcAtTarget = targetLocation.characters
                    .OfType<NPC>()
                    .Where(npc => npc.IsVillager && !npc.IsInvisible)
                    .ToList();

                foreach (var character in visibleNpcAtTarget)
                {
                    character.clearTextAboveHead();
                    character.IsInvisible = true;
                }

                var spawnedDummies = new List<NPC>();
                bool captureCompleted = false;

                try
                {
                    Game1.warpCharacter(dummyNpc, locationName, targetTile);
                    ApplyNaturalNpcWarpOffset(dummyNpc);
                    spawnedDummies.Add(dummyNpc);

                    bool ownerNpcPlaced = false;

                    for (int additionalIndex = 0; additionalIndex < additionalDummyNpcs.Count; additionalIndex++)
                    {
                        NPC additionalDummyNpc = additionalDummyNpcs[additionalIndex];
                        Vector2 placementAnchorTile = DetermineAdditionalNpcPlacementAnchor(spawnedDummies, dummyNpc, additionalIndex);
                        int maxDistanceFromAnchor = additionalIndex switch
                        {
                            0 => 5,
                            1 => 4,
                            _ => 3
                        };

                        if (!TryFindNearbyWalkableInteriorTile(targetLocation, selectedArea.Value, placementAnchorTile, maxDistanceFromAnchor, occupiedTiles, out Vector2 additionalTile))
                            continue;

                        occupiedTiles.Add(((int)additionalTile.X, (int)additionalTile.Y));
                        Game1.warpCharacter(additionalDummyNpc, locationName, additionalTile);
                        ApplyNaturalNpcWarpOffset(additionalDummyNpc);
                        spawnedDummies.Add(additionalDummyNpc);

                        if (ownerNpcNameSet.Contains(additionalDummyNpc.Name))
                            ownerNpcPlaced = true;
                    }

                    if (ownerNpcRequired && !ownerNpcPlaced)
                        continue;

                    Vector2 captureCenterTile = DetermineGroupCaptureCenterTile(spawnedDummies, dummyNpc, targetLocation);
                    ApplyGroupFacingDirections(spawnedDummies, dummyNpc, captureCenterTile);

                    CaptureNpcPhoto(dummyNpc, captureCenterTile, Game1.random.NextBool(), Game1.random.NextDouble() < 0.3, visibleNpcAtTarget);
                    captureCompleted = true;
                }
                finally
                {
                    foreach (NPC character in visibleNpcAtTarget)
                        character.IsInvisible = false;

                    foreach (NPC spawnedDummy in spawnedDummies)
                        spawnedDummy.currentLocation?.characters?.Remove(spawnedDummy);
                }

                if (captureCompleted)
                    return;
            }
        }

        private static NPC? CreateDummyNpc(string npcName)
        {
            if (string.IsNullOrWhiteSpace(npcName))
                return null;

            NPC? realNpc = Game1.getCharacterFromName(npcName, mustBeVillager: false);
            if (realNpc == null)
                return null;

            try
            {
                Texture2D portrait = SHelper.GameContent.Load<Texture2D>($"Characters\\{npcName}");
                NPC dummyNpc = new NPC(new AnimatedSprite($"Characters\\{npcName}", 0, 16, 32), Vector2.Zero, "Town", 2, npcName, portrait, true);
                // dummyNpc.SimpleNonVillagerNPC = true;
                dummyNpc.EventActor = true;
                dummyNpc.faceDirection(2);
                return dummyNpc;
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed to create dummy NPC for '{npcName}': {ex}", LogLevel.Trace);
                return null;
            }
        }


        private static int DetermineAdditionalNpcCount(AreaData area, bool ownerNpcRequired)
        {
            int width = Math.Max(0, Math.Abs(area.endX - area.startX) - 1);
            int height = Math.Max(0, Math.Abs(area.endY - area.startY) - 1);
            int interiorTileCount = width * height;

            int maxAdditional = interiorTileCount switch
            {
                < 12 => 0,
                < 48 => 1,
                < 120 => 2,
                _ => 3
            };

            maxAdditional = Game1.random.Next(0, maxAdditional + 1);

            if (ownerNpcRequired)
                maxAdditional = Math.Max(1, maxAdditional);

            int minAdditional = ownerNpcRequired ? 1 : 0;
            maxAdditional = Math.Clamp(maxAdditional, minAdditional, 3);

            return Game1.random.Next(minAdditional, maxAdditional + 1);
        }

        private static HashSet<string> BuildOwnerNpcNameSet(List<string>? ownerNpcNames, string primaryNpcName)
        {
            var ownerNpcNameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (ownerNpcNames == null)
                return ownerNpcNameSet;

            foreach (string ownerNpcName in ownerNpcNames)
            {
                if (string.IsNullOrWhiteSpace(ownerNpcName))
                    continue;

                string trimmedName = ownerNpcName.Trim();
                if (string.Equals(trimmedName, primaryNpcName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!IsEligibleCompanionNpc(trimmedName))
                    continue;

                ownerNpcNameSet.Add(trimmedName);
            }

            return ownerNpcNameSet;
        }

        private static bool IsEligibleCompanionNpc(string npcName)
        {
            if (string.IsNullOrWhiteSpace(npcName))
                return false;

            NPC? npc = Game1.getCharacterFromName(npcName, mustBeVillager: false);
            return npc != null && npc.IsVillager && npc.CanSocialize && !npc.IsInvisible;
        }

        private static List<NPC> CreateAdditionalDummyNpcs(string primaryNpcName, int additionalNpcCount, HashSet<string> ownerNpcNameSet)
        {
            var additionalDummyNpcs = new List<NPC>();
            if (additionalNpcCount <= 0)
                return additionalDummyNpcs;

            var selectedNpcNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { primaryNpcName };

            if (ownerNpcNameSet.Count > 0)
            {
                List<string> ownerCandidates = ownerNpcNameSet.ToList();
                while (ownerCandidates.Count > 0 && additionalDummyNpcs.Count == 0)
                {
                    int randomOwnerIndex = Game1.random.Next(ownerCandidates.Count);
                    string ownerNpcName = ownerCandidates[randomOwnerIndex];
                    ownerCandidates.RemoveAt(randomOwnerIndex);

                    NPC? ownerDummyNpc = CreateDummyNpc(ownerNpcName);
                    if (ownerDummyNpc == null)
                        continue;

                    additionalDummyNpcs.Add(ownerDummyNpc);
                    selectedNpcNames.Add(ownerNpcName);
                }
            }

            if (additionalDummyNpcs.Count >= additionalNpcCount)
                return additionalDummyNpcs;

            List<string> randomCandidates = Utility.getAllVillagers()
                .Where(npc => npc != null && npc.IsVillager && npc.CanSocialize && !npc.IsInvisible)
                .Select(npc => npc.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Where(name => !selectedNpcNames.Contains(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            int primaryNpcAge = ResolveNpcAge(primaryNpcName);

            while (additionalDummyNpcs.Count < additionalNpcCount && randomCandidates.Count > 0)
            {
                string? candidateName = PickWeightedCompanionName(randomCandidates, primaryNpcAge);
                if (string.IsNullOrWhiteSpace(candidateName))
                    break;

                randomCandidates.RemoveAll(name => string.Equals(name, candidateName, StringComparison.OrdinalIgnoreCase));
                selectedNpcNames.Add(candidateName);

                NPC? companionDummyNpc = CreateDummyNpc(candidateName);
                if (companionDummyNpc != null)
                    additionalDummyNpcs.Add(companionDummyNpc);
            }

            return additionalDummyNpcs;
        }

        private static string? PickWeightedCompanionName(List<string> candidates, int primaryNpcAge)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            var weightedCandidates = new List<(string Name, double CumulativeWeight)>(candidates.Count);
            double cumulativeWeight = 0;
            bool primaryIsTeen = primaryNpcAge == 1;

            foreach (string candidateName in candidates)
            {
                int candidateAge = ResolveNpcAge(candidateName);
                bool candidateIsTeen = candidateAge == 1;
                double weight = primaryIsTeen
                    ? (candidateIsTeen ? 3.0 : 1.5)
                    : (candidateIsTeen ? 1.0 : 2.5);

                cumulativeWeight += weight;
                weightedCandidates.Add((candidateName, cumulativeWeight));
            }

            double roll = Game1.random.NextDouble() * cumulativeWeight;
            foreach ((string Name, double CumulativeWeight) candidate in weightedCandidates)
            {
                if (roll <= candidate.CumulativeWeight)
                    return candidate.Name;
            }

            return weightedCandidates[weightedCandidates.Count - 1].Name;
        }

        private static int ResolveNpcAge(string npcName)
        {
            NPC? realNpc = Game1.getCharacterFromName(npcName, mustBeVillager: false);
            return realNpc?.Age ?? 0;
        }

        private static Vector2 DetermineAdditionalNpcPlacementAnchor(List<NPC> spawnedDummies, NPC mainDummyNpc, int additionalIndex)
        {
            if (spawnedDummies == null || spawnedDummies.Count == 0)
                return mainDummyNpc.Tile;

            if (additionalIndex <= 0)
                return mainDummyNpc.Tile;

            if (additionalIndex == 1)
            {
                List<NPC> previousTwoNpcs = spawnedDummies.Take(2).ToList();
                if (previousTwoNpcs.Count < 2)
                    return mainDummyNpc.Tile;

                return ComputeRoundedCenterTile(previousTwoNpcs.Select(npc => npc.Tile));
            }

            List<NPC> previousThreeNpcs = spawnedDummies.Take(3).ToList();
            if (previousThreeNpcs.Count < 3)
                previousThreeNpcs = spawnedDummies;

            return ComputeRoundedCenterTile(previousThreeNpcs.Select(npc => npc.Tile));
        }

        private static Vector2 ComputeRoundedCenterTile(IEnumerable<Vector2> tiles)
        {
            List<Vector2> tileList = tiles?.ToList() ?? new List<Vector2>();
            if (tileList.Count == 0)
                return Vector2.Zero;

            float averageX = tileList.Average(tile => tile.X);
            float averageY = tileList.Average(tile => tile.Y);
            return new Vector2((float)Math.Round(averageX), (float)Math.Round(averageY));
        }

        private static bool TryFindNearbyWalkableInteriorTile(GameLocation location, AreaData area, Vector2 anchorTile, int maxDistanceFromAnchor, HashSet<(int X, int Y)> excludedTiles, out Vector2 targetTile)
        {
            targetTile = Vector2.Zero;

            int minX = Math.Min(area.startX, area.endX) + 1;
            int maxX = Math.Max(area.startX, area.endX) - 1;
            int minY = Math.Min(area.startY, area.endY) + 1;
            int maxY = Math.Max(area.startY, area.endY) - 1;

            if (minX > maxX || minY > maxY)
                return false;

            int anchorX = (int)anchorTile.X;
            int anchorY = (int)anchorTile.Y;
            var nearbyTiles = new List<Vector2>();
            var preferredSpreadTiles = new List<Vector2>();

            for (int tileY = minY; tileY <= maxY; tileY++)
            {
                for (int tileX = minX; tileX <= maxX; tileX++)
                {
                    if (excludedTiles.Contains((tileX, tileY)))
                        continue;

                    if (!IsWalkableWarpTile(location, tileX, tileY) || !IsWalkableWarpTile(location, tileX, tileY - 1))
                        continue;

                    int distance = Math.Abs(tileX - anchorX) + Math.Abs(tileY - anchorY);
                    if (distance <= 0 || distance > maxDistanceFromAnchor)
                        continue;

                    Vector2 candidateTile = new Vector2(tileX, tileY);
                    nearbyTiles.Add(candidateTile);
                    if (distance >= 2)
                        preferredSpreadTiles.Add(candidateTile);
                }
            }

            if (nearbyTiles.Count == 0)
                return false;

            List<Vector2> finalCandidates = preferredSpreadTiles.Count > 0
                ? preferredSpreadTiles
                : nearbyTiles;

            targetTile = finalCandidates[Game1.random.Next(finalCandidates.Count)];
            return true;
        }

        private static Vector2 DetermineGroupCaptureCenterTile(List<NPC> spawnedDummies, NPC mainDummyNpc, GameLocation targetLocation)
        {
            if (spawnedDummies == null || spawnedDummies.Count <= 1)
                return mainDummyNpc.Tile;

            float averageX = spawnedDummies.Average(npc => npc.Tile.X);
            float averageY = spawnedDummies.Average(npc => npc.Tile.Y);
            Vector2 centerTile = new Vector2((float)Math.Round(averageX), (float)Math.Round(averageY));

            Vector2 offsetFromMain = centerTile - mainDummyNpc.Tile;
            float distanceFromMain = offsetFromMain.Length();
            if (distanceFromMain > 2f && distanceFromMain > 0f)
            {
                Vector2 clampedOffset = offsetFromMain * (2f / distanceFromMain);
                centerTile = mainDummyNpc.Tile + clampedOffset;
                centerTile = new Vector2((float)Math.Round(centerTile.X), (float)Math.Round(centerTile.Y));
            }

            if (!targetLocation.isTileOnMap(centerTile))
                return mainDummyNpc.Tile;

            return centerTile;
        }

        private static void ApplyGroupFacingDirections(List<NPC> spawnedDummies, NPC mainDummyNpc, Vector2 groupCenterTile)
        {
            if (spawnedDummies == null || spawnedDummies.Count == 0)
                return;

            if (spawnedDummies.Count == 1)
            {
                mainDummyNpc.faceDirection(2);
                return;
            }

            foreach (NPC spawnedDummy in spawnedDummies)
            {
                Vector2? randomOtherTile = GetRandomOtherNpcTile(spawnedDummy, spawnedDummies);
                Vector2 lookTarget = randomOtherTile ?? groupCenterTile;

                if (lookTarget == spawnedDummy.Tile)
                {
                    Vector2 randomNudge = new Vector2(Game1.random.Next(-1, 2), Game1.random.Next(-1, 2));
                    if (randomNudge != Vector2.Zero)
                        lookTarget += randomNudge;
                }
                var direction = GetFacingDirectionToward(spawnedDummy.Tile, lookTarget);
                Game1.chatBox.addErrorMessage($"NPC '{spawnedDummy.Name}' at {spawnedDummy.Tile} faces {(randomOtherTile.HasValue ? $"other NPC at {randomOtherTile.Value}" : $"group center at {groupCenterTile}")} with direction {direction}.");
                spawnedDummy.faceDirection(direction);
            }
        }

        private static Vector2? GetRandomOtherNpcTile(NPC sourceNpc, List<NPC> npcGroup)
        {
            var otherNpcs = npcGroup
                .Where(candidateNpc => !ReferenceEquals(sourceNpc, candidateNpc))
                .ToList();

            if (otherNpcs.Count == 0)
                return null;

            NPC targetNpc = otherNpcs[Game1.random.Next(otherNpcs.Count)];
            return targetNpc.Tile;
        }

        private static void ApplyNaturalNpcWarpOffset(NPC npc)
        {
            if (npc == null)
                return;

            int maxOffsetX = Math.Max(2, Game1.tileSize / 6);
            int maxOffsetY = Math.Max(2, Game1.tileSize / 8);

            float offsetX = Game1.random.Next(-maxOffsetX, maxOffsetX + 1);
            float offsetY = Game1.random.Next(-maxOffsetY, maxOffsetY + 1);
            npc.Position += new Vector2(offsetX, offsetY);
        }

        private static int GetFacingDirectionToward(Vector2 fromTile, Vector2 toTile)
        {
            float dx = toTile.X - fromTile.X;
            float dy = toTile.Y - fromTile.Y;

            if (Math.Abs(dx) < 0.001f && Math.Abs(dy) < 0.001f)
                return 2;

            if (Math.Abs(dx) >= Math.Abs(dy))
                return dx >= 0 ? 1 : 3;

            return dy >= 0 ? 2 : 0;
        }

        private static bool TryFindRandomWalkableInteriorTile(GameLocation location, AreaData area, out Vector2 targetTile, HashSet<(int X, int Y)>? excludedTiles = null)
        {
            targetTile = Vector2.Zero;

            int minX = Math.Min(area.startX, area.endX) + 1;
            int maxX = Math.Max(area.startX, area.endX) - 1;
            int minY = Math.Min(area.startY, area.endY) + 1;
            int maxY = Math.Max(area.startY, area.endY) - 1;

            if (minX > maxX || minY > maxY)
                return false;

            int width = maxX - minX + 1;
            int height = maxY - minY + 1;
            int totalTileCount = width * height;
            int randomChecks = Math.Min(totalTileCount, 40);
            var checkedTiles = new HashSet<(int X, int Y)>();

            for (int i = 0; i < randomChecks; i++)
            {
                int tileX = Game1.random.Next(minX, maxX + 1);
                int tileY = Game1.random.Next(minY, maxY + 1);

                if (!checkedTiles.Add((tileX, tileY)))
                    continue;

                if (excludedTiles != null && excludedTiles.Contains((tileX, tileY)))
                    continue;

                if (!IsWalkableWarpTile(location, tileX, tileY) || !IsWalkableWarpTile(location, tileX, tileY - 1))
                    continue;

                targetTile = new Vector2(tileX, tileY);
                return true;
            }

            for (int tileY = minY; tileY <= maxY; tileY++)
            {
                for (int tileX = minX; tileX <= maxX; tileX++)
                {
                    if (checkedTiles.Contains((tileX, tileY)))
                        continue;

                    if (excludedTiles != null && excludedTiles.Contains((tileX, tileY)))
                        continue;

                    if (!IsWalkableWarpTile(location, tileX, tileY) || !IsWalkableWarpTile(location, tileX, tileY - 1))
                        continue;

                    targetTile = new Vector2(tileX, tileY);
                    return true;
                }
            }

            return false;
        }

        private static bool IsWalkableWarpTile(GameLocation location, int tileX, int tileY)
        {
            var tile = new Vector2(tileX, tileY);
            if (!location.isTileOnMap(tile))
            {
                Game1.chatBox.addErrorMessage($"Tile {tile} is not on the map.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(location.doesTileHaveProperty(tileX, tileY, "Water", "Back")))
            {
                Game1.chatBox.addErrorMessage($"Tile {tile} has Water property.");
                return false;
            }

            if (HasBuildingLayerTile(location, tileX, tileY))
            {
                Game1.chatBox.addErrorMessage($"Tile {tile} has a building layer tile.");
                return false;
            }

            if (location.objects != null && location.objects.ContainsKey(tile))
            {
                Game1.chatBox.addErrorMessage($"Tile {tile} has an object.");
                return false;
            }

            if (!location.isTileLocationOpen(tile))
            {
                Game1.chatBox.addErrorMessage($"Tile {tile} is not open.");
                return false;
            }

            if (!location.isTilePassable(new Location(tileX, tileY), Game1.viewport))
            {
                Game1.chatBox.addErrorMessage($"Tile {tile} is not passable.");
                return false;
            }

            if (location.characters != null && location.characters.Any(c => c.Tile == tile))
            {
                Game1.chatBox.addErrorMessage($"Tile {tile} has a character on it.");
                return false;
            }

            if (!location.CanSpawnCharacterHere(tile))
            {
                Game1.chatBox.addErrorMessage($"Tile {tile} cannot spawn character.");
                return false;
            }

            if (location.terrainFeatures != null && location.terrainFeatures.ContainsKey(tile))
            {
                var feature = location.terrainFeatures[tile];
                Game1.chatBox.addErrorMessage($"Tile {tile} has terrain feature {feature.GetType().Name}.");
                if (feature is Tree || feature is LargeTerrainFeature)
                {
                    Game1.chatBox.addErrorMessage($"Tile {tile} has a tree or large terrain feature.");
                    return false;
                }
            }

            return true;
        }

        private static bool HasBuildingLayerTile(GameLocation location, int x, int y)
        {
            var buildingLayer = location.map?.GetLayer("Buildings");
            if (buildingLayer != null && x >= 0 && y >= 0 && x < buildingLayer.LayerWidth && y < buildingLayer.LayerHeight)
            {
                return buildingLayer.Tiles[x, y] != null;
            }

            return false;
        }

    }
}
