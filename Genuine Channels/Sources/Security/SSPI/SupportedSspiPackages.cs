/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

namespace Belikov.GenuineChannels.Security.SSPI
{
	/// <summary>
	/// Enumerates all SSPI packages supported by Genuine Channels.
	/// </summary>
	public enum SupportedSspiPackages
	{
		/// <summary>
		/// If the application specifies Negotiate, Negotiate analyzes the request and picks 
		/// the best SSP to handle the request based on customer-configured security policy.
		/// </summary>
		Negotiate,

		/// <summary>
		/// Windows NT Challenge/Response (NTLM) is the authentication protocol used on
		/// networks that include systems running the Windows NT operating system and on
		/// stand-alone systems.
		/// </summary>
		NTLM,

		/// <summary>
		/// The Kerberos protocol defines how clients interact with a network authentication
		/// service. Clients obtain tickets from the Kerberos Key Distribution Center (KDC), 
		/// and they present these tickets to servers when connections are established. 
		/// </summary>
		Kerberos,
	}
}
