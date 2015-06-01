/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.Connection
{
	/// <summary>
	/// Controls the state of the specific connection and fires Genuine Channels events when
	/// the state is changed.
	/// </summary>
	internal class ConnectionStateSignaller
	{
		/// <summary>
		/// Constructs an instance of the ConnectionSignaller class.
		/// </summary>
		/// <param name="remote">The remote host.</param>
		/// <param name="iGenuineEventProvider">The event provider.</param>
		public ConnectionStateSignaller(HostInformation remote, IGenuineEventProvider iGenuineEventProvider)
		{
			this._remote = remote;
			this._iGenuineEventProvider = iGenuineEventProvider;
			this._currentState = GenuineEventType.GeneralConnectionIndeterminate;
		}

		private HostInformation _remote;
		private IGenuineEventProvider _iGenuineEventProvider;
		private GenuineEventType _currentState;

		/// <summary>
		/// Sets the state of the persistent connection to the specific remote host.
		/// </summary>
		/// <param name="genuineEventType">The state of the connection.</param>
		/// <param name="reason">The exception.</param>
		/// <param name="additionalInfo">The additional info.</param>
		public void SetState(GenuineEventType genuineEventType, Exception reason, object additionalInfo)
		{
			lock (this)
			{
				if (this._currentState == genuineEventType)
					return ;

				this._currentState = genuineEventType;
			}

			this._iGenuineEventProvider.Fire(new GenuineEventArgs(genuineEventType, reason, this._remote, additionalInfo));
		}
	}
}
