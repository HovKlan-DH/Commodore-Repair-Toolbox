using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Handlers.DataHandling;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tabs.TabSchematics;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CRT;

public partial class TabSchematics : UserControl
{
    public Main? MainWindow { get; set; }

    public bool IsLabelEditorActive => this.thisIsLabelEditorMode;

    // Zoom
    internal Matrix schematicsMatrix = Matrix.Identity;

    // Thumbnails
    internal ObservableCollection<SchematicThumbnail> currentThumbnails = new();

    // Full-res viewer
    internal Bitmap? currentFullResBitmap;
    internal CancellationTokenSource? fullResLoadCts;

    // Panning
    private bool isPanning;
    private Point panStartPoint;
    private Matrix panStartMatrix;

    // Highlights
    internal Dictionary<string, HighlightSpatialIndex> highlightIndexBySchematic = new(StringComparer.OrdinalIgnoreCase);
    internal Dictionary<string, BoardSchematicEntry> schematicByName = new(StringComparer.OrdinalIgnoreCase);

    // Highlight rects per schematic per board label — built at board load, used for on-demand highlighting
    internal Dictionary<string, Dictionary<string, List<Rect>>> highlightRectsBySchematicAndLabel = new(StringComparer.OrdinalIgnoreCase);

    // Insert logic class declaration
    internal PolylineManagement? polylineManager;

    // Thumbnail drag/drop reordering
    private Point thisThumbnailDragStartPoint;
    private bool thisIsDraggingThumbnail;
    private SchematicThumbnail? thisDraggedThumbnail;
    private int thisDraggedThumbnailOriginalIndex = -1;
    private bool thisDraggedThumbnailWasSelected;
    private double thisDraggedThumbnailHeight = 120.0;
    private SchematicThumbnail? thisThumbnailDropPlaceholder;
    private double thisDraggedThumbnailWidth = 160.0;
    private Point thisThumbnailDragPointerOffsetInItem;
//    private int thisThumbnailCurrentInsertIndex = -1;
//    private Point thisThumbnailDragStartPointInList;
    private double thisThumbnailLastPointerYInList = double.NaN;
    private double thisThumbnailDragGhostFixedX;
    private bool thisSuppressThumbnailSelectionChanged;
    private double thisCurrentHighlightBlinkFactor = 1.0;

    // Fullscreen
    private bool thisIsFullscreenMode;
    private GridLength thisRestoreLeftColumnWidth = new(1, GridUnitType.Star);
    private GridLength thisRestoreSplitterColumnWidth = new(4, GridUnitType.Pixel);
    private GridLength thisRestoreRightColumnWidth = new(1, GridUnitType.Star);
    private double thisRestoreRightColumnMinWidth = 100.0;

    // KiCad import / overlay
    private KiCadProjectBundle? thisKiCadProject;
//    private string thisKiCadProjectPath = string.Empty;
    private readonly HashSet<string> thisSelectedKiCadReferences = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> thisSelectedKiCadNormalizedNetNames = new(StringComparer.OrdinalIgnoreCase);
    private bool thisIsKiCadCalibrationCaptureMode;
    private readonly List<Point> thisKiCadCalibrationImagePoints = new();
    private string? thisHoveredKiCadNetName;
    private string? thisHoveredKiCadPadNumber;
    private readonly HashSet<string> thisLockedKiCadNetNames = new(StringComparer.OrdinalIgnoreCase);
    private bool thisIsKiCadOverlayRefreshQueued;
    private int thisKiCadOverlayRefreshRequestVersion;
    private int thisKiCadOverlayLastRenderedVersion;
    private bool thisIsInteractiveCadTraceHoverShiftPressed;

    private string? thisHoveredComponentBoardLabel;
    private readonly HashSet<string> thisSchematicsOnlySelectedBoardLabels = new(StringComparer.OrdinalIgnoreCase);
    private bool thisSuppressBoardSettingsChanged;
    private PointerPressedEventArgs? thisThumbnailDragStartEventArgs;
    private readonly List<Border> thisEditorLabelContainers = new();
    private readonly List<TextBlock> thisEditorLabelTextBlocks = new();
    private readonly List<ScaleTransform> thisEditorLabelScaleTransforms = new();
    private string thisLastEditorLabelVisualSignature = string.Empty;
    private readonly List<Border> thisStandardLabelContainers = new();
    private readonly List<TextBlock> thisStandardLabelTextBlocks = new();
    private readonly List<ScaleTransform> thisStandardLabelScaleTransforms = new();
    private string thisLastStandardLabelVisualSignature = string.Empty;

    private Point thisLastKiCadHoverHitTestContainerPoint = new(double.NaN, double.NaN);
    private long thisLastKiCadHoverHitTestTimestamp;
    private string thisLastKiCadNetConnectionsSignature = string.Empty;
    private string thisLastThumbnailHighlightSignature = string.Empty;

    private readonly Dictionary<string, KiCadPcbNetRenderCache> thisKiCadPcbNetRenderCacheByKey = new(StringComparer.OrdinalIgnoreCase);

    private sealed class KiCadPcbPadRenderNode
    {
        public KiCadGraphNode Info { get; init; } = new();
        public KiCadPcbFootprint Footprint { get; init; } = null!;
        public KiCadPcbPad Pad { get; init; } = null!;
        public Point CenterWorld { get; init; }
        public double RadiusWorld { get; init; }
    }

    private sealed class KiCadPcbSegmentRenderNode
    {
        public KiCadGraphNode Info { get; init; } = new();
        public Point StartWorld { get; init; }
        public Point EndWorld { get; init; }
        public double WidthWorld { get; init; }
    }

    private sealed class KiCadPcbViaRenderNode
    {
        public KiCadGraphNode Info { get; init; } = new();
        public Point CenterWorld { get; init; }
        public double DiameterWorld { get; init; }
    }

    private sealed class KiCadPcbArcRenderNode
    {
        public KiCadGraphNode Info { get; init; } = new();
        public Point StartWorld { get; init; }
        public Point MidWorld { get; init; }
        public Point EndWorld { get; init; }
        public double WidthWorld { get; init; }
    }

    private sealed class KiCadPcbNetRenderCache
    {
        public Dictionary<string, KiCadGraphNode> NodesById { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, HashSet<string>> AdjacencyByNodeId { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> PadReferenceByNodeId { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> AllNodeIds { get; init; } = new(StringComparer.OrdinalIgnoreCase);
        public List<KiCadPcbPadRenderNode> PadNodes { get; init; } = new();
        public List<KiCadPcbSegmentRenderNode> SegmentNodes { get; init; } = new();
        public List<KiCadPcbViaRenderNode> ViaNodes { get; init; } = new();
        public List<KiCadPcbArcRenderNode> ArcNodes { get; init; } = new();
    }

    private sealed class LabelEditorUndoHighlightState
    {
        public string SchematicName { get; set; } = string.Empty;
        public string BoardLabel { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool IsSelected { get; set; }
    }

    private sealed class LabelEditorUndoState
    {
        public List<LabelEditorUndoHighlightState> Highlights { get; } = new();
        public int PrimarySelectedIndex { get; set; } = -1;
    }

    private readonly Stack<LabelEditorUndoState> thisLabelEditorUndoStack = new();
    private readonly Stack<LabelEditorUndoState> thisLabelEditorRedoStack = new();

    private sealed class KiCadViewCalibration
    {
        public static KiCadViewCalibration Identity { get; } = new();

        public bool HasAffineCalibration { get; init; }
        public double A { get; init; }
        public double B { get; init; }
        public double C { get; init; }
        public double D { get; init; }
        public double E { get; init; }
        public double F { get; init; }

        public double ScaleX { get; init; } = 1.0;
        public double ScaleY { get; init; } = 1.0;
        public double OffsetX { get; init; }
        public double OffsetY { get; init; }
        public bool MirrorX { get; init; }
        public bool MirrorY { get; init; }
    }

    private readonly struct KiCadCalibrationPoint
    {
        public KiCadCalibrationPoint(double worldX, double worldY, double imageX, double imageY)
        {
            this.WorldX = worldX;
            this.WorldY = worldY;
            this.ImageX = imageX;
            this.ImageY = imageY;
        }

        public double WorldX { get; }
        public double WorldY { get; }
        public double ImageX { get; }
        public double ImageY { get; }
    }

    private sealed class KiCadCalibrationWorldPointCandidate
    {
        public string Label { get; init; } = string.Empty;
        public double WorldX { get; init; }
        public double WorldY { get; init; }
    }

    // ###########################################################################################
    // Triggers a visual overlay refresh when the hovered KiCad net changes.
    // ###########################################################################################
    private void SetHoveredKiCadNet(string? netName)
    {
        if (string.Equals(this.thisHoveredKiCadNetName, netName, StringComparison.OrdinalIgnoreCase))
            return;

        this.thisHoveredKiCadNetName = netName;
        this.RefreshKiCadOverlay();
    }

    private sealed class EditableComponentHighlight
    {
        public string SchematicName { get; set; } = string.Empty;
        public string BoardLabel { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    private enum LabelEditorDragMode
    {
        None,
        Move,
        ResizeTopLeft,
        ResizeTop,
        ResizeTopRight,
        ResizeRight,
        ResizeBottomRight,
        ResizeBottom,
        ResizeBottomLeft,
        ResizeLeft
    }

    // Label editor
    private bool thisIsLabelEditorMode;
    private bool thisIsShowingLabelEditorMenu;
    private Point thisLastLabelEditorMenuPoint;
    private string thisLabelEditorSchematicName = string.Empty;
    private readonly List<EditableComponentHighlight> thisLabelEditorWorkingHighlights = new();
    private readonly HashSet<EditableComponentHighlight> thisSelectedLabelEditorHighlights = new();
    private EditableComponentHighlight? thisSelectedLabelEditorHighlight;
    private bool thisIsDrawingLabelEditorRectangle;
    private Point thisLabelEditorDrawStartPixelPoint;
    private Rect? thisLabelEditorDraftRectangle;
    private EditableComponentHighlight? thisPendingNewLabelEditorHighlight;
    private LabelEditorDragMode thisLabelEditorDragMode;
    private Point thisLabelEditorDragStartPixelPoint;
    private Rect thisLabelEditorOriginalSelectionBounds;
    private readonly Dictionary<EditableComponentHighlight, Rect> thisLabelEditorOriginalDragRectangles = new();

    public TabSchematics()
    {
        InitializeComponent();

        this.EnableLabelEditorButton.Click += (_, _) => this.BeginLabelEditorMode();
        this.CancelLabelEditorChangesButton.Click += (_, _) => this.CancelLabelEditorChanges();
        this.ApplyLabelEditorChangesButton.Click += (_, _) => this.ApplyLabelEditorChanges();
     
        this.BeginKiCadCalibrationButton.Click += (_, _) => this.BeginKiCadCalibrationCapture();
        this.CancelKiCadCalibrationButton.Click += (_, _) => this.CancelKiCadCalibrationCapture();

        this.ConfirmNewLabelEditorBoardLabelButton.Click += (_, _) => this.ConfirmNewLabelEditorPrompt();
        this.CancelNewLabelEditorBoardLabelButton.Click += (_, _) => this.CancelNewLabelEditorPrompt();

        this.NewLabelEditorBoardLabelTextBox.KeyDown += this.OnNewLabelEditorPromptKeyDown;
        this.NewLabelEditorCategoryComboBox.KeyDown += this.OnNewLabelEditorPromptKeyDown;

        this.CopyKiCadWorldPointCandidatesButton.Click += (_, _) => this.CopyKiCadWorldPointCandidatesAsync();

        this.ClearKiCadTraceSelectionButton.Click += (_, _) => this.ClearAllKiCadTraceSelections();
    }

    // ###########################################################################################
    // Handle manual row clicks for scaled label visibilities.
    // ###########################################################################################
    private void OnLabelBoardRowClicked(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.CheckLabelBoard.IsChecked = !this.CheckLabelBoard.IsChecked;
            e.Handled = true;
        }
    }

    private void OnLabelTechnicalRowClicked(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.CheckLabelTechnical.IsChecked = !this.CheckLabelTechnical.IsChecked;
            e.Handled = true;
        }
    }

    private void OnLabelFriendlyRowClicked(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.CheckLabelFriendly.IsChecked = !this.CheckLabelFriendly.IsChecked;
            e.Handled = true;
        }
    }

    private void OnLabelSelectedOnlyRowClicked(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.CheckLabelSelectedOnly.IsChecked = !this.CheckLabelSelectedOnly.IsChecked;
            e.Handled = true;
        }
    }

    // ###########################################################################################
    // Initializes the control by injecting the parent main window instance and wiring events.
    // ###########################################################################################
    public void Initialize(Main mainWindow)
    {
        this.MainWindow = mainWindow;

        // Restore initial states from User Settings
        this.CheckLabelBoard.IsChecked = UserSettings.SchematicsLabelBoard;
        this.CheckLabelTechnical.IsChecked = UserSettings.SchematicsLabelTechnical;
        this.CheckLabelFriendly.IsChecked = UserSettings.SchematicsLabelFriendly;
        this.CheckLabelSelectedOnly.IsChecked = UserSettings.SchematicsLabelSelectedOnly;

        this.thisSuppressBoardSettingsChanged = true;
        this.CheckBoardMarkPin1OnSelectedComponent.IsChecked = true;
        this.CheckBoardHoverHighlightsTraces.IsChecked = true;
        this.CheckBoardContributorMode.IsChecked = UserSettings.ContributorMode;
        this.BoardSettingsPanel.IsEnabled = false;
        this.thisSuppressBoardSettingsChanged = false;

        DragDrop.SetAllowDrop(this.SchematicsThumbnailList, true);
        this.SchematicsThumbnailList.AddHandler(DragDrop.DragOverEvent, this.OnThumbnailDragOver);
        this.SchematicsThumbnailList.AddHandler(DragDrop.DropEvent, this.OnThumbnailDrop);
        this.SchematicsThumbnailList.AddHandler(DragDrop.DragLeaveEvent, this.OnThumbnailDragLeave);

        this.AddHandler(
            InputElement.KeyDownEvent,
            this.OnSchematicsKeyDown,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);

        this.AddHandler(
            InputElement.KeyUpEvent,
            this.OnSchematicsKeyUp,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);

        UserSettings.InteractiveCadTraceHoverModeChanged += this.OnInteractiveCadTraceHoverModeChanged;

        bool isLabelsExpanded = UserSettings.SchematicsLabelsPanelExpanded;
        this.LabelsListPanel.IsVisible = isLabelsExpanded;
        this.ToggleLabelsPanelButton.Content = isLabelsExpanded ? "Collapse" : "Expand";

        bool isBoardSettingsExpanded = UserSettings.SchematicsBoardSettingsPanelExpanded;
        this.BoardSettingsListPanel.IsVisible = isBoardSettingsExpanded;
        this.ToggleBoardSettingsPanelButton.Content = isBoardSettingsExpanded ? "Collapse" : "Expand";

        this.CheckLabelBoard.IsCheckedChanged += (s, e) =>
        {
            UserSettings.SchematicsLabelBoard = this.CheckLabelBoard.IsChecked == true;
            this.UpdateComponentLabels();
        };
        this.CheckLabelTechnical.IsCheckedChanged += (s, e) =>
        {
            UserSettings.SchematicsLabelTechnical = this.CheckLabelTechnical.IsChecked == true;
            this.UpdateComponentLabels();
        };
        this.CheckLabelFriendly.IsCheckedChanged += (s, e) =>
        {
            UserSettings.SchematicsLabelFriendly = this.CheckLabelFriendly.IsChecked == true;
            this.UpdateComponentLabels();
        };
        this.CheckLabelSelectedOnly.IsCheckedChanged += (s, e) =>
        {
            UserSettings.SchematicsLabelSelectedOnly = this.CheckLabelSelectedOnly.IsChecked == true;
            this.UpdateComponentLabels();
        };

        this.CheckBoardMarkPin1OnSelectedComponent.IsCheckedChanged += (s, e) =>
        {
            if (this.thisSuppressBoardSettingsChanged)
            {
                return;
            }

            var boardKey = this.MainWindow?.GetCurrentBoardKey();
            if (string.IsNullOrWhiteSpace(boardKey))
            {
                return;
            }

            UserSettings.SetSchematicsMarkPin1OnSelectedComponentForBoard(
                boardKey,
                this.CheckBoardMarkPin1OnSelectedComponent.IsChecked == true);

            this.RefreshKiCadOverlay(forceImmediate: true);
        };

        this.CheckBoardHoverHighlightsTraces.IsCheckedChanged += (s, e) =>
        {
            if (this.thisSuppressBoardSettingsChanged)
            {
                return;
            }

            var boardKey = this.MainWindow?.GetCurrentBoardKey();
            if (string.IsNullOrWhiteSpace(boardKey))
            {
                return;
            }

            UserSettings.SetSchematicsHoverHighlightsTracesForBoard(
                boardKey,
                this.CheckBoardHoverHighlightsTraces.IsChecked == true);

            this.RefreshKiCadOverlay();
        };

        this.CheckBoardContributorMode.IsCheckedChanged += (s, e) =>
        {
            if (this.thisSuppressBoardSettingsChanged)
            {
                return;
            }

            var boardKey = this.MainWindow?.GetCurrentBoardKey();
            if (string.IsNullOrWhiteSpace(boardKey))
            {
                return;
            }

            UserSettings.SetContributorModeForBoard(
                boardKey,
                this.CheckBoardContributorMode.IsChecked == true);
        };

        this.ToggleLabelsPanelButton.Click += (s, e) =>
        {
            bool willBeExpanded = !this.LabelsListPanel.IsVisible;
            this.LabelsListPanel.IsVisible = willBeExpanded;
            this.ToggleLabelsPanelButton.Content = willBeExpanded ? "Collapse" : "Expand";
            UserSettings.SchematicsLabelsPanelExpanded = willBeExpanded;
        };

        this.ToggleBoardSettingsPanelButton.Click += (s, e) =>
        {
            bool willBeExpanded = !this.BoardSettingsListPanel.IsVisible;
            this.BoardSettingsListPanel.IsVisible = willBeExpanded;
            this.ToggleBoardSettingsPanelButton.Content = willBeExpanded ? "Collapse" : "Expand";
            UserSettings.SchematicsBoardSettingsPanelExpanded = willBeExpanded;
        };

        this.polylineManager = new PolylineManagement(this, this.SchematicsPolylineCanvas);

        this.polylineManager.TraceStatsChanged += stats =>
        {
            Dispatcher.UIThread.Post(() => this.BuildTracesListPanel(stats));
        };

        // Saves active lines down dynamically over to disk
        this.polylineManager.TracesModified += () =>
        {
            var boardKey = this.MainWindow?.GetCurrentBoardKey();
            var schematicName = (this.SchematicsThumbnailList.SelectedItem as SchematicThumbnail)?.Name;

            if (!string.IsNullOrEmpty(boardKey) && !string.IsNullOrEmpty(schematicName))
            {
                var export = this.polylineManager.ExportTraces();
                TraceStorage.SaveTraces(boardKey, schematicName, export);
            }
        };

        this.polylineManager.PaletteColorsChanged += colors =>
        {
            Dispatcher.UIThread.Post(() => this.RebuildDynamicPalette(colors));
        };
        this.RebuildDynamicPalette(this.polylineManager.PaletteColors);

        this.polylineManager.PaletteStateChanged += (visible, point) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (visible)
                {
                    this.TraceFloatingPalette.Margin = new Avalonia.Thickness(point.X + 15, point.Y + 15, 0, 0);
                    this.TraceFloatingPalette.IsVisible = true;
                }
                else
                {
                    this.TraceFloatingPalette.IsVisible = false;
                }
            });
        };

        this.ClearTracesButton.Click += (s, e) =>
        {
            // Execute absolute clearing that triggers our new JSON saver event 
            this.polylineManager?.ClearAllTracesAndSave();
        };

        this.polylineManager.UndoStateChanged += hasUndo =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                this.UndoDeletedTraceButton.IsVisible = hasUndo;

                // Keep panel populated even when 0 lines remain if undo limit evaluates to valid stack lengths accurately 
                this.TracesPanel.IsVisible = (this.TracesListPanel.Children.Count > 0) || hasUndo;
            });
        };

        this.UndoDeletedTraceButton.Click += (s, e) =>
        {
            this.polylineManager?.UndoLastDeletion();
        };

        this.SchematicsSplitter.AddHandler(
            InputElement.PointerReleasedEvent,
            this.OnSchematicsSplitterPointerReleased,
            RoutingStrategies.Bubble,
            handledEventsToo: true);

        this.SchematicsImage.RenderTransformOrigin = RelativePoint.TopLeft;
        this.SchematicsHighlightsOverlay.RenderTransformOrigin = RelativePoint.TopLeft;
        this.SchematicsHoverHighlightsOverlay.RenderTransformOrigin = RelativePoint.TopLeft;

        this.SchematicsContainer.PropertyChanged += (s, e) =>
        {
            if (e.Property == Visual.BoundsProperty)
            {
                this.ClampSchematicsMatrix();
                this.RefreshKiCadOverlay();
            }
        };

        this.SchematicsImage.PropertyChanged += (s, e) =>
        {
            if (e.Property == Visual.BoundsProperty)
            {
                this.ClampSchematicsMatrix();
                this.RefreshKiCadOverlay();
            }
        };

        this.SchematicsThumbnailList.SelectionChanged += this.OnSchematicsThumbnailSelectionChanged;
        this.SchematicsContainer.PointerExited += this.OnSchematicsPointerExited;

        this.UpdateInteractiveCadTraceHoverModeUi();
    }

    private void OnTraceColorPickerPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property.Name == "Color" && e.NewValue is Color c)
        {
            this.polylineManager?.ChangeActiveColor(c);
            this.polylineManager?.AddOrReplacePaletteColor(c);
            if (this.CustomColorButton != null)
            {
                this.CustomColorButton.Background = new SolidColorBrush(c);
            }
        }
    }

    // ###########################################################################################
    // Dynamically rebuilds Standard Ellipses mapped to the floating Palette context window.
    // ###########################################################################################
    private void RebuildDynamicPalette(List<Color> colors)
    {
        this.DynamicPaletteColorsPanel.Children.Clear();

        foreach (var c in colors)
        {
            var ellipse = new Avalonia.Controls.Shapes.Ellipse
            {
                Fill = new SolidColorBrush(c),
                Width = 18,
                Height = 18,
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            ellipse.PointerPressed += this.OnPaletteColorClicked;
            this.DynamicPaletteColorsPanel.Children.Add(ellipse);
        }
    }

    // ###########################################################################################
    // Generates the code-behind view layout tracking the amounts and visibility configs per line color.
    // ###########################################################################################
    private void BuildTracesListPanel(Dictionary<Color, int> stats)
    {
        this.TracesListPanel.Children.Clear();
        int totalCounts = 0;

        foreach (var kvp in stats)
        {
            Color colorItem = kvp.Key;
            int count = kvp.Value;
            totalCounts += count;

            var row = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 6,
                Margin = new Avalonia.Thickness(0, 1),
                Background = Brushes.Transparent, // Ensures the empty space between items catches mouse clicks
                Cursor = new Cursor(StandardCursorType.Hand)
            };

            var cb = new CheckBox
            {
                MinHeight = 0,
                Margin = new Avalonia.Thickness(0),
                Padding = new Avalonia.Thickness(0),
                CornerRadius = new Avalonia.CornerRadius(2),
                IsChecked = this.polylineManager?.GetColorVisibility(colorItem) ?? true,
                IsHitTestVisible = false // Disable direct native hits so the row handles everything universally
            };

            cb.IsCheckedChanged += (s, e) =>
            {
                this.polylineManager?.SetVisibilityByColor(colorItem, cb.IsChecked == true);
            };

            // Wrapped CheckBox slightly larger, as the template natively has invisible touch padding 
            // that shrinks its visual square smaller than its layout bounds.
            var cbContainer = new Viewbox
            {
                Width = 20,
                Height = 20,
                Child = cb,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                IsHitTestVisible = false
            };

            var border = new Border
            {
                Width = 48,
                Height = 14,
                Background = new SolidColorBrush(colorItem),
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(2),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            // Set DynamicResource for BorderBrush
            border.Bind(Border.BorderBrushProperty, this.GetResourceObservable("Schematics_Panel_TracesVisible_Trace_Border"));

            var txt = new TextBlock
            {
                Text = $"({count})",
                FontSize = 11,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            // Set DynamicResource for Foreground
            txt.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("Schematics_Panels_Fg"));

            // Clicking anywhere on the row flips the active status of the checkbox
            row.PointerPressed += (s, e) =>
            {
                if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                {
                    cb.IsChecked = !cb.IsChecked;
                    e.Handled = true;
                }
            };

            row.Children.Add(cbContainer);
            row.Children.Add(border);
            row.Children.Add(txt);

            this.TracesListPanel.Children.Add(row);
        }

        this.TracesPanel.IsVisible = totalCounts > 0;
    }

    // ###########################################################################################
    // Applies the locally clicked palette color onto the currently active trace line.
    // ###########################################################################################
    private void OnPaletteColorClicked(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Avalonia.Controls.Shapes.Ellipse ellipse && ellipse.Fill is ISolidColorBrush brush)
        {
            this.polylineManager?.ChangeActiveColor(brush.Color);

            // Sync standard ColorPicker thumb AND button surface internally reflecting context accurately
            this.SetTraceColorPickerColor(brush.Color);
            this.CustomColorButton.Background = brush;
        }
        e.Handled = true;
    }

    // ###########################################################################################
    // Updates UI visibility for all contextual label borders at once.
    // ###########################################################################################
    public void HideLabels()
    {
        this.SchematicsNameBorder.IsVisible = false;
        this.SchematicsRegionBorder.IsVisible = false;
        this.SchematicsHoverLabelBorder.IsVisible = false;
        this.SchematicsHoverPadBorder.IsVisible = false;
    }

    // ###########################################################################################
    // Safely retrieves and sets the ColorView inside the unmapped flyout surface.
    // ###########################################################################################
    private void SetTraceColorPickerColor(Color color)
    {
        if (this.CustomColorButton.Flyout is Avalonia.Controls.Flyout flyout && flyout.Content != null)
        {
            var propInfo = flyout.Content.GetType().GetProperty("Color");
            propInfo?.SetValue(flyout.Content, color);
        }
    }

    // ###########################################################################################
    // Removes the currently targeted polyline trace securely via UI.
    // ###########################################################################################
    private void OnPaletteDeleteClicked(object? sender, PointerPressedEventArgs e)
    {
        this.polylineManager?.DeleteActivePolyline();
        e.Handled = true;
    }

    // ###########################################################################################
    // Handles mouse wheel zoom on the Schematics image, centered on the cursor position.
    // The image control already fits the bitmap to the available area, so matrix scale 1.0 is
    // the true minimum zoom and must not be reduced further.
    // ###########################################################################################
    private void OnSchematicsZoom(object? sender, PointerWheelEventArgs e)
    {
        var pos = e.GetPosition(this.SchematicsImage);
        double delta = e.Delta.Y > 0 ? AppConfig.SchematicsZoomFactor : 1.0 / AppConfig.SchematicsZoomFactor;

        double currentScale = this.schematicsMatrix.M11;
        double newScale = currentScale * delta;

        if (newScale > AppConfig.SchematicsMaxZoom)
            return;

        // The image is already fully fitted by Stretch="Uniform", so do not allow zooming out
        // below the baseline matrix scale of 1.0.
        if (delta < 1.0 && currentScale <= 1.0)
        {
            e.Handled = true;
            return;
        }

        // Snap cleanly back to the fitted baseline when zooming out crosses below 1.0.
        if (newScale < 1.0)
        {
            this.schematicsMatrix = Matrix.Identity;
            this.ClampSchematicsMatrix();
            e.Handled = true;
            return;
        }

        // Build a zoom matrix centered at the cursor position in image-local space
        var zoomMatrix = Matrix.CreateTranslation(-pos.X, -pos.Y)
                       * Matrix.CreateScale(delta, delta)
                       * Matrix.CreateTranslation(pos.X, pos.Y);

        this.schematicsMatrix = zoomMatrix * this.schematicsMatrix;
        this.ClampSchematicsMatrix();

        e.Handled = true;
    }

    // ###########################################################################################
    // Handles right-click for panning on the schematic view and selection toggling on release.
    // Left-click selects hovered component, and single-click opens the component info popup.
    // Also routes pointer presses to the polyline manager if appropriate.
    // ###########################################################################################
    private void OnSchematicsPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetPosition(this.SchematicsContainer);
        var pointer = e.GetCurrentPoint(this.SchematicsContainer);

        this.UpdateInteractiveCadTraceHoverShiftState(e.KeyModifiers);

        if (this.IsPointerInsideKiCadNetConnectionsPanel(point))
        {
            this.ClearTransientHoverForKiCadNetConnectionsPanel();
            return;
        }

        if (this.IsPointerInsideLabelEditorMenu(point) || this.IsPointerInsideNewLabelPrompt(point))
        {
            e.Handled = true;
            return;
        }

        if (this.thisIsLabelEditorMode && this.SchematicsNewLabelPromptBorder.IsVisible)
        {
            e.Handled = true;
            return;
        }

        if (pointer.Properties.IsLeftButtonPressed && this.thisIsShowingLabelEditorMenu)
        {
            this.HideLabelEditorMenu();
        }

        if (this.thisIsLabelEditorMode)
        {
            if (pointer.Properties.IsRightButtonPressed)
            {
                this.isPanning = true;
                this.panStartPoint = point;
                this.panStartMatrix = this.schematicsMatrix;

                this.HideSchematicsHoverUi();
                this.SchematicsContainer.Cursor = new Cursor(StandardCursorType.SizeAll);

                e.Pointer.Capture(this.SchematicsContainer);
                e.Handled = true;
                return;
            }

            if (pointer.Properties.IsLeftButtonPressed)
            {
                bool isCtrlDown = e.KeyModifiers.HasFlag(KeyModifiers.Control);

                if (!this.TryGetLabelEditorPixelPoint(point, out var pixelPoint))
                {
                    if (!isCtrlDown)
                    {
                        this.ClearSelectedLabelEditorHighlight();
                    }

                    e.Handled = true;
                    return;
                }

                if (isCtrlDown)
                {
                    if (this.TryGetLabelEditorHighlightAtContainerPoint(point, out var toggleIndex))
                    {
                        this.ToggleSelectedLabelEditorHighlight(this.thisLabelEditorWorkingHighlights[toggleIndex]);
                    }

                    e.Handled = true;
                    return;
                }

                if (this.TryGetSelectedLabelEditorHandleAtContainerPoint(point, out var handleIndex, out var resizeMode))
                {
                    this.StartLabelEditorDrag(handleIndex, pixelPoint, resizeMode);
                    e.Handled = true;
                    return;
                }

                if (this.TryGetSelectedLabelEditorHighlightAtContainerPoint(point, out var selectedWorkingIndex))
                {
                    this.StartLabelEditorDrag(selectedWorkingIndex, pixelPoint, LabelEditorDragMode.Move);
                    e.Handled = true;
                    return;
                }

                if (this.TryGetLabelEditorHighlightAtContainerPoint(point, out var workingIndex))
                {
                    this.SetSingleSelectedLabelEditorHighlight(this.thisLabelEditorWorkingHighlights[workingIndex], refresh: false);
                    this.StartLabelEditorDrag(workingIndex, pixelPoint, LabelEditorDragMode.Move);
                    e.Handled = true;
                    return;
                }

                this.ClearSelectedLabelEditorHighlights(refresh: false);
                this.StartDrawingLabelEditorRectangle(pixelPoint);

                e.Handled = true;
                return;
            }
        }

        if (this.thisIsKiCadCalibrationCaptureMode)
        {
            if (pointer.Properties.IsRightButtonPressed)
            {
                this.isPanning = true;
                this.panStartPoint = point;
                this.panStartMatrix = this.schematicsMatrix;

                this.HideSchematicsHoverUi();
                this.SchematicsContainer.Cursor = new Cursor(StandardCursorType.SizeAll);

                e.Pointer.Capture(this.SchematicsContainer);
                e.Handled = true;
                return;
            }

            if (pointer.Properties.IsLeftButtonPressed)
            {
                this.CaptureKiCadCalibrationPointAsync(point);
                e.Handled = true;
                return;
            }
        }

        bool hoveringComponent = this.TryGetHoveredBoardLabel(point, out var boardLabel, out var displayText);
        string? activeHoveredKiCadNetName = this.GetActiveHoveredKiCadNetName();
        bool hoveringKiCadNet = !string.IsNullOrWhiteSpace(activeHoveredKiCadNetName);

        if (TryInvert(this.schematicsMatrix, out var inv) && !hoveringKiCadNet)
        {
            var localPoint = new Point(
                (point.X * inv.M11) + (point.Y * inv.M21) + inv.M31,
                (point.X * inv.M12) + (point.Y * inv.M22) + inv.M32);

            if (this.polylineManager != null && this.polylineManager.OnPointerPressed(point, localPoint, pointer, hoveringComponent))
            {
                e.Handled = true;
                return;
            }
        }

        if (pointer.Properties.IsRightButtonPressed)
        {
            this.isPanning = true;
            this.panStartPoint = point;
            this.panStartMatrix = this.schematicsMatrix;

            this.HideSchematicsHoverUi();
            this.SchematicsContainer.Cursor = new Cursor(StandardCursorType.SizeAll);

            e.Pointer.Capture(this.SchematicsContainer);
            e.Handled = true;
            return;
        }

        if (pointer.Properties.IsLeftButtonPressed && !this.thisIsLabelEditorMode)
        {
            bool lockedChanged = false;

            if (hoveringKiCadNet)
            {
                if (this.thisLockedKiCadNetNames.Contains(activeHoveredKiCadNetName!))
                {
                    this.thisLockedKiCadNetNames.Remove(activeHoveredKiCadNetName!);
                    this.thisHoveredKiCadNetName = null;
                }
                else
                {
                    this.thisLockedKiCadNetNames.Add(activeHoveredKiCadNetName!);
                }

                lockedChanged = true;
                this.RefreshKiCadOverlay();
                this.RefreshBlinkStateFromCurrentSelection();
            }

            if (lockedChanged)
            {
                e.Handled = true;
                return;
            }

            if (hoveringComponent)
            {
                this.SelectComponentByBoardLabel(boardLabel);

                if (e.ClickCount == 1 && this.MainWindow != null)
                {
                    this.MainWindow.OpenComponentInfoPopup(boardLabel, displayText);
                }

                e.Handled = true;
                return;
            }
        }
    }

    // ###########################################################################################
    // Translates the schematics image while the right mouse button is held down.
    // Routes movement and shift key state to Polyline Manager and minimizes editor overlay churn
    // by batching transient hover-state updates instead of mutating overlay properties directly.
    // ###########################################################################################
    private void OnSchematicsPointerMoved(object? sender, PointerEventArgs e)
    {
        var point = e.GetPosition(this.SchematicsContainer);
        bool isShiftDown = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        this.UpdateInteractiveCadTraceHoverShiftState(e.KeyModifiers);

        if (!this.isPanning && this.IsPointerInsideKiCadNetConnectionsPanel(point))
        {
            this.ClearTransientHoverForKiCadNetConnectionsPanel();
            return;
        }

        if (this.thisIsLabelEditorMode && this.thisLabelEditorDragMode != LabelEditorDragMode.None)
        {
            if (this.SchematicsLabelEditorOverlay.HoveredIndex != -1)
            {
                this.SetLabelEditorOverlayTransientState(hoveredIndex: -1);
            }

            this.UpdateLabelEditorCursor(point);

            if (this.TryGetLabelEditorPixelPoint(point, out var pixelPoint))
            {
                this.UpdateLabelEditorDrag(pixelPoint, e.KeyModifiers);
            }

            e.Handled = true;
            return;
        }

        if (this.thisIsLabelEditorMode && this.thisIsDrawingLabelEditorRectangle)
        {
            if (this.SchematicsLabelEditorOverlay.HoveredIndex != -1)
            {
                this.SetLabelEditorOverlayTransientState(hoveredIndex: -1);
            }

            this.UpdateLabelEditorCursor(point);

            if (this.TryGetLabelEditorPixelPoint(point, out var pixelPoint))
            {
                this.UpdateDrawingLabelEditorRectangle(pixelPoint);
            }

            e.Handled = true;
            return;
        }

        if (!this.thisIsLabelEditorMode && TryInvert(this.schematicsMatrix, out var inv))
        {
            var localPoint = new Point(
                (point.X * inv.M11) + (point.Y * inv.M21) + inv.M31,
                (point.X * inv.M12) + (point.Y * inv.M22) + inv.M32);

            if (this.polylineManager != null && this.polylineManager.OnPointerMoved(localPoint, isShiftDown))
            {
                e.Handled = true;
                return;
            }
        }

        if (this.isPanning)
        {
            var delta = point - this.panStartPoint;
            this.schematicsMatrix = this.panStartMatrix * Matrix.CreateTranslation(delta.X, delta.Y);
            this.ClampSchematicsMatrix();
            e.Handled = true;
            return;
        }

        if (!this.thisIsLabelEditorMode && !this.thisIsKiCadCalibrationCaptureMode)
        {
            if (this.ShouldProcessKiCadHoverHitTest(point))
            {
                this.HitTestKiCadOverlayForHover(point);
            }
        }

        if (this.thisIsLabelEditorMode)
        {
            int hoveredIndex = -1;

            if (this.TryGetSelectedLabelEditorHighlightAtContainerPoint(point, out var hoveredSelectedIndex))
            {
                hoveredIndex = hoveredSelectedIndex;
            }

            if (this.SchematicsLabelEditorOverlay.HoveredIndex != hoveredIndex)
            {
                this.SetLabelEditorOverlayTransientState(hoveredIndex: hoveredIndex);
            }
        }

        this.UpdateSchematicsHoverUi(point);
    }

    // ###########################################################################################
    // Returns true when the pointer is inside the currently selected rectangle's move or resize
    // interaction area so cursor feedback and marker visibility activate at the same time.
    // ###########################################################################################
    private bool IsPointerOverSelectedLabelEditorInteraction(Point pointerInContainer)
    {
        if (!this.HasSelectedLabelEditorHighlightsForCurrentSchematic())
        {
            return false;
        }

        if (this.TryGetSelectedLabelEditorHandleAtContainerPoint(pointerInContainer, out _))
        {
            return true;
        }

        return this.TryGetSelectedLabelEditorHighlightAtContainerPoint(pointerInContainer, out _);
    }

    // ###########################################################################################
    // Exits pan mode when the right mouse button is released, or finalized polyline logic.
    // Also evaluates if the release qualifies as a stationary right-click to toggle selection
    // or show the label-editor action menu on empty space.
    // ###########################################################################################
    private void OnSchematicsPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var point = e.GetPosition(this.SchematicsContainer);

        this.UpdateInteractiveCadTraceHoverShiftState(e.KeyModifiers);

        if (!this.isPanning && this.IsPointerInsideKiCadNetConnectionsPanel(point))
        {
            this.ClearTransientHoverForKiCadNetConnectionsPanel();
            return;
        }

        if (this.thisIsLabelEditorMode && this.thisLabelEditorDragMode != LabelEditorDragMode.None)
        {
            this.CompleteLabelEditorDrag();
            this.UpdateLabelEditorCursor(point);
            e.Handled = true;
            return;
        }

        if (this.thisIsLabelEditorMode && this.thisIsDrawingLabelEditorRectangle)
        {
            if (this.TryGetLabelEditorPixelPoint(point, out var pixelPoint))
            {
                this.CompleteDrawingLabelEditorRectangle(point, pixelPoint);
            }
            else
            {
                this.thisIsDrawingLabelEditorRectangle = false;
                this.thisLabelEditorDraftRectangle = null;
                this.RefreshLabelEditorOverlay();
            }

            this.UpdateLabelEditorCursor(point);
            e.Handled = true;
            return;
        }

        if (!this.thisIsLabelEditorMode && TryInvert(this.schematicsMatrix, out var inv))
        {
            var localPoint = new Point(
                (point.X * inv.M11) + (point.Y * inv.M21) + inv.M31,
                (point.X * inv.M12) + (point.Y * inv.M22) + inv.M32);

            if (this.polylineManager != null && this.polylineManager.OnPointerReleased(point, localPoint))
            {
                e.Handled = true;
                return;
            }
        }

        if (!this.isPanning)
            return;

        this.isPanning = false;
        e.Pointer.Capture(null);

        var delta = point - this.panStartPoint;
        bool isStationaryRightClick = Math.Abs(delta.X) < 4 && Math.Abs(delta.Y) < 4;

        if (isStationaryRightClick)
        {
            if (this.thisIsKiCadCalibrationCaptureMode)
            {
                this.ShowLabelEditorMenu(point);
            }
            else if (this.thisIsLabelEditorMode)
            {
                if (this.TryGetLabelEditorHighlightAtContainerPoint(point, out var workingIndex))
                {
                    this.DeleteLabelEditorHighlight(workingIndex);
                    this.HideLabelEditorMenu();
                }
                else
                {
                    this.ShowLabelEditorMenu(point);
                }
            }
            else
            {
                string? activeHoveredKiCadNetName = this.GetActiveHoveredKiCadNetName();

                if (this.TryGetHoveredBoardLabel(point, out var boardLabel, out _))
                {
                    this.ToggleComponentSelectionByBoardLabel(boardLabel);
                }
                else if (!string.IsNullOrWhiteSpace(activeHoveredKiCadNetName) && this.thisLockedKiCadNetNames.Contains(activeHoveredKiCadNetName))
                {
                    // Deselects the currently hovered item with a right click
                    this.thisLockedKiCadNetNames.Remove(activeHoveredKiCadNetName);
                    this.thisHoveredKiCadNetName = null; // Clear hover state immediately
                    this.RefreshKiCadOverlay();
                    this.RefreshBlinkStateFromCurrentSelection();
                }
                else if (this.thisLockedKiCadNetNames.Count > 0)
                {
                    // Right clicking anywhere else on an empty un-hovered space clears all locked KiCad traces 
                    this.thisLockedKiCadNetNames.Clear();
                    this.RefreshKiCadOverlay();
                    this.RefreshBlinkStateFromCurrentSelection();
                }
                else
                {
                    this.ShowLabelEditorMenu(point);
                }
            }
        }

        this.UpdateSchematicsHoverUi(e.GetPosition(this.SchematicsContainer));
        e.Handled = true;
    }

    // ###########################################################################################
    // Updates the displayed region and schematic name overlays.
    // ###########################################################################################
    public void UpdateOverlayLabels()
    {
        var selected = this.SchematicsThumbnailList.SelectedItem as SchematicThumbnail;

        string schematicName = selected?.Name ?? string.Empty;
        this.SchematicsNameLabel.Text = schematicName;
        this.SchematicsNameBorder.IsVisible = !string.IsNullOrWhiteSpace(schematicName);

        // Fetch the active local region from the Main window, falling back to UserSettings if not attached
        string rawRegion = this.MainWindow?.LocalRegion?.Trim() ?? UserSettings.Region?.Trim() ?? string.Empty;
        this.SchematicsRegionLabel.Text = string.IsNullOrWhiteSpace(rawRegion) ? "All Regions" : rawRegion;

        bool hasExplicitRegions = this.MainWindow?.CurrentBoardHasExplicitRegionComponents() ?? true;
        this.SchematicsRegionBorder.IsVisible = this.SchematicsNameBorder.IsVisible && hasExplicitRegions;

        string regionKey = rawRegion.ToUpperInvariant();

        string colorPrefix = regionKey switch
        {
            "PAL" => "Schematics_Region_PAL",
            "NTSC" => "Schematics_Region_NTSC",
            _ => "SchematicsRegion"
        };

        this.SchematicsRegionBorder.Bind(
            Border.BackgroundProperty,
            this.GetResourceObservable($"{colorPrefix}_Bg"));

        this.SchematicsRegionBorder.Bind(
            Border.BorderBrushProperty,
            this.GetResourceObservable($"{colorPrefix}_Border"));

        this.SchematicsRegionLabel.Bind(
            TextBlock.ForegroundProperty,
            this.GetResourceObservable($"{colorPrefix}_Fg"));
    }

    // ###########################################################################################
    // Loads the full-resolution image for the selected thumbnail and sets up the highlight overlay.
    // ###########################################################################################
    private async void OnSchematicsThumbnailSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (this.thisSuppressThumbnailSelectionChanged)
            return;

        this.UpdateOverlayLabels();

        this.fullResLoadCts?.Cancel();
        this.fullResLoadCts = new CancellationTokenSource();
        var cts = this.fullResLoadCts;

        var selected = this.SchematicsThumbnailList.SelectedItem as SchematicThumbnail;

        this.thisHoveredComponentBoardLabel = null;

        this.ResetKiCadHoverHitTestThrottle();

        this.SchematicsImage.Source = null;
        this.SchematicsMissingImageText.IsVisible = false; // Hide while loading
        this.schematicsMatrix = Matrix.Identity;
        ((MatrixTransform)this.SchematicsImage.RenderTransform!).Matrix = this.schematicsMatrix;
        ((MatrixTransform)this.SchematicsHighlightsOverlay.RenderTransform!).Matrix = this.schematicsMatrix;
        ((MatrixTransform)this.SchematicsHoverHighlightsOverlay.RenderTransform!).Matrix = this.schematicsMatrix;

        this.SchematicsHighlightsOverlay.HighlightIndex = null;
        this.SchematicsHighlightsOverlay.BitmapPixelSize = new PixelSize(0, 0);
        this.SchematicsHighlightsOverlay.ViewMatrix = this.schematicsMatrix;

        this.SchematicsHoverHighlightsOverlay.HighlightIndex = null;
        this.SchematicsHoverHighlightsOverlay.BitmapPixelSize = new PixelSize(0, 0);
        this.SchematicsHoverHighlightsOverlay.ViewMatrix = this.schematicsMatrix;

        // Save the newly selected schematic for this board
        var boardKey = this.MainWindow?.GetCurrentBoardKey();
        if (!string.IsNullOrEmpty(boardKey) && selected != null)
        {
            UserSettings.SetLastSchematicForBoard(boardKey, selected.Name);
        }

        this.RestoreBoardSettings(boardKey ?? string.Empty);

        if (selected == null || string.IsNullOrEmpty(selected.ImageFilePath))
            return;

        var bitmap = await Task.Run(() =>
        {
            if (cts.Token.IsCancellationRequested) return null;

            try { return new Bitmap(selected.ImageFilePath); }
            catch (Exception ex)
            {
                Logger.Warning($"Cannot load image file [{selected.ImageFilePath}] - [{ex.Message}]");
                return null;
            }
        }, cts.Token);

        if (cts.Token.IsCancellationRequested || !ReferenceEquals(cts, this.fullResLoadCts))
        {
            bitmap?.Dispose();
            return;
        }

        this.currentFullResBitmap?.Dispose();
        this.currentFullResBitmap = bitmap;
        this.SchematicsImage.Source = bitmap;

        if (bitmap != null)
        {
            this.SchematicsMissingImageText.IsVisible = false;

            // Always set BitmapPixelSize so the overlay can render as soon as a component is selected,
            // even if no highlight index exists yet at the time this schematic loads.
            this.SchematicsHighlightsOverlay.BitmapPixelSize = bitmap.PixelSize;
            this.SchematicsHoverHighlightsOverlay.BitmapPixelSize = bitmap.PixelSize;

            if (this.highlightIndexBySchematic.TryGetValue(selected.Name, out var index) &&
                this.schematicByName.TryGetValue(selected.Name, out var schematic))
            {
                this.SchematicsHighlightsOverlay.HighlightIndex = index;
                this.SchematicsHighlightsOverlay.HighlightColor = ParseColorOrDefault(schematic.MainImageHighlightColor, Colors.IndianRed);
                this.SchematicsHighlightsOverlay.HighlightOpacity = ParseOpacityOrDefault(schematic.MainHighlightOpacity, 0.20);
            }
        }
        else
        {
            this.SchematicsMissingImageText.IsVisible = true;
        }

        this.SchematicsHighlightsOverlay.ViewMatrix = this.schematicsMatrix;
        this.SchematicsHighlightsOverlay.InvalidateVisual();
        this.SchematicsHoverHighlightsOverlay.ViewMatrix = this.schematicsMatrix;
        this.SchematicsHoverHighlightsOverlay.InvalidateVisual();

        // Populate trace database for this exact board/schematic setup immediately before render logic finishes 
        if (this.polylineManager != null && !string.IsNullOrEmpty(boardKey))
        {
            var loaded = TraceStorage.GetTraces(boardKey, selected.Name);
            this.polylineManager.ImportTraces(loaded);
        }

        // Defer a clamp call so the engine can measure and center the new image layout 
        // immediately instead of waiting for a window resize or banner collapse.
        Dispatcher.UIThread.Post(() =>
        {
            this.ClampSchematicsMatrix();
            this.UpdateComponentLabels();
            this.RefreshHoveredComponentHighlightOverlay();
            this.RefreshKiCadOverlay();
        });
    }

    // ###########################################################################################
    // Saves the schematics/thumbnail split ratio for the current board after the drag ends.
    // Deferred via Post to ensure Bounds reflects the completed layout pass.
    // ###########################################################################################
    private void OnSchematicsSplitterPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var boardKey = this.MainWindow?.GetCurrentBoardKey();
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
    // Clears the main schematics image and resets the zoom and highlight overlay state.
    // Also clears cached KiCad PCB graph data so the next board starts from a clean state.
    // ###########################################################################################
    public void ResetSchematicsViewer()
    {
        this.thisIsKiCadOverlayRefreshQueued = false;
        this.thisKiCadOverlayRefreshRequestVersion = 0;
        this.thisKiCadOverlayLastRenderedVersion = 0;
        this.thisKiCadPcbNetRenderCacheByKey.Clear();

        this.SchematicsKiCadOverlayCanvas.ClearGeometry();
        ((MatrixTransform)this.SchematicsKiCadOverlayCanvas.RenderTransform!).Matrix = this.schematicsMatrix;

        this.KiCadNetConnectionsPanel.IsVisible = false;
        this.KiCadNetConnectionsList.ItemsSource = null;

        this.thisSelectedKiCadReferences.Clear();
        this.thisSelectedKiCadNormalizedNetNames.Clear();

        this.thisHoveredKiCadNetName = null;
        this.thisHoveredKiCadPadNumber = null;
        this.thisHoveredComponentBoardLabel = null;
        this.thisSchematicsOnlySelectedBoardLabels.Clear();
        this.thisLockedKiCadNetNames.Clear();

        this.thisIsKiCadCalibrationCaptureMode = false;
        this.thisKiCadCalibrationImagePoints.Clear();

        this.thisIsDraggingThumbnail = false;
        this.thisDraggedThumbnail = null;
        this.thisDraggedThumbnailOriginalIndex = -1;
        this.thisDraggedThumbnailWasSelected = false;
        this.thisThumbnailLastPointerYInList = double.NaN;
        this.thisThumbnailDragStartEventArgs = null;
        this.ClearThumbnailDropPlaceholder();
        this.HideThumbnailDragGhost();

        this.polylineManager?.Reset();
        this.SchematicsLabelsCanvas.Children.Clear();
        this.ResetComponentLabelVisualCaches();

        this.SetTraceColorPickerColor(Colors.White);
        this.CustomColorButton.Background = Brushes.White;

        this.ResetKiCadHoverHitTestThrottle();
        this.thisLastKiCadNetConnectionsSignature = string.Empty;
        this.thisLastThumbnailHighlightSignature = string.Empty;

        this.fullResLoadCts?.Cancel();
        this.fullResLoadCts = null;

        this.currentFullResBitmap?.Dispose();
        this.currentFullResBitmap = null;

        this.SchematicsNameBorder.IsVisible = false;
        this.SchematicsRegionBorder.IsVisible = false;

        this.SchematicsImage.Source = null;
        this.SchematicsMissingImageText.IsVisible = false;

        this.schematicsMatrix = Matrix.Identity;
        ((MatrixTransform)this.SchematicsImage.RenderTransform!).Matrix = this.schematicsMatrix;
        ((MatrixTransform)this.SchematicsHighlightsOverlay.RenderTransform!).Matrix = this.schematicsMatrix;
        ((MatrixTransform)this.SchematicsHoverHighlightsOverlay.RenderTransform!).Matrix = this.schematicsMatrix;
        ((MatrixTransform)this.SchematicsLabelEditorOverlay.RenderTransform!).Matrix = this.schematicsMatrix;

        this.SchematicsHighlightsOverlay.HighlightIndex = null;
        this.SchematicsHighlightsOverlay.BitmapPixelSize = new PixelSize(0, 0);
        this.SchematicsHighlightsOverlay.ViewMatrix = this.schematicsMatrix;

        this.SchematicsHoverHighlightsOverlay.HighlightIndex = null;
        this.SchematicsHoverHighlightsOverlay.BitmapPixelSize = new PixelSize(0, 0);
        this.SchematicsHoverHighlightsOverlay.ViewMatrix = this.schematicsMatrix;

        this.thisIsLabelEditorMode = false;
        this.thisLabelEditorSchematicName = string.Empty;
        this.thisSelectedLabelEditorHighlights.Clear();
        this.thisSelectedLabelEditorHighlight = null;
        this.thisPendingNewLabelEditorHighlight = null;
        this.thisIsDrawingLabelEditorRectangle = false;
        this.thisLabelEditorDraftRectangle = null;
        this.thisLabelEditorDragMode = LabelEditorDragMode.None;
        this.thisLabelEditorOriginalSelectionBounds = default;
        this.thisLabelEditorOriginalDragRectangles.Clear();
        this.thisLabelEditorWorkingHighlights.Clear();
        this.HideLabelEditorMenu();
        this.HideNewLabelEditorPrompt();

        this.SchematicsLabelEditorOverlay.ApplyState(
            rectangles: Array.Empty<Rect>(),
            selectedIndex: -1,
            selectedIndices: Array.Empty<int>(),
            selectionBounds: null,
            hoveredIndex: -1,
            draftRectangle: null,
            snapGuides: Array.Empty<(Point Start, Point End)>(),
            bitmapPixelSize: new PixelSize(0, 0),
            viewMatrix: this.schematicsMatrix,
            highlightColor: Colors.IndianRed,
            highlightOpacity: 0.20,
            isVisible: false);

        this.RestoreBoardSettings(string.Empty);

        this.isPanning = false;
        this.HideSchematicsHoverUi();
    }

    // ###########################################################################################
    // Returns the rectangle (in the image control's local coordinate space) that the actual
    // bitmap content occupies, accounting for Stretch="Uniform" letterboxing on either axis.
    // ###########################################################################################
    internal Rect GetImageContentRect()
    {
        var imageSize = this.SchematicsImage.Bounds.Size;
        var bitmap = this.currentFullResBitmap;

        if (bitmap == null || imageSize.Width <= 0 || imageSize.Height <= 0)
            return new Rect(imageSize);

        double containerAspect = imageSize.Width / imageSize.Height;
        double bitmapAspect = bitmap.Size.Width / bitmap.Size.Height;

        double contentX, contentY, contentWidth, contentHeight;

        if (bitmapAspect > containerAspect)
        {
            contentWidth = imageSize.Width;
            contentHeight = imageSize.Width / bitmapAspect;
            contentX = 0;
            contentY = (imageSize.Height - contentHeight) / 2.0;
        }
        else
        {
            contentHeight = imageSize.Height;
            contentWidth = imageSize.Height * bitmapAspect;
            contentX = (imageSize.Width - contentWidth) / 2.0;
            contentY = 0;
        }

        return new Rect(contentX, contentY, contentWidth, contentHeight);
    }

    // ###########################################################################################
    // Clamps the current schematics matrix so no empty space is visible inside the container.
    // Updates the editor overlay through one batched state apply so pan/zoom only triggers a
    // single redraw instead of multiple overlay invalidations.
    // ###########################################################################################
    private void ClampSchematicsMatrix()
    {
        var containerSize = this.SchematicsContainer.Bounds.Size;
        if (containerSize.Width <= 0 || containerSize.Height <= 0)
        {
            return;
        }

        var contentRect = this.GetImageContentRect();
        double scale = this.schematicsMatrix.M11;
        double tx = this.schematicsMatrix.M31;
        double ty = this.schematicsMatrix.M32;

        var transformedRect = contentRect.TransformToAABB(this.schematicsMatrix);

        double scaledWidth = transformedRect.Width;
        double scaledHeight = transformedRect.Height;
        double scaledLeft = transformedRect.Left;
        double scaledTop = transformedRect.Top;
        double scaledRight = transformedRect.Right;
        double scaledBottom = transformedRect.Bottom;

        if (scaledWidth >= containerSize.Width)
        {
            if (scaledLeft > 0)
            {
                tx -= scaledLeft;
            }
            else if (scaledRight < containerSize.Width)
            {
                tx += containerSize.Width - scaledRight;
            }
        }
        else
        {
            tx = (containerSize.Width - scaledWidth) / 2.0 - scale * contentRect.Left;
        }

        if (scaledHeight >= containerSize.Height)
        {
            if (scaledTop > 0)
            {
                ty -= scaledTop;
            }
            else if (scaledBottom < containerSize.Height)
            {
                ty += containerSize.Height - scaledBottom;
            }
        }
        else
        {
            ty = -(scale * contentRect.Top);
        }

        this.schematicsMatrix = new Matrix(scale, 0, 0, scale, tx, ty);

        ((MatrixTransform)this.SchematicsImage.RenderTransform!).Matrix = this.schematicsMatrix;
        ((MatrixTransform)this.SchematicsHighlightsOverlay.RenderTransform!).Matrix = this.schematicsMatrix;
        ((MatrixTransform)this.SchematicsHoverHighlightsOverlay.RenderTransform!).Matrix = this.schematicsMatrix;
        ((MatrixTransform)this.SchematicsLabelEditorOverlay.RenderTransform!).Matrix = this.schematicsMatrix;
        ((MatrixTransform)this.SchematicsPolylineCanvas.RenderTransform!).Matrix = this.schematicsMatrix;
        ((MatrixTransform)this.SchematicsLabelsCanvas.RenderTransform!).Matrix = this.schematicsMatrix;
        ((MatrixTransform)this.SchematicsKiCadOverlayCanvas.RenderTransform!).Matrix = this.schematicsMatrix;

        this.SchematicsHighlightsOverlay.ViewMatrix = this.schematicsMatrix;
        this.SchematicsHighlightsOverlay.InvalidateVisual();

        this.SchematicsHoverHighlightsOverlay.ViewMatrix = this.schematicsMatrix;
        this.SchematicsHoverHighlightsOverlay.InvalidateVisual();

        this.SchematicsLabelEditorOverlay.ApplyState(
            rectangles: this.SchematicsLabelEditorOverlay.Rectangles,
            selectedIndex: this.SchematicsLabelEditorOverlay.SelectedIndex,
            selectedIndices: this.SchematicsLabelEditorOverlay.SelectedIndices,
            selectionBounds: this.SchematicsLabelEditorOverlay.SelectionBounds,
            hoveredIndex: this.SchematicsLabelEditorOverlay.HoveredIndex,
            draftRectangle: this.SchematicsLabelEditorOverlay.DraftRectangle,
            snapGuides: this.SchematicsLabelEditorOverlay.SnapGuides,
            bitmapPixelSize: this.SchematicsLabelEditorOverlay.BitmapPixelSize,
            viewMatrix: this.schematicsMatrix,
            highlightColor: this.SchematicsLabelEditorOverlay.HighlightColor,
            highlightOpacity: this.SchematicsLabelEditorOverlay.HighlightOpacity,
            isVisible: this.SchematicsLabelEditorOverlay.IsVisible);

        this.polylineManager?.UpdateScaleFactor(scale);
        this.UpdateComponentLabelsScale(scale);
    }

    // ###########################################################################################
    // Applies inverse scale to mapped labels so they remain standard text size regardless of zoom.
    // Uses cached editor or standard scale transforms directly when available to avoid repeated
    // child-tree scanning and LINQ allocations during pan and zoom.
    // ###########################################################################################
    private void UpdateComponentLabelsScale(double scale)
    {
        double inverseScale = scale > 0 ? 1.0 / scale : 1.0;

        if (this.thisIsLabelEditorMode && this.thisEditorLabelScaleTransforms.Count > 0)
        {
            for (int i = 0; i < this.thisEditorLabelScaleTransforms.Count; i++)
            {
                this.thisEditorLabelScaleTransforms[i].ScaleX = inverseScale;
                this.thisEditorLabelScaleTransforms[i].ScaleY = inverseScale;
            }

            return;
        }

        if (this.thisStandardLabelScaleTransforms.Count > 0)
        {
            for (int i = 0; i < this.thisStandardLabelScaleTransforms.Count; i++)
            {
                this.thisStandardLabelScaleTransforms[i].ScaleX = inverseScale;
                this.thisStandardLabelScaleTransforms[i].ScaleY = inverseScale;
            }

            return;
        }

        foreach (var child in this.SchematicsLabelsCanvas.Children)
        {
            if (child is Border container && container.RenderTransform is TransformGroup group)
            {
                var scaleTransform = group.Children.OfType<ScaleTransform>().FirstOrDefault();
                if (scaleTransform != null)
                {
                    scaleTransform.ScaleX = inverseScale;
                    scaleTransform.ScaleY = inverseScale;
                }
            }
        }
    }

    // ###########################################################################################
    // Generates floating labels exactly above relevant components based on checkbox matrix.
    // While label-editor mode is active, board labels from the working copy are shown immediately
    // so newly confirmed labels appear without waiting for Apply.
    // ###########################################################################################
    public void UpdateComponentLabels()
    {
        if (this.currentFullResBitmap == null)
        {
            this.SchematicsLabelsCanvas.Children.Clear();
            this.ResetComponentLabelVisualCaches();
            return;
        }

        double imgWidth = this.currentFullResBitmap.PixelSize.Width;
        double imgHeight = this.currentFullResBitmap.PixelSize.Height;
        if (imgWidth <= 0 || imgHeight <= 0)
        {
            this.SchematicsLabelsCanvas.Children.Clear();
            this.ResetComponentLabelVisualCaches();
            return;
        }

        var selectedThumb = this.SchematicsThumbnailList.SelectedItem as SchematicThumbnail;
        if (selectedThumb == null)
        {
            this.SchematicsLabelsCanvas.Children.Clear();
            this.ResetComponentLabelVisualCaches();
            return;
        }

        var contentRect = this.GetImageContentRect();

        double currentScale = this.schematicsMatrix.M11;
        double inverseScale = currentScale > 0 ? 1.0 / currentScale : 1.0;

        if (this.thisIsLabelEditorMode)
        {
            if (this.thisStandardLabelContainers.Count > 0)
            {
                this.SchematicsLabelsCanvas.Children.Clear();
                this.ResetStandardComponentLabelVisualCache();
            }

            var editorRows = this.thisLabelEditorWorkingHighlights
                .Where(row =>
                    string.Equals(row.SchematicName, selectedThumb.Name, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(row.BoardLabel))
                .ToList();

            this.UpdateEditorComponentLabels(editorRows, contentRect, imgWidth, imgHeight, inverseScale);
            return;
        }

        if (this.thisEditorLabelContainers.Count > 0)
        {
            this.SchematicsLabelsCanvas.Children.Clear();
            this.ResetEditorComponentLabelVisualCache();
        }

        if (this.CheckLabelBoard.IsChecked != true &&
            this.CheckLabelTechnical.IsChecked != true &&
            this.CheckLabelFriendly.IsChecked != true)
        {
            for (int i = 0; i < this.thisStandardLabelContainers.Count; i++)
            {
                this.thisStandardLabelContainers[i].IsVisible = false;
            }

            return;
        }

        if (this.MainWindow == null)
        {
            for (int i = 0; i < this.thisStandardLabelContainers.Count; i++)
            {
                this.thisStandardLabelContainers[i].IsVisible = false;
            }

            return;
        }

        if (!this.highlightRectsBySchematicAndLabel.TryGetValue(selectedThumb.Name, out var byLabel))
        {
            for (int i = 0; i < this.thisStandardLabelContainers.Count; i++)
            {
                this.thisStandardLabelContainers[i].IsVisible = false;
            }

            return;
        }

        var visibleItems = this.MainWindow.ComponentFilterListBox.ItemsSource?.Cast<Main.ComponentListItem>().ToList() ?? new List<Main.ComponentListItem>();
        var selectedItems = this.MainWindow.ComponentFilterListBox.SelectedItems?.Cast<Main.ComponentListItem>().ToList() ?? new List<Main.ComponentListItem>();

        bool selectedOnly = this.CheckLabelSelectedOnly.IsChecked == true;
        var itemsToLoop = selectedOnly ? selectedItems : visibleItems;
        var seenLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var standardLabels = new List<(string Text, double LocalX, double LocalY)>();

        foreach (var item in itemsToLoop)
        {
            if (string.IsNullOrWhiteSpace(item.BoardLabel)) continue;
            if (!seenLabels.Add(item.BoardLabel)) continue;

            if (!byLabel.TryGetValue(item.BoardLabel, out var rects) || rects.Count == 0) continue;

            var parts = item.SelectionKey?.Split('\u001F') ?? Array.Empty<string>();
            string friendlyName = parts.Length > 1 ? parts[1] : string.Empty;
            string technicalName = parts.Length > 2 ? parts[2] : string.Empty;

            var lines = new List<string>();
            if (this.CheckLabelBoard.IsChecked == true && !string.IsNullOrWhiteSpace(item.BoardLabel)) lines.Add(item.BoardLabel);
            if (this.CheckLabelTechnical.IsChecked == true && !string.IsNullOrWhiteSpace(technicalName)) lines.Add(technicalName);
            if (this.CheckLabelFriendly.IsChecked == true && !string.IsNullOrWhiteSpace(friendlyName)) lines.Add(friendlyName);

            if (lines.Count == 0) continue;

            string labelText = string.Join("\n", lines);

            foreach (var r in rects)
            {
                double centerX = r.X + (r.Width / 2.0);
                double centerY = r.Y + (r.Height / 2.0);

                double localX = contentRect.X + (centerX / imgWidth) * contentRect.Width;
                double localY = contentRect.Y + (centerY / imgHeight) * contentRect.Height;

                standardLabels.Add((labelText, localX, localY));
            }
        }

        this.UpdateStandardComponentLabels(standardLabels, inverseScale);
    }

    // ###########################################################################################
    // Creates a pre-scaled bitmap from a full-resolution source image.
    // ###########################################################################################
    public static RenderTargetBitmap CreateScaledThumbnail(Bitmap source, int maxWidth)
    {
        double scale = Math.Min(1.0, (double)maxWidth / source.PixelSize.Width);
        int tw = Math.Max(1, (int)(source.PixelSize.Width * scale));
        int th = Math.Max(1, (int)(source.PixelSize.Height * scale));

        var imageControl = new Image { Source = source, Stretch = Stretch.Uniform };
        imageControl.Measure(new Size(tw, th));
        imageControl.Arrange(new Rect(0, 0, tw, th));

        var rtb = new RenderTargetBitmap(new PixelSize(tw, th), new Vector(96, 96));
        rtb.Render(imageControl);
        return rtb;
    }

    // ###########################################################################################
    // Composites highlight rectangles onto a base thumbnail and returns the new rendered bitmap.
    // ###########################################################################################
    public static RenderTargetBitmap CreateHighlightedThumbnail(
        IImage baseThumbnail, PixelSize originalPixelSize,
        HighlightSpatialIndex index, BoardSchematicEntry schematic, double opacityMultiplier = 1.0)
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
            HighlightOpacity = ParseOpacityOrDefault(schematic.ThumbnailHighlightOpacity, 0.20) * Math.Clamp(opacityMultiplier, 0.0, 1.0),
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
    // Rebuilds highlight indices from the selected board labels, then applies highlight visuals
    // to the main schematic and all thumbnails.
    // Also preserves schematic-only selections for components hidden by category/search filters.
    // ###########################################################################################
    public void UpdateHighlightsForComponents(List<string> boardLabels)
    {
        this.ClearVisibleBoardLabelsFromSchematicsOnlySelection();

        var effectiveBoardLabels = new HashSet<string>(
            boardLabels.Where(label => !string.IsNullOrWhiteSpace(label)),
            StringComparer.OrdinalIgnoreCase);

        foreach (string boardLabel in this.thisSchematicsOnlySelectedBoardLabels)
        {
            effectiveBoardLabels.Add(boardLabel);
        }

        this.highlightIndexBySchematic = new(StringComparer.OrdinalIgnoreCase);

        if (effectiveBoardLabels.Count > 0)
        {
            foreach (var (schematicName, byLabel) in this.highlightRectsBySchematicAndLabel)
            {
                var rects = new List<Rect>();

                foreach (string boardLabel in effectiveBoardLabels)
                {
                    if (byLabel.TryGetValue(boardLabel, out var labelRects))
                    {
                        rects.AddRange(labelRects);
                    }
                }

                if (rects.Count > 0)
                {
                    this.highlightIndexBySchematic[schematicName] = new HighlightSpatialIndex(rects);
                }
            }
        }

        this.UpdateKiCadSelectionFromBoardLabels(effectiveBoardLabels);
        this.RefreshBlinkStateFromCurrentSelection();
        this.UpdateComponentLabels();
        this.RefreshHoveredComponentHighlightOverlay();
    }

    // ###########################################################################################
    // Returns true when there is any selection that should participate in "Blink selected".
    // Includes selected components and explicitly selected KiCad nets, but not hover-only nets.
    // ###########################################################################################
    public bool HasBlinkEligibleSelection()
    {
        return this.highlightIndexBySchematic.Count > 0 ||
               this.thisLockedKiCadNetNames.Count > 0;
    }

    // ###########################################################################################
    // Recomputes blink timer state and reapplies visuals after KiCad net selection changes.
    // This keeps blinking responsive even when no component selection exists.
    // ###########################################################################################
    private void RefreshBlinkStateFromCurrentSelection()
    {
        bool hasBlinkEligibleSelection = this.HasBlinkEligibleSelection();
        bool hasComponentSelection = this.highlightIndexBySchematic.Count > 0;

        if (this.MainWindow != null)
        {
            this.MainWindow.UpdateBlinkTimer(hasBlinkEligibleSelection);
            this.ApplyHighlightVisuals(
                hasComponentSelection,
                this.MainWindow.GetCurrentBlinkFactor(hasBlinkEligibleSelection));
            return;
        }

        this.ApplyHighlightVisuals(hasComponentSelection, 1.0);
    }

    // ###########################################################################################
    // Applies current highlight visuals (including blink phase) to main schematic and thumbnails.
    // Blink-only updates no longer regenerate all thumbnail bitmaps unless the actual selection
    // set changed, which avoids heavy redraw churn during blinking.
    // ###########################################################################################
    public void ApplyHighlightVisuals(bool hasSelection, double blinkFactor)
    {
        this.thisCurrentHighlightBlinkFactor = Math.Clamp(blinkFactor, 0.0, 1.0);

        var selectedThumb = this.SchematicsThumbnailList.SelectedItem as SchematicThumbnail;
        if (selectedThumb != null &&
            this.highlightIndexBySchematic.TryGetValue(selectedThumb.Name, out var mainIndex) &&
            this.schematicByName.TryGetValue(selectedThumb.Name, out var mainSchematic))
        {
            this.SchematicsHighlightsOverlay.HighlightIndex = mainIndex;
            this.SchematicsHighlightsOverlay.BitmapPixelSize = this.currentFullResBitmap?.PixelSize ?? new PixelSize(0, 0);
            this.SchematicsHighlightsOverlay.HighlightColor = ParseColorOrDefault(mainSchematic.MainImageHighlightColor, Colors.IndianRed);
            this.SchematicsHighlightsOverlay.HighlightOpacity =
                ParseOpacityOrDefault(mainSchematic.MainHighlightOpacity, 0.20) * this.thisCurrentHighlightBlinkFactor;
        }
        else
        {
            this.SchematicsHighlightsOverlay.HighlightIndex = null;
        }

        this.SchematicsHighlightsOverlay.InvalidateVisual();

        bool hasKiCadSelection = this.HasKiCadSelectionForThumbnailDimming();
        bool hasAnyThumbnailSelection = hasSelection || hasKiCadSelection;

        string thumbnailSignature = this.BuildThumbnailHighlightSignature(hasSelection, hasKiCadSelection);

        if (!string.Equals(this.thisLastThumbnailHighlightSignature, thumbnailSignature, StringComparison.Ordinal))
        {
            this.thisLastThumbnailHighlightSignature = thumbnailSignature;

            foreach (var thumb in this.currentThumbnails)
            {
                if (thumb.BaseThumbnail == null)
                {
                    continue;
                }

                bool hasComponentMatch = false;
                bool hasKiCadMatch = false;

                if (this.highlightIndexBySchematic.TryGetValue(thumb.Name, out var thumbIndex) &&
                    this.schematicByName.TryGetValue(thumb.Name, out var thumbSchematic))
                {
                    hasComponentMatch = true;

                    var highlighted = CreateHighlightedThumbnail(
                        thumb.BaseThumbnail,
                        thumb.OriginalPixelSize,
                        thumbIndex,
                        thumbSchematic,
                        opacityMultiplier: 1.0);

                    var old = thumb.ImageSource;
                    thumb.ImageSource = highlighted;

                    if (!ReferenceEquals(old, thumb.BaseThumbnail))
                    {
                        (old as IDisposable)?.Dispose();
                    }
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

                if (hasKiCadSelection)
                {
                    hasKiCadMatch = this.DoesSchematicContainSelectedKiCadContent(thumb.Name);
                }

                bool hasMatch = hasComponentMatch || hasKiCadMatch;
                bool isRelevantForDimming = !hasAnyThumbnailSelection || hasMatch;

                thumb.VisualOpacity = isRelevantForDimming ? 1.0 : 0.35;
                thumb.IsMatchForSelection = hasAnyThumbnailSelection && hasMatch;
            }
        }

        this.RefreshKiCadOverlay(forceImmediate: true);
    }

    // ###########################################################################################
    // Returns true when KiCad trace or pad selections should participate in thumbnail dimming.
    // Hover-only KiCad nets are excluded so thumbnails do not flicker while moving the pointer.
    // ###########################################################################################
    private bool HasKiCadSelectionForThumbnailDimming()
    {
        return this.thisSelectedKiCadNormalizedNetNames.Count > 0 ||
               this.thisLockedKiCadNetNames.Count > 0;
    }

    // ###########################################################################################
    // Returns true when the requested schematic contains any currently selected KiCad net content.
    // Used to dim thumbnails that do not include the selected traces, pads, or lines.
    // ###########################################################################################
    private bool DoesSchematicContainSelectedKiCadContent(string schematicName)
    {
        if (string.IsNullOrWhiteSpace(schematicName) || this.thisKiCadProject == null)
        {
            return false;
        }

        var selectedNets = new HashSet<string>(this.thisSelectedKiCadNormalizedNetNames, StringComparer.OrdinalIgnoreCase);
        foreach (var lockedNet in this.thisLockedKiCadNetNames)
        {
            selectedNets.Add(lockedNet);
        }

        if (selectedNets.Count == 0)
        {
            return false;
        }

        var view = this.ResolveKiCadViewForSchematicName(schematicName);
        if (view == null)
        {
            return false;
        }

        if (string.Equals(view.Type, "pcb_top", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(view.Type, "pcb_bottom", StringComparison.OrdinalIgnoreCase))
        {
            var root = this.thisKiCadProject.Root;
            if (view.SourceIndex < 0 || view.SourceIndex >= root.Pcb.Count)
            {
                return false;
            }

            var pcb = root.Pcb[view.SourceIndex];

            foreach (var net in pcb.Nets.List)
            {
                string normalizedName = net.NormalizedName?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(normalizedName) ||
                    !selectedNets.Contains(normalizedName) ||
                    !net.Id.HasValue)
                {
                    continue;
                }

                if (!pcb.HighlightIndex.TryGetValue(net.Id.Value.ToString(CultureInfo.InvariantCulture), out var bucket))
                {
                    continue;
                }

                if (bucket.Pads.Count > 0 ||
                    bucket.Segments.Count > 0 ||
                    bucket.Vias.Count > 0 ||
                    bucket.Arcs.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        if (string.Equals(view.Type, "schematic", StringComparison.OrdinalIgnoreCase))
        {
            if (!this.thisKiCadProject.SchematicNetPathIndexBySchematicIndex.TryGetValue(view.SourceIndex, out var indexByNet))
            {
                return false;
            }

            foreach (string selectedNet in selectedNets)
            {
                if (!indexByNet.TryGetValue(selectedNet, out var resolvedPaths))
                {
                    continue;
                }

                if (resolvedPaths.Any(path => path.Points.Count >= 2))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // ###########################################################################################
    // Resolves any schematic thumbnail name to the corresponding KiCad project view.
    // Top and bottom replica pages are matched explicitly, while schematic pages are matched by
    // exact display name first and by page ordinal second.
    // ###########################################################################################
    private KiCadProjectView? ResolveKiCadViewForSchematicName(string schematicName)
    {
        var project = this.thisKiCadProject?.Root.Project;
        if (project == null || project.Views.Count == 0 || string.IsNullOrWhiteSpace(schematicName))
        {
            return null;
        }

        string targetName = schematicName;
        if (this.schematicByName.TryGetValue(schematicName, out var entry) && !string.IsNullOrWhiteSpace(entry.CadName))
        {
            targetName = entry.CadName.Trim();
        }

        var exact = project.Views.FirstOrDefault(view =>
            string.Equals(view.DisplayName, targetName, StringComparison.OrdinalIgnoreCase));

        if (exact != null)
        {
            return exact;
        }

        if (string.Equals(targetName, "Top (replica)", StringComparison.OrdinalIgnoreCase))
        {
            return project.Views.FirstOrDefault(view =>
                string.Equals(view.Type, "pcb_top", StringComparison.OrdinalIgnoreCase));
        }

        if (string.Equals(targetName, "Bottom (replica)", StringComparison.OrdinalIgnoreCase))
        {
            return project.Views.FirstOrDefault(view =>
                string.Equals(view.Type, "pcb_bottom", StringComparison.OrdinalIgnoreCase));
        }

        if (TryExtractSchematicPageOrdinal(targetName, out int pageOrdinal))
        {
            var schematicViews = project.Views
                .Where(view => string.Equals(view.Type, "schematic", StringComparison.OrdinalIgnoreCase))
                .OrderBy(view => view.SourceIndex)
                .ToList();

            int targetIndex = pageOrdinal - 1;
            if (targetIndex >= 0 && targetIndex < schematicViews.Count)
            {
                return schematicViews[targetIndex];
            }
        }

        return null;
    }

    // ###########################################################################################
    // Clears hover label and resets schematic cursor.
    // ###########################################################################################
    public void HideSchematicsHoverUi()
    {
        this.SetHoveredComponentBoardLabel(null);
        this.SetHoveredKiCadNet(null);
        this.thisHoveredKiCadPadNumber = null;
        this.SchematicsHoverLabelBorder.IsVisible = false;
        this.SchematicsHoverLabelText.Text = string.Empty;
        this.SchematicsHoverPadBorder.IsVisible = false;
        this.SchematicsHoverPadText.Text = string.Empty;
        this.SchematicsContainer.Cursor = Cursor.Default;

        if (this.MainWindow != null)
            this.MainWindow.isHoveringComponent = false;
    }

    // ###########################################################################################
    // Clears hover UI when pointer exits schematic area.
    // Uses a batched overlay update so exiting the editor does not trigger extra redraw churn.
    // ###########################################################################################
    private void OnSchematicsPointerExited(object? sender, PointerEventArgs e)
    {
        if (this.isPanning)
        {
            return;
        }

        if (this.thisIsLabelEditorMode && this.SchematicsLabelEditorOverlay.HoveredIndex != -1)
        {
            this.SetLabelEditorOverlayTransientState(hoveredIndex: -1);
        }

        this.HideSchematicsHoverUi();
    }

    // ###########################################################################################
    // Updates hover label/cursor from current pointer position.
    // ###########################################################################################
    private void UpdateSchematicsHoverUi(Point pointerInContainer)
    {
        if (this.thisIsKiCadCalibrationCaptureMode)
        {
            this.SetHoveredComponentBoardLabel(null);
            this.SchematicsContainer.Cursor = new Cursor(StandardCursorType.Cross);
            this.SchematicsHoverLabelText.Text =
                "Calibration capture - Left-click point, Right-click pan, Esc cancel";
            this.SchematicsHoverLabelBorder.IsVisible = true;
            this.SchematicsHoverPadBorder.IsVisible = false;

            if (this.MainWindow != null)
            {
                this.MainWindow.isHoveringComponent = false;
            }

            return;
        }

        if (this.IsPointerInsideKiCadNetConnectionsPanel(pointerInContainer))
        {
            this.ClearTransientHoverForKiCadNetConnectionsPanel();
            return;
        }

        if (this.thisIsLabelEditorMode)
        {
            this.SetHoveredComponentBoardLabel(null);

            if (this.MainWindow != null)
            {
                this.MainWindow.isHoveringComponent = false;
            }

            this.SchematicsHoverLabelBorder.IsVisible = false;
            this.SchematicsHoverLabelText.Text = string.Empty;
            this.SchematicsHoverPadBorder.IsVisible = false;
            this.SchematicsHoverPadText.Text = string.Empty;
            this.UpdateLabelEditorCursor(pointerInContainer);
            return;
        }

        bool hoveringComponent = this.TryGetHoveredBoardLabel(pointerInContainer, out var hoveredBoardLabel, out var displayText);

        if (hoveringComponent)
        {
            this.SetHoveredComponentBoardLabel(hoveredBoardLabel);
            this.SchematicsHoverLabelText.Text = displayText;
            this.SchematicsHoverLabelBorder.IsVisible = true;
            if (this.MainWindow != null) this.MainWindow.isHoveringComponent = true;
        }
        else
        {
            this.SetHoveredComponentBoardLabel(null);
            if (this.MainWindow != null) this.MainWindow.isHoveringComponent = false;
            this.SchematicsHoverLabelBorder.IsVisible = false;
            this.SchematicsHoverLabelText.Text = string.Empty;
        }

        this.RefreshKiCadHoverPadUi();

        if (hoveringComponent || this.SchematicsHoverPadBorder.IsVisible)
        {
            this.SchematicsContainer.Cursor = new Cursor(StandardCursorType.Hand);
        }
        else
        {
            this.SchematicsContainer.Cursor = Cursor.Default;
        }
    }

    // ###########################################################################################
    // Resolves hovered board label and the exact text shown in component selector.
    // Uses all components for the current board/region so schematic hit-testing remains available
    // even when the left-side category or search filters hide that component from the list.
    // ###########################################################################################
    private bool TryGetHoveredBoardLabel(Point pointerInContainer, out string boardLabel, out string displayText)
    {
        boardLabel = string.Empty;
        displayText = string.Empty;

        if (this.currentFullResBitmap == null || this.MainWindow == null || this.MainWindow.CurrentBoardData == null)
        {
            return false;
        }

        var selectedThumb = this.SchematicsThumbnailList.SelectedItem as SchematicThumbnail;
        if (selectedThumb == null)
        {
            return false;
        }

        if (!this.highlightRectsBySchematicAndLabel.TryGetValue(selectedThumb.Name, out var byLabel))
        {
            return false;
        }

        if (!TryInvert(this.schematicsMatrix, out var inv))
        {
            return false;
        }

        var localPoint = new Point(
            (pointerInContainer.X * inv.M11) + (pointerInContainer.Y * inv.M21) + inv.M31,
            (pointerInContainer.X * inv.M12) + (pointerInContainer.Y * inv.M22) + inv.M32);

        var contentRect = this.GetImageContentRect();
        if (contentRect.Width <= 0 || contentRect.Height <= 0 || !contentRect.Contains(localPoint))
        {
            return false;
        }

        double px = ((localPoint.X - contentRect.X) / contentRect.Width) * this.currentFullResBitmap.PixelSize.Width;
        double py = ((localPoint.Y - contentRect.Y) / contentRect.Height) * this.currentFullResBitmap.PixelSize.Height;
        var pixelPoint = new Point(px, py);

        string activeRegion = this.MainWindow.LocalRegion?.Trim() ?? string.Empty;
        var seenLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var component in this.MainWindow.CurrentBoardData.Components)
        {
            string componentBoardLabel = component.BoardLabel?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(componentBoardLabel))
            {
                continue;
            }

            string componentRegion = component.Region?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(componentRegion) &&
                !string.Equals(componentRegion, activeRegion, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!seenLabels.Add(componentBoardLabel))
            {
                continue;
            }

            if (!byLabel.TryGetValue(componentBoardLabel, out var rects))
            {
                continue;
            }

            if (!rects.Any(r => r.Contains(pixelPoint)))
            {
                continue;
            }

            var parts = new List<string>(3);

            if (!string.IsNullOrWhiteSpace(componentBoardLabel))
            {
                parts.Add(componentBoardLabel);
            }

            if (!string.IsNullOrWhiteSpace(component.FriendlyName))
            {
                parts.Add(component.FriendlyName.Trim());
            }

            if (!string.IsNullOrWhiteSpace(component.TechnicalNameOrValue))
            {
                parts.Add(component.TechnicalNameOrValue.Trim());
            }

            boardLabel = componentBoardLabel;
            displayText = parts.Count > 0 ? string.Join(" | ", parts) : componentBoardLabel;
            return true;
        }

        return false;
    }

    // ###########################################################################################
    // Selects first component row matching board label and scrolls it into view.
    // Falls back to a schematic-only selection when the component is hidden by current filters.
    // ###########################################################################################
    private void SelectComponentByBoardLabel(string boardLabel)
    {
        if (this.MainWindow == null)
        {
            return;
        }

        var items = this.MainWindow.ComponentFilterListBox.ItemsSource?.Cast<Main.ComponentListItem>().ToList() ?? new List<Main.ComponentListItem>();
        int index = items.FindIndex(item => string.Equals(item.BoardLabel, boardLabel, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            this.thisSchematicsOnlySelectedBoardLabels.Remove(boardLabel);
            this.MainWindow.ComponentFilterListBox.Selection.Select(index);
            this.MainWindow.ComponentFilterListBox.ScrollIntoView(items[index]);
            return;
        }

        if (this.thisSchematicsOnlySelectedBoardLabels.Add(boardLabel))
        {
            this.RefreshHighlightsFromCurrentComponentSelection();
        }
    }

    // ###########################################################################################
    // Deselects all component rows that match the given board label.
    // Also removes any schematic-only hidden selection for the same board label.
    // ###########################################################################################
    private void DeselectComponentByBoardLabel(string boardLabel)
    {
        bool removedHiddenSelection = this.thisSchematicsOnlySelectedBoardLabels.Remove(boardLabel);

        if (this.MainWindow == null)
        {
            if (removedHiddenSelection)
            {
                this.RefreshHighlightsFromCurrentComponentSelection();
            }

            return;
        }

        var items = this.MainWindow.ComponentFilterListBox.ItemsSource?.Cast<Main.ComponentListItem>().ToList() ?? new List<Main.ComponentListItem>();
        bool removedVisibleSelection = false;

        for (int i = 0; i < items.Count; i++)
        {
            if (string.Equals(items[i].BoardLabel, boardLabel, StringComparison.OrdinalIgnoreCase))
            {
                this.MainWindow.ComponentFilterListBox.Selection.Deselect(i);
                removedVisibleSelection = true;
            }
        }

        if (removedHiddenSelection && !removedVisibleSelection)
        {
            this.RefreshHighlightsFromCurrentComponentSelection();
        }
    }

    // ###########################################################################################
    // Toggles selection for a component board label.
    // ###########################################################################################
    private void ToggleComponentSelectionByBoardLabel(string boardLabel)
    {
        if (this.IsComponentBoardLabelSelected(boardLabel))
        {
            this.DeselectComponentByBoardLabel(boardLabel);
        }
        else
        {
            this.SelectComponentByBoardLabel(boardLabel);
        }
    }

    // ###########################################################################################
    // Tries to invert a 2D affine matrix.
    // ###########################################################################################
    private static bool TryInvert(Matrix m, out Matrix inv)
    {
        double a = m.M11, b = m.M12, c = m.M21, d = m.M22, e = m.M31, f = m.M32;
        double det = (a * d) - (b * c);

        if (Math.Abs(det) < 1e-12)
        {
            inv = Matrix.Identity;
            return false;
        }

        double idet = 1.0 / det;
        double na = d * idet, nb = -b * idet, nc = -c * idet, nd = a * idet;
        double ne = -((e * na) + (f * nc)), nf = -((e * nb) + (f * nd));

        inv = new Matrix(na, nb, nc, nd, ne, nf);
        return true;
    }

    // ###########################################################################################
    // Builds per-schematic highlight rect lookups.
    // ###########################################################################################
    public static Dictionary<string, Dictionary<string, List<Rect>>> BuildHighlightRects(BoardData boardData, string region)
    {
        var componentRegionsByLabel = boardData.Components
            .Where(c => !string.IsNullOrWhiteSpace(c.BoardLabel))
            .GroupBy(c => c.BoardLabel, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(c => c.Region?.Trim() ?? string.Empty)
                      .Where(r => !string.IsNullOrWhiteSpace(r))
                      .Distinct(StringComparer.OrdinalIgnoreCase)
                      .ToList(),
                StringComparer.OrdinalIgnoreCase);

        bool IsVisibleByRegion(string boardLabel)
        {
            if (!componentRegionsByLabel.TryGetValue(boardLabel, out var regionsForLabel)) return true;
            if (regionsForLabel.Count == 0) return true;
            return regionsForLabel.Any(r => string.Equals(r, region, StringComparison.OrdinalIgnoreCase));
        }

        var result = new Dictionary<string, Dictionary<string, List<Rect>>>(StringComparer.OrdinalIgnoreCase);

        foreach (var h in boardData.ComponentHighlights)
        {
            if (string.IsNullOrWhiteSpace(h.SchematicName) || string.IsNullOrWhiteSpace(h.BoardLabel)) continue;
            if (!IsVisibleByRegion(h.BoardLabel)) continue;

            if (!TryParseDouble(h.X, out var x) || !TryParseDouble(h.Y, out var y) ||
                !TryParseDouble(h.Width, out var w) || !TryParseDouble(h.Height, out var hh))
                continue;

            if (w <= 0 || hh <= 0) continue;

            if (!result.TryGetValue(h.SchematicName, out var byLabel))
            {
                byLabel = new Dictionary<string, List<Rect>>(StringComparer.OrdinalIgnoreCase);
                result[h.SchematicName] = byLabel;
            }

            if (!byLabel.TryGetValue(h.BoardLabel, out var rects))
            {
                rects = new List<Rect>();
                byLabel[h.BoardLabel] = rects;
            }

            rects.Add(new Rect(x, y, w, hh));
        }

        return result;
    }

    public static bool TryParseDouble(string text, out double value)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    public static Color ParseColorOrDefault(string text, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(text)) return fallback;
        try { return Color.Parse(text.Trim()); }
        catch { return fallback; }
    }

    public static double ParseOpacityOrDefault(string text, double fallback)
    {
        if (!TryParseDouble(text, out var v)) return fallback;
        if (v > 1.0) v /= 100.0;
        return Math.Clamp(v, 0.0, 1.0);
    }

    // ###########################################################################################
    // Safely resolves visual brushes from global Theme dictionaries, regardless of UI attach state.
    // ###########################################################################################
    private IBrush ResolveThemeBrush(string key, IBrush fallback)
    {
        if (this.TryFindResource(key, out var localRes) && localRes is IBrush localBrush)
            return localBrush;

        if (Application.Current != null)
        {
            var theme = Application.Current.ActualThemeVariant;
            if (Application.Current.TryGetResource(key, theme, out var appRes) && appRes is IBrush appBrush)
                return appBrush;
        }

        return fallback;
    }

    // ###########################################################################################
    // Loads thumbnails, applies saved user order, and removes stale saved entries automatically.
    // ###########################################################################################
    public void LoadSortedThumbnails(string boardKey, List<SchematicThumbnail> rawList)
    {
        this.RestoreBoardSettings(boardKey);

        var savedOrder = UserSettings.GetSchematicsOrder(boardKey);
        List<SchematicThumbnail> orderedList;

        if (savedOrder != null && savedOrder.Count > 0)
        {
            var orderLookup = savedOrder
                .Select((name, index) => new { name, index })
                .ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);

            orderedList = rawList
                .OrderBy(x => orderLookup.TryGetValue(x.Name, out int orderIndex) ? orderIndex : int.MaxValue)
                .ToList();

            var currentNames = orderedList.Select(x => x.Name).ToList();
            if (!currentNames.SequenceEqual(savedOrder, StringComparer.OrdinalIgnoreCase))
            {
                UserSettings.SetSchematicsOrder(boardKey, currentNames);
            }
        }
        else
        {
            orderedList = rawList;
        }

        this.currentThumbnails.Clear();
        foreach (var thumbnail in orderedList)
        {
            this.currentThumbnails.Add(thumbnail);
        }

        this.SchematicsThumbnailList.ItemsSource = this.currentThumbnails;
    }

    // ###########################################################################################
    // Starts tracking a thumbnail for possible drag reorder.
    // Suppresses immediate ListBox selection so dragging does not replace the large schematic.
    // ###########################################################################################
    private void OnThumbnailPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (sender is not Control control || control.DataContext is not SchematicThumbnail thumbnail || thumbnail.IsDropPlaceholder)
            return;

        this.thisThumbnailDragStartEventArgs = e;
        this.thisThumbnailDragStartPoint = e.GetPosition(this);
//        this.thisThumbnailDragStartPointInList = e.GetPosition(this.SchematicsThumbnailList);
        this.thisThumbnailDragPointerOffsetInItem = e.GetPosition(control);
        this.thisIsDraggingThumbnail = true;
        this.thisDraggedThumbnail = thumbnail;
        this.thisDraggedThumbnailWasSelected = ReferenceEquals(this.SchematicsThumbnailList.SelectedItem, thumbnail);
        this.thisDraggedThumbnailHeight = Math.Max(control.Bounds.Height, 80.0);
        this.thisDraggedThumbnailWidth = Math.Max(control.Bounds.Width, 120.0);
        this.thisSuppressThumbnailSelectionChanged = true;

        e.Handled = true;
    }

    // ###########################################################################################
    // Begins drag/drop reordering once the pointer has moved far enough.
    // ###########################################################################################
    private async void OnThumbnailPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!this.thisIsDraggingThumbnail || this.thisDraggedThumbnail == null)
            return;

        var point = e.GetPosition(this);
        var diff = this.thisThumbnailDragStartPoint - point;

        // Slightly higher threshold to avoid accidental detaching on tiny pointer jitter.
        if (Math.Abs(diff.X) <= 6 && Math.Abs(diff.Y) <= 6)
            return;

        if (sender is not Control control)
            return;

        this.thisIsDraggingThumbnail = false;

        this.thisDraggedThumbnailOriginalIndex = this.currentThumbnails.IndexOf(this.thisDraggedThumbnail);
        if (this.thisDraggedThumbnailOriginalIndex < 0)
            return;

        this.thisDraggedThumbnailHeight = Math.Max(control.Bounds.Height, 80.0);
        this.thisDraggedThumbnailWidth = Math.Max(control.Bounds.Width, 120.0);

        var pointerInList = e.GetPosition(this.SchematicsThumbnailList);
        this.thisThumbnailLastPointerYInList = pointerInList.Y;

        var transformToList = control.TransformToVisual(this.SchematicsThumbnailList);
        if (transformToList.HasValue)
        {
            var boundsInList = new Rect(control.Bounds.Size).TransformToAABB(transformToList.Value);
            this.thisThumbnailDragGhostFixedX = Math.Max(0, boundsInList.X);
        }
        else
        {
            this.thisThumbnailDragGhostFixedX = Math.Max(0, pointerInList.X - this.thisThumbnailDragPointerOffsetInItem.X);
        }

        this.currentThumbnails.RemoveAt(this.thisDraggedThumbnailOriginalIndex);
//        this.thisThumbnailCurrentInsertIndex = this.thisDraggedThumbnailOriginalIndex;
        this.ShowThumbnailDropPlaceholder(this.thisDraggedThumbnailOriginalIndex);
        this.ShowThumbnailDragGhost(this.thisDraggedThumbnail, pointerInList);

        if (this.thisThumbnailDragStartEventArgs == null)
        {
            this.RestoreDraggedThumbnail();
            this.HideThumbnailDragGhost();
            this.ClearThumbnailDropPlaceholder();
            this.thisDraggedThumbnail = null;
            this.thisDraggedThumbnailOriginalIndex = -1;
            this.thisDraggedThumbnailWasSelected = false;
//            this.thisThumbnailCurrentInsertIndex = -1;
            this.thisThumbnailLastPointerYInList = double.NaN;
            this.thisThumbnailDragStartEventArgs = null;
            return;
        }

        var dragData = new DataTransfer();

        var effect = await DragDrop.DoDragDropAsync(
            this.thisThumbnailDragStartEventArgs,
            dragData,
            DragDropEffects.Move);

        if (effect != DragDropEffects.Move && this.thisDraggedThumbnail != null)
        {
            this.RestoreDraggedThumbnail();
        }

        this.HideThumbnailDragGhost();
        this.ClearThumbnailDropPlaceholder();
        this.thisDraggedThumbnail = null;
        this.thisDraggedThumbnailOriginalIndex = -1;
        this.thisDraggedThumbnailWasSelected = false;
//        this.thisThumbnailCurrentInsertIndex = -1;
        this.thisThumbnailLastPointerYInList = double.NaN;
        this.thisThumbnailDragStartEventArgs = null;

        e.Handled = true;
    }

    // ###########################################################################################
    // Updates the floating drag ghost to follow the mouse while preserving the original grab point.
    // Horizontal movement is locked so the detached thumbnail only moves vertically.
    // ###########################################################################################
    private void UpdateThumbnailDragGhostPosition(Point pointerInList)
    {
        if (!this.ThumbnailDragGhost.IsVisible || this.ThumbnailDragGhost.RenderTransform is not TranslateTransform transform)
            return;

        transform.X = this.thisThumbnailDragGhostFixedX;
        transform.Y = Math.Max(0, pointerInList.Y - this.thisThumbnailDragPointerOffsetInItem.Y);
    }

    // ###########################################################################################
    // Shows a detached visual thumbnail that follows the mouse during drag.
    // ###########################################################################################
    private void ShowThumbnailDragGhost(SchematicThumbnail thumbnail, Point pointerInList)
    {
        this.ThumbnailDragGhost.Width = this.thisDraggedThumbnailWidth;
        this.ThumbnailDragGhostName.Text = thumbnail.Name;
        this.ThumbnailDragGhostImage.Source = thumbnail.ImageSource ?? thumbnail.BaseThumbnail;
        this.ThumbnailDragGhost.IsVisible = true;
        this.UpdateThumbnailDragGhostPosition(pointerInList);
    }

    // ###########################################################################################
    // Hides the floating drag ghost after drop or cancel.
    // ###########################################################################################
    private void HideThumbnailDragGhost()
    {
        this.ThumbnailDragGhost.IsVisible = false;
        this.ThumbnailDragGhostName.Text = string.Empty;
        this.ThumbnailDragGhostImage.Source = null;
        this.thisThumbnailDragGhostFixedX = 0;

        if (this.ThumbnailDragGhost.RenderTransform is TranslateTransform transform)
        {
            transform.X = 0;
            transform.Y = 0;
        }
    }

    // ###########################################################################################
    // Stops local drag tracking when the pointer is released.
    // If no drag started, treat the interaction as a normal selection click.
    // ###########################################################################################
    private void OnThumbnailPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (this.thisIsDraggingThumbnail &&
            sender is Control control &&
            control.DataContext is SchematicThumbnail thumbnail &&
            !thumbnail.IsDropPlaceholder)
        {
            this.thisSuppressThumbnailSelectionChanged = false;
            this.SchematicsThumbnailList.SelectedItem = thumbnail;
            e.Handled = true;
        }

        this.thisIsDraggingThumbnail = false;
        this.thisThumbnailDragStartEventArgs = null;
    }

    // ###########################################################################################
    // Creates or moves the temporary placeholder item to the requested insert index.
    // The provided insert index is already in current collection coordinates, so no extra
    // reindex adjustment is applied after removing the existing placeholder.
    // ###########################################################################################
    private void ShowThumbnailDropPlaceholder(int insertIndex)
    {
        if (this.thisThumbnailDropPlaceholder == null)
        {
            this.thisThumbnailDropPlaceholder = new SchematicThumbnail
            {
                IsDropPlaceholder = true
            };
        }

        this.thisThumbnailDropPlaceholder.PlaceholderHeight = this.thisDraggedThumbnailHeight;
        this.thisThumbnailDropPlaceholder.PlaceholderWidth = this.thisDraggedThumbnailWidth;

        int existingIndex = this.currentThumbnails.IndexOf(this.thisThumbnailDropPlaceholder);

        if (existingIndex == insertIndex)
        {
//            this.thisThumbnailCurrentInsertIndex = insertIndex;
            return;
        }

        if (existingIndex >= 0)
        {
            this.currentThumbnails.RemoveAt(existingIndex);
        }

        insertIndex = Math.Clamp(insertIndex, 0, this.currentThumbnails.Count);
        this.currentThumbnails.Insert(insertIndex, this.thisThumbnailDropPlaceholder);
//        this.thisThumbnailCurrentInsertIndex = insertIndex;
    }

    // ###########################################################################################
    // Removes the temporary placeholder item from the thumbnails list.
    // ###########################################################################################
    private void ClearThumbnailDropPlaceholder()
    {
        if (this.thisThumbnailDropPlaceholder == null)
            return;

        int index = this.currentThumbnails.IndexOf(this.thisThumbnailDropPlaceholder);
        if (index >= 0)
        {
            this.currentThumbnails.RemoveAt(index);
        }
    }

    // ###########################################################################################
    // Restores the dragged thumbnail to its original position when no drop occurs.
    // ###########################################################################################
    private void RestoreDraggedThumbnail()
    {
        if (this.thisDraggedThumbnail == null)
            return;

        this.ClearThumbnailDropPlaceholder();

        int restoreIndex = this.thisDraggedThumbnailOriginalIndex;
        if (restoreIndex < 0 || restoreIndex > this.currentThumbnails.Count)
        {
            restoreIndex = this.currentThumbnails.Count;
        }

        this.currentThumbnails.Insert(restoreIndex, this.thisDraggedThumbnail);
//        this.thisThumbnailCurrentInsertIndex = restoreIndex;
        this.thisThumbnailLastPointerYInList = double.NaN;

        if (this.thisDraggedThumbnailWasSelected)
        {
            this.SchematicsThumbnailList.SelectedItem = this.thisDraggedThumbnail;
        }

        this.thisSuppressThumbnailSelectionChanged = false;
        this.thisThumbnailDragStartEventArgs = null;
    }

    // ###########################################################################################
    // Saves the current thumbnail order for the active board, excluding the placeholder item.
    // ###########################################################################################
    private void SaveCurrentThumbnailOrder()
    {
        var boardKey = this.MainWindow?.GetCurrentBoardKey();
        if (string.IsNullOrWhiteSpace(boardKey))
            return;

        var orderedNames = this.currentThumbnails
            .Where(x => !x.IsDropPlaceholder && !string.IsNullOrWhiteSpace(x.Name))
            .Select(x => x.Name)
            .ToList();

        UserSettings.SetSchematicsOrder(boardKey, orderedNames);
    }

    // ###########################################################################################
    // Updates the placeholder position while dragging over the thumbnail list.
    // Uses actual ListBoxItem row bounds and only moves in the current drag direction.
    // ###########################################################################################
    private void OnThumbnailDragOver(object? sender, DragEventArgs e)
    {
        if (this.thisDraggedThumbnail == null)
        {
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.DragEffects = DragDropEffects.Move;

        var pointerInList = e.GetPosition(this.SchematicsThumbnailList);
        this.UpdateThumbnailDragGhostPosition(pointerInList);

        int placeholderIndex = this.thisThumbnailDropPlaceholder != null
            ? this.currentThumbnails.IndexOf(this.thisThumbnailDropPlaceholder)
            : -1;

        if (placeholderIndex < 0)
        {
            e.Handled = true;
            return;
        }

        if (double.IsNaN(this.thisThumbnailLastPointerYInList))
        {
            this.thisThumbnailLastPointerYInList = pointerInList.Y;
            e.Handled = true;
            return;
        }

        double deltaY = pointerInList.Y - this.thisThumbnailLastPointerYInList;
        if (Math.Abs(deltaY) < 0.1)
        {
            e.Handled = true;
            return;
        }

        bool isMovingUp = deltaY < 0;
        bool isMovingDown = deltaY > 0;

        double ghostTopY = pointerInList.Y - this.thisThumbnailDragPointerOffsetInItem.Y;
        double ghostBottomY = ghostTopY + this.thisDraggedThumbnailHeight;

        if (isMovingUp && placeholderIndex > 0)
        {
            var itemAbove = this.SchematicsThumbnailList.ContainerFromIndex(placeholderIndex - 1) as ListBoxItem;
            if (itemAbove != null)
            {
                var transform = itemAbove.TransformToVisual(this.SchematicsThumbnailList);
                if (transform.HasValue)
                {
                    var boundsAbove = new Rect(itemAbove.Bounds.Size).TransformToAABB(transform.Value);

                    if (ghostTopY <= boundsAbove.Bottom)
                    {
                        this.ShowThumbnailDropPlaceholder(placeholderIndex - 1);
                        this.thisThumbnailLastPointerYInList = pointerInList.Y;
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        if (isMovingDown && placeholderIndex < this.currentThumbnails.Count - 1)
        {
            var itemBelow = this.SchematicsThumbnailList.ContainerFromIndex(placeholderIndex + 1) as ListBoxItem;
            if (itemBelow != null)
            {
                var transform = itemBelow.TransformToVisual(this.SchematicsThumbnailList);
                if (transform.HasValue)
                {
                    var boundsBelow = new Rect(itemBelow.Bounds.Size).TransformToAABB(transform.Value);

                    if (ghostBottomY >= boundsBelow.Top)
                    {
                        this.ShowThumbnailDropPlaceholder(placeholderIndex + 1);
                        this.thisThumbnailLastPointerYInList = pointerInList.Y;
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        this.thisThumbnailLastPointerYInList = pointerInList.Y;
        e.Handled = true;
    }

    // ###########################################################################################
    // Keeps the placeholder visible even if the drag briefly leaves the list bounds.
    // ###########################################################################################
    private void OnThumbnailDragLeave(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
    }

    // ###########################################################################################
    // Finalizes thumbnail reordering by replacing the placeholder with the dragged item.
    // ###########################################################################################
    private void OnThumbnailDrop(object? sender, DragEventArgs e)
    {
        if (this.thisDraggedThumbnail == null)
        {
            e.Handled = true;
            return;
        }

        int insertIndex = this.thisThumbnailDropPlaceholder != null
            ? this.currentThumbnails.IndexOf(this.thisThumbnailDropPlaceholder)
            : this.currentThumbnails.Count;

        this.ClearThumbnailDropPlaceholder();

        if (insertIndex < 0 || insertIndex > this.currentThumbnails.Count)
        {
            insertIndex = this.currentThumbnails.Count;
        }

        this.currentThumbnails.Insert(insertIndex, this.thisDraggedThumbnail);
//        this.thisThumbnailCurrentInsertIndex = insertIndex;

        if (this.thisDraggedThumbnailWasSelected)
        {
            this.SchematicsThumbnailList.SelectedItem = this.thisDraggedThumbnail;
        }

        this.HideThumbnailDragGhost();
        this.SaveCurrentThumbnailOrder();

        this.thisDraggedThumbnail = null;
        this.thisDraggedThumbnailOriginalIndex = -1;
        this.thisDraggedThumbnailWasSelected = false;
//        this.thisThumbnailCurrentInsertIndex = -1;
        this.thisThumbnailLastPointerYInList = double.NaN;
        this.thisSuppressThumbnailSelectionChanged = false;
        this.thisThumbnailDragStartEventArgs = null;

        e.Handled = true;
    }

    // ###########################################################################################
    // Expands the control into image-only mode before it is rehosted in the fullscreen window.
    // ###########################################################################################
    public void EnterFullscreenMode()
    {
        if (this.thisIsFullscreenMode)
            return;

        this.thisIsFullscreenMode = true;

        this.thisRestoreLeftColumnWidth = this.SchematicsInnerGrid.ColumnDefinitions[0].Width;
        this.thisRestoreSplitterColumnWidth = this.SchematicsInnerGrid.ColumnDefinitions[1].Width;
        this.thisRestoreRightColumnWidth = this.SchematicsInnerGrid.ColumnDefinitions[2].Width;
        this.thisRestoreRightColumnMinWidth = this.SchematicsInnerGrid.ColumnDefinitions[2].MinWidth;

        this.thisIsDraggingThumbnail = false;
        this.thisThumbnailDragStartEventArgs = null;
        this.ClearThumbnailDropPlaceholder();
        this.HideThumbnailDragGhost();

        this.SchematicsInnerGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
        this.SchematicsInnerGrid.ColumnDefinitions[1].Width = new GridLength(0, GridUnitType.Pixel);
        this.SchematicsInnerGrid.ColumnDefinitions[2].Width = new GridLength(0, GridUnitType.Pixel);
        this.SchematicsInnerGrid.ColumnDefinitions[2].MinWidth = 0;

        this.SchematicsSplitter.IsVisible = false;
        this.SchematicsThumbnailList.IsVisible = false;

        this.RefreshAfterHostChanged();
    }

    // ###########################################################################################
    // Restores the normal schematics tab layout after leaving fullscreen mode.
    // ###########################################################################################
    public void ExitFullscreenMode()
    {
        if (!this.thisIsFullscreenMode)
            return;

        this.thisIsFullscreenMode = false;

        this.SchematicsInnerGrid.ColumnDefinitions[0].Width = this.thisRestoreLeftColumnWidth;
        this.SchematicsInnerGrid.ColumnDefinitions[1].Width = this.thisRestoreSplitterColumnWidth;
        this.SchematicsInnerGrid.ColumnDefinitions[2].Width = this.thisRestoreRightColumnWidth;
        this.SchematicsInnerGrid.ColumnDefinitions[2].MinWidth = this.thisRestoreRightColumnMinWidth;

        this.SchematicsSplitter.IsVisible = true;
        this.SchematicsThumbnailList.IsVisible = true;

        this.RefreshAfterHostChanged();
    }

    // ###########################################################################################
    // Re-clamps and redraws the viewer after the control is moved between windows.
    // Uses one full label refresh and then a follow-up clamp-only pass for final layout settling.
    // ###########################################################################################
    public void RefreshAfterHostChanged()
    {
        Dispatcher.UIThread.Post(() =>
        {
            this.ClampSchematicsMatrix();
            this.UpdateOverlayLabels();
            this.UpdateComponentLabels();
        }, DispatcherPriority.Background);

        Dispatcher.UIThread.Post(() =>
        {
            this.ClampSchematicsMatrix();
        }, DispatcherPriority.Background);
    }

    // ###########################################################################################
    // Returns true when the pointer is currently inside the floating label-editor menu bounds.
    // ###########################################################################################
    private bool IsPointerInsideLabelEditorMenu(Point containerPoint)
    {
        if (!this.SchematicsLabelEditorMenuBorder.IsVisible)
        {
            return false;
        }

        Point? translatedTopLeft = this.SchematicsLabelEditorMenuBorder.TranslatePoint(new Point(0, 0), this.SchematicsContainer);
        if (!translatedTopLeft.HasValue)
        {
            return false;
        }

        var menuRect = new Rect(translatedTopLeft.Value, this.SchematicsLabelEditorMenuBorder.Bounds.Size);
        return menuRect.Contains(containerPoint);
    }

    // ###########################################################################################
    // Returns true when the schematics actions menu is allowed to be shown.
    // Contributor mode enables menu entry from empty-space right click.
    // ###########################################################################################
    private bool CanShowSchematicsActionsMenu()
    {
        return this.IsBoardContributorModeEnabled() ||
               this.thisIsLabelEditorMode ||
               this.thisIsKiCadCalibrationCaptureMode;
    }

    // ###########################################################################################
    // Shows the floating schematic action menu at the requested schematic container location.
    // ###########################################################################################
    private void ShowLabelEditorMenu(Point containerPoint)
    {
        if (!this.CanShowSchematicsActionsMenu())
        {
            return;
        }

        this.thisLastLabelEditorMenuPoint = containerPoint;
        this.UpdateLabelEditorMenuButtons();

        double estimatedWidth = 240.0;
        double estimatedHeight = this.thisIsLabelEditorMode
            ? 110.0
            : this.thisIsKiCadCalibrationCaptureMode ? 90.0 : 155.0;

        double x = Math.Clamp(containerPoint.X, 6.0, Math.Max(6.0, this.SchematicsContainer.Bounds.Width - estimatedWidth));
        double y = Math.Clamp(containerPoint.Y, 6.0, Math.Max(6.0, this.SchematicsContainer.Bounds.Height - estimatedHeight));

        this.SchematicsLabelEditorMenuBorder.Margin = new Thickness(x, y, 0, 0);
        this.SchematicsLabelEditorMenuBorder.IsVisible = true;
        this.thisIsShowingLabelEditorMenu = true;
    }

    // ###########################################################################################
    // Hides the floating label-editor action menu.
    // ###########################################################################################
    private void HideLabelEditorMenu()
    {
        this.SchematicsLabelEditorMenuBorder.IsVisible = false;
        this.thisIsShowingLabelEditorMenu = false;
    }

    // ###########################################################################################
    // Updates the menu text and button visibility according to the current editor and calibration state.
    // ###########################################################################################
    private void UpdateLabelEditorMenuButtons()
    {
        this.SchematicsLabelEditorMenuStateTextBlock.Text = this.thisIsLabelEditorMode
            ? "Component label editor mode"
            : this.thisIsKiCadCalibrationCaptureMode
                ? "Image calibration capture"
                : "Schematic actions";

        this.EnableLabelEditorButton.IsVisible = !this.thisIsLabelEditorMode && !this.thisIsKiCadCalibrationCaptureMode;
        this.BeginKiCadCalibrationButton.IsVisible = !this.thisIsLabelEditorMode && !this.thisIsKiCadCalibrationCaptureMode;
        this.CopyKiCadWorldPointCandidatesButton.IsVisible = !this.thisIsLabelEditorMode && !this.thisIsKiCadCalibrationCaptureMode;
        this.CancelKiCadCalibrationButton.IsVisible = this.thisIsKiCadCalibrationCaptureMode;

        this.CancelLabelEditorChangesButton.IsVisible = this.thisIsLabelEditorMode;
        this.ApplyLabelEditorChangesButton.IsVisible = this.thisIsLabelEditorMode;
    }

    // ###########################################################################################
    // Returns the currently selected schematic name, or an empty string if none is selected.
    // ###########################################################################################
    private string GetCurrentSchematicName()
    {
        return (this.SchematicsThumbnailList.SelectedItem as SchematicThumbnail)?.Name?.Trim() ?? string.Empty;
    }

    // ###########################################################################################
    // Loads a fresh working-copy snapshot of highlight rectangles for the current schematic.
    // ###########################################################################################
    private void LoadLabelEditorWorkingCopyForCurrentSchematic()
    {
        this.thisLabelEditorWorkingHighlights.Clear();
        this.thisSelectedLabelEditorHighlights.Clear();
        this.thisSelectedLabelEditorHighlight = null;
        this.thisLabelEditorOriginalDragRectangles.Clear();
        this.thisLabelEditorSchematicName = this.GetCurrentSchematicName();

        var boardData = this.MainWindow?.CurrentBoardData;
        if (boardData == null || string.IsNullOrWhiteSpace(this.thisLabelEditorSchematicName))
        {
            return;
        }

        foreach (var row in boardData.ComponentHighlights.Where(h =>
                     string.Equals(h.SchematicName, this.thisLabelEditorSchematicName, StringComparison.OrdinalIgnoreCase)))
        {
            if (!TryParseDouble(row.X, out var x) ||
                !TryParseDouble(row.Y, out var y) ||
                !TryParseDouble(row.Width, out var width) ||
                !TryParseDouble(row.Height, out var height))
            {
                continue;
            }

            if (width <= 0 || height <= 0)
            {
                continue;
            }

            string boardLabel = row.BoardLabel?.Trim() ?? string.Empty;
            string category = boardData.Components
                .FirstOrDefault(component => string.Equals(component.BoardLabel, boardLabel, StringComparison.OrdinalIgnoreCase))
                ?.Category?.Trim() ?? string.Empty;

            this.thisLabelEditorWorkingHighlights.Add(new EditableComponentHighlight
            {
                SchematicName = row.SchematicName?.Trim() ?? string.Empty,
                BoardLabel = boardLabel,
                Category = category,
                X = x,
                Y = y,
                Width = width,
                Height = height
            });
        }
    }

    // ###########################################################################################
    // Enters label-editor mode and captures the current schematic highlights into a working copy.
    // Closes the action menu immediately after enabling the editor.
    // ###########################################################################################
    private void BeginLabelEditorMode()
    {
        this.thisIsLabelEditorMode = true;

        this.LoadLabelEditorWorkingCopyForCurrentSchematic();
        this.RefreshLabelEditorOverlay();
        this.HideLabelEditorMenu();
        this.SchematicsContainer.Focus();

        Logger.Info($"Label editor enabled for schematic [{this.thisLabelEditorSchematicName}] with [{this.thisLabelEditorWorkingHighlights.Count}] rectangles loaded");
    }

    // ###########################################################################################
    // Cancels the current label-editor session and discards all in-memory editor changes.
    // Clears undo and redo because the editor session is ending.
    // ###########################################################################################
    private void CancelLabelEditorChanges()
    {
        this.thisIsLabelEditorMode = false;
        this.thisLabelEditorSchematicName = string.Empty;
        this.thisSelectedLabelEditorHighlights.Clear();
        this.thisSelectedLabelEditorHighlight = null;
        this.thisPendingNewLabelEditorHighlight = null;
        this.thisIsDrawingLabelEditorRectangle = false;
        this.thisLabelEditorDraftRectangle = null;
        this.thisLabelEditorDragMode = LabelEditorDragMode.None;
        this.thisLabelEditorOriginalSelectionBounds = default;
        this.thisLabelEditorOriginalDragRectangles.Clear();
        this.thisLabelEditorWorkingHighlights.Clear();
        this.thisLabelEditorUndoStack.Clear();
        this.thisLabelEditorRedoStack.Clear();

        this.HideLabelEditorMenu();
        this.HideNewLabelEditorPrompt();
        this.RefreshLabelEditorOverlay();
        this.SchematicsContainer.Focus();

        Logger.Info("Label editor changes canceled");
    }

    // ###########################################################################################
    // Validates and saves the current editor session to the board Excel file, then reloads the
    // board from disk so the runtime state reflects the persisted workbook content.
    // Clears undo and redo because the editor session is ending.
    // ###########################################################################################
    private async void ApplyLabelEditorChanges()
    {
        if (this.SchematicsNewLabelPromptBorder.IsVisible)
        {
            this.ConfirmNewLabelEditorPrompt();

            if (this.SchematicsNewLabelPromptBorder.IsVisible)
            {
                Logger.Info("Label editor save aborted because the new-label prompt is still visible after confirmation attempt");
                return;
            }
        }

        if (!this.TryValidateLabelEditorSave(out var validationError))
        {
            Logger.Warning($"Label editor validation failed - [{validationError}]");
            await this.ShowLabelEditorValidationFailedDialogAsync(validationError);
            return;
        }

        string schematicName = this.GetCurrentSchematicName();
        string excelPath = this.MainWindow?.GetCurrentBoardExcelPath() ?? string.Empty;
        string cacheKey = this.MainWindow?.GetCurrentBoardEntry()?.ExcelDataFile?.Trim() ?? string.Empty;
        string region = this.MainWindow?.LocalRegion?.Trim() ?? string.Empty;

        Logger.Debug($"Label editor resolved Excel save path: [{excelPath}]");
        Logger.Debug($"Label editor resolved cache key: [{cacheKey}]");
        Logger.Debug($"Label editor resolved schematic name: [{schematicName}]");
        Logger.Debug($"Label editor resolved region: [{region}]");

        if (string.IsNullOrWhiteSpace(excelPath))
        {
            Logger.Warning("Label editor save failed - could not resolve current board Excel path");
            return;
        }

        var saveRows = this.BuildLabelEditorSaveRowsForCurrentSchematic();

        Logger.Debug($"Label editor built save rows count: [{saveRows.Count}]");

        var saveResult = await BoardDataWriter.SaveLabelEditorChangesAsync(
            excelPath,
            schematicName,
            saveRows,
            region);

        if (!saveResult.Success)
        {
            Logger.Warning($"Label editor save failed - [{saveResult.ErrorMessage}]");
            await this.ShowLabelEditorSaveFailedDialogAsync(saveResult.ErrorMessage);
            return;
        }

        Logger.Info($"Label editor save succeeded for Excel path: [{excelPath}]");

        BoardDataReader.ClearCache(cacheKey);

        this.thisIsLabelEditorMode = false;
        this.thisSelectedLabelEditorHighlights.Clear();
        this.thisSelectedLabelEditorHighlight = null;
        this.thisPendingNewLabelEditorHighlight = null;
        this.thisIsDrawingLabelEditorRectangle = false;
        this.thisLabelEditorDraftRectangle = null;
        this.thisLabelEditorDragMode = LabelEditorDragMode.None;
        this.thisLabelEditorOriginalSelectionBounds = default;
        this.thisLabelEditorOriginalDragRectangles.Clear();
        this.thisLabelEditorWorkingHighlights.Clear();
        this.thisLabelEditorUndoStack.Clear();
        this.thisLabelEditorRedoStack.Clear();

        this.HideLabelEditorMenu();
        this.HideNewLabelEditorPrompt();
        this.RefreshLabelEditorOverlay();
        this.SchematicsContainer.Focus();

        this.MainWindow?.ReloadCurrentBoardFromDisk(schematicName);

        Logger.Info($"Label editor changes saved and reload requested for schematic [{schematicName}]");
    }

    // ###########################################################################################
    // Refreshes the label-editor overlay from the current working-copy rectangles and selected item.
    // Uses the same main highlight color and opacity as the normal schematic highlight.
    // Applies overlay state in one batch to avoid repeated redraws during drag operations.
    // ###########################################################################################
    private void RefreshLabelEditorOverlay(IReadOnlyList<(Point Start, Point End)>? snapGuides = null)
    {
        if (!this.thisIsLabelEditorMode || this.currentFullResBitmap == null)
        {
            this.SchematicsLabelEditorOverlay.ApplyState(
                rectangles: Array.Empty<Rect>(),
                selectedIndex: -1,
                selectedIndices: Array.Empty<int>(),
                selectionBounds: null,
                hoveredIndex: -1,
                draftRectangle: null,
                snapGuides: Array.Empty<(Point Start, Point End)>(),
                bitmapPixelSize: this.currentFullResBitmap?.PixelSize ?? new PixelSize(0, 0),
                viewMatrix: this.schematicsMatrix,
                highlightColor: Colors.IndianRed,
                highlightOpacity: 0.20,
                isVisible: false);

            this.UpdateComponentLabels();
            return;
        }

        string schematicName = this.GetCurrentSchematicName();

        var itemsForCurrentSchematic = this.thisLabelEditorWorkingHighlights
            .Where(row => string.Equals(row.SchematicName, schematicName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var rects = itemsForCurrentSchematic
            .Select(row => new Rect(row.X, row.Y, row.Width, row.Height))
            .ToList();

        int selectedIndex = -1;
        if (this.thisSelectedLabelEditorHighlight != null)
        {
            selectedIndex = itemsForCurrentSchematic.IndexOf(this.thisSelectedLabelEditorHighlight);
        }

        var selectedIndices = itemsForCurrentSchematic
            .Select((row, index) => new { row, index })
            .Where(x => this.thisSelectedLabelEditorHighlights.Contains(x.row))
            .Select(x => x.index)
            .ToList();

        Color highlightColor = Colors.IndianRed;
        double highlightOpacity = 0.20;

        if (!string.IsNullOrWhiteSpace(schematicName) &&
            this.schematicByName.TryGetValue(schematicName, out var schematic))
        {
            highlightColor = ParseColorOrDefault(schematic.MainImageHighlightColor, Colors.IndianRed);
            highlightOpacity = ParseOpacityOrDefault(schematic.MainHighlightOpacity, 0.20);
        }

        this.SchematicsLabelEditorOverlay.ApplyState(
            rectangles: rects,
            selectedIndex: selectedIndex,
            selectedIndices: selectedIndices,
            selectionBounds: null,
            hoveredIndex: -1,
            draftRectangle: this.thisLabelEditorDraftRectangle,
            snapGuides: snapGuides ?? Array.Empty<(Point Start, Point End)>(),
            bitmapPixelSize: this.currentFullResBitmap.PixelSize,
            viewMatrix: this.schematicsMatrix,
            highlightColor: highlightColor,
            highlightOpacity: highlightOpacity,
            isVisible: true);

        this.UpdateComponentLabels();
    }

    // ###########################################################################################
    // Snaps active resize edges to nearby neighbor edges within 2 px.
    // Snap candidates are rejected when another component blocks the path to that neighbor.
    // Only components currently visible in the viewport can participate in snap alignment.
    // When multiple visible neighbors align to the same snapped edge, guides are shown to all.
    // ###########################################################################################
    private void ApplyLabelEditorResizeSnap(
        EditableComponentHighlight currentHighlight,
        ref double left,
        ref double top,
        ref double right,
        ref double bottom,
        List<(Point Start, Point End)> snapGuides,
        bool suppressSnap)
    {
        const double snapThreshold = 2.0;
        const double epsilon = 0.001;

        if (suppressSnap ||
            this.thisLabelEditorDragMode == LabelEditorDragMode.None ||
            this.thisLabelEditorDragMode == LabelEditorDragMode.Move)
        {
            return;
        }

        string schematicName = this.GetCurrentSchematicName();

        bool resizesTop =
            this.thisLabelEditorDragMode == LabelEditorDragMode.ResizeTop ||
            this.thisLabelEditorDragMode == LabelEditorDragMode.ResizeTopLeft ||
            this.thisLabelEditorDragMode == LabelEditorDragMode.ResizeTopRight;

        bool resizesBottom =
            this.thisLabelEditorDragMode == LabelEditorDragMode.ResizeBottom ||
            this.thisLabelEditorDragMode == LabelEditorDragMode.ResizeBottomLeft ||
            this.thisLabelEditorDragMode == LabelEditorDragMode.ResizeBottomRight;

        bool resizesLeft =
            this.thisLabelEditorDragMode == LabelEditorDragMode.ResizeLeft ||
            this.thisLabelEditorDragMode == LabelEditorDragMode.ResizeTopLeft ||
            this.thisLabelEditorDragMode == LabelEditorDragMode.ResizeBottomLeft;

        bool resizesRight =
            this.thisLabelEditorDragMode == LabelEditorDragMode.ResizeRight ||
            this.thisLabelEditorDragMode == LabelEditorDragMode.ResizeTopRight ||
            this.thisLabelEditorDragMode == LabelEditorDragMode.ResizeBottomRight;

        static bool RangesOverlap(double a1, double a2, double b1, double b2)
        {
            return Math.Min(a2, b2) > Math.Max(a1, b1);
        }

        static Rect BuildCurrentRect(double leftValue, double topValue, double rightValue, double bottomValue)
        {
            return new Rect(
                leftValue,
                topValue,
                Math.Max(1.0, rightValue - leftValue),
                Math.Max(1.0, bottomValue - topValue));
        }

        Rect? visiblePixelRect = null;

        if (this.currentFullResBitmap != null &&
            this.SchematicsContainer.Bounds.Width > 0 &&
            this.SchematicsContainer.Bounds.Height > 0 &&
            TryInvert(this.schematicsMatrix, out var inverseMatrix))
        {
            var contentRect = this.GetLabelEditorImageContentRect();

            if (contentRect.Width > 0 && contentRect.Height > 0)
            {
                var containerRect = new Rect(this.SchematicsContainer.Bounds.Size);
                var visibleLocalRect = containerRect.TransformToAABB(inverseMatrix);

                double clippedLeft = Math.Max(contentRect.Left, visibleLocalRect.Left);
                double clippedTop = Math.Max(contentRect.Top, visibleLocalRect.Top);
                double clippedRight = Math.Min(contentRect.Right, visibleLocalRect.Right);
                double clippedBottom = Math.Min(contentRect.Bottom, visibleLocalRect.Bottom);

                if (clippedRight > clippedLeft && clippedBottom > clippedTop)
                {
                    double bitmapWidth = this.currentFullResBitmap.PixelSize.Width;
                    double bitmapHeight = this.currentFullResBitmap.PixelSize.Height;

                    double pixelLeft = Math.Clamp(
                        ((clippedLeft - contentRect.X) / contentRect.Width) * bitmapWidth,
                        0.0,
                        bitmapWidth);

                    double pixelTop = Math.Clamp(
                        ((clippedTop - contentRect.Y) / contentRect.Height) * bitmapHeight,
                        0.0,
                        bitmapHeight);

                    double pixelRight = Math.Clamp(
                        ((clippedRight - contentRect.X) / contentRect.Width) * bitmapWidth,
                        0.0,
                        bitmapWidth);

                    double pixelBottom = Math.Clamp(
                        ((clippedBottom - contentRect.Y) / contentRect.Height) * bitmapHeight,
                        0.0,
                        bitmapHeight);

                    if (pixelRight > pixelLeft && pixelBottom > pixelTop)
                    {
                        visiblePixelRect = new Rect(
                            pixelLeft,
                            pixelTop,
                            pixelRight - pixelLeft,
                            pixelBottom - pixelTop);
                    }
                }
            }
        }

        bool IsRectVisibleInCurrentView(Rect rect)
        {
            if (!visiblePixelRect.HasValue)
            {
                return true;
            }

            var visibleRect = visiblePixelRect.Value;

            return rect.Right > visibleRect.Left &&
                   rect.Left < visibleRect.Right &&
                   rect.Bottom > visibleRect.Top &&
                   rect.Top < visibleRect.Bottom;
        }

        bool IsVerticalPathBlocked(double sourceY, double targetY, Rect currentRect, EditableComponentHighlight targetHighlight)
        {
            double minY = Math.Min(sourceY, targetY);
            double maxY = Math.Max(sourceY, targetY);

            foreach (var other in this.thisLabelEditorWorkingHighlights)
            {
                if (ReferenceEquals(other, currentHighlight) ||
                    ReferenceEquals(other, targetHighlight) ||
                    !string.Equals(other.SchematicName, schematicName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var otherRect = new Rect(other.X, other.Y, other.Width, other.Height);

                if (!RangesOverlap(currentRect.Left, currentRect.Right, otherRect.Left, otherRect.Right))
                {
                    continue;
                }

                if (otherRect.Bottom > minY && otherRect.Top < maxY)
                {
                    return true;
                }
            }

            return false;
        }

        bool IsHorizontalPathBlocked(double sourceX, double targetX, Rect currentRect, EditableComponentHighlight targetHighlight)
        {
            double minX = Math.Min(sourceX, targetX);
            double maxX = Math.Max(sourceX, targetX);

            foreach (var other in this.thisLabelEditorWorkingHighlights)
            {
                if (ReferenceEquals(other, currentHighlight) ||
                    ReferenceEquals(other, targetHighlight) ||
                    !string.Equals(other.SchematicName, schematicName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var otherRect = new Rect(other.X, other.Y, other.Width, other.Height);

                if (!RangesOverlap(currentRect.Top, currentRect.Bottom, otherRect.Top, otherRect.Bottom))
                {
                    continue;
                }

                if (otherRect.Right > minX && otherRect.Left < maxX)
                {
                    return true;
                }
            }

            return false;
        }

        static bool TryBuildHorizontalGuide(Rect currentRect, Rect targetRect, double y, out (Point Start, Point End) guide)
        {
            guide = default;

            if (currentRect.Right < targetRect.Left)
            {
                guide = (new Point(currentRect.Right, y), new Point(targetRect.Left, y));
                return targetRect.Left - currentRect.Right > 0;
            }

            if (targetRect.Right < currentRect.Left)
            {
                guide = (new Point(targetRect.Right, y), new Point(currentRect.Left, y));
                return currentRect.Left - targetRect.Right > 0;
            }

            return false;
        }

        static bool TryBuildVerticalGuide(Rect currentRect, Rect targetRect, double x, out (Point Start, Point End) guide)
        {
            guide = default;

            if (currentRect.Bottom < targetRect.Top)
            {
                guide = (new Point(x, currentRect.Bottom), new Point(x, targetRect.Top));
                return targetRect.Top - currentRect.Bottom > 0;
            }

            if (targetRect.Bottom < currentRect.Top)
            {
                guide = (new Point(x, targetRect.Bottom), new Point(x, currentRect.Top));
                return currentRect.Top - targetRect.Bottom > 0;
            }

            return false;
        }

        if (resizesTop || resizesBottom)
        {
            Rect currentRect = BuildCurrentRect(left, top, right, bottom);
            double sourceY = resizesTop ? currentRect.Top : currentRect.Bottom;
            double bestDistance = snapThreshold + 0.001;
            double bestY = sourceY;
            var bestTargets = new List<EditableComponentHighlight>();

            void ConsiderVerticalCandidate(EditableComponentHighlight other, double candidateY)
            {
                double distance = Math.Abs(sourceY - candidateY);
                if (distance > snapThreshold ||
                    IsVerticalPathBlocked(sourceY, candidateY, currentRect, other))
                {
                    return;
                }

                if (distance < bestDistance - epsilon)
                {
                    bestDistance = distance;
                    bestY = candidateY;
                    bestTargets.Clear();
                    bestTargets.Add(other);
                    return;
                }

                if (Math.Abs(distance - bestDistance) <= epsilon &&
                    Math.Abs(candidateY - bestY) <= epsilon &&
                    !bestTargets.Contains(other))
                {
                    bestTargets.Add(other);
                }
            }

            foreach (var other in this.thisLabelEditorWorkingHighlights)
            {
                if (ReferenceEquals(other, currentHighlight) ||
                    this.IsSelectedLabelEditorHighlight(other) ||
                    !string.Equals(other.SchematicName, schematicName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var otherRect = new Rect(other.X, other.Y, other.Width, other.Height);
                if (!IsRectVisibleInCurrentView(otherRect))
                {
                    continue;
                }

                ConsiderVerticalCandidate(other, otherRect.Top);
                ConsiderVerticalCandidate(other, otherRect.Bottom);
            }

            if (bestTargets.Count > 0)
            {
                if (resizesTop)
                {
                    top = bestY;
                }
                else
                {
                    bottom = bestY;
                }

                currentRect = BuildCurrentRect(left, top, right, bottom);

                foreach (var bestTarget in bestTargets)
                {
                    var targetRect = new Rect(bestTarget.X, bestTarget.Y, bestTarget.Width, bestTarget.Height);

                    if (TryBuildHorizontalGuide(currentRect, targetRect, bestY, out var guide))
                    {
                        snapGuides.Add(guide);
                    }
                }
            }
        }

        if (resizesLeft || resizesRight)
        {
            Rect currentRect = BuildCurrentRect(left, top, right, bottom);
            double sourceX = resizesLeft ? currentRect.Left : currentRect.Right;
            double bestDistance = snapThreshold + 0.001;
            double bestX = sourceX;
            var bestTargets = new List<EditableComponentHighlight>();

            void ConsiderHorizontalCandidate(EditableComponentHighlight other, double candidateX)
            {
                double distance = Math.Abs(sourceX - candidateX);
                if (distance > snapThreshold ||
                    IsHorizontalPathBlocked(sourceX, candidateX, currentRect, other))
                {
                    return;
                }

                if (distance < bestDistance - epsilon)
                {
                    bestDistance = distance;
                    bestX = candidateX;
                    bestTargets.Clear();
                    bestTargets.Add(other);
                    return;
                }

                if (Math.Abs(distance - bestDistance) <= epsilon &&
                    Math.Abs(candidateX - bestX) <= epsilon &&
                    !bestTargets.Contains(other))
                {
                    bestTargets.Add(other);
                }
            }

            foreach (var other in this.thisLabelEditorWorkingHighlights)
            {
                if (ReferenceEquals(other, currentHighlight) ||
                    this.IsSelectedLabelEditorHighlight(other) ||
                    !string.Equals(other.SchematicName, schematicName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var otherRect = new Rect(other.X, other.Y, other.Width, other.Height);
                if (!IsRectVisibleInCurrentView(otherRect))
                {
                    continue;
                }

                ConsiderHorizontalCandidate(other, otherRect.Left);
                ConsiderHorizontalCandidate(other, otherRect.Right);
            }

            if (bestTargets.Count > 0)
            {
                if (resizesLeft)
                {
                    left = bestX;
                }
                else
                {
                    right = bestX;
                }

                currentRect = BuildCurrentRect(left, top, right, bottom);

                foreach (var bestTarget in bestTargets)
                {
                    var targetRect = new Rect(bestTarget.X, bestTarget.Y, bestTarget.Width, bestTarget.Height);

                    if (TryBuildVerticalGuide(currentRect, targetRect, bestX, out var guide))
                    {
                        snapGuides.Add(guide);
                    }
                }
            }
        }
    }

    // ###########################################################################################
    // Computes the editor overlay image content rect using the exact same top-left anchoring
    // logic as the editor overlay renderer so pointer hit testing matches drawn rectangles.
    // ###########################################################################################
    private Rect GetLabelEditorImageContentRect()
    {
        var bitmap = this.currentFullResBitmap;

        Size controlSize = this.SchematicsLabelEditorOverlay.Bounds.Size;
        if (controlSize.Width <= 0 || controlSize.Height <= 0)
        {
            controlSize = this.SchematicsContainer.Bounds.Size;
        }

        if (bitmap == null || controlSize.Width <= 0 || controlSize.Height <= 0)
        {
            return new Rect(controlSize);
        }

        double containerAspect = controlSize.Width / controlSize.Height;
        double bitmapAspect = (double)bitmap.PixelSize.Width / bitmap.PixelSize.Height;

        if (bitmapAspect > containerAspect)
        {
            return new Rect(0, 0, controlSize.Width, controlSize.Width / bitmapAspect);
        }
        else
        {
            return new Rect(0, 0, controlSize.Height * bitmapAspect, controlSize.Height);
        }
    }

    // ###########################################################################################
    // Converts a schematic container pointer position into bitmap pixel coordinates used by the
    // label editor, returning false if the pointer is outside the visible image content area.
    // ###########################################################################################
    private bool TryGetLabelEditorPixelPoint(Point pointerInContainer, out Point pixelPoint)
    {
        pixelPoint = default;

        if (!this.thisIsLabelEditorMode || this.currentFullResBitmap == null)
        {
            return false;
        }

        if (!TryInvert(this.schematicsMatrix, out var inv))
        {
            return false;
        }

        var localPoint = new Point(
            (pointerInContainer.X * inv.M11) + (pointerInContainer.Y * inv.M21) + inv.M31,
            (pointerInContainer.X * inv.M12) + (pointerInContainer.Y * inv.M22) + inv.M32);

        var contentRect = this.GetLabelEditorImageContentRect();
        if (contentRect.Width <= 0 || contentRect.Height <= 0 || !contentRect.Contains(localPoint))
        {
            return false;
        }

        double px = ((localPoint.X - contentRect.X) / contentRect.Width) * this.currentFullResBitmap.PixelSize.Width;
        double py = ((localPoint.Y - contentRect.Y) / contentRect.Height) * this.currentFullResBitmap.PixelSize.Height;

        pixelPoint = new Point(px, py);
        return true;
    }

    // ###########################################################################################
    // Tries to find the topmost editable highlight rectangle under the current pointer position.
    // Returns the working-list index for direct select/delete operations.
    // ###########################################################################################
    private bool TryGetLabelEditorHighlightAtContainerPoint(Point pointerInContainer, out int workingIndex)
    {
        workingIndex = -1;

        if (!this.TryGetLabelEditorPixelPoint(pointerInContainer, out var pixelPoint))
        {
            return false;
        }

        string schematicName = this.GetCurrentSchematicName();
        if (string.IsNullOrWhiteSpace(schematicName))
        {
            return false;
        }

        for (int i = this.thisLabelEditorWorkingHighlights.Count - 1; i >= 0; i--)
        {
            var row = this.thisLabelEditorWorkingHighlights[i];
            if (!string.Equals(row.SchematicName, schematicName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rect = new Rect(row.X, row.Y, row.Width, row.Height);
            if (!rect.Contains(pixelPoint))
            {
                continue;
            }

            workingIndex = i;
            return true;
        }

        return false;
    }

    // ###########################################################################################
    // Clears the current label-editor selection and removes visible selection markers.
    // ###########################################################################################
    private void ClearSelectedLabelEditorHighlight()
    {
        this.ClearSelectedLabelEditorHighlights();
    }

    // ###########################################################################################
    // Deletes the requested working-copy editor highlight and refreshes the overlay immediately.
    // Records the previous state so the deletion can be undone within the current editor session.
    // ###########################################################################################
    private void DeleteLabelEditorHighlight(int workingIndex)
    {
        if (workingIndex < 0 || workingIndex >= this.thisLabelEditorWorkingHighlights.Count)
        {
            return;
        }

        this.PushLabelEditorUndoState(this.CreateLabelEditorUndoState());

        var deleted = this.thisLabelEditorWorkingHighlights[workingIndex];
        this.thisLabelEditorWorkingHighlights.RemoveAt(workingIndex);
        this.thisSelectedLabelEditorHighlights.Remove(deleted);
        this.thisLabelEditorOriginalDragRectangles.Remove(deleted);

        if (ReferenceEquals(this.thisSelectedLabelEditorHighlight, deleted))
        {
            this.thisSelectedLabelEditorHighlight = this.GetFirstSelectedLabelEditorHighlightForCurrentSchematic();
        }

        this.RefreshLabelEditorOverlay();

        Logger.Debug($"Label editor rectangle deleted for board label [{deleted.BoardLabel}] on schematic [{deleted.SchematicName}]");
    }

    // ###########################################################################################
    // Normalizes a rectangle so width and height are always positive regardless of drag direction.
    // ###########################################################################################
    private static Rect CreateNormalizedRect(Point start, Point end)
    {
        double x = Math.Min(start.X, end.X);
        double y = Math.Min(start.Y, end.Y);
        double width = Math.Abs(end.X - start.X);
        double height = Math.Abs(end.Y - start.Y);

        return new Rect(x, y, width, height);
    }

    // ###########################################################################################
    // Returns true when the pointer is currently inside the new-label prompt bounds.
    // ###########################################################################################
    private bool IsPointerInsideNewLabelPrompt(Point containerPoint)
    {
        if (!this.SchematicsNewLabelPromptBorder.IsVisible)
        {
            return false;
        }

        Point? translatedTopLeft = this.SchematicsNewLabelPromptBorder.TranslatePoint(new Point(0, 0), this.SchematicsContainer);
        if (!translatedTopLeft.HasValue)
        {
            return false;
        }

        var promptRect = new Rect(translatedTopLeft.Value, this.SchematicsNewLabelPromptBorder.Bounds.Size);
        return promptRect.Contains(containerPoint);
    }

    // ###########################################################################################
    // Starts drawing a new editor rectangle from the current bitmap pixel position.
    // ###########################################################################################
    private void StartDrawingLabelEditorRectangle(Point startPixelPoint)
    {
        this.thisIsDrawingLabelEditorRectangle = true;
        this.thisLabelEditorDrawStartPixelPoint = startPixelPoint;
        this.thisLabelEditorDraftRectangle = new Rect(startPixelPoint.X, startPixelPoint.Y, 0, 0);
        this.RefreshLabelEditorOverlay();
    }

    // ###########################################################################################
    // Updates the current draft editor rectangle while the mouse is being dragged.
    // ###########################################################################################
    private void UpdateDrawingLabelEditorRectangle(Point currentPixelPoint)
    {
        if (!this.thisIsDrawingLabelEditorRectangle)
        {
            return;
        }

        this.thisLabelEditorDraftRectangle = CreateNormalizedRect(this.thisLabelEditorDrawStartPixelPoint, currentPixelPoint);
        this.RefreshLabelEditorOverlay();
    }

    // ###########################################################################################
    // Returns true when a newly drawn editor rectangle is too small to be considered intentional.
    // This prevents accidental tiny drags from opening the new-label prompt.
    // ###########################################################################################
    private static bool IsLabelEditorRectangleTooSmall(Rect rect)
    {
        const double minimumWidth = 15.0;
        const double minimumHeight = 15.0;
        const double minimumArea = minimumWidth * minimumHeight;

        return rect.Width < minimumWidth ||
               rect.Height < minimumHeight ||
               (rect.Width * rect.Height) < minimumArea;
    }

    // ###########################################################################################
    // Completes the current rectangle drawing operation and opens the board-label prompt.
    // Records the pre-create state so the new rectangle can be undone after confirmation.
    // ###########################################################################################
    private void CompleteDrawingLabelEditorRectangle(Point releaseContainerPoint, Point releasePixelPoint)
    {
        if (!this.thisIsDrawingLabelEditorRectangle)
        {
            return;
        }

        var finalRect = CreateNormalizedRect(this.thisLabelEditorDrawStartPixelPoint, releasePixelPoint);

        this.thisIsDrawingLabelEditorRectangle = false;
        this.thisLabelEditorDraftRectangle = null;

        if (IsLabelEditorRectangleTooSmall(finalRect))
        {
            this.RefreshLabelEditorOverlay();
            return;
        }

        this.PushLabelEditorUndoState(this.CreateLabelEditorUndoState());

        var newRow = new EditableComponentHighlight
        {
            SchematicName = this.GetCurrentSchematicName(),
            BoardLabel = string.Empty,
            Category = string.Empty,
            X = finalRect.X,
            Y = finalRect.Y,
            Width = finalRect.Width,
            Height = finalRect.Height
        };

        this.thisLabelEditorWorkingHighlights.Add(newRow);
        this.SetSingleSelectedLabelEditorHighlight(newRow, refresh: false);
        this.thisPendingNewLabelEditorHighlight = newRow;

        this.RefreshLabelEditorOverlay();
        this.ShowNewLabelEditorPrompt(releaseContainerPoint);
    }

    // ###########################################################################################
    // Shows the prompt used for entering the board label and category of a newly drawn rectangle.
    // ###########################################################################################
    private void ShowNewLabelEditorPrompt(Point containerPoint)
    {
        double estimatedWidth = 280.0;
        double estimatedHeight = 170.0;

        double x = Math.Clamp(containerPoint.X, 6.0, Math.Max(6.0, this.SchematicsContainer.Bounds.Width - estimatedWidth));
        double y = Math.Clamp(containerPoint.Y, 6.0, Math.Max(6.0, this.SchematicsContainer.Bounds.Height - estimatedHeight));

        var categories = this.GetAvailableLabelEditorCategories();
        string preferredCategory =
            this.MainWindow?.CategoryFilterListBox.SelectedItems?
                .Cast<string>()
                .FirstOrDefault(category => !string.IsNullOrWhiteSpace(category))
            ?? categories.FirstOrDefault() ?? "General";

        this.NewLabelEditorBoardLabelTextBox.Text = string.Empty;
        this.NewLabelEditorCategoryComboBox.ItemsSource = categories;
        this.NewLabelEditorCategoryComboBox.SelectedItem = preferredCategory;

        this.SchematicsNewLabelPromptBorder.Margin = new Thickness(x, y, 0, 0);
        this.SchematicsNewLabelPromptBorder.IsVisible = true;

        Dispatcher.UIThread.Post(() => this.NewLabelEditorBoardLabelTextBox.Focus(), DispatcherPriority.Background);
    }

    // ###########################################################################################
    // Hides the prompt used for entering the board label of a newly drawn rectangle.
    // ###########################################################################################
    private void HideNewLabelEditorPrompt()
    {
        this.SchematicsNewLabelPromptBorder.IsVisible = false;
        this.NewLabelEditorBoardLabelTextBox.Text = string.Empty;
        this.NewLabelEditorCategoryComboBox.ItemsSource = null;
        this.NewLabelEditorCategoryComboBox.SelectedItem = null;
    }

    // ###########################################################################################
    // Commits the entered board label and category onto the newly created rectangle and keeps it selected.
    // ###########################################################################################
    private void ConfirmNewLabelEditorPrompt()
    {
        if (this.thisPendingNewLabelEditorHighlight == null)
        {
            this.HideNewLabelEditorPrompt();
            this.SchematicsContainer.Focus();
            return;
        }

        string boardLabel = this.NewLabelEditorBoardLabelTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(boardLabel))
        {
            this.NewLabelEditorBoardLabelTextBox.Focus();
            return;
        }

        string category = this.NewLabelEditorCategoryComboBox.SelectedItem as string ?? string.Empty;
        if (string.IsNullOrWhiteSpace(category))
        {
            this.NewLabelEditorCategoryComboBox.Focus();
            return;
        }

        this.thisPendingNewLabelEditorHighlight.BoardLabel = boardLabel;
        this.thisPendingNewLabelEditorHighlight.Category = category;
        this.thisPendingNewLabelEditorHighlight = null;

        this.HideNewLabelEditorPrompt();
        this.RefreshLabelEditorOverlay();
        this.SchematicsContainer.Focus();
    }

    // ###########################################################################################
    // Cancels the newly created rectangle naming prompt and removes the pending rectangle.
    // ###########################################################################################
    private void CancelNewLabelEditorPrompt()
    {
        if (this.thisPendingNewLabelEditorHighlight != null)
        {
            this.thisLabelEditorWorkingHighlights.Remove(this.thisPendingNewLabelEditorHighlight);
            this.thisSelectedLabelEditorHighlights.Remove(this.thisPendingNewLabelEditorHighlight);
            this.thisPendingNewLabelEditorHighlight = null;
            this.thisSelectedLabelEditorHighlight = null;
        }

        this.thisLabelEditorDraftRectangle = null;
        this.thisIsDrawingLabelEditorRectangle = false;

        this.HideNewLabelEditorPrompt();
        this.RefreshLabelEditorOverlay();
        this.SchematicsContainer.Focus();
    }

    // ###########################################################################################
    // Converts a schematic container pointer position into overlay-local coordinates used for
    // editor-handle hit testing, returning false if the pointer is outside the image content area.
    // ###########################################################################################
    private bool TryGetLabelEditorLocalPoint(Point pointerInContainer, out Point localPoint)
    {
        localPoint = default;

        if (!this.thisIsLabelEditorMode || this.currentFullResBitmap == null)
        {
            return false;
        }

        if (!TryInvert(this.schematicsMatrix, out var inv))
        {
            return false;
        }

        localPoint = new Point(
            (pointerInContainer.X * inv.M11) + (pointerInContainer.Y * inv.M21) + inv.M31,
            (pointerInContainer.X * inv.M12) + (pointerInContainer.Y * inv.M22) + inv.M32);

        var contentRect = this.GetLabelEditorImageContentRect();
        return contentRect.Width > 0 && contentRect.Height > 0 && contentRect.Contains(localPoint);
    }

    // ###########################################################################################
    // Converts a pixel-space highlight rectangle into editor overlay local coordinates.
    // ###########################################################################################
    private Rect ConvertLabelEditorPixelRectToLocalRect(Rect pixelRect)
    {
        if (this.currentFullResBitmap == null)
        {
            return default;
        }

        var contentRect = this.GetLabelEditorImageContentRect();

        double sx = contentRect.Width / this.currentFullResBitmap.PixelSize.Width;
        double sy = contentRect.Height / this.currentFullResBitmap.PixelSize.Height;

        double x = contentRect.X + (pixelRect.X * sx);
        double y = contentRect.Y + (pixelRect.Y * sy);
        double w = pixelRect.Width * sx;
        double h = pixelRect.Height * sy;

        return new Rect(x, y, w, h);
    }

    // ###########################################################################################
    // Tries to hit one of the resize handles of any selected rectangle under the pointer.
    // Corner handles are evaluated first, and side handles only exist in the center gap between
    // corners so tiny components still allow true corner resizing in both X and Y.
    // ###########################################################################################
    private bool TryGetSelectedLabelEditorHandleAtContainerPoint(
        Point pointerInContainer,
        out int workingIndex,
        out LabelEditorDragMode dragMode)
    {
        workingIndex = -1;
        dragMode = LabelEditorDragMode.None;

        if (this.currentFullResBitmap == null)
        {
            return false;
        }

        if (!this.TryGetLabelEditorLocalPoint(pointerInContainer, out var localPoint))
        {
            return false;
        }

        double scale = Math.Max(0.0001, this.schematicsMatrix.M11);
        string schematicName = this.GetCurrentSchematicName();

        for (int i = this.thisLabelEditorWorkingHighlights.Count - 1; i >= 0; i--)
        {
            var row = this.thisLabelEditorWorkingHighlights[i];

            if (!string.Equals(row.SchematicName, schematicName, StringComparison.OrdinalIgnoreCase) ||
                !this.IsSelectedLabelEditorHighlight(row))
            {
                continue;
            }

            var localRect = this.ConvertLabelEditorPixelRectToLocalRect(new Rect(row.X, row.Y, row.Width, row.Height));

            foreach (var hitTarget in BuildLabelEditorHandleHitRects(localRect, scale))
            {
                if (!hitTarget.HitRect.Contains(localPoint))
                {
                    continue;
                }

                workingIndex = i;
                dragMode = hitTarget.DragMode;
                return true;
            }
        }

        return false;
    }

    // ###########################################################################################
    // Starts dragging an existing highlight rectangle for move or resize operations.
    // ###########################################################################################
    private void StartLabelEditorDrag(int workingIndex, Point startPixelPoint, LabelEditorDragMode dragMode)
    {
        if (workingIndex < 0 || workingIndex >= this.thisLabelEditorWorkingHighlights.Count)
        {
            return;
        }

        var anchorHighlight = this.thisLabelEditorWorkingHighlights[workingIndex];

        if (!this.IsSelectedLabelEditorHighlight(anchorHighlight))
        {
            this.SetSingleSelectedLabelEditorHighlight(anchorHighlight, refresh: false);
        }
        else
        {
            this.thisSelectedLabelEditorHighlight = anchorHighlight;
        }

        this.thisLabelEditorDragMode = dragMode;
        this.thisLabelEditorDragStartPixelPoint = startPixelPoint;
        this.thisLabelEditorOriginalSelectionBounds = new Rect(
            anchorHighlight.X,
            anchorHighlight.Y,
            anchorHighlight.Width,
            anchorHighlight.Height);

        this.CaptureSelectedLabelEditorDragState();
        this.RefreshLabelEditorOverlay();
    }

    // ###########################################################################################
    // Applies the current drag delta to the selected rectangle for move or resize operations.
    // Holding Shift during mouse-resize suppresses snap alignment.
    // ###########################################################################################
    private void UpdateLabelEditorDrag(Point currentPixelPoint, KeyModifiers modifiers)
    {
        if (!this.HasSelectedLabelEditorHighlightsForCurrentSchematic() ||
            this.thisLabelEditorDragMode == LabelEditorDragMode.None)
        {
            return;
        }

        double dx = currentPixelPoint.X - this.thisLabelEditorDragStartPixelPoint.X;
        double dy = currentPixelPoint.Y - this.thisLabelEditorDragStartPixelPoint.Y;
        bool suppressSnap = modifiers.HasFlag(KeyModifiers.Shift);

        var snapGuides = new List<(Point Start, Point End)>();

        foreach (var row in this.GetSelectedLabelEditorHighlightsForCurrentSchematic())
        {
            if (!this.thisLabelEditorOriginalDragRectangles.TryGetValue(row, out var originalRect))
            {
                originalRect = new Rect(row.X, row.Y, row.Width, row.Height);
            }

            double left = originalRect.Left;
            double top = originalRect.Top;
            double right = originalRect.Right;
            double bottom = originalRect.Bottom;

            switch (this.thisLabelEditorDragMode)
            {
                case LabelEditorDragMode.Move:
                    left += dx;
                    right += dx;
                    top += dy;
                    bottom += dy;
                    break;

                case LabelEditorDragMode.ResizeTopLeft:
                    left += dx;
                    top += dy;
                    break;

                case LabelEditorDragMode.ResizeTop:
                    top += dy;
                    break;

                case LabelEditorDragMode.ResizeTopRight:
                    right += dx;
                    top += dy;
                    break;

                case LabelEditorDragMode.ResizeRight:
                    right += dx;
                    break;

                case LabelEditorDragMode.ResizeBottomRight:
                    right += dx;
                    bottom += dy;
                    break;

                case LabelEditorDragMode.ResizeBottom:
                    bottom += dy;
                    break;

                case LabelEditorDragMode.ResizeBottomLeft:
                    left += dx;
                    bottom += dy;
                    break;

                case LabelEditorDragMode.ResizeLeft:
                    left += dx;
                    break;
            }

            this.ApplyLabelEditorResizeSnap(row, ref left, ref top, ref right, ref bottom, snapGuides, suppressSnap);

            const double minimumSize = 1.0;

            if (right < left + minimumSize)
            {
                if (this.thisLabelEditorDragMode == LabelEditorDragMode.ResizeLeft ||
                    this.thisLabelEditorDragMode == LabelEditorDragMode.ResizeTopLeft ||
                    this.thisLabelEditorDragMode == LabelEditorDragMode.ResizeBottomLeft)
                {
                    left = right - minimumSize;
                }
                else
                {
                    right = left + minimumSize;
                }
            }

            if (bottom < top + minimumSize)
            {
                if (this.thisLabelEditorDragMode == LabelEditorDragMode.ResizeTop ||
                    this.thisLabelEditorDragMode == LabelEditorDragMode.ResizeTopLeft ||
                    this.thisLabelEditorDragMode == LabelEditorDragMode.ResizeTopRight)
                {
                    top = bottom - minimumSize;
                }
                else
                {
                    bottom = top + minimumSize;
                }
            }

            row.X = left;
            row.Y = top;
            row.Width = Math.Max(1.0, right - left);
            row.Height = Math.Max(1.0, bottom - top);
        }

        this.RefreshLabelEditorOverlay(snapGuides);
    }

    // ###########################################################################################
    // Finishes the current move or resize operation for the selected rectangle.
    // Clears any temporary snap guides and records the pre-drag state for undo when needed.
    // ###########################################################################################
    private void CompleteLabelEditorDrag()
    {
        if (this.thisLabelEditorDragMode != LabelEditorDragMode.None)
        {
            var beforeDragState = this.CreateLabelEditorUndoStateFromOriginalDragState();
            var afterDragState = this.CreateLabelEditorUndoState();

            if (!this.AreLabelEditorUndoStatesEqual(beforeDragState, afterDragState))
            {
                this.PushLabelEditorUndoState(beforeDragState);
            }
        }

        this.thisLabelEditorDragMode = LabelEditorDragMode.None;
        this.thisLabelEditorOriginalDragRectangles.Clear();

        if (this.SchematicsLabelEditorOverlay.SnapGuides.Count > 0)
        {
            this.SetLabelEditorOverlayTransientState(
                snapGuides: Array.Empty<(Point Start, Point End)>());
        }
    }

    // ###########################################################################################
    // Applies keyboard move, expand, or shrink operations to the selected editor rectangle.
    // Arrow keys move by 1 px, Shift expands in the pressed direction, and Alt shrinks from
    // the opposite side of the pressed direction. Each committed step is undoable.
    // ###########################################################################################
    private bool ApplySelectedLabelEditorKeyboardStep(Key key, KeyModifiers modifiers)
    {
        if (!this.thisIsLabelEditorMode ||
            !this.HasSelectedLabelEditorHighlightsForCurrentSchematic() ||
            this.SchematicsNewLabelPromptBorder.IsVisible)
        {
            return false;
        }

        if (modifiers.HasFlag(KeyModifiers.Shift) && modifiers.HasFlag(KeyModifiers.Alt))
        {
            return false;
        }

        var selectedRows = this.GetSelectedLabelEditorHighlightsForCurrentSchematic();
        if (selectedRows.Count == 0)
        {
            return false;
        }

        var undoState = this.CreateLabelEditorUndoState();

        var sourceRects = selectedRows.ToDictionary(
            row => row,
            row => new Rect(row.X, row.Y, row.Width, row.Height));

        bool isShift = modifiers.HasFlag(KeyModifiers.Shift);
        bool isAlt = modifiers.HasFlag(KeyModifiers.Alt);
        const double step = 1.0;
        bool changed = false;

        foreach (var row in selectedRows)
        {
            var originalRect = sourceRects[row];

            double x = originalRect.X;
            double y = originalRect.Y;
            double width = originalRect.Width;
            double height = originalRect.Height;

            if (!isShift && !isAlt)
            {
                switch (key)
                {
                    case Key.Left:
                        x -= step;
                        changed = true;
                        break;

                    case Key.Right:
                        x += step;
                        changed = true;
                        break;

                    case Key.Up:
                        y -= step;
                        changed = true;
                        break;

                    case Key.Down:
                        y += step;
                        changed = true;
                        break;
                }
            }
            else if (isShift)
            {
                switch (key)
                {
                    case Key.Left:
                        x -= step;
                        width += step;
                        changed = true;
                        break;

                    case Key.Right:
                        width += step;
                        changed = true;
                        break;

                    case Key.Up:
                        y -= step;
                        height += step;
                        changed = true;
                        break;

                    case Key.Down:
                        height += step;
                        changed = true;
                        break;
                }
            }
            else if (isAlt)
            {
                switch (key)
                {
                    case Key.Left:
                        if (width > step)
                        {
                            width -= step;
                            changed = true;
                        }
                        break;

                    case Key.Right:
                        if (width > step)
                        {
                            x += step;
                            width -= step;
                            changed = true;
                        }
                        break;

                    case Key.Up:
                        if (height > step)
                        {
                            height -= step;
                            changed = true;
                        }
                        break;

                    case Key.Down:
                        if (height > step)
                        {
                            y += step;
                            height -= step;
                            changed = true;
                        }
                        break;
                }
            }

            row.X = x;
            row.Y = y;
            row.Width = Math.Max(1.0, width);
            row.Height = Math.Max(1.0, height);
        }

        if (!changed)
        {
            return false;
        }

        this.PushLabelEditorUndoState(undoState);
        this.RefreshLabelEditorOverlay();
        return true;
    }

    // ###########################################################################################
    // Handles keyboard interaction for label-editor and KiCad calibration capture workflows.
    // Ctrl+Z undoes label-editor changes and Ctrl+Y redoes them within the current editor session.
    // ###########################################################################################
    private void OnSchematicsKeyDown(object? sender, KeyEventArgs e)
    {
        this.UpdateInteractiveCadTraceHoverShiftState(e.KeyModifiers);

        if (this.thisIsKiCadCalibrationCaptureMode && e.Key == Key.Escape)
        {
            this.CancelKiCadCalibrationCapture();
            e.Handled = true;
            return;
        }

        if (!this.thisIsLabelEditorMode)
        {
            return;
        }

        if (this.SchematicsNewLabelPromptBorder.IsVisible)
        {
            return;
        }

        bool isCtrlDown = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        if (isCtrlDown && e.Key == Key.Z)
        {
            if (this.TryUndoLabelEditorChange())
            {
                e.Handled = true;
            }

            return;
        }

        if (isCtrlDown && e.Key == Key.Y)
        {
            if (this.TryRedoLabelEditorChange())
            {
                e.Handled = true;
            }

            return;
        }

        if (this.ApplySelectedLabelEditorKeyboardStep(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
        }
    }

    // ###########################################################################################
    // Tracks key releases so SHIFT-based KiCad hover highlighting updates immediately.
    // ###########################################################################################
    private void OnSchematicsKeyUp(object? sender, KeyEventArgs e)
    {
        this.UpdateInteractiveCadTraceHoverShiftState(e.KeyModifiers);
    }

    // ###########################################################################################
    // Builds the available category list for the new-label prompt and ensures at least one item
    // exists so a newly created component can always be categorized immediately.
    // ###########################################################################################
    private List<string> GetAvailableLabelEditorCategories()
    {
        var boardData = this.MainWindow?.CurrentBoardData;

        var categories = boardData?.Components
            .Select(component => component.Category?.Trim() ?? string.Empty)
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category, StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? new List<string>();

        if (categories.Count == 0)
        {
            categories.Add("General");
        }

        return categories;
    }

    // ###########################################################################################
    // Handles Enter/Escape while the new-label prompt is open so the prompt can be confirmed
    // directly from the keyboard without clicking the buttons.
    // ###########################################################################################
    private void OnNewLabelEditorPromptKeyDown(object? sender, KeyEventArgs e)
    {
        if (!this.SchematicsNewLabelPromptBorder.IsVisible)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            this.ConfirmNewLabelEditorPrompt();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            this.CancelNewLabelEditorPrompt();
            e.Handled = true;
        }
    }

    // ###########################################################################################
    // Validates the current schematic editor rows before anything is written to disk.
    // ###########################################################################################
    private bool TryValidateLabelEditorSave(out string validationError)
    {
        validationError = string.Empty;

        string schematicName = this.GetCurrentSchematicName();
        if (string.IsNullOrWhiteSpace(schematicName))
        {
            validationError = "No schematic is currently selected";
            return false;
        }

        var rows = this.thisLabelEditorWorkingHighlights
            .Where(row => string.Equals(row.SchematicName, schematicName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.BoardLabel))
            {
                validationError = "One or more rectangles are missing a board label";
                return false;
            }

            if (string.IsNullOrWhiteSpace(row.Category))
            {
                validationError = $"The label [{row.BoardLabel}] is missing a category";
                return false;
            }

            if (row.Width <= 0 || row.Height <= 0)
            {
                validationError = $"The label [{row.BoardLabel}] has invalid rectangle dimensions";
                return false;
            }
        }

        var conflictingCategoryGroup = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.BoardLabel))
            .GroupBy(row => row.BoardLabel.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group
                .Select(row => row.Category?.Trim() ?? string.Empty)
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() > 1);

        if (conflictingCategoryGroup != null)
        {
            validationError = $"The board label [{conflictingCategoryGroup.Key}] has conflicting categories in the editor";
            return false;
        }

        return true;
    }

    // ###########################################################################################
    // Converts the current schematic editor rows into the workbook save DTO used by the writer.
    // ###########################################################################################
    private List<LabelEditorSaveRow> BuildLabelEditorSaveRowsForCurrentSchematic()
    {
        string schematicName = this.GetCurrentSchematicName();
        string region = this.MainWindow?.LocalRegion?.Trim() ?? string.Empty;

        return this.thisLabelEditorWorkingHighlights
            .Where(row => string.Equals(row.SchematicName, schematicName, StringComparison.OrdinalIgnoreCase))
            .Select(row => new LabelEditorSaveRow
            {
                SchematicName = row.SchematicName.Trim(),
                BoardLabel = row.BoardLabel.Trim(),
                Category = row.Category.Trim(),
                Region = region,
                X = row.X,
                Y = row.Y,
                Width = row.Width,
                Height = row.Height
            })
            .ToList();
    }

    // ###########################################################################################
    // Updates the schematic cursor for label-editor interactions.
    // Shows Hand over resize handles and SizeAll over movable rectangles.
    // ###########################################################################################
    private void UpdateLabelEditorCursor(Point pointerInContainer)
    {
        if (!this.thisIsLabelEditorMode)
        {
            this.SchematicsContainer.Cursor = Cursor.Default;
            return;
        }

        if (this.thisLabelEditorDragMode != LabelEditorDragMode.None)
        {
            this.SchematicsContainer.Cursor = this.thisLabelEditorDragMode == LabelEditorDragMode.Move
                ? new Cursor(StandardCursorType.SizeAll)
                : new Cursor(StandardCursorType.Hand);
            return;
        }

        if (this.thisIsDrawingLabelEditorRectangle)
        {
            this.SchematicsContainer.Cursor = Cursor.Default;
            return;
        }

        if (this.TryGetSelectedLabelEditorHandleAtContainerPoint(pointerInContainer, out _))
        {
            this.SchematicsContainer.Cursor = new Cursor(StandardCursorType.Hand);
            return;
        }

        if (this.TryGetSelectedLabelEditorHighlightAtContainerPoint(pointerInContainer, out _))
        {
            this.SchematicsContainer.Cursor = new Cursor(StandardCursorType.SizeAll);
            return;
        }

        if (this.TryGetLabelEditorHighlightAtContainerPoint(pointerInContainer, out _))
        {
            this.SchematicsContainer.Cursor = new Cursor(StandardCursorType.SizeAll);
            return;
        }

        this.SchematicsContainer.Cursor = Cursor.Default;
    }

    // ###########################################################################################
    // Shows a modal error dialog when label-editor changes cannot be written to the Excel file
    // so save failures are visible immediately instead of only appearing in the logfile.
    // ###########################################################################################
    private async Task ShowLabelEditorSaveFailedDialogAsync(string errorMessage)
    {
        string message = string.IsNullOrWhiteSpace(errorMessage)
            ? "The label editor changes could not be saved."
            : errorMessage;

        var closeButton = new Button
        {
            Content = "Close",
            MinWidth = 110,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };

        var dialog = new Window
        {
            Title = "Label editor save failed",
            Width = 540,
            MinWidth = 460,
            CanResize = false,
            ShowInTaskbar = false,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        closeButton.Click += (_, _) => dialog.Close();

        var errorAccentBrush = new SolidColorBrush(Color.Parse("#C62828"));
        var panelBackgroundBrush = this.ResolveThemeBrush("Schematics_Panels_Bg", new SolidColorBrush(Color.Parse("#FFF8F8")));
        var panelBorderBrush = this.ResolveThemeBrush("Schematics_Panels_Border", new SolidColorBrush(Color.Parse("#E0B4B4")));
        var foregroundBrush = this.ResolveThemeBrush("Schematics_Panels_Fg", Brushes.Black);

        dialog.Content = new Border
        {
            Background = panelBackgroundBrush,
            BorderBrush = panelBorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(18),
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    new Border
                    {
                        Background = errorAccentBrush,
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(12, 10),
                        Child = new StackPanel
                        {
                            Orientation = Avalonia.Layout.Orientation.Horizontal,
                            Spacing = 10,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = "⚠",
                                    FontSize = 22,
                                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                                    Foreground = Brushes.White
                                },
                                new TextBlock
                                {
                                    Text = "Unable to save label editor changes",
                                    FontSize = 14,
                                    FontWeight = FontWeight.Bold,
                                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                                    Foreground = Brushes.White,
                                    TextWrapping = TextWrapping.Wrap
                                }
                            }
                        }
                    },
                    new TextBlock
                    {
                        Text = message,
                        Foreground = foregroundBrush,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = "If the Excel workbook is open in another program, close it and try again. Check the logfile for technical details.",
                        Foreground = foregroundBrush,
                        Opacity = 0.85,
                        TextWrapping = TextWrapping.Wrap
                    },
                    closeButton
                }
            }
        };

        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            await dialog.ShowDialog(owner);
        }
        else
        {
            dialog.Show();
        }
    }

    // ###########################################################################################
    // Shows a modal error dialog when the label editor contains invalid data so validation
    // problems are visible immediately instead of only appearing in the logfile.
    // ###########################################################################################
    private async Task ShowLabelEditorValidationFailedDialogAsync(string errorMessage)
    {
        string message = string.IsNullOrWhiteSpace(errorMessage)
            ? "The label editor contains invalid data."
            : errorMessage;

        var closeButton = new Button
        {
            Content = "Close",
            MinWidth = 110,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };

        var dialog = new Window
        {
            Title = "Label editor validation failed",
            Width = 540,
            MinWidth = 460,
            CanResize = false,
            ShowInTaskbar = false,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        closeButton.Click += (_, _) => dialog.Close();

        var errorAccentBrush = new SolidColorBrush(Color.Parse("#C62828"));
        var panelBackgroundBrush = this.ResolveThemeBrush("Schematics_Panels_Bg", new SolidColorBrush(Color.Parse("#FFF8F8")));
        var panelBorderBrush = this.ResolveThemeBrush("Schematics_Panels_Border", new SolidColorBrush(Color.Parse("#E0B4B4")));
        var foregroundBrush = this.ResolveThemeBrush("Schematics_Panels_Fg", Brushes.Black);

        dialog.Content = new Border
        {
            Background = panelBackgroundBrush,
            BorderBrush = panelBorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(18),
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    new Border
                    {
                        Background = errorAccentBrush,
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(12, 10),
                        Child = new StackPanel
                        {
                            Orientation = Avalonia.Layout.Orientation.Horizontal,
                            Spacing = 10,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = "⚠",
                                    FontSize = 22,
                                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                                    Foreground = Brushes.White
                                },
                                new TextBlock
                                {
                                    Text = "Unable to apply component label editor changes",
                                    FontSize = 14,
                                    FontWeight = FontWeight.Bold,
                                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                                    Foreground = Brushes.White,
                                    TextWrapping = TextWrapping.Wrap
                                }
                            }
                        }
                    },
                    new TextBlock
                    {
                        Text = message,
                        Foreground = foregroundBrush,
                        TextWrapping = TextWrapping.Wrap
                    },
                    closeButton
                }
            }
        };

        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            await dialog.ShowDialog(owner);
        }
        else
        {
            dialog.Show();
        }
    }

    // ###########################################################################################
    // Loads the KiCad JSON file that resides next to the currently selected board Excel file.
    // Clears all KiCad runtime caches so the next render uses fresh project data.
    // ###########################################################################################
    public async Task LoadKiCadProjectForCurrentBoardAsync()
    {
        string jsonPath = this.GetKiCadProjectJsonPathForCurrentBoard();

        this.thisKiCadProject = null;
        this.thisKiCadPcbNetRenderCacheByKey.Clear();
        this.thisSelectedKiCadReferences.Clear();
        this.thisSelectedKiCadNormalizedNetNames.Clear();
        this.thisLockedKiCadNetNames.Clear();
        this.thisHoveredKiCadNetName = null;
        this.thisHoveredKiCadPadNumber = null;
        this.thisIsKiCadOverlayRefreshQueued = false;
        this.thisKiCadOverlayRefreshRequestVersion = 0;
        this.thisKiCadOverlayLastRenderedVersion = 0;
        this.thisLastKiCadNetConnectionsSignature = string.Empty;
        this.thisLastThumbnailHighlightSignature = string.Empty;
        this.ResetKiCadHoverHitTestThrottle();
        this.ClearKiCadOverlay();

        this.RestoreBoardSettings(this.MainWindow?.GetCurrentBoardKey() ?? string.Empty);

        if (string.IsNullOrWhiteSpace(jsonPath) || !System.IO.File.Exists(jsonPath))
        {
            return;
        }

        this.thisKiCadProject = await KiCadProjectLoader.LoadAsync(jsonPath);

        if (this.thisKiCadProject != null)
        {
            Logger.Info($"KiCad overlay data is available for current board: [{jsonPath}]");
        }

        this.RestoreBoardSettings(this.MainWindow?.GetCurrentBoardKey() ?? string.Empty);
        this.RefreshKiCadOverlay();
    }

    // ###########################################################################################
    // Resolves the KiCad JSON path beside the current board Excel file.
    // ###########################################################################################
    private string GetKiCadProjectJsonPathForCurrentBoard()
    {
        string excelPath = this.MainWindow?.GetCurrentBoardExcelPath() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(excelPath))
        {
            return string.Empty;
        }

        string? directory = System.IO.Path.GetDirectoryName(excelPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return string.Empty;
        }

        return System.IO.Path.Combine(directory, "KiCad-traces.json");
    }

    // ###########################################################################################
    // Updates the active KiCad selection from the currently selected board-label references and
    // derives the corresponding normalized PCB net names from footprint pad assignments.
    // ###########################################################################################
    private void UpdateKiCadSelectionFromBoardLabels(IEnumerable<string> boardLabels)
    {
        this.thisSelectedKiCadReferences.Clear();
        this.thisSelectedKiCadNormalizedNetNames.Clear();

        foreach (string boardLabel in boardLabels
                     .Where(label => !string.IsNullOrWhiteSpace(label))
                     .Select(label => label.Trim()))
        {
            this.thisSelectedKiCadReferences.Add(boardLabel);
        }

        foreach (string netName in this.BuildKiCadNormalizedNetNamesForReferences(this.thisSelectedKiCadReferences))
        {
            this.thisSelectedKiCadNormalizedNetNames.Add(netName);
        }

        this.RefreshKiCadOverlay();
    }

    // ###########################################################################################
    // Derives normalized PCB net names from the selected footprint references by reading the net
    // assignments on the pads of matching footprints.
    // ###########################################################################################
    private HashSet<string> BuildKiCadNormalizedNetNamesForReferences(IEnumerable<string> references)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var root = this.thisKiCadProject?.Root;
        if (root == null || root.Pcb.Count == 0)
        {
            return result;
        }

        var referenceSet = new HashSet<string>(
            references.Where(reference => !string.IsNullOrWhiteSpace(reference)),
            StringComparer.OrdinalIgnoreCase);

        if (referenceSet.Count == 0)
        {
            return result;
        }

        var pcb = root.Pcb[0];

        foreach (var footprint in pcb.Footprints)
        {
            string reference = footprint.Reference?.Trim() ?? string.Empty;
            if (!referenceSet.Contains(reference))
            {
                continue;
            }

            foreach (var pad in footprint.Pads)
            {
                string normalizedName = pad.Net?.NormalizedName?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(normalizedName))
                {
                    result.Add(normalizedName);
                }
            }
        }

        return result;
    }

    // ###########################################################################################
    // Clears the imported KiCad overlay geometry.
    // ###########################################################################################
    private void ClearKiCadOverlay()
    {
        this.SchematicsKiCadOverlayCanvas.ClearGeometry();
    }

    // ###########################################################################################
    // Resolves the active Excel/image schematic selection to the corresponding KiCad project view.
    // Top and bottom replica pages are matched explicitly, while schematic pages are matched by
    // exact display name first and by page ordinal second.
    // ###########################################################################################
    private KiCadProjectView? ResolveKiCadViewForCurrentSchematic()
    {
        return this.ResolveKiCadViewForSchematicName(this.GetCurrentSchematicName());
    }

    // ###########################################################################################
    // Extracts the ordinal page number from names such as "Schematics #1 of 2".
    // ###########################################################################################
    private static bool TryExtractSchematicPageOrdinal(string schematicName, out int pageOrdinal)
    {
        pageOrdinal = 0;

        int hashIndex = schematicName.IndexOf('#');
        int ofIndex = schematicName.IndexOf(" of ", StringComparison.OrdinalIgnoreCase);

        if (hashIndex < 0 || ofIndex <= hashIndex)
        {
            return false;
        }

        string digits = new string(schematicName[(hashIndex + 1)..ofIndex].Where(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out pageOrdinal) &&
               pageOrdinal > 0;
    }

    private class KiCadGraphNode
    {
        public string Id { get; set; } = string.Empty;
        public bool IsTargetPad { get; set; }
        public bool IsForeignPad { get; set; }
        public int SegmentIndex { get; set; } = -1;
        public int ViaIndex { get; set; } = -1;
        public int ArcIndex { get; set; } = -1;
        public KiCadPcbHighlightPadRef? PadRef { get; set; }
    }

    // ###########################################################################################
    // Renders PCB copper geometry for the currently selected normalized net names.
    // Uses cached per-net graph topology so adjacency building is not repeated on every refresh.
    // Also renders a pin-1-only marker for selected or hovered components when enabled.
    // ###########################################################################################
    private void RenderKiCadPcbGeometry(KiCadProjectView view)
    {
        var root = this.thisKiCadProject?.Root;
        if (root == null ||
            view.SourceIndex < 0 ||
            view.SourceIndex >= root.Pcb.Count)
        {
            return;
        }

        var pcb = root.Pcb[view.SourceIndex];
        var contentRect = this.GetImageContentRect();
        var worldBounds = this.GetKiCadPcbWorldBounds(pcb);

        if (contentRect.Width <= 0 ||
            contentRect.Height <= 0 ||
            worldBounds.Width <= 0 ||
            worldBounds.Height <= 0)
        {
            return;
        }

        string currentSchematicName = this.GetCurrentSchematicName();
        var calibration = this.GetKiCadViewCalibration(currentSchematicName);

        Color overlayColor = Colors.DeepSkyBlue;
        double baseOpacity = 0.20;
        if (this.schematicByName.TryGetValue(currentSchematicName, out var schematicEntry))
        {
            overlayColor = ParseColorOrDefault(schematicEntry.MainImageHighlightColor, Colors.DeepSkyBlue);
            baseOpacity = ParseOpacityOrDefault(schematicEntry.MainHighlightOpacity, 0.20);
        }

        double translatedOpacity = Math.Clamp(baseOpacity + 0.25, 0.0, 1.0);
        string? activeHoveredKiCadNetName = this.GetActiveHoveredKiCadNetName();

        var activeNets = new HashSet<string>(this.thisSelectedKiCadNormalizedNetNames, StringComparer.OrdinalIgnoreCase);
        foreach (var locked in this.thisLockedKiCadNetNames)
        {
            activeNets.Add(locked);
        }

        if (!string.IsNullOrWhiteSpace(activeHoveredKiCadNetName))
        {
            activeNets.Add(activeHoveredKiCadNetName);
        }

        var matchingNetIds = pcb.Nets.List
            .Where(net => !string.IsNullOrWhiteSpace(net.NormalizedName) &&
                          activeNets.Contains(net.NormalizedName.Trim()) &&
                          net.Id.HasValue)
            .Select(net => new { Id = net.Id!.Value, Name = net.NormalizedName!.Trim() })
            .Distinct()
            .ToList();

        string requiredLayer = string.Equals(view.Type, "pcb_bottom", StringComparison.OrdinalIgnoreCase)
            ? "B.Cu"
            : "F.Cu";

        var primitives = new List<KiCadOverlayPrimitive>();

        void AddPadPrimitive(KiCadPcbPad pad, IBrush padBrush)
        {
            if (pad.AbsoluteCenter == null)
            {
                return;
            }

            Point center = this.MapKiCadWorldToLocal(
                pad.AbsoluteCenter.X,
                pad.AbsoluteCenter.Y,
                worldBounds,
                contentRect,
                calibration);

            double width = this.MapKiCadWorldLengthToLocal(
                pad.Size?.X ?? 1.2,
                worldBounds,
                contentRect,
                calibration);

            double height = this.MapKiCadWorldLengthToLocal(
                pad.Size?.Y ?? 1.2,
                worldBounds,
                contentRect,
                calibration);

            var rect = new Rect(
                center.X - (Math.Max(2.0, width) / 2.0),
                center.Y - (Math.Max(2.0, height) / 2.0),
                Math.Max(2.0, width),
                Math.Max(2.0, height));

            var pen = new Pen(padBrush, 1.2);

            if (string.Equals(pad.Shape?.Trim(), "rect", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pad.Shape?.Trim(), "roundrect", StringComparison.OrdinalIgnoreCase))
            {
                primitives.Add(new KiCadOverlayPrimitive
                {
                    Kind = KiCadOverlayPrimitiveKind.Rectangle,
                    Rect = rect,
                    Pen = pen,
                    Fill = padBrush
                });
            }
            else
            {
                primitives.Add(new KiCadOverlayPrimitive
                {
                    Kind = KiCadOverlayPrimitiveKind.Ellipse,
                    Rect = rect,
                    Pen = pen,
                    Fill = padBrush
                });
            }
        }

        foreach (var netInfo in matchingNetIds)
        {
            if (!pcb.HighlightIndex.TryGetValue(netInfo.Id.ToString(CultureInfo.InvariantCulture), out var bucket))
            {
                continue;
            }

            bool isHoveredNet = string.Equals(activeHoveredKiCadNetName, netInfo.Name, StringComparison.OrdinalIgnoreCase);
            bool isLockedNet = this.thisLockedKiCadNetNames.Contains(netInfo.Name);
            bool isExplicitHighlight = isLockedNet || isHoveredNet;

            bool isSelectionDerivedNet = this.thisSelectedKiCadNormalizedNetNames.Contains(netInfo.Name);
            bool shouldBlinkThisNet = isLockedNet || isSelectionDerivedNet;

            double blinkFactor = shouldBlinkThisNet ? this.thisCurrentHighlightBlinkFactor : 1.0;
            double effectiveOpacity = Math.Clamp(translatedOpacity * blinkFactor, 0.0, 1.0);

            IBrush strokeBrush = isHoveredNet && !shouldBlinkThisNet
                ? new SolidColorBrush(overlayColor, 1.0)
                : new SolidColorBrush(overlayColor, effectiveOpacity);

            IBrush fillBrush = strokeBrush;

            var cache = this.GetOrCreateKiCadPcbNetRenderCache(
                pcb,
                view.SourceIndex,
                netInfo.Id,
                bucket,
                requiredLayer);

            var activeDrawIds = this.BuildKiCadPcbActiveDrawIds(cache, isExplicitHighlight);

            foreach (var segmentNode in cache.SegmentNodes)
            {
                if (!activeDrawIds.Contains(segmentNode.Info.Id))
                {
                    continue;
                }

                Point start = this.MapKiCadWorldToLocal(
                    segmentNode.StartWorld.X,
                    segmentNode.StartWorld.Y,
                    worldBounds,
                    contentRect,
                    calibration);

                Point end = this.MapKiCadWorldToLocal(
                    segmentNode.EndWorld.X,
                    segmentNode.EndWorld.Y,
                    worldBounds,
                    contentRect,
                    calibration);

                double thickness = this.MapKiCadWorldLengthToLocal(
                    segmentNode.WidthWorld,
                    worldBounds,
                    contentRect,
                    calibration);

                primitives.Add(new KiCadOverlayPrimitive
                {
                    Kind = KiCadOverlayPrimitiveKind.Line,
                    Start = start,
                    End = end,
                    Pen = new Pen(strokeBrush, Math.Max(1.0, thickness - 1.0))
                });
            }

            foreach (var viaNode in cache.ViaNodes)
            {
                if (!activeDrawIds.Contains(viaNode.Info.Id))
                {
                    continue;
                }

                Point center = this.MapKiCadWorldToLocal(
                    viaNode.CenterWorld.X,
                    viaNode.CenterWorld.Y,
                    worldBounds,
                    contentRect,
                    calibration);

                double diameter = this.MapKiCadWorldLengthToLocal(
                    viaNode.DiameterWorld,
                    worldBounds,
                    contentRect,
                    calibration);

                primitives.Add(new KiCadOverlayPrimitive
                {
                    Kind = KiCadOverlayPrimitiveKind.Ellipse,
                    Rect = new Rect(
                        center.X - (Math.Max(2.0, diameter) / 2.0),
                        center.Y - (Math.Max(2.0, diameter) / 2.0),
                        Math.Max(2.0, diameter),
                        Math.Max(2.0, diameter)),
                    Pen = new Pen(strokeBrush, 1.2),
                    Fill = fillBrush
                });
            }

            foreach (var arcNode in cache.ArcNodes)
            {
                if (!activeDrawIds.Contains(arcNode.Info.Id))
                {
                    continue;
                }

                Point start = this.MapKiCadWorldToLocal(
                    arcNode.StartWorld.X,
                    arcNode.StartWorld.Y,
                    worldBounds,
                    contentRect,
                    calibration);

                Point mid = this.MapKiCadWorldToLocal(
                    arcNode.MidWorld.X,
                    arcNode.MidWorld.Y,
                    worldBounds,
                    contentRect,
                    calibration);

                Point end = this.MapKiCadWorldToLocal(
                    arcNode.EndWorld.X,
                    arcNode.EndWorld.Y,
                    worldBounds,
                    contentRect,
                    calibration);

                double thickness = this.MapKiCadWorldLengthToLocal(
                    arcNode.WidthWorld,
                    worldBounds,
                    contentRect,
                    calibration);

                primitives.Add(new KiCadOverlayPrimitive
                {
                    Kind = KiCadOverlayPrimitiveKind.Polyline,
                    Points = this.SampleQuadraticBezier(start, mid, end, 20),
                    Pen = new Pen(strokeBrush, Math.Max(1.0, thickness - 1.0))
                });
            }

            foreach (var padNode in cache.PadNodes)
            {
                if (!activeDrawIds.Contains(padNode.Info.Id))
                {
                    continue;
                }

                bool isSelectedComponentPin1 = this.ShouldUseSelectedComponentPin1Highlight(
                    padNode.Footprint,
                    padNode.Pad,
                    requiredLayer);

                IBrush padBrush = isSelectedComponentPin1
                    ? new SolidColorBrush(Colors.Orange, 1.0)
                    : fillBrush;

                AddPadPrimitive(padNode.Pad, padBrush);
            }
        }

        if (this.HasPin1HighlightTargetReference())
        {
            foreach (var footprint in pcb.Footprints)
            {
                string reference = footprint.Reference?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(reference))
                {
                    continue;
                }

                if (!this.thisSelectedKiCadReferences.Contains(reference) &&
                    !string.Equals(this.thisHoveredComponentBoardLabel, reference, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var pad in footprint.Pads)
                {
                    if (pad.AbsoluteCenter == null ||
                        !TabSchematics.IsKiCadPcbPointVisibleOnSide(pad.Layers, requiredLayer) ||
                        !TabSchematics.IsPrimaryPadForPin1Highlight(footprint, pad, requiredLayer))
                    {
                        continue;
                    }

                    AddPadPrimitive(pad, new SolidColorBrush(Colors.Orange, 1.0));
                }
            }
        }

        this.SchematicsKiCadOverlayCanvas.SetGeometry(primitives);
    }

    // ###########################################################################################
    // Renders resolved schematic wire paths for the currently selected normalized net names.
    // Uses a render-only overlay control instead of creating one Polyline control per path.
    // ###########################################################################################
    private void RenderKiCadSchematicGeometry(KiCadProjectView view)
    {
        var bundle = this.thisKiCadProject;
        if (bundle == null ||
            view.SourceIndex < 0 ||
            view.SourceIndex >= bundle.Root.Schematics.Count)
        {
            return;
        }

        if (!bundle.SchematicNetPathIndexBySchematicIndex.TryGetValue(view.SourceIndex, out var indexByNet))
        {
            return;
        }

        var schematic = bundle.Root.Schematics[view.SourceIndex];
        var contentRect = this.GetImageContentRect();
        var worldBounds = this.GetKiCadSchematicWorldBounds(schematic);

        if (contentRect.Width <= 0 ||
            contentRect.Height <= 0 ||
            worldBounds.Width <= 0 ||
            worldBounds.Height <= 0)
        {
            return;
        }

        string currentSchematicName = this.GetCurrentSchematicName();
        var calibration = this.GetKiCadViewCalibration(currentSchematicName);

        Color overlayColor = Colors.Orange;
        double baseOpacity = 0.20;
        if (this.schematicByName.TryGetValue(currentSchematicName, out var schematicEntry))
        {
            overlayColor = ParseColorOrDefault(schematicEntry.MainImageHighlightColor, Colors.Orange);
            baseOpacity = ParseOpacityOrDefault(schematicEntry.MainHighlightOpacity, 0.20);
        }

        double translatedOpacity = Math.Clamp(baseOpacity + 0.25, 0.0, 1.0);
        string? activeHoveredKiCadNetName = this.GetActiveHoveredKiCadNetName();

        var activeNets = new HashSet<string>(this.thisSelectedKiCadNormalizedNetNames, StringComparer.OrdinalIgnoreCase);
        foreach (var locked in this.thisLockedKiCadNetNames)
        {
            activeNets.Add(locked);
        }

        if (!string.IsNullOrWhiteSpace(activeHoveredKiCadNetName))
        {
            activeNets.Add(activeHoveredKiCadNetName);
        }

        var primitives = new List<KiCadOverlayPrimitive>();

        foreach (string normalizedNetName in activeNets)
        {
            if (!indexByNet.TryGetValue(normalizedNetName, out var resolvedPaths))
            {
                continue;
            }

            bool isHoveredNet = string.Equals(activeHoveredKiCadNetName, normalizedNetName, StringComparison.OrdinalIgnoreCase);
            bool isLockedNet = this.thisLockedKiCadNetNames.Contains(normalizedNetName);

            bool isSelectionDerivedNet = this.thisSelectedKiCadNormalizedNetNames.Contains(normalizedNetName);
            bool shouldBlinkThisNet = isLockedNet || isSelectionDerivedNet;

            double blinkFactor = shouldBlinkThisNet ? this.thisCurrentHighlightBlinkFactor : 1.0;
            double effectiveOpacity = Math.Clamp(translatedOpacity * blinkFactor, 0.0, 1.0);

            IBrush strokeBrush = isHoveredNet && !shouldBlinkThisNet
                ? new SolidColorBrush(overlayColor, 1.0)
                : new SolidColorBrush(overlayColor, effectiveOpacity);

            var pen = new Pen(strokeBrush, 1.2);

            foreach (var resolvedPath in resolvedPaths)
            {
                if (resolvedPath.Points.Count < 2)
                {
                    continue;
                }

                var localPoints = resolvedPath.Points
                    .Select(point => this.MapKiCadWorldToLocal(
                        point.X,
                        point.Y,
                        worldBounds,
                        contentRect,
                        calibration))
                    .ToList();

                primitives.Add(new KiCadOverlayPrimitive
                {
                    Kind = KiCadOverlayPrimitiveKind.Polyline,
                    Points = localPoints,
                    Pen = pen
                });
            }
        }

        this.SchematicsKiCadOverlayCanvas.SetGeometry(primitives);
    }

    // ###########################################################################################
    // Returns the calibration object for the current schematic.
    // Models rigid 2-point orthogonal coordinate tracking for non-rotated exported replica images.
    // ###########################################################################################
    private KiCadViewCalibration GetKiCadViewCalibration(string schematicName)
    {
        if (string.IsNullOrWhiteSpace(schematicName) ||
            !this.schematicByName.TryGetValue(schematicName, out var schematicEntry))
        {
            return KiCadViewCalibration.Identity;
        }

        bool hasP1 = TabSchematics.TryParseCalibrationPoint(
            schematicEntry.KiCadP1WorldX, schematicEntry.KiCadP1WorldY, schematicEntry.KiCadP1ImageX, schematicEntry.KiCadP1ImageY, out var p1);

        bool hasP2 = TabSchematics.TryParseCalibrationPoint(
            schematicEntry.KiCadP2WorldX, schematicEntry.KiCadP2WorldY, schematicEntry.KiCadP2ImageX, schematicEntry.KiCadP2ImageY, out var p2);

        if (hasP1 && hasP2 &&
            TabSchematics.TryBuildOrthogonalCalibration(p1, p2, out var orthogonalCalibration))
        {
            return orthogonalCalibration;
        }

        return KiCadViewCalibration.Identity;
    }

    // ###########################################################################################
    // Maps one KiCad world-space point into the local image coordinate system currently used by
    // the schematics image and overlays.
    // Prefers affine calibration when present and falls back to the older normalized mapping.
    // ###########################################################################################
    private Point MapKiCadWorldToLocal(
        double worldX,
        double worldY,
        Rect worldBounds,
        Rect contentRect,
        KiCadViewCalibration calibration)
    {
        if (calibration.HasAffineCalibration &&
            this.currentFullResBitmap != null &&
            this.currentFullResBitmap.PixelSize.Width > 0 &&
            this.currentFullResBitmap.PixelSize.Height > 0)
        {
            double bitmapX = (calibration.A * worldX) + (calibration.B * worldY) + calibration.C;
            double bitmapY = (calibration.D * worldX) + (calibration.E * worldY) + calibration.F;

            double localX = contentRect.X + ((bitmapX / this.currentFullResBitmap.PixelSize.Width) * contentRect.Width);
            double localY = contentRect.Y + ((bitmapY / this.currentFullResBitmap.PixelSize.Height) * contentRect.Height);

            return new Point(localX, localY);
        }

        if (worldBounds.Width <= 0 || worldBounds.Height <= 0)
        {
            return new Point(contentRect.X, contentRect.Y);
        }

        double nx = (worldX - worldBounds.X) / worldBounds.Width;
        double ny = (worldY - worldBounds.Y) / worldBounds.Height;

        if (calibration.MirrorX)
        {
            nx = 1.0 - nx;
        }

        if (calibration.MirrorY)
        {
            ny = 1.0 - ny;
        }

        nx *= calibration.ScaleX;
        ny *= calibration.ScaleY;

        double localXFallback = contentRect.X + (nx * contentRect.Width);
        double localYFallback = contentRect.Y + (ny * contentRect.Height);

        if (this.currentFullResBitmap != null)
        {
            if (this.currentFullResBitmap.PixelSize.Width > 0)
            {
                localXFallback += calibration.OffsetX * (contentRect.Width / this.currentFullResBitmap.PixelSize.Width);
            }

            if (this.currentFullResBitmap.PixelSize.Height > 0)
            {
                localYFallback += calibration.OffsetY * (contentRect.Height / this.currentFullResBitmap.PixelSize.Height);
            }
        }
        else
        {
            localXFallback += calibration.OffsetX;
            localYFallback += calibration.OffsetY;
        }

        return new Point(localXFallback, localYFallback);
    }

    // ###########################################################################################
    // Converts one KiCad world-space length into the current local overlay coordinate space.
    // Uses affine basis-vector scaling when affine calibration exists.
    // ###########################################################################################
    private double MapKiCadWorldLengthToLocal(
        double worldLength,
        Rect worldBounds,
        Rect contentRect,
        KiCadViewCalibration calibration)
    {
        if (calibration.HasAffineCalibration &&
            this.currentFullResBitmap != null &&
            this.currentFullResBitmap.PixelSize.Width > 0 &&
            this.currentFullResBitmap.PixelSize.Height > 0)
        {
            double bitmapToLocalX = contentRect.Width / this.currentFullResBitmap.PixelSize.Width;
            double bitmapToLocalY = contentRect.Height / this.currentFullResBitmap.PixelSize.Height;

            double localUnitX = Math.Sqrt(
                Math.Pow(calibration.A * bitmapToLocalX, 2.0) +
                Math.Pow(calibration.D * bitmapToLocalY, 2.0));

            double localUnitY = Math.Sqrt(
                Math.Pow(calibration.B * bitmapToLocalX, 2.0) +
                Math.Pow(calibration.E * bitmapToLocalY, 2.0));

            double averageScale = (localUnitX + localUnitY) / 2.0;
            return worldLength * averageScale;
        }

        double sx = contentRect.Width / Math.Max(0.0001, worldBounds.Width);
        double sy = contentRect.Height / Math.Max(0.0001, worldBounds.Height);

        sx *= Math.Abs(calibration.ScaleX);
        sy *= Math.Abs(calibration.ScaleY);

        return worldLength * ((sx + sy) / 2.0);
    }

    // ###########################################################################################
    // Computes a world bounding box for all PCB geometry used by the MVP overlay.
    // ###########################################################################################
    private Rect GetKiCadPcbWorldBounds(KiCadPcb pcb)
    {
        bool hasValue = false;
        double minX = 0;
        double minY = 0;
        double maxX = 0;
        double maxY = 0;

        void Include(double x, double y)
        {
            if (!hasValue)
            {
                minX = maxX = x;
                minY = maxY = y;
                hasValue = true;
                return;
            }

            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        foreach (var segment in pcb.Routing.Segments)
        {
            if (segment.Start != null) Include(segment.Start.X, segment.Start.Y);
            if (segment.End != null) Include(segment.End.X, segment.End.Y);
        }

        foreach (var via in pcb.Routing.Vias)
        {
            if (via.At != null) Include(via.At.X, via.At.Y);
        }

        foreach (var arc in pcb.Routing.Arcs)
        {
            if (arc.Start != null) Include(arc.Start.X, arc.Start.Y);
            if (arc.Mid != null) Include(arc.Mid.X, arc.Mid.Y);
            if (arc.End != null) Include(arc.End.X, arc.End.Y);
        }

        foreach (var footprint in pcb.Footprints)
        {
            foreach (var pad in footprint.Pads)
            {
                if (pad.AbsoluteCenter == null)
                {
                    continue;
                }

                double halfWidth = (pad.Size?.X ?? 0.0) / 2.0;
                double halfHeight = (pad.Size?.Y ?? 0.0) / 2.0;

                Include(pad.AbsoluteCenter.X - halfWidth, pad.AbsoluteCenter.Y - halfHeight);
                Include(pad.AbsoluteCenter.X + halfWidth, pad.AbsoluteCenter.Y + halfHeight);
            }
        }

        return hasValue
            ? new Rect(minX, minY, Math.Max(0.0001, maxX - minX), Math.Max(0.0001, maxY - minY))
            : default;
    }

    // ###########################################################################################
    // Computes a world bounding box for schematic wires, polylines, and net labels.
    // ###########################################################################################
    private Rect GetKiCadSchematicWorldBounds(KiCadSchematic schematic)
    {
        bool hasValue = false;
        double minX = 0;
        double minY = 0;
        double maxX = 0;
        double maxY = 0;

        void Include(double x, double y)
        {
            if (!hasValue)
            {
                minX = maxX = x;
                minY = maxY = y;
                hasValue = true;
                return;
            }

            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        foreach (var wire in schematic.Wires)
        {
            foreach (var point in wire.Points)
            {
                Include(point.X, point.Y);
            }
        }

        foreach (var polyline in schematic.Polylines)
        {
            foreach (var point in polyline.Points)
            {
                Include(point.X, point.Y);
            }
        }

        foreach (var label in schematic.Labels.Local)
        {
            if (label.At != null) Include(label.At.X, label.At.Y);
        }

        foreach (var label in schematic.Labels.Global)
        {
            if (label.At != null) Include(label.At.X, label.At.Y);
        }

        foreach (var label in schematic.Labels.Hierarchical)
        {
            if (label.At != null) Include(label.At.X, label.At.Y);
        }

        return hasValue
            ? new Rect(minX, minY, Math.Max(0.0001, maxX - minX), Math.Max(0.0001, maxY - minY))
            : default;
    }

    // ###########################################################################################
    // Samples one quadratic Bézier curve for PCB arc rendering.
    // ###########################################################################################
    private List<Point> SampleQuadraticBezier(Point start, Point control, Point end, int steps)
    {
        var points = new List<Point>(Math.Max(2, steps + 1));

        for (int i = 0; i <= steps; i++)
        {
            double t = (double)i / steps;
            double mt = 1.0 - t;

            double x = (mt * mt * start.X) + (2.0 * mt * t * control.X) + (t * t * end.X);
            double y = (mt * mt * start.Y) + (2.0 * mt * t * control.Y) + (t * t * end.Y);

            points.Add(new Point(x, y));
        }

        return points;
    }

    // ###########################################################################################
    // Parses one 3-point calibration row from Excel text values.
    // ###########################################################################################
    private static bool TryParseCalibrationPoint(
        string worldXText,
        string worldYText,
        string imageXText,
        string imageYText,
        out KiCadCalibrationPoint point)
    {
        point = default;

        if (!TabSchematics.TryParseDouble(worldXText, out double worldX) ||
            !TabSchematics.TryParseDouble(worldYText, out double worldY) ||
            !TabSchematics.TryParseDouble(imageXText, out double imageX) ||
            !TabSchematics.TryParseDouble(imageYText, out double imageY))
        {
            return false;
        }

        point = new KiCadCalibrationPoint(worldX, worldY, imageX, imageY);
        return true;
    }

    // ###########################################################################################
    // Converts a schematic container pointer position into bitmap pixel coordinates for the
    // currently displayed schematic image.
    // ###########################################################################################
    private bool TryGetSchematicsImagePixelPoint(Point pointerInContainer, out Point pixelPoint)
    {
        pixelPoint = default;

        if (this.currentFullResBitmap == null)
        {
            return false;
        }

        if (!TryInvert(this.schematicsMatrix, out var inv))
        {
            return false;
        }

        var localPoint = new Point(
            (pointerInContainer.X * inv.M11) + (pointerInContainer.Y * inv.M21) + inv.M31,
            (pointerInContainer.X * inv.M12) + (pointerInContainer.Y * inv.M22) + inv.M32);

        var contentRect = this.GetImageContentRect();
        if (contentRect.Width <= 0 || contentRect.Height <= 0 || !contentRect.Contains(localPoint))
        {
            return false;
        }

        double px = ((localPoint.X - contentRect.X) / contentRect.Width) * this.currentFullResBitmap.PixelSize.Width;
        double py = ((localPoint.Y - contentRect.Y) / contentRect.Height) * this.currentFullResBitmap.PixelSize.Height;

        pixelPoint = new Point(px, py);
        return true;
    }

    // ###########################################################################################
    // Enables image calibration capture for the current schematic.
    // ###########################################################################################
    private void BeginKiCadCalibrationCapture()
    {
        this.thisIsKiCadCalibrationCaptureMode = true;
        this.thisKiCadCalibrationImagePoints.Clear();

        this.HideLabelEditorMenu();
        this.SchematicsContainer.Focus();
        this.UpdateSchematicsHoverUi(new Point(0, 0));

        Logger.Info($"Image calibration capture started for schematic [{this.GetCurrentSchematicName()}]");
    }

    // ###########################################################################################
    // Cancels the current image calibration capture workflow.
    // ###########################################################################################
    private void CancelKiCadCalibrationCapture()
    {
        this.thisIsKiCadCalibrationCaptureMode = false;
        this.thisKiCadCalibrationImagePoints.Clear();

        this.HideSchematicsHoverUi();
        this.HideLabelEditorMenu();
        this.SchematicsContainer.Focus();

        Logger.Info("Image calibration capture canceled");
    }

    // ###########################################################################################
    // Captures one image-space calibration point and copies the X and Y coordinates to the clipboard.
    // ###########################################################################################
    private async void CaptureKiCadCalibrationPointAsync(Point pointerInContainer)
    {
        if (!this.thisIsKiCadCalibrationCaptureMode)
        {
            return;
        }

        if (!this.TryGetSchematicsImagePixelPoint(pointerInContainer, out var pixelPoint))
        {
            return;
        }

        this.thisKiCadCalibrationImagePoints.Clear();
        this.thisKiCadCalibrationImagePoints.Add(pixelPoint);

        Logger.Info(
            $"Image calibration point captured for schematic [{this.GetCurrentSchematicName()}] -> ImageX=[{pixelPoint.X.ToString("0.##", CultureInfo.InvariantCulture)}] ImageY=[{pixelPoint.Y.ToString("0.##", CultureInfo.InvariantCulture)}]");

        string xText = pixelPoint.X.ToString("0.######", CultureInfo.InvariantCulture);
        string yText = pixelPoint.Y.ToString("0.######", CultureInfo.InvariantCulture);
        string clipboardText = $"'{xText}\t'{yText}";

        if (TopLevel.GetTopLevel(this) is TopLevel topLevel && topLevel.Clipboard != null)
        {
            await ClipboardExtensions.SetTextAsync(topLevel.Clipboard, clipboardText);
        }

        this.thisIsKiCadCalibrationCaptureMode = false;
        this.SchematicsContainer.Cursor = Cursor.Default;
        this.SchematicsHoverLabelText.Text = "Calibration values copied to clipboard";
        this.SchematicsHoverLabelBorder.IsVisible = true;

        Logger.Info($"Image calibration capture completed for schematic [{this.GetCurrentSchematicName()}]");
    }

    // ###########################################################################################
    // Adds one KiCad calibration world-point candidate if the label/coordinate combination was
    // not already added to the current candidate list.
    // ###########################################################################################
    private static void AddKiCadCalibrationWorldPointCandidate(
        List<KiCadCalibrationWorldPointCandidate> candidates,
        HashSet<string> seen,
        string label,
        double worldX,
        double worldY)
    {
        string normalizedLabel = label?.Trim() ?? string.Empty;
        string key =
            $"{Math.Round(worldX, 6).ToString(CultureInfo.InvariantCulture)}|" +
            $"{Math.Round(worldY, 6).ToString(CultureInfo.InvariantCulture)}|" +
            normalizedLabel;

        if (!seen.Add(key))
        {
            return;
        }

        candidates.Add(new KiCadCalibrationWorldPointCandidate
        {
            Label = normalizedLabel,
            WorldX = worldX,
            WorldY = worldY
        });
    }

    // ###########################################################################################
    // Builds candidate KiCad world points for the currently selected schematic or PCB view.
    // PCB candidates are filtered to selected references when a component selection exists.
    // ###########################################################################################
    private List<KiCadCalibrationWorldPointCandidate> BuildCurrentKiCadCalibrationWorldPointCandidates()
    {
        var bundle = this.thisKiCadProject;
        var currentView = this.ResolveKiCadViewForCurrentSchematic();

        if (bundle == null || currentView == null)
        {
            return new List<KiCadCalibrationWorldPointCandidate>();
        }

        if (string.Equals(currentView.Type, "pcb_top", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(currentView.Type, "pcb_bottom", StringComparison.OrdinalIgnoreCase))
        {
            if (currentView.SourceIndex < 0 || currentView.SourceIndex >= bundle.Root.Pcb.Count)
            {
                return new List<KiCadCalibrationWorldPointCandidate>();
            }

            string requiredLayer = string.Equals(currentView.Type, "pcb_bottom", StringComparison.OrdinalIgnoreCase)
                ? "B.Cu"
                : "F.Cu";

            return this.BuildPcbCalibrationWorldPointCandidates(bundle.Root.Pcb[currentView.SourceIndex], requiredLayer);
        }

        if (string.Equals(currentView.Type, "schematic", StringComparison.OrdinalIgnoreCase))
        {
            return this.BuildSchematicCalibrationWorldPointCandidates(bundle, currentView);
        }

        return new List<KiCadCalibrationWorldPointCandidate>();
    }

    // ###########################################################################################
    // Builds candidate KiCad world points for one PCB view using exact pad centers, via centers,
    // segment endpoints, arc control points, and board-corner landmarks.
    // Always includes pads for all visible footprints on the current PCB side.
    // ###########################################################################################
    private List<KiCadCalibrationWorldPointCandidate> BuildPcbCalibrationWorldPointCandidates(KiCadPcb pcb, string requiredLayer)
    {
        var candidates = new List<KiCadCalibrationWorldPointCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenCoordinates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        bool hasReferenceFilter = this.thisSelectedKiCadReferences.Count > 0;

        void AddExactCandidate(string label, double worldX, double worldY)
        {
            string coordinateKey =
                $"{Math.Round(worldX, 4).ToString(CultureInfo.InvariantCulture)}|" +
                $"{Math.Round(worldY, 4).ToString(CultureInfo.InvariantCulture)}";

            if (!seenCoordinates.Add(coordinateKey))
            {
                return;
            }

            TabSchematics.AddKiCadCalibrationWorldPointCandidate(
                candidates,
                seen,
                label,
                worldX,
                worldY);
        }

        bool IsMatchingSelectedNet(KiCadNetRef? net)
        {
            if (!hasReferenceFilter)
            {
                return true;
            }

            string normalizedName = net?.NormalizedName?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(normalizedName) &&
                   this.thisSelectedKiCadNormalizedNetNames.Contains(normalizedName);
        }

        string BuildNetSuffix(KiCadNetRef? net)
        {
            string normalizedName = net?.NormalizedName?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(normalizedName)
                ? string.Empty
                : $" [{normalizedName}]";
        }

        foreach (var footprint in pcb.Footprints
                     .OrderBy(footprint => footprint.Reference?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            string reference = footprint.Reference?.Trim() ?? string.Empty;

            var visiblePads = footprint.Pads
                .Where(pad => pad.AbsoluteCenter != null)
                .Where(pad => TabSchematics.IsKiCadPcbPointVisibleOnSide(pad.Layers, requiredLayer))
                .OrderBy(pad => pad.Number?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var pad in visiblePads)
            {
                string padNumber = pad.Number?.Trim() ?? "?";
                string label = string.IsNullOrWhiteSpace(reference)
                    ? $"Pad {padNumber}"
                    : $"{reference} pad {padNumber}";

                AddExactCandidate(label, pad.AbsoluteCenter!.X, pad.AbsoluteCenter.Y);
            }
        }

        for (int i = 0; i < pcb.Routing.Vias.Count; i++)
        {
            var via = pcb.Routing.Vias[i];
            if (via.At == null)
            {
                continue;
            }

            if (!TabSchematics.IsKiCadPcbPointVisibleOnSide(via.Layers, requiredLayer))
            {
                continue;
            }

            if (!IsMatchingSelectedNet(via.Net))
            {
                continue;
            }

            AddExactCandidate(
                $"Via {i + 1:000}{BuildNetSuffix(via.Net)}",
                via.At.X,
                via.At.Y);
        }

        for (int i = 0; i < pcb.Routing.Segments.Count; i++)
        {
            var segment = pcb.Routing.Segments[i];
            if (segment.Start == null || segment.End == null)
            {
                continue;
            }

            if (!string.Equals(segment.Layer?.Trim(), requiredLayer, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!IsMatchingSelectedNet(segment.Net))
            {
                continue;
            }

            string suffix = BuildNetSuffix(segment.Net);

            AddExactCandidate($"Segment {i + 1:000} start{suffix}", segment.Start.X, segment.Start.Y);
            AddExactCandidate($"Segment {i + 1:000} end{suffix}", segment.End.X, segment.End.Y);
        }

        for (int i = 0; i < pcb.Routing.Arcs.Count; i++)
        {
            var arc = pcb.Routing.Arcs[i];
            if (arc.Start == null || arc.Mid == null || arc.End == null)
            {
                continue;
            }

            if (!string.Equals(arc.Layer?.Trim(), requiredLayer, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!IsMatchingSelectedNet(arc.Net))
            {
                continue;
            }

            string suffix = BuildNetSuffix(arc.Net);

            AddExactCandidate($"Arc {i + 1:000} start{suffix}", arc.Start.X, arc.Start.Y);
            AddExactCandidate($"Arc {i + 1:000} mid{suffix}", arc.Mid.X, arc.Mid.Y);
            AddExactCandidate($"Arc {i + 1:000} end{suffix}", arc.End.X, arc.End.Y);
        }

        if (!hasReferenceFilter)
        {
            Rect bounds = this.GetKiCadPcbWorldBounds(pcb);
            if (bounds.Width > 0 && bounds.Height > 0)
            {
                AddExactCandidate("PCB bounds top-left", bounds.Left, bounds.Top);
                AddExactCandidate("PCB bounds top-right", bounds.Right, bounds.Top);
                AddExactCandidate("PCB bounds bottom-left", bounds.Left, bounds.Bottom);
                AddExactCandidate("PCB bounds bottom-right", bounds.Right, bounds.Bottom);
            }
        }

        return candidates
            .OrderBy(candidate => candidate.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.WorldX)
            .ThenBy(candidate => candidate.WorldY)
            .ToList();
    }

    // ###########################################################################################
    // Builds candidate KiCad world points for the currently selected schematic or PCB view.
    // PCB views always include component pads for all visible footprints.
    // ###########################################################################################
    private List<KiCadCalibrationWorldPointCandidate> BuildSchematicCalibrationWorldPointCandidates(
        KiCadProjectBundle bundle,
        KiCadProjectView view)
    {
        var candidates = new List<KiCadCalibrationWorldPointCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (view.SourceIndex < 0 || view.SourceIndex >= bundle.Root.Schematics.Count)
        {
            return candidates;
        }

        var schematic = bundle.Root.Schematics[view.SourceIndex];

        foreach (var symbol in schematic.Symbols)
        {
            string reference = symbol.Reference?.Trim() ?? string.Empty;
            string value = symbol.Value?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(reference) || TabSchematics.IsInternalKiCadSymbolReference(reference))
            {
                continue;
            }

            foreach (var property in symbol.PropertiesDetailed.Where(property =>
                         property.At != null &&
                         string.Equals(property.Name?.Trim(), "Reference", StringComparison.OrdinalIgnoreCase) &&
                         property.Effects?.Hide != true))
            {
                string anchorDescription = TabSchematics.DescribeKiCadTextAnchor(property.Effects);

                TabSchematics.AddKiCadCalibrationWorldPointCandidate(
                    candidates,
                    seen,
                    $"Component {reference} reference text ({anchorDescription})",
                    property.At!.X,
                    property.At.Y);
            }

            if (symbol.At != null)
            {
                TabSchematics.AddKiCadCalibrationWorldPointCandidate(
                    candidates,
                    seen,
                    $"Component {reference} symbol anchor",
                    symbol.At.X,
                    symbol.At.Y);
            }
        }

        foreach (var label in schematic.Labels.Local.Where(label => label.At != null))
        {
            string text = label.Text?.Trim() ?? "(local label)";
            TabSchematics.AddKiCadCalibrationWorldPointCandidate(
                candidates,
                seen,
                $"Local label {text}",
                label.At!.X,
                label.At.Y);
        }

        foreach (var label in schematic.Labels.Global.Where(label => label.At != null))
        {
            string text = label.Text?.Trim() ?? "(global label)";
            TabSchematics.AddKiCadCalibrationWorldPointCandidate(
                candidates,
                seen,
                $"Global label {text}",
                label.At!.X,
                label.At.Y);
        }

        foreach (var label in schematic.Labels.Hierarchical.Where(label => label.At != null))
        {
            string text = label.Text?.Trim() ?? "(hierarchical label)";
            TabSchematics.AddKiCadCalibrationWorldPointCandidate(
                candidates,
                seen,
                $"Hierarchical label {text}",
                label.At!.X,
                label.At.Y);
        }

        if (bundle.SchematicNetPathIndexBySchematicIndex.TryGetValue(view.SourceIndex, out var indexByNet))
        {
            IEnumerable<KeyValuePair<string, List<KiCadResolvedPath>>> pathsToUse =
                this.thisSelectedKiCadNormalizedNetNames.Count > 0
                    ? indexByNet
                        .Where(kvp => this.thisSelectedKiCadNormalizedNetNames.Contains(kvp.Key))
                        .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                    : indexByNet
                        .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
                        .Take(20);

            foreach (var kvp in pathsToUse)
            {
                int pathOrdinal = 1;

                foreach (var path in kvp.Value)
                {
                    if (path.Points.Count == 0)
                    {
                        pathOrdinal++;
                        continue;
                    }

                    var start = path.Points[0];
                    var end = path.Points[path.Points.Count - 1];

                    TabSchematics.AddKiCadCalibrationWorldPointCandidate(
                        candidates,
                        seen,
                        $"{kvp.Key} path {pathOrdinal} start",
                        start.X,
                        start.Y);

                    TabSchematics.AddKiCadCalibrationWorldPointCandidate(
                        candidates,
                        seen,
                        $"{kvp.Key} path {pathOrdinal} end",
                        end.X,
                        end.Y);

                    pathOrdinal++;
                }
            }
        }

        Rect bounds = this.GetKiCadSchematicWorldBounds(schematic);
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            TabSchematics.AddKiCadCalibrationWorldPointCandidate(candidates, seen, "Schematic bounds top-left", bounds.Left, bounds.Top);
            TabSchematics.AddKiCadCalibrationWorldPointCandidate(candidates, seen, "Schematic bounds top-right", bounds.Right, bounds.Top);
            TabSchematics.AddKiCadCalibrationWorldPointCandidate(candidates, seen, "Schematic bounds bottom-left", bounds.Left, bounds.Bottom);
            TabSchematics.AddKiCadCalibrationWorldPointCandidate(candidates, seen, "Schematic bounds bottom-right", bounds.Right, bounds.Bottom);
            TabSchematics.AddKiCadCalibrationWorldPointCandidate(candidates, seen, "Schematic bounds center", bounds.Center.X, bounds.Center.Y);
        }

        return candidates
            .OrderBy(candidate =>
            {
                if (candidate.Label.Contains("reference text", StringComparison.OrdinalIgnoreCase)) return 0;
                if (candidate.Label.Contains("symbol anchor", StringComparison.OrdinalIgnoreCase)) return 1;
                return 2;
            })
            .ThenBy(candidate => candidate.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ###########################################################################################
    // Returns true when a KiCad calibration candidate is allowed in clipboard export.
    // Only pad entries and component reference-text entries are copied.
    // ###########################################################################################
    private static bool IsClipboardEligibleKiCadCalibrationWorldPointCandidate(KiCadCalibrationWorldPointCandidate candidate)
    {
        string label = candidate.Label?.Trim() ?? string.Empty;

        return label.StartsWith("Pad ", StringComparison.OrdinalIgnoreCase) ||
               label.Contains(" pad ", StringComparison.OrdinalIgnoreCase) ||
               label.Contains(" reference text ", StringComparison.OrdinalIgnoreCase);
    }

    // ###########################################################################################
    // Builds tab-separated clipboard text for KiCad world-point candidates so they can be pasted
    // into Excel or inspected in a text editor.
    // ###########################################################################################
    private string BuildKiCadWorldPointCandidatesClipboardText(IReadOnlyList<KiCadCalibrationWorldPointCandidate> candidates)
    {
        var lines = new List<string>(candidates.Count + 1)
        {
            "Label\tWorld X\tWorld Y"
        };

        foreach (var candidate in candidates)
        {
            lines.Add(
                $"{candidate.Label}\t" +
                $"{candidate.WorldX.ToString("0.######", CultureInfo.InvariantCulture)}\t" +
                $"{candidate.WorldY.ToString("0.######", CultureInfo.InvariantCulture)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    // ###########################################################################################
    // Copies candidate KiCad world points for the current view to the clipboard.
    // Only pad entries and component reference-text entries are exported.
    // ###########################################################################################
    private async void CopyKiCadWorldPointCandidatesAsync()
    {
        var allCandidates = this.BuildCurrentKiCadCalibrationWorldPointCandidates();
        var clipboardCandidates = allCandidates
            .Where(TabSchematics.IsClipboardEligibleKiCadCalibrationWorldPointCandidate)
            .ToList();

        string schematicName = this.GetCurrentSchematicName();

        if (clipboardCandidates.Count == 0)
        {
            this.SchematicsHoverLabelText.Text = allCandidates.Count == 0
                ? $"No KiCad match found for '{schematicName}'. Names must match."
                : $"No pad or reference text entries found for '{schematicName}'.";
            this.SchematicsHoverLabelBorder.IsVisible = true;
            this.HideLabelEditorMenu();
            this.SchematicsContainer.Focus();

            if (TopLevel.GetTopLevel(this) is TopLevel topLevelClear && topLevelClear.Clipboard != null)
            {
                await topLevelClear.Clipboard.ClearAsync();
            }

            if (allCandidates.Count == 0)
            {
                Logger.Warning($"KiCad calibration copy failed. Excel name '{schematicName}' not found in traces JSON.");
            }
            else
            {
                Logger.Warning($"KiCad calibration copy found no pad/reference-text entries for schematic [{schematicName}].");
            }

            return;
        }

        string clipboardText = this.BuildKiCadWorldPointCandidatesClipboardText(clipboardCandidates);

        if (TopLevel.GetTopLevel(this) is TopLevel topLevel && topLevel.Clipboard != null)
        {
            //            await topLevel.Clipboard.SetTextAsync(clipboardText);
            await ClipboardExtensions.SetTextAsync(topLevel.Clipboard, clipboardText);
        }

        this.SchematicsHoverLabelText.Text = $"Copied {clipboardCandidates.Count} KiCad calibration points";
        this.SchematicsHoverLabelBorder.IsVisible = true;
        this.HideLabelEditorMenu();
        this.SchematicsContainer.Focus();

        Logger.Info($"Copied [{clipboardCandidates.Count}] KiCad calibration points for schematic [{schematicName}]");
    }

    // ###########################################################################################
    // Returns true when a KiCad copper point is visible on the inspected PCB side.
    // Treats "*.Cu" as visible on both sides so through-hole pads and vias are included.
    // ###########################################################################################
    private static bool IsKiCadPcbPointVisibleOnSide(IEnumerable<string> layers, string requiredLayer)
    {
        foreach (string layer in layers
                     .Where(layer => !string.IsNullOrWhiteSpace(layer))
                     .Select(layer => layer.Trim()))
        {
            if (string.Equals(layer, requiredLayer, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(layer, "*.Cu", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return !layers.Any();
    }

    // ###########################################################################################
    // Builds a strict orthogonal transform bypassing affine shear by utilizing exactly two points.
    // Ideal for non-rotated exported replica images to ensure perfectly parallel geometry tracking.
    // ###########################################################################################
    private static bool TryBuildOrthogonalCalibration(
        KiCadCalibrationPoint p1,
        KiCadCalibrationPoint p2,
        out KiCadViewCalibration calibration)
    {
        calibration = KiCadViewCalibration.Identity;

        double dWorldX = p2.WorldX - p1.WorldX;
        double dWorldY = p2.WorldY - p1.WorldY;

        if (Math.Abs(dWorldX) < 0.0001 || Math.Abs(dWorldY) < 0.0001)
        {
            return false;
        }

        double scaleX = (p2.ImageX - p1.ImageX) / dWorldX;
        double scaleY = (p2.ImageY - p1.ImageY) / dWorldY;

        double offsetX = p1.ImageX - (p1.WorldX * scaleX);
        double offsetY = p1.ImageY - (p1.WorldY * scaleY);

        calibration = new KiCadViewCalibration
        {
            HasAffineCalibration = true,
            A = scaleX,
            B = 0,
            C = offsetX,
            D = 0,
            E = scaleY,
            F = offsetY
        };

        return true;
    }

    // ###########################################################################################
    // Projects mouse pixel coordinates back onto KiCad PCB world coordinates for high-perf hovering.
    // ###########################################################################################
    private bool TryMapLocalToKiCadWorld(Point localPoint, Rect worldBounds, Rect contentRect, KiCadViewCalibration calibration, out Point worldPoint)
    {
        worldPoint = default;

        if (calibration.HasAffineCalibration && this.currentFullResBitmap != null &&
            this.currentFullResBitmap.PixelSize.Width > 0 && this.currentFullResBitmap.PixelSize.Height > 0)
        {
            double scaleX = contentRect.Width / this.currentFullResBitmap.PixelSize.Width;
            double scaleY = contentRect.Height / this.currentFullResBitmap.PixelSize.Height;

            double bitmapX = (localPoint.X - contentRect.X) / scaleX;
            double bitmapY = (localPoint.Y - contentRect.Y) / scaleY;

            double bx = bitmapX - calibration.C;
            double by = bitmapY - calibration.F;

            double det = (calibration.A * calibration.E) - (calibration.B * calibration.D);
            if (Math.Abs(det) < 1e-10) return false;

            double invDet = 1.0 / det;
            double wx = (calibration.E * bx - calibration.B * by) * invDet;
            double wy = (calibration.A * by - calibration.D * bx) * invDet;

            worldPoint = new Point(wx, wy);
            return true;
        }

        if (worldBounds.Width <= 0 || worldBounds.Height <= 0) return false;

        double localXFallback = localPoint.X;
        double localYFallback = localPoint.Y;

        if (this.currentFullResBitmap != null)
        {
            if (this.currentFullResBitmap.PixelSize.Width > 0)
                localXFallback -= calibration.OffsetX * (contentRect.Width / this.currentFullResBitmap.PixelSize.Width);
            if (this.currentFullResBitmap.PixelSize.Height > 0)
                localYFallback -= calibration.OffsetY * (contentRect.Height / this.currentFullResBitmap.PixelSize.Height);
        }
        else
        {
            localXFallback -= calibration.OffsetX;
            localYFallback -= calibration.OffsetY;
        }

        double nx = (localXFallback - contentRect.X) / contentRect.Width;
        double ny = (localYFallback - contentRect.Y) / contentRect.Height;

        if (Math.Abs(calibration.ScaleX) > 1e-10) nx /= calibration.ScaleX;
        if (Math.Abs(calibration.ScaleY) > 1e-10) ny /= calibration.ScaleY;

        if (calibration.MirrorX) nx = 1.0 - nx;
        if (calibration.MirrorY) ny = 1.0 - ny;

        worldPoint = new Point((nx * worldBounds.Width) + worldBounds.X, (ny * worldBounds.Height) + worldBounds.Y);
        return true;
    }

    // ###########################################################################################
    // Math helper mapping minimal hit distance between the local mouse vector pointing to line.
    // ###########################################################################################
    private static double DistanceToSegment(Point p, double vX, double vY, double wX, double wY)
    {
        double l2 = Math.Pow(wX - vX, 2) + Math.Pow(wY - vY, 2);
        if (l2 == 0.0) return Math.Sqrt(Math.Pow(p.X - vX, 2) + Math.Pow(p.Y - vY, 2));

        double t = Math.Max(0, Math.Min(1, ((p.X - vX) * (wX - vX) + (p.Y - vY) * (wY - vY)) / l2));
        double projX = vX + t * (wX - vX);
        double projY = vY + t * (wY - vY);

        return Math.Sqrt(Math.Pow(p.X - projX, 2) + Math.Pow(p.Y - projY, 2));
    }

    // ###########################################################################################
    // Reverses affine tracking matrices rapidly detecting any 2D intersection hit between
    // local mouse input vs world-tracked components mathematically.
    // ###########################################################################################
    private void HitTestKiCadOverlayForHover(Point pointerInContainer)
    {
        var view = this.ResolveKiCadViewForCurrentSchematic();
        if (view == null || this.thisKiCadProject == null || this.currentFullResBitmap == null)
        {
            this.SetHoveredKiCadNet(null);
            return;
        }

        bool isTop = string.Equals(view.Type, "pcb_top", StringComparison.OrdinalIgnoreCase);
        bool isBottom = string.Equals(view.Type, "pcb_bottom", StringComparison.OrdinalIgnoreCase);

        if (!isTop && !isBottom)
        {
            // Only perform hover-hit-tests on pure PCB rendering right now.
            this.SetHoveredKiCadNet(null);
            return;
        }

        string requiredLayer = isBottom ? "B.Cu" : "F.Cu";
        var pcb = this.thisKiCadProject.Root.Pcb.ElementAtOrDefault(view.SourceIndex);
        if (pcb == null)
        {
            this.SetHoveredKiCadNet(null);
            return;
        }

        var contentRect = this.GetImageContentRect();
        var worldBounds = this.GetKiCadPcbWorldBounds(pcb);
        string currentSchematicName = this.GetCurrentSchematicName();
        var calibration = this.GetKiCadViewCalibration(currentSchematicName);

        if (!TryInvert(this.schematicsMatrix, out var inv))
        {
            this.SetHoveredKiCadNet(null);
            return;
        }

        var localPoint = new Point(
            (pointerInContainer.X * inv.M11) + (pointerInContainer.Y * inv.M21) + inv.M31,
            (pointerInContainer.X * inv.M12) + (pointerInContainer.Y * inv.M22) + inv.M32);

        if (!this.TryMapLocalToKiCadWorld(localPoint, worldBounds, contentRect, calibration, out var worldPoint))
        {
            this.SetHoveredKiCadNet(null);
            return;
        }

        double closestDist = double.MaxValue;
        KiCadNetRef? bestNet = null;
        string? bestPadNumber = null;
        double baseThresholdWorld = 0.5; // Baseline threshold in KiCad units (~0.5mm limit)

        foreach (var footprint in pcb.Footprints)
        {
            foreach (var pad in footprint.Pads)
            {
                if (pad.Net == null || string.IsNullOrWhiteSpace(pad.Net.NormalizedName)) continue;
                if (!TabSchematics.IsKiCadPcbPointVisibleOnSide(pad.Layers, requiredLayer)) continue;
                if (pad.AbsoluteCenter == null) continue;

                double dx = pad.AbsoluteCenter.X - worldPoint.X;
                double dy = pad.AbsoluteCenter.Y - worldPoint.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist < closestDist && dist < (pad.Size?.X / 2.0 ?? baseThresholdWorld) + 0.3)
                {
                    closestDist = dist;
                    bestNet = pad.Net;
                    bestPadNumber = pad.Number;
                }
            }
        }

        foreach (var segment in pcb.Routing.Segments)
        {
            if (segment.Net == null || string.IsNullOrWhiteSpace(segment.Net.NormalizedName)) continue;
            if (!string.Equals(segment.Layer?.Trim(), requiredLayer, StringComparison.OrdinalIgnoreCase)) continue;
            if (segment.Start == null || segment.End == null) continue;

            double dist = DistanceToSegment(worldPoint, segment.Start.X, segment.Start.Y, segment.End.X, segment.End.Y);
            if (dist < closestDist && dist < (segment.Width / 2.0 ?? 0.25) + 0.3)
            {
                closestDist = dist;
                bestNet = segment.Net;
                bestPadNumber = null;
            }
        }

        foreach (var via in pcb.Routing.Vias)
        {
            if (via.Net == null || string.IsNullOrWhiteSpace(via.Net.NormalizedName)) continue;
            if (!TabSchematics.IsKiCadPcbPointVisibleOnSide(via.Layers, requiredLayer)) continue;
            if (via.At == null) continue;

            double dx = via.At.X - worldPoint.X;
            double dy = via.At.Y - worldPoint.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);

            if (dist < closestDist && dist < (via.Size / 2.0 ?? 0.4) + 0.3)
            {
                closestDist = dist;
                bestNet = via.Net;
                bestPadNumber = null;
            }
        }

        string? foundNet = bestNet?.NormalizedName?.Trim();
        this.SetHoveredKiCadNet(string.IsNullOrWhiteSpace(foundNet) ? null : foundNet);
        this.thisHoveredKiCadPadNumber = bestPadNumber?.Trim();
    }

    // ###########################################################################################
    // Updates the panel displaying connected components and pins for all currently active nets.
    // Rebuilds the list only when the active net set actually changes.
    // ###########################################################################################
    private void UpdateKiCadNetConnectionsPanel()
    {
        var activeNets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? activeHoveredKiCadNetName = this.GetActiveHoveredKiCadNetName();

        if (!string.IsNullOrWhiteSpace(activeHoveredKiCadNetName))
        {
            activeNets.Add(activeHoveredKiCadNetName);
        }

        foreach (string lockedNet in this.thisLockedKiCadNetNames)
        {
            if (!string.IsNullOrWhiteSpace(lockedNet))
            {
                activeNets.Add(lockedNet);
            }
        }

        this.ClearKiCadTraceSelectionButton.IsEnabled = this.thisLockedKiCadNetNames.Count > 0;

        if (activeNets.Count == 0 || this.thisKiCadProject?.Root == null)
        {
            this.thisLastKiCadNetConnectionsSignature = string.Empty;
            this.KiCadNetConnectionsNetNameText.Text = string.Empty;
            this.KiCadNetConnectionsList.ItemsSource = null;
            this.KiCadNetConnectionsPanel.IsVisible = false;
            return;
        }

        var sortedNetNames = activeNets
            .OrderBy(netName => netName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        string signature = string.Join("\u001F", sortedNetNames);

        if (string.Equals(this.thisLastKiCadNetConnectionsSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        var boardComponents = this.MainWindow?.CurrentBoardData?.Components;
        var orderLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (boardComponents != null)
        {
            for (int i = 0; i < boardComponents.Count; i++)
            {
                string boardLabel = boardComponents[i].BoardLabel?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(boardLabel) && !orderLookup.ContainsKey(boardLabel))
                {
                    orderLookup[boardLabel] = i;
                }
            }
        }

        var connections = new List<(string ConnStr, int OrderIndex, int PadIndex)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pcb in this.thisKiCadProject.Root.Pcb)
        {
            foreach (var footprint in pcb.Footprints)
            {
                string refName = footprint.Reference?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(refName))
                {
                    continue;
                }

                foreach (var pad in footprint.Pads)
                {
                    string netName = pad.Net?.NormalizedName?.Trim() ?? string.Empty;

                    if (!activeNets.Contains(netName))
                    {
                        continue;
                    }

                    string padNum = pad.Number?.Trim() ?? "?";

                    string connStr = activeNets.Count > 1
                        ? $"{refName} pin {padNum} [{netName}]"
                        : $"{refName} pin {padNum}";

                    if (!seen.Add(connStr))
                    {
                        continue;
                    }

                    int orderIndex = orderLookup.TryGetValue(refName, out int idx) ? idx : int.MaxValue;
                    int padIndex = int.TryParse(padNum, out int parsedPad) ? parsedPad : int.MaxValue;

                    connections.Add((connStr, orderIndex, padIndex));
                }
            }
        }

        if (connections.Count == 0)
        {
            this.thisLastKiCadNetConnectionsSignature = signature;
            this.KiCadNetConnectionsNetNameText.Text = string.Join(Environment.NewLine, sortedNetNames);
            this.KiCadNetConnectionsList.ItemsSource = null;
            this.KiCadNetConnectionsPanel.IsVisible = false;
            return;
        }

        var sortedConnections = connections
            .OrderBy(connection => connection.OrderIndex)
            .ThenBy(connection => connection.PadIndex)
            .ThenBy(connection => connection.ConnStr, StringComparer.OrdinalIgnoreCase)
            .Select(connection => connection.ConnStr)
            .ToList();

        this.thisLastKiCadNetConnectionsSignature = signature;
        this.KiCadNetConnectionsNetNameText.Text = string.Join(Environment.NewLine, sortedNetNames);
        this.KiCadNetConnectionsList.ItemsSource = sortedConnections;
        this.KiCadNetConnectionsPanel.IsVisible = true;
    }

    // ###########################################################################################
    // Queues normal KiCad overlay refreshes, but allows blink-driven callers to bypass the queue
    // and render immediately so visual blinking stays synchronized with the main highlight layer.
    // Version tracking prevents stale queued callbacks from redrawing after an immediate refresh.
    // ###########################################################################################
    private void RefreshKiCadOverlay(bool forceImmediate = false)
    {
        this.thisKiCadOverlayRefreshRequestVersion = unchecked(this.thisKiCadOverlayRefreshRequestVersion + 1);

        if (forceImmediate)
        {
            this.thisIsKiCadOverlayRefreshQueued = false;

            int renderVersion = this.thisKiCadOverlayRefreshRequestVersion;
            this.RefreshKiCadOverlayNow();
            this.thisKiCadOverlayLastRenderedVersion = renderVersion;

            if (this.thisKiCadOverlayLastRenderedVersion != this.thisKiCadOverlayRefreshRequestVersion)
            {
                this.RefreshKiCadOverlay();
            }

            return;
        }

        if (this.thisIsKiCadOverlayRefreshQueued)
        {
            return;
        }

        this.thisIsKiCadOverlayRefreshQueued = true;

        Dispatcher.UIThread.Post(() =>
        {
            this.thisIsKiCadOverlayRefreshQueued = false;

            if (this.thisKiCadOverlayLastRenderedVersion == this.thisKiCadOverlayRefreshRequestVersion)
            {
                return;
            }

            int renderVersion = this.thisKiCadOverlayRefreshRequestVersion;
            this.RefreshKiCadOverlayNow();
            this.thisKiCadOverlayLastRenderedVersion = renderVersion;

            if (this.thisKiCadOverlayLastRenderedVersion != this.thisKiCadOverlayRefreshRequestVersion)
            {
                this.RefreshKiCadOverlay();
            }
        }, DispatcherPriority.Background);
    }

    // ###########################################################################################
    // Rebuilds the currently visible KiCad overlay for the selected image view immediately.
    // PCB views render copper geometry, while schematic views render resolved wire paths.
    // Also allows a pin-1-only render path for hovered components even when no traces are active.
    // ###########################################################################################
    private void RefreshKiCadOverlayNow()
    {
        this.ClearKiCadOverlay();
        this.UpdateKiCadNetConnectionsPanel();

        string? activeHoveredKiCadNetName = this.GetActiveHoveredKiCadNetName();
        bool hasActiveKiCadNets =
            this.thisSelectedKiCadNormalizedNetNames.Count > 0 ||
            this.thisLockedKiCadNetNames.Count > 0 ||
            !string.IsNullOrWhiteSpace(activeHoveredKiCadNetName);

        if (this.thisKiCadProject == null || this.currentFullResBitmap == null)
        {
            return;
        }

        var currentView = this.ResolveKiCadViewForCurrentSchematic();
        if (currentView == null)
        {
            return;
        }

        if (string.Equals(currentView.Type, "pcb_top", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(currentView.Type, "pcb_bottom", StringComparison.OrdinalIgnoreCase))
        {
            if (!hasActiveKiCadNets && !this.HasPin1HighlightTargetReference())
            {
                return;
            }

            this.RenderKiCadPcbGeometry(currentView);
            return;
        }

        if (!hasActiveKiCadNets)
        {
            return;
        }

        if (string.Equals(currentView.Type, "schematic", StringComparison.OrdinalIgnoreCase))
        {
            this.RenderKiCadSchematicGeometry(currentView);
        }
    }

    // ###########################################################################################
    // Compares KiCad pad designators so numeric pins sort numerically and non-numeric pins sort
    // alphabetically. This lets footprints like B/C/E choose B as the primary highlighted pin.
    // ###########################################################################################
    private static int CompareKiCadPadDesignators(string? left, string? right)
    {
        string leftValue = left?.Trim() ?? string.Empty;
        string rightValue = right?.Trim() ?? string.Empty;

        bool leftIsNumber = int.TryParse(leftValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int leftNumber);
        bool rightIsNumber = int.TryParse(rightValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rightNumber);

        if (leftIsNumber && rightIsNumber)
        {
            return leftNumber.CompareTo(rightNumber);
        }

        if (leftIsNumber != rightIsNumber)
        {
            return leftIsNumber ? -1 : 1;
        }

        return StringComparer.OrdinalIgnoreCase.Compare(leftValue, rightValue);
    }

    // ###########################################################################################
    // Returns true when the given pad is the primary pad that should receive the special marker.
    // Prefers pad "1" when it exists; otherwise falls back to the first visible pad designator.
    // ###########################################################################################
    private static bool IsPrimaryPadForPin1Highlight(
        KiCadPcbFootprint footprint,
        KiCadPcbPad pad,
        string requiredLayer)
    {
        string currentPadDesignator = pad.Number?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(currentPadDesignator))
        {
            return false;
        }

        if (string.Equals(currentPadDesignator, "1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var visiblePadDesignators = footprint.Pads
            .Where(candidate => candidate.AbsoluteCenter != null)
            .Where(candidate => TabSchematics.IsKiCadPcbPointVisibleOnSide(candidate.Layers, requiredLayer))
            .Select(candidate => candidate.Number?.Trim() ?? string.Empty)
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (visiblePadDesignators.Count == 0)
        {
            return false;
        }

        if (visiblePadDesignators.Any(candidate =>
                string.Equals(candidate, "1", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        string primaryDesignator = visiblePadDesignators
            .OrderBy(candidate => candidate, Comparer<string>.Create(TabSchematics.CompareKiCadPadDesignators))
            .First();

        return string.Equals(currentPadDesignator, primaryDesignator, StringComparison.OrdinalIgnoreCase);
    }

    // ###########################################################################################
    // Updates the currently hovered component board label and rebuilds the hover-only overlay.
    // Also refreshes the KiCad overlay immediately so pin-1 hover markers appear in sync.
    // ###########################################################################################
    private void SetHoveredComponentBoardLabel(string? boardLabel)
    {
        if (string.Equals(this.thisHoveredComponentBoardLabel, boardLabel, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        this.thisHoveredComponentBoardLabel = boardLabel;
        this.RefreshHoveredComponentHighlightOverlay();
        this.RefreshKiCadOverlay(forceImmediate: true);
    }

    // ###########################################################################################
    // Rebuilds the hover-only highlight overlay for the active schematic component.
    // Suppresses hover overlay when the same component is already selected.
    // ###########################################################################################
    private void RefreshHoveredComponentHighlightOverlay()
    {
        this.SchematicsHoverHighlightsOverlay.BitmapPixelSize = this.currentFullResBitmap?.PixelSize ?? new PixelSize(0, 0);
        this.SchematicsHoverHighlightsOverlay.ViewMatrix = this.schematicsMatrix;

        if (this.currentFullResBitmap == null || string.IsNullOrWhiteSpace(this.thisHoveredComponentBoardLabel))
        {
            this.SchematicsHoverHighlightsOverlay.HighlightIndex = null;
            this.SchematicsHoverHighlightsOverlay.InvalidateVisual();
            return;
        }

        string schematicName = this.GetCurrentSchematicName();
        if (string.IsNullOrWhiteSpace(schematicName) ||
            !this.highlightRectsBySchematicAndLabel.TryGetValue(schematicName, out var byLabel) ||
            !byLabel.TryGetValue(this.thisHoveredComponentBoardLabel, out var rects) ||
            rects.Count == 0)
        {
            this.SchematicsHoverHighlightsOverlay.HighlightIndex = null;
            this.SchematicsHoverHighlightsOverlay.InvalidateVisual();
            return;
        }

        bool isAlreadySelected = this.IsComponentBoardLabelSelected(this.thisHoveredComponentBoardLabel);

        if (isAlreadySelected)
        {
            this.SchematicsHoverHighlightsOverlay.HighlightIndex = null;
            this.SchematicsHoverHighlightsOverlay.InvalidateVisual();
            return;
        }

        Color highlightColor = Colors.IndianRed;
        double highlightOpacity = 0.20;

        if (this.schematicByName.TryGetValue(schematicName, out var schematic))
        {
            highlightColor = ParseColorOrDefault(schematic.MainImageHighlightColor, Colors.IndianRed);
            highlightOpacity = ParseOpacityOrDefault(schematic.MainHighlightOpacity, 0.20);
        }

        this.SchematicsHoverHighlightsOverlay.HighlightColor = highlightColor;
        this.SchematicsHoverHighlightsOverlay.HighlightOpacity = highlightOpacity;
        this.SchematicsHoverHighlightsOverlay.HighlightIndex = new HighlightSpatialIndex(rects);
        this.SchematicsHoverHighlightsOverlay.InvalidateVisual();
    }

    // ###########################################################################################
    // Returns true when a schematic symbol reference is a generated internal KiCad helper symbol
    // that should not be used as a human-facing calibration candidate.
    // ###########################################################################################
    private static bool IsInternalKiCadSymbolReference(string reference)
    {
        string trimmed = reference?.Trim() ?? string.Empty;
        return trimmed.StartsWith("#", StringComparison.OrdinalIgnoreCase);
    }

    // ###########################################################################################
    // Builds a short human-readable description of the text anchor based on KiCad justification.
    // ###########################################################################################
    private static string DescribeKiCadTextAnchor(KiCadSchematicTextEffects? effects)
    {
        var justify = effects?.Justify ?? new List<string>();

        bool hasLeft = justify.Any(value => string.Equals(value, "left", StringComparison.OrdinalIgnoreCase));
        bool hasRight = justify.Any(value => string.Equals(value, "right", StringComparison.OrdinalIgnoreCase));
        bool hasTop = justify.Any(value => string.Equals(value, "top", StringComparison.OrdinalIgnoreCase));
        bool hasBottom = justify.Any(value => string.Equals(value, "bottom", StringComparison.OrdinalIgnoreCase));

        string horizontal = hasLeft ? "left" : hasRight ? "right" : "center";
        string vertical = hasTop ? "top" : hasBottom ? "bottom" : "middle";

        return $"{horizontal}-{vertical}";
    }

    // ###########################################################################################
    // Clears all explicitly selected KiCad traces and refreshes the overlay and blink state.
    // ###########################################################################################
    private void ClearAllKiCadTraceSelections()
    {
        this.thisLockedKiCadNetNames.Clear();
        this.thisHoveredKiCadNetName = null;
        this.thisHoveredKiCadPadNumber = null;

        this.SchematicsHoverPadBorder.IsVisible = false;
        this.SchematicsHoverPadText.Text = string.Empty;

        this.RefreshKiCadOverlay();
        this.RefreshBlinkStateFromCurrentSelection();
    }

    // ###########################################################################################
    // Returns true when the pointer is currently inside the KiCad net connections panel bounds.
    // This prevents hover and click handling from reacting to traces or components behind it.
    // ###########################################################################################
    private bool IsPointerInsideKiCadNetConnectionsPanel(Point containerPoint)
    {
        if (!this.KiCadNetConnectionsPanel.IsVisible)
        {
            return false;
        }

        Point? translatedTopLeft = this.KiCadNetConnectionsPanel.TranslatePoint(new Point(0, 0), this.SchematicsContainer);
        if (!translatedTopLeft.HasValue)
        {
            return false;
        }

        var panelRect = new Rect(translatedTopLeft.Value, this.KiCadNetConnectionsPanel.Bounds.Size);
        return panelRect.Contains(containerPoint);
    }

    // ###########################################################################################
    // Clears transient hover state while the pointer is inside the KiCad net connections panel.
    // Explicitly selected or locked KiCad nets remain visible and are not affected.
    // ###########################################################################################
    private void ClearTransientHoverForKiCadNetConnectionsPanel()
    {
        this.SetHoveredComponentBoardLabel(null);
        this.SetHoveredKiCadNet(null);
        this.thisHoveredKiCadPadNumber = null;

        this.SchematicsHoverLabelBorder.IsVisible = false;
        this.SchematicsHoverLabelText.Text = string.Empty;
        this.SchematicsHoverPadBorder.IsVisible = false;
        this.SchematicsHoverPadText.Text = string.Empty;
        this.SchematicsContainer.Cursor = Cursor.Default;

        if (this.MainWindow != null)
        {
            this.MainWindow.isHoveringComponent = false;
        }
    }

    // ###########################################################################################
    // Handle manual row clicks for board-specific schematic settings.
    // ###########################################################################################
    private void OnBoardHoverHighlightsTracesRowClicked(object? sender, PointerPressedEventArgs e)
    {
        if (!this.CheckBoardHoverHighlightsTraces.IsEnabled)
        {
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.CheckBoardHoverHighlightsTraces.IsChecked = !this.CheckBoardHoverHighlightsTraces.IsChecked;
            e.Handled = true;
        }
    }

    // ###########################################################################################
    // Handle manual row clicks for board-specific contributor mode.
    // ###########################################################################################
    private void OnBoardContributorModeRowClicked(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.CheckBoardContributorMode.IsChecked = !this.CheckBoardContributorMode.IsChecked;
            e.Handled = true;
        }
    }

    // ###########################################################################################
    // Restores board-specific schematic settings from persisted configuration.
    // Disables trace-hover behavior when the currently shown schematic has no KiCad overlay data.
    // ###########################################################################################
    private void RestoreBoardSettings(string boardKey)
    {
        this.thisSuppressBoardSettingsChanged = true;

        bool hasBoard = !string.IsNullOrWhiteSpace(boardKey);

        this.BoardSettingsPanel.IsEnabled = hasBoard;

        this.CheckBoardMarkPin1OnSelectedComponent.IsChecked = hasBoard
            ? UserSettings.GetSchematicsMarkPin1OnSelectedComponentForBoard(boardKey)
            : false;

        this.CheckBoardHoverHighlightsTraces.IsChecked = hasBoard
            ? UserSettings.GetSchematicsHoverHighlightsTracesForBoard(boardKey)
            : true;

        this.CheckBoardContributorMode.IsEnabled = hasBoard;
        this.CheckBoardContributorMode.IsChecked = hasBoard
            ? UserSettings.GetContributorModeForBoard(boardKey)
            : UserSettings.ContributorMode;

        this.thisSuppressBoardSettingsChanged = false;

        this.UpdateInteractiveCadTraceHoverModeUi();
    }

    // ###########################################################################################
    // Returns true when the current schematic allows hover-driven KiCad trace highlighting.
    // ###########################################################################################
    private bool IsBoardHoverHighlightsTracesEnabled()
    {
        if (!this.HasCurrentSchematicKiCadTraces())
        {
            return false;
        }

        if (string.Equals(UserSettings.InteractiveCadTraceHoverMode, "HoldShift", StringComparison.Ordinal))
        {
            return this.thisIsInteractiveCadTraceHoverShiftPressed;
        }

        var boardKey = this.MainWindow?.GetCurrentBoardKey();

        return !string.IsNullOrWhiteSpace(boardKey) &&
               UserSettings.GetSchematicsHoverHighlightsTracesForBoard(boardKey);
    }

    // ###########################################################################################
    // Returns true when contributor-only schematic actions are enabled for the current board.
    // ###########################################################################################
    private bool IsBoardContributorModeEnabled()
    {
        var boardKey = this.MainWindow?.GetCurrentBoardKey();
        return string.IsNullOrWhiteSpace(boardKey)
            ? UserSettings.ContributorMode
            : UserSettings.GetContributorModeForBoard(boardKey);
    }

    // ###########################################################################################
    // Returns true when the currently shown schematic has KiCad overlay data available.
    // ###########################################################################################
    private bool HasCurrentSchematicKiCadTraces()
    {
        if (this.thisKiCadProject?.Root?.Project == null)
        {
            return false;
        }

        var view = this.ResolveKiCadViewForCurrentSchematic();
        if (view == null)
        {
            return false;
        }

        if (string.Equals(view.Type, "pcb_top", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(view.Type, "pcb_bottom", StringComparison.OrdinalIgnoreCase))
        {
            return view.SourceIndex >= 0 &&
                   view.SourceIndex < this.thisKiCadProject.Root.Pcb.Count;
        }

        if (string.Equals(view.Type, "schematic", StringComparison.OrdinalIgnoreCase))
        {
            return view.SourceIndex >= 0 &&
                   view.SourceIndex < this.thisKiCadProject.Root.Schematics.Count;
        }

        return false;
    }

    // ###########################################################################################
    // Updates cached SHIFT state for interactive KiCad hover highlighting.
    // ###########################################################################################
    private void UpdateInteractiveCadTraceHoverShiftState(KeyModifiers modifiers)
    {
        bool isShiftPressed = modifiers.HasFlag(KeyModifiers.Shift);

        if (this.thisIsInteractiveCadTraceHoverShiftPressed == isShiftPressed)
        {
            return;
        }

        this.thisIsInteractiveCadTraceHoverShiftPressed = isShiftPressed;
        this.RefreshKiCadHoverPadUi();
        this.RefreshKiCadOverlay();
    }

    // ###########################################################################################
    // Refreshes the transient KiCad pad hover label based on the active hover mode.
    // ###########################################################################################
    private void RefreshKiCadHoverPadUi()
    {
        string hoveredPadNumber = this.GetActiveHoveredKiCadPadNumber()?.Trim() ?? string.Empty;
        string hoveredNetName = this.GetActiveHoveredKiCadNetName()?.Trim() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(hoveredPadNumber))
        {
            this.SchematicsHoverPadText.Text = string.IsNullOrWhiteSpace(hoveredNetName)
                ? hoveredPadNumber
                : $"{hoveredPadNumber} | {hoveredNetName}";
            this.SchematicsHoverPadBorder.IsVisible = true;
        }
        else
        {
            this.SchematicsHoverPadBorder.IsVisible = false;
            this.SchematicsHoverPadText.Text = string.Empty;
        }
    }

    // ###########################################################################################
    // Reacts to global interactive CAD trace hover mode changes from configuration.
    // ###########################################################################################
    private void OnInteractiveCadTraceHoverModeChanged()
    {
        Dispatcher.UIThread.Post(() =>
        {
            this.UpdateInteractiveCadTraceHoverModeUi();
            this.RefreshKiCadHoverPadUi();
            this.RefreshKiCadOverlay();
        });
    }

    // ###########################################################################################
    // Updates schematics board-settings visibility for the global interactive CAD trace mode.
    // ###########################################################################################
    private void UpdateInteractiveCadTraceHoverModeUi()
    {
        bool isAlwaysMode = string.Equals(UserSettings.InteractiveCadTraceHoverMode, "Always", StringComparison.Ordinal);
        bool hasBoard = this.BoardSettingsPanel.IsEnabled;
        bool hasKiCadTraces = this.HasCurrentSchematicKiCadTraces();
        bool hasKiCadPcbPadData = this.HasCurrentSchematicKiCadPcbPadData();

        this.BoardMarkPin1OnSelectedComponentRow.IsVisible = hasBoard && hasKiCadPcbPadData;
        this.CheckBoardMarkPin1OnSelectedComponent.IsEnabled = hasBoard && hasKiCadPcbPadData;

        this.BoardHoverHighlightsTracesRow.IsVisible = isAlwaysMode && hasBoard && hasKiCadTraces;
        this.CheckBoardHoverHighlightsTraces.IsEnabled =
            isAlwaysMode &&
            hasBoard &&
            hasKiCadTraces;
    }

    // ###########################################################################################
    // Returns the active hovered KiCad net name, honoring the current hover mode settings.
    // ###########################################################################################
    private string? GetActiveHoveredKiCadNetName()
    {
        return this.IsBoardHoverHighlightsTracesEnabled()
            ? this.thisHoveredKiCadNetName
            : null;
    }

    // ###########################################################################################
    // Returns the active hovered KiCad pad number, honoring the current hover mode settings.
    // ###########################################################################################
    private string? GetActiveHoveredKiCadPadNumber()
    {
        return this.IsBoardHoverHighlightsTracesEnabled()
            ? this.thisHoveredKiCadPadNumber
            : null;
    }

    // ###########################################################################################
    // Returns the currently selected editor highlights for the active schematic in working-list order.
    // ###########################################################################################
    private List<EditableComponentHighlight> GetSelectedLabelEditorHighlightsForCurrentSchematic()
    {
        string schematicName = this.GetCurrentSchematicName();

        return this.thisLabelEditorWorkingHighlights
            .Where(row => string.Equals(row.SchematicName, schematicName, StringComparison.OrdinalIgnoreCase))
            .Where(row => this.thisSelectedLabelEditorHighlights.Contains(row))
            .ToList();
    }

    // ###########################################################################################
    // Returns the first selected editor highlight for the active schematic, or null when none exist.
    // ###########################################################################################
    private EditableComponentHighlight? GetFirstSelectedLabelEditorHighlightForCurrentSchematic()
    {
        return this.GetSelectedLabelEditorHighlightsForCurrentSchematic().FirstOrDefault();
    }

    // ###########################################################################################
    // Returns true when the given highlight is part of the current editor selection.
    // ###########################################################################################
    private bool IsSelectedLabelEditorHighlight(EditableComponentHighlight highlight)
    {
        return this.thisSelectedLabelEditorHighlights.Contains(highlight);
    }

    // ###########################################################################################
    // Clears the current multi-selection and optionally refreshes the editor overlay.
    // ###########################################################################################
    private void ClearSelectedLabelEditorHighlights(bool refresh = true)
    {
        this.thisSelectedLabelEditorHighlights.Clear();
        this.thisSelectedLabelEditorHighlight = null;

        if (refresh)
        {
            this.RefreshLabelEditorOverlay();
        }
    }

    // ###########################################################################################
    // Replaces the current multi-selection with one highlight and sets it as the primary selection.
    // ###########################################################################################
    private void SetSingleSelectedLabelEditorHighlight(EditableComponentHighlight highlight, bool refresh = true)
    {
        this.thisSelectedLabelEditorHighlights.Clear();
        this.thisSelectedLabelEditorHighlights.Add(highlight);
        this.thisSelectedLabelEditorHighlight = highlight;

        if (refresh)
        {
            this.RefreshLabelEditorOverlay();
        }
    }

    // ###########################################################################################
    // Toggles one highlight inside the current multi-selection and updates the primary selection.
    // ###########################################################################################
    private void ToggleSelectedLabelEditorHighlight(EditableComponentHighlight highlight)
    {
        if (this.thisSelectedLabelEditorHighlights.Contains(highlight))
        {
            this.thisSelectedLabelEditorHighlights.Remove(highlight);

            if (ReferenceEquals(this.thisSelectedLabelEditorHighlight, highlight))
            {
                this.thisSelectedLabelEditorHighlight = this.GetFirstSelectedLabelEditorHighlightForCurrentSchematic();
            }
        }
        else
        {
            this.thisSelectedLabelEditorHighlights.Add(highlight);
            this.thisSelectedLabelEditorHighlight = highlight;
        }

        this.RefreshLabelEditorOverlay();
    }

    // ###########################################################################################
    // Returns true when there is at least one selected editor highlight on the current schematic.
    // ###########################################################################################
    private bool HasSelectedLabelEditorHighlightsForCurrentSchematic()
    {
        return this.GetSelectedLabelEditorHighlightsForCurrentSchematic().Count > 0;
    }

    // ###########################################################################################
    // Computes the combined selection bounds for all selected editor highlights on the current schematic.
    // ###########################################################################################
    private bool TryGetSelectedLabelEditorBounds(out Rect selectionBounds)
    {
        selectionBounds = default;

        var selected = this.GetSelectedLabelEditorHighlightsForCurrentSchematic();
        if (selected.Count == 0)
        {
            return false;
        }

        double left = selected.Min(row => row.X);
        double top = selected.Min(row => row.Y);
        double right = selected.Max(row => row.X + row.Width);
        double bottom = selected.Max(row => row.Y + row.Height);

        selectionBounds = new Rect(left, top, right - left, bottom - top);
        return true;
    }

    // ###########################################################################################
    // Returns true when the pointer is inside the current selection bounds so grouped move can start.
    // ###########################################################################################
    private bool IsPointerInsideSelectedLabelEditorBounds(Point pointerInContainer)
    {
        if (!this.TryGetSelectedLabelEditorBounds(out var selectionBounds))
        {
            return false;
        }

        if (!this.TryGetLabelEditorLocalPoint(pointerInContainer, out var localPoint))
        {
            return false;
        }

        var localRect = this.ConvertLabelEditorPixelRectToLocalRect(selectionBounds);
        return localRect.Contains(localPoint);
    }

    // ###########################################################################################
    // Captures the original rectangles of all selected highlights before a move or resize starts.
    // ###########################################################################################
    private void CaptureSelectedLabelEditorDragState()
    {
        this.thisLabelEditorOriginalDragRectangles.Clear();

        foreach (var row in this.GetSelectedLabelEditorHighlightsForCurrentSchematic())
        {
            this.thisLabelEditorOriginalDragRectangles[row] = new Rect(row.X, row.Y, row.Width, row.Height);
        }
    }

    // ###########################################################################################
    // Applies a transformed group bounds rectangle back onto all selected highlights proportionally.
    // ###########################################################################################
    private void ApplyTransformedBoundsToSelectedLabelEditorHighlights(
        Rect originalSelectionBounds,
        Rect newSelectionBounds,
        IReadOnlyDictionary<EditableComponentHighlight, Rect>? sourceRects = null)
    {
        var selected = this.GetSelectedLabelEditorHighlightsForCurrentSchematic();
        if (selected.Count == 0)
        {
            return;
        }

        double originalWidth = Math.Max(1.0, originalSelectionBounds.Width);
        double originalHeight = Math.Max(1.0, originalSelectionBounds.Height);
        double newWidth = Math.Max(1.0, newSelectionBounds.Width);
        double newHeight = Math.Max(1.0, newSelectionBounds.Height);

        foreach (var row in selected)
        {
            Rect sourceRect = sourceRects != null && sourceRects.TryGetValue(row, out var storedRect)
                ? storedRect
                : new Rect(row.X, row.Y, row.Width, row.Height);

            double relativeLeft = (sourceRect.Left - originalSelectionBounds.Left) / originalWidth;
            double relativeTop = (sourceRect.Top - originalSelectionBounds.Top) / originalHeight;
            double relativeRight = (sourceRect.Right - originalSelectionBounds.Left) / originalWidth;
            double relativeBottom = (sourceRect.Bottom - originalSelectionBounds.Top) / originalHeight;

            double left = newSelectionBounds.Left + (relativeLeft * newWidth);
            double top = newSelectionBounds.Top + (relativeTop * newHeight);
            double right = newSelectionBounds.Left + (relativeRight * newWidth);
            double bottom = newSelectionBounds.Top + (relativeBottom * newHeight);

            row.X = left;
            row.Y = top;
            row.Width = Math.Max(1.0, right - left);
            row.Height = Math.Max(1.0, bottom - top);
        }
    }

    // ###########################################################################################
    // Returns the selected editor highlight under the pointer, if any.
    // ###########################################################################################
    private bool TryGetSelectedLabelEditorHighlightAtContainerPoint(Point pointerInContainer, out int workingIndex)
    {
        workingIndex = -1;

        if (!this.TryGetLabelEditorHighlightAtContainerPoint(pointerInContainer, out var hitIndex))
        {
            return false;
        }

        var hitHighlight = this.thisLabelEditorWorkingHighlights[hitIndex];
        if (!this.IsSelectedLabelEditorHighlight(hitHighlight))
        {
            return false;
        }

        workingIndex = hitIndex;
        return true;
    }

    // ###########################################################################################
    // Handle manual row clicks for board-specific pin-1 marking.
    // ###########################################################################################
    private void OnBoardMarkPin1OnSelectedComponentRowClicked(object? sender, PointerPressedEventArgs e)
    {
        if (!this.CheckBoardMarkPin1OnSelectedComponent.IsEnabled)
        {
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.CheckBoardMarkPin1OnSelectedComponent.IsChecked = !this.CheckBoardMarkPin1OnSelectedComponent.IsChecked;
            e.Handled = true;
        }
    }

    // ###########################################################################################
    // Returns true when the current schematic is a KiCad-backed PCB replica view with pad data.
    // Pin-1 marking is only available there because the current KiCad JSON exposes pad geometry.
    // ###########################################################################################
    private bool HasCurrentSchematicKiCadPcbPadData()
    {
        if (this.thisKiCadProject?.Root?.Project == null)
        {
            return false;
        }

        var view = this.ResolveKiCadViewForCurrentSchematic();
        if (view == null)
        {
            return false;
        }

        if (!string.Equals(view.Type, "pcb_top", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(view.Type, "pcb_bottom", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return view.SourceIndex >= 0 &&
               view.SourceIndex < this.thisKiCadProject.Root.Pcb.Count;
    }

    // ###########################################################################################
    // Returns true when the current board enables the special orange pin-1 marker.
    // ###########################################################################################
    private bool IsBoardMarkPin1OnSelectedComponentEnabled()
    {
        if (!this.HasCurrentSchematicKiCadPcbPadData())
        {
            return false;
        }

        var boardKey = this.MainWindow?.GetCurrentBoardKey();
        return !string.IsNullOrWhiteSpace(boardKey) &&
               UserSettings.GetSchematicsMarkPin1OnSelectedComponentForBoard(boardKey);
    }

    // ###########################################################################################
    // Returns true when the supplied pad belongs to a selected or hovered component and should
    // receive the special primary-pin highlight.
    // ###########################################################################################
    private bool ShouldUseSelectedComponentPin1Highlight(
        KiCadPcbFootprint footprint,
        KiCadPcbPad pad,
        string requiredLayer)
    {
        if (!this.IsBoardMarkPin1OnSelectedComponentEnabled())
        {
            return false;
        }

        string normalizedReference = footprint.Reference?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedReference))
        {
            return false;
        }

        bool isTargetComponent =
            this.thisSelectedKiCadReferences.Contains(normalizedReference) ||
            string.Equals(this.thisHoveredComponentBoardLabel, normalizedReference, StringComparison.OrdinalIgnoreCase);

        if (!isTargetComponent)
        {
            return false;
        }

        return TabSchematics.IsPrimaryPadForPin1Highlight(footprint, pad, requiredLayer);
    }

    // ###########################################################################################
    // Returns true when there is a selected or hovered component reference that can receive a
    // special pin-1 marker on the current PCB KiCad overlay.
    // ###########################################################################################
    private bool HasPin1HighlightTargetReference()
    {
        if (!this.IsBoardMarkPin1OnSelectedComponentEnabled())
        {
            return false;
        }

        return this.thisSelectedKiCadReferences.Count > 0 ||
               !string.IsNullOrWhiteSpace(this.thisHoveredComponentBoardLabel);
    }

    // ###########################################################################################
    // Captures the current label-editor working state so it can be restored by undo or redo.
    // ###########################################################################################
    private LabelEditorUndoState CreateLabelEditorUndoState()
    {
        var state = new LabelEditorUndoState
        {
            PrimarySelectedIndex = this.thisSelectedLabelEditorHighlight != null
                ? this.thisLabelEditorWorkingHighlights.IndexOf(this.thisSelectedLabelEditorHighlight)
                : -1
        };

        foreach (var row in this.thisLabelEditorWorkingHighlights)
        {
            state.Highlights.Add(new LabelEditorUndoHighlightState
            {
                SchematicName = row.SchematicName,
                BoardLabel = row.BoardLabel,
                Category = row.Category,
                X = row.X,
                Y = row.Y,
                Width = row.Width,
                Height = row.Height,
                IsSelected = this.thisSelectedLabelEditorHighlights.Contains(row)
            });
        }

        return state;
    }

    // ###########################################################################################
    // Captures the label-editor state as it existed before the active drag started.
    // ###########################################################################################
    private LabelEditorUndoState CreateLabelEditorUndoStateFromOriginalDragState()
    {
        var state = new LabelEditorUndoState
        {
            PrimarySelectedIndex = this.thisSelectedLabelEditorHighlight != null
                ? this.thisLabelEditorWorkingHighlights.IndexOf(this.thisSelectedLabelEditorHighlight)
                : -1
        };

        foreach (var row in this.thisLabelEditorWorkingHighlights)
        {
            Rect rect = this.thisLabelEditorOriginalDragRectangles.TryGetValue(row, out var originalRect)
                ? originalRect
                : new Rect(row.X, row.Y, row.Width, row.Height);

            state.Highlights.Add(new LabelEditorUndoHighlightState
            {
                SchematicName = row.SchematicName,
                BoardLabel = row.BoardLabel,
                Category = row.Category,
                X = rect.X,
                Y = rect.Y,
                Width = rect.Width,
                Height = rect.Height,
                IsSelected = this.thisSelectedLabelEditorHighlights.Contains(row)
            });
        }

        return state;
    }

    // ###########################################################################################
    // Compares two label-editor snapshots so duplicate undo and redo entries can be skipped.
    // ###########################################################################################
    private bool AreLabelEditorUndoStatesEqual(LabelEditorUndoState leftState, LabelEditorUndoState rightState)
    {
        const double epsilon = 0.0001;

        if (ReferenceEquals(leftState, rightState))
        {
            return true;
        }

        if (leftState.Highlights.Count != rightState.Highlights.Count ||
            leftState.PrimarySelectedIndex != rightState.PrimarySelectedIndex)
        {
            return false;
        }

        for (int i = 0; i < leftState.Highlights.Count; i++)
        {
            var left = leftState.Highlights[i];
            var right = rightState.Highlights[i];

            if (!string.Equals(left.SchematicName, right.SchematicName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(left.BoardLabel, right.BoardLabel, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(left.Category, right.Category, StringComparison.OrdinalIgnoreCase) ||
                Math.Abs(left.X - right.X) > epsilon ||
                Math.Abs(left.Y - right.Y) > epsilon ||
                Math.Abs(left.Width - right.Width) > epsilon ||
                Math.Abs(left.Height - right.Height) > epsilon ||
                left.IsSelected != right.IsSelected)
            {
                return false;
            }
        }

        return true;
    }

    // ###########################################################################################
    // Pushes one undo snapshot and clears redo because a new forward edit has been committed.
    // ###########################################################################################
    private void PushLabelEditorUndoState(LabelEditorUndoState state)
    {
        if (this.thisLabelEditorUndoStack.Count > 0 &&
            this.AreLabelEditorUndoStatesEqual(this.thisLabelEditorUndoStack.Peek(), state))
        {
            return;
        }

        this.thisLabelEditorUndoStack.Push(state);
        this.thisLabelEditorRedoStack.Clear();
    }

    // ###########################################################################################
    // Restores one label-editor snapshot back into the working state and refreshes the overlay.
    // ###########################################################################################
    private void RestoreLabelEditorUndoState(LabelEditorUndoState state)
    {
        this.thisLabelEditorWorkingHighlights.Clear();
        this.thisSelectedLabelEditorHighlights.Clear();
        this.thisSelectedLabelEditorHighlight = null;
        this.thisPendingNewLabelEditorHighlight = null;
        this.thisIsDrawingLabelEditorRectangle = false;
        this.thisLabelEditorDraftRectangle = null;
        this.thisLabelEditorDragMode = LabelEditorDragMode.None;
        this.thisLabelEditorOriginalSelectionBounds = default;
        this.thisLabelEditorOriginalDragRectangles.Clear();

        this.HideNewLabelEditorPrompt();

        foreach (var snapshot in state.Highlights)
        {
            var row = new EditableComponentHighlight
            {
                SchematicName = snapshot.SchematicName,
                BoardLabel = snapshot.BoardLabel,
                Category = snapshot.Category,
                X = snapshot.X,
                Y = snapshot.Y,
                Width = snapshot.Width,
                Height = snapshot.Height
            };

            this.thisLabelEditorWorkingHighlights.Add(row);

            if (snapshot.IsSelected)
            {
                this.thisSelectedLabelEditorHighlights.Add(row);
            }
        }

        if (state.PrimarySelectedIndex >= 0 &&
            state.PrimarySelectedIndex < this.thisLabelEditorWorkingHighlights.Count)
        {
            var primary = this.thisLabelEditorWorkingHighlights[state.PrimarySelectedIndex];
            if (this.thisSelectedLabelEditorHighlights.Contains(primary))
            {
                this.thisSelectedLabelEditorHighlight = primary;
            }
        }

        if (this.thisSelectedLabelEditorHighlight == null)
        {
            this.thisSelectedLabelEditorHighlight = this.GetFirstSelectedLabelEditorHighlightForCurrentSchematic();
        }

        this.RefreshLabelEditorOverlay();
        this.SchematicsContainer.Focus();
    }

    // ###########################################################################################
    // Restores the previous label-editor snapshot and moves the current state onto the redo stack.
    // ###########################################################################################
    private bool TryUndoLabelEditorChange()
    {
        if (!this.thisIsLabelEditorMode || this.SchematicsNewLabelPromptBorder.IsVisible)
        {
            return false;
        }

        var currentState = this.CreateLabelEditorUndoState();

        while (this.thisLabelEditorUndoStack.Count > 0 &&
               this.AreLabelEditorUndoStatesEqual(this.thisLabelEditorUndoStack.Peek(), currentState))
        {
            this.thisLabelEditorUndoStack.Pop();
        }

        if (this.thisLabelEditorUndoStack.Count == 0)
        {
            return false;
        }

        var previousState = this.thisLabelEditorUndoStack.Pop();

        if (this.thisLabelEditorRedoStack.Count == 0 ||
            !this.AreLabelEditorUndoStatesEqual(this.thisLabelEditorRedoStack.Peek(), currentState))
        {
            this.thisLabelEditorRedoStack.Push(currentState);
        }

        this.RestoreLabelEditorUndoState(previousState);
        return true;
    }

    // ###########################################################################################
    // Restores the next label-editor snapshot and moves the current state back onto the undo stack.
    // ###########################################################################################
    private bool TryRedoLabelEditorChange()
    {
        if (!this.thisIsLabelEditorMode || this.SchematicsNewLabelPromptBorder.IsVisible)
        {
            return false;
        }

        var currentState = this.CreateLabelEditorUndoState();

        while (this.thisLabelEditorRedoStack.Count > 0 &&
               this.AreLabelEditorUndoStatesEqual(this.thisLabelEditorRedoStack.Peek(), currentState))
        {
            this.thisLabelEditorRedoStack.Pop();
        }

        if (this.thisLabelEditorRedoStack.Count == 0)
        {
            return false;
        }

        var nextState = this.thisLabelEditorRedoStack.Pop();

        if (this.thisLabelEditorUndoStack.Count == 0 ||
            !this.AreLabelEditorUndoStatesEqual(this.thisLabelEditorUndoStack.Peek(), currentState))
        {
            this.thisLabelEditorUndoStack.Push(currentState);
        }

        this.RestoreLabelEditorUndoState(nextState);
        return true;
    }

    // ###########################################################################################
    // Clears the cached editor label visual pool so stale controls are not retained when the
    // component label editor is inactive.
    // ###########################################################################################
    private void ResetEditorComponentLabelVisualCache()
    {
        this.thisEditorLabelContainers.Clear();
        this.thisEditorLabelTextBlocks.Clear();
        this.thisEditorLabelScaleTransforms.Clear();
        this.thisLastEditorLabelVisualSignature = string.Empty;
    }

    // ###########################################################################################
    // Ensures the reusable editor label visual pool contains at least the requested number of
    // controls. Labels are created once and then reused during drag/resize operations.
    // ###########################################################################################
    private void EnsureEditorComponentLabelVisualPoolSize(int requiredCount)
    {
        while (this.thisEditorLabelContainers.Count < requiredCount)
        {
            var textBlock = new TextBlock
            {
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                TextAlignment = TextAlignment.Center
            };
            textBlock.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("Schematics_ComponentLabel_Fg"));

            var innerBorder = new Border
            {
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(4),
                Padding = new Avalonia.Thickness(6, 4),
                Child = textBlock
            };
            innerBorder.Bind(Border.BackgroundProperty, this.GetResourceObservable("Schematics_ComponentLabel_Bg"));
            innerBorder.Bind(Border.BorderBrushProperty, this.GetResourceObservable("Schematics_ComponentLabel_Border"));

            var scaleTransform = new ScaleTransform(1.0, 1.0);
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(scaleTransform);

            var container = new Border
            {
                IsHitTestVisible = false,
                IsVisible = false,
                Child = innerBorder,
                RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                RenderTransform = transformGroup
            };

            this.thisEditorLabelContainers.Add(container);
            this.thisEditorLabelTextBlocks.Add(textBlock);
            this.thisEditorLabelScaleTransforms.Add(scaleTransform);
            this.SchematicsLabelsCanvas.Children.Add(container);
        }
    }

    // ###########################################################################################
    // Builds a lightweight signature for the currently visible editor labels so controls are only
    // re-bound when the count or label text actually changes.
    // ###########################################################################################
    private string BuildEditorComponentLabelVisualSignature(IReadOnlyList<EditableComponentHighlight> rows)
    {
        if (rows.Count == 0)
        {
            return string.Empty;
        }

        var parts = new string[rows.Count];

        for (int i = 0; i < rows.Count; i++)
        {
            parts[i] = rows[i].BoardLabel?.Trim() ?? string.Empty;
        }

        return string.Join("\u001F", parts);
    }

    // ###########################################################################################
    // Updates the reusable editor label controls without clearing and rebuilding the entire canvas
    // on every pointer move. This removes the large allocation churn that caused runaway memory.
    // ###########################################################################################
    private void UpdateEditorComponentLabels(
        IReadOnlyList<EditableComponentHighlight> rows,
        Rect contentRect,
        double imgWidth,
        double imgHeight,
        double inverseScale)
    {
        if (this.thisEditorLabelContainers.Count == 0 && this.SchematicsLabelsCanvas.Children.Count > 0)
        {
            this.SchematicsLabelsCanvas.Children.Clear();
        }

        this.EnsureEditorComponentLabelVisualPoolSize(rows.Count);

        string newSignature = this.BuildEditorComponentLabelVisualSignature(rows);
        bool textChanged = !string.Equals(
            this.thisLastEditorLabelVisualSignature,
            newSignature,
            StringComparison.Ordinal);

        if (textChanged)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                this.thisEditorLabelTextBlocks[i].Text = rows[i].BoardLabel;
            }

            this.thisLastEditorLabelVisualSignature = newSignature;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var container = this.thisEditorLabelContainers[i];
            var scaleTransform = this.thisEditorLabelScaleTransforms[i];

            double centerX = row.X + (row.Width / 2.0);
            double centerY = row.Y + (row.Height / 2.0);

            double localX = contentRect.X + (centerX / imgWidth) * contentRect.Width;
            double localY = contentRect.Y + (centerY / imgHeight) * contentRect.Height;

            scaleTransform.ScaleX = inverseScale;
            scaleTransform.ScaleY = inverseScale;

            bool needsMeasure =
                textChanged ||
                !container.IsVisible ||
                container.DesiredSize.Width <= 0 ||
                container.DesiredSize.Height <= 0;

            container.IsVisible = true;

            if (needsMeasure)
            {
                container.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            }

            Size desiredSize = container.DesiredSize;

            Canvas.SetLeft(container, localX - (desiredSize.Width / 2.0));
            Canvas.SetTop(container, localY - (desiredSize.Height / 2.0));
        }

        for (int i = rows.Count; i < this.thisEditorLabelContainers.Count; i++)
        {
            this.thisEditorLabelContainers[i].IsVisible = false;
        }
    }

    // ###########################################################################################
    // Updates transient editor-overlay state such as hovered handles or snap guides without
    // rebuilding the entire editor model or touching the reusable label pool.
    // ###########################################################################################
    private void SetLabelEditorOverlayTransientState(
        int? hoveredIndex = null,
        IReadOnlyList<(Point Start, Point End)>? snapGuides = null)
    {
        this.SchematicsLabelEditorOverlay.ApplyState(
            rectangles: this.SchematicsLabelEditorOverlay.Rectangles,
            selectedIndex: this.SchematicsLabelEditorOverlay.SelectedIndex,
            selectedIndices: this.SchematicsLabelEditorOverlay.SelectedIndices,
            selectionBounds: this.SchematicsLabelEditorOverlay.SelectionBounds,
            hoveredIndex: hoveredIndex ?? this.SchematicsLabelEditorOverlay.HoveredIndex,
            draftRectangle: this.SchematicsLabelEditorOverlay.DraftRectangle,
            snapGuides: snapGuides ?? this.SchematicsLabelEditorOverlay.SnapGuides,
            bitmapPixelSize: this.SchematicsLabelEditorOverlay.BitmapPixelSize,
            viewMatrix: this.SchematicsLabelEditorOverlay.ViewMatrix,
            highlightColor: this.SchematicsLabelEditorOverlay.HighlightColor,
            highlightOpacity: this.SchematicsLabelEditorOverlay.HighlightOpacity,
            isVisible: this.SchematicsLabelEditorOverlay.IsVisible);
    }

    // ###########################################################################################
    // Clears all cached component-label visual pools so stale controls are not retained when the
    // viewer is reset or when label rendering switches between standard and editor modes.
    // ###########################################################################################
    private void ResetComponentLabelVisualCaches()
    {
        this.thisEditorLabelContainers.Clear();
        this.thisEditorLabelTextBlocks.Clear();
        this.thisEditorLabelScaleTransforms.Clear();
        this.thisLastEditorLabelVisualSignature = string.Empty;

        this.thisStandardLabelContainers.Clear();
        this.thisStandardLabelTextBlocks.Clear();
        this.thisStandardLabelScaleTransforms.Clear();
        this.thisLastStandardLabelVisualSignature = string.Empty;
    }

    // ###########################################################################################
    // Clears the cached standard component-label visual pool so stale controls are not retained
    // when normal label rendering is disabled or the editor takes ownership of the labels canvas.
    // ###########################################################################################
    private void ResetStandardComponentLabelVisualCache()
    {
        this.thisStandardLabelContainers.Clear();
        this.thisStandardLabelTextBlocks.Clear();
        this.thisStandardLabelScaleTransforms.Clear();
        this.thisLastStandardLabelVisualSignature = string.Empty;
    }

    // ###########################################################################################
    // Ensures the reusable standard component-label visual pool contains at least the requested
    // number of controls. Labels are created once and then reused on later refreshes.
    // ###########################################################################################
    private void EnsureStandardComponentLabelVisualPoolSize(int requiredCount)
    {
        while (this.thisStandardLabelContainers.Count < requiredCount)
        {
            var textBlock = new TextBlock
            {
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                TextAlignment = TextAlignment.Center
            };
            textBlock.Bind(TextBlock.ForegroundProperty, this.GetResourceObservable("Schematics_ComponentLabel_Fg"));

            var innerBorder = new Border
            {
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(4),
                Padding = new Avalonia.Thickness(6, 4),
                Child = textBlock
            };
            innerBorder.Bind(Border.BackgroundProperty, this.GetResourceObservable("Schematics_ComponentLabel_Bg"));
            innerBorder.Bind(Border.BorderBrushProperty, this.GetResourceObservable("Schematics_ComponentLabel_Border"));

            var scaleTransform = new ScaleTransform(1.0, 1.0);
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(scaleTransform);

            var container = new Border
            {
                IsHitTestVisible = false,
                IsVisible = false,
                Child = innerBorder,
                RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                RenderTransform = transformGroup
            };

            this.thisStandardLabelContainers.Add(container);
            this.thisStandardLabelTextBlocks.Add(textBlock);
            this.thisStandardLabelScaleTransforms.Add(scaleTransform);
            this.SchematicsLabelsCanvas.Children.Add(container);
        }
    }

    // ###########################################################################################
    // Builds a lightweight signature for the currently visible standard labels so text updates
    // only occur when the visible label set actually changes.
    // ###########################################################################################
    private string BuildStandardComponentLabelVisualSignature(IReadOnlyList<(string Text, double LocalX, double LocalY)> labels)
    {
        if (labels.Count == 0)
        {
            return string.Empty;
        }

        var parts = new string[labels.Count];

        for (int i = 0; i < labels.Count; i++)
        {
            parts[i] = labels[i].Text ?? string.Empty;
        }

        return string.Join("\u001F", parts);
    }

    // ###########################################################################################
    // Updates the reusable standard component-label controls without clearing and rebuilding the
    // entire canvas on every refresh. This removes the same allocation churn pattern that the
    // editor labels originally suffered from.
    // ###########################################################################################
    private void UpdateStandardComponentLabels(
        IReadOnlyList<(string Text, double LocalX, double LocalY)> labels,
        double inverseScale)
    {
        if (this.thisStandardLabelContainers.Count == 0 && this.SchematicsLabelsCanvas.Children.Count > 0)
        {
            this.SchematicsLabelsCanvas.Children.Clear();
        }

        this.EnsureStandardComponentLabelVisualPoolSize(labels.Count);

        string newSignature = this.BuildStandardComponentLabelVisualSignature(labels);
        bool textChanged = !string.Equals(
            this.thisLastStandardLabelVisualSignature,
            newSignature,
            StringComparison.Ordinal);

        if (textChanged)
        {
            for (int i = 0; i < labels.Count; i++)
            {
                this.thisStandardLabelTextBlocks[i].Text = labels[i].Text;
            }

            this.thisLastStandardLabelVisualSignature = newSignature;
        }

        for (int i = 0; i < labels.Count; i++)
        {
            var label = labels[i];
            var container = this.thisStandardLabelContainers[i];
            var scaleTransform = this.thisStandardLabelScaleTransforms[i];

            scaleTransform.ScaleX = inverseScale;
            scaleTransform.ScaleY = inverseScale;

            bool needsMeasure =
                textChanged ||
                !container.IsVisible ||
                container.DesiredSize.Width <= 0 ||
                container.DesiredSize.Height <= 0;

            container.IsVisible = true;

            if (needsMeasure)
            {
                container.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            }

            Size desiredSize = container.DesiredSize;

            Canvas.SetLeft(container, label.LocalX - (desiredSize.Width / 2.0));
            Canvas.SetTop(container, label.LocalY - (desiredSize.Height / 2.0));
        }

        for (int i = labels.Count; i < this.thisStandardLabelContainers.Count; i++)
        {
            this.thisStandardLabelContainers[i].IsVisible = false;
        }
    }

    // ###########################################################################################
    // Builds non-overlapping resize-handle hit rectangles so corner drags keep two-axis behavior
    // even when the selected component is too small for all handle zones to coexist.
    // ###########################################################################################
    private static List<(Rect HitRect, LabelEditorDragMode DragMode)> BuildLabelEditorHandleHitRects(Rect localRect, double scale)
    {
        double handleSize = Math.Clamp(10.0 / scale, 5.0, 18.0);
        double half = handleSize / 2.0;
        double minimumGap = Math.Clamp(2.0 / scale, 1.0, 4.0);

        var hitRects = new List<(Rect HitRect, LabelEditorDragMode DragMode)>(8)
        {
            (new Rect(localRect.Left - half, localRect.Top - half, handleSize, handleSize), LabelEditorDragMode.ResizeTopLeft),
            (new Rect(localRect.Right - half, localRect.Top - half, handleSize, handleSize), LabelEditorDragMode.ResizeTopRight),
            (new Rect(localRect.Right - half, localRect.Bottom - half, handleSize, handleSize), LabelEditorDragMode.ResizeBottomRight),
            (new Rect(localRect.Left - half, localRect.Bottom - half, handleSize, handleSize), LabelEditorDragMode.ResizeBottomLeft)
        };

        double horizontalSideHitLength = Math.Max(0.0, localRect.Width - handleSize - minimumGap);
        if (horizontalSideHitLength > 0.0)
        {
            double horizontalSideLeft = localRect.Center.X - (horizontalSideHitLength / 2.0);

            hitRects.Add((new Rect(horizontalSideLeft, localRect.Top - half, horizontalSideHitLength, handleSize), LabelEditorDragMode.ResizeTop));
            hitRects.Add((new Rect(horizontalSideLeft, localRect.Bottom - half, horizontalSideHitLength, handleSize), LabelEditorDragMode.ResizeBottom));
        }

        double verticalSideHitLength = Math.Max(0.0, localRect.Height - handleSize - minimumGap);
        if (verticalSideHitLength > 0.0)
        {
            double verticalSideTop = localRect.Center.Y - (verticalSideHitLength / 2.0);

            hitRects.Add((new Rect(localRect.Right - half, verticalSideTop, handleSize, verticalSideHitLength), LabelEditorDragMode.ResizeRight));
            hitRects.Add((new Rect(localRect.Left - half, verticalSideTop, handleSize, verticalSideHitLength), LabelEditorDragMode.ResizeLeft));
        }

        return hitRects;
    }

    // ###########################################################################################
    // Compatibility overload used by cursor and hover logic.
    // ###########################################################################################
    private bool TryGetSelectedLabelEditorHandleAtContainerPoint(Point pointerInContainer, out LabelEditorDragMode dragMode)
    {
        return this.TryGetSelectedLabelEditorHandleAtContainerPoint(pointerInContainer, out _, out dragMode);
    }

    // ###########################################################################################
    // Applies the persisted schematics/thumbnail split ratio for the supplied board key.
    // Called early so the splitter does not flash at the default centered position.
    // ###########################################################################################
    public void ApplySchematicsSplitterRatio(string boardKey)
    {
        double ratio = Math.Clamp(
            string.IsNullOrWhiteSpace(boardKey)
                ? 0.5
                : UserSettings.GetSchematicsSplitterRatio(boardKey),
            0.05,
            0.95);

        this.SchematicsInnerGrid.ColumnDefinitions[0].Width = new GridLength(ratio * 100.0, GridUnitType.Star);
        this.SchematicsInnerGrid.ColumnDefinitions[2].Width = new GridLength((1.0 - ratio) * 100.0, GridUnitType.Star);
    }

    // ###########################################################################################
    // Clears any schematic-only selections that were created for components hidden by the current
    // category or search filters.
    // ###########################################################################################
    internal void ClearSchematicsOnlySelectedComponents()
    {
        this.thisSchematicsOnlySelectedBoardLabels.Clear();
    }

    // ###########################################################################################
    // Returns true when the board label is selected either through the visible component list or
    // through the schematic-only hidden-selection cache.
    // ###########################################################################################
    private bool IsComponentBoardLabelSelected(string boardLabel)
    {
        if (string.IsNullOrWhiteSpace(boardLabel))
        {
            return false;
        }

        if (this.thisSchematicsOnlySelectedBoardLabels.Contains(boardLabel))
        {
            return true;
        }

        return this.MainWindow?.ComponentFilterListBox.SelectedItems?
            .Cast<Main.ComponentListItem>()
            .Any(item => string.Equals(item.BoardLabel, boardLabel, StringComparison.OrdinalIgnoreCase)) ?? false;
    }

    // ###########################################################################################
    // Removes schematic-only selections for any labels that are currently visible in the filtered
    // component list so the visible list remains the active source of truth for those rows.
    // ###########################################################################################
    private void ClearVisibleBoardLabelsFromSchematicsOnlySelection()
    {
        if (this.MainWindow == null)
        {
            return;
        }

        foreach (var item in this.MainWindow.ComponentFilterListBox.ItemsSource?.Cast<Main.ComponentListItem>() ?? Enumerable.Empty<Main.ComponentListItem>())
        {
            if (!string.IsNullOrWhiteSpace(item.BoardLabel))
            {
                this.thisSchematicsOnlySelectedBoardLabels.Remove(item.BoardLabel);
            }
        }
    }

    // ###########################################################################################
    // Rebuilds highlights from the visible component-list selection while preserving any extra
    // schematic-only selections for components hidden by category or search filters.
    // ###########################################################################################
    private void RefreshHighlightsFromCurrentComponentSelection()
    {
        var boardLabels = this.MainWindow?.ComponentFilterListBox.SelectedItems?
            .Cast<Main.ComponentListItem>()
            .Select(item => item.BoardLabel)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        this.UpdateHighlightsForComponents(boardLabels);
    }

    // ###########################################################################################
    // Resets the lightweight throttle state used for KiCad hover hit-testing.
    // ###########################################################################################
    private void ResetKiCadHoverHitTestThrottle()
    {
        this.thisLastKiCadHoverHitTestContainerPoint = new Point(double.NaN, double.NaN);
        this.thisLastKiCadHoverHitTestTimestamp = 0;
    }

    // ###########################################################################################
    // Limits how often expensive KiCad hover hit-tests can run while the pointer is moving.
    // This keeps dense PCB overlays responsive during fast pan and pointer motion.
    // ###########################################################################################
    private bool ShouldProcessKiCadHoverHitTest(Point pointerInContainer)
    {
        const double minimumDistance = 3.0;
        const double minimumIntervalMilliseconds = 16.0;

        long now = Stopwatch.GetTimestamp();

        if (double.IsNaN(this.thisLastKiCadHoverHitTestContainerPoint.X) ||
            double.IsNaN(this.thisLastKiCadHoverHitTestContainerPoint.Y))
        {
            this.thisLastKiCadHoverHitTestContainerPoint = pointerInContainer;
            this.thisLastKiCadHoverHitTestTimestamp = now;
            return true;
        }

        double dx = pointerInContainer.X - this.thisLastKiCadHoverHitTestContainerPoint.X;
        double dy = pointerInContainer.Y - this.thisLastKiCadHoverHitTestContainerPoint.Y;
        double distance = Math.Sqrt((dx * dx) + (dy * dy));

        double elapsedMilliseconds =
            this.thisLastKiCadHoverHitTestTimestamp == 0
                ? double.MaxValue
                : ((now - this.thisLastKiCadHoverHitTestTimestamp) * 1000.0) / Stopwatch.Frequency;

        if (distance < minimumDistance && elapsedMilliseconds < minimumIntervalMilliseconds)
        {
            return false;
        }

        this.thisLastKiCadHoverHitTestContainerPoint = pointerInContainer;
        this.thisLastKiCadHoverHitTestTimestamp = now;
        return true;
    }

    // ###########################################################################################
    // Builds a stable signature for thumbnail highlight state so blink-only updates do not
    // regenerate all thumbnail bitmaps when the actual selection set has not changed.
    // ###########################################################################################
    private string BuildThumbnailHighlightSignature(bool hasComponentSelection, bool hasKiCadSelection)
    {
        string componentPart = hasComponentSelection
            ? string.Join(
                "\u001E",
                this.highlightIndexBySchematic.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            : string.Empty;

        string selectedNetPart = hasKiCadSelection
            ? string.Join(
                "\u001E",
                this.thisSelectedKiCadNormalizedNetNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            : string.Empty;

        string lockedNetPart = hasKiCadSelection
            ? string.Join(
                "\u001E",
                this.thisLockedKiCadNetNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            : string.Empty;

        return string.Join("\u001F", componentPart, selectedNetPart, lockedNetPart);
    }

    // ###########################################################################################
    // Builds a stable cache key for one PCB net graph on one board side.
    // ###########################################################################################
    private static string BuildKiCadPcbNetRenderCacheKey(int pcbIndex, int netId, string requiredLayer)
    {
        return string.Join(
            "\u001F",
            pcbIndex.ToString(CultureInfo.InvariantCulture),
            netId.ToString(CultureInfo.InvariantCulture),
            requiredLayer.Trim());
    }

    // ###########################################################################################
    // Returns the cached PCB net graph for the requested net/layer, building it once on demand.
    // ###########################################################################################
    private KiCadPcbNetRenderCache GetOrCreateKiCadPcbNetRenderCache(
        KiCadPcb pcb,
        int pcbIndex,
        int netId,
        KiCadPcbHighlightBucket bucket,
        string requiredLayer)
    {
        string cacheKey = TabSchematics.BuildKiCadPcbNetRenderCacheKey(pcbIndex, netId, requiredLayer);

        if (this.thisKiCadPcbNetRenderCacheByKey.TryGetValue(cacheKey, out var cache))
        {
            return cache;
        }

        cache = this.BuildKiCadPcbNetRenderCache(pcb, bucket, requiredLayer);
        this.thisKiCadPcbNetRenderCacheByKey[cacheKey] = cache;
        return cache;
    }

    // ###########################################################################################
    // Builds one cached PCB net graph containing pads, segments, vias, arcs, and adjacency.
    // This is the expensive part that should not run on every overlay refresh.
    // ###########################################################################################
    private KiCadPcbNetRenderCache BuildKiCadPcbNetRenderCache(
        KiCadPcb pcb,
        KiCadPcbHighlightBucket bucket,
        string requiredLayer)
    {
        var cache = new KiCadPcbNetRenderCache();

        int idCounter = 0;

        foreach (var padRef in bucket.Pads)
        {
            if (padRef.FootprintIndex < 0 || padRef.FootprintIndex >= pcb.Footprints.Count)
            {
                continue;
            }

            var footprint = pcb.Footprints[padRef.FootprintIndex];
            if (padRef.PadIndex < 0 || padRef.PadIndex >= footprint.Pads.Count)
            {
                continue;
            }

            var pad = footprint.Pads[padRef.PadIndex];
            if (pad.AbsoluteCenter == null ||
                !TabSchematics.IsKiCadPcbPointVisibleOnSide(pad.Layers, requiredLayer))
            {
                continue;
            }

            var info = new KiCadGraphNode
            {
                Id = $"P{idCounter++}",
                PadRef = padRef
            };

            cache.NodesById[info.Id] = info;
            cache.AllNodeIds.Add(info.Id);
            cache.PadReferenceByNodeId[info.Id] = footprint.Reference?.Trim() ?? string.Empty;

            cache.PadNodes.Add(new KiCadPcbPadRenderNode
            {
                Info = info,
                Footprint = footprint,
                Pad = pad,
                CenterWorld = new Point(pad.AbsoluteCenter.X, pad.AbsoluteCenter.Y),
                RadiusWorld = Math.Max(pad.Size?.X ?? 1.2, pad.Size?.Y ?? 1.2) / 2.0
            });
        }

        foreach (int segmentIndex in bucket.Segments)
        {
            if (segmentIndex < 0 || segmentIndex >= pcb.Routing.Segments.Count)
            {
                continue;
            }

            var segment = pcb.Routing.Segments[segmentIndex];
            if (segment.Start == null ||
                segment.End == null ||
                !string.Equals(segment.Layer?.Trim(), requiredLayer, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var info = new KiCadGraphNode
            {
                Id = $"S{idCounter++}",
                SegmentIndex = segmentIndex
            };

            cache.NodesById[info.Id] = info;
            cache.AllNodeIds.Add(info.Id);

            cache.SegmentNodes.Add(new KiCadPcbSegmentRenderNode
            {
                Info = info,
                StartWorld = new Point(segment.Start.X, segment.Start.Y),
                EndWorld = new Point(segment.End.X, segment.End.Y),
                WidthWorld = segment.Width ?? 0.25
            });
        }

        foreach (int viaIndex in bucket.Vias)
        {
            if (viaIndex < 0 || viaIndex >= pcb.Routing.Vias.Count)
            {
                continue;
            }

            var via = pcb.Routing.Vias[viaIndex];
            if (via.At == null ||
                !TabSchematics.IsKiCadPcbPointVisibleOnSide(via.Layers, requiredLayer))
            {
                continue;
            }

            var info = new KiCadGraphNode
            {
                Id = $"V{idCounter++}",
                ViaIndex = viaIndex
            };

            cache.NodesById[info.Id] = info;
            cache.AllNodeIds.Add(info.Id);

            cache.ViaNodes.Add(new KiCadPcbViaRenderNode
            {
                Info = info,
                CenterWorld = new Point(via.At.X, via.At.Y),
                DiameterWorld = via.Size ?? 0.8
            });
        }

        foreach (int arcIndex in bucket.Arcs)
        {
            if (arcIndex < 0 || arcIndex >= pcb.Routing.Arcs.Count)
            {
                continue;
            }

            var arc = pcb.Routing.Arcs[arcIndex];
            if (arc.Start == null ||
                arc.Mid == null ||
                arc.End == null ||
                !string.Equals(arc.Layer?.Trim(), requiredLayer, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var info = new KiCadGraphNode
            {
                Id = $"A{idCounter++}",
                ArcIndex = arcIndex
            };

            cache.NodesById[info.Id] = info;
            cache.AllNodeIds.Add(info.Id);

            cache.ArcNodes.Add(new KiCadPcbArcRenderNode
            {
                Info = info,
                StartWorld = new Point(arc.Start.X, arc.Start.Y),
                MidWorld = new Point(arc.Mid.X, arc.Mid.Y),
                EndWorld = new Point(arc.End.X, arc.End.Y),
                WidthWorld = arc.Width ?? 0.25
            });
        }

        void AddEdge(string id1, string id2)
        {
            if (!cache.AdjacencyByNodeId.TryGetValue(id1, out var set1))
            {
                set1 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                cache.AdjacencyByNodeId[id1] = set1;
            }

            set1.Add(id2);

            if (!cache.AdjacencyByNodeId.TryGetValue(id2, out var set2))
            {
                set2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                cache.AdjacencyByNodeId[id2] = set2;
            }

            set2.Add(id1);
        }

        for (int i = 0; i < cache.SegmentNodes.Count; i++)
        {
            for (int j = i + 1; j < cache.SegmentNodes.Count; j++)
            {
                var s1 = cache.SegmentNodes[i];
                var s2 = cache.SegmentNodes[j];

                double dist = Math.Min(
                    Math.Min(
                        TabSchematics.DistanceToSegment(s1.StartWorld, s2.StartWorld.X, s2.StartWorld.Y, s2.EndWorld.X, s2.EndWorld.Y),
                        TabSchematics.DistanceToSegment(s1.EndWorld, s2.StartWorld.X, s2.StartWorld.Y, s2.EndWorld.X, s2.EndWorld.Y)),
                    Math.Min(
                        TabSchematics.DistanceToSegment(s2.StartWorld, s1.StartWorld.X, s1.StartWorld.Y, s1.EndWorld.X, s1.EndWorld.Y),
                        TabSchematics.DistanceToSegment(s2.EndWorld, s1.StartWorld.X, s1.StartWorld.Y, s1.EndWorld.X, s1.EndWorld.Y)));

                if (dist <= (s1.WidthWorld / 2.0) + (s2.WidthWorld / 2.0) + 0.05)
                {
                    AddEdge(s1.Info.Id, s2.Info.Id);
                }
            }
        }

        foreach (var padNode in cache.PadNodes)
        {
            foreach (var segmentNode in cache.SegmentNodes)
            {
                if (TabSchematics.DistanceToSegment(
                        padNode.CenterWorld,
                        segmentNode.StartWorld.X,
                        segmentNode.StartWorld.Y,
                        segmentNode.EndWorld.X,
                        segmentNode.EndWorld.Y) <= padNode.RadiusWorld + (segmentNode.WidthWorld / 2.0) + 0.05)
                {
                    AddEdge(padNode.Info.Id, segmentNode.Info.Id);
                }
            }
        }

        foreach (var padNode in cache.PadNodes)
        {
            foreach (var viaNode in cache.ViaNodes)
            {
                double dx = padNode.CenterWorld.X - viaNode.CenterWorld.X;
                double dy = padNode.CenterWorld.Y - viaNode.CenterWorld.Y;
                double dist = Math.Sqrt((dx * dx) + (dy * dy));

                if (dist <= padNode.RadiusWorld + (viaNode.DiameterWorld / 2.0) + 0.05)
                {
                    AddEdge(padNode.Info.Id, viaNode.Info.Id);
                }
            }
        }

        foreach (var viaNode in cache.ViaNodes)
        {
            foreach (var segmentNode in cache.SegmentNodes)
            {
                if (TabSchematics.DistanceToSegment(
                        viaNode.CenterWorld,
                        segmentNode.StartWorld.X,
                        segmentNode.StartWorld.Y,
                        segmentNode.EndWorld.X,
                        segmentNode.EndWorld.Y) <= (viaNode.DiameterWorld / 2.0) + (segmentNode.WidthWorld / 2.0) + 0.05)
                {
                    AddEdge(viaNode.Info.Id, segmentNode.Info.Id);
                }
            }
        }

        foreach (var arcNode in cache.ArcNodes)
        {
            foreach (var segmentNode in cache.SegmentNodes)
            {
                double dist = Math.Min(
                    TabSchematics.DistanceToSegment(
                        arcNode.StartWorld,
                        segmentNode.StartWorld.X,
                        segmentNode.StartWorld.Y,
                        segmentNode.EndWorld.X,
                        segmentNode.EndWorld.Y),
                    Math.Min(
                        TabSchematics.DistanceToSegment(
                            arcNode.MidWorld,
                            segmentNode.StartWorld.X,
                            segmentNode.StartWorld.Y,
                            segmentNode.EndWorld.X,
                            segmentNode.EndWorld.Y),
                        TabSchematics.DistanceToSegment(
                            arcNode.EndWorld,
                            segmentNode.StartWorld.X,
                            segmentNode.StartWorld.Y,
                            segmentNode.EndWorld.X,
                            segmentNode.EndWorld.Y)));

                if (dist <= (arcNode.WidthWorld / 2.0) + (segmentNode.WidthWorld / 2.0) + 0.05)
                {
                    AddEdge(arcNode.Info.Id, segmentNode.Info.Id);
                }
            }

            foreach (var padNode in cache.PadNodes)
            {
                double dist = Math.Min(
                    Math.Sqrt(Math.Pow(padNode.CenterWorld.X - arcNode.StartWorld.X, 2) + Math.Pow(padNode.CenterWorld.Y - arcNode.StartWorld.Y, 2)),
                    Math.Min(
                        Math.Sqrt(Math.Pow(padNode.CenterWorld.X - arcNode.MidWorld.X, 2) + Math.Pow(padNode.CenterWorld.Y - arcNode.MidWorld.Y, 2)),
                        Math.Sqrt(Math.Pow(padNode.CenterWorld.X - arcNode.EndWorld.X, 2) + Math.Pow(padNode.CenterWorld.Y - arcNode.EndWorld.Y, 2))));

                if (dist <= padNode.RadiusWorld + (arcNode.WidthWorld / 2.0) + 0.05)
                {
                    AddEdge(padNode.Info.Id, arcNode.Info.Id);
                }
            }

            foreach (var viaNode in cache.ViaNodes)
            {
                double dist = Math.Min(
                    Math.Sqrt(Math.Pow(viaNode.CenterWorld.X - arcNode.StartWorld.X, 2) + Math.Pow(viaNode.CenterWorld.Y - arcNode.StartWorld.Y, 2)),
                    Math.Min(
                        Math.Sqrt(Math.Pow(viaNode.CenterWorld.X - arcNode.MidWorld.X, 2) + Math.Pow(viaNode.CenterWorld.Y - arcNode.MidWorld.Y, 2)),
                        Math.Sqrt(Math.Pow(viaNode.CenterWorld.X - arcNode.EndWorld.X, 2) + Math.Pow(viaNode.CenterWorld.Y - arcNode.EndWorld.Y, 2))));

                if (dist <= (viaNode.DiameterWorld / 2.0) + (arcNode.WidthWorld / 2.0) + 0.05)
                {
                    AddEdge(viaNode.Info.Id, arcNode.Info.Id);
                }
            }
        }

        return cache;
    }

    // ###########################################################################################
    // Resolves the currently drawable node ids from a cached PCB net graph.
    // Explicit hover/lock draws the whole net, while selection-derived rendering starts from the
    // selected component pads and stops traversal at foreign pads.
    // ###########################################################################################
    private HashSet<string> BuildKiCadPcbActiveDrawIds(KiCadPcbNetRenderCache cache, bool isExplicitHighlight)
    {
        if (isExplicitHighlight)
        {
            return new HashSet<string>(cache.AllNodeIds, StringComparer.OrdinalIgnoreCase);
        }

        var activeDrawIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();

        foreach (var padNode in cache.PadNodes)
        {
            string reference = padNode.Footprint.Reference?.Trim() ?? string.Empty;
            bool isTargetPad = this.thisSelectedKiCadReferences.Count == 0 ||
                               this.thisSelectedKiCadReferences.Contains(reference);

            if (!isTargetPad)
            {
                continue;
            }

            if (activeDrawIds.Add(padNode.Info.Id))
            {
                queue.Enqueue(padNode.Info.Id);
            }
        }

        while (queue.Count > 0)
        {
            string currentId = queue.Dequeue();

            if (!cache.AdjacencyByNodeId.TryGetValue(currentId, out var neighbors))
            {
                continue;
            }

            foreach (string neighborId in neighbors)
            {
                if (!activeDrawIds.Add(neighborId))
                {
                    continue;
                }

                bool isForeignPad =
                    cache.PadReferenceByNodeId.TryGetValue(neighborId, out string? reference) &&
                    this.thisSelectedKiCadReferences.Count > 0 &&
                    !this.thisSelectedKiCadReferences.Contains(reference);

                if (!isForeignPad)
                {
                    queue.Enqueue(neighborId);
                }
            }
        }

        return activeDrawIds;
    }

}