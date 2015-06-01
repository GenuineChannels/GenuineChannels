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
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Parameters;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Utilities;

namespace Belikov.GenuineChannels.GenuineSharedMemory
{
	/// <summary>
	/// Implements a channel that uses Shared Memory as a transport.
	/// </summary>
	public class GenuineSharedMemoryChannel : BasicChannelWithSecurity
	{
		/// <summary>
		/// Constructs an instance of the GenuineSharedMemoryChannel class.
		/// </summary>
		/// <param name="properties">An IDictionary of the channel properties which hold the configuration information for the current channel.</param>
		/// <param name="iClientChannelSinkProvider">The IClientChannelSinkProvider that creates the client channel sinks for the underlying channel through which remoting messages flow through.</param>
		/// <param name="iServerChannelSinkProvider">The IServerChannelSinkProvider that creates server channel sinks for the underlying channel through which remoting messages flow through.</param>
		public GenuineSharedMemoryChannel(IDictionary properties, IClientChannelSinkProvider iClientChannelSinkProvider, IServerChannelSinkProvider iServerChannelSinkProvider)
			: base(iClientChannelSinkProvider, iServerChannelSinkProvider)
		{
			this.ITransportContext = TransportContextServices.CreateDefaultSharedMemoryContext(properties, this);
			this.InitializeInstance(properties);

			if (this._channelName == null)
				this._channelName = "gshmem";
			if (this._urlPrefix == null)
				this._urlPrefix = "gshmem";
			this._possibleChannelPrefixes = new string[] { this.UrlPrefix, this.UriPrefix };

			// retrieve settings
			foreach (DictionaryEntry entry in properties)
			{
				if (string.Compare(entry.Key.ToString(), "listen", true) == 0)
					this._uriToListen = entry.Value.ToString();
			}

			if (this._uriToListen != null && (this._uriToListen.Length <= 0 || ! this._uriToListen.StartsWith("gshmem:")))
				throw GenuineExceptions.Get_Server_IncorrectAddressToListen(this._uriToListen);

			// start listening
			if (this._uriToListen != null)
				this.StartListening(null);
		}

		/// <summary>
		/// Constructs a full name of a shared object given the name and channel properties.
		/// </summary>
		/// <param name="name">The name of a shared object.</param>
		/// <param name="parameters">The channel parameters.</param>
		/// <returns>The full name of a shared object.</returns>
		public static string ConstructSharedObjectName(string name, IParameterProvider parameters)
		{
			if (name == null)
				throw new ArgumentNullException("name");
   	
			if (parameters == null)
				throw new ArgumentNullException("parameters");
   	
			bool smSessionLocal = (bool) parameters[GenuineParameter.SMSessionLocal];
			string objectName = (smSessionLocal ? @"Local\" : @"Global\") + name;

			return objectName;
		}

		private string _uriToListen;

		/// <summary>
		/// Returns an array of all URLs for the URI.
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
			this.ITransportContext.ConnectionManager.StartListening(this._uriToListen);
		}

		/// <summary>
		/// Instructs the current channel to stop listening for requests.
		/// </summary>
		/// <param name="data">Optional state information for the channel.</param>
		public override void StopListening(object data)
		{
			this.ITransportContext.ConnectionManager.StopListening(this._uriToListen);
			base.StopListening(data);
		}

		#region ----------------------Channel Prefixes--------------------------

		/// <summary>
		/// Gets the URI prefix.
		/// </summary>
		public override string UriPrefix 
		{ 
			get
			{
				return "_gshmem";
			}
		}

		#endregion

	}
}
