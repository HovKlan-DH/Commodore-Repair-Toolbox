using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;




namespace Commodore_Repair_Toolbox
{
    public partial class Main : Form
    {
        // Reference to the popup/info form
        private FormComponent currentPopup = null;

        // Blinking of components
        private Timer blinkTimer;
        private bool blinkState = false;

        // Fullscreen mode
        private bool isFullscreen = false;
        private FormWindowState formPreviousWindowState;
        private FormBorderStyle formPreviousFormBorderStyle;
        private Rectangle previousBoundsForm;
        private Rectangle previousBoundsPanelBehindTab;

        // Main panel (left side) + image
        private CustomPanel panelZoom;
        private Panel panelImage;
        private Image image;
        
        // "Main" schematics (left-side of SplitContainer)
        private Label labelFile;
        private Label labelComponent;
        private OverlayPanel overlayPanel;

        // Thumbnails (right-side of SplitContainer)
        private Dictionary<string, OverlayPanel> overlayPanelsList = new Dictionary<string, OverlayPanel>();
        private Dictionary<string, float> overlayListZoomFactors = new Dictionary<string, float>();

        // Resizing of window/schematic
        private bool isResizedByMouseWheel = false;
        private bool isResizing = false;

        

        // Data loaded from Excel
        public List<Hardware> classHardware = new List<Hardware>();

        // Overlays for main image
        private List<PictureBox> overlayComponentsTab = new List<PictureBox>();
        private Dictionary<int, Point> overlayComponentsTabOriginalLocations = new Dictionary<int, Point>();
        private Dictionary<int, Size> overlayComponentsTabOriginalSizes = new Dictionary<int, Size>();

        // Old references
        //private List<string> alistBoxComponentsSelectedItems = new List<string>();
        private List<string> listBoxComponentsSelectedLabels = new List<string>();
        private Dictionary<string, string> listBoxNameValueMapping = new Dictionary<string, string>();


        // Current user selection
        public string hardwareSelectedName;
        private string hardwareSelectedFolder;
        private string boardSelectedName;
        private string boardSelectedFolder;
        private string imageSelectedName;
        private string imageSelectedFile;

        private float zoomFactor = 1.0f;
        private Point overlayPanelLastMousePos = Point.Empty;

        private static string buildType = ""; // Debug, Release
        private static string appVer = "";
        private static string onlineAvailableVersion = "";
        private static string urlCheckOnlineVersion = "https://dennis.dk/crt/";

        // Add a field to track the current cursor state
//        private bool isCursorHand = false;

        // ---------------------------------------------------------------------
        // Constructor

        public Main()
        {
            InitializeComponent();
            EnableDoubleBuffering();

            // Initialize the blink timer
            blinkTimer = new Timer();
            blinkTimer.Interval = 500; // Blink interval in milliseconds
            blinkTimer.Tick += BlinkTimer_Tick;

            // Attach the checkbox event handler
            checkBoxBlink.CheckedChanged += CheckBox1_CheckedChanged;

            
            richTextBoxHelp.Rtf = @"{\rtf1\ansi
\i Commodore Repair Toolbox\i0  is not so advanced, and it is quite simple to use, but some basic help is always nice to have.\par
\par
Mouse functions:\par
\pard    \'95  \b Left-click\b0  on a component will show a information popup\par
\pard    \'95  \b Right-click\b0  on a component will toggle highlight\par
\pard    \'95  \b Right-click\b0  and \b Hold\b0  will pan the image\par
\pard    \'95  \b Scrollwheel\b0  will zoom in/out\par
\pard
\par
Keyboard functions:\par
\pard    \'95  \b F11\b0  will toggle fullscreen\par
\pard    \'95  \b ESCAPE\b0  will exit fullscreen or close popup info\par
\pard    \'95  \b SPACE\b0  will toggle blinking for selected components\par
\pard
\par
Component selection:\par
\pard    \'95  When a component is selected, then it will visualize if component is part of image in list-view:\par
\pard    \pard    \pard    \'95  Appending an asterisk/* as first character in label\par
\pard    \pard    \pard    \'95  Background color of label changes to red\par
\pard    \'95  You cannot highlight a component in image, if its component category is unselected\par
\par
Configuration saved:\par
\pard    \'95  Last-viewed schematics\par
\pard    \'95  Schematics divider/slider position\par
\pard    \'95  Component categories saved per board\par
\pard
\par
How-to add or update something yourself:\par
\pard    \'95  View https://github.com/HovKlan-DH/Commodore-Repair-Toolbox\par
\pard
\par
}";


            LoadExcelData();

            LoadSettings();
            //isFiltering = true;
            PopulateComboBoxes();
            Debug.WriteLine(splitContainerSchematics.SplitterDistance);

            AttachEventHandlers();





            // Get build type
#if DEBUG
            buildType = "Debug";
#else
            buildType = "Release";
#endif

            // Get application file version from assembly
            Assembly assemblyInfo = Assembly.GetExecutingAssembly();
            string assemblyVersion = FileVersionInfo.GetVersionInfo(assemblyInfo.Location).FileVersion;
            string year = assemblyVersion.Substring(0, 4);
            string month = assemblyVersion.Substring(5, 2);
            string day = assemblyVersion.Substring(8, 2);
            string rev = assemblyVersion.Substring(11); // will be ignored in RELEASE builds
            switch (month)
            {
                case "01": month = "January"; break;
                case "02": month = "February"; break;
                case "03": month = "March"; break;
                case "04": month = "April"; break;
                case "05": month = "May"; break;
                case "06": month = "June"; break;
                case "07": month = "July"; break;
                case "08": month = "August"; break;
                case "09": month = "September"; break;
                case "10": month = "October"; break;
                case "11": month = "November"; break;
                case "12": month = "December"; break;
                default: month = "Unknown"; break;
            }
            day = day.TrimStart(new Char[] { '0' }); // remove leading zero
            day = day.TrimEnd(new Char[] { '.' }); // remove last dot
            string date = year + "-" + month + "-" + day;

            // Beautify revision and build-type 
            rev = "(rev. " + rev + ")";
            rev = buildType == "Debug" ? rev : "";
            string buildTypeTmp = buildType == "Debug" ? "# DEVELOPMENT " : "";

            // Set the application version
            appVer = (date + " " + buildTypeTmp + rev).Trim();
            labelAboutVersion.Text = "Version: " + appVer;

            CheckForUpdate();

            // Attach the TextChanged event handler for textBox1
            textBox1.TextChanged += TextBox1_TextChanged;


            // Attach the Click event handler for the form and its child controls
            AttachClickEventHandlers(this);

            comboBoxHardware.DropDownClosed += ComboBox_DropDownClosed;
            comboBoxBoard.DropDownClosed += ComboBox_DropDownClosed;

            // Attach the SelectedIndexChanged event handler for listBoxCategories
            listBoxCategories.SelectedIndexChanged += ListBoxCategories_SelectedIndexChanged;



            
            richTextBoxRessources.LinkClicked += richTextBoxRessources_LinkClicked;

//            richTextBoxRessources.MouseDown += RichTextBoxRessources_MouseDown;















        }

        private void AttachClickEventHandlers(Control parent)
        {
            if (!(parent is ComboBox))
                parent.Click += (s, e) => textBox1.Focus();
            foreach (Control child in parent.Controls)
            {
                AttachClickEventHandlers(child);
            }
        }


        private void ComboBox_DropDownClosed(object sender, EventArgs e)
        {
            textBox1.Text = "";
        }


        private void ComboBoxHardware_SelectedIndexChanged(object sender, EventArgs e)
        {
            Debug.WriteLine("ComboBoxHardware_SelectedIndexChanged called");
            comboBoxBoard.SelectedIndexChanged -= ComboBoxBoard_SelectedIndexChanged; // Temporarily detach event handler
            comboBoxBoard.Items.Clear();
            hardwareSelectedName = comboBoxHardware.SelectedItem.ToString();

            var hw = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            if (hw != null)
            {
                foreach (var board in hw.Boards)
                {
                    comboBoxBoard.Items.Add(board.Name);
                }
                comboBoxBoard.SelectedIndex = 0;
            }

            comboBoxBoard.SelectedIndexChanged += ComboBoxBoard_SelectedIndexChanged; // Reattach event handler

            // Call FilterListBoxComponents to apply the filter after changing the ComboBox
            FilterListBoxComponents();
        }


        private void ComboBoxBoard_SelectedIndexChanged(object sender, EventArgs e)
        {
            Debug.WriteLine("ComboBoxBoard_SelectedIndexChanged called");
            SetupNewBoard();
            UpdateHighlights(); // Clear old highlights
            FilterListBoxComponents(); // Apply filter after setting up new board
        }

        private void TextBox1_TextChanged(object sender, EventArgs e)
        {
            Debug.WriteLine("TextBox1_TextChanged called");
            FilterListBoxComponents();
        }

        private void ListBoxCategories_SelectedIndexChanged(object sender, EventArgs e)
        {
            Debug.WriteLine("ListBoxCategories_SelectedIndexChanged called");
            FilterListBoxComponents();
        }

        /*
        * --------------------------
        * CHANGELOG FOR NEXT RELEASE
        * --------------------------
        * 
        * Application:
            * Fixed highlights in thumbnails were misaligned
            * Fixed clicking in thumbnail and directly at a component would not switch to this main image
            * Fixed dynamic resizing of list-boxes depending on selected board
            * Fixed "Component links" did not show
            * Fixed "About" and "Help" textboxes have been set to read-only
            * Fixed "Help" should not be accessible to fullscreen mode
            * Added show of asterisk/coloring in thumbnail label, when chosen component is visible in thumbnail
            * Added more text in "Help" tab 
            * Added check for newer version online (info in "About" tab)
            * Added filtering for components
            * Changed label in thumbnail so it no longer floats above the image, but is added before the image
            * Changed component list to now provide a simpler overview
        * Data:
            * Hardware: Commodore 128 or 128D
                * Board: 310378
                    * Added pinout for most components
                    * Refined highlights for multiple components

       
        */


        private void FilterListBoxComponents()
        {
            string filterText = textBox1.Text.ToLower();
            listBoxComponents.Items.Clear();
            listBoxNameValueMapping.Clear();

            var foundHardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var foundBoard = foundHardware?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (foundBoard != null)
            {
                foreach (ComponentBoard comp in foundBoard.Components)
                {
                    if (listBoxCategories.SelectedItems.Contains(comp.Type))
                    {
                        string displayText = comp.Label;
                        displayText += comp.NameTechnical != "?" ? " | " + comp.NameTechnical : "";
                        displayText += comp.NameFriendly != "?" ? " | " + comp.NameFriendly : "";

                        if (string.IsNullOrEmpty(filterText) || displayText.ToLower().Contains(filterText))
                        {
                            Debug.WriteLine(displayText);
                            listBoxComponents.Items.Add(displayText);
                            listBoxNameValueMapping[displayText] = comp.Label;
                        }
                    }
                }
            }
        }


        // ###########################################################################################
        // Check for a newer version online.
        // If newer version exists, then show this in "About" tab.
        // ###########################################################################################

        private void CheckForUpdate()
        {
            try
            {
                WebClient webClient = new WebClient();
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
                webClient.Headers.Add("user-agent", ("CRT " + appVer).Trim());
                
                // Have some control POST data
                var postData = new System.Collections.Specialized.NameValueCollection
                {
                    { "control", "CRT" }
                };

                // Send the POST data to the server
                byte[] responseBytes = webClient.UploadValues(urlCheckOnlineVersion, postData);

                // Convert the response bytes to a string
                onlineAvailableVersion = Encoding.UTF8.GetString(responseBytes);

                if (onlineAvailableVersion.Substring(0, 7) == "Version")
                {
                    onlineAvailableVersion = onlineAvailableVersion.Substring(9);

                    // Inform of new version
                    if (onlineAvailableVersion != appVer)
                    {
                        string existingRtf = richTextBoxAbout.Rtf;
                        int desiredFontSize = 32;
                        string newVersionRtf = @"\par\par\par \cf1 \fs" + desiredFontSize + @" There is a newer version available online: \b " + onlineAvailableVersion + @"\b0 \cf0 \fs0 \par";

                        // Insert new version info into existing RTF
                        int colorTableIndex = existingRtf.IndexOf(@"\colortbl");
                        int insertPos = existingRtf.IndexOf('}', colorTableIndex);
                        if (insertPos != -1)
                        {
                            existingRtf = existingRtf.Insert(insertPos, @"\red205\green92\blue92;");
                        }

                        // Change the color of the new version info
                        newVersionRtf = newVersionRtf.Replace(@"\cf1", @"\cf2");

                        // Append the new version info before the last closing brace
                        int lastBraceIndex = existingRtf.LastIndexOf('}');
                        if (lastBraceIndex > 0)
                        {
                            existingRtf = existingRtf.Insert(lastBraceIndex, newVersionRtf);
                        }

                        richTextBoxAbout.Rtf = existingRtf;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private void CheckBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxBlink.Checked)
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
                    if (listBoxComponentsSelectedLabels.Contains(overlay.ComponentLabel))
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
                    if (listBoxComponentsSelectedLabels.Contains(overlay.ComponentLabel))
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

        private void BlinkSelectedOverlays(bool state)
        {
            foreach (var overlayPanel in overlayPanelsList.Values)
            {
                foreach (var overlay in overlayPanel.Overlays)
                {
                    if (listBoxComponentsSelectedLabels.Contains(overlay.ComponentLabel))
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
                    if (listBoxComponentsSelectedLabels.Contains(overlay.ComponentLabel))
                    {
                        overlay.Highlighted = state;
                    }
                }
                overlayPanel.Invalidate();
            }
        }

        private void AdjustComponentCategoriesListBoxHeight()
        {
            int listBoxLocationEnd_org = listBoxCategories.Location.Y + listBoxCategories.Height;
            int itemHeight = listBoxCategories.ItemHeight;
            int itemCount = listBoxCategories.Items.Count;
            int borderHeight = listBoxCategories.Height - listBoxCategories.ClientSize.Height;
            listBoxCategories.Height = (itemHeight * itemCount) + borderHeight;
            int listBoxLocationEnd_new = listBoxCategories.Location.Y + listBoxCategories.Height;
            int diff =  listBoxLocationEnd_org - listBoxLocationEnd_new;
            if (diff > 0)
            {
                labelComponents.Location = new Point(labelComponents.Location.X, listBoxLocationEnd_new + 11);
                listBoxComponents.Location = new Point(listBoxComponents.Location.X, listBoxLocationEnd_new + 11 + 20);
                listBoxComponents.Height = listBoxComponents.Height + diff + 15;
            } else
            {
                diff = Math.Abs(diff); // reverse the negative to a positive integer
                labelComponents.Location = new Point(labelComponents.Location.X, listBoxLocationEnd_new + 11);
                listBoxComponents.Location = new Point(listBoxComponents.Location.X, listBoxLocationEnd_new + 11 + 20);
                listBoxComponents.Height = listBoxComponents.Height - diff + 15;
            }
        }     

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

            panelMain.DoubleBuffered(true);
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
            panelListAutoscroll.Layout += PanelListAutoscroll_Layout;
            this.Load += Form_Load;
            tabControl.Dock = DockStyle.Fill;

            this.FormClosing += Main_FormClosing;

            // Subscribe to the Paint event of the SplitContainer
            splitContainerSchematics.Paint += SplitContainer1_Paint;
        }

        // ---------------------------------------------------------------------
        // Load hardware data from Excel
        private void LoadExcelData()
        {
            DataStructure.GetAllData(classHardware);
        }

        // ---------------------------------------------------------------------
        // Populate combo boxes with loaded data
        private void PopulateComboBoxes()
        {
            foreach (Hardware hardware in classHardware)
            {
                comboBoxHardware.Items.Add(hardware.Name);
            }
            comboBoxHardware.SelectedIndex = 0;
            hardwareSelectedName = comboBoxHardware.SelectedItem.ToString();
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
                splitContainerSchematics.SplitterDistance = splitterPosition;
            }

            // Apply combo box selections
            if (int.TryParse(comboBox1Val, out int comboBox1Index) && comboBox1Index >= 0 && comboBox1Index < comboBoxHardware.Items.Count)
            {
                comboBoxHardware.SelectedIndex = comboBox1Index;
            }
            else
            {
                comboBoxHardware.SelectedIndex = 0;
            }

            if (int.TryParse(comboBox2Val, out int comboBox2Index) && comboBox2Index >= 0 && comboBox2Index < comboBoxBoard.Items.Count)
            {
                comboBoxBoard.SelectedIndex = comboBox2Index;
            }
            else
            {
                comboBoxBoard.SelectedIndex = 0;
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
            comboBoxHardware.SelectedIndexChanged += (s, e) =>
            {
                Configuration.SaveSetting("ComboBox1Index", comboBoxHardware.SelectedIndex.ToString());
            };

            comboBoxBoard.SelectedIndexChanged += (s, e) =>
            {
                Configuration.SaveSetting("ComboBox2Index", comboBoxBoard.SelectedIndex.ToString());
            };

            // Save splitter position
            splitContainerSchematics.SplitterMoved += (s, e) =>
            {
                Configuration.SaveSetting("SplitterPosition", splitContainerSchematics.SplitterDistance.ToString());
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
            panelBehindTab.Controls.Remove(panelMain);
            panelBehindTab.Controls.Add(panelMain);

            // Hide tabs, and show fullscreen panel
            tabControl.Visible = false;
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
            panelBehindTab.Controls.Remove(panelMain);
            splitContainerSchematics.Panel1.Controls.Add(panelMain);

            // Show again the tabs, and hide the fullscreen panel
            tabControl.Visible = true;
            //            AdjustPanelSchematicPanelsWidth();
            buttonFullscreen.Text = "Fullscreen";
            isFullscreen = false;
        }

        // ---------------------------------------------------------------------------
        // Event - form initialized, but not yet shown
        // ---------------------------------------------------------------------------

        private void Form_Load(object sender, EventArgs e)
        {
            string savedState = Configuration.GetSetting("WindowState", "Maximized");
            if (Enum.TryParse(savedState, out FormWindowState state) && state != FormWindowState.Minimized)
            {
                this.WindowState = state;
            }

            ApplySavedSettings();
            AttachConfigurationSaveEvents();
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

            boardSelectedName = comboBoxBoard.SelectedItem.ToString();

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


            // If no config was found (or empty):
            bool loaded = LoadSelectedCategories(); // attempt to select from config
            if (!loaded && listBoxCategories.Items.Count > 0)
            {
                // fallback: auto-select everything
                for (int i = 0; i < listBoxCategories.Items.Count; i++)
                {
                    listBoxCategories.SetSelected(i, true);
                }
            }

            InitializeComponentList(true);

            AdjustComponentCategoriesListBoxHeight();

            InitializeList();
            InitializeTabMain();
            PopulateRichTextBoxRessources(selectedBoard);
        }

        // ---------------------------------------------------------------------
        // Form events

        private void Form_ResizeBegin(object sender, EventArgs e)
        {
            isResizing = true;
        }

        private void Form_ResizeEnd(object sender, EventArgs e)
        {
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

        // ---------------------------------------------------------------------
        // Lists of components

        private void InitializeComponentCategories()
        {
            listBoxCategories.Items.Clear();

            var foundHardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var foundBoard = foundHardware?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (foundBoard != null)
            {
                foreach (ComponentBoard component in foundBoard.Components)
                {
                    if (!string.IsNullOrEmpty(component.Type) && !listBoxCategories.Items.Contains(component.Type))
                    {
                        listBoxCategories.Items.Add(component.Type);
                    }
                }
            }
        }

        private void InitializeComponentList(bool clearList = true)
        {
            listBoxComponents.Items.Clear();
            listBoxNameValueMapping.Clear();

            var foundHardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var foundBoard = foundHardware?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (foundBoard != null)
            {
                foreach (ComponentBoard comp in foundBoard.Components)
                {
                    if (listBoxCategories.SelectedItems.Contains(comp.Type))
                    {
                        string displayText = comp.Label;
                        displayText += comp.NameTechnical != "?" ? " | " + comp.NameTechnical : "";
                        displayText += comp.NameFriendly != "?" ? " | " + comp.NameFriendly : "";
                        listBoxComponents.Items.Add(displayText);
                        listBoxNameValueMapping[displayText] = comp.Label;

                        if (listBoxComponentsSelectedLabels.Contains(comp.Label))
                        {
                            int idx = listBoxComponents.Items.IndexOf(displayText);
                            listBoxComponents.SetSelected(idx, true);
                        }
                    }
                }
            }

            // Apply filter after initializing the component list
            FilterListBoxComponents();
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
            panelMain.Controls.Clear();
            overlayComponentsTab.Clear();
            overlayComponentsTabOriginalSizes.Clear();
            overlayComponentsTabOriginalLocations.Clear();

            // Build the arrays
            CreateOverlayArraysToTab();

            // Create scrolling container
            panelZoom = new CustomPanel
            {
                Size = new Size(panelMain.Width - panelListMain.Width - 25, panelMain.Height),
                AutoScroll = true,
                Dock = DockStyle.Fill
            };
            panelZoom.DoubleBuffered(true);
            panelMain.Controls.Add(panelZoom);

            // Create panelImage for the main picture
            panelImage = new Panel
            {
                Size = image.Size,
                BackgroundImage = image,
                BackgroundImageLayout = ImageLayout.Zoom,
                Dock = DockStyle.None
            };
            panelImage.DoubleBuffered(true);
            panelZoom.Controls.Add(panelImage);

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
            panelMain.Controls.Add(labelFile);
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
            panelMain.Controls.Add(labelComponent);
            labelComponent.BringToFront();

            // Finish up
            ResizeTabImage();
            panelZoom.CustomMouseWheel += panelZoom_MouseWheel;
            panelZoom.Resize += panelZoom_Resize;

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

            var hw = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var bd = hw?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (bd == null) return;

            int yPosition = 5;
            int availableWidth = panelListAutoscroll.ClientSize.Width - SystemInformation.VerticalScrollBarWidth;

            foreach (BoardFileOverlays file in bd.Files)
            {
                string path = Path.Combine(Application.StartupPath, "Data", hardwareSelectedFolder, boardSelectedFolder, file.FileName);
                Image image2 = Image.FromFile(path);

                Panel thumbnailContainer = new Panel
                {
                    Name = file.Name + "_container",
                    BorderStyle = BorderStyle.None,
                    Location = new Point(0, yPosition),
                    Padding = new Padding(3),
                    Margin = new Padding(0),
                    Width = availableWidth,
                    Height = 150
                };
                thumbnailContainer.DoubleBuffered(true);

                Label labelListFile = new Label
                {
                    Text = file.Name,
                    AutoSize = false,
                    Dock = DockStyle.Top,
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor = Color.Khaki,
                    ForeColor = Color.Black,
                    Font = new Font("Calibri", 9),
                    Padding = new Padding(2),
                    Height = 20
                };
                labelListFile.DoubleBuffered(true);
                thumbnailContainer.Controls.Add(labelListFile);

                Panel panelList2 = new Panel
                {
                    Name = file.Name,
                    BackgroundImage = Image.FromFile(path),
                    BackgroundImageLayout = ImageLayout.Zoom,
                    Dock = DockStyle.Fill,
                    Margin = new Padding(0)
                };
                panelList2.DoubleBuffered(true);

                OverlayPanel overlayPanelList = new OverlayPanel
                {
                    Dock = DockStyle.Fill
                };

                overlayPanelList.OverlayPanelMouseDown += (s, e2) =>
                {
                    if (e2.Button == MouseButtons.Left)
                        OnListImageLeftClicked(panelList2);
                };

                overlayPanelList.OverlayClicked += (s, e2) =>
                {
                    if (e2.MouseArgs.Button == MouseButtons.Left)
                        OnListImageLeftClicked(panelList2);
                };

                panelList2.Controls.Add(overlayPanelList);
                overlayPanelsList[file.Name] = overlayPanelList;
                overlayListZoomFactors[file.Name] = 1.0f;

                panelList2.BackgroundImage = Image.FromFile(path);
                panelList2.BackgroundImageLayout = ImageLayout.Zoom;

                thumbnailContainer.Controls.Add(panelList2);
                panelListAutoscroll.Controls.Add(thumbnailContainer);

                yPosition += thumbnailContainer.Height + 10;
            }

            panelListAutoscroll.AutoScroll = true;
            panelListAutoscroll.HorizontalScroll.Enabled = false;
            panelListAutoscroll.HorizontalScroll.Visible = false;

            AdjustImageSizes();
            DrawBorderInList();
            HighlightOverlays("list");
        }

        private void RefreshThumbnailLabels()
        {
            var hw = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var bd = hw?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (bd == null) return;

            foreach (var file in bd.Files)
            {
                // Now search for the container panel (instead of panel with file.Name)
                var container = panelListAutoscroll.Controls
                    .OfType<Panel>()
                    .FirstOrDefault(p => p.Name == file.Name + "_container");
                if (container == null) continue;

                // Find the label inside the container panel.
                var labelListFile = container.Controls.OfType<Label>().FirstOrDefault();
                if (labelListFile == null) continue;

                bool hasSelectedComponent = listBoxComponentsSelectedLabels.Any(selectedLabel =>
                {
                    var compBounds = file.Components.FirstOrDefault(c => c.Label == selectedLabel);
                    return compBounds != null && compBounds.Overlays != null && compBounds.Overlays.Count > 0;
                });

                if (hasSelectedComponent)
                {
                    labelListFile.Text = "* " + file.Name;
                    labelListFile.BackColor = Color.IndianRed;
                    labelListFile.ForeColor = Color.White;
                }
                else
                {
                    labelListFile.Text = file.Name;
                    labelListFile.BackColor = Color.Khaki;
                    labelListFile.ForeColor = Color.Black;
                }
            }
        }

        // 2) Call this new method at the end of your UpdateHighlights() method:

        private void AdjustImageSizes()
        {
            int scrollbarWidth = panelListAutoscroll.VerticalScroll.Visible
                ? SystemInformation.VerticalScrollBarWidth - 14
                : 0;
            int availableWidth = panelListAutoscroll.ClientSize.Width - scrollbarWidth;
            int yPosition = 5;

            foreach (Panel container in panelListAutoscroll.Controls.OfType<Panel>())
            {
                if (container.Controls.Count < 2) continue;
                Label lbl = container.Controls[0] as Label;
                Panel imagePanel = container.Controls[1] as Panel;
                if (imagePanel?.BackgroundImage == null) continue;

                float aspectRatio = (float)imagePanel.BackgroundImage.Height / imagePanel.BackgroundImage.Width;
                int newImageHeight = (int)(availableWidth * aspectRatio);

                imagePanel.Bounds = new Rectangle(0, lbl.Height, availableWidth, newImageHeight);
                container.Size = new Size(availableWidth, lbl.Height + newImageHeight);
                container.Location = new Point(0, yPosition);

                yPosition += container.Height + 10;

                float scaleFactor = (float)availableWidth / imagePanel.BackgroundImage.Width;
                string key = container.Name.Replace("_container", "");
                overlayListZoomFactors[key] = scaleFactor;
                if (overlayPanelsList.ContainsKey(key))
                {
                    overlayPanelsList[key].Bounds = imagePanel.ClientRectangle;
                }
            }

            panelListAutoscroll.AutoScrollMinSize = new Size(0, yPosition + 10);
            HighlightOverlays("list");
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
            foreach (Panel container in panelListAutoscroll.Controls.OfType<Panel>())
            {
                container.Paint -= Panel_Paint_Special;
                container.Paint += Panel_Paint_Special;
                container.Invalidate();
            }
        }

        private void Panel_Paint_Special(object sender, PaintEventArgs e)
        {
            Panel panel = (Panel)sender;
            string selectedContainer = imageSelectedName + "_container";
            if (panel.Name == selectedContainer)
            {
                float penWidth = 2;
                using (Pen pen = new Pen(Color.Red, penWidth))
                {
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    float offset = penWidth / 2;

                    e.Graphics.DrawRectangle(
                        pen,
                        offset,
                        offset,
                        panel.ClientSize.Width - penWidth,
                        panel.ClientSize.Height - penWidth
                    );
                }
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
            SaveSelectedCategories(); // Save the user’s chosen categories
            UpdateHighlights();
        }

        // "Clear Selection" button
        private void button1_Click(object sender, EventArgs e)
        {
            clearSelection();
            textBox1.Text = "";
        }

        private void clearSelection()
        {
            listBoxComponents.ClearSelected();
            listBoxComponentsSelectedLabels.Clear();
            UpdateHighlights();
        }

        // "Select All" button
        private void button2_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < listBoxComponents.Items.Count; i++)
            {
                listBoxComponents.SetSelected(i, true);
            }
            UpdateHighlights();
        }

        // ---------------------------------------------------------------------
        // comboBox events

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            comboBoxBoard.Items.Clear();
            hardwareSelectedName = comboBoxHardware.SelectedItem.ToString();

            var hw = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            if (hw != null)
            {
                foreach (var board in hw.Boards)
                {
                    comboBoxBoard.Items.Add(board.Name);
                }
                comboBoxBoard.SelectedIndex = 0;
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

            float xZoomFactor = (float)panelZoom.Width / image.Width;
            float yZoomFactor = (float)panelZoom.Height / image.Height;
            zoomFactor = Math.Min(xZoomFactor, yZoomFactor);

            panelImage.Size = new Size(
                (int)(image.Width * zoomFactor),
                (int)(image.Height * zoomFactor)
            );
            HighlightOverlays("tab");
        }

        private void panelZoom_Resize(object sender, EventArgs e)
        {
            if (!isResizedByMouseWheel)
            {
                ResizeTabImage();
            }
            isResizedByMouseWheel = false;
        }

        private void panelZoom_MouseWheel(object sender, MouseEventArgs e)
        {
            ControlUpdateHelper.BeginControlUpdate(panelZoom);

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
                    if (panelImage.Width > panelZoom.Width || panelImage.Height > panelZoom.Height)
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
                        e.X - panelZoom.AutoScrollPosition.X,
                        e.Y - panelZoom.AutoScrollPosition.Y
                    );

                    Point newScrollPosition = new Point(
                        (int)(mousePosition.X * (zoomFactor / oldZoomFactor)),
                        (int)(mousePosition.Y * (zoomFactor / oldZoomFactor))
                    );

                    // 4) Apply the new size
                    panelImage.Size = newSize;

                    // 5) Update the scroll position
                    panelZoom.AutoScrollPosition = new Point(
                        newScrollPosition.X - e.X,
                        newScrollPosition.Y - e.Y
                    );

                    // 6) Re‐highlight overlays (so they scale properly)
                    HighlightOverlays("tab");
                }
            }
            finally
            {
                ControlUpdateHelper.EndControlUpdate(panelZoom);
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
            foreach (BoardFileOverlays bf in bd.Files)
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

                    bool highlighted = listBoxComponentsSelectedLabels.Contains(overlayComponentsTab[i].Name);

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
                foreach (BoardFileOverlays bf in bd.Files)
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

                        bool highlighted = listBoxComponentsSelectedLabels.Contains(comp.Label);

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
            listBoxComponentsSelectedLabels.Clear();

            // Build a list of actual component labels from selected items
            foreach (var selectedItem in listBoxComponents.SelectedItems)
            {
                string displayText = selectedItem.ToString();
                if (listBoxNameValueMapping.TryGetValue(displayText, out string actualValue))
                {
                    listBoxComponentsSelectedLabels.Add(actualValue);
                }
            }

            HighlightOverlays("tab");
            HighlightOverlays("list");

            // Refresh the thumbnail labels to show asterisks
            RefreshThumbnailLabels();
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
                int index = listBoxComponents.FindString(key);
                if (index >= 0)
                {
                    // Make sure it's selected (force highlight)
                    if (!listBoxComponents.GetSelected(index))
                    {
                        listBoxComponents.SetSelected(index, true);
                    }
                }

                // Refresh the highlight overlays
                UpdateHighlights();

                // 2) SHOW the form for the clicked component
                var hardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
                var board = hardware?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
                var comp = board?.Components.FirstOrDefault(c => c.Label == labelClicked);
                if (comp != null)
                {
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
                int index = listBoxComponents.FindString(key);

                if (index >= 0)
                {
                    bool currentlySelected = listBoxComponents.GetSelected(index);
                    listBoxComponents.SetSelected(index, !currentlySelected);
                }
                else
                {
                    // If not in list, re-add it
                    foreach (var item in listBoxNameValueMapping)
                    {
                        if (item.Value == labelClicked)
                        {
                            listBoxComponents.Items.Add(item.Key);
                            int newIndex = listBoxComponents.FindString(item.Key);
                            listBoxComponents.SetSelected(newIndex, true);
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
                var hw = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
                var bd = hw?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
                var comp = bd?.Components.FirstOrDefault(c => c.Label == e.OverlayInfo.ComponentLabel);

                if (comp != null)
                {
                    labelComponent.Text = comp.Label;
                    labelComponent.Text += comp.NameTechnical != "?" ? " | " + comp.NameTechnical : "";
                    labelComponent.Text += comp.NameFriendly != "?" ? " | " + comp.NameFriendly : "";
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
                panelZoom.AutoScrollPosition = new Point(
                    -panelZoom.AutoScrollPosition.X - dx,
                    -panelZoom.AutoScrollPosition.Y - dy
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
            Debug.WriteLine(splitContainerSchematics.SplitterDistance);
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
            if (tabControl.SelectedTab.Text == "Schematics")
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
                checkBoxBlink.Checked = !checkBoxBlink.Checked;
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl.SelectedTab.Text == "Schematics")
            {
                buttonFullscreen.Enabled = true;
            }
            else
            {
                buttonFullscreen.Enabled = false;
            }
        }

        private void richTextBox_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.LinkText) { UseShellExecute = true });
        }

        private void SaveSelectedCategories()
        {
            // 1) Build a unique config key from hardware and board
            string configKey = $"SelectedCategories|{hardwareSelectedName}|{boardSelectedName}";

            // 2) Gather selected categories from listBox2
            var selectedCategories = listBoxCategories.SelectedItems
                .Cast<object>()
                .Select(item => item.ToString());

            // 3) Join them into a single string (e.g. CSV)
            string joined = string.Join(";", selectedCategories);

            // 4) Save to config
            Configuration.SaveSetting(configKey, joined);
        }

        private bool LoadSelectedCategories()
        {
            // e.g. "SelectedCategories|C128|310378"
            string configKey = $"SelectedCategories|{hardwareSelectedName}|{boardSelectedName}";
            Debug.WriteLine($"Looking for configKey = '{configKey}'");
            string joined = Configuration.GetSetting(configKey, "");

            if (string.IsNullOrEmpty(joined))
            {
                // No saved categories => return false so we can fallback
                return false;
            }

            listBoxCategories.ClearSelected();
            string[] categories = joined.Split(';');
            foreach (string cat in categories)
            {
                int idx = listBoxCategories.Items.IndexOf(cat);
                if (idx >= 0)
                {
                    listBoxCategories.SetSelected(idx, true);
                }
            }
            return true;
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            Configuration.SaveSetting("WindowState", this.WindowState.ToString());
        }

        private void PopulateRichTextBoxRessources(Board selectedBoard)
        {
            if (selectedBoard.BoardLinks == null || !selectedBoard.BoardLinks.Any())
            {
                richTextBoxRessources.Clear();
                return;
            }

            // Get the two datasets
            var groupedLinks = selectedBoard.BoardLinks.GroupBy(link => link.Category);
            var groupedLocalFiles = selectedBoard.BoardLocalFiles.GroupBy(file => file.Category);

            StringBuilder rtfBuilder = new StringBuilder();

            rtfBuilder.Append(@"{\rtf1\ansi\ansicpg1252");

            // Local files
            rtfBuilder.Append(@"\b Local files:\b0");
            rtfBuilder.Append(@"\par\par");
            foreach (var group in groupedLocalFiles)
            {
                rtfBuilder.Append(@"\b ");
                rtfBuilder.Append(group.Key);
                rtfBuilder.Append(@":\b0\par");
                foreach (var file in group)
                {
                    string filePath = Path.Combine(Application.StartupPath, "Data", hardwareSelectedFolder, boardSelectedFolder, file.Datafile);
                    rtfBuilder.Append(@"\pard    \'95 ");
                    rtfBuilder.Append($@"{{\field{{\*\fldinst HYPERLINK ""file:///{filePath.Replace(@"\", @"\\")}""}}{{\fldrslt {file.Name}}}}}");
                    rtfBuilder.Append(@"\par");
                }
                rtfBuilder.Append(@"\par");
            }

            // Links
            rtfBuilder.Append(@"\b Links:\b0");
            rtfBuilder.Append(@"\par\par");

            foreach (var group in groupedLinks)
            {
                rtfBuilder.Append(@"\b ");
                rtfBuilder.Append(group.Key);
                rtfBuilder.Append(@":\b0\par");

                foreach (var link in group)
                {
                    rtfBuilder.Append(@"\pard    \'95 ");
                    //rtfBuilder.Append($@"{{\field{{\*\fldinst HYPERLINK ""{link.Url}""}}{{\fldrslt {link.Name}}}}}");
                    //rtfBuilder.Append($@"{{\field{{\*\fldinst{{ HYPERLINK ""{link.Url}"" }}}}{{\fldrslt {link.Name}}}}}");
                    //rtfBuilder.Append($@"{{\field{{\*\fldinst{{ HYPERLINK ""{link.Url}"" }}}}{{\fldrslt {{{link.Name}}}}}}}");
                    //rtfBuilder.Append($@"{{\field{{\*\fldinst{{HYPERLINK ""{link.Url}""}}}}{{\fldrslt{{\ul\cf1 {link.Name}}}}}}}");
                    //rtfBuilder.Append($@"{{\field{{\*\fldinst{{ HYPERLINK ""{link.Url}"" }}}}{{\fldrslt{{{link.Name}}}}}}}");
                    //rtfBuilder.Append($@"{{\field{{\*\fldinst{{ HYPERLINK ""{link.Url}"" }}}}{{\fldrslt{{\ul {link.Name}}}}}}}");
                    //                    rtfBuilder.Append($@"{{\field{{\*\fldinst{{ HYPERLINK ""{link.Url}"" }}}}{{\fldrslt{{{link.Name}}}}}}}");
//                    rtfBuilder.Append("{\\rtf1\\ansi\\ansicpg1252\\cocoartf1038\\cocoasubrtf350\r\n{\\fonttbl\\f0\\fnil\\fcharset0 Calibri;}\r\n{\\colortbl;\\red255\\green255\\blue255;}\r\n\\paperw11900\\paperh16840\\vieww12000\\viewh13860\\viewkind0\r\n\\pard\\tx560\\tx1120\\tx1680\\tx2240\\tx2800\\tx3360\\tx3920\\tx4480\\tx5040\\tx5600\\tx6160\\tx6720\\ql\\qnatural\\pardirnatural\r\n\r\n\\f0\\fs22 \\cf0 ");
                    rtfBuilder.Append("{\\field{\\*\\fldinst{HYPERLINK \"" + link.Url + "\"}}{\\fldrslt " + link.Name + "}}");
                    rtfBuilder.Append(@"\par");
                }

                rtfBuilder.Append(@"\par");
            }

            rtfBuilder.Append("}");
            richTextBoxRessources.Rtf = rtfBuilder.ToString();

            /*
            string link2 = "https://whatever.dk";
            string name2 = "My Link";
            StringBuilder rtf = new StringBuilder();
            rtf.Append(@"{\rtf1\ansi");
            rtf.Append(@"{\fonttbl\f0\fnil\fcharset0 Calibri;}");
            rtf.Append(@"{\colortbl ;\red0\green0\blue255;}");
            rtf.Append(@"\viewkind4\uc1\pard\f0\fs20 ");
            rtf.Append($@"{{\field{{\*\fldinst HYPERLINK ""{link2}""}}{{\fldrslt{{\ul\cf1 {name2}}}}}}}");
            rtf.Append(@"\par}");
            */

            /*
            string link3 = "https://dennis.dk";
            string name3 = "My Link";
            StringBuilder rtf = new StringBuilder();
            rtf.Append("{\\rtf1\\ansi\\ansicpg1252\\cocoartf1038\\cocoasubrtf350\r\n{\\fonttbl\\f0\\fnil\\fcharset0 Calibri;}\r\n{\\colortbl;\\red255\\green255\\blue255;}\r\n\\paperw11900\\paperh16840\\vieww12000\\viewh13860\\viewkind0\r\n\\pard\\tx560\\tx1120\\tx1680\\tx2240\\tx2800\\tx3360\\tx3920\\tx4480\\tx5040\\tx5600\\tx6160\\tx6720\\ql\\qnatural\\pardirnatural\r\n\r\n\\f0\\fs22 \\cf0 ");
            rtf.Append("Here are some start text, ");
            rtf.Append("{\\field{\\*\\fldinst{HYPERLINK \""+ link3 +"\"}}{\\fldrslt "+ name3 +"}}");
            rtf.Append(". Here are some final text\\\r\n}");
            richTextBoxRessources.Rtf = rtf.ToString();
            */

            /*
            string link1 = "https://dennis.dk";
            string name1 = "My Link 1";
            string link2 = "https://example.com";
            string name2 = "My Link 2";

            StringBuilder rtf = new StringBuilder();
            rtf.Append("{\\rtf1\\ansi\\ansicpg1252\\deff0{\\fonttbl{\\f0\\fswiss Helvetica;}}");
            rtf.Append("{\\colortbl ;\\red0\\green0\\blue255;}"); // Define hyperlink color

            rtf.Append("\\viewkind4\\uc1\\pard\\fs20 Here are some start text, ");

            rtf.Append(" {\\field{\\*\\fldinst { HYPERLINK \"" + link1 + "\" }}{\\fldrslt \\ul\\cf1 " + name1 + "}} ");
            rtf.Append(" and ");
            rtf.Append(" {\\field{\\*\\fldinst { HYPERLINK \"" + link2 + "\" }}{\\fldrslt \\ul\\cf1 " + name2 + "}} ");

            rtf.Append(". Here are some final text\\par}");
            */

            StringBuilder rtf = new StringBuilder();

            rtf.Append("{\\rtf1\\ansi\\ansicpg1252\\deff0\\nouicompat\\deflang1033{\\fonttbl{\\f0\\fswiss\\fprq2\\fcharset0 Calibri; } {\\f1\\fnil\\fcharset0 Calibri; } {\\f2\\fnil\\fcharset2 Symbol; } }");
            rtf.Append("{\\colortbl;\\red0\\green0\\blue255;\\red5\\green99\\blue193; }");
            rtf.Append("{\\*\\generator Riched20 10.0.19041}\\viewkind4\\uc1");
            rtf.Append("\\pard\\widctlpar\\sa160\\sl252\\slmult1\\kerning2\\f0\\fs22\\lang1030 Header 1:\\par");
            rtf.Append("");
            rtf.Append("\\pard{\\pntext\\f2\\'B7\\tab}{\\*\\pn\\pnlvlblt\\pnf2\\pnindent0{\\pntxtb\\'B7}}\\widctlpar\\fi-360\\li720\\sa160\\sl252\\slmult1 {{\\field{\\*\\fldinst{HYPERLINK \"https://link1\"}}{\\fldrslt{\\ul\\cf1\\cf2\\ul Link 1}}}}\\f0\\fs22\\par");
            rtf.Append("{\\pntext\\f2\\'B7\\tab}{{\\field{\\*\\fldinst{HYPERLINK \"https://link2\"}}{\\fldrslt{\\ul\\cf1\\cf2\\ul Link 2}}}}\\f0\\fs22\\par");
            rtf.Append("");
            rtf.Append("\\pard\\widctlpar\\sa160\\sl252\\slmult1 Header 2:\\par");
            rtf.Append("");
            rtf.Append("\\pard{\\pntext\\f2\\'B7\\tab}{\\*\\pn\\pnlvlblt\\pnf2\\pnindent0{\\pntxtb\\'B7}}\\widctlpar\\fi-360\\li720\\sa160\\sl252\\slmult1 {{\\field{\\*\\fldinst{HYPERLINK \"https://link3\"}}{\\fldrslt{\\ul\\cf1\\cf2\\ul Link 3}}}}\\f0\\fs22\\par");
            rtf.Append("");
            rtf.Append("\\pard\\sa200\\sl276\\slmult1\\kerning0\\f1\\lang6\\par");
            rtf.Append("}");

            richTextBoxRessources.Rtf = rtf.ToString();
            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            await webView21.EnsureCoreWebView2Async(null);

            // Handle navigation events
            webView21.CoreWebView2.NavigationStarting += (sender, args) =>
            {
                Debug.WriteLine("NavigationStarting: " + args.Uri);
                if (args.Uri.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    args.Cancel = true;
                    string localPath = new Uri(args.Uri).LocalPath;
                    Process.Start(new ProcessStartInfo(localPath) { UseShellExecute = true });
                }
            };

            // HTML
            string htmlContent = @"
                <html>
                <head>
                <meta charset='UTF-8'>
                <title>Local File Test</title>
                </head>
                <body>
                <h1>Try opening local files:</h1>
                <ul>
                <li><a href='file:///C:\\GlDifxCmd.log'>Open local log file</a></li>
                <li><a href='https://www.google.com'>Google</a></li>
                </ul>
                </body>
                </html>";

            webView21.NavigateToString(htmlContent);
        }

        private void richTextBoxRessources_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            try
            {
                if (e.LinkText.StartsWith("http://") || e.LinkText.StartsWith("https://"))
                {
                    Process.Start(new ProcessStartInfo(e.LinkText) { UseShellExecute = true });
                }
                else if (e.LinkText.StartsWith("file:///"))
                {
                    string localPath = e.LinkText.Replace("file:///", "").Replace("/", "\\");
                    Process.Start(new ProcessStartInfo(localPath) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open link: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void richTextBoxEx1_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            Debug.WriteLine("Link clicked: " + e.LinkText);
        }

        // What is this?
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    }

    // -------------------------------------------------------------------------
    // Class definitions

    // "Hardware" is read from the very first "Data.xlsx" Excel file (level 1).
    // It contains a list of all associated boards and their respective data files.
    public class Hardware
    {
        public string Name { get; set; }
        public string Folder { get; set; }
        public string Datafile { get; set; }
        public List<Board> Boards { get; set; }
    }

    // "Board" is read from level 2 of the Excel data files
    public class Board
    {
        public string Name { get; set; }
        public string Folder { get; set; }
        public string Datafile { get; set; }
        public List<BoardFileOverlays> Files { get; set; }
        public List<ComponentBoard> Components { get; set; }
        public List<BoardLink> BoardLinks { get; set; }
        public List<BoardLocalFiles> BoardLocalFiles { get; set; }
    }

    // "BoardFileOverlays" contains all overlay info and bounds per image
    public class BoardFileOverlays
    {
        public string Name { get; set; }
        public string FileName { get; set; }
        public string HighlightColorTab { get; set; }
        public string HighlightColorList { get; set; }
        public int HighlightOpacityTab { get; set; }
        public int HighlightOpacityList { get; set; }
        public List<ComponentBounds> Components { get; set; }
    }

    // "BoardLink" contains all web links per board
    public class BoardLink
    {
        public string Category { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
    }

    // "BoardLocalFiles" contains all local file links per board
    public class BoardLocalFiles
    {
        public string Category { get; set; }
        public string Name { get; set; }
        public string Datafile { get; set; }
    }

    // Helper-classes
    
    public class ComponentBoard
    {
        public string Label { get; set; }
        public string NameTechnical { get; set; }
        public string NameFriendly { get; set; }
        public string Type { get; set; }
        public string ImagePinout { get; set; }
        public string OneLiner { get; set; }
        public string Description { get; set; }
        public List<ComponentLocalFiles> LocalFiles { get; set; }
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

    public class ComponentLocalFiles
    {
        public string Name { get; set; }
        public string FileName { get; set; }
    }

    public class ComponentLinks
    {
        public string Name { get; set; }
        public string Url { get; set; }
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