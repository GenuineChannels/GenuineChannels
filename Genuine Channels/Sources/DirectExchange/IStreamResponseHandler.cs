/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.IO;

using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.DirectExchange
{
	/// <summary>
	/// Represents a method that handles a stream received from the remote host in reply to the sent
	/// request.
	/// </summary>
	public delegate void StreamResponseEventHandler(object response, HostInformation remoteHost, object tag);

	/// <summary>
	/// Describes behavior of the response handler.
	/// </summary>
	public interface IStreamResponseHandler
	{
		/// <summary>
		/// Handles a stream received from the remote host in reply to the sent request.
		/// </summary>
		/// <param name="response">The response.</param>
		/// <param name="remoteHost">The recipient of the message.</param>
		/// <param name="tag">The object provided during initiation of the invocation.</param>
		void HandleResponse(Stream response, HostInformation remoteHost, object tag);

		/// <summary>
		/// Dispatches the exception to the response processor.
		/// </summary>
		/// <param name="exception">The exception.</param>
		/// <param name="remoteHost">The recipient of the message.</param>
		/// <param name="tag">The object provided during initiation of the invocation.</param>
		void HandleException(Exception exception, HostInformation remoteHost, object tag);
	}
}
