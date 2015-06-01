/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

namespace Belikov.GenuineChannels.DotNetRemotingLayer
{
	/// <summary>
	/// Is used to fire events related to Genuine Channels.
	/// </summary>
	public delegate void GenuineChannelsGlobalEventHandler(object sender, GenuineEventArgs e);

	/// <summary>
	/// IEventProvider allows intercepting channel events.
	/// </summary>
	public interface IGenuineChannelEventProvider
	{
		/// <summary>
		/// This event is fired on any global event related to Genuine Channels.
		/// </summary>
		event GenuineChannelsGlobalEventHandler GenuineChannelsEvent;

		/// <summary>
		/// Fires the event with the specified parameter.
		/// </summary>
		/// <param name="e">The event arguments.</param>
		void FireGenuineEvent(GenuineEventArgs e);
	}
}
