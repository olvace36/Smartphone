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

namespace Smartphone
{
    /// <summary>The mod entry point.</summary>
    public partial class ModEntry
    {
        private const int PhotoCaptureWidth = 520;
        private const int PhotoCaptureHeight = 705;

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


        private void OnRendered(object sender, RenderedEventArgs e)
        {
            if (!takeScreenshot)
                return;
            
            takeScreenshot = false;

            GraphicsDevice graphics = Game1.graphics.GraphicsDevice;

            int backBufferWidth = graphics.PresentationParameters.BackBufferWidth;
            int backBufferHeight = graphics.PresentationParameters.BackBufferHeight;

            // Create a RenderTarget the same size as the back buffer
            using RenderTarget2D copyTarget = new RenderTarget2D(graphics, backBufferWidth, backBufferHeight);

            // Copy current back buffer into our render target
            graphics.SetRenderTarget(copyTarget);
            graphics.Clear(Color.Black);

            // Draw the backbuffer content into the render target
            graphics.SetRenderTarget(null);
            Color[] rawData = new Color[backBufferWidth * backBufferHeight];
            graphics.GetBackBufferData(rawData);

            // Crop region from the phone camera viewport.
            int cropX = currentMenuX + 40;
            int cropY = currentMenuY + 190;
            int cropWidth = PhotoCaptureWidth;
            int cropHeight = PhotoCaptureHeight;

            // Ensure bounds don't exceed original image size
            cropX = Math.Clamp(cropX, 0, backBufferWidth - cropWidth);
            cropY = Math.Clamp(cropY, 0, backBufferHeight - cropHeight);

            Microsoft.Xna.Framework.Rectangle captureBounds = new Microsoft.Xna.Framework.Rectangle(cropX, cropY, cropWidth, cropHeight);

            // Extract pixel data from the cropped region
            Color[] croppedData = new Color[cropWidth * cropHeight];
            for (int y = 0; y < cropHeight; y++)
            {
                for (int x = 0; x < cropWidth; x++)
                {
                    int srcIndex = (cropY + y) * backBufferWidth + (cropX + x);
                    int destIndex = y * cropWidth + x;
                    croppedData[destIndex] = rawData[srcIndex];
                }
            }

            // Create cropped texture
            using Texture2D croppedTexture = new Texture2D(graphics, cropWidth, cropHeight);
            croppedTexture.SetData(croppedData);

            SaveCapturedPhoto(croppedTexture, Game1.currentLocation?.Name, BuildImageTags(captureBounds).ToList());
        }


        private void CaptureNpcPhoto(NPC npc)
        {
            if (!Context.IsWorldReady || npc == null || npc.currentLocation == null || Game1.graphics?.GraphicsDevice == null || Game1.game1 == null)
                return;

            GraphicsDevice graphics = Game1.graphics.GraphicsDevice;
            GameLocation targetLocation = npc.currentLocation;
            var renderStateSnapshot = new PhotoRenderStateSnapshot();
            var captureBounds = new Microsoft.Xna.Framework.Rectangle(0, 0, PhotoCaptureWidth, PhotoCaptureHeight);
            List<string> tags = new List<string>();

            using RenderTarget2D renderTarget = new RenderTarget2D(graphics, PhotoCaptureWidth, PhotoCaptureHeight);

            try
            {
                Game1.currentLocation = targetLocation;
                Game1.viewport = BuildNpcCaptureViewport(npc, targetLocation);
                PrepareLocationRenderState(targetLocation);

                graphics.SetRenderTarget(renderTarget);
                graphics.Clear(Color.Black);

                Game1.game1.DrawWorld(Game1.currentGameTime, renderTarget);
                tags = BuildImageTags(captureBounds).ToList();
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Failed to capture off-screen NPC photo for {npc.Name}: {ex}", LogLevel.Error);
                return;
            }
            finally
            {
                graphics.SetRenderTarget(null);
                renderStateSnapshot.Restore();
            }

            if (!tags.Contains($"has {npc.Name}", StringComparer.OrdinalIgnoreCase))
                tags.Add($"has {npc.Name}");

            SaveCapturedPhoto(renderTarget, targetLocation.Name, tags);
        }

        private void SaveCapturedPhoto(Texture2D capturedTexture, string? locationName, IEnumerable<string> tags)
        {
            string folderPath = Path.Combine(Helper.DirectoryPath, "userdata", GetCurrentSaveFolderName(), "image");
            Directory.CreateDirectory(folderPath);

            string resolvedLocationName = string.IsNullOrWhiteSpace(locationName)
                ? "UnknownLocation"
                : locationName;

            string filename = $"{resolvedLocationName}-{Game1.currentSeason}-Y{Game1.year}-D{Game1.dayOfMonth:D2}_{Game1.random.Next(10000, 99999)}.png";
            string path = Path.Combine(folderPath, filename);

            using Texture2D opaqueTexture = CreateOpaqueTexture(capturedTexture);
            using FileStream fs = new FileStream(path, FileMode.Create);
            opaqueTexture.SaveAsPng(fs, opaqueTexture.Width, opaqueTexture.Height);

            SetImageTags(filename, (tags ?? Enumerable.Empty<string>()).ToList());
            Game1.addHUDMessage(new HUDMessage("Photo saved!", HUDMessage.newQuest_type));
        }

        private static xTile.Dimensions.Rectangle BuildNpcCaptureViewport(NPC npc, GameLocation location)
        {
            Microsoft.Xna.Framework.Rectangle npcBounds = npc.GetBoundingBox();
            object? map = GetMemberValue(location, "Map", "map");

            int mapWidth = map == null ? -1 : TryReadIntMember(map, "DisplayWidth", "displayWidth");
            int mapHeight = map == null ? -1 : TryReadIntMember(map, "DisplayHeight", "displayHeight");

            mapWidth = Math.Max(PhotoCaptureWidth, mapWidth);
            mapHeight = Math.Max(PhotoCaptureHeight, mapHeight);

            int viewX = npcBounds.Center.X - (PhotoCaptureWidth / 2);
            int viewY = npcBounds.Center.Y - (PhotoCaptureHeight / 2);

            viewX = Math.Clamp(viewX, 0, Math.Max(0, mapWidth - PhotoCaptureWidth));
            viewY = Math.Clamp(viewY, 0, Math.Max(0, mapHeight - PhotoCaptureHeight));

            return new xTile.Dimensions.Rectangle(viewX, viewY, PhotoCaptureWidth, PhotoCaptureHeight);
        }

        private sealed class PhotoRenderStateSnapshot
        {
            private readonly xTile.Dimensions.Rectangle viewport;
            private readonly GameLocation currentLocation;
            private readonly Color ambientLight;
            private readonly Color outdoorLight;
            private readonly Dictionary<string, LightSource> lightSources = new Dictionary<string, LightSource>(StringComparer.Ordinal);

            public PhotoRenderStateSnapshot()
            {
                viewport = Game1.viewport;
                currentLocation = Game1.currentLocation;
                ambientLight = Game1.ambientLight;
                outdoorLight = Game1.outdoorLight;

                foreach (KeyValuePair<string, LightSource> lightSource in Game1.currentLightSources)
                    lightSources[lightSource.Key] = lightSource.Value;
            }

            public void Restore()
            {
                Game1.viewport = viewport;
                Game1.currentLocation = currentLocation;
                Game1.ambientLight = ambientLight;
                Game1.outdoorLight = outdoorLight;

                Game1.currentLightSources.Clear();
                foreach (KeyValuePair<string, LightSource> lightSource in lightSources)
                    Game1.currentLightSources[lightSource.Key] = lightSource.Value;
            }
        }

        private static void PrepareLocationRenderState(GameLocation targetLocation)
        {
            Game1.currentLightSources.Clear();

            RefreshAmbientLightForCapture(targetLocation);
            AddMapLightsForCapture(targetLocation);
            AddSharedLightsForCapture(targetLocation);
        }

        private static void RefreshAmbientLightForCapture(GameLocation targetLocation)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo? updateAmbientLighting = typeof(GameLocation).GetMethod("_updateAmbientLighting", flags);
            if (updateAmbientLighting != null)
            {
                updateAmbientLighting.Invoke(targetLocation, null);
                return;
            }

            if (targetLocation.IsOutdoors && !targetLocation.ignoreOutdoorLighting.Value)
                Game1.ambientLight = targetLocation.IsRainingHere() ? new Color(255, 200, 80) : Color.White;
        }

        private static void AddMapLightsForCapture(GameLocation targetLocation)
        {
            string lightIdPrefix = $"{targetLocation.NameOrUniqueName}_MapLight_";
            AddMapPropertyLights(targetLocation, lightIdPrefix, "Light", LightSource.LightContext.MapLight);

            if (!Game1.isTimeToTurnOffLighting(targetLocation) && !targetLocation.IsRainingHere())
            {
                AddMapPropertyLights(targetLocation, lightIdPrefix, "WindowLight", LightSource.LightContext.WindowLight);

                foreach (Vector2 lightGlow in targetLocation.lightGlows)
                {
                    Game1.currentLightSources.Add(new LightSource(
                        $"{lightIdPrefix}_{lightGlow.X}_{lightGlow.Y}_Glow",
                        6,
                        lightGlow,
                        1f,
                        LightSource.LightContext.WindowLight,
                        0L,
                        targetLocation.NameOrUniqueName));
                }
            }
        }

        private static void AddMapPropertyLights(GameLocation targetLocation, string lightIdPrefix, string propertyName, LightSource.LightContext context)
        {
            string[] propertyValues = targetLocation.GetMapPropertySplitBySpaces(propertyName);
            for (int i = 0; i + 2 < propertyValues.Length; i += 3)
            {
                if (!int.TryParse(propertyValues[i], out int tileX)
                    || !int.TryParse(propertyValues[i + 1], out int tileY)
                    || !int.TryParse(propertyValues[i + 2], out int textureIndex))
                {
                    continue;
                }

                Vector2 position = new Vector2((tileX * 64) + 32, (tileY * 64) + 32);
                string suffix = context == LightSource.LightContext.WindowLight ? "_Window" : string.Empty;

                Game1.currentLightSources.Add(new LightSource(
                    $"{lightIdPrefix}_{tileX}_{tileY}{suffix}",
                    textureIndex,
                    position,
                    1f,
                    context,
                    0L,
                    targetLocation.NameOrUniqueName));
            }
        }

        private static void AddSharedLightsForCapture(GameLocation targetLocation)
        {
            foreach (KeyValuePair<string, LightSource> sharedLight in targetLocation.sharedLights.Pairs)
                Game1.currentLightSources[sharedLight.Key] = sharedLight.Value;
        }

        private static Texture2D CreateOpaqueTexture(Texture2D source)
        {
            Color[] rawData = new Color[source.Width * source.Height];
            source.GetData(rawData);

            for (int i = 0; i < rawData.Length; i++)
            {
                Color pixel = rawData[i];
                if (pixel.A != byte.MaxValue)
                    rawData[i] = new Color(pixel.R, pixel.G, pixel.B, byte.MaxValue);
            }

            Texture2D opaqueTexture = new Texture2D(source.GraphicsDevice, source.Width, source.Height);
            opaqueTexture.SetData(rawData);
            return opaqueTexture;
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
        }



        private void OnSaving(object sender, SavingEventArgs e)
        {
            MessageManager.SaveData();
            NotificationManager.SaveNoticationData();
            SaveImageTags();

            if (phoneMenu != null)
                phoneMenu.ClosePhoneMenu();
            Helper.Data.WriteJsonFile($"./userdata/{Constants.SaveFolderName}/RecentEventMemory", RecentEvents);
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            LoadImageTags();

            string npc_characteristic = Helper.ModContent.GetInternalAssetName("assets/npc_characteristic.json").BaseName;
            NpcCharacteristics = Helper.ModContent.Load<Dictionary<string, string>>(npc_characteristic);

            string npcAgesAsset = Helper.ModContent.GetInternalAssetName("assets/npc_age.json").BaseName;
            npcAges = Helper.ModContent.Load<Dictionary<string, List<string>>>(npcAgesAsset);
            npcToAgeGroup = npcAges
                .SelectMany(kvp => kvp.Value.Select(npc => new { npc, group = kvp.Key }))
                .ToDictionary(x => x.npc, x => x.group);



            foreach (var npc in Utility.getAllCharacters())
            {
                if (!string.IsNullOrEmpty(npc.Birthday_Season) && npc.Birthday_Day > 0)
                {
                    var key = (npc.Birthday_Season, npc.Birthday_Day);
                    if (!NpcBirthdaysByDate.ContainsKey(key))
                        NpcBirthdaysByDate[key] = new List<NPC>();

                    NpcBirthdaysByDate[key].Add(npc);
                }
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
            CaptureNpcPhoto(Game1.getCharacterFromName("Abigail"));


            if (Game1.timeOfDay < 2300)
                CheckSendNewMessage();

            if (Config.OpenAIKey!= "" && e.NewTime >= 700 && e.NewTime % 100 == 0 && chatModel != "gpt-5.4-nano")
            {
                Task.Run(async () => 
                {
                    var (premium, regular) = await GetOpenAIUsage();
                    if (regular > 10000000)
                    {
                        chatModel = "gpt-5.4-nano";
                        summaryModel = "gpt-5.4-nano";
                    }
                });
            }

            if (pendingInitNotification && isPlayerFree())
            {
                Game1.drawLetterMessage("=== Smartphone ===^^It looks like this your first time here, or you have recently updated the mod and ]] LOST YOUR DATA [[^^" +
                    "Please note that your data, including conversation, summary, setting, memory, everything,... are saved in        $$/Mods/Smartphone/Userdata$$ folder.^    ]] YOU MUST COPY IT WHEN UPDATE THE MOD [[^^" +
                        "Thanks for trying out the mod, HaPyke +++");

                NotificationManager.addNotification("=== Smartphone ===^^It looks like this your first time here, or you have recently updated the mod and LOST YOUR DATA^^" +
                    "Please note that your data, including conversation, summary, setting, memory, everything,... are saved in /Mods/Smartphone/Userdata folder. YOU MUST COPY IT WHEN UPDATE THE MOD^^" +
                        "Thanks for trying out the mod, HaPyke");

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



        /// <summary>Call to OpenAI to generate a response</summary>
        public static async Task<string> SendMessageToAssistant(string npcName, string text = "", int textCount = 0, string type = "response")
        {
            NPC npc = Game1.getCharacterFromName(npcName);
            var random = Game1.random;
            if (npc is null)
            {
                return "SYSTEM: ---Got an error---";
            }

            if (true)
            {
                var key = k1 + k2 + k3;

                if (Config.OpenAIKey != "")
                    key = Config.OpenAIKey;

                var user = text;
                var system = "";

                if (text != "text")
                    system = GetSystemMessage(npc, type, textCount);
                else
                    system = GetSystemMessage(npc, type);

                string responseMessage = "";
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
                    var requestBody = new Dictionary<string, object>
                    {
                        { "model", chatModel },
                        { "input", new object[]
                            {
                                new
                                {
                                    role = "developer",
                                    content = new object[]
                                    {
                                        new { type = "input_text", text = system }
                                    }
                                },
                                new
                                {
                                    role = "user",
                                    content = new object[]
                                    {
                                        new { type = "input_text", text = user }
                                    }
                                }
                            }
                        },
                        { "text", new { format = new { type = "text" }, verbosity = "low" } },
                        { "reasoning", new { effort = "none", summary = "auto" } }
                    };
                    var tools = GetToolList(npc);
                    if (tools.Length > 0)
                    {
                        requestBody.Add("tools", tools);
                        requestBody.Add("tool_choice", "auto");
                    }

                    var jsonRequest = JsonConvert.SerializeObject(requestBody);
                    var httpContent = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                    var httpResponse = await httpClient.PostAsync("https://api.openai.com/v1/responses", httpContent);
                    if (httpResponse.IsSuccessStatusCode)
                    {
                        var jsonResponse = await httpResponse.Content.ReadAsStringAsync();



                        SMonitor.Log(jsonResponse.ToString(), LogLevel.Error);

                        JObject json = JObject.Parse(jsonResponse);
                        JArray toolCalls = GetResponseFunctionCalls(json);
                        responseMessage = GetResponseOutputText(json);

                        SMonitor.Log("-----", LogLevel.Error);
                        SMonitor.Log(system, LogLevel.Error);
                        SMonitor.Log("-----", LogLevel.Error);
                        SMonitor.Log(user, LogLevel.Error);
                        SMonitor.Log("-----", LogLevel.Error);
                        SMonitor.Log(responseMessage, LogLevel.Error);
                        SMonitor.Log("\n\n", LogLevel.Error);


                        if (toolCalls.Count > 0)
                        {
                            foreach (var call in toolCalls)
                            {
                                string functionName = call["name"]?.ToString() ?? string.Empty;
                                string argumentsJson = call["arguments"]?.ToString() ?? "{}";


                                if (functionName == "Schedule_Event")
                                {
                                    var args = JObject.Parse(argumentsJson);
                                    var eventNpcName = args["npc"]?.ToString();
                                    var eventType = args["event_type"]?.ToString();
                                    var eventTime = args["time_of_day"]?.ToString();
                                    var textResponse = args["npc_response"]?.ToString() ?? $"I will see you at {eventTime}.";

                                    if (string.IsNullOrWhiteSpace(eventNpcName)
                                        || string.IsNullOrWhiteSpace(eventType)
                                        || !TryNormalizeEventTime(eventTime, out string normalizedEventTime))
                                    {
                                        continue;
                                    }

                                    if (!TryGetRegisteredUnlimitedEvent(eventType, out var registeredEvent) || registeredEvent == null)
                                    {
                                        SMonitor.Log($"Schedule_Event ignored because event type '{eventType}' is not registered.", LogLevel.Warn);
                                        continue;
                                    }

                                    if (phoneMenu != null)
                                        phoneMenu.ClosePhoneMenu();

                                    Game1.activeClickableMenu = new ConfirmationDialog(
                                        $"Schedule {registeredEvent.DisplayName} event with {npc.Name} at {normalizedEventTime}?",
                                        onConfirm: (Farmer who) =>
                                        {
                                            var pendingEvent = (eventNpcName.Trim(), registeredEvent.EventType, normalizedEventTime);
                                            if (!PendingUnlimitedEvents.Contains(pendingEvent))
                                            {
                                                PendingUnlimitedEvents.Add(pendingEvent);
                                            }

                                            Game1.activeClickableMenu = null;
                                            MessageManager.AddMessage(npcName, $"{npcName}: {textResponse}");

                                        },
                                        onCancel: (Farmer who) =>
                                        {
                                            Game1.activeClickableMenu = null;
                                        }
                                    );
                                }
                                else if (functionName == "Send_Gift")
                                {
                                    var args = JObject.Parse(argumentsJson);
                                    string itemId = args["itemId"]?.ToString() ?? string.Empty;

                                    if (!string.IsNullOrEmpty(itemId))
                                        sendPlayerGift(itemId, npcName);

                                    if (phoneMenu != null)
                                        phoneMenu.ClosePhoneMenu();
                                }
                            }
                        }

                        if (responseMessage.StartsWith($"{npc.Name}:"))
                            responseMessage = responseMessage.Substring(npc.Name.Length + 1).TrimStart();
                        return responseMessage;
                    }
                    else
                    {
                        // Get the status code
                        var statusCode = (int)httpResponse.StatusCode; // Convert to int for switch
                        string errorMessage = "Check for mod update";
                        switch (statusCode)
                        {
                            case 403:
                                errorMessage = "Country, region, or territory not supported.";
                                break;
                            case 429:
                                errorMessage = "Please try again in a few minutes. If not work, then total AI usage for all players has passed the limit set by OpenAI. This will be reset the next day in timezone UTC+0";
                                break;
                            case 500:
                                errorMessage = "Server Error: The server had an issue while processing your request. Please try again.";
                                break;
                            case 503:
                                errorMessage = "Server Overload: The server is experiencing high traffic. Please try again later.";
                                break;
                        }

                        SMonitor.Log($"Unable to receive AI content. {statusCode}, {errorMessage}\n\n", LogLevel.Error);
                        return "SYSTEM: ---Got an error---";
                    }
                }
            }
        }

        private static string GetSystemMessage(NPC npc, string type, int textCount = 0)
        {
            try
            {
                string npcAge, npcManner, npcSocial, npcHeartLevel;

                int age = npc.Age;
                int manner = npc.Manners;
                int social = npc.SocialAnxiety;
                int heartLevel = 0;
                if (Game1.player.friendshipData.ContainsKey(npc.Name)) heartLevel = (int)Game1.player.friendshipData[npc.Name].Points / 250;

                npcAge = age == 0 ? "adult" : age == 1 ? "teens" : age == 2 ? "child" : "adult";
                npcManner = manner == 0 ? "a typical neutral manner" : manner == 1 ? "a polite and courteous manner" : manner == 2 ? "a distant and reserved manner" : "a typical neutral manner";
                npcSocial = social == 0 ? "an outgoing person" : social == 1 ? "a little shy person" : social == 2 ? "neither too outgoing nor shy" : "neither too outgoing nor shy";
                npcHeartLevel = heartLevel <= 2 ? ".0" : heartLevel <= 5 ? ".3" : ".6";

                string relation = heartLevel <= 2 ? "stranger" : heartLevel <= 4 ? "acquaintance" : heartLevel <= 6 ? "close friend" : "best friend";
                bool isDating = Game1.player.friendshipData.TryGetValue(npc.Name, out Friendship friendship) && friendship.IsDating();
                bool isMarried = friendship != null && friendship.IsMarried();
                if (isDating) relation = "dating";
                else if (isMarried) relation = "married";

                string playerLocation = Game1.player.currentLocation.Name;
                string npcLocation = npc.currentLocation.Name;
                if (Game1.CurrentEvent != null && Game1.CurrentEvent.isFestival)
                    playerLocation = npcLocation = Game1.CurrentEvent.FestivalName;
                else if (npcLocation == npc.DefaultMap)
                    npcLocation = $"{npc.Name} home";

                int time = Game1.timeOfDay;
                int hour24 = (time / 100) % 24;
                int minute = time % 100;
                string period = hour24 >= 12 ? "PM" : "AM";
                int hour12 = hour24 % 12;
                if (hour12 == 0) hour12 = 12;
                string timeFormatted = $"{hour12}:{minute:00} {period}";


                string planting = (FarmCropNames.Count + FarmTreeNames.Count) == 0
                    ? ""
                    : (Game1.random.NextBool() && FarmCropNames.Count > 0
                        ? FarmCropNames[Game1.random.Next(FarmCropNames.Count)]
                        : FarmTreeNames.Count > 0
                            ? FarmTreeNames[Game1.random.Next(FarmTreeNames.Count)]
                            : FarmCropNames[Game1.random.Next(FarmCropNames.Count)]);



                string data = @$"Player location: {playerLocation}; {npc.Name} location: {npcLocation}; Current time: {timeFormatted}; Today weather: {Game1.currentLocation.GetWeather().Weather}; Tomorrow weather: {Game1.weatherForTomorrow}; Day of months: {Game1.dayOfMonth}; Current season: {Game1.currentLocation.GetSeason()};";

                if (planting != "")
                    data += $"Player planting some {planting} on the farm";
                else
                    data += "Player not planting any crop on the farm";

                string latestGift = "";
                var dayAgo = 0;
                if (GiftMemories.TryGetValue(npc.Name, out GiftMemory memory))
                {
                    latestGift = memory.GiftName;
                    dayAgo = 3 - memory.DaysRemaining;
                }

                if (latestGift != "")
                    data += $"\nPlayer gifted {npc.Name} a {latestGift} {dayAgo} days ago.";

                foreach (var recentEvent in RecentEvents)
                {
                    data += $"\n{recentEvent.Description}";
                }


                var npcBirthdayTmr = GetNpcsWithBirthdayToday();
                if (npcBirthdayTmr.Count > 0)
                {
                    string birthdayNames = "NPC " + string.Join(", ", npcBirthdayTmr.Select(n => n.Name)) + " having birthday.";
                    data += $"\n{birthdayNames}";
                }



                var messages_history = npcMessagesToday.ContainsKey(npc.Name) ? npcMessagesToday[npc.Name] : new List<string>();
                string combined = string.Join("\n", messages_history
                    .Skip(Math.Max(0, messages_history.Count - 12 + textCount))
                    .Take(Math.Min(12, messages_history.Count - textCount)));

                string summary = "";
                if (npcConversationSummary.ContainsKey(npc.Name))
                    summary = npcConversationSummary[npc.Name];

                var maxCharacteristicCharacterCount = 1300;

                if (Config.OpenAIKey != "" && Config.MaxCharacteristicCharacterCount != 0)
                    maxCharacteristicCharacterCount = Config.MaxCharacteristicCharacterCount;

                var npcCharacteristic = $" {npc.Name} is {npcAge}, {npcManner}, and is {npcSocial}";

                if (NpcCharacteristics.ContainsKey(npc.Name))
                {
                    npcCharacteristic = NpcCharacteristics[npc.Name];

                    if (npcCharacteristic.Length > maxCharacteristicCharacterCount)
                    {
                        npcCharacteristic = npcCharacteristic.Substring(0, maxCharacteristicCharacterCount);
                    }
                }

                if (type == "response")
                {
                    var systemMessage = $@"You are an AI roleplaying as NPC {npc.Name} in Stardew Valley, texting the PLAYER {Game1.player.Name} ({Game1.player.Gender}, teen/young adult).
                        Current relationship with player: {relation}.
                        Personality: {npcCharacteristic}

                        WORLD CONTEXT:
                        {data}
                        Conversation Summary: {summary}

                        Current conversation:
                        {combined}

                        YOUR DIRECTIVES:
                        1. Reply naturally as {npc.Name} in =40 words, matching their tone and relationship with the player.
                        2. SLICE OF LIFE: Whenever natural, invent small, mundane details happening or happened around you. It can be anything that related to the context of the game.
                        3. EVENT NEGOTIATION: If the player mentions hanging out event (picnic, dinner, campfire, birthday, ...), confirm Player to specify or you suggest a Time of Day for the event that is between now and before 11:00 PM. 
                        4. TRIGGERING TOOLS: Once an activity AND a time are specified and agreed, call the appropriate function.
                        5. SENDING GIFTS: If you want to surprise the player with an item or the PLAYER specify request a gift, call the Send_Mail_Gift tool and text them that you are sending something via the mail.";

                    return systemMessage;
                }
                else
                {
                    var systemMessage = $@"You are an AI roleplaying as NPC {npc.Name} in Stardew Valley. Send a spontaneous text message to the PLAYER {Game1.player.Name} ({Game1.player.Gender}, teen/young adult) to start a new conversation.
                        Relationship: {relation}.
                        Personality: {npcCharacteristic}

                        WORLD CONTEXT:
                        {data}
                        Relationship Summary: {summary}

                        YOUR DIRECTIVES:
                        1. Keep the text under 40 words. Be creative, in-character, and match your relationship level.
                        2. SLICE OF LIFE: Use your current location, the time ({timeFormatted}), and the weather to mention something mundane or interesting happening right now (e.g., ""The wind is howling outside my house,"" or ""I'm incredibly bored at the shop today"").
                        3. DO NOT act like an AI assistant. Act exactly like a real person sending a casual text.
                        4. SENDING GIFTS: If you want to surprise the player with an item, call the Send_Gift tool and text them that you are sending something via the mail.";

                    return systemMessage;
                }
            }
            catch (Exception e)
            {
                return "";
            }
        }



        /// <summary> Summary the conversation between a NPC and player </summary>
        public static async Task<string> SummaryConversation(string npcName, string messageList)
        {
            NPC npc = Game1.getCharacterFromName(npcName);
            var random = Game1.random;
            if (npc is null)
                return "error";

            if (true)
            {
                string summary = "";
                if (npcConversationSummary.ContainsKey(npcName))
                    summary = npcConversationSummary[npcName];


                var key = k1 + k2 + k3;
                var maxAiSummaryLength = 350;
                if (Config.OpenAIKey != "" && Config.MaxSummaryWordCount != 0)
                {
                    maxAiSummaryLength = Config.MaxSummaryWordCount;
                    key = Config.OpenAIKey;
                }

                var system = $"You are the memory manager for the NPC {npcName} in Stardew Valley. "
                + "Your job is to read the previous memory summary and today's new conversation with the PLAYER, and summarize them into an updated memory bank. "
                + "Focus on retaining factual lore such as the player's preferences, recent gifts, important life events, current quests or goals, and most important the current emotional standing and relationship between the NPC and the PLAYER. "
                + $"Keep the summary under {maxAiSummaryLength} words. Remove outdated pleasantries, trivial daily greetings, or resolved minor topics, and just like human, some memory could be faded. "
                + "Be concise, do not include header, irelevant or no value information.";

                var user = $"Update the memory summary for {npcName} based on today's conversation.\n\n"
                    + "PREVIOUS SUMMARY:\n" + summary + "\n"
                    + "TODAY'S NEW CONVERSATION:\n" + messageList;




                var model = summaryModel;
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
                    var requestBody = new
                    {
                        model = model,
                        input = new object[]
                        {
                            new
                            {
                                role = "developer",
                                content = new object[]
                                {
                                    new { type = "input_text", text = system }
                                }
                            },
                            new
                            {
                                role = "user",
                                content = new object[]
                                {
                                    new { type = "input_text", text = user }
                                }
                            },
                        },
                        text = new { format = new { type = "text" }, verbosity = "medium" },
                        reasoning = new { effort = "low", summary = "auto" }
                    };

                    var jsonRequest = JsonConvert.SerializeObject(requestBody);
                    var httpContent = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                    var httpResponse = await httpClient.PostAsync("https://api.openai.com/v1/responses", httpContent);
                    if (httpResponse.IsSuccessStatusCode)
                    {
                        var jsonResponse = await httpResponse.Content.ReadAsStringAsync();

                        JObject json = JObject.Parse(jsonResponse);
                        var responseMessage = GetResponseOutputText(json);

                        //Game1.chatBox.addErrorMessage(jsonResponse.ToString());

                        if (responseMessage.StartsWith($"{npc.Name}:"))
                            responseMessage = responseMessage.Substring(npc.Name.Length + 1).TrimStart();

                        //Game1.chatBox.addErrorMessage(user);
                        //Game1.chatBox.addErrorMessage("-----");
                        //Game1.chatBox.addErrorMessage(system);
                        //Game1.chatBox.addErrorMessage("-----");
                        //Game1.chatBox.addErrorMessage(responseMessage);
                        //Game1.chatBox.addErrorMessage("\n\n");
                        return responseMessage;
                    }
                    else
                    {
                        // Get the status code
                        var statusCode = (int)httpResponse.StatusCode; // Convert to int for switch
                        string errorMessage = "Check for mod update";
                        switch (statusCode)
                        {
                            case 403:
                                errorMessage = "Country, region, or territory not supported.";
                                break;
                            case 429:
                                errorMessage = "Please try again in a few minutes. If not work, then total AI usage for all players has passed the limit set by OpenAI. This will be reset the next day in timezone UTC+0";
                                break;
                            case 500:
                                errorMessage = "Server Error: The server had an issue while processing your request. Please try again.";
                                break;
                            case 503:
                                errorMessage = "Server Overload: The server is experiencing high traffic. Please try again later.";
                                break;
                        }

                        SMonitor.Log($"Unable to receive AI content. {errorMessage}\n\n", LogLevel.Error);
                        return "SYSTEM: ---Got an error---";
                    }
                }
            }
        }

        public static object[] GetToolList(NPC npc)
        {
            int heartLevel = 0;
            if (Game1.player.friendshipData.ContainsKey(npc.Name)) heartLevel = (int)Game1.player.friendshipData[npc.Name].Points / 250;

            var functionList = new List<object>();


            if (heartLevel >= 3)
            {
                List<Item> cookingItems = new();
                HashSet<string> universalHates = new HashSet<string>();
                HashSet<string> universalDislikes = new HashSet<string>();

                foreach (var entry in Game1.NPCGiftTastes)
                {
                    if (entry.Key == "Universal_Hate")
                    {
                        foreach (string id in entry.Value.Split(' '))
                            universalHates.Add(id);
                    }
                    else if (entry.Key == "Universal_Dislike")
                    {
                        foreach (string id in entry.Value.Split(' '))
                            universalDislikes.Add(id);
                    }
                }

                foreach (var pair in Game1.objectData)
                {
                    var baseData = pair.Value;
                    string baseId = pair.Key;


                    if ((baseData.Category == -26 || baseData.Category == -7) && baseData.Price >= 150 && baseData.Price <= 1000
                        && !baseData.Name.Contains("Pickled") && !baseData.Name.Contains("Elixir") && !baseData.Name.Contains("Roe") && !baseData.Name.Contains("Mayonnaise") && !baseData.Name.Contains("Smoked") && !baseData.Name.Contains("Oil")
                        && !baseData.Name.Contains("Jelly") && !baseData.Name.Contains("Honey") && !baseData.Name.Contains("Wine") && !baseData.Name.Contains("Dried") && !baseData.Name.Contains("Juice")
                        && !universalHates.Contains(baseId) && !universalDislikes.Contains(baseId))
                    {
                        var item = new StardewValley.Object(baseId, 1);
                        cookingItems.Add(item);
                    }

                }
                Item randomItem = null;
                Random rng = new Random();

                if (cookingItems.Count > 0)
                {
                    int index = rng.Next(cookingItems.Count);
                    randomItem = cookingItems[index];
                }
                functionList.Add(
                    new
                    {
                        type = "function",
                        name = "Send_Gift",
                        description = $"Trigger this when you want to send a gift of {randomItem?.Name} to the player.",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                npc = new { type = "string", description = "Name of the NPC" },
                                itemId = new
                                {
                                    type = "string",
                                    description = "Qualified Item ID of the gift",
                                    @enum = new[] { randomItem?.QualifiedItemId }
            }
                            },
                            required = new[] { "npc", "itemId" },
                            additionalProperties = false
                        },
                        strict = true
                    }
                );
            }



            var registeredEvents = GetRegisteredUnlimitedEventsForHeartLevel(heartLevel);

            if (registeredEvents.Count > 0)
            {
                var allowedEvents = registeredEvents
                    .Select(evt => evt.EventType)
                    .ToArray();
                var readableEventNames = string.Join(", ", registeredEvents.Select(evt => evt.DisplayName));
                var extraToolDescriptions = string.Join(
                    " ",
                    registeredEvents
                        .Select(evt => evt.ToolDescription)
                        .Where(text => !string.IsNullOrWhiteSpace(text))
                        .Distinct(StringComparer.OrdinalIgnoreCase));

                string scheduleToolDescription = "Trigger ONLY when you and the PLAYER have agreed to hang out today for one of the available event types AND a time between now and 11:00 PM is specified.";
                scheduleToolDescription += $" Available event types: {readableEventNames}.";
                if (!string.IsNullOrWhiteSpace(extraToolDescriptions))
                    scheduleToolDescription += $" {extraToolDescriptions}";

                functionList.Add(
                    new
                    {
                        type = "function",
                        name = "Schedule_Event",
                        description = scheduleToolDescription,
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                npc = new { type = "string", description = "Name of the NPC" },
                                event_type = new
                                {
                                    type = "string",
                                    description = "The specific type of event you both agreed to.",
                                    @enum = allowedEvents.ToArray()
                                },
                                time_of_day = new { type = "string", description = "The agreed time in HHMM format (e.g., '0900', '1730', '2150')" },
                                npc_response = new { type = "string", description = "A message from NPC to invite or confirm the event" }
                            },
                            required = new[] { "npc", "event_type", "time_of_day", "npc_response" },
                            additionalProperties = false
                        },
                        strict = true
                    }
                );
            }
            return functionList.ToArray();
        }

        private static JArray GetResponseFunctionCalls(JObject responseJson)
        {
            var calls = new JArray();
            var output = responseJson?["output"] as JArray;
            if (output == null)
                return calls;

            foreach (var item in output)
            {
                if (item?["type"]?.ToString() == "function_call")
                    calls.Add(item);
            }

            return calls;
        }

        private static string GetResponseOutputText(JObject responseJson)
        {
            var output = responseJson?["output"] as JArray;
            if (output != null)
            {
                foreach (var item in output.Reverse())
                {
                    if (item?["type"]?.ToString() != "message")
                        continue;

                    var content = item["content"] as JArray;
                    if (content == null)
                        continue;

                    foreach (var part in content)
                    {
                        if (part?["type"]?.ToString() == "output_text")
                        {
                            var text = part["text"]?.ToString();
                            if (!string.IsNullOrWhiteSpace(text))
                                return text;
                        }
                    }
                }
            }

            var fallbackText = responseJson?["output_text"]?.ToString();
            return fallbackText ?? string.Empty;
        }


        public static async Task<(int, int)> GetOpenAIUsage()
        {
            SMonitor.Log("Checking OpenAI usage...", LogLevel.Error);
            List<string> premiumModels = new List<string> { "gpt-5.4", "gpt-5.2", "gpt-5.1", "gpt-5.1-codex", "gpt-5", "gpt-5-codex", "gpt-5-chat-latest", "gpt-4.1", "gpt-4o", "o1", "o3" };
            List<string> regularModels = new List<string> { "gpt-5.4-mini", "gpt-5.4-nano", "gpt-5.1-codex-mini", "gpt-5-mini", "gpt-5-nano", "gpt-4.1-mini", "gpt-4.1-nano", "gpt-4o-mini",
                                                            "o1-mini", "o3-mini", "o4-mini", "codex-mini-latest" };
            string admin_key = xk1 + xk2 + xk3;
            string usageUrl = "https://api.openai.com/v1/organization/usage/completions";
            using HttpClient client = new HttpClient();

            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", admin_key);

            DateTime utcNow = DateTime.UtcNow;
            DateTime utcStartOfToday = utcNow.Date;
            long startTime = new DateTimeOffset(utcStartOfToday).ToUnixTimeSeconds();
            long endTime = new DateTimeOffset(utcNow).ToUnixTimeSeconds();

            try
            {
                var perModelTotals = new Dictionary<string, (long Input, long Output)>();
                string? nextPage = null;
                bool hasMore;

                do
                {
                    var query = $"start_time={startTime}&end_time={endTime}&group_by=model";
                    if (!string.IsNullOrWhiteSpace(nextPage))
                        query += $"&page={Uri.EscapeDataString(nextPage)}";

                    string requestUrl = $"{usageUrl}?{query}";
                    HttpResponseMessage response = await client.GetAsync(requestUrl);

                    if (!response.IsSuccessStatusCode)
                    {
                        string error = await response.Content.ReadAsStringAsync();
                        SMonitor.Log($"Error: {response.StatusCode} - {error}", LogLevel.Error);
                        return (-1, -1);
                    }

                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    JObject json = JObject.Parse(jsonResponse);

                    var data = json["data"] as JArray;
                    if (data != null)
                    {
                        foreach (var bucket in data)
                        {
                            var results = bucket?["results"] as JArray;
                            if (results == null)
                                continue;

                            foreach (var result in results)
                            {
                                string? model = result?["model"]?.ToString();
                                if (string.IsNullOrWhiteSpace(model))
                                    model = "unknown";

                                long inputTokens = result?["input_tokens"]?.Value<long>() ?? 0;
                                long outputTokens = result?["output_tokens"]?.Value<long>() ?? 0;

                                if (perModelTotals.TryGetValue(model, out var totals))
                                {
                                    perModelTotals[model] = (totals.Input + inputTokens, totals.Output + outputTokens);
                                }
                                else
                                {
                                    perModelTotals[model] = (inputTokens, outputTokens);
                                }
                            }
                        }
                    }

                    hasMore = json["has_more"]?.Value<bool>() ?? false;
                    nextPage = json["next_page"]?.ToString();
                }
                while (hasMore && !string.IsNullOrWhiteSpace(nextPage));

                //SMonitor.Log($"OpenAI usage window (UTC): {utcStartOfToday:yyyy-MM-dd HH:mm:ss} -> {utcNow:yyyy-MM-dd HH:mm:ss}", LogLevel.Info);
                if (perModelTotals.Count == 0)
                {
                    return (-1, -1);
                }

                //foreach (var item in perModelTotals.OrderByDescending(x => x.Value.Input + x.Value.Output))
                //{
                //    SMonitor.Log($"Model: {item.Key} | Input: {item.Value.Input} | Output: {item.Value.Output}", LogLevel.Info);
                //}

                // Aggregate totals for premium and regular model groups
                long premiumInputTotal = 0, premiumOutputTotal = 0;
                long regularInputTotal = 0, regularOutputTotal = 0;

                foreach (var kv in perModelTotals)
                {
                    var modelName = kv.Key ?? string.Empty;
                    var input = kv.Value.Input;
                    var output = kv.Value.Output;

                    // Prefer matching regular (specific) names first, then premium.
                    bool isRegular = regularModels.Any(rm =>
                        modelName.StartsWith(rm, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(rm, modelName, StringComparison.OrdinalIgnoreCase)
                    );
                    bool isPremium = !isRegular && premiumModels.Any(pm =>
                        modelName.StartsWith(pm, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(pm, modelName, StringComparison.OrdinalIgnoreCase)
                    );

                    if (isRegular)
                    {
                        regularInputTotal += input;
                        regularOutputTotal += output;
                    }
                    else if (isPremium)
                    {
                        premiumInputTotal += input;
                        premiumOutputTotal += output;
                    }
                }

                //SMonitor.Log($"Premium Models Total | Input: {premiumInputTotal} | Output: {premiumOutputTotal}", LogLevel.Info);
                //SMonitor.Log($"Regular Models Total | Input: {regularInputTotal} | Output: {regularOutputTotal}", LogLevel.Info);

                return ((int)(premiumInputTotal + premiumOutputTotal), (int)(regularInputTotal + regularOutputTotal));
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Request failed: {ex.Message}", LogLevel.Error);
                return (-1, -1);
            }
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
    }
}
