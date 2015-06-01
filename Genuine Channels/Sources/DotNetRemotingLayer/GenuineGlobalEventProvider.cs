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
	/// The global event provier containing an event fired for every event related to Genuine Channels.
	/// </summary>
	public class GenuineGlobalEventProvider
	{
		/// <summary>
		/// The event which is fired on any global event related to Genuine Channels.
		/// </summary>
		public static event GenuineChannelsGlobalEventHandler GenuineChannelsGlobalEvent
		{
			add
			{
				lock (_genuineChannelsGlobalEventObject)
					_genuineChannelsGlobalEvent += value;
			}
			remove
			{
				lock (_genuineChannelsGlobalEventObject)
					_genuineChannelsGlobalEvent -= value;
			}
		}
		private static event GenuineChannelsGlobalEventHandler _genuineChannelsGlobalEvent;
		private static object _genuineChannelsGlobalEventObject = new object();

		/// <summary>
		/// Fires the GenuineChannelsGlobalEvent event with specified parameters.
		/// </summary>
		/// <param name="sender">The source object.</param>
		/// <param name="e">Event arguments.</param>
		public static void FireGlobalEvent(object sender, GenuineEventArgs e)
		{
			GenuineChannelsGlobalEventHandler clonedEvent = null;

			lock (_genuineChannelsGlobalEventObject)
			{
				if (_genuineChannelsGlobalEvent == null)
					return ;

				clonedEvent = (GenuineChannelsGlobalEventHandler) _genuineChannelsGlobalEvent.Clone();
			}

			clonedEvent(sender, e);
		}

	}
}
