using StardewModdingAPI;
using StardewValley;
using System.Collections.Generic;

namespace Smartphone
{
    public class ModConfig
    {
        public const string NpcRequirementMeet = "Meet";
        public const string NpcRequirementFriend = "Friend";

        public const string PostPerDayHigh = "High";
        public const string PostPerDayMedium = "Medium";
        public const string PostPerDayLow = "Low";

        public const string OpenAIModel_51 = "gpt-5.1";
        public const string OpenAIModel_5mini = "gpt-5-mini";
        public const string OpenAIModel_5nano = "gpt-5-nano";
        public const string OpenAIModel_54mini = "gpt-5.4-mini";
        public const string OpenAIModel_54nano = "gpt-5.4-nano";

        public const string CharacteristicModeMinimal = "minimal";
        public const string CharacteristicModeShort = "short";
        public const string CharacteristicModeLong = "long";


        public SButton ModKey { get; set; } = SButton.H;
        public string NpcMessageRequirement { get; set; } = NpcRequirementFriend;
        public string PostPerDay { get; set; } = PostPerDayLow;

        // advance
        public string OpenAIKey { get; set; } = "";
        public string OpenAIModel { get; set; } = OpenAIModel_54mini;
        public string CharacteristicMode { get; set; } = CharacteristicModeShort;


        public int MaxSummaryWordCount { get; set; } = 350;














        public int MaxMessage { get; set; } = 1000;
        public int MaxStardewConnectPosts { get; set; } = 100;
        public int PlayerMaxPhoto { get; set; } = 100;
        public int NpcMaxPhoto { get; set; } = 200;
        public bool ShowSocialImageTags { get; set; } = true;
        public bool ShowMessageImageTags { get; set; } = true;
        public bool ShowAiCredit { get; set; } = true;
        public bool notifyNotification { get; set; } = true;
        public bool notifyMessage { get; set; } = true;
        public bool notifyStardewSocial { get; set; } = true;

        public bool DisableDailyMessage { get; set; } = false;
    }
}
