using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;

namespace Commodore_Repair_Toolbox
{
    public partial class FormComponent : Form
    {
        private readonly Dictionary<string, string> localFiles = new Dictionary<string, string>();
        private readonly Dictionary<string, string> links = new Dictionary<string, string>();
        private List<string> imagePaths = new List<string>();
        private int currentImageIndex = 0;
        private readonly BoardComponents component;
        private Timer scrollTimer = new Timer();
        private bool isScrolling = false;
        private Main main; // instance of the "Main" form
        private List<PictureBox> thumbnailPictureBoxes = new List<PictureBox>();
        private int thumbnailWindowStart = 0; // Index in imagePaths for the first visible thumbnail
        private DateTime lastArrowKeyTime = DateTime.MinValue;
        private List<ComponentImages> filteredComponentImages = new List<ComponentImages>();

        public string PictureBoxName { get; }

        public FormComponent(BoardComponents component, Main main)
        {
            InitializeComponent();

            this.main = main; // assign the Main instance

            // Assign the passed-in component to the class-level field
            this.component = component;

            buttonRegionPal.Click += ButtonRegionPal_Click;
            buttonRegionNtsc.Click += ButtonRegionNtsc_Click;

            // Initialize the scroll timer
            scrollTimer.Interval = 1;
            scrollTimer.Tick += (s, e) => { 
                isScrolling = false; 
                scrollTimer.Stop(); 
            };

            // Bind the MouseClick event to reset the image index
            pictureBox1.MouseClick += PictureBox1_MouseClick;

            PictureBoxName = component.Label;

            // Basic labels
            label1.Text = Main.ConvertStringToLabel(component.Label);
            label2.Text = Main.ConvertStringToLabel(component.NameTechnical);
            label3.Text = Main.ConvertStringToLabel(component.NameFriendly);
            label4.Text = component.Type;
            textBox2.Text = Main.ConvertStringToLabel(component.OneLiner);

            // Description box
            //            textBox1.Text = "";
            //            if (component.ComponentImages != null && component.ComponentImages.Count > currentImageIndex)
            //            {
            //                textBox1.Text = component.ComponentImages[currentImageIndex].Note ?? "";
            //            }
            //            textBox1.ScrollBars = ScrollBars.Vertical;

            //            LoadImageUserNotes(); // will override default texts, if any

            // Define an array with all pinout images
            /*
            imagePaths = new List<string>(); // ensure it exists by default
            if (component.ComponentImages != null && component.ComponentImages.Count > 0)
            {
                // Ensure the imagePaths list is populated in the same order as component.ComponentImages
                imagePaths = component.ComponentImages
                    .Select(image => Path.Combine(Application.StartupPath, image.FileName))
                    .Where(File.Exists)
                    .ToList();
            }
            */

            Main.selectedRegion = Configuration.GetSetting("SelectedRegion", "PAL");
            FilterImagesByRegion();

            LayoutThumbnails();
            UpdateImage();

            // Bind the MouseWheel event
            pictureBox1.MouseWheel += PictureBox1_MouseWheel;

            // Local files (datasheets, etc.)
            if (component.LocalFiles != null)
            {
                foreach (var localFile in component.LocalFiles)
                {
                    listBox1.Items.Add(localFile.Name);
                    localFiles[localFile.Name] = localFile.FileName;
                }
            }

            // Links (URLs)
            if (component.ComponentLinks != null)
            {
                foreach (var linkItem in component.ComponentLinks)
                {
                    listBox2.Items.Add(linkItem.Name);
                    links[linkItem.Name] = linkItem.Url;
                }
            }

            // Allow pressing Escape to close
            KeyPreview = true;
            KeyPress += Form_KeyPress;

            // Focus on something that is not the first "textBox" in the form
            ActiveControl = label1;

            //ResizeEnd += Form_ResizeEnd;
            Resize += Form_Resize;

            Shown += Form_Shown;
        }

        private void Form_Shown(object sender, EventArgs e)
        {
            SetRegionButtonColors();

        }

        private void SetRegionButtonColors()
        {
            // Example: Set focus to buttonRegionPal if selectedRegion is "PAL"
            //            Debug.WriteLine(selectedRegion);
            if (Main.selectedRegion == "NTSC")
            {
                buttonRegionPal.FlatStyle = FlatStyle.Standard;
                buttonRegionPal.FlatAppearance.BorderColor = SystemColors.ControlDark;
                buttonRegionPal.FlatAppearance.BorderSize = 1;
                buttonRegionPal.BackColor = SystemColors.Control;
                buttonRegionPal.ForeColor = SystemColors.ControlText;
                buttonRegionNtsc.BackColor = Color.LightSteelBlue;
                buttonRegionNtsc.ForeColor = Color.Black;
                labelRegion.BackColor = Color.LightSteelBlue;
                labelRegion.ForeColor = Color.Black;
            }
            else
            {
                buttonRegionPal.FlatStyle = FlatStyle.Flat;
                buttonRegionPal.FlatAppearance.BorderColor = Color.DarkRed;
                buttonRegionPal.FlatAppearance.BorderSize = 1;
                buttonRegionPal.BackColor = Color.IndianRed;
                buttonRegionPal.ForeColor = Color.White;
                buttonRegionNtsc.BackColor = SystemColors.Control;
                buttonRegionNtsc.ForeColor = SystemColors.ControlText;
                labelRegion.BackColor = Color.IndianRed;
                labelRegion.ForeColor = Color.White;
            }
            int palCount = component.ComponentImages.Count(img => string.IsNullOrEmpty(img.Region) || string.Equals(img.Region, "PAL", StringComparison.OrdinalIgnoreCase));
            int ntscCount = component.ComponentImages.Count(img => string.IsNullOrEmpty(img.Region) || string.Equals(img.Region, "NTSC", StringComparison.OrdinalIgnoreCase));
            buttonRegionPal.Text = $"PAL ({palCount})";
            buttonRegionNtsc.Text = $"NTSC ({ntscCount})";
        }

        private void ButtonRegionPal_Click(object sender, EventArgs e)
        {
            Main.selectedRegion = "PAL";
            Configuration.SaveSetting("SelectedRegion", "PAL");
            FilterImagesByRegion();
            SetRegionButtonColors();
            main.SetRegionButtonColors();
        }

        private void ButtonRegionNtsc_Click(object sender, EventArgs e)
        {
            Main.selectedRegion = "NTSC";
            Configuration.SaveSetting("SelectedRegion", "NTSC");
            FilterImagesByRegion();
            SetRegionButtonColors();
            main.SetRegionButtonColors();
        }

        private void FilterImagesByRegion()
        {
            if (component.ComponentImages != null && component.ComponentImages.Count > 0)
            {
                filteredComponentImages = component.ComponentImages
                    .Where(img =>
                        string.IsNullOrEmpty(Main.selectedRegion) ||
                        string.IsNullOrEmpty(img.Region) ||
                        string.Equals(img.Region, Main.selectedRegion, StringComparison.OrdinalIgnoreCase)
                    )
                    .ToList();

                imagePaths = filteredComponentImages
                    .Select(img => Path.Combine(Application.StartupPath, img.FileName))
                    .Where(File.Exists)
                    .ToList();

                // Remove any filteredComponentImages whose file doesn't exist
                filteredComponentImages = filteredComponentImages
                    .Where((img, idx) => File.Exists(Path.Combine(Application.StartupPath, img.FileName)))
                    .ToList();
            }
            else
            {
                imagePaths = new List<string>();
                filteredComponentImages = new List<ComponentImages>();
            }

            if (currentImageIndex >= imagePaths.Count)
                currentImageIndex = 0;

            LayoutThumbnails();
            UpdateImage();
        }

        private void Form_Resize(object sender, EventArgs e)
        {
            LayoutThumbnails();
        }


        private void RemoveThumbnails ()
        {
            // Remove old thumbnails and their labels from panel3
            foreach (var pb in thumbnailPictureBoxes)
            {
                // Remove and dispose all child controls (labels) from the PictureBox
                foreach (Control ctrl in pb.Controls.OfType<Label>().ToList())
                {
                    pb.Controls.Remove(ctrl);
                    ctrl.Dispose();
                }
                panel3.Controls.Remove(pb);
                pb.Dispose();
            }
            thumbnailPictureBoxes.Clear();
        }

        private void LayoutThumbnails()
        {

            labelImageX.Location = new Point(10, pictureBox1.Height - labelImageX.Height - 5);

            // Define width of "panel3"
            int panelThumbnailHeight = 80;
            int panelThumbnailWidth = 120;
            int panelWidth = panel3.Width;

            if (imagePaths.Count > 1)
            {
                            
                RemoveThumbnails();


                // Find how many panels we can show with width of "panelWidth"
                int panels = panelWidth / panelThumbnailWidth;
                if (panels < 1) panels = 1; // Always show at least one

                // Recalculate width so thumbnails fill the panel exactly
                int dynamicThumbnailWidth = panelWidth / panels;
                int dynamicThumbnailHeight = panelThumbnailHeight; // Keep height as before

                int y = panel3.Height - dynamicThumbnailHeight;
                for (int i = 0; i < panels; i++)
                {
                    PictureBox pictureBox = new PictureBox
                    {
                        Width = dynamicThumbnailWidth,
                        Height = dynamicThumbnailHeight,
                        Margin = new Padding(0),
                        BorderStyle = BorderStyle.FixedSingle,
                        BackColor = Color.WhiteSmoke,
                        Tag = i,
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Location = new Point(i * dynamicThumbnailWidth, y)
                    };
                    thumbnailPictureBoxes.Add(pictureBox);





//                    Skal til at den skal gemme REGION
//UserData | Commodore 128 and 128D | 310378 | U21 | DB6 | 1 | PAL | Notes = Default text aaaa NTSC





                    // Add label9 (Pin)
                    Label labelPin = new Label
                    {
                        AutoSize = true,
                        Height = 15,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Font = new Font("Calibri", 8, FontStyle.Regular),
                        BackColor = Color.Khaki,
                        ForeColor = Color.Black,
//                        Text = (i < component.ComponentImages?.Count)
                        Text = (i < filteredComponentImages?.Count)
//                            ? "Pin " + component.ComponentImages[i].Pin
                            ? "Pin " + filteredComponentImages[i].Pin
                            : "",
                        Location = new Point(5, 5)
                    };
                    pictureBox.Controls.Add(labelPin);
                    // Now that labelPin.Width is correct, center it horizontally
    //                labelPin.Location = new Point(
    //                    (pictureBox.Width - labelPin.Width) / 2,
    //                    5
    //                );

                    if (i < imagePaths.Count)
                    {
                        pictureBox.Image = Image.FromFile(imagePaths[i]);
                    }

                    pictureBox.Click += (sender, e) =>
                    {
                        int imgIdx = (int)((PictureBox)sender).Tag;
                        currentImageIndex = imgIdx;
                        UpdateImage();
                    };

                    pictureBox.Paint += (sender, e) =>
                    {
                        var pb = (PictureBox)sender;
                        if ((int)pb.Tag == currentImageIndex)
                        {
                            using (var pen = new Pen(Color.Red, 3))
                            {
                                pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                                e.Graphics.DrawRectangle(pen, 1, 1, pb.Width - 5, pb.Height - 6);
                            }
                        }
                    };

                    panel3.Controls.Add(pictureBox);
                    pictureBox.Controls.Add(labelPin);
                }

                // Invalidate all thumbnails
                foreach (var pb in thumbnailPictureBoxes)
                    pb.Invalidate();

            }

            // Resize the main picture box to fill the remaining space
            if (imagePaths.Count > 1)
            {
                pictureBox1.Size = new Size(panel3.Width, panel3.Height - panelThumbnailHeight - 5);
            } else
            {
                pictureBox1.Size = new Size(panel3.Width, panel3.Height);
            }
             
        }

        private void UpdateThumbnails()
        {
            int panels = thumbnailPictureBoxes.Count;
            int imagesCount = imagePaths.Count;

            // Adjust window so currentImageIndex is always visible
            if (currentImageIndex < thumbnailWindowStart)
                thumbnailWindowStart = currentImageIndex;
            else if (currentImageIndex >= thumbnailWindowStart + panels)
                thumbnailWindowStart = currentImageIndex - panels + 1;

            // Clamp window start
            if (thumbnailWindowStart < 0) thumbnailWindowStart = 0;
            if (thumbnailWindowStart > Math.Max(0, imagesCount - panels))
                thumbnailWindowStart = Math.Max(0, imagesCount - panels);

            // Update images, tags, and labels for each thumbnail PictureBox
            for (int i = 0; i < panels; i++)
            {
                int imgIdx = thumbnailWindowStart + i;
                var pb = thumbnailPictureBoxes[i];
                pb.Tag = imgIdx;
                if (imgIdx < imagesCount)
                {
                    pb.Image = Image.FromFile(imagePaths[imgIdx]);
                    pb.Visible = true;

                    // Update labels inside the PictureBox
                    var labels = pb.Controls.OfType<Label>().ToList();
                    if (labels.Count >= 1 && component.ComponentImages != null && imgIdx < component.ComponentImages.Count)
                    {
//                        var pinValue = component.ComponentImages[imgIdx].Pin;
                        var pinValue = filteredComponentImages[imgIdx].Pin;
//                        var nameValue = component.ComponentImages[imgIdx].Name;
                        var nameValue = filteredComponentImages[imgIdx].Name;
                        int pinInt;
                        if (int.TryParse(pinValue, out pinInt))
                        {
                            labels[0].Text = "Pin " + pinValue;
                        }
                        else
                        {
                            labels[0].Text = nameValue;
                        }
                    }
                }
                else
                {
                    pb.Image = null;
                    pb.Visible = false;

                    // Optionally clear label text if out of range
                    var labels = pb.Controls.OfType<Label>().ToList();
                    if (labels.Count >= 1)
                    {
                        labels[0].Text = "";
                    }
                }
                pb.Invalidate();
            }
        }




        private void PictureBox1_MouseClick(object sender, MouseEventArgs e)
        {
            // Reset the image index to the first image
            currentImageIndex = 0;

            // Give control to "pictureBox1"
            ActiveControl = pictureBox1;

            // Update the image
            UpdateImage();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Only go in here, if the input text area is NOT in focus
            if (textBox1.Focused || textBox2.Focused)
            {
                return base.ProcessCmdKey(ref msg, keyData);
            }

            // Delay logic for arrow keys
            if (keyData == Keys.Down || keyData == Keys.Right ||
                keyData == Keys.Up || keyData == Keys.Left)
            {
                var now = DateTime.Now;
                if ((now - lastArrowKeyTime).TotalMilliseconds < 50)
                    return true; // Ignore if less than 10ms since last arrow key

                lastArrowKeyTime = now;
            }

            if (keyData == Keys.Down || keyData == Keys.Right)
            {
                // Simulate scrolling down (next image)
                currentImageIndex = (currentImageIndex + 1) % imagePaths.Count;
                UpdateImage();
                return true;
            }
            else if (keyData == Keys.Up || keyData == Keys.Left)
            {
                // Simulate scrolling up (previous image)
                currentImageIndex = (currentImageIndex - 1 + imagePaths.Count) % imagePaths.Count;
                UpdateImage();
                return true;
            }
            else if (keyData == Keys.Space)
            {
                // Reset to the first image
                currentImageIndex = 0;
                UpdateImage();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            PictureBox1_MouseWheel(pictureBox1, e);
        }


        private void PictureBox1_MouseWheel(object sender, MouseEventArgs e)
        {
            if (isScrolling) return; // Ignore if already scrolling
            isScrolling = true;
            scrollTimer.Start();

            if (imagePaths.Count == 0) return;

            // Determine the scroll direction
            if (e.Delta > 0)
            {
                // Scroll up: move to the previous image
                currentImageIndex = (currentImageIndex - 1 + imagePaths.Count) % imagePaths.Count;
            }
            else if (e.Delta < 0)
            {
                // Scroll down: move to the next image
                currentImageIndex = (currentImageIndex + 1) % imagePaths.Count;
            }

            UpdateImage();
        }

        private void UpdateImage()
        {

            textBox1.TextChanged -= textBox1_TextChanged; // "Description"
            textBox2.TextChanged -= textBox2_TextChanged; // "OneLiner"
            panel1.Paint -= new PaintEventHandler(panel1_Paint);
            panel2.Paint -= new PaintEventHandler(panel2_Paint);
            
            // Default hide all labels
            labelPin.Visible = false;
            labelRegion.Visible = false;
            labelName.Visible = false;
            labelReading.Visible = false;
            labelImageX.Visible = false;

            if (imagePaths.Count == 0) return;

            // Update the image
            pictureBox1.Image = Image.FromFile(imagePaths[currentImageIndex]);

            // Update labels
//            string region = component.ComponentImages[currentImageIndex].Region;
//            string pin = component.ComponentImages[currentImageIndex].Pin;
//            string name = component.ComponentImages[currentImageIndex].Name;
            string region = filteredComponentImages[currentImageIndex].Region;
            string pin = filteredComponentImages[currentImageIndex].Pin;
            string name = filteredComponentImages[currentImageIndex].Name;
            string reading = filteredComponentImages[currentImageIndex].Reading;
            labelPin.Text = "Pin " + pin;
            labelName.Text = name;
            labelRegion.Text = region;
            labelPin.Visible = pin != "" ? true : false;
            labelName.Visible = name != "" ? true : false;
            labelRegion.Visible = region != "" ? true : false;
            if (labelPin.Visible)
            {
                labelName.Location = new Point(labelPin.Location.X + labelPin.Width + 3, labelName.Location.Y); 
            }
            else
            {
                labelName.Location = new Point(labelPin.Location.X, labelName.Location.Y);
            }
            if (labelName.Visible)
            {
                labelRegion.Location = new Point(labelName.Location.X + labelName.Width + 3, labelRegion.Location.Y);
            }
            else
            {
                labelRegion.Location = new Point(labelPin.Location.X + labelPin.Width + 3, labelRegion.Location.Y);
            }
//            labelRegion.Location = new Point(labelName.Location.X + labelName.Width + 3, labelRegion.Location.Y);
            if (labelRegion.Visible)
            {
                labelReading.Location = new Point(labelRegion.Location.X + labelRegion.Width + 3, labelReading.Location.Y);
            }
            else if (labelName.Visible)
            {
                labelReading.Location = new Point(labelName.Location.X + labelName.Width + 3, labelReading.Location.Y);
            }
            else
            {
                labelReading.Location = new Point(labelPin.Location.X + labelPin.Width + 3, labelReading.Location.Y);
            }

            // Find the "Description" from the "component.Oscilloscope" list, based on the component name and pin
//            var oscilloscopeItem = component.Oscilloscope?.FirstOrDefault(item => item.Name == name && item.Pin == pin);
//            string reading = oscilloscopeItem?.Reading ?? string.Empty;
            labelReading.Text = reading;
            labelReading.Visible = reading != "" ? true : false;

            // Count the number of images, and show a counter in "label13"
            if (imagePaths.Count > 1)
            {
                labelImageX.Text = $"Image {currentImageIndex + 1} of {imagePaths.Count}";
                // Place labelImageX at bottom left corner, alike labelPin in top left corner
                labelImageX.Location = new Point(10, pictureBox1.Height - labelImageX.Height - 5);
                labelImageX.Visible = true;
            } else
            {
                labelImageX.Visible = false;
            }




            // Show the Note for the current image
            if (component.ComponentImages != null && component.ComponentImages.Count > currentImageIndex)
            {
//                string loadedTxt = component.ComponentImages[currentImageIndex].Note ?? "";
                string loadedTxt = filteredComponentImages[currentImageIndex].Note ?? "";
                loadedTxt = loadedTxt.Replace("\n", Environment.NewLine);
                textBox1.Text = loadedTxt;
                //hest1
            }
            else
            {
                textBox1.Text = "";
            }

            LoadImageUserNotes(); // will override default texts, if any

            // Define event for textBox1.TextChanged to save user notes
            textBox1.TextChanged += textBox1_TextChanged; // "Description"
            textBox2.TextChanged += textBox2_TextChanged; // "OneLiner"
            panel1.Paint += new PaintEventHandler(panel1_Paint);
            panel2.Paint += new PaintEventHandler(panel2_Paint);

            // Trigger repaints to update the border
            panel1.Invalidate();
            panel2.Invalidate();



            UpdateThumbnails();

        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {
            Rectangle rect = panel1.ClientRectangle;
            rect.Inflate(0, 0); // shrink to avoid clipping
            string textBox1_org = textBox1.Text.Replace(Environment.NewLine, "\n");
//            Color borderColor = (textBox1_org != component.ComponentImages[currentImageIndex].Note) ? Color.IndianRed : ColorTranslator.FromHtml("#96919D");
            Color borderColor = (textBox1_org != filteredComponentImages[currentImageIndex].Note) ? Color.IndianRed : ColorTranslator.FromHtml("#96919D");
            ControlPaint.DrawBorder(e.Graphics, rect, borderColor, ButtonBorderStyle.Solid);
        }

        private void panel2_Paint(object sender, PaintEventArgs e)
        {
            Rectangle rect = panel2.ClientRectangle;
            rect.Inflate(0, 0); // shrink to avoid clipping
            Color borderColor = (textBox2.Text != component.OneLiner) ? Color.IndianRed : ColorTranslator.FromHtml("#96919D");
            ControlPaint.DrawBorder(e.Graphics, rect, borderColor, ButtonBorderStyle.Solid);
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            SaveComponentUserOneliner();
            panel2.Invalidate(); // trigger repaint to update the border
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            SaveImageUserNotes();
            panel1.Invalidate(); // trigger repaint to update the border

            string componentId = $"component-{component.Label}-notes";
            string value = textBox1.Text.Replace(Environment.NewLine, "\\n");
            // Assuming there is a reference to the WebView2 control:
            string script = $"window.postMessage({{type:'updateNotes',id:'{componentId}',value:`{value}`}}, '*');";
            // Call this on the main form's WebView2 instance:
            if (main?.webView2Resources != null)
            {
                main.webView2Resources.ExecuteScriptAsync(script);
            }
        }

        private void SaveComponentUserOneliner()
        {
            if (component == null) return;

            // Create a base key for the configuration file
            string key = $"UserData|{Main.hardwareSelectedName}|{Main.boardSelectedName}|{component.Label}|Oneliner";

            // Delete the configuration line, if the text in "Description" equals default text from "component.OneLiner"
            if (textBox2.Text == component.OneLiner || textBox2.Text == "")
            {
                Configuration.SaveSetting(key, ""); // delete the entire line in the configuration file
                textBox2.Text = component.OneLiner;
                return;
            }

            // Save the OneLiner
            Configuration.SaveSetting(key, textBox2.Text.Trim());
        }

        private void SaveImageUserNotes()
        {
            if (component == null) return;

            string key = $"UserData|{Main.hardwareSelectedName}|{Main.boardSelectedName}|{component.Label}|{filteredComponentImages[currentImageIndex].Name}|{filteredComponentImages[currentImageIndex].Pin}|{filteredComponentImages[currentImageIndex].Region}|Notes";

            // First, restore any previously escaped newlines to real newlines
            string rawText = textBox1.Text.Replace(Environment.NewLine, "\\n");

            // Delete the key, if it is set to empty
            if (textBox1.Text.Trim() == "")
            {
                Configuration.SaveSetting(key, "");
                textBox1.Text = filteredComponentImages[currentImageIndex].Note;
                return;
            }

            Configuration.SaveSetting(key, rawText.Trim());
        }

        private void LoadImageUserNotes()
        {
            if (component == null) return;

            // Load the "OneLiner"
            string baseKey = $"UserData|{Main.hardwareSelectedName}|{Main.boardSelectedName}|{component.Label}|Oneliner";
            string txt = Configuration.GetSetting(baseKey, "");
            if (txt != "")
            {
                textBox2.Text = txt;
            }

            // Load the "Description"
            baseKey = $"UserData|{Main.hardwareSelectedName}|{Main.boardSelectedName}|{component.Label}|{filteredComponentImages[currentImageIndex].Name}|{filteredComponentImages[currentImageIndex].Pin}|{filteredComponentImages[currentImageIndex].Region}|Notes";
            string serializedDescription = Configuration.GetSetting(baseKey, "");
            if (!string.IsNullOrEmpty(serializedDescription))
            {
                // Normalize all \r\n, \r, \n to Environment.NewLine after unescaping
                string restored = serializedDescription.Replace("\\n", Environment.NewLine);
                textBox1.Text = restored;
                // hest2
            }
        }

        private void Form_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Escape)
            {
                Close();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Close();
        }


        // Handle local-file selection
        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBox1.SelectedItem == null) return;

            string selectedName = listBox1.SelectedItem.ToString();
            if (!localFiles.ContainsKey(selectedName)) return;

            var filePath = Path.Combine(Application.StartupPath, localFiles[selectedName]);

            if (File.Exists(filePath))
            {
                System.Diagnostics.Process.Start(filePath);
            }
        }

        private void listBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBox2.SelectedItem == null) return;

            string selectedName = listBox2.SelectedItem.ToString();
            if (!links.ContainsKey(selectedName)) return;

            string url = links[selectedName];
            System.Diagnostics.Process.Start(url);
        }
        
    }
}