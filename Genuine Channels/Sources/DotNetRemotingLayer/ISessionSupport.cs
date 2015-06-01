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
	/// Represents a thread-safe collection of key-and-value pairs.
	/// </summary>
	public interface ISessionSupport
	{
		/// <summary>
		/// Gets or sets the element with the specified key in thread-safe way.
		/// Deletes an entry if the set value is a null reference.
		/// </summary>
		object this[object key]
		{
			get;
			set;
		}
	}
}
