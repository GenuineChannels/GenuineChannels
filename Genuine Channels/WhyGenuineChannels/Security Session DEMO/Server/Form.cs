using System;
using System.Data;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Windows.Forms;

using Belikov.GenuineChannels;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.GenuineTcp;
using Known;

namespace Server
{
	/// <summary>
	/// Summary description for Form1.
	/// </summary>
	public class Form : System.Windows.Forms.Form
	{
		private System.Windows.Forms.StatusBar statusBar;
		private System.Windows.Forms.StatusBarPanel statusBarPanelConnect;
		private System.Windows.Forms.MainMenu mainMenu;
		private System.Windows.Forms.MenuItem menuItem1;
		private System.Windows.Forms.MenuItem menuItem3;
		private System.Windows.Forms.Panel panel;
		private System.Windows.Forms.Label label8;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Button buttonStart;
		private System.Windows.Forms.Button buttonStop;
		private System.Windows.Forms.RichTextBox richTextBoxLog;
		private System.Windows.Forms.MenuItem menuItemExit;
		private System.Windows.Forms.MenuItem menuItemAbout;
		private System.Windows.Forms.NumericUpDown numericUpDownPort;
		private System.Windows.Forms.Timer timer;
		private System.Windows.Forms.CheckBox checkBoxFireTheEvent;
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
			this.statusBar = new System.Windows.Forms.StatusBar();
			this.statusBarPanelConnect = new System.Windows.Forms.StatusBarPanel();
			this.mainMenu = new System.Windows.Forms.MainMenu();
			this.menuItem1 = new System.Windows.Forms.MenuItem();
			this.menuItemExit = new System.Windows.Forms.MenuItem();
			this.menuItem3 = new System.Windows.Forms.MenuItem();
			this.menuItemAbout = new System.Windows.Forms.MenuItem();
			this.panel = new System.Windows.Forms.Panel();
			this.label8 = new System.Windows.Forms.Label();
			this.label1 = new System.Windows.Forms.Label();
			this.numericUpDownPort = new System.Windows.Forms.NumericUpDown();
			this.buttonStart = new System.Windows.Forms.Button();
			this.buttonStop = new System.Windows.Forms.Button();
			this.richTextBoxLog = new System.Windows.Forms.RichTextBox();
			this.timer = new System.Windows.Forms.Timer(this.components);
			this.checkBoxFireTheEvent = new System.Windows.Forms.CheckBox();
			((System.ComponentModel.ISupportInitialize)(this.statusBarPanelConnect)).BeginInit();
			this.panel.SuspendLayout();
			((System.ComponentModel.ISupportInitialize)(this.numericUpDownPort)).BeginInit();
			this.SuspendLayout();
			// 
			// statusBar
			// 
			this.statusBar.Location = new System.Drawing.Point(0, 296);
			this.statusBar.Name = "statusBar";
			this.statusBar.Panels.AddRange(new System.Windows.Forms.StatusBarPanel[] {
																						 this.statusBarPanelConnect});
			this.statusBar.ShowPanels = true;
			this.statusBar.Size = new System.Drawing.Size(528, 22);
			this.statusBar.TabIndex = 7;
			// 
			// statusBarPanelConnect
			// 
			this.statusBarPanelConnect.AutoSize = System.Windows.Forms.StatusBarPanelAutoSize.Spring;
			this.statusBarPanelConnect.MinWidth = 150;
			this.statusBarPanelConnect.Text = "Status:";
			this.statusBarPanelConnect.Width = 512;
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
																					  this.menuItemExit});
			this.menuItem1.Text = "&File";
			// 
			// menuItemExit
			// 
			this.menuItemExit.Index = 0;
			this.menuItemExit.Text = "E&xit";
			this.menuItemExit.Click += new System.EventHandler(this.menuItemExit_Click);
			// 
			// menuItem3
			// 
			this.menuItem3.Index = 1;
			this.menuItem3.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
																					  this.menuItemAbout});
			this.menuItem3.Text = "&Help";
			// 
			// menuItemAbout
			// 
			this.menuItemAbout.Index = 0;
			this.menuItemAbout.Text = "&About";
			this.menuItemAbout.Click += new System.EventHandler(this.menuItemAbout_Click);
			// 
			// panel
			// 
			this.panel.Anchor = ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
				| System.Windows.Forms.AnchorStyles.Right);
			this.panel.BackColor = System.Drawing.SystemColors.ControlLight;
			this.panel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panel.Controls.AddRange(new System.Windows.Forms.Control[] {
																				this.label8});
			this.panel.Location = new System.Drawing.Point(8, 8);
			this.panel.Name = "panel";
			this.panel.Size = new System.Drawing.Size(512, 56);
			this.panel.TabIndex = 0;
			// 
			// label8
			// 
			this.label8.Anchor = ((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
				| System.Windows.Forms.AnchorStyles.Right);
			this.label8.BackColor = System.Drawing.SystemColors.ControlLight;
			this.label8.Font = new System.Drawing.Font("Arial", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((System.Byte)(0)));
			this.label8.Location = new System.Drawing.Point(8, 8);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(496, 40);
			this.label8.TabIndex = 0;
			this.label8.Text = "Enter the server port and press the Start button to start accepting clients. Pres" +
				"s the Stop button to stop accepting clients.";
			// 
			// label1
			// 
			this.label1.Location = new System.Drawing.Point(8, 72);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(100, 20);
			this.label1.TabIndex = 1;
			this.label1.Text = "Port:";
			this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
			// 
			// numericUpDownPort
			// 
			this.numericUpDownPort.Location = new System.Drawing.Point(112, 72);
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
			this.numericUpDownPort.TabIndex = 2;
			this.numericUpDownPort.Value = new System.Decimal(new int[] {
																			8737,
																			0,
																			0,
																			0});
			// 
			// buttonStart
			// 
			this.buttonStart.Location = new System.Drawing.Point(8, 96);
			this.buttonStart.Name = "buttonStart";
			this.buttonStart.TabIndex = 3;
			this.buttonStart.Text = "&Start";
			this.buttonStart.Click += new System.EventHandler(this.buttonStart_Click);
			// 
			// buttonStop
			// 
			this.buttonStop.Location = new System.Drawing.Point(88, 96);
			this.buttonStop.Name = "buttonStop";
			this.buttonStop.TabIndex = 4;
			this.buttonStop.Text = "&Stop";
			this.buttonStop.Click += new System.EventHandler(this.buttonStop_Click);
			// 
			// richTextBoxLog
			// 
			this.richTextBoxLog.Anchor = (((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
				| System.Windows.Forms.AnchorStyles.Left) 
				| System.Windows.Forms.AnchorStyles.Right);
			this.richTextBoxLog.Location = new System.Drawing.Point(8, 128);
			this.richTextBoxLog.Name = "richTextBoxLog";
			this.richTextBoxLog.ReadOnly = true;
			this.richTextBoxLog.Size = new System.Drawing.Size(512, 160);
			this.richTextBoxLog.TabIndex = 6;
			this.richTextBoxLog.Text = "";
			// 
			// timer
			// 
			this.timer.Interval = 333;
			this.timer.Tick += new System.EventHandler(this.timer_Tick);
			// 
			// checkBoxFireTheEvent
			// 
			this.checkBoxFireTheEvent.Checked = true;
			this.checkBoxFireTheEvent.CheckState = System.Windows.Forms.CheckState.Checked;
			this.checkBoxFireTheEvent.Location = new System.Drawing.Point(280, 96);
			this.checkBoxFireTheEvent.Name = "checkBoxFireTheEvent";
			this.checkBoxFireTheEvent.Size = new System.Drawing.Size(216, 24);
			this.checkBoxFireTheEvent.TabIndex = 5;
			this.checkBoxFireTheEvent.Text = "Fire the event three times per second";
			this.checkBoxFireTheEvent.CheckedChanged += new System.EventHandler(this.checkBoxFireTheEvent_CheckedChanged);
			// 
			// Form
			// 
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
			this.ClientSize = new System.Drawing.Size(528, 318);
			this.Controls.AddRange(new System.Windows.Forms.Control[] {
																		  this.checkBoxFireTheEvent,
																		  this.richTextBoxLog,
																		  this.statusBar,
																		  this.buttonStart,
																		  this.buttonStop,
																		  this.label1,
																		  this.numericUpDownPort,
																		  this.panel});
			this.Menu = this.mainMenu;
			this.Name = "Form";
			this.Text = "Security Session Demo Server";
			this.Load += new System.EventHandler(this.Form_Load);
			((System.ComponentModel.ISupportInitialize)(this.statusBarPanelConnect)).EndInit();
			this.panel.ResumeLayout(false);
			((System.ComponentModel.ISupportInitialize)(this.numericUpDownPort)).EndInit();
			this.ResumeLayout(false);

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


		#region GUI events

		private void menuItemExit_Click(object sender, System.EventArgs e)
		{
			this.Close();
		}

		private void menuItemAbout_Click(object sender, System.EventArgs e)
		{
			About about = new About();
			about.ShowDialog(this);
		}


		private void Form_Load(object sender, System.EventArgs e)
		{
			this.EstablishSecuritySession = new EstablishSecuritySession();
			this.ServerEventProvider = new ServerEventProvider(this);
			this.CreateFile = new CreateFile();

			RemotingServices.Marshal(this.ServerEventProvider, "ServerEventProvider.rem");
			RemotingServices.Marshal(this.EstablishSecuritySession, "EstablishSecuritySession.rem");
			RemotingServices.Marshal(this.CreateFile, "CreateFile.rem");

			GenuineGlobalEventProvider.GenuineChannelsGlobalEvent += new GenuineChannelsGlobalEventHandler(this.GenuineChannelsEventHandler);

			this.buttonStartEnabled = true;
			this.buttonStart_Click(sender, e);

			this.timer.Start();
		}

		private void buttonStart_Click(object sender, System.EventArgs e)
		{
			try
			{
				if (this.GenuineTcpChannel == null)
				{
					Hashtable properties = new Hashtable();
					properties["port"] = this.numericUpDownPort.Value.ToString();
					properties["port"] = this.numericUpDownPort.Value.ToString();
					this.GenuineTcpChannel = new GenuineTcpChannel(properties, null, null);
					ChannelServices.RegisterChannel(this.GenuineTcpChannel);
				}
				else
				{
					this.GenuineTcpChannel["port"] = this.numericUpDownPort.Value.ToString();
					this.GenuineTcpChannel.StartListening(null);
				}

				this.UpdateLog("Server has been successfully started.");
				this.buttonStartEnabled = false;
			}
			catch(Exception ex)
			{
				if (ex is OperationException)
				{
					OperationException operationException = (OperationException) ex;
					MessageBox.Show(this, operationException.OperationErrorMessage.UserFriendlyMessage, operationException.OperationErrorMessage.ErrorIdentifier, MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
				else
					MessageBox.Show(this, ex.Message, "Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
		}

		private void buttonStop_Click(object sender, System.EventArgs e)
		{
			try
			{
				this.GenuineTcpChannel.StopListening(null);
			}
			catch(Exception)
			{
			}

			this.buttonStartEnabled = true;
		}

		private void timer_Tick(object sender, System.EventArgs e)
		{
			this.ServerEventProvider.FireEvent();
		}

		private void checkBoxFireTheEvent_CheckedChanged(object sender, System.EventArgs e)
		{
			this.IsEventFired = this.checkBoxFireTheEvent.Checked;
		}

		#endregion

		/// <summary>
		/// Sets values of connect and disconnect buttons.
		/// </summary>
		public bool buttonStartEnabled
		{
			set
			{
				this.buttonStart.Enabled = value;
				this.buttonStop.Enabled = ! value;
				this.statusBarPanelConnect.Text = "Status: " + (!value ? "Accepting clients." : "Stopped.");
			}
		}

		public bool IsEventFired = true;

		public ServerEventProvider ServerEventProvider;
		public EstablishSecuritySession EstablishSecuritySession;
		public CreateFile CreateFile;


		private GenuineTcpChannel GenuineTcpChannel;

		/// <summary>
		/// Processes events thrown by Genuine Channels.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		public void GenuineChannelsEventHandler(object sender, GenuineEventArgs e)
		{
			string remoteHostInfo = e.HostInformation == null ? "<not specified>" : e.HostInformation.ToString();

			switch (e.EventType)
			{
				case GenuineEventType.GeneralConnectionEstablished:
					this.UpdateLog("Connection is established by {0}.", remoteHostInfo);
					break;

				case GenuineEventType.GeneralConnectionClosed:
					if (e.SourceException != null)
						this.UpdateLog("Connection to the host {0} is closed due to the exception: {1}.", remoteHostInfo, e.SourceException);
					else
						this.UpdateLog("Connection to the host {0} is closed.", remoteHostInfo, e.SourceException);
					break;

				case GenuineEventType.GeneralConnectionReestablishing:
					if (e.SourceException != null)
						this.UpdateLog("Connection to the host {0} has been broken due to the exception: {1} but will probably be restored.", remoteHostInfo, e.SourceException);
					else
						this.UpdateLog("Connection to the host {0} has been broken but will probably be restored.", remoteHostInfo);
					break;
			}
		}

		private delegate void RichTextBoxAppendTextDelegate(string str);

		/// <summary>
		/// Appends a log.
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


	}
}
