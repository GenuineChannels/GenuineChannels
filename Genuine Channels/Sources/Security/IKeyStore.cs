/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

namespace Belikov.GenuineChannels.Security
{
	/// <summary>
	/// Manages a set of key providers creating Security Sessions.
	/// </summary>
	public interface IKeyStore
	{
		/// <summary>
		/// Gets a key provider.
		/// </summary>
		/// <param name="name">The name of key provider.</param>
		/// <returns>Created key provider.</returns>
		IKeyProvider GetKey(string name);

		/// <summary>
		/// Assign a key provider for the specific Security Session name.
		/// </summary>
		/// <param name="name">The name of Security Context.</param>
		/// <param name="iKeyProvider">The key provider.</param>
		void SetKey(string name, IKeyProvider iKeyProvider);
	}
}
