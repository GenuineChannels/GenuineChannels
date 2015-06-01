/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

namespace Belikov.GenuineChannels.GenuineTcp
{
	/// <summary>
	/// Enumerats lifetime states of TCP finite state automaton.
	/// </summary>
	public enum TcpInvocationFiniteAutomatonState
	{
		/// <summary>
		/// Connection was terminated.
		/// </summary>
		Teardown,

		/// <summary>
		/// Connection is not in use.
		/// </summary>
		ClientAvailable,

		/// <summary>
		/// Sending was initiated.
		/// </summary>
		ClientSending,

		/// <summary>
		/// Response receiving was initiated.
		/// </summary>
		ClientReceiving,

		/// <summary>
		/// Awaiting for the incoming request (i.e. reading).
		/// </summary>
		ServerAwaiting,

		/// <summary>
		/// Awaiting until a response will be reported.
		/// </summary>
		ServerExecution,

		/// <summary>
		/// Response is sending to the remote host.
		/// </summary>
		ServerSending,

	}
}
