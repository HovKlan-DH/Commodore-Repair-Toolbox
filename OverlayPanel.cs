using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Commodore_Retro_Toolbox
{
    public class OverlayInfo
    {
        public Rectangle Bounds { get; set; }
        public Color Color { get; set; }
        public int Opacity { get; set; }
        public bool Highlighted { get; set; }
        public string ComponentLabel { get; set; }
    }

    // For clicks on overlays
    public class OverlayClickedEventArgs : EventArgs
    {
        public OverlayInfo OverlayInfo { get; }
        public MouseEventArgs MouseArgs { get; }
        public OverlayClickedEventArgs(OverlayInfo overlayInfo, MouseEventArgs mouseArgs)
        {
            OverlayInfo = overlayInfo;
            MouseArgs = mouseArgs;
        }
    }

    // For hover changes (enter/leave)
    public class OverlayHoverChangedEventArgs : EventArgs
    {
        public OverlayInfo OverlayInfo { get; }
        public bool IsHovering { get; }
        public Point MouseLocation { get; }
        public OverlayHoverChangedEventArgs(OverlayInfo overlayInfo, bool isHovering, Point mouseLocation)
        {
            OverlayInfo = overlayInfo;
            IsHovering = isHovering;
            MouseLocation = mouseLocation;
        }
    }

    public class OverlayPanel : Panel
    {
        public List<OverlayInfo> Overlays { get; } = new List<OverlayInfo>();

        // Fired if an overlay is clicked (left or right)
        public event EventHandler<OverlayClickedEventArgs> OverlayClicked;

        // Fired if mouse enters or leaves an overlay
        public event EventHandler<OverlayHoverChangedEventArgs> OverlayHoverChanged;

        // Fired if the user clicked empty space in the panel
        public event MouseEventHandler OverlayPanelMouseDown;
        public event MouseEventHandler OverlayPanelMouseMove;
        public event MouseEventHandler OverlayPanelMouseUp;

        private OverlayInfo currentHover; // track which overlay is currently hovered

        public OverlayPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint, true);
            BackColor = Color.Transparent;
            Dock = DockStyle.Fill;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            foreach (var overlay in Overlays)
            {
                using (var brush = new SolidBrush(
                    Color.FromArgb(overlay.Highlighted ? overlay.Opacity : 0, overlay.Color)))
                {
                    e.Graphics.FillRectangle(brush, overlay.Bounds);
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            bool clickedOverlay = false;
            for (int i = Overlays.Count - 1; i >= 0; i--)
            {
                if (Overlays[i].Bounds.Contains(e.Location))
                {
                    clickedOverlay = true;
                    OverlayClicked?.Invoke(this, new OverlayClickedEventArgs(Overlays[i], e));
                    break;
                }
            }

            if (!clickedOverlay)
            {
                // Fire "empty space" mouse-down
                OverlayPanelMouseDown?.Invoke(this, e);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // Fire "empty space" mouse-move always
            OverlayPanelMouseMove?.Invoke(this, e);

            // Check if we are hovering any overlay
            OverlayInfo found = null;
            for (int i = Overlays.Count - 1; i >= 0; i--)
            {
                if (Overlays[i].Bounds.Contains(e.Location))
                {
                    found = Overlays[i];
                    break;
                }
            }

            if (found != currentHover)
            {
                // Hover changed: left old overlay or entered a new one
                if (currentHover != null)
                {
                    // We left the old overlay
                    OverlayHoverChanged?.Invoke(this,
                        new OverlayHoverChangedEventArgs(currentHover, false, e.Location));
                }
                if (found != null)
                {
                    // We entered a new overlay
                    OverlayHoverChanged?.Invoke(this,
                        new OverlayHoverChangedEventArgs(found, true, e.Location));
                }
                currentHover = found;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            OverlayPanelMouseUp?.Invoke(this, e);
        }
    }
}
