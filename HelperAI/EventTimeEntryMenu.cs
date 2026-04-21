using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace Smartphone
{
    public partial class ModEntry
    {
        private sealed class EventTimeEntryMenu : IClickableMenu
        {
            private readonly Action<string> onConfirm;
            private readonly Action onCancel;
            private readonly string npcDisplayName;
            private readonly string eventDisplayName;

            private readonly TextBox timeTextBox;
            private readonly Rectangle textBoxBounds;
            private readonly Rectangle minusButtonBounds;
            private readonly Rectangle plusButtonBounds;
            private readonly Rectangle okButtonBounds;
            private readonly Rectangle cancelButtonBounds;

            private string validationMessage = string.Empty;

            public EventTimeEntryMenu(
                string npcDisplayName,
                string eventDisplayName,
                Action<string> onConfirm,
                Action onCancel)
                : base(Game1.uiViewport.Width / 2 - 260, Game1.uiViewport.Height / 2 - 170, 520, 340, showUpperRightCloseButton: false)
            {
                this.onConfirm = onConfirm ?? (_ => { });
                this.onCancel = onCancel ?? (() => { });
                this.npcDisplayName = string.IsNullOrWhiteSpace(npcDisplayName) ? "NPC" : npcDisplayName;
                this.eventDisplayName = string.IsNullOrWhiteSpace(eventDisplayName) ? "event" : eventDisplayName;

                textBoxBounds = new Rectangle(xPositionOnScreen + 105, yPositionOnScreen + 150, 200, 48);
                minusButtonBounds = new Rectangle(xPositionOnScreen + 45, yPositionOnScreen + 150, 48, 48);
                plusButtonBounds = new Rectangle(xPositionOnScreen + 317, yPositionOnScreen + 150, 48, 48);
                okButtonBounds = new Rectangle(xPositionOnScreen + 105, yPositionOnScreen + 245, 120, 56);
                cancelButtonBounds = new Rectangle(xPositionOnScreen + 245, yPositionOnScreen + 245, 120, 56);

                timeTextBox = new TextBox(
                    Game1.content.Load<Texture2D>("LooseSprites\\textBox"),
                    null,
                    Game1.smallFont,
                    Game1.textColor)
                {
                    X = textBoxBounds.X,
                    Y = textBoxBounds.Y,
                    Width = textBoxBounds.Width,
                    Height = textBoxBounds.Height,
                    Text = BuildDefaultSuggestedTime()
                };
                timeTextBox.textLimit = 5;
                timeTextBox.Selected = true;
                Game1.keyboardDispatcher.Subscriber = timeTextBox;
            }

            public override void update(GameTime time)
            {
                base.update(time);
                timeTextBox.Update();
            }

            public override void receiveLeftClick(int x, int y, bool playSound = true)
            {
                if (okButtonBounds.Contains(x, y))
                {
                    TrySubmit();
                    return;
                }

                if (cancelButtonBounds.Contains(x, y))
                {
                    CancelAndClose();
                    return;
                }

                if (minusButtonBounds.Contains(x, y))
                {
                    AdjustTimeByMinutes(-10);
                    Game1.playSound("smallSelect");
                    FocusTextBox();
                    return;
                }

                if (plusButtonBounds.Contains(x, y))
                {
                    AdjustTimeByMinutes(10);
                    Game1.playSound("smallSelect");
                    FocusTextBox();
                    return;
                }

                bool clickedTextBox = textBoxBounds.Contains(x, y);
                timeTextBox.Selected = clickedTextBox;
                if (clickedTextBox)
                {
                    Game1.keyboardDispatcher.Subscriber = timeTextBox;
                }
                else if (Game1.keyboardDispatcher.Subscriber == timeTextBox)
                {
                    Game1.keyboardDispatcher.Subscriber = null;
                }
            }

            public override void receiveKeyPress(Keys key)
            {
                if (key == Keys.Enter)
                {
                    TrySubmit();
                    return;
                }

                if (key == Keys.Escape)
                {
                    CancelAndClose();
                    return;
                }

                if (key == Keys.OemPlus || key == Keys.Add)
                {
                    AdjustTimeByMinutes(10);
                    Game1.playSound("smallSelect");
                    return;
                }

                if (key == Keys.OemMinus || key == Keys.Subtract)
                {
                    AdjustTimeByMinutes(-10);
                    Game1.playSound("smallSelect");
                    return;
                }

                base.receiveKeyPress(key);
            }

            public override void receiveRightClick(int x, int y, bool playSound = true)
            {
                CancelAndClose();
            }

            public override void draw(SpriteBatch b)
            {
                Game1.drawDialogueBox(xPositionOnScreen, yPositionOnScreen, width, height, false, true);

                string title = $"Schedule {eventDisplayName} with {npcDisplayName}";
                Vector2 titleSize = Game1.dialogueFont.MeasureString(title);
                Vector2 titlePosition = new Vector2(xPositionOnScreen + (width - titleSize.X) / 2f, yPositionOnScreen + 48f);
                Utility.drawTextWithShadow(b, title, Game1.dialogueFont, titlePosition, Game1.textColor);

                Utility.drawTextWithShadow(
                    b,
                    "Enter HHMM (24h)",
                    Game1.smallFont,
                    new Vector2(xPositionOnScreen + 45, yPositionOnScreen + 108),
                    Game1.textColor);

                DrawButton(b, minusButtonBounds, "-", new Color(235, 235, 235));
                DrawButton(b, plusButtonBounds, "+", new Color(235, 235, 235));

                IClickableMenu.drawTextureBox(
                    b,
                    Game1.menuTexture,
                    new Rectangle(0, 256, 60, 60),
                    textBoxBounds.X,
                    textBoxBounds.Y - 6,
                    textBoxBounds.Width + 16,
                    textBoxBounds.Height + 12,
                    Color.White,
                    1f,
                    false);
                timeTextBox.Draw(b);

                string digitsOnly = new string((timeTextBox.Text ?? string.Empty).Where(char.IsDigit).ToArray());
                if (TryNormalizeEventTime(digitsOnly, out string normalizedPreviewTime))
                {
                    Utility.drawTextWithShadow(
                        b,
                        $"Selected: {FormatEventTimeForDisplay(normalizedPreviewTime)}",
                        Game1.smallFont,
                        new Vector2(xPositionOnScreen + 45, yPositionOnScreen + 212),
                        Game1.textColor);
                }
                else
                {
                    Utility.drawTextWithShadow(
                        b,
                        "Selected: Invalid time",
                        Game1.smallFont,
                        new Vector2(xPositionOnScreen + 45, yPositionOnScreen + 212),
                        Color.IndianRed);
                }

                DrawButton(b, okButtonBounds, "OK", new Color(196, 236, 196));
                DrawButton(b, cancelButtonBounds, "Cancel", new Color(242, 218, 218));

                if (!string.IsNullOrWhiteSpace(validationMessage))
                {
                    Utility.drawTextWithShadow(
                        b,
                        validationMessage,
                        Game1.smallFont,
                        new Vector2(xPositionOnScreen + 45, yPositionOnScreen + 305),
                        Color.IndianRed);
                }

                drawMouse(b);
            }

            private static void DrawButton(SpriteBatch b, Rectangle bounds, string text, Color boxColor)
            {
                IClickableMenu.drawTextureBox(
                    b,
                    Game1.menuTexture,
                    new Rectangle(0, 256, 60, 60),
                    bounds.X,
                    bounds.Y,
                    bounds.Width,
                    bounds.Height,
                    boxColor,
                    1f,
                    false);

                Vector2 textSize = Game1.smallFont.MeasureString(text);
                Vector2 textPosition = new Vector2(
                    bounds.X + (bounds.Width - textSize.X) / 2f,
                    bounds.Y + (bounds.Height - textSize.Y) / 2f + 2f);

                Utility.drawTextWithShadow(b, text, Game1.smallFont, textPosition, Game1.textColor);
            }

            private static string BuildDefaultSuggestedTime()
            {
                int currentTime = Math.Clamp(Game1.timeOfDay, 600, 2300);
                int hour = currentTime / 100;
                int minute = currentTime % 100;

                minute = ((minute + 9) / 10) * 10;
                if (minute >= 60)
                {
                    hour++;
                    minute = 0;
                }

                int suggestedTime = (hour * 100) + minute;
                if (suggestedTime > 2300)
                    suggestedTime = 2300;

                if (!TryNormalizeEventTime($"{suggestedTime:0000}", out string normalized))
                    normalized = "1200";

                return normalized;
            }

            private void FocusTextBox()
            {
                timeTextBox.Selected = true;
                Game1.keyboardDispatcher.Subscriber = timeTextBox;
            }

            private void CancelAndClose()
            {
                if (Game1.keyboardDispatcher.Subscriber == timeTextBox)
                    Game1.keyboardDispatcher.Subscriber = null;

                timeTextBox.Selected = false;
                Game1.playSound("bigDeSelect");
                onCancel();
            }

            private void TrySubmit()
            {
                validationMessage = string.Empty;
                string digitsOnly = new string((timeTextBox.Text ?? string.Empty).Where(char.IsDigit).ToArray());
                if (!TryNormalizeEventTime(digitsOnly, out string normalizedEventTime))
                {
                    validationMessage = "Please enter a valid time in HHMM between 0600 and 2300.";
                    Game1.playSound("cancel");
                    return;
                }

                if (int.TryParse(normalizedEventTime, out int selectedTime)
                    && TryNormalizeEventTime($"{Math.Clamp(Game1.timeOfDay, 600, 2300):0000}", out string minAllowed)
                    && int.TryParse(minAllowed, out int minimumTime)
                    && selectedTime < minimumTime)
                {
                    validationMessage = $"Choose now or later ({FormatEventTimeForDisplay(minAllowed)}).";
                    Game1.playSound("cancel");
                    return;
                }

                if (Game1.keyboardDispatcher.Subscriber == timeTextBox)
                    Game1.keyboardDispatcher.Subscriber = null;

                timeTextBox.Selected = false;
                Game1.playSound("smallSelect");
                onConfirm(normalizedEventTime);
            }

            private void AdjustTimeByMinutes(int deltaMinutes)
            {
                string digitsOnly = new string((timeTextBox.Text ?? string.Empty).Where(char.IsDigit).ToArray());
                if (!TryNormalizeEventTime(digitsOnly, out string normalizedCurrentTime))
                    normalizedCurrentTime = BuildDefaultSuggestedTime();

                if (!int.TryParse(normalizedCurrentTime, out int currentTime))
                    currentTime = 1200;

                int currentTotalMinutes = ((currentTime / 100) * 60) + (currentTime % 100);
                int adjustedTotalMinutes = Math.Clamp(currentTotalMinutes + deltaMinutes, 6 * 60, 23 * 60);
                adjustedTotalMinutes = (adjustedTotalMinutes / 10) * 10;

                int adjustedTime = ((adjustedTotalMinutes / 60) * 100) + (adjustedTotalMinutes % 60);
                timeTextBox.Text = $"{adjustedTime:0000}";
                validationMessage = string.Empty;
            }

        }



        public static void OpenScheduleEventTimeMenu(
        string eventNpcName,
        string eventType,
        string eventDisplayName,
        string npcDisplayName,
        string? npcResponseTemplate,
        bool addNpcConfirmationMessage = true)
        {
            Game1.activeClickableMenu = new EventTimeEntryMenu(
                npcDisplayName,
                eventDisplayName,
                onConfirm: normalizedEventTime =>
                {
                    bool isNewSchedule = TryAddPendingUnlimitedEvent(eventNpcName, eventType, normalizedEventTime);

                    Game1.activeClickableMenu = null;

                    if (addNpcConfirmationMessage)
                    {
                        string finalNpcResponse = BuildScheduledEventNpcResponse(npcResponseTemplate, normalizedEventTime);
                        MessageManager.AddMessage(eventNpcName, $"{npcDisplayName}: {finalNpcResponse}");
                    }
                    else
                    {
                        string displayTime = FormatEventTimeForDisplay(normalizedEventTime);
                        string feedback = isNewSchedule
                            ? $"Scheduled {eventDisplayName} with {npcDisplayName} at {displayTime}."
                            : $"{eventDisplayName} with {npcDisplayName} at {displayTime} is already scheduled.";
                        Game1.addHUDMessage(new HUDMessage(feedback, isNewSchedule ? 2 : 3));
                    }
                },
                onCancel: () =>
                {
                    Game1.activeClickableMenu = null;
                });
        }

        internal static bool TryOpenManualScheduleEventTimeMenu(string npcName, string eventType)
        {
            NPC? npc = Game1.getCharacterFromName(npcName, mustBeVillager: false);
            if (npc is null)
                return false;

            string normalizedNpcName = (npcName ?? string.Empty).Trim();
            string normalizedEventType = (eventType ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(normalizedNpcName) || string.IsNullOrWhiteSpace(normalizedEventType))
                return false;

            if (!TryGetRegisteredUnlimitedEvent(normalizedEventType, out RegisteredUnlimitedEvent? registeredEvent) || registeredEvent == null)
                return false;

            string npcDisplayName = string.IsNullOrWhiteSpace(npc.displayName)
                ? normalizedNpcName
                : npc.displayName;

            OpenScheduleEventTimeMenu(
                normalizedNpcName,
                registeredEvent.EventType,
                registeredEvent.DisplayName,
                npcDisplayName,
                npcResponseTemplate: null,
                addNpcConfirmationMessage: false);

            return true;
        }

        private static bool TryAddPendingUnlimitedEvent(string npcName, string eventType, string normalizedEventTime)
        {
            var pendingEvent = (npcName.Trim(), eventType.Trim(), normalizedEventTime.Trim());
            if (PendingUnlimitedEvents.Contains(pendingEvent))
                return false;

            PendingUnlimitedEvents.Add(pendingEvent);
            return true;
        }

        private static string BuildScheduledEventNpcResponse(string? npcResponseTemplate, string normalizedEventTime)
        {
            string displayTime = FormatEventTimeForDisplay(normalizedEventTime);

            string response = (npcResponseTemplate ?? string.Empty)
                .Replace("{{time}}", displayTime, StringComparison.OrdinalIgnoreCase)
                .Replace("{time}", displayTime, StringComparison.OrdinalIgnoreCase)
                .Trim();

            if (string.IsNullOrWhiteSpace(response))
                return $"I will see you at {displayTime}.";

            return response;
        }

        private static string FormatEventTimeForDisplay(string normalizedEventTime)
        {
            if (!int.TryParse(normalizedEventTime, out int parsedTime))
                return normalizedEventTime ?? string.Empty;

            int hour24 = (parsedTime / 100) % 24;
            int minute = parsedTime % 100;

            string period = hour24 >= 12 ? "PM" : "AM";
            int hour12 = hour24 % 12;
            if (hour12 == 0)
                hour12 = 12;

            return $"{hour12}:{minute:00} {period}";
        }

        public static string BuildResponseConversationUserInput(NPC npc, string fallbackText)
        {
            if (npc == null || string.IsNullOrWhiteSpace(npc.Name))
                return (fallbackText ?? string.Empty).Trim();

            var messagesHistory = npcMessagesToday.ContainsKey(npc.Name)
                ? npcMessagesToday[npc.Name]
                : new List<string>();
            string conversation = BuildConversationPreviewForAi(messagesHistory, npc.Name, maxLines: 12);

            if (!string.IsNullOrWhiteSpace(conversation))
                return conversation;

            return (fallbackText ?? string.Empty).Trim();
        }
    }
}
