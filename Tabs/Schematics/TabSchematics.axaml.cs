using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Handlers.DataHandling;
using System;
using System.Collections;
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

    public bool IsLabelEditorActive => this.thisIsLabelEditorMode || this.thisIsKiCadTraceCalibrationMode;

    // Zoom
    internal Matrix schematicsMatrix = Matrix.Identity;

    // Thumbnails
    internal ObservableCollection<SchematicThumbnail> currentThumbnails = new();

    // Full-res viewer
    internal Bitmap? currentFullResBitmap;
    internal CancellationTokenSource? fullResLoadCts;

    private bool thisIsKiCadTraceCalibrationMode;
    private double thisKiCadCalibrationImageLeft;
    private double thisKiCadCalibrationImageTop;
    private double thisKiCadCalibrationImageRight;
    private double thisKiCadCalibrationImageBottom;
    private double thisKiCadCalibrationStartImageLeft;
    private double thisKiCadCalibrationStartImageTop;
    private double thisKiCadCalibrationStartImageRight;
    private double thisKiCadCalibrationStartImageBottom;
    private LabelEditorDragMode thisKiCadTraceCalibrationDragMode;
    private Point thisKiCadTraceCalibrationDragStartPixelPoint;

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
    private string? thisHoveredKiCadNetName;
    private string? thisHoveredKiCadPadNumber;
    private readonly HashSet<string> thisLockedKiCadNetNames = new(StringComparer.OrdinalIgnoreCase);
    private bool thisIsKiCadOverlayRefreshQueued;
    private int thisKiCadOverlayRefreshRequestVersion;
    private int thisKiCadOverlayLastRenderedVersion;
    private bool thisIsInteractiveCadTraceHoverShiftPressed;
    private readonly Dictionary<string, HashSet<string>> thisImportantSignalNetNamesByDisplayName =
    new(StringComparer.OrdinalIgnoreCase);
    private readonly object thisKiCadPcbNetRenderCacheSync = new();
    private readonly object thisKiCadPcbHoverHitTestCacheSync = new();
    private readonly Dictionary<string, Task> thisKiCadPcbNetRenderBuildTaskByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task> thisKiCadPcbHoverHitTestBuildTaskByKey = new(StringComparer.OrdinalIgnoreCase);
    private string thisCurrentKiCadRuntimeCacheScopeKey = string.Empty;
    private readonly LinkedList<string> thisKiCadRuntimeCacheScopeLru = new();
    private readonly Dictionary<string, KiCadRuntimeCacheScope> thisKiCadRuntimeCacheScopeByKey =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> thisImportantSignalDisplayNames = new();
    private int thisKiCadProjectLoadVersion;
    private readonly HashSet<string> thisSelectedImportantSignalDisplayNames =
        new(StringComparer.OrdinalIgnoreCase);

    private string? thisHoveredComponentBoardLabel;
    private readonly HashSet<string> thisSchematicsOnlySelectedBoardLabels = new(StringComparer.OrdinalIgnoreCase);
    private bool thisSuppressBoardSettingsChanged;
    private bool thisSuppressGlobalSettingsChanged;
    private PointerPressedEventArgs? thisThumbnailDragStartEventArgs;
    private readonly List<Border> thisEditorLabelContainers = new();
    private readonly List<TextBlock> thisEditorLabelTextBlocks = new();
    private readonly List<ScaleTransform> thisEditorLabelScaleTransforms = new();
    private string thisLastEditorLabelVisualSignature = string.Empty;
    private readonly List<Border> thisStandardLabelContainers = new();
    private readonly List<TextBlock> thisStandardLabelTextBlocks = new();
    private readonly List<ScaleTransform> thisStandardLabelScaleTransforms = new();
    private string thisLastStandardLabelVisualSignature = string.Empty;
    private string thisLastCreatedLabelEditorCategory = string.Empty;
    private string thisLabelEditorSearchText = string.Empty;

    private Point thisLastKiCadHoverHitTestContainerPoint = new(double.NaN, double.NaN);
    private long thisLastKiCadHoverHitTestTimestamp;
    private string thisLastKiCadNetConnectionsSignature = string.Empty;
    private string thisLastThumbnailHighlightSignature = string.Empty;

    private readonly Dictionary<string, KiCadPcbNetRenderCache> thisKiCadPcbNetRenderCacheByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, KiCadPcbHoverHitTestCache> thisKiCadPcbHoverHitTestCacheByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, KiCadSchematicHoverHitTestCache> thisKiCadSchematicHoverHitTestCacheByKey =
        new(StringComparer.OrdinalIgnoreCase);


    private sealed class KiCadSchematicHoverLabelCandidate
    {
        public string NormalizedNetName { get; init; } = string.Empty;
        public Point LocalPoint { get; init; }
    }

    private sealed class KiCadSchematicHoverSegmentCandidate
    {
        public string NormalizedNetName { get; init; } = string.Empty;
        public Point StartLocal { get; init; }
        public Point EndLocal { get; init; }
    }

    private sealed class KiCadSchematicHoverHitTestCache
    {
        public double CellSizeLocal { get; init; } = 24.0;
        public List<KiCadSchematicHoverLabelCandidate> LabelCandidates { get; init; } = new();
        public List<KiCadSchematicHoverSegmentCandidate> SegmentCandidates { get; init; } = new();
        public Dictionary<long, List<int>> LabelIndicesByCell { get; init; } = new();
        public Dictionary<long, List<int>> SegmentIndicesByCell { get; init; } = new();
    }

    private sealed class ImportantSignalListItem
    {
        public string DisplayName { get; init; } = string.Empty;
        public string ToolTipText { get; init; } = string.Empty;

        public override string ToString()
        {
            return this.DisplayName;
        }
    }

    private sealed class KiCadRuntimeCacheScope
    {
        public Dictionary<string, KiCadPcbNetRenderCache> NetRenderCacheByKey { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, KiCadPcbHoverHitTestCache> HoverHitTestCacheByKey { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, Task> NetRenderBuildTaskByKey { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, Task> HoverHitTestBuildTaskByKey { get; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class KiCadPcbHoverPadCandidate
    {
        public KiCadNetRef Net { get; init; } = null!;
        public string PadNumber { get; init; } = string.Empty;
        public Point CenterWorld { get; init; }
        public double HitRadiusWorld { get; init; }
    }

    private sealed class KiCadPcbHoverSegmentCandidate
    {
        public KiCadNetRef Net { get; init; } = null!;
        public Point StartWorld { get; init; }
        public Point EndWorld { get; init; }
        public double HitRadiusWorld { get; init; }
    }

    private sealed class KiCadPcbHoverViaCandidate
    {
        public KiCadNetRef Net { get; init; } = null!;
        public Point CenterWorld { get; init; }
        public double HitRadiusWorld { get; init; }
    }

    private sealed class KiCadPcbHoverZoneCandidate
    {
        public KiCadNetRef Net { get; init; } = null!;
        public IReadOnlyList<IReadOnlyList<Point>> PolygonsWorld { get; init; } = Array.Empty<IReadOnlyList<Point>>();
        public Rect BoundsWorld { get; init; }
    }

    private sealed class KiCadPcbZoneRenderNode
    {
        public KiCadGraphNode Info { get; init; } = new();
        public KiCadPcbZone Zone { get; init; } = null!;
        public IReadOnlyList<IReadOnlyList<Point>> PolygonsWorld { get; init; } = Array.Empty<IReadOnlyList<Point>>();
        public Rect BoundsWorld { get; init; }
    }

    private sealed class KiCadPcbHoverHitTestCache
    {
        public double CellSizeWorld { get; init; } = 2.0;
        public double MaxHitRadiusWorld { get; set; } = 0.8;

        public List<KiCadPcbHoverPadCandidate> PadCandidates { get; init; } = new();
        public List<KiCadPcbHoverSegmentCandidate> SegmentCandidates { get; init; } = new();
        public List<KiCadPcbHoverViaCandidate> ViaCandidates { get; init; } = new();
        public List<KiCadPcbHoverZoneCandidate> ZoneCandidates { get; init; } = new();

        public Dictionary<long, List<int>> PadIndicesByCell { get; init; } = new();
        public Dictionary<long, List<int>> SegmentIndicesByCell { get; init; } = new();
        public Dictionary<long, List<int>> ViaIndicesByCell { get; init; } = new();
        public Dictionary<long, List<int>> ZoneIndicesByCell { get; init; } = new();
    }

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
        public List<KiCadPcbZoneRenderNode> ZoneNodes { get; init; } = new();
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

        public double ScaleX { get; init; } = 1.0;
        public double ScaleY { get; init; } = 1.0;
        public double OffsetX { get; init; }
        public double OffsetY { get; init; }
        public bool MirrorX { get; init; }
        public bool MirrorY { get; init; }
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

    // ###########################################################################################
    // Initializes the schematics tab control and wires the shared UI actions used by the viewer,
    // label editor, and KiCad calibration workflows.
    // ###########################################################################################
    public TabSchematics()
    {
        InitializeComponent();

        this.ClearImportantSignalsButton.Click += (_, _) => this.ClearImportantSignalsSelection();

        this.EnableLabelEditorButton.Click += (_, _) => this.BeginLabelEditorMode();
        this.CancelLabelEditorChangesButton.Click += (_, _) => this.CancelLabelEditorChanges();
        this.ApplyLabelEditorChangesButton.Click += (_, _) => this.ApplyLabelEditorChanges();

        this.ConfirmNewLabelEditorBoardLabelButton.Click += (_, _) => this.ConfirmNewLabelEditorPrompt();
        this.CancelNewLabelEditorBoardLabelButton.Click += (_, _) => this.CancelNewLabelEditorPrompt();

        this.NewLabelEditorBoardLabelTextBox.KeyDown += this.OnNewLabelEditorPromptKeyDown;
        this.NewLabelEditorCategoryComboBox.KeyDown += this.OnNewLabelEditorPromptKeyDown;

        this.ClearKiCadTraceSelectionButton.Click += (_, _) => this.ClearAllKiCadTraceSelections();

        this.BeginKiCadTraceCalibrationButton.Click += (_, _) => this.BeginKiCadTraceCalibrationMode();
        this.ApplyKiCadTraceCalibrationButton.Click += (_, _) => this.ApplyKiCadTraceCalibration();
        this.CancelKiCadTraceCalibrationButton.Click += (_, _) => this.CancelKiCadTraceCalibrationMode();

        this.CheckGlobalShowCalibrationTracesAndPads.IsChecked = true;
        this.CheckGlobalShowCalibrationTracesAndPads.IsCheckedChanged += (_, _) =>
        {
            if (this.thisIsKiCadTraceCalibrationMode)
            {
                this.RefreshKiCadOverlay(forceImmediate: true);
            }
        };
    }

    // ###########################################################################################
    // Handles row clicks for the temporary KiCad calibration visibility toggle that hides or shows
    // the rendered traces and pads while keeping the calibration box visible.
    // ###########################################################################################
    private void OnGlobalShowCalibrationTracesAndPadsRowClicked(object? sender, PointerPressedEventArgs e)
    {
        if (!this.CheckGlobalShowCalibrationTracesAndPads.IsEnabled)
        {
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.CheckGlobalShowCalibrationTracesAndPads.IsChecked =
                this.CheckGlobalShowCalibrationTracesAndPads.IsChecked != true;

            e.Handled = true;
        }
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

    public void Initialize(Main mainWindow)
    {
        this.MainWindow = mainWindow;

        var thisPinchGestureRecognizer = new PinchGestureRecognizer();
        this.SchematicsContainer.GestureRecognizers.Add(thisPinchGestureRecognizer);

        var thisScrollGestureRecognizer = new ScrollGestureRecognizer
        {
            CanHorizontallyScroll = true,
            CanVerticallyScroll = true
        };
        this.SchematicsContainer.GestureRecognizers.Add(thisScrollGestureRecognizer);

        this.SchematicsContainer.Pinch += this.OnSchematicsPinch;
        this.SchematicsContainer.ScrollGesture += this.OnSchematicsScrollGesture;

        // Restore initial states from User Settings
        this.CheckLabelBoard.IsChecked = UserSettings.SchematicsLabelBoard;
        this.CheckLabelTechnical.IsChecked = UserSettings.SchematicsLabelTechnical;
        this.CheckLabelFriendly.IsChecked = UserSettings.SchematicsLabelFriendly;
        this.CheckLabelSelectedOnly.IsChecked = UserSettings.SchematicsLabelSelectedOnly;

        this.thisSuppressBoardSettingsChanged = true;
        this.thisSuppressGlobalSettingsChanged = true;
        this.CheckBoardMarkPin1OnSelectedComponent.IsChecked = true;
        this.CheckBoardShowTracesOnSelectedComponent.IsChecked = UserSettings.SchematicsShowTracesOnSelectedComponent;
        this.CheckGlobalShowTracesOnComponentSelect.IsChecked = UserSettings.SchematicsShowTracesOnComponentSelect;
        this.CheckGlobalShowOppositeSideTraces.IsChecked = UserSettings.SchematicsShowOppositeSideTraces;
        this.CheckGlobalShowZones.IsChecked = UserSettings.SchematicsShowZones;
        this.CheckGlobalHoverHighlightsTraces.IsChecked = true;
        this.CheckBoardContributorMode.IsChecked = UserSettings.ContributorMode;
        this.thisSuppressGlobalSettingsChanged = false;
        this.thisSuppressBoardSettingsChanged = false;

        this.UpdateGlobalSettingsControls();

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

        bool isGlobalSettingsExpanded = UserSettings.SchematicsGlobalSettingsPanelExpanded;
        this.GlobalSettingsListPanel.IsVisible = isGlobalSettingsExpanded;
        this.ToggleGlobalSettingsPanelButton.Content = isGlobalSettingsExpanded ? "Collapse" : "Expand";

        bool isImportantSignalsExpanded = UserSettings.SchematicsImportantSignalsPanelExpanded;
        this.UpdateImportantSignalsPanelExpandedState(isImportantSignalsExpanded);
        this.UpdateImportantSignalsClearButtonState(false);

        bool isKiCadNetConnectionsExpanded = UserSettings.SchematicsNetConnectionsPanelExpanded;
        this.UpdateKiCadNetConnectionsPanelExpandedState(isKiCadNetConnectionsExpanded);

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

        this.CheckBoardShowTracesOnSelectedComponent.IsCheckedChanged += (s, e) =>
        {
            if (this.thisSuppressBoardSettingsChanged)
            {
                return;
            }

            UserSettings.SchematicsShowTracesOnSelectedComponent =
                this.CheckBoardShowTracesOnSelectedComponent.IsChecked == true;

            this.RefreshKiCadOverlay(forceImmediate: true);
        };

        this.CheckGlobalShowOppositeSideTraces.IsCheckedChanged += (s, e) =>
        {
            if (this.thisSuppressGlobalSettingsChanged)
            {
                return;
            }

            UserSettings.SchematicsShowOppositeSideTraces =
                this.CheckGlobalShowOppositeSideTraces.IsChecked == true;

            this.RefreshKiCadOverlay(forceImmediate: true);
        };

        this.CheckGlobalShowZones.IsCheckedChanged += (s, e) =>
        {
            if (this.thisSuppressGlobalSettingsChanged)
            {
                return;
            }

            UserSettings.SchematicsShowZones =
                this.CheckGlobalShowZones.IsChecked != false;

            this.RefreshKiCadOverlay(forceImmediate: true);
        };

        this.CheckGlobalShowTracesOnComponentSelect.IsCheckedChanged += (s, e) =>
        {
            if (this.thisSuppressGlobalSettingsChanged)
            {
                return;
            }

            UserSettings.SchematicsShowTracesOnComponentSelect =
                this.CheckGlobalShowTracesOnComponentSelect.IsChecked == true;

            this.RefreshKiCadOverlay(forceImmediate: true);
        };

        this.CheckGlobalHoverHighlightsTraces.IsCheckedChanged += (s, e) =>
        {
            this.ApplyInteractiveCadTraceHoverModeFromGlobalSettings();
            this.UpdateInteractiveCadTraceHoverModeUi();
            this.RefreshKiCadHoverPadUi();
            this.RefreshKiCadOverlay();
        };

        this.CheckBoardContributorMode.IsCheckedChanged += (s, e) =>
        {
            if (this.thisSuppressBoardSettingsChanged)
            {
                return;
            }

            bool isEnabled = this.CheckBoardContributorMode.IsChecked == true;
            UserSettings.ContributorMode = isEnabled;
        };

        this.SchematicsInteractiveCadTraceHoverHoldShiftCheckBox.IsCheckedChanged +=
            this.OnSchematicsInteractiveCadTraceHoverModeChanged;

        this.ToggleLabelsPanelButton.Click += (s, e) =>
        {
            bool willBeExpanded = !this.LabelsListPanel.IsVisible;
            this.LabelsListPanel.IsVisible = willBeExpanded;
            this.ToggleLabelsPanelButton.Content = willBeExpanded ? "Collapse" : "Expand";
            UserSettings.SchematicsLabelsPanelExpanded = willBeExpanded;
        };

        this.ToggleGlobalSettingsPanelButton.Click += (s, e) =>
        {
            bool willBeExpanded = !this.GlobalSettingsListPanel.IsVisible;
            this.GlobalSettingsListPanel.IsVisible = willBeExpanded;
            this.ToggleGlobalSettingsPanelButton.Content = willBeExpanded ? "Collapse" : "Expand";
            UserSettings.SchematicsGlobalSettingsPanelExpanded = willBeExpanded;
        };

        this.ToggleImportantSignalsPanelButton.Click += (s, e) =>
        {
            bool willBeExpanded = !this.ImportantSignalsListPanel.IsVisible;
            this.UpdateImportantSignalsPanelExpandedState(willBeExpanded);
            this.UpdateImportantSignalsClearButtonState(this.ImportantSignalsPanel.IsVisible);
            UserSettings.SchematicsImportantSignalsPanelExpanded = willBeExpanded;
        };

        this.ImportantSignalsListBox.SelectionChanged += (s, e) =>
        {
            this.SyncSelectedImportantSignalsFromList();
            this.RefreshKiCadOverlay(forceImmediate: true);
            this.RefreshBlinkStateFromCurrentSelection();
        };

        this.ToggleKiCadNetConnectionsPanelButton.Click += (s, e) =>
        {
            bool willBeExpanded = !this.KiCadNetConnectionsContentPanel.IsVisible;
            this.UpdateKiCadNetConnectionsPanelExpandedState(willBeExpanded);
            UserSettings.SchematicsNetConnectionsPanelExpanded = willBeExpanded;
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
                this.UpdateComponentLabels();
                this.RefreshKiCadOverlay();
            }
        };

        this.SchematicsImage.PropertyChanged += (s, e) =>
        {
            if (e.Property == Visual.BoundsProperty)
            {
                this.ClampSchematicsMatrix();
                this.UpdateComponentLabels();
                this.RefreshKiCadOverlay();
            }
        };

        this.SchematicsThumbnailList.SelectionChanged += this.OnSchematicsThumbnailSelectionChanged;
        this.SchematicsContainer.PointerExited += this.OnSchematicsPointerExited;

        this.UpdateInteractiveCadTraceHoverModeUi();

        this.UpdateLabelEditorLockState();
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
    // Returns true when the pointer is currently inside any interactive overlay panel that should
    // consume wheel input instead of letting the schematic viewer zoom underneath it.
    // ###########################################################################################
    private bool IsPointerInsideInteractiveOverlayPanel(Point containerPoint)
    {
        bool IsInsideVisibleOverlay(Control? overlay)
        {
            if (overlay == null ||
                !overlay.IsVisible ||
                overlay.Bounds.Width <= 0 ||
                overlay.Bounds.Height <= 0)
            {
                return false;
            }

            Point? translatedTopLeft = overlay.TranslatePoint(new Point(0, 0), this.SchematicsContainer);
            if (!translatedTopLeft.HasValue)
            {
                return false;
            }

            var overlayRect = new Rect(translatedTopLeft.Value, overlay.Bounds.Size);
            return overlayRect.Contains(containerPoint);
        }

        return IsInsideVisibleOverlay(this.GlobalSettingsPanel) ||
               IsInsideVisibleOverlay(this.LabelsPanel) ||
               IsInsideVisibleOverlay(this.ImportantSignalsPanel) ||
               IsInsideVisibleOverlay(this.TracesPanel) ||
               IsInsideVisibleOverlay(this.KiCadNetConnectionsPanel) ||
               IsInsideVisibleOverlay(this.TraceFloatingPalette) ||
               IsInsideVisibleOverlay(this.SchematicsLabelEditorMenuBorder) ||
               IsInsideVisibleOverlay(this.SchematicsNewLabelPromptBorder);
    }

    // ###########################################################################################
    // Handles mouse wheel zoom on the Schematics image, centered on the cursor position.
    // Wheel input over interactive overlay panels is consumed there and must not zoom the
    // schematic viewer underneath, even when a nested scroll viewer reaches its top or bottom.
    // ###########################################################################################
    private void OnSchematicsZoom(object? sender, PointerWheelEventArgs e)
    {
        var zoomCenterInContainer = e.GetPosition(this.SchematicsContainer);

        if (this.IsPointerInsideInteractiveOverlayPanel(zoomCenterInContainer))
        {
            e.Handled = true;
            return;
        }

        double zoomFactor = e.Delta.Y > 0
            ? AppConfig.SchematicsZoomFactor
            : 1.0 / AppConfig.SchematicsZoomFactor;

        this.ApplySchematicsZoom(zoomFactor, zoomCenterInContainer);

        e.Handled = true;
    }

    // ###########################################################################################
    // Applies zoom around a container-space anchor point and reuses the same clamping logic for
    // mouse wheel zoom and pinch zoom gestures.
    // ###########################################################################################
    private void ApplySchematicsZoom(double zoomFactor, Point zoomCenterInContainer)
    {
        if (this.currentFullResBitmap == null)
        {
            return;
        }

        if (double.IsNaN(zoomFactor) || double.IsInfinity(zoomFactor) || zoomFactor <= 0)
        {
            return;
        }

        double currentScale = this.schematicsMatrix.M11;
        double newScale = currentScale * zoomFactor;

        if (newScale > AppConfig.SchematicsMaxZoom)
        {
            zoomFactor = AppConfig.SchematicsMaxZoom / currentScale;
            newScale = currentScale * zoomFactor;
        }

        // The image is already fully fitted by Stretch="Uniform", so do not allow zooming out
        // below the baseline matrix scale of 1.0.
        if (newScale < 1.0)
        {
            this.schematicsMatrix = Matrix.Identity;
            this.ClampSchematicsMatrix();
            return;
        }

        var zoomMatrix =
            Matrix.CreateTranslation(-zoomCenterInContainer.X, -zoomCenterInContainer.Y) *
            Matrix.CreateScale(zoomFactor, zoomFactor) *
            Matrix.CreateTranslation(zoomCenterInContainer.X, zoomCenterInContainer.Y);

        // Apply zoom in container space, matching the same row-vector composition used by panning.
        this.schematicsMatrix = this.schematicsMatrix * zoomMatrix;
        this.ClampSchematicsMatrix();
    }

    // ###########################################################################################
    // Handles trackpad pinch zoom. macOS trackpad pinch does not reliably come through as a mouse
    // wheel event, so this explicit gesture path is needed.
    // ###########################################################################################
    private void OnSchematicsPinch(object? sender, PinchEventArgs e)
    {
        if (this.currentFullResBitmap == null)
        {
            return;
        }

        Point zoomCenterInContainer = new(
            this.SchematicsContainer.Bounds.Width / 2.0,
            this.SchematicsContainer.Bounds.Height / 2.0);

        this.ApplySchematicsZoom(e.Scale, zoomCenterInContainer);

        e.Handled = true;
    }

    // ###########################################################################################
    // Handles two-finger trackpad pan gestures independently of right-mouse panning.
    // Uses strict edge clamping so manual pan cannot drag the image below or beyond the viewport.
    // If the direction feels reversed on a specific platform, flip the signs below.
    // ###########################################################################################
    private void OnSchematicsScrollGesture(object? sender, ScrollGestureEventArgs e)
    {
        if (this.currentFullResBitmap == null)
        {
            return;
        }

        if (this.isPanning)
        {
            return;
        }

        Vector delta = e.Delta;

        if (Math.Abs(delta.X) < 0.001 && Math.Abs(delta.Y) < 0.001)
        {
            return;
        }

        this.schematicsMatrix = this.schematicsMatrix * Matrix.CreateTranslation(delta.X, delta.Y);
//hest        this.schematicsMatrix = this.schematicsMatrix * Matrix.CreateTranslation(-delta.X, -delta.Y); // replace above line if two-finger pan feels inverted on macOS
        this.ClampSchematicsMatrix(useStrictEdgeClamp: true);

        e.Handled = true;
    }

    // ###########################################################################################
    // Handles right-click for panning on the schematic view and selection toggling on release.
    // Left-click selects hovered component, single-click opens component info popup, and while the
    // new KiCad trace calibration mode is active the same pointer pipeline is reused for moving and
    // resizing the temporary calibration box.
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

        if (this.thisIsKiCadTraceCalibrationMode)
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
                if (!this.TryGetSchematicsImagePixelPoint(point, out var pixelPoint))
                {
                    e.Handled = true;
                    return;
                }

                if (this.TryGetKiCadTraceCalibrationHandleAtContainerPoint(point, out var resizeMode))
                {
                    this.StartKiCadTraceCalibrationDrag(pixelPoint, resizeMode);
                    this.UpdateKiCadTraceCalibrationCursor(point);
                    e.Handled = true;
                    return;
                }

                if (this.IsPointerInsideCurrentKiCadCalibrationBounds(point))
                {
                    this.StartKiCadTraceCalibrationDrag(pixelPoint, LabelEditorDragMode.Move);
                    this.UpdateKiCadTraceCalibrationCursor(point);
                    e.Handled = true;
                    return;
                }

                e.Handled = true;
                return;
            }
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
    // Routes movement and shift key state to Polyline Manager, label editor, and the new KiCad
    // trace calibration box interaction mode.
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

        if (this.thisIsKiCadTraceCalibrationMode && this.thisKiCadTraceCalibrationDragMode != LabelEditorDragMode.None)
        {
            if (this.TryGetSchematicsImagePixelPoint(point, out var pixelPoint))
            {
                this.UpdateKiCadTraceCalibrationDrag(pixelPoint);
            }

            this.UpdateKiCadTraceCalibrationCursor(point);
            e.Handled = true;
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
                this.UpdateDrawingLabelEditorRectangle(pixelPoint, e.KeyModifiers);
            }

            e.Handled = true;
            return;
        }

        if (!this.thisIsLabelEditorMode && !this.thisIsKiCadTraceCalibrationMode && TryInvert(this.schematicsMatrix, out var inv))
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

        if (this.thisIsKiCadTraceCalibrationMode)
        {
            this.UpdateKiCadTraceCalibrationCursor(point);
            this.SchematicsHoverLabelBorder.IsVisible = false;
            this.SchematicsHoverLabelText.Text = string.Empty;
            this.SchematicsHoverPadBorder.IsVisible = false;
            this.SchematicsHoverPadText.Text = string.Empty;

            if (this.MainWindow != null)
            {
                this.MainWindow.isHoveringComponent = false;
            }

            e.Handled = true;
            return;
        }

        if (!this.thisIsLabelEditorMode)
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
    // Exits pan mode when the right mouse button is released, finalizes label-editor operations,
    // and handles the new KiCad trace calibration move/resize workflow including empty-space
    // right-click access to Apply or Discard actions.
    // Keeps keyboard focus on the schematics control while KiCad trace calibration mode is active.
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

        if (this.thisIsKiCadTraceCalibrationMode && this.thisKiCadTraceCalibrationDragMode != LabelEditorDragMode.None)
        {
            this.CompleteKiCadTraceCalibrationDrag();
            this.UpdateKiCadTraceCalibrationCursor(point);
            this.SchematicsContainer.Focus();
            this.Focus();
            e.Handled = true;
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
                this.CompleteDrawingLabelEditorRectangle(point, pixelPoint, e.KeyModifiers);
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

        if (!this.thisIsLabelEditorMode && !this.thisIsKiCadTraceCalibrationMode && TryInvert(this.schematicsMatrix, out var inv))
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
        {
            if (this.thisIsKiCadTraceCalibrationMode)
            {
                this.UpdateKiCadTraceCalibrationCursor(point);
                this.SchematicsContainer.Focus();
                this.Focus();
            }

            return;
        }

        this.isPanning = false;
        e.Pointer.Capture(null);

        var delta = point - this.panStartPoint;
        bool isStationaryRightClick = Math.Abs(delta.X) < 4 && Math.Abs(delta.Y) < 4;

        if (isStationaryRightClick)
        {
            if (this.thisIsKiCadTraceCalibrationMode)
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
                    this.thisLockedKiCadNetNames.Remove(activeHoveredKiCadNetName);
                    this.thisHoveredKiCadNetName = null;
                    this.RefreshKiCadOverlay();
                    this.RefreshBlinkStateFromCurrentSelection();
                }
                else if (this.thisLockedKiCadNetNames.Count > 0)
                {
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

        if (this.thisIsKiCadTraceCalibrationMode)
        {
            this.SchematicsContainer.Focus();
            this.Focus();
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

        this.RebuildImportantSignalsPanel();
        this.UpdateKiCadNetConnectionsPanel(Array.Empty<string>());

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
                this.SchematicsHighlightsOverlay.HighlightColor = ParseColorOrDefault(schematic.SchematicHighlightColor, Colors.IndianRed);
                this.SchematicsHighlightsOverlay.HighlightOpacity = ParseOpacityOrDefault(schematic.SchematicHighlightOpacity, 0.20);
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
    // Resets only the active UI/session state and leaves persistent per-board KiCad runtime
    // caches intact so returning to a previously visited board can reuse them.
    // ###########################################################################################
    public void ResetSchematicsViewer()
    {
        this.thisIsKiCadOverlayRefreshQueued = false;
        this.thisKiCadOverlayRefreshRequestVersion = 0;
        this.thisKiCadOverlayLastRenderedVersion = 0;
        this.thisKiCadSchematicHoverHitTestCacheByKey.Clear();
        this.thisKiCadProject = null;
        this.thisCurrentKiCadRuntimeCacheScopeKey = string.Empty;

        lock (this.thisKiCadPcbNetRenderCacheSync)
        {
            this.thisKiCadPcbNetRenderBuildTaskByKey.Clear();
        }

        lock (this.thisKiCadPcbHoverHitTestCacheSync)
        {
            this.thisKiCadPcbHoverHitTestBuildTaskByKey.Clear();
        }

        this.SchematicsKiCadOverlayCanvas.ClearGeometry();
        ((MatrixTransform)this.SchematicsKiCadOverlayCanvas.RenderTransform!).Matrix = this.schematicsMatrix;

        this.KiCadNetConnectionsPanel.IsVisible = false;
        this.KiCadNetConnectionsHeaderTextBlock.Text = "Netlist name";
        this.KiCadNetConnectionsList.ItemsSource = null;
        this.UpdateKiCadNetConnectionsClearButtonState(false);

        this.UpdateImportantSignalsHeaderText();
        this.ImportantSignalsListBox.ItemsSource = null;
        this.ImportantSignalsListBox.SelectedItems?.Clear();
        this.ImportantSignalsPanel.IsVisible = false;
        this.UpdateImportantSignalsClearButtonState(false);

        this.thisSelectedKiCadReferences.Clear();
        this.thisSelectedKiCadNormalizedNetNames.Clear();
        this.thisSelectedImportantSignalDisplayNames.Clear();

        this.thisHoveredKiCadNetName = null;
        this.thisHoveredKiCadPadNumber = null;
        this.thisHoveredComponentBoardLabel = null;
        this.thisSchematicsOnlySelectedBoardLabels.Clear();
        this.thisLockedKiCadNetNames.Clear();

        this.thisIsKiCadTraceCalibrationMode = false;
        this.thisKiCadTraceCalibrationDragMode = LabelEditorDragMode.None;
        this.thisKiCadCalibrationImageLeft = 0.0;
        this.thisKiCadCalibrationImageTop = 0.0;
        this.thisKiCadCalibrationImageRight = 0.0;
        this.thisKiCadCalibrationImageBottom = 0.0;
        this.thisKiCadCalibrationStartImageLeft = 0.0;
        this.thisKiCadCalibrationStartImageTop = 0.0;
        this.thisKiCadCalibrationStartImageRight = 0.0;
        this.thisKiCadCalibrationStartImageBottom = 0.0;

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
        this.thisLastCreatedLabelEditorCategory = string.Empty;
        this.thisSelectedLabelEditorHighlights.Clear();
        this.thisSelectedLabelEditorHighlight = null;
        this.thisPendingNewLabelEditorHighlight = null;
        this.thisIsDrawingLabelEditorRectangle = false;
        this.thisLabelEditorDraftRectangle = null;
        this.thisLabelEditorDragMode = LabelEditorDragMode.None;
        this.thisLabelEditorOriginalSelectionBounds = default;
        this.thisLabelEditorOriginalDragRectangles.Clear();
        this.thisLabelEditorWorkingHighlights.Clear();
        this.UpdateLabelEditorLockState();
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
    // Returns the rectangle (in local overlay coordinates) that the actual bitmap content occupies.
    // Must match the overlay renderer mapping exactly, with no centering offset applied.
    // ###########################################################################################
    internal Rect GetImageContentRect()
    {
        return this.GetSchematicsContentRect();
    }

    // ###########################################################################################
    // Computes the schematic image content rect using the same top-left anchored logic as all
    // overlay renderers so labels, hit testing, and editor rectangles always share one mapping.
    // ###########################################################################################
    private Rect GetSchematicsContentRect()
    {
        var bitmap = this.currentFullResBitmap;

        Size controlSize = this.SchematicsHighlightsOverlay.Bounds.Size;
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
    // Computes the editor overlay image content rect using the exact same mapping as the main
    // schematic overlays so pointer hit testing stays aligned after reloads and mode switches.
    // ###########################################################################################
    private Rect GetLabelEditorImageContentRect()
    {
        return this.GetSchematicsContentRect();
    }

    // ###########################################################################################
    // Computes the effective visible viewport inside the schematics container after subtracting
    // only the panel edges that should actually constrain panning. Bottom-docked utility panels
    // reserve bottom space, while the net connections panel reserves right-side space. This avoids
    // false top-edge shrinkage when a corner panel appears, which otherwise causes jumpy panning.
    // ###########################################################################################
    private Rect GetSchematicsVisibleViewportRect()
    {
        Size containerSize = this.SchematicsContainer.Bounds.Size;
        if (containerSize.Width <= 0 || containerSize.Height <= 0)
        {
            return new Rect(containerSize);
        }

        double leftInset = 0.0;
        double topInset = 0.0;
        double rightInset = 0.0;
        double bottomInset = 0.0;

        void IncludeOverlay(
            Control? overlay,
            bool reserveLeft = false,
            bool reserveTop = false,
            bool reserveRight = false,
            bool reserveBottom = false)
        {
            if (overlay == null ||
                !overlay.IsVisible ||
                overlay.Bounds.Width <= 0 ||
                overlay.Bounds.Height <= 0)
            {
                return;
            }

            Point? translatedTopLeft = overlay.TranslatePoint(new Point(0, 0), this.SchematicsContainer);
            if (!translatedTopLeft.HasValue)
            {
                return;
            }

            double left = Math.Max(0.0, translatedTopLeft.Value.X);
            double top = Math.Max(0.0, translatedTopLeft.Value.Y);
            double right = Math.Min(containerSize.Width, translatedTopLeft.Value.X + overlay.Bounds.Width);
            double bottom = Math.Min(containerSize.Height, translatedTopLeft.Value.Y + overlay.Bounds.Height);

            if (right <= left || bottom <= top)
            {
                return;
            }

            if (reserveLeft)
            {
                leftInset = Math.Max(leftInset, right);
            }

            if (reserveTop)
            {
                topInset = Math.Max(topInset, bottom);
            }

            if (reserveRight)
            {
                rightInset = Math.Max(rightInset, containerSize.Width - left);
            }

            if (reserveBottom)
            {
                bottomInset = Math.Max(bottomInset, containerSize.Height - top);
            }
        }

        IncludeOverlay(this.GlobalSettingsPanel, reserveBottom: true);
        IncludeOverlay(this.LabelsPanel, reserveBottom: true);
        IncludeOverlay(this.ImportantSignalsPanel, reserveBottom: true);
        IncludeOverlay(this.TracesPanel, reserveBottom: true);
        IncludeOverlay(this.KiCadNetConnectionsPanel, reserveRight: true);

        double viewportLeft = Math.Clamp(leftInset, 0.0, containerSize.Width);
        double viewportTop = Math.Clamp(topInset, 0.0, containerSize.Height);
        double viewportRight = Math.Clamp(containerSize.Width - rightInset, viewportLeft, containerSize.Width);
        double viewportBottom = Math.Clamp(containerSize.Height - bottomInset, viewportTop, containerSize.Height);

        return new Rect(
            viewportLeft,
            viewportTop,
            Math.Max(1.0, viewportRight - viewportLeft),
            Math.Max(1.0, viewportBottom - viewportTop));
    }

    // ###########################################################################################
    // Clamps the current schematics matrix while preserving zoom-anchor stability by default.
    // Manual panning can opt into strict edge clamping so the image cannot be dragged beyond the
    // currently visible viewport after edge-docked overlay panels have been accounted for.
    // Also allows baseline-scale panning when overlay panels reduce the effectively visible area.
    // ###########################################################################################
    private void ClampSchematicsMatrix(bool useStrictEdgeClamp = false)
    {
        Size containerSize = this.SchematicsContainer.Bounds.Size;
        if (containerSize.Width <= 0 || containerSize.Height <= 0)
        {
            return;
        }

        Rect viewportRect = this.GetSchematicsVisibleViewportRect();
        if (viewportRect.Width <= 0 || viewportRect.Height <= 0)
        {
            viewportRect = new Rect(containerSize);
        }

        Rect contentRect = this.GetImageContentRect();

        double scale = this.schematicsMatrix.M11;
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
        {
            scale = 1.0;
        }

        double tx = this.schematicsMatrix.M31;
        double ty = this.schematicsMatrix.M32;

        const double minimumScaleEpsilon = 0.000001;

        double scaledLeftAtZero = scale * contentRect.Left;
        double scaledTopAtZero = scale * contentRect.Top;
        double scaledRightAtZero = scale * contentRect.Right;
        double scaledBottomAtZero = scale * contentRect.Bottom;

        double leftAlignedTx = viewportRect.Left - scaledLeftAtZero;
        double rightAlignedTx = viewportRect.Right - scaledRightAtZero;
        double topAlignedTy = viewportRect.Top - scaledTopAtZero;
        double bottomAlignedTy = viewportRect.Bottom - scaledBottomAtZero;

        double minTx = Math.Min(leftAlignedTx, rightAlignedTx);
        double maxTx = Math.Max(leftAlignedTx, rightAlignedTx);
        double minTy = Math.Min(topAlignedTy, bottomAlignedTy);
        double maxTy = Math.Max(topAlignedTy, bottomAlignedTy);

        bool shouldUseStrictEdgeClamp = useStrictEdgeClamp || this.isPanning;

        bool shouldAllowBaselinePanX =
            contentRect.Width > viewportRect.Width + 0.01 ||
            viewportRect.Left > 0.01 ||
            viewportRect.Right < containerSize.Width - 0.01;

        bool shouldAllowBaselinePanY =
            contentRect.Height > viewportRect.Height + 0.01 ||
            viewportRect.Top > 0.01 ||
            viewportRect.Bottom < containerSize.Height - 0.01;

        if (scale <= 1.0 + minimumScaleEpsilon)
        {
            tx = shouldAllowBaselinePanX
                ? Math.Clamp(tx, minTx, maxTx)
                : 0.0;

            ty = shouldAllowBaselinePanY
                ? Math.Clamp(ty, minTy, maxTy)
                : 0.0;
        }
        else if (shouldUseStrictEdgeClamp)
        {
            tx = Math.Clamp(tx, minTx, maxTx);
            ty = Math.Clamp(ty, minTy, maxTy);
        }
        else
        {
            if (tx < minTx)
            {
                tx = minTx;
            }
            else if (tx > maxTx)
            {
                tx = maxTx;
            }

            if (ty < minTy)
            {
                ty = minTy;
            }
            else if (ty > maxTy)
            {
                ty = maxTy;
            }
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
            HighlightColor = ParseColorOrDefault(schematic.ThumbnailHighlightColor, Colors.IndianRed),
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
    // Returns true when any schematic selection is active that should participate in blink visuals.
    // ###########################################################################################
    public bool HasBlinkEligibleSelection()
    {
        if (this.highlightIndexBySchematic.Count > 0)
        {
            return true;
        }

        if (this.thisLockedKiCadNetNames.Count > 0)
        {
            return true;
        }

        if (this.thisSelectedImportantSignalDisplayNames.Count > 0)
        {
            return true;
        }

        string? hoveredNetName = this.GetActiveHoveredKiCadNetName();
        if (!string.IsNullOrWhiteSpace(hoveredNetName))
        {
            return true;
        }

        return false;
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
    // Thumbnail highlights must be regenerated when the component-selection blink phase changes
    // because their highlight overlay is baked into the rendered thumbnail bitmap.
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
            this.SchematicsHighlightsOverlay.HighlightColor = ParseColorOrDefault(mainSchematic.SchematicHighlightColor, Colors.IndianRed);
            this.SchematicsHighlightsOverlay.HighlightOpacity =
                ParseOpacityOrDefault(mainSchematic.SchematicHighlightOpacity, 0.20) * this.thisCurrentHighlightBlinkFactor;
        }
        else
        {
            this.SchematicsHighlightsOverlay.HighlightIndex = null;
        }

        this.SchematicsHighlightsOverlay.InvalidateVisual();

        bool isLabelEditorModeActive = this.thisIsLabelEditorMode;
        string activeEditorSchematicName = isLabelEditorModeActive
            ? this.GetCurrentSchematicName()
            : string.Empty;

        bool hasKiCadSelection = this.HasKiCadSelectionForThumbnailDimming();
        bool hasAnyThumbnailSelection = hasSelection || hasKiCadSelection || isLabelEditorModeActive;

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

                if (isLabelEditorModeActive)
                {
                    if (!ReferenceEquals(thumb.ImageSource, thumb.BaseThumbnail))
                    {
                        var old = thumb.ImageSource;
                        thumb.ImageSource = thumb.BaseThumbnail;
                        (old as IDisposable)?.Dispose();
                    }

                    bool isRelevantForEditor = string.Equals(
                        thumb.Name,
                        activeEditorSchematicName,
                        StringComparison.OrdinalIgnoreCase);

                    thumb.VisualOpacity = isRelevantForEditor ? 1.0 : 0.35;
                    thumb.IsMatchForSelection = false;
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
                        opacityMultiplier: this.thisCurrentHighlightBlinkFactor);

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
    // Returns true when explicit KiCad trace selections should participate in thumbnail dimming.
    // Hover-only KiCad nets are excluded so thumbnails do not flicker while moving the pointer.
    // Component-derived net names are also excluded because component presence must be validated by
    // the actual schematic image/component data, not by shared net names on other pages.
    // ###########################################################################################
    private bool HasKiCadSelectionForThumbnailDimming()
    {
        return this.BuildKiCadThumbnailDimmingNetNames().Count > 0;
    }

    // ###########################################################################################
    // Returns true when the requested schematic contains any explicitly selected KiCad net content.
    // Only explicit KiCad trace selections participate here, so component-driven thumbnail matching
    // stays reliable and depends on real component/image presence instead of shared net names.
    // Zones are included so copper pours participate in thumbnail dimming and selection presence.
    // ###########################################################################################
    private bool DoesSchematicContainSelectedKiCadContent(string schematicName)
    {
        if (string.IsNullOrWhiteSpace(schematicName) || this.thisKiCadProject == null)
        {
            return false;
        }

        var selectedNets = this.BuildKiCadThumbnailDimmingNetNames();
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
                string netId = net.Id?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(normalizedName) ||
                    !selectedNets.Contains(normalizedName) ||
                    string.IsNullOrWhiteSpace(netId))
                {
                    continue;
                }

                if (!pcb.HighlightIndex.TryGetValue(netId, out var bucket))
                {
                    continue;
                }

                if (bucket.Pads.Count > 0 ||
                    bucket.Segments.Count > 0 ||
                    bucket.Vias.Count > 0 ||
                    bucket.Arcs.Count > 0 ||
                    bucket.Zones.Count > 0)
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
    // Updates hover label and cursor from current pointer position.
    // The new KiCad trace calibration mode takes over hover UI so alignment work stays visually
    // clean and only the calibration box interaction feedback is shown.
    // ###########################################################################################
    private void UpdateSchematicsHoverUi(Point pointerInContainer)
    {
        if (this.thisIsKiCadTraceCalibrationMode)
        {
            this.SetHoveredComponentBoardLabel(null);
            this.SetHoveredKiCadNet(null);
            this.thisHoveredKiCadPadNumber = null;

            this.SchematicsHoverLabelText.Text =
                "KiCad calibration mode - drag inside box to move, drag edges to resize, drag across to flip";
            this.SchematicsHoverLabelBorder.IsVisible = true;
            this.SchematicsHoverPadBorder.IsVisible = false;
            this.SchematicsHoverPadText.Text = string.Empty;

            if (this.MainWindow != null)
            {
                this.MainWindow.isHoveringComponent = false;
            }

            this.UpdateKiCadTraceCalibrationCursor(pointerInContainer);
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

            if (this.MainWindow != null)
            {
                this.MainWindow.isHoveringComponent = true;
            }
        }
        else
        {
            this.SetHoveredComponentBoardLabel(null);

            if (this.MainWindow != null)
            {
                this.MainWindow.isHoveringComponent = false;
            }

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
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        string normalized = text.Trim();

        bool isPercent = normalized.EndsWith("%", StringComparison.Ordinal);
        if (isPercent)
        {
            normalized = normalized[..^1].Trim();
        }

        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return fallback;
        }

        if (isPercent || value > 1.0)
        {
            value /= 100.0;
        }

        return Math.Clamp(value, 0.0, 1.0);
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
    // Contributor mode enables menu entry from empty-space right click, and active editor or KiCad
    // calibration workflows keep the same shared floating menu available.
    // ###########################################################################################
    private bool CanShowSchematicsActionsMenu()
    {
        return this.IsBoardContributorModeEnabled() ||
               this.thisIsLabelEditorMode ||
               this.thisIsKiCadTraceCalibrationMode;
    }

    // ###########################################################################################
    // Shows the floating schematic action menu at the requested schematic container location.
    // The menu adapts its height to contributor mode, label editor mode, and the new KiCad trace
    // calibration workflow.
    // ###########################################################################################
    private void ShowLabelEditorMenu(Point containerPoint)
    {
        if (!this.CanShowSchematicsActionsMenu())
        {
            return;
        }

        this.thisLastLabelEditorMenuPoint = containerPoint;
        this.UpdateLabelEditorMenuButtons();

        double estimatedWidth = 250.0;
        double estimatedHeight =
            this.thisIsLabelEditorMode ? 110.0 :
            this.thisIsKiCadTraceCalibrationMode ? 105.0 :
            195.0;

        double x = Math.Clamp(
            containerPoint.X,
            6.0,
            Math.Max(6.0, this.SchematicsContainer.Bounds.Width - estimatedWidth));

        double y = Math.Clamp(
            containerPoint.Y,
            6.0,
            Math.Max(6.0, this.SchematicsContainer.Bounds.Height - estimatedHeight));

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
    // Updates the menu text and button visibility according to the current editor and calibration
    // state, including the new KiCad trace calibration start/apply/discard workflow.
    // ###########################################################################################
    private void UpdateLabelEditorMenuButtons()
    {
        this.SchematicsLabelEditorMenuStateTextBlock.Text =
            this.thisIsLabelEditorMode
                ? "Component label editor mode"
                : this.thisIsKiCadTraceCalibrationMode
                    ? "KiCad trace calibration"
                    : "Contributor mode actions";

        this.EnableLabelEditorButton.IsVisible =
            !this.thisIsLabelEditorMode &&
            !this.thisIsKiCadTraceCalibrationMode;

        this.BeginKiCadTraceCalibrationButton.IsVisible =
            !this.thisIsLabelEditorMode &&
            !this.thisIsKiCadTraceCalibrationMode &&
            this.HasCurrentSchematicKiCadTraces();

        this.ApplyLabelEditorChangesButton.IsVisible = this.thisIsLabelEditorMode;
        this.CancelLabelEditorChangesButton.IsVisible = this.thisIsLabelEditorMode;

        this.ApplyKiCadTraceCalibrationButton.IsVisible = this.thisIsKiCadTraceCalibrationMode;
        this.CancelKiCadTraceCalibrationButton.IsVisible = this.thisIsKiCadTraceCalibrationMode;
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
        this.thisLastCreatedLabelEditorCategory = string.Empty;

        this.LoadLabelEditorWorkingCopyForCurrentSchematic();
        this.UpdateLabelEditorLockState();
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
        this.thisLastCreatedLabelEditorCategory = string.Empty;
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

        this.UpdateLabelEditorLockState();
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
        this.thisLastCreatedLabelEditorCategory = string.Empty;
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

        this.UpdateLabelEditorLockState();
        this.HideLabelEditorMenu();
        this.HideNewLabelEditorPrompt();
        this.RefreshLabelEditorOverlay();
        this.SchematicsContainer.Focus();

        if (this.MainWindow != null)
        {
            await this.MainWindow.DisableLaunchDataSyncAfterLocalBoardEditAsync();
            this.MainWindow.ReloadCurrentBoardFromDisk(schematicName);
        }

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
            highlightColor = ParseColorOrDefault(schematic.SchematicHighlightColor, Colors.IndianRed);
            highlightOpacity = ParseOpacityOrDefault(schematic.SchematicHighlightOpacity, 0.20);
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
    // Snaps active resize edges to nearby neighbor edges within 2 px, or emits exact-match guides
    // without changing the rectangle when keyboard resizing wants visual alignment only.
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
        bool suppressSnap,
        bool snapOnMatch = true,
        LabelEditorDragMode? dragModeOverride = null)
    {
        const double snapThreshold = 2.0;
        const double epsilon = 0.001;
        const double guideMatchThreshold = 0.5;

        LabelEditorDragMode dragMode = dragModeOverride ?? this.thisLabelEditorDragMode;

        if (suppressSnap ||
            dragMode == LabelEditorDragMode.None ||
            dragMode == LabelEditorDragMode.Move)
        {
            return;
        }

        string schematicName = this.GetCurrentSchematicName();

        bool resizesTop =
            dragMode == LabelEditorDragMode.ResizeTop ||
            dragMode == LabelEditorDragMode.ResizeTopLeft ||
            dragMode == LabelEditorDragMode.ResizeTopRight;

        bool resizesBottom =
            dragMode == LabelEditorDragMode.ResizeBottom ||
            dragMode == LabelEditorDragMode.ResizeBottomLeft ||
            dragMode == LabelEditorDragMode.ResizeBottomRight;

        bool resizesLeft =
            dragMode == LabelEditorDragMode.ResizeLeft ||
            dragMode == LabelEditorDragMode.ResizeTopLeft ||
            dragMode == LabelEditorDragMode.ResizeBottomLeft;

        bool resizesRight =
            dragMode == LabelEditorDragMode.ResizeRight ||
            dragMode == LabelEditorDragMode.ResizeTopRight ||
            dragMode == LabelEditorDragMode.ResizeBottomRight;

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

            double startX = Math.Min(currentRect.Left, targetRect.Left);
            double endX = Math.Max(currentRect.Right, targetRect.Right);

            if (endX - startX <= 0.01)
            {
                return false;
            }

            guide = (new Point(startX, y), new Point(endX, y));
            return true;
        }

        static bool TryBuildVerticalGuide(Rect currentRect, Rect targetRect, double x, out (Point Start, Point End) guide)
        {
            guide = default;

            double startY = Math.Min(currentRect.Top, targetRect.Top);
            double endY = Math.Max(currentRect.Bottom, targetRect.Bottom);

            if (endY - startY <= 0.01)
            {
                return false;
            }

            guide = (new Point(x, startY), new Point(x, endY));
            return true;
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

                if (!snapOnMatch)
                {
                    if (distance > guideMatchThreshold)
                    {
                        return;
                    }

                    if (bestTargets.Count == 0)
                    {
                        bestY = candidateY;
                        bestTargets.Add(other);
                        return;
                    }

                    if (Math.Abs(candidateY - bestY) <= guideMatchThreshold &&
                        !bestTargets.Contains(other))
                    {
                        bestTargets.Add(other);
                    }

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
                if (snapOnMatch)
                {
                    if (resizesTop)
                    {
                        top = bestY;
                    }
                    else
                    {
                        bottom = bestY;
                    }
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

                if (!snapOnMatch)
                {
                    if (distance > guideMatchThreshold)
                    {
                        return;
                    }

                    if (bestTargets.Count == 0)
                    {
                        bestX = candidateX;
                        bestTargets.Add(other);
                        return;
                    }

                    if (Math.Abs(candidateX - bestX) <= guideMatchThreshold &&
                        !bestTargets.Contains(other))
                    {
                        bestTargets.Add(other);
                    }

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
                if (snapOnMatch)
                {
                    if (resizesLeft)
                    {
                        left = bestX;
                    }
                    else
                    {
                        right = bestX;
                    }
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
    // Snaps the moved selection bounds to nearby neighbor edges while preserving the current
    // selection layout. SHIFT still suppresses the snap, matching resize behavior.
    // When snapOnMatch is false, no movement is applied and guides are shown only for exact matches.
    // ###########################################################################################
    private void ApplyLabelEditorMoveSnap(
        IReadOnlyList<EditableComponentHighlight> selectedHighlights,
        IReadOnlyDictionary<EditableComponentHighlight, Rect> sourceRects,
        ref Rect movedSelectionBounds,
        List<(Point Start, Point End)> snapGuides,
        bool suppressSnap,
        bool snapOnMatch = true)
    {
        const double snapThreshold = 2.0;
        const double epsilon = 0.001;
        const double guideMatchThreshold = 0.5;

        if (suppressSnap || selectedHighlights.Count == 0)
        {
            return;
        }

        string schematicName = this.GetCurrentSchematicName();
        var selectedSet = new HashSet<EditableComponentHighlight>(selectedHighlights);

        static bool RangesOverlap(double a1, double a2, double b1, double b2)
        {
            return Math.Min(a2, b2) > Math.Max(a1, b1);
        }

        Rect GetRect(EditableComponentHighlight highlight)
        {
            if (sourceRects.TryGetValue(highlight, out var sourceRect))
            {
                return sourceRect;
            }

            return new Rect(highlight.X, highlight.Y, highlight.Width, highlight.Height);
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
                if (selectedSet.Contains(other) ||
                    ReferenceEquals(other, targetHighlight) ||
                    !string.Equals(other.SchematicName, schematicName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var otherRect = GetRect(other);

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
                if (selectedSet.Contains(other) ||
                    ReferenceEquals(other, targetHighlight) ||
                    !string.Equals(other.SchematicName, schematicName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var otherRect = GetRect(other);

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

            double startX = Math.Min(currentRect.Left, targetRect.Left);
            double endX = Math.Max(currentRect.Right, targetRect.Right);

            if (endX - startX <= 0.01)
            {
                return false;
            }

            guide = (new Point(startX, y), new Point(endX, y));
            return true;
        }

        static bool TryBuildVerticalGuide(Rect currentRect, Rect targetRect, double x, out (Point Start, Point End) guide)
        {
            guide = default;

            double startY = Math.Min(currentRect.Top, targetRect.Top);
            double endY = Math.Max(currentRect.Bottom, targetRect.Bottom);

            if (endY - startY <= 0.01)
            {
                return false;
            }

            guide = (new Point(x, startY), new Point(x, endY));
            return true;
        }

        Rect currentMovedSelectionBounds = movedSelectionBounds;

        double bestDeltaY = 0.0;
        double bestDistanceY = snapThreshold + 0.001;
        var bestVerticalTargets = new List<(EditableComponentHighlight Target, double Y)>();

        void ConsiderVerticalCandidate(EditableComponentHighlight other, double sourceY, double candidateY)
        {
            double delta = candidateY - sourceY;
            double distance = Math.Abs(delta);

            if (distance > snapThreshold ||
                IsVerticalPathBlocked(sourceY, candidateY, currentMovedSelectionBounds, other))
            {
                return;
            }

            if (!snapOnMatch)
            {
                if (distance > guideMatchThreshold)
                {
                    return;
                }

                if (bestVerticalTargets.Count == 0)
                {
                    bestDeltaY = delta;
                    bestVerticalTargets.Add((other, candidateY));
                    return;
                }

                if (Math.Abs(delta - bestDeltaY) <= guideMatchThreshold &&
                    !bestVerticalTargets.Any(target =>
                        ReferenceEquals(target.Target, other) &&
                        Math.Abs(target.Y - candidateY) <= guideMatchThreshold))
                {
                    bestVerticalTargets.Add((other, candidateY));
                }

                return;
            }

            if (distance < bestDistanceY - epsilon)
            {
                bestDistanceY = distance;
                bestDeltaY = delta;
                bestVerticalTargets.Clear();
                bestVerticalTargets.Add((other, candidateY));
                return;
            }

            if (Math.Abs(distance - bestDistanceY) <= epsilon &&
                Math.Abs(delta - bestDeltaY) <= epsilon &&
                !bestVerticalTargets.Any(target =>
                    ReferenceEquals(target.Target, other) &&
                    Math.Abs(target.Y - candidateY) <= epsilon))
            {
                bestVerticalTargets.Add((other, candidateY));
            }
        }

        foreach (var other in this.thisLabelEditorWorkingHighlights)
        {
            if (selectedSet.Contains(other) ||
                !string.Equals(other.SchematicName, schematicName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var otherRect = GetRect(other);
            if (!IsRectVisibleInCurrentView(otherRect))
            {
                continue;
            }

            ConsiderVerticalCandidate(other, currentMovedSelectionBounds.Top, otherRect.Top);
            ConsiderVerticalCandidate(other, currentMovedSelectionBounds.Top, otherRect.Bottom);
            ConsiderVerticalCandidate(other, currentMovedSelectionBounds.Bottom, otherRect.Top);
            ConsiderVerticalCandidate(other, currentMovedSelectionBounds.Bottom, otherRect.Bottom);
        }

        if (bestVerticalTargets.Count > 0)
        {
            if (snapOnMatch)
            {
                movedSelectionBounds = new Rect(
                    movedSelectionBounds.X,
                    movedSelectionBounds.Y + bestDeltaY,
                    movedSelectionBounds.Width,
                    movedSelectionBounds.Height);
            }

            currentMovedSelectionBounds = movedSelectionBounds;

            foreach (var target in bestVerticalTargets)
            {
                var targetRect = GetRect(target.Target);

                if (TryBuildHorizontalGuide(currentMovedSelectionBounds, targetRect, target.Y, out var guide))
                {
                    snapGuides.Add(guide);
                }
            }
        }

        currentMovedSelectionBounds = movedSelectionBounds;

        double bestDeltaX = 0.0;
        double bestDistanceX = snapThreshold + 0.001;
        var bestHorizontalTargets = new List<(EditableComponentHighlight Target, double X)>();

        void ConsiderHorizontalCandidate(EditableComponentHighlight other, double sourceX, double candidateX)
        {
            double delta = candidateX - sourceX;
            double distance = Math.Abs(delta);

            if (distance > snapThreshold ||
                IsHorizontalPathBlocked(sourceX, candidateX, currentMovedSelectionBounds, other))
            {
                return;
            }

            if (!snapOnMatch)
            {
                if (distance > guideMatchThreshold)
                {
                    return;
                }

                if (bestHorizontalTargets.Count == 0)
                {
                    bestDeltaX = delta;
                    bestHorizontalTargets.Add((other, candidateX));
                    return;
                }

                if (Math.Abs(delta - bestDeltaX) <= guideMatchThreshold &&
                    !bestHorizontalTargets.Any(target =>
                        ReferenceEquals(target.Target, other) &&
                        Math.Abs(target.X - candidateX) <= guideMatchThreshold))
                {
                    bestHorizontalTargets.Add((other, candidateX));
                }

                return;
            }

            if (distance < bestDistanceX - epsilon)
            {
                bestDistanceX = distance;
                bestDeltaX = delta;
                bestHorizontalTargets.Clear();
                bestHorizontalTargets.Add((other, candidateX));
                return;
            }

            if (Math.Abs(distance - bestDistanceX) <= epsilon &&
                Math.Abs(delta - bestDeltaX) <= epsilon &&
                !bestHorizontalTargets.Any(target =>
                    ReferenceEquals(target.Target, other) &&
                    Math.Abs(target.X - candidateX) <= epsilon))
            {
                bestHorizontalTargets.Add((other, candidateX));
            }
        }

        foreach (var other in this.thisLabelEditorWorkingHighlights)
        {
            if (selectedSet.Contains(other) ||
                !string.Equals(other.SchematicName, schematicName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var otherRect = GetRect(other);
            if (!IsRectVisibleInCurrentView(otherRect))
            {
                continue;
            }

            ConsiderHorizontalCandidate(other, currentMovedSelectionBounds.Left, otherRect.Left);
            ConsiderHorizontalCandidate(other, currentMovedSelectionBounds.Left, otherRect.Right);
            ConsiderHorizontalCandidate(other, currentMovedSelectionBounds.Right, otherRect.Left);
            ConsiderHorizontalCandidate(other, currentMovedSelectionBounds.Right, otherRect.Right);
        }

        if (bestHorizontalTargets.Count > 0)
        {
            if (snapOnMatch)
            {
                movedSelectionBounds = new Rect(
                    movedSelectionBounds.X + bestDeltaX,
                    movedSelectionBounds.Y,
                    movedSelectionBounds.Width,
                    movedSelectionBounds.Height);
            }

            currentMovedSelectionBounds = movedSelectionBounds;

            foreach (var target in bestHorizontalTargets)
            {
                var targetRect = GetRect(target.Target);

                if (TryBuildVerticalGuide(currentMovedSelectionBounds, targetRect, target.X, out var guide))
                {
                    snapGuides.Add(guide);
                }
            }
        }
    }

    // ###########################################################################################
    // Maps one keyboard resize gesture to the equivalent editor drag mode so exact-match guides
    // can be shown without applying any mouse-style snap movement.
    // ###########################################################################################
    private static bool TryGetKeyboardLabelEditorResizeDragMode(Key key, KeyModifiers modifiers, out LabelEditorDragMode dragMode)
    {
        dragMode = LabelEditorDragMode.None;

        bool isShift = modifiers.HasFlag(KeyModifiers.Shift);
        bool isAlt = modifiers.HasFlag(KeyModifiers.Alt);

        if (isShift == isAlt)
        {
            return false;
        }

        switch (key)
        {
            case Key.Left:
                dragMode = isShift
                    ? LabelEditorDragMode.ResizeLeft
                    : LabelEditorDragMode.ResizeRight;
                return true;

            case Key.Right:
                dragMode = isShift
                    ? LabelEditorDragMode.ResizeRight
                    : LabelEditorDragMode.ResizeLeft;
                return true;

            case Key.Up:
                dragMode = isShift
                    ? LabelEditorDragMode.ResizeTop
                    : LabelEditorDragMode.ResizeBottom;
                return true;

            case Key.Down:
                dragMode = isShift
                    ? LabelEditorDragMode.ResizeBottom
                    : LabelEditorDragMode.ResizeTop;
                return true;

            default:
                return false;
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
    // Applies the same neighbor-edge snap behavior used by resize operations unless Shift is held.
    // ###########################################################################################
    private void UpdateDrawingLabelEditorRectangle(Point currentPixelPoint, KeyModifiers modifiers)
    {
        if (!this.thisIsDrawingLabelEditorRectangle)
        {
            return;
        }

        var draftRect = CreateNormalizedRect(this.thisLabelEditorDrawStartPixelPoint, currentPixelPoint);
        var snapGuides = new List<(Point Start, Point End)>();

        this.ApplyNewLabelEditorRectangleSnap(
            ref draftRect,
            snapGuides,
            modifiers.HasFlag(KeyModifiers.Shift));

        this.thisLabelEditorDraftRectangle = draftRect;
        this.RefreshLabelEditorOverlay(snapGuides);
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
    // The final rectangle also uses neighbor-edge snap behavior unless Shift is held.
    // ###########################################################################################
    private void CompleteDrawingLabelEditorRectangle(
        Point releaseContainerPoint,
        Point releasePixelPoint,
        KeyModifiers modifiers)
    {
        if (!this.thisIsDrawingLabelEditorRectangle)
        {
            return;
        }

        var finalRect = CreateNormalizedRect(this.thisLabelEditorDrawStartPixelPoint, releasePixelPoint);

        var snapGuides = new List<(Point Start, Point End)>();
        this.ApplyNewLabelEditorRectangleSnap(
            ref finalRect,
            snapGuides,
            modifiers.HasFlag(KeyModifiers.Shift));

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
    // Reuses the last confirmed new-component category when available.
    // ###########################################################################################
    private void ShowNewLabelEditorPrompt(Point containerPoint)
    {
        double estimatedWidth = 280.0;
        double estimatedHeight = 170.0;

        double x = Math.Clamp(containerPoint.X, 6.0, Math.Max(6.0, this.SchematicsContainer.Bounds.Width - estimatedWidth));
        double y = Math.Clamp(containerPoint.Y, 6.0, Math.Max(6.0, this.SchematicsContainer.Bounds.Height - estimatedHeight));

        var categories = this.GetAvailableLabelEditorCategories();

        string preferredCategory = categories
            .FirstOrDefault(category => string.Equals(
                category,
                this.thisLastCreatedLabelEditorCategory,
                StringComparison.OrdinalIgnoreCase))
            ?? this.MainWindow?.CategoryFilterListBox.SelectedItems?
                .Cast<string>()
                .FirstOrDefault(category => !string.IsNullOrWhiteSpace(category))
            ?? categories.FirstOrDefault()
            ?? "General";

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
    // Remembers the chosen category so the next new component defaults to the same category.
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
        this.thisLastCreatedLabelEditorCategory = category;
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
    // Holding Shift during mouse drag suppresses snap alignment.
    // Uses the drag-start rectangles as the stable source so pointer movement does not compound.
    // ###########################################################################################
    private void UpdateLabelEditorDrag(Point currentPixelPoint, KeyModifiers modifiers)
    {
        if (!this.HasSelectedLabelEditorHighlightsForCurrentSchematic() ||
            this.thisLabelEditorDragMode == LabelEditorDragMode.None)
        {
            return;
        }

        var selectedRows = this.GetSelectedLabelEditorHighlightsForCurrentSchematic();
        if (selectedRows.Count == 0)
        {
            return;
        }

        var sourceRects = new Dictionary<EditableComponentHighlight, Rect>();

        foreach (var row in selectedRows)
        {
            if (!this.thisLabelEditorOriginalDragRectangles.TryGetValue(row, out var originalRect))
            {
                originalRect = new Rect(row.X, row.Y, row.Width, row.Height);
            }

            sourceRects[row] = originalRect;
        }

        double dx = currentPixelPoint.X - this.thisLabelEditorDragStartPixelPoint.X;
        double dy = currentPixelPoint.Y - this.thisLabelEditorDragStartPixelPoint.Y;
        bool suppressSnap = modifiers.HasFlag(KeyModifiers.Shift);

        var snapGuides = new List<(Point Start, Point End)>();

        if (this.thisLabelEditorDragMode == LabelEditorDragMode.Move)
        {
            double originalLeft = sourceRects.Values.Min(rect => rect.Left);
            double originalTop = sourceRects.Values.Min(rect => rect.Top);
            double originalRight = sourceRects.Values.Max(rect => rect.Right);
            double originalBottom = sourceRects.Values.Max(rect => rect.Bottom);

            var originalSelectionBounds = new Rect(
                originalLeft,
                originalTop,
                originalRight - originalLeft,
                originalBottom - originalTop);

            var movedSelectionBounds = new Rect(
                originalSelectionBounds.X + dx,
                originalSelectionBounds.Y + dy,
                originalSelectionBounds.Width,
                originalSelectionBounds.Height);

            this.ApplyLabelEditorMoveSnap(
                selectedRows,
                sourceRects,
                ref movedSelectionBounds,
                snapGuides,
                suppressSnap);

            double snappedDx = movedSelectionBounds.X - originalSelectionBounds.X;
            double snappedDy = movedSelectionBounds.Y - originalSelectionBounds.Y;

            foreach (var row in selectedRows)
            {
                var originalRect = sourceRects[row];
                row.X = originalRect.X + snappedDx;
                row.Y = originalRect.Y + snappedDy;
                row.Width = originalRect.Width;
                row.Height = originalRect.Height;
            }

            this.RefreshLabelEditorOverlay(snapGuides);
            return;
        }

        foreach (var row in selectedRows)
        {
            var originalRect = sourceRects[row];

            double left = originalRect.Left;
            double top = originalRect.Top;
            double right = originalRect.Right;
            double bottom = originalRect.Bottom;

            switch (this.thisLabelEditorDragMode)
            {
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
    // Applies saved KiCad mirror flags onto the calibration-box coordinates by swapping edges.
    // Calibration mode encodes mirroring by having Left>Right and/or Top>Bottom.
    // ###########################################################################################
    private void ApplyKiCadCalibrationMirrorFlagsToBox(bool mirrorX, bool mirrorY)
    {
        if (mirrorX)
        {
            (this.thisKiCadCalibrationImageLeft, this.thisKiCadCalibrationImageRight) =
                (this.thisKiCadCalibrationImageRight, this.thisKiCadCalibrationImageLeft);
        }

        if (mirrorY)
        {
            (this.thisKiCadCalibrationImageTop, this.thisKiCadCalibrationImageBottom) =
                (this.thisKiCadCalibrationImageBottom, this.thisKiCadCalibrationImageTop);
        }
    }
    
    // ###########################################################################################
    // Applies keyboard move, expand, or shrink operations to the KiCad trace calibration box.
    // Arrow keys move by 1 px, Shift expands in the pressed direction, and Alt shrinks from
    // the opposite side of the pressed direction, matching the component label editor behavior.
    // ###########################################################################################
    private bool ApplyKiCadTraceCalibrationKeyboardStep(Key key, KeyModifiers modifiers)
    {
        if (!this.thisIsKiCadTraceCalibrationMode ||
            this.currentFullResBitmap == null ||
            this.thisKiCadTraceCalibrationDragMode != LabelEditorDragMode.None ||
            this.SchematicsLabelEditorMenuBorder.IsVisible)
        {
            return false;
        }

        if (modifiers.HasFlag(KeyModifiers.Shift) && modifiers.HasFlag(KeyModifiers.Alt))
        {
            return false;
        }

        bool thisIsShift = modifiers.HasFlag(KeyModifiers.Shift);
        bool thisIsAlt = modifiers.HasFlag(KeyModifiers.Alt);
        const double thisStep = 1.0;
        bool thisChanged = false;

        bool thisMirrorX = this.thisKiCadCalibrationImageLeft > this.thisKiCadCalibrationImageRight;
        bool thisMirrorY = this.thisKiCadCalibrationImageTop > this.thisKiCadCalibrationImageBottom;

        double thisLeft = Math.Min(this.thisKiCadCalibrationImageLeft, this.thisKiCadCalibrationImageRight);
        double thisRight = Math.Max(this.thisKiCadCalibrationImageLeft, this.thisKiCadCalibrationImageRight);
        double thisTop = Math.Min(this.thisKiCadCalibrationImageTop, this.thisKiCadCalibrationImageBottom);
        double thisBottom = Math.Max(this.thisKiCadCalibrationImageTop, this.thisKiCadCalibrationImageBottom);

        if (!thisIsShift && !thisIsAlt)
        {
            switch (key)
            {
                case Key.Left:
                    thisLeft -= thisStep;
                    thisRight -= thisStep;
                    thisChanged = true;
                    break;

                case Key.Right:
                    thisLeft += thisStep;
                    thisRight += thisStep;
                    thisChanged = true;
                    break;

                case Key.Up:
                    thisTop -= thisStep;
                    thisBottom -= thisStep;
                    thisChanged = true;
                    break;

                case Key.Down:
                    thisTop += thisStep;
                    thisBottom += thisStep;
                    thisChanged = true;
                    break;
            }
        }
        else if (thisIsShift)
        {
            switch (key)
            {
                case Key.Left:
                    thisLeft -= thisStep;
                    thisChanged = true;
                    break;

                case Key.Right:
                    thisRight += thisStep;
                    thisChanged = true;
                    break;

                case Key.Up:
                    thisTop -= thisStep;
                    thisChanged = true;
                    break;

                case Key.Down:
                    thisBottom += thisStep;
                    thisChanged = true;
                    break;
            }
        }
        else if (thisIsAlt)
        {
            switch (key)
            {
                case Key.Left:
                    if ((thisRight - thisLeft) > thisStep)
                    {
                        thisRight -= thisStep;
                        thisChanged = true;
                    }
                    break;

                case Key.Right:
                    if ((thisRight - thisLeft) > thisStep)
                    {
                        thisLeft += thisStep;
                        thisChanged = true;
                    }
                    break;

                case Key.Up:
                    if ((thisBottom - thisTop) > thisStep)
                    {
                        thisBottom -= thisStep;
                        thisChanged = true;
                    }
                    break;

                case Key.Down:
                    if ((thisBottom - thisTop) > thisStep)
                    {
                        thisTop += thisStep;
                        thisChanged = true;
                    }
                    break;
            }
        }

        if (!thisChanged)
        {
            return false;
        }

        this.thisKiCadCalibrationImageLeft = thisMirrorX ? thisRight : thisLeft;
        this.thisKiCadCalibrationImageRight = thisMirrorX ? thisLeft : thisRight;
        this.thisKiCadCalibrationImageTop = thisMirrorY ? thisBottom : thisTop;
        this.thisKiCadCalibrationImageBottom = thisMirrorY ? thisTop : thisBottom;

        this.RefreshKiCadOverlay(forceImmediate: true);
        return true;
    }

    // ###########################################################################################
    // Applies keyboard move, expand, or shrink operations to the selected editor rectangle.
    // Arrow keys move by 1 px, Shift expands in the pressed direction, and Alt shrinks from
    // the opposite side of the pressed direction. Each committed step is undoable.
    // Keyboard operations do not snap, but exact neighbor matches show the dashed guide.
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

        var snapGuides = new List<(Point Start, Point End)>();

        if (!isShift && !isAlt)
        {
            double movedLeft = selectedRows.Min(row => row.X);
            double movedTop = selectedRows.Min(row => row.Y);
            double movedRight = selectedRows.Max(row => row.X + row.Width);
            double movedBottom = selectedRows.Max(row => row.Y + row.Height);

            var movedSelectionBounds = new Rect(
                movedLeft,
                movedTop,
                movedRight - movedLeft,
                movedBottom - movedTop);

            this.ApplyLabelEditorMoveSnap(
                selectedRows,
                sourceRects,
                ref movedSelectionBounds,
                snapGuides,
                suppressSnap: false,
                snapOnMatch: false);
        }
        else if (TryGetKeyboardLabelEditorResizeDragMode(key, modifiers, out var keyboardResizeDragMode))
        {
            foreach (var row in selectedRows)
            {
                double left = row.X;
                double top = row.Y;
                double right = row.X + row.Width;
                double bottom = row.Y + row.Height;

                this.ApplyLabelEditorResizeSnap(
                    row,
                    ref left,
                    ref top,
                    ref right,
                    ref bottom,
                    snapGuides,
                    suppressSnap: false,
                    snapOnMatch: false,
                    dragModeOverride: keyboardResizeDragMode);
            }
        }

        this.PushLabelEditorUndoState(undoState);
        this.RefreshLabelEditorOverlay(snapGuides);
        return true;
    }

    // ###########################################################################################
    // Handles keyboard interaction for label-editor and KiCad calibration workflows.
    // Ctrl+Z undoes label-editor changes and Ctrl+Y redoes them within the current editor session.
    // Pressing D duplicates the currently selected editor rectangle and opens the new-label prompt.
    // ###########################################################################################
    private void OnSchematicsKeyDown(object? sender, KeyEventArgs e)
    {
        this.UpdateInteractiveCadTraceHoverShiftState(e.KeyModifiers);

        if (this.thisIsKiCadTraceCalibrationMode)
        {
            if (e.Key == Key.Escape)
            {
                this.CancelKiCadTraceCalibrationMode();
                e.Handled = true;
                return;
            }

            if (this.ApplyKiCadTraceCalibrationKeyboardStep(e.Key, e.KeyModifiers))
            {
                e.Handled = true;
            }

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

        bool thisIsCtrlDown = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        if (thisIsCtrlDown && e.Key == Key.Z)
        {
            if (this.TryUndoLabelEditorChange())
            {
                e.Handled = true;
            }

            return;
        }

        if (thisIsCtrlDown && e.Key == Key.Y)
        {
            if (this.TryRedoLabelEditorChange())
            {
                e.Handled = true;
            }

            return;
        }

        if (!thisIsCtrlDown &&
            !e.KeyModifiers.HasFlag(KeyModifiers.Alt) &&
            e.Key == Key.D)
        {
            if (this.TryDuplicateSelectedLabelEditorHighlight())
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
    // Newly created component rows should be inserted with an empty Region value so they become
    // shared/global entries instead of inheriting the currently selected PAL or NTSC region.
    // ###########################################################################################
    private List<LabelEditorSaveRow> BuildLabelEditorSaveRowsForCurrentSchematic()
    {
        string schematicName = this.GetCurrentSchematicName();

        return this.thisLabelEditorWorkingHighlights
            .Where(row => string.Equals(row.SchematicName, schematicName, StringComparison.OrdinalIgnoreCase))
            .Select(row => new LabelEditorSaveRow
            {
                SchematicName = row.SchematicName.Trim(),
                BoardLabel = row.BoardLabel.Trim(),
                Category = row.Category.Trim(),
                Region = string.Empty,
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
    // Loads raw KiCad overlay data for the current board without blocking the initial board UI.
    // Establishes a persistent per-board runtime cache scope so revisiting the same KiCad project
    // can reuse already-built render and hover caches across board switches in this session.
    // ###########################################################################################
    public async Task LoadKiCadProjectForCurrentBoardAsync()
    {
        int loadVersion = unchecked(++this.thisKiCadProjectLoadVersion);
        string expectedBoardKey = this.MainWindow?.GetCurrentBoardKey() ?? string.Empty;

        this.thisKiCadProject = null;
        this.thisCurrentKiCadRuntimeCacheScopeKey = this.BuildCurrentKiCadRuntimeCacheScopeKey();
        this.thisKiCadSchematicHoverHitTestCacheByKey.Clear();

        lock (this.thisKiCadPcbNetRenderCacheSync)
        {
            this.thisKiCadPcbNetRenderBuildTaskByKey.Clear();
        }

        lock (this.thisKiCadPcbHoverHitTestCacheSync)
        {
            this.thisKiCadPcbHoverHitTestBuildTaskByKey.Clear();
        }

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
        this.thisImportantSignalNetNamesByDisplayName.Clear();
        this.thisImportantSignalDisplayNames.Clear();
        this.thisSelectedImportantSignalDisplayNames.Clear();
        this.ResetKiCadHoverHitTestThrottle();
        this.ClearKiCadOverlay();

        this.KiCadNetConnectionsPanel.IsVisible = false;
        this.KiCadNetConnectionsHeaderTextBlock.Text = "Netlist name";
        this.KiCadNetConnectionsList.ItemsSource = null;
        this.UpdateKiCadNetConnectionsClearButtonState(false);

        this.UpdateImportantSignalsHeaderText();
        this.ImportantSignalsListBox.ItemsSource = null;
        this.ImportantSignalsListBox.SelectedItems?.Clear();
        this.ImportantSignalsPanel.IsVisible = false;
        this.UpdateImportantSignalsClearButtonState(false);

        this.RestoreBoardSettings(expectedBoardKey);

        var rawPaths = this.MainWindow?.GetCurrentBoardKiCadRawPaths() ?? new List<string>();
        if (rawPaths.Count == 0)
        {
            this.thisCurrentKiCadRuntimeCacheScopeKey = string.Empty;
            this.RebuildImportantSignalsPanel();
            this.RestoreBoardSettings(expectedBoardKey);
            this.RefreshKiCadOverlay();
            return;
        }

        var boardEntry = this.MainWindow?.GetCurrentBoardEntry();
        string hardwareName = boardEntry?.HardwareName ?? string.Empty;
        string boardName = boardEntry?.BoardName ?? string.Empty;

        KiCadProjectBundle? loadedProject = null;

        try
        {
            loadedProject = await Task.Run(async () =>
                await KiCadProjectLoader.LoadRawAsync(rawPaths, hardwareName, boardName));
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to load KiCad project in background - [{ex}]");
        }

        if (loadVersion != this.thisKiCadProjectLoadVersion)
        {
            return;
        }

        string currentBoardKey = this.MainWindow?.GetCurrentBoardKey() ?? string.Empty;
        if (!string.Equals(expectedBoardKey, currentBoardKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        this.thisKiCadProject = loadedProject;

        var activeScope = this.GetOrCreateCurrentKiCadRuntimeCacheScope();

        lock (this.thisKiCadPcbNetRenderCacheSync)
        {
            this.thisKiCadPcbNetRenderCacheByKey.Clear();
            this.thisKiCadPcbNetRenderBuildTaskByKey.Clear();

            if (activeScope != null)
            {
                foreach (var pair in activeScope.NetRenderCacheByKey)
                {
                    this.thisKiCadPcbNetRenderCacheByKey[pair.Key] = pair.Value;
                }

                foreach (var pair in activeScope.NetRenderBuildTaskByKey)
                {
                    this.thisKiCadPcbNetRenderBuildTaskByKey[pair.Key] = pair.Value;
                }
            }
        }

        lock (this.thisKiCadPcbHoverHitTestCacheSync)
        {
            this.thisKiCadPcbHoverHitTestCacheByKey.Clear();
            this.thisKiCadPcbHoverHitTestBuildTaskByKey.Clear();

            if (activeScope != null)
            {
                foreach (var pair in activeScope.HoverHitTestCacheByKey)
                {
                    this.thisKiCadPcbHoverHitTestCacheByKey[pair.Key] = pair.Value;
                }

                foreach (var pair in activeScope.HoverHitTestBuildTaskByKey)
                {
                    this.thisKiCadPcbHoverHitTestBuildTaskByKey[pair.Key] = pair.Value;
                }
            }
        }

        this.thisSelectedKiCadReferences.Clear();
        this.thisSelectedKiCadNormalizedNetNames.Clear();
        this.thisLockedKiCadNetNames.Clear();
        this.thisHoveredKiCadNetName = null;
        this.thisHoveredKiCadPadNumber = null;
        this.thisLastKiCadNetConnectionsSignature = string.Empty;
        this.thisLastThumbnailHighlightSignature = string.Empty;
        this.thisImportantSignalNetNamesByDisplayName.Clear();
        this.thisImportantSignalDisplayNames.Clear();
        this.thisSelectedImportantSignalDisplayNames.Clear();
        this.ResetKiCadHoverHitTestThrottle();

        this.RebuildImportantSignalsPanel();
        this.RestoreBoardSettings(currentBoardKey);
        this.RefreshKiCadOverlay();
    }

    // ###########################################################################################
    // Rebuilds the grouped "Important signals" panel from the current board data and available KiCad
    // net names. Only display groups that can resolve to at least one real KiCad normalized net name.
    // Contributor mode logs duplicate groups, duplicate mappings, unresolved rows, and final totals.
    // ###########################################################################################
    private void RebuildImportantSignalsPanel()
    {
        this.thisImportantSignalNetNamesByDisplayName.Clear();
        this.thisImportantSignalDisplayNames.Clear();
        this.thisSelectedImportantSignalDisplayNames.Clear();

        this.UpdateImportantSignalsHeaderText();
        this.ImportantSignalsListBox.ItemsSource = null;
        this.ImportantSignalsListBox.SelectedItems?.Clear();
        this.ImportantSignalsPanel.IsVisible = false;
        this.UpdateImportantSignalsClearButtonState(false);

        if (!this.HasCurrentSchematicKiCadTraces())
        {
            return;
        }

        var boardData = this.MainWindow?.CurrentBoardData;
        var root = this.thisKiCadProject?.Root;

        if (boardData == null || root == null || root.Pcb.Count == 0)
        {
            if (UserSettings.ContributorMode)
            {
                Logger.Warning("Important signals debug: panel rebuild aborted because board data or KiCad PCB data is unavailable");
            }

            return;
        }

        var knownNetNames = new HashSet<string>(
            root.Pcb
                .SelectMany(pcb => pcb.Nets.List)
                .SelectMany(net => new[]
                {
                    net.Name?.Trim() ?? string.Empty,
                    net.NormalizedName?.Trim() ?? string.Empty
                })
                .Where(netName => !string.IsNullOrWhiteSpace(netName)),
            StringComparer.OrdinalIgnoreCase);

        var normalizedNameByAnyKnownName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pcb in root.Pcb)
        {
            foreach (var net in pcb.Nets.List)
            {
                string normalizedName = net.NormalizedName?.Trim() ?? string.Empty;
                string name = net.Name?.Trim() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(normalizedName))
                {
                    normalizedNameByAnyKnownName[normalizedName] = normalizedName;
                }

                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(normalizedName))
                {
                    normalizedNameByAnyKnownName[name] = normalizedName;
                }
            }
        }

        bool isContributorMode = UserSettings.ContributorMode;

        if (isContributorMode)
        {
            Logger.Info($"Important signals debug: Excel rows loaded [{boardData.KiCadImportantSignals.Count}]");
            Logger.Info($"Important signals debug: loaded [{knownNetNames.Count}] raw KiCad net names");
            Logger.Info($"Important signals debug: built [{normalizedNameByAnyKnownName.Count}] KiCad net name mappings");

            var normalizedNetNames = root.Pcb
                .SelectMany(pcb => pcb.Nets.List)
                .Select(net => net.NormalizedName?.Trim() ?? string.Empty)
                .Where(netName => !string.IsNullOrWhiteSpace(netName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(netName => netName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Logger.Info($"Important signals debug: loaded [{normalizedNetNames.Count}] normalized KiCad net names");

            foreach (string normalizedNetName in normalizedNetNames)
            {
                Logger.Info($"Important signals debug: normalized KiCad net name [{normalizedNetName}]");
            }
        }

        var excelRowCountByDisplayName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var seenResolvedDisplayNameToNetPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicateResolvedDisplayNameToNetPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int invalidRowCount = 0;
        int unresolvedRowCount = 0;
        int resolvedRowCount = 0;

        foreach (var entry in boardData.KiCadImportantSignals)
        {
            string displayName = entry.DisplayName?.Trim() ?? string.Empty;
            string kiCadNetName = entry.KiCadNetName?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(kiCadNetName))
            {
                invalidRowCount++;

                if (isContributorMode)
                {
                    Logger.Warning(
                        $"Important signals debug: skipped invalid Excel row with DisplayName=[{displayName}] KiCadNetName=[{kiCadNetName}]");
                }

                continue;
            }

            if (excelRowCountByDisplayName.TryGetValue(displayName, out int currentDisplayNameCount))
            {
                excelRowCountByDisplayName[displayName] = currentDisplayNameCount + 1;
            }
            else
            {
                excelRowCountByDisplayName[displayName] = 1;
            }

            if (!normalizedNameByAnyKnownName.TryGetValue(kiCadNetName, out string? normalizedNetName) ||
                string.IsNullOrWhiteSpace(normalizedNetName))
            {
                unresolvedRowCount++;

                if (isContributorMode)
                {
                    Logger.Warning(
                        $"Important signals debug: Excel KiCad net name [{kiCadNetName}] for display name [{displayName}] did not match any loaded KiCad net name");
                }

                continue;
            }

            if (!this.thisImportantSignalNetNamesByDisplayName.TryGetValue(displayName, out var netNames))
            {
                netNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                this.thisImportantSignalNetNamesByDisplayName[displayName] = netNames;
                this.thisImportantSignalDisplayNames.Add(displayName);
            }

            string resolvedPairKey = $"{displayName}\u001F{normalizedNetName}";

            if (!seenResolvedDisplayNameToNetPairs.Add(resolvedPairKey))
            {
                duplicateResolvedDisplayNameToNetPairs.Add(resolvedPairKey);

                if (isContributorMode)
                {
                    Logger.Warning(
                        $"Important signals debug: duplicate resolved mapping detected for display name [{displayName}] -> normalized net [{normalizedNetName}]");
                }
            }

            netNames.Add(normalizedNetName);
            resolvedRowCount++;
        }

        if (isContributorMode)
        {
            foreach (var duplicateDisplayName in excelRowCountByDisplayName
                         .Where(entry => entry.Value > 1)
                         .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                int resolvedNetCount = this.thisImportantSignalNetNamesByDisplayName.TryGetValue(duplicateDisplayName.Key, out var resolvedNetNames)
                    ? resolvedNetNames.Count
                    : 0;

                Logger.Info(
                    $"Important signals debug: display name [{duplicateDisplayName.Key}] appears in [{duplicateDisplayName.Value}] Excel rows and resolves to [{resolvedNetCount}] unique KiCad nets");
            }

            Logger.Info($"Important signals debug: invalid Excel rows skipped [{invalidRowCount}]");
            Logger.Info($"Important signals debug: unresolved Excel rows [{unresolvedRowCount}]");
            Logger.Info($"Important signals debug: resolved Excel rows [{resolvedRowCount}]");
            Logger.Info($"Important signals debug: duplicate resolved mappings [{duplicateResolvedDisplayNameToNetPairs.Count}]");
            Logger.Info($"Important signals debug: resolved display groups [{this.thisImportantSignalDisplayNames.Count}]");
        }

        if (this.thisImportantSignalDisplayNames.Count == 0)
        {
            if (isContributorMode)
            {
                Logger.Warning("Important signals debug: no Excel signal rows could be resolved to loaded KiCad net names");
            }

            return;
        }

        var items = this.thisImportantSignalDisplayNames
            .Select(displayName =>
            {
                var mappedNetNames = this.thisImportantSignalNetNamesByDisplayName.TryGetValue(displayName, out var netNames)
                    ? netNames.OrderBy(netName => netName, StringComparer.OrdinalIgnoreCase).ToList()
                    : new List<string>();

                return new ImportantSignalListItem
                {
                    DisplayName = displayName,
                    ToolTipText = string.Join(", ", mappedNetNames)
                };
            })
            .ToList();

        this.UpdateImportantSignalsHeaderText();
        this.ImportantSignalsListBox.ItemsSource = items;
        this.ImportantSignalsPanel.IsVisible = true;
        this.UpdateImportantSignalsClearButtonState(true);

        if (isContributorMode)
        {
            Logger.Info($"Important signals debug: built [{items.Count}] visible important signal groups");

            foreach (var item in items)
            {
                Logger.Info($"Important signals debug: display name [{item.DisplayName}] -> [{item.ToolTipText}]");
            }
        }
    }

    // ###########################################################################################
    // Synchronizes the currently selected important signal display names from the visible list control.
    // ###########################################################################################
    private void SyncSelectedImportantSignalsFromList()
    {
        this.thisSelectedImportantSignalDisplayNames.Clear();

        foreach (var item in this.ImportantSignalsListBox.SelectedItems?.Cast<ImportantSignalListItem>() ?? Enumerable.Empty<ImportantSignalListItem>())
        {
            string displayName = item.DisplayName?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                this.thisSelectedImportantSignalDisplayNames.Add(displayName);
            }
        }

        this.UpdateImportantSignalsHeaderText();
        this.UpdateImportantSignalsClearButtonState(this.ImportantSignalsPanel.IsVisible);
    }

    // ###########################################################################################
    // Clears the selected important signal list items and also clears any matching explicit KiCad
    // net selections so overlapping net picks can be cleared from either panel.
    // ###########################################################################################
    private void ClearImportantSignalsSelection()
    {
        var selectedNetNames = this.BuildSelectedImportantSignalNetNames();

        this.ImportantSignalsListBox.SelectedItems?.Clear();
        this.thisSelectedImportantSignalDisplayNames.Clear();
        this.ClearKiCadTraceSelectionsForNetNames(selectedNetNames);
        this.UpdateImportantSignalsHeaderText();
        this.UpdateImportantSignalsClearButtonState(this.ImportantSignalsPanel.IsVisible);
        this.RefreshKiCadOverlay(forceImmediate: true);
        this.RefreshBlinkStateFromCurrentSelection();
    }

    // ###########################################################################################
    // Builds the effective set of normalized KiCad net names selected through the "Important signals"
    // panel by expanding each selected display group to all mapped KiCad net names.
    // ###########################################################################################
    private HashSet<string> BuildSelectedImportantSignalNetNames()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string displayName in this.thisSelectedImportantSignalDisplayNames)
        {
            if (!this.thisImportantSignalNetNamesByDisplayName.TryGetValue(displayName, out var netNames))
            {
                continue;
            }

            foreach (string netName in netNames)
            {
                if (!string.IsNullOrWhiteSpace(netName))
                {
                    result.Add(netName);
                }
            }
        }

        return result;
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
    // Renders resolved schematic wire paths for the currently selected normalized net names.
    // Uses a render-only overlay control instead of creating one Polyline control per path.
    // ###########################################################################################
/*
    private void RenderKiCadSchematicGeometry(KiCadProjectView view)
    {
        this.RenderKiCadSchematicGeometry(view, this.BuildActiveKiCadTracePreviewNetNames());
    }
*/

    // ###########################################################################################
    // Returns the active KiCad calibration for the current schematic.
    // Uses the temporary interactive box calibration while calibration mode is active; otherwise
    // loads the persisted box calibration from the board JSON file.
    // ###########################################################################################
    private KiCadViewCalibration GetKiCadViewCalibration(string schematicName)
    {
        if (this.thisIsKiCadTraceCalibrationMode &&
            string.Equals(this.GetCurrentSchematicName(), schematicName, StringComparison.OrdinalIgnoreCase) &&
            this.currentFullResBitmap != null &&
            this.currentFullResBitmap.PixelSize.Width > 0 &&
            this.currentFullResBitmap.PixelSize.Height > 0)
        {
            double left = Math.Min(this.thisKiCadCalibrationImageLeft, this.thisKiCadCalibrationImageRight);
            double right = Math.Max(this.thisKiCadCalibrationImageLeft, this.thisKiCadCalibrationImageRight);
            double top = Math.Min(this.thisKiCadCalibrationImageTop, this.thisKiCadCalibrationImageBottom);
            double bottom = Math.Max(this.thisKiCadCalibrationImageTop, this.thisKiCadCalibrationImageBottom);

            return new KiCadViewCalibration
            {
                ScaleX = (right - left) / this.currentFullResBitmap.PixelSize.Width,
                ScaleY = (bottom - top) / this.currentFullResBitmap.PixelSize.Height,
                OffsetX = left,
                OffsetY = top,
                MirrorX = this.thisKiCadCalibrationImageLeft > this.thisKiCadCalibrationImageRight,
                MirrorY = this.thisKiCadCalibrationImageTop > this.thisKiCadCalibrationImageBottom
            };
        }

        string excelPath = this.MainWindow?.GetCurrentBoardExcelPath() ?? string.Empty;

        if (BoardComponentHighlightStorage.TryLoadKiCadCalibration(
                excelPath,
                schematicName,
                out _,
                out double offsetX,
                out double offsetY,
                out double scaleX,
                out double scaleY,
                out bool mirrorX,
                out bool mirrorY))
        {
            return new KiCadViewCalibration
            {
                ScaleX = scaleX,
                ScaleY = scaleY,
                OffsetX = offsetX,
                OffsetY = offsetY,
                MirrorX = mirrorX,
                MirrorY = mirrorY
            };
        }

        return KiCadViewCalibration.Identity;
    }

    // ###########################################################################################
    // Maps one KiCad world-space point into the local image coordinate system currently used by
    // the schematics image and overlays using the active box-based calibration model.
    // ###########################################################################################
    private Point MapKiCadWorldToLocal(
        double worldX,
        double worldY,
        Rect worldBounds,
        Rect contentRect,
        KiCadViewCalibration calibration)
    {
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

        double localX = contentRect.X + (nx * contentRect.Width);
        double localY = contentRect.Y + (ny * contentRect.Height);

        if (this.currentFullResBitmap != null)
        {
            if (this.currentFullResBitmap.PixelSize.Width > 0)
            {
                localX += calibration.OffsetX * (contentRect.Width / this.currentFullResBitmap.PixelSize.Width);
            }

            if (this.currentFullResBitmap.PixelSize.Height > 0)
            {
                localY += calibration.OffsetY * (contentRect.Height / this.currentFullResBitmap.PixelSize.Height);
            }
        }
        else
        {
            localX += calibration.OffsetX;
            localY += calibration.OffsetY;
        }

        return new Point(localX, localY);
    }

    // ###########################################################################################
    // Converts one KiCad world-space length into the current local overlay coordinate space using
    // the active box-based calibration model.
    // ###########################################################################################
    private double MapKiCadWorldLengthToLocal(
        double worldLength,
        Rect worldBounds,
        Rect contentRect,
        KiCadViewCalibration calibration)
    {
        double thisScaleX = contentRect.Width / Math.Max(0.0001, worldBounds.Width);
        double thisScaleY = contentRect.Height / Math.Max(0.0001, worldBounds.Height);

        thisScaleX *= Math.Abs(calibration.ScaleX);
        thisScaleY *= Math.Abs(calibration.ScaleY);

        return worldLength * ((thisScaleX + thisScaleY) / 2.0);
    }

    // ###########################################################################################
    // Computes a world bounding box for all PCB geometry used by the MVP overlay.
    // Copper zones are included so rendering and hit testing use the full occupied board area.
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

        foreach (var zone in pcb.Routing.Zones)
        {
            foreach (var polygon in zone.FilledPolygons.Count > 0 ? zone.FilledPolygons : zone.OutlinePolygons)
            {
                foreach (var point in polygon.Points)
                {
                    Include(point.X, point.Y);
                }
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
    // Uses adaptive subdivision based on on-screen curve length so long arcs stay smooth without
    // exploding the point count for short arcs.
    // ###########################################################################################
    private List<Point> SampleQuadraticBezier(Point start, Point control, Point end, int steps)
    {
        double firstLegLength = Math.Sqrt(
            Math.Pow(control.X - start.X, 2.0) +
            Math.Pow(control.Y - start.Y, 2.0));

        double secondLegLength = Math.Sqrt(
            Math.Pow(end.X - control.X, 2.0) +
            Math.Pow(end.Y - control.Y, 2.0));

        double approximateScreenLength = firstLegLength + secondLegLength;

        int adaptiveSteps = Math.Clamp(
            (int)Math.Ceiling(approximateScreenLength / 6.0),
            12,
            96);

        int effectiveSteps = Math.Max(2, Math.Max(steps, adaptiveSteps));

        var points = new List<Point>(effectiveSteps + 1);

        for (int i = 0; i <= effectiveSteps; i++)
        {
            double t = (double)i / effectiveSteps;
            double mt = 1.0 - t;

            double x = (mt * mt * start.X) + (2.0 * mt * t * control.X) + (t * t * end.X);
            double y = (mt * mt * start.Y) + (2.0 * mt * t * control.Y) + (t * t * end.Y);

            points.Add(new Point(x, y));
        }

        return points;
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
    // Projects one schematic-local point back into KiCad world coordinates using the active
    // box-based calibration model.
    // ###########################################################################################
    private bool TryMapLocalToKiCadWorld(
        Point localPoint,
        Rect worldBounds,
        Rect contentRect,
        KiCadViewCalibration calibration,
        out Point worldPoint)
    {
        worldPoint = default;

        if (worldBounds.Width <= 0 || worldBounds.Height <= 0)
        {
            return false;
        }

        double thisLocalX = localPoint.X;
        double thisLocalY = localPoint.Y;

        if (this.currentFullResBitmap != null)
        {
            if (this.currentFullResBitmap.PixelSize.Width > 0)
            {
                thisLocalX -= calibration.OffsetX * (contentRect.Width / this.currentFullResBitmap.PixelSize.Width);
            }

            if (this.currentFullResBitmap.PixelSize.Height > 0)
            {
                thisLocalY -= calibration.OffsetY * (contentRect.Height / this.currentFullResBitmap.PixelSize.Height);
            }
        }
        else
        {
            thisLocalX -= calibration.OffsetX;
            thisLocalY -= calibration.OffsetY;
        }

        double thisNormalizedX = (thisLocalX - contentRect.X) / contentRect.Width;
        double thisNormalizedY = (thisLocalY - contentRect.Y) / contentRect.Height;

        if (Math.Abs(calibration.ScaleX) > 1e-10)
        {
            thisNormalizedX /= calibration.ScaleX;
        }

        if (Math.Abs(calibration.ScaleY) > 1e-10)
        {
            thisNormalizedY /= calibration.ScaleY;
        }

        if (calibration.MirrorX)
        {
            thisNormalizedX = 1.0 - thisNormalizedX;
        }

        if (calibration.MirrorY)
        {
            thisNormalizedY = 1.0 - thisNormalizedY;
        }

        worldPoint = new Point(
            (thisNormalizedX * worldBounds.Width) + worldBounds.X,
            (thisNormalizedY * worldBounds.Height) + worldBounds.Y);

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
    // Enumerates all schematic label types that can identify one net on a schematic page.
    // ###########################################################################################
    private static IEnumerable<KiCadSchematicLabel> EnumerateKiCadSchematicNetLabels(KiCadSchematic schematic)
    {
        foreach (var label in schematic.Labels.Local)
        {
            yield return label;
        }

        foreach (var label in schematic.Labels.Global)
        {
            yield return label;
        }

        foreach (var label in schematic.Labels.Hierarchical)
        {
            yield return label;
        }
    }

    // ###########################################################################################
    // Hit-tests KiCad schematic nets using a cached local-space spatial index for labels and
    // resolved wire segments so hover remains responsive on dense schematic pages.
    // ###########################################################################################
    private void HitTestKiCadSchematicOverlayForHover(KiCadProjectView view, Point localPoint)
    {
        if (this.thisKiCadProject == null ||
            view.SourceIndex < 0 ||
            view.SourceIndex >= this.thisKiCadProject.Root.Schematics.Count)
        {
            this.SetHoveredKiCadNet(null);
            this.thisHoveredKiCadPadNumber = null;
            return;
        }

        var schematic = this.thisKiCadProject.Root.Schematics[view.SourceIndex];
        var contentRect = this.GetImageContentRect();
        var worldBounds = this.GetKiCadSchematicWorldBounds(schematic);

        if (contentRect.Width <= 0 ||
            contentRect.Height <= 0 ||
            worldBounds.Width <= 0 ||
            worldBounds.Height <= 0)
        {
            this.SetHoveredKiCadNet(null);
            this.thisHoveredKiCadPadNumber = null;
            return;
        }

        string currentSchematicName = this.GetCurrentSchematicName();
        var calibration = this.GetKiCadViewCalibration(currentSchematicName);

        var cache = this.GetOrCreateKiCadSchematicHoverHitTestCache(
            view,
            schematic,
            worldBounds,
            contentRect,
            calibration,
            currentSchematicName);

        if (cache == null)
        {
            this.SetHoveredKiCadNet(null);
            this.thisHoveredKiCadPadNumber = null;
            return;
        }

        double hitThresholdLocal = Math.Max(3.0, 8.0 / Math.Max(0.0001, this.schematicsMatrix.M11));

        int minCellX = TabSchematics.GetKiCadHoverCellCoord(localPoint.X - hitThresholdLocal, cache.CellSizeLocal);
        int maxCellX = TabSchematics.GetKiCadHoverCellCoord(localPoint.X + hitThresholdLocal, cache.CellSizeLocal);
        int minCellY = TabSchematics.GetKiCadHoverCellCoord(localPoint.Y - hitThresholdLocal, cache.CellSizeLocal);
        int maxCellY = TabSchematics.GetKiCadHoverCellCoord(localPoint.Y + hitThresholdLocal, cache.CellSizeLocal);

        string? bestNetName = null;
        double bestDistance = double.MaxValue;

        var testedLabelIndices = new HashSet<int>();
        var testedSegmentIndices = new HashSet<int>();

        for (int cellY = minCellY; cellY <= maxCellY; cellY++)
        {
            for (int cellX = minCellX; cellX <= maxCellX; cellX++)
            {
                long cellKey = TabSchematics.BuildKiCadHoverCellKey(cellX, cellY);

                if (cache.LabelIndicesByCell.TryGetValue(cellKey, out var labelIndices))
                {
                    foreach (int labelIndex in labelIndices)
                    {
                        if (!testedLabelIndices.Add(labelIndex))
                        {
                            continue;
                        }

                        var candidate = cache.LabelCandidates[labelIndex];
                        double dx = candidate.LocalPoint.X - localPoint.X;
                        double dy = candidate.LocalPoint.Y - localPoint.Y;
                        double distance = Math.Sqrt((dx * dx) + (dy * dy));

                        if (distance <= hitThresholdLocal && distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestNetName = candidate.NormalizedNetName;
                        }
                    }
                }

                if (cache.SegmentIndicesByCell.TryGetValue(cellKey, out var segmentIndices))
                {
                    foreach (int segmentIndex in segmentIndices)
                    {
                        if (!testedSegmentIndices.Add(segmentIndex))
                        {
                            continue;
                        }

                        var candidate = cache.SegmentCandidates[segmentIndex];

                        double distance = TabSchematics.DistanceToSegment(
                            localPoint,
                            candidate.StartLocal.X,
                            candidate.StartLocal.Y,
                            candidate.EndLocal.X,
                            candidate.EndLocal.Y);

                        if (distance <= hitThresholdLocal && distance < bestDistance)
                        {
                            bestDistance = distance;
                            bestNetName = candidate.NormalizedNetName;
                        }
                    }
                }
            }
        }

        this.SetHoveredKiCadNet(string.IsNullOrWhiteSpace(bestNetName) ? null : bestNetName);
        this.thisHoveredKiCadPadNumber = null;
    }

    // ###########################################################################################
    // Performs KiCad hover hit-testing for both PCB and schematic views.
    // PCB pages use the existing spatial cache, while schematic pages resolve the nearest net
    // by checking net-label anchors and rendered wire/polyline paths in the active sheet.
    // ###########################################################################################
    private void HitTestKiCadOverlayForHover(Point pointerInContainer)
    {
        var view = this.ResolveKiCadViewForCurrentSchematic();
        if (view == null || this.thisKiCadProject == null || this.currentFullResBitmap == null)
        {
            this.SetHoveredKiCadNet(null);
            return;
        }

        if (!TryInvert(this.schematicsMatrix, out var inv))
        {
            this.SetHoveredKiCadNet(null);
            return;
        }

        var localPoint = new Point(
            (pointerInContainer.X * inv.M11) + (pointerInContainer.Y * inv.M21) + inv.M31,
            (pointerInContainer.X * inv.M12) + (pointerInContainer.Y * inv.M22) + inv.M32);

        bool isTop = string.Equals(view.Type, "pcb_top", StringComparison.OrdinalIgnoreCase);
        bool isBottom = string.Equals(view.Type, "pcb_bottom", StringComparison.OrdinalIgnoreCase);
        bool isSchematic = string.Equals(view.Type, "schematic", StringComparison.OrdinalIgnoreCase);

        if (isSchematic)
        {
            this.HitTestKiCadSchematicOverlayForHover(view, localPoint);
            return;
        }

        if (!isTop && !isBottom)
        {
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

        if (!this.TryMapLocalToKiCadWorld(localPoint, worldBounds, contentRect, calibration, out var worldPoint))
        {
            this.SetHoveredKiCadNet(null);
            return;
        }

        var cache = this.GetOrCreateKiCadPcbHoverHitTestCache(pcb, view.SourceIndex, requiredLayer);
        if (cache == null)
        {
            this.SetHoveredKiCadNet(null);
            this.thisHoveredKiCadPadNumber = null;
            return;
        }

        const double zoneHoverToleranceWorld = 0.4;
        double searchRadiusWorld = Math.Max(0.8, Math.Max(cache.MaxHitRadiusWorld, zoneHoverToleranceWorld));

        int minCellX = TabSchematics.GetKiCadHoverCellCoord(worldPoint.X - searchRadiusWorld, cache.CellSizeWorld);
        int maxCellX = TabSchematics.GetKiCadHoverCellCoord(worldPoint.X + searchRadiusWorld, cache.CellSizeWorld);
        int minCellY = TabSchematics.GetKiCadHoverCellCoord(worldPoint.Y - searchRadiusWorld, cache.CellSizeWorld);
        int maxCellY = TabSchematics.GetKiCadHoverCellCoord(worldPoint.Y + searchRadiusWorld, cache.CellSizeWorld);

        var testedPadIndices = new HashSet<int>();
        var testedZoneIndices = new HashSet<int>();
        var testedSegmentIndices = new HashSet<int>();
        var testedViaIndices = new HashSet<int>();

        double closestPadDist = double.MaxValue;
        KiCadNetRef? bestPadNet = null;
        string? bestPadNumber = null;

        for (int cellY = minCellY; cellY <= maxCellY; cellY++)
        {
            for (int cellX = minCellX; cellX <= maxCellX; cellX++)
            {
                long cellKey = TabSchematics.BuildKiCadHoverCellKey(cellX, cellY);

                if (!cache.PadIndicesByCell.TryGetValue(cellKey, out var padIndices))
                {
                    continue;
                }

                foreach (int padIndex in padIndices)
                {
                    if (!testedPadIndices.Add(padIndex))
                    {
                        continue;
                    }

                    var candidate = cache.PadCandidates[padIndex];

                    double dx = candidate.CenterWorld.X - worldPoint.X;
                    double dy = candidate.CenterWorld.Y - worldPoint.Y;
                    double dist = Math.Sqrt((dx * dx) + (dy * dy));

                    if (dist < candidate.HitRadiusWorld && dist < closestPadDist)
                    {
                        closestPadDist = dist;
                        bestPadNet = candidate.Net;
                        bestPadNumber = candidate.PadNumber;
                    }
                }
            }
        }

        if (bestPadNet != null)
        {
            string? foundPadNet = bestPadNet.NormalizedName?.Trim();
            this.SetHoveredKiCadNet(string.IsNullOrWhiteSpace(foundPadNet) ? null : foundPadNet);
            this.thisHoveredKiCadPadNumber = bestPadNumber?.Trim();
            return;
        }

        double closestZoneDist = double.MaxValue;
        KiCadNetRef? bestZoneNet = null;

        for (int cellY = minCellY; cellY <= maxCellY; cellY++)
        {
            for (int cellX = minCellX; cellX <= maxCellX; cellX++)
            {
                long cellKey = TabSchematics.BuildKiCadHoverCellKey(cellX, cellY);

                if (!cache.ZoneIndicesByCell.TryGetValue(cellKey, out var zoneIndices))
                {
                    continue;
                }

                foreach (int zoneIndex in zoneIndices)
                {
                    if (!testedZoneIndices.Add(zoneIndex))
                    {
                        continue;
                    }

                    var candidate = cache.ZoneCandidates[zoneIndex];

                    if (!TabSchematics.IsPointInOrNearZone(
                            worldPoint,
                            candidate.PolygonsWorld,
                            zoneHoverToleranceWorld,
                            out double zoneDistanceWorld))
                    {
                        continue;
                    }

                    if (zoneDistanceWorld < closestZoneDist)
                    {
                        closestZoneDist = zoneDistanceWorld;
                        bestZoneNet = candidate.Net;
                    }
                }
            }
        }

        if (bestZoneNet != null)
        {
            string? foundZoneNet = bestZoneNet.NormalizedName?.Trim();
            this.SetHoveredKiCadNet(string.IsNullOrWhiteSpace(foundZoneNet) ? null : foundZoneNet);
            this.thisHoveredKiCadPadNumber = null;
            return;
        }

        double closestDist = double.MaxValue;
        KiCadNetRef? bestNet = null;

        for (int cellY = minCellY; cellY <= maxCellY; cellY++)
        {
            for (int cellX = minCellX; cellX <= maxCellX; cellX++)
            {
                long cellKey = TabSchematics.BuildKiCadHoverCellKey(cellX, cellY);

                if (cache.SegmentIndicesByCell.TryGetValue(cellKey, out var segmentIndices))
                {
                    foreach (int segmentIndex in segmentIndices)
                    {
                        if (!testedSegmentIndices.Add(segmentIndex))
                        {
                            continue;
                        }

                        var candidate = cache.SegmentCandidates[segmentIndex];

                        double dist = TabSchematics.DistanceToSegment(
                            worldPoint,
                            candidate.StartWorld.X,
                            candidate.StartWorld.Y,
                            candidate.EndWorld.X,
                            candidate.EndWorld.Y);

                        if (dist < closestDist && dist < candidate.HitRadiusWorld)
                        {
                            closestDist = dist;
                            bestNet = candidate.Net;
                        }
                    }
                }

                if (cache.ViaIndicesByCell.TryGetValue(cellKey, out var viaIndices))
                {
                    foreach (int viaIndex in viaIndices)
                    {
                        if (!testedViaIndices.Add(viaIndex))
                        {
                            continue;
                        }

                        var candidate = cache.ViaCandidates[viaIndex];

                        double dx = candidate.CenterWorld.X - worldPoint.X;
                        double dy = candidate.CenterWorld.Y - worldPoint.Y;
                        double dist = Math.Sqrt((dx * dx) + (dy * dy));

                        if (dist < closestDist && dist < candidate.HitRadiusWorld)
                        {
                            closestDist = dist;
                            bestNet = candidate.Net;
                        }
                    }
                }
            }
        }

        string? foundNet = bestNet?.NormalizedName?.Trim();
        this.SetHoveredKiCadNet(string.IsNullOrWhiteSpace(foundNet) ? null : foundNet);
        this.thisHoveredKiCadPadNumber = null;
    }

    // ###########################################################################################
    // Updates the panel displaying connected components and pins for all currently active nets.
    // Rebuilds the list only when the active net set actually changes.
    // ###########################################################################################
    private void UpdateKiCadNetConnectionsPanel()
    {
        this.UpdateKiCadNetConnectionsPanel(this.BuildActiveKiCadTracePreviewNetNames());
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
    // While trace calibration mode is active, the temporary calibration transform is used and the
    // temporary traces-and-pads checkbox can suppress all overlay geometry except the box itself.
    // ###########################################################################################
    private void RefreshKiCadOverlayNow()
    {
        this.ClearKiCadOverlay();

        var activeTracePreviewReferences = this.BuildActiveKiCadTracePreviewReferences();
        var activeTracePreviewNets = this.BuildActiveKiCadTracePreviewNetNames();

        this.UpdateKiCadNetConnectionsPanel(activeTracePreviewNets);

        bool hasActiveKiCadNets = activeTracePreviewNets.Count > 0;

        if (this.thisKiCadProject == null || this.currentFullResBitmap == null)
        {
            return;
        }

        var currentView = this.ResolveKiCadViewForCurrentSchematic();
        if (currentView == null)
        {
            return;
        }

        if (this.thisIsKiCadTraceCalibrationMode)
        {
            bool thisShouldShowCalibrationTracesAndPads =
                this.CheckGlobalShowCalibrationTracesAndPads.IsChecked != false;

            if (thisShouldShowCalibrationTracesAndPads)
            {
                if (string.Equals(currentView.Type, "pcb_top", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(currentView.Type, "pcb_bottom", StringComparison.OrdinalIgnoreCase))
                {
                    var calibrationNetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    var pcb = this.thisKiCadProject.Root.Pcb.ElementAtOrDefault(currentView.SourceIndex);
                    if (pcb != null)
                    {
                        foreach (var net in pcb.Nets.List)
                        {
                            string normalizedName = net.NormalizedName?.Trim() ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(normalizedName))
                            {
                                calibrationNetNames.Add(normalizedName);
                            }
                        }
                    }

                    this.RenderKiCadPcbGeometry(
                        currentView,
                        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                        calibrationNetNames);
                }
                else if (string.Equals(currentView.Type, "schematic", StringComparison.OrdinalIgnoreCase))
                {
                    if (this.thisKiCadProject.SchematicNetPathIndexBySchematicIndex.TryGetValue(currentView.SourceIndex, out var indexByNet))
                    {
                        var calibrationNetNames = new HashSet<string>(
                            indexByNet.Keys.Where(key => !string.IsNullOrWhiteSpace(key)),
                            StringComparer.OrdinalIgnoreCase);

                        this.RenderKiCadSchematicGeometry(currentView, calibrationNetNames);
                    }
                }
            }

            var primitives = this.SchematicsKiCadOverlayCanvas.Primitives.ToList();
            primitives.Add(this.BuildKiCadCalibrationBoxPrimitive());
            this.SchematicsKiCadOverlayCanvas.SetGeometry(primitives);

            return;
        }

        if (string.Equals(currentView.Type, "pcb_top", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(currentView.Type, "pcb_bottom", StringComparison.OrdinalIgnoreCase))
        {
            if (!hasActiveKiCadNets && !this.HasPin1HighlightTargetReference())
            {
                return;
            }

            this.RenderKiCadPcbGeometry(currentView, activeTracePreviewReferences, activeTracePreviewNets);
            return;
        }

        if (!hasActiveKiCadNets)
        {
            return;
        }

        if (string.Equals(currentView.Type, "schematic", StringComparison.OrdinalIgnoreCase))
        {
            this.RenderKiCadSchematicGeometry(currentView, activeTracePreviewNets);
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
    // Only forces an immediate KiCad overlay refresh when the effective hover-driven KiCad state
    // actually changes.
    // ###########################################################################################
    private void SetHoveredComponentBoardLabel(string? boardLabel)
    {
        string? normalizedBoardLabel = string.IsNullOrWhiteSpace(boardLabel)
            ? null
            : boardLabel.Trim();

        string? previousBoardLabel = string.IsNullOrWhiteSpace(this.thisHoveredComponentBoardLabel)
            ? null
            : this.thisHoveredComponentBoardLabel.Trim();

        if (string.Equals(previousBoardLabel, normalizedBoardLabel, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        bool shouldRefreshKiCadOverlay = false;

        if (this.IsBoardMarkPin1OnSelectedComponentEnabled())
        {
            shouldRefreshKiCadOverlay = true;
        }

        if (!shouldRefreshKiCadOverlay && this.IsBoardShowTracesOnSelectedComponentEnabled())
        {
            var previousNets = this.BuildKiCadNormalizedNetNamesForSingleReference(previousBoardLabel);
            var nextNets = this.BuildKiCadNormalizedNetNamesForSingleReference(normalizedBoardLabel);

            if (!TabSchematics.SetEqualsOrdinalIgnoreCase(previousNets, nextNets))
            {
                shouldRefreshKiCadOverlay = true;
            }
        }

        this.thisHoveredComponentBoardLabel = normalizedBoardLabel;
        this.RefreshHoveredComponentHighlightOverlay();

        if (shouldRefreshKiCadOverlay)
        {
            this.RefreshKiCadOverlay(forceImmediate: true);
        }
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
            highlightColor = ParseColorOrDefault(schematic.SchematicHighlightColor, Colors.IndianRed);
            highlightOpacity = ParseOpacityOrDefault(schematic.SchematicHighlightOpacity, 0.20);
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
    // Clears all explicit KiCad trace selections and any selected Important signals so shared nets
    // can always be cleared from the Netlist names panel regardless of where they were added from.
    // ###########################################################################################
    private void ClearAllKiCadTraceSelections()
    {
        this.thisLockedKiCadNetNames.Clear();
        this.thisHoveredKiCadNetName = null;
        this.thisHoveredKiCadPadNumber = null;

        this.ImportantSignalsListBox.SelectedItems?.Clear();
        this.thisSelectedImportantSignalDisplayNames.Clear();
        this.UpdateImportantSignalsHeaderText();
        this.UpdateImportantSignalsClearButtonState(this.ImportantSignalsPanel.IsVisible);

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
    private void OnGlobalHoverHighlightsTracesRowClicked(object? sender, PointerPressedEventArgs e)
    {
        if (!this.CheckGlobalHoverHighlightsTraces.IsEnabled)
        {
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.CheckGlobalHoverHighlightsTraces.IsChecked = !this.CheckGlobalHoverHighlightsTraces.IsChecked;
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
    // ###########################################################################################
    private void RestoreBoardSettings(string boardKey)
    {
        this.thisSuppressBoardSettingsChanged = true;
        this.thisSuppressGlobalSettingsChanged = true;

        bool hasBoard = !string.IsNullOrWhiteSpace(boardKey);

        this.CheckBoardMarkPin1OnSelectedComponent.IsChecked = hasBoard
            ? UserSettings.GetSchematicsMarkPin1OnSelectedComponentForBoard(boardKey)
            : false;

        this.CheckBoardShowTracesOnSelectedComponent.IsChecked = UserSettings.SchematicsShowTracesOnSelectedComponent;
        this.CheckGlobalShowTracesOnComponentSelect.IsChecked = UserSettings.SchematicsShowTracesOnComponentSelect;
        this.CheckGlobalShowOppositeSideTraces.IsChecked = UserSettings.SchematicsShowOppositeSideTraces;
        this.CheckGlobalShowZones.IsChecked = UserSettings.SchematicsShowZones;

        this.CheckBoardContributorMode.IsEnabled = hasBoard;
        this.CheckBoardContributorMode.IsChecked = UserSettings.ContributorMode;

        bool isInteractiveCadTraceHoverEnabled =
            !string.Equals(UserSettings.InteractiveCadTraceHoverMode, "Disabled", StringComparison.Ordinal);

        bool isInteractiveCadTraceHoverHoldShiftMode =
            string.Equals(UserSettings.InteractiveCadTraceHoverMode, "HoldShift", StringComparison.Ordinal);

        this.CheckGlobalHoverHighlightsTraces.IsChecked = isInteractiveCadTraceHoverEnabled;

        bool shouldRestoreHoldShiftCheckState =
            isInteractiveCadTraceHoverEnabled ||
            this.SchematicsInteractiveCadTraceHoverHoldShiftCheckBox.IsChecked is null;

        if (shouldRestoreHoldShiftCheckState)
        {
            this.SchematicsInteractiveCadTraceHoverHoldShiftCheckBox.IsChecked = isInteractiveCadTraceHoverHoldShiftMode;
        }

        this.UpdateSchematicsInteractiveCadTraceHoverHoldShiftVisualState(isInteractiveCadTraceHoverEnabled);

        this.thisSuppressGlobalSettingsChanged = false;
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

        return UserSettings.InteractiveCadTraceHoverMode switch
        {
            "Disabled" => false,
            "HoldShift" => this.thisIsInteractiveCadTraceHoverShiftPressed,
            _ => true
        };
    }

    // ###########################################################################################
    // Returns true when contributor-only schematic actions are enabled globally.
    // ###########################################################################################
    private bool IsBoardContributorModeEnabled()
    {
        return UserSettings.ContributorMode;
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
            this.UpdateGlobalSettingsControls();
            this.UpdateInteractiveCadTraceHoverModeUi();
            this.RefreshKiCadHoverPadUi();
            this.RefreshKiCadOverlay();
        });
    }

    // ###########################################################################################
    // Updates schematics settings visibility for global and board-specific CAD trace options.
    // Also exposes the temporary calibration-only traces-and-pads toggle while KiCad calibration
    // mode is active so alignment can be checked against the underlying image.
    // ###########################################################################################
    private void UpdateInteractiveCadTraceHoverModeUi()
    {
        bool hasBoard = !string.IsNullOrWhiteSpace(this.MainWindow?.GetCurrentBoardKey());
        bool hasKiCadTraces = this.HasCurrentSchematicKiCadTraces();
        bool hasKiCadPcbPadData = this.HasCurrentSchematicKiCadPcbPadData();
        bool isHoverHighlightEnabled = this.CheckGlobalHoverHighlightsTraces.IsChecked == true;
        bool isHoldShiftEnabled = hasKiCadTraces && isHoverHighlightEnabled;

        var currentView = this.ResolveKiCadViewForCurrentSchematic();
        bool isCurrentViewPcb =
            currentView != null &&
            (string.Equals(currentView.Type, "pcb_top", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(currentView.Type, "pcb_bottom", StringComparison.OrdinalIgnoreCase));

        bool isCalibrationTraceToggleVisible =
            hasKiCadTraces &&
            this.thisIsKiCadTraceCalibrationMode;

        this.BoardMarkPin1OnSelectedComponentRow.IsVisible = hasBoard && hasKiCadPcbPadData;
        this.CheckBoardMarkPin1OnSelectedComponent.IsEnabled = hasBoard && hasKiCadPcbPadData;

        this.GlobalHoverHighlightsTracesRow.IsVisible = hasKiCadTraces;
        this.CheckGlobalHoverHighlightsTraces.IsEnabled = hasKiCadTraces;

        this.GlobalShowTracesOnComponentSelectRow.IsVisible = hasKiCadTraces;
        this.GlobalShowTracesOnComponentSelectRow.IsEnabled = hasKiCadTraces;
        this.GlobalShowTracesOnComponentSelectRow.Opacity = hasKiCadTraces ? 1.0 : 0.55;
        this.GlobalShowTracesOnComponentSelectRow.Cursor = hasKiCadTraces
            ? new Cursor(StandardCursorType.Hand)
            : Cursor.Default;
        this.CheckGlobalShowTracesOnComponentSelect.IsEnabled = hasKiCadTraces;

        this.GlobalShowOppositeSideTracesRow.IsVisible = isCurrentViewPcb;
        this.GlobalShowOppositeSideTracesRow.IsEnabled = isCurrentViewPcb;
        this.GlobalShowOppositeSideTracesRow.Opacity = isCurrentViewPcb ? 1.0 : 0.55;
        this.GlobalShowOppositeSideTracesRow.Cursor = isCurrentViewPcb
            ? new Cursor(StandardCursorType.Hand)
            : Cursor.Default;
        this.CheckGlobalShowOppositeSideTraces.IsEnabled = isCurrentViewPcb;

        this.GlobalShowCalibrationTracesAndPadsRow.IsVisible = isCalibrationTraceToggleVisible;
        this.GlobalShowCalibrationTracesAndPadsRow.IsEnabled = isCalibrationTraceToggleVisible;
        this.GlobalShowCalibrationTracesAndPadsRow.Opacity = isCalibrationTraceToggleVisible ? 1.0 : 0.55;
        this.GlobalShowCalibrationTracesAndPadsRow.Cursor = isCalibrationTraceToggleVisible
            ? new Cursor(StandardCursorType.Hand)
            : Cursor.Default;
        this.CheckGlobalShowCalibrationTracesAndPads.IsEnabled = isCalibrationTraceToggleVisible;

        this.GlobalShowZonesRow.IsVisible = isCurrentViewPcb;
        this.GlobalShowZonesRow.IsEnabled = isCurrentViewPcb;
        this.GlobalShowZonesRow.Opacity = isCurrentViewPcb ? 1.0 : 0.55;
        this.GlobalShowZonesRow.Cursor = isCurrentViewPcb
            ? new Cursor(StandardCursorType.Hand)
            : Cursor.Default;
        this.CheckGlobalShowZones.IsEnabled = isCurrentViewPcb;

        this.SchematicsInteractiveCadTraceHoverHoldShiftRow.IsVisible = hasKiCadTraces;

        this.BoardShowTracesOnSelectedComponentRow.IsVisible = hasKiCadTraces;
        this.CheckBoardShowTracesOnSelectedComponent.IsEnabled = hasKiCadTraces;

        this.UpdateSchematicsInteractiveCadTraceHoverHoldShiftVisualState(isHoldShiftEnabled);
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
    // on every pointer move. While label-editor search is active, only matching labels are shown.
    // ###########################################################################################
    private void UpdateEditorComponentLabels(
        IReadOnlyList<EditableComponentHighlight> rows,
        Rect contentRect,
        double imgWidth,
        double imgHeight,
        double inverseScale)
    {
        var visibleRows = string.IsNullOrWhiteSpace(this.thisLabelEditorSearchText)
            ? rows.ToList()
            : rows.Where(this.DoesLabelEditorHighlightMatchSearch).ToList();

        if (this.thisEditorLabelContainers.Count == 0 && this.SchematicsLabelsCanvas.Children.Count > 0)
        {
            this.SchematicsLabelsCanvas.Children.Clear();
        }

        this.EnsureEditorComponentLabelVisualPoolSize(visibleRows.Count);

        string newSignature = this.BuildEditorComponentLabelVisualSignature(visibleRows);
        bool textChanged = !string.Equals(
            this.thisLastEditorLabelVisualSignature,
            newSignature,
            StringComparison.Ordinal);

        if (textChanged)
        {
            for (int i = 0; i < visibleRows.Count; i++)
            {
                this.thisEditorLabelTextBlocks[i].Text = visibleRows[i].BoardLabel;
            }

            this.thisLastEditorLabelVisualSignature = newSignature;
        }

        for (int i = 0; i < visibleRows.Count; i++)
        {
            var row = visibleRows[i];
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

        for (int i = visibleRows.Count; i < this.thisEditorLabelContainers.Count; i++)
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
        double cornerHandleSize = Math.Clamp(9.0 / scale, 4.0, 12.0);
        double sideHandleThickness = Math.Clamp(6.0 / scale, 2.5, 7.0);
        double cornerHalf = cornerHandleSize / 2.0;
        double sideHalf = sideHandleThickness / 2.0;
        double minimumGap = Math.Clamp(2.0 / scale, 1.0, 3.0);

        var hitRects = new List<(Rect HitRect, LabelEditorDragMode DragMode)>(8)
        {
            (new Rect(localRect.Left - cornerHalf, localRect.Top - cornerHalf, cornerHandleSize, cornerHandleSize), LabelEditorDragMode.ResizeTopLeft),
            (new Rect(localRect.Right - cornerHalf, localRect.Top - cornerHalf, cornerHandleSize, cornerHandleSize), LabelEditorDragMode.ResizeTopRight),
            (new Rect(localRect.Right - cornerHalf, localRect.Bottom - cornerHalf, cornerHandleSize, cornerHandleSize), LabelEditorDragMode.ResizeBottomRight),
            (new Rect(localRect.Left - cornerHalf, localRect.Bottom - cornerHalf, cornerHandleSize, cornerHandleSize), LabelEditorDragMode.ResizeBottomLeft)
        };

        double horizontalSideHitLength = Math.Max(0.0, localRect.Width - (cornerHandleSize * 2.0) - minimumGap);
        if (horizontalSideHitLength > 0.0)
        {
            double horizontalSideLeft = localRect.Center.X - (horizontalSideHitLength / 2.0);

            hitRects.Add((new Rect(horizontalSideLeft, localRect.Top - sideHalf, horizontalSideHitLength, sideHandleThickness), LabelEditorDragMode.ResizeTop));
            hitRects.Add((new Rect(horizontalSideLeft, localRect.Bottom - sideHalf, horizontalSideHitLength, sideHandleThickness), LabelEditorDragMode.ResizeBottom));
        }

        double verticalSideHitLength = Math.Max(0.0, localRect.Height - (cornerHandleSize * 2.0) - minimumGap);
        if (verticalSideHitLength > 0.0)
        {
            double verticalSideTop = localRect.Center.Y - (verticalSideHitLength / 2.0);

            hitRects.Add((new Rect(localRect.Right - sideHalf, verticalSideTop, sideHandleThickness, verticalSideHitLength), LabelEditorDragMode.ResizeRight));
            hitRects.Add((new Rect(localRect.Left - sideHalf, verticalSideTop, sideHandleThickness, verticalSideHitLength), LabelEditorDragMode.ResizeLeft));
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
    // Builds a stable signature for thumbnail highlight state so thumbnails only rebuild when the
    // actual rendered state changes. Component blink phase is included because thumbnail highlight
    // overlays are baked into the rendered bitmap.
    // ###########################################################################################
    private string BuildThumbnailHighlightSignature(bool hasComponentSelection, bool hasKiCadSelection)
    {
        string componentPart = hasComponentSelection
            ? string.Join(
                "\u001E",
                this.highlightIndexBySchematic.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            : string.Empty;

        string componentBlinkPart = hasComponentSelection
            ? this.thisCurrentHighlightBlinkFactor.ToString("0.###", CultureInfo.InvariantCulture)
            : string.Empty;

        var explicitKiCadNets = this.BuildKiCadThumbnailDimmingNetNames();

        string explicitKiCadNetPart = hasKiCadSelection
            ? string.Join(
                "\u001E",
                explicitKiCadNets.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            : string.Empty;

        string importantSignalPart = this.thisSelectedImportantSignalDisplayNames.Count > 0
            ? string.Join(
                "\u001E",
                this.thisSelectedImportantSignalDisplayNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            : string.Empty;

        string lockedNetPart = hasKiCadSelection
            ? string.Join(
                "\u001E",
                this.thisLockedKiCadNetNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            : string.Empty;

        string labelEditorModePart = this.thisIsLabelEditorMode ? "LabelEditor" : string.Empty;
        string labelEditorSchematicPart = this.thisIsLabelEditorMode
            ? this.GetCurrentSchematicName()
            : string.Empty;

        return string.Join(
            "\u001F",
            componentPart,
            componentBlinkPart,
            explicitKiCadNetPart,
            importantSignalPart,
            lockedNetPart,
            labelEditorModePart,
            labelEditorSchematicPart);
    }

    // ###########################################################################################
    // Builds a stable cache key for one PCB net graph on one board side.
    // ###########################################################################################
    private static string BuildKiCadPcbNetRenderCacheKey(int pcbIndex, string netId, string requiredLayer)
    {
        return string.Join(
            "\u001F",
            pcbIndex.ToString(CultureInfo.InvariantCulture),
            netId?.Trim() ?? string.Empty,
            requiredLayer.Trim());
    }

    // ###########################################################################################
    // Returns the cached PCB net graph for the requested net/layer.
    // The cache is stored both in the current working dictionaries and in the active persistent
    // per-board runtime cache scope so revisiting the same board can reuse the heavy build result.
    // ###########################################################################################
    private KiCadPcbNetRenderCache? GetOrCreateKiCadPcbNetRenderCache(
        KiCadPcb pcb,
        int pcbIndex,
        string netId,
        KiCadPcbHighlightBucket bucket,
        string requiredLayer)
    {
        string cacheKey = TabSchematics.BuildKiCadPcbNetRenderCacheKey(pcbIndex, netId, requiredLayer);
        KiCadProjectBundle? expectedProject = this.thisKiCadProject;
        string expectedScopeKey = this.thisCurrentKiCadRuntimeCacheScopeKey;
        var activeScope = this.GetOrCreateCurrentKiCadRuntimeCacheScope();

        lock (this.thisKiCadPcbNetRenderCacheSync)
        {
            if (this.thisKiCadPcbNetRenderCacheByKey.TryGetValue(cacheKey, out var cache))
            {
                return cache;
            }

            if (activeScope != null &&
                activeScope.NetRenderCacheByKey.TryGetValue(cacheKey, out var scopedCache))
            {
                this.thisKiCadPcbNetRenderCacheByKey[cacheKey] = scopedCache;
                return scopedCache;
            }

            if (this.thisKiCadPcbNetRenderBuildTaskByKey.ContainsKey(cacheKey) ||
                (activeScope != null && activeScope.NetRenderBuildTaskByKey.ContainsKey(cacheKey)))
            {
                return null;
            }

            Task buildTask = Task.Run(() =>
            {
                try
                {
                    var builtCache = this.BuildKiCadPcbNetRenderCache(pcb, bucket, requiredLayer);

                    lock (this.thisKiCadPcbNetRenderCacheSync)
                    {
                        if (!ReferenceEquals(expectedProject, this.thisKiCadProject) ||
                            !string.Equals(expectedScopeKey, this.thisCurrentKiCadRuntimeCacheScopeKey, StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }

                        this.thisKiCadPcbNetRenderCacheByKey[cacheKey] = builtCache;

                        if (activeScope != null)
                        {
                            activeScope.NetRenderCacheByKey[cacheKey] = builtCache;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to build KiCad PCB net render cache [{cacheKey}] - [{ex.Message}]");
                }
                finally
                {
                    lock (this.thisKiCadPcbNetRenderCacheSync)
                    {
                        this.thisKiCadPcbNetRenderBuildTaskByKey.Remove(cacheKey);

                        if (activeScope != null)
                        {
                            activeScope.NetRenderBuildTaskByKey.Remove(cacheKey);
                        }
                    }

                    if (ReferenceEquals(expectedProject, this.thisKiCadProject) &&
                        string.Equals(expectedScopeKey, this.thisCurrentKiCadRuntimeCacheScopeKey, StringComparison.OrdinalIgnoreCase))
                    {
                        Dispatcher.UIThread.Post(
                            () => this.RefreshKiCadOverlay(),
                            DispatcherPriority.Background);
                    }
                }
            });

            this.thisKiCadPcbNetRenderBuildTaskByKey[cacheKey] = buildTask;

            if (activeScope != null)
            {
                activeScope.NetRenderBuildTaskByKey[cacheKey] = buildTask;
            }

            return null;
        }
    }

    // ###########################################################################################
    // Builds one cached PCB net graph containing pads, segments, vias, arcs, zones, and adjacency.
    // Zones participate in connectivity so selected traces can continue into copper pours.
    // Uses a broad-phase zone spatial index so exact zone-touch tests only run for nearby geometry.
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

        foreach (int zoneIndex in bucket.Zones)
        {
            if (zoneIndex < 0 || zoneIndex >= pcb.Routing.Zones.Count)
            {
                continue;
            }

            var zone = pcb.Routing.Zones[zoneIndex];
            if (!TabSchematics.IsKiCadPcbZoneVisibleOnSide(zone, requiredLayer))
            {
                continue;
            }

            var polygonsWorld = TabSchematics.GetKiCadZoneWorldPolygons(zone);
            if (polygonsWorld.Count == 0)
            {
                continue;
            }

            var boundsWorld = TabSchematics.GetKiCadZoneWorldBounds(polygonsWorld);
            if (boundsWorld.Width <= 0 || boundsWorld.Height <= 0)
            {
                continue;
            }

            var info = new KiCadGraphNode
            {
                Id = $"Z{idCounter++}"
            };

            cache.NodesById[info.Id] = info;
            cache.AllNodeIds.Add(info.Id);

            cache.ZoneNodes.Add(new KiCadPcbZoneRenderNode
            {
                Info = info,
                Zone = zone,
                PolygonsWorld = polygonsWorld,
                BoundsWorld = boundsWorld
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

        static Rect BuildCircleBounds(Point centerWorld, double radiusWorld)
        {
            double safeRadius = Math.Max(0.05, radiusWorld);

            return new Rect(
                centerWorld.X - safeRadius,
                centerWorld.Y - safeRadius,
                safeRadius * 2.0,
                safeRadius * 2.0);
        }

        static Rect BuildSegmentBounds(Point startWorld, Point endWorld, double radiusWorld)
        {
            double safeRadius = Math.Max(0.05, radiusWorld);
            double minX = Math.Min(startWorld.X, endWorld.X) - safeRadius;
            double minY = Math.Min(startWorld.Y, endWorld.Y) - safeRadius;
            double maxX = Math.Max(startWorld.X, endWorld.X) + safeRadius;
            double maxY = Math.Max(startWorld.Y, endWorld.Y) + safeRadius;

            return new Rect(
                minX,
                minY,
                Math.Max(0.0001, maxX - minX),
                Math.Max(0.0001, maxY - minY));
        }

        static Rect BuildArcBounds(KiCadPcbArcRenderNode arcNode, double radiusWorld)
        {
            double safeRadius = Math.Max(0.05, radiusWorld);
            double minX = Math.Min(arcNode.StartWorld.X, Math.Min(arcNode.MidWorld.X, arcNode.EndWorld.X)) - safeRadius;
            double minY = Math.Min(arcNode.StartWorld.Y, Math.Min(arcNode.MidWorld.Y, arcNode.EndWorld.Y)) - safeRadius;
            double maxX = Math.Max(arcNode.StartWorld.X, Math.Max(arcNode.MidWorld.X, arcNode.EndWorld.X)) + safeRadius;
            double maxY = Math.Max(arcNode.StartWorld.Y, Math.Max(arcNode.MidWorld.Y, arcNode.EndWorld.Y)) + safeRadius;

            return new Rect(
                minX,
                minY,
                Math.Max(0.0001, maxX - minX),
                Math.Max(0.0001, maxY - minY));
        }

        static bool RectsIntersect(Rect left, Rect right)
        {
            return left.Left <= right.Right &&
                   left.Right >= right.Left &&
                   left.Top <= right.Bottom &&
                   left.Bottom >= right.Top;
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

        const double zoneGridCellSizeWorld = 8.0;
        var zoneIndicesByCell = new Dictionary<long, List<int>>();
        var zoneBoundsByIndex = new List<Rect>(cache.ZoneNodes.Count);

        for (int i = 0; i < cache.ZoneNodes.Count; i++)
        {
            Rect boundsWorld = cache.ZoneNodes[i].BoundsWorld;
            zoneBoundsByIndex.Add(boundsWorld);

            int minCellX = TabSchematics.GetKiCadHoverCellCoord(boundsWorld.Left, zoneGridCellSizeWorld);
            int maxCellX = TabSchematics.GetKiCadHoverCellCoord(boundsWorld.Right, zoneGridCellSizeWorld);
            int minCellY = TabSchematics.GetKiCadHoverCellCoord(boundsWorld.Top, zoneGridCellSizeWorld);
            int maxCellY = TabSchematics.GetKiCadHoverCellCoord(boundsWorld.Bottom, zoneGridCellSizeWorld);

            TabSchematics.AddKiCadHoverIndexToCellRange(
                zoneIndicesByCell,
                minCellX,
                maxCellX,
                minCellY,
                maxCellY,
                i);
        }

        List<int> GetCandidateZoneIndices(Rect candidateBounds)
        {
            if (zoneBoundsByIndex.Count == 0)
            {
                return new List<int>();
            }

            int minCellX = TabSchematics.GetKiCadHoverCellCoord(candidateBounds.Left, zoneGridCellSizeWorld);
            int maxCellX = TabSchematics.GetKiCadHoverCellCoord(candidateBounds.Right, zoneGridCellSizeWorld);
            int minCellY = TabSchematics.GetKiCadHoverCellCoord(candidateBounds.Top, zoneGridCellSizeWorld);
            int maxCellY = TabSchematics.GetKiCadHoverCellCoord(candidateBounds.Bottom, zoneGridCellSizeWorld);

            var result = new List<int>();
            var seen = new HashSet<int>();

            for (int cellY = minCellY; cellY <= maxCellY; cellY++)
            {
                for (int cellX = minCellX; cellX <= maxCellX; cellX++)
                {
                    long cellKey = TabSchematics.BuildKiCadHoverCellKey(cellX, cellY);

                    if (!zoneIndicesByCell.TryGetValue(cellKey, out var zoneIndices))
                    {
                        continue;
                    }

                    foreach (int zoneIndex in zoneIndices)
                    {
                        if (!seen.Add(zoneIndex))
                        {
                            continue;
                        }

                        if (!RectsIntersect(candidateBounds, zoneBoundsByIndex[zoneIndex]))
                        {
                            continue;
                        }

                        result.Add(zoneIndex);
                    }
                }
            }

            return result;
        }

        foreach (var padNode in cache.PadNodes)
        {
            var candidateZoneIndices = GetCandidateZoneIndices(
                BuildCircleBounds(
                    padNode.CenterWorld,
                    padNode.RadiusWorld + 0.05));

            foreach (int zoneIndex in candidateZoneIndices)
            {
                var zoneNode = cache.ZoneNodes[zoneIndex];

                if (TabSchematics.DoesCircleTouchZone(
                        padNode.CenterWorld,
                        padNode.RadiusWorld + 0.05,
                        zoneNode.PolygonsWorld))
                {
                    AddEdge(zoneNode.Info.Id, padNode.Info.Id);
                }
            }
        }

        foreach (var viaNode in cache.ViaNodes)
        {
            var candidateZoneIndices = GetCandidateZoneIndices(
                BuildCircleBounds(
                    viaNode.CenterWorld,
                    (viaNode.DiameterWorld / 2.0) + 0.05));

            foreach (int zoneIndex in candidateZoneIndices)
            {
                var zoneNode = cache.ZoneNodes[zoneIndex];

                if (TabSchematics.DoesCircleTouchZone(
                        viaNode.CenterWorld,
                        (viaNode.DiameterWorld / 2.0) + 0.05,
                        zoneNode.PolygonsWorld))
                {
                    AddEdge(zoneNode.Info.Id, viaNode.Info.Id);
                }
            }
        }

        foreach (var segmentNode in cache.SegmentNodes)
        {
            var candidateZoneIndices = GetCandidateZoneIndices(
                BuildSegmentBounds(
                    segmentNode.StartWorld,
                    segmentNode.EndWorld,
                    (segmentNode.WidthWorld / 2.0) + 0.05));

            foreach (int zoneIndex in candidateZoneIndices)
            {
                var zoneNode = cache.ZoneNodes[zoneIndex];

                if (TabSchematics.DoesSegmentTouchZone(
                        segmentNode.StartWorld,
                        segmentNode.EndWorld,
                        (segmentNode.WidthWorld / 2.0) + 0.05,
                        zoneNode.PolygonsWorld))
                {
                    AddEdge(zoneNode.Info.Id, segmentNode.Info.Id);
                }
            }
        }

        foreach (var arcNode in cache.ArcNodes)
        {
            var candidateZoneIndices = GetCandidateZoneIndices(
                BuildArcBounds(
                    arcNode,
                    (arcNode.WidthWorld / 2.0) + 0.05));

            foreach (int zoneIndex in candidateZoneIndices)
            {
                var zoneNode = cache.ZoneNodes[zoneIndex];

                if (this.DoesArcTouchZone(
                        arcNode,
                        (arcNode.WidthWorld / 2.0) + 0.05,
                        zoneNode.PolygonsWorld))
                {
                    AddEdge(zoneNode.Info.Id, arcNode.Info.Id);
                }
            }
        }

        return cache;
    }

    // ###########################################################################################
    // Resolves the currently drawable node ids from a cached PCB net graph.
    // Explicit hover/lock draws the whole net, while selection-derived rendering starts from the
    // selected or hovered component pads and stops traversal at foreign pads.
    // ###########################################################################################
    private HashSet<string> BuildKiCadPcbActiveDrawIds(
        KiCadPcbNetRenderCache cache,
        bool isExplicitHighlight,
        IReadOnlySet<string> activeReferences)
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
            bool isTargetPad = activeReferences.Count == 0 ||
                               activeReferences.Contains(reference);

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
                    activeReferences.Count > 0 &&
                    !activeReferences.Contains(reference);

                if (!isForeignPad)
                {
                    queue.Enqueue(neighborId);
                }
            }
        }

        return activeDrawIds;
    }

    // ###########################################################################################
    // Builds a stable cache key for one PCB-side hover lookup cache.
    // ###########################################################################################
    private static string BuildKiCadPcbHoverHitTestCacheKey(int pcbIndex, string requiredLayer)
    {
        return string.Join(
            "\u001F",
            pcbIndex.ToString(CultureInfo.InvariantCulture),
            requiredLayer.Trim());
    }

    // ###########################################################################################
    // Packs one grid-cell coordinate pair into a stable dictionary key.
    // ###########################################################################################
    private static long BuildKiCadHoverCellKey(int cellX, int cellY)
    {
        return ((long)cellX << 32) ^ (uint)cellY;
    }

    // ###########################################################################################
    // Converts one world coordinate into the hover-grid cell coordinate for spatial lookup.
    // ###########################################################################################
    private static int GetKiCadHoverCellCoord(double worldCoord, double cellSizeWorld)
    {
        return (int)Math.Floor(worldCoord / Math.Max(0.0001, cellSizeWorld));
    }

    // ###########################################################################################
    // Adds one candidate index to every spatial cell touched by its expanded hit area.
    // ###########################################################################################
    private static void AddKiCadHoverIndexToCellRange(
        Dictionary<long, List<int>> cellMap,
        int minCellX,
        int maxCellX,
        int minCellY,
        int maxCellY,
        int candidateIndex)
    {
        for (int cellY = minCellY; cellY <= maxCellY; cellY++)
        {
            for (int cellX = minCellX; cellX <= maxCellX; cellX++)
            {
                long key = TabSchematics.BuildKiCadHoverCellKey(cellX, cellY);

                if (!cellMap.TryGetValue(key, out var indices))
                {
                    indices = new List<int>();
                    cellMap[key] = indices;
                }

                indices.Add(candidateIndex);
            }
        }
    }

    // ###########################################################################################
    // Returns the cached PCB hover-hit-test data for the requested board side.
    // The cache is stored both in the current working dictionaries and in the active persistent
    // per-board runtime cache scope so revisiting the same board can reuse hover preparation work.
    // ###########################################################################################
    private KiCadPcbHoverHitTestCache? GetOrCreateKiCadPcbHoverHitTestCache(
        KiCadPcb pcb,
        int pcbIndex,
        string requiredLayer)
    {
        string cacheKey = TabSchematics.BuildKiCadPcbHoverHitTestCacheKey(pcbIndex, requiredLayer);
        KiCadProjectBundle? expectedProject = this.thisKiCadProject;
        string expectedScopeKey = this.thisCurrentKiCadRuntimeCacheScopeKey;
        var activeScope = this.GetOrCreateCurrentKiCadRuntimeCacheScope();

        lock (this.thisKiCadPcbHoverHitTestCacheSync)
        {
            if (this.thisKiCadPcbHoverHitTestCacheByKey.TryGetValue(cacheKey, out var cache))
            {
                return cache;
            }

            if (activeScope != null &&
                activeScope.HoverHitTestCacheByKey.TryGetValue(cacheKey, out var scopedCache))
            {
                this.thisKiCadPcbHoverHitTestCacheByKey[cacheKey] = scopedCache;
                return scopedCache;
            }

            if (this.thisKiCadPcbHoverHitTestBuildTaskByKey.ContainsKey(cacheKey) ||
                (activeScope != null && activeScope.HoverHitTestBuildTaskByKey.ContainsKey(cacheKey)))
            {
                return null;
            }

            Task buildTask = Task.Run(() =>
            {
                try
                {
                    var builtCache = this.BuildKiCadPcbHoverHitTestCache(pcb, requiredLayer);

                    lock (this.thisKiCadPcbHoverHitTestCacheSync)
                    {
                        if (!ReferenceEquals(expectedProject, this.thisKiCadProject) ||
                            !string.Equals(expectedScopeKey, this.thisCurrentKiCadRuntimeCacheScopeKey, StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }

                        this.thisKiCadPcbHoverHitTestCacheByKey[cacheKey] = builtCache;

                        if (activeScope != null)
                        {
                            activeScope.HoverHitTestCacheByKey[cacheKey] = builtCache;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Failed to build KiCad PCB hover cache [{cacheKey}] - [{ex.Message}]");
                }
                finally
                {
                    lock (this.thisKiCadPcbHoverHitTestCacheSync)
                    {
                        this.thisKiCadPcbHoverHitTestBuildTaskByKey.Remove(cacheKey);

                        if (activeScope != null)
                        {
                            activeScope.HoverHitTestBuildTaskByKey.Remove(cacheKey);
                        }
                    }

                    if (ReferenceEquals(expectedProject, this.thisKiCadProject) &&
                        string.Equals(expectedScopeKey, this.thisCurrentKiCadRuntimeCacheScopeKey, StringComparison.OrdinalIgnoreCase))
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            if (double.IsNaN(this.thisLastKiCadHoverHitTestContainerPoint.X) ||
                                double.IsNaN(this.thisLastKiCadHoverHitTestContainerPoint.Y))
                            {
                                return;
                            }

                            this.HitTestKiCadOverlayForHover(this.thisLastKiCadHoverHitTestContainerPoint);
                            this.RefreshKiCadHoverPadUi();
                        }, DispatcherPriority.Background);
                    }
                }
            });

            this.thisKiCadPcbHoverHitTestBuildTaskByKey[cacheKey] = buildTask;

            if (activeScope != null)
            {
                activeScope.HoverHitTestBuildTaskByKey[cacheKey] = buildTask;
            }

            return null;
        }
    }

    // ###########################################################################################
    // Builds one spatial hover cache for a PCB side so pointer hover no longer scans every pad,
    // segment, via, and zone in the board on every move event.
    // ###########################################################################################
    private KiCadPcbHoverHitTestCache BuildKiCadPcbHoverHitTestCache(KiCadPcb pcb, string requiredLayer)
    {
        var cache = new KiCadPcbHoverHitTestCache
        {
            CellSizeWorld = 2.0,
            MaxHitRadiusWorld = 0.8
        };

        foreach (var footprint in pcb.Footprints)
        {
            foreach (var pad in footprint.Pads)
            {
                if (pad.Net == null ||
                    string.IsNullOrWhiteSpace(pad.Net.NormalizedName) ||
                    pad.AbsoluteCenter == null ||
                    !TabSchematics.IsKiCadPcbPointVisibleOnSide(pad.Layers, requiredLayer))
                {
                    continue;
                }

                double hitRadiusWorld = Math.Max(pad.Size?.X ?? 0.5, pad.Size?.Y ?? 0.5) / 2.0 + 0.3;
                cache.MaxHitRadiusWorld = Math.Max(cache.MaxHitRadiusWorld, hitRadiusWorld);

                int candidateIndex = cache.PadCandidates.Count;

                cache.PadCandidates.Add(new KiCadPcbHoverPadCandidate
                {
                    Net = pad.Net,
                    PadNumber = pad.Number?.Trim() ?? string.Empty,
                    CenterWorld = new Point(pad.AbsoluteCenter.X, pad.AbsoluteCenter.Y),
                    HitRadiusWorld = hitRadiusWorld
                });

                int minCellX = TabSchematics.GetKiCadHoverCellCoord(pad.AbsoluteCenter.X - hitRadiusWorld, cache.CellSizeWorld);
                int maxCellX = TabSchematics.GetKiCadHoverCellCoord(pad.AbsoluteCenter.X + hitRadiusWorld, cache.CellSizeWorld);
                int minCellY = TabSchematics.GetKiCadHoverCellCoord(pad.AbsoluteCenter.Y - hitRadiusWorld, cache.CellSizeWorld);
                int maxCellY = TabSchematics.GetKiCadHoverCellCoord(pad.AbsoluteCenter.Y + hitRadiusWorld, cache.CellSizeWorld);

                TabSchematics.AddKiCadHoverIndexToCellRange(
                    cache.PadIndicesByCell,
                    minCellX,
                    maxCellX,
                    minCellY,
                    maxCellY,
                    candidateIndex);
            }
        }

        foreach (var segment in pcb.Routing.Segments)
        {
            if (segment.Net == null ||
                string.IsNullOrWhiteSpace(segment.Net.NormalizedName) ||
                segment.Start == null ||
                segment.End == null ||
                !string.Equals(segment.Layer?.Trim(), requiredLayer, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            double hitRadiusWorld = (segment.Width ?? 0.25) / 2.0 + 0.3;
            cache.MaxHitRadiusWorld = Math.Max(cache.MaxHitRadiusWorld, hitRadiusWorld);

            int candidateIndex = cache.SegmentCandidates.Count;

            cache.SegmentCandidates.Add(new KiCadPcbHoverSegmentCandidate
            {
                Net = segment.Net,
                StartWorld = new Point(segment.Start.X, segment.Start.Y),
                EndWorld = new Point(segment.End.X, segment.End.Y),
                HitRadiusWorld = hitRadiusWorld
            });

            double minX = Math.Min(segment.Start.X, segment.End.X) - hitRadiusWorld;
            double maxX = Math.Max(segment.Start.X, segment.End.X) + hitRadiusWorld;
            double minY = Math.Min(segment.Start.Y, segment.End.Y) - hitRadiusWorld;
            double maxY = Math.Max(segment.Start.Y, segment.End.Y) + hitRadiusWorld;

            TabSchematics.AddKiCadHoverIndexToCellRange(
                cache.SegmentIndicesByCell,
                TabSchematics.GetKiCadHoverCellCoord(minX, cache.CellSizeWorld),
                TabSchematics.GetKiCadHoverCellCoord(maxX, cache.CellSizeWorld),
                TabSchematics.GetKiCadHoverCellCoord(minY, cache.CellSizeWorld),
                TabSchematics.GetKiCadHoverCellCoord(maxY, cache.CellSizeWorld),
                candidateIndex);
        }

        foreach (var via in pcb.Routing.Vias)
        {
            if (via.Net == null ||
                string.IsNullOrWhiteSpace(via.Net.NormalizedName) ||
                via.At == null ||
                !TabSchematics.IsKiCadPcbPointVisibleOnSide(via.Layers, requiredLayer))
            {
                continue;
            }

            double hitRadiusWorld = (via.Size ?? 0.4) / 2.0 + 0.3;
            cache.MaxHitRadiusWorld = Math.Max(cache.MaxHitRadiusWorld, hitRadiusWorld);

            int candidateIndex = cache.ViaCandidates.Count;

            cache.ViaCandidates.Add(new KiCadPcbHoverViaCandidate
            {
                Net = via.Net,
                CenterWorld = new Point(via.At.X, via.At.Y),
                HitRadiusWorld = hitRadiusWorld
            });

            int minCellX = TabSchematics.GetKiCadHoverCellCoord(via.At.X - hitRadiusWorld, cache.CellSizeWorld);
            int maxCellX = TabSchematics.GetKiCadHoverCellCoord(via.At.X + hitRadiusWorld, cache.CellSizeWorld);
            int minCellY = TabSchematics.GetKiCadHoverCellCoord(via.At.Y - hitRadiusWorld, cache.CellSizeWorld);
            int maxCellY = TabSchematics.GetKiCadHoverCellCoord(via.At.Y + hitRadiusWorld, cache.CellSizeWorld);

            TabSchematics.AddKiCadHoverIndexToCellRange(
                cache.ViaIndicesByCell,
                minCellX,
                maxCellX,
                minCellY,
                maxCellY,
                candidateIndex);
        }

        const double zoneHoverToleranceWorld = 0.4;

        foreach (var zone in pcb.Routing.Zones)
        {
            if (zone.Net == null ||
                string.IsNullOrWhiteSpace(zone.Net.NormalizedName) ||
                !TabSchematics.IsKiCadPcbZoneVisibleOnSide(zone, requiredLayer))
            {
                continue;
            }

            var polygonsWorld = TabSchematics.GetKiCadZoneWorldPolygons(zone);
            if (polygonsWorld.Count == 0)
            {
                continue;
            }

            Rect boundsWorld = TabSchematics.GetKiCadZoneWorldBounds(polygonsWorld);
            if (boundsWorld.Width <= 0 || boundsWorld.Height <= 0)
            {
                continue;
            }

            int candidateIndex = cache.ZoneCandidates.Count;

            cache.ZoneCandidates.Add(new KiCadPcbHoverZoneCandidate
            {
                Net = zone.Net,
                PolygonsWorld = polygonsWorld,
                BoundsWorld = boundsWorld
            });

            cache.MaxHitRadiusWorld = Math.Max(cache.MaxHitRadiusWorld, zoneHoverToleranceWorld);

            double minX = boundsWorld.Left - zoneHoverToleranceWorld;
            double maxX = boundsWorld.Right + zoneHoverToleranceWorld;
            double minY = boundsWorld.Top - zoneHoverToleranceWorld;
            double maxY = boundsWorld.Bottom + zoneHoverToleranceWorld;

            TabSchematics.AddKiCadHoverIndexToCellRange(
                cache.ZoneIndicesByCell,
                TabSchematics.GetKiCadHoverCellCoord(minX, cache.CellSizeWorld),
                TabSchematics.GetKiCadHoverCellCoord(maxX, cache.CellSizeWorld),
                TabSchematics.GetKiCadHoverCellCoord(minY, cache.CellSizeWorld),
                TabSchematics.GetKiCadHoverCellCoord(maxY, cache.CellSizeWorld),
                candidateIndex);
        }

        return cache;
    }

    // ###########################################################################################
    // Builds a stable rounded key for one PCB world-space point so connected trace endpoints can
    // be grouped into continuous rendered chains without relying on exact floating-point equality.
    // ###########################################################################################
    private static string BuildKiCadWorldPointKey(Point point)
    {
        return string.Join(
            "|",
            Math.Round(point.X, 6).ToString(CultureInfo.InvariantCulture),
            Math.Round(point.Y, 6).ToString(CultureInfo.InvariantCulture));
    }

    // ###########################################################################################
    // Groups connected PCB segments into continuous point chains so the overlay can render one
    // smoothed polyline per trace run instead of many separate line primitives with visible seams.
    // ###########################################################################################
    private List<List<Point>> BuildConnectedKiCadPcbSegmentPointChains(IReadOnlyList<KiCadPcbSegmentRenderNode> segmentNodes)
    {
        var chains = new List<List<Point>>();

        if (segmentNodes.Count == 0)
        {
            return chains;
        }

        var segmentIndicesByPointKey = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        void AddSegmentIndex(string pointKey, int segmentIndex)
        {
            if (!segmentIndicesByPointKey.TryGetValue(pointKey, out var indices))
            {
                indices = new List<int>();
                segmentIndicesByPointKey[pointKey] = indices;
            }

            indices.Add(segmentIndex);
        }

        for (int i = 0; i < segmentNodes.Count; i++)
        {
            var segmentNode = segmentNodes[i];

            AddSegmentIndex(TabSchematics.BuildKiCadWorldPointKey(segmentNode.StartWorld), i);
            AddSegmentIndex(TabSchematics.BuildKiCadWorldPointKey(segmentNode.EndWorld), i);
        }

        var remainingSegmentIndices = new HashSet<int>(Enumerable.Range(0, segmentNodes.Count));

        int GetRemainingDegree(string pointKey)
        {
            if (!segmentIndicesByPointKey.TryGetValue(pointKey, out var indices))
            {
                return 0;
            }

            int degree = 0;

            for (int i = 0; i < indices.Count; i++)
            {
                if (remainingSegmentIndices.Contains(indices[i]))
                {
                    degree++;
                }
            }

            return degree;
        }

        Point GetOtherEndpoint(KiCadPcbSegmentRenderNode segmentNode, string currentPointKey)
        {
            string startKey = TabSchematics.BuildKiCadWorldPointKey(segmentNode.StartWorld);
            return string.Equals(startKey, currentPointKey, StringComparison.Ordinal)
                ? segmentNode.EndWorld
                : segmentNode.StartWorld;
        }

        while (remainingSegmentIndices.Count > 0)
        {
            int seedSegmentIndex = remainingSegmentIndices.First();
            var seedSegment = segmentNodes[seedSegmentIndex];

            string seedStartKey = TabSchematics.BuildKiCadWorldPointKey(seedSegment.StartWorld);
            string seedEndKey = TabSchematics.BuildKiCadWorldPointKey(seedSegment.EndWorld);

            int seedStartDegree = GetRemainingDegree(seedStartKey);
            int seedEndDegree = GetRemainingDegree(seedEndKey);

            Point currentPoint;
            Point nextPoint;

            if (seedStartDegree != 2 && seedEndDegree == 2)
            {
                currentPoint = seedSegment.StartWorld;
                nextPoint = seedSegment.EndWorld;
            }
            else if (seedEndDegree != 2 && seedStartDegree == 2)
            {
                currentPoint = seedSegment.EndWorld;
                nextPoint = seedSegment.StartWorld;
            }
            else
            {
                currentPoint = seedSegment.StartWorld;
                nextPoint = seedSegment.EndWorld;
            }

            var chain = new List<Point> { currentPoint };
            int currentSegmentIndex = seedSegmentIndex;

            while (true)
            {
                remainingSegmentIndices.Remove(currentSegmentIndex);
                chain.Add(nextPoint);

                string nextPointKey = TabSchematics.BuildKiCadWorldPointKey(nextPoint);

                if (!segmentIndicesByPointKey.TryGetValue(nextPointKey, out var connectedIndices))
                {
                    break;
                }

                int nextSegmentIndex = -1;

                for (int i = 0; i < connectedIndices.Count; i++)
                {
                    int candidateIndex = connectedIndices[i];

                    if (remainingSegmentIndices.Contains(candidateIndex))
                    {
                        if (nextSegmentIndex >= 0)
                        {
                            nextSegmentIndex = -1;
                            break;
                        }

                        nextSegmentIndex = candidateIndex;
                    }
                }

                if (nextSegmentIndex < 0)
                {
                    break;
                }

                var nextSegmentNode = segmentNodes[nextSegmentIndex];
                currentSegmentIndex = nextSegmentIndex;
                currentPoint = nextPoint;
                nextPoint = GetOtherEndpoint(nextSegmentNode, nextPointKey);
            }

            if (chain.Count >= 2)
            {
                chains.Add(chain);
            }
        }

        return chains;
    }

    // ###########################################################################################
    // Returns the on-screen KiCad PCB stroke thickness.
    // Keeps most of the mapped width so split trace chains meet cleanly, while trimming only a
    // small amount to avoid the overlay looking too bloated.
    // ###########################################################################################
    private double GetKiCadOverlayStrokeThickness(double mappedThickness)
    {
        if (mappedThickness <= 1.0)
        {
            return 1.0;
        }

        double trim = Math.Min(0.35, mappedThickness * 0.10);
        return Math.Max(1.0, mappedThickness - trim);
    }

    // ###########################################################################################
    // Handle manual row clicks for board-specific selected-component trace preview.
    // ###########################################################################################
    private void OnBoardShowTracesOnSelectedComponentRowClicked(object? sender, PointerPressedEventArgs e)
    {
        if (!this.CheckBoardShowTracesOnSelectedComponent.IsEnabled)
        {
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.CheckBoardShowTracesOnSelectedComponent.IsChecked = !this.CheckBoardShowTracesOnSelectedComponent.IsChecked;
            e.Handled = true;
        }
    }

    // ###########################################################################################
    // Returns true when hovered components should preview the same traces as a selected component.
    // Uses the global checkbox state instead of a board-specific setting.
    // ###########################################################################################
    private bool IsBoardShowTracesOnSelectedComponentEnabled()
    {
        if (!this.HasCurrentSchematicKiCadTraces())
        {
            return false;
        }

        return UserSettings.SchematicsShowTracesOnSelectedComponent;
    }

    // ###########################################################################################
    // Builds the effective KiCad net-name set used for trace preview in the current schematic.
    // Includes selected-component nets only when the global select option is enabled, and includes
    // hovered-component nets when selected-component trace preview is enabled.
    // Also includes manually selected important-signal net groups.
    // ###########################################################################################
    private HashSet<string> BuildActiveKiCadTracePreviewNetNames()
    {
        var activeNets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (UserSettings.SchematicsShowTracesOnComponentSelect)
        {
            foreach (string netName in this.thisSelectedKiCadNormalizedNetNames)
            {
                activeNets.Add(netName);
            }
        }

        if (this.IsBoardShowTracesOnSelectedComponentEnabled() &&
            !string.IsNullOrWhiteSpace(this.thisHoveredComponentBoardLabel))
        {
            foreach (string netName in this.BuildKiCadNormalizedNetNamesForReferences(
                new[] { this.thisHoveredComponentBoardLabel.Trim() }))
            {
                activeNets.Add(netName);
            }
        }

        foreach (string netName in this.BuildSelectedImportantSignalNetNames())
        {
            activeNets.Add(netName);
        }

        foreach (string lockedNet in this.thisLockedKiCadNetNames)
        {
            activeNets.Add(lockedNet);
        }

        string? activeHoveredKiCadNetName = this.GetActiveHoveredKiCadNetName();
        if (!string.IsNullOrWhiteSpace(activeHoveredKiCadNetName))
        {
            activeNets.Add(activeHoveredKiCadNetName);
        }

        return activeNets;
    }
    
    // ###########################################################################################
    // Builds the effective KiCad reference set used for PCB trace preview traversal.
    // Includes the hovered component when selected-component trace preview is enabled.
    // ###########################################################################################
    private HashSet<string> BuildActiveKiCadTracePreviewReferences()
    {
        var activeReferences = new HashSet<string>(this.thisSelectedKiCadReferences, StringComparer.OrdinalIgnoreCase);

        if (this.IsBoardShowTracesOnSelectedComponentEnabled() &&
            !string.IsNullOrWhiteSpace(this.thisHoveredComponentBoardLabel))
        {
            activeReferences.Add(this.thisHoveredComponentBoardLabel.Trim());
        }

        return activeReferences;
    }

    // ###########################################################################################
    // Builds the normalized KiCad net-name set for one optional component reference.
    // Returns an empty set when no valid board label is supplied.
    // ###########################################################################################
    private HashSet<string> BuildKiCadNormalizedNetNamesForSingleReference(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return this.BuildKiCadNormalizedNetNamesForReferences(new[] { reference.Trim() });
    }

    // ###########################################################################################
    // Compares two string collections as case-insensitive sets without caring about order.
    // ###########################################################################################
    private static bool SetEqualsOrdinalIgnoreCase(
        IReadOnlyCollection<string> left,
        IReadOnlyCollection<string> right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        if (left.Count == 0)
        {
            return true;
        }

        var leftSet = new HashSet<string>(left, StringComparer.OrdinalIgnoreCase);
        return leftSet.SetEquals(right);
    }

    // ###########################################################################################
    // Updates the panel displaying connected components and pins for a supplied active net set.
    // This avoids recomputing hover-preview net names multiple times within one overlay refresh.
    // Keeps the clear button visible even when the panel body is collapsed, as long as the panel
    // itself is populated with netlist content.
    // ###########################################################################################
    private void UpdateKiCadNetConnectionsPanel(IReadOnlyCollection<string> activeNets)
    {
        var activeNetLookup = activeNets as HashSet<string> ??
                              new HashSet<string>(activeNets, StringComparer.OrdinalIgnoreCase);

        if (!this.HasCurrentSchematicKiCadTraces() ||
            activeNetLookup.Count == 0 ||
            this.thisKiCadProject?.Root == null)
        {
            this.thisLastKiCadNetConnectionsSignature = string.Empty;
            this.KiCadNetConnectionsHeaderTextBlock.Text = "Netlist names";
            this.KiCadNetConnectionsNetNameText.Text = string.Empty;
            this.KiCadNetConnectionsList.ItemsSource = null;
            this.KiCadNetConnectionsPanel.IsVisible = false;
            this.UpdateKiCadNetConnectionsClearButtonState(false);
            return;
        }

        var sortedNetNames = activeNetLookup
            .OrderBy(netName => netName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        string signature = string.Join("\u001F", sortedNetNames);

        if (string.Equals(this.thisLastKiCadNetConnectionsSignature, signature, StringComparison.Ordinal))
        {
            this.KiCadNetConnectionsHeaderTextBlock.Text = sortedNetNames.Count > 0
                ? $"Netlist names ({sortedNetNames.Count})"
                : "Netlist names";
            this.KiCadNetConnectionsPanel.IsVisible = true;
            this.UpdateKiCadNetConnectionsClearButtonState(true);
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

                    if (!activeNetLookup.Contains(netName))
                    {
                        continue;
                    }

                    string padNum = pad.Number?.Trim() ?? "?";

                    string connStr = activeNetLookup.Count > 1
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
            this.KiCadNetConnectionsHeaderTextBlock.Text = sortedNetNames.Count > 0
                ? $"Netlist names ({sortedNetNames.Count})"
                : "Netlist names";
            this.KiCadNetConnectionsNetNameText.Text = string.Join(Environment.NewLine, sortedNetNames);
            this.KiCadNetConnectionsList.ItemsSource = null;
            this.KiCadNetConnectionsPanel.IsVisible = false;
            this.UpdateKiCadNetConnectionsClearButtonState(false);
            return;
        }

        var sortedConnections = connections
            .OrderBy(connection => connection.OrderIndex)
            .ThenBy(connection => connection.PadIndex)
            .ThenBy(connection => connection.ConnStr, StringComparer.OrdinalIgnoreCase)
            .Select(connection => connection.ConnStr)
            .ToList();

        this.thisLastKiCadNetConnectionsSignature = signature;
        this.KiCadNetConnectionsHeaderTextBlock.Text = sortedNetNames.Count > 0
            ? $"Netlist names ({sortedNetNames.Count})"
            : "Netlist names";
        this.KiCadNetConnectionsNetNameText.Text = string.Join(Environment.NewLine, sortedNetNames);
        this.KiCadNetConnectionsList.ItemsSource = sortedConnections;
        this.KiCadNetConnectionsPanel.IsVisible = true;
        this.UpdateKiCadNetConnectionsClearButtonState(true);
    }

    // ###########################################################################################
    // Renders PCB copper geometry for a precomputed set of active references and net names.
    // Missing KiCad net caches are built in the background so the UI can render immediately and
    // refresh itself when the heavy continuity graph becomes available.
    // Opposite-side traces now share the same opacity behavior as the primary side, so only the
    // opposite-side color remains configurable from board data.
    // ###########################################################################################
    private void RenderKiCadPcbGeometry(
        KiCadProjectView view,
        IReadOnlySet<string> activeReferences,
        IReadOnlySet<string> activeNets)
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
        Color oppositeTraceHighlightColor = Colors.DodgerBlue;

        if (this.schematicByName.TryGetValue(currentSchematicName, out var schematicEntry))
        {
            overlayColor = ParseColorOrDefault(schematicEntry.SchematicHighlightColor, Colors.DeepSkyBlue);
            baseOpacity = ParseOpacityOrDefault(schematicEntry.SchematicHighlightOpacity, 0.20);
            oppositeTraceHighlightColor = ParseColorOrDefault(schematicEntry.OppositeTraceHighlightColor, Colors.DodgerBlue);
        }

        double translatedOpacity = Math.Clamp(baseOpacity + 0.25, 0.0, 1.0);
        string? activeHoveredKiCadNetName = this.GetActiveHoveredKiCadNetName();

        var matchingNetIds = pcb.Nets.List
            .Where(net => !string.IsNullOrWhiteSpace(net.NormalizedName) &&
                          activeNets.Contains(net.NormalizedName.Trim()) &&
                          !string.IsNullOrWhiteSpace(net.Id))
            .Select(net => new { Id = net.Id!.Trim(), Name = net.NormalizedName!.Trim() })
            .Distinct()
            .ToList();

        string primaryLayer = string.Equals(view.Type, "pcb_bottom", StringComparison.OrdinalIgnoreCase)
            ? "B.Cu"
            : "F.Cu";

        string oppositeLayer = string.Equals(primaryLayer, "F.Cu", StringComparison.OrdinalIgnoreCase)
            ? "B.Cu"
            : "F.Cu";

        bool showOppositeSideTraces = UserSettings.SchematicsShowOppositeSideTraces;
        bool showZones = UserSettings.SchematicsShowZones;

        var primitives = new List<KiCadOverlayPrimitive>();
        var firstPinBrush = this.ResolveThemeBrush("Schematics_FirstPin", new SolidColorBrush(Colors.Orange));

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

        void AddTracePrimitivesForLayer(
            KiCadPcbNetRenderCache cache,
            HashSet<string> activeDrawIds,
            IBrush strokeBrush)
        {
            var activeSegmentNodes = cache.SegmentNodes
                .Where(segmentNode => activeDrawIds.Contains(segmentNode.Info.Id))
                .ToList();

            foreach (var segmentGroup in activeSegmentNodes.GroupBy(
                         segmentNode => Math.Round(segmentNode.WidthWorld, 6)))
            {
                var groupedSegments = segmentGroup.ToList();

                if (groupedSegments.Count == 0)
                {
                    continue;
                }

                double thickness = this.GetKiCadOverlayStrokeThickness(
                    this.MapKiCadWorldLengthToLocal(
                        groupedSegments[0].WidthWorld,
                        worldBounds,
                        contentRect,
                        calibration));

                var pen = new Pen(strokeBrush, thickness);

                foreach (var chain in this.BuildConnectedKiCadPcbSegmentPointChains(groupedSegments))
                {
                    if (chain.Count < 2)
                    {
                        continue;
                    }

                    var localPoints = chain
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

                double thickness = this.GetKiCadOverlayStrokeThickness(
                    this.MapKiCadWorldLengthToLocal(
                        arcNode.WidthWorld,
                        worldBounds,
                        contentRect,
                        calibration));

                var sampledArcPoints = this.SampleQuadraticBezier(start, mid, end, 20);

                primitives.Add(new KiCadOverlayPrimitive
                {
                    Kind = KiCadOverlayPrimitiveKind.Polyline,
                    Points = sampledArcPoints,
                    Pen = new Pen(strokeBrush, thickness)
                });
            }
        }

        foreach (var netInfo in matchingNetIds)
        {
            if (!pcb.HighlightIndex.TryGetValue(netInfo.Id, out var bucket))
            {
                continue;
            }

            bool isHoveredNet = string.Equals(activeHoveredKiCadNetName, netInfo.Name, StringComparison.OrdinalIgnoreCase);
            bool isLockedNet = this.thisLockedKiCadNetNames.Contains(netInfo.Name);

            var selectedImportantSignalNetNames = this.BuildSelectedImportantSignalNetNames();
            bool isImportantSignalDerivedNet = selectedImportantSignalNetNames.Contains(netInfo.Name);

            bool isExplicitHighlight = isLockedNet || isHoveredNet || isImportantSignalDerivedNet;

            bool isSelectionDerivedNet = this.thisSelectedKiCadNormalizedNetNames.Contains(netInfo.Name);
            bool shouldBlinkThisNet = isLockedNet || isSelectionDerivedNet || isImportantSignalDerivedNet;

            double blinkFactor = shouldBlinkThisNet ? this.thisCurrentHighlightBlinkFactor : 1.0;
            double effectiveOpacity = Math.Clamp(translatedOpacity * blinkFactor, 0.0, 1.0);

            IBrush primaryBrush = isHoveredNet && !shouldBlinkThisNet
                ? new SolidColorBrush(overlayColor, 1.0)
                : new SolidColorBrush(overlayColor, effectiveOpacity);

            IBrush oppositeBrush = isHoveredNet && !shouldBlinkThisNet
                ? new SolidColorBrush(oppositeTraceHighlightColor, 1.0)
                : new SolidColorBrush(oppositeTraceHighlightColor, effectiveOpacity);

            if (showOppositeSideTraces)
            {
                var oppositeCache = this.GetOrCreateKiCadPcbNetRenderCache(
                    pcb,
                    view.SourceIndex,
                    netInfo.Id,
                    bucket,
                    oppositeLayer);

                if (oppositeCache != null)
                {
                    var oppositeActiveDrawIds = this.BuildKiCadPcbActiveDrawIds(
                        oppositeCache,
                        isExplicitHighlight,
                        activeReferences);

                    if (showZones)
                    {
                        foreach (var zoneNode in oppositeCache.ZoneNodes)
                        {
                            if (!oppositeActiveDrawIds.Contains(zoneNode.Info.Id))
                            {
                                continue;
                            }

                            Geometry? zoneGeometry = this.BuildKiCadZoneGeometry(
                                zoneNode.PolygonsWorld,
                                worldBounds,
                                contentRect,
                                calibration);

                            if (zoneGeometry == null)
                            {
                                continue;
                            }

                            double oppositeZoneFillOpacity = isHoveredNet && !shouldBlinkThisNet
                                ? Math.Min(1.0, Math.Clamp(translatedOpacity * 0.65, 0.10, 0.38) + 0.12)
                                : Math.Clamp(effectiveOpacity * 0.65, 0.10, 0.38);

                            primitives.Add(new KiCadOverlayPrimitive
                            {
                                Kind = KiCadOverlayPrimitiveKind.Geometry,
                                Geometry = zoneGeometry,
                                Fill = new SolidColorBrush(oppositeTraceHighlightColor, oppositeZoneFillOpacity),
                                Pen = new Pen(oppositeBrush, 1.0)
                            });
                        }
                    }

                    AddTracePrimitivesForLayer(oppositeCache, oppositeActiveDrawIds, oppositeBrush);
                }
            }

            var primaryCache = this.GetOrCreateKiCadPcbNetRenderCache(
                pcb,
                view.SourceIndex,
                netInfo.Id,
                bucket,
                primaryLayer);

            if (primaryCache == null)
            {
                continue;
            }

            var primaryActiveDrawIds = this.BuildKiCadPcbActiveDrawIds(
                primaryCache,
                isExplicitHighlight,
                activeReferences);

            if (showZones)
            {
                foreach (var zoneNode in primaryCache.ZoneNodes)
                {
                    if (!primaryActiveDrawIds.Contains(zoneNode.Info.Id))
                    {
                        continue;
                    }

                    Geometry? zoneGeometry = this.BuildKiCadZoneGeometry(
                        zoneNode.PolygonsWorld,
                        worldBounds,
                        contentRect,
                        calibration);

                    if (zoneGeometry == null)
                    {
                        continue;
                    }

                    double zoneFillOpacity = isHoveredNet && !shouldBlinkThisNet
                        ? 0.32
                        : Math.Clamp(effectiveOpacity * 0.65, 0.10, 0.38);

                    primitives.Add(new KiCadOverlayPrimitive
                    {
                        Kind = KiCadOverlayPrimitiveKind.Geometry,
                        Geometry = zoneGeometry,
                        Fill = new SolidColorBrush(overlayColor, zoneFillOpacity),
                        Pen = new Pen(primaryBrush, 1.0)
                    });
                }
            }

            AddTracePrimitivesForLayer(primaryCache, primaryActiveDrawIds, primaryBrush);

            foreach (var viaNode in primaryCache.ViaNodes)
            {
                if (!primaryActiveDrawIds.Contains(viaNode.Info.Id))
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
                    Pen = new Pen(primaryBrush, 1.2),
                    Fill = primaryBrush
                });
            }

            foreach (var padNode in primaryCache.PadNodes)
            {
                if (!primaryActiveDrawIds.Contains(padNode.Info.Id))
                {
                    continue;
                }

                bool isSelectedComponentPin1 = this.ShouldUseSelectedComponentPin1Highlight(
                    padNode.Footprint,
                    padNode.Pad,
                    primaryLayer);

                IBrush padBrush = isSelectedComponentPin1
                    ? firstPinBrush
                    : primaryBrush;

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
                        !TabSchematics.IsKiCadPcbPointVisibleOnSide(pad.Layers, primaryLayer) ||
                        !TabSchematics.IsPrimaryPadForPin1Highlight(footprint, pad, primaryLayer))
                    {
                        continue;
                    }

                    AddPadPrimitive(pad, firstPinBrush);
                }
            }
        }

        this.SchematicsKiCadOverlayCanvas.SetGeometry(primitives);
    }

    // ###########################################################################################
    // Renders resolved schematic wire paths for a precomputed set of active normalized net names.
    // This avoids recomputing hover-preview net names multiple times within one refresh cycle.
    // ###########################################################################################
    private void RenderKiCadSchematicGeometry(
        KiCadProjectView view,
        IReadOnlySet<string> activeNets)
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
            overlayColor = ParseColorOrDefault(schematicEntry.SchematicHighlightColor, Colors.Orange);
            baseOpacity = ParseOpacityOrDefault(schematicEntry.SchematicHighlightOpacity, 0.20);
        }

        double translatedOpacity = Math.Clamp(baseOpacity + 0.25, 0.0, 1.0);
        string? activeHoveredKiCadNetName = this.GetActiveHoveredKiCadNetName();

        var primitives = new List<KiCadOverlayPrimitive>();

        foreach (string normalizedNetName in activeNets)
        {
            if (!indexByNet.TryGetValue(normalizedNetName, out var resolvedPaths))
            {
                continue;
            }

            bool isHoveredNet = string.Equals(activeHoveredKiCadNetName, normalizedNetName, StringComparison.OrdinalIgnoreCase);
            bool isLockedNet = this.thisLockedKiCadNetNames.Contains(normalizedNetName);

            var selectedImportantSignalNetNames = this.BuildSelectedImportantSignalNetNames();
            bool isImportantSignalDerivedNet = selectedImportantSignalNetNames.Contains(normalizedNetName);

            bool isSelectionDerivedNet = this.thisSelectedKiCadNormalizedNetNames.Contains(normalizedNetName);
            bool shouldBlinkThisNet = isLockedNet || isSelectionDerivedNet || isImportantSignalDerivedNet;

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
    // Returns true when the supplied zone is visible on the inspected PCB side.
    // ###########################################################################################
    private static bool IsKiCadPcbZoneVisibleOnSide(KiCadPcbZone zone, string requiredLayer)
    {
        return TabSchematics.IsKiCadPcbPointVisibleOnSide(zone.Layers, requiredLayer);
    }

    // ###########################################################################################
    // Returns the world-space polygons that should be used for one zone.
    // Filled polygons are preferred because they match the final poured copper area.
    // ###########################################################################################
    private static IReadOnlyList<IReadOnlyList<Point>> GetKiCadZoneWorldPolygons(KiCadPcbZone zone)
    {
        var sourcePolygons = zone.FilledPolygons.Count > 0
            ? zone.FilledPolygons
            : zone.OutlinePolygons;

        return sourcePolygons
            .Where(polygon => polygon.Points.Count >= 3)
            .Select(polygon => (IReadOnlyList<Point>)polygon.Points
                .Select(point => new Point(point.X, point.Y))
                .ToList())
            .ToList();
    }

    // ###########################################################################################
    // Returns true when the point lies inside the polygon using ray-casting.
    // ###########################################################################################
    private static bool IsPointInPolygon(IReadOnlyList<Point> polygon, Point point)
    {
        if (polygon.Count < 3)
        {
            return false;
        }

        bool inside = false;
        int previousIndex = polygon.Count - 1;

        for (int currentIndex = 0; currentIndex < polygon.Count; currentIndex++)
        {
            Point current = polygon[currentIndex];
            Point previous = polygon[previousIndex];

            bool intersects = ((current.Y > point.Y) != (previous.Y > point.Y)) &&
                              (point.X < ((previous.X - current.X) * (point.Y - current.Y) / ((previous.Y - current.Y) + 1e-12)) + current.X);

            if (intersects)
            {
                inside = !inside;
            }

            previousIndex = currentIndex;
        }

        return inside;
    }

    // ###########################################################################################
    // Returns the shortest distance from the point to the polygon boundary.
    // ###########################################################################################
    private static double GetDistanceToPolygonBoundary(Point point, IReadOnlyList<Point> polygon)
    {
        if (polygon.Count < 2)
        {
            return double.MaxValue;
        }

        double minimumDistance = double.MaxValue;

        for (int i = 0; i < polygon.Count; i++)
        {
            Point start = polygon[i];
            Point end = polygon[(i + 1) % polygon.Count];

            double distance = TabSchematics.DistanceToSegment(
                point,
                start.X,
                start.Y,
                end.X,
                end.Y);

            if (distance < minimumDistance)
            {
                minimumDistance = distance;
            }
        }

        return minimumDistance;
    }

    // ###########################################################################################
    // Returns true when the point is inside the zone or near its boundary within the supplied
    // tolerance. The closest distance is returned so overlapping candidates can be ranked.
    // ###########################################################################################
    private static bool IsPointInOrNearZone(
        Point point,
        IReadOnlyList<IReadOnlyList<Point>> polygonsWorld,
        double toleranceWorld,
        out double distanceWorld)
    {
        distanceWorld = double.MaxValue;

        foreach (var polygon in polygonsWorld)
        {
            if (TabSchematics.IsPointInPolygon(polygon, point))
            {
                distanceWorld = 0.0;
                return true;
            }

            double boundaryDistance = TabSchematics.GetDistanceToPolygonBoundary(point, polygon);
            if (boundaryDistance < distanceWorld)
            {
                distanceWorld = boundaryDistance;
            }
        }

        return distanceWorld <= toleranceWorld;
    }

    // ###########################################################################################
    // Returns true when a circular copper feature touches the zone.
    // ###########################################################################################
    private static bool DoesCircleTouchZone(
        Point centerWorld,
        double radiusWorld,
        IReadOnlyList<IReadOnlyList<Point>> polygonsWorld)
    {
        return TabSchematics.IsPointInOrNearZone(centerWorld, polygonsWorld, radiusWorld, out _);
    }

    // ###########################################################################################
    // Returns true when a segment touches the zone.
    // Uses fast endpoint checks and an adaptive sample count instead of a fixed heavy sample loop.
    // ###########################################################################################
    private static bool DoesSegmentTouchZone(
        Point startWorld,
        Point endWorld,
        double radiusWorld,
        IReadOnlyList<IReadOnlyList<Point>> polygonsWorld)
    {
        if (polygonsWorld.Count == 0)
        {
            return false;
        }

        if (TabSchematics.IsPointInOrNearZone(startWorld, polygonsWorld, radiusWorld, out _) ||
            TabSchematics.IsPointInOrNearZone(endWorld, polygonsWorld, radiusWorld, out _))
        {
            return true;
        }

        double dx = endWorld.X - startWorld.X;
        double dy = endWorld.Y - startWorld.Y;
        double segmentLength = Math.Sqrt((dx * dx) + (dy * dy));

        int sampleCount = Math.Clamp(
            (int)Math.Ceiling(segmentLength / Math.Max(0.75, radiusWorld * 3.0)),
            4,
            12);

        for (int i = 1; i < sampleCount; i++)
        {
            double t = (double)i / sampleCount;

            Point samplePoint = new(
                startWorld.X + (dx * t),
                startWorld.Y + (dy * t));

            if (TabSchematics.IsPointInOrNearZone(samplePoint, polygonsWorld, radiusWorld, out _))
            {
                return true;
            }
        }

        return false;
    }

    // ###########################################################################################
    // Returns true when an arc touches the zone.
    // Uses fast control-point checks and a lightweight adaptive world-space sampler.
    // ###########################################################################################
    private bool DoesArcTouchZone(
        KiCadPcbArcRenderNode arcNode,
        double radiusWorld,
        IReadOnlyList<IReadOnlyList<Point>> polygonsWorld)
    {
        if (polygonsWorld.Count == 0)
        {
            return false;
        }

        if (TabSchematics.IsPointInOrNearZone(arcNode.StartWorld, polygonsWorld, radiusWorld, out _) ||
            TabSchematics.IsPointInOrNearZone(arcNode.MidWorld, polygonsWorld, radiusWorld, out _) ||
            TabSchematics.IsPointInOrNearZone(arcNode.EndWorld, polygonsWorld, radiusWorld, out _))
        {
            return true;
        }

        double firstLegLength = Math.Sqrt(
            Math.Pow(arcNode.MidWorld.X - arcNode.StartWorld.X, 2.0) +
            Math.Pow(arcNode.MidWorld.Y - arcNode.StartWorld.Y, 2.0));

        double secondLegLength = Math.Sqrt(
            Math.Pow(arcNode.EndWorld.X - arcNode.MidWorld.X, 2.0) +
            Math.Pow(arcNode.EndWorld.Y - arcNode.MidWorld.Y, 2.0));

        double approximateArcLength = firstLegLength + secondLegLength;

        int sampleCount = Math.Clamp(
            (int)Math.Ceiling(approximateArcLength / Math.Max(1.0, radiusWorld * 3.5)),
            6,
            16);

        for (int i = 1; i < sampleCount; i++)
        {
            double t = (double)i / sampleCount;
            double mt = 1.0 - t;

            Point samplePoint = new(
                (mt * mt * arcNode.StartWorld.X) + (2.0 * mt * t * arcNode.MidWorld.X) + (t * t * arcNode.EndWorld.X),
                (mt * mt * arcNode.StartWorld.Y) + (2.0 * mt * t * arcNode.MidWorld.Y) + (t * t * arcNode.EndWorld.Y));

            if (TabSchematics.IsPointInOrNearZone(samplePoint, polygonsWorld, radiusWorld, out _))
            {
                return true;
            }
        }

        return false;
    }

    // ###########################################################################################
    // Builds one filled geometry for a KiCad copper zone from world-space polygons.
    // ###########################################################################################
    private Geometry? BuildKiCadZoneGeometry(
        IReadOnlyList<IReadOnlyList<Point>> polygonsWorld,
        Rect worldBounds,
        Rect contentRect,
        KiCadViewCalibration calibration)
    {
        if (polygonsWorld.Count == 0)
        {
            return null;
        }

        var geometry = new StreamGeometry();
        bool hasFigure = false;

        using (var geometryContext = geometry.Open())
        {
            foreach (var polygon in polygonsWorld)
            {
                if (polygon.Count < 3)
                {
                    continue;
                }

                var localPoints = polygon
                    .Select(point => this.MapKiCadWorldToLocal(
                        point.X,
                        point.Y,
                        worldBounds,
                        contentRect,
                        calibration))
                    .ToList();

                if (localPoints.Count < 3)
                {
                    continue;
                }

                geometryContext.BeginFigure(localPoints[0], isFilled: true);

                for (int i = 1; i < localPoints.Count; i++)
                {
                    geometryContext.LineTo(localPoints[i]);
                }

                geometryContext.EndFigure(isClosed: true);
                hasFigure = true;
            }
        }

        return hasFigure ? geometry : null;
    }

    // ###########################################################################################
    // Computes a world-space bounding box for a zone polygon set.
    // ###########################################################################################
    private static Rect GetKiCadZoneWorldBounds(IReadOnlyList<IReadOnlyList<Point>> polygonsWorld)
    {
        bool hasValue = false;
        double minX = 0;
        double minY = 0;
        double maxX = 0;
        double maxY = 0;

        foreach (var polygon in polygonsWorld)
        {
            foreach (var point in polygon)
            {
                if (!hasValue)
                {
                    minX = maxX = point.X;
                    minY = maxY = point.Y;
                    hasValue = true;
                    continue;
                }

                if (point.X < minX) minX = point.X;
                if (point.X > maxX) maxX = point.X;
                if (point.Y < minY) minY = point.Y;
                if (point.Y > maxY) maxY = point.Y;
            }
        }

        return hasValue
            ? new Rect(minX, minY, Math.Max(0.0001, maxX - minX), Math.Max(0.0001, maxY - minY))
            : default;
    }

    // ###########################################################################################
    // Syncs the copied global settings controls from persisted user settings.
    // ###########################################################################################
    private void UpdateGlobalSettingsControls()
    {
        this.thisSuppressGlobalSettingsChanged = true;

        bool isInteractiveCadTraceHoverEnabled =
            !string.Equals(UserSettings.InteractiveCadTraceHoverMode, "Disabled", StringComparison.Ordinal);

        bool isInteractiveCadTraceHoverHoldShiftMode =
            string.Equals(UserSettings.InteractiveCadTraceHoverMode, "HoldShift", StringComparison.Ordinal);

        this.CheckGlobalHoverHighlightsTraces.IsChecked = isInteractiveCadTraceHoverEnabled;
        this.CheckGlobalShowOppositeSideTraces.IsChecked = UserSettings.SchematicsShowOppositeSideTraces;
        this.CheckGlobalShowZones.IsChecked = UserSettings.SchematicsShowZones;

        bool shouldRestoreHoldShiftCheckState =
            isInteractiveCadTraceHoverEnabled ||
            this.SchematicsInteractiveCadTraceHoverHoldShiftCheckBox.IsChecked is null;

        if (shouldRestoreHoldShiftCheckState)
        {
            this.SchematicsInteractiveCadTraceHoverHoldShiftCheckBox.IsChecked = isInteractiveCadTraceHoverHoldShiftMode;
        }

        this.UpdateSchematicsInteractiveCadTraceHoverHoldShiftVisualState(isInteractiveCadTraceHoverEnabled);

        this.thisSuppressGlobalSettingsChanged = false;
    }

    // ###########################################################################################
    // Persists the copied interactive CAD trace hover option from the schematics global settings panel.
    // ###########################################################################################
    private void OnSchematicsInteractiveCadTraceHoverModeChanged(object? sender, RoutedEventArgs e)
    {
        this.ApplyInteractiveCadTraceHoverModeFromGlobalSettings();
    }

    // ###########################################################################################
    // Handles row clicks for the "Hold SHIFT to highlight traces on hover" option.
    // ###########################################################################################
    private void OnSchematicsInteractiveCadTraceHoverHoldShiftRowClicked(object? sender, PointerPressedEventArgs e)
    {
        if (!this.SchematicsInteractiveCadTraceHoverHoldShiftCheckBox.IsEnabled)
        {
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.SchematicsInteractiveCadTraceHoverHoldShiftCheckBox.IsChecked =
                this.SchematicsInteractiveCadTraceHoverHoldShiftCheckBox.IsChecked != true;

            e.Handled = true;
        }
    }

    // ###########################################################################################
    // Persists the global hover-trace behavior from the schematics global settings panel.
    // ###########################################################################################
    private void ApplyInteractiveCadTraceHoverModeFromGlobalSettings()
    {
        if (this.thisSuppressGlobalSettingsChanged)
        {
            return;
        }

        bool isEnabled = this.CheckGlobalHoverHighlightsTraces.IsChecked == true;
        bool requiresShift =
            isEnabled &&
            this.SchematicsInteractiveCadTraceHoverHoldShiftCheckBox.IsChecked == true;

        this.UpdateSchematicsInteractiveCadTraceHoverHoldShiftVisualState(isEnabled);

        UserSettings.InteractiveCadTraceHoverMode = !isEnabled
            ? "Disabled"
            : requiresShift
                ? "HoldShift"
                : "Always";
    }

    // ###########################################################################################
    // Applies enabled state, cursor, and dimmed appearance to the SHIFT hover option row.
    // ###########################################################################################
    private void UpdateSchematicsInteractiveCadTraceHoverHoldShiftVisualState(bool isEnabled)
    {
        this.SchematicsInteractiveCadTraceHoverHoldShiftCheckBox.IsEnabled = isEnabled;
        this.SchematicsInteractiveCadTraceHoverHoldShiftRow.IsEnabled = isEnabled;
        this.SchematicsInteractiveCadTraceHoverHoldShiftRow.Opacity = isEnabled ? 1.0 : 0.55;
        this.SchematicsInteractiveCadTraceHoverHoldShiftRow.Cursor = isEnabled
            ? new Cursor(StandardCursorType.Hand)
            : Cursor.Default;
    }

    // ###########################################################################################
    // Enables or disables navigation controls that would invalidate the current label-editor session.
    // While the editor is active, schematic thumbnails and the main hardware/board selectors are locked.
    // Also updates thumbnail relevance dimming, the main-window special-mode banner, and the
    // shared component search box mode.
    // ###########################################################################################
    private void UpdateLabelEditorLockState()
    {
        bool isNavigationEnabled = !this.thisIsLabelEditorMode;

        if (!isNavigationEnabled)
        {
            this.thisIsDraggingThumbnail = false;
            this.thisThumbnailDragStartEventArgs = null;
            this.ClearThumbnailDropPlaceholder();
            this.HideThumbnailDragGhost();
        }

        this.SchematicsThumbnailList.IsEnabled = isNavigationEnabled;
        this.MainWindow?.SetSchematicsEditorNavigationEnabled(isNavigationEnabled);
        this.MainWindow?.SetSchematicsLabelEditorModeBannerVisible(this.thisIsLabelEditorMode);
        this.MainWindow?.UpdateComponentSearchTextBoxMode();

        if (this.MainWindow?.CurrentBoardData != null)
        {
            this.MainWindow.OnComponentSearchTextChanged(null, null!);
        }

        bool hasComponentSelection = this.highlightIndexBySchematic.Count > 0;
        bool hasBlinkEligibleSelection = this.HasBlinkEligibleSelection();
        double blinkFactor = this.MainWindow?.GetCurrentBlinkFactor(hasBlinkEligibleSelection) ?? 1.0;

        this.ApplyHighlightVisuals(hasComponentSelection, blinkFactor);
    }

    // ###########################################################################################
    // Converts one label-editor bitmap pixel point into schematics container coordinates so popups
    // can be positioned near duplicated or newly created rectangles.
    // ###########################################################################################
    private Point ConvertLabelEditorPixelPointToContainerPoint(Point pixelPoint)
    {
        if (this.currentFullResBitmap == null ||
            this.currentFullResBitmap.PixelSize.Width <= 0 ||
            this.currentFullResBitmap.PixelSize.Height <= 0)
        {
            return new Point(0, 0);
        }

        var contentRect = this.GetLabelEditorImageContentRect();

        double localX = contentRect.X + ((pixelPoint.X / this.currentFullResBitmap.PixelSize.Width) * contentRect.Width);
        double localY = contentRect.Y + ((pixelPoint.Y / this.currentFullResBitmap.PixelSize.Height) * contentRect.Height);

        return new Point(
            (localX * this.schematicsMatrix.M11) + (localY * this.schematicsMatrix.M21) + this.schematicsMatrix.M31,
            (localX * this.schematicsMatrix.M12) + (localY * this.schematicsMatrix.M22) + this.schematicsMatrix.M32);
    }

    // ###########################################################################################
    // Duplicates the currently selected label-editor rectangle, places the copy next to the source,
    // and opens the new-label prompt for the duplicated component.
    // The duplicated component prefers the source category and makes it the new default category.
    // ###########################################################################################
    private bool TryDuplicateSelectedLabelEditorHighlight()
    {
        if (!this.thisIsLabelEditorMode ||
            this.SchematicsNewLabelPromptBorder.IsVisible ||
            this.thisIsDrawingLabelEditorRectangle ||
            this.thisLabelEditorDragMode != LabelEditorDragMode.None ||
            this.currentFullResBitmap == null)
        {
            return false;
        }

        var selectedHighlights = this.GetSelectedLabelEditorHighlightsForCurrentSchematic();
        if (selectedHighlights.Count != 1)
        {
            return false;
        }

        var sourceHighlight = selectedHighlights[0];
        string duplicatedCategory = sourceHighlight.Category?.Trim() ?? string.Empty;

        const double duplicateGapPixels = 12.0;

        double bitmapWidth = this.currentFullResBitmap.PixelSize.Width;
        double bitmapHeight = this.currentFullResBitmap.PixelSize.Height;

        double duplicateX = sourceHighlight.X + sourceHighlight.Width + duplicateGapPixels;
        double duplicateY = sourceHighlight.Y;

        if (duplicateX + sourceHighlight.Width > bitmapWidth)
        {
            duplicateX = sourceHighlight.X - sourceHighlight.Width - duplicateGapPixels;
        }

        duplicateX = Math.Clamp(
            duplicateX,
            0.0,
            Math.Max(0.0, bitmapWidth - sourceHighlight.Width));

        duplicateY = Math.Clamp(
            duplicateY,
            0.0,
            Math.Max(0.0, bitmapHeight - sourceHighlight.Height));

        this.PushLabelEditorUndoState(this.CreateLabelEditorUndoState());

        var duplicatedHighlight = new EditableComponentHighlight
        {
            SchematicName = sourceHighlight.SchematicName,
            BoardLabel = string.Empty,
            Category = duplicatedCategory,
            X = duplicateX,
            Y = duplicateY,
            Width = sourceHighlight.Width,
            Height = sourceHighlight.Height
        };

        if (!string.IsNullOrWhiteSpace(duplicatedCategory))
        {
            this.thisLastCreatedLabelEditorCategory = duplicatedCategory;
        }

        this.thisLabelEditorWorkingHighlights.Add(duplicatedHighlight);
        this.SetSingleSelectedLabelEditorHighlight(duplicatedHighlight, refresh: false);
        this.thisPendingNewLabelEditorHighlight = duplicatedHighlight;

        this.RefreshLabelEditorOverlay();

        Point promptAnchorPoint = this.ConvertLabelEditorPixelPointToContainerPoint(
            new Point(
                duplicatedHighlight.X + (duplicatedHighlight.Width / 2.0),
                duplicatedHighlight.Y + (duplicatedHighlight.Height / 2.0)));

        this.ShowNewLabelEditorPrompt(promptAnchorPoint);

        Logger.Info(
            $"Label editor duplicated rectangle from board label [{sourceHighlight.BoardLabel}] on schematic [{sourceHighlight.SchematicName}]");

        return true;
    }

    // ###########################################################################################
    // Applies neighbor-edge snapping to a newly drawn rectangle by reusing the existing resize
    // snap logic for all four edges while the rectangle is still only a draft.
    // ###########################################################################################
    private void ApplyNewLabelEditorRectangleSnap(
        ref Rect rect,
        List<(Point Start, Point End)> snapGuides,
        bool suppressSnap)
    {
        if (suppressSnap ||
            rect.Width <= 0 ||
            rect.Height <= 0 ||
            string.IsNullOrWhiteSpace(this.GetCurrentSchematicName()))
        {
            return;
        }

        double left = rect.Left;
        double top = rect.Top;
        double right = rect.Right;
        double bottom = rect.Bottom;

        var draftHighlight = new EditableComponentHighlight
        {
            SchematicName = this.GetCurrentSchematicName(),
            X = rect.X,
            Y = rect.Y,
            Width = rect.Width,
            Height = rect.Height
        };

        LabelEditorDragMode originalDragMode = this.thisLabelEditorDragMode;

        try
        {
            this.thisLabelEditorDragMode = LabelEditorDragMode.ResizeTop;
            this.ApplyLabelEditorResizeSnap(
                draftHighlight,
                ref left,
                ref top,
                ref right,
                ref bottom,
                snapGuides,
                suppressSnap: false);

            this.thisLabelEditorDragMode = LabelEditorDragMode.ResizeBottom;
            this.ApplyLabelEditorResizeSnap(
                draftHighlight,
                ref left,
                ref top,
                ref right,
                ref bottom,
                snapGuides,
                suppressSnap: false);

            this.thisLabelEditorDragMode = LabelEditorDragMode.ResizeLeft;
            this.ApplyLabelEditorResizeSnap(
                draftHighlight,
                ref left,
                ref top,
                ref right,
                ref bottom,
                snapGuides,
                suppressSnap: false);

            this.thisLabelEditorDragMode = LabelEditorDragMode.ResizeRight;
            this.ApplyLabelEditorResizeSnap(
                draftHighlight,
                ref left,
                ref top,
                ref right,
                ref bottom,
                snapGuides,
                suppressSnap: false);
        }
        finally
        {
            this.thisLabelEditorDragMode = originalDragMode;
        }

        rect = new Rect(
            left,
            top,
            Math.Max(1.0, right - left),
            Math.Max(1.0, bottom - top));
    }

    // ###########################################################################################
    // Applies the current expand/collapse state to the KiCad net connections panel content.
    // ###########################################################################################
    private void UpdateKiCadNetConnectionsPanelExpandedState(bool isExpanded)
    {
        this.KiCadNetConnectionsContentPanel.IsVisible = isExpanded;
        this.ToggleKiCadNetConnectionsPanelButton.Content = isExpanded ? "Collapse" : "Expand";
    }

    // ###########################################################################################
    // Updates the visibility and enabled state of the KiCad trace-clear button.
    // The button only clears explicit KiCad net picks and Important signal selections.
    // ###########################################################################################
    private void UpdateKiCadNetConnectionsClearButtonState(bool hasVisibleNetlistContent)
    {
        bool hasAnythingToClear =
            this.thisLockedKiCadNetNames.Count > 0 ||
            this.thisSelectedImportantSignalDisplayNames.Count > 0;

        this.ClearKiCadTraceSelectionButton.IsVisible = hasAnythingToClear;
        this.ClearKiCadTraceSelectionButton.IsEnabled = hasAnythingToClear;
    }

    // ###########################################################################################
    // Handle manual row clicks for selected-component trace preview.
    // ###########################################################################################
    private void OnGlobalShowTracesOnComponentSelectRowClicked(object? sender, PointerPressedEventArgs e)
    {
        if (!this.CheckGlobalShowTracesOnComponentSelect.IsEnabled)
        {
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.CheckGlobalShowTracesOnComponentSelect.IsChecked = !this.CheckGlobalShowTracesOnComponentSelect.IsChecked;
            e.Handled = true;
        }
    }

    // ###########################################################################################
    // Applies search text to the label editor so the search box can find editor components by
    // board label or category while the editor is active.
    // ###########################################################################################
    internal void ApplyLabelEditorSearchFilter(string searchTerm)
    {
        this.thisLabelEditorSearchText = searchTerm?.Trim() ?? string.Empty;

        if (!this.thisIsLabelEditorMode)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(this.thisLabelEditorSearchText))
        {
            this.RefreshLabelEditorOverlay();
            return;
        }

        var matches = this.GetMatchingLabelEditorHighlightsForCurrentSchematic();
        if (matches.Count == 0)
        {
            this.RefreshLabelEditorOverlay();
            return;
        }

        if (this.thisSelectedLabelEditorHighlight == null ||
            !matches.Contains(this.thisSelectedLabelEditorHighlight))
        {
            this.SetSingleSelectedLabelEditorHighlight(matches[0], refresh: false);
        }

        this.RefreshLabelEditorOverlay();
    }

    // ###########################################################################################
    // Returns the current-schematic label editor highlights that match the active search text.
    // ###########################################################################################
    private List<EditableComponentHighlight> GetMatchingLabelEditorHighlightsForCurrentSchematic()
    {
        string schematicName = this.GetCurrentSchematicName();

        return this.thisLabelEditorWorkingHighlights
            .Where(row => string.Equals(row.SchematicName, schematicName, StringComparison.OrdinalIgnoreCase))
            .Where(this.DoesLabelEditorHighlightMatchSearch)
            .ToList();
    }

    // ###########################################################################################
    // Returns true when the given label editor highlight matches the active search text.
    // Matches board label and category using the same multi-term AND behavior as normal search.
    // ###########################################################################################
    private bool DoesLabelEditorHighlightMatchSearch(EditableComponentHighlight highlight)
    {
        if (string.IsNullOrWhiteSpace(this.thisLabelEditorSearchText))
        {
            return true;
        }

        var searchTerms = this.thisLabelEditorSearchText
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (searchTerms.Length == 0)
        {
            return true;
        }

        string searchableText = string.Join(
            " | ",
            new[]
            {
                highlight.BoardLabel?.Trim() ?? string.Empty,
                highlight.Category?.Trim() ?? string.Empty
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

        foreach (string term in searchTerms)
        {
            if (searchableText.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }
        }

        return true;
    }

    // ###########################################################################################
    // Handle manual row clicks for opposite-side PCB trace preview.
    // ###########################################################################################
    private void OnGlobalShowOppositeSideTracesRowClicked(object? sender, PointerPressedEventArgs e)
    {
        if (!this.CheckGlobalShowOppositeSideTraces.IsEnabled)
        {
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.CheckGlobalShowOppositeSideTraces.IsChecked = !this.CheckGlobalShowOppositeSideTraces.IsChecked;
            e.Handled = true;
        }
    }

    // ###########################################################################################
    // Builds a stable runtime cache scope key for the current board's raw KiCad files.
    // The key matches the raw loader identity by including file paths and last-write timestamps.
    // ###########################################################################################
    private string BuildCurrentKiCadRuntimeCacheScopeKey()
    {
        var rawPaths = this.MainWindow?.GetCurrentBoardKiCadRawPaths() ?? new List<string>();

        var existingPaths = rawPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(System.IO.Path.GetFullPath)
            .Where(System.IO.File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (existingPaths.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            "\u001E",
            existingPaths.Select(path =>
                $"{path}|{System.IO.File.GetLastWriteTimeUtc(path).Ticks.ToString(CultureInfo.InvariantCulture)}"));
    }

    // ###########################################################################################
    // Returns the active KiCad runtime cache scope for the current board and updates the LRU order.
    // Creates a new scope when this KiCad project has not been seen before in the current session.
    // ###########################################################################################
    private KiCadRuntimeCacheScope? GetOrCreateCurrentKiCadRuntimeCacheScope()
    {
        string scopeKey = this.BuildCurrentKiCadRuntimeCacheScopeKey();
        this.thisCurrentKiCadRuntimeCacheScopeKey = scopeKey;

        if (string.IsNullOrWhiteSpace(scopeKey))
        {
            return null;
        }

        if (!this.thisKiCadRuntimeCacheScopeByKey.TryGetValue(scopeKey, out var scope))
        {
            scope = new KiCadRuntimeCacheScope();
            this.thisKiCadRuntimeCacheScopeByKey[scopeKey] = scope;
        }

        this.TouchKiCadRuntimeCacheScope(scopeKey);
        this.TrimKiCadRuntimeCacheScopes();

        return scope;
    }

    // ###########################################################################################
    // Marks one runtime cache scope as recently used so older boards can be evicted first.
    // ###########################################################################################
    private void TouchKiCadRuntimeCacheScope(string scopeKey)
    {
        if (string.IsNullOrWhiteSpace(scopeKey))
        {
            return;
        }

        var node = this.thisKiCadRuntimeCacheScopeLru.First;
        while (node != null)
        {
            var next = node.Next;

            if (string.Equals(node.Value, scopeKey, StringComparison.OrdinalIgnoreCase))
            {
                this.thisKiCadRuntimeCacheScopeLru.Remove(node);
                break;
            }

            node = next;
        }

        this.thisKiCadRuntimeCacheScopeLru.AddFirst(scopeKey);
    }

    // ###########################################################################################
    // Limits the number of retained KiCad runtime cache scopes so switching across many boards
    // does not grow memory usage without bound during one application session.
    // ###########################################################################################
    private void TrimKiCadRuntimeCacheScopes()
    {
        const int maximumRetainedScopes = 4;

        while (this.thisKiCadRuntimeCacheScopeLru.Count > maximumRetainedScopes)
        {
            var lastNode = this.thisKiCadRuntimeCacheScopeLru.Last;
            if (lastNode == null)
            {
                break;
            }

            string scopeKey = lastNode.Value;
            this.thisKiCadRuntimeCacheScopeLru.RemoveLast();

            if (string.Equals(scopeKey, this.thisCurrentKiCadRuntimeCacheScopeKey, StringComparison.OrdinalIgnoreCase))
            {
                this.thisKiCadRuntimeCacheScopeLru.AddFirst(scopeKey);
                break;
            }

            this.thisKiCadRuntimeCacheScopeByKey.Remove(scopeKey);
        }
    }

    // ###########################################################################################
    // Clears only the active runtime cache scope when the current KiCad project is replaced or
    // needs to be invalidated, without destroying caches for other previously visited boards.
    // ###########################################################################################
    private void ClearCurrentKiCadRuntimeCaches()
    {
        if (string.IsNullOrWhiteSpace(this.thisCurrentKiCadRuntimeCacheScopeKey))
        {
            return;
        }

        if (!this.thisKiCadRuntimeCacheScopeByKey.TryGetValue(this.thisCurrentKiCadRuntimeCacheScopeKey, out var scope))
        {
            return;
        }

        lock (this.thisKiCadPcbNetRenderCacheSync)
        {
            scope.NetRenderCacheByKey.Clear();
            scope.NetRenderBuildTaskByKey.Clear();
        }

        lock (this.thisKiCadPcbHoverHitTestCacheSync)
        {
            scope.HoverHitTestCacheByKey.Clear();
            scope.HoverHitTestBuildTaskByKey.Clear();
        }
    }

    // ###########################################################################################
    // Handle manual row clicks for global KiCad zone visibility.
    // ###########################################################################################
    private void OnGlobalShowZonesRowClicked(object? sender, PointerPressedEventArgs e)
    {
        if (!this.CheckGlobalShowZones.IsEnabled)
        {
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            this.CheckGlobalShowZones.IsChecked = !this.CheckGlobalShowZones.IsChecked;
            e.Handled = true;
        }
    }

    // ###########################################################################################
    // Builds a stable cache key for schematic hover hit-testing on one schematic image.
    // ###########################################################################################
    private static string BuildKiCadSchematicHoverHitTestCacheKey(string schematicName, int schematicIndex)
    {
        return string.Join(
            "\u001F",
            schematicIndex.ToString(CultureInfo.InvariantCulture),
            schematicName?.Trim() ?? string.Empty);
    }

    // ###########################################################################################
    // Returns the cached schematic hover-hit-test data for the current schematic view.
    // ###########################################################################################
    private KiCadSchematicHoverHitTestCache? GetOrCreateKiCadSchematicHoverHitTestCache(
        KiCadProjectView view,
        KiCadSchematic schematic,
        Rect worldBounds,
        Rect contentRect,
        KiCadViewCalibration calibration,
        string schematicName)
    {
        string cacheKey = TabSchematics.BuildKiCadSchematicHoverHitTestCacheKey(schematicName, view.SourceIndex);

        if (this.thisKiCadSchematicHoverHitTestCacheByKey.TryGetValue(cacheKey, out var cache))
        {
            return cache;
        }

        if (!this.thisKiCadProject!.SchematicNetPathIndexBySchematicIndex.TryGetValue(view.SourceIndex, out var indexByNet) ||
            indexByNet.Count == 0)
        {
            return null;
        }

        cache = new KiCadSchematicHoverHitTestCache
        {
            CellSizeLocal = 24.0
        };

        foreach (var label in TabSchematics.EnumerateKiCadSchematicNetLabels(schematic))
        {
            string normalizedNetName = label.NormalizedText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedNetName) ||
                label.At == null ||
                !indexByNet.ContainsKey(normalizedNetName))
            {
                continue;
            }

            Point localPoint = this.MapKiCadWorldToLocal(
                label.At.X,
                label.At.Y,
                worldBounds,
                contentRect,
                calibration);

            int candidateIndex = cache.LabelCandidates.Count;
            cache.LabelCandidates.Add(new KiCadSchematicHoverLabelCandidate
            {
                NormalizedNetName = normalizedNetName,
                LocalPoint = localPoint
            });

            int cellX = TabSchematics.GetKiCadHoverCellCoord(localPoint.X, cache.CellSizeLocal);
            int cellY = TabSchematics.GetKiCadHoverCellCoord(localPoint.Y, cache.CellSizeLocal);

            TabSchematics.AddKiCadHoverIndexToCellRange(
                cache.LabelIndicesByCell,
                cellX,
                cellX,
                cellY,
                cellY,
                candidateIndex);
        }

        foreach (var pair in indexByNet)
        {
            string normalizedNetName = pair.Key?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedNetName))
            {
                continue;
            }

            foreach (var resolvedPath in pair.Value)
            {
                if (resolvedPath.Points.Count < 2)
                {
                    continue;
                }

                Point previousPoint = this.MapKiCadWorldToLocal(
                    resolvedPath.Points[0].X,
                    resolvedPath.Points[0].Y,
                    worldBounds,
                    contentRect,
                    calibration);

                for (int i = 1; i < resolvedPath.Points.Count; i++)
                {
                    Point currentPoint = this.MapKiCadWorldToLocal(
                        resolvedPath.Points[i].X,
                        resolvedPath.Points[i].Y,
                        worldBounds,
                        contentRect,
                        calibration);

                    int candidateIndex = cache.SegmentCandidates.Count;
                    cache.SegmentCandidates.Add(new KiCadSchematicHoverSegmentCandidate
                    {
                        NormalizedNetName = normalizedNetName,
                        StartLocal = previousPoint,
                        EndLocal = currentPoint
                    });

                    double minX = Math.Min(previousPoint.X, currentPoint.X);
                    double maxX = Math.Max(previousPoint.X, currentPoint.X);
                    double minY = Math.Min(previousPoint.Y, currentPoint.Y);
                    double maxY = Math.Max(previousPoint.Y, currentPoint.Y);

                    TabSchematics.AddKiCadHoverIndexToCellRange(
                        cache.SegmentIndicesByCell,
                        TabSchematics.GetKiCadHoverCellCoord(minX, cache.CellSizeLocal),
                        TabSchematics.GetKiCadHoverCellCoord(maxX, cache.CellSizeLocal),
                        TabSchematics.GetKiCadHoverCellCoord(minY, cache.CellSizeLocal),
                        TabSchematics.GetKiCadHoverCellCoord(maxY, cache.CellSizeLocal),
                        candidateIndex);

                    previousPoint = currentPoint;
                }
            }
        }

        this.thisKiCadSchematicHoverHitTestCacheByKey[cacheKey] = cache;
        return cache;
    }

    // ###########################################################################################
    // Builds the explicit KiCad net-name set that is allowed to participate in thumbnail dimming.
    // Component-derived net names are intentionally excluded so a selected component does not make
    // unrelated schematic pages look relevant just because they share one of the same net names.
    // ###########################################################################################
    private HashSet<string> BuildKiCadThumbnailDimmingNetNames()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string importantSignalNet in this.BuildSelectedImportantSignalNetNames())
        {
            if (!string.IsNullOrWhiteSpace(importantSignalNet))
            {
                result.Add(importantSignalNet);
            }
        }

        foreach (string lockedNet in this.thisLockedKiCadNetNames)
        {
            if (!string.IsNullOrWhiteSpace(lockedNet))
            {
                result.Add(lockedNet);
            }
        }

        return result;
    }

    // ###########################################################################################
    // Applies the current expand/collapse state to the Important signals panel content.
    // ###########################################################################################
    private void UpdateImportantSignalsPanelExpandedState(bool isExpanded)
    {
        this.ImportantSignalsListPanel.IsVisible = isExpanded;
        this.ToggleImportantSignalsPanelButton.Content = isExpanded ? "Collapse" : "Expand";
    }

    // ###########################################################################################
    // Updates the visibility and enabled state of the Important signals clear button.
    // The button stays visible whenever the panel has content, but is disabled when nothing is selected.
    // When the panel is collapsed and nothing is selected, the button is hidden to save space.
    // ###########################################################################################
    private void UpdateImportantSignalsClearButtonState(bool hasVisibleImportantSignalsContent)
    {
        bool hasAnythingToClear = this.thisSelectedImportantSignalDisplayNames.Count > 0;
        bool isExpanded = this.ImportantSignalsListPanel.IsVisible;

        bool shouldShowButton = hasVisibleImportantSignalsContent && (isExpanded || hasAnythingToClear);

        this.ClearImportantSignalsButton.IsVisible = shouldShowButton;
        this.ClearImportantSignalsButton.IsEnabled = hasAnythingToClear;
    }

    // ###########################################################################################
    // Updates the Important signals header so it shows only the selected-signal count.
    // When nothing is selected, the header falls back to the base panel title.
    // ###########################################################################################
    private void UpdateImportantSignalsHeaderText()
    {
        int selectedCount = this.thisSelectedImportantSignalDisplayNames.Count;

        this.ImportantSignalsHeaderTextBlock.Text = selectedCount > 0
            ? $"Important signals ({selectedCount})"
            : "Important signals";
    }

    // ###########################################################################################
    // Clears explicit KiCad trace selections that match the supplied normalized net names.
    // This allows overlapping Important signal and manual KiCad net selections to clear each other.
    // ###########################################################################################
    private void ClearKiCadTraceSelectionsForNetNames(IReadOnlyCollection<string> normalizedNetNames)
    {
        if (normalizedNetNames == null || normalizedNetNames.Count == 0)
        {
            return;
        }

        var targetNetNames = new HashSet<string>(
            normalizedNetNames
                .Where(netName => !string.IsNullOrWhiteSpace(netName))
                .Select(netName => netName.Trim()),
            StringComparer.OrdinalIgnoreCase);

        if (targetNetNames.Count == 0)
        {
            return;
        }

        this.thisLockedKiCadNetNames.RemoveWhere(netName => targetNetNames.Contains(netName));

        string hoveredNetName = this.thisHoveredKiCadNetName?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(hoveredNetName) &&
            targetNetNames.Contains(hoveredNetName))
        {
            this.thisHoveredKiCadNetName = null;
            this.thisHoveredKiCadPadNumber = null;
            this.SchematicsHoverPadBorder.IsVisible = false;
            this.SchematicsHoverPadText.Text = string.Empty;
        }
    }

    // ###########################################################################################
    // Enters interactive KiCad trace calibration mode and seeds the resize box from the currently
    // active calibration if one exists, otherwise from the default full-image KiCad bounds.
    // The temporary traces-and-pads visibility toggle always defaults to checked on entry.
    // ###########################################################################################
    private void BeginKiCadTraceCalibrationMode()
    {
        if (this.currentFullResBitmap == null || this.thisKiCadProject == null)
        {
            return;
        }

        var view = this.ResolveKiCadViewForCurrentSchematic();
        if (view == null)
        {
            return;
        }

        Rect imageBounds = this.BuildKiCadCalibrationImageBounds(view);

        this.thisKiCadCalibrationImageLeft = imageBounds.Left;
        this.thisKiCadCalibrationImageTop = imageBounds.Top;
        this.thisKiCadCalibrationImageRight = imageBounds.Right;
        this.thisKiCadCalibrationImageBottom = imageBounds.Bottom;

        // Load persisted mirror flags and re-apply them onto the calibration box.
        string excelPath = this.MainWindow?.GetCurrentBoardExcelPath() ?? string.Empty;
        string schematicName = this.GetCurrentSchematicName();
        if (BoardComponentHighlightStorage.TryLoadKiCadCalibration(
                excelPath,
                schematicName,
                out _,
                out _,
                out _,
                out _,
                out _,
                out bool mirrorX,
                out bool mirrorY))
        {
            this.ApplyKiCadCalibrationMirrorFlagsToBox(mirrorX, mirrorY);
        }

        this.thisKiCadCalibrationStartImageLeft = this.thisKiCadCalibrationImageLeft;
        this.thisKiCadCalibrationStartImageTop = this.thisKiCadCalibrationImageTop;
        this.thisKiCadCalibrationStartImageRight = this.thisKiCadCalibrationImageRight;
        this.thisKiCadCalibrationStartImageBottom = this.thisKiCadCalibrationImageBottom;

        this.thisKiCadTraceCalibrationDragMode = LabelEditorDragMode.None;
        this.thisIsKiCadTraceCalibrationMode = true;

        this.CheckGlobalShowCalibrationTracesAndPads.IsChecked = true;

        this.HideLabelEditorMenu();
        this.UpdateInteractiveCadTraceHoverModeUi();
        this.SchematicsContainer.Focus();
        this.Focus();
        this.RefreshKiCadOverlay(forceImmediate: true);
        this.UpdateSchematicsHoverUi(new Point(0, 0));

        Logger.Info($"KiCad trace calibration mode enabled for schematic [{this.GetCurrentSchematicName()}]");
    }

    // ###########################################################################################
    // Cancels the current interactive KiCad trace calibration session and restores the persisted
    // calibration without writing anything to disk.
    // ###########################################################################################
    private void CancelKiCadTraceCalibrationMode()
    {
        this.thisIsKiCadTraceCalibrationMode = false;
        this.thisKiCadTraceCalibrationDragMode = LabelEditorDragMode.None;
        this.thisKiCadCalibrationImageLeft = 0.0;
        this.thisKiCadCalibrationImageTop = 0.0;
        this.thisKiCadCalibrationImageRight = 0.0;
        this.thisKiCadCalibrationImageBottom = 0.0;
        this.thisKiCadCalibrationStartImageLeft = 0.0;
        this.thisKiCadCalibrationStartImageTop = 0.0;
        this.thisKiCadCalibrationStartImageRight = 0.0;
        this.thisKiCadCalibrationStartImageBottom = 0.0;

        this.CheckGlobalShowCalibrationTracesAndPads.IsChecked = true;

        this.HideLabelEditorMenu();
        this.UpdateInteractiveCadTraceHoverModeUi();
        this.RefreshKiCadOverlay(forceImmediate: true);
        this.SchematicsContainer.Focus();

        Logger.Info("KiCad trace calibration mode canceled");
    }

    // ###########################################################################################
    // Saves the current interactive KiCad trace calibration box into the board JSON file and then
    // exits calibration mode so the persisted transform becomes the active transform immediately.
    // ###########################################################################################
    private void ApplyKiCadTraceCalibration()
    {
        if (!this.thisIsKiCadTraceCalibrationMode || this.currentFullResBitmap == null)
        {
            return;
        }

        string schematicName = this.GetCurrentSchematicName();
        string excelPath = this.MainWindow?.GetCurrentBoardExcelPath() ?? string.Empty;
        string cadName = this.schematicByName.TryGetValue(schematicName, out var entry)
            ? entry.CadName?.Trim() ?? string.Empty
            : string.Empty;

        if (string.IsNullOrWhiteSpace(excelPath) || string.IsNullOrWhiteSpace(schematicName))
        {
            return;
        }

        double left = Math.Min(this.thisKiCadCalibrationImageLeft, this.thisKiCadCalibrationImageRight);
        double right = Math.Max(this.thisKiCadCalibrationImageLeft, this.thisKiCadCalibrationImageRight);
        double top = Math.Min(this.thisKiCadCalibrationImageTop, this.thisKiCadCalibrationImageBottom);
        double bottom = Math.Max(this.thisKiCadCalibrationImageTop, this.thisKiCadCalibrationImageBottom);

        bool mirrorX = this.thisKiCadCalibrationImageLeft > this.thisKiCadCalibrationImageRight;
        bool mirrorY = this.thisKiCadCalibrationImageTop > this.thisKiCadCalibrationImageBottom;

        double scaleX = (right - left) / this.currentFullResBitmap.PixelSize.Width;
        double scaleY = (bottom - top) / this.currentFullResBitmap.PixelSize.Height;
        double offsetX = left;
        double offsetY = top;

        BoardComponentHighlightStorage.SaveKiCadCalibration(
            excelPath,
            schematicName,
            cadName,
            offsetX,
            offsetY,
            scaleX,
            scaleY,
            mirrorX,
            mirrorY);

        this.thisIsKiCadTraceCalibrationMode = false;
        this.thisKiCadTraceCalibrationDragMode = LabelEditorDragMode.None;
        this.CheckGlobalShowCalibrationTracesAndPads.IsChecked = true;

        this.HideLabelEditorMenu();
        this.UpdateInteractiveCadTraceHoverModeUi();
        this.RefreshKiCadOverlay(forceImmediate: true);
        this.SchematicsContainer.Focus();

        Logger.Info(
            $"KiCad trace calibration saved for schematic [{schematicName}] " +
            $"OffsetX=[{offsetX.ToString("0.######", CultureInfo.InvariantCulture)}] " +
            $"OffsetY=[{offsetY.ToString("0.######", CultureInfo.InvariantCulture)}] " +
            $"ScaleX=[{scaleX.ToString("0.######", CultureInfo.InvariantCulture)}] " +
            $"ScaleY=[{scaleY.ToString("0.######", CultureInfo.InvariantCulture)}] " +
            $"MirrorX=[{mirrorX}] MirrorY=[{mirrorY}]");
    }

    // ###########################################################################################
    // Builds the current KiCad calibration box in image-pixel coordinates by mapping the active
    // KiCad view bounds through the currently active calibration.
    // ###########################################################################################
    private Rect BuildKiCadCalibrationImageBounds(KiCadProjectView view)
    {
        if (this.currentFullResBitmap == null)
        {
            return default;
        }

        Rect worldBounds;
        string currentSchematicName = this.GetCurrentSchematicName();

        if (string.Equals(view.Type, "pcb_top", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(view.Type, "pcb_bottom", StringComparison.OrdinalIgnoreCase))
        {
            if (view.SourceIndex < 0 || view.SourceIndex >= this.thisKiCadProject!.Root.Pcb.Count)
            {
                return new Rect(0, 0, this.currentFullResBitmap.PixelSize.Width, this.currentFullResBitmap.PixelSize.Height);
            }

            worldBounds = this.GetKiCadPcbWorldBounds(this.thisKiCadProject.Root.Pcb[view.SourceIndex]);
        }
        else
        {
            if (view.SourceIndex < 0 || view.SourceIndex >= this.thisKiCadProject!.Root.Schematics.Count)
            {
                return new Rect(0, 0, this.currentFullResBitmap.PixelSize.Width, this.currentFullResBitmap.PixelSize.Height);
            }

            worldBounds = this.GetKiCadSchematicWorldBounds(this.thisKiCadProject.Root.Schematics[view.SourceIndex]);
        }

        if (worldBounds.Width <= 0 || worldBounds.Height <= 0)
        {
            return new Rect(0, 0, this.currentFullResBitmap.PixelSize.Width, this.currentFullResBitmap.PixelSize.Height);
        }

        var calibration = KiCadViewCalibration.Identity;

        string excelPath = this.MainWindow?.GetCurrentBoardExcelPath() ?? string.Empty;
        if (BoardComponentHighlightStorage.TryLoadKiCadCalibration(
                excelPath,
                currentSchematicName,
                out _,
                out double offsetX,
                out double offsetY,
                out double scaleX,
                out double scaleY,
                out bool mirrorX,
                out bool mirrorY))
        {
            calibration = new KiCadViewCalibration
            {
                OffsetX = offsetX,
                OffsetY = offsetY,
                ScaleX = scaleX,
                ScaleY = scaleY,
                MirrorX = mirrorX,
                MirrorY = mirrorY
            };
        }

        Point topLeft = this.MapKiCadWorldToImagePixel(worldBounds.Left, worldBounds.Top, worldBounds, calibration);
        Point topRight = this.MapKiCadWorldToImagePixel(worldBounds.Right, worldBounds.Top, worldBounds, calibration);
        Point bottomLeft = this.MapKiCadWorldToImagePixel(worldBounds.Left, worldBounds.Bottom, worldBounds, calibration);
        Point bottomRight = this.MapKiCadWorldToImagePixel(worldBounds.Right, worldBounds.Bottom, worldBounds, calibration);

        double left = new[] { topLeft.X, topRight.X, bottomLeft.X, bottomRight.X }.Min();
        double right = new[] { topLeft.X, topRight.X, bottomLeft.X, bottomRight.X }.Max();
        double top = new[] { topLeft.Y, topRight.Y, bottomLeft.Y, bottomRight.Y }.Min();
        double bottom = new[] { topLeft.Y, topRight.Y, bottomLeft.Y, bottomRight.Y }.Max();

        return new Rect(left, top, Math.Max(1.0, right - left), Math.Max(1.0, bottom - top));
    }

    // ###########################################################################################
    // Maps one KiCad world coordinate directly into image-pixel coordinates using the current
    // non-affine box calibration model.
    // ###########################################################################################
    private Point MapKiCadWorldToImagePixel(
        double worldX,
        double worldY,
        Rect worldBounds,
        KiCadViewCalibration calibration)
    {
        if (this.currentFullResBitmap == null || worldBounds.Width <= 0 || worldBounds.Height <= 0)
        {
            return default;
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

        double imageX = calibration.OffsetX + (nx * calibration.ScaleX * this.currentFullResBitmap.PixelSize.Width);
        double imageY = calibration.OffsetY + (ny * calibration.ScaleY * this.currentFullResBitmap.PixelSize.Height);

        return new Point(imageX, imageY);
    }

    // ###########################################################################################
    // Converts an image-pixel rectangle into schematic-local coordinates so the calibration border
    // can be drawn on top of the current image using the same mapping as other overlays.
    // ###########################################################################################
    private Rect ConvertImagePixelRectToLocalRect(Rect imagePixelRect)
    {
        if (this.currentFullResBitmap == null ||
            this.currentFullResBitmap.PixelSize.Width <= 0 ||
            this.currentFullResBitmap.PixelSize.Height <= 0)
        {
            return default;
        }

        var contentRect = this.GetImageContentRect();

        double x = contentRect.X + ((imagePixelRect.X / this.currentFullResBitmap.PixelSize.Width) * contentRect.Width);
        double y = contentRect.Y + ((imagePixelRect.Y / this.currentFullResBitmap.PixelSize.Height) * contentRect.Height);
        double width = (imagePixelRect.Width / this.currentFullResBitmap.PixelSize.Width) * contentRect.Width;
        double height = (imagePixelRect.Height / this.currentFullResBitmap.PixelSize.Height) * contentRect.Height;

        return new Rect(x, y, width, height);
    }

    // ###########################################################################################
    // Builds the visible KiCad calibration border box and explicit corner/side handle markers so the
    // user can see where resize interaction is available while aligning the temporary KiCad overlay.
    // The border is drawn slightly outside the actual KiCad data bounds to avoid covering details.
    // ###########################################################################################
    private KiCadOverlayPrimitive BuildKiCadCalibrationBoxPrimitive()
    {
        Rect thisBorderImageRect = this.GetKiCadCalibrationBorderImageRect();
        Rect thisLocalRect = this.ConvertImagePixelRectToLocalRect(thisBorderImageRect);

        double thisScale = Math.Max(0.0001, this.schematicsMatrix.M11);
        double thisHandleSize = Math.Clamp(10.0 / thisScale, 5.0, 12.0);
        double thisHalfHandleSize = thisHandleSize / 2.0;

        var thisHandleBrush = new SolidColorBrush(Colors.LimeGreen, 1.0);
        var thisHandlePen = new Pen(thisHandleBrush, 1.0);
        var thisBorderPen = new Pen(thisHandleBrush, 1.0);

        var thisPrimitives = new List<KiCadOverlayPrimitive>
    {
        new KiCadOverlayPrimitive
        {
            Kind = KiCadOverlayPrimitiveKind.Rectangle,
            Rect = thisLocalRect,
            Pen = thisBorderPen,
            Fill = null
        }
    };

        var thisHandleCenters = new[]
        {
        new Point(thisLocalRect.Left, thisLocalRect.Top),
        new Point(thisLocalRect.Center.X, thisLocalRect.Top),
        new Point(thisLocalRect.Right, thisLocalRect.Top),
        new Point(thisLocalRect.Right, thisLocalRect.Center.Y),
        new Point(thisLocalRect.Right, thisLocalRect.Bottom),
        new Point(thisLocalRect.Center.X, thisLocalRect.Bottom),
        new Point(thisLocalRect.Left, thisLocalRect.Bottom),
        new Point(thisLocalRect.Left, thisLocalRect.Center.Y)
    };

        foreach (var thisHandleCenter in thisHandleCenters)
        {
            thisPrimitives.Add(new KiCadOverlayPrimitive
            {
                Kind = KiCadOverlayPrimitiveKind.Rectangle,
                Rect = new Rect(
                    thisHandleCenter.X - thisHalfHandleSize,
                    thisHandleCenter.Y - thisHalfHandleSize,
                    thisHandleSize,
                    thisHandleSize),
                Pen = thisHandlePen,
                Fill = thisHandleBrush
            });
        }

        var thisGeometry = new StreamGeometry();

        using (var thisGeometryContext = thisGeometry.Open())
        {
            foreach (var thisPrimitive in thisPrimitives)
            {
                if (thisPrimitive.Kind != KiCadOverlayPrimitiveKind.Rectangle)
                {
                    continue;
                }

                thisGeometryContext.BeginFigure(thisPrimitive.Rect.TopLeft, isFilled: thisPrimitive.Fill != null);
                thisGeometryContext.LineTo(thisPrimitive.Rect.TopRight);
                thisGeometryContext.LineTo(thisPrimitive.Rect.BottomRight);
                thisGeometryContext.LineTo(thisPrimitive.Rect.BottomLeft);
                thisGeometryContext.EndFigure(isClosed: true);
            }
        }

        return new KiCadOverlayPrimitive
        {
            Kind = KiCadOverlayPrimitiveKind.Geometry,
            Geometry = thisGeometry,
            Pen = thisBorderPen,
            Fill = null
        };
    }

    // ###########################################################################################
    // Returns the current interactive calibration rectangle in image-pixel coordinates.
    // Left can be greater than right and top can be greater than bottom so flip state is preserved.
    // ###########################################################################################
/*
        private Rect GetCurrentKiCadCalibrationImageRect()
        {
            double left = Math.Min(this.thisKiCadCalibrationImageLeft, this.thisKiCadCalibrationImageRight);
            double right = Math.Max(this.thisKiCadCalibrationImageLeft, this.thisKiCadCalibrationImageRight);
            double top = Math.Min(this.thisKiCadCalibrationImageTop, this.thisKiCadCalibrationImageBottom);
            double bottom = Math.Max(this.thisKiCadCalibrationImageTop, this.thisKiCadCalibrationImageBottom);

            return new Rect(left, top, Math.Max(1.0, right - left), Math.Max(1.0, bottom - top));
        }
*/

    // ###########################################################################################
    // Returns true when the pointer is inside the currently visible KiCad calibration rectangle.
    // This is used for move-drag behavior while calibration mode is active.
    // ###########################################################################################
    private bool IsPointerInsideCurrentKiCadCalibrationBounds(Point pointerInContainer)
    {
        if (!this.thisIsKiCadTraceCalibrationMode)
        {
            return false;
        }

        if (!this.TryGetSchematicsImagePixelPoint(pointerInContainer, out var pixelPoint))
        {
            return false;
        }

        double left = Math.Min(this.thisKiCadCalibrationImageLeft, this.thisKiCadCalibrationImageRight);
        double right = Math.Max(this.thisKiCadCalibrationImageLeft, this.thisKiCadCalibrationImageRight);
        double top = Math.Min(this.thisKiCadCalibrationImageTop, this.thisKiCadCalibrationImageBottom);
        double bottom = Math.Max(this.thisKiCadCalibrationImageTop, this.thisKiCadCalibrationImageBottom);

        return pixelPoint.X >= left &&
               pixelPoint.X <= right &&
               pixelPoint.Y >= top &&
               pixelPoint.Y <= bottom;
    }

    // ###########################################################################################
    // Builds the visual calibration-border rectangle in image-pixel space.
    // The border is intentionally expanded slightly outside the actual KiCad data bounds so the
    // visible box and handles do not sit directly on top of traces and pads.
    // ###########################################################################################
    private Rect GetKiCadCalibrationBorderImageRect()
    {
        const double thisBorderPaddingPixels = 10.0;

        double thisLeft = Math.Min(this.thisKiCadCalibrationImageLeft, this.thisKiCadCalibrationImageRight);
        double thisRight = Math.Max(this.thisKiCadCalibrationImageLeft, this.thisKiCadCalibrationImageRight);
        double thisTop = Math.Min(this.thisKiCadCalibrationImageTop, this.thisKiCadCalibrationImageBottom);
        double thisBottom = Math.Max(this.thisKiCadCalibrationImageTop, this.thisKiCadCalibrationImageBottom);

        double thisExpandedLeft = thisLeft - thisBorderPaddingPixels;
        double thisExpandedTop = thisTop - thisBorderPaddingPixels;
        double thisExpandedRight = thisRight + thisBorderPaddingPixels;
        double thisExpandedBottom = thisBottom + thisBorderPaddingPixels;

        if (this.currentFullResBitmap != null)
        {
            thisExpandedLeft = Math.Clamp(thisExpandedLeft, 0.0, this.currentFullResBitmap.PixelSize.Width);
            thisExpandedTop = Math.Clamp(thisExpandedTop, 0.0, this.currentFullResBitmap.PixelSize.Height);
            thisExpandedRight = Math.Clamp(thisExpandedRight, 0.0, this.currentFullResBitmap.PixelSize.Width);
            thisExpandedBottom = Math.Clamp(thisExpandedBottom, 0.0, this.currentFullResBitmap.PixelSize.Height);
        }

        return new Rect(
            thisExpandedLeft,
            thisExpandedTop,
            Math.Max(1.0, thisExpandedRight - thisExpandedLeft),
            Math.Max(1.0, thisExpandedBottom - thisExpandedTop));
    }

    // ###########################################################################################
    // Tries to resolve which KiCad calibration resize handle is under the pointer so the box can
    // be resized from edges or corners and flipped naturally by dragging across opposite sides.
    // Hit-testing uses the expanded visual border rectangle so the handles match what is drawn.
    // ###########################################################################################
    private bool TryGetKiCadTraceCalibrationHandleAtContainerPoint(
        Point pointerInContainer,
        out LabelEditorDragMode dragMode)
    {
        dragMode = LabelEditorDragMode.None;

        if (!this.thisIsKiCadTraceCalibrationMode ||
            this.currentFullResBitmap == null)
        {
            return false;
        }

        if (!TryInvert(this.schematicsMatrix, out var thisInverseMatrix))
        {
            return false;
        }

        var thisLocalPoint = new Point(
            (pointerInContainer.X * thisInverseMatrix.M11) + (pointerInContainer.Y * thisInverseMatrix.M21) + thisInverseMatrix.M31,
            (pointerInContainer.X * thisInverseMatrix.M12) + (pointerInContainer.Y * thisInverseMatrix.M22) + thisInverseMatrix.M32);

        var thisContentRect = this.GetImageContentRect();
        if (thisContentRect.Width <= 0 || thisContentRect.Height <= 0 || !thisContentRect.Contains(thisLocalPoint))
        {
            return false;
        }

        Rect thisBorderImageRect = this.GetKiCadCalibrationBorderImageRect();
        Rect thisLocalRect = this.ConvertImagePixelRectToLocalRect(thisBorderImageRect);
        double thisScale = Math.Max(0.0001, this.schematicsMatrix.M11);

        foreach (var thisHitTarget in BuildLabelEditorHandleHitRects(thisLocalRect, thisScale))
        {
            if (!thisHitTarget.HitRect.Contains(thisLocalPoint))
            {
                continue;
            }

            dragMode = thisHitTarget.DragMode;
            return true;
        }

        return false;
    }

    // ###########################################################################################
    // Remaps a visually hit KiCad calibration handle to the underlying stored edge/corner definition.
    // This keeps resize behavior correct after horizontal and/or vertical flips, because the visible
    // top-left corner may no longer correspond to the stored left/top values.
    // ###########################################################################################
    private LabelEditorDragMode RemapKiCadTraceCalibrationDragModeForCurrentFlip(LabelEditorDragMode dragMode)
    {
        bool thisIsMirroredX = this.thisKiCadCalibrationImageLeft > this.thisKiCadCalibrationImageRight;
        bool thisIsMirroredY = this.thisKiCadCalibrationImageTop > this.thisKiCadCalibrationImageBottom;

        if (!thisIsMirroredX && !thisIsMirroredY)
        {
            return dragMode;
        }

        return dragMode switch
        {
            LabelEditorDragMode.ResizeTopLeft => thisIsMirroredX
                ? thisIsMirroredY
                    ? LabelEditorDragMode.ResizeBottomRight
                    : LabelEditorDragMode.ResizeTopRight
                : thisIsMirroredY
                    ? LabelEditorDragMode.ResizeBottomLeft
                    : LabelEditorDragMode.ResizeTopLeft,

            LabelEditorDragMode.ResizeTop => thisIsMirroredY
                ? LabelEditorDragMode.ResizeBottom
                : LabelEditorDragMode.ResizeTop,

            LabelEditorDragMode.ResizeTopRight => thisIsMirroredX
                ? thisIsMirroredY
                    ? LabelEditorDragMode.ResizeBottomLeft
                    : LabelEditorDragMode.ResizeTopLeft
                : thisIsMirroredY
                    ? LabelEditorDragMode.ResizeBottomRight
                    : LabelEditorDragMode.ResizeTopRight,

            LabelEditorDragMode.ResizeRight => thisIsMirroredX
                ? LabelEditorDragMode.ResizeLeft
                : LabelEditorDragMode.ResizeRight,

            LabelEditorDragMode.ResizeBottomRight => thisIsMirroredX
                ? thisIsMirroredY
                    ? LabelEditorDragMode.ResizeTopLeft
                    : LabelEditorDragMode.ResizeBottomLeft
                : thisIsMirroredY
                    ? LabelEditorDragMode.ResizeTopRight
                    : LabelEditorDragMode.ResizeBottomRight,

            LabelEditorDragMode.ResizeBottom => thisIsMirroredY
                ? LabelEditorDragMode.ResizeTop
                : LabelEditorDragMode.ResizeBottom,

            LabelEditorDragMode.ResizeBottomLeft => thisIsMirroredX
                ? thisIsMirroredY
                    ? LabelEditorDragMode.ResizeTopRight
                    : LabelEditorDragMode.ResizeBottomRight
                : thisIsMirroredY
                    ? LabelEditorDragMode.ResizeTopLeft
                    : LabelEditorDragMode.ResizeBottomLeft,

            LabelEditorDragMode.ResizeLeft => thisIsMirroredX
                ? LabelEditorDragMode.ResizeRight
                : LabelEditorDragMode.ResizeLeft,

            _ => dragMode
        };
    }

    // ###########################################################################################
    // Starts a KiCad calibration move or resize drag by capturing both the pointer start pixel and
    // the current box edges so drag updates remain stable and do not accumulate rounding drift.
    // Visual resize handles are remapped to the stored flipped edge/corner definition first.
    // ###########################################################################################
    private void StartKiCadTraceCalibrationDrag(Point startPixelPoint, LabelEditorDragMode dragMode)
    {
        this.thisKiCadTraceCalibrationDragMode =
            dragMode == LabelEditorDragMode.Move
                ? LabelEditorDragMode.Move
                : this.RemapKiCadTraceCalibrationDragModeForCurrentFlip(dragMode);

        this.thisKiCadTraceCalibrationDragStartPixelPoint = startPixelPoint;
        this.thisKiCadCalibrationStartImageLeft = this.thisKiCadCalibrationImageLeft;
        this.thisKiCadCalibrationStartImageTop = this.thisKiCadCalibrationImageTop;
        this.thisKiCadCalibrationStartImageRight = this.thisKiCadCalibrationImageRight;
        this.thisKiCadCalibrationStartImageBottom = this.thisKiCadCalibrationImageBottom;
    }

    // ###########################################################################################
    // Updates the temporary KiCad calibration box during drag. Moving preserves the box size while
    // resize modes allow edge crossing so horizontal and vertical flipping happen automatically.
    // ###########################################################################################
    private void UpdateKiCadTraceCalibrationDrag(Point currentPixelPoint)
    {
        if (!this.thisIsKiCadTraceCalibrationMode ||
            this.thisKiCadTraceCalibrationDragMode == LabelEditorDragMode.None)
        {
            return;
        }

        double dx = currentPixelPoint.X - this.thisKiCadTraceCalibrationDragStartPixelPoint.X;
        double dy = currentPixelPoint.Y - this.thisKiCadTraceCalibrationDragStartPixelPoint.Y;

        double left = this.thisKiCadCalibrationStartImageLeft;
        double top = this.thisKiCadCalibrationStartImageTop;
        double right = this.thisKiCadCalibrationStartImageRight;
        double bottom = this.thisKiCadCalibrationStartImageBottom;

        switch (this.thisKiCadTraceCalibrationDragMode)
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

        this.thisKiCadCalibrationImageLeft = left;
        this.thisKiCadCalibrationImageTop = top;
        this.thisKiCadCalibrationImageRight = right;
        this.thisKiCadCalibrationImageBottom = bottom;

        this.RefreshKiCadOverlay(forceImmediate: true);
    }

    // ###########################################################################################
    // Completes the active KiCad calibration drag and clears the transient drag mode so the overlay
    // returns to idle calibration interaction state.
    // ###########################################################################################
    private void CompleteKiCadTraceCalibrationDrag()
    {
        this.thisKiCadTraceCalibrationDragMode = LabelEditorDragMode.None;
    }

    // ###########################################################################################
    // Updates the cursor while KiCad trace calibration mode is active so resize handles and move
    // areas feel consistent with the component label editor interactions.
    // ###########################################################################################
    private void UpdateKiCadTraceCalibrationCursor(Point pointerInContainer)
    {
        if (!this.thisIsKiCadTraceCalibrationMode)
        {
            return;
        }

        if (this.thisKiCadTraceCalibrationDragMode != LabelEditorDragMode.None)
        {
            this.SchematicsContainer.Cursor = this.thisKiCadTraceCalibrationDragMode == LabelEditorDragMode.Move
                ? new Cursor(StandardCursorType.SizeAll)
                : new Cursor(StandardCursorType.Hand);
            return;
        }

        if (this.TryGetKiCadTraceCalibrationHandleAtContainerPoint(pointerInContainer, out _))
        {
            this.SchematicsContainer.Cursor = new Cursor(StandardCursorType.Hand);
            return;
        }

        if (this.IsPointerInsideCurrentKiCadCalibrationBounds(pointerInContainer))
        {
            this.SchematicsContainer.Cursor = new Cursor(StandardCursorType.SizeAll);
            return;
        }

        this.SchematicsContainer.Cursor = Cursor.Default;
    }




}