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

            string[] aiModelValues =
            {
                ModConfig.OpenAIModel_51,
                ModConfig.OpenAIModel_5mini,
                ModConfig.OpenAIModel_5nano,
                ModConfig.OpenAIModel_54mini,
                ModConfig.OpenAIModel_54nano,
                ModConfig.GeminiModel_35Flash,
                ModConfig.GeminiModel_31FlashLite,
                ModConfig.GeminiModel_3FlashPreview
            };

            string[] characteristicValues =
            {
                ModConfig.CharacteristicModeMinimal,
                ModConfig.CharacteristicModeShort,
                ModConfig.CharacteristicModeLong
            };

            string[] newMessageChanceValues =
            {
                ModConfig.NewMessageChanceDefault,
                ModConfig.NewMessageChanceLow
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
                reset: () =>
                {
                    Config = new ModConfig();
                    RefreshIgnoredNpcList();
                },
                save: () =>
                {
                    Helper.WriteConfig(Config);
                    RefreshIgnoredNpcList();
                }
            );

            // main page: options most players change often
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.title.quick_setup"));

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.enable_ai"),
                tooltip: () => Helper.Translation.Get("config.tooltip.enable_ai"),
                getValue: () => Config.EnableAI,
                setValue: value => Config.EnableAI = value
            );

            configMenu.AddKeybind(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.open_phone_key"),
                tooltip: () => Helper.Translation.Get("config.tooltip.open_phone_key"),
                getValue: () => Config.ModKey,
                setValue: value => Config.ModKey = value
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.language"),
                tooltip: () => Helper.Translation.Get("config.tooltip.language"),
                getValue: () => string.IsNullOrWhiteSpace(Config.Language) ? "English" : Config.Language,
                setValue: value => Config.Language = string.IsNullOrWhiteSpace(value) ? "English" : value.Trim()
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.who_can_message"),
                tooltip: () => Helper.Translation.Get("config.tooltip.who_can_message"),
                getValue: () => EnsureAllowedValue(Config.NpcMessageRequirement, ModConfig.NpcRequirementFriend, npcRequirementValues),
                setValue: value => Config.NpcMessageRequirement = value,
                allowedValues: npcRequirementValues,
                formatAllowedValue: value => value switch
                {
                    ModConfig.NpcRequirementMeet => Helper.Translation.Get("config.value.meet"),
                    ModConfig.NpcRequirementFriend => Helper.Translation.Get("config.value.friend"),
                    _ => value
                }
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.disable_daily_message"),
                tooltip: () => Helper.Translation.Get("config.tooltip.disable_daily_message"),
                getValue: () => Config.DisableDailyMessage,
                setValue: value => Config.DisableDailyMessage = value
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.disable_update_warning"),
                tooltip: () => Helper.Translation.Get("config.tooltip.disable_update_warning"),
                getValue: () => Config.DisableUpdateWarning,
                setValue: value => Config.DisableUpdateWarning = value
            );

            configMenu.AddPageLink(
                mod: ModManifest,
                pageId: "ai-settings",
                text: () => Helper.Translation.Get("config.page.ai_settings"),
                tooltip: () => Helper.Translation.Get("config.tooltip.ai_settings")
            );

            configMenu.AddPageLink(
                mod: ModManifest,
                pageId: "storage-limits",
                text: () => Helper.Translation.Get("config.page.storage_limits"),
                tooltip: () => Helper.Translation.Get("config.tooltip.storage_limits")
            );

            configMenu.AddPageLink(
                mod: ModManifest,
                pageId: "display",
                text: () => Helper.Translation.Get("config.page.display"),
                tooltip: () => Helper.Translation.Get("config.tooltip.display")
            );

            configMenu.AddPageLink(
                mod: ModManifest,
                pageId: "notifications",
                text: () => Helper.Translation.Get("config.page.notifications"),
                tooltip: () => Helper.Translation.Get("config.tooltip.notifications")
            );

            configMenu.AddPageLink(
                mod: ModManifest,
                pageId: "misc",
                text: () => Helper.Translation.Get("config.page.misc"),
                tooltip: () => Helper.Translation.Get("config.tooltip.misc")
            );

            // AI Settings page
            configMenu.AddPage(mod: ModManifest, pageId: "ai-settings", pageTitle: () => Helper.Translation.Get("config.page.ai_settings"));
            configMenu.AddParagraph(
                mod: ModManifest,
                text: () => Helper.Translation.Get("config.paragraph.ai_settings")
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.key"),
                tooltip: () => Helper.Translation.Get("config.tooltip.key"),
                getValue: () => Config.Key,
                setValue: value => Config.Key = value
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.model"),
                tooltip: () => Helper.Translation.Get("config.tooltip.model"),
                getValue: () => EnsureAllowedValue(Config.Model, ModConfig.OpenAIModel_54mini, aiModelValues),
                setValue: value => Config.Model = value,
                allowedValues: aiModelValues,
                formatAllowedValue: value => value switch
                {
                    ModConfig.OpenAIModel_54mini => Helper.Translation.Get("config.value.model.gpt54mini"),
                    ModConfig.OpenAIModel_54nano => Helper.Translation.Get("config.value.model.gpt54nano"),
                    ModConfig.OpenAIModel_51 => Helper.Translation.Get("config.value.model.gpt51"),
                    ModConfig.OpenAIModel_5mini => Helper.Translation.Get("config.value.model.gpt5mini"),
                    ModConfig.OpenAIModel_5nano => Helper.Translation.Get("config.value.model.gpt5nano"),
                    ModConfig.GeminiModel_35Flash => Helper.Translation.Get("config.value.model.gemini35flash"),
                    ModConfig.GeminiModel_31FlashLite => Helper.Translation.Get("config.value.model.gemini31flashlite"),
                    ModConfig.GeminiModel_3FlashPreview => Helper.Translation.Get("config.value.model.gemini3flashpreview"),
                    _ => value
                }
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.social_activity"),
                tooltip: () => Helper.Translation.Get("config.tooltip.social_activity"),
                getValue: () => EnsureAllowedValue(Config.PostPerDay, ModConfig.PostPerDayMedium, postPerDayValues),
                setValue: value => Config.PostPerDay = value,
                allowedValues: postPerDayValues,
                formatAllowedValue: value => value switch
                {
                    ModConfig.PostPerDayHigh => Helper.Translation.Get("config.value.social_activity.high"),
                    ModConfig.PostPerDayMedium => Helper.Translation.Get("config.value.social_activity.medium"),
                    ModConfig.PostPerDayLow => Helper.Translation.Get("config.value.social_activity.low"),
                    _ => value
                }
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.npc_characteristic"),
                tooltip: () => Helper.Translation.Get("config.tooltip.npc_characteristic"),
                getValue: () => EnsureAllowedValue(Config.CharacteristicMode, ModConfig.CharacteristicModeShort, characteristicValues),
                setValue: value => Config.CharacteristicMode = value,
                allowedValues: characteristicValues,
                formatAllowedValue: value => value switch
                {
                    ModConfig.CharacteristicModeMinimal => Helper.Translation.Get("config.value.characteristic.minimal"),
                    ModConfig.CharacteristicModeShort => Helper.Translation.Get("config.value.characteristic.short"),
                    ModConfig.CharacteristicModeLong => Helper.Translation.Get("config.value.characteristic.long"),
                    _ => value
                }
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.high_quality_comment"),
                tooltip: () => Helper.Translation.Get("config.tooltip.high_quality_comment"),
                getValue: () => Config.BetterQualityComment,
                setValue: value => Config.BetterQualityComment = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.summary_max_words"),
                tooltip: () => Helper.Translation.Get("config.tooltip.summary_max_words"),
                getValue: () => Config.MaxSummaryWordCount,
                setValue: value => Config.MaxSummaryWordCount = Math.Clamp(value, 0, 5000),
                min: 0,
                max: 5000
            );

            // storage and limits page
            configMenu.AddPage(mod: ModManifest, pageId: "storage-limits", pageTitle: () => Helper.Translation.Get("config.page.storage_limits"));

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.posts_to_keep"),
                tooltip: () => Helper.Translation.Get("config.tooltip.posts_to_keep"),
                getValue: () => Config.MaxStardewConnectPosts,
                setValue: value => Config.MaxStardewConnectPosts = Math.Clamp(value, 10, 500),
                min: 10,
                max: 500
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.player_photos_to_keep"),
                tooltip: () => Helper.Translation.Get("config.tooltip.player_photos_to_keep"),
                getValue: () => Config.PlayerMaxPhoto,
                setValue: value => Config.PlayerMaxPhoto = Math.Clamp(value, 1, 500),
                min: 1,
                max: 500
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.npc_photos_to_keep"),
                tooltip: () => Helper.Translation.Get("config.tooltip.npc_photos_to_keep"),
                getValue: () => Config.NpcMaxPhoto,
                setValue: value => Config.NpcMaxPhoto = Math.Clamp(value, 1, 500),
                min: 1,
                max: 500
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.messages_per_npc"),
                tooltip: () => Helper.Translation.Get("config.tooltip.messages_per_npc"),
                getValue: () => Config.MaxMessage,
                setValue: value => Config.MaxMessage = Math.Clamp(value, 50, 5000),
                min: 50,
                max: 5000
            );

            // display page
            configMenu.AddPage(mod: ModManifest, pageId: "display", pageTitle: () => Helper.Translation.Get("config.page.display"));

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.show_ai_credit"),
                tooltip: () => Helper.Translation.Get("config.tooltip.show_ai_credit"),
                getValue: () => Config.ShowAiCredit,
                setValue: value => Config.ShowAiCredit = value
            );
            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.show_social_image_tags"),
                tooltip: () => Helper.Translation.Get("config.tooltip.show_social_image_tags"),
                getValue: () => Config.ShowSocialImageTags,
                setValue: value => Config.ShowSocialImageTags = value
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.show_unread_comment"),
                tooltip: () => Helper.Translation.Get("config.tooltip.show_unread_comment"),
                getValue: () => Config.ShowUnreadComment,
                setValue: value => Config.ShowUnreadComment = value
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.show_message_image_tags"),
                tooltip: () => Helper.Translation.Get("config.tooltip.show_message_image_tags"),
                getValue: () => Config.ShowMessageImageTags,
                setValue: value => Config.ShowMessageImageTags = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.camera_flash_radius"),
                tooltip: () => Helper.Translation.Get("config.tooltip.camera_flash_radius"),
                getValue: () => Math.Clamp(Config.PlayerCaptureWorldFlashRadius, 1f, 10f),
                setValue: value =>
                {
                    float clamped = Math.Clamp(value, 1f, 10f);
                    Config.PlayerCaptureWorldFlashRadius = MathF.Round(clamped * 10f) / 10f;
                },
                min: 1f,
                max: 10f,
                interval: 0.1f,
                formatValue: value => $"{value:0.0}"
            );

            configMenu.AddParagraph(
                mod: ModManifest,
                text: () => Helper.Translation.Get("config.paragraph.phone_size")
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.small_phone_size"),
                tooltip: () => Helper.Translation.Get("config.tooltip.small_phone_size"),
                getValue: () => Config.UseSmallPhoneSize,
                setValue: value => Config.UseSmallPhoneSize = value
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.show_phone_icon"),
                tooltip: () => Helper.Translation.Get("config.tooltip.show_phone_icon"),
                getValue: () => Config.ShowPhoneIcon,
                setValue: value => Config.ShowPhoneIcon = value
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.phone_icon_x"),
                tooltip: () => Helper.Translation.Get("config.tooltip.phone_icon_x"),
                getValue: () => Config.HudPhoneIconOffsetX,
                setValue: value => Config.HudPhoneIconOffsetX = Math.Clamp(value, -2000, 2000),
                min: -2000,
                max: 2000,
                interval: 1
            );

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.phone_icon_y"),
                tooltip: () => Helper.Translation.Get("config.tooltip.phone_icon_y"),
                getValue: () => Config.HudPhoneIconOffsetY,
                setValue: value => Config.HudPhoneIconOffsetY = Math.Clamp(value, -2000, 2000),
                min: -2000,
                max: 2000,
                interval: 1
            );

            // notification page
            configMenu.AddPage(mod: ModManifest, pageId: "notifications", pageTitle: () => Helper.Translation.Get("config.page.notifications"));

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.notification_popups"),
                tooltip: () => Helper.Translation.Get("config.tooltip.notification_popups"),
                getValue: () => Config.notifyNotification,
                setValue: value => Config.notifyNotification = value
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.message_popups"),
                tooltip: () => Helper.Translation.Get("config.tooltip.message_popups"),
                getValue: () => Config.notifyMessage,
                setValue: value => Config.notifyMessage = value
            );

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.social_popups"),
                tooltip: () => Helper.Translation.Get("config.tooltip.social_popups"),
                getValue: () => Config.notifyStardewSocial,
                setValue: value => Config.notifyStardewSocial = value
            );

            // Misc page
            configMenu.AddPage(mod: ModManifest, pageId: "misc", pageTitle: () => Helper.Translation.Get("config.page.misc_title"));

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.new_message_chance"),
                tooltip: () => Helper.Translation.Get("config.tooltip.new_message_chance"),
                getValue: () => EnsureAllowedValue(Config.NewMessageChance, ModConfig.NewMessageChanceDefault, newMessageChanceValues),
                setValue: value => Config.NewMessageChance = value,
                allowedValues: newMessageChanceValues,
                formatAllowedValue: value => value switch
                {
                    ModConfig.NewMessageChanceDefault => Helper.Translation.Get("config.value.new_message_chance.default"),
                    ModConfig.NewMessageChanceLow => Helper.Translation.Get("config.value.new_message_chance.low"),
                    _ => value
                }
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.ignored_npcs"),
                tooltip: () => Helper.Translation.Get("config.tooltip.ignored_npcs"),
                getValue: () => string.IsNullOrWhiteSpace(Config.IgnoredNpc) ? string.Empty : Config.IgnoredNpc,
                setValue: value =>
                {
                    Config.IgnoredNpc = value ?? string.Empty;
                    RefreshIgnoredNpcList();
                }
            );
        }

    }

}