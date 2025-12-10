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
        }

    }

}