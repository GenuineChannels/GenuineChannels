/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

namespace Belikov.GenuineChannels
{
	/// <summary>
	/// Provides methods to localize the text of exceptions.
	/// </summary>
	public interface IGenuineExceptionLocalizer
	{
		/// <summary>
		/// Returns the localized error message corresponding to the specified error identifier or a null reference.
		/// </summary>
		/// <param name="errorId">The error identifier.</param>
		/// <param name="messageParameters">The message parameters.</param>
		/// <returns>A localized error message or a null reference to use the default error message.</returns>
		string Localize(string errorId, params object[] messageParameters);
	}
}
