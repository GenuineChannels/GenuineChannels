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
using System.Runtime.Remoting.Channels;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Text;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Serialization;

using Belikov.Common.ThreadProcessing;

using Belikov.GenuineChannels.BufferPooling;
using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.GenuineTcp;
using Belikov.GenuineChannels.GenuineHttp;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Parameters;
using Belikov.GenuineChannels.Receiving;
using Belikov.GenuineChannels.Security;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Utilities;

namespace Belikov.GenuineChannels.GenuineXHttp
{
	/// <summary>
	/// Provides an implementation for a sender-receiver Connection Manager that uses the HTTP protocol to transmit messages
	/// via sockets.
	/// </summary>
	internal class XHttpConnectionManager : ConnectionManager, ITimerConsumer, IAcceptConnectionConsumer
	{
		/// <summary>
		/// Constructs an instance of the XHttpConnectionManager class.
		/// </summary>
		/// <param name="iTransportContext">The transport context.</param>
		public XHttpConnectionManager(ITransportContext iTransportContext) : base(iTransportContext)
		{
			this._HalfSync_Client_onEndReceiving = new AsyncCallback(this.LowLevel_HalfSync_Client_EndReceiving);
			this._HalfSync_Server_onEndReceiving = new AsyncCallback(this.LowLevel_HalfSync_Server_EndReceiving);
			this._AsyncSending_onEndSending = new AsyncCallback(this.LowLevel_OnEndAsyncSending);
			this._internal_TimerCallback = new WaitCallback(this.Internal_TimerCallback);
			this._HalfSync_Client_onContinueReceiving = new WaitCallback(this.Pool_Client_ContinueHalfSyncReceiving);
			this._HalfSync_Server_onContinueReceiving = new WaitCallback(this.Pool_Server_ContinueHalfSyncReceiving);
			this._releaseConnections_InspectPersistentConnections = new PersistentConnectionStorage.ProcessConnectionEventHandler(this.ReleaseConnections_InspectPersistentConnections);
			this._internal_TimerCallback_InspectPersistentConnections = new PersistentConnectionStorage.ProcessConnectionEventHandler(this.Internal_TimerCallback_InspectPersistentConnections);

			this.Local = new HostInformation("_ghttp://" + iTransportContext.HostIdentifier, iTransportContext);
			TimerProvider.Attach(this);

			_xHttpReadHeaderTimeout = GenuineUtility.ConvertToMilliseconds(iTransportContext.IParameterProvider[GenuineParameter.XHttpReadHttpMessageTimeout]);
		}

		private AsyncCallback _HalfSync_Server_onEndReceiving;
		private AsyncCallback _HalfSync_Client_onEndReceiving;
		private AsyncCallback _AsyncSending_onEndSending;
		private WaitCallback _HalfSync_Server_onContinueReceiving;
		private WaitCallback _HalfSync_Client_onContinueReceiving;
		private byte[] _unusedBuffer = new byte[1];

		#region -- Sending -------------------------------------------------------------------------

		/// <summary>
		/// A collection of established persistent connections.
		/// </summary>
		private PersistentConnectionStorage _persistent = new PersistentConnectionStorage();

		/// <summary>
		/// Sends the message to the remote host.
		/// Returns a response if corresponding Security Session is established and the initial message is not one-way.
		/// </summary>
		/// <param name="message">The message to be sent or a null reference (if there is a queued message).</param>
		protected override void InternalSend(Message message)
		{
			XHttpConnection xHttpConnection = this.Pool_GetConnectionForSending(message);
			XHttpPhysicalConnection xHttpPhysicalConnection = null;

			try
			{
				if (xHttpConnection.IsClient)
					xHttpPhysicalConnection = xHttpConnection.Sender;
				else
					xHttpPhysicalConnection = xHttpConnection.Listener;

				lock (xHttpPhysicalConnection.PhysicalConnectionStateLock)
				{
					if (xHttpPhysicalConnection.AcquireIfAvailable())
					{
						// connection lock is acquired, send or start sending
						this.LowLevel_SendHttpContent(GenuineUtility.GetTimeout(xHttpConnection.CloseConnectionAfterInactivity),
							message, null, null, xHttpConnection.MessageContainer, xHttpPhysicalConnection,
							xHttpConnection.GenuineConnectionType, HttpPacketType.Usual, false, message.IsSynchronous, true, true);
					}
					else
					{
						// queue the message
						xHttpConnection.MessageContainer.AddMessage(message, false);
					}
				}
			}
			catch(Exception ex)
			{
				this.ConnectionFailed(ex, xHttpConnection, xHttpPhysicalConnection);
			}
		}

		#endregion

		#region -- Pool Management -----------------------------------------------------------------

		private int _xHttpReadHeaderTimeout;

		/// <summary>
		/// Continues receiving and processing of the message in half-sync mode.
		/// </summary>
		/// <param name="xHttpPhysicalConnectionAsObject">The connection.</param>
		private void Pool_Client_ContinueHalfSyncReceiving(object xHttpPhysicalConnectionAsObject)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			XHttpPhysicalConnection xHttpPhysicalConnection = (XHttpPhysicalConnection) xHttpPhysicalConnectionAsObject;

			try
			{
				byte protocolVersion;
				string remoteUri;
				int sequenceNo;
				GenuineConnectionType genuineConnectionType;
				Guid connectionId;
				HttpPacketType httpPacketType;
				string connectionName;
				int remoteHostUniqueIdentifier;
				Stream inputStream = this.LowLevel_ParseHttpContent(
					GenuineUtility.GetTimeout(this._xHttpReadHeaderTimeout), true, 
					xHttpPhysicalConnection.Socket, xHttpPhysicalConnection.ConnectionLevelSecurity, 
					out protocolVersion, out remoteUri, out sequenceNo, out genuineConnectionType, out connectionId, 
					out httpPacketType, out connectionName, out remoteHostUniqueIdentifier);

				// client & sender - just check the sequenceNo and continue sending from the pool
				// client & listener - parse messages, execute them, start listening again
				xHttpPhysicalConnection.XHttpConnection.Renew();
				xHttpPhysicalConnection.XHttpConnection.SignalState(GenuineEventType.GeneralConnectionEstablished, null, null);

				// the sequenceNo must be the same, otherwise - disconnect
				if (sequenceNo != xHttpPhysicalConnection.SequenceNo)
					throw GenuineExceptions.Get_Channel_Desynchronization();

				xHttpPhysicalConnection.Remote.UpdateUri(remoteUri, remoteHostUniqueIdentifier);

				// if the remote host has asked to terminate the connection
				if (httpPacketType == HttpPacketType.ClosedManually || httpPacketType == HttpPacketType.Desynchronization)
					throw GenuineExceptions.Get_Receive_ConnectionClosed();

				if (httpPacketType == HttpPacketType.SenderError)
					throw GenuineExceptions.Get_Receive_ConnectionClosed(this.LowLevel_ParseException(inputStream).Message);

				if (! xHttpPhysicalConnection.IsSender)
				{
					// parse and execute messages
					if (httpPacketType != HttpPacketType.ListenerTimedOut)
						this.LowLevel_ParseLabelledStream(inputStream, xHttpPhysicalConnection);
				}

				// skip the remaining content
				inputStream.Close();

				// send the next request
				this.Pool_HandleClientConnection(xHttpPhysicalConnection);
			}
			catch(Exception ex)
			{
				this.ConnectionFailed(ex, xHttpPhysicalConnection.XHttpConnection, xHttpPhysicalConnection);
			}
		}

		/// <summary>
		/// Continues receiving and processing of the message in half-sync mode.
		/// </summary>
		/// <param name="socketAsObject">The connection.</param>
		private void Pool_Server_ContinueHalfSyncReceiving(object socketAsObject)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			XHttpConnection xHttpConnection = null;
			XHttpPhysicalConnection xHttpPhysicalConnection = null;
			Socket socket = (Socket) socketAsObject;

			byte protocolVersion;
			string remoteUri = null;
			int sequenceNo = 0;
			GenuineConnectionType genuineConnectionType = GenuineConnectionType.None;
			Guid hostId = Guid.Empty;
			HttpPacketType httpPacketType = HttpPacketType.Unkown;
			string connectionName;
			int remoteHostUniqueIdentifier;

			try
			{
				// server & sender - parse messages, execute them, send confirmation
				// server & listener - if messages are available, send them all. otherwise set available flag and hold the connection for a while

				// first, read the header
				Stream inputStream = this.LowLevel_ParseHttpContent(GenuineUtility.GetTimeout(GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.XHttpReadHttpMessageTimeout])),
					false, socket, null, out protocolVersion, out remoteUri, out sequenceNo, out genuineConnectionType, out hostId, out httpPacketType, out connectionName, out remoteHostUniqueIdentifier);

				// who it was
				HostInformation remote = this.ITransportContext.KnownHosts["_ghttp://" + hostId.ToString("N")];
				remote.UpdateUri(remote.Uri, remoteHostUniqueIdentifier);
				remote.ProtocolVersion = protocolVersion;

				// get the connection
				lock (remote.PersistentConnectionEstablishingLock)
				{
					xHttpConnection = this._persistent.Get(remote.Uri, connectionName) as XHttpConnection;

					if (httpPacketType == HttpPacketType.Establishing_ResetConnection && xHttpConnection != null)
					{
						xHttpConnection.Dispose(GenuineExceptions.Get_Receive_ConnectionClosed("The connection was closed by HttpPacketType.Establishing_ResetConnection client request."));
						xHttpConnection = null;
					}

					if (xHttpConnection == null)
					{
						using (new ReaderAutoLocker(this._disposeLock))
						{
							if (this._disposed)
								throw OperationException.WrapException(this._disposeReason);

							// provide a possibility to decline the connection
							ConnectionAcceptedCancellableEventParameter connectionAcceptedCancellableEventParameter = new ConnectionAcceptedCancellableEventParameter();
							connectionAcceptedCancellableEventParameter.Socket = socket;
							connectionAcceptedCancellableEventParameter.IPEndPoint = (IPEndPoint) socket.RemoteEndPoint;
							this.ITransportContext.IGenuineEventProvider.Fire(new GenuineEventArgs(GenuineEventType.GHttpConnectionAccepted, null, null, connectionAcceptedCancellableEventParameter));
							if (connectionAcceptedCancellableEventParameter.Cancel)
								throw GenuineExceptions.Get_Connect_CanNotAcceptIncomingConnection("Connection accepting was cancelled by the event consumer.");

							xHttpConnection = this.Pool_CreateConnection(remote, remote.Uri, false, connectionName);
						}
					}
				}

				bool theSamePacketIsRequested;

				lock (xHttpConnection.Listener.PhysicalConnectionStateLock)
				{
					if (httpPacketType == HttpPacketType.Listening)
					{
						// if there is an already registered listener request - release it
						if (xHttpConnection.Listener.ConnectionAvailable && xHttpConnection.Listener.Socket != socket)
						{
							// shut down the previous physical connection
							this.LowLevel_SendServerError(new PhysicalConnectionAndSocket(xHttpConnection.Listener, socket));
//							GenuineThreadPool.QueueUserWorkItem(new WaitCallback(this.LowLevel_SendServerError), new PhysicalConnectionAndSocket(xHttpConnection.Listener, socket), true);
							xHttpConnection.Listener.AcquireIfAvailable();
						}

						xHttpConnection.Listener.Socket = socket;
						xHttpPhysicalConnection = xHttpConnection.Listener;
					}
					else
					{
						xHttpConnection.Sender.Socket = socket;
						xHttpPhysicalConnection = xHttpConnection.Sender;
					}

					theSamePacketIsRequested = xHttpPhysicalConnection.SequenceNo == sequenceNo && xHttpPhysicalConnection.SentContent != null;

#if DEBUG
					xHttpPhysicalConnection.TypeOfSocket = "Accepted";
#endif

					xHttpPhysicalConnection.Remote.PhysicalAddress = socket.RemoteEndPoint;
					xHttpPhysicalConnection.Remote.LocalPhysicalAddress = socket.LocalEndPoint;
				}	// lock (xHttpConnection.Listener.PhysicalConnectionStateLock)

				// renew connection lifetime
				xHttpConnection.Renew();
				xHttpPhysicalConnection.XHttpConnection.SignalState(GenuineEventType.GeneralConnectionEstablished, null, null);

				// if the same packet stream is requested, send the response
				if (theSamePacketIsRequested)
				{
					// skip the current stream
					inputStream.Close();

					// send the stream and initiate receiving
					this.LowLevel_SendHttpContent(remote.ExpireTime, null, null, null, null, xHttpPhysicalConnection, genuineConnectionType, HttpPacketType.RequestRepeated, true, true, true, true);
					return ;
				}

				lock (xHttpConnection.Listener.PhysicalConnectionStateLock)
				{
					// if desynchronization, report and continue
					if (httpPacketType != HttpPacketType.Establishing_ResetConnection && xHttpPhysicalConnection.SequenceNo > sequenceNo || sequenceNo > xHttpPhysicalConnection.SequenceNo + 1)
					{
						// LOG:
						if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
						{
							binaryLogWriter.WriteEvent(LogCategory.Connection, "XHttpConnectionManager.Pool_Server_ContinueHalfSyncreceiving",
								LogMessageType.ConnectionStreamDesynchronization, GenuineExceptions.Get_Debugging_GeneralWarning("Stream desynchronization."), 
								null, xHttpPhysicalConnection.Remote, null, 
								GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
								xHttpPhysicalConnection.ConnectionLevelSecurity, xHttpPhysicalConnection.ConnectionLevelSecurity == null ? null : xHttpPhysicalConnection.ConnectionLevelSecurity.Name, 
								xHttpConnection.DbgConnectionId, 0, 0, 0, null, null, null, null,
								"Stream desynchronization error. Received sequence number: {0}. Expected sequence number: {1}.", xHttpPhysicalConnection.SequenceNo, sequenceNo);
						}

						// send the stream and initiate receiving
						this.LowLevel_SendHttpContent(remote.ExpireTime, null, null, null, null, xHttpPhysicalConnection, genuineConnectionType, HttpPacketType.Desynchronization, false, true, true, true);
						return ;
					}

					// the next sequence is requested
					if (xHttpPhysicalConnection.SentContent != null)
					{
						// release the content
						xHttpPhysicalConnection.SentContent.Close();
						xHttpPhysicalConnection.SentContent = null;
					}

					// respond with the same seq No
					xHttpPhysicalConnection.SequenceNo = sequenceNo;
					xHttpPhysicalConnection.Socket = socket;
					xHttpPhysicalConnection.MessagesBeingSent.Clear();
				}

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "XHttpConnectionManager.Pool_Server_ContinueHalfSyncreceiving",
						LogMessageType.ReceivingFinished, null, 
						null, xHttpPhysicalConnection.Remote, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						xHttpPhysicalConnection.ConnectionLevelSecurity, xHttpPhysicalConnection.ConnectionLevelSecurity == null ? null : xHttpPhysicalConnection.ConnectionLevelSecurity.Name, 
						xHttpConnection.DbgConnectionId, 0, 0, 0, null, null, null, null,
						"HTTP Stream received. Packet type: {0}.", Enum.Format(typeof(HttpPacketType), httpPacketType, "g"));
				}

				// analyze the type of the packet
				switch(httpPacketType)
				{
					case HttpPacketType.Establishing_ResetConnection:
					case HttpPacketType.Establishing:
						// mark the remote host as client
						lock (remote.PersistentConnectionEstablishingLock)
							remote.GenuinePersistentConnectionState = GenuinePersistentConnectionState.Accepted;

						// read CLSS streams
						BinaryReader binaryReader = new BinaryReader(inputStream);
						Stream clsseStream = null;
						GenuineChunkedStream outputStream = new GenuineChunkedStream(false);

						// process the sender info
						int writtenSize = binaryReader.ReadInt32();

						using (new GenuineChunkedStreamSizeLabel(outputStream))
						{
							if (xHttpConnection.Sender.ConnectionLevelSecurity != null && ! xHttpConnection.Sender.ConnectionLevelSecurity.IsEstablished)
							{
								using (Stream clssData = new DelimiterStream(inputStream, writtenSize))
								{
									clsseStream = xHttpConnection.Sender.ConnectionLevelSecurity.EstablishSession(clssData, true);
								}
							}

							if (clsseStream != null)
								GenuineUtility.CopyStreamToStream(clsseStream, outputStream);
						}


						// process the listener info
						writtenSize = binaryReader.ReadInt32();
						using (new GenuineChunkedStreamSizeLabel(outputStream))
						{
							if (xHttpConnection.Listener.ConnectionLevelSecurity != null && ! xHttpConnection.Listener.ConnectionLevelSecurity.IsEstablished)
							{
								using (Stream clssData = new DelimiterStream(inputStream, writtenSize))
								{
									clsseStream = xHttpConnection.Listener.ConnectionLevelSecurity.EstablishSession(clssData, true);
								}
							}

							if (clsseStream != null)
								GenuineUtility.CopyStreamToStream(clsseStream, outputStream);
						}

						// skip remaining part of the packet
						inputStream.Close();

						this.LowLevel_SendHttpContent(remote.ExpireTime, null, outputStream, null, null, xHttpPhysicalConnection,
							genuineConnectionType, httpPacketType, false, true, true, false);
						break;

					case HttpPacketType.Listening:
						// if messages are available, send them immediately
						// otherwise put the connection off for a while

						// skip the remaining content
						inputStream.Close();
						Pool_Server_ProcessListenerRequest(xHttpConnection, xHttpPhysicalConnection);
						break;

					case HttpPacketType.Usual:
						// apply CLSS if it was established
						if (xHttpPhysicalConnection.ConnectionLevelSecurity != null)
							inputStream = xHttpPhysicalConnection.ConnectionLevelSecurity.Decrypt(inputStream);
						Pool_Server_ProcessSenderRequest(xHttpConnection, xHttpPhysicalConnection, inputStream);
						break;

					default:
						throw GenuineExceptions.Get_Receive_IncorrectData("Unexpected type of the packet.");
				}
			}
			catch(Exception ex)
			{
				try
				{
					// LOG:
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
					{
						binaryLogWriter.WriteEvent(LogCategory.Connection, "XHttpConnectionManager.Pool_Server_ContinueHalfSyncreceiving",
							LogMessageType.ReceivingFinished, ex, 
							null, xHttpPhysicalConnection.Remote, null, 
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
							xHttpPhysicalConnection.ConnectionLevelSecurity, xHttpPhysicalConnection.ConnectionLevelSecurity == null ? null : xHttpPhysicalConnection.ConnectionLevelSecurity.Name, 
							xHttpConnection.DbgConnectionId, 0, 0, 0, null, null, null, null,
							"Error occurred while receiving HTTP Stream. Sequence No: {0}. Connection type: {1}. Packet type: {2}.",
							sequenceNo, genuineConnectionType, httpPacketType);
					}

					SocketUtility.CloseSocket(socket);
				}
				catch
				{
				}
			}
		}

		/// <summary>
		/// Processes the listening request on the server side.
		/// </summary>
		/// <param name="xHttpConnection">The connection.</param>
		/// <param name="xHttpPhysicalConnection">The physical connection.</param>
		private void Pool_Server_ProcessListenerRequest(XHttpConnection xHttpConnection, XHttpPhysicalConnection xHttpPhysicalConnection)
		{
			lock (xHttpConnection.Listener.PhysicalConnectionStateLock)
			{
				try
				{
					Message message = xHttpConnection.MessageContainer.GetMessage();
					if (message == null)
					{
						// no data is available, postpone the request
//						xHttpPhysicalConnection.Listener_Opened = GenuineUtility.TickCount;
						xHttpPhysicalConnection.MarkAsAvailable();
						return;
					}

					// some data is available, gather the stream and send it
					this.LowLevel_SendHttpContent(GenuineUtility.GetTimeout(xHttpConnection.CloseConnectionAfterInactivity), 
						message, null, null, xHttpConnection.MessageContainer, xHttpPhysicalConnection, 
						xHttpConnection.GenuineConnectionType, HttpPacketType.Usual, 
						false, true, true, true);
				}
				catch(Exception ex)
				{
					// LOG:
					BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
					{
						binaryLogWriter.WriteEvent(LogCategory.Connection, "XHttpConnectionManager.Pool_Server_ProcessListenerRequest",
							LogMessageType.AsynchronousSendingStarted, ex, 
							null, xHttpPhysicalConnection.Remote, null, 
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
							null, null, 
							xHttpConnection.DbgConnectionId, 0, 0, 0, null, null, null, null,
							"Error occurred while sending HTTP Listener Request. Sequence No: {0}.",
							xHttpPhysicalConnection.SequenceNo);
					}
				}
			}
		}

		/// <summary>
		/// Processes the sending request on the server side.
		/// </summary>
		/// <param name="xHttpConnection">The connection.</param>
		/// <param name="xHttpPhysicalConnection">The physical connection.</param>
		/// <param name="inputStream">The connection.</param>
		private void Pool_Server_ProcessSenderRequest(XHttpConnection xHttpConnection, XHttpPhysicalConnection xHttpPhysicalConnection, Stream inputStream)
		{
			Exception gotException = null;

			try
			{
				this.LowLevel_ParseLabelledStream(inputStream, xHttpPhysicalConnection);
				inputStream.Close();
			}
			catch(Exception ex)
			{
				gotException = ex;

				// LOG:
				BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "XHttpConnectionManager.Pool_Server_ProcessSenderRequest",
						LogMessageType.ReceivingFinished, ex, 
						null, xHttpConnection.Remote, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, 
						xHttpConnection.DbgConnectionId, 0, 0, 0, null, null, null, null,
						"Error occurred while parsing HTTP Sender Request. Sequence No: {0}.",
						xHttpPhysicalConnection.SequenceNo);
				}
			}

			if (gotException != null)
			{
				gotException = OperationException.WrapException(gotException);
				GenuineChunkedStream outputStream = new GenuineChunkedStream(false);
				BinaryFormatter binaryFormatter = new BinaryFormatter(new RemotingSurrogateSelector(), new StreamingContext(StreamingContextStates.Other));
				binaryFormatter.Serialize(outputStream, gotException);

				this.LowLevel_SendHttpContent(GenuineUtility.GetTimeout(xHttpConnection.CloseConnectionAfterInactivity), 
					null, null, gotException, null, xHttpPhysicalConnection, 
					GenuineConnectionType.Persistent, HttpPacketType.SenderError,
					false, true, true, false);
			}
			else
			{
				// serialize and send the OK response
				this.LowLevel_SendHttpContent(GenuineUtility.GetTimeout(xHttpConnection.CloseConnectionAfterInactivity), 
					null, null, null, null, xHttpPhysicalConnection, GenuineConnectionType.Persistent,
					HttpPacketType.SenderResponse, false, true, true, false);
			}
		}

		/// <summary>
		/// Sends a request to the server according to the type of the connection provided.
		/// Does not process exceptions.
		/// </summary>
		/// <param name="xHttpPhysicalConnection"></param>
		private void Pool_HandleClientConnection(XHttpPhysicalConnection xHttpPhysicalConnection)
		{
			// client & sender - start sending a message or mark the connection as available
			// client & listener - send the next request
			if (xHttpPhysicalConnection.IsSender)
				Pool_Sender_HandleClientConnection(xHttpPhysicalConnection);
			else
				Pool_Listener_HandleClientConnection(xHttpPhysicalConnection);
		}

		/// <summary>
		/// Starts sending a message if one is available. Marks the connection is available if there are no messages at the moment.
		/// Does not process exceptions.
		/// </summary>
		/// <param name="xHttpPhysicalConnection">The sending connection.</param>
		private void Pool_Sender_HandleClientConnection(XHttpPhysicalConnection xHttpPhysicalConnection)
		{
			lock (xHttpPhysicalConnection.PhysicalConnectionStateLock)
			{
				Message message = xHttpPhysicalConnection.XHttpConnection.MessageContainer.GetMessage();
				if (message == null)
				{
					xHttpPhysicalConnection.MarkAsAvailable();
					return ;
				}

				this.LowLevel_SendHttpContent(GenuineUtility.GetTimeout(xHttpPhysicalConnection.XHttpConnection.CloseConnectionAfterInactivity),
					message, null, null, xHttpPhysicalConnection.XHttpConnection.MessageContainer,
					xHttpPhysicalConnection, GenuineConnectionType.Persistent, HttpPacketType.Usual, 
					false, true, true, true);
			}
		}

		/// <summary>
		/// Starts sending a message if one is available. Marks the connection is available if there are no messages at the moment.
		/// Does not process exceptions.
		/// </summary>
		/// <param name="xHttpPhysicalConnection">The sending connection.</param>
		private void Pool_Listener_HandleClientConnection(XHttpPhysicalConnection xHttpPhysicalConnection)
		{
			this.LowLevel_SendHttpContent(GenuineUtility.GetTimeout(xHttpPhysicalConnection.XHttpConnection.CloseConnectionAfterInactivity),
				null, null, null, null, xHttpPhysicalConnection, GenuineConnectionType.Persistent, 
				HttpPacketType.Listening, false, true, true, true);
		}

		/// <summary>
		/// Answers a connection fitting to the specified parameters or throws the corresponding exception
		/// when it's impossible.
		/// </summary>
		/// <param name="message">The message being sent.</param>
		/// <returns>The acquired connection.</returns>
		private XHttpConnection Pool_GetConnectionForSending(Message message)
		{
			XHttpConnection xHttpConnection = null;
			bool isServer = false;
			string uri = null;

			using (new ReaderAutoLocker(this._disposeLock))
			{
				if (this._disposed)
					throw OperationException.WrapException(this._disposeReason);

				switch(message.SecuritySessionParameters.GenuineConnectionType)
				{
					case GenuineConnectionType.Persistent:
						uri = message.Recipient.Url;
						if (message.ConnectionName == null)
							message.ConnectionName = message.SecuritySessionParameters.ConnectionName;

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
								xHttpConnection = this._persistent.Get(uri, message.ConnectionName) as XHttpConnection;

								if (xHttpConnection == null)
								{
									// try to establish a persistent connection
									xHttpConnection = this.Pool_EstablishPersistentConnection(message.Recipient, message.ConnectionName);

									// OK, connection established
									xHttpConnection.MessageContainer = new MessageContainer(this.ITransportContext);
									this._persistent.Set(uri, xHttpConnection.ConnectionName, xHttpConnection);

									xHttpConnection.CloseConnectionAfterInactivity = GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.ClosePersistentConnectionAfterInactivity]);
									xHttpConnection.Renew();
									xHttpConnection.SignalState(GenuineEventType.GeneralConnectionEstablished, null, null);
								}
							}
							else
							{
								// remote host is a client and if there is no connection to it, it's unreachable
								lock (this._persistent)
									xHttpConnection = this._persistent.Get(uri, message.ConnectionName) as XHttpConnection;
								if (xHttpConnection == null)
									throw GenuineExceptions.Get_Send_DestinationIsUnreachable(message.Recipient.Uri);
							}

						}
						break;

					case GenuineConnectionType.Invocation:
						break;
				}
			}

			return xHttpConnection;
		}

		#endregion

		#region -- Connection closing and failures -------------------------------------------------

		private PersistentConnectionStorage.ProcessConnectionEventHandler _releaseConnections_InspectPersistentConnections;
		private class ReleaseConnections_Parameters
		{
			public ArrayList FailedConnections;
			public HostInformation HostInformation;
		}

		/// <summary>
		/// Finds connections to be released.
		/// </summary>
		/// <param name="xHttpConnectionAsObject">The connection.</param>
		/// <param name="releaseConnections_ParametersAsObject">Stuff to make decisions and to save the results.</param>
		private void ReleaseConnections_InspectPersistentConnections(object xHttpConnectionAsObject, object releaseConnections_ParametersAsObject)
		{
			XHttpConnection xHttpConnection = (XHttpConnection) xHttpConnectionAsObject;
			ReleaseConnections_Parameters parameters = (ReleaseConnections_Parameters) releaseConnections_ParametersAsObject;

			if (parameters.HostInformation != null && xHttpConnection.Remote != parameters.HostInformation)
				return ;

			parameters.FailedConnections.Add(xHttpConnection);
		}

		/// <summary>
		/// Closes the specified connections to the remote host and releases acquired resources.
		/// </summary>
		/// <param name="hostInformation">Host information.</param>
		/// <param name="genuineConnectionType">What kind of connections will be affected by this operation.</param>
		/// <param name="reason">Reason of resource releasing.</param>
		public override void ReleaseConnections(HostInformation hostInformation, GenuineConnectionType genuineConnectionType, Exception reason)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			ArrayList connectionsToClose = new ArrayList();

			// LOG:
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
			{
				binaryLogWriter.WriteEvent(LogCategory.Connection, "XHttpConnectionManager.ReleaseConnections",
					LogMessageType.ReleaseConnections, reason, null, hostInformation, null, 
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
					null, null, -1, 0, 0, 0, Enum.Format(typeof(GenuineConnectionType), genuineConnectionType, "g"), null, null, null,
					"Connections \"{0}\" are terminated.", Enum.Format(typeof(GenuineConnectionType), genuineConnectionType, "g"), null);
			}

			using (new WriterAutoLocker(this._disposeLock))
			{
				// persistent
				if ( (genuineConnectionType & GenuineConnectionType.Persistent) != 0 )
				{
					ReleaseConnections_Parameters releaseConnections_Parameters = new ReleaseConnections_Parameters();
					releaseConnections_Parameters.FailedConnections = connectionsToClose;
					releaseConnections_Parameters.HostInformation = hostInformation;

					this._persistent.InspectAllConnections(this._releaseConnections_InspectPersistentConnections, releaseConnections_Parameters);
				}
			}

			// close connections
			foreach (XHttpConnection nextXHttpConnection in connectionsToClose)
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "XHttpConnectionManager.ReleaseConnections",
						LogMessageType.ConnectionShuttingDown, reason, null, hostInformation, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, nextXHttpConnection.DbgConnectionId, 0, 0, 0, Enum.Format(typeof(GenuineConnectionType), genuineConnectionType, "g"), null, null, null,
						"Connection is terminated.");
				}

				this.ConnectionFailed(GenuineExceptions.Get_Channel_ConnectionShutDown(reason), nextXHttpConnection, null);

				if (nextXHttpConnection.GenuineConnectionType == GenuineConnectionType.Persistent)
					nextXHttpConnection.SignalState(GenuineEventType.GeneralConnectionClosed, reason, null);
			}
		}

		/// <summary>
		/// Returns names of connections opened to the specified destination.
		/// Not all Connection Manager support this member.
		/// </summary>
		/// <param name="uri">The URI or URL of the remote host.</param>
		/// <returns>Names of connections opened to the specified destination.</returns>
		public override string[] GetConnectionNames(string uri)
		{
			string ignored;
			uri = GenuineUtility.Parse(uri, out ignored);

			return this._persistent.GetAll(uri);
		}

		/// <summary>
		/// Operates with failed sockets.
		/// </summary>
		/// <param name="exception">The source exception.</param>
		/// <param name="xHttpConnection">The failed logical connection.</param>
		/// <param name="xHttpPhysicalConnection">The failed physical connection.</param>
		private void ConnectionFailed(Exception exception, XHttpConnection xHttpConnection, XHttpPhysicalConnection xHttpPhysicalConnection)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			try
			{
				if (xHttpPhysicalConnection == null && xHttpConnection == null)
				{
					// LOG:
					if ( binaryLogWriter != null )
					{
						binaryLogWriter.WriteImplementationWarningEvent("XHttpConnectionManager.ConnectionFailed",
							LogMessageType.ConnectionFailed, exception, 
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
							"The connection has not been specified while invoking XHttpConnectionManager.ConnectionFailed.");
					}
					return ;
				}

				OperationException operationException = exception as OperationException;
				bool conflict409Received = false;
				if (operationException != null)
					conflict409Received = operationException.OperationErrorMessage.ErrorIdentifier.IndexOf("ConflictOfConnections") >= 0;

				// if it's a server, just close the connection
				// if it's a client sender, re-send it again
				// if it's a client listener, re-send the request again with the same seq no
				bool tryToReestablish = ! ConnectionManager.IsExceptionCritical(exception as OperationException);

				HostInformation remote = null;
				if (xHttpConnection != null)
					remote = xHttpConnection.Remote;

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "XHttpConnectionManager.ConnectionFailed",
						LogMessageType.ConnectionFailed, exception, null, remote, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
						null, null, 
						xHttpConnection == null ? -1 : xHttpConnection.DbgConnectionId, 0, 0, 0, null, null, null, null,
						"XHTTP connection has failed.");
				}

				using (new ReaderAutoLocker(this._disposeLock))
				{
					if (this._disposed)
						tryToReestablish = false;
				}

				// close the socket
				if (xHttpPhysicalConnection != null)
					GenuineThreadPool.QueueUserWorkItem(new WaitCallback(SocketUtility.CloseSocket), xHttpPhysicalConnection.Socket, true);

				switch (xHttpConnection.GenuineConnectionType)
				{
					case GenuineConnectionType.Persistent:
						// ignore connections causing conflict
						if (conflict409Received)
							break;

						lock (remote.PersistentConnectionEstablishingLock)
						{
							using (new ReaderAutoLocker(this._disposeLock))
							{
								if (! tryToReestablish || xHttpConnection.IsDisposed)
								{
									this._persistent.Remove(remote.PrimaryUri, xHttpConnection.ConnectionName);

									// release all resources
									this.ITransportContext.KnownHosts.ReleaseHostResources(xHttpConnection.Remote, exception);
									xHttpConnection.Dispose(exception);

									xHttpConnection.SignalState(GenuineEventType.GeneralConnectionClosed, exception, null);
									if (exception is OperationException && ((OperationException) exception).OperationErrorMessage.ErrorIdentifier == "GenuineChannels.Exception.Receive.ServerHasBeenRestarted")
										xHttpConnection.SignalState(GenuineEventType.GeneralServerRestartDetected, exception, null);
									break;
								}

								xHttpConnection.SignalState(GenuineEventType.GeneralConnectionReestablishing, exception, null);

								// start reestablishing
								if (remote.GenuinePersistentConnectionState == GenuinePersistentConnectionState.Opened)
								{
									// start the reestablishing, if possible
									if (xHttpPhysicalConnection != null && xHttpPhysicalConnection.Reestablish_ObtainStatus())
										GenuineThreadPool.QueueUserWorkItem(new WaitCallback(this.ReestablishConnection), xHttpPhysicalConnection, true);
								}

							}	// using (new ReaderAutoLocker(this._disposeLock))
						}	// lock (remote.PersistentConnectionEstablishingLock)
						break;

					case GenuineConnectionType.Invocation:
						break;
				}
			}
			catch(Exception ex)
			{
				// LOG:
				if ( binaryLogWriter != null )
					binaryLogWriter.WriteImplementationWarningEvent("XHttpConnectionManager.ConnectionFailed",
						LogMessageType.Warning, ex, GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						"An unexpected exception is raised inside XHttpConnectionManager.ConnectionFailed method. Most likely, something must be fixed.");
			}
		}

		/// <summary>
		/// Reestablishes the specified physical connection.
		/// </summary>
		/// <param name="xHttpPhysicalConnectionAsObject">The connection reestablished.</param>
		private void ReestablishConnection(object xHttpPhysicalConnectionAsObject)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			XHttpPhysicalConnection xHttpPhysicalConnection = (XHttpPhysicalConnection) xHttpPhysicalConnectionAsObject;
			HostInformation remote = xHttpPhysicalConnection.Remote;

			// LOG:
			if ( binaryLogWriter != null )
				binaryLogWriter.WriteImplementationWarningEvent("XHttpConnectionManager.ReestablishConnection",
					LogMessageType.Warning, GenuineExceptions.Get_Debugging_GeneralWarning("The connection is available during ReestablishConnection invocation."), 
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
					"The connection is available during ReestablishConnection invocation.");

			using(new ReaderAutoLocker(this._disposeLock))
			{
				if (this._disposed)
					return ;
			}

			try
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "XHttpConnectionManager.ReestablishConnection",
						LogMessageType.ConnectionReestablishing, null, null, xHttpPhysicalConnection.Remote, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, 
						xHttpPhysicalConnection.XHttpConnection.DbgConnectionId, 
						0, 0, 0, null, null, null, null,
						"Trying to reconnect to {0}.", remote.Url);
				}

				remote.Renew(GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.MaxTimeSpanToReconnect]), false);
				int deadline = remote.ExpireTime;

				for ( int tryNumber = 0; ; tryNumber++ )
				{
					Thread.Sleep((TimeSpan) this.ITransportContext.IParameterProvider[GenuineParameter.SleepBetweenReconnections]);
					if (tryNumber > (int) this.ITransportContext.IParameterProvider[GenuineParameter.ReconnectionTries] || GenuineUtility.IsTimeoutExpired(deadline))
						throw GenuineExceptions.Get_Channel_ReconnectionFailed();

					// and manager was not disposed
					using (new ReaderAutoLocker(this._disposeLock))
					{
						if (this._disposed)
							throw GenuineExceptions.Get_Channel_ReconnectionFailed();
					}

					// if connection was disposed, reconnection is senseless
					if (xHttpPhysicalConnection.XHttpConnection.IsDisposed)
						return ;

					// the next attempt
					try
					{
						xHttpPhysicalConnection.CheckConnectionStatus();

						bool sentContentPresents;
						lock (xHttpPhysicalConnection.PhysicalConnectionStateLock)
							sentContentPresents = xHttpPhysicalConnection.SentContent != null;

						if (sentContentPresents)
							this.LowLevel_SendHttpContent(deadline, null, null, null, 
								xHttpPhysicalConnection.XHttpConnection.MessageContainer, 
								xHttpPhysicalConnection, GenuineConnectionType.Persistent, 
								HttpPacketType.Unkown, true, true, true, true);
						else
							this.Pool_HandleClientConnection(xHttpPhysicalConnection);
					}
					catch (Exception ex)
					{
						// LOG:
						if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
							binaryLogWriter.WriteEvent(LogCategory.Connection, "XHttpConnectionManager.ReestablishConnection",
								LogMessageType.ConnectionReestablishing, ex, null, remote, null, 
								GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
								null, null, 
								-1, 0, 0, 0, null, null, null, null,
								"Reconnection failed. The current try: {0}. Milliseconds remained: {1}.", tryNumber, GenuineUtility.CompareTickCounts(deadline, GenuineUtility.TickCount));
						continue;
					}

					// the connection was reestablished
					using(new ReaderAutoLocker(this._disposeLock))
					{
						if (this._disposed)
							throw OperationException.WrapException(this._disposeReason);

						xHttpPhysicalConnection.Reestablish_ResetStatus();
						xHttpPhysicalConnection.XHttpConnection.Renew();
						xHttpPhysicalConnection.MarkAsAvailable();
						xHttpPhysicalConnection.XHttpConnection.SignalState(GenuineEventType.GeneralConnectionEstablished, null, null);
					}
					break;
				}
			}
			catch(Exception ex)
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "XHttpConnectionManager.ReestablishConnection",
						LogMessageType.ConnectionReestablished, ex, null, remote, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, -1, 
						0, 0, 0, null, null, null, null,
						"The connection cannot be reestablished.");
				}

				this.ConnectionFailed(GenuineExceptions.Get_Channel_ConnectionShutDown(ex), xHttpPhysicalConnection.XHttpConnection, xHttpPhysicalConnection);
			}
		}

		#endregion

		#region -- Low-Level operations ------------------------------------------------------------

		private static byte[] _100ContinueContent = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");

		/// <summary>
		/// Sends HTTP 1.1 100 Continue header back to the client.
		/// </summary>
		/// <param name="socket"></param>
		private void LowLevel_Send100Continue(Socket socket)
		{
			socket.Send(_100ContinueContent, 0, _100ContinueContent.Length, SocketFlags.None);
		}

		/// <summary>
		/// Sends HTTP content to the remote host.
		/// Does not process exceptions.
		/// Automatically initiates asynchronous receiving.
		/// Automatically manages stream seqNo for clients' connections.
		/// </summary>
		/// <param name="timeout">Operation timeout.</param>
		/// <param name="message">The message.</param>
		/// <param name="content">The content sent instead of the message.</param>
		/// <param name="exceptionToBeSent">The exception being sent in the response.</param>
		/// <param name="messageContainer">The message container.</param>
		/// <param name="xHttpPhysicalConnection">The physical connection.</param>
		/// <param name="genuineConnectionType">The type of the connection.</param>
		/// <param name="httpPacketType">The type of the HTTP packet.</param>
		/// <param name="repeatSending">Whether the content was already packed.</param>
		/// <param name="synchronous">Whether to send content synchronously.</param>
		/// <param name="startAutomaticReceiving">Indicates whether to start automatic receiving of the response/request if the type of the sending is synchronous.</param>
		/// <param name="applyClss">A boolean value indicating whether the CLSS should be applied.</param>
		private void LowLevel_SendHttpContent(int timeout, Message message, 
			GenuineChunkedStream content, Exception exceptionToBeSent, 
			MessageContainer messageContainer, XHttpPhysicalConnection xHttpPhysicalConnection,
			GenuineConnectionType genuineConnectionType, HttpPacketType httpPacketType, 
			bool repeatSending, bool synchronous, bool startAutomaticReceiving, bool applyClss
			)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			// check whether the connection is valid and available
			xHttpPhysicalConnection.CheckConnectionStatus();

			// prevent the ping
			if (! xHttpPhysicalConnection.IsSender)
				xHttpPhysicalConnection.XHttpConnection.LastTimeContentWasSent = GenuineUtility.TickCount;

			// valid copies
			Socket socket;
			Stream sentContent;

			// to prevent from changing sequence information or disposing sent content during its encryption
			lock (xHttpPhysicalConnection.PhysicalConnectionStateLock)
			{
				if (! repeatSending)
				{
					if (xHttpPhysicalConnection.XHttpConnection.IsClient)
						xHttpPhysicalConnection.SequenceNo ++;

					if (message != null || (message == null && content == null && exceptionToBeSent == null))
					{
						GenuineChunkedStream packedMessages = new GenuineChunkedStream(false);
						MessageCoder.FillInLabelledStream(message, messageContainer, 
							xHttpPhysicalConnection.MessagesBeingSent, packedMessages, 
							xHttpPhysicalConnection.AsyncSendBuffer, 
							(int) this.ITransportContext.IParameterProvider[GenuineParameter.HttpRecommendedPacketSize]);

						// LOG:
						if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Transport] > 0 )
						{
							for ( int i = 0; i < xHttpPhysicalConnection.MessagesBeingSent.Count; i++)
							{
								Message nextMessage = (Message) xHttpPhysicalConnection.MessagesBeingSent[i];

								binaryLogWriter.WriteEvent(LogCategory.Transport, "XHttpConnectionManager.LowLevel_SendHttpContent",
									LogMessageType.MessageIsSentAsynchronously, null, nextMessage, xHttpPhysicalConnection.Remote, null,
									GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, xHttpPhysicalConnection.ConnectionLevelSecurity, null,
									xHttpPhysicalConnection.XHttpConnection.DbgConnectionId, xHttpPhysicalConnection.SequenceNo, 0, 0, null, null, null, null,
									"The message will be sent in the {0} stream N: {1}.", 
									xHttpPhysicalConnection == xHttpPhysicalConnection.XHttpConnection.Sender ? "SENDER" : "LISTENER",
									xHttpPhysicalConnection.SequenceNo);
							}
						}

						xHttpPhysicalConnection.SentContent = packedMessages;
					}
					else if (content != null)
					{
						xHttpPhysicalConnection.MessagesBeingSent.Clear();
						xHttpPhysicalConnection.SentContent = content;
					}
					else //if (exceptionToBeSent != null)
					{
#if DEBUG
						Debug.Assert(httpPacketType == HttpPacketType.SenderError);
#endif

						Exception exception = OperationException.WrapException(exceptionToBeSent);
						GenuineChunkedStream output = new GenuineChunkedStream(false);
						BinaryFormatter binaryFormatter = new BinaryFormatter(new RemotingSurrogateSelector(), new StreamingContext(StreamingContextStates.Other));
						binaryFormatter.Serialize(output, exception);

						xHttpPhysicalConnection.MessagesBeingSent.Clear();
						xHttpPhysicalConnection.SentContent = output;
					}

					// client applies CLSSE only to the actual content
					if (applyClss && xHttpPhysicalConnection.XHttpConnection.IsClient && xHttpPhysicalConnection.ConnectionLevelSecurity != null)
					{
						GenuineChunkedStream encryptedContent = new GenuineChunkedStream(false);
						xHttpPhysicalConnection.ConnectionLevelSecurity.Encrypt(xHttpPhysicalConnection.SentContent, encryptedContent);
						xHttpPhysicalConnection.SentContent = encryptedContent;
					}

					// write the binary header
					GenuineChunkedStream resultStream = new GenuineChunkedStream(false);
					BinaryWriter binaryWriter = new BinaryWriter(resultStream);

					if (xHttpPhysicalConnection.XHttpConnection.IsClient)
						HttpMessageCoder.WriteRequestHeader(binaryWriter, MessageCoder.PROTOCOL_VERSION, genuineConnectionType, xHttpPhysicalConnection.XHttpConnection.HostId, httpPacketType, xHttpPhysicalConnection.SequenceNo, xHttpPhysicalConnection.XHttpConnection.ConnectionName, xHttpPhysicalConnection.XHttpConnection.Remote.LocalHostUniqueIdentifier);
					else
						HttpMessageCoder.WriteResponseHeader(binaryWriter, xHttpPhysicalConnection.XHttpConnection.Remote.ProtocolVersion, this.ITransportContext.ConnectionManager.Local.Uri, xHttpPhysicalConnection.SequenceNo, httpPacketType, xHttpPhysicalConnection.XHttpConnection.Remote.LocalHostUniqueIdentifier);

					resultStream.WriteStream(xHttpPhysicalConnection.SentContent);
					xHttpPhysicalConnection.SentContent = resultStream;

					// while server applies CLSSE to the entire response (except HTTP stuff, of course)
					if (applyClss && ! xHttpPhysicalConnection.XHttpConnection.IsClient && xHttpPhysicalConnection.ConnectionLevelSecurity != null)
					{
						GenuineChunkedStream encryptedContent = new GenuineChunkedStream(false);
						xHttpPhysicalConnection.ConnectionLevelSecurity.Encrypt(xHttpPhysicalConnection.SentContent, encryptedContent);
						xHttpPhysicalConnection.SentContent = encryptedContent;
					}

					// generally it's impossible to have xHttpPhysicalConnection.SentContent without available length in the current implementation
					// nevertheless, it's necessary to calculate the final length of the content if it's unknown
					if (! xHttpPhysicalConnection.SentContent.CanSeek)
					{
						GenuineChunkedStream actualContent = new GenuineChunkedStream(false);
						GenuineUtility.CopyStreamToStream(xHttpPhysicalConnection.SentContent, actualContent, xHttpPhysicalConnection.AsyncSendBuffer);
						xHttpPhysicalConnection.SentContent = actualContent;
					}

					// write the header and compose final content
					resultStream = new GenuineChunkedStream(false);
					StreamWriter streamWriter = new StreamWriter(new NonClosableStream(resultStream), Encoding.ASCII, 3500);

					if (xHttpPhysicalConnection.XHttpConnection.IsClient)
						streamWriter.Write("POST /{0} HTTP/1.1\r\nAccept: */*\r\nContent-Type: application/octet-stream\r\nContent-Length: {1}\r\nUser-Agent: {2}\r\nHost: {3}\r\nConnection: Keep-Alive\r\nPragma: no-cache\r\n\r\n",
							xHttpPhysicalConnection.EntryUri, xHttpPhysicalConnection.SentContent.Length, xHttpPhysicalConnection.XHttpConnection.UserAgent, xHttpPhysicalConnection.LocalEndPoint);
					else
					{
						string now = DateTime.Now.ToString("r");
						streamWriter.Write("HTTP/1.1 200 OK\r\nServer: GXHTTP\r\nDate: {0}\r\nX-Powered-By: Genuine Channels\r\nCache-Control: private\r\nContent-Type: application/octet-stream\r\nContent-Length: {1}\r\n\r\n", 
							now, xHttpPhysicalConnection.SentContent.Length);
					}

					streamWriter.Flush();
					streamWriter.Close();
					resultStream.WriteStream(xHttpPhysicalConnection.SentContent);

					xHttpPhysicalConnection.SentContent = resultStream;
				}
				else
				{
					xHttpPhysicalConnection.SentContent.Position = 0;
				}

				socket = xHttpPhysicalConnection.Socket;
				sentContent = xHttpPhysicalConnection.SentContent;
			}	// lock (xHttpPhysicalConnection.PhysicalConnectionStateLock)

			if (synchronous)
			{
				// send the content
				SyncSocketWritingStream syncSocketWritingStream = new SyncSocketWritingStream(this, socket, timeout, xHttpPhysicalConnection.XHttpConnection.DbgConnectionId, xHttpPhysicalConnection.Remote);
				GenuineUtility.CopyStreamToStreamPhysically(sentContent, syncSocketWritingStream, xHttpPhysicalConnection.AsyncSendBuffer);

				// automatically start receiving the response/request
				if (startAutomaticReceiving)
				{
					if (xHttpPhysicalConnection.XHttpConnection.IsClient)
						this.LowLevel_HalfSync_Client_StartReceiving(xHttpPhysicalConnection);
					else
						this.LowLevel_HalfSync_Server_StartReceiving(xHttpPhysicalConnection.Socket);
				}
			}
			else
			{
				xHttpPhysicalConnection.AsyncSendBufferCurrentPosition = 0;
				xHttpPhysicalConnection.AsyncSendBufferIsLastPacket = false;
				xHttpPhysicalConnection.AsyncSendBufferSizeOfValidContent = 0;
				xHttpPhysicalConnection.AsyncSendStream = sentContent;

				this.LowLevel_InitiateAsyncSending(xHttpPhysicalConnection);
			}
		}

		/// <summary>
		/// Is used to transfer connection and socket parameters to the working thread.
		/// </summary>
		private class PhysicalConnectionAndSocket
		{
			public XHttpPhysicalConnection XHttpPhysicalConnection;
			public Socket Socket;

			/// <summary>
			/// Constructs an instance of the PhysicalConnectionAndSocket class.
			/// </summary>
			/// <param name="xHttpPhysicalConnection">The connection.</param>
			/// <param name="socket">The socket.</param>
			public PhysicalConnectionAndSocket(XHttpPhysicalConnection xHttpPhysicalConnection, Socket socket)
			{
				this.Socket = socket;
				this.XHttpPhysicalConnection = xHttpPhysicalConnection;
			}
		}

		/// <summary>
		/// Sends the error to the remote host.
		/// </summary>
		/// <param name="PhysicalConnectionAndSocketAsObject">The context.</param>
		public void LowLevel_SendServerError(object PhysicalConnectionAndSocketAsObject)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			try
			{
				PhysicalConnectionAndSocket physicalConnectionAndSocket = (PhysicalConnectionAndSocket) PhysicalConnectionAndSocketAsObject;

				try
				{
					// send the response
					int timeout = GenuineUtility.GetTimeout(30000);
					SyncSocketWritingStream syncSocketWritingStream = new SyncSocketWritingStream(this, physicalConnectionAndSocket.Socket, timeout, physicalConnectionAndSocket.XHttpPhysicalConnection.XHttpConnection.DbgConnectionId, physicalConnectionAndSocket.XHttpPhysicalConnection.Remote);

					StreamWriter streamWriter = new StreamWriter(new NonClosableStream(syncSocketWritingStream), Encoding.ASCII, 3500);
					streamWriter.WriteLine("HTTP/1.1 409 Conflict\r\nServer: GXHTTP\r\nDate: {0}\r\nX-Powered-By: Genuine Channels\r\nContent-Length: 0\r\n\r\n");
					streamWriter.Flush();
					streamWriter.Close();

					binaryLogWriter.WriteEvent(LogCategory.Transport, "XHttpConnectionManager.LowLevel_SendServerError",
						LogMessageType.SynchronousSendingFinished, GenuineExceptions.Get_Debugging_GeneralWarning("409 Conflict HTTP response has been sent."),
						null, null, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, -1,
						0, 0, 0, null, null, null, null,
						"409 Conflict HTTP response has been sent.");
				}
				catch (Exception ex)
				{
					binaryLogWriter.WriteEvent(LogCategory.Transport, "XHttpConnectionManager.LowLevel_SendServerError",
						LogMessageType.SynchronousSendingFinished, ex,
						null, null, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, -1,
						0, 0, 0, null, null, null, null,
						"409 Conflict HTTP response cannot be sent.");
				}
				finally
				{
					SocketUtility.CloseSocket(physicalConnectionAndSocket.Socket);
				}
			}
			catch
			{
			}
		}

		/// <summary>
		/// Initiates sending through the physical asynchronously.
		/// </summary>
		/// <param name="xHttpPhysicalConnection">The physical connection.</param>
		/// <returns>True if the sending was initiated. False if the specified physical connection contains no data to send.</returns>
		private bool LowLevel_InitiateAsyncSending(XHttpPhysicalConnection xHttpPhysicalConnection)
		{
			// initiate the sending
			xHttpPhysicalConnection.AsyncSendBufferCurrentPosition = 0;
			xHttpPhysicalConnection.AsyncSendBufferSizeOfValidContent = GenuineUtility.TryToReadFromStream(xHttpPhysicalConnection.AsyncSendStream, xHttpPhysicalConnection.AsyncSendBuffer, 0, xHttpPhysicalConnection.AsyncSendBuffer.Length);
			if (xHttpPhysicalConnection.AsyncSendBufferSizeOfValidContent == 0)
				return false;
			xHttpPhysicalConnection.AsyncSendBufferIsLastPacket = xHttpPhysicalConnection.AsyncSendBufferSizeOfValidContent < xHttpPhysicalConnection.AsyncSendBuffer.Length;

			// LOG:
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Transport] > 0 )
				binaryLogWriter.WriteEvent(LogCategory.Transport, "XHttpConnectionManager.LowLevel_InitiateAsyncSending",
					LogMessageType.AsynchronousSendingStarted, null, null, xHttpPhysicalConnection.Remote, null,
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
					null, null, 
					xHttpPhysicalConnection.XHttpConnection.DbgConnectionId, 0, 0, 0, null, null, null, null,
					"The connection has been obtained and the stream is being sent asynchronously.");

			AsyncThreadStarter.QueueTask(new Async_InitiateSocketSending(xHttpPhysicalConnection.Socket, xHttpPhysicalConnection.AsyncSendBuffer, 
				xHttpPhysicalConnection.AsyncSendBufferCurrentPosition, 
				xHttpPhysicalConnection.AsyncSendBufferSizeOfValidContent - xHttpPhysicalConnection.AsyncSendBufferCurrentPosition, 
				this._AsyncSending_onEndSending, xHttpPhysicalConnection));

			return true;
		}

		/// <summary>
		/// Completes async sending.
		/// </summary>
		/// <param name="ar">The result of the asynchronous call.</param>
		private void LowLevel_OnEndAsyncSending(IAsyncResult ar)
		{
			XHttpPhysicalConnection xHttpPhysicalConnection = null;

			try
			{
				// get the result of the sending
				xHttpPhysicalConnection = (XHttpPhysicalConnection) ar.AsyncState;
				int bytesSent = xHttpPhysicalConnection.Socket.EndSend(ar);

				// advance buffer position
				this.IncreaseBytesSent(bytesSent);
				xHttpPhysicalConnection.AsyncSendBufferCurrentPosition += bytesSent;

				if (xHttpPhysicalConnection.AsyncSendBufferCurrentPosition < xHttpPhysicalConnection.AsyncSendBufferSizeOfValidContent)
				{
					// continue sending
					AsyncThreadStarter.QueueTask(new Async_InitiateSocketSending(xHttpPhysicalConnection.Socket, 
						xHttpPhysicalConnection.AsyncSendBuffer, 
						xHttpPhysicalConnection.AsyncSendBufferCurrentPosition, 
						xHttpPhysicalConnection.AsyncSendBufferSizeOfValidContent - xHttpPhysicalConnection.AsyncSendBufferCurrentPosition, 
						this._AsyncSending_onEndSending, xHttpPhysicalConnection));
					return ;
				}

				// check whether it is possible to continue sending
				if (this.LowLevel_InitiateAsyncSending(xHttpPhysicalConnection))
					return ;

				// ok, now we need to receive HTTP response or request
				if (xHttpPhysicalConnection.XHttpConnection.IsClient)
					this.LowLevel_HalfSync_Client_StartReceiving(xHttpPhysicalConnection);
				else
					this.LowLevel_HalfSync_Server_StartReceiving(xHttpPhysicalConnection.Socket);
			}
			catch(Exception ex)
			{
				this.ConnectionFailed(ex, xHttpPhysicalConnection.XHttpConnection, xHttpPhysicalConnection);
			}
		}

		/// <summary>
		/// Answers a line read from the specified stream and finished with CR LF byte sequence.
		/// </summary>
		/// <param name="stream">The source stream.</param>
		/// <param name="indexOfFirstDigit">The index of the first found digit in the line.</param>
		/// <param name="indexOfLastDigit">The index of the last found digit in the line.</param>
		/// <returns>A line read from the specified stream and finished with CR LF byte sequence.</returns>
		private string LowLevel_ReadToUpperLine(Stream stream, out int indexOfFirstDigit, out int indexOfLastDigit)
		{
			indexOfFirstDigit = -1;
			indexOfLastDigit = -1;
			StringBuilder stringBuilder = new StringBuilder(50);

			for ( ; ; )
			{
				int nextByte = stream.ReadByte();
				if (nextByte == (short) '\r')
					continue;
				if (nextByte == (short) '\n')
					break;

				// analyze the symbol
				char nextSymbol = (char) (short) nextByte;
				if (Char.IsDigit(nextSymbol) && indexOfFirstDigit < 0)
					indexOfFirstDigit = stringBuilder.Length;
				if (Char.IsDigit(nextSymbol) && indexOfFirstDigit >= 0)
					indexOfLastDigit = stringBuilder.Length;

				// and put it down
				stringBuilder.Append(Char.ToUpper(nextSymbol));
			}

			return stringBuilder.ToString();
		}

		/// <summary>
		/// Parses HTTP request or response.
		/// </summary>
		/// <param name="timeout">The reading timeout.</param>
		/// <param name="client">Specifies the parsing logic.</param>
		/// <param name="socket">The connection.</param>
		/// <param name="connectionLevelSecurity">Connection-level Security Session.</param>
		/// <param name="protocolVersion">The version of the protocol.</param>
		/// <param name="uri">The URI of the remote host.</param>
		/// <param name="sequenceNo">The sequence no of the parsing packet.</param>
		/// <param name="genuineConnectionType">The type of the connection.</param>
		/// <param name="hostId">The identifier of the host.</param>
		/// <param name="httpPacketType">The type of the HTTP packet.</param>
		/// <param name="connectionName">The name of the connection.</param>
		/// <param name="remoteHostUniqueIdentifier">The unique identifier of the HostInformation used by the remote host.</param>
		/// <returns>A stream based on the specified socket.</returns>
		private Stream LowLevel_ParseHttpContent(int timeout, bool client, Socket socket, SecuritySession connectionLevelSecurity, out byte protocolVersion, out string uri, out int sequenceNo, out GenuineConnectionType genuineConnectionType, out Guid hostId, out HttpPacketType httpPacketType, out string connectionName, out int remoteHostUniqueIdentifier)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			protocolVersion = MessageCoder.PROTOCOL_VERSION;
			uri = null;
			genuineConnectionType = GenuineConnectionType.None;
			hostId = Guid.Empty;
			httpPacketType = HttpPacketType.Unkown;
			connectionName = null;

			// read the entire header
			Stream inputStream = new BufferedStream(new SyncSocketStream(socket, this, timeout));

			int contentLength = -1;

#if DEBUG
			GenuineChunkedStream httpHeadersContent = new GenuineChunkedStream(false);
			StreamWriter httpTraffic = new StreamWriter(httpHeadersContent);
#endif

			GXHTTPHeaderParser gxhttpHeaderParser = new GXHTTPHeaderParser(client);
			int indexOfFirstDigit;
			int indexOfLastDigit;

			for ( ; ; )
			{
				string line = this.LowLevel_ReadToUpperLine(inputStream, out indexOfFirstDigit, out indexOfLastDigit);

#if DEBUG
				httpTraffic.Write(line);
				httpTraffic.Write(Environment.NewLine);
#endif

				if (line.Length <= 0)
					break;

				if (gxhttpHeaderParser.ParseHeader(line, indexOfFirstDigit, indexOfLastDigit) == GXHTTPHeaderParser.HeaderFields.Expect100Continue && gxhttpHeaderParser.IsHttp11)
					this.LowLevel_Send100Continue(socket);
			}

			contentLength = (int) gxhttpHeaderParser.ContentLength;

#if DEBUG
			// LOG:
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.LowLevelTransport] > 0 )
			{
				binaryLogWriter.WriteTransportContentEvent(LogCategory.LowLevelTransport, "XHttpConnectionManager.LowLevel_ParseHttpContent",
					LogMessageType.LowLevelTransport_SyncReceivingCompleted, null, null, null, 
					binaryLogWriter[LogCategory.LowLevelTransport] > 1 ? httpHeadersContent : null, 
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
					-1, contentLength, socket.RemoteEndPoint.ToString(),
					null, null,
					"XHTTP Headers parsed. Content Length: {0}.", contentLength);
			}

			httpHeadersContent.Dispose();
#endif

			// ok, the header has been successfully skipped
			if (contentLength < 0)
				throw GenuineExceptions.Get_Receive_IncorrectData();

			// TODO: fix this! there is socket -> buffer -> delimiter chain, while it must be socket -> delimiter -> buffer
			// and we know the content length
			inputStream = new DelimiterStream(inputStream, contentLength);

			// LOG:
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.LowLevelTransport] > 0 )
			{
				GenuineChunkedStream contentCopy = null;
				bool writeContent = binaryLogWriter[LogCategory.LowLevelTransport] > 1;
				if (writeContent)
				{
					contentCopy = new GenuineChunkedStream(false);
					GenuineUtility.CopyStreamToStream(inputStream, contentCopy, contentLength);
				}

				binaryLogWriter.WriteTransportContentEvent(LogCategory.LowLevelTransport, "XHttpConnectionManager.LowLevel_ParseHttpContent",
					LogMessageType.LowLevelTransport_SyncReceivingCompleted, null, null, null, 
					writeContent ? contentCopy : null, 
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
					-1, contentLength, socket.RemoteEndPoint.ToString(),
					null, null,
					"XHTTP Message.", contentLength);

				if (writeContent)
					inputStream = contentCopy;
			}

			// server always encrypts the entire content, including response headers
			if (client && connectionLevelSecurity != null && connectionLevelSecurity.IsEstablished)
				inputStream = connectionLevelSecurity.Decrypt(inputStream);

			BinaryReader binaryReader = new BinaryReader(inputStream);

			// read binary header
			if (client)
				HttpMessageCoder.ReadResponseHeader(binaryReader, out uri, out sequenceNo, out httpPacketType, out remoteHostUniqueIdentifier);
			else
				HttpMessageCoder.ReadRequestHeader(binaryReader, out protocolVersion, out genuineConnectionType, out hostId, out httpPacketType, out sequenceNo, out connectionName, out remoteHostUniqueIdentifier);

			return inputStream;
		}

		/// <summary>
		/// Reads messages from the stream and processes them.
		/// </summary>
		/// <param name="stream">The source stream.</param>
		/// <param name="xHttpPhysicalConnection">The connection.</param>
		private void LowLevel_ParseLabelledStream(Stream stream, XHttpPhysicalConnection xHttpPhysicalConnection)
		{
			BinaryReader binaryReader = new BinaryReader(stream);

			while ( binaryReader.ReadByte() == 0 )
			{
				using (LabelledStream labelledStream = new LabelledStream(this.ITransportContext, stream, xHttpPhysicalConnection.AsyncReceiveBuffer))
				{
					GenuineChunkedStream receivedContent = new GenuineChunkedStream(true);
					GenuineUtility.CopyStreamToStream(labelledStream, receivedContent);
					this.ITransportContext.IIncomingStreamHandler.HandleMessage(receivedContent, xHttpPhysicalConnection.Remote, xHttpPhysicalConnection.XHttpConnection.GenuineConnectionType, xHttpPhysicalConnection.XHttpConnection.ConnectionName, xHttpPhysicalConnection.XHttpConnection.DbgConnectionId, false, null, xHttpPhysicalConnection.ConnectionLevelSecurity, null);
				}
			}
		}

		/// <summary>
		/// Answers an exception deserialized.
		/// </summary>
		/// <param name="stream">The input stream.</param>
		/// <returns>The exception.</returns>
		private Exception LowLevel_ParseException(Stream stream)
		{
			try
			{
				BinaryFormatter binaryFormatter = new BinaryFormatter(new RemotingSurrogateSelector(), new StreamingContext(StreamingContextStates.Other));
				Exception exception = binaryFormatter.Deserialize(stream) as Exception;
				if (exception == null)
					return GenuineExceptions.Get_Receive_IncorrectData();
				return exception;
			}
			catch
			{
				return GenuineExceptions.Get_Receive_IncorrectData();
			}
		}

		/// <summary>
		/// Initiates receiving of the message's header.
		/// </summary>
		/// <param name="xHttpPhysicalConnection">The physical connection.</param>
		internal void LowLevel_HalfSync_Client_StartReceiving(XHttpPhysicalConnection xHttpPhysicalConnection)
		{
			// LOG:
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Transport] > 0 )
			{
				binaryLogWriter.WriteEvent(LogCategory.Transport, "XHttpConnectionManager.LowLevel_HalfSync_Client_StartReceiving",
					LogMessageType.ReceivingStarted, null, null, xHttpPhysicalConnection.Remote, null,
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
					xHttpPhysicalConnection.ConnectionLevelSecurity, 
					xHttpPhysicalConnection.ConnectionLevelSecurity == null ? null : xHttpPhysicalConnection.ConnectionLevelSecurity.Name,
					xHttpPhysicalConnection.XHttpConnection.DbgConnectionId, 0, 0, 0, null, null, null, null,
					"The asynchronous receiving is initiated.");
			}

			// check the connection
			xHttpPhysicalConnection.CheckConnectionStatus();

			// and initiate receiving
			AsyncThreadStarter.QueueTask(new Async_InitiateSocketReceiving(xHttpPhysicalConnection.Socket, 
				this._unusedBuffer, 0, 
				1, 
				_HalfSync_Client_onEndReceiving, xHttpPhysicalConnection));
		}

		/// <summary>
		/// Processes the received content.
		/// </summary>
		/// <param name="ar">The result of the asynchronous invocation.</param>
		private void LowLevel_HalfSync_Client_EndReceiving(IAsyncResult ar)
		{
			XHttpPhysicalConnection xHttpPhysicalConnection = (XHttpPhysicalConnection) ar.AsyncState;

			try
			{
				int bytesReceived = xHttpPhysicalConnection.Socket.EndReceive(ar);

				// if the connection was closed
				if (bytesReceived == 0)
					throw GenuineExceptions.Get_Receive_Portion();

				this.ITransportContext.ConnectionManager.IncreaseBytesReceived(bytesReceived);
				xHttpPhysicalConnection.XHttpConnection.Renew();
				GenuineThreadPool.QueueUserWorkItem(_HalfSync_Client_onContinueReceiving, xHttpPhysicalConnection, true);
			}
			catch(Exception ex)
			{
				this.ConnectionFailed(ex, xHttpPhysicalConnection.XHttpConnection, xHttpPhysicalConnection);
			}
		}

		/// <summary>
		/// Initiates receiving of the message's header.
		/// </summary>
		/// <param name="socket">The connection.</param>
		internal void LowLevel_HalfSync_Server_StartReceiving(Socket socket)
		{
			// initiate receiving
			AsyncThreadStarter.QueueTask(new Async_InitiateSocketReceiving(socket, 
				this._unusedBuffer, 0, 1, 
				_HalfSync_Server_onEndReceiving, socket));
		}

		/// <summary>
		/// Processes the received content.
		/// </summary>
		/// <param name="ar">The result of the asynchronous invocation.</param>
		private void LowLevel_HalfSync_Server_EndReceiving(IAsyncResult ar)
		{
			try
			{
				Socket socket = (Socket) ar.AsyncState;

				try
				{
					int bytesReceived = socket.EndReceive(ar);

					// if the connection is closed
					if (bytesReceived == 0)
						throw GenuineExceptions.Get_Receive_Portion();

					this.ITransportContext.ConnectionManager.IncreaseBytesReceived(bytesReceived);

//					this.LowLevel_Send100Continue(socket);

//					this.Pool_Server_ContinueHalfSyncReceiving(socket);
					GenuineThreadPool.QueueUserWorkItem(_HalfSync_Server_onContinueReceiving, socket, true);
				}
				catch
				{
					SocketUtility.CloseSocket(socket);
				}
			}
			catch (Exception ex)
			{
				// LOG:
				BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
				if ( binaryLogWriter != null )
				{
					binaryLogWriter.WriteImplementationWarningEvent("XHttpConnectionManager.LowLevel_HalfSync_Server_EndReceiving",
						LogMessageType.Error, ex, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
						"Something must be fixed...");
				}
			}
		}

		#endregion

		#region -- Connection Establishing ---------------------------------------------------------

		/// <summary>
		/// Creates and registers a logical connection to the specified host.
		/// </summary>
		/// <param name="remote">The remote host.</param>
		/// <param name="primaryUri">The primary uri of the remote host.</param>
		/// <param name="isClient">The type of the behavior applying to the connection.</param>
		/// <param name="connectionName">Connection name.</param>
		/// <returns>The created connection.</returns>
		private XHttpConnection Pool_CreateConnection(HostInformation remote, string primaryUri, bool isClient, string connectionName)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			if (connectionName == null)
				connectionName = this.GetUniqueConnectionName();

			using (new ReaderAutoLocker(this._disposeLock))
			{
				XHttpConnection xHttpConnection = new XHttpConnection(this, isClient, connectionName);
				xHttpConnection.Remote = remote;
				xHttpConnection.CloseConnectionAfterInactivity = GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.ClosePersistentConnectionAfterInactivity]);
				xHttpConnection.GenuineConnectionType = GenuineConnectionType.Persistent;

				xHttpConnection.Sender = new XHttpPhysicalConnection(xHttpConnection, true);
				xHttpConnection.Listener = new XHttpPhysicalConnection(xHttpConnection, false);

				xHttpConnection.MessageContainer = new MessageContainer(this.ITransportContext);

				this._persistent.Set(primaryUri, connectionName, xHttpConnection);

				// and CLSS
				string securitySessionName = this.ITransportContext.IParameterProvider[GenuineParameter.SecuritySessionForPersistentConnections] as string;
				if (securitySessionName != null)
				{
					xHttpConnection.Sender.ConnectionLevelSecurity = this.ITransportContext.IKeyStore.GetKey(securitySessionName).CreateSecuritySession(securitySessionName, null);
					xHttpConnection.Listener.ConnectionLevelSecurity = this.ITransportContext.IKeyStore.GetKey(securitySessionName).CreateSecuritySession(securitySessionName, null);
				}

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteConnectionParameterEvent(LogCategory.Connection, "XHttpConnectionManager.Pool_CreateConnection",
						LogMessageType.ConnectionParameters, null, remote, this.ITransportContext.IParameterProvider,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, xHttpConnection.DbgConnectionId, 
						"An XHTTP connection is being established.");
				}

				return xHttpConnection;
			}
		}


		/// <summary>
		/// Establishes a connection to the remote host.
		/// </summary>
		/// <param name="remote">The remote host.</param>
		/// <param name="connectionName">The name of the connection.</param>
		/// <returns>The established connection.</returns>
		internal XHttpConnection Pool_EstablishPersistentConnection(HostInformation remote, string connectionName)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			using (new ReaderAutoLocker(this._disposeLock))
			{
				if (this._disposed)
					throw OperationException.WrapException(this._disposeReason);
			}

//			remoteUri = null;
			Stream inputStream = null;

			// the time we should finish connection establishing before
			int timeout = GenuineUtility.GetTimeout( (TimeSpan) this.ITransportContext.IParameterProvider[GenuineParameter.ConnectTimeout]);

			XHttpConnection xHttpConnection = this.Pool_CreateConnection(remote, remote.Url, true, connectionName);
			xHttpConnection.Sender.CheckConnectionStatus();

			// get connection-level SS
			string connectionLevelSSName = this.ITransportContext.IParameterProvider[GenuineParameter.SecuritySessionForPersistentConnections] as string;
			if (connectionLevelSSName != null)
			{
				xHttpConnection.Sender.ConnectionLevelSecurity = this.ITransportContext.IKeyStore.GetKey(connectionLevelSSName).CreateSecuritySession(connectionLevelSSName, null);
				xHttpConnection.Listener.ConnectionLevelSecurity = this.ITransportContext.IKeyStore.GetKey(connectionLevelSSName).CreateSecuritySession(connectionLevelSSName, null);
			}

			// establish it
			// gather the output stream
			Stream senderInputClsseStream = Stream.Null;
			Stream listenerInputClsseStream = Stream.Null;
			GenuineChunkedStream clsseStream = null;
			bool resendContent = false;

			for (;;)
			{
				bool clssShouldBeSent = false;

				if (GenuineUtility.IsTimeoutExpired(timeout))
					throw GenuineExceptions.Get_Connect_CanNotConnectToRemoteHost(remote.Url, "Timeout expired.");

				if (! resendContent)
				{
					// the CLSS content
					clsseStream = new GenuineChunkedStream();
					BinaryWriter binaryWriter = new BinaryWriter(clsseStream);
					clssShouldBeSent = xHttpConnection.GatherContentOfConnectionLevelSecuritySessions(senderInputClsseStream, 
						listenerInputClsseStream, clsseStream, xHttpConnection.Sender.ConnectionLevelSecurity, xHttpConnection.Listener.ConnectionLevelSecurity);
				}
				else
					clsseStream.Position = 0;

				try
				{
					// send the request
					this.LowLevel_SendHttpContent(timeout, null, clsseStream, null, null, xHttpConnection.Sender, GenuineConnectionType.Persistent,
						HttpPacketType.Establishing, resendContent, true, false, false);

					// parse the response
					byte protocolVersion;
					string remoteUri;
					int sequenceNo;
					GenuineConnectionType genuineConnectionType;
					Guid connectionId;
					HttpPacketType httpPacketType;
					string receivedConnectionName;
					int remoteHostUniqueIdentifier;
					inputStream = this.LowLevel_ParseHttpContent(timeout, true, xHttpConnection.Sender.Socket, 
						xHttpConnection.Sender.ConnectionLevelSecurity, out protocolVersion, out remoteUri, out sequenceNo, out genuineConnectionType, out connectionId, out httpPacketType, out receivedConnectionName, out remoteHostUniqueIdentifier);

					// check received items
					if ( httpPacketType == HttpPacketType.SenderError )
						throw GenuineExceptions.Get_Connect_CanNotConnectToRemoteHost(remote.ToString(), this.LowLevel_ParseException(inputStream).Message);

					if ( httpPacketType != HttpPacketType.Establishing )
						throw GenuineExceptions.Get_Connect_CanNotConnectToRemoteHost(remote.ToString(), "Wrong response received from the remote host.");

					// check the restartion if either CLSS or the persistent connection is used
					remote.UpdateUri(remoteUri, remoteHostUniqueIdentifier);

					// receive CLSS data
					using (inputStream)
					{
						BinaryReader binaryReader = new BinaryReader(inputStream);

						int writtenSize = binaryReader.ReadInt32();
						senderInputClsseStream = new GenuineChunkedStream(true);
						GenuineUtility.CopyStreamToStream(inputStream, senderInputClsseStream, writtenSize);

						writtenSize = binaryReader.ReadInt32();
						listenerInputClsseStream = new GenuineChunkedStream(true);
						GenuineUtility.CopyStreamToStream(inputStream, listenerInputClsseStream, writtenSize);
					}

					resendContent = false;
				}
				catch(Exception ex)
				{
					// LOG:
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
					{
						binaryLogWriter.WriteEvent(LogCategory.Connection, "XHttpConnectionManager.Pool_EstablishPersistentConnection",
							LogMessageType.SynchronousSendingFinished, ex, null, remote, null,
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
							xHttpConnection.Sender.ConnectionLevelSecurity, connectionLevelSSName, 
							xHttpConnection.DbgConnectionId, 0, 0, 0, null, null, null, null,
							"Cannot send the establishing packet to the remote host.");
					}

					if (ConnectionManager.IsExceptionCritical(ex as OperationException))
						throw;

					// force content resending
					resendContent = true;
					xHttpConnection.Sender.CloseSocket();
					continue;
				}

				if (xHttpConnection.Sender.ConnectionLevelSecurity != null && ! xHttpConnection.Sender.ConnectionLevelSecurity.IsEstablished)
					continue;
				if (xHttpConnection.Listener.ConnectionLevelSecurity != null && ! xHttpConnection.Listener.ConnectionLevelSecurity.IsEstablished)
					continue;
				if (clssShouldBeSent)
					continue;

				break;
			}

			// LOG:
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.HostInformation] > 0 )
			{
				binaryLogWriter.WriteHostInformationEvent("XHttpConnectionManager.Pool_EstablishPersistentConnection",
					LogMessageType.HostInformationCreated, null, remote,
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
					xHttpConnection.Sender.ConnectionLevelSecurity, connectionLevelSSName, 
					xHttpConnection.DbgConnectionId,
					"HostInformation is ready for actions.");
			}

			xHttpConnection.Remote = remote;
			xHttpConnection.Sender.MarkAsAvailable();

			// LOG:
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
			{
				binaryLogWriter.WriteEvent(LogCategory.Connection, "XHttpConnectionManager.Pool_EstablishPersistentConnection",
					LogMessageType.ConnectionEstablished, null, null, xHttpConnection.Remote, null,
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
					xHttpConnection.Sender.ConnectionLevelSecurity, connectionLevelSSName, 
					xHttpConnection.DbgConnectionId, (int) GenuineConnectionType.Persistent, 0, 0, this.GetType().Name, remote.LocalPhysicalAddress.ToString(), remote.PhysicalAddress.ToString(), null,
					"The connection to the remote host is established.");
			}

			// try to start the listener
			try
			{
				this.Pool_HandleClientConnection(xHttpConnection.Listener);
			}
			catch(Exception ex)
			{
				this.ConnectionFailed(ex, xHttpConnection, xHttpConnection.Listener);
			}

			return xHttpConnection;
		}


		#endregion

		#region -- Disposing -----------------------------------------------------------------------

		/// <summary>
		/// Releases all resources.
		/// </summary>
		/// <param name="reason">The reason of disposing.</param>
		public override void InternalDispose(Exception reason)
		{
			this.ReleaseConnections(null, GenuineConnectionType.All, reason);

			object[] listeningEntries = null;
			lock (this._listeningSockets.SyncRoot)
			{
				listeningEntries = new object[this._listeningSockets.Count];
				this._listeningSockets.Keys.CopyTo(listeningEntries, listeningEntries.Length);
			}

			foreach (object listeningEntry in listeningEntries)
			{
				try
				{
					this.StopListening(listeningEntry);
				}
				catch
				{
				}
			}
		}


		#endregion

		#region -- Listening -----------------------------------------------------------------------

		/// <summary>
		/// The local port being listened.
		/// </summary>
		public int LocalPort = -1;

		/// <summary>
		/// Listening sockets. End Point => AcceptConnectionClosure.
		/// </summary>
		private Hashtable _listeningSockets = Hashtable.Synchronized(new Hashtable());

		/// <summary>
		/// Starts listening to the specified end point and accepting incoming connections.
		/// </summary>
		/// <param name="endPointAsObject">The end point.</param>
		public override void StartListening(object endPointAsObject)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			using (new ReaderAutoLocker(this._disposeLock))
			{
				if (this._disposed)
					throw OperationException.WrapException(this._disposeReason);
			}

			// parse the listening end point
			string endPoint = endPointAsObject.ToString();
			int port;
			string interfaceAddress = GenuineUtility.SplitToHostAndPort(endPoint, out port);

			if (interfaceAddress == null || port < 0 || port > 65535)
				throw GenuineExceptions.Get_Server_IncorrectAddressToListen(endPoint);

			if (_listeningSockets.ContainsKey(endPoint))
				throw GenuineExceptions.Get_Server_EndPointIsAlreadyBeingListenedTo(endPoint);

			IPEndPoint ipEndPoint = new IPEndPoint(GenuineUtility.ResolveIPAddress(interfaceAddress), port);

			// start socket listening
			Socket socket = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

			try
			{
				// set linger option
				LingerOption lingerOption = new LingerOption(true, 3);
				socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, lingerOption);

				socket.Bind(ipEndPoint);
				socket.Listen(15);

				AcceptConnectionClosure acceptConnectionClosure = new AcceptConnectionClosure(this.ITransportContext, endPoint, socket, this);

				// register the listening closure
				lock (this._listeningSockets.SyncRoot)
				{
					if (_listeningSockets.ContainsKey(endPoint))
					{
						SocketUtility.CloseSocket(socket);
						throw GenuineExceptions.Get_Server_EndPointIsAlreadyBeingListenedTo(endPoint);
					}
					_listeningSockets[endPoint] = acceptConnectionClosure;
				}

				this.ITransportContext.IGenuineEventProvider.Fire(new GenuineEventArgs(GenuineEventType.GeneralListenerStarted, null, this.Local, endPoint));

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.AcceptingConnection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.AcceptingConnection, "XHttpConnectionManager.StartListening",
						LogMessageType.ListeningStarted, null, null, null, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, -1, 0, 0, 0, endPoint, null, null, null,
						"A listening socket has been associated with the \"{0}\" local end point.", endPoint);
				}

				Thread thread = new Thread(new ThreadStart(acceptConnectionClosure.AcceptConnections));
				thread.IsBackground = true;
				thread.Start();
			}
			catch(Exception ex)
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.AcceptingConnection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.AcceptingConnection, "XHttpConnectionManager.StartListening",
						LogMessageType.ListeningStarted, ex, null, null, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, -1, 0, 0, 0, endPoint, null, null, null,
						"Can't associate a socket with the specified end point \"{0}\".", endPoint);
				}

				SocketUtility.CloseSocket(socket);
				throw;
			}

			this.LocalPort = ((IPEndPoint) socket.LocalEndPoint).Port;
		}

		/// <summary>
		/// Stops listening to the specified end point. Does not close any connections.
		/// </summary>
		/// <param name="endPointAsObject">The end point.</param>
		public override void StopListening(object endPointAsObject)
		{
			string endPoint = (string) endPointAsObject;
			if (! this._listeningSockets.ContainsKey(endPoint))
				return ;

			AcceptConnectionClosure acceptConnectionClosure = (AcceptConnectionClosure) this._listeningSockets[endPoint];

			// recalculate the local port
			if (this.LocalPort == ((IPEndPoint) acceptConnectionClosure.Socket.LocalEndPoint).Port)
			{
				lock (this._listeningSockets.SyncRoot)
					foreach (DictionaryEntry entry in this._listeningSockets)
					{
						if (entry.Key.ToString() == endPoint)
							continue;

						// I don't care about interfaces here,
						// probably it would be better to find 0.0.0.0 interface here
						AcceptConnectionClosure anotherConnectionClosure = (AcceptConnectionClosure) entry.Value;
						this.LocalPort = ((IPEndPoint) anotherConnectionClosure.Socket.LocalEndPoint).Port;
						break;
					}
			}

			// shut down the thread
			acceptConnectionClosure.StopListening.Set();
			SocketUtility.CloseSocket(acceptConnectionClosure.Socket);

			this._listeningSockets.Remove(endPoint);

			this.ITransportContext.IGenuineEventProvider.Fire(new GenuineEventArgs(GenuineEventType.GeneralListenerShutDown, null, this.Local, endPoint));

			// LOG:
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.AcceptingConnection] > 0 )
			{
				binaryLogWriter.WriteEvent(LogCategory.AcceptingConnection, "XHttpConnectionManager.StopListening",
					LogMessageType.ListeningStopped, null, null, null, null, 
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
					null, null, -1, 0, 0, 0, endPoint, null, null, null,
					"A listening socket is no longer associated with the \"{0}\" local end point.", endPoint);
			}
		}

		/// <summary>
		/// Accepts the connection.
		/// </summary>
		/// <param name="clientSocket">The socket.</param>
		void IAcceptConnectionConsumer.AcceptConnection(Socket clientSocket)
		{
//			clientSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoDelay, 1);
			this.LowLevel_HalfSync_Server_StartReceiving(clientSocket);
		}

		/// <summary>
		/// Gets an indication whether the Connection Manager has been disposed.
		/// </summary>
		/// <returns></returns>
		public bool IsDisposed()
		{
			return this._disposed;
		}

		#endregion

		#region -- Timer ---------------------------------------------------------------------------

		/// <summary>
		/// Closes expired connections and sends ping via inactive connections.
		/// </summary>
		public void TimerCallback()
		{
			GenuineThreadPool.QueueUserWorkItem(_internal_TimerCallback, null, false);
		}
		private WaitCallback _internal_TimerCallback;

		private PersistentConnectionStorage.ProcessConnectionEventHandler _internal_TimerCallback_InspectPersistentConnections;
		private class Internal_TimerCallback_Parameters
		{
			public ArrayList ExpiredConnections;
			public ArrayList SendPingTo;

			public int SendPingAfter;
			public int Now;
		}

		/// <summary>
		/// Finds connections to be released.
		/// </summary>
		/// <param name="xHttpConnectionAsObject">The connection.</param>
		/// <param name="parametersAsObject">Stuff to make decisions and to save the results.</param>
		private void Internal_TimerCallback_InspectPersistentConnections(object xHttpConnectionAsObject, object parametersAsObject)
		{
			XHttpConnection xHttpConnection = (XHttpConnection) xHttpConnectionAsObject;
			Internal_TimerCallback_Parameters parameters = (Internal_TimerCallback_Parameters) parametersAsObject;

			if (GenuineUtility.IsTimeoutExpired(xHttpConnection.ShutdownTime, parameters.Now))
				parameters.ExpiredConnections.Add(xHttpConnection);
			if (GenuineUtility.IsTimeoutExpired(xHttpConnection.LastTimeContentWasSent + parameters.SendPingAfter, parameters.Now))
				parameters.SendPingTo.Add(xHttpConnection);
		}

		/// <summary>
		/// Closes expired connections and sends ping via inactive connections.
		/// </summary>
		/// <param name="ignored">Ignored.</param>
		private void Internal_TimerCallback(object ignored)
		{
			int now = GenuineUtility.TickCount;
			int sendPingAfter = GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.PersistentConnectionSendPingAfterInactivity]);

#if DEBUG
			if (GenuineUtility.IsDebuggingModeEnabled)
				return ;
#endif

			ArrayList expiredConnections = new ArrayList();
			ArrayList sendPingTo = new ArrayList();

			// go through the pool and close all expired connections
			// persistent
			Internal_TimerCallback_Parameters internal_TimerCallback_Parameters = new Internal_TimerCallback_Parameters();
			internal_TimerCallback_Parameters.Now = now;
			internal_TimerCallback_Parameters.SendPingAfter = sendPingAfter;
			internal_TimerCallback_Parameters.ExpiredConnections = expiredConnections;
			internal_TimerCallback_Parameters.SendPingTo = sendPingTo;

			this._persistent.InspectAllConnections(this._internal_TimerCallback_InspectPersistentConnections, internal_TimerCallback_Parameters);

			foreach (XHttpConnection nextXHttpConnection in expiredConnections)
				this.ConnectionFailed(GenuineExceptions.Get_Channel_ConnectionClosedAfterTimeout(), nextXHttpConnection, null);
			foreach (XHttpConnection nextXHttpConnection in sendPingTo)
				GenuineThreadPool.QueueUserWorkItem(new WaitCallback(this.SendPing), nextXHttpConnection, true);
		}


		/// <summary>
		/// Sends a ping to the remote host.
		/// </summary>
		/// <param name="xHttpConnectionAsObject">The client or server connection.</param>
		private void SendPing(object xHttpConnectionAsObject)
		{
			XHttpConnection xHttpConnection = (XHttpConnection) xHttpConnectionAsObject;
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			try
			{
				Message message = new Message(this.ITransportContext, xHttpConnection.Remote, GenuineReceivingHandler.PING_MESSAGE_REPLYID, new TransportHeaders(), Stream.Null);
				message.IsSynchronous = false;

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
					binaryLogWriter.WriteMessageCreatedEvent("XHttpConnectionManager.SendPing",
						LogMessageType.MessageCreated, null, message, true, xHttpConnection.Remote, null, 
						"HTTP PING", "HTTP PING", GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, xHttpConnection.DbgConnectionId, 
						-1, null, -1, null, 
						"HTTP ping is created and sent.");

				this.Send(message);
			}
			catch(Exception ex)
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
					binaryLogWriter.WriteEvent(LogCategory.Connection, "XHttpConnectionManager.SendPing",
						LogMessageType.ConnectionPingSending, ex, null, xHttpConnection.Remote, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null, xHttpConnection.DbgConnectionId, 0, 0, 0, null, null, null, null,
						"Cannot send a ping.");
			}
		}

		#endregion

	}
}
