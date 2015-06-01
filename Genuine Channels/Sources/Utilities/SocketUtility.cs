/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Net;
using System.Net.Sockets;

namespace Belikov.GenuineChannels.Utilities
{
	/// <summary>
	/// Contains a set of methods useful while winsocks are used.
	/// </summary>
	internal class SocketUtility
	{
		/// <summary>
		/// Singleton with static methods.
		/// </summary>
		private SocketUtility()
		{
		}

		/// <summary>
		/// Closes the socket.
		/// </summary>
		/// <param name="socketAsObject">The socket.</param>
		public static void CloseSocket(object socketAsObject)
		{
			Socket socket = socketAsObject as Socket;

			if (socket == null)
				return ;

			try
			{
				socket.Shutdown(SocketShutdown.Both);
			}
			catch(Exception)
			{
			}

			try
			{
				socket.Close();
			}
			catch(Exception)
			{
			}

			try
			{
				((IDisposable) socket).Dispose();
			}
			catch(Exception)
			{
			}
		}
	}
}
