/* Genuine Channels product.
 *
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 *
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.IO;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

using Belikov.Common.ThreadProcessing;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Receiving;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Utilities;
using Zyan.SafeDeserializationHelpers;

namespace Belikov.GenuineChannels.BroadcastEngine
{
	/// <summary>
	/// Represents a method which is used to invoke the real proxy asynchronously.
	/// </summary>
	public delegate IMessage InvokeAsyncDelegate(IMessage msg);

	/// <summary>
	/// Sends the specified message to all receivers, collects results and completes broadcast call.
	/// </summary>
	public class ResultCollector : IResponseProcessor, IClientChannelSink, IMessageSink
	{
		/// <summary>
		/// Constructs an instance of the ResultCollector class.
		/// Created object is in "Sending" state by default.
		/// </summary>
		/// <param name="dispatcher">The dispatcher.</param>
		/// <param name="capacity">The expected number of receivers.</param>
		/// <param name="iMessage">The message.</param>
		public ResultCollector(Dispatcher dispatcher, int capacity, IMessage iMessage)
		{
			this._iMessage = iMessage;
			this._dispatcher = dispatcher;
			this.Successful = new Hashtable(capacity);
			this.Failed = new Hashtable(capacity);
		}

		/// <summary>
		/// Releases acquired resources.
		/// </summary>
		public void Dispose()
		{
		}

		private Dispatcher _dispatcher;
		private IMessage _iMessage;
		private string _broadcastGuid = Guid.NewGuid().ToString("N");

		/// <summary>
		/// The message being sent in serialized representation.
		/// </summary>
		private GenuineChunkedStream _messageStream;

		/// <summary>
		/// Indicates whether the second broadcast stage (sending the message via the usual channel(s) to the
		/// failed receivers) was completed.
		/// </summary>
		private bool _secondStagePerformed = false;

		/// <summary>
		/// Represents a keyed collection containing successful results of invocations.
		/// Keys are URIs of the remote hosts, values support the IMethodReturnMessage interface.
		/// </summary>
		public Hashtable Successful;

		/// <summary>
		/// Represents a keyed collection containing unsuccessful results of the invocations.
		/// Keys contain URI, values support the IMethodReturnMessage interface or inherit the Exception class.
		/// </summary>
		public Hashtable Failed;

		/// <summary>
		/// Is set when all replies have arrived.
		/// </summary>
		public ManualResetEvent AllMessagesReceived = new ManualResetEvent(false);

		/// <summary>
		/// Represents a keyed collection containing receivers' uris of receivers that did not
		/// respond to the send message.
		/// Uri {string} => null.
		/// </summary>
		public Hashtable UnrepliedReceivers = Hashtable.Synchronized(new Hashtable());

		/// <summary>
		/// Writes down response came from the remote host.
		/// </summary>
		/// <param name="mbrUri">The uri of the remote MBR.</param>
		/// <param name="returnMessage">The received message or a null reference.</param>
		/// <param name="ex">The exception occurred during sending or receiving.</param>
		public void ParseResult(string mbrUri, IMethodReturnMessage returnMessage, Exception ex)
		{
			// if time-out has expired
			if (this.AllMessagesReceived.WaitOne(0, false))
				return ;

			// to increase or zero the number of consecutive fails
			ReceiverInfo receiverInfo = this._dispatcher.GetReceiverInfo(mbrUri);
			BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;

			// if an exception is received
			Exception exception = null;
			if (ex != null || (returnMessage != null && returnMessage.Exception != null))
			{
				exception = ex != null ? ex : returnMessage.Exception;

				HostInformation dbgRemoteHost = (receiverInfo == null ? null : receiverInfo.DbgRemoteHost);
				string numberOfFails = (receiverInfo == null ? string.Empty : receiverInfo.NumberOfFails.ToString());

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
				{
					binaryLogWriter.WriteBroadcastEngineEvent(LogCategory.BroadcastEngine, "ResultCollector.ParseResult",
						LogMessageType.BroadcastResponseParsed, exception, null, dbgRemoteHost, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, false, this._dispatcher, this, false, receiverInfo,
						numberOfFails, "false",
						"The exception is received in response to the broadcast. NumberOfFails: {0}.", numberOfFails);
				}
			}

			// if it's a message that the invocation was repeated, ignore it
			if (exception is OperationException)
			{
				OperationException operationException = (OperationException) exception;
				if (operationException.OperationErrorMessage.ErrorIdentifier == "GenuineChannels.Exception.Broadcast.CallHasAlreadyBeenMade")
					return ;
			}

			lock (this)
			{
				// if successful response from this object has already been received
				this.UnrepliedReceivers.Remove(mbrUri);

				if (this.Successful.ContainsKey(mbrUri))
					return ;
			}

			lock(this)
			{
				// if an exception is received
				if (exception != null)
				{
					this.Failed[mbrUri] = exception;

					if (receiverInfo != null)
					{
						lock(receiverInfo)
						{
							if (receiverInfo.GeneralBroadcastSender == null && this._dispatcher.MaximumNumberOfConsecutiveFailsToExcludeReceiverAutomatically != 0 &&
								receiverInfo.NumberOfFails >= this._dispatcher.MaximumNumberOfConsecutiveFailsToExcludeReceiverAutomatically)
							{
								// exclude it
								this._dispatcher.Remove(receiverInfo.MbrObject);
							}
						}
					}
				}
				else
				{
					// successful response
					this.Successful[mbrUri] = returnMessage;
					this.Failed.Remove(mbrUri);

					// LOG:
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
					{
						HostInformation dbgRemoteHost = (receiverInfo == null ? null : receiverInfo.DbgRemoteHost);

						binaryLogWriter.WriteBroadcastEngineEvent(LogCategory.BroadcastEngine, "ResultCollector.ParseResult",
							LogMessageType.BroadcastResponseParsed, null, null, dbgRemoteHost, null,
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
							null, null, false, this._dispatcher, this, false, receiverInfo,
							null, "true",
							"The successful response is received.");
					}

					if (receiverInfo != null)
					{
						lock(receiverInfo)
						{
							receiverInfo.NumberOfFails = 0;
						}
					}
				}

				if (this.UnrepliedReceivers.Count == 0)
					this.AllMessagesReceived.Set();
			}
		}

		private BroadcastCallFinishedHandler _broadcastCallFinishedHandler;

		/// <summary>
		/// Initiate the receiving of responses.
		/// Returns true if the invocation has been completed synchronously.
		/// </summary>
		/// <returns>The true value if the invocation has been completed synchronously.</returns>
		public bool StartReceiving()
		{
			bool callIsAsync = false;
			lock (this._dispatcher)
			{
				callIsAsync = this._dispatcher.CallIsAsync;
				this._broadcastCallFinishedHandler = this._dispatcher.BroadcastCallFinishedHandler;
			}

			if (! callIsAsync || this._broadcastCallFinishedHandler == null)
			{
				this.AllMessagesReceived.WaitOne(this._dispatcher.ReceiveResultsTimeOut, false);

				// this call sends messages to failed receivers
				this.WaitUntilReceiversReplyOrTimeOut(null, false);

				// LOG:
				BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
				{
					binaryLogWriter.WriteBroadcastEngineEvent(LogCategory.BroadcastEngine, "ResultCollector.StartReceiving",
						LogMessageType.BroadcastInvocationCompleted, null, null, null, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, false, this._dispatcher, this, false, null,
						null, null,
						"The broadcast invocation is completed.");
				}

				this.AllMessagesReceived.Set();
				return true;
			}

			ThreadPool.RegisterWaitForSingleObject(this.AllMessagesReceived, new WaitOrTimerCallback(this.WaitUntilReceiversReplyOrTimeOut), null, this._dispatcher.ReceiveResultsTimeOut, true);
			return false;
		}

		/// <summary>
		/// Calls the callback when either all receivers reply or wait time elapses.
		/// </summary>
		/// <param name="state">Ignored.</param>
		/// <param name="timeOut">Ignored.</param>
		private void WaitUntilReceiversReplyOrTimeOut(object state, bool timeOut)
		{
			// check whether we have any unreplied true multicast-enabled receivers
			bool unrepliedReceivers = false;
			lock(this)
			{
				if (this.Failed.Count > 0 && ! this._secondStagePerformed)
					foreach ( DictionaryEntry entry in this.Failed )
					{
						// fixed in 2.2 version
						string uri = (string) entry.Key;
						ReceiverInfo receiverInfo = this._dispatcher.GetReceiverInfo(uri);
						int numberOfFails = 0;
						if (receiverInfo != null)
						{
							lock(receiverInfo)
							{
								numberOfFails = ++receiverInfo.NumberOfFails;
							}
						}

						if (this._dispatcher.MaximumNumberOfConsecutiveFailsToExcludeReceiverAutomatically != 0 &&
							numberOfFails >= this._dispatcher.MaximumNumberOfConsecutiveFailsToExcludeReceiverAutomatically)
						{
							this._dispatcher.Remove(uri);
						}

						// set time-out exception
						OperationException operationException = entry.Value as OperationException;
						if (operationException != null && operationException.OperationErrorMessage != null &&
							operationException.OperationErrorMessage.ErrorIdentifier == "GenuineChannels.Exception.Broadcast.RemoteEndPointDidNotReplyForTimeOut")
						{
							unrepliedReceivers = true;
							break;
						}
					}
			}

			if (unrepliedReceivers)
			{
				this.SendMessageToFailedReceiversDirectly();
				this.StartReceiving();
				return ;
			}

			// LOG:
			BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
			{
				binaryLogWriter.WriteBroadcastEngineEvent(LogCategory.BroadcastEngine, "ResultCollector.WaitUntilReceiversReplyOrTimeOut",
					LogMessageType.BroadcastInvocationCompleted, null, null, null, null,
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
					null, null, false, this._dispatcher, this, false, null,
					null, null,
					"The broadcast invocation is completed.");
			}

			// to stop receiving info
			this.AllMessagesReceived.Set();
			if (this._broadcastCallFinishedHandler != null)
				this._broadcastCallFinishedHandler(this._dispatcher, this._iMessage, this);
		}

		private const string _uniqueReceiverName = "_";

		/// <summary>
		/// Sends the invocation to all registered receivers.
		/// </summary>
		/// <param name="msg">The message to be sent.</param>
		public void PerformBroadcasting(IMessage msg)
		{
			BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;
			string methodName = null;
			string invocationTarget = null;

			// the first stage of the broadcasting
			try
			{
				ArrayList listOfExcludedReceivers = new ArrayList();
				this._iMessage = msg;
				methodName = BinaryLogWriter.ParseInvocationMethod(msg.Properties["__MethodName"] as string, msg.Properties["__TypeName"] as string);

				BinaryFormatter formatterForLocalRecipients = null;

				// serialize the message
				var binaryFormatter = new BinaryFormatter(new RemotingSurrogateSelector(), new StreamingContext(StreamingContextStates.Other)).Safe();
				this._messageStream = new GenuineChunkedStream(false);
				binaryFormatter.Serialize(this._messageStream, msg);

				// to trace the message if it could reach the server via several channels
				string callGuidSubstring = null;
				if (this._dispatcher.IgnoreRecurrentCalls)
					callGuidSubstring = Guid.NewGuid().ToString("N");

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
				{
					binaryLogWriter.WriteBroadcastEngineEvent(LogCategory.BroadcastEngine, "ResultCollector.PerformBroadcasting",
						LogMessageType.BroadcastInvocationInitiated, null, null, null,
						binaryLogWriter[LogCategory.BroadcastEngine] > 1 ? (Stream) this._messageStream.Clone() : null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, true, this._dispatcher, this, false, null,
						methodName, null,
						"The broadcast invocation is initiated.");
				}

				// to prevent firing resultCollector.AllMessagesReceived event
				this.UnrepliedReceivers[_uniqueReceiverName] = null;

				object[] listOfReceiverInfo;
				this._dispatcher.GetListOfReceivers(out listOfReceiverInfo, msg, this);

				// through all recipients
				for ( int i = 0; i < listOfReceiverInfo.Length; i++)
				{
					ReceiverInfo receiverInfo = listOfReceiverInfo[i] as ReceiverInfo;
					if (receiverInfo == null)
						continue;

					string mbrUri = (string) receiverInfo.MbrUri;
					invocationTarget = mbrUri;

					try
					{
						lock (receiverInfo)
						{
							if (this._dispatcher.MaximumNumberOfConsecutiveFailsToExcludeReceiverAutomatically != 0 &&
								receiverInfo.NumberOfFails >= this._dispatcher.MaximumNumberOfConsecutiveFailsToExcludeReceiverAutomatically)
							{
								// put it to the list containing receivers being excluded
								listOfExcludedReceivers.Add(mbrUri);
								continue;
							}
						}

						// think that it'll fail
						if ( !receiverInfo.Local && receiverInfo.GeneralBroadcastSender == null)
						{
							lock(this)
							{
								this.Failed[mbrUri] = GenuineExceptions.Get_Broadcast_RemoteEndPointDidNotReplyForTimeOut();
							}
						}

						if (receiverInfo.Local)
						{
							// call to local appdomain
							// ignore recurrent calls
							if (this._dispatcher.IgnoreRecurrentCalls && UniqueCallTracer.Instance.WasGuidRegistered(mbrUri + callGuidSubstring))
								continue;

							// we'll wait for the answer from this receiver
							this.UnrepliedReceivers[mbrUri] = null;

							// LOG:
							if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
							{
								binaryLogWriter.WriteBroadcastEngineEvent(LogCategory.BroadcastEngine, "ResultCollector.PerformBroadcasting",
									LogMessageType.BroadcastRecipientInvoked, null, null, null, null,
									GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
									null, null, false, this._dispatcher, this, false, receiverInfo,
									null, null,
									"The local receiver is invoked via LocalPerformer.");
							}

                            if (formatterForLocalRecipients == null)
                                formatterForLocalRecipients = new BinaryFormatter().Safe();

                            // fixed in 2.5.9.6
                            IMessage iLocalMessage = (IMessage)formatterForLocalRecipients.Deserialize((Stream)this._messageStream.Clone());

							// queue task to run the call locally
							//IMessage iLocalMessage = (IMessage) binaryFormatter.Deserialize( (Stream) this._messageStream.Clone() );
                            LocalPerformer localPerformer = new LocalPerformer(iLocalMessage, this, receiverInfo.MbrObject);
							GenuineThreadPool.QueueUserWorkItem(new WaitCallback(localPerformer.Call), null, false);
						}
						else if (receiverInfo.GeneralBroadcastSender != null)
						{
							// call via true multicast channel
							Stream messageToBeSent = (Stream) this._messageStream.Clone();

							// send via real broadcast sender to the specific court
							msg.Properties["__Uri"] = string.Empty;
							Message message = Message.CreateOutcomingMessage(receiverInfo.GeneralBroadcastSender.ITransportContext, msg, new TransportHeaders(), messageToBeSent, false);
							message.DestinationMarshalByRef = receiverInfo.SerializedObjRef;
							message.GenuineMessageType = GenuineMessageType.TrueBroadcast;

							// to ignore recurrent calls on the remote side
							if (this._dispatcher.IgnoreRecurrentCalls)
								message.ITransportHeaders[Message.TransportHeadersBroadcastSendGuid] = callGuidSubstring;

							// LOG:
							if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
							{
								message.ITransportHeaders[Message.TransportHeadersInvocationTarget] = invocationTarget;
								message.ITransportHeaders[Message.TransportHeadersMethodName] = methodName;

								binaryLogWriter.WriteBroadcastEngineEvent(LogCategory.BroadcastEngine, "ResultCollector.PerformBroadcasting",
									LogMessageType.BroadcastRecipientInvoked, null, null, null, null,
									GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
									null, null, false, this._dispatcher, this, false, receiverInfo,
									null, null,
									"Mulsticast sender is being invoked.");
							}

							// register to catch all the answers
							receiverInfo.GeneralBroadcastSender.ITransportContext.IIncomingStreamHandler.RegisterResponseProcessor(message.MessageId, this);
							// and send it
							receiverInfo.GeneralBroadcastSender.SendMessage(message, this);
						}
						else
						{
							// send the invocation through the usual channel
							// we'll wait for the reply
							this.UnrepliedReceivers[mbrUri] = null;

							// send only if this receiver is not expected to receive message via broadcast channel
							if (receiverInfo.NeedsBroadcastSimulation)
							{
								// each time a new stream is created because sinks change stream position concurrently
								Stream messageToBeSent = (Stream) this._messageStream.Clone();
								TransportHeaders transportHeaders = new TransportHeaders();

								// to ignore recurrent calls on the remote side
								if (this._dispatcher.IgnoreRecurrentCalls)
									transportHeaders[Message.TransportHeadersBroadcastSendGuid] = callGuidSubstring;

								// LOG:
								if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
								{
									binaryLogWriter.WriteBroadcastEngineEvent(LogCategory.BroadcastEngine, "ResultCollector.PerformBroadcasting",
										LogMessageType.BroadcastRecipientInvoked, null, null, receiverInfo.DbgRemoteHost, null,
										GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
										null, null, false, this._dispatcher, this, false, receiverInfo,
										null, null,
										"The broadcast recipient is being invoked directly.");
								}

								// invoke the destination MBR
								msg.Properties["__Uri"] = receiverInfo.MbrUri;
								transportHeaders[Message.TransportHeadersBroadcastObjRefOrCourt] = receiverInfo.SerializedObjRef;
								transportHeaders[Message.TransportHeadersMbrUriName] = receiverInfo.MbrUri;
								transportHeaders[Message.TransportHeadersGenuineMessageType] = GenuineMessageType.BroadcastEngine;
								transportHeaders[Message.TransportHeadersInvocationTarget] = invocationTarget;
								transportHeaders[Message.TransportHeadersMethodName] = methodName;
								ClientChannelSinkStack clientChannelSinkStack = new ClientChannelSinkStack(this);
								clientChannelSinkStack.Push(this, null);
								receiverInfo.IClientChannelSink.AsyncProcessRequest(clientChannelSinkStack, this._iMessage, transportHeaders, messageToBeSent);
							}
						}
					}
					catch(Exception ex)
					{
						this.ParseResult(mbrUri, null, ex);
					}
				}

				// remove set uri from the hash to check wither the invocation finished
				this.UnrepliedReceivers.Remove(_uniqueReceiverName);
				if (this.UnrepliedReceivers.Count <= 0)
					this.AllMessagesReceived.Set();

				this.StartReceiving();

				if (listOfExcludedReceivers.Count > 0)
				{
					foreach(string uri in listOfExcludedReceivers)
						this._dispatcher.Remove(uri);
				}
			}
			catch(Exception ex)
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
				{
					binaryLogWriter.WriteBroadcastEngineEvent(LogCategory.BroadcastEngine, "ResultCollector.PerformBroadcasting",
						LogMessageType.BroadcastInvocationInitiated, ex, null, null, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, false, this._dispatcher, this, false, null,
						invocationTarget, methodName,
						"A critical failure occurred during broadcast.");
				}

				throw;
			}
		}

		/// <summary>
		/// Looks for a failed broadcast receivers and invoke them again.
		/// </summary>
		public void SendMessageToFailedReceiversDirectly()
		{
			if (this._secondStagePerformed)
				return ;

			this._secondStagePerformed = true;
			BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;

			// the second stage of the broadcasting
			using (ReaderAutoLocker reader = new ReaderAutoLocker(this._dispatcher._readerWriterLock))
			{
				// to prevent firing resultCollector.AllMessagesReceived event
				this.UnrepliedReceivers[_uniqueReceiverName] = null;

				lock(this)
				{
					foreach ( DictionaryEntry entry in this.Failed )
					{
						OperationException operationException = entry.Value as OperationException;
						if (operationException != null && operationException.OperationErrorMessage != null &&
							operationException.OperationErrorMessage.ErrorIdentifier == "GenuineChannels.Exception.Broadcast.RemoteEndPointDidNotReplyForTimeOut")
						{
							string uri = (string) entry.Key;
							ReceiverInfo receiverInfo = this._dispatcher.GetReceiverInfo(uri);

							// whether this receiver is expected to receive message via broadcast channel
							if (receiverInfo == null || receiverInfo.NeedsBroadcastSimulation)
								continue;

							// switch it back to simulation mode if the limit is exceeded
							lock(receiverInfo)
							{
								receiverInfo.NumberOfMulticastFails++;
								if (this._dispatcher.MaximumNumberOfConsecutiveFailsToEnableSimulationMode != 0 &&
									receiverInfo.NumberOfMulticastFails >= this._dispatcher.MaximumNumberOfConsecutiveFailsToEnableSimulationMode)
								{
									// force simulation
									receiverInfo.NeedsBroadcastSimulation = true;
									receiverInfo.NumberOfMulticastFails = 0;
								}
							}

							// each time a new stream is created because sinks change stream position concurrently
							Stream messageToBeSent = (Stream) this._messageStream.Clone();
							TransportHeaders transportHeaders = new TransportHeaders();

							// LOG:
							if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
							{
								string methodName = BinaryLogWriter.ParseInvocationMethod(this._iMessage.Properties["__MethodName"] as string, this._iMessage.Properties["__TypeName"] as string);
								string invocationTarget = receiverInfo.MbrUri;

								binaryLogWriter.WriteBroadcastEngineEvent(LogCategory.BroadcastEngine, "ResultCollector.SendMessageToFailedReceiversDirectly",
									LogMessageType.BroadcastRecipientInvokedAfterTimeout, null, null, receiverInfo.DbgRemoteHost, null,
									GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
									null, null, false, this._dispatcher, this, false, receiverInfo,
									invocationTarget, methodName,
									"The broadcast invocation is being directed to the recipient, which did not respond during the first stage.");
							}

							// set destination MBR
							this._iMessage.Properties["__Uri"] = receiverInfo.MbrUri;
							transportHeaders[Message.TransportHeadersBroadcastObjRefOrCourt] = receiverInfo.SerializedObjRef;
							ClientChannelSinkStack clientChannelSinkStack = new ClientChannelSinkStack(this);
							clientChannelSinkStack.Push(this, null);
							receiverInfo.IClientChannelSink.AsyncProcessRequest(clientChannelSinkStack, this._iMessage, transportHeaders, messageToBeSent);
						}
					}
				}

				this.UnrepliedReceivers.Remove(_uniqueReceiverName);
				if (this.UnrepliedReceivers.Count <= 0)
					this.AllMessagesReceived.Set();
			}
		}


		#region -- IResponseProcessor --------------------------------------------------------------

		/// <summary>
		/// Processes the received response.
		/// </summary>
		/// <param name="message">The response.</param>
		public void ProcessRespose(Message message)
		{
			// it's a response via the usual channel that the remote host successfully received a message via broadcast channel
			// message.Sender - remote MBR uri
			string objectUri = message.DestinationMarshalByRef as string;
			if (objectUri == null)
				return ;

			lock(this._dispatcher)
			{
				ReceiverInfo receiverInfo = this._dispatcher.GetReceiverInfo(objectUri);
				if (receiverInfo == null)
					return ;

				receiverInfo.NeedsBroadcastSimulation = false;
				receiverInfo.NumberOfMulticastFails = 0;
			}

			// if time-out has expired
			if (this.AllMessagesReceived.WaitOne(0, false))
				return ;

			try
			{
				var binaryFormatter = new BinaryFormatter().Safe();
				object reply = binaryFormatter.Deserialize(message.Stream);
				this.ParseResult(objectUri, reply as IMethodReturnMessage, reply as Exception);
			}
			catch(Exception ex)
			{
				this.ParseResult(objectUri, null, ex);
			}
		}

		/// <summary>
		/// An exception occurred during processing.
		/// </summary>
		/// <param name="exceptionAsObject">The exception.</param>
		public void DispatchException(object exceptionAsObject)
		{
			// it's impossible to do something here because the target is unknown
		}

		/// <summary>
		/// Indicates whether this message processor still waits for the response/responses.
		/// </summary>
		/// <param name="now">The current time elapsed since the system started.</param>
		/// <returns>The true value if the message processor still waits for the response.</returns>
		public bool IsExpired (int now)
		{
			return this.AllMessagesReceived.WaitOne(0, false);
		}

		/// <summary>
		/// Gets the uri of the remote host which is expected to send a response.
		/// </summary>
		public HostInformation Remote
		{
			get
			{
				return null;
			}
		}

		/// <summary>
		/// Gets an indication whether the response processor does not require a separate thread for processing.
		/// </summary>
		public bool IsShortInProcessing
		{
			get
			{
				// deserialization & locking may take noticable time
				return false;
			}
		}

		/// <summary>
		/// Gets the initial message for which the response is expected. Is used only for debugging purposes to track down
		/// the source message.
		/// </summary>
		public Message Message
		{
			get
			{
				return null;
			}
		}

		#endregion

		#region -- IClientChannelSink --------------------------------------------------------------

		/// <summary>
		/// Gets a dictionary through which sink properties can be accessed.
		/// </summary>
		public IDictionary Properties
		{
			get
			{
				return null;
			}
		}

		/// <summary>
		/// Gets the next server channel sink in the server sink chain.
		/// </summary>
		public IClientChannelSink NextChannelSink
		{
			get
			{
				return null;
			}
		}

		/// <summary>
		/// Requests asynchronous processing of a method call on the current sink.
		/// </summary>
		/// <param name="sinkStack">A stack of channel sinks that called this sink.</param>
		/// <param name="msg">The message to process.</param>
		/// <param name="headers">The headers to add to the outgoing message heading to the server.</param>
		/// <param name="stream">The stream headed to the transport sink.</param>
		public void AsyncProcessRequest(IClientChannelSinkStack sinkStack, IMessage msg,
			ITransportHeaders headers, Stream stream)
		{
			return ;
		}

		/// <summary>
		/// Requests asynchronous processing of a response to a method call on the current sink.
		/// </summary>
		/// <param name="sinkStack">A stack of sinks that called this sink.</param>
		/// <param name="state">Information generated on the request side that is associated with this sink.</param>
		/// <param name="headers">The headers retrieved from the server response stream.</param>
		/// <param name="stream">The stream coming back from the transport sink.</param>
		public void AsyncProcessResponse(IClientResponseChannelSinkStack sinkStack, object state,
			ITransportHeaders headers, Stream stream)
		{
			IMethodReturnMessage iMethodReturnMessage = null;
			string mbrUri = null;

			// if time-out has expired
			if (this.AllMessagesReceived.WaitOne(0, false))
				return ;

			try
			{
				mbrUri = (string) headers[Message.TransportHeadersMbrUriName];
				var binaryFormatter = new BinaryFormatter().Safe();
				iMethodReturnMessage = (IMethodReturnMessage) binaryFormatter.Deserialize(stream);
				this.ParseResult(mbrUri, iMethodReturnMessage, null);
			}
			catch (Exception ex)
			{
				if (mbrUri != null)
					this.ParseResult(mbrUri, null, ex);
			}
		}

		/// <summary>
		/// Returns the Stream onto which the provided message is to be serialized.
		/// </summary>
		/// <param name="msg">The IMethodCallMessage containing details about the method call.</param>
		/// <param name="headers">The headers to add to the outgoing message heading to the server.</param>
		/// <returns>The Stream onto which the provided message is to be serialized.</returns>
		public Stream GetRequestStream(IMessage msg, ITransportHeaders headers)
		{
			return null;
		}

		/// <summary>
		/// Requests message processing from the current sink.
		/// </summary>
		/// <param name="msg">The message to process.</param>
		/// <param name="requestHeaders">The headers to add to the outgoing message heading to the server.</param>
		/// <param name="requestStream">The stream headed to the transport sink.</param>
		/// <param name="responseHeaders">When this method returns, contains an ITransportHeaders interface that holds the headers that the server returned. This parameter is passed uninitialized.</param>
		/// <param name="responseStream">When this method returns, contains a Stream coming back from the transport sink. This parameter is passed uninitialized.</param>
		public void ProcessMessage(IMessage msg, ITransportHeaders requestHeaders, Stream requestStream,
			out ITransportHeaders responseHeaders, out Stream responseStream)
		{
			responseHeaders = null;
			responseStream = null;
		}

		#endregion

		#region -- IMessageSink --------------------------------------------------------------------

		/// <summary>
		/// Gets the next message sink in the sink chain.
		/// </summary>
		public IMessageSink NextSink
		{
			get
			{
				return null;
			}
		}

		/// <summary>
		/// Asynchronously processes the given message.
		/// </summary>
		/// <param name="msg">The message to process.</param>
		/// <param name="replySink">The reply sink for the reply message.</param>
		/// <returns>Returns an IMessageCtrl interface that provides a way to control asynchronous messages after they have been dispatched.</returns>
		public IMessageCtrl AsyncProcessMessage(IMessage msg, IMessageSink replySink)
		{
			return null;
		}

		/// <summary>
		/// Synchronously processes the given message.
		/// </summary>
		/// <param name="msg">The message to process.</param>
		/// <returns>A reply message in response to the request.</returns>
		public IMessage SyncProcessMessage(IMessage msg)
		{
			return null;
		}

		#endregion

		#region -- Debugging stuff -----------------------------------------------------------------

		/// <summary>
		/// Gets the unique identifier of the current instance. Is used for debugging purposes only.
		/// </summary>
		public int DbgResultCollectorId
		{
			get
			{
				return this._dbgResultCollectorId;
			}
		}
		private int _dbgResultCollectorId = Interlocked.Increment(ref _dbgTotalResultCollectors);
		private static int _dbgTotalResultCollectors = 0;

		#endregion
	}
}
