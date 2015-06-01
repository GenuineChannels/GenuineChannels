/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Messaging;

using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.DotNetRemotingLayer
{
	/// <summary>
	/// Factory class creating tcp client transport sinks.
	/// </summary>
	public class GenuineTcpClientTransportSinkProvider : IClientChannelSinkProvider
	{
		/// <summary>
		/// Constructs an instance of the GenuineTcpClientTransportSinkProvider class.
		/// </summary>
		/// <param name="iTransportContext">Transport Context.</param>
		public GenuineTcpClientTransportSinkProvider(ITransportContext iTransportContext)
		{
			this.ITransportContext = iTransportContext;
		}

		/// <summary>
		/// The transport context to send messages through.
		/// </summary>
		public ITransportContext ITransportContext;

		/// <summary>
		/// Gets or sets the next sink provider in the channel sink provider chain.
		/// </summary>
		IClientChannelSinkProvider IClientChannelSinkProvider.Next
		{
			get
			{	
				// it's the last transport sink provider
				return null;
			}
			set
			{
				throw new NotSupportedException();
			}
		}

		/// <summary>
		/// Creates a client transport sink.
		/// </summary>
		/// <param name="channel">Channel for which the current sink chain is being constructed.</param>
		/// <param name="url">The URL of the object to connect to. This parameter can be a null reference (Nothing in Visual Basic) if the connection is based entirely on the information contained in the remoteChannelData parameter.</param>
		/// <param name="remoteChannelData">A channel data object describing a channel on the remote server.</param>
		/// <returns>The first sink of the newly formed channel sink chain, or a null reference (Nothing in Visual Basic) indicating that this provider will not or cannot provide a connection for this endpoint.</returns>
		IClientChannelSink IClientChannelSinkProvider.CreateSink(IChannelSender channel, string url,
			object remoteChannelData)
		{
			return new GenuineTcpClientTransportSink(url, this.ITransportContext);
		}
	}
}
