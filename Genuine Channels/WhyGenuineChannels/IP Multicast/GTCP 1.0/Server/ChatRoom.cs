using System;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Messaging;

using KnownObjects;
using Belikov.GenuineChannels;
using Belikov.GenuineChannels.BroadcastEngine;

namespace Server
{
	/// <summary>
	/// Represents a chat room.
	/// </summary>
	public class ChatRoom : MarshalByRefObject, IChatRoom
	{
		/// <summary>
		/// Constructs ChatRoom instance.
		/// </summary>
		public ChatRoom()
		{
			//*** Send the event via IP multicast to the specific court
			IBroadcastSenderProvider iBroadcastSenderProvider = ChannelServices.GetChannel("BroadcastSender") as IBroadcastSenderProvider;
			this._dispatcher.Add(iBroadcastSenderProvider.GetBroadcastSender("/Chat/GlobalRoom"));

			// bind server's methods
			this._dispatcher.BroadcastCallFinishedHandler += new BroadcastCallFinishedHandler(this.BroadcastCallFinishedHandler);
			this._dispatcher.CallIsAsync = true;
		}

		/// <summary>
		/// Chat members.
		/// </summary>
		private Dispatcher _dispatcher = new Dispatcher(typeof(IMessageReceiver));

		/// <summary>
		/// Attaches the client.
		/// </summary>
		/// <param name="iMessageReceiver">Message receiver.</param>
		/// <param name="nickname">Nickname.</param>
		public void AttachClient(IMessageReceiver iMessageReceiver, string nickname)
		{
			if (iMessageReceiver == null)
				throw new ArgumentNullException("iMessageReceiver");

			this._dispatcher.Add((MarshalByRefObject) iMessageReceiver);

			GenuineUtility.CurrentSession["Nickname"] = nickname;
		}

		/// <summary>
		/// Sends the message to all clients.
		/// </summary>
		/// <param name="message">Message to send.</param>
		/// <returns>Number of clients having received this message.</returns>
		public void SendMessage(string message)
		{
			// fetch the nickname
			string nickname = GenuineUtility.CurrentSession["Nickname"] as string;
			Console.WriteLine("Message \"{0}\" will be sent to all clients from {1}.", message, nickname);

			IMessageReceiver iMessageReceiver = (IMessageReceiver) this._dispatcher.TransparentProxy;
			iMessageReceiver.ReceiveMessage(message, nickname);
		}

		/// <summary>
		/// Called by the dispatcher when all calls are performed.
		/// Does nothing.
		/// </summary>
		/// <param name="dispatcher">Source dipatcher.</param>
		/// <param name="message">Source message.</param>
		/// <param name="resultCollector">Call results.</param>
		public void BroadcastCallFinishedHandler(Dispatcher dispatcher, IMessage message, ResultCollector resultCollector)
		{
		}

	}
}
