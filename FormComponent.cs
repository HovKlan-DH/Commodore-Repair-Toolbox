using System;
using System.Windows.Forms;

namespace Commodore_Retro_Toolbox
{
    public partial class FormComponent : Form
    {
        public string PictureBoxName { get; private set; }

        public FormComponent(string pictureBoxName)
        {
            InitializeComponent();
            this.PictureBoxName = pictureBoxName;
            label1.Text = pictureBoxName;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            MessageBox.Show(PictureBoxName);
        }
    }
}
