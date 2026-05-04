using ContentPatcher;
using StardewModdingAPI;

namespace Smartphone
{

    public partial class ModEntry
    {

        public static void ConfigMenu(IContentPatcherAPI api, IManifest ModManifest, IModHelper Helper)
        {
            // get Generic Mod Config Menu's API (if it's installed)
            var configMenu = Helper.ModRegistry.GetApi<Smartphone.Data.IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            string[] npcRequirementValues =
            {
                ModConfig.NpcRequirementMeet,
                ModConfig.NpcRequirementFriend
            };

            string[] postPerDayValues =
            {
                ModConfig.PostPerDayHigh,
                ModConfig.PostPerDayMedium,
                ModConfig.PostPerDayLow
            };

            string[] openAiModelValues =
            {
                ModConfig.OpenAIModel_51,
                ModConfig.OpenAIModel_5mini,
                ModConfig.OpenAIModel_5nano,
                ModConfig.OpenAIModel_54mini,
                ModConfig.OpenAIModel_54nano
            };

            string[] characteristicValues =
            {
                ModConfig.CharacteristicModeMinimal,
                ModConfig.CharacteristicModeShort,
                ModConfig.CharacteristicModeLong
            };

            static string EnsureAllowedValue(string? value, string fallback, string[] allowedValues)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return fallback;

                return Array.IndexOf(allowedValues, value) >= 0 ? value : fallback;
            }

            // register mod
            configMenu.Register(
                mod: ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => Helper.WriteConfig(Config)
            );

            // main page: options most players change often
            configMenu.AddSectionTitle(mod: ModManifest, text: () => "Quick Setup");

            configMenu.AddKeybind(
                mod: ModManifest,
                name: () => "Open phone key",
                tooltip: () => "The key used to open your in-game smartphone.",
                getValue: () => Config.ModKey,
                setValue: value => Config.ModKey = value
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => "Who can message you",
                tooltip: () => "Meet: NPC must be talked to at least once.\nFriend: NPC must have at least 1 heart.",
                getValue: () => EnsureAllowedValue(Config.NpcMessageRequirement, ModConfig.NpcRequirementFriend, npcRequirementValues),
                setValue: value => Config.NpcMessageRequirement = value,
                allowedValues: npcRequirementValues,
                formatAllowedValue: value => value switch
                {
                    ModConfig.NpcRequirementMeet => "Meet",
                    ModConfig.NpcRequirementFriend => "Friend (1+ heart)",
                    _ => value
                }
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Disable daily message",
                tooltip: () => "If enabled, the `Good morning...` option will be disabled.\nYou should keep this option False which overtime will help the AI understand what is lore of the character.",
                getValue: () => Config.DisableDailyMessage,
                setValue: value => Config.DisableDailyMessage = value
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Show AI credit button",
                tooltip: () => "You have a limited number of usage of the AI features each day when no OpenAI key is provided.\nThis option shows how many usages you have left. Check it at Messages -> NPC -> AI Credit.",
                getValue: () => Config.ShowAiCredit,
                setValue: value => Config.ShowAiCredit = value
            );

            configMenu.AddPageLink(
                mod: ModManifest,
                pageId: "ai-settings",
                text: () => "AI Settings",
                tooltip: () => "API key, model choice, and limits setting."
            );

            configMenu.AddPageLink(
                mod: ModManifest,
                pageId: "storage-limits",
                text: () => "Storage and Limits",
                tooltip: () => "How many messages, posts, and photos are kept on your computer."
            );

            configMenu.AddPageLink(
                mod: ModManifest,
                pageId: "display",
                text: () => "Display",
                tooltip: () => "Image tag visibility options."
            );

            configMenu.AddPageLink(
                mod: ModManifest,
                pageId: "notifications",
                text: () => "HUD Notifications",
                tooltip: () => "Toggle popup notifications for each app."
            );

            // AI Settings page
            configMenu.AddPage(mod: ModManifest, pageId: "ai-settings", pageTitle: () => "AI Settings");
            configMenu.AddParagraph(
                mod: ModManifest,
                text: () => "These settings are only effective when an OpenAI API key is provided."
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => "OpenAI API key",
                tooltip: () => "Use your own key to remove shared usage limits.\nGet one from https://platform.openai.com/account/api-keys.\nRestart the game after changing this value.",
                getValue: () => Config.OpenAIKey,
                setValue: value => Config.OpenAIKey = (value ?? string.Empty).Trim()
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => "OpenAI model",
                tooltip: () => "Chooses the model used for AI replies and summaries.",
                getValue: () => EnsureAllowedValue(Config.OpenAIModel, ModConfig.OpenAIModel_54mini, openAiModelValues),
                setValue: value => Config.OpenAIModel = value,
                allowedValues: openAiModelValues,
                formatAllowedValue: value => value switch
                {
                    ModConfig.OpenAIModel_54mini => "gpt-5.4-mini (recommended)",
                    ModConfig.OpenAIModel_54nano => "gpt-5.4-nano",
                    ModConfig.OpenAIModel_51 => "gpt-5.1",
                    ModConfig.OpenAIModel_5mini => "gpt-5-mini (2nd recommended)",
                    ModConfig.OpenAIModel_5nano => "gpt-5-nano (cheapest)",
                    _ => value
                }
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => "StardewConnect activity",
                tooltip: () => "Controls how often StardewConnect posts and engagement are generated.",
                getValue: () => EnsureAllowedValue(Config.PostPerDay, ModConfig.PostPerDayMedium, postPerDayValues),
                setValue: value => Config.PostPerDay = value,
                allowedValues: postPerDayValues,
                formatAllowedValue: value => value switch
                {
                    ModConfig.PostPerDayHigh => "High (more updates)",
                    ModConfig.PostPerDayMedium => "Medium (recommended)",
                    ModConfig.PostPerDayLow => "Low (default, fewer updates)",
                    _ => value
                }
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => "NPC characteristic detail",
                tooltip: () => "NPC characteristic give the AI a background for each NPC during the chat and the post generation.\nHigher detail improves quality but uses more tokens per NPC.",
                getValue: () => EnsureAllowedValue(Config.CharacteristicMode, ModConfig.CharacteristicModeShort, characteristicValues),
                setValue: value => Config.CharacteristicMode = value,
                allowedValues: characteristicValues,
                formatAllowedValue: value => value switch
                {
                    ModConfig.CharacteristicModeMinimal => "Minimal (lowest cost)",
                    ModConfig.CharacteristicModeShort => "Short (recommended)",
                    ModConfig.CharacteristicModeLong => "Long (highest detail)",
                    _ => value
                }
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "High quality comment",
                tooltip: () => "If enabled, AI will be provided with the minimal characteristic detail for each NPC.\nThis may improve the quality of comments but will use more tokens.",
                getValue: () => Config.BetterQualityComment,
                setValue: value => Config.BetterQualityComment = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Summary max words",
                tooltip: () => "Conversation are summarized daily so the AI knows what the previous conversation is about.\nHigher limit means AI will `remember` more details but will use more tokens.\nMemory saved at \"Userdata\\SaveFolder\\summary\".",
                getValue: () => Config.MaxSummaryWordCount,
                setValue: value => Config.MaxSummaryWordCount = Math.Clamp(value, 0, 5000),
                min: 0,
                max: 5000
            );

            // storage and limits page
            configMenu.AddPage(mod: ModManifest, pageId: "storage-limits", pageTitle: () => "Storage and Limits");

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "StardewConnect posts to keep",
                tooltip: () => "Older posts are removed first when this limit is exceeded.",
                getValue: () => Config.MaxStardewConnectPosts,
                setValue: value => Config.MaxStardewConnectPosts = Math.Clamp(value, 10, 500),
                min: 10,
                max: 500
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Player photos to keep",
                tooltip: () => "Older player photos are deleted first.",
                getValue: () => Config.PlayerMaxPhoto,
                setValue: value => Config.PlayerMaxPhoto = Math.Clamp(value, 1, 500),
                min: 1,
                max: 500
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "NPC photos to keep",
                tooltip: () => "Older NPC photos are deleted first.",
                getValue: () => Config.NpcMaxPhoto,
                setValue: value => Config.NpcMaxPhoto = Math.Clamp(value, 1, 500),
                min: 1,
                max: 500
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Messages per NPC",
                tooltip: () => "Older text messages are removed first when this limit is exceeded.",
                getValue: () => Config.MaxMessage,
                setValue: value => Config.MaxMessage = Math.Clamp(value, 50, 5000),
                min: 50,
                max: 5000
            );

            // display page
            configMenu.AddPage(mod: ModManifest, pageId: "display", pageTitle: () => "Display");

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Show StardewConnect image tags",
                tooltip: () => "Shows saved image tag text above attached images in StardewConnect posts.",
                getValue: () => Config.ShowSocialImageTags,
                setValue: value => Config.ShowSocialImageTags = value
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Show unread comment count",
                tooltip: () => "Shows unread comment count on posts.",
                getValue: () => Config.ShowUnreadComment,
                setValue: value => Config.ShowUnreadComment = value
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Show message image tags",
                tooltip: () => "Shows attached image tags when hovering photo bubbles in the Text app.",
                getValue: () => Config.ShowMessageImageTags,
                setValue: value => Config.ShowMessageImageTags = value
            );

            // notification page
            configMenu.AddPage(mod: ModManifest, pageId: "notifications", pageTitle: () => "HUD Notifications");

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Notification app popups",
                tooltip: () => "Show HUD popups for Notification app updates.",
                getValue: () => Config.notifyNotification,
                setValue: value => Config.notifyNotification = value
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Message popups",
                tooltip: () => "Show HUD popups for new NPC text messages.",
                getValue: () => Config.notifyMessage,
                setValue: value => Config.notifyMessage = value
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "StardewConnect popups",
                tooltip: () => "Show HUD popups for new StardewConnect activity.",
                getValue: () => Config.notifyStardewSocial,
                setValue: value => Config.notifyStardewSocial = value
            );
        }

    }

}