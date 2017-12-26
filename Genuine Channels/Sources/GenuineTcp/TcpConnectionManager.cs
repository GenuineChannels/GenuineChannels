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

namespace Belikov.GenuineChannels.GenuineTcp
{
	/// <summary>
	/// Provides an implementation for a sender-receiver Connection Manager that uses the TCP protocol to transmit messages.
	/// </summary>
	internal class TcpConnectionManager : ConnectionManager, ITimerConsumer, IAcceptConnectionConsumer
	{
		/// <summary>
		/// Constructs an instance of the TcpConnectionManager class.
		/// </summary>
		/// <param name="iTransportContext">The transport context.</param>
		public TcpConnectionManager(ITransportContext iTransportContext) : base(iTransportContext)
		{
			this._onEndSending = new AsyncCallback(this.LowLevel_EndSending);
			this._internal_TimerCallback = new WaitCallback(this.Internal_TimerCallback);
			this._HalfSync_onContinueReceiving = new WaitCallback(this.Pool_ContinueHalfSyncReceiving);
			this._HalfSync_onEndReceiving = new AsyncCallback(this.LowLevel_HalfSync_EndReceiving);
			this._lowLevel_Client_PreventDelayedAck = new WaitCallback(this.LowLevel_Client_PreventDelayedAck);
			this._processObjectLateBoundHandler = new PersistentConnectionStorage.ProcessConnectionEventHandler(this.FindExpiredConnections);
			this._releaseConnections_InspectPersistentConnections = new PersistentConnectionStorage.ProcessConnectionEventHandler(this.ReleaseConnections_InspectPersistentConnections);

			this._tcpReadRequestBeforeProcessing = (bool) iTransportContext.IParameterProvider[GenuineParameter.TcpReadRequestBeforeProcessing];

			this.Local = new HostInformation("_gtcp://" + iTransportContext.HostIdentifier, iTransportContext);
			TimerProvider.Attach(this);
		}


		#region -- Exchange logic ------------------------------------------------------------------

		/// <summary>
		/// Sends the message to the remote host.
		/// Returns a response if corresponding Security Session is established and the initial message is not one-way.
		/// </summary>
		/// <param name="message">The message to be sent or a null reference (if there is a queued message).</param>
		protected override void InternalSend(Message message)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			TcpSocketInfo tcpSocketInfo = null;

			if (message.IsSynchronous)
			{
				for ( ; ; )
				{
					// get the available connection
					tcpSocketInfo = this.GetConnectionForSending(message);

					// LOG:
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
					{
						binaryLogWriter.WriteEvent(LogCategory.Connection, "TcpConnectionManager.InternalSend",
							LogMessageType.ConnectionSelected, null, null, tcpSocketInfo.Remote, null,
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
							tcpSocketInfo.ConnectionLevelSecurity, 
							tcpSocketInfo.ConnectionLevelSecurity == null ? null : tcpSocketInfo.ConnectionLevelSecurity.Name, 
							tcpSocketInfo.DbgConnectionId, 0, 0, 0, null, null, null, null,
							"TCP connection has been selected. Stack trace: {0}", Environment.StackTrace);
					}

					try
					{
						// register a message
						if (tcpSocketInfo.MessageContainer != null)
							tcpSocketInfo.MessageContainer.AddMessage(message, true);

						SendSync(message, tcpSocketInfo);
						return ;
					}
					catch (Exception ex)
					{
						this.SocketFailed(ex, tcpSocketInfo);

						// if it's a transport problem, change the transport
						OperationException operationException = ex as OperationException;
						if (operationException != null && operationException.OperationErrorMessage.ErrorIdentifier == "GenuineChannels.Exception.Send.TransportProblem")
						{
							message.SerializedContent.Position = 0;
							continue;
						}

						// otherwise, deliver the exception back to the caller
						throw;
					}
					finally
					{
						// unregister the message
						if (tcpSocketInfo.MessageContainer != null)
							tcpSocketInfo.MessageContainer.UnregisterSyncMessage(message);
					}
				}
			}
			else
			{
				tcpSocketInfo = this.GetConnectionForSending(message);
				SendAsync(message, tcpSocketInfo);
			}
		}

		/// <summary>
		/// Sends the message to the remote host.
		/// Returns a response if corresponding Security Session parameters are set and 
		/// the initial message is not one-way.
		/// </summary>
		/// <param name="message">Message to be sent synchronously.</param>
		/// <param name="tcpSocketInfo">Socket connection.</param>
		private void SendSync(Message message, TcpSocketInfo tcpSocketInfo)
		{
			SyncMessageSlot syncMessageSlot = null;

			// try to acquire the read access
			bool sendAccessGranted = true;
			lock (tcpSocketInfo)
			{
				if (tcpSocketInfo.LockForSending)
				{
					// wait until the current sending finishes
					syncMessageSlot = new SyncMessageSlot();
					tcpSocketInfo.MessageContainer.AddMessage(syncMessageSlot, false);
					sendAccessGranted = false;
				}
				else
					tcpSocketInfo.LockForSending = true;
			}

			if (! sendAccessGranted)
			{
				// allow SyncSinkStackProcess to influence on the message processing
				WaitHandle[] waitHandles = null;
				if (message.CancelSending != null)
				{
					waitHandles = new WaitHandle[2];
					waitHandles[1] = message.CancelSending;
				}
				else
					waitHandles = new WaitHandle[1];
				waitHandles[0] = syncMessageSlot.ConnectionAvailable;

				// wait for any of events
				int milliseconds = GenuineUtility.GetMillisecondsLeft(message.FinishTime);
				if (milliseconds <= 0)
					throw GenuineExceptions.Get_Send_ServerDidNotReply();

				int resultFlag = WaitHandle.WaitAny(waitHandles, milliseconds, false);
				if ( resultFlag == WaitHandle.WaitTimeout )
				{
					lock (syncMessageSlot)
					{
						syncMessageSlot.IsValid = false;
						// initiate sending of the next message
						if (syncMessageSlot.TcpSocketInfo != null)
							this.Pool_StartSending(syncMessageSlot.TcpSocketInfo);
					}
					throw GenuineExceptions.Get_Send_ServerDidNotReply();
				}

				if (resultFlag == 1)
					throw GenuineExceptions.Get_Send_Timeout();

				// rethrow the exception
				if (syncMessageSlot.SyncWaitException != null)
					throw OperationException.WrapException(syncMessageSlot.SyncWaitException);
				tcpSocketInfo = syncMessageSlot.TcpSocketInfo;
			}

			// send it
			this.LowLevel_SendSync(message, tcpSocketInfo);

			switch(tcpSocketInfo.GenuineConnectionType)
			{
				case GenuineConnectionType.Persistent:
				case GenuineConnectionType.Named:
					message.IsResendAfterFail = false;
					this.Pool_StartSending(tcpSocketInfo);
					break;

				case GenuineConnectionType.Invocation:
					// it's necessary to provide correct infomation, de bene esse
					lock (tcpSocketInfo)
					{
						tcpSocketInfo.TcpInvocationFiniteAutomatonState = TcpInvocationFiniteAutomatonState.ClientReceiving;
						tcpSocketInfo.Renew();
					}

					//					if (! tcpSocketInfo.IsServer && ! message.IsOneWay)
					//					{
					//						// read the response
					//						using (Stream stream = this.LowLevel_ReadSync(tcpSocketInfo, message.FinishTime))
					//							this.ITransportContext.IIncomingStreamHandler.HandleMessage(stream, tcpSocketInfo.Remote, tcpSocketInfo.GenuineConnectionType, tcpSocketInfo.ConnectionName, true, null, tcpSocketInfo.ConnectionLevelSecurity, null);
					//					}

					lock (tcpSocketInfo)
					{
						tcpSocketInfo.TcpInvocationFiniteAutomatonState = TcpInvocationFiniteAutomatonState.ClientAvailable;
						tcpSocketInfo.Renew();
					}
					break;
			}
		}

		/// <summary>
		/// Sends the message to the remote host.
		/// Returns a response if corresponding Security Session is established and the initial message is not one-way.
		/// </summary>
		/// <param name="message">The message to be sent asynchronously.</param>
		/// <param name="tcpSocketInfo">The connection.</param>
		private void SendAsync(Message message, TcpSocketInfo tcpSocketInfo)
		{
			try
			{
				// try to acquire read access
				bool sendAccessGranted = true;
				lock (tcpSocketInfo)
				{
					if (tcpSocketInfo.LockForSending)
					{
						sendAccessGranted = false;
						tcpSocketInfo.MessageContainer.AddMessage(message, false);
					}
					else
						tcpSocketInfo.LockForSending = true;
				}

				if (sendAccessGranted)
				{
					tcpSocketInfo.Message = message;
					tcpSocketInfo.AsyncSendStream = null;

					// LOG:
					BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Transport] > 0 )
						binaryLogWriter.WriteEvent(LogCategory.Transport, "TcpConnectionManager.SendAsync",
							LogMessageType.MessageIsSentAsynchronously, null, message, message.Recipient, null,
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
							tcpSocketInfo.ConnectionLevelSecurity == null ? null : tcpSocketInfo.ConnectionLevelSecurity, 
							tcpSocketInfo.ConnectionLevelSecurity == null ? null : tcpSocketInfo.ConnectionLevelSecurity.Name, 
							tcpSocketInfo.DbgConnectionId, 0, 0, 0, null, null, null, null,
							"The connection has been obtained and the message is being sent asynchronously.");

					LowLevel_StartSending(message, tcpSocketInfo);
				}
			}
			catch(Exception ex)
			{
				this.SocketFailed(ex, tcpSocketInfo);
			}
		}

		/// <summary>
		/// Sends a ping through the specified connection.
		/// </summary>
		/// <param name="tcpSocketInfoAsObject">The connection.</param>
		private void SendPing(object tcpSocketInfoAsObject)
		{
			TcpSocketInfo tcpSocketInfo = null;

			try
			{
				tcpSocketInfo = (TcpSocketInfo) tcpSocketInfoAsObject;

				// TODO: 2.5.5 fix. Don't send ping messages if there is a least one message in the queue
				lock (tcpSocketInfo)
				{
					if (tcpSocketInfo.MessageContainer.IsMessageAvailable)
						return ;
				}

				Message message = new Message(this.ITransportContext, tcpSocketInfo.Remote, GenuineReceivingHandler.PING_MESSAGE_REPLYID, new TransportHeaders(), Stream.Null);
				message.IsSynchronous = false;
				message.IsOneWay = true;
				message.SerializedContent = Stream.Null;

				// LOG:
				BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
					binaryLogWriter.WriteMessageCreatedEvent("TcpConnectionManager.SendPing",
						LogMessageType.MessageCreated, null, message, true, tcpSocketInfo.Remote, null, 
						"TCP PING", "TCP PING", 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, tcpSocketInfo.DbgConnectionId, 
						-1, null, -1, null, 
						"TCP ping is created and sent.");

				this.SendAsync(message, tcpSocketInfo);
			}
			catch(Exception ex)
			{
				// LOG:
				BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
					binaryLogWriter.WriteEvent(LogCategory.Connection, "TcpConnectionManager.SendPing",
						LogMessageType.ConnectionPingSending, ex, null, tcpSocketInfo == null ? null : tcpSocketInfo.Remote, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null, -1, 0, 0, 0, null, null, null, null,
						"Cannot send a ping.");
			}
		}

		#endregion

		#region -- Low-level network exchange ------------------------------------------------------

		/// <summary>
		/// 0[1] - Magic byte.
		/// 1[4] - Chunk length.
		/// 5[1] - Stream finish flag.
		/// </summary>
		internal const int HEADER_SIZE = 6;

		/// <summary>
		/// Size of the buffer to read messages from the socket.
		/// </summary>
		internal const int SOCKET_BUFFER_SIZE = 5000;

		/// <summary>
		/// Sends a message synchronously. Does not process exceptions!
		/// </summary>
		/// <param name="message">Message.</param>
		/// <param name="tcpSocketInfo">Acquired connection.</param>
		public void LowLevel_SendSync(Message message, TcpSocketInfo tcpSocketInfo)
		{
			Stream stream = null;
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			// LOG:
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Transport] > 0 )
				binaryLogWriter.WriteEvent(LogCategory.Transport, "TcpConnectionManager.LowLevel_SendSync",
					LogMessageType.MessageIsSentSynchronously, null, message, message.Recipient, null,
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
					tcpSocketInfo.ConnectionLevelSecurity == null ? null : tcpSocketInfo.ConnectionLevelSecurity, 
					tcpSocketInfo.ConnectionLevelSecurity == null ? null : tcpSocketInfo.ConnectionLevelSecurity.Name, 
					tcpSocketInfo.DbgConnectionId, 0, 0, 0, null, null, null, null,
					"The connection has been obtained and the message is being sent synchronously.");

			try
			{
				// PING: prevent a ping
				if (tcpSocketInfo.GenuineConnectionType == GenuineConnectionType.Persistent)
					tcpSocketInfo.LastTimeContentWasSent = GenuineUtility.TickCount;

				tcpSocketInfo.SetupWriting(message.FinishTime);

				// create a stream
				bool finished = false;

				// enable connection-level security encryption
				stream = message.SerializedContent;
				if (tcpSocketInfo.ConnectionLevelSecurity != null)
				{
					GenuineChunkedStream encryptedContentStream = new GenuineChunkedStream(true);
					tcpSocketInfo.ConnectionLevelSecurity.Encrypt(stream, encryptedContentStream);
					stream = encryptedContentStream;
				}

				// send it in chunks
				while ( ! finished )
				{
					int currentPosition = HEADER_SIZE;

					// read data
					int size = 0;
					while ( tcpSocketInfo._sendBuffer.Length > currentPosition &&
						(size = stream.Read(tcpSocketInfo._sendBuffer, currentPosition, tcpSocketInfo._sendBuffer.Length - currentPosition)) > 0)
					{
						if (size <= 0)
							break;

						currentPosition += size;
					}

					finished = currentPosition < tcpSocketInfo._sendBuffer.Length;
					int totalSize = currentPosition;

					// prepare header
					tcpSocketInfo._sendBuffer[0] = MessageCoder.COMMAND_MAGIC_CODE;
					MessageCoder.WriteInt32(tcpSocketInfo._sendBuffer, 1, totalSize - HEADER_SIZE);
					tcpSocketInfo._sendBuffer[5] = finished ? (byte) 1 : (byte) 0;

					// and send it
					currentPosition = 0;
					tcpSocketInfo.Write(tcpSocketInfo._sendBuffer, currentPosition, totalSize);
					this.IncreaseBytesSent(totalSize);
				}

				// the message can be resent only if the persistent pattern is used
				if (tcpSocketInfo.GenuineConnectionType == GenuineConnectionType.Persistent  && ! (bool) this.ITransportContext.IParameterProvider[GenuineParameter.TcpDoNotResendMessages])
					tcpSocketInfo.MessagesSentSynchronously.PutMessage(message);
			}
			catch(Exception ex)
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Transport] > 0 )
					binaryLogWriter.WriteEvent(LogCategory.Transport, "TcpConnectionManager.LowLevel_SendSync",
						LogMessageType.MessageIsSentSynchronously, ex, message, message.Recipient, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
						tcpSocketInfo.ConnectionLevelSecurity == null ? null : tcpSocketInfo.ConnectionLevelSecurity, 
						tcpSocketInfo.ConnectionLevelSecurity == null ? null : tcpSocketInfo.ConnectionLevelSecurity.Name, 
						tcpSocketInfo.DbgConnectionId, 0, 0, 0, null, null, null, null,
						"The exception occurred while synchronous sending.");

				throw;
			}
			finally
			{
				lock (tcpSocketInfo)
					tcpSocketInfo.LockForSending = false;
			}

			// the message was sent
			// PING: schedule a ping
			if (tcpSocketInfo.GenuineConnectionType == GenuineConnectionType.Persistent)
				tcpSocketInfo.LastTimeContentWasSent = GenuineUtility.TickCount;
		}

		/// <summary>
		/// Provides a stream reading synchronously from the given connection.
		/// </summary>
		/// <param name="tcpSocketInfo">Socket structure.</param>
		/// <param name="timeoutInMilliseconds">Read timeout.</param>
		/// <param name="automaticallyContinueReading">Indicates whether this instance will automatically initiate reading of the next message from the specified connection.</param>
		/// <returns>A stream.</returns>
		private Stream LowLevel_ReadSync(TcpSocketInfo tcpSocketInfo, int timeoutInMilliseconds, bool automaticallyContinueReading)
		{
			Stream inputStream = new SyncSocketReadingStream(this, tcpSocketInfo, timeoutInMilliseconds, automaticallyContinueReading);

			if (tcpSocketInfo.ConnectionLevelSecurity != null)
				return tcpSocketInfo.ConnectionLevelSecurity.Decrypt(inputStream);
			return inputStream;
		}

		/// <summary>
		/// Starts sending the message through the specified connection.
		/// </summary>
		/// <param name="message">The message being sent.</param>
		/// <param name="tcpSocketInfo">The connection.</param>
		private void LowLevel_StartSending(Message message, TcpSocketInfo tcpSocketInfo)
		{
			// PING: prevent the ping
			if (tcpSocketInfo.GenuineConnectionType == GenuineConnectionType.Persistent)
				tcpSocketInfo.LastTimeContentWasSent = GenuineUtility.TickCount;

			if (tcpSocketInfo.AsyncSendStream == null)
			{
				tcpSocketInfo.AsyncSendStream = message.SerializedContent;
				if (tcpSocketInfo.ConnectionLevelSecurity != null && message.SerializedContent != Stream.Null)
				{
					GenuineChunkedStream encryptedContentStream = new GenuineChunkedStream(true);
					tcpSocketInfo.ConnectionLevelSecurity.Encrypt(tcpSocketInfo.AsyncSendStream, encryptedContentStream);
					tcpSocketInfo.AsyncSendStream = encryptedContentStream;
				}
			}

			// initiate the sending
			tcpSocketInfo.Message = message;
			if (tcpSocketInfo.AsyncSendBuffer == null)
				tcpSocketInfo.AsyncSendBuffer = new byte[tcpSocketInfo.MaxSendSize];
			tcpSocketInfo.AsyncSendBufferCurrentPosition = 0;
			tcpSocketInfo.AsyncSendBufferSizeOfValidContent = HEADER_SIZE + GenuineUtility.TryToReadFromStream(tcpSocketInfo.AsyncSendStream, tcpSocketInfo.AsyncSendBuffer, HEADER_SIZE, tcpSocketInfo.AsyncSendBuffer.Length - HEADER_SIZE);
			tcpSocketInfo.AsyncSendBuffer[0] = MessageCoder.COMMAND_MAGIC_CODE;
			MessageCoder.WriteInt32(tcpSocketInfo.AsyncSendBuffer, 1, tcpSocketInfo.AsyncSendBufferSizeOfValidContent - HEADER_SIZE);
			tcpSocketInfo.AsyncSendBufferIsLastPacket = tcpSocketInfo.AsyncSendBufferSizeOfValidContent < tcpSocketInfo.AsyncSendBuffer.Length - HEADER_SIZE;
			tcpSocketInfo.AsyncSendBuffer[5] = tcpSocketInfo.AsyncSendBufferIsLastPacket ? (byte) 1 : (byte) 0;

			// LOG:
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Transport] > 0 )
			{
				binaryLogWriter.WriteTransportContentEvent(LogCategory.Transport, "TcpConnectionManager.LowLevel_StartSending",
					LogMessageType.AsynchronousSendingStarted, null, tcpSocketInfo.Message, tcpSocketInfo.Remote, 
					binaryLogWriter[LogCategory.Transport] > 1 ? new MemoryStream(GenuineUtility.CutOutBuffer(tcpSocketInfo.AsyncSendBuffer, tcpSocketInfo.AsyncSendBufferCurrentPosition, tcpSocketInfo.AsyncSendBufferSizeOfValidContent - tcpSocketInfo.AsyncSendBufferCurrentPosition)) : null,
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
					tcpSocketInfo.DbgConnectionId, tcpSocketInfo.AsyncSendBufferSizeOfValidContent - tcpSocketInfo.AsyncSendBufferCurrentPosition, tcpSocketInfo.Remote.PhysicalAddress.ToString(), null, null, 
					"The chunk is being sent asynchronously.");
			}

			AsyncThreadStarter.QueueTask(new Async_InitiateSocketSending(tcpSocketInfo.Socket, tcpSocketInfo.AsyncSendBuffer, tcpSocketInfo.AsyncSendBufferCurrentPosition, tcpSocketInfo.AsyncSendBufferSizeOfValidContent - tcpSocketInfo.AsyncSendBufferCurrentPosition, this._onEndSending, tcpSocketInfo));
			//			tcpSocketInfo.Socket.BeginSend(tcpSocketInfo.AsyncSendBuffer, tcpSocketInfo.AsyncSendBufferCurrentPosition, tcpSocketInfo.AsyncSendBufferSizeOfValidContent - tcpSocketInfo.AsyncSendBufferCurrentPosition, SocketFlags.None, this._onEndSending, tcpSocketInfo);
		}

		private AsyncCallback _onEndSending;

		/// <summary>
		/// Completes sending of the message.
		/// </summary>
		/// <param name="ar"></param>
		private void LowLevel_EndSending(IAsyncResult ar)
		{
			TcpSocketInfo tcpSocketInfo = null;
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			try
			{
				tcpSocketInfo = ar.AsyncState as TcpSocketInfo;
				if (tcpSocketInfo == null)
				{
					if ( binaryLogWriter != null)
					{
						binaryLogWriter.WriteImplementationWarningEvent("TcpConnectionManager.LowLevel_EndSending",
							LogMessageType.AsynchronousSendingFinished, GenuineExceptions.Get_Debugging_GeneralWarning("TcpSocketInfo is NULL!"),
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
							"Unexpected situation: the TcpSocketInfo is null.");
					}
					return ;
				}

				int bytesSent = tcpSocketInfo.Socket.EndSend(ar);

				this.IncreaseBytesSent(bytesSent);
				tcpSocketInfo.AsyncSendBufferCurrentPosition += bytesSent;

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.LowLevelTransport] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.LowLevelTransport, "TcpConnectionManager.LowLevel_EndSending",
						LogMessageType.LowLevelTransport_AsyncSendingCompleted, null, tcpSocketInfo.Message, tcpSocketInfo.Remote, 
						null, GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, tcpSocketInfo.ConnectionLevelSecurity, 
						tcpSocketInfo.ConnectionLevelSecurity == null ? null : tcpSocketInfo.ConnectionLevelSecurity.Name,
						tcpSocketInfo.DbgConnectionId, tcpSocketInfo.AsyncSendBuffer.GetHashCode(), bytesSent, 0, null, null, null, null,
						"The asynchronous sending completed.");
				}

				// all synchronous messages are 100% delivered here
				tcpSocketInfo.MessagesSentSynchronously.ReleaseAllMessages();

				if (tcpSocketInfo.AsyncSendBufferCurrentPosition >= tcpSocketInfo.AsyncSendBufferSizeOfValidContent && ! tcpSocketInfo.AsyncSendBufferIsLastPacket)
				{
					this.LowLevel_StartSending(tcpSocketInfo.Message, tcpSocketInfo);
					return ;
				}

				// continue sending if there is a content in a buffer
				if (tcpSocketInfo.AsyncSendBufferCurrentPosition < tcpSocketInfo.AsyncSendBufferSizeOfValidContent)
				{
					AsyncThreadStarter.QueueTask(new Async_InitiateSocketSending(tcpSocketInfo.Socket, tcpSocketInfo.AsyncSendBuffer, tcpSocketInfo.AsyncSendBufferCurrentPosition, tcpSocketInfo.AsyncSendBufferSizeOfValidContent - tcpSocketInfo.AsyncSendBufferCurrentPosition, this._onEndSending, tcpSocketInfo));
					//					tcpSocketInfo.Socket.BeginSend(tcpSocketInfo.AsyncSendBuffer, tcpSocketInfo.AsyncSendBufferCurrentPosition, tcpSocketInfo.AsyncSendBufferSizeOfValidContent - tcpSocketInfo.AsyncSendBufferCurrentPosition, SocketFlags.None, this._onEndSending, tcpSocketInfo);
					return ;
				}

				// ok, assign further task for this connection
				lock (tcpSocketInfo)
				{
					// release memory resources
					tcpSocketInfo.AsyncSendStream.Close();

					// LOG:
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
					{
						binaryLogWriter.WriteEvent(LogCategory.Connection, "TcpConnectionManager.SendAsync",
							LogMessageType.MessageHasBeenSentAsynchronously, null, tcpSocketInfo.Message, tcpSocketInfo.Remote, null,
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
							tcpSocketInfo.ConnectionLevelSecurity, 
							tcpSocketInfo.ConnectionLevelSecurity == null ? null : tcpSocketInfo.ConnectionLevelSecurity.Name, 
							tcpSocketInfo.DbgConnectionId, 0, 0, 0, null, null, null, null,
							"The message has been sent asynchronously.");
					}

					if (tcpSocketInfo.Message != null)
					{
						tcpSocketInfo.Message.Dispose();
						tcpSocketInfo.Message = null;
					}

					// PING: schedule a ping
					if (tcpSocketInfo.GenuineConnectionType == GenuineConnectionType.Persistent)
						tcpSocketInfo.LastTimeContentWasSent = GenuineUtility.TickCount;

					tcpSocketInfo.Message = null;
					tcpSocketInfo.LockForSending = false;
				}
				this.Pool_StartSending(tcpSocketInfo);
			}
			catch(Exception ex)
			{
				if (tcpSocketInfo.Message != null)
					tcpSocketInfo.Message.SerializedContent.Position = 0;

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Transport] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "TcpConnectionManager.LowLevel_EndSending",
						LogMessageType.AsynchronousSendingFinished, ex, tcpSocketInfo.Message, tcpSocketInfo.Remote, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
						tcpSocketInfo.ConnectionLevelSecurity, 
						tcpSocketInfo.ConnectionLevelSecurity == null ? null : tcpSocketInfo.ConnectionLevelSecurity.Name, 
						tcpSocketInfo.DbgConnectionId, 0, 0, 0, null, null, null, null,
						"The exception occurred while sending an asynchronous message.");
				}

				this.SocketFailed(ex, tcpSocketInfo);
			}
		}

		private WaitCallback _lowLevel_Client_PreventDelayedAck;

		/// <summary>
		/// Sends a small synchronous packet to the server in order to prevent delayed ack.
		/// </summary>
		/// <param name="tcpSocketInfoAsObject">The TCP connection.</param>
		internal void LowLevel_Client_PreventDelayedAck(object tcpSocketInfoAsObject)
		{
			TcpSocketInfo tcpSocketInfo = (TcpSocketInfo) tcpSocketInfoAsObject;

			try
			{
				Message message = new Message(this.ITransportContext, tcpSocketInfo.Remote, GenuineReceivingHandler.PING_MESSAGE_REPLYID, new TransportHeaders(), Stream.Null);
				message.IsSynchronous = true;
				message.SerializedContent = Stream.Null;
				this.SendSync(message, tcpSocketInfo);
			}
			catch(Exception ex)
			{
				BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Transport] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Transport, "TcpConnectionManager.LowLevel_Client_PreventDelayedAck",
						LogMessageType.SynchronousSendingFinished, ex, null, tcpSocketInfo.Remote, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
						tcpSocketInfo.ConnectionLevelSecurity, 
						tcpSocketInfo.ConnectionLevelSecurity == null ? null : tcpSocketInfo.ConnectionLevelSecurity.Name, 
						tcpSocketInfo.DbgConnectionId, 0, 0, 0, null, null, null, null,
						"The exception occurred while sending a synchronous message.");
				}
			}
		}

		/// <summary>
		/// Initiates receiving of the message's header.
		/// </summary>
		/// <param name="tcpSocketInfo">The connection.</param>
		internal void LowLevel_HalfSync_StartReceiving(TcpSocketInfo tcpSocketInfo)
		{
			if (tcpSocketInfo.ReceivingHeaderBuffer == null)
				tcpSocketInfo.ReceivingHeaderBuffer = new byte[HEADER_SIZE];

			tcpSocketInfo.ReceivingBufferExpectedSize = HEADER_SIZE;
			tcpSocketInfo.ReceivingBufferCurrentPosition = 0;
			tcpSocketInfo.IsHeaderIsBeingReceived = true;

			// LOG:
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Transport] > 0 )
			{
				binaryLogWriter.WriteEvent(LogCategory.Transport, "TcpConnectionManager.LowLevel_HalfSync_StartReceiving",
					LogMessageType.ReceivingStarted, null, null, tcpSocketInfo.Remote, null,
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
					tcpSocketInfo.ConnectionLevelSecurity, 
					tcpSocketInfo.ConnectionLevelSecurity == null ? null : tcpSocketInfo.ConnectionLevelSecurity.Name,
					tcpSocketInfo.DbgConnectionId, 0, 0, 0, null, null, null, null,
					"The asynchronous receiving is initiated. Requested size: {0}.", tcpSocketInfo.ReceivingBufferExpectedSize - tcpSocketInfo.ReceivingBufferCurrentPosition);
			}

			AsyncThreadStarter.QueueTask(new Async_InitiateSocketReceiving(tcpSocketInfo.Socket, 
				tcpSocketInfo.ReceivingHeaderBuffer, tcpSocketInfo.ReceivingBufferCurrentPosition, 
				tcpSocketInfo.ReceivingBufferExpectedSize - tcpSocketInfo.ReceivingBufferCurrentPosition, 
				_HalfSync_onEndReceiving, tcpSocketInfo));

			if ( ! tcpSocketInfo.IsServer && (bool) this.ITransportContext.IParameterProvider[GenuineParameter.TcpPreventDelayedAck])
				GenuineThreadPool.QueueUserWorkItem(this._lowLevel_Client_PreventDelayedAck, tcpSocketInfo, true);
		}

		private AsyncCallback _HalfSync_onEndReceiving;

		/// <summary>
		/// Processes the received content.
		/// </summary>
		/// <param name="ar">The result of the asynchronous invocation.</param>
		private void LowLevel_HalfSync_EndReceiving(IAsyncResult ar)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			TcpSocketInfo tcpSocketInfo = (TcpSocketInfo) ar.AsyncState;

			try
			{
				int bytesReceived = tcpSocketInfo.Socket.EndReceive(ar);

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.LowLevelTransport] > 0 )
				{
					binaryLogWriter.WriteTransportContentEvent(LogCategory.LowLevelTransport, "TcpConnectionManager.LowLevel_HalfSync_EndReceiving",
						LogMessageType.LowLevelTransport_AsyncReceivingCompleted, null, null, tcpSocketInfo.Remote, 
						binaryLogWriter[LogCategory.LowLevelTransport] > 1 ? new MemoryStream(tcpSocketInfo.ReceivingHeaderBuffer, tcpSocketInfo.ReceivingBufferCurrentPosition, bytesReceived) : null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						tcpSocketInfo.DbgConnectionId, bytesReceived, tcpSocketInfo.Socket.RemoteEndPoint.ToString(), null, null,
						"Socket.EndReceive(). Bytes received: {0}. Socket: {1}.", bytesReceived, (long) tcpSocketInfo.Socket.Handle);
				}

				tcpSocketInfo.ReceivingBufferCurrentPosition += bytesReceived;

				// if the connection was closed
				if (tcpSocketInfo.ReceivingBufferCurrentPosition == 0)
					throw GenuineExceptions.Get_Receive_Portion();

				if (tcpSocketInfo.ReceivingBufferCurrentPosition < tcpSocketInfo.ReceivingBufferExpectedSize)
				{
					AsyncThreadStarter.QueueTask(new Async_InitiateSocketReceiving(tcpSocketInfo.Socket, 
						tcpSocketInfo.ReceivingHeaderBuffer, tcpSocketInfo.ReceivingBufferCurrentPosition, 
						tcpSocketInfo.ReceivingBufferExpectedSize - tcpSocketInfo.ReceivingBufferCurrentPosition, 
						_HalfSync_onEndReceiving, tcpSocketInfo));
					return ;
				}

				GenuineThreadPool.QueueUserWorkItem(_HalfSync_onContinueReceiving, tcpSocketInfo, true);
				//				this.Pool_ContinueHalfSyncReceiving(tcpSocketInfo);
			}
			catch(Exception ex)
			{
				this.SocketFailed(ex, tcpSocketInfo);
			}
		}

		private WaitCallback _HalfSync_onContinueReceiving;

		/// <summary>
		/// Closes the connection.
		/// </summary>
		/// <param name="tcpSocketInfoAsObject">The connection to be closed.</param>
		private void LowLevel_CloseConnection(object tcpSocketInfoAsObject)
		{
			TcpSocketInfo tcpSocketInfo = (TcpSocketInfo) tcpSocketInfoAsObject;

			if (tcpSocketInfo.Socket != null)
				SocketUtility.CloseSocket(tcpSocketInfo.Socket);
		}

		#endregion

		#region -- Establishing of Connections -----------------------------------------------------

		/// <summary>
		/// A connection store containing all persistent connection.
		/// </summary>
		private PersistentConnectionStorage _persistent= new PersistentConnectionStorage();

		/// <summary>
		/// Connection primary URL + "/" + connection name => TcpSocketInfo.
		/// </summary>
		private Hashtable _named = new Hashtable();

		/// <summary>
		/// Contains all invocation connections Hash 
		/// { Server url => ArrayList containing TcpSocketInfo }.
		/// </summary>
		private Hashtable _invocation = Hashtable.Synchronized(new Hashtable());

		/// <summary>
		/// Contains known named invocation connections to send responses through.
		/// { Connection Id => TcpSocketInfo }
		/// </summary>
		private Hashtable _knownInvocationConnections = Hashtable.Synchronized(new Hashtable());

		/// <summary>
		/// Establishes a connection to the remote host.
		/// </summary>
		/// <param name="remote">The HostInformation of the remote host.</param>
		/// <param name="genuineConnectionType">Type of the connection.</param>
		/// <param name="localUri">Local URI.</param>
		/// <param name="localPort">Local port.</param>
		/// <param name="connectionName">The name of the connection or a null reference.</param>
		/// <param name="remoteUri">Remote URI.</param>
		/// <param name="remoteHostUniqueIdentifier">The unique identifier of the HostInformation used by the remote host.</param>
		/// <returns>A connection.</returns>
		internal TcpSocketInfo LowLevel_OpenConnection(HostInformation remote, GenuineConnectionType genuineConnectionType, string localUri, int localPort, string connectionName, out string remoteUri, out int remoteHostUniqueIdentifier)
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
			string url = remote.Url;

			// parse provided url and fetch a port and IP address
			int portNumber;
			string hostName = GenuineUtility.SplitToHostAndPort(url, out portNumber);

			// LOG:
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
			{
				binaryLogWriter.WriteEvent(LogCategory.Connection, "TcpConnectionManager.LowLevel_OpenConnection",
					LogMessageType.ConnectionEstablishing, null, null, remote, null,
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
					-1, 0, 0, 0, null, null, null, null,
					"The connection is being established to {0}.", hostName);
			}

			Socket socket = null;

			// the time we should finish connection establishing before
			int timeout = GenuineUtility.GetTimeout( (TimeSpan) this.ITransportContext.IParameterProvider[GenuineParameter.ConnectTimeout] );
			using (ConnectionEstablishingClosure connectionEstablishingClosure = new ConnectionEstablishingClosure(hostName, portNumber, this.ITransportContext.IParameterProvider))
			{
				connectionEstablishingClosure.StartOperation();
				if (! connectionEstablishingClosure.Completed.WaitOne( GenuineUtility.GetMillisecondsLeft(timeout), false))
					throw GenuineExceptions.Get_Connect_CanNotConnectToRemoteHost(url, "Timeout expired.");

				if (connectionEstablishingClosure.Exception != null)
					throw GenuineExceptions.Get_Connect_CanNotConnectToRemoteHost(url, connectionEstablishingClosure.Exception.Message);

				socket = connectionEstablishingClosure.CompleteOperation();
			}

			if (! socket.Connected)
				throw GenuineExceptions.Get_Connect_CanNotConnectToRemoteHost(url, "Socket.Connected property is false after connecting.");

			if (connectionName == null)
				connectionName = "$/__GC/" + hostName;
			TcpSocketInfo tcpSocketInfo = new TcpSocketInfo(socket, this.ITransportContext, connectionName
#if DEBUG
				,"Opened connection"
#endif
				);
			tcpSocketInfo.GenuineConnectionType = genuineConnectionType;

			// LOG:
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
			{
				binaryLogWriter.WriteConnectionParameterEvent(LogCategory.Connection, "TcpConnectionManager.LowLevel_OpenConnection",
					LogMessageType.ConnectionParameters, null, remote, this.ITransportContext.IParameterProvider,
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, tcpSocketInfo.DbgConnectionId, 
					"The connection is being established to \"{0}\".", hostName);
			}

			// send protocol version and the type of the connection
			Message message = new Message(null, null, -1, new TransportHeaders(), Stream.Null);
			message.FinishTime = timeout;
			message.SerializedContent = MessageCoder.SerializeConnectionHeader(MessageCoder.PROTOCOL_VERSION, genuineConnectionType, tcpSocketInfo.ConnectionName).BaseStream;
			LowLevel_SendSync(message, tcpSocketInfo);
			tcpSocketInfo.MessagesSentSynchronously.ReleaseAllMessages();

			// get connection-level SS
			string connectionLevelSSName = null;
			switch(genuineConnectionType)
			{
				case GenuineConnectionType.Persistent:
					connectionLevelSSName = this.ITransportContext.IParameterProvider[GenuineParameter.SecuritySessionForPersistentConnections] as string;
					break;

				case GenuineConnectionType.Named:
					connectionLevelSSName = this.ITransportContext.IParameterProvider[GenuineParameter.SecuritySessionForNamedConnections] as string;
					break;

				case GenuineConnectionType.Invocation:
					connectionLevelSSName = this.ITransportContext.IParameterProvider[GenuineParameter.SecuritySessionForInvocationConnections] as string;
					break;
			}

			SecuritySession securitySession = null;
			if (connectionLevelSSName != null)
				securitySession = this.ITransportContext.IKeyStore.GetKey(connectionLevelSSName).CreateSecuritySession(connectionLevelSSName, null);

			// establish it
			if (securitySession != null && ! securitySession.IsEstablished)
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "TcpConnectionManager.LowLevel_OpenConnection",
						LogMessageType.ConnectionSecurityIsEstablished, null, null, tcpSocketInfo.Remote, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, securitySession, connectionLevelSSName,
						tcpSocketInfo.DbgConnectionId, 0, 0, 0, null, null, null, null,
						"The Connection Level Security Session is being established.");
				}

				bool firstPass = true;
				for ( ; ; )
				{
					inputStream = Stream.Null;

					try
					{
						// prepare streams
						if (! firstPass)
							inputStream = this.LowLevel_ReadSync(tcpSocketInfo, timeout, false);
						else
							firstPass = false;

						outputStream = securitySession.EstablishSession(inputStream, true);

						if (outputStream == null)
							break;

						// send a packet to the remote host
						message = new Message(null, null, -1, new TransportHeaders(), Stream.Null);
						message.FinishTime = timeout;
						message.SerializedContent = outputStream;
						LowLevel_SendSync(message, tcpSocketInfo);
						tcpSocketInfo.MessagesSentSynchronously.ReleaseAllMessages();

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

			tcpSocketInfo.ConnectionLevelSecurity = securitySession;

			// now send connection info through the established connection
			using (GenuineChunkedStream serializedLocalInfo = new GenuineChunkedStream(false))
			{
				// serialize local info
				BinaryWriter binaryWriter = new BinaryWriter(serializedLocalInfo);
				binaryWriter.Write((string) localUri);

				// 2.5.2 fix
				//				binaryWriter.Write((int) localPort);
				binaryWriter.Write((int) -1);
				binaryWriter.Write((byte) genuineConnectionType);
				binaryWriter.Write((int) remote.LocalHostUniqueIdentifier);

				// and send it
				message = new Message(null, null, -1, new TransportHeaders(), Stream.Null);
				message.FinishTime = timeout;
				message.SerializedContent = serializedLocalInfo;
				LowLevel_SendSync(message, tcpSocketInfo);
				tcpSocketInfo.MessagesSentSynchronously.ReleaseAllMessages();

				// read remote info
				using (Stream remoteUriStream = this.LowLevel_ReadSync(tcpSocketInfo, timeout, false))
				{
					BinaryReader binaryReader = new BinaryReader(remoteUriStream);
					remoteUri = binaryReader.ReadString();
					remoteHostUniqueIdentifier = binaryReader.ReadInt32();
				}
			}

			tcpSocketInfo.IsServer = false;
			return tcpSocketInfo;
		}

		/// <summary>
		/// Accepts an incoming TCP connection.
		/// </summary>
		/// <param name="socket">The socket.</param>
		/// <param name="localUri">URI of the local host.</param>
		/// <param name="remoteUri">Uri of the remote host.</param>
		/// <param name="remoteAddress">Address of the remote host.</param>
		/// <param name="connectionName">The name of the connection.</param>
		/// <param name="remoteHostUniqueIdentifier">The unique identifier of the HostInformation used by the remote host.</param>
		/// <param name="reasonOfStateLost">The reason why the remote host has lost its state (if it's happened).</param>
		/// <returns>An instance of the TcpSocketInfo class.</returns>
		internal TcpSocketInfo LowLevel_AcceptConnection(Socket socket, string localUri, out string remoteUri, out string remoteAddress, out string connectionName, out int remoteHostUniqueIdentifier, out Exception reasonOfStateLost)
		{
			using (new ReaderAutoLocker(this._disposeLock))
			{
				if (this._disposed)
					throw OperationException.WrapException(this._disposeReason);
			}

			// set the remote end point and local end point
			using (new ThreadDataSlotKeeper(OccupiedThreadSlots.SocketDuringEstablishing, socket))
			{
				remoteUri = null;
				remoteAddress = null;
				remoteHostUniqueIdentifier = 0;

				Stream inputStream = null;
				Stream outputStream = null;

				// the time we should finish connection establishing before
				int timeout = GenuineUtility.GetTimeout( (TimeSpan) this.ITransportContext.IParameterProvider[GenuineParameter.ConnectTimeout]);

				// setup a socket
				LingerOption lingerOption = new LingerOption(true, 3);
				socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, lingerOption);

				if ((bool) this.ITransportContext.IParameterProvider[GenuineParameter.TcpDisableNagling])
					socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoDelay, 1);

				// create a connection
				TcpSocketInfo tcpSocketInfo = new TcpSocketInfo(socket, this.ITransportContext, null
#if DEBUG
					,"Accepted connection"
#endif
					);

				// LOG:
				BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.AcceptingConnection] > 0 )
					binaryLogWriter.WriteEvent(LogCategory.AcceptingConnection, "TcpConnectionManager.LowLevel_AcceptConnection",
						LogMessageType.ConnectionAccepting, null, null, null, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, tcpSocketInfo.DbgConnectionId, 0, 0, 0, socket.RemoteEndPoint.ToString(), socket.LocalEndPoint.ToString(), null, null,
						"An inbound connection is being accepted. Remote end point: {0}.", socket.RemoteEndPoint.ToString());

				// receive and analyze protocol version and the type of the connection
				GenuineConnectionType genuineConnectionType;
				byte protocolVersion;
				using (Stream readingStream = this.LowLevel_ReadSync(tcpSocketInfo, timeout, false))
				{
					BinaryReader binaryReader = new BinaryReader(readingStream);
					MessageCoder.DeserializeConnectionHeader(binaryReader, out protocolVersion, out genuineConnectionType, out connectionName);
				}

				// define the connection logic
				tcpSocketInfo.GenuineConnectionType = genuineConnectionType;
				tcpSocketInfo.ConnectionName = connectionName;

				// get connection-level SS
				string connectionLevelSSName = null;
				switch (tcpSocketInfo.GenuineConnectionType)
				{
					case GenuineConnectionType.Persistent:
						connectionLevelSSName = this.ITransportContext.IParameterProvider[GenuineParameter.SecuritySessionForPersistentConnections] as string;
						break;

					case GenuineConnectionType.Named:
						connectionLevelSSName = this.ITransportContext.IParameterProvider[GenuineParameter.SecuritySessionForNamedConnections] as string;
						break;

					case GenuineConnectionType.Invocation:
						connectionLevelSSName = this.ITransportContext.IParameterProvider[GenuineParameter.SecuritySessionForInvocationConnections] as string;
						break;
				}

				SecuritySession securitySession = null;
				if (connectionLevelSSName != null)
					securitySession = this.ITransportContext.IKeyStore.GetKey(connectionLevelSSName).CreateSecuritySession(connectionLevelSSName, null);

				// establish a SS
				if (securitySession != null && ! securitySession.IsEstablished)
				{
					// LOG:
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.AcceptingConnection] > 0 )
						binaryLogWriter.WriteEvent(LogCategory.AcceptingConnection, "TcpConnectionManager.LowLevel_AcceptConnection",
							LogMessageType.ConnectionSecurityIsEstablished, null, null, null, null, 
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
							securitySession, connectionLevelSSName, tcpSocketInfo.DbgConnectionId, 0, 0, 0, null, null, null, null,
							"The Connection Level Security Session must be established prior to connection is accepted.");

					for ( ; ; )
					{
						inputStream = null;

						try
						{
							// prepare streams
							inputStream = this.LowLevel_ReadSync(tcpSocketInfo, timeout, false);
							outputStream = securitySession.EstablishSession(inputStream, true);

							if (outputStream == null)
								break;

							// send a packet to the remote host
							Message message = new Message(null, null, -1, new TransportHeaders(), Stream.Null);
							message.FinishTime = timeout;
							message.SerializedContent = outputStream;
							LowLevel_SendSync(message, tcpSocketInfo);
							tcpSocketInfo.MessagesSentSynchronously.ReleaseAllMessages();

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

				tcpSocketInfo.ConnectionLevelSecurity = securitySession;

				// exchange with connection info
				int remotePort = 0;

				// read remote info
				using (Stream headerStream = this.LowLevel_ReadSync(tcpSocketInfo, timeout, false))
				{
					//				this.SkipPacketHeader(headerStream, connectionLevelSSName);
					BinaryReader binaryReader = new BinaryReader(headerStream);

					remoteUri = binaryReader.ReadString();
					remotePort = binaryReader.ReadInt32();
					tcpSocketInfo.GenuineConnectionType = (GenuineConnectionType) binaryReader.ReadByte();

					if (protocolVersion >= 0x1)
						remoteHostUniqueIdentifier = binaryReader.ReadInt32();
				}

				HostInformation remote = this.ITransportContext.KnownHosts[remoteUri];
				remote.ProtocolVersion = protocolVersion;

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteConnectionParameterEvent(LogCategory.Connection, "TcpConnectionManager.LowLevel_AcceptConnection",
						LogMessageType.ConnectionParameters, null, remote, this.ITransportContext.IParameterProvider,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, tcpSocketInfo.DbgConnectionId, 
						"The connection is accepted. Protocol version: \"{0}\".", protocolVersion);
				}

				reasonOfStateLost = remote.UpdateUri(remoteUri, remoteHostUniqueIdentifier, tcpSocketInfo.GenuineConnectionType == GenuineConnectionType.Persistent);

				using (GenuineChunkedStream serializedLocalInfo = new GenuineChunkedStream(false))
				{
					// serialize local info
					BinaryWriter binaryWriter = new BinaryWriter(serializedLocalInfo);
					binaryWriter.Write((string) localUri);

					if (protocolVersion >= 0x1)
						binaryWriter.Write((int) remote.LocalHostUniqueIdentifier);

					// and send it
					Message message = new Message(null, null, -1, new TransportHeaders(), Stream.Null);
					message.FinishTime = timeout;
					message.SerializedContent = serializedLocalInfo;
					LowLevel_SendSync(message, tcpSocketInfo);
					tcpSocketInfo.MessagesSentSynchronously.ReleaseAllMessages();
				}

				// build network address of the remote host
				if (remotePort > 0)
				{
					IPEndPoint remoteHostIpEndPoint = (IPEndPoint) socket.RemoteEndPoint;
					remoteAddress = "gtcp://" + remoteHostIpEndPoint.Address.ToString() + ":" + remotePort;
				}

				tcpSocketInfo.IsServer = true;
				return tcpSocketInfo;
			}
		}

		/// <summary>
		/// All checks on number of connections should be performed only while access to this object is obtained.
		/// </summary>
		private object _lowLevel_OpenConnectionLock = new object();

		/// <summary>
		/// Re-establishes the persistent connection to the remote host.
		/// </summary>
		/// <param name="stubSocketInfoAsObject">Stub socket that holds message queue.</param>
		private void ReestablishConnection(object stubSocketInfoAsObject)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			TcpSocketInfo stubSocketInfo = (TcpSocketInfo) stubSocketInfoAsObject;
			HostInformation remote = stubSocketInfo.Remote;
			TcpSocketInfo tcpSocketInfo = null;

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
					binaryLogWriter.WriteEvent(LogCategory.Connection, "TcpConnectionManager.ReestablishConnection",
						LogMessageType.ConnectionReestablishing, null, null, remote, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, -1, 0, 0, 0, null, null, null, null,
						"The connection is being reestablished.");
				}

				remote.Renew(GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.MaxTimeSpanToReconnect]), false);
				int deadline = GenuineUtility.GetTimeout((TimeSpan) this.ITransportContext.IParameterProvider[GenuineParameter.MaxTimeSpanToReconnect]);

				int tryNumber = 0;
				int tryDeadline = GenuineUtility.GetTimeout((TimeSpan) this.ITransportContext.IParameterProvider[GenuineParameter.MaxTimeSpanToReconnect]);
				string remoteUri = null;
				int remoteHostUniqueIdentifier;

				for ( ; ; )
				{
					// check whether it's possible to continue
					tcpSocketInfo = null;
					Thread.Sleep((TimeSpan) this.ITransportContext.IParameterProvider[GenuineParameter.SleepBetweenReconnections]);
					if (tryNumber++ > (int) this.ITransportContext.IParameterProvider[GenuineParameter.ReconnectionTries] || GenuineUtility.IsTimeoutExpired(tryDeadline))
						throw GenuineExceptions.Get_Channel_ReconnectionFailed();
					if (GenuineUtility.IsTimeoutExpired(deadline))
						throw GenuineExceptions.Get_Channel_ReconnectionFailed();

					// and manager was not disposed
					using(new ReaderAutoLocker(this._disposeLock))
					{
						if (this._disposed)
							throw GenuineExceptions.Get_Channel_ReconnectionFailed();
					}

					// if stub is not alive, reconnection is senseless
					lock (stubSocketInfo)
						if (! stubSocketInfo.IsValid)
							return ;

					// the next attempt
					try
					{
						tcpSocketInfo = LowLevel_OpenConnection(remote, GenuineConnectionType.Persistent, this.Local.Uri, this.LocalPort, stubSocketInfo.ConnectionName, out remoteUri, out remoteHostUniqueIdentifier);
						remote.PhysicalAddress = tcpSocketInfo.Socket.RemoteEndPoint;
						remote.LocalPhysicalAddress = tcpSocketInfo.Socket.LocalEndPoint;
					}
					catch(Exception ex)
					{
						// LOG:
						if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
							binaryLogWriter.WriteEvent(LogCategory.Connection, "TcpConnectionManager.ReestablishConnection",
								LogMessageType.ConnectionReestablishing, ex, null, remote, null, 
								GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
								null, null, -1, 0, 0, 0, null, null, null, null,
								"Reconnection failed. The current try: {0}. Milliseconds remained: {1}.", tryNumber, GenuineUtility.CompareTickCounts(deadline, GenuineUtility.TickCount));
						continue;
					}

					// the connection has been reestablished
					remote.UpdateUri(remoteUri, remoteHostUniqueIdentifier, true);

					using (new ReaderAutoLocker(this._disposeLock))
					{
						// obtain the current persistent socket info
						lock (this._persistent.SyncRoot)
						{
							TcpSocketInfo currentSocketInfo = this._persistent.Get(remote.Url, stubSocketInfo.ConnectionName) as TcpSocketInfo;

							// if the connection is closed
							if (currentSocketInfo == null)
							{
								// forget it
								GenuineThreadPool.QueueUserWorkItem(new WaitCallback(this.LowLevel_CloseConnection), tcpSocketInfo, false);
								return ;
							}

							lock (currentSocketInfo)
							{
								// if the connection considered to be finally closed
								if (! currentSocketInfo.IsValid)
								{
									// forget it
									GenuineThreadPool.QueueUserWorkItem(new WaitCallback(this.LowLevel_CloseConnection), tcpSocketInfo, false);
									return ;
								}

								// otherwise, inherit its characteristics
								tcpSocketInfo.MessageContainer = currentSocketInfo.MessageContainer;
								tcpSocketInfo.Remote = currentSocketInfo.Remote;
								tcpSocketInfo._iMessageRegistrator = currentSocketInfo._iMessageRegistrator;
								tcpSocketInfo.CloseConnectionAfterInactivity = GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.ClosePersistentConnectionAfterInactivity]);
								tcpSocketInfo.Renew();

								this._persistent.Set(remote.Url, stubSocketInfo.ConnectionName, tcpSocketInfo);
							}

							this.Pool_StartSending(tcpSocketInfo);
							this.Pool_InitiateReceiving(tcpSocketInfo, tcpSocketInfo.ITransportContext.IParameterProvider);

							tcpSocketInfo.SignalState(GenuineEventType.GeneralConnectionEstablished, null, null);

							// LOG:
							if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
							{
								binaryLogWriter.WriteEvent(LogCategory.Connection, "TcpConnectionManager.ReestablishConnection",
									LogMessageType.ConnectionReestablished, null, null, remote, null, 
									GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
									tcpSocketInfo.ConnectionLevelSecurity, tcpSocketInfo.ConnectionLevelSecurity == null ? null : tcpSocketInfo.ConnectionLevelSecurity.Name, 
									tcpSocketInfo == null ? -1 : tcpSocketInfo.DbgConnectionId, 
									(int) tcpSocketInfo.GenuineConnectionType, 0, 0, this.GetType().Name, remote.LocalPhysicalAddress.ToString(), remote.PhysicalAddress.ToString(), null,
									"The connection has been reestablished.");
							}
						}
					}

					return ;
				}
			}
			catch(Exception ex)
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "TcpConnectionManager.ReestablishConnection",
						LogMessageType.ConnectionReestablished, ex, null, remote, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, tcpSocketInfo == null ? -1 : tcpSocketInfo.DbgConnectionId, 
						0, 0, 0, null, null, null, null,
						"The connection cannot be reestablished.");
				}

				// the connection
				if (tcpSocketInfo != null)
					LowLevel_CloseConnection(tcpSocketInfo);

				bool disposeAllResources = false;

				// the queue
				lock (this._persistent.SyncRoot)
				{
					tcpSocketInfo = this._persistent.Get(remote.Url, stubSocketInfo.ConnectionName) as TcpSocketInfo;

					// if it's still the stub
					if (object.ReferenceEquals(stubSocketInfo, tcpSocketInfo))
					{
						this._persistent.Remove(remote.Url, stubSocketInfo.ConnectionName);
						disposeAllResources = true;
					}
				}

				// close all SSs
				if (disposeAllResources)
					this.ITransportContext.KnownHosts.ReleaseHostResources(remote, ex);

				// and the events
				stubSocketInfo.SignalState(GenuineEventType.GeneralConnectionClosed, ex, null);

				OperationException operationException = ex as OperationException;
				if (operationException != null && operationException.OperationErrorMessage.ErrorIdentifier == "GenuineChannels.Exception.Receive.ServerHasBeenRestarted")
					this.ITransportContext.IGenuineEventProvider.Fire(new GenuineEventArgs(GenuineEventType.GeneralServerRestartDetected, GenuineExceptions.Get_Receive_ServerHasBeenRestared(), remote, null));
			}
			finally
			{
			}
		}



		#endregion

		#region -- Connection closing --------------------------------------------------------------

		/// <summary>
		/// Closes expired connections and sends ping via inactive connections.
		/// </summary>
		public void TimerCallback()
		{
			lock (this)
			{
				if (this._isInProgress)
					return ;

				this._isInProgress = true;
			}

			GenuineThreadPool.QueueUserWorkItem(_internal_TimerCallback, null, false);
		}
		private WaitCallback _internal_TimerCallback;
		private bool _isInProgress = false;

		private PersistentConnectionStorage.ProcessConnectionEventHandler _processObjectLateBoundHandler;
		private class FindExpiredConnections_CollectedConnections
		{
			public ArrayList SendPingTo = new ArrayList();
			public ArrayList ExpiredConnections = new ArrayList();
			public int SendPingAfterInactivity;
			public int Now;
		}

		/// <summary>
		/// Analyzes the state of the connection.
		/// </summary>
		/// <param name="tcpSocketInfoAsObject">The connection.</param>
		/// <param name="findExpiredConnections_CollectedConnectionsAsObject">Stuff to make decisions and to save the results.</param>
		private void FindExpiredConnections(object tcpSocketInfoAsObject, object findExpiredConnections_CollectedConnectionsAsObject)
		{
			TcpSocketInfo tcpSocketInfo = (TcpSocketInfo) tcpSocketInfoAsObject;
			FindExpiredConnections_CollectedConnections findExpiredConnections_CollectedConnections = (FindExpiredConnections_CollectedConnections) findExpiredConnections_CollectedConnectionsAsObject;

			if (GenuineUtility.IsTimeoutExpired(tcpSocketInfo.ShutdownTime, findExpiredConnections_CollectedConnections.Now))
				findExpiredConnections_CollectedConnections.ExpiredConnections.Add(tcpSocketInfo);

			if (GenuineUtility.IsTimeoutExpired(tcpSocketInfo.LastTimeContentWasSent + findExpiredConnections_CollectedConnections.SendPingAfterInactivity, findExpiredConnections_CollectedConnections.Now))
				findExpiredConnections_CollectedConnections.SendPingTo.Add(tcpSocketInfo);
		}

		/// <summary>
		/// Closes expired connections and sends pings via inactive connections.
		/// </summary>
		/// <param name="ignored">Ignored.</param>
		private void Internal_TimerCallback(object ignored)
		{
			try
			{
				int now = GenuineUtility.TickCount;
				int forcePingSince = GenuineUtility.TickCount - GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.PersistentConnectionSendPingAfterInactivity]);

				// go through the pool and close all expired connections

				// persistent
				ArrayList expiredConnections = new ArrayList();
				ArrayList sendPingTo = new ArrayList();
				FindExpiredConnections_CollectedConnections findExpiredConnections_CollectedConnections = new FindExpiredConnections_CollectedConnections();
				findExpiredConnections_CollectedConnections.ExpiredConnections = expiredConnections;
				findExpiredConnections_CollectedConnections.SendPingTo = sendPingTo;
				findExpiredConnections_CollectedConnections.Now = now;
				findExpiredConnections_CollectedConnections.SendPingAfterInactivity = GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.PersistentConnectionSendPingAfterInactivity]);

				this._persistent.InspectAllConnections(this._processObjectLateBoundHandler, findExpiredConnections_CollectedConnections);

				foreach (TcpSocketInfo tcpSocketInfo in expiredConnections)
					this.SocketFailed(GenuineExceptions.Get_Channel_ConnectionClosedAfterTimeout(), tcpSocketInfo);
				foreach (TcpSocketInfo tcpSocketInfo in sendPingTo)
					GenuineThreadPool.QueueUserWorkItem(new WaitCallback(this.SendPing), tcpSocketInfo, false);

				// named
				expiredConnections.Clear();
				sendPingTo.Clear();
				lock (this._named)
				{
					foreach (DictionaryEntry dictionaryEntry in this._named)
					{
						TcpSocketInfo tcpSocketInfo = (TcpSocketInfo) dictionaryEntry.Value;
						if (GenuineUtility.IsTimeoutExpired(tcpSocketInfo.ShutdownTime, now))
							expiredConnections.Add(tcpSocketInfo);
						if (GenuineUtility.IsTimeoutExpired(tcpSocketInfo.LastTimeContentWasSent, forcePingSince))
							sendPingTo.Add(tcpSocketInfo);
					}
				}

				foreach (TcpSocketInfo tcpSocketInfo in expiredConnections)
					this.SocketFailed(GenuineExceptions.Get_Channel_ConnectionClosedAfterTimeout(), tcpSocketInfo);
				foreach (TcpSocketInfo tcpSocketInfo in sendPingTo)
					GenuineThreadPool.QueueUserWorkItem(new WaitCallback(this.SendPing), tcpSocketInfo, false);

				// invocation
				expiredConnections.Clear();
				lock (this._invocation.SyncRoot)
				{
					foreach (DictionaryEntry dictionaryEntry in this._invocation)
					{
						ArrayList arrayList = (ArrayList) dictionaryEntry.Value;
						foreach (TcpSocketInfo tcpSocketInfo in arrayList)
							lock (tcpSocketInfo)
								if (GenuineUtility.IsTimeoutExpired(tcpSocketInfo.ShutdownTime, now))
									expiredConnections.Add(tcpSocketInfo);
					}
				}

				foreach (TcpSocketInfo tcpSocketInfo in expiredConnections)
					this.SocketFailed(GenuineExceptions.Get_Channel_ConnectionClosedAfterTimeout(), tcpSocketInfo);
			}
			catch
			{
			}
			finally
			{
				lock (this)
					this._isInProgress = false;
			}
		}


		#endregion

		#region -- Pool Management -----------------------------------------------------------------

		/// <summary>
		/// Acquires a connection according to message parameters or throw the corresponding exception
		/// if it's impossible.
		/// </summary>
		/// <param name="message">The message being sent.</param>
		/// <returns>The acquired connection.</returns>
		private TcpSocketInfo GetConnectionForSending(Message message)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			TcpSocketInfo tcpSocketInfo = null;
			string uri = null;
			bool isServer = false;
			string remoteUri = null;
			int remoteHostUniqueIdentifier;

			using (new ReaderAutoLocker(this._disposeLock))
			{
				if (this._disposed)
					throw OperationException.WrapException(this._disposeReason);

				switch (message.SecuritySessionParameters.GenuineConnectionType)
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
								tcpSocketInfo = this._persistent.Get(uri, message.ConnectionName) as TcpSocketInfo;

								if (tcpSocketInfo != null && !tcpSocketInfo.IsValid)
								{
									this._persistent.Remove(uri, message.ConnectionName);
									tcpSocketInfo = null;
								}

								if (tcpSocketInfo == null)
								{
									// try to establish a persistent connection
									tcpSocketInfo = LowLevel_OpenConnection(message.Recipient, GenuineConnectionType.Persistent, this.Local.Uri, this.LocalPort, message.ConnectionName, out remoteUri, out remoteHostUniqueIdentifier);

									// update remote host info
									message.Recipient.UpdateUri(remoteUri, remoteHostUniqueIdentifier);
									message.Recipient.PhysicalAddress = tcpSocketInfo.Socket.RemoteEndPoint;
									message.Recipient.LocalPhysicalAddress = tcpSocketInfo.Socket.LocalEndPoint;
									tcpSocketInfo.Remote = message.Recipient;

									// LOG:
									if ( binaryLogWriter != null && binaryLogWriter[LogCategory.HostInformation] > 0 )
									{
										binaryLogWriter.WriteHostInformationEvent("TcpConnectionManager.LowLevel_OpenConnection",
											LogMessageType.HostInformationCreated, null, message.Recipient,
											GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, tcpSocketInfo.ConnectionLevelSecurity, 
											tcpSocketInfo.ConnectionLevelSecurity == null ? null : tcpSocketInfo.ConnectionLevelSecurity.Name, 
											tcpSocketInfo.DbgConnectionId,
											"HostInformation is ready for actions.");
									}

									// OK, connection established
									tcpSocketInfo.MessageContainer = new MessageContainer(this.ITransportContext);
									this._persistent.Set(uri, tcpSocketInfo.ConnectionName, tcpSocketInfo);

									tcpSocketInfo.CloseConnectionAfterInactivity = GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.ClosePersistentConnectionAfterInactivity]);
									tcpSocketInfo.Renew();
									tcpSocketInfo.SignalState(GenuineEventType.GeneralConnectionEstablished, null, null);

									// LOG:
									if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
									{
										binaryLogWriter.WriteEvent(LogCategory.Connection, "TcpConnectionManager.GetConnectionForSending",
											LogMessageType.ConnectionEstablished, null, null, tcpSocketInfo.Remote, null,
											GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
											tcpSocketInfo.ConnectionLevelSecurity, 
											tcpSocketInfo.ConnectionLevelSecurity == null ? null : tcpSocketInfo.ConnectionLevelSecurity.Name, 
											tcpSocketInfo.DbgConnectionId, (int) message.SecuritySessionParameters.GenuineConnectionType, 0, 0, this.GetType().Name, message.Recipient.LocalPhysicalAddress.ToString(), message.Recipient.PhysicalAddress.ToString(), null,
											"The connection to the remote host is established.");
									}

									this.Pool_StartSending(tcpSocketInfo);
									this.Pool_InitiateReceiving(tcpSocketInfo, this.ITransportContext.IParameterProvider);
								}
							}
							else
							{
								// remote host is a client and if there is no connection to it, it's unreachable
								tcpSocketInfo = this._persistent.Get(uri, message.ConnectionName) as TcpSocketInfo;
								if (tcpSocketInfo == null)
									throw GenuineExceptions.Get_Send_DestinationIsUnreachable(message.Recipient.Uri);
							}
						}
						break;

					case GenuineConnectionType.Named:
						if (message.ConnectionName == null)
							message.ConnectionName = message.SecuritySessionParameters.ConnectionName;

						isServer = message.Recipient.Url == null;
						string fullConnectionName = this.GetNamedConnectionName(isServer, message.Recipient, message.ConnectionName);

						try
						{
							// if it's possible to establish a connection to the remote host
							if (! isServer)
							{
								bool shouldBeEstablished = false;
								lock (this._named)
								{
									tcpSocketInfo = this._named[fullConnectionName] as TcpSocketInfo;

									// if it's necessary to establish a connection
									if (tcpSocketInfo == null)
									{
										shouldBeEstablished = true;

										// register a message queue for this connection
										tcpSocketInfo = new TcpSocketInfo(null, this.ITransportContext, message.ConnectionName
#if DEBUG
											,"Stub for the named connection while it's being established."
#endif
											);
										tcpSocketInfo.Remote = message.Recipient;
										tcpSocketInfo.GenuineConnectionType = GenuineConnectionType.Named;
										tcpSocketInfo.LockForSending = true;
										tcpSocketInfo.CloseConnectionAfterInactivity = GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.ConnectTimeout]);
										tcpSocketInfo.MessageContainer = new MessageContainer(this.ITransportContext);
										tcpSocketInfo.Renew();
										this._named[fullConnectionName] = tcpSocketInfo;
									}
								}

								if (shouldBeEstablished)
								{
									// try to establish named connection
									tcpSocketInfo = LowLevel_OpenConnection(message.Recipient, GenuineConnectionType.Named, this.Local.Uri, this.LocalPort, message.ConnectionName, out remoteUri, out remoteHostUniqueIdentifier);

									// update remote host info
									message.Recipient.UpdateUri(remoteUri, remoteHostUniqueIdentifier);
									message.Recipient.PhysicalAddress = tcpSocketInfo.Socket.RemoteEndPoint;
									message.Recipient.LocalPhysicalAddress = tcpSocketInfo.Socket.LocalEndPoint;
									tcpSocketInfo.Remote = message.Recipient;

									// calculate the time to close the connection after
									if (message.SecuritySessionParameters.CloseAfterInactivity == TimeSpan.MinValue)
										tcpSocketInfo.CloseConnectionAfterInactivity = GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.CloseNamedConnectionAfterInactivity]);
									else
										tcpSocketInfo.CloseConnectionAfterInactivity = GenuineUtility.ConvertToMilliseconds(message.SecuritySessionParameters.CloseAfterInactivity);
									tcpSocketInfo.Renew();

									// and exchange it with the registered queue
									lock (this._named)
									{
										TcpSocketInfo stubWithQueue = (TcpSocketInfo) this._named[fullConnectionName];
										this._named[fullConnectionName] = tcpSocketInfo;
										tcpSocketInfo.MessageContainer = stubWithQueue.MessageContainer;
									}

									// LOG:
									if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
									{
										binaryLogWriter.WriteEvent(LogCategory.Connection, "TcpConnectionManager.GetConnectionForSending",
											LogMessageType.ConnectionEstablished, null, null, tcpSocketInfo.Remote, null,
											GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
											tcpSocketInfo.ConnectionLevelSecurity, 
											tcpSocketInfo.ConnectionLevelSecurity == null ? null : tcpSocketInfo.ConnectionLevelSecurity.Name, 
											tcpSocketInfo.DbgConnectionId, (int) message.SecuritySessionParameters.GenuineConnectionType, 0, 0, this.GetType().Name, message.Recipient.LocalPhysicalAddress.ToString(), message.Recipient.PhysicalAddress.ToString(), null,
											"The connection to the remote host is established.");
									}

									this.Pool_StartSending(tcpSocketInfo);
									this.Pool_InitiateReceiving(tcpSocketInfo, this.ITransportContext.IParameterProvider);
								}
							}
							else
							{
								// remote host is a client and if there are no connection to it, it's unreachable
								lock (this._named)
									tcpSocketInfo = this._named[fullConnectionName] as TcpSocketInfo;
								if (tcpSocketInfo == null)
								{
									Exception exception = GenuineExceptions.Get_Send_DestinationIsUnreachable(message.Recipient.Uri);

									// LOG:
									if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
									{
										binaryLogWriter.WriteEvent(LogCategory.Connection, "TcpConnectionManager.GetConnectionForSending",
											LogMessageType.ConnectionEstablished, exception, message, message.Recipient, null,
											GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
											null, null, -1, (int) message.SecuritySessionParameters.GenuineConnectionType, 0, 0, this.GetType().Name, null, null, null,
											"The connection to the remote host cannot be established.");
									}

									throw exception;
								}
							}
						}
						catch(Exception ex)
						{
							tcpSocketInfo.Remote = message.Recipient;
							this.SocketFailed(ex, tcpSocketInfo);

							TcpSocketInfo existingConnection = null;
							lock (this._named)
								existingConnection = this._named[fullConnectionName] as TcpSocketInfo;

							this.SocketFailed(ex, existingConnection);
//
//							MessageContainer messageContainerToBeClearedUp = null;
//							lock (this._named)
//							{
//								TcpSocketInfo existingConnection = this._named[fullConnectionName] as TcpSocketInfo;
//								if (existingConnection != null)
//									messageContainerToBeClearedUp = existingConnection.MessageContainer;
//								this._named.Remove(fullConnectionName);
//							}
//
//							if (messageContainerToBeClearedUp != null)
//								messageContainerToBeClearedUp.Dispose(ex);
//
//							if (ConnectionManager.IsExceptionCritical(ex as OperationException))
//								message.Recipient.Dispose(ex);
							throw;
						}
						break;

					case GenuineConnectionType.Invocation:

						// TODO: remove it after updating this code
						throw new ApplicationException("This version of Genuine Channels does not support the Invocation Connection Pattern.");

						//						// if it's a request, it's necessary to open the connection
						//						if (message.ConnectionId == null || ! message.ConnectionId.StartsWith("~"))
						//						{
						//							// try to find inactive one
						//							ArrayList invocationConnections = null;
						//							lock (this._invocationConnections.SyncRoot)
						//							{
						//								invocationConnections = this._invocationConnections[message.Recipient.Url] as ArrayList;
						//								if (invocationConnections == null)
						//									this._invocationConnections[message.Recipient.Url] = invocationConnections = new ArrayList();
						//							}
						//
						//							lock (invocationConnections)
						//								for ( int i = 0; i < invocationConnections.Count ; i++ )
						//								{
						//									tcpSocketInfo = (TcpSocketInfo) invocationConnections[i];
						//									if (tcpSocketInfo.IsValid && tcpSocketInfo.TcpInvocationFiniteAutomatonState == TcpInvocationFiniteAutomatonState.ClientAvailable)
						//									{
						//										// connection may be broken
						//										message.IsResendAfterFail = true;
						//										tcpSocketInfo.TcpInvocationFiniteAutomatonState = TcpInvocationFiniteAutomatonState.ClientSending;
						//										tcpSocketInfo.InitialMessage = message;
						//										break;
						//									}
						//									else
						//										tcpSocketInfo = null;
						//								}
						//
						//							if (tcpSocketInfo == null)
						//							{
						//								// it is necessary to open a new one
						//								tcpSocketInfo = LowLevel_OpenConnection(message.Recipient, GenuineConnectionType.Invocation, this.Local.Uri, this.LocalPort, null, out remoteUri);
						//								tcpSocketInfo.Remote = message.Recipient;
						//
						//								// update remote host info
						//								message.Recipient.UpdateUri(remoteUri);
						//								tcpSocketInfo.Remote = message.Recipient;
						//								tcpSocketInfo.TcpInvocationFiniteAutomatonState = TcpInvocationFiniteAutomatonState.ClientSending;
						//								tcpSocketInfo.InitialMessage = message;
						//								message.IsResendAfterFail = false;
						//
						//								// calculate the time to close the connection after
						//								if (message.SecuritySessionParameters.CloseAfterInactivity == TimeSpan.MinValue)
						//									tcpSocketInfo.CloseConnectionAfterInactivity = GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.CloseInvocationConnectionAfterInactivity]);
						//								else
						//									tcpSocketInfo.CloseConnectionAfterInactivity = GenuineUtility.ConvertToMilliseconds(message.SecuritySessionParameters.CloseAfterInactivity);
						//								tcpSocketInfo.Renew();
						//
						//								// add opened connection to connection pool
						//								lock (this._invocationConnections.SyncRoot)
						//								{
						//									ArrayList connections = (ArrayList) this._invocationConnections[message.Recipient.Url];
						//									lock (connections)
						//										connections.Add(tcpSocketInfo);
						//								}
						//
						//								this.Pool_InitiateReceiving(tcpSocketInfo, this.ITransportContext.IParameterProvider);
						//							}
						//						}
						//						else
						//						{
						//							// it's a reply, it's necessary just to send the response through 
						//							// the existent connection with the given name
						//							tcpSocketInfo = this._knownInvocationConnections[message.ConnectionId] as TcpSocketInfo;
						//							if (tcpSocketInfo == null)
						//								throw GenuineExceptions.Get_Send_NoNamedConnectionFound(message.ConnectionId);
						//							tcpSocketInfo.TcpInvocationFiniteAutomatonState = TcpInvocationFiniteAutomatonState.ServerSending;
						//							tcpSocketInfo.InitialMessage = message;
						//						}
						//
						//						tcpSocketInfo.Renew();
						//						break;

					default:
						throw GenuineExceptions.Get_Connect_CanNotConnectToRemoteHost(message.Recipient.PrimaryUri, "Invalid type of connection requested.");
				}
			}

			return tcpSocketInfo;
		}

		/// <summary>
		/// Assigns the sending task for a connection.
		/// </summary>
		/// <param name="tcpSocketInfo">Tcp connection.</param>
		private void Pool_StartSending(TcpSocketInfo tcpSocketInfo)
		{
			Message message = null;

			switch (tcpSocketInfo.GenuineConnectionType)
			{
				case GenuineConnectionType.Persistent:
				case GenuineConnectionType.Named:
					lock (tcpSocketInfo)
					{
						// if something uses this socket
						if (tcpSocketInfo.LockForSending)
							return ;

						tcpSocketInfo.LockForSending = true;

						// try to send the next item in the queue
						for ( ; ; )
						{
							message = (Message) tcpSocketInfo.MessageContainer.GetMessage();

							if (message == null)
							{
								tcpSocketInfo.LockForSending = false;
								return ;
							}

							if (message is SyncMessageSlot)
							{
								SyncMessageSlot syncMessageSlot = (SyncMessageSlot) message;
								lock (syncMessageSlot)
								{
									if (! syncMessageSlot.IsValid)
										continue;

									syncMessageSlot.TcpSocketInfo = tcpSocketInfo;
									syncMessageSlot.ConnectionAvailable.Set();
									break;
								}
							}

							// LOG:
							BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
							if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Transport] > 0 )
								binaryLogWriter.WriteEvent(LogCategory.Transport, "TcpConnectionManager.SendAsync",
									LogMessageType.MessageIsSentAsynchronously, null, message, message.Recipient, null,
									GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
									tcpSocketInfo.ConnectionLevelSecurity == null ? null : tcpSocketInfo.ConnectionLevelSecurity, 
									tcpSocketInfo.ConnectionLevelSecurity == null ? null : tcpSocketInfo.ConnectionLevelSecurity.Name, 
									tcpSocketInfo.DbgConnectionId, 0, 0, 0, null, null, null, null,
									"The connection has been obtained and the message is being sent asynchronously.");

							tcpSocketInfo.Message = message;
							tcpSocketInfo.AsyncSendStream = null;
							this.LowLevel_StartSending(message, tcpSocketInfo);
							break;
						}
					}
					break;

				case GenuineConnectionType.Invocation:
					this.Pool_HandleAvailableInvocationOrOneWayConnection(tcpSocketInfo);
					break;
			}
		}

		/// <summary>
		/// Initiates reading incoming content from the socket and dispatching it to the message handler manager.
		/// Should be called only once for persistent and named connections.
		/// </summary>
		/// <param name="tcpSocketInfo">The connection.</param>
		/// <param name="iParameterProvider">Parameter provider or a null reference.</param>
		private void Pool_InitiateReceiving(TcpSocketInfo tcpSocketInfo, IParameterProvider iParameterProvider)
		{
			this.LowLevel_HalfSync_StartReceiving(tcpSocketInfo);
		}

		/// <summary>
		/// Sets correct connection state and parameters when the current operation is successfully completed.
		/// </summary>
		/// <param name="tcpSocketInfo"></param>
		private void Pool_HandleAvailableInvocationOrOneWayConnection(TcpSocketInfo tcpSocketInfo)
		{
			if (tcpSocketInfo.CloseConnectionAfterInactivity <= 0)
				this.SocketFailed(GenuineExceptions.Get_Connection_LifetimeCycleEnded(), tcpSocketInfo);
			else
				tcpSocketInfo.Renew();
		}

		/// <summary>
		/// Destroys the specified connection and releases absolutely all resources belonging to
		/// the remote host the connection points at.
		/// </summary>
		/// <param name="tcpSocketInfo">The connection.</param>
		/// <param name="reason">The reason of disposing connection resources.</param>
		private void Pool_DestroyPersistentConnection(TcpSocketInfo tcpSocketInfo, Exception reason)
		{
			bool disposeMessages = false;
			bool connectionAvailable = true;

			// the queue
			lock (this._persistent.SyncRoot)
			{
				TcpSocketInfo currentTcpSocketInfo = this._persistent.Get(tcpSocketInfo.PrimaryRemoteUri, tcpSocketInfo.ConnectionName) as TcpSocketInfo;

				// if it's still the stub
				if (object.ReferenceEquals(currentTcpSocketInfo, tcpSocketInfo))
				{
					this._persistent.Remove(tcpSocketInfo.PrimaryRemoteUri, tcpSocketInfo.ConnectionName);
					connectionAvailable = this._persistent.Get(tcpSocketInfo.PrimaryRemoteUri, null) != null;
					disposeMessages = true;
				}
			}

			// release messages
			if (disposeMessages)
				tcpSocketInfo.MessageContainer.Dispose(reason);

			// release HostInformation if there are no connections available
			if (! connectionAvailable)
			{
				tcpSocketInfo.SignalState(GenuineEventType.GeneralConnectionClosed, reason, null);
				this.ITransportContext.KnownHosts.ReleaseHostResources(tcpSocketInfo.Remote, reason);
			}

			GenuineThreadPool.QueueUserWorkItem(new WaitCallback(LowLevel_CloseConnection), tcpSocketInfo, false);
		}

		private bool _tcpReadRequestBeforeProcessing;

		/// <summary>
		/// Continues receiving and processing of the message in half-sync mode.
		/// </summary>
		/// <param name="tcpSocketInfoAsObject">The connection.</param>
		private void Pool_ContinueHalfSyncReceiving(object tcpSocketInfoAsObject)
		{
			TcpSocketInfo tcpSocketInfo = null;
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			try
			{
				tcpSocketInfo = (TcpSocketInfo) tcpSocketInfoAsObject;

                bool automaticallyContinueReceiving = (tcpSocketInfo.ConnectionLevelSecurity == null);

				// create the stream
                SyncSocketReadingStream stream = new SyncSocketReadingStream(this, tcpSocketInfo, GenuineUtility.GetTimeout(tcpSocketInfo.CloseConnectionAfterInactivity), automaticallyContinueReceiving);

				tcpSocketInfo.Renew();

				if (stream.IsMessageProcessed)
				{
					// it's a ping

					// LOG:
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
						binaryLogWriter.WriteEvent(LogCategory.Connection, "TcpConnectionManager.Pool_ContinueHalfSyncReceiving",
							LogMessageType.ConnectionPingReceived, null, null, tcpSocketInfo == null ? null : tcpSocketInfo.Remote, null, 
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
							null, null, tcpSocketInfo.DbgConnectionId, 0, 0, 0, null, null, null, null,
							"The ping message has been received. Stream number: {0}.", stream.DbgStreamId);

					if (!automaticallyContinueReceiving)
	                    this.LowLevel_HalfSync_StartReceiving(tcpSocketInfo);

					return ;
				}

				// provide an option to read the entire message first
				Stream resultStream = null;
				bool contentLoggingRequired = binaryLogWriter != null && binaryLogWriter[LogCategory.Transport] > 1;
				if (this._tcpReadRequestBeforeProcessing || contentLoggingRequired)
				{
					resultStream = new GenuineChunkedStream(contentLoggingRequired ? false : true);
					GenuineUtility.CopyStreamToStream(stream, resultStream);
				}
				else
					resultStream = stream;

                // LOG:
                if (binaryLogWriter != null && binaryLogWriter[LogCategory.Transport] > 0)
                    binaryLogWriter.WriteTransportContentEvent(LogCategory.Transport, "TcpConnectionManager.Pool_ContinueHalfSyncReceiving",
                        LogMessageType.ReceivingFinished, null, null, tcpSocketInfo.Remote,
                        contentLoggingRequired ? resultStream : null,
                        GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
                        tcpSocketInfo.DbgConnectionId, binaryLogWriter[LogCategory.Transport] > 1 ? (int)resultStream.Length : 0, tcpSocketInfo.Remote.PhysicalAddress.ToString(),
                        null, null,
                        "Content has been received. Stream number: {0}.", stream.DbgStreamId);

				if (tcpSocketInfo.ConnectionLevelSecurity != null)
                    resultStream = tcpSocketInfo.ConnectionLevelSecurity.Decrypt(resultStream);

                if (!automaticallyContinueReceiving)
                    this.LowLevel_HalfSync_StartReceiving(tcpSocketInfo);

				// provide it to the handler
				this.ITransportContext.IIncomingStreamHandler.HandleMessage(resultStream, tcpSocketInfo.Remote, tcpSocketInfo.GenuineConnectionType, tcpSocketInfo.ConnectionName, tcpSocketInfo.DbgConnectionId, true, tcpSocketInfo._iMessageRegistrator, tcpSocketInfo.ConnectionLevelSecurity, null);

				tcpSocketInfo.Renew();
			}
			catch(Exception ex)
			{
				this.SocketFailed(ex, tcpSocketInfo);
			}
		}

		/// <summary>
		/// Gets the name of the TCP connection.
		/// </summary>
		/// <param name="isServer">A boolean value indicating whether this connection manager works as a server.</param>
		/// <param name="hostInformation">The HostInformation of the remote host.</param>
		/// <param name="connectionName">The name of the connection.</param>
		/// <returns>The name of the TCP connection.</returns>
		private string GetNamedConnectionName(bool isServer, HostInformation hostInformation, string connectionName)
		{
			//			bool isServer = connectionName.StartsWith("~");
			return isServer ? hostInformation.Uri + "/" + connectionName : hostInformation.Url + "/" + connectionName;
		}

		/// <summary>
		/// Processes failed sockets.
		/// </summary>
		/// <param name="exception">The source exception.</param>
		/// <param name="tcpSocketInfo">The socket.</param>
		private void SocketFailed(Exception exception, TcpSocketInfo tcpSocketInfo)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			try
			{
				lock (tcpSocketInfo)
				{
					// if connection's resources were released
					if (! tcpSocketInfo.IsValid)
						return ;

					tcpSocketInfo.IsValid = false;
					tcpSocketInfo.LockForSending = true;
				}

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "TcpConnectionManager.SocketFailed",
						LogMessageType.ConnectionFailed, exception, null, tcpSocketInfo.Remote, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
						tcpSocketInfo.ConnectionLevelSecurity, 
						tcpSocketInfo.ConnectionLevelSecurity == null ? null : tcpSocketInfo.ConnectionLevelSecurity.Name, 
						tcpSocketInfo.DbgConnectionId, 0, 0, 0, null, null, null, null,
						"TCP connection has failed.");
				}

				// choose the type of the connection
				switch (tcpSocketInfo.GenuineConnectionType)
				{
					case GenuineConnectionType.Persistent:
						// determine whether the problem is critical
						bool criticalError = ConnectionManager.IsExceptionCritical(exception as OperationException) ||
							(int) this.ITransportContext.IParameterProvider[GenuineParameter.ReconnectionTries] <= 0 ||
							((TimeSpan) this.ITransportContext.IParameterProvider[GenuineParameter.SleepBetweenReconnections]).TotalMilliseconds <= 0;
						bool queueWasOverflowed = false;

						lock (tcpSocketInfo)
						{
							try
							{
								if (tcpSocketInfo.Message != null)
								{
									tcpSocketInfo.Message.SerializedContent.Position = 0;
									tcpSocketInfo.MessageContainer.AddMessage(tcpSocketInfo.Message, false);
								}
								tcpSocketInfo.Message = null;
							}
							catch(Exception)
							{
								// queue is overrun
								criticalError = true;
								queueWasOverflowed = true;
							}

							try
							{
								// it automatically resets the stream position
								tcpSocketInfo.MessagesSentSynchronously.MoveMessages(tcpSocketInfo.MessageContainer);
							}
							catch(Exception)
							{
								// queue is overrun
								criticalError = true;
								queueWasOverflowed = true;
							}
						}

						// check whether it is a primary connection failure
						if (! object.ReferenceEquals(tcpSocketInfo, this._persistent.Get(tcpSocketInfo.PrimaryRemoteUri, tcpSocketInfo.ConnectionName)) && 
							! queueWasOverflowed)
						{
							// it's either the previous stub or a parallel connection
							// close it silently
							GenuineThreadPool.QueueUserWorkItem(new WaitCallback(this.LowLevel_CloseConnection), tcpSocketInfo, false);
							return ;
						}

						if (criticalError)
						{
							// LOG:
							if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
								binaryLogWriter.WriteEvent(LogCategory.Connection, "TcpConnectionManager.SocketFailed",
									LogMessageType.ConnectionReestablished, exception, null, tcpSocketInfo.Remote, null, 
									GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
									null, null, tcpSocketInfo.DbgConnectionId, 0, 0, 0, null, null, null, null,
									"The connection cannot be reestablished due to the critical exception.");

							this.Pool_DestroyPersistentConnection(tcpSocketInfo, exception);
							return ;
						}

						// the error is not critical and the connection will be possibly restored
						tcpSocketInfo.SignalState(GenuineEventType.GeneralConnectionReestablishing, exception, null);

						try
						{
							// try to reestablish the connection
							lock (this._persistent.SyncRoot)
							{
								TcpSocketInfo existentSocketInfo = this._persistent.Get(tcpSocketInfo.PrimaryRemoteUri, tcpSocketInfo.ConnectionName) as TcpSocketInfo;

								// FIX 2.5.2. We can reestablish a connection if and only if no connections have been
								// already established. Otherwise, we can overwrite currently established connection
								if (ReferenceEquals(existentSocketInfo, tcpSocketInfo))
								{
									lock (tcpSocketInfo)
									{
										// create stub connection
										TcpSocketInfo connectionWithQueue = new TcpSocketInfo(null, this.ITransportContext, tcpSocketInfo.ConnectionName
#if DEBUG
											,"Stub for the persistent connection while it's being established."
#endif
											);
										connectionWithQueue.LockForSending = true;
										connectionWithQueue.MessageContainer = tcpSocketInfo.MessageContainer;
										connectionWithQueue.CloseConnectionAfterInactivity = GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.MaxTimeSpanToReconnect]);
										connectionWithQueue.Remote = tcpSocketInfo.Remote;

										connectionWithQueue.GenuineConnectionType = GenuineConnectionType.Persistent;
										connectionWithQueue.IsServer = tcpSocketInfo.IsServer;
										connectionWithQueue.Renew();

										// LOG:
										if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
											binaryLogWriter.WriteEvent(LogCategory.Connection, "TcpConnectionManager.SocketFailed",
												LogMessageType.ConnectionReestablishing, exception, null, tcpSocketInfo.Remote, null, 
												GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
												null, null, connectionWithQueue.DbgConnectionId, 0, 0, 0, null, null, null, null,
												"The stub connection is constructed after exception.");

										connectionWithQueue.Remote.Renew(connectionWithQueue.CloseConnectionAfterInactivity, false);
										this._persistent.Set(tcpSocketInfo.PrimaryRemoteUri, tcpSocketInfo.ConnectionName, connectionWithQueue);

										// initiate connection reestablishing
										if (tcpSocketInfo.Remote.GenuinePersistentConnectionState == GenuinePersistentConnectionState.Opened)
											GenuineThreadPool.QueueUserWorkItem(new WaitCallback(this.ReestablishConnection), connectionWithQueue, true);
									}
								}
								else
								{
									// LOG:
									if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
										binaryLogWriter.WriteEvent(LogCategory.Connection, "TcpConnectionManager.SocketFailed",
											LogMessageType.ConnectionReestablishing, exception, null, tcpSocketInfo.Remote, null, 
											GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
											null, null, tcpSocketInfo.DbgConnectionId, 0, 0, 0, null, null, null, null,
											"The connection establishing has not been initiated since the original TcpSocketInfo is absent.");
								}
							}
						}
						catch(Exception ex)
						{
							// LOG:
							if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
								binaryLogWriter.WriteEvent(LogCategory.Connection, "TcpConnectionManager.SocketFailed",
									LogMessageType.ConnectionReestablishing, ex, null, 
									tcpSocketInfo == null ? null : tcpSocketInfo.Remote, null, 
									GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
									null, null, tcpSocketInfo.DbgConnectionId, 0, 0, 0, null, null, null, null,
									"An exception is occurred while trying to initiate connection reestablishing.");

							// queue overloaded or fatal connection error
							// force connection closing
							this.SocketFailed(GenuineExceptions.Get_Receive_ConnectionClosed(), tcpSocketInfo);
							return ;
						}
						break;

					case GenuineConnectionType.Named:
						// update the pool
						string fullConnectionName = GetNamedConnectionName(tcpSocketInfo.IsServer, tcpSocketInfo.Remote, tcpSocketInfo.ConnectionName);

						lock (this._named)
						{
							if (object.ReferenceEquals(this._named[fullConnectionName], tcpSocketInfo))
								this._named.Remove(fullConnectionName);
						}

						// release queue
						if (tcpSocketInfo.MessageContainer != null)
							tcpSocketInfo.MessageContainer.Dispose(exception);

						// release all of them
						if (exception is GenuineExceptions.ChannelClosed || exception is GenuineExceptions.ServerHasBeenRestarted)
							this.ITransportContext.KnownHosts.ReleaseHostResources(tcpSocketInfo.Remote, exception);
						break;

					case GenuineConnectionType.Invocation:
						lock (tcpSocketInfo)
						{
							switch (tcpSocketInfo.TcpInvocationFiniteAutomatonState)
							{
								case TcpInvocationFiniteAutomatonState.ClientAvailable:
								case TcpInvocationFiniteAutomatonState.ClientReceiving:
								case TcpInvocationFiniteAutomatonState.ClientSending:
									// break client state cycle according to FA transition diagram
									lock (this._invocation)
									{
										ArrayList connections = (ArrayList) this._invocation[tcpSocketInfo.Remote.Url];
										if (connections != null)
											lock (connections)
												connections.Remove(tcpSocketInfo);
									}

									lock (tcpSocketInfo)
									{
										if (tcpSocketInfo.InitialMessage != null)
											if (tcpSocketInfo.InitialMessage.IsResendAfterFail && ! tcpSocketInfo.InitialMessage.IsSynchronous)
											{
												Message message = tcpSocketInfo.InitialMessage;
												tcpSocketInfo.InitialMessage = null;
												this.Send(message);
											}
											else
											{
												this.ITransportContext.IIncomingStreamHandler.DispatchException(tcpSocketInfo.InitialMessage, exception);
												tcpSocketInfo.InitialMessage = null;
											}
									}
									break;

								case TcpInvocationFiniteAutomatonState.ServerAwaiting:
								case TcpInvocationFiniteAutomatonState.ServerExecution:
								case TcpInvocationFiniteAutomatonState.ServerSending:
									lock (this._knownInvocationConnections.SyncRoot)
										this._knownInvocationConnections.Remove(tcpSocketInfo.ConnectionName);
									break;
							}
						}
						break;
				}

				// close the socket
				GenuineThreadPool.QueueUserWorkItem(new WaitCallback(LowLevel_CloseConnection), tcpSocketInfo, false);
			}
			catch(Exception ex)
			{
				// LOG:
				if ( binaryLogWriter != null )
					binaryLogWriter.WriteImplementationWarningEvent("TcpConnectionManager.SocketFailed",
						LogMessageType.Warning, ex, GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						"An unexpected exception is raised inside TcpConnectionManager.SocketFailed method. Most likely, something must be fixed.");
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

			// Prepare dual-mode (IPv4 & IPv6) for the socket listener (Vista and Longhorn above only).
			// Idea based on http://blogs.msdn.com/wndp/archive/2006/10/24/creating-ip-agnostic-applications-part-2-dual-mode-sockets.aspx
			AddressFamily addressFamily = ipEndPoint.AddressFamily;
			if (addressFamily == AddressFamily.InterNetwork && GenuineUtility.LocalSystemSupportsIPv6)
				addressFamily = AddressFamily.InterNetworkV6;

			// start socket listening
			Socket socket = new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp);

			try
			{
				// set linger option
				LingerOption lingerOption = new LingerOption(true, 3);
				socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, lingerOption);

				// setup port sharing: share the port (other apps can use the same one):
				//TODO: discuss, if this should be an properties entry controlled behavior?
				//socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);

				// setup dual listener mode, if IPv6 and IPv4 running at the same time.
				if (addressFamily == AddressFamily.InterNetworkV6 && GenuineUtility.LocalSystemSupportsIPv6)
				{
					// Set dual-mode (IPv4 & IPv6) for the socket listener (Vista and Longhorn above only).
					// 27 is equivalent to IPV6_V6ONLY socket option in the winsock snippet below,
					// based on http://blogs.msdn.com/wndp/archive/2006/10/24/creating-ip-agnostic-applications-part-2-dual-mode-sockets.aspx
					socket.SetSocketOption(SocketOptionLevel.IPv6, (SocketOptionName) 27, 0);

					if (!GenuineUtility.IsIPv4MappedToIPv6(ipEndPoint.Address))
					{
						if (Equals(ipEndPoint.Address, IPAddress.Any))
							ipEndPoint = new IPEndPoint(IPAddress.IPv6Any, port);
						else
							ipEndPoint = new IPEndPoint(GenuineUtility.MapToIPv6(ipEndPoint.Address), port);
					}
				}
				
				socket.Bind(ipEndPoint);
				socket.Listen(15);

				AcceptConnectionClosure acceptConnectionClosure = new AcceptConnectionClosure(this.ITransportContext, endPoint, socket, this);

				// register the listening closure
				lock (this._listeningSockets.SyncRoot)
				{
					if (_listeningSockets.ContainsKey(endPoint))
					{
						this.LowLevel_CloseConnection(socket);
						throw GenuineExceptions.Get_Server_EndPointIsAlreadyBeingListenedTo(endPoint);
					}
					_listeningSockets[endPoint] = acceptConnectionClosure;
				}

				this.ITransportContext.IGenuineEventProvider.Fire(new GenuineEventArgs(GenuineEventType.GeneralListenerStarted, null, this.Local, endPoint));

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.AcceptingConnection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.AcceptingConnection, "TcpConnectionManager.StartListening",
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
					binaryLogWriter.WriteEvent(LogCategory.AcceptingConnection, "TcpConnectionManager.StartListening",
						LogMessageType.ListeningStarted, ex, null, null, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, -1, 0, 0, 0, endPoint, null, null, null,
						"A listening socket cannot be associated with the \"{0}\" local end point.", endPoint);
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
						// probably it will be better to find 0.0.0.0 interface here
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
				binaryLogWriter.WriteEvent(LogCategory.AcceptingConnection, "TcpConnectionManager.StopListening",
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
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			//			GenuineConnectionType genuineConnectionType = GenuineConnectionType.None;
			string remoteUri = "Unknown.";
			string remoteAddress = "Address of the remote host.";
			string connectionName;
			int remoteHostUniqueIdentifier;
			TcpSocketInfo tcpSocketInfo = null;
			HostInformation hostInformation = null;

			using (new ReaderAutoLocker(this._disposeLock))
			{
				if (this._disposed)
					throw OperationException.WrapException(this._disposeReason);
			}

			using (new ThreadDataSlotKeeper(OccupiedThreadSlots.SocketDuringEstablishing, clientSocket))
			{
				try
				{
					// provide a possibility to decline the connection
					ConnectionAcceptedCancellableEventParameter connectionAcceptedCancellableEventParameter = new ConnectionAcceptedCancellableEventParameter();
					connectionAcceptedCancellableEventParameter.Socket = clientSocket;
					connectionAcceptedCancellableEventParameter.IPEndPoint = (IPEndPoint) clientSocket.RemoteEndPoint;
					this.ITransportContext.IGenuineEventProvider.Fire(new GenuineEventArgs(GenuineEventType.GTcpConnectionAccepted, null, null, connectionAcceptedCancellableEventParameter));
					if (connectionAcceptedCancellableEventParameter.Cancel)
						throw GenuineExceptions.Get_Connect_CanNotAcceptIncomingConnection("Connection accepting was cancelled by the event consumer.");

					// accept the connection
					Exception reasonOfStateLost = null;
					tcpSocketInfo = this.LowLevel_AcceptConnection(clientSocket, 
						this.Local.Uri, out remoteUri, out remoteAddress, out connectionName, out remoteHostUniqueIdentifier, out reasonOfStateLost);

					// get the remote host
					hostInformation = this.ITransportContext.KnownHosts[remoteUri];
					if (remoteAddress != null)
						hostInformation.UpdateUrl(remoteAddress);
					tcpSocketInfo.Remote = hostInformation;
					hostInformation.PhysicalAddress = tcpSocketInfo.Socket.RemoteEndPoint;
					hostInformation.LocalPhysicalAddress = tcpSocketInfo.Socket.LocalEndPoint;

					// LOG:
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.HostInformation] > 0 )
					{
						binaryLogWriter.WriteHostInformationEvent("TcpConnectionManager.LowLevel_AcceptConnection",
							LogMessageType.HostInformationCreated, null, hostInformation, 
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, tcpSocketInfo.ConnectionLevelSecurity, 
							tcpSocketInfo.ConnectionLevelSecurity == null ? null : tcpSocketInfo.ConnectionLevelSecurity.Name, 
							tcpSocketInfo.DbgConnectionId, "HostInformation is ready for actions.");
					}

					using (new ReaderAutoLocker(this._disposeLock))
					{
						if (this._disposed)
							throw OperationException.WrapException(this._disposeReason);

						switch (tcpSocketInfo.GenuineConnectionType)
						{
							case GenuineConnectionType.Persistent:
								lock (hostInformation.PersistentConnectionEstablishingLock)
								{
									if (hostInformation.GenuinePersistentConnectionState == GenuinePersistentConnectionState.Opened)
										throw GenuineExceptions.Get_Processing_LogicError("Remote host tries to open persistent connection while it's a server.");
									hostInformation.GenuinePersistentConnectionState = GenuinePersistentConnectionState.Accepted;

									// close existent connection
									lock (this._persistent.SyncRoot)
									{
										TcpSocketInfo existentSocketInfo = this._persistent.Get(remoteUri, connectionName) as TcpSocketInfo;
										if (existentSocketInfo != null)
										{
											lock (existentSocketInfo)
												tcpSocketInfo.MessageContainer = existentSocketInfo.MessageContainer;
											this.SocketFailed(GenuineExceptions.Get_Connect_ConnectionReestablished(), existentSocketInfo);
											this.LowLevel_CloseConnection(existentSocketInfo);
										}
										else
										{
											tcpSocketInfo.MessageContainer = new MessageContainer(this.ITransportContext);
										}

										// register the new connection
										this._persistent.Set(remoteUri, connectionName, tcpSocketInfo);
									}
								}

								tcpSocketInfo.CloseConnectionAfterInactivity = GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.ClosePersistentConnectionAfterInactivity]);
								tcpSocketInfo.Renew();
								tcpSocketInfo.SignalState(GenuineEventType.GeneralConnectionEstablished, null, null);

								// LOG:
								if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
								{
									binaryLogWriter.WriteEvent(LogCategory.Connection, "TcpConnectionManager.IAcceptConnectionConsumer.AcceptConnection",
										LogMessageType.ConnectionEstablished, null, null, tcpSocketInfo.Remote, null,
										GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
										tcpSocketInfo.ConnectionLevelSecurity, 
										tcpSocketInfo.ConnectionLevelSecurity == null ? null : tcpSocketInfo.ConnectionLevelSecurity.Name, 
										tcpSocketInfo.DbgConnectionId, (int) tcpSocketInfo.GenuineConnectionType, 0, 0, this.GetType().Name, hostInformation.LocalPhysicalAddress.ToString(), hostInformation.PhysicalAddress.ToString(), null,
										"The persistent connection is established.");
								}

								// to avoid sending previous messages, encrypted by previously used Security Sessions
								if (reasonOfStateLost != null)
								{
									tcpSocketInfo.MessagesSentSynchronously.ReleaseAllMessages();
									tcpSocketInfo.MessageContainer.Dispose(reasonOfStateLost);
									tcpSocketInfo.MessageContainer = new MessageContainer(this.ITransportContext);
								}

								// start processing
								this.Pool_StartSending(tcpSocketInfo);
								this.Pool_InitiateReceiving(tcpSocketInfo, this.ITransportContext.IParameterProvider);
								break;

							case GenuineConnectionType.Named:
								// register the new connection
								string fullConnectionName = remoteUri + "/" + tcpSocketInfo.ConnectionName;
								tcpSocketInfo.MessageContainer = new MessageContainer(this.ITransportContext);
								lock (this._named)
									this._named[fullConnectionName] = tcpSocketInfo;

								// LOG:
								if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
								{
									binaryLogWriter.WriteEvent(LogCategory.Connection, "TcpConnectionManager.IAcceptConnectionConsumer.AcceptConnection",
										LogMessageType.ConnectionEstablished, null, null, tcpSocketInfo.Remote, null,
										GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
										tcpSocketInfo.ConnectionLevelSecurity, 
										tcpSocketInfo.ConnectionLevelSecurity == null ? null : tcpSocketInfo.ConnectionLevelSecurity.Name, 
										tcpSocketInfo.DbgConnectionId, (int) tcpSocketInfo.GenuineConnectionType, 0, 0, this.GetType().Name, hostInformation.LocalPhysicalAddress.ToString(), hostInformation.PhysicalAddress.ToString(), null,
										"The named connection is established.");
								}

								// start processing
								this.Pool_StartSending(tcpSocketInfo);
								this.Pool_InitiateReceiving(tcpSocketInfo, this.ITransportContext.IParameterProvider);

								tcpSocketInfo.CloseConnectionAfterInactivity = GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.CloseNamedConnectionAfterInactivity]);
								break;

							case GenuineConnectionType.Invocation:
								// register the new connection
								tcpSocketInfo.TcpInvocationFiniteAutomatonState = TcpInvocationFiniteAutomatonState.ServerAwaiting;
								this._knownInvocationConnections[tcpSocketInfo.ConnectionName] = tcpSocketInfo;

								// LOG:
								if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
								{
									binaryLogWriter.WriteEvent(LogCategory.Connection, "TcpConnectionManager.IAcceptConnectionConsumer.AcceptConnection",
										LogMessageType.ConnectionEstablished, null, null, tcpSocketInfo.Remote, null,
										GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
										tcpSocketInfo.ConnectionLevelSecurity, 
										tcpSocketInfo.ConnectionLevelSecurity == null ? null : tcpSocketInfo.ConnectionLevelSecurity.Name, 
										tcpSocketInfo.DbgConnectionId, (int) tcpSocketInfo.GenuineConnectionType, 0, 0, this.GetType().Name, hostInformation.LocalPhysicalAddress.ToString(), hostInformation.PhysicalAddress.ToString(), null,
										"The invocation connection is established.");
								}

								// start processing
								this.Pool_InitiateReceiving(tcpSocketInfo, this.ITransportContext.IParameterProvider);

								tcpSocketInfo.CloseConnectionAfterInactivity = GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.CloseInvocationConnectionAfterInactivity]);
								break;
						}
					}

					// start the shutdown timer
					tcpSocketInfo.Renew();
				}
				catch(Exception ex)
				{
					// LOG:
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.AcceptingConnection] > 0 )
						binaryLogWriter.WriteEvent(LogCategory.AcceptingConnection, "TcpConnectionManager.IAcceptConnectionConsumer.AcceptConnection",
							LogMessageType.ConnectionAccepting, ex, null, hostInformation, null, 
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
							null, null, tcpSocketInfo == null ? -1 : tcpSocketInfo.DbgConnectionId, 
							0, 0, 0, null, null, null, null,
							"The inbound connection has not been accepted.");

					SocketUtility.CloseSocket(clientSocket);
				}
			}
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

		#region -- IConnectionManager and IDispose -------------------------------------------------

		private PersistentConnectionStorage.ProcessConnectionEventHandler _releaseConnections_InspectPersistentConnections;
		private class ReleaseConnections_Parameters
		{
			public ArrayList FailedConnections;
			public HostInformation HostInformation;
			public GenuineConnectionType GenuineConnectionType;
		}

		/// <summary>
		/// Finds connections to be released.
		/// </summary>
		/// <param name="tcpSocketInfoAsObject">The connection.</param>
		/// <param name="releaseConnections_ParametersAsObject">Stuff to make decisions and to save the results.</param>
		private void ReleaseConnections_InspectPersistentConnections(object tcpSocketInfoAsObject, object releaseConnections_ParametersAsObject)
		{
			TcpSocketInfo tcpSocketInfo = (TcpSocketInfo) tcpSocketInfoAsObject;
			ReleaseConnections_Parameters releaseConnections_Parameters = (ReleaseConnections_Parameters) releaseConnections_ParametersAsObject;

			if (releaseConnections_Parameters.HostInformation != null && tcpSocketInfo.Remote != releaseConnections_Parameters.HostInformation)
				return ;
			if ( (releaseConnections_Parameters.GenuineConnectionType & tcpSocketInfo.GenuineConnectionType) == 0)
				return ;

			releaseConnections_Parameters.FailedConnections.Add(tcpSocketInfo);
		}

		/// <summary>
		/// Closes the specified connections to the remote host and releases acquired resources.
		/// </summary>
		/// <param name="hostInformation">Host information or a null reference to close all connection according to specified types.</param>
		/// <param name="genuineConnectionType">What kind of connections will be affected by this operation.</param>
		/// <param name="reason">Reason of resource releasing.</param>
		public override void ReleaseConnections(HostInformation hostInformation, GenuineConnectionType genuineConnectionType, Exception reason)
		{
			TcpSocketInfo tcpSocketInfo = null;
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			// LOG:
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
			{
				binaryLogWriter.WriteEvent(LogCategory.Connection, "TcpConnectionManager.ReleaseConnections",
					LogMessageType.ReleaseConnections, reason, null, hostInformation, null, 
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
					null, null, -1, 0, 0, 0, Enum.Format(typeof(GenuineConnectionType), genuineConnectionType, "g"), null, null, null,
					"Connections \"{0}\" are terminated.", Enum.Format(typeof(GenuineConnectionType), genuineConnectionType, "g"), null);
			}

			// go through all connections and close such of them that fall under the specified category
			ArrayList failedConnections = new ArrayList();

			// persistent
			ReleaseConnections_Parameters releaseConnections_Parameters = new ReleaseConnections_Parameters();
			releaseConnections_Parameters.FailedConnections = failedConnections;
			releaseConnections_Parameters.HostInformation = hostInformation;
			releaseConnections_Parameters.GenuineConnectionType = genuineConnectionType;

			this._persistent.InspectAllConnections(this._releaseConnections_InspectPersistentConnections, releaseConnections_Parameters);

			// named
			if ( (genuineConnectionType & GenuineConnectionType.Persistent) != 0 || (genuineConnectionType & GenuineConnectionType.Named) != 0)
				lock (this._named)
				{
					foreach (DictionaryEntry entry in this._named)
					{
						tcpSocketInfo = (TcpSocketInfo) entry.Value;
						if (hostInformation != null && tcpSocketInfo.Remote != hostInformation)
							continue;
						if ( (genuineConnectionType & tcpSocketInfo.GenuineConnectionType) == 0)
							continue;

						failedConnections.Add(tcpSocketInfo);
					}
				}

			// invocation
			if ( (genuineConnectionType & GenuineConnectionType.Invocation) != 0)
			{
				lock (this._invocation.SyncRoot)
					foreach (DictionaryEntry entry in this._invocation)
					{
						ArrayList connections = (ArrayList) entry.Value;
						lock (connections)
						{
							foreach (TcpSocketInfo nextTcpSocketInfo in connections)
							{
								if (hostInformation != null && nextTcpSocketInfo.Remote != hostInformation)
									continue;

								failedConnections.Add(nextTcpSocketInfo);
							}
						}
					}

				lock (this._knownInvocationConnections.SyncRoot)
					foreach (DictionaryEntry entry in this._knownInvocationConnections)
					{
						tcpSocketInfo = (TcpSocketInfo) entry.Value;
						if (hostInformation != null && tcpSocketInfo.Remote != hostInformation)
							continue;

						failedConnections.Add(tcpSocketInfo);
					}
			}

			// close connections
			foreach (TcpSocketInfo failedTcpSocketInfo in failedConnections)
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Security] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "TcpConnectionManager.ReleaseConnections",
						LogMessageType.ConnectionShuttingDown, reason, null, failedTcpSocketInfo.Remote, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, failedTcpSocketInfo.DbgConnectionId, 0, 0, 0, null, null, null, null,
						"The connection is terminated.");
				}

				this.SocketFailed(GenuineExceptions.Get_Channel_ConnectionShutDown(reason), failedTcpSocketInfo);

				if (failedTcpSocketInfo.GenuineConnectionType == GenuineConnectionType.Persistent)
					failedTcpSocketInfo.SignalState(GenuineEventType.GeneralConnectionClosed, reason, null);
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
		/// Releases all required resources. Closes all opened connections. 
		/// Stops the network activity. Dispatches the provided exception to all invocations 
		/// made via this Connection Manager.
		/// </summary>
		/// <param name="reason"></param>
		public override void InternalDispose(Exception reason)
		{
			this.ReleaseConnections(null, GenuineConnectionType.All, reason);

			// stop listening to all ports
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

	}
}
