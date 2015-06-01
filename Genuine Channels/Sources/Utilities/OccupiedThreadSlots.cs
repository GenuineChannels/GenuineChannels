/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

namespace Belikov.GenuineChannels.Utilities
{
	/// <summary>
	/// Enumerates names of the used thread slots.
	/// </summary>
	public class OccupiedThreadSlots
	{
		/// <summary>
		/// The current message being executed.
		/// It contains GC uris, Security Session parameters and the transport shell.
		/// </summary>
		public const string CurrentMessage = "__GC__CurrentMessage";

		/// <summary>
		/// Specifies the name of the thread slot where the socket will be kept while connection is being established.
		/// </summary>
		public const string SocketDuringEstablishing = "__GC__EstablishingSocket";

		/// <summary>
		/// The Security Session parameters being used in the current thread.
		/// </summary>
		public const string CurrentSecuritySessionParameters = "__GC__CurrentSecuritySessionParameters";

		/// <summary>
		/// Current Dispatcher filter being used.
		/// </summary>
		public const string DispatcherFilterUsed = "__GC__DispatcherFilterUsed";
	}
}
