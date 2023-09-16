using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using ExcelDataReader;
using System.Data;
using OfficeOpenXml;
using System.Xml.Linq;

namespace Commodore_Repair_Toolbox
{
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
        public List<Component> Components { get; set; }
    }

    public class File
    {
        public string Name { get; set; }
        public string FileName { get; set; }
        public string HighlightColorTab { get; set; }
        public string HighlightColorList { get; set; }
        public string HighlightOpacityTab { get; set; }
        public string HighlightOpacityList { get; set; }
        public List<Component> Components { get; set; }
    }

    public class Component
    {
        public string NameLabel { get; set; }
        public string NameTechnical { get; set; }
        public string NameFriendly { get; set; }
        public List<Overlay> Overlays { get; set; }
    }

    public class Overlay
    {
        public Rectangle Bounds { get; set; }
    }

        
    public partial class Main : Form
    {

        // UI elements
        private CustomPanel panelMain;
        private Panel panelImage;

        // Main variables
        private Image image;
        private float zoomFactor = 1.0f;
        private Point lastMousePosition;
        private Size overlayTab1_originalSize;
        private Point overlayTab1_originalLocation;
        private bool isResizedByMouseWheel = false;

        private string hardwareSelected = "Commodore 64 (Breadbin)";
        private string boardSelected = "250425";
        private string imageSelected = "Schematics 1 of 2";
        private string highlightTabColor = "";
        private string nameTechnical = "";

        // Overlay array in "Main" tab
        List<PictureBox> overlayComponentsList = new List<PictureBox>(); 
        List<PictureBox> overlayComponentsTab = new List<PictureBox>();
        Dictionary<string, Size> overlayComponentsTabOriginalSizes = new Dictionary<string, Size>();
        Dictionary<string, Point> overlayComponentsTabOriginalLocations = new Dictionary<string, Point>();

        List<Hardware> classHardware = new List<Hardware>();
        
        


        // ---------------------------------------------------------------------------------


        public Main()
        {
            InitializeComponent();






            // I am using this as "Polyform Noncommercial license"
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            // Get all hardware from the Excel data file
            using (var package = new ExcelPackage(new FileInfo(Application.StartupPath + "\\Data\\Data.xlsx")))
            {
                // Assuming data is in the first worksheet
                var worksheet = package.Workbook.Worksheets[0];

                // Find the row that starts with the "searchHeader"
                string searchHeader = "\"Hardware\" name in drop-down";
                int row = 1;
                while (row <= worksheet.Dimension.End.Row)
                {
                    if (worksheet.Cells[row, 1].Value != null && worksheet.Cells[row, 1].Value.ToString() == searchHeader)
                    {
                        break; // found the starting row
                    }
                    row++;
                }

                // Skip the header row
                row++;

                // Now, start reading data from the identified row
                while (worksheet.Cells[row, 1].Value != null)
                {
                    string name = worksheet.Cells[row, 1].Value.ToString();
                    string folder = worksheet.Cells[row, 2].Value.ToString();
                    Hardware hardware = new Hardware
                    {
                        Name = name,
                        Folder = folder,
                    };
                    classHardware.Add(hardware);
                    row++;
                }
            }

            // Read all boards in to the class
            List<Board> classBoard = new List<Board>();
            foreach (Hardware hardware in classHardware)
            {
                using (var package = new ExcelPackage(new FileInfo(Application.StartupPath + "\\Data\\" + hardware.Folder + "\\Data.xlsx")))
                {
                    var worksheet = package.Workbook.Worksheets[0];

                    // Find the row that starts with the "searchHeader"
                    string searchHeader = "\"Board\" name in drop-down";
                    int row = 1;
                    while (row <= worksheet.Dimension.End.Row)
                    {
                        if (worksheet.Cells[row, 1].Value != null && worksheet.Cells[row, 1].Value.ToString() == searchHeader)
                        {
                            break; // found the starting row
                        }
                        row++;
                    }

                    // Skip the header row
                    row++;

                    // Now, start reading data from the identified row
                    while (worksheet.Cells[row, 1].Value != null)
                    {
                        string name = worksheet.Cells[row, 1].Value.ToString();
                        string folder = worksheet.Cells[row, 2].Value.ToString();
                        Board boarda = new Board
                        {
                            Name = name,
                            Folder = folder,
                        };
                        classBoard.Add(boarda);
                        
                        // Associate the board with the hardware
                        // Create the "Boards" property if it is NULL and then add the board
                        if (hardware.Boards == null)
                        {
                            hardware.Boards = new List<Board>();
                        }
                        hardware.Boards.Add(boarda);
                        row++;
                    }
                }
            }

            // Read all files in to the class
            List<File> classFile = new List<File>();
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    using (var package = new ExcelPackage(new FileInfo(Application.StartupPath + "\\Data\\" + hardware.Folder + "\\" + board.Folder + "\\Data.xlsx")))
                    {
                        var worksheet = package.Workbook.Worksheets[0];

                        // Find the row that starts with the "searchHeader"
                        string searchHeader = "LIST IMAGES";
                        int row = 1;
                        while (row <= worksheet.Dimension.End.Row)
                        {
                            if (worksheet.Cells[row, 1].Value != null && worksheet.Cells[row, 1].Value.ToString() == searchHeader)
                            {
                                break; // found the starting row
                            }
                            row++;
                        }

                        // Skip the header row
                        row++;
                        row++;

                        // Now, start reading data from the identified row
                        while (worksheet.Cells[row, 1].Value != null)
                        {
                            string name = worksheet.Cells[row, 1].Value.ToString();
                            string file = worksheet.Cells[row, 2].Value.ToString();
                            File filea = new File
                            {
                                Name = name,
                                FileName = file,
                            };
                            classFile.Add(filea);

                            // Associate the board with the hardware
                            // Create the "Boards" property if it is NULL and then add the board
                            if (board.Files == null)
                            {
                                board.Files = new List<File>();
                            }
                            board.Files.Add(filea);
                            row++;
                        }
                    }
                }
            }

            // Read all components in to the class
            List<Component> classComponent = new List<Component>();
            foreach (Hardware hardware in classHardware)
            {
                foreach (Board board in hardware.Boards)
                {
                    bool populateComponents = true; // only populate the components once to the "board" component list
                    foreach (File file in board.Files)
                    {

                        using (var package = new ExcelPackage(new FileInfo(Application.StartupPath + "\\Data\\" + hardware.Folder + "\\" + board.Folder + "\\Data.xlsx")))
                        {
                            var worksheet = package.Workbook.Worksheets[0];

                            // Find the row that starts with the "searchHeader"
                            string searchHeader = "COMPONENTS";
                            int row = 1;
                            while (row <= worksheet.Dimension.End.Row)
                            {
                                if (worksheet.Cells[row, 1].Value != null && worksheet.Cells[row, 1].Value.ToString() == searchHeader)
                                {
                                    break; // found the starting row
                                }
                                row++;
                            }

                            // Skip the header row
                            row++;
                            row++;

                            // Now, start reading data from the identified row
                            while (worksheet.Cells[row, 1].Value != null)
                            {
                                string name = worksheet.Cells[row, 1].Value.ToString();
                                Component component = new Component
                                {
                                    NameLabel = name,
                                };
                                classComponent.Add(component);

                                // Associate the board with the hardware
                                // Create the "Boards" property if it is NULL and then add the board
                                if(populateComponents)
                                {
                                    if (board.Components == null)
                                    {
                                        board.Components = new List<Component>();
                                    }
                                    board.Components.Add(component);
                                }
                                if (file.Components == null)
                                {
                                    file.Components = new List<Component>();
                                }
                                file.Components.Add(component);
                                row++;
                            }
                        }
                        populateComponents = false;
                    }
                }
            }

            // Now you have a list of hardware, each containing a list of associated boards
            foreach (Hardware hardware in classHardware)
            {
                Debug.WriteLine("Hardware Name = " + hardware.Name + ", Folder = " + hardware.Folder);
                foreach (Board board in hardware.Boards)
                {
                    Debug.WriteLine("  Board Name = " + board.Name + ", Folder = " + board.Folder);
                    foreach (File file in board.Files)
                    {
                        Debug.WriteLine("    File Name = " + file.Name + ", FileName = " + file.FileName);
                        foreach (Component component in file.Components)
                        {
                            Debug.WriteLine("      Component Name = " + component.NameLabel);
                        }
                    }
                    Debug.WriteLine("  Board Name = " + board.Name + ", Folder = " + board.Folder); 
                    foreach (Component component in board.Components)
                    {
                        Debug.WriteLine("    Component Name = " + component.NameLabel);
                    }
                }
            }







            // Load the active image
            image = Image.FromFile(Application.StartupPath + "\\Data\\Commodore 64 Breadbin\\250425\\Schematics 1of2.gif");

            // Create the array that will be used for all overlays
            CreateOverlayArrays("tab");
            CreateOverlayArrays("list");

            // Initialize the two parts in the "Main" tab, the zoomable area and the image list
            InitializeTabMain();
            InitializeList();

            /*
             
            // Load Excel Data
            var excelData = ReadExcel(Application.StartupPath + "\\Data\\Commodore 64 Breadbin\\250425\\Data.xlsx");

            foreach (DataColumn column in excelData.Columns)
            {
                Console.WriteLine(column.ColumnName);
            }

            // Load JSON Data
            var jsonPath = Application.StartupPath + "\\Data\\Commodore 64 Breadbin\\250425\\Highlighting.json";
            var jsonData = File.ReadAllText(jsonPath);
            var jsonHighlightData = JsonConvert.DeserializeObject<Dictionary<string, List<Highlight>>>(jsonData);

            // Work with Excel Data
            foreach (DataRow row in excelData.Rows)
            {
                string name = row["Name"].ToString();
                string file = row["File"].ToString();
                Console.WriteLine($"Name: {name}, File: {file}");
            }

            // Work with JSON Data
            foreach (var entry in jsonHighlightData)
            {
                string schematicName = entry.Key;
                foreach (var highlight in entry.Value)
                {
                    string tabColor = highlight.HighlightTabColor;
                    // Access other highlight properties and components
                }
            }

            */

            /*

            // Walk through each "hardware"
            foreach (var hardware in data)
            {
                Console.WriteLine("Key: " + hardware.Key);
            }

            // Walk through each "board" within the "hardware"
            foreach (var board in data[hardwareSelected])
            {
                Console.WriteLine("Key: " + board.Key);
            }

            // Walk through each "image" within the "board"
            var imageDict = data[hardwareSelected][boardSelected]["image"] as Dictionary<string, object>;
            if (imageDict != null)
            {
                foreach (var image in imageDict)
                {
                    Console.WriteLine("Key: " + image.Key);
                }
            }

            // Walk through each "component" within the "board"
            var componentDict = data[hardwareSelected][boardSelected]["component"] as Dictionary<string, object>;
            if (componentDict != null)
            {
                foreach (var component in componentDict)
                {
                    Console.WriteLine("Key: " + component.Key);
                }
            }

            // Get specific string value from "image"
            string keyToFind = "component-highlight-tab-color";
            highlightTabColor = imageFindString(keyToFind);
            Debug.WriteLine(highlightTabColor);

            string componentCategory = "component";
            string componentSelected = "U1";
            keyToFind = "technical";

            try
            {
                var imageMainDict = data[hardwareSelected][boardSelected]["image"] as Dictionary<string, object>;
                if (imageMainDict == null) throw new KeyNotFoundException("Image main dictionary is null or wrong type.");

                if (!imageMainDict.ContainsKey(imageSelected)) throw new KeyNotFoundException($"Key '{imageSelected}' not found in image main dictionary.");

                imageDict = imageMainDict[imageSelected] as Dictionary<string, object>;
                if (imageDict == null) throw new KeyNotFoundException("Image dictionary is null or wrong type.");

                if (!imageDict.ContainsKey(componentCategory)) throw new KeyNotFoundException($"Key '{componentCategory}' not found in image dictionary.");

                componentDict = imageDict[componentCategory] as Dictionary<string, object>;
                if (componentDict == null) throw new KeyNotFoundException("Component dictionary is null or wrong type.");

                if (!componentDict.ContainsKey(componentSelected)) throw new KeyNotFoundException($"Key '{componentSelected}' not found in component dictionary.");

                var specificComponentDict = componentDict[componentSelected] as Dictionary<string, object>;
                if (specificComponentDict == null) throw new KeyNotFoundException("Specific component dictionary is null or wrong type.");

                if (!specificComponentDict.ContainsKey(keyToFind)) throw new KeyNotFoundException($"Key '{keyToFind}' not found in specific component dictionary.");

                var value = specificComponentDict[keyToFind];
                Console.WriteLine($"{keyToFind} = {value}");
            }
            catch (KeyNotFoundException e)
            {
                Console.WriteLine(e.Message);
            }

            */

        }


        // ---------------------------------------------------------------------------------


        /*
         
        public static DataTable ReadExcel(string path)
        {
            DataTable dt = new DataTable();
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var result = reader.AsDataSet(new ExcelDataSetConfiguration()
                    {
                        ConfigureDataTable = (tableReader) => new ExcelDataTableConfiguration()
                        {
                            UseHeaderRow = true,
                            FilterRow = (rowReader) => {
                                return rowReader.Depth >= 2; // Skip the first 2 rows, adjust this number as per your Excel file
                            }
                        }
                    });
                    dt = result.Tables[0];
                }
            }
            return dt;
        }

        private string imageFindString(string keyToFind)
        {
            var imageMainDict = data[hardwareSelected][boardSelected]["image"] as Dictionary<string, object>;
            if (imageMainDict != null && imageMainDict.ContainsKey(imageSelected))
            {
                var imageDict = imageMainDict[imageSelected] as Dictionary<string, object>;
                if (imageDict != null && imageDict.ContainsKey(keyToFind))
                {
                    return (string)imageDict[keyToFind];
                }
            }
            return "";
        }

        private string componentFindString(string component, string keyToFind)
        {
            var imageMainDict = data[hardwareSelected][boardSelected]["image"] as Dictionary<string, object>;
            if (imageMainDict != null && imageMainDict.ContainsKey(imageSelected))
            {
                var componentDict = imageMainDict[imageSelected] as Dictionary<string, object>;
                if (componentDict != null && componentDict.ContainsKey(component))
                {
                    var specificComponentDict = componentDict[keyToFind] as Dictionary<string, object>;
                    if (specificComponentDict != null && specificComponentDict.ContainsKey(keyToFind))
                    {
                        return (string)specificComponentDict[keyToFind];
                    }
                }
            }
            return "";
        }

        */


        // ---------------------------------------------------------------------------------


        private void InitializeTabMain()
        {

            // Initialize main panel, make it part of the "tabMain" and fill the entire size
            panelMain = new CustomPanel
            {
                Size = new Size(panel1.Width - panel2.Width - 25, panel1.Height),
                AutoScroll = true,
                Dock = DockStyle.Fill,
            };
            panel1.Controls.Add(panelMain);

            // Initialize image panel
            panelImage = new Panel
            {
                Size = image.Size,
                BackgroundImage = image,
                BackgroundImageLayout = ImageLayout.Zoom,
                Dock = DockStyle.None,
            };
            panelMain.Controls.Add(panelImage);

            // Enable double buffering for smoother updates
            panelMain.DoubleBuffered(true);
            panelImage.DoubleBuffered(true);

            // Create all overlays defined in the array
            foreach (PictureBox overlayTab in overlayComponentsTab)
            {
                panelImage.Controls.Add(overlayTab);
                overlayTab.DoubleBuffered(true);

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

            Panel panelList1;
            Panel panelList2;
            Label labelList1;
            PictureBox overlayList1;

            // Initialize main panel, make it part of the "tabMain" and fill the entire size
            panelList1 = new Panel
            {
                Size = new Size(panel2.Width, panel2.Height),
                AutoScroll = true,
                Dock = DockStyle.Fill,
            };
            panel2.Controls.Add(panelList1);

            // Initialize image panel
            panelList2 = new Panel
            {
                Size = image.Size,
                BackgroundImage = image,
                BackgroundImageLayout = ImageLayout.Zoom,
                Dock = DockStyle.None,
                //BorderStyle = BorderStyle.FixedSingle,
            };
            panelList1.Controls.Add(panelList2);

            // Add the Paint event handler to draw the border
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

            labelList1 = new Label
            {
                Text = "Schematics 1 of 2",
                Location = new Point(0, 0),
                BorderStyle = BorderStyle.FixedSingle,
                AutoSize = true,
                BackColor = Color.White,
                Padding = new Padding(left:2, top:2, right:2, bottom:2),
            };
            panelList2.Controls.Add(labelList1);

            panelList1.DoubleBuffered(true);
            panelList2.DoubleBuffered(true);
            labelList1.DoubleBuffered(true);

            // Create all overlays defined in the array
            foreach (PictureBox overlayList in overlayComponentsList)
            {
                panelList2.Controls.Add(overlayList);
                overlayList.DoubleBuffered(true);
            }

            // Set the zoom factor for the size of the panel
            float xZoomFactor = (float)panelList1.Width / image.Width;
            float yZoomFactor = (float)panelList1.Height / image.Height;
            float zoomFactor = Math.Min(xZoomFactor, yZoomFactor);

            // Update the image based on the zoom factor
            panelList2.Size = new Size((int)(image.Width * zoomFactor), (int)(image.Height * zoomFactor));

            HighlightOverlays("list", zoomFactor);
        }


        // ---------------------------------------------------------------------------------


        private void HighlightOverlays (string scope, float zoomFactor)
        {

            if (scope == "tab")
            {
                foreach (PictureBox overlay in overlayComponentsTab)
                {
                    Size originalSize = overlayComponentsTabOriginalSizes[overlay.Name];
                    Point originalLocation = overlayComponentsTabOriginalLocations[overlay.Name];
                    int newWidth = (int)(originalSize.Width * zoomFactor);
                    int newHeight = (int)(originalSize.Height * zoomFactor);
                    overlay.Size = new Size(newWidth, newHeight);
                    overlay.Location = new Point((int)(originalLocation.X * zoomFactor), (int)(originalLocation.Y * zoomFactor));

                    // Dispose the overlay transparent bitmap and create a new one (bitmaps cannot be resized)
                    if (overlay.Image != null)
                    {
                        overlay.Image.Dispose();
                    }
                    Bitmap newBmp = new Bitmap(newWidth, newHeight);
                    using (Graphics g = Graphics.FromImage(newBmp))
                    {
                        g.Clear(Color.FromArgb(128, Color.Red)); // 50% opacity
                    }
                    overlay.Image = newBmp;
                    overlay.DoubleBuffered(true);
                }
            }
            
            
            if (scope == "list")
            {
                foreach (PictureBox overlayList in overlayComponentsList)
                {
                    int newWidth = (int)(overlayList.Width * zoomFactor);
                    int newHeight = (int)(overlayList.Height * zoomFactor);
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
                }
            }

            
        }


        // ---------------------------------------------------------------------------------


        private void CreateOverlayArrays (string scope)
        {
            List<int> x = new List<int> { 1226, 2654, 833 };
            List<int> y = new List<int> { 1672, 1672, 589 };
            List<int> width = new List<int> { 250, 159, 100 };
            List<int> height = new List<int> { 410, 385, 91 };

            for (int i = 0; i <= 2; i++)
            {

                // Define the PictureBox
                PictureBox overlay = new PictureBox
                {
                    Name = $"U{i}",
                    Location = new Point(x[i], y[i]),
                    Size = new Size(width[i], height[i]),
                    BackColor = Color.Transparent,
                };

                // Add the PictureBox to the "Main" image
                if (scope == "tab")
                {
                    overlayComponentsTab.Add(overlay);
                    overlayComponentsTabOriginalSizes[overlay.Name] = overlay.Size;
                    overlayComponentsTabOriginalLocations[overlay.Name] = overlay.Location;
                }

                // Add the PictureBox to the "List" image (?????????????)
                if (scope == "list")
                {
                    overlayComponentsList.Add(overlay);
                }
                    
            }
        }


        // ---------------------------------------------------------------------------------


        /*
        private void Main_Shown(object sender, EventArgs e)
        {
            //panelMain.AutoScrollPosition = new Point(750, 400);FitImageToPanel();
        }
        */


        // ---------------------------------------------------------------------------------


        private void ResizeTabImage()
        {
            // Set the zoom factor
            float xZoomFactor = (float)panelMain.Width / image.Width;
            float yZoomFactor = (float)panelMain.Height / image.Height;
            zoomFactor = Math.Min(xZoomFactor, yZoomFactor);

            // Update the image size to the zoom factor
            panelImage.Size = new Size((int)(image.Width * zoomFactor), (int)(image.Height * zoomFactor));

            HighlightOverlays("tab",zoomFactor);
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

                // Update the scroll position of the containerPanel.
                panelMain.AutoScrollPosition = new Point(newScrollPosition.X - e.X, newScrollPosition.Y - e.Y);

                //HighlightTabOverlay();
                HighlightOverlays("tab", zoomFactor);

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