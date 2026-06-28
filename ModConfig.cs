using StardewModdingAPI;
using StardewValley;
using System.Collections.Generic;

namespace Smartphone
{
    public class ModConfig
    {
        public SButton ModKey { get; set; } = SButton.H;

        public int PlayerMaxPhoto { get; set; } = 100;
        public float PlayerCaptureWorldFlashRadius { get; set; } = 3f;

        public SButton DecreasePhoneSizeKey { get; set; } = SButton.OemComma;
        public SButton IncreasePhoneSizeKey { get; set; } = SButton.OemPeriod;
        public float PhoneSize { get; set; } = 1f;
        public string ShowSizeButton { get; set; } = "Hover";
        public int HudPhoneIconOffsetX { get; set; } = 0;
        public int HudPhoneIconOffsetY { get; set; } = 0;

        public bool NotifyNotification { get; set; } = true;

        public bool DisableUpdateWarning { get; set; } = false;
        public bool ShowPhoneIcon { get; set; } = true;

        public string BlacklistNpc { get; set; } = "Leo, Krobus, Dwarf, Gunther, Birdie, Bouncer, MoonSBV, PanSBV, RaccoonSBV, Leximonster, Dianna, Torts";
        public string FriendshipRequirement { get; set; } = "Meet";
    }
}
