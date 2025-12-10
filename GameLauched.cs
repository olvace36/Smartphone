using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System.Text;
using Newtonsoft.Json;
using Microsoft.Xna.Framework.Graphics;
using StardewValley.Network;
using Newtonsoft.Json.Linq;
using Microsoft.Xna.Framework;
using HarmonyLib;
using System.Reflection;
using static StardewValley.Minigames.MineCart;
using System.Runtime.CompilerServices;
using StardewValley.Tools;
using StardewValley.TerrainFeatures;
using StardewValley.Extensions;
using StardewValley.Minigames;
using StardewValley.GameData.Characters;
using ContentPatcher;
using StardewValley.Objects;
using StardewValley.Menus;
using StardewValley.Triggers;
using StardewValley.Delegates;
using StardewValley.Locations;
using xTile.Dimensions;
using StardewValley.GameData.Crops;
using xTile.Tiles;
using StardewValley.Characters;
using Smartphone.Data;


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
                Monitor.Log("Failed to get Mod1 API.", LogLevel.Warn);
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
                Game1.activeClickableMenu = phoneMenu;
            }
        }



        private void OnSaving(object sender, SavingEventArgs e)
        {
            MessageManager.SaveData();

            Helper.Data.WriteJsonFile($"./userdata/{Constants.SaveFolderName}/GiftMemoryData", GiftMemories);
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
                    var requestBody = new
                    {
                        model = chatModel,
                        messages = new[]
                        {
                            new { role = "system", content = system},
                            new { role = "user", content = user },
                        },
                        tools = GetToolList(npc),
                        tool_choice = "auto",
                        reasoning_effort = "minimal",
                        verbosity = "low"
                    };

                    var jsonRequest = JsonConvert.SerializeObject(requestBody);
                    var httpContent = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                    var httpResponse = await httpClient.PostAsync("https://api.openai.com/v1/chat/completions", httpContent);
                    if (httpResponse.IsSuccessStatusCode)
                    {
                        var jsonResponse = await httpResponse.Content.ReadAsStringAsync();

                        //SMonitor.Log(jsonResponse.ToString(), LogLevel.Error);

                        //SMonitor.Log(system, LogLevel.Error);
                        //SMonitor.Log("-----", LogLevel.Error);
                        //SMonitor.Log(user, LogLevel.Error);
                        //SMonitor.Log("-----", LogLevel.Error);
                        //SMonitor.Log(responseMessage, LogLevel.Error);
                        //SMonitor.Log("\n\n", LogLevel.Error);


                        dynamic json = JsonConvert.DeserializeObject(jsonResponse);
                        var msg = json.choices[0].message;


                        if (msg.tool_calls != null)
                        {
                            foreach (var call in msg.tool_calls)
                            {
                                if (call.function.name == "Dinner_Event")
                                {
                                    phoneMenu.ClosePhoneMenu();
                                    Game1.activeClickableMenu = new ConfirmationDialog(
                                        $"You are going for dinner with {npc.Name}",
                                        onConfirm: (Farmer who) =>
                                        {
                                            Game1.activeClickableMenu = null;
                                            var args = JsonConvert.DeserializeObject(call.function.arguments.ToString());
                                            iUnlimitedEventExpansionApi.TriggerDinnerEvent((string)args.npc);

                                        },
                                        onCancel: (Farmer who) =>
                                        {
                                            Game1.activeClickableMenu = null;
                                        }
                                    );
                                }
                                else if (call.function.name == "NPC_Birthday_Event")
                                {
                                    phoneMenu.ClosePhoneMenu();
                                    Game1.activeClickableMenu = new ConfirmationDialog(
                                        $"You are going to celebrate {npc.Name}'s birthday",
                                        onConfirm: (Farmer who) =>
                                        {
                                            Game1.activeClickableMenu = null;
                                            var args = JsonConvert.DeserializeObject(call.function.arguments.ToString());
                                            iUnlimitedEventExpansionApi.TriggerNpcBirthdayEvent((string)args.npc);

                                        },
                                        onCancel: (Farmer who) =>
                                        {
                                            Game1.activeClickableMenu = null;
                                        }
                                    );
                                }
                                else if (call.function.name == "Picnic_Event")
                                {
                                    phoneMenu.ClosePhoneMenu();
                                    Game1.activeClickableMenu = new ConfirmationDialog(
                                        $"You are going for a picnic with {npc.Name}",
                                        onConfirm: (Farmer who) =>
                                        {
                                            Game1.activeClickableMenu = null;
                                            var args = JsonConvert.DeserializeObject(call.function.arguments.ToString());
                                            iUnlimitedEventExpansionApi.TriggerPicnicEvent((string)args.npc);

                                        },
                                        onCancel: (Farmer who) =>
                                        {
                                            Game1.activeClickableMenu = null;
                                        }
                                    );
                                }
                                else if (call.function.name == "Campfire_Event")
                                {
                                    phoneMenu.ClosePhoneMenu();
                                    Game1.activeClickableMenu = new ConfirmationDialog(
                                        $"You are going for a campfire night with {npc.Name}",
                                        onConfirm: (Farmer who) =>
                                        {
                                            Game1.activeClickableMenu = null;
                                            var args = JsonConvert.DeserializeObject(call.function.arguments.ToString());
                                            iUnlimitedEventExpansionApi.TriggerCampingEvent((string)args.npc);

                                        },
                                        onCancel: (Farmer who) =>
                                        {
                                            Game1.activeClickableMenu = null;
                                        }
                                    );
                                }
                            }
                        }
                        else if (!string.IsNullOrEmpty((string)msg.content))
                        {
                            responseMessage = msg.content;
                        }




                        if (responseMessage.StartsWith($"{npc.Name}:"))
                            responseMessage = responseMessage.Substring(npc.Name.Length + 1).TrimStart();




                        Game1.addHUDMessage(new HUDMessage($"A new message from {npcName}", HUDMessage.newQuest_type));
                        DelayedAction.playSoundAfterDelay(MessageManager.currentPhoneSound, 0);
                        DelayedAction.playSoundAfterDelay(MessageManager.currentPhoneSound, 1500);

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
                    data += $"Player planting some {planting}";
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
                    var systemMessage = @$"You are an AI assistant in context of game Stardew Valley. Your task is to read the user message and the context of the conversation, and then decide one of these 2 cases:
                            1. If the conversation expresses an intent that matches a function, you **just return a function call**.
                            2. Else, respond to the player {Game1.player.Name} ({Game1.player.Gender}, teen/young adult), who is {relation} to {npc.Name}, as if you are the NPC {npc.Name}, using their tone, relationship, personality, and the tone of the player message." +

                            $" {npcCharacteristic} " +
                            "\nYou will be given a summary of their relationship, recent messages, and other in-world context. Limit spoken responses to under 40 words. Only call a function if the player is asking to do something specific." +
                            "\nSUMMARY:\n" + summary +
                            "\nMASSAGE HISTORY: \n" + combined +
                            "\nWORLD CONTEXT:\n" + data 
                            ;

                    systemMessage = $@"You are an AI assistant in game Stardew Valley. You will act as NPC {npc.Name}, texting with the PLAYER {Game1.player.Name} ({Game1.player.Gender}, teen/young adult, is {relation} to {npc.Name}).
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

                var user = $"From the previous summary of the conversation and the new discusstion today, create a new summary of the conversation between NPC {npcName} in Stardew Valley and PLAYER."
                    + "\nPREVIOUS SUMMARY: " + summary
                    + "\nNEW DISCUSSTION: " + messageList;

                var system = $"You are an AI assistant specialized in summarizing conversations between a NPC {npcName} and the PLAYER in the context of the game Stardew Valley. "
                    + "You will be given the previous summary of the conversation, along with today’s new conversation. "
                    + "Your task is to create a new summary of under 250 words, focus on the key points, tone and flow of the conversation. "
                    + "You may remove less important or outdated information if necessary to stay within the word limit.";




                var model = summaryModel;
                string responseMessage = "";
                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
                    var requestBody = new
                    {
                        model = model,
                        messages = new[]
                        {
                            new { role = "system", content = system},
                            new { role = "user", content = user },
                        },
                        reasoning_effort = "minimal",
                        verbosity = "low"
                    };

                    var jsonRequest = JsonConvert.SerializeObject(requestBody);
                    var httpContent = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                    var httpResponse = await httpClient.PostAsync("https://api.openai.com/v1/chat/completions", httpContent);
                    if (httpResponse.IsSuccessStatusCode)
                    {
                        var jsonResponse = await httpResponse.Content.ReadAsStringAsync();

                        dynamic json = JsonConvert.DeserializeObject(jsonResponse);
                        responseMessage = json.choices[0].message.content;

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
                        function = new
                        {
                            name = "NPC_Birthday_Event",
                            description = "Use this function when PLAYER want to hold an event to celebrate NPC's birthday.",
                            parameters = new
                            {
                                type = "object",
                                properties = new
                                {
                                    npc = new { type = "string", description = "Name of the NPC" }
                                },
                                required = new[] { "npc" }
                            }
                        }
                    }
                );
                functionList.Add(
                    new
                    {
                        type = "function",
                        function = new
                        {
                            name = "Campfire_Event",
                            description = "Use this function when PLAYER and NPC is planning to go for campfire together.",
                            parameters = new
                            {
                                type = "object",
                                properties = new
                                {
                                    npc = new { type = "string", description = "Name of the NPC" }
                                },
                                required = new[] { "npc" }
                            }
                        }
                    }
                );
            }
            if (heartLevel >= 4)
            {
                functionList.Add(
                    new
                    {
                        type = "function",
                        function = new
                        {
                            name = "Picnic_Event",
                            description = "Use this function when PLAYER and NPC is planning to go for a picnic together.",
                            parameters = new
                            {
                                type = "object",
                                properties = new
                                {
                                    npc = new { type = "string", description = "Name of the NPC" }
                                },
                                required = new[] { "npc" }
                            }
                        }
                    }
                );
                functionList.Add(
                    new
                    {
                        type = "function",
                        function = new
                        {
                            name = "Dinner_Event",
                            description = "Use this function when PLAYER and NPC is planning to have dinner together.",
                            parameters = new
                            {
                                type = "object",
                                properties = new
                                {
                                    npc = new { type = "string", description = "Name of the NPC" }
                                },
                                required = new[] { "npc" }
                            }
                        }
                    }
                );
            }
            return functionList.ToArray();
        }


    }
}