/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Channels;
using System.Threading;

using Belikov.GenuineChannels;
using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Security;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Utilities;

namespace Belikov.GenuineChannels.GenuineTcp
{
	/// <summary>
	/// GenuineTcpChannel implements a .NET Remoting channel wrapper over GTCP Transport Context.
	/// </summary>
	public class GenuineTcpChannel : BasicChannelWithSecurity
	{
		/// <summary>
		/// Constructs an instance of the GenuineTcpChannel class.
		/// </summary>
		/// <param name="properties">An IDictionary of the channel properties which hold the configuration information for the current channel.</param>
		/// <param name="iClientChannelSinkProvider">The IClientChannelSinkProvider that creates the client channel sinks for the underlying channel through which remoting messages flow through.</param>
		/// <param name="iServerChannelSinkProvider">The IServerChannelSinkProvider that creates server channel sinks for the underlying channel through which remoting messages flow through.</param>
		public GenuineTcpChannel(IDictionary properties, IClientChannelSinkProvider iClientChannelSinkProvider, IServerChannelSinkProvider iServerChannelSinkProvider)
			: base(iClientChannelSinkProvider, iServerChannelSinkProvider)
		{
			this.ITransportContext = TransportContextServices.CreateDefaultTcpContext(properties, this);
			this.InitializeInstance(properties);

			if (this._channelName == null)
				this._channelName = "gtcp";
			if (this._urlPrefix == null)
				this._urlPrefix = "gtcp";
			this._possibleChannelPrefixes = new string[] { this.UrlPrefix, this.UriPrefix };

			// Start listening
			this.StartListening(null);
		}

		/// <summary>
		/// Returns the client channel sink provider (transport level).
		/// </summary>
		/// <returns>The client channel sink provider.</returns>
		protected override IClientChannelSinkProvider CreateClientChannelSinkProvider()
		{
			return new GenuineTcpClientTransportSinkProvider(this.ITransportContext);
		}

		#region -- IChannelReceiver ----------------------------------------------------------------

		/// <summary>
		/// The port being listened to.
		/// </summary>
		public int LocalPort
		{
			get
			{
				TcpConnectionManager tcpConnectionManager = (TcpConnectionManager) this.ITransportContext.ConnectionManager;
				return tcpConnectionManager.LocalPort;
			}
		}

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

			if (this._localPort >= 0)
				this.ITransportContext.ConnectionManager.StartListening(this.ListeningEntry);
		}

		/// <summary>
		/// Instructs the current channel to stop listening for requests.
		/// </summary>
		/// <param name="data">Optional state information for the channel.</param>
		public override void StopListening(object data)
		{
			if (this._localPort >= 0)
			{
				this.ITransportContext.ConnectionManager.StopListening(this.ListeningEntry);
				base.StopListening(data);
			}
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
				return "_gtcp";
			}
		}

		#endregion

		#region -- Settings ------------------------------------------------------------------------

		/// <summary>
		/// The port number to bind to.
		/// </summary>
		private int _localPort = -1;

		/// <summary>
		/// Interface.
		/// </summary>
		private string _localInterface = "0.0.0.0";

		/// <summary>
		/// The listening entry the channel binds listening socket to.
		/// </summary>
		public string ListeningEntry
		{
			get
			{
				return this.UrlPrefix + @"://" + this._localInterface + ":" + this._localPort;
			}
		}

		/// <summary>
		/// Reads settings.
		/// </summary>
		/// <param name="properties">Settings container to read from.</param>
		protected override void ReadSettings(IDictionary properties)
		{
			base.ReadSettings(properties);

			// retrieve settings
			foreach (DictionaryEntry entry in properties)
			{
				if (string.Compare(entry.Key.ToString(), "interface", true) == 0)
					this._localInterface = entry.Value.ToString();

				if (string.Compare(entry.Key.ToString(), "port", true) == 0)
					this._localPort = GenuineUtility.SafeConvertToInt32(entry.Value, this._localPort);
			}
		}

		#endregion

	}
}
