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
	class ChatClient : MarshalByRefObject, IChatClient
	{
		/// <summary>
		/// We always subscribe the same listener to prevent from receiving the same 
		/// call again.
		/// </summary>
		public static ChatClient Instance = new ChatClient();

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
			// wait for the server
			Console.WriteLine("Sleep for 3 seconds.");
			Thread.Sleep(TimeSpan.FromSeconds(3));

			// setup .NET Remoting
			Console.WriteLine("Configuring Remoting environment...");
			System.Configuration.ConfigurationSettings.GetConfig("DNS");
			RemotingConfiguration.Configure("Client.exe.config");

			Console.WriteLine(".NET Remoting has been configured from Client.exe.config file.");

			for(;;)
			{
				try
				{
					// subscribe to the chat event
					IChatRoom iChatRoom = (IChatRoom) Activator.GetObject(typeof(IChatRoom), 
						ConfigurationSettings.AppSettings["RemoteHostUri"] + "/ChatRoom.rem");
					iChatRoom.AttachClient(ChatClient.Instance);

					for(;;)
					{
						Console.WriteLine("Enter a message to send or an empty string to exit.");

						// read a string
						string str = Console.ReadLine();
						if (str.Length <= 0)
							return ;

						// and send it to all other clients
						iChatRoom.SendMessage(str);
						Console.WriteLine("Message \"{0}\" has been sent to all clients.", str);
					}
				}
				catch(Exception ex)
				{
					Console.WriteLine("Exception: {0}. Stack trace: {1}.", ex.Message, ex.StackTrace);
				}

				Console.WriteLine("Next attempt to connect to the server will be in 3 seconds.");
				Thread.Sleep(3000);
			}
		}

		/// <summary>
		/// Message receiver.
		/// It receives messages async and writes them separately from the main thread.
		/// But it does not matter for console application.
		/// </summary>
		/// <param name="message">The message.</param>
        public object ReceiveMessage(string message)
		{
			Console.WriteLine("Message \"{0}\" has been received from the server.", message);
			return null;
		}

		/// <summary>
		/// This is to insure that when created as a Singleton, the first instance never dies,
		/// regardless of the expired time.
		/// </summary>
		/// <returns></returns>
		public override object InitializeLifetimeService()
		{
			return null;
		}
	}
}
