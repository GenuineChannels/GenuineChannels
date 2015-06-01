/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Threading;

using Belikov.GenuineChannels.BroadcastEngine;
using Belikov.GenuineChannels.Logbook;

namespace Belikov.GenuineChannels.Receiving
{
	/// <summary>
	/// Implements a message registrator which remembers all messages during specified period of time.
	/// </summary>
	public class MessageRegistratorWithLimitedTime : IMessageRegistrator
	{
		/// <summary>
		/// Constructs an instance of the MessageRegistratorWithLimitedTime class.
		/// </summary>
		public MessageRegistratorWithLimitedTime()
		{
		}

		/// <summary>
		/// Checks whether this message was processed before.
		/// </summary>
		/// <param name="uri">The uri of the remote host.</param>
		/// <param name="messageId">The message identifier.</param>
		/// <param name="replyId">The identifier of the response.</param>
		/// <returns>True if message was processed before.</returns>
		public bool WasRegistered(string uri, int messageId, int replyId)
		{
			return UniqueCallTracer.Instance.WasGuidRegistered("_GRH_/" + uri + "/" + messageId.ToString() + "/" + replyId.ToString());
		}
	}
}
