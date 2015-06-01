/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

namespace Belikov.GenuineChannels.Security.ZeroProofAuthorization
{
	/// <summary>
	/// Enumerates the states of the Zero Proof Authorization Security Session FA.
	/// Warning: serialized as a byte.
	/// </summary>
	public enum ZpaPacketStatusFlag
	{
		/// <summary>
		/// The client's command being sent to the server to force building up the 
		/// security context.
		/// </summary>
		ForceInitialization = 15,

		/// <summary>
		/// The first stage of the initialization.
		/// </summary>
		Salt = 20,

		/// <summary>
		/// The second stage of the initialization.
		/// </summary>
		HashedPassword = 25,

		/// <summary>
		/// Security context has been created and security session was established.
		/// </summary>
		SessionEstablished = 30,

		/// <summary>
		/// Exception thrown while establishing security context.
		/// </summary>
		ExceptionThrown = 35,
	}
}
