/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

using Belikov.GenuineChannels.Parameters;
using Belikov.GenuineChannels.Messaging;

namespace Belikov.GenuineChannels.Receiving
{
	/// <summary>
	/// Represents the entity consuming transport's services.
	/// </summary>
	public interface ITransportUser
	{
		/// <summary>
		/// Handles an incoming message delivered by the transport.
		/// </summary>
		/// <param name="message">The message.</param>
		void HandleMessage(Message message);
	}
}
