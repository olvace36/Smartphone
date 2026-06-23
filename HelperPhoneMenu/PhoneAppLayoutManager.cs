using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace Smartphone
{
    // ──────────────────────────────────────────────────────────────────────────
    // Data structures
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>All supported app widget sizes on the home screen grid.</summary>
    public enum AppSize
    {
        /// <summary>1 column × 1 row</summary>
        Size1x1,
        /// <summary>2 columns × 1 row</summary>
        Size2x1,
        /// <summary>2 columns × 2 rows</summary>
        Size2x2,
        /// <summary>3 columns × 2 rows</summary>
        Size3x2,
        /// <summary>4 columns × 2 rows</summary>
        Size4x2,
        /// <summary>4 columns × 3 rows</summary>
        Size4x3,
        /// <summary>4 columns × 4 rows</summary>
        Size4x4,
    }

    /// <summary>A single item stored in the layout (either an app or a folder).</summary>
    public class LayoutItem
    {
        /// <summary>App composite ID, or "__folder__" if this is a folder.</summary>
        public string AppId { get; set; } = string.Empty;
        public AppSize Size { get; set; } = AppSize.Size1x1;
        /// <summary>For folders: the display name.</summary>
        public string FolderName { get; set; } = string.Empty;
        /// <summary>For folders: the list of app IDs contained within.</summary>
        public List<string> FolderItems { get; set; } = new();

        public bool IsFolder => AppId == PhoneAppLayoutManager.FolderAppId;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Layout file serialization root
    // ──────────────────────────────────────────────────────────────────────────

    internal class PhoneLayoutData
    {
        public List<LayoutItem> Main { get; set; } = new();
        public List<string> Dock { get; set; } = new();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Dropdown menu state
    // ──────────────────────────────────────────────────────────────────────────

    internal enum DropdownOption
    {
        ChangeSize,
        ChangeSize1x1,
        ChangeSize2x1,
        ChangeSize2x2,
        ChangeSize3x2,
        ChangeSize4x2,
        ChangeSize4x3,
        ChangeSize4x4,
        RemoveFromDock,
        AddToDock,
        Close,
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Manager
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Manages the iOS-style grid layout for the phone home screen.
    /// Handles packing, drawing, drag-and-drop, reorder mode, folders, and
    /// JSON persistence.
    /// </summary>
    internal sealed class PhoneAppLayoutManager
    {
        // ── constants ──────────────────────────────────────────────────────

        public const string FolderAppId = "__folder__";

        // Grid geometry (base values, relative to phone content top-left unless specified)
        private const int GridCols = 4;
        private const int GridRows = 5;
        private const int GridCellSize = 114;      // px per cell (1x1)
        private const int GridStartX = 23;         // centered horizontally inside 520px content width ((520 - (4*114 + 3*6)) / 2)
        private const int GridStartY = 50;         // starting Y relative to content top (leaves 50px for status bar)
        private const int GridPadding = 6;         // gap between cells
        
        // Dock geometry
        private const int DockCellSize = 96;       // reduced from 112
        private const int DockPadding = 8;
        private const int DockCols = 4;
        private const int DockStartY = 702;        // centered vertically inside 120px dock height starting at Y=690 (690 + (120 - 96)/2)
        private const int AppIconPadding = 8;      // inset inside cell rect

        // Reorder mode visual constants
        private const float JiggleAmplitude = 1.2f;   // degrees
        private const float JiggleFrequency = 6f;     // oscillations / second

        // Done button
        private const int DoneButtonX = 340;
        private const int DoneButtonY = 40;
        private const int DoneButtonW = 130;
        private const int DoneButtonH = 44;

        // ── references ─────────────────────────────────────────────────────

        private readonly PhoneMenu _menu;

        // ── layout state ───────────────────────────────────────────────────

        /// <summary>All pages of main grid items (each page is a list of <see cref="LayoutItem"/>).</summary>
        private List<List<LayoutItem>> _pages = new();
        /// <summary>Dock: up to 4 app IDs (1x1 only, always visible).</summary>
        private List<string> _dock = new();

        // ── reorder mode ───────────────────────────────────────────────────

        public bool IsReorderMode { get; private set; }
        private double _jiggleElapsed;

        // drag state
        private bool _isDragging;
        private string? _dragAppId;
        private int _dragSourcePage;
        private int _dragSourceIndex;           // -1 = dragging from dock
        private bool _dragFromDock;
        private AppSize _dragAppSize;
        private int _dragMouseX, _dragMouseY;
        private int _dragOffsetX, _dragOffsetY;
        private string? _mergeTargetId;
        private int _hoverMainIndex = -1;
        private int _hoverDockIndex = -1;

        // click-hold and drag threshold state
        private bool _isDragCandidate;
        private bool _dragStarted;
        private string? _clickedAppId;
        private bool _clickedIsDock;
        private int _clickedIndex;
        private int _clickStartX;
        private int _clickStartY;
        private AppSize _clickedAppSize;

        // dropdown
        private bool _dropdownOpen;
        private string? _dropdownAppId;
        private bool _dropdownForDock;
        private int _dropdownForMainIndex = -1;
        private List<(DropdownOption option, Rectangle bounds, string label)> _dropdownItems = new();

        // folder view
        private LayoutItem? _openFolder;
        private List<Rectangle> _folderItemBounds = new();
        private Rectangle _folderOverlayBounds;

        // Done button bounds (scaled, in screen coords)
        private Rectangle _doneButtonBounds;
        // current page
        private int _currentPage;

        // click bounds used per frame
        private readonly Dictionary<string, Rectangle> _mainClickBounds = new();
        private readonly Dictionary<int, Rectangle> _dockClickBounds = new();
        private Rectangle _prevPageBounds = Rectangle.Empty;
        private Rectangle _nextPageBounds = Rectangle.Empty;

        // ── ctor ───────────────────────────────────────────────────────────

        public PhoneAppLayoutManager(PhoneMenu menu)
        {
            _menu = menu;
        }

        // ── public API ─────────────────────────────────────────────────────

        public void EnterReorderMode()
        {
            IsReorderMode = true;
            _jiggleElapsed = 0;
            _isDragging = false;
            _dropdownOpen = false;
            _openFolder = null;
        }

        public void ExitReorderMode()
        {
            IsReorderMode = false;
            _isDragging = false;
            _dropdownOpen = false;
            _openFolder = null;
            SaveLayout();
        }

        public int CurrentPage => _currentPage;

        // ── update ─────────────────────────────────────────────────────────

        public void Update(GameTime time)
        {
            if (IsReorderMode)
                _jiggleElapsed += time.ElapsedGameTime.TotalSeconds;
        }

        // ── draw ───────────────────────────────────────────────────────────

        public void DrawHomeScreen(SpriteBatch b)
        {
            _mainClickBounds.Clear();
            _dockClickBounds.Clear();
            _prevPageBounds = Rectangle.Empty;
            _nextPageBounds = Rectangle.Empty;

            // Refresh app snapshots to get current visible apps.
            List<HomeAppEntryProxy> allApps = BuildAllAppsSnapshot();

            // Sync layout to the current app list.
            SyncLayoutWithApps(allApps);

            // Draw dock divider line
            DrawDockDivider(b);

            // Draw dock
            DrawDock(b, allApps);

            // Draw current page
            if (_currentPage >= 0 && _currentPage < _pages.Count)
                DrawPage(b, _pages[_currentPage], allApps);

            // Draw page dots
            DrawPageIndicator(b);

            // Draw reorder UI on top
            if (IsReorderMode)
            {
                DrawDoneButton(b);
                if (_isDragging)
                    DrawDragGhost(b, allApps);
            }

            // Draw folder overlay
            if (_openFolder != null)
                DrawFolderOverlay(b, allApps);

            // Draw dropdown
            if (_dropdownOpen)
                DrawDropdown(b);
        }

        // ── input ──────────────────────────────────────────────────────────

        /// <summary>Called on left-click. Returns true if consumed.</summary>
        public bool ReceiveLeftClick(int x, int y)
        {
            // Done button
            if (IsReorderMode && _doneButtonBounds.Contains(x, y))
            {
                Game1.playSound("smallSelect");
                ExitReorderMode();
                return true;
            }

            // Close dropdown on click outside
            if (_dropdownOpen)
            {
                bool clickedInside = _dropdownItems.Any(item => item.bounds.Contains(x, y));
                if (clickedInside)
                {
                    HandleDropdownClick(x, y);
                    return true;
                }
                else
                {
                    _dropdownOpen = false;
                    return true;
                }
            }

            // Folder overlay
            if (_openFolder != null)
            {
                bool inOverlay = _folderOverlayBounds.Contains(x, y);
                if (!inOverlay)
                {
                    _openFolder = null;
                    return true;
                }
                // Click on item inside folder
                for (int i = 0; i < _folderItemBounds.Count; i++)
                {
                    if (_folderItemBounds[i].Contains(x, y))
                    {
                        string fid = _openFolder.FolderItems[i];
                        HandleAppLaunch(fid);
                        _openFolder = null;
                        return true;
                    }
                }
                return true;
            }

            // Page navigation
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

            // Reset drag candidate tracking
            _isDragCandidate = false;
            _dragStarted = false;

            // Dock clicks (check if we clicked a dock app first)
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

            // Main grid clicks (check if we clicked a main app)
            if (_currentPage >= 0 && _currentPage < _pages.Count)
            {
                List<LayoutItem> page = _pages[_currentPage];
                for (int i = 0; i < page.Count; i++)
                {
                    LayoutItem item = page[i];
                    if (!_mainClickBounds.TryGetValue(item.AppId + "_" + i, out Rectangle bounds))
                        continue;

                    if (!bounds.Contains(x, y)) continue;

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

            if (_isDragCandidate && !_dragStarted && IsReorderMode)
            {
                int dx = x - _clickStartX;
                int dy = y - _clickStartY;
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) >= 10)
                {
                    _dragStarted = true;
                    // Find the bounds for offset calculation
                    Rectangle itemBounds = Rectangle.Empty;
                    if (_clickedIsDock)
                    {
                        _dockClickBounds.TryGetValue(_clickedIndex, out itemBounds);
                    }
                    else
                    {
                        _mainClickBounds.TryGetValue(_clickedAppId + "_" + _clickedIndex, out itemBounds);
                    }

                    if (itemBounds != Rectangle.Empty)
                    {
                        BeginDrag(_clickedAppId!, _clickedAppSize, _clickedIsDock ? -1 : _currentPage, _clickedIndex, _clickedIsDock, _clickStartX, _clickStartY, itemBounds);
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
                _dragStarted = false;
                _isDragCandidate = false;
            }
            else if (_isDragCandidate)
            {
                _isDragCandidate = false;
                _dragStarted = false;

                if (IsReorderMode)
                {
                    if (!_clickedIsDock)
                    {
                        // Open dropdown for main grid app
                        if (_currentPage >= 0 && _currentPage < _pages.Count && _clickedIndex >= 0 && _clickedIndex < _pages[_currentPage].Count)
                        {
                            Rectangle bounds;
                            if (_mainClickBounds.TryGetValue(_clickedAppId + "_" + _clickedIndex, out bounds))
                            {
                                OpenDropdownForMain(_clickedIndex, bounds);
                            }
                        }
                    }
                }
                else
                {
                    // Normal mode launch
                    if (_clickedIsDock)
                    {
                        HandleAppLaunch(_clickedAppId!);
                    }
                    else
                    {
                        if (_currentPage >= 0 && _currentPage < _pages.Count && _clickedIndex >= 0 && _clickedIndex < _pages[_currentPage].Count)
                        {
                            LayoutItem item = _pages[_currentPage][_clickedIndex];
                            if (item.IsFolder)
                            {
                                Rectangle bounds;
                                _mainClickBounds.TryGetValue(item.AppId + "_" + _clickedIndex, out bounds);
                                OpenFolderOverlay(item, bounds);
                            }
                            else
                            {
                                HandleAppLaunch(_clickedAppId!);
                            }
                        }
                    }
                }
            }
        }

        public bool TryChangePageScroll(int delta)
        {
            if (_dropdownOpen || _openFolder != null)
                return false;

            int total = _pages.Count;
            int next = Math.Clamp(_currentPage + delta, 0, Math.Max(0, total - 1));
            if (next == _currentPage) return false;
            _currentPage = next;
            return true;
        }

        // ── persistence ────────────────────────────────────────────────────

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
                // Rebuild pages from flat main list
                RebuildPagesFromFlat(data.Main ?? new List<LayoutItem>());
                SyncLayoutWithApps(allApps);
            }
            catch (Exception ex)
            {
                ModEntry.SMonitor?.Log($"[PhoneAppLayoutManager] Failed to load layout: {ex.Message}", LogLevel.Warn);
                BuildDefaultLayout(allApps);
            }
        }

        public void SaveLayout()
        {
            try
            {
                List<LayoutItem> flat = _pages.SelectMany(p => p).ToList();
                PhoneLayoutData data = new PhoneLayoutData
                {
                    Main = flat,
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
                ModEntry.SMonitor?.Log($"[PhoneAppLayoutManager] Failed to save layout: {ex.Message}", LogLevel.Warn);
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

        // ── layout building ────────────────────────────────────────────────

        private void BuildDefaultLayout(List<HomeAppEntryProxy> allApps)
        {
            _dock.Clear();
            _pages.Clear();

            // Put first 4 builtin apps in dock
            var builtins = allApps.Where(a => IsBuiltinApp(a.Id)).Take(DockCols).ToList();
            foreach (var app in builtins)
                _dock.Add(app.Id);

            // Remaining apps go to main grid
            var remaining = allApps.Where(a => !_dock.Contains(a.Id)).ToList();
            List<LayoutItem> flat = remaining.Select(a => new LayoutItem
            {
                AppId = a.Id,
                Size = AppSize.Size1x1
            }).ToList();

            RebuildPagesFromFlat(flat);
        }

        private bool IsBuiltinApp(string id)
        {
            return id.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase);
        }

        private void RebuildPagesFromFlat(List<LayoutItem> flat)
        {
            _pages.Clear();
            List<LayoutItem> current = new();
            int usedCells = 0;

            foreach (LayoutItem item in flat)
            {
                int cells = GetCellCount(item.Size);
                if (!FitsOnPage(usedCells, item.Size, GridCols, GridRows))
                {
                    _pages.Add(current);
                    current = new List<LayoutItem>();
                    usedCells = 0;
                }
                current.Add(item);
                usedCells += cells;
            }

            if (current.Count > 0 || _pages.Count == 0)
                _pages.Add(current);
        }

        private bool FitsOnPage(int usedCells, AppSize size, int cols, int rows)
        {
            return usedCells + GetCellCount(size) <= cols * rows;
        }

        /// <summary>Sync: add new apps that don't exist in layout; remove apps no longer registered.</summary>
        private void SyncLayoutWithApps(List<HomeAppEntryProxy> allApps)
        {
            HashSet<string> allIds = new(allApps.Select(a => a.Id), StringComparer.OrdinalIgnoreCase);
            HashSet<string> layoutIds = new(StringComparer.OrdinalIgnoreCase);

            // Collect what's in layout
            foreach (string id in _dock) layoutIds.Add(id);
            foreach (List<LayoutItem> page in _pages)
                foreach (LayoutItem item in page)
                    layoutIds.Add(item.AppId);

            // Remove dock items no longer visible
            _dock.RemoveAll(id => !allIds.Contains(id));

            // Remove main grid items no longer visible (but keep folders)
            foreach (List<LayoutItem> page in _pages)
                page.RemoveAll(item => !item.IsFolder && !allIds.Contains(item.AppId));

            // Clean up empty pages (except at least one page always exists)
            _pages.RemoveAll(p => p.Count == 0);
            if (_pages.Count == 0) _pages.Add(new List<LayoutItem>());

            // Add new apps that aren't in layout yet
            var newApps = allApps.Where(a => !layoutIds.Contains(a.Id));
            List<LayoutItem> lastPage = _pages[_pages.Count - 1];
            foreach (HomeAppEntryProxy app in newApps)
            {
                LayoutItem newItem = new LayoutItem
                {
                    AppId = app.Id,
                    Size = AppSize.Size1x1
                };

                if (!FitsOnPage(CountPageCells(lastPage), AppSize.Size1x1, GridCols, GridRows))
                {
                    lastPage = new List<LayoutItem>();
                    _pages.Add(lastPage);
                }
                lastPage.Add(newItem);
            }

            _currentPage = Math.Clamp(_currentPage, 0, Math.Max(0, _pages.Count - 1));
        }

        private int CountPageCells(List<LayoutItem> page)
        {
            return page.Sum(item => GetCellCount(item.Size));
        }

        // ── drawing helpers ────────────────────────────────────────────────

        private void DrawDock(SpriteBatch b, List<HomeAppEntryProxy> allApps)
        {
            Rectangle contentRect = _menu.GetPhoneContentBounds();
            // Draw gray translucent dock background (from Y=690 to Y=810, height 120 base)
            Rectangle dockBgRect = new Rectangle(contentRect.X, contentRect.Y + ScaleUi(690), contentRect.Width, ScaleUi(120));
            b.Draw(Game1.staminaRect, dockBgRect, new Color(80, 80, 80, 180));

            int count = _dock.Count;
            for (int i = 0; i < count; i++)
            {
                Rectangle cellRect = GetDockCellRect(i, count);
                _dockClickBounds[i] = cellRect;

                string appId = _dock[i];
                if (string.IsNullOrEmpty(appId)) continue;

                HomeAppEntryProxy? app = FindApp(allApps, appId);
                if (app == null) continue;

                if (IsReorderMode && _isDragging && _dragFromDock && _dragSourceIndex == i)
                    continue; // skip ghost origin

                float jiggle = IsReorderMode ? GetJiggleAngle(i * 73) : 0f;
                DrawAppCell(b, app, cellRect, AppSize.Size1x1, jiggle, isMergeTarget: appId == _mergeTargetId);
            }
        }

        private void DrawPage(SpriteBatch b, List<LayoutItem> page, List<HomeAppEntryProxy> allApps)
        {
            int col = 0, row = 0;
            for (int i = 0; i < page.Count; i++)
            {
                LayoutItem item = page[i];
                (int w, int h) = GetSizeDims(item.Size);

                // Advance past filled rows/cols
                AdvanceGridCursor(ref col, ref row, w, h);

                Rectangle cellRect = GetMainCellRect(col, row, w, h);
                string key = item.AppId + "_" + i;
                _mainClickBounds[key] = cellRect;

                if (IsReorderMode && _isDragging && !_dragFromDock
                    && _dragSourcePage == _currentPage && _dragSourceIndex == i)
                {
                    // Skip drawing ghost origin
                    col += w;
                    if (col >= GridCols) { col = 0; row += h; }
                    continue;
                }

                float jiggle = IsReorderMode ? GetJiggleAngle((i + 13) * 37) : 0f;
                bool isMerge = !string.IsNullOrEmpty(_mergeTargetId)
                               && (item.AppId == _mergeTargetId);

                if (item.IsFolder)
                {
                    DrawFolderCell(b, item, cellRect, jiggle);
                }
                else
                {
                    HomeAppEntryProxy? app = FindApp(allApps, item.AppId);
                    if (app != null)
                        DrawAppCell(b, app, cellRect, item.Size, jiggle, isMerge);
                }

                col += w;
                if (col >= GridCols) { col = 0; row += h; }
            }
        }

        private void DrawAppCell(SpriteBatch b, HomeAppEntryProxy app, Rectangle cellRect,
            AppSize size, float jiggleDeg, bool isMergeTarget)
        {
            Rectangle iconRect = new Rectangle(
                cellRect.X + ScaleUi(AppIconPadding),
                cellRect.Y + ScaleUi(AppIconPadding),
                Math.Max(1, cellRect.Width - ScaleUi(AppIconPadding * 2)),
                Math.Max(1, cellRect.Height - ScaleUi(AppIconPadding * 2)));

            // Merge-target highlight
            if (isMergeTarget)
            {
                b.Draw(Game1.staminaRect, cellRect, Color.White * 0.35f);
            }

            // Draw icon with jiggle transform
            DrawIconWithJiggle(b, app, iconRect, jiggleDeg);

            // Badge
            if (app.GetBadgeCount != null)
            {
                try
                {
                    int badge = app.GetBadgeCount();
                    if (badge > 0)
                        DrawBadge(b, iconRect, badge);
                }
                catch { }
            }

            // Label (only for 1x1 on normal mode, or always in reorder mode for larger sizes)
            if (size == AppSize.Size1x1 || IsReorderMode)
                DrawAppLabel(b, iconRect, app.DisplayName);
        }

        private void DrawFolderCell(SpriteBatch b, LayoutItem folder, Rectangle cellRect, float jiggleDeg)
        {
            // Draw folder background
            Rectangle innerRect = new Rectangle(
                cellRect.X + ScaleUi(4),
                cellRect.Y + ScaleUi(4),
                Math.Max(1, cellRect.Width - ScaleUi(8)),
                Math.Max(1, cellRect.Height - ScaleUi(8)));

            b.Draw(Game1.staminaRect, innerRect, Color.DarkSlateBlue * 0.6f);

            // Draw label
            DrawAppLabel(b, cellRect, folder.FolderName);
        }

        private void DrawIconWithJiggle(SpriteBatch b, HomeAppEntryProxy app, Rectangle iconRect, float jiggleDeg)
        {
            if (Math.Abs(jiggleDeg) < 0.001f)
            {
                // No jiggle, direct draw
                DrawAppIcon(b, app.IconTexture, iconRect, app.SourceRect);
                return;
            }

            // We can't rotate with SpriteBatch without restarting the batch.
            // For simplicity, draw without rotation but with a slight offset to simulate jiggle.
            float offsetX = (float)(Math.Sin(jiggleDeg * MathHelper.Pi / 180.0) * 2);
            Rectangle shifted = new Rectangle(iconRect.X + (int)offsetX, iconRect.Y, iconRect.Width, iconRect.Height);
            DrawAppIcon(b, app.IconTexture, shifted, app.SourceRect);
        }

        private void DrawAppIcon(SpriteBatch b, Texture2D texture, Rectangle bounds, Rectangle? sourceRect)
        {
            if (texture == null) return;

            Rectangle textureBounds = new Rectangle(0, 0, texture.Width, texture.Height);
            Rectangle source = sourceRect.HasValue
                ? Rectangle.Intersect(sourceRect.Value, textureBounds)
                : textureBounds;
            if (source.Width <= 0 || source.Height <= 0) source = textureBounds;

            float scale = Math.Min(bounds.Width / (float)source.Width, bounds.Height / (float)source.Height);
            int dw = Math.Max(1, (int)Math.Round(source.Width * scale));
            int dh = Math.Max(1, (int)Math.Round(source.Height * scale));
            Rectangle drawRect = new Rectangle(
                bounds.X + (bounds.Width - dw) / 2,
                bounds.Y + (bounds.Height - dh) / 2,
                dw, dh);

            b.Draw(texture, drawRect, source, Color.White);
        }

        private void DrawAppLabel(SpriteBatch b, Rectangle appBounds, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            SpriteFont labelFont = Game1.smallFont;
            float labelScale = _menu.PhoneUiScale < 0.999f ? 0.55f : 0.65f;
            Vector2 labelSize = labelFont.MeasureString(name) * labelScale;
            Vector2 labelPos = new Vector2(
                appBounds.X + (appBounds.Width - labelSize.X) / 2f,
                appBounds.Bottom + ScaleUi(3));

            b.DrawString(labelFont, name, labelPos, GetHomeTextColor(), 0f, Vector2.Zero, labelScale, SpriteEffects.None, 1f);
        }

        private void DrawBadge(SpriteBatch b, Rectangle iconRect, int badgeCount)
        {
            string text = Math.Min(99, badgeCount).ToString();
            Vector2 textSize = Game1.smallFont.MeasureString(text);
            int bw = Math.Max(ScaleUi(24), (int)textSize.X + ScaleUi(10));
            int bh = Math.Max(ScaleUi(18), (int)textSize.Y + ScaleUi(4));
            int bx = iconRect.Right - bw / 2 - ScaleUi(6);
            int by = iconRect.Top - bh / 2 + ScaleUi(6);

            IClickableMenu.drawTextureBox(b, Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                bx, by, bw, bh,
                new Color(255, 0, 0, 200), 1f, false);

            b.DrawString(Game1.smallFont, text,
                new Vector2(bx + (bw - textSize.X) / 2f, by + (bh - textSize.Y) / 2f),
                Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 1f);
        }

        private void DrawDockDivider(SpriteBatch b)
        {
            Rectangle contentBounds = _menu.GetPhoneContentBounds();
            int divY = contentBounds.Y + ScaleUi(690);
            // Draw a subtle light border line at the top edge of the gray dock background
            b.Draw(Game1.staminaRect,
                new Rectangle(contentBounds.X, divY, contentBounds.Width, ScaleUi(1)),
                Color.White * 0.25f);
        }

        private void DrawPageIndicator(SpriteBatch b)
        {
            if (_pages.Count <= 1) return;

            Rectangle contentBounds = _menu.GetPhoneContentBounds();
            int dotSize = ScaleUi(10);
            int dotSpacing = ScaleUi(16);
            int totalW = _pages.Count * dotSpacing;
            int startX = contentBounds.X + ScaleUi(260) - totalW / 2; // Center horizontally relative to content width (520 / 2 = 260)
            int dotY = contentBounds.Y + ScaleUi(690 - 22); // 22px above dock background

            for (int i = 0; i < _pages.Count; i++)
            {
                Color dotColor = i == _currentPage ? Color.White : Color.White * 0.4f;
                Rectangle dotRect = new Rectangle(startX + i * dotSpacing, dotY, dotSize, dotSize);
                b.Draw(Game1.staminaRect, dotRect, dotColor);
            }

            // Prev/next buttons for accessibility
            _prevPageBounds = new Rectangle(
                contentBounds.X + ScaleUi(10),
                contentBounds.Y + ScaleUi(690 - 28),
                ScaleUi(50), ScaleUi(30));
            _nextPageBounds = new Rectangle(
                contentBounds.X + contentBounds.Width - ScaleUi(60),
                contentBounds.Y + ScaleUi(690 - 28),
                ScaleUi(50), ScaleUi(30));
        }

        private void DrawDoneButton(SpriteBatch b)
        {
            _doneButtonBounds = new Rectangle(
                _menu.xPositionOnScreen + ScaleUi(DoneButtonX),
                _menu.yPositionOnScreen + ScaleUi(DoneButtonY),
                ScaleUi(DoneButtonW),
                ScaleUi(DoneButtonH));

            IClickableMenu.drawTextureBox(b, Game1.menuTexture,
                new Rectangle(0, 256, 60, 60),
                _doneButtonBounds.X, _doneButtonBounds.Y,
                _doneButtonBounds.Width, _doneButtonBounds.Height,
                new Color(80, 200, 120, 230), 1f, false);

            string label = ModEntry.SHelper.Translation.Get("ui.reorder.done").Default("Done");
            Vector2 labelSize = Game1.smallFont.MeasureString(label);
            float labelScale = 0.75f;
            b.DrawString(Game1.smallFont, label,
                new Vector2(
                    _doneButtonBounds.X + (_doneButtonBounds.Width - labelSize.X * labelScale) / 2f,
                    _doneButtonBounds.Y + (_doneButtonBounds.Height - labelSize.Y * labelScale) / 2f),
                Color.White, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 1f);
        }

        private void DrawDragGhost(SpriteBatch b, List<HomeAppEntryProxy> allApps)
        {
            if (string.IsNullOrEmpty(_dragAppId)) return;
            HomeAppEntryProxy? app = FindApp(allApps, _dragAppId);
            if (app == null) return;

            (int w, int h) = GetSizeDims(_dragAppSize);
            int ghostW = ScaleUi(w * GridCellSize + (w - 1) * GridPadding);
            int ghostH = ScaleUi(h * GridCellSize + (h - 1) * GridPadding);
            Rectangle ghostRect = new Rectangle(
                _dragMouseX - _dragOffsetX,
                _dragMouseY - _dragOffsetY,
                ghostW, ghostH);

            // Semi-transparent ghost
            DrawAppIcon(b, app.IconTexture,
                new Rectangle(ghostRect.X + ScaleUi(4), ghostRect.Y + ScaleUi(4),
                    ghostRect.Width - ScaleUi(8), ghostRect.Height - ScaleUi(8)),
                app.SourceRect);
            b.Draw(Game1.staminaRect, ghostRect, Color.White * 0.2f);
        }

        private void DrawFolderOverlay(SpriteBatch b, List<HomeAppEntryProxy> allApps)
        {
            if (_openFolder == null) return;

            // Dim background
            b.Draw(Game1.staminaRect, _menu.GetPhoneContentBounds(), Color.Black * 0.55f);

            // Folder panel
            _folderOverlayBounds = new Rectangle(
                _menu.xPositionOnScreen + ScaleUi(40),
                _menu.yPositionOnScreen + ScaleUi(200),
                ScaleUi(520),
                ScaleUi(400));
            b.Draw(Game1.staminaRect, _folderOverlayBounds, new Color(40, 40, 80, 220));

            // Folder name
            string folderName = _openFolder.FolderName ?? "Folder";
            float nameScale = _menu.PhoneUiScale < 0.999f ? 0.7f : 0.85f;
            Vector2 nameSize = Game1.dialogueFont.MeasureString(folderName) * nameScale;
            b.DrawString(Game1.dialogueFont, folderName,
                new Vector2(_folderOverlayBounds.X + (_folderOverlayBounds.Width - nameSize.X) / 2f,
                    _folderOverlayBounds.Y + ScaleUi(12)),
                Color.White, 0f, Vector2.Zero, nameScale, SpriteEffects.None, 1f);

            // Items in folder
            _folderItemBounds.Clear();
            int folderCols = 4;
            int folderCellSize = ScaleUi(90);
            int folderPad = ScaleUi(8);
            int startX = _folderOverlayBounds.X + ScaleUi(18);
            int startY = _folderOverlayBounds.Y + ScaleUi(70);

            for (int i = 0; i < _openFolder.FolderItems.Count; i++)
            {
                string fid = _openFolder.FolderItems[i];
                HomeAppEntryProxy? fapp = FindApp(allApps, fid);
                int col = i % folderCols;
                int row = i / folderCols;
                Rectangle itemRect = new Rectangle(
                    startX + col * (folderCellSize + folderPad),
                    startY + row * (folderCellSize + folderPad + ScaleUi(16)),
                    folderCellSize, folderCellSize);

                _folderItemBounds.Add(itemRect);
                if (fapp != null)
                {
                    DrawAppIcon(b, fapp.IconTexture,
                        new Rectangle(itemRect.X + ScaleUi(6), itemRect.Y + ScaleUi(6),
                            itemRect.Width - ScaleUi(12), itemRect.Height - ScaleUi(12)),
                        fapp.SourceRect);
                    DrawAppLabel(b, itemRect, fapp.DisplayName);
                }
            }
        }

        private void DrawDropdown(SpriteBatch b)
        {
            if (!_dropdownOpen || _dropdownItems.Count == 0) return;

            foreach ((DropdownOption opt, Rectangle bounds, string label) in _dropdownItems)
            {
                IClickableMenu.drawTextureBox(b, Game1.menuTexture,
                    new Rectangle(0, 256, 60, 60),
                    bounds.X, bounds.Y, bounds.Width, bounds.Height,
                    new Color(240, 240, 240, 235), 1f, false);

                float textScale = 0.65f;
                Vector2 textSize = Game1.dialogueFont.MeasureString(label) * textScale;
                b.DrawString(Game1.dialogueFont, label,
                    new Vector2(bounds.X + (bounds.Width - textSize.X) / 2f,
                        bounds.Y + (bounds.Height - textSize.Y) / 2f),
                    Color.Black, 0f, Vector2.Zero, textScale, SpriteEffects.None, 1f);
            }
        }

        // ── dropdown ───────────────────────────────────────────────────────

        private void OpenDropdownForMain(int index, Rectangle appBounds)
        {
            _dropdownAppId = _pages[_currentPage][index].AppId;
            _dropdownForDock = false;
            _dropdownForMainIndex = index;
            BuildDropdownItems(appBounds);
            _dropdownOpen = true;
        }

        private void OpenDropdownForDock(int index, Rectangle appBounds)
        {
            _dropdownAppId = _dock[index];
            _dropdownForDock = true;
            _dropdownForMainIndex = index;
            BuildDropdownItems(appBounds);
            _dropdownOpen = true;
        }

        private void BuildDropdownItems(Rectangle anchorBounds)
        {
            _dropdownItems.Clear();
            int itemH = ScaleUi(42);
            int itemW = ScaleUi(200);
            int x = anchorBounds.X;
            int y = anchorBounds.Bottom + ScaleUi(4);

            // Clamp to screen
            Rectangle phoneBounds = _menu.GetPhoneContentBounds();
            if (x + itemW > phoneBounds.Right) x = phoneBounds.Right - itemW;
            if (x < phoneBounds.Left) x = phoneBounds.Left;

            List<(DropdownOption, string)> options = new List<(DropdownOption, string)>
            {
                (DropdownOption.ChangeSize1x1, "1×1"),
                (DropdownOption.ChangeSize2x1, "2×1"),
                (DropdownOption.ChangeSize2x2, "2×2"),
                (DropdownOption.ChangeSize3x2, "3×2"),
                (DropdownOption.ChangeSize4x2, "4×2"),
                (DropdownOption.ChangeSize4x3, "4×3"),
                (DropdownOption.ChangeSize4x4, "4×4"),
                (DropdownOption.Close, ModEntry.SHelper.Translation.Get("ui.reorder.close").Default("Cancel")),
            };

            // Ensure dropdown doesn't go off bottom
            int totalH = options.Count * (itemH + ScaleUi(2));
            if (y + totalH > phoneBounds.Bottom)
                y = Math.Max(anchorBounds.Top - totalH - ScaleUi(4), phoneBounds.Top);

            for (int i = 0; i < options.Count; i++)
            {
                (DropdownOption opt, string label) = options[i];
                Rectangle itemBounds = new Rectangle(x, y + i * (itemH + ScaleUi(2)), itemW, itemH);
                _dropdownItems.Add((opt, itemBounds, label));
            }
        }

        private void HandleDropdownClick(int x, int y)
        {
            foreach ((DropdownOption opt, Rectangle bounds, _) in _dropdownItems)
            {
                if (!bounds.Contains(x, y)) continue;

                switch (opt)
                {
                    case DropdownOption.ChangeSize1x1: ApplySize(AppSize.Size1x1); break;
                    case DropdownOption.ChangeSize2x1: ApplySize(AppSize.Size2x1); break;
                    case DropdownOption.ChangeSize2x2: ApplySize(AppSize.Size2x2); break;
                    case DropdownOption.ChangeSize3x2: ApplySize(AppSize.Size3x2); break;
                    case DropdownOption.ChangeSize4x2: ApplySize(AppSize.Size4x2); break;
                    case DropdownOption.ChangeSize4x3: ApplySize(AppSize.Size4x3); break;
                    case DropdownOption.ChangeSize4x4: ApplySize(AppSize.Size4x4); break;

                    case DropdownOption.Close:
                        _dropdownOpen = false;
                        _dropdownItems.Clear();
                        break;
                }

                if (opt != DropdownOption.Close)
                {
                    _dropdownOpen = false;
                    _dropdownItems.Clear();
                    Game1.playSound("smallSelect");
                }
                return;
            }
        }

        private void ApplySize(AppSize newSize)
        {
            if (_dropdownForDock || _currentPage >= _pages.Count) return;
            if (_dropdownForMainIndex < 0 || _dropdownForMainIndex >= _pages[_currentPage].Count) return;

            _pages[_currentPage][_dropdownForMainIndex].Size = newSize;
            // Re-pack pages to handle overflow
            List<LayoutItem> flat = _pages.SelectMany(p => p).ToList();
            RebuildPagesFromFlat(flat);
        }

        private void AddAppToDock(string? appId)
        {
            if (string.IsNullOrEmpty(appId)) return;
            if (_dock.Contains(appId, StringComparer.OrdinalIgnoreCase)) return;
            if (_dock.Count >= DockCols) return;
            _dock.Add(appId!);
        }

        private void RemoveAppFromDock(int dockIndex)
        {
            if (dockIndex < 0 || dockIndex >= _dock.Count) return;
            string appId = _dock[dockIndex];
            _dock.RemoveAt(dockIndex);

            // Move app back to main grid
            List<LayoutItem> lastPage = _pages.Last();
            LayoutItem item = new LayoutItem { AppId = appId, Size = AppSize.Size1x1 };
            if (!FitsOnPage(CountPageCells(lastPage), AppSize.Size1x1, GridCols, GridRows))
            {
                lastPage = new List<LayoutItem>();
                _pages.Add(lastPage);
            }
            lastPage.Add(item);
        }

        // ── drag & drop ────────────────────────────────────────────────────

        private void BeginDrag(string appId, AppSize size, int sourcePage, int sourceIndex,
            bool isDock, int mouseX, int mouseY, Rectangle itemBounds)
        {
            _isDragging = true;
            _dragAppId = appId;
            _dragAppSize = size;
            _dragSourcePage = sourcePage;
            _dragSourceIndex = sourceIndex;
            _dragFromDock = isDock;
            _dragMouseX = mouseX;
            _dragMouseY = mouseY;
            _dragOffsetX = mouseX - itemBounds.X;
            _dragOffsetY = mouseY - itemBounds.Y;
            _mergeTargetId = null;
            Game1.playSound("smallSelect");
        }

        private void UpdateDragHover(int x, int y)
        {
            _mergeTargetId = null;
            _hoverMainIndex = -1;
            _hoverDockIndex = -1;

            // Check dock hover
            int activeDockCount = _dock.Count;
            for (int i = 0; i < activeDockCount; i++)
            {
                if (_dockClickBounds.TryGetValue(i, out Rectangle r) && r.Contains(x, y))
                {
                    _hoverDockIndex = i;
                    _mergeTargetId = _dock[i];
                    return;
                }
            }

            // General dock area hover (if we didn't hit a specific app but are inside the dock bar, and dock is not full)
            if (_isDragging && activeDockCount < DockCols)
            {
                Rectangle contentRect = _menu.GetPhoneContentBounds();
                Rectangle dockBgRect = new Rectangle(contentRect.X, contentRect.Y + ScaleUi(690), contentRect.Width, ScaleUi(120));
                if (dockBgRect.Contains(x, y))
                {
                    _hoverDockIndex = activeDockCount;
                    return;
                }
            }

            // Check main grid hover
            if (_currentPage < _pages.Count)
            {
                List<LayoutItem> page = _pages[_currentPage];
                for (int i = 0; i < page.Count; i++)
                {
                    string key = page[i].AppId + "_" + i;
                    if (_mainClickBounds.TryGetValue(key, out Rectangle r) && r.Contains(x, y))
                    {
                        _hoverMainIndex = i;
                        _mergeTargetId = page[i].AppId;
                        return;
                    }
                }
            }
        }

        private void DropDraggedItem(int x, int y)
        {
            if (string.IsNullOrEmpty(_dragAppId)) return;

            // Determine drop target
            bool droppedOnDock = _hoverDockIndex >= 0;
            bool droppedOnMain = _hoverMainIndex >= 0;

            // Remove from source
            if (_dragFromDock && _dragSourceIndex >= 0 && _dragSourceIndex < _dock.Count)
            {
                _dock.RemoveAt(_dragSourceIndex);
            }
            else if (_dragSourcePage >= 0 && _dragSourcePage < _pages.Count)
            {
                if (_dragSourceIndex >= 0 && _dragSourceIndex < _pages[_dragSourcePage].Count)
                    _pages[_dragSourcePage].RemoveAt(_dragSourceIndex);
            }

            if (droppedOnDock)
            {
                // Dropped on dock slot
                string? targetId = _hoverDockIndex < _dock.Count ? _dock[_hoverDockIndex] : null;

                if (!string.IsNullOrEmpty(targetId) && targetId != _dragAppId)
                {
                    // Swap dock item with drag source
                    _dock[_hoverDockIndex] = _dragAppId!;
                    // Move bumped app to main grid
                    AddToMainGrid(targetId);
                }
                else if (!string.IsNullOrEmpty(targetId))
                {
                    // Same app, just re-insert
                    _dock.Insert(Math.Clamp(_hoverDockIndex, 0, _dock.Count), _dragAppId!);
                }
                else
                {
                    // Empty slot
                    while (_dock.Count <= _hoverDockIndex) _dock.Add(string.Empty);
                    _dock[_hoverDockIndex] = _dragAppId!;
                }
            }
            else if (droppedOnMain && _currentPage < _pages.Count)
            {
                List<LayoutItem> page = _pages[_currentPage];
                if (_hoverMainIndex < page.Count)
                {
                    // Insert before target (push target right)
                    page.Insert(_hoverMainIndex, new LayoutItem { AppId = _dragAppId!, Size = _dragAppSize });
                }
                else
                {
                    page.Add(new LayoutItem { AppId = _dragAppId!, Size = _dragAppSize });
                }
            }
            else
            {
                // Dropped in empty space — append to current page or new page
                AddToMainGrid(_dragAppId!, _dragAppSize);
            }

            // Clean up and re-pack
            _dock.RemoveAll(string.IsNullOrEmpty);
            List<LayoutItem> flat = _pages.SelectMany(p => p).ToList();
            RebuildPagesFromFlat(flat);

            _mergeTargetId = null;
            _hoverMainIndex = -1;
            _hoverDockIndex = -1;
        }

        private void AddToMainGrid(string appId, AppSize size = AppSize.Size1x1)
        {
            if (_pages.Count == 0) _pages.Add(new List<LayoutItem>());
            List<LayoutItem> last = _pages.Last();
            LayoutItem item = new LayoutItem { AppId = appId, Size = size };
            if (!FitsOnPage(CountPageCells(last), size, GridCols, GridRows))
            {
                last = new List<LayoutItem>();
                _pages.Add(last);
            }
            last.Add(item);
        }

        // ── folder ─────────────────────────────────────────────────────────

        private void OpenFolderOverlay(LayoutItem folder, Rectangle bounds)
        {
            _openFolder = folder;
            _folderItemBounds.Clear();
            Game1.playSound("smallSelect");
        }

        // ── app launch ─────────────────────────────────────────────────────

        private void HandleAppLaunch(string appId)
        {
            _menu.TryHandleHomeAppClickPublic(appId);
        }

        // ── geometry helpers ───────────────────────────────────────────────

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

        private Rectangle GetDockCellRect(int col) => GetDockCellRect(col, _dock.Count);

        private void AdvanceGridCursor(ref int col, ref int row, int w, int h)
        {
            // If the item doesn't fit in the remaining columns of this row, move to next row
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
            // Try to get from setting
            try
            {
                // Access current color through the PhoneMenu's text color helper
                return _menu.GetHomeTextColorPublic();
            }
            catch
            {
                return Color.White;
            }
        }

        // ── static size helpers ────────────────────────────────────────────

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

        // ── snapshot ───────────────────────────────────────────────────────

        private List<HomeAppEntryProxy> BuildAllAppsSnapshot()
        {
            return _menu.BuildHomeAppsSnapshotPublic();
        }

        private static HomeAppEntryProxy? FindApp(List<HomeAppEntryProxy> apps, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return apps.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Public proxy for home app entry (to avoid partial class leakage)
    // ──────────────────────────────────────────────────────────────────────────

    internal class HomeAppEntryProxy
    {
        public string Id { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public Texture2D IconTexture { get; init; } = null!;
        public Rectangle? SourceRect { get; init; }
        public Func<int>? GetBadgeCount { get; init; }
    }
}
