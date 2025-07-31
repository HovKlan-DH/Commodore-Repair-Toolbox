namespace Commodore_Repair_Toolbox
{
    partial class FormComponent
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormComponent));
            this.pictureBoxImage = new System.Windows.Forms.PictureBox();
            this.button1 = new System.Windows.Forms.Button();
            this.labelDisplayName = new System.Windows.Forms.Label();
            this.labelType = new System.Windows.Forms.Label();
            this.listBoxLocalFiles = new System.Windows.Forms.ListBox();
            this.listBoxLinks = new System.Windows.Forms.ListBox();
            this.label6 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.textBoxNote = new System.Windows.Forms.TextBox();
            this.labelPin = new System.Windows.Forms.Label();
            this.labelRegion = new System.Windows.Forms.Label();
            this.labelName = new System.Windows.Forms.Label();
            this.labelReading = new System.Windows.Forms.Label();
            this.labelImageX = new System.Windows.Forms.Label();
            this.textBoxOneliner = new System.Windows.Forms.TextBox();
            this.panelNote = new System.Windows.Forms.Panel();
            this.panelOneliner = new System.Windows.Forms.Panel();
            this.panelImageAndThumbnails = new System.Windows.Forms.Panel();
            this.buttonRegionPal = new System.Windows.Forms.Button();
            this.buttonRegionNtsc = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.buttonToggle = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxImage)).BeginInit();
            this.panelNote.SuspendLayout();
            this.panelOneliner.SuspendLayout();
            this.panelImageAndThumbnails.SuspendLayout();
            this.SuspendLayout();
            // 
            // pictureBoxImage
            // 
            this.pictureBoxImage.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pictureBoxImage.BackColor = System.Drawing.Color.White;
            this.pictureBoxImage.Location = new System.Drawing.Point(3, 3);
            this.pictureBoxImage.Name = "pictureBoxImage";
            this.pictureBoxImage.Size = new System.Drawing.Size(618, 644);
            this.pictureBoxImage.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBoxImage.TabIndex = 0;
            this.pictureBoxImage.TabStop = false;
            // 
            // button1
            // 
            this.button1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.button1.Font = new System.Drawing.Font("Calibri", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.button1.Location = new System.Drawing.Point(1030, 785);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 30);
            this.button1.TabIndex = 6;
            this.button1.Text = "Close";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // labelDisplayName
            // 
            this.labelDisplayName.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.labelDisplayName.AutoSize = true;
            this.labelDisplayName.Font = new System.Drawing.Font("Calibri", 13.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelDisplayName.Location = new System.Drawing.Point(632, 9);
            this.labelDisplayName.Name = "labelDisplayName";
            this.labelDisplayName.Size = new System.Drawing.Size(63, 28);
            this.labelDisplayName.TabIndex = 1;
            this.labelDisplayName.Text = "Label";
            // 
            // labelType
            // 
            this.labelType.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.labelType.AutoSize = true;
            this.labelType.Font = new System.Drawing.Font("Calibri", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelType.Location = new System.Drawing.Point(633, 37);
            this.labelType.Name = "labelType";
            this.labelType.Size = new System.Drawing.Size(42, 21);
            this.labelType.TabIndex = 100;
            this.labelType.Text = "Type";
            // 
            // listBoxLocalFiles
            // 
            this.listBoxLocalFiles.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.listBoxLocalFiles.Font = new System.Drawing.Font("Calibri", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listBoxLocalFiles.FormattingEnabled = true;
            this.listBoxLocalFiles.ItemHeight = 21;
            this.listBoxLocalFiles.Location = new System.Drawing.Point(637, 549);
            this.listBoxLocalFiles.Name = "listBoxLocalFiles";
            this.listBoxLocalFiles.Size = new System.Drawing.Size(468, 88);
            this.listBoxLocalFiles.TabIndex = 4;
            this.listBoxLocalFiles.SelectedIndexChanged += new System.EventHandler(this.listBoxLocalFiles_SelectedIndexChanged);
            // 
            // listBoxLinks
            // 
            this.listBoxLinks.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.listBoxLinks.Font = new System.Drawing.Font("Calibri", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listBoxLinks.FormattingEnabled = true;
            this.listBoxLinks.ItemHeight = 21;
            this.listBoxLinks.Location = new System.Drawing.Point(637, 673);
            this.listBoxLinks.Name = "listBoxLinks";
            this.listBoxLinks.Size = new System.Drawing.Size(468, 88);
            this.listBoxLinks.TabIndex = 5;
            this.listBoxLinks.SelectedIndexChanged += new System.EventHandler(this.listBoxLinks_SelectedIndexChanged);
            // 
            // label6
            // 
            this.label6.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Calibri", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.Location = new System.Drawing.Point(633, 138);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(246, 21);
            this.label6.TabIndex = 100;
            this.label6.Text = "Image notes (you can modify this)";
            // 
            // label7
            // 
            this.label7.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.label7.AutoSize = true;
            this.label7.Font = new System.Drawing.Font("Calibri", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label7.Location = new System.Drawing.Point(633, 525);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(160, 21);
            this.label7.TabIndex = 100;
            this.label7.Text = "Component local files";
            // 
            // label8
            // 
            this.label8.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.label8.AutoSize = true;
            this.label8.Font = new System.Drawing.Font("Calibri", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label8.Location = new System.Drawing.Point(633, 649);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(128, 21);
            this.label8.TabIndex = 100;
            this.label8.Text = "Component links";
            // 
            // textBoxNote
            // 
            this.textBoxNote.BackColor = System.Drawing.SystemColors.Window;
            this.textBoxNote.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBoxNote.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBoxNote.Font = new System.Drawing.Font("Calibri", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxNote.Location = new System.Drawing.Point(2, 2);
            this.textBoxNote.Multiline = true;
            this.textBoxNote.Name = "textBoxNote";
            this.textBoxNote.Size = new System.Drawing.Size(464, 348);
            this.textBoxNote.TabIndex = 3;
            // 
            // labelPin
            // 
            this.labelPin.AutoSize = true;
            this.labelPin.BackColor = System.Drawing.Color.Khaki;
            this.labelPin.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.labelPin.Font = new System.Drawing.Font("Calibri", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelPin.Location = new System.Drawing.Point(15, 15);
            this.labelPin.Name = "labelPin";
            this.labelPin.Padding = new System.Windows.Forms.Padding(2);
            this.labelPin.Size = new System.Drawing.Size(38, 28);
            this.labelPin.TabIndex = 100;
            this.labelPin.Text = "Pin";
            // 
            // labelRegion
            // 
            this.labelRegion.AutoSize = true;
            this.labelRegion.BackColor = System.Drawing.Color.IndianRed;
            this.labelRegion.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.labelRegion.Font = new System.Drawing.Font("Calibri", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelRegion.ForeColor = System.Drawing.Color.White;
            this.labelRegion.Location = new System.Drawing.Point(125, 14);
            this.labelRegion.Name = "labelRegion";
            this.labelRegion.Padding = new System.Windows.Forms.Padding(2);
            this.labelRegion.Size = new System.Drawing.Size(66, 28);
            this.labelRegion.TabIndex = 100;
            this.labelRegion.Text = "Region";
            // 
            // labelName
            // 
            this.labelName.AutoSize = true;
            this.labelName.BackColor = System.Drawing.Color.Khaki;
            this.labelName.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.labelName.Font = new System.Drawing.Font("Calibri", 10.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelName.Location = new System.Drawing.Point(58, 14);
            this.labelName.Name = "labelName";
            this.labelName.Padding = new System.Windows.Forms.Padding(2);
            this.labelName.Size = new System.Drawing.Size(61, 28);
            this.labelName.TabIndex = 100;
            this.labelName.Text = "Name";
            // 
            // labelReading
            // 
            this.labelReading.AutoSize = true;
            this.labelReading.BackColor = System.Drawing.Color.WhiteSmoke;
            this.labelReading.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.labelReading.Font = new System.Drawing.Font("Calibri", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelReading.Location = new System.Drawing.Point(197, 14);
            this.labelReading.Name = "labelReading";
            this.labelReading.Padding = new System.Windows.Forms.Padding(2);
            this.labelReading.Size = new System.Drawing.Size(74, 28);
            this.labelReading.TabIndex = 100;
            this.labelReading.Text = "Reading";
            // 
            // labelImageX
            // 
            this.labelImageX.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.labelImageX.AutoSize = true;
            this.labelImageX.BackColor = System.Drawing.Color.WhiteSmoke;
            this.labelImageX.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.labelImageX.Font = new System.Drawing.Font("Calibri", 10.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelImageX.Location = new System.Drawing.Point(13, 612);
            this.labelImageX.Name = "labelImageX";
            this.labelImageX.Size = new System.Drawing.Size(66, 24);
            this.labelImageX.TabIndex = 100;
            this.labelImageX.Text = "ImageX";
            // 
            // textBoxOneliner
            // 
            this.textBoxOneliner.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textBoxOneliner.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBoxOneliner.Location = new System.Drawing.Point(5, 5);
            this.textBoxOneliner.Name = "textBoxOneliner";
            this.textBoxOneliner.Size = new System.Drawing.Size(458, 15);
            this.textBoxOneliner.TabIndex = 2;
            // 
            // panelNote
            // 
            this.panelNote.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelNote.BackColor = System.Drawing.Color.White;
            this.panelNote.Controls.Add(this.textBoxNote);
            this.panelNote.Location = new System.Drawing.Point(637, 160);
            this.panelNote.Name = "panelNote";
            this.panelNote.Padding = new System.Windows.Forms.Padding(2);
            this.panelNote.Size = new System.Drawing.Size(468, 352);
            this.panelNote.TabIndex = 3;
            // 
            // panelOneliner
            // 
            this.panelOneliner.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.panelOneliner.BackColor = System.Drawing.Color.White;
            this.panelOneliner.Controls.Add(this.textBoxOneliner);
            this.panelOneliner.Location = new System.Drawing.Point(637, 96);
            this.panelOneliner.Name = "panelOneliner";
            this.panelOneliner.Padding = new System.Windows.Forms.Padding(5);
            this.panelOneliner.Size = new System.Drawing.Size(468, 28);
            this.panelOneliner.TabIndex = 2;
            // 
            // panelImageAndThumbnails
            // 
            this.panelImageAndThumbnails.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelImageAndThumbnails.BackColor = System.Drawing.Color.White;
            this.panelImageAndThumbnails.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelImageAndThumbnails.Controls.Add(this.labelRegion);
            this.panelImageAndThumbnails.Controls.Add(this.labelName);
            this.panelImageAndThumbnails.Controls.Add(this.labelReading);
            this.panelImageAndThumbnails.Controls.Add(this.labelImageX);
            this.panelImageAndThumbnails.Controls.Add(this.pictureBoxImage);
            this.panelImageAndThumbnails.Location = new System.Drawing.Point(0, 0);
            this.panelImageAndThumbnails.Name = "panelImageAndThumbnails";
            this.panelImageAndThumbnails.Size = new System.Drawing.Size(626, 826);
            this.panelImageAndThumbnails.TabIndex = 101;
            // 
            // buttonRegionPal
            // 
            this.buttonRegionPal.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonRegionPal.Location = new System.Drawing.Point(638, 785);
            this.buttonRegionPal.Name = "buttonRegionPal";
            this.buttonRegionPal.Size = new System.Drawing.Size(93, 30);
            this.buttonRegionPal.TabIndex = 102;
            this.buttonRegionPal.Tag = "PAL";
            this.buttonRegionPal.Text = "PAL (123)";
            this.buttonRegionPal.UseVisualStyleBackColor = true;
            // 
            // buttonRegionNtsc
            // 
            this.buttonRegionNtsc.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonRegionNtsc.Location = new System.Drawing.Point(737, 785);
            this.buttonRegionNtsc.Name = "buttonRegionNtsc";
            this.buttonRegionNtsc.Size = new System.Drawing.Size(93, 30);
            this.buttonRegionNtsc.TabIndex = 103;
            this.buttonRegionNtsc.Tag = "NTSC";
            this.buttonRegionNtsc.Text = "NTSC (123)";
            this.buttonRegionNtsc.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Calibri", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(633, 72);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(390, 21);
            this.label2.TabIndex = 104;
            this.label2.Text = "Component one-liner description (you can modify this)";
            // 
            // buttonToggle
            // 
            this.buttonToggle.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonToggle.Location = new System.Drawing.Point(837, 785);
            this.buttonToggle.Name = "buttonToggle";
            this.buttonToggle.Size = new System.Drawing.Size(75, 30);
            this.buttonToggle.TabIndex = 105;
            this.buttonToggle.Text = "Toggle";
            this.buttonToggle.UseVisualStyleBackColor = true;
            // 
            // FormComponent
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1117, 827);
            this.Controls.Add(this.buttonToggle);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.buttonRegionNtsc);
            this.Controls.Add(this.buttonRegionPal);
            this.Controls.Add(this.labelPin);
            this.Controls.Add(this.panelImageAndThumbnails);
            this.Controls.Add(this.panelOneliner);
            this.Controls.Add(this.panelNote);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.listBoxLinks);
            this.Controls.Add(this.listBoxLocalFiles);
            this.Controls.Add(this.labelType);
            this.Controls.Add(this.labelDisplayName);
            this.Controls.Add(this.button1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(600, 678);
            this.Name = "FormComponent";
            this.Text = "Component Information";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxImage)).EndInit();
            this.panelNote.ResumeLayout(false);
            this.panelNote.PerformLayout();
            this.panelOneliner.ResumeLayout(false);
            this.panelOneliner.PerformLayout();
            this.panelImageAndThumbnails.ResumeLayout(false);
            this.panelImageAndThumbnails.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox pictureBoxImage;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Label labelDisplayName;
        private System.Windows.Forms.Label labelType;
        private System.Windows.Forms.ListBox listBoxLocalFiles;
        private System.Windows.Forms.ListBox listBoxLinks;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox textBoxNote;
        private System.Windows.Forms.Label labelPin;
        private System.Windows.Forms.Label labelRegion;
        private System.Windows.Forms.Label labelName;
        private System.Windows.Forms.Label labelReading;
        private System.Windows.Forms.Label labelImageX;
        private System.Windows.Forms.TextBox textBoxOneliner;
        private System.Windows.Forms.Panel panelNote;
        private System.Windows.Forms.Panel panelOneliner;
        private System.Windows.Forms.Panel panelImageAndThumbnails;
        private System.Windows.Forms.Button buttonRegionPal;
        private System.Windows.Forms.Button buttonRegionNtsc;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button buttonToggle;
    }
}