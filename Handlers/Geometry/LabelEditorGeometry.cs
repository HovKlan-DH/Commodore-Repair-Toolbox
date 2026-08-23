using Avalonia;
using Avalonia.Input;
using System;
using System.Collections.Generic;

namespace Handlers.Geometry
{
    internal enum LabelEditorDragMode
    {
        None,
        Move,
        ResizeTopLeft,
        ResizeTop,
        ResizeTopRight,
        ResizeRight,
        ResizeBottomRight,
        ResizeBottom,
        ResizeBottomLeft,
        ResizeLeft
    }
    // ###########################################################################################
    // Geometry for the component label editor: which resize handle is under the pointer, which
    // keyboard chord maps to which resize direction, and whether a drawn rectangle is usable.
    //
    // Extracted from TabSchematics so the handle layout and keyboard mapping can be tested.
    // ###########################################################################################
    internal static class LabelEditorGeometry
    {

    // ###########################################################################################
    // Maps one keyboard resize gesture to the equivalent editor drag mode so exact-match guides
    // can be shown without applying any mouse-style snap movement.
    // ###########################################################################################
    public static bool TryGetKeyboardLabelEditorResizeDragMode(Key key, KeyModifiers modifiers, out LabelEditorDragMode dragMode)
    {
        dragMode = LabelEditorDragMode.None;

        bool isShift = modifiers.HasFlag(KeyModifiers.Shift);
        bool isAlt = modifiers.HasFlag(KeyModifiers.Alt);

        if (isShift == isAlt)
        {
            return false;
        }

        switch (key)
        {
            case Key.Left:
                dragMode = isShift
                    ? LabelEditorDragMode.ResizeLeft
                    : LabelEditorDragMode.ResizeRight;
                return true;

            case Key.Right:
                dragMode = isShift
                    ? LabelEditorDragMode.ResizeRight
                    : LabelEditorDragMode.ResizeLeft;
                return true;

            case Key.Up:
                dragMode = isShift
                    ? LabelEditorDragMode.ResizeTop
                    : LabelEditorDragMode.ResizeBottom;
                return true;

            case Key.Down:
                dragMode = isShift
                    ? LabelEditorDragMode.ResizeBottom
                    : LabelEditorDragMode.ResizeTop;
                return true;

            default:
                return false;
        }
    }

    // ###########################################################################################
    // Returns true when a newly drawn editor rectangle is too small to be considered intentional.
    // This prevents accidental tiny drags from opening the new-label prompt.
    // ###########################################################################################
    public static bool IsLabelEditorRectangleTooSmall(Rect rect)
    {
        const double minimumWidth = 15.0;
        const double minimumHeight = 15.0;
        const double minimumArea = minimumWidth * minimumHeight;

        return rect.Width < minimumWidth ||
               rect.Height < minimumHeight ||
               (rect.Width * rect.Height) < minimumArea;
    }

    // ###########################################################################################
    // Builds non-overlapping resize-handle hit rectangles so corner drags keep two-axis behavior
    // even when the selected component is too small for all handle zones to coexist.
    // ###########################################################################################
    public static List<(Rect HitRect, LabelEditorDragMode DragMode)> BuildLabelEditorHandleHitRects(Rect localRect, double scale)
    {
        double cornerHandleSize = Math.Clamp(9.0 / scale, 4.0, 12.0);
        double sideHandleThickness = Math.Clamp(6.0 / scale, 2.5, 7.0);
        double cornerHalf = cornerHandleSize / 2.0;
        double sideHalf = sideHandleThickness / 2.0;
        double minimumGap = Math.Clamp(2.0 / scale, 1.0, 3.0);

        var hitRects = new List<(Rect HitRect, LabelEditorDragMode DragMode)>(8)
        {
            (new Rect(localRect.Left - cornerHalf, localRect.Top - cornerHalf, cornerHandleSize, cornerHandleSize), LabelEditorDragMode.ResizeTopLeft),
            (new Rect(localRect.Right - cornerHalf, localRect.Top - cornerHalf, cornerHandleSize, cornerHandleSize), LabelEditorDragMode.ResizeTopRight),
            (new Rect(localRect.Right - cornerHalf, localRect.Bottom - cornerHalf, cornerHandleSize, cornerHandleSize), LabelEditorDragMode.ResizeBottomRight),
            (new Rect(localRect.Left - cornerHalf, localRect.Bottom - cornerHalf, cornerHandleSize, cornerHandleSize), LabelEditorDragMode.ResizeBottomLeft)
        };

        double horizontalSideHitLength = Math.Max(0.0, localRect.Width - (cornerHandleSize * 2.0) - minimumGap);
        if (horizontalSideHitLength > 0.0)
        {
            double horizontalSideLeft = localRect.Center.X - (horizontalSideHitLength / 2.0);

            hitRects.Add((new Rect(horizontalSideLeft, localRect.Top - sideHalf, horizontalSideHitLength, sideHandleThickness), LabelEditorDragMode.ResizeTop));
            hitRects.Add((new Rect(horizontalSideLeft, localRect.Bottom - sideHalf, horizontalSideHitLength, sideHandleThickness), LabelEditorDragMode.ResizeBottom));
        }

        double verticalSideHitLength = Math.Max(0.0, localRect.Height - (cornerHandleSize * 2.0) - minimumGap);
        if (verticalSideHitLength > 0.0)
        {
            double verticalSideTop = localRect.Center.Y - (verticalSideHitLength / 2.0);

            hitRects.Add((new Rect(localRect.Right - sideHalf, verticalSideTop, sideHandleThickness, verticalSideHitLength), LabelEditorDragMode.ResizeRight));
            hitRects.Add((new Rect(localRect.Left - sideHalf, verticalSideTop, sideHandleThickness, verticalSideHitLength), LabelEditorDragMode.ResizeLeft));
        }

        return hitRects;
    }
    }
}