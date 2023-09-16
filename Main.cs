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

namespace Commodore_Repair_Toolbox
{
    public partial class Main : Form
    {

        // UI elements
        private CustomPanel panelMain;
        private Panel panelImage;
//        private PictureBox overlayTab1;

        /*
        private Dictionary<string, Dictionary<string, Dictionary<string, object>>> data =
            new Dictionary<string, Dictionary<string, Dictionary<string, object>>>
            {
                {
                    "Commodore 64 (Breadbin)",
                    new Dictionary<string, Dictionary<string, object>>
                    {
                        {
                            "250425",
                            new Dictionary<string, object>
                            {
                                {
                                    "image",
                                    new Dictionary<string, object>
                                    {
                                        {
                                            "Schematics 1 of 2",
                                            new Dictionary<string, object>
                                            {
                                                { "file", @"\Data\Commodore 64 Breadbin\250425\Schematics 1of2.gif" },
                                                { "component-highlight-tab-color", "Yellow" },
                                                { "component-highlight-tab-opacity", "80%" },
                                                { "component-highlight-list-color", "Red" },
                                                { "component-highlight-list-opacity", "100%" },
                                                {
                                                    "component",
                                                    new Dictionary<string, object>
                                                    {
                                                        {
                                                            "U1",
                                                            new Dictionary<string, object>
                                                            {
                                                                {
                                                                    "location",
                                                                    new List<Dictionary<string, int>>
                                                                    {
                                                                        new Dictionary<string, int> { { "x", 1280 }, { "y", 1655 }, { "width", 110 }, { "height", 55 } },
                                                                        new Dictionary<string, int> { { "x", 1000 }, { "y", 750 }, { "width", 85 }, { "height", 100 } }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                },
                                            }
                                        },
                                        { "Schematics 2 of 2", new Dictionary<string, object> { { "file", @"\Data\Commodore 64 Breadbin\250425\Schematics 2of2.gif" } } },
                                        { "Layout", new Dictionary<string, object> { { "file", @"\Data\Commodore 64 Breadbin\250425\Board layout 250425.png" } } },
                                        { "Top", new Dictionary<string, object> { { "file", @"\Data\Commodore 64 Breadbin\250425\Print top.JPG" } } }
                                    }
                                },
                                {
                                    "component",
                                    new Dictionary<string, object>
                                    {
                                        {
                                            "U1",
                                            new Dictionary<string, object>
                                            {
                                                { "file", @"\Data\Commodore 64 Breadbin\250425\U3_pinout.gif" },
                                                { "technical", "6526" },
                                                { "friendly", "CIA 1" },
                                                { "type", "IC" },
                                                { "oneliner", "I/O chip with Serial and Parallel port" },
                                                { "description", "Blaa...." },
                                                {
                                                    "datasheet",
                                                    new Dictionary<string, string>
                                                    {
                                                        { "6526 datasheet 1", @"\Data\Commodore 64 Breadbin\250425\U3_sheet1.pdf" },
                                                        { "6526 datasheet 2", "https://whatever.com" }
                                                    }
                                                },
                                                {
                                                    "link",
                                                    new Dictionary<string, string>
                                                    {
                                                        { "A good reference", "https://yea.haga./dh" },
                                                        { "Beginners guide", "https://whatever.com" }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                {
                    "Commodore 64 model C",
                    new Dictionary<string, Dictionary<string, object>>
                    {
                        {
                            "321212",
                            new Dictionary<string, object>
                            {
                                {
                                    "image",
                                    new Dictionary<string, object>
                                    {
                                        // Copy the same structure as in the previous model here...
                                    }
                                },
                                {
                                    "component",
                                    new Dictionary<string, object>
                                    {
                                        // Copy the same structure as in the previous model here...
                                    }
                                }
                            }
                        }
                    }
                }
            };
        */

        // Main variables
        private Image image;
        private float zoomFactor = 1.0f;
        private Point lastMousePosition;
        private Size overlayTab1_originalSize;
        private Point overlayTab1_originalLocation;
        private bool isResizedByMouseWheel = false;


        // ###########################################################################################
        // Main()
        // -----------------
        // This is where it all starts :-)
        // ###########################################################################################

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

        public Main()
        {
            InitializeComponent();

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

            // Create all overlays defined in the array
            foreach (PictureBox overlayTab in overlayComponentsTab)
            {

//                PictureBox overlayTab = new PictureBox
//                {
//                    Name = $"U{i}",
//                    Size = new Size(250, 410),
//                    Location = new Point(1226+(i * 270), 1672+(i * 270)),
//                    BackColor = Color.Transparent,
//                };
                panelImage.Controls.Add(overlayTab);

                overlayTab.MouseDown += PanelImage_MouseDown;
                overlayTab.MouseUp += PanelImage_MouseUp;
                overlayTab.MouseMove += PanelImage_MouseMove;
                overlayTab.MouseEnter += new EventHandler(this.Overlay_MouseEnter);
                overlayTab.MouseLeave += new EventHandler(this.Overlay_MouseLeave);

//                overlayComponents.Add(overlayTab);
//                overlayComponentsOriginalSizes[overlayTab.Name] = overlayTab.Size;
//                overlayComponentsOriginalLocations[overlayTab.Name] = overlayTab.Location;
            }

            /*
            // Initialize overlay PictureBox and store its original dimensions
            overlayTab1 = new PictureBox
            {
                Name = "U3",
                Size = new Size(250, 410),
                Location = new Point(1226, 1672),
                BackColor = Color.Transparent,
            };
            panelImage.Controls.Add(overlayTab1);
            overlayTab1_originalSize = overlayTab1.Size;
            overlayTab1_originalLocation = overlayTab1.Location;
            */

            ResizeTabImage();

            // Attach event handlers for mouse events and form shown
            panelMain.CustomMouseWheel += new MouseEventHandler(PanelMain_MouseWheel);
            panelImage.MouseDown += PanelImage_MouseDown;
            panelImage.MouseUp += PanelImage_MouseUp;
            panelImage.MouseMove += PanelImage_MouseMove;
            //            this.Shown += new EventHandler(this.Main_Shown);
            panelMain.Resize += new EventHandler(this.PanelMain_Resize);

//            overlayTab1.MouseDown += PanelImage_MouseDown;
//            overlayTab1.MouseUp += PanelImage_MouseUp;
//            overlayTab1.MouseMove += PanelImage_MouseMove;
//            overlayTab1.MouseEnter += new EventHandler(this.Overlay_MouseEnter);
//            overlayTab1.MouseLeave += new EventHandler(this.Overlay_MouseLeave);

            // Enable double buffering for smoother updates
            panelMain.DoubleBuffered(true);
            panelImage.DoubleBuffered(true);
        }

        private void panelMain_MouseEnter(object sender, EventArgs e)
        {
            panelMain.Focus();
        }

        // ###########################################################################################
        // InitializeTabMain()
        // ------------
        // Setup the tab named "Main"
        // ###########################################################################################

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

            //            overlayList1 = new PictureBox
            //            {
            //                Name = "U3",
            //                Size = new Size(250, 410),
            //                Location = new Point(1226, 1672),
            //                BackColor = Color.Transparent,
            //            };
            //            panelList2.Controls.Add(overlayList1);

            // Create all overlays defined in the array
            foreach (PictureBox overlayList in overlayComponentsList)
            {
                panelList2.Controls.Add(overlayList);
            }

            // Set the zoom factor for the size of the panel
            float xZoomFactor = (float)panelList1.Width / image.Width;
            float yZoomFactor = (float)panelList1.Height / image.Height;
            float zoomFactor = Math.Min(xZoomFactor, yZoomFactor);

            // Update the image size to the zoom factor
            panelList2.Size = new Size((int)(image.Width * zoomFactor), (int)(image.Height * zoomFactor));

            ///*
            // Highlight the overlay
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
            }
            //*/
        }


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


        private void Main_Shown(object sender, EventArgs e)
        {
            //FitTabImageToPanel();

            //panelMain.AutoScrollPosition = new Point(750, 400);FitImageToPanel();
        }


        // ###########################################################################################
        // FitImageToPanel()
        // -----------------
        // Resize image to fit main panel display (show 100% of the image)
        // ###########################################################################################

        private void ResizeTabImage()
        {
            // Set the zoom factor
            float xZoomFactor = (float)panelMain.Width / image.Width;
            float yZoomFactor = (float)panelMain.Height / image.Height;
            zoomFactor = Math.Min(xZoomFactor, yZoomFactor);

            // Update the image size to the zoom factor
            panelImage.Size = new Size((int)(image.Width * zoomFactor), (int)(image.Height * zoomFactor));

            HighlightTabOverlay();
        }

        private void HighlightTabOverlay()
        {

            // Highlight all overlays from the array
            foreach (PictureBox overlayComponent in overlayComponentsTab)
            {

                if (overlayComponent != null)
                {
                    Size originalSize = overlayComponentsTabOriginalSizes[overlayComponent.Name];
                    Point originalLocation = overlayComponentsTabOriginalLocations[overlayComponent.Name];
                    int newWidth = (int)(originalSize.Width * zoomFactor);
                    int newHeight = (int)(originalSize.Height * zoomFactor);
                    overlayComponent.Size = new Size(newWidth, newHeight);
                    overlayComponent.Location = new Point((int)(originalLocation.X * zoomFactor), (int)(originalLocation.Y * zoomFactor));

                    // Dispose the overlay transparent bitmap and create a new one (bitmaps cannot be resized)
                    if (overlayComponent.Image != null)
                    {
                        overlayComponent.Image.Dispose();
                    }
                    Bitmap newBmp = new Bitmap(newWidth, newHeight);
                    using (Graphics g = Graphics.FromImage(newBmp))
                    {
                        g.Clear(Color.FromArgb(128, Color.Red)); // 50% opacity
                    }
                    overlayComponent.Image = newBmp;
                }
            }

            /*
            // Update the overlay
            if (overlayTab1 != null)
            {
                int newWidth = (int)(overlayTab1_originalSize.Width * zoomFactor);
                int newHeight = (int)(overlayTab1_originalSize.Height * zoomFactor);
                overlayTab1.Size = new Size(newWidth, newHeight);
                overlayTab1.Location = new Point((int)(overlayTab1_originalLocation.X * zoomFactor), (int)(overlayTab1_originalLocation.Y * zoomFactor));

                // Dispose the overlay transparent bitmap and create a new one (bitmaps cannot be resized)
                if (overlayTab1.Image != null)
                {
                    overlayTab1.Image.Dispose();
                }
                Bitmap newBmp = new Bitmap(newWidth, newHeight);
                using (Graphics g = Graphics.FromImage(newBmp))
                {
                    g.Clear(Color.FromArgb(128, Color.Red)); // 50% opacity
                }
                overlayTab1.Image = newBmp;
            }
            */
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

                HighlightTabOverlay();

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
                label3.Text = control.Name;
            }
            label3.Visible = true;
        }

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