using System;
using System.Collections;
using System.Reflection;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;

namespace Smartphone
{
    public partial class ModEntry
    {
        private const int MaxCropTagsPerImage = 20;
        private const int MaxFruitTreeTagsPerImage = 10;
        private const int MaxAnimalTagsPerImage = 10;
        private const string PlayerTag = "has Player";
        private const int BuildingFrontTilesLeftRight = 2;
        private const int BuildingFrontTilesUp = 3;
        private const int BuildingFrontTilesDown = 1;
        private static string LastTriggeredUnlimitedEventTag = string.Empty;
        private static int LastTriggeredUnlimitedEventYear = -1;
        private static string LastTriggeredUnlimitedEventSeason = string.Empty;
        private static int LastTriggeredUnlimitedEventDay = -1;

        public static Dictionary<string, string> ImageTags = new(StringComparer.OrdinalIgnoreCase);

        private static string ImageTagDataPath => $"./userdata/{GetCurrentSaveFolderName()}/imageTags";

        private static string GetCurrentSaveFolderName()
        {
            return string.IsNullOrWhiteSpace(Constants.SaveFolderName)
                ? "default"
                : Constants.SaveFolderName;
        }

        private static string GetImageFolderPath()
        {
            return Path.Combine(SHelper.DirectoryPath, "userdata", GetCurrentSaveFolderName(), "image");
        }

        public static void LoadImageTags()
        {
            var loaded = SHelper.Data.ReadJsonFile<Dictionary<string, string>>(ImageTagDataPath)
                         ?? new Dictionary<string, string>();
            ImageTags = new Dictionary<string, string>(loaded, StringComparer.OrdinalIgnoreCase);

            CleanupMissingImageTagEntries();
        }

        public static void SaveImageTags()
        {
            SHelper.Data.WriteJsonFile(ImageTagDataPath, ImageTags);
        }

        public static void SetImageTags(string imageName, IEnumerable<string> tags)
        {
            if (string.IsNullOrWhiteSpace(imageName))
                return;

            var cleanedTags = (tags ?? Enumerable.Empty<string>())
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (cleanedTags.Count == 0)
            {
                RemoveImageTags(imageName);
                return;
            }

            Game1.chatBox.addErrorMessage($"{string.Join(", ", cleanedTags)}");
            ImageTags[imageName] = string.Join(";", cleanedTags);
            SaveImageTags();
        }

        public static void RemoveImageTags(string imageName)
        {
            if (string.IsNullOrWhiteSpace(imageName))
                return;

            if (ImageTags.Remove(imageName))
                SaveImageTags();
        }

        private static void CleanupMissingImageTagEntries()
        {
            if (ImageTags.Count == 0)
                return;

            var existingImageNames = Directory.Exists(GetImageFolderPath())
                ? Directory.GetFiles(GetImageFolderPath(), "*.png")
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            bool hasChanges = false;

            foreach (string imageName in ImageTags.Keys.ToList())
            {
                if (!existingImageNames.Contains(imageName))
                {
                    ImageTags.Remove(imageName);
                    hasChanges = true;
                }
            }

            if (hasChanges)
                SaveImageTags();
        }

        private static IEnumerable<string> BuildImageTags(Rectangle captureBounds)
        {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                BuildLocationTag(Game1.currentLocation)
            };

            AddBuildingFrontTags(tags, captureBounds);
            AddWeatherTags(tags);
            AddCurrentEventTags(tags);
            AddCharacterTags(tags, captureBounds);
            AddCropAndFruitTreeTags(tags, captureBounds);
            AddFarmAnimalTags(tags, captureBounds);
            AddHeldFishTag(tags);

            return tags;
        }

        private static void AddCharacterTags(HashSet<string> tags, Rectangle captureBounds)
        {
            if (Game1.player != null
                && IsPlayerInCurrentLocation()
                && IsCharacterInsideCapture(Game1.player, captureBounds))
            {
                tags.Add(PlayerTag);
                AddPlayerClothingTags(tags);
            }

            if (Game1.currentLocation?.characters != null)
            {
                foreach (NPC npc in Game1.currentLocation.characters.OfType<NPC>())
                {
                    if (string.IsNullOrWhiteSpace(npc.Name))
                        continue;

                    if (IsCharacterInsideCapture(npc, captureBounds))
                        tags.Add($"has {npc.Name}");
                }
            }
        }

        private static bool IsPlayerInCurrentLocation()
        {
            if (Game1.player?.currentLocation == null || Game1.currentLocation == null)
                return false;

            if (ReferenceEquals(Game1.player.currentLocation, Game1.currentLocation))
                return true;

            string playerLocationName = TryReadStringMember(Game1.player.currentLocation, "NameOrUniqueName", "Name");
            string currentLocationName = TryReadStringMember(Game1.currentLocation, "NameOrUniqueName", "Name");
            return !string.IsNullOrWhiteSpace(playerLocationName)
                && string.Equals(playerLocationName, currentLocationName, StringComparison.OrdinalIgnoreCase);
        }

        private static void AddPlayerClothingTags(HashSet<string> tags)
        {
            if (Game1.player == null)
                return;
                Game1.player.shirtItem?.Value?.Name.Trim();
                Game1.player.pantsItem?.Value?.Name.Trim();
                Game1.player.boots?.Value?.Name.Trim();
                Game1.player.hat?.Value?.Name.Trim(); 

                string outfitTag = $"player outfit: ";
                if (Game1.player.shirtItem?.Value != null && !string.IsNullOrWhiteSpace(Game1.player.shirtItem.Value.Name))
                    outfitTag += $"shirt {Game1.player.shirtItem.Value.Name.Trim()} ";
                if (Game1.player.pantsItem?.Value != null && !string.IsNullOrWhiteSpace(Game1.player.pantsItem.Value.Name))
                    outfitTag += $"pants {Game1.player.pantsItem.Value.Name.Trim()} ";
                if (Game1.player.boots?.Value != null && !string.IsNullOrWhiteSpace(Game1.player.boots.Value.Name))
                    outfitTag += $"boots {Game1.player.boots.Value.Name.Trim()} ";
                if (Game1.player.hat?.Value != null && !string.IsNullOrWhiteSpace(Game1.player.hat.Value.Name))
                    outfitTag += $"hat {Game1.player.hat.Value.Name.Trim()} ";

                if(outfitTag != "player outfit: ")
                    tags.Add(outfitTag);

        }

        private static void AddCropAndFruitTreeTags(HashSet<string> tags, Rectangle captureBounds)
        {
            GameLocation? currentLocation = Game1.currentLocation;
            if (currentLocation == null || currentLocation.terrainFeatures == null)
                return;

            var terrainFeatures = currentLocation.terrainFeatures;
            var cropGroups = new Dictionary<string, CropTagGroup>(StringComparer.OrdinalIgnoreCase);
            var fruitTreeGroups = new Dictionary<string, FruitTreeTagGroup>(StringComparer.OrdinalIgnoreCase);

            foreach (Vector2 tile in terrainFeatures.Keys)
            {
                if (!terrainFeatures.TryGetValue(tile, out TerrainFeature? feature) || feature == null)
                    continue;

                if (feature is HoeDirt dirt && dirt.crop != null)
                {
                    if (!IsTileAreaInsideCapture(tile, captureBounds, 0, 0, 64, 64))
                        continue;

                    string cropName = GetCropDisplayName(dirt.crop);
                    if (string.IsNullOrWhiteSpace(cropName))
                        continue;

                    if (!cropGroups.TryGetValue(cropName, out CropTagGroup? cropGroup))
                    {
                        cropGroup = new CropTagGroup();
                        cropGroups[cropName] = cropGroup;
                    }

                    cropGroup.Add(dirt.readyForHarvest());
                    continue;
                }

                if (feature is FruitTree fruitTree)
                {
                    if (!IsTileAreaInsideCapture(tile, captureBounds, -32, -128, 128, 192))
                        continue;

                    string fruitTreeName = NormalizeFruitTreeName(fruitTree.GetDisplayName());
                    if (string.IsNullOrWhiteSpace(fruitTreeName))
                        continue;

                    if (!fruitTreeGroups.TryGetValue(fruitTreeName, out FruitTreeTagGroup? fruitTreeGroup))
                    {
                        fruitTreeGroup = new FruitTreeTagGroup();
                        fruitTreeGroups[fruitTreeName] = fruitTreeGroup;
                    }

                    fruitTreeGroup.Add(TryReadIntMember(fruitTree, "fruitsOnTree", "fruit", "Fruit") > 0);
                }
            }

            int cropTagCount = 0;
            foreach (var cropGroup in cropGroups
                         .OrderByDescending(entry => entry.Value.Count)
                         .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (cropTagCount >= MaxCropTagsPerImage)
                    break;

                string cropTag = $"{cropGroup.Key} {cropGroup.Value.GetGrowthState()}";
                if (tags.Add(cropTag))
                    cropTagCount++;

                if (cropTagCount >= MaxCropTagsPerImage)
                    break;

                if (cropGroup.Value.Count >= 18 && tags.Add($"{cropGroup.Key} field"))
                    cropTagCount++;
            }

            int fruitTreeTagCount = 0;
            foreach (var fruitTreeGroup in fruitTreeGroups
                         .OrderByDescending(entry => entry.Value.Count)
                         .ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (fruitTreeTagCount >= MaxFruitTreeTagsPerImage)
                    break;

                string fruitTreeTag = fruitTreeGroup.Value.GetRegularTag(fruitTreeGroup.Key);
                if (tags.Add(fruitTreeTag))
                    fruitTreeTagCount++;

                if (fruitTreeTagCount >= MaxFruitTreeTagsPerImage)
                    break;

                if (fruitTreeGroup.Value.Count >= 6 && tags.Add($"{NormalizeFruitTreeOrchardName(fruitTreeGroup.Key)} orchard"))
                    fruitTreeTagCount++;
            }
        }

        private sealed class CropTagGroup
        {
            public int Count { get; private set; }
            public int ReadyCount { get; private set; }
            public int GrowingCount { get; private set; }

            public void Add(bool readyForHarvest)
            {
                Count++;

                if (readyForHarvest)
                    ReadyCount++;
                else
                    GrowingCount++;
            }

            public string GetGrowthState()
            {
                if (ReadyCount == 0)
                    return "still_growing";

                if (GrowingCount == 0)
                    return "ready_to_harvest";

                return ReadyCount >= GrowingCount
                    ? "ready_to_harvest"
                    : "still_growing";
            }
        }

        private sealed class FruitTreeTagGroup
        {
            public int Count { get; private set; }
            public int WithFruitCount { get; private set; }
            public int WithoutFruitCount { get; private set; }

            public void Add(bool hasFruit)
            {
                Count++;

                if (hasFruit)
                    WithFruitCount++;
                else
                    WithoutFruitCount++;
            }

            public string GetRegularTag(string fruitTreeName)
            {
                if (WithFruitCount == 0)
                    return fruitTreeName;

                if (WithoutFruitCount == 0)
                    return $"{fruitTreeName} with fruit";

                return WithFruitCount >= WithoutFruitCount
                    ? $"{fruitTreeName} with fruit"
                    : fruitTreeName;
            }
        }

        private static string NormalizeFruitTreeOrchardName(string? rawTreeName)
        {
            if (string.IsNullOrWhiteSpace(rawTreeName))
                return "fruit orchard";

            string normalized = rawTreeName.Trim().ToLowerInvariant();
            if (normalized.EndsWith(" tree", StringComparison.OrdinalIgnoreCase))
                normalized = normalized[..^5].TrimEnd();

            return string.IsNullOrWhiteSpace(normalized)
                ? "fruit orchard"
                : normalized;
        }

        private static void AddFarmAnimalTags(HashSet<string> tags, Rectangle captureBounds)
        {
            int animalTagCount = 0;

            foreach (FarmAnimal animal in GetFarmAnimalsForCurrentLocation())
            {
                if (animalTagCount >= MaxAnimalTagsPerImage)
                    break;

                if (!IsCharacterInsideCapture(animal, captureBounds))
                    continue;

                string animalType = GetFarmAnimalTypeName(animal);
                if (string.IsNullOrWhiteSpace(animalType))
                    continue;

                bool isBaby = TryInvokeBooleanMember(animal, "isBaby");
                string animalTag = isBaby
                    ? $"baby {animalType}"
                    : animalType;

                if (tags.Add(animalTag))
                    animalTagCount++;
            }
        }

        private static IEnumerable<FarmAnimal> GetFarmAnimalsForCurrentLocation()
        {
            var animals = new HashSet<FarmAnimal>();
            GameLocation? location = Game1.currentLocation;
            if (location == null)
                return animals;

            if (location.characters != null)
            {
                foreach (FarmAnimal animal in location.characters.OfType<FarmAnimal>())
                    animals.Add(animal);
            }

            AppendFarmAnimalsFromUnknownCollection(GetMemberValue(location, "animals", "Animals"), animals);

            GameLocation? farm = Game1.getFarm();
            if (farm != null)
                AppendFarmAnimalsFromUnknownCollection(GetMemberValue(farm, "animals", "Animals"), animals);

            return animals.Where(animal => IsAnimalInCurrentLocation(animal, location));
        }

        private static void AppendFarmAnimalsFromUnknownCollection(object? collection, HashSet<FarmAnimal> animals)
        {
            if (collection == null)
                return;

            if (collection is IEnumerable enumerable)
                AppendFarmAnimalsFromEnumerable(enumerable, animals);

            object? values = GetMemberValue(collection, "Values");
            if (values is IEnumerable valuesEnumerable)
                AppendFarmAnimalsFromEnumerable(valuesEnumerable, animals);
        }

        private static void AppendFarmAnimalsFromEnumerable(IEnumerable entries, HashSet<FarmAnimal> animals)
        {
            foreach (object? entry in entries)
            {
                FarmAnimal? animal = TryExtractFarmAnimal(entry);
                if (animal != null)
                    animals.Add(animal);
            }
        }

        private static bool IsAnimalInCurrentLocation(FarmAnimal animal, GameLocation currentLocation)
        {
            GameLocation? animalLocation = animal.currentLocation;
            if (animalLocation == null)
                return string.Equals(currentLocation.Name, "Farm", StringComparison.OrdinalIgnoreCase);

            if (ReferenceEquals(animalLocation, currentLocation))
                return true;

            string animalLocationName = TryReadStringMember(animalLocation, "NameOrUniqueName", "Name");
            string currentLocationName = TryReadStringMember(currentLocation, "NameOrUniqueName", "Name");
            return !string.IsNullOrWhiteSpace(animalLocationName)
                && string.Equals(animalLocationName, currentLocationName, StringComparison.OrdinalIgnoreCase);
        }

        private static FarmAnimal? TryExtractFarmAnimal(object? entry)
        {
            if (entry == null)
                return null;

            if (entry is FarmAnimal directAnimal)
                return directAnimal;

            object? value = entry;
            Type entryType = entry.GetType();
            if (entryType.IsGenericType && entryType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                value = entryType.GetProperty("Value")?.GetValue(entry);

            if (value is FarmAnimal valueAnimal)
                return valueAnimal;

            if (value != null)
                return GetMemberValue(value, "Value") as FarmAnimal;

            return null;
        }

        private static string GetFarmAnimalTypeName(FarmAnimal animal)
        {
            string rawAnimalType = TryReadStringMember(animal, "displayType", "DisplayType", "type", "Type");
            if (string.IsNullOrWhiteSpace(rawAnimalType))
                rawAnimalType = animal.Name;

            string normalized = rawAnimalType.Trim().ToLowerInvariant().Replace('_', ' ');

            foreach (string prefix in new[] { "white ", "brown ", "blue ", "golden ", "void " })
            {
                if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    normalized = normalized[prefix.Length..].Trim();
                    break;
                }
            }

            if (normalized.StartsWith("baby ", StringComparison.OrdinalIgnoreCase))
                normalized = normalized[5..].Trim();

            return string.IsNullOrWhiteSpace(normalized) ? "animal" : normalized;
        }

        private static string GetCropDisplayName(Crop crop)
        {
            try
            {
                string harvestIndex = crop.indexOfHarvest.Value;
                if (!string.IsNullOrWhiteSpace(harvestIndex))
                    return new StardewValley.Object(harvestIndex, 1).DisplayName.Trim().ToLowerInvariant();
            }
            catch
            {
            }

            return "crop";
        }

        private static string NormalizeFruitTreeName(string? rawTreeName)
        {
            if (string.IsNullOrWhiteSpace(rawTreeName))
                return "fruit tree";

            string normalized = rawTreeName.Trim().ToLowerInvariant();
            return normalized.Contains("tree", StringComparison.OrdinalIgnoreCase)
                ? normalized
                : $"{normalized} tree";
        }

        private static bool IsTileAreaInsideCapture(Vector2 tile, Rectangle captureBounds, int offsetX, int offsetY, int width, int height)
        {
            Vector2 worldTopLeft = new Vector2((tile.X * 64f) + offsetX, (tile.Y * 64f) + offsetY);
            Vector2 screenTopLeft = Game1.GlobalToLocal(Game1.viewport, worldTopLeft);

            var screenBounds = new Rectangle(
                (int)screenTopLeft.X,
                (int)screenTopLeft.Y,
                width,
                height);

            return new Rectangle(
                (int)(captureBounds.X / Game1.options.zoomLevel), 
                (int)(captureBounds.Y / Game1.options.zoomLevel), 
                (int)(captureBounds.Width / Game1.options.zoomLevel), 
                (int)(captureBounds.Height / Game1.options.zoomLevel)).Contains(screenBounds);
        }

        private static void AddHeldFishTag(HashSet<string> tags)
        {
            if (Game1.player == null || !tags.Contains(PlayerTag))
                return;

            Item? heldItem = Game1.player.ActiveObject ?? Game1.player.CurrentItem;
            if (heldItem is not StardewValley.Object fishObject || fishObject.Category != -4)
                return;

            string fishName = fishObject.DisplayName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(fishName))
                return;

            tags.Add($"holding {fishName}");
        }

        private static void AddBuildingFrontTags(HashSet<string> tags, Rectangle captureBounds)
        {
            GameLocation? currentLocation = Game1.currentLocation;
            if (currentLocation == null || currentLocation.buildings == null)
                return;

            foreach (var door in currentLocation.doors)
            {
                foreach (var doorItem in door)
                {
                    Point tile = doorItem.Key;
                    if (!IsTileAreaInsideCapture(tile.ToVector2(), captureBounds, -BuildingFrontTilesLeftRight * 64, -BuildingFrontTilesUp * 64, (BuildingFrontTilesLeftRight + BuildingFrontTilesLeftRight + 1) * 64, (BuildingFrontTilesUp + BuildingFrontTilesDown + 1) * 64))
                        continue;

                    string buildingName = doorItem.Value?.Trim();
                    if (string.IsNullOrWhiteSpace(buildingName))
                        continue;

                    tags.Add($"in front of {buildingName}");
                }
            }
        }

        private static void AddWeatherTags(HashSet<string> tags)
        {
            tags.Add($"weather {(Game1.currentLocation.GetWeather().Weather.ToString() == "Festival" ? "Sunny" : Game1.currentLocation.GetWeather().Weather.ToString())}");
        }

        private static void AddCurrentEventTags(HashSet<string> tags)
        {
            if (Game1.CurrentEvent == null)
                return;

            if (Game1.CurrentEvent.isFestival && !string.IsNullOrWhiteSpace(Game1.CurrentEvent.FestivalName))
                tags.Add(Game1.CurrentEvent.FestivalName.Trim());

            if (TryGetRecentUnlimitedEventTag(out string unlimitedEventTag))
                tags.Add(unlimitedEventTag);

            foreach (NPC birthdayNpc in GetNpcsWithBirthdayToday())
            {
                if (!string.IsNullOrWhiteSpace(birthdayNpc.Name))
                    tags.Add($"{birthdayNpc.Name} birthday");
            }

            if (!Game1.CurrentEvent.isFestival && !TryGetRecentUnlimitedEventTag(out _))
            {
                List<string> actorNames = GetCurrentEventActorNames(Game1.CurrentEvent);
                if (actorNames.Count == 1)
                    tags.Add($"event with {actorNames[0]}");
                else if (actorNames.Count > 1)
                    tags.Add($"event with {string.Join(", ", actorNames.Take(2))}");
            }
        }

        private static List<string> GetCurrentEventActorNames(Event currentEvent)
        {
            var actorNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            object? actorsObject = GetMemberValue(currentEvent, "actors", "Actors");
            if (actorsObject is not IEnumerable actorsEnumerable)
                return new List<string>();

            foreach (object? actorEntry in actorsEnumerable)
            {
                if (actorEntry == null)
                    continue;

                object? value = actorEntry;
                Type entryType = actorEntry.GetType();
                if (entryType.IsGenericType && entryType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                    value = entryType.GetProperty("Value")?.GetValue(actorEntry);

                if (value is NPC npc && !string.IsNullOrWhiteSpace(npc.Name))
                    actorNames.Add(npc.Name);
            }

            actorNames.Remove(Game1.player?.Name ?? string.Empty);
            return actorNames.ToList();
        }

        private static void RememberTriggeredUnlimitedEvent(string eventType, string npcName)
        {
            string normalizedEventType = (eventType ?? string.Empty).Replace('_', ' ').Trim();
            string normalizedNpcName = (npcName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedEventType) || string.IsNullOrWhiteSpace(normalizedNpcName))
                return;

            LastTriggeredUnlimitedEventTag = $"{normalizedEventType} with {normalizedNpcName}";
            LastTriggeredUnlimitedEventYear = Game1.year;
            LastTriggeredUnlimitedEventSeason = Game1.currentSeason ?? string.Empty;
            LastTriggeredUnlimitedEventDay = Game1.dayOfMonth;
        }

        private static void ResetTriggeredUnlimitedEventTag()
        {
            LastTriggeredUnlimitedEventTag = string.Empty;
            LastTriggeredUnlimitedEventYear = -1;
            LastTriggeredUnlimitedEventSeason = string.Empty;
            LastTriggeredUnlimitedEventDay = -1;
        }

        private static bool TryGetRecentUnlimitedEventTag(out string tag)
        {
            tag = string.Empty;
            if (string.IsNullOrWhiteSpace(LastTriggeredUnlimitedEventTag))
                return false;

            bool isSameDay = LastTriggeredUnlimitedEventYear == Game1.year
                && string.Equals(LastTriggeredUnlimitedEventSeason, Game1.currentSeason, StringComparison.OrdinalIgnoreCase)
                && LastTriggeredUnlimitedEventDay == Game1.dayOfMonth;

            if (!isSameDay)
                return false;

            tag = LastTriggeredUnlimitedEventTag;
            return true;
        }

        private static bool TryInvokeBooleanMember(object source, string memberName)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;

            MethodInfo? method = source.GetType().GetMethod(memberName, flags, Type.DefaultBinder, Type.EmptyTypes, null);
            if (method != null && method.ReturnType == typeof(bool) && method.Invoke(source, null) is bool methodResult)
                return methodResult;

            object? value = GetMemberValue(source, memberName);
            return value is bool boolValue && boolValue;
        }

        private static int TryReadIntMember(object source, params string[] memberNames)
        {
            foreach (string memberName in memberNames)
            {
                object? value = GetMemberValue(source, memberName);
                int extracted = ExtractIntFromUnknownValue(value);
                if (extracted >= 0)
                    return extracted;
            }

            return -1;
        }

        private static string TryReadStringMember(object source, params string[] memberNames)
        {
            foreach (string memberName in memberNames)
            {
                object? value = GetMemberValue(source, memberName);
                string extracted = ExtractStringFromUnknownValue(value);
                if (!string.IsNullOrWhiteSpace(extracted))
                    return extracted;
            }

            return string.Empty;
        }

        private static object? GetMemberValue(object source, params string[] memberNames)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;

            foreach (string memberName in memberNames)
            {
                PropertyInfo? property = source.GetType().GetProperty(memberName, flags);
                if (property != null)
                    return property.GetValue(source);

                FieldInfo? field = source.GetType().GetField(memberName, flags);
                if (field != null)
                    return field.GetValue(source);
            }

            return null;
        }

        private static int ExtractIntFromUnknownValue(object? value)
        {
            if (value == null)
                return -1;

            if (value is int intValue)
                return intValue;

            if (value is long longValue && longValue >= 0 && longValue <= int.MaxValue)
                return (int)longValue;

            if (value is short shortValue && shortValue >= 0)
                return shortValue;

            if (value is byte byteValue)
                return byteValue;

            if (value is string textValue && int.TryParse(textValue, out int parsedTextValue))
                return parsedTextValue;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;

            PropertyInfo? valueProperty = value.GetType().GetProperty("Value", flags);
            if (valueProperty != null)
            {
                object? nestedValue = valueProperty.GetValue(value);
                int nestedInt = ExtractIntFromUnknownValue(nestedValue);
                if (nestedInt >= 0)
                    return nestedInt;
            }

            PropertyInfo? countProperty = value.GetType().GetProperty("Count", flags);
            if (countProperty != null)
            {
                object? countValue = countProperty.GetValue(value);
                int parsedCount = ExtractIntFromUnknownValue(countValue);
                if (parsedCount >= 0)
                    return parsedCount;
            }

            return -1;
        }

        private static string ExtractStringFromUnknownValue(object? value)
        {
            if (value == null)
                return string.Empty;

            if (value is string stringValue)
                return stringValue;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;
            PropertyInfo? valueProperty = value.GetType().GetProperty("Value", flags);
            if (valueProperty != null)
            {
                object? nestedValue = valueProperty.GetValue(value);
                if (nestedValue is string nestedString)
                    return nestedString;
            }

            return value.ToString() ?? string.Empty;
        }

        private static bool IsCharacterInsideCapture(Character character, Rectangle captureBounds)
        {
            Rectangle characterBounds = character.GetBoundingBox();
            Vector2 screenTopLeft = Game1.GlobalToLocal(Game1.viewport, new Vector2(characterBounds.X, characterBounds.Y - 64));
            var screenBounds = new Rectangle(
                (int)screenTopLeft.X,
                (int)screenTopLeft.Y,
                characterBounds.Width,
                characterBounds.Height);

            return new Rectangle(
                (int)(captureBounds.X / Game1.options.zoomLevel), 
                (int)(captureBounds.Y / Game1.options.zoomLevel), 
                (int)(captureBounds.Width / Game1.options.zoomLevel), 
                (int)(captureBounds.Height / Game1.options.zoomLevel)).Contains(screenBounds);
        }

        private static string BuildLocationTag(GameLocation? location)
        {
            string locationName = location?.Name;
            if (string.IsNullOrWhiteSpace(locationName))
                locationName = "unknown location";

            return $"at {locationName}";
        }
    }
}
