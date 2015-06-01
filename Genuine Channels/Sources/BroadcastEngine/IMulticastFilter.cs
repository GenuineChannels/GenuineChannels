/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved. 
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Runtime.Remoting.Messaging;

namespace Belikov.GenuineChannels.BroadcastEngine
{
	/// <summary>
	/// Represents a class that filters out unnecessary receivers.
	/// </summary>
	public interface IMulticastFilter
	{
		/// <summary>
		/// Returns receivers that should be called.
		/// </summary>
		/// <param name="cachedReceiverList">All registered receivers (read-only cached array).</param>
		/// <param name="iMessage">The invocation.</param>
		/// <returns>Receivers that will be called.</returns>
		object[] GetReceivers(object[] cachedReceiverList, IMessage iMessage);
	}
}
