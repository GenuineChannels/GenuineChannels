/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Messaging;

using Belikov.GenuineChannels;
using Belikov.GenuineChannels.BroadcastEngine;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Utilities;

namespace Belikov.GenuineChannels.GenuineHttp
{
	/// <summary>
	/// Implements GHTTP client channel behavior.
	/// </summary>
	public class GenuineHttpClientChannel : BasicChannelWithSecurity
	{
		/// <summary>
		/// Constructs an instance of the GenuineHttpClientChannel class.
		/// </summary>
		/// <param name="properties">An IDictionary of the channel properties which hold the configuration information for the current channel.</param>
		/// <param name="iClientChannelSinkProvider">The IClientChannelSinkProvider that creates the client channel sinks for the underlying channel through which remoting messages flow through.</param>
		/// <param name="iServerChannelSinkProvider">The IServerChannelSinkProvider that creates server channel sinks for the underlying channel through which remoting messages flow through.</param>
		public GenuineHttpClientChannel(IDictionary properties, IClientChannelSinkProvider iClientChannelSinkProvider, IServerChannelSinkProvider iServerChannelSinkProvider) :
			base(iClientChannelSinkProvider, iServerChannelSinkProvider)
		{
			this.ITransportContext = TransportContextServices.CreateDefaultClientHttpContext(properties, this);
			this.InitializeInstance(properties);

			if (this._channelName == null)
				this._channelName = "ghttp";
			if (this._urlPrefix == null)
				this._urlPrefix = "ghttp";
			this._possibleChannelPrefixes = new string[] { this.UrlPrefix, this.UriPrefix };
		}

		#region -- IChannelReceiver ----------------------------------------------------------------

		/// <summary>
		/// Returns an array of all the URLs for a URI.
		/// </summary>
		/// <param name="objectUri">The URI for which URLs are required.</param>
		/// <returns>An array of the URLs.</returns>
		public override string[] GetUrlsForUri(string objectUri)
		{
			return new string[1] { this.UriPrefix + @"://" + this.ITransportContext.HostIdentifier + "/" + objectUri };
		}

		#endregion

		#region -- Channel Prefixes ----------------------------------------------------------------

		/// <summary>
		/// Gets the initial part of the URI prefix.
		/// </summary>
		public override string UriPrefix 
		{ 
			get
			{
				return "_ghttp";
			}
		}

		#endregion

	}
}
