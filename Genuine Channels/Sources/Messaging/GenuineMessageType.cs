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
	/// Enumerates types of messages being used by Genuine Channels solution.
	/// WARNING: Serialized as a byte.
	/// </summary>
	public enum GenuineMessageType
	{
		/// <summary>
		/// Unknown message types usually means incorrect incoming content.
		/// </summary>
		Unknown = 0,

		/// <summary>
		/// An ordinary transport user.
		/// </summary>
		Ordinary = 1,

		/// <summary>
		/// A message received by "true" multicast channel.
		/// </summary>
		TrueBroadcast = 2,

		/// <summary>
		/// Broadcast engine invocation directed to specific object.
		/// </summary>
		BroadcastEngine = 3,

		/// <summary>
		/// Stream content should be redirected by logical channel to another remote host.
		/// </summary>
		RedirectLogicalChannel = 4,

		/// <summary>
		/// Stream content will be consumed by external stream consumer.
		/// </summary>
		ExternalStreamConsumer = 5,

		/// <summary>
		/// Don't know whether someone will need it.
		/// </summary>
		Custom = 6,
	}
}
