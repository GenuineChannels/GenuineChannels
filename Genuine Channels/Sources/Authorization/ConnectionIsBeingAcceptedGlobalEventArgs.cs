/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Net;

using Belikov.GenuineChannels.DotNetRemotingLayer;

namespace Belikov.GenuineChannels.Authorization
{
	/// <summary>
	/// ConnectionIsBeingAcceptedGlobalEventArgs contains authentication information
	/// about a remote host which establishes a connection.
	/// </summary>
	public class ConnectionIsBeingAcceptedGlobalEventArgs : EventArgs
	{
		/// <summary>
		/// Represents a value indicating whether an action should be cancelled.
		/// </summary>
		public bool Cancel = false;

		/// <summary>
		/// The address of the remote host.
		/// </summary>
		public IPEndPoint IPEndPoint;

		/// <summary>
		/// Url of the remote host or a null reference.
		/// </summary>
		public string Url;

		/// <summary>
		/// The login name provided by the remote host.
		/// </summary>
		public string LoginName;

		/// <summary>
		/// The hashsum of the password provided by the remote host.
		/// </summary>
		public byte[] HashedPassword;

		/// <summary>
		/// The credential provided by the remote host.
		/// </summary>
		public object ProvidedCredentials;
	}
}
