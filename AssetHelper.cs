using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class AssetHelper
{
    public const string DefaultPhoneThemeName = "default";

    private static Dictionary<string, string> componentThemes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "phone", DefaultPhoneThemeName },
        { "app_appstore", DefaultPhoneThemeName },
        { "app_calendar", DefaultPhoneThemeName },
        { "app_camera", DefaultPhoneThemeName },
        { "app_notification", DefaultPhoneThemeName },
        { "app_photo", DefaultPhoneThemeName },
        { "app_setting", DefaultPhoneThemeName }
    };

    public static string CurrentPhoneThemeName => GetComponentTheme("phone");

    public static string GetPhoneThemesRootPath()
    {
        return "phone_themes";
    }

    public static string GetPhoneThemeFolderPath()
    {
        return GetComponentThemeFolderPath("phone");
    }

    public static string GetComponentThemeFolderPath(string component)
    {
        return Path.Combine(GetPhoneThemesRootPath(), component, GetComponentTheme(component));
    }

    public static string GetComponentTheme(string component)
    {
        LoadComponentThemes();
        if (componentThemes.TryGetValue(component, out string? theme))
            return theme;
        return DefaultPhoneThemeName;
    }

    public static void SetComponentTheme(string component, string theme)
    {
        componentThemes[component] = ResolveComponentThemeName(component, theme);
        SaveComponentThemes();
    }

    public static List<string> GetAvailableThemeNamesForComponent(string component)
    {
        List<string> themeNames = new();
        string componentRootPath = Path.Combine(GetAbsolutePhoneThemesRootPath(), component);

        if (Directory.Exists(componentRootPath))
        {
            foreach (string directoryPath in Directory.GetDirectories(componentRootPath))
            {
                string? folderName = Path.GetFileName(directoryPath);
                if (!string.IsNullOrWhiteSpace(folderName))
                    themeNames.Add(folderName);
            }
        }

        themeNames = themeNames
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        themeNames.RemoveAll(name => string.Equals(name, DefaultPhoneThemeName, StringComparison.OrdinalIgnoreCase));
        themeNames.Insert(0, DefaultPhoneThemeName);

        return themeNames;
    }

    public static List<string> GetAvailablePhoneThemeNames()
    {
        return GetAvailableThemeNamesForComponent("phone");
    }

    public static string ResolveComponentThemeName(string component, string? requestedThemeName)
    {
        List<string> availableThemes = GetAvailableThemeNamesForComponent(component);

        if (!string.IsNullOrWhiteSpace(requestedThemeName))
        {
            string? matchedTheme = availableThemes.FirstOrDefault(name =>
                string.Equals(name, requestedThemeName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(matchedTheme))
                return matchedTheme;
        }

        return DefaultPhoneThemeName;
    }

    public static string ResolvePhoneThemeName(string? requestedThemeName)
    {
        return ResolveComponentThemeName("phone", requestedThemeName);
    }

    public static bool SetCurrentPhoneTheme(string? requestedThemeName)
    {
        LoadComponentThemes();
        if (!string.IsNullOrWhiteSpace(requestedThemeName) && !string.Equals(requestedThemeName, DefaultPhoneThemeName, StringComparison.OrdinalIgnoreCase))
        {
            if (componentThemes["phone"] == DefaultPhoneThemeName)
            {
                componentThemes["phone"] = ResolveComponentThemeName("phone", requestedThemeName);
                SaveComponentThemes();
            }
        }
        return true;
    }

    private static string GetAbsolutePhoneThemesRootPath()
    {
        string? modFolderPath = Smartphone.ModEntry.Instance?.Helper?.DirectoryPath ?? Smartphone.ModEntry.SHelper?.DirectoryPath;
        if (string.IsNullOrWhiteSpace(modFolderPath))
            return GetPhoneThemesRootPath();

        return Path.Combine(modFolderPath, GetPhoneThemesRootPath());
    }

    private static bool themesLoaded = false;
    private static void LoadComponentThemes()
    {
        if (themesLoaded) return;
        try
        {
            string? modFolderPath = Smartphone.ModEntry.Instance?.Helper?.DirectoryPath ?? Smartphone.ModEntry.SHelper?.DirectoryPath;
            if (string.IsNullOrWhiteSpace(modFolderPath)) return;

            string path = Path.Combine(modFolderPath, "userdata", "component_themes.json");
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var data = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                if (data != null)
                {
                    foreach (var kv in data)
                    {
                        if (componentThemes.ContainsKey(kv.Key))
                            componentThemes[kv.Key] = kv.Value;
                    }
                }
            }
            themesLoaded = true;
        }
        catch { }
    }

    private static void SaveComponentThemes()
    {
        try
        {
            string? modFolderPath = Smartphone.ModEntry.Instance?.Helper?.DirectoryPath ?? Smartphone.ModEntry.SHelper?.DirectoryPath;
            if (string.IsNullOrWhiteSpace(modFolderPath)) return;

            string dir = Path.Combine(modFolderPath, "userdata");
            Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, "component_themes.json");
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(componentThemes, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(path, json);
        }
        catch { }
    }

    public class ImagesConstants
    {
        public const string PhoneBackground = "default_background.png";
        public const string PhoneEmpty = "default.png";
        public const string Background = "background.png";

        public const string AppCamera = "1x1.png";
        public const string AppPhoto = "1x1.png";
        public const string AppSetting = "1x1.png";
        public const string AppNotification = "1x1.png";
        public const string AppAppStore = "1x1.png";
        public const string AppCalendar = "1x1.png";
    }
}