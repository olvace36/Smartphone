using System.Collections.Generic;
using Newtonsoft.Json;

namespace Smartphone.Data
{
    /// <summary>Persisted data for photo albums and favourites.</summary>
    public class PhotoAlbumStore
    {
        [JsonProperty("favourites")]
        public List<string> FavouriteFileNames { get; set; } = new();

        [JsonProperty("albums")]
        public List<PhotoAlbumEntry> Albums { get; set; } = new();
    }

    /// <summary>A single user-created photo album.</summary>
    public class PhotoAlbumEntry
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("files")]
        public List<string> PhotoFileNames { get; set; } = new();
    }
}
