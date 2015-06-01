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
using System.Runtime.Serialization;

using Belikov.Common.ThreadProcessing;
using Belikov.GenuineChannels.BufferPooling;
using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Parameters;
using Belikov.GenuineChannels.Receiving;
using Belikov.GenuineChannels.Security;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Utilities;

namespace Belikov.GenuineChannels.GenuineHttp
{
	/// <summary>
	/// Implements HTTP transport manager working via .NET Remoting http web request implementation.
	/// </summary>
	internal class HttpClientConnectionManager : ConnectionManager, ITimerConsumer
	{
		/// <summary>
		/// Constructs an instance of the HttpClientConnectionManager class.
		/// </summary>
		/// <param name="iTransportContext">The transport context.</param>
		public HttpClientConnectionManager(ITransportContext iTransportContext) : base(iTransportContext)
		{
			this.Local = new HostInformation("_ghttp://" + iTransportContext.HostIdentifier, iTransportContext);

			// calculate host renewing timespan
			this._hostRenewingSpan = GenuineUtility.ConvertToMilliseconds(iTransportContext.IParameterProvider[GenuineParameter.ClosePersistentConnectionAfterInactivity]) + GenuineUtility.ConvertToMilliseconds(iTransportContext.IParameterProvider[GenuineParameter.MaxTimeSpanToReconnect]);
			this._internal_TimerCallback = new WaitCallback(Internal_TimerCallback);

			this._pool_Sender_OnEndSending_ContinueExchange = new WaitCallback(this.Pool_Sender_OnEndSending_ContinueExchange);
			this._listener_OnEndReceiving_ContinueExchange = new WaitCallback(this.Pool_Sender_OnEndSending_ContinueExchange);

			this._webRequestInitiationTimeout = (TimeSpan) iTransportContext.IParameterProvider[GenuineParameter.HttpWebRequestInitiationTimeout];
			this._httpAsynchronousRequestTimeout = GenuineUtility.ConvertToMilliseconds(iTransportContext.IParameterProvider[GenuineParameter.HttpAsynchronousRequestTimeout]);

			TimerProvider.Attach(this);
		}

		/// <summary>
		/// Sends the message to the remote host.
		/// </summary>
		/// <param name="message">The message to be sent.</param>
		protected override void InternalSend(Message message)
		{
			switch (message.SecuritySessionParameters.GenuineConnectionType)
			{
				case GenuineConnectionType.Persistent:
					this.InternalSend(message, this.Pool_GetConnectionForSending(message));
					break;

				case GenuineConnectionType.Named:
					throw new NotSupportedException("Genuine HTTP client connection manager doesn't support named connections.");

				case GenuineConnectionType.Invocation:
					HttpInvocationConnection httpInvocationConnection = this.FindInvocationConnection(message);
					httpInvocationConnection.SendMessage(message);
					break;
			}
		}

		/// <summary>
		/// Sends the message to the remote host.
		/// </summary>
		/// <param name="message">The message to be sent.</param>
		/// <param name="httpClientConnection">The connection.</param>
		private void InternalSend(Message message, HttpClientConnection httpClientConnection)
		{
			bool sendingLockObtained = false;

			lock (httpClientConnection.SendingLock)
			{
				if (! httpClientConnection.IsSent)
				{
					httpClientConnection.IsSent = true;
					sendingLockObtained = true;
				}
				else
				{
					httpClientConnection.MessageContainer.AddMessage(message, false);
				}
			}

			if (sendingLockObtained)
				this.LowLevel_Sender_Send(message, httpClientConnection, httpClientConnection.OnEndSending, false);
		}


		#region -- Invocation connections ----------------------------------------------------------

		/// <summary>
		/// Set of invocation connections { remote url => connection }.
		/// </summary>
		private Hashtable _invocation = Hashtable.Synchronized(new Hashtable());

		/// <summary>
		/// Gets an invocation connection that is able to deliver a message and receive a result.
		/// </summary>
		/// <param name="message">The source message.</param>
		/// <returns>An invocation connection that is able to deliver a message and receive a result.</returns>
		private HttpInvocationConnection FindInvocationConnection(Message message)
		{
			lock (this._invocation.SyncRoot)
			{
				HttpInvocationConnection httpInvocationConnection = this._invocation[message.Recipient.Url] as HttpInvocationConnection;
				if (httpInvocationConnection == null)
					this._invocation[message.Recipient.Url] = httpInvocationConnection = new HttpInvocationConnection(this.ITransportContext, message.Recipient);
				return httpInvocationConnection;
			}
		}

		#endregion

		#region -- Pool management -----------------------------------------------------------------

		/// <summary>
		/// Set of connections {remote url => connection}.
		/// </summary>
		private Hashtable _persistent = Hashtable.Synchronized(new Hashtable());

		/// <summary>
		/// TimeSpan to renew the host resource for.
		/// </summary>
		private int _hostRenewingSpan;

		/// <summary>
		/// Opens or returns established connection according to message parameters.
		/// </summary>
		/// <param name="message">The message.</param>
		/// <returns>The established connection.</returns>
		private HttpClientConnection Pool_GetConnectionForSending(Message message)
		{
			HttpClientConnection httpClientConnection = null;

			using (new ReaderAutoLocker(this._disposeLock))
			{
				if (this._disposed)
					throw OperationException.WrapException(this._disposeReason);
			}

			string connectionName = message.ConnectionName;
			if (connectionName == null || connectionName.Length <= 0)
				connectionName = "$/__GC/" + message.Recipient.PrimaryUri;

			if (! Monitor.TryEnter(message.Recipient.PersistentConnectionEstablishingLock, GenuineUtility.GetMillisecondsLeft(message.FinishTime)))
				throw GenuineExceptions.Get_Send_Timeout();

			try
			{
				lock (this._persistent.SyncRoot)
				{
					httpClientConnection = this._persistent[message.Recipient.Url] as HttpClientConnection;

					if (httpClientConnection != null && httpClientConnection._disposed)
					{
						this._persistent.Remove(message.Recipient.Url);
						httpClientConnection = null;
					}

					if (httpClientConnection != null)
						return httpClientConnection;
				}

				// it's necessary to open the connection to the remote host
				httpClientConnection = this.LowLevel_OpenConnection(message.Recipient, message.SecuritySessionParameters.GenuineConnectionType, connectionName);

				using (new ReaderAutoLocker(this._disposeLock))
				{
					if (this._disposed)
					{
						httpClientConnection.Dispose(this._disposeReason);
						throw OperationException.WrapException(this._disposeReason);
					}

					this._persistent[message.Recipient.Url] = httpClientConnection;
				}

				httpClientConnection.SignalState(GenuineEventType.GeneralConnectionEstablished, null, null);
				httpClientConnection.MessageContainer = new MessageContainer(this.ITransportContext);
			}
			finally
			{
				Monitor.Exit(message.Recipient.PersistentConnectionEstablishingLock);
			}

			return httpClientConnection;
		}

		/// <summary>
		/// Finishes sending a message through the connection.
		/// </summary>
		/// <param name="httpClientConnection">The connection.</param>
		/// <param name="httpWebResponse">The received response.</param>
		public void Pool_Sender_OnEndSending(HttpClientConnection httpClientConnection, HttpWebResponse httpWebResponse)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			Stream inputStream = null;

			try
			{
				httpClientConnection.Remote.Renew(this._hostRenewingSpan, false);
				httpClientConnection.LastMessageWasReceviedAt = GenuineUtility.TickCount;
				httpClientConnection.SignalState(GenuineEventType.GeneralConnectionEstablished, null, null);

				httpClientConnection.SentContent.Close();
				httpClientConnection.SentContent = null;
				httpClientConnection.MessagesBeingSent.Clear();

				// process the content
				inputStream = httpWebResponse.GetResponseStream();

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.LowLevelTransport] > 0 )
				{
					GenuineChunkedStream content = null;
					if (binaryLogWriter[LogCategory.Transport] > 1)
					{
						content = new GenuineChunkedStream(false);
						GenuineUtility.CopyStreamToStream(inputStream, content, (int) httpWebResponse.ContentLength);
					}

					binaryLogWriter.WriteTransportContentEvent(LogCategory.LowLevelTransport, "HttpClientConnectionManager.Pool_Sender_OnEndSending",
						LogMessageType.AsynchronousSendingFinished, null, null, httpClientConnection.Remote, 
						binaryLogWriter[LogCategory.LowLevelTransport] > 1 ? content : null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, httpClientConnection.DbgConnectionId, 
						(int) httpWebResponse.ContentLength, null, null, null, 
						"The content of the response received by the sender connection.");

					if (binaryLogWriter[LogCategory.Transport] > 1)
						inputStream = content;
				}

				BinaryReader binaryReader = new BinaryReader(inputStream);
				string serverUri;
				int sequenceNo;
				HttpPacketType httpPacketType;
				int remoteHostUniqueIdentifier;
				HttpMessageCoder.ReadResponseHeader(binaryReader, out serverUri, out sequenceNo, out httpPacketType, out remoteHostUniqueIdentifier);

				if (sequenceNo != httpClientConnection.SendSequenceNo)
					throw GenuineExceptions.Get_Receive_IncorrectData();
				if (httpClientConnection.GenuineConnectionType == GenuineConnectionType.Persistent)
					httpClientConnection.Remote.UpdateUri(serverUri, remoteHostUniqueIdentifier);

				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.LowLevelTransport] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.LowLevelTransport, "HttpClientConnectionManager.Pool_Sender_OnEndSending",
						LogMessageType.LowLevelTransport_AsyncSendingCompleted, null, null, httpClientConnection.Remote, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, httpClientConnection.Sender_ConnectionLevelSecurity, 
						httpClientConnection.Sender_ConnectionLevelSecurity == null ? null : httpClientConnection.Sender_ConnectionLevelSecurity.Name,
						httpClientConnection.DbgConnectionId, (int) httpPacketType, sequenceNo, 0, null, null, null, null,
						"SENDER invocation completed. Type of the packet: {0}. Server Uri: {1}. Seq No: {2}",
						Enum.Format(typeof(HttpPacketType), httpPacketType, "g"), serverUri, sequenceNo);
				}

				if (httpPacketType == HttpPacketType.Desynchronization)
				{
					this.ConnectionFailed(httpClientConnection, true, GenuineExceptions.Get_Channel_Desynchronization(), false);
					return ;
				}

				if (httpPacketType == HttpPacketType.SenderError)
				{
					Exception deserializedException = null;

					try
					{
						BinaryFormatter binaryFormatter = new BinaryFormatter(new RemotingSurrogateSelector(), new StreamingContext(StreamingContextStates.Other));
						deserializedException = binaryFormatter.Deserialize(inputStream) as Exception;
					}
					catch
					{
					}

					if (deserializedException != null)
						this.ConnectionFailed(httpClientConnection, true, GenuineExceptions.Get_Receive_ConnectionClosed(deserializedException.Message), false);
					else
						this.ConnectionFailed(httpClientConnection, true, GenuineExceptions.Get_Receive_ConnectionClosed("Remote host was not able to parse the request. Probably due to the security reasons."), false);
					return ;
				}

				// fetch and process messages
				this.LowLevel_ParseLabelledStream(inputStream, httpClientConnection.Sender_ReceiveBuffer, httpClientConnection, httpClientConnection.Sender_ConnectionLevelSecurity);
			}
			finally
			{
				if (inputStream != null)
					inputStream.Close();
				httpWebResponse.Close();

				// TODO: remove this
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Debugging] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Debugging, "HttpClientConnectionManager.Pool_Sender_OnEndSending",
						LogMessageType.DebuggingSuccess, null, null, httpClientConnection.Remote, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, httpClientConnection.Sender_ConnectionLevelSecurity, 
						null,
						httpClientConnection.DbgConnectionId, -1, -1, -1, null, null, null, null,
						"HttpClientConnectionManager.Pool_Sender_OnEndSending is completed.");
				}

			}

			GenuineThreadPool.QueueUserWorkItem(this._pool_Sender_OnEndSending_ContinueExchange, httpClientConnection, false);
		}

		private WaitCallback _pool_Sender_OnEndSending_ContinueExchange;

		/// <summary>
		/// This is the second part of the Pool_Sender_OnEndSending implementation being
		/// executed in the separate thread to prevent .NET Framework internal deadlock.
		/// </summary>
		/// <param name="httpClientConnectionAsObject">The connection.</param>
		private void Pool_Sender_OnEndSending_ContinueExchange(object httpClientConnectionAsObject)
		{
			Message message = null;
			HttpClientConnection httpClientConnection = (HttpClientConnection) httpClientConnectionAsObject;

			lock (httpClientConnection.SendingLock)
			{
				// analyze the queue
				message = httpClientConnection.MessageContainer.GetMessage();
				if (message == null)
				{
					// release the lock
					httpClientConnection.IsSent = false;
					return ;
				}
			}

			Debug.Assert(httpClientConnection.IsSent == true);
			this.LowLevel_Sender_Send(message, httpClientConnection, httpClientConnection.OnEndSending, false);
		}

		/// <summary>
		/// Processes failed or closed connections.
		/// </summary>
		/// <param name="httpClientConnection">The connection.</param>
		/// <param name="sender">The type of the failed connection (P/Async).</param>
		/// <param name="exception">The exception.</param>
		/// <param name="tryToReestablish">True if it's a good idea to try to reestablish the connection.</param>
		public void ConnectionFailed(HttpClientConnection httpClientConnection, bool sender, Exception exception, bool tryToReestablish)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			try
			{
				if (tryToReestablish)
					tryToReestablish = ! ConnectionManager.IsExceptionCritical(exception as OperationException);

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpClientConnectionManager.ConnectionFailed",
						LogMessageType.ConnectionFailed, exception, null, httpClientConnection.Remote, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
						httpClientConnection.DbgConnectionId, 
						0, 0, 0, null, null, null, null,
						"Connection failure. Sender: {0}. Connection type: {1}. Try to reestablish: {2}.", 
						sender.ToString(), Enum.GetName(typeof(GenuineConnectionType), httpClientConnection.GenuineConnectionType), 
						tryToReestablish.ToString());
				}

				using (new ReaderAutoLocker(this._disposeLock))
				{
					if (this._disposed)
						tryToReestablish = false;
				}

				lock (httpClientConnection.Remote.PersistentConnectionEstablishingLock)
				{
					if (! tryToReestablish || httpClientConnection._disposed)
					{
						using (new ReaderAutoLocker(this._disposeLock))
						{
							this._persistent.Remove(httpClientConnection.Remote.Url);
						}

						// close the content
						if (httpClientConnection.SentContent != null)
						{
							httpClientConnection.SentContent.Close();
							httpClientConnection.SentContent = null;
						}

						// release all resources
						this.ITransportContext.KnownHosts.ReleaseHostResources(httpClientConnection.Remote, exception);
						foreach (Message message in httpClientConnection.MessagesBeingSent)
							this.ITransportContext.IIncomingStreamHandler.DispatchException(message, exception);
						httpClientConnection.MessagesBeingSent.Clear();
						httpClientConnection.Dispose(exception);

						httpClientConnection.SignalState(GenuineEventType.GeneralConnectionClosed, exception, null);
						if (exception is OperationException && ((OperationException) exception).OperationErrorMessage.ErrorIdentifier == "GenuineChannels.Exception.Receive.ServerHasBeenRestarted")
							httpClientConnection.SignalState(GenuineEventType.GeneralServerRestartDetected, exception, null);

						// LOG:
						if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
						{
							binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpClientConnectionManager.ConnectionFailed",
								LogMessageType.ConnectionFailed, exception, null, httpClientConnection.Remote, null,
								GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
								httpClientConnection.DbgConnectionId, 
								0, 0, 0, null, null, null, null,
								"The connection has been completely terminated and removed from all collection. Sender: {0}. Connection type: {1}. Try to reestablish: {2}.", 
								sender.ToString(), Enum.GetName(typeof(GenuineConnectionType), httpClientConnection.GenuineConnectionType), 
								tryToReestablish.ToString());
						}

						return ;
					}

					httpClientConnection.SignalState(GenuineEventType.GeneralConnectionReestablishing, exception, null);

					HttpClientConnection.ReconnectionContainer reconnectionContainer = sender ? httpClientConnection.SenderReconnectionLock : httpClientConnection.ListenerReconnectionLock;
					lock (reconnectionContainer.SyncLock)
					{
						if (GenuineUtility.IsTimeoutExpired(reconnectionContainer.ReconnectionStartedAt + GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.MaxTimeSpanToReconnect])))
						{
							// LOG:
							if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
							{
								binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpClientConnectionManager.ConnectionFailed",
									LogMessageType.ConnectionReestablishing, GenuineExceptions.Get_Debugging_GeneralWarning("Connection was not reestablished within the specified time boundaries."),
									null, httpClientConnection.Remote, null,
									GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
									httpClientConnection.DbgConnectionId, 
									0, 0, 0, null, null, null, null,
									"The connection has not been reestablished within the specified time boundaries.");
							}

							// just close the connection
							using (new ReaderAutoLocker(this._disposeLock))
							{
								this._persistent.Remove(httpClientConnection.Remote.Url);
							}

							this.ITransportContext.KnownHosts.ReleaseHostResources(httpClientConnection.Remote, exception);
							httpClientConnection.Dispose(exception);
							httpClientConnection.MessageContainer.Dispose(exception);

							// and fire a warning
							httpClientConnection.SignalState(GenuineEventType.GeneralConnectionClosed, exception, null);
							return ;
						}

						// resend the content or rerequest the previous content
						if (sender)
						{
							if (httpClientConnection.SentContent != null)
								this.LowLevel_Sender_Send(null, httpClientConnection, httpClientConnection.OnEndSending, true);
						}
						else
							this.LowLevel_InitiateListening(httpClientConnection, false);
					}
				}
			}
			catch(Exception ex)
			{
				if ( binaryLogWriter != null )
					binaryLogWriter.WriteImplementationWarningEvent("HttpClientConnectionManager.ConnectionFailed",
						LogMessageType.Error, ex, GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						"Unexpected exception occurred in the HttpClientConnectionManager.ConnectionFailed method. Most likely, something must be fixed.");
			}
		}

		/// <summary>
		/// Closes expired connections and sends ping via inactive connections.
		/// </summary>
		public void TimerCallback()
		{
			GenuineThreadPool.QueueUserWorkItem(_internal_TimerCallback, null, false);
		}
		private WaitCallback _internal_TimerCallback;

		/// <summary>
		/// Closes expired connections and sends ping via inactive connections.
		/// </summary>
		/// <param name="ignored">Ignored.</param>
		private void Internal_TimerCallback(object ignored)
		{
			int now = GenuineUtility.TickCount;
			int sendPingAfter = GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.PersistentConnectionSendPingAfterInactivity]);
			int closePersistentConnectionAfter = GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.ClosePersistentConnectionAfterInactivity]);
			int closeInvocationConnectionsAfter = GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.CloseInvocationConnectionAfterInactivity]);
			int closeOneWayConnectionsAfter = GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.CloseOneWayConnectionAfterInactivity]);
			int reconnectionFailedAfter = GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.MaxTimeSpanToReconnect]);

			// go through the pool and close all expired connections
			// persistent
			ArrayList connectionsBeingRemoved = new ArrayList();
			DictionaryEntry[] entries = null;
			lock (this._persistent.SyncRoot)
			{
				entries = new DictionaryEntry[this._persistent.Count];
				this._persistent.CopyTo(entries, 0);
			}

			foreach (DictionaryEntry dictionaryEntry in entries)
			{
				HttpClientConnection httpClientConnection = (HttpClientConnection) dictionaryEntry.Value;
				if (httpClientConnection._disposed)
				{
					connectionsBeingRemoved.Add(dictionaryEntry.Key);
					continue;
				}

				// retrieve reconnection information in a transaction
				bool isReconnectionStarted = false;
				int reconnectionStarted = 0;
				lock (httpClientConnection.ListenerReconnectionLock.SyncLock)
				{
					isReconnectionStarted |= httpClientConnection.ListenerReconnectionLock.IsReconnectionStarted;
					if (httpClientConnection.ListenerReconnectionLock.IsReconnectionStarted)
						reconnectionStarted = httpClientConnection.ListenerReconnectionLock.ReconnectionStartedAt;
				}

				lock (httpClientConnection.SenderReconnectionLock.SyncLock)
				{
					isReconnectionStarted |= httpClientConnection.SenderReconnectionLock.IsReconnectionStarted;
					if (httpClientConnection.SenderReconnectionLock.IsReconnectionStarted)
						reconnectionStarted = httpClientConnection.SenderReconnectionLock.ReconnectionStartedAt;
				}

				if (GenuineUtility.IsTimeoutExpired(httpClientConnection.LastMessageWasReceviedAt + closePersistentConnectionAfter, now))
				{
					// if connection is not being reestablished, then it's necessary to close it
					// if it's being reestablished, it comes under reestablishing rules
					if ( ! isReconnectionStarted )
						GenuineThreadPool.QueueUserWorkItem(new WaitCallback(this.PerformConnectionFailure), httpClientConnection, false);
				}

				if (GenuineUtility.IsTimeoutExpired(httpClientConnection.LastMessageWasSentAt + sendPingAfter, now))
					GenuineThreadPool.QueueUserWorkItem(new WaitCallback(this.SendPing), httpClientConnection, false);

				if ( isReconnectionStarted && GenuineUtility.IsTimeoutExpired(reconnectionStarted + reconnectionFailedAfter, now))
					GenuineThreadPool.QueueUserWorkItem(new WaitCallback(this.PerformConnectionFailure), httpClientConnection, false);
			}

			foreach (object key in connectionsBeingRemoved)
				this._persistent.Remove(key);
		}

		/// <summary>
		/// Closes the expired connection.
		/// </summary>
		/// <param name="httpClientConnectionAsObject">The connection to be closed.</param>
		private void PerformConnectionFailure(object httpClientConnectionAsObject)
		{
			this.ConnectionFailed(httpClientConnectionAsObject as HttpClientConnection, true, GenuineExceptions.Get_Channel_ConnectionClosedAfterTimeout(), false);
		}

		/// <summary>
		/// Sends a ping through the specified connection.
		/// </summary>
		/// <param name="httpClientConnectionAsObject">The connection.</param>
		private void SendPing(object httpClientConnectionAsObject)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			HttpClientConnection httpClientConnection = null;

			try
			{
				httpClientConnection = (HttpClientConnection) httpClientConnectionAsObject;

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpClientConnectionManager.SendPing",
						LogMessageType.ConnectionPingSending, null, null, httpClientConnection.Remote, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
						httpClientConnection.DbgConnectionId, 
						0, 0, 0, null, null, null, null,
						"Sending a ping.");
				}

                //Message message = new Message(this.ITransportContext, httpClientConnection.Remote, GenuineReceivingHandler.PING_MESSAGE_REPLYID, new TransportHeaders(), Stream.Null);
                Message message = new Message(this.ITransportContext, httpClientConnection.Remote, -1, new TransportHeaders(), Stream.Null);
				message.IsSynchronous = false;
				this.Send(message);
			}
			catch(Exception ex)
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpClientConnectionManager.SendPing",
						LogMessageType.ConnectionPingSending, ex, null, 
						httpClientConnection == null ? null : httpClientConnection.Remote, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
						httpClientConnection == null ? -1 : httpClientConnection.DbgConnectionId, 
						0, 0, 0, null, null, null, null,
						"Exception occurred while sending a ping.");
				}
			}
		}

		#endregion

		#region -- Connection recovering -----------------------------------------------------------

		/// <summary>
		/// Represents a failed HTTP request.
		/// </summary>
		private class FailedHttpRequest
		{
			/// <summary>
			/// The connection.
			/// </summary>
			public HttpClientConnection HttpClientConnection;

			/// <summary>
			/// The reason of failure.
			/// </summary>
			public Exception Exception;

			/// <summary>
			/// Represents a value indicating the type of the failed connection.
			/// </summary>
			public bool Sender;
		}

		/// <summary>
		/// Tries to reestablish the failed connection.
		/// </summary>
		/// <param name="failedHttpRequestAsObject">The failed request.</param>
		private void ReestablishFailedConnection(object failedHttpRequestAsObject)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			using(new ReaderAutoLocker(this._disposeLock))
			{
				if (this._disposed)
					return ;
			}

			FailedHttpRequest failedHttpRequest = (FailedHttpRequest) failedHttpRequestAsObject;
			HttpClientConnection httpClientConnection = failedHttpRequest.HttpClientConnection;
			HttpClientConnection.ReconnectionContainer reconnectionContainer = failedHttpRequest.Sender ? httpClientConnection.SenderReconnectionLock : httpClientConnection.ListenerReconnectionLock;

			// this variable is used to prevent from setting reconnectionContainer.IsReconnectionStarted to false
			// within a very short period of time
			bool closureClosed = false;

			try
			{
				Exception exception = failedHttpRequest.Exception;

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpClientConnectionManager.ReestablishFailedConnection",
						LogMessageType.ConnectionReestablishing, null, null, httpClientConnection.Remote, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
						httpClientConnection.DbgConnectionId, 
						0, 0, 0, null, null, null, null,
						"Reestablishing the {0} HTTP connection. System.Net.ServicePointManager.DefaultConnectionLimit = {1}.", 
						failedHttpRequest.Sender ? "SENDER" : "LISTENER",
						System.Net.ServicePointManager.DefaultConnectionLimit);
				}

				int reconnectionMustBeFinishedBefore = GenuineUtility.GetTimeout((TimeSpan) httpClientConnection.ITransportContext.IParameterProvider[GenuineParameter.MaxTimeSpanToReconnect]);

				for ( int i = 0; i < (int) httpClientConnection.ITransportContext.IParameterProvider[GenuineParameter.ReconnectionTries] && ! GenuineUtility.IsTimeoutExpired(reconnectionMustBeFinishedBefore) && ! httpClientConnection._disposed; i++ )
				{
					Thread.Sleep((TimeSpan) httpClientConnection.ITransportContext.IParameterProvider[GenuineParameter.SleepBetweenReconnections]);

					using(new ReaderAutoLocker(this._disposeLock))
					{
						if (this._disposed)
							return ;
					}

					lock (reconnectionContainer.SyncLock)
					{
						reconnectionContainer.RequestFailed = false;
					}

					if (httpClientConnection._disposed)
						return ;

					try
					{
						// TODO: remove this
						// LOG:
						if ( binaryLogWriter != null )
						{
							binaryLogWriter.WriteEvent(LogCategory.Debugging, "HttpClientConnectionManager.ReestablishFailedConnection",
								LogMessageType.DebuggingWarning, null, null, httpClientConnection.Remote, null,
								GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
								httpClientConnection.DbgConnectionId, 
								0, 0, 0, null, null, null, null,
								"Reestablishing the {0} attempt #{1}", failedHttpRequest.Sender ? "SENDER" : "LISTENER", i);
						}

						this.ConnectionFailed(httpClientConnection, failedHttpRequest.Sender, exception, true);

						lock (reconnectionContainer.SyncLock)
						{
							// TODO: remove this
							// LOG:
							if ( binaryLogWriter != null )
							{
								binaryLogWriter.WriteEvent(LogCategory.Debugging, "HttpClientConnectionManager.ReestablishFailedConnection",
									LogMessageType.DebuggingWarning, null, null, httpClientConnection.Remote, null,
									GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
									httpClientConnection.DbgConnectionId, 
									0, 0, 0, null, null, null, null,
									"Reestablishing the {0} attempt N{1} RESULT: {2}", 
									failedHttpRequest.Sender ? "SENDER" : "LISTENER", i, reconnectionContainer.RequestFailed ? "FAILURE" : "SUCCESS");
							}

							if (reconnectionContainer.RequestFailed)
								continue;

							closureClosed = true;
							reconnectionContainer.IsReconnectionStarted = false;
							return;
						}
					}
					catch(Exception ex)
					{
						// LOG:
						if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
						{
							binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpClientConnectionManager.ReestablishFailedConnection",
								LogMessageType.ConnectionReestablishing, ex, null, httpClientConnection.Remote, null,
								GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
								httpClientConnection.DbgConnectionId, 
								0, 0, 0, null, null, null, null,
								"Reestablishing attempt failed. Reestablishing the {0} HTTP connection will be continued. Try: {1}. Milliseconds left: {2}", 
								failedHttpRequest.Sender ? "SENDER" : "LISTENER",
								i, GenuineUtility.CompareTickCounts(reconnectionMustBeFinishedBefore, GenuineUtility.TickCount));
						}

						httpClientConnection.SignalState(GenuineEventType.GeneralConnectionReestablishing, ex, null);
					}
				}

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpClientConnectionManager.ReestablishFailedConnection",
						LogMessageType.ConnectionReestablished, GenuineExceptions.Get_Debugging_GeneralWarning("Reestablishing failed."), 
						null, httpClientConnection.Remote, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
						httpClientConnection.DbgConnectionId, 
						0, 0, 0, null, null, null, null,
						"Reestablishing the {0} HTTP connection has failed.", failedHttpRequest.Sender ? "SENDER" : "LISTENER");
				}

				// connection can not be restored
				this.ConnectionFailed(httpClientConnection, failedHttpRequest.Sender, exception, false);
			}
			finally
			{
				if (! closureClosed)
					reconnectionContainer.IsReconnectionStarted = false;
			}
		}

		#endregion

		#region -- Low-level layer -----------------------------------------------------------------

		/// <summary>
		/// Represents a value indicating the maximum time period of WebRequest initialization.
		/// </summary>
		private TimeSpan _webRequestInitiationTimeout;
		private int _httpAsynchronousRequestTimeout;

		/// <summary>
		/// Sends the message to the remote host through the sender connection.
		/// </summary>
		/// <param name="message">Message to be sent.</param>
		/// <param name="httpClientConnection">The connection.</param>
		/// <param name="asyncCallback">The callback to be called after the operation will be completed.</param>
		/// <param name="repeatSending">Whether to send the previous content again.</param>
		private void LowLevel_Sender_Send(Message message, HttpClientConnection httpClientConnection, AsyncCallback asyncCallback, bool repeatSending)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			// connection lock obtained, assemble the packet and send it
			try
			{
				if (! repeatSending)
				{
					httpClientConnection.SendSequenceNo ++;
					httpClientConnection.LastMessageWasSentAt = GenuineUtility.TickCount;

					httpClientConnection.SentContent = new GenuineChunkedStream(false);
					MessageCoder.FillInLabelledStream(message, httpClientConnection.MessageContainer, 
						httpClientConnection.MessagesBeingSent, httpClientConnection.SentContent, 
						httpClientConnection.Sender_SendBuffer, 
						(int) this.ITransportContext.IParameterProvider[GenuineParameter.HttpRecommendedPacketSize]);

					// LOG:
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Transport] > 0 )
					{
						for ( int i = 0; i < httpClientConnection.MessagesBeingSent.Count; i++)
						{
							Message nextMessage = (Message) httpClientConnection.MessagesBeingSent[i];

							binaryLogWriter.WriteEvent(LogCategory.Transport, "HttpClientConnectionManager.LowLevel_Sender_Send",
								LogMessageType.MessageIsSentAsynchronously, null, nextMessage, httpClientConnection.Remote, null,
								GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, httpClientConnection.Sender_ConnectionLevelSecurity, 
								null,
								httpClientConnection.DbgConnectionId, httpClientConnection.SendSequenceNo, 0, 0, null, null, null, null,
								"The message will be sent in the SENDER stream N: {0}.", httpClientConnection.SendSequenceNo);
						}
					}

					// apply CLSSE
					if (httpClientConnection.Sender_ConnectionLevelSecurity != null)
					{
						GenuineChunkedStream encryptedContent = new GenuineChunkedStream(false);
						httpClientConnection.Sender_ConnectionLevelSecurity.Encrypt(httpClientConnection.SentContent, encryptedContent);
						httpClientConnection.SentContent = encryptedContent;
					}

					// prepare the final version of the content
					GenuineChunkedStream resultStream = new GenuineChunkedStream(false);
					BinaryWriter binaryWriter = new BinaryWriter(resultStream);
					HttpMessageCoder.WriteRequestHeader(binaryWriter, MessageCoder.PROTOCOL_VERSION, httpClientConnection.GenuineConnectionType, httpClientConnection.HostId, HttpPacketType.Usual, httpClientConnection.SendSequenceNo, httpClientConnection.ConnectionName, httpClientConnection.Remote.LocalHostUniqueIdentifier);
					if (httpClientConnection.SentContent.CanSeek)
						resultStream.WriteStream(httpClientConnection.SentContent);
					else
						GenuineUtility.CopyStreamToStream(httpClientConnection.SentContent, resultStream);

					httpClientConnection.SentContent = resultStream;
				}
				else
				{
					httpClientConnection.SentContent.Position = 0;
				}

				// try to send it
				httpClientConnection.Sender = httpClientConnection.InitializeRequest(true, httpClientConnection._keepalive);
				httpClientConnection.Sender.ContentLength = httpClientConnection.SentContent.Length;

				this.LowLevel_InitiateHttpWebRequest(httpClientConnection.Sender, httpClientConnection.SentContent, httpClientConnection.SenderClosed, true, asyncCallback, httpClientConnection);
			}
			catch(Exception ex)
			{
				// sending failed, rollback the entire operation

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpClientConnectionManager.LowLevel_Sender_Send",
						LogMessageType.AsynchronousSendingFinished, ex, null, httpClientConnection.Remote, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
						httpClientConnection.DbgConnectionId, 0, 0, 0, null, null, null, null,
						"Error occurred while sending a message to the remote host.");
				}

				this.StartReestablishingIfNecessary(httpClientConnection, ex, true);
			}
		}

		/// <summary>
		/// Initiates the HTTP request.
		/// </summary>
		/// <param name="httpWebRequest">The instance of the HttpWebRequest class.</param>
		/// <param name="inputStream">The input stream.</param>
		/// <param name="closed">The state event.</param>
		/// <param name="sender">Indicates whether the sender request is being sent.</param>
		/// <param name="asyncCallback">The callback.</param>
		/// <param name="dbgHttpClientConnection">The connection provided for debugging purposes.</param>
		private void LowLevel_InitiateHttpWebRequest(HttpWebRequest httpWebRequest, Stream inputStream, ManualResetEvent closed, bool sender, AsyncCallback asyncCallback, HttpClientConnection dbgHttpClientConnection)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			using(new ReaderAutoLocker(this._disposeLock))
			{
				if (this._disposed)
					throw OperationException.WrapException(this._disposeReason);
			}

			Stream requestStream = null;

			try
			{
				if (! closed.WaitOne(this._webRequestInitiationTimeout, false))
					throw GenuineExceptions.Get_Processing_LogicError("[0] Deadlock or a heavy load.");

				closed.Reset();

				requestStream = httpWebRequest.GetRequestStream();

				if (binaryLogWriter != null && binaryLogWriter[LogCategory.LowLevelTransport] > 0 )
				{
					binaryLogWriter.WriteTransportContentEvent(LogCategory.LowLevelTransport, "HttpClientConnectionManager.LowLevel_InitiateHttpWebRequest",
						LogMessageType.LowLevelTransport_AsyncSendingInitiating, null, null,
						dbgHttpClientConnection == null ? null : dbgHttpClientConnection.Remote, 
						binaryLogWriter[LogCategory.LowLevelTransport] > 1 ? inputStream : null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, dbgHttpClientConnection == null ? -1 : dbgHttpClientConnection.DbgConnectionId,
						(int) inputStream.Length, null, null, null,
						"HTTP {0} Request is being sent. Size: {1}.", sender ? "SENDER" : "LISTENER", (int) inputStream.Length);
				}

				GenuineUtility.CopyStreamToStream(inputStream, requestStream, (int) inputStream.Length);
				this.IncreaseBytesSent((int) inputStream.Length);

				HttpWebRequestCop webRequestCop = new HttpWebRequestCop(httpWebRequest, asyncCallback, httpWebRequest, this._httpAsynchronousRequestTimeout);
				//httpWebRequest.BeginGetResponse(asyncCallback, httpWebRequest);
			}
			catch(Exception ex)
			{
				if (binaryLogWriter != null && binaryLogWriter[LogCategory.LowLevelTransport] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.LowLevelTransport, "HttpClientConnectionManager.LowLevel_InitiateHttpWebRequest",
						LogMessageType.LowLevelTransport_AsyncSendingInitiating, ex, null,
						dbgHttpClientConnection == null ? null : dbgHttpClientConnection.Remote, 
						null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null, 
						dbgHttpClientConnection == null ? -1 : dbgHttpClientConnection.DbgConnectionId,
						0, 0, 0, null, null, null, null,
						"Exception occurred while initiating HTTP request.");
				}

				try
				{
					httpWebRequest.Abort();
				}
				catch(Exception)
				{
				}

				throw;
			}
			finally
			{
				try
				{
					if (requestStream != null)
						requestStream.Close();
				}
				catch(Exception ex)
				{
					// ignore & warning
					if (binaryLogWriter != null && binaryLogWriter[LogCategory.LowLevelTransport] > 0 )
					{
						binaryLogWriter.WriteEvent(LogCategory.LowLevelTransport, "HttpClientConnectionManager.LowLevel_InitiateHttpWebRequest",
							LogMessageType.LowLevelTransport_AsyncSendingInitiating, ex, null,
							dbgHttpClientConnection == null ? null : dbgHttpClientConnection.Remote, 
							null,
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null, 
							dbgHttpClientConnection == null ? -1 : dbgHttpClientConnection.DbgConnectionId,
							0, 0, 0, null, null, null, null,
							"Can't close the stream!");
					}
				}

				closed.Set();
			}
		}

		/// <summary>
		/// Opens connection to the remote host.
		/// </summary>
		/// <param name="remote">The remote host.</param>
		/// <param name="genuineConnectionType">The type of the connection.</param>
		/// <param name="connectionName">The name of the connection.</param>
		/// <returns>The opened connection.</returns>
		private HttpClientConnection LowLevel_OpenConnection(HostInformation remote, GenuineConnectionType genuineConnectionType, string connectionName)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			using (new ReaderAutoLocker(this._disposeLock))
			{
				if (this._disposed)
					throw OperationException.WrapException(this._disposeReason);
			}

			// the time we should finish connection establishing before
			int timeout = GenuineUtility.GetTimeout((TimeSpan) this.ITransportContext.IParameterProvider[GenuineParameter.ConnectTimeout]);

			// LOG:
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
			{
				binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpClientConnectionManager.LowLevel_OpenConnection",
					LogMessageType.ConnectionEstablishing, null, null, remote, null,
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
					-1, 0, 0, 0, null, null, null, null,
					"The connection is being established to \"{0}\".", remote == null ? "?" : remote.Url == null ? "?" : remote.Url);
			}

			// first - send the check request and start CLSSE
			HttpClientConnection httpClientConnection = new HttpClientConnection(this.ITransportContext, connectionName);
			httpClientConnection.Remote = remote;
			httpClientConnection.GenuineConnectionType = genuineConnectionType;

			httpClientConnection.Sender_ConnectionLevelSecurity = this.CreateConnectionLevelSecuritySession(genuineConnectionType);
			httpClientConnection.Listener_ConnectionLevelSecurity = this.CreateConnectionLevelSecuritySession(genuineConnectionType);

			// LOG:
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
			{
				binaryLogWriter.WriteConnectionParameterEvent(LogCategory.Connection, "HttpClientConnectionManager.LowLevel_OpenConnection",
					LogMessageType.ConnectionParameters, null, remote, this.ITransportContext.IParameterProvider,
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, httpClientConnection.DbgConnectionId, 
					"An HTTP connection is being established.");
			}

			// gather the output stream
			Stream senderInputClsseStream = Stream.Null;
			Stream listenerInputClsseStream = Stream.Null;
			Stream clsseStream = null;
			GenuineChunkedStream outputStream = null;
			bool resend = false;
			bool firstFailure = false;

			// CLSS establishing
			for ( int i = 0; ; i++ )
			{
				bool noClsseToSend = true;

				if (! resend)
				{
					// build the content
					outputStream = new GenuineChunkedStream(false);
					BinaryWriter binaryWriter = new BinaryWriter(outputStream);
					HttpMessageCoder.WriteRequestHeader(binaryWriter, MessageCoder.PROTOCOL_VERSION, genuineConnectionType, httpClientConnection.HostId, 
						i == 0 ? HttpPacketType.Establishing_ResetConnection : HttpPacketType.Establishing, ++httpClientConnection.SendSequenceNo,
						httpClientConnection.ConnectionName, remote.LocalHostUniqueIdentifier);


					// CLSSE info
					using (new GenuineChunkedStreamSizeLabel(outputStream))
					{
						if (httpClientConnection.Sender_ConnectionLevelSecurity != null && ! httpClientConnection.Sender_ConnectionLevelSecurity.IsEstablished)
						{
							clsseStream = httpClientConnection.Sender_ConnectionLevelSecurity.EstablishSession(senderInputClsseStream, true);
							if (clsseStream != null)
							{
								noClsseToSend = false;
								GenuineUtility.CopyStreamToStream(clsseStream, outputStream);
							}
						}

					}

					// CLSSE info
					using (new GenuineChunkedStreamSizeLabel(outputStream))
					{
						if (httpClientConnection.Listener_ConnectionLevelSecurity != null && ! httpClientConnection.Listener_ConnectionLevelSecurity.IsEstablished)
						{
							clsseStream = httpClientConnection.Listener_ConnectionLevelSecurity.EstablishSession(listenerInputClsseStream, true);
							if (clsseStream != null)
							{
								noClsseToSend = false;
								GenuineUtility.CopyStreamToStream(clsseStream, outputStream);
							}
						}
					}
				}
				else
					outputStream.Position = 0;

				// parse the response
				HttpWebRequest httpWebRequest = httpClientConnection.InitializeRequest(true, httpClientConnection._keepalive);
				int millisecondsRemain = GenuineUtility.CompareTickCounts(timeout, GenuineUtility.TickCount);
				if ( millisecondsRemain <= 0)
					throw GenuineExceptions.Get_Channel_ConnectionClosedAfterTimeout();

				httpWebRequest.Timeout = millisecondsRemain;
				httpWebRequest.ContentLength = outputStream.Length;
				Stream requestStream = null;

				try
				{
					requestStream = httpWebRequest.GetRequestStream();
					GenuineUtility.CopyStreamToStream(outputStream, requestStream, (int) outputStream.Length);
					using (HttpWebResponse httpWebResponse = (HttpWebResponse) httpWebRequest.GetResponse())
					{
						// LOG:
						if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
						{
							StringBuilder stringBuilderHeaders = new StringBuilder(1024);
							foreach (string headerName in httpWebResponse.Headers.Keys)
								stringBuilderHeaders.AppendFormat("{0}=\"{1}\"; ", headerName, httpWebResponse.Headers.Get(headerName));

							binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpClientConnectionManager.LowLevel_OpenConnection",
								LogMessageType.ConnectionEstablishing, null, null, remote, null,
								GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
								httpClientConnection.DbgConnectionId, 0, 0, 0, null, null, null, null,
								"The trial connection has been successfully completed. Content-Length: {0}; Content encoding: {1}; Content type: {2}; Protocol version: {3}; Respose uri: {4}; Status code: {5}; Status description: {6}; HTTP headers: {7}.", 
								httpWebResponse.ContentLength, httpWebResponse.ContentEncoding, httpWebResponse.ContentType,
								httpWebResponse.ProtocolVersion, httpWebResponse.ResponseUri, 
								httpWebResponse.StatusCode, httpWebResponse.StatusDescription,
								stringBuilderHeaders.ToString());
						}

						using (Stream responseStream = httpWebResponse.GetResponseStream())
						{							
							BinaryReader binaryReader = new BinaryReader(responseStream);
							string serverUri;
							int sequenceNo;
							HttpPacketType httpPacketType;
							int remoteHostUniqueIdentifier;

							HttpMessageCoder.ReadResponseHeader(binaryReader, out serverUri, out sequenceNo, out httpPacketType, out remoteHostUniqueIdentifier);

							if ( httpPacketType != HttpPacketType.Establishing && httpPacketType != HttpPacketType.Establishing_ResetConnection )
								throw GenuineExceptions.Get_Connect_CanNotConnectToRemoteHost(remote.ToString(), "Wrong response received from the remote host.");

							// check the restartion if either CLSS or the persistent connection is used
							if (genuineConnectionType == GenuineConnectionType.Persistent || httpClientConnection.Sender_ConnectionLevelSecurity != null)
								remote.UpdateUri(serverUri, remoteHostUniqueIdentifier);

							// the first SS
							int writtenSize = binaryReader.ReadInt32();
							senderInputClsseStream = new GenuineChunkedStream(true);
							GenuineUtility.CopyStreamToStream(responseStream, senderInputClsseStream, writtenSize);

							writtenSize = binaryReader.ReadInt32();
							listenerInputClsseStream = new GenuineChunkedStream(true);
							GenuineUtility.CopyStreamToStream(responseStream, listenerInputClsseStream, writtenSize);
						}
					}

					outputStream.Close();
				}
				catch(Exception ex)
				{
					if (firstFailure)
						throw;

					// LOG:
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
					{
						binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpClientConnectionManager.LowLevel_OpenConnection",
							LogMessageType.ConnectionEstablishing, ex, null, remote, null,
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
							-1, 0, 0, 0, null, null, null, null,
							"Exception occurred while establishing the connection to \"{0}\". The connection establishing will be continued.", remote == null ? "?" : remote.Url == null ? "?" : remote.Url);
					}

					// try to recover
					resend = true;
					continue;
				}
				finally
				{
					if (requestStream != null)
						requestStream.Close();
				}

				firstFailure = false;
				resend = false;

				if (httpClientConnection.Sender_ConnectionLevelSecurity != null && ! httpClientConnection.Sender_ConnectionLevelSecurity.IsEstablished)
					continue;
				if (httpClientConnection.Listener_ConnectionLevelSecurity != null && ! httpClientConnection.Listener_ConnectionLevelSecurity.IsEstablished)
					continue;
				if (! noClsseToSend)
					continue;

				break;
			}

			// start listener if it's a persistent connection
			remote.PhysicalAddress = remote.Url;

			// LOG:
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.HostInformation] > 0 )
			{
				binaryLogWriter.WriteHostInformationEvent("HttpClientConnectionManager.LowLevel_OpenConnection", 
					LogMessageType.HostInformationCreated, null, remote, 
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, httpClientConnection.Sender_ConnectionLevelSecurity, 
					httpClientConnection.Sender_ConnectionLevelSecurity == null ? null : httpClientConnection.Sender_ConnectionLevelSecurity.Name, 
					httpClientConnection.DbgConnectionId, 
					"HostInformation is ready for actions.");
			}

			// LOG:
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
			{
				binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpClientConnectionManager.LowLevel_OpenConnection",
					LogMessageType.ConnectionEstablished, null, null, remote, null,
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
					httpClientConnection.Sender_ConnectionLevelSecurity, 
					httpClientConnection.Sender_ConnectionLevelSecurity == null ? null : httpClientConnection.Sender_ConnectionLevelSecurity.Name, 
					httpClientConnection.DbgConnectionId, (int) genuineConnectionType, 0, 0, this.GetType().Name, remote.Url, remote.Url, null,
					"The connection to the remote host is established.");
			}

			if (genuineConnectionType == GenuineConnectionType.Persistent)
				this.LowLevel_InitiateListening(httpClientConnection, true);
			return httpClientConnection;
		}

		/// <summary>
		/// Reads messages from the stream and processes them.
		/// </summary>
		/// <param name="stream">The source stream.</param>
		/// <param name="intermediateBuffer">The intermediate buffer.</param>
		/// <param name="httpClientConnection">The connection.</param>
		/// <param name="connectionLevelSecuritySession">The Connection Level Security Session that decrypted this message.</param>
		public void LowLevel_ParseLabelledStream(Stream stream, byte[] intermediateBuffer, HttpClientConnection httpClientConnection, SecuritySession connectionLevelSecuritySession)
		{
			bool directExecution = httpClientConnection.GenuineConnectionType == GenuineConnectionType.Invocation;

			BinaryReader binaryReader = new BinaryReader(stream);

			while ( binaryReader.ReadByte() == 0 )
			{
				using (LabelledStream labelledStream = new LabelledStream(this.ITransportContext, stream, intermediateBuffer))
				{
					if (directExecution)
						this.ITransportContext.IIncomingStreamHandler.HandleMessage(labelledStream, httpClientConnection.Remote, httpClientConnection.GenuineConnectionType, httpClientConnection.ConnectionName, httpClientConnection.DbgConnectionId, true, null, connectionLevelSecuritySession, null);
					else
					{
						GenuineChunkedStream receivedContent = new GenuineChunkedStream(true);
						GenuineUtility.CopyStreamToStream(labelledStream, receivedContent);
						this.ITransportContext.IIncomingStreamHandler.HandleMessage(receivedContent, httpClientConnection.Remote, httpClientConnection.GenuineConnectionType, httpClientConnection.ConnectionName, httpClientConnection.DbgConnectionId, false, null, connectionLevelSecuritySession, null);
					}
				}
			}
		}

		#endregion

		#region -- P/listener management -----------------------------------------------------------

		/// <summary>
		/// Initiates the listening request.
		/// </summary>
		/// <param name="httpClientConnection">The connection.</param>
		/// <param name="requestNextPacket">Whether to request the next packet.</param>
		private void LowLevel_InitiateListening(HttpClientConnection httpClientConnection, bool requestNextPacket)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			try
			{
#if DEBUG
				Debug.Assert(httpClientConnection.GenuineConnectionType == GenuineConnectionType.Persistent);
#endif

				if (requestNextPacket)
					httpClientConnection.ListenerSequenceNo ++;

				// write the request header
				using (GenuineChunkedStream listenerStream = new GenuineChunkedStream(false))
				{
					BinaryWriter binaryWriter = new BinaryWriter(listenerStream);
					HttpMessageCoder.WriteRequestHeader(binaryWriter, MessageCoder.PROTOCOL_VERSION, httpClientConnection.GenuineConnectionType, httpClientConnection.HostId, HttpPacketType.Listening, httpClientConnection.ListenerSequenceNo, httpClientConnection.ConnectionName, httpClientConnection.Remote.LocalHostUniqueIdentifier);

					// start the request
					httpClientConnection.Listener = httpClientConnection.InitializeRequest(false, httpClientConnection._keepalive);
					httpClientConnection.Listener.ContentLength = listenerStream.Length;

					this.LowLevel_InitiateHttpWebRequest(httpClientConnection.Listener, listenerStream, httpClientConnection.ListenerClosed, false, httpClientConnection.OnEndReceiving, httpClientConnection);
				}
			}
			catch(Exception ex)
			{
				// It's necessary to schedule connection reestablishing

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpClientConnectionManager.LowLevel_InitiateListening",
						LogMessageType.ConnectionFailed, ex, null, httpClientConnection.Remote, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
						httpClientConnection.DbgConnectionId, 0, 0, 0, null, null, null, null,
						"The LISTENER request Seq No={0} has failed. The connection will be reestablished, if possible.", httpClientConnection.ListenerSequenceNo);
				}

				this.StartReestablishingIfNecessary(httpClientConnection, ex, false);
			}
		}

		/// <summary>
		/// Finishes sending a message through the connection.
		/// </summary>
		/// <param name="httpClientConnection">The connection.</param>
		/// <param name="httpWebResponse">The received response.</param>
		public void Listener_OnEndReceiving(HttpClientConnection httpClientConnection, HttpWebResponse httpWebResponse)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			Stream inputStream = null;

			try
			{
#if DEBUG
				Debug.Assert(httpClientConnection.GenuineConnectionType == GenuineConnectionType.Persistent);
#endif

				httpClientConnection.Remote.Renew(this._hostRenewingSpan, false);
				httpClientConnection.LastMessageWasReceviedAt = GenuineUtility.TickCount;
				httpClientConnection.SignalState(GenuineEventType.GeneralConnectionEstablished, null, null);

				// process the content
				inputStream = httpWebResponse.GetResponseStream();

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.LowLevelTransport] > 0 )
				{
					bool writeContent = binaryLogWriter[LogCategory.LowLevelTransport] > 1;
					GenuineChunkedStream content = null;
					if (writeContent)
					{
						content = new GenuineChunkedStream(false);
						GenuineUtility.CopyStreamToStream(inputStream, content, (int) httpWebResponse.ContentLength);
					}

					binaryLogWriter.WriteTransportContentEvent(LogCategory.LowLevelTransport, "HttpClientConnectionManager.Listener_OnEndReceiving",
						LogMessageType.LowLevelTransport_AsyncReceivingCompleted, null, null, httpClientConnection.Remote, 
						writeContent ? content : null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
						httpClientConnection.DbgConnectionId, (int) httpWebResponse.ContentLength,
						null, null, null,
						"The content has been received.");

					if (writeContent)
						inputStream = content;
				}

				if (httpClientConnection.Listener_ConnectionLevelSecurity != null)
					inputStream = httpClientConnection.Listener_ConnectionLevelSecurity.Decrypt(inputStream);

				BinaryReader binaryReader = new BinaryReader(inputStream);
				string serverUri;
				int sequenceNo;
				HttpPacketType httpPacketType;
				int remoteHostUniqueIdentifier;
				HttpMessageCoder.ReadResponseHeader(binaryReader, out serverUri, out sequenceNo, out httpPacketType, out remoteHostUniqueIdentifier);

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpClientConnectionManager.Listener_OnEndReceiving",
						LogMessageType.ReceivingFinished, null, null, httpClientConnection.Remote, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, httpClientConnection.Listener_ConnectionLevelSecurity, null,
						httpClientConnection.DbgConnectionId, 0, 0, 0, null, null, null, null,
						"A response to the listener request has been received. Server uri: {0}. Sequence no: {1}. Packet type: {2}. Content-encoding: {3}. Content-length: {4}. Protocol version: {5}. Response uri: \"{6}\". Server: \"{7}\". Status code: {8}. Status description: \"{9}\".",
						serverUri, sequenceNo, Enum.Format(typeof(HttpPacketType), httpPacketType, "g"), 
						httpWebResponse.ContentEncoding, httpWebResponse.ContentLength, 
						httpWebResponse.ProtocolVersion, httpWebResponse.ResponseUri, 
						httpWebResponse.Server, httpWebResponse.StatusCode, httpWebResponse.StatusDescription);
				}

				if (httpPacketType == HttpPacketType.Desynchronization)
					throw GenuineExceptions.Get_Channel_Desynchronization();

				if (sequenceNo != httpClientConnection.ListenerSequenceNo)
					throw GenuineExceptions.Get_Processing_LogicError(
						string.Format("Impossible situation. The request stream number: {0}. The received stream number: {1}.", httpClientConnection.ListenerSequenceNo, sequenceNo)
						);

				if (httpClientConnection.GenuineConnectionType == GenuineConnectionType.Persistent)
					httpClientConnection.Remote.UpdateUri(serverUri, remoteHostUniqueIdentifier);

				// if the remote host has asked to terminate a connection
				if (httpPacketType == HttpPacketType.ClosedManually || httpPacketType == HttpPacketType.Desynchronization)
					throw GenuineExceptions.Get_Receive_ConnectionClosed();

				// fetch and process messages
				if (httpPacketType != HttpPacketType.ListenerTimedOut)
					this.LowLevel_ParseLabelledStream(inputStream, httpClientConnection.Listener_ReceiveBuffer, httpClientConnection, httpClientConnection.Listener_ConnectionLevelSecurity);

				GenuineUtility.CopyStreamToStream(inputStream, Stream.Null);
			}
			catch(Exception ex)
			{
				this.ConnectionFailed(httpClientConnection, false, ex, true);
				return ;
			}
			finally
			{
				if (inputStream != null)
					inputStream.Close();
				httpWebResponse.Close();
			}

			GenuineThreadPool.QueueUserWorkItem(new WaitCallback(this.Listener_OnEndReceiving_ContinueExchange), httpClientConnection, false);
		}

		private WaitCallback _listener_OnEndReceiving_ContinueExchange;

		/// <summary>
		/// The second part of the Listener_OnEndReceiving member implementation which is executed
		/// in the separate thread to avoid internal .NET Framework deadlock.
		/// </summary>
		/// <param name="httpClientConnectionAsObject">The connection represented as a reference to an instance of the Object class.</param>
		private void Listener_OnEndReceiving_ContinueExchange(object httpClientConnectionAsObject)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			HttpClientConnection httpClientConnection = (HttpClientConnection) httpClientConnectionAsObject;

			this.LowLevel_InitiateListening(httpClientConnection, true);
		}

		#endregion

		#region -- Listening -----------------------------------------------------------------------

		/// <summary>
		/// Starts listening to the specified end point and accepting incoming connections.
		/// </summary>
		/// <param name="endPoint">The end point.</param>
		public override void StartListening(object endPoint)
		{
			throw new NotSupportedException();
		}

		/// <summary>
		/// Stops listening to the specified end point. Does not close any connections.
		/// </summary>
		/// <param name="endPoint">The end point</param>
		public override void StopListening(object endPoint)
		{
			throw new NotSupportedException();
		}

		#endregion

		#region -- Resource releasing --------------------------------------------------------------

		/// <summary>
		/// Closes the specified connections to the remote host and releases acquired resources.
		/// </summary>
		/// <param name="hostInformation">Host information.</param>
		/// <param name="genuineConnectionType">What kind of connections will be affected by this operation.</param>
		/// <param name="reason">Reason of resource releasing.</param>
		public override void ReleaseConnections(HostInformation hostInformation, GenuineConnectionType genuineConnectionType, Exception reason)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			HttpClientConnection httpClientConnection = null;
			ArrayList connectionsToClose = new ArrayList();

			// LOG:
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
			{
				binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpClientConnectionManager.ReleaseConnections",
					LogMessageType.ReleaseConnections, reason, null, hostInformation, null, 
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
					null, null, -1, 0, 0, 0, Enum.Format(typeof(GenuineConnectionType), genuineConnectionType, "g"), null, null, null,
					"\"{0}\" connections will be terminated.", Enum.Format(typeof(GenuineConnectionType), genuineConnectionType, "g"), null);
			}

			// TODO: 2.5.3. fix (deadlock by design), will be completely fixed in 3.0
//			using (new WriterAutoLocker(this._disposeLock))
			{
				// persistent
				if ( (genuineConnectionType & GenuineConnectionType.Persistent) != 0 )
					lock (this._persistent.SyncRoot)
					{
						foreach (DictionaryEntry entry in this._persistent)
						{
							httpClientConnection = (HttpClientConnection) entry.Value;
							if (hostInformation != null && httpClientConnection.Remote != hostInformation)
								continue;

							connectionsToClose.Add(httpClientConnection);
						}
					}

				// close connections
				foreach (HttpClientConnection nextHttpClientConnection in connectionsToClose)
				{
					// LOG:
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
					{
						binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpClientConnectionManager.ReleaseConnections",
							LogMessageType.ConnectionShuttingDown, reason, null, nextHttpClientConnection.Remote, null,
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
							nextHttpClientConnection.DbgConnectionId, 0, 0, 0, null, null, null, null,
							"The connection is being shut down manually.");
					}

					this.ConnectionFailed(nextHttpClientConnection, true, GenuineExceptions.Get_Channel_ConnectionShutDown(reason), false);

					if (nextHttpClientConnection.GenuineConnectionType == GenuineConnectionType.Persistent)
					{
						nextHttpClientConnection.SignalState(GenuineEventType.GeneralConnectionClosed, reason, null);
					}
				}
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
			lock (this._persistent.SyncRoot)
			{
				if (this._persistent.ContainsKey(uri))
				{
					string[] result = new string[1];
					result[0] = uri;
					return result;
				}
			}

			return null;
		}

		/// <summary>
		/// Releases all resources.
		/// </summary>
		/// <param name="reason">The reason of disposing.</param>
		public override void InternalDispose(Exception reason)
		{
			this.ReleaseConnections(null, GenuineConnectionType.All, reason);
		}

		#endregion

		#region -- Reestablishing ------------------------------------------------------------------

		/// <summary>
		/// Starts reestablishing if it has not been started.
		/// </summary>
		/// <param name="httpClientConnection">The HTTP client connection.</param>
		/// <param name="exception">The reason of the connection failure.</param>
		/// <param name="sender">A flag indicating the role of the connection.</param>
		public void StartReestablishingIfNecessary(HttpClientConnection httpClientConnection, Exception exception, bool sender)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			HttpClientConnection.ReconnectionContainer reconnectionContainer = sender ? httpClientConnection.SenderReconnectionLock : httpClientConnection.ListenerReconnectionLock;
			lock (reconnectionContainer.SyncLock)
			{
				if (! reconnectionContainer.IsReconnectionStarted)
				{
					// LOG:
					if ( binaryLogWriter != null )
					{
						binaryLogWriter.WriteEvent(LogCategory.Debugging, "HttpClientConnectionManager.StartReestablishingIfNecessary",
							LogMessageType.ConnectionReestablishing, exception, null, null, null, 
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
							null, null, -1, 0, 0, 0, null, null, null, null,
							"Reconnection is being started.");
					}

					reconnectionContainer.IsReconnectionStarted = true;
					reconnectionContainer.ReconnectionStartedAt = GenuineUtility.TickCount;

					FailedHttpRequest failedHttpRequest = new FailedHttpRequest();
					failedHttpRequest.HttpClientConnection = httpClientConnection;
					failedHttpRequest.Exception = exception;
					failedHttpRequest.Sender = sender;
					GenuineThreadPool.QueueUserWorkItem(new WaitCallback(this.ReestablishFailedConnection), failedHttpRequest, true);
				}
				else
				{
					// LOG:
					if ( binaryLogWriter != null )
					{
						binaryLogWriter.WriteEvent(LogCategory.Debugging, "HttpClientConnectionManager.StartReestablishingIfNecessary",
							LogMessageType.ConnectionReestablishing, exception, null, null, null, 
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
							null, null, -1, 0, 0, 0, null, null, null, null,
							"Reconnection is already in progress.");
					}

					reconnectionContainer.RequestFailed = true;
				}
			}
		}

		/// <summary>
		/// Resets the reestablishing flag.
		/// </summary>
		/// <param name="httpClientConnection">The HTTP client connection.</param>
		/// <param name="sender">A flag indicating the role of the connection.</param>
		public void ReestablishingCompleted(HttpClientConnection httpClientConnection, bool sender)
		{
			HttpClientConnection.ReconnectionContainer reconnectionContainer = sender ? httpClientConnection.SenderReconnectionLock : httpClientConnection.ListenerReconnectionLock;
			reconnectionContainer.IsReconnectionStarted = false;
		}

		#endregion
	}
}
