using System;
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Web;
using System.Web.SessionState;

using Belikov.GenuineChannels;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.GenuineHttp;

namespace Server 
{
	/// <summary>
	/// Summary description for Global.
	/// </summary>
	public class Global : System.Web.HttpApplication
	{
		public Global()
		{
			InitializeComponent();
		}	

		/// <summary>
		/// Log that is shown on the default.aspx page.
		/// </summary>
		public static TextWriter GlobalConsole = new StringWriter();

		protected void Application_Start(Object sender, EventArgs e)
		{
			Console.SetOut(GlobalConsole);

			try
			{
				// setup remoting
				System.Configuration.ConfigurationSettings.GetConfig("DNS");
				GenuineGlobalEventProvider.GenuineChannelsGlobalEvent += new GenuineChannelsGlobalEventHandler(GenuineChannelsEventHandler);
				//GlobalLoggerContainer.Logger = new BinaryLog(@"c:\tmp\server.log");

				Hashtable properties = new Hashtable();
				properties["MaxContentSize"] = "5000000";
				properties["PersistentConnectionSendPingAfterInactivity"] = "15000";
				properties["MaxQueuedItems"] = "100000";
				properties["MaxTotalSize"] = "100000000";
				properties["Compression"] = "false";
				properties["MaxTimeSpanToReconnect"] = "5000";

				GenuineHttpServerChannel channel = new GenuineHttpServerChannel(properties, null, null);
				ChannelServices.RegisterChannel(channel);

				// bind the service
				RemotingServices.Marshal(new ChatServer(), "ChatServer.rem");
			}
			catch(Exception ex)
			{
				Console.WriteLine("Exception: {0} Stack trace: {1}.", ex.Message, ex.StackTrace);
			}
		}

		/// <summary>
		/// Catches Genuine Channels events and removes client session when
		/// user disconnects.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		public static void GenuineChannelsEventHandler(object sender, GenuineEventArgs e)
		{
			if (e.SourceException == null)
				Console.WriteLine("Global event: {0}\r\nUrl: {1}", e.EventType, 
					e.HostInformation == null ? "<not specified>" : e.HostInformation.ToString());
			else
				Console.WriteLine("Global event: {0}\r\nUrl: {1}\r\nException: {2}", e.EventType, 
					e.HostInformation == null ? "<not specified>" : e.HostInformation.ToString(), 
					e.SourceException);

			if (e.EventType == GenuineEventType.GeneralConnectionClosed)
			{
				string nickname = e.HostInformation["Nickname"] as string;
				if (nickname != null)
					Console.WriteLine("Client \"{0}\" has been disconnected.", nickname);
			}
		}

		protected void Session_Start(Object sender, EventArgs e)
		{

		}

		protected void Application_BeginRequest(Object sender, EventArgs e)
		{

		}

		protected void Application_EndRequest(Object sender, EventArgs e)
		{

		}

		protected void Application_AuthenticateRequest(Object sender, EventArgs e)
		{

		}

		protected void Application_Error(Object sender, EventArgs e)
		{

		}

		protected void Session_End(Object sender, EventArgs e)
		{

		}

		protected void Application_End(Object sender, EventArgs e)
		{

		}
			
		#region Web Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{    
		}
		#endregion
	}
}

