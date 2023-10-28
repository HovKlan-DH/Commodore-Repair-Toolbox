using Commodore_Repair_Toolbox;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Commodore_Retro_Toolbox
{
    public partial class FormComponent : Form
    {
        public string PictureBoxName { get; private set; }
        Dictionary<string, string> localFiles = new Dictionary<string, string>();
        Dictionary<string, string> links = new Dictionary<string, string>();
        string hardwareSelectedFolder;
        string boardSelectedFolder;

        public FormComponent(ComponentBoard component, string hwSelectedFolder, string bdSelectedFolder)
        {
            InitializeComponent();

            hardwareSelectedFolder = hwSelectedFolder;
            boardSelectedFolder = bdSelectedFolder;

            this.PictureBoxName = component.Label;
            label1.Text = component.Label;
            label2.Text = component.NameTechnical;
            label3.Text = component.NameFriendly;
            label4.Text = component.Type;
            label5.Text = component.OneLiner;
            textBox1.Text = component.Description;
            textBox1.ScrollBars = ScrollBars.Vertical;

            if(System.IO.File.Exists(Application.StartupPath + "\\Data\\" + hardwareSelectedFolder + "\\" + boardSelectedFolder + "\\" + component.ImagePinout))
            {
                Image image = Image.FromFile(Application.StartupPath + "\\Data\\" + hardwareSelectedFolder + "\\" + boardSelectedFolder + "\\" + component.ImagePinout);
                pictureBox1.Image = image;
            }

            if(component.LocalFiles != null)
            {
                foreach (LocalFiles localFile in component.LocalFiles)
                {
                    listBox1.Items.Add(localFile.Name);
                    localFiles.Add(localFile.Name, localFile.FileName);
                }
            }

            if(component.ComponentLinks != null)
            {
                foreach (ComponentLinks link in component.ComponentLinks)
                {
                    listBox2.Items.Add(link.Name);
                    links.Add(link.Name, link.Url);
                }
            }

            this.KeyPreview = true;
            this.KeyPress += new KeyPressEventHandler(Form_KeyPress);

            AttachMouseDownEventHandlers(this);

        }


        private void AttachMouseDownEventHandlers(Control parentControl)
        {
            parentControl.MouseDown += GenericMouseDownHandler;

            foreach (Control control in parentControl.Controls)
            {
                if (control is TextBox && control.Name == "textBox1"
                    || control is ListBox && control.Name == "listBox1"
                    || control is ListBox && control.Name == "listBox2"
                    ) { 
                    continue;
                }
                AttachMouseDownEventHandlers(control);
            }
        }

        private void GenericMouseDownHandler(object sender, MouseEventArgs e)
        {
            // Your code here
            this.Close();
        }


        void Form_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Escape)
            {
                this.Close();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(listBox1.SelectedItem != null)
            {
                string selectedName = listBox1.SelectedItem.ToString();
                if (localFiles.ContainsKey(selectedName))
                {
                    if (System.IO.File.Exists(Application.StartupPath + "\\Data\\" + hardwareSelectedFolder + "\\" + boardSelectedFolder + "\\" + localFiles[selectedName]))
                    {
                        System.Diagnostics.Process.Start(Application.StartupPath + "\\Data\\" + hardwareSelectedFolder + "\\" + boardSelectedFolder + "\\" + localFiles[selectedName]);
                    }
                }
            }
        }

        private void listBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(listBox2.SelectedItem != null)
            {
                string selectedName = listBox2.SelectedItem.ToString();
                if (links.ContainsKey(selectedName))
                {
                    System.Diagnostics.Process.Start(links[selectedName]);
                }
            }
        }
    }
}
