using System;
using System.Configuration;
using System.Runtime.Remoting;
using System.Threading;

using KnownObjects;

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
				RemotingConfiguration.Configure("Client.exe.config");

				Console.WriteLine(".NET Remoting has been configured from Client.exe.config file.");

				for ( int i = 0; i < 3; i++)
				{
					Thread thread = new Thread(new ThreadStart(InvokeOperation));
					thread.IsBackground = true;
					thread.Start();
				}

				Console.WriteLine("Press ENTER to exit.");
				Console.ReadLine();
			}
			catch(Exception ex)
			{
				Console.WriteLine("Exception: {0}. Stack trace: {1}.", ex.Message, ex.StackTrace);
			}
		}

		public static void InvokeOperation()
		{
			Console.WriteLine("Invoking a long-duration operation...");
			IOperationProvider iOperationProvider = (IOperationProvider) Activator.GetObject(typeof(IOperationProvider),
				ConfigurationSettings.AppSettings["RemoteHostUri"] + "/OperationProvider.rem");
			Console.WriteLine(iOperationProvider.Do());
		}

	}
}
