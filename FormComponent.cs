using Commodore_Repair_Toolbox;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Commodore_Retro_Toolbox
{
    public partial class FormComponent : Form
    {
        public string PictureBoxName { get; private set; }

        public FormComponent(ComponentBoard component, string hardwareSelectedFolder, string boardSelectedFolder)
        {
            InitializeComponent();

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


            this.KeyPreview = true;
            this.KeyPress += new KeyPressEventHandler(Form_KeyPress);

            AttachMouseDownEventHandlers(this);

        }


        private void AttachMouseDownEventHandlers(Control parentControl)
        {
            parentControl.MouseDown += GenericMouseDownHandler;

            foreach (Control control in parentControl.Controls)
            {
                if (control is TextBox && control.Name == "textBox1") { 
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

    }
}
