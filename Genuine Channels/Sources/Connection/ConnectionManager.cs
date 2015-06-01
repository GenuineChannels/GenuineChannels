/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Runtime.Remoting.Channels;

using Belikov.Common.ThreadProcessing;
using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Parameters;
using Belikov.GenuineChannels.Security;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Utilities;

namespace Belikov.GenuineChannels.Connection
{
	/// <summary>
	/// Manages a set of connections being used for performing exchange with the remote host
	/// via abstract transport.
	/// </summary>
	public abstract class ConnectionManager : MarshalByRefObject
	{
		/// <summary>
		/// Constructs an instance of the ConnectionManager class.
		/// </summary>
		/// <param name="iTransportContext">The Transport context.</param>
		public ConnectionManager(ITransportContext iTransportContext)
		{
			this.ITransportContext = iTransportContext;
		}

		/// <summary>
		/// The Transport context.
		/// </summary>
		public ITransportContext ITransportContext;

		/// <summary>
		/// Contains information about the local host.
		/// </summary>
		public HostInformation Local;

		/// <summary>
		/// Sends a message to the remote host.
		/// </summary>
		/// <param name="message">The message to be sent.</param>
		public void Send(Message message)
		{
#if TRIAL
			_messagesBeingSent[message.MessageId] = message;
#endif

			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			try
			{
				using (new ReaderAutoLocker(this._disposeLock))
				{
					if (this._disposed)
						throw OperationException.WrapException(this._disposeReason);
				}

				SecuritySession session = null;

				if (this._disposed)
					throw OperationException.WrapException(this._disposeReason);

				// get the security session descriptor
				if (message.SecuritySessionParameters == null)
				{
					SecuritySessionParameters securitySessionParameters = SecuritySessionServices.GetCurrentSecurityContext();
					if (securitySessionParameters == null)
						securitySessionParameters = message.Recipient.SecuritySessionParameters;
					if (securitySessionParameters == null)
						securitySessionParameters = this.ITransportContext.SecuritySessionParameters;
					if (securitySessionParameters == null)
						securitySessionParameters = SecuritySessionServices.DefaultSecuritySession;
					message.SecuritySessionParameters = securitySessionParameters;
				}

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
					binaryLogWriter.WriteSecuritySessionParametersEvent("ConnectionManager.Send", 
						LogMessageType.SecuritySessionParametersAssembled, null, message, message.Recipient, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						message.SecuritySessionParameters,
						"Security Session Parameters have been assembled.");

				// determine the type of sending
				message.IsSynchronous = (message.SecuritySessionParameters.Attributes & SecuritySessionAttributes.ForceSync) != 0 ||
					(message.IsSynchronous && (message.SecuritySessionParameters.Attributes & SecuritySessionAttributes.ForceAsync) == 0);

				// the time until invocation times out
				if (! message.FinishTime_Initialized)
				{
					TimeSpan messageTimeout = message.SecuritySessionParameters.Timeout;
					if (messageTimeout == TimeSpan.MinValue)
						messageTimeout = (TimeSpan) this.ITransportContext.IParameterProvider[GenuineParameter.InvocationTimeout];
					message.FinishTime = GenuineUtility.GetTimeout(messageTimeout);

					message.FinishTime_Initialized = true;
				}

				// checks whether the message has been already processed by Security Session
				if (message.SerializedContent == null)
				{
					session = message.Recipient.GetSecuritySession(message.SecuritySessionParameters.Name, this.ITransportContext.IKeyStore);
					if (! session.IsEstablished)
					{
						if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Security] > 0 )
							binaryLogWriter.WriteEvent(LogCategory.Security, "ConnectionManager.Send",
								LogMessageType.SecuritySessionHasNotBeenEstablishedYet, null, message, message.Recipient, null, 
								GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
								session, session.Name, -1, 0, 0, 0, null, null, null, null,
								"The requested Security Session is not established.");

						session.InitiateEstablishingSecuritySession(message.SecuritySessionParameters);

						// if it's a sync sending, then wait until security session will be established
						if ( message.IsSynchronous )
						{
							int timeSpanToWait = GenuineUtility.GetMillisecondsLeft(message.FinishTime);
							if (timeSpanToWait <= 0)
								throw GenuineExceptions.Get_Send_ServerDidNotReply();

							// wait until Security Session will be established or a failure will be detected
							int firedEvent = 0;
							if (message.CancelSending != null)
								firedEvent = WaitHandle.WaitAny(new WaitHandle[] { session.IsEstablishedEvent, session.Failed, message.CancelSending }, timeSpanToWait, false);
							else
								firedEvent = WaitHandle.WaitAny(new WaitHandle[] { session.IsEstablishedEvent, session.Failed }, timeSpanToWait, false);

							if (firedEvent == WaitHandle.WaitTimeout)
								throw GenuineExceptions.Get_Send_ServerDidNotReply();

							// analyze the problem, if any
							Exception exception = session.ReasonOfFailure;
							if (firedEvent == 1)
							{
								if (exception != null)
									throw OperationException.WrapException(exception);
								else
									throw GenuineExceptions.Get_Security_ContextWasNotEstablished(session.Name);
							}

							// if the message has been cancelled, let the sender to understand the reason
							if (firedEvent == 2)
								return ;
						}
						else if (! session.IsEstablished)
						{
							// it's async and SS still isn't established
							session.PutMessageToAwaitingQueue(message);
							return;
						}
					}
				}

				// if serialization is necessary
				if (message.SerializedContent == null)
				{
					// serialize the message
					GenuineChunkedStream serializedMessageStream = new GenuineChunkedStream(false);
					MessageCoder.Serialize(serializedMessageStream, message, (message.SecuritySessionParameters.Attributes & SecuritySessionAttributes.EnableCompression) != 0);

					// save the name of the Security Session
					GenuineChunkedStream resultStream = new GenuineChunkedStream(false);
					BinaryWriter writer = new BinaryWriter(resultStream);
					writer.Write(message.SecuritySessionParameters.Name);
					session.Encrypt(serializedMessageStream, resultStream);
					message.SerializedContent = resultStream;

					// LOG: put down the log record
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Security] > 0 )
					{
						binaryLogWriter.WriteEvent(LogCategory.Security, "ConnectionManager.Send",
							LogMessageType.SecuritySessionApplied, null, message, message.Recipient, 
							binaryLogWriter[LogCategory.Security] > 1 ? message.SerializedContent : null, 
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, session,
							session.Name, -1, 0, 0, 0, null, null, null, null,
							"The message has been processed by the established Security Session.");
					}
				}

#if TRIAL
				if (message.MessageId > 3005)
					throw GenuineExceptions.Get_Channel_TrialConditionExceeded("The maximum number of messages restriction has been exceeded. You can not send more than 3000 messages using TRIAL version.");
#endif

				message.Sender = this.Local;
				this.InternalSend(message);
			}
			catch(Exception ex)
			{
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Security] > 0 )
					binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "ConnectionManager.Send",
						LogMessageType.Error, ex, message, message.Recipient, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, -1, 0, 0, 0, null, null, null, null,
						"An exception occurred while processing the message.");

				throw;
			}
		}

#if TRIAL

		/// <summary>
		/// Well, right now it's hard to make Genuine Channels have memory leak :))
		/// </summary>
		public static Hashtable _messagesBeingSent = Hashtable.Synchronized(new Hashtable());

#endif

		/// <summary>
		/// Sends the message to the remote host. Services messages with serialized content only,
		/// after Security Session completes its job.
		/// </summary>
		/// <param name="message">The message.</param>
		protected abstract void InternalSend(Message message);

		#region -- Connection Management -----------------------------------------------------------

		/// <summary>
		/// Starts listening to the specified end point and accepting incoming connections.
		/// </summary>
		/// <param name="endPoint">The end point.</param>
		public abstract void StartListening(object endPoint);

		/// <summary>
		/// Stops listening to the specified end point. Does not close any established connections.
		/// </summary>
		/// <param name="endPoint">The end point</param>
		public abstract void StopListening(object endPoint);

		/// <summary>
		/// Closes all connections that fit to the specified characteristics.
		/// Automatically releases all resources acquired by connections being closed.
		/// </summary>
		/// <param name="hostInformation">The host or a null reference to embrace all remote hosts.</param>
		/// <param name="genuineConnectionType">Specifies what connection patterns will be affected by this operation.</param>
		/// <param name="reason">The reason of resource releasing that will be dispatched to all callers waiting something from the connections being closed.</param>
		public abstract void ReleaseConnections(HostInformation hostInformation, GenuineConnectionType genuineConnectionType, Exception reason);

		/// <summary>
		/// Returns the names of connections opened to the specified destination.
		/// </summary>
		/// <param name="uri">The URI or URL of the remote host.</param>
		/// <returns>Names of connections opened to the specified destination.</returns>
		public virtual string[] GetConnectionNames(string uri)
		{
			return null;
		}

		#endregion

		#region -- Disposing -----------------------------------------------------------------------

		/// <summary>
		/// Gets or sets a value indicating whether this instance was disposed.
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
		/// The reason of the Connection Manager disposing.
		/// </summary>
		internal Exception _disposeReason = null;
		
		/// <summary>
		/// The dispose lock.
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

			this.InternalDispose(reason);
		}

		/// <summary>
		/// Releases all resources.
		/// </summary>
		/// <param name="reason">The reason of disposing.</param>
		public abstract void InternalDispose(Exception reason);

		#endregion

		#region -- Traffic counters ----------------------------------------------------------------

		/// <summary>
		/// Gets a value indicating how many bytes have been sent to the remote host.
		/// </summary>
		public long BytesSent
		{
			get
			{
				lock(_totalBytesSentLock)
				{
					return _totalBytesSent;
				}
			}
		}
		private long _totalBytesSent = 0;
		private object _totalBytesSentLock = new object();

		/// <summary>
		/// Increases the bytes sent counter.
		/// </summary>
		/// <param name="bytesSent">Number of bytes sent.</param>
		internal void IncreaseBytesSent(int bytesSent)
		{
			lock(_totalBytesSentLock)
			{
				_totalBytesSent += bytesSent;
			}
		}


		/// <summary>
		/// Gets a value indicating how many bytes have been received from the remote host.
		/// </summary>
		public long BytesReceived
		{
			get
			{
				lock(_totalBytesReceivedLock)
				{
					return _totalBytesReceived;
				}
			}
		}
		private long _totalBytesReceived = 0;
		private object _totalBytesReceivedLock = new object();

		/// <summary>
		/// Increases the bytes received counter.
		/// </summary>
		/// <param name="bytesReceived"></param>
		internal void IncreaseBytesReceived(int bytesReceived)
		{
			lock(_totalBytesReceivedLock)
			{
				_totalBytesReceived += bytesReceived;
			}
		}


		#endregion

		#region -- Different common things ---------------------------------------------------------

		/// <summary>
		/// Determines whether the exception is critical and the connection is subject to be destroyed.
		/// </summary>
		/// <param name="operationException">The instance of the OperationException class or a null reference.</param>
		/// <returns>True if the exception is critical and the connection is subject to be destroyed.</returns>
		internal static bool IsExceptionCritical(OperationException operationException)
		{
			if (operationException == null)
				return false;

			return operationException is GenuineExceptions.QueueIsOverloaded || 
				operationException is GenuineExceptions.ChannelClosed ||
				operationException is GenuineExceptions.ServerHasBeenRestarted ||
				operationException is GenuineExceptions.ClientDidNotReconnectWithinTimeOut ||
				operationException is GenuineExceptions.ConnectionClosed ||
				operationException is GenuineExceptions.ChannelDesynchronization ||
				operationException is GenuineExceptions.ConnectionShutDown ||
				operationException is GenuineExceptions.ConnectionClosedAfterTimeout;
		}

		/// <summary>
		/// Creates and returns the Connection Level Security Session.
		/// </summary>
		/// <param name="genuineConnectionType">The type of the connection.</param>
		/// <returns>The created Security Session.</returns>
		internal SecuritySession CreateConnectionLevelSecuritySession(GenuineConnectionType genuineConnectionType)
		{
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

			// create and return the Security Session
			if (connectionLevelSSName != null)
				return this.ITransportContext.IKeyStore.GetKey(connectionLevelSSName).CreateSecuritySession(connectionLevelSSName, null);

			return null;
		}

		/// <summary>
		/// Releases information belonging to the failed connection.
		/// </summary>
		/// <param name="hostInformation">The reason of releasing.</param>
		/// <param name="exception">The reason of the resource releasing.</param>
		internal void ReleaseHostResources(HostInformation hostInformation, Exception exception)
		{
			this.ITransportContext.IGenuineEventProvider.Fire(new GenuineEventArgs(GenuineEventType.GeneralServerRestartDetected, exception, hostInformation, null));
		}

		/// <summary>
		/// Spares a connection name, which is unique within the current appdomain.
		/// </summary>
		/// <returns>A connection name that is unique within the current appdomain.</returns>
		internal string GetUniqueConnectionName()
		{
			return Interlocked.Increment(ref _ConnectionCounter).ToString();
		}

		private static int _ConnectionCounter = 0;
		private static int _ConnectionIdentifierCounter = 0;

		/// <summary>
		/// Gets a unique connection identifier, which is used for debugging purposes only.
		/// </summary>
		/// <returns>The unique connection identifier, which is used for debugging purposes only.</returns>
		internal static int GetUniqueConnectionId()
		{
			return Interlocked.Increment(ref _ConnectionIdentifierCounter);
		}

		#endregion

	}
}
