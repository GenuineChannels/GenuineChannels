/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;

using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Logbook;

namespace Belikov.GenuineChannels.Security.SSPI
{
	/// <summary>
	/// Represents a server's SSPI security context.
	/// </summary>
	public class SspiServerSecurityContext : SspiSecurityContext
	{
		/// <summary>
		/// Constructs an instance of the SspiServerSecurityContext class.
		/// </summary>
		/// <param name="keyProvider_SspiServer">Parent KeyProvider_SspiServer factory.</param>
		public SspiServerSecurityContext(KeyProvider_SspiServer keyProvider_SspiServer)
		{
			this.KeyProvider_SspiServer = keyProvider_SspiServer;

			// get credentials handle
			SspiApi.AcquireCredentialsHandle(null, 
				this.KeyProvider_SspiServer.PackageName, SspiApi.SECPKG_CRED_INBOUND,
				this._credHandle, ref this._ptsExpiry);
		}

		/// <summary>
		/// Parent session factory to get settings from.
		/// </summary>
		public KeyProvider_SspiServer KeyProvider_SspiServer;

		/// <summary>
		/// Indicates whether security context has been established.
		/// </summary>
		public bool ContinueProcessing = true;

		/// <summary>
		/// Indicates whether AcceptSecurityContext has initialized this._phContext member.
		/// </summary>
		private bool firstCall = true;

		/// <summary>
		/// Builds up a security context between the client application and a remote peer.
		/// Should be called until continueProcessing is false.
		/// </summary>
		/// <param name="receivedData">Data received from the remote host.</param>
		/// <param name="outputStream">Stream to write the data being sent into.</param>
		/// <returns>True if security context has been succesfully built up.</returns>
		public bool BuildUpSecurityContext(Stream receivedData, Stream outputStream)
		{
			bool result = SspiApi.AcceptSecurityContext(this._credHandle, this._phContext,
				this.KeyProvider_SspiServer.RequiredFeatures, ref this._ptsExpiry,
				receivedData, outputStream, firstCall);

			firstCall = false;
			this.ContinueProcessing = !result;
			return result;
		}

	}
}
