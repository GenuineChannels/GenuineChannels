/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.Security.ZeroProofAuthorization
{
	/// <summary>
	/// Represents the account manager.
	/// </summary>
	public interface IAuthorizationManager
	{
		/// <summary>
		/// Gets a password for the account represented by the specified login object.
		/// Should fire a serializable exception if the provided login is incorrect.
		/// </summary>
		/// <param name="providedLogin">The login.</param>
		/// <returns>The password.</returns>
		string GetPassword(object providedLogin);
	}
}
