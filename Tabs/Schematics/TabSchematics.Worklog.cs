using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Handlers.DataHandling;
using Handlers.Geometry;
using Tabs.TabSchematics;

namespace CRT;

// ###########################################################################################
// Worklog "Add entry" area-marking mode: drag-draw a rectangle on the board to mark where a
// fault is, then show the "New fault" quick card anchored near it, pre-populated with every
// component whose highlight rectangle intersects the drawn area. Cancel just dismisses the
// card and exits the mode; Save persists the entry via WorklogManager.AddEntry and then does
// the same.
//
// Part of the TabSchematics partial class - see TabSchematics.axaml.cs for the tab overview.
// ###########################################################################################
public partial class TabSchematics
{
    private bool thisIsWorklogEntryMode;

    private bool thisIsDrawingWorklogEntryRectangle;

    private Point thisWorklogEntryDrawStartPixelPoint;

    private Rect? thisWorklogEntryDraftRectangle;

    private Rect? thisWorklogEntryFinalRectangle;

    private int thisWorklogEntryWorkbookId;

    // ###########################################################################################
    // The id AddEntry will hand the entry currently being drawn, resolved once when entry mode
    // starts and reused for the on-board badge and the card header.
    //
    // Not looked up per draw: WorklogManager.PeekNextEntryId reads and re-parses entries.json, and
    // RefreshWorklogEntryBadge runs from UpdateSchematicsTransform on every zoom/pan frame - so
    // asking it there did synchronous disk I/O at frame rate for a number that cannot change while
    // the mode is open. The sibling RescaleWorklogEntriesListBadges avoids the same trap by design.
    // ###########################################################################################
    private int thisWorklogEntryNextId = 1;

    private readonly ObservableCollection<WorklogEntryComponentRow> thisWorklogEntryComponentRows = new();

    private Border? thisWorklogEntryBadgeBorder;

    private ScaleTransform? thisWorklogEntryBadgeScaleTransform;

    // ###########################################################################################
    // Which corner of the drawn entry area the on-board "#N" badge sits at - always the corner
    // diagonally opposite the one the "New fault" card is anchored to, so the two never overlap.
    // ###########################################################################################
    private RectCorner thisWorklogEntryBadgeCorner = RectCorner.TopLeft;

    private const string WorklogCategoryNote = "Note";
    private const string WorklogCategoryCosmetic = "Cosmetic";
    private const string WorklogCategoryIssue = "Issue";

    // ###########################################################################################
    // The category chosen for the entry currently being drawn/edited. Drives both the selected
    // chip's visual state and the color of the marked-area boundary and its "#N" badge - resets
    // to WorklogCategoryNote every time a new entry card is opened.
    // ###########################################################################################
    private string thisWorklogEntrySelectedCategory = WorklogCategoryNote;

    private const string WorklogStateOpen = "Open";
    private const string WorklogStateClosed = "Closed";

    // ###########################################################################################
    // The state chosen for the entry currently being drawn/edited. Drives the selected state
    // pill's outline/color - resets to WorklogStateOpen every time a new entry card is opened.
    // Open reuses the "Issue" category color - category and state are unrelated axes (fault kind
    // vs. resolution state) that happen to share a palette on purpose.
    // ###########################################################################################
    private string thisWorklogEntrySelectedState = WorklogStateOpen;

    // ###########################################################################################
    // Whether the top-bar "Show worklogs" checkbox is currently checked. Mirrors the checkbox
    // rather than owning the preference: its starting value comes from
    // UserSettings.WorklogShowEntriesChecked (which defaults to true), and Main.RefreshWorklogBar
    // re-seeds it whenever the workbook on screen changes.
    // ###########################################################################################
    private bool thisIsShowingWorklogEntriesList;

    private int thisWorklogEntriesListWorkbookId;

    // ###########################################################################################
    // One "#N" badge + state pill placed on SchematicsWorklogEntriesBadgeCanvas for the "Show
    // worklogs" list view - one row per saved entry currently rendered for this schematic. Rebuilt
    // from scratch (control tree torn down and recreated) only when the underlying entry set can
    // actually change: the checkbox being toggled, a schematic switch, or a new/edited entry being
    // saved. A zoom/pan tick instead calls RescaleWorklogEntriesListBadges, which repositions and
    // rescales these same controls in place - see that method for why a full rebuild on every
    // pointer-move frame was wasteful (re-reading entries.json and recreating every control).
    // ###########################################################################################
    private readonly List<(Border Badge, Rect PixelRect, WorklogEntryRecord Entry, Color Color)> thisWorklogEntriesListBadges = new();

    // ###########################################################################################
    // Enters worklog entry-drawing mode for the given active workbook. Refuses to start while the
    // label editor or KiCad calibration mode is active, or while no schematic image is loaded, so
    // the caller can tell whether the mode actually started.
    // ###########################################################################################
    public bool BeginWorklogEntryMode(int workbookId)
    {
        if (this.currentFullResBitmap == null || this.thisIsLabelEditorMode || this.thisIsKiCadTraceCalibrationMode)
        {
            return false;
        }

        this.thisIsWorklogEntryMode = true;
        this.thisWorklogEntryWorkbookId = workbookId;
        this.thisWorklogEntryNextId = WorklogManager.PeekNextEntryId(workbookId);
        this.thisIsDrawingWorklogEntryRectangle = false;
        this.thisWorklogEntryDraftRectangle = null;
        this.thisWorklogEntryFinalRectangle = null;

        this.HideNewWorklogEntryCard();
        this.RefreshWorklogEntryOverlay();

        this.SchematicsContainer.Cursor = new Cursor(StandardCursorType.Cross);
        this.SchematicsContainer.Focus();
        this.Focus();

        Logger.Info($"Worklog entry drawing mode enabled for workbook [#{workbookId}] on schematic [{this.GetCurrentSchematicName()}]");
        return true;
    }

    // ###########################################################################################
    // Exits worklog entry-drawing mode from any point in the flow (top-bar Cancel, Escape, or the
    // card's own close/Cancel/Save), clearing the drawn area and telling the main window to reset
    // the "Add entry" / "Cancel entry" buttons. Safe to call when the mode is not active.
    // ###########################################################################################
    public void CancelWorklogEntryMode()
    {
        if (!this.thisIsWorklogEntryMode)
        {
            return;
        }

        this.thisIsWorklogEntryMode = false;
        this.thisIsDrawingWorklogEntryRectangle = false;
        this.thisWorklogEntryDraftRectangle = null;
        this.thisWorklogEntryFinalRectangle = null;

        this.HideNewWorklogEntryCard();
        this.RefreshWorklogEntryOverlay();

        this.SchematicsContainer.Cursor = Cursor.Default;
        this.SchematicsContainer.Focus();

        this.MainWindow?.ResetWorklogEntryModeButtons();

        Logger.Info("Worklog entry drawing mode canceled");
    }

    // ###########################################################################################
    // Starts drawing the entry area rectangle from the current bitmap pixel position.
    // ###########################################################################################
    private void StartDrawingWorklogEntryRectangle(Point startPixelPoint)
    {
        this.thisIsDrawingWorklogEntryRectangle = true;
        this.thisWorklogEntryDrawStartPixelPoint = startPixelPoint;
        this.thisWorklogEntryDraftRectangle = new Rect(startPixelPoint.X, startPixelPoint.Y, 0, 0);
        this.thisWorklogEntryFinalRectangle = null;

        this.HideNewWorklogEntryCard();
        this.RefreshWorklogEntryOverlay();
    }

    // ###########################################################################################
    // Updates the draft entry area rectangle while the mouse is being dragged.
    // ###########################################################################################
    private void UpdateDrawingWorklogEntryRectangle(Point currentPixelPoint)
    {
        if (!this.thisIsDrawingWorklogEntryRectangle)
        {
            return;
        }

        this.thisWorklogEntryDraftRectangle = RectGeometry.CreateNormalizedRect(
            this.thisWorklogEntryDrawStartPixelPoint,
            currentPixelPoint);

        this.RefreshWorklogEntryOverlay();
    }

    // ###########################################################################################
    // Finishes drawing the entry area rectangle and opens the "New fault" card, anchored against
    // the drawn area's own bounds rather than the release point. A rectangle too small to be a
    // deliberate drag (an accidental click) is discarded instead, the same threshold the label
    // editor uses for the same reason.
    // ###########################################################################################
    private void CompleteDrawingWorklogEntryRectangle(Point releasePixelPoint)
    {
        if (!this.thisIsDrawingWorklogEntryRectangle)
        {
            return;
        }

        var finalRect = RectGeometry.CreateNormalizedRect(this.thisWorklogEntryDrawStartPixelPoint, releasePixelPoint);

        this.thisIsDrawingWorklogEntryRectangle = false;
        this.thisWorklogEntryDraftRectangle = null;

        if (LabelEditorGeometry.IsLabelEditorRectangleTooSmall(finalRect))
        {
            this.RefreshWorklogEntryOverlay();
            return;
        }

        this.thisWorklogEntryFinalRectangle = finalRect;
        this.RefreshWorklogEntryOverlay();
        this.ShowNewWorklogEntryCard();
    }

    // ###########################################################################################
    // Pushes the current draft/final rectangle into the shared overlay control. The final
    // rectangle is rendered as both "selected" and "hovered" so ComponentLabelEditorOverlay draws
    // its corner+side marker handles around it, matching the mockup's marked-area look.
    // ###########################################################################################
    private void RefreshWorklogEntryOverlay()
    {
        Color categoryColor = this.GetSelectedWorklogEntryCategoryColor();

        if (!this.thisIsWorklogEntryMode || this.currentFullResBitmap == null)
        {
            this.SchematicsWorklogEntryOverlay.ApplyState(
                rectangles: Array.Empty<Rect>(),
                selectedIndex: -1,
                selectedIndices: Array.Empty<int>(),
                selectionBounds: null,
                hoveredIndex: -1,
                draftRectangle: null,
                snapGuides: Array.Empty<(Point Start, Point End)>(),
                bitmapPixelSize: this.currentFullResBitmap?.PixelSize ?? new PixelSize(0, 0),
                viewMatrix: this.schematicsMatrix,
                highlightColor: categoryColor,
                highlightOpacity: 0.12,
                isVisible: false);
            this.RefreshWorklogEntryBadge(null);
            return;
        }

        var rectangles = this.thisWorklogEntryFinalRectangle.HasValue
            ? new[] { this.thisWorklogEntryFinalRectangle.Value }
            : Array.Empty<Rect>();

        this.SchematicsWorklogEntryOverlay.ApplyState(
            rectangles: rectangles,
            selectedIndex: rectangles.Length > 0 ? 0 : -1,
            selectedIndices: Array.Empty<int>(),
            selectionBounds: null,
            hoveredIndex: rectangles.Length > 0 ? 0 : -1,
            draftRectangle: this.thisWorklogEntryDraftRectangle,
            snapGuides: Array.Empty<(Point Start, Point End)>(),
            bitmapPixelSize: this.currentFullResBitmap.PixelSize,
            viewMatrix: this.schematicsMatrix,
            highlightColor: categoryColor,
            highlightOpacity: 0.12,
            isVisible: true);

        this.RefreshWorklogEntryBadge(this.thisWorklogEntryFinalRectangle);
    }

    // ###########################################################################################
    // Resolves the currently selected category's color - see ResolveWorklogCategoryColor for the
    // shared lookup (falls back to IndianRed if the resource cannot be resolved for any reason).
    // ###########################################################################################
    private Color GetSelectedWorklogEntryCategoryColor() => this.ResolveWorklogCategoryColor(this.thisWorklogEntrySelectedCategory);

    // ###########################################################################################
    // Shows a small "#N" badge at thisWorklogEntryBadgeCorner of the marked entry area - the
    // corner diagonally opposite wherever the "New fault" card is anchored, so it stays visible
    // which entry number a given marking on the board belongs to without the card covering it.
    // Kept a constant screen size across zoom the same way the standard component labels do: a
    // ScaleTransform set to the inverse of the current view scale.
    // ###########################################################################################
    private void RefreshWorklogEntryBadge(Rect? finalPixelRectangle)
    {
        if (!finalPixelRectangle.HasValue || this.currentFullResBitmap == null)
        {
            if (this.thisWorklogEntryBadgeBorder != null)
            {
                this.thisWorklogEntryBadgeBorder.IsVisible = false;
            }
            return;
        }

        if (this.thisWorklogEntryBadgeBorder == null)
        {
            var textBlock = new TextBlock
            {
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White
            };

            this.thisWorklogEntryBadgeBorder = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 3),
                IsHitTestVisible = false,
                Child = textBlock,
                RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative)
            };

            this.thisWorklogEntryBadgeScaleTransform = new ScaleTransform(1.0, 1.0);
            this.thisWorklogEntryBadgeBorder.RenderTransform = this.thisWorklogEntryBadgeScaleTransform;

            this.SchematicsWorklogEntryBadgeCanvas.Children.Add(this.thisWorklogEntryBadgeBorder);
        }

        this.thisWorklogEntryBadgeBorder.Background = new SolidColorBrush(this.GetSelectedWorklogEntryCategoryColor());

        var contentRect = this.GetImageContentRect();
        var localRect = RectGeometry.PixelToLocalRect(finalPixelRectangle.Value, contentRect, this.currentFullResBitmap.PixelSize);

        double scale = this.schematicsMatrix.M11;
        double inverseScale = scale > 0 ? 1.0 / scale : 1.0;

        ((TextBlock)this.thisWorklogEntryBadgeBorder.Child!).Text = $"#{this.thisWorklogEntryNextId}";
        this.thisWorklogEntryBadgeScaleTransform!.ScaleX = inverseScale;
        this.thisWorklogEntryBadgeScaleTransform!.ScaleY = inverseScale;

        this.thisWorklogEntryBadgeBorder.IsVisible = true;
        this.thisWorklogEntryBadgeBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        Size desiredSize = this.thisWorklogEntryBadgeBorder.DesiredSize * inverseScale;

        Point anchorPoint = this.thisWorklogEntryBadgeCorner switch
        {
            RectCorner.TopRight => new Point(localRect.Right, localRect.Top),
            RectCorner.BottomLeft => new Point(localRect.Left, localRect.Bottom),
            RectCorner.BottomRight => new Point(localRect.Right, localRect.Bottom),
            _ => new Point(localRect.Left, localRect.Top),
        };

        Canvas.SetLeft(this.thisWorklogEntryBadgeBorder, anchorPoint.X - (desiredSize.Width / 2.0));
        Canvas.SetTop(this.thisWorklogEntryBadgeBorder, anchorPoint.Y - (desiredSize.Height / 2.0));
    }

    // ###########################################################################################
    // How far the "New fault" card sits from the drawn entry area's edge on the side it sits
    // beside, so the two do not visually touch. See AnchoredCardPlacementGeometry's gap
    // parameter - only the horizontal axis is offset, the vertical stays a true edge match.
    // ###########################################################################################
    private const double WorklogEntryCardGap = 8.0;

    // ###########################################################################################
    // Shows the "New fault" card anchored against the drawn entry area's own bounds rather than
    // the mouse release point - the card always sits beside the area to its left or right, in one
    // of four corner placements, whichever has the most room (see AnchoredCardPlacementGeometry).
    // The on-board "#N" badge moves to the diagonally opposite corner so the two never overlap.
    // The card must already be measurable (IsVisible=true) before its real DesiredSize is known,
    // so content is filled in and it is made visible before placement is computed.
    //
    // The margin MUST be cleared before measuring: Avalonia's DesiredSize includes the control's
    // margin, so measuring while the previous placement's margin is still set reports a card that
    // is (previous X, previous Y) larger than it really is, and the next placement is computed
    // from that inflated size - which is what made the card appear to drift with the mouse.
    // ###########################################################################################
    private void ShowNewWorklogEntryCard()
    {
        this.WorklogEntryIdText.Text = $"#{this.thisWorklogEntryNextId}";
        this.WorklogEntryTitleTextBox.Text = string.Empty;
        this.WorklogEntryDescriptionTextBox.Text = string.Empty;
        this.thisWorklogEntrySelectedCategory = WorklogCategoryNote;
        this.UpdateWorklogEntryCategoryChipVisuals();
        this.thisWorklogEntrySelectedState = WorklogStateOpen;
        this.UpdateWorklogEntryStatePillVisuals();
        this.RefreshWorklogEntryComponentList();

        this.SchematicsNewWorklogEntryCardBorder.IsVisible = true;
        this.SchematicsNewWorklogEntryCardBorder.Margin = new Thickness(0);
        this.SchematicsNewWorklogEntryCardBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Size cardSize = this.SchematicsNewWorklogEntryCardBorder.DesiredSize;

        double x = 6.0;
        double y = 6.0;

        Rect? anchorRectInContainer = this.GetWorklogEntryAreaBoundsInContainer();
        if (anchorRectInContainer.HasValue)
        {
            var placement = AnchoredCardPlacementGeometry.ComputePlacement(
                anchorRectInContainer.Value,
                this.SchematicsContainer.Bounds.Size,
                cardSize,
                WorklogEntryCardGap);

            this.thisWorklogEntryBadgeCorner = placement.BadgeCorner;

            x = Math.Clamp(placement.CardTopLeft.X, 6.0, Math.Max(6.0, this.SchematicsContainer.Bounds.Width - cardSize.Width));
            y = Math.Clamp(placement.CardTopLeft.Y, 6.0, Math.Max(6.0, this.SchematicsContainer.Bounds.Height - cardSize.Height));
        }

        this.SchematicsNewWorklogEntryCardBorder.Margin = new Thickness(x, y, 0, 0);

        this.RefreshWorklogEntryOverlay();

        Dispatcher.UIThread.Post(() => this.WorklogEntryTitleTextBox.Focus(), DispatcherPriority.Background);
    }

    // ###########################################################################################
    // Returns the drawn entry area's bounds in SchematicsContainer's own coordinate space (the
    // space the card's Margin-based positioning uses), by asking Avalonia to translate the
    // area's local-space corners through the schematic view's actual render-transform chain -
    // robust to however that chain is structured, rather than re-deriving it from schematicsMatrix
    // by hand.
    // ###########################################################################################
    private Rect? GetWorklogEntryAreaBoundsInContainer()
    {
        if (this.currentFullResBitmap == null || !this.thisWorklogEntryFinalRectangle.HasValue)
        {
            return null;
        }

        var contentRect = this.GetImageContentRect();
        var localRect = RectGeometry.PixelToLocalRect(this.thisWorklogEntryFinalRectangle.Value, contentRect, this.currentFullResBitmap.PixelSize);

        Point? topLeft = this.SchematicsWorklogEntryOverlay.TranslatePoint(localRect.TopLeft, this.SchematicsContainer);
        Point? bottomRight = this.SchematicsWorklogEntryOverlay.TranslatePoint(localRect.BottomRight, this.SchematicsContainer);

        if (!topLeft.HasValue || !bottomRight.HasValue)
        {
            return null;
        }

        return new Rect(topLeft.Value, bottomRight.Value);
    }

    // ###########################################################################################
    // Hides the "New fault" card and clears its description text.
    // ###########################################################################################
    private void HideNewWorklogEntryCard()
    {
        this.SchematicsNewWorklogEntryCardBorder.IsVisible = false;
        this.WorklogEntryTitleTextBox.Text = string.Empty;
        this.WorklogEntryDescriptionTextBox.Text = string.Empty;
    }

    // ###########################################################################################
    // Selects a category chip - only one is selected at a time. Updates the chips' visuals and
    // re-colors the marked-area boundary and badge to match, via RefreshWorklogEntryOverlay.
    // ###########################################################################################
    private void OnWorklogEntryCategoryChipPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { Tag: string category })
        {
            this.thisWorklogEntrySelectedCategory = category;
            this.UpdateWorklogEntryCategoryChipVisuals();
            this.RefreshWorklogEntryOverlay();
        }
    }

    // ###########################################################################################
    // Restyles all three category chips to reflect thisWorklogEntrySelectedCategory: the selected
    // chip gets a filled background/border in its category color with white text, the rest show
    // their usual neutral outline. Also recolors the "#N" pill in the card header to match, the
    // same way the on-board badge does.
    //
    // Text only - the chips carry no colour dot. The selected chip is filled with its category
    // colour, which is what identifies the category; a dot would repeat that on the selected chip
    // and be the only colour on the unselected ones.
    // ###########################################################################################
    private void UpdateWorklogEntryCategoryChipVisuals()
    {
        this.ApplyWorklogCategoryChipVisualState(this.WorklogCategoryNoteChip, this.WorklogCategoryNoteText, WorklogCategoryNote);
        this.ApplyWorklogCategoryChipVisualState(this.WorklogCategoryCosmeticChip, this.WorklogCategoryCosmeticText, WorklogCategoryCosmetic);
        this.ApplyWorklogCategoryChipVisualState(this.WorklogCategoryIssueChip, this.WorklogCategoryIssueText, WorklogCategoryIssue);

        this.WorklogEntryIdBadge.Background = new SolidColorBrush(this.GetSelectedWorklogEntryCategoryColor());
    }

    private void ApplyWorklogCategoryChipVisualState(Border chip, TextBlock label, string category)
    {
        var categoryBrush = this.ResolveThemeBrush($"Worklog_Category_{category}", new SolidColorBrush(Colors.IndianRed));

        if (string.Equals(this.thisWorklogEntrySelectedCategory, category, StringComparison.Ordinal))
        {
            chip.Background = categoryBrush;
            chip.BorderBrush = categoryBrush;
            chip.BorderThickness = new Thickness(2);
            chip.Opacity = 0.9;
            label.Foreground = Brushes.White;
            label.FontWeight = FontWeight.SemiBold;
        }
        else
        {
            chip.Background = this.ResolveThemeBrush("Form_Bg", new SolidColorBrush(Color.Parse("#F5F5F5")));
            chip.BorderBrush = this.ResolveThemeBrush("Form_Border", new SolidColorBrush(Color.Parse("#CCCCCC")));
            chip.BorderThickness = new Thickness(1);
            chip.Opacity = 1.0;
            label.Foreground = this.ResolveThemeBrush("Schematics_Panels_Fg", Brushes.Black);
            label.FontWeight = FontWeight.Normal;
        }
    }

    // ###########################################################################################
    // Selects a state pill - only one is selected at a time, same one-of-three pattern as the
    // category chips above.
    // ###########################################################################################
    private void OnWorklogEntryStatePillPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { Tag: string state })
        {
            this.thisWorklogEntrySelectedState = state;
            this.UpdateWorklogEntryStatePillVisuals();
        }
    }

    // ###########################################################################################
    // Restyles both state pills to reflect thisWorklogEntrySelectedState: the selected pill
    // gets an outline and text in its state color, the other shows the usual neutral outline.
    // Unlike the category chips, the selected pill stays outline-only (no filled background) -
    // that is the visual distinction the maintainer asked for between category and state.
    //
    // The dot keeps its state color in BOTH pills, exactly as the category chips' dots do - it is
    // the state's identity, not a selection cue, so dimming it when unselected would read as a
    // different state rather than an unselected one.
    // ###########################################################################################
    private void UpdateWorklogEntryStatePillVisuals()
    {
        this.ApplyWorklogEntryStatePillVisualState(this.WorklogStateOpenPill, this.WorklogStateOpenText, WorklogStateOpen, "Worklog_Status_Open");
        this.ApplyWorklogEntryStatePillVisualState(this.WorklogStateClosedPill, this.WorklogStateClosedText, WorklogStateClosed, "Worklog_Status_Closed");
    }

    private void ApplyWorklogEntryStatePillVisualState(Border pill, TextBlock label, string state, string colorResourceKey)
    {
        var stateBrush = this.ResolveThemeBrush(colorResourceKey, new SolidColorBrush(Colors.IndianRed));

        if (string.Equals(this.thisWorklogEntrySelectedState, state, StringComparison.Ordinal))
        {
            pill.Background = this.ResolveThemeBrush("Schematics_Panels_Bg", new SolidColorBrush(Color.Parse("#F5F5F5")));
            pill.BorderBrush = stateBrush;
            pill.BorderThickness = new Thickness(2);
            label.Foreground = stateBrush;
            label.FontWeight = FontWeight.SemiBold;
        }
        else
        {
            pill.Background = this.ResolveThemeBrush("Form_Bg", new SolidColorBrush(Color.Parse("#F5F5F5")));
            pill.BorderBrush = this.ResolveThemeBrush("Form_Border", new SolidColorBrush(Color.Parse("#CCCCCC")));
            pill.BorderThickness = new Thickness(1);
            label.Foreground = this.ResolveThemeBrush("Schematics_Panels_Fg", Brushes.Black);
            label.FontWeight = FontWeight.Normal;
        }
    }

    // ###########################################################################################
    // Rebuilds the "Mark components in scope" checklist from the components whose highlight
    // rectangle intersects the drawn entry area, in the same order they appear in the board data
    // (the order the Overview tab lists them in too - neither view re-sorts). The actual
    // intersection test and row-building are pure Handlers/ helpers - see RectGeometry and
    // ComponentListBuilder - so this method is just wiring them up to the current schematic/board.
    // ###########################################################################################
    private void RefreshWorklogEntryComponentList()
    {
        this.thisWorklogEntryComponentRows.Clear();

        var boardData = this.MainWindow?.CurrentBoardData;
        string schematicName = this.GetCurrentSchematicName();

        if (boardData != null &&
            this.thisWorklogEntryFinalRectangle.HasValue &&
            !string.IsNullOrWhiteSpace(schematicName) &&
            this.highlightRectsBySchematicAndLabel.TryGetValue(schematicName, out var rectsByLabel))
        {
            var touchedLabels = RectGeometry.FindKeysWithRectsIntersecting(rectsByLabel, this.thisWorklogEntryFinalRectangle.Value);
            var componentsInScope = ComponentListBuilder.BuildComponentsInScope(boardData, touchedLabels);

            foreach (var component in componentsInScope)
            {
                this.thisWorklogEntryComponentRows.Add(new WorklogEntryComponentRow
                {
                    BoardLabel = component.BoardLabel,
                    DisplayName = component.DisplayName
                });
            }
        }

        this.WorklogEntryComponentCountText.Text = $"{this.thisWorklogEntryComponentRows.Count} found";
        this.WorklogEntryNoComponentsText.IsVisible = this.thisWorklogEntryComponentRows.Count == 0;
    }

    // ###########################################################################################
    // Builds the component scope for a SAVED entry, for the full editor's copy of the checklist.
    //
    // Returns null when the scope cannot be determined - no board data, or no highlight rectangles
    // loaded for that entry's schematic. Null and empty mean different things to the caller: empty
    // is "this area genuinely touches nothing", null is "unknown", and only the latter leaves the
    // entry's saved ComponentLabels untouched. Returning an empty list for an unknown scope would
    // wipe the user's selection the first time they saved.
    // ###########################################################################################
    private List<(string BoardLabel, string DisplayName)>? BuildWorklogEntryComponentScope(WorklogEntryRecord entry)
    {
        var boardData = this.MainWindow?.CurrentBoardData;
        if (boardData == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(entry.SchematicName) ||
            !this.highlightRectsBySchematicAndLabel.TryGetValue(entry.SchematicName, out var rectsByLabel))
        {
            return null;
        }

        var area = new Rect(entry.AreaX, entry.AreaY, entry.AreaWidth, entry.AreaHeight);
        var touchedLabels = RectGeometry.FindKeysWithRectsIntersecting(rectsByLabel, area);

        return ComponentListBuilder.BuildComponentsInScope(boardData, touchedLabels)
            .Select(c => (c.BoardLabel, c.DisplayName))
            .ToList();
    }

    // ###########################################################################################
    // "All" / "None" links above the checklist for quickly bulk-marking every touched component
    // as in or out of scope.
    // ###########################################################################################
    private void OnWorklogEntrySelectAllComponentsClick(object? sender, RoutedEventArgs e)
    {
        foreach (var row in this.thisWorklogEntryComponentRows)
        {
            row.IsChecked = true;
        }
    }

    private void OnWorklogEntrySelectNoneComponentsClick(object? sender, RoutedEventArgs e)
    {
        foreach (var row in this.thisWorklogEntryComponentRows)
        {
            row.IsChecked = false;
        }
    }

    // ###########################################################################################
    // Toggles a checklist row's checkbox when anywhere in its row is clicked, not just the
    // checkbox itself - the checkbox and its labels are IsHitTestVisible="False" so this Border
    // handler is the only thing that reacts to the click.
    // ###########################################################################################
    private void OnWorklogEntryComponentRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control && control.DataContext is WorklogEntryComponentRow row)
        {
            row.IsChecked = !row.IsChecked;
        }
    }

    // ###########################################################################################
    // Returns true when the pointer is currently inside the "New fault" card bounds, so schematic
    // panning/selection does not fire underneath it.
    // ###########################################################################################
    private bool IsPointerInsideWorklogEntryCard(Point containerPoint)
    {
        if (!this.SchematicsNewWorklogEntryCardBorder.IsVisible)
        {
            return false;
        }

        Point? translatedTopLeft = this.SchematicsNewWorklogEntryCardBorder.TranslatePoint(new Point(0, 0), this.SchematicsContainer);
        if (!translatedTopLeft.HasValue)
        {
            return false;
        }

        var cardRect = new Rect(translatedTopLeft.Value, this.SchematicsNewWorklogEntryCardBorder.Bounds.Size);
        return cardRect.Contains(containerPoint);
    }

    // ###########################################################################################
    // The card's Cancel button dismisses the card and exits entry mode without saving anything.
    // ###########################################################################################
    private void OnWorklogEntryCardDismissClick(object? sender, RoutedEventArgs e)
    {
        this.CancelWorklogEntryMode();
    }

    // ###########################################################################################
    // Persists the entry currently in the "New fault" card via WorklogManager.AddEntry, then
    // exits entry mode the same way Cancel does. A blank final rectangle (mode entered but no
    // area ever drawn) is defensive only - the card cannot be open without one, see
    // CompleteDrawingWorklogEntryRectangle. Refreshes the worklog bar afterwards so a workbook
    // that just auto-closed (see WorklogManager.AddEntry) is reflected immediately.
    // ###########################################################################################
    private void OnWorklogEntryCardSaveClick(object? sender, RoutedEventArgs e)
    {
        if (!this.thisWorklogEntryFinalRectangle.HasValue)
        {
            this.CancelWorklogEntryMode();
            return;
        }

        string title = this.WorklogEntryTitleTextBox.Text?.Trim() ?? string.Empty;
        string description = this.WorklogEntryDescriptionTextBox.Text?.Trim() ?? string.Empty;
        var componentLabels = this.thisWorklogEntryComponentRows
            .Where(row => row.IsChecked)
            .Select(row => row.BoardLabel);

        var savedEntry = WorklogManager.AddEntry(
            this.thisWorklogEntryWorkbookId,
            this.GetCurrentSchematicName(),
            this.thisWorklogEntryFinalRectangle.Value,
            title,
            description,
            this.thisWorklogEntrySelectedCategory,
            this.thisWorklogEntrySelectedState,
            componentLabels);

        if (savedEntry == null)
        {
            // Nothing was persisted (already logged by AddEntry): either the workbook's own folder
            // could not be found - most likely deleted from disk while the card was open - or the
            // write itself failed. Leave the card open with its typed content intact rather than
            // silently discarding what the user entered.
            return;
        }

        this.CancelWorklogEntryMode();
        this.MainWindow?.RefreshWorklogBar();
        this.RefreshWorklogEntriesListOverlay();
    }

    // ###########################################################################################
    // Turns the "Show worklogs" list view on or off for the given workbook - called by the
    // top-bar checkbox. Turning it on rebuilds the overlay for the schematic currently on screen;
    // turning it off clears both the colored-area overlay and its badges/pills.
    // ###########################################################################################
    public void SetShowWorklogEntriesList(bool isShowing, int workbookId)
    {
        this.thisIsShowingWorklogEntriesList = isShowing;
        this.thisWorklogEntriesListWorkbookId = workbookId;

        this.RefreshWorklogEntriesListOverlay();
    }

    // ###########################################################################################
    // Rebuilds the "Show worklogs" list view for the schematic currently on screen: every saved
    // entry whose SchematicName matches it, each drawn in its own category color, with a "#N"
    // badge and state pill anchored at its area's top-left. Entries are deliberately scoped to
    // the current schematic name (not the whole workbook) - a board can have entries recorded
    // against different schematics (e.g. top vs. bottom PCB side), and only the ones for what is
    // actually on screen should show.
    //
    // Also rebuilds every thumbnail's own state-pill overlay (ThumbnailWorklogPillsOverlay) from
    // the same workbook, one call covering both since they always change together - toggling
    // "Show worklogs" or switching workbook/board affects the main view and the thumbnails in the
    // same instant.
    // ###########################################################################################
    private void RefreshWorklogEntriesListOverlay()
    {
        foreach (var (badge, _, _, _) in this.thisWorklogEntriesListBadges)
        {
            this.SchematicsWorklogEntriesBadgeCanvas.Children.Remove(badge);
        }
        this.thisWorklogEntriesListBadges.Clear();

        // The hover label caches which entry it is painted for and skips repainting while that id
        // is unchanged. An entry edited in the full editor keeps its id, so without this the label
        // would keep showing the pre-edit title and state icon until the pointer moved to a
        // different pill. Resetting the appearance rather than only clearing the cached id matters:
        // the id records what is currently painted, so zeroing it while the label still carried an
        // entry's colors would make the next reset a no-op and strand those colors on the label.
        this.ResetSchematicsHoverLabelToDefaultAppearance();

        if (!this.thisIsShowingWorklogEntriesList || this.currentFullResBitmap == null)
        {
            this.SchematicsWorklogEntriesOverlay.IsVisible = false;
            this.SchematicsWorklogEntriesOverlay.Entries = Array.Empty<WorklogEntriesOverlay.Entry>();
            this.ClearThumbnailWorklogPills();
            return;
        }

        string schematicName = this.GetCurrentSchematicName();
        var allEntries = WorklogManager.GetEntries(this.thisWorklogEntriesListWorkbookId);
        var entries = allEntries
            .Where(entry => string.Equals(entry.SchematicName, schematicName, StringComparison.Ordinal))
            .ToList();

        var contentRect = this.GetImageContentRect();
        double scale = this.schematicsMatrix.M11;
        double inverseScale = scale > 0 ? 1.0 / scale : 1.0;

        var overlayEntries = new List<WorklogEntriesOverlay.Entry>(entries.Count);

        foreach (var entry in entries)
        {
            var pixelRect = new Rect(entry.AreaX, entry.AreaY, entry.AreaWidth, entry.AreaHeight);
            Color color = this.ResolveWorklogCategoryColor(entry.Category);

            overlayEntries.Add(new WorklogEntriesOverlay.Entry(pixelRect, color));

            this.CreateWorklogEntriesListBadge(entry, color, pixelRect, contentRect, inverseScale);
        }

        this.SchematicsWorklogEntriesOverlay.BitmapPixelSize = this.currentFullResBitmap.PixelSize;
        this.SchematicsWorklogEntriesOverlay.ViewMatrix = this.schematicsMatrix;
        this.SchematicsWorklogEntriesOverlay.Entries = overlayEntries;
        this.SchematicsWorklogEntriesOverlay.IsVisible = true;
        this.SchematicsWorklogEntriesOverlay.InvalidateVisual();

        this.RefreshThumbnailWorklogPills(allEntries);
    }

    // ###########################################################################################
    // Tears down every worklog visual on the schematic surface. Called by ResetSchematicsViewer on
    // a board switch, alongside the KiCad/label/polyline teardown it already did.
    //
    // Without this the badges stayed as live children of SchematicsWorklogEntriesBadgeCanvas after
    // the board changed - still carrying their entry ids and click handlers, drawn over the new
    // board, opening the editor for an entry belonging to the previous one. RefreshWorklogBar does
    // not necessarily clean up, because it only re-syncs when the workbook id actually differs.
    // ###########################################################################################
    public void ResetWorklogOverlays()
    {
        foreach (var (badge, _, _, _) in this.thisWorklogEntriesListBadges)
        {
            this.SchematicsWorklogEntriesBadgeCanvas.Children.Remove(badge);
        }
        this.thisWorklogEntriesListBadges.Clear();

        this.SchematicsWorklogEntriesOverlay.IsVisible = false;
        this.SchematicsWorklogEntriesOverlay.Entries = Array.Empty<WorklogEntriesOverlay.Entry>();

        if (this.thisWorklogEntryBadgeBorder != null)
        {
            this.thisWorklogEntryBadgeBorder.IsVisible = false;
        }

        this.ClearThumbnailWorklogPills();
    }

    // ###########################################################################################
    // Clears every thumbnail's state-pill overlay - used when "Show worklogs" is off or there is
    // no workbook to show.
    // ###########################################################################################
    private void ClearThumbnailWorklogPills()
    {
        foreach (var thumbnail in this.currentThumbnails)
        {
            if (thumbnail.WorklogPills.Count > 0)
            {
                thumbnail.WorklogPills = Array.Empty<ThumbnailWorklogPillsOverlay.Pill>();
            }
        }
    }

    // ###########################################################################################
    // Assigns each thumbnail its own "#N" pills - one per saved entry whose SchematicName matches
    // that thumbnail, centered on the entry's marked area (not the drawn bounds), colored by
    // category - see ThumbnailWorklogPillsOverlay for why status is deliberately left off. A
    // board's entries can span several schematics, so this groups once across all of them rather
    // than only the schematic currently on screen.
    // ###########################################################################################
    private void RefreshThumbnailWorklogPills(List<WorklogEntryRecord> allEntries)
    {
        var entriesBySchematic = allEntries
            .GroupBy(entry => entry.SchematicName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        foreach (var thumbnail in this.currentThumbnails)
        {
            if (!entriesBySchematic.TryGetValue(thumbnail.Name, out var thumbnailEntries))
            {
                if (thumbnail.WorklogPills.Count > 0)
                {
                    thumbnail.WorklogPills = Array.Empty<ThumbnailWorklogPillsOverlay.Pill>();
                }
                continue;
            }

            var pills = new List<ThumbnailWorklogPillsOverlay.Pill>(thumbnailEntries.Count);
            foreach (var entry in thumbnailEntries)
            {
                var center = new Point(
                    entry.AreaX + (entry.AreaWidth / 2.0),
                    entry.AreaY + (entry.AreaHeight / 2.0));

                pills.Add(new ThumbnailWorklogPillsOverlay.Pill(center, this.ResolveWorklogCategoryColor(entry.Category), entry.Id));
            }

            thumbnail.WorklogPills = pills;
        }
    }

    // ###########################################################################################
    // Resolves a category's theme color by name - the same Worklog_Category_* resources the "New
    // fault" card's chips use (GetSelectedWorklogEntryCategoryColor covers only the category
    // currently being edited, not an arbitrary saved one, hence this separate overload).
    // ###########################################################################################
    private Color ResolveWorklogCategoryColor(string category)
    {
        var brush = this.ResolveThemeBrush($"Worklog_Category_{category}", new SolidColorBrush(Colors.IndianRed));
        return brush is ISolidColorBrush solidBrush ? solidBrush.Color : Colors.IndianRed;
    }

    // ###########################################################################################
    // Resolves a saved entry's state pill color - the same Worklog_Status_Open/
    // Worklog_Status_Closed theme resources the worklog bar's own workbook-status pill uses, so an
    // entry's Open/Closed and a workbook's Open/Closed always render identically. They are two
    // different axes wearing one palette on purpose: both read "Open"/"Closed" to the user, and
    // showing them in different colours would imply a distinction that does not exist.
    //
    // Anything unrecognised falls through to Open: state is a free-form string in entries.json, so
    // a hand-edited or future value must still render rather than throw.
    // ###########################################################################################
    private Color ResolveWorklogStateColor(string state)
    {
        string stateColorResourceKey = state switch
        {
            WorklogStateClosed => "Worklog_Status_Closed",
            _ => "Worklog_Status_Open",
        };

        var brush = this.ResolveThemeBrush(stateColorResourceKey, new SolidColorBrush(Colors.IndianRed));
        return brush is ISolidColorBrush solidBrush ? solidBrush.Color : Colors.IndianRed;
    }

    // ###########################################################################################
    // Builds one "#N" badge + state pill for the "Show worklogs" list view, anchored at the top-
    // left of the entry's marked area, and adds it to SchematicsWorklogEntriesBadgeCanvas. Kept a
    // constant screen size across zoom via a ScaleTransform, the same technique
    // RefreshWorklogEntryBadge uses for the single in-progress draft badge.
    // ###########################################################################################
    private void CreateWorklogEntriesListBadge(WorklogEntryRecord entry, Color color, Rect pixelRect, Rect contentRect, double inverseScale)
    {
        var idText = new TextBlock
        {
            Text = $"#{entry.Id}",
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Foreground = Brushes.White,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        var stateBrush = new SolidColorBrush(this.ResolveWorklogStateColor(entry.State));

        // A solid disc of the state colour inside a white ring - no glyph. It used to carry a white
        // FontAwesome circle, which filled the interior white and left only a thin rim of the state
        // colour showing, so the dot read as white-on-colour rather than as the state's own colour.
        // The state is conveyed by the fill alone, exactly as the dots in the state pills are.
        var statePill = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(8),
            Background = stateBrush,
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        var content = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 4 };
        content.Children.Add(idText);
        content.Children.Add(statePill);

        var badge = new Border
        {
            Background = new SolidColorBrush(color),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 3),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = content,
            RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            RenderTransform = new ScaleTransform(inverseScale, inverseScale),
            Tag = entry.Id
        };
        badge.PointerPressed += this.OnWorklogEntryPillPointerPressed;

        this.SchematicsWorklogEntriesBadgeCanvas.Children.Add(badge);
        this.thisWorklogEntriesListBadges.Add((badge, pixelRect, entry, color));

        this.PositionWorklogEntriesListBadge(badge, pixelRect, contentRect, inverseScale);
    }

    // ###########################################################################################
    // Positions and scales one "Show worklogs" badge for the given inverse-scale, anchored at the
    // top-left of its entry's marked area - shared by CreateWorklogEntriesListBadge (first layout)
    // and RescaleWorklogEntriesListBadges (every later zoom/pan tick).
    // ###########################################################################################
    private void PositionWorklogEntriesListBadge(Border badge, Rect pixelRect, Rect contentRect, double inverseScale)
    {
        ((ScaleTransform)badge.RenderTransform!).ScaleX = inverseScale;
        ((ScaleTransform)badge.RenderTransform!).ScaleY = inverseScale;

        var localRect = RectGeometry.PixelToLocalRect(pixelRect, contentRect, this.currentFullResBitmap!.PixelSize);

        badge.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Size desiredSize = badge.DesiredSize * inverseScale;

        Canvas.SetLeft(badge, localRect.Left - (desiredSize.Width / 2.0));
        Canvas.SetTop(badge, localRect.Top - (desiredSize.Height / 2.0));
    }

    // ###########################################################################################
    // Hit-tests the "Show worklogs" list view's own "#N" pills (not the marked area they are
    // anchored to) at the given container point. Checked back-to-front so an overlapping pill
    // drawn later (on top) wins. Used by UpdateSchematicsHoverUi to give a worklog pill's hover
    // info priority over the component highlight underneath it - the two can and do overlap,
    // since a fault is typically marked right on top of the component it concerns, but only the
    // small pill itself should trigger the swap, not the whole marked area.
    //
    // BOTH corners are translated, not a corner plus Bounds.Size: each pill carries a centered
    // ScaleTransform (PositionWorklogEntriesListBadge keeps it a constant screen size across
    // zoom), and Bounds is the pre-transform layout size. Pairing a translated corner with an
    // untransformed size gave a rect that was both mis-sized and offset at any zoom other than
    // 100% - too big above it (hover triggering off empty space beside the pill), too small
    // below it (dead edges). Translating both corners lets the visual tree apply the pill's
    // ScaleTransform and the canvas MatrixTransform to each, so the rect matches what is drawn
    // without this method re-deriving either.
    //
    // The corners are (0,0) and (Width,Height) - badge-LOCAL coordinates, because TranslatePoint
    // reads its argument in the source control's own space. Passing Bounds.TopLeft/BottomRight
    // instead looks equivalent but is not: Bounds is expressed in the PARENT's space, so for a
    // badge at Canvas.SetLeft(120)/SetTop(80) its TopLeft is (120,80) rather than (0,0) and the
    // translation adds the pill's position a second time, landing the rect at roughly twice its
    // real offset and killing hover everywhere. GetWorklogEntryAreaBoundsInContainer looks like
    // the counter-example but is not - the rect it translates is already in the space of the
    // overlay it calls TranslatePoint on.
    // ###########################################################################################
    private bool TryGetHoveredWorklogEntry(Point pointerInContainer, out WorklogEntryRecord entry, out Color color)
    {
        entry = null!;
        color = default;

        if (!this.thisIsShowingWorklogEntriesList || this.thisWorklogEntriesListBadges.Count == 0)
        {
            return false;
        }

        for (int i = this.thisWorklogEntriesListBadges.Count - 1; i >= 0; i--)
        {
            var (badge, _, candidateEntry, candidateColor) = this.thisWorklogEntriesListBadges[i];

            Point? topLeft = badge.TranslatePoint(new Point(0, 0), this.SchematicsContainer);
            Point? bottomRight = badge.TranslatePoint(new Point(badge.Bounds.Width, badge.Bounds.Height), this.SchematicsContainer);

            if (!topLeft.HasValue || !bottomRight.HasValue)
            {
                continue;
            }

            var badgeRect = new Rect(topLeft.Value, bottomRight.Value);
            if (badgeRect.Contains(pointerInContainer))
            {
                entry = candidateEntry;
                color = candidateColor;
                return true;
            }
        }

        return false;
    }

    // ###########################################################################################
    // Repositions/rescales the existing "Show worklogs" badges for the current view matrix without
    // touching which entries are shown - called on every zoom/pan tick (TabSchematics.Viewport.cs).
    // The canvas's own MatrixTransform already keeps the colored-area overlay and the badges'
    // container in sync cheaply; this only needs to redo the inverse-scale ScaleTransform and
    // re-anchor each badge, the same per-frame work RefreshWorklogEntryBadge does for the single
    // in-progress draft badge. No disk read and no control-tree rebuild, unlike
    // RefreshWorklogEntriesListOverlay, which this deliberately does not call.
    // ###########################################################################################
    private void RescaleWorklogEntriesListBadges()
    {
        if (this.thisWorklogEntriesListBadges.Count == 0 || this.currentFullResBitmap == null)
        {
            return;
        }

        var contentRect = this.GetImageContentRect();
        double scale = this.schematicsMatrix.M11;
        double inverseScale = scale > 0 ? 1.0 / scale : 1.0;

        foreach (var (badge, pixelRect, _, _) in this.thisWorklogEntriesListBadges)
        {
            this.PositionWorklogEntriesListBadge(badge, pixelRect, contentRect, inverseScale);
        }
    }

    // ###########################################################################################
    // Opens the full editor for the clicked saved entry's pill. Looks the entry back up from disk
    // by id (rather than capturing the WorklogEntryRecord in the closure) so the editor always
    // opens against the latest saved data, even if something else changed it first.
    //
    // Left button only, and never while an entry area is being marked out:
    //  - the badges sit on the one overlay canvas that is hit-test visible, so handling every
    //    button made each of them a dead zone for right-button panning (and opened the editor on a
    //    right-click).
    //  - during entry-drawing mode a drag that crossed a badge opened the editor over the
    //    half-drawn rectangle, leaving the drawing state stuck with no pointer release.
    // In both cases the press must fall through untouched rather than being marked handled.
    // ###########################################################################################
    private async void OnWorklogEntryPillPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (this.thisIsWorklogEntryMode ||
            !e.GetCurrentPoint(this.SchematicsContainer).Properties.IsLeftButtonPressed)
        {
            return;
        }

        // Without this, the press bubbles up to OnSchematicsPointerPressed and is picked up as the
        // start of a manual KiCad-trace drag, which then follows the mouse for the rest of the
        // click - even once the editor dialog above has already opened and closed.
        e.Handled = true;

        if (sender is not Border { Tag: int entryId })
        {
            return;
        }

        int workbookId = this.thisWorklogEntriesListWorkbookId;

        var entry = WorklogManager.GetEntries(workbookId).FirstOrDefault(x => x.Id == entryId);
        if (entry == null)
        {
            return;
        }

        if (TopLevel.GetTopLevel(this) is not Window ownerWindow)
        {
            return;
        }

        var editor = new WorklogEntryEditorWindow();

        // The workbook id is captured up front: the dialog below is modal but the board can still
        // change underneath it (the thumbnail load path refreshes on its own), and re-reading the
        // field afterwards would point the editor at a different workbook than the badge came from.
        editor.Initialize(workbookId, entry, this.currentFullResBitmap);

        // The editor cannot work this out for itself - it has neither the board data nor the
        // highlight rectangles - so the scope is computed here and handed over. Resolved against
        // the entry's OWN schematic rather than whichever one is on screen: the "Show worklogs"
        // list can put a badge for another schematic in view, and using the visible one would
        // offer components from a different board image entirely.
        var componentsInScope = this.BuildWorklogEntryComponentScope(entry);
        if (componentsInScope != null)
        {
            editor.InitializeComponentScope(componentsInScope);
        }

        await editor.ShowDialog(ownerWindow);

        if (!editor.WasSaved)
        {
            return;
        }

        // Anything after the await runs as an async void continuation, where an exception is
        // unobservable and takes the process down instead of surfacing - so the refresh, which
        // touches controls a board switch may have torn down meanwhile, is guarded.
        try
        {
            this.RefreshWorklogEntriesListOverlay();
            this.MainWindow?.RefreshWorklogBar();
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to refresh worklog overlays after editing entry [#{entryId}]: [{ex.Message}]");
        }
    }
}

// ###########################################################################################
// One row in the worklog entry card's "Mark components in scope" checklist: a component whose
// highlight rectangle intersects the drawn entry area. Public and top-level (not nested inside
// TabSchematics) so the compiled DataTemplate in TabSchematics.axaml can bind to it.
// ###########################################################################################
public sealed class WorklogEntryComponentRow : System.ComponentModel.INotifyPropertyChanged
{
    private bool _isChecked = true;

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public bool IsChecked
    {
        get => this._isChecked;
        set
        {
            if (this._isChecked == value)
                return;

            this._isChecked = value;
            this.PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(this.IsChecked)));
        }
    }

    public string BoardLabel { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}
