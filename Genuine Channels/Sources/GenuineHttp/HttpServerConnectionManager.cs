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
using System.Runtime.Remoting.Messaging;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Web;

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
using Zyan.SafeDeserializationHelpers;

namespace Belikov.GenuineChannels.GenuineHttp
{
	/// <summary>
	/// Intercepts incoming requests come to the registered web handler and processes them.
	/// </summary>
	internal class HttpServerConnectionManager : ConnectionManager, ITimerConsumer
	{
		/// <summary>
		/// Constructs an instance of the HttpServerConnectionManager class.
		/// </summary>
		/// <param name="iTransportContext">The transport context.</param>
		public HttpServerConnectionManager(ITransportContext iTransportContext) : base(iTransportContext)
		{
			this.Local = new HostInformation("_ghttp://" + iTransportContext.HostIdentifier, iTransportContext);
			this._releaseConnections_InspectPersistentConnections = new PersistentConnectionStorage.ProcessConnectionEventHandler(this.ReleaseConnections_InspectPersistentConnections);
			this._internal_TimerCallback_InspectPersistentConnections = new PersistentConnectionStorage.ProcessConnectionEventHandler(this.Internal_TimerCallback_InspectPersistentConnections);

			// calculate host renewing timespan
//			this._hostRenewingSpan = GenuineUtility.ConvertToMilliseconds(iTransportContext.IParameterProvider[GenuineParameter.ClosePersistentConnectionAfterInactivity]) + GenuineUtility.ConvertToMilliseconds(iTransportContext.IParameterProvider[GenuineParameter.MaxTimeSpanToReconnect]);
			this._internal_TimerCallback = new WaitCallback(Internal_TimerCallback);

			this._closeInvocationConnectionAfterInactivity = GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.CloseInvocationConnectionAfterInactivity]);

			TimerProvider.Attach(this);
		}

		/// <summary>
		/// Sends the message to the remote host.
		/// </summary>
		/// <param name="message">The message to be sent.</param>
		protected override void InternalSend(Message message)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			switch (message.SecuritySessionParameters.GenuineConnectionType)
			{
				case GenuineConnectionType.Persistent:
					if (message.ConnectionName == null)
						message.ConnectionName = message.SecuritySessionParameters.ConnectionName;

					HttpServerConnection httpServerConnection = this._persistent.Get(message.Recipient.Uri, message.ConnectionName) as HttpServerConnection;
					if (httpServerConnection == null)
						throw GenuineExceptions.Get_Send_DestinationIsUnreachable(message.Recipient.Uri);

					bool messageWasSent = false;
					lock (httpServerConnection.Listener_Lock)
					{
						if (httpServerConnection.Listener != null)
						{
							try
							{
								// the listener request is available - send the message through it
								if (httpServerConnection.Listener.HttpContext.Response.IsClientConnected)
								{
									messageWasSent = true;

									// some data is available, gather the stream and send it
									GenuineChunkedStream responseStream = this.LowLevel_CreateStreamWithHeader(HttpPacketType.Usual, httpServerConnection.Listener_SequenceNo, message.Recipient);
									MessageCoder.FillInLabelledStream(message, httpServerConnection.Listener_MessageContainer,
										httpServerConnection.Listener_MessagesBeingSent, responseStream,
										httpServerConnection.Listener_IntermediateBuffer,
										(int) this.ITransportContext.IParameterProvider[GenuineParameter.HttpRecommendedPacketSize]);

									// LOG:
									if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
									{
										for ( int i = 0; i < httpServerConnection.Listener_MessagesBeingSent.Count; i++)
										{
											Message nextMessage = (Message) httpServerConnection.Listener_MessagesBeingSent[i];

											binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "HttpServerConnectionManager.InternalSend",
												LogMessageType.MessageIsSentAsynchronously, null, nextMessage, httpServerConnection.Remote, null,
												GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, httpServerConnection.Listener_SecuritySession,
												null,
												httpServerConnection.DbgConnectionId, httpServerConnection.Listener_SequenceNo, 0, 0, null, null, null, null,
												"The message will be sent in the LISTENER stream N: {0}.", httpServerConnection.Listener_SequenceNo);
										}
									}

									httpServerConnection.Listener_SentStream = responseStream;
									this.LowLevel_SendStream(httpServerConnection.Listener.HttpContext, true, httpServerConnection, true, ref httpServerConnection.Listener_SentStream, httpServerConnection);
								}
							}
							catch(Exception ex)
							{
								// a client is expected to re-request this data
								// LOG:
								if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
								{
									binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpServerConnectionManager.InternalSend",
										LogMessageType.ConnectionEstablishing, ex, null, httpServerConnection.Remote, null,
										GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
										httpServerConnection.DbgConnectionId, 0, 0, 0, null, null, null, null,
										"Asynchronous sending failed. Listener seq no: {0}.", httpServerConnection.Listener_SequenceNo);
								}
							}
							finally
							{
								httpServerConnection.Listener.Complete(false);
								httpServerConnection.Listener = null;
							}
						}

						if (! messageWasSent)
						{
							try
							{
								// there is no listener requests available, so put the message into the queue
								httpServerConnection.Listener_MessageContainer.AddMessage(message, false);
							}
							catch(Exception ex)
							{
								// too many messages in the queue
								httpServerConnection.Dispose(ex);
								this.ITransportContext.KnownHosts.ReleaseHostResources(message.Recipient, ex);
							}
						}
					}
					break;

				case GenuineConnectionType.Named:
					// named connections are not supported
					throw new NotSupportedException("The named pattern is not supported by the GHTTP server channel.");

				case GenuineConnectionType.Invocation:
					// I/connectionId
					lock (this._invocation.SyncRoot)
					{
						if (this._invocation.ContainsKey(message.SecuritySessionParameters.ConnectionName))
						{
							// the response has been already registered
							if (this._invocation[message.SecuritySessionParameters.ConnectionName] != null)
								throw GenuineExceptions.Get_Processing_LogicError("The response has been already registered.");

							this._invocation[message.SecuritySessionParameters.ConnectionName] = message;
							break;
						}
					}

					// there is no invocation connection awaiting for this response
					throw GenuineExceptions.Get_Send_DestinationIsUnreachable(message.SecuritySessionParameters.ConnectionName);
			}
		}


		#region -- Handling incoming requests ------------------------------------------------------

		private int _closeInvocationConnectionAfterInactivity;

		/// <summary>
		/// Handles the incoming HTTP request.
		/// </summary>
		/// <param name="httpServerRequestResultAsObject">The HTTP request.</param>
		public void HandleIncomingRequest(object httpServerRequestResultAsObject)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			HttpServerRequestResult httpServerRequestResult = (HttpServerRequestResult) httpServerRequestResultAsObject;
			HttpServerConnection httpServerConnection = null;
			bool postponeResponse = false;

			try
			{
				// get the stream
				HttpRequest httpRequest = httpServerRequestResult.HttpContext.Request;
				Stream incomingStream = httpRequest.InputStream;

				if (incomingStream.Length <= 0)
				{
					// LOG:
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.LowLevelTransport] > 0 )
					{
						binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpServerConnectionManager.HandleIncomingRequest",
							LogMessageType.LowLevelTransport_AsyncReceivingCompleted, null, null, null, null,
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
							-1, 0, 0, 0, null, null, null, null,
							"Empty content has been received. It will be ignored.");
					}

					return ;
				}

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.LowLevelTransport] > 0 )
				{
					bool writeContent = binaryLogWriter[LogCategory.LowLevelTransport] > 1;
					GenuineChunkedStream content = null;
					if (writeContent)
					{
						content = new GenuineChunkedStream(false);
						GenuineUtility.CopyStreamToStream(incomingStream, content);
					}

					binaryLogWriter.WriteTransportContentEvent(LogCategory.LowLevelTransport, "HttpServerConnectionManager.HandleIncomingRequest",
						LogMessageType.LowLevelTransport_AsyncReceivingCompleted, null, null, null,
						writeContent ? content : null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, -1, (int) httpServerRequestResult.HttpContext.Request.ContentLength,
						httpServerRequestResult.HttpContext.Request.UserHostAddress,
						null, null,
						"HTTP request has been received.");

					if (writeContent)
						incomingStream = content;
				}

				BinaryReader binaryReader = new BinaryReader(incomingStream);

				// read the header
				byte protocolVersion;
				GenuineConnectionType genuineConnectionType;
				Guid hostId;
				HttpPacketType httpPacketType;
				int sequenceNo;
				string connectionName;
				int remoteHostUniqueIdentifier;
				HttpMessageCoder.ReadRequestHeader(binaryReader, out protocolVersion, out genuineConnectionType, out hostId, out httpPacketType, out sequenceNo, out connectionName, out remoteHostUniqueIdentifier);

				HostInformation remote = this.ITransportContext.KnownHosts["_ghttp://" + hostId.ToString("N")];
				remote.ProtocolVersion = protocolVersion;
//				remote.Renew(this._hostRenewingSpan, false);
				remote.PhysicalAddress = httpRequest.UserHostAddress;

				// raise an event if we were not recognized
				remote.UpdateUri(remote.Uri, remoteHostUniqueIdentifier);

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpServerConnectionManager.HandleIncomingRequest",
						LogMessageType.ReceivingFinished, null, null, remote, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null, -1,
						0, 0, 0, null, null, null, null,
						"HTTP request is being processed. Packet type: {0}. Sequence no: {1}. Content length: {2}. Host address: {3}.",
						Enum.Format(typeof(HttpPacketType), httpPacketType, "g"), sequenceNo, httpRequest.ContentLength, httpRequest.UserHostAddress);
				}

				// prepare the output stream
				GenuineChunkedStream outputStream = new GenuineChunkedStream(false);
				BinaryWriter binaryWriter = new BinaryWriter(outputStream);
				HttpMessageCoder.WriteResponseHeader(binaryWriter, protocolVersion, this.ITransportContext.ConnectionManager.Local.Uri, sequenceNo, httpPacketType, remote.LocalHostUniqueIdentifier);

				// analyze the received packet
				switch(genuineConnectionType)
				{
					case GenuineConnectionType.Persistent:
					{
						// get the server connection
						lock (remote.PersistentConnectionEstablishingLock)
						{
							httpServerConnection = this._persistent.Get(remote.Uri, connectionName) as HttpServerConnection;
							if (httpServerConnection != null && httpServerConnection._disposed)
								httpServerConnection = null;

							// initialize CLSS from the very beginning, if necessary
							if (httpPacketType == HttpPacketType.Establishing_ResetConnection)
							{
								if (httpServerConnection != null)
									httpServerConnection.Dispose(GenuineExceptions.Get_Receive_ConnectionClosed("Client sends Establishing_ResetCLSS flag."));
								httpServerConnection = null;
							}

							if (httpServerConnection == null)
							{
								// create the new connection
								httpServerConnection = new HttpServerConnection(this.ITransportContext, hostId, remote, connectionName,
									GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.ClosePersistentConnectionAfterInactivity]) + GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.MaxTimeSpanToReconnect]));

								if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
								{
									binaryLogWriter.WriteConnectionParameterEvent(LogCategory.Connection, "HttpServerConnectionManager.HandleIncomingRequest",
										LogMessageType.ConnectionParameters, null, remote, this.ITransportContext.IParameterProvider,
										GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, httpServerConnection.DbgConnectionId,
										"HTTP connection is being established.");
								}

								this._persistent.Set(remote.Uri, connectionName, httpServerConnection);

								// and CLSS
								string securitySessionName = this.ITransportContext.IParameterProvider[GenuineParameter.SecuritySessionForPersistentConnections] as string;
								if (securitySessionName != null)
								{
									httpServerConnection.Sender_SecuritySession = this.ITransportContext.IKeyStore.GetKey(securitySessionName).CreateSecuritySession(securitySessionName, null);
									httpServerConnection.Listener_SecuritySession = this.ITransportContext.IKeyStore.GetKey(securitySessionName).CreateSecuritySession(securitySessionName, null);
								}

								// LOG:
								if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
								{
									binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpServerConnectionManager.HandleIncomingRequest",
										LogMessageType.ConnectionEstablished, null, null, remote, null,
										GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, httpServerConnection.Sender_SecuritySession, securitySessionName,
										httpServerConnection.DbgConnectionId, (int) GenuineConnectionType.Persistent, 0, 0, this.GetType().Name, null, null, null,
										"The connection is being established.");
								}

								// LOG:
								if ( binaryLogWriter != null && binaryLogWriter[LogCategory.HostInformation] > 0 )
								{
									binaryLogWriter.WriteHostInformationEvent("HttpServerConnectionManager.HandleIncomingRequest",
										LogMessageType.HostInformationCreated, null, remote,
										GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, httpServerConnection.Sender_SecuritySession,
										securitySessionName, httpServerConnection.DbgConnectionId,
										"HostInformation is ready for actions.");
								}

								this.ITransportContext.IGenuineEventProvider.Fire(new GenuineEventArgs(GenuineEventType.GHttpConnectionAccepted, null, remote, httpRequest.UserHostAddress));
							}
						}

						httpServerConnection.Renew();
						httpServerConnection.SignalState(GenuineEventType.GeneralConnectionEstablished, null, null);

						switch(httpPacketType)
						{
							case HttpPacketType.Establishing_ResetConnection:
							case HttpPacketType.Establishing:
								Stream clsseStream = Stream.Null;

								// establish the CLSS
								// P/Sender
								int length = binaryReader.ReadInt32();
								if (httpServerConnection.Sender_SecuritySession != null)
								{
									using (Stream senderClssReading = new DelimiterStream(incomingStream, length))
									{
										clsseStream = httpServerConnection.Sender_SecuritySession.EstablishSession(
											senderClssReading, true);
									}
								}

								if (clsseStream == null)
									clsseStream = Stream.Null;

								using (new GenuineChunkedStreamSizeLabel(outputStream))
									GenuineUtility.CopyStreamToStream(clsseStream, outputStream);

								// P/Listener
								length = binaryReader.ReadInt32();
								clsseStream = Stream.Null;
								if (httpServerConnection.Listener_SecuritySession != null)
									clsseStream = httpServerConnection.Listener_SecuritySession.EstablishSession(
										new DelimiterStream(incomingStream, length), true);

								if (clsseStream == null)
									clsseStream = Stream.Null;

								using (new GenuineChunkedStreamSizeLabel(outputStream))
									GenuineUtility.CopyStreamToStream(clsseStream, outputStream);

								// write the answer
								Stream finalStream = outputStream;
								this.LowLevel_SendStream(httpServerRequestResult.HttpContext, false, null, false, ref finalStream, httpServerConnection);
								break;

							case HttpPacketType.Listening:
								postponeResponse = this.LowLevel_ProcessListenerRequest(httpServerRequestResult, httpServerConnection, sequenceNo);
								break;

							case HttpPacketType.Usual:
								this.LowLevel_ProcessSenderRequest(genuineConnectionType, incomingStream, httpServerRequestResult, httpServerConnection, sequenceNo, remote);
								break;

							default:
								// LOG:
								if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
								{
									binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpServerConnectionManager.HandleIncomingRequest",
										LogMessageType.Error, GenuineExceptions.Get_Debugging_GeneralWarning("Unexpected type of the packet."), null, remote, null,
										GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null, httpServerConnection == null ? -1 : httpServerConnection.DbgConnectionId,
										0, 0, 0, null, null, null, null,
										"Unexpected type of the packet. Packet type: {0}. Sequence no: {1}. Content length: {2}. Host address: {3}.",
										Enum.Format(typeof(HttpPacketType), httpPacketType, "g"), sequenceNo, httpRequest.ContentLength, httpRequest.UserHostAddress);
								}
								break;
						}
					}
						break;

					case GenuineConnectionType.Named:
						throw new NotSupportedException("Named connections are not supported.");

					case GenuineConnectionType.Invocation:
						// renew the host
						remote.Renew(this._closeInvocationConnectionAfterInactivity, false);

						this.LowLevel_ProcessSenderRequest(genuineConnectionType, incomingStream, httpServerRequestResult, null, sequenceNo, remote);
						break;
				}
			}
			catch(Exception ex)
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpServerConnectionManager.HandleIncomingRequest",
						LogMessageType.ReceivingFinished, ex, null, null, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null, -1,
						0, 0, 0, null, null, null, null,
						"Error occurred while processing incoming HTTP request.");
				}
			}
			finally
			{
				if (! postponeResponse)
					httpServerRequestResult.Complete(true);
			}
		}

		#endregion

		#region -- Low-level members ---------------------------------------------------------------

		/// <summary>
		/// Handles the listener request.
		/// </summary>
		/// <param name="httpServerRequestResult">The request.</param>
		/// <param name="httpServerConnection">The connection.</param>
		/// <param name="requestedSequenceNo">The sequence number.</param>
		/// <returns>True if the response should be delayed.</returns>
		public bool LowLevel_ProcessListenerRequest(HttpServerRequestResult httpServerRequestResult, HttpServerConnection httpServerConnection, int requestedSequenceNo)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			GenuineChunkedStream responseStream = null;

			lock (httpServerConnection.Listener_Lock)
			{
				// if there is an already registered listener request, release it
				if (httpServerConnection.Listener != null)
				{
					try
					{
						this.LowLevel_ReportServerError(httpServerConnection.Listener.HttpContext);
					}
					finally
					{
						httpServerConnection.Listener.Complete(false);
						httpServerConnection.Listener = null;
					}
				}

				try
				{
					Debug.Assert(httpServerConnection.Listener == null);

					// if the previous content requested - send it
					if (requestedSequenceNo == httpServerConnection.Listener_SequenceNo && httpServerConnection.Listener_SentStream != null)
					{
						httpServerConnection.Listener_SentStream.Position = 0;
						this.LowLevel_SendStream(httpServerRequestResult.HttpContext, true, httpServerConnection, false, ref httpServerConnection.Listener_SentStream, httpServerConnection);
						return false;
					}

					// if too old content version requested - send an error
					if (requestedSequenceNo < httpServerConnection.Listener_SequenceNo || requestedSequenceNo > httpServerConnection.Listener_SequenceNo + 1)
					{
						// LOG:
						if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
						{
							string errorMessage = string.Format("Desynchronization error. Current sequence number: {0}. Requested sequence number: {1}.", httpServerConnection.Listener_SequenceNo, requestedSequenceNo);
							binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpServerConnectionManager.LowLevel_ProcessListenerRequest",
								LogMessageType.Error, GenuineExceptions.Get_Debugging_GeneralWarning(errorMessage), null, httpServerConnection.Remote, null,
								GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
								httpServerConnection.DbgConnectionId, 0, 0, 0, null, null, null, null,
								errorMessage);
						}

						// send the error
						Stream errorResponseStream = this.LowLevel_CreateStreamWithHeader(HttpPacketType.Desynchronization, requestedSequenceNo, httpServerConnection.Remote);
						MessageCoder.FillInLabelledStream(null, null, null, (GenuineChunkedStream) errorResponseStream, httpServerConnection.Listener_IntermediateBuffer, (int) this.ITransportContext.IParameterProvider[GenuineParameter.HttpRecommendedPacketSize]);
						this.LowLevel_SendStream(httpServerRequestResult.HttpContext, true, httpServerConnection, true, ref errorResponseStream, httpServerConnection);
						return false;
					}

					// the next sequence is requested
					if (httpServerConnection.Listener_SentStream != null)
					{
						// release the content
						httpServerConnection.Listener_MessagesBeingSent.Clear();

						httpServerConnection.Listener_SentStream.Close();
						httpServerConnection.Listener_SentStream = null;
					}

					Message message = httpServerConnection.Listener_MessageContainer.GetMessage();
					if (message == null)
					{
						// LOG:
						if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
						{
							binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpServerConnectionManager.LowLevel_ProcessListenerRequest",
								LogMessageType.ReceivingFinished, null, null, httpServerConnection.Remote, null,
								GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
								httpServerConnection.DbgConnectionId, 0, 0, 0, null, null, null, null,
								"Listener request is postponed.");
						}

						// no data is available, postpone the request
						httpServerConnection.Listener_Opened = GenuineUtility.TickCount;
						httpServerConnection.Listener = httpServerRequestResult;
						httpServerConnection.Listener_SequenceNo = requestedSequenceNo;
						return true;
					}

					// some data is available, gather the stream and send it
					responseStream = this.LowLevel_CreateStreamWithHeader(HttpPacketType.Usual, requestedSequenceNo, httpServerConnection.Remote);
					MessageCoder.FillInLabelledStream(message, httpServerConnection.Listener_MessageContainer,
						httpServerConnection.Listener_MessagesBeingSent, responseStream,
						httpServerConnection.Listener_IntermediateBuffer,
						(int) this.ITransportContext.IParameterProvider[GenuineParameter.HttpRecommendedPacketSize]);

					// LOG:
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
					{
						for ( int i = 0; i < httpServerConnection.Listener_MessagesBeingSent.Count; i++)
						{
							Message nextMessage = (Message) httpServerConnection.Listener_MessagesBeingSent[i];

							binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "HttpServerConnectionManager.LowLevel_ProcessListenerRequest",
								LogMessageType.MessageIsSentAsynchronously, null, nextMessage, httpServerConnection.Remote, null,
								GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, httpServerConnection.Listener_SecuritySession,
								null,
								httpServerConnection.DbgConnectionId, httpServerConnection.Listener_SequenceNo, 0, 0, null, null, null, null,
								"The message is sent in the LISTENER stream N: {0}.", httpServerConnection.Listener_SequenceNo);
						}
					}

					httpServerConnection.Listener_SentStream = responseStream;
					httpServerConnection.Listener_SequenceNo = requestedSequenceNo;
					this.LowLevel_SendStream(httpServerRequestResult.HttpContext, true, httpServerConnection, true, ref httpServerConnection.Listener_SentStream, httpServerConnection);
				}
				catch(Exception ex)
				{
					// LOG:
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
					{
						binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpServerConnectionManager.LowLevel_ProcessListenerRequest",
							LogMessageType.Error, ex, null, httpServerConnection.Remote, null,
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
							httpServerConnection.DbgConnectionId, 0, 0, 0, null, null, null, null,
							"Error occurred while processing the listener request N: {0}.", httpServerConnection.Listener_SequenceNo);
					}
				}
			}

			return false;
		}

		/// <summary>
		/// Processes the sender's request.
		/// </summary>
		/// <param name="genuineConnectionType">The type of the connection.</param>
		/// <param name="input">The incoming data.</param>
		/// <param name="httpServerRequestResult">The request.</param>
		/// <param name="httpServerConnection">The connection.</param>
		/// <param name="sequenceNo">The sequence number.</param>
		/// <param name="remote">The information about remote host.</param>
		public void LowLevel_ProcessSenderRequest(GenuineConnectionType genuineConnectionType, Stream input, HttpServerRequestResult httpServerRequestResult, HttpServerConnection httpServerConnection, int sequenceNo, HostInformation remote)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			GenuineChunkedStream outputStream = null;

			// parse the incoming stream
			bool directExecution = genuineConnectionType != GenuineConnectionType.Persistent;
			BinaryReader binaryReader = new BinaryReader(input);

			using (BufferKeeper bufferKeeper = new BufferKeeper(0))
			{
				switch(genuineConnectionType)
				{
					case GenuineConnectionType.Persistent:
						Exception gotException = null;

						try
						{
							if (httpServerConnection.Sender_SecuritySession != null)
							{
								input = httpServerConnection.Sender_SecuritySession.Decrypt(input);
								binaryReader = new BinaryReader(input);
							}

							while (binaryReader.ReadByte() == 0)
								using(LabelledStream labelledStream = new LabelledStream(this.ITransportContext, input, bufferKeeper.Buffer))
								{
									GenuineChunkedStream receivedContent = new GenuineChunkedStream(true);
									GenuineUtility.CopyStreamToStream(labelledStream, receivedContent);
									this.ITransportContext.IIncomingStreamHandler.HandleMessage(receivedContent, httpServerConnection.Remote, genuineConnectionType, httpServerConnection.ConnectionName, httpServerConnection.DbgConnectionId, false, this._iMessageRegistrator, httpServerConnection.Sender_SecuritySession, httpServerRequestResult);
								}
						}
						catch(Exception ex)
						{
							gotException = ex;

							// LOG:
							if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
							{
								binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpServerConnectionManager.LowLevel_ProcessSenderRequest",
									LogMessageType.Error, ex, null, httpServerConnection.Remote, null,
									GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
									httpServerConnection.DbgConnectionId, 0, 0, 0, null, null, null, null,
									"Error occurred while processing the sender request N: {0}.", httpServerConnection.Sender_SequenceNo);
							}
						}

						if (gotException != null)
						{
							gotException = OperationException.WrapException(gotException);
							outputStream = this.LowLevel_CreateStreamWithHeader(HttpPacketType.SenderError, sequenceNo, remote);
							var binaryFormatter = new BinaryFormatter(new RemotingSurrogateSelector(), new StreamingContext(StreamingContextStates.Other)).Safe();
							binaryFormatter.Serialize(outputStream, gotException);
						}
						else
						{
							// serialize and send the empty response
							outputStream = this.LowLevel_CreateStreamWithHeader(HttpPacketType.SenderResponse, sequenceNo, remote);
							MessageCoder.FillInLabelledStream(null, null, null, outputStream,
								bufferKeeper.Buffer, (int) this.ITransportContext.IParameterProvider[GenuineParameter.HttpRecommendedPacketSize]);
						}
						break;

					case GenuineConnectionType.Invocation:
						// register the http context as an invocation waiters
						string connectionGuid = Guid.NewGuid().ToString("N");

						try
						{
							if (binaryReader.ReadByte() != 0)
							{
								// LOG:
								if ( binaryLogWriter != null )
								{
									binaryLogWriter.WriteImplementationWarningEvent("HttpServerConnectionManager.LowLevel_ProcessSenderRequest", LogMessageType.Error,
										GenuineExceptions.Get_Debugging_GeneralWarning("The invocation request doesn't contain any messages."),
										GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
										"The invocation request doesn't contain any messages.");
								}
							}

							using(LabelledStream labelledStream = new LabelledStream(this.ITransportContext, input, bufferKeeper.Buffer))
							{
								// process the response
								this._invocation[connectionGuid] = null;
								this.ITransportContext.IIncomingStreamHandler.HandleMessage(labelledStream, remote, genuineConnectionType, connectionGuid, -1, true, null, null, httpServerRequestResult);
							}

							if (binaryReader.ReadByte() != 1)
							{
								// LOG:
								if ( binaryLogWriter != null )
								{
									binaryLogWriter.WriteImplementationWarningEvent("HttpServerConnectionManager.LowLevel_ProcessSenderRequest", LogMessageType.Error,
										GenuineExceptions.Get_Debugging_GeneralWarning("The invocation request must not contain more than one message."),
										GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
										"The invocation request must not contain more than one message.");
								}
							}

							// if there is a response, serialize it
							outputStream = this.LowLevel_CreateStreamWithHeader(HttpPacketType.Usual, sequenceNo, remote);
							Message message = this._invocation[connectionGuid] as Message;
							MessageCoder.FillInLabelledStream(message, null, null, outputStream,
								bufferKeeper.Buffer, (int) this.ITransportContext.IParameterProvider[GenuineParameter.HttpRecommendedPacketSize]);
						}
						finally
						{
							this._invocation.Remove(connectionGuid);
						}
						break;
				}
			}

			// report back to the client
			Stream finalStream = outputStream;
			this.LowLevel_SendStream(httpServerRequestResult.HttpContext, false, null, true, ref finalStream, httpServerConnection);
		}

		/// <summary>
		/// Creates and returns the stream with a written response header.
		/// </summary>
		/// <param name="httpPacketType">The type of the packet.</param>
		/// <param name="sequenceNo">The sequence number.</param>
		/// <param name="remote">The HostInformation of the remote host.</param>
		/// <returns>The stream with a written response header.</returns>
		public GenuineChunkedStream LowLevel_CreateStreamWithHeader(HttpPacketType httpPacketType, int sequenceNo, HostInformation remote)
		{
			GenuineChunkedStream output = new GenuineChunkedStream(false);
			BinaryWriter binaryWriter = new BinaryWriter(output);
			HttpMessageCoder.WriteResponseHeader(binaryWriter, remote.ProtocolVersion, this.ITransportContext.ConnectionManager.Local.Uri, sequenceNo, httpPacketType, remote.LocalHostUniqueIdentifier);
			return output;
		}

		/// <summary>
		/// Sends the response to the remote host.
		/// </summary>
		/// <param name="httpContext">The http context.</param>
		/// <param name="listener">True if it's a listener.</param>
		/// <param name="httpServerConnection">The connection containing CLSS.</param>
		/// <param name="applyClss">Indicates whether it is necessary to apply Connection Level Security Session.</param>
		/// <param name="content">The content being sent to the remote host.</param>
		/// <param name="httpServerConnectionForDebugging">The connection that will be mentioned in the debug records.</param>
		public void LowLevel_SendStream(HttpContext httpContext, bool listener, HttpServerConnection httpServerConnection, bool applyClss, ref Stream content, HttpServerConnection httpServerConnectionForDebugging)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			try
			{
				if (applyClss)
				{
					// detect clss
					SecuritySession clss = null;
					if (httpServerConnection != null && listener && httpServerConnection.Listener_SecuritySession != null && httpServerConnection.Listener_SecuritySession.IsEstablished)
						clss = httpServerConnection.Listener_SecuritySession;
					if (httpServerConnection != null && ! listener && httpServerConnection.Sender_SecuritySession != null && httpServerConnection.Sender_SecuritySession.IsEstablished)
						clss = httpServerConnection.Sender_SecuritySession;

					// apply clss
					if (clss != null)
					{
						GenuineChunkedStream encryptedContent = new GenuineChunkedStream(false);
						clss.Encrypt(content, encryptedContent);
						content = encryptedContent;
					}
				}

#if DEBUG
				Debug.Assert(content.CanSeek);
				Debug.Assert(content.Length >= 0);
#endif

				// prepare the response
				HttpResponse response = httpContext.Response;
				response.ContentType = "application/octet-stream";
				response.StatusCode = 200;
				response.StatusDescription = "OK";
				int contentLength = (int) content.Length;
				response.AppendHeader("Content-Length", contentLength.ToString() );

				// write the response
				Stream responseStream = response.OutputStream;
				GenuineUtility.CopyStreamToStream(content, responseStream, contentLength);
				this.ITransportContext.ConnectionManager.IncreaseBytesSent(contentLength);

#if DEBUG
				// the content must end up here
				Debug.Assert(content.ReadByte() == -1);
#endif

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.LowLevelTransport] > 0 )
				{
					bool writeContent = binaryLogWriter[LogCategory.LowLevelTransport] > 1;
					GenuineChunkedStream copiedContent = null;
					if (writeContent)
					{
						copiedContent = new GenuineChunkedStream(false);
						GenuineUtility.CopyStreamToStream(content, copiedContent);
					}

					binaryLogWriter.WriteTransportContentEvent(LogCategory.LowLevelTransport, "HttpServerConnectionManager.LowLevel_SendStream",
						LogMessageType.LowLevelTransport_AsyncSendingInitiating, null, null, httpServerConnectionForDebugging.Remote,
						copiedContent,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, httpServerConnectionForDebugging.DbgConnectionId,
						(int) content.Length, null, null, null,
						"Response is sent back to the client. Buffer: {0}; BufferOutput: {1}; Charset: {2}; ContentEncoding: {3}; ContentType: {4}; IsClientConnected: {5}; StatusCode: {6}; StatusDescription: {7}; SuppressContent: {8}; Content-Length: {9}. Connection: {10}.",
						response.Buffer, response.BufferOutput, response.Charset,
						response.ContentEncoding, response.ContentType, response.IsClientConnected,
						response.StatusCode, response.StatusDescription, response.SuppressContent,
						contentLength, listener ? "LISTENER" : "SENDER");
				}
			}
			catch(Exception ex)
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpServerConnectionManager.LowLevel_SendStream",
						LogMessageType.MessageIsSentAsynchronously, ex, null, httpServerConnectionForDebugging.Remote, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
						httpServerConnectionForDebugging.DbgConnectionId, 0, 0, 0, null, null, null, null,
						"Error occurred while sending a response.");
				}
				throw;
			}
		}

		/// <summary>
		/// Sends the response to the remote host.
		/// </summary>
		/// <param name="httpContext">The http context.</param>
		public void LowLevel_ReportServerError(HttpContext httpContext)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			try
			{
				// prepare the response
				HttpResponse response = httpContext.Response;
				response.ContentType = "application/octet-stream";
				response.StatusCode = 409;
				response.StatusDescription = "Conflict";
				response.AppendHeader ( "Content-Length", "0" );
			}
			catch (Exception ex)
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpServerConnectionManager.LowLevel_ReportServerError",
						LogMessageType.Error, ex, null, null, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
						-1, 0, 0, 0, null, null, null, null,
						"Error occurred while sending 409/conflict error.");
				}
			}
		}


		#endregion

		#region -- Pool management -----------------------------------------------------------------

		/// <summary>
		/// The message registrator.
		/// </summary>
		private IMessageRegistrator _iMessageRegistrator = new MessageRegistratorWithLimitedTime();

		/// <summary>
		/// The set of persistent connections (with CLSS and message queues): Uri -> connection.
		/// </summary>
		private PersistentConnectionStorage _persistent = new PersistentConnectionStorage();

		/// <summary>
		/// The set of response to messages received via invocation connections (connection Id => Message or a null reference).
		/// </summary>
		private Hashtable _invocation = Hashtable.Synchronized(new Hashtable());

		/// <summary>
		/// Sends the packet with empty content and specified type of packet to the remote host
		/// through the listening connection.
		/// </summary>
		/// <param name="httpServerConnection">The connection.</param>
		/// <param name="httpPacketType">The type of packet.</param>
		private void Pool_SendThroughListener(HttpServerConnection httpServerConnection, HttpPacketType httpPacketType)
		{
			lock (httpServerConnection.Listener_Lock)
			{
				if ( httpServerConnection.Listener == null )
					return ;

				try
				{
					Stream outputStream = this.LowLevel_CreateStreamWithHeader(httpPacketType, httpServerConnection.Listener_SequenceNo, httpServerConnection.Remote);
					this.LowLevel_SendStream(httpServerConnection.Listener.HttpContext, true, httpServerConnection, true, ref outputStream, httpServerConnection);
				}
				finally
				{
					httpServerConnection.Listener.Complete(false);
					httpServerConnection.Listener = null;
				}
			}
		}

		#endregion

		#region -- Resource releasing --------------------------------------------------------------

		private PersistentConnectionStorage.ProcessConnectionEventHandler _releaseConnections_InspectPersistentConnections;
		private class ReleaseConnections_Parameters
		{
			public ArrayList FailedConnections;
			public HostInformation HostInformation;
		}

		/// <summary>
		/// Finds connections to be released.
		/// </summary>
		/// <param name="httpServerConnectionAsObject">The connection.</param>
		/// <param name="releaseConnections_ParametersAsObject">Stuff to make decisions and to save the results.</param>
		private void ReleaseConnections_InspectPersistentConnections(object httpServerConnectionAsObject, object releaseConnections_ParametersAsObject)
		{
			HttpServerConnection httpServerConnection = (HttpServerConnection) httpServerConnectionAsObject;
			ReleaseConnections_Parameters releaseConnections_Parameters = (ReleaseConnections_Parameters) releaseConnections_ParametersAsObject;

			if (releaseConnections_Parameters.HostInformation != null && httpServerConnection.Remote != releaseConnections_Parameters.HostInformation)
				return ;

			releaseConnections_Parameters.FailedConnections.Add(httpServerConnection);
		}

		/// <summary>
		/// Closes the specified connections to the remote host and releases acquired resources.
		/// </summary>
		/// <param name="hostInformation">Host information.</param>
		/// <param name="genuineConnectionType">What kind of connections will be affected by this operation.</param>
		/// <param name="reason">The reason of resource releasing.</param>
		public override void ReleaseConnections(HostInformation hostInformation, GenuineConnectionType genuineConnectionType, Exception reason)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			ArrayList connectionsToClose = new ArrayList();

			using (new WriterAutoLocker(this._disposeLock))
			{
				if (this._disposed)
					return ;

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpServerConnectionManager.ReleaseConnections",
						LogMessageType.ReleaseConnections, reason, null, hostInformation, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, -1, 0, 0, 0, Enum.Format(typeof(GenuineConnectionType), genuineConnectionType, "g"), null, null, null,
						"Connections \"{0}\" will be terminated.", Enum.Format(typeof(GenuineConnectionType), genuineConnectionType, "g"), null);
				}

				// persistent
				if ( (genuineConnectionType & GenuineConnectionType.Persistent) != 0 )
				{
					// persistent
					ReleaseConnections_Parameters releaseConnections_Parameters = new ReleaseConnections_Parameters();
					releaseConnections_Parameters.FailedConnections = connectionsToClose;
					releaseConnections_Parameters.HostInformation = hostInformation;

					this._persistent.InspectAllConnections(this._releaseConnections_InspectPersistentConnections, releaseConnections_Parameters);
				}

				// close connections
				foreach (HttpServerConnection nextHttpServerConnection in connectionsToClose)
				{
					lock (nextHttpServerConnection.Listener_Lock)
					{
						// LOG:
						if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
						{
							binaryLogWriter.WriteEvent(LogCategory.Connection, "HttpClientConnectionManager.ReleaseConnections",
								LogMessageType.ConnectionShuttingDown, reason, null, nextHttpServerConnection.Remote, null,
								GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
								nextHttpServerConnection.DbgConnectionId, 0, 0, 0, null, null, null, null,
								"The connection is being shut down manually.");
						}

						nextHttpServerConnection.SignalState(GenuineEventType.GeneralConnectionClosed, reason, null);

						this._persistent.Remove(nextHttpServerConnection.Remote.Uri, nextHttpServerConnection.ConnectionName);
						if (nextHttpServerConnection.Listener != null)
							this.Pool_SendThroughListener(nextHttpServerConnection, HttpPacketType.ClosedManually);
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
			string ignored;
			uri = GenuineUtility.Parse(uri, out ignored);

			return this._persistent.GetAll(uri);
		}

		/// <summary>
		/// Releases all resources.
		/// </summary>
		/// <param name="reason">The reason of disposing.</param>
		public override void InternalDispose(Exception reason)
		{
			this.ReleaseConnections(null, GenuineConnectionType.All, reason);
		}

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
			public ArrayList SendPingTo = new ArrayList();
			public ArrayList ExpiredConnections = new ArrayList();

			public int CloseListenerConnectionsAfter;
			public int Now;
		}

		/// <summary>
		/// Finds connections to be released.
		/// </summary>
		/// <param name="httpServerConnectionAsObject">The connection.</param>
		/// <param name="parametersAsObject">Stuff to make decisions and to save the results.</param>
		private void Internal_TimerCallback_InspectPersistentConnections(object httpServerConnectionAsObject, object parametersAsObject)
		{
			HttpServerConnection httpServerConnection = (HttpServerConnection) httpServerConnectionAsObject;
			Internal_TimerCallback_Parameters parameters = (Internal_TimerCallback_Parameters) parametersAsObject;

			lock (httpServerConnection.Remote.PersistentConnectionEstablishingLock)
			{
				if (GenuineUtility.IsTimeoutExpired(httpServerConnection.ShutdownTime, parameters.Now))
					parameters.ExpiredConnections.Add(httpServerConnection);
				if (httpServerConnection.Listener != null && GenuineUtility.IsTimeoutExpired(httpServerConnection.Listener_Opened + parameters.CloseListenerConnectionsAfter, parameters.Now))
					parameters.SendPingTo.Add(httpServerConnection);
			}
		}

		/// <summary>
		/// Closes expired connections and sends ping via inactive connections.
		/// </summary>
		/// <param name="ignored">Ignored.</param>
		private void Internal_TimerCallback(object ignored)
		{
			Internal_TimerCallback_Parameters internal_TimerCallback_Parameters = new Internal_TimerCallback_Parameters();
			internal_TimerCallback_Parameters.ExpiredConnections = new ArrayList();
			internal_TimerCallback_Parameters.SendPingTo = new ArrayList();
			internal_TimerCallback_Parameters.CloseListenerConnectionsAfter = GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.ClosePersistentConnectionAfterInactivity]);
			internal_TimerCallback_Parameters.Now = GenuineUtility.TickCount;

			// go through the pool and close all expired connections
			this._persistent.InspectAllConnections(this._internal_TimerCallback_InspectPersistentConnections, internal_TimerCallback_Parameters);

			// send ping to expired
			foreach (HttpServerConnection httpServerConnection in internal_TimerCallback_Parameters.SendPingTo)
				this.Pool_SendThroughListener(httpServerConnection, HttpPacketType.ListenerTimedOut);

			// close expired connections
			foreach (HttpServerConnection httpServerConnection in internal_TimerCallback_Parameters.ExpiredConnections)
			{
				// just remove it silently
				this._persistent.Remove(httpServerConnection.Remote.Uri, httpServerConnection.ConnectionName);
				if (httpServerConnection.Listener != null)
					this.Pool_SendThroughListener(httpServerConnection, HttpPacketType.ClosedManually);
			}
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

	}
}
