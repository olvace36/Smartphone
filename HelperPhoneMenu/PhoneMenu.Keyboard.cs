using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace Smartphone
{
    public partial class PhoneMenu
    {
        // ─────────────────────────────────────────────────────────────
        //  Text Input & Keyboard Logic
        // ─────────────────────────────────────────────────────────────

        private TextBox textBox;
        private PhoneTextInputSubscriber textInputSubscriber;
        private double textCursorBlinkElapsedSeconds = 0d;
        private Task<string> pendingKeyboardTask = null;
        private EditableTextFieldKind pendingKeyboardField = EditableTextFieldKind.None;

        private float backspacePressedTime = 0f;
        private float backspaceRepeatTimer = 0f;
        private const float BackspaceInitialDelay = 400f; // ms
        private const float BackspaceRepeatInterval = 40f;  // ms

        private const int TextUndoHistoryLimit = 128;

        private sealed class TextEditSnapshot
        {
            public string Text { get; init; } = "";
            public int CursorIndex { get; init; }
            public int SelectionAnchorIndex { get; init; }
        }

        private enum EditableTextFieldKind
        {
            None,
            PhotoAlbumName,
            FolderGroupName,
            PhoneAppInput,
            AppLibrarySearch
        }

        private sealed class PhoneTextInputSubscriber : IKeyboardSubscriber
        {
            private readonly PhoneMenu owner;

            public bool Selected { get; set; }

            public PhoneTextInputSubscriber(PhoneMenu owner)
            {
                this.owner = owner;
            }

            public void RecieveTextInput(char inputChar)
            {
                if (!Selected)
                    return;

                owner.TryApplyComposedTextInput(inputChar.ToString());
            }

            public void RecieveTextInput(string text)
            {
                if (!Selected)
                    return;

                owner.TryApplyComposedTextInput(text);
            }

            public void RecieveCommandInput(char command)
            {
                // Command keys are handled through receiveKeyPress to avoid duplicate edits.
            }

            public void RecieveSpecialInput(Keys key)
            {
            }
        }

        partial void InitKeyboardTextInput()
        {
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

            textInputSubscriber = new PhoneTextInputSubscriber(this);
        }

        private EditableTextFieldKind GetActiveEditableTextField()
        {
            if (currentApp == "appPhoto" && photoAlbumCreationOpen)
                return EditableTextFieldKind.PhotoAlbumName;

            if (currentApp == null && layoutManager != null && layoutManager.IsEditingFolderName)
                return EditableTextFieldKind.FolderGroupName;

            if (currentApp == null && layoutManager != null && layoutManager.IsSearchingAppLibrary)
                return EditableTextFieldKind.AppLibrarySearch;

            if (currentApp == "appPhone")
            {
                bool isSearching = phoneAppCurrentTab == 0;
                bool isViewingDetail = phoneAppViewingContactDetail;

                if (isSearching && !isViewingDetail)
                    return EditableTextFieldKind.PhoneAppInput;
            }

            return EditableTextFieldKind.None;
        }

        private void SetPhoneTextInputFocus(bool focused)
        {
            if (Game1.keyboardDispatcher == null)
                return;

            if (focused)
            {
                if (!ReferenceEquals(Game1.keyboardDispatcher.Subscriber, textInputSubscriber))
                    Game1.keyboardDispatcher.Subscriber = textInputSubscriber;

                return;
            }

            if (ReferenceEquals(Game1.keyboardDispatcher.Subscriber, textInputSubscriber))
                Game1.keyboardDispatcher.Subscriber = null;
        }

        private bool IsEditableTextFieldAcceptingComposedInput(EditableTextFieldKind field)
        {
            return field switch
            {
                EditableTextFieldKind.PhotoAlbumName => currentApp == "appPhoto" && photoAlbumCreationOpen,
                EditableTextFieldKind.FolderGroupName => currentApp == null && layoutManager != null && layoutManager.IsEditingFolderName,
                EditableTextFieldKind.AppLibrarySearch => currentApp == null && layoutManager != null && layoutManager.IsSearchingAppLibrary,
                EditableTextFieldKind.PhoneAppInput => currentApp == "appPhone" && !phoneAppViewingContactDetail &&
                    (phoneAppCurrentTab == 0),
                _ => false
            };
        }

        private bool TryApplyComposedTextInput(string? inputText)
        {
            EditableTextFieldKind field = GetActiveEditableTextField();
            if (!IsEditableTextFieldAcceptingComposedInput(field))
                return false;

            string normalizedText = (inputText ?? "").Trim();
            if (normalizedText.Length == 0)
                return false;

            if (!TryApplyEditableTextInsertionToField(field, normalizedText))
                return false;

            Game1.playSound("coin");
            return true;
        }

        private bool TryApplyEditableTextInsertionToField(EditableTextFieldKind field, string insertionText)
        {
            if (string.IsNullOrEmpty(insertionText))
                return false;

            switch (field)
            {
                case EditableTextFieldKind.PhotoAlbumName:
                    ApplyEditableTextInsertion(
                        field,
                        ref photoNewAlbumName,
                        ref photoNewAlbumNameCursorIndex,
                        ref photoNewAlbumNameSelectionAnchorIndex,
                        insertionText);
                    return true;

                case EditableTextFieldKind.FolderGroupName:
                    if (layoutManager != null)
                    {
                        foreach (char c in insertionText)
                        {
                            layoutManager.HandleTextInput(c);
                        }
                    }
                    return true;

                case EditableTextFieldKind.AppLibrarySearch:
                    if (layoutManager != null)
                    {
                        layoutManager.HandleSearchTextInput(insertionText);
                    }
                    return true;

                case EditableTextFieldKind.PhoneAppInput:
                    if (currentApp == "appPhone" && !phoneAppViewingContactDetail)
                    {
                        bool isSearching = phoneAppCurrentTab == 0;

                        if (isSearching)
                        {
                            if (phoneAppSearchQuery.Length < 20)
                            {
                                int available = 20 - phoneAppSearchQuery.Length;
                                string toAdd = insertionText;
                                if (toAdd.Length > available) toAdd = toAdd.Substring(0, available);
                                phoneAppSearchQuery += toAdd;
                                return true;
                            }
                        }
                    }
                    return false;

                default:
                    return false;
            }
        }

        private void UpdateTextInputRepeat(GameTime time)
        {
            Microsoft.Xna.Framework.Input.KeyboardState kbState = Microsoft.Xna.Framework.Input.Keyboard.GetState();

            if (kbState.IsKeyDown(Keys.Back))
            {
                EditableTextFieldKind activeField = GetActiveEditableTextField();
                if (activeField != EditableTextFieldKind.None)
                {
                    backspacePressedTime += (float)time.ElapsedGameTime.TotalMilliseconds;
                    if (backspacePressedTime >= BackspaceInitialDelay)
                    {
                        backspaceRepeatTimer += (float)time.ElapsedGameTime.TotalMilliseconds;
                        if (backspaceRepeatTimer >= BackspaceRepeatInterval)
                        {
                            backspaceRepeatTimer = 0f;
                            PerformBackspace(activeField);
                        }
                    }
                }
                else
                {
                    backspacePressedTime = 0f;
                    backspaceRepeatTimer = 0f;
                }
            }
            else
            {
                backspacePressedTime = 0f;
                backspaceRepeatTimer = 0f;
            }
        }

        private void PerformBackspace(EditableTextFieldKind activeField)
        {
            switch (activeField)
            {
                case EditableTextFieldKind.PhotoAlbumName:
                    if (photoNewAlbumName.Length > 0)
                    {
                        photoNewAlbumName = photoNewAlbumName.Substring(0, photoNewAlbumName.Length - 1);
                        photoNewAlbumNameCursorIndex = Math.Max(0, photoNewAlbumNameCursorIndex - 1);
                        photoNewAlbumNameSelectionAnchorIndex = photoNewAlbumNameCursorIndex;
                        Game1.playSound("hammer");
                    }
                    break;

                case EditableTextFieldKind.FolderGroupName:
                    if (layoutManager != null && layoutManager.IsEditingFolderName && layoutManager.FolderNameBuffer.Length > 0)
                    {
                        string current = layoutManager.FolderNameBuffer;
                        layoutManager.SetFolderNameBuffer(current.Substring(0, current.Length - 1));
                        Game1.playSound("thudStep");
                    }
                    break;

                case EditableTextFieldKind.AppLibrarySearch:
                    if (layoutManager != null && layoutManager.IsSearchingAppLibrary && layoutManager.SearchQueryBuffer.Length > 0)
                    {
                        string current = layoutManager.SearchQueryBuffer;
                        layoutManager.SetSearchQueryBuffer(current.Substring(0, current.Length - 1));
                        Game1.playSound("thudStep");
                    }
                    break;

                case EditableTextFieldKind.PhoneAppInput:
                    if (currentApp == "appPhone" && phoneAppCurrentTab == 0 && !phoneAppViewingContactDetail && phoneAppSearchQuery.Length > 0)
                    {
                        phoneAppSearchQuery = phoneAppSearchQuery.Substring(0, phoneAppSearchQuery.Length - 1);
                        Game1.playSound("thudStep");
                    }
                    break;
            }
        }

        private List<TextEditSnapshot> GetTextUndoHistory(EditableTextFieldKind field)
        {
            return field switch
            {
                EditableTextFieldKind.PhotoAlbumName => photoAlbumNameUndoHistory,
                _ => new()
            };
        }

        private void PushTextUndoSnapshot(EditableTextFieldKind field, string text, int cursorIndex, int selectionAnchorIndex)
        {
            List<TextEditSnapshot> history = GetTextUndoHistory(field);
            history.Add(new TextEditSnapshot
            {
                Text = text ?? "",
                CursorIndex = cursorIndex,
                SelectionAnchorIndex = selectionAnchorIndex
            });

            if (history.Count > TextUndoHistoryLimit)
                history.RemoveAt(0);
        }

        private void TriggerAndroidKeyboard(EditableTextFieldKind field, string currentText)
        {
            if (Constants.TargetPlatform != GamePlatform.Android) return;

            pendingKeyboardField = field;

            try
            {
                Type keyboardInputType = typeof(Microsoft.Xna.Framework.Input.Keyboard).Assembly.GetType("Microsoft.Xna.Framework.Input.KeyboardInput");
                if (keyboardInputType != null)
                {
                    var showMethod = keyboardInputType.GetMethod("Show", new[] { typeof(string), typeof(string), typeof(string), typeof(bool) });
                    if (showMethod != null)
                    {
                        pendingKeyboardTask = (Task<string>)showMethod.Invoke(null, new object[] { ModEntry.SHelper.Translation.Get("ui.photo.keyboard_input_title"), ModEntry.SHelper.Translation.Get("ui.photo.enter_text"), currentText, false });
                    }
                }
            }
            catch (Exception)
            {
                pendingKeyboardTask = null;
                pendingKeyboardField = EditableTextFieldKind.None;
            }
        }

        private void UpdateAndroidKeyboard()
        {
            if (pendingKeyboardTask != null && pendingKeyboardTask.IsCompleted)
            {
                if (!pendingKeyboardTask.IsFaulted && pendingKeyboardTask.Result != null)
                {
                    string result = pendingKeyboardTask.Result;
                    EditableTextFieldKind field = pendingKeyboardField;
                    SetEditableTextFieldState(field, result, result.Length, result.Length, clearUndoHistory: false);
                }
                pendingKeyboardTask = null;
                pendingKeyboardField = EditableTextFieldKind.None;
            }
        }

        private bool HandleAndroidKeyboardTap(int x, int y)
        {
            if (Constants.TargetPlatform != GamePlatform.Android)
                return false;

            // 1. Photos App Album Naming
            if (currentApp == "appPhoto" && photoAlbumCreationOpen)
            {
                if (photoAlbumInputAreaBounds.Contains(x, y))
                {
                    TriggerAndroidKeyboard(EditableTextFieldKind.PhotoAlbumName, photoNewAlbumName);
                    return true;
                }
            }

            // 2. Folder Rename Box
            if (currentApp == null && layoutManager != null && layoutManager.IsReorderMode)
            {
                if (layoutManager.FolderRenameBoxBounds.Contains(x, y))
                {
                    TriggerAndroidKeyboard(EditableTextFieldKind.FolderGroupName, layoutManager.FolderNameBuffer);
                    return true;
                }
            }

            // 3. App Library Search Box
            if (currentApp == null && layoutManager != null)
            {
                if (layoutManager.SearchBoxBounds.Contains(x, y))
                {
                    TriggerAndroidKeyboard(EditableTextFieldKind.AppLibrarySearch, layoutManager.SearchQueryBuffer);
                    return true;
                }
            }

            // 4. Contacts App Search Box
            if (currentApp == "appPhone" && phoneAppCurrentTab == 0 && !phoneAppViewingContactDetail)
            {
                Rectangle bounds = GetPhoneContentBounds();
                Rectangle searchBoxRect = new Rectangle(bounds.X + ScaleUiValue(15), bounds.Y + ScaleUiValue(10), bounds.Width - ScaleUiValue(30), ScaleUiValue(60));
                if (searchBoxRect.Contains(x, y))
                {
                    TriggerAndroidKeyboard(EditableTextFieldKind.PhoneAppInput, phoneAppSearchQuery);
                    return true;
                }
            }

            return false;
        }

        private void ClearTextUndoHistory(EditableTextFieldKind field)
        {
            GetTextUndoHistory(field).Clear();
        }

        private void SetEditableTextFieldState(
            EditableTextFieldKind field,
            string text,
            int cursorIndex,
            int selectionAnchorIndex,
            bool clearUndoHistory = true)
        {
            string safeText = text ?? "";
            int safeCursorIndex = Math.Clamp(cursorIndex, 0, safeText.Length);
            int safeSelectionAnchorIndex = Math.Clamp(selectionAnchorIndex, 0, safeText.Length);

            switch (field)
            {
                case EditableTextFieldKind.PhotoAlbumName:
                    photoNewAlbumName = safeText;
                    photoNewAlbumNameCursorIndex = safeCursorIndex;
                    photoNewAlbumNameSelectionAnchorIndex = safeSelectionAnchorIndex;
                    break;

                case EditableTextFieldKind.FolderGroupName:
                    if (layoutManager != null)
                    {
                        layoutManager.SetFolderNameBuffer(safeText);
                    }
                    break;

                case EditableTextFieldKind.AppLibrarySearch:
                    if (layoutManager != null)
                    {
                        layoutManager.SetSearchQueryBuffer(safeText);
                    }
                    break;
            }

            if (clearUndoHistory)
                ClearTextUndoHistory(field);
        }

        private void ResetEditableTextFieldState(EditableTextFieldKind field, bool clearUndoHistory = true)
        {
            SetEditableTextFieldState(field, "", 0, 0, clearUndoHistory);
        }

        private static (int Start, int End) GetSelectionRange(int cursorIndex, int selectionAnchorIndex, int textLength)
        {
            int safeCursorIndex = Math.Clamp(cursorIndex, 0, textLength);
            int safeSelectionAnchorIndex = Math.Clamp(selectionAnchorIndex, 0, textLength);
            return safeCursorIndex < safeSelectionAnchorIndex
                ? (safeCursorIndex, safeSelectionAnchorIndex)
                : (safeSelectionAnchorIndex, safeCursorIndex);
        }

        private void ApplyEditableTextInsertion(
            EditableTextFieldKind field,
            ref string text,
            ref int cursorIndex,
            ref int selectionAnchorIndex,
            string insertionText)
        {
            string safeText = text ?? "";
            string safeInsertionText = insertionText ?? "";
            int safeCursorIndex = Math.Clamp(cursorIndex, 0, safeText.Length);
            int safeSelectionAnchorIndex = Math.Clamp(selectionAnchorIndex, 0, safeText.Length);
            (int selectionStart, int selectionEnd) = GetSelectionRange(safeCursorIndex, safeSelectionAnchorIndex, safeText.Length);

            PushTextUndoSnapshot(field, safeText, safeCursorIndex, safeSelectionAnchorIndex);

            if (selectionStart != selectionEnd)
                safeText = safeText.Remove(selectionStart, selectionEnd - selectionStart);

            int insertIndex = selectionStart;
            safeText = safeText.Insert(insertIndex, safeInsertionText);

            text = safeText;
            cursorIndex = insertIndex + safeInsertionText.Length;
            selectionAnchorIndex = cursorIndex;
        }
    }
}
