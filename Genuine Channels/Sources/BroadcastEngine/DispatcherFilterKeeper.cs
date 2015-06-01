/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved. 
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

namespace Belikov.GenuineChannels.BroadcastEngine
{
	/// <summary>
	/// DispatcherFilterKeeper forces the specified dispatcher filter to be used during all 
	/// invocations made in the current thread until the Dispose method is called. 
	/// The previous filter is set up when the Disposed method is called.
	/// </summary>
	public struct DispatcherFilterKeeper : IDisposable
	{
		/// <summary>
		/// Constructs an instance of the DispatcherFilterKeeper class.
		/// </summary>
		/// <param name="iMulticastFilter">The filter supporting IMulticastFilter interface.</param>
		public DispatcherFilterKeeper(IMulticastFilter iMulticastFilter)
		{
			this._previousIMulticastFilter = Dispatcher.SetCurrentFilter(iMulticastFilter);
		}

		private IMulticastFilter _previousIMulticastFilter;

		/// <summary>
		/// Restores the previous dispatcher filter.
		/// </summary>
		public void Dispose()
		{
			Dispatcher.SetCurrentFilter(this._previousIMulticastFilter);
		}

	}
}
