using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Commodore_Retro_Toolbox;
using System.Linq;
using System.Runtime.InteropServices;
using System.Net.Http;

namespace Commodore_Repair_Toolbox
{




    // #################################################################################


    public partial class Main : Form
    {


        private Label labelFile;
        private Label labelComponent;

        private Dictionary<PictureBox, Bitmap> cachedHighlightImages = new Dictionary<PictureBox, Bitmap>();
        private Dictionary<PictureBox, Bitmap> cachedTransparentImages = new Dictionary<PictureBox, Bitmap>();

        private Dictionary<string, OverlayPanel> overlayPanelsList = new Dictionary<string, OverlayPanel>();
        private Dictionary<string, float> overlayListZoomFactors = new Dictionary<string, float>();


        private OverlayPanel overlayPanel;
        


                [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

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

            // Add this here:
            /*
                        this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                                      ControlStyles.OptimizedDoubleBuffer |
                                      ControlStyles.UserPaint, true);
                        this.UpdateStyles();
            */

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
                        Debug.WriteLine("      [" + component.Label + "] (" + component.Type + ")");
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

            NavigateWithPreCheck(initialUrl);

        }

        private string initialUrl = "https://commodore-repair-toolbox.dk/hest1";

        private async void NavigateWithPreCheck(string url)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        // Navigate with WebBrowser if the request is successful
                        webBrowser1.Navigate(url);
                    }
                    else
                    {
                        // Handle HTTP errors here
                        webBrowser1.DocumentText = $"Failed to load content: HTTP error. Status code: {response.StatusCode}, Reason: {response.ReasonPhrase}";
                    }
                }
                catch (HttpRequestException ex)
                {
                    // Handle network errors here
                    webBrowser1.DocumentText = $"Failed to load content: Network error. Exception: {ex.Message}";
                }
            }
        }


        private void webBrowser1_Navigating_1(object sender, WebBrowserNavigatingEventArgs e)
        {
            if (e.Url.ToString() != initialUrl)
            {
                e.Cancel = true;
                System.Diagnostics.Process.Start("explorer.exe", e.Url.ToString());
            }
        }



        private void Form_ResizeBegin(object sender, EventArgs e)
        {
            Debug.WriteLine("Form_ResizeBegin");
            isResizing = true;
        }

        private void webBrowser1_Navigated(object sender, WebBrowserNavigatedEventArgs e)
        {
        }

        private void webBrowser1_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if (webBrowser1.DocumentText.Contains("Hest"))
            {
                // Page loaded successfully; do nothing
            }
            else
            {
                webBrowser1.DocumentText = "Failed to load page.";
            }
        }






        // ---------------------------------------------------------------------------------


        private void Form_ResizeEnd(object sender, EventArgs e)
        {
            /*
            Debug.WriteLine("Form_ResizeEnd");
            isResizing = false;

            // Highlight (relevant) overlays
            HighlightOverlays("tab");
            */

            Debug.WriteLine("Form_ResizeEnd");
            isResizing = false;
            ResizeTabImage();
            HighlightOverlays("tab");
            HighlightOverlays("list");
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
                        if (!string.IsNullOrEmpty(component.Type) && !listBox2.Items.Contains(component.Type))
                        {
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
                            string displayText = component.Label + "   " + component.NameTechnical + "   " + component.NameFriendly;
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
            // Load the image
            image = Image.FromFile(
                Application.StartupPath + "\\Data\\" +
                hardwareSelectedFolder + "\\" +
                boardSelectedFolder + "\\" +
                imageSelectedFile);

            panelZoom.Controls.Clear();
            overlayComponentsTab.Clear();
            overlayComponentsTabOriginalSizes.Clear();
            overlayComponentsTabOriginalLocations.Clear();
            CreateOverlayArraysToTab();

            panelMain = new CustomPanel
            {
                Size = new Size(panelZoom.Width - panelListMain.Width - 25, panelZoom.Height),
                AutoScroll = true,
                Dock = DockStyle.Fill
            };
            panelMain.DoubleBuffered(true);
            panelZoom.Controls.Add(panelMain);

            panelImage = new Panel
            {
                Size = image.Size,
                BackgroundImage = image,
                BackgroundImageLayout = ImageLayout.Zoom,
                Dock = DockStyle.None
            };
            panelImage.DoubleBuffered(true);
            panelMain.Controls.Add(panelImage);

            // Create the overlay panel
            overlayPanel = new OverlayPanel
            {
                Bounds = panelImage.ClientRectangle
            };
            panelImage.Controls.Add(overlayPanel);
            overlayPanel.BringToFront();

            // Subscribe overlay events
            overlayPanel.OverlayClicked += OverlayPanel_OverlayClicked;
            overlayPanel.OverlayHoverChanged += OverlayPanel_OverlayHoverChanged;
            overlayPanel.OverlayPanelMouseDown += OverlayPanel_OverlayPanelMouseDown;
            overlayPanel.OverlayPanelMouseMove += OverlayPanel_OverlayPanelMouseMove;
            overlayPanel.OverlayPanelMouseUp += OverlayPanel_OverlayPanelMouseUp;

            // Create the file label (store in the class-level field 'labelFile')
            labelFile = new Label
            {
                Name = "labelFile",
                Text = imageSelectedName,
                AutoSize = true,
                BackColor = Color.White,
                ForeColor = Color.Black,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Arial", 9),
                Location = new Point(5, 5),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            labelFile.DoubleBuffered(true);
            panelZoom.Controls.Add(labelFile);
            labelFile.BringToFront();

            // Create the component label (store in the class-level field 'labelComponent')
            labelComponent = new Label
            {
                Name = "labelComponent",
                AutoSize = true,
                BackColor = Color.Red,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Arial", 9),
                Location = new Point(5, 25),
                Visible = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            labelComponent.DoubleBuffered(true);
            panelZoom.Controls.Add(labelComponent);
            labelComponent.BringToFront();

            // Now do the usual finishing steps
            ResizeTabImage();
            panelMain.CustomMouseWheel += PanelMain_MouseWheel;
            panelMain.Resize += PanelMain_Resize;
        }



        // ---------------------------------------------------------------------------------


        private void InitializeList()
        {
            // Clear old panels and dictionaries
            panelListAutoscroll.Controls.Clear();
            overlayPanelsList.Clear();
            overlayListZoomFactors.Clear();

            int yPosition = 0;
            var hardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var board = hardware?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (board == null) return;

            foreach (var file in board.Files)
            {
                // Load background image
                Image image2 = Image.FromFile(Application.StartupPath + "\\Data\\" +
                                              hardwareSelectedFolder + "\\" +
                                              boardSelectedFolder + "\\" +
                                              file.FileName);

                // Create panel for thumbnail
                Panel panelList2 = new Panel
                {
                    Name = file.Name,
                    BackgroundImage = image2,
                    BackgroundImageLayout = ImageLayout.Zoom,
                    Location = new Point(0, yPosition),
                    Dock = DockStyle.None
                };
                panelList2.DoubleBuffered(true);

                // Calculate local zoom factor for the thumbnail
                float xZoomFactor = (float)panelListAutoscroll.Width / image2.Width;
                float yZoomFactor = (float)panelListAutoscroll.Height / image2.Height;
                float zoomFactor = Math.Min(xZoomFactor, yZoomFactor);

                // Set the panel size
                panelList2.Size = new Size(
                    (int)(image2.Width * zoomFactor),
                    (int)(image2.Height * zoomFactor)
                );

                // Create overlay panel on top
                OverlayPanel overlayPanelList = new OverlayPanel
                {
                    Bounds = panelList2.ClientRectangle
                };
                panelList2.Controls.Add(overlayPanelList);
                overlayPanelList.BringToFront();

                // If user left-clicks empty space => select main image
                overlayPanelList.OverlayPanelMouseDown += (s, e2) =>
                {
                    if (e2.Button == MouseButtons.Left)
                    {
                        OnListImageLeftClicked(panelList2);
                    }
                };

                // Store these references so HighlightOverlays("list") can draw overlays
                overlayPanelsList[file.Name] = overlayPanelList;
                overlayListZoomFactors[file.Name] = zoomFactor;

                // Optional label showing the file name
                Label labelListFile = new Label
                {
                    Text = file.Name,
                    Location = new Point(0, 0),
                    BorderStyle = BorderStyle.FixedSingle,
                    AutoSize = true,
                    BackColor = Color.White,
                    Padding = new Padding(2),
                };
                labelListFile.DoubleBuffered(true);
                labelListFile.Parent = panelList2;
                labelListFile.BringToFront();

                // Add the panel to the scrollable container
                panelListAutoscroll.Controls.Add(panelList2);
                yPosition += panelList2.Height + 10;
            }

            DrawBorderInList();
            HighlightOverlays("list");
        }

        private void OnListImageLeftClicked(Panel pan)
        {
            // The panel's Name is file.Name
            imageSelectedName = pan.Name;
            Debug.WriteLine("User clicked thumbnail: " + imageSelectedName);

            // Find the actual file and set imageSelectedFile
            var hardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var board = hardware?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (board != null)
            {
                var file = board.Files.FirstOrDefault(f => f.Name == imageSelectedName);
                if (file != null)
                {
                    imageSelectedFile = file.FileName;
                    Debug.WriteLine("    File Name = " + file.Name + ", FileName = " + file.FileName);
                }
            }

            // Redraw border for newly selected thumbnail
            DrawBorderInList();

            // Re-initialize main display with new image
            InitializeTabMain();
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

        /*
        private void HighlightOverlays (string scope)
        {
            // Skip execution if the form is minimized (size would be 0)
            if (this.WindowState == FormWindowState.Minimized)
            {
                return; 
            }

            if (!isResizing)
            {

                List<Hardware> hardwareList = classHardware;
                var hardware = hardwareList.FirstOrDefault(h => h.Name == hardwareSelectedName);
                var board = hardware.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
                var file = board.Files.FirstOrDefault(f => f.Name == imageSelectedName);
                Color colorZoom = Color.FromName(file.HighlightColorTab);
                Color colorList = Color.FromName(file.HighlightColorList);
                int opacityZoom = file.HighlightOpacityTab;
                int opacityList = file.HighlightOpacityList;

                // Tab
                if (scope == "tab")
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
                                g.Clear(Color.FromArgb(opacityZoom, colorZoom)); // 50% opacity
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
*/
        private void HighlightOverlays(string scope)
        {
            if (this.WindowState == FormWindowState.Minimized || isResizing)
                return;

            if (scope == "tab")
            {
                // [Unchanged: build overlays for the main panel...]
                if (overlayPanel == null) return;
                var hardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
                var board = hardware?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
                var file = board?.Files.FirstOrDefault(f => f.Name == imageSelectedName);
                if (file == null) return;

                Color colorZoom = Color.FromName(file.HighlightColorTab);
                int opacityZoom = file.HighlightOpacityTab;

                overlayPanel.Overlays.Clear();
                for (int i = 0; i < overlayComponentsTab.Count; i++)
                {
                    Rectangle rect = new Rectangle(
                        (int)(overlayComponentsTabOriginalLocations[i].X * zoomFactor),
                        (int)(overlayComponentsTabOriginalLocations[i].Y * zoomFactor),
                        (int)(overlayComponentsTabOriginalSizes[i].Width * zoomFactor),
                        (int)(overlayComponentsTabOriginalSizes[i].Height * zoomFactor)
                    );

                    bool highlighted = listBoxSelectedActualValues.Contains(overlayComponentsTab[i].Name);
                    overlayPanel.Overlays.Add(new OverlayInfo
                    {
                        Bounds = rect,
                        Color = colorZoom,
                        Opacity = opacityZoom,
                        Highlighted = highlighted,
                        ComponentLabel = overlayComponentsTab[i].Name
                    });
                }
                overlayPanel.Invalidate();
            }
            else if (scope == "list")
            {
                // Build overlays for each file's overlay panel
                var hardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
                var board = hardware?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
                if (board == null) return;

                foreach (var file in board.Files)
                {
                    // Check if we have an OverlayPanel for this file
                    if (!overlayPanelsList.ContainsKey(file.Name)) continue;

                    OverlayPanel listPanel = overlayPanelsList[file.Name];
                    listPanel.Overlays.Clear();

                    Color colorList = Color.FromName(file.HighlightColorList);
                    int opacityList = file.HighlightOpacityList;
                    float listZoom = overlayListZoomFactors[file.Name];

                    // For each component in this file
                    foreach (var compBounds in file.Components)
                    {
                        if (compBounds.Overlays == null) continue;

                        bool highlighted = listBoxSelectedActualValues.Contains(compBounds.Label);

                        foreach (var ov in compBounds.Overlays)
                        {
                            Rectangle rect = new Rectangle(
                                (int)(ov.Bounds.X * listZoom),
                                (int)(ov.Bounds.Y * listZoom),
                                (int)(ov.Bounds.Width * listZoom),
                                (int)(ov.Bounds.Height * listZoom)
                            );

                            listPanel.Overlays.Add(new OverlayInfo
                            {
                                Bounds = rect,
                                Color = colorList,
                                Opacity = opacityList,
                                Highlighted = highlighted,
                                ComponentLabel = compBounds.Label
                            });
                        }
                    }

                    listPanel.Invalidate();
                }
            }
        }

        private Bitmap GetCachedImage(PictureBox pb, bool highlighted, Color color, int opacity)
        {
            var cache = highlighted ? cachedHighlightImages : cachedTransparentImages;
            if (cache.TryGetValue(pb, out Bitmap bmp))
                return bmp;
            bmp = new Bitmap(pb.Width, pb.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(highlighted ? opacity : 0, color));
            }
            cache[pb] = bmp;
            return bmp;
        }


        // ---------------------------------------------------------------------------------
        //
        // CreateOverlayArraysToList
        //
        // Create a PictureBox per image and component in both the main zoomable image
        // and all the list images. This is the PictureBox only but it will not be
        // associated to any concrete object/image yet
        // ---------------------------------------------------------------------------------

        private void CreateOverlayArraysToList()
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
                                    Debug.WriteLine("Created PictureBox in LIST [" + file.Name + "] with hash [" + overlayPictureBox.GetHashCode() + "] - Overlay Name:" + component.Label + ", X:" + overlay.Bounds.X + ", Y:" + overlay.Bounds.Y + ", Width:" + overlay.Bounds.Width + ", Height:" + overlay.Bounds.Height);
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
            float xZoomFactor = (float)panelMain.Width / image.Width;
            float yZoomFactor = (float)panelMain.Height / image.Height;
            zoomFactor = Math.Min(xZoomFactor, yZoomFactor);
            panelImage.Size = new Size((int)(image.Width * zoomFactor), (int)(image.Height * zoomFactor));
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




        private void PanelMain_MouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
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


/*
        private void PanelImage_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                Debug.WriteLine("MouseDown event");
                lastMousePosition = e.Location;
            }
        }
*/


        // ---------------------------------------------------------------------------------

/*
        private void PanelImage_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                Debug.WriteLine("MouseMove event");
                int dx = e.X - lastMousePosition.X;
                int dy = e.Y - lastMousePosition.Y;

                panelMain.AutoScrollPosition = new Point(-panelMain.AutoScrollPosition.X - dx, -panelMain.AutoScrollPosition.Y - dy);
            }
        }
*/


        // ---------------------------------------------------------------------------------

/*
        private void PanelImage_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                Debug.WriteLine("MouseUp event");
                lastMousePosition = Point.Empty;
            }
        }
*/

        private void PanelImageComponent_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (sender is PictureBox pb)
            {
                if (e.Button == MouseButtons.Left)
                {
                    var hardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
                    var board = hardware.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
                    var components = board.Components.FirstOrDefault(c => c.Label == pb.Name);

                    Debug.WriteLine(pb.Name);
                    FormComponent formComponent = new FormComponent(components, hardwareSelectedFolder, boardSelectedFolder);
                    formComponent.ShowDialog();
                }
                else if (e.Button == MouseButtons.Right)
                {
                    Debug.WriteLine(pb.Name);

                    string key = listBoxNameValueMapping.FirstOrDefault(x => x.Value == pb.Name).Key;
                    int index = listBox1.FindString(key);

                    if (index >= 0)
                    {
                        bool currentlySelected = listBox1.GetSelected(index);
                        listBox1.SetSelected(index, !currentlySelected);
                    }
                    else
                    {
                        // Force adding the item back if missing from the selection
                        foreach (var item in listBoxNameValueMapping)
                        {
                            if (item.Value == pb.Name)
                            {
                                listBox1.Items.Add(item.Key);
                                int newIndex = listBox1.FindString(item.Key);
                                listBox1.SetSelected(newIndex, true);
                                break;
                            }
                        }
                    }

                    UpdateHighlights();
                }
            }
        }




        private void OverlayPanel_OverlayClicked(object sender, OverlayClickedEventArgs e)
        {
            // e.OverlayInfo.ComponentLabel = which overlay was clicked
            // e.MouseArgs.Button tells left or right

            if (e.MouseArgs.Button == MouseButtons.Left)
            {
                // Show form for the clicked component
                var hardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
                var board = hardware?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
                var comp = board?.Components.FirstOrDefault(c => c.Label == e.OverlayInfo.ComponentLabel);
                if (comp != null)
                {
                    Debug.WriteLine("Left-click on " + comp.Label);
                    FormComponent formComponent = new FormComponent(comp, hardwareSelectedFolder, boardSelectedFolder);
                    formComponent.ShowDialog();
                }
            }
            else if (e.MouseArgs.Button == MouseButtons.Right)
            {
                // Toggle highlight in listBox1
                string labelClicked = e.OverlayInfo.ComponentLabel;
                Debug.WriteLine("Right-click on " + labelClicked);

                // Find item in listBox1 that has Value == labelClicked
                string key = listBoxNameValueMapping
                    .FirstOrDefault(x => x.Value == labelClicked)
                    .Key;
                int index = listBox1.FindString(key);

                if (index >= 0)
                {
                    bool currentlySelected = listBox1.GetSelected(index);
                    listBox1.SetSelected(index, !currentlySelected);
                }
                else
                {
                    // If not in list, re-add
                    foreach (var item in listBoxNameValueMapping)
                    {
                        if (item.Value == labelClicked)
                        {
                            listBox1.Items.Add(item.Key);
                            int newIndex = listBox1.FindString(item.Key);
                            listBox1.SetSelected(newIndex, true);
                            break;
                        }
                    }
                }

                UpdateHighlights();
            }
        }

        //
        // 2) OverlayPanel_OverlayHoverChanged: handle mouse entering/leaving an overlay
        //
        private void OverlayPanel_OverlayHoverChanged(object sender, OverlayHoverChangedEventArgs e)
        {
            if (labelComponent == null) return;  // Just a safety check

            if (e.IsHovering)
            {
                // Mouse just entered an overlay
                this.Cursor = Cursors.Hand;
                labelComponent.Text = e.OverlayInfo.ComponentLabel;
                labelComponent.Visible = true;
            }
            else
            {
                // Mouse left the overlay
                this.Cursor = Cursors.Default;
                labelComponent.Visible = false;
            }
        }

        //
        // 3) We replicate your right-click-drag logic on empty space (panning the image):
        //
        private Point overlayPanelLastMousePos = Point.Empty;

        private void OverlayPanel_OverlayPanelMouseDown(object sender, MouseEventArgs e)
        {
            // Called when user clicks empty space in overlayPanel
            if (e.Button == MouseButtons.Right)
            {
                Debug.WriteLine("Right-click on empty space: start drag");
                overlayPanelLastMousePos = e.Location;
            }
        }

        private void OverlayPanel_OverlayPanelMouseMove(object sender, MouseEventArgs e)
        {
            // If user is holding right-click on empty space, we pan
            if (e.Button == MouseButtons.Right && overlayPanelLastMousePos != Point.Empty)
            {
                int dx = e.X - overlayPanelLastMousePos.X;
                int dy = e.Y - overlayPanelLastMousePos.Y;
                panelMain.AutoScrollPosition = new Point(
                    -panelMain.AutoScrollPosition.X - dx,
                    -panelMain.AutoScrollPosition.Y - dy
                );
            }
        }

        private void OverlayPanel_OverlayPanelMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                Debug.WriteLine("End right-click drag");
                overlayPanelLastMousePos = Point.Empty;
            }
        }


        // ---------------------------------------------------------------------------------

/*
        private void Overlay_MouseEnter(object sender, EventArgs e)
        {
            this.Cursor = System.Windows.Forms.Cursors.Hand;
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
*/

        private void PanelList2_MouseEnter(object sender, EventArgs e)
        {
            this.Cursor = System.Windows.Forms.Cursors.Hand;
        }

        private void PanelList2_MouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
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

/*
        private void Overlay_MouseLeave(object sender, EventArgs e)
        {
            this.Cursor = System.Windows.Forms.Cursors.Default;
            //label3.Visible = false;
            System.Windows.Forms.Label label = (System.Windows.Forms.Label)this.Controls.Find("labelComponent", true).FirstOrDefault();
            label.Visible = false;
        }
*/

        private void PanelList2_MouseLeave(object sender, EventArgs e)
        {
            this.Cursor = System.Windows.Forms.Cursors.Default;
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
        public event System.Windows.Forms.MouseEventHandler CustomMouseWheel;

        protected override void OnMouseWheel(System.Windows.Forms.MouseEventArgs e)
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
        public int HighlightOpacityTab { get; set; }
        public int HighlightOpacityList { get; set; }
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
        public List<LocalFiles> LocalFiles { get; set; }
        public List<ComponentLinks> ComponentLinks { get; set; }
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

    public class LocalFiles
    {
        public string Name { get; set; }
        public string FileName { get; set; }
    }

    public class ComponentLinks
    {
        public string Name { get; set; }
        public string Url { get; set; }
    }



    // #################################################################################


}