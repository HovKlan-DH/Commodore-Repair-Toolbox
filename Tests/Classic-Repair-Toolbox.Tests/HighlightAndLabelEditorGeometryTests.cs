using Avalonia;
using Avalonia.Input;
using Handlers.DataHandling;
using Handlers.Geometry;

namespace ClassicRepairToolbox.Tests;

// Tests for the last two Tier B extractions.
//
// HighlightRectBuilder decides which component rectangles appear on a schematic for the active
// region (PAL / NTSC) - get it wrong and a user sees components that do not exist on their board.
// LabelEditorGeometry decides where the resize handles are and what the keyboard chords do.
public class HighlightRectBuilderTests
{
    private static BoardData Board(
        (string Label, string Region)[] components,
        (string Schematic, string Label, string X, string Y, string W, string H)[] highlights)
    {
        var data = new BoardData();

        foreach (var (label, region) in components)
        {
            data.Components.Add(new ComponentEntry { BoardLabel = label, Region = region });
        }

        foreach (var (schematic, label, x, y, w, h) in highlights)
        {
            data.ComponentHighlights.Add(new ComponentHighlightEntry
            {
                SchematicName = schematic, BoardLabel = label, X = x, Y = y, Width = w, Height = h
            });
        }

        return data;
    }

    [Fact]
    public void A_highlight_is_grouped_by_schematic_then_board_label()
    {
        BoardData board = Board(
            new[] { ("U1", "") },
            new[] { ("Sheet 1", "U1", "10", "20", "30", "40") });

        var rects = HighlightRectBuilder.BuildHighlightRects(board, "PAL");

        Rect rect = Assert.Single(rects["Sheet 1"]["U1"]);
        Assert.Equal(new Rect(10, 20, 30, 40), rect);
    }

    [Fact]
    public void A_component_with_no_declared_region_is_visible_in_every_region()
    {
        BoardData board = Board(
            new[] { ("U1", "") },
            new[] { ("Sheet 1", "U1", "1", "1", "5", "5") });

        Assert.Single(HighlightRectBuilder.BuildHighlightRects(board, "PAL")["Sheet 1"]);
        Assert.Single(HighlightRectBuilder.BuildHighlightRects(board, "NTSC")["Sheet 1"]);
    }

    [Fact]
    public void A_component_declared_for_one_region_is_hidden_in_the_other()
    {
        BoardData board = Board(
            new[] { ("U1", "PAL") },
            new[] { ("Sheet 1", "U1", "1", "1", "5", "5") });

        Assert.Single(HighlightRectBuilder.BuildHighlightRects(board, "PAL")["Sheet 1"]);
        Assert.Empty(HighlightRectBuilder.BuildHighlightRects(board, "NTSC"));
    }

    [Fact]
    public void A_component_listed_for_both_regions_is_visible_in_both()
    {
        BoardData board = Board(
            new[] { ("U1", "PAL"), ("U1", "NTSC") },
            new[] { ("Sheet 1", "U1", "1", "1", "5", "5") });

        Assert.Single(HighlightRectBuilder.BuildHighlightRects(board, "PAL")["Sheet 1"]);
        Assert.Single(HighlightRectBuilder.BuildHighlightRects(board, "NTSC")["Sheet 1"]);
    }

    [Fact]
    public void Region_matching_ignores_case()
    {
        BoardData board = Board(
            new[] { ("U1", "pal") },
            new[] { ("Sheet 1", "U1", "1", "1", "5", "5") });

        Assert.Single(HighlightRectBuilder.BuildHighlightRects(board, "PAL")["Sheet 1"]);
    }

    [Fact]
    public void A_highlight_for_a_label_that_has_no_component_row_is_still_shown()
    {
        // The label editor can create a highlight before its Components row exists.
        BoardData board = Board(
            Array.Empty<(string, string)>(),
            new[] { ("Sheet 1", "ORPHAN", "1", "1", "5", "5") });

        Assert.Single(HighlightRectBuilder.BuildHighlightRects(board, "PAL")["Sheet 1"]);
    }

    [Fact]
    public void Several_rectangles_for_one_label_are_all_kept()
    {
        BoardData board = Board(
            new[] { ("U1", "") },
            new[]
            {
                ("Sheet 1", "U1", "1", "1", "5", "5"),
                ("Sheet 1", "U1", "50", "50", "5", "5")
            });

        Assert.Equal(2, HighlightRectBuilder.BuildHighlightRects(board, "PAL")["Sheet 1"]["U1"].Count);
    }

    [Theory]
    [InlineData("", "1", "5", "5")]        // blank X
    [InlineData("1", "1", "0", "5")]       // zero width
    [InlineData("1", "1", "5", "-1")]      // negative height
    [InlineData("1", "1", "abc", "5")]     // unparseable
    [InlineData("1,5", "1", "5", "5")]     // comma decimal separator
    public void A_malformed_or_degenerate_highlight_is_dropped(string x, string y, string w, string h)
    {
        BoardData board = Board(
            new[] { ("U1", "") },
            new[] { ("Sheet 1", "U1", x, y, w, h) });

        Assert.Empty(HighlightRectBuilder.BuildHighlightRects(board, "PAL"));
    }

    [Fact]
    public void A_highlight_with_no_schematic_or_label_is_dropped()
    {
        BoardData board = Board(
            new[] { ("U1", "") },
            new[]
            {
                ("", "U1", "1", "1", "5", "5"),
                ("Sheet 1", "  ", "1", "1", "5", "5")
            });

        Assert.Empty(HighlightRectBuilder.BuildHighlightRects(board, "PAL"));
    }

    [Fact]
    public void A_board_with_no_highlights_yields_an_empty_lookup()
    {
        Assert.Empty(HighlightRectBuilder.BuildHighlightRects(new BoardData(), "PAL"));
    }
}

public class LabelEditorGeometryTests
{
    // ------------------------------------------------- IsLabelEditorRectangleTooSmall

    [Theory]
    [InlineData(20, 20)]
    [InlineData(15, 15)]     // exactly at the minimum
    [InlineData(100, 15)]
    public void A_usable_rectangle_is_accepted(double width, double height)
    {
        Assert.False(LabelEditorGeometry.IsLabelEditorRectangleTooSmall(new Rect(0, 0, width, height)));
    }

    [Theory]
    [InlineData(14, 100)]    // too narrow, however tall
    [InlineData(100, 14)]    // too short, however wide
    [InlineData(0, 0)]
    [InlineData(5, 5)]
    public void A_rectangle_below_the_minimum_on_either_axis_is_rejected(double width, double height)
    {
        // An accidental click would otherwise create a highlight nobody can grab again.
        Assert.True(LabelEditorGeometry.IsLabelEditorRectangleTooSmall(new Rect(0, 0, width, height)));
    }

    // ------------------------------------- TryGetKeyboardLabelEditorResizeDragMode

    // LabelEditorDragMode is internal, so these cannot be [Theory] parameters on a public method.
    private static LabelEditorDragMode ResizeModeFor(Key key, KeyModifiers modifiers)
    {
        Assert.True(LabelEditorGeometry.TryGetKeyboardLabelEditorResizeDragMode(
            key, modifiers, out LabelEditorDragMode mode));
        return mode;
    }

    [Fact]
    public void Shift_plus_an_arrow_grows_the_edge_the_arrow_points_at()
    {
        Assert.Equal(LabelEditorDragMode.ResizeLeft, ResizeModeFor(Key.Left, KeyModifiers.Shift));
        Assert.Equal(LabelEditorDragMode.ResizeRight, ResizeModeFor(Key.Right, KeyModifiers.Shift));
        Assert.Equal(LabelEditorDragMode.ResizeTop, ResizeModeFor(Key.Up, KeyModifiers.Shift));
        Assert.Equal(LabelEditorDragMode.ResizeBottom, ResizeModeFor(Key.Down, KeyModifiers.Shift));
    }

    [Fact]
    public void Alt_plus_an_arrow_moves_the_opposite_edge()
    {
        Assert.Equal(LabelEditorDragMode.ResizeRight, ResizeModeFor(Key.Left, KeyModifiers.Alt));
        Assert.Equal(LabelEditorDragMode.ResizeLeft, ResizeModeFor(Key.Right, KeyModifiers.Alt));
        Assert.Equal(LabelEditorDragMode.ResizeBottom, ResizeModeFor(Key.Up, KeyModifiers.Alt));
        Assert.Equal(LabelEditorDragMode.ResizeTop, ResizeModeFor(Key.Down, KeyModifiers.Alt));
    }

    [Theory]
    [InlineData(KeyModifiers.None)]
    [InlineData(KeyModifiers.Control)]
    public void An_arrow_without_shift_or_alt_is_not_a_resize(KeyModifiers modifiers)
    {
        // Plain arrows nudge the whole highlight instead.
        Assert.False(LabelEditorGeometry.TryGetKeyboardLabelEditorResizeDragMode(
            Key.Left, modifiers, out _));
    }

    [Fact]
    public void Shift_and_alt_together_are_ambiguous_and_rejected()
    {
        Assert.False(LabelEditorGeometry.TryGetKeyboardLabelEditorResizeDragMode(
            Key.Left, KeyModifiers.Shift | KeyModifiers.Alt, out _));
    }

    [Fact]
    public void A_non_arrow_key_is_not_a_resize()
    {
        Assert.False(LabelEditorGeometry.TryGetKeyboardLabelEditorResizeDragMode(
            Key.A, KeyModifiers.Shift, out _));
    }

    // ------------------------------------------- BuildLabelEditorHandleHitRects

    [Fact]
    public void A_large_selection_gets_all_eight_handles()
    {
        var handles = LabelEditorGeometry.BuildLabelEditorHandleHitRects(new Rect(0, 0, 200, 200), scale: 1.0);

        Assert.Equal(8, handles.Count);
        Assert.Equal(8, handles.Select(h => h.DragMode).Distinct().Count());
    }

    [Fact]
    public void The_four_corner_handles_always_exist()
    {
        // Corner drags resize two axes at once, so they take priority when space is tight.
        var handles = LabelEditorGeometry.BuildLabelEditorHandleHitRects(new Rect(0, 0, 4, 4), scale: 1.0);

        var modes = handles.Select(h => h.DragMode).ToList();

        Assert.Contains(LabelEditorDragMode.ResizeTopLeft, modes);
        Assert.Contains(LabelEditorDragMode.ResizeTopRight, modes);
        Assert.Contains(LabelEditorDragMode.ResizeBottomRight, modes);
        Assert.Contains(LabelEditorDragMode.ResizeBottomLeft, modes);
    }

    [Fact]
    public void A_selection_too_narrow_for_side_handles_drops_them_rather_than_overlapping()
    {
        var handles = LabelEditorGeometry.BuildLabelEditorHandleHitRects(new Rect(0, 0, 4, 200), scale: 1.0);

        var modes = handles.Select(h => h.DragMode).ToList();

        Assert.DoesNotContain(LabelEditorDragMode.ResizeTop, modes);
        Assert.DoesNotContain(LabelEditorDragMode.ResizeBottom, modes);
        Assert.Contains(LabelEditorDragMode.ResizeLeft, modes);    // vertical sides still fit
    }

    [Fact]
    public void Handles_grow_in_world_terms_as_the_view_zooms_out()
    {
        // Handles are sized in screen pixels, so a zoomed-out view needs bigger world rects.
        var zoomedIn = LabelEditorGeometry.BuildLabelEditorHandleHitRects(new Rect(0, 0, 200, 200), scale: 4.0);
        var zoomedOut = LabelEditorGeometry.BuildLabelEditorHandleHitRects(new Rect(0, 0, 200, 200), scale: 0.5);

        double inSize = zoomedIn.First(h => h.DragMode == LabelEditorDragMode.ResizeTopLeft).HitRect.Width;
        double outSize = zoomedOut.First(h => h.DragMode == LabelEditorDragMode.ResizeTopLeft).HitRect.Width;

        Assert.True(outSize > inSize);
    }

    [Fact]
    public void Handle_size_is_clamped_so_it_never_becomes_absurd()
    {
        var extreme = LabelEditorGeometry.BuildLabelEditorHandleHitRects(new Rect(0, 0, 200, 200), scale: 0.001);

        double size = extreme.First(h => h.DragMode == LabelEditorDragMode.ResizeTopLeft).HitRect.Width;

        Assert.InRange(size, 4.0, 12.0);
    }

    [Fact]
    public void Corner_handles_are_centred_on_the_corners()
    {
        var handles = LabelEditorGeometry.BuildLabelEditorHandleHitRects(new Rect(100, 100, 200, 200), scale: 1.0);

        Rect topLeft = handles.First(h => h.DragMode == LabelEditorDragMode.ResizeTopLeft).HitRect;

        Assert.Equal(100, topLeft.Center.X, precision: 6);
        Assert.Equal(100, topLeft.Center.Y, precision: 6);
    }
}
