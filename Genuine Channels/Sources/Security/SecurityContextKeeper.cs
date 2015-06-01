/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

namespace Belikov.GenuineChannels.Security
{
	/// <summary>
	/// SecurityContextKeeper forces the specified security context to be used during all calls in
	/// the current thread. SecurityContextKeeper automatically restores the previous security 
	/// context when the Disposed method is called.
	/// </summary>
	public struct SecurityContextKeeper : IDisposable
	{
		/// <summary>
		/// Forces the specified security context to be used in all calls in
		/// the current thread until Dispose method is called.
		/// </summary>
		/// <param name="securitySessionParameters">Security Session parameters.</param>
		public SecurityContextKeeper(SecuritySessionParameters securitySessionParameters)
		{
			this._previousSecuritySessionParameters = SecuritySessionServices.SetCurrentSecurityContext(securitySessionParameters);
		}

		private SecuritySessionParameters _previousSecuritySessionParameters;

		/// <summary>
		/// Restores security context.
		/// </summary>
		public void Dispose()
		{
			SecuritySessionServices.SetCurrentSecurityContext(this._previousSecuritySessionParameters);
		}
	}
}
