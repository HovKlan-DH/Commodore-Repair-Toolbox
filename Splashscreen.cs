using System;
using System.Windows.Forms;

namespace Commodore_Repair_Toolbox
{
    public partial class Splashscreen : Form
    {
        public static Splashscreen Current { get; private set; }

        public Splashscreen()
        {
            InitializeComponent();

            Current = this;

            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;

            label4.Text = string.Empty;

            // Center the picture box within the panel
            pictureBox1.Location = new System.Drawing.Point(
                (panelMain.Width - pictureBox1.Width) / 2,
                pictureBox1.Location.Y
            );

            // Center all labels horizontally within the panel
            label1.Location = new System.Drawing.Point(
                (panelMain.Width - label1.Width) / 2,
                label1.Location.Y
            );

            label2.Location = new System.Drawing.Point(
                (panelMain.Width - label2.Width) / 2,
                label2.Location.Y
            );

            label3.Location = new System.Drawing.Point(
                (panelMain.Width - label3.Width) / 2,
                label3.Location.Y
            );

            label2.Text = Main.GetAssemblyVersion();
        }

        public void UpdateStatus(string status)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(UpdateStatus), status);
                return;
            }

            label4.Text = status;

            label4.Location = new System.Drawing.Point(
                (panelMain.Width - label4.Width) / 2,
                label4.Location.Y
            );

            label4.Refresh();
            panelMain.Refresh();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (ReferenceEquals(Current, this))
            {
                Current = null;
            }

            base.OnFormClosed(e);
        }
    }
}