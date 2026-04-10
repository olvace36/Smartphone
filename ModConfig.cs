using StardewModdingAPI;
using StardewValley;
using System.Collections.Generic;

namespace Smartphone
{
    public class ModConfig
    {
        public const string NpcRequirementNoRequirement = "No requirement";
        public const string NpcRequirementMeet = "Meet";
        public const string NpcRequirementFriend = "Friend";

        public SButton ModKey { get; set; } = SButton.H;
        public string HelperOption { get; set; } = "Always";
        public string NpcMessageRequirement { get; set; } = NpcRequirementFriend;

        // advance
        public string OpenAIKey { get; set; } = "";
        public int MaxSummaryWordCount { get; set; } = 350;
        public int MaxCharacteristicCharacterCount { get; set; } = 1300;
        public int MaxMessage { get; set; } = 1000;
        public int MaxStardewConnectPosts { get; set; } = 100;
        public int PlayerMaxPhoto { get; set; } = 100;
        public int NpcMaxPhoto { get; set; } = 200;
        public bool ShowSocialImageTags { get; set; } = true;
        public bool ShowMessageImageTags { get; set; } = true;
        public bool notifyNotification { get; set; } = true;
        public bool notifyMessage { get; set; } = true;
        public bool notifyStardewSocial { get; set; } = true;

        public bool DisableDailyMessage { get; set; } = false;
    }
}
