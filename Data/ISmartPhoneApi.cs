using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Smartphone
{
    public interface ISmartPhoneApi
    {
        /// ======================================
        /// API to register custom apps or app groups on the smartphone home screen.
        /// ======================================

        /// <summary>
        /// Registers a custom app icon on the smartphone home screen.
        /// </summary>
        /// <param name="ownerModId">The unique ID of the mod that owns this app (e.g. d5a1lamdtd.markettown).</param>
        /// <param name="appId">A unique app ID within the owner mod (e.g. d5a1lamdtd.markettown.marketlog).</param>
        /// <param name="displayName">Name shown as a label under the app icon (e.g. Market Log).</param>
        /// <param name="iconTexture">Texture used as the app icon (any size but should be square and above 84*84).</param>
        /// <param name="onClick">Callback invoked when the app icon is clicked.</param>
        /// <param name="closePhoneOnLaunch">Whether the phone menu should close before invoking <paramref name="onClick"/>.</param>
        /// <param name="sortOrder">Lower values are shown earlier in the app grid.</param>
        /// <param name="sourceRect">Optional source rectangle if the icon is part of a spritesheet. If null, the full texture is used.</param>
        /// <param name="isVisible">Optional callback to decide whether the icon should currently be visible (e.g. () => Context.IsWorldReady).</param>
        /// <param name="getBadgeCount">Optional callback to draw a badge count on the icon.</param>
        /// <returns>True if registration succeeded; otherwise false.</returns>
        bool RegisterPhoneApp(
            string ownerModId,
            string appId,
            string displayName,
            Texture2D iconTexture,
            Action onClick,
            bool closePhoneOnLaunch = true,
            int sortOrder = 0,
            Rectangle? sourceRect = null,
            Func<bool>? isVisible = null,
            Func<int>? getBadgeCount = null
        );

        /// <summary>
        /// Unregisters a previously registered custom app.
        /// </summary>
        /// <param name="ownerModId">The unique ID of the mod that owns this app.</param>
        /// <param name="appId">The app ID that was used during registration.</param>
        /// <returns>True if an app was removed; otherwise false.</returns>
        bool UnregisterPhoneApp(string ownerModId, string appId);

        /// <summary>
        /// Registers a grouped app on the smartphone home screen. Clicking it opens a built-in
        /// app-group page managed by Smartphone.
        /// </summary>
        /// <param name="ownerModId">The unique ID of the mod that owns this app group.</param>
        /// <param name="groupId">A unique group ID within the owner mod.</param>
        /// <param name="displayName">Name shown as a label under the app icon.</param>
        /// <param name="iconTexture">Texture used as the app icon (any size; it is fit into the phone icon slot while preserving aspect ratio).</param>
        /// <param name="sortOrder">Lower values are shown earlier in the app grid.</param>
        /// <param name="sourceRect">Optional source rectangle if the icon is part of a spritesheet.</param>
        /// <param name="isVisible">Optional callback to decide whether the icon should currently be visible.</param>
        /// <param name="getBadgeCount">Optional callback to draw a badge count on the icon.</param>
        /// <returns>True if registration succeeded; otherwise false.</returns>
        bool RegisterPhoneAppGroup(
            string ownerModId,
            string groupId,
            string displayName,
            Texture2D iconTexture,
            int sortOrder = 0,
            Rectangle? sourceRect = null,
            Func<bool>? isVisible = null,
            Func<int>? getBadgeCount = null
        );

        /// <summary>
        /// Unregisters a previously registered app group and all of its items.
        /// </summary>
        /// <param name="ownerModId">The unique ID of the mod that owns this app group.</param>
        /// <param name="groupId">The group ID used during registration.</param>
        /// <returns>True if a group was removed; otherwise false.</returns>
        bool UnregisterPhoneAppGroup(string ownerModId, string groupId);

        /// <summary>
        /// Registers or updates an item inside a phone app group.
        /// </summary>
        /// <param name="ownerModId">The unique ID of the mod that owns this app group.</param>
        /// <param name="groupId">The group ID that should contain this item.</param>
        /// <param name="itemId">A unique item ID within the app group.</param>
        /// <param name="displayName">Name shown below the item icon in the group page.</param>
        /// <param name="iconTexture">Texture used as the item icon (any size; it is fit into the phone icon slot while preserving aspect ratio).</param>
        /// <param name="onClick">Callback invoked when the item is clicked.</param>
        /// <param name="closePhoneOnLaunch">Whether the phone menu should close before invoking <paramref name="onClick"/>.</param>
        /// <param name="sortOrder">Lower values are shown earlier in the group grid.</param>
        /// <param name="sourceRect">Optional source rectangle if the icon is part of a spritesheet.</param>
        /// <param name="isVisible">Optional callback to decide whether the item should currently be visible.</param>
        /// <param name="getBadgeCount">Optional callback to draw a badge count on the item icon.</param>
        /// <returns>True if registration succeeded; otherwise false.</returns>
        bool RegisterPhoneAppGroupItem(
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
            Func<int>? getBadgeCount = null
        );

        /// <summary>
        /// Unregisters a previously registered app group item.
        /// </summary>
        /// <param name="ownerModId">The unique ID of the mod that owns this app group.</param>
        /// <param name="groupId">The group ID that contains this item.</param>
        /// <param name="itemId">The item ID used during registration.</param>
        /// <returns>True if an item was removed; otherwise false.</returns>
        bool UnregisterPhoneAppGroupItem(string ownerModId, string groupId, string itemId);





        /// ======================================
        /// API to control smartphone screen navigation.
        /// ======================================

        /// <summary>
        /// Opens the smartphone home (landing) screen for the current player.
        /// If the phone is closed, it is opened first.
        /// </summary>
        /// <returns>True if the home screen was opened; otherwise false.</returns>
        bool OpenPhoneHomeScreen();

        /// <summary>
        /// Opens a registered app-group screen for the current player.
        /// If the phone is closed, it is opened first.
        /// </summary>
        /// <param name="ownerModId">The unique ID of the mod that owns this app group.</param>
        /// <param name="groupId">The app-group ID that was used during registration.</param>
        /// <returns>True if the app-group screen was opened; otherwise false.</returns>
        bool OpenPhoneAppGroup(string ownerModId, string groupId);





        /// ======================================
        /// API to register custom quick actions in App Messenger chat menu.
        /// ======================================

        /// <summary>
        /// Registers a custom quick-action icon in the App Messenger chat quick-action menu (opened by the ^ button).
        /// The callback receives the currently selected NPC internal name.
        /// </summary>
        /// <param name="ownerModId">The unique ID of the mod that owns this quick action.</param>
        /// <param name="actionId">A unique quick-action ID within the owner mod.</param>
        /// <param name="iconTexture">Texture used as the quick-action icon.</param>
        /// <param name="onClick">Callback invoked when the quick-action icon is clicked. Receives selected NPC name.</param>
        /// <param name="closePhoneOnLaunch">Whether the phone menu should close before invoking <paramref name="onClick"/>.</param>
        /// <param name="sortOrder">Lower values are shown earlier in the quick-action stack.</param>
        /// <param name="sourceRect">Optional source rectangle if the icon is part of a spritesheet.</param>
        /// <param name="npcNames">Optional allowlist of NPC internal names. If provided, only these NPC names will show this quick action (e.g. "Abigail", "Lewis").</param>
        /// <returns>True if registration succeeded; otherwise false.</returns>
        bool RegisterChatQuickActionButton(
            string ownerModId,
            string actionId,
            Texture2D iconTexture,
            Action<string> onClick,
            bool closePhoneOnLaunch = false,
            int sortOrder = 0,
            Rectangle? sourceRect = null,
            List<string>? npcNames = null
        );

        /// <summary>
        /// Unregisters a previously registered App Messenger chat quick-action icon.
        /// </summary>
        /// <param name="ownerModId">The unique ID of the mod that owns this quick action.</param>
        /// <param name="actionId">The quick-action ID that was used during registration.</param>
        /// <returns>True if a quick-action icon was removed; otherwise false.</returns>
        bool UnregisterChatQuickActionButton(string ownerModId, string actionId);





        /// ======================================
        /// API for interacting with the smartphone messenger app
        /// ======================================

        /// <summary>
        /// Gets a list of NPCs that have appear in the messenger app for a specific player.
        /// </summary>
        /// <param name="playerId">(optional) The target player's UniqueMultiplayerID as string. If provided and not a valid online player ID, returns an empty list.</param>
        /// <returns>A list of NPC names.</returns>
        List<string> GetPhoneNpcList(string playerId = "");

        /// <summary>
        /// Sends a message from an NPC to the player. This method is used to simulate receiving messages on the player's smartphone from NPCs in the game. Nothing will happen if the specified NPC is not in the messenger app list.
        /// </summary>
        /// <param name="npcName">The name of the NPC sending the message (case-sensitive).</param>
        /// <param name="message">The content of the message being sent.</param>
        /// <param name="playerId">(optional) The target player's UniqueMultiplayerID as string. If null/empty/invalid, this is broadcast to all online players.</param>
        void SendSmartphoneMessageFromNPC(string npcName, string message, string playerId = "");

        /// <summary>
        /// Sends a message from the player to an NPC. This method is used to simulate sending messages from the player's smartphone to NPCs in the game. Nothing will happen if the specified NPC is not in the messenger app list.
        /// </summary>
        /// <param name="npcName">The name of the NPC receiving the message (case-sensitive).</param>
        /// <param name="message">The content of the message being sent.</param>
        /// <param name="playerId">(optional) The target player's UniqueMultiplayerID as string. If null/empty/invalid, this is broadcast to all online players.</param>
        void SendSmartphoneMessageFromPlayer(string npcName, string message, string playerId = "");

        /// <summary>
        /// Sends a notification to the player's smartphone.
        /// </summary>
        /// <param name="message">The content of the notification (shown in the phone notification message).</param>
        /// <param name="notificationName">(optional) The name of the notification (shown on ingame notification HUD).</param>
        /// <param name="playerId">(optional) The target player's UniqueMultiplayerID as string. If null/empty/invalid, this is broadcast to all online players.</param>
        void SendSmartphoneNotification(string message, string notificationName = "", string playerId = "");





        /// ======================================
        /// API for interacting with the StardewConnect social media app
        /// ======================================

        /// <summary>
        /// Creates a StardewConnect post authored by an NPC.
        /// </summary>
        /// <param name="npcName">NPC name.</param>
        /// <param name="postText">Post content. Can be empty if an image is attached.</param>
        /// <param name="attachedImageFile">Optional image file name from the NPC smartphone photo folder.</param>
        /// <returns>The new post ID if created; otherwise null.</returns>
        string? CreateStardewConnectPostFromNpc(string npcName, string postText, string attachedImageFile = "");

        /// <summary>
        /// Adds an NPC-authored comment to a StardewConnect post.
        /// </summary>
        /// <param name="postId">Target post ID.</param>
        /// <param name="npcName">NPC name.</param>
        /// <param name="commentText">Comment content.</param>
        /// <returns>True if comment was added.</returns>
        bool AddStardewConnectCommentFromNpc(string postId, string npcName, string commentText);

        /// <summary>
        /// Sets whether an NPC likes a StardewConnect post.
        /// </summary>
        /// <param name="postId">Target post ID.</param>
        /// <param name="npcName">NPC name.</param>
        /// <param name="liked">True to like, false to unlike.</param>
        /// <returns>True if operation succeeded.</returns>
        bool SetStardewConnectPostLikedFromNpc(string postId, string npcName, bool liked);





        /// ======================================
        /// API for Unlimited Event Expansion only
        /// ======================================

        /// <summary>
        /// Registers or updates an event type that can be suggested by AI chat and scheduled through Smartphone.
        /// The <paramref name="eventType"/> value is used as the tool enum value, so keep it stable.
        /// </summary>
        /// <param name="ownerModId">The unique ID of the mod that owns this event registration.</param>
        /// <param name="eventType">Unique event key shown to the AI tool (for example: "Birthday").</param>
        /// <param name="triggerEvent">Callback invoked when Smartphone triggers this event for an NPC name.</param>
        /// <param name="minimumHeartLevel">Minimum heart level required before this event is exposed to AI tools.</param>
        /// <param name="toolDescription">Optional extra context appended to the Schedule_Event tool description.</param>
        /// <returns>True if registration succeeded; otherwise false.</returns>
        bool RegisterUnlimitedEvent(
            string ownerModId,
            string eventType,
            Action<string> triggerEvent,
            int minimumHeartLevel = 0,
            string toolDescription = ""
        );

        /// <summary>
        /// Unregisters a previously registered event type.
        /// </summary>
        /// <param name="ownerModId">The unique ID of the mod that owns this event registration.</param>
        /// <param name="eventType">The event key that was used during registration.</param>
        /// <returns>True if an event type was removed; otherwise false.</returns>
        bool UnregisterUnlimitedEvent(string ownerModId, string eventType);

    }
}
