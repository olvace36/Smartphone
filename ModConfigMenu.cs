using ContentPatcher;
using StardewModdingAPI;

namespace Smartphone
{

    public partial class ModEntry
    {

        public static void ConfigMenu (IContentPatcherAPI api, IManifest ModManifest, IModHelper Helper)
        {
            

            // get Generic Mod Config Menu's API (if it's installed)
            var configMenu = Helper.ModRegistry.GetApi<Smartphone.Data.IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null)
                return;

            // register mod
            configMenu.Register(
                mod: ModManifest,
                reset: () => Config = new ModConfig(),
                save: () => Helper.WriteConfig(Config)
            );


            configMenu.AddKeybind(
                mod: ModManifest,
                name: () => "Mod Key",
                getValue: () => Config.ModKey,
                setValue: value => Config.ModKey = value
            );

            configMenu.AddParagraph(
                mod: ModManifest,
                text: () => "Controls which villagers appear in the Messages app."
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => "NPC message requirement",
                tooltip: () => "No requirement: see everyone from the start.\nMeet: only NPCs you've already spoken to.\nFriend: requires 1+ hearts.",
                getValue: () => Config.NpcMessageRequirement,
                setValue: value => Config.NpcMessageRequirement = value,
                allowedValues: new string[]
                {
                    ModConfig.NpcRequirementNoRequirement,
                    ModConfig.NpcRequirementMeet,
                    ModConfig.NpcRequirementFriend
                },
                formatAllowedValue: value => value
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => "Post per day",
                tooltip: () => "High: more frequent social updates (posts every 3h, engagement every 2h).\nMedium: balanced frequency (posts every 4h, engagement every 3h).\nLow: fewer updates (posts every 6h, engagement every 4h).",
                getValue: () => Config.PostPerDay,
                setValue: value => Config.PostPerDay = value,
                allowedValues: new string[]
                {
                    ModConfig.PostPerDayHigh,
                    ModConfig.PostPerDayMedium,
                    ModConfig.PostPerDayLow
                },
                formatAllowedValue: value => value
            );

            // advance settings
            configMenu.AddSectionTitle(mod: ModManifest, () => "Advance Settings");
            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => "OpenAI API Key",
                tooltip: () => "You can provide your own key to avoid usage limit. Cost are expected to be very low (1 to 8 cents per game day depending on your settings).\nYou can get your API key from https://platform.openai.com/account/api-keys.\nRestart your game after changing the key. ",
                getValue: () => Config.OpenAIKey,
                setValue: value => Config.OpenAIKey = value.Trim()
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Max summary word count",
                tooltip: () => "Limit the maximum word count for the conversation summary generated. Only effective if you have provided OpenAI API key.",
                getValue: () => Config.MaxSummaryWordCount,
                setValue: value => Config.MaxSummaryWordCount = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Max characteristic character count",
                tooltip: () => "Max character count for each NPC characteristic. Only effective if you have provided OpenAI API key.",
                getValue: () => Config.MaxCharacteristicCharacterCount,
                setValue: value => Config.MaxCharacteristicCharacterCount = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "StardewConnect max posts",
                tooltip: () => "Maximum number of StardewConnect posts to keep when the day saves. Older posts are removed first.",
                getValue: () => Config.MaxStardewConnectPosts,
                setValue: value => Config.MaxStardewConnectPosts = Math.Clamp(value, 10, 500)
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Player photo max",
                tooltip: () => "Maximum number of player photos to keep. Older photos are deleted automatically when the limit is exceeded.",
                getValue: () => Config.PlayerMaxPhoto,
                setValue: value => Config.PlayerMaxPhoto = Math.Clamp(value, 1, 500),
                min: 1,
                max: 500
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "NPC photo max",
                tooltip: () => "Maximum number of NPC photos to keep. Older photos are deleted automatically when the limit is exceeded.",
                getValue: () => Config.NpcMaxPhoto,
                setValue: value => Config.NpcMaxPhoto = Math.Clamp(value, 1, 500),
                min: 1,
                max: 500
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => "Messages max per NPC",
                tooltip: () => "Maximum number of stored messages per NPC in the Text app. Older messages are removed first.",
                getValue: () => Config.MaxMessage,
                setValue: value => Config.MaxMessage = Math.Clamp(value, 50, 5000)
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Show StardewConnect image tags",
                tooltip: () => "Show the saved image tag text above attached images in StardewConnect posts.",
                getValue: () => Config.ShowSocialImageTags,
                setValue: value => Config.ShowSocialImageTags = value
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Show message image tags",
                tooltip: () => "Show attached message image tags when hovering photo bubbles in Text app.",
                getValue: () => Config.ShowMessageImageTags,
                setValue: value => Config.ShowMessageImageTags = value
            );

            configMenu.AddSectionTitle(mod: ModManifest, () => "HUD Notifications");

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Notify notification app",
                tooltip: () => "Show HUD popups for Notification app updates.",
                getValue: () => Config.notifyNotification,
                setValue: value => Config.notifyNotification = value
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Notify messages",
                tooltip: () => "Show HUD popups for new NPC messages.",
                getValue: () => Config.notifyMessage,
                setValue: value => Config.notifyMessage = value
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Notify StardewConnect",
                tooltip: () => "Show HUD popups for new StardewConnect social notifications.",
                getValue: () => Config.notifyStardewSocial,
                setValue: value => Config.notifyStardewSocial = value
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Disable Daily Message",
                tooltip: () => "Disable the daily message feature.",
                getValue: () => Config.DisableDailyMessage,
                setValue: value => Config.DisableDailyMessage = value
            );
        }

    }

}