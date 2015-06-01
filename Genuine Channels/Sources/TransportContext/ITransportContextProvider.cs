/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

namespace Belikov.GenuineChannels.TransportContext
{
	/// <summary>
	/// Contains a property to fetch the used ITransportContext from the target.
	/// </summary>
	public interface ITransportContextProvider
	{
		/// <summary>
		/// Gets the underlying transport context.
		/// </summary>
		ITransportContext ITransportContext { get; }
	}
}
