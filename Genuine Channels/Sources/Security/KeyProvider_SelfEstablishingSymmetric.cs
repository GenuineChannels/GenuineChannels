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
	/// Represents a Key Provider, which spawns SecuritySession_SelfEstablishingSymmetric Security Sessions.
	/// </summary>
	public class KeyProvider_SelfEstablishingSymmetric : IKeyProvider
	{
		/// <summary>
		/// Constructs an instance of the KeyProvider_SelfEstablishingSymmetric class.
		/// </summary>
		public KeyProvider_SelfEstablishingSymmetric()
		{
		}

		/// <summary>
		/// Creates SecuritySession which will perform all traffic processing in
		/// specific security context.
		/// </summary>
		/// <param name="name">The name of the SecuritySession being created.</param>
		/// <param name="remote">The remote host.</param>
		/// <returns>SecuritySession that will perform all traffic processing that is performed in specific security context.</returns>
		public SecuritySession CreateSecuritySession(string name, HostInformation remote)
		{
			return new SecuritySession_SelfEstablishingSymmetric(name, remote);
		}

		/// <summary>
		/// Returns a string that represents the current instance.
		/// </summary>
		/// <returns>A String that represents the current instance.</returns>
		public override string ToString()
		{
			return string.Format("KeyProvider_SelfEstablishingSymmetric.");
		}
	}
}
