/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

namespace Belikov.GenuineChannels.Security.SSPI
{
	/// <summary>
	/// Enumerates all supported SSPI features.
	/// DEVELOPER WARNING: Serialized as a byte.
	/// </summary>
	[Flags]
	public enum SspiFeatureFlags
	{
		/// <summary>
		/// Simple authentication.
		/// </summary>
		None = 0,

		/// <summary>
		/// Forces content encryption (and content integrity as well).
		/// </summary>
		Encryption = 1,

		/// <summary>
		/// Forces checking content integrity.
		/// </summary>
		Signing = 2,

		/// <summary>
		/// Forces execution impersonation on the server side.
		/// </summary>
		Impersonation = 4,

		/// <summary>
		/// Forces delegation on the server side. Valid only if Kerberos package is used.
		/// </summary>
		Delegation = 8,

	}
}
