/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

namespace Belikov.GenuineChannels.Receiving
{
	/// <summary>
	/// Provides a method to keep track of already processed messages.
	/// </summary>
	public interface IMessageRegistrator
	{
		/// <summary>
		/// Checks whether this message was processed before.
		/// </summary>
		/// <param name="uri">The uri of the remote host.</param>
		/// <param name="messageId">The message identifier.</param>
		/// <param name="replyId">The identifier of the response.</param>
		/// <returns>True if the message was processed before.</returns>
		bool WasRegistered(string uri, int messageId, int replyId);
	}
}
