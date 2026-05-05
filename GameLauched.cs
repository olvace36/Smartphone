using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ContentPatcher;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
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
using StardewValley.GameData.LocationContexts;
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
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.GameLoop.GameLaunched += OnGameLauched;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.Saving += OnSaving;
            helper.Events.GameLoop.DayEnding += OnDayEnding;
            helper.Events.GameLoop.TimeChanged += OnTimeChange;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.OneSecondUpdateTicked += OnOneSecondUpdateTicked;
            helper.Events.Player.Warped += OnWarped;
            helper.Events.Display.MenuChanged += OnMenuChanged;
            helper.Events.Display.WindowResized += OnWindowResized;
            helper.Events.Display.Rendered += OnRendered;
            helper.Events.Multiplayer.ModMessageReceived += OnModMessageReceived;


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
                MessageManager.MarkPhoneOpenedToday();
            }

            // DEVTOOL
            // if (e.Button == SButton.O && Game1.activeClickableMenu == null && true)
            // {
            //     isGridVisible = !isGridVisible;
            //     Game1.chatBox.addInfoMessage($"Grid {(isGridVisible ? "enabled" : "disabled")}.");
            //     ToggleGrid(e);
            //     firstClickTile = null;
            //     return;
            // }
            // if (e.Button == SButton.MouseLeft)
            // {
            //     var tile = e.Cursor.Tile;
            //     Game1.chatBox.addErrorMessage((IsWalkableWarpTile(Game1.currentLocation, (int)tile.X, (int)tile.Y) && IsWalkableWarpTile(Game1.currentLocation, (int)tile.X, (int)tile.Y - 1)).ToString());
            // }
        }


        private void OnSaving(object sender, SavingEventArgs e)
        {
            MessageManager.SaveData();
            NotificationManager.SaveNoticationData();
            StardewConnectManager.SaveData();
            SaveImageTags();

            if (phoneMenu != null)
                phoneMenu.ClosePhoneMenu();
            Helper.Data.WriteJsonFile($"./userdata/{Constants.SaveFolderName}/recent_event_memory", RecentEvents);
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            npcConversationSummary = Helper.Data.ReadJsonFile<Dictionary<string, string>>($"./userdata/{Constants.SaveFolderName}/npcConversationSummary")
                   ?? new Dictionary<string, string>();

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
            UpdatePostInteractionLimit();
            PhoneDialogueRuntime.ClearDailyState();
            StardewConnectManager.LoadData();

            string npc_characteristic_minimal = Helper.ModContent.GetInternalAssetName("assets/npc_characteristics_minimal.json").BaseName;
            string npc_characteristic_short = Helper.ModContent.GetInternalAssetName("assets/npc_characteristics_short.json").BaseName;
            string npc_characteristic_long = Helper.ModContent.GetInternalAssetName("assets/npc_characteristics_long.json").BaseName;

            NpcCharacteristicsMinimal = Helper.ModContent.Load<Dictionary<string, string>>(npc_characteristic_minimal)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => TruncateCharacteristicValue(kvp.Value, 400));

            NpcCharacteristicsShort = Helper.ModContent.Load<Dictionary<string, string>>(npc_characteristic_short)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => TruncateCharacteristicValue(kvp.Value, 800));

            NpcCharacteristicsLong = Helper.ModContent.Load<Dictionary<string, string>>(npc_characteristic_long)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => TruncateCharacteristicValue(kvp.Value, 1500));


            foreach (var npc in Utility.getAllVillagers()
            .OfType<NPC>()
                .Where(npc => npc.CanSocialize && !npc.IsInvisible && !socialNpcBlacklist.Contains(npc.Name, StringComparer.OrdinalIgnoreCase))
                .ToList())
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
                    bool hasNewerVersion = await CheckForNewerVersion(modInfo);

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

            InitializeSocialCoopOnSaveLoaded();
        }

        private static string TruncateCharacteristicValue(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || maxLength <= 0)
                return string.Empty;

            return value.Length <= maxLength
                ? value
                : value.Substring(0, maxLength);
        }



        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            // Reset daily variables
            ClearPendingRandomNpcSocialPost();
            ResetDailyAiUsageLimit();
            isTodayEventAdded = false;
            lastTimeReceiveMessage = 600;
            npcMessagesToday.Clear();
            PhoneDialogueRuntime.ClearDailyState();
            FarmCropNames.Clear();
            FarmTreeNames.Clear();
            ResetTriggeredUnlimitedEventTag();

            UpdateSocialPostLimit();
            MessageManager.LoadData();
            ApplySavedPhoneTheme();
            NotificationManager.LoadNoticationData();
            if (!IsFarmhandSocialPeer())
                StardewConnectManager.LoadData();
            HandlePhoneUsageInactivityOnDayStarted();
            HandleAiModelSettingTimeChanged(600);
            GiftMemories = Helper.Data.ReadJsonFile<Dictionary<string, GiftMemory>>($"./userdata/{Constants.SaveFolderName}/GiftMemoryData")
                   ?? new Dictionary<string, GiftMemory>();
            RecentEvents = Helper.Data.ReadJsonFile<List<RecentEvent>>($"./userdata/{Constants.SaveFolderName}/recent_event_memory")
                   ?? new List<RecentEvent>();


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

            if (ShouldHostRunSocialSimulation())
                PrepareDailyRandomNpcSocialPosts();

            // send conversationSummary to iModApi
            if (iUnlimitedEventExpansionApi != null)
                iUnlimitedEventExpansionApi.SendNpcConversationSummary(npcConversationSummary);
        }

        private void HandlePhoneUsageInactivityOnDayStarted()
        {
            IsAiDisabledForPhoneInactivityToday = false;
            return;

            if (!IsAiUsageLimitEnabled())
                return;

            int currentDayNumber = MessageManager.GetCurrentPhoneUsageDayNumber();
            if (currentDayNumber <= 0)
                return;

            int lastOpenedDay = Math.Max(0, MessageManager.lastPhoneOpenedDay);
            if (lastOpenedDay > currentDayNumber)
                lastOpenedDay = currentDayNumber;

            int daysSinceLastOpen = lastOpenedDay <= 0
                ? currentDayNumber
                : currentDayNumber - lastOpenedDay;

            if (daysSinceLastOpen >= 3)
            {
                IsAiDisabledForPhoneInactivityToday = true;
                NotificationManager.addNotification("=== Smartphone ===^^Seem like you are not using Smartphone. All AI are disabled until you come back.");
                return;
            }

            if (daysSinceLastOpen >= 2)
                NotificationManager.addNotification("=== Smartphone ===^^Many things are happening, remember to check your Smartphone.");
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
            PhoneMenu.ClearPendingQueuedChatReplies();
            ClearQueuedAiActions();

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
            var conversationsToSummarize = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in npcMessagesToday)
            {
                string npcName = kvp.Key;
                List<string> messages = kvp.Value
                    .Skip(Math.Max(0, kvp.Value.Count - 30))
                    .ToList();

                if (messages.Count == 0)
                    continue;

                conversationsToSummarize[npcName] = string.Join("\n", messages);
            }

            if (conversationsToSummarize.Count == 0)
                return;

            Task.Run(async () =>
            {
                try
                {
                    Dictionary<string, string> batchSummaries = await SummaryConversationsBatch(conversationsToSummarize, bypassAiLimit: true);
                    if (batchSummaries.Count == 0)
                        return;

                    foreach (var kvp in batchSummaries)
                        npcConversationSummary[kvp.Key] = kvp.Value;

                    Helper.Data.WriteJsonFile($"./userdata/{Constants.SaveFolderName}/npcConversationSummary", npcConversationSummary);
                }
                catch (Exception ex)
                {
                    SMonitor.Log($"Unable to update NPC conversation summaries in batch: {ex}", LogLevel.Trace);
                }
            });
        }

        private void OnTimeChange(object sender, TimeChangedEventArgs e)
        {

            HandleAiUsageTimeChanged(e.NewTime);
            HandleAiModelSettingTimeChanged(e.NewTime);

            if (ShouldHostRunSocialSimulation())
            {
                HandleScheduledSocialPostsOnTimeChanged(e.NewTime);

                if (GetSocialCommentEngagementIntervalFromConfig().Contains(e.NewTime))
                    QueueRandomNpcCommentEngagement();

                if (GetSocialLikeEngagementIntervalFromConfig().Contains(e.NewTime))
                    QueueRandomNpcLikeEngagement();
            }

            if (Game1.timeOfDay < 2200)
                CheckSendNewMessage();

            if (pendingInitNotification && isPlayerFree())
            {
                Game1.drawLetterMessage("=== Smartphone ===^^Looks like this is your first time here, or you have recently updated the mod and >> LOST YOUR DATA @@^^" +
                    "Please note that your data, including conversations, photos, StardewSocial, everything,... are saved in             $$/Mods/Smartphone/Userdata$$ folder.^     >> YOU MUST COPY IT WHEN UPDATE THE MOD @@^^                                      > continue >" +
                    "^Smartphone costs $real money$ to maintain. To keep this mod available for everyone, please use it responsibly!!!^^Really really really like the mod and like to help keep the lights on? Check out the mod page for ways to contribute.^^" +
                    "         Thanks for trying out the mod, HaPyke +++");

                NotificationManager.addNotification("=== Smartphone ===^^Looks like this is your first time here, or you have recently updated the mod and >> LOST YOUR DATA @@^^" +
                    "Please note that your data, including conversations, photos, StardewSocial, everything,... are saved in             $$/Mods/Smartphone/Userdata$$^     >> YOU MUST COPY IT WHEN UPDATE THE MOD @@^^" +
                    "Smartphone costs $real money$ to maintain. To keep this mod available for everyone, please use it responsibly!!!^^Really really really like the mod and like to help keep the lights on? Check out the mod page for ways to contribute.^^Thanks for trying out the mod, HaPyke");

                pendingInitNotification = false;
            }
        }

        private void OnWarped(object sender, WarpedEventArgs e)
        {
            if (!Context.IsWorldReady || e == null || !e.IsLocalPlayer)
                return;
        }

        private void OnMenuChanged(object sender, MenuChangedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;
        }

        private void OnWindowResized(object sender, WindowResizedEventArgs e)
        {
            if (phoneMenu == null)
                return;

            phoneMenu.ResetToDefaultPosition();
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


        private static async Task<(bool IsLatest, string? LatestVersion, string? LatestUrl)> CheckForModUpdate(IModInfo modInfo)
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

        public static async Task<bool> CheckForNewerVersion(IModInfo? modInfo)
        {
            if (modInfo?.Manifest == null)
                return false;

            var update = await CheckForModUpdate(modInfo);
            return !update.IsLatest;
        }



    }
}
