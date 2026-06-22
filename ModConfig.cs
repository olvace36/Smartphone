using StardewModdingAPI;
using StardewValley;
using System.Collections.Generic;

namespace Smartphone
{
    public class ModConfig
    {
        public SButton ModKey { get; set; } = SButton.H;
        public string Language { get; set; } = "English";

        public int PlayerMaxPhoto { get; set; } = 100;
        public int NpcMaxPhoto { get; set; } = 250;
        public float PlayerCaptureWorldFlashRadius { get; set; } = 3f;

        public bool UseSmallPhoneSize { get; set; } = false;
        public int HudPhoneIconOffsetX { get; set; } = 0;
        public int HudPhoneIconOffsetY { get; set; } = 0;

        public bool notifyNotification { get; set; } = true;

        public bool DisableUpdateWarning { get; set; } = false;
        public bool ShowPhoneIcon { get; set; } = true;
    }
}
