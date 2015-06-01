/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

namespace Belikov.GenuineChannels.Messaging
{
	/// <summary>
	/// Manages a FIFO list of messages with the specified size.
	/// Is used by GTCP channel for sending synchronous messages with guaranteed delivery.
	/// </summary>
	internal class MessageList
	{
		/// <summary>
		/// Constructs an instance of the MessageList class.
		/// </summary>
		/// <param name="size">The size of the list.</param>
		public MessageList(int size)
		{
			this._messages = new Message[size];
		}

		private Message[] _messages;
		private int _head = 0;

		/// <summary>
		/// Puts the message into the message list.
		/// </summary>
		/// <param name="message">The message.</param>
		public void PutMessage(Message message)
		{
			lock (this)
			{
				// release the current available message
				Message previousMessage = this._messages[this._head] as Message;
				if (previousMessage != null)
					previousMessage.Dispose();

				this._messages [ this._head++ ] = message;
				if (this._head >= this._messages.Length)
					this._head = 0;
			}
		}

		/// <summary>
		/// Transfers registered messages to the message container.
		/// </summary>
		/// <param name="messageContainer">The Message Containter.</param>
		public void MoveMessages(MessageContainer messageContainer)
		{
			lock (this)
			{
				for ( int i = 0; i < this._messages.Length; i++ )
				{
					Message message = this._messages[i] as Message;

					// 2.5.1 ignores messages that have been answered
					if (message != null && ! message.HasBeenAsnwered)
					{
						message.SerializedContent.Position = 0;
						messageContainer.AddMessage(message, false);
						this._messages[i] = null;
					}
				}
			}
		}

		/// <summary>
		/// Releases all messages.
		/// </summary>
		public void ReleaseAllMessages()
		{
			lock (this)
			{
				for ( int i = 0; i < this._messages.Length; i++ )
				{
					Message message = this._messages[i] as Message;
					if (message != null)
					{
						message.Dispose();
						this._messages[i] = null;
					}
				}
			}
		}

	}
}
