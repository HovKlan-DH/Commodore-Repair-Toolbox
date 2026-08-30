using Handlers.DataHandling;

namespace ClassicRepairToolbox.Tests;

// Characterisation tests for ComponentListBuilder - the region filter, category filter and
// search that decide which components the main window lists.
//
// This was private static logic inside Main. The rules it encodes are invisible from the UI and
// easy to break: a blank region means "shared", search terms are ANDed across the whole composed
// display string, and a component with no usable text is dropped entirely.
public class ComponentListBuilderTests
{
    private static BoardData Board(params ComponentEntry[] components)
        => new() { Components = components.ToList() };

    private static ComponentEntry Component(
        string boardLabel = "", string friendly = "", string technical = "",
        string category = "", string region = "")
        => new()
        {
            BoardLabel = boardLabel,
            FriendlyName = friendly,
            TechnicalNameOrValue = technical,
            Category = category,
            Region = region
        };

    // -------------------------------------------------------------- BuildDistinctCategories

    [Fact]
    public void Categories_are_returned_once_each_in_first_seen_order()
    {
        var board = Board(
            Component(boardLabel: "U1", category: "IC"),
            Component(boardLabel: "R1", category: "Resistor"),
            Component(boardLabel: "U2", category: "IC"));

        Assert.Equal(new[] { "IC", "Resistor" }, ComponentListBuilder.BuildDistinctCategories(board));
    }

    // De-duplication is case-insensitive, but the FIRST spelling seen is the one kept.
    [Fact]
    public void Categories_differing_only_in_case_collapse_to_the_first_spelling()
    {
        var board = Board(
            Component(boardLabel: "U1", category: "IC"),
            Component(boardLabel: "U2", category: "ic"));

        Assert.Equal(new[] { "IC" }, ComponentListBuilder.BuildDistinctCategories(board));
    }

    [Fact]
    public void Blank_categories_are_skipped()
    {
        var board = Board(
            Component(boardLabel: "U1", category: ""),
            Component(boardLabel: "U2", category: "   "),
            Component(boardLabel: "U3", category: "IC"));

        Assert.Equal(new[] { "IC" }, ComponentListBuilder.BuildDistinctCategories(board));
    }

    [Fact]
    public void A_board_with_no_components_yields_no_categories()
    {
        Assert.Empty(ComponentListBuilder.BuildDistinctCategories(Board()));
    }

    // -------------------------------------------------------------- region filtering

    // The central rule: a component with NO region is shared and appears in every region, while
    // one that names a region appears only there.
    [Fact]
    public void A_component_without_a_region_is_shared_across_every_region()
    {
        var board = Board(Component(boardLabel: "U1"));

        Assert.Single(ComponentListBuilder.BuildComponentItems(board, "PAL"));
        Assert.Single(ComponentListBuilder.BuildComponentItems(board, "NTSC"));
    }

    [Fact]
    public void A_component_naming_a_region_appears_only_in_that_region()
    {
        var board = Board(Component(boardLabel: "U1", region: "PAL"));

        Assert.Single(ComponentListBuilder.BuildComponentItems(board, "PAL"));
        Assert.Empty(ComponentListBuilder.BuildComponentItems(board, "NTSC"));
    }

    [Fact]
    public void Region_matching_ignores_case_and_surrounding_whitespace()
    {
        var board = Board(Component(boardLabel: "U1", region: "  pal  "));

        Assert.Single(ComponentListBuilder.BuildComponentItems(board, "PAL"));
    }

    // A whitespace-only region trims to empty, which means shared - not "a region called space".
    [Fact]
    public void A_whitespace_only_region_counts_as_shared()
    {
        var board = Board(Component(boardLabel: "U1", region: "   "));

        Assert.Single(ComponentListBuilder.BuildComponentItems(board, "NTSC"));
    }

    // -------------------------------------------------------------- display text

    [Fact]
    public void Display_text_joins_board_label_friendly_name_and_technical_name()
    {
        var board = Board(Component(boardLabel: "U1", friendly: "PLA", technical: "906114-01"));

        var items = ComponentListBuilder.BuildComponentItems(board, "PAL");

        Assert.Equal("U1 | PLA | 906114-01", items.Single().DisplayText);
    }

    // Blank parts are skipped rather than leaving an empty slot, so the separator never appears
    // with nothing around it.
    [Fact]
    public void Blank_parts_are_omitted_from_the_display_text()
    {
        var board = Board(Component(boardLabel: "U1", technical: "906114-01"));

        Assert.Equal("U1 | 906114-01", ComponentListBuilder.BuildComponentItems(board, "PAL").Single().DisplayText);
    }

    // A component with nothing to show at all is dropped entirely - it would otherwise render as
    // a blank, unclickable row.
    [Fact]
    public void A_component_with_no_usable_text_is_dropped()
    {
        var board = Board(Component(category: "IC", region: "PAL"));

        Assert.Empty(ComponentListBuilder.BuildComponentItems(board, "PAL"));
    }

    [Fact]
    public void Display_text_parts_are_trimmed()
    {
        var board = Board(Component(boardLabel: "  U1  ", friendly: "  PLA  "));

        Assert.Equal("U1 | PLA", ComponentListBuilder.BuildComponentItems(board, "PAL").Single().DisplayText);
    }

    // -------------------------------------------------------------- search

    // Terms are ANDed, and each is matched against the WHOLE composed display string - so a term
    // hitting the label and another hitting the technical name both count.
    [Fact]
    public void Every_search_term_must_match_somewhere_in_the_display_text()
    {
        var board = Board(Component(boardLabel: "U1", friendly: "PLA", technical: "906114-01"));

        Assert.Single(ComponentListBuilder.BuildComponentItems(board, "PAL", null, "U1 906114"));
        Assert.Empty(ComponentListBuilder.BuildComponentItems(board, "PAL", null, "U1 missing"));
    }

    [Fact]
    public void Search_is_case_insensitive()
    {
        var board = Board(Component(boardLabel: "U1", friendly: "PLA"));

        Assert.Single(ComponentListBuilder.BuildComponentItems(board, "PAL", null, "pla"));
    }

    // A blank or whitespace-only search is "no filter", not "match nothing".
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_search_term_does_not_filter(string search)
    {
        var board = Board(Component(boardLabel: "U1"));

        Assert.Single(ComponentListBuilder.BuildComponentItems(board, "PAL", null, search));
    }

    // Runs of spaces between terms collapse rather than producing an empty term that matches
    // everything (or nothing).
    [Fact]
    public void Repeated_spaces_between_search_terms_are_ignored()
    {
        var board = Board(Component(boardLabel: "U1", friendly: "PLA"));

        Assert.Single(ComponentListBuilder.BuildComponentItems(board, "PAL", null, "U1    PLA"));
    }

    // The search runs against the composed string, which includes the " | " separator - so a term
    // containing the separator can match across two fields. Documented, not endorsed.
    [Fact]
    public void A_search_term_can_span_the_field_separator()
    {
        var board = Board(Component(boardLabel: "U1", friendly: "PLA"));

        Assert.Single(ComponentListBuilder.BuildComponentItems(board, "PAL", null, "U1 | PLA"));
    }

    // -------------------------------------------------------------- category filter

    [Fact]
    public void Only_components_in_the_category_filter_are_listed()
    {
        var board = Board(
            Component(boardLabel: "U1", category: "IC"),
            Component(boardLabel: "R1", category: "Resistor"));

        var items = ComponentListBuilder.BuildComponentItems(
            board, "PAL", new HashSet<string> { "IC" });

        Assert.Equal("U1", items.Single().BoardLabel);
    }

    // The filter set is built with the default comparer, so category matching here is
    // case-SENSITIVE even though BuildDistinctCategories de-duplicates case-insensitively.
    [Fact]
    public void The_category_filter_matches_case_sensitively()
    {
        var board = Board(Component(boardLabel: "U1", category: "IC"));

        Assert.Empty(ComponentListBuilder.BuildComponentItems(board, "PAL", new HashSet<string> { "ic" }));
    }

    // A null filter means "no category filtering", distinct from an empty set which excludes all.
    [Fact]
    public void A_null_category_filter_lets_everything_through_but_an_empty_set_excludes_all()
    {
        var board = Board(Component(boardLabel: "U1", category: "IC"));

        Assert.Single(ComponentListBuilder.BuildComponentItems(board, "PAL", null));
        Assert.Empty(ComponentListBuilder.BuildComponentItems(board, "PAL", new HashSet<string>()));
    }

    // -------------------------------------------------------------- selection key

    // The key joins four fields with a unit separator (U+001F) so it stays unique even when a
    // field legitimately contains a pipe or a space.
    [Fact]
    public void The_selection_key_joins_four_trimmed_fields_with_a_unit_separator()
    {
        var board = Board(Component(
            boardLabel: " U1 ", friendly: " PLA ", technical: " 906114-01 ", region: " PAL "));

        var item = ComponentListBuilder.BuildComponentItems(board, "PAL").Single();

        const char separator = '\u001F';
        Assert.Equal(
            string.Join(separator, "U1", "PLA", "906114-01", "PAL"),
            item.SelectionKey);
    }

    // The key includes the region, so the PAL and NTSC variants of one label stay distinguishable.
    [Fact]
    public void Two_region_variants_of_one_label_get_different_selection_keys()
    {
        var pal = ComponentListBuilder
            .BuildComponentItems(Board(Component(boardLabel: "U1", region: "PAL")), "PAL").Single();
        var ntsc = ComponentListBuilder
            .BuildComponentItems(Board(Component(boardLabel: "U1", region: "NTSC")), "NTSC").Single();

        Assert.NotEqual(pal.SelectionKey, ntsc.SelectionKey);
    }

    [Fact]
    public void ToString_returns_the_display_text_so_the_list_renders_it()
    {
        var item = ComponentListBuilder
            .BuildComponentItems(Board(Component(boardLabel: "U1", friendly: "PLA")), "PAL").Single();

        Assert.Equal(item.DisplayText, item.ToString());
    }

    // -------------------------------------------------------------- HasExplicitRegionComponents

    // This is what decides whether the PAL/NTSC switch is offered at all.
    [Theory]
    [InlineData("PAL", true)]
    [InlineData("NTSC", true)]
    [InlineData("pal", true)]
    [InlineData("  NTSC  ", true)]
    [InlineData("", false)]
    [InlineData("SECAM", false)]
    public void A_board_offers_the_region_switch_only_for_PAL_or_NTSC_components(string region, bool expected)
    {
        var board = Board(Component(boardLabel: "U1", region: region));

        Assert.Equal(expected, ComponentListBuilder.HasExplicitRegionComponents(board));
    }

    [Fact]
    public void A_null_board_has_no_explicit_region_components()
    {
        Assert.False(ComponentListBuilder.HasExplicitRegionComponents(null));
    }

    [Fact]
    public void A_board_with_no_components_has_no_explicit_region_components()
    {
        Assert.False(ComponentListBuilder.HasExplicitRegionComponents(Board()));
    }

    // -------------------------------------------------------------- IsSupportedKiCadRawFile

    [Theory]
    [InlineData("board.kicad_pcb", true)]
    [InlineData("board.kicad_pro", true)]
    [InlineData("board.kicad_sch", true)]
    [InlineData("BOARD.KICAD_PCB", true)]
    [InlineData("  board.kicad_pcb  ", true)]
    [InlineData("board.kicad_prl", false)]     // a real KiCad file, but not one we load
    [InlineData("board.txt", false)]
    [InlineData("board", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    public void Only_the_three_loadable_KiCad_extensions_are_supported(string path, bool expected)
    {
        Assert.Equal(expected, ComponentListBuilder.IsSupportedKiCadRawFile(path));
    }

    // -------------------------------------------------------------- BuildSyncBannerText

    [Fact]
    public void The_sync_banner_gains_a_protected_file_count_only_when_there_are_some()
    {
        Assert.Equal("Sync done", ComponentListBuilder.BuildSyncBannerText("Sync done", 0));
        Assert.Equal(
            "Sync done; protected contribution related files are [3]",
            ComponentListBuilder.BuildSyncBannerText("Sync done", 3));
    }

    // A negative count is treated the same as none rather than rendering "[-1]".
    [Fact]
    public void A_negative_protected_file_count_is_treated_as_none()
    {
        Assert.Equal("Sync done", ComponentListBuilder.BuildSyncBannerText("Sync done", -1));
    }

    // -------------------------------------------------------------- BuildComponentsInScope

    // This backs the worklog entry card's "Mark components in scope" checklist: given the set of
    // board labels a drawn area touches, return the matching components in board-data order (the
    // same order Overview and the main Component list already use).
    [Fact]
    public void Only_components_whose_label_is_in_scope_are_returned()
    {
        var board = Board(
            Component(boardLabel: "C1", friendly: "Ceramic"),
            Component(boardLabel: "R1", friendly: "Resistor"));

        var rows = ComponentListBuilder.BuildComponentsInScope(board, new HashSet<string> { "C1" });

        Assert.Equal("C1", Assert.Single(rows).BoardLabel);
    }

    // Order must follow boardData.Components, not the (unordered) scope set, or the list would
    // shuffle components relative to how Overview and the main Component list show them.
    [Fact]
    public void Matched_components_keep_board_data_order_regardless_of_scope_set_order()
    {
        var board = Board(
            Component(boardLabel: "C3"),
            Component(boardLabel: "C1"),
            Component(boardLabel: "C2"));

        var rows = ComponentListBuilder.BuildComponentsInScope(
            board, new HashSet<string> { "C1", "C2", "C3" });

        Assert.Equal(new[] { "C3", "C1", "C2" }, rows.Select(row => row.BoardLabel));
    }

    [Fact]
    public void Board_label_matching_is_case_insensitive()
    {
        var board = Board(Component(boardLabel: "C1"));

        var rows = ComponentListBuilder.BuildComponentsInScope(board, new HashSet<string> { "c1" });

        Assert.Single(rows);
    }

    [Fact]
    public void An_empty_scope_yields_no_rows()
    {
        var board = Board(Component(boardLabel: "C1"));

        Assert.Empty(ComponentListBuilder.BuildComponentsInScope(board, new HashSet<string>()));
    }

    // The display name deliberately excludes the board label - the UI shows that separately, bold.
    [Fact]
    public void Display_name_joins_friendly_and_technical_name_but_not_the_board_label()
    {
        var board = Board(Component(boardLabel: "C1", friendly: "Ceramic", technical: "100pF 25V"));

        var row = ComponentListBuilder.BuildComponentsInScope(board, new HashSet<string> { "C1" }).Single();

        Assert.Equal("Ceramic | 100pF 25V", row.DisplayName);
    }

    [Fact]
    public void Blank_display_name_parts_are_omitted()
    {
        var board = Board(Component(boardLabel: "C1", technical: "100pF 25V"));

        var row = ComponentListBuilder.BuildComponentsInScope(board, new HashSet<string> { "C1" }).Single();

        Assert.Equal("100pF 25V", row.DisplayName);
    }

    [Fact]
    public void Board_label_and_display_name_parts_are_trimmed()
    {
        var board = Board(Component(boardLabel: "  C1  ", friendly: "  Ceramic  "));

        var row = ComponentListBuilder.BuildComponentsInScope(board, new HashSet<string> { "C1" }).Single();

        Assert.Equal("C1", row.BoardLabel);
        Assert.Equal("Ceramic", row.DisplayName);
    }
}
