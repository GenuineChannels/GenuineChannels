/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Channels;
using System.Threading;

using Belikov.GenuineChannels;
using Belikov.GenuineChannels.BroadcastEngine;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Utilities;

namespace Belikov.GenuineChannels.GenuineUdp
{
	/// <summary>
	/// Implements UDP Genuine Channel with IP multicasting support.
	/// </summary>
	public class GenuineUdpChannel : BasicChannelWithSecurity, IBroadcastSenderProvider
	{
		/// <summary>
		/// Constructs an instance of the GenuineUdpChannel class.
		/// </summary>
		/// <param name="properties">An IDictionary of the channel properties which hold the configuration information for the current channel.</param>
		/// <param name="iClientChannelSinkProvider">The IClientChannelSinkProvider that creates the client channel sinks for the underlying channel through which remoting messages flow through.</param>
		/// <param name="iServerChannelSinkProvider">The IServerChannelSinkProvider that creates server channel sinks for the underlying channel through which remoting messages flow through.</param>
		public GenuineUdpChannel(IDictionary properties, IClientChannelSinkProvider iClientChannelSinkProvider, IServerChannelSinkProvider iServerChannelSinkProvider)
			: base(iClientChannelSinkProvider, iServerChannelSinkProvider)
		{
			this.ITransportContext = TransportContextServices.CreateDefaultUdpContext(properties, this);
			this.InitializeInstance(properties);

			if (this._channelName == null)
				this._channelName = "gudp";
			if (this._urlPrefix == null)
				this._urlPrefix = "gudp";
			this._possibleChannelPrefixes = new string[] { this.UrlPrefix, this.UriPrefix, GetBroadcastUriPrefix(this.UrlPrefix), GetBroadcastUriPrefix(this.UriPrefix) };

			// retrieve settings
			string uriToListen = this["Address"] as string;
			foreach (DictionaryEntry entry in properties)
			{
				if (string.Compare(entry.Key.ToString(), "Address", true) == 0)
					uriToListen = entry.Value.ToString();
			}
			if (uriToListen == null || uriToListen.Length <= 0 || ! uriToListen.StartsWith(this.UrlPrefix + ":"))
				throw GenuineExceptions.Get_Server_IncorrectAddressToListen(uriToListen);

			this.StartListening(uriToListen);
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

		/// <summary>
		/// Instructs the current channel to start listening for requests.
		/// </summary>
		/// <param name="data">Optional initialization information.</param>
		public override void StartListening(object data)
		{
			base.StartListening(data);
			this.ITransportContext.ConnectionManager.StartListening(data);
		}

		/// <summary>
		/// Instructs the current channel to stop listening for requests.
		/// </summary>
		/// <param name="data">Optional state information for the channel.</param>
		public override void StopListening(object data)
		{
			this.ITransportContext.ConnectionManager.StopListening(data);
			base.StopListening(data);
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
				return "_gudp";
			}
		}

		/// <summary>
		/// Changes gXXX uri prefix to gbXXX.
		/// </summary>
		/// <param name="prefix">The prefix of an URI.</param>
		/// <returns>The changed prefix.</returns>
		private string GetBroadcastUriPrefix(string prefix)
		{
			if (prefix.StartsWith("g"))
				return "gb" + prefix.Substring(1);
			return "_gb" + prefix.Substring(2);
		}

		#endregion

		#region -- IBroadcastSenderProvider --------------------------------------------------------

		/// <summary>
		/// Creates and answers with IP Multicast sender that multicast messages via IP multicasting
		/// to the specified court.
		/// </summary>
		/// <param name="court">Court name.</param>
		/// <returns>Returns sender for IP multicasting to the specified court.</returns>
		public GeneralBroadcastSender GetBroadcastSender(string court)
		{
			return new IPMulticastSender(court, this.ITransportContext);
		}

		#endregion

	}

}
