using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class AssetHelper
{
    public const string DefaultPhoneThemeName = "default";

    private static string currentPhoneThemeName = DefaultPhoneThemeName;

    public static string CurrentPhoneThemeName => currentPhoneThemeName;

    public static string GetPhoneThemesRootPath()
    {
        return "phone_themes";
    }

    public static string GetPhoneThemeFolderPath()
    {
        return Path.Combine(GetPhoneThemesRootPath(), currentPhoneThemeName);
    }

    public static List<string> GetAvailablePhoneThemeNames()
    {
        List<string> themeNames = new();
        string themesRootPath = GetAbsolutePhoneThemesRootPath();

        if (Directory.Exists(themesRootPath))
        {
            foreach (string directoryPath in Directory.GetDirectories(themesRootPath))
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

    public static string ResolvePhoneThemeName(string? requestedThemeName)
    {
        List<string> availableThemes = GetAvailablePhoneThemeNames();

        if (!string.IsNullOrWhiteSpace(requestedThemeName))
        {
            string? matchedTheme = availableThemes.FirstOrDefault(name =>
                string.Equals(name, requestedThemeName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(matchedTheme))
                return matchedTheme;
        }

        return DefaultPhoneThemeName;
    }

    public static bool SetCurrentPhoneTheme(string? requestedThemeName)
    {
        string resolvedThemeName = ResolvePhoneThemeName(requestedThemeName);
        bool changed = !string.Equals(currentPhoneThemeName, resolvedThemeName, StringComparison.OrdinalIgnoreCase);
        currentPhoneThemeName = resolvedThemeName;
        return changed;
    }

    private static string GetAbsolutePhoneThemesRootPath()
    {
        string? modFolderPath = Smartphone.ModEntry.Instance?.Helper?.DirectoryPath ?? Smartphone.ModEntry.SHelper?.DirectoryPath;
        if (string.IsNullOrWhiteSpace(modFolderPath))
            return GetPhoneThemesRootPath();

        return Path.Combine(modFolderPath, GetPhoneThemesRootPath());
    }


    public class ImagesConstants
    {
        public const string PhoneBackground = "phone_background.png";
        public const string PhoneEmpty = "phone_empty.png";
        public const string Background = "background.png";

        public const string AppText = "app_text.png";
        public const string AppCamera = "app_camera.png";
        public const string AppPhoto = "app_photo.png";
        public const string AppSocial = "app_social.png";
        public const string AppSetting = "app_setting.png";
        public const string AppNotification = "app_notification.png";
    }

}