using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Commodore_Retro_Toolbox;
using System.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.CodeDom;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Reflection.Emit;
using OfficeOpenXml.Style;

namespace Commodore_Repair_Toolbox
{


    // #################################################################################


    public partial class Main : Form
    {

        private bool isResizedByMouseWheel = false;
        private bool isResizing = false;
        private CustomPanel panelMain;
        private List<string> selectedItems = new List<string>();
        private Dictionary<int, Point> overlayComponentsTabOriginalLocations = new Dictionary<int, Point>();
        private Dictionary<int, Size> overlayComponentsTabOriginalSizes = new Dictionary<int, Size>();
        private Dictionary<string, List<PictureBox>> overlayComponentsList = new Dictionary<string, List<PictureBox>>();
        private Dictionary<string, string> listBoxNameValueMapping = new Dictionary<string, string>();
        private float zoomFactor = 1.0f;
        private Image image;
        public List<Hardware> classHardware = new List<Hardware>();
        private List<PictureBox> overlayComponentsTab = new List<PictureBox>();
        private List<PictureBox> visiblePictureBoxes = new List<PictureBox>();
        private List<string> listBoxSelectedActualValues = new List<string>();
        private Panel panelImage;
        private Point lastMousePosition;
        public string hardwareSelectedName;
        private string hardwareSelectedFolder;
        private string boardSelectedName;
        private string boardSelectedFolder;
        private string imageSelectedName;
        private string imageSelectedFile;


        

        // ---------------------------------------------------------------------------------

        public Main()
        {
            // Initialize the UI form
            InitializeComponent();

        

            // Bind some needed events
            this.ResizeBegin += new EventHandler(this.Form_ResizeBegin);
            this.ResizeEnd += new EventHandler(this.Form_ResizeEnd);
            this.Shown += new System.EventHandler(this.Main_Shown);

            // Optimize drawing performance
            panelZoom.DoubleBuffered(true);
            panelListMain.DoubleBuffered(true);
            panelListAutoscroll.DoubleBuffered(true);

            // Read all data from Excel and JSON files once only
            DataStructure.GetAllData(classHardware);

            // Debug output to show if we have the correct data
            foreach (Hardware hardware in classHardware)
            {
                Debug.WriteLine("[" + hardware.Name + "]");
                Debug.WriteLine("  [Folder]=[" + hardware.Folder + "]");
                foreach (Board board in hardware.Boards)
                {
                    Debug.WriteLine("  [" + board.Name + "]");
                    Debug.WriteLine("    [Folder]=[" + board.Folder + "]");
                    Debug.WriteLine("    [Component]");
                    foreach (ComponentBoard component in board.Components)
                    {
                        Debug.WriteLine("      [" + component.Label + "] ("+ component.Type +")");
                    }
                    foreach (Commodore_Repair_Toolbox.File file in board.Files)
                    {
                        Debug.WriteLine("    [" + file.Name + "]");
                        Debug.WriteLine("      [FileName]=[" + file.FileName + "]");
                        Debug.WriteLine("      [Component]");
                        foreach (ComponentBounds component in file.Components)
                        {
                            Debug.WriteLine("        [" + component.Label + "]");
                            if (component.Overlays != null)
                            {
                                foreach (Overlay overlay in component.Overlays)
                                {
                                    Debug.WriteLine("          [X]=[" + overlay.Bounds.X + "], [Y]=[" + overlay.Bounds.Y + "], [Width]=[" + overlay.Bounds.Width + "], [Height]=[" + overlay.Bounds.Height + "]");
                                }
                            }
                        }
                    }
                }
            }

            // Populate the "hardware" and "board" combobox'es and set variables for selected hardware/board
            foreach (Hardware hardware in classHardware)
            {
                comboBox1.Items.Add(hardware.Name);
            }
            comboBox1.SelectedIndex = 0;
            hardwareSelectedName = comboBox1.SelectedItem.ToString();

            //            foreach (Board board in classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName).Boards)
            //            {
            //                comboBox2.Items.Add(board.Name);
            //            }
            //            comboBox2.SelectedIndex = 0;
        }


        // ---------------------------------------------------------------------------------



        private void SetupNewBoard()
        {
            boardSelectedName = comboBox2.SelectedItem.ToString();
            var selectedHardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var selectedBoard = selectedHardware.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            hardwareSelectedFolder = selectedHardware.Folder;
            boardSelectedFolder = selectedHardware.Boards.FirstOrDefault(b => b.Name == boardSelectedName).Folder;
            imageSelectedName = selectedBoard.Files.FirstOrDefault().Name;
            imageSelectedFile = selectedBoard.Files.FirstOrDefault(f => f.Name == imageSelectedName).FileName;

            // Show the components in the list for this specific board
            InitializeComponentCategories();
            InitializeComponentList();

            InitializeList();
            InitializeTabMain();
        }

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


        private void InitializeComponentCategories()
        {

            listBox2.Items.Clear();

            // Search for the correct Hardware and Board
            Hardware foundHardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            if (foundHardware != null)
            {
                Board foundBoard = foundHardware.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
                if (foundBoard != null)
                {
                    foreach (ComponentBoard component in foundBoard.Components)
                    {
                        if (!string.IsNullOrEmpty(component.Type) && !listBox2.Items.Contains(component.Type)) { 
                            listBox2.Items.Add(component.Type);
                        }
                    }
                }
            }

            for (int i = 0; i < listBox2.Items.Count; i++)
            {
                listBox2.SetSelected(i, true);
            }
        }

        private void InitializeComponentList(bool clearList = true)
        {

            foreach (var item in listBox1.SelectedItems)
            {
                selectedItems.Add(listBoxNameValueMapping[item.ToString()]);
            }

            listBox1.Items.Clear();
            listBoxNameValueMapping.Clear();
            if (clearList)
            {
                
            }

            // Search for the correct Hardware and Board
            Hardware foundHardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            if (foundHardware != null)
            {
                Board foundBoard = foundHardware.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
                if (foundBoard != null)
                {
                    foreach (ComponentBoard component in foundBoard.Components)
                    {
                        if (listBox2.SelectedItems.Contains(component.Type))
                        {
                            string displayText = component.Label + "   "+ component.NameTechnical + "   "+ component.NameFriendly;
                            string actualValue = component.Label;
                            listBox1.Items.Add(displayText);
                            listBoxNameValueMapping[displayText] = actualValue;

                            // Check if the component.Label is in the selectedItems list
                            if (selectedItems.Contains(component.Label))
                            {
                                int index = listBox1.Items.IndexOf(displayText);
                                listBox1.SetSelected(index, true);
                            }
                        }
                    }
                }
            }

            // Remove the selection, if the component is no longer visible in the component list.
            // This could have been done in one "foreach" loop, but it is bad practise to modify directly in the list you are iterating
            List<string> itemsToRemove = new List<string>();
            foreach (string selectedItem in selectedItems)
            {
                bool isVisible = listBoxNameValueMapping.Values.Contains(selectedItem);
                if (!isVisible)
                {
                    itemsToRemove.Add(selectedItem);
                }
            }
            foreach (string itemToRemove in itemsToRemove)
            {
                selectedItems.Remove(itemToRemove);
            }

        }


        // ---------------------------------------------------------------------------------


        private void InitializeTabMain()
        {
            // Get the image for the main tab
            image = Image.FromFile(Application.StartupPath + "\\Data\\" + hardwareSelectedFolder + "\\" + boardSelectedFolder + "\\" + imageSelectedFile);

            // Reset existing main tab (if already set)
            panelZoom.Controls.Clear();

            overlayComponentsTab.Clear();
            overlayComponentsTabOriginalSizes.Clear();
            overlayComponentsTabOriginalLocations.Clear();

            CreateOverlayArraysToTab();

            // Initialize main panel, make it part of the "tabMain" and fill the entire size
            panelMain = new CustomPanel
            {
                Size = new Size(panelZoom.Width - panelListMain.Width - 25, panelZoom.Height),
                AutoScroll = true,
                Dock = DockStyle.Fill,
            };
            panelMain.DoubleBuffered(true);
            panelZoom.Controls.Add(panelMain);

            // Initialize zoomable image panel
            panelImage = new Panel
            {
                Size = image.Size,
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
                Debug.WriteLine("Attached PictureBox in ZOOM [" + imageSelectedName + "] with hash [" + overlayTab.GetHashCode() + "]");

                // Trigger on events
                overlayTab.MouseDown += PanelImage_MouseDown;
                overlayTab.MouseUp += PanelImage_MouseUp;
                overlayTab.MouseMove += PanelImage_MouseMove;
                overlayTab.MouseEnter += new EventHandler(this.Overlay_MouseEnter);
                overlayTab.MouseLeave += new EventHandler(this.Overlay_MouseLeave);
                overlayTab.MouseClick += new MouseEventHandler(this.PanelImageComponent_MouseClick);
            }

            
            System.Windows.Forms.Label labelFile = new System.Windows.Forms.Label
            {
                Text = imageSelectedName,
                BackColor = Color.White,
                ForeColor = Color.Black,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Arial", 9),
                Location = new Point(5, 5),
                AutoSize = true,
                Name = "labelFile",
            };
            labelFile.DoubleBuffered(true);
            panelMain.Controls.Add(labelFile);
            labelFile.BringToFront();

            System.Windows.Forms.Label labelComponent = new System.Windows.Forms.Label
            {
//                Text = "Hest",
                BackColor = Color.Red,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Arial", 9),
                Location = new Point(5, 25),
                AutoSize = true,
                Visible = false,
                Name = "labelComponent",
            };
            labelComponent.DoubleBuffered(true);
            panelMain.Controls.Add(labelComponent);
            labelComponent.BringToFront();

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

            CreateOverlayArraysToList();

            panelListAutoscroll.Controls.Clear();

            int yPosition = 0;

            foreach (Hardware hardware in classHardware)
            {
                if (hardware.Name == hardwareSelectedName)
                {
                    foreach (Board board in hardware.Boards)
                    {
                        if (board.Name == boardSelectedName)
                        {
                            foreach (Commodore_Repair_Toolbox.File file in board.Files)
                            {
                                Panel panelList2;
                                System.Windows.Forms.Label labelList1;
                                Image image2 = Image.FromFile(Application.StartupPath + "\\Data\\" + hardwareSelectedFolder + "\\" + boardSelectedFolder + "\\" + file.FileName);

                                // Initialize image panel
                                panelList2 = new Panel
                                {
                                    Size = image2.Size,
                                    Location = new Point(0, yPosition),
                                    BackgroundImage = image2,
                                    BackgroundImageLayout = ImageLayout.Zoom,
                                    Dock = DockStyle.None,
                                    Name = file.Name,
                                };
                                panelList2.DoubleBuffered(true);
                                panelListAutoscroll.Controls.Add(panelList2);

                                panelList2.MouseEnter += new EventHandler(this.PanelList2_MouseEnter);
                                panelList2.MouseLeave += new EventHandler(this.PanelList2_MouseLeave);
                                panelList2.MouseClick += new MouseEventHandler(this.PanelList2_MouseClick);

                                labelList1 = new System.Windows.Forms.Label
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
                                    if (overlayList.Tag == file.Name)
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
                                            Debug.WriteLine("Attached PictureBox in LIST [" + file.Name + "] with hash [" + overlayList.GetHashCode() + "]");

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

            DrawBorderInList();

            // Highlight (relevant) overlays
            HighlightOverlays("list");
        }


        private void DrawBorderInList()
        {

            foreach (Panel panel in panelListAutoscroll.Controls.OfType<Panel>())
            {
                // Remove existing Paint event handlers to avoid duplication
                panel.Paint -= Panel_Paint_Standard;
                panel.Paint -= Panel_Paint_Special;

                if (panel.Name == imageSelectedName)
                {
                    // Special border for selected panel
                    panel.Paint += Panel_Paint_Special;
                }
                else
                {
                    // Standard border for all other panels
                    panel.Paint += Panel_Paint_Standard;
                }

                panel.Invalidate();  // Force the panel to repaint
            }
        }

        private void Panel_Paint_Standard(object sender, PaintEventArgs e)
        {
            // Draw standard border
            float penWidth = 1;
            using (Pen pen = new Pen(Color.Black, penWidth))
            {
                float halfPenWidth = penWidth / 2;
                e.Graphics.DrawRectangle(pen, halfPenWidth, halfPenWidth, ((Panel)sender).Width - penWidth, ((Panel)sender).Height - penWidth);
            }
        }

        private void Panel_Paint_Special(object sender, PaintEventArgs e)
        {
            // Draw special border
            float penWidth = 1;
            using (Pen pen = new Pen(Color.Red, penWidth))
            {
                pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                pen.DashPattern = new float[] { 4, 2 };
                float halfPenWidth = penWidth / 2;
                e.Graphics.DrawRectangle(pen, halfPenWidth, halfPenWidth, ((Panel)sender).Width - penWidth, ((Panel)sender).Height - penWidth);
            }
        }

        // ---------------------------------------------------------------------------------


        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateHighlights();
        }

        private void UpdateHighlights()
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
            // Skip execution if the form is minimized (size would be 0)
            if (this.WindowState == FormWindowState.Minimized)
            {
                return; 
            }

            if (!isResizing)
            {

                // Tab
                if(scope == "tab")
                {


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
                            if (listBoxSelectedActualValues.Contains(overlay.Name))
                            {
                                g.Clear(Color.FromArgb(128, Color.Blue)); // 50% opacity
                            } else
                            {
                                g.Clear(Color.FromArgb(0, Color.Transparent)); // 0% opacity
                            }
                                
                        }
                        overlay.Image = newBmp;
                        overlay.DoubleBuffered(true);
                        Debug.WriteLine("Attached PictureBox in ZOOM [" + imageSelectedName + "] with hash [" + overlay.GetHashCode() + "]");

                        index++;
                    }

                    /*
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
                    */

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
        // CreateOverlayArraysToList
        //
        // Create a PictureBox per image and component in both the main zoomable image
        // and all the list images. This is the PictureBox only but it will not be
        // associated to any concrete object/image yet
        // ---------------------------------------------------------------------------------

        private void CreateOverlayArraysToList ()
        {

            // Walk through the class object and find the specific selected hardware and board
            Hardware foundHardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            if (foundHardware != null)
            {
                Board foundBoard = foundHardware.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
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

                                    // List
                                    // ----

                                    // Define a new PictureBox
                                    overlayPictureBox = new PictureBox
                                    {
                                        Name = $"{component.Label}",
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
                                    Debug.WriteLine("Created PictureBox in LIST [" + file.Name + "] with hash [" + overlayPictureBox.GetHashCode() + "] - Overlay Name:"+ component.Label + ", X:" + overlay.Bounds.X + ", Y:" + overlay.Bounds.Y + ", Width:" + overlay.Bounds.Width + ", Height:" + overlay.Bounds.Height);
                                }
                            }
                        }
                    }
                }
            }
        }


        // ---------------------------------------------------------------------------------
        //
        // CreateOverlayArraysToTab
        //
        // Create a PictureBox per image and component in both the main zoomable image
        // and all the list images. This is the PictureBox only but it will not be
        // associated to any concrete object/image yet
        // ---------------------------------------------------------------------------------

        private void CreateOverlayArraysToTab()
        {

            // Walk through the class object and find the specific selected hardware and board
            Hardware foundHardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            if (foundHardware != null)
            {
                Board foundBoard = foundHardware.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
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
                                    if (file.Name == imageSelectedName)
                                    {
                                        // Define a new PictureBox
                                        overlayPictureBox = new PictureBox
                                        {
                                            Name = $"{component.Label}",
                                            Location = new Point(overlay.Bounds.X, overlay.Bounds.Y),
                                            Size = new Size(overlay.Bounds.Width, overlay.Bounds.Height),
                                            Tag = file.Name,
                                        };

                                        // Add the overlay to the array
                                        overlayComponentsTab.Add(overlayPictureBox);
                                        Debug.WriteLine("Created PictureBox in ZOOM [" + file.Name + "] with hash [" + overlayPictureBox.GetHashCode() + "] - Overlay Name:" + component.Label + ", X:" + overlay.Bounds.X + ", Y:" + overlay.Bounds.Y + ", Width:" + overlay.Bounds.Width + ", Height:" + overlay.Bounds.Height);
                                        int index = overlayComponentsTab.Count - 1;
                                        overlayComponentsTabOriginalSizes.Add(index, overlayPictureBox.Size);
                                        overlayComponentsTabOriginalLocations.Add(index, overlayPictureBox.Location);
                                    }
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




            /*
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
            */

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
                if (zoomFactor <= 4.0)
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

                // Update the scroll position of the containerPanel.
                panelMain.AutoScrollPosition = new Point(newScrollPosition.X - e.X, newScrollPosition.Y - e.Y);



                /*
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

                    // Dispose of the old bitmap
                    if (overlay.Image != null)
                    {
                        overlay.Image.Dispose();
                    }

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
                */

                HighlightOverlays("tab");



                /*
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
                */

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

        private void PanelImageComponent_MouseClick(object sender, MouseEventArgs e)
        {

            if (e.Button == MouseButtons.Left)
            {

                // Cast "sender" as a PictureBox and create an instance of it
                if (sender is PictureBox pb)
                {

                    var hardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
                    var board = hardware.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
                    var components = board.Components.FirstOrDefault(c => c.Label == pb.Name);

                    Debug.WriteLine(pb.Name);
                    FormComponent formComponent = new FormComponent(components);
                    formComponent.ShowDialog();
                }
            }

            if (e.Button == MouseButtons.Right)
            {

                // Cast "sender" as a PictureBox and create an instance of it
                if (sender is PictureBox pb)
                {
                    Debug.WriteLine(pb.Name);

                    string key = listBoxNameValueMapping.FirstOrDefault(x => x.Value == pb.Name).Key;
                    int index = listBox1.FindString(key);
                    listBox1.SetSelected(index, !listBox1.GetSelected(index));
                }
            }
        }


        // ---------------------------------------------------------------------------------


        private void Overlay_MouseEnter(object sender, EventArgs e)
        {
            this.Cursor = Cursors.Hand;
            Control control = sender as Control;
            System.Windows.Forms.Label label;
            if (control != null)
            {
//                label3.Text = control.Name;
                label = (System.Windows.Forms.Label)this.Controls.Find("labelComponent", true).FirstOrDefault();
                label.Text = control.Name;
            }
//            Label label3.Visible = true;
            label = (System.Windows.Forms.Label)this.Controls.Find("labelComponent", true).FirstOrDefault();
            label.Visible = true;
        }

        private void PanelList2_MouseEnter(object sender, EventArgs e)
        {
            this.Cursor = Cursors.Hand;
        }

        private void PanelList2_MouseClick(object sender, MouseEventArgs e)
        {
            if (sender is Panel pan)
            {
                

                if (e.Button == MouseButtons.Left)
                {
                    Debug.WriteLine(pan.Name);

                    imageSelectedName = pan.Name;

                    foreach (Hardware hardware in classHardware)
                    {
                        if (hardware.Name == hardwareSelectedName)
                        {
                            Debug.WriteLine("Hardware Name = " + hardware.Name + ", Folder = " + hardware.Folder);
                            foreach (Board board in hardware.Boards)
                            {
                                if (board.Name == boardSelectedName)
                                {
                                    Debug.WriteLine("  Board Name = " + board.Name + ", Folder = " + board.Folder);
                                    foreach (Commodore_Repair_Toolbox.File file in board.Files)
                                    {
                                        if (file.Name == imageSelectedName)
                                        {
                                            Debug.WriteLine("    File Name = " + file.Name + ", FileName = " + file.FileName);
                                            imageSelectedFile = file.FileName;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }


                    //                    panelListAutoscroll.Controls.Clear();
                    //                    panelZoom.Controls.Clear();

                    //                    listBoxSelectedActualValues.Clear();
                    //                    listBoxNameValueMapping.Clear();
                    //                    overlayComponentsList.Clear();
                    //                    overlayComponentsTab.Clear();
                    //                    overlayComponentsTabOriginalSizes.Clear();
                    //                    overlayComponentsTabOriginalLocations.Clear();
                    //                    classHardware.Clear();
                    //                    visiblePictureBoxes.Clear();


                    DrawBorderInList();
                    InitializeTabMain();

                }
            }
        }

        // ---------------------------------------------------------------------------------


        private void Overlay_MouseLeave(object sender, EventArgs e)
        {
            this.Cursor = Cursors.Default;
            //label3.Visible = false;
            System.Windows.Forms.Label label = (System.Windows.Forms.Label)this.Controls.Find("labelComponent", true).FirstOrDefault();
            label.Visible = false;
        }

        private void PanelList2_MouseLeave(object sender, EventArgs e)
        {
            this.Cursor = Cursors.Default;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            listBox1.SelectedIndex = -1;
            UpdateHighlights();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetupNewBoard();

            // Update (remove) any highlights previously done, as no components are now selected in the list
            UpdateHighlights();
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            comboBox2.Items.Clear();
            hardwareSelectedName = comboBox1.SelectedItem.ToString();

            foreach (Board board in classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName).Boards)
            {
                comboBox2.Items.Add(board.Name);
            }
            comboBox2.SelectedIndex = 0;
        }

        private void listBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            InitializeComponentList(false);
            // Update (remove) any highlights previously done, as no components are now selected in the list
            UpdateHighlights();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < listBox1.Items.Count; i++)
            {
                listBox1.SetSelected(i, true);
            }
            UpdateHighlights();
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
        public string Datafile { get; set; }
        public List<Board> Boards { get; set; }
    }


    public class Board
    {
        public string Name { get; set; }
        public string Folder { get; set; }
        public string Datafile { get; set; }
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
        public string Label { get; set; }
        public string NameTechnical { get; set; }
        public string NameFriendly { get; set; }
        public string Type { get; set; }
        public string ImagePinout { get; set; }
        public string OneLiner { get; set; }
        public string Description { get; set; }
    }


    public class ComponentBounds
    {
        public string Label { get; set; }
        public List<Overlay> Overlays { get; set; }
    }


    public class Overlay
    {
        public Rectangle Bounds { get; set; }
    }


    // #################################################################################


}