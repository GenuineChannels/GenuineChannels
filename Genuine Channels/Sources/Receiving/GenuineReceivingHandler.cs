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
using System.Threading;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Principal;
using System.Text;
using System.Web;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Serialization;

using Belikov.GenuineChannels.BroadcastEngine;
using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.DirectExchange;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.GenuineHttp;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Parameters;
using Belikov.GenuineChannels.Security;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Utilities;

namespace Belikov.GenuineChannels.Receiving
{
	/// <summary>
	/// Manages a number of message handlers processing incoming messages: 
	/// wait cells, multiple response catchers, custom stream handlers, 
	/// broadcast court interceptors, IClientChannelSinks and 
	/// sync response catchers.
	/// </summary>
	public class GenuineReceivingHandler : MarshalByRefObject, IIncomingStreamHandler, ITimerConsumer
	{
		/// <summary>
		/// Constructs an instance of the GenuineReceivingHandler class.
		/// </summary>
		/// <param name="iTransportContext">Transport context.</param>
		/// <param name="defaultTransportUser">Default transport user.</param>
		public GenuineReceivingHandler(ITransportContext iTransportContext, ITransportUser defaultTransportUser)
		{
			this.ITransportContext = iTransportContext;
			this.DefaultTransportUser = defaultTransportUser;
			this._waitCallback_InternalExecuteMessagewaitCallback = new WaitCallback(this.InternalExecuteMessage);
			TimerProvider.Attach(this);
		}

		/// <summary>
		/// Callback invoking InternalExecuteMessage method.
		/// </summary>
		private WaitCallback _waitCallback_InternalExecuteMessagewaitCallback;

		/// <summary>
		/// Transport Context.
		/// </summary>
		public readonly ITransportContext ITransportContext;

		/// <summary>
		/// The default transport user.
		/// </summary>
		public readonly ITransportUser DefaultTransportUser;

		/// <summary>
		/// All messages with such reply id will be ignored.
		/// </summary>
		public const int PING_MESSAGE_REPLYID = -3;

		/// <summary>
		/// Processes incoming requests and responses.
		/// </summary>
		/// <param name="stream">The stream containing a request or a response.</param>
		/// <param name="remote">The remote host.</param>
		/// <param name="genuineConnectionType">The type of the connection.</param>
		/// <param name="connectionName">Connection id to send a response through.</param>
		/// <param name="dbgConnectionId">The identifier of the connection, which is used for debugging purposes only.</param>
		/// <param name="useThisThread">True to invoke the target in the current thread.</param>
		/// <param name="iMessageRegistrator">The message registrator.</param>
		/// <param name="connectionLevelSecuritySession">Connection Level Security Session.</param>
		/// <param name="httpServerRequestResult">The HTTP request through which the message was received.</param>
		/// <returns>True if it's a one-way message.</returns>
		public bool HandleMessage(Stream stream, HostInformation remote, GenuineConnectionType genuineConnectionType, string connectionName, int dbgConnectionId, bool useThisThread, IMessageRegistrator iMessageRegistrator, SecuritySession connectionLevelSecuritySession, HttpServerRequestResult httpServerRequestResult)
		{
			// read the Security Session name
			BinaryReader binaryReader = new BinaryReader(stream);
			string sessionName = binaryReader.ReadString();
			Message message = null;
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			// and decode the packet
			SecuritySession securitySession = remote.GetSecuritySession(sessionName, this.ITransportContext.IKeyStore);
			if (securitySession == null)
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Security] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Security, "GenuineReceivingHandler.HandleMessage", 
						LogMessageType.SecuritySessionApplied, GenuineExceptions.Get_Security_ContextNotFound(sessionName), 
						message, remote, 
						null, GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
						securitySession, sessionName, dbgConnectionId,
						0, 0, 0, null, null, null, null,
						"The requested Security Session can not be constructed or established. The name of Security Session: {0}.", 
						sessionName);
				}

				this.ITransportContext.IGenuineEventProvider.Fire(new GenuineEventArgs(GenuineEventType.SecuritySessionWasNotFound, GenuineExceptions.Get_Security_ContextNotFound(sessionName),
					remote, null));

				return true;
			}

			// decode the stream and roll back if it was a Security Session's message
			stream = securitySession.Decrypt(stream);
			if (stream == null)
				return true;

			// read the message
			message = MessageCoder.Deserialize(stream, sessionName);
			message.ConnectionName = connectionName;
			message.SecuritySessionParameters._connectionName = connectionName;
			message.SecuritySessionParameters._genuineConnectionType = genuineConnectionType;
			message.Sender = remote;
			message.ITransportContext = this.ITransportContext;
			message.ConnectionLevelSecuritySession = connectionLevelSecuritySession;
			message.HttpServerRequestResult = httpServerRequestResult;

			// LOG:
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Security] > 0 )
			{
				binaryLogWriter.WriteEvent(LogCategory.Security, "GenuineReceivingHandler.HandleMessage", 
					LogMessageType.SecuritySessionApplied, null, message, remote, 
					null, GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
					securitySession, sessionName, dbgConnectionId,
					0, 0, 0, null, null, null, null,
					"The Security Session has been used for decrypting the message.");
			}

			// LOG:
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
			{
				bool readContent = this.ITransportContext.BinaryLogWriter[LogCategory.MessageProcessing] > 1;
				if (readContent)
				{
					GenuineChunkedStream streamClone = new GenuineChunkedStream();
					GenuineUtility.CopyStreamToStream(message.Stream, streamClone);
					message.Stream = streamClone;
				}

				binaryLogWriter.WriteMessageCreatedEvent("GenuineReceivingHandler.HandleMessage", 
					LogMessageType.MessageReceived, null, message, message.ReplyToId > 0, remote, 
					readContent ? message.Stream : null,
					message.ITransportHeaders[Message.TransportHeadersInvocationTarget] as string, message.ITransportHeaders[Message.TransportHeadersMethodName] as string,
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, connectionName, dbgConnectionId,
					connectionLevelSecuritySession == null ? -1 : connectionLevelSecuritySession.SecuritySessionId,
					connectionLevelSecuritySession == null ? null : connectionLevelSecuritySession.Name,
					securitySession == null ? -1 : securitySession.SecuritySessionId,
					securitySession.Name,
					"The message has been received.");
			}

			if (message.ReplyToId == PING_MESSAGE_REPLYID)
				return true;

			if (iMessageRegistrator != null && iMessageRegistrator.WasRegistered(remote.Uri, message.MessageId, message.ReplyToId))
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
					binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "GenuineReceivingHandler.HandleMessage", 
						LogMessageType.MessageDispatched, GenuineExceptions.Get_Debugging_GeneralWarning("The message has been already processed."), message, remote, 
						null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, securitySession, securitySession.Name,
						dbgConnectionId,
						GenuineUtility.TickCount, 0, message.SeqNo, null, null, null, null,
						"The message has been already processed. Therefore, this message is ignored.");

				return true;
			}

			// if it's a response, then direct the message to the response handler
			if (message.ReplyToId > 0)
			{
				message.IsOneWay = true;
				IResponseProcessor iResponseProcessor = GenuineReceivingHandler._responseHandlers[message.ReplyToId] as IResponseProcessor;

				// nothing waits for this request
				if (iResponseProcessor == null)
					return true;

				// 2.5.1: set the answer flag
				if (iResponseProcessor.Message != null)
					iResponseProcessor.Message.HasBeenAsnwered = true;

				if (iResponseProcessor.IsShortInProcessing)
				{
					iResponseProcessor.ProcessRespose(message);

#if TRIAL
#else
					if (iResponseProcessor.IsExpired(GenuineUtility.TickCount))
						GenuineReceivingHandler._responseHandlers.Remove(message.ReplyToId);
#endif

					return true;
				}
			}

			// take care about the thread and call context
			if (useThisThread)
				InternalExecuteMessage(message);
			else
				GenuineThreadPool.QueueUserWorkItem(this._waitCallback_InternalExecuteMessagewaitCallback, message, false);

			return message.IsOneWay;
		}


		/// <summary>
		/// Processes the message.
		/// </summary>
		/// <param name="messageAsObject">An instance of the Message class.</param>
		private void InternalExecuteMessage(object messageAsObject)
		{
			Message message = null;

			try
			{
				message = (Message) messageAsObject;

				if (message.ConnectionLevelSecuritySession == null)
					HandleMessage_AfterCLSS(message);
				else
					message.ConnectionLevelSecuritySession.Invoke(message, true);
			}
			catch(Exception ex)
			{
				// LOG:
				BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
					binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "GenuineReceivingHandler.InternalExecuteMessage", 
						LogMessageType.SecuritySessionApplied, ex, message, message.Sender, 
						null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, message.ConnectionLevelSecuritySession, null,
						-1, 0, 0, 0, null, null, null, null,
						"An exception occurred while executing the request.");
			}
		}

		/// <summary>
		/// Processes the message.
		/// </summary>
		/// <param name="message">The message.</param>
		public void HandleMessage_AfterCLSS(Message message)
		{
			SecuritySession securitySession = message.Sender.GetSecuritySession(message.SecuritySessionParameters.Name, this.ITransportContext.IKeyStore);
			if (securitySession == null)
			{
				// LOG:
				BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
					binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "GenuineReceivingHandler.HandleMessage_AfterCLSS", 
						LogMessageType.SecuritySessionApplied, GenuineExceptions.Get_Security_ContextNotFound(message.SecuritySessionParameters.Name), 
						message, message.Sender, 
						null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, message.SecuritySessionParameters.Name,
						-1, 0, 0, 0, null, null, null, null,
						"The message can not be processed due to the raised exception.");
				return ;
			}

			// initialize all the necessary info
			using(new ThreadDataSlotKeeper(OccupiedThreadSlots.CurrentMessage, message))
			{
				if (message.HttpServerRequestResult != null && 
					(bool) message.ITransportContext.IParameterProvider[GenuineParameter.HttpAuthentication] && 
					message.HttpServerRequestResult.IPrincipal != null && 
					message.HttpServerRequestResult.IPrincipal.Identity is WindowsIdentity && 
					((WindowsIdentity) message.HttpServerRequestResult.IPrincipal.Identity).Token != IntPtr.Zero )
				{
					// impersonate authenticated security context
					WindowsImpersonationContext windowsImpersonationContext = null;
					IPrincipal previousPrincipal = null;
					previousPrincipal = Thread.CurrentPrincipal;

					try
					{
						windowsImpersonationContext = ((WindowsIdentity) message.HttpServerRequestResult.IPrincipal.Identity).Impersonate();
						Thread.CurrentPrincipal = message.HttpServerRequestResult.IPrincipal;
						securitySession.Invoke(message, false);
					}
					finally
					{
						Thread.CurrentPrincipal = previousPrincipal;
						windowsImpersonationContext.Undo();
					}
				}
				else
				{
					securitySession.Invoke(message, false);
				}
			}
		}

		/// <summary>
		/// Invokes the target or dispatches the response according to message content.
		/// Throws the exception on any errors.
		/// </summary>
		/// <param name="message">The message being processed.</param>
		public void HandleMessage_Final(Message message)
		{
			// get the specified transport user
			ITransportUser iTransportUser = null;
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			if (message.SecuritySessionParameters.RemoteTransportUser.Length > 0)
				iTransportUser = this._transportUsers[message.SecuritySessionParameters.RemoteTransportUser] as ITransportUser;

			if (iTransportUser == null)
				iTransportUser = this.DefaultTransportUser;

			// direct the response to the response handler
			if (message.ReplyToId > 0)
			{
				IResponseProcessor iResponseProcessor = GenuineReceivingHandler._responseHandlers[message.ReplyToId] as IResponseProcessor;

				// check whether it's OK
				if (iResponseProcessor == null)
				{
#if TRIAL
#else
					GenuineReceivingHandler._responseHandlers.Remove(message.ReplyToId);
#endif

					return ;
				}

				if (! iResponseProcessor.IsExpired(GenuineUtility.TickCount))
					iResponseProcessor.ProcessRespose(message);

#if TRIAL
#else
				if (iResponseProcessor.IsExpired(GenuineUtility.TickCount))
					GenuineReceivingHandler._responseHandlers.Remove(message.ReplyToId);
#endif

				return ;
			}

//			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Security] > 0 )
//				binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "GenuineReceivingHandler.HandleMessage_Final",
//					LogMessageType.MessageDispatched, null, message, message.Recipient, null, 
//					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
//					null, message.SecuritySessionParameters.Name, -1, 0, 0, 0, null, null, null, null,
//					"The message is dispatched.");

			// dispatch it
			switch (message.GenuineMessageType)
			{
				case GenuineMessageType.Ordinary:
					// dispatch the message to the sink
					iTransportUser.HandleMessage(message);
					break;

				case GenuineMessageType.TrueBroadcast:
				case GenuineMessageType.BroadcastEngine:
					// get the string
					BinaryFormatter binaryFormatter = new BinaryFormatter();
					IMessage iMessage = null;

					try
					{
						iMessage = (IMessage) binaryFormatter.Deserialize(message.Stream);
						IMethodReturnMessage ret = null;

						if (message.DestinationMarshalByRef is string)
						{
							// message come via true multicast channel to the specific court
							MarshalByRefObject receiver, sender;

							// get the court info
							CourtCollection.Find( (string) message.DestinationMarshalByRef,
								out receiver, out sender);

							if (receiver == null)
							{
								// message to the unregistered court has been received
								this.ITransportContext.IGenuineEventProvider.Fire(new GenuineEventArgs(GenuineEventType.BroadcastUnknownCourtReceived, null,
									message.Sender, message.DestinationMarshalByRef));

								// LOG: put down the log record
								if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
								{
									binaryLogWriter.WriteEvent(LogCategory.BroadcastEngine, "GenuineReceivingHanlder.HandleMessage_Final",
										LogMessageType.MessageRequestInvoking, GenuineExceptions.Get_Debugging_GeneralWarning("No objects are associated with the \"{0}\" court.", (string) message.DestinationMarshalByRef), 
										message, message.Sender, null,
										GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
										null, null, -1, 0, 0, 0, null, null, null, null,
										"The message is ignored because no objects are associated with the \"{0}\" court.", (string) message.DestinationMarshalByRef);
								}

								return ;
							}

							if (message.ITransportHeaders[Message.TransportHeadersBroadcastSendGuid] is string 
								&& UniqueCallTracer.Instance.WasGuidRegistered( RemotingServices.GetObjectUri(receiver) + (string) message.ITransportHeaders[Message.TransportHeadersBroadcastSendGuid]))
							{
								// LOG: put down the log record
								if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
								{
									binaryLogWriter.WriteEvent(LogCategory.BroadcastEngine, "GenuineReceivingHanlder.HandleMessage_Final",
										LogMessageType.MessageRequestInvoking, GenuineExceptions.Get_Broadcast_CallHasAlreadyBeenMade(), 
										message, message.Sender, null,
										GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
										null, null, -1, 0, 0, 0, null, null, null, null,
										"This message is ignored because this call has been already processed.");
								}

								// this call has been already processed
								ret = new ReturnMessage(GenuineExceptions.Get_Broadcast_CallHasAlreadyBeenMade(), (IMethodCallMessage) iMessage);
							}
							else
							{
								// perform the call
								try
								{
									// LOG: put down the log record
									if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
									{
										binaryLogWriter.WriteEvent(LogCategory.BroadcastEngine, "GenuineReceivingHanlder.HandleMessage_Final",
											LogMessageType.MessageRequestInvoking, null, 
											message, message.Sender, null,
											GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
											null, null, -1, 0, 0, 0, null, null, null, null,
											"The multicast message is being invoked on court \"{0}\".", (string) message.DestinationMarshalByRef);
									}

									ret = RemotingServices.ExecuteMessage(receiver, (IMethodCallMessage) iMessage);
								}
								catch(Exception ex)
								{
									// LOG: put down the log record
									if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
									{
										binaryLogWriter.WriteEvent(LogCategory.BroadcastEngine, "GenuineReceivingHanlder.HandleMessage_Final",
											LogMessageType.MessageRequestInvoking, ex, 
											message, message.Sender, null,
											GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
											null, null, -1, 0, 0, 0, null, null, null, null,
											"An exception occurred while the message was being processed. The exception is dispatched back.");
									}

									ret = new ReturnMessage(ex, (IMethodCallMessage) iMessage);
								}
							}

//							if (sender != null)
//								message.Sender = this.ITransportContext.KnownHosts[GenuineUtility.FetchChannelUriFromMbr(sender)];

							// set correct remote guid
							ITransportContext iTransportContext = this.ITransportContext;
							if (sender != null)
							{
								string uri;
								GenuineUtility.FetchChannelUriFromMbr(sender, out uri, out iTransportContext);
								if (uri == null || iTransportContext == null)
									iTransportContext = this.ITransportContext;
								else
									message.Sender = iTransportContext.KnownHosts[uri];
							}

							Message reply = CreateResponseToBroadcastMessage(message, ret, binaryFormatter);
							reply.DestinationMarshalByRef = GenuineUtility.FetchDotNetUriFromMbr(receiver);

							// LOG: put down the log record
							if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
								this.PutDownRecordAboutResponseCreation(binaryLogWriter, reply, ret, "The response to the \"true\" broadcast message has been created.");

							iTransportContext.ConnectionManager.Send(reply);
						}
						else
						{
							MarshalByRefObject marshalByRefObject = null;

							if ((message.DestinationMarshalByRef is MarshalByRefObject))
								marshalByRefObject = (MarshalByRefObject) message.DestinationMarshalByRef;
							else
							{
								Exception exception = GenuineExceptions.Get_Debugging_GeneralWarning("Can't process the object with type {0}", message.DestinationMarshalByRef == null ? "<null!>" : message.DestinationMarshalByRef.GetType().ToString());

								if (binaryLogWriter != null)
								{
									binaryLogWriter.WriteImplementationWarningEvent("GenuineReceivingHandler.HandleMessage_Final",
										LogMessageType.CriticalError, exception,
										GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
										"The response with the {0} type cannot be processed.", message.DestinationMarshalByRef.GetType());
								}

								throw exception;
							}

							if (message.ITransportHeaders[Message.TransportHeadersBroadcastSendGuid] is string 
								&& UniqueCallTracer.Instance.WasGuidRegistered( RemotingServices.GetObjectUri( marshalByRefObject ) + (string) message.ITransportHeaders[Message.TransportHeadersBroadcastSendGuid]))
							{
								Exception exception = GenuineExceptions.Get_Broadcast_CallHasAlreadyBeenMade();

								// LOG: put down the log record
								if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
								{
									binaryLogWriter.WriteEvent(LogCategory.BroadcastEngine, "GenuineReceivingHanlder.HandleMessage_Final",
										LogMessageType.MessageRequestInvoking, exception, 
										message, message.Sender, null,
										GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
										null, null, -1, 0, 0, 0, null, null, null, null,
										"This message is ignored because this call has been already processed.");
								}

								ret = new ReturnMessage(exception, (IMethodCallMessage) iMessage);
							}
							else
							{
								// LOG: put down the log record
								if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
								{
									binaryLogWriter.WriteEvent(LogCategory.BroadcastEngine, "GenuineReceivingHanlder.HandleMessage_Final",
										LogMessageType.MessageRequestInvoking, null, 
										message, message.Sender, null,
										GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
										null, null, -1, 0, 0, 0, null, null, null, null,
										"The usual broadcast message is being invoked.");
								}

								ret = RemotingServices.ExecuteMessage(marshalByRefObject, (IMethodCallMessage) iMessage);
							}

							Message reply = CreateResponseToBroadcastMessage(message, ret, binaryFormatter);

							// LOG: put down the log record
							if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
								this.PutDownRecordAboutResponseCreation(binaryLogWriter, reply, ret, "The response to the usual broadcast invocation has been created.");

							reply.ITransportHeaders[Message.TransportHeadersMbrUriName] = message.ITransportHeaders[Message.TransportHeadersMbrUriName];
							this.ITransportContext.ConnectionManager.Send(reply);
						}
					}
					catch(Exception ex)
					{
						try
						{
							// LOG: put down the log record
							if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
							{
								binaryLogWriter.WriteEvent(LogCategory.BroadcastEngine, "GenuineReceivingHanlder.HandleMessage_Final",
									LogMessageType.MessageRequestInvoking, ex, 
									message, message.Sender, null,
									GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
									null, null, -1, 0, 0, 0, null, null, null, null,
									"An exception occurred while the usual broadcast message was being processed. The exception is dispatched back.");
							}

							ReturnMessage returnMessage = new ReturnMessage(ex, (IMethodCallMessage) iMessage);
							Message reply = CreateResponseToBroadcastMessage(message, returnMessage, binaryFormatter);

							// LOG: put down the log record
							if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
								this.PutDownRecordAboutResponseCreation(binaryLogWriter, reply, returnMessage, "The response to the usual broadcast invocation has been created.");

							this.ITransportContext.ConnectionManager.Send(reply);
						}
						catch(Exception internalException)
						{
							// it's a destiny not to reply to this message

							// LOG: put down the log record
							if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
							{
								binaryLogWriter.WriteEvent(LogCategory.BroadcastEngine, "GenuineReceivingHanlder.HandleMessage_Final",
									LogMessageType.MessageRequestInvoking, internalException, 
									message, message.Sender, null,
									GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
									null, null, -1, 0, 0, 0, null, null, null, null,
									"An exception occurred again while the previous exception was being serialized. This request is ignored.");
							}
						}
					}
					break;

				case GenuineMessageType.ExternalStreamConsumer:
					// direct the response to response handler
					if (message.ReplyToId > 0)
					{
						IResponseProcessor iResponseProcessor = GenuineReceivingHandler._responseHandlers[message.ReplyToId] as IResponseProcessor;
						if (! iResponseProcessor.IsExpired(GenuineUtility.TickCount))
							iResponseProcessor.ProcessRespose(message);

#if TRIAL
#else
						if (iResponseProcessor.IsExpired(GenuineUtility.TickCount))
							GenuineReceivingHandler._responseHandlers.Remove(message.ReplyToId);
#endif

						break;
					}

					string entryName = message.DestinationMarshalByRef as string;
					if (entryName != null)
					{
						if (message.IsOneWay)
						{
							this.ITransportContext.DirectExchangeManager.HandleRequest(message);
							return ;
						}

						Stream response = null;
						Message reply;
						try
						{
							// LOG: put down the log record
							if ( binaryLogWriter != null && binaryLogWriter[LogCategory.DXM] > 0 )
							{
								binaryLogWriter.WriteEvent(LogCategory.BroadcastEngine, "GenuineReceivingHanlder.HandleMessage_Final",
									LogMessageType.MessageRequestInvoking, null, 
									message, message.Sender, null,
									GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
									null, null, -1, 0, 0, 0, null, null, null, null,
									"The DXM message is being dispatched to the \"{0}\" entry.", entryName);
							}

							response = this.ITransportContext.DirectExchangeManager.HandleRequest(message);
							if (response == null)
								response = Stream.Null;
							reply = new Message(message, new TransportHeaders(), response);

							// LOG: put down the log record
							if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
							{
								if (this.ITransportContext.BinaryLogWriter[LogCategory.MessageProcessing] > 1)
								{
									// make a copy of the source stream
									GenuineChunkedStream copy = new GenuineChunkedStream(false);
									GenuineUtility.CopyStreamToStream(reply.Stream, copy);
									reply.Stream = copy;
								}

								binaryLogWriter.WriteMessageCreatedEvent("AsyncSinkStackResponseProcessor.ProcessRespose",
									LogMessageType.MessageCreated, null, reply, false, reply.Recipient, 
									this.ITransportContext.BinaryLogWriter[LogCategory.MessageProcessing] > 1 ? reply.Stream : null, 
									entryName, entryName,
									GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, -1, -1, null, -1, null,
									"A response to DXM request has been created.");

								message.ITransportHeaders[Message.TransportHeadersInvocationTarget] = entryName;
								message.ITransportHeaders[Message.TransportHeadersMethodName] = entryName;

								binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "AsyncSinkStackResponseProcessor.ProcessRespose",
									LogMessageType.MessageRequestInvoked, null, reply, reply.Recipient, null, 
									GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null, -1, 
									GenuineUtility.TickCount, 0, message.SeqNo, null, null, null, null,
									"The DXM invocation has been completed.");
							}

							message.ITransportContext.ConnectionManager.Send(reply);
						}
						catch(Exception ex)
						{
							// LOG: put down the log record
							if ( binaryLogWriter != null && binaryLogWriter[LogCategory.DXM] > 0 )
							{
								binaryLogWriter.WriteEvent(LogCategory.BroadcastEngine, "GenuineReceivingHanlder.HandleMessage_Final",
									LogMessageType.MessageRequestInvoking, ex, 
									message, message.Sender, null,
									GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
									null, null, -1, 0, 0, 0, null, null, null, null,
									"The invocation of the DXM message directed to the \"{0}\" entry has resulted in exception.", entryName);
							}

							// return this exception as a result
							BinaryFormatter binaryFormatter2 = new BinaryFormatter();
							GenuineChunkedStream serializedException = new GenuineChunkedStream(false);
							binaryFormatter2.Serialize(serializedException, ex);

							reply = new Message(message, new TransportHeaders(), serializedException);
							reply.ContainsSerializedException = true;

							// LOG: put down the log record
							if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
							{
								string invocationTarget = entryName;
								string methodName = null;
								object service = this.ITransportContext.DirectExchangeManager.GetServerService(entryName);
								if (service != null)
									methodName = service.GetType().FullName;

								binaryLogWriter.WriteMessageCreatedEvent("GenuineReceivingHanlder.HandleMessage_Final",
									LogMessageType.MessageCreated, null, reply, false, reply.Recipient, 
									this.ITransportContext.BinaryLogWriter[LogCategory.MessageProcessing] > 1 ? reply.Stream : null, 
									invocationTarget, methodName,
									GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, -1, -1, null, -1, null,
									"The response to DXM message directed to the \"{0}\" court has been created.", entryName);

								reply.ITransportHeaders[Message.TransportHeadersInvocationTarget] = invocationTarget;
								reply.ITransportHeaders[Message.TransportHeadersMethodName] = methodName;
							}

							this.ITransportContext.ConnectionManager.Send(reply);
						}
					}
					break;

				default:
					// LOG:
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
						binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "GenuineReceivingHandler.HandleMessage_Final", 
							LogMessageType.MessageDispatched, GenuineExceptions.Get_Debugging_GeneralWarning("Unknown message type."), 
							message, message.Sender, 
							null,
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, message.SecuritySessionParameters.Name,
							-1, 0, 0, 0, null, null, null, null,
							"A message has an unknown type and can not be processed. Type: {0}", (int) message.GenuineMessageType);
					break;
			}
		}

		/// <summary>
		/// Puts down a record describing the creation of the response message.
		/// </summary>
		/// <param name="binaryLogWriter">The binary log writer.</param>
		/// <param name="reply">The response message.</param>
		/// <param name="ret">The response.</param>
		/// <param name="message">The text of the message.</param>
		/// <param name="parameters">Parameters to the message text.</param>
		internal void PutDownRecordAboutResponseCreation(BinaryLogWriter binaryLogWriter, Message reply, IMessage ret, string message, params object[] parameters)
		{
			string invocationTarget = ret.Properties["__Uri"] as string;
			string methodName = BinaryLogWriter.ParseInvocationMethod(ret.Properties["__MethodName"] as string, ret.Properties["__TypeName"] as string);

			binaryLogWriter.WriteMessageCreatedEvent("GenuineReceivingHanlder.PutDownRecordAboutResponseCreation",
				LogMessageType.MessageCreated, null, reply, false, reply.Recipient, 
				this.ITransportContext.BinaryLogWriter[LogCategory.MessageProcessing] > 1 ? reply.Stream : null, 
				invocationTarget, methodName,
				GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, -1, -1, null, -1, null,
				message, parameters);

			reply.ITransportHeaders[Message.TransportHeadersInvocationTarget] = invocationTarget;
			reply.ITransportHeaders[Message.TransportHeadersMethodName] = methodName;
		}

		/// <summary>
		/// Packs and returns a message that contains back response to the broadcast message.
		/// </summary>
		/// <param name="message">The source message.</param>
		/// <param name="returnMessage">IMethodReturnMessage response.</param>
		/// <param name="binaryFormatter">Just to prevent creating addition instance.</param>
		/// <returns>The reply message.</returns>
		private Message CreateResponseToBroadcastMessage(Message message, object returnMessage, BinaryFormatter binaryFormatter)
		{
			// serialize the reply
			GenuineChunkedStream stream = new GenuineChunkedStream(false);
			binaryFormatter.Serialize(stream, returnMessage);

			// and wrap it into the message
			return new Message(message, new TransportHeaders(), stream);
		}

		/// <summary>
		/// Contains a set of response handlers awaiting for the messages with the specific
		/// reply id.
		/// </summary>
		private static Hashtable _responseHandlers = Hashtable.Synchronized(new Hashtable());

		/// <summary>
		/// Registers a response processor that waits for messages containing a response to the 
		/// message with the specified identifier.
		/// </summary>
		/// <param name="replyId">ID of the source message.</param>
		/// <param name="iResponseProcessor">A response processor instance.</param>
		public void RegisterResponseProcessor(int replyId, IResponseProcessor iResponseProcessor)
		{
			GenuineReceivingHandler._responseHandlers[replyId] = iResponseProcessor;
		}

		/// <summary>
		/// True if there is a handler awaiting for the response to the message with the specified id.
		/// </summary>
		/// <param name="replyId">The id of the source message.</param>
		/// <returns>True if there is a handler awaiting for the response to the message with the specified id.</returns>
		public bool IsHandlerAlive(int replyId)
		{
			IResponseProcessor iResponseProcessor = GenuineReceivingHandler._responseHandlers[replyId] as IResponseProcessor;
			if (iResponseProcessor == null)
				return false;
			return ! iResponseProcessor.IsExpired(GenuineUtility.TickCount);
		}

		/// <summary>
		/// Keeps a set of associative links between strings and transport users.
		/// </summary>
		private Hashtable _transportUsers = Hashtable.Synchronized(new Hashtable());

		/// <summary>
		/// Associates the specified transport user with the specified name.
		/// </summary>
		/// <param name="name">The name to associate the transport user with.</param>
		/// <param name="transportUser">The transport user.</param>
		public void RegisterTransportUser(string name, ITransportUser transportUser)
		{
			this._transportUsers[name] = transportUser;
		}

		/// <summary>
		/// Dispatches the exception to all response processors awaiting something from the specific remote host.
		/// </summary>
		/// <param name="hostInformation">The remote host.</param>
		/// <param name="exception">The exception.</param>
		public void DispatchException(HostInformation hostInformation, Exception exception)
		{
			// LOG:
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
				binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "GenuineReceivingHandler.DispatchException", 
					LogMessageType.ExceptionDispatched, exception, 
					null, hostInformation, 
					null,
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
					-1, 0, 0, 0, null, null, null, null,
					"The exception is being dispatched back to the caller context.");

			Hashtable expiredHandlers = new Hashtable();

			lock (GenuineReceivingHandler._responseHandlers.SyncRoot)
			{
				foreach (DictionaryEntry entry in GenuineReceivingHandler._responseHandlers)
				{
					IResponseProcessor iResponseProcessor = entry.Value as IResponseProcessor;

					// 2.5.2 fix; fixed in 2.5.6 again
					bool hostsEqual = (iResponseProcessor.Remote == hostInformation);
					
					if (! hostsEqual && iResponseProcessor.Remote != null && hostInformation != null)
						hostsEqual = (iResponseProcessor.Remote.Url == hostInformation.Url && hostInformation.Url != null)
							|| (iResponseProcessor.Remote.Uri == hostInformation.Uri && hostInformation.Uri != null);

					if (hostsEqual)
						expiredHandlers[entry.Key] = iResponseProcessor;
				}

				foreach (DictionaryEntry entry in expiredHandlers)
				{
					IResponseProcessor iResponseProcessor = entry.Value as IResponseProcessor;

					// LOG:
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
						binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "GenuineReceivingHandler.DispatchException", 
							LogMessageType.ExceptionDispatched, exception, 
							iResponseProcessor.Message, hostInformation, 
							null,
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
							-1, 0, 0, 0, null, null, null, null,
							"The exception is being dispatched back to the caller context.");

					iResponseProcessor.DispatchException(OperationException.WrapException(exception));

#if TRIAL
#else
					GenuineReceivingHandler._responseHandlers.Remove(entry.Key);
#endif
					
				}
			}
		}

		/// <summary>
		/// Dispatches the exception to the response processor specified by the replyId value.
		/// </summary>
		/// <param name="sourceMessage">The source message.</param>
		/// <param name="exception">The exception</param>
		public void DispatchException(Message sourceMessage, Exception exception)
		{
			// LOG:
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
				binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "GenuineReceivingHandler.DispatchException2", 
					LogMessageType.ExceptionDispatched, exception, 
					sourceMessage, sourceMessage.Recipient, 
					null,
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
					-1, 0, 0, 0, null, null, null, null,
					"The exception is being dispatched back to the caller context.");

			IResponseProcessor iResponseProcessor = GenuineReceivingHandler._responseHandlers[sourceMessage.MessageId] as IResponseProcessor;
			if (iResponseProcessor != null)
			{
				iResponseProcessor.DispatchException(OperationException.WrapException(exception));

#if TRIAL
#else
				if (iResponseProcessor.IsExpired(GenuineUtility.TickCount))
					GenuineReceivingHandler._responseHandlers.Remove(sourceMessage.ReplyToId);
#endif

			}
		}

		/// <summary>
		/// Invokes ScanForExpiredHandlers method in the separate thread.
		/// </summary>
		public void TimerCallback()
		{
			ArrayList expiredResponses = new ArrayList();
			Exception exception = GenuineExceptions.Get_Send_ServerDidNotReply();

			int now = GenuineUtility.TickCount;

			// remove expired elements from the collection
			lock (GenuineReceivingHandler._responseHandlers.SyncRoot)
			{
				foreach (DictionaryEntry entry in GenuineReceivingHandler._responseHandlers)
				{
					IResponseProcessor iResponseProcessor = (IResponseProcessor) entry.Value;
					if (iResponseProcessor.IsExpired(now))
						expiredResponses.Add(entry);
				}
			}

#if TRIAL
#else
			// first, get rid of expired handlers
			foreach (DictionaryEntry entry in expiredResponses)
				GenuineReceivingHandler._responseHandlers.Remove(entry.Key);
#endif

			// then dispatch exceptions
			foreach (DictionaryEntry entry in expiredResponses)
			{
				IResponseProcessor iResponseProcessor = (IResponseProcessor) entry.Value;
				GenuineThreadPool.QueueUserWorkItem(new WaitCallback(iResponseProcessor.DispatchException), exception, false);
			}
		}

	}
}
