/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.Security.ZeroProofAuthorization
{
	/// <summary>
	/// Implements a server-side Zero Proof Authorization Key Provider.
	/// </summary>
	public class KeyProvider_ZpaServer : IKeyProvider
	{
		/// <summary>
		/// Constructs an instance of the KeyProvider_ZpaServer class.
		/// </summary>
		/// <param name="zpaFeatureFlags">The requested security options.</param>
		/// <param name="iAuthorizationManager">The password provider.</param>
		public KeyProvider_ZpaServer(ZpaFeatureFlags zpaFeatureFlags, IAuthorizationManager iAuthorizationManager)
		{
			this._zpaFeatureFlags = zpaFeatureFlags;
			this._iAuthorizationManager = iAuthorizationManager;
		}

		/// <summary>
		/// To synchronize access to the local members.
		/// </summary>
		private object _accessToLocalMembers = new object();

		/// <summary>
		/// The requested security options.
		/// </summary>
		public ZpaFeatureFlags ZpaFeatureFlags
		{
			get
			{
				lock (this._accessToLocalMembers)
					return this._zpaFeatureFlags ;
			}
			set
			{
				lock (this._accessToLocalMembers)
					this._zpaFeatureFlags  = value;
			}
		}
		private ZpaFeatureFlags _zpaFeatureFlags;

		/// <summary>
		/// The password provider.
		/// </summary>
		public IAuthorizationManager IAuthorizationManager
		{
			get
			{
				lock (this._accessToLocalMembers)
					return this._iAuthorizationManager;
			}
			set
			{
				lock (this._accessToLocalMembers)
					this._iAuthorizationManager = value;
			}
		}
		private IAuthorizationManager _iAuthorizationManager;

		/// <summary>
		/// Creates a Security Session.
		/// </summary>
		/// <param name="name">Name of the SecuritySession being created.</param>
		/// <param name="remote">The remote host.</param>
		/// <returns>Created Security Session.</returns>
		public SecuritySession CreateSecuritySession(string name, HostInformation remote)
		{
			return new SecuritySession_ZpaServer(name, remote, this);
		}

		/// <summary>
		/// Returns a string that represents the current instance.
		/// </summary>
		/// <returns>A String that represents the current instance.</returns>
		public override string ToString()
		{
			return string.Format("KeyProvider_ZpaServer Features: {0}.", Enum.Format(typeof(ZpaFeatureFlags), this.ZpaFeatureFlags, "g"));
		}

	}
}
