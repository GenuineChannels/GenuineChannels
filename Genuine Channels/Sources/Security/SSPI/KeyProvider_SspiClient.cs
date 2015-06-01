/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Net;
using System.Security;

using Belikov.GenuineChannels.Security;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.Security.SSPI
{
	/// <summary>
	/// Spawns SecuritySession_SspiClient sessions that use
	/// SSPI API to provide authentication, encryption, content integrity checking and other security features.
	/// </summary>
	public class KeyProvider_SspiClient : IKeyProvider
	{
		/// <summary>
		/// Constructs an instance of the KeyProvider_SspiClient class.
		/// </summary>
		/// <param name="requiredFeatures">SSPI features that will be provided by the security session.</param>
		/// <param name="packageName">The name of the used SSPI package.</param>
		/// <param name="authIdentity">The authentication identity used during authentication.</param>
		/// <param name="targetName">The name of the server which will be the target of the context. See description of the InitializeSecurityContext function in Platform SDK Security.</param>
		public KeyProvider_SspiClient(SspiFeatureFlags requiredFeatures, SupportedSspiPackages packageName, NetworkCredential authIdentity, string targetName) : this(requiredFeatures, packageName, authIdentity, targetName, null)
		{
		}

		/// <summary>
		/// Constructs an instance of the KeyProvider_SspiClient class.
		/// </summary>
		/// <param name="requiredFeatures">SSPI features that will be provided by the security session.</param>
		/// <param name="packageName">The name of the used SSPI package.</param>
		/// <param name="authIdentity">The authentication identity used during authentication.</param>
		/// <param name="targetName">The name of the server which will be the target of the context. See description of the InitializeSecurityContext function in Platform SDK Security.</param>
		/// <param name="delegatedContext">The context being delegated to the destination host.</param>
		public KeyProvider_SspiClient(SspiFeatureFlags requiredFeatures, SupportedSspiPackages packageName, NetworkCredential authIdentity, string targetName, SecuritySession_SspiServer delegatedContext)
		{
			this.RequiredFeatures = requiredFeatures;
			this.PackageName = Enum.Format(typeof(SupportedSspiPackages), packageName, "f");
			this.AuthIdentity = authIdentity;
			this.ServerName = targetName;
			this.DelegatedContext = delegatedContext;
		}

		/// <summary>
		/// SSPI features that will be provided by the security session.
		/// </summary>
		public SspiFeatureFlags RequiredFeatures;

		/// <summary>
		/// The name of the used SSPI package.
		/// </summary>
		public string PackageName;

		/// <summary>
		/// The authentication identity used during authentication.
		/// </summary>
		public NetworkCredential AuthIdentity;

		/// <summary>
		/// Name of the server which will be the target of the context.
		/// </summary>
		public string ServerName;

		/// <summary>
		/// The security context being delegated to the remote host.
		/// </summary>
		public SecuritySession_SspiServer DelegatedContext;

		/// <summary>
		/// Creates a Security Session.
		/// </summary>
		/// <param name="name">Name of the SecuritySession being created.</param>
		/// <param name="remote">The remote host.</param>
		/// <returns>Created Security Session.</returns>
		public SecuritySession CreateSecuritySession(string name, HostInformation remote)
		{
			return new SecuritySession_SspiClient(name, remote, this);
		}

		/// <summary>
		/// Returns a string that represents the current instance.
		/// </summary>
		/// <returns>A String that represents the current instance.</returns>
		public override string ToString()
		{
			return string.Format("KeyProvider_SspiClient Features: {0}; Package: {1}; Target name: {2}; Delegated context: {3}.", 
				Enum.Format(typeof(SspiFeatureFlags), this.RequiredFeatures, "g"), this.PackageName, this.ServerName,
				this.DelegatedContext == null ? "<not specified>" : "<specified!>");
		}
	}
}
