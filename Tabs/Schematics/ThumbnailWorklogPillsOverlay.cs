using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using Handlers.Geometry;

namespace Tabs.TabSchematics
{
    // ###########################################################################################
    // Draws a small "#N" pill - no drawn bounds, no status - for every saved worklog entry that
    // belongs to this thumbnail's schematic, centered on the entry's marked area. Colored by
    // category, matching TabSchematics.Worklog.cs's CreateWorklogEntriesListBadge outer badge (the
    // main view's badge also carries a state pill; this one deliberately does not - the thumbnail
    // is just meant to give an at-a-glance idea of where issues are marked, not their status). Used
    // by the thumbnail gallery; visibility follows the same "Show worklogs" checkbox as the
    // full-size overlay (WorklogEntriesOverlay) and the main view's badges.
    //
    // A self-contained Control that computes its own centered content rect from its arranged Bounds
    // (see RectGeometry.GetCenteredImageContentRect - the thumbnail Image is Stretch-aligned, unlike
    // the main SchematicsImage, so content is centered rather than anchored at the origin), the same
    // technique WorklogEntriesOverlay uses.
    // ###########################################################################################
    public sealed class ThumbnailWorklogPillsOverlay : Control
    {
        public readonly record struct Pill(Point PixelCenter, Color Color, int EntryId);

        public static readonly StyledProperty<IReadOnlyList<Pill>> PillsProperty =
            AvaloniaProperty.Register<ThumbnailWorklogPillsOverlay, IReadOnlyList<Pill>>(
                nameof(Pills), defaultValue: Array.Empty<Pill>());

        public static readonly StyledProperty<PixelSize> BitmapPixelSizeProperty =
            AvaloniaProperty.Register<ThumbnailWorklogPillsOverlay, PixelSize>(
                nameof(BitmapPixelSize), defaultValue: new PixelSize(0, 0));

        static ThumbnailWorklogPillsOverlay()
        {
            AffectsRender<ThumbnailWorklogPillsOverlay>(PillsProperty, BitmapPixelSizeProperty);
        }

        public IReadOnlyList<Pill> Pills
        {
            get => this.GetValue(PillsProperty);
            set => this.SetValue(PillsProperty, value ?? Array.Empty<Pill>());
        }

        public PixelSize BitmapPixelSize
        {
            get => this.GetValue(BitmapPixelSizeProperty);
            set => this.SetValue(BitmapPixelSizeProperty, value);
        }

        // ###########################################################################################
        // A little smaller than the main view's badge (FontSize 11, Padding 8,3, CornerRadius 10),
        // but not much smaller - thumbnails do not zoom, so unlike the main view's badges there is
        // no inverse-scale transform keeping this constant; it is just a fixed screen size.
        // ###########################################################################################
        public double FontSize { get; set; } = 10.0;

        protected override Size ArrangeOverride(Size finalSize)
        {
            var result = base.ArrangeOverride(finalSize);
            this.InvalidateVisual();
            return result;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var pills = this.Pills;
            if (pills.Count == 0)
                return;

            if (this.Bounds.Width <= 0 || this.Bounds.Height <= 0)
                return;

            var bitmapPixelSize = this.BitmapPixelSize;
            if (bitmapPixelSize.Width <= 0 || bitmapPixelSize.Height <= 0)
                return;

            var contentRect = RectGeometry.GetCenteredImageContentRect(this.Bounds.Size, bitmapPixelSize);
            if (contentRect.Width <= 0 || contentRect.Height <= 0)
                return;

            double sx = contentRect.Width / bitmapPixelSize.Width;
            double sy = contentRect.Height / bitmapPixelSize.Height;

            foreach (var pill in pills)
            {
                var center = new Point(
                    contentRect.X + (pill.PixelCenter.X * sx),
                    contentRect.Y + (pill.PixelCenter.Y * sy));

                this.DrawPill(context, center, pill.Color, pill.EntryId);
            }
        }

        // ###########################################################################################
        // Draws one rounded, category-colored pill with a white bold "#N" label, centered at the
        // given point - the same look as CreateWorklogEntriesListBadge's outer badge, minus the
        // inner state pill (this overlay never shows status).
        // ###########################################################################################
        private void DrawPill(DrawingContext context, Point center, Color color, int entryId)
        {
            var typeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);
            var formattedText = new FormattedText(
                $"#{entryId.ToString(CultureInfo.InvariantCulture)}",
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                this.FontSize,
                Brushes.White);

            const double paddingX = 5.0;
            const double paddingY = 2.0;

            double width = formattedText.Width + (paddingX * 2);
            double height = formattedText.Height + (paddingY * 2);
            var pillRect = new Rect(center.X - (width / 2.0), center.Y - (height / 2.0), width, height);

            context.DrawRectangle(
                new SolidColorBrush(color),
                new Pen(Brushes.White, 1.0),
                pillRect,
                height / 2.0,
                height / 2.0);

            var textOrigin = new Point(
                pillRect.X + paddingX,
                pillRect.Y + paddingY);
            context.DrawText(formattedText, textOrigin);
        }
    }
}
