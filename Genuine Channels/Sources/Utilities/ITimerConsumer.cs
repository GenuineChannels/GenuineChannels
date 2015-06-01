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
	/// ITimerConsumer provides an interface for consumers of TimerProvider service.
	/// </summary>
	public interface ITimerConsumer
	{
		/// <summary>
		/// Is called at specified intervals.
		/// The procedure being called must not do any long-duration perfomance during this call.
		/// </summary>
		void TimerCallback();
	}
}
