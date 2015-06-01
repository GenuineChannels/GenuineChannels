/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

namespace Belikov.GenuineChannels.Parameters
{
	/// <summary>
	/// Represents a way to get specific channel's or transport's parameter value quickly.
	/// </summary>
	public interface IParameterProvider
	{
		/// <summary>
		/// Gets a parameter's value.
		/// </summary>
		/// <param name="genuineParameter">The name of the parameter.</param>
		/// <returns>The value of the parameter.</returns>
		object this[GenuineParameter genuineParameter] { get; set; }
	}
}
