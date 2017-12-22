using System;
using System.Runtime.Remoting.Messaging;

namespace KnownObjects
{
	/// <summary>
	/// Specifies a callback called when a message is received.
	/// </summary>
	public interface IChatClient
	{
		/// <summary>
		/// Is called by the server when a message is accepted.
		/// </summary>
		/// <param name="message">A message.</param>
		object ReceiveMessage(string message);
	}

	/// <summary>
	/// ChatRoom provides common methods for chatting.
	/// </summary>
	public interface IChatRoom
	{
		/// <summary>
		/// Sends the message to all clients.
		/// </summary>
		/// <param name="message">Message to send.</param>
		void SendMessage(string message);

		/// <summary>
		/// Attaches a client.
		/// </summary>
		/// <param name="iChatClient">Receiver that will receive chat messages.</param>
		void AttachClient(IChatClient iChatClient);
	}
}
