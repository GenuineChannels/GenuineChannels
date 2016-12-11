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
	class ChatClient : MarshalByRefObject, IMessageReceiver
	{
		/// <summary>
		/// The only instance.
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
		/// A proxy to server business object.
		/// </summary>
		public static IChatServer IChatServer;

		/// <summary>
		/// To provide thread-safe access to ChatClient.IChatServer member.
		/// </summary>
		public static object IChatServerLock = new object();

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
			GenuineGlobalEventProvider.GenuineChannelsGlobalEvent += new GenuineChannelsGlobalEventHandler(GenuineChannelsEventHandler);
			RemotingConfiguration.Configure("Client.exe.config");
			//GlobalLoggerContainer.Logger = new BinaryLog(@"c:\tmp\client.log");

			Console.WriteLine(".NET Remoting has been configured from Client.exe.config file.");

			Console.WriteLine("Please enter a nickname:");
			ChatClient.Nickname = Console.ReadLine();

			// bind client's receiver
			RemotingServices.Marshal(ChatClient.Instance, "MessageReceiver.rem");

			for(;;)
			{
				try
				{
					// subscribe to the chat event
					lock(ChatClient.IChatServerLock)
					{
						ChatClient.IChatServer = (IChatServer) Activator.GetObject(typeof(IChatRoom),
							ConfigurationSettings.AppSettings["RemoteHostUri"] + "/ChatServer.rem");
						ChatClient.IChatRoom = ChatClient.IChatServer.EnterToChatRoom(ChatClient.Nickname);
					}

					for(;;)
					{
						Console.WriteLine("Enter a message to send or an empty string to exit.");

						string str = Console.ReadLine();
						if (str.Length <= 0)
							return ;

						ChatClient.IChatRoom.SendMessage(str);
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
				// server has been restarted so it does not know that we have been subscribed to
				// messages and ours nickname
				lock (ChatClient.IChatServerLock)
				{
					ChatClient.IChatServer = (IChatServer) Activator.GetObject(typeof(IChatRoom),
						ConfigurationSettings.AppSettings["RemoteHostUri"] + "/ChatServer.rem");
					ChatClient.IChatRoom = ChatClient.IChatServer.EnterToChatRoom(ChatClient.Nickname);
				}
			}
		}

		/// <summary>
		/// Message receiver.
		/// It receives messages async and writes them separately from the main thread.
		/// But it does not matter for console application.
		/// </summary>
		/// <param name="message">The message.</param>
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
