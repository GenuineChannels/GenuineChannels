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
	/// Enumerates all global events available in Genuine Channels solution.
	/// </summary>
	public enum GenuineEventType
	{
		/// <summary>
		/// The connection is in the indeterminated state.
		/// </summary>
		GeneralConnectionIndeterminate,

		/// <summary>
		/// The connection was established.
		/// </summary>
		GeneralConnectionEstablished,

		/// <summary>
		/// The remote server has been restarted.
		/// All URIs and transparent proxies became invalid.
		/// Client application has to re-login and reattach to all server's events.
		/// </summary>
		GeneralServerRestartDetected,

		/// <summary>
		/// The remote host signals that this is the first persistent connection it has accepted or opened.
		/// Therefore, remote host has an empty Client Session and has no established Security Session.
		/// </summary>
		GeneralNewSessionDetected,

		/// <summary>
		/// The connection has been broken but will be probably restored.
		/// </summary>
		GeneralConnectionReestablishing,

		/// <summary>
		/// The connection was closed and all resources related to this connection were released.
		/// </summary>
		GeneralConnectionClosed,

		/// <summary>
		/// Connection Manager successfully started listening to the specific local end point.
		/// </summary>
		GeneralListenerStarted,

		/// <summary>
		/// Connection Manager stopped listening to the specific local end point.
		/// </summary>
		GeneralListenerShutDown,

		/// <summary>
		/// Connection Manager caught an exception during accepting connection.
		/// </summary>
		GeneralListenerFailure,

		// -- GTCP events --------------------------------------------------------------------------

		/// <summary>
		/// GTCP connection accepted from the client. You can analyze client's end point and
		/// reject the connection.
		/// </summary>
		GTcpConnectionAccepted,

		// -- GHTTP events -------------------------------------------------------------------------

		/// <summary>
		/// GHTTP connection accepted from the client.
		/// </summary>
		GHttpConnectionAccepted,

		// -- UDP events ---------------------------------------------------------------------------

		/// <summary>
		/// An exception fired by UDP socket.
		/// </summary>
		GUdpSocketException,

		// -- Broadcast Engine ---------------------------------------------------------------------

		/// <summary>
		/// Broadcast message directed to unassigned court was received.
		/// </summary>
		BroadcastUnknownCourtReceived,

		// -- Direct Exchange Manager --------------------------------------------------------------

		/// <summary>
		/// A request has come to an unknown server service.
		/// </summary>
		UnknownServerService,

		// -- Security Session Events --------------------------------------------------------------

		/// <summary>
		/// Unknown security session is requested by the remote peer.
		/// </summary>
		SecuritySessionWasNotFound,

		/// <summary>
		/// Security session failed. All requests sent in this security context will result in
		/// exception. All responses will be lost (actually the current design of Genuine Channels
		/// prevents from having such responses, but in general it's possible).
		/// </summary>
		SecuritySessionFailed,

		/// <summary>
		/// All resources associated with the remote host were released.
		/// </summary>
		HostResourcesReleased,

	}
}
