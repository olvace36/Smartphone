# Smartphone Framework API Guide

This guide is intended for modders who want to register custom apps or widgets on the Smartphone home screen, create custom on-screen screens (in either Portrait or Landscape mode), send notifications, or integrate with the Contacts and Photo apps.

---

## 1. Getting the API

Retrieve the `ISmartPhoneApi` interface from SMAPI's Mod Registry, typically within your mod's `GameLaunched` event:

```csharp
private ISmartPhoneApi? smartphoneApi;

private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
{
    this.smartphoneApi = this.Helper.ModRegistry.GetApi<ISmartPhoneApi>("d5a1lamdtd.Smartphone");
    if (this.smartphoneApi == null)
    {
        this.Monitor.Log("Smartphone API is unavailable.", LogLevel.Warn);
        return;
    }
}
```

---

## 2. App & Widget Registration

You can register a custom app icon on the Smartphone home screen. A single API is used for registering both standard icons and widgets of different sizes.

### Registering an App
Use `RegisterPhoneApp` to register your app. To provide the app icon, pass a `Dictionary<string, Texture2D>` via `themedIconTextures` containing at least a `"default"` key.

```csharp
bool registered = smartphoneApi.RegisterPhoneApp(
    ownerModId: this.ModManifest.UniqueID,
    appId: "my_custom_app",
    displayName: "My App",
    onClick: () => OpenMyCustomAppScreen(),
    closePhoneOnLaunch: true,
    sourceRect: null,
    getBadgeCount: () => GetUnreadCount(),
    supportedSizes: new[] { AppSize.Size1x1 },
    onDrawWidget: null,
    themedIconTextures: new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase)
    {
        { "default", myIconTexture }
    }
);
```

#### Supported App Sizes (Widgets)
The `AppSize` enum defines the footprint of your app icon/widget on the home screen:
```csharp
public enum AppSize
{
    Size1x1, // Standard app icon
    Size2x1,
    Size2x2,
    Size2x3,
    Size2x4,
    Size4x2,
    Size4x3,
    Size4x4
}
```
If you provide sizes larger than `Size1x1` in `supportedSizes`, the user can resize your app icon into a widget on their home screen. You must then provide the `onDrawWidget` callback to render the widget content:
```csharp
void DrawWidget(SpriteBatch b, Rectangle widgetBounds, AppSize size)
{
    // Draw custom widget UI within widgetBounds
}
```

### Unregistering an App
```csharp
smartphoneApi.UnregisterPhoneApp(this.ModManifest.UniqueID, "my_custom_app");
```

### Querying Textures
If your app needs to retrieve standard app icons or icons registered by other apps:
- **Custom Apps**: `Texture2D? GetAppIconTexture(string appId)`
- **Built-in Apps**: `Texture2D? GetAppTexture(AppIconType appIconType)`
  * `AppIconType` values: `Notification`, `AppStore`, `Camera`, `Photo`, `Setting`, `Calendar`.

---

## 3. Creating a Custom On-Screen App UI

If you want your app to render *inside* the phone screen, your `onClick` callback should open a custom class derived from Stardew Valley's `IClickableMenu`. 

The transition will feel seamless if your menu initializes at the phone's current position and handles dragging/scaling events correctly.

### Setup and Layout (Portrait Mode)
In your custom menu's constructor, query the current phone dimensions and offsets from the API:

```csharp
public class MyCustomAppScreen : IClickableMenu
{
    private readonly ISmartPhoneApi smartphoneApi;
    private readonly Action onBack;

    private int phoneFrameWidth;
    private int phoneFrameHeight;
    private int phoneContentOffsetX;
    private int phoneContentOffsetY;
    private float phoneUiScale;

    private Texture2D? phoneFrameTexture;
    private Texture2D? phoneBackgroundTexture;

    private int contentWidth;
    private int contentHeight;

    public MyCustomAppScreen(ISmartPhoneApi api, Action onBack) : base()
    {
        this.smartphoneApi = api;
        this.onBack = onBack;

        // Position the menu matching the phone
        var (px, py) = api.GetPhonePosition();
        this.xPositionOnScreen = px;
        this.yPositionOnScreen = py;

        // Get layout dimensions
        this.phoneFrameWidth = api.GetPhoneFrameWidth();
        this.phoneFrameHeight = api.GetPhoneFrameHeight();
        var (offX, offY) = api.GetPhoneContentOffset();
        this.phoneContentOffsetX = offX;
        this.phoneContentOffsetY = offY;
        this.phoneUiScale = api.GetPhoneUiScale();

        this.width = this.phoneFrameWidth;
        this.height = this.phoneFrameHeight;

        this.phoneFrameTexture = api.GetPhoneFrameTexture();
        this.phoneBackgroundTexture = api.GetPhoneBackgroundTexture();

        // Calculate the inner screen content bounds
        if (this.phoneBackgroundTexture != null)
        {
            this.contentWidth = (int)Math.Round(this.phoneBackgroundTexture.Width * this.phoneUiScale);
            this.contentHeight = (int)Math.Round(this.phoneBackgroundTexture.Height * this.phoneUiScale);
        }
        else
        {
            this.contentWidth = Math.Max(1, this.phoneFrameWidth - (this.phoneContentOffsetX * 2));
            this.contentHeight = Math.Max(1, this.phoneFrameHeight - this.phoneContentOffsetY - (int)(80 * this.phoneUiScale));
        }
    }
}
```

### Rendering Content Inside the Screen Bezel
To ensure your app's content does not bleed outside the phone's bezel screen area, render your screen content using a scissor test with the screen's content boundaries:

```csharp
private Rectangle GetContentBounds()
{
    return new Rectangle(
        this.xPositionOnScreen + this.phoneContentOffsetX,
        this.yPositionOnScreen + this.phoneContentOffsetY,
        this.contentWidth,
        this.contentHeight
    );
}

public override void draw(SpriteBatch b)
{
    // 1. Draw a dark overlay to dim the rest of the game screen
    b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * 0.6f);

    Rectangle contentRect = this.GetContentBounds();

    // 2. Draw the wallpaper/background
    if (this.phoneBackgroundTexture != null)
    {
        b.Draw(this.phoneBackgroundTexture, contentRect, Color.White);
    }
    else
    {
        b.Draw(Game1.staminaRect, contentRect, new Color(30, 30, 30));
    }

    // 3. Render custom app content using Scissor Masking
    b.End();
    b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState { ScissorTestEnable = true });
    
    Rectangle previousScissor = Game1.graphics.GraphicsDevice.ScissorRectangle;
    Game1.graphics.GraphicsDevice.ScissorRectangle = contentRect;

    // ---> DRAW YOUR APP CONTENT HERE <---

    b.End();
    Game1.graphics.GraphicsDevice.ScissorRectangle = previousScissor;
    b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

    // 4. Draw the phone frame bezel on top of the content
    if (this.phoneFrameTexture != null)
    {
        b.Draw(this.phoneFrameTexture, new Rectangle(this.xPositionOnScreen, this.yPositionOnScreen, this.phoneFrameWidth, this.phoneFrameHeight), Color.White);
    }

    // 5. Draw the scale adjustment buttons (+ and -) on the phone frame
    this.smartphoneApi.DrawPhoneSizeButtons(b, this.xPositionOnScreen, this.yPositionOnScreen);

    // 6. Draw standard cursor
    drawMouse(b);
}
```

### Bottom Navigation & Interaction
Route clicks on the bezel to the standard navigation buttons and size adjustments:

```csharp
public override void receiveLeftClick(int x, int y, bool playSound = true)
{
    // Handle Home/Back bottom navigation
    if (this.smartphoneApi.HandlePhoneAppBottomNavClick(x, y, this.xPositionOnScreen, this.yPositionOnScreen, onBack: this.onBack))
    {
        return;
    }

    // Handle frame UI scale clicks (+ and -)
    if (this.smartphoneApi.HandlePhoneSizeButtonsClick(x, y, this.xPositionOnScreen, this.yPositionOnScreen))
    {
        return;
    }
}
```

### Scaling and Resizing Keyboard Shortcuts
Ensure you listen to the configured decrease/increase key triggers inside `receiveKeyPress`:

```csharp
public override void receiveKeyPress(Microsoft.Xna.Framework.Input.Keys key)
{
    if (key == Microsoft.Xna.Framework.Input.Keys.Escape)
    {
        this.onBack?.Invoke();
        return;
    }

    string keyStr = key.ToString();
    if (keyStr == this.smartphoneApi.GetDecreaseSizeKey())
    {
        this.smartphoneApi.AdjustPhoneSize(-0.1f);
        return;
    }
    if (keyStr == this.smartphoneApi.GetIncreaseSizeKey())
    {
        this.smartphoneApi.AdjustPhoneSize(0.1f);
        return;
    }

    base.receiveKeyPress(key);
}
```

### Preserving Phone Location During Dragging
In order to allow users to drag the phone anywhere on the screen, check for drags on the bezel area in `leftClickHeld` and update the global coordinate cache in `update`:

```csharp
private bool isDragging;
private int dragOffsetX;
private int dragOffsetY;

public override void leftClickHeld(int x, int y)
{
    base.leftClickHeld(x, y);

    if (!this.isDragging)
    {
        Rectangle frameBounds = new Rectangle(this.xPositionOnScreen, this.yPositionOnScreen, this.phoneFrameWidth, this.phoneFrameHeight);
        Rectangle contentBounds = this.GetContentBounds();

        // Start dragging if clicking inside phone bezel but outside screen content area
        if (frameBounds.Contains(x, y) && !contentBounds.Contains(x, y))
        {
            this.isDragging = true;
            this.dragOffsetX = x - this.xPositionOnScreen;
            this.dragOffsetY = y - this.yPositionOnScreen;
        }
    }
}

public override void releaseLeftClick(int x, int y)
{
    base.releaseLeftClick(x, y);
    this.isDragging = false;
}

public override void update(GameTime time)
{
    // Update dimensions if scaling changed
    float currentScale = this.smartphoneApi.GetPhoneUiScale();
    if (currentScale != this.phoneUiScale)
    {
        this.phoneUiScale = currentScale;
        this.phoneFrameWidth = this.smartphoneApi.GetPhoneFrameWidth();
        this.phoneFrameHeight = this.smartphoneApi.GetPhoneFrameHeight();
        var (offX, offY) = this.smartphoneApi.GetPhoneContentOffset();
        this.phoneContentOffsetX = offX;
        this.phoneContentOffsetY = offY;
        
        this.width = this.phoneFrameWidth;
        this.height = this.phoneFrameHeight;
        
        // Recalculate your custom UI layouts here
    }

    base.update(time);

    // Apply drag coordinate adjustments
    if (this.isDragging)
    {
        this.xPositionOnScreen = Game1.getMouseX() - this.dragOffsetX;
        this.yPositionOnScreen = Game1.getMouseY() - this.dragOffsetY;
        
        // Sync position globally
        this.smartphoneApi.SetPhonePosition(this.xPositionOnScreen, this.yPositionOnScreen);
    }
}
```

---

## 4. Landscape Mode Implementation

If your app needs to run in Landscape mode, the phone menu must be drawn rotated by `-90` degrees (`-MathHelper.PiOver2`). 

### Dimension Swapping
In landscape orientation, the menu's visual width matches the portrait height, and the visual height matches the portrait width:

```csharp
// Visual dimensions are swapped
this.width = this.phoneFrameHeight; 
this.height = this.phoneFrameWidth;

// Center-rotate the on-screen menu coordinates
this.xPositionOnScreen = px + (this.phoneFrameWidth - this.phoneFrameHeight) / 2;
this.yPositionOnScreen = py + (this.phoneFrameHeight - this.phoneFrameWidth) / 2;
```

### Landscape Content Bounds
The active content area bounds are calculated as follows in landscape mode:

```csharp
int landscapeContentX = this.xPositionOnScreen + this.phoneContentOffsetY;
int landscapeContentY = this.yPositionOnScreen + this.phoneFrameWidth - this.phoneContentOffsetX - this.contentWidth;
int LWidth = this.contentHeight;
int LHeight = this.contentWidth;

Rectangle landscapeContentBounds = new Rectangle(landscapeContentX, landscapeContentY, LWidth, LHeight);
```

### Drawing Rotated Textures
Use the rotation parameter in `SpriteBatch.Draw` with `-MathHelper.PiOver2` to draw the phone background and frame rotated:

```csharp
// Draw background rotated
if (this.phoneBackgroundTexture != null)
{
    float bgScaleX = (float)this.contentWidth / this.phoneBackgroundTexture.Width;
    float bgScaleY = (float)this.contentHeight / this.phoneBackgroundTexture.Height;
    b.Draw(
        this.phoneBackgroundTexture,
        new Vector2(landscapeContentX, landscapeContentY + this.contentWidth),
        null,
        Color.White,
        -MathHelper.PiOver2,
        Vector2.Zero,
        new Vector2(bgScaleX, bgScaleY),
        SpriteEffects.None,
        0f
    );
}

// Draw phone frame rotated
if (this.phoneFrameTexture != null)
{
    float scaleX = (float)this.phoneFrameWidth / this.phoneFrameTexture.Width;
    float scaleY = (float)this.phoneFrameHeight / this.phoneFrameTexture.Height;
    b.Draw(
        this.phoneFrameTexture,
        new Vector2(this.xPositionOnScreen, this.yPositionOnScreen + this.phoneFrameWidth),
        null,
        Color.White,
        -MathHelper.PiOver2,
        Vector2.Zero,
        new Vector2(scaleX, scaleY),
        SpriteEffects.None,
        0f
    );
}

// Render size control buttons rotated
this.smartphoneApi.DrawPhoneSizeButtons(b, this.xPositionOnScreen, this.yPositionOnScreen, landscape: true);
```

### Input Coordinate Remapping
For interactions like bottom navigation, home button, and size button clicks, you must map the mouse coordinate space `(x, y)` back to the portrait coordinate offsets `(px_click, py_click)` before routing inputs to the API:

```csharp
public override void receiveLeftClick(int x, int y, bool playSound = true)
{
    // Retrieve portrait coordinate anchors
    int px = this.xPositionOnScreen - (this.phoneFrameWidth - this.phoneFrameHeight) / 2;
    int py = this.yPositionOnScreen - (this.phoneFrameHeight - this.phoneFrameWidth) / 2;

    // Map landscape (x, y) mouse click back to portrait bounds
    int px_click = px + (this.yPositionOnScreen + this.phoneFrameWidth - y);
    int py_click = py + (x - this.xPositionOnScreen);

    // Route inputs using mapped coordinates
    if (this.smartphoneApi.HandlePhoneAppBottomNavClick(px_click, py_click, px, py, onBack: this.onBack))
    {
        return;
    }

    if (this.smartphoneApi.HandlePhoneSizeButtonsClick(px_click, py_click, px, py))
    {
        return;
    }
}
```

### Syncing drag coordinates globally in landscape:
If your landscape window gets dragged, you must compute the equivalent portrait coordinate values when calling `SetPhonePosition`:
```csharp
if (this.isDragging)
{
    this.xPositionOnScreen = Game1.getMouseX() - this.dragOffsetX;
    this.yPositionOnScreen = Game1.getMouseY() - this.dragOffsetY;
    
    // Map landscape top-left back to portrait coordinates
    int px = this.xPositionOnScreen - (this.phoneFrameWidth - this.phoneFrameHeight) / 2;
    int py = this.yPositionOnScreen - (this.phoneFrameHeight - this.phoneFrameWidth) / 2;
    this.smartphoneApi.SetPhonePosition(px, py);
}
```

---

## 5. Contacts Card API

You can register custom content sections (cards) to appear within the Smartphone Contacts information screen, providing up to 4 clickable interaction buttons.

### 1. Implement `IContactActionCardButton`
Create a helper button class satisfying the interface:
```csharp
public class ContactActionCardButton : IContactActionCardButton
{
    public string Text { get; set; } = string.Empty;
    public Color BackgroundColor { get; set; } = Color.White;
    public Color TextColor { get; set; } = Color.Black;
    public Action<string>? OnClick { get; set; } // npcName is passed as parameter
}
```

### 2. Register the Card
Register the card using `RegisterContactActionCard`. You can optionally restrict the card's visibility to specific NPCs by passing a list of their internal names:

```csharp
var playButton = new ContactActionCardButton
{
    Text = "Play Game",
    BackgroundColor = Color.CadetBlue,
    TextColor = Color.White,
    OnClick = (npcName) => StartGameWithNPC(npcName)
};

bool registered = smartphoneApi.RegisterContactActionCard(
    modId: this.ModManifest.UniqueID,
    cardTitle: "Arcade Play",
    buttons: new List<IContactActionCardButton> { playButton },
    npcNames: new List<string> { "Abigail", "Sebastian" } // Optional filter
);
```

### 3. Contact Updates Event
Listen to `ContactableNpcsChanged` to be notified whenever the list of contactable characters changes (e.g. when friendship levels rise or a character is met):
```csharp
smartphoneApi.ContactableNpcsChanged += OnContactableNpcsChanged;

private void OnContactableNpcsChanged(List<string> npcs)
{
    // npcs lists all current contactable NPC internal names
}
```

---

## 6. Notifications API

Send localized HUD alerts and phone messages to players:

```csharp
// Sends a notification message.
// - message: content shown inside the phone notification list
// - notificationName: title displayed on the in-game HUD alert
// - playerId: optional Multiplayer ID string (leave blank to broadcast to all players)
smartphoneApi.SendSmartphoneNotification(
    message: "Abigail wants to play a game with you!",
    notificationName: "Smartphone Alert",
    playerId: Game1.player.UniqueMultiplayerID.ToString()
);
```

---

## 7. Photo App API

You can integrate your mod with the Photo app to capture in-game screenshots programmatically or let players select photos.

### Capturing a Photo Programmatically
```csharp
// Captures a photo in the game world and returns the path to the saved image
string savedPhotoPath = smartphoneApi.CaptureNpcPhoto(
    targetLocation: Game1.currentLocation,
    captureCenter: Game1.player.getTileLocation(),
    npc: null,
    landscape: false,
    square: false
);
```

### Photo Selector Menu
Open the photo selection screen to retrieve photo textures and metadata JSON string representations:
```csharp
smartphoneApi.RetrievePhotos(
    limit: 3,
    getTexture: true,
    getMetadata: true,
    onComplete: (jsonResult) =>
    {
        // Parse the JSON string into List<SelectedPhotoResult>
    },
    squareOnly: false
);
```
`SelectedPhotoResult` class properties:
- `string AbsolutePath`
- `string FileName`
- `string Tag`
- `string Location`
- `string Timestamp`
- `byte[]? TextureData`

### Accessing Photo Information Directly
```csharp
Texture2D photoTex = smartphoneApi.GetPlayerPhotoTexture("photo_filename.png");
string metadataJson = smartphoneApi.GetPlayerPhotoMetadata("photo_filename.png");
```