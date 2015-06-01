using System;
using System.Runtime.Remoting.Messaging;

namespace KnownObjects
{
	/// <summary>
	/// Describes a callback called when a message is received.
	/// </summary>
	public interface IMessageReceiver
	{
		/// <summary>
		/// Is called by the server when a message is accepted.
		/// </summary>
		/// <param name="message">A message.</param>
		/// <param name="nickname">Nickname of the client who sent the message.</param>
		object ReceiveMessage(string message, string nickname);
	}

	/// <summary>
	/// Server chat room factory.
	/// </summary>
	public interface IChatServer
	{
		/// <summary>
		/// Performs log in to the chat room.
		/// </summary>
		/// <param name="nickname">Nickname.</param>
		/// <returns>Chat room interface.</returns>
		IChatRoom EnterToChatRoom(string nickname);
	}

	/// <summary>
	/// ChatRoom provides methods for the chatting.
	/// </summary>
	public interface IChatRoom
	{
		/// <summary>
		/// Sends the message to all clients.
		/// </summary>
		/// <param name="message">Message being sent.</param>
		void SendMessage(string message);
	}
}
