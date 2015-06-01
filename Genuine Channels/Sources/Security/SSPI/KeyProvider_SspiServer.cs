/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.Security.SSPI
{
	/// <summary>
	/// Spawns SecuritySession_SspiServer sessions that use
	/// SSPI API to provide authentication, encryption, content signing and other security features.
	/// </summary>
	public class KeyProvider_SspiServer : IKeyProvider
	{
		/// <summary>
		/// Constructs an instance of the KeyProvider_SspiServer class.
		/// </summary>
		/// <param name="requiredFeatures">SSPI features that will be provided by Security Sessions.</param>
		/// <param name="packageName">The name of the used SSPI package.</param>
		public KeyProvider_SspiServer(SspiFeatureFlags requiredFeatures, SupportedSspiPackages packageName)
		{
			this.RequiredFeatures = requiredFeatures;
			this.PackageName = Enum.Format(typeof(SupportedSspiPackages), packageName, "f");
		}

		/// <summary>
		/// SSPI features provided by Security Sessions.
		/// </summary>
		public SspiFeatureFlags RequiredFeatures;

		/// <summary>
		/// The name of the used SSPI package.
		/// </summary>
		public string PackageName;

		/// <summary>
		/// Creates a Security Session.
		/// </summary>
		/// <param name="name">The name of the created SecuritySession.</param>
		/// <param name="remote">The remote host.</param>
		/// <returns>Created Security Session.</returns>
		public SecuritySession CreateSecuritySession(string name, HostInformation remote)
		{
			return new SecuritySession_SspiServer(name, remote, this);
		}
	}
}
