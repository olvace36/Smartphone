using ContentPatcher;
using StardewModdingAPI;
using System;

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

            // register mod
            configMenu.Register(
                mod: ModManifest,
                reset: () =>
                {
                    Config = new ModConfig();
                },
                save: () =>
                {
                    Helper.WriteConfig(Config);
                }
            );

            // main page: options most players change often
            configMenu.AddSectionTitle(mod: ModManifest, text: () => Helper.Translation.Get("config.title.quick_setup"));

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

            configMenu.AddBoolOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.disable_update_warning"),
                tooltip: () => Helper.Translation.Get("config.tooltip.disable_update_warning"),
                getValue: () => Config.DisableUpdateWarning,
                setValue: value => Config.DisableUpdateWarning = value
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

            // storage and limits page
            configMenu.AddPage(mod: ModManifest, pageId: "storage-limits", pageTitle: () => Helper.Translation.Get("config.page.storage_limits"));

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

            // display page
            configMenu.AddPage(mod: ModManifest, pageId: "display", pageTitle: () => Helper.Translation.Get("config.page.display"));

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

            configMenu.AddNumberOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.phone_size"),
                tooltip: () => Helper.Translation.Get("config.tooltip.phone_size"),
                getValue: () => Math.Clamp(Config.PhoneSize, 0.7f, 1.5f),
                setValue: value =>
                {
                    float clamped = Math.Clamp(value, 0.7f, 1.5f);
                    Config.PhoneSize = MathF.Round(clamped * 10f) / 10f;
                },
                min: 0.7f,
                max: 1.5f,
                interval: 0.1f,
                formatValue: value => $"{value:0.0}"
            );

            configMenu.AddKeybind(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.decrease_phone_size_key"),
                tooltip: () => Helper.Translation.Get("config.tooltip.decrease_phone_size_key"),
                getValue: () => Config.DecreasePhoneSizeKey,
                setValue: value => Config.DecreasePhoneSizeKey = value
            );

            configMenu.AddKeybind(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.increase_phone_size_key"),
                tooltip: () => Helper.Translation.Get("config.tooltip.increase_phone_size_key"),
                getValue: () => Config.IncreasePhoneSizeKey,
                setValue: value => Config.IncreasePhoneSizeKey = value
            );

            configMenu.AddTextOption(
                mod: ModManifest,
                name: () => Helper.Translation.Get("config.name.show_size_button"),
                tooltip: () => Helper.Translation.Get("config.tooltip.show_size_button"),
                getValue: () => Config.ShowSizeButton,
                setValue: value => Config.ShowSizeButton = value,
                allowedValues: new string[] { "Disable", "Hover", "Always" },
                formatAllowedValue: value => Helper.Translation.Get($"config.value.show_size_button.{value.ToLower()}")
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
        }

    }

}