using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.VisualTree;
using CRT;
using Handlers.DataHandling;

namespace ClassicRepairToolbox.Tests.Ui;

// The Workbooks tab's "Find a previous repair" box: what it filters, and the highlighting it puts
// on the runs that matched.
//
// The query LANGUAGE itself is pinned down in WorklogSearchQueryTests (pure, no UI) and which
// fields are searched in WorklogSearchIndexTests - these tests are only about the tab actually
// applying them: cards disappearing, entry lists narrowing, and matched text rendering as marked
// Runs rather than plain Text.
//
// COLLECTION NOTE: "HeadlessUi" for the same reason WorkbooksListTests is - it constructs a
// control and so needs the shared dispatcher thread, while also driving WorklogManager's and
// UserSettings' statics. See that file's own header for why that is safe here.
[Collection("HeadlessUi")]
public sealed class WorkbooksSearchTests : IDisposable
{
    private readonly TempWorkspace thisWorkspace = new();

    private readonly string thisBoardKey = "Commodore 64|250469 " + Guid.NewGuid().ToString("N");

    public void Dispose()
    {
        this.LoadWorklog();
        this.thisWorkspace.Dispose();
    }

    private string LoadWorklog()
    {
        string root = this.thisWorkspace.Path_("Workbook-" + Guid.NewGuid().ToString("N"));
        WorklogManager.LoadFrom(root);
        return root;
    }

    private static TabWorkbooks BuildTab(string boardKey)
    {
        var tab = new TabWorkbooks { BoardKeyOverrideForTests = boardKey };
        tab.ActivateWorkbookOverrideForTests = (key, workbookId) =>
        {
            UserSettings.SetActiveWorkbookId(key, workbookId);
            tab.RefreshWorkbooks();
        };
        tab.RefreshWorkbooks();
        return tab;
    }

    // Types into the real search box, then refreshes.
    //
    // The explicit RefreshWorkbooks is NOT the test taking a shortcut past the handler: these tabs
    // are never attached to a visual tree (no test constructs Main), and a detached TextBox does
    // not raise TextChanged, so the tab's own OnFindRepairTextChanged cannot fire here. Setting the
    // real control's Text and then refreshing exercises the same path the running app takes, since
    // RefreshWorkbooks deliberately re-reads the box rather than trusting a cached copy - which is
    // exactly what makes the filter survive every OTHER refresh trigger too (a board switch, an
    // entry save), not just a keystroke.
    private static void Search(TabWorkbooks tab, string query)
    {
        tab.GetControl<TextBox>("FindRepairTextBox").Text = query;
        tab.RefreshWorkbooks();
    }

    private static StackPanel ListPanel(TabWorkbooks tab) =>
        tab.GetControl<StackPanel>("WorkbookListPanel");

    private static StackPanel EntriesPanel(TabWorkbooks tab) =>
        tab.GetControl<StackPanel>("SelectedSchematicEntriesPanel");

    private static string CountText(TabWorkbooks tab) =>
        tab.GetControl<TextBlock>("WorkbookCountText").Text ?? string.Empty;

    // A TextBlock's visible text whether it was set as plain Text or built from highlighted Runs -
    // once a search marks part of a block, Text is null and the content lives in Inlines, so a
    // reader that only looked at Text would see every highlighted card as blank.
    private static string VisibleText(TextBlock block)
    {
        if (block.Text != null)
            return block.Text;

        return block.Inlines == null
            ? string.Empty
            : string.Concat(block.Inlines.OfType<Run>().Select(r => r.Text));
    }

    // The top-line's title, read the Inlines-aware way - a search matching the title highlights it,
    // which leaves TextBlock.Text null.
    private static string HeaderText(TabWorkbooks tab) =>
        VisibleText(tab.GetControl<TextBlock>("WorkbookHeaderTitleText"));

    private static List<string> TextsIn(Control root) =>
        root.GetSelfAndVisualDescendants()
            .OfType<TextBlock>()
            .Select(VisibleText)
            .ToList();

    // The runs a search marked, across a whole subtree - what the user sees highlighted.
    private static List<string> HighlightedRuns(Control root) =>
        root.GetSelfAndVisualDescendants()
            .OfType<TextBlock>()
            .Where(b => b.Inlines != null)
            .SelectMany(b => b.Inlines!.OfType<Run>())
            .Where(r => r.Background != null)
            .Select(r => r.Text ?? string.Empty)
            .ToList();

    // -------------------------------------------------------------- Filtering the workbook list

    [Fact]
    public void An_empty_search_box_shows_every_workbook()
    {
        this.LoadWorklog();
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Black screen", "");
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Dead PLA", "");

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);

            // An empty box is not a filter - clearing the field must bring everything back rather
            // than leaving the tab blank.
            Assert.Equal(2, ListPanel(tab).Children.Count);

            Search(tab, "black");
            Assert.Single(ListPanel(tab).Children);

            Search(tab, "");
            Assert.Equal(2, ListPanel(tab).Children.Count);
        });
    }

    [Fact]
    public void A_workbook_is_found_by_its_own_title()
    {
        this.LoadWorklog();
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Black screen", "");
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Dead PLA", "");

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);
            Search(tab, "pla");

            var card = Assert.Single(ListPanel(tab).Children);
            Assert.Contains("Dead PLA", TextsIn((Control)card));

            // The heading counts what is SHOWN, so it reads as the result count.
            Assert.Equal("1 workbook", CountText(tab));
        });
    }

    [Fact]
    public void A_workbook_is_found_by_its_note()
    {
        this.LoadWorklog();
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Black screen", "collected on tuesday");
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Dead PLA", "");

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);
            Search(tab, "tuesday");

            var card = Assert.Single(ListPanel(tab).Children);
            Assert.Contains("Black screen", TextsIn((Control)card));
        });
    }

    // The point of searching a worklog: find the JOB by something recorded inside it, not just by
    // the title somebody gave the workbook months ago.
    [Fact]
    public void A_workbook_is_found_through_text_in_one_of_its_entries()
    {
        this.LoadWorklog();
        var withEntry = WorklogManager.CreateWorkbook(this.thisBoardKey, "Black screen", "");
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Dead PLA", "");

        WorklogManager.AddEntry(
            withEntry!.Id, "Sheet 1", new Avalonia.Rect(0, 0, 10, 10),
            "Replaced U18", "the 6510 ran hot", "Issue", "Open", Array.Empty<string>());

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);
            Search(tab, "6510");

            var card = Assert.Single(ListPanel(tab).Children);
            Assert.Contains("Black screen", TextsIn((Control)card));
        });
    }

    [Fact]
    public void A_search_matching_nothing_empties_the_list_and_says_so()
    {
        this.LoadWorklog();
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Black screen", "");

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);
            Search(tab, "nothingmatchesthis");

            Assert.Empty(ListPanel(tab).Children);

            // "No results" must not read as "no workbooks recorded" - that looks like data loss.
            var empty = tab.GetControl<TextBlock>("NoWorkbooksText");
            Assert.True(empty.IsVisible);
            Assert.Contains("match your search", empty.Text ?? string.Empty);
        });
    }

    [Fact]
    public void Every_term_must_match_because_space_means_and()
    {
        this.LoadWorklog();
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Black screen on a C64", "");
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Black keyboard", "");

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);

            Search(tab, "black");
            Assert.Equal(2, ListPanel(tab).Children.Count);

            Search(tab, "black screen");
            Assert.Single(ListPanel(tab).Children);
        });
    }

    [Fact]
    public void A_minus_term_excludes_matching_workbooks()
    {
        this.LoadWorklog();
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Black screen on a C64", "");
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Black keyboard", "");

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);
            Search(tab, "black -keyboard");

            var card = Assert.Single(ListPanel(tab).Children);
            Assert.Contains("Black screen on a C64", TextsIn((Control)card));
        });
    }

    [Fact]
    public void A_quoted_phrase_matches_the_whole_run_including_its_spaces()
    {
        this.LoadWorklog();
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Black screen", "");
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Screen is black", "");

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);

            // Unquoted, both match - the two words appear in each, in some order.
            Search(tab, "black screen");
            Assert.Equal(2, ListPanel(tab).Children.Count);

            // Quoted, only the one carrying that exact contiguous run does.
            Search(tab, "\"black screen\"");
            var card = Assert.Single(ListPanel(tab).Children);
            Assert.Contains("Black screen", TextsIn((Control)card));
        });
    }

    [Fact]
    public void Searching_is_not_case_sensitive()
    {
        this.LoadWorklog();
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Black SCREEN", "");

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);
            Search(tab, "screen");

            Assert.Single(ListPanel(tab).Children);
        });
    }

    // -------------------------------------------------------------- Filtering the entry list

    [Fact]
    public void The_entry_list_narrows_to_the_entries_that_matched()
    {
        this.LoadWorklog();
        var workbook = WorklogManager.CreateWorkbook(this.thisBoardKey, "Black screen", "");

        WorklogManager.AddEntry(
            workbook!.Id, "Sheet 1", new Avalonia.Rect(0, 0, 10, 10),
            "Replaced U18", "the 6510 ran hot", "Issue", "Open", Array.Empty<string>());
        WorklogManager.AddEntry(
            workbook.Id, "Sheet 1", new Avalonia.Rect(0, 0, 10, 10),
            "Recapped the PSU", "", "Issue", "Open", Array.Empty<string>());

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);
            tab.CurrentBoardDataOverrideForTests = this.BoardDataWithSchematic("Sheet 1");
            tab.RefreshBoardPreviewsForCurrentSelection();

            // Both entries are on the schematic to begin with.
            Assert.Equal(2, EntriesPanel(tab).Children.Count);

            Search(tab, "6510");

            var card = Assert.Single(EntriesPanel(tab).Children);
            Assert.Contains("Replaced U18", TextsIn((Control)card));
        });
    }

    // A workbook found by its OWN text keeps all of its entries visible - the user found the job
    // they were looking for, and blanking its contents would make the result look empty.
    [Fact]
    public void A_workbook_matched_by_its_own_title_still_shows_all_its_entries()
    {
        this.LoadWorklog();
        var workbook = WorklogManager.CreateWorkbook(this.thisBoardKey, "Jensens C64", "");

        WorklogManager.AddEntry(
            workbook!.Id, "Sheet 1", new Avalonia.Rect(0, 0, 10, 10),
            "Replaced U18", "", "Issue", "Open", Array.Empty<string>());
        WorklogManager.AddEntry(
            workbook.Id, "Sheet 1", new Avalonia.Rect(0, 0, 10, 10),
            "Recapped the PSU", "", "Issue", "Open", Array.Empty<string>());

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);
            tab.CurrentBoardDataOverrideForTests = this.BoardDataWithSchematic("Sheet 1");
            tab.RefreshBoardPreviewsForCurrentSelection();

            Search(tab, "jensens");

            Assert.Single(ListPanel(tab).Children);
            Assert.Equal(2, EntriesPanel(tab).Children.Count);
        });
    }

    // -------------------------------------------------------------- The filtered-out active workbook

    // The top-line, its Edit/Delete buttons and the whole right-hand side belong to whatever the tab
    // shows. Leaving that on a workbook the search has hidden meant "Delete workbook" destroyed a
    // workbook that was not in the list - so the shown workbook moves to one that survived, WITHOUT
    // re-activating anything.
    [Fact]
    public void Filtering_out_the_active_workbook_moves_the_top_line_to_a_shown_one()
    {
        this.LoadWorklog();
        var active = WorklogManager.CreateWorkbook(this.thisBoardKey, "Black screen", "");
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Dead PLA", "");

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);

            // Make the FIRST workbook the active one, then search for something only the other has.
            tab.SelectWorkbookForTests(active!.Id);
            Assert.Contains("Black screen", HeaderText(tab));

            Search(tab, "PLA");

            var card = (Control)Assert.Single(ListPanel(tab).Children);
            Assert.Contains("Dead PLA", TextsIn(card));

            // The top-line follows the list rather than naming the hidden workbook.
            Assert.Contains("Dead PLA", HeaderText(tab));
            Assert.DoesNotContain("Black screen", HeaderText(tab));
        });
    }

    // Moving the SHOWN workbook must not move the ACTIVE one: the worklog bar, "Show worklogs" and
    // "Add worklog" all act on the saved active id, and typing in a search box must never redirect
    // where the next drawn entry is written.
    [Fact]
    public void Filtering_does_not_change_which_workbook_is_activated_app_wide()
    {
        this.LoadWorklog();
        var active = WorklogManager.CreateWorkbook(this.thisBoardKey, "Black screen", "");
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Dead PLA", "");

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);
            tab.SelectWorkbookForTests(active!.Id);

            Search(tab, "PLA");

            Assert.Equal(active.Id, UserSettings.GetActiveWorkbookId(this.thisBoardKey));
        });
    }

    // A board change is a change of subject: carrying the query over lands the user on a filtered,
    // often empty list for a board they just picked, with the reason sitting in a box they are not
    // looking at. Main.OnBoardSelectionChanged calls this before it refreshes.
    [Fact]
    public void Clearing_the_search_for_a_board_change_empties_the_box_and_the_filter()
    {
        this.LoadWorklog();
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Black screen", "");
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Dead PLA", "");

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);

            Search(tab, "PLA");
            Assert.Single(ListPanel(tab).Children);

            tab.ClearSearchForBoardChange();
            tab.RefreshWorkbooks();

            Assert.Equal(string.Empty, tab.GetControl<TextBox>("FindRepairTextBox").Text);
            Assert.Equal(2, ListPanel(tab).Children.Count);
        });
    }

    // -------------------------------------------------------------- Highlighting

    [Fact]
    public void The_matched_run_is_highlighted_and_the_rest_is_not()
    {
        this.LoadWorklog();
        WorklogManager.CreateWorkbook(this.thisBoardKey, "This is a text", "");

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);

            // The request's own example: searching "this text" marks both words, nothing else.
            Search(tab, "this text");

            var card = (Control)Assert.Single(ListPanel(tab).Children);
            var highlighted = HighlightedRuns(card);

            Assert.Contains("This", highlighted);
            Assert.Contains("text", highlighted);
            Assert.DoesNotContain(" is a ", highlighted);
        });
    }

    [Fact]
    public void Highlighting_preserves_the_original_casing_and_the_full_text()
    {
        this.LoadWorklog();
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Dead CPU here", "");

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);
            Search(tab, "cpu");

            var card = (Control)Assert.Single(ListPanel(tab).Children);

            // The mark is drawn over the text as written, not over a lowercased copy...
            Assert.Contains("CPU", HighlightedRuns(card));

            // ...and splitting it into runs must not drop or duplicate a character.
            Assert.Contains("Dead CPU here", TextsIn(card));
        });
    }

    [Fact]
    public void Nothing_is_highlighted_when_no_search_is_active()
    {
        this.LoadWorklog();
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Dead CPU here", "");

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);

            var card = (Control)Assert.Single(ListPanel(tab).Children);
            Assert.Empty(HighlightedRuns(card));

            // With no query the block carries plain Text rather than Runs - cheaper to lay out,
            // and there is nothing to mark.
            Assert.Contains("Dead CPU here", TextsIn(card));
        });
    }

    // Clearing the box has to remove the marks as well as restore the rows - a stale highlight left
    // behind would claim a match that is no longer being searched for.
    [Fact]
    public void Clearing_the_search_removes_the_highlighting()
    {
        this.LoadWorklog();
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Dead CPU here", "");

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);

            Search(tab, "cpu");
            Assert.NotEmpty(HighlightedRuns((Control)ListPanel(tab).Children[0]));

            Search(tab, "");
            var card = (Control)Assert.Single(ListPanel(tab).Children);
            Assert.Empty(HighlightedRuns(card));
            Assert.Contains("Dead CPU here", TextsIn(card));
        });
    }

    [Fact]
    public void An_excluded_term_is_never_highlighted()
    {
        this.LoadWorklog();
        WorklogManager.CreateWorkbook(this.thisBoardKey, "Dead PLA here", "");

        UiTest.Run(() =>
        {
            var tab = BuildTab(this.thisBoardKey);
            Search(tab, "dead -cpu");

            var card = (Control)Assert.Single(ListPanel(tab).Children);
            var highlighted = HighlightedRuns(card);

            Assert.Contains("Dead", highlighted);
            Assert.DoesNotContain("cpu", highlighted, StringComparer.OrdinalIgnoreCase);
        });
    }

    // A minimal but genuinely valid 1x1 PNG - the board pane does a plain File-based
    // `new Bitmap(path)`, so the fixture only has to be a file libpng can decode. Same bytes and
    // same reasoning as WorkbooksBoardPreviewTests' own copy.
    private static readonly byte[] OnePixelPng =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x44, 0x41,
        0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
        0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82
    };

    // The tab's own minimal BoardData - just enough for the board pane to have a schematic to group
    // entries under, without standing up a real board Excel file.
    private BoardData BoardDataWithSchematic(string schematicName)
    {
        string imagePath = this.thisWorkspace.Path_(schematicName + ".png");
        File.WriteAllBytes(imagePath, OnePixelPng);

        return new BoardData
        {
            Schematics = new List<BoardSchematicEntry>
            {
                new() { SchematicName = schematicName, SchematicImageFile = imagePath },
            },
        };
    }
}
