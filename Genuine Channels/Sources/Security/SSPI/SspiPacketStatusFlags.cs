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
	/// Enumerates SSPI session commands.
	/// WARNING: serialized as a byte value.
	/// </summary>
	public enum SspiPacketStatusFlags
	{
		/// <summary>
		/// New security context should be created.
		/// </summary>
		InitializeFromScratch,

		/// <summary>
		/// Building up of the current security context is in progress.
		/// </summary>
		ContinueAuthentication,

		/// <summary>
		/// The server's command being sent to the client to force building up the 
		/// security context when the server has a message to be sent under this context.
		/// </summary>
		ForceInitialization,

		/// <summary>
		/// Security context has been created and security session was established.
		/// </summary>
		SessionEstablished,

		/// <summary>
		/// Exception thrown while establishing security context.
		/// </summary>
		ExceptionThrown,
	}
}
