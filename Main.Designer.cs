namespace Commodore_Repair_Toolbox
{
    partial class Main
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Main));
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabSchematics = new System.Windows.Forms.TabPage();
            this.splitContainerSchematics = new System.Windows.Forms.SplitContainer();
            this.panelMain = new System.Windows.Forms.Panel();
            this.panelListMain = new System.Windows.Forms.Panel();
            this.panelListAutoscroll = new System.Windows.Forms.Panel();
            this.tabRessources = new System.Windows.Forms.TabPage();
            this.webView21 = new Microsoft.Web.WebView2.WinForms.WebView2();
            this.tabHelp = new System.Windows.Forms.TabPage();
            this.webView22 = new Microsoft.Web.WebView2.WinForms.WebView2();
            this.tabAbout = new System.Windows.Forms.TabPage();
            this.webView23 = new Microsoft.Web.WebView2.WinForms.WebView2();
            this.richTextBoxHelp = new System.Windows.Forms.RichTextBox();
            this.comboBoxHardware = new System.Windows.Forms.ComboBox();
            this.labelHardware = new System.Windows.Forms.Label();
            this.labelBoard = new System.Windows.Forms.Label();
            this.comboBoxBoard = new System.Windows.Forms.ComboBox();
            this.listBoxComponents = new System.Windows.Forms.ListBox();
            this.labelComponents = new System.Windows.Forms.Label();
            this.buttonClear = new System.Windows.Forms.Button();
            this.label7 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.listBoxCategories = new System.Windows.Forms.ListBox();
            this.labelCategories = new System.Windows.Forms.Label();
            this.buttonAll = new System.Windows.Forms.Button();
            this.buttonFullscreen = new System.Windows.Forms.Button();
            this.panelBehindTab = new System.Windows.Forms.Panel();
            this.textBoxFilterComponents = new System.Windows.Forms.TextBox();
            this.checkBoxBlink = new System.Windows.Forms.CheckBox();
            this.tabControl.SuspendLayout();
            this.tabSchematics.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerSchematics)).BeginInit();
            this.splitContainerSchematics.Panel1.SuspendLayout();
            this.splitContainerSchematics.Panel2.SuspendLayout();
            this.splitContainerSchematics.SuspendLayout();
            this.panelListMain.SuspendLayout();
            this.tabRessources.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.webView21)).BeginInit();
            this.tabHelp.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.webView22)).BeginInit();
            this.tabAbout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.webView23)).BeginInit();
            this.panelBehindTab.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl
            // 
            this.tabControl.Controls.Add(this.tabSchematics);
            this.tabControl.Controls.Add(this.tabRessources);
            this.tabControl.Controls.Add(this.tabHelp);
            this.tabControl.Controls.Add(this.tabAbout);
            this.tabControl.Font = new System.Drawing.Font("Calibri", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tabControl.Location = new System.Drawing.Point(30, 26);
            this.tabControl.Margin = new System.Windows.Forms.Padding(0);
            this.tabControl.Name = "tabControl";
            this.tabControl.Padding = new System.Drawing.Point(0, 0);
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(721, 570);
            this.tabControl.TabIndex = 0;
            this.tabControl.SelectedIndexChanged += new System.EventHandler(this.tabControl1_SelectedIndexChanged);
            // 
            // tabSchematics
            // 
            this.tabSchematics.Controls.Add(this.splitContainerSchematics);
            this.tabSchematics.Location = new System.Drawing.Point(4, 30);
            this.tabSchematics.Margin = new System.Windows.Forms.Padding(0);
            this.tabSchematics.Name = "tabSchematics";
            this.tabSchematics.Size = new System.Drawing.Size(713, 536);
            this.tabSchematics.TabIndex = 0;
            this.tabSchematics.Text = "Schematics";
            this.tabSchematics.UseVisualStyleBackColor = true;
            // 
            // splitContainerSchematics
            // 
            this.splitContainerSchematics.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerSchematics.Location = new System.Drawing.Point(0, 0);
            this.splitContainerSchematics.Margin = new System.Windows.Forms.Padding(0);
            this.splitContainerSchematics.Name = "splitContainerSchematics";
            // 
            // splitContainerSchematics.Panel1
            // 
            this.splitContainerSchematics.Panel1.Controls.Add(this.panelMain);
            this.splitContainerSchematics.Panel1MinSize = 400;
            // 
            // splitContainerSchematics.Panel2
            // 
            this.splitContainerSchematics.Panel2.Controls.Add(this.panelListMain);
            this.splitContainerSchematics.Panel2MinSize = 100;
            this.splitContainerSchematics.Size = new System.Drawing.Size(713, 536);
            this.splitContainerSchematics.SplitterDistance = 600;
            this.splitContainerSchematics.SplitterWidth = 11;
            this.splitContainerSchematics.TabIndex = 7;
            this.splitContainerSchematics.SplitterMoved += new System.Windows.Forms.SplitterEventHandler(this.splitContainer1_SplitterMoved);
            // 
            // panelMain
            // 
            this.panelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelMain.Location = new System.Drawing.Point(0, 0);
            this.panelMain.Margin = new System.Windows.Forms.Padding(0);
            this.panelMain.Name = "panelMain";
            this.panelMain.Size = new System.Drawing.Size(600, 536);
            this.panelMain.TabIndex = 5;
            // 
            // panelListMain
            // 
            this.panelListMain.Controls.Add(this.panelListAutoscroll);
            this.panelListMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelListMain.Location = new System.Drawing.Point(0, 0);
            this.panelListMain.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.panelListMain.Name = "panelListMain";
            this.panelListMain.Size = new System.Drawing.Size(102, 536);
            this.panelListMain.TabIndex = 6;
            // 
            // panelListAutoscroll
            // 
            this.panelListAutoscroll.AutoScroll = true;
            this.panelListAutoscroll.BackColor = System.Drawing.Color.Transparent;
            this.panelListAutoscroll.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelListAutoscroll.Location = new System.Drawing.Point(0, 0);
            this.panelListAutoscroll.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.panelListAutoscroll.Name = "panelListAutoscroll";
            this.panelListAutoscroll.Size = new System.Drawing.Size(102, 536);
            this.panelListAutoscroll.TabIndex = 0;
            // 
            // tabRessources
            // 
            this.tabRessources.BackColor = System.Drawing.Color.White;
            this.tabRessources.Controls.Add(this.webView21);
            this.tabRessources.Location = new System.Drawing.Point(4, 30);
            this.tabRessources.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tabRessources.Name = "tabRessources";
            this.tabRessources.Size = new System.Drawing.Size(713, 536);
            this.tabRessources.TabIndex = 3;
            this.tabRessources.Text = "Ressources";
            // 
            // webView21
            // 
            this.webView21.AllowExternalDrop = true;
            this.webView21.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.webView21.CreationProperties = null;
            this.webView21.DefaultBackgroundColor = System.Drawing.Color.White;
            this.webView21.Location = new System.Drawing.Point(0, 3);
            this.webView21.Name = "webView21";
            this.webView21.Size = new System.Drawing.Size(710, 530);
            this.webView21.TabIndex = 5;
            this.webView21.ZoomFactor = 1D;
            // 
            // tabHelp
            // 
            this.tabHelp.BackColor = System.Drawing.Color.White;
            this.tabHelp.Controls.Add(this.webView22);
            this.tabHelp.Location = new System.Drawing.Point(4, 30);
            this.tabHelp.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tabHelp.Name = "tabHelp";
            this.tabHelp.Size = new System.Drawing.Size(713, 536);
            this.tabHelp.TabIndex = 6;
            this.tabHelp.Text = "Help";
            // 
            // webView22
            // 
            this.webView22.AllowExternalDrop = true;
            this.webView22.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.webView22.CreationProperties = null;
            this.webView22.DefaultBackgroundColor = System.Drawing.Color.White;
            this.webView22.Location = new System.Drawing.Point(0, 3);
            this.webView22.Name = "webView22";
            this.webView22.Size = new System.Drawing.Size(710, 530);
            this.webView22.TabIndex = 6;
            this.webView22.ZoomFactor = 1D;
            // 
            // tabAbout
            // 
            this.tabAbout.BackColor = System.Drawing.Color.White;
            this.tabAbout.Controls.Add(this.webView23);
            this.tabAbout.Font = new System.Drawing.Font("Calibri", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tabAbout.Location = new System.Drawing.Point(4, 30);
            this.tabAbout.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tabAbout.Name = "tabAbout";
            this.tabAbout.Size = new System.Drawing.Size(713, 536);
            this.tabAbout.TabIndex = 5;
            this.tabAbout.Text = "About";
            // 
            // webView23
            // 
            this.webView23.AllowExternalDrop = true;
            this.webView23.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.webView23.CreationProperties = null;
            this.webView23.DefaultBackgroundColor = System.Drawing.Color.White;
            this.webView23.Location = new System.Drawing.Point(0, 3);
            this.webView23.Name = "webView23";
            this.webView23.Size = new System.Drawing.Size(710, 530);
            this.webView23.TabIndex = 7;
            this.webView23.ZoomFactor = 1D;
            // 
            // richTextBoxHelp
            // 
            this.richTextBoxHelp.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.richTextBoxHelp.BackColor = System.Drawing.Color.White;
            this.richTextBoxHelp.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.richTextBoxHelp.Location = new System.Drawing.Point(552, 3);
            this.richTextBoxHelp.Name = "richTextBoxHelp";
            this.richTextBoxHelp.ReadOnly = true;
            this.richTextBoxHelp.Size = new System.Drawing.Size(192, 211);
            this.richTextBoxHelp.TabIndex = 0;
            this.richTextBoxHelp.Text = "Text set in code ...";
            this.richTextBoxHelp.LinkClicked += new System.Windows.Forms.LinkClickedEventHandler(this.richTextBox_LinkClicked);
            // 
            // comboBoxHardware
            // 
            this.comboBoxHardware.BackColor = System.Drawing.Color.White;
            this.comboBoxHardware.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxHardware.Font = new System.Drawing.Font("Calibri", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.comboBoxHardware.FormattingEnabled = true;
            this.comboBoxHardware.Location = new System.Drawing.Point(12, 34);
            this.comboBoxHardware.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.comboBoxHardware.Name = "comboBoxHardware";
            this.comboBoxHardware.Size = new System.Drawing.Size(273, 29);
            this.comboBoxHardware.TabIndex = 1;
            this.comboBoxHardware.SelectedIndexChanged += new System.EventHandler(this.comboBox1_SelectedIndexChanged);
            // 
            // labelHardware
            // 
            this.labelHardware.AutoSize = true;
            this.labelHardware.Font = new System.Drawing.Font("Calibri", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelHardware.Location = new System.Drawing.Point(8, 13);
            this.labelHardware.Name = "labelHardware";
            this.labelHardware.Size = new System.Drawing.Size(78, 21);
            this.labelHardware.TabIndex = 2;
            this.labelHardware.Text = "Hardware";
            // 
            // labelBoard
            // 
            this.labelBoard.AutoSize = true;
            this.labelBoard.Font = new System.Drawing.Font("Calibri", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelBoard.Location = new System.Drawing.Point(8, 71);
            this.labelBoard.Name = "labelBoard";
            this.labelBoard.Size = new System.Drawing.Size(51, 21);
            this.labelBoard.TabIndex = 4;
            this.labelBoard.Text = "Board";
            // 
            // comboBoxBoard
            // 
            this.comboBoxBoard.BackColor = System.Drawing.Color.White;
            this.comboBoxBoard.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxBoard.Font = new System.Drawing.Font("Calibri", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.comboBoxBoard.FormattingEnabled = true;
            this.comboBoxBoard.Location = new System.Drawing.Point(12, 92);
            this.comboBoxBoard.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.comboBoxBoard.Name = "comboBoxBoard";
            this.comboBoxBoard.Size = new System.Drawing.Size(273, 29);
            this.comboBoxBoard.TabIndex = 3;
            this.comboBoxBoard.SelectedIndexChanged += new System.EventHandler(this.comboBox2_SelectedIndexChanged);
            // 
            // listBoxComponents
            // 
            this.listBoxComponents.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.listBoxComponents.Font = new System.Drawing.Font("Calibri", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listBoxComponents.FormattingEnabled = true;
            this.listBoxComponents.ItemHeight = 21;
            this.listBoxComponents.Location = new System.Drawing.Point(12, 295);
            this.listBoxComponents.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.listBoxComponents.Name = "listBoxComponents";
            this.listBoxComponents.SelectionMode = System.Windows.Forms.SelectionMode.MultiSimple;
            this.listBoxComponents.Size = new System.Drawing.Size(273, 235);
            this.listBoxComponents.TabIndex = 7;
            this.listBoxComponents.SelectedIndexChanged += new System.EventHandler(this.listBox1_SelectedIndexChanged);
            // 
            // labelComponents
            // 
            this.labelComponents.AutoSize = true;
            this.labelComponents.Font = new System.Drawing.Font("Calibri", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelComponents.Location = new System.Drawing.Point(8, 274);
            this.labelComponents.Name = "labelComponents";
            this.labelComponents.Size = new System.Drawing.Size(117, 21);
            this.labelComponents.TabIndex = 8;
            this.labelComponents.Text = "Component list";
            // 
            // buttonClear
            // 
            this.buttonClear.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonClear.Font = new System.Drawing.Font("Calibri", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonClear.Location = new System.Drawing.Point(12, 580);
            this.buttonClear.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonClear.Name = "buttonClear";
            this.buttonClear.Size = new System.Drawing.Size(62, 30);
            this.buttonClear.TabIndex = 9;
            this.buttonClear.Text = "Clear";
            this.buttonClear.UseVisualStyleBackColor = true;
            this.buttonClear.Click += new System.EventHandler(this.button1_Click);
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.BackColor = System.Drawing.SystemColors.Info;
            this.label7.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.label7.Location = new System.Drawing.Point(316, 591);
            this.label7.Name = "label7";
            this.label7.Padding = new System.Windows.Forms.Padding(5);
            this.label7.Size = new System.Drawing.Size(105, 28);
            this.label7.TabIndex = 1;
            this.label7.Text = "U27 (3 places)";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.label5.Location = new System.Drawing.Point(10, 9);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(106, 18);
            this.label5.TabIndex = 0;
            this.label5.Text = "Schematic 1 of 2";
            // 
            // listBoxCategories
            // 
            this.listBoxCategories.Font = new System.Drawing.Font("Calibri", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listBoxCategories.FormattingEnabled = true;
            this.listBoxCategories.ItemHeight = 21;
            this.listBoxCategories.Location = new System.Drawing.Point(12, 153);
            this.listBoxCategories.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.listBoxCategories.Name = "listBoxCategories";
            this.listBoxCategories.SelectionMode = System.Windows.Forms.SelectionMode.MultiSimple;
            this.listBoxCategories.Size = new System.Drawing.Size(273, 109);
            this.listBoxCategories.Sorted = true;
            this.listBoxCategories.TabIndex = 10;
            this.listBoxCategories.SelectedIndexChanged += new System.EventHandler(this.listBox2_SelectedIndexChanged);
            // 
            // labelCategories
            // 
            this.labelCategories.AutoSize = true;
            this.labelCategories.Font = new System.Drawing.Font("Calibri", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelCategories.Location = new System.Drawing.Point(8, 132);
            this.labelCategories.Name = "labelCategories";
            this.labelCategories.Size = new System.Drawing.Size(207, 21);
            this.labelCategories.TabIndex = 11;
            this.labelCategories.Text = "Show component categories";
            // 
            // buttonAll
            // 
            this.buttonAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonAll.Font = new System.Drawing.Font("Calibri", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonAll.Location = new System.Drawing.Point(80, 580);
            this.buttonAll.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonAll.Name = "buttonAll";
            this.buttonAll.Size = new System.Drawing.Size(71, 30);
            this.buttonAll.TabIndex = 12;
            this.buttonAll.Text = "All";
            this.buttonAll.UseVisualStyleBackColor = true;
            this.buttonAll.Click += new System.EventHandler(this.button2_Click);
            // 
            // buttonFullscreen
            // 
            this.buttonFullscreen.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonFullscreen.Font = new System.Drawing.Font("Calibri", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonFullscreen.Location = new System.Drawing.Point(157, 579);
            this.buttonFullscreen.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.buttonFullscreen.Name = "buttonFullscreen";
            this.buttonFullscreen.Size = new System.Drawing.Size(128, 30);
            this.buttonFullscreen.TabIndex = 13;
            this.buttonFullscreen.Text = "Fullscreen";
            this.buttonFullscreen.UseVisualStyleBackColor = true;
            this.buttonFullscreen.Click += new System.EventHandler(this.buttonFullscreen_Click);
            // 
            // panelBehindTab
            // 
            this.panelBehindTab.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelBehindTab.BackColor = System.Drawing.Color.WhiteSmoke;
            this.panelBehindTab.Controls.Add(this.tabControl);
            this.panelBehindTab.Controls.Add(this.richTextBoxHelp);
            this.panelBehindTab.Location = new System.Drawing.Point(299, 13);
            this.panelBehindTab.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.panelBehindTab.Name = "panelBehindTab";
            this.panelBehindTab.Size = new System.Drawing.Size(771, 627);
            this.panelBehindTab.TabIndex = 14;
            // 
            // textBoxFilterComponents
            // 
            this.textBoxFilterComponents.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.textBoxFilterComponents.Location = new System.Drawing.Point(12, 544);
            this.textBoxFilterComponents.Name = "textBoxFilterComponents";
            this.textBoxFilterComponents.Size = new System.Drawing.Size(273, 28);
            this.textBoxFilterComponents.TabIndex = 1;
            // 
            // checkBoxBlink
            // 
            this.checkBoxBlink.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.checkBoxBlink.AutoSize = true;
            this.checkBoxBlink.Font = new System.Drawing.Font("Calibri", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.checkBoxBlink.Location = new System.Drawing.Point(12, 615);
            this.checkBoxBlink.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.checkBoxBlink.Name = "checkBoxBlink";
            this.checkBoxBlink.Size = new System.Drawing.Size(218, 25);
            this.checkBoxBlink.TabIndex = 15;
            this.checkBoxBlink.Text = "Blink selected components";
            this.checkBoxBlink.UseVisualStyleBackColor = true;
            // 
            // Main
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 21F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.WhiteSmoke;
            this.ClientSize = new System.Drawing.Size(1082, 653);
            this.Controls.Add(this.textBoxFilterComponents);
            this.Controls.Add(this.checkBoxBlink);
            this.Controls.Add(this.panelBehindTab);
            this.Controls.Add(this.buttonFullscreen);
            this.Controls.Add(this.buttonAll);
            this.Controls.Add(this.labelCategories);
            this.Controls.Add(this.listBoxCategories);
            this.Controls.Add(this.buttonClear);
            this.Controls.Add(this.labelComponents);
            this.Controls.Add(this.listBoxComponents);
            this.Controls.Add(this.labelBoard);
            this.Controls.Add(this.comboBoxBoard);
            this.Controls.Add(this.labelHardware);
            this.Controls.Add(this.comboBoxHardware);
            this.Font = new System.Drawing.Font("Calibri", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.MinimumSize = new System.Drawing.Size(1100, 600);
            this.Name = "Main";
            this.Text = "Commodore Repair Toolbox";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.tabControl.ResumeLayout(false);
            this.tabSchematics.ResumeLayout(false);
            this.splitContainerSchematics.Panel1.ResumeLayout(false);
            this.splitContainerSchematics.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerSchematics)).EndInit();
            this.splitContainerSchematics.ResumeLayout(false);
            this.panelListMain.ResumeLayout(false);
            this.tabRessources.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.webView21)).EndInit();
            this.tabHelp.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.webView22)).EndInit();
            this.tabAbout.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.webView23)).EndInit();
            this.panelBehindTab.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabSchematics;
        private System.Windows.Forms.TabPage tabRessources;
        private System.Windows.Forms.TabPage tabAbout;
        private System.Windows.Forms.ComboBox comboBoxHardware;
        private System.Windows.Forms.Label labelHardware;
        private System.Windows.Forms.Label labelBoard;
        private System.Windows.Forms.ComboBox comboBoxBoard;
        private System.Windows.Forms.ListBox listBoxComponents;
        private System.Windows.Forms.Label labelComponents;
        private System.Windows.Forms.Button buttonClear;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Panel panelMain;
        private System.Windows.Forms.Panel panelListMain;
        private System.Windows.Forms.Panel panelListAutoscroll;
        private System.Windows.Forms.ListBox listBoxCategories;
        private System.Windows.Forms.Label labelCategories;
        private System.Windows.Forms.Button buttonAll;
        private System.Windows.Forms.Button buttonFullscreen;
        private System.Windows.Forms.SplitContainer splitContainerSchematics;
        private System.Windows.Forms.Panel panelBehindTab;
        private System.Windows.Forms.CheckBox checkBoxBlink;
        private System.Windows.Forms.TabPage tabHelp;
        private System.Windows.Forms.RichTextBox richTextBoxHelp;
        private System.Windows.Forms.TextBox textBoxFilterComponents;
        private Microsoft.Web.WebView2.WinForms.WebView2 webView21;
        private Microsoft.Web.WebView2.WinForms.WebView2 webView22;
        private Microsoft.Web.WebView2.WinForms.WebView2 webView23;
    }
}