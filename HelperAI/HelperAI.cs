using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Extensions;
using StardewValley.Menus;

namespace Smartphone
{
    public partial class ModEntry
    {
        public static string k1 = "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";
        public static string k2 = "";
        public static string k3 = "";

        public static string xk1 = "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";
        public static string xk2 = "";
        public static string xk3 = "";

        public static string summaryModel = "gpt-5.4-mini";
        public static string chatModel = "gpt-5.4-mini";

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

                if (perModelTotals.Count == 0)
                {
                    return (-1, -1);
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

                return ((int)(premiumInputTotal + premiumOutputTotal), (int)(regularInputTotal + regularOutputTotal));
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Request failed: {ex.Message}", LogLevel.Error);
                return (-1, -1);
            }
        }
    }
}
