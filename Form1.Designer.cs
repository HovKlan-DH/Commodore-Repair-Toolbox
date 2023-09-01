namespace Commodore_Retro_Toolbox
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            tabControl1 = new TabControl();
            tabPage1 = new TabPage();
            panel1 = new Panel();
            panel2 = new Panel();
            label5 = new Label();
            tabPage2 = new TabPage();
            tabPage3 = new TabPage();
            tabPage4 = new TabPage();
            tabPage5 = new TabPage();
            tabPage6 = new TabPage();
            pictureBox1 = new PictureBox();
            comboBox1 = new ComboBox();
            label1 = new Label();
            label2 = new Label();
            comboBox2 = new ComboBox();
            listBox1 = new ListBox();
            label3 = new Label();
            button1 = new Button();
            label4 = new Label();
            groupBox1 = new GroupBox();
            radioButton1 = new RadioButton();
            groupBox2 = new GroupBox();
            radioButton2 = new RadioButton();
            tabControl1.SuspendLayout();
            tabPage1.SuspendLayout();
            panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            SuspendLayout();
            // 
            // tabControl1
            // 
            tabControl1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Controls.Add(tabPage2);
            tabControl1.Controls.Add(tabPage3);
            tabControl1.Controls.Add(tabPage4);
            tabControl1.Controls.Add(tabPage5);
            tabControl1.Controls.Add(tabPage6);
            tabControl1.Location = new Point(218, 12);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(531, 636);
            tabControl1.TabIndex = 0;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(panel1);
            tabPage1.Controls.Add(label5);
            tabPage1.Location = new Point(4, 29);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(3);
            tabPage1.Size = new Size(523, 603);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "Main";
            tabPage1.UseVisualStyleBackColor = true;
            tabPage1.SizeChanged += tabPage1_SizeChanged;
            // 
            // panel1
            // 
            panel1.Controls.Add(panel2);
            panel1.Location = new Point(0, 36);
            panel1.Name = "panel1";
            panel1.Size = new Size(523, 567);
            panel1.TabIndex = 10;
            panel1.Paint += panel1_Paint_1;
            // 
            // panel2
            // 
            panel2.Location = new Point(139, 292);
            panel2.Name = "panel2";
            panel2.Size = new Size(33, 63);
            panel2.TabIndex = 10;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            label5.Location = new Point(6, 8);
            label5.Name = "label5";
            label5.Size = new Size(125, 20);
            label5.TabIndex = 10;
            label5.Text = "Schematic 1 of 2";
            // 
            // tabPage2
            // 
            tabPage2.Location = new Point(4, 29);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new Padding(3);
            tabPage2.Size = new Size(523, 603);
            tabPage2.TabIndex = 1;
            tabPage2.Text = "Trivia";
            tabPage2.UseVisualStyleBackColor = true;
            // 
            // tabPage3
            // 
            tabPage3.Location = new Point(4, 29);
            tabPage3.Name = "tabPage3";
            tabPage3.Padding = new Padding(3);
            tabPage3.Size = new Size(523, 603);
            tabPage3.TabIndex = 2;
            tabPage3.Text = "Troubleshooting";
            tabPage3.UseVisualStyleBackColor = true;
            // 
            // tabPage4
            // 
            tabPage4.Location = new Point(4, 29);
            tabPage4.Name = "tabPage4";
            tabPage4.Size = new Size(523, 603);
            tabPage4.TabIndex = 3;
            tabPage4.Text = "Links";
            tabPage4.UseVisualStyleBackColor = true;
            // 
            // tabPage5
            // 
            tabPage5.Location = new Point(4, 29);
            tabPage5.Name = "tabPage5";
            tabPage5.Size = new Size(523, 603);
            tabPage5.TabIndex = 4;
            tabPage5.Text = "Settings";
            tabPage5.UseVisualStyleBackColor = true;
            // 
            // tabPage6
            // 
            tabPage6.Location = new Point(4, 29);
            tabPage6.Name = "tabPage6";
            tabPage6.Size = new Size(523, 603);
            tabPage6.TabIndex = 5;
            tabPage6.Text = "About";
            tabPage6.UseVisualStyleBackColor = true;
            // 
            // pictureBox1
            // 
            pictureBox1.Image = (Image)resources.GetObject("pictureBox1.Image");
            pictureBox1.ImageLocation = "";
            pictureBox1.InitialImage = null;
            pictureBox1.Location = new Point(6, 59);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(186, 142);
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.TabIndex = 8;
            pictureBox1.TabStop = false;
            // 
            // comboBox1
            // 
            comboBox1.FormattingEnabled = true;
            comboBox1.Items.AddRange(new object[] { "Commodore 64 Breadbin", "Commodore 64C", "Commodore 128", "Commodore 128D" });
            comboBox1.Location = new Point(12, 41);
            comboBox1.Name = "comboBox1";
            comboBox1.Size = new Size(200, 28);
            comboBox1.TabIndex = 1;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(12, 18);
            label1.Name = "label1";
            label1.Size = new Size(74, 20);
            label1.TabIndex = 2;
            label1.Text = "Hardware";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(12, 87);
            label2.Name = "label2";
            label2.Size = new Size(49, 20);
            label2.TabIndex = 4;
            label2.Text = "Board";
            // 
            // comboBox2
            // 
            comboBox2.FormattingEnabled = true;
            comboBox2.Items.AddRange(new object[] { "250407", "250425" });
            comboBox2.Location = new Point(12, 110);
            comboBox2.Name = "comboBox2";
            comboBox2.Size = new Size(200, 28);
            comboBox2.TabIndex = 3;
            // 
            // listBox1
            // 
            listBox1.FormattingEnabled = true;
            listBox1.ItemHeight = 20;
            listBox1.Items.AddRange(new object[] { "U1   6526   CIA 1", "U2   6526   CIA 2", "U3   2364   Basic ROM", "U4   2364   Kernal ROM", "R100   100Ohm   Resistor", "R101   100Ohm   Resistor", "R102   100Ohm   Resistor", "R103   100Ohm   Resistor", "R104   100Ohm   Resistor", "R105   100Ohm   Resistor", "R106   100Ohm   Resistor", "R107   100Ohm   Resistor", "R108   100Ohm   Resistor", "R110   100Ohm   Resistor", "R112   100Ohm   Resistor" });
            listBox1.Location = new Point(12, 187);
            listBox1.Name = "listBox1";
            listBox1.SelectionMode = SelectionMode.MultiSimple;
            listBox1.Size = new Size(200, 224);
            listBox1.TabIndex = 5;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(12, 164);
            label3.Name = "label3";
            label3.Size = new Size(87, 20);
            label3.TabIndex = 6;
            label3.Text = "Component";
            // 
            // button1
            // 
            button1.Location = new Point(12, 417);
            button1.Name = "button1";
            button1.Size = new Size(200, 29);
            button1.TabIndex = 7;
            button1.Text = "Clear selections";
            button1.UseVisualStyleBackColor = true;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(6, 36);
            label4.Name = "label4";
            label4.Size = new Size(119, 20);
            label4.TabIndex = 9;
            label4.Text = "Schematic 1 of 2";
            // 
            // groupBox1
            // 
            groupBox1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            groupBox1.Controls.Add(pictureBox1);
            groupBox1.Controls.Add(label4);
            groupBox1.Location = new Point(755, 18);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(214, 626);
            groupBox1.TabIndex = 10;
            groupBox1.TabStop = false;
            groupBox1.Text = "List";
            // 
            // radioButton1
            // 
            radioButton1.AutoSize = true;
            radioButton1.Checked = true;
            radioButton1.Location = new Point(21, 38);
            radioButton1.Name = "radioButton1";
            radioButton1.Size = new Size(92, 24);
            radioButton1.TabIndex = 11;
            radioButton1.TabStop = true;
            radioButton1.Text = "Highlight";
            radioButton1.UseVisualStyleBackColor = true;
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(radioButton2);
            groupBox2.Controls.Add(radioButton1);
            groupBox2.Location = new Point(12, 468);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(200, 125);
            groupBox2.TabIndex = 12;
            groupBox2.TabStop = false;
            groupBox2.Text = "Highlight";
            // 
            // radioButton2
            // 
            radioButton2.AutoSize = true;
            radioButton2.Location = new Point(21, 68);
            radioButton2.Name = "radioButton2";
            radioButton2.Size = new Size(139, 24);
            radioButton2.TabIndex = 12;
            radioButton2.Text = "Do not highlight";
            radioButton2.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(981, 660);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            Controls.Add(button1);
            Controls.Add(label3);
            Controls.Add(listBox1);
            Controls.Add(label2);
            Controls.Add(comboBox2);
            Controls.Add(label1);
            Controls.Add(comboBox1);
            Controls.Add(tabControl1);
            MinimumSize = new Size(800, 500);
            Name = "Form1";
            Text = "Commodore Retro Toolbox";
            tabControl1.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            tabPage1.PerformLayout();
            panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TabControl tabControl1;
        private TabPage tabPage1;
        private TabPage tabPage2;
        private ComboBox comboBox1;
        private Label label1;
        private Label label2;
        private ComboBox comboBox2;
        private ListBox listBox1;
        private Label label3;
        private Button button1;
        private PictureBox pictureBox1;
        private Label label4;
        private TabPage tabPage3;
        private TabPage tabPage4;
        private TabPage tabPage5;
        private TabPage tabPage6;
        private GroupBox groupBox1;
        private Label label5;
        private RadioButton radioButton1;
        private GroupBox groupBox2;
        private RadioButton radioButton2;
        private Panel panel1;
        private Panel panel2;
    }
}
