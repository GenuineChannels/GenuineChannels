/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Text;
using System.Text.RegularExpressions;

using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Parameters;
using Belikov.GenuineChannels.Receiving;
using Belikov.GenuineChannels.Security;
using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.GenuineXHttp
{
	/// <summary>
	/// Represents a connection, which consists of listener and sender TCP connections.
	/// </summary>
	internal class XHttpConnection : GeneralConnection
	{
		/// <summary>
		/// Constructs an instance of the XHttpClientConnection class.
		/// </summary>
		/// <param name="xHttpConnectionManager">The parent connection manager.</param>
		/// <param name="isClient">Indicates the type of network logic.</param>
		/// <param name="connectionName">The name of the connection.</param>
		public XHttpConnection(XHttpConnectionManager xHttpConnectionManager, bool isClient, string connectionName) : base(xHttpConnectionManager.ITransportContext)
		{
			this.XHttpConnectionManager = xHttpConnectionManager;
			this.IsClient = isClient;
			this.ConnectionName = connectionName;

			// initialize connection id
			this.HostId = xHttpConnectionManager.ITransportContext.BinaryHostIdentifier;
			this.HostIdAsString = xHttpConnectionManager.ITransportContext.HostIdentifier;

			// cache all setting's values
			this.UserAgent = xHttpConnectionManager.ITransportContext.IParameterProvider[GenuineParameter.HttpWebUserAgent] as string;
			if (this.UserAgent == null)
				this.UserAgent = @"Mozilla/4.0+ (compatible; MSIE 6.0; Windows " + Environment.OSVersion.Version +
					"; GXHTTP; MS .NET CLR " + Environment.Version.ToString() + ")";
			this.UserAgent = Regex.Replace(this.UserAgent, "\r|\n", " ", RegexOptions.None);
		}

		/// <summary>
		/// The name of the User Agent.
		/// </summary>
		public string UserAgent;

		/// <summary>
		/// The parent Connection Manager.
		/// </summary>
		public XHttpConnectionManager XHttpConnectionManager;

		/// <summary>
		/// The identifier of the host.
		/// </summary>
		public byte[] HostId;

		/// <summary>
		/// The type of connection.
		/// </summary>
		public bool IsClient;

		/// <summary>
		/// The name of the connection.
		/// </summary>
		public string ConnectionName;

		/// <summary>
		/// The unique connection identifier, which is used for debugging purposes only.
		/// </summary>
		public int DbgConnectionId = ConnectionManager.GetUniqueConnectionId();

		#region -- Listener ------------------------------------------------------------------------

		/// <summary>
		/// The listening connection.
		/// </summary>
		public XHttpPhysicalConnection Listener;

		#endregion

		#region -- Sender --------------------------------------------------------------------------

		public XHttpPhysicalConnection Sender;

		#endregion

		#region -- Disposing -----------------------------------------------------------------------

		/// <summary>
		/// Releases acquired resources.
		/// </summary>
		/// <param name="reason">The reason of disposing.</param>
		public override void InternalDispose(Exception reason)
		{
			if (this.Listener != null)
				this.Listener.Dispose(reason);
			if (this.Sender != null)
				this.Sender.Dispose(reason);
		}

		#endregion

	}
}
