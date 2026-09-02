using Avalonia;
using Handlers.DataHandling;

namespace ClassicRepairToolbox.Tests;

// The pure parts of a worklog entry that BOTH tabs rendering one depend on: which components its
// marked area covers, what its Work done rows total, and the singular/plural count wording.
//
// These were private members of two different UserControls, one copy each, and the component-scope
// computation decides whether the "Mark components in scope" checklist appears in the editor modal.
// The Schematics tab and the Workbooks tab are required to open the SAME modal, and a fix applied
// to one copy and missed on the other is exactly the divergence already reported once - so what is
// pinned here is the single shared implementation both now call.
//
// No collection attribute: nothing here touches a static singleton, a control, or the filesystem.
public class WorklogEntryScopeTests
{
    private static BoardData BuildBoardData(params string[] boardLabels)
    {
        var data = new BoardData();
        foreach (string label in boardLabels)
        {
            data.Components.Add(new ComponentEntry
            {
                BoardLabel = label,
                FriendlyName = label + " name",
            });
        }

        return data;
    }

    private static WorklogEntryRecord EntryCovering(string schematicName, Rect area) => new()
    {
        Id = 1,
        SchematicName = schematicName,
        AreaX = area.X,
        AreaY = area.Y,
        AreaWidth = area.Width,
        AreaHeight = area.Height,
    };

    private static Dictionary<string, Dictionary<string, List<Rect>>> RectCache(
        string schematicName, params (string Label, Rect Rect)[] rects)
    {
        var byLabel = new Dictionary<string, List<Rect>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (label, rect) in rects)
        {
            byLabel[label] = new List<Rect> { rect };
        }

        // OrdinalIgnoreCase, matching how Main builds the real cache at all four of its write sites
        // and how TabSchematics declares the field.
        return new Dictionary<string, Dictionary<string, List<Rect>>>(StringComparer.OrdinalIgnoreCase)
        {
            [schematicName] = byLabel,
        };
    }

    [Fact]
    public void Components_whose_highlight_the_area_touches_are_in_scope()
    {
        var boardData = BuildBoardData("U1", "U2");
        var cache = RectCache("Sheet 1",
            ("U1", new Rect(0, 0, 10, 10)),
            ("U2", new Rect(100, 100, 10, 10)));

        var scope = WorklogEntryScope.BuildComponentsInScope(
            boardData, cache, EntryCovering("Sheet 1", new Rect(5, 5, 20, 20)));

        Assert.NotNull(scope);
        Assert.Equal(new[] { "U1" }, scope!.Select(c => c.BoardLabel));
    }

    // NULL and EMPTY are NOT interchangeable to the caller, and this is the reason the distinction is
    // pinned here rather than left to the two call sites: empty means "this area genuinely covers no
    // component", null means "the scope is unknown", and only null leaves the entry's saved
    // ComponentLabels alone. Returning empty for an unknown scope would wipe the user's selection the
    // first time they saved.
    [Fact]
    public void An_area_touching_nothing_is_empty_scope_not_unknown_scope()
    {
        var boardData = BuildBoardData("U1");
        var cache = RectCache("Sheet 1", ("U1", new Rect(100, 100, 10, 10)));

        var scope = WorklogEntryScope.BuildComponentsInScope(
            boardData, cache, EntryCovering("Sheet 1", new Rect(0, 0, 5, 5)));

        Assert.NotNull(scope);
        Assert.Empty(scope!);
    }

    [Fact]
    public void No_board_data_means_the_scope_is_unknown()
    {
        var cache = RectCache("Sheet 1", ("U1", new Rect(0, 0, 10, 10)));

        Assert.Null(WorklogEntryScope.BuildComponentsInScope(
            null, cache, EntryCovering("Sheet 1", new Rect(0, 0, 5, 5))));
    }

    [Fact]
    public void No_highlight_cache_at_all_means_the_scope_is_unknown()
    {
        Assert.Null(WorklogEntryScope.BuildComponentsInScope(
            BuildBoardData("U1"), null, EntryCovering("Sheet 1", new Rect(0, 0, 5, 5))));
    }

    // A cache that exists but has no key for THIS entry's schematic. Reachable on a board load, where
    // the cache is populated by a fire-and-forget task after the previews are already on screen, and
    // on a region switch, where HighlightRectBuilder legitimately drops every highlight belonging to
    // the other region. Unknown, not empty - saving must not discard the entry's component labels
    // just because the rects for its schematic are not loaded.
    [Fact]
    public void A_cache_with_no_entry_for_that_schematic_means_the_scope_is_unknown()
    {
        var cache = RectCache("Sheet 2", ("U1", new Rect(0, 0, 10, 10)));

        Assert.Null(WorklogEntryScope.BuildComponentsInScope(
            BuildBoardData("U1"), cache, EntryCovering("Sheet 1", new Rect(0, 0, 5, 5))));
    }

    [Fact]
    public void An_entry_with_no_schematic_name_has_an_unknown_scope()
    {
        var cache = RectCache("Sheet 1", ("U1", new Rect(0, 0, 10, 10)));

        Assert.Null(WorklogEntryScope.BuildComponentsInScope(
            BuildBoardData("U1"), cache, EntryCovering("   ", new Rect(0, 0, 5, 5))));
    }

    // ------------------------------------------------------------------- work done totals

    [Fact]
    public void Work_done_totals_sum_hours_and_cost_across_every_row()
    {
        var entry = new WorklogEntryRecord
        {
            WorkDoneItems =
            {
                new WorklogWorkDoneRecord { HoursSpent = 1.5, Cost = 20.0 },
                new WorklogWorkDoneRecord { HoursSpent = 2.25, Cost = 5.5 },
            },
        };

        var (hours, cost) = WorklogEntryScope.GetWorkDoneTotals(entry);

        Assert.Equal(3.75, hours);
        Assert.Equal(25.5, cost);
    }

    // Zero rather than a blank or a null: both callers render these into a label unconditionally.
    [Fact]
    public void An_entry_with_no_work_done_rows_totals_zero()
    {
        Assert.Equal((0.0, 0.0), WorklogEntryScope.GetWorkDoneTotals(new WorklogEntryRecord()));
        Assert.Equal((0.0, 0.0), WorklogEntryScope.GetWorkDoneTotals(null));
    }

    // ------------------------------------------------------------------------ pluralisation

    // The plural rule has to hold at zero as well as at one and many - "1 entries" is the classic
    // way this goes wrong, and there were several open-coded copies of the ternary before this.
    [Theory]
    [InlineData(0, "0 entries")]
    [InlineData(1, "1 entry")]
    [InlineData(2, "2 entries")]
    [InlineData(11, "11 entries")]
    public void A_count_takes_the_singular_only_at_one(int count, string expected)
    {
        Assert.Equal(expected, WorklogEntryScope.FormatCount(count, "entry", "entries"));
    }
}
