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
using System.Text.RegularExpressions;

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
	/// Implements GHTTP client connection logic.
	/// </summary>
	internal class HttpClientConnection
	{
		/// <summary>
		/// Constructs an instance of the HttpClientConnection class.
		/// </summary>
		/// <param name="iTransportContext">The Transport Context.</param>
		/// <param name="connectionName">The name of the connection.</param>
		public HttpClientConnection(ITransportContext iTransportContext, string connectionName)
		{
			this.HostId = iTransportContext.BinaryHostIdentifier;
			this.HostIdAsString = iTransportContext.HostIdentifier;
			this.ConnectionName = connectionName;

			this.ITransportContext = iTransportContext;
			this.HttpClientConnectionManager = (HttpClientConnectionManager) this.ITransportContext.ConnectionManager;
			this.MessageContainer = new MessageContainer(iTransportContext);

			// the less buffer the less performance
			this.Sender_SendBuffer = new byte[35000];
			this.Sender_ReceiveBuffer = new byte[35000];
			this.Listener_ReceiveBuffer = new byte[35000];
			this.OnEndSending = new AsyncCallback(this.Callback_OnEndSending);
			this.OnEndReceiving = new AsyncCallback(this.Callback_OnEndReceiving);

			// cache all setting's values
			this._userAgent = this.ITransportContext.IParameterProvider[GenuineParameter.HttpWebUserAgent] as string;
			this._mimeMediaType = this.ITransportContext.IParameterProvider[GenuineParameter.HttpMimeMediaType] as string;
			if (this._userAgent == null)
				this._userAgent = @"Mozilla/4.0+ (compatible; MSIE 6.0; Windows " + Environment.OSVersion.Version +
					"; Genuine HTTP Client Channel; MS .NET CLR " + Environment.Version.ToString() + ")";
			this._userAgent = Regex.Replace(this._userAgent, "\r|\n", " ", RegexOptions.None);

			this._useUnsafeConnectionSharing = (bool) this.ITransportContext.IParameterProvider[GenuineParameter.HttpUnsafeConnectionSharing];
			this._allowWriteStreamBuffering = (bool) this.ITransportContext.IParameterProvider[GenuineParameter.HttpAllowWriteStreamBuffering];

			this._keepalive = (bool) iTransportContext.IParameterProvider[GenuineParameter.HttpKeepAlive];
			this._credentials = this.ITransportContext.IParameterProvider[GenuineParameter.HttpAuthCredential] as ICredentials;
			string userName = this.ITransportContext.IParameterProvider[GenuineParameter.HttpAuthUserName] as string;
			string password = this.ITransportContext.IParameterProvider[GenuineParameter.HttpAuthPassword] as string;
			string domain = this.ITransportContext.IParameterProvider[GenuineParameter.HttpAuthDomain] as string;

			bool httpUseDefaultCredentials = (bool) this.ITransportContext.IParameterProvider[GenuineParameter.HttpUseDefaultCredentials];

			if (this._credentials != null || (userName != null && userName != string.Empty &&
				password != null && password != string.Empty) || httpUseDefaultCredentials)
			{
				this._useWebAuthentication = true;

				// and setup credentials
				if (this._credentials == null)
				{
					if (httpUseDefaultCredentials)
						this._credentials = CredentialCache.DefaultCredentials;
					else
					{
						if (domain == null)
							this._credentials = new NetworkCredential(userName, password);
						else
							this._credentials = new NetworkCredential(userName, password, domain);
					}
				}
			}

			if ((bool) this.ITransportContext.IParameterProvider[GenuineParameter.HttpUseGlobalProxy])
            {
#if (FRM20)
                this._iWebProxy = WebRequest.DefaultWebProxy;
#else
                this._iWebProxy = GlobalProxySelection.Select;
#endif
            }
			else
			{
				if (! (this.ITransportContext.IParameterProvider[GenuineParameter.HttpProxyUri] is string) )
                {
#if (FRM20)
                    this._iWebProxy = null;
#else
                    this._iWebProxy = GlobalProxySelection.GetEmptyWebProxy();
#endif
                }
				else
					this._iWebProxy = new WebProxy((string) this.ITransportContext.IParameterProvider[GenuineParameter.HttpProxyUri], (bool) this.ITransportContext.IParameterProvider[GenuineParameter.HttpBypassOnLocal]);
			}
		}

		/// <summary>
		/// The connection manager.
		/// </summary>
		public HttpClientConnectionManager HttpClientConnectionManager;

		/// <summary>
		/// The identifier of the remote host.
		/// </summary>
		public byte[] HostId;

		/// <summary>
		/// The connection id in string format.
		/// </summary>
		public string HostIdAsString;

		/// <summary>
		/// The name of the connection.
		/// </summary>
		public string ConnectionName;

		/// <summary>
		/// The unique connection identifier, which is used for debugging purposes only.
		/// </summary>
		public int DbgConnectionId = ConnectionManager.GetUniqueConnectionId();

		/// <summary>
		/// The message queue.
		/// </summary>
		public MessageContainer MessageContainer;

		/// <summary>
		/// The transport context.
		/// </summary>
		public ITransportContext ITransportContext;

		/// <summary>
		/// The remote host.
		/// </summary>
		public HostInformation Remote;

		/// <summary>
		/// The type of this connection.
		/// </summary>
		public GenuineConnectionType GenuineConnectionType;

		/// <summary>
		/// To guarantee atomic access to local members.
		/// </summary>
		private object _accessToLocalMembers = new object();

		#region -- Security ------------------------------------------------------------------------

		/// <summary>
		/// Connection-level Security Session.
		/// </summary>
		public SecuritySession Sender_ConnectionLevelSecurity;

		/// <summary>
		/// Connection-level Security Session.
		/// </summary>
		public SecuritySession Listener_ConnectionLevelSecurity;

		#endregion

		#region -- The sender ----------------------------------------------------------------------

		/// <summary>
		/// Sender must obtain the lock for this object in order to send something.
		/// </summary>
		public object SendingLock = new object();

		/// <summary>
		/// Indicates whether the request is being sent to the remote host.
		/// </summary>
		public bool IsSent = false;

		/// <summary>
		/// Contains all messages currently being sent to the remote host.
		/// </summary>
		public ArrayList MessagesBeingSent = new ArrayList();

		/// <summary>
		/// The sender.
		/// </summary>
		public HttpWebRequest Sender;

		/// <summary>
		/// Content being sent to the remote host.
		/// </summary>
		public GenuineChunkedStream SentContent;

		/// <summary>
		/// The buffer for intermediate serialization.
		/// </summary>
		public byte[] Sender_SendBuffer;

		/// <summary>
		/// The buffer for analyzing incoming content.
		/// </summary>
		public byte[] Sender_ReceiveBuffer;

		/// <summary>
		/// The callback calling on the end of sending.
		/// </summary>
		public AsyncCallback OnEndSending;

		/// <summary>
		/// The sequence number of the packet.
		/// </summary>
		public int SendSequenceNo = 0;

		/// <summary>
		/// Signals whether the sender request was completed.
		/// </summary>
		public ManualResetEvent SenderClosed = new ManualResetEvent(true);

		/// <summary>
		/// Processes sending results.
		/// </summary>
		/// <param name="ar">The result.</param>
		private void Callback_OnEndSending(IAsyncResult ar)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			try
			{
				if (ar.AsyncState != this.Sender && binaryLogWriter != null)
				{
					binaryLogWriter.WriteImplementationWarningEvent("HttpClientConnection.Callback_OnEndSending",
						LogMessageType.Warning, GenuineExceptions.Get_Debugging_GeneralWarning("The provided sender does not match the expected Sender instance."),
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
						"The provided sender does not match the expected Sender instance.");
				}

				HttpWebRequest httpWebRequest = (HttpWebRequest) ar.AsyncState;                
#if (FRM20)
                // timeout has been already set
                HttpWebResponse httpWebResponse = null;
                try
                {
                    httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.Timeout)
                        return;
                }
#else
				HttpWebResponse httpWebResponse = (HttpWebResponse) httpWebRequest.EndGetResponse(ar);
#endif

				if (this._disposed)
				{
					httpWebResponse.GetResponseStream().Close();
					httpWebResponse.Close();
					return ;
				}

				this.HttpClientConnectionManager.Pool_Sender_OnEndSending(this, httpWebResponse);
			}
			catch(Exception ex)
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.LowLevelTransport] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.LowLevelTransport, "HttpClientConnection.Callback_OnEndSending",
						LogMessageType.LowLevelTransport_AsyncSendingCompleted, ex, null, this.Remote, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
						this.DbgConnectionId, 0, 0, 0, null, null, null, null,
						"Exception occurred while completing an asynchronous sending.");
				}

				try
				{
					HttpWebRequest httpWebRequest = (HttpWebRequest) ar.AsyncState;
					httpWebRequest.Abort();
				}
				catch
				{
				}

				if (this._disposed)
					return ;

				this.HttpClientConnectionManager.StartReestablishingIfNecessary(this, ex, true);
//
//				this.HttpClientConnectionManager.ConnectionFailed(this, true, ex, true);
			}
		}

		#endregion

		#region -- The Listener --------------------------------------------------------------------

		/// <summary>
		/// The sequence number of the packet.
		/// </summary>
		public int ListenerSequenceNo = 0;

		/// <summary>
		/// The listener.
		/// </summary>
		public HttpWebRequest Listener;

		/// <summary>
		/// The callback calling on the end of receiving.
		/// </summary>
		public AsyncCallback OnEndReceiving;

		/// <summary>
		/// The buffer for analyzing incoming content.
		/// </summary>
		public byte[] Listener_ReceiveBuffer;

		/// <summary>
		/// Signals whether the listener request was completed.
		/// </summary>
		public ManualResetEvent ListenerClosed = new ManualResetEvent(true);

		/// <summary>
		/// Processes sending results.
		/// </summary>
		/// <param name="ar">The result.</param>
		private void Callback_OnEndReceiving(IAsyncResult ar)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			try
			{
#if DEBUG
				if (ar.AsyncState != this.Listener && binaryLogWriter != null)
				{
					binaryLogWriter.WriteImplementationWarningEvent("HttpClientConnection.Callback_OnEndSending",
						LogMessageType.Warning, GenuineExceptions.Get_Debugging_GeneralWarning("The provided receiver does not match the expected Listener instance"),
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
						"The provided receiver does not match the expected Listener instance.");
				}
#endif

                
				HttpWebRequest httpWebRequest = (HttpWebRequest) ar.AsyncState;
#if (FRM20)
                // timeout has been already set
                HttpWebResponse httpWebResponse = null;
                try
                {
                    httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.Timeout)
                        return;
                }
#else
                HttpWebResponse httpWebResponse = (HttpWebResponse) httpWebRequest.EndGetResponse(ar);
#endif


                if (this._disposed)
				{
					httpWebResponse.GetResponseStream().Close();
					httpWebResponse.Close();
					return ;
				}

				this.ITransportContext.ConnectionManager.IncreaseBytesReceived((int) httpWebResponse.ContentLength);
				this.HttpClientConnectionManager.Listener_OnEndReceiving(this, httpWebResponse);
			}
			catch(Exception ex)
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.LowLevelTransport] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.LowLevelTransport, "HttpClientConnection.Callback_OnEndReceiving",
						LogMessageType.LowLevelTransport_AsyncReceivingCompleted, ex, null, this.Remote, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
						this.DbgConnectionId, 0, 0, 0, null, null, null, null,
						"Exception occurred while completing an asynchronous receiving.");
				}

				try
				{
                    // TODO: Receiving has been completed by this time. Wny do we call abort?
#if (!FRM20)
					HttpWebRequest httpWebRequest = (HttpWebRequest) ar.AsyncState;
					httpWebRequest.Abort();
#endif
				}
				catch
				{
				}

				if (this._disposed)
					return ;

				this.HttpClientConnectionManager.StartReestablishingIfNecessary(this, ex, false);
//
//				this.HttpClientConnectionManager.ConnectionFailed(this, false, ex, true);
			}
		}

		#endregion

		#region -- Persistent connection stuff (including reconnection) ----------------------------

		/// <summary>
		/// Implements a reconnection lock that indicates whether reconnection process is in progress
		/// and when its timeout expires.
		/// </summary>
		internal class ReconnectionContainer
		{
			/// <summary>
			/// A lock for this object must be obtained before using any members of this class.
			/// </summary>
			public object SyncLock = new object();

			/// <summary>
			/// A boolean value that indicates whether the HTTP request has failed and IsReconnectionStarted was true when this happened.
			/// </summary>
			public bool RequestFailed
			{
				get
				{
					lock (this.SyncLock)
						return this._requestFailed;
				}
				set
				{
					lock (this.SyncLock)
						this._requestFailed = value;
				}
			}
			private bool _requestFailed = false;

			/// <summary>
			/// The moment when reconnection was initiated.
			/// </summary>
			public int ReconnectionStartedAt
			{
				get
				{
					lock (this.SyncLock)
						return this._reconnectionStarted;
				}
				set
				{
					lock (this.SyncLock)
						this._reconnectionStarted = value;
				}
			}
			private int _reconnectionStarted = 0;

			/// <summary>
			/// Gets or sets a value indicating whether the reconnection is in progress.
			/// </summary>
			public bool IsReconnectionStarted
			{
				get
				{
					lock (this.SyncLock)
						return this._isReconnectionStarted;
				}
				set
				{
					lock (this.SyncLock)
					{
						if (value)
						{
							this._isReconnectionStarted = true;
							this._reconnectionStarted = GenuineUtility.TickCount;
						}
						else
						{
							this._isReconnectionStarted = false;
							this._reconnectionStarted = 0;
						}
					}
				}
			}
			private bool _isReconnectionStarted = false;
		}

		/// <summary>
		/// The reconnection lock for the listener connection.
		/// </summary>
		public ReconnectionContainer ListenerReconnectionLock = new ReconnectionContainer();

		/// <summary>
		/// The reconnection lock for the sender connection.
		/// </summary>
		public ReconnectionContainer SenderReconnectionLock = new ReconnectionContainer();

		#endregion

		#region -- Setting up the request ----------------------------------------------------------

		/// <summary>
		/// Represents a value indicating whether HTTP 1.1 keep-alive connections will be used.
		/// </summary>
		internal bool _keepalive = false;

		private IWebProxy _iWebProxy;
		private string _userAgent;
		private string _mimeMediaType;
		private bool _useWebAuthentication = false;
		private bool _useUnsafeConnectionSharing = false;
		private ICredentials _credentials;
		private bool _allowWriteStreamBuffering = false;

		/// <summary>
		/// Creates and initializes http web request.
		/// </summary>
		/// <param name="sender">True to create a sender request.</param>
		/// <param name="keepAlive">False to force connection closing.</param>
		/// <returns>Initialized HttpWebRequest instance.</returns>
		public HttpWebRequest InitializeRequest(bool sender, bool keepAlive)
		{
			string postfix = sender ? "SENDER" : "LISTENER";

			// setup connection
			int prefixPos = this.Remote.Url.IndexOf(':');
			if (prefixPos <= 0)
				throw GenuineExceptions.Get_Connect_CanNotConnectToRemoteHost(this.Remote.Url, "Incorrect URI.");

			HttpWebRequest webRequest = null;
			if (this.Remote.Url[prefixPos - 1] == 's')
				webRequest = (HttpWebRequest) HttpWebRequest.Create("https" + this.Remote.Url.Substring(prefixPos) + '?' + Guid.NewGuid().ToString("N"));
			else
				webRequest = (HttpWebRequest) HttpWebRequest.Create("http" + this.Remote.Url.Substring(prefixPos) + '?' + Guid.NewGuid().ToString("N"));

			webRequest.AllowAutoRedirect = false;
			webRequest.AllowWriteStreamBuffering = this._useWebAuthentication || this._allowWriteStreamBuffering || this._iWebProxy != null;
			webRequest.ContentType = "application/octet-stream";
			webRequest.ConnectionGroupName = this.HostIdAsString + postfix;

			webRequest.PreAuthenticate = this._useWebAuthentication;

#if FRM11 || FRM20
			webRequest.UnsafeAuthenticatedConnectionSharing = this._useUnsafeConnectionSharing;
#endif

			webRequest.KeepAlive = keepAlive;
			webRequest.Method = "POST";
			webRequest.Pipelined = false;
			webRequest.SendChunked = false;

			webRequest.Proxy = this._iWebProxy;
			webRequest.UserAgent = this._userAgent;
			webRequest.MediaType = this._mimeMediaType;

			webRequest.Accept = @"*/*"; 
			webRequest.Headers.Set("Cache-Control", "no-cache");
			webRequest.Headers.Set("Pragma", "no-cache");

			webRequest.Expect = null;

			if (this._credentials != null)
			{
				webRequest.Credentials = this._credentials;
				webRequest.AllowWriteStreamBuffering = true;
			}

			return webRequest;
		}

		#endregion

		#region -- Timing --------------------------------------------------------------------------

		/// <summary>
		/// The moment by which the message should be received successfuly via this connection.
		/// </summary>
		public int LastMessageWasReceviedAt
		{
			get
			{
				lock (this._accessToLocalMembers)
					return this._lastMessageWasReceviedAt;
			}
			set
			{
				lock (this._accessToLocalMembers)
					this._lastMessageWasReceviedAt = value;
			}
		}
		private int _lastMessageWasReceviedAt = GenuineUtility.TickCount;

		/// <summary>
		/// The moment at which the last message was sent to the remote host.
		/// </summary>
		public int LastMessageWasSentAt
		{
			get
			{
				lock (this._accessToLocalMembers)
					return this._lastMessageWasSentAt;
			}
			set
			{
				lock (this._accessToLocalMembers)
					this._lastMessageWasSentAt = value;
			}
		}
		private int _lastMessageWasSentAt = GenuineUtility.TickCount;

		#endregion

		#region -- Connection shut down ------------------------------------------------------------

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

			try
			{
				this.Sender.Abort();
			}
			catch(Exception)
			{
			}

			try
			{
				this.Sender = this.InitializeRequest(true, false);
				this.Sender.ContentLength = 0;
				this.Sender.GetRequestStream().Close();
				this.Sender.Timeout = 300;
				HttpWebResponse httpWebResponse = (HttpWebResponse) this.Sender.GetResponse();
				httpWebResponse.GetResponseStream().Close();
				httpWebResponse.Close();
			}
			catch(Exception)
			{
			}

			if (this.GenuineConnectionType == GenuineConnectionType.Persistent)
			{
				try
				{
					this.Listener.Abort();
				}
				catch(Exception)
				{
				}

				try
				{
					this.Listener = this.InitializeRequest(false, false);
					this.Listener.ContentLength = 0;
					this.Listener.GetRequestStream().Close();
					this.Listener.Timeout = 300;
					HttpWebResponse httpWebResponse = (HttpWebResponse) this.Listener.GetResponse();
					httpWebResponse.GetResponseStream().Close();
					httpWebResponse.Close();
				}
				catch(Exception)
				{
				}
			}
		}

		/// <summary>
		/// Finishes the asynchronous invocation.
		/// </summary>
		/// <param name="ar">The async result.</param>
		private void On_ShutdownCompleted(IAsyncResult ar)
		{
			try
			{
                HttpWebRequest httpWebRequest = (HttpWebRequest)ar.AsyncState;
#if (FRM20)
                // timeout has been already set
                HttpWebResponse httpWebResponse = null;
                try
                {
                    httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.Timeout)
                        return;
                }
#else
				HttpWebResponse httpWebResponse = (HttpWebResponse) httpWebRequest.EndGetResponse(ar);
#endif
                httpWebResponse.GetResponseStream().Close();
				httpWebResponse.Close();
			}
			catch(Exception)
			{
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

	}
}
