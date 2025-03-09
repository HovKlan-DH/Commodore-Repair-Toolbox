using Commodore_Repair_Toolbox;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows.Forms;

/*

VGG Image Annotator
https://www.robots.ox.ac.uk/~vgg/software/via/

*/

namespace Commodore_Repair_Toolbox
{
    public partial class Main : Form
    {

        private Timer blinkTimer;
        private bool blinkState = false;

        private FormComponent currentPopup = null;

        // Fullscreen
        private bool isFullscreen = false;
        private FormWindowState formPreviousWindowState;
        private FormBorderStyle formPreviousFormBorderStyle;
        private Rectangle previousBoundsForm;
        private Rectangle previousBoundsPanelBehindTab;

        // ---------------------------------------------------------------------
        // UI labels
        private Label labelFile;
        private Label labelComponent;

        // Overlay caching (optional optimization)
        private Dictionary<PictureBox, Bitmap> cachedHighlightImages = new Dictionary<PictureBox, Bitmap>();
        private Dictionary<PictureBox, Bitmap> cachedTransparentImages = new Dictionary<PictureBox, Bitmap>();

        // Overlays for the right-side list
        private Dictionary<string, OverlayPanel> overlayPanelsList = new Dictionary<string, OverlayPanel>();
        private Dictionary<string, float> overlayListZoomFactors = new Dictionary<string, float>();

        // Main overlay panel
        private OverlayPanel overlayPanel;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private bool isResizedByMouseWheel = false;
        private bool isResizing = false;

        // Main panel (left side) + image
        private CustomPanel panelMain;
        private Panel panelImage;
        private Image image;

        // Data loaded from Excel
        public List<Hardware> classHardware = new List<Hardware>();

        // Overlays for main image
        private List<PictureBox> overlayComponentsTab = new List<PictureBox>();
        private Dictionary<int, Point> overlayComponentsTabOriginalLocations = new Dictionary<int, Point>();
        private Dictionary<int, Size> overlayComponentsTabOriginalSizes = new Dictionary<int, Size>();

        // Old references
        private List<string> selectedItems = new List<string>();
        private Dictionary<string, List<PictureBox>> overlayComponentsList = new Dictionary<string, List<PictureBox>>();
        private List<PictureBox> visiblePictureBoxes = new List<PictureBox>();
        private Dictionary<string, string> listBoxNameValueMapping = new Dictionary<string, string>();
        private List<string> listBoxSelectedActualValues = new List<string>();

        // Current user selection
        public string hardwareSelectedName;
        private string hardwareSelectedFolder;
        private string boardSelectedName;
        private string boardSelectedFolder;
        private string imageSelectedName;
        private string imageSelectedFile;

        private float zoomFactor = 1.0f;
        private Point overlayPanelLastMousePos = Point.Empty;

        // URL for webBrowser pre-check
        private string initialUrl = "https://commodore-repair-toolbox.dk/hest1";

        // ---------------------------------------------------------------------
        // Constructor

        public Main()
        {
            InitializeComponent();
            EnableDoubleBuffering();
            AttachEventHandlers();

            LoadData();
            PopulateComboBoxes();
            LoadSettings();

            ApplySavedSettings();

            AttachConfigurationSaveEvents();

            // Initialize the blink timer
            blinkTimer = new Timer();
            blinkTimer.Interval = 500; // Blink interval in milliseconds
            blinkTimer.Tick += BlinkTimer_Tick;

            // Attach the checkbox event handler
            checkBox1.CheckedChanged += CheckBox1_CheckedChanged;

            AdjustComponentCategoriesListBoxHeight();

            richTextBox3.Rtf = @"{\rtf1\ansi
\i Commodore Repair Toolbox\i0  is not that advanced, so it is quite simple to use.\par
\par
Mouse functions:\par
\pard    \'95  \b Left-click\b0  on a component will show a popup with more information\par
\pard    \'95  \b Right-click\b0  on a component will highlight it\par
\pard    \'95  \b Right-click\b0 and \b Hold down\b0  will pan the image\par
\pard
\par
Keyboard functions:\par
\pard    \'95  \b F11\b0  will toggle fullscreen\par
\pard    \'95  \b ESCAPE\b0  will exit fullscreen or close popup info\par
\pard    \'95  \b SPACE\b0  will toggle blinking for selected components\par
\pard
\par
Configuration saved:\par
\pard    \'95  Last-viewed schematics\par
\pard    \'95  Schematics divider/slider position\par
\pard
\par
How-to add or update something yourself:\par
\pard    \'95  View https://github.com/HovKlan-DH/Commodore-Repair-Toolbox\par
\pard
\par
}";
        }

        private void CheckBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                BlinkTimer_Tick(null, null); // Perform the first blink immediately
                blinkTimer.Start();
            }
            else
            {
                blinkTimer.Stop();
                EnableSelectedOverlays();
            }
        }

        private void EnableSelectedOverlays()
        {
            foreach (var overlayPanel in overlayPanelsList.Values)
            {
                foreach (var overlay in overlayPanel.Overlays)
                {
                    if (listBoxSelectedActualValues.Contains(overlay.ComponentLabel))
                    {
                        overlay.Highlighted = true;
                    }
                }
                overlayPanel.Invalidate();
            }

            if (overlayPanel != null)
            {
                foreach (var overlay in overlayPanel.Overlays)
                {
                    if (listBoxSelectedActualValues.Contains(overlay.ComponentLabel))
                    {
                        overlay.Highlighted = true;
                    }
                }
                overlayPanel.Invalidate();
            }
        }

        private void BlinkTimer_Tick(object sender, EventArgs e)
        {
            BlinkSelectedOverlays(blinkState);
            blinkState = !blinkState;
        }

        private void ResetOverlayVisibility()
        {
            foreach (var overlayPanel in overlayPanelsList.Values)
            {
                foreach (var overlay in overlayPanel.Overlays)
                {
                    overlay.Highlighted = true;
                }
                overlayPanel.Invalidate();
            }

            if (overlayPanel != null)
            {
                foreach (var overlay in overlayPanel.Overlays)
                {
                    overlay.Highlighted = true;
                }
                overlayPanel.Invalidate();
            }
        }

        private void BlinkSelectedOverlays(bool state)
        {
            foreach (var overlayPanel in overlayPanelsList.Values)
            {
                foreach (var overlay in overlayPanel.Overlays)
                {
                    if (listBoxSelectedActualValues.Contains(overlay.ComponentLabel))
                    {
                        overlay.Highlighted = state;
                    }
                }
                overlayPanel.Invalidate();
            }

            if (overlayPanel != null)
            {
                foreach (var overlay in overlayPanel.Overlays)
                {
                    if (listBoxSelectedActualValues.Contains(overlay.ComponentLabel))
                    {
                        overlay.Highlighted = state;
                    }
                }
                overlayPanel.Invalidate();
            }
        }

        private void AdjustComponentCategoriesListBoxHeight()
        {
            int listBoxLocationEnd_org = listBox2.Location.Y + listBox2.Height;
            int itemHeight = listBox2.ItemHeight;
            int itemCount = listBox2.Items.Count;
            int borderHeight = listBox2.Height - listBox2.ClientSize.Height;
            listBox2.Height = (itemHeight * itemCount) + borderHeight;
            int listBoxLocationEnd_new = listBox2.Location.Y + listBox2.Height;
            int diff =  listBoxLocationEnd_org - listBoxLocationEnd_new;
            if (diff > 0)
            {
                label2.Location = new Point(label2.Location.X, label2.Location.Y - diff - 11);
                listBox1.Location = new Point(listBox1.Location.X, listBox1.Location.Y - diff - 11);
                listBox1.Height = listBox1.Height + diff + 11;
            } else
            {
                label2.Location = new Point(label2.Location.X, label2.Location.Y + diff);
            }
        }

        /*
        // Call this method after populating listBox2
        private void InitializeComponentCategoriesListBox()
        {
            listBox2.Items.Clear();

            var foundHardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var foundBoard = foundHardware?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
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

            // Auto-select all
            for (int i = 0; i < listBox2.Items.Count; i++)
            {
                listBox2.SetSelected(i, true);
            }

            // Adjust the height of listBox2 to match the number of elements
            AdjustComponentCategoriesListBoxHeight();
        }
        */

        // ---------------------------------------------------------------------
        // Enable double-buffering for smoother UI rendering
        private void EnableDoubleBuffering()
        {
            this.SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint,
                true
            );
            this.UpdateStyles();

            panelZoom.DoubleBuffered(true);
            panelListMain.DoubleBuffered(true);
            panelListAutoscroll.DoubleBuffered(true);
        }

        // ---------------------------------------------------------------------
        // Attach necessary event handlers
        private void AttachEventHandlers()
        {
            ResizeBegin += Form_ResizeBegin;
            ResizeEnd += Form_ResizeEnd;
            Resize += Form_Resize;
            Shown += Main_Shown;
            panelListAutoscroll.Resize += panelListAutoscroll_Resize;
            panelListAutoscroll.Layout += PanelListAutoscroll_Layout;
            this.Load += Form_Loaded;
            tabControl1.Dock = DockStyle.Fill;

            // Subscribe to the Paint event of the SplitContainer
            splitContainer1.Paint += SplitContainer1_Paint;
        }

        // ---------------------------------------------------------------------
        // Load hardware data from Excel
        private void LoadData()
        {
            DataStructure.GetAllData(classHardware);

            // Debug print loaded data
            foreach (Hardware hw2 in classHardware)
            {
                Debug.WriteLine($"[Hardware: {hw2.Name}] Folder={hw2.Folder}");
                foreach (Board bd in hw2.Boards)
                {
                    Debug.WriteLine($"   [Board: {bd.Name}] Folder={bd.Folder}");
                    foreach (BoardFile bf in bd.Files)
                    {
                        Debug.WriteLine($"      [BoardFile: {bf.Name}] => {bf.FileName}");
                    }
                }
            }
        }

        // ---------------------------------------------------------------------
        // Populate combo boxes with loaded data
        private void PopulateComboBoxes()
        {
            foreach (Hardware hardware in classHardware)
            {
                comboBox1.Items.Add(hardware.Name);
            }
            comboBox1.SelectedIndex = 0;
            hardwareSelectedName = comboBox1.SelectedItem.ToString();
        }

        // ---------------------------------------------------------------------
        // Load settings from configuration file
        private void LoadSettings()
        {
            Configuration.LoadConfig();
        }

        // ---------------------------------------------------------------------
        // Apply saved settings to controls
        private void ApplySavedSettings()
        {
            // Load saved settings for combo boxes, splitter, and selected image
            string splitterPosVal = Configuration.GetSetting("SplitterPosition", "250");
            string comboBox1Val = Configuration.GetSetting("ComboBox1Index", "0");
            string comboBox2Val = Configuration.GetSetting("ComboBox2Index", "0");
            string selectedImageVal = Configuration.GetSetting("SelectedImage", "");

            // Apply splitter position
            if (int.TryParse(splitterPosVal, out int splitterPosition) && splitterPosition > 0)
            {
                splitContainer1.SplitterDistance = splitterPosition;
            }

            // Apply combo box selections
            if (int.TryParse(comboBox1Val, out int comboBox1Index) && comboBox1Index >= 0 && comboBox1Index < comboBox1.Items.Count)
            {
                comboBox1.SelectedIndex = comboBox1Index;
            }
            else
            {
                comboBox1.SelectedIndex = 0;
            }

            if (int.TryParse(comboBox2Val, out int comboBox2Index) && comboBox2Index >= 0 && comboBox2Index < comboBox2.Items.Count)
            {
                comboBox2.SelectedIndex = comboBox2Index;
            }
            else
            {
                comboBox2.SelectedIndex = 0;
            }

            // Apply selected image if exists
            if (!string.IsNullOrEmpty(selectedImageVal))
            {
                imageSelectedName = selectedImageVal;
                LoadSelectedImage();
            }
        }

        // ---------------------------------------------------------------------
        // Load the selected image based on saved setting
        private void LoadSelectedImage()
        {
            var hw = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var bd = hw?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (bd != null)
            {
                var file = bd.Files.FirstOrDefault(f => f.Name == imageSelectedName);
                if (file != null)
                {
                    imageSelectedFile = file.FileName;
                    InitializeTabMain();  // Load the selected image
                }
            }
        }

        // ---------------------------------------------------------------------
        // Attach event handlers for saving settings
        private void AttachConfigurationSaveEvents()
        {
            // Save combo box selections
            comboBox1.SelectedIndexChanged += (s, e) =>
            {
                Configuration.SaveSetting("ComboBox1Index", comboBox1.SelectedIndex.ToString());
            };

            comboBox2.SelectedIndexChanged += (s, e) =>
            {
                Configuration.SaveSetting("ComboBox2Index", comboBox2.SelectedIndex.ToString());
            };

            // Save splitter position
            splitContainer1.SplitterMoved += (s, e) =>
            {
                Configuration.SaveSetting("SplitterPosition", splitContainer1.SplitterDistance.ToString());
            };

            // Save selected image when changed
            panelListAutoscroll.ControlAdded += (s, e) =>
            {
                if (e.Control is Panel panel && panel.Name == imageSelectedName)
                {
                    Configuration.SaveSetting("SelectedImage", imageSelectedName);
                }
            };
        }


        // ---------------------------------------------------------------------------
        // Fullscreen - Enter
        // ---------------------------------------------------------------------------

        private void FullscreenModeEnter()
        {
            // Save current and set new window state
            formPreviousWindowState = this.WindowState;
            formPreviousFormBorderStyle = this.FormBorderStyle;
            previousBoundsForm = this.Bounds;
            previousBoundsPanelBehindTab = panelBehindTab.Bounds;
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Normal;
            this.Bounds = Screen.PrimaryScreen.Bounds;

            // Set bounds for fullscreen panel
            panelBehindTab.Location = new Point(panelBehindTab.Location.X, 0);
            panelBehindTab.Width = ClientSize.Width - panelBehindTab.Location.X;
            panelBehindTab.Height = ClientSize.Height;

            // Determine which tab should be maximized
            panelBehindTab.Controls.Remove(panelZoom);
            panelBehindTab.Controls.Add(panelZoom);

            // Hide tabs, and show fullscreen panel
            tabControl1.Visible = false;
            buttonFullscreen.Text = "Exit fullscreen";
            isFullscreen = true;
        }

        // ---------------------------------------------------------------------------
        // Fullscreen - exit
        // ---------------------------------------------------------------------------

        private void FullscreenModeExit()
        {
            // Restore previous window state
            this.FormBorderStyle = formPreviousFormBorderStyle;
            this.WindowState = formPreviousWindowState;
            this.Bounds = previousBoundsForm;
            panelBehindTab.Bounds = previousBoundsPanelBehindTab;

            // Determine which tab should be repopulated with the previous maximized panel
            panelBehindTab.Controls.Remove(panelZoom);
            splitContainer1.Panel1.Controls.Add(panelZoom);

            // Show again the tabs, and hide the fullscreen panel
            tabControl1.Visible = true;
            //            AdjustPanelSchematicPanelsWidth();
            buttonFullscreen.Text = "Fullscreen";
            isFullscreen = false;
        }

        // ---------------------------------------------------------------------------
        // Event - form initialized, but not yet shown
        // ---------------------------------------------------------------------------

        private void Form_Loaded(object sender, EventArgs e)
        {
//            panelBehindTab.Location = new Point(panelBehindTab.Location.X, 0);
//            InitializeList();

        }

        private void AttachClosePopupOnClick(Control parent)
        {
            // Attach a single MouseDown event to close any open popup
            parent.MouseDown += (s, e) =>
            {
                // Only if we click in the main form and a popup is open
                if (currentPopup != null && currentPopup.Visible)
                {
                    currentPopup.Close();
                    currentPopup = null;
                }
            };



            // Recurse to child controls
            foreach (Control child in parent.Controls)
            {
                // If you want to SKIP certain controls (text boxes?), you can do:
                // if (child is TextBox) continue;
                // or similar.
                AttachClosePopupOnClick(child);
            }
        }

        private void ShowComponentPopup(ComponentBoard comp)
        {
            // If an old popup is still open, close it
            if (currentPopup != null && !currentPopup.IsDisposed)
            {
                currentPopup.Close();
            }

            // Create new popup
            currentPopup = new FormComponent(comp, hardwareSelectedFolder, boardSelectedFolder);

            // Show it modeless (non-blocking)
            currentPopup.Show();
        }

        // ---------------------------------------------------------------------
        // Setup new board after user selects in comboBox2

        private void SetupNewBoard()
        {
            boardSelectedName = comboBox2.SelectedItem.ToString();

            var selectedHardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var selectedBoard = selectedHardware?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (selectedHardware == null || selectedBoard == null) return;

            hardwareSelectedFolder = selectedHardware.Folder;
            boardSelectedFolder = selectedBoard.Folder;

            // Default to first file
            imageSelectedName = selectedBoard.Files.FirstOrDefault()?.Name;
            imageSelectedFile = selectedBoard.Files.FirstOrDefault()?.FileName;

            // Initialize UI
            InitializeComponentCategories();
            InitializeComponentList();
            InitializeList();
            InitializeTabMain();

            // Attempt to load a URL in webBrowser
            NavigateWithPreCheck(initialUrl);
        }

        // ---------------------------------------------------------------------
        // Web browser logic

        private async void NavigateWithPreCheck(string url)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        webBrowser1.Navigate(url);
                    }
                    else
                    {
                        webBrowser1.DocumentText = $"Failed to load content: HTTP error {response.StatusCode} - {response.ReasonPhrase}";
                    }
                }
                catch (HttpRequestException ex)
                {
                    webBrowser1.DocumentText = $"Failed to load content: Network error - {ex.Message}";
                }
            }
        }

        private void webBrowser1_Navigating_1(object sender, WebBrowserNavigatingEventArgs e)
        {
            // Force external links to open in external browser
            if (e.Url.ToString() != initialUrl)
            {
                e.Cancel = true;
                Process.Start("explorer.exe", e.Url.ToString());
            }
        }

        // ---------------------------------------------------------------------
        // Form events

        private void Form_ResizeBegin(object sender, EventArgs e)
        {
            Debug.WriteLine("Form_ResizeBegin");
            isResizing = true;
        }

        private void Form_ResizeEnd(object sender, EventArgs e)
        {
            Debug.WriteLine("Form_ResizeEnd");
            isResizing = false;
            ResizeTabImage();
            HighlightOverlays("tab");
            HighlightOverlays("list");
        }

        private void Form_Resize(object sender, EventArgs e)
        {
            Debug.WriteLine("Form_Resized");
            InitializeList();
        }

        private void Main_Shown(object sender, EventArgs e)
        {
            // Optionally do something when form is first shown
        }

        // ---------------------------------------------------------------------
        // Lists of components (left side listBox2 -> listBox1)

        private void InitializeComponentCategories()
        {
            listBox2.Items.Clear();

            var foundHardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var foundBoard = foundHardware?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
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

            // Auto-select all
            for (int i = 0; i < listBox2.Items.Count; i++)
            {
                listBox2.SetSelected(i, true);
            }
        }

        private void InitializeComponentList(bool clearList = true)
        {
            // Keep track of currently selected items
            foreach (var item in listBox1.SelectedItems)
            {
                selectedItems.Add(listBoxNameValueMapping[item.ToString()]);
            }

            listBox1.Items.Clear();
            listBoxNameValueMapping.Clear();

            var foundHardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var foundBoard = foundHardware?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (foundBoard != null)
            {
                foreach (ComponentBoard comp in foundBoard.Components)
                {
                    if (listBox2.SelectedItems.Contains(comp.Type))
                    {
                        string displayText = comp.Label + "   " + comp.NameTechnical + "   " + comp.NameFriendly;
                        listBox1.Items.Add(displayText);
                        listBoxNameValueMapping[displayText] = comp.Label;

                        if (selectedItems.Contains(comp.Label))
                        {
                            int idx = listBox1.Items.IndexOf(displayText);
                            listBox1.SetSelected(idx, true);
                        }
                    }
                }
            }

            // Remove items that are no longer visible
            List<string> itemsToRemove = new List<string>();
            foreach (string sel in selectedItems)
            {
                if (!listBoxNameValueMapping.Values.Contains(sel))
                {
                    itemsToRemove.Add(sel);
                }
            }
            foreach (string rem in itemsToRemove)
            {
                selectedItems.Remove(rem);
            }
        }

        // ---------------------------------------------------------------------
        // Tab: main image (left side)

        private void InitializeTabMain()
        {
            // Load main image
            image = Image.FromFile(
                Path.Combine(Application.StartupPath, "Data", hardwareSelectedFolder, boardSelectedFolder, imageSelectedFile)
            );

            // Clear old controls
            panelZoom.Controls.Clear();
            overlayComponentsTab.Clear();
            overlayComponentsTabOriginalSizes.Clear();
            overlayComponentsTabOriginalLocations.Clear();

            // Build the arrays
            CreateOverlayArraysToTab();

            // Create scrolling container
            panelMain = new CustomPanel
            {
                Size = new Size(panelZoom.Width - panelListMain.Width - 25, panelZoom.Height),
                AutoScroll = true,
                Dock = DockStyle.Fill
            };
            panelMain.DoubleBuffered(true);
            panelZoom.Controls.Add(panelMain);

            // Create panelImage for the main picture
            panelImage = new Panel
            {
                Size = image.Size,
                BackgroundImage = image,
                BackgroundImageLayout = ImageLayout.Zoom,
                Dock = DockStyle.None
            };
            panelImage.DoubleBuffered(true);
            panelMain.Controls.Add(panelImage);

            // Create overlay panel on top
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

            // Top-left label: file name
            labelFile = new Label
            {
                Name = "labelFile",
                Text = imageSelectedName,
                AutoSize = true,
                BackColor = Color.Khaki,
                ForeColor = Color.Black,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Calibri", 11),
                Location = new Point(5, 5),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            labelFile.DoubleBuffered(true);
            panelZoom.Controls.Add(labelFile);
            labelFile.BringToFront();

            // Top-left label: hovered component name
            labelComponent = new Label
            {
                Name = "labelComponent",
                AutoSize = true,
                BackColor = Color.Khaki,
                ForeColor = Color.Black,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Calibri", 13, FontStyle.Bold),
                Location = new Point(5, 30),
                Visible = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            labelComponent.DoubleBuffered(true);
            panelZoom.Controls.Add(labelComponent);
            labelComponent.BringToFront();

            // Finish up
            ResizeTabImage();
            panelMain.CustomMouseWheel += PanelMain_MouseWheel;
            panelMain.Resize += PanelMain_Resize;

            // THEN re-run the attach method:
            AttachClosePopupOnClick(this);
        }

        // ---------------------------------------------------------------------
        // Right-side list (thumbnails)

        private void PanelListAutoscroll_Layout(object sender, LayoutEventArgs e)
        {
            AdjustImageSizes();
        }

        private void InitializeList()
        {
            panelListAutoscroll.Controls.Clear();
            overlayPanelsList.Clear();
            overlayListZoomFactors.Clear();

            int yPosition = 5;
            var hw = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var bd = hw?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (bd == null) return;

            foreach (BoardFile file in bd.Files)
            {
                // Load background image
                string path = Path.Combine(Application.StartupPath, "Data", hardwareSelectedFolder, boardSelectedFolder, file.FileName);
                Image image2 = Image.FromFile(path);

                Panel panelList2 = new Panel
                {
                    Name = file.Name,
                    BackgroundImage = image2,
                    BackgroundImageLayout = ImageLayout.Zoom,
                    Location = new Point(0, yPosition),
                    Dock = DockStyle.None,
                    Padding = new Padding(0),
                    Margin = new Padding(0)
                };
                panelList2.DoubleBuffered(true);

                // Initial size calculation
                float xZoomFactor = (float)panelListAutoscroll.ClientSize.Width / image2.Width;
                float yZoomFactor = (float)panelListAutoscroll.ClientSize.Height / image2.Height;
                float zoomFactor = Math.Min(xZoomFactor, yZoomFactor);

                panelList2.Size = new Size(
                    (int)(image2.Width * zoomFactor),
                    (int)(image2.Height * zoomFactor)
                );

                // Overlay panel on top
                OverlayPanel overlayPanelList = new OverlayPanel
                {
                    Bounds = panelList2.ClientRectangle
                };
                panelList2.Controls.Add(overlayPanelList);
                overlayPanelList.BringToFront();

                // Left-click empty space => pick main image
                overlayPanelList.OverlayPanelMouseDown += (s, e2) =>
                {
                    if (e2.Button == MouseButtons.Left)
                    {
                        OnListImageLeftClicked(panelList2);
                    }
                };

                // Store references for overlay drawing
                overlayPanelsList[file.Name] = overlayPanelList;
                overlayListZoomFactors[file.Name] = zoomFactor;

                // Label for file name
                Label labelListFile = new Label
                {
                    Text = file.Name,
                    Location = new Point(0, 0),
                    BorderStyle = BorderStyle.FixedSingle,
                    AutoSize = true,
                    BackColor = Color.Khaki,
                    ForeColor = Color.Black,
                    Font = new Font("Calibri", 9),
                    Padding = new Padding(2),
                    Margin = new Padding(0)
                };
                labelListFile.DoubleBuffered(true);
                labelListFile.Parent = panelList2;
                labelListFile.BringToFront();

                panelListAutoscroll.Controls.Add(panelList2);
                yPosition += panelList2.Height + 3;
            }

            // Adjust the height of the panelListAutoscroll to fit the thumbnails
            panelListAutoscroll.AutoScrollMinSize = new Size(0, yPosition + 5);

            DrawBorderInList();
            HighlightOverlays("list");
        }

        private void AdjustImageSizes()
        {
            int scrollbarWidth = panelListAutoscroll.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth - 14: 0;
            int availableWidth = panelListAutoscroll.ClientSize.Width - scrollbarWidth;

            int yPosition = 5;

            foreach (Panel panelList2 in panelListAutoscroll.Controls.OfType<Panel>())
            {
                if (panelList2.BackgroundImage != null)
                {
                    float aspectRatio = (float)panelList2.BackgroundImage.Height / panelList2.BackgroundImage.Width;
                    int newHeight = (int)(availableWidth * aspectRatio);

                    panelList2.Bounds = new Rectangle(0, yPosition, availableWidth, newHeight);

                    yPosition += newHeight + 5; // 5px spacing between panels
                }
            }

            // Adjust the height of the panelListAutoscroll to fit the thumbnails
            panelListAutoscroll.AutoScrollMinSize = new Size(0, yPosition + 5);
        }





        private void panelListAutoscroll_Resize(object sender, EventArgs e)
        {
            //AdjustImageSizes();
        }










        private void OnListImageLeftClicked(Panel pan)
        {
            imageSelectedName = pan.Name;
            Configuration.SaveSetting("SelectedImage", imageSelectedName);  // Save selected image
            Debug.WriteLine("User clicked thumbnail: " + imageSelectedName);

            var hw = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var bd = hw?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (bd != null)
            {
                var file = bd.Files.FirstOrDefault(f => f.Name == imageSelectedName);
                if (file != null)
                {
                    imageSelectedFile = file.FileName;
                    InitializeTabMain();  // Load the selected image
                }
            }

            DrawBorderInList();  // Ensure border is updated
        }




        private void DrawBorderInList()
        {
            foreach (Panel panel in panelListAutoscroll.Controls.OfType<Panel>())
            {
                panel.Paint -= Panel_Paint_Standard;
                panel.Paint -= Panel_Paint_Special;

                if (panel.Name == imageSelectedName)
                {
                    panel.Paint += Panel_Paint_Special;
                }
                else
                {
                    panel.Paint += Panel_Paint_Standard;
                }
                panel.Invalidate();
            }
        }

        private void Panel_Paint_Standard(object sender, PaintEventArgs e)
        {
            float penWidth = 1;
            using (Pen pen = new Pen(Color.Black, penWidth))
            {
                float halfPenWidth = penWidth / 2;
                e.Graphics.DrawRectangle(
                    pen,
                    halfPenWidth,
                    halfPenWidth,
                    ((Panel)sender).Width - penWidth,
                    ((Panel)sender).Height - penWidth
                );
            }
        }

        private void Panel_Paint_Special(object sender, PaintEventArgs e)
        {
            float penWidth = 2;
            using (Pen pen = new Pen(Color.Red, penWidth))
            {
                pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                pen.DashPattern = new float[] { 4, 2 };
                float halfPenWidth = penWidth / 2;
                e.Graphics.DrawRectangle(
                    pen,
                    halfPenWidth,
                    halfPenWidth,
                    ((Panel)sender).Width - penWidth,
                    ((Panel)sender).Height - penWidth
                );
            }
        }

        // ---------------------------------------------------------------------
        // listBox events

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateHighlights();
        }

        private void listBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            InitializeComponentList(false);
            UpdateHighlights();
        }

        // "Clear Selection" button
        private void button1_Click(object sender, EventArgs e)
        {
            listBox1.SelectedIndex = -1;
            UpdateHighlights();
        }

        // "Select All" button
        private void button2_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < listBox1.Items.Count; i++)
            {
                listBox1.SetSelected(i, true);
            }
            UpdateHighlights();
        }

        // ---------------------------------------------------------------------
        // comboBox events

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            comboBox2.Items.Clear();
            hardwareSelectedName = comboBox1.SelectedItem.ToString();

            var hw = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            if (hw != null)
            {
                foreach (var board in hw.Boards)
                {
                    comboBox2.Items.Add(board.Name);
                }
                comboBox2.SelectedIndex = 0;
            }
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetupNewBoard();
            UpdateHighlights(); // Clear old highlights
        }

        // ---------------------------------------------------------------------
        // Overlays for main image

        private void ResizeTabImage()
        {
            if (image == null) return;

            float xZoomFactor = (float)panelMain.Width / image.Width;
            float yZoomFactor = (float)panelMain.Height / image.Height;
            zoomFactor = Math.Min(xZoomFactor, yZoomFactor);

            panelImage.Size = new Size(
                (int)(image.Width * zoomFactor),
                (int)(image.Height * zoomFactor)
            );
            HighlightOverlays("tab");
        }

        private void PanelMain_Resize(object sender, EventArgs e)
        {
            if (!isResizedByMouseWheel)
            {
                ResizeTabImage();
            }
            isResizedByMouseWheel = false;
        }

        private void PanelMain_MouseWheel(object sender, MouseEventArgs e)
        {
            ControlUpdateHelper.BeginControlUpdate(panelMain);

            Debug.WriteLine("MouseWheel event");

            try
            {
                float oldZoomFactor = zoomFactor;
                bool hasZoomChanged = false;

                if (e.Delta > 0) // scrolling up => zoom in
                {
                    if (zoomFactor <= 4.0f)
                    {
                        zoomFactor *= 1.5f;
                        hasZoomChanged = true;
                    }
                }
                else // scrolling down => zoom out
                {
                    // Only zoom out if the image is bigger than the container
                    if (panelImage.Width > panelMain.Width || panelImage.Height > panelMain.Height)
                    {
                        zoomFactor /= 1.5f;
                        hasZoomChanged = true;
                    }
                }

                if (hasZoomChanged)
                {
                    isResizedByMouseWheel = true;

                    // 2) Calculate new size
                    Size newSize = new Size(
                        (int)(image.Width * zoomFactor),
                        (int)(image.Height * zoomFactor)
                    );

                    // 3) Figure out how to keep the same "point under mouse"
                    Point mousePosition = new Point(
                        e.X - panelMain.AutoScrollPosition.X,
                        e.Y - panelMain.AutoScrollPosition.Y
                    );

                    Point newScrollPosition = new Point(
                        (int)(mousePosition.X * (zoomFactor / oldZoomFactor)),
                        (int)(mousePosition.Y * (zoomFactor / oldZoomFactor))
                    );

                    // 4) Apply the new size
                    panelImage.Size = newSize;

                    // 5) Update the scroll position
                    panelMain.AutoScrollPosition = new Point(
                        newScrollPosition.X - e.X,
                        newScrollPosition.Y - e.Y
                    );

                    // 6) Re‐highlight overlays (so they scale properly)
                    HighlightOverlays("tab");
                }
            }
            finally
            {
                ControlUpdateHelper.EndControlUpdate(panelMain);
            }
        }

        // ---------------------------------------------------------------------
        // Creating the overlay arrays

        private void CreateOverlayArraysToTab()
        {
            var hw = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var bd = hw?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (bd == null) return;

            // Only for the currently selected image
            foreach (BoardFile bf in bd.Files)
            {
                if (bf.Name != imageSelectedName) continue;

                foreach (var comp in bf.Components)
                {
                    if (comp.Overlays == null) continue;

                    foreach (var ov in comp.Overlays)
                    {
                        PictureBox overlayPictureBox = new PictureBox
                        {
                            Name = comp.Label,
                            Location = new Point(ov.Bounds.X, ov.Bounds.Y),
                            Size = new Size(ov.Bounds.Width, ov.Bounds.Height),
                            Tag = bf.Name
                        };

                        overlayComponentsTab.Add(overlayPictureBox);
                        int idx = overlayComponentsTab.Count - 1;
                        overlayComponentsTabOriginalSizes[idx] = overlayPictureBox.Size;
                        overlayComponentsTabOriginalLocations[idx] = overlayPictureBox.Location;
                    }
                }
            }
        }

        private void CreateOverlayArraysToList()
        {
            var hw = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var bd = hw?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (bd == null) return;

            foreach (BoardFile bf in bd.Files)
            {
                foreach (var comp in bf.Components)
                {
                    if (comp.Overlays == null) continue;

                    foreach (var ov in comp.Overlays)
                    {
                        PictureBox overlayPictureBox = new PictureBox
                        {
                            Name = comp.Label,
                            Location = new Point(ov.Bounds.X, ov.Bounds.Y),
                            Size = new Size(ov.Bounds.Width, ov.Bounds.Height),
                            Tag = bf.Name
                        };

                        if (!overlayComponentsList.ContainsKey(bf.Name))
                        {
                            overlayComponentsList[bf.Name] = new List<PictureBox>();
                        }
                        overlayComponentsList[bf.Name].Add(overlayPictureBox);

                        Debug.WriteLine($"Created PictureBox in LIST [{bf.Name}] with hash [{overlayPictureBox.GetHashCode()}] - Overlay Name: {comp.Label}, X:{ov.Bounds.X}, Y:{ov.Bounds.Y}, W:{ov.Bounds.Width}, H:{ov.Bounds.Height}");
                    }
                }
            }
        }

        // ---------------------------------------------------------------------
        // Handling highlight overlays

        private void HighlightOverlays(string scope)
        {
            if (this.WindowState == FormWindowState.Minimized || isResizing) return;

            var hw = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var bd = hw?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (bd == null) return;

            if (scope == "tab")
            {
                // Draw overlays on the main image
                if (overlayPanel == null) return;

                var bf = bd.Files.FirstOrDefault(f => f.Name == imageSelectedName);
                if (bf == null) return;

                overlayPanel.Overlays.Clear();

                Color colorZoom = Color.FromName(bf.HighlightColorTab);
                int opacityZoom = bf.HighlightOpacityTab;

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
                // Draw overlays on each thumbnail
                foreach (BoardFile bf in bd.Files)
                {
                    if (!overlayPanelsList.ContainsKey(bf.Name)) continue;

                    OverlayPanel listPanel = overlayPanelsList[bf.Name];
                    listPanel.Overlays.Clear();

                    Color colorList = Color.FromName(bf.HighlightColorList);
                    int opacityList = bf.HighlightOpacityList;
                    float listZoom = overlayListZoomFactors[bf.Name];

                    foreach (var comp in bf.Components)
                    {
                        if (comp.Overlays == null) continue;

                        bool highlighted = listBoxSelectedActualValues.Contains(comp.Label);

                        foreach (var ov in comp.Overlays)
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
                                ComponentLabel = comp.Label
                            });
                        }
                    }
                    listPanel.Invalidate();
                }
            }
        }

        private void UpdateHighlights()
        {
            listBoxSelectedActualValues.Clear();

            // Build a list of actual component labels from selected items
            foreach (var selectedItem in listBox1.SelectedItems)
            {
                string displayText = selectedItem.ToString();
                if (listBoxNameValueMapping.TryGetValue(displayText, out string actualValue))
                {
                    listBoxSelectedActualValues.Add(actualValue);
                }
            }

            HighlightOverlays("tab");
            HighlightOverlays("list");
        }

        // ---------------------------------------------------------------------
        // Overlay events (mouse click, hover, etc.)

        private void OverlayPanel_OverlayClicked(object sender, OverlayClickedEventArgs e)
        {
            // "labelClicked" is the component's label from the overlay
            string labelClicked = e.OverlayInfo.ComponentLabel;

            if (e.MouseArgs.Button == MouseButtons.Left)
            {
                // 1) HIGHLIGHT the overlay by ensuring it's selected in listBox1
                //    (If it isn't already.)
                string key = listBoxNameValueMapping
                    .FirstOrDefault(x => x.Value == labelClicked)
                    .Key; // e.g. "R12   74LS257   DataBusChip"
                int index = listBox1.FindString(key);
                if (index >= 0)
                {
                    // Make sure it's selected (force highlight)
                    if (!listBox1.GetSelected(index))
                    {
                        listBox1.SetSelected(index, true);
                    }
                }
                else
                {
                    // If not found in the list, optionally re-add
                    // (Many prefer ignoring if not found.)
                    // e.g.:
                    // listBox1.Items.Add(key);
                    // int newIndex = listBox1.FindString(key);
                    // listBox1.SetSelected(newIndex, true);
                }

                // Refresh the highlight overlays
                UpdateHighlights();

                // 2) SHOW the form for the clicked component
                var hardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
                var board = hardware?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
                var comp = board?.Components.FirstOrDefault(c => c.Label == labelClicked);
                if (comp != null)
                {
                    Debug.WriteLine("Left-click on " + comp.Label);
                    //FormComponent formComponent = new FormComponent(comp, hardwareSelectedFolder, boardSelectedFolder);
                    //formComponent.ShowDialog();
                    // hest
                    ShowComponentPopup(comp);
                }
            }
            else if (e.MouseArgs.Button == MouseButtons.Right)
            {
                // Existing RIGHT-CLICK toggle logic:
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
                    // If not in list, re-add it
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

        private void OverlayPanel_OverlayHoverChanged(object sender, OverlayHoverChangedEventArgs e)
        {
            if (labelComponent == null) return;

            if (e.IsHovering)
            {
                this.Cursor = Cursors.Hand;
                //labelComponent.Text = e.OverlayInfo.ComponentLabel;
                var hw = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
                var bd = hw?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
                var comp = bd?.Components.FirstOrDefault(c => c.Label == e.OverlayInfo.ComponentLabel);

                if (comp != null)
                {
                    labelComponent.Text = comp.Label + " | " + comp.NameTechnical + " | " + comp.NameFriendly;
                }
                else
                {
                    labelComponent.Text = e.OverlayInfo.ComponentLabel;
                }
                labelComponent.Visible = true;
            }
            else
            {
                this.Cursor = Cursors.Default;
                labelComponent.Visible = false;
            }
        }

        private void OverlayPanel_OverlayPanelMouseDown(object sender, MouseEventArgs e)
        {
            // Right-click drag on empty space
            if (e.Button == MouseButtons.Right)
            {
                Debug.WriteLine("Right-click drag start");
                overlayPanelLastMousePos = e.Location;
            }
        }

        private void OverlayPanel_OverlayPanelMouseMove(object sender, MouseEventArgs e)
        {
            // If user is dragging with right-click
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
                Debug.WriteLine("Right-click drag end");
                overlayPanelLastMousePos = Point.Empty;
            }
        }

        private void splitContainer1_SplitterMoved(object sender, SplitterEventArgs e)
        {
            InitializeList();
        }

        // ---------------------------------------------------------------------------
        // Custom paint event for SplitContainer
        // ---------------------------------------------------------------------------

        private void SplitContainer1_Paint(object sender, PaintEventArgs e)
        {
            SplitContainer splitContainer = sender as SplitContainer;
            if (splitContainer != null)
            {
                // Draw a custom line in the middle of the splitter
                int splitterWidth = splitContainer.SplitterWidth;
                int halfWidth = splitterWidth / 2;
                int x = splitContainer.SplitterDistance + halfWidth;
                int y1 = splitContainer.Panel1.ClientRectangle.Top;
                int y2 = splitContainer.Panel1.ClientRectangle.Bottom;

                using (Pen pen = new Pen(Color.LightGray, 2))
                {
                    e.Graphics.DrawLine(pen, x, y1, x, y2);
                }
            }
        }
        /*
                private void SplitContainer1_Paint(object sender, PaintEventArgs e)
                {
                    SplitContainer splitContainer = sender as SplitContainer;
                    if (splitContainer != null)
                    {
                        // Draw a custom line in the middle of the splitter
                        int splitterWidth = splitContainer.SplitterWidth;
                        int halfWidth = splitterWidth / 2;
                        int x = splitContainer.SplitterDistance + halfWidth;
                        int y1 = splitContainer.Panel1.ClientRectangle.Top;
                        int y2 = splitContainer.Panel1.ClientRectangle.Bottom;

                        using (Pen pen = new Pen(Color.DarkGray, 2))
                        {
                            e.Graphics.DrawLine(pen, x, y1, x, y2);
                        }
                    }
                }
        */

        private void buttonFullscreen_Click(object sender, EventArgs e)
        {
            if (!isFullscreen)
            {
                FullscreenModeEnter();
            }
            else
            {
                FullscreenModeExit();
                InitializeList();
            }
        }

        // ---------------------------------------------------------------------------
        // Keyboard handling
        // ---------------------------------------------------------------------------

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (tabControl1.SelectedTab.Text == "Schematics")
            {
                // Fullscreen mode toggle
                if (keyData == Keys.F11)
                {
                    buttonFullscreen_Click(null, null);
                    return true;
                }
                else if (keyData == Keys.Escape && isFullscreen)
                {
                    buttonFullscreen_Click(null, null);
                    return true;
                }
            }

            // Toggle checkBox1 with SPACE key
            if (keyData == Keys.Space)
            {
                checkBox1.Checked = !checkBox1.Checked;
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl1.SelectedTab.Text == "Ressources" || tabControl1.SelectedTab.Text == "About")
            {
                buttonFullscreen.Enabled = false;
            }
            else
            {
                buttonFullscreen.Enabled = true;
            }
        }

        private void richTextBox_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.LinkText) { UseShellExecute = true });
        }
    }



    // -------------------------------------------------------------------------
    // Support classes: renamed "File" -> "BoardFile" to avoid System.IO.File conflict

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

        // Use "BoardFile" instead of "File"
        public List<BoardFile> Files { get; set; }

        public List<ComponentBoard> Components { get; set; }
    }

    // Renamed "File" => "BoardFile"
    public class BoardFile
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
}