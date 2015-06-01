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

namespace Belikov.GenuineChannels.GenuineHttp
{
	/// <summary>
	/// Implements GHTTP server connection behavior (P/*).
	/// </summary>
	internal class HttpServerConnection
	{
		/// <summary>
		/// Constructs an instance of the HttpServerConnection class.
		/// </summary>
		/// <param name="iTransportContext">The Transport Context.</param>
		/// <param name="hostId">The id of the remote host.</param>
		/// <param name="remote">The remote host.</param>
		/// <param name="connectionName">The name of the connection.</param>
		/// <param name="closeConnectionAfterInactivity">The time span to renew a connection after each message.</param>
		public HttpServerConnection(ITransportContext iTransportContext, Guid hostId, HostInformation remote, string connectionName, int closeConnectionAfterInactivity)
		{
			this.ITransportContext = iTransportContext;
			this.HttpServerConnectionManager = (HttpServerConnectionManager) iTransportContext.ConnectionManager;
			this.ConnectionName = connectionName;
			this.CloseConnectionAfterInactivity = closeConnectionAfterInactivity;

			this.HostId = hostId.ToByteArray();
			this.HostIdAsString = hostId.ToString("N");
			this.Listener_MessageContainer = new MessageContainer(iTransportContext);

			this.Remote = remote;
		}

		/// <summary>
		/// To guarantee atomic access to local members.
		/// </summary>
		private object _accessToLocalMembers = new object();

		/// <summary>
		/// The connection manager.
		/// </summary>
		public HttpServerConnectionManager HttpServerConnectionManager;

		/// <summary>
		/// The connection id.
		/// </summary>
		public byte[] HostId;

		/// <summary>
		/// The identifier of the remote host in a string format.
		/// </summary>
		public string HostIdAsString;

		/// <summary>
		/// The transport context.
		/// </summary>
		public ITransportContext ITransportContext;

		/// <summary>
		/// The remote host.
		/// </summary>
		public HostInformation Remote;

		/// <summary>
		/// The name of the connection.
		/// </summary>
		public string ConnectionName;

		/// <summary>
		/// The unique connection identifier, which is used for debugging purposes only.
		/// </summary>
		public int DbgConnectionId = ConnectionManager.GetUniqueConnectionId();

		#region -- Sender --------------------------------------------------------------------------

		/// <summary>
		/// The current sequence number;
		/// </summary>
		public int Sender_SequenceNo = 0;

		#endregion

		#region -- Listener ------------------------------------------------------------------------

		/// <summary>
		/// The current sequence number;
		/// </summary>
		public int Listener_SequenceNo = 0;

		/// <summary>
		/// The current stream being sent.
		/// </summary>
		public Stream Listener_SentStream;

		/// <summary>
		/// Connection manager must obtain a lock for this object to modify any
		/// information regarding the listener connection.
		/// </summary>
		public object Listener_Lock = new object();

		/// <summary>
		/// The request.
		/// </summary>
		public HttpServerRequestResult Listener;

		/// <summary>
		/// The message container.
		/// </summary>
		public MessageContainer Listener_MessageContainer;

		/// <summary>
		/// Contains all messages currently being sent to the remote host.
		/// </summary>
		public ArrayList Listener_MessagesBeingSent = new ArrayList();

		/// <summary>
		/// The intermediate buffer.
		/// </summary>
		public byte[] Listener_IntermediateBuffer = new byte[35000];

		/// <summary>
		/// The time when a listener request was received.
		/// </summary>
		public int Listener_Opened = GenuineUtility.TickCount;

		#endregion

		#region -- Security ------------------------------------------------------------------------

		/// <summary>
		/// CLSS.
		/// </summary>
		public SecuritySession Sender_SecuritySession;

		/// <summary>
		/// CLSS.
		/// </summary>
		public SecuritySession Listener_SecuritySession;

		#endregion

		#region -- IDisposable Members -------------------------------------------------------------

		/// <summary>
		/// Indicates whether this instance was disposed.
		/// </summary>
		internal bool _disposed
		{
			get
			{
				lock (this.__disposedLock)
					return this.__disposed;
			}
			set
			{
				lock (this.__disposedLock)
					this.__disposed = value;
			}
		}
		private bool __disposed = false;
		private object __disposedLock = new object();

		/// <summary>
		/// The reason this shell was disposed due to.
		/// </summary>
		internal Exception _disposeReason = null;
		
		/// <summary>
		/// Dispose lock.
		/// </summary>
		internal ReaderWriterLock _disposeLock = new ReaderWriterLock();

		/// <summary>
		/// Releases all resources.
		/// </summary>
		/// <param name="reason">The reason of disposing.</param>
		public void Dispose(Exception reason)
		{
			if (this._disposed)
				return ;

			if (reason == null)
				reason = GenuineExceptions.Get_Processing_TransportConnectionFailed();

			// stop all processing
			using(new WriterAutoLocker(this._disposeLock))
			{
				if (this._disposed)
					return ;

				_disposed = true;
				this._disposeReason = reason;
			}
		}

		#endregion

		#region -- Connection state ----------------------------------------------------------------

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

		#region -- Lifetime ------------------------------------------------------------------------

		/// <summary>
		/// The time span to close invocation connection after this period of inactivity.
		/// </summary>
		public int CloseConnectionAfterInactivity;

		/// <summary>
		/// The time after which the socket will be shut down automatically.
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
		/// Renews the connection lifetime.
		/// </summary>
		public void Renew()
		{
			lock (this._accessToLocalMembers)
				this._shutdownTime = GenuineUtility.GetTimeout(this.CloseConnectionAfterInactivity);
			this.Remote.Renew(this.CloseConnectionAfterInactivity, false);
		}

		#endregion

	}
}
