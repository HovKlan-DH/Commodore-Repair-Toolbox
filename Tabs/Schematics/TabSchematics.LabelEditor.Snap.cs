using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Handlers.DataHandling;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tabs.TabSchematics;
using Handlers.Geometry;

namespace CRT;

// ###########################################################################################
// Snapping maths for the label editor - aligning a moved, resized or newly drawn highlight
// to its neighbours, and producing the guide lines shown while snapping.
//
// Part of the TabSchematics partial class - see TabSchematics.axaml.cs for the tab overview.
// ###########################################################################################
public partial class TabSchematics
{
    // ###########################################################################################
    // Snaps active resize edges to nearby neighbor edges within 2 px, or emits exact-match guides
    // without changing the rectangle when keyboard resizing wants visual alignment only.
    // Snap candidates are rejected when another component blocks the path to that neighbor.
    // Only components currently visible in the viewport can participate in snap alignment.
    // When multiple visible neighbors align to the same snapped edge, guides are shown to all.
    // ###########################################################################################
    private void ApplyLabelEditorResizeSnap(
        EditableComponentHighlight currentHighlight,
        ref double left,
        ref double top,
        ref double right,
        ref double bottom,
        List<(Point Start, Point End)> snapGuides,
        bool suppressSnap,
        bool snapOnMatch = true,
        LabelEditorDragMode? dragModeOverride = null)
    {
        const double snapThreshold = 2.0;
        const double epsilon = 0.001;
        const double guideMatchThreshold = 0.5;

        LabelEditorDragMode dragMode = dragModeOverride ?? this.thisLabelEditorDragMode;

        if (suppressSnap ||
            dragMode == LabelEditorDragMode.None ||
            dragMode == LabelEditorDragMode.Move)
        {
            return;
        }

        string schematicName = this.GetCurrentSchematicName();

        bool resizesTop =
            dragMode == LabelEditorDragMode.ResizeTop ||
            dragMode == LabelEditorDragMode.ResizeTopLeft ||
            dragMode == LabelEditorDragMode.ResizeTopRight;

        bool resizesBottom =
            dragMode == LabelEditorDragMode.ResizeBottom ||
            dragMode == LabelEditorDragMode.ResizeBottomLeft ||
            dragMode == LabelEditorDragMode.ResizeBottomRight;

        bool resizesLeft =
            dragMode == LabelEditorDragMode.ResizeLeft ||
            dragMode == LabelEditorDragMode.ResizeTopLeft ||
            dragMode == LabelEditorDragMode.ResizeBottomLeft;

        bool resizesRight =
            dragMode == LabelEditorDragMode.ResizeRight ||
            dragMode == LabelEditorDragMode.ResizeTopRight ||
            dragMode == LabelEditorDragMode.ResizeBottomRight;

        static bool RangesOverlap(double a1, double a2, double b1, double b2)
        {
            return Math.Min(a2, b2) > Math.Max(a1, b1);
        }

        static Rect BuildCurrentRect(double leftValue, double topValue, double rightValue, double bottomValue)
        {
            return new Rect(
                leftValue,
                topValue,
                Math.Max(1.0, rightValue - leftValue),
                Math.Max(1.0, bottomValue - topValue));
        }

        Rect? visiblePixelRect = null;

        if (this.currentFullResBitmap != null &&
            this.SchematicsContainer.Bounds.Width > 0 &&
            this.SchematicsContainer.Bounds.Height > 0 &&
            RectGeometry.TryInvert(this.schematicsMatrix, out var inverseMatrix))
        {
            var contentRect = this.GetLabelEditorImageContentRect();

            if (contentRect.Width > 0 && contentRect.Height > 0)
            {
                var containerRect = new Rect(this.SchematicsContainer.Bounds.Size);
                var visibleLocalRect = containerRect.TransformToAABB(inverseMatrix);

                double clippedLeft = Math.Max(contentRect.Left, visibleLocalRect.Left);
                double clippedTop = Math.Max(contentRect.Top, visibleLocalRect.Top);
                double clippedRight = Math.Min(contentRect.Right, visibleLocalRect.Right);
                double clippedBottom = Math.Min(contentRect.Bottom, visibleLocalRect.Bottom);

                if (clippedRight > clippedLeft && clippedBottom > clippedTop)
                {
                    double bitmapWidth = this.currentFullResBitmap.PixelSize.Width;
                    double bitmapHeight = this.currentFullResBitmap.PixelSize.Height;

                    double pixelLeft = Math.Clamp(
                        ((clippedLeft - contentRect.X) / contentRect.Width) * bitmapWidth,
                        0.0,
                        bitmapWidth);

                    double pixelTop = Math.Clamp(
                        ((clippedTop - contentRect.Y) / contentRect.Height) * bitmapHeight,
                        0.0,
                        bitmapHeight);

                    double pixelRight = Math.Clamp(
                        ((clippedRight - contentRect.X) / contentRect.Width) * bitmapWidth,
                        0.0,
                        bitmapWidth);

                    double pixelBottom = Math.Clamp(
                        ((clippedBottom - contentRect.Y) / contentRect.Height) * bitmapHeight,
                        0.0,
                        bitmapHeight);

                    if (pixelRight > pixelLeft && pixelBottom > pixelTop)
                    {
                        visiblePixelRect = new Rect(
                            pixelLeft,
                            pixelTop,
                            pixelRight - pixelLeft,
                            pixelBottom - pixelTop);
                    }
                }
            }
        }

        bool IsRectVisibleInCurrentView(Rect rect)
        {
            if (!visiblePixelRect.HasValue)
            {
                return true;
            }

            var visibleRect = visiblePixelRect.Value;

            return rect.Right > visibleRect.Left &&
                   rect.Left < visibleRect.Right &&
                   rect.Bottom > visibleRect.Top &&
                   rect.Top < visibleRect.Bottom;
        }

        bool IsVerticalPathBlocked(double sourceY, double targetY, Rect currentRect, EditableComponentHighlight targetHighlight)
        {
            double minY = Math.Min(sourceY, targetY);
            double maxY = Math.Max(sourceY, targetY);

            foreach (var other in this.thisLabelEditorWorkingHighlights)
            {
                if (ReferenceEquals(other, currentHighlight) ||
                    ReferenceEquals(other, targetHighlight) ||
                    !string.Equals(other.SchematicName, schematicName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var otherRect = new Rect(other.X, other.Y, other.Width, other.Height);

                if (!RangesOverlap(currentRect.Left, currentRect.Right, otherRect.Left, otherRect.Right))
                {
                    continue;
                }

                if (otherRect.Bottom > minY && otherRect.Top < maxY)
                {
                    return true;
                }
            }

            return false;
        }

        bool IsHorizontalPathBlocked(double sourceX, double targetX, Rect currentRect, EditableComponentHighlight targetHighlight)
        {
            double minX = Math.Min(sourceX, targetX);
            double maxX = Math.Max(sourceX, targetX);

            foreach (var other in this.thisLabelEditorWorkingHighlights)
            {
                if (ReferenceEquals(other, currentHighlight) ||
                    ReferenceEquals(other, targetHighlight) ||
                    !string.Equals(other.SchematicName, schematicName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var otherRect = new Rect(other.X, other.Y, other.Width, other.Height);

                if (!RangesOverlap(currentRect.Top, currentRect.Bottom, otherRect.Top, otherRect.Bottom))
                {
                    continue;
                }

                if (otherRect.Right > minX && otherRect.Left < maxX)
                {
                    return true;
                }
            }

            return false;
        }

        static bool TryBuildHorizontalGuide(Rect currentRect, Rect targetRect, double y, out (Point Start, Point End) guide)
        {
            guide = default;

            double startX = Math.Min(currentRect.Left, targetRect.Left);
            double endX = Math.Max(currentRect.Right, targetRect.Right);

            if (endX - startX <= 0.01)
            {
                return false;
            }

            guide = (new Point(startX, y), new Point(endX, y));
            return true;
        }

        static bool TryBuildVerticalGuide(Rect currentRect, Rect targetRect, double x, out (Point Start, Point End) guide)
        {
            guide = default;

            double startY = Math.Min(currentRect.Top, targetRect.Top);
            double endY = Math.Max(currentRect.Bottom, targetRect.Bottom);

            if (endY - startY <= 0.01)
            {
                return false;
            }

            guide = (new Point(x, startY), new Point(x, endY));
            return true;
        }

        if (resizesTop || resizesBottom)
        {
            Rect currentRect = BuildCurrentRect(left, top, right, bottom);
            double sourceY = resizesTop ? currentRect.Top : currentRect.Bottom;
            double bestDistance = snapThreshold + 0.001;
            double bestY = sourceY;
            var bestTargets = new List<EditableComponentHighlight>();

            void ConsiderVerticalCandidate(EditableComponentHighlight other, double candidateY)
            {
                double distance = Math.Abs(sourceY - candidateY);
                if (distance > snapThreshold ||
                    IsVerticalPathBlocked(sourceY, candidateY, currentRect, other))
                {
                    return;
                }

                if (!snapOnMatch)
                {
                    if (distance > guideMatchThreshold)
                    {
                        return;
                    }

                    if (bestTargets.Count == 0)
                    {
                        bestY = candidateY;
                        bestTargets.Add(other);
                        return;
                    }

                    if (Math.Abs(candidateY - bestY) <= guideMatchThreshold &&
                        !bestTargets.Contains(other))
                    {
                        bestTargets.Add(other);
                    }

                    return;
                }

                if (distance < bestDistance - epsilon)
                {
                    bestDistance = distance;
                    bestY = candidateY;
                    bestTargets.Clear();
                    bestTargets.Add(other);
                    return;
                }

                if (Math.Abs(distance - bestDistance) <= epsilon &&
                    Math.Abs(candidateY - bestY) <= epsilon &&
                    !bestTargets.Contains(other))
                {
                    bestTargets.Add(other);
                }
            }

            foreach (var other in this.thisLabelEditorWorkingHighlights)
            {
                if (ReferenceEquals(other, currentHighlight) ||
                    this.IsSelectedLabelEditorHighlight(other) ||
                    !string.Equals(other.SchematicName, schematicName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var otherRect = new Rect(other.X, other.Y, other.Width, other.Height);
                if (!IsRectVisibleInCurrentView(otherRect))
                {
                    continue;
                }

                ConsiderVerticalCandidate(other, otherRect.Top);
                ConsiderVerticalCandidate(other, otherRect.Bottom);
            }

            if (bestTargets.Count > 0)
            {
                if (snapOnMatch)
                {
                    if (resizesTop)
                    {
                        top = bestY;
                    }
                    else
                    {
                        bottom = bestY;
                    }
                }

                currentRect = BuildCurrentRect(left, top, right, bottom);

                foreach (var bestTarget in bestTargets)
                {
                    var targetRect = new Rect(bestTarget.X, bestTarget.Y, bestTarget.Width, bestTarget.Height);

                    if (TryBuildHorizontalGuide(currentRect, targetRect, bestY, out var guide))
                    {
                        snapGuides.Add(guide);
                    }
                }
            }
        }

        if (resizesLeft || resizesRight)
        {
            Rect currentRect = BuildCurrentRect(left, top, right, bottom);
            double sourceX = resizesLeft ? currentRect.Left : currentRect.Right;
            double bestDistance = snapThreshold + 0.001;
            double bestX = sourceX;
            var bestTargets = new List<EditableComponentHighlight>();

            void ConsiderHorizontalCandidate(EditableComponentHighlight other, double candidateX)
            {
                double distance = Math.Abs(sourceX - candidateX);
                if (distance > snapThreshold ||
                    IsHorizontalPathBlocked(sourceX, candidateX, currentRect, other))
                {
                    return;
                }

                if (!snapOnMatch)
                {
                    if (distance > guideMatchThreshold)
                    {
                        return;
                    }

                    if (bestTargets.Count == 0)
                    {
                        bestX = candidateX;
                        bestTargets.Add(other);
                        return;
                    }

                    if (Math.Abs(candidateX - bestX) <= guideMatchThreshold &&
                        !bestTargets.Contains(other))
                    {
                        bestTargets.Add(other);
                    }

                    return;
                }

                if (distance < bestDistance - epsilon)
                {
                    bestDistance = distance;
                    bestX = candidateX;
                    bestTargets.Clear();
                    bestTargets.Add(other);
                    return;
                }

                if (Math.Abs(distance - bestDistance) <= epsilon &&
                    Math.Abs(candidateX - bestX) <= epsilon &&
                    !bestTargets.Contains(other))
                {
                    bestTargets.Add(other);
                }
            }

            foreach (var other in this.thisLabelEditorWorkingHighlights)
            {
                if (ReferenceEquals(other, currentHighlight) ||
                    this.IsSelectedLabelEditorHighlight(other) ||
                    !string.Equals(other.SchematicName, schematicName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var otherRect = new Rect(other.X, other.Y, other.Width, other.Height);
                if (!IsRectVisibleInCurrentView(otherRect))
                {
                    continue;
                }

                ConsiderHorizontalCandidate(other, otherRect.Left);
                ConsiderHorizontalCandidate(other, otherRect.Right);
            }

            if (bestTargets.Count > 0)
            {
                if (snapOnMatch)
                {
                    if (resizesLeft)
                    {
                        left = bestX;
                    }
                    else
                    {
                        right = bestX;
                    }
                }

                currentRect = BuildCurrentRect(left, top, right, bottom);

                foreach (var bestTarget in bestTargets)
                {
                    var targetRect = new Rect(bestTarget.X, bestTarget.Y, bestTarget.Width, bestTarget.Height);

                    if (TryBuildVerticalGuide(currentRect, targetRect, bestX, out var guide))
                    {
                        snapGuides.Add(guide);
                    }
                }
            }
        }
    }

    // ###########################################################################################
    // Snaps the moved selection bounds to nearby neighbor edges while preserving the current
    // selection layout. SHIFT still suppresses the snap, matching resize behavior.
    // When snapOnMatch is false, no movement is applied and guides are shown only for exact matches.
    // ###########################################################################################
    private void ApplyLabelEditorMoveSnap(
        IReadOnlyList<EditableComponentHighlight> selectedHighlights,
        IReadOnlyDictionary<EditableComponentHighlight, Rect> sourceRects,
        ref Rect movedSelectionBounds,
        List<(Point Start, Point End)> snapGuides,
        bool suppressSnap,
        bool snapOnMatch = true)
    {
        const double snapThreshold = 2.0;
        const double epsilon = 0.001;
        const double guideMatchThreshold = 0.5;

        if (suppressSnap || selectedHighlights.Count == 0)
        {
            return;
        }

        string schematicName = this.GetCurrentSchematicName();
        var selectedSet = new HashSet<EditableComponentHighlight>(selectedHighlights);

        static bool RangesOverlap(double a1, double a2, double b1, double b2)
        {
            return Math.Min(a2, b2) > Math.Max(a1, b1);
        }

        Rect GetRect(EditableComponentHighlight highlight)
        {
            if (sourceRects.TryGetValue(highlight, out var sourceRect))
            {
                return sourceRect;
            }

            return new Rect(highlight.X, highlight.Y, highlight.Width, highlight.Height);
        }

        Rect? visiblePixelRect = null;

        if (this.currentFullResBitmap != null &&
            this.SchematicsContainer.Bounds.Width > 0 &&
            this.SchematicsContainer.Bounds.Height > 0 &&
            RectGeometry.TryInvert(this.schematicsMatrix, out var inverseMatrix))
        {
            var contentRect = this.GetLabelEditorImageContentRect();

            if (contentRect.Width > 0 && contentRect.Height > 0)
            {
                var containerRect = new Rect(this.SchematicsContainer.Bounds.Size);
                var visibleLocalRect = containerRect.TransformToAABB(inverseMatrix);

                double clippedLeft = Math.Max(contentRect.Left, visibleLocalRect.Left);
                double clippedTop = Math.Max(contentRect.Top, visibleLocalRect.Top);
                double clippedRight = Math.Min(contentRect.Right, visibleLocalRect.Right);
                double clippedBottom = Math.Min(contentRect.Bottom, visibleLocalRect.Bottom);

                if (clippedRight > clippedLeft && clippedBottom > clippedTop)
                {
                    double bitmapWidth = this.currentFullResBitmap.PixelSize.Width;
                    double bitmapHeight = this.currentFullResBitmap.PixelSize.Height;

                    double pixelLeft = Math.Clamp(
                        ((clippedLeft - contentRect.X) / contentRect.Width) * bitmapWidth,
                        0.0,
                        bitmapWidth);

                    double pixelTop = Math.Clamp(
                        ((clippedTop - contentRect.Y) / contentRect.Height) * bitmapHeight,
                        0.0,
                        bitmapHeight);

                    double pixelRight = Math.Clamp(
                        ((clippedRight - contentRect.X) / contentRect.Width) * bitmapWidth,
                        0.0,
                        bitmapWidth);

                    double pixelBottom = Math.Clamp(
                        ((clippedBottom - contentRect.Y) / contentRect.Height) * bitmapHeight,
                        0.0,
                        bitmapHeight);

                    if (pixelRight > pixelLeft && pixelBottom > pixelTop)
                    {
                        visiblePixelRect = new Rect(
                            pixelLeft,
                            pixelTop,
                            pixelRight - pixelLeft,
                            pixelBottom - pixelTop);
                    }
                }
            }
        }

        bool IsRectVisibleInCurrentView(Rect rect)
        {
            if (!visiblePixelRect.HasValue)
            {
                return true;
            }

            var visibleRect = visiblePixelRect.Value;

            return rect.Right > visibleRect.Left &&
                   rect.Left < visibleRect.Right &&
                   rect.Bottom > visibleRect.Top &&
                   rect.Top < visibleRect.Bottom;
        }

        bool IsVerticalPathBlocked(double sourceY, double targetY, Rect currentRect, EditableComponentHighlight targetHighlight)
        {
            double minY = Math.Min(sourceY, targetY);
            double maxY = Math.Max(sourceY, targetY);

            foreach (var other in this.thisLabelEditorWorkingHighlights)
            {
                if (selectedSet.Contains(other) ||
                    ReferenceEquals(other, targetHighlight) ||
                    !string.Equals(other.SchematicName, schematicName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var otherRect = GetRect(other);

                if (!RangesOverlap(currentRect.Left, currentRect.Right, otherRect.Left, otherRect.Right))
                {
                    continue;
                }

                if (otherRect.Bottom > minY && otherRect.Top < maxY)
                {
                    return true;
                }
            }

            return false;
        }

        bool IsHorizontalPathBlocked(double sourceX, double targetX, Rect currentRect, EditableComponentHighlight targetHighlight)
        {
            double minX = Math.Min(sourceX, targetX);
            double maxX = Math.Max(sourceX, targetX);

            foreach (var other in this.thisLabelEditorWorkingHighlights)
            {
                if (selectedSet.Contains(other) ||
                    ReferenceEquals(other, targetHighlight) ||
                    !string.Equals(other.SchematicName, schematicName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var otherRect = GetRect(other);

                if (!RangesOverlap(currentRect.Top, currentRect.Bottom, otherRect.Top, otherRect.Bottom))
                {
                    continue;
                }

                if (otherRect.Right > minX && otherRect.Left < maxX)
                {
                    return true;
                }
            }

            return false;
        }

        static bool TryBuildHorizontalGuide(Rect currentRect, Rect targetRect, double y, out (Point Start, Point End) guide)
        {
            guide = default;

            double startX = Math.Min(currentRect.Left, targetRect.Left);
            double endX = Math.Max(currentRect.Right, targetRect.Right);

            if (endX - startX <= 0.01)
            {
                return false;
            }

            guide = (new Point(startX, y), new Point(endX, y));
            return true;
        }

        static bool TryBuildVerticalGuide(Rect currentRect, Rect targetRect, double x, out (Point Start, Point End) guide)
        {
            guide = default;

            double startY = Math.Min(currentRect.Top, targetRect.Top);
            double endY = Math.Max(currentRect.Bottom, targetRect.Bottom);

            if (endY - startY <= 0.01)
            {
                return false;
            }

            guide = (new Point(x, startY), new Point(x, endY));
            return true;
        }

        Rect currentMovedSelectionBounds = movedSelectionBounds;

        double bestDeltaY = 0.0;
        double bestDistanceY = snapThreshold + 0.001;
        var bestVerticalTargets = new List<(EditableComponentHighlight Target, double Y)>();

        void ConsiderVerticalCandidate(EditableComponentHighlight other, double sourceY, double candidateY)
        {
            double delta = candidateY - sourceY;
            double distance = Math.Abs(delta);

            if (distance > snapThreshold ||
                IsVerticalPathBlocked(sourceY, candidateY, currentMovedSelectionBounds, other))
            {
                return;
            }

            if (!snapOnMatch)
            {
                if (distance > guideMatchThreshold)
                {
                    return;
                }

                if (bestVerticalTargets.Count == 0)
                {
                    bestDeltaY = delta;
                    bestVerticalTargets.Add((other, candidateY));
                    return;
                }

                if (Math.Abs(delta - bestDeltaY) <= guideMatchThreshold &&
                    !bestVerticalTargets.Any(target =>
                        ReferenceEquals(target.Target, other) &&
                        Math.Abs(target.Y - candidateY) <= guideMatchThreshold))
                {
                    bestVerticalTargets.Add((other, candidateY));
                }

                return;
            }

            if (distance < bestDistanceY - epsilon)
            {
                bestDistanceY = distance;
                bestDeltaY = delta;
                bestVerticalTargets.Clear();
                bestVerticalTargets.Add((other, candidateY));
                return;
            }

            if (Math.Abs(distance - bestDistanceY) <= epsilon &&
                Math.Abs(delta - bestDeltaY) <= epsilon &&
                !bestVerticalTargets.Any(target =>
                    ReferenceEquals(target.Target, other) &&
                    Math.Abs(target.Y - candidateY) <= epsilon))
            {
                bestVerticalTargets.Add((other, candidateY));
            }
        }

        foreach (var other in this.thisLabelEditorWorkingHighlights)
        {
            if (selectedSet.Contains(other) ||
                !string.Equals(other.SchematicName, schematicName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var otherRect = GetRect(other);
            if (!IsRectVisibleInCurrentView(otherRect))
            {
                continue;
            }

            ConsiderVerticalCandidate(other, currentMovedSelectionBounds.Top, otherRect.Top);
            ConsiderVerticalCandidate(other, currentMovedSelectionBounds.Top, otherRect.Bottom);
            ConsiderVerticalCandidate(other, currentMovedSelectionBounds.Bottom, otherRect.Top);
            ConsiderVerticalCandidate(other, currentMovedSelectionBounds.Bottom, otherRect.Bottom);
        }

        if (bestVerticalTargets.Count > 0)
        {
            if (snapOnMatch)
            {
                movedSelectionBounds = new Rect(
                    movedSelectionBounds.X,
                    movedSelectionBounds.Y + bestDeltaY,
                    movedSelectionBounds.Width,
                    movedSelectionBounds.Height);
            }

            currentMovedSelectionBounds = movedSelectionBounds;

            foreach (var target in bestVerticalTargets)
            {
                var targetRect = GetRect(target.Target);

                if (TryBuildHorizontalGuide(currentMovedSelectionBounds, targetRect, target.Y, out var guide))
                {
                    snapGuides.Add(guide);
                }
            }
        }

        currentMovedSelectionBounds = movedSelectionBounds;

        double bestDeltaX = 0.0;
        double bestDistanceX = snapThreshold + 0.001;
        var bestHorizontalTargets = new List<(EditableComponentHighlight Target, double X)>();

        void ConsiderHorizontalCandidate(EditableComponentHighlight other, double sourceX, double candidateX)
        {
            double delta = candidateX - sourceX;
            double distance = Math.Abs(delta);

            if (distance > snapThreshold ||
                IsHorizontalPathBlocked(sourceX, candidateX, currentMovedSelectionBounds, other))
            {
                return;
            }

            if (!snapOnMatch)
            {
                if (distance > guideMatchThreshold)
                {
                    return;
                }

                if (bestHorizontalTargets.Count == 0)
                {
                    bestDeltaX = delta;
                    bestHorizontalTargets.Add((other, candidateX));
                    return;
                }

                if (Math.Abs(delta - bestDeltaX) <= guideMatchThreshold &&
                    !bestHorizontalTargets.Any(target =>
                        ReferenceEquals(target.Target, other) &&
                        Math.Abs(target.X - candidateX) <= guideMatchThreshold))
                {
                    bestHorizontalTargets.Add((other, candidateX));
                }

                return;
            }

            if (distance < bestDistanceX - epsilon)
            {
                bestDistanceX = distance;
                bestDeltaX = delta;
                bestHorizontalTargets.Clear();
                bestHorizontalTargets.Add((other, candidateX));
                return;
            }

            if (Math.Abs(distance - bestDistanceX) <= epsilon &&
                Math.Abs(delta - bestDeltaX) <= epsilon &&
                !bestHorizontalTargets.Any(target =>
                    ReferenceEquals(target.Target, other) &&
                    Math.Abs(target.X - candidateX) <= epsilon))
            {
                bestHorizontalTargets.Add((other, candidateX));
            }
        }

        foreach (var other in this.thisLabelEditorWorkingHighlights)
        {
            if (selectedSet.Contains(other) ||
                !string.Equals(other.SchematicName, schematicName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var otherRect = GetRect(other);
            if (!IsRectVisibleInCurrentView(otherRect))
            {
                continue;
            }

            ConsiderHorizontalCandidate(other, currentMovedSelectionBounds.Left, otherRect.Left);
            ConsiderHorizontalCandidate(other, currentMovedSelectionBounds.Left, otherRect.Right);
            ConsiderHorizontalCandidate(other, currentMovedSelectionBounds.Right, otherRect.Left);
            ConsiderHorizontalCandidate(other, currentMovedSelectionBounds.Right, otherRect.Right);
        }

        if (bestHorizontalTargets.Count > 0)
        {
            if (snapOnMatch)
            {
                movedSelectionBounds = new Rect(
                    movedSelectionBounds.X + bestDeltaX,
                    movedSelectionBounds.Y,
                    movedSelectionBounds.Width,
                    movedSelectionBounds.Height);
            }

            currentMovedSelectionBounds = movedSelectionBounds;

            foreach (var target in bestHorizontalTargets)
            {
                var targetRect = GetRect(target.Target);

                if (TryBuildVerticalGuide(currentMovedSelectionBounds, targetRect, target.X, out var guide))
                {
                    snapGuides.Add(guide);
                }
            }
        }
    }

    // ###########################################################################################
    // Applies neighbor-edge snapping to a newly drawn rectangle by reusing the existing resize
    // snap logic for all four edges while the rectangle is still only a draft.
    // ###########################################################################################
    private void ApplyNewLabelEditorRectangleSnap(
        ref Rect rect,
        List<(Point Start, Point End)> snapGuides,
        bool suppressSnap)
    {
        if (suppressSnap ||
            rect.Width <= 0 ||
            rect.Height <= 0 ||
            string.IsNullOrWhiteSpace(this.GetCurrentSchematicName()))
        {
            return;
        }

        double left = rect.Left;
        double top = rect.Top;
        double right = rect.Right;
        double bottom = rect.Bottom;

        var draftHighlight = new EditableComponentHighlight
        {
            SchematicName = this.GetCurrentSchematicName(),
            X = rect.X,
            Y = rect.Y,
            Width = rect.Width,
            Height = rect.Height
        };

        LabelEditorDragMode originalDragMode = this.thisLabelEditorDragMode;

        try
        {
            this.thisLabelEditorDragMode = LabelEditorDragMode.ResizeTop;
            this.ApplyLabelEditorResizeSnap(
                draftHighlight,
                ref left,
                ref top,
                ref right,
                ref bottom,
                snapGuides,
                suppressSnap: false);

            this.thisLabelEditorDragMode = LabelEditorDragMode.ResizeBottom;
            this.ApplyLabelEditorResizeSnap(
                draftHighlight,
                ref left,
                ref top,
                ref right,
                ref bottom,
                snapGuides,
                suppressSnap: false);

            this.thisLabelEditorDragMode = LabelEditorDragMode.ResizeLeft;
            this.ApplyLabelEditorResizeSnap(
                draftHighlight,
                ref left,
                ref top,
                ref right,
                ref bottom,
                snapGuides,
                suppressSnap: false);

            this.thisLabelEditorDragMode = LabelEditorDragMode.ResizeRight;
            this.ApplyLabelEditorResizeSnap(
                draftHighlight,
                ref left,
                ref top,
                ref right,
                ref bottom,
                snapGuides,
                suppressSnap: false);
        }
        finally
        {
            this.thisLabelEditorDragMode = originalDragMode;
        }

        rect = new Rect(
            left,
            top,
            Math.Max(1.0, right - left),
            Math.Max(1.0, bottom - top));
    }
}