using System;
using System.Collections.Generic;
//using System.ComponentModel;
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
        private Timer scrollTimer = new Timer();
        private bool isScrolling = false;

        public string PictureBoxName { get; }

        public FormComponent(BoardComponents component)
        {
            InitializeComponent();

            // Assign the passed-in component to the class-level field
            this.component = component;

            // Initialize the scroll timer
            scrollTimer.Interval = 10; // Adjust the interval as needed (100ms is a good starting point)
            scrollTimer.Tick += (s, e) => { isScrolling = false; scrollTimer.Stop(); };

            // Bind the MouseClick event to reset the image index
            pictureBox1.MouseClick += PictureBox1_MouseClick;

            PictureBoxName = component.Label;

            // Basic labels
            label1.Text = Main.ConvertStringToLabel(component.Label);
            label2.Text = Main.ConvertStringToLabel(component.NameTechnical);
            label3.Text = Main.ConvertStringToLabel(component.NameFriendly);
            label4.Text = component.Type;
            label5.Text = Main.ConvertStringToLabel(component.OneLiner);

            // Description box
            textBox1.Text = Main.ConvertStringToLabel(component.Description);
            textBox1.ScrollBars = ScrollBars.Vertical;

            // Define an array with all pinout images
            imagePaths = new List<string>(); // ensure it exists by default
            if (component.ComponentImages != null && component.ComponentImages.Count > 0)
            {
                // Ensure the imagePaths list is populated in the same order as component.ComponentImages
                imagePaths = component.ComponentImages
                    .Select(image => Path.Combine(Application.StartupPath, image.FileName))
                    .Where(File.Exists)
                    .ToList();
            }

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
        }

        private void PictureBox1_MouseClick(object sender, MouseEventArgs e)
        {
            // Reset the image index to the first image
            currentImageIndex = 0;

            // Update the image
            UpdateImage();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
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
            // Default hide all labels
            label9.Visible = false;
            label10.Visible = false;
            label11.Visible = false;
            label12.Visible = false;
            label13.Visible = false;

            if (imagePaths.Count == 0) return;

            // Update the image
            pictureBox1.Image = Image.FromFile(imagePaths[currentImageIndex]);

            // Update labels
            string region = component.ComponentImages[currentImageIndex].Region;
            string pin = component.ComponentImages[currentImageIndex].Pin;
            string name = component.ComponentImages[currentImageIndex].Name;
            label9.Text = "Pin " + pin;
            label11.Text = name;
            label10.Text = region;
            label9.Visible = pin != "" ? true : false;
            label11.Visible = name != "" ? true : false;
            label10.Visible = region != "" ? true : false;
            if (!label9.Visible)
            {
                label11.Location = new Point(label9.Location.X, label11.Location.Y);
            }
            else
            {
                label11.Location = new Point(label9.Location.X + label9.Width + 3, label11.Location.Y);
            }
            label10.Location = new Point(label11.Location.X + label11.Width + 3, label10.Location.Y);

            // Find the "Description" from the "component.Oscilloscope" list, based on the component name and pin
            var oscilloscopeItem = component.Oscilloscope?.FirstOrDefault(item => item.Name == name && item.Pin == pin);
            string reading = oscilloscopeItem?.Reading ?? string.Empty;
            label12.Text = reading;
            label12.Visible = reading != "" ? true : false;

            // Count the number of images, and show a counter in "label13"
            if (imagePaths.Count > 1)
            {
                label13.Text = $"Image {currentImageIndex + 1} of {imagePaths.Count}";
                label13.Location = new Point(pictureBox1.Width - label13.Width - 0, label13.Location.Y);
                label13.Visible = true;
            } else
            {
                label13.Visible = false;
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