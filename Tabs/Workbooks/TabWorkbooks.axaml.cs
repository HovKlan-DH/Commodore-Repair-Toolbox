using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Handlers.DataHandling;
using Handlers.Geometry;
using Handlers.Theming;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CRT
{
    // ###########################################################################################
    // THE WORKBOOKS TAB - concept "C; Worklog tab" from the mockup, now functional apart from one
    // field.
    //
    // FILE MAP - this class is split across:
    //   TabWorkbooks.axaml.cs        (this file) construction, Main wiring, the splitters and their
    //                                persistence, the left-hand workbook list and its cards, and
    //                                the top-line above the board/entry split
    //   TabWorkbooks.BoardPreviews.cs the board pane and the entry list beside it - schematic
    //                                previews, entry badges, schematic selection, entry detail
    //                                cards, and the editor a badge opens
    //
    // What IS real:
    //   - the left panel, listing the workbooks WorklogManager holds for the selected board,
    //     rebuilt by RefreshWorkbooks;
    //   - clicking a card ACTIVATES that workbook app-wide (see SelectWorkbook): the choice is
    //     persisted to UserSettings.ActiveWorkbookIdByBoard and the worklog bar, "Show worklogs"
    //     and "Add worklog" all follow it. The tab does not switch away - the top-line and the
    //     board pane update in place;
    //   - the board pane (TabWorkbooks.BoardPreviews.cs): every schematic image with entries in the
    //     ACTIVE workbook, each entry drawn as the Schematics tab draws it - a dashed bounds
    //     rectangle plus an anchored badge where "show marked area" is on, a corner-parked badge
    //     where it is not;
    //   - clicking a badge opens the SAME WorklogEntryEditorWindow the Schematics tab opens,
    //     component-scope checklist included;
    //   - clicking elsewhere on a preview selects that schematic and switches the entry list on the
    //     right to it, one detail card per entry.
    //
    // What is NOT wired up: the "Find a previous repair" field, which is disabled rather than left
    // looking functional - see the note beside it in the .axaml.
    //
    // Main.RefreshWorklogBar calls RefreshWorkbooks, so the list, the selected card, the top-line
    // and the board pane all follow a board change and any workbook edit through the one funnel the
    // rest of the worklog feature already refreshes through. The board pane has a second, narrower
    // entry point for when the component highlight cache changes - see
    // RefreshBoardPreviewsForCurrentSelection.
    //
    // Visibility is not decided here: Main.axaml.cs's ApplyWorklogBarVisibility shows and hides the
    // tab along with the worklog bar, both driven by "Enable Worklog" in Configuration.
    // ###########################################################################################
    public partial class TabWorkbooks : UserControl
    {
        // Set by Main during startup wiring, the same way TabSchematics gets its reference. The
        // board key lives on the main window (it is the hardware/board combo selection), so the
        // tab cannot resolve which board it is showing without this.
        public Main? MainWindow { get; set; }

        // The board key normally comes from the main window's hardware/board selection, which no
        // test constructs. This is the seam that lets the headless tests exercise the list itself
        // without standing up Main and its combo boxes - the same idea as UserSettings.LoadFrom
        // and DataManager.LoadFrom. Null in the running app, always.
        internal string? BoardKeyOverrideForTests { get; set; }

        // Same idea, for the board pane (TabWorkbooks.BoardPreviews.cs): it normally reads
        // Main.CurrentBoardData for the schematic image list, which again needs no test to stand up
        // Main itself. Null in the running app, always.
        internal BoardData? CurrentBoardDataOverrideForTests { get; set; }

        // What RefreshBoardPreviews actually reads - the override when a test set one, otherwise
        // the real main window's board data.
        private BoardData? CurrentBoardDataForPreviews =>
            this.CurrentBoardDataOverrideForTests ?? this.MainWindow?.CurrentBoardData;

        // Same idea again, for BuildWorklogEntryComponentScope (TabWorkbooks.BoardPreviews.cs): it
        // normally reads MainWindow.TabSchematicsControl.highlightRectsBySchematicAndLabel, which
        // again needs no test to stand up Main and a whole TabSchematics. Null in the running app,
        // always.
        //
        // The setter REJECTS a dictionary that is not OrdinalIgnoreCase-keyed. The real cache always
        // is - Main builds it that way at every one of its write sites, and TabSchematics declares
        // the field that way - so a seam that accepted a plain `new Dictionary<...>()` would let a
        // future test encode Ordinal lookup as the contract and pass, while the app, reading the
        // real cache, behaved the opposite way. A test seam that can certify behaviour opposite to
        // production's is worse than no seam.
        private Dictionary<string, Dictionary<string, List<Rect>>>? thisHighlightRectsOverrideForTests;

        internal Dictionary<string, Dictionary<string, List<Rect>>>? HighlightRectsBySchematicAndLabelOverrideForTests
        {
            get => this.thisHighlightRectsOverrideForTests;
            set
            {
                if (value != null && !ReferenceEquals(value.Comparer, StringComparer.OrdinalIgnoreCase))
                {
                    throw new ArgumentException(
                        "The highlight-rect cache is keyed OrdinalIgnoreCase in the running app; a test " +
                        "override with any other comparer would pin the opposite behaviour. Build it with " +
                        "new Dictionary<string, Dictionary<string, List<Rect>>>(StringComparer.OrdinalIgnoreCase).",
                        nameof(value));
                }

                this.thisHighlightRectsOverrideForTests = value;
            }
        }

        // What BuildWorklogEntryComponentScope actually reads - the override when a test set one,
        // otherwise the real main window's TabSchematics cache.
        private Dictionary<string, Dictionary<string, List<Rect>>>? HighlightRectsBySchematicAndLabelForPreviews =>
            this.thisHighlightRectsOverrideForTests ?? this.MainWindow?.TabSchematicsControl?.highlightRectsBySchematicAndLabel;

        // The workbook currently shown in the top-line, board and entry list. -1 means none -
        // either the board has no workbooks, or nothing has been clicked yet.
        //
        // Kept across a RefreshWorkbooks rebuild (see the end of that method): a board edit or a
        // theme change must not silently drop the user's selection back to "nothing chosen".
        private int thisSelectedWorkbookId = -1;

        // The SAME two glyphs the worklog bar's status pill and a worklog entry's own state pill use
        // - a workbook reading "Open" here and "Open" there must look identical, which is the whole
        // reason this pill was built to the existing recipe rather than invented again. They come
        // from WorklogGlyphs rather than being spelled out here; the other partial of THIS class
        // already had its own copy, which is what made "six declarations" six rather than three.
        private static readonly string LockOpenGlyph = WorklogGlyphs.OpenGlyph;

        private static readonly string LockClosedGlyph = WorklogGlyphs.ClosedGlyph;

        public TabWorkbooks()
        {
            this.InitializeComponent();

            // The top-line's padlock needs the same ascent-overflow reservation the code-built cards
            // get, and markup cannot compute it - see the comment beside the control. The cards' own
            // glyphs are handled in BuildWorkbookCard.
            //
            // Null-guarded like every other named-control access in this class. This constructor runs
            // from Main's own InitializeComponent, so an unguarded dereference here does not degrade
            // the tab - it throws out of Main's constructor and the WHOLE MAIN WINDOW fails to
            // construct at startup. It also depended on the markup hardcoding a Text on that glyph,
            // a correctness-by-coincidence with no compile-time link.
            if (this.WorkbookHeaderStatusGlyph != null)
            {
                this.WorkbookHeaderStatusGlyph.Padding = Handlers.Geometry.FontAwesomeGlyphMetrics
                    .GetTopOverflowThicknessForText(this.WorkbookHeaderStatusGlyph.Text, this.WorkbookHeaderStatusGlyph.FontSize);
            }
        }

        // ###########################################################################################
        // Releases the full-resolution schematic bitmaps the board pane decoded, once this tab is off
        // the visual tree - see thisSchematicBitmapsByPath in TabWorkbooks.BoardPreviews.cs for why
        // they are held for the tab's whole life rather than freed on each rebuild. Without this the
        // last set outlives the tab, exactly as WorklogEntryEditorWindow's own Closed handler
        // documents for its thumbnails.
        // ###########################################################################################
        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            this.DisposeSchematicBitmaps();
        }

        // ###########################################################################################
        // Hands the tab its main-window reference, matching TabSchematics/TabOverview/TabContribute.
        // The board key is the hardware/board combo selection, which lives on the main window, so
        // without this the tab cannot tell which board's workbooks to list.
        // ###########################################################################################
        public void Initialize(Main mainWindow)
        {
            this.MainWindow = mainWindow;
            this.thisActivateWorkbook = mainWindow.ActivateWorkbook;
            this.ApplySplitterWidths();
            this.WireSplitterPersistence();
        }

        // ###########################################################################################
        // Subscribes both splitters' PointerReleased so a finished drag is saved.
        //
        // AddHandler with handledEventsToo: true, NOT a PointerReleased="..." attribute in the
        // markup: GridSplitter marks the event handled as it completes its own drag, so an ordinary
        // subscription never runs and the width is silently never saved. Main's own
        // OnMainSplitterPointerReleased and TabSchematics.ApplySchematicsSplitterRatio wire theirs
        // the same way, with the same comment - this tab had the markup form and neither of its two
        // widths was ever written.
        // ###########################################################################################
        private void WireSplitterPersistence()
        {
            this.OuterSplitter.AddHandler(
                InputElement.PointerReleasedEvent,
                this.OnOuterSplitterPointerReleased,
                RoutingStrategies.Bubble,
                handledEventsToo: true);

            this.BoardEntrySplitter.AddHandler(
                InputElement.PointerReleasedEvent,
                this.OnBoardEntrySplitterPointerReleased,
                RoutingStrategies.Bubble,
                handledEventsToo: true);
        }

        // Exposed to the test project so a splitter-persistence test can wire the handlers without
        // constructing a real Main (Initialize's normal caller) - the same idea as
        // ApplySplitterWidthsForTests.
        internal void WireSplitterPersistenceForTests() => this.WireSplitterPersistence();

        // ###########################################################################################
        // Restores both of this tab's splitters from UserSettings, so they do not flash back to
        // their design-time default width every time the app opens - the same pattern
        // TabSchematics.ApplySchematicsSplitterRatio and Main's own LeftPanelWidth follow. Unlike
        // the Schematics tab's splitter, these are plain pixel widths rather than a per-board ratio:
        // this tab's layout does not depend on which board is selected, so one app-wide value per
        // splitter is enough.
        // ###########################################################################################
        private void ApplySplitterWidths()
        {
            this.OuterSplitGrid.ColumnDefinitions[0].Width = new GridLength(ClampPanelWidth(UserSettings.WorkbooksLeftPanelWidth));
            this.BoardEntrySplitGrid.ColumnDefinitions[2].Width = new GridLength(ClampPanelWidth(UserSettings.WorkbooksEntryListWidth));
        }

        // ###########################################################################################
        // Keeps a restored panel width usable on the screen it is being restored ONTO.
        //
        // The saved value is a raw pixel width from whatever monitor the drag happened on, and this
        // runs once from Initialize, before the window has a meaningful size to compare against. A
        // width saved on a large monitor was applied verbatim on a small one: a 1100px panel on a
        // 1366px screen squeezes everything else to near-zero and puts the splitter off-screen, at
        // which point the only way back is hand-editing settings.json.
        //
        // Clamped to a fixed ceiling rather than to a fraction of the window because the window is
        // not laid out yet at this point. The ceiling is generous - it only catches values that are
        // unusable on any ordinary screen - and the floor keeps a panel dragged shut from restoring
        // as an invisible sliver with no splitter to grab.
        // ###########################################################################################
        private const double MinimumPanelWidth = 120.0;

        private const double MaximumPanelWidth = 900.0;

        private static double ClampPanelWidth(double width) =>
            double.IsNaN(width) || width < MinimumPanelWidth
                ? MinimumPanelWidth
                : Math.Min(width, MaximumPanelWidth);

        // Exposed to the test project so a splitter-persistence test can call this without
        // constructing a real Main (Initialize's normal caller) - the same idea as
        // SelectWorkbookForTests and SelectSchematicForTests.
        internal void ApplySplitterWidthsForTests() => this.ApplySplitterWidths();

        // ###########################################################################################
        // Saves the left workbook-list panel's width after the outer splitter is dragged. Deferred
        // via Post, matching Main's own OnLeftSplitterPointerReleased/TabSchematics's
        // OnSchematicsSplitterPointerReleased: the drag has not yet been applied to Bounds at the
        // moment PointerReleased fires, so reading it synchronously would save the width from before
        // this drag.
        // ###########################################################################################
        private void OnOuterSplitterPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            Dispatcher.UIThread.Post(() => UserSettings.WorkbooksLeftPanelWidth = this.WorkbookListBorder.Bounds.Width);
        }

        // Same idea as OnOuterSplitterPointerReleased, for the board-pane/entry-list splitter.
        private void OnBoardEntrySplitterPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            Dispatcher.UIThread.Post(() => UserSettings.WorkbooksEntryListWidth = this.SelectedSchematicEntriesBorder.Bounds.Width);
        }

        // ###########################################################################################
        // Rebuilds the left-hand workbook list for the currently selected board.
        //
        // Called from Main.RefreshWorklogBar rather than being wired to anything here: that method
        // is already the single place the app refreshes worklog state from (board changes, entry
        // saves, workbook creation and closure all funnel through it), so hanging this off it means
        // the list cannot go stale in a case the bar handles and this forgot.
        //
        // Rebuilding the whole panel each time, rather than diffing it, is deliberate - the list is
        // a handful of cards, and a rebuild cannot leave a stale card behind.
        //
        // Takes the board's workbooks when the caller has just read them (Main.RefreshWorklogBar has,
        // to resolve which one is active) and reads them itself otherwise. GetWorkbooksForBoard goes
        // through ReadAllWorkbooks, which enumerates EVERY workbook folder on disk and does a
        // File.Exists + ReadAllText + Deserialize per folder, uncached, with the board filter applied
        // only afterwards - and there is no delete feature, so that set only ever grows. Reading it
        // twice per refresh pass was the single most expensive thing this tab did.
        // ###########################################################################################
        public void RefreshWorkbooks(List<WorkbookRecord>? boardWorkbooks = null)
        {
            if (this.WorkbookListPanel == null)
                return;

            string boardKey = this.BoardKeyOverrideForTests ?? this.MainWindow?.GetCurrentBoardKey() ?? string.Empty;
            var workbooks = boardWorkbooks ?? WorklogManager.GetWorkbooksForBoard(boardKey);

            this.WorkbookListPanel.Children.Clear();

            // "1 workbook" / "3 workbooks" - the count is the panel's heading, so it has to be
            // right for one as well as for none and many.
            this.WorkbookCountText.Text = WorklogEntryScope.FormatCount(workbooks.Count, "workbook", "workbooks");

            this.NoWorkbooksText.IsVisible = workbooks.Count == 0;

            // Which card is highlighted is decided by the SAME WorklogManager.ResolveActiveWorkbook
            // the worklog bar, "Show worklogs" and "Add worklog" use (via
            // Main.ResolveActiveWorkbookForBoard), reading the same saved
            // UserSettings.ActiveWorkbookIdByBoard - so this panel's highlighted card cannot
            // disagree with what the rest of the app is acting on for this board. There is no
            // in-memory tier: SelectWorkbook persists and lets the refresh that follows re-derive
            // the selection from that one saved id, whether the caller is Main or a test.
            //
            // NOTE that when nothing is saved this HIGHLIGHTS the newest workbook without saving
            // it. The highlight is therefore an assumption, not an activation - which is why
            // SelectWorkbook has no "already selected, nothing to do" guard; see its header.
            var active = WorklogManager.ResolveActiveWorkbook(workbooks, UserSettings.GetActiveWorkbookId(boardKey));
            this.thisSelectedWorkbookId = active?.Id ?? -1;

            foreach (var workbook in workbooks)
            {
                bool isSelected = workbook.Id == this.thisSelectedWorkbookId;
                this.WorkbookListPanel.Children.Add(this.BuildWorkbookCard(workbook, isSelected));
            }

            this.ApplyHeaderForWorkbook(active);
            this.RefreshBoardPreviews();
        }

        // ###########################################################################################
        // How SelectWorkbook activates a workbook app-wide. Set by Initialize to Main's own
        // ActivateWorkbook; left null only in headless tests, which supply their own via
        // ActivateWorkbookOverrideForTests.
        //
        // A settable delegate rather than a "if (MainWindow != null) ... else <do it locally>"
        // branch inside SelectWorkbook, and this is the point: with a branch, EVERY test ran the
        // no-MainWindow side and the shipped click path - persist, then rebuild from the saved id -
        // was pinned by nothing at all. One path now, whoever calls it.
        // ###########################################################################################
        private Action<string, int>? thisActivateWorkbook;

        // Lets a headless test stand in for Main.ActivateWorkbook - normally
        // "save the id, then RefreshWorkbooks", which is what the running app does via
        // Main.RefreshWorklogBar. Null in the running app, always; Initialize sets the real one.
        internal Action<string, int>? ActivateWorkbookOverrideForTests
        {
            get => this.thisActivateWorkbook;
            set => this.thisActivateWorkbook = value;
        }

        // ###########################################################################################
        // Selects a workbook by id, called when the user clicks a card. This "activates" the
        // workbook app-wide rather than only changing what this panel highlights: the worklog bar
        // above the tabs switches to showing it, "Show worklogs" on the Schematics tab draws ITS
        // entries, and "Add worklog" starts writing new entries into it - all in place of the
        // board's newest workbook, which is what every one of those otherwise defaults to.
        //
        // Deliberately does NOT switch tabs. Activating a workbook here is meant to be seen HERE -
        // the top-line and the board pane update in place for the newly active workbook - so the
        // user can browse several workbooks' schematics without leaving this tab. Only "Add
        // worklog" on the bar jumps to Schematics (see Main.OnWorklogAddEntryClick), because
        // drawing a new entry needs the actual schematic view to draw on.
        //
        // Activation goes through thisActivateWorkbook (Main.ActivateWorkbook in the running app),
        // which saves to UserSettings.ActiveWorkbookIdByBoard and calls RefreshWorklogBar - and
        // that calls back into RefreshWorkbooks above, which re-derives thisSelectedWorkbookId from
        // the same saved id. So there is exactly one path that decides what is selected and this
        // method does not duplicate it.
        //
        // NO "already selected, nothing to do" guard, deliberately. RefreshWorkbooks HIGHLIGHTS a
        // default card when the board has no saved activation, without saving anything - so the
        // card the user is looking at can be "the selected one" on screen while
        // ActiveWorkbookIdByBoard is empty. An early return on id equality made clicking that exact
        // card a no-op, leaving the highlight and the persisted state disagreeing: creating a newer
        // workbook afterwards then silently moved the user off it, and only clicking a different
        // card and back made the choice stick. An assumed highlight and a real activation must not
        // be indistinguishable to the persistence layer. Re-activating what is already active is
        // idempotent and cheap, so there is nothing to protect against here.
        // ###########################################################################################
        // Exposed to the test project so WorkbooksListTests can select a card without fighting
        // pointer-event routing against a UserControl that has no window - the same idea as
        // BoardKeyOverrideForTests. The running app always reaches this through a card's
        // PointerPressed handler in BuildWorkbookCard, never directly.
        internal void SelectWorkbookForTests(int workbookId) => this.SelectWorkbook(workbookId);

        private void SelectWorkbook(int workbookId)
        {
            string boardKey = this.BoardKeyOverrideForTests ?? this.MainWindow?.GetCurrentBoardKey() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(boardKey))
                return;

            this.thisActivateWorkbook?.Invoke(boardKey, workbookId);
        }

        // ###########################################################################################
        // Updates the top-line's title and status pill for the given workbook, or clears it back to
        // the tab's construction-time placeholder when there is none (no board selected, or the
        // board has no workbooks).
        // ###########################################################################################
        private void ApplyHeaderForWorkbook(WorkbookRecord? workbook)
        {
            if (workbook == null)
            {
                this.WorkbookHeaderTitleText.Text = "No workbook selected";
                this.WorkbookHeaderStatusPill.IsVisible = false;
                return;
            }

            string title = string.IsNullOrWhiteSpace(workbook.Title) ? "(untitled)" : workbook.Title;
            this.WorkbookHeaderTitleText.Text = $"#{workbook.Id} · {title}";

            bool isOpen = WorklogManager.IsWorkbookStatusOpen(workbook.Status);
            var statusBrush = ResolveWorklogStatusBrush(isOpen);

            this.WorkbookHeaderStatusPill.IsVisible = true;
            this.WorkbookHeaderStatusPill.BorderBrush = statusBrush;
            this.WorkbookHeaderStatusText.Text = workbook.Status;
            this.WorkbookHeaderStatusText.Foreground = statusBrush;
            this.WorkbookHeaderStatusGlyph.Text = isOpen ? LockOpenGlyph : LockClosedGlyph;
            this.WorkbookHeaderStatusGlyph.Foreground = statusBrush;

            // Recomputed rather than reused from the constructor: the glyph switches between the
            // lock and lock-open codepoints, and the two overshoot their font's declared ascent by
            // different amounts (see FontAwesomeGlyphMetrics), so a padding value fixed at
            // construction would be right for only one of the two states.
            this.WorkbookHeaderStatusGlyph.Padding = Handlers.Geometry.FontAwesomeGlyphMetrics
                .GetTopOverflowThicknessForText(this.WorkbookHeaderStatusGlyph.Text, this.WorkbookHeaderStatusGlyph.FontSize);
        }

        // ###########################################################################################
        // Builds one workbook card: the "#N" id with its status pill, the title, and the worklog
        // count with the start date. Clicking anywhere on the card selects that workbook - see
        // SelectWorkbook.
        //
        // Built in code rather than as an ItemsControl DataTemplate because the status pill's brush
        // has to be resolved through Application.Current with an explicit ThemeVariant (see
        // ResolveWorklogStatusBrush), which a template binding cannot express - the same reason
        // Main builds the worklog bar's pill in code.
        //
        // An instance method, not static: it needs to reach SelectWorkbook on THIS tab from the
        // click handler it wires up.
        // ###########################################################################################
        private Border BuildWorkbookCard(WorkbookRecord workbook, bool isSelected)
        {
            bool isOpen = WorklogManager.IsWorkbookStatusOpen(workbook.Status);
            var statusBrush = ResolveWorklogStatusBrush(isOpen);

            // --- Row 1: "#N" and the status pill -------------------------------------------------
            var idText = new TextBlock
            {
                Text = $"#{workbook.Id}",
                FontSize = 11,
                FontWeight = FontWeight.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Dot, label AND border all take the status colour, exactly as the worklog bar's pill
            // and an entry's state pill do. Colouring only the dot would leave this pill visibly
            // different from the ones it is meant to match.
            var statusGlyph = new TextBlock
            {
                Text = isOpen ? LockOpenGlyph : LockClosedGlyph,
                FontFamily = ResolveFontAwesomeSolid(),
                FontSize = 10,
                Foreground = statusBrush,
                VerticalAlignment = VerticalAlignment.Center
            };

            // The padlocks are drawn taller than the font's declared ascent, so their top pixel row
            // is clipped without a reserved one. Computed from this control's own font size rather
            // than hardcoded - see Handlers/Geometry/FontAwesomeGlyphMetrics.cs.
            statusGlyph.Padding = Handlers.Geometry.FontAwesomeGlyphMetrics
                .GetTopOverflowThicknessForText(statusGlyph.Text, statusGlyph.FontSize);

            var statusLabel = new TextBlock
            {
                Text = workbook.Status,
                FontSize = 10,
                FontWeight = FontWeight.SemiBold,
                Foreground = statusBrush,
                VerticalAlignment = VerticalAlignment.Center
            };

            var statusContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            statusContent.Children.Add(statusGlyph);
            statusContent.Children.Add(statusLabel);

            var statusPill = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 1),
                BorderBrush = statusBrush,
                BorderThickness = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Child = statusContent
            };

            // Through ThemeResources like every other lookup here. This was an inline copy of the
            // two-step idiom with NO fallback, so a miss left Background unset rather than rendering
            // something - invisible until someone noticed the pill was the wrong colour.
            statusPill.Background = ThemeResources.ResolveBrush("Form_Bg", Brushes.Transparent);

            var headerRow = new Grid();
            headerRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            headerRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Grid.SetColumn(idText, 0);
            Grid.SetColumn(statusPill, 1);
            headerRow.Children.Add(idText);
            headerRow.Children.Add(statusPill);

            // --- Row 2: the title ----------------------------------------------------------------
            // A workbook can be created without one, so fall back to something rather than leaving
            // the card's middle line blank and the card looking broken.
            var titleText = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(workbook.Title) ? "(untitled)" : workbook.Title,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            };

            // --- Row 3: the worklog count and the start date -------------------------------------
            var metaText = new TextBlock
            {
                Text = BuildWorkbookMetaText(workbook),
                FontSize = 10,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            metaText.Classes.Add("WorkbooksMuted");

            var body = new StackPanel { Spacing = 3 };
            body.Children.Add(headerRow);
            body.Children.Add(titleText);
            body.Children.Add(metaText);

            var card = new Border
            {
                // The record rides along on the card so SelectWorkbook can read it back off
                // WorkbookListPanel.Children without a second id->record lookup. The Hand cursor
                // comes from the WorkbooksJobCard style in the .axaml, not set here.
                Tag = workbook,
                Child = body
            };
            card.Classes.Add("WorkbooksJobCard");
            if (isSelected)
                card.Classes.Add("Selected");

            card.PointerPressed += (_, _) => this.SelectWorkbook(workbook.Id);

            return card;
        }

        // ###########################################################################################
        // The card's third line: "{x} worklogs · started 2026-August-26".
        //
        // The date format matches the worklog bar's (yyyy-MMMM-dd, invariant), so a workbook's start
        // date reads the same in both places. Invariant rather than the current culture on purpose:
        // the bar chose it so the month is always a name and never an ambiguous number, and the two
        // must not disagree.
        // ###########################################################################################
        private static string BuildWorkbookMetaText(WorkbookRecord workbook)
        {
            string startDate = workbook.StartDate.ToString("yyyy-MMMM-dd", CultureInfo.InvariantCulture);

            return $"{WorklogEntryScope.FormatCount(workbook.EntryCount, "worklog", "worklogs")} · started {startDate}";
        }

        // ###########################################################################################
        // Resolves the Open/Closed status colour the same way every other worklog surface does.
        //
        // ThemeResources.Resolve does the two-step Application.Current + ActualThemeVariant lookup
        // these ThemeDictionaries-scoped keys need - see that class for why a plain TryFindResource
        // on a control silently returns the fallback instead.
        //
        // The fallbacks are last-resort only, and must track the theme values in App.axaml.
        // ###########################################################################################
        private static IBrush ResolveWorklogStatusBrush(bool isOpen) =>
            ThemeResources.ResolveBrush(
                isOpen ? "Worklog_Status_Open" : "Worklog_Status_Closed",
                isOpen ? Brushes.IndianRed : new SolidColorBrush(Color.Parse("#4C8C31")));

        // The Font Awesome families, so a padlock built here is the same font as the identical
        // padlock declared in XAML. Note is the one category whose glyph is Regular rather than Solid
        // (WorklogEntryEditorWindow.axaml's EditorCategoryNoteIcon).
        private static FontFamily ResolveFontAwesomeSolid() => ThemeResources.ResolveFontAwesomeSolid();

        private static FontFamily ResolveFontAwesomeRegular() => ThemeResources.ResolveFontAwesomeRegular();
    }
}
