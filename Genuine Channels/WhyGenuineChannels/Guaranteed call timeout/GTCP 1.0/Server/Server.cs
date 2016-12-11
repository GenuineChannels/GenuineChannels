using System;
using System.Collections;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Lifetime;
using System.Runtime.Remoting.Messaging;

using KnownObjects;
using Belikov.GenuineChannels;
using Belikov.GenuineChannels.BroadcastEngine;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Logbook;

namespace Server
{
	/// <summary>
	/// Server.
	/// </summary>
	class Server
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
			try
			{
				// setup remoting
				System.Configuration.ConfigurationSettings.GetConfig("DNS");
				GenuineGlobalEventProvider.GenuineChannelsGlobalEvent += new GenuineChannelsGlobalEventHandler(GenuineChannelsEventHandler);
				RemotingConfiguration.Configure("Server.exe.config");
				//GlobalLoggerContainer.Logger = new BinaryLog(@"c:\tmp\server.log");

				// bind the business object
				RemotingServices.Marshal(new OperationProvider(), "OperationProvider.rem");

				Console.WriteLine("Server has been started. Press enter to exit.");
				Console.ReadLine();
			}
			catch(Exception ex)
			{
				Console.WriteLine("Exception: {0}. Stack trace: {1}.", ex.Message, ex.StackTrace);
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
		}

	}
}
