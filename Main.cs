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
        private CustomPanel panelList1;
        private Panel panelImage;
        private Panel panelList2;
        private PictureBox overlayTab1;
        private PictureBox overlayList1;

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
        private Size overlayList1_originalSize;
        private Point overlayList1_originalLocation;
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

        public Main()
        {
            InitializeComponent();

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

            InitializeTabMain();
            InitializeList();
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

            // Load an image and initialize image panel
            image = Image.FromFile(Application.StartupPath + "\\Data\\Commodore 64 Breadbin\\250425\\Schematics 1of2.gif");
            panelImage = new Panel
            {
                Size = image.Size,
                BackgroundImage = image,
                BackgroundImageLayout = ImageLayout.Zoom,
                Dock = DockStyle.None,
            };
            panelMain.Controls.Add(panelImage);

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

            // Attach event handlers for mouse events and form shown
            panelMain.CustomMouseWheel += new MouseEventHandler(PanelMain_MouseWheel);
            panelImage.MouseDown += PanelImage_MouseDown;
            panelImage.MouseUp += PanelImage_MouseUp;
            panelImage.MouseMove += PanelImage_MouseMove;
            this.Shown += new EventHandler(this.Main_Shown);
            panelMain.Resize += new EventHandler(this.PanelMain_Resize);

            overlayTab1.MouseDown += PanelImage_MouseDown;
            overlayTab1.MouseUp += PanelImage_MouseUp;
            overlayTab1.MouseMove += PanelImage_MouseMove;
            overlayTab1.MouseEnter += new EventHandler(this.Overlay_MouseEnter);
            overlayTab1.MouseLeave += new EventHandler(this.Overlay_MouseLeave);

            // Enable double buffering for smoother updates
            panelMain.DoubleBuffered(true);
            panelImage.DoubleBuffered(true);
        }


        // ###########################################################################################
        // InitializeTabMain()
        // ------------
        // Setup the tab named "Main"
        // ###########################################################################################

        private void InitializeList()
        {

            // Initialize main panel, make it part of the "tabMain" and fill the entire size
            panelList1 = new CustomPanel
            {
                Size = new Size(panel2.Width, panel2.Height),
                AutoScroll = true,
                Dock = DockStyle.Fill,
            };
            panel2.Controls.Add(panelList1);

            // Load an image and initialize image panel
            image = Image.FromFile(Application.StartupPath + "\\Data\\Commodore 64 Breadbin\\250425\\Schematics 1of2.gif");
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

            Label labelList1 = new Label
            {
                Text = "Schematics 1 of 2",
                Location = new Point(0, 0),
                BorderStyle = BorderStyle.FixedSingle,
                AutoSize = true,
                BackColor = Color.White,
                Padding = new Padding(left:2, top:2, right:2, bottom:2),
            };
            panelList2.Controls.Add(labelList1);

            // Initialize overlay PictureBox and store its original dimensions
            overlayList1 = new PictureBox
            {
                Name = "U3",
                Size = new Size(250, 410),
                Location = new Point(1226, 1672),
                BackColor = Color.Transparent,
            };
            panelList2.Controls.Add(overlayList1);
            overlayList1_originalSize = overlayList1.Size;
            overlayList1_originalLocation = overlayList1.Location;








            //            tabMain.Controls.Add(panelImageList); 
            //            panelImageList.AutoScroll = true;
            //            panelImageList.Location = new Point(300, 10);
            //panelImageList.Dock = DockStyle.Fill;
            //groupBoxList.Controls.Add(panelImageList);


            // ---

            /*
             
            PictureBox listBox1 = new PictureBox
            {
                Size = new Size(167, 110), // Set the size you want
                Location = new Point(4, 25), // Set the location within the parent control
                Image = Image.FromFile(Application.StartupPath + "\\Data\\Commodore 64 Breadbin\\250425\\Schematics 1of2.gif"), // Load an image from file
                SizeMode = PictureBoxSizeMode.Zoom, // Optional: Set how the image should be displayed
                                                    //                BorderStyle = BorderStyle.FixedSingle,
            };

            

            Label listLabel1 = new Label
            {
                Text = "Schematics 1 of 2",
                Location = new Point(0, 0),  // Set the location within the parent control
                BorderStyle = BorderStyle.FixedSingle,
                AutoSize = true,
                BackColor = Color.White,  // Set the background color to Red
                Padding = new Padding(left: 2, top: 2, right: 2, bottom: 2),
            };
            listBox1.Controls.Add(listLabel1);

            // ---
             
            PictureBox listBox2 = new PictureBox
            {
                Size = new Size(167, 110), // Set the size you want
                Location = new Point(4, 140), // Set the location within the parent control
                Image = Image.FromFile(Application.StartupPath + "\\Data\\Commodore 64 Breadbin\\250425\\Schematics 2of2.gif"), // Load an image from file
                SizeMode = PictureBoxSizeMode.Zoom, // Optional: Set how the image should be displayed
                BorderStyle = BorderStyle.FixedSingle,
            };

            panelImageList.Controls.Add(listBox2);

            Label listLabel2 = new Label
            {
                Text = "Schematics 2 of 2",
                Location = new Point(0, 0),  // Set the location within the parent control
                BorderStyle = BorderStyle.FixedSingle,
                AutoSize = true,
                BackColor = Color.White,  // Set the background color to Red
                Padding = new Padding(left: 2, top: 2, right: 2, bottom: 2),
            };
            listBox2.Controls.Add(listLabel2);

            // ---

            PictureBox listBox3 = new PictureBox
            {
                Size = new Size(167, 110), // Set the size you want
                Location = new Point(4, 255), // Set the location within the parent control
                Image = Image.FromFile(Application.StartupPath + "\\Data\\Commodore 64 Breadbin\\250425\\Board layout 250425.png"), // Load an image from file
                SizeMode = PictureBoxSizeMode.Zoom, // Optional: Set how the image should be displayed
                BorderStyle = BorderStyle.FixedSingle,
            };

            panelImageList.Controls.Add(listBox3);

            Label listLabel3 = new Label
            {
                Text = "Layout",
                Location = new Point(0, 0),  // Set the location within the parent control
                BorderStyle = BorderStyle.FixedSingle,
                AutoSize = true,
                BackColor = Color.White,  // Set the background color to Red
                Padding = new Padding(left: 2, top: 2, right: 2, bottom: 2),
            };
            listBox3.Controls.Add(listLabel3);

            // ---

            PictureBox listBox4 = new PictureBox
            {
                Size = new Size(167, 110), // Set the size you want
                Location = new Point(4, 370), // Set the location within the parent control
                Image = Image.FromFile(Application.StartupPath + "\\Data\\Commodore 64 Breadbin\\250425\\Print top.JPG"), // Load an image from file
                SizeMode = PictureBoxSizeMode.Zoom, // Optional: Set how the image should be displayed
                BorderStyle = BorderStyle.FixedSingle,
            };

            panelImageList.Controls.Add(listBox4);

            Label listLabel4 = new Label
            {
                Text = "Top",
                Location = new Point(0, 0),  // Set the location within the parent control
                BorderStyle = BorderStyle.FixedSingle,
                AutoSize = true,
                BackColor = Color.White,  // Set the background color to Red
                Padding = new Padding(left: 2, top: 2, right: 2, bottom: 2),
            };
            listBox4.Controls.Add(listLabel4);

            */

            // ---
            /*
            PictureBox listBox5 = new PictureBox
            {
                Size = new Size(167, 110), // Set the size you want
                Location = new Point(4, 490), // Set the location within the parent control
                Image = Image.FromFile(Application.StartupPath + "\\Data\\Schematics.gif"), // Load an image from file
                SizeMode = PictureBoxSizeMode.Zoom, // Optional: Set how the image should be displayed
                BorderStyle = BorderStyle.FixedSingle,
            };

            panelImageList.Controls.Add(listBox5);

            Label listLabel5 = new Label
            {
                Text = "Whatever",
                Location = new Point(0, 0),  // Set the location within the parent control
                BorderStyle = BorderStyle.FixedSingle,
                AutoSize = true,
                BackColor = Color.White,  // Set the background color to Red
                Padding = new Padding(left: 2, top: 2, right: 2, bottom: 2),
            };
            listBox5.Controls.Add(listLabel5);
            */

            // ---

            /*
             
            double ratio = (double)(100 - (double)(((double)(listBox1.Image.Width - listBox1.Width) * 100) / listBox1.Image.Width)) / 100;
            int newLocationX = (int)Math.Round(1226 * ratio, 8);
            int newLocationY = (int)Math.Round(1672 * ratio, 8);
            int newSizeWidth = (int)Math.Round(listBox1.Size.Width * ratio, 8);
            int newSizeHeight = (int)Math.Round(listBox1.Size.Height * ratio, 8);
            int newHeight = (int)(listBox1.Height);

            // Initialize overlay PictureBox and store its original dimensions
            overlayList1 = new PictureBox
            {
                Name = "U3",
                Size = new Size(6, 4),
                Location = new Point(45, 62),
                BackColor = Color.Transparent,
            };
            listBox1.Controls.Add(overlayList1);


            // Create a new bitmap with the new dimensions
            Bitmap newBmp = new Bitmap(overlayList1.Width, overlayList1.Height);

            // Perform drawing operations here, if any
            using (Graphics g = Graphics.FromImage(newBmp))
            {
                g.Clear(Color.FromArgb(128, Color.Red)); // 50% opacity
            }

            // Set the new bitmap
            overlayList1.Image = newBmp;
            */
        }


        // ###########################################################################################
        // Main_Shown()
        // ------------
        // What to do AFTER the Main() form has been shown (this is not the tab named "Main")?
        // ###########################################################################################

        private void Main_Shown(object sender, EventArgs e)
        {
            FitTabImageToPanel();
            FitListImageToPanel();

            //panelMain.AutoScrollPosition = new Point(750, 400);FitImageToPanel();
        }


        // ###########################################################################################
        // FitImageToPanel()
        // -----------------
        // Resize image to fit main panel display (show 100% of the image)
        // ###########################################################################################

        private void FitTabImageToPanel()
        {
            // Set the zoom factor
            float xZoomFactor = (float)panelMain.Width / image.Width;
            float yZoomFactor = (float)panelMain.Height / image.Height;
            zoomFactor = Math.Min(xZoomFactor, yZoomFactor);

            // Update the image size to the zoom factor
            panelImage.Size = new Size((int)(image.Width * zoomFactor), (int)(image.Height * zoomFactor));

            // HEST
            // Update the overlays
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
        }

        private void FitListImageToPanel()
        {
            // Set the zoom factor
            float xZoomFactor = (float)panelList1.Width / image.Width;
            float yZoomFactor = (float)panelList1.Height / image.Height;
            zoomFactor = Math.Min(xZoomFactor, yZoomFactor);

            // Update the image size to the zoom factor
            panelList2.Size = new Size((int)(image.Width * zoomFactor), (int)(image.Height * zoomFactor));

            // HEST
            // Update the overlays
            if (overlayList1 != null)
            {
                int newWidth = (int)(overlayList1_originalSize.Width * zoomFactor);
                int newHeight = (int)(overlayList1_originalSize.Height * zoomFactor);
                overlayList1.Size = new Size(newWidth, newHeight);
                overlayList1.Location = new Point((int)(overlayList1_originalLocation.X * zoomFactor), (int)(overlayList1_originalLocation.Y * zoomFactor));

                // Dispose the overlay transparent bitmap and create a new one (bitmaps cannot be resized)
                if (overlayList1.Image != null)
                {
                    overlayList1.Image.Dispose();
                }
                Bitmap newBmp = new Bitmap(newWidth, newHeight);
                using (Graphics g = Graphics.FromImage(newBmp))
                {
                    g.Clear(Color.FromArgb(128, Color.Red)); // 50% opacity
                }
                overlayList1.Image = newBmp;
            }
        }


        private void PanelMain_Resize(object sender, EventArgs e)
        {
            if (!isResizedByMouseWheel)
            {
                FitTabImageToPanel();
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

                if (overlayTab1 != null)
                {
                    int newWidth = (int)(overlayTab1_originalSize.Width * zoomFactor);
                    int newHeight = (int)(overlayTab1_originalSize.Height * zoomFactor);

                    overlayTab1.Size = new Size(newWidth, newHeight);
                    overlayTab1.Location = new Point((int)(overlayTab1_originalLocation.X * zoomFactor), (int)(overlayTab1_originalLocation.Y * zoomFactor));

                    // Dispose of the old bitmap
                    if (overlayTab1.Image != null)
                    {
                        overlayTab1.Image.Dispose();
                    }

                    // Create a new bitmap with the new dimensions
                    Bitmap newBmp = new Bitmap(newWidth, newHeight);

                    // Perform drawing operations here, if any
                    using (Graphics g = Graphics.FromImage(newBmp))
                    {
                        g.Clear(Color.FromArgb(128, Color.Red)); // 50% opacity
                    }

                    // Set the new bitmap
                    overlayTab1.Image = newBmp;
                }

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
                //                label7.Text = control.Name;
            }
            //            label7.Visible = true;
        }

        private void Overlay_MouseLeave(object sender, EventArgs e)
        {
            this.Cursor = Cursors.Default;
            //            label7.Visible = false;
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