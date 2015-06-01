/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

namespace Belikov.GenuineChannels.Connection
{
	/// <summary>
	/// Enumerates the types of connection patterns supported by Genuine Channels.
	/// WARNING: serialized as a byte.
	/// </summary>
	[Flags]
	public enum GenuineConnectionType
	{
		/// <summary>
		/// No connections will be affected by this value.
		/// </summary>
		None = 0x00,

		/// <summary>
		/// A connection for the usual exchange between two hosts.
		/// </summary>
		Persistent = 0x01,

		/// <summary>
		/// A kind of persistent connection using for specific calls.
		/// </summary>
		Named = 0x02,

		/// <summary>
		/// Connection should be closed after an invocation is delivered to the remote host and the reply to it received.
		/// No other packets may be sent through this connection.
		/// </summary>
		Invocation = 0x04,

//		/// <summary>
//		/// Connection is to close immediately after a message has been sent or received.
//		/// </summary>
//		OneWay = 0x08,

		/// <summary>
		/// All connections will be affected by this value.
		/// </summary>
		All = 0xFF,
	}
}
