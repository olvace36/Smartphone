*** IMPORTANT ***
*** IMPORTANT ***
*** IMPORTANT ***

When updating to a newer version of the mod, copy the folder ** userdata ** to the new mod folder.
This folder contains all data you created (photos, chat history, settings, ...).


*** ADVANCED AI CUSTOM PROVIDER (CONFIG.JSON ONLY) ***

Power users can route Smartphone AI calls to a custom service (local LLM or another provider) by editing config.json.

When CustomApiEndpoint is configured, Smartphone uses custom template mode for:
- NPC chat responses
- Daily conversation summaries
- StardewSocial post text generation
- StardewSocial comment generation

Network policy:
- Remote endpoints must use HTTPS.
- HTTP is only allowed for localhost or loopback hosts (localhost, 127.0.0.1, ::1).

Supported payload placeholders (plain TOKEN and {{TOKEN}} are both supported):
- INPUT_HERE (combined SYSTEM + USER text)
- SYSTEM_INPUT_HERE
- USER_INPUT_HERE
- SYSTEM_MESSAGE_HERE
- USER_MESSAGE_HERE
- MODEL_HERE

Example config.json values:

{
	"CustomApiEndpoint": "http://localhost:11434/v1/chat/completions",
	"CustomApiKey": "",
	"CustomApiKeyHeader": "Authorization",
	"CustomApiKeyPrefix": "Bearer",
	"CustomApiPayloadTemplate": "{\"model\":\"MODEL_HERE\",\"messages\":[{\"role\":\"system\",\"content\":\"SYSTEM_INPUT_HERE\"},{\"role\":\"user\",\"content\":\"USER_INPUT_HERE\"}]}",
	"CustomApiResponseTextPath": "choices[0].message.content",
	"CustomApiTimeoutSeconds": 45
}

Notes:
- CustomApiKey is optional.
- If your provider returns text in a different field, set CustomApiResponseTextPath (for example output_text, result.text, or candidates[0].content.parts[0].text).
- In custom template mode, function calling is disabled for chat. The mod use function call for Unlimited Event Expansion only, so you can fall back to use the button instead.


*** EXTERNAL APP API (FOR OTHER MODDERS) ***

This mod now exposes a phone app registration API.
Other mods can register their own app icon on the Smartphone home screen and provide a click callback.
Check out my another mod call Smartphone - Game App, which is a very basic mod that give you an example of how to register app, app group, and text quick action button

1) Get the API from Smartphone in your mod (usually in GameLaunched):

	ISmartPhoneApi smartphoneApi = Helper.ModRegistry.GetApi<ISmartPhoneApi>("d5a1lamdtd.Smartphone");

2) Load your icon texture (any size works; square 256x256 is a good default for quality):

	Texture2D icon = Helper.ModContent.Load<Texture2D>("assets/market_app.png");

3) Register your app:

	smartphoneApi.RegisterPhoneApp(
		 ownerModId: this.ModManifest.UniqueID,
		 appId: "markettown-main",
		 displayName: "Market",
		 iconTexture: icon,
		 onClick: () => OpenMarketTownMenu(),
		 closePhoneOnLaunch: true,
		 sortOrder: 100,
		 sourceRect: null,
		 isVisible: () => Context.IsWorldReady,
		 getBadgeCount: () => 0
	);

4) Optional: unregister if needed:

	smartphoneApi.UnregisterPhoneApp(this.ModManifest.UniqueID, "markettown-main");


API method summary:
- RegisterPhoneApp(ownerModId, appId, displayName, iconTexture, onClick, closePhoneOnLaunch, sortOrder, sourceRect, isVisible, getBadgeCount)
- UnregisterPhoneApp(ownerModId, appId)

Notes:
- ownerModId + appId is the unique key. Registering the same key again updates the existing app.
- If sourceRect is provided, width and height must both be > 0.


*** APP GROUP API (MAX 9 ITEMS) ***

You can also create a grouped app (see the example AppGame mod).
Each group can have up to 9 items.

1) Register the group app icon:

	smartphoneApi.RegisterPhoneAppGroup(
		 ownerModId: this.ModManifest.UniqueID,
		 groupId: "market-tools",
		 displayName: "Market",
		 iconTexture: groupIcon,
		 sortOrder: 120,
		 sourceRect: null,
		 isVisible: () => Context.IsWorldReady,
		 getBadgeCount: () => 0
	);

2) Register items in that group (max 9):

	smartphoneApi.RegisterPhoneAppGroupItem(
		 ownerModId: this.ModManifest.UniqueID,
		 groupId: "market-tools",
		 itemId: "market-log",
		 displayName: "Log",
		 iconTexture: logIcon,
		 onClick: () => OpenMarketTownMenu(),
		 closePhoneOnLaunch: true,
		 sortOrder: 0,
		 sourceRect: null,
		 isVisible: () => Context.IsWorldReady,
		 getBadgeCount: () => 0
	);

3) Optional unregister:

	smartphoneApi.UnregisterPhoneAppGroupItem(this.ModManifest.UniqueID, "market-tools", "market-log");
	smartphoneApi.UnregisterPhoneAppGroup(this.ModManifest.UniqueID, "market-tools");


Additional method summary:
- RegisterPhoneAppGroup(ownerModId, groupId, displayName, iconTexture, sortOrder, sourceRect, isVisible, getBadgeCount)
- UnregisterPhoneAppGroup(ownerModId, groupId)
- RegisterPhoneAppGroupItem(ownerModId, groupId, itemId, displayName, iconTexture, onClick, closePhoneOnLaunch, sortOrder, sourceRect, isVisible, getBadgeCount)
- UnregisterPhoneAppGroupItem(ownerModId, groupId, itemId)

Notes:
- RegisterPhoneAppGroup must be called before RegisterPhoneAppGroupItem.
- ownerModId + groupId + itemId is the unique key. Registering the same key again updates that item.
- A group can contain at most 9 items. Adding a new 10th item fails.
- If sourceRect is provided, width and height must both be > 0.


*** PHONE NAVIGATION API ***

You can programmatically open Smartphone home or jump directly to a registered app group.
This is useful for custom app UIs that need a Back button to return to Smartphone.

Example usage:

	bool openedHome = smartphoneApi.OpenPhoneHomeScreen();
	bool openedGroup = smartphoneApi.OpenPhoneAppGroup(this.ModManifest.UniqueID, "market-tools");

Method summary:
- OpenPhoneHomeScreen()
- OpenPhoneAppGroup(ownerModId, groupId)

Notes:
- OpenPhoneAppGroup returns false if the group is not registered or currently not visible.
- Both methods return false when the game world is not ready.


*** TEXT CHAT QUICK ACTION BUTTON API ***

You can register custom quick-action buttons in the Text app chat menu (the menu opened by the ^ button).

Layout behavior:
- Built-in actions stay at the bottom of the quick-action column:
  - Attach photo
  - Schedule event
  - AI credit info (only shown when shared AI mode is active and ShowAiCredit is enabled)
- Registered actions always appear above built-in actions.
- Registered actions are sorted by sortOrder (lower value appears closer to the built-in actions).

Example registration:

	smartphoneApi.RegisterChatQuickActionButton(
		 ownerModId: this.ModManifest.UniqueID,
		 actionId: "market-gift-hint",
		 iconTexture: quickActionIcon,
		 onClick: npcName => OpenGiftHintMenuForNpc(npcName),
		 closePhoneOnLaunch: false,
		 sortOrder: 0,
		 sourceRect: null,
		 npcNames: new List<string> { "Abigail", "Lewis" }
	);

Optional unregister:

	smartphoneApi.UnregisterChatQuickActionButton(this.ModManifest.UniqueID, "market-gift-hint");

Method summary:
- RegisterChatQuickActionButton(ownerModId, actionId, iconTexture, onClick, closePhoneOnLaunch, sortOrder, sourceRect, npcNames)
- UnregisterChatQuickActionButton(ownerModId, actionId)

Notes:
- ownerModId + actionId is the unique key. Registering the same key again updates the action.
- npcNames is optional. If provided, only listed NPC names can see the action.
- Custom actions are only shown in NPC conversations (not player-to-player conversations).


*** MESSENGER API ***

You can interact with the Smartphone messenger app from another mod.

Example usage:

	// Get list for local player
	List<string> localNpcList = smartphoneApi.GetPhoneNpcList();

	// Optional target player (UniqueMultiplayerID as string)
	string targetPlayerId = Game1.player.UniqueMultiplayerID.ToString();

	smartphoneApi.SendSmartphoneMessageFromNPC("Abigail", "Meet me at the beach tonight.", targetPlayerId);
	smartphoneApi.SendSmartphoneMessageFromPlayer("Abigail", "I will be there!", targetPlayerId);
	smartphoneApi.SendSmartphoneNotification("New chat message", "Smartphone", targetPlayerId);

Method summary:
- GetPhoneNpcList(playerId)
- SendSmartphoneMessageFromNPC(npcName, message, playerId)
- SendSmartphoneMessageFromPlayer(npcName, message, playerId)
- SendSmartphoneNotification(message, notificationName, playerId)

Notes:
- playerId is optional and should be UniqueMultiplayerID as string.
- For SendSmartphoneMessageFromNPC / SendSmartphoneMessageFromPlayer / SendSmartphoneNotification:
  - Empty or invalid playerId broadcasts to all online players.
- For GetPhoneNpcList:
  - Empty playerId returns local player's list.
  - Non-empty invalid playerId returns an empty list.
- npcName must be a valid in-game NPC internal name.


*** STARDEWCONNECT SOCIAL API ***

You can create social posts/comments/likes authored by NPCs.

Example usage:

	string? postId = smartphoneApi.CreateStardewConnectPostFromNpc(
		npcName: "Abigail",
		postText: "Beautiful sunset at the mountain lake!",
		attachedImageFile: "abigail_sunset.png"
	);

	if (!string.IsNullOrWhiteSpace(postId))
	{
		smartphoneApi.AddStardewConnectCommentFromNpc(postId, "Leah", "That looks amazing!");
		smartphoneApi.SetStardewConnectPostLikedFromNpc(postId, "Sebastian", true);
	}

Method summary:
- CreateStardewConnectPostFromNpc(npcName, postText, attachedImageFile)
- AddStardewConnectCommentFromNpc(postId, npcName, commentText)
- SetStardewConnectPostLikedFromNpc(postId, npcName, liked)

Notes:
- attachedImageFile should be a file name from the NPC smartphone photo folder.
- CreateStardewConnectPostFromNpc returns the new post ID, or null if creation failed.
- AddStardewConnectCommentFromNpc / SetStardewConnectPostLikedFromNpc return false when the operation cannot be applied.
- In multiplayer, call these from the host/main player for authoritative social state.


*** DYNAMIC UNLIMITED EVENT API ***

UEE (or any mod) can register event types that Smartphone AI is allowed to schedule.
This removes the need to hardcode event names inside Smartphone every time a new event is added.

Example registration from another mod:

	smartphoneApi.RegisterUnlimitedEvent(
		 ownerModId: this.ModManifest.UniqueID,
		 eventType: "Stargazing",
		 triggerEvent: npcName => myUeeApi.TriggerStargazingEvent(npcName),
		 minimumHeartLevel: 6,
		 toolDescription: "Use this only after both sides agree to meet tonight for stargazing."
	);

Unregister when needed:

	smartphoneApi.UnregisterUnlimitedEvent(this.ModManifest.UniqueID, "Stargazing");

Method summary:
- RegisterUnlimitedEvent(ownerModId, eventType, triggerEvent, minimumHeartLevel, toolDescription)
- UnregisterUnlimitedEvent(ownerModId, eventType)

Notes:
- eventType becomes the enum value used by the Schedule_Event tool.
- minimumHeartLevel controls when the event appears in AI tools.
- triggerEvent is invoked by Smartphone when the scheduled time is reached.
- eventType is globally unique. Another mod cannot overwrite an event type owned by a different mod.
- minimumHeartLevel is clamped to 0 or higher.