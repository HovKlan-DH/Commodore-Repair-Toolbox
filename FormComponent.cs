using Commodore_Repair_Toolbox;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Commodore_Repair_Toolbox
{
    public partial class FormComponent : Form
    {
        private readonly Dictionary<string, string> localFiles = new Dictionary<string, string>();
        private readonly Dictionary<string, string> links = new Dictionary<string, string>();

        private readonly string hardwareSelectedFolder;
        private readonly string boardSelectedFolder;

        public string PictureBoxName { get; }

        public FormComponent(ComponentBoard component, string hwSelectedFolder, string bdSelectedFolder)
        {
            InitializeComponent();

            hardwareSelectedFolder = hwSelectedFolder;
            boardSelectedFolder = bdSelectedFolder;

            PictureBoxName = component.Label;

            // Basic labels
            label1.Text = component.Label;
            label2.Text = component.NameTechnical;
            label3.Text = component.NameFriendly;
            label4.Text = component.Type;
            label5.Text = component.OneLiner;

            // Description box
            textBox1.Text = component.Description;
            textBox1.ScrollBars = ScrollBars.Vertical;

            // Pinout image
            if (component.ComponentImages != null && component.ComponentImages.Count > 0)
            {
                var imagePath = Path.Combine(
                    //Application.StartupPath, "Data",
                    hardwareSelectedFolder, boardSelectedFolder,
                    component.ComponentImages[0].FileName // Assuming the first image is the pinout image
                );
                if (File.Exists(imagePath))
                {
                    pictureBox1.Image = Image.FromFile(imagePath);
                }
            }

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

        private void Form_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Escape)
            {
                Close();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // "Close" button
            Close();
        }

        // Handle local-file selection
        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBox1.SelectedItem == null) return;

            string selectedName = listBox1.SelectedItem.ToString();
            if (!localFiles.ContainsKey(selectedName)) return;

            var filePath = Path.Combine(
                Application.StartupPath, "Data",
                hardwareSelectedFolder, boardSelectedFolder,
                localFiles[selectedName]
            );

            if (File.Exists(filePath))
            {
                System.Diagnostics.Process.Start(filePath);
            }
        }

        // Handle link selection
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