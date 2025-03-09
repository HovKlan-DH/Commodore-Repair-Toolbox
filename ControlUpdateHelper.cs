using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Commodore_Repair_Toolbox
{
    public static class ControlUpdateHelper
    {
        private const int WM_SETREDRAW = 0x000B;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

        /// <summary>
        /// Temporarily disables painting for the specified control
        /// (prevents WM_PAINT messages from being processed).
        /// </summary>
        public static void BeginControlUpdate(Control control)
        {
            if (control == null) return;
            SendMessage(control.Handle, WM_SETREDRAW, 0, 0);
        }

        /// <summary>
        /// Re‐enables painting for the specified control and forces a refresh.
        /// </summary>
        public static void EndControlUpdate(Control control)
        {
            if (control == null) return;
            SendMessage(control.Handle, WM_SETREDRAW, 1, 0);
            control.Refresh();
        }
    }
}