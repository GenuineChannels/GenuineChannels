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
	/// Represents a client's SSPI security context.
	/// </summary>
	public class SspiClientSecurityContext : SspiSecurityContext
	{
		/// <summary>
		/// Constructs an instance of the SspiClientSecurityContext class.
		/// </summary>
		/// <param name="keyProvider_SspiClient">Parent factory to get settings from.</param>
		public SspiClientSecurityContext(KeyProvider_SspiClient keyProvider_SspiClient)
		{
			// write settings
			this.KeyProvider_SspiClient = keyProvider_SspiClient;

//			// WAS:
//			// get credentials handle
//			SspiApi.AcquireCredentialsHandle(keyProvider_SspiClient.AuthIdentity, 
//				keyProvider_SspiClient.PackageName, SspiApi.SECPKG_CRED_OUTBOUND, 
//				this._credHandle, ref this._ptsExpiry);


			if (this.KeyProvider_SspiClient.DelegatedContext == null)
			{
				// get credentials handle
				SspiApi.AcquireCredentialsHandle(keyProvider_SspiClient.AuthIdentity, 
					keyProvider_SspiClient.PackageName, SspiApi.SECPKG_CRED_BOTH, 
					this._credHandle, ref this._ptsExpiry);
			}
			else
			{
				SspiApi.AcquireCredentialsHandle(keyProvider_SspiClient.AuthIdentity, 
					keyProvider_SspiClient.PackageName, SspiApi.SECPKG_CRED_BOTH, 
					this._credHandle, ref this._ptsExpiry);
			}
		}

		/// <summary>
		/// Parent factory to get settings from.
		/// </summary>
		public KeyProvider_SspiClient KeyProvider_SspiClient;

		/// <summary>
		/// Indicates whether security context has been built up.
		/// </summary>
		public bool ContinueProcessing = true;

		/// <summary>
		/// Builds up a security context between the client application and a remote peer.
		/// Should be called until continueProcessing is false.
		/// </summary>
		/// <param name="receivedData">Data received from the remote host.</param>
		/// <param name="outputStream">Stream to write the data being sent into.</param>
		/// <returns>True if security context has been succesfully built up.</returns>
		public bool BuildUpSecurityContext(Stream receivedData, Stream outputStream)
		{
			bool result = SspiApi.InitializeSecurityContext(this._credHandle,
				receivedData == null ? null : this._phContext, this._phContext, this.KeyProvider_SspiClient.ServerName, 
				this.KeyProvider_SspiClient.RequiredFeatures, ref this._ptsExpiry,
				receivedData, outputStream);

			this.ContinueProcessing = ! result;
			return result;
		}

	}
}
