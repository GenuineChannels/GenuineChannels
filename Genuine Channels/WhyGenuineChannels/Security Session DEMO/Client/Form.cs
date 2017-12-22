using System;
using System.Data;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Windows.Forms;
using System.Text;
using System.Threading;

using Belikov.GenuineChannels;
using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.GenuineTcp;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Security;
using Belikov.GenuineChannels.Security.SSPI;
using Known;

namespace Client
{
	/// <summary>
	/// Summary description for Form1.
	/// </summary>
	public class Form : System.Windows.Forms.Form, IClientEventReceiver, IEventLogger
	{
		private System.Windows.Forms.TabControl tabControlConnection;
		private System.Windows.Forms.TabPage tabPageConnection;
		private System.Windows.Forms.TabPage tabPageSecurity;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.TextBox textBoxServerAddress;
		private System.Windows.Forms.NumericUpDown numericUpDownPort;
		private System.Windows.Forms.Button buttonDisconnect;
		private System.Windows.Forms.GroupBox groupBox1;
		private System.Windows.Forms.RadioButton radioButtonBasic;
		private System.Windows.Forms.RadioButton radioButtonKnownSymmetric;
		private System.Windows.Forms.TextBox textBoxSspiUserName;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.TextBox textBoxSspiDomain;
		private System.Windows.Forms.TextBox textBoxSspiPassword;
		private System.Windows.Forms.RadioButton radioButtonSelfEstablishedSymmetric;
		private System.Windows.Forms.CheckBox checkBoxEnableCompression;
		private System.Windows.Forms.GroupBox groupBoxSSPI;
		private System.Windows.Forms.Label label6;
		private System.Windows.Forms.Label label10;
		private System.Windows.Forms.TabPage tabPageLog;
		private System.Windows.Forms.RichTextBox richTextBoxLog;
		private System.Windows.Forms.Button buttonConnect;
		private System.Windows.Forms.Button buttonApplySecuritySession;
		private System.Windows.Forms.StatusBar statusBar;
		private System.Windows.Forms.MainMenu mainMenu;
		private System.Windows.Forms.MenuItem menuItem1;
		private System.Windows.Forms.MenuItem menuItem2;
		private System.Windows.Forms.MenuItem menuItem3;
		private System.Windows.Forms.MenuItem menuItem4;
		private System.Windows.Forms.StatusBarPanel statusBarPanelConnect;
		private System.Windows.Forms.StatusBarPanel statusBarPanelBytesSent;
		private System.Windows.Forms.StatusBarPanel statusBarPanelBytesReceived;
		private System.Windows.Forms.StatusBarPanel statusBarPanelCPS;
		private System.Windows.Forms.Label label5;
		private System.Windows.Forms.Label label8;
		private System.Windows.Forms.Panel panel1;
		private System.Windows.Forms.Panel panel2;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Panel panel4;
		private System.Windows.Forms.Label label7;
		private System.Windows.Forms.Timer timer;
		private System.Windows.Forms.CheckBox checkBoxShowReceivedEvents;
		private System.Windows.Forms.CheckBox checkBoxShowTraffic;
		private System.Windows.Forms.RadioButton radioButtonSspiEncryption;
		private System.Windows.Forms.RadioButton radioButtonSspiCheckIntegrity;
		private System.Windows.Forms.RadioButton radioButtonNoSspiFeatures;
		private System.Windows.Forms.RadioButton radioButtonSspiNtlm;
		private System.Windows.Forms.RadioButton radioButtonSspiKerberos;
		private System.Windows.Forms.RadioButton radioButtonSspiNegotiation;
		private System.Windows.Forms.Label label14;
		private System.Windows.Forms.TextBox textBoxTargetName;
		private System.Windows.Forms.Label label17;
		private System.Windows.Forms.TextBox textBoxFileName;
		private System.Windows.Forms.Button buttonEventsClearLogContent;
		private System.Windows.Forms.Label label9;
		private System.Windows.Forms.NumericUpDown numericUpDownCallTimeout;
		private System.ComponentModel.IContainer components;

		public Form()
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();

			//
			// TODO: Add any constructor code after InitializeComponent call
			//
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if (components != null) 
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			this.tabControlConnection = new System.Windows.Forms.TabControl();
			this.tabPageConnection = new System.Windows.Forms.TabPage();
			this.panel1 = new System.Windows.Forms.Panel();
			this.label8 = new System.Windows.Forms.Label();
			this.buttonDisconnect = new System.Windows.Forms.Button();
			this.numericUpDownPort = new System.Windows.Forms.NumericUpDown();
			this.textBoxServerAddress = new System.Windows.Forms.TextBox();
			this.buttonConnect = new System.Windows.Forms.Button();
			this.label3 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.tabPageSecurity = new System.Windows.Forms.TabPage();
			this.textBoxFileName = new System.Windows.Forms.TextBox();
			this.label17 = new System.Windows.Forms.Label();
			this.panel2 = new System.Windows.Forms.Panel();
			this.label1 = new System.Windows.Forms.Label();
			this.buttonApplySecuritySession = new System.Windows.Forms.Button();
			this.groupBoxSSPI = new System.Windows.Forms.GroupBox();
			this.textBoxTargetName = new System.Windows.Forms.TextBox();
			this.label14 = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			this.textBoxSspiDomain = new System.Windows.Forms.TextBox();
			this.textBoxSspiPassword = new System.Windows.Forms.TextBox();
			this.textBoxSspiUserName = new System.Windows.Forms.TextBox();
			this.radioButtonSspiEncryption = new System.Windows.Forms.RadioButton();
			this.radioButtonSspiCheckIntegrity = new System.Windows.Forms.RadioButton();
			this.radioButtonNoSspiFeatures = new System.Windows.Forms.RadioButton();
			this.label5 = new System.Windows.Forms.Label();
			this.label6 = new System.Windows.Forms.Label();
			this.label10 = new System.Windows.Forms.Label();
			this.groupBox1 = new System.Windows.Forms.GroupBox();
			this.numericUpDownCallTimeout = new System.Windows.Forms.NumericUpDown();
			this.label9 = new System.Windows.Forms.Label();
			this.radioButtonSspiNegotiation = new System.Windows.Forms.RadioButton();
			this.radioButtonSspiKerberos = new System.Windows.Forms.RadioButton();
			this.radioButtonSspiNtlm = new System.Windows.Forms.RadioButton();
			this.checkBoxEnableCompression = new System.Windows.Forms.CheckBox();
			this.radioButtonSelfEstablishedSymmetric = new System.Windows.Forms.RadioButton();
			this.radioButtonKnownSymmetric = new System.Windows.Forms.RadioButton();
			this.radioButtonBasic = new System.Windows.Forms.RadioButton();
			this.tabPageLog = new System.Windows.Forms.TabPage();
			this.buttonEventsClearLogContent = new System.Windows.Forms.Button();
			this.checkBoxShowTraffic = new System.Windows.Forms.CheckBox();
			this.checkBoxShowReceivedEvents = new System.Windows.Forms.CheckBox();
			this.panel4 = new System.Windows.Forms.Panel();
			this.label7 = new System.Windows.Forms.Label();
			this.richTextBoxLog = new System.Windows.Forms.RichTextBox();
			this.statusBar = new System.Windows.Forms.StatusBar();
			this.statusBarPanelConnect = new System.Windows.Forms.StatusBarPanel();
			this.statusBarPanelBytesSent = new System.Windows.Forms.StatusBarPanel();
			this.statusBarPanelBytesReceived = new System.Windows.Forms.StatusBarPanel();
			this.statusBarPanelCPS = new System.Windows.Forms.StatusBarPanel();
			this.mainMenu = new System.Windows.Forms.MainMenu();
			this.menuItem1 = new System.Windows.Forms.MenuItem();
			this.menuItem2 = new System.Windows.Forms.MenuItem();
			this.menuItem3 = new System.Windows.Forms.MenuItem();
			this.menuItem4 = new System.Windows.Forms.MenuItem();
			this.timer = new System.Windows.Forms.Timer(this.components);
			this.tabControlConnection.SuspendLayout();
			this.tabPageConnection.SuspendLayout();
			this.panel1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.numericUpDownPort)).BeginInit();
			this.tabPageSecurity.SuspendLayout();
			this.panel2.SuspendLayout();
			this.groupBoxSSPI.SuspendLayout();
			this.groupBox1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.numericUpDownCallTimeout)).BeginInit();
			this.tabPageLog.SuspendLayout();
			this.panel4.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.statusBarPanelConnect)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.statusBarPanelBytesSent)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.statusBarPanelBytesReceived)).BeginInit();
			((System.ComponentModel.ISupportInitialize)(this.statusBarPanelCPS)).BeginInit();
			this.SuspendLayout();
			// 
			// tabControlConnection
			// 
			this.tabControlConnection.Anchor = (((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
				| System.Windows.Forms.AnchorStyles.Left) 
				| System.Windows.Forms.AnchorStyles.Right);
			this.tabControlConnection.Controls.AddRange(new System.Windows.Forms.Control[] {
																							   this.tabPageConnection,
																							   this.tabPageSecurity,
																							   this.tabPageLog});
			this.tabControlConnection.Location = new System.Drawing.Point(8, 8);
			this.tabControlConnection.Name = "tabControlConnection";
			this.tabControlConnection.SelectedIndex = 0;
			this.tabControlConnection.Size = new System.Drawing.Size(696, 496);
			this.tabControlConnection.TabIndex = 0;
			// 
			// tabPageConnection
			// 
			this.tabPageConnection.Controls.AddRange(new System.Windows.Forms.Control[] {
																							this.panel1,
																							this.buttonDisconnect,
																							this.numericUpDownPort,
																							this.textBoxServerAddress,
																							this.buttonConnect,
																							this.label3,
																							this.label2});
			this.tabPageConnection.Location = new System.Drawing.Point(4, 22);
			this.tabPageConnection.Name = "tabPageConnection";
			this.tabPageConnection.Size = new System.Drawing.Size(688, 470);
			this.tabPageConnection.TabIndex = 0;
			this.tabPageConnection.Text = "Connection";
			// 
			// panel1
			// 
			this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
				| System.Windows.Forms.AnchorStyles.Right);
			this.panel1.BackColor = System.Drawing.SystemColors.ControlLight;
			this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel1.Controls.AddRange(new System.Windows.Forms.Control[] {
																				 this.label8});
			this.panel1.Location = new System.Drawing.Point(8, 8);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(672, 56);
			this.panel1.TabIndex = 7;
			// 
			// label8
			// 
			this.label8.Anchor = ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
				| System.Windows.Forms.AnchorStyles.Right);
			this.label8.BackColor = System.Drawing.SystemColors.ControlLight;
			this.label8.Font = new System.Drawing.Font("Arial", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((System.Byte)(0)));
			this.label8.Location = new System.Drawing.Point(8, 8);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(656, 40);
			this.label8.TabIndex = 6;
			this.label8.Text = "Enter server address (domain name or IP address) and port and click Connect to co" +
				"nnect to the server. Click Disconnect to close the established connection.";
			// 
			// buttonDisconnect
			// 
			this.buttonDisconnect.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.buttonDisconnect.Location = new System.Drawing.Point(88, 128);
			this.buttonDisconnect.Name = "buttonDisconnect";
			this.buttonDisconnect.TabIndex = 5;
			this.buttonDisconnect.Text = "&Disconnect";
			this.buttonDisconnect.Click += new System.EventHandler(this.buttonDisconnect_Click);
			// 
			// numericUpDownPort
			// 
			this.numericUpDownPort.Location = new System.Drawing.Point(112, 96);
			this.numericUpDownPort.Maximum = new System.Decimal(new int[] {
																			  65535,
																			  0,
																			  0,
																			  0});
			this.numericUpDownPort.Minimum = new System.Decimal(new int[] {
																			  1,
																			  0,
																			  0,
																			  0});
			this.numericUpDownPort.Name = "numericUpDownPort";
			this.numericUpDownPort.TabIndex = 3;
			this.numericUpDownPort.Value = new System.Decimal(new int[] {
																			8737,
																			0,
																			0,
																			0});
			// 
			// textBoxServerAddress
			// 
			this.textBoxServerAddress.Anchor = ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
				| System.Windows.Forms.AnchorStyles.Right);
			this.textBoxServerAddress.Location = new System.Drawing.Point(112, 72);
			this.textBoxServerAddress.Name = "textBoxServerAddress";
			this.textBoxServerAddress.Size = new System.Drawing.Size(568, 20);
			this.textBoxServerAddress.TabIndex = 1;
			this.textBoxServerAddress.Text = "gtcp://127.0.0.1";
			// 
			// buttonConnect
			// 
			this.buttonConnect.DialogResult = System.Windows.Forms.DialogResult.OK;
			this.buttonConnect.Location = new System.Drawing.Point(8, 128);
			this.buttonConnect.Name = "buttonConnect";
			this.buttonConnect.TabIndex = 4;
			this.buttonConnect.Text = "&Connect";
			this.buttonConnect.Click += new System.EventHandler(this.buttonConnect_Click);
			// 
			// label3
			// 
			this.label3.Location = new System.Drawing.Point(8, 96);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(100, 20);
			this.label3.TabIndex = 2;
			this.label3.Text = "&Port:";
			this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// label2
			// 
			this.label2.Location = new System.Drawing.Point(8, 72);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(100, 20);
			this.label2.TabIndex = 0;
			this.label2.Text = "Server &address:";
			this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// tabPageSecurity
			// 
			this.tabPageSecurity.Controls.AddRange(new System.Windows.Forms.Control[] {
																						  this.textBoxFileName,
																						  this.label17,
																						  this.panel2,
																						  this.buttonApplySecuritySession,
																						  this.groupBoxSSPI,
																						  this.groupBox1});
			this.tabPageSecurity.Location = new System.Drawing.Point(4, 22);
			this.tabPageSecurity.Name = "tabPageSecurity";
			this.tabPageSecurity.Size = new System.Drawing.Size(688, 470);
			this.tabPageSecurity.TabIndex = 1;
			this.tabPageSecurity.Text = "Encryption and Impersonation";
			// 
			// textBoxFileName
			// 
			this.textBoxFileName.Anchor = ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
				| System.Windows.Forms.AnchorStyles.Right);
			this.textBoxFileName.Location = new System.Drawing.Point(160, 416);
			this.textBoxFileName.Name = "textBoxFileName";
			this.textBoxFileName.Size = new System.Drawing.Size(512, 20);
			this.textBoxFileName.TabIndex = 4;
			this.textBoxFileName.Text = "c:\\tmp\\file1.txt";
			// 
			// label17
			// 
			this.label17.Location = new System.Drawing.Point(8, 416);
			this.label17.Name = "label17";
			this.label17.Size = new System.Drawing.Size(152, 23);
			this.label17.TabIndex = 3;
			this.label17.Text = "File name (at the server):";
			// 
			// panel2
			// 
			this.panel2.Anchor = ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
				| System.Windows.Forms.AnchorStyles.Right);
			this.panel2.BackColor = System.Drawing.SystemColors.ControlLight;
			this.panel2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel2.Controls.AddRange(new System.Windows.Forms.Control[] {
																				 this.label1});
			this.panel2.Location = new System.Drawing.Point(8, 8);
			this.panel2.Name = "panel2";
			this.panel2.Size = new System.Drawing.Size(672, 56);
			this.panel2.TabIndex = 0;
			// 
			// label1
			// 
			this.label1.Anchor = ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
				| System.Windows.Forms.AnchorStyles.Right);
			this.label1.BackColor = System.Drawing.SystemColors.ControlLight;
			this.label1.Font = new System.Drawing.Font("Arial", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((System.Byte)(0)));
			this.label1.Location = new System.Drawing.Point(8, 8);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(656, 40);
			this.label1.TabIndex = 0;
			this.label1.Text = "Choose Security Session and specify its parameters. Enter a file name being creat" +
				"ed at the server (NTFS partition is recommended for testing SSPI). Then click Cr" +
				"eate File.";
			// 
			// buttonApplySecuritySession
			// 
			this.buttonApplySecuritySession.DialogResult = System.Windows.Forms.DialogResult.OK;
			this.buttonApplySecuritySession.Location = new System.Drawing.Point(8, 440);
			this.buttonApplySecuritySession.Name = "buttonApplySecuritySession";
			this.buttonApplySecuritySession.Size = new System.Drawing.Size(144, 23);
			this.buttonApplySecuritySession.TabIndex = 5;
			this.buttonApplySecuritySession.Text = "&Create File";
			this.buttonApplySecuritySession.Click += new System.EventHandler(this.buttonApplySecuritySession_Click);
			// 
			// groupBoxSSPI
			// 
			this.groupBoxSSPI.Anchor = ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
				| System.Windows.Forms.AnchorStyles.Right);
			this.groupBoxSSPI.Controls.AddRange(new System.Windows.Forms.Control[] {
																					   this.textBoxTargetName,
																					   this.label14,
																					   this.label4,
																					   this.textBoxSspiDomain,
																					   this.textBoxSspiPassword,
																					   this.textBoxSspiUserName,
																					   this.radioButtonSspiEncryption,
																					   this.radioButtonSspiCheckIntegrity,
																					   this.radioButtonNoSspiFeatures,
																					   this.label5,
																					   this.label6,
																					   this.label10});
			this.groupBoxSSPI.Location = new System.Drawing.Point(8, 208);
			this.groupBoxSSPI.Name = "groupBoxSSPI";
			this.groupBoxSSPI.Size = new System.Drawing.Size(672, 200);
			this.groupBoxSSPI.TabIndex = 2;
			this.groupBoxSSPI.TabStop = false;
			this.groupBoxSSPI.Text = "SSPI";
			// 
			// textBoxTargetName
			// 
			this.textBoxTargetName.Anchor = ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
				| System.Windows.Forms.AnchorStyles.Right);
			this.textBoxTargetName.Location = new System.Drawing.Point(112, 88);
			this.textBoxTargetName.Name = "textBoxTargetName";
			this.textBoxTargetName.Size = new System.Drawing.Size(552, 20);
			this.textBoxTargetName.TabIndex = 7;
			this.textBoxTargetName.Text = "(Required only by Kerberos and Negotiation packages)";
			// 
			// label14
			// 
			this.label14.Location = new System.Drawing.Point(8, 88);
			this.label14.Name = "label14";
			this.label14.TabIndex = 6;
			this.label14.Text = "Target name:";
			// 
			// label4
			// 
			this.label4.Location = new System.Drawing.Point(8, 112);
			this.label4.Name = "label4";
			this.label4.TabIndex = 8;
			this.label4.Text = "Content control:";
			// 
			// textBoxSspiDomain
			// 
			this.textBoxSspiDomain.Anchor = ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
				| System.Windows.Forms.AnchorStyles.Right);
			this.textBoxSspiDomain.Location = new System.Drawing.Point(112, 64);
			this.textBoxSspiDomain.Name = "textBoxSspiDomain";
			this.textBoxSspiDomain.Size = new System.Drawing.Size(552, 20);
			this.textBoxSspiDomain.TabIndex = 5;
			this.textBoxSspiDomain.Text = ".";
			// 
			// textBoxSspiPassword
			// 
			this.textBoxSspiPassword.Anchor = ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
				| System.Windows.Forms.AnchorStyles.Right);
			this.textBoxSspiPassword.Location = new System.Drawing.Point(112, 40);
			this.textBoxSspiPassword.Name = "textBoxSspiPassword";
			this.textBoxSspiPassword.PasswordChar = '*';
			this.textBoxSspiPassword.Size = new System.Drawing.Size(552, 20);
			this.textBoxSspiPassword.TabIndex = 3;
			this.textBoxSspiPassword.Text = "";
			// 
			// textBoxSspiUserName
			// 
			this.textBoxSspiUserName.Anchor = ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
				| System.Windows.Forms.AnchorStyles.Right);
			this.textBoxSspiUserName.Location = new System.Drawing.Point(112, 16);
			this.textBoxSspiUserName.Name = "textBoxSspiUserName";
			this.textBoxSspiUserName.Size = new System.Drawing.Size(552, 20);
			this.textBoxSspiUserName.TabIndex = 1;
			this.textBoxSspiUserName.Text = ". (Enter a dot as the first symbol to use process\'s security context.)";
			// 
			// radioButtonSspiEncryption
			// 
			this.radioButtonSspiEncryption.Location = new System.Drawing.Point(112, 160);
			this.radioButtonSspiEncryption.Name = "radioButtonSspiEncryption";
			this.radioButtonSspiEncryption.Size = new System.Drawing.Size(240, 32);
			this.radioButtonSspiEncryption.TabIndex = 11;
			this.radioButtonSspiEncryption.Text = "Check content integrity and encrypt content (Windows 2003 or XP is required)";
			// 
			// radioButtonSspiCheckIntegrity
			// 
			this.radioButtonSspiCheckIntegrity.Location = new System.Drawing.Point(112, 136);
			this.radioButtonSspiCheckIntegrity.Name = "radioButtonSspiCheckIntegrity";
			this.radioButtonSspiCheckIntegrity.Size = new System.Drawing.Size(168, 24);
			this.radioButtonSspiCheckIntegrity.TabIndex = 10;
			this.radioButtonSspiCheckIntegrity.Text = "Check content integrity";
			// 
			// radioButtonNoSspiFeatures
			// 
			this.radioButtonNoSspiFeatures.Checked = true;
			this.radioButtonNoSspiFeatures.Location = new System.Drawing.Point(112, 112);
			this.radioButtonNoSspiFeatures.Name = "radioButtonNoSspiFeatures";
			this.radioButtonNoSspiFeatures.TabIndex = 9;
			this.radioButtonNoSspiFeatures.TabStop = true;
			this.radioButtonNoSspiFeatures.Text = "None";
			// 
			// label5
			// 
			this.label5.Location = new System.Drawing.Point(8, 16);
			this.label5.Name = "label5";
			this.label5.TabIndex = 0;
			this.label5.Text = "&User name:";
			// 
			// label6
			// 
			this.label6.Location = new System.Drawing.Point(8, 40);
			this.label6.Name = "label6";
			this.label6.TabIndex = 2;
			this.label6.Text = "&Password:";
			// 
			// label10
			// 
			this.label10.Location = new System.Drawing.Point(8, 64);
			this.label10.Name = "label10";
			this.label10.TabIndex = 4;
			this.label10.Text = "&Domain:";
			// 
			// groupBox1
			// 
			this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
				| System.Windows.Forms.AnchorStyles.Right);
			this.groupBox1.Controls.AddRange(new System.Windows.Forms.Control[] {
																					this.numericUpDownCallTimeout,
																					this.label9,
																					this.radioButtonSspiNegotiation,
																					this.radioButtonSspiKerberos,
																					this.radioButtonSspiNtlm,
																					this.checkBoxEnableCompression,
																					this.radioButtonSelfEstablishedSymmetric,
																					this.radioButtonKnownSymmetric,
																					this.radioButtonBasic});
			this.groupBox1.Location = new System.Drawing.Point(8, 72);
			this.groupBox1.Name = "groupBox1";
			this.groupBox1.Size = new System.Drawing.Size(672, 128);
			this.groupBox1.TabIndex = 1;
			this.groupBox1.TabStop = false;
			this.groupBox1.Text = "Current Security Session";
			// 
			// numericUpDownCallTimeout
			// 
			this.numericUpDownCallTimeout.Location = new System.Drawing.Point(264, 96);
			this.numericUpDownCallTimeout.Maximum = new System.Decimal(new int[] {
																					 1000,
																					 0,
																					 0,
																					 0});
			this.numericUpDownCallTimeout.Minimum = new System.Decimal(new int[] {
																					 29,
																					 0,
																					 0,
																					 0});
			this.numericUpDownCallTimeout.Name = "numericUpDownCallTimeout";
			this.numericUpDownCallTimeout.Size = new System.Drawing.Size(144, 20);
			this.numericUpDownCallTimeout.TabIndex = 9;
			this.numericUpDownCallTimeout.Value = new System.Decimal(new int[] {
																				   240,
																				   0,
																				   0,
																				   0});
			// 
			// label9
			// 
			this.label9.Location = new System.Drawing.Point(152, 96);
			this.label9.Name = "label9";
			this.label9.Size = new System.Drawing.Size(112, 20);
			this.label9.TabIndex = 8;
			this.label9.Text = "Timeout value (sec):";
			this.label9.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// radioButtonSspiNegotiation
			// 
			this.radioButtonSspiNegotiation.Location = new System.Drawing.Point(408, 64);
			this.radioButtonSspiNegotiation.Name = "radioButtonSspiNegotiation";
			this.radioButtonSspiNegotiation.Size = new System.Drawing.Size(232, 32);
			this.radioButtonSspiNegotiation.TabIndex = 5;
			this.radioButtonSspiNegotiation.Text = "SSPI Negotiation";
			// 
			// radioButtonSspiKerberos
			// 
			this.radioButtonSspiKerberos.Location = new System.Drawing.Point(408, 40);
			this.radioButtonSspiKerberos.Name = "radioButtonSspiKerberos";
			this.radioButtonSspiKerberos.Size = new System.Drawing.Size(232, 24);
			this.radioButtonSspiKerberos.TabIndex = 4;
			this.radioButtonSspiKerberos.Text = "SSPI Kerberos";
			// 
			// radioButtonSspiNtlm
			// 
			this.radioButtonSspiNtlm.Location = new System.Drawing.Point(408, 16);
			this.radioButtonSspiNtlm.Name = "radioButtonSspiNtlm";
			this.radioButtonSspiNtlm.Size = new System.Drawing.Size(232, 24);
			this.radioButtonSspiNtlm.TabIndex = 3;
			this.radioButtonSspiNtlm.Text = "SSPI NTLM";
			// 
			// checkBoxEnableCompression
			// 
			this.checkBoxEnableCompression.Checked = true;
			this.checkBoxEnableCompression.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBoxEnableCompression.Location = new System.Drawing.Point(16, 96);
			this.checkBoxEnableCompression.Name = "checkBoxEnableCompression";
			this.checkBoxEnableCompression.Size = new System.Drawing.Size(136, 20);
			this.checkBoxEnableCompression.TabIndex = 6;
			this.checkBoxEnableCompression.Text = "Enable compression";
			// 
			// radioButtonSelfEstablishedSymmetric
			// 
			this.radioButtonSelfEstablishedSymmetric.Anchor = ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
				| System.Windows.Forms.AnchorStyles.Right);
			this.radioButtonSelfEstablishedSymmetric.Location = new System.Drawing.Point(16, 64);
			this.radioButtonSelfEstablishedSymmetric.Name = "radioButtonSelfEstablishedSymmetric";
			this.radioButtonSelfEstablishedSymmetric.Size = new System.Drawing.Size(392, 32);
			this.radioButtonSelfEstablishedSymmetric.TabIndex = 2;
			this.radioButtonSelfEstablishedSymmetric.Text = "Symmetric Key Encryption. Key is generated and sent using Asymmetric Key Encrypti" +
				"on once when Security Session is established.";
			// 
			// radioButtonKnownSymmetric
			// 
			this.radioButtonKnownSymmetric.Location = new System.Drawing.Point(16, 40);
			this.radioButtonKnownSymmetric.Name = "radioButtonKnownSymmetric";
			this.radioButtonKnownSymmetric.Size = new System.Drawing.Size(392, 24);
			this.radioButtonKnownSymmetric.TabIndex = 1;
			this.radioButtonKnownSymmetric.Text = "Symmetric Key Encryption with known 256-bit Rijndael key.";
			// 
			// radioButtonBasic
			// 
			this.radioButtonBasic.Checked = true;
			this.radioButtonBasic.Location = new System.Drawing.Point(16, 16);
			this.radioButtonBasic.Name = "radioButtonBasic";
			this.radioButtonBasic.TabIndex = 0;
			this.radioButtonBasic.TabStop = true;
			this.radioButtonBasic.Text = "No Encryption.";
			// 
			// tabPageLog
			// 
			this.tabPageLog.Controls.AddRange(new System.Windows.Forms.Control[] {
																					 this.buttonEventsClearLogContent,
																					 this.checkBoxShowTraffic,
																					 this.checkBoxShowReceivedEvents,
																					 this.panel4,
																					 this.richTextBoxLog});
			this.tabPageLog.Location = new System.Drawing.Point(4, 22);
			this.tabPageLog.Name = "tabPageLog";
			this.tabPageLog.Size = new System.Drawing.Size(688, 470);
			this.tabPageLog.TabIndex = 3;
			this.tabPageLog.Text = "Events and traffic";
			// 
			// buttonEventsClearLogContent
			// 
			this.buttonEventsClearLogContent.Location = new System.Drawing.Point(592, 72);
			this.buttonEventsClearLogContent.Name = "buttonEventsClearLogContent";
			this.buttonEventsClearLogContent.Size = new System.Drawing.Size(88, 23);
			this.buttonEventsClearLogContent.TabIndex = 3;
			this.buttonEventsClearLogContent.Text = "Clear content";
			this.buttonEventsClearLogContent.Click += new System.EventHandler(this.buttonEventsClearLogContent_Click);
			// 
			// checkBoxShowTraffic
			// 
			this.checkBoxShowTraffic.Location = new System.Drawing.Point(168, 72);
			this.checkBoxShowTraffic.Name = "checkBoxShowTraffic";
			this.checkBoxShowTraffic.Size = new System.Drawing.Size(152, 24);
			this.checkBoxShowTraffic.TabIndex = 2;
			this.checkBoxShowTraffic.Text = "Show traffic";
			// 
			// checkBoxShowReceivedEvents
			// 
			this.checkBoxShowReceivedEvents.Location = new System.Drawing.Point(8, 72);
			this.checkBoxShowReceivedEvents.Name = "checkBoxShowReceivedEvents";
			this.checkBoxShowReceivedEvents.Size = new System.Drawing.Size(152, 24);
			this.checkBoxShowReceivedEvents.TabIndex = 1;
			this.checkBoxShowReceivedEvents.Text = "Show received events";
			// 
			// panel4
			// 
			this.panel4.Anchor = ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
				| System.Windows.Forms.AnchorStyles.Right);
			this.panel4.BackColor = System.Drawing.SystemColors.ControlLight;
			this.panel4.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel4.Controls.AddRange(new System.Windows.Forms.Control[] {
																				 this.label7});
			this.panel4.Location = new System.Drawing.Point(8, 8);
			this.panel4.Name = "panel4";
			this.panel4.Size = new System.Drawing.Size(672, 56);
			this.panel4.TabIndex = 0;
			// 
			// label7
			// 
			this.label7.Anchor = ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
				| System.Windows.Forms.AnchorStyles.Right);
			this.label7.BackColor = System.Drawing.SystemColors.ControlLight;
			this.label7.Font = new System.Drawing.Font("Arial", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((System.Byte)(0)));
			this.label7.Location = new System.Drawing.Point(8, 8);
			this.label7.Name = "label7";
			this.label7.Size = new System.Drawing.Size(656, 40);
			this.label7.TabIndex = 0;
			this.label7.Text = "This tab page shows you traffic and all caught events and exceptions.";
			// 
			// richTextBoxLog
			// 
			this.richTextBoxLog.Anchor = (((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
				| System.Windows.Forms.AnchorStyles.Left) 
				| System.Windows.Forms.AnchorStyles.Right);
			this.richTextBoxLog.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((System.Byte)(204)));
			this.richTextBoxLog.Location = new System.Drawing.Point(8, 96);
			this.richTextBoxLog.Name = "richTextBoxLog";
			this.richTextBoxLog.ReadOnly = true;
			this.richTextBoxLog.Size = new System.Drawing.Size(672, 365);
			this.richTextBoxLog.TabIndex = 4;
			this.richTextBoxLog.Text = "";
			this.richTextBoxLog.WordWrap = false;
			// 
			// statusBar
			// 
			this.statusBar.Location = new System.Drawing.Point(0, 507);
			this.statusBar.Name = "statusBar";
			this.statusBar.Panels.AddRange(new System.Windows.Forms.StatusBarPanel[] {
																						 this.statusBarPanelConnect,
																						 this.statusBarPanelBytesSent,
																						 this.statusBarPanelBytesReceived,
																						 this.statusBarPanelCPS});
			this.statusBar.ShowPanels = true;
			this.statusBar.Size = new System.Drawing.Size(712, 22);
			this.statusBar.TabIndex = 1;
			// 
			// statusBarPanelConnect
			// 
			this.statusBarPanelConnect.AutoSize = System.Windows.Forms.StatusBarPanelAutoSize.Spring;
			this.statusBarPanelConnect.MinWidth = 150;
			this.statusBarPanelConnect.Text = "Connection status:";
			this.statusBarPanelConnect.Width = 296;
			// 
			// statusBarPanelBytesSent
			// 
			this.statusBarPanelBytesSent.MinWidth = 150;
			this.statusBarPanelBytesSent.Text = "Bytes sent:";
			this.statusBarPanelBytesSent.Width = 150;
			// 
			// statusBarPanelBytesReceived
			// 
			this.statusBarPanelBytesReceived.MinWidth = 150;
			this.statusBarPanelBytesReceived.Text = "Bytes received:";
			this.statusBarPanelBytesReceived.Width = 150;
			// 
			// statusBarPanelCPS
			// 
			this.statusBarPanelCPS.Text = "CPS:";
			// 
			// mainMenu
			// 
			this.mainMenu.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
																					 this.menuItem1,
																					 this.menuItem3});
			// 
			// menuItem1
			// 
			this.menuItem1.Index = 0;
			this.menuItem1.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
																					  this.menuItem2});
			this.menuItem1.Text = "&File";
			// 
			// menuItem2
			// 
			this.menuItem2.Index = 0;
			this.menuItem2.Text = "E&xit";
			this.menuItem2.Click += new System.EventHandler(this.menuItem2_Click);
			// 
			// menuItem3
			// 
			this.menuItem3.Index = 1;
			this.menuItem3.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
																					  this.menuItem4});
			this.menuItem3.Text = "&Help";
			// 
			// menuItem4
			// 
			this.menuItem4.Index = 0;
			this.menuItem4.Text = "&About";
			this.menuItem4.Click += new System.EventHandler(this.menuItem4_Click);
			// 
			// timer
			// 
			this.timer.Interval = 500;
			this.timer.Tick += new System.EventHandler(this.timer_Tick);
			// 
			// Form
			// 
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
			this.ClientSize = new System.Drawing.Size(712, 529);
			this.Controls.AddRange(new System.Windows.Forms.Control[] {
																		  this.statusBar,
																		  this.tabControlConnection});
			this.Menu = this.mainMenu;
			this.Name = "Form";
			this.Text = "Security Session Demo Application";
			this.Load += new System.EventHandler(this.Form_Load);
			this.tabControlConnection.ResumeLayout(false);
			this.tabPageConnection.ResumeLayout(false);
			this.panel1.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.numericUpDownPort)).EndInit();
			this.tabPageSecurity.ResumeLayout(false);
			this.panel2.ResumeLayout(false);
			this.groupBoxSSPI.ResumeLayout(false);
			this.groupBox1.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.numericUpDownCallTimeout)).EndInit();
			this.tabPageLog.ResumeLayout(false);
			this.panel4.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.statusBarPanelConnect)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.statusBarPanelBytesSent)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.statusBarPanelBytesReceived)).EndInit();
			((System.ComponentModel.ISupportInitialize)(this.statusBarPanelCPS)).EndInit();
			this.ResumeLayout(false);

		}
		#endregion

		#region GUI events

		private void menuItem4_Click(object sender, System.EventArgs e)
		{
			About about = new About();
			about.ShowDialog(this);
		}

		private void menuItem2_Click(object sender, System.EventArgs e)
		{
			this.Close();
		}


		private void Form_Load(object sender, System.EventArgs e)
		{
			this.ButtonConnectEnabled = true;
			GenuineGlobalEventProvider.GenuineChannelsGlobalEvent += new GenuineChannelsGlobalEventHandler(GenuineChannelsEventHandler);			
			this.timer.Start();
		}

		private IEstablishSecuritySession _iEstablishSecuritySession;
		private ICreateFile _iCreateFile;

		/// <summary>
		/// Connects to the server and registers our event listener.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void buttonConnect_Click(object sender, System.EventArgs e)
		{
			try
			{
				// set up a channel
				if (this.GenuineTcpChannel == null)
				{
					Hashtable properties = new Hashtable();
					this.GenuineTcpChannel = new GenuineTcpChannel(properties, null, null);
					ChannelServices.RegisterChannel(this.GenuineTcpChannel);

					this.GenuineTcpChannel.ITransportContext.IEventLogger = this;
				}

				// build up a local transparent proxy to the event provider
				string serverAddress = this.textBoxServerAddress.Text + ":" + this.numericUpDownPort.Value;
				IServerEventProvider iServerEventProvider = (IServerEventProvider) Activator.GetObject(typeof(IServerEventProvider),
					serverAddress + "/ServerEventProvider.rem");

				// connects to the server and subscribe to its event
				iServerEventProvider.Subscribe(this);
				this.UpdateLog("Client's listener has been successfully subscribed to the server event.");

				// build up other proxies
				this._iEstablishSecuritySession = (IEstablishSecuritySession) Activator.GetObject(typeof(IEstablishSecuritySession),
					serverAddress + "/EstablishSecuritySession.rem");
				this._iCreateFile = (ICreateFile) Activator.GetObject(typeof(ICreateFile),
					serverAddress + "/CreateFile.rem");
			}
			catch(Exception ex)
			{
				if (ex is OperationException)
				{
					// an exception thrown by GenuineChannels
					OperationException operationException = (OperationException) ex;
					MessageBox.Show(this, operationException.OperationErrorMessage.UserFriendlyMessage, operationException.OperationErrorMessage.ErrorIdentifier, MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
				else
					MessageBox.Show(this, ex.Message, "Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		/// <summary>
		/// Closes a connection to the server.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void buttonDisconnect_Click(object sender, System.EventArgs e)
		{
			try
			{
				this.GenuineTcpChannel.StopListening(null);
				this.GenuineTcpChannel.ITransportContext.ConnectionManager.ReleaseConnections(null, GenuineConnectionType.All, GenuineExceptions.Get_Receive_ConnectionClosed("Connection has been terminated manually."));
				this.ButtonConnectEnabled = true;
			}
			catch(Exception ex)
			{
				string str = ex.Message;
			}
		}

		private long _previousCps = 0;
		private long _previousBytesSum = 0;
		private void timer_Tick(object sender, System.EventArgs e)
		{
			if (this.GenuineTcpChannel == null)
				return ;

			// take values
			long bytesReceived = this.GenuineTcpChannel.ITransportContext.ConnectionManager.BytesReceived;
			long bytesSent = this.GenuineTcpChannel.ITransportContext.ConnectionManager.BytesSent;

			this.statusBarPanelBytesReceived.Text = string.Format("Bytes received: {0}", bytesReceived);
			this.statusBarPanelBytesSent.Text = string.Format("Bytes sent: {0}", bytesSent);

			// slow shifting
			long currentCps = bytesSent + bytesReceived - this._previousBytesSum;
			long cps = (2 * this._previousCps + currentCps) / 3;

			// show cps
			this.statusBarPanelCPS.Text = string.Format("CPS: {0}", cps);

			// advance counters
			this._previousCps = cps;
			this._previousBytesSum = bytesReceived + bytesSent;
		}

		private int currentSecuritySessionNumber = 0;

		/// <summary>
		/// Applies Security Session changes.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void buttonApplySecuritySession_Click(object sender, System.EventArgs e)
		{
			try
			{
				// create the name of the Security Session
				string securitySessionName = "/SSDemo/builtSS/" + this.currentSecuritySessionNumber++;

				// add Timeout property
				SecuritySessionParameters parameters = new SecuritySessionParameters(
					// the name of Security Session
					securitySessionName, 
					// the use of compression
					this.checkBoxEnableCompression.Checked ? SecuritySessionAttributes.EnableCompression : SecuritySessionAttributes.None,
					// the timeout of the invocation
					TimeSpan.FromSeconds((double) this.numericUpDownCallTimeout.Value));

				IKeyProvider iKeyProvider = new KeyProvider_Basic();
				SecuritySessionType securitySessionType = SecuritySessionType.Basic;
				NetworkCredential networkCredential = null;
				SspiFeatureFlags sspiFeatureFlags = SspiFeatureFlags.Impersonation;
				SupportedSspiPackages package = SupportedSspiPackages.NTLM;

				if (this.radioButtonKnownSymmetric.Checked)
				{
					SymmetricAlgorithm symmetricAlgorithm = SymmetricAlgorithm.Create();

					// read the key
					Stream stream = typeof(IEstablishSecuritySession).Assembly.GetManifestResourceStream("Known.written_key");
					byte[] key = new byte[32];
					stream.Read(key, 0, key.Length);
					stream.Close();

					// initialize the key
					symmetricAlgorithm.Key = key;
					symmetricAlgorithm.Mode = CipherMode.ECB;

					iKeyProvider = new KeyProvider_KnownSymmetric(symmetricAlgorithm);
					securitySessionType = SecuritySessionType.KnownSymmetric;
				}

				if (this.radioButtonSelfEstablishedSymmetric.Checked)
				{
					iKeyProvider = new KeyProvider_SelfEstablishingSymmetric();
					securitySessionType = SecuritySessionType.SelfEstablishingSymmetric;
				}

				if (this.radioButtonSspiNtlm.Checked || this.radioButtonSspiNegotiation.Checked || this.radioButtonSspiKerberos.Checked)
				{
					// look for requested features
					if (this.radioButtonSspiCheckIntegrity.Checked)
						sspiFeatureFlags |= SspiFeatureFlags.Signing;
					if (this.radioButtonSspiEncryption.Checked)
						sspiFeatureFlags |= SspiFeatureFlags.Encryption;

					// scan credential
					if (this.textBoxSspiUserName.Text.Length > 0 && ! this.textBoxSspiUserName.Text.StartsWith("."))
						networkCredential = new NetworkCredential(this.textBoxSspiUserName.Text, this.textBoxSspiPassword.Text, this.textBoxSspiDomain.Text);

					// the package
					if (this.radioButtonSspiNegotiation.Checked)
						package = SupportedSspiPackages.Negotiate;
					if (this.radioButtonSspiKerberos.Checked)
						package = SupportedSspiPackages.Kerberos;

					// and create the security session
					iKeyProvider = new KeyProvider_SspiClient(sspiFeatureFlags, package, networkCredential, this.textBoxTargetName.Text);

					securitySessionType = SecuritySessionType.Sspi;
				}

				// create security sessions at both client and server
				this.GenuineTcpChannel.ITransportContext.IKeyStore.SetKey(securitySessionName, iKeyProvider);
				this._iEstablishSecuritySession.CreateSecuritySession(securitySessionName, securitySessionType, sspiFeatureFlags, package);

				// and create a file using this security session
				using(new SecurityContextKeeper(parameters))
				{
					StringBuilder builder = new StringBuilder();

					builder.AppendFormat("Current time: {0}.\r\n", DateTime.Now);
					builder.AppendFormat("Security session type: {0}.\r\n", ((object) iKeyProvider).GetType().FullName);
					if (this.checkBoxEnableCompression.Checked)
						builder.Append("Compression is enabled.\r\n");
					else
						builder.Append("Compression is disabled.\r\n");
					if (networkCredential == null)
						builder.AppendFormat("Default credential.\r\n");
					else
						builder.AppendFormat("Credential with the user name: {0} \r\n", networkCredential.UserName);

					this._iCreateFile.Create(this.textBoxFileName.Text, builder.ToString());
				}

				MessageBox.Show(this, "File has been succcessfully created.", "Success.", MessageBoxButtons.OK, MessageBoxIcon.Information);
			}
			catch(Exception ex)
			{
				if (ex is OperationException)
				{
					// an exception thrown by GenuineChannels
					OperationException operationException = (OperationException) ex;
					MessageBox.Show(this, operationException.OperationErrorMessage.UserFriendlyMessage, operationException.OperationErrorMessage.ErrorIdentifier, MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
				else
					MessageBox.Show(this, ex.Message, "Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void buttonEventsClearLogContent_Click(object sender, System.EventArgs e)
		{
			this.richTextBoxLog.Clear();
		}

		#endregion

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main() 
		{
			Application.Run(new Form());
		}

		/// <summary>
		/// GenuineTcpClientChannel.
		/// </summary>
		public GenuineTcpChannel GenuineTcpChannel;

		/// <summary>
		/// Sets values of connect and disconnect buttons.
		/// </summary>
		public bool ButtonConnectEnabled
		{
			set
			{
				this.buttonConnect.Enabled = value;
				this.buttonDisconnect.Enabled = ! value;
				this.buttonApplySecuritySession.Enabled = ! value;
			}
		}

		/// <summary>
		/// Processes events thrown by Genuine Channels.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		public void GenuineChannelsEventHandler(object sender, GenuineEventArgs e)
		{
			string remoteHostInfo = e.HostInformation == null ? "<not specified>" : e.HostInformation.ToString();
			switch(e.EventType)
			{
				case GenuineEventType.GeneralConnectionEstablished:
					this.UpdateLog("Connection is established to {0}.", remoteHostInfo);
					this.ButtonConnectEnabled = false;
					this.statusBarPanelConnect.Text = "Connection status: CONNECTED";
					break;

				case GenuineEventType.GeneralConnectionClosed:
					if (e.SourceException != null)
						this.UpdateLog("Connection to the host {0} is closed due to the exception: {1}.", remoteHostInfo, e.SourceException);
					else
						this.UpdateLog("Connection to the host {0} is closed.", remoteHostInfo, e.SourceException);

					this.ButtonConnectEnabled = true;
					if (e.SourceException is OperationException && 
						((OperationException) e.SourceException).OperationErrorMessage.ErrorIdentifier.IndexOf("ServerHasBeenRestarted") > -1 )
						this.UpdateLog("WARNING: The server has been restarted.");
					this.statusBarPanelConnect.Text = "Connection status: DISCONNECTED";
					break;

				case GenuineEventType.GeneralConnectionReestablishing:
					if (e.SourceException != null)
						this.UpdateLog("Connection to the host {0} has been broken due to the exception: {1} but will probably be restored.", remoteHostInfo, e.SourceException);
					else
						this.UpdateLog("Connection to the host {0} has been broken but will probably be restored.", remoteHostInfo);
					this.statusBarPanelConnect.Text = "Connection status: RESTORING";
					break;

				default:
					if (e.SourceException != null)
						this.UpdateLog("Event: {0}. Remote host: {1}. Exception: {2}.", e.EventType, 
							e.HostInformation == null ? "<not specified>" : e.HostInformation.ToString(), 
							e.SourceException.Message);
					else
						this.UpdateLog("Event: {0}. Remote host: {1}.", e.EventType, 
							e.HostInformation == null ? "<not specified>" : e.HostInformation.ToString());
					break;
			}
		}

		private delegate void RichTextBoxAppendTextDelegate(string str);

		/// <summary>
		/// Appends a log with a formattable string.
		/// </summary>
		/// <param name="format">String.</param>
		/// <param name="args">String arguments.</param>
		public void UpdateLog(string format, params object[] args)
		{
			string str = string.Format(format, args);
			str = string.Format("\r\n------- {0} \r\n{1}", DateTime.Now, str);
			RichTextBoxAppendTextDelegate richTextBoxAppendTextDelegate = new RichTextBoxAppendTextDelegate(this.richTextBoxLog.AppendText);
			this.richTextBoxLog.Invoke(richTextBoxAppendTextDelegate, new object[] { str });
		}

		/// <summary>
		/// Appends a log with a string.
		/// </summary>
		/// <param name="str"></param>
		private void AddStringToLog(object str)
		{
			RichTextBoxAppendTextDelegate richTextBoxAppendTextDelegate = new RichTextBoxAppendTextDelegate(this.richTextBoxLog.AppendText);
			this.richTextBoxLog.Invoke(richTextBoxAppendTextDelegate, new object[] { str.ToString() });
		}

		/// <summary>
		/// Is invocated by server at all clients subscribed to events.
		/// </summary>
		/// <param name="message">A message.</param>
		/// <returns>Null.</returns>
		public object ReceiveEvent(string message)
		{
			if (this.checkBoxShowReceivedEvents.Checked)
				this.UpdateLog("An event has been received from the server.");
			return null;
		}

		/// <summary>
		/// Puts message to log.
		/// </summary>
		/// <param name="logMessageCategory">Message category to filter out.</param>
		/// <param name="ex">Exception to show error and stack trace from.</param>
		/// <param name="author">Error author. For example, Type.FullName.</param>
		/// <param name="buffer">Binary info related to the event.</param>
		/// <param name="message">Error message.</param>
		/// <param name="parameters">Message text parameters for processing in string.Format function.</param>
		public void Log(LogMessageCategory logMessageCategory, Exception ex, string author, byte[] buffer, string message, params object[] parameters)
		{
			if (this.checkBoxShowTraffic.Checked && (logMessageCategory & LogMessageCategory.Traffic) != 0)
			{
				StringBuilder builder = new StringBuilder();
				builder.AppendFormat(message, parameters);

				for ( int i = 0; i < (buffer.Length + 15) / 16; i++)
				{
					builder.Append("\r\n");

					// create hex representation
					for ( int o = 0; o < Math.Min(16, buffer.Length - i * 16); o++ )
						builder.AppendFormat("{0,2:X2} ", buffer[i*16 + o]);
					builder.Append(" | ");
					for ( int o = 0; o < Math.Min(16, buffer.Length - i * 16); o++ )
					{
						char symbol = ' ';
						if (buffer[i*16 + o] >= 32 && buffer[i*16 + o] < 128)
							symbol = (char) (short) buffer[i*16 + o];
						builder.Append(symbol);
					}
				}

				string str = string.Format("\r\n------- {0} \r\n{1}", DateTime.Now, builder.ToString());

				// I can't do it right here because otherwise we'll lock Genuine Channels processor thread.
				// Genuine Channels never provides its thread except for the logging.
				ThreadPool.QueueUserWorkItem(new WaitCallback(this.AddStringToLog), str);
			}
		}

		/// <summary>
		/// Should return true if logger implements saving byte[] buffer to the log.
		/// It can influence on performance because transport layer will have to convert Stream
		/// being sent to byte[] array.
		/// </summary>
		public bool AcceptBinaryData 
		{ 
			get
			{
				return true;
			}
		}

	}
}
