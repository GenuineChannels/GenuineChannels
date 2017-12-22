using System;
using System.Configuration;
using System.Runtime.Remoting;
using System.Threading;

using KnownObjects;
using Belikov.GenuineChannels;
using Belikov.GenuineChannels.BroadcastEngine;
using Belikov.GenuineChannels.DotNetRemotingLayer;

namespace Client
{
	/// <summary>
	/// ChatClient demonstrates a simple client application.
	/// </summary>
	class ChatClient : MarshalByRefObject, IMessageReceiver
	{
		/// <summary>
		/// Singleton.
		/// </summary>
		public static ChatClient Instance = new ChatClient();

		/// <summary>
		/// Nickname.
		/// </summary>
		public static string Nickname;

		/// <summary>
		/// Chat room.
		/// </summary>
		public static IChatRoom IChatRoom;

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
			Console.WriteLine("Sleep for 3 seconds.");
			Thread.Sleep(TimeSpan.FromSeconds(3));

			Console.WriteLine("Configuring Remoting environment...");
			System.Configuration.ConfigurationSettings.GetConfig("DNS");
			GenuineGlobalEventProvider.GenuineChannelsGlobalEvent += new GenuineChannelsGlobalEventHandler(GenuineChannelsEventHandler);
			RemotingConfiguration.Configure("Client.exe.config");

			Console.WriteLine(".NET Remoting has been configured from Client.exe.config file.");

			Console.WriteLine("Please enter nickname of this client:");
			ChatClient.Nickname = Console.ReadLine();

			for(;;)
			{
				try
				{
					IChatServer iChatServer = (IChatServer) Activator.GetObject(typeof(IChatRoom),
						ConfigurationSettings.AppSettings["RemoteHostUri"] + "/ChatServer.rem");
					ChatClient.IChatRoom = iChatServer.EnterToChatRoom(ChatClient.Instance, ChatClient.Nickname);

					for(;;)
					{
						Console.WriteLine("Enter a message to send or an empty string to exit.");

						string str = Console.ReadLine();
						if (str.Length <= 0)
							return ;

						ChatClient.IChatRoom.SendMessage(str);
						Console.WriteLine("Message \"{0}\" has been sent.", str);
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

		public static void GenuineChannelsEventHandler(object sender, GenuineEventArgs e)
		{
			if (e.SourceException == null)
				Console.WriteLine("Global event: {0}\r\nUrl: {1}", e.EventType, 
					e.HostInformation == null ? "<not specified>" : e.HostInformation.ToString());
			else
				Console.WriteLine("Global event: {0}\r\nUrl: {1}\r\nException: {2}", e.EventType, 
					e.HostInformation == null ? "<not specified>" : e.HostInformation.ToString(), 
					e.SourceException);

			if (e.EventType == GenuineEventType.GeneralServerRestartDetected)
			{
				// server has been restarted so we have to register our listener again
				IChatServer iChatServer = (IChatServer) Activator.GetObject(typeof(IChatRoom),
					ConfigurationSettings.AppSettings["RemoteHostUri"] + "/ChatServer.rem");
				ChatClient.IChatRoom = iChatServer.EnterToChatRoom(ChatClient.Instance, ChatClient.Nickname);
			}
		}

		/// <summary>
		/// Receives messages.
		/// </summary>
		/// <param name="message">The message.</param>
		/// <param name="nickname">Person who sent the message.</param>
		public object ReceiveMessage(string message, string nickname)
		{
			Console.WriteLine("Message \"{0}\" from \"{1}\".", message, nickname);
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
