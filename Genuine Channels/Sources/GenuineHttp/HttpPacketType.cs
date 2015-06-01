/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

namespace Belikov.GenuineChannels.GenuineHttp
{
	/// <summary>
	/// Enumerates types of HTTP packet.
	/// </summary>
	internal enum HttpPacketType
	{
		/// <summary>
		/// CLIENT. The usual (PIO/sender) message.
		/// </summary>
		Usual,

		/// <summary>
		/// CLIENT. The listening request (P/listener).
		/// </summary>
		Listening,

		/// <summary>
		/// CLIENT/SERVER. The request to establish a new connection and release all data previously acquired by this host.
		/// </summary>
		Establishing_ResetConnection,

		/// <summary>
		/// CLIENT/SERVER. The request to establish a connection.
		/// </summary>
		Establishing,

		/// <summary>
		/// SERVER. Server received two listener requests and this one is ignored.
		/// </summary>
		DoubleRequests,

		/// <summary>
		/// SERVER. Server repeated the response.
		/// </summary>
		RequestRepeated,

		/// <summary>
		/// SERVER. Too small or too large sequence number.
		/// </summary>
		Desynchronization,

		/// <summary>
		/// SERVER. The response to the sender's request.
		/// </summary>
		SenderResponse,

		/// <summary>
		/// SERVER. The listener request is expired.
		/// </summary>
		ListenerTimedOut,

		/// <summary>
		/// SERVER. The listener connection is manually closed.
		/// </summary>
		ClosedManually,

		/// <summary>
		/// SERVER. Error during parsing the client's request.
		/// </summary>
		SenderError,

		/// <summary>
		/// CLIENT/SERVER. Specifies undetermined state of the connection.
		/// </summary>
		Unkown,

	}
}
