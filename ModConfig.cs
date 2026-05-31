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
        public const string GeminiModel_35Flash = "gemini-3.5-flash";
        public const string GeminiModel_31FlashLite = "gemini-3.1-flash-lite";
        public const string GeminiModel_3FlashPreview = "gemini-3-flash-preview";

        public static readonly List<string> geminiModels = new()
        {
            GeminiModel_35Flash,
            GeminiModel_31FlashLite,
            GeminiModel_3FlashPreview
        };

        public static readonly List<string> openAIModels = new()
        {
            OpenAIModel_51,
            OpenAIModel_5mini,
            OpenAIModel_5nano,
            OpenAIModel_54mini,
            OpenAIModel_54nano
        };

        public const string CharacteristicModeMinimal = "minimal";
        public const string CharacteristicModeShort = "short";
        public const string CharacteristicModeLong = "long";


        public SButton ModKey { get; set; } = SButton.H;
        public string NpcMessageRequirement { get; set; } = NpcRequirementMeet;
        public string PostPerDay { get; set; } = PostPerDayLow;
        public string Language { get; set; } = "English";

        private string _key = string.Empty;
        private string _model = OpenAIModel_54mini;

        // advance
        public string Key
        {
            get => _key;
            set => _key = (value ?? string.Empty).Trim();
        }

        public string Model
        {
            get => string.IsNullOrWhiteSpace(_model) ? OpenAIModel_54mini : _model;
            set => _model = string.IsNullOrWhiteSpace(value) ? OpenAIModel_54mini : value.Trim();
        }

        // Legacy aliases for older config.json files.
        public string OpenAIKey
        {
            get => Key;
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                    Key = value;
                else if (string.IsNullOrWhiteSpace(_key))
                    _key = string.Empty;
            }
        }

        public string OpenAIModel
        {
            get => Model;
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                    Model = value;
                else if (string.IsNullOrWhiteSpace(_model))
                    _model = OpenAIModel_54mini;
            }
        }
        public string CharacteristicMode { get; set; } = CharacteristicModeShort;
        public bool BetterQualityComment { get; set; } = false;


        public int MaxSummaryWordCount { get; set; } = 350;














        public int MaxMessage { get; set; } = 1000;
        public int MaxStardewConnectPosts { get; set; } = 100;
        public int PlayerMaxPhoto { get; set; } = 100;
        public int NpcMaxPhoto { get; set; } = 250;
        public float PlayerCaptureWorldFlashRadius { get; set; } = 3f;
        public bool ShowSocialImageTags { get; set; } = false;
        public bool ShowUnreadComment { get; set; } = true;
        public bool ShowMessageImageTags { get; set; } = false;
        public bool UseSmallPhoneSize { get; set; } = false;
        public int HudPhoneIconOffsetX { get; set; } = 0;
        public int HudPhoneIconOffsetY { get; set; } = 0;
        public bool ShowAiCredit { get; set; } = true;
        public bool notifyNotification { get; set; } = true;
        public bool notifyMessage { get; set; } = true;
        public bool notifyStardewSocial { get; set; } = true;
        public bool DisableUpdateWarning { get; set; } = false;

        public bool DisableDailyMessage { get; set; } = false;
    }
}
