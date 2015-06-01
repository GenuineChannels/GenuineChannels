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
	/// Genuine Channel Event Provider being used in the specific Transport Context.
	/// </summary>
	public interface IGenuineEventProvider
	{
		/// <summary>
		/// Registers the event consumer.
		/// </summary>
		/// <param name="iGenuineEventHandler">The event handler to be registered.</param>
		void Register(IGenuineEventHandler iGenuineEventHandler);

		/// <summary>
		/// Unregisters the event consumer.
		/// </summary>
		/// <param name="iGenuineEventHandler">The event handler to be unregistered.</param>
		void Unregister(IGenuineEventHandler iGenuineEventHandler);

		/// <summary>
		/// Fires the event.
		/// </summary>
		/// <param name="genuineEventArgs">The event parameters.</param>
		void Fire(GenuineEventArgs genuineEventArgs);
	}
}
