/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Threading;

namespace Belikov.GenuineChannels.Utilities
{
	/// <summary>
	/// Represents a queued work item.
	/// </summary>
	internal class GenuineThreadPoolWorkItem
	{
		/// <summary>
		/// Constructs an instance of the GenuineThreadPoolWorkItem class.
		/// </summary>
		/// <param name="workCallback">A WaitCallback to be invoked in the working thread.</param>
		/// <param name="callbackState">An object containing data to be used by the working method.</param>
		public GenuineThreadPoolWorkItem(WaitCallback workCallback, object callbackState)
		{
			this.WorkCallback = workCallback;
			this.CallbackState = callbackState;
		}

		/// <summary>
		/// A WaitCallback to be invoked in the working thread.
		/// </summary>
		public WaitCallback WorkCallback;

		/// <summary>
		/// An object containing data to be used by the working method.
		/// </summary>
		public object CallbackState;
	}
}
