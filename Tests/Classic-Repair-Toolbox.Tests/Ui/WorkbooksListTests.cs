using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CRT;
using Handlers.DataHandling;

namespace ClassicRepairToolbox.Tests.Ui;

// The Workbooks tab's left-hand panel and workbook selection - the parts of that tab wired to
// real data.
//
// It lists what WorklogManager holds for the current board: a "#N" id with an Open/Closed status
// pill, the title, and "{x} worklogs · started {date}". Everything else on the tab is still static
// mockup markup, so there is nothing else there worth asserting yet.
//
// EVERY assertion runs INSIDE UiTest.Run, not just the construction. Reading a control's property
// from the test thread throws "the calling thread cannot access this object" - even a plain
// GetControl does, because the name-scope lookup itself reads a styled property. So the pattern
// here is to do the whole build-and-assert inside the body and let xunit's failure propagate out,
// rather than hoisting the control out to assert on afterwards.
//
// COLLECTION NOTE: this is in "HeadlessUi" rather than "Worklog" or "UserSettings" because it
// constructs a control and so needs the shared dispatcher thread - a class can only join one
// collection. It nonetheless drives BOTH WorklogManager's static root (every test here points the
// manager at its OWN uniquely-named temp folder and re-points it in Dispose) and UserSettings'
// static _data, which "UserSettings" collection tests re-point via LoadFrom and reset in their own
// Dispose.
//
// Unique board keys are NOT enough to make that safe: UserSettingsTests.Dispose replaces the whole
// shared _data object and _settingsFilePath, and the splitter tests below read/write plain scalars
// (WorkbooksLeftPanelWidth/WorkbooksEntryListWidth) that UserSettingsTests writes too. What makes
// it safe is xunit.runner.json's "parallelizeTestCollections": false - every collection in this
// assembly runs sequentially, so no collection can race another's statics. Keep that file; without
// it this class races "UserSettings" intermittently.
[Collection("HeadlessUi")]
public sealed class WorkbooksListTests : IDisposable
{
    private readonly TempWorkspace thisWorkspace = new();

    // A board key unique to THIS test instance (xunit constructs the class per test), so the
    // UserSettings.ActiveWorkbookIdByBoard entry that BuildTab's activation now writes cannot leak
    // into the next test and pre-select a workbook it never activated. Was a shared literal back
    // when selection was set on an in-memory field and nothing was persisted.
    private readonly string thisBoardKey = "Commodore 64|250469 " + Guid.NewGuid().ToString("N");


    public void Dispose()
    {
        // Detach from the temp folder so nothing written later can reach the user's real one.
        this.LoadWorklog();
        this.thisWorkspace.Dispose();
    }

    private string LoadWorklog()
    {
        string root = this.thisWorkspace.Path_("Workbook-" + Guid.NewGuid().ToString("N"));
        WorklogManager.LoadFrom(root);
        return root;
    }

    // Stands in for Main.ActivateWorkbook, doing exactly what it does: persist the choice to
    // UserSettings.ActiveWorkbookIdByBoard, then refresh - Main goes via RefreshWorklogBar, which
    // calls straight back into RefreshWorkbooks, so RefreshWorkbooks IS the refresh here.
    //
    // Injecting this rather than letting the tab branch on "MainWindow == null" is the whole point:
    // with a branch, every test below ran a test-only fallback that set the selected id directly,
    // while the shipped path - persist, then re-derive the selection from the saved id - was pinned
    // by nothing. Now there is one path and these tests are on it.
    private static void InstallActivation(TabWorkbooks tab)
    {
        tab.ActivateWorkbookOverrideForTests = (boardKey, workbookId) =>
        {
            UserSettings.SetActiveWorkbookId(boardKey, workbookId);
            tab.RefreshWorkbooks();
        };
    }

    // The tab reads its board key from the main window, which no test constructs. Passing the key
    // in directly keeps these tests to the panel itself rather than to Main's combo boxes.
    private static TabWorkbooks BuildTab(string boardKey)
    {
        var tab = new TabWorkbooks { BoardKeyOverrideForTests = boardKey };
        InstallActivation(tab);
        tab.RefreshWorkbooks();
        return tab;
    }

    private static StackPanel ListPanel(TabWorkbooks tab) =>
        tab.GetControl<StackPanel>("WorkbookListPanel");

    private static string CountText(TabWorkbooks tab) =>
        tab.GetControl<TextBlock>("WorkbookCountText").Text ?? string.Empty;

    // Every TextBlock in a card, in visual order - the card is built in code, so this is how a
    // test reads back what it actually rendered.
    private static List<string> CardTexts(Control card) =>
        card.GetSelfAndVisualDescendants()
            .OfType<TextBlock>()
            .Select(t => t.Text ?? string.Empty)
            .ToList();

    private static List<string> CardTexts(TabWorkbooks tab, int index) =>
        CardTexts((Control)ListPanel(tab).Children[index]);

    // The colour a card's status label is painted in, looked up by the word it shows.
    private static Color StatusLabelColour(TabWorkbooks tab, string status)
    {
        var label = ((Control)ListPanel(tab).Children[0])
            .GetSelfAndVisualDescendants()
            .OfType<TextBlock>()
            .FirstOrDefault(t => t.Text == status);

        Assert.NotNull(label);
        return ((ISolidColorBrush)label!.Foreground!).Color;
    }

    [Fact]
    public void A_board_with_no_workbooks_shows_zero_and_the_empty_hint()
    {
        this.LoadWorklog();

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);

            // "0 workbooks", not "0 workbook" - the plural rule has to hold at zero as well as
            // at many.
            Assert.Equal("0 workbooks", CountText(tab));
            Assert.Empty(ListPanel(tab).Children);
            Assert.True(tab.GetControl<TextBlock>("NoWorkbooksText").IsVisible);
        });
    }

    // The heading is a count, so it has to read correctly at one. "1 workbooks" is the classic
    // way this goes wrong and is exactly what a naive $"{n} workbooks" would produce.
    [Fact]
    public void A_single_workbook_is_counted_in_the_singular()
    {
        this.LoadWorklog();
        WorklogManager.CreateWorkbook(this.thisBoardKey, "No picture, black screen", "");

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);

            Assert.Equal("1 workbook", CountText(tab));
            Assert.Single(ListPanel(tab).Children);
            Assert.False(tab.GetControl<TextBlock>("NoWorkbooksText").IsVisible);
        });
    }

    [Fact]
    public void Each_card_shows_its_id_title_and_worklog_count_with_the_start_date()
    {
        this.LoadWorklog();
        var workbook = WorklogManager.CreateWorkbook(this.thisBoardKey, "No picture, black screen", "");
        Assert.NotNull(workbook);

        // "0 worklogs · started 2026-September-01" - the date format is the worklog bar's
        // (yyyy-MMMM-dd, invariant), so a workbook's start date reads the same in both places.
        string expectedDate = workbook!.StartDate.ToString("yyyy-MMMM-dd", System.Globalization.CultureInfo.InvariantCulture);

        UiTest.Run(() =>
        {
            var texts = CardTexts(BuildTab(this.thisBoardKey), 0);

            Assert.Contains($"#{workbook.Id}", texts);
            Assert.Contains("No picture, black screen", texts);
            Assert.Contains($"0 worklogs · started {expectedDate}", texts);
        });
    }

    // The count on the card is worklog ENTRIES, and it has to move when entries are added - it is
    // a stored field on the record, not something recomputed at read time, so a stale EntryCount
    // is a real possibility rather than a theoretical one.
    [Fact]
    public void The_card_counts_worklogs_and_uses_the_singular_for_one()
    {
        this.LoadWorklog();
        var workbook = WorklogManager.CreateWorkbook(this.thisBoardKey, "Dead PLA", "");
        Assert.NotNull(workbook);

        WorklogManager.AddEntry(workbook!.Id, "Sch", new Avalonia.Rect(0, 0, 1, 1), "Checked PLA", "", "Issue", "Open", Array.Empty<string>());

        UiTest.Run(() =>
        {
            var texts = CardTexts(BuildTab(this.thisBoardKey), 0);
            Assert.Contains(texts, t => t.StartsWith("1 worklog ·", StringComparison.Ordinal));
        });

        WorklogManager.AddEntry(workbook.Id, "Sch", new Avalonia.Rect(0, 0, 1, 1), "Replaced PLA", "", "Issue", "Open", Array.Empty<string>());

        UiTest.Run(() =>
        {
            var texts = CardTexts(BuildTab(this.thisBoardKey), 0);
            Assert.Contains(texts, t => t.StartsWith("2 worklogs ·", StringComparison.Ordinal));
        });
    }

    // Newest first, matching GetWorkbooksForBoard. The user reads these as a history, and the id
    // is printed on every card, so an order that is not descending id looks sorted by nothing.
    [Fact]
    public void Workbooks_are_listed_newest_first()
    {
        this.LoadWorklog();
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Oldest", "");
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Middle", "");
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Newest", "");

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);

            var titles = ListPanel(tab).Children
                .Select(child => CardTexts((Control)child))
                .Select(texts => texts.FirstOrDefault(t => t is "Oldest" or "Middle" or "Newest"))
                .ToList();

            Assert.Equal(new[] { "Newest", "Middle", "Oldest" }, titles);
            Assert.Equal("3 workbooks", CountText(tab));
        });
    }

    // The status pill is the one piece of this card shared with the rest of the worklog feature -
    // the worklog bar and an entry's own state pill use the same two brushes and the same padlock
    // glyphs. An Open workbook showing a different red here than in the bar is exactly the drift
    // that WorklogStatusBrushTests was written to stop, so it is asserted rather than assumed.
    [Fact]
    public void An_open_workbook_pill_is_painted_with_the_shared_open_brush()
    {
        this.LoadWorklog();
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Still going", "");

        UiTest.Run(() =>
        {
            Assert.Equal(Colors.IndianRed, StatusLabelColour(BuildTab(this.thisBoardKey), "Open"));
        });
    }

    // A workbook closes when all its entries are resolved, and the card must follow - showing
    // "Open" on a finished repair would be worse than showing no status at all.
    [Fact]
    public void A_closed_workbook_pill_is_painted_with_the_shared_closed_brush()
    {
        this.LoadWorklog();
        var workbook = WorklogManager.CreateWorkbook(this.thisBoardKey, "Finished", "");
        Assert.NotNull(workbook);

        // Status is derived, so this closes the workbook the way the app does rather than by
        // writing the field.
        WorklogManager.AddEntry(workbook!.Id, "Sch", new Avalonia.Rect(0, 0, 1, 1), "Sorted", "", "Issue", "Closed", Array.Empty<string>());

        UiTest.Run(() =>
        {
            Assert.Equal(Color.Parse("#4C8C31"), StatusLabelColour(BuildTab(this.thisBoardKey), "Closed"));
        });
    }

    // A workbook can be created without a title, and the card's middle line would then be blank -
    // which reads as a broken card rather than as an unnamed one.
    [Fact]
    public void A_workbook_with_no_title_falls_back_to_a_placeholder()
    {
        this.LoadWorklog();
        WorklogManager.CreateWorkbook(this.thisBoardKey, "   ", "");

        UiTest.Run(() =>
        {
            Assert.Contains("(untitled)", CardTexts(BuildTab(this.thisBoardKey), 0));
        });
    }

    // The list is scoped to the selected board. Showing another board's repairs under this
    // board's name would be worse than showing none.
    [Fact]
    public void Only_the_selected_boards_workbooks_are_listed()
    {
        this.LoadWorklog();
        WorklogManager.CreateWorkbook(this.thisBoardKey, "C64 repair", "");
        WorklogManager.CreateWorkbook("Amiga 500|A500", "Amiga repair", "");

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);

            Assert.Equal("1 workbook", CountText(tab));
            Assert.Contains("C64 repair", CardTexts(tab, 0));
        });
    }

    // With no board selected the tab has no key at all, which is the state it is constructed in
    // before Main has wired it up. It must render the empty state rather than throwing or listing
    // every workbook on disk.
    [Fact]
    public void No_selected_board_shows_the_empty_state_rather_than_everything_on_disk()
    {
        this.LoadWorklog();
        WorklogManager.CreateWorkbook(this.thisBoardKey, "C64 repair", "");

        UiTest.Run(() =>
        {
            var tab = BuildTab(string.Empty);

            Assert.Equal("0 workbooks", CountText(tab));
            Assert.Empty(ListPanel(tab).Children);
        });
    }

    // ---------------------------------------------------------------------------------------
    // Selection: clicking a card in the left panel (SelectWorkbookForTests stands in for that
    // click - see its comment) selects that workbook for the top-line above the board/entry-list
    // split. The board, pins and entry list themselves are still the mockup's hardcoded sample
    // markup and are not asserted on here.
    // ---------------------------------------------------------------------------------------

    private static bool IsCardSelected(Control card) => card.Classes.Contains("Selected");

    private static string HeaderTitleText(TabWorkbooks tab) =>
        tab.GetControl<TextBlock>("WorkbookHeaderTitleText").Text ?? string.Empty;

    private static string HeaderStatusText(TabWorkbooks tab) =>
        tab.GetControl<TextBlock>("WorkbookHeaderStatusText").Text ?? string.Empty;

    private static bool HeaderPillVisible(TabWorkbooks tab) =>
        tab.GetControl<Border>("WorkbookHeaderStatusPill").IsVisible;

    // The newest workbook is picked automatically, matching what the user sees: it is the top
    // card in the list (GetWorkbooksForBoard is newest first), so having ANY other one
    // pre-selected would show a selected card that is not the one on screen looking selected.
    [Fact]
    public void The_newest_workbook_is_selected_by_default()
    {
        this.LoadWorklog();
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Older", "");
        var newest = WorklogManager.CreateWorkbook(this.thisBoardKey, "Newer", "");
        Assert.NotNull(newest);

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);

            Assert.True(IsCardSelected((Control)ListPanel(tab).Children[0]));
            Assert.False(IsCardSelected((Control)ListPanel(tab).Children[1]));
            Assert.Equal($"#{newest!.Id} · Newer", HeaderTitleText(tab));
        });
    }

    // Clicking the card that is ALREADY highlighted must still activate it.
    //
    // RefreshWorkbooks highlights the newest workbook when the board has no saved activation, but
    // saves nothing - so that highlight is an assumption, not an activation. SelectWorkbook used to
    // return early on "the id I was given is the id I already show", which made clicking that exact
    // card a no-op: the card looked active while ActiveWorkbookIdByBoard stayed empty, and creating
    // a newer workbook afterwards then silently moved the user off it (no saved id -> fall back to
    // newest). The only click that would have overwritten the assumption was the one the guard
    // swallowed, so the user had to click a different card and click back to make it stick.
    [Fact]
    public void Clicking_the_card_that_is_already_highlighted_still_persists_the_activation()
    {
        this.LoadWorklog();
        var only = WorklogManager.CreateWorkbook(this.thisBoardKey, "Only one", "");
        Assert.NotNull(only);

        // Nothing activated yet: the highlight below is RefreshWorkbooks' default, not a choice.
        Assert.Null(UserSettings.GetActiveWorkbookId(this.thisBoardKey));

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);
            Assert.True(IsCardSelected((Control)ListPanel(tab).Children[0]));

            tab.SelectWorkbookForTests(only!.Id);
        });

        Assert.Equal(only!.Id, UserSettings.GetActiveWorkbookId(this.thisBoardKey));
    }

    // The consequence of the bug above, stated as behaviour rather than as a stored value: having
    // clicked the workbook they are looking at, the user must STAY on it when a newer one appears.
    // With the activation unsaved, the newer workbook won the "fall back to newest" rule and the
    // selection moved on its own.
    [Fact]
    public void An_activated_workbook_is_kept_when_a_newer_one_is_created()
    {
        this.LoadWorklog();
        var first = WorklogManager.CreateWorkbook(this.thisBoardKey, "First", "");
        Assert.NotNull(first);

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);

            // The click that the old guard turned into a no-op.
            tab.SelectWorkbookForTests(first!.Id);

            var newer = WorklogManager.CreateWorkbook(this.thisBoardKey, "Newer", "");
            Assert.NotNull(newer);

            tab.RefreshWorkbooks();

            // Newer is the top card (newest first) - the selected one must still be First.
            Assert.Equal($"#{first.Id} · First", HeaderTitleText(tab));
            Assert.True(IsCardSelected((Control)ListPanel(tab).Children[1]));
            Assert.False(IsCardSelected((Control)ListPanel(tab).Children[0]));
        });
    }

    [Fact]
    public void Clicking_a_card_selects_it_and_deselects_the_others()
    {
        this.LoadWorklog();
        var first = WorklogManager.CreateWorkbook(this.thisBoardKey, "First", "");
        var second = WorklogManager.CreateWorkbook(this.thisBoardKey, "Second", "");
        Assert.NotNull(first);
        Assert.NotNull(second);

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);

            // Second is newest, so it starts selected; selecting First must move the highlight
            // rather than merely add it.
            tab.SelectWorkbookForTests(first!.Id);

            Assert.True(IsCardSelected((Control)ListPanel(tab).Children[1]));
            Assert.False(IsCardSelected((Control)ListPanel(tab).Children[0]));
            Assert.Equal($"#{first.Id} · First", HeaderTitleText(tab));
        });
    }

    // The pill has to show the CLICKED workbook's own status, not whatever the default selection
    // happened to be - Open and Closed are visually distinct (different glyph, different colour),
    // so this is the case most likely to silently show the wrong workbook's state.
    [Fact]
    public void Selecting_a_workbook_updates_the_top_line_status_pill()
    {
        this.LoadWorklog();
        var open = WorklogManager.CreateWorkbook(this.thisBoardKey, "Still going", "");
        var closed = WorklogManager.CreateWorkbook(this.thisBoardKey, "Finished", "");
        Assert.NotNull(open);
        Assert.NotNull(closed);

        // Status is derived, so this closes it the way the app does rather than by writing the
        // field.
        WorklogManager.AddEntry(closed!.Id, "Sch", new Avalonia.Rect(0, 0, 1, 1), "Sorted", "", "Issue", "Closed", Array.Empty<string>());

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);

            tab.SelectWorkbookForTests(closed.Id);
            Assert.Equal("Closed", HeaderStatusText(tab));
            Assert.Equal(Color.Parse("#4C8C31"), ((ISolidColorBrush)tab.GetControl<TextBlock>("WorkbookHeaderStatusText").Foreground!).Color);

            tab.SelectWorkbookForTests(open!.Id);
            Assert.Equal("Open", HeaderStatusText(tab));
            Assert.Equal(Colors.IndianRed, ((ISolidColorBrush)tab.GetControl<TextBlock>("WorkbookHeaderStatusText").Foreground!).Color);
        });
    }

    // A board with no workbooks has nothing to select - the header must say so rather than show
    // a stale title from whatever board was selected before, and the pill (which has no status to
    // show) must not linger on screen either.
    [Fact]
    public void A_board_with_no_workbooks_clears_the_top_line()
    {
        this.LoadWorklog();

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);

            Assert.Equal("No workbook selected", HeaderTitleText(tab));
            Assert.False(HeaderPillVisible(tab));
        });
    }

    // Refreshing the list (a board edit, a new entry elsewhere) must not silently reset the
    // user's selection back to the newest workbook - that would be surprising every time
    // something else on screen changed while they were looking at an older repair.
    [Fact]
    public void A_refresh_keeps_the_existing_selection_when_it_still_exists()
    {
        this.LoadWorklog();
        var older = WorklogManager.CreateWorkbook(this.thisBoardKey, "Older", "");
        Assert.NotNull(older);

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);
            tab.SelectWorkbookForTests(older!.Id);

            // A newer workbook now exists; refreshing must not silently jump the selection to it.
            WorklogManager.CreateWorkbook(this.thisBoardKey, "Newer", "");
            tab.RefreshWorkbooks();

            Assert.Equal($"#{older.Id} · Older", HeaderTitleText(tab));
            Assert.True(IsCardSelected((Control)ListPanel(tab).Children[1]));
        });
    }

    // UserSettings.ActiveWorkbookIdByBoard - what Main.ActivateWorkbook persists when the user
    // clicks a card WITH a real MainWindow - is the primary source RefreshWorkbooks reads, ahead
    // of "newest". This is what makes activating an older workbook from this tab stick across a
    // refresh even before SelectWorkbook's own in-memory fallback would apply.
    //
    // A dedicated board key, not shared with any other test in this file: this test is the only
    // one in the "HeadlessUi" collection that writes to UserSettings.ActiveWorkbookIdByBoard (see
    // the file header's COLLECTION NOTE), and an own key means it needs no cleanup to avoid
    // leaking a saved id to a test that runs after it in the same process.
    [Fact]
    public void A_workbook_activated_via_UserSettings_is_selected_ahead_of_the_newest_one()
    {
        this.LoadWorklog();
        const string boardKey = "Commodore 64|250407 (UserSettings activation test)";

        var older = WorklogManager.CreateWorkbook(boardKey, "Older", "");
        var newer = WorklogManager.CreateWorkbook(boardKey, "Newer", "");
        Assert.NotNull(older);
        Assert.NotNull(newer);

        UserSettings.SetActiveWorkbookId(boardKey, older!.Id);

        UiTest.Run(() =>
        {
            var tab = BuildTab(boardKey);

            // Newer is the higher id and would win the "fall back to newest" rule on its own -
            // this only passes if the saved activation is actually consulted first.
            Assert.Equal($"#{older.Id} · Older", HeaderTitleText(tab));
            Assert.True(IsCardSelected((Control)ListPanel(tab).Children[1]));
        });
    }

    // A saved activation naming a workbook that no longer exists on this board (deleted by hand,
    // or left over from before the board's workbooks were cleared) must not leave the panel with
    // nothing selected - it falls back to newest, the same as when nothing was ever activated.
    [Fact]
    public void A_stale_activated_workbook_id_falls_back_to_the_newest_workbook()
    {
        this.LoadWorklog();
        const string boardKey = "Commodore 64|250407 (stale activation test)";

        UserSettings.SetActiveWorkbookId(boardKey, 9999);
        var only = WorklogManager.CreateWorkbook(boardKey, "Only one", "");
        Assert.NotNull(only);

        UiTest.Run(() =>
        {
            var tab = BuildTab(boardKey);

            Assert.Equal($"#{only!.Id} · Only one", HeaderTitleText(tab));
            Assert.True(IsCardSelected((Control)ListPanel(tab).Children[0]));
        });
    }

    // The two splitter widths (left workbook list / rest, board pane / entry list) are restored
    // from UserSettings.WorkbooksLeftPanelWidth/WorkbooksEntryListWidth via
    // ApplySplitterWidthsForTests, the same seam Initialize calls in the running app.
    [Fact]
    public void Splitter_widths_are_restored_from_UserSettings()
    {
        this.LoadWorklog();

        double savedLeftPanelWidth = UserSettings.WorkbooksLeftPanelWidth;
        double savedEntryListWidth = UserSettings.WorkbooksEntryListWidth;

        try
        {
            // Written explicitly rather than read back off whatever UserSettings happens to hold, so
            // the assertion is against a known value and cannot pass by coincidence. Both are inside
            // the clamp range below, which is the point of this test as distinct from the next one.
            UserSettings.WorkbooksLeftPanelWidth = 260.0;
            UserSettings.WorkbooksEntryListWidth = 310.0;

            UiTest.Run(() =>
            {
                var tab = BuildTab(this.thisBoardKey);
                tab.ApplySplitterWidthsForTests();

                Assert.Equal(260.0, tab.GetControl<Grid>("OuterSplitGrid").ColumnDefinitions[0].Width.Value);
                Assert.Equal(310.0, tab.GetControl<Grid>("BoardEntrySplitGrid").ColumnDefinitions[2].Width.Value);
            });
        }
        finally
        {
            UserSettings.WorkbooksLeftPanelWidth = savedLeftPanelWidth;
            UserSettings.WorkbooksEntryListWidth = savedEntryListWidth;
        }
    }

    // A width saved on a large monitor is applied verbatim on a small one - these are raw pixels,
    // and ApplySplitterWidths runs once from Initialize, before the window has a size to compare
    // against. Restoring a 1100px panel on a 1366px screen squeezed everything else to near-zero and
    // put the splitter off-screen, leaving the tab unusable with no way back except hand-editing
    // settings.json. A width dragged shut to nothing had the mirror problem: no panel and no
    // splitter left to grab.
    [Theory]
    [InlineData(4000.0, 900.0)]   // absurdly wide - clamped down to the ceiling
    [InlineData(0.0, 120.0)]      // dragged shut - raised to the floor
    [InlineData(-50.0, 120.0)]    // nonsense - same
    public void An_out_of_range_saved_width_is_clamped_to_something_usable(double saved, double expected)
    {
        this.LoadWorklog();

        double savedLeftPanelWidth = UserSettings.WorkbooksLeftPanelWidth;
        double savedEntryListWidth = UserSettings.WorkbooksEntryListWidth;

        try
        {
            UserSettings.WorkbooksLeftPanelWidth = saved;
            UserSettings.WorkbooksEntryListWidth = saved;

            UiTest.Run(() =>
            {
                var tab = BuildTab(this.thisBoardKey);
                tab.ApplySplitterWidthsForTests();

                Assert.Equal(expected, tab.GetControl<Grid>("OuterSplitGrid").ColumnDefinitions[0].Width.Value);
                Assert.Equal(expected, tab.GetControl<Grid>("BoardEntrySplitGrid").ColumnDefinitions[2].Width.Value);
            });
        }
        finally
        {
            UserSettings.WorkbooksLeftPanelWidth = savedLeftPanelWidth;
            UserSettings.WorkbooksEntryListWidth = savedEntryListWidth;
        }
    }

    // The SAVE half of the same pair, and the thing the restore test above cannot see: the tab
    // originally wired both handlers with a PointerReleased="..." attribute in the markup, and
    // GridSplitter marks that event handled as it finishes its own drag - so neither handler ever
    // ran and neither width was ever written, while the restore test stayed green over a setting
    // nothing could change.
    //
    // The assertion that matters is therefore the "handled" part: the event is raised with
    // Handled ALREADY true, exactly as it arrives from a real GridSplitter drag. Only an AddHandler
    // subscription with handledEventsToo: true sees it. The handler defers its read through
    // Dispatcher.UIThread.Post (the width is not in Bounds yet at release time), so RunJobs is
    // needed before reading the setting back.
    [Fact]
    public void A_finished_splitter_drag_saves_both_widths_even_though_the_splitter_handled_the_event()
    {
        this.LoadWorklog();

        double savedLeftPanelWidth = UserSettings.WorkbooksLeftPanelWidth;
        double savedEntryListWidth = UserSettings.WorkbooksEntryListWidth;

        try
        {
            UiTest.Run(() =>
            {
                var tab = BuildTab("Commodore 64|250407 (splitter save test)");
                tab.WireSplitterPersistenceForTests();

                // A real arranged size, so the Bounds the handlers read are not zero and the
                // saved values are distinguishable from the defaults.
                var window = new Window { Width = 1200, Height = 700, Content = tab };
                try
                {
                    window.Show();
                    window.Measure(new Size(1200, 700));
                    window.Arrange(new Rect(0, 0, 1200, 700));
                    Dispatcher.UIThread.RunJobs();

                    // Deliberately pre-handled - see this test's header.
                    RaiseHandledPointerReleased(tab.GetControl<GridSplitter>("OuterSplitter"));
                    RaiseHandledPointerReleased(tab.GetControl<GridSplitter>("BoardEntrySplitter"));
                    Dispatcher.UIThread.RunJobs();

                    Assert.Equal(
                        tab.GetControl<Border>("WorkbookListBorder").Bounds.Width,
                        UserSettings.WorkbooksLeftPanelWidth);
                    Assert.Equal(
                        tab.GetControl<Border>("SelectedSchematicEntriesBorder").Bounds.Width,
                        UserSettings.WorkbooksEntryListWidth);
                }
                finally
                {
                    window.Close();
                }
            });
        }
        finally
        {
            // These are plain app-wide scalars with no per-board key to keep this test's writes
            // away from anything else that reads them, so put them back.
            UserSettings.WorkbooksLeftPanelWidth = savedLeftPanelWidth;
            UserSettings.WorkbooksEntryListWidth = savedEntryListWidth;
        }
    }

    // A PointerReleased carrying Handled = true, the state a GridSplitter leaves it in after its
    // own drag. Raised directly rather than driven through window.MouseDown/MouseUp because the
    // point is the handled flag, not the gesture.
    private static void RaiseHandledPointerReleased(GridSplitter splitter)
    {
        var args = new PointerReleasedEventArgs(
            splitter,
            new Pointer(0, PointerType.Mouse, isPrimary: true),
            splitter,
            default,
            0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None,
            MouseButton.Left)
        {
            RoutedEvent = InputElement.PointerReleasedEvent,
            Handled = true,
        };

        splitter.RaiseEvent(args);
    }

    // ---------------------------------------------------------------------------------------
    // The top-line's Note text and its Edit/Delete workbook actions
    // (WorkbookHeaderNoteText/WorkbookHeaderActionsPanel) - not the click handlers themselves
    // (those open a real modal via ShowDialog, which needs a live Window and is exercised by
    // hand, not headlessly - see BUILDING.md), but everything ApplyHeaderForWorkbook decides
    // about what is on screen for a given workbook.
    // ---------------------------------------------------------------------------------------

    private static TextBlock HeaderNoteBlock(TabWorkbooks tab) =>
        tab.GetControl<TextBlock>("WorkbookHeaderNoteText");

    private static bool HeaderActionsVisible(TabWorkbooks tab) =>
        tab.GetControl<StackPanel>("WorkbookHeaderActionsPanel").IsVisible;

    [Fact]
    public void The_top_line_shows_the_selected_workbooks_note()
    {
        this.LoadWorklog();
        WorklogManager.CreateWorkbook(this.thisBoardKey, "C64 job", "Bought at auction, no picture");

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);

            Assert.True(HeaderNoteBlock(tab).IsVisible);
            Assert.Equal("Bought at auction, no picture", HeaderNoteBlock(tab).Text);
        });
    }

    // Most workbooks are created without a note (it is optional in the create dialog) - the row
    // must collapse rather than show an empty muted TextBlock next to the status pill.
    [Fact]
    public void A_blank_note_is_hidden_rather_than_shown_empty()
    {
        this.LoadWorklog();
        WorklogManager.CreateWorkbook(this.thisBoardKey, "C64 job", "");

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);

            Assert.False(HeaderNoteBlock(tab).IsVisible);
        });
    }

    // Switching between two selected workbooks must show the CLICKED one's own note, not the
    // first one's left over from before - the same class of bug the status-pill test above guards.
    [Fact]
    public void Selecting_a_different_workbook_updates_the_shown_note()
    {
        this.LoadWorklog();
        var withNote = WorklogManager.CreateWorkbook(this.thisBoardKey, "Has a note", "Leaking cap near U4");
        var withoutNote = WorklogManager.CreateWorkbook(this.thisBoardKey, "No note", "");
        Assert.NotNull(withNote);
        Assert.NotNull(withoutNote);

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);

            // withoutNote is newest, so it starts selected.
            Assert.False(HeaderNoteBlock(tab).IsVisible);

            tab.SelectWorkbookForTests(withNote!.Id);
            Assert.True(HeaderNoteBlock(tab).IsVisible);
            Assert.Equal("Leaking cap near U4", HeaderNoteBlock(tab).Text);

            tab.SelectWorkbookForTests(withoutNote!.Id);
            Assert.False(HeaderNoteBlock(tab).IsVisible);
        });
    }

    // The Edit/Delete actions act on "whichever workbook the top-line shows", so they must appear
    // exactly when that line names a real workbook and disappear exactly when it does not -
    // otherwise a click on either would have no workbook to act on.
    [Fact]
    public void The_edit_and_delete_actions_are_hidden_when_no_workbook_is_selected()
    {
        this.LoadWorklog();

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);

            Assert.Equal("No workbook selected", HeaderTitleText(tab));
            Assert.False(HeaderActionsVisible(tab));
        });
    }

    [Fact]
    public void The_edit_and_delete_actions_are_shown_once_a_workbook_is_selected()
    {
        this.LoadWorklog();
        WorklogManager.CreateWorkbook(this.thisBoardKey, "C64 job", "");

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);

            Assert.True(HeaderActionsVisible(tab));
        });
    }

    // ---------------------------------------------------------------------------------------
    // WorklogManager.DeleteWorkbook, from the list's point of view: the deleted card must be
    // gone from the list and the panel must land on one of the workbooks that remains - the
    // click handler itself (OnDeleteWorkbookClick) additionally shows a confirmation modal via
    // ShowDialog, which is exercised by hand rather than headlessly, per this file's own note
    // above.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Deleting_the_selected_workbook_and_refreshing_selects_the_next_one_in_the_list()
    {
        this.LoadWorklog();
        var older = WorklogManager.CreateWorkbook(this.thisBoardKey, "Older job", "");
        var newer = WorklogManager.CreateWorkbook(this.thisBoardKey, "Newer job", "");
        Assert.NotNull(older);
        Assert.NotNull(newer);

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);

            // Newer starts selected (newest-first default).
            Assert.Equal($"#{newer!.Id} · Newer job", HeaderTitleText(tab));

            Assert.True(WorklogManager.DeleteWorkbook(newer.Id));
            tab.RefreshWorkbooks();

            // ResolveActiveWorkbook's stale-id fallback (the saved activation, if any, no longer
            // names a real workbook) lands the selection on the only one left.
            Assert.Equal($"#{older!.Id} · Older job", HeaderTitleText(tab));
            Assert.Single(ListPanel(tab).Children);
        });
    }

    // Deleting the board's only workbook must clear the top-line back to its empty state rather
    // than leave a stale title or a status pill with nothing behind it.
    [Fact]
    public void Deleting_the_only_workbook_clears_the_top_line()
    {
        this.LoadWorklog();
        var only = WorklogManager.CreateWorkbook(this.thisBoardKey, "Only one", "");
        Assert.NotNull(only);

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);

            Assert.True(WorklogManager.DeleteWorkbook(only!.Id));
            tab.RefreshWorkbooks();

            Assert.Equal("No workbook selected", HeaderTitleText(tab));
            Assert.False(HeaderPillVisible(tab));
            Assert.False(HeaderActionsVisible(tab));
            Assert.Empty(ListPanel(tab).Children);
        });
    }
}
