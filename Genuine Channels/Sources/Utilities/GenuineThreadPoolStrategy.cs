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
	/// Enumerates all strategies that can be used by Genuine Thread Pool.
	/// </summary>
	public enum GenuineThreadPoolStrategy
	{
		/// <summary>
		/// Always to use Native Thread Pool's working threads.
		/// </summary>
		AlwaysNative,

		/// <summary>
		/// Use Native Thread Pool's working threads for the processing.
		/// Use Genuine Thread Pool's threads only for long-duration processes.
		/// </summary>
		OnlyLongDuration,

		/// <summary>
		/// Use Native Thread Pool's working threads right up to the specified limit.
		/// Use Genuine Thread Pool's threads only for long-duration processes.
		/// </summary>
		SwitchAfterExhaustion,

		/// <summary>
		/// Always use Genuine Thread Pool's threads.
		/// </summary>
		AlwaysThreads,
	}
}
