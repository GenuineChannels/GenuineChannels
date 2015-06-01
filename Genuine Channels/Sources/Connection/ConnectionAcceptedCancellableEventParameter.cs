/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Net;
using System.Net.Sockets;

namespace Belikov.GenuineChannels.Connection
{
	/// <summary>
	/// Implements a Genuine Channels event provider that provides the Cancel member which can be used to cancel
	/// event consequences.
	/// </summary>
	public class ConnectionAcceptedCancellableEventParameter
	{
		/// <summary>
		/// Indicates whether an action must be cancelled.
		/// </summary>
		public bool Cancel = false;

		/// <summary>
		/// The Socket being accepted.
		/// </summary>
		public Socket Socket;

		/// <summary>
		/// Remote IPEndPoint.
		/// </summary>
		public IPEndPoint IPEndPoint;
	}
}
