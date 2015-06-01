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
	/// Abstract factory that spawns security contexts.
	/// </summary>
	public interface IKeyProvider
	{
		/// <summary>
		/// Creates a Security Session.
		/// </summary>
		/// <param name="name">The name of the SecuritySession being created.</param>
		/// <param name="remote">The remote host.</param>
		/// <returns>Created Security Session.</returns>
		SecuritySession CreateSecuritySession(string name, HostInformation remote);
	}
}
