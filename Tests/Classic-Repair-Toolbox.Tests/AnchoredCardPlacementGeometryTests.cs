using Avalonia;
using Handlers.Geometry;

namespace ClassicRepairToolbox.Tests;

// Tests for AnchoredCardPlacementGeometry - the corner-placement maths behind the worklog "New
// fault" card.
//
// The rule the card must obey is narrow on purpose: it is ALWAYS beside the drawn entry area,
// left or right, never stacked above or below it, giving exactly four placements - right+down,
// left+down, right+up, left+up. Two of the card's edges then coincide with two of the area's
// edges (with a small gap on the horizontal axis only). The four "mockup" tests below walk those
// four cases in the same order the maintainer's mockup numbers them.
//
// Each test hand-derives the expected corner/space arithmetic independently rather than trusting
// whatever the implementation happens to produce, per the project's testing rules.
public class AnchoredCardPlacementGeometryTests
{
    // A roomy container and a card small enough to fit on either side, so the four mockup tests
    // differ only in where the anchor rect sits.
    private static readonly Size Container = new(1000, 800);
    private static readonly Size Card = new(340, 400);

    // ------------------------------------------------------------- the four mockup placements

    [Fact]
    public void Mockup_1_most_room_right_and_down_puts_the_card_right_of_the_area_top_aligned()
    {
        // Anchor near the top-left: spaceRight=850 beats spaceLeft=50, and spaceDown (measured
        // from the anchor's TOP edge, 800-50=750) beats spaceUp (its BOTTOM edge, 130). So the
        // card's top-LEFT corner meets the area's top-RIGHT corner and it grows right and down.
        var placement = AnchoredCardPlacementGeometry.ComputePlacement(
            anchorRect: new Rect(50, 50, 100, 80),
            containerSize: Container,
            cardSize: Card,
            gap: 0);

        Assert.Equal(new Point(150, 50), placement.CardTopLeft);
        Assert.Equal(RectCorner.BottomLeft, placement.BadgeCorner);
    }

    [Fact]
    public void Mockup_2_most_room_left_and_down_puts_the_card_left_of_the_area_top_aligned()
    {
        // Anchor near the top-right: spaceLeft=850 beats spaceRight=50, spaceDown=750 beats
        // spaceUp=130. The card's top-RIGHT corner meets the area's top-LEFT corner, so its left
        // edge lands at 850-340=510 and its top edge still aligns with the area's top.
        var placement = AnchoredCardPlacementGeometry.ComputePlacement(
            anchorRect: new Rect(850, 50, 100, 80),
            containerSize: Container,
            cardSize: Card,
            gap: 0);

        Assert.Equal(new Point(510, 50), placement.CardTopLeft);
        Assert.Equal(RectCorner.BottomRight, placement.BadgeCorner);
    }

    [Fact]
    public void Mockup_3_most_room_right_and_up_puts_the_card_right_of_the_area_bottom_aligned()
    {
        // Anchor near the bottom-left: spaceRight=850 beats spaceLeft=50, spaceUp=750 beats
        // spaceDown=130. The card's bottom-LEFT corner meets the area's bottom-RIGHT corner, so
        // its top edge sits a full card height above the area's bottom: 750-400=350.
        var placement = AnchoredCardPlacementGeometry.ComputePlacement(
            anchorRect: new Rect(50, 670, 100, 80),
            containerSize: Container,
            cardSize: Card,
            gap: 0);

        Assert.Equal(new Point(150, 350), placement.CardTopLeft);
        Assert.Equal(RectCorner.TopLeft, placement.BadgeCorner);
    }

    [Fact]
    public void Mockup_4_most_room_left_and_up_puts_the_card_left_of_the_area_bottom_aligned()
    {
        // Anchor near the bottom-right: spaceLeft=850 beats spaceRight=50, spaceUp=750 beats
        // spaceDown=130. The card's bottom-RIGHT corner meets the area's bottom-LEFT corner.
        var placement = AnchoredCardPlacementGeometry.ComputePlacement(
            anchorRect: new Rect(850, 670, 100, 80),
            containerSize: Container,
            cardSize: Card,
            gap: 0);

        Assert.Equal(new Point(510, 350), placement.CardTopLeft);
        Assert.Equal(RectCorner.TopRight, placement.BadgeCorner);
    }

    // ------------------------------------------------------------------ never stacks, ever

    [Fact]
    public void A_tall_container_with_far_more_room_below_still_places_the_card_beside_the_area()
    {
        // The case that used to stack the card underneath the area: spaceDown (780) dwarfs
        // spaceRight (420). Vertical room must NOT decide the side - the card goes right, because
        // 420 beats spaceLeft's 20, and merely grows downwards from the area's top edge.
        var placement = AnchoredCardPlacementGeometry.ComputePlacement(
            anchorRect: new Rect(20, 20, 60, 60),
            containerSize: new Size(500, 800),
            cardSize: new Size(300, 150),
            gap: 0);

        Assert.Equal(new Point(80, 20), placement.CardTopLeft);
        Assert.Equal(RectCorner.BottomLeft, placement.BadgeCorner);
    }

    [Fact]
    public void The_chosen_corner_does_not_depend_on_whether_the_card_actually_fits()
    {
        // Placement is decided by free room alone, never by whether this particular card fits in
        // it - a card twice the container's size picks the same corner as a tiny one. That is
        // what keeps the card on a stable side as its content (the components-in-scope list)
        // changes height, instead of flipping to another corner mid-flow.
        var anchorRect = new Rect(50, 50, 100, 80);

        var tiny = AnchoredCardPlacementGeometry.ComputePlacement(anchorRect, Container, new Size(20, 20), gap: 0);
        var huge = AnchoredCardPlacementGeometry.ComputePlacement(anchorRect, Container, new Size(2000, 2000), gap: 0);

        // Both meet the area's top-right corner: same X (the area's right edge), same top edge.
        Assert.Equal(new Point(150, 50), tiny.CardTopLeft);
        Assert.Equal(new Point(150, 50), huge.CardTopLeft);
        Assert.Equal(huge.BadgeCorner, tiny.BadgeCorner);
    }

    [Fact]
    public void When_the_card_fits_nowhere_ties_are_broken_toward_right_and_down()
    {
        // A box centred exactly in a container too small for the card: spaceLeft/spaceRight tie
        // at 40 and spaceUp/spaceDown tie at 60. The implementation deliberately resolves ties in
        // favour of right/down so the result stays deterministic - this pins that quirk down
        // rather than leaving it to chance. Note the result is not clamped to the container;
        // callers are expected to clamp the final position themselves.
        var placement = AnchoredCardPlacementGeometry.ComputePlacement(
            anchorRect: new Rect(40, 40, 20, 20),
            containerSize: new Size(100, 100),
            cardSize: new Size(500, 500),
            gap: 0);

        Assert.Equal(new Point(60, 40), placement.CardTopLeft);
        Assert.Equal(RectCorner.BottomLeft, placement.BadgeCorner);
    }

    [Fact]
    public void A_card_wider_than_the_room_on_its_chosen_side_is_returned_unclamped()
    {
        // Anchor near the bottom-right of a small container, with a card too wide for the room on
        // its left (300 available, 340 needed): the maths still returns the left placement, at a
        // negative X, and leaves clamping to the caller. Pinned down because a caller that
        // forgets to clamp will see the card hang off the viewport rather than quietly get a
        // different corner.
        var placement = AnchoredCardPlacementGeometry.ComputePlacement(
            anchorRect: new Rect(300, 300, 100, 80),
            containerSize: new Size(500, 500),
            cardSize: new Size(340, 200),
            gap: 0);

        // spaceLeft=300 beats spaceRight=100, and spaceUp=380 beats spaceDown=200.
        Assert.Equal(new Point(-40, 180), placement.CardTopLeft);
        Assert.Equal(RectCorner.TopRight, placement.BadgeCorner);
    }

    // ------------------------------------------------------------------------------ gap

    [Fact]
    public void A_gap_pushes_a_right_hand_card_further_right_and_leaves_its_top_edge_aligned()
    {
        // Mockup 1 with an 8px gap: only X (the separating axis) moves, by exactly the gap.
        // Y must stay a true edge match with the area's top - that shared border is the look.
        var placement = AnchoredCardPlacementGeometry.ComputePlacement(
            anchorRect: new Rect(50, 50, 100, 80),
            containerSize: Container,
            cardSize: Card,
            gap: 8);

        Assert.Equal(new Point(158, 50), placement.CardTopLeft);
    }

    [Fact]
    public void A_gap_pushes_a_left_hand_card_further_left_and_leaves_its_bottom_edge_aligned()
    {
        // Mockup 4 with an 8px gap: X moves the other way (850-340-8=502) and Y is untouched,
        // still putting the card's bottom edge exactly on the area's bottom edge.
        var placement = AnchoredCardPlacementGeometry.ComputePlacement(
            anchorRect: new Rect(850, 670, 100, 80),
            containerSize: Container,
            cardSize: Card,
            gap: 8);

        Assert.Equal(new Point(502, 350), placement.CardTopLeft);
    }
}
