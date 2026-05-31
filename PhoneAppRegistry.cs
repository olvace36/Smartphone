using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;

namespace Smartphone
{
    internal enum RegisteredPhoneAppKind
    {
        Action = 0,
        Group = 1
    }

    internal sealed class RegisteredPhoneApp
    {
        public string OwnerModId { get; init; } = "";
        public string AppId { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public Texture2D IconTexture { get; init; } = null!;
        public Action? OnClick { get; init; }
        public bool ClosePhoneOnLaunch { get; init; }
        public int SortOrder { get; init; }
        public Rectangle? SourceRect { get; init; }
        public Func<bool>? IsVisible { get; init; }
        public Func<int>? GetBadgeCount { get; init; }
        public RegisteredPhoneAppKind Kind { get; init; } = RegisteredPhoneAppKind.Action;

        public string CompositeId => BuildCompositeId(this.OwnerModId, this.AppId);

        public static string BuildCompositeId(string ownerModId, string appId)
        {
            return $"{ownerModId.Trim()}::{appId.Trim()}";
        }
    }

    internal sealed class RegisteredPhoneAppGroupItem
    {
        public string OwnerModId { get; init; } = "";
        public string GroupId { get; init; } = "";
        public string ItemId { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public Texture2D IconTexture { get; init; } = null!;
        public Action OnClick { get; init; } = null!;
        public bool ClosePhoneOnLaunch { get; init; }
        public int SortOrder { get; init; }
        public Rectangle? SourceRect { get; init; }
        public Func<bool>? IsVisible { get; init; }
        public Func<int>? GetBadgeCount { get; init; }

        public string GroupCompositeId => RegisteredPhoneApp.BuildCompositeId(this.OwnerModId, this.GroupId);
        public string CompositeId => BuildCompositeId(this.OwnerModId, this.GroupId, this.ItemId);

        public static string BuildCompositeId(string ownerModId, string groupId, string itemId)
        {
            return $"{ownerModId.Trim()}::{groupId.Trim()}::{itemId.Trim()}";
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
        private const int MaxItemsPerRegisteredPhoneAppGroup = 9;

        private static readonly object RegisteredPhoneAppsLock = new();
        private static readonly Dictionary<string, RegisteredPhoneApp> RegisteredPhoneApps = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Dictionary<string, RegisteredPhoneAppGroupItem>> RegisteredPhoneAppGroupItems = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, RegisteredChatQuickActionButton> RegisteredChatQuickActionButtons = new(StringComparer.OrdinalIgnoreCase);

        internal static bool RegisterPhoneAppInternal(
            string ownerModId,
            string appId,
            string displayName,
            Texture2D iconTexture,
            Action onClick,
            bool closePhoneOnLaunch,
            int sortOrder,
            Rectangle? sourceRect,
            Func<bool>? isVisible,
            Func<int>? getBadgeCount)
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
                sortOrder,
                sourceRect,
                isVisible,
                getBadgeCount,
                RegisteredPhoneAppKind.Action);
        }

        internal static bool RegisterPhoneAppGroupInternal(
            string ownerModId,
            string groupId,
            string displayName,
            Texture2D iconTexture,
            int sortOrder,
            Rectangle? sourceRect,
            Func<bool>? isVisible,
            Func<int>? getBadgeCount)
        {
            return RegisterPhoneAppCore(
                ownerModId,
                groupId,
                displayName,
                iconTexture,
                null,
                closePhoneOnLaunch: false,
                sortOrder,
                sourceRect,
                isVisible,
                getBadgeCount,
                RegisteredPhoneAppKind.Group);
        }

        private static bool RegisterPhoneAppCore(
            string ownerModId,
            string appId,
            string displayName,
            Texture2D iconTexture,
            Action? onClick,
            bool closePhoneOnLaunch,
            int sortOrder,
            Rectangle? sourceRect,
            Func<bool>? isVisible,
            Func<int>? getBadgeCount,
            RegisteredPhoneAppKind kind)
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

            string key = RegisteredPhoneApp.BuildCompositeId(ownerModId, appId);
            var app = new RegisteredPhoneApp
            {
                OwnerModId = ownerModId.Trim(),
                AppId = appId.Trim(),
                DisplayName = displayName.Trim(),
                IconTexture = iconTexture,
                OnClick = onClick,
                ClosePhoneOnLaunch = closePhoneOnLaunch,
                SortOrder = sortOrder,
                SourceRect = sourceRect,
                IsVisible = isVisible,
                GetBadgeCount = getBadgeCount,
                Kind = kind
            };

            bool replaced;
            lock (RegisteredPhoneAppsLock)
            {
                replaced = RegisteredPhoneApps.ContainsKey(key);
                RegisteredPhoneApps[key] = app;

                if (!RegisteredPhoneAppGroupItems.ContainsKey(key))
                    RegisteredPhoneAppGroupItems[key] = new Dictionary<string, RegisteredPhoneAppGroupItem>(StringComparer.OrdinalIgnoreCase);
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
                RegisteredPhoneAppGroupItems.Remove(key);
            }

            if (removed)
                SMonitor?.Log($"Unregistered smartphone app '{key}'.", LogLevel.Trace);

            return removed;
        }

        internal static bool UnregisterPhoneAppGroupInternal(string ownerModId, string groupId)
        {
            return UnregisterPhoneAppInternal(ownerModId, groupId);
        }

        internal static bool RegisterPhoneAppGroupItemInternal(
            string ownerModId,
            string groupId,
            string itemId,
            string displayName,
            Texture2D iconTexture,
            Action onClick,
            bool closePhoneOnLaunch,
            int sortOrder,
            Rectangle? sourceRect,
            Func<bool>? isVisible,
            Func<int>? getBadgeCount)
        {
            if (string.IsNullOrWhiteSpace(ownerModId)
                || string.IsNullOrWhiteSpace(groupId)
                || string.IsNullOrWhiteSpace(itemId)
                || string.IsNullOrWhiteSpace(displayName)
                || iconTexture == null
                || onClick == null)
            {
                SMonitor?.Log("RegisterPhoneAppGroupItem failed: ownerModId, groupId, itemId, displayName, iconTexture, and onClick are required.", LogLevel.Warn);
                return false;
            }

            if (sourceRect.HasValue && (sourceRect.Value.Width <= 0 || sourceRect.Value.Height <= 0))
            {
                SMonitor?.Log($"RegisterPhoneAppGroupItem failed for '{ownerModId}:{groupId}:{itemId}': sourceRect must have positive width and height.", LogLevel.Warn);
                return false;
            }

            string groupCompositeId = RegisteredPhoneApp.BuildCompositeId(ownerModId, groupId);
            string itemCompositeId = RegisteredPhoneAppGroupItem.BuildCompositeId(ownerModId, groupId, itemId);

            var item = new RegisteredPhoneAppGroupItem
            {
                OwnerModId = ownerModId.Trim(),
                GroupId = groupId.Trim(),
                ItemId = itemId.Trim(),
                DisplayName = displayName.Trim(),
                IconTexture = iconTexture,
                OnClick = onClick,
                ClosePhoneOnLaunch = closePhoneOnLaunch,
                SortOrder = sortOrder,
                SourceRect = sourceRect,
                IsVisible = isVisible,
                GetBadgeCount = getBadgeCount
            };

            bool replaced;
            lock (RegisteredPhoneAppsLock)
            {
                if (!RegisteredPhoneApps.TryGetValue(groupCompositeId, out var groupApp) || groupApp.Kind != RegisteredPhoneAppKind.Group)
                {
                    SMonitor?.Log($"RegisterPhoneAppGroupItem failed for '{itemCompositeId}': group '{groupCompositeId}' is not registered.", LogLevel.Warn);
                    return false;
                }

                if (!RegisteredPhoneAppGroupItems.TryGetValue(groupCompositeId, out var itemsById))
                {
                    itemsById = new Dictionary<string, RegisteredPhoneAppGroupItem>(StringComparer.OrdinalIgnoreCase);
                    RegisteredPhoneAppGroupItems[groupCompositeId] = itemsById;
                }

                replaced = itemsById.ContainsKey(itemCompositeId);
                if (!replaced && itemsById.Count >= MaxItemsPerRegisteredPhoneAppGroup)
                {
                    SMonitor?.Log($"RegisterPhoneAppGroupItem failed for '{itemCompositeId}': max {MaxItemsPerRegisteredPhoneAppGroup} items per group.", LogLevel.Warn);
                    return false;
                }

                itemsById[itemCompositeId] = item;
            }

            SMonitor?.Log(
                replaced
                    ? $"Updated smartphone app-group item '{itemCompositeId}'."
                    : $"Registered smartphone app-group item '{itemCompositeId}'.",
                LogLevel.Trace);
            return true;
        }

        internal static bool UnregisterPhoneAppGroupItemInternal(string ownerModId, string groupId, string itemId)
        {
            if (string.IsNullOrWhiteSpace(ownerModId)
                || string.IsNullOrWhiteSpace(groupId)
                || string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }

            string groupCompositeId = RegisteredPhoneApp.BuildCompositeId(ownerModId, groupId);
            string itemCompositeId = RegisteredPhoneAppGroupItem.BuildCompositeId(ownerModId, groupId, itemId);

            bool removed = false;
            lock (RegisteredPhoneAppsLock)
            {
                if (RegisteredPhoneAppGroupItems.TryGetValue(groupCompositeId, out var itemsById))
                    removed = itemsById.Remove(itemCompositeId);
            }

            if (removed)
                SMonitor?.Log($"Unregistered smartphone app-group item '{itemCompositeId}'.", LogLevel.Trace);

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
                    .OrderBy(p => p.SortOrder)
                    .ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
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

        internal static List<RegisteredPhoneAppGroupItem> GetRegisteredPhoneAppGroupItemsSnapshot(string groupCompositeId)
        {
            if (string.IsNullOrWhiteSpace(groupCompositeId))
                return new List<RegisteredPhoneAppGroupItem>();

            List<RegisteredPhoneAppGroupItem> snapshot;
            lock (RegisteredPhoneAppsLock)
            {
                if (!RegisteredPhoneAppGroupItems.TryGetValue(groupCompositeId, out var itemsById))
                    return new List<RegisteredPhoneAppGroupItem>();

                snapshot = itemsById.Values
                    .OrderBy(p => p.SortOrder)
                    .ThenBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p => p.CompositeId, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            List<RegisteredPhoneAppGroupItem> visibleItems = new();
            foreach (RegisteredPhoneAppGroupItem item in snapshot)
            {
                if (IsRegisteredPhoneAppGroupItemVisible(item))
                    visibleItems.Add(item);
            }

            return visibleItems;
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
                if (app.Kind == RegisteredPhoneAppKind.Group)
                {
                    menu.OpenRegisteredAppGroup(app.CompositeId, app.DisplayName);
                    return true;
                }

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

        internal static bool TryOpenRegisteredPhoneAppGroup(string groupCompositeId, PhoneMenu menu)
        {
            if (string.IsNullOrWhiteSpace(groupCompositeId))
                return false;

            RegisteredPhoneApp? app;
            lock (RegisteredPhoneAppsLock)
                RegisteredPhoneApps.TryGetValue(groupCompositeId, out app);

            if (app == null || app.Kind != RegisteredPhoneAppKind.Group)
                return false;

            if (!IsRegisteredPhoneAppVisible(app))
                return false;

            try
            {
                menu.OpenRegisteredAppGroup(app.CompositeId, app.DisplayName);
                return true;
            }
            catch (Exception ex)
            {
                SMonitor?.Log($"Error while opening smartphone app group '{groupCompositeId}': {ex}", LogLevel.Error);
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

        internal static bool OpenPhoneAppGroupInternal(string ownerModId, string groupId)
        {
            if (!Context.IsWorldReady
                || string.IsNullOrWhiteSpace(ownerModId)
                || string.IsNullOrWhiteSpace(groupId))
            {
                return false;
            }

            EnsurePhoneMenuUsesCurrentScale();

            phoneMenu.OpenHomeScreen();

            string groupCompositeId = RegisteredPhoneApp.BuildCompositeId(ownerModId, groupId);
            if (!TryOpenRegisteredPhoneAppGroup(groupCompositeId, phoneMenu))
                return false;

            Game1.activeClickableMenu = phoneMenu;
            return true;
        }

        internal static bool TryInvokeRegisteredPhoneAppGroupItem(string groupCompositeId, string itemCompositeId, PhoneMenu menu)
        {
            if (string.IsNullOrWhiteSpace(groupCompositeId) || string.IsNullOrWhiteSpace(itemCompositeId))
                return false;

            RegisteredPhoneAppGroupItem? item;
            lock (RegisteredPhoneAppsLock)
            {
                if (!RegisteredPhoneAppGroupItems.TryGetValue(groupCompositeId, out var itemsById))
                    return false;

                if (!itemsById.TryGetValue(itemCompositeId, out item))
                    return false;
            }

            if (item == null)
                return false;

            if (!IsRegisteredPhoneAppGroupItemVisible(item))
                return false;

            try
            {
                if (item.ClosePhoneOnLaunch)
                    menu.ClosePhoneMenu();

                item.OnClick.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                SMonitor?.Log($"Error while opening smartphone app-group item '{item.CompositeId}': {ex}", LogLevel.Error);
                return false;
            }
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

        private static bool IsRegisteredPhoneAppGroupItemVisible(RegisteredPhoneAppGroupItem item)
        {
            if (item.IsVisible == null)
                return true;

            try
            {
                return item.IsVisible.Invoke();
            }
            catch (Exception ex)
            {
                SMonitor?.Log($"Visibility callback failed for smartphone app-group item '{item.CompositeId}': {ex.Message}", LogLevel.Warn);
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
