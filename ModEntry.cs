
using System;
using System.Diagnostics.Metrics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Smartphone.Data;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.GameData.Characters;
using StardewValley.Menus;
using StardewValley.Objects;

namespace Smartphone
{
    /// <summary>The mod entry point.</summary>
    public class ModApi : ISmartPhoneApi
    {
        private readonly IMonitor monitor;

        public ModApi(IMonitor monitor)
        {
            this.monitor = monitor;
        }

        public void SendSmartphoneNotification(string message, string notificationName = "", string playerId = "")
        {
            NotificationManager.addNotification(message, notificationName);
        }

        public string CaptureNpcPhoto(GameLocation targetLocation, Vector2 captureCenter, NPC npc = null, bool landscape = false, bool square = false, List<NPC>? visibleNpcAtTarget = null, float zoomLevel = 1f, int? captureTimeOfDay = null, string saveLocation = null)
        {
            return ModEntry.CaptureNpcPhoto(targetLocation, captureCenter, npc, landscape, square, visibleNpcAtTarget, zoomLevel, captureTimeOfDay, saveLocation);
        }

        public List<string> GetPlayerPhotoNames()
        {
            return ModEntry.GetPlayerPhotoNamesInternal();
        }

        public Texture2D GetPlayerPhotoTexture(string photoName)
        {
            return ModEntry.GetPlayerPhotoTextureInternal(photoName);
        }

        public string GetPlayerPhotoMetadata(string photoName)
        {
            return ModEntry.GetPlayerPhotoMetadataInternal(photoName);
        }

        public Dictionary<string, Texture2D> GetAllPlayerPhotoTextures()
        {
            return ModEntry.GetAllPlayerPhotoTexturesInternal();
        }

        public bool RegisterPhoneApp(
            string ownerModId,
            string appId,
            string displayName,
            Texture2D iconTexture,
            Action onClick,
            bool closePhoneOnLaunch = true,
            int sortOrder = 0,
            Rectangle? sourceRect = null,
            Func<bool>? isVisible = null,
            Func<int>? getBadgeCount = null)
        {
            return ModEntry.RegisterPhoneAppInternal(
                ownerModId,
                appId,
                displayName,
                iconTexture,
                onClick,
                closePhoneOnLaunch,
                sortOrder,
                sourceRect,
                isVisible,
                getBadgeCount);
        }

        public bool UnregisterPhoneApp(string ownerModId, string appId)
        {
            return ModEntry.UnregisterPhoneAppInternal(ownerModId, appId);
        }

        public bool RegisterPhoneAppGroup(
            string ownerModId,
            string groupId,
            string displayName,
            Texture2D iconTexture,
            int sortOrder = 0,
            Rectangle? sourceRect = null,
            Func<bool>? isVisible = null,
            Func<int>? getBadgeCount = null)
        {
            return ModEntry.RegisterPhoneAppGroupInternal(
                ownerModId,
                groupId,
                displayName,
                iconTexture,
                sortOrder,
                sourceRect,
                isVisible,
                getBadgeCount);
        }

        public bool UnregisterPhoneAppGroup(string ownerModId, string groupId)
        {
            return ModEntry.UnregisterPhoneAppGroupInternal(ownerModId, groupId);
        }

        public bool RegisterPhoneAppGroupItem(
            string ownerModId,
            string groupId,
            string itemId,
            string displayName,
            Texture2D iconTexture,
            Action onClick,
            bool closePhoneOnLaunch = true,
            int sortOrder = 0,
            Rectangle? sourceRect = null,
            Func<bool>? isVisible = null,
            Func<int>? getBadgeCount = null)
        {
            return ModEntry.RegisterPhoneAppGroupItemInternal(
                ownerModId,
                groupId,
                itemId,
                displayName,
                iconTexture,
                onClick,
                closePhoneOnLaunch,
                sortOrder,
                sourceRect,
                isVisible,
                getBadgeCount);
        }

        public bool UnregisterPhoneAppGroupItem(string ownerModId, string groupId, string itemId)
        {
            return ModEntry.UnregisterPhoneAppGroupItemInternal(ownerModId, groupId, itemId);
        }

        public bool OpenPhoneHomeScreen()
        {
            return ModEntry.OpenPhoneHomeScreenInternal();
        }

        public bool OpenPhoneAppGroup(string ownerModId, string groupId)
        {
            return ModEntry.OpenPhoneAppGroupInternal(ownerModId, groupId);
        }

        public bool RegisterChatQuickActionButton(
            string ownerModId,
            string actionId,
            Texture2D iconTexture,
            Action<string> onClick,
            bool closePhoneOnLaunch = false,
            int sortOrder = 0,
            Rectangle? sourceRect = null,
            List<string>? npcNames = null)
        {
            return ModEntry.RegisterChatQuickActionButtonInternal(
                ownerModId,
                actionId,
                iconTexture,
                onClick,
                closePhoneOnLaunch,
                sortOrder,
                sourceRect,
                npcNames);
        }

        public bool UnregisterChatQuickActionButton(string ownerModId, string actionId)
        {
            return ModEntry.UnregisterChatQuickActionButtonInternal(ownerModId, actionId);
        }

        public Texture2D? GetAppTexture(AppIconType appIconType)
        {
            switch (appIconType)
            {
                case AppIconType.Notification: return Textures.AppNotification;
                case AppIconType.AppStore: return Textures.AppAppStore;
                case AppIconType.Camera: return Textures.AppCamera;
                case AppIconType.Photo: return Textures.AppPhoto;
                case AppIconType.Setting: return Textures.AppSetting;
                case AppIconType.Calendar: return Textures.AppCalendar;
                default: return null;
            }
        }

        public void RetrievePhotos(int limit, bool getTexture, bool getMetadata, Action<string> onComplete, bool squareOnly = false)
        {
            ModEntry.RetrievePhotosInternal(limit, getTexture, getMetadata, onComplete, squareOnly);
        }

        public bool IsSmallPhoneSize()
        {
            return ModEntry.Config?.UseSmallPhoneSize == true;
        }

        public float GetPhoneUiScale()
        {
            return ModEntry.GetActivePhoneUiScale();
        }

        public int GetPhoneFrameWidth()
        {
            return ModEntry.GetScaledPhoneFrameWidth();
        }

        public int GetPhoneFrameHeight()
        {
            return ModEntry.GetScaledPhoneFrameHeight();
        }

        public (int offsetX, int offsetY) GetPhoneContentOffset()
        {
            return (ModEntry.GetScaledPhoneContentOffsetX(), ModEntry.GetScaledPhoneContentOffsetY());
        }

        public Texture2D? GetPhoneFrameTexture()
        {
            var texture = Textures.PhoneEmpty;
            return (texture != null && !texture.IsDisposed) ? texture : null;
        }

        public Texture2D? GetPhoneBackgroundTexture()
        {
            var texture = Textures.PhoneBackground;
            return (texture != null && !texture.IsDisposed) ? texture : null;
        }

        public (int x, int y) GetPhonePosition()
        {
            return (ModEntry.currentMenuX, ModEntry.currentMenuY);
        }

        public void SetPhonePosition(int x, int y)
        {
            ModEntry.currentMenuX = x;
            ModEntry.currentMenuY = y;
            if (ModEntry.phoneMenu != null)
            {
                ModEntry.phoneMenu.xPositionOnScreen = x;
                ModEntry.phoneMenu.yPositionOnScreen = y;
            }
        }

        public bool HandlePhoneAppBottomNavClick(int x, int y, int phoneX, int phoneY, Action? onBack = null)
        {
            float uiScale = ModEntry.GetActivePhoneUiScale();
            int scale(int val) => ModEntry.ScalePhoneUiValue(val, uiScale);

            var backBounds = new Rectangle(phoneX + scale(132), phoneY + scale(925), scale(64), scale(64));
            var lockBounds = new Rectangle(phoneX + scale(405), phoneY + scale(925), scale(64), scale(64));
            var homeBounds = new Rectangle(phoneX + scale(265), phoneY + scale(925), scale(64), scale(64));

            if (backBounds.Contains(x, y))
            {
                Game1.playSound("cancel");
                if (onBack != null)
                {
                    onBack();
                }
                else
                {
                    Game1.activeClickableMenu?.exitThisMenuNoSound();
                    ModEntry.OpenPhoneFromHudTrigger();
                }
                return true;
            }

            if (lockBounds.Contains(x, y))
            {
                Game1.playSound("cancel");
                Game1.activeClickableMenu?.exitThisMenuNoSound();
                return true;
            }

            if (homeBounds.Contains(x, y))
            {
                Game1.playSound("bigSelect");
                Game1.activeClickableMenu?.exitThisMenuNoSound();
                PhoneMenu.currentApp = null;
                ModEntry.OpenPhoneFromHudTrigger();
                return true;
            }

            return false;
        }

    }

    public partial class ModEntry : Mod
    {


        public static ModEntry Instance;

        public static IMonitor SMonitor;
        public static IModHelper SHelper;
        public static IUnlimitedEventExpansionApi iUnlimitedEventExpansionApi;

        private ModApi apiInstance;

        public override object GetApi()
        {
            return this.apiInstance ??= new ModApi(Monitor);
        }

        public static ModConfig Config;

        public static JToken eventString;

        public static string currentPhoneTheme = "Default";
        public static string currentPhoneBackground = "";
        public static string currentPhoneSound = "bigSelect";
        public static string currentPhoneTextColor = "Black";
        public static string currentPlayerProfile = "";
        public static string currentPlayerBirthDate = "";
        public static string currentPlayerBirthSeason = "";
        public static string currentPlayerAge = "";

        public static bool takeScreenshot = false;
        public static int currentMenuX;
        public static int currentMenuY;
        public static bool cameraLandscapeMode = false;
        public static bool cameraSquareMode = false;
        public static bool cameraFlashMode = false;
        public static float cameraZoomFactor = 1f;

        public static bool pendingInitNotification = false;
        public static bool pendingPhoneOsInitialization = false;
        public static bool hasNewVersionAvailable = false;


        private static readonly object SaveFolderNameLock = new();
        private static string activeSaveFolderName = string.Empty;

        public static PhoneMenu phoneMenu;
        private Dictionary<string, Dictionary<string, AreaData>> areaTags;
        public static string GetActiveSaveFolderName()
        {
            lock (SaveFolderNameLock)
            {
                if (string.IsNullOrWhiteSpace(activeSaveFolderName))
                    activeSaveFolderName = ResolveSaveFolderNameFromContext();

                return activeSaveFolderName;
            }
        }

        public static string GetSaveDataPath(string fileName = "")
        {
            string normalizedFileName = (fileName ?? string.Empty)
                .Trim()
                .TrimStart('/', '\\');

            if (string.IsNullOrWhiteSpace(normalizedFileName))
                return $"./userdata/{GetActiveSaveFolderName()}";

            return $"./userdata/{GetActiveSaveFolderName()}/{normalizedFileName}";
        }

        public static void RefreshActiveSaveFolderName()
        {
            string resolved = ResolveSaveFolderNameFromContext();
            lock (SaveFolderNameLock)
                activeSaveFolderName = resolved;
        }

        public static void SetActiveSaveFolderName(string saveFolderName)
        {
            string normalizedSaveFolderName = NormalizeSaveFolderName(saveFolderName);
            if (string.IsNullOrWhiteSpace(normalizedSaveFolderName))
                return;

            lock (SaveFolderNameLock)
                activeSaveFolderName = normalizedSaveFolderName;
        }

        public static void ClearActiveSaveFolderName()
        {
            lock (SaveFolderNameLock)
                activeSaveFolderName = string.Empty;
        }

        private static string ResolveSaveFolderNameFromContext()
        {
            string constantsSaveFolder = NormalizeSaveFolderName(Constants.SaveFolderName);

            if (!string.IsNullOrWhiteSpace(constantsSaveFolder))
            {
                int underscoreIndex = constantsSaveFolder.IndexOf('_');

                // If an underscore is found, return from the underscore onwards
                if (underscoreIndex != -1)
                {
                    return constantsSaveFolder.Substring(underscoreIndex); // e.g., "_12345"
                }
            }

            long uniqueId = 0;

            if (Context.IsWorldReady && Context.IsMultiplayer && Game1.MasterPlayer != null)
            {
                uniqueId = Game1.MasterPlayer.UniqueMultiplayerID;
            }
            else if (Context.IsWorldReady && Game1.player != null)
            {
                uniqueId = Game1.player.UniqueMultiplayerID;
            }

            if (uniqueId > 0)
                return $"_{uniqueId}";

            if (!string.IsNullOrWhiteSpace(constantsSaveFolder))
            {
                int lastUnderscore = constantsSaveFolder.LastIndexOf('_');
                if (lastUnderscore >= 0 && lastUnderscore < constantsSaveFolder.Length - 1)
                {
                    string possibleId = constantsSaveFolder.Substring(lastUnderscore + 1);
                    if (long.TryParse(possibleId, out _))
                        return $"_{possibleId}";
                }

                return constantsSaveFolder;
            }

            return "default";
        }

        private static string NormalizeSaveFolderName(string saveFolderName)
        {
            string normalizedValue = (saveFolderName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedValue))
                return string.Empty;

            char[] invalidChars = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(normalizedValue.Length);
            foreach (char character in normalizedValue)
            {
                if (character == '/'
                    || character == '\\'
                    || Array.IndexOf(invalidChars, character) >= 0)
                {
                    continue;
                }

                builder.Append(character);
            }

            return builder.ToString().Trim();
        }



        public static List<string> GetPlayerPhotoNamesInternal()
        {
            return ImageMetadataStore.Keys.ToList();
        }

        public static Texture2D GetPlayerPhotoTextureInternal(string photoName)
        {
            string photoPath = Path.Combine(SHelper.DirectoryPath, "userdata", GetActiveSaveFolderName(), "photo_player", photoName);
            if (File.Exists(photoPath))
            {
                using var stream = new FileStream(photoPath, FileMode.Open, FileAccess.Read);
                return Texture2D.FromStream(Game1.graphics.GraphicsDevice, stream);
            }
            return null;
        }

        public static string GetPlayerPhotoMetadataInternal(string photoName)
        {
            if (ImageMetadataStore.TryGetValue(photoName, out var metadata))
            {
                return Newtonsoft.Json.JsonConvert.SerializeObject(metadata);
            }
            return null;
        }

        public static Dictionary<string, Texture2D> GetAllPlayerPhotoTexturesInternal()
        {
            var dict = new Dictionary<string, Texture2D>();
            string folderPath = Path.Combine(SHelper.DirectoryPath, "userdata", GetActiveSaveFolderName(), "photo_player");

            if (Directory.Exists(folderPath))
            {
                var sortedFiles = Directory.GetFiles(folderPath, "*.jpg")
                                           .Select(f => new FileInfo(f))
                                           .OrderByDescending(fi => fi.LastWriteTime)
                                           .Select(fi => fi.FullName);

                foreach (string photoPath in sortedFiles)
                {
                    string photoName = Path.GetFileName(photoPath);
                    try
                    {
                        using var stream = new FileStream(photoPath, FileMode.Open, FileAccess.Read);
                        var texture = Texture2D.FromStream(Game1.graphics.GraphicsDevice, stream);
                        dict[photoName] = texture;
                    }
                    catch { }
                }
            }
            return dict;
        }

        // =========================================================================================
        // =========================================================================================
        // =========================================================================================





        public static void RetrievePhotosInternal(int limit, bool getTexture, bool getMetadata, Action<string> onComplete, bool squareOnly = false)
        {
            if (limit <= 0)
            {
                onComplete?.Invoke("[]");
                return;
            }

            EnsurePhoneMenuUsesCurrentScale();

            // Set state on phone menu
            phoneMenu.StartPhotoSelectionApiMode(limit, getTexture, getMetadata, onComplete, squareOnly);

            // Open the phone menu and bypass lock screen, going directly to the photo app
            phoneMenu.rootLandingState = PhoneMenu.RootLandingState.Home;
            phoneMenu.OpenPhotoApp();
            Game1.activeClickableMenu = phoneMenu;
        }
    }
}