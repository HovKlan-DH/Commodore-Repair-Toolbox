using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Commodore_Repair_Toolbox
{
    public class OverlayInfo
    {
        public Rectangle Bounds { get; set; }
        public Color Color { get; set; }
        public int Opacity { get; set; } // 0-255
        public bool Highlighted { get; set; }
        public string ComponentLabel { get; set; }
    }

    // Fired when an overlay is clicked (left or right)
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

    // Fired when mouse enters or leaves an overlay
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

        // Events
        public event EventHandler<OverlayClickedEventArgs> OverlayClicked;
        public event EventHandler<OverlayHoverChangedEventArgs> OverlayHoverChanged;

        // These let Main.cs handle "empty space" logic, e.g. panning
        public event MouseEventHandler OverlayPanelMouseDown;
        public event MouseEventHandler OverlayPanelMouseMove;
        public event MouseEventHandler OverlayPanelMouseUp;

        private OverlayInfo currentHover;

        // --- Right-click single-click vs. drag logic ---
        private bool _isRightPanning = false;
        private Point _rightDownLocation = Point.Empty;
        private const int DRAG_THRESHOLD = 5;

        public OverlayPanel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint,
                true
            );
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

            if (e.Button == MouseButtons.Left)
            {
                // LEFT-CLICK => detect overlays immediately
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
                    // No overlay => empty space
                    OverlayPanelMouseDown?.Invoke(this, e);
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                // RIGHT-CLICK => we won't detect overlays yet
                // We'll decide on single-click vs. drag later
                _isRightPanning = false;
                _rightDownLocation = e.Location;

                // Fire "empty space" down in case user drags
                OverlayPanelMouseDown?.Invoke(this, e);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // Always pass mouse-move to "empty space" event
            OverlayPanelMouseMove?.Invoke(this, e);

            // LEFT button or no button => do normal hover detection
            // RIGHT => we might be panning or deciding to pan
            if (e.Button == MouseButtons.Right)
            {
                // Check if we've moved enough to call it a drag
                int dx = e.Location.X - _rightDownLocation.X;
                int dy = e.Location.Y - _rightDownLocation.Y;
                if (!_isRightPanning && (Math.Abs(dx) > DRAG_THRESHOLD || Math.Abs(dy) > DRAG_THRESHOLD))
                {
                    _isRightPanning = true;
                }

                // If we're panning, skip overlay hover detection
                if (_isRightPanning) return;
            }

            // If not panning with right-click, do hover detection
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
                // Hover changed
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

            if (e.Button == MouseButtons.Right)
            {
                if (_isRightPanning)
                {
                    // We were dragging => done panning
                    _isRightPanning = false;
                }
                else
                {
                    // Single right-click => detect overlays
//                    bool clickedOverlay = false;
                    for (int i = Overlays.Count - 1; i >= 0; i--)
                    {
                        if (Overlays[i].Bounds.Contains(e.Location))
                        {
//                            clickedOverlay = true;
                            // Pass a "right-click" overlay event
                            OverlayClicked?.Invoke(this, new OverlayClickedEventArgs(Overlays[i], e));
                            break;
                        }
                    }
                }
            }
        }
    }
}