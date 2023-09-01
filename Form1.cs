using System.Drawing;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Commodore_Retro_Toolbox
{
    public partial class Form1 : Form
    {
        private Image image; // Store the loaded image
        private Rectangle destinationRect; // Store the destination rectangle
        private Panel overlayPanel; // Panel for overlays
        private bool isResizing = false; // Flag to track resizing state
        private const int WM_SIZING = 0x214;

        public Form1()
        {
            InitializeComponent();

            comboBox1.SelectedIndex = 0;
            comboBox2.SelectedIndex = 0;

            panel2.BackColor = Color.FromArgb(128, Color.Red); // Set the background color with 50% opacity

            // Load the image once in the constructor
            try
            {
                image = Image.FromFile("D:\\Data\\Development\\Visual Studio\\Commodore-Retro-Toolbox\\Data\\Commodore 64 Breadbin\\250425\\Schematics 1of2.gif");
                destinationRect = new Rectangle(0, 0, panel1.Width, panel1.Height);
            }
            catch (Exception ex)
            {
                // Handle any exceptions that occur during image loading
                MessageBox.Show("Error loading image: " + ex.Message);
            }

            // Enable double buffering for the panels
            panel1.DoubleBuffered(true);
            panel2.DoubleBuffered(true);

            // Wire up the ResizeBegin and ResizeEnd event handlers
            this.ResizeBegin += Form1_ResizeBegin;
            this.ResizeEnd += Form1_ResizeEnd;

            resize();
            
        }

        private void tabPage1_SizeChanged(object sender, EventArgs e)
        {

           

            resize();
            
        }

        private void resize()
        {


            // Suspend layout updates during resizing
            panel1.SuspendLayout();
            panel2.SuspendLayout();

            // Check if the selected tab is the one containing PictureBox2
            if (tabControl1.SelectedTab == tabPage1)
            {
                // Calculate the new size for PictureBox2 based on the TabPage's size while maintaining the aspect ratio
                float aspectRatio = (float)image.Width / image.Height;
                int newWidth = tabPage1.ClientSize.Width - panel1.Location.X - 10;
                int newHeight = (int)(newWidth / aspectRatio);

                if (newHeight > tabPage1.ClientSize.Height - panel1.Top - 10) // Check height restrictions
                {
                    newHeight = tabPage1.ClientSize.Height - panel1.Top - 10;
                    newWidth = (int)(newHeight * aspectRatio);
                }

                // Set the new size for PictureBox2
                panel1.Size = new Size(newWidth, newHeight);

                // Update the destination rectangle
                destinationRect.Size = panel1.Size;

                // Set the size of panel2 to match panel1
                //               panel2.Size = panel1.Size;

                // Resume layout updates only when resizing is complete
                if (!isResizing)
                {
                    int scaledX = (int)(((double)1230 * panel1.Width) / image.Width);
                    int scaledY = (int)(((double)1680 * panel1.Height) / image.Height);
                    int newObjectWidth = (int)(((double)250 / image.Width) * panel1.Width);
                    int newObjectHeight = (int)(((double)380 / image.Height) * panel1.Height);
                    //            Debug.WriteLine(image.Width.ToString() + ", " + panel1.Width.ToString() + ", " + scaledX);

                    panel2.Location = new Point(scaledX, scaledY);
                    panel2.Size = new Size(newObjectWidth, newObjectHeight);
                    // Ensure that Panel2 is always on top of Panel1
                    panel2.BringToFront();
                }

            }

            // Resume layout updates
            panel1.ResumeLayout();
            panel2.ResumeLayout();
        }


        // Handle the ResizeBegin event to indicate the start of resizing
        private void Form1_ResizeBegin(object sender, EventArgs e)
        {
            isResizing = true;
            panel2.Hide();
        }

        // Handle the ResizeEnd event to indicate the end of resizing
        private void Form1_ResizeEnd(object sender, EventArgs e)
        {
            isResizing = false;
            resize(); // Update the layout once resizing is complete
            panel2.Show();
        }

        private void panel1_Paint_1(object sender, PaintEventArgs e)
        {
            // Check if the image is loaded
            if (image != null)
            {
                // Draw the image within the destination rectangle
                e.Graphics.DrawImage(image, destinationRect);
            }
        }

    }
}