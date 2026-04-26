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

        public static bool IsMaxedLimit = false;
        public static bool IsReducedQuality = false;
        public static int totalFailedCheck = 0;


        // gpt-5.4 variant support reasoning effor: none, low, medium, high, xhigh
        // gpt-5 variant support reasoning effort: minimal, low, medium, high
        public static string chatModel = "gpt-5.4-mini";
        public static object chatReasoningEffort = new { effort = "none" };


        public static string summaryModel = "gpt-5.4-mini";
        public static object summaryReasoningEffort = new { effort = "low" };

        internal static void HandleAiModelSettingTimeChanged(int newTime)
        {
            if (!string.IsNullOrWhiteSpace(Config.OpenAIKey))
            {
                chatModel = Config.OpenAIModel;
                summaryModel = Config.OpenAIModel;

                switch (chatModel)
                {
                    case ModConfig.OpenAIModel_51:
                        chatReasoningEffort = new { effort = "none" };
                        summaryReasoningEffort = new { effort = "none" };
                        break;
                    case ModConfig.OpenAIModel_5mini:
                        chatReasoningEffort = new { effort = "minimal" };
                        summaryReasoningEffort = new { effort = "minimal" };
                        break;
                    case ModConfig.OpenAIModel_5nano:
                        chatReasoningEffort = new { effort = "minimal" };
                        summaryReasoningEffort = new { effort = "minimal" };
                        break;
                    case ModConfig.OpenAIModel_54mini:
                        chatReasoningEffort = new { effort = "none" };
                        summaryReasoningEffort = new { effort = "none" };
                        break;
                    case ModConfig.OpenAIModel_54nano:
                        chatReasoningEffort = new { effort = "none" };
                        summaryReasoningEffort = new { effort = "none" };
                        break;
                    default:
                        chatReasoningEffort = new { effort = "minimal" };
                        summaryReasoningEffort = new { effort = "minimal" };
                        break;
                }
                return;
            }


            if (Config.OpenAIKey == "" && newTime % 300 == 0)
            {
                Task.Run(async () =>
                {
                    var (premium, regular) = await GetOpenAIUsage();
                    if (regular == -1 || premium == -1)
                    {
                        totalFailedCheck += 1;
                        if (totalFailedCheck >= 3)
                        {
                            IsMaxedLimit = true;
                            NotificationManager.addNotification("=== Smartphone ===^^Failed to check AI usage for 3 times in a row, AI usage is temporarily disabled.^^Try restart your game and check mod page for support. HaPyke!");
                            return;
                        }
                    }


                    if (regular > 30000000)
                    {
                        IsMaxedLimit = true;
                        NotificationManager.addNotification("=== Smartphone ===^^AI usage reached its limit. Chat and StardewSocial are temporarily disabled.^^This will be reset the next day in timezone UTC+0. HaPyke!");
                        return;
                    }


                    IsMaxedLimit = false;
                    if (regular > 14000000 && chatModel != "gpt-5-nano")
                    {
                        chatModel = "gpt-5-nano";
                        chatReasoningEffort = new { effort = "minimal" };

                        summaryModel = "gpt-5-mini";
                        summaryReasoningEffort = new { effort = "minimal" };

                        IsReducedQuality = true;

                        NotificationManager.addNotification("=== Smartphone ===^^Usage is very high today, AI quality is temporarily downgraded so I won't go bankrupt lol.^^This will be reset the next day in timezone UTC+0. HaPyke!");
                    }
                    else if (regular > 10000000)
                    {
                        IsReducedQuality = false;
                        chatModel = "gpt-5-mini";
                        chatReasoningEffort = new { effort = "minimal" };

                        summaryModel = "gpt-5-mini";
                        summaryReasoningEffort = new { effort = "minimal" };
                    }
                    else
                    {
                        IsReducedQuality = false;
                        chatModel = "gpt-5.4-mini";
                        chatReasoningEffort = new { effort = "none" };

                        summaryModel = "gpt-5.4-mini";
                        summaryReasoningEffort = new { effort = "none" };
                    }
                });
            }
        }


        private static string GetNpcCharacteristicForPrompt(NPC npc)
        {
            if (npc is null)
                return string.Empty;

            string npcAge = npc.Age == 0 ? "adult" : npc.Age == 1 ? "teens" : npc.Age == 2 ? "child" : "adult";
            string npcManner = npc.Manners == 0 ? "a typical neutral manner" : npc.Manners == 1 ? "a polite and courteous manner" : npc.Manners == 2 ? "a distant and reserved manner" : "a typical neutral manner";
            string npcSocial = npc.SocialAnxiety == 0 ? "an outgoing person" : npc.SocialAnxiety == 1 ? "a little shy person" : "neither too outgoing nor shy";

            string npcCharacteristic = $" {npc.Name} is {npcAge}, {npcManner}, and is {npcSocial}";

            // CUSTOM CHARACTERISTIC OVERRIDE
            if (!string.IsNullOrWhiteSpace(Config.OpenAIKey))
            {
                if (Config.CharacteristicMode == ModConfig.CharacteristicModeLong && NpcCharacteristicsLong.TryGetValue(npc.Name, out string? customCharacteristicLong) && !string.IsNullOrWhiteSpace(customCharacteristicLong))
                {
                    npcCharacteristic = customCharacteristicLong;
                }
                else if (Config.CharacteristicMode == ModConfig.CharacteristicModeShort && NpcCharacteristicsShort.TryGetValue(npc.Name, out string? customCharacteristic) && !string.IsNullOrWhiteSpace(customCharacteristic))
                {
                    npcCharacteristic = customCharacteristic;
                }
                else if (Config.CharacteristicMode == ModConfig.CharacteristicModeMinimal && NpcCharacteristicsMinimal.TryGetValue(npc.Name, out string? customCharacteristicMinimal) && !string.IsNullOrWhiteSpace(customCharacteristicMinimal))
                {
                    npcCharacteristic = customCharacteristicMinimal;
                }
                return npcCharacteristic.Trim();
            }
            // DEFAULT CHARACTERISTIC
            else
            {
                if (NpcCharacteristicsShort.TryGetValue(npc.Name, out string? customCharacteristic) && !string.IsNullOrWhiteSpace(customCharacteristic) && !IsReducedQuality)
                {
                    npcCharacteristic = customCharacteristic;
                }
                else if (NpcCharacteristicsMinimal.TryGetValue(npc.Name, out string? customCharacteristicMinimal) && !string.IsNullOrWhiteSpace(customCharacteristicMinimal))
                {
                    npcCharacteristic = customCharacteristicMinimal;
                }

                return npcCharacteristic.Trim();
            }
        }

        /// <summary>Call to OpenAI to generate a response</summary>
        public static async Task<string> SendMessageToAssistant(string npcName, string text = "", string type = "response")
        {
            NPC npc = Game1.getCharacterFromName(npcName);
            var random = Game1.random;
            if (npc is null || IsMaxedLimit)
            {
                return "SYSTEM: ---Got an error---";
            }

            if (true)
            {
                var key = k1 + k2 + k3;

                if (Config.OpenAIKey != "")
                    key = Config.OpenAIKey;

                string user = type == "response"
                    ? ModEntry.BuildResponseConversationUserInput(npc, text)
                    : text;
                string system = GetSystemMessage(npc, type);

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
                        { "reasoning", chatReasoningEffort }
                    };

                    if (type == "response")
                    {
                        var tools = GetToolList(npc);
                        if (tools.Length > 0)
                        {
                            requestBody.Add("tools", tools);
                            requestBody.Add("tool_choice", "auto");
                        }
                    }

                    var jsonRequest = JsonConvert.SerializeObject(requestBody);
                    var httpContent = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                    var httpResponse = await httpClient.PostAsync("https://api.openai.com/v1/responses", httpContent);
                    if (httpResponse.IsSuccessStatusCode)
                    {
                        var jsonResponse = await httpResponse.Content.ReadAsStringAsync();
                        RegisterSuccessfulAiCall();

                        SMonitor.Log(jsonResponse.ToString(), LogLevel.Error);

                        JObject json = JObject.Parse(jsonResponse);
                        JArray toolCalls = GetResponseFunctionCalls(json);
                        responseMessage = GetResponseOutputText(json);

                        SMonitor.Log("system-----", LogLevel.Error);
                        SMonitor.Log(system, LogLevel.Error);
                        SMonitor.Log("user-----", LogLevel.Error);
                        SMonitor.Log(user, LogLevel.Error);
                        SMonitor.Log("response-----", LogLevel.Error);
                        SMonitor.Log(responseMessage, LogLevel.Error);
                        SMonitor.Log("\n\n", LogLevel.Error);

                        if (toolCalls.Count > 0)
                        {
                            foreach (var call in toolCalls)
                            {
                                string functionName = call["name"]?.ToString() ?? string.Empty;
                                string argumentsJson = call["arguments"]?.ToString() ?? "{}";

                                if (functionName == "schedule_event")
                                {
                                    var args = JObject.Parse(argumentsJson);
                                    var eventNpcName = args["npc"]?.ToString();
                                    var eventType = args["event_type"]?.ToString();
                                    var textResponse = args["npc_response"]?.ToString();

                                    if (string.IsNullOrWhiteSpace(eventNpcName)
                                        || string.IsNullOrWhiteSpace(eventType))
                                    {
                                        continue;
                                    }

                                    if (!TryGetRegisteredUnlimitedEvent(eventType, out var registeredEvent) || registeredEvent == null)
                                    {
                                        SMonitor.Log($"schedule_event ignored because event type '{eventType}' is not registered.", LogLevel.Warn);
                                        continue;
                                    }

                                    if (phoneMenu != null)
                                        phoneMenu.ClosePhoneMenu();

                                    string npcDisplayName = string.IsNullOrWhiteSpace(npc.displayName)
                                        ? npc.Name
                                        : npc.displayName;

                                    Game1.activeClickableMenu = new ConfirmationDialog(
                                        $"Schedule {registeredEvent.EventType} event with {npcDisplayName}?",
                                        onConfirm: (Farmer who) =>
                                        {
                                            iUnlimitedEventExpansionApi.OpenScheduleEventTimeMenu(
                                                eventNpcName.Trim(),
                                                registeredEvent.EventType,
                                                npcDisplayName,
                                                textResponse);

                                        },
                                        onCancel: (Farmer who) =>
                                        {
                                            Game1.activeClickableMenu = null;
                                        }
                                    );
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
                        SMonitor.Log(httpResponse.ToString(), LogLevel.Error);
                        return "SYSTEM: ---Got an error---";
                    }
                }
            }
        }

        private const string PlayerPhotoPrefix = "PlayerPhoto:";
        private const string PlayerPhotoTagPrefix = "PlayerPhotoTag:";
        private const string NpcPhotoPrefix = "NpcPhoto:";
        private const string NpcPhotoTagPrefix = "NpcPhotoTag:";

        private static string BuildConversationPreviewForAi(IEnumerable<string> rawMessages, string npcName, int maxLines)
        {
            List<string> sanitizedMessages = SanitizeConversationMessagesForAi(rawMessages, npcName);

            int safeMaxLines = Math.Max(1, maxLines);
            int skipCount = Math.Max(0, sanitizedMessages.Count - safeMaxLines);
            int takeCount = Math.Min(safeMaxLines, Math.Max(0, sanitizedMessages.Count));

            return string.Join("\n", sanitizedMessages.Skip(skipCount).Take(takeCount));
        }

        private static List<string> SanitizeConversationMessagesForAi(IEnumerable<string> rawMessages, string npcName)
        {
            var source = (rawMessages ?? Enumerable.Empty<string>())
                .Select(message => (message ?? string.Empty).Trim())
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .ToList();

            var sanitized = new List<string>();

            for (int i = 0; i < source.Count; i++)
            {
                string line = source[i];

                if (line.StartsWith(PlayerPhotoPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    string tags = TryConsumeFollowingPhotoTag(source, ref i, isPlayerPhoto: true);
                    sanitized.Add(FormatPhotoSummaryForAi(isPlayerPhoto: true, tags, npcName));
                    continue;
                }

                if (line.StartsWith(NpcPhotoPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    string tags = TryConsumeFollowingPhotoTag(source, ref i, isPlayerPhoto: false);
                    sanitized.Add(FormatPhotoSummaryForAi(isPlayerPhoto: false, tags, npcName));
                    continue;
                }

                if (line.StartsWith(PlayerPhotoTagPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    string tags = NormalizePhotoTagText(line.Substring(PlayerPhotoTagPrefix.Length));
                    sanitized.Add(FormatPhotoSummaryForAi(isPlayerPhoto: true, tags, npcName));
                    continue;
                }

                if (line.StartsWith(NpcPhotoTagPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    string tags = NormalizePhotoTagText(line.Substring(NpcPhotoTagPrefix.Length));
                    sanitized.Add(FormatPhotoSummaryForAi(isPlayerPhoto: false, tags, npcName));
                    continue;
                }

                sanitized.Add(line);
            }

            return sanitized;
        }

        private static string TryConsumeFollowingPhotoTag(List<string> source, ref int currentIndex, bool isPlayerPhoto)
        {
            if (currentIndex + 1 >= source.Count)
                return string.Empty;

            string nextLine = source[currentIndex + 1];
            string expectedTagPrefix = isPlayerPhoto ? PlayerPhotoTagPrefix : NpcPhotoTagPrefix;

            if (!nextLine.StartsWith(expectedTagPrefix, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            currentIndex++;
            return NormalizePhotoTagText(nextLine.Substring(expectedTagPrefix.Length));
        }

        private static string NormalizePhotoTagText(string rawTagText)
        {
            return string.Join("; ",
                (rawTagText ?? string.Empty)
                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(tag => tag.Trim())
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static string FormatPhotoSummaryForAi(bool isPlayerPhoto, string tags, string npcName)
        {
            string normalizedTags = NormalizePhotoTagText(tags);

            if (isPlayerPhoto)
            {
                if (string.IsNullOrWhiteSpace(normalizedTags))
                    return "";

                return $"PLAYER: [Attached photo tags: {normalizedTags}]";
            }

            string speaker = string.IsNullOrWhiteSpace(npcName) ? "NPC" : npcName;
            if (string.IsNullOrWhiteSpace(normalizedTags))
                return $"";

            return $"{speaker}: [Sent photo tags: {normalizedTags}]";
        }

        private static string GetSystemMessage(NPC npc, string type)
        {
            try
            {
                int heartLevel = 0;
                if (Game1.player.friendshipData.ContainsKey(npc.Name)) heartLevel = (int)Game1.player.friendshipData[npc.Name].Points / 250;


                string relation = heartLevel <= 2 ? "stranger" : heartLevel <= 4 ? "acquaintance" : heartLevel <= 6 ? "close friend" : "best friend";
                bool isDating = Game1.player.friendshipData.TryGetValue(npc.Name, out Friendship friendship) && friendship.IsDating();
                bool isRoommate = friendship != null && friendship.IsRoommate();
                bool isMarried = friendship != null && friendship.IsMarried();
                bool isEngaged = friendship != null && friendship.IsEngaged();
                bool isDivorced = friendship != null && friendship.IsDivorced();

                if (isDivorced) relation = "divorced";
                else if (isRoommate) relation = "roommate";
                else if (isMarried) relation = "married";
                else if (isEngaged) relation = "engaged";
                else if (isDating) relation = "dating";

                string npcLocation = npc.currentLocation.DisplayName;
                if (npc.currentLocation.Name == npc.DefaultMap)
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

                string data = @$"{npc.Name} currently at {npcLocation}; Today weather: {Game1.currentLocation.GetWeather().Weather}; Tomorrow weather: {Game1.weatherForTomorrow};  Time: {timeFormatted}, day {Game1.dayOfMonth} {Game1.currentLocation.GetSeason()};";

                if (planting != "")
                    data += $"\nPlayer planting some {planting} on the farm";

                string latestGift = "";
                var dayAgo = 0;
                if (GiftMemories.TryGetValue(npc.Name, out GiftMemory? memory) && memory != null)
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
                    data += "It is " + string.Join(", ", npcBirthdayTmr.Select(n => n.Name)) + " birthday today.";
                }

                var messages_history = npcMessagesToday.ContainsKey(npc.Name) ? npcMessagesToday[npc.Name] : new List<string>();
                string combined = BuildConversationPreviewForAi(messages_history, npc.Name, maxLines: 12);

                string summary = "";
                if (npcConversationSummary.ContainsKey(npc.Name))
                    summary = npcConversationSummary[npc.Name];

                var npcCharacteristic = GetNpcCharacteristicForPrompt(npc);

                if (type == "response")
                {
                    object[] possibleEvent = GetToolList(npc, listOnly: true);
                    var systemMessage = $@"
                            **Context**
                            * You are roleplaying as NPC **{npc.Name}** in Stardew Valley, responding to a conversation with PLAYER **{Game1.player.Name}** ({(Game1.player.IsMale ? "Male" : "Female")}, teen/young adult).

                            **Response Instructions**
                            1. **Response:** Reply to the user with a message of <30 words.
                            2. **Slice of Life:** You may occasionally invent small, random dynamic details happening around you for your response. Be creative and do not be repetitive with your responses. 

                            * **{npc.Name} personality:** {npcCharacteristic}
                            * **Relationship with player:** {relation}
                            * **World Context:** {data}
                            * **Memory Summary:** {summary}
                            ";

                    if (possibleEvent.Any())
                        systemMessage = $@"
                            **Context**
                            * You are roleplaying as NPC **{npc.Name}** in Stardew Valley, responding to a conversation with PLAYER **{Game1.player.Name}** ({(Game1.player.IsMale ? "Male" : "Female")}, teen/young adult).

                            **Response Instructions**
                            1. **Function call:** When the player intends to invite the NPC for an event that is in the possible event list below, then you must return the function schedule_event and finish.
                            2. **Response:** If player do not intend to invite the NPC for an event, then reply to the user with a message of <30 words.
                            3. **Slice of Life:** You may occasionally invent small, random dynamic details happening around you for your response. Be creative and do not be repetitive with your responses. Do not invite or suggest the player for an event.
                            
                            * **{npc.Name} personality:** {npcCharacteristic}
                            * **Relationship with player:** {relation}
                            * **Possible events:** {string.Join(", ", possibleEvent)}
                            * **World Context:** {data}
                            * **Memory Summary:** {summary}
                            ";



                    return systemMessage;
                }
                else if (type == "text")
                {
                    var systemMessage = $@"
                        **Context**
                        * You are roleplaying as NPC {npc.Name} in Stardew Valley. Send a text message to the PLAYER {Game1.player.Name} ({Game1.player.Gender}, teen/young adult) to start a new random conversation.

                        **Response Instructions**
                        1. Keep the text under 30 words. Be creative, in-character, and match your relationship level.
                        3. **Slice of Life:** You may occasionally invent small, random dynamic details happening around you for your response. Be creative and not repetitive with your responses.

                        * **{npc.Name} personality:** {npcCharacteristic}
                        * **Relationship with player:** {relation}
                        * **World Context:** {data}
                        * **Memory Summary:** {summary}
                        ";
                    return systemMessage;
                }
                else if (type == "invite")
                {
                    object[] possibleEvent = GetToolList(npc, listOnly: true, ignoreBirthdayEvent: true);
                    var systemMessage = $@"
                        **Context**
                        * You are roleplaying as NPC {npc.Name} in Stardew Valley. Send a text message to the PLAYER {Game1.player.Name} ({Game1.player.Gender}, teen/young adult) to invite them to hang out.

                        **Response Instructions**
                        * Base on the relationship with player and the world context, if it's appropriate to invite the player for an event today, then send a text invitation under 20 words. You will select one of these events: {string.Join(", ", possibleEvent)}. Otherwise, do not send any message.

                        * **{npc.Name} personality:** {npcCharacteristic}
                        * **Relationship with player:** {relation}
                        * **World Context:** {data}
                        * **Memory Summary:** {summary}
                        ";

                    return systemMessage;
                }

                return "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        public static async Task<Dictionary<string, string>> SummaryConversationsBatch(Dictionary<string, string> conversationsByNpc, bool bypassAiLimit = false)
        {
            var parsedSummaries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (conversationsByNpc == null || conversationsByNpc.Count == 0)
                return parsedSummaries;

            if (!bypassAiLimit && !TryConsumeAiCallSlot())
            {
                SMonitor.Log("SummaryConversationsBatch skipped because AI limit is reached.", LogLevel.Trace);
                return parsedSummaries;
            }

            var key = k1 + k2 + k3;
            var maxAiSummaryLength = 200;
            var maxConversationsToSummarize = Config.OpenAIKey != "" ? 6 : 3;
            if (Config.OpenAIKey != "" && Config.MaxSummaryWordCount != 0)
            {
                maxAiSummaryLength = Config.MaxSummaryWordCount;
                key = Config.OpenAIKey;
            }

            var currentMemories = new List<object>();
            var todayConversations = new List<object>();
            var expectedNpcNames = new List<string>();
            var summaryCandidates = new List<(string NpcName, string Conversation, string PreviousSummary, int ConversationLength)>();

            foreach (var kvp in conversationsByNpc)
            {
                string npcName = (kvp.Key ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(npcName))
                    continue;

                NPC npc = Game1.getCharacterFromName(npcName, mustBeVillager: false);
                if (npc is null)
                    continue;

                string sanitizedMessageList = BuildConversationPreviewForAi(
                    (kvp.Value ?? string.Empty).Split('\n', StringSplitOptions.None),
                    npcName,
                    maxLines: 20);

                if (string.IsNullOrWhiteSpace(sanitizedMessageList))
                    continue;

                string previousSummary = npcConversationSummary.TryGetValue(npcName, out string? existingSummary)
                    ? existingSummary
                    : string.Empty;

                summaryCandidates.Add((npcName, sanitizedMessageList, previousSummary, sanitizedMessageList.Length));
            }

            if (summaryCandidates.Count == 0)
                return parsedSummaries;

            foreach (var candidate in summaryCandidates
                .OrderByDescending(item => item.ConversationLength)
                .ThenBy(item => item.NpcName, StringComparer.OrdinalIgnoreCase)
                .Take(maxConversationsToSummarize))
            {
                currentMemories.Add(new { npc = candidate.NpcName, summary = candidate.PreviousSummary });
                todayConversations.Add(new { npc = candidate.NpcName, conversation = candidate.Conversation });
                expectedNpcNames.Add(candidate.NpcName);
            }

            var system = "You are the memory manager for multiple NPCs in Stardew Valley. "
                + "You are provided with two arrays: current memory summaries and today's new conversations with the PLAYER. "
                + "For each NPC, read the previous memory and the new conversation, then summarize it into an updated memory bank. "
                + "Focus on factual lore, player preferences, important life events, and the current emotional standing/relationship with the PLAYER. "
                + $"Keep each NPC summary under {maxAiSummaryLength} words. Remove outdated, trivial, non-memory-value details. "
                + "Return only one valid JSON object with this format: {\"summaries\":{\"NPC Name\":\"updated summary\"}}. ";


            var userPayload = new
            {
                currentMemory = currentMemories,
                todayConversation = todayConversations
            };

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
                var requestBody = new
                {
                    model = summaryModel,
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
                                new { type = "input_text", text = JsonConvert.SerializeObject(userPayload) }
                            }
                        },
                    },
                    text = new { format = new { type = "json_object" }, verbosity = "low" },
                    reasoning = summaryReasoningEffort
                };

                var jsonRequest = JsonConvert.SerializeObject(requestBody);
                var httpContent = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                var httpResponse = await httpClient.PostAsync("https://api.openai.com/v1/responses", httpContent);
                if (httpResponse.IsSuccessStatusCode)
                {
                    var jsonResponse = await httpResponse.Content.ReadAsStringAsync();
                    RegisterSuccessfulAiCall();
                    JObject json = JObject.Parse(jsonResponse);
                    SMonitor.Log(json.ToString(), LogLevel.Error);
                    string responseText = GetResponseOutputText(json);

                    if (TryParseBatchConversationSummaries(responseText, expectedNpcNames, out Dictionary<string, string> summaries))
                        return summaries;

                    SMonitor.Log($"Unable to parse batch conversation summaries: {responseText}", LogLevel.Trace);
                    return parsedSummaries;
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
                    return parsedSummaries;
                }
            }
        }

        private static async Task<Dictionary<string, string>> GenerateNpcSocialPostTextsBatch(IReadOnlyList<DailySocialPostPlan> scheduledPosts)
        {
            var generatedPosts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (scheduledPosts == null || scheduledPosts.Count == 0 || IsMaxedLimit)
                return generatedPosts;

            List<DailySocialPostPlan> validPlans = scheduledPosts
                .Where(plan => plan != null
                    && plan.IncludeText
                    && !string.IsNullOrWhiteSpace(plan.PlanId)
                    && !string.IsNullOrWhiteSpace(plan.AuthorName))
                .ToList();

            if (validPlans.Count == 0)
                return generatedPosts;

            try
            {
                var key = k1 + k2 + k3;
                if (Config.OpenAIKey != "")
                    key = Config.OpenAIKey;

                var payloadPosts = validPlans.Select(plan => new
                {
                    id = plan.PlanId,
                    npc = plan.AuthorName,
                    characteristicDevelopmentStage = ResolveNpcCharacteristicDevelopmentStage(plan.AuthorName),
                    npcCharacteristic = (plan.NpcCharacteristic ?? string.Empty).Trim(),
                    imageTags = string.Join(", ", (plan.AttachmentTags ?? new List<List<string>>())
                        .SelectMany(tags => tags ?? new List<string>())
                        .Where(tag => !string.IsNullOrWhiteSpace(tag))
                        .Select(tag => tag.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)),
                    worldContext = BuildWorldContextForSocialPost(plan.ScheduledTime),
                    scheduledTime = plan.ScheduledTime
                }).ToList();

                string developerMessage = @"
                    You are roleplaying as NPCs in Stardew Valley. Your task is to write posts on a social media channel for each NPC.
                    For each post input item, generate one short in-character post from the first-person perspective of the provided NPC.
                    Requirements:
                    - Keep each post under 40 words.
                    - Match the personality and characteristic development stage of the NPC.
                    - If image tags are provided, then the post should be relevant to the photo content and the world context. The tags describing where, when, what and who can be seen in the photo. In most case, the photo is taken by the NPC when they are visiting others, hanging out or just doing something.
                    - Invent random topic such as daily life, something happened, something fun, something strange, some drama, some topic to debate,... Be creative and dynamics. Do not be repetitive!
                    Return exactly one valid JSON object in this format:
                    {""posts"": [{""id"": ""<id>"", ""text"": ""<generated post text>""}]}
                    Every returned id must match an input id.";

                var userPayload = new
                {
                    posts = payloadPosts
                };

                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);

                    var requestBody = new
                    {
                        model = chatModel,
                        input = new object[]
                        {
                            new
                            {
                                role = "developer",
                                content = new object[]
                                {
                                    new { type = "input_text", text = developerMessage }
                                }
                            },
                            new
                            {
                                role = "user",
                                content = new object[]
                                {
                                    new { type = "input_text", text = JsonConvert.SerializeObject(userPayload, Formatting.Indented) }
                                }
                            }
                        },
                        text = new { format = new { type = "json_object" }, verbosity = "low" },
                        reasoning = chatReasoningEffort
                    };

                    var jsonRequest = JsonConvert.SerializeObject(requestBody);
                    var httpContent = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                    var httpResponse = await httpClient.PostAsync("https://api.openai.com/v1/responses", httpContent);
                    if (httpResponse.IsSuccessStatusCode)
                    {
                        string jsonResponse = await httpResponse.Content.ReadAsStringAsync();
                        RegisterSuccessfulAiCall();
                        JObject json = JObject.Parse(jsonResponse);
                        SMonitor.Log(JsonConvert.SerializeObject(userPayload, Formatting.Indented), LogLevel.Error);
                        SMonitor.Log(json.ToString(), LogLevel.Error);
                        string responseText = GetResponseOutputText(json).Trim();

                        string[] expectedPostIds = validPlans
                            .Select(plan => plan.PlanId)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray();

                        if (TryParseGeneratedNpcSocialPosts(responseText, expectedPostIds, out Dictionary<string, string> parsedPosts))
                            return parsedPosts;

                        SMonitor.Log($"Unable to parse generated social posts payload: {responseText}", LogLevel.Trace);
                        return generatedPosts;
                    }

                    int statusCode = (int)httpResponse.StatusCode;
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

                    SMonitor.Log($"Unable to generate batch social post content. {statusCode}, {errorMessage}\n\n", LogLevel.Error);
                    return generatedPosts;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Unable to generate batch social post content: {ex}", LogLevel.Trace);
                return generatedPosts;
            }
        }

        private static string ResolveNpcCharacteristicDevelopmentStage(string npcName)
        {
            if (string.IsNullOrWhiteSpace(npcName)
                || Game1.player?.friendshipData == null
                || !Game1.player.friendshipData.TryGetValue(npcName, out Friendship? friendship)
                || friendship == null)
            {
                return "very early";
            }

            int heartLevel = friendship.Points / 250;
            return heartLevel <= 2
                ? "very early"
                : heartLevel <= 4
                    ? "early"
                    : heartLevel <= 6
                        ? "middle"
                        : "late";
        }

        private static string BuildWorldContextForSocialPost(int scheduledTime)
        {
            string weather = Game1.currentLocation != null
                ? Game1.currentLocation.GetWeather().Weather.ToString()
                : "Unknown";

            string season = Game1.currentLocation != null
                ? Game1.currentLocation.GetSeason().ToString()
                : Game1.currentSeason;

            return $"Weather: {weather}. Time: {ResolveSocialPostPeriod(scheduledTime)}. Day: {Game1.dayOfMonth} {season}.";
        }

        private static string ResolveSocialPostPeriod(int scheduledTime)
        {
            return scheduledTime switch
            {
                >= 600 and < 1200 => "morning",
                >= 1200 and < 1700 => "afternoon",
                >= 1700 and < 2200 => "evening",
                _ => "night"
            };
        }

        public static async Task<Dictionary<string, Dictionary<string, string>>> GenerateNpcSocialPostCommentsBatch(IReadOnlyDictionary<string, IReadOnlyList<string>> commenterNamesByPostId)
        {
            var generatedCommentsByPost = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            if (commenterNamesByPostId == null || commenterNamesByPostId.Count == 0 || IsMaxedLimit)
                return generatedCommentsByPost;

            var expectedCommentersByPost = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            var payloadPosts = new List<object>();

            foreach (KeyValuePair<string, IReadOnlyList<string>> entry in commenterNamesByPostId)
            {
                string postId = (entry.Key ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(postId))
                    continue;

                StardewConnectPost? post = StardewConnectManager.GetPost(postId);
                if (post == null)
                    continue;

                string[] normalizedCommenters = (entry.Value ?? Array.Empty<string>())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (normalizedCommenters.Length == 0)
                    continue;

                expectedCommentersByPost[postId] = normalizedCommenters;

                string postAuthor = (post.AuthorName ?? string.Empty).Trim();
                string postText = (post.Text ?? string.Empty).Trim();
                string postTag = StardewConnectManager.GetPostTag(post).Trim();
                var latestComments = (post.Comments ?? Enumerable.Empty<StardewConnectComment>())
                    .Where(comment => comment != null)
                    .TakeLast(3)
                    .Select(comment => new
                    {
                        authorName = (comment!.AuthorName ?? string.Empty).Trim(),
                        text = (comment.Text ?? string.Empty).Trim()
                    })
                    .Where(comment => !string.IsNullOrWhiteSpace(comment.authorName) || !string.IsNullOrWhiteSpace(comment.text))
                    .ToArray();

                var commenterPayload = normalizedCommenters
                    .Select(name =>
                    {
                        int heartLevel = 0;
                        if (Game1.player.friendshipData.ContainsKey(name))
                            heartLevel = (int)Game1.player.friendshipData[name].Points / 250;

                        string npcCharacteristicState = heartLevel <= 2
                            ? "very early"
                            : heartLevel <= 4
                                ? "early"
                                : heartLevel <= 6
                                    ? "middle"
                                    : "late";

                        return new
                        {
                            npc = name,
                            characteristicDevelopmentState = npcCharacteristicState
                        };
                    })
                    .ToArray();

                payloadPosts.Add(new
                {
                    id = postId,
                    author = postAuthor,
                    postDescription = postText,
                    imageTag = postTag,
                    recentComments = latestComments,
                    commenters = commenterPayload
                });
            }

            if (payloadPosts.Count == 0)
                return generatedCommentsByPost;

            try
            {
                var key = k1 + k2 + k3;

                if (Config.OpenAIKey != "")
                    key = Config.OpenAIKey;

                string weatherText = Game1.currentLocation?.GetWeather().Weather.ToString() ?? "Unknown";
                string seasonText = Game1.currentLocation?.GetSeason().ToString() ?? "Unknown";
                int time = Game1.timeOfDay;
                string timeFormatted = time switch
                {
                    >= 600 and < 1200 => "morning",
                    >= 1200 and < 1700 => "afternoon",
                    >= 1700 and < 2200 => "evening",
                    _ => "night"
                };

                var developerMessage = $@"
                You are roleplaying as Stardew Valley NPCs writing comments to posts on a social media platform.
                Generate comments for multiple posts in one response.
                For each post:
                - Return exactly one short in-character comment for every requested NPC in commenters.
                - If the NPC is also the post author, then they should respond to other commenters instead of replying to their own post.
                - Use post author, post description, recent comments, and image tags when relevant.
                - Follow each NPC characteristic development state when deciding tone.
                - Keep each comment concise, casual, and under 20 words. Be dynamic and creative. Do not be repetitive.
                You may tag another NPC with @Name to respond to their comment. To tag the PLAYER, use @{Game1.player.Name}.
                Return exactly one valid JSON object with this format:
                {{""posts"": [{{""id"": ""<postId>"", ""comments"": {{""NPC name"": ""Comment""}}}}]}}
                Every returned id must match an input id.
                Include every requested NPC exactly once for each returned id.
                No markdown, no labels, no extra prose.";

                var userPayload = new
                {
                    worldContext = $"Weather: {weatherText}. Time: {timeFormatted} of day {Game1.dayOfMonth} {seasonText}",
                    posts = payloadPosts
                };

                var requestBody = new
                {
                    model = chatModel,
                    input = new object[]
                    {
                        new
                        {
                            role = "developer",
                            content = new object[]
                            {
                                new { type = "input_text", text = developerMessage }
                            }
                        },
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new { type = "input_text", text = JsonConvert.SerializeObject(userPayload, Formatting.Indented) }
                            }
                        }
                    },
                    text = new { format = new { type = "json_object" }, verbosity = "low" },
                    reasoning = chatReasoningEffort
                };

                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);

                    var jsonRequest = JsonConvert.SerializeObject(requestBody);
                    var httpContent = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                    var httpResponse = await httpClient.PostAsync("https://api.openai.com/v1/responses", httpContent);
                    if (httpResponse.IsSuccessStatusCode)
                    {
                        string jsonResponse = await httpResponse.Content.ReadAsStringAsync();
                        RegisterSuccessfulAiCall();
                        JObject json = JObject.Parse(jsonResponse);
                        SMonitor.Log(json.ToString(), LogLevel.Error);
                        string responseText = GetResponseOutputText(json).Trim();

                        if (TryParseGeneratedNpcSocialCommentsBatch(responseText, expectedCommentersByPost, out Dictionary<string, Dictionary<string, string>> parsedCommentsByPost))
                            return parsedCommentsByPost;

                        SMonitor.Log($"Unable to parse generated social comments payload: {responseText}", LogLevel.Trace);
                        return generatedCommentsByPost;
                    }

                    int statusCode = (int)httpResponse.StatusCode;
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

                    SMonitor.Log($"Unable to generate batched social comments. {statusCode}, {errorMessage}\n\n", LogLevel.Error);
                    return generatedCommentsByPost;
                }
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Unable to generate batched social comments: {ex}", LogLevel.Trace);
                return generatedCommentsByPost;
            }
        }

        public static object[] GetToolList(NPC npc, bool listOnly = false, bool ignoreBirthdayEvent = false)
        {
            var functionList = new List<object>();

            if (Game1.timeOfDay > 2200 || iUnlimitedEventExpansionApi == null)
                return functionList.ToArray();

            int heartLevel = 0;
            if (Game1.player.friendshipData.ContainsKey(npc.Name)) heartLevel = (int)Game1.player.friendshipData[npc.Name].Points / 250;
            var registeredEvents = GetRegisteredUnlimitedEventsForHeartLevel(heartLevel);

            if (registeredEvents.Count > 0)
            {
                var allowedEvents = registeredEvents
                    .Select(evt => evt.EventType)
                    .ToArray();

                // Get list of all event only, exclude Birthday for invitation type
                if (listOnly)
                {
                    if (!ignoreBirthdayEvent)
                        return allowedEvents;

                    return registeredEvents
                    .Where(evt => !evt.EventType.Equals("Birthday", StringComparison.OrdinalIgnoreCase))
                    .Select(evt => evt.EventType)
                    .ToArray();
                }

                var extraToolDescriptions = string.Join(
                    " ",
                    registeredEvents
                        .Select(evt => evt.ToolDescription)
                        .Where(text => !string.IsNullOrWhiteSpace(text))
                        .Distinct(StringComparer.OrdinalIgnoreCase));

                string scheduleToolDescription = "Schedule an event for NPC and PLAYER, specifying the event type, and NPC response message.";
                if (!string.IsNullOrWhiteSpace(extraToolDescriptions))
                    scheduleToolDescription += $" {extraToolDescriptions}";

                functionList.Add(
                    new
                    {
                        type = "function",
                        name = "schedule_event",
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
                                    description = "The specific type of event agreed upon.",
                                    @enum = allowedEvents.ToArray()
                                },
                                npc_response = new { type = "string", description = "A message from the NPC to invite or confirm the event" }
                            },
                            required = new[] { "npc", "event_type", "npc_response" },
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
                            var text = part["text"]?.ToString().Trim();
                            if (!string.IsNullOrWhiteSpace(text))
                                return text;
                        }
                    }
                }
            }

            var fallbackText = responseJson?["output_text"]?.ToString()?.Trim();
            return fallbackText ?? string.Empty;
        }

        private static bool TryParseBatchConversationSummaries(string responseText, IEnumerable<string> expectedNpcNames, out Dictionary<string, string> summaries)
        {
            summaries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string[] normalizedNpcNames = (expectedNpcNames ?? Enumerable.Empty<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (normalizedNpcNames.Length == 0)
                return false;

            string jsonPayload = ExtractJsonPayload(responseText);
            if (string.IsNullOrWhiteSpace(jsonPayload))
                return false;

            try
            {
                JToken token = JToken.Parse(jsonPayload);
                return TryPopulateBatchConversationSummaries(token, normalizedNpcNames, summaries);
            }
            catch (JsonReaderException)
            {
                return false;
            }
        }

        private static bool TryPopulateBatchConversationSummaries(JToken token, IReadOnlyCollection<string> expectedNpcNames, Dictionary<string, string> summaries)
        {
            if (token == null)
                return false;

            if (token.Type == JTokenType.Object)
            {
                JObject obj = (JObject)token;
                if ((TryGetJsonPropertyValue(obj, "summaries", out JToken? summariesToken)
                        || TryGetJsonPropertyValue(obj, "summary", out summariesToken)
                        || TryGetJsonPropertyValue(obj, "memory", out summariesToken))
                    && summariesToken != null)
                {
                    return TryPopulateBatchConversationSummaries(summariesToken, expectedNpcNames, summaries);
                }

                foreach (string npcName in expectedNpcNames)
                {
                    if (!TryGetJsonPropertyValue(obj, npcName, out JToken? summaryToken))
                        continue;

                    string summaryText = NormalizeGeneratedConversationSummaryText(npcName, ExtractConversationSummaryText(summaryToken));
                    if (!string.IsNullOrWhiteSpace(summaryText))
                        summaries[npcName] = summaryText;
                }

                return summaries.Count > 0;
            }

            if (token.Type != JTokenType.Array)
                return false;

            foreach (JToken item in (JArray)token)
            {
                if (item.Type != JTokenType.Object)
                    continue;

                JObject itemObject = (JObject)item;
                string npcName = string.Empty;
                if (TryGetJsonPropertyValue(itemObject, "npc", out JToken? npcToken)
                    || TryGetJsonPropertyValue(itemObject, "name", out npcToken)
                    || TryGetJsonPropertyValue(itemObject, "character", out npcToken))
                {
                    npcName = (npcToken?.ToString() ?? string.Empty).Trim();
                }

                if (string.IsNullOrWhiteSpace(npcName) || !expectedNpcNames.Contains(npcName, StringComparer.OrdinalIgnoreCase))
                    continue;

                JToken? summaryTokenValue = null;
                if (!TryGetJsonPropertyValue(itemObject, "summary", out summaryTokenValue)
                    && !TryGetJsonPropertyValue(itemObject, "memory", out summaryTokenValue)
                    && !TryGetJsonPropertyValue(itemObject, "text", out summaryTokenValue))
                {
                    continue;
                }

                string summaryText = NormalizeGeneratedConversationSummaryText(npcName, ExtractConversationSummaryText(summaryTokenValue));
                if (!string.IsNullOrWhiteSpace(summaryText))
                    summaries[npcName] = summaryText;
            }

            return summaries.Count > 0;
        }

        private static string ExtractConversationSummaryText(JToken? summaryToken)
        {
            if (summaryToken == null)
                return string.Empty;

            if (summaryToken.Type == JTokenType.Object)
            {
                JObject summaryObject = (JObject)summaryToken;
                if (TryGetJsonPropertyValue(summaryObject, "summary", out JToken? nestedSummary)
                    || TryGetJsonPropertyValue(summaryObject, "memory", out nestedSummary)
                    || TryGetJsonPropertyValue(summaryObject, "text", out nestedSummary))
                {
                    return nestedSummary?.ToString() ?? string.Empty;
                }
            }

            if (summaryToken.Type == JTokenType.Array)
            {
                return string.Join(" ",
                    ((JArray)summaryToken)
                        .Select(item => item?.ToString() ?? string.Empty)
                        .Where(text => !string.IsNullOrWhiteSpace(text)));
            }

            return summaryToken.ToString();
        }

        private static string NormalizeGeneratedConversationSummaryText(string npcName, string? summaryText)
        {
            string normalizedText = string.Join(" ", (summaryText ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                .Trim();

            if (normalizedText.StartsWith($"{npcName}:", StringComparison.OrdinalIgnoreCase))
                normalizedText = normalizedText.Substring(npcName.Length + 1).TrimStart();

            if ((normalizedText.StartsWith("\"") && normalizedText.EndsWith("\"") && normalizedText.Length > 1)
                || (normalizedText.StartsWith("'") && normalizedText.EndsWith("'") && normalizedText.Length > 1))
            {
                normalizedText = normalizedText.Substring(1, normalizedText.Length - 2).Trim();
            }

            return normalizedText;
        }

        private static bool TryPopulateGeneratedNpcSocialComments(JToken token, IReadOnlyCollection<string> expectedNpcNames, Dictionary<string, string> comments)
        {
            if (token == null)
                return false;

            if (token.Type == JTokenType.Object)
            {
                JObject obj = (JObject)token;
                if (TryGetJsonPropertyValue(obj, "comments", out JToken? commentsToken) && commentsToken != null)
                    return TryPopulateGeneratedNpcSocialComments(commentsToken, expectedNpcNames, comments);

                foreach (string npcName in expectedNpcNames)
                {
                    if (!TryGetJsonPropertyValue(obj, npcName, out JToken? commentToken))
                        continue;

                    string commentText = NormalizeGeneratedSocialCommentText(npcName, commentToken?.ToString());
                    if (!string.IsNullOrWhiteSpace(commentText))
                        comments[npcName] = commentText;
                }

                return comments.Count > 0;
            }

            if (token.Type != JTokenType.Array)
                return false;

            foreach (JToken item in (JArray)token)
            {
                if (item.Type != JTokenType.Object)
                    continue;

                JObject itemObject = (JObject)item;
                string npcName = string.Empty;
                if (TryGetJsonPropertyValue(itemObject, "npc", out JToken? npcToken)
                    || TryGetJsonPropertyValue(itemObject, "name", out npcToken)
                    || TryGetJsonPropertyValue(itemObject, "author", out npcToken))
                {
                    npcName = (npcToken?.ToString() ?? string.Empty).Trim();
                }

                if (string.IsNullOrWhiteSpace(npcName) || !expectedNpcNames.Contains(npcName, StringComparer.OrdinalIgnoreCase))
                    continue;

                JToken? commentTokenValue = null;
                if (!TryGetJsonPropertyValue(itemObject, "comment", out commentTokenValue)
                    && !TryGetJsonPropertyValue(itemObject, "text", out commentTokenValue)
                    && !TryGetJsonPropertyValue(itemObject, "message", out commentTokenValue))
                {
                    continue;
                }

                string commentText = NormalizeGeneratedSocialCommentText(npcName, commentTokenValue?.ToString());
                if (!string.IsNullOrWhiteSpace(commentText))
                    comments[npcName] = commentText;
            }

            return comments.Count > 0;
        }

        private static bool TryParseGeneratedNpcSocialCommentsBatch(string responseText, IReadOnlyDictionary<string, string[]> expectedCommentersByPost, out Dictionary<string, Dictionary<string, string>> commentsByPost)
        {
            commentsByPost = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            if (expectedCommentersByPost == null || expectedCommentersByPost.Count == 0)
                return false;

            string jsonPayload = ExtractJsonPayload(responseText);
            if (string.IsNullOrWhiteSpace(jsonPayload))
                return false;

            try
            {
                JToken token = JToken.Parse(jsonPayload);
                return TryPopulateGeneratedNpcSocialCommentsBatch(token, expectedCommentersByPost, commentsByPost);
            }
            catch (JsonReaderException)
            {
                return false;
            }
        }

        private static bool TryPopulateGeneratedNpcSocialCommentsBatch(JToken token, IReadOnlyDictionary<string, string[]> expectedCommentersByPost, Dictionary<string, Dictionary<string, string>> commentsByPost)
        {
            if (token == null)
                return false;

            if (token.Type == JTokenType.Object)
            {
                JObject obj = (JObject)token;

                if ((TryGetJsonPropertyValue(obj, "posts", out JToken? postsToken)
                        || TryGetJsonPropertyValue(obj, "results", out postsToken)
                        || TryGetJsonPropertyValue(obj, "commentsByPost", out postsToken)
                        || TryGetJsonPropertyValue(obj, "comments_by_post", out postsToken))
                    && postsToken != null)
                {
                    TryPopulateGeneratedNpcSocialCommentsBatch(postsToken, expectedCommentersByPost, commentsByPost);
                }

                string postId = string.Empty;
                if (TryGetJsonPropertyValue(obj, "id", out JToken? idToken)
                    || TryGetJsonPropertyValue(obj, "postId", out idToken)
                    || TryGetJsonPropertyValue(obj, "post_id", out idToken))
                {
                    postId = (idToken?.ToString() ?? string.Empty).Trim();
                }

                if (!string.IsNullOrWhiteSpace(postId)
                    && expectedCommentersByPost.TryGetValue(postId, out string[]? expectedCommentersForId)
                    && expectedCommentersForId != null
                    && expectedCommentersForId.Length > 0)
                {
                    var postComments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (TryPopulateGeneratedNpcSocialComments(obj, expectedCommentersForId, postComments))
                        commentsByPost[postId] = postComments;
                }

                foreach (KeyValuePair<string, string[]> entry in expectedCommentersByPost)
                {
                    string expectedPostId = entry.Key;
                    string[] expectedCommenters = entry.Value;
                    if (string.IsNullOrWhiteSpace(expectedPostId)
                        || expectedCommenters == null
                        || expectedCommenters.Length == 0)
                    {
                        continue;
                    }

                    if (!TryGetJsonPropertyValue(obj, expectedPostId, out JToken? postCommentsToken) || postCommentsToken == null)
                        continue;

                    var postComments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    if (TryPopulateGeneratedNpcSocialComments(postCommentsToken, expectedCommenters, postComments))
                        commentsByPost[expectedPostId] = postComments;
                }

                return commentsByPost.Count > 0;
            }

            if (token.Type != JTokenType.Array)
                return false;

            foreach (JToken item in (JArray)token)
            {
                TryPopulateGeneratedNpcSocialCommentsBatch(item, expectedCommentersByPost, commentsByPost);
            }

            return commentsByPost.Count > 0;
        }

        private static bool TryParseGeneratedNpcSocialPosts(string responseText, IEnumerable<string> expectedPostIds, out Dictionary<string, string> posts)
        {
            posts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string[] normalizedPostIds = (expectedPostIds ?? Enumerable.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (normalizedPostIds.Length == 0)
                return false;

            string jsonPayload = ExtractJsonPayload(responseText);
            if (string.IsNullOrWhiteSpace(jsonPayload))
                return false;

            try
            {
                JToken token = JToken.Parse(jsonPayload);
                return TryPopulateGeneratedNpcSocialPosts(token, normalizedPostIds, posts);
            }
            catch (JsonReaderException)
            {
                return false;
            }
        }

        private static bool TryPopulateGeneratedNpcSocialPosts(JToken token, IReadOnlyCollection<string> expectedPostIds, Dictionary<string, string> posts)
        {
            if (token == null)
                return false;

            if (token.Type == JTokenType.Object)
            {
                JObject obj = (JObject)token;

                if ((TryGetJsonPropertyValue(obj, "posts", out JToken? postsToken)
                        || TryGetJsonPropertyValue(obj, "post", out postsToken)
                        || TryGetJsonPropertyValue(obj, "results", out postsToken))
                    && postsToken != null)
                {
                    return TryPopulateGeneratedNpcSocialPosts(postsToken, expectedPostIds, posts);
                }

                string postId = string.Empty;
                if (TryGetJsonPropertyValue(obj, "id", out JToken? idToken)
                    || TryGetJsonPropertyValue(obj, "postId", out idToken)
                    || TryGetJsonPropertyValue(obj, "post_id", out idToken))
                {
                    postId = (idToken?.ToString() ?? string.Empty).Trim();
                }

                if (!string.IsNullOrWhiteSpace(postId) && expectedPostIds.Contains(postId, StringComparer.OrdinalIgnoreCase))
                {
                    JToken? postTextToken = null;
                    if (!TryGetJsonPropertyValue(obj, "text", out postTextToken)
                        && !TryGetJsonPropertyValue(obj, "message", out postTextToken)
                        && !TryGetJsonPropertyValue(obj, "postText", out postTextToken)
                        && !TryGetJsonPropertyValue(obj, "post_text", out postTextToken))
                    {
                        return posts.Count > 0;
                    }

                    string postText = ExtractGeneratedSocialPostText(postTextToken);
                    if (!string.IsNullOrWhiteSpace(postText))
                        posts[postId] = postText;

                    return posts.Count > 0;
                }

                foreach (string expectedPostId in expectedPostIds)
                {
                    if (!TryGetJsonPropertyValue(obj, expectedPostId, out JToken? postTextToken))
                        continue;

                    string postText = ExtractGeneratedSocialPostText(postTextToken);
                    if (!string.IsNullOrWhiteSpace(postText))
                        posts[expectedPostId] = postText;
                }

                return posts.Count > 0;
            }

            if (token.Type != JTokenType.Array)
                return false;

            foreach (JToken item in (JArray)token)
            {
                if (item.Type != JTokenType.Object)
                    continue;

                JObject itemObject = (JObject)item;
                string postId = string.Empty;
                if (TryGetJsonPropertyValue(itemObject, "id", out JToken? idToken)
                    || TryGetJsonPropertyValue(itemObject, "postId", out idToken)
                    || TryGetJsonPropertyValue(itemObject, "post_id", out idToken))
                {
                    postId = (idToken?.ToString() ?? string.Empty).Trim();
                }

                if (string.IsNullOrWhiteSpace(postId) || !expectedPostIds.Contains(postId, StringComparer.OrdinalIgnoreCase))
                    continue;

                JToken? postTextToken = null;
                if (!TryGetJsonPropertyValue(itemObject, "text", out postTextToken)
                    && !TryGetJsonPropertyValue(itemObject, "message", out postTextToken)
                    && !TryGetJsonPropertyValue(itemObject, "postText", out postTextToken)
                    && !TryGetJsonPropertyValue(itemObject, "post_text", out postTextToken))
                {
                    continue;
                }

                string postText = ExtractGeneratedSocialPostText(postTextToken);
                if (!string.IsNullOrWhiteSpace(postText))
                    posts[postId] = postText;
            }

            return posts.Count > 0;
        }

        private static string ExtractGeneratedSocialPostText(JToken? textToken)
        {
            if (textToken == null)
                return string.Empty;

            if (textToken.Type == JTokenType.Object)
            {
                JObject textObject = (JObject)textToken;
                if (TryGetJsonPropertyValue(textObject, "text", out JToken? nestedText)
                    || TryGetJsonPropertyValue(textObject, "message", out nestedText)
                    || TryGetJsonPropertyValue(textObject, "post", out nestedText)
                    || TryGetJsonPropertyValue(textObject, "value", out nestedText))
                {
                    return NormalizeGeneratedSocialPostText(nestedText?.ToString());
                }
            }

            if (textToken.Type == JTokenType.Array)
            {
                string merged = string.Join(" ",
                    ((JArray)textToken)
                        .Select(entry => entry?.ToString() ?? string.Empty)
                        .Where(entry => !string.IsNullOrWhiteSpace(entry)));

                return NormalizeGeneratedSocialPostText(merged);
            }

            return NormalizeGeneratedSocialPostText(textToken.ToString());
        }

        private static bool TryGetJsonPropertyValue(JObject obj, string propertyName, out JToken? value)
        {
            foreach (JProperty property in obj.Properties())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static string NormalizeGeneratedSocialPostText(string? postText)
        {
            string normalizedText = string.Join(" ", (postText ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                .Trim();

            if ((normalizedText.StartsWith("\"") && normalizedText.EndsWith("\"") && normalizedText.Length > 1)
                || (normalizedText.StartsWith("'") && normalizedText.EndsWith("'") && normalizedText.Length > 1))
            {
                normalizedText = normalizedText.Substring(1, normalizedText.Length - 2).Trim();
            }

            return normalizedText;
        }

        private static string NormalizeGeneratedSocialCommentText(string npcName, string? commentText)
        {
            string normalizedText = string.Join(" ", (commentText ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                .Trim();

            if (normalizedText.StartsWith($"{npcName}:", StringComparison.OrdinalIgnoreCase))
                normalizedText = normalizedText.Substring(npcName.Length + 1).TrimStart();

            if ((normalizedText.StartsWith("\"") && normalizedText.EndsWith("\"") && normalizedText.Length > 1)
                || (normalizedText.StartsWith("'") && normalizedText.EndsWith("'") && normalizedText.Length > 1))
            {
                normalizedText = normalizedText.Substring(1, normalizedText.Length - 2).Trim();
            }

            return normalizedText;
        }

        private static string ExtractJsonPayload(string responseText)
        {
            string trimmed = (responseText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                return string.Empty;

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                int firstLineBreak = trimmed.IndexOf('\n');
                if (firstLineBreak >= 0)
                    trimmed = trimmed.Substring(firstLineBreak + 1).Trim();

                int closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
                if (closingFence >= 0)
                    trimmed = trimmed.Substring(0, closingFence).Trim();
            }

            if (trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal))
                return trimmed;

            int objectStart = trimmed.IndexOf('{');
            int objectEnd = trimmed.LastIndexOf('}');
            if (objectStart >= 0 && objectEnd > objectStart)
                return trimmed.Substring(objectStart, objectEnd - objectStart + 1).Trim();

            int arrayStart = trimmed.IndexOf('[');
            int arrayEnd = trimmed.LastIndexOf(']');
            if (arrayStart >= 0 && arrayEnd > arrayStart)
                return trimmed.Substring(arrayStart, arrayEnd - arrayStart + 1).Trim();

            return trimmed;
        }

        public static async Task<(int, int)> GetOpenAIUsage()
        {
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

                SMonitor.Log($"Premium models: {premiumInputTotal} input tokens, {premiumOutputTotal} output tokens.\nRegular models: {regularInputTotal} input tokens, {regularOutputTotal} output tokens.", LogLevel.Error);
                return ((int)(premiumInputTotal + premiumOutputTotal), (int)(regularInputTotal + regularOutputTotal));
            }
            catch (Exception ex)
            {
                SMonitor.Log($"Request failed: {ex.Message}", LogLevel.Error);
                return (-1, -1);
            }
        }
        public static string BuildResponseConversationUserInput(NPC npc, string fallbackText)
        {
            if (npc == null || string.IsNullOrWhiteSpace(npc.Name))
                return (fallbackText ?? string.Empty).Trim();

            var messagesHistory = npcMessagesToday.ContainsKey(npc.Name)
                ? npcMessagesToday[npc.Name]
                : new List<string>();
            string conversation = BuildConversationPreviewForAi(messagesHistory, npc.Name, maxLines: 12);

            if (!string.IsNullOrWhiteSpace(conversation))
                return conversation;

            return (fallbackText ?? string.Empty).Trim();
        }

    }
}
