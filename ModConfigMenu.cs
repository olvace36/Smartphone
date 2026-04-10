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

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => "Show event trigger helper menu.",
                tooltip: () => "Only effective if you have Unlimited Event Expansion mod.\nOption to display side menu for quick event trigger.\nAlways: Always show.\nMinimal: Only show when last message contain sepecific keyword (e.g. Dinner).\nNever: Never show helper menu.",
                getValue: () => Config.HelperOption,
                setValue: value => Config.HelperOption = value,
                allowedValues: new string[] { "Always", "Minimal", "Never" },
                formatAllowedValue: value => value
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

            // advance settings
            configMenu.AddSectionTitle(mod: ModManifest, () => "Advance Settings");
            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => "OpenAI API Key",
                tooltip: () => "You can provide your own key to avoid usage limit and able to customize conversation summary limit below.",
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

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => "Show StardewConnect image tags",
                tooltip: () => "Show the saved image tag text above attached images in StardewConnect posts.",
                getValue: () => Config.ShowSocialImageTags,
                setValue: value => Config.ShowSocialImageTags = value
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
        }

    }

}