/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.IO;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Messaging;
using System.Threading;

using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Parameters;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Utilities;

namespace Belikov.GenuineChannels.Security
{
	/// <summary>
	/// Represents a security context servicing one connection or a set of invocations being made 
	/// in the context requiring this Security Session.
	/// In general, it can provide cryptographic, impersonation, authentication security capabilities.
	/// </summary>
	public abstract class SecuritySession : MarshalByRefObject
	{
		/// <summary>
		/// Initializes an instance of the SecuritySession class.
		/// </summary>
		/// <param name="name">The name of the Security Session.</param>
		/// <param name="remote">Information about remote host.</param>
		public SecuritySession(string name, HostInformation remote)
		{
			this._name = name;
			this.Remote = remote;

			// LOG:
			BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;
			if (binaryLogWriter != null && binaryLogWriter[LogCategory.Security] > 0 )
				binaryLogWriter.WriteEvent(LogCategory.Security, "SecuritySession.SecuritySession",
					LogMessageType.SecuritySessionCreated, null, null, remote, null, 
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, this,
					name, -1, 
					0, 0, 0, this.GetType().Name, null, null, null,
					"Security Session has been created.");
		}

		/// <summary>
		/// To guarantee atomic access to local members.
		/// </summary>
		private object _accessToLocalMembers = new object();

		/// <summary>
		/// Contains information about the remote host.
		/// </summary>
		public readonly HostInformation Remote;

		/// <summary>
		/// Gets the name of this Security Session.
		/// </summary>
		public string Name
		{
			get
			{
				return this._name;
			}
		}
		private string _name;

		/// <summary>
		/// The identifier of the current session. Is used only for debugging purposes and outputting diagnostic messages.
		/// </summary>
		public int SecuritySessionId
		{
			get
			{
				return this._securitySessionId;
			}
		}
		private int _securitySessionId = Interlocked.Increment(ref _securitySessionMaxId);
		private static int _securitySessionMaxId = 0;

		#region -- Establishing --------------------------------------------------------------------

		/// <summary>
		/// Gets an indication whether this Security Session is established.
		/// </summary>
		public bool IsEstablished
		{
			get
			{
				return _isEstablished.WaitOne(0, false);
			}
		}

		/// <summary>
		/// Gets an indication whether the Security Session fails during establishing.
		/// </summary>
		public ManualResetEvent Failed
		{
			get
			{
				return this._failed;
			}
		}
		private ManualResetEvent _failed = new ManualResetEvent(false);

		/// <summary>
		/// Gets or sets the reason of failure.
		/// </summary>
		public Exception ReasonOfFailure
		{
			get
			{
				lock (this._accessToLocalMembers)
					return this._reasonOfFailure;
			}
			set
			{
				lock (this._accessToLocalMembers)
					this._reasonOfFailure = value;
			}
		}
		private Exception _reasonOfFailure;

		/// <summary>
		/// Is set if security session is established.
		/// </summary>
		public ManualResetEvent IsEstablishedEvent
		{
			get
			{
				return this._isEstablished;
			}
		}

		/// <summary>
		/// Is set if security session is established.
		/// </summary>
		protected ManualResetEvent _isEstablished = new ManualResetEvent(false);

		/// <summary>
		/// The Security Session parameters being forced for the messages containing internal 
		/// Security Session data.
		/// </summary>
		protected SecuritySessionParameters _establishingSecuritySessionParameters;

		/// <summary>
		/// Informs all dependent entities that the Security Session has been established.
		/// </summary>
		protected virtual void SessionEstablished()
		{
			BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;

			// LOG:
			if (binaryLogWriter != null && binaryLogWriter[LogCategory.Security] > 0 )
				binaryLogWriter.WriteEvent(LogCategory.Security, "SecuritySession.SessionEstablished",
					LogMessageType.SecuritySessionEstablished, null, null, this.Remote, null, 
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, this,
					this.Name, -1, 0, 0, 0, null, null, null, null,
					"Security Session has been established.");

			this._isEstablished.Set();
			SendAssociatedMessages();
		}

		/// <summary>
		/// Initiates or continues establishing of the Security Session.
		/// </summary>
		/// <param name="input">A null reference or an incoming stream.</param>
		/// <param name="connectionLevel">Indicates whether the Security Session operates on connection level.</param>
		/// <returns>A stream containing data for sending to the remote host or a null reference if Security Session is established.</returns>
		public abstract GenuineChunkedStream EstablishSession(Stream input, bool connectionLevel);

		/// <summary>
		/// Initiates establishing Security Session with specific remote host via specified IConnectionManager.
		/// </summary>
		/// <param name="securitySessionParameters">Security Session parameters.</param>
		public void InitiateEstablishingSecuritySession(SecuritySessionParameters securitySessionParameters)
		{
			BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;
			if (binaryLogWriter != null && binaryLogWriter[LogCategory.Security] > 0 )
				binaryLogWriter.WriteEvent(LogCategory.Security, "SecuritySession.InitiateEstablishingSecuritySession",
					LogMessageType.SecuritySessionInitiated, null, null, this.Remote, null, 
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, this,
					this.Name, -1, 0, 0, 0, this.GetType().Name, this.Name, null, null,
					"Security Session establishing is initiated.");

			this.Failed.Reset();
			this._establishingSecuritySessionParameters = securitySessionParameters;
			GenuineThreadPool.QueueUserWorkItem(new WaitCallback(this.Internal_InitiateEstablishingSecuritySession), securitySessionParameters, false);
		}

		/// <summary>
		/// Processes incoming Security Session Establishing message and sent a response if it's available.
		/// </summary>
		/// <param name="ignored">Ignored.</param>
		private void Internal_InitiateEstablishingSecuritySession(object ignored)
		{
			try
			{
				// get the next packet
				if (this.IsEstablished)
					return ;

				GenuineChunkedStream outputStream = this.EstablishSession(Stream.Null, false);

				if (outputStream != null)
					this.SendMessage(outputStream);
				return ;
			}
			catch(Exception ex)
			{
				this.DispatchException(ex);

#if DEBUG
//				this.Remote.ITransportContext.IEventLogger.Log(LogMessageCategory.Security, null, "SecuritySession.Internal_InitiateEstablishingSecuritySession",
//					null, "Exception during establishing Security Session. Exception: {0}. Stack trace: {1}.", ex.Message, ex.StackTrace);
#endif

				this.Remote.ITransportContext.IGenuineEventProvider.Fire(new GenuineEventArgs(GenuineEventType.SecuritySessionFailed, ex, this.Remote, this));
			}
		}

		/// <summary>
		/// Wraps the specified stream into a message and sends it to the remote host.
		/// </summary>
		/// <param name="streamAsObject">The stream to be sent to the remote host.</param>
		protected void SendMessage(object streamAsObject)
		{
			// send output stream
			Message message = new Message(this.Remote.ITransportContext, this.Remote, 0, new TransportHeaders(), Stream.Null);
			message.SecuritySessionParameters = this._establishingSecuritySessionParameters;
			message.IsSynchronous = (bool) this.Remote.ITransportContext.IParameterProvider[GenuineParameter.HoldThreadDuringSecuritySessionEstablishing];
			message.SerializedContent = (Stream) streamAsObject;
			this.Remote.ITransportContext.ConnectionManager.Send(message);
		}

		#endregion

		#region -- Cryptography --------------------------------------------------------------------

		/// <summary>
		/// Encrypts the message data and put a result into the specified output stream.
		/// </summary>
		/// <param name="input">The stream containing the serialized message.</param>
		/// <param name="output">The result stream with the data being sent to the remote host.</param>
		public abstract void Encrypt(Stream input, GenuineChunkedStream output);

		/// <summary>
		/// Creates and returns a stream containing decrypted data.
		/// </summary>
		/// <param name="input">A stream containing encrypted data.</param>
		/// <returns>A stream with decrypted data.</returns>
		public abstract Stream Decrypt(Stream input);

		/// <summary>
		/// Sets up correct security context and invokes a target.
		/// This method may not throw any exceptions.
		/// </summary>
		/// <param name="message">The message to be performed.</param>
		/// <param name="connectionLevel">Indicates whether Security Session is used on the connection level.</param>
		public virtual void Invoke(Message message, bool connectionLevel)
		{
			if (connectionLevel)
				message.ITransportContext.IIncomingStreamHandler.HandleMessage_AfterCLSS(message);
			else
				message.ITransportContext.IIncomingStreamHandler.HandleMessage_Final(message);
		}

		/// <summary>
		/// Creates and initializes GenuineChunkedStream with the header containing Processor's
		/// data and the security context name.
		/// </summary>
		/// <returns>Created and initialized GenuineChunkedStream.</returns>
		protected GenuineChunkedStream CreateOutputStream()
		{
			GenuineChunkedStream stream = new GenuineChunkedStream(false);

			// name of the coder
			BinaryWriter binaryWriter = new BinaryWriter(stream);
			binaryWriter.Write(this.Name);

			return stream;
		}

		#endregion

		#region -- Messaging -----------------------------------------------------------------------

		/// <summary>
		/// An array of messages waiting while Security Session is established.
		/// </summary>
		private ArrayList _awaitingMessages = new ArrayList();
		private object _awaitingMessagesLock = new object();

		/// <summary>
		/// Queues the message to wait until its Security Session will be established.
		/// </summary>
		/// <param name="message">The message.</param>
		public void PutMessageToAwaitingQueue(Message message)
		{
			lock (this._awaitingMessagesLock)
				this._awaitingMessages.Add(message);

			if (this.IsEstablished)
				this.SendAssociatedMessages();
		}

		/// <summary>
		/// Sends all messages requring the established Security Session.
		/// </summary>
		public void SendAssociatedMessages()
		{
			ArrayList messages = null;
			lock (this._awaitingMessagesLock)
			{
				messages = this._awaitingMessages;
				this._awaitingMessages = new ArrayList();
			}

			// sends awaiting async messages
			for ( int i = 0; i < messages.Count; i++)
			{
				Message message = (Message) messages[i];
				message.ITransportContext.ConnectionManager.Send(message);
			}
		}

		/// <summary>
		/// Dispatches the specified exception to all callers dependent on this Security Session.
		/// </summary>
		/// <param name="exception">The exception to be dispatched.</param>
		internal void DispatchException(Exception exception)
		{
			this.ReasonOfFailure = exception;
			this.Failed.Set();

			ArrayList messages = null;
			lock (this._awaitingMessagesLock)
			{
				messages = this._awaitingMessages;
				this._awaitingMessages = new ArrayList();
			}

			// sends awaiting async messages
			for ( int i = 0; i < messages.Count; i++)
			{
				Message message = (Message) messages[i];

				if (message.ReplyToId > 0)
					message.ITransportContext.IIncomingStreamHandler.DispatchException(message, exception);
			}
		}

		#endregion

	}
}
