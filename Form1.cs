namespace Commodore_Retro_Toolbox
{
    public partial class Form1 : Form
    {
        private Image image; // Store the loaded image
        private Rectangle destinationRect; // Store the destination rectangle

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
        }

        private void tabPage1_SizeChanged(object sender, EventArgs e)
        {
            // Suspend layout updates during resizing
            // HEST - check if this makes any difference????????
            panel1.SuspendLayout();
            panel2.SuspendLayout();

            // Check if the selected tab is the one containing PictureBox2
            if (tabControl1.SelectedTab == tabPage1)
            {
                // Calculate the new size for PictureBox2 based on the TabPage's size while maintaining the aspect ratio
                float aspectRatio = (float)image.Width / image.Height;
                int newWidth = tabPage1.ClientSize.Width - panel1.Left - 10; // Adjust for padding or margins if needed
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
                panel2.Size = panel1.Size;
            }

            // Resume layout updates
            panel1.ResumeLayout();
            panel2.ResumeLayout();
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