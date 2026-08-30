using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using Handlers.Geometry;

namespace Tabs.TabSchematics
{
    // ###########################################################################################
    // Draws every saved worklog entry's marked area for the schematic currently on screen, each in
    // its own entry's category color - unlike SchematicHighlightsOverlay/ComponentLabelEditorOverlay,
    // which both draw every rectangle in one shared color. Used by the top-bar "Show worklogs"
    // checkbox; the "#N" badge and state pill for each entry are separate controls placed on a
    // canvas by TabSchematics.Worklog.cs, the same way the single draft entry's badge is.
    // ###########################################################################################
    public sealed class WorklogEntriesOverlay : Control
    {
        public readonly record struct Entry(Rect PixelRect, Color Color);

        private IReadOnlyList<Entry> thisEntries = Array.Empty<Entry>();

        public IReadOnlyList<Entry> Entries
        {
            get => this.thisEntries;
            set
            {
                this.thisEntries = value ?? Array.Empty<Entry>();
                this.InvalidateVisual();
            }
        }

        // ###########################################################################################
        // All three change what Render draws, so they invalidate on assignment rather than relying
        // on every call site to remember an InvalidateVisual afterwards - which is what the two
        // existing callers happened to do, leaving any future third caller with a stale overlay
        // drawn at the previous zoom's border thickness and dash spacing.
        // ###########################################################################################
        public static readonly StyledProperty<PixelSize> BitmapPixelSizeProperty =
            AvaloniaProperty.Register<WorklogEntriesOverlay, PixelSize>(
                nameof(BitmapPixelSize), defaultValue: new PixelSize(0, 0));

        public static readonly StyledProperty<Matrix> ViewMatrixProperty =
            AvaloniaProperty.Register<WorklogEntriesOverlay, Matrix>(
                nameof(ViewMatrix), defaultValue: Matrix.Identity);

        public static readonly StyledProperty<double> FillOpacityProperty =
            AvaloniaProperty.Register<WorklogEntriesOverlay, double>(
                nameof(FillOpacity), defaultValue: 0.12);

        static WorklogEntriesOverlay()
        {
            AffectsRender<WorklogEntriesOverlay>(BitmapPixelSizeProperty, ViewMatrixProperty, FillOpacityProperty);
        }

        public PixelSize BitmapPixelSize
        {
            get => this.GetValue(BitmapPixelSizeProperty);
            set => this.SetValue(BitmapPixelSizeProperty, value);
        }

        public Matrix ViewMatrix
        {
            get => this.GetValue(ViewMatrixProperty);
            set => this.SetValue(ViewMatrixProperty, value);
        }

        public double FillOpacity
        {
            get => this.GetValue(FillOpacityProperty);
            set => this.SetValue(FillOpacityProperty, value);
        }

        // ###########################################################################################
        // Forces a re-render whenever the control is re-arranged (e.g. after a splitter drag),
        // ensuring the marked areas are redrawn with up-to-date bounds.
        // ###########################################################################################
        protected override Size ArrangeOverride(Size finalSize)
        {
            var result = base.ArrangeOverride(finalSize);
            this.InvalidateVisual();
            return result;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (this.thisEntries.Count == 0)
                return;

            if (this.Bounds.Width <= 0 || this.Bounds.Height <= 0)
                return;

            if (this.BitmapPixelSize.Width <= 0 || this.BitmapPixelSize.Height <= 0)
                return;

            var contentRect = RectGeometry.GetImageContentRect(this.Bounds.Size, this.BitmapPixelSize);
            if (contentRect.Width <= 0 || contentRect.Height <= 0)
                return;

            double scale = Math.Max(0.0001, this.ViewMatrix.M11);
            double borderThickness = Math.Clamp(1.0 / scale, 0.5, 1.0);
            double fillOpacity = Math.Clamp(this.FillOpacity, 0.0, 1.0);
            var dashStyle = new DashStyle(new[] { Math.Clamp(6.0 / scale, 2.0, 6.0), Math.Clamp(4.0 / scale, 2.0, 4.0) }, 0);

            foreach (var entry in this.thisEntries)
            {
                var localRect = RectGeometry.PixelToLocalRect(entry.PixelRect, contentRect, this.BitmapPixelSize);
                var borderRect = RectGeometry.InsetRectForStroke(localRect, borderThickness);

                var fillBrush = new SolidColorBrush(entry.Color, fillOpacity);
                var pen = new Pen(new SolidColorBrush(entry.Color, 1.0), borderThickness, dashStyle);

                context.DrawRectangle(fillBrush, null, localRect);
                context.DrawRectangle(null, pen, borderRect);
            }
        }
    }
}
