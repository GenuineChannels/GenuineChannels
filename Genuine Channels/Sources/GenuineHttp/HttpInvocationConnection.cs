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

using Belikov.GenuineChannels.BufferPooling;
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
	/// Represents an invocation connection opened to a particular remote host.
	/// </summary>
	public class HttpInvocationConnection
	{
		/// <summary>
		/// Constructs an instance of the HttpInvocationConnection class.
		/// </summary>
		/// <param name="iTransportContext">The transport context.</param>
		/// <param name="remote">The remote host.</param>
		public HttpInvocationConnection(ITransportContext iTransportContext, HostInformation remote)
		{
			this.HostId = iTransportContext.BinaryHostIdentifier;
			this.HostIdAsString = iTransportContext.HostIdentifier;
			this._asyncCallback_onRequestCompleted = new AsyncCallback(this.OnRequestCompleted);

			this.ITransportContext = iTransportContext;
			this.Remote = remote;

//			this.OnEndSending = new AsyncCallback(this.Callback_OnEndSending);
//			this.OnEndReceiving = new AsyncCallback(this.Callback_OnEndReceiving);

			// cache all setting's values
			this._userAgent = this.ITransportContext.IParameterProvider[GenuineParameter.HttpWebUserAgent] as string;
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

			this._hostRenewingSpan = GenuineUtility.ConvertToMilliseconds(iTransportContext.IParameterProvider[GenuineParameter.ClosePersistentConnectionAfterInactivity]) + GenuineUtility.ConvertToMilliseconds(iTransportContext.IParameterProvider[GenuineParameter.MaxTimeSpanToReconnect]);

			if (this._credentials != null || (userName != null && userName != string.Empty &&
				password != null && password != string.Empty) )
			{
				this._useWebAuthentication = true;

				// and setup credentials
				if (this._credentials == null)
				{
					if ((bool) this.ITransportContext.IParameterProvider[GenuineParameter.HttpUseDefaultCredentials])
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
                if (!(this.ITransportContext.IParameterProvider[GenuineParameter.HttpProxyUri] is string))
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

			this._httpAsynchronousRequestTimeout = GenuineUtility.ConvertToMilliseconds(iTransportContext.IParameterProvider[GenuineParameter.HttpAsynchronousRequestTimeout]);
		}

		private int _httpAsynchronousRequestTimeout;

		/// <summary>
		/// The identifier of the remote host.
		/// </summary>
		public byte[] HostId;

		/// <summary>
		/// The connection id in string format.
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
		/// To guarantee atomic access to local members.
		/// </summary>
		private object _accessToLocalMembers = new object();

		#region -- Sending and receiving -----------------------------------------------------------

		private AsyncCallback _asyncCallback_onRequestCompleted;
		private int _hostRenewingSpan;

		/// <summary>
		/// Sends the stream to the remote host.
		/// </summary>
		/// <param name="message">The message.</param>
		public void SendMessage(Message message)
		{
			int availableConnectionEntry = 0;
			HttpWebRequest httpWebRequest = null;

			try
			{
				// serialize the message
				GenuineChunkedStream stream = new GenuineChunkedStream(false);
				using (BufferKeeper bufferKeeper = new BufferKeeper(0))
				{
					MessageCoder.FillInLabelledStream(message, null, null, stream, bufferKeeper.Buffer, 
						(int) this.ITransportContext.IParameterProvider[GenuineParameter.HttpRecommendedPacketSize]);
				}

				// add the header
				GenuineChunkedStream resultStream = new GenuineChunkedStream(false);
				BinaryWriter binaryWriter = new BinaryWriter(resultStream);
				HttpMessageCoder.WriteRequestHeader(binaryWriter, MessageCoder.PROTOCOL_VERSION, GenuineConnectionType.Invocation, this.ITransportContext.BinaryHostIdentifier, HttpPacketType.Usual, message.MessageId, string.Empty, this.Remote.LocalHostUniqueIdentifier);
				if (stream.CanSeek)
					resultStream.WriteStream(stream);
				else
					GenuineUtility.CopyStreamToStream(stream, resultStream);

				// get a connection
				availableConnectionEntry = FindAvailableConnectionEntry();
				httpWebRequest = this.InitializeRequest("__GC_INVC_" + availableConnectionEntry.ToString(), this._keepalive);

				this.InitiateSending(new ConnectionInfo(httpWebRequest, availableConnectionEntry, message), resultStream, 
					this._httpAsynchronousRequestTimeout);
			}
			catch
			{
				try
				{
					if (httpWebRequest != null)
						httpWebRequest.Abort();
				}
				catch
				{
				}

				this.ReleaseConnectionEntry(availableConnectionEntry);

				throw;
			}
		}

		/// <summary>
		/// Initiates sending of the specified HTTP request.
		/// </summary>
		/// <param name="connectionInfo">The connection.</param>
		/// <param name="stream">The content of the request.</param>
		/// <param name="timeout">The timeout of the operation.</param>
		private void InitiateSending(ConnectionInfo connectionInfo, Stream stream, int timeout)
		{
			Stream requestStream = null;

			try
			{
				// try to send it
				connectionInfo.HttpWebRequest.ContentLength = stream.Length;

				requestStream = connectionInfo.HttpWebRequest.GetRequestStream();

#if DEBUG
				byte[] content = new byte[(int) stream.Length];
				GenuineUtility.ReadDataFromStream(stream, content, 0, content.Length);

//				this.ITransportContext.IEventLogger.Log(LogMessageCategory.TransportLayer, null, "HttpInvocationConnection.InitiateSending",
//					content, "The content of the request sent by the client application. Size: {0}.", stream.Length);
				stream = new MemoryStream(content);
#endif

				GenuineUtility.CopyStreamToStream(stream, requestStream, (int) stream.Length);

				//connectionInfo.HttpWebRequest.BeginGetResponse(this._asyncCallback_onRequestCompleted, connectionInfo);
				HttpWebRequestCop webRequestCop = new HttpWebRequestCop(connectionInfo.HttpWebRequest, this._asyncCallback_onRequestCompleted, connectionInfo, this._httpAsynchronousRequestTimeout);
			}
			finally
			{
				if (requestStream != null)
					requestStream.Close();
			}
		}

		/// <summary>
		/// A storage containing information with respect to a specific HTTP request.
		/// </summary>
		private class ConnectionInfo
		{
			/// <summary>
			/// Constructs an instance of the ConnectionInfo class.
			/// </summary>
			/// <param name="httpWebRequest">The web request.</param>
			/// <param name="index">The index.</param>
			/// <param name="message">The source message.</param>
			public ConnectionInfo(HttpWebRequest httpWebRequest, int index, Message message)
			{
				this.HttpWebRequest = httpWebRequest;
				this.Index = index;
				this.Message = message;
			}

			/// <summary>
			/// The HttpWebRequest.
			/// </summary>
			public HttpWebRequest HttpWebRequest;

			/// <summary>
			/// The index.
			/// </summary>
			public int Index;

			/// <summary>
			/// The source message containing messageId. It can be used to dispatch a network exception to the caller context.
			/// </summary>
			public Message Message;
		}

		/// <summary>
		/// Completes the HTTP request.
		/// </summary>
		/// <param name="ar">The result of the HTTP request.</param>
		private void OnRequestCompleted(IAsyncResult ar)
		{
			HttpWebResponse httpWebResponse = null;
			Stream inputStream = null;
			ConnectionInfo connectionInfo = null;

			try
			{
				connectionInfo = (ConnectionInfo) ar.AsyncState;
                HttpWebRequest httpWebRequest = connectionInfo.HttpWebRequest;
#if (FRM20)
                // timeout has been already set
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
				httpWebResponse = (HttpWebResponse) httpWebRequest.EndGetResponse(ar);
#endif
				this.Remote.Renew(this._hostRenewingSpan, false);

				// process the content
				inputStream = httpWebResponse.GetResponseStream();

#if DEBUG
//				if (this.ITransportContext.IEventLogger.AcceptBinaryData)
//				{
//					byte[] content = new byte[(int) httpWebResponse.ContentLength];
//					GenuineUtility.ReadDataFromStream(inputStream, content, 0, content.Length);
//
//					this.ITransportContext.IEventLogger.Log(LogMessageCategory.Traffic, null, "HttpInvocationConnection.OnRequestCompleted",
//						content, "The content of the response received by the HttpInvocationConnection. Size: {0}.", content.Length);
//					inputStream = new MemoryStream(content, false);
//				}
#endif

				BinaryReader binaryReader = new BinaryReader(inputStream);
				string serverUri;
				int sequenceNo;
				HttpPacketType httpPacketType;
				int remoteHostUniqueIdentifier;
				HttpMessageCoder.ReadResponseHeader(binaryReader, out serverUri, out sequenceNo, out httpPacketType, out remoteHostUniqueIdentifier);

#if DEBUG
//				this.ITransportContext.IEventLogger.Log(LogMessageCategory.TransportLayer, null, "HttpInvocationConnection.OnRequestCompleted",
//					null, "The invocation request returned. Server uri: {0}. Sequence no: {1}. Packet type: {2}. Content-encoding: {3}. Content-length: {4}. Protocol version: {5}. Response uri: \"{6}\". Server: \"{7}\". Status code: {8}. Status description: \"{9}\".",
//					serverUri, sequenceNo, Enum.Format(typeof(HttpPacketType), httpPacketType, "g"), 
//					httpWebResponse.ContentEncoding, httpWebResponse.ContentLength, 
//					httpWebResponse.ProtocolVersion, httpWebResponse.ResponseUri, 
//					httpWebResponse.Server, httpWebResponse.StatusCode, httpWebResponse.StatusDescription);
#endif

				// if the remote host has asked to terminate a connection
				if (httpPacketType == HttpPacketType.ClosedManually || httpPacketType == HttpPacketType.Desynchronization || httpPacketType == HttpPacketType.SenderError)
					throw GenuineExceptions.Get_Receive_ConnectionClosed();

				// skip the first byte
				if (binaryReader.ReadByte() != 0)
				{
					if (! connectionInfo.Message.IsOneWay)
					{
//						this.ITransportContext.IEventLogger.Log(LogMessageCategory.FatalError, GenuineExceptions.Get_Processing_LogicError("The invocation response doesn't contain any messages."), "HttpInvocationConnection.OnRequestCompleted",
//							null, "The HTTP response doesn't contain content. The response to the request was not received.");
					}
				}
				else
				{
					// fetch and process the response messages
					using (BufferKeeper bufferKeeper = new BufferKeeper(0))
					{
						using (LabelledStream labelledStream = new LabelledStream(this.ITransportContext, inputStream, bufferKeeper.Buffer))
						{
							GenuineChunkedStream receivedRequest = new GenuineChunkedStream(true);
							GenuineUtility.CopyStreamToStream(labelledStream, receivedRequest);
							this.ITransportContext.IIncomingStreamHandler.HandleMessage(receivedRequest, this.Remote, GenuineConnectionType.Invocation, string.Empty, -1, true, null, null, null);
						}
					}
				}
			}
			catch (Exception ex)
			{
//				this.ITransportContext.IEventLogger.Log(LogMessageCategory.Error, ex, "HttpInvocationConnection.OnRequestCompleted",
//					null, "Exception occurred during receiving a response to an invocation request.");

				// dispatch the exception to the caller context
				if (connectionInfo != null)
					this.ITransportContext.IIncomingStreamHandler.DispatchException(connectionInfo.Message, ex);
			}
			finally
			{
				if (inputStream != null)
					inputStream.Close();
				if (httpWebResponse != null)
					httpWebResponse.Close();

				// release the connection
				if (connectionInfo != null)
					this.ReleaseConnectionEntry(connectionInfo.Index);
			}
		}


		#endregion

		#region -- Connection Postfixes Management -------------------------------------------------

		private ArrayList _availableConnections = new ArrayList();

		/// <summary>
		/// Returns the index of the available element.
		/// </summary>
		/// <returns>The index of the available element.</returns>
		private int FindAvailableConnectionEntry()
		{
			lock (this._availableConnections)
			{
				for ( int i = 0; i < this._availableConnections.Count; i++ )
				{
					if ((bool) this._availableConnections[i])
					{
						this._availableConnections[i] = false;
						return i;
					}
				}

				this._availableConnections.Add(false);
				return this._availableConnections.Count - 1;
			}
		}

		/// <summary>
		/// Releases the element with the specified index.
		/// </summary>
		/// <param name="index">The index.</param>
		private void ReleaseConnectionEntry(int index)
		{
			lock (this._availableConnections)
			{
#if DEBUG
				if (this._availableConnections.Count <= index)
					throw GenuineExceptions.Get_Processing_LogicError("HttpInvocationConnection.ReleaseConnectionEntry. The requested element does not exist.");

				if ((bool) this._availableConnections[index])
					throw GenuineExceptions.Get_Processing_LogicError("HttpInvocationConnection.ReleaseConnectionEntry. Element is already available!");
#endif

				this._availableConnections[index] = true;
			}
		}

		#endregion

		#region -- Setting up the request ----------------------------------------------------------

		/// <summary>
		/// Represents a value indicating whether HTTP 1.1 keep-alive connections will be used.
		/// </summary>
		internal bool _keepalive = false;

		private IWebProxy _iWebProxy;
		private string _userAgent;
		private bool _useWebAuthentication = false;
		private bool _useUnsafeConnectionSharing = false;
		private ICredentials _credentials;
		private bool _allowWriteStreamBuffering = false;

		/// <summary>
		/// Creates and initializes http web request.
		/// </summary>
		/// <param name="postfix">True to create a sender request.</param>
		/// <param name="keepAlive">False to force connection closing.</param>
		/// <returns>Initialized HttpWebRequest instance.</returns>
		public HttpWebRequest InitializeRequest(string postfix, bool keepAlive)
		{
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
#if FRM11
			webRequest.UnsafeAuthenticatedConnectionSharing = this._useUnsafeConnectionSharing;
#endif

			webRequest.KeepAlive = keepAlive;
			webRequest.Method = "POST";
			webRequest.Pipelined = false;
			webRequest.SendChunked = false;

			webRequest.Proxy = this._iWebProxy;
			webRequest.UserAgent = this._userAgent;

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

	}
}
