using System;
using System.Configuration;
using System.Runtime.Remoting;
using System.Threading;

using KnownObjects;
using Belikov.GenuineChannels;
using Belikov.GenuineChannels.BroadcastEngine;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Logbook;

namespace Client
{
	/// <summary>
	/// ChatClient demostrates simple client application.
	/// </summary>
	class Client
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
			try
			{
				Console.WriteLine("Sleep for 3 seconds.");
				Thread.Sleep(TimeSpan.FromSeconds(3));

				Console.WriteLine("Configuring Remoting environment...");
				System.Configuration.ConfigurationSettings.GetConfig("DNS");
				GenuineGlobalEventProvider.GenuineChannelsGlobalEvent += new GenuineChannelsGlobalEventHandler(GenuineChannelsEventHandler);
				RemotingConfiguration.Configure("Client.exe.config");
				//GlobalLoggerContainer.Logger = new BinaryLog(@"c:\tmp\client.log");

				Console.WriteLine(".NET Remoting has been configured from Client.exe.config file.");

				Console.WriteLine("Invoking a long-duration operation...");
				IOperationProvider iOperationProvider = (IOperationProvider) Activator.GetObject(typeof(IOperationProvider),
					ConfigurationSettings.AppSettings["RemoteHostUri"] + "/OperationProvider.rem");
				Console.WriteLine(iOperationProvider.Do());
			}
			catch(Exception ex)
			{
				Console.WriteLine("Exception: {0}. Stack trace: {1}.", ex.Message, ex.StackTrace);
			}

			Console.WriteLine("Press ENTER to exit.");
			Console.ReadLine();
		}

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
