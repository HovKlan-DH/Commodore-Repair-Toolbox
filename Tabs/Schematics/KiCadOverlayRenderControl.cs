using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Collections.Generic;

namespace Tabs.TabSchematics
{
    public enum KiCadOverlayPrimitiveKind
    {
        Line,
        Rectangle,
        Ellipse,
        Polyline
    }

    public sealed class KiCadOverlayPrimitive
    {
        public KiCadOverlayPrimitiveKind Kind { get; init; }
        public Point Start { get; init; }
        public Point End { get; init; }
        public Rect Rect { get; init; }
        public IReadOnlyList<Point> Points { get; init; } = Array.Empty<Point>();
        public Pen? Pen { get; init; }
        public IBrush? Fill { get; init; }
    }

    public sealed class KiCadOverlayRenderControl : Control
    {
        private IReadOnlyList<KiCadOverlayPrimitive> thisPrimitives = Array.Empty<KiCadOverlayPrimitive>();

        public IReadOnlyList<KiCadOverlayPrimitive> Primitives => this.thisPrimitives;

        // ###########################################################################################
        // Replaces the current render geometry and triggers a redraw.
        // ###########################################################################################
        public void SetGeometry(IReadOnlyList<KiCadOverlayPrimitive>? primitives)
        {
            this.thisPrimitives = primitives ?? Array.Empty<KiCadOverlayPrimitive>();
            this.InvalidateVisual();
        }

        // ###########################################################################################
        // Clears all render geometry and triggers a redraw.
        // ###########################################################################################
        public void ClearGeometry()
        {
            this.thisPrimitives = Array.Empty<KiCadOverlayPrimitive>();
            this.InvalidateVisual();
        }

        // ###########################################################################################
        // Forces a redraw after layout changes so the overlay remains visually in sync.
        // ###########################################################################################
        protected override Size ArrangeOverride(Size finalSize)
        {
            var result = base.ArrangeOverride(finalSize);
            this.InvalidateVisual();
            return result;
        }

        // ###########################################################################################
        // Draws all KiCad overlay primitives in one control instead of creating thousands of child
        // controls on a Canvas.
        // ###########################################################################################
        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (this.thisPrimitives.Count == 0)
            {
                return;
            }

            for (int i = 0; i < this.thisPrimitives.Count; i++)
            {
                var primitive = this.thisPrimitives[i];

                switch (primitive.Kind)
                {
                    case KiCadOverlayPrimitiveKind.Line:
                        if (primitive.Pen != null)
                        {
                            context.DrawLine(primitive.Pen, primitive.Start, primitive.End);
                        }
                        break;

                    case KiCadOverlayPrimitiveKind.Rectangle:
                        context.DrawRectangle(primitive.Fill, primitive.Pen, primitive.Rect);
                        break;

                    case KiCadOverlayPrimitiveKind.Ellipse:
                        context.DrawEllipse(
                            primitive.Fill,
                            primitive.Pen,
                            primitive.Rect.Center,
                            primitive.Rect.Width / 2.0,
                            primitive.Rect.Height / 2.0);
                        break;

                    case KiCadOverlayPrimitiveKind.Polyline:
                        if (primitive.Pen == null || primitive.Points.Count < 2)
                        {
                            break;
                        }

                        for (int p = 1; p < primitive.Points.Count; p++)
                        {
                            context.DrawLine(
                                primitive.Pen,
                                primitive.Points[p - 1],
                                primitive.Points[p]);
                        }
                        break;
                }
            }
        }
    }
}