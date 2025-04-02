﻿namespace Commodore_Repair_Toolbox
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
            this.panelThumbnails = new System.Windows.Forms.Panel();
            this.tabOverview = new System.Windows.Forms.TabPage();
            this.webView2Overview = new Microsoft.Web.WebView2.WinForms.WebView2();
            this.tabRessources = new System.Windows.Forms.TabPage();
            this.webView2Ressources = new Microsoft.Web.WebView2.WinForms.WebView2();
            this.tabFeedback = new System.Windows.Forms.TabPage();
            this.label9 = new System.Windows.Forms.Label();
            this.buttonSendFeedback = new System.Windows.Forms.Button();
            this.textBox6 = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.textBoxFeedback = new System.Windows.Forms.TextBox();
            this.checkBoxAttachExcel = new System.Windows.Forms.CheckBox();
            this.textBox5 = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.textBoxEmail = new System.Windows.Forms.TextBox();
            this.textBox2 = new System.Windows.Forms.TextBox();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.tabHelp = new System.Windows.Forms.TabPage();
            this.webView2Help = new Microsoft.Web.WebView2.WinForms.WebView2();
            this.tabAbout = new System.Windows.Forms.TabPage();
            this.webView2About = new Microsoft.Web.WebView2.WinForms.WebView2();
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
            this.buttonResize = new System.Windows.Forms.Button();
            this.tabControl.SuspendLayout();
            this.tabSchematics.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerSchematics)).BeginInit();
            this.splitContainerSchematics.Panel1.SuspendLayout();
            this.splitContainerSchematics.Panel2.SuspendLayout();
            this.splitContainerSchematics.SuspendLayout();
            this.tabOverview.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.webView2Overview)).BeginInit();
            this.tabRessources.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.webView2Ressources)).BeginInit();
            this.tabFeedback.SuspendLayout();
            this.tabHelp.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.webView2Help)).BeginInit();
            this.tabAbout.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.webView2About)).BeginInit();
            this.panelBehindTab.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl
            // 
            this.tabControl.Controls.Add(this.tabSchematics);
            this.tabControl.Controls.Add(this.tabOverview);
            this.tabControl.Controls.Add(this.tabRessources);
            this.tabControl.Controls.Add(this.tabFeedback);
            this.tabControl.Controls.Add(this.tabHelp);
            this.tabControl.Controls.Add(this.tabAbout);
            this.tabControl.Font = new System.Drawing.Font("Calibri", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tabControl.Location = new System.Drawing.Point(30, 26);
            this.tabControl.Margin = new System.Windows.Forms.Padding(0);
            this.tabControl.Name = "tabControl";
            this.tabControl.Padding = new System.Drawing.Point(0, 0);
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(728, 570);
            this.tabControl.TabIndex = 0;
            this.tabControl.SelectedIndexChanged += new System.EventHandler(this.tabControl1_SelectedIndexChanged);
            // 
            // tabSchematics
            // 
            this.tabSchematics.Controls.Add(this.splitContainerSchematics);
            this.tabSchematics.Location = new System.Drawing.Point(4, 30);
            this.tabSchematics.Margin = new System.Windows.Forms.Padding(0);
            this.tabSchematics.Name = "tabSchematics";
            this.tabSchematics.Size = new System.Drawing.Size(720, 536);
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
            this.splitContainerSchematics.Panel2.Controls.Add(this.panelThumbnails);
            this.splitContainerSchematics.Panel2MinSize = 100;
            this.splitContainerSchematics.Size = new System.Drawing.Size(720, 536);
            this.splitContainerSchematics.SplitterDistance = 605;
            this.splitContainerSchematics.SplitterWidth = 11;
            this.splitContainerSchematics.TabIndex = 7;
            // 
            // panelMain
            // 
            this.panelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelMain.Location = new System.Drawing.Point(0, 0);
            this.panelMain.Margin = new System.Windows.Forms.Padding(0);
            this.panelMain.Name = "panelMain";
            this.panelMain.Size = new System.Drawing.Size(605, 536);
            this.panelMain.TabIndex = 5;
            // 
            // panelThumbnails
            // 
            this.panelThumbnails.AutoScroll = true;
            this.panelThumbnails.BackColor = System.Drawing.Color.Transparent;
            this.panelThumbnails.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelThumbnails.Location = new System.Drawing.Point(0, 0);
            this.panelThumbnails.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.panelThumbnails.Name = "panelThumbnails";
            this.panelThumbnails.Size = new System.Drawing.Size(104, 536);
            this.panelThumbnails.TabIndex = 0;
            // 
            // tabOverview
            // 
            this.tabOverview.Controls.Add(this.webView2Overview);
            this.tabOverview.Location = new System.Drawing.Point(4, 30);
            this.tabOverview.Name = "tabOverview";
            this.tabOverview.Size = new System.Drawing.Size(720, 536);
            this.tabOverview.TabIndex = 8;
            this.tabOverview.Text = "Overview";
            this.tabOverview.UseVisualStyleBackColor = true;
            // 
            // webView2Overview
            // 
            this.webView2Overview.AllowExternalDrop = true;
            this.webView2Overview.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.webView2Overview.BackColor = System.Drawing.Color.White;
            this.webView2Overview.CreationProperties = null;
            this.webView2Overview.DefaultBackgroundColor = System.Drawing.Color.White;
            this.webView2Overview.Location = new System.Drawing.Point(0, 3);
            this.webView2Overview.Name = "webView2Overview";
            this.webView2Overview.Size = new System.Drawing.Size(710, 530);
            this.webView2Overview.TabIndex = 6;
            this.webView2Overview.ZoomFactor = 1D;
            // 
            // tabRessources
            // 
            this.tabRessources.BackColor = System.Drawing.Color.White;
            this.tabRessources.Controls.Add(this.webView2Ressources);
            this.tabRessources.Location = new System.Drawing.Point(4, 30);
            this.tabRessources.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tabRessources.Name = "tabRessources";
            this.tabRessources.Size = new System.Drawing.Size(720, 536);
            this.tabRessources.TabIndex = 3;
            this.tabRessources.Text = "Ressources";
            // 
            // webView2Ressources
            // 
            this.webView2Ressources.AllowExternalDrop = true;
            this.webView2Ressources.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.webView2Ressources.CreationProperties = null;
            this.webView2Ressources.DefaultBackgroundColor = System.Drawing.Color.White;
            this.webView2Ressources.Location = new System.Drawing.Point(0, 3);
            this.webView2Ressources.Name = "webView2Ressources";
            this.webView2Ressources.Size = new System.Drawing.Size(710, 530);
            this.webView2Ressources.TabIndex = 5;
            this.webView2Ressources.ZoomFactor = 1D;
            // 
            // tabFeedback
            // 
            this.tabFeedback.Controls.Add(this.label9);
            this.tabFeedback.Controls.Add(this.buttonSendFeedback);
            this.tabFeedback.Controls.Add(this.textBox6);
            this.tabFeedback.Controls.Add(this.label8);
            this.tabFeedback.Controls.Add(this.textBoxFeedback);
            this.tabFeedback.Controls.Add(this.checkBoxAttachExcel);
            this.tabFeedback.Controls.Add(this.textBox5);
            this.tabFeedback.Controls.Add(this.label6);
            this.tabFeedback.Controls.Add(this.textBoxEmail);
            this.tabFeedback.Controls.Add(this.textBox2);
            this.tabFeedback.Controls.Add(this.textBox1);
            this.tabFeedback.Controls.Add(this.label4);
            this.tabFeedback.Controls.Add(this.label3);
            this.tabFeedback.Controls.Add(this.label2);
            this.tabFeedback.Controls.Add(this.label1);
            this.tabFeedback.Location = new System.Drawing.Point(4, 30);
            this.tabFeedback.Name = "tabFeedback";
            this.tabFeedback.Size = new System.Drawing.Size(720, 536);
            this.tabFeedback.TabIndex = 7;
            this.tabFeedback.Text = "Feedback";
            this.tabFeedback.UseVisualStyleBackColor = true;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(132, 18);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(297, 21);
            this.label9.TabIndex = 16;
            this.label9.Text = "Information being submitted in feedback:";
            // 
            // buttonSendFeedback
            // 
            this.buttonSendFeedback.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonSendFeedback.Location = new System.Drawing.Point(14, 495);
            this.buttonSendFeedback.Name = "buttonSendFeedback";
            this.buttonSendFeedback.Size = new System.Drawing.Size(211, 29);
            this.buttonSendFeedback.TabIndex = 8;
            this.buttonSendFeedback.Text = "Send feedback to developer";
            this.buttonSendFeedback.UseVisualStyleBackColor = true;
            this.buttonSendFeedback.Click += new System.EventHandler(this.buttonSendFeedback_Click);
            // 
            // textBox6
            // 
            this.textBox6.Location = new System.Drawing.Point(136, 138);
            this.textBox6.Name = "textBox6";
            this.textBox6.ReadOnly = true;
            this.textBox6.Size = new System.Drawing.Size(297, 28);
            this.textBox6.TabIndex = 15;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(10, 141);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(109, 21);
            this.label8.TabIndex = 14;
            this.label8.Text = "Excel data file:";
            // 
            // textBoxFeedback
            // 
            this.textBoxFeedback.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxFeedback.Location = new System.Drawing.Point(14, 267);
            this.textBoxFeedback.Multiline = true;
            this.textBoxFeedback.Name = "textBoxFeedback";
            this.textBoxFeedback.Size = new System.Drawing.Size(684, 194);
            this.textBoxFeedback.TabIndex = 13;
            // 
            // checkBoxAttachExcel
            // 
            this.checkBoxAttachExcel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.checkBoxAttachExcel.AutoSize = true;
            this.checkBoxAttachExcel.Location = new System.Drawing.Point(14, 464);
            this.checkBoxAttachExcel.Name = "checkBoxAttachExcel";
            this.checkBoxAttachExcel.Size = new System.Drawing.Size(305, 25);
            this.checkBoxAttachExcel.TabIndex = 12;
            this.checkBoxAttachExcel.Text = "Attach Excel data file for selected board";
            this.checkBoxAttachExcel.UseVisualStyleBackColor = true;
            this.checkBoxAttachExcel.CheckedChanged += new System.EventHandler(this.checkBox1_CheckedChanged);
            // 
            // textBox5
            // 
            this.textBox5.Location = new System.Drawing.Point(136, 42);
            this.textBox5.Name = "textBox5";
            this.textBox5.ReadOnly = true;
            this.textBox5.Size = new System.Drawing.Size(297, 28);
            this.textBox5.TabIndex = 11;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(8, 45);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(143, 21);
            this.label6.TabIndex = 10;
            this.label6.Text = "Selected hardware:";
            // 
            // textBoxEmail
            // 
            this.textBoxEmail.Location = new System.Drawing.Point(14, 202);
            this.textBoxEmail.Name = "textBoxEmail";
            this.textBoxEmail.Size = new System.Drawing.Size(419, 28);
            this.textBoxEmail.TabIndex = 6;
            // 
            // textBox2
            // 
            this.textBox2.Location = new System.Drawing.Point(136, 106);
            this.textBox2.Name = "textBox2";
            this.textBox2.ReadOnly = true;
            this.textBox2.Size = new System.Drawing.Size(297, 28);
            this.textBox2.TabIndex = 5;
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(136, 74);
            this.textBox1.Name = "textBox1";
            this.textBox1.ReadOnly = true;
            this.textBox1.Size = new System.Drawing.Size(297, 28);
            this.textBox1.TabIndex = 4;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(10, 243);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(187, 21);
            this.label4.TabIndex = 3;
            this.label4.Text = "Your feedback (text only):";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(10, 182);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(482, 21);
            this.label3.TabIndex = 2;
            this.label3.Text = "Your email address (optional, but needed if you want any response):";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(8, 109);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(146, 21);
            this.label2.TabIndex = 1;
            this.label2.Text = "Selected schematic:";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(8, 77);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(118, 21);
            this.label1.TabIndex = 0;
            this.label1.Text = "Selected board:";
            // 
            // tabHelp
            // 
            this.tabHelp.BackColor = System.Drawing.Color.White;
            this.tabHelp.Controls.Add(this.webView2Help);
            this.tabHelp.Location = new System.Drawing.Point(4, 30);
            this.tabHelp.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tabHelp.Name = "tabHelp";
            this.tabHelp.Size = new System.Drawing.Size(720, 536);
            this.tabHelp.TabIndex = 6;
            this.tabHelp.Text = "Help";
            // 
            // webView2Help
            // 
            this.webView2Help.AllowExternalDrop = true;
            this.webView2Help.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.webView2Help.CreationProperties = null;
            this.webView2Help.DefaultBackgroundColor = System.Drawing.Color.White;
            this.webView2Help.Location = new System.Drawing.Point(0, 3);
            this.webView2Help.Name = "webView2Help";
            this.webView2Help.Size = new System.Drawing.Size(710, 530);
            this.webView2Help.TabIndex = 6;
            this.webView2Help.ZoomFactor = 1D;
            // 
            // tabAbout
            // 
            this.tabAbout.BackColor = System.Drawing.Color.White;
            this.tabAbout.Controls.Add(this.webView2About);
            this.tabAbout.Font = new System.Drawing.Font("Calibri", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tabAbout.Location = new System.Drawing.Point(4, 30);
            this.tabAbout.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.tabAbout.Name = "tabAbout";
            this.tabAbout.Size = new System.Drawing.Size(720, 536);
            this.tabAbout.TabIndex = 5;
            this.tabAbout.Text = "About";
            // 
            // webView2About
            // 
            this.webView2About.AllowExternalDrop = true;
            this.webView2About.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.webView2About.CreationProperties = null;
            this.webView2About.DefaultBackgroundColor = System.Drawing.Color.White;
            this.webView2About.Location = new System.Drawing.Point(0, 3);
            this.webView2About.Name = "webView2About";
            this.webView2About.Size = new System.Drawing.Size(710, 530);
            this.webView2About.TabIndex = 7;
            this.webView2About.ZoomFactor = 1D;
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
            this.buttonClear.Click += new System.EventHandler(this.buttonClear_Click);
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
            this.panelBehindTab.Controls.Add(this.buttonResize);
            this.panelBehindTab.Controls.Add(this.tabControl);
            this.panelBehindTab.Location = new System.Drawing.Point(299, 13);
            this.panelBehindTab.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.panelBehindTab.Name = "panelBehindTab";
            this.panelBehindTab.Size = new System.Drawing.Size(782, 641);
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
            // buttonResize
            // 
            this.buttonResize.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonResize.Font = new System.Drawing.Font("Calibri", 7.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonResize.Location = new System.Drawing.Point(732, -1);
            this.buttonResize.Name = "buttonResize";
            this.buttonResize.Size = new System.Drawing.Size(50, 25);
            this.buttonResize.TabIndex = 1;
            this.buttonResize.Text = "Resize";
            this.buttonResize.UseVisualStyleBackColor = true;
            this.buttonResize.Click += new System.EventHandler(this.button1_Click);
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
            this.tabOverview.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.webView2Overview)).EndInit();
            this.tabRessources.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.webView2Ressources)).EndInit();
            this.tabFeedback.ResumeLayout(false);
            this.tabFeedback.PerformLayout();
            this.tabHelp.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.webView2Help)).EndInit();
            this.tabAbout.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.webView2About)).EndInit();
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
        private System.Windows.Forms.Panel panelThumbnails;
        private System.Windows.Forms.ListBox listBoxCategories;
        private System.Windows.Forms.Label labelCategories;
        private System.Windows.Forms.Button buttonAll;
        private System.Windows.Forms.Button buttonFullscreen;
        private System.Windows.Forms.SplitContainer splitContainerSchematics;
        private System.Windows.Forms.Panel panelBehindTab;
        private System.Windows.Forms.CheckBox checkBoxBlink;
        private System.Windows.Forms.TabPage tabHelp;
        private System.Windows.Forms.TextBox textBoxFilterComponents;
        private Microsoft.Web.WebView2.WinForms.WebView2 webView2Ressources;
        private Microsoft.Web.WebView2.WinForms.WebView2 webView2Help;
        private Microsoft.Web.WebView2.WinForms.WebView2 webView2About;
        private System.Windows.Forms.TabPage tabFeedback;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button buttonSendFeedback;
        private System.Windows.Forms.TextBox textBoxEmail;
        private System.Windows.Forms.TextBox textBox2;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.CheckBox checkBoxAttachExcel;
        private System.Windows.Forms.TextBox textBox5;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox textBoxFeedback;
        private System.Windows.Forms.TextBox textBox6;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TabPage tabOverview;
        private Microsoft.Web.WebView2.WinForms.WebView2 webView2Overview;
        private System.Windows.Forms.Button buttonResize;
    }
}