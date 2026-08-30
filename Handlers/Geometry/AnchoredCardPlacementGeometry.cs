using Avalonia;

namespace Handlers.Geometry
{
    // ###########################################################################################
    // Which corner of the anchor rectangle a placement touches (or, for a badge, which corner it
    // should sit at).
    // ###########################################################################################
    public enum RectCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    // ###########################################################################################
    // Where to put a popover-style card, and which corner of the anchor rectangle a companion
    // badge should use instead (the corner diagonally opposite the one the card touches, so the
    // two never overlap).
    // ###########################################################################################
    public readonly struct AnchoredCardPlacement
    {
        public Point CardTopLeft { get; init; }
        public RectCorner BadgeCorner { get; init; }
    }

    // ###########################################################################################
    // Positions a popover-style card against one corner of an anchor rectangle (the worklog entry
    // area drawn on a schematic).
    //
    // There are exactly FOUR possible placements, and the card is ALWAYS beside the anchor rect
    // horizontally - never stacked above or below it:
    //
    //     right + down   card's top-left     corner meets the anchor's top-right    corner
    //     left  + down   card's top-right    corner meets the anchor's top-left     corner
    //     right + up     card's bottom-left  corner meets the anchor's bottom-right corner
    //     left  + up     card's bottom-right corner meets the anchor's bottom-left  corner
    //
    // The side is whichever of left/right has more free room next to the anchor; the growth
    // direction is whichever of up/down has more free room, measured from the anchor edge the
    // card aligns with (down is measured from the anchor's TOP edge, since a downward card
    // top-aligns with it; up is measured from the anchor's BOTTOM edge, for the same reason).
    // Ties break deterministically towards right and down so results are reproducible.
    //
    // The placement is NOT a function of where the mouse was released - only of the anchor rect,
    // the container and the card size - so the same drawn area always yields the same placement.
    //
    // gap adds a small visual breathing space on the horizontal (separating) axis only - the card
    // is pushed that much further from the anchor rect on whichever side it sits - so the two do
    // not visually touch. The vertical axis stays a true edge-to-edge alignment, since that is the
    // "shared border" look the card is meant to have.
    //
    // The result is deliberately not clamped to the container: when the card cannot fit on either
    // side (a viewport narrower than the card) the caller is the one that decides how to clamp.
    // ###########################################################################################
    public static class AnchoredCardPlacementGeometry
    {
        public static AnchoredCardPlacement ComputePlacement(Rect anchorRect, Size containerSize, Size cardSize, double gap)
        {
            double spaceRight = System.Math.Max(0, containerSize.Width - anchorRect.Right);
            double spaceLeft = System.Math.Max(0, anchorRect.Left);

            // Room for a card that grows downwards is measured from the anchor's TOP edge (that
            // is the edge such a card aligns with), and room for one that grows upwards from the
            // anchor's BOTTOM edge - not from the far edges, which would measure the wrong span.
            double spaceDown = System.Math.Max(0, containerSize.Height - anchorRect.Top);
            double spaceUp = System.Math.Max(0, anchorRect.Bottom);

            bool onRight = spaceRight >= spaceLeft;
            bool alignTop = spaceDown >= spaceUp;

            double cardX = onRight
                ? anchorRect.Right + gap
                : anchorRect.Left - cardSize.Width - gap;

            double cardY = alignTop
                ? anchorRect.Top
                : anchorRect.Bottom - cardSize.Height;

            RectCorner touchedCorner = (onRight, alignTop) switch
            {
                (true, true) => RectCorner.TopRight,
                (false, true) => RectCorner.TopLeft,
                (true, false) => RectCorner.BottomRight,
                (false, false) => RectCorner.BottomLeft,
            };

            RectCorner badgeCorner = touchedCorner switch
            {
                RectCorner.TopRight => RectCorner.BottomLeft,
                RectCorner.TopLeft => RectCorner.BottomRight,
                RectCorner.BottomRight => RectCorner.TopLeft,
                _ => RectCorner.TopRight,
            };

            return new AnchoredCardPlacement
            {
                CardTopLeft = new Point(cardX, cardY),
                BadgeCorner = badgeCorner
            };
        }
    }
}
