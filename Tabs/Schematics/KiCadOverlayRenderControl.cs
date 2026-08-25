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
        Polyline,
        Geometry
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
        public Geometry? Geometry { get; init; }

        // Rotation about Rect.Center, in Avalonia's clockwise Y-down degrees. Only Rectangle and
        // Ellipse honour it; it carries a rotated KiCad pad's true orientation.
        public double RotationDegrees { get; init; }
    }

    public sealed class KiCadOverlayRenderControl : Control
    {
        private IReadOnlyList<KiCadOverlayPrimitive> thisPrimitives = Array.Empty<KiCadOverlayPrimitive>();
        private IReadOnlyList<Geometry?> thisCachedPrimitiveGeometries = Array.Empty<Geometry?>();

        public IReadOnlyList<KiCadOverlayPrimitive> Primitives => this.thisPrimitives;

        // ###########################################################################################
        // Replaces the current render geometry, rebuilds any cached polyline paths, and triggers
        // a redraw so curved KiCad traces render smoothly without recreating UI child controls.
        // ###########################################################################################
        public void SetGeometry(IReadOnlyList<KiCadOverlayPrimitive>? primitives)
        {
            this.thisPrimitives = primitives ?? Array.Empty<KiCadOverlayPrimitive>();
            this.thisCachedPrimitiveGeometries = this.BuildCachedPrimitiveGeometries(this.thisPrimitives);
            this.InvalidateVisual();
        }

        // ###########################################################################################
        // Builds cached drawing geometries for primitives that benefit from path rendering.
        // Currently this is used for polylines so joins and curves render smoothly.
        // ###########################################################################################
        private IReadOnlyList<Geometry?> BuildCachedPrimitiveGeometries(IReadOnlyList<KiCadOverlayPrimitive> primitives)
        {
            var geometries = new Geometry?[primitives.Count];

            for (int i = 0; i < primitives.Count; i++)
            {
                var primitive = primitives[i];

                if (primitive.Kind == KiCadOverlayPrimitiveKind.Polyline &&
                    primitive.Points.Count >= 2)
                {
                    geometries[i] = BuildPolylineGeometry(primitive.Points);
                }
                else if (primitive.Kind == KiCadOverlayPrimitiveKind.Geometry &&
                         primitive.Geometry != null)
                {
                    geometries[i] = primitive.Geometry;
                }
            }

            return geometries;
        }

        // ###########################################################################################
        // Clears all render geometry, resets the cached path list, and triggers a redraw.
        // ###########################################################################################
        public void ClearGeometry()
        {
            this.thisPrimitives = Array.Empty<KiCadOverlayPrimitive>();
            this.thisCachedPrimitiveGeometries = Array.Empty<Geometry?>();
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
        // Builds one continuous geometry for a polyline so Avalonia can render joins and caps
        // smoothly instead of drawing each segment as an isolated line.
        // ###########################################################################################
        private static Geometry BuildPolylineGeometry(IReadOnlyList<Point> points)
        {
            var geometry = new StreamGeometry();

            using (var geometryContext = geometry.Open())
            {
                geometryContext.BeginFigure(points[0], isFilled: false);

                for (int i = 1; i < points.Count; i++)
                {
                    geometryContext.LineTo(points[i]);
                }

                geometryContext.EndFigure(isClosed: false);
            }

            return geometry;
        }

        // ###########################################################################################
        // Returns a pen configured for smooth KiCad trace rendering while preserving the original
        // brush, thickness, dash style, and miter limit.
        // Uses round caps and round joins to match the original trace appearance without recreating
        // thousands of UI elements.
        // ###########################################################################################
        private static Pen BuildSmoothedPolylinePen(Pen sourcePen)
        {
            return new Pen(
                sourcePen.Brush,
                sourcePen.Thickness,
                sourcePen.DashStyle,
                PenLineCap.Round,
                PenLineJoin.Round,
                sourcePen.MiterLimit);
        }

        // ###########################################################################################
        // Pushes the rotation a primitive asks for, turning about the centre of its own rect so a
        // rotated KiCad pad keeps its position and only changes orientation. Axis-aligned primitives
        // - which is nearly all of them - get an identity transform rather than a special case, so
        // the caller can always wrap the draw in a using block.
        // ###########################################################################################
        private static DrawingContext.PushedState PushPrimitiveRotation(
            DrawingContext context,
            KiCadOverlayPrimitive primitive)
        {
            if (Handlers.Geometry.KiCadPadGeometry.IsAxisAligned(primitive.RotationDegrees))
            {
                return context.PushTransform(Matrix.Identity);
            }

            Point centre = primitive.Rect.Center;
            double radians = primitive.RotationDegrees * Math.PI / 180.0;

            return context.PushTransform(
                Matrix.CreateTranslation(-centre.X, -centre.Y) *
                Matrix.CreateRotation(radians) *
                Matrix.CreateTranslation(centre.X, centre.Y));
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
                        using (PushPrimitiveRotation(context, primitive))
                        {
                            context.DrawRectangle(primitive.Fill, primitive.Pen, primitive.Rect);
                        }
                        break;

                    case KiCadOverlayPrimitiveKind.Ellipse:
                        using (PushPrimitiveRotation(context, primitive))
                        {
                            context.DrawEllipse(
                                primitive.Fill,
                                primitive.Pen,
                                primitive.Rect.Center,
                                primitive.Rect.Width / 2.0,
                                primitive.Rect.Height / 2.0);
                        }
                        break;

                    case KiCadOverlayPrimitiveKind.Polyline:
                        if (primitive.Pen == null || primitive.Points.Count < 2)
                        {
                            break;
                        }

                        var cachedPolylineGeometry =
                            i < this.thisCachedPrimitiveGeometries.Count
                                ? this.thisCachedPrimitiveGeometries[i]
                                : null;

                        if (cachedPolylineGeometry == null)
                        {
                            break;
                        }

                        context.DrawGeometry(
                            null,
                            BuildSmoothedPolylinePen(primitive.Pen),
                            cachedPolylineGeometry);
                        break;

                    case KiCadOverlayPrimitiveKind.Geometry:
                        var cachedGeometry =
                            i < this.thisCachedPrimitiveGeometries.Count
                                ? this.thisCachedPrimitiveGeometries[i]
                                : primitive.Geometry;

                        if (cachedGeometry == null)
                        {
                            break;
                        }

                        context.DrawGeometry(
                            primitive.Fill,
                            primitive.Pen,
                            cachedGeometry);
                        break;
                }
            }
        }



    }
}