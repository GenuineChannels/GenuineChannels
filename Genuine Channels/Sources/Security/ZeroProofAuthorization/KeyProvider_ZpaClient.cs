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

namespace Belikov.GenuineChannels.Security.ZeroProofAuthorization
{
	/// <summary>
	/// Implements a Key Provider containing all the necessary information for establishing
	/// SecuritySession_ZpaClient Security Session.
	/// </summary>
	public class KeyProvider_ZpaClient : IKeyProvider
	{
		/// <summary>
		/// Constructs an instance of the KeyProvider_ZpaClient class.
		/// </summary>
		/// <param name="zpaFeatureFlags">The requested security options.</param>
		/// <param name="login">The login.</param>
		/// <param name="password">The password.</param>
		public KeyProvider_ZpaClient(ZpaFeatureFlags zpaFeatureFlags, object login, string password)
		{
			this._zpaFeatureFlags = zpaFeatureFlags;
			this._login = login;
			this._password = password;
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
					return this._zpaFeatureFlags;
			}
			set
			{
				lock (this._accessToLocalMembers)
					this._zpaFeatureFlags = value;
			}
		}
		private ZpaFeatureFlags _zpaFeatureFlags;

		/// <summary>
		/// The login.
		/// </summary>
		public object Login
		{
			get
			{
				lock (this._accessToLocalMembers)
					return this._login;
			}
			set
			{
				lock (this._accessToLocalMembers)
					this._login = value;
			}
		}
		private object _login;

		/// <summary>
		/// The password.
		/// </summary>
		public string Password
		{
			get
			{
				lock (this._accessToLocalMembers)
					return this._password;
			}
			set
			{
				lock (this._accessToLocalMembers)
					this._password = value;
			}
		}
		private string _password;

		/// <summary>
		/// Creates a Security Session.
		/// </summary>
		/// <param name="name">Name of the SecuritySession being created.</param>
		/// <param name="remote">The remote host.</param>
		/// <returns>Created Security Session.</returns>
		public SecuritySession CreateSecuritySession(string name, HostInformation remote)
		{
			return new SecuritySession_ZpaClient(name, remote, this);
		}

		/// <summary>
		/// Returns a string that represents the current instance.
		/// </summary>
		/// <returns>A String that represents the current instance.</returns>
		public override string ToString()
		{
			return string.Format("KeyProvider_ZpaClient Features: {0}.", Enum.Format(typeof(ZpaFeatureFlags), this.ZpaFeatureFlags, "g"));
		}

	}
}
