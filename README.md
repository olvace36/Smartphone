*** IMPORTANT ***
*** IMPORTANT ***
*** IMPORTANT ***

When updating to newer version of the mod, bring the folder ** userdata ** to the new mod folder
This folder contain all data you created (photo, chat history, setting, ...)


*** EXTERNAL APP API (FOR OTHER MODDERS) ***

This mod now exposes a phone app registration API.
Other mods can register their own app icon on the Smartphone home screen and provide a click callback.

1) Get the API from Smartphone in your mod (usually in GameLaunched):

	ISmartPhoneApi smartphoneApi = Helper.ModRegistry.GetApi<ISmartPhoneApi>("d5a1lamdtd.Smartphone");

2) Load your icon texture (84x84 is recommended):

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


*** DYNAMIC UNLIMITED EVENT API ***

UEE (or any mod) can register event types that Smartphone AI is allowed to schedule.
This removes the need to hardcode event names inside Smartphone every time a new event is added.

Example registration from another mod:

	smartphoneApi.RegisterUnlimitedEvent(
		 ownerModId: this.ModManifest.UniqueID,
		 eventType: "Stargazing",
		 displayName: "Stargazing",
		 triggerEvent: npcName => myUeeApi.TriggerStargazingEvent(npcName),
		 minimumHeartLevel: 6,
		 toolDescription: "Use this only after both sides agree to meet tonight for stargazing."
	);

Unregister when needed:

	smartphoneApi.UnregisterUnlimitedEvent(this.ModManifest.UniqueID, "Stargazing");

Method summary:
- RegisterUnlimitedEvent(ownerModId, eventType, displayName, triggerEvent, minimumHeartLevel, toolDescription)
- UnregisterUnlimitedEvent(ownerModId, eventType)

Notes:
- eventType becomes the enum value used by the Schedule_Event tool.
- minimumHeartLevel controls when the event appears in AI tools.
- triggerEvent is invoked by Smartphone when the scheduled time is reached.