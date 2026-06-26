using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Newtonsoft.Json;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace Smartphone
{
    // ──────────────────────────────────────────────────────────────────────────
    // Data structures
    // ──────────────────────────────────────────────────────────────────────────

    public class LayoutItem
    {
        public string AppId { get; set; } = string.Empty;
        public AppSize Size { get; set; } = AppSize.Size1x1;
        public int GridCol { get; set; } = -1;
        public int GridRow { get; set; } = -1;
        public string FolderName { get; set; } = string.Empty;
        public List<string> FolderItems { get; set; } = new();

        public bool IsFolder => AppId == PhoneAppLayoutManager.FolderAppId;
    }

    internal class PhoneLayoutData
    {
        public List<List<LayoutItem>> Pages { get; set; } = new();
        public List<LayoutItem> Main { get; set; } = new();
        public List<string> Dock { get; set; } = new();
    }

    internal enum DropdownOption
    {
        ChangeSize1x1,
        ChangeSize2x1,
        ChangeSize2x2,
        ChangeSize3x2,
        ChangeSize4x2,
        ChangeSize4x3,
        ChangeSize4x4,
        SelectTheme
    }

    internal sealed class PhoneAppLayoutManager
    {
        public const string FolderAppId = "__folder__";

        // Grid Geometry
        private const int GridCols = 4;
        private const int GridRows = 6;
        private const int GridCellSize = 114;
        private const int GridStartX = 32;
        private const int GridStartY = 3;
        private const int GridPadding = 0;

        // Dock Geometry
        private const int DockCellSize = 96;
        private const int DockPadding = 8;
        private const int DockCols = 4;
        private const int DockStartY = 708;

        private const int GridIconPadding = 14;
        private const int DockIconPadding = 4;

        private const float JiggleAmplitude = 1.2f;
        private const float JiggleFrequency = 6f;

        // UI Header Controls
        private const int DoneButtonX = 350;
        private const int DoneButtonY = 0;
        private const int DoneButtonW = 115;
        private const int DoneButtonH = 36;

        private const int ResetButtonX = 55;
        private const int ResetButtonY = 0;
        private const int ResetButtonW = 115;
        private const int ResetButtonH = 36;

        private readonly PhoneMenu _menu;

        private List<List<LayoutItem>> _pages = new();
        private List<string> _dock = new();

        public bool IsReorderMode { get; private set; }
        private double _jiggleElapsed;

        // Drag States
        private bool _isDragging;
        private string? _dragAppId;
        private int _dragSourcePage;
        private int _dragSourceIndex;
        private bool _dragFromDock;
        private AppSize _dragAppSize;
        private int _dragMouseX, _dragMouseY;
        private LayoutItem? _draggedItem;

        // theme option
        private bool _isDropdownShowingThemes;

        private bool _hoverIsOnIcon;
        private bool _hoverIsOnGap;
        private int _hoverCellCol = -1;
        private int _hoverCellRow = -1;
        private int _hoverMainIndex = -1;
        private int _hoverDockIndex = -1;

        private double _pageBoundaryHoverTimer;
        private int _lastPageBoundaryDir;

        private double _folderPageHoverTimer;
        private int _lastFolderPageDir;

        private bool _isDragCandidate;
        private bool _dragStarted;
        private double _holdTimer;
        private string? _clickedAppId;
        private bool _clickedIsDock;
        private int _clickedIndex;
        private int _clickStartX;
        private int _clickStartY;
        private AppSize _clickedAppSize;

        private bool _dropdownOpen;
        private string? _dropdownAppId;
        private bool _dropdownForDock;
        private int _dropdownForMainIndex = -1;
        private List<(DropdownOption option, Rectangle bounds, string label)> _dropdownItems = new();

        private LayoutItem? _openFolder;
        private int _openFolderIndex = -1;
        private int _currentFolderPage;
        private bool _isEditingFolderName;
        private string _folderNameBuffer = string.Empty;
        private List<Rectangle> _folderItemBounds = new();
        private Rectangle _folderOverlayBounds;
        private Rectangle _folderRenameBoxBounds;

        private Rectangle _doneButtonBounds;
        private Rectangle _resetButtonBounds;
        private int _currentPage;

        private readonly Dictionary<string, Rectangle> _mainClickBounds = new();
        private readonly Dictionary<int, Rectangle> _dockClickBounds = new();
        private Rectangle _prevPageBounds = Rectangle.Empty;
        private Rectangle _nextPageBounds = Rectangle.Empty;

        public PhoneAppLayoutManager(PhoneMenu menu)
        {
            _menu = menu;
        }

        public void EnterReorderMode()
        {
            IsReorderMode = true;
            _jiggleElapsed = 0;
            _isDragging = false;
            _dropdownOpen = false;
            _openFolder = null;
            _isEditingFolderName = false;
            _isDropdownShowingThemes = false;
        }

        public void ExitReorderMode()
        {
            IsReorderMode = false;
            _isDragging = false;
            _dropdownOpen = false;
            _openFolder = null;
            _isEditingFolderName = false;
            SaveLayout();
        }

        public int CurrentPage => _currentPage;

        public void Update(GameTime time)
        {
            if (IsReorderMode)
                _jiggleElapsed += time.ElapsedGameTime.TotalSeconds;

            if (_isDragCandidate && !_isDragging)
            {
                _holdTimer += time.ElapsedGameTime.TotalSeconds;
                if (_holdTimer > 1.0)
                {
                    _holdTimer = 0;
                    if (!IsReorderMode)
                    {
                        EnterReorderMode();
                    }
                    _dragStarted = true;
                    Rectangle itemBounds = Rectangle.Empty;
                    if (_openFolder != null)
                    {
                        int relIndex = _clickedIndex - (_currentFolderPage * 9);
                        if (relIndex >= 0 && relIndex < _folderItemBounds.Count)
                            itemBounds = _folderItemBounds[relIndex];
                    }
                    else if (_clickedIsDock)
                    {
                        _dockClickBounds.TryGetValue(_clickedIndex, out itemBounds);
                    }
                    else
                    {
                        _mainClickBounds.TryGetValue(_clickedAppId + "_" + _clickedIndex, out itemBounds);
                    }

                    if (itemBounds != Rectangle.Empty)
                    {
                        int mx = Game1.getMouseX();
                        int my = Game1.getMouseY();
                        BeginDrag(_clickedAppId!, _clickedAppSize, _clickedIsDock ? -1 : _currentPage, _clickedIndex, _clickedIsDock, mx, my, itemBounds);
                        _dragMouseX = mx;
                        _dragMouseY = my;
                    }
                }
            }
            else
            {
                _holdTimer = 0;
            }

            if (_isDragging)
            {
                Rectangle contentBounds = _menu.GetPhoneContentBounds();
                int borderTolerance = ScaleUi(35);

                if (_openFolder != null)
                {
                    _folderOverlayBounds = new Rectangle(contentBounds.X + ScaleUi(40), contentBounds.Y + ScaleUi(140), ScaleUi(440), ScaleUi(440));
                    int folderTotalPages = (int)Math.Ceiling(_openFolder.FolderItems.Count / 9.0);

                    if (_dragMouseY >= _folderOverlayBounds.Top && _dragMouseY <= _folderOverlayBounds.Bottom)
                    {
                        if (_dragMouseX < _folderOverlayBounds.Left + ScaleUi(40) && _currentFolderPage > 0)
                        {
                            UpdateFolderPageBoundaryHover(time, -1, folderTotalPages);
                        }
                        else if (_dragMouseX > _folderOverlayBounds.Right - ScaleUi(40) && _currentFolderPage < folderTotalPages)
                        {
                            UpdateFolderPageBoundaryHover(time, 1, folderTotalPages);
                        }
                        else
                        {
                            _folderPageHoverTimer = 0;
                            _lastFolderPageDir = 0;
                        }
                    }
                    else
                    {
                        _folderPageHoverTimer = 0;
                        _lastFolderPageDir = 0;
                    }
                }
                else
                {
                    if (_dragMouseX < contentBounds.Left + borderTolerance && _currentPage > 0)
                    {
                        UpdatePageBoundaryHover(time, -1);
                    }
                    else if (_dragMouseX > contentBounds.Right - borderTolerance)
                    {
                        UpdatePageBoundaryHover(time, 1);
                    }
                    else
                    {
                        _pageBoundaryHoverTimer = 0;
                        _lastPageBoundaryDir = 0;
                    }
                }
            }
        }

        private void UpdatePageBoundaryHover(GameTime time, int direction)
        {
            if (_lastPageBoundaryDir != direction)
            {
                _lastPageBoundaryDir = direction;
                _pageBoundaryHoverTimer = 0;
            }

            _pageBoundaryHoverTimer += time.ElapsedGameTime.TotalSeconds;
            if (_pageBoundaryHoverTimer >= 1.0)
            {
                _pageBoundaryHoverTimer = 0;
                if (direction == 1 && _currentPage == _pages.Count - 1)
                {
                    _pages.Add(new List<LayoutItem>());
                }
                _currentPage = Math.Clamp(_currentPage + direction, 0, _pages.Count - 1);
                Game1.playSound("shwip");
            }
        }

        private void UpdateFolderPageBoundaryHover(GameTime time, int direction, int maxPages)
        {
            if (_lastFolderPageDir != direction)
            {
                _lastFolderPageDir = direction;
                _folderPageHoverTimer = 0;
            }

            _folderPageHoverTimer += time.ElapsedGameTime.TotalSeconds;
            if (_folderPageHoverTimer >= 1.0)
            {
                _folderPageHoverTimer = 0;
                int allowedMax = direction == 1 ? maxPages : maxPages - 1;
                _currentFolderPage = Math.Clamp(_currentFolderPage + direction, 0, allowedMax);
                Game1.playSound("shwip");
            }
        }

        public void DrawHomeScreen(SpriteBatch b)
        {
            _mainClickBounds.Clear();
            _dockClickBounds.Clear();
            _prevPageBounds = Rectangle.Empty;
            _nextPageBounds = Rectangle.Empty;

            List<HomeAppEntryProxy> allApps = BuildAllAppsSnapshot();
            SyncLayoutWithApps(allApps);

            DrawDock(b, allApps);

            if (IsReorderMode && _openFolder == null)
            {
                DrawEmptyGridPlaceholders(b);
            }

            if (_currentPage >= 0 && _currentPage < _pages.Count)
                DrawPage(b, _pages[_currentPage], allApps);

            DrawPageIndicator(b);

            if (IsReorderMode && _openFolder == null)
            {
                DrawDoneButton(b);
                DrawResetButton(b);
                if (_isDragging)
                    DrawDragGhost(b, allApps);
            }

            if (_openFolder != null)
                DrawFolderOverlay(b, allApps);

            if (_dropdownOpen)
                DrawDropdown(b);
        }

        private void DrawEmptyGridPlaceholders(SpriteBatch b)
        {
            for (int col = 0; col < GridCols; col++)
            {
                for (int row = 0; row < GridRows; row++)
                {
                    Rectangle r = GetMainCellRect(col, row, 1, 1);
                    b.Draw(Game1.staminaRect, r, Color.White * 0.04f);

                    if (_hoverCellCol == col && _hoverCellRow == row && !_hoverIsOnIcon)
                    {
                        b.Draw(Game1.staminaRect, r, Color.LightGreen * 0.25f);
                    }
                }
            }
        }

        public bool ReceiveLeftClick(int x, int y)
        {
            if (IsReorderMode && _doneButtonBounds.Contains(x, y) && _openFolder == null)
            {
                Game1.playSound("smallSelect");
                ExitReorderMode();
                return true;
            }

            if (IsReorderMode && _resetButtonBounds.Contains(x, y) && _openFolder == null)
            {
                Game1.playSound("trashcan");
                BuildDefaultLayout(BuildAllAppsSnapshot());
                SaveLayout();
                _menu.ResetPhoneBackgroundToDefault();
                return true;
            }

            if (_dropdownOpen)
            {
                if (_dropdownItems.Any(item => item.bounds.Contains(x, y)))
                {
                    HandleDropdownClick(x, y);
                }
                else
                {
                    // Check if the user is clicking the EXACT same app icon that the dropdown is open for
                    if (_dropdownForMainIndex >= 0 && _currentPage >= 0 && _currentPage < _pages.Count)
                    {
                        var page = _pages[_currentPage];
                        if (_dropdownForMainIndex < page.Count)
                        {
                            var item = page[_dropdownForMainIndex];
                            // Check if the cursor coordinates are inside the app icon's bounding hitbox
                            if (_mainClickBounds.TryGetValue(item.AppId + "_" + _dropdownForMainIndex, out Rectangle appBounds) && appBounds.Contains(x, y))
                            {
                                // If we aren't already viewing themes, transition to the texture selection view
                                if (!_isDropdownShowingThemes)
                                {
                                    _isDropdownShowingThemes = true;
                                    List<HomeAppEntryProxy> allApps = BuildAllAppsSnapshot();
                                    HomeAppEntryProxy? app = FindApp(allApps, item.AppId);

                                    BuildThemeDropdownItems(appBounds, app);
                                    Game1.playSound("smallSelect");
                                    return true; // Consume the click and remain open
                                }
                            }
                        }
                    }

                    // Otherwise, clicking completely outside closes the menu entirely
                    _dropdownOpen = false;
                    _isDropdownShowingThemes = false;
                }
                return true;
            }

            if (_openFolder != null)
            {
                if (_isEditingFolderName && !_folderRenameBoxBounds.Contains(x, y))
                {
                    _isEditingFolderName = false;
                    _openFolder.FolderName = string.IsNullOrWhiteSpace(_folderNameBuffer) ? "Folder" : _folderNameBuffer;
                    SaveLayout();
                }

                if (_folderRenameBoxBounds.Contains(x, y) && IsReorderMode)
                {
                    _isEditingFolderName = true;
                    _folderNameBuffer = _openFolder.FolderName;
                    return true;
                }

                for (int i = 0; i < _folderItemBounds.Count; i++)
                {
                    if (_folderItemBounds[i].Contains(x, y))
                    {
                        int targetGlobalIdx = (_currentFolderPage * 9) + i;
                        if (targetGlobalIdx >= _openFolder.FolderItems.Count) continue;

                        string fid = _openFolder.FolderItems[targetGlobalIdx];
                        if (string.IsNullOrEmpty(fid)) continue;

                        _isDragCandidate = true;
                        _clickedAppId = fid;
                        _clickedIsDock = false;
                        _clickedIndex = targetGlobalIdx;
                        _clickStartX = x;
                        _clickStartY = y;
                        _clickedAppSize = AppSize.Size1x1;
                        return true;
                    }
                }

                if (!_folderOverlayBounds.Contains(x, y) && !_isDragging)
                {
                    if (_isEditingFolderName)
                    {
                        _openFolder.FolderName = string.IsNullOrWhiteSpace(_folderNameBuffer) ? "Folder" : _folderNameBuffer;
                        SaveLayout();
                    }
                    _openFolder = null;
                    _openFolderIndex = -1;
                    _isEditingFolderName = false;
                    Game1.playSound("bigDeSelect");
                }
                return true;
            }

            if (_prevPageBounds.Contains(x, y) && _currentPage > 0)
            {
                _currentPage--;
                Game1.playSound("shwip");
                return true;
            }
            if (_nextPageBounds.Contains(x, y) && _currentPage < _pages.Count - 1)
            {
                _currentPage++;
                Game1.playSound("shwip");
                return true;
            }

            _isDragCandidate = false;
            _dragStarted = false;

            foreach (KeyValuePair<int, Rectangle> kv in _dockClickBounds)
            {
                if (!kv.Value.Contains(x, y)) continue;
                string appId = kv.Key < _dock.Count ? _dock[kv.Key] : string.Empty;
                if (string.IsNullOrEmpty(appId)) return true;

                _isDragCandidate = true;
                _clickedAppId = appId;
                _clickedIsDock = true;
                _clickedIndex = kv.Key;
                _clickStartX = x;
                _clickStartY = y;
                _clickedAppSize = AppSize.Size1x1;
                return true;
            }

            if (_currentPage >= 0 && _currentPage < _pages.Count)
            {
                List<LayoutItem> page = _pages[_currentPage];
                for (int i = 0; i < page.Count; i++)
                {
                    LayoutItem item = page[i];
                    if (!_mainClickBounds.TryGetValue(item.AppId + "_" + i, out Rectangle bounds) || !bounds.Contains(x, y))
                        continue;

                    _isDragCandidate = true;
                    _clickedAppId = item.AppId;
                    _clickedIsDock = false;
                    _clickedIndex = i;
                    _clickStartX = x;
                    _clickStartY = y;
                    _clickedAppSize = item.Size;
                    return true;
                }
            }

            return false;
        }

        public void ReceiveLeftClickHeld(int x, int y)
        {
            if (_dropdownOpen) return;

            if (_isDragCandidate && !_dragStarted)
            {
                int dx = x - _clickStartX;
                int dy = y - _clickStartY;

                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) >= 10)
                {
                    if (IsReorderMode)
                    {
                        _dragStarted = true;
                        Rectangle itemBounds = Rectangle.Empty;
                        if (_openFolder != null)
                        {
                            int relIndex = _clickedIndex - (_currentFolderPage * 9);
                            if (relIndex >= 0 && relIndex < _folderItemBounds.Count)
                                itemBounds = _folderItemBounds[relIndex];
                        }
                        else if (_clickedIsDock)
                        {
                            _dockClickBounds.TryGetValue(_clickedIndex, out itemBounds);
                        }
                        else
                        {
                            _mainClickBounds.TryGetValue(_clickedAppId + "_" + _clickedIndex, out itemBounds);
                        }

                        if (itemBounds != Rectangle.Empty)
                        {
                            BeginDrag(_clickedAppId!, _clickedAppSize, _clickedIsDock ? -1 : _currentPage, _clickedIndex, _clickedIsDock, x, y, itemBounds);
                        }
                    }
                    else
                    {
                        _isDragCandidate = false;
                    }
                }
            }

            if (_isDragging)
            {
                _dragMouseX = x;
                _dragMouseY = y;
                UpdateDragHover(x, y);
            }
        }

        public void ReleaseLeftClick(int x, int y)
        {
            if (_isDragging)
            {
                DropDraggedItem(x, y);
                _isDragging = false;
                _dragAppId = null;
                _draggedItem = null;
                _dragStarted = false;
                _isDragCandidate = false;
            }
            else if (_isDragCandidate)
            {
                _isDragCandidate = false;
                _dragStarted = false;

                if (_menu.HasTouchSwiped)
                {
                    return;
                }

                if (_openFolder != null && _clickedIndex >= 0 && _clickedIndex < _openFolder.FolderItems.Count)
                {
                    if (!IsReorderMode)
                    {
                        string fid = _openFolder.FolderItems[_clickedIndex];
                        if (!string.IsNullOrEmpty(fid))
                        {
                            HandleAppLaunch(fid);
                            _openFolder = null;
                        }
                    }
                }
                else if (_openFolder == null && _currentPage >= 0 && _currentPage < _pages.Count && _clickedIndex >= 0 && _clickedIndex < _pages[_currentPage].Count && !_clickedIsDock)
                {
                    LayoutItem item = _pages[_currentPage][_clickedIndex];
                    if (item.IsFolder)
                    {
                        _mainClickBounds.TryGetValue(item.AppId + "_" + _clickedIndex, out Rectangle bounds);
                        OpenFolderOverlay(item, _clickedIndex, bounds);
                    }
                    else if (IsReorderMode)
                    {
                        _mainClickBounds.TryGetValue(item.AppId + "_" + _clickedIndex, out Rectangle bounds);
                        OpenDropdownForMain(_clickedIndex, bounds);
                    }
                    else
                    {
                        HandleAppLaunch(item.AppId);
                    }
                }
                else if (_clickedIsDock && !IsReorderMode)
                {
                    HandleAppLaunch(_clickedAppId!);
                }
            }
        }

        // Expose editing state to the core PhoneMenu text subscriber loop
        public bool IsEditingFolderName => _isEditingFolderName;

        // Allows Android keyboard or direct input snapshots to set the string safely
        internal void SetFolderNameBuffer(string value)
        {
            _folderNameBuffer = value ?? "";
            if (_folderNameBuffer.Length > 15)
                _folderNameBuffer = _folderNameBuffer.Substring(0, 15);
        }

        // Changed from void to bool to let PhoneMenu know when a key is swallowed/consumed
        public bool HandleKeyPress(Keys key)
        {
            if (_openFolder != null && _isEditingFolderName)
            {
                if (key == Keys.Enter)
                {
                    _isEditingFolderName = false;
                    _openFolder.FolderName = string.IsNullOrWhiteSpace(_folderNameBuffer) ? "Folder" : _folderNameBuffer;
                    SaveLayout();
                    return true;
                }
                else if (key == Keys.Back && _folderNameBuffer.Length > 0)
                {
                    _folderNameBuffer = _folderNameBuffer.Substring(0, _folderNameBuffer.Length - 1);
                    return true;
                }
                return true; // Swallow all other alphanumeric inputs while typing to prevent Escape or other triggers
            }
            return false;
        }

        public void HandleTextInput(char character)
        {
            if (_openFolder != null && _isEditingFolderName && (char.IsLetterOrDigit(character) || character == ' '))
            {
                if (_folderNameBuffer.Length < 15)
                {
                    _folderNameBuffer += character;
                }
            }
        }

        public bool TryChangePageScroll(int delta)
        {
            if (_dropdownOpen) return false;

            if (_openFolder != null)
            {
                int folderTotalPages = (int)Math.Ceiling(_openFolder.FolderItems.Count / 9.0);
                int nextFolderPage = Math.Clamp(_currentFolderPage + delta, 0, Math.Max(0, folderTotalPages));
                if (nextFolderPage == _currentFolderPage) return false;
                _currentFolderPage = nextFolderPage;
                return true;
            }

            int next = Math.Clamp(_currentPage + delta, 0, Math.Max(0, _pages.Count - 1));
            if (next == _currentPage) return false;
            _currentPage = next;
            return true;
        }

        public void LoadLayout(List<HomeAppEntryProxy> allApps)
        {
            string path = GetLayoutFilePath();
            if (!File.Exists(path))
            {
                BuildDefaultLayout(allApps);
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                PhoneLayoutData? data = JsonConvert.DeserializeObject<PhoneLayoutData>(json);
                if (data == null)
                {
                    BuildDefaultLayout(allApps);
                    return;
                }

                _dock = data.Dock ?? new List<string>();

                if (data.Pages != null && data.Pages.Count > 0)
                {
                    _pages = data.Pages;
                }
                else if (data.Main != null && data.Main.Count > 0)
                {
                    MigrateFlatListToSparsePages(data.Main);
                }
                else
                {
                    BuildDefaultLayout(allApps);
                }

                SyncLayoutWithApps(allApps);
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor?.Log($"[PhoneAppLayoutManager] Failed to load layout details: {ex.Message}", LogLevel.Warn);
                BuildDefaultLayout(allApps);
            }
        }

        private void MigrateFlatListToSparsePages(List<LayoutItem> flatList)
        {
            _pages.Clear();
            List<LayoutItem> currentPage = new();
            int col = 0, row = 0;

            foreach (var item in flatList)
            {
                (int w, int h) = GetSizeDims(item.Size);
                AdvanceGridCursor(ref col, ref row, w, h);

                if (row >= GridRows)
                {
                    _pages.Add(currentPage);
                    currentPage = new List<LayoutItem>();
                    col = 0; row = 0;
                    AdvanceGridCursor(ref col, ref row, w, h);
                }

                item.GridCol = col;
                item.GridRow = row;
                currentPage.Add(item);
                col += w;
            }

            if (currentPage.Count > 0 || _pages.Count == 0)
                _pages.Add(currentPage);
        }

        public void SaveLayout()
        {
            try
            {
                PhoneLayoutData data = new PhoneLayoutData
                {
                    Pages = _pages,
                    Dock = _dock
                };

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                string path = GetLayoutFilePath();
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor?.Log($"[PhoneAppLayoutManager] Failed saving layouts: {ex.Message}", LogLevel.Warn);
            }
        }

        private string GetLayoutFilePath()
        {
            return Path.Combine(
                ModEntry.Instance.Helper.DirectoryPath,
                "userdata",
                ModEntry.GetActiveSaveFolderName(),
                "phone_layout.json");
        }

        private void BuildDefaultLayout(List<HomeAppEntryProxy> allApps)
        {
            _dock.Clear();
            _pages.Clear();

            // Populate the docked menu with App Store, Notification, and Setting
            _dock.Add("builtin:appstore");
            _dock.Add("builtin:notification");
            _dock.Add("builtin:setting");

            List<LayoutItem> page1 = new List<LayoutItem>();

            // Calendar 4x2: Occupies Columns 0 to 3, Rows 0 & 1
            page1.Add(new LayoutItem { AppId = "builtin:calendar", Size = AppSize.Size4x2, GridCol = 0, GridRow = 0 });

            // Photo 2x2: Occupies Columns 0 & 1, Rows 2 & 3
            page1.Add(new LayoutItem { AppId = "builtin:photo", Size = AppSize.Size2x2, GridCol = 0, GridRow = 2 });

            // Camera 1x1: Occupies Column 2, Row 2
            page1.Add(new LayoutItem { AppId = "builtin:camera", Size = AppSize.Size1x1, GridCol = 2, GridRow = 2 });

            _pages.Add(page1);

            HashSet<string> placedIds = new(StringComparer.OrdinalIgnoreCase)
            {
                "builtin:calendar", "builtin:setting", "builtin:appstore", "builtin:camera", "builtin:photo", "builtin:notification"
            };

            var remainingApps = allApps.Where(a => !placedIds.Contains(a.Id)).ToList();
            List<LayoutItem> flat = remainingApps.Select(a => new LayoutItem { AppId = a.Id, Size = AppSize.Size1x1 }).ToList();

            foreach (var item in flat)
            {
                RelocateItem(item, 0);
            }

            _currentPage = 0;
        }

        private void SyncLayoutWithApps(List<HomeAppEntryProxy> allApps)
        {
            HashSet<string> allIds = new(allApps.Select(a => a.Id), StringComparer.OrdinalIgnoreCase);

            _dock.RemoveAll(id => !allIds.Contains(id) && !string.IsNullOrEmpty(id));

            foreach (var page in _pages)
            {
                page.RemoveAll(item => !item.IsFolder && !allIds.Contains(item.AppId));
                foreach (var folder in page.Where(f => f.IsFolder))
                {
                    for (int i = 0; i < folder.FolderItems.Count; i++)
                    {
                        if (!string.IsNullOrEmpty(folder.FolderItems[i]) && !allIds.Contains(folder.FolderItems[i]))
                        {
                            folder.FolderItems[i] = string.Empty;
                        }
                    }
                }
            }

            if (!IsReorderMode)
            {
                _pages.RemoveAll(p => p.Count == 0);
                foreach (var page in _pages)
                {
                    page.RemoveAll(item => item.IsFolder && (item.FolderItems.Count == 0 || item.FolderItems.All(string.IsNullOrEmpty)));
                }
            }
            if (_pages.Count == 0) _pages.Add(new List<LayoutItem>());

            HashSet<string> trackedIds = new(StringComparer.OrdinalIgnoreCase);
            foreach (string id in _dock) trackedIds.Add(id);
            foreach (var page in _pages)
            {
                foreach (var item in page)
                {
                    if (item.IsFolder)
                    {
                        foreach (var fid in item.FolderItems)
                            if (!string.IsNullOrEmpty(fid)) trackedIds.Add(fid);
                    }
                    else
                    {
                        trackedIds.Add(item.AppId);
                    }
                }
            }

            var missingApps = allApps.Where(a => !trackedIds.Contains(a.Id));
            foreach (var app in missingApps)
            {
                var targetPage = _pages.Last();
                (int c, int r) = FindFirstAvailableGridPosition(targetPage, 1, 1);
                if (c == -1)
                {
                    targetPage = new List<LayoutItem>();
                    _pages.Add(targetPage);
                    (c, r) = FindFirstAvailableGridPosition(targetPage, 1, 1);
                }

                targetPage.Add(new LayoutItem
                {
                    AppId = app.Id,
                    Size = AppSize.Size1x1,
                    GridCol = c,
                    GridRow = r
                });
            }

            _currentPage = Math.Clamp(_currentPage, 0, _pages.Count - 1);
        }

        private (int col, int row) FindFirstAvailableGridPosition(List<LayoutItem> page, int w, int h)
        {
            for (int r = 0; r <= GridRows - h; r++)
            {
                for (int c = 0; c <= GridCols - w; c++)
                {
                    if (IsCellRegionFree(page, c, r, w, h))
                        return (c, r);
                }
            }
            return (-1, -1);
        }

        private bool IsCellRegionFree(List<LayoutItem> page, int startCol, int startRow, int w, int h, LayoutItem? ignoreItem = null)
        {
            if (startCol < 0 || startCol + w > GridCols || startRow < 0 || startRow + h > GridRows)
                return false;

            bool[,] occupied = new bool[GridCols, GridRows];
            foreach (var item in page)
            {
                if (item == ignoreItem || item.GridCol < 0 || item.GridRow < 0) continue;
                (int iw, int ih) = GetSizeDims(item.Size);

                for (int c = item.GridCol; c < item.GridCol + iw && c < GridCols; c++)
                {
                    for (int r = item.GridRow; r < item.GridRow + ih && r < GridRows; r++)
                    {
                        occupied[c, r] = true;
                    }
                }
            }

            for (int c = startCol; c < startCol + w; c++)
            {
                for (int r = startRow; r < startRow + h; r++)
                {
                    if (occupied[c, r]) return false;
                }
            }
            return true;
        }

        private bool Intersects(int col, int row, int w, int h, LayoutItem other)
        {
            if (other.GridCol < 0 || other.GridRow < 0) return false;
            (int ow, int oh) = GetSizeDims(other.Size);
            return col < other.GridCol + ow && col + w > other.GridCol &&
                   row < other.GridRow + oh && row + h > other.GridRow;
        }

        private void RelocateItem(LayoutItem item, int preferredPage)
        {
            (int w, int h) = GetSizeDims(item.Size);
            int pIdx = preferredPage;

            while (true)
            {
                if (pIdx >= _pages.Count)
                {
                    _pages.Add(new List<LayoutItem>());
                }

                var page = _pages[pIdx];
                (int c, int r) = FindFirstAvailableGridPosition(page, w, h);
                if (c != -1)
                {
                    item.GridCol = c;
                    item.GridRow = r;
                    page.Add(item);
                    break;
                }
                pIdx++;
            }
        }

        private void DrawDock(SpriteBatch b, List<HomeAppEntryProxy> allApps)
        {
            Rectangle contentRect = _menu.GetPhoneContentBounds();
            Rectangle dockBgRect = new Rectangle(contentRect.X, contentRect.Y + ScaleUi(702), contentRect.Width, ScaleUi(108));

            if (Textures.DockedMenuBackground != null)
            {
                b.Draw(Textures.DockedMenuBackground, dockBgRect, Color.White);
            }
            else
            {
                b.Draw(Game1.staminaRect, dockBgRect, new Color(80, 80, 80, 180));
            }

            int count = _dock.Count;
            int displayCount = Math.Max(1, count);

            for (int i = 0; i < displayCount; i++)
            {
                Rectangle cellRect = GetDockCellRect(i, displayCount);
                _dockClickBounds[i] = cellRect;

                if (i >= _dock.Count) continue;
                string appId = _dock[i];
                if (string.IsNullOrEmpty(appId)) continue;

                HomeAppEntryProxy? app = FindApp(allApps, appId);
                if (app == null) continue;

                if (IsReorderMode && _isDragging && _dragFromDock && _dragSourceIndex == i)
                    continue;

                float jiggle = IsReorderMode && _openFolder == null ? GetJiggleAngle(i * 73) : 0f;
                DrawAppCell(b, app, cellRect, AppSize.Size1x1, jiggle, isMergeTarget: (i == _hoverDockIndex && _hoverIsOnIcon), isDockContext: true);
            }
        }

        private void DrawPage(SpriteBatch b, List<LayoutItem> page, List<HomeAppEntryProxy> allApps)
        {
            for (int i = 0; i < page.Count; i++)
            {
                LayoutItem item = page[i];
                if (item.GridCol < 0 || item.GridRow < 0) continue;

                if (_isDragging && !_dragFromDock && _dragSourcePage == _currentPage && _dragSourceIndex == i && _openFolder == null)
                    continue;

                (int w, int h) = GetSizeDims(item.Size);
                Rectangle cellRect = GetMainCellRect(item.GridCol, item.GridRow, w, h);
                _mainClickBounds[item.AppId + "_" + i] = cellRect;

                float jiggle = IsReorderMode && _openFolder == null ? GetJiggleAngle((i + 13) * 37) : 0f;
                bool isMerge = (i == _hoverMainIndex && _hoverIsOnIcon);

                if (item.IsFolder)
                {
                    DrawFolderCell(b, item, cellRect, jiggle, allApps);
                }
                else
                {
                    HomeAppEntryProxy? app = FindApp(allApps, item.AppId);
                    if (app != null)
                        DrawAppCell(b, app, cellRect, item.Size, jiggle, isMerge, isDockContext: false);
                }
            }
        }

        private void DrawAppCell(SpriteBatch b, HomeAppEntryProxy app, Rectangle cellRect, AppSize size, float jiggleDeg, bool isMergeTarget, bool isDockContext)
        {
            int pad = isDockContext ? DockIconPadding : GridIconPadding; // cite: PhoneAppLayoutManager.cs
            Rectangle iconRect = new Rectangle(
                cellRect.X + ScaleUi(pad),
                cellRect.Y + ScaleUi(pad),
                Math.Max(1, cellRect.Width - ScaleUi(pad * 2)),
                Math.Max(1, cellRect.Height - ScaleUi(pad * 2))); // cite: PhoneAppLayoutManager.cs

            if (isMergeTarget)
            {
                b.Draw(Game1.staminaRect, cellRect, Color.White * 0.4f); // cite: PhoneAppLayoutManager.cs
            }

            // --- NEW DESIGN ENHANCEMENT: DYNAMIC WIDGET CHECK ---
            if (app.OnDrawWidget != null)
            {
                // If reorder mode is active, translate the rect automatically so widgets jiggle smoothly!
                if (Math.Abs(jiggleDeg) > 0.001f)
                {
                    float offsetX = (float)(Math.Sin(jiggleDeg * MathHelper.Pi / 180.0) * 2);
                    Rectangle shiftedRect = new Rectangle(iconRect.X + (int)offsetX, iconRect.Y, iconRect.Width, iconRect.Height);
                    app.OnDrawWidget(b, shiftedRect, size);
                }
                else
                {
                    app.OnDrawWidget(b, iconRect, size);
                }
            }
            else
            {
                // Fall back to standard static icon render if no widget callback exists
                DrawIconWithJiggle(b, app, iconRect, jiggleDeg);
            }

            if (app.GetBadgeCount != null)
            {
                try
                {
                    int badge = app.GetBadgeCount();
                    if (badge > 0) DrawBadge(b, iconRect, badge);
                }
                catch { }
            }

            if (!isDockContext)
                DrawAppLabel(b, app.DisplayName, new Vector2(cellRect.Center.X, cellRect.Bottom - ScaleUi(14)), Color.White, 1, cellRect.Width - 27);
        }

        private void DrawFolderCell(SpriteBatch b, LayoutItem folder, Rectangle cellRect, float jiggleDeg, List<HomeAppEntryProxy> allApps)
        {
            Rectangle innerRect = new Rectangle(
                cellRect.X + ScaleUi(GridIconPadding),
                cellRect.Y + ScaleUi(GridIconPadding),
                Math.Max(1, cellRect.Width - ScaleUi(GridIconPadding * 2)),
                Math.Max(1, cellRect.Height - ScaleUi(GridIconPadding * 2)));

            // Draw custom themed group background if available; fallback to shapes safely
            if (Textures.GroupBackground != null)
            {
                b.Draw(Textures.GroupBackground, innerRect, Color.White);
            }
            else
            {
                b.Draw(Game1.staminaRect, innerRect, Color.Black * 0.25f);
                b.Draw(Game1.staminaRect, innerRect, Color.White * 0.15f);
            }

            int miniGridPadding = ScaleUi(4);
            int miniCellSize = (innerRect.Width - (miniGridPadding * 4)) / 3;

            for (int i = 0; i < 9; i++)
            {
                if (i >= folder.FolderItems.Count) break;
                if (string.IsNullOrEmpty(folder.FolderItems[i])) continue;

                HomeAppEntryProxy? childApp = FindApp(allApps, folder.FolderItems[i]);
                if (childApp == null) continue;

                int mc = i % 3;
                int mr = i / 3;
                Rectangle miniIconRect = new Rectangle(
                    innerRect.X + miniGridPadding + mc * (miniCellSize + miniGridPadding),
                    innerRect.Y + miniGridPadding + mr * (miniCellSize + miniGridPadding),
                    miniCellSize, miniCellSize);

                DrawAppIcon(b, childApp.IconTexture, miniIconRect, childApp.SourceRect);
            }

            DrawAppLabel(b, folder.FolderName, new Vector2(cellRect.Center.X, cellRect.Bottom - ScaleUi(14)), Color.White, 1, cellRect.Width - 27);
        }

        private void DrawIconWithJiggle(SpriteBatch b, HomeAppEntryProxy app, Rectangle iconRect, float jiggleDeg)
        {
            if (Math.Abs(jiggleDeg) < 0.001f)
            {
                DrawAppIcon(b, app.IconTexture, iconRect, app.SourceRect);
                return;
            }
            float offsetX = (float)(Math.Sin(jiggleDeg * MathHelper.Pi / 180.0) * 2);
            Rectangle shifted = new Rectangle(iconRect.X + (int)offsetX, iconRect.Y, iconRect.Width, iconRect.Height);
            DrawAppIcon(b, app.IconTexture, shifted, app.SourceRect);
        }

        private void DrawAppIcon(SpriteBatch b, Texture2D texture, Rectangle bounds, Rectangle? sourceRect)
        {
            if (texture == null) return;
            Rectangle source = sourceRect ?? new Rectangle(0, 0, texture.Width, texture.Height);
            float scale = Math.Min(bounds.Width / (float)source.Width, bounds.Height / (float)source.Height);
            int dw = Math.Max(1, (int)Math.Round(source.Width * scale));
            int dh = Math.Max(1, (int)Math.Round(source.Height * scale));
            Rectangle drawRect = new Rectangle(bounds.X + (bounds.Width - dw) / 2, bounds.Y + (bounds.Height - dh) / 2, dw, dh);
            b.Draw(texture, drawRect, source, Color.White);
        }

        private void DrawAppLabel(SpriteBatch b, string text, Vector2 centerPos, Color color, float alpha, int maxCellWidth = 0)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            float fontScale = _menu.GetPhoneTextScale(0.7f);
            SpriteFont font = Game1.smallFont;
            Vector2 fullTextSize = font.MeasureString(text) * fontScale;

            // Calculate the allowable horizontal boundary for this label (defaulting to ~84px scaled if not explicitly passed)
            int maxWidth = maxCellWidth > 0 ? maxCellWidth : ScaleUi(84);
            int padding = ScaleUi(8);
            int clipWidth = maxWidth + padding;

            string displayText = text;
            float offsetX = 0f;

            // If the text exceeds the allowed cell width, compute a continuous scrolling marquee offset
            if (fullTextSize.X > clipWidth)
            {
                string loopText = text + "   " + text;
                Vector2 singleSize = font.MeasureString(text + "   ") * fontScale;

                // Smooth scrolling speed (0.035f pixels per millisecond)
                double timeMs = Game1.currentGameTime.TotalGameTime.TotalMilliseconds;
                offsetX = (float)(timeMs * 0.035 % singleSize.X);

                displayText = loopText;
            }

            // Center position calculations
            float startX = centerPos.X - Math.Min(fullTextSize.X, clipWidth) / 2f;
            Vector2 textPos = new(startX - offsetX, centerPos.Y);

            // If scrolling is needed, use a ScissorRectangle (Hardware Clipping Mask) to prevent text from bleeding into neighboring icons
            Rectangle originalScissor = b.GraphicsDevice.ScissorRectangle;
            bool useClipping = fullTextSize.X > clipWidth;

            if (useClipping)
            {
                b.End();
                b.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, null, new RasterizerState { ScissorTestEnable = true });

                Rectangle clipRect = new((int)(centerPos.X - clipWidth / 2f), (int)centerPos.Y - 2, clipWidth, (int)fullTextSize.Y + 6);

                // Safely intersect with existing viewport bounds to prevent crashes
                Rectangle screenViewport = b.GraphicsDevice.Viewport.Bounds;
                clipRect = Rectangle.Intersect(clipRect, screenViewport);

                b.GraphicsDevice.ScissorRectangle = clipRect;
            }

            // Draw clean drop shadow
            b.DrawString(
                font,
                displayText,
                textPos + new Vector2(1, 1),
                Color.Black * 0.4f * alpha,
                0f,
                Vector2.Zero,
                fontScale,
                SpriteEffects.None,
                0f);

            // Draw label text
            b.DrawString(
                font,
                displayText,
                textPos,
                color * alpha,
                0f,
                Vector2.Zero,
                fontScale,
                SpriteEffects.None,
                0f);

            // Restore normal rendering state if clipping was activated
            if (useClipping)
            {
                b.End();
                b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);
                b.GraphicsDevice.ScissorRectangle = originalScissor;
            }
        }

        private void DrawBadge(SpriteBatch b, Rectangle iconRect, int badgeCount)
        {
            string text = Math.Min(99, badgeCount).ToString();
            Vector2 textSize = Game1.smallFont.MeasureString(text);
            int bw = Math.Max(ScaleUi(24), (int)textSize.X + ScaleUi(10));
            int bh = Math.Max(ScaleUi(18), (int)textSize.Y + ScaleUi(4));
            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                iconRect.Right - bw / 2 - ScaleUi(4), iconRect.Top - bh / 2 + ScaleUi(4), bw, bh, new Color(255, 0, 0, 220), 1f, false);
        }

        private void DrawPageIndicator(SpriteBatch b)
        {
            if (_pages.Count <= 1) return;
            Rectangle contentBounds = _menu.GetPhoneContentBounds();
            int dotSize = ScaleUi(8), dotSpacing = ScaleUi(14);
            int totalW = _pages.Count * dotSpacing;
            int startX = contentBounds.X + ScaleUi(260) - totalW / 2;
            int dotY = contentBounds.Y + ScaleUi(665);

            for (int i = 0; i < _pages.Count; i++)
            {
                b.Draw(Game1.staminaRect, new Rectangle(startX + i * dotSpacing, dotY, dotSize, dotSize), i == _currentPage ? Color.White : Color.White * 0.4f);
            }
            _prevPageBounds = new Rectangle(contentBounds.X + ScaleUi(10), contentBounds.Y + ScaleUi(655), ScaleUi(50), ScaleUi(30));
            _nextPageBounds = new Rectangle(contentBounds.Right - ScaleUi(60), contentBounds.Y + ScaleUi(655), ScaleUi(50), ScaleUi(30));
        }

        private void DrawDoneButton(SpriteBatch b)
        {
            Rectangle contentBounds = _menu.GetPhoneContentBounds();
            _doneButtonBounds = new Rectangle(
                contentBounds.X + ScaleUi(DoneButtonX),
                contentBounds.Y + ScaleUi(DoneButtonY),
                ScaleUi(DoneButtonW),
                ScaleUi(DoneButtonH));

            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                _doneButtonBounds.X, _doneButtonBounds.Y, _doneButtonBounds.Width, _doneButtonBounds.Height, new Color(80, 200, 120, 240), 1f, false);

            string label = ModEntry.SHelper.Translation.Get("ui.reorder.done").Default("Done");
            Vector2 size = Game1.smallFont.MeasureString(label) * 0.7f;
            b.DrawString(Game1.smallFont, label, new Vector2(_doneButtonBounds.X + (_doneButtonBounds.Width - size.X) / 2f, _doneButtonBounds.Y + (_doneButtonBounds.Height - size.Y) / 2f), Color.White, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 1f);
        }

        private void DrawResetButton(SpriteBatch b)
        {
            Rectangle contentBounds = _menu.GetPhoneContentBounds();
            _resetButtonBounds = new Rectangle(
                contentBounds.X + ScaleUi(ResetButtonX),
                contentBounds.Y + ScaleUi(ResetButtonY),
                ScaleUi(ResetButtonW),
                ScaleUi(ResetButtonH));

            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                _resetButtonBounds.X, _resetButtonBounds.Y, _resetButtonBounds.Width, _resetButtonBounds.Height, new Color(220, 80, 80, 240), 1f, false);

            string label = ModEntry.SHelper.Translation.Get("ui.reorder.reset").Default("Reset");
            Vector2 size = Game1.smallFont.MeasureString(label) * 0.7f;
            b.DrawString(Game1.smallFont, label, new Vector2(_resetButtonBounds.X + (_resetButtonBounds.Width - size.X) / 2f, _resetButtonBounds.Y + (_resetButtonBounds.Height - size.Y) / 2f), Color.White, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 1f);
        }

        private void DrawDragGhost(SpriteBatch b, List<HomeAppEntryProxy> allApps)
        {
            if (string.IsNullOrEmpty(_dragAppId)) return;

            int size1x1 = ScaleUi(GridCellSize);
            int ghostSize = (int)(size1x1 * 0.5f);
            Rectangle gRect = new Rectangle(_dragMouseX, _dragMouseY, ghostSize, ghostSize);

            if (_dragAppId == FolderAppId)
            {
                if (Textures.GroupBackground != null)
                {
                    b.Draw(Textures.GroupBackground, gRect, Color.White * 0.8f);
                }
                else
                {
                    b.Draw(Game1.staminaRect, gRect, Color.Black * 0.35f);
                    b.Draw(Game1.staminaRect, gRect, Color.White * 0.15f);
                }
                return;
            }

            HomeAppEntryProxy? app = FindApp(allApps, _dragAppId);
            if (app == null) return;

            DrawAppIcon(b, app.IconTexture, gRect, app.SourceRect);
            b.Draw(Game1.staminaRect, gRect, Color.White * 0.25f);
        }

        private void DrawFolderOverlay(SpriteBatch b, List<HomeAppEntryProxy> allApps)
        {
            if (_openFolder == null) return;
            Rectangle phoneContent = _menu.GetPhoneContentBounds();

            // Draw dark backdrop screen blur dimming layer
            b.Draw(Game1.staminaRect, phoneContent, Color.Black * 0.65f);

            _folderOverlayBounds = new Rectangle(phoneContent.X + ScaleUi(40), phoneContent.Y + ScaleUi(140), ScaleUi(440), ScaleUi(440));

            // Apply the background texture asset directly on the overlay footprint
            if (Textures.GroupBackground != null)
            {
                b.Draw(Textures.GroupBackground, _folderOverlayBounds, Color.White);
            }
            else
            {
                b.Draw(Game1.staminaRect, _folderOverlayBounds, Color.Black * 0.2f);
                b.Draw(Game1.staminaRect, _folderOverlayBounds, Color.White * 0.75f);
            }

            // --- MOVE GROUP NAME OUTSIDE THE BOX ---
            string title = _isEditingFolderName ? (_folderNameBuffer + "|") : (_openFolder.FolderName ?? "Folder");
            Vector2 titleSize = Game1.dialogueFont.MeasureString(title) * 0.8f;

            // Placed ScaleUi(45) pixels above the upper boundary edge of the group container box
            Vector2 titlePos = new Vector2(_folderOverlayBounds.X + (_folderOverlayBounds.Width - titleSize.X) / 2f, _folderOverlayBounds.Y - ScaleUi(45));

            // Render text in White/LightGray to make it pop against the dark screen overlay background tint
            b.DrawString(Game1.dialogueFont, title, titlePos, Color.White, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 1f);

            // Re-bind click hitbox bounds to the new elevated title positioning coordinates
            _folderRenameBoxBounds = new Rectangle((int)titlePos.X - ScaleUi(10), (int)titlePos.Y - ScaleUi(5), (int)titleSize.X + ScaleUi(20), (int)titleSize.Y + ScaleUi(10));

            if (IsReorderMode && !_isEditingFolderName)
            {
                b.Draw(Game1.staminaRect, new Rectangle(_folderRenameBoxBounds.Right, _folderRenameBoxBounds.Y + ScaleUi(6), ScaleUi(6), ScaleUi(14)), Color.White * 0.75f);
            }

            _folderItemBounds.Clear();

            // --- REDUCE ITEM GAPS & OPTIMIZE ENTIRE AREA FOR GRID ONLY ---
            int folderCols = 3;
            int innerCellSize = ScaleUi(96);
            int padX = ScaleUi(20); // Reduced from 24 to 20 to bring outer items inward
            int padY = ScaleUi(10); // Reduced from 16 to 10 to tighten vertical layout gaps

            // Dynamically calculate the total content layout bounds to achieve perfect centering symmetry
            int totalGridW = 3 * innerCellSize + 2 * padX;
            int rowStepY = innerCellSize + padY + ScaleUi(14);
            int totalGridH = 2 * rowStepY + innerCellSize + ScaleUi(14);

            int startX = _folderOverlayBounds.X + (_folderOverlayBounds.Width - totalGridW) / 2;
            int startY = _folderOverlayBounds.Y + (_folderOverlayBounds.Height - totalGridH) / 2;

            int startIdx = _currentFolderPage * 9;

            while (_openFolder.FolderItems.Count < startIdx + 9)
            {
                _openFolder.FolderItems.Add(string.Empty);
            }

            for (int i = 0; i < 9; i++)
            {
                int globalIdx = startIdx + i;
                string fid = _openFolder.FolderItems[globalIdx];

                int c = i % folderCols;
                int r = i / folderCols;
                Rectangle itemBounds = new Rectangle(startX + c * (innerCellSize + padX), startY + r * (innerCellSize + padY + ScaleUi(14)), innerCellSize, innerCellSize);
                _folderItemBounds.Add(itemBounds);

                if (_isDragging && _dragAppId == fid && _dragSourcePage == -2) continue;

                if (!string.IsNullOrEmpty(fid))
                {
                    HomeAppEntryProxy? childApp = FindApp(allApps, fid);
                    if (childApp != null)
                    {
                        float jiggle = IsReorderMode ? GetJiggleAngle(globalIdx * 45) : 0f;
                        Rectangle iconBounds = new Rectangle(itemBounds.X + ScaleUi(6), itemBounds.Y + ScaleUi(6), itemBounds.Width - ScaleUi(12), itemBounds.Height - ScaleUi(12));
                        DrawIconWithJiggle(b, childApp, iconBounds, jiggle);

                        Vector2 labelSz = Game1.smallFont.MeasureString(childApp.DisplayName) * 0.55f;
                        b.DrawString(Game1.smallFont, childApp.DisplayName, new Vector2(itemBounds.X + (itemBounds.Width - labelSz.X) / 2f, itemBounds.Bottom - ScaleUi(4)), Color.Black, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 1f);
                    }
                }
                else if (IsReorderMode)
                {
                    b.Draw(Game1.staminaRect, itemBounds, Color.Black * 0.05f);
                }
            }

            // Draw pagination dot index indicators at the bottom margin area
            int folderTotalPages = (int)Math.Ceiling(_openFolder.FolderItems.Count / 9.0);
            if (folderTotalPages > 1)
            {
                int dotSize = ScaleUi(6), dotSpacing = ScaleUi(12);
                int totalDotW = folderTotalPages * dotSpacing;
                int dotStartX = _folderOverlayBounds.X + (_folderOverlayBounds.Width - totalDotW) / 2;
                int dotY = _folderOverlayBounds.Bottom - ScaleUi(22);

                for (int p = 0; p < folderTotalPages; p++)
                {
                    b.Draw(Game1.staminaRect, new Rectangle(dotStartX + p * dotSpacing, dotY, dotSize, dotSize), p == _currentFolderPage ? Color.Black * 0.7f : Color.Black * 0.2f);
                }
            }

            if (_isDragging && _dragSourcePage == -2)
            {
                DrawDragGhost(b, allApps);
            }
        }

        private void DrawDropdown(SpriteBatch b)
        {
            foreach (var item in _dropdownItems)
            {
                IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), item.bounds.X, item.bounds.Y, item.bounds.Width, item.bounds.Height, Color.White, 1f, false);
                Vector2 sz = Game1.smallFont.MeasureString(item.label) * 0.65f;
                b.DrawString(Game1.smallFont, item.label, new Vector2(item.bounds.X + (item.bounds.Width - sz.X) / 2f, item.bounds.Y + (item.bounds.Height - sz.Y) / 2f), Color.Black, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 1f);
            }
        }

        private void OpenDropdownForMain(int index, Rectangle appBounds)
        {
            var item = _pages[_currentPage][index];
            if (item.IsFolder) return;

            _dropdownAppId = item.AppId;
            _dropdownForDock = false;
            _dropdownForMainIndex = index;

            List<HomeAppEntryProxy> allApps = BuildAllAppsSnapshot();
            HomeAppEntryProxy? app = FindApp(allApps, item.AppId);

            BuildDropdownItems(appBounds, app);
            _dropdownOpen = true;
        }

        private void BuildDropdownItems(Rectangle anchorBounds, HomeAppEntryProxy? app)
        {
            _dropdownItems.Clear();
            int itemH = ScaleUi(32), itemW = ScaleUi(140);
            int x = anchorBounds.X, y = anchorBounds.Bottom + ScaleUi(4);

            List<(AppSize size, DropdownOption option, string label)> allOptions = new()
            {
                (AppSize.Size1x1, DropdownOption.ChangeSize1x1, "1×1"),
                (AppSize.Size2x1, DropdownOption.ChangeSize2x1, "2×1"),
                (AppSize.Size2x2, DropdownOption.ChangeSize2x2, "2×2"),
                (AppSize.Size3x2, DropdownOption.ChangeSize3x2, "3×2"),
                (AppSize.Size4x2, DropdownOption.ChangeSize4x2, "4×2"),
                (AppSize.Size4x3, DropdownOption.ChangeSize4x3, "4×3"),
                (AppSize.Size4x4, DropdownOption.ChangeSize4x4, "4×4")
            };

            List<(DropdownOption option, string label)> options = new();

            bool hasExplicitSizes = app?.SupportedSizes != null && app.SupportedSizes.Count > 0;

            foreach (var opt in allOptions)
            {
                if (hasExplicitSizes)
                {
                    if (app!.SupportedSizes.Contains(opt.size))
                    {
                        options.Add((opt.option, opt.label));
                    }
                }
                else
                {
                    bool isBuiltIn = app == null || app.Id.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase);
                    if (isBuiltIn || opt.size == AppSize.Size1x1)
                    {
                        options.Add((opt.option, opt.label));
                    }
                }
            }

            if (options.Count == 0)
            {
                options.Add((DropdownOption.ChangeSize1x1, "1×1 Size"));
            }

            Rectangle contentBounds = _menu.GetPhoneContentBounds();
            if (y + options.Count * (itemH + ScaleUi(2)) > contentBounds.Bottom)
            {
                y = anchorBounds.Top - options.Count * (itemH + ScaleUi(2)) - ScaleUi(4);
            }

            for (int i = 0; i < options.Count; i++)
            {
                _dropdownItems.Add((options[i].option, new Rectangle(x, y + i * (itemH + ScaleUi(2)), itemW, itemH), options[i].label));
            }
        }

        private void HandleDropdownClick(int x, int y)
        {
            foreach (var item in _dropdownItems)
            {
                if (!item.bounds.Contains(x, y)) continue;

                if (item.option == DropdownOption.ChangeSize1x1) ApplySize(AppSize.Size1x1);
                if (item.option == DropdownOption.ChangeSize2x1) ApplySize(AppSize.Size2x1);
                if (item.option == DropdownOption.ChangeSize2x2) ApplySize(AppSize.Size2x2);
                if (item.option == DropdownOption.ChangeSize3x2) ApplySize(AppSize.Size3x2);
                if (item.option == DropdownOption.ChangeSize4x2) ApplySize(AppSize.Size4x2);
                if (item.option == DropdownOption.ChangeSize4x3) ApplySize(AppSize.Size4x3);
                if (item.option == DropdownOption.ChangeSize4x4) ApplySize(AppSize.Size4x4);

                // Handle texture sub-theme choice execution
                if (item.option == DropdownOption.SelectTheme)
                {
                    ApplyTheme(_dropdownAppId!, item.label);
                    _dropdownOpen = false;
                    _isDropdownShowingThemes = false;
                    return;
                }

                _dropdownOpen = false;
                Game1.playSound("smallSelect");
                return;
            }
        }

        private string GetThemeComponentForApp(string appId)
        {
            if (appId.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase))
            {
                return "app_" + appId.Substring("builtin:".Length);
            }
            return "app_" + appId.Replace(":", "_");
        }

        private void ApplyTheme(string appId, string themeName)
        {
            if (string.IsNullOrEmpty(appId)) return;
            string component = GetThemeComponentForApp(appId);

            // Save choice to component_themes json data manifest
            AssetHelper.SetComponentTheme(component, themeName);

            // Clear the active in-memory cache to terminate old textures references completely
            Textures.AppTextureCache?.Clear();

            // Re-bind graphics pointers immediately across the screen context
            _menu.ReloadThemeTextures();
        }

        private void ApplySize(AppSize newSize)
        {
            if (_dropdownForMainIndex >= 0 && _dropdownForMainIndex < _pages[_currentPage].Count)
            {
                var item = _pages[_currentPage][_dropdownForMainIndex];
                (int nw, int nh) = GetSizeDims(newSize);

                item.Size = newSize;

                if (item.GridCol + nw > GridCols) item.GridCol = Math.Max(0, GridCols - nw);
                if (item.GridRow + nh > GridRows) item.GridRow = Math.Max(0, GridRows - nh);

                List<LayoutItem> page = _pages[_currentPage];
                List<LayoutItem> toDisplace = new List<LayoutItem>();

                for (int i = page.Count - 1; i >= 0; i--)
                {
                    var other = page[i];
                    if (other == item) continue;

                    if (Intersects(item.GridCol, item.GridRow, nw, nh, other))
                    {
                        toDisplace.Add(other);
                        page.RemoveAt(i);
                    }
                }

                foreach (var disp in toDisplace)
                {
                    RelocateItem(disp, _currentPage);
                }

                CleanupEmptyPages();
                SaveLayout();
            }
        }

        private void BeginDrag(string appId, AppSize size, int sourcePage, int sourceIndex, bool isDock, int mouseX, int mouseY, Rectangle itemBounds)
        {
            _isDragging = true;
            _dragAppId = appId;
            _dragAppSize = size;
            _dragSourcePage = _openFolder != null ? -2 : sourcePage;
            _dragSourceIndex = sourceIndex;
            _dragFromDock = isDock;
            _dragMouseX = mouseX;
            _dragMouseY = mouseY;

            if (_openFolder == null && !isDock && sourcePage >= 0 && sourcePage < _pages.Count && sourceIndex >= 0 && sourceIndex < _pages[sourcePage].Count)
            {
                _draggedItem = _pages[sourcePage][sourceIndex];
            }
            else
            {
                _draggedItem = null;
            }

            _hoverCellCol = -1;
            _hoverCellRow = -1;
            _hoverIsOnIcon = false;
            _hoverIsOnGap = false;

            Game1.playSound("smallSelect");
        }

        private void UpdateDragHover(int x, int y)
        {
            _hoverMainIndex = -1;
            _hoverDockIndex = -1;
            _hoverCellCol = -1;
            _hoverCellRow = -1;
            _hoverIsOnIcon = false;
            _hoverIsOnGap = false;

            Rectangle contentRect = _menu.GetPhoneContentBounds();

            if (_openFolder != null)
            {
                int startIdx = _currentFolderPage * 9;
                int closestIdx = -1;
                float closestDist = float.MaxValue;

                for (int i = 0; i < _folderItemBounds.Count; i++)
                {
                    int globalIdx = startIdx + i;
                    if (globalIdx == _dragSourceIndex) continue;

                    Vector2 center = new Vector2(_folderItemBounds[i].Center.X, _folderItemBounds[i].Center.Y);
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist < closestDist && dist < ScaleUi(110))
                    {
                        closestDist = dist;
                        closestIdx = globalIdx;
                    }
                }

                if (closestIdx != -1)
                {
                    _hoverMainIndex = closestIdx;
                    _hoverIsOnGap = true;
                }
                return;
            }

            Rectangle dockBgRect = new Rectangle(contentRect.X, contentRect.Y + ScaleUi(690), contentRect.Width, ScaleUi(120));
            if (dockBgRect.Contains(x, y))
            {
                for (int i = 0; i < _dock.Count; i++)
                {
                    if (_dockClickBounds.TryGetValue(i, out Rectangle cellBound) && cellBound.Contains(x, y))
                    {
                        _hoverDockIndex = i;
                        _hoverIsOnIcon = true;
                        return;
                    }
                }

                if (_dock.Count == 0)
                {
                    _hoverDockIndex = 0;
                }
                else
                {
                    int closestIdx = _dock.Count;
                    for (int i = 0; i < _dock.Count; i++)
                    {
                        if (_dockClickBounds.TryGetValue(i, out Rectangle cellBound))
                        {
                            if (x < cellBound.Center.X)
                            {
                                closestIdx = i;
                                break;
                            }
                        }
                    }
                    _hoverDockIndex = closestIdx;
                }
                return;
            }

            int relX = x - contentRect.X;
            int relY = y - contentRect.Y;

            for (int col = 0; col < GridCols; col++)
            {
                for (int row = 0; row < GridRows; row++)
                {
                    int cx = ScaleUi(GridStartX + col * (GridCellSize + GridPadding));
                    int cy = ScaleUi(GridStartY + row * (GridCellSize + GridPadding));
                    int cs = ScaleUi(GridCellSize);

                    if (relX >= cx && relX < cx + cs && relY >= cy && relY < cy + cs)
                    {
                        _hoverCellCol = col;
                        _hoverCellRow = row;

                        var page = _currentPage < _pages.Count ? _pages[_currentPage] : null;
                        if (page != null)
                        {
                            for (int i = 0; i < page.Count; i++)
                            {
                                if (!_dragFromDock && _dragSourcePage == _currentPage && i == _dragSourceIndex && _openFolder == null)
                                    continue;

                                var item = page[i];
                                (int iw, int ih) = GetSizeDims(item.Size);
                                if (col >= item.GridCol && col < item.GridCol + iw && row >= item.GridRow && row < item.GridRow + ih)
                                {
                                    _hoverMainIndex = i;

                                    Rectangle cellBounds = GetMainCellRect(item.GridCol, item.GridRow, iw, ih);
                                    Rectangle iconBounds = new Rectangle(cellBounds.X + ScaleUi(GridIconPadding), cellBounds.Y + ScaleUi(GridIconPadding), cellBounds.Width - ScaleUi(GridIconPadding * 2), cellBounds.Height - ScaleUi(GridIconPadding * 2));

                                    if (iconBounds.Contains(x, y))
                                    {
                                        _hoverIsOnIcon = true;
                                    }
                                    else
                                    {
                                        _hoverIsOnGap = true;
                                    }
                                    return;
                                }
                            }
                        }
                        return;
                    }
                }
            }
        }

        private void DropDraggedItem(int x, int y)
        {
            if (string.IsNullOrEmpty(_dragAppId)) return;

            int sourceCol = -1;
            int sourceRow = -1;
            if (!_dragFromDock && _dragSourcePage >= 0 && _dragSourcePage < _pages.Count && _dragSourceIndex >= 0 && _dragSourceIndex < _pages[_dragSourcePage].Count)
            {
                sourceCol = _pages[_dragSourcePage][_dragSourceIndex].GridCol;
                sourceRow = _pages[_dragSourcePage][_dragSourceIndex].GridRow;
            }

            if (!_dragFromDock && _dragSourcePage == _currentPage && _dragSourceIndex == _hoverMainIndex && _openFolder == null)
            {
                _hoverMainIndex = -1; _hoverDockIndex = -1; _hoverCellCol = -1; _hoverCellRow = -1;
                return;
            }

            if (_dragFromDock && _dragSourceIndex == _hoverDockIndex && _hoverIsOnIcon && _openFolder == null)
            {
                _hoverMainIndex = -1; _hoverDockIndex = -1; _hoverCellCol = -1; _hoverCellRow = -1;
                return;
            }

            if (_dragSourcePage == -2 && _openFolder != null)
            {
                if (_folderOverlayBounds.Contains(x, y))
                {
                    if (_hoverMainIndex >= 0 && _hoverMainIndex < _openFolder.FolderItems.Count)
                    {
                        string temp = _openFolder.FolderItems[_hoverMainIndex];
                        _openFolder.FolderItems[_hoverMainIndex] = _dragAppId;
                        _openFolder.FolderItems[_dragSourceIndex] = temp;
                    }
                    else
                    {
                        _openFolder.FolderItems[_dragSourceIndex] = _dragAppId;
                    }
                }
                else
                {
                    _openFolder.FolderItems[_dragSourceIndex] = string.Empty;
                    var page = _pages[_currentPage];
                    (int c, int r) = (_hoverCellCol != -1) ? (_hoverCellCol, _hoverCellRow) : FindFirstAvailableGridPosition(page, 1, 1);

                    if (c != -1 && IsCellRegionFree(page, c, r, 1, 1))
                    {
                        page.Add(new LayoutItem { AppId = _dragAppId, Size = AppSize.Size1x1, GridCol = c, GridRow = r });
                    }
                    else
                    {
                        AddToPageGrid(_currentPage, _dragAppId, AppSize.Size1x1);
                    }
                }

                while (_openFolder.FolderItems.Count > 0 && string.IsNullOrEmpty(_openFolder.FolderItems.Last()))
                {
                    _openFolder.FolderItems.RemoveAt(_openFolder.FolderItems.Count - 1);
                }

                // AUTO-PRUNE FOLDER PAGES: Recalculate total page count and snap layout viewport to match active allocations
                int folderTotalPages = (int)Math.Ceiling(_openFolder.FolderItems.Count / 9.0);
                if (_currentFolderPage >= folderTotalPages && folderTotalPages > 0)
                {
                    _currentFolderPage = folderTotalPages - 1;
                }

                if (_openFolder.FolderItems.Count == 0 || _openFolder.FolderItems.All(string.IsNullOrEmpty))
                {
                    if (_currentPage < _pages.Count) _pages[_currentPage].Remove(_openFolder);
                    _openFolder = null;
                    _openFolderIndex = -1;
                }

                CleanupEmptyPages();
                SaveLayout();
                _hoverMainIndex = -1; _hoverDockIndex = -1; _hoverCellCol = -1; _hoverCellRow = -1;
                return;
            }

            LayoutItem? targetItem = null;
            List<LayoutItem>? targetPage = (_currentPage >= 0 && _currentPage < _pages.Count) ? _pages[_currentPage] : null;
            if (targetPage != null && _hoverMainIndex >= 0 && _hoverMainIndex < targetPage.Count)
            {
                targetItem = targetPage[_hoverMainIndex];
            }

            // DROP GROUP ON APP ICON PROTECTION: Revert folder drop targets away from standard apps to prevent spilling
            if (targetItem != null && _hoverIsOnIcon && targetPage != null)
            {
                if (_dragAppId == FolderAppId || (_draggedItem != null && _draggedItem.IsFolder))
                {
                    if (_draggedItem != null && _dragSourcePage >= 0 && _dragSourcePage < _pages.Count)
                    {
                        _draggedItem.GridCol = sourceCol;
                        _draggedItem.GridRow = sourceRow;
                        _pages[_dragSourcePage].Add(_draggedItem);
                    }
                    else if (_dragFromDock)
                    {
                        _dock.Insert(Math.Clamp(_dragSourceIndex, 0, _dock.Count), _dragAppId);
                    }
                    else
                    {
                        AddToPageGrid(_dragSourcePage, _dragAppId, _dragAppSize);
                    }

                    _dock.RemoveAll(string.IsNullOrEmpty);
                    CleanupEmptyPages();
                    SaveLayout();
                    _hoverMainIndex = -1; _hoverDockIndex = -1; _hoverCellCol = -1; _hoverCellRow = -1;
                    return;
                }
            }

            if (_dragFromDock)
            {
                if (_dragSourceIndex >= 0 && _dragSourceIndex < _dock.Count) _dock.RemoveAt(_dragSourceIndex);
            }
            else if (_dragSourcePage >= 0 && _dragSourcePage < _pages.Count)
            {
                if (_dragSourceIndex >= 0 && _dragSourceIndex < _pages[_dragSourcePage].Count)
                    _pages[_dragSourcePage].RemoveAt(_dragSourceIndex);
            }

            if (_hoverDockIndex >= 0)
            {
                if (_dragAppId == FolderAppId || (_draggedItem != null && _draggedItem.IsFolder))
                {
                    if (_dragFromDock) _dock.Insert(Math.Clamp(_dragSourceIndex, 0, _dock.Count), _dragAppId);
                    else if (_dragSourcePage >= 0 && _draggedItem != null) _pages[_dragSourcePage].Add(_draggedItem);
                    else AddToPageGrid(_currentPage, _dragAppId, _dragAppSize);

                    _dock.RemoveAll(string.IsNullOrEmpty);
                    CleanupEmptyPages(); SaveLayout();
                    _hoverMainIndex = -1; _hoverDockIndex = -1; _hoverCellCol = -1; _hoverCellRow = -1;
                    return;
                }

                _dragAppSize = AppSize.Size1x1;
                if (_draggedItem != null) _draggedItem.Size = AppSize.Size1x1;

                if (_dragFromDock)
                {
                    if (_hoverIsOnIcon && _hoverDockIndex < _dock.Count)
                    {
                        string targetId = _dock[_hoverDockIndex];
                        _dock[_hoverDockIndex] = _dragAppId;
                        _dock.Insert(Math.Clamp(_dragSourceIndex, 0, _dock.Count), targetId);
                    }
                    else
                    {
                        _dock.Insert(Math.Clamp(_hoverDockIndex, 0, _dock.Count), _dragAppId);
                    }
                }
                else
                {
                    if (_hoverIsOnIcon && _hoverDockIndex < _dock.Count)
                    {
                        string bumpId = _dock[_hoverDockIndex];
                        _dock[_hoverDockIndex] = _dragAppId;
                        AddToPageGrid(_currentPage, bumpId, AppSize.Size1x1);
                    }
                    else
                    {
                        if (_dock.Count < DockCols)
                        {
                            _dock.Insert(Math.Clamp(_hoverDockIndex, 0, _dock.Count), _dragAppId);
                        }
                        else
                        {
                            int bumpIdx = Math.Clamp(_hoverDockIndex, 0, _dock.Count - 1);
                            string bumpId = _dock[bumpIdx];
                            _dock[bumpIdx] = _dragAppId;
                            AddToPageGrid(_currentPage, bumpId, AppSize.Size1x1);
                        }
                    }
                }

                _dock.RemoveAll(string.IsNullOrEmpty);
                CleanupEmptyPages(); SaveLayout();
                _hoverMainIndex = -1; _hoverDockIndex = -1; _hoverCellCol = -1; _hoverCellRow = -1;
                return;
            }

            if (targetItem != null && _hoverIsOnIcon && targetPage != null)
            {
                if (targetItem.IsFolder)
                {
                    int firstEmpty = targetItem.FolderItems.IndexOf(string.Empty);
                    if (firstEmpty != -1) targetItem.FolderItems[firstEmpty] = _dragAppId;
                    else targetItem.FolderItems.Add(_dragAppId);
                }
                else
                {
                    LayoutItem newFolder = new LayoutItem
                    {
                        AppId = FolderAppId,
                        FolderName = "Folder",
                        GridCol = targetItem.GridCol,
                        GridRow = targetItem.GridRow,
                        Size = AppSize.Size1x1,
                        FolderItems = new List<string> { targetItem.AppId, _dragAppId }
                    };
                    targetPage.Remove(targetItem);
                    targetPage.Add(newFolder);
                }
            }
            else if (targetItem != null && _hoverIsOnGap && targetPage != null)
            {
                int targetCol = targetItem.GridCol;
                int targetRow = targetItem.GridRow;
                (int dw, int dh) = GetSizeDims(_dragAppSize);

                if (targetCol + dw > GridCols) targetCol = Math.Max(0, GridCols - dw);
                if (targetRow + dh > GridRows) targetRow = Math.Max(0, GridRows - dh);

                LayoutItem newItem = _draggedItem ?? new LayoutItem { AppId = _dragAppId, Size = _dragAppSize };
                newItem.GridCol = targetCol; newItem.GridRow = targetRow;

                List<LayoutItem> tDisplace = new List<LayoutItem>();
                for (int i = targetPage.Count - 1; i >= 0; i--)
                {
                    if (Intersects(targetCol, targetRow, dw, dh, targetPage[i]))
                    {
                        tDisplace.Add(targetPage[i]);
                        targetPage.RemoveAt(i);
                    }
                }
                targetPage.Add(newItem);
                foreach (var disp in tDisplace) RelocateItem(disp, _currentPage);
            }
            else if (_hoverCellCol != -1 && _hoverCellRow != -1 && targetPage != null)
            {
                (int w, int h) = GetSizeDims(_dragAppSize);
                int dropCol = _hoverCellCol;
                int dropRow = _hoverCellRow;
                if (dropCol + w > GridCols) dropCol = Math.Max(0, GridCols - w);
                if (dropRow + h > GridRows) dropRow = Math.Max(0, GridRows - h);

                LayoutItem newItem = _draggedItem ?? new LayoutItem { AppId = _dragAppId, Size = _dragAppSize };
                newItem.GridCol = dropCol; newItem.GridRow = dropRow;

                List<LayoutItem> toDisplace = new List<LayoutItem>();
                for (int i = targetPage.Count - 1; i >= 0; i--)
                {
                    if (Intersects(dropCol, dropRow, w, h, targetPage[i]))
                    {
                        toDisplace.Add(targetPage[i]);
                        targetPage.RemoveAt(i);
                    }
                }
                targetPage.Add(newItem);
                foreach (var disp in toDisplace) RelocateItem(disp, _currentPage);
            }
            else
            {
                if (_draggedItem != null && _dragSourcePage >= 0 && _dragSourcePage < _pages.Count)
                {
                    _draggedItem.GridCol = sourceCol;
                    _draggedItem.GridRow = sourceRow;
                    _pages[_dragSourcePage].Add(_draggedItem);
                }
                else if (_dragFromDock)
                {
                    _dock.Insert(Math.Clamp(_dragSourceIndex, 0, _dock.Count), _dragAppId);
                }
                else
                {
                    AddToPageGrid(_dragSourcePage, _dragAppId, _dragAppSize);
                }
            }

            _dock.RemoveAll(string.IsNullOrEmpty);
            CleanupEmptyPages();
            SaveLayout();

            _hoverMainIndex = -1; _hoverDockIndex = -1; _hoverCellCol = -1; _hoverCellRow = -1;
        }

        private void AddToPageGrid(int pageIndex, string appId, AppSize size)
        {
            if (pageIndex < 0 || pageIndex >= _pages.Count) pageIndex = _pages.Count - 1;
            if (_pages.Count == 0) _pages.Add(new List<LayoutItem>());

            var targetPage = _pages[pageIndex];
            (int w, int h) = GetSizeDims(size);
            (int c, int r) = FindFirstAvailableGridPosition(targetPage, w, h);

            if (c == -1)
            {
                targetPage = new List<LayoutItem>();
                _pages.Add(targetPage);
                _currentPage = _pages.Count - 1;
                (c, r) = FindFirstAvailableGridPosition(targetPage, w, h);
            }

            targetPage.Add(new LayoutItem { AppId = appId, Size = size, GridCol = c, GridRow = r });
        }

        private void CleanupEmptyPages()
        {
            for (int i = _pages.Count - 1; i >= 1; i--)
            {
                if (_pages[i].Count == 0)
                {
                    _pages.RemoveAt(i);
                }
            }
            _currentPage = Math.Clamp(_currentPage, 0, _pages.Count - 1);
        }

        private void OpenFolderOverlay(LayoutItem folder, int index, Rectangle bounds)
        {
            _openFolder = folder;
            _openFolderIndex = index;
            _currentFolderPage = 0;
            _isEditingFolderName = false;
            _folderItemBounds.Clear();
            Game1.playSound("smallSelect");
        }

        private void HandleAppLaunch(string appId)
        {
            _menu.TryHandleHomeAppClickPublic(appId);
        }

        private Rectangle GetMainCellRect(int col, int row, int w, int h)
        {
            Rectangle contentRect = _menu.GetPhoneContentBounds();
            int x = contentRect.X + ScaleUi(GridStartX + col * (GridCellSize + GridPadding));
            int y = contentRect.Y + ScaleUi(GridStartY + row * (GridCellSize + GridPadding));
            int width = ScaleUi(w * GridCellSize + (w - 1) * GridPadding);
            int height = ScaleUi(h * GridCellSize + (h - 1) * GridPadding);
            return new Rectangle(x, y, Math.Max(1, width), Math.Max(1, height));
        }

        private Rectangle GetDockCellRect(int col, int totalCount)
        {
            Rectangle contentRect = _menu.GetPhoneContentBounds();
            int count = Math.Max(1, totalCount);
            int totalWidth = count * DockCellSize + (count - 1) * DockPadding;
            int startX = (520 - totalWidth) / 2;
            int x = contentRect.X + ScaleUi(startX + col * (DockCellSize + DockPadding));
            int y = contentRect.Y + ScaleUi(DockStartY);
            return new Rectangle(x, y, ScaleUi(DockCellSize), ScaleUi(DockCellSize));
        }

        private void AdvanceGridCursor(ref int col, ref int row, int w, int h)
        {
            if (col + w > GridCols)
            {
                col = 0;
                row++;
            }
        }

        private int ScaleUi(int val) => ModEntry.ScalePhoneUiValue(val, _menu.PhoneUiScale);

        private float GetJiggleAngle(int seed)
        {
            double phase = (double)seed / 100.0;
            return (float)(Math.Sin(_jiggleElapsed * JiggleFrequency * MathHelper.TwoPi + phase) * JiggleAmplitude);
        }

        private Color GetHomeTextColor()
        {
            try { return _menu.GetHomeTextColorPublic(); }
            catch { return Color.White; }
        }

        public static int GetCellCount(AppSize size)
        {
            (int w, int h) = GetSizeDims(size);
            return w * h;
        }

        public static (int cols, int rows) GetSizeDims(AppSize size)
        {
            return size switch
            {
                AppSize.Size1x1 => (1, 1),
                AppSize.Size2x1 => (2, 1),
                AppSize.Size2x2 => (2, 2),
                AppSize.Size3x2 => (3, 2),
                AppSize.Size4x2 => (4, 2),
                AppSize.Size4x3 => (4, 3),
                AppSize.Size4x4 => (4, 4),
                _ => (1, 1)
            };
        }

        private List<HomeAppEntryProxy> BuildAllAppsSnapshot() => _menu.BuildHomeAppsSnapshotPublic();

        private static HomeAppEntryProxy? FindApp(List<HomeAppEntryProxy> apps, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return apps.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
        }



        private void BuildThemeDropdownItems(Rectangle anchorBounds, HomeAppEntryProxy? app)
        {
            _dropdownItems.Clear();
            if (app == null) return;

            string component = GetThemeComponentForApp(app.Id);
            List<string> themes = new() { "default" };

            // Scan your phone_themes folder for any component-specific styles
            string themeComponentDir = Path.Combine(ModEntry.Instance.Helper.DirectoryPath, "phone_themes", component);
            if (Directory.Exists(themeComponentDir))
            {
                foreach (string dir in Directory.GetDirectories(themeComponentDir))
                {
                    string name = Path.GetFileName(dir);
                    if (!string.Equals(name, "default", StringComparison.OrdinalIgnoreCase))
                    {
                        themes.Add(name);
                    }
                }
            }

            int itemH = ScaleUi(32), itemW = ScaleUi(140);
            int x = anchorBounds.X, y = anchorBounds.Bottom + ScaleUi(4);

            // Dynamic viewport bounding protection so choices don't bleed past the bottom phone edge
            Rectangle contentBounds = _menu.GetPhoneContentBounds();
            if (y + themes.Count * (itemH + ScaleUi(2)) > contentBounds.Bottom)
            {
                y = anchorBounds.Top - themes.Count * (itemH + ScaleUi(2)) - ScaleUi(4);
            }

            for (int i = 0; i < themes.Count; i++)
            {
                _dropdownItems.Add((DropdownOption.SelectTheme, new Rectangle(x, y + i * (itemH + ScaleUi(2)), itemW, itemH), themes[i]));
            }
        }
    }

    internal class HomeAppEntryProxy
    {
        public string Id { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public Texture2D IconTexture { get; init; } = null!;
        public Rectangle? SourceRect { get; init; }
        public Func<int>? GetBadgeCount { get; init; }
        public List<AppSize> SupportedSizes { get; init; } = new();
        public Action<SpriteBatch, Rectangle, AppSize>? OnDrawWidget { get; init; } // <-- Add this
    }
}