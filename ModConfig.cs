using StardewModdingAPI;
using StardewValley;
using System.Collections.Generic;

namespace Smartphone
{
    public class ModConfig
    {
        public SButton ModKey { get; set; } = SButton.H;
        public string HelperOption { get; set; } = "Always";

        // advance
        public string OpenAIKey { get; set; } = "";
        public int MaxSummaryWordCount { get; set; } = 350;
        public int MaxCharacteristicCharacterCount { get; set; } = 1300;
    }
}
