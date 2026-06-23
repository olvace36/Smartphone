using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace Smartphone
{
    internal sealed class RegisteredPhoneApp
    {
        public string OwnerModId { get; init; } = "";
        public string AppId { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public Texture2D IconTexture { get; init; } = null!;
        public Action? OnClick { get; init; }
        public bool ClosePhoneOnLaunch { get; init; }
        public Rectangle? SourceRect { get; init; }
        public Func<bool>? IsVisible { get; init; }
        public Func<int>? GetBadgeCount { get; init; }
        public List<AppSize> SupportedSizes { get; init; } = new() { AppSize.Size1x1 };
        public Action<SpriteBatch, Rectangle, AppSize>? OnDrawWidget { get; init; }

        public string CompositeId => BuildCompositeId(this.OwnerModId, this.AppId);

        public static string BuildCompositeId(string ownerModId, string appId)
        {
            return $"{ownerModId.Trim()}::{appId.Trim()}";
        }
    }

    internal sealed class RegisteredChatQuickActionButton
    {
        public string OwnerModId { get; init; } = "";
        public string ActionId { get; init; } = "";
        public Texture2D IconTexture { get; init; } = null!;
        public Action<string> OnClick { get; init; } = null!;
        public bool ClosePhoneOnLaunch { get; init; }
        public int SortOrder { get; init; }
        public Rectangle? SourceRect { get; init; }
        public HashSet<string>? AllowedNpcNames { get; init; }

        public string CompositeId => BuildCompositeId(this.OwnerModId, this.ActionId);

        public static string BuildCompositeId(string ownerModId, string actionId)
        {
            return $"{ownerModId.Trim()}::{actionId.Trim()}";
        }
    }

    public partial class ModEntry
    {
        private static readonly object RegisteredPhoneAppsLock = new();
        private static readonly Dictionary<string, RegisteredPhoneApp> RegisteredPhoneApps = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, RegisteredChatQuickActionButton> RegisteredChatQuickActionButtons = new(StringComparer.OrdinalIgnoreCase);

        internal static bool RegisterPhoneAppInternal(
            string ownerModId,
            string appId,
            string displayName,
            Texture2D iconTexture,
            Action onClick,
            bool closePhoneOnLaunch,
            Rectangle? sourceRect,
            Func<bool>? isVisible,
            Func<int>? getBadgeCount,
            List<AppSize>? supportedSizes = null,
            Action<SpriteBatch, Rectangle, AppSize>? onDrawWidget = null) // <-- Add here
        {
            if (onClick == null)
            {
                SMonitor?.Log("RegisterPhoneApp failed: onClick is required for normal apps.", LogLevel.Warn);
                return false;
            }

            return RegisterPhoneAppCore(
                ownerModId,
                appId,
                displayName,
                iconTexture,
                onClick,
                closePhoneOnLaunch,
                sourceRect,
                isVisible,
                getBadgeCount,
                supportedSizes,
                onDrawWidget); // <-- Forward here
        }

        private static bool RegisterPhoneAppCore(
            string ownerModId,
            string appId,
            string displayName,
            Texture2D iconTexture,
            Action? onClick,
            bool closePhoneOnLaunch,
            Rectangle? sourceRect,
            Func<bool>? isVisible,
            Func<int>? getBadgeCount,
            List<AppSize>? supportedSizes = null,
            Action<SpriteBatch, Rectangle, AppSize>? onDrawWidget = null) // <-- Add here
        {
            if (string.IsNullOrWhiteSpace(ownerModId)
                || string.IsNullOrWhiteSpace(appId)
                || string.IsNullOrWhiteSpace(displayName)
                || iconTexture == null)
            {
                SMonitor?.Log("RegisterPhoneApp failed: ownerModId, appId/groupId, displayName, and iconTexture are required.", LogLevel.Warn);
                return false;
            }

            if (sourceRect.HasValue && (sourceRect.Value.Width <= 0 || sourceRect.Value.Height <= 0))
            {
                SMonitor?.Log($"RegisterPhoneApp failed for '{ownerModId}:{appId}': sourceRect must have positive width and height.", LogLevel.Warn);
                return false;
            }

            List<AppSize> sizes = (supportedSizes != null && supportedSizes.Count > 0)
                ? supportedSizes
                : new List<AppSize> { AppSize.Size1x1 };

            string key = RegisteredPhoneApp.BuildCompositeId(ownerModId, appId);
            var app = new RegisteredPhoneApp
            {
                OwnerModId = ownerModId.Trim(),
                AppId = appId.Trim(),
                DisplayName = displayName.Trim(),
                IconTexture = iconTexture,
                OnClick = onClick,
                ClosePhoneOnLaunch = closePhoneOnLaunch,
                SourceRect = sourceRect,
                IsVisible = isVisible,
                GetBadgeCount = getBadgeCount,
                SupportedSizes = sizes,
                OnDrawWidget = onDrawWidget // <-- Assign property here
            };

            bool replaced;
            lock (RegisteredPhoneAppsLock)
            {
                replaced = RegisteredPhoneApps.ContainsKey(key);
                RegisteredPhoneApps[key] = app;
            }

            SMonitor?.Log(
                replaced
                    ? $"Updated smartphone app registration '{key}'."
                    : $"Registered smartphone app '{key}'.",
                LogLevel.Trace);
            return true;
        }

        internal static bool UnregisterPhoneAppInternal(string ownerModId, string appId)
        {
            if (string.IsNullOrWhiteSpace(ownerModId) || string.IsNullOrWhiteSpace(appId))
                return false;

            string key = RegisteredPhoneApp.BuildCompositeId(ownerModId, appId);
            bool removed;
            lock (RegisteredPhoneAppsLock)
            {
                removed = RegisteredPhoneApps.Remove(key);
            }

            if (removed)
                SMonitor?.Log($"Unregistered smartphone app '{key}'.", LogLevel.Trace);

            return removed;
        }





        internal static bool RegisterChatQuickActionButtonInternal(
            string ownerModId,
            string actionId,
            Texture2D iconTexture,
            Action<string> onClick,
            bool closePhoneOnLaunch,
            int sortOrder,
            Rectangle? sourceRect,
            List<string>? npcNames)
        {
            if (string.IsNullOrWhiteSpace(ownerModId)
                || string.IsNullOrWhiteSpace(actionId)
                || iconTexture == null
                || onClick == null)
            {
                SMonitor?.Log("RegisterChatQuickActionButton failed: ownerModId, actionId, iconTexture, and onClick are required.", LogLevel.Warn);
                return false;
            }

            if (sourceRect.HasValue && (sourceRect.Value.Width <= 0 || sourceRect.Value.Height <= 0))
            {
                SMonitor?.Log($"RegisterChatQuickActionButton failed for '{ownerModId}:{actionId}': sourceRect must have positive width and height.", LogLevel.Warn);
                return false;
            }

            HashSet<string>? allowedNpcNames = null;
            if (npcNames != null)
            {
                var sanitizedNames = npcNames
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name.Trim())
                    .ToList();

                if (sanitizedNames.Count > 0)
                    allowedNpcNames = new HashSet<string>(sanitizedNames, StringComparer.OrdinalIgnoreCase);
            }

            string key = RegisteredChatQuickActionButton.BuildCompositeId(ownerModId, actionId);
            var action = new RegisteredChatQuickActionButton
            {
                OwnerModId = ownerModId.Trim(),
                ActionId = actionId.Trim(),
                IconTexture = iconTexture,
                OnClick = onClick,
                ClosePhoneOnLaunch = closePhoneOnLaunch,
                SortOrder = sortOrder,
                SourceRect = sourceRect,
                AllowedNpcNames = allowedNpcNames
            };

            bool replaced;
            lock (RegisteredPhoneAppsLock)
            {
                replaced = RegisteredChatQuickActionButtons.ContainsKey(key);
                RegisteredChatQuickActionButtons[key] = action;
            }

            SMonitor?.Log(
                replaced
                    ? $"Updated chat quick-action button '{key}'."
                    : $"Registered chat quick-action button '{key}'.",
                LogLevel.Trace);
            return true;
        }

        internal static bool UnregisterChatQuickActionButtonInternal(string ownerModId, string actionId)
        {
            if (string.IsNullOrWhiteSpace(ownerModId) || string.IsNullOrWhiteSpace(actionId))
                return false;

            string key = RegisteredChatQuickActionButton.BuildCompositeId(ownerModId, actionId);

            bool removed;
            lock (RegisteredPhoneAppsLock)
                removed = RegisteredChatQuickActionButtons.Remove(key);

            if (removed)
                SMonitor?.Log($"Unregistered chat quick-action button '{key}'.", LogLevel.Trace);

            return removed;
        }

        internal static List<RegisteredChatQuickActionButton> GetRegisteredChatQuickActionButtonsSnapshot(string selectedNpcName)
        {
            if (string.IsNullOrWhiteSpace(selectedNpcName))
                return new List<RegisteredChatQuickActionButton>();

            List<RegisteredChatQuickActionButton> snapshot;
            lock (RegisteredPhoneAppsLock)
            {
                snapshot = RegisteredChatQuickActionButtons.Values
                    .OrderBy(p => p.SortOrder)
                    .ThenBy(p => p.CompositeId, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            var visibleActions = new List<RegisteredChatQuickActionButton>();
            foreach (RegisteredChatQuickActionButton action in snapshot)
            {
                if (IsRegisteredChatQuickActionButtonVisibleForNpc(action, selectedNpcName))
                    visibleActions.Add(action);
            }

            return visibleActions;
        }

        internal static bool TryInvokeRegisteredChatQuickActionButton(string compositeId, string selectedNpcName, PhoneMenu menu)
        {
            if (string.IsNullOrWhiteSpace(compositeId) || string.IsNullOrWhiteSpace(selectedNpcName))
                return false;

            RegisteredChatQuickActionButton? action;
            lock (RegisteredPhoneAppsLock)
                RegisteredChatQuickActionButtons.TryGetValue(compositeId, out action);

            if (action == null)
                return false;

            if (!IsRegisteredChatQuickActionButtonVisibleForNpc(action, selectedNpcName))
                return false;

            try
            {
                if (action.ClosePhoneOnLaunch)
                    menu.ClosePhoneMenu();

                action.OnClick.Invoke(selectedNpcName);
                return true;
            }
            catch (Exception ex)
            {
                SMonitor?.Log($"Error while invoking chat quick-action button '{action.CompositeId}': {ex}", LogLevel.Error);
                return false;
            }
        }

        internal static List<RegisteredPhoneApp> GetRegisteredPhoneAppsSnapshot()
        {
            List<RegisteredPhoneApp> snapshot;
            lock (RegisteredPhoneAppsLock)
            {
                snapshot = RegisteredPhoneApps.Values
                    .OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p => p.CompositeId, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            List<RegisteredPhoneApp> visibleApps = new();
            foreach (RegisteredPhoneApp app in snapshot)
            {
                if (IsRegisteredPhoneAppVisible(app))
                    visibleApps.Add(app);
            }

            return visibleApps;
        }

        internal static bool TryInvokeRegisteredPhoneApp(string compositeId, PhoneMenu menu)
        {
            if (string.IsNullOrWhiteSpace(compositeId))
                return false;

            RegisteredPhoneApp? app;
            lock (RegisteredPhoneAppsLock)
                RegisteredPhoneApps.TryGetValue(compositeId, out app);

            if (app == null)
                return false;

            if (!IsRegisteredPhoneAppVisible(app))
                return false;

            try
            {
                if (app.ClosePhoneOnLaunch)
                    menu.ClosePhoneMenu();

                app.OnClick?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                SMonitor?.Log($"Error while opening smartphone app '{app.CompositeId}': {ex}", LogLevel.Error);
                return false;
            }
        }

        internal static bool OpenPhoneHomeScreenInternal()
        {
            if (!Context.IsWorldReady)
                return false;

            EnsurePhoneMenuUsesCurrentScale();

            phoneMenu.OpenHomeScreen();
            Game1.activeClickableMenu = phoneMenu;
            return true;
        }

        private static bool IsRegisteredPhoneAppVisible(RegisteredPhoneApp app)
        {
            if (app.IsVisible == null)
                return true;

            try
            {
                return app.IsVisible.Invoke();
            }
            catch (Exception ex)
            {
                SMonitor?.Log($"Visibility callback failed for smartphone app '{app.CompositeId}': {ex.Message}", LogLevel.Warn);
                return false;
            }
        }

        private static bool IsRegisteredChatQuickActionButtonVisibleForNpc(RegisteredChatQuickActionButton action, string selectedNpcName)
        {
            if (action.AllowedNpcNames != null
                && action.AllowedNpcNames.Count > 0
                && !action.AllowedNpcNames.Contains(selectedNpcName))
            {
                return false;
            }

            return true;
        }
    }
}
