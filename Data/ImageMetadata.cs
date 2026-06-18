using System;
using Newtonsoft.Json;

namespace Smartphone.Data
{
    public class ImageMetadata
    {
        [JsonProperty("l")]
        public string Location { get; set; } = string.Empty;

        [JsonProperty("ts")]
        public string TimeString { get; set; } = string.Empty;

        [JsonProperty("tag")]
        public string Tag { get; set; } = string.Empty;
    }
}
