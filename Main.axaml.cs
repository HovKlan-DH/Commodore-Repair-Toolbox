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
        private const double SchematicsZoomFactor = 1.5;
        private const double SchematicsMinZoom = 0.9;
        private const double SchematicsMaxZoom = 20.0;

        // Thumbnails
        private List<SchematicThumbnail> _currentThumbnails = [];
        private const int ThumbnailMaxWidth = 800;

        // Full-res viewer
        private Bitmap? _currentFullResBitmap;
        private CancellationTokenSource? _fullResLoadCts;

        // Panning
        private bool _isPanning;
        private Point _panStartPoint;
        private Matrix _panStartMatrix;

        // Highlights
        private const string DefaultRegion = "PAL";
        private Dictionary<string, HighlightSpatialIndex> _highlightIndexBySchematic = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, BoardSchematicEntry> _schematicByName = new(StringComparer.OrdinalIgnoreCase);

        public Main()
        {
            InitializeComponent();

            // Align the visual transform origin with the top-left coordinate system used in ClampSchematicsMatrix
            this.SchematicsImage.RenderTransformOrigin = RelativePoint.TopLeft;
            this.SchematicsHighlightsOverlay.RenderTransformOrigin = RelativePoint.TopLeft;

            // Keep highlights correct after layout changes (e.g. splitter drags)
            this.SchematicsContainer.PropertyChanged += (s, e) =>
            {
                if (e.Property == Visual.BoundsProperty)
                    this.ClampSchematicsMatrix();
            };

            this.SubscribePanelSizeChanges();
            this.HardwareComboBox.SelectionChanged += this.OnHardwareSelectionChanged;
            this.BoardComboBox.SelectionChanged += this.OnBoardSelectionChanged;
            this.SchematicsThumbnailList.SelectionChanged += this.OnSchematicsThumbnailSelectionChanged;
            this.PopulateHardwareDropDown();

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            this.AppVersionText.Text = version != null
                ? $"Version {version.Major}.{version.Minor}.{version.Build}"
                : "Version unknown";

            this.CheckForAppUpdate();
        }

        // ###########################################################################################
        // Checks for an available update on startup and shows the install button if one is found.
        // ###########################################################################################
        private async void CheckForAppUpdate()
        {
            bool? available = await UpdateService.CheckForUpdateAsync();

            if (available == true)
            {
                this.UpdateStatusText.Text = $"Version {UpdateService.PendingVersion} is available";
                this.InstallUpdateButton.IsVisible = true;
            }
            else if (available == false)
            {
                this.UpdateStatusText.Text = "Application is up to date";
            }
            else
            {
                this.UpdateStatusText.Text = $"Could not check for updates - [{UpdateService.LastCheckError}]";
            }
        }

        // ###########################################################################################
        // Downloads and installs the pending update, showing progress, then restarts the app.
        // ###########################################################################################
        private async void OnInstallUpdateClick(object? sender, RoutedEventArgs e)
        {
            this.InstallUpdateButton.IsEnabled = false;
            this.UpdateProgressBar.IsVisible = true;
            this.UpdateStatusText.Text = "Downloading update...";

            await UpdateService.DownloadAndInstallAsync(progress =>
            {
                Dispatcher.UIThread.Post(() => this.UpdateProgressBar.Value = progress);
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
        // Also builds per-schematic highlight indices for fast viewport rendering.
        // ###########################################################################################
        private async void OnBoardSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            foreach (var thumb in this._currentThumbnails)
                (thumb.ImageSource as IDisposable)?.Dispose();
            this._currentThumbnails = [];
            this.SchematicsThumbnailList.ItemsSource = null;

            this._highlightIndexBySchematic = new(StringComparer.OrdinalIgnoreCase);
            this._schematicByName = new(StringComparer.OrdinalIgnoreCase);

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

            // Build highlight indices (can be large)
            var indices = await Task.Run(() => CreateHighlightIndices(boardData, DefaultRegion));
            this._highlightIndexBySchematic = indices.HighlightIndexBySchematic;
            this._schematicByName = indices.SchematicByName;

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

            // Pre-scale to thumbnail size on UI thread, then release full-resolution originals
            var thumbnails = new List<SchematicThumbnail>();

            foreach (var (name, fullPath, fullBitmap) in loaded)
            {
                IImage? thumbnailImage = null;

                if (fullBitmap != null)
                {
                    if (this._highlightIndexBySchematic.TryGetValue(name, out var index) &&
                        this._schematicByName.TryGetValue(name, out var schematic))
                    {
                        thumbnailImage = CreateScaledThumbnailWithHighlights(fullBitmap, ThumbnailMaxWidth, index, schematic);
                    }
                    else
                    {
                        thumbnailImage = CreateScaledThumbnail(fullBitmap, ThumbnailMaxWidth);
                    }

                    fullBitmap.Dispose();
                }

                thumbnails.Add(new SchematicThumbnail
                {
                    Name = name,
                    ImageFilePath = fullPath,
                    ImageSource = thumbnailImage
                });
            }

            this._currentThumbnails = thumbnails;
            this.SchematicsThumbnailList.ItemsSource = thumbnails;

            if (thumbnails.Count > 0)
                this.SchematicsThumbnailList.SelectedIndex = 0;
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

            if (bitmap != null &&
                this._highlightIndexBySchematic.TryGetValue(selected.Name, out var index) &&
                this._schematicByName.TryGetValue(selected.Name, out var schematic))
            {
                this.SchematicsHighlightsOverlay.HighlightIndex = index;
                this.SchematicsHighlightsOverlay.BitmapPixelSize = bitmap.PixelSize;
                this.SchematicsHighlightsOverlay.HighlightColor = ParseColorOrDefault(schematic.MainImageHighlightColor, Colors.IndianRed);
                this.SchematicsHighlightsOverlay.HighlightOpacity = ParseOpacityOrDefault(schematic.MainHighlightOpacity, 0.20);
            }

            this.SchematicsHighlightsOverlay.ViewMatrix = this._schematicsMatrix;
            this.SchematicsHighlightsOverlay.InvalidateVisual();
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
        // Creates a pre-scaled thumbnail and bakes component highlight overlays into it.
        // ###########################################################################################
        private static RenderTargetBitmap CreateScaledThumbnailWithHighlights(Bitmap source, int maxWidth, HighlightSpatialIndex index, BoardSchematicEntry schematic)
        {
            double scale = Math.Min(1.0, (double)maxWidth / source.PixelSize.Width);
            int tw = Math.Max(1, (int)(source.PixelSize.Width * scale));
            int th = Math.Max(1, (int)(source.PixelSize.Height * scale));

            var root = new Grid();

            var image = new Image
            {
                Source = source,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
            };

            var overlay = new SchematicHighlightsOverlay
            {
                HighlightIndex = index,
                BitmapPixelSize = source.PixelSize,
                ViewMatrix = Matrix.Identity,
                HighlightColor = ParseColorOrDefault(schematic.ThumbnailImageHighlightColor, Colors.IndianRed),
                HighlightOpacity = ParseOpacityOrDefault(schematic.ThumbnailHighlightOpacity, 0.20),
                IsHitTestVisible = false
            };

            root.Children.Add(image);
            root.Children.Add(overlay);

            root.Measure(new Size(tw, th));
            root.Arrange(new Rect(0, 0, tw, th));

            var rtb = new RenderTargetBitmap(new PixelSize(tw, th), new Vector(96, 96));
            rtb.Render(root);
            return rtb;
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
            double delta = e.Delta.Y > 0 ? SchematicsZoomFactor : 1.0 / SchematicsZoomFactor;

            double newScale = this._schematicsMatrix.M11 * delta;

            if (newScale > SchematicsMaxZoom)
                return;

            if (newScale < SchematicsMinZoom)
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
        // Creates per-schematic highlight indices, filtered by region (PAL or empty region).
        // ###########################################################################################
        private static (Dictionary<string, HighlightSpatialIndex> HighlightIndexBySchematic, Dictionary<string, BoardSchematicEntry> SchematicByName)
            CreateHighlightIndices(BoardData boardData, string region)
        {
            var schematicByName = boardData.Schematics
                .Where(s => !string.IsNullOrWhiteSpace(s.SchematicName))
                .ToDictionary(s => s.SchematicName, s => s, StringComparer.OrdinalIgnoreCase);

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

            var rectsBySchematic = new Dictionary<string, List<Rect>>(StringComparer.OrdinalIgnoreCase);

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

                if (!rectsBySchematic.TryGetValue(h.SchematicName, out var list))
                {
                    list = [];
                    rectsBySchematic[h.SchematicName] = list;
                }

                list.Add(new Rect(x, y, w, hh));
            }

            var indexBySchematic = new Dictionary<string, HighlightSpatialIndex>(StringComparer.OrdinalIgnoreCase);
            foreach (var (schematicName, rects) in rectsBySchematic)
            {
                if (rects.Count == 0)
                    continue;

                indexBySchematic[schematicName] = new HighlightSpatialIndex(rects);
            }

            return (indexBySchematic, schematicByName);
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
    }
}