/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Net.Sockets;

namespace Belikov.GenuineChannels.GenuineTcp
{
	/// <summary>
	/// Exposes a connection consumer, which completes accepting of connections
	/// and pass them to Connection Manager.
	/// </summary>
	internal interface IAcceptConnectionConsumer
	{
		/// <summary>
		/// Gets an indication whether the Connection Manager has been disposed.
		/// </summary>
		/// <returns></returns>
		bool IsDisposed();

		/// <summary>
		/// Completes accepting of the specified socket.
		/// </summary>
		/// <param name="socket">The socket accepted.</param>
		void AcceptConnection(Socket socket);
	}
}
