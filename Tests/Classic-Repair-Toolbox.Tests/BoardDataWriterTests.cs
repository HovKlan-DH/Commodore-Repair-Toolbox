using Handlers.DataHandling;

namespace ClassicRepairToolbox.Tests;

// Characterisation tests for BoardDataWriter - what the label editor's Save button actually
// does to a contributor's board files.
//
// Two things must hold, because the alternative is destroying someone's work:
//   - highlight rectangles go to the sidecar .json, not the workbook;
//   - a component the editor invented is appended to the Components sheet without disturbing
//     the rows already there.
// BoardDataReader keeps its loaded boards in a static, non-thread-safe Dictionary. This class and
// BoardDataWriterTests both add to and remove from it, so they share one collection and run
// sequentially - the same treatment UserSettings and DataManager already get. Left in parallel,
// concurrent writes to that dictionary drop entries and a cached board comes back as null.
[Collection("BoardData")]
public sealed class BoardDataWriterTests : IDisposable
{
    private readonly TempWorkspace thisWorkspace = new();
    private readonly List<string> thisCacheKeys = new();

    public void Dispose()
    {
        foreach (string key in this.thisCacheKeys)
        {
            BoardDataReader.ClearCache(key);
        }

        this.thisWorkspace.Dispose();
    }

    private string NewCacheKey()
    {
        string key = "test-" + Guid.NewGuid().ToString("N");
        this.thisCacheKeys.Add(key);
        return key;
    }

    private Task<BoardData?> ReadBackAsync(string excelPath) =>
        BoardDataReader.LoadAsync(excelPath, this.NewCacheKey());

    private string CompleteBoard() =>
        BoardWorkbookBuilder.WriteCompleteBoard(this.thisWorkspace.Path_("board.xlsx"));

    private static LabelEditorSaveRow Row(
        string label, double x = 1, double y = 1, string schematic = "Sheet 1", string category = "IC") =>
        new()
        {
            SchematicName = schematic,
            BoardLabel = label,
            Category = category,
            X = x,
            Y = y,
            Width = 10,
            Height = 10
        };

    private static Task<(bool Success, string ErrorMessage)> SaveAsync(
        string excelPath, string schematic, params LabelEditorSaveRow[] rows) =>
        BoardDataWriter.SaveLabelEditorChangesAsync(excelPath, schematic, rows, region: "PAL");

    // ------------------------------------------------------------------- guard rails

    [Fact]
    public async Task Saving_to_a_missing_workbook_fails_with_a_readable_message()
    {
        var result = await SaveAsync(this.thisWorkspace.Path_("missing.xlsx"), "Sheet 1", Row("U1"));

        Assert.False(result.Success);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Saving_without_a_schematic_name_fails(string schematicName)
    {
        var result = await SaveAsync(this.CompleteBoard(), schematicName, Row("U1"));

        Assert.False(result.Success);
        Assert.Contains("No schematic", result.ErrorMessage);
    }

    [Fact]
    public async Task Saving_to_a_workbook_with_no_components_sheet_fails_without_corrupting_it()
    {
        string path = new BoardWorkbookBuilder()
            .Sheet("Board schematics", BoardWorkbookBuilder.SchematicsHeaders,
                new[] { "u", "Sheet 1", "", "s.png", "", "", "", "", "" })
            .SaveTo(this.thisWorkspace.Path_("nocomponents.xlsx"));

        // A brand new label needs a Components row, so the missing sheet is fatal here.
        var result = await SaveAsync(path, "Sheet 1", Row("BRANDNEW"));

        Assert.False(result.Success);
        Assert.NotEqual(string.Empty, result.ErrorMessage);
    }

    // -------------------------------------------------------------- highlight saving

    [Fact]
    public async Task Highlights_are_written_to_the_sidecar_json()
    {
        string excel = this.CompleteBoard();

        var result = await SaveAsync(excel, "Sheet 1", Row("U1", x: 11, y: 22));

        Assert.True(result.Success, result.ErrorMessage);

        ComponentHighlightEntry highlight = Assert.Single(
            BoardComponentHighlightStorage.LoadComponentHighlights(excel));

        Assert.Equal("Sheet 1", highlight.SchematicName);
        Assert.Equal("U1", highlight.BoardLabel);
        Assert.Equal("11", highlight.X);
        Assert.Equal("22", highlight.Y);
    }

    [Fact]
    public async Task Saving_one_schematic_leaves_another_schematics_highlights_alone()
    {
        string excel = this.CompleteBoard();

        Assert.True((await SaveAsync(excel, "Sheet 1", Row("U1"))).Success);
        Assert.True((await SaveAsync(excel, "Sheet 2", Row("C1", schematic: "Sheet 2"))).Success);
        Assert.True((await SaveAsync(excel, "Sheet 1", Row("U1", x: 99))).Success);

        var highlights = BoardComponentHighlightStorage.LoadComponentHighlights(excel);

        Assert.Equal(2, highlights.Count);
        Assert.Contains(highlights, h => h.SchematicName == "Sheet 2" && h.BoardLabel == "C1");
        Assert.Contains(highlights, h => h.SchematicName == "Sheet 1" && h.X == "99");
    }

    [Fact]
    public async Task Saving_an_empty_row_set_clears_that_schematic()
    {
        string excel = this.CompleteBoard();

        Assert.True((await SaveAsync(excel, "Sheet 1", Row("U1"))).Success);
        Assert.True((await SaveAsync(excel, "Sheet 1")).Success);

        Assert.Empty(BoardComponentHighlightStorage.LoadComponentHighlights(excel));
    }

    // --------------------------------------------------------- component row appending

    [Fact]
    public async Task A_label_that_already_exists_does_not_add_a_component_row()
    {
        string excel = this.CompleteBoard();
        int before = (await this.ReadBackAsync(excel))!.Components.Count;

        Assert.True((await SaveAsync(excel, "Sheet 1", Row("U1"))).Success);

        Assert.Equal(before, (await this.ReadBackAsync(excel))!.Components.Count);
    }

    [Fact]
    public async Task A_brand_new_label_is_appended_to_the_components_sheet()
    {
        string excel = this.CompleteBoard();

        Assert.True((await SaveAsync(excel, "Sheet 1", Row("R99", category: "Resistor"))).Success);

        BoardData? data = await this.ReadBackAsync(excel);

        Assert.Contains(data!.Components, c => c.BoardLabel == "R99");
    }

    [Fact]
    public async Task Appending_a_component_preserves_the_rows_already_in_the_sheet()
    {
        // The regression that would eat a contributor's component data.
        string excel = this.CompleteBoard();

        Assert.True((await SaveAsync(excel, "Sheet 1", Row("R99", category: "Resistor"))).Success);

        BoardData? data = await this.ReadBackAsync(excel);

        Assert.Contains(data!.Components, c => c.BoardLabel == "U1" && c.FriendlyName == "PLA");
        Assert.Contains(data.Components, c => c.BoardLabel == "C1" && c.FriendlyName == "Filter cap");
        Assert.Contains(data.Components, c => c.BoardLabel == "R99");
    }

    [Fact]
    public async Task Appending_a_component_preserves_the_other_sheets()
    {
        string excel = this.CompleteBoard();

        Assert.True((await SaveAsync(excel, "Sheet 1", Row("R99"))).Success);

        BoardData? data = await this.ReadBackAsync(excel);

        Assert.Single(data!.Schematics);
        Assert.Single(data.Credits);
        Assert.Single(data.BoardLinks);
        Assert.Single(data.ComponentImages);
        Assert.Single(data.KiCadImportantSignals);
    }

    [Fact]
    public async Task The_appended_component_carries_its_category()
    {
        string excel = this.CompleteBoard();

        Assert.True((await SaveAsync(excel, "Sheet 1", Row("R99", category: "Resistor"))).Success);

        ComponentEntry appended = (await this.ReadBackAsync(excel))!
            .Components.Single(c => c.BoardLabel == "R99");

        Assert.Equal("Resistor", appended.Category);
    }

    [Fact]
    public async Task The_appended_component_region_comes_from_the_row_not_the_region_argument()
    {
        // CURRENT BEHAVIOUR, worth knowing: the `region` argument to
        // SaveLabelEditorChangesAsync is threaded down to WriteComponentRow but never used -
        // the written Region is item.Region. A row with no Region therefore lands with an
        // empty Region even when "PAL" was passed in.
        string excel = this.CompleteBoard();

        Assert.True((await SaveAsync(excel, "Sheet 1", Row("R99"))).Success);        // no Region on the row

        ComponentEntry appended = (await this.ReadBackAsync(excel))!
            .Components.Single(c => c.BoardLabel == "R99");

        Assert.Equal(string.Empty, appended.Region);
    }

    [Fact]
    public async Task A_row_that_sets_its_own_region_has_it_written()
    {
        string excel = this.CompleteBoard();

        var row = new LabelEditorSaveRow
        {
            SchematicName = "Sheet 1", BoardLabel = "R98", Category = "Resistor", Region = "NTSC",
            X = 1, Y = 1, Width = 10, Height = 10
        };

        Assert.True((await BoardDataWriter.SaveLabelEditorChangesAsync(
            excel, "Sheet 1", new[] { row }, region: "PAL")).Success);

        ComponentEntry appended = (await this.ReadBackAsync(excel))!
            .Components.Single(c => c.BoardLabel == "R98");

        Assert.Equal("NTSC", appended.Region);
    }

    [Fact]
    public async Task The_same_new_label_used_twice_is_only_appended_once()
    {
        string excel = this.CompleteBoard();

        Assert.True((await SaveAsync(excel, "Sheet 1",
            Row("R99", x: 1, y: 1), Row("R99", x: 50, y: 50))).Success);

        BoardData? data = await this.ReadBackAsync(excel);

        Assert.Single(data!.Components, c => c.BoardLabel == "R99");
        // ...but both rectangles are still saved.
        Assert.Equal(2, BoardComponentHighlightStorage.LoadComponentHighlights(excel)
            .Count(h => h.BoardLabel == "R99"));
    }

    [Fact]
    public async Task Rows_with_a_blank_board_label_are_ignored()
    {
        string excel = this.CompleteBoard();
        int before = (await this.ReadBackAsync(excel))!.Components.Count;

        Assert.True((await SaveAsync(excel, "Sheet 1", Row("   "), Row("U1"))).Success);

        Assert.Equal(before, (await this.ReadBackAsync(excel))!.Components.Count);
        Assert.Single(BoardComponentHighlightStorage.LoadComponentHighlights(excel));
    }

    [Fact]
    public async Task The_workbook_is_still_readable_after_a_save()
    {
        // EPPlus round-trip guard: a corrupted workbook would make the board unopenable.
        string excel = this.CompleteBoard();

        Assert.True((await SaveAsync(excel, "Sheet 1", Row("R99"))).Success);

        Assert.NotNull(await this.ReadBackAsync(excel));
    }

    [Fact]
    public async Task Saving_twice_in_a_row_succeeds()
    {
        string excel = this.CompleteBoard();

        Assert.True((await SaveAsync(excel, "Sheet 1", Row("R99"))).Success);
        Assert.True((await SaveAsync(excel, "Sheet 1", Row("R99"), Row("R100"))).Success);

        BoardData? data = await this.ReadBackAsync(excel);

        Assert.Contains(data!.Components, c => c.BoardLabel == "R99");
        Assert.Contains(data.Components, c => c.BoardLabel == "R100");
    }
}
