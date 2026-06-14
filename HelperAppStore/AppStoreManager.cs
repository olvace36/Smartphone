using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;

namespace Smartphone
{
    public class AppStoreMod
    {
        public string UniqueID { get; set; } = "";
        public string Author { get; set; } = "";
        public string Name { get; set; } = "";
        public string ShortDescription { get; set; } = "";
        public string FullDescription { get; set; } = "";
        public string UpdateKey { get; set; } = "";
        public string ModURL { get; set; } = "";
        public string IconURL { get; set; } = "";
        public string Timestamp { get; set; } = "";
        public string PublishedAt { get; set; } = "";
        public string TotalEndorsement { get; set; } = "";
        public string LatestVersion { get; set; } = "";
        public string ModType { get; set; } = "";


        public byte[] IconBytes { get; set; }
        public Texture2D IconTexture { get; set; }
        public bool IsFetchingIcon { get; set; }
    }

    internal static class AppStoreManager
    {
        private const string SheetUrl = "https://docs.google.com/spreadsheets/d/e/2PACX-1vSrSa8WPN27y_HycjbTTB9wiXsZgC-Wu8pMXd3yeFWItBorqio8-e46e4IDf8Vnq2frlQYJb1hYvGk5/pub?gid=0&single=true&output=csv";
        private static readonly HttpClient HttpClient = new HttpClient();
        private static readonly SemaphoreSlim TextureCreationSemaphore = new SemaphoreSlim(1, 1);


        static AppStoreManager()
        {
            // Set a standard browser User-Agent to prevent CDNs like NexusMods/Cloudflare from blocking the request with 403 Forbidden
            HttpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36");
        }

        public static List<AppStoreMod> AllMods { get; private set; } = new List<AppStoreMod>();
        public static List<AppStoreMod> Mods { get; private set; } = new List<AppStoreMod>();
        public static Dictionary<string, Texture2D> DescriptionImages { get; private set; } = new Dictionary<string, Texture2D>();
        private static HashSet<string> FetchingDescriptionImages { get; set; } = new HashSet<string>();
        private static Dictionary<string, byte[]> fetchedDescriptionBytes = new Dictionary<string, byte[]>();

        public static void Initialize()
        {
            Task.Run(async () =>
            {
                try
                {
                    ModEntry.SMonitor?.Log("AppStoreManager: Fetching mod list...", LogLevel.Trace);
                    string fetchUrl = $"{SheetUrl}&t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                    string csvData = await HttpClient.GetStringAsync(fetchUrl);
                    ParseCsv(csvData);
                    ModEntry.SMonitor?.Log($"AppStoreManager: Fetched {Mods.Count} mods.", LogLevel.Debug);
                }
                catch (Exception ex)
                {
                    ModEntry.SMonitor?.Log($"AppStoreManager: Failed to fetch mod list: {ex}", LogLevel.Error);
                }
            });
        }

        private static void ParseCsv(string csv)
        {
            var parsedMods = new List<AppStoreMod>();
            var currentLine = new List<string>();
            string currentPart = "";
            bool inQuotes = false;
            var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            bool isHeaderParsed = false;

            for (int i = 0; i < csv.Length; i++)
            {
                char c = csv[i];

                if (c == '\"')
                {
                    if (inQuotes && i + 1 < csv.Length && csv[i + 1] == '\"')
                    {
                        currentPart += '\"';
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    currentLine.Add(currentPart);
                    currentPart = "";
                }
                else if ((c == '\r' || c == '\n') && !inQuotes)
                {
                    currentLine.Add(currentPart);
                    currentPart = "";

                    if (currentLine.Count > 0 && currentLine.Any(s => !string.IsNullOrWhiteSpace(s)))
                    {
                        if (!isHeaderParsed)
                        {
                            for (int col = 0; col < currentLine.Count; col++)
                            {
                                string header = currentLine[col].Trim().Replace(" ", "").ToLowerInvariant();
                                if (!string.IsNullOrEmpty(header))
                                    headerMap[header] = col;
                            }
                            isHeaderParsed = true;
                        }
                        else
                        {
                            string GetValue(string key)
                            {
                                if (headerMap.TryGetValue(key, out int index) && index < currentLine.Count)
                                {
                                    return currentLine[index].Trim();
                                }
                                return "";
                            }

                            parsedMods.Add(new AppStoreMod
                            {
                                UniqueID = GetValue("uniqueid"),
                                Author = GetValue("author"),
                                Name = GetValue("name"),
                                ShortDescription = GetValue("shortdescription"),
                                FullDescription = GetValue("fulldescription"),
                                UpdateKey = GetValue("updatekey"),
                                ModURL = GetValue("modurl"),
                                IconURL = GetValue("iconurl"),
                                Timestamp = GetValue("timestamp"),
                                PublishedAt = GetValue("publishedat"),
                                TotalEndorsement = GetValue("totalendorsement"),
                                LatestVersion = GetValue("latestversion"),
                                ModType = headerMap.ContainsKey("modtype") ? GetValue("modtype") : "App"
                            });
                        }
                    }
                    currentLine.Clear();

                    if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
                        i++;
                }
                else
                {
                    currentPart += c;
                }
            }

            if (currentPart != "" || currentLine.Count > 0)
            {
                currentLine.Add(currentPart);
                if (currentLine.Count > 0 && currentLine.Any(s => !string.IsNullOrWhiteSpace(s)))
                {
                    if (!isHeaderParsed)
                    {
                        // Highly unlikely to have just one line without a newline that is the header
                    }
                    else
                    {
                        string GetValue(string key)
                        {
                            if (headerMap.TryGetValue(key, out int index) && index < currentLine.Count)
                            {
                                return currentLine[index].Trim();
                            }
                            return "";
                        }

                        parsedMods.Add(new AppStoreMod
                        {
                            UniqueID = GetValue("uniqueid"),
                            Author = GetValue("author"),
                            Name = GetValue("name"),
                            ShortDescription = GetValue("shortdescription"),
                            FullDescription = GetValue("fulldescription"),
                            UpdateKey = GetValue("updatekey"),
                            ModURL = GetValue("modurl"),
                            IconURL = GetValue("iconurl"),
                            Timestamp = GetValue("timestamp"),
                            PublishedAt = GetValue("publishedat"),
                            TotalEndorsement = GetValue("totalendorsement"),
                            LatestVersion = GetValue("latestversion"),
                            ModType = headerMap.ContainsKey("modtype") ? GetValue("modtype") : "App"
                        });
                    }
                }
            }

            AllMods = parsedMods;
            ApplySortAndTypeFilter("Latest", "App");
        }

        public static void ApplySortAndTypeFilter(string sortOption, string typeOption)
        {
            if (AllMods == null) return;

            var filteredMods = AllMods.Where(m => m.ModType.Equals(typeOption, StringComparison.OrdinalIgnoreCase)).ToList();

            switch (sortOption)
            {
                case "Endorsement":
                    Mods = filteredMods.OrderByDescending(m => int.TryParse(m.TotalEndorsement, out int e) ? e : 0).ToList();
                    break;
                case "Alphabet":
                    Mods = filteredMods.OrderBy(m => m.Name).ToList();
                    break;
                case "Official":
                    Mods = filteredMods.Where(m => m.Author == "d5a1lamdtd")
                                   .OrderByDescending(m => int.TryParse(m.TotalEndorsement, out int e) ? e : 0)
                                   .ToList();
                    break;
                case "Installed":
                    Mods = filteredMods.Where(m => ModEntry.SHelper.ModRegistry.Get(m.UniqueID) != null)
                                   .OrderBy(m => m.Name)
                                   .ToList();
                    break;
                case "Latest":
                default:
                    Mods = filteredMods.OrderByDescending(m => DateTime.TryParse(m.Timestamp, out var dt) ? dt : DateTime.MinValue).ToList();
                    break;
            }
        }

        public static void FetchIconsForPage(int page, int itemsPerPage)
        {
            int startIndex = page * itemsPerPage;
            int endIndex = Math.Min(startIndex + itemsPerPage, Mods.Count);

            for (int i = startIndex; i < endIndex; i++)
            {
                var mod = Mods[i];
                if (mod.IconBytes != null || mod.IsFetchingIcon || string.IsNullOrWhiteSpace(mod.IconURL))
                    continue;

                mod.IsFetchingIcon = true;
                Task.Run(async () =>
                {
                    try
                    {
                        string fetchUrl = mod.IconURL;
                        if (!string.IsNullOrWhiteSpace(fetchUrl) && fetchUrl.StartsWith("http"))
                        {
                            // Wrap the URL in a public image conversion proxy to ensure it is returned as a JPG
                            // since MonoGame's Texture2D.FromStream cannot decode WebP images natively,
                            // which are commonly served by NexusMods and Imgur.
                            fetchUrl = $"https://wsrv.nl/?url={Uri.EscapeDataString(fetchUrl)}&w=400&q=80&output=jpg";
                        }

                        var bytes = await HttpClient.GetByteArrayAsync(fetchUrl);
                        mod.IconBytes = bytes;


                        ModEntry.SMonitor?.Log($"AppStoreManager: Fetched icon bytes for {mod.Name}", LogLevel.Trace);
                        LogCacheSize();
                    }
                    catch (Exception ex)
                    {
                        ModEntry.SMonitor?.Log($"AppStoreManager: Failed to fetch icon for {mod.Name} from {mod.IconURL}: {ex.Message}", LogLevel.Warn);
                    }
                });
            }
        }



        public static void LogCacheSize()
        {
            long totalBytes = 0;
            if (AllMods != null)
            {
                foreach (var m in AllMods)
                {
                    if (m.IconBytes != null)
                        totalBytes += m.IconBytes.Length;
                }
            }

            lock (FetchingDescriptionImages)
            {
                foreach (var bytes in fetchedDescriptionBytes.Values)
                {
                    if (bytes != null)
                        totalBytes += bytes.Length;
                }
            }

            double mb = totalBytes / 1024.0 / 1024.0;
            ModEntry.SMonitor?.Log($"AppStoreManager: Current cache size: {mb:0.00} MB", LogLevel.Debug);
        }

        public static Texture2D GetOrFetchDescriptionImage(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            if (DescriptionImages.TryGetValue(url, out Texture2D texture))
            {
                return texture;
            }

            byte[] bytesToDecode = null;
            lock (FetchingDescriptionImages)
            {
                if (fetchedDescriptionBytes.TryGetValue(url, out byte[] bytes))
                {
                    bytesToDecode = bytes;
                    fetchedDescriptionBytes.Remove(url);
                }
            }

            if (bytesToDecode != null)
            {
                try
                {
                    using (var stream = new MemoryStream(bytesToDecode))
                    {
                        var newTexture = Texture2D.FromStream(StardewValley.Game1.graphics.GraphicsDevice, stream);
                        DescriptionImages[url] = newTexture;
                        return newTexture;
                    }
                }
                catch (Exception ex)
                {
                    ModEntry.SMonitor?.Log($"AppStoreManager: Failed to decode BBCode image from {url}: {ex.Message}", LogLevel.Warn);
                }
            }

            bool shouldFetch = false;
            lock (FetchingDescriptionImages)
            {
                if (!FetchingDescriptionImages.Contains(url))
                {
                    FetchingDescriptionImages.Add(url);
                    shouldFetch = true;
                }
            }

            if (shouldFetch)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        string fetchUrl = url;
                        if (fetchUrl.StartsWith("http"))
                        {
                            fetchUrl = $"https://wsrv.nl/?url={Uri.EscapeDataString(fetchUrl)}&w=600&q=80&output=jpg";
                        }

                        var bytes = await HttpClient.GetByteArrayAsync(fetchUrl);

                        lock (FetchingDescriptionImages)
                        {
                            fetchedDescriptionBytes[url] = bytes;
                        }

                        ModEntry.SMonitor?.Log($"AppStoreManager: Fetched BBCode image from {url}", LogLevel.Trace);
                    }
                    catch (Exception ex)
                    {
                        ModEntry.SMonitor?.Log($"AppStoreManager: Failed to fetch BBCode image from {url}: {ex.Message}", LogLevel.Warn);
                    }
                });
            }

            return null;
        }

        public static void DisposeTextures()
        {
            // Disposal logic removed to preserve cache across phone menu opens/closes.
            // This ensures instant loading when returning to the App Store.
            ModEntry.SMonitor?.Log("AppStoreManager: DisposeTextures called, but disposal is disabled to preserve cache.", LogLevel.Debug);
        }
    }
}
