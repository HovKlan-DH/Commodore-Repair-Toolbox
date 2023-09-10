using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Commodore_Retro_Toolbox
{
    public partial class Main : Form
    {

        // UI elements
        private CustomPanel panelMain;
        private Panel panelImage;
        private PictureBox overlayPictureBox1;
        private Bitmap overlayBitmap1;
//        private PictureBox listBox1;

        // Main variables
        private Image image;
        private float zoomFactor = 1.0f;
        private Point lastMousePosition;
        private Size originalOverlaySize;
        private Point originalOverlayLocation;
        private bool isResizedByMouseWheel = false;


        // ###########################################################################################
        // Main()
        // -----------------
        // This is where it all starts :-)
        // ###########################################################################################

        public Main()
        {
            InitializeComponent();
            InitializeTabMain();
            InitializeList();
        }


        // ###########################################################################################
        // InitializeTabMain()
        // -------------------
        // Setup the tab named "Main"
        // ###########################################################################################

        private void InitializeTabMain()
        {


            // Initialize main panel, make it part of the "tabMain" and fill the entire size
            panelMain = new CustomPanel
            {
                Size = new Size(10, 10),
                Location = new Point(0, 0),
                AutoScroll = true,
                Dock = DockStyle.Fill,
            };
            panel1.Controls.Add(panelMain);

            // Load an image and initialize image panel
            image = Image.FromFile(Application.StartupPath + "\\Data\\Commodore 64 Breadbin\\250425\\Schematics 1of2.gif");
            panelImage = new Panel
            {
                Size = image.Size,
                BackgroundImage = image,
                BackgroundImageLayout = ImageLayout.Zoom,
                Dock = DockStyle.None,
            };
            panelMain.Controls.Add(panelImage);

            // Initialize overlay PictureBox and store its original dimensions
            overlayPictureBox1 = new PictureBox
            {
                Name = "U3",
                Size = new Size(250, 410),
                Location = new Point(1226, 1672),
                BackColor = Color.Transparent,
            };
            panelImage.Controls.Add(overlayPictureBox1);
            originalOverlaySize = overlayPictureBox1.Size;
            originalOverlayLocation = overlayPictureBox1.Location;



            /*
            // Create and set a bitmap for the overlay PictureBox
            overlayBitmap1 = new Bitmap(overlayPictureBox1.Width, overlayPictureBox1.Height);
            using (Graphics g = Graphics.FromImage(overlayBitmap1))
            {
                g.Clear(Color.FromArgb(128, Color.Red)); // 50% opacity
            }
            overlayPictureBox1.Image = overlayBitmap1;
            */

            // Attach event handlers for mouse events and form shown
            panelMain.CustomMouseWheel += new MouseEventHandler(PanelMain_MouseWheel);
            panelImage.MouseDown += PanelImage_MouseDown;
            panelImage.MouseUp += PanelImage_MouseUp;
            panelImage.MouseMove += PanelImage_MouseMove;
            this.Shown += new EventHandler(this.Main_Shown);
            panelMain.Resize += new EventHandler(this.PanelMain_Resize);

            overlayPictureBox1.MouseDown += PanelImage_MouseDown;
            overlayPictureBox1.MouseUp += PanelImage_MouseUp;
            overlayPictureBox1.MouseMove += PanelImage_MouseMove;
            overlayPictureBox1.MouseEnter += new EventHandler(this.Overlay_MouseEnter);
            overlayPictureBox1.MouseLeave += new EventHandler(this.Overlay_MouseLeave);

            // Enable double buffering for smoother updates
            panelMain.DoubleBuffered(true);
            panelImage.DoubleBuffered(true);
        }


        // ###########################################################################################
        // InitializeTabMain()
        // ------------
        // Setup the tab named "Main"
        // ###########################################################################################

        private void InitializeList()
        {

            panelImageList.AutoScroll = true;
            panelImageList.Location = new Point(0, 0);
            panelImageList.Dock = DockStyle.Fill;
            groupBoxList.Controls.Add(panelImageList);

            // ---

            PictureBox listBox1 = new PictureBox
            {
                Size = new Size(167, 110), // Set the size you want
                Location = new Point(4, 25), // Set the location within the parent control
                Image = Image.FromFile(Application.StartupPath + "\\Data\\Commodore 64 Breadbin\\250425\\Schematics 1of2.gif"), // Load an image from file
                SizeMode = PictureBoxSizeMode.Zoom, // Optional: Set how the image should be displayed
//                BorderStyle = BorderStyle.FixedSingle,
            };

            // Add the Paint event handler to draw the border
            listBox1.Paint += new PaintEventHandler((sender, e) =>
            {
                float penWidth = 1;
                using (Pen pen = new Pen(Color.Red, penWidth))
                {
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                    pen.DashPattern = new float[] { 4, 2 };
                    float halfPenWidth = penWidth / 2;
                    e.Graphics.DrawRectangle(pen, halfPenWidth, halfPenWidth, listBox1.Width - penWidth, listBox1.Height - penWidth);
                }
            });
            panelImageList.Controls.Add(listBox1);

            Label listLabel1 = new Label
            {
                Text = "Schematics 1 of 2",
                Location = new Point(0, 0),  // Set the location within the parent control
                BorderStyle = BorderStyle.FixedSingle,
                AutoSize = true,
                BackColor = Color.White,  // Set the background color to Red
                Padding = new Padding(left:2, top:2, right:2, bottom:2),
            };
            listBox1.Controls.Add(listLabel1);

            // ---

            PictureBox listBox2 = new PictureBox
            {
                Size = new Size(167, 110), // Set the size you want
                Location = new Point(4, 140), // Set the location within the parent control
                Image = Image.FromFile(Application.StartupPath + "\\Data\\Commodore 64 Breadbin\\250425\\Schematics 2of2.gif"), // Load an image from file
                SizeMode = PictureBoxSizeMode.Zoom, // Optional: Set how the image should be displayed
                BorderStyle = BorderStyle.FixedSingle,
            };

            panelImageList.Controls.Add(listBox2);

            Label listLabel2 = new Label
            {
                Text = "Schematics 2 of 2",
                Location = new Point(0, 0),  // Set the location within the parent control
                BorderStyle = BorderStyle.FixedSingle,
                AutoSize = true,
                BackColor = Color.White,  // Set the background color to Red
                Padding = new Padding(left: 2, top: 2, right: 2, bottom: 2),
            };
            listBox2.Controls.Add(listLabel2);

            // ---

            PictureBox listBox3 = new PictureBox
            {
                Size = new Size(167, 110), // Set the size you want
                Location = new Point(4, 255), // Set the location within the parent control
                Image = Image.FromFile(Application.StartupPath + "\\Data\\Commodore 64 Breadbin\\250425\\Board layout 250425.png"), // Load an image from file
                SizeMode = PictureBoxSizeMode.Zoom, // Optional: Set how the image should be displayed
                BorderStyle = BorderStyle.FixedSingle,
            };

            panelImageList.Controls.Add(listBox3);

            Label listLabel3 = new Label
            {
                Text = "Layout",
                Location = new Point(0, 0),  // Set the location within the parent control
                BorderStyle = BorderStyle.FixedSingle,
                AutoSize = true,
                BackColor = Color.White,  // Set the background color to Red
                Padding = new Padding(left: 2, top: 2, right: 2, bottom: 2),
            };
            listBox3.Controls.Add(listLabel3);

            // ---

            PictureBox listBox4 = new PictureBox
            {
                Size = new Size(167, 110), // Set the size you want
                Location = new Point(4, 370), // Set the location within the parent control
                Image = Image.FromFile(Application.StartupPath + "\\Data\\Commodore 64 Breadbin\\250425\\Print top.JPG"), // Load an image from file
                SizeMode = PictureBoxSizeMode.Zoom, // Optional: Set how the image should be displayed
                BorderStyle = BorderStyle.FixedSingle,
            };

            panelImageList.Controls.Add(listBox4);

            Label listLabel4 = new Label
            {
                Text = "Top",
                Location = new Point(0, 0),  // Set the location within the parent control
                BorderStyle = BorderStyle.FixedSingle,
                AutoSize = true,
                BackColor = Color.White,  // Set the background color to Red
                Padding = new Padding(left: 2, top: 2, right: 2, bottom: 2),
            };
            listBox4.Controls.Add(listLabel4);

            // ---
            /*
            PictureBox listBox5 = new PictureBox
            {
                Size = new Size(167, 110), // Set the size you want
                Location = new Point(4, 490), // Set the location within the parent control
                Image = Image.FromFile(Application.StartupPath + "\\Data\\Schematics.gif"), // Load an image from file
                SizeMode = PictureBoxSizeMode.Zoom, // Optional: Set how the image should be displayed
                BorderStyle = BorderStyle.FixedSingle,
            };

            panelImageList.Controls.Add(listBox5);

            Label listLabel5 = new Label
            {
                Text = "Whatever",
                Location = new Point(0, 0),  // Set the location within the parent control
                BorderStyle = BorderStyle.FixedSingle,
                AutoSize = true,
                BackColor = Color.White,  // Set the background color to Red
                Padding = new Padding(left: 2, top: 2, right: 2, bottom: 2),
            };
            listBox5.Controls.Add(listLabel5);
            */

            // ---

            double ratio = (double)(100 - (double)(((double)(listBox1.Image.Width - listBox1.Width) * 100) / listBox1.Image.Width)) / 100;
            int newLocationX = (int)Math.Round(1226 * ratio,8);
            int newLocationY = (int)Math.Round(1672 * ratio,8);
            int newSizeWidth = (int)Math.Round(listBox1.Size.Width * ratio, 8);
            int newSizeHeight = (int)Math.Round(listBox1.Size.Height * ratio, 8);
            int newHeight = (int)(listBox1.Height);

            // Initialize overlay PictureBox and store its original dimensions
            PictureBox overlayPictureBox100 = new PictureBox
            {
                Name = "U3",
                Size = new Size(6, 4),
                Location = new Point(45, 62),
                BackColor = Color.Transparent,
            };
            listBox1.Controls.Add(overlayPictureBox100);


            // Create a new bitmap with the new dimensions
            Bitmap newBmp = new Bitmap(overlayPictureBox100.Width, overlayPictureBox100.Height);

            // Perform drawing operations here, if any
            using (Graphics g = Graphics.FromImage(newBmp))
            {
                g.Clear(Color.FromArgb(128, Color.Red)); // 50% opacity
            }

            // Set the new bitmap
            overlayPictureBox100.Image = newBmp;

        }


        // ###########################################################################################
        // Main_Shown()
        // ------------
        // What to do AFTER the Main() form has been shown (this is not the tab named "Main")?
        // ###########################################################################################

        private void Main_Shown(object sender, EventArgs e)
        {
            FitImageToPanel();
            
            //panelMain.AutoScrollPosition = new Point(750, 400);FitImageToPanel();
        }
        
        
        // ###########################################################################################
        // FitImageToPanel()
        // -----------------
        // Resize image to fit main panel display (show 100% of the image)
        // ###########################################################################################

        private void FitImageToPanel()
        {
            // Set the zoom factor
            float xZoomFactor = (float)panelMain.Width / image.Width;
            float yZoomFactor = (float)panelMain.Height / image.Height;
            zoomFactor = Math.Min(xZoomFactor, yZoomFactor);

            // Update the image size to the zoom factor
            panelImage.Size = new Size((int)(image.Width * zoomFactor), (int)(image.Height * zoomFactor));

            // HEST
            // Update the overlays
            if (overlayPictureBox1 != null)
            {
                int newWidth = (int)(originalOverlaySize.Width * zoomFactor);
                int newHeight = (int)(originalOverlaySize.Height * zoomFactor);
                overlayPictureBox1.Size = new Size(newWidth, newHeight);
                overlayPictureBox1.Location = new Point((int)(originalOverlayLocation.X * zoomFactor), (int)(originalOverlayLocation.Y * zoomFactor));

                // Dispose the overlay transparent bitmap and create a new one (bitmaps cannot be resized)
                if (overlayPictureBox1.Image != null)
                {
                    overlayPictureBox1.Image.Dispose();
                }
                Bitmap newBmp = new Bitmap(newWidth, newHeight);
                using (Graphics g = Graphics.FromImage(newBmp))
                {
                    g.Clear(Color.FromArgb(128, Color.Red)); // 50% opacity
                }
                overlayPictureBox1.Image = newBmp;
            }
        }


        private void PanelMain_Resize(object sender, EventArgs e)
        {
            if (!isResizedByMouseWheel)
            {
                FitImageToPanel();
            }

            isResizedByMouseWheel = false;
        }

        private void PanelMain_MouseWheel(object sender, MouseEventArgs e)
        {
            Debug.WriteLine("MouseWheel event");
            float oldZoomFactor = zoomFactor;

            Debug.WriteLine("Before: panelMain.Width="+ panelMain.Width+", panelImage.Width="+panelImage.Width+", image.Width=" + image.Width+ ", panelMain.AutoScrollPosition.X="+ panelMain.AutoScrollPosition.X);

            Debug.WriteLine("zoomFactor="+ zoomFactor);

            // Change the zoom factor based on the mouse wheel movement.
            bool hasZoomChanged = false;
            if (e.Delta > 0)
            {
                if (zoomFactor <= 5) {
                    Debug.WriteLine("Zoom In");
                    zoomFactor *= 1.5f;
                    hasZoomChanged = true;
                }
            }
            else 
            {
                if (panelImage.Width > panelMain.Width || panelImage.Height > panelMain.Height)
                {
                    Debug.WriteLine("Zoom Out");
                    zoomFactor /= 1.5f;
                    hasZoomChanged = true;
                }
            }

            if (hasZoomChanged)
            {
                isResizedByMouseWheel = true;

                // Calculate the new size of the imagePanel.
                Size newSize = new Size((int)(image.Width * zoomFactor), (int)(image.Height * zoomFactor));

                // Calculate the current mouse position relative to the content in the containerPanel.
                Point mousePosition = new Point(e.X - panelMain.AutoScrollPosition.X, e.Y - panelMain.AutoScrollPosition.Y);

                // Calculate what the new scroll position should be so that the content under the mouse stays under the mouse.
                Point newScrollPosition = new Point(
                    (int)(mousePosition.X * (zoomFactor / oldZoomFactor)),
                    (int)(mousePosition.Y * (zoomFactor / oldZoomFactor))
                );

                // Update the size of the imagePanel.
                panelImage.Size = newSize;

                // Update the scroll position of the containerPanel.
                panelMain.AutoScrollPosition = new Point(newScrollPosition.X - e.X, newScrollPosition.Y - e.Y);

                if (overlayPictureBox1 != null)
                {
                    int newWidth = (int)(originalOverlaySize.Width * zoomFactor);
                    int newHeight = (int)(originalOverlaySize.Height * zoomFactor);

                    overlayPictureBox1.Size = new Size(newWidth, newHeight);
                    overlayPictureBox1.Location = new Point((int)(originalOverlayLocation.X * zoomFactor), (int)(originalOverlayLocation.Y * zoomFactor));

                    // Dispose of the old bitmap
                    if (overlayPictureBox1.Image != null)
                    {
                        overlayPictureBox1.Image.Dispose();
                    }

                    // Create a new bitmap with the new dimensions
                    Bitmap newBmp = new Bitmap(newWidth, newHeight);

                    // Perform drawing operations here, if any
                    using (Graphics g = Graphics.FromImage(newBmp))
                    {
                        g.Clear(Color.FromArgb(128, Color.Red)); // 50% opacity
                    }

                    // Set the new bitmap
                    overlayPictureBox1.Image = newBmp;
                }

                Debug.WriteLine("After: panelMain.Width=" + panelMain.Width + ", panelImage.Width=" + panelImage.Width + ", image.Width=" + image.Width + ", panelMain.AutoScrollPosition.X=" + panelMain.AutoScrollPosition.X);

            }
        }

        private void PanelImage_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                Debug.WriteLine("MouseDown event");
                lastMousePosition = e.Location;
            }
        }

        private void PanelImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                Debug.WriteLine("MouseMove event");
                int dx = e.X - lastMousePosition.X;
                int dy = e.Y - lastMousePosition.Y;

                panelMain.AutoScrollPosition = new Point(-panelMain.AutoScrollPosition.X - dx, -panelMain.AutoScrollPosition.Y - dy);
            }
        }

        private void PanelImage_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                Debug.WriteLine("MouseUp event");
                lastMousePosition = Point.Empty;
            }
        }

        private void Overlay_MouseEnter(object sender, EventArgs e)
        {
            this.Cursor = Cursors.Hand;
            Control control = sender as Control;
            if (control != null)
            {
//                label7.Text = control.Name;
            }
//            label7.Visible = true;
        }

        private void Overlay_MouseLeave(object sender, EventArgs e)
        {
            this.Cursor = Cursors.Default;
//            label7.Visible = false;
        }


    }

    public class CustomPanel : Panel
    {
        public event MouseEventHandler CustomMouseWheel;

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            CustomMouseWheel?.Invoke(this, e);
        }
    }

}