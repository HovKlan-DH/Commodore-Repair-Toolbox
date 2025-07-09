using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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
        private readonly List<BoardComponents> componentList;
        private Timer scrollWheelTimer = new Timer();
        private bool isScrolling = false;
        private Main main; // instance of the "Main" form
        private List<PictureBox> thumbnailPictureBoxes = new List<PictureBox>();
        private int thumbnailWindowStart = 0; // index in "imagePaths" for the first visible thumbnail
        private DateTime lastArrowKeyTime = DateTime.MinValue;
        private List<ComponentImages> filteredComponentImages = new List<ComponentImages>();
//        private string windowState = "Normal";
//        private Timer windowMoveStopTimer = new Timer();


        public FormComponent(List<BoardComponents> components, Main main)
        {
            InitializeComponent();

            this.main = main; // assign the Main instance

            this.component = components[0]; // first instance will always have the images (if any)

            // Assign the form-input "components" - not sure why this is required... but it is
            componentList = components;

            // Initialize the scrollwheel timer
            scrollWheelTimer.Interval = 1;
            scrollWheelTimer.Tick += (s, e) => { 
                isScrolling = false; 
                scrollWheelTimer.Stop(); 
            };

            // Set the "Type", which is static for the entire popup
            labelType.Text = component.Type;

            // Filter based on region
            FilterImagesByRegion();

            // Set variable with links for "local files"
            if (component.LocalFiles != null)
            {
                foreach (var localFile in component.LocalFiles)
                {
                    listBoxLocalFiles.Items.Add(localFile.Name);
                    localFiles[localFile.Name] = localFile.FileName;
                }
            }

            // Set variable with "links" (URLs)
            if (component.ComponentLinks != null)
            {
                foreach (var linkItem in component.ComponentLinks)
                {
                    listBoxLinks.Items.Add(linkItem.Name);
                    links[linkItem.Name] = linkItem.Url;
                }
            }

            // Allow pressing "Escape" to close
            KeyPreview = true;
            KeyPress += Form_KeyPress;

            // Focus on something that is not the first "textBox" in the form
            ActiveControl = labelDisplayName;
            
            // Define events
            Resize += Form_Resize;
            panelNote.Paint += new PaintEventHandler(panelNote_Paint);
            panelOneliner.Paint += new PaintEventHandler(panelOneliner_Paint);
            pictureBoxImage.MouseWheel += PictureBox1_MouseWheel;
            pictureBoxImage.MouseClick += PictureBox1_MouseClick;
            buttonRegionPal.Click += ButtonsRegion_Click;
            buttonRegionNtsc.Click += ButtonsRegion_Click;

            // Attach "form load" event, which is triggered just before form is shown
            Load += Form_Load;
        }

        private void Form_Load(object sender, EventArgs e)
        {
            /*
            windowState = Configuration.GetSetting("WindowStatePopup", "Normal");
            if (Enum.TryParse(windowState, out FormWindowState state) && state != FormWindowState.Minimized)
            {
                this.WindowState = state;
            }

            windowMoveStopTimer.Interval = 200;
            windowMoveStopTimer.Tick += MoveStopTimer_Tick;
            */

            Shown += Form_Shown;
        }

        private void Form_Shown(object sender, EventArgs e)
        {
            SetRegionButtonColors();
        }

        private void Form_Resize(object sender, EventArgs e)
        {
            LayoutThumbnails();

            /*
            // Enable/reset the time to detect when the movement has stopped
            windowMoveStopTimer.Stop();
            windowMoveStopTimer.Start();
            */
        }

        /*
        private void MoveStopTimer_Tick(object sender, EventArgs e)
        {
            windowMoveStopTimer.Stop();

            // Save the new "window state" and load and apply the splitter position for the new state
            windowState = this.WindowState.ToString();
            Configuration.SaveSetting("WindowStatePopup", windowState);
        }
        */

        private void SetRegionButtonColors()
        {
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

            int palCount = component.ComponentImages?
                .Count(img =>
                    (string.IsNullOrEmpty(img.Region) || string.Equals(img.Region, "PAL", StringComparison.OrdinalIgnoreCase))
                    && !string.IsNullOrEmpty(img.FileName)
                ) ?? 0;

            int ntscCount = component.ComponentImages?
                .Count(img =>
                    (string.IsNullOrEmpty(img.Region) || string.Equals(img.Region, "NTSC", StringComparison.OrdinalIgnoreCase))
                    && !string.IsNullOrEmpty(img.FileName)
                ) ?? 0;

            buttonRegionPal.Text = $"PAL ({palCount})";
            buttonRegionNtsc.Text = $"NTSC ({ntscCount})";
        }

        private void ButtonsRegion_Click(object sender, EventArgs e)
        {
            // Get the region from the button's "Tag" property
            var button = sender as Button;
            if (button == null || button.Tag == null) return;
            string region = button.Tag.ToString();

            Main.selectedRegion = region;
            Configuration.SaveSetting("SelectedRegion", region);
            FilterImagesByRegion();
            SetRegionButtonColors();
            main.SetRegionButtonColors();
            PopulateComponentOneliner();
        }

        private void PopulateComponentOneliner()
        {
            // We must ensure it will not trigger this event, as that would cause it to save when changed.
            // We only want it to save when user manually changes it.
            textBoxOneliner.TextChanged -= textBoxOneliner_TextChanged;

            // Get the component "technical name"
            string technicalName = GetDefaultTechnicalName();

            // Get the default "oneliner" text from Excel
            foreach (var comp in componentList)
            {
                if (comp.NameTechnical == technicalName || comp.NameTechnical == "")
                {
                    // Set default text from Excel
                    textBoxOneliner.Text = Main.ConvertStringToLabel(comp.OneLiner);
                    break;
                }
            }

            // Load the "oneliner" from configuration file
            string baseKey = $"UserData|{Main.hardwareSelectedName}|{Main.boardSelectedName}|{component.Label}|{technicalName}|Oneliner";
            string txt = Configuration.GetSetting(baseKey, "");
            if (txt != "")
            {
                textBoxOneliner.Text = txt;
            }

            textBoxOneliner.TextChanged += textBoxOneliner_TextChanged;
            panelOneliner.Invalidate(); // trigger repaint to update the border
        }

        private void PopulateImageNote()
        {
            // We must ensure it will not trigger this event, as that would cause it to save when changed.
            // We only want it to save when user manually changes it.
            textBoxNote.TextChanged -= textBoxNote_TextChanged;

            string pin = filteredComponentImages[currentImageIndex].Pin ?? "";
            string pinName = filteredComponentImages[currentImageIndex].Name ?? "";
            string pinRegion = filteredComponentImages[currentImageIndex].Region ?? "";
            string pinNote = filteredComponentImages[currentImageIndex].Note ?? "";

            pinName = pinName == "" ? "Pinout" : pinName; // default to "Pinout" if empty

            textBoxNote.Text = Main.ConvertStringToLabel(pinNote).Replace("\n", Environment.NewLine);

            // Load the "note" from configuration file
            string baseKey = $"UserData|{Main.hardwareSelectedName}|{Main.boardSelectedName}|{component.Label}|{pin}|{pinName}|{pinRegion}|Note";
            string serializedDescription = Configuration.GetSetting(baseKey, "");
            if (!string.IsNullOrEmpty(serializedDescription))
            {
                // Normalize all \r\n, \r, \n to Environment.NewLine after unescaping
                string restored = serializedDescription.Replace("\\n", Environment.NewLine);
                textBoxNote.Text = restored;

            }

            textBoxNote.TextChanged += textBoxNote_TextChanged;
            panelNote.Invalidate(); // trigger repaint to update the border
        }

        private void SetFormTitles()
        {
            foreach (var comp in componentList)
            {
                if (comp.Region == Main.selectedRegion || comp.Region == "")
                {
                    string formTitle = comp.NameDisplay ?? "Component Information";
                    this.Text = formTitle;
                    labelDisplayName.Text = formTitle;
                    break;
                }
            }
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

                // Remove any "filteredComponentImages" whose file does not exist, but keep if "FileName" is empty
                filteredComponentImages = filteredComponentImages
                    .Where((img, idx) => string.IsNullOrWhiteSpace(img.FileName) || File.Exists(Path.Combine(Application.StartupPath, img.FileName)))
                    .ToList();
            }
            else
            {
                imagePaths = new List<string>();
                filteredComponentImages = new List<ComponentImages> { new ComponentImages() };
            }

            if (currentImageIndex >= imagePaths.Count)
            {
                currentImageIndex = 0;
            }

            LayoutThumbnails();
            UpdateImage();
        }

        private void RemoveThumbnails ()
        {
            // Remove old thumbnails and their labels
            foreach (var pb in thumbnailPictureBoxes)
            {
                // Remove and dispose all child controls (labels)
                foreach (Control ctrl in pb.Controls.OfType<Label>().ToList())
                {
                    pb.Controls.Remove(ctrl);
                    ctrl.Dispose();
                }
                panelImageAndThumbnails.Controls.Remove(pb);
                pb.Dispose();
            }
            thumbnailPictureBoxes.Clear();
        }

        private void LayoutThumbnails()
        {
            labelImageX.Location = new Point(10, pictureBoxImage.Height - labelImageX.Height - 5);

            // Define width of "panelImageAndThumbnails"
            int panelThumbnailHeight = 80;
            int panelThumbnailWidth = 120;
            int panelWidth = panelImageAndThumbnails.Width;

            if (imagePaths.Count > 1)
            {                            
                RemoveThumbnails();

                // Find how many panels we can show with width of "panelWidth"
                int panels = panelWidth / panelThumbnailWidth;
                if (panels < 1) panels = 1; // Always show at least one

                // Recalculate width so thumbnails fill the panel exactly
                int dynamicThumbnailWidth = panelWidth / panels;
                int dynamicThumbnailHeight = panelThumbnailHeight; // Keep height as before

                int y = panelImageAndThumbnails.Height - dynamicThumbnailHeight;

                // Create the amount of thumbnails we can fit in the panel
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

                    string pinText = i < filteredComponentImages?.Count ? "Pin "+filteredComponentImages[i].Pin : "";
                    Label labelPin = new Label
                    {
                        AutoSize = true,
                        Height = 15,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Font = new Font("Calibri", 8, FontStyle.Regular),
                        BackColor = Color.Khaki,
                        ForeColor = Color.Black,
                        Text = pinText,
                        Location = new Point(5, 5)
                    };
                    pictureBox.Controls.Add(labelPin);

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

                    panelImageAndThumbnails.Controls.Add(pictureBox);
                    pictureBox.Controls.Add(labelPin);
                }

                // Invalidate all thumbnails (redraw)
                foreach (var pb in thumbnailPictureBoxes)
                {
                    pb.Invalidate();
                }
            }

            // Resize the main picture box to fill the remaining space
            if (imagePaths.Count > 1)
            {
                pictureBoxImage.Size = new Size(panelImageAndThumbnails.Width, panelImageAndThumbnails.Height - panelThumbnailHeight - 5);
            } else
            {
                pictureBoxImage.Size = new Size(panelImageAndThumbnails.Width, panelImageAndThumbnails.Height);
            }
        }

        private void UpdateThumbnails()
        {
            int panels = thumbnailPictureBoxes.Count;
            int imagesCount = imagePaths.Count;

            // Adjust window so "currentImageIndex" is always visible
            if (currentImageIndex < thumbnailWindowStart)
            {
                thumbnailWindowStart = currentImageIndex;
            }
            else if (currentImageIndex >= thumbnailWindowStart + panels)
            {
                thumbnailWindowStart = currentImageIndex - panels + 1;
            }

            // Clamp window start
            if (thumbnailWindowStart < 0)
            {
                thumbnailWindowStart = 0;
            }                
            if (thumbnailWindowStart > Math.Max(0, imagesCount - panels))
            {
                thumbnailWindowStart = Math.Max(0, imagesCount - panels);
            }

            // Update images, tags, and labels for each thumbnail
            for (int i = 0; i < panels; i++)
            {
                int imgIdx = thumbnailWindowStart + i;
                var pb = thumbnailPictureBoxes[i];
                pb.Tag = imgIdx;
                if (imgIdx < imagesCount)
                {
                    pb.Image = Image.FromFile(imagePaths[imgIdx]);
                    pb.Visible = true;

                    // Update labels inside the "PictureBox"
                    var labels = pb.Controls.OfType<Label>().ToList();
                    if (
                        labels.Count >= 1 && component.ComponentImages != null && 
                        imgIdx < component.ComponentImages.Count
                        )
                    {
                        var pinValue = filteredComponentImages[imgIdx].Pin;
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

                    // Clear label text if out of range
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
            ActiveControl = pictureBoxImage;

            // Update the image
            UpdateImage();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Only go in here, if the input text area is NOT in focus
            if (textBoxNote.Focused || textBoxOneliner.Focused)
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
            PictureBox1_MouseWheel(pictureBoxImage, e);
        }

        private void PictureBox1_MouseWheel(object sender, MouseEventArgs e)
        {
            // Return if already scrolling
            if (isScrolling)
            {
                return;
            }

            isScrolling = true;
            scrollWheelTimer.Start();

            if (imagePaths.Count == 0) return;

            // Determine the scroll direction
            if (e.Delta > 0)
            {
                // Scroll up: move to the PREVIOUS image
                currentImageIndex = (currentImageIndex - 1 + imagePaths.Count) % imagePaths.Count;
            }
            else if (e.Delta < 0)
            {
                // Scroll down: move to the NEXT image
                currentImageIndex = (currentImageIndex + 1) % imagePaths.Count;
            }

            UpdateImage();
        }

        private void UpdateImage()
        {
            // Default hide all labels
            labelPin.Visible = false;
            labelRegion.Visible = false;
            labelName.Visible = false;
            labelReading.Visible = false;
            labelImageX.Visible = false;

            // Show more, if there are thumbnails
            if (imagePaths.Count > 0) 
            { 
                // Update the image
                pictureBoxImage.Image = Image.FromFile(imagePaths[currentImageIndex]);

                // Update labels
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
                labelReading.Text = reading;
                labelReading.Visible = reading != "" ? true : false;

                // Count the number of images, and show a counter in "label13"
                if (imagePaths.Count > 1)
                {
                    labelImageX.Text = $"Image {currentImageIndex + 1} of {imagePaths.Count}";
                
                    // Place "labelImageX" at bottom left corner
                    labelImageX.Location = new Point(10, pictureBoxImage.Height - labelImageX.Height - 5);
                    labelImageX.Visible = true;
                } else
                {
                    labelImageX.Visible = false;
                }                

                UpdateThumbnails();
            }

            SetFormTitles();
            PopulateComponentOneliner();
            PopulateImageNote();
        }

        private string GetDefaultTechnicalName()
        {
            // Get default component "technical name"
            foreach (var comp in componentList)
            {
                if (comp.Region == Main.selectedRegion || comp.Region == "")
                {
                    string technicalName = comp.NameTechnical ?? "";
                    return technicalName;
                }
            }
            return "";
        }

        private void panelNote_Paint(object sender, PaintEventArgs e)
        {
            Rectangle rect = panelNote.ClientRectangle;
            rect.Inflate(0, 0); // shrink to avoid clipping
            string textBox1_org = textBoxNote.Text.Replace(Environment.NewLine, "\n");
            string excelData = filteredComponentImages[currentImageIndex].Note ?? "";
            Color borderColor = textBox1_org != excelData ? Color.IndianRed : ColorTranslator.FromHtml("#96919D");
            ControlPaint.DrawBorder(e.Graphics, rect, borderColor, ButtonBorderStyle.Solid);
        }

        private void panelOneliner_Paint(object sender, PaintEventArgs e)
        {
            Rectangle rect = panelOneliner.ClientRectangle;
            rect.Inflate(0, 0); // shrink to avoid clipping

            // Get the component "technical name"
            string technicalName = GetDefaultTechnicalName();

            string excelData = component.OneLiner ?? "";

            // Get the default "oneliner" text from Excel
            foreach (var comp in componentList)
            {
                if (comp.NameTechnical == technicalName || comp.NameTechnical == "")
                {
                    excelData = Main.ConvertStringToLabel(comp.OneLiner);
                    break;
                }
            }

            Color borderColor = textBoxOneliner.Text != excelData ? Color.IndianRed : ColorTranslator.FromHtml("#96919D");
            ControlPaint.DrawBorder(e.Graphics, rect, borderColor, ButtonBorderStyle.Solid);
        }

        private void textBoxOneliner_TextChanged(object sender, EventArgs e)
        {
            textBoxOneliner.TextChanged -= textBoxOneliner_TextChanged;
            SaveComponentUserOneliner();
            textBoxOneliner.TextChanged += textBoxOneliner_TextChanged;
            panelOneliner.Invalidate(); // trigger repaint to update the border
        }

        private void textBoxNote_TextChanged(object sender, EventArgs e)
        {
            textBoxNote.TextChanged -= textBoxNote_TextChanged;
            SaveImageUserNotes();
            textBoxNote.TextChanged += textBoxNote_TextChanged;
            panelNote.Invalidate(); // trigger repaint to update the border

            string componentId = $"component-{component.Label}-notes";
            string value = textBoxNote.Text.Replace(Environment.NewLine, "\\n");
            
            // Assuming there is a reference to the "WebView2" control:
            string script = $"window.postMessage({{type:'updateNotes',id:'{componentId}',value:`{value}`}}, '*');";
            
            // Call this on the main form "WebView2" instance:
            if (main?.webView2Resources != null)
            {
                main.webView2Resources.ExecuteScriptAsync(script);
            }
        }

        private void SaveComponentUserOneliner()
        {
            if (component == null) return;

            // Get the component "technical name"
            string technicalName = GetDefaultTechnicalName();

            string key = $"UserData|{Main.hardwareSelectedName}|{Main.boardSelectedName}|{component.Label}|{technicalName}|Oneliner";

            // Delete the configuration line, if the text in "Description" equals default text from "component.OneLiner"
            if (textBoxOneliner.Text == component.OneLiner || textBoxOneliner.Text == "")
            {
                Configuration.SaveSetting(key, ""); // delete the entire line in the configuration file

                // Get the default text from Excel
                foreach (var comp in componentList)
                {
                    // Set default text from Excel
                    if (comp.NameTechnical == technicalName || comp.NameTechnical == "")
                    {
                        textBoxOneliner.Text = Main.ConvertStringToLabel(comp.OneLiner);
                        
                        // Move the cursor to the end of the textbox
                        textBoxOneliner.SelectionStart = textBoxOneliner.Text.Length;
                        textBoxOneliner.SelectionLength = 0;
                        return;
                    }
                }                
            }

            // Save the OneLiner
            Configuration.SaveSetting(key, textBoxOneliner.Text.Trim());
        }

        private void SaveImageUserNotes()
        {
            if (component == null) return;
            if (textBoxNote.Text == filteredComponentImages[currentImageIndex].Note) return;

            string pin = filteredComponentImages[currentImageIndex].Pin ?? "";
            string pinName = filteredComponentImages[currentImageIndex].Name ?? "Pinout";
            string pinRegion = filteredComponentImages[currentImageIndex].Region ?? "";
            string pinNote = filteredComponentImages[currentImageIndex].Note ?? "";

            pinName = pinName == "" ? "Pinout" : pinName; // default to "Pinout" if empty

            string key = $"UserData|{Main.hardwareSelectedName}|{Main.boardSelectedName}|{component.Label}|{pin}|{pinName}|{pinRegion}|Note";

            string rawText = textBoxNote.Text.Replace(Environment.NewLine, "\\n");

            // Delete the key, if it is set to empty
            if (textBoxNote.Text.Trim() == "")
            {
                Configuration.SaveSetting(key, "");
                pinNote = pinNote.Replace("\n", Environment.NewLine);
                textBoxNote.Text = pinNote;
                
                // Move the cursor to the end of the textbox
                textBoxNote.SelectionStart = textBoxNote.Text.Length;
                textBoxNote.SelectionLength = 0;
                return;
            }

            Configuration.SaveSetting(key, rawText.Trim());
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

        private void listBoxLocalFiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxLocalFiles.SelectedItem == null) return;

            string selectedName = listBoxLocalFiles.SelectedItem.ToString();
            if (!localFiles.ContainsKey(selectedName)) return;

            var filePath = Path.Combine(Application.StartupPath, localFiles[selectedName]);

            if (File.Exists(filePath))
            {
                System.Diagnostics.Process.Start(filePath);
            }
        }

        private void listBoxLinks_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxLinks.SelectedItem == null) return;

            string selectedName = listBoxLinks.SelectedItem.ToString();
            if (!links.ContainsKey(selectedName)) return;

            string url = links[selectedName];
            System.Diagnostics.Process.Start(url);
        }
        
    }
}