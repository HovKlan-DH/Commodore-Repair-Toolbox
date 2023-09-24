using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Commodore_Retro_Toolbox;
using System.Linq;

namespace Commodore_Repair_Toolbox
{


    // #################################################################################


    public partial class Main : Form
    {

        // UI elements
        private CustomPanel panelMain;
        private Panel panelImage;

        Dictionary<string, string> listBoxNameValueMapping = new Dictionary<string, string>();

        // List to hold the actual values of selected items
        List<string> listBoxSelectedActualValues = new List<string>();

        // Main variables
        private float zoomFactor = 1.0f;
        private Point lastMousePosition;
        private bool isResizedByMouseWheel = false;

        // Overlay array in "Main" tab
        //List<PictureBox> overlayComponentsList = new List<PictureBox>();
        Dictionary<string, List<PictureBox>> overlayComponentsList = new Dictionary<string, List<PictureBox>>();
        List<PictureBox> overlayComponentsTab = new List<PictureBox>();
        Dictionary<int, Size> overlayComponentsTabOriginalSizes = new Dictionary<int, Size>();
        Dictionary<int, Point> overlayComponentsTabOriginalLocations = new Dictionary<int, Point>();

        // Create a Dictionary to map each image to its list of PictureBox overlays
        //Dictionary<string, List<PictureBox>> imageToOverlays = new Dictionary<string, List<PictureBox>>();

        List<Hardware> classHardware = new List<Hardware>();

        private Image image = Image.FromFile(Application.StartupPath + "\\Data\\Commodore 64 Breadbin\\250425\\Schematics 1of2.gif");
        //private int yPosition = 0;
        private string hardwareSelected = "Commodore 64 (Breadbin)";
        private string boardSelected = "250425";
        private string imageSelected = "Schematics 1 of 2";

        private List<PictureBox> visiblePictureBoxes = new List<PictureBox>();

        private bool isResizing = false;


        // ---------------------------------------------------------------------------------


        public Main()
        {
            InitializeComponent();

            DataStructure.GetAllData(classHardware);

            /*
            // Now you have a list of hardware, each containing a list of associated boards
            foreach (Hardware hardware in classHardware)
            {
                Debug.WriteLine("Hardware Name = " + hardware.Name + ", Folder = " + hardware.Folder);
                foreach (Board board in hardware.Boards)
                {
                    Debug.WriteLine("  Board Name = " + board.Name + ", Folder = " + board.Folder);
                    foreach (ComponentBoard component in board.Components)
                    {
                        Debug.WriteLine("    Component Name = " + component.NameLabel);
                    }
                    foreach (Commodore_Repair_Toolbox.File file in board.Files)
                    {
                        Debug.WriteLine("    File Name = " + file.Name + ", FileName = " + file.FileName);
                        foreach (ComponentBounds component in file.Components)
                        {
                            Debug.WriteLine("      Component Name = " + component.NameLabel);
                            if (component.Overlays != null)
                            {
                                foreach (Overlay overlay in component.Overlays)
                                {
                                    Debug.WriteLine("        Overlay X=" + overlay.Bounds.X + ", Y=" + overlay.Bounds.Y + ", Width=" + overlay.Bounds.Width + ", Height=" + overlay.Bounds.Height);
                                }
                            }
                        }
                    }
                }
            }
            */

            // Create the array that will be used for all overlays
            CreateOverlayArrays();

            // Initialize the two parts in the "Main" tab, the zoomable area and the image list
            InitializeTabMain();
            InitializeList();

            // Optimize drawing
            panelZoom.DoubleBuffered(true);
            panelListMain.DoubleBuffered(true);
            panelListAutoscroll.DoubleBuffered(true);

            InitializeComponentList();

            this.ResizeBegin += new EventHandler(this.Form_ResizeBegin);
            this.ResizeEnd += new EventHandler(this.Form_ResizeEnd);
            this.Shown += new System.EventHandler(this.Main_Shown);
        }


        // ---------------------------------------------------------------------------------


        private void Form_ResizeBegin(object sender, EventArgs e)
        {
            Debug.WriteLine("Form_ResizeBegin");
            isResizing = true;
            //visiblePictureBoxes.Clear();
            //FindPictureBoxes(this);
        }


        // ---------------------------------------------------------------------------------


        private void Form_ResizeEnd(object sender, EventArgs e)
        {
            Debug.WriteLine("Form_ResizeEnd");
//            foreach (PictureBox pictureBox in visiblePictureBoxes)
//            {
//                pictureBox.Visible = true;
//            }
//            visiblePictureBoxes.Clear();
            isResizing = false;

            // Highlight (relevant) overlays
            HighlightOverlays("tab");

        }


        // ---------------------------------------------------------------------------------


        private void FindPictureBoxes(Control container)
        {
            foreach (Control control in container.Controls)
            {
                if (control is PictureBox && control.Visible)
                {
                    visiblePictureBoxes.Add((PictureBox)control);
                    control.Visible = false;
                }
                else if (control.HasChildren)
                {
                    FindPictureBoxes(control);
                }
            }
        }
        
        
        // ---------------------------------------------------------------------------------


        private void InitializeComponentList()
        {

            // Search for the correct Hardware and Board
            Hardware foundHardware = classHardware.FirstOrDefault(h => h.Name == "Commodore 64 (Breadbin)");
            if (foundHardware != null)
            {
                Board foundBoard = foundHardware.Boards.FirstOrDefault(b => b.Name == "250425");
                if (foundBoard != null)
                {
                    foreach (ComponentBoard component in foundBoard.Components)
                    {
                        
                        string displayText = component.NameLabel + "   6526   Resistor";
                        string actualValue = component.NameLabel;
                        listBox1.Items.Add(displayText);
                        listBoxNameValueMapping[displayText] = actualValue;
                    }
                }
            }
        }


        // ---------------------------------------------------------------------------------


        private void InitializeTabMain()
        {

            // Initialize main panel, make it part of the "tabMain" and fill the entire size
            panelMain = new CustomPanel
            {
                Size = new Size(panelZoom.Width - panelListMain.Width - 25, panelZoom.Height),
                AutoScroll = true,
                Dock = DockStyle.Fill,
            };
            panelMain.DoubleBuffered(true);
            panelZoom.Controls.Add(panelMain);

            // Calculate the new size of the imagePanel.
//            Size newSize = new Size((int)(image.Width * zoomFactor), (int)(image.Height * zoomFactor));

            // Initialize zoomable image panel
            panelImage = new Panel
            {
                Size = image.Size,
                //Size = newSize,
                BackgroundImage = image,
                BackgroundImageLayout = ImageLayout.Zoom,
                Dock = DockStyle.None,
            };
            panelImage.DoubleBuffered(true);
            panelMain.Controls.Add(panelImage);

            // Create all overlays defined in the array
            foreach (PictureBox overlayTab in overlayComponentsTab)
            {
                overlayTab.DoubleBuffered(true); 
                panelImage.Controls.Add(overlayTab);
                Debug.WriteLine("Attached PictureBox in ZOOM ["+ imageSelected +"] with hash [" + overlayTab.GetHashCode() +"]");

                // Trigger on events
                overlayTab.MouseDown += PanelImage_MouseDown;
                overlayTab.MouseUp += PanelImage_MouseUp;
                overlayTab.MouseMove += PanelImage_MouseMove;
                overlayTab.MouseEnter += new EventHandler(this.Overlay_MouseEnter);
                overlayTab.MouseLeave += new EventHandler(this.Overlay_MouseLeave);
            }

            ResizeTabImage();

            // Attach event handlers for mouse events and form shown
            panelMain.CustomMouseWheel += new MouseEventHandler(PanelMain_MouseWheel);
            panelImage.MouseDown += PanelImage_MouseDown;
            panelImage.MouseUp += PanelImage_MouseUp;
            panelImage.MouseMove += PanelImage_MouseMove;
            panelMain.Resize += new EventHandler(this.PanelMain_Resize);
        }


        // ---------------------------------------------------------------------------------


        private void InitializeList()
        {
            int yPosition = 0;

            foreach (Hardware hardware in classHardware)
            {
                if (hardware.Name == hardwareSelected)
                {
                    foreach (Board board in hardware.Boards)
                    {
                        if (board.Name == boardSelected)
                        {
                            foreach (Commodore_Repair_Toolbox.File file in board.Files)
                            {
                                Panel panelList2;
                                Label labelList1;
                                Image image2 = Image.FromFile(Application.StartupPath + "\\Data\\Commodore 64 Breadbin\\250425\\" + file.FileName);

                                // Initialize image panel
                                panelList2 = new Panel
                                {
                                    Size = image2.Size,
                                    Location = new Point(0, yPosition),
                                    BackgroundImage = image2,
                                    BackgroundImageLayout = ImageLayout.Zoom,
                                    Dock = DockStyle.None,
                                };
                                panelList2.DoubleBuffered(true);
                                panelListAutoscroll.Controls.Add(panelList2);

                                // Add the Paint event handler to draw the border
                                if(imageSelected == file.Name)
                                {
                                    panelList2.Paint += new PaintEventHandler((sender, e) =>
                                    {
                                        float penWidth = 1;
                                        using (Pen pen = new Pen(Color.Red, penWidth))
                                        {
                                            pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                                            pen.DashPattern = new float[] { 4, 2 };
                                            float halfPenWidth = penWidth / 2;
                                            e.Graphics.DrawRectangle(pen, halfPenWidth, halfPenWidth, panelList2.Width - penWidth, panelList2.Height - penWidth);
                                        }
                                    });
                                }

                                labelList1 = new Label
                                {
                                    Text = file.Name,
                                    Location = new Point(0, 0),
                                    BorderStyle = BorderStyle.FixedSingle,
                                    AutoSize = true,
                                    BackColor = Color.White,
                                    Padding = new Padding(left: 2, top: 2, right: 2, bottom: 2),
                                };
                                //panelList2.Controls.Add(labelList1);
                                labelList1.DoubleBuffered(true);
                                labelList1.Parent = panelList2;

                                // Set the zoom factor for the size of the panel
                                float xZoomFactor = (float)panelListAutoscroll.Width / image2.Width;
                                float yZoomFactor = (float)panelListAutoscroll.Height / image2.Height;
                                float zoomFactor = Math.Min(xZoomFactor, yZoomFactor);

                                // Update the image based on the zoom factor
                                panelList2.Size = new Size((int)(image2.Width * zoomFactor), (int)(image2.Height * zoomFactor));

                                //                              this.SuspendLayout();




                                // Create all overlays defined in the array
                                foreach (PictureBox overlayList in overlayComponentsList[file.Name])
                                {
                                    if(overlayList.Tag == file.Name)
                                    {
                                        
                                        int newWidth = (int)(overlayList.Width * zoomFactor);
                                        int newHeight = (int)(overlayList.Height * zoomFactor);

                                        if (newWidth > 0 && newHeight > 0)
                                        {
                                            overlayList.Size = new Size(newWidth, newHeight);
                                            overlayList.Location = new Point((int)(overlayList.Location.X * zoomFactor), (int)(overlayList.Location.Y * zoomFactor));

                                            // Dispose the overlay transparent bitmap and create a new one (bitmaps cannot be resized)
                                            if (overlayList.Image != null)
                                            {
                                                overlayList.Image.Dispose();
                                            }
                                            Bitmap newBmp = new Bitmap(newWidth, newHeight);
                                            using (Graphics g = Graphics.FromImage(newBmp))
                                            {
                                                g.Clear(Color.FromArgb(128, Color.Red)); // 50% opacity
                                            }
                                            overlayList.Image = newBmp;
                                        
                                            overlayList.DoubleBuffered(true);
                                            panelList2.Controls.Add(overlayList);
                                            Debug.WriteLine("Attached PictureBox in LIST ["+ file.Name + "] with hash [" + overlayList.GetHashCode() +"]");

                                        }
                                        
                                    }
                                }


                                //                                this.ResumeLayout();

                                yPosition += panelList2.Height + 10;
                            }
                        }
                    }
                }
            }

            // Highlight (relevant) overlays
            HighlightOverlays("list");

        }


        // ---------------------------------------------------------------------------------


        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            listBoxSelectedActualValues.Clear();
            
            // Loop through the SelectedItems collection of the ListBox
            foreach (var selectedItem in listBox1.SelectedItems)
            {
                string selectedDisplayText = selectedItem.ToString();
                if (listBoxNameValueMapping.ContainsKey(selectedDisplayText))
                {
                    string selectedActualValue = listBoxNameValueMapping[selectedDisplayText];
                    listBoxSelectedActualValues.Add(selectedActualValue);
                }
            }

            // Highlight (relevant) overlays
            HighlightOverlays("tab");
            HighlightOverlays("list");
        }


        // ---------------------------------------------------------------------------------
        //
        // HighlightOverlays
        //
        // Write something here ...
        // ---------------------------------------------------------------------------------

        private void HighlightOverlays (string scope)
        {
            if (!isResizing)
            {

                // Tab
                if(scope == "tab")
                {

                    foreach (var pb in overlayComponentsTab)
                    {

                        if (listBoxSelectedActualValues.Contains(pb.Name))
                        {
                            Debug.WriteLine("Toggling PictureBox in ZOOM ["+ imageSelected +"] to [Visible] with hash [" + pb.GetHashCode() + "]");
                            pb.Visible = true;
                        }
                        else
                        {
                            Debug.WriteLine("Toggling PictureBox in ZOOM ["+ imageSelected +"] to [Hide] with hash [" + pb.GetHashCode() + "]");
                            pb.Visible = false;
                            //pb.BackColor = Color.Blue;
                        }
                    }

                }

                // List
                if(scope == "list")
                {
                    foreach (var pb in overlayComponentsList)
                    {
                        foreach (PictureBox pb2 in overlayComponentsList[pb.Key])
                        {

                            if (listBoxSelectedActualValues.Contains(pb2.Name))
                            {
                                Debug.WriteLine("Toggling PictureBox in LIST [" + pb.Key + "] to [Visible] with hash [" + pb2.GetHashCode() +"]");
                                pb2.Visible = true;
                            }
                            else
                            {
                                Debug.WriteLine("Toggling PictureBox in LIST [" + pb.Key + "] to [Hide] with hash [" + pb2.GetHashCode() +"]");
                                pb2.Visible = false;
                            }
                        }
                    }
                }
            }
        }


        // ---------------------------------------------------------------------------------
        //
        // CreateOverlayArrays
        //
        // Create a PictureBox per image and component in both the main zoomable image
        // and all the list images. This is the PictureBox only but it will not be
        // associated to any concrete object/image yet
        // ---------------------------------------------------------------------------------

        private void CreateOverlayArrays ()
        {

            // Walk through the class object and find the specific selected
            // hardware and board
            Hardware foundHardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelected);
            if (foundHardware != null)
            {
                Board foundBoard = foundHardware.Boards.FirstOrDefault(b => b.Name == boardSelected);
                if (foundBoard != null)
                {

                    // Walk through all images for the specific board
                    foreach (Commodore_Repair_Toolbox.File file in foundBoard.Files)
                    {

                        // Walk through all the components, again for the specific board
                        foreach (ComponentBounds component in file.Components)
                        {
                            if (component.Overlays != null)
                            {

                                // Create a PictureBox per component
                                foreach (Overlay overlay in component.Overlays)
                                {

                                    PictureBox overlayPictureBox;

                                    // Tab
                                    // ---
                                    // "Tab" - only create a PictureBox if we are processing
                                    // the same image as the one shown in the zoomable
                                    // image
                                    if (file.Name == imageSelected)
                                    {

                                        // Define a new PictureBox
                                        overlayPictureBox = new PictureBox
                                        {
                                            Name = $"{component.NameLabel}",
                                            Location = new Point(overlay.Bounds.X, overlay.Bounds.Y),
                                            Size = new Size(overlay.Bounds.Width, overlay.Bounds.Height),
                                            Tag = file.Name,
                                        };

                                        // Add the overlay to the array
                                        overlayComponentsTab.Add(overlayPictureBox);
                                        Debug.WriteLine("Created PictureBox in ZOOM [" + file.Name + "] with hash [" + overlayPictureBox.GetHashCode() + "] - Overlay Name:" + component.NameLabel + ", X:" + overlay.Bounds.X + ", Y:" + overlay.Bounds.Y + ", Width:" + overlay.Bounds.Width + ", Height:" + overlay.Bounds.Height);
                                        int index = overlayComponentsTab.Count - 1;
                                        overlayComponentsTabOriginalSizes.Add(index, overlayPictureBox.Size);
                                        overlayComponentsTabOriginalLocations.Add(index, overlayPictureBox.Location);
                                    }

                                    // List
                                    // ----

                                    // Define a new PictureBox
                                    overlayPictureBox = new PictureBox
                                    {
                                        Name = $"{component.NameLabel}",
                                        Location = new Point(overlay.Bounds.X, overlay.Bounds.Y),
                                        Size = new Size(overlay.Bounds.Width, overlay.Bounds.Height),
                                        Tag = file.Name,
                                    };

                                    // Create main image within the array (if it does not exists)
                                    if (!overlayComponentsList.ContainsKey(file.Name))
                                    {
                                        List<PictureBox> overlaysForImage = new List<PictureBox>();
                                        overlayComponentsList[file.Name] = overlaysForImage;
                                    }

                                    // Add the overlay to the array
                                    overlayComponentsList[file.Name].Add(overlayPictureBox);
                                    Debug.WriteLine("Created PictureBox in LIST [" + file.Name + "] with hash [" + overlayPictureBox.GetHashCode() + "] - Overlay Name:"+ component.NameLabel + ", X:" + overlay.Bounds.X + ", Y:" + overlay.Bounds.Y + ", Width:" + overlay.Bounds.Width + ", Height:" + overlay.Bounds.Height);
                                }
                            }
                        }
                    }
                }
            }
        }


        // ---------------------------------------------------------------------------------


        private void Main_Shown(object sender, EventArgs e)
        {
            //panelMain.AutoScrollPosition = new Point(750, 400);
        }


        // ---------------------------------------------------------------------------------


        private void ResizeTabImage()
        {
            // Set the zoom factor
            float xZoomFactor = (float)panelMain.Width / image.Width;
            float yZoomFactor = (float)panelMain.Height / image.Height;
            zoomFactor = Math.Min(xZoomFactor, yZoomFactor);

            // Update the image size to the zoom factor
            panelImage.Size = new Size((int)(image.Width * zoomFactor), (int)(image.Height * zoomFactor));
            //panelImage.Size = new Size((int)(panelImage.Width * zoomFactor), (int)(panelImage.Height * zoomFactor));
            //panelImage.Refresh();

            // Resize the overlays
            int index = 0;
            foreach (PictureBox overlay in overlayComponentsTab)
            {
                // Dispose the current overlay
                if (overlay.Image != null)
                {
                    overlay.Image.Dispose();
                }

                Size originalSize = overlayComponentsTabOriginalSizes[index];
                Point originalLocation = overlayComponentsTabOriginalLocations[index];
                int newWidth = (int)(originalSize.Width * zoomFactor);
                int newHeight = (int)(originalSize.Height * zoomFactor);
                overlay.Size = new Size(newWidth, newHeight);
                overlay.Location = new Point((int)(originalLocation.X * zoomFactor), (int)(originalLocation.Y * zoomFactor));

                // Create a new bitmap
                Bitmap newBmp = new Bitmap(newWidth, newHeight);
                using (Graphics g = Graphics.FromImage(newBmp))
                {
                    g.Clear(Color.FromArgb(128, Color.Red)); // 50% opacity
                }
                overlay.Image = newBmp;
                overlay.DoubleBuffered(true);
                Debug.WriteLine("Attached PictureBox in ZOOM [" + imageSelected + "] with hash [" + overlay.GetHashCode() + "]");

                index++;
            }

            // Highlight (relevant) overlays            
            HighlightOverlays("tab");
        }


        // ---------------------------------------------------------------------------------


        private void PanelMain_Resize(object sender, EventArgs e)
        {
            if (!isResizedByMouseWheel)
            {
                ResizeTabImage();
            }

            isResizedByMouseWheel = false;
        }


        // ---------------------------------------------------------------------------------


        private void PanelMain_MouseWheel(object sender, MouseEventArgs e)
        {
            Debug.WriteLine("MouseWheel event");
            float oldZoomFactor = zoomFactor;

            Debug.WriteLine("Before: panelMain.Width=" + panelMain.Width + ", panelImage.Width=" + panelImage.Width + ", image.Width=" + image.Width + ", panelMain.AutoScrollPosition.X=" + panelMain.AutoScrollPosition.X);

            Debug.WriteLine("zoomFactor=" + zoomFactor);

            // Change the zoom factor based on the mouse wheel movement.
            bool hasZoomChanged = false;
            if (e.Delta > 0)
            {
                if (zoomFactor <= 5)
                {
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
//                panelImage.Refresh();

                HighlightOverlays("tab");

                // Update the scroll position of the containerPanel.
                panelMain.AutoScrollPosition = new Point(newScrollPosition.X - e.X, newScrollPosition.Y - e.Y);

                Debug.WriteLine("After: panelMain.Width=" + panelMain.Width + ", panelImage.Width=" + panelImage.Width + ", image.Width=" + image.Width + ", panelMain.AutoScrollPosition.X=" + panelMain.AutoScrollPosition.X);

            }
        }


        // ---------------------------------------------------------------------------------


        private void PanelImage_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                Debug.WriteLine("MouseDown event");
                lastMousePosition = e.Location;
            }
        }


        // ---------------------------------------------------------------------------------


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


        // ---------------------------------------------------------------------------------


        private void PanelImage_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                Debug.WriteLine("MouseUp event");
                lastMousePosition = Point.Empty;
            }
        }


        // ---------------------------------------------------------------------------------


        private void Overlay_MouseEnter(object sender, EventArgs e)
        {
            this.Cursor = Cursors.Hand;
            Control control = sender as Control;
            if (control != null)
            {
                label3.Text = control.Name;
            }
            label3.Visible = true;
        }


        // ---------------------------------------------------------------------------------


        private void Overlay_MouseLeave(object sender, EventArgs e)
        {
            this.Cursor = Cursors.Default;
            label3.Visible = false;
        }

        private void button1_Click(object sender, EventArgs e)
        {
        }
    }


    // #################################################################################
    // Class definitions


    public class CustomPanel : Panel
    {
        public event MouseEventHandler CustomMouseWheel;

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            CustomMouseWheel?.Invoke(this, e);
        }
    }


    public class Hardware
    {
        public string Name { get; set; }
        public string Folder { get; set; }
        public List<Board> Boards { get; set; }
    }


    public class Board
    {
        public string Name { get; set; }
        public string Folder { get; set; }
        public List<File> Files { get; set; }
        public List<ComponentBoard> Components { get; set; }
    }


    public class File
    {
        public string Name { get; set; }
        public string FileName { get; set; }
        public string HighlightColorTab { get; set; }
        public string HighlightColorList { get; set; }
        public string HighlightOpacityTab { get; set; }
        public string HighlightOpacityList { get; set; }
        public List<ComponentBounds> Components { get; set; }
    }


    public class ComponentBoard
    {
        public string NameLabel { get; set; }
        public string NameTechnical { get; set; }
        public string NameFriendly { get; set; }
    }


    public class ComponentBounds
    {
        public string NameLabel { get; set; }
        public List<Overlay> Overlays { get; set; }
    }


    public class Overlay
    {
        public Rectangle Bounds { get; set; }
    }


    // #################################################################################


}