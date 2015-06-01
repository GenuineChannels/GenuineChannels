/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Channels;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;

using Belikov.Common.ThreadProcessing;
using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Parameters;
using Belikov.GenuineChannels.Receiving;
using Belikov.GenuineChannels.Security;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Utilities;

namespace Belikov.GenuineChannels.GenuineSharedMemory
{
	/// <summary>
	/// Implement connection manager logic for Shared memory transport.
	/// </summary>
	internal class SharedMemoryConnectionManager : ConnectionManager, ITimerConsumer
	{
		/// <summary>
		/// Constructs an instance of the SharedMemoryConnectionManager class.
		/// </summary>
		/// <param name="iTransportContext">The transport context.</param>
		public SharedMemoryConnectionManager(ITransportContext iTransportContext) : base(iTransportContext)
		{
			this._sendTimeoutSpan = GenuineUtility.ConvertToMilliseconds(iTransportContext.IParameterProvider[GenuineParameter.SMSendTimeout]);

			this.Local = new HostInformation("_gshmem://" + iTransportContext.HostIdentifier, iTransportContext);
			TimerProvider.Attach(this);
		}

		/// <summary>
		/// Sends the message to the remote host.
		/// Returns a response if the corresponding Security Session is established and the initial message is not one-way.
		/// </summary>
		/// <param name="message">Message to be sent or a null reference (if there is a queued message).</param>
		protected override void InternalSend(Message message)
		{
			SharedMemoryConnection sharedMemoryConnection = null;
			string uri = message.Recipient.Url;
			bool isServer = false;

			// get the connection
			lock (message.Recipient.PersistentConnectionEstablishingLock)
			{
				switch (message.Recipient.GenuinePersistentConnectionState)
				{
					case GenuinePersistentConnectionState.NotEstablished:
						if (message.Recipient.Url == null)
							throw GenuineExceptions.Get_Send_DestinationIsUnreachable(message.Recipient.Uri);
						message.Recipient.GenuinePersistentConnectionState = GenuinePersistentConnectionState.Opened;
						break;

					case GenuinePersistentConnectionState.Accepted:
						isServer = true;
						uri = message.Recipient.Uri;
						break;
				}

				// if it's possible to establish a connection to the remote host
				if (! isServer)
				{
					sharedMemoryConnection = this._persistent[uri] as SharedMemoryConnection;

					if (sharedMemoryConnection == null)
					{
						// try to establish a persistent connection
						string remoteUri;
						int remoteHostUniqueIdentifier;
						sharedMemoryConnection = this.LowLevel_OpenConnection(message.Recipient, this.Local.Uri, out remoteUri, out remoteHostUniqueIdentifier);

						// update remote host info
						message.Recipient.UpdateUri(remoteUri, remoteHostUniqueIdentifier);
						sharedMemoryConnection.Remote = message.Recipient;
						sharedMemoryConnection.SignalState(GenuineEventType.GeneralConnectionEstablished, null, null);

						// OK, connection established
						this._persistent[uri] = sharedMemoryConnection;
						this.Connection_InitiateReceiving(sharedMemoryConnection);
					}
				}
				else
				{
					// remote host is a client and if there is no connection to it, it's unreachable
					sharedMemoryConnection = this._persistent[uri] as SharedMemoryConnection;
					if (sharedMemoryConnection == null)
						throw GenuineExceptions.Get_Send_DestinationIsUnreachable(message.Recipient.Uri);
				}
			}

			this.SendSync(message, sharedMemoryConnection);
		}

		/// <summary>
		/// Sends the message through the specified connection.
		/// </summary>
		/// <param name="message">The message.</param>
		/// <param name="sharedMemoryConnection">The connection.</param>
		private void SendSync(Message message, SharedMemoryConnection sharedMemoryConnection)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			try
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "SharedMemoryConnectionManager.SendSync",
						LogMessageType.MessageIsSentSynchronously, null, message, sharedMemoryConnection.Remote, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						sharedMemoryConnection.ConnectionLevelSecurity, 
						sharedMemoryConnection.ConnectionLevelSecurity == null ? null : sharedMemoryConnection.ConnectionLevelSecurity.Name, 
						sharedMemoryConnection.DbgConnectionId, 0, 0, 0, null, null, null, null,
						"Message is sent synchronously through the Shared Memory connection.");
				}

				// now that we have the connection, obtain the write access
				int sendTimeout = GenuineUtility.GetTimeout(this._sendTimeoutSpan);
				if (GenuineUtility.IsTimeoutExpired(message.FinishTime, sendTimeout))
					sendTimeout = message.FinishTime;

				try
				{
					if (! Monitor.TryEnter(sharedMemoryConnection.WriteAccess, GenuineUtility.GetMillisecondsLeft(sendTimeout)) )
						throw GenuineExceptions.Get_Send_Timeout();
				}
				catch
				{
					throw GenuineExceptions.Get_Send_Timeout();
				}

				// the write access was obtained, send the message
				try
				{
					sharedMemoryConnection.LowLevel_SendSync(message.SerializedContent, sendTimeout);
				}
				catch(Exception ex)
				{
					this.ConnectionFailed(ex, sharedMemoryConnection);
					throw;
				}
				finally
				{
					Monitor.Exit(sharedMemoryConnection.WriteAccess);
				}

				message.Dispose();
			}
			catch (Exception ex)
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "SharedMemoryConnectionManager.SendSync",
						LogMessageType.MessageIsSentSynchronously, ex, message, sharedMemoryConnection.Remote, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, sharedMemoryConnection.DbgConnectionId, 0, 0, 0, null, null, null, null,
						"The message cannot be delivered.");
				}

				throw;
			}
			finally
			{
				message.Dispose();
			}
		}

		private int _sendTimeoutSpan;

		/// <summary>
		/// Connection primary URL => Shared connection.
		/// </summary>
		private Hashtable _persistent = Hashtable.Synchronized(new Hashtable());

		/// <summary>
		/// The listening closure.
		/// </summary>
		private SMAcceptConnectionClosure _smAcceptConnectionClosure;

		/// <summary>
		/// Starts listening to the specified end point and accepting incoming connections.
		/// </summary>
		/// <param name="endPoint">The end point.</param>
		public override void StartListening(object endPoint)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			if (this._smAcceptConnectionClosure != null)
				throw GenuineExceptions.Get_Server_EndPointIsAlreadyBeingListenedTo(this._smAcceptConnectionClosure.ShareName);
			SharedMemoryConnection sharedMemoryConnection = null;

			using (new ReaderAutoLocker(this._disposeLock))
			{
				if (this._disposed)
					throw OperationException.WrapException(this._disposeReason);
			}

			string shareName = endPoint as string;
			if (shareName == null || shareName.Length <= 0 || ! shareName.StartsWith("gshmem"))
				throw GenuineExceptions.Get_Server_IncorrectAddressToListen(shareName);

			try
			{
				sharedMemoryConnection = new SharedMemoryConnection(this.ITransportContext, shareName, true, true);

				this._smAcceptConnectionClosure = new SMAcceptConnectionClosure(this.ITransportContext, sharedMemoryConnection, this, shareName);
				this.ITransportContext.IGenuineEventProvider.Fire(new GenuineEventArgs(GenuineEventType.GeneralListenerStarted, null, this.Local, endPoint));

				Thread thread = new Thread(new ThreadStart(this._smAcceptConnectionClosure.AcceptConnections));
				thread.IsBackground = true;
				thread.Start();

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.AcceptingConnection, "SharedMemoryConnectionManager.StartListening",
						LogMessageType.ListeningStarted, null, null, null, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, -1, 
						0, 0, 0, shareName, null, null, null,
						"\"{0}\" is now listened.", shareName);
				}
			}
			catch(Exception ex)
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.AcceptingConnection, "SharedMemoryConnectionManager.StartListening",
						LogMessageType.ListeningStarted, ex, null, null, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, -1, 
						0, 0, 0, shareName, null, null, null,
						"Listening to \"{0}\" cannot be started.", shareName);
				}

				if (sharedMemoryConnection != null)
					sharedMemoryConnection.ReleaseUnmanagedResources();
				throw;
			}
		}

		/// <summary>
		/// Stops listening to the specified end point. Does not close any connections.
		/// </summary>
		/// <param name="endPoint">The end point</param>
		public override void StopListening(object endPoint)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			if (this._smAcceptConnectionClosure != null)
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.AcceptingConnection, "SharedMemoryConnectionManager.StopListening",
						LogMessageType.ListeningStopped, null, null, null, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, -1, 
						0, 0, 0, this._smAcceptConnectionClosure.ShareName, null, null, null,
						"\"{0}\" is not now listened.", this._smAcceptConnectionClosure.ShareName);
				}

				this._smAcceptConnectionClosure.StopListening.Set();
				this._smAcceptConnectionClosure = null;
			}
		}

		/// <summary>
		/// Closes the specified connections to the remote host and releases acquired resources.
		/// </summary>
		/// <param name="hostInformation">The host information.</param>
		/// <param name="genuineConnectionType">A value indicating what kind of connections will be affected by this operation.</param>
		/// <param name="reason">The reason of resource releasing.</param>
		public override void ReleaseConnections(HostInformation hostInformation, GenuineConnectionType genuineConnectionType, Exception reason)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			reason = GenuineExceptions.Get_Channel_ConnectionShutDown(reason);

			// LOG:
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
			{
				binaryLogWriter.WriteEvent(LogCategory.Connection, "SharedMemoryConnectionManager.ReleaseConnections",
					LogMessageType.ReleaseConnections, reason, null, hostInformation, null, 
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
					null, null, -1, 0, 0, 0, Enum.Format(typeof(GenuineConnectionType), genuineConnectionType, "g"), null, null, null,
					"Connections \"{0}\" will be terminated.", Enum.Format(typeof(GenuineConnectionType), genuineConnectionType, "g"), null);
			}

			if (hostInformation == null)
			{
				this.InternalDispose(reason);
				return ;
			}

			SharedMemoryConnection sharedMemoryConnection = null;
			if (hostInformation.Url != null)
				sharedMemoryConnection = this._persistent[hostInformation.Url] as SharedMemoryConnection;
			if (sharedMemoryConnection != null)
				this.ConnectionFailed(reason, sharedMemoryConnection);

			sharedMemoryConnection = null;
			if (hostInformation.Uri != null)
				sharedMemoryConnection = this._persistent[hostInformation.Uri] as SharedMemoryConnection;
			if (sharedMemoryConnection != null)
				this.ConnectionFailed(reason, sharedMemoryConnection);
		}

		/// <summary>
		/// Releases all resources.
		/// </summary>
		/// <param name="reason">The reason of disposing.</param>
		public override void InternalDispose(Exception reason)
		{
			object[] connections = null;

			lock (this._persistent.SyncRoot)
			{
				connections = new object[this._persistent.Count];
				this._persistent.Values.CopyTo(connections, 0);
				this._persistent.Clear();
			}

			foreach (SharedMemoryConnection sharedMemoryConnection in connections)
				this.ConnectionFailed(reason, sharedMemoryConnection);
		}

		#region -- Low level exchange --------------------------------------------------------------

		/// <summary>
		/// Opens a connection to the host specified by the url.
		/// </summary>
		/// <param name="remote">The HostInformation of the Remote Host.</param>
		/// <param name="localUri">The uri of the local host.</param>
		/// <param name="remoteUri">The uri of the remote host.</param>
		/// <param name="remoteHostUniqueIdentifier">The unique identifier of the HostInformation used by the remote host.</param>
		/// <returns>The established connection.</returns>
		private SharedMemoryConnection LowLevel_OpenConnection(HostInformation remote, string localUri, out string remoteUri, out int remoteHostUniqueIdentifier)
		{
			using (new ReaderAutoLocker(this._disposeLock))
			{
				if (this._disposed)
					throw OperationException.WrapException(this._disposeReason);
			}

			remoteUri = null;
			Stream inputStream = null;
			Stream outputStream = null;
			string url = remote.Url;

			// the maximum time during which the connection must be established
			int timeout = GenuineUtility.GetTimeout((TimeSpan) this.ITransportContext.IParameterProvider[GenuineParameter.ConnectTimeout]);

			IParameterProvider parameters = this.ITransportContext.IParameterProvider;

			string mutexName = GenuineSharedMemoryChannel.ConstructSharedObjectName(
				"MUTEX" + url, parameters);
			string clientConnected = GenuineSharedMemoryChannel.ConstructSharedObjectName(
				"CC" + url, parameters);
			string clientAccepted = GenuineSharedMemoryChannel.ConstructSharedObjectName(
				"CA" + url, parameters);

			// open the server share
			SharedMemoryConnection serverSharedMemoryConnection = new SharedMemoryConnection(this.ITransportContext, url, false, false);

			// LOG:
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
			{
				binaryLogWriter.WriteConnectionParameterEvent(LogCategory.Connection, "SharedMemoryConnectionManager.LowLevel_OpenConnection",
					LogMessageType.ConnectionParameters, null, remote, this.ITransportContext.IParameterProvider,
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, serverSharedMemoryConnection.DbgConnectionId, 
					"A Shared Memory connection is being established.");
			}

			// and create the local share
			string shareName = "gshmem://" + Guid.NewGuid().ToString("N");
			SharedMemoryConnection sharedMemoryConnection = new SharedMemoryConnection(this.ITransportContext, shareName, true, true);

			BinaryWriter connectionInformation = MessageCoder.SerializeConnectionHeader(MessageCoder.PROTOCOL_VERSION, GenuineConnectionType.Persistent, "Default");
			connectionInformation.Write(shareName);

			// let the server know that a client's share is ready
			Mutex mutex = null;
			try
			{
				mutex = WindowsAPI.OpenMutex(mutexName);

				NamedEvent _clientConnected = NamedEvent.OpenNamedEvent(clientConnected);
				NamedEvent _clientAccepted = NamedEvent.OpenNamedEvent(clientAccepted);

				if (! GenuineUtility.WaitOne(mutex, GenuineUtility.GetMillisecondsLeft(timeout)) )
					throw GenuineExceptions.Get_Connect_CanNotConnectToRemoteHost(url, "Can not acquire the lock for the global mutex.");

				// wait until server accepts this client
				_clientAccepted.ManualResetEvent.Reset();
				_clientConnected.ManualResetEvent.Set();

				// copy client's name
				serverSharedMemoryConnection.LowLevel_SendSync(connectionInformation.BaseStream, timeout);

				if (! GenuineUtility.WaitOne(_clientAccepted.ManualResetEvent, GenuineUtility.GetMillisecondsLeft(timeout)) )
					throw GenuineExceptions.Get_Connect_CanNotConnectToRemoteHost(url, "Remote server did not accept a request within the specified time span.");
			}
			finally
			{
				if (mutex != null)
				{
					try
					{
						mutex.ReleaseMutex();
					}
					catch
					{
					}

					try
					{
						mutex.Close();
					}
					catch
					{
					}
				}
			}

			// get the connection-level Security Session
			string connectionLevelSSName = this.ITransportContext.IParameterProvider[GenuineParameter.SecuritySessionForPersistentConnections] as string;
			SecuritySession securitySession = null;
			if (connectionLevelSSName != null)
				securitySession = this.ITransportContext.IKeyStore.GetKey(connectionLevelSSName).CreateSecuritySession(connectionLevelSSName, null);

			// establish it
			if (securitySession != null && ! securitySession.IsEstablished)
			{
				bool firstPass = true;
				for ( ; ; )
				{
					inputStream = Stream.Null;

					try
					{
						// prepare streams
						if (! firstPass)
							inputStream = sharedMemoryConnection.LowLevel_ReadSync(timeout);
						else
							firstPass = false;

						outputStream = securitySession.EstablishSession(inputStream, true);

						if (outputStream == null)
							break;

						// send a packet to the remote host
						sharedMemoryConnection.LowLevel_SendSync(outputStream, timeout);
						if (securitySession.IsEstablished)
							break;
					}
					finally
					{
						if (inputStream != null)
							inputStream.Close();
						if (outputStream != null)
							outputStream.Close();
					}
				}
			}

			sharedMemoryConnection.ConnectionLevelSecurity = securitySession;

			// now send connection info through the established connection
			using (GenuineChunkedStream serializedLocalInfo = new GenuineChunkedStream(false))
			{
				// serialize local info
				BinaryWriter binaryWriter = new BinaryWriter(serializedLocalInfo);
				binaryWriter.Write((string) localUri);
				binaryWriter.Write((int) remote.LocalHostUniqueIdentifier);

				// and send it
				sharedMemoryConnection.LowLevel_SendSync(serializedLocalInfo, timeout);

				// read remote info
				using (Stream remoteUriStream = sharedMemoryConnection.LowLevel_ReadSync(timeout))
				{
					BinaryReader binaryReader = new BinaryReader(remoteUriStream);
					remoteUri = binaryReader.ReadString();
					remoteHostUniqueIdentifier = binaryReader.ReadInt32();
				}
			}

			sharedMemoryConnection.Remote = remote;
			sharedMemoryConnection.Remote.UpdateUri(remoteUri, remoteHostUniqueIdentifier);
			sharedMemoryConnection.Remote.GenuinePersistentConnectionState = GenuinePersistentConnectionState.Opened;

			// LOG:
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.HostInformation] > 0 )
			{
				binaryLogWriter.WriteHostInformationEvent("SharedMemoryConnectionManager.LowLevel_OpenConnection", 
					LogMessageType.HostInformationCreated, null, remote, 
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null, 
					sharedMemoryConnection.DbgConnectionId, 
					"HostInformation is ready for actions.");
			}

			// LOG:
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
			{
				binaryLogWriter.WriteEvent(LogCategory.Connection, "SharedMemoryConnectionManager.LowLevel_OpenConnection",
					LogMessageType.ConnectionEstablished, null, null, remote, null,
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
					securitySession, connectionLevelSSName, 
					sharedMemoryConnection.DbgConnectionId, (int) GenuineConnectionType.Persistent, 0, 0, this.GetType().Name, null, null, null,
					"The connection to the remote host is established.");
			}

			return sharedMemoryConnection;
		}


		/// <summary>
		/// Accepts an incoming connection.
		/// </summary>
		/// <param name="url">The name of the share.</param>
		/// <param name="localUri">URI of the local host.</param>
		/// <param name="protocolVersion">The version of the protocol supported by the remote host.</param>
		/// <param name="remoteUri">Uri of the remote host.</param>
		/// <param name="remoteHostUniqueIdentifier">The unique identifier of the HostInformation used by the remote host.</param>
		/// <returns>The established connection.</returns>
		private SharedMemoryConnection LowLevel_AcceptConnection_1(string url, string localUri, byte protocolVersion, out string remoteUri, out int remoteHostUniqueIdentifier)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			using (new ReaderAutoLocker(this._disposeLock))
			{
				if (this._disposed)
					throw OperationException.WrapException(this._disposeReason);
			}

			remoteUri = null;
			Stream inputStream = null;
			Stream outputStream = null;
			remoteHostUniqueIdentifier = 0;

			// the maximum time during which the connection must be established
			int timeout = GenuineUtility.GetTimeout((TimeSpan) this.ITransportContext.IParameterProvider[GenuineParameter.ConnectTimeout]);

			// open the client's share
			SharedMemoryConnection sharedMemoryConnection = new SharedMemoryConnection(this.ITransportContext, url, false, true);

			// LOG:
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
			{
				binaryLogWriter.WriteConnectionParameterEvent(LogCategory.Connection, "SharedMemoryConnectionManager.LowLevel_AcceptConnection",
					LogMessageType.ConnectionParameters, null, null, this.ITransportContext.IParameterProvider,
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, sharedMemoryConnection.DbgConnectionId, 
					"The connection is being established to \"{0}\".", url);
			}

			// get the connection-level Security Session
			string connectionLevelSSName = this.ITransportContext.IParameterProvider[GenuineParameter.SecuritySessionForPersistentConnections] as string;
			SecuritySession securitySession = null;
			if (connectionLevelSSName != null)
				securitySession = this.ITransportContext.IKeyStore.GetKey(connectionLevelSSName).CreateSecuritySession(connectionLevelSSName, null);

			// establish it
			if (securitySession != null && ! securitySession.IsEstablished)
			{
				for ( ; ; )
				{
					inputStream = Stream.Null;

					try
					{
						// prepare streams
						inputStream = sharedMemoryConnection.LowLevel_ReadSync(timeout);
						outputStream = securitySession.EstablishSession(inputStream, true);

						if (outputStream == null)
							break;

						// send a packet to the remote host
						sharedMemoryConnection.LowLevel_SendSync(outputStream, timeout);
						if (securitySession.IsEstablished)
							break;
					}
					finally
					{
						if (inputStream != null)
							inputStream.Close();
						if (outputStream != null)
							outputStream.Close();
					}
				}
			}

			sharedMemoryConnection.ConnectionLevelSecurity = securitySession;
			HostInformation remote = null;

			// read remote info
			using (Stream remoteUriStream = sharedMemoryConnection.LowLevel_ReadSync(timeout))
			{
				BinaryReader binaryReader = new BinaryReader(remoteUriStream);
				remoteUri = binaryReader.ReadString();

				remote = this.ITransportContext.KnownHosts[remoteUri];
				if (protocolVersion > 0)
					remoteHostUniqueIdentifier = binaryReader.ReadInt32();
			}

			// now send connection info through the established connection
			using (GenuineChunkedStream serializedLocalInfo = new GenuineChunkedStream(false))
			{
				// serialize local info
				BinaryWriter binaryWriter = new BinaryWriter(serializedLocalInfo);
				binaryWriter.Write((string) localUri);

				if (protocolVersion > 0)
					binaryWriter.Write((int) remote.LocalHostUniqueIdentifier);

				// and send it
				sharedMemoryConnection.LowLevel_SendSync(serializedLocalInfo, timeout);
			}

			// LOG:
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
			{
				binaryLogWriter.WriteEvent(LogCategory.Connection, "SharedMemoryConnectionManager.LowLevel_AcceptConnection_1",
					LogMessageType.ConnectionEstablished, null, null, remote, null,
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
					securitySession, connectionLevelSSName, 
					sharedMemoryConnection.DbgConnectionId, (int) GenuineConnectionType.Persistent, 0, 0, this.GetType().Name, null, null, null,
					"The connection to the remote host has been established.");
			}

			return sharedMemoryConnection;
		}

		/// <summary>
		/// Accepts the incoming connection.
		/// </summary>
		/// <param name="shareName">The name of the share.</param>
		/// <param name="protocolVersion">The version of the protocol supported by the remote host.</param>
		internal void Connection_AcceptConnection(string shareName, byte protocolVersion)
		{
			try
			{
				// accept the connection
				string remoteUri = null;
				int remoteHostUniqueIdentifier;
				SharedMemoryConnection connection = this.LowLevel_AcceptConnection_1(shareName, this.Local.Uri, protocolVersion, out remoteUri, out remoteHostUniqueIdentifier);

				connection.Remote = this.ITransportContext.KnownHosts[remoteUri];
				connection.Remote.ProtocolVersion = protocolVersion;
				connection.Remote.GenuinePersistentConnectionState = GenuinePersistentConnectionState.Accepted;
				connection.Remote.UpdateUri(remoteUri, remoteHostUniqueIdentifier);

				this._persistent[remoteUri] = connection;
				connection.SignalState(GenuineEventType.GeneralConnectionEstablished, null, null);

				// and process incoming messages
				this.Connection_InitiateReceiving(connection);
			}
			catch(Exception ex)
			{
				// LOG:
				BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "SharedMemoryConnectionManager.Connection_AcceptConnection",
						LogMessageType.ConnectionEstablished, ex, null, null, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
						null, null, 
						-1, 0, 0, 0, null, null, null, null,
						"The connection from the remote host \"{0}\" has been refused.", shareName);
				}

				this.ITransportContext.IGenuineEventProvider.Fire(new GenuineEventArgs(GenuineEventType.GeneralListenerFailure, ex, null, shareName));
			}
		}

		/// <summary>
		/// Initiates reading incoming content from the socket and dispatching it to the message handler manager.
		/// Should be called only once for persistent and named connections.
		/// </summary>
		/// <param name="sharedMemoryConnection">The connection.</param>
		internal void Connection_InitiateReceiving(SharedMemoryConnection sharedMemoryConnection)
		{
			SynchronousReceiving synchronousReceiving = new SynchronousReceiving();
			synchronousReceiving.SharedMemoryConnection = sharedMemoryConnection;
			synchronousReceiving.SharedMemoryConnectionManager = this;
			Thread thread = new Thread(new ThreadStart(synchronousReceiving.ReceiveSynchronously));
			thread.IsBackground = true;
			thread.Start();
		}

		/// <summary>
		/// Provides a receiving method being executed in a separate thread.
		/// </summary>
		private class SynchronousReceiving
		{
			public SharedMemoryConnection SharedMemoryConnection;
			public SharedMemoryConnectionManager SharedMemoryConnectionManager;

			/// <summary>
			/// Reads messages from the connection and processes them synchronously.
			/// </summary>
			public void ReceiveSynchronously()
			{
				try
				{
					for ( ; ; )
					{
						if (! this.SharedMemoryConnection.IsValid)
							return ;

						using (Stream stream = this.SharedMemoryConnection.LowLevel_ReadSync(GenuineUtility.GetTimeout(this.SharedMemoryConnection._closeAfterInactivity)))
						{
							// receive the message
							GenuineChunkedStream theMessage = new GenuineChunkedStream(true);
							GenuineUtility.CopyStreamToStream(stream, theMessage);

							// a message was successfully received
							this.SharedMemoryConnection.Renew();
							if (theMessage.Length == 0)
							{
								// LOG:
								BinaryLogWriter binaryLogWriter = this.SharedMemoryConnectionManager.ITransportContext.BinaryLogWriter;
								if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
								{
									binaryLogWriter.WriteEvent(LogCategory.Connection, "SharedMemoryConnectionManager.ReceiveSynchronously",
										LogMessageType.ConnectionPingReceived, null, null, null, null, 
										GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
										null, null, this.SharedMemoryConnection.DbgConnectionId, 0, 0, 0, null, null, null, null,
										"A message with zero size is treated as ping.");
								}

								continue;
							}

							this.SharedMemoryConnectionManager.ITransportContext.IIncomingStreamHandler.HandleMessage(theMessage, this.SharedMemoryConnection.Remote, GenuineConnectionType.Persistent, null, this.SharedMemoryConnection.DbgConnectionId, false, null, this.SharedMemoryConnection.ConnectionLevelSecurity, null);
							this.SharedMemoryConnection.Renew();
						}
					}
				}
				catch(Exception ex)
				{
					this.SharedMemoryConnectionManager.ConnectionFailed(ex, this.SharedMemoryConnection);
				}
			}
		}

		/// <summary>
		/// Releases all resources related to the specified connection.
		/// </summary>
		/// <param name="exception">The reason.</param>
		/// <param name="sharedMemoryConnection">The connection.</param>
		private void ConnectionFailed(Exception exception, SharedMemoryConnection sharedMemoryConnection)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			try
			{
				sharedMemoryConnection.IsValid = false;

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "SharedMemoryConnectionManager.ConnectionFailed",
						LogMessageType.ConnectionFailed, exception, null, sharedMemoryConnection.Remote, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
						null, null, 
						sharedMemoryConnection.DbgConnectionId, 0, 0, 0, null, null, null, null,
						"Connection has failed.");
				}

				// unregister the connection
				if (sharedMemoryConnection.Remote.GenuinePersistentConnectionState == GenuinePersistentConnectionState.Accepted)
					this._persistent.Remove(sharedMemoryConnection.Remote.Uri);
				else
					this._persistent.Remove(sharedMemoryConnection.Remote.Url);

				// release all resources
				this.ITransportContext.KnownHosts.ReleaseHostResources(sharedMemoryConnection.Remote, exception);
				sharedMemoryConnection.ReleaseUnmanagedResources();
				sharedMemoryConnection.SignalState(GenuineEventType.GeneralConnectionClosed, exception, null);
			}
			catch(Exception ex)
			{
				// LOG:
				if ( binaryLogWriter != null)
				{
					binaryLogWriter.WriteImplementationWarningEvent("SharedMemoryConnectionManager.ConnectionFailed",
						LogMessageType.CriticalError, ex, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
						"Unexpected exception inside the SharedMemoryClientConnectionManager.ConnectionFailed method. Most likely, something must be fixed.");
				}
			}
		}

		#endregion

		#region -- Ping management -----------------------------------------------------------------

		/// <summary>
		/// Closes expired connections and sends ping via inactive connections.
		/// </summary>
		public void TimerCallback()
		{
			int now = GenuineUtility.TickCount;
			int forcePingAfter = GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.PersistentConnectionSendPingAfterInactivity]);

			lock (this._persistent.SyncRoot)
			{
				// by all connections
				foreach (DictionaryEntry dictionaryEntry in this._persistent)
				{
					SharedMemoryConnection sharedMemoryConnection = (SharedMemoryConnection) dictionaryEntry.Value;
					if (GenuineUtility.IsTimeoutExpired(sharedMemoryConnection.LastTimeAMessageWasSent + forcePingAfter, now))
						GenuineThreadPool.QueueUserWorkItem(new WaitCallback(this.SendPing), sharedMemoryConnection, false);
				}
			}
		}

		/// <summary>
		/// Sends a ping through the specified connection.
		/// </summary>
		/// <param name="sharedMemoryConnectionAsObject">The connection.</param>
		private void SendPing(object sharedMemoryConnectionAsObject)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			SharedMemoryConnection sharedMemoryConnection = (SharedMemoryConnection) sharedMemoryConnectionAsObject;

			try
			{
				Message message = new Message(this.ITransportContext, sharedMemoryConnection.Remote, GenuineReceivingHandler.PING_MESSAGE_REPLYID, new TransportHeaders(), Stream.Null);
				message.IsSynchronous = false;
				message.FinishTime = GenuineUtility.GetTimeout((TimeSpan) message.ITransportContext.IParameterProvider[GenuineParameter.InvocationTimeout]);

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "SharedMemoryConnectionManager.SendPing",
						LogMessageType.ConnectionPingSending, null, message, sharedMemoryConnection.Remote, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
						null, null, 
						sharedMemoryConnection.DbgConnectionId, 0, 0, 0, null, null, null, null,
						"Ping is being sending.");
				}

				message.SerializedContent = Stream.Null;
				this.SendSync(message, sharedMemoryConnection);
			}
			catch(Exception ex)
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "SharedMemoryConnectionManager.SendPing",
						LogMessageType.ConnectionPingSent, ex, null, sharedMemoryConnection.Remote, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
						null, null, 
						sharedMemoryConnection.DbgConnectionId, 0, 0, 0, null, null, null, null,
						"Ping cannot be sent.");
				}
			}
		}

		#endregion
	}
}
