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

namespace Server
{
	/// <summary>
	/// Chat server implements server that configures Genuine Server TCP Channel and implements
	/// chat server behavior.
	/// </summary>
	class ChatServer : MarshalByRefObject, IChatServer
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

				ChatServer.GlobalRoom = new ChatRoom();

				// bind the server
				RemotingServices.Marshal(new ChatServer(), "ChatServer.rem");
				RemotingServices.Marshal(new ReturnItselfImplementation(), "ReturnItself.rem");

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

			if (e.EventType == GenuineEventType.GeneralConnectionClosed)
			{
				// remove client session
				string nickname = e.HostInformation["Nickname"] as string;
				if (nickname != null)
					Console.WriteLine("Client \"{0}\" has been disconnected.", nickname);
			}
		}

		/// <summary>
		/// This example was designed to have the only chat room.
		/// </summary>
		public static ChatRoom GlobalRoom;

		/// <summary>
		/// Performs log in to the chat room.
		/// </summary>
		/// <param name="iMessageReceiver">Chat message receiver.</param>
		/// <param name="nickname">Nickname.</param>
		/// <returns>Chat room interface.</returns>
		public IChatRoom EnterToChatRoom(IMessageReceiver iMessageReceiver, string nickname)
		{
			GlobalRoom.AttachClient(iMessageReceiver, nickname);
			GenuineUtility.CurrentSession["Nickname"] = nickname;
			return GlobalRoom;
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
