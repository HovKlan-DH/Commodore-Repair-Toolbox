using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;


namespace Commodore_Repair_Toolbox
{
    public partial class Main : Form
    {
        // Default values
        private string buildType = ""; // Debug|Release
        private string onlineAvailableVersion = ""; // will be empty, if no newer version available
        private string crtPage = "https://commodore-repair-toolbox.dk";
        private string crtPageAutoUpdate = "/auto-update/";
        private string crtPageFeedback = "/feedback-app/";

        // HTML code for all tabs using "WebView2" component for content
        private string htmlForTabs = @"
            <style>
            body { padding: 10px; font-family: Calibri, sans-serif; font-size: 11pt; }
            h1 { font-size: 14pt; }
            h2 { font-size: 11pt; padding: 0px; margin: 0px; }
            ul { margin: 0px; }
            a { color: #5181d0; }
            </style>
            <script>
            document.addEventListener('click', function(e) {
                // Return if we should not react on this class
                if (e.target.tagName.toLowerCase() === 'td' && 
                    e.target.classList.contains('doNotFocusFilter') && 
                    e.target.hasAttribute('data-compLabel')) {
                    return; // Do nothing if this specific element is clicked
                }
                // Send an event for clicking anywhere in the HTML code
                window.chrome.webview.postMessage('htmlClick');
            });
            </script>
        ";

        // Reference to the popup/info form
        private FormComponent componentInfoPopup = null;

        // Blinking of components
        private Timer blinkTimer;
        private bool blinkState = false;

        // Fullscreen mode
        private string windowState = "Maximized";
        private bool isFullscreen = false;
        private FormWindowState formPreviousWindowState;
        private FormBorderStyle formPreviousFormBorderStyle;
        private Rectangle previousBoundsForm;
        private Rectangle previousBoundsPanelBehindTab;
        private Rectangle previousBoundsFullscreenButton;

        // Main panel (left side) + image
        private CustomPanel panelZoom;
        private Panel panelImageMain;
        private Image image;

        // "Main" schematics (left-side of SplitContainer)
        private Label labelFile;
        private Label labelComponent;
        public static OverlayPanel overlayPanel;
        private Dictionary<string, Control> overlayLabelMap = new Dictionary<string, Control>();

        // Thumbnails (right-side of SplitContainer)
        private Dictionary<string, OverlayPanel> overlayPanelsList = new Dictionary<string, OverlayPanel>();
        private Dictionary<string, float> overlayListZoomFactors = new Dictionary<string, float>();
        private Color labelImageBgClr = Color.Khaki;
        private Color labelImageTxtClr = Color.Black;
        private Color labelImageHasElementsBgClr = Color.SteelBlue;
        private Color labelImageHasElementsTxtClr = Color.White;

        // Resizing of window/schematic
        private bool isResizedByMouseWheel = false;

        // Data loaded from Excel
        public static List<Hardware> classHardware = new List<Hardware>();

        // Overlays for main image
        private List<PictureBox> overlayComponentsTab = new List<PictureBox>();
        private Dictionary<int, Point> overlayComponentsTabOriginalLocations = new Dictionary<int, Point>();
        private Dictionary<int, Size> overlayComponentsTabOriginalSizes = new Dictionary<int, Size>();

        // Selected entries in the components list
        private List<string> listBoxComponentsSelectedText = new List<string>();

        // Version information
        private string versionThis = "";
        private string versionOnline = "";
        private string versionOnlineTxt = "";

        // Current user selection
        public static string hardwareSelectedName;
        public static string boardSelectedName;
        private string boardSelectedFilename;
        public static string schematicSelectedName;
        private string schematicSelectedFile;
        public static float zoomFactor = 1.0f;
        private Point overlayPanelLastMousePos = Point.Empty;

        // Misc
        private Dictionary<Control, EventHandler> clickEventHandlers = new Dictionary<Control, EventHandler>();
        private TabPage previousTab;
        private int thumbnailSelectedBorderWidth = 3;
        private Timer windowMoveStopTimer = new Timer();
        private Point windowLastLocation;
        bool thumbnailsSameWidth = false;
        int thumbnailsWidth = 0;

        // Polyline
        //        private PolylinesManagement polylinesManagement = new PolylinesManagement();
        private PolylinesManagement polylinesManagement;
        private int zoomLevel = 1;
        private int panelTracesVisibleHeight = 0;
        private bool isPanelTracesVisible = false;


        // ###########################################################################################
        // Main form constructor.
        // ###########################################################################################

        public Main()
        {
            InitializeComponent();

            polylinesManagement = new PolylinesManagement(this);

            // Get build type
#if DEBUG
            buildType = "Debug";
#else
                buildType = "Release";
#endif

            // Create or overwrite the debug output file
            CreateDebugOutputFile();

            // Enable double-buffering for smoother UI rendering
            EnableDoubleBuffering();

            // Get application versions - both this one and the one online
            GetAssemblyVersion();
            GetOnlineVersion();

            // Initialize relevant "WebView2" components (used in tab pages)
            InitializeTabHelp();
            InitializeTabAbout();

            // Load all files (Excel and configuration)
            LoadExcelData();
            LoadConfigFile();

            // Attach "form load" event, which is triggered just before form is shown
            Load += Form_Load;
        }


        // ###########################################################################################
        // Event: Form load.
        // Triggered just before form is shown
        // ###########################################################################################

        private void Form_Load(object sender, EventArgs e)
        {
            windowState = Configuration.GetSetting("WindowState", "Maximized");
            if (Enum.TryParse(windowState, out FormWindowState state) && state != FormWindowState.Minimized)
            {
                this.WindowState = state;
            }

            tabControl.Dock = DockStyle.Fill;
            panelLabelsVisible.Visible = false;

            // Initialize the blink timer
            InitializeBlinkTimer();

            Shown += Form_Shown;
        }

        private void Form_Shown(object sender, EventArgs e)
        {
            Debug.WriteLine("Form_Shown()");

            ApplyConfigSettings();

            // Attach various event handles
            AttachEventHandlers();

            AttachConfigurationSaveEvents();

            SetupNewBoard();
            //LoadSelectedImage();

            UpdateComponentList("Form_Shown");

            // Set initial focus to "textBoxFilterComponents"
            textBoxFilterComponents.Focus();

            // Initialize "label11" with the initial zoom level
            label11.Text = $"{zoomLevel}";

            StartDrawingPolylines();
            PopulatePolylineVisibilityPanel();
        }

        private void Form_Closing(object sender, FormClosingEventArgs e)
        {
            PolylinesManagement.SavePolylinesToConfig();
        }


        // ###########################################################################################
        // Get the assembly version.
        // Will transform assembly information into a text string.
        // ###########################################################################################

        private void GetAssemblyVersion()
        {
            try
            {
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
                versionThis = (date + " " + buildTypeTmp + rev).Trim();
            }
            catch (Exception ex)
            {
                DebugOutput("EXCEPTION in \"GetAssemblyVersion()\":");
                DebugOutput(ex.ToString());
            }
        }


        // ###########################################################################################
        // Get the version available online (newest version).
        // ###########################################################################################

        private void GetOnlineVersion()
        {
            try
            {
                using (WebClient webClient = new WebClient())
                {
                    ServicePointManager.Expect100Continue = true;
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
                    webClient.Headers.Add("user-agent", "CRT " + versionThis);

                    // Include some control POST data
                    var postData = new System.Collections.Specialized.NameValueCollection
                    {
                        { "control", "CRT" }
                    };

                    // Send the POST data to the server
                    byte[] responseBytes = webClient.UploadValues(crtPage + crtPageAutoUpdate, postData);

                    // Convert the "response bytes" to a human readable string
                    onlineAvailableVersion = Encoding.UTF8.GetString(responseBytes);

                    if (onlineAvailableVersion.Substring(0, 7) == "Version")
                    {
                        onlineAvailableVersion = onlineAvailableVersion.Substring(9);
                        if (onlineAvailableVersion != versionThis)
                        {
                            tabAbout.Text = "About*";
                            versionOnline = onlineAvailableVersion;
                            versionOnlineTxt = "<font color='IndianRed'>";
                            versionOnlineTxt += $"There is a newer version available online: <b>" + versionOnline + @"</b ><br />";
                            versionOnlineTxt += "View the <i>Changelog</i> and download the new version from here, <a href='https://github.com/HovKlan-DH/Commodore-Repair-Toolbox/releases' target='_blank'> https://github.com/HovKlan-DH/Commodore-Repair-Toolbox/releases</a><br />";
                            versionOnlineTxt += "<br />";
                            versionOnlineTxt += "</font>";
                        }
                        else
                        {
                            versionOnline = "";
                        }
                    }
                    else
                    {
                        tabAbout.Text = "About*";
                        versionOnlineTxt = "<font color='IndianRed'>";
                        versionOnlineTxt += "<hr>";
                        versionOnlineTxt += "ERROR:<br />";
                        versionOnlineTxt += $"It was not possible to check for a newer version, as the server connection to <a href='https://commodore-repair-toolbox.dk' target='_blank'>https://commodore-repair-toolbox.dk</a> cannot be established - please check your connectivity.<br />The exact recieved HTTP error is:<br /><br /><b>{onlineAvailableVersion}</b>";
                        versionOnlineTxt += "<hr>";
                        versionOnlineTxt += "<br />";
                        versionOnlineTxt += "</font>";
                    }
                }
            }
            catch (Exception ex)
            {
                DebugOutput("EXCEPTION in \"GetOnlineVersion()\":");
                DebugOutput(ex.ToString());
                tabAbout.Text = "About*";
                versionOnlineTxt = "<font color='IndianRed'>";
                versionOnlineTxt += "<hr>";
                versionOnlineTxt += "ERROR:<br />";
                versionOnlineTxt += $"It was not possible to check for a newer version, as the server connection to <a href='https://commodore-repair-toolbox.dk' target='_blank'>https://commodore-repair-toolbox.dk</a> cannot be established - please check your connectivity.<br />The exact recieved HTTP error is:<br /><br /><b>{ex.Message}</b>";
                versionOnlineTxt += "<hr>";
                versionOnlineTxt += "<br />";
                versionOnlineTxt += "</font>";
            }
        }


        // ###########################################################################################
        // Load all Excel data, and have it ready for usage.
        // ###########################################################################################

        private void LoadExcelData()
        {
            DataStructure.GetAllData(classHardware);
        }


        // ###########################################################################################
        // Load the saved settings from the configuration file (if any).
        // ###########################################################################################

        private void LoadConfigFile()
        {
            try
            {
                Configuration.LoadConfig();
            }
            catch (Exception ex)
            {
                DebugOutput("EXCEPTION in \"LoadConfigFile()\":");
                DebugOutput(ex.ToString());
            }
        }


        // ###########################################################################################
        // Apply saved configuration settings to controls.
        // ###########################################################################################

        private void ApplyConfigSettings()
        {
            // Set default values first
            string defaultSelectedHardware = classHardware.FirstOrDefault()?.Name;
            string defaultSelectedBoard = classHardware.FirstOrDefault(h => h.Name == defaultSelectedHardware)?.Boards?.FirstOrDefault()?.Name;
            string defaultShowLabel = "True";
            string defaultShowTechnicalName = "False";
            string defaultShowFriendlyName = "False";
            string defaultShowLabelsHeight = "95";
            string defaultShowTraces = "True";
            string defaultShowTracesHeight = "0";

            // Load saved settings from configuration file - or set default if none exists
            string selectedHardwareVal = Configuration.GetSetting("HardwareSelected", defaultSelectedHardware);
            string selectedBoardVal = Configuration.GetSetting("BoardSelected", defaultSelectedBoard);
            string selectedShowLabel = Configuration.GetSetting("ShowLabel", defaultShowLabel);
            string selectedShowTechnicalName = Configuration.GetSetting("ShowTechnicalName", defaultShowTechnicalName);
            string selectedShowFriendlyName = Configuration.GetSetting("ShowFriendlyName", defaultShowFriendlyName);
            string showLabelsHeight = Configuration.GetSetting("ShowLabelsHeight", defaultShowLabelsHeight);
            string showTraces = Configuration.GetSetting("ShowTraces", defaultShowTraces);
            string showTracesHeight = Configuration.GetSetting("ShowTracesHeight", defaultShowTracesHeight);
            string userEmail = Configuration.GetSetting("UserEmail", "");

            textBoxEmail.Text = userEmail; // set email address in "Feedback" tab

            // Populate all hardware in combobox - and select
            foreach (Hardware hardware in classHardware)
            {
                comboBoxHardware.Items.Add(hardware.Name);
            }
            int indexHardware = comboBoxHardware.Items.IndexOf(selectedHardwareVal);
            if (indexHardware == -1)
            {
                indexHardware = comboBoxHardware.Items.IndexOf(defaultSelectedHardware);
                hardwareSelectedName = defaultSelectedHardware;
            }
            comboBoxHardware.SelectedIndex = indexHardware;
            hardwareSelectedName = comboBoxHardware.SelectedItem.ToString();
            textBox5.Text = ConvertStringToLabel(hardwareSelectedName); // feedback info           

            // Populate all boards in combobox, based on selected hardware - and select
            var hw = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            if (hw != null)
            {
                foreach (var board in hw.Boards)
                {
                    comboBoxBoard.Items.Add(board.Name);
                }
                int indexBoard = comboBoxBoard.Items.IndexOf(selectedBoardVal);
                if (indexBoard == -1)
                {
                    indexBoard = comboBoxBoard.Items.IndexOf(defaultSelectedBoard);
                    boardSelectedName = defaultSelectedBoard;
                }
                comboBoxBoard.SelectedIndex = indexBoard;
                boardSelectedName = comboBoxBoard.SelectedItem.ToString();
                textBox1.Text = ConvertStringToLabel(boardSelectedName); // feedback info           
            }

            // Set the "Labels visible" checkboxes
            checkBox1.Checked = selectedShowLabel == "True" ? true : false;
            checkBox2.Checked = selectedShowTechnicalName == "True" ? true : false;
            checkBox3.Checked = selectedShowFriendlyName == "True" ? true : false;

            // .. and the height for its panel
            panelLabelsVisible.Height = Convert.ToInt32(showLabelsHeight);

            panelTracesVisibleHeight = Convert.ToInt32(showTracesHeight);
            isPanelTracesVisible = bool.TryParse(showTraces, out bool result) && result;
        }


        // ###########################################################################################
        // Create/overwrite the debug output logfile
        // ###########################################################################################

        private void CreateDebugOutputFile()
        {
            string filePath = Path.Combine(Application.StartupPath, "Commodore-Repair-Toolbox.log");
            using (StreamWriter writer = new StreamWriter(filePath, false))
            {
                writer.WriteLine("Debug output file created [" + DateTime.Now + "]");
            }
        }

        public static void DebugOutput(string text)
        {
            string filePath = Path.Combine(Application.StartupPath, "Commodore-Repair-Toolbox.log");
            using (StreamWriter writer = new StreamWriter(filePath, true))
            {
                writer.WriteLine(text);
                Debug.WriteLine(text);
            }
        }


        // ###########################################################################################
        // Attach necessary event handlers.
        // ###########################################################################################

        private void AttachEventHandlers()
        {
            ResizeBegin += Form_ResizeBegin;
            ResizeEnd += Form_ResizeEnd;
            comboBoxHardware.SelectedIndexChanged += comboBoxHardware_SelectedIndexChanged;
            comboBoxBoard.SelectedIndexChanged += comboBoxBoard_SelectedIndexChanged;
            checkBoxBlink.CheckedChanged += CheckBoxBlink_CheckedChanged;
            textBoxFilterComponents.TextChanged += TextBoxFilterComponents_TextChanged;
            splitContainerSchematics.Paint += SplitContainer1_Paint;
            comboBoxHardware.DropDownClosed += ComboBox_DropDownClosed;
            comboBoxBoard.DropDownClosed += ComboBox_DropDownClosed;
            listBoxCategories.SelectedIndexChanged += ListBoxCategories_SelectedIndexChanged;
            AttachClickEventsToFocusFilterComponents(this);
            textBoxEmail.TextChanged += TextBoxEmail_TextChanged;
            tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
            splitContainerSchematics.SplitterMoved += SplitContainer1_SplitterMoved;
            listBoxComponents.SelectedIndexChanged += listBoxComponents_SelectedIndexChanged;
            windowMoveStopTimer.Interval = 200;
            windowMoveStopTimer.Tick += MoveStopTimer_Tick;
            Move += Form_Move;
            FormClosing += Form_Closing;
        }


        // ###########################################################################################
        // Attach event handlers for saving configuration settings.
        // ###########################################################################################
        private void AttachConfigurationSaveEvents()
        {
            // Selection of new hardware
            comboBoxHardware.SelectedIndexChanged += (s, e) =>
            {
                Configuration.SaveSetting("HardwareSelected", hardwareSelectedName);
            };

            // Selection of new board
            comboBoxBoard.SelectedIndexChanged += (s, e) =>
            {
                Configuration.SaveSetting("BoardSelected", boardSelectedName);
            };
        }


        // ###########################################################################################
        // Attach a click event to all controls with a name, and have them focus
        // "textBoxFilterComponents" so it is ready for text input for filtering.
        // ###########################################################################################

        private void AttachClickEventsToFocusFilterComponents(Control parent)
        {
            if (!(parent is ComboBox))
            {
                EventHandler handler = (s, e) => textBoxFilterComponents.Focus();
                parent.Click += handler;
                clickEventHandlers[parent] = handler;
            }
            foreach (Control child in parent.Controls)
            {
                if (child != null)
                {
                    AttachClickEventsToFocusFilterComponents(child);
                }
            }
        }

        private void RemoveClickEventsToFocusFilterComponents()
        {
            foreach (var kvp in clickEventHandlers)
            {
                kvp.Key.Click -= kvp.Value;
            }
            clickEventHandlers.Clear();
        }


        // ###########################################################################################
        // Events: Resize.
        // ###########################################################################################

        private void Form_ResizeBegin(object sender, EventArgs e)
        {
            SuspendLayout();
        }

        private void Form_ResizeEnd(object sender, EventArgs e)
        {
            ResizeTabImage();
            ResumeLayout();
            ReadaptThumbnails();
        }

        private void Form_Move(object sender, EventArgs e)
        {
            // Disable event for "splitter moved", as it otherwise ruins the saved data
            splitContainerSchematics.SplitterMoved -= SplitContainer1_SplitterMoved;

            // Enable/reset the time to detect when the movemen has stopped
            windowLastLocation = this.Location;
            windowMoveStopTimer.Stop();
            windowMoveStopTimer.Start();
        }


        // ###########################################################################################
        // What happens when window movement has stopped
        // ###########################################################################################

        private void MoveStopTimer_Tick(object sender, EventArgs e)
        {
            if (this.Location == windowLastLocation)
            {
                windowMoveStopTimer.Stop();

                // Save the new "window state" and load and apply the splitter position for the new state
                windowState = this.WindowState.ToString();
                Configuration.SaveSetting("WindowState", windowState);
                LoadAndApplySplitterPosition();

                ReadaptThumbnails();

                // We can now reenable the event for "splitter moved"
                splitContainerSchematics.SplitterMoved += SplitContainer1_SplitterMoved;
            }
        }


        // ###########################################################################################
        // Initialize and update the tab for "Overview".
        // Will load new content from board data file.
        // ###########################################################################################

        private async void UpdateTabOverview(Board selectedBoard)
        {
            if (webView2Overview.CoreWebView2 == null)
            {
                await webView2Overview.EnsureCoreWebView2Async(null);
            }

            var foundHardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var foundBoard = foundHardware?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);

            string revisionDate = "";
            if (foundBoard != null)
            {
                revisionDate = foundBoard.RevisionDate;
            }

            string htmlContent = @"
                <html>
                <head>
                <meta charset='UTF-8'>
                <script>
                    document.addEventListener('click', function(e) {
                    var target = e.target;
                    <!-- Event for 'Open URL' -->
                    if (target.tagName.toLowerCase() === 'a' && !target.href.startsWith('file://')) {
                        e.preventDefault();
                        window.chrome.webview.postMessage('openUrl:' + target.href);
                    }
                    <!-- Event for 'Open file' -->
                    if (target.tagName.toLowerCase() === 'a' && target.href.startsWith('file://')) {
                        e.preventDefault();
                        window.chrome.webview.postMessage('openFile:' + target.href);
                    }
                    <!-- Event for 'Open component' -->
                    if (target.matches('[data-compLabel]')) {
                        var compLabel = target.getAttribute('data-compLabel');
                        window.chrome.webview.postMessage('openComp:' + compLabel);
                    }
                    <!-- Event for 'Close popup' -->
                    if (!(e.target.tagName.toLowerCase() === 'a' && e.target.href.startsWith('file://')) &&
                        !e.target.matches('[data-compLabel]')) {
                        window.chrome.webview.postMessage('htmlClick');
                    }
                    });
                </script>
                <style>
                body { overflow-x: hidden; }
                table { border-collapse: collapse; }
                thead {
                    position: sticky;
                    top: 0;
                    background-color: #DDD;
                    z-index: 20;
                }
                tbody tr:hover { background-color: #f2f2f2; }
                th { 
                    text-align: left; 
                    color: black;
                }
                th, td { 
                    padding: 4px 10px; 
                    font-size: 11pt;
                }
                td[data-compLabel] {
                    color: #0645AD;
                    cursor: pointer;
                }
                td[data-compLabel]:hover { color: #0B0080; }
                .tooltip-link {
                    position: relative;
                    cursor: pointer;
                    text-decoration: none;
                }
                .tooltip-link::after {
                    content: attr(data-title);
                    position: absolute;
                    bottom: 100%;
                    left: 50%;
                    transform: translateX(-50%);
                    background: black;
                    color: white;
                    padding: 4px 8px;
                    border-radius: 4px;
                    white-space: nowrap;
                    opacity: 0;
                    pointer-events: none;
                    transition: opacity 0.1s ease-in-out;
                    font-size: 15px;
                    z-index: 10;
                }
                .tooltip-link:hover::after { opacity: 1; }
                </style>
                </head>
                <body>
                " + htmlForTabs + @"
                <h1>Overview of components</h1>
                This is a overview of all components for the selected board. Components listed, follows what is visible in the ""Component list"".<br /><br />
                The data for this board has the revision date: <b>" + revisionDate + @"</b><br /><br />
            ";

            if (foundBoard != null)
            {
                if (foundBoard?.Components != null)
                {
                    // Filter components based on what is visible in "listBoxComponents"
                    var visibleComponents = listBoxComponents.Items.Cast<string>().ToHashSet();

                    htmlContent += "<table width='100%' border='1'>";
                    htmlContent += "<thead>";
                    htmlContent += "<tr>";
                    htmlContent += "<th valign='bottom'>Type</th>";
                    htmlContent += "<th valign='bottom'>Component</th>";
                    htmlContent += "<th valign='bottom'>Technical name</th>";
                    htmlContent += "<th valign='bottom'>Friendly name</th>";
                    htmlContent += "<th valign='bottom'>Short descr.</th>";
                    htmlContent += "<th valign='bottom'>Long descr.</th>";
                    htmlContent += "<th valign='bottom'>Local files</th>";
                    htmlContent += "<th valign='bottom'>Web links</th>";
                    htmlContent += "</tr>";
                    htmlContent += "</thead>";
                    htmlContent += "<tbody>";

                    foreach (BoardComponents comp in foundBoard.Components)
                    {
                        if (!visibleComponents.Contains(comp.NameDisplay)) continue; // skip component if it is not visible in "listBoxComponents"

                        string compType = comp.Type;
                        string compLabel = comp.Label;
                        string compNameTechnical = comp.NameTechnical;
                        string compNameFriendly = comp.NameFriendly;
                        string compDescrShort = comp.OneLiner;
                        string compDescrLong = comp.Description;
                        compDescrLong = compDescrLong.Replace("\n", "<br />");

                        compNameFriendly = compNameFriendly.Replace("?", "");

                        htmlContent += "<tr>";

                        htmlContent += $"<td valign='top'>{compType}</td>";
                        htmlContent += $"<td valign='top' data-compLabel='{compLabel}' class='doNotFocusFilter'>{compLabel}</td>";
                        htmlContent += $"<td valign='top'>{compNameTechnical}</td>";
                        htmlContent += $"<td valign='top'>{compNameFriendly}</td>";
                        htmlContent += $"<td valign='top'>{compDescrShort}</td>";
                        htmlContent += $"<td valign='top'>{compDescrLong}</td>";

                        // Include component local files
                        htmlContent += "<td valign='top'>";
                        if (comp.LocalFiles != null && comp.LocalFiles.Count > 0)
                        {
                            int counter = 1;
                            foreach (ComponentLocalFiles file in comp.LocalFiles)
                            {
                                // Translate the relative path into an absolute path
                                string filePath = Path.GetFullPath(Path.Combine(Application.StartupPath, file.FileName));
                                string fileUri = new Uri(filePath).AbsoluteUri;

                                htmlContent += "<a href='" + fileUri + "' class='tooltip-link' data-title='" + file.Name + "' target='_blank'>#" + counter + "</a> ";
                                counter++;
                            }
                        }
                        htmlContent += "</td>";

                        // Include component links
                        htmlContent += "<td valign='top'>";
                        if (comp.ComponentLinks != null && comp.ComponentLinks.Count > 0)
                        {
                            int counter = 1;
                            foreach (ComponentLinks link in comp.ComponentLinks)
                            {
                                htmlContent += "<a href='" + link.Url + "' target='_blank' class='tooltip-link' data-title='" + link.Name + "'>#" + counter + "</a> ";
                                counter++;
                            }
                        }
                        htmlContent += "</td>";

                        htmlContent += "</tr>";
                    }

                    htmlContent += "</tbody>";
                    htmlContent += "</table>";
                }
            }

            htmlContent += "<br />";
            htmlContent += "</body>";
            htmlContent += "</html >";

            // Make sure we detach any current event handles, before we add a new one
            webView2Overview.CoreWebView2.WebMessageReceived -= WebView2_WebMessageReceived;
            webView2Overview.CoreWebView2.WebMessageReceived += WebView2_WebMessageReceived;

            webView2Overview.NavigateToString(htmlContent);
        }


        private void WebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var message = e.TryGetWebMessageAsString();

            CloseComponentPopup();

            // Open URL
            if (message.StartsWith("openUrl:"))
            {
                string url = message.Substring("openUrl:".Length);
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }

            // Open file
            else if (message.StartsWith("openFile:"))
            {
                string fileUrl = message.Substring("openFile:".Length);
                if (File.Exists(new Uri(fileUrl).LocalPath))
                {
                    Process.Start(new ProcessStartInfo(new Uri(fileUrl).LocalPath) { UseShellExecute = true });
                }
                else
                {
                    DebugOutput("File [" + new Uri(fileUrl).LocalPath + "] does not exists!");
                }
            }

            // Open component
            else if (message.StartsWith("openComp:"))
            {
                string compName = message.Substring("openComp:".Length);
                var foundHardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
                var foundBoard = foundHardware?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
                BoardComponents selectedComp = foundBoard?.Components.FirstOrDefault(c => c.Label == compName);
                if (selectedComp != null)
                {
                    componentInfoPopup = new FormComponent(selectedComp);
                    componentInfoPopup.Show(this);
                    componentInfoPopup.BringToFront();
                }
            }

            // Click anywhere in HTML
            else if (message == "htmlClick")
            {
                textBoxFilterComponents.Focus();
                // NOP (it has this event to close the component popup)
            }
        }

        private void CloseComponentPopup()
        {
            if (componentInfoPopup != null && !componentInfoPopup.IsDisposed)
            {
                componentInfoPopup.Close();
                componentInfoPopup = null;
            }
        }


        // ###########################################################################################
        // Initialize and update the tab for "Ressources".
        // Will load new content from board data file.
        // ###########################################################################################

        private async void UpdateTabRessources(Board selectedBoard)
        {
            if (webView2Ressources.CoreWebView2 == null)
            {
                await webView2Ressources.EnsureCoreWebView2Async(null);
            }

            string htmlContent = @"
                <html>
                <head>
                <meta charset='UTF-8'>
                <script>
                document.addEventListener('click', function(e) {
                    var target = e.target;
                    if (target.tagName.toLowerCase() === 'a' && target.href.startsWith('file://')) {
                        e.preventDefault();
                        window.chrome.webview.postMessage('openFile:' + target.href);
                    }
                });
                </script>
                </head>
                <body>
                " + htmlForTabs + @"
                <h1>Ressources for troubleshooting and information</h1><br />
            ";

            // Get the two datasets, or "null"
            var groupedLocalFiles = selectedBoard.BoardLocalFiles?.GroupBy(file => file.Category);
            var groupedLinks = selectedBoard.BoardLinks?.GroupBy(link => link.Category);

            // Local files
            if (groupedLocalFiles != null && groupedLocalFiles.Any())
            {
                htmlContent += "<h1>Local files</h1>";
                foreach (var group in groupedLocalFiles)
                {
                    htmlContent += "<h2>" + group.Key + "</h2>";
                    htmlContent += "<ul>";

                    foreach (var file in group)
                    {
                        // Translate the relative path into an absolute path
                        string filePath = Path.GetFullPath(Path.Combine(Application.StartupPath, file.Datafile));
                        string fileUri = new Uri(filePath).AbsoluteUri;

                        htmlContent += "<li><a href='" + fileUri + "' target='_blank'>" + file.Name + "</a></li>";
                    }
                    htmlContent += "</ul>";
                    htmlContent += "<br />";
                }
            }
            else
            {
                htmlContent += "<font color='IndianRed'>Could not read [Board local files] data from data file!</a></font><br />";
            }

            // Web links
            if (groupedLinks != null && groupedLinks.Any())
            {
                htmlContent += "<h1>Links</h1>";
                foreach (var group in groupedLinks)
                {
                    htmlContent += "<h2>" + group.Key + "</h2>";
                    htmlContent += "<ul>";

                    foreach (var link in group)
                    {
                        htmlContent += "<li><a href='" + link.Url + "' target='_blank'>" + link.Name + "</a></li>";
                    }
                    htmlContent += "</ul>";
                    htmlContent += "<br />";
                }
            }
            else
            {
                htmlContent += "<font color='IndianRed'>Could not read [Board links] data from data file!</a></font><br />";
            }

            htmlContent += "</body>";
            htmlContent += "</html >";

            // Make sure we detach any current event handles, before we add a new one
            webView2Ressources.CoreWebView2.WebMessageReceived -= WebView2_WebMessageReceived; // detach first
            webView2Ressources.CoreWebView2.WebMessageReceived += WebView2_WebMessageReceived; // attach again
            webView2Ressources.CoreWebView2.NewWindowRequested -= WebView2OpenUrl_NewWindowRequested; // detach first
            webView2Ressources.CoreWebView2.NewWindowRequested += WebView2OpenUrl_NewWindowRequested; // attach again

            webView2Ressources.NavigateToString(htmlContent);
        }

        private void WebView2OpenUrl_NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs args)
        {
            // Open URL in default web browser
            args.Handled = true;
            Process.Start(new ProcessStartInfo(args.Uri) { UseShellExecute = true });

            // Set focus back to the "textBoxFilterComponents"
            textBoxFilterComponents.Focus();
        }


        // ###########################################################################################
        // Initialize the tab for "Help".
        // ###########################################################################################

        private async void InitializeTabHelp()
        {
            if (webView2Help.CoreWebView2 == null)
            {
                await webView2Help.EnsureCoreWebView2Async(null);
            }

            string htmlContent = @"
                <html>
                <head>
                <meta charset='UTF-8'>
                </head>
                <body>
                " + htmlForTabs + @"
                <h1>Help for application usage</h1><br />

                <b>""Schematics"" tab</b>:<br />
                <br />

                <ul>
                <li>Mouse functions:</li>
                <ul>
                <li><b>Left-click</b> on a component will show a information popup</li>
                <li><b>Right-click</b> on a component will toggle highlight</li>
                <li><b>Right-click</b> and <b>Hold</b> will pan the image</li>
                <li><b>Scrollwheel</b> will zoom in/out</li>
                </ul>
                </ul>
                <br />

                <ul>
                <li>Keyboard functions:</li>
                <ul>
                <li><b>F11</b> will toggle fullscreen</li>
                <li><b>ESCAPE</b> will exit fullscreen</li>
                <li><b>ENTER</b> will toggle blinking for selected components</li>
                <li><b>ALT</b>+<b>A</b> will select all components in ""Component list""</li>
                <li><b>ALT</b>+<b>C</b> will clear all selections and show all components in ""Component list""</li>
                <li>Start typing anywhere to filter component list</li>
                <li>Filtering supports multi-word/character searching:</li>
                <ul>
                <li><span style='background:lightgrey;'>&nbsp;cpu 2 85&nbsp;</span> will find e.g. the component <span style='background:lightgrey;'>&nbsp;U6 | CPU | 8502&nbsp;</span></li>
                </ul>
                </ul>
                </ul>
                <br />

                <ul>
                <li>Component selection:</li>
                <ul>
                <li>When a component is selected, then it will visualize if component is part of thumbnail in list-view:</li>
                <ul>
                <li>Appending an asterisk/* as first character in thumbnail label</li>
                <li>Background color of thumbnail label changes to blue</li>
                </ul>
                <li>You cannot highlight a component in image, if its component category is unselected</li>
                </ul>
                </ul>
                <br />
                
                <ul>
                <li>Labels visible:</li>
                <ul>
                <li>If no components are selected for the specific schematic, then the panel with checkboxes will not be shown</li>
                <li>The panel can be toggled minimized/maximized with the ""M"" button</li>
                <li>When only one checkbox is selected, then it will replace whitespaces in text with new-lines to condense the text</li>
                </ul>
                </ul>
                <br />

                <b>""Component information popup""</b>:<br />
                <br />

                If multiple images are available for the selected component, then it will show ""Image 1 of x"" in the top right corner.<br />
                <br />

                <ul>
                <li>Mouse functions:</li>
                <ul>
                <li><b>Click</b> in the image area will change back to first image (typical the pinout)</li>
                <li><b>Scrollwheel</b> will change image, if multiple images</li>
                </ul>
                </ul>
                <br />

                <ul>
                <li>Keyboard functions:</li>
                <ul>
                <li><b>ARROW KEYS</b> will change image, if multiple images</li>
                <li><b>SPACE</b> will change back to first image (typical the pinout)</li>
                <li><b>ESCAPE</b> or <b>ENTER</b> will close popup</li>
                </ul>
                </ul>
                <br />

                <b>Misc</b>:<br />
                <br />

                <ul>
                <li>How-to add or update your own data:</li>
                <ul>
                <li>View <a href='https://github.com/HovKlan-DH/Commodore-Repair-Toolbox?tab=readme-ov-file#software-used' target='_blank'>GitHub Documentation</a></li>
                </ul>
                </ul>
                <br />

                <ul>
                <li>Report a problem or comment something from either of these places:</li>
                <ul>
                <li>Through the ""Feedback"" tab</li>
                <li>Through the <a href='https://github.com/HovKlan-DH/Commodore-Repair-Toolbox/issues' target='_blank'>GitHub Issues</a></li>
                </ul>
                </ul>
                <br />
                
                When there is a newer version available online, it will be marked with an asterisk (*) in the ""About"" tab.<br />
                Then navigate to the tab and view or download the new version.<br />

                </body>
                </html>
            ";

            // Make sure we detach any current event handles, before we add a new one
            webView2Help.CoreWebView2.WebMessageReceived -= WebView2_WebMessageReceived; // detach first
            webView2Help.CoreWebView2.WebMessageReceived += WebView2_WebMessageReceived; // attach again
            webView2Help.CoreWebView2.NewWindowRequested -= WebView2OpenUrl_NewWindowRequested; // detach first
            webView2Help.CoreWebView2.NewWindowRequested += WebView2OpenUrl_NewWindowRequested; // attach again

            webView2Help.NavigateToString(htmlContent);
        }


        // ###########################################################################################
        // Initialize the tab for "About".
        // ###########################################################################################

        private async void InitializeTabAbout()
        {
            if (webView2About.CoreWebView2 == null)
            {
                await webView2About.EnsureCoreWebView2Async(null);
            }

            string htmlContent = @"
                <html>
                <head>
                <meta charset='UTF-8'>
                </head>
                <body>
                " + htmlForTabs + @"
                <h1>Commodore Repair Toolbox</h1><br />

                You are running version <b>" + versionThis + @"</b> (64-bit)<br />
                <br />

                " + versionOnlineTxt + @"

                All programming done by Dennis Helligsø (dennis@commodore-repair-toolbox.dk).<br />
                <br />

                Visit official project home page at <a href='https://github.com/HovKlan-DH/Commodore-Repair-Toolbox' target='_blank'>https://github.com/HovKlan-DH/Commodore-Repair-Toolbox</a><br />
                <br />
                
                </body>
                </html>
            ";

            // Make sure we detach any current event handles, before we add a new one
            webView2About.CoreWebView2.WebMessageReceived -= WebView2_WebMessageReceived; // detach first
            webView2About.CoreWebView2.WebMessageReceived += WebView2_WebMessageReceived; // attach again
            webView2About.CoreWebView2.NewWindowRequested -= WebView2OpenUrl_NewWindowRequested; // detach first
            webView2About.CoreWebView2.NewWindowRequested += WebView2OpenUrl_NewWindowRequested; // attach again

            webView2About.NavigateToString(htmlContent);
        }


        // ###########################################################################################
        // Initialize the tab for "About".
        // ###########################################################################################

        private void InitializeBlinkTimer()
        {
            blinkTimer = new Timer();
            blinkTimer.Interval = 500;
            blinkTimer.Tick += BlinkTimer_Tick;
        }


        // ###########################################################################################
        // What happens when a combobox is closed.
        // Applicable for "Hardware" or "Board" comboboxes.
        // ###########################################################################################

        private void ComboBox_DropDownClosed(object sender, EventArgs e)
        {
            textBoxFilterComponents.Text = "";
            textBoxFilterComponents.Focus();
        }



        // ###########################################################################################
        // Do an update of the component list, taking selection and filtering in to consideration.
        // ###########################################################################################

        private void UpdateComponentSelection()
        {
            // Debug
#if DEBUG
            StackTrace stackTrace = new StackTrace();
            StackFrame callerFrame = stackTrace.GetFrame(1);
            MethodBase callerMethod = callerFrame.GetMethod();
            string callerName = callerMethod.Name;
            Debug.WriteLine("[UpdateComponentSelection] called from [" + callerName + "]");
#endif

            var listBoxComponentsSelectedClone = listBoxComponents.SelectedItems.Cast<object>().ToList(); // create a list of selected components

            foreach (var item in listBoxComponents.Items)
            {
                if (listBoxComponentsSelectedClone.Contains(item))
                {
                    AddSelectedComponentIfNotInList(item.ToString());
                }
                else
                {
                    RemoveSelectedComponentIfInList(item.ToString());
                }
            }

            UpdateShowOfSelectedComponents();

            // Update overlays
            ShowOverlaysAccordingToComponentList();
        }


        private void UpdateComponentList(string from)
        {
#if DEBUG
            StackTrace stackTrace = new StackTrace();
            StackFrame callerFrame = stackTrace.GetFrame(1);
            MethodBase callerMethod = callerFrame.GetMethod();
            string callerName = callerMethod.Name;
            Debug.WriteLine("[UpdateComponentList] called from [" + callerName + "]");
#endif

            // Decouple the event handler to avoid continuous recycling
            listBoxComponents.SelectedIndexChanged -= listBoxComponents_SelectedIndexChanged;

            var listBoxComponentsSelectedClone = listBoxComponents.SelectedItems.Cast<object>().ToList(); // create a list of selected components
            string filterText = textBoxFilterComponents.Text.ToLower();
            string[] searchTerms = filterText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); // Split filter text into terms

            // Deselect all components if we have an empty filter search
            if (from == "TextBoxFilterComponents_TextChanged" && searchTerms.Length == 0)
            {
                listBoxComponentsSelectedClone.Clear();
                listBoxComponentsSelectedText.Clear();
            }

            // Convert the selected items to a "HashSet" for faster lookup
            var selectedComponents = new HashSet<string>(listBoxComponentsSelectedText);

            listBoxComponents.BeginUpdate(); // suspend redrawing this listBox while updating it
            listBoxComponents.Items.Clear();

            // Walk through all components for selected hardware and board
            var foundHardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var foundBoard = foundHardware?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (foundBoard != null && foundBoard.Components != null)
            {
                foreach (BoardComponents comp in foundBoard.Components)
                {
                    string componentCategory = comp.Type;
                    string componentDisplay = comp.NameDisplay;

                    if (listBoxCategories.SelectedItems.Contains(componentCategory))
                    {
                        // Check if the component matches all search terms
                        bool matchesAllTerms = searchTerms.Length == 0 || searchTerms.All(term => componentDisplay.ToLower().Contains(term));

                        if (matchesAllTerms)
                        {
                            listBoxComponents.Items.Add(componentDisplay);

                            if (listBoxComponentsSelectedClone.Contains(componentDisplay) || (from == "TextBoxFilterComponents_TextChanged" && searchTerms.Length > 0))
                            {
                                if (!selectedComponents.Contains(componentDisplay))
                                {
                                    AddSelectedComponentIfNotInList(componentDisplay);
                                }
                                int index = listBoxComponents.Items.IndexOf(componentDisplay);
                                if (index >= 0)
                                {
                                    listBoxComponents.SetSelected(index, true);
                                }
                            }
                            else
                            {
                                RemoveSelectedComponentIfInList(componentDisplay);
                            }
                        }
                        else
                        {
                            RemoveSelectedComponentIfInList(componentDisplay);
                        }
                    }
                    else
                    {
                        RemoveSelectedComponentIfInList(componentDisplay);
                    }
                }
            }

            listBoxComponents.EndUpdate(); // resume redrawing of this specific listBox

            UpdateShowOfSelectedComponents();
            ShowOverlaysAccordingToComponentList();
            UpdateTabOverview(GetSelectedBoardClass());

            listBoxComponents.SelectedIndexChanged += listBoxComponents_SelectedIndexChanged;
        }

        private void AddSelectedComponentIfNotInList(string componentDisplay)
        {
            if (!listBoxComponentsSelectedText.Contains(componentDisplay))
            {
                listBoxComponentsSelectedText.Add(componentDisplay);
            }
        }

        private void RemoveSelectedComponentIfInList(string componentDisplay)
        {
            if (listBoxComponentsSelectedText.Contains(componentDisplay))
            {
                listBoxComponentsSelectedText.Remove(componentDisplay);
            }
        }

        private void listBoxComponents_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateComponentSelection();
        }

        private void UpdateShowOfSelectedComponents()
        {
            if (listBoxComponentsSelectedText.Count > 0)
            {
                labelComponents.Text = $"Component list ({listBoxComponentsSelectedText.Count} selected)";
            }
            else
            {
                labelComponents.Text = "Component list";
            }
        }


        // ###########################################################################################
        // Handling highlighting of overlays for "Main" and thumbnail images.
        // ###########################################################################################

        private void HighlightOverlays(string scope)
        {
            // Debug
#if DEBUG
            StackTrace stackTrace = new StackTrace();
            StackFrame callerFrame = stackTrace.GetFrame(1);
            MethodBase callerMethod = callerFrame.GetMethod();
            string callerName = callerMethod.Name;
            Debug.WriteLine("[HighlightOverlays(" + scope + ")] called from [" + callerName + "]");
#endif

            var hw = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var bd = hw?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (bd == null) return;

            // Convert the selected items to a "HashSet" for faster lookup
            var selectedComponents = new HashSet<string>(listBoxComponentsSelectedText);

            // Main
            if (scope == "tab")
            {
                // Draw overlays on the main image
                if (overlayPanel == null) return;
                var bf = bd?.Files?.FirstOrDefault(f => f.Name == schematicSelectedName);
                if (bf == null) return;

                DisposeAllControls(overlayPanel);
                overlayPanel.Overlays.Clear();

                Color colorZoom = Color.FromName(bf.HighlightColorTab);
                int opacityZoom = bf.HighlightOpacityTab;

                bool hasHighlightedOverlays = false;

                for (int i = 0; i < overlayComponentsTab.Count; i++)
                {
                    Rectangle rect = new Rectangle(
                        (int)(overlayComponentsTabOriginalLocations[i].X * zoomFactor),
                        (int)(overlayComponentsTabOriginalLocations[i].Y * zoomFactor),
                        (int)(overlayComponentsTabOriginalSizes[i].Width * zoomFactor),
                        (int)(overlayComponentsTabOriginalSizes[i].Height * zoomFactor)
                    );

                    // Find component "label" from component "display name"
                    string componentLabel = bd.Components.FirstOrDefault(cb => cb.NameDisplay == overlayComponentsTab[i].Name)?.Label ?? "";
                    string componentTechName = bd.Components.FirstOrDefault(cb => cb.NameDisplay == overlayComponentsTab[i].Name)?.NameTechnical ?? "";
                    string componentFriendlyName = bd.Components.FirstOrDefault(cb => cb.NameDisplay == overlayComponentsTab[i].Name)?.NameFriendly ?? "";

                    // Check if the component is selected in component list
                    bool highlighted = selectedComponents.Contains(overlayComponentsTab[i].Name);

                    overlayPanel.Overlays.Add(new OverlayInfo
                    {
                        Bounds = rect,
                        Color = colorZoom,
                        Opacity = opacityZoom,
                        Highlighted = highlighted,
                        ComponentLabel = componentLabel,
                        ComponentDisplay = overlayComponentsTab[i].Name
                    });

                    // Create a label, if the component is highlighted
                    if (highlighted)
                    {
                        hasHighlightedOverlays = true;

                        string labelText = checkBox1.Checked ? ConvertStringToLabel(componentLabel) : "";
                        labelText += checkBox2.Checked && componentTechName != "" ? Environment.NewLine + ConvertStringToLabel(componentTechName) : "";
                        labelText += checkBox3.Checked && componentFriendlyName != "" ? Environment.NewLine + ConvertStringToLabel(componentFriendlyName) : "";

                        // Overwrite the label text, if we hae only one checkbox enabled
                        labelText = checkBox1.Checked && !checkBox2.Checked && !checkBox3.Checked ? ConvertStringToLabel(componentLabel.Replace(" ", Environment.NewLine)) : labelText;
                        labelText = !checkBox1.Checked && checkBox2.Checked && !checkBox3.Checked ? ConvertStringToLabel(componentTechName.Replace(" ", Environment.NewLine)) : labelText;
                        labelText = !checkBox1.Checked && !checkBox2.Checked && checkBox3.Checked ? ConvertStringToLabel(componentFriendlyName.Replace(" ", Environment.NewLine)) : labelText;

                        // Weird command but it will remove empty newlines at beginning and end
                        labelText = string.Join(Environment.NewLine, labelText.Split(new[] { Environment.NewLine }, StringSplitOptions.None).Where(line => !string.IsNullOrWhiteSpace(line)));

                        // Add a component label, if one of the checkboxes are checked
                        if (checkBox1.Checked || checkBox2.Checked || checkBox3.Checked)
                        {
                            Label label = new Label
                            {
                                Name = "Label for component",
                                Text = labelText,
                                AutoSize = true,
                                BackColor = Color.Khaki,
                                ForeColor = Color.Black,
                                Font = new Font("Calibri", 9, FontStyle.Bold),
                                BorderStyle = BorderStyle.FixedSingle,
                                TextAlign = ContentAlignment.MiddleCenter,
                                Enabled = false,
                                Tag = componentLabel,
                            };
                            label.DoubleBuffered(true);

                            // Calculate the label center/middle position within the rectangle
                            int labelX = rect.X + (rect.Width - label.PreferredWidth) / 2;
                            int labelY = rect.Y + (rect.Height - label.PreferredHeight) / 2;
                            label.Location = new Point(labelX, labelY);

                            // Have a mapping table, as this isfaster than seaching all controls for a specific name
                            overlayLabelMap[componentLabel] = label;

                            overlayPanel.Controls.Add(label);
                        }
                    }
                }
                overlayPanel.Invalidate();

                // Ensure the "Labels visible" panel is visible
                if (hasHighlightedOverlays)
                {
                    // Add the label panel to "panelMain"
                    panelMain.Controls.Add(panelLabelsVisible);
                    int visibleHeight = panelZoom.ClientRectangle.Height;
                    panelLabelsVisible.Location = new Point(0, visibleHeight - panelLabelsVisible.Height - 2); // bottom-left corner of the visible area
                    panelLabelsVisible.BringToFront();
                    panelLabelsVisible.Visible = true;
                }
                else
                {
                    panelLabelsVisible.Visible = false;
                }
            }

            // Thumbnail list
            else if (scope == "list")
            {
                if (bd.Files != null)
                {
                    // Draw overlays on each thumbnail
                    foreach (BoardOverlays bo in bd.Files)
                    {
                        if (!overlayPanelsList.ContainsKey(bo.Name)) continue;

                        OverlayPanel listPanel = overlayPanelsList[bo.Name];
                        listPanel.Overlays.Clear();

                        Color colorList = Color.FromName(bo.HighlightColorList);
                        int opacityList = bo.HighlightOpacityList;
                        float listZoom = overlayListZoomFactors[bo.Name];

                        if (bo?.Components != null)
                        {
                            foreach (var comp in bo.Components)
                            {
                                if (comp.Overlays == null) continue;

                                // Find component "display name"
                                string componentDisplay = bd.Components
                                    .FirstOrDefault(cb => cb.Label == comp.Label)?.NameDisplay ?? "";

                                // Check if the component is selected in component list
                                bool highlighted = selectedComponents.Contains(componentDisplay);

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
                                        ComponentLabel = comp.Label,
                                        ComponentDisplay = componentDisplay
                                    });
                                }
                            }
                        }
                        listPanel.Invalidate();
                    }
                }
            }
        }


        // ###########################################################################################
        // Filtering of components.
        // Dividing string with whitespaces will do a multi-word search, where the
        // order is not imporant.
        // ###########################################################################################

        private void TextBoxFilterComponents_TextChanged(object sender, EventArgs e)
        {
            UpdateComponentList("TextBoxFilterComponents_TextChanged");
        }


        private void ListBoxCategories_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Debug
#if DEBUG
            StackTrace stackTrace = new StackTrace();
            StackFrame callerFrame = stackTrace.GetFrame(1);
            MethodBase callerMethod = callerFrame.GetMethod();
            string callerName = callerMethod.Name;
            Debug.WriteLine("[ListBoxCategories_SelectedIndexChanged] called from [" + callerName + "]");
#endif

            SaveSelectedCategories();
            UpdateComponentList("ListBoxCategories_SelectedIndexChanged");
        }


        // ###########################################################################################
        // Save the selected categories.
        // ###########################################################################################              

        private void SaveSelectedCategories()
        {
            // 1) Build a unique config key from hardware and board
            string configKey = $"SelectedCategories|{hardwareSelectedName}|{boardSelectedName}";

            // 2) Gather selected categories from "listBoxCategories"
            var selectedCategories = listBoxCategories.SelectedItems
                .Cast<object>()
                .Select(item => item.ToString());

            string joined = string.Join(";", selectedCategories);

            Configuration.SaveSetting(configKey, joined);
        }


        // ###########################################################################################
        // Load the selected categories from configuration file.
        // ###########################################################################################

        private bool LoadSelectedCategories()
        {
            // Debug
#if DEBUG
            StackTrace stackTrace = new StackTrace();
            StackFrame callerFrame = stackTrace.GetFrame(1);
            MethodBase callerMethod = callerFrame.GetMethod();
            string callerName = callerMethod.Name;
            Debug.WriteLine("[LoadSelectedCategories] called from [" + callerName + "]");
#endif

            // Get "SelectedCategories" from configuration file
            string configKey = $"SelectedCategories|{hardwareSelectedName}|{boardSelectedName}";
            string joined = Configuration.GetSetting(configKey, "");

            if (string.IsNullOrEmpty(joined)) return false;

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


        // ###########################################################################################
        // Blink handling.
        // ###########################################################################################

        private void CheckBoxBlink_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxBlink.Checked)
            {
                BlinkTimer_Tick(null, null); // perform the first blink immediately
                blinkTimer.Start();
            }
            else
            {
                blinkTimer.Stop();
                ReHighlightSelectedComponents(); // make sure the components will get highlighted again after end blinking
            }
        }

        private void BlinkTimer_Tick(object sender, EventArgs e)
        {
            BlinkSelectedOverlays(blinkState);
            blinkState = !blinkState;
        }

        private void BlinkSelectedOverlays(bool state)
        {
            SuspendDrawing(panelImageMain);

            // Convert the selected items to a "HashSet" for faster lookup
            var selectedComponents = new HashSet<string>(listBoxComponentsSelectedText);

            // Handle the main/schematic image
            if (overlayPanel != null)
            {
                foreach (var overlay in overlayPanel.Overlays)
                {
                    if (selectedComponents.Contains(overlay.ComponentDisplay))
                    {
                        overlay.Highlighted = state;

                        if (overlayLabelMap.TryGetValue(overlay.ComponentLabel, out var label))
                        {
                            label.Visible = state;
                        }
                    }
                }
            }

            ResumeDrawing(panelImageMain);

            // Handle the thumbnails
            foreach (var overlayPanel2 in overlayPanelsList.Values)
            {
                foreach (var overlay in overlayPanel2.Overlays)
                {
                    if (selectedComponents.Contains(overlay.ComponentDisplay))
                    {
                        overlay.Highlighted = state;
                    }
                }
                overlayPanel2.Invalidate();
            }
        }

        private void ReHighlightSelectedComponents()
        {
            ShowOverlaysAccordingToComponentList();
        }


        // ###########################################################################################
        // Handle input of email address in "Feedback" tab.
        // ###########################################################################################

        private void TextBoxEmail_TextChanged(object sender, EventArgs e)
        {
            string email = textBoxEmail.Text;
            Configuration.SaveSetting("UserEmail", email);
        }


        // ###########################################################################################
        // Changing tab.
        // ###########################################################################################

        private void TabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (previousTab == tabFeedback && tabControl.SelectedTab != tabFeedback)
            {
                // Changed TO tabFeedback
                textBoxFilterComponents.TextChanged += TextBoxFilterComponents_TextChanged;
                AttachClickEventsToFocusFilterComponents(this);
                textBoxFilterComponents.Focus();
            }
            else if (previousTab != tabFeedback && tabControl.SelectedTab == tabFeedback)
            {
                // Changed away FROM tabFeedback
                textBoxFilterComponents.TextChanged -= TextBoxFilterComponents_TextChanged;
                RemoveClickEventsToFocusFilterComponents();
                textBoxFeedback.Focus();
            }

            // Make sure to resize thumbnails, in case some resizing has happend in between
            if (tabControl.SelectedTab == tabSchematics)
            {
                ReadaptThumbnails();
            }

            previousTab = tabControl.SelectedTab;
        }


        // ###########################################################################################
        // Repaint the borders for all thumbnails (selected thumbnail will get marked)
        // ###########################################################################################

        private void DrawBorderInList()
        {
            foreach (Panel container in panelThumbnails.Controls.OfType<Panel>())
            {
                container.Paint -= Panel_Paint_Special;
                container.Paint += Panel_Paint_Special;
                container.Invalidate();
            }
        }

        private void Panel_Paint_Special(object sender, PaintEventArgs e)
        {
            Panel panel = (Panel)sender;
            string selectedContainer = schematicSelectedName + "_container";
            if (panel.Name == selectedContainer)
            {
                float penWidth = thumbnailSelectedBorderWidth;
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


        // ###########################################################################################
        // Setup the board after user selects it in "comboboxBoard".
        // ###########################################################################################

        private void SetupNewBoard()
        {
#if DEBUG
            StackTrace stackTrace = new StackTrace();
            StackFrame callerFrame = stackTrace.GetFrame(1);
            MethodBase callerMethod = callerFrame.GetMethod();
            string callerName = callerMethod.Name;
            Debug.WriteLine("[SetupNewBoard] called from [" + callerName + "]");
#endif

            //            listBoxComponents.BeginUpdate(); // suspend redrawing this listBox while updating it
            listBoxComponents.Items.Clear();

            boardSelectedName = comboBoxBoard.SelectedItem.ToString();
            textBox1.Text = ConvertStringToLabel(boardSelectedName); // feedback info

            var selectedBoardClass = GetSelectedBoardClass();
            if (selectedBoardClass == null) return;

            boardSelectedFilename = selectedBoardClass.DataFile;

            // Load selected thumbnail from configuration file, if already set
            string configKey = $"SelectedThumbnail|{hardwareSelectedName}|{boardSelectedName}";
            schematicSelectedName = Configuration.GetSetting(configKey, null);

            // Select the schematic - check if we can find the current selection, but otherwise default to first schematic
            var selectedSchematic = selectedBoardClass?.Files?.FirstOrDefault(f => f.Name == schematicSelectedName);
            if (selectedSchematic == null)
            {
                selectedSchematic = selectedBoardClass?.Files?.FirstOrDefault();
            }
            schematicSelectedName = selectedSchematic?.Name;
            schematicSelectedFile = selectedSchematic?.SchematicFileName;
            textBox2.Text = ConvertStringToLabel(schematicSelectedName); // feedback info

            // Initialize UI
            InitializeComponentCategories();

            // If no config was found (or empty):
            listBoxCategories.BeginUpdate(); // suspend redrawing this listBox while updating it
            bool loaded = LoadSelectedCategories(); // attempt to select from config
            if (!loaded && listBoxCategories.Items.Count > 0)
            {
                // fallback: auto-select everything
                for (int i = 0; i < listBoxCategories.Items.Count; i++)
                {
                    listBoxCategories.SetSelected(i, true);
                }
            }
            listBoxCategories.EndUpdate(); // resume redrawing of this specific listBox

            LoadAndApplySplitterPosition();

            SuspendLayout();
            InitializeThumbnails();
            InitializeTabMain();
            UpdateTabOverview(selectedBoardClass);
            UpdateTabRessources(selectedBoardClass);

            // Load polylines after initializing thumbnails and tabs
            PolylinesManagement.LoadPolylines();
            PopulatePolylineVisibilityPanel();

            ResumeLayout();
        }

        private void LoadAndApplySplitterPosition()
        {
            // Debug
#if DEBUG
            StackTrace stackTrace = new StackTrace();
            StackFrame callerFrame = stackTrace.GetFrame(1);
            MethodBase callerMethod = callerFrame.GetMethod();
            string callerName = callerMethod.Name;
            Debug.WriteLine("[LoadAndApplySplitterPosition] called from [" + callerName + "]");
#endif

            // Load and apply the board specific splitter position
            string defaultSplitterPos = (splitContainerSchematics.Width * 0.9).ToString(); // 90% of full width
            string configKey = $"SplitterPosition|{windowState}|{hardwareSelectedName}|{boardSelectedName}";
            string splitterPosVal = Configuration.GetSetting(configKey, defaultSplitterPos);
            if (int.TryParse(splitterPosVal, out int splitterPosition) && splitterPosition > 0)
            {
                splitContainerSchematics.SplitterDistance = splitterPosition;
            }
        }


        // ###########################################################################################
        // Get the class of the selected board.
        // ###########################################################################################

        private Board GetSelectedBoardClass()
        {
            var hardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            return hardware?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
        }


        // ###########################################################################################
        // Convert special characters, so they can be shown in labels.
        // Currently I only know of "&" being a problem?
        // ###########################################################################################

        public static string ConvertStringToLabel(string str)
        {
            return str.Replace("&", "&&");
        }


        // ###########################################################################################
        // Load the selected image, based on selected board and saved configuration setting.
        // ###########################################################################################

        /*
        private void LoadSelectedImage()
        {
            var hw = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var bd = hw?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (bd != null)
            {
                var file = bd.Files.FirstOrDefault(f => f.Name == schematicSelectedName);
                if (file != null)
                {
                    schematicSelectedFile = file.SchematicFileName;
                    InitializeTabMain();
                }
            }
        }
        */


        // ###########################################################################################
        // Enable double-buffering for smoother UI rendering.
        // ###########################################################################################

        private void EnableDoubleBuffering()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint,
                true
            );
            this.UpdateStyles();

            panelMain.DoubleBuffered(true);
            panelThumbnails.DoubleBuffered(true);
        }


        // ###########################################################################################
        // Enter fullscreen.
        // ###########################################################################################

        private void FullscreenModeEnter()
        {
            // Save (in variable only - not config file) current and set new window state
            formPreviousWindowState = WindowState;
            formPreviousFormBorderStyle = FormBorderStyle;
            previousBoundsForm = Bounds;
            previousBoundsPanelBehindTab = panelBehindTab.Bounds;
            previousBoundsFullscreenButton = buttonFullscreen.Bounds;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal;
            Bounds = Screen.PrimaryScreen.Bounds;

            // Set bounds for fullscreen panel
            panelBehindTab.Dock = DockStyle.Fill;
            panelBehindTab.BringToFront();

            // Determine which tab should be maximized
            panelBehindTab.Controls.Remove(panelMain);
            panelBehindTab.Controls.Add(panelMain);

            // We hould not populate the filter textbox when inside fullscreen
            textBoxFilterComponents.Enabled = false;

            // Hide tabs, and show fullscreen panel
            tabControl.Visible = false;
            buttonFullscreen.Text = "Exit fullscreen";
            isFullscreen = true;
        }


        // ###########################################################################################
        // Exit fullscreen.
        // ###########################################################################################

        private void FullscreenModeExit()
        {
            // Restore previous window state
            panelBehindTab.Dock = DockStyle.None;
            FormBorderStyle = formPreviousFormBorderStyle;
            WindowState = formPreviousWindowState;
            Bounds = previousBoundsForm;
            panelBehindTab.Bounds = previousBoundsPanelBehindTab;
            buttonFullscreen.Bounds = previousBoundsFullscreenButton;

            // Determine which tab should be repopulated with the previous maximized panel
            panelBehindTab.Controls.Remove(panelMain);
            splitContainerSchematics.Panel1.Controls.Add(panelMain);

            // Re-enable the filter
            textBoxFilterComponents.Enabled = true;
            textBoxFilterComponents.Focus();

            // Show again the tabs, and hide the fullscreen panel
            tabControl.Visible = true;
            buttonFullscreen.Text = "Fullscreen";
            isFullscreen = false;
        }


        // ###########################################################################################
        // Handle the fullscreen button click.
        // ###########################################################################################

        private void buttonFullscreen_Click(object sender, EventArgs e)
        {
            SuspendLayout();
            if (!isFullscreen)
            {
                FullscreenModeEnter();
            }
            else
            {
                FullscreenModeExit();
                ReadaptThumbnails();
            }

            ResumeLayout();

            // Reposition fullscreen button when in fullscreen (can only be done when UI is rendered)
            if (isFullscreen)
            {
                buttonFullscreen.Location = new Point(panelBehindTab.Width - buttonFullscreen.Width - 25, panelBehindTab.Height - 55);
                buttonFullscreen.BringToFront();
            }
        }


        // ###########################################################################################
        // Check if the "Fullscreen" button should be enabled or not.
        // ###########################################################################################

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


        // ###########################################################################################
        // Initialize the main/schematic image.
        // ###########################################################################################

        private void InitializeTabMain()
        {
            // Debug
#if DEBUG
            StackTrace stackTrace = new StackTrace();
            StackFrame callerFrame = stackTrace.GetFrame(1);
            MethodBase callerMethod = callerFrame.GetMethod();
            string callerName = callerMethod.Name;
            Debug.WriteLine("[InitializeTabMain] called from [" + callerName + "]");
#endif

            // Dispose the image, if one already exists
            if (image != null)
            {
                image.Dispose();
                image = null;
            }

            // Load main image
            string filePath = Path.Combine(Application.StartupPath, schematicSelectedFile);
            image = Image.FromFile(
                filePath
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
                AutoScroll = true,
                Dock = DockStyle.Fill
            };
            panelZoom.DoubleBuffered(true);
            panelMain.Controls.Add(panelZoom);

            // Create panelImage for the main picture
            panelImageMain = new Panel
            {
                Size = image.Size,
                BackgroundImage = image,
                BackgroundImageLayout = ImageLayout.Zoom,
                Dock = DockStyle.None
            };
            panelImageMain.DoubleBuffered(true);
            panelZoom.Controls.Add(panelImageMain);

            // Create overlay panel on top
            overlayPanel = new OverlayPanel
            {
                Bounds = panelImageMain.ClientRectangle
            };
            panelImageMain.Controls.Add(overlayPanel);
            overlayPanel.BringToFront();

            // Subscribe overlay events
            overlayPanel.OverlayClicked += OverlayPanel_OverlayClicked;
            overlayPanel.OverlayHoverChanged += OverlayPanel_OverlayHoverChanged;
            overlayPanel.OverlayPanelMouseDown += OverlayPanel_OverlayPanelMouseDown;
            overlayPanel.OverlayPanelMouseMove += OverlayPanel_OverlayPanelMouseMove;
            overlayPanel.OverlayPanelMouseUp += OverlayPanel_OverlayPanelMouseUp;

            // hest
            overlayPanel.MouseDown += polylinesManagement.panelImageMain_MouseDown;
            overlayPanel.MouseMove += polylinesManagement.panelImageMain_MouseMove;
            overlayPanel.MouseUp += polylinesManagement.panelImageMain_MouseUp;
            overlayPanel.Paint += polylinesManagement.panelImageMain_Paint;

            // Top-left label: file name
            labelFile = new Label
            {
                Name = "labelFile",
                Text = ConvertStringToLabel(schematicSelectedName),
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

        // ###########################################################################################
        // Create overlay arrays for the main/schematic image.
        // ###########################################################################################

        private void CreateOverlayArraysToTab()
        {
            var hw = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var bd = hw?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (bd == null) return;

            foreach (BoardOverlays bo in bd.Files)
            {
                if (bo.Name != schematicSelectedName) continue;
                if (bo?.Components != null)
                {
                    foreach (var comp in bo.Components)
                    {
                        string componentDisplay = bd.Components.FirstOrDefault(cb => cb.Label == comp.Label)?.NameDisplay ?? "";
                        if (comp.Overlays == null) continue;

                        foreach (var ov in comp.Overlays)
                        {
                            PictureBox overlayPictureBox = new PictureBox
                            {
                                Name = componentDisplay,
                                Location = new Point(ov.Bounds.X, ov.Bounds.Y),
                                Size = new Size(ov.Bounds.Width, ov.Bounds.Height),
                                Tag = bo.Name
                            };

                            overlayComponentsTab.Add(overlayPictureBox);
                            int idx = overlayComponentsTab.Count - 1;
                            overlayComponentsTabOriginalSizes[idx] = overlayPictureBox.Size;
                            overlayComponentsTabOriginalLocations[idx] = overlayPictureBox.Location;
                        }
                    }
                }
            }
        }


        // ###########################################################################################
        // Initialize the list of thumbnails.
        // ###########################################################################################

        private void InitializeThumbnails()
        {
            // Debug
#if DEBUG
            StackTrace stackTrace = new StackTrace();
            StackFrame callerFrame = stackTrace.GetFrame(1);
            MethodBase callerMethod = callerFrame.GetMethod();
            string callerName = callerMethod.Name;
            Debug.WriteLine("[InitializeList] called from [" + callerName + "]");
#endif

            // Gracefully dispose all controls
            DisposeAllControls(panelThumbnails);
            panelThumbnails.Controls.Clear();

            // Clear all lists
            overlayPanelsList.Clear();
            overlayListZoomFactors.Clear();

            // Find relevant schematic images to show here
            var hw = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var bd = hw?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (bd == null) return;
            if (bd.Files == null) return;

            // Walkthrough each schematic image for this board
            foreach (BoardOverlays schematic in bd.Files)
            {
                string filename = Path.Combine(Application.StartupPath, schematic.SchematicFileName);

                // Panel that will hold the label and the image
                Panel panelThumbnail = new Panel
                {
                    Name = schematic.Name + "_container",
                    BorderStyle = BorderStyle.None,
                };

                Label labelThumbnail = new Label
                {
                    Text = schematic.Name,
                    AutoSize = false,
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor = Color.Khaki,
                    ForeColor = Color.Black,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font = new Font("Calibri", 9),
                };

                PictureBox panelImage = new PictureBox
                {
                    Name = schematic.Name,
                    BackgroundImage = Image.FromFile(filename),
                    BackgroundImageLayout = ImageLayout.Zoom,
                    BorderStyle = BorderStyle.FixedSingle,
                    Dock = DockStyle.None,
                };

                // Overlay panel for the image that will ensure we can click anywhere on the image (to select it)
                OverlayPanel overlayPanel = new OverlayPanel
                {
                    Dock = DockStyle.Fill
                };

                // Add panels to each other
                panelThumbnails.Controls.Add(panelThumbnail);
                panelThumbnail.Controls.Add(labelThumbnail);
                panelThumbnail.Controls.Add(panelImage);
                panelImage.Controls.Add(overlayPanel);

                overlayPanelsList[schematic.Name] = overlayPanel;

                panelThumbnail.DoubleBuffered(true);
                labelThumbnail.DoubleBuffered(true);
                panelImage.DoubleBuffered(true);
                overlayPanel.DoubleBuffered(true);

                // Attach "MouseDown" and "Clicked" events to the overlay panel
                overlayPanel.OverlayPanelMouseDown += (s, e2) =>
                {
                    if (e2.Button == MouseButtons.Left)
                        ThumbnailImageClicked(panelImage);
                };
                overlayPanel.OverlayClicked += (s, e2) =>
                {
                    if (e2.MouseArgs.Button == MouseButtons.Left)
                        ThumbnailImageClicked(panelImage);
                };
                labelThumbnail.MouseDown += (s, e2) =>
                {
                    if (e2.Button == MouseButtons.Left)
                        ThumbnailImageClicked(panelImage);
                };
                labelThumbnail.Click += (s, e2) =>
                {
                    if (((MouseEventArgs)e2).Button == MouseButtons.Left)
                        ThumbnailImageClicked(panelImage);
                };
            }
            ReadaptThumbnails();
            DrawBorderInList();
        }


        // ###########################################################################################
        // Resize the thumbnails.
        // ###########################################################################################

        private void ReadaptThumbnails()
        {
            // Debug
#if DEBUG
            StackTrace stackTrace = new StackTrace();
            StackFrame callerFrame = stackTrace.GetFrame(1);
            MethodBase callerMethod = callerFrame.GetMethod();
            string callerName = callerMethod.Name;
            Debug.WriteLine("[ReadaptThumbnails] called from [" + callerName + "]");
#endif

            thumbnailsSameWidth = false;
            thumbnailsWidth = 0;
            int thumbnailsWidthOld = 0;

            // Suspend continues redrawing of thumbnail panel
            SuspendDrawing(panelThumbnails);

            for (int i = 0; i <= 40; i++) // "40" is unrealistic here, as it should never go higher than 4
            {
                thumbnailsWidthOld = thumbnailsWidth;
                if (!thumbnailsSameWidth)
                {
                    ReadaptThumbnails_Retry();
                }
                else
                {
                    break;
                }

                // if we end up in a race condition, then end redrawing when most of the thumbnail image is visible
                if (i >= 3 && thumbnailsWidth < thumbnailsWidthOld)
                {
                    Debug.WriteLine("Race condition in [ReadaptThumbnails]");
                    panelThumbnails.AutoScrollMinSize = new Size(0, panelThumbnails.ClientSize.Height + 1);
                    break;
                }
                else
                {
                    panelThumbnails.AutoScrollMinSize = Size.Empty;
                }
            }

            HighlightOverlays("list");

            ResumeDrawing(panelThumbnails);
        }

        private void ReadaptThumbnails_Retry()
        {

            int yPosition = 3; // first thumbnail position
            int padding = 1; // padding from left and right borders
            int verticalSpaceBetweenThumbnails = 1;
            int availableWidthForThumbnailContainer = panelThumbnails.ClientSize.Width - (padding * 2);
            int availableWidthForThumbnailElement = availableWidthForThumbnailContainer - (thumbnailSelectedBorderWidth * 2);

            // Check if we can end the loop for continues redrawing
            thumbnailsSameWidth = availableWidthForThumbnailContainer == thumbnailsWidth ? true : false;

            // Walkthrough all (parent) panels (a panel is a thumbnail container)
            foreach (Panel panelThumbnail in panelThumbnails.Controls.OfType<Panel>())
            {
                // Get the two elements inside the panel
                Label labelImage = panelThumbnail.Controls[0] as Label;
                PictureBox pictureBoxImage = panelThumbnail.Controls[1] as PictureBox;

                // Calculate height for the label
                Size textSize = TextRenderer.MeasureText(labelImage.Text, labelImage.Font);
                int labelHeight = textSize.Height + 2;

                // Calculate new height for the image, based on fixed width
                float aspectRatio = (float)pictureBoxImage.BackgroundImage.Height / pictureBoxImage.BackgroundImage.Width;
                int newImageHeight = (int)(availableWidthForThumbnailElement * aspectRatio);

                // Set location and new size for all three elements
                panelThumbnail.Location = new Point(padding, yPosition + panelThumbnails.AutoScrollPosition.Y); // "panelThumbnails.AutoScrollPosition.Y" will take into consideration, if there is a scrollbar visible, and then correctly position the thumbnail
                panelThumbnail.Size = new Size(availableWidthForThumbnailContainer, labelHeight + newImageHeight + (thumbnailSelectedBorderWidth * 2));
                labelImage.Location = new Point(thumbnailSelectedBorderWidth, thumbnailSelectedBorderWidth);
                labelImage.Size = new Size(availableWidthForThumbnailElement, labelHeight + thumbnailSelectedBorderWidth);
                pictureBoxImage.Location = new Point(thumbnailSelectedBorderWidth, labelHeight + thumbnailSelectedBorderWidth - 1); // -1 to avoid having a double-line
                pictureBoxImage.Size = new Size(availableWidthForThumbnailElement, newImageHeight + 1); // +1 to compensate for the above -1

                yPosition += panelThumbnail.Height + verticalSpaceBetweenThumbnails;

                float scaleFactor = (float)pictureBoxImage.Width / pictureBoxImage.BackgroundImage.Width;
                string key = panelThumbnail.Name.Replace("_container", "");

                // Update the aspect ratio used, in the overlay list
                overlayListZoomFactors[key] = scaleFactor;
                if (overlayPanelsList.ContainsKey(key))
                {
                    overlayPanelsList[key].Bounds = pictureBoxImage.ClientRectangle;
                }
            }
            thumbnailsWidth = availableWidthForThumbnailContainer;
        }


        // ###########################################################################################
        // Refresh the labels of the thumbnails (show if component is included), based on selected components.
        // ###########################################################################################

        private void RefreshThumbnailLabels()
        {
            var hw = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var bd = hw?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (bd == null) return;
            if (bd.Files == null) return;

            // Convert the selected items to a "HashSet" for faster lookup
            var selectedComponents = new HashSet<string>(listBoxComponentsSelectedText);

            foreach (var file in bd.Files)
            {
                // Now search for the container panel (instead of panel with file.Name)
                var container = panelThumbnails.Controls
                    .OfType<Panel>()
                    .FirstOrDefault(p => p.Name == file.Name + "_container");
                if (container == null) continue;

                // Find the label inside the container panel.
                var labelListFile = container.Controls.OfType<Label>().FirstOrDefault();
                if (labelListFile == null) continue;

                bool hasSelectedComponent = selectedComponents.Any(selectedComponentText =>
                {
                    // Find component "label"
                    string componentLabel = bd.Components
                        .FirstOrDefault(cb => cb.NameDisplay == selectedComponentText)?.Label ?? "";

                    // Find component "bounds" for the label
                    var compBounds = file.Components.FirstOrDefault(c => c.Label == componentLabel);

                    return compBounds != null && compBounds.Overlays != null && compBounds.Overlays.Count > 0;
                });

                if (hasSelectedComponent)
                {
                    labelListFile.Text = "* " + ConvertStringToLabel(file.Name);
                    labelListFile.BackColor = labelImageHasElementsBgClr;
                    labelListFile.ForeColor = labelImageHasElementsTxtClr;
                }
                else
                {
                    labelListFile.Text = ConvertStringToLabel(file.Name);
                    labelListFile.BackColor = labelImageBgClr;
                    labelListFile.ForeColor = labelImageTxtClr;
                }
            }
        }


        // ###########################################################################################
        // Handle the click on a thumbnail image.
        // ###########################################################################################

        private void ThumbnailImageClicked(PictureBox pan)
        {
            // Debug
#if DEBUG
            StackTrace stackTrace = new StackTrace();
            StackFrame callerFrame = stackTrace.GetFrame(1);
            MethodBase callerMethod = callerFrame.GetMethod();
            string callerName = callerMethod.Name;
            Debug.WriteLine("[ThumbnailImageClicked] called from [" + callerName + "]");
#endif

            // Clear all current overlays
            overlayPanel.Overlays.Clear();

            schematicSelectedName = pan.Name;

            // Save the board specific selected thumbnail to configuration file
            string configKey = $"SelectedThumbnail|{hardwareSelectedName}|{boardSelectedName}";
            Configuration.SaveSetting(configKey, schematicSelectedName);

            textBox2.Text = ConvertStringToLabel(schematicSelectedName); // feedback info

            var hw = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var bd = hw?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (bd != null)
            {
                var file = bd.Files.FirstOrDefault(f => f.Name == schematicSelectedName);
                if (file != null)
                {
                    schematicSelectedFile = file.SchematicFileName;
                    InitializeTabMain();  // load the selected image to "Main"
                }
            }

            // Ensure thumbnail border gets updated
            DrawBorderInList();

            // hest
            // Make sure polylines for the new image are loaded
            if (!PolylinesManagement.imagePolylines.ContainsKey(schematicSelectedName))
            {
                PolylinesManagement.imagePolylines[schematicSelectedName] = new List<List<Point>>();
            }

            // Update the polylines reference to use the correct list for this schematic
            PolylinesManagement.polylines = PolylinesManagement.imagePolylines[schematicSelectedName];

            // Reset selection when changing images
            PolylinesManagement.selectedPolylineIndex = -1;
            PolylinesManagement.selectedMarker = (-1, -1);

            // Update button state
//            UpdateButtonColorPolylineState();

            PopulatePolylineVisibilityPanel();

            // Force redraw to show polylines for this schematic
            //            overlayPanel.Invalidate();
        }


        // ###########################################################################################
        // Initialize the component categories.
        // ###########################################################################################

        private void InitializeComponentCategories()
        {
            // Debug
#if DEBUG
            StackTrace stackTrace = new StackTrace();
            StackFrame callerFrame = stackTrace.GetFrame(1);
            MethodBase callerMethod = callerFrame.GetMethod();
            string callerName = callerMethod.Name;
            Debug.WriteLine("[InitializeComponentCategories] called from [" + callerName + "]");
#endif

            listBoxCategories.BeginUpdate(); // suspend redrawing this listBox while updating it
            listBoxCategories.Items.Clear();

            var foundHardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var foundBoard = foundHardware?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);

            if (foundBoard?.Components != null)
            {
                // Use a "HashSet" to track added categories for faster lookups
                var addedTypes = new HashSet<string>();

                foreach (BoardComponents component in foundBoard.Components)
                {
                    if (!string.IsNullOrEmpty(component.Type) && addedTypes.Add(component.Type))
                    {
                        listBoxCategories.Items.Add(component.Type);
                    }
                }
            }
            listBoxCategories.EndUpdate(); // resume redrawing of this specific listBox
        }


        // ###########################################################################################
        // Resize the main/schematic image.
        // ###########################################################################################

        private void ResizeTabImage()
        {
            if (image == null) return;

            int scrollbarVerticalWidth = SystemInformation.VerticalScrollBarWidth;
            int scrollbarHorizontalHeight = SystemInformation.HorizontalScrollBarHeight;

            int availableWidth = panelZoom.Width - scrollbarVerticalWidth;
            int availableHeight = panelZoom.Height - scrollbarHorizontalHeight;

            float xZoomFactor = (float)availableWidth / image.Width;
            float yZoomFactor = (float)availableHeight / image.Height;
            zoomFactor = Math.Min(xZoomFactor, yZoomFactor);

            panelImageMain.Size = new Size(
                (int)(image.Width * zoomFactor),
                (int)(image.Height * zoomFactor)
            );

            // Always enforce scrollbars to avoid the weird "first zoom-in" flickering
            panelZoom.AutoScroll = true;
            panelZoom.AutoScrollMinSize = new Size(panelZoom.Width + 1, panelZoom.Height + 1);

            HighlightOverlays("tab");
        }


        // ###########################################################################################
        // Resize the tab image when the panel is resized by mousewheel.
        // ###########################################################################################

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
                        zoomLevel++;
                    }
                }
                else // scrolling down => zoom out
                {
                    // Only zoom out if the image is bigger than the container
                    if (panelImageMain.Width > panelZoom.Width || panelImageMain.Height > panelZoom.Height)
                    {
                        zoomFactor /= 1.5f;
                        hasZoomChanged = true;
                        zoomLevel--;
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
                    panelImageMain.Size = newSize;

                    // 5) Update the scroll position
                    panelZoom.AutoScrollPosition = new Point(
                        newScrollPosition.X - e.X,
                        newScrollPosition.Y - e.Y
                    );

                    // 6) Re‐highlight overlays (so they scale properly)
                    HighlightOverlays("tab");

                    label11.Text = $"{zoomLevel}";
                }
            }
            finally
            {
                ControlUpdateHelper.EndControlUpdate(panelZoom);
            }
        }


        // ###########################################################################################
        // Highlight all overlays according to the selected components.
        // ###########################################################################################

        private void ShowOverlaysAccordingToComponentList()
        {
            HighlightOverlays("tab");
            HighlightOverlays("list");

            // Refresh the thumbnails, to show if thumbnail includes the selected components
            RefreshThumbnailLabels();
        }


        // ###########################################################################################
        // Handle click-event for component highlights - both left- and rightclick events.
        // ###########################################################################################

        private void OverlayPanel_OverlayClicked(object sender, OverlayClickedEventArgs e)
        {
            string componentClickedLabel = e.OverlayInfo.ComponentLabel;

            // Find component "display name"
            var hw = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var bd = hw?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            string componentDisplay = bd.Components
                .FirstOrDefault(cb => cb.Label == componentClickedLabel)?.NameDisplay ?? "";

            // Left-mouse click (select component and show popup)
            if (e.MouseArgs.Button == MouseButtons.Left)
            {
                // 1) HIGHLIGHT the overlay
                if (!listBoxComponentsSelectedText.Contains(componentDisplay))
                {
                    listBoxComponentsSelectedText.Add(componentDisplay);
                }
                int index = listBoxComponents.Items.IndexOf(componentDisplay);
                if (index >= 0)
                {
                    listBoxComponents.SetSelected(index, true);
                }

                // 2) SHOW the form for the clicked component
                var hardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
                var board = hardware?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
                var comp = board?.Components.FirstOrDefault(c => c.Label == componentClickedLabel);
                if (comp != null)
                {
                    ShowComponentPopup(comp);
                }
            }

            // Right-mouse click (toggle component selection)
            else if (e.MouseArgs.Button == MouseButtons.Right)
            {

                if (!listBoxComponentsSelectedText.Contains(componentDisplay))
                {
                    listBoxComponentsSelectedText.Add(componentDisplay);
                    int index = listBoxComponents.Items.IndexOf(componentDisplay);
                    if (index >= 0)
                    {
                        listBoxComponents.SetSelected(index, true);
                    }
                }
                else
                {
                    listBoxComponentsSelectedText.Remove(componentDisplay);
                    int index = listBoxComponents.Items.IndexOf(componentDisplay);
                    if (index >= 0)
                    {
                        listBoxComponents.SetSelected(index, false);
                    }
                }
                ShowOverlaysAccordingToComponentList();
            }

            listBoxComponents.EndUpdate(); // resume redrawing of this specific listBox

            // Refresh the highlight overlays
            ShowOverlaysAccordingToComponentList();
        }


        // ###########################################################################################
        // Handle mouse-hover event for component highlights.
        // ###########################################################################################

        private void OverlayPanel_OverlayHoverChanged(object sender, OverlayHoverChangedEventArgs e)
        {
            if (labelComponent == null) return;

            if (e.IsHovering)
            {
                Cursor = Cursors.Hand;
                var hw = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
                var bd = hw?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
                var comp = bd?.Components.FirstOrDefault(c => c.Label == e.OverlayInfo.ComponentLabel);

                if (comp != null)
                {
                    labelComponent.Text = ConvertStringToLabel(comp.Label);
                    labelComponent.Text += comp.NameTechnical != "?" ? " | " + ConvertStringToLabel(comp.NameTechnical) : "";
                    labelComponent.Text += comp.NameFriendly != "?" ? " | " + ConvertStringToLabel(comp.NameFriendly) : "";
                }
                else
                {
                    labelComponent.Text = ConvertStringToLabel(e.OverlayInfo.ComponentLabel);
                }
                labelComponent.Visible = true;
            }
            else
            {
                Cursor = Cursors.Cross;
                labelComponent.Visible = false;
            }
        }


        // ###########################################################################################
        // Allow panning when right-clicking (and hold) directly on a component overlay.
        // ###########################################################################################

        private void OverlayPanel_OverlayPanelMouseDown(object sender, MouseEventArgs e)
        {
            // Right-click drag on empty space
            if (e.Button == MouseButtons.Right)
            {
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
                overlayPanelLastMousePos = Point.Empty;
            }
        }


        // ###########################################################################################
        // Close any open component informtion popup.
        // ###########################################################################################

        private void AttachClosePopupOnClick(Control parent)
        {
            // Attach a single MouseDown event to close any open popup
            parent.MouseDown += (s, e) =>
            {
                // Only if we click in the main form and a popup is open
                CloseComponentPopup();
            };

            // Recurse to child controls
            foreach (Control child in parent.Controls)
            {
                AttachClosePopupOnClick(child);
            }
        }


        // ###########################################################################################
        // Show the component information popup.
        // ###########################################################################################

        private void ShowComponentPopup(BoardComponents comp)
        {
            string title = comp.NameDisplay;
            componentInfoPopup = new FormComponent(comp);
            componentInfoPopup.Text = ConvertStringToLabel(title);
            componentInfoPopup.Show(this);
            componentInfoPopup.BringToFront();
        }


        // ###########################################################################################
        // Dispose all controls in a parent control.
        // Proper handling of memory.
        // ###########################################################################################

        void DisposeAllControls(Control parent)
        {
            for (int i = parent.Controls.Count - 1; i >= 0; i--)
            {
                Control child = parent.Controls[i];
                DisposeAllControls(child);
                child.Dispose();
            }
        }


        // ###########################################################################################
        // "Clear" buttons
        // ###########################################################################################

        private void buttonClear_Click(object sender, EventArgs e)
        {
            ClearEverything();
        }

        private void ClearEverything()
        {
            listBoxComponents.ClearSelected();
            listBoxComponentsSelectedText.Clear();
            textBoxFilterComponents.Text = "";
        }


        // ###########################################################################################
        // "Mark all" button
        // ###########################################################################################

        private void buttonAll_Click(object sender, EventArgs e)
        {
            listBoxComponents.SelectedIndexChanged -= listBoxComponents_SelectedIndexChanged;

            listBoxComponents.BeginUpdate(); // suspend redrawing this listBox while updating it

            for (int i = 0; i < listBoxComponents.Items.Count; i++)
            {
                listBoxComponents.SetSelected(i, true);
            }

            listBoxComponents.EndUpdate(); // resume redrawing of this specific listBox

            listBoxComponents.SelectedIndexChanged += listBoxComponents_SelectedIndexChanged;
            UpdateComponentSelection();
        }


        // ###########################################################################################
        // "Hardware" combobox change.
        // ###########################################################################################

        private void comboBoxHardware_SelectedIndexChanged(object sender, EventArgs e)
        {
            ClearEverything();

            comboBoxBoard.Items.Clear();
            hardwareSelectedName = comboBoxHardware.SelectedItem.ToString();
            textBox5.Text = ConvertStringToLabel(hardwareSelectedName); // feedback info

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


        // ###########################################################################################
        // "Board" combobox change.
        // ###########################################################################################

        private void comboBoxBoard_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetupNewBoard();
        }


        // ###########################################################################################
        // Save and resize thumbnails when the splitter position changes.
        // ###########################################################################################

        private void SplitContainer1_SplitterMoved(object sender, SplitterEventArgs e)
        {
            // Save the board specific splitter position to configuration file
            string configKey = $"SplitterPosition|{windowState}|{hardwareSelectedName}|{boardSelectedName}";
            Configuration.SaveSetting(configKey, splitContainerSchematics.SplitterDistance.ToString());

            ReadaptThumbnails();
        }


        // ###########################################################################################
        // Paint event for "SplitContainer".
        // ###########################################################################################

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

                using (Pen pen = new Pen(Color.Gray, 2))
                {
                    e.Graphics.DrawLine(pen, x, y1, x, y2);
                }
            }
        }


        // ###########################################################################################
        // Keyboard handling.
        // ###########################################################################################

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

                // Move selected polyline with arrow keys
                if (PolylinesManagement.selectedPolylineIndex != -1)
                {
                    int dx = 0, dy = 0;

                    if (keyData == Keys.Up)
                        dy = -1;
                    else if (keyData == Keys.Down)
                        dy = 1;
                    else if (keyData == Keys.Left)
                        dx = -1;
                    else if (keyData == Keys.Right)
                        dx = 1;

                    if (dx != 0 || dy != 0)
                    {
                        PolylinesManagement.MovePolyline(PolylinesManagement.selectedPolylineIndex, dx, dy);
                        overlayPanel.Invalidate();
                        PolylinesManagement.SavePolylinesToConfig();
                        return true;
                    }
                }
            }

            // Toggle "checkBoxBlink" with SPACE key
            if (keyData == Keys.Enter && tabControl.SelectedTab.Text != "Feedback")
            {
                checkBoxBlink.Checked = !checkBoxBlink.Checked;
                return true;
            }

            // Trigger the "buttonAll_Click" event
            if (keyData == (Keys.Alt | Keys.A))
            {
                buttonAll_Click(null, EventArgs.Empty);
                return true;
            }

            // Trigger the "buttonClear_Click" event
            if (keyData == (Keys.Alt | Keys.C))
            {
                buttonClear_Click(null, EventArgs.Empty);
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }


        // ###########################################################################################
        // Post feedback to server.
        // ###########################################################################################

        private void buttonSendFeedback_Click(object sender, EventArgs e)
        {
            string email = textBoxEmail.Text;
            string feedback = ConvertStringToLabel(textBoxFeedback.Text);

            // Validate the email address
            bool isValidEmail = true;
            if (email.Length > 0)
            {
                isValidEmail = IsValidEmail(email);
            }

            // Proceed if the email address is empty or valid
            if (isValidEmail)
            {
                var foundHardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
                var foundBoard = foundHardware?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
                string boardFile = foundBoard?.DataFile;
                string excelFilePath = Path.Combine(Application.StartupPath, boardFile);
                try
                {
                    using (WebClient webClient = new WebClient())
                    {
                        ServicePointManager.Expect100Continue = true;
                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
                        webClient.Headers.Add("user-agent", "CRT " + versionThis);

                        // Build the data to send
                        var data = new NameValueCollection
                            {
                                { "version", versionThis },
                                { "hardware", hardwareSelectedName },
                                { "board", boardSelectedName },
                                { "schematic", schematicSelectedName },
                                { "filename", boardFile },
                                { "email", email },
                                { "feedback", feedback }
                            };

                        // Attach the binary file if the checkbox is checked
                        if (checkBoxAttachExcel.Checked)
                        {
                            if (!IsFileLocked(excelFilePath))
                            {
                                byte[] fileBytes = File.ReadAllBytes(excelFilePath);
                                string fileBase64 = Convert.ToBase64String(fileBytes);
                                data["attachment"] = fileBase64;
                            }
                            else
                            {
                                MessageBox.Show("The Excel file currently has an exclusive lock, and cannot be read. Please close any application that might be using it and try again.",
                                    "ERROR: Cannot read file",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                            }
                        }

                        // Send it to the server
                        var response = webClient.UploadValues(crtPage + crtPageFeedback, "POST", data);
                        string resultFromServer = Encoding.UTF8.GetString(response);
                        if (resultFromServer == "Success")
                        {
                            if (email.Length > 0)
                            {
                                string txt = "Feedback sent. Please allow for some time, if any response is needed.";
                                MessageBox.Show(txt,
                                    "OK: Feedback sent",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Information);
                            }
                            else
                            {
                                string txt = "Feedback sent. No response will be given, as you did not specify an email address.";
                                MessageBox.Show(txt,
                                    "OK: Feedback sent",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Information);
                            }
                        }
                        else
                        {
                            string txt = "No feedback sent - did you fill in some text or attached the Excel data file?";
                            MessageBox.Show(txt,
                                "ERROR: Feedback not sent",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
                        }
                    }
                }
                catch (WebException ex)
                {
                    MessageBox.Show("CRT cannot submit the feedback right now, please retry later. If the issue persists, then you can connect directly with the developer at [dennis@commodore-repair-toolbox.dk]." + Environment.NewLine + Environment.NewLine + "The exact recieved HTTP error is:" + Environment.NewLine + Environment.NewLine + ex.Message,
                        "ERROR: Cannot connect with server",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }

            }
            else
            {
                string txt = "Invalid email address [" + email + "].";
                MessageBox.Show(txt,
                    "ERROR: Feedback not sent",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }


        // ###########################################################################################
        // Check if the file is exclusively locked by another application.
        // ###########################################################################################

        private bool IsFileLocked(string filePath)
        {
            try
            {
                using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (IOException)
            {
                return true;
            }
            return false;
        }


        // ###########################################################################################
        // Check if the email address syntax is valid.
        // ###########################################################################################

        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var addr = new System.Net.Mail.MailAddress(email);

                // Ensure the original email matches exactly and contains a valid domain and TLD
                return addr.Address == email &&
                       Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[a-zA-Z]{2,}$");
            }
            catch
            {
                return false;
            }
        }


        // ###########################################################################################
        // Populate the filename of the attached Excel file, to the feedback tab UI.
        // ###########################################################################################

        private void checkBoxAttachExcel_CheckedChanged(object sender, EventArgs e)
        {
            string txtForField = Path.GetFileName(boardSelectedFilename);
            textBox6.Text = checkBoxAttachExcel.Checked ? txtForField : "";
        }


        // ###########################################################################################
        // Define a custom method to suspend and resume drawing on a control.
        // Should be better than "SuspendLayout" and "ResumeLayout".
        // ###########################################################################################

        private const int WM_SETREDRAW = 0x000B;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, bool wParam, int lParam);

        public static void SuspendDrawing(Control control)
        {
            SendMessage(control.Handle, WM_SETREDRAW, false, 0);
        }

        public static void ResumeDrawing(Control control)
        {
            SendMessage(control.Handle, WM_SETREDRAW, true, 0);
            control.Refresh();
        }


        // ###########################################################################################
        // Save the 3 checkboxes in "Labels visible".
        // ###########################################################################################

        private void checkBoxVisibleLabels_CheckedChanged(object sender, EventArgs e)
        {
            // Save the state of the checkboxes
            Configuration.SaveSetting("ShowLabel", checkBox1.Checked.ToString());
            Configuration.SaveSetting("ShowTechnicalName", checkBox2.Checked.ToString());
            Configuration.SaveSetting("ShowFriendlyName", checkBox3.Checked.ToString());

            // Refresh the overlays
            HighlightOverlays("tab");
        }


        // ###########################################################################################
        // Minimize and maximize the "Labels visible" panel.
        // ###########################################################################################

        private void TogglePanelLabelsVisibility_Click(object sender, EventArgs e)
        {
            int currentHeight = panelLabelsVisible.Height;
            int newHeight = 0;

            if (currentHeight == 95)
            {
                newHeight = 26;
            }
            else
            {
                newHeight = 95;
            }

            panelLabelsVisible.Height = newHeight;

            Configuration.SaveSetting("ShowLabelsHeight", newHeight.ToString());

            // Reposition the panel
            int visibleHeight = panelZoom.ClientRectangle.Height;
            panelLabelsVisible.Location = new Point(0, visibleHeight - panelLabelsVisible.Height - 2); // bottom-left corner of the visible area
        }


        // ###########################################################################################
        // At application launch, then immediately enable drawing of polylines.
        // ###########################################################################################

        private void StartDrawingPolylines()
        {
            // Ensure the current image has an entry in the dictionary
            if (!PolylinesManagement.imagePolylines.ContainsKey(Main.schematicSelectedName))
            {
                PolylinesManagement.imagePolylines[Main.schematicSelectedName] = new List<List<Point>>();
            }
        }


        // ###########################################################################################
        // Clicking color button in "Traces visible" panel.
        // ###########################################################################################

        private void buttonColorPolyline_Click(object sender, EventArgs e)
        {
            if (colorDialog1.ShowDialog() == DialogResult.OK)
            {
                // If we should only choose the color for the next new polyline
                if (PolylinesManagement.selectedPolylineIndex == -1)
                {
                    PolylinesManagement.LastSelectedPolylineColor = colorDialog1.Color;
                    return;
                }

                // Use the composite key to set the color
                var key = (schematicSelectedName, PolylinesManagement.selectedPolylineIndex);
                PolylinesManagement.polylineColors[key] = colorDialog1.Color;

                // Update the last selected color
                PolylinesManagement.LastSelectedPolylineColor = colorDialog1.Color;

                // Redraw the panel to apply the new color
                overlayPanel.Invalidate();

                // Update the visibility panel and counters
                PopulatePolylineVisibilityPanel();
            }

            PolylinesManagement.SavePolylinesToConfig();
        }



        // ###########################################################################################
        // Update the "Traces visible" panel.
        // Will completely recreate all panel elements.
        // Will add/remove new colors and show/hide panel.
        // ###########################################################################################

        public void PopulatePolylineVisibilityPanel()
        {
            panelZoom.Invalidate();

            SuspendDrawing(panel1);

            panel1.Controls.Clear();

           // Get the polyline colors for the selected schematic
           var relevantColors = PolylinesManagement.polylineColors
                .Where(kvp => kvp.Key.ImageName == schematicSelectedName)
                .GroupBy(kvp => kvp.Value.ToArgb()) // Group by ARGB value
                .ToDictionary(group => Color.FromArgb(group.Key), group => group.Count()); // Convert back to Color and count

            foreach (var color in relevantColors.Keys)
            {
                if (!PolylinesManagement.CheckboxStates.ContainsKey(color))
                {
                    PolylinesManagement.CheckboxStates[color] = true; // Default to visible
                }
            }

            int yOffset = 0; // start position for the first element

            // Create a label to display the headline
            Label labelHeadline = new Label
            {
                Text = "Traces visible",
                AutoSize = true,
                Location = new Point(1, yOffset + 4),
                Font = new Font("Calibri", 10, FontStyle.Regular)
            };

            // Create a button to toggle visibility of panel
            Button buttonToggleTracesVisibility = new Button
            {
                Text = "M",
                Size = new Size(22, 23),
                BackColor = Color.LightGray,
            };
            buttonToggleTracesVisibility.Location = new Point(panel1.Width - buttonToggleTracesVisibility.Width - 2, yOffset);

            panel1.Controls.Add(labelHeadline);
            panel1.Controls.Add(buttonToggleTracesVisibility);
            panel1.Controls.Add(buttonTraceColor);
            panel1.Controls.Add(buttonTracesDelete);

            yOffset += labelHeadline.Height + 9;

            foreach (var colorEntry in relevantColors)
            {
                var color = colorEntry.Key;
                var count = colorEntry.Value;

                // Create the checkbox
                bool isChecked = PolylinesManagement.CheckboxStates.ContainsKey(color) ? PolylinesManagement.CheckboxStates[color] : true;
                CheckBox checkBox = new CheckBox
                {
                    Text = "", // No text, as the color is visualized
                    Checked = isChecked,
                    Tag = color,
                    AutoSize = true,
                    Location = new Point(5, yOffset) // Position next to the color panel
                };

                // Create a panel to display the color
                Panel colorPanel = new Panel
                {
                    Size = new Size(50, 13), // Small square to represent the color
                    BackColor = color,
                    BorderStyle = BorderStyle.FixedSingle,
                    Location = new Point(25, yOffset) // Align with the checkbox
                };

                // Create a label to display the counter
                Label counterLabel = new Label
                {
                    Text = $"({count})", // Show the count
                    AutoSize = true,
                    Location = new Point(80, yOffset), // Position after the color panel
                    Font = new Font("Calibri", 9, FontStyle.Regular)
                };
                counterLabel.Location = new Point(colorPanel.Right + 2, yOffset - 1);

                // Attach an event handler to toggle visibility
                checkBox.CheckedChanged += CheckBox_CheckedChanged;
                colorPanel.Click += ColorPanel_Click;

                // Add the color panel, checkbox, and counter label to the manual panel
                panel1.Controls.Add(colorPanel);
                panel1.Controls.Add(checkBox);
                panel1.Controls.Add(counterLabel);
                

                yOffset += Math.Max(colorPanel.Height, checkBox.Height) + 5; // Adjust spacing
            }

            int buttonSize = (panel1.Width - 13) / 2;
            buttonTraceColor.Size = new Size(buttonSize, 23);
            buttonTracesDelete.Size = new Size(buttonSize, 23);
            buttonTraceColor.Location = new Point(5, yOffset);
            buttonTracesDelete.Location = new Point(panel1.Width - buttonTracesDelete.Width - 5, yOffset);

            // Add the label panel to "panelMain"
            panelMain.Controls.Add(panel1);

            // Attach the event handler for the button
            panel1.Click -= TogglePanelTracesVisibility_Click;
            panel1.Click += TogglePanelTracesVisibility_Click;
            labelHeadline.Click -= TogglePanelTracesVisibility_Click;
            labelHeadline.Click += TogglePanelTracesVisibility_Click;
            buttonToggleTracesVisibility.Click -= TogglePanelTracesVisibility_Click;
            buttonToggleTracesVisibility.Click += TogglePanelTracesVisibility_Click;

            if (isPanelTracesVisible)
            {
                panel1.Height = yOffset + buttonTraceColor.Height + 5;
                panelTracesVisibleHeight = panel1.Height;
                Configuration.SaveSetting("ShowTracesHeight", panelTracesVisibleHeight.ToString());
            } else
            {
                // "26" pixels equals it is minimized
                panel1.Height = 26;
            }

            int visibleHeight = panelZoom.ClientRectangle.Height;
            int visibleWidth = panelZoom.ClientRectangle.Width;
            panel1.Location = new Point(visibleWidth - panel1.Width - 2, visibleHeight - panel1.Height - 2); // bottom-left corner of the visible area

            ResumeDrawing(panel1);

            if (relevantColors.Count == 0)
            {
                panel1.Visible = false;
            }
            else
            {
                panel1.Visible = true;
                panel1.BringToFront();
            }

        }


        // ###########################################################################################
        // Checkbox event handler for toggling polyline visibility.
        // ###########################################################################################

        private void CheckBox_CheckedChanged(object sender, EventArgs e)
        {
            var cb = sender as CheckBox;
            if (cb != null && cb.Tag is Color selectedColor)
            {
                polylinesManagement.TogglePolylineVisibility(selectedColor, cb.Checked);
            }
        }


        // ###########################################################################################
        // Clicking the color panel/bar in the "Traces visible" panel.
        // ###########################################################################################

        private void ColorPanel_Click(object sender, EventArgs e)
        {
            var panel = sender as Panel;
            if (panel != null && panel.BackColor is Color selectedColor)
            {
                // Toggle the visibility state
                bool isVisible = PolylinesManagement.CheckboxStates.ContainsKey(selectedColor)
                                 && PolylinesManagement.CheckboxStates[selectedColor];
                PolylinesManagement.CheckboxStates[selectedColor] = !isVisible;

                // Update the visibility of the polyline
                polylinesManagement.TogglePolylineVisibility(selectedColor, !isVisible);

                // Find the corresponding checkbox for the color
                foreach (Control control in panel1.Controls)
                {
                    if (control is CheckBox checkBox && checkBox.Tag is Color color && color == selectedColor)
                    {
                        // Set the checkbox state to match the new visibility state
                        checkBox.Checked = !isVisible;
                        break;
                    }
                }
            }
        }


        // ###########################################################################################
        // Minimize and maximize the "Traces visible" panel.
        // ###########################################################################################

        private void TogglePanelTracesVisibility_Click(object sender, EventArgs e)
        {
            int currentHeight = panel1.Height;
            int newHeight = 0;

            if (currentHeight > 26)
            {
                newHeight = 26;
                isPanelTracesVisible = false;
            }
            else
            {
                newHeight = panelTracesVisibleHeight;
                isPanelTracesVisible = true;
            }

            panel1.Height = newHeight;

            // Reposition the panel
            int visibleHeight = panelZoom.ClientRectangle.Height;
            int visibleWidth = panelZoom.ClientRectangle.Width;
            panel1.Location = new Point(visibleWidth - panel1.Width - 2, visibleHeight - panel1.Height - 2); // bottom-left corner of the visible area

            Configuration.SaveSetting("ShowTraces", isPanelTracesVisible.ToString());
        }


        // ###########################################################################################
        // "Delete all traces" button.
        // Will delete only for the selected hardware and board.
        // ###########################################################################################

        private void buttonTracesDelete_Click(object sender, EventArgs e)
        {
            // Confirm deletion
            var confirmResult = MessageBox.Show(
                "Are you sure you want to delete all traces for the selected hardware and board?",
                "Confirm deletion of all traces",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (confirmResult == DialogResult.Yes)
            {
                // Clear traces for the selected hardware and board
                var selectedHardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
                var selectedBoard = selectedHardware?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
                if (selectedBoard != null)
                {
                    PolylinesManagement.ClearTracesForBoard(selectedBoard);
                    PopulatePolylineVisibilityPanel();
                }
            }
        }
    }


    // ###########################################################################################
    // Class definitions.
    // ###########################################################################################

    // "Hardware" is read from the Excel file in level 1, "Commodore-Repair-Toolbox.xlsx".
    // It contains a list of all associated boards.
    public class Hardware
    {
        public string Name { get; set; }
        public List<Board> Boards { get; set; }
    }

    // "Board" is read from the Excel file in level 1, "Commodore-Repair-Toolbox.xlsx".
    public class Board
    {
        public string Name { get; set; }
        public string RevisionDate { get; set; }
        public string DataFile { get; set; }
        public List<BoardOverlays> Files { get; set; }
        public List<BoardComponents> Components { get; set; }
        public List<BoardLinks> BoardLinks { get; set; }
        public List<BoardLocalFiles> BoardLocalFiles { get; set; }
    }

    public class BoardComponents
    {
        public string Label { get; set; }
        public string NameTechnical { get; set; }
        public string NameFriendly { get; set; }
        public string NameDisplay { get; set; }
        public string Type { get; set; }
        public string OneLiner { get; set; }
        public string Description { get; set; }
        public List<ComponentOscilloscope> Oscilloscope { get; set; }
        public List<ComponentLocalFiles> LocalFiles { get; set; }
        public List<ComponentLinks> ComponentLinks { get; set; }
        public List<ComponentImages> ComponentImages { get; set; }
    }

    public class BoardOverlays
    {
        public string Name { get; set; }
        public string SchematicFileName { get; set; }
        public string HighlightColorTab { get; set; }
        public string HighlightColorList { get; set; }
        public int HighlightOpacityTab { get; set; }
        public int HighlightOpacityList { get; set; }
        public List<ComponentBounds> Components { get; set; }
    }

    public class BoardLinks
    {
        public string Category { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
    }

    public class BoardLocalFiles
    {
        public string Category { get; set; }
        public string Name { get; set; }
        public string Datafile { get; set; }
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

    public class ComponentImages
    {
        public string Region { get; set; }
        public string Pin { get; set; }
        public string Name { get; set; }
        public string FileName { get; set; }
    }

    public class ComponentOscilloscope
    {
        public string Name { get; set; }
        public string Region { get; set; }
        public string Pin { get; set; }
        public string Reading { get; set; }
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