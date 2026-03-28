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
        //
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
            helper.Events.GameLoop.OneSecondUpdateTicked += OneOneSecondUpdateTicked;
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
            this.Monitor.Log("Loading Smartphone", LogLevel.Trace);
            var api = this.Helper.ModRegistry.GetApi<IContentPatcherAPI>("Pathoschild.ContentPatcher");

            ConfigMenu(api, this.ModManifest, Helper);

            iUnlimitedEventExpansionApi = SHelper.ModRegistry.GetApi<IUnlimitedEventExpansionApi>("d5a1lamdtd.UnlimitedEventExpansion");

            if (iUnlimitedEventExpansionApi == null)
            {
                Monitor.Log("Failed to get iUnlimitedEventExpansionApi API.", LogLevel.Warn);
                return;
            }

        }       // **** Config Handle ****


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

            // Crop region (520x720 from top-left point)
            int cropX = currentMenuX + 40;
            int cropY = currentMenuY + 190;
            int cropWidth = 520;
            int cropHeight = 705;

            // Ensure bounds don't exceed original image size
            cropX = Math.Clamp(cropX, 0, backBufferWidth - cropWidth);
            cropY = Math.Clamp(cropY, 0, backBufferHeight - cropHeight);

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
            Texture2D croppedTexture = new Texture2D(graphics, cropWidth, cropHeight);
            croppedTexture.SetData(croppedData);

            // Save cropped image
            string folderPath = Path.Combine(Helper.DirectoryPath, "userdata", Constants.SaveFolderName, "image");
            Directory.CreateDirectory(folderPath);

            string filename = $"{Game1.currentLocation.Name}-{Game1.currentSeason}-Y{Game1.year}-D{Game1.dayOfMonth:D2}_{Game1.random.Next(10000, 99999)}.png";

            string path = Path.Combine(folderPath, filename);
            using FileStream fs = new FileStream(path, FileMode.Create);
            croppedTexture.SaveAsPng(fs, cropWidth, cropHeight);


            Game1.addHUDMessage(new HUDMessage("Photo saved!", HUDMessage.newQuest_type));
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

            if (phoneMenu != null)
                phoneMenu.ClosePhoneMenu();
            Helper.Data.WriteJsonFile($"./userdata/{Constants.SaveFolderName}/RecentEventMemory", RecentEvents);
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
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

            MessageManager.LoadData();
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

            if (Game1.timeOfDay < 2300)
                CheckSendNewMessage();

            if (pendingInitNotification
                && Game1.timeOfDay > 600
                && Game1.player.CanMove
                && !(Game1.player.isRidingHorse()
                    || Game1.currentLocation == null
                    || Game1.eventUp
                    || Game1.isFestival()
                    || Game1.IsFading()
                    || Game1.activeClickableMenu != null
                    || Game1.dialogueUp
                    || Game1.player.UsingTool))
            {
                Game1.drawLetterMessage("=== Smartphone ===^^It looks like this your first time here, or you have recently updated the mod and ]] LOST YOUR DATA [[^^" +
                    "Please note that your data, including conversation, summary, setting, memory, everything,... are saved in        $$/Mods/Smartphone/Userdata$$ folder.^    ]] YOU MUST COPY IT WHEN UPDATE THE MOD [[^^" +
                        "Thanks for trying out the mod, HaPyke +++");

                NotificationManager.addNotication("=== Smartphone ===^^It looks like this your first time here, or you have recently updated the mod and LOST YOUR DATA^^" +
                    "Please note that your data, including conversation, summary, setting, memory, everything,... are saved in /Mods/Smartphone/Userdata folder. YOU MUST COPY IT WHEN UPDATE THE MOD^^" +
                        "Thanks for trying out the mod, HaPyke");

                pendingInitNotification = false;
            }
        }

        private void OneOneSecondUpdateTicked(object sender, OneSecondUpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            if (e.IsMultipleOf(120))
                CheckCurrentEvent();

        }



        /// <summary>Call to OpenAI to generate a response</summary>
        public static async Task<string> SendMessageToAssistant(string npcName, string text = "", int textCount = 0, string type = "response")
        {
            NPC npc = Game1.getCharacterFromName(npcName);
            var random = Game1.random;
            if (npc is null)
            {
                return "error";
            }

            if (true)
            {
                var key = k1 + k2 + k3;

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
                        { "model", "gpt-5.4-mini" },
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

                        //SMonitor.Log(system, LogLevel.Error);
                        //SMonitor.Log("-----", LogLevel.Error);
                        //SMonitor.Log(user, LogLevel.Error);
                        //SMonitor.Log("-----", LogLevel.Error);
                        //SMonitor.Log(responseMessage, LogLevel.Error);
                        //SMonitor.Log("\n\n", LogLevel.Error);


                        if (toolCalls.Count > 0)
                        {
                            foreach (var call in toolCalls)
                            {
                                string functionName = call["name"]?.ToString() ?? string.Empty;
                                string argumentsJson = call["arguments"]?.ToString() ?? "{}";

                                if (functionName == "Dinner_Event")
                                {

                                    if (phoneMenu != null)
                                        phoneMenu.ClosePhoneMenu();
                                    Game1.activeClickableMenu = new ConfirmationDialog(
                                        $"You are going for dinner with {npc.Name}",
                                        onConfirm: (Farmer who) =>
                                        {
                                            Game1.activeClickableMenu = null;
                                            var args = JObject.Parse(argumentsJson);
                                            iUnlimitedEventExpansionApi.TriggerDinnerEvent(args["npc"]?.ToString() ?? npc.Name);
                                            MessageManager.AddMessage(npcName, $"{npcName}: Yes. That sounds wonderfull.");

                                        },
                                        onCancel: (Farmer who) =>
                                        {
                                            Game1.activeClickableMenu = null;
                                        }
                                    );
                                }
                                else if (functionName == "NPC_Birthday_Event")
                                {
                                    if (phoneMenu != null)
                                        phoneMenu.ClosePhoneMenu();
                                    Game1.activeClickableMenu = new ConfirmationDialog(
                                        $"You are going to celebrate {npc.Name}'s birthday",
                                        onConfirm: (Farmer who) =>
                                        {
                                            Game1.activeClickableMenu = null;
                                            var args = JObject.Parse(argumentsJson);
                                            iUnlimitedEventExpansionApi.TriggerNpcBirthdayEvent(args["npc"]?.ToString() ?? npc.Name);
                                            MessageManager.AddMessage(npcName, $"{npcName}: Thank you so much!");

                                        },
                                        onCancel: (Farmer who) =>
                                        {
                                            Game1.activeClickableMenu = null;
                                        }
                                    );
                                }
                                else if (functionName == "Picnic_Event")
                                {
                                    if (phoneMenu != null)
                                        phoneMenu.ClosePhoneMenu();
                                    Game1.activeClickableMenu = new ConfirmationDialog(
                                        $"You are going for a picnic with {npc.Name}",
                                        onConfirm: (Farmer who) =>
                                        {
                                            Game1.activeClickableMenu = null;
                                            var args = JObject.Parse(argumentsJson);
                                            iUnlimitedEventExpansionApi.TriggerPicnicEvent(args["npc"]?.ToString() ?? npc.Name);
                                            MessageManager.AddMessage(npcName, $"{npcName}: Yes. That sounds wonderfull.");

                                        },
                                        onCancel: (Farmer who) =>
                                        {
                                            Game1.activeClickableMenu = null;
                                        }
                                    );
                                }
                                else if (functionName == "Campfire_Event")
                                {
                                    if (phoneMenu != null)
                                        phoneMenu.ClosePhoneMenu();
                                    Game1.activeClickableMenu = new ConfirmationDialog(
                                        $"You are going for a campfire night with {npc.Name}",
                                        onConfirm: (Farmer who) =>
                                        {
                                            Game1.activeClickableMenu = null;
                                            var args = JObject.Parse(argumentsJson);
                                            iUnlimitedEventExpansionApi.TriggerCampingEvent(args["npc"]?.ToString() ?? npc.Name);
                                            MessageManager.AddMessage(npcName, $"{npcName}: Yes. That sounds wonderfull.");

                                        },
                                        onCancel: (Farmer who) =>
                                        {
                                            Game1.activeClickableMenu = null;
                                        }
                                    );
                                }
                                if (phoneMenu != null)
                                    phoneMenu.ClosePhoneMenu();
                            }
                        }




                        if (responseMessage.StartsWith($"{npc.Name}:"))
                            responseMessage = responseMessage.Substring(npc.Name.Length + 1).TrimStart();




                        Game1.addHUDMessage(new HUDMessage($"A new message from {npcName}", HUDMessage.newQuest_type));
                        DelayedAction.playSoundAfterDelay(MessageManager.currentPhoneSound, 0);

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
                        return "an error";
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

                var npcCharacteristic = $" {npc.Name} is {npcAge}, {npcManner}, and is {npcSocial}";
                if (NpcCharacteristics.ContainsKey(npc.Name))
                {
                    npcCharacteristic = NpcCharacteristics[npc.Name];
                    var words = npcCharacteristic.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    if (words.Length > 150)
                    {
                        npcCharacteristic = string.Join(" ", words.Take(150));
                    }
                }
                if (type == "response")
                {
                    var systemMessage = $@"You are an AI assistant in game Stardew Valley. You will act as NPC {npc.Name}, texting with the PLAYER {Game1.player.Name} ({Game1.player.Gender}, teen/young adult, is {relation} to {npc.Name}).
                        First, base on the user message and the **current** conversation (not the summary), decide:
                        • If the conversation is pointing directly toward an action that matches a listed function, then reply with **only** the function call.
                        • Otherwise, answer as {npc.Name} in ≤40 words, matching their tone, characteristic and current relationship with PLAYER: {npcCharacteristic} 

                        This is the conversation you need to reply too: 
                            {combined}
                        Other context you may rely on:
                            – Relationship summary: {summary}
                            – World context: {data}

                        Remember: output EITHER a function call OR plain text, never both. Read the conversation carefully to decide if it match a function.
                    ";

                    return systemMessage;
                }
                else
                {
                    var systemMessage = $"You are an AI assistant specialized in generating conversation dialogue for NPC in game Stardew Valley." +
                            $" You will consider yourself as NPC {npc.Name}, send a text message to Player {Game1.player.Name} ({Game1.player.Gender}, teen/young adult) a message to start a new conversation or to share something.\n" +
                            $" Currently, {Game1.player.Name} is {relation} to {npc.Name}.\n" +
                            $" {npcCharacteristic} " +
                            " \nBe creative with the topic, and keep it within the context of the game. Limit to under 40 words." +
                            " \nThis is the summary of the conversation that they exchanged: " + summary +
                            " \nThere is some other contexts that you may choose to use:\n" + data;

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

                var system = $"You are the memory manager for the NPC {npcName} in Stardew Valley. "
                + "Your job is to read the previous memory summary and today's new conversation with the PLAYER, and summarize them into an updated memory bank. "
                + "Focus on retaining factual lore such as the player's preferences, recent gifts, important life events, current quests or goals, and most important the current emotional standing and relationship between the NPC and the PLAYER. "
                + "Keep the summary under 350 words. Remove outdated pleasantries, trivial daily greetings, or resolved minor topics, and just like human, some memory could be faded. " 
                + "Be concise, do not include header, irelevant or no value information.";

                var user = $"Update the memory summary for {npcName} based on today's conversation.\n\n"
                    + "PREVIOUS SUMMARY:\n" + summary + "\n"
                    + "TODAY'S NEW CONVERSATION:\n" + messageList
                    + "";




                var model = summaryModel;
                string responseMessage = "";
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
                        responseMessage = GetResponseOutputText(json);

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
                        return "a error";
                    }
                }
            }
        }

        public static object[] GetToolList(NPC npc)
        {

            int heartLevel = 0;
            if (Game1.player.friendshipData.ContainsKey(npc.Name)) heartLevel = (int)Game1.player.friendshipData[npc.Name].Points / 250;

            var functionList = new List<object>();

            if (heartLevel >= 2)
            {
                functionList.Add(
                    new
                    {
                        type = "function",
                        name = "NPC_Birthday_Event",
                        description = "Use this function when PLAYER want to hold an event to celebrate NPC's birthday.",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                npc = new { type = "string", description = "Name of the NPC" }
                            },
                            required = new[] { "npc" },
                            additionalProperties = false
                        },
                        strict = true
                    }
                );
                functionList.Add(
                    new
                    {
                        type = "function",
                        name = "Campfire_Event",
                        description = "Use this function when PLAYER and NPC is planning to go for campfire together.",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                npc = new { type = "string", description = "Name of the NPC" }
                            },
                            required = new[] { "npc" },
                            additionalProperties = false
                        },
                        strict = true
                    }
                );
            }
            if (heartLevel >= 4)
            {
                functionList.Add(
                    new
                    {
                        type = "function",
                        name = "Picnic_Event",
                        description = "Use this function when PLAYER and NPC is planning to go for a picnic together.",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                npc = new { type = "string", description = "Name of the NPC" }
                            },
                            required = new[] { "npc" },
                            additionalProperties = false
                        },
                        strict = true
                    }
                );
                functionList.Add(
                    new
                    {
                        type = "function",
                        name = "Dinner_Event",
                        description = "Use this function when PLAYER and NPC is planning to have dinner together.",
                        parameters = new
                        {
                            type = "object",
                            properties = new
                            {
                                npc = new { type = "string", description = "Name of the NPC" }
                            },
                            required = new[] { "npc" },
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
                foreach (var item in output)
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


        public static async Task GetOpenAIUsage()
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
                        return;
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

                SMonitor.Log($"OpenAI usage window (UTC): {utcStartOfToday:yyyy-MM-dd HH:mm:ss} -> {utcNow:yyyy-MM-dd HH:mm:ss}", LogLevel.Info);
                if (perModelTotals.Count == 0)
                {
                    SMonitor.Log("No usage records found for the current UTC day.", LogLevel.Info);
                    return;
                }

                foreach (var item in perModelTotals.OrderByDescending(x => x.Value.Input + x.Value.Output))
                {
                    SMonitor.Log($"Model: {item.Key} | Input: {item.Value.Input} | Output: {item.Value.Output}", LogLevel.Info);
                }

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

                SMonitor.Log($"Premium Models Total | Input: {premiumInputTotal} | Output: {premiumOutputTotal}", LogLevel.Info);
                SMonitor.Log($"Regular Models Total | Input: {regularInputTotal} | Output: {regularOutputTotal}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Request failed: {ex.Message}", LogLevel.Error);
            }
        }
        

    }
}