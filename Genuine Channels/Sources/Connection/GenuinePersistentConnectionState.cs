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
	/// Enumerats all states of a persistent connection.
	/// </summary>
	public enum GenuinePersistentConnectionState
	{
		/// <summary>
		/// Persistent connection is not established.
		/// </summary>
		NotEstablished,

		/// <summary>
		/// Persistent connection was established, local host is a client.
		/// </summary>
		Opened,

		/// <summary>
		/// Persistent connection was accepted, local host is a server.
		/// </summary>
		Accepted,
	}
}
