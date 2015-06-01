/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.IO;

using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.Receiving;
using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.DirectExchange
{
	/// <summary>
	/// Describes the interface between custom server service entry and Genuine Channels' Connection
	/// Manager.
	/// </summary>
	public interface IServerServiceEntry
	{
		/// <summary>
		/// Processes the incoming requests.
		/// Must close the provided stream immediately after using.
		/// Must return true if there is no response to the incoming request.
		/// May not throw any exceptions.
		/// </summary>
		/// <param name="stream">The stream containing a request or a response.</param>
		/// <param name="sender">The remote host that sent this request.</param>
		/// <returns>The response.</returns>
		Stream HandleMessage(Stream stream, HostInformation sender);
	}
}
