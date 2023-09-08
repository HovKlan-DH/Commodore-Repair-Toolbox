using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Commodore_Retro_Toolbox
{
    public partial class Main : Form
    {

        private Image image;
        private float zoomFactor = 1.0f;
        private Point lastMousePosition;
        private CustomPanel panelMain;
        private Panel panelImage;

        public Main()
        {
            InitializeComponent();

            panelMain = new CustomPanel
            {
                Size = new Size(500, 350),
                Location = new Point(0, 0),
                AutoScroll = true,
                Dock = DockStyle.Fill,
            };
            tabPage1.Controls.Add(panelMain);


            //image = Image.FromFile("Application.StartupPath + "\\Data\\Schematics.gif");
            image = Image.FromFile(Application.StartupPath + "\\Data\\Image.jpg");
            panelImage = new Panel
            {
                Size = image.Size,
                BackgroundImage = image,
                BackgroundImageLayout = ImageLayout.Zoom,
                Dock = DockStyle.None,
            };
            panelMain.Controls.Add(panelImage);

            panelMain.CustomMouseWheel += new MouseEventHandler(PanelMain_MouseWheel); 
            panelImage.MouseDown += PanelImage_MouseDown;
            panelImage.MouseUp += PanelImage_MouseUp;
            panelImage.MouseMove += PanelImage_MouseMove;
            

            // Enable double buffering for the panels (smoother drawing updates)
            panelMain.DoubleBuffered(true);
            panelImage.DoubleBuffered(true);

        }



        private void PanelMain_MouseWheel(object sender, MouseEventArgs e)
        {
            Debug.WriteLine("MouseWheel event 2");
            float oldZoomFactor = zoomFactor;

            // Change the zoom factor based on the mouse wheel movement.
            if (e.Delta > 0)
                zoomFactor *= 1.5f;
            else
                zoomFactor /= 1.5f;

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
        }

        private void PanelImage_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                Debug.WriteLine("MouseDown event 2");
                lastMousePosition = e.Location;
            }
        }

        private void PanelImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                Debug.WriteLine("MouseMove event 2");
                int dx = e.X - lastMousePosition.X;
                int dy = e.Y - lastMousePosition.Y;

                panelMain.AutoScrollPosition = new Point(-panelMain.AutoScrollPosition.X - dx, -panelMain.AutoScrollPosition.Y - dy);
            }
        }

        private void PanelImage_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                Debug.WriteLine("MouseUp event 2");
                lastMousePosition = Point.Empty;
            }
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
