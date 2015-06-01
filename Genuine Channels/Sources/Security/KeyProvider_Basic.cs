/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.Security
{
	/// <summary>
	/// Establishes a simple connection without security context.
	/// </summary>
	public class KeyProvider_Basic : IKeyProvider
	{
		/// <summary>
		/// Constructs an instance of the KeyProvider_Basic class.
		/// </summary>
		public KeyProvider_Basic()
		{
		}

		/// <summary>
		/// Creates SecuritySession which will perform all traffic processing in
		/// specific security context.
		/// </summary>
		/// <param name="name">Name of the SecuritySession being created.</param>
		/// <param name="remote">The remote host.</param>
		/// <returns>SecuritySession that will perform all traffic processing that is performed in specific security context.</returns>
		public SecuritySession CreateSecuritySession(string name, HostInformation remote)
		{
			return new SecuritySession_Basic(name);
		}

		/// <summary>
		/// Returns a string that represents the current instance.
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return "KeyProvider_Basic. No security features provided";
		}
	}
}
