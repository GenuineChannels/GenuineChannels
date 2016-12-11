using System;
using System.Collections;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Lifetime;
using System.Runtime.Remoting.Messaging;

using KnownObjects;

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

	}
}
