/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;

namespace Belikov.GenuineChannels.Receiving
{
	/// <summary>
	/// Implements a message registrator which remembers several last messages.
	/// </summary>
	public class MessageRegistratorWithLimitedQuantity : IMessageRegistrator
	{
		/// <summary>
		/// Constructs an instance of the MessageRegistratorWithLimitedQuantity class.
		/// </summary>
		/// <param name="capacity">The number of elements that this instance is capable of storing.</param>
		public MessageRegistratorWithLimitedQuantity(int capacity)
		{
			this._registeredMessages = new int[capacity];
		}

		private int[] _registeredMessages;
		private int _head = 0;

		/// <summary>
		/// Checks whether this message was processed before.
		/// </summary>
		/// <param name="uri">The uri of the remote host.</param>
		/// <param name="messageId">The message identifier.</param>
		/// <param name="replyId">The identifier of the response.</param>
		/// <returns>True if message was processed before.</returns>
		public bool WasRegistered(string uri, int messageId, int replyId)
		{
			lock (this._registeredMessages)
			{
				for ( int i = 0; i < this._registeredMessages.Length; i++ )
					if (this._registeredMessages[i] == messageId)
						return true;

				this._registeredMessages[this._head++] = messageId;
				this._head %= this._registeredMessages.Length;
			}

			return false;
		}
	}
}
