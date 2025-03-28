using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
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
        ";

        // Reference to the popup/info form
        private FormComponent componentInfoPopup = null;

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

        // Selected entries in the components list
        private List<string> listBoxComponentsSelectedLabels = new List<string>();
        private Dictionary<string, string> listBoxNameValueMapping = new Dictionary<string, string>();

        // Version information
        private string versionThis = "";
        private string versionOnline = "";
        private string versionOnlineTxt = "";

        // Current user selection
        public string hardwareSelectedName;
        private string hardwareSelectedFolder;
        private string boardSelectedName;
        private string boardSelectedFolder;
        private string boardSelectedFilename;
        private string schematicSelectedName;
        private string schematicSelectedFile;
        private float zoomFactor = 1.0f;
        private Point overlayPanelLastMousePos = Point.Empty;

        // Misc
        private Dictionary<Control, EventHandler> clickEventHandlers = new Dictionary<Control, EventHandler>();
        private TabPage previousTab;


        // ###########################################################################################
        // Main form constructor.
        // ###########################################################################################

        public Main()
        {
            InitializeComponent();

            // Create or overwrite the debug output file
            CreateDebugOutputFile();

            // Get build type
            #if DEBUG
                buildType = "Debug";
            #else
                buildType = "Release";
            #endif

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
            string savedState = Configuration.GetSetting("WindowState", "Maximized");
            if (Enum.TryParse(savedState, out FormWindowState state) && state != FormWindowState.Minimized)
            {
                this.WindowState = state;
            }
                        
            ApplyConfigSettings();

            AttachConfigurationSaveEvents();

            tabControl.Dock = DockStyle.Fill;

            // Set initial focus to textBoxFilterComponents
            textBoxFilterComponents.Focus();

            // Initialize the blink timer
            InitializeBlinkTimer();

            // Attach various event handles
            AttachEventHandlers();
        }

        private void Form_Shown(object sender, EventArgs e)
        {
            ReadaptThumbnails();
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
                WebClient webClient = new WebClient();
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
                webClient.Headers.Add("user-agent", "CRT "+ versionThis);

                // Have some control POST data
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
                    } else
                    {
                        versionOnline = "";
                    }
                } else
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
        // Create/overwrite the debug output logfile
        // ###########################################################################################

        private void CreateDebugOutputFile()
        {
            string filePath = Path.Combine(Application.StartupPath, "Commodore-Repair-Toolbox.log");
            using (StreamWriter writer = new StreamWriter(filePath, false))
            {
                writer.WriteLine("Debug output file created [" + DateTime.Now +"]");
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
//            Load += Form_Load;
            FormClosing += Main_FormClosing;
            ResizeBegin += Form_ResizeBegin;
            ResizeEnd += Form_ResizeEnd;
            checkBoxBlink.CheckedChanged += CheckBoxBlink_CheckedChanged;
            textBoxFilterComponents.TextChanged += TextBoxFilterComponents_TextChanged;
//            panelListAutoscroll.Layout += PanelListAutoscroll_Layout;
            splitContainerSchematics.Paint += SplitContainer1_Paint;
            comboBoxHardware.DropDownClosed += ComboBox_DropDownClosed;
            comboBoxBoard.DropDownClosed += ComboBox_DropDownClosed;
            listBoxCategories.SelectedIndexChanged += ListBoxCategories_SelectedIndexChanged;
            AttachClickEventsToFocusFilterComponents(this);
            textBox3.TextChanged += TextBox3_TextChanged;
            tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
            splitContainerSchematics.SplitterMoved += SplitContainer1_SplitterMoved;
            this.Shown += new EventHandler(Form_Shown);
        }


        // ###########################################################################################
        // Attach a click event to all controls with a name, and have them focus
        // "textBoxFilterComponents" so it is ready for text input for filtering.
        // ###########################################################################################

        private void AttachClickEventsToFocusFilterComponents(Control parent)
        {
            if (!(parent is ComboBox))
            {
                //                if (!string.IsNullOrEmpty(parent.Name)) // do not attach components without any name
                //                {           
                //                    Debug.WriteLine("AttachClickEventHandlers: " + (string.IsNullOrEmpty(parent.Name) ? "[Unnamed " + parent.GetType().Name + "]" : parent.Name));
                //parent.Click += (s, e) => textBoxFilterComponents.Focus();
                EventHandler handler = (s, e) => textBoxFilterComponents.Focus();
                parent.Click += handler;
                clickEventHandlers[parent] = handler;
                //                }
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
            isResizing = true;
        }

        private void Form_ResizeEnd(object sender, EventArgs e)
        {
            InitializeList();

            isResizing = false;
            ResizeTabImage();
            HighlightOverlays("tab");
            HighlightOverlays("list");
        }


        // ###########################################################################################
        // Initialize and update the tab for "Overview".
        // Will load new content from board data file.
        // ###########################################################################################

        private EventHandler<CoreWebView2WebMessageReceivedEventArgs> _componentHandlerOverview;
        private EventHandler<CoreWebView2WebMessageReceivedEventArgs> _fileOpenHandlerOverview;
        private EventHandler<CoreWebView2NewWindowRequestedEventArgs> _urlHandlerOverview;

        private async void UpdateTabOverview(Board selectedBoard)
        {
            if (webView2Overview.CoreWebView2 == null)
            {
                await webView2Overview.EnsureCoreWebView2Async(null);
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
                    if (target.matches('[data-compLabel]')) {
                        var compLabel = target.getAttribute('data-compLabel');
                        window.chrome.webview.postMessage('openComp:' + compLabel);
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
                <h1>Overview of components</h1><br />
            ";

            var foundHardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var foundBoard = foundHardware?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (foundBoard != null)
            {
                if (foundBoard?.Components != null)
                {
                    htmlContent += "<table width='100%' border='1'>";
                    htmlContent += "<thead>";
                    htmlContent += "<tr>";
                    htmlContent += "<th>Type</th>";
                    htmlContent += "<th>Component</th>";
                    htmlContent += "<th>Technical name</th>";
                    htmlContent += "<th>Friendly name</th>";
                    htmlContent += "<th>Short descr.</th>";
                    htmlContent += "<th>Long descr.</th>";
                    htmlContent += "<th>Local files</th>";
                    htmlContent += "<th>Web links</th>";
                    htmlContent += "</tr>";
                    htmlContent += "</thead>";
                    htmlContent += "<tbody>";

                    foreach (ComponentBoard comp in foundBoard.Components)
                    {

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
                        htmlContent += $"<td valign='top' data-compLabel='{compLabel}'>{compLabel}</td>";
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
                                string filePath = Path.Combine(Application.StartupPath, hardwareSelectedFolder, boardSelectedFolder, file.FileName);
                                htmlContent += "<a href='file:///" + filePath.Replace(@"\", @"\\") + "' class='tooltip-link' data-title='" + file.Name + "' target='_blank'>#" + counter + "</a> ";
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
                                htmlContent += "<a href='" + link.Url + "' target='_blank' class='tooltip-link' data-title='"+ link.Name + "'>#" + counter + "</a> ";
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

            // ---
            // Make sure we only have one handler for the "open*" message (seems
            // to be an issue where it can trigger an event multiple times!?

            if (_componentHandlerOverview != null)
            {
                webView2Overview.CoreWebView2.WebMessageReceived -= _componentHandlerOverview;
            }
            _componentHandlerOverview = (sender, args) =>
            {
                string message = args.TryGetWebMessageAsString();
                if (message.StartsWith("openComp:"))
                {
                    string compName = message.Substring("openComp:".Length);
                    ComponentBoard selectedComp = foundBoard.Components.FirstOrDefault(c => c.Label == compName);
                    if (selectedComp != null)
                    {
                        FormComponent componentInfoPopup = new FormComponent(selectedComp, hardwareSelectedFolder, boardSelectedFolder);
                        componentInfoPopup.Show(this);
                        componentInfoPopup.TopMost = true;
                    }
                }
            };
            webView2Overview.CoreWebView2.WebMessageReceived += _componentHandlerOverview;

            // Handle the "file" handler
            if (_fileOpenHandlerOverview != null)
            {
                webView2Overview.CoreWebView2.WebMessageReceived -= _fileOpenHandlerOverview;
            }
            _fileOpenHandlerOverview = (sender, args) =>
            {
                string message = args.TryGetWebMessageAsString();
                if (message.StartsWith("openFile:"))
                {
                    string fileUrl = message.Substring("openFile:".Length);
                    Process.Start(new ProcessStartInfo(new Uri(fileUrl).LocalPath) { UseShellExecute = true });
                }
            };
            webView2Overview.CoreWebView2.WebMessageReceived += _fileOpenHandlerOverview;

            // Handle the "URL" handler
            if (_urlHandlerOverview != null)
            {
                webView2Overview.CoreWebView2.NewWindowRequested -= _urlHandlerOverview;
            }
            _urlHandlerOverview = (sender, args) =>
            {
                args.Handled = true; // prevent the default behavior
                Process.Start(new ProcessStartInfo(args.Uri) { UseShellExecute = true });
            };
            webView2Overview.CoreWebView2.NewWindowRequested += _urlHandlerOverview;

            // ---

            webView2Overview.NavigateToString(htmlContent);
        }


        // ###########################################################################################
        // Initialize and update the tab for "Ressources".
        // Will load new content from board data file.
        // ###########################################################################################

        private EventHandler<CoreWebView2WebMessageReceivedEventArgs> _fileOpenHandlerRessources;
        private EventHandler<CoreWebView2NewWindowRequestedEventArgs> _urlHandlerRessources;

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
                "+ htmlForTabs +@"
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
                        string filePath = Path.Combine(Application.StartupPath, hardwareSelectedFolder, boardSelectedFolder, file.Datafile);
                        htmlContent += "<li><a href='file:///" + filePath.Replace(@"\", @"\\") + "' target='_blank'>" + file.Name + "</a></li>";
                    }
                    htmlContent += "</ul>";
                    htmlContent += "<br />";
                }
            } else
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

            // ---
            // Make sure we only have one handler for the "openFile" message (seems
            // to be an issue where it can trigger an event multiple times!?

            // Handle the "file" handler
            if (_fileOpenHandlerRessources != null)
            {
                webView2Ressources.CoreWebView2.WebMessageReceived -= _fileOpenHandlerRessources;
            }
            _fileOpenHandlerRessources = (sender, args) =>
            {
                string message = args.TryGetWebMessageAsString();
                if (message.StartsWith("openFile:"))
                {
                    string fileUrl = message.Substring("openFile:".Length);
                    Process.Start(new ProcessStartInfo(new Uri(fileUrl).LocalPath) { UseShellExecute = true });
                }
            };
            webView2Ressources.CoreWebView2.WebMessageReceived += _fileOpenHandlerRessources;

            // Handle the "URL" handler
            if (_urlHandlerRessources != null)
            {
                webView2Ressources.CoreWebView2.NewWindowRequested -= _urlHandlerRessources;
            }
            _urlHandlerRessources = (sender, args) =>
            {
                args.Handled = true; // prevent the default behavior
                Process.Start(new ProcessStartInfo(args.Uri) { UseShellExecute = true });
            };
            webView2Ressources.CoreWebView2.NewWindowRequested += _urlHandlerRessources;

            // ---

            webView2Ressources.NavigateToString(htmlContent);
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
                "+ htmlForTabs +@"
                <h1>Help for application usage</h1><br />

                <i>Commodore Repair Toolbox</i> is fairly simple, but some basic help is always nice to have.<br />
                <br />

                Mouse functions:<br />
                <ul>
                <li><b>Left-click</b> on a component will show a information popup</li>
                <li><b>Right-click</b> on a component will toggle highlight</li>
                <li><b>Right-click</b> and <b>Hold</b> will pan the image</li>
                <li><b>Scrollwheel</b> will zoom in/out</li>
                </ul>
                <br />

                Keyboard functions:<br />
                <ul>
                <li><b>F11</b> will toggle fullscreen</li>
                <li><b>ESCAPE</b> will exit fullscreen or close popup component information</li>
                <li><b>SPACE</b> will toggle blinking for selected components (does not apply in ""Feedback"" tab)</li>
                <li>Focus cursor in input field, and type, to filter component list</li>
                </ul>
                <br />

                Component selection:<br />
                <ul>
                <li>When a component is selected, then it will visualize if component is part of thumbnail in list-view:</li>
                <ul>
                <li>Appending an asterisk/* as first character in thumbnail label</li>
                <li>Background color of thumbnail label changes to red</li>
                </ul>
                <li>You cannot highlight a component in image, if its component category is unselected</li>
                </ul>
                <br />
                
                Configuration saved:<br />
                <ul>
                <li>Last viewed schematic</li>
                <li>Schematic/thumbnails slider position</li>
                <li>Shown component categories saved per board</li>
                </ul>
                <br />

                How-to add or update your own data:<br />
                <ul>
                <li>View <a href='https://github.com/HovKlan-DH/Commodore-Repair-Toolbox?tab=readme-ov-file#software-used' target='_blank'>GitHub Documentation</a></li>
                </ul>
                <br />

                How-to report a problem or comment something:<br />
                <ul>
                <li>View <a href='https://github.com/HovKlan-DH/Commodore-Repair-Toolbox/issues' target='_blank'>GitHub Issues</a></li>
                </ul>
                <br />
                
                When there is a newer version available online, it will be marked with an asterisk (*) in the ""About"" tab.<br />
                Then navigate to the tab and view or download the new version.<br />

                </body>
                </html>
            ";

            // Open URLs in default web browser
            webView2Help.CoreWebView2.NewWindowRequested += (sender, args) =>
            {
                args.Handled = true; // do not render in internal browser mode
                Process.Start(new ProcessStartInfo(args.Uri) { UseShellExecute = true });
            };

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

            /*
            if (versionOnlineTxt != "")
            {
                versionOnlineTxt = @"
                    <font color='IndianRed'>
                    There is a newer version available online: <b>" + versionOnline + @"</b><br />
                    </font>
                    View Changelog and download the new version from here, <a href='https://github.com/HovKlan-DH/Commodore-Repair-Toolbox/releases' target='_blank'>https://github.com/HovKlan-DH/Commodore-Repair-Toolbox/releases</a><br />
                    <br />
                ";
            }
            */

            string htmlContent = @"
                <html>
                <head>
                <meta charset='UTF-8'>
                </head>
                <body>
                "+ htmlForTabs + @"
                <h1>Commodore Repair Toolbox</h1><br />

                You are running version: <b>"+ versionThis + @"</b><br />
                <br />

                " + versionOnlineTxt + @"

                All programming done by Dennis Helligsø (dennis@commodore-repair-toolbox.dk).<br />
                <br />

                Visit official project home page at <a href='https://github.com/HovKlan-DH/Commodore-Repair-Toolbox' target='_blank'>https://github.com/HovKlan-DH/Commodore-Repair-Toolbox</a><br />
                <br />
                
                </body>
                </html>
            ";

            // Open URLs in default web browser
            webView2About.CoreWebView2.NewWindowRequested += (sender, args) =>
            {
                args.Handled = true; // do not render in internal browser mode
                Process.Start(new ProcessStartInfo(args.Uri) { UseShellExecute = true });
            };

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
        // Filtering of components.
        // ###########################################################################################

        private void TextBoxFilterComponents_TextChanged(object sender, EventArgs e)
        {
            FilterListBoxComponents();
        }

        private void ListBoxCategories_SelectedIndexChanged(object sender, EventArgs e)
        {
            FilterListBoxComponents();
        }

        private void FilterListBoxComponents()
        {
            string filterText = textBoxFilterComponents.Text.ToLower();
            listBoxComponents.Items.Clear();
            listBoxNameValueMapping.Clear();

            var foundHardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var foundBoard = foundHardware?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (foundBoard != null)
            {
                if (foundBoard?.Components != null)
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
                                listBoxComponents.Items.Add(displayText);
                                listBoxNameValueMapping[displayText] = comp.Label;
                            }
                        }
                    }
                }
            }
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
                EnableSelectedOverlays();
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


        // ###########################################################################################
        // Handle input of email address in "Feedback" tab.
        // ###########################################################################################

        private void TextBox3_TextChanged(object sender, EventArgs e)
        {
            string email = textBox3.Text;
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
                textBox4.Focus();
            }

            previousTab = tabControl.SelectedTab;
        }
        





        // HERTIL











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
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint,
                true
            );
            this.UpdateStyles();

            panelMain.DoubleBuffered(true);
            //panelListMain.DoubleBuffered(true);
            panelListAutoscroll.DoubleBuffered(true);
        }





        // ---------------------------------------------------------------------
        // Apply saved settings to controls

        private void ApplyConfigSettings()
        {
            // Set default values first
            string defaultSelectedHardware = classHardware.FirstOrDefault()?.Name;
            string defaultSelectedBoard = classHardware.FirstOrDefault(h => h.Name == defaultSelectedHardware)?.Boards?.FirstOrDefault()?.Name;
            string defaultSelectedSchematic = classHardware.FirstOrDefault(h => h.Name == defaultSelectedHardware)?.Boards?.FirstOrDefault(b => b.Name == defaultSelectedBoard)?.Files?.FirstOrDefault()?.Name;
            string defaultSplitterPos = (splitContainerSchematics.Width * 0.9).ToString(); // 90% of full width

            // Load saved settings from configuration file - or set default if none exists
            string selectedHardwareVal = Configuration.GetSetting("HardwareSelected", defaultSelectedHardware);
            string selectedBoardVal = Configuration.GetSetting("BoardSelected", defaultSelectedBoard);
            string selectedSchematicVal = Configuration.GetSetting("SelectedThumbnail", defaultSelectedSchematic);
            string splitterPosVal = Configuration.GetSetting("SplitterPosition", defaultSplitterPos);
            string userEmail = Configuration.GetSetting("UserEmail", "");

            textBox3.Text = userEmail; // set email address in "Feedback" tab

            // Populate all hardware in combobox - and select
            foreach (Hardware hardware in classHardware)
            {
                comboBoxHardware.Items.Add(hardware.Name);
            }
            int indexHardware = comboBoxHardware.Items.IndexOf(selectedHardwareVal);
            comboBoxHardware.SelectedIndex = indexHardware;
            hardwareSelectedName = comboBoxHardware.SelectedItem.ToString();
            textBox5.Text = hardwareSelectedName; // feedback info           

            // Populate all boards in combobox, based on selected hardware - and select
            var hw = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            if (hw != null)
            {
                foreach (var board in hw.Boards)
                {
                    comboBoxBoard.Items.Add(board.Name);
                }
                int indexBoard = comboBoxBoard.Items.IndexOf(selectedBoardVal);
                comboBoxBoard.SelectedIndex = indexBoard;
                boardSelectedName = comboBoxBoard.SelectedItem.ToString();
                textBox1.Text = boardSelectedName; // feedback info           
            }

            // Set the selected schematic
            var bd = hw?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (bd != null)
            {
                var schematic = bd.Files.FirstOrDefault(f => f.Name == selectedSchematicVal) ?? bd.Files.FirstOrDefault();
                if (schematic != null)
                {
                    //selectedSchematicVal = schematic.Name;
                    schematicSelectedName = schematic.Name;
                    boardSelectedFilename = Path.GetFileName(bd.Datafile); // feedback info
                    LoadSelectedImage();
                }
            }

            // Apply splitter position
            if (int.TryParse(splitterPosVal, out int splitterPosition) && splitterPosition > 0)
            {
                splitContainerSchematics.SplitterDistance = splitterPosition;
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
                var file = bd.Files.FirstOrDefault(f => f.Name == schematicSelectedName);
                if (file != null)
                {
                    schematicSelectedFile = file.FileName;
                    InitializeTabMain();
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
                //Configuration.SaveSetting("HardwareSelected", comboBoxHardware.SelectedIndex.ToString());
                Configuration.SaveSetting("HardwareSelected", hardwareSelectedName);
            };

            comboBoxBoard.SelectedIndexChanged += (s, e) =>
            {
                //Configuration.SaveSetting("BoardSelected", comboBoxBoard.SelectedIndex.ToString());
                Configuration.SaveSetting("BoardSelected", boardSelectedName);
            };

            // Save splitter position
            splitContainerSchematics.SplitterMoved += (s, e) =>
            {
                Configuration.SaveSetting("SplitterPosition", splitContainerSchematics.SplitterDistance.ToString());
            };

            /*
            // Save selected image when changed
            panelListAutoscroll.ControlAdded += (s, e) =>
            {
                if (e.Control is Panel panel && panel.Name == schematicSelectedName)
                {
                    Configuration.SaveSetting("ThumbnailImage", schematicSelectedName);
                }
            };
            */
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

       

        private void AttachClosePopupOnClick(Control parent)
        {
            // Attach a single MouseDown event to close any open popup
            parent.MouseDown += (s, e) =>
            {
                // Only if we click in the main form and a popup is open
                if (componentInfoPopup != null && componentInfoPopup.Visible)
                {
                    componentInfoPopup.Close();
                    componentInfoPopup = null;
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
            // Show it modeless (non-blocking)
            string title = "";
            title = comp.Label;
            title += comp.NameTechnical != "?" ? " | " + comp.NameTechnical : "";
            title += comp.NameFriendly != "?" ? " | " + comp.NameFriendly : "";

            // Create new popup
            componentInfoPopup = new FormComponent(comp, hardwareSelectedFolder, boardSelectedFolder);
            componentInfoPopup.Text = title;
            componentInfoPopup.Show(this);
            componentInfoPopup.TopMost = true;
        }


        // ---------------------------------------------------------------------
        // Setup new board after user selects in comboBox2

        private void SetupNewBoard()
        {

            boardSelectedName = comboBoxBoard.SelectedItem.ToString();
            textBox1.Text = boardSelectedName; // feedback info

            var selectedHardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var selectedBoard = selectedHardware?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (selectedHardware == null || selectedBoard == null) return;

            hardwareSelectedFolder = selectedHardware.Folder;
            boardSelectedFolder = selectedBoard.Folder;

            // Default to first file
            schematicSelectedName = selectedBoard.Files.FirstOrDefault()?.Name;
            schematicSelectedFile = selectedBoard.Files.FirstOrDefault()?.FileName;
            textBox2.Text = schematicSelectedName; // feedback info

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
            UpdateTabOverview(selectedBoard);
            UpdateTabRessources(selectedBoard);
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
                if (foundBoard?.Components != null)
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
        }

        private void InitializeComponentList(bool clearList = true)
        {
            listBoxComponents.Items.Clear();
            listBoxNameValueMapping.Clear();

            var foundHardware = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var foundBoard = foundHardware?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (foundBoard != null)
            {
                if (foundBoard?.Components != null)
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
                Path.Combine(Application.StartupPath, hardwareSelectedFolder, boardSelectedFolder, schematicSelectedFile)
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
                Text = schematicSelectedName,
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
            //ReadaptThumbnails();
        }

        private void InitializeList()
        {
            StackTrace stackTrace = new StackTrace();
            StackFrame callerFrame = stackTrace.GetFrame(1);
            MethodBase callerMethod = callerFrame.GetMethod();
            string callerName = callerMethod.Name;
            Debug.WriteLine("[InitializeList] called from [" + callerName + "]");

            panelListAutoscroll.AutoScroll = true;
//            panelListAutoscroll.HorizontalScroll.Enabled = false;
//            panelListAutoscroll.HorizontalScroll.Visible = false;

            panelListAutoscroll.Controls.Clear();
            overlayPanelsList.Clear();
            overlayListZoomFactors.Clear();

            // Find relevant schematic images to show here
            var hw = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var bd = hw?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (bd == null) return;

            // Walkthrough each schematic image for this board
            foreach (BoardFileOverlays schematic in bd.Files)
            {
                string filename = Path.Combine(Application.StartupPath, hardwareSelectedFolder, boardSelectedFolder, schematic.FileName);

                // Panel that will hold the label and the image
                Panel panelThumbnail = new Panel
                {
                    Name = schematic.Name + "_container",
                    BorderStyle = BorderStyle.FixedSingle,
                    Padding = new Padding(3),
                    //Margin = new Padding(0),
                };

                Label labelThumbnail = new Label
                {
                    Text = schematic.Name,
                    AutoSize = false,
                    Dock = DockStyle.Top,
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor = Color.Khaki,
                    ForeColor = Color.Black,
                    Font = new Font("Calibri", 9),
                    //Padding = new Padding(0),
                    Height = 20
                };

                Panel panelImage = new Panel
                {
                    Name = schematic.Name,
                    BackgroundImage = Image.FromFile(filename),
                    BackgroundImageLayout = ImageLayout.Zoom,
                    //Dock = DockStyle.Fill,
                    Dock = DockStyle.None,
                    //Margin = new Padding(0)
                };

                // Overlay panel for the image that will ensure we can click anywhere on the image (to select it)
                OverlayPanel overlayPanel = new OverlayPanel
                {
                    Dock = DockStyle.Fill
                };

                // Add panels to each other
                panelListAutoscroll.Controls.Add(panelThumbnail);
                panelThumbnail.Controls.Add(labelThumbnail);
                panelThumbnail.Controls.Add(panelImage);
                panelImage.Controls.Add(overlayPanel);

                overlayPanelsList[schematic.Name] = overlayPanel;

                panelThumbnail.DoubleBuffered(true);
                labelThumbnail.DoubleBuffered(true);
                panelImage.DoubleBuffered(true);
                overlayPanel.DoubleBuffered(true);

                // Attach "MouseDown" event to the overlay panel
                overlayPanel.OverlayPanelMouseDown += (s, e2) =>
                {
                    if (e2.Button == MouseButtons.Left)
                        OnListImageLeftClicked(panelImage);
                };                

                // Attach "OverlayClicked" event to the overlay panel
                overlayPanel.OverlayClicked += (s, e2) =>
                {
                    if (e2.MouseArgs.Button == MouseButtons.Left)
                        OnListImageLeftClicked(panelImage);
                };
            }

            ReadaptThumbnails(); 
            DrawBorderInList();
            HighlightOverlays("list");
        }
        private void ReadaptThumbnails()
        {
            StackTrace stackTrace = new StackTrace();
            StackFrame callerFrame = stackTrace.GetFrame(1);
            MethodBase callerMethod = callerFrame.GetMethod();
            string callerName = callerMethod.Name;
            Debug.WriteLine("[ReadaptThumbnails] called from [" + callerName +"]");

            // Set initial values
            int scrollbarWidth = 0;
            if (panelListAutoscroll.VerticalScroll.Visible)
            {
                scrollbarWidth = SystemInformation.VerticalScrollBarWidth - 14;
            }
            int availableWidthForThumbnail = panelListAutoscroll.ClientSize.Width - scrollbarWidth;
            int availableHeightForThumbnail = panelListAutoscroll.ClientSize.Height;
            int yPosition = 5;

            // Walkthrough all (parent) panels (one panel is a thumbnail)
            foreach (Panel panelThumbnail in panelListAutoscroll.Controls.OfType<Panel>())
            {
                Label labelThumbnail = panelThumbnail.Controls[0] as Label;
                Panel panelImage = panelThumbnail.Controls[1] as Panel;

                int labelHeight = labelThumbnail.Height;
                float aspectRatio = (float)panelImage.BackgroundImage.Height / panelImage.BackgroundImage.Width;
                int newImageHeight = (int)(availableWidthForThumbnail * aspectRatio);

              
                panelThumbnail.Size = new Size(availableWidthForThumbnail, labelHeight + newImageHeight);
                panelThumbnail.Location = new Point(0, yPosition);
                panelImage.Size = new Size(availableWidthForThumbnail, newImageHeight - 5);
                panelImage.Location = new Point(0, labelHeight);

              

                yPosition += panelThumbnail.Height + 10;

                float scaleFactor = (float)panelImage.Width / panelImage.BackgroundImage.Width;
                string key = panelThumbnail.Name.Replace("_container", "");
                overlayListZoomFactors[key] = scaleFactor;
                if (overlayPanelsList.ContainsKey(key))
                {
                    overlayPanelsList[key].Bounds = panelImage.ClientRectangle;
                }

              

            }

            

            //panelListAutoscroll.AutoScrollMinSize = new Size(0, yPosition + 10);
            HighlightOverlays("list");

            if (panelListAutoscroll.HorizontalScroll.Visible)
            {
                //ReadaptThumbnails();
            }
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

        

        private void OnListImageLeftClicked(Panel pan)
        {
            schematicSelectedName = pan.Name;
            Configuration.SaveSetting("SelectedThumbnail", schematicSelectedName);  // Save selected image
            textBox2.Text = schematicSelectedName; // feedback info

            var hw = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var bd = hw?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (bd != null)
            {
                var file = bd.Files.FirstOrDefault(f => f.Name == schematicSelectedName);
                if (file != null)
                {
                    schematicSelectedFile = file.FileName;
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
            string selectedContainer = schematicSelectedName + "_container";
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
            textBoxFilterComponents.Text = "";
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

        private void comboBoxHardware_SelectedIndexChanged(object sender, EventArgs e)
        {
            comboBoxBoard.Items.Clear();
            hardwareSelectedName = comboBoxHardware.SelectedItem.ToString();
            textBox5.Text = hardwareSelectedName; // feedback info

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

        private void comboBoxBoard_SelectedIndexChanged(object sender, EventArgs e)
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
                if (bf.Name != schematicSelectedName) continue;

                if (bf?.Components != null)
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

                            overlayComponentsTab.Add(overlayPictureBox);
                            int idx = overlayComponentsTab.Count - 1;
                            overlayComponentsTabOriginalSizes[idx] = overlayPictureBox.Size;
                            overlayComponentsTabOriginalLocations[idx] = overlayPictureBox.Location;
                        }
                    }
                }
            }
        }

        // ---------------------------------------------------------------------
        // Handling highlight overlays

        private void HighlightOverlays(string scope)
        {
            if (this.WindowState == FormWindowState.Minimized || isResizing)
            {
                return;
            }

            var hw = classHardware.FirstOrDefault(h => h.Name == hardwareSelectedName);
            var bd = hw?.Boards.FirstOrDefault(b => b.Name == boardSelectedName);
            if (bd == null) return;

            if (scope == "tab")
            {
                // Draw overlays on the main image
                if (overlayPanel == null) return;

                var bf = bd.Files.FirstOrDefault(f => f.Name == schematicSelectedName);
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

                    if (bf?.Components != null)
                    {
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

        private void SplitContainer1_SplitterMoved(object sender, SplitterEventArgs e)
        {
            //InitializeList();
            ReadaptThumbnails();
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
            if (keyData == Keys.Space && tabControl.SelectedTab.Text != "Feedback")
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
                
        /*
        // What is this?
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        */

        private void button1_Click_1(object sender, EventArgs e)
        {
            string email = textBox3.Text;
            string feedback = textBox4.Text;

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
                string boardFile = foundBoard?.Datafile;
                string excelFilePath = Path.Combine(Application.StartupPath, foundHardware.Folder, boardFile);
                try
                {
                    WebClient webClient = new WebClient();
                    ServicePointManager.Expect100Continue = true;
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;
                    webClient.Headers.Add("user-agent", "CRT "+ versionThis);

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
                    if (checkBox1.Checked)
                    {
                        byte[] fileBytes = File.ReadAllBytes(excelFilePath);
                        string fileBase64 = Convert.ToBase64String(fileBytes);
                        data["attachment"] = fileBase64;
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
                    } else
                    {
                        string txt = "No feedback sent - did you fill in some text or attached the Excel data file?";
                        MessageBox.Show(txt,
                            "ERROR: Feedback not sent",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                }
                catch (WebException ex)
                {
                    MessageBox.Show("CRT cannot submit the feedback right now, please retry later. If the issue persists, then you can connect directly with the developer at [dennis@commodore-repair-toolbox.dk]."+ Environment.NewLine + Environment.NewLine + "The exact recieved HTTP error is:" + Environment.NewLine + Environment.NewLine + ex.Message,
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
        // Check if the email address typed in feedback is valid - not a very good check though!
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


        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            textBox6.Text = checkBox1.Checked ? boardSelectedFilename : "";
        }

    }

    // -------------------------------------------------------------------------
    // Class definitions

    // "Hardware" is read from the very first Excel file (Commodore-Repair-Toolbox.xlsx).
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
        //public string ImagePinout { get; set; } // hest - must be deleted once replaced!
        public string OneLiner { get; set; }
        public string Description { get; set; }
        public List<ComponentLocalFiles> LocalFiles { get; set; }
        public List<ComponentLinks> ComponentLinks { get; set; }
        public List<ComponentImages> ComponentImages { get; set; }
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
        public string Name { get; set; }
        public string FileName { get; set; }
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