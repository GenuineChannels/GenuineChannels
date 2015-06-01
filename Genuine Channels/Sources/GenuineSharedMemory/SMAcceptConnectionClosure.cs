/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.IO;
using System.Threading;

using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Parameters;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Utilities;

namespace Belikov.GenuineChannels.GenuineSharedMemory
{
	/// <summary>
	/// Implements a Shared Memory listener which is capable of accepting inbound client's requests.
	/// </summary>
	internal class SMAcceptConnectionClosure
	{
		/// <summary>
		/// Constructs an instance of the SMAcceptConnectionClosure class.
		/// </summary>
		/// <param name="iTransportContext">The transport context.</param>
		/// <param name="sharedMemoryConnection">The server's connection.</param>
		/// <param name="sharedMemoryConnectionManager">The connection manager.</param>
		/// <param name="shareName">The name of the share.</param>
		public SMAcceptConnectionClosure(ITransportContext iTransportContext, SharedMemoryConnection sharedMemoryConnection, SharedMemoryConnectionManager sharedMemoryConnectionManager, string shareName)
		{
			this.ITransportContext = iTransportContext;
			this.SharedMemoryConnection = sharedMemoryConnection;
			this.SharedMemoryConnectionManager = sharedMemoryConnectionManager;
			this.ShareName = shareName;
		}

		/// <summary>
		/// The transport context.
		/// </summary>
		public ITransportContext ITransportContext;

		/// <summary>
		/// The server's memory share.
		/// </summary>
		public SharedMemoryConnection SharedMemoryConnection;

		/// <summary>
		/// The connection manager.
		/// </summary>
		public SharedMemoryConnectionManager SharedMemoryConnectionManager;

		/// <summary>
		/// Name of the share.
		/// </summary>
		public string ShareName;

		/// <summary>
		/// Indicates whether the listening should be stopped.
		/// </summary>
		public ManualResetEvent StopListening = new ManualResetEvent(false);

		private Mutex _mutex;
		private NamedEvent _clientConnectedEvent;
		private NamedEvent _clientAcceptedEvent;

		/// <summary>
		/// Accepts incoming connections.
		/// </summary>
		public void AcceptConnections()
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			byte protocolVersion;
			GenuineConnectionType genuineConnectionType;

			try
			{
				IParameterProvider parameters = this.ITransportContext.IParameterProvider;

				string mutexName = GenuineSharedMemoryChannel.ConstructSharedObjectName(
					"MUTEX" + this.ShareName, parameters);
				string clientConnected = GenuineSharedMemoryChannel.ConstructSharedObjectName(
					"CC" + this.ShareName, parameters);
				string clientAccepted = GenuineSharedMemoryChannel.ConstructSharedObjectName(
					"CA" + this.ShareName, parameters);

				this._mutex = WindowsAPI.CreateMutex(mutexName);

				this._clientConnectedEvent = NamedEvent.CreateNamedEvent(clientConnected, false, false);
				this._clientAcceptedEvent = NamedEvent.CreateNamedEvent(clientAccepted, false, true);
				WaitHandle[] handles = new WaitHandle[2] { this._clientConnectedEvent.ManualResetEvent, this.StopListening };

				for ( ; ; )
				{
					try
					{
						// listen
						WaitHandle.WaitAny(handles);

						// if shutting down
						if (this.StopListening.WaitOne(0, false))
							return ;

						// set timeout
						int timeout = GenuineUtility.GetTimeout((TimeSpan) this.ITransportContext.IParameterProvider[GenuineParameter.ConnectTimeout]);

						// client is connecting
						using (Stream headerStream = this.SharedMemoryConnection.LowLevel_ReadSync(timeout))
						{
							BinaryReader binaryReader = new BinaryReader(headerStream);
							string connectionId;
							MessageCoder.DeserializeConnectionHeader(binaryReader, out protocolVersion, out genuineConnectionType, out connectionId);
							string shareName = binaryReader.ReadString();
							this._clientAcceptedEvent.ManualResetEvent.Set();

							// LOG:
							if ( binaryLogWriter != null && binaryLogWriter[LogCategory.AcceptingConnection] > 0 )
							{
								binaryLogWriter.WriteEvent(LogCategory.AcceptingConnection, "SMAcceptConnectionClosure.AcceptConnections",
									LogMessageType.ConnectionAccepting, null, null, null, null, 
									GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
									null, null, -1, 0, 0, 0, null, null, null, null,
									"An inbound Shared Memory connection is being accepted.");
							}

							AcceptConnectionInformation acceptConnectionInformation = new AcceptConnectionInformation();
							acceptConnectionInformation.ShareName = shareName;
							acceptConnectionInformation.ProtocolVersion = protocolVersion;
							GenuineThreadPool.QueueUserWorkItem(new WaitCallback(this.AcceptConnection), acceptConnectionInformation, true);
						}
					}
					catch(Exception ex)
					{
						// LOG:
						if ( binaryLogWriter != null && binaryLogWriter[LogCategory.AcceptingConnection] > 0 )
						{
							binaryLogWriter.WriteEvent(LogCategory.AcceptingConnection, "SMAcceptConnectionClosure.AcceptConnections",
								LogMessageType.ConnectionAccepting, ex, null, null, null, 
								GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
								null, null, -1, 0, 0, 0, null, null, null, null,
								"Can't accept a connection.");
						}
					}
				}
			}
			catch(Exception ex)
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.AcceptingConnection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.AcceptingConnection, "SMAcceptConnectionClosure.AcceptConnections",
						LogMessageType.CriticalError, ex, null, null, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, -1, 0, 0, 0, null, null, null, null,
						"Critical listener failure. No connections will be accepted.");
				}

				this.SharedMemoryConnectionManager.ITransportContext.IGenuineEventProvider.Fire(new GenuineEventArgs(GenuineEventType.GeneralListenerFailure, ex, null, this.ShareName));
			}
			finally
			{
				this.SharedMemoryConnection.ReleaseUnmanagedResources();
				if (this._mutex != null)
					this._mutex.Close();
			}
		}

		/// <summary>
		/// Contains information about the connection being accepted.
		/// </summary>
		private class AcceptConnectionInformation
		{
			/// <summary>
			/// The name of the client share.
			/// </summary>
			public string ShareName;

			/// <summary>
			/// The version of the protocol supported by the client.
			/// </summary>
			public byte ProtocolVersion;
		}

		/// <summary>
		/// Accepts incoming connection.
		/// </summary>
		/// <param name="acceptConnectionInformationAsObject">Information about the connection being accepted.</param>
		public void AcceptConnection(object acceptConnectionInformationAsObject)
		{
			AcceptConnectionInformation acceptConnectionInformation = (AcceptConnectionInformation) acceptConnectionInformationAsObject;

			try
			{
				this.SharedMemoryConnectionManager.Connection_AcceptConnection(acceptConnectionInformation.ShareName, acceptConnectionInformation.ProtocolVersion);
			}
			catch(Exception ex)
			{
				this.ITransportContext.BinaryLogWriter.WriteEvent(LogCategory.Connection, "SMAcceptConnectionClosure.AcceptConnection", 
					LogMessageType.ConnectionAccepting, ex, null, null, null,
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
					null, null, -1, 0, 0, 0, null, null, null, null,
					"The connection to {0} has been refused due to the exception.", acceptConnectionInformation.ShareName);

				this.SharedMemoryConnectionManager.ITransportContext.IGenuineEventProvider.Fire(new GenuineEventArgs(GenuineEventType.GeneralListenerFailure, ex, null, acceptConnectionInformation.ShareName));
			}
		}

	}
}
