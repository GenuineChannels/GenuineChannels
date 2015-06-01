/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.Diagnostics;
using System.Net;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

using Belikov.Common.ThreadProcessing;

using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Parameters;
using Belikov.GenuineChannels.Security;
using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.Connection
{
	/// <summary>
	/// Represents a connection to the remote host.
	/// </summary>
	internal abstract class GeneralConnection
	{
		/// <summary>
		/// Constructs an instance of the GeneralConnection class.
		/// </summary>
		/// <param name="iTransportContext">The Transport Context.</param>
		public GeneralConnection(ITransportContext iTransportContext)
		{
			this.ITransportContext = iTransportContext;
			this.HostIdAsString = iTransportContext.HostIdentifier;
		}

		/// <summary>
		/// A queue of the messages arranged to be sent through this connection.
		/// </summary>
		public MessageContainer MessageContainer;

		/// <summary>
		/// The connection id in string format.
		/// </summary>
		public string HostIdAsString;

		/// <summary>
		/// The remote host.
		/// </summary>
		public HostInformation Remote;

		/// <summary>
		/// The transport context.
		/// </summary>
		public ITransportContext ITransportContext;

		/// <summary>
		/// The type of this connection.
		/// </summary>
		public GenuineConnectionType GenuineConnectionType;

		/// <summary>
		/// To guarantee atomic access to local members.
		/// </summary>
		protected object _accessToLocalMembers = new object();

		/// <summary>
		/// Connection Manager must obtain the lock for this object in order to modify data
		/// related to reconnection stuff.
		/// </summary>
		public object ReconnectionLock = new object();

		/// <summary>
		/// A 32-bit signed integer containing the amount of time in milliseconds that has passed since the last sending of a message to the remote host.
		/// </summary>
		public int LastTimeContentWasSent
		{
			get
			{
				lock (_accessToLocalMembers)
					return _lastTimeContentWasSent;
			}
			set
			{
				lock (_accessToLocalMembers)
					_lastTimeContentWasSent = value;
			}
		}
		private int _lastTimeContentWasSent = GenuineUtility.TickCount;

		#region -- Disposing -----------------------------------------------------------------------

		/// <summary>
		/// Indicates whether this instance was disposed.
		/// </summary>
		internal bool IsDisposed
		{
			get
			{
				using (new ReaderAutoLocker(this.DisposeLock))
					return this.__disposed;
			}
			set
			{
				using (new WriterAutoLocker(this.DisposeLock))
					this.__disposed = value;
			}
		}
		private bool __disposed = false;

		/// <summary>
		/// The reason of the disposing.
		/// </summary>
		internal Exception _disposeReason = null;
		
		/// <summary>
		/// Dispose lock.
		/// </summary>
		internal ReaderWriterLock DisposeLock = new ReaderWriterLock();

		/// <summary>
		/// Releases all resources.
		/// </summary>
		/// <param name="reason">The reason of disposing.</param>
		public void Dispose(Exception reason)
		{
			if (this.IsDisposed)
				return ;

			if (reason == null)
				reason = GenuineExceptions.Get_Processing_TransportConnectionFailed();

			// stop the processing
			using (new WriterAutoLocker(this.DisposeLock))
			{
				if (this.IsDisposed)
					return ;

				this.IsDisposed = true;
				this._disposeReason = reason;
			}

			InternalDispose(reason);
		}

		/// <summary>
		/// Releases resources.
		/// </summary>
		/// <param name="reason">The reason of disposing.</param>
		public abstract void InternalDispose(Exception reason);

		#endregion

		#region -- Signalling ----------------------------------------------------------------------

		/// <summary>
		/// The state controller.
		/// </summary>
		private ConnectionStateSignaller _connectionStateSignaller;

		/// <summary>
		/// The state controller lock.
		/// </summary>
		private object _connectionStateSignallerLock = new object();

		/// <summary>
		/// Sets the state of the connection.
		/// </summary>
		/// <param name="genuineEventType">The state of the connection.</param>
		/// <param name="reason">The exception.</param>
		/// <param name="additionalInfo">The additional info.</param>
		public void SignalState(GenuineEventType genuineEventType, Exception reason, object additionalInfo)
		{
			lock (this._connectionStateSignallerLock)
			{
				if (this._connectionStateSignaller == null)
					this._connectionStateSignaller = new ConnectionStateSignaller(this.Remote, this.ITransportContext.IGenuineEventProvider);

				this._connectionStateSignaller.SetState(genuineEventType, reason, additionalInfo);
			}
		}

		#endregion

		#region -- Renewing ------------------------------------------------------------------------

		/// <summary>
		/// The time span to close persistent connection after this period of inactivity.
		/// </summary>
		public int CloseConnectionAfterInactivity;

		/// <summary>
		/// The time after which the socket will be shut down automatically, or a DateTime.MaxValue value.
		/// </summary>
		public int ShutdownTime
		{
			get
			{
				lock (this._accessToLocalMembers)
					return this._shutdownTime;
			}
		}
		private int _shutdownTime = GenuineUtility.FurthestFuture;

		/// <summary>
		/// Renews socket activity for the CloseConnectionAfterInactivity value.
		/// </summary>
		public void Renew()
		{
			lock (this._accessToLocalMembers)
				this._shutdownTime = GenuineUtility.GetTimeout(this.CloseConnectionAfterInactivity);
			this.Remote.Renew(this.CloseConnectionAfterInactivity, false);
		}

		#endregion

		#region -- Establishing --------------------------------------------------------------------


		/// <summary>
		/// Establishes Connection Level Security Session and gather their output into specified stream.
		/// </summary>
		/// <param name="senderInput">The input stream.</param>
		/// <param name="listenerInput">The input stream.</param>
		/// <param name="output">The output stream.</param>
		/// <param name="sender">The sender's Security Session.</param>
		/// <param name="listener">The listener's Security Session.</param>
		/// <returns>True if at least one Security Session requested sending of data.</returns>
		public bool GatherContentOfConnectionLevelSecuritySessions(Stream senderInput, Stream listenerInput, GenuineChunkedStream output, SecuritySession sender, SecuritySession listener)
		{
			bool clssDataPresents = false;
			Stream clsseStream;

			// CLSSE info
			using (new GenuineChunkedStreamSizeLabel(output))
			{
				if (sender != null && ! sender.IsEstablished)
				{
					clsseStream = sender.EstablishSession(senderInput, true);
					if (clsseStream != null)
					{
						clssDataPresents = true;
						GenuineUtility.CopyStreamToStream(clsseStream, output);
					}
				}
			}

			// CLSSE info
			using (new GenuineChunkedStreamSizeLabel(output))
			{
				if (listener != null && ! listener.IsEstablished)
				{
					clsseStream = listener.EstablishSession(listenerInput, true);
					if (clsseStream != null)
					{
						clssDataPresents = true;
						GenuineUtility.CopyStreamToStream(clsseStream, output);
					}
				}
			}

			return clssDataPresents;
		}

		#endregion
	}
}
