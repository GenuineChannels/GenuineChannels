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
	/// Chat server implements server that configures Genuine Server TCP Channel and implements
	/// chat server behavior.
	/// </summary>
	class ChatServer : MarshalByRefObject, IChatServer
	{
		/// <summary>
		/// This example was designed to have the only chat room.
		/// </summary>
		public static ChatRoom GlobalRoom = new ChatRoom();

		/// <summary>
		/// Logs into the chat room.
		/// </summary>
		/// <param name="nickname">Nickname.</param>
		/// <returns>Chat room interface.</returns>
		public IChatRoom EnterToChatRoom(string nickname)
		{
			GlobalRoom.AttachClient(nickname);
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
