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
	/// Represents a working item that starts asynchronous operation.
	/// </summary>
	internal interface IAsyncWorkItem
	{
		/// <summary>
		/// Initiates an asynchronous operation.
		/// </summary>
		void StartAsynchronousOperation();
	}
}
