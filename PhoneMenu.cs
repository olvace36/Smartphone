using System.Diagnostics.Metrics;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Characters;
using StardewValley.GameData.HomeRenovations;
using StardewValley.Menus;


namespace Smartphone
{
    public class PhoneMenu : IClickableMenu
    {
        private bool isDragging = false;
        private int dragOffsetX;
        private int dragOffsetY;

        private static Dictionary<string, List<string>> pendingMessages = new();
        private static Dictionary<string, CancellationTokenSource> replyTimers = new();
        private static TimeSpan delay = TimeSpan.FromSeconds(20);
        Texture2D furnitureTexture = Game1.content.Load<Texture2D>("TileSheets\\furniture");



        private Texture2D texturePhoneBackground = Textures.PhoneBackground;
        private Texture2D texturePhoneCapture = Textures.PhoneEmpty;
        private Texture2D texturePortraitBackground = Textures.Background;

        private Texture2D textureAppCamera = Textures.AppCamera;
        private Texture2D textureAppText = Textures.AppText;
        private Texture2D textureAppPhoto = Textures.AppPhoto;
        private Texture2D textureAppSetting = Textures.AppSetting;
        private Texture2D textureAppGame = Textures.AppGame;
        private Texture2D textureAppNotification = Textures.AppNotification;

        private Texture2D textureGameDarts = Textures.GameDarts;
        private Texture2D textureGameJack = Textures.GameJack;
        private Texture2D textureGameCart = Textures.GameCart;
        private Texture2D textureGameCrane = Textures.GameCrane;
        private Texture2D textureGamePirate = Textures.GamePirate;
        private Texture2D textureGameSpin = Textures.GameSpin;

        public List<ClickableComponent> messageableNpcList;
        private int scrollOffset = 0;
        private int maxVisibleNPCs = 7;
        int visibleSlots = 7;
        int spacing = 80;

        public static string selectedNpc = null;
        private string currentMessage = "";
        public static List<string> messageHistory = new();
        private int chatScrollOffset = 0;
        public static List<string> notificationHistory = new();
        private int notificationScrollOffset = 0;
        int maxBubbleWidth = 400;

        private Dictionary<string, ClickableTextureComponent> favourityNpcButton = new();
        private List<string> phoneSoundList = new();
        private Dictionary<string, ClickableTextureComponent> phoneSoundButton = new();


        // buttons
        private ClickableTextureComponent okButton;
        private ClickableTextureComponent backButton;
        private ClickableTextureComponent removeButton;
        private ClickableTextureComponent captureButton;
        private ClickableTextureComponent photoNextButton;
        private ClickableTextureComponent photoPreviousButton;
        private ClickableTextureComponent heartButton;

        // apps
        public static string currentApp = null;
        private ClickableTextureComponent appPhoto;
        private ClickableTextureComponent appText;
        private ClickableTextureComponent appCamera;
        private ClickableTextureComponent appSetting;
        private ClickableTextureComponent appGame;
        private ClickableTextureComponent appCalendar;
        private ClickableTextureComponent appNotification;


        private ClickableTextureComponent gamePirate;
        private ClickableTextureComponent gameJack;
        private ClickableTextureComponent gameDarts;
        private ClickableTextureComponent gameCart;
        private ClickableTextureComponent gameCrane;
        private ClickableTextureComponent gameSpin;


        private string captureFolderPath = Path.Combine(ModEntry.Instance.Helper.DirectoryPath, "userdata", Constants.SaveFolderName, "image");
        private List<string> capturedImages;
        private int currentImageIndex = -1;
        private Texture2D currentDisplayedImage = null;
        private Texture2D phoneBackgroundImage = null;


        // this textBox is hidden. it is created only to disable the game function key
        private TextBox textBox;

        private (string, string) currentSuggestion = ("", "");
        private Rectangle messageSuggestionBounds = Rectangle.Empty;
        private Rectangle firstMessageBounds = Rectangle.Empty;

        public PhoneMenu() : base(Game1.uiViewport.Width / 2 - 400, Game1.uiViewport.Height / 2 - 500, 600, 1000, true)
        {
            this.upperRightCloseButton = null;

            messageableNpcList = new List<ClickableComponent>();
            Dictionary<string, long> latestTimestamps = MessageManager.GetLatestAddDictionary();
            List<NPC> villagers = Utility.getAllVillagers()
            .Where(n =>
                n.IsVillager &&
                n.CanSocialize &&
                !n.IsMonster &&
                Game1.player.getFriendshipHeartLevelForNPC(n.Name) >= 1
                //|| n.Name == "Lewis" || n.Name == "Robin"
            )
            .OrderByDescending(npc => MessageManager.favouriteNpc.Contains(npc.Name))
            .ThenByDescending(npc => latestTimestamps.TryGetValue(npc.Name, out long ts) ? ts : 0)
            .ThenBy(npc => npc.Name)
            .ToList();

            for (int i = 0; i < villagers.Count; i++)
            {
                messageableNpcList.Add(new ClickableComponent(new Rectangle(0, 0, 0, 0), villagers[i].Name));
            }


            textBox = new TextBox(
                Game1.content.Load<Texture2D>("LooseSprites\\textBox"),
                null,
                Game1.smallFont,
                Game1.textColor
            )
            {
                X = -100,
                Y = -100,
                Width = 1,
                Height = 1,
                Text = ""
            };

            phoneSoundList = new List<string>
            {
                "getNewSpecialItem",
                "crystal",
                "phone",
                "achievement",
                "cacklingWitch",
                "dog_bark",
                "Duck",
                "cat",
                "explosion",
                "goldenWalnut",
                "machine_bell",
                "Meteorite",
                "thunder",
                "UFO",
                "yoba"
            };

            delay = TimeSpan.FromSeconds(Game1.random.NextInt64(10, 15));

            if (!string.IsNullOrEmpty(MessageManager.currentPhoneBackground) && File.Exists(MessageManager.currentPhoneBackground))
            {
                using (FileStream stream = new FileStream(MessageManager.currentPhoneBackground, FileMode.Open))
                {
                    Texture2D fullImage = Texture2D.FromStream(Game1.graphics.GraphicsDevice, stream);
                    phoneBackgroundImage = CropTexture(fullImage); // Crop from top-left
                }
            }

        }


        public override void draw(SpriteBatch b)
        {
            // notification app
            appNotification = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 80, this.yPositionOnScreen + 250, 84, 84),
                textureAppNotification,
                new Rectangle(0, 0, 84, 84),
                1f
            );

            // text app
            appText = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 200, this.yPositionOnScreen + 250, 84, 84),
                textureAppText,
                new Rectangle(0, 0, 84, 84),
                1f);
            okButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 490, this.yPositionOnScreen + 820, 64, 64),
                Game1.mouseCursors,
                new Rectangle(128, 256, 64, 64),
                1f);
            removeButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 300, this.yPositionOnScreen + 125, 32, 26 * 2),
                Game1.mouseCursors,
                new Rectangle(564, 102, 16, 26),
            2f);



            // camera app
            appCamera = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 320, this.yPositionOnScreen + 250, 84, 84),
                textureAppCamera,
                new Rectangle(0, 0, 84, 84),
                1f);
            captureButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 130, this.yPositionOnScreen + 117, 100, 40),
                Game1.mouseCursors2,
                new Rectangle(72, 32, 18, 15),
                4f);


            // photo app
            appPhoto = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 440, this.yPositionOnScreen + 250, 84, 84),
                textureAppPhoto,
                new Rectangle(0, 0, 84, 84),
                1f);

            photoNextButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 500, this.yPositionOnScreen + 500, 64, 64),
                Game1.mouseCursors,
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 33),
                1f);

            photoPreviousButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 35, this.yPositionOnScreen + 500, 64, 64),
                Game1.mouseCursors,
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 44),
                1f);



            // setting app
            appSetting = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 80, this.yPositionOnScreen + 370, 84, 84),
                textureAppSetting,
                new Rectangle(0, 0, 84, 84),
                1f);


            // calendar app
            appCalendar = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 200, this.yPositionOnScreen + 370, 84, 84),
                furnitureTexture,
                new Rectangle(417, 698, 15, 16),
                5.25f 
            );

            // game app
            appGame = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 320, this.yPositionOnScreen + 370, 84, 84),
                textureAppGame,
                new Rectangle(0, 0, 84, 84),
                1f
            );


            // minigame app
            gameDarts = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 140, this.yPositionOnScreen + 370, 84, 84),
                textureGameDarts,
                new Rectangle(0, 0, 84, 84),
                1f);

            gamePirate = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 260, this.yPositionOnScreen + 370, 84, 84),
                textureGamePirate,
                new Rectangle(0, 0, 84, 84),
                1f);

            gameCart = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 380, this.yPositionOnScreen + 370, 84, 84),
                textureGameCart,
                new Rectangle(0, 0, 84, 84),
                1f);

            gameSpin = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 140, this.yPositionOnScreen + 490, 84, 84),
                textureGameSpin,
                new Rectangle(0, 0, 84, 84),
                1f);

            gameJack = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 260, this.yPositionOnScreen + 490, 84, 84),
                textureGameJack,
                new Rectangle(0, 0, 84, 84),
                1f);

            gameCrane = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 380, this.yPositionOnScreen + 490, 84, 84),
                textureGameCrane,
                new Rectangle(0, 0, 84, 84),
                1f);


            // global
            backButton = new ClickableTextureComponent(
                new Rectangle(this.xPositionOnScreen + 40, this.yPositionOnScreen + 115, 64, 64),
                Game1.mouseCursors,
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 44),
                1f);


            if (currentApp == null)
            {
                b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height), Color.Black * 0.6f);
                b.Draw(texturePhoneCapture, new Vector2(xPositionOnScreen, yPositionOnScreen), Color.White);
                b.Draw(texturePhoneBackground, new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 190), Color.White);
                if (phoneBackgroundImage != null)
                {
                    Vector2 imagePosition = new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 190);
                    b.Draw(phoneBackgroundImage, imagePosition, Color.White * 0.8f);
                }

                appPhoto.draw(b);
                appCamera.draw(b);
                appText.draw(b);
                appSetting.draw(b);
                appCalendar.draw(b);
                appGame.draw(b);
                appNotification.draw(b);

                int unreadNpcCount = MessageManager.unreadCounts.Count(pair => pair.Value > 0);
                if (unreadNpcCount > 0)
                {
                    string unreadText = Math.Min(99, unreadNpcCount).ToString();
                    Vector2 unreadTextSize = Game1.smallFont.MeasureString(unreadText);

                    int badgeWidth = Math.Max(32, (int)unreadTextSize.X + 18);
                    int badgeHeight = Math.Max(24, (int)unreadTextSize.Y + 6);
                    int badgeX = appText.bounds.Right - (badgeWidth / 2) - 10;
                    int badgeY = appText.bounds.Y - (badgeHeight / 2) + 10;

                    IClickableMenu.drawTextureBox(
                        b,
                        Game1.menuTexture,
                        new Rectangle(0, 256, 60, 60),
                        badgeX,
                        badgeY,
                        badgeWidth,
                        badgeHeight,
                        new Color(255, 0, 0, 150),
                        1f,
                        false
                    );

                    Vector2 unreadTextPosition = new Vector2(
                        badgeX + (badgeWidth - unreadTextSize.X) / 2f,
                        badgeY + (badgeHeight - unreadTextSize.Y) / 2f
                    );
                    b.DrawString(Game1.smallFont, unreadText, unreadTextPosition, Color.White);
                }

                int unreadNotification = NotificationManager.getUnreadNotication();
                if (unreadNotification > 0)
                {
                    string unreadText = unreadNotification > 9 ? "9" : unreadNotification.ToString();
                    Vector2 unreadTextSize = Game1.smallFont.MeasureString(unreadText);

                    int badgeWidth = Math.Max(32, (int)unreadTextSize.X + 18);
                    int badgeHeight = Math.Max(24, (int)unreadTextSize.Y + 6);
                    int badgeX = appNotification.bounds.Right - (badgeWidth / 2) - 10;
                    int badgeY = appNotification.bounds.Y - (badgeHeight / 2) + 10;

                    IClickableMenu.drawTextureBox(
                        b,
                        Game1.menuTexture,
                        new Rectangle(0, 256, 60, 60),
                        badgeX,
                        badgeY,
                        badgeWidth,
                        badgeHeight,
                        new Color(255, 0, 0, 150),
                        1f,
                        false
                    );

                    Vector2 unreadTextPosition = new Vector2(
                        badgeX + (badgeWidth - unreadTextSize.X) / 2f,
                        badgeY + (badgeHeight - unreadTextSize.Y) / 2f
                    );
                    b.DrawString(Game1.smallFont, unreadText, unreadTextPosition, Color.White);
                }

            }
            else if (currentApp == "appGame")
            {
                b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height), Color.Black * 0.6f);
                b.Draw(texturePhoneCapture, new Vector2(xPositionOnScreen, yPositionOnScreen), Color.White);
                b.Draw(texturePhoneBackground, new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 190), Color.White);
                backButton.draw(b);

                gameCart.draw(b);
                gameDarts.draw(b);
                gameJack.draw(b);
                gamePirate.draw(b);
                gameCrane.draw(b);
                gameSpin.draw(b);
            }
            else if (currentApp == "appCamera")
            {
                Rectangle phoneRect = new Rectangle(xPositionOnScreen + 40, yPositionOnScreen + 190, 520, 705);

                b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.viewport.Width, phoneRect.Top), Color.Black * 0.4f);
                b.Draw(Game1.staminaRect, new Rectangle(0, phoneRect.Bottom, Game1.viewport.Width, Game1.viewport.Height - phoneRect.Bottom), Color.Black * 0.4f);
                b.Draw(Game1.staminaRect, new Rectangle(0, phoneRect.Top, phoneRect.Left, phoneRect.Height), Color.Black * 0.4f);
                b.Draw(Game1.staminaRect, new Rectangle(phoneRect.Right, phoneRect.Top, Game1.viewport.Width - phoneRect.Right, phoneRect.Height), Color.Black * 0.4f);


                b.Draw(texturePhoneCapture, new Vector2(xPositionOnScreen, yPositionOnScreen), Color.White);
                captureButton.draw(b);
                backButton.draw(b);
            }
            else if (currentApp == "appPhoto")
            {
                b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height), Color.Black * 0.6f);
                b.Draw(texturePhoneCapture, new Vector2(xPositionOnScreen, yPositionOnScreen), Color.White);
                b.Draw(texturePhoneBackground, new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 190), Color.White);
                backButton.draw(b);
                okButton.draw(b);

                if (currentDisplayedImage != null)
                {
                    Vector2 imagePosition = new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 190);
                    b.Draw(currentDisplayedImage, imagePosition, Color.White);

                    b.Draw(
                        removeButton.texture,
                        new Vector2(removeButton.bounds.X, removeButton.bounds.Y),
                        removeButton.sourceRect,
                        Color.White * 0.8f,
                        0f,
                        Vector2.Zero,
                        removeButton.scale,
                        SpriteEffects.None,
                        1f
                    );

                    if (currentImageIndex >= 0 && currentImageIndex < capturedImages.Count)
                    {
                        string rawName = Path.GetFileNameWithoutExtension(capturedImages[currentImageIndex]);

                        // Clean for display: remove random ID after "_", replace dashes with spaces
                        int underscoreIndex = rawName.IndexOf('_');
                        string displayName = underscoreIndex >= 0 ? rawName.Substring(0, underscoreIndex) : rawName;
                        displayName = displayName.Replace("-", " ");

                        Vector2 namePos = new Vector2(imagePosition.X, imagePosition.Y + currentDisplayedImage.Height - 50);
                        b.DrawString(Game1.dialogueFont, displayName, namePos, Color.White);

                        var rect = new Rectangle(218, 428, 7, 7);
                        if (MessageManager.currentPhoneBackground == capturedImages[currentImageIndex])
                            rect = new Rectangle(211, 428, 7, 7);

                        heartButton = new ClickableTextureComponent(
                        name: capturedImages[currentImageIndex],
                        bounds: new Rectangle(xPositionOnScreen + 500, yPositionOnScreen + 210, 35, 35),
                        label: null,
                        hoverText: "",
                        texture: Game1.mouseCursors,
                            sourceRect: rect,
                            scale: 5f
                        );
                        heartButton.draw(b);
                    }
                }



                // Draw buttons
                photoNextButton.draw(b);
                photoPreviousButton.draw(b);
            }
            else if (currentApp == "appText")
            {
                b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height), Color.Black * 0.6f);
                b.Draw(texturePhoneCapture, new Vector2(xPositionOnScreen, yPositionOnScreen), Color.White);
                b.Draw(texturePhoneBackground, new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 190), Color.White);
                backButton.draw(b);


                if (selectedNpc == null)
                {
                    favourityNpcButton.Clear();
                    int yStart = yPositionOnScreen + 220;
                    for (int i = 0; i < visibleSlots; i++)
                    {
                        int index = i + scrollOffset;
                        if (index >= messageableNpcList.Count) break;

                        var npc = messageableNpcList[index];
                        int y = yStart + i * spacing;



                        // === Draw scaled background box behind portrait ===
                        int portraitSize = 56; // Target display size
                        float scale = portraitSize / 64f * 1.4f; // Your texture is 64x64

                        Vector2 boxPosition = new Vector2(xPositionOnScreen + 100, y);

                        int unread = MessageManager.GetUnreadCount(npc.name);
                        if (unread > 0)
                        {
                            string number = Math.Min(unread, 9).ToString(); // display max 9
                            int digitSize = 8;
                            int spacingBetweenDigits = 0;
                            int totalWidth = number.Length * (digitSize + spacingBetweenDigits);

                            int numberX = (int)boxPosition.X - totalWidth - 41;
                            int numberY = (int)boxPosition.Y + 15;

                            int digitWidth = 8;
                            int digitHeight = 8;

                            int digitsPerRow = 6;

                            for (int j = 0; j < number.Length; j++)
                            {
                                char c = number[j];
                                if (char.IsDigit(c))
                                {
                                    int digit = c - '0';

                                    int row = digit / digitsPerRow;
                                    int col = digit % digitsPerRow;

                                    Rectangle sourceRect = new Rectangle(
                                        512 + col * digitWidth,     // X offset starts at 512
                                        128 + row * digitHeight,    // Y offset starts at 128
                                        digitWidth,
                                        digitHeight
                                    );

                                    //Game1.chatBox.addErrorMessage(unread.ToString());
                                    b.Draw(
                                        Game1.mouseCursors,
                                        new Vector2(numberX, numberY),
                                        sourceRect,
                                        Color.White,
                                        0f,
                                        Vector2.Zero,
                                        5f, // Scale it up to be visible
                                        SpriteEffects.None,
                                        1f
                                    );
                                }
                            }
                        }



                        b.Draw(
                            texturePortraitBackground,
                            position: boxPosition,
                            sourceRectangle: null,
                            color: Color.White,
                            rotation: 0f,
                            origin: Vector2.Zero,
                            scale: scale,
                            effects: SpriteEffects.None,
                            layerDepth: 0f
                        );
                        var realNpc = Game1.getCharacterFromName(npc.name);

                        // === Draw NPC portrait ===
                        if (realNpc.Portrait != null)
                        {
                            Rectangle portraitRect = new Rectangle((int)boxPosition.X + 9, (int)boxPosition.Y + 10, 60, 60);
                            Rectangle sourceRect = new Rectangle(0, 0, 64, 64);
                            b.Draw(realNpc.Portrait, portraitRect, sourceRect, Color.White);
                        }

                        // === Draw NPC name ===
                        int nameX = (int)boxPosition.X + portraitSize + 40;
                        npc.bounds = new Rectangle(nameX, y, 200, portraitSize);
                        b.DrawString(Game1.dialogueFont, npc.name, new Vector2(nameX, y + 15), Color.Black);


                        var rect = new Rectangle(218, 428, 7, 7);
                        if (MessageManager.favouriteNpc.Contains(npc.name))
                            rect = new Rectangle(211, 428, 7, 7);

                        heartButton = new ClickableTextureComponent(
                        name: npc.name,
                        bounds: new Rectangle(nameX + 310, y + 20, 35, 35),
                            label: null,
                            hoverText: "",
                        texture: Game1.mouseCursors,
                            sourceRect: rect,
                            scale: 5f
                        );

                        favourityNpcButton[npc.name] = heartButton;
                        heartButton.draw(b);

                    }

                    // Draw input box
                    int inputX = xPositionOnScreen + 100;
                    int inputY = yPositionOnScreen + 820;
                    int inputWidth = 400;

                    int fontHeight = (int)Game1.smallFont.MeasureString("A").Y;
                    int inputHeight = fontHeight + 30;

                    IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                        inputX, inputY, inputWidth, inputHeight,
                        Color.White, 1f, false);

                    string visibleText = currentMessage;
                    Vector2 textSize = Game1.smallFont.MeasureString(currentMessage);
                    int maxInputWidth = 420;

                    while (textSize.X > maxInputWidth - 20 && visibleText.Length > 1)
                    {
                        visibleText = visibleText.Substring(1);
                        textSize = Game1.smallFont.MeasureString(visibleText);
                    }

                    b.DrawString(Game1.smallFont, visibleText, new Vector2(inputX + 15, inputY + 20), Color.Black);

                    // enable ghost textBox. do not remove
                    textBox.Selected = true;
                    Game1.keyboardDispatcher.Subscriber = textBox;
                }
                else
                {
                    b.DrawString(Game1.dialogueFont, $"{selectedNpc}", new Vector2(xPositionOnScreen + 105, yPositionOnScreen + 125), Color.Black);

                    backButton.draw(b);
                    b.Draw(
                        removeButton.texture,
                        new Vector2(removeButton.bounds.X, removeButton.bounds.Y),
                        removeButton.sourceRect,
                        Color.White * 0.8f,
                        0f,
                        Vector2.Zero,
                        removeButton.scale,
                        SpriteEffects.None,
                        1f
                    );


                    // Save old spritebatch state and apply clipping
                    b.End();

                    Rectangle chatClipRect = new Rectangle(xPositionOnScreen, yPositionOnScreen + 200, width, 600); // Adjust height as needed
                    Game1.graphics.GraphicsDevice.ScissorRectangle = chatClipRect;

                    b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState() { ScissorTestEnable = true });

                    // Draw messages within clipped region
                    int messageY = yPositionOnScreen + 200;
                    SpriteFont font = Game1.smallFont;
                    List<string> allMessages = messageHistory;
                    List<string> visibleMessages = allMessages.Skip(chatScrollOffset).ToList();

                    foreach (string msg in visibleMessages)
                    {
                        bool fromSystem = msg.StartsWith("SYSTEM: ") && msg.EndsWith("---");
                        if (false)
                        {
                            b.DrawString(Game1.smallFont, msg, new Vector2(xPositionOnScreen + 50 + 15, yPositionOnScreen + 400 + 20), Color.Black);
                        }
                        else
                        {
                            bool fromPlayer = msg.StartsWith("PLAYER:") || msg.StartsWith("NPC:");
                            string text = msg.Substring(msg.IndexOf(":") + 1).Trim();
                            List<string> wrappedLines = SplitTextIntoLines(text, font, maxBubbleWidth);

                            int lineHeight = (int)font.MeasureString("A").Y + 4;
                            int bubbleHeight = wrappedLines.Count * lineHeight + 10;
                            int bubbleWidth = 0;

                            foreach (var line in wrappedLines)
                                bubbleWidth = Math.Max(bubbleWidth, (int)font.MeasureString(line).X + 20);

                            Rectangle bubbleRect = msg.StartsWith("PLAYER:")
                                ? new Rectangle(xPositionOnScreen + width - bubbleWidth - 50, messageY, bubbleWidth, bubbleHeight)
                                : new Rectangle(xPositionOnScreen + 50, messageY, bubbleWidth, bubbleHeight);

                            Color bgColor = fromPlayer ? new Color(160, 210, 255) : new Color(220, 220, 220);

                            if (!fromSystem)
                            {
                                IClickableMenu.drawTextureBox(
                                    b,
                                    Game1.menuTexture,
                                    new Rectangle(0, 256, 60, 60), // source rect for 9-slice
                                    bubbleRect.X - 5,
                                    bubbleRect.Y,
                                    bubbleRect.Width + 12,
                                    bubbleRect.Height + 10,
                                    new Color(255, 255, 255, 200),
                                    1f,
                                    false
                                );

                                int textY = bubbleRect.Y + 15;
                                foreach (var line in wrappedLines)
                                {
                                    Vector2 linePos = new Vector2(bubbleRect.X + 10, textY);
                                    b.DrawString(font, line, linePos, Color.Black);
                                    textY += lineHeight;
                                }
                            }
                            else
                            {
                                int textY = bubbleRect.Y + 15;
                                Vector2 linePos = new Vector2(bubbleRect.X + 10, textY);
                                b.DrawString(font, text, linePos, Color.Black);
                            }

                            messageY += bubbleHeight + 10;
                        }
                    }

                    // Reset clipping
                    b.End();
                    b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

                    if (ModEntry.npcMessagesToday.ContainsKey(selectedNpc) && ModEntry.npcMessagesToday[selectedNpc].Count > 0)
                    {
                        okButton.draw(b);
                        // Draw input box
                        int inputX = xPositionOnScreen + 50;
                        int inputY = yPositionOnScreen + 820;
                        int inputWidth = 430;

                        int fontHeight = (int)Game1.smallFont.MeasureString("A").Y;
                        int inputHeight = fontHeight + 30;

                        IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                            inputX, inputY, inputWidth, inputHeight,
                            Color.White, 1f, false);

                        string visibleText = currentMessage;
                        Vector2 textSize = Game1.smallFont.MeasureString(currentMessage);
                        int maxInputWidth = 420;

                        while (textSize.X > maxInputWidth - 20 && visibleText.Length > 1)
                        {
                            visibleText = visibleText.Substring(1);
                            textSize = Game1.smallFont.MeasureString(visibleText);
                        }

                        b.DrawString(Game1.smallFont, visibleText, new Vector2(inputX + 15, inputY + 20), Color.Black);

                        // enable ghost textBox. do not remove
                        textBox.Selected = true;
                        Game1.keyboardDispatcher.Subscriber = textBox;
                    }
                    else
                    {
                        string firstMessage = Game1.timeOfDay < 1200 ? $"Good morning {selectedNpc}" : Game1.timeOfDay < 1800 ? $"Good afternoon {selectedNpc}" : $"Good evening {selectedNpc}";
                        float maxWidth = Game1.smallFont.MeasureString(firstMessage).X + 20f;
                        int lineHeight = (int)(Game1.smallFont.MeasureString("A").Y + 20);

                        Vector2 position = new Vector2(xPositionOnScreen + 300 - (maxWidth + 20) / 2, yPositionOnScreen + 820);
                        firstMessageBounds = new Rectangle(
                            (int)position.X,
                            (int)position.Y,
                            (int)maxWidth + 20,
                            lineHeight + 10
                        );

                        IClickableMenu.drawTextureBox(
                            Game1.spriteBatch,
                            Game1.menuTexture,
                            new Rectangle(0, 256, 60, 60),
                            firstMessageBounds.X,
                            firstMessageBounds.Y,
                            firstMessageBounds.Width,
                            firstMessageBounds.Height,
                            Color.White,
                            1f,
                            false
                        );

                        Utility.drawTextWithShadow(
                            Game1.spriteBatch,
                            firstMessage,
                            Game1.smallFont,
                            new Vector2(firstMessageBounds.X + 20, firstMessageBounds.Y + 20),
                            Game1.textColor
                        );

                    }


                    if (currentSuggestion.Item1 != "" && ModEntry.Config.HelperOption == "Minimal")
                    {
                        string suggestionText = currentSuggestion.Item1;
                        string[] lines = suggestionText.Split('\n');

                        // Measure the width of the longest line
                        float maxWidth = lines.Max(line => Game1.smallFont.MeasureString(line).X) + 15f;

                        // Get font line height (all lines same height in pixel font)
                        int lineHeight = (int)(Game1.smallFont.MeasureString("A").Y + 5);

                        Vector2 position = new Vector2(xPositionOnScreen + 600, yPositionOnScreen + 800);
                        messageSuggestionBounds = new Rectangle(
                            (int)position.X,
                            (int)position.Y,
                            (int)maxWidth + 20,
                            lineHeight * lines.Length + 10
                        );

                        IClickableMenu.drawTextureBox(
                            Game1.spriteBatch,
                            Game1.menuTexture,
                            new Rectangle(0, 256, 60, 60),
                            messageSuggestionBounds.X,
                            messageSuggestionBounds.Y,
                            messageSuggestionBounds.Width,
                            messageSuggestionBounds.Height,
                            Color.White,
                            1f,
                            false
                        );

                        for (int i = 0; i < lines.Length; i++)
                        {
                            Utility.drawTextWithShadow(
                                Game1.spriteBatch,
                                lines[i],
                                Game1.smallFont,
                                new Vector2(messageSuggestionBounds.X + 15, messageSuggestionBounds.Y + 10 + i * lineHeight),
                                Game1.textColor
                            );
                        }
                    }
                    else if (ModEntry.Config.HelperOption == "Always" && currentMessage != "")
                    {
                        string[] lines = { "Type one of these value",
                            "to start event manually:",
                            "  [Trigger Picnic Event]",
                            "  [Trigger Dinner Event]",
                            "  [Trigger Campfire Event]",
                            "  [Trigger Birthday Event]"
                        };

                        float maxWidth = lines.Max(line => Game1.smallFont.MeasureString(line).X) + 15f;

                        int lineHeight = (int)(Game1.smallFont.MeasureString("A").Y + 5);

                        Vector2 position = new Vector2(xPositionOnScreen + 600, yPositionOnScreen + 650);
                        messageSuggestionBounds = new Rectangle(
                            (int)position.X,
                            (int)position.Y,
                            (int)maxWidth + 20,
                            lineHeight * lines.Length + 10
                        );

                        IClickableMenu.drawTextureBox(
                            Game1.spriteBatch,
                            Game1.menuTexture,
                            new Rectangle(0, 256, 60, 60),
                            messageSuggestionBounds.X,
                            messageSuggestionBounds.Y,
                            messageSuggestionBounds.Width,
                            messageSuggestionBounds.Height,
                            Color.White,
                            1f,
                            false
                        );

                        for (int i = 0; i < lines.Length; i++)
                        {
                            Utility.drawTextWithShadow(
                                Game1.spriteBatch,
                                lines[i],
                                Game1.smallFont,
                                new Vector2(messageSuggestionBounds.X + 15, messageSuggestionBounds.Y + 10 + i * lineHeight),
                                Game1.textColor
                            );
                        }
                    }
                }
            }
            else if (currentApp == "appSetting")
            {
                b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height), Color.Black * 0.6f);
                b.Draw(texturePhoneCapture, new Vector2(xPositionOnScreen, yPositionOnScreen), Color.White);
                b.Draw(texturePhoneBackground, new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 190), Color.White);
                backButton.draw(b);

                int yStart = yPositionOnScreen + 220;
                for (int i = 0; i < visibleSlots; i++)
                {
                    int index = i + scrollOffset;
                    if (index >= phoneSoundList.Count) break;

                    var soundString = phoneSoundList[index];
                    int y = yStart + i * spacing;



                    // === Draw scaled background box behind portrait ===

                    Vector2 boxPosition = new Vector2(xPositionOnScreen + 100, y);


                    // === Draw NPC name ===
                    int nameX = (int)boxPosition.X;
                    b.DrawString(Game1.dialogueFont, soundString, new Vector2(nameX, y + 15), Color.Black);


                    var rect = new Rectangle(218, 428, 7, 7);
                    if (MessageManager.currentPhoneSound == soundString)
                        rect = new Rectangle(211, 428, 7, 7);

                    heartButton = new ClickableTextureComponent(
                    name: soundString,
                    bounds: new Rectangle(nameX + 370, y + 20, 35, 35),
                        label: null,
                        hoverText: "",
                    texture: Game1.mouseCursors,
                        sourceRect: rect,
                        scale: 5f
                    );

                    phoneSoundButton[soundString] = heartButton;
                    heartButton.draw(b);

                }


            }
            else if (currentApp == "appNotification")
            {
                b.Draw(Game1.staminaRect, new Rectangle(0, 0, Game1.viewport.Width, Game1.viewport.Height), Color.Black * 0.6f);
                b.Draw(texturePhoneCapture, new Vector2(xPositionOnScreen, yPositionOnScreen), Color.White);
                b.Draw(texturePhoneBackground, new Vector2(xPositionOnScreen + 40, yPositionOnScreen + 190), Color.White);
                b.Draw(
                        removeButton.texture,
                        new Vector2(removeButton.bounds.X, removeButton.bounds.Y),
                        removeButton.sourceRect,
                        Color.White * 0.8f,
                        0f,
                        Vector2.Zero,
                        removeButton.scale,
                        SpriteEffects.None,
                        1f
                    );
                backButton.draw(b);

                b.End();
                Rectangle chatClipRect = new Rectangle(xPositionOnScreen, yPositionOnScreen + 200, width, 665); // Adjust height as needed
                Game1.graphics.GraphicsDevice.ScissorRectangle = chatClipRect;

                b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState() { ScissorTestEnable = true });

                // Draw messages within clipped region
                int messageY = yPositionOnScreen + 200;
                SpriteFont font = Game1.smallFont;

                // Show newest notifications first. Reverse the list, then skip by offset.
                foreach (string msg in notificationHistory.AsEnumerable().Reverse().Skip(notificationScrollOffset).ToList())
                {
                    List<string> wrappedLines = SplitNotificationIntoLines(msg, font, 485);

                    int lineHeight = (int)font.MeasureString("A").Y + 4;
                    int bubbleHeight = wrappedLines.Count * lineHeight + 10;
                    int bubbleWidth = 0;

                    foreach (var line in wrappedLines)
                        bubbleWidth = Math.Max(bubbleWidth, (int)font.MeasureString(line).X + 20);

                    Rectangle bubbleRect = new Rectangle(xPositionOnScreen + 50, messageY, bubbleWidth, bubbleHeight);

                    IClickableMenu.drawTextureBox(
                        b,
                        Game1.menuTexture,
                        new Rectangle(0, 256, 60, 60), // source rect for 9-slice
                        bubbleRect.X - 5,
                        bubbleRect.Y,
                        bubbleRect.Width + 12,
                        bubbleRect.Height + 10,
                        new Color(0, 0, 0, 100),
                        1f,
                        false
                    );

                    int textY = bubbleRect.Y + 15;
                    foreach (var line in wrappedLines)
                    {
                        Vector2 linePos = new Vector2(bubbleRect.X + 10, textY);
                        b.DrawString(font, line, linePos, Color.White);
                        textY += lineHeight;
                    }

                    messageY += bubbleHeight + 20;
                    
                }

                // Reset clipping
                b.End();
                b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

            }


            // base
            base.draw(b);
            drawMouse(b);

           

        }
        public override void releaseLeftClick(int x, int y)
        {
            base.releaseLeftClick(x, y);
            isDragging = false;
        }

        public override void update(GameTime time)
        {
            base.update(time);

            if (isDragging)
            {
                xPositionOnScreen = Game1.getMouseX() - dragOffsetX;
                yPositionOnScreen = Game1.getMouseY() - dragOffsetY;

                xPositionOnScreen = Math.Max(0, Math.Min(xPositionOnScreen, Game1.uiViewport.Width - width));
                yPositionOnScreen = Math.Max(0, Math.Min(yPositionOnScreen, Game1.uiViewport.Height - height));
            }

            ModEntry.currentMenuX = xPositionOnScreen;
            ModEntry.currentMenuY = yPositionOnScreen;


        }

        public override void leftClickHeld(int x, int y)
        {
            base.leftClickHeld(x, y);
            if (new Rectangle(xPositionOnScreen, yPositionOnScreen, width, height).Contains(x, y))
            {
                isDragging = true;
                dragOffsetX = x - xPositionOnScreen;
                dragOffsetY = y - yPositionOnScreen;
            }
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            if (selectedNpc != null && currentSuggestion.Item1 != "" && messageSuggestionBounds.Contains(x, y))
            {
                currentMessage = currentSuggestion.Item2.Replace("\n", " ");
                currentSuggestion = new ("", "");
                Game1.playSound("smallSelect"); // optional
                CalculateScrollToBottomOffset(messageHistory);
                return;
            }
            if(selectedNpc != null && firstMessageBounds.Contains(x, y) && (!ModEntry.npcMessagesToday.ContainsKey(selectedNpc) || ModEntry.npcMessagesToday[selectedNpc].Count == 0))
            {
                string firstMessage = Game1.timeOfDay < 1200 ? $"Good morning {selectedNpc}" : Game1.timeOfDay < 1800 ? $"Good afternoon {selectedNpc}" : $"Good evening {selectedNpc}";
                MessageManager.AddMessage(selectedNpc, $"PLAYER: {firstMessage}");
                ModEntry.FirstDailyText(selectedNpc, firstMessage);
                messageHistory = MessageManager.GetMessages(selectedNpc);

            }



            if (appPhoto.containsPoint(x, y) && currentApp == null)
            {
                capturedImages = Directory.Exists(captureFolderPath)
                    ? Directory.GetFiles(captureFolderPath, "*.png")
                        .OrderByDescending(f => File.GetCreationTime(f))
                        .ToList()
                    : new List<string>();

                currentApp = "appPhoto";

                if (capturedImages.Count > 0)
                {
                    currentImageIndex = 0; // Newest image is now first
                    LoadImageAtIndex(currentImageIndex);
                    Game1.playSound("smallSelect");
                }
                return;
            }
            else if (appText.containsPoint(x, y) && currentApp == null)
            {
                UpdateNpcList();
                currentApp = "appText";
                return;
            }
            else if (appCamera.containsPoint(x, y) && currentApp == null)
            {
                currentApp = "appCamera";
                return;
            }
            else if (appSetting.containsPoint(x, y) && currentApp == null)
            {
                currentApp = "appSetting";
                return;
            }
            else if(appGame.containsPoint(x, y) && currentApp == null)
            {
                currentApp = "appGame";
                return;
            }
            else if(appNotification.containsPoint(x, y) && currentApp == null)
            {
                OpenNotification();
                currentApp = "appNotification";
                return;
            }


            if (captureButton.containsPoint(x, y) && currentApp == "appCamera")
            {
                ModEntry.takeScreenshot = true;
                return;
            }

            if (backButton.containsPoint(x, y))
            {
                if (currentApp == "appText")
                {
                    if (selectedNpc != null)
                    {
                        MessageManager.SetUnreadCount(selectedNpc);
                        currentSuggestion = new("", "");
                        selectedNpc = null;
                        currentMessage = "";
                        UpdateNpcList();
                        return;
                    }
                    else
                    {
                        UpdateNpcList();
                        currentApp = null;
                        currentMessage = "";
                        return;
                    }
                }
                else if ( new List<string> { "appCamera", "appPhoto", "appSetting", "appGame", "appNotification" }.Contains(currentApp))
                {
                    
                    currentApp = null;
                    return;
                }
            }

            if (removeButton.containsPoint(x, y) && currentApp == "appText")
            {
                MessageManager.ClearMessages(selectedNpc);
                messageHistory = MessageManager.GetMessages(selectedNpc);
                return;
            }
            else if (removeButton.containsPoint(x, y) && currentApp == "appPhoto")
            {
                if (currentImageIndex >= 0 && currentImageIndex < capturedImages.Count)
                {
                    string fileToDelete = capturedImages[currentImageIndex];

                    // Remove file from disk
                    try
                    {
                        File.Delete(fileToDelete);
                    }
                    catch (Exception ex)
                    {
                        ModEntry.SMonitor.Log($"Failed to delete image: {ex.Message}", LogLevel.Warn);
                    }

                    // Remove from list
                    capturedImages.RemoveAt(currentImageIndex);

                    // Choose next image
                    if (capturedImages.Count == 0)
                    {
                        currentDisplayedImage = null;
                        currentImageIndex = -1;
                    }
                    else if (currentImageIndex > 0)
                    {
                        currentImageIndex--; // Go to previous (left)
                        LoadImageAtIndex(currentImageIndex);
                    }
                    else
                    {
                        // Stay at index 0 if no left
                        LoadImageAtIndex(currentImageIndex);
                    }

                    Game1.playSound("trashcan");
                }

                return;
            }
            else if (removeButton.containsPoint(x, y) && currentApp == "appNotification")
            {
                NotificationManager.clearNotification();
                notificationHistory = NotificationManager.getNoticationList();
                return;
            }





            if (photoNextButton.containsPoint(x, y) && currentApp == "appPhoto")
            {
                if (currentImageIndex > 0)
                {
                    currentImageIndex--;
                    LoadImageAtIndex(currentImageIndex);
                    Game1.playSound("shwip");
                    return;
                }
            }
            else if (photoPreviousButton.containsPoint(x, y) && currentApp == "appPhoto")
            {
                if (currentImageIndex < capturedImages.Count - 1)
                {
                    currentImageIndex++;
                    LoadImageAtIndex(currentImageIndex);
                    Game1.playSound("shwip");
                    return;
                }
            }
            else if (heartButton != null && heartButton.containsPoint(x,y) && currentApp == "appPhoto")
            {
                MessageManager.currentPhoneBackground = heartButton.name;

                if (!string.IsNullOrEmpty(MessageManager.currentPhoneBackground) && File.Exists(MessageManager.currentPhoneBackground))
                {
                    using (FileStream stream = new FileStream(MessageManager.currentPhoneBackground, FileMode.Open))
                    {
                        phoneBackgroundImage = Texture2D.FromStream(Game1.graphics.GraphicsDevice, stream);
                        phoneBackgroundImage = CropTexture(phoneBackgroundImage);
                    }
                }
            }

            if (currentApp == "appSetting")
            {
                foreach (var button in phoneSoundButton.Values)
                {
                    if (button.containsPoint(x, y))
                    {
                        DelayedAction.playSoundAfterDelay(button.name, 0);
                        DelayedAction.playSoundAfterDelay(button.name, 1500);
                        MessageManager.currentPhoneSound = button.name;
                    }
                }
            }


            if (appCalendar.containsPoint(x, y) && currentApp == null)
            {
                ClosePhoneMenu();
                Game1.activeClickableMenu = new Billboard();
            }



            if (selectedNpc == null && currentApp == "appText")
            {
                foreach (var button in favourityNpcButton.Values)
                {
                    if (button.containsPoint(x, y))
                    {
                        if (MessageManager.favouriteNpc.Contains(button.name))
                            MessageManager.favouriteNpc.Remove(button.name);
                        else
                            MessageManager.favouriteNpc.Add(button.name);

                        DelayedAction.functionAfterDelay(UpdateNpcList, 200);
                    }
                }

                int yStart = yPositionOnScreen + 220;

                for (int i = 0; i < visibleSlots; i++)
                {
                    int index = i + scrollOffset;
                    if (index >= messageableNpcList.Count) break;

                    Rectangle slot = new Rectangle(xPositionOnScreen + 40, yStart + i * spacing, 400, 60);
                    if (slot.Contains(x, y))
                    {
                        currentMessage = "";
                        selectedNpc = messageableNpcList[index].name;
                        MessageManager.SetUnreadCount(selectedNpc);
                        messageHistory = MessageManager.GetMessages(selectedNpc);
                        OpenChat(selectedNpc);
                        return;
                    }
                }
            }
            else if (currentApp == "appText" && selectedNpc != null)
            {
                if (okButton.containsPoint(x, y))
                {
                    if (!string.IsNullOrWhiteSpace(currentMessage))
                    {
                        onPlayerSend();
                    }
                }
            }


            
            else if (gameCart.containsPoint(x, y) && currentApp == "appGame")
            {
                exitThisMenu();
                int type = Game1.dayOfMonth % 2 == 0 ? 3 : 2;
                Game1.currentMinigame = new StardewValley.Minigames.MineCart(0, type);
                return;
            }
            else if (gameJack.containsPoint(x, y) && currentApp == "appGame")
            {
                exitThisMenu();
                int bet = Game1.dayOfMonth % 2 == 0 ? 1000 : 100;
                int costToStart = 100; // Always 100G to start
                int playerGold = Game1.player.Money;
                int playerClubCoins = Game1.player.clubCoins;

                // Check if player has enough gold and coins
                if (playerGold < costToStart)
                {
                    Game1.addHUDMessage(new HUDMessage($"You don't have enough 100 Gold!", 3));
                    return;
                }
                if (playerClubCoins < bet)
                {
                    Game1.addHUDMessage(new HUDMessage($"You don't have enough Casino Coins for today bet: {bet}!", 3));
                    return;
                }

                // Confirmation message
                string question = $"Play Calico Jack?\nCost to start: {costToStart}G\nToday's bet: {bet} Casino Coins";

                // Show confirmation dialog
                Game1.activeClickableMenu = new ConfirmationDialog(
                    question,
                    onConfirm: (Farmer who) =>
                    {
                        Game1.player.Money -= costToStart;

                        bool highStakes = bet == 1000;
                        Game1.activeClickableMenu = null;
                        Game1.currentMinigame = new StardewValley.Minigames.CalicoJack(highStakes: highStakes);
                    },
                    onCancel: (Farmer who) =>
                    {
                        Game1.activeClickableMenu = null;
                    }
                );

                return;
            }
            else if (gameDarts.containsPoint(x, y) && currentApp == "appGame")
            {
                exitThisMenu();
                Game1.currentMinigame = new StardewValley.Minigames.Darts();
                return;
            }
            else if (gamePirate.containsPoint(x, y) && currentApp == "appGame")
            {
                exitThisMenu();
                Game1.currentMinigame = new StardewValley.Minigames.AbigailGame();
                return;
            }
            else if (gameSpin.containsPoint(x, y) && currentApp == "appGame")
            {
                exitThisMenu();
                if (Game1.player.clubCoins < 10)
                {
                    Game1.addHUDMessage(new HUDMessage($"You don't have enough Casino Coins!", 3));
                    return;
                }
                Game1.currentMinigame = new StardewValley.Minigames.Slots();
                return;
            }
            else if (gameCrane.containsPoint(x, y) && currentApp == "appGame")
            {
                exitThisMenu();
                string question = $"Play Crane Game?\nCost to start: 750G";

                // Show confirmation dialog
                Game1.activeClickableMenu = new ConfirmationDialog(
                    question,
                    onConfirm: (Farmer who) =>
                    {
                        Game1.player.Money -= 750;

                        Game1.activeClickableMenu = null;
                        Game1.currentMinigame = new StardewValley.Minigames.CraneGame();
                    },
                    onCancel: (Farmer who) =>
                    {
                        Game1.activeClickableMenu = null;
                    }
                );
                return;
            }


        }


        public override void receiveScrollWheelAction(int direction)
        {
            if (selectedNpc != null && currentApp == "appText")
            {
                if (direction > 0) chatScrollOffset = Math.Max(chatScrollOffset - 1, 0);
                else chatScrollOffset++;
                chatScrollOffset = Math.Min(chatScrollOffset, messageHistory.Count - 1);
            }
            else if (currentApp == "appText")
            {
                scrollOffset -= direction / 120;
                scrollOffset = Math.Max(0, Math.Min(scrollOffset, messageableNpcList.Count - maxVisibleNPCs));
            }
            else if (currentApp == "appSetting")
            {
                scrollOffset -= direction / 120;
                scrollOffset = Math.Max(0, Math.Min(scrollOffset, phoneSoundList.Count - maxVisibleNPCs));
            }
            else if (currentApp == "appNotification")
            {
                if (direction > 0)
                    notificationScrollOffset = Math.Max(notificationScrollOffset - 1, 0);
                else
                    notificationScrollOffset++;
                notificationScrollOffset = Math.Min(notificationScrollOffset, Math.Max(0, notificationHistory.Count - 1));
            }
        }


        public override void receiveKeyPress(Keys key)
        {
            if (key == Keys.Escape)
            {
                if (currentApp == "appText" && selectedNpc != null)
                {
                    MessageManager.SetUnreadCount(selectedNpc);
                    selectedNpc = null;
                    currentSuggestion = new("", "");
                    return;
                }
                
                if (currentApp != null)
                {
                    currentApp = null;
                    return;
                }

                currentMessage = "";
                exitThisMenu();
                return;
            }
            else if (selectedNpc == null && currentApp == "appText")
            {
                if (key == Keys.Back && currentMessage.Length > 0)
                {
                    currentMessage = currentMessage[..^1];
                    UpdateNpcList();
                }
                else
                {
                    Game1.playSound("coin");

                    bool shiftPressed = Keyboard.GetState().IsKeyDown(Keys.LeftShift) || Keyboard.GetState().IsKeyDown(Keys.RightShift);
                    char c = GetCharFromKey(key, shiftPressed);

                    if (c != '\0') currentMessage += c;
                    UpdateNpcList();
                }
            }
            else if (selectedNpc != null && currentApp == "appText")
            {
                if (key == Keys.Back && currentMessage.Length > 0)
                    currentMessage = currentMessage[..^1];
                else if (key == Keys.Enter)
                {

                    if (!string.IsNullOrWhiteSpace(currentMessage))
                    {
                        onPlayerSend();
                    }
                }
                else
                {
                    Game1.playSound("coin");

                    bool shiftPressed = Keyboard.GetState().IsKeyDown(Keys.LeftShift) || Keyboard.GetState().IsKeyDown(Keys.RightShift);
                    char c = GetCharFromKey(key, shiftPressed);

                    if (c != '\0') currentMessage += c;
                }
            }
        }

        private void onPlayerSend()
        {
            if (currentMessage.ToLower().Contains("dinner"))
                currentSuggestion = new($"Want to invite\n{selectedNpc} for dinner?", "[start dinner event]");
            else if (currentMessage.ToLower().Contains("birthday") && ModEntry.GetNpcsWithBirthdayToday().Contains(Game1.getCharacterFromName(selectedNpc)))
                currentSuggestion = new($"Want to celebrate\n{selectedNpc}'s birthday'?", "[start NPC birthday event]");
            else
                currentSuggestion = new("", "");


            MessageManager.AddMessage(selectedNpc, $"PLAYER: {currentMessage}");
            messageHistory = MessageManager.GetMessages(selectedNpc);
            string reply = sendTextMessage(selectedNpc, currentMessage);
            currentMessage = "";
            chatScrollOffset = CalculateScrollToBottomOffset(messageHistory);
        }


        public static string sendTextMessage(string npcName, string userMessage)
        {
            if (!pendingMessages.ContainsKey(npcName))
                pendingMessages[npcName] = new List<string>();

            pendingMessages[npcName].Add(userMessage);

            // Cancel old timer if exists
            if (replyTimers.ContainsKey(npcName))
                replyTimers[npcName].Cancel();

            var cts = new CancellationTokenSource();
            replyTimers[npcName] = cts;

            // Start delayed task
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delay, cts.Token);
                    await SendBatchMessage(npcName);
                }
                catch (TaskCanceledException) { /* Timer reset */ }
            });

            return ""; // no immediate reply
        }

        private static async Task SendBatchMessage(string npcName)
        {
            if (!pendingMessages.TryGetValue(npcName, out var messages) || messages.Count == 0)
                return;

            // Combine player messages into one
            string merged = string.Join("\n", messages);
            int counter = messages.Count;

            // Clear the queue
            pendingMessages[npcName].Clear();

            // Call assistant (simulate with delay if needed)

            string reply = await ModEntry.SendMessageToAssistant(npcName, merged, counter);

            // Add reply to message history
            if (reply != null && reply != "")
            { 
                MessageManager.AddMessage(npcName, $"{npcName}: {reply}");
                if (selectedNpc == npcName)
                    messageHistory = MessageManager.GetMessages(npcName);
            }
        }



        private char GetCharFromKey(Keys key, bool shift)
        {
            switch (key)
            {
                // Letters
                case >= Keys.A and <= Keys.Z:
                    return shift ? key.ToString()[0] : char.ToLower(key.ToString()[0]);

                // Top row numbers
                case Keys.D0: return shift ? ')' : '0';
                case Keys.D1: return shift ? '!' : '1';
                case Keys.D2: return shift ? '@' : '2';
                case Keys.D3: return shift ? '#' : '3';
                case Keys.D4: return shift ? '$' : '4';
                case Keys.D5: return shift ? '%' : '5';
                case Keys.D6: return shift ? '^' : '6';
                case Keys.D7: return shift ? '&' : '7';
                case Keys.D8: return shift ? '*' : '8';
                case Keys.D9: return shift ? '(' : '9';

                // Numpad numbers
                case Keys.NumPad0: return '0';
                case Keys.NumPad1: return '1';
                case Keys.NumPad2: return '2';
                case Keys.NumPad3: return '3';
                case Keys.NumPad4: return '4';
                case Keys.NumPad5: return '5';
                case Keys.NumPad6: return '6';
                case Keys.NumPad7: return '7';
                case Keys.NumPad8: return '8';
                case Keys.NumPad9: return '9';

                // Symbols
                case Keys.OemPeriod: return shift ? '>' : '.';
                case Keys.OemComma: return shift ? '<' : ',';
                case Keys.OemQuestion: return shift ? '?' : '/';
                case Keys.OemSemicolon: return shift ? ':' : ';';
                case Keys.OemQuotes: return shift ? '"' : '\'';
                case Keys.OemOpenBrackets: return shift ? '{' : '[';
                case Keys.OemCloseBrackets: return shift ? '}' : ']';
                case Keys.OemPipe: return shift ? '|' : '\\';
                case Keys.OemMinus: return shift ? '_' : '-';
                case Keys.OemPlus: return shift ? '+' : '=';
                case Keys.Space: return ' ';

                default: return '\0';
            }
        }


        private List<string> SplitTextIntoLines(string text, SpriteFont font, int maxWidth)
        {
            List<string> lines = new();
            // Treat caret '^' and newline '\n' as explicit line breaks.
            string[] explicitLines = text.Split(new[] { '^', '\n' }, StringSplitOptions.None);

            foreach (var explicitLine in explicitLines)
            {
                if (explicitLine == "")
                {
                    lines.Add("");
                    continue;
                }

                string[] words = explicitLine.Split(' ');
                string line = "";

                foreach (var word in words)
                {
                    string test = string.IsNullOrEmpty(line) ? word : line + " " + word;
                    if (font.MeasureString(test).X > maxWidth)
                    {
                        if (!string.IsNullOrEmpty(line))
                            lines.Add(line);

                        // If single word itself is longer than the max width, split it character-wise.
                        if (font.MeasureString(word).X > maxWidth)
                        {
                            string partial = "";
                            foreach (char c in word)
                            {
                                string testPartial = partial + c;
                                if (font.MeasureString(testPartial).X > maxWidth)
                                {
                                    if (!string.IsNullOrEmpty(partial))
                                        lines.Add(partial);
                                    partial = c.ToString();
                                }
                                else
                                {
                                    partial = testPartial;
                                }
                            }
                            line = partial;
                        }
                        else
                        {
                            line = word;
                        }
                    }
                    else
                    {
                        line = test;
                    }
                }

                if (!string.IsNullOrEmpty(line))
                    lines.Add(line);
            }

            return lines;
        }

        private List<string> SplitNotificationIntoLines(string text, SpriteFont font, int maxWidth)
        {
            List<string> lines = new();
            // Treat caret '^' and newline '\n' as explicit line breaks.
            string[] explicitLines = text.Split(new[] { '^', '\n' }, StringSplitOptions.None);

            foreach (var explicitLine in explicitLines)
            {
                if (explicitLine == "")
                {
                    lines.Add("");
                    continue;
                }

                string[] words = explicitLine.Split(' ');
                string line = "";

                foreach (var word in words)
                {
                    string test = string.IsNullOrEmpty(line) ? word : line + " " + word;
                    if (font.MeasureString(test).X > maxWidth)
                    {
                        if (!string.IsNullOrEmpty(line))
                            lines.Add(line);

                        if (font.MeasureString(word).X > maxWidth)
                        {
                            string partial = "";
                            foreach (char c in word)
                            {
                                string testPartial = partial + c;
                                if (font.MeasureString(testPartial).X > maxWidth)
                                {
                                    if (!string.IsNullOrEmpty(partial))
                                        lines.Add(partial);
                                    partial = c.ToString();
                                }
                                else
                                {
                                    partial = testPartial;
                                }
                            }
                            line = partial;
                        }
                        else
                        {
                            line = word;
                        }
                    }
                    else
                    {
                        line = test;
                    }
                }

                if (!string.IsNullOrEmpty(line))
                    lines.Add(line);
            }

            return lines;
        }

        public void OpenChat(string npcName)
        {
            selectedNpc = npcName;
            messageHistory = MessageManager.GetMessages(npcName);
            chatScrollOffset = CalculateScrollToBottomOffset(messageHistory);
        }

        public void OpenNotification()
        {
            NotificationManager.resetUnreadNotication();
            notificationHistory = NotificationManager.getNoticationList();
            notificationScrollOffset = 0;
        }

        private int CalculateScrollToBottomOffset(List<string> msgList)
        {
            return Math.Max(0, msgList.Count - 4);
        }


        private void LoadImageAtIndex(int index)
        {
            if (index < 0 || index >= capturedImages.Count)
            {
                currentDisplayedImage = null;
                return;
            }

            using (FileStream stream = new FileStream(capturedImages[index], FileMode.Open))
            {
                currentDisplayedImage = Texture2D.FromStream(Game1.graphics.GraphicsDevice, stream);
                currentDisplayedImage = CropTexture(currentDisplayedImage);
            }
        }

        private void UpdateNpcList()
        {
            messageableNpcList = new List<ClickableComponent>();
            Dictionary<string, long> latestTimestamps = MessageManager.GetLatestAddDictionary();
            string filter = currentMessage?.ToLower() ?? "";

            List<NPC> villagers = Utility.getAllVillagers()
            .Where(n =>
                (n.IsVillager &&
                 n.CanSocialize &&
                 !n.IsMonster &&
                 Game1.player.getFriendshipHeartLevelForNPC(n.Name) >= 1
                 //|| n.Name == "Lewis" || n.Name == "Robin"
                 )
                 && n.Name.ToLower().Contains(filter)
            )
            .OrderByDescending(npc => MessageManager.favouriteNpc.Contains(npc.Name))
            .ThenByDescending(npc => latestTimestamps.TryGetValue(npc.Name, out long ts) ? ts : 0)
            .ThenBy(npc => npc.Name)
            .ToList();

            for (int i = 0; i < villagers.Count; i++)
            {
                messageableNpcList.Add(new ClickableComponent(new Rectangle(0, 0, 0, 0), villagers[i].Name));
            }
        }
        private Texture2D CropTexture(Texture2D source, int x = 0, int y = 0, int width = 520, int height = 705)
        {
            Color[] data = new Color[width * height];
            source.GetData(0, new Rectangle(x, y, width, height), data, 0, data.Length);

            Texture2D cropped = new Texture2D(Game1.graphics.GraphicsDevice, width, height);
            cropped.SetData(data);

            return cropped;
        }

        public void ClosePhoneMenu()
        {
            if (currentApp == "appText" && selectedNpc != null)
            {
                MessageManager.SetUnreadCount(selectedNpc);
                selectedNpc = null;
                currentSuggestion = new("", "");
            }

            if (currentApp != null)
            {
                currentApp = null;
            }

            currentMessage = "";
            exitThisMenu();
        }

    }
}
