/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

namespace Belikov.GenuineChannels.Parameters
{
	/// <summary>
	/// Provides a parameter provider with default values of the parameters.
	/// </summary>
	public class DefaultParameterProvider : MarshalByRefObject, IParameterProvider
	{
		/// <summary>
		/// Initializes parameter array with default values.
		/// </summary>
		public DefaultParameterProvider()
		{
			this._readParameters = new object[(int) GenuineParameter.LastParameter + 1];

			// General settings
			this._readParameters[(int) GenuineParameter.MaxContentSize] = 20000000;
			this._readParameters[(int) GenuineParameter.MaxQueuedItems] = 100;
			this._readParameters[(int) GenuineParameter.MaxTotalSize] = 20000000;
			this._readParameters[(int) GenuineParameter.NoSizeChecking] = false;
			this._readParameters[(int) GenuineParameter.Compression] = false;

			this._readParameters[(int) GenuineParameter.InvocationTimeout] = TimeSpan.FromSeconds(120);
			this._readParameters[(int) GenuineParameter.SyncResponses] = true;

			// Common settings
			this._readParameters[(int) GenuineParameter.ConnectTimeout] = TimeSpan.FromSeconds(120);
			this._readParameters[(int) GenuineParameter.SecuritySessionForPersistentConnections] = null;
			this._readParameters[(int) GenuineParameter.SecuritySessionForNamedConnections] = null;
			this._readParameters[(int) GenuineParameter.SecuritySessionForInvocationConnections] = null;

			this._readParameters[(int) GenuineParameter.ClosePersistentConnectionAfterInactivity] = TimeSpan.FromSeconds(100);
			this._readParameters[(int) GenuineParameter.CloseNamedConnectionAfterInactivity] = TimeSpan.FromSeconds(120);
			this._readParameters[(int) GenuineParameter.CloseInvocationConnectionAfterInactivity] = TimeSpan.FromSeconds(15);
			this._readParameters[(int) GenuineParameter.CloseOneWayConnectionAfterInactivity] = TimeSpan.FromSeconds(15);

			this._readParameters[(int) GenuineParameter.PersistentConnectionSendPingAfterInactivity] = TimeSpan.FromSeconds(40);

			this._readParameters[(int) GenuineParameter.MaxTimeSpanToReconnect] = TimeSpan.FromSeconds(180);
			this._readParameters[(int) GenuineParameter.ReconnectionTries] = 180;
			this._readParameters[(int) GenuineParameter.SleepBetweenReconnections] = TimeSpan.FromMilliseconds(500);
//			this._readParameters[(int) GenuineParameter.IgnoreRemoteHostUriChanges] = false;

			// TCP parameters
			this._readParameters[(int) GenuineParameter.TcpMaxSendSize] = 64000;
			this._readParameters[(int) GenuineParameter.TcpReadRequestBeforeProcessing] = true;
			this._readParameters[(int) GenuineParameter.TcpDoNotResendMessages] = false;
			this._readParameters[(int) GenuineParameter.TcpDisableNagling] = false;
			this._readParameters[(int) GenuineParameter.TcpPreventDelayedAck] = false;
			this._readParameters[(int) GenuineParameter.TcpReceiveBufferSize] = -1;
			this._readParameters[(int) GenuineParameter.TcpSendBufferSize] = -1;
			this._readParameters[(int) GenuineParameter.TcpReuseAddressPort] = false;
			this._readParameters[(int) GenuineParameter.TcpDualSocketMode] = true;

			// Shared memory
			this._readParameters[(int) GenuineParameter.SMShareSize] = 300000;
			this._readParameters[(int) GenuineParameter.SMSendTimeout] = TimeSpan.FromMilliseconds(4000);
			this._readParameters[(int) GenuineParameter.SMSessionLocal] = false;

			// UDP
			this._readParameters[(int) GenuineParameter.UdpReceiveBuffer] = 64000;
			this._readParameters[(int) GenuineParameter.UdpPacketSize] = 450;
			this._readParameters[(int) GenuineParameter.UdpMtu] = 4500;
			this._readParameters[(int) GenuineParameter.UdpAssembleTimeSpan] = TimeSpan.FromMinutes(3);

			// HTTP
			this._readParameters[(int) GenuineParameter.HttpUseGlobalProxy] = false;
			this._readParameters[(int) GenuineParameter.HttpBypassOnLocal] = false;
			this._readParameters[(int) GenuineParameter.HttpUseDefaultCredentials] = false;
			this._readParameters[(int) GenuineParameter.HttpAllowWriteStreamBuffering] = false;
			this._readParameters[(int) GenuineParameter.HttpUnsafeConnectionSharing] = false;
			this._readParameters[(int) GenuineParameter.HttpRecommendedPacketSize] = 100000;
			this._readParameters[(int) GenuineParameter.HttpKeepAlive] = true;
			this._readParameters[(int) GenuineParameter.HttpAuthentication] = false;
			this._readParameters[(int) GenuineParameter.HttpStoreAndProvideHttpContext] = false;
			this._readParameters[(int) GenuineParameter.HttpWebRequestInitiationTimeout] = TimeSpan.FromSeconds(15);
			this._readParameters[(int) GenuineParameter.HttpAsynchronousRequestTimeout] = TimeSpan.FromSeconds(180);

			// XHTTP
			this._readParameters[(int) GenuineParameter.XHttpReadHttpMessageTimeout] = TimeSpan.FromSeconds(60);

			// Security settings
			this._readParameters[(int) GenuineParameter.HoldThreadDuringSecuritySessionEstablishing] = true;

			// Logging
			this._readParameters[(int) GenuineParameter.EnableGlobalLoggingToMemory] = 0;
			this._readParameters[(int) GenuineParameter.EnableGlobalLoggingToFile] = string.Empty;
			this._readParameters[(int) GenuineParameter.LoggingParameters] = Logbook.GenuineLoggingServices.DefaultLoggingOptions;

			// Versioning
			this._readParameters[(int) GenuineParameter.CompatibilityLevel] = 1;
		}

		/// <summary>
		/// Read parameters.
		/// </summary>
		private object[] _readParameters;

		/// <summary>
		/// Gets a parameter's value.
		/// </summary>
		/// <param name="genuineParameter">The name of the parameter.</param>
		/// <returns>The value of the parameter.</returns>
		public object this[GenuineParameter genuineParameter]
		{ 
			get
			{
				return this._readParameters[(int) genuineParameter];
			}
			set
			{
				this._readParameters[(int) genuineParameter] = value;
			}
		}
	}
}
