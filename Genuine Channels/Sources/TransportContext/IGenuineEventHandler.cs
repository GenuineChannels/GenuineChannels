/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

using Belikov.GenuineChannels.DotNetRemotingLayer;

namespace Belikov.GenuineChannels.TransportContext
{
	/// <summary>
	/// IGenuineEventHandler interface describes a method associated with the event.
	/// </summary>
	public interface IGenuineEventHandler
	{
		/// <summary>
		/// Handles the event. Fires an exception if it doesn't want to receive events anymore.
		/// </summary>
		/// <param name="genuineEventArgs">The event information.</param>
		void OnGenuineEvent(GenuineEventArgs genuineEventArgs);
	}
}
