using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace CRT
{
    public partial class Main : Window
    {
        // Zoom
        private Matrix _schematicsMatrix = Matrix.Identity;

        // Thumbnails
        private List<SchematicThumbnail> _currentThumbnails = [];

        // Full-res viewer
        private Bitmap? _currentFullResBitmap;
        private CancellationTokenSource? _fullResLoadCts;

        // Panning
        private bool _isPanning;
        private Point _panStartPoint;
        private Matrix _panStartMatrix;

        // Highlights
        private Dictionary<string, HighlightSpatialIndex> _highlightIndexBySchematic = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, BoardSchematicEntry> _schematicByName = new(StringComparer.OrdinalIgnoreCase);

        // Highlight rects per schematic per board label — built at board load, used for on-demand highlighting
        private Dictionary<string, Dictionary<string, List<Rect>>> _highlightRectsBySchematicAndLabel = new(StringComparer.OrdinalIgnoreCase);

        // Window placement: tracks the last known normal-state size and position
        private double _restoreWidth;
        private double _restoreHeight;
        private PixelPoint _restorePosition;
        private DispatcherTimer? _windowPlacementSaveTimer;

        // Category filter: suppresses saves during programmatic selection changes
        private bool _suppressCategoryFilterSave;

        private BoardData? _currentBoardData;
        private bool _suppressComponentHighlightUpdate;

        public Main()
        {
            InitializeComponent();

            // Restore left panel width from settings
            this.RootGrid.ColumnDefinitions[0].Width = new GridLength(UserSettings.LeftPanelWidth);
            this.RootGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);

            // Subscribe to splitter pointer-release to persist positions when a drag ends.
            // handledEventsToo: true is required because GridSplitter marks the event as handled.
            this.MainSplitter.AddHandler(
                InputElement.PointerReleasedEvent,
                this.OnMainSplitterPointerReleased,
                RoutingStrategies.Bubble,
                handledEventsToo: true);

            this.SchematicsSplitter.AddHandler(
                InputElement.PointerReleasedEvent,
                this.OnSchematicsSplitterPointerReleased,
                RoutingStrategies.Bubble,
                handledEventsToo: true);

            // Initialize restore values from settings, then apply window placement before Show()
            // so Normal windows appear at the right place/size with zero flicker.
            // Maximized windows are positioned on the saved screen before being maximized so the
            // OS maximizes them on the correct monitor.
            _restoreWidth = Math.Max(this.MinWidth, UserSettings.WindowWidth);
            _restoreHeight = Math.Max(this.MinHeight, UserSettings.WindowHeight);
            _restorePosition = new PixelPoint(UserSettings.WindowX, UserSettings.WindowY);

            if (UserSettings.HasWindowPlacement)
            {
                this.WindowStartupLocation = WindowStartupLocation.Manual;
                this.Width = _restoreWidth;
                this.Height = _restoreHeight;

                if (UserSettings.WindowState == nameof(Avalonia.Controls.WindowState.Maximized))
                {
                    // Place anywhere on the saved screen so the OS maximizes it there
                    this.Position = new PixelPoint(UserSettings.WindowScreenX + 100, UserSettings.WindowScreenY + 100);
                    this.WindowState = Avalonia.Controls.WindowState.Maximized;
                }
                else
                {
                    this.Position = _restorePosition;
                }
            }

            this.Opened += this.OnWindowFirstOpened;
            this.Closing += this.OnWindowClosing;

            // Align the visual transform origin with the top-left coordinate system used in ClampSchematicsMatrix
            this.SchematicsImage.RenderTransformOrigin = RelativePoint.TopLeft;
            this.SchematicsHighlightsOverlay.RenderTransformOrigin = RelativePoint.TopLeft;

            // Keep highlights correct after layout changes (e.g. splitter drags)
            this.SchematicsContainer.PropertyChanged += (s, e) =>
            {
                if (e.Property == Visual.BoundsProperty)
                    this.ClampSchematicsMatrix();
            };

            // Also clamp when the individual image object updates its logical dimensions
            this.SchematicsImage.PropertyChanged += (s, e) =>
            {
                if (e.Property == Visual.BoundsProperty)
                    this.ClampSchematicsMatrix();
            };

            this.SubscribePanelSizeChanges();
            this.HardwareComboBox.SelectionChanged += this.OnHardwareSelectionChanged;
            this.BoardComboBox.SelectionChanged += this.OnBoardSelectionChanged;
            this.SchematicsThumbnailList.SelectionChanged += this.OnSchematicsThumbnailSelectionChanged;
            this.CategoryFilterListBox.SelectionChanged += this.OnCategoryFilterSelectionChanged;
            this.ComponentFilterListBox.SelectionChanged += this.OnComponentFilterSelectionChanged;
            this.PopulateHardwareDropDown();

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            var versionString = version != null
                ? $"{version.Major}.{version.Minor}.{version.Build}"
                : null;

            this.AppVersionText.Text = versionString != null
                ? $"Version {versionString}"
                : "Version (unknown)";

            this.Title = versionString != null
                ? $"Commodore Repair Toolbox {versionString}"
                : "Commodore Repair Toolbox";

            // Initialize configuration checkboxes — subscribe after setting initial values
            // to avoid triggering redundant saves during startup
            this.CheckVersionOnLaunchCheckBox.IsChecked = UserSettings.CheckVersionOnLaunch;
            this.CheckDataOnLaunchCheckBox.IsChecked = UserSettings.CheckDataOnLaunch;
            this.CheckVersionOnLaunchCheckBox.IsCheckedChanged += this.OnCheckVersionOnLaunchChanged;
            this.CheckDataOnLaunchCheckBox.IsCheckedChanged += this.OnCheckDataOnLaunchChanged;

            if (UserSettings.CheckVersionOnLaunch)
            {
                this.CheckForAppUpdate();
            }

            this.StartBackgroundSyncAsync();
        }

        // ###########################################################################################
        // Checks for an available update on startup and shows the banner if one is found.
        // ###########################################################################################
        private async void CheckForAppUpdate()
        {
            bool? available = await UpdateService.CheckForUpdateAsync();

            if (available == true)
            {
                this.UpdateBannerText.Text = $"Version {UpdateService.PendingVersion} is available";
                this.UpdateBanner.IsVisible = true;
            }
        }

        // ###########################################################################################
        // Shows the sync banner during background sync, then hides it automatically if nothing
        // changed, or keeps it visible with a summary if files were updated.
        // ###########################################################################################
        private async void StartBackgroundSyncAsync()
        {
            if (!DataManager.HasPendingSync)
                return;

            this.SyncBannerText.Text = "Synching data with online source...";
            this.SyncBanner.IsVisible = true;

            int changed = await DataManager.SyncRemainingAsync(status =>
                Dispatcher.UIThread.Post(() => this.SyncBannerText.Text = status));

            if (changed > 0)
            {
                this.SyncBannerText.Text = changed == 1
                    ? "1 file updated in the background"
                    : $"{changed} files updated in the background";
            }
            else
            {
                this.SyncBanner.IsVisible = false;
            }
        }

        // ###########################################################################################
        // Dismisses the sync banner.
        // ###########################################################################################
        private void OnSyncBannerDismiss(object? sender, RoutedEventArgs e)
        {
            this.SyncBanner.IsVisible = false;
        }

        // ###########################################################################################
        // Dismisses the sync banner when clicking anywhere on it.
        // ###########################################################################################
        private void OnSyncBannerPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            this.SyncBanner.IsVisible = false;
        }

        // ###########################################################################################
        // Dismisses the update banner without cancelling the update.
        // ###########################################################################################
        private void OnUpdateBannerDismiss(object? sender, RoutedEventArgs e)
        {
            this.UpdateBanner.IsVisible = false;
        }

        // ###########################################################################################
        // Downloads and installs the pending update, showing progress in the banner text.
        // ###########################################################################################
        private async void OnInstallUpdateClick(object? sender, RoutedEventArgs e)
        {
            this.UpdateBannerInstallButton.IsEnabled = false;
            this.UpdateBannerDismissButton.IsEnabled = false;
            this.UpdateBannerText.Text = "Downloading update...";

            await UpdateService.DownloadAndInstallAsync(progress =>
            {
                Dispatcher.UIThread.Post(() => this.UpdateBannerText.Text = $"Downloading update... {progress}%");
            });
            // DownloadAndInstallAsync calls ApplyUpdatesAndRestart internally - app relaunches automatically
        }

        // ###########################################################################################
        // Subscribes to Bounds property changes on each panel to update the size labels in real-time.
        // ###########################################################################################
        private void SubscribePanelSizeChanges()
        {
            this.LeftPanel.PropertyChanged += (s, e) =>
            {
                if (e.Property == Visual.BoundsProperty)
                    this.LeftSizeLabel.Text = $"{this.LeftPanel.Bounds.Width:F0} × {this.LeftPanel.Bounds.Height:F0}";
            };

            this.RightPanel.PropertyChanged += (s, e) =>
            {
                if (e.Property == Visual.BoundsProperty)
                    this.RightSizeLabel.Text = $"{this.RightPanel.Bounds.Width:F0} × {this.RightPanel.Bounds.Height:F0}";
            };
        }

        // ###########################################################################################
        // Populates the hardware drop-down with distinct, sorted hardware names from loaded data.
        // Automatically selects the first entry and triggers the board drop-down to populate.
        // ###########################################################################################
        private void PopulateHardwareDropDown()
        {
            var hardwareNames = DataManager.HardwareBoards
                .Select(e => e.HardwareName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            this.HardwareComboBox.ItemsSource = hardwareNames;

            if (hardwareNames.Count > 0)
                this.HardwareComboBox.SelectedIndex = 0;
        }

        // ###########################################################################################
        // Filters the board drop-down to only show boards belonging to the selected hardware.
        // ###########################################################################################
        private void OnHardwareSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var selectedHardware = this.HardwareComboBox.SelectedItem as string;

            var boards = DataManager.HardwareBoards
                .Where(entry => string.Equals(entry.HardwareName, selectedHardware, StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.BoardName)
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .ToList();

            this.BoardComboBox.ItemsSource = boards;
            this.BoardComboBox.SelectedIndex = boards.Count > 0 ? 0 : -1;
        }

        // ###########################################################################################
        // Handles board selection changes - loads board data and builds the thumbnail gallery.
        // Also builds per-schematic, per-label highlight rect lookup for selection-driven rendering.
        // ###########################################################################################
        private async void OnBoardSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            // Suppress category saves immediately — setting ItemsSource = null below fires
            // SelectionChanged with an empty selection, which would overwrite the new board's
            // saved categories before the restore logic has a chance to run.
            this._suppressCategoryFilterSave = true;

            foreach (var thumb in this._currentThumbnails)
            {
                if (!ReferenceEquals(thumb.ImageSource, thumb.BaseThumbnail))
                    (thumb.ImageSource as IDisposable)?.Dispose();
                (thumb.BaseThumbnail as IDisposable)?.Dispose();
            }
            this._currentThumbnails = [];
            this.SchematicsThumbnailList.ItemsSource = null;
            this.CategoryFilterListBox.ItemsSource = null;
            this.ComponentFilterListBox.ItemsSource = null;

            this._highlightIndexBySchematic = new(StringComparer.OrdinalIgnoreCase);
            this._schematicByName = new(StringComparer.OrdinalIgnoreCase);
            this._highlightRectsBySchematicAndLabel = new(StringComparer.OrdinalIgnoreCase);
            this._currentBoardData = null;

            this.ResetSchematicsViewer();

            var selectedHardware = this.HardwareComboBox.SelectedItem as string;
            var selectedBoard = this.BoardComboBox.SelectedItem as string;

            if (string.IsNullOrEmpty(selectedHardware) || string.IsNullOrEmpty(selectedBoard))
                return;

            var entry = DataManager.HardwareBoards.FirstOrDefault(e =>
                string.Equals(e.HardwareName, selectedHardware, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.BoardName, selectedBoard, StringComparison.OrdinalIgnoreCase));

            if (entry == null || string.IsNullOrWhiteSpace(entry.ExcelDataFile))
                return;

            var boardData = await DataManager.LoadBoardDataAsync(entry);
            if (boardData == null)
                return;

            this._currentBoardData = boardData;

            // Populate category filter in insertion order
            var categories = BuildDistinctCategories(boardData);
            var boardKey = this.GetCurrentBoardKey();

            this.CategoryFilterListBox.ItemsSource = categories;

            var savedCategories = UserSettings.GetSelectedCategories(boardKey);
            if (savedCategories == null)
            {
                // No saved selection yet — default: select all
                this.CategoryFilterListBox.SelectAll();
            }
            else
            {
                // Restore the previously saved per-board selection
                for (int i = 0; i < categories.Count; i++)
                {
                    if (savedCategories.Contains(categories[i], StringComparer.OrdinalIgnoreCase))
                        this.CategoryFilterListBox.Selection.Select(i);
                }
            }
            this._suppressCategoryFilterSave = false;

            // Populate component filter for this board, filtered by the active region and categories
            var activeCategories = new HashSet<string>(
                this.CategoryFilterListBox.SelectedItems?.Cast<string>() ?? [],
                StringComparer.OrdinalIgnoreCase);
            var componentItems = BuildComponentItems(boardData, AppConfig.DefaultRegion, activeCategories);
            this.ComponentFilterListBox.ItemsSource = componentItems;

            // Build per-schematic, per-label highlight rects for selection-driven highlighting
            this._highlightRectsBySchematicAndLabel = await Task.Run(() => BuildHighlightRects(boardData, AppConfig.DefaultRegion));
            this._schematicByName = boardData.Schematics
                .Where(s => !string.IsNullOrWhiteSpace(s.SchematicName))
                .ToDictionary(s => s.SchematicName, s => s, StringComparer.OrdinalIgnoreCase);
            this._highlightIndexBySchematic = new(StringComparer.OrdinalIgnoreCase);

            // Load full-resolution bitmaps on a background thread
            var loaded = await Task.Run(() =>
            {
                var result = new List<(string Name, string FullPath, Bitmap? FullBitmap)>();

                foreach (var schematic in boardData.Schematics)
                {
                    if (string.IsNullOrWhiteSpace(schematic.SchematicImageFile))
                        continue;

                    var fullPath = Path.Combine(DataManager.DataRoot,
                        schematic.SchematicImageFile.Replace('/', Path.DirectorySeparatorChar));

                    Bitmap? bitmap = null;
                    if (File.Exists(fullPath))
                    {
                        try { bitmap = new Bitmap(fullPath); }
                        catch (Exception ex) { Logger.Warning($"Could not load schematic image [{fullPath}] - [{ex.Message}]"); }
                    }

                    result.Add((schematic.SchematicName, fullPath, bitmap));
                }

                return result;
            });

            // Pre-scale to base thumbnails (no highlights) on the UI thread, then release full-resolution originals
            var thumbnails = new List<SchematicThumbnail>();

            foreach (var (name, fullPath, fullBitmap) in loaded)
            {
                RenderTargetBitmap? baseThumbnail = null;
                PixelSize originalPixelSize = default;

                if (fullBitmap != null)
                {
                    baseThumbnail = CreateScaledThumbnail(fullBitmap, AppConfig.ThumbnailMaxWidth);
                    originalPixelSize = fullBitmap.PixelSize;
                    fullBitmap.Dispose();
                }

                thumbnails.Add(new SchematicThumbnail
                {
                    Name = name,
                    ImageFilePath = fullPath,
                    BaseThumbnail = baseThumbnail,
                    OriginalPixelSize = originalPixelSize,
                    ImageSource = baseThumbnail
                });
            }

            this._currentThumbnails = thumbnails;
            this.SchematicsThumbnailList.ItemsSource = thumbnails;

            if (thumbnails.Count > 0)
                this.SchematicsThumbnailList.SelectedIndex = 0;

            // Restore schematics splitter ratio saved for this specific board
            var ratio = UserSettings.GetSchematicsSplitterRatio(boardKey);
            this.SchematicsInnerGrid.ColumnDefinitions[0].Width = new GridLength(ratio * 100.0, GridUnitType.Star);
            this.SchematicsInnerGrid.ColumnDefinitions[2].Width = new GridLength((1.0 - ratio) * 100.0, GridUnitType.Star);
        }

        // ###########################################################################################
        // Loads the full-resolution image for the selected thumbnail and sets up the highlight overlay.
        // ###########################################################################################
        private async void OnSchematicsThumbnailSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            this._fullResLoadCts?.Cancel();
            this._fullResLoadCts = new CancellationTokenSource();
            var cts = this._fullResLoadCts;

            var selected = this.SchematicsThumbnailList.SelectedItem as SchematicThumbnail;

            this.SchematicsImage.Source = null;
            this._schematicsMatrix = Matrix.Identity;
            ((MatrixTransform)this.SchematicsImage.RenderTransform!).Matrix = this._schematicsMatrix;
            ((MatrixTransform)this.SchematicsHighlightsOverlay.RenderTransform!).Matrix = this._schematicsMatrix;

            this.SchematicsHighlightsOverlay.HighlightIndex = null;
            this.SchematicsHighlightsOverlay.BitmapPixelSize = new PixelSize(0, 0);
            this.SchematicsHighlightsOverlay.ViewMatrix = this._schematicsMatrix;

            if (selected == null || string.IsNullOrEmpty(selected.ImageFilePath))
                return;

            var bitmap = await Task.Run(() =>
            {
                if (cts.Token.IsCancellationRequested)
                    return null;

                try { return new Bitmap(selected.ImageFilePath); }
                catch (Exception ex)
                {
                    Logger.Warning($"Could not load full-res schematic [{selected.ImageFilePath}] - [{ex.Message}]");
                    return null;
                }
            }, cts.Token);

            if (cts.Token.IsCancellationRequested)
            {
                bitmap?.Dispose();
                return;
            }

            this._currentFullResBitmap?.Dispose();
            this._currentFullResBitmap = bitmap;
            this.SchematicsImage.Source = bitmap;

            if (bitmap != null)
            {
                // Always set BitmapPixelSize so the overlay can render as soon as a component is selected,
                // even if no highlight index exists yet at the time this schematic loads.
                this.SchematicsHighlightsOverlay.BitmapPixelSize = bitmap.PixelSize;

                if (this._highlightIndexBySchematic.TryGetValue(selected.Name, out var index) &&
                    this._schematicByName.TryGetValue(selected.Name, out var schematic))
                {
                    this.SchematicsHighlightsOverlay.HighlightIndex = index;
                    this.SchematicsHighlightsOverlay.HighlightColor = ParseColorOrDefault(schematic.MainImageHighlightColor, Colors.IndianRed);
                    this.SchematicsHighlightsOverlay.HighlightOpacity = ParseOpacityOrDefault(schematic.MainHighlightOpacity, 0.20);
                }
            }

            this.SchematicsHighlightsOverlay.ViewMatrix = this._schematicsMatrix;
            this.SchematicsHighlightsOverlay.InvalidateVisual();

            // Defer a clamp call so the engine can measure and center the new image layout 
            // immediately instead of waiting for a window resize or banner collapse.
            Dispatcher.UIThread.Post(() => this.ClampSchematicsMatrix());
        }

        // ###########################################################################################
        // Clears the main schematics image and resets the zoom and highlight overlay state.
        // ###########################################################################################
        private void ResetSchematicsViewer()
        {
            this._fullResLoadCts?.Cancel();
            this._fullResLoadCts = null;

            this._currentFullResBitmap?.Dispose();
            this._currentFullResBitmap = null;

            this.SchematicsImage.Source = null;

            this._schematicsMatrix = Matrix.Identity;
            ((MatrixTransform)this.SchematicsImage.RenderTransform!).Matrix = this._schematicsMatrix;
            ((MatrixTransform)this.SchematicsHighlightsOverlay.RenderTransform!).Matrix = this._schematicsMatrix;

            this.SchematicsHighlightsOverlay.HighlightIndex = null;
            this.SchematicsHighlightsOverlay.BitmapPixelSize = new PixelSize(0, 0);
            this.SchematicsHighlightsOverlay.ViewMatrix = this._schematicsMatrix;

            this._isPanning = false;
            this.SchematicsContainer.Cursor = Cursor.Default;
        }

        // ###########################################################################################
        // Returns the rectangle (in the image control's local coordinate space) that the actual
        // bitmap content occupies, accounting for Stretch="Uniform" letterboxing on either axis.
        // ###########################################################################################
        private Rect GetImageContentRect()
        {
            var imageSize = this.SchematicsImage.Bounds.Size;
            var bitmap = this._currentFullResBitmap;

            if (bitmap == null || imageSize.Width <= 0 || imageSize.Height <= 0)
                return new Rect(imageSize);

            double containerAspect = imageSize.Width / imageSize.Height;

            // Use .Size (logical dimensions) instead of .PixelSize to account for image DPI metadata
            double bitmapAspect = bitmap.Size.Width / bitmap.Size.Height;

            double contentX, contentY, contentWidth, contentHeight;

            if (bitmapAspect > containerAspect)
            {
                // Letterbox top and bottom
                contentWidth = imageSize.Width;
                contentHeight = imageSize.Width / bitmapAspect;
                contentX = 0;
                contentY = (imageSize.Height - contentHeight) / 2.0;
            }
            else
            {
                // Letterbox left and right
                contentHeight = imageSize.Height;
                contentWidth = imageSize.Height * bitmapAspect;
                contentX = (imageSize.Width - contentWidth) / 2.0;
                contentY = 0;
            }

            return new Rect(contentX, contentY, contentWidth, contentHeight);
        }

        // ###########################################################################################
        // Clamps the current schematics matrix so no empty space is visible inside the container.
        // If the scaled content is smaller than the container horizontally it is centered on that axis.
        // Vertically, content is always top-aligned. Always writes the corrected matrix back to the
        // RenderTransform.
        // ###########################################################################################
        private void ClampSchematicsMatrix()
        {
            var containerSize = this.SchematicsContainer.Bounds.Size;
            if (containerSize.Width <= 0 || containerSize.Height <= 0)
                return;

            var contentRect = this.GetImageContentRect();
            double scale = this._schematicsMatrix.M11;
            double tx = this._schematicsMatrix.M31;
            double ty = this._schematicsMatrix.M32;

            var transformedRect = contentRect.TransformToAABB(this._schematicsMatrix);

            double scaledWidth = transformedRect.Width;
            double scaledHeight = transformedRect.Height;
            double scaledLeft = transformedRect.Left;
            double scaledTop = transformedRect.Top;
            double scaledRight = transformedRect.Right;
            double scaledBottom = transformedRect.Bottom;

            // Horizontal - prevent empty space; center if content is narrower than container
            if (scaledWidth >= containerSize.Width)
            {
                if (scaledLeft > 0) tx -= scaledLeft;
                else if (scaledRight < containerSize.Width) tx += containerSize.Width - scaledRight;
            }
            else
            {
                tx = (containerSize.Width - scaledWidth) / 2.0 - scale * contentRect.Left;
            }

            // Vertical - prevent empty space; top-align if content is shorter than container
            if (scaledHeight >= containerSize.Height)
            {
                if (scaledTop > 0) ty -= scaledTop;
                else if (scaledBottom < containerSize.Height) ty += containerSize.Height - scaledBottom;
            }
            else
            {
                ty = -(scale * contentRect.Top);
            }

            this._schematicsMatrix = new Matrix(scale, 0, 0, scale, tx, ty);
            ((MatrixTransform)this.SchematicsImage.RenderTransform!).Matrix = this._schematicsMatrix;
            ((MatrixTransform)this.SchematicsHighlightsOverlay.RenderTransform!).Matrix = this._schematicsMatrix;

            this.SchematicsHighlightsOverlay.ViewMatrix = this._schematicsMatrix;
            this.SchematicsHighlightsOverlay.InvalidateVisual();
        }

        // ###########################################################################################
        // Creates a pre-scaled bitmap from a full-resolution source image.
        // ###########################################################################################
        private static RenderTargetBitmap CreateScaledThumbnail(Bitmap source, int maxWidth)
        {
            double scale = Math.Min(1.0, (double)maxWidth / source.PixelSize.Width);
            int tw = Math.Max(1, (int)(source.PixelSize.Width * scale));
            int th = Math.Max(1, (int)(source.PixelSize.Height * scale));

            var imageControl = new Image
            {
                Source = source,
                Stretch = Stretch.Uniform
            };
            imageControl.Measure(new Size(tw, th));
            imageControl.Arrange(new Rect(0, 0, tw, th));

            var rtb = new RenderTargetBitmap(new PixelSize(tw, th), new Vector(96, 96));
            rtb.Render(imageControl);
            return rtb;
        }

        // ###########################################################################################
        // Composites highlight rectangles onto a base thumbnail and returns the new rendered bitmap.
        // ###########################################################################################
        private static RenderTargetBitmap CreateHighlightedThumbnail(
            IImage baseThumbnail, PixelSize originalPixelSize,
            HighlightSpatialIndex index, BoardSchematicEntry schematic)
        {
            int tw = 1, th = 1;
            if (baseThumbnail is RenderTargetBitmap rtb)
            {
                tw = rtb.PixelSize.Width;
                th = rtb.PixelSize.Height;
            }
            else if (baseThumbnail is Bitmap bmp)
            {
                tw = bmp.PixelSize.Width;
                th = bmp.PixelSize.Height;
            }

            var root = new Grid();

            var image = new Image
            {
                Source = baseThumbnail,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
            };

            var overlay = new SchematicHighlightsOverlay
            {
                HighlightIndex = index,
                BitmapPixelSize = originalPixelSize,
                ViewMatrix = Matrix.Identity,
                HighlightColor = ParseColorOrDefault(schematic.ThumbnailImageHighlightColor, Colors.IndianRed),
                HighlightOpacity = ParseOpacityOrDefault(schematic.ThumbnailHighlightOpacity, 0.20),
                IsHitTestVisible = false
            };

            root.Children.Add(image);
            root.Children.Add(overlay);

            root.Measure(new Size(tw, th));
            root.Arrange(new Rect(0, 0, tw, th));

            var result = new RenderTargetBitmap(new PixelSize(tw, th), new Vector(96, 96));
            result.Render(root);
            return result;
        }

        // ###########################################################################################
        // Handles the button click event and updates the status text.
        // ###########################################################################################
        private void OnMyButtonClick(object sender, RoutedEventArgs e)
        {
            this.StatusText.Text = "Button was clicked!";
        }

        // ###########################################################################################
        // Handles mouse wheel zoom on the Schematics image, centered on the cursor position.
        // ###########################################################################################
        private void OnSchematicsZoom(object? sender, PointerWheelEventArgs e)
        {
            var pos = e.GetPosition(this.SchematicsImage);
            double delta = e.Delta.Y > 0 ? AppConfig.SchematicsZoomFactor : 1.0 / AppConfig.SchematicsZoomFactor;

            double newScale = this._schematicsMatrix.M11 * delta;

            if (newScale > AppConfig.SchematicsMaxZoom)
                return;

            if (newScale < AppConfig.SchematicsMinZoom)
            {
                this._schematicsMatrix = Matrix.Identity;
                ((MatrixTransform)this.SchematicsImage.RenderTransform!).Matrix = this._schematicsMatrix;
                ((MatrixTransform)this.SchematicsHighlightsOverlay.RenderTransform!).Matrix = this._schematicsMatrix;

                this.SchematicsHighlightsOverlay.ViewMatrix = this._schematicsMatrix;
                this.SchematicsHighlightsOverlay.InvalidateVisual();

                e.Handled = true;
                return;
            }

            // Build a zoom matrix centered at the cursor position in image-local space
            var zoomMatrix = Matrix.CreateTranslation(-pos.X, -pos.Y)
                           * Matrix.CreateScale(delta, delta)
                           * Matrix.CreateTranslation(pos.X, pos.Y);

            this._schematicsMatrix = zoomMatrix * this._schematicsMatrix;
            this.ClampSchematicsMatrix();

            e.Handled = true;
        }

        // ###########################################################################################
        // Enters pan mode when the right mouse button is pressed.
        // ###########################################################################################
        private void OnSchematicsPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this.SchematicsContainer).Properties.IsRightButtonPressed)
                return;

            this._isPanning = true;
            this._panStartPoint = e.GetPosition(this.SchematicsContainer);
            this._panStartMatrix = this._schematicsMatrix;
            this.SchematicsContainer.Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Pointer.Capture(this.SchematicsContainer);
            e.Handled = true;
        }

        // ###########################################################################################
        // Translates the schematics image while the right mouse button is held down.
        // ###########################################################################################
        private void OnSchematicsPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!this._isPanning)
                return;

            var delta = e.GetPosition(this.SchematicsContainer) - this._panStartPoint;
            this._schematicsMatrix = this._panStartMatrix * Matrix.CreateTranslation(delta.X, delta.Y);
            this.ClampSchematicsMatrix();
            e.Handled = true;
        }

        // ###########################################################################################
        // Exits pan mode when the right mouse button is released.
        // ###########################################################################################
        private void OnSchematicsPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!this._isPanning)
                return;

            this._isPanning = false;
            this.SchematicsContainer.Cursor = Cursor.Default;
            e.Pointer.Capture(null);
            e.Handled = true;
        }

        // ###########################################################################################
        // Builds per-schematic, per-board-label highlight rect lookup from the loaded board data,
        // filtered by the active region. Used for on-demand highlighting when a component is selected.
        // ###########################################################################################
        private static Dictionary<string, Dictionary<string, List<Rect>>> BuildHighlightRects(BoardData boardData, string region)
        {
            var componentRegionByLabel = boardData.Components
                .Where(c => !string.IsNullOrWhiteSpace(c.BoardLabel))
                .GroupBy(c => c.BoardLabel, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Region ?? string.Empty, StringComparer.OrdinalIgnoreCase);

            bool IsVisibleByRegion(string boardLabel)
            {
                if (!componentRegionByLabel.TryGetValue(boardLabel, out var r))
                    return true;

                if (string.IsNullOrWhiteSpace(r))
                    return true;

                return string.Equals(r.Trim(), region, StringComparison.OrdinalIgnoreCase);
            }

            var result = new Dictionary<string, Dictionary<string, List<Rect>>>(StringComparer.OrdinalIgnoreCase);

            foreach (var h in boardData.ComponentHighlights)
            {
                if (string.IsNullOrWhiteSpace(h.SchematicName) || string.IsNullOrWhiteSpace(h.BoardLabel))
                    continue;

                if (!IsVisibleByRegion(h.BoardLabel))
                    continue;

                if (!TryParseDouble(h.X, out var x) ||
                    !TryParseDouble(h.Y, out var y) ||
                    !TryParseDouble(h.Width, out var w) ||
                    !TryParseDouble(h.Height, out var hh))
                    continue;

                if (w <= 0 || hh <= 0)
                    continue;

                if (!result.TryGetValue(h.SchematicName, out var byLabel))
                {
                    byLabel = new Dictionary<string, List<Rect>>(StringComparer.OrdinalIgnoreCase);
                    result[h.SchematicName] = byLabel;
                }

                if (!byLabel.TryGetValue(h.BoardLabel, out var rects))
                {
                    rects = [];
                    byLabel[h.BoardLabel] = rects;
                }

                rects.Add(new Rect(x, y, w, hh));
            }

            return result;
        }

        // ###########################################################################################
        // Handles component selection changes and drives highlight updates in both the main viewer
        // and all thumbnails.
        // ###########################################################################################
        private void OnComponentFilterSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (this._suppressComponentHighlightUpdate)
                return;

            var boardLabels = this.ComponentFilterListBox.SelectedItems?
                .Cast<ComponentListItem>()
                .Select(item => item.BoardLabel)
                .Where(l => !string.IsNullOrEmpty(l))
                .ToList() ?? [];

            this.UpdateHighlightsForComponents(boardLabels);
        }

        /// ###########################################################################################
        // Rebuilds highlight indices from the selected board labels, updates the main schematic
        // viewer overlay, and regenerates or restores all thumbnails accordingly.
        // ###########################################################################################
        private void UpdateHighlightsForComponents(List<string> boardLabels)
        {
            // Rebuild per-schematic highlight indices containing only the selected board labels
            this._highlightIndexBySchematic = new(StringComparer.OrdinalIgnoreCase);

            if (boardLabels.Count > 0)
            {
                foreach (var (schematicName, byLabel) in this._highlightRectsBySchematicAndLabel)
                {
                    var rects = new List<Rect>();
                    foreach (var label in boardLabels)
                    {
                        if (byLabel.TryGetValue(label, out var labelRects))
                            rects.AddRange(labelRects);
                    }

                    if (rects.Count > 0)
                        this._highlightIndexBySchematic[schematicName] = new HighlightSpatialIndex(rects);
                }
            }

            // Update main viewer overlay for the currently displayed schematic
            var selectedThumb = this.SchematicsThumbnailList.SelectedItem as SchematicThumbnail;
            if (selectedThumb != null &&
                this._highlightIndexBySchematic.TryGetValue(selectedThumb.Name, out var mainIndex) &&
                this._schematicByName.TryGetValue(selectedThumb.Name, out var mainSchematic))
            {
                this.SchematicsHighlightsOverlay.HighlightIndex = mainIndex;
                this.SchematicsHighlightsOverlay.BitmapPixelSize = this._currentFullResBitmap?.PixelSize ?? new PixelSize(0, 0);
                this.SchematicsHighlightsOverlay.HighlightColor = ParseColorOrDefault(mainSchematic.MainImageHighlightColor, Colors.IndianRed);
                this.SchematicsHighlightsOverlay.HighlightOpacity = ParseOpacityOrDefault(mainSchematic.MainHighlightOpacity, 0.20);
            }
            else
            {
                this.SchematicsHighlightsOverlay.HighlightIndex = null;
            }
            this.SchematicsHighlightsOverlay.InvalidateVisual();

            // Regenerate thumbnails that have matching highlights; restore others to base
            foreach (var thumb in this._currentThumbnails)
            {
                if (thumb.BaseThumbnail == null)
                    continue;

                if (this._highlightIndexBySchematic.TryGetValue(thumb.Name, out var thumbIndex) &&
                    this._schematicByName.TryGetValue(thumb.Name, out var thumbSchematic))
                {
                    var highlighted = CreateHighlightedThumbnail(thumb.BaseThumbnail, thumb.OriginalPixelSize, thumbIndex, thumbSchematic);
                    var old = thumb.ImageSource;
                    thumb.ImageSource = highlighted;
                    if (!ReferenceEquals(old, thumb.BaseThumbnail))
                        (old as IDisposable)?.Dispose();
                }
                else
                {
                    if (!ReferenceEquals(thumb.ImageSource, thumb.BaseThumbnail))
                    {
                        var old = thumb.ImageSource;
                        thumb.ImageSource = thumb.BaseThumbnail;
                        (old as IDisposable)?.Dispose();
                    }
                }
            }
        }

        // ###########################################################################################
        // Parses a double using invariant culture for Excel-origin numeric text.
        // ###########################################################################################
        private static bool TryParseDouble(string text, out double value)
            => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        // ###########################################################################################
        // Parses an Avalonia color string or returns a fallback.
        // ###########################################################################################
        private static Color ParseColorOrDefault(string text, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(text))
                return fallback;

            try { return Color.Parse(text.Trim()); }
            catch { return fallback; }
        }

        // ###########################################################################################
        // Parses opacity; supports 0-1 and 0-100 (treated as percent). Returns fallback on failure.
        // ###########################################################################################
        private static double ParseOpacityOrDefault(string text, double fallback)
        {
            if (!TryParseDouble(text, out var v))
                return fallback;

            if (v > 1.0)
                v /= 100.0;

            return Math.Clamp(v, 0.0, 1.0);
        }

        // ###########################################################################################
        // Persists the "Check for new version at launch" preference when the checkbox is toggled.
        // ###########################################################################################
        private void OnCheckVersionOnLaunchChanged(object? sender, RoutedEventArgs e)
        {
            UserSettings.CheckVersionOnLaunch = this.CheckVersionOnLaunchCheckBox.IsChecked == true;
        }

        // ###########################################################################################
        // Persists the "Check for new or updated data at launch" preference when the checkbox is toggled.
        // ###########################################################################################
        private void OnCheckDataOnLaunchChanged(object? sender, RoutedEventArgs e)
        {
            UserSettings.CheckDataOnLaunch = this.CheckDataOnLaunchCheckBox.IsChecked == true;
        }

        // ###########################################################################################
        // Saves the selected category list for the current board whenever the user changes it.
        // Skipped during programmatic population to avoid overwriting a valid saved state.
        // ###########################################################################################
        private void OnCategoryFilterSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (this._suppressCategoryFilterSave)
                return;

            var boardKey = this.GetCurrentBoardKey();
            if (string.IsNullOrEmpty(boardKey))
                return;

            var selected = this.CategoryFilterListBox.SelectedItems?
                .Cast<string>()
                .ToList() ?? [];

            UserSettings.SetSelectedCategories(boardKey, selected);

            if (this._currentBoardData != null)
            {
                // Capture selected board labels before the list is rebuilt
                var previouslySelectedLabels = new HashSet<string>(
                    this.ComponentFilterListBox.SelectedItems?.Cast<ComponentListItem>()
                        .Select(i => i.BoardLabel) ?? [],
                    StringComparer.OrdinalIgnoreCase);

                var categoryFilter = new HashSet<string>(selected, StringComparer.OrdinalIgnoreCase);
                var componentItems = BuildComponentItems(this._currentBoardData, AppConfig.DefaultRegion, categoryFilter);

                // Suppress highlight updates during ItemsSource replacement and re-selection
                this._suppressComponentHighlightUpdate = true;
                this.ComponentFilterListBox.ItemsSource = componentItems;

                // Re-select items that were selected before and are still present in the new list
                for (int i = 0; i < componentItems.Count; i++)
                {
                    if (previouslySelectedLabels.Contains(componentItems[i].BoardLabel))
                        this.ComponentFilterListBox.Selection.Select(i);
                }
                this._suppressComponentHighlightUpdate = false;

                // Drive a single highlight update with only the surviving selected labels
                var survivingLabels = componentItems
                    .Where(item => previouslySelectedLabels.Contains(item.BoardLabel))
                    .Select(item => item.BoardLabel)
                    .Where(l => !string.IsNullOrEmpty(l))
                    .ToList();

                this.UpdateHighlightsForComponents(survivingLabels);
            }
        }

        // ###########################################################################################
        // Returns a composite key uniquely identifying the current hardware and board selection.
        // Used to store and retrieve per-board settings such as the schematics splitter position.
        // ###########################################################################################
        private string GetCurrentBoardKey()
        {
            var hw = this.HardwareComboBox.SelectedItem as string;
            var board = this.BoardComboBox.SelectedItem as string;
            if (string.IsNullOrEmpty(hw) || string.IsNullOrEmpty(board))
            {
                return string.Empty;
            }
            return $"{hw}|{board}";
        }

        // ###########################################################################################
        // Saves the left panel width after the main splitter drag ends.
        // Deferred via Post to ensure Bounds reflects the completed layout pass.
        // ###########################################################################################
        private void OnMainSplitterPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            Dispatcher.UIThread.Post(() => UserSettings.LeftPanelWidth = this.LeftPanel.Bounds.Width);
        }

        // ###########################################################################################
        // Saves the schematics/thumbnail split ratio for the current board after the drag ends.
        // Deferred via Post to ensure Bounds reflects the completed layout pass.
        // ###########################################################################################
        private void OnSchematicsSplitterPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            var boardKey = this.GetCurrentBoardKey();
            if (string.IsNullOrEmpty(boardKey))
            {
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                var leftWidth = this.SchematicsContainer.Bounds.Width;
                var rightWidth = this.SchematicsThumbnailList.Bounds.Width;
                var total = leftWidth + rightWidth;
                if (total <= 0)
                {
                    return;
                }
                UserSettings.SetSchematicsSplitterRatio(boardKey, leftWidth / total);
            });
        }

        // ###########################################################################################
        // On first open: validates the saved position is on a live screen (corrects to primary
        // if the monitor was disconnected), then subscribes to size, position, and state tracking.
        // ###########################################################################################
        private void OnWindowFirstOpened(object? sender, EventArgs e)
        {
            this.Opened -= this.OnWindowFirstOpened;

            if (UserSettings.HasWindowPlacement && this.WindowState == Avalonia.Controls.WindowState.Normal)
            {
                bool isOnScreen = this.Screens.All.Any(s =>
                    _restorePosition.X >= s.Bounds.X &&
                    _restorePosition.Y >= s.Bounds.Y &&
                    _restorePosition.X < s.Bounds.X + s.Bounds.Width &&
                    _restorePosition.Y < s.Bounds.Y + s.Bounds.Height);

                if (!isOnScreen)
                {
                    var primary = this.Screens.Primary;
                    if (primary != null)
                    {
                        this.Position = new PixelPoint(
                            primary.Bounds.X + Math.Max(0, (primary.Bounds.Width - (int)this.Width) / 2),
                            primary.Bounds.Y + Math.Max(0, (primary.Bounds.Height - (int)this.Height) / 2));
                    }
                }
            }

            // Save when the window is maximized, restored, or moved to another screen
            this.PropertyChanged += (s, args) =>
            {
                if (args.Property == Window.WindowStateProperty)
                {
                    this.ScheduleWindowPlacementSave();
                }
            };

            this.PositionChanged += this.OnWindowPositionChanged;
            this.SizeChanged += this.OnWindowSizeChanged;
        }

        // ###########################################################################################
        // Tracks the window's position in Normal state and schedules a debounced save.
        // ###########################################################################################
        private void OnWindowPositionChanged(object? sender, PixelPointEventArgs e)
        {
            if (this.WindowState == Avalonia.Controls.WindowState.Normal)
            {
                _restorePosition = e.Point;
                this.ScheduleWindowPlacementSave();
            }
        }

        // ###########################################################################################
        // Tracks the window's size in Normal state and schedules a debounced save.
        // ###########################################################################################
        private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            if (this.WindowState == Avalonia.Controls.WindowState.Normal)
            {
                _restoreWidth = e.NewSize.Width;
                _restoreHeight = e.NewSize.Height;
                this.ScheduleWindowPlacementSave();
            }
        }

        // ###########################################################################################
        // Resets and starts a 500 ms debounce timer; saves only after the window has been
        // idle for that period, avoiding a write on every pixel during resize or move.
        // ###########################################################################################
        private void ScheduleWindowPlacementSave()
        {
            if (_windowPlacementSaveTimer == null)
            {
                _windowPlacementSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                _windowPlacementSaveTimer.Tick += (s, e) =>
                {
                    _windowPlacementSaveTimer.Stop();
                    this.CommitWindowPlacement();
                };
            }

            _windowPlacementSaveTimer.Stop();
            _windowPlacementSaveTimer.Start();
        }

        // ###########################################################################################
        // Captures the current window state and screen, then persists to settings.
        // Called by the debounce timer and directly on close.
        // ###########################################################################################
        private void CommitWindowPlacement()
        {
            var state = this.WindowState == Avalonia.Controls.WindowState.Minimized
                ? Avalonia.Controls.WindowState.Normal
                : this.WindowState;

            var screen = this.Screens.All.FirstOrDefault(s =>
                this.Position.X >= s.Bounds.X &&
                this.Position.Y >= s.Bounds.Y &&
                this.Position.X < s.Bounds.X + s.Bounds.Width &&
                this.Position.Y < s.Bounds.Y + s.Bounds.Height)
                ?? this.Screens.Primary;

            UserSettings.SaveWindowPlacement(
                state.ToString(),
                _restoreWidth,
                _restoreHeight,
                _restorePosition.X,
                _restorePosition.Y,
                screen?.Bounds.X ?? 0,
                screen?.Bounds.Y ?? 0,
                screen?.Bounds.Width ?? 1920,
                screen?.Bounds.Height ?? 1080,
                screen?.Scaling ?? 1.0);
        }

        // ###########################################################################################
        // Stops any pending debounce timer and does a final synchronous save on close.
        // ###########################################################################################
        private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
        {
            _windowPlacementSaveTimer?.Stop();
            this.CommitWindowPlacement();
        }

        // ###########################################################################################
        // Builds a distinct list of component categories in the order they first appear.
        // ###########################################################################################
        private static List<string> BuildDistinctCategories(BoardData boardData)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var categories = new List<string>();

            foreach (var component in boardData.Components)
            {
                if (!string.IsNullOrWhiteSpace(component.Category) && seen.Add(component.Category))
                    categories.Add(component.Category);
            }

            return categories;
        }

        // ###########################################################################################
        // Builds component list items filtered by the given region.
        // Each item carries the board label for highlight lookups and a display text assembled
        // from the non-empty parts: BoardLabel | FriendlyName | TechnicalNameOrValue.
        // Components with an empty Region column are always included regardless of the active region.
        // ###########################################################################################
        private static List<ComponentListItem> BuildComponentItems(BoardData boardData, string region)
        {
            var items = new List<ComponentListItem>();

            foreach (var component in boardData.Components)
            {
                var componentRegion = component.Region?.Trim() ?? string.Empty;

                if (!string.IsNullOrEmpty(componentRegion) &&
                    !string.Equals(componentRegion, region, StringComparison.OrdinalIgnoreCase))
                    continue;

                var parts = new List<string>(3);
                if (!string.IsNullOrWhiteSpace(component.BoardLabel))
                    parts.Add(component.BoardLabel.Trim());
                if (!string.IsNullOrWhiteSpace(component.FriendlyName))
                    parts.Add(component.FriendlyName.Trim());
                if (!string.IsNullOrWhiteSpace(component.TechnicalNameOrValue))
                    parts.Add(component.TechnicalNameOrValue.Trim());

                if (parts.Count == 0)
                    continue;

                items.Add(new ComponentListItem
                {
                    BoardLabel = component.BoardLabel?.Trim() ?? string.Empty,
                    DisplayText = string.Join(" | ", parts)
                });
            }

            return items;
        }

        // ###########################################################################################
        // Lightweight view model for a component list item — carries the board label for
        // highlight lookups alongside the display text shown in the UI.
        // ###########################################################################################
        private sealed class ComponentListItem
        {
            public string DisplayText { get; init; } = string.Empty;
            public string BoardLabel { get; init; } = string.Empty;
            public override string ToString() => this.DisplayText;
        }

        // ###########################################################################################
        // Builds component list items filtered by the given region.
        // Each item carries the board label for highlight lookups and a display text assembled
        // from the non-empty parts: BoardLabel | FriendlyName | TechnicalNameOrValue.
        // Components with an empty Region column are always included regardless of the active region.
        // ###########################################################################################
        private static List<ComponentListItem> BuildComponentItems(BoardData boardData, string region, HashSet<string>? categoryFilter = null)
        {
            var items = new List<ComponentListItem>();

            foreach (var component in boardData.Components)
            {
                var componentRegion = component.Region?.Trim() ?? string.Empty;

                if (!string.IsNullOrEmpty(componentRegion) &&
                    !string.Equals(componentRegion, region, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (categoryFilter != null && !categoryFilter.Contains(component.Category ?? string.Empty))
                    continue;

                var parts = new List<string>(3);
                if (!string.IsNullOrWhiteSpace(component.BoardLabel))
                    parts.Add(component.BoardLabel.Trim());
                if (!string.IsNullOrWhiteSpace(component.FriendlyName))
                    parts.Add(component.FriendlyName.Trim());
                if (!string.IsNullOrWhiteSpace(component.TechnicalNameOrValue))
                    parts.Add(component.TechnicalNameOrValue.Trim());

                if (parts.Count == 0)
                    continue;

                items.Add(new ComponentListItem
                {
                    BoardLabel = component.BoardLabel?.Trim() ?? string.Empty,
                    DisplayText = string.Join(" | ", parts)
                });
            }

            return items;
        }

    }
}