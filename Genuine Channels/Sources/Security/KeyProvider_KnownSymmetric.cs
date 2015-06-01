/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Security;
using System.Security.Cryptography;

using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.Security
{
	/// <summary>
	/// KeyProvider_KnownSymmetric spawns security contexts that use given symmetric
	/// cryptographic algorithm for traffic encryption.
	/// </summary>
	public class KeyProvider_KnownSymmetric : IKeyProvider
	{
		/// <summary>
		/// Initializes an instance of the KeyProvider_KnownSymmetric class.
		/// </summary>
		/// <param name="symmetricAlgorithm">SymmetricAlgorithm to be used.</param>
		public KeyProvider_KnownSymmetric(SymmetricAlgorithm symmetricAlgorithm)
		{
			this.SymmetricAlgorithm = symmetricAlgorithm;
		}

		/// <summary>
		/// SymmetricAlgorithm that will be used during traffic encryption.
		/// </summary>
		public SymmetricAlgorithm SymmetricAlgorithm;

		/// <summary>
		/// Creates SecuritySession which will perform all traffic processing in
		/// the specific security context.
		/// </summary>
		/// <param name="name">The name of the SecuritySession being created.</param>
		/// <param name="remote">The remote host.</param>
		/// <returns>SecuritySession that will perform all traffic processing that is performed in specific security context.</returns>
		public SecuritySession CreateSecuritySession(string name, HostInformation remote)
		{
			return new SecuritySession_KnownSymmetric(this.SymmetricAlgorithm, name);
		}

		/// <summary>
		/// Returns a string that represents the current instance.
		/// </summary>
		/// <returns>A String that represents the current instance.</returns>
		public override string ToString()
		{
			return string.Format("KeyProvider_KnownSymmetric. Symmetric algorithm: {0}. Key size: {1}", 
				this.SymmetricAlgorithm.GetType().Name, this.SymmetricAlgorithm.KeySize);
		}
	}
}
