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

using Belikov.GenuineChannels;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.DotNetRemotingLayer
{
	/// <summary>
	/// Implements the client transport sink.
	/// </summary>
	public class GenuineTcpClientTransportSink : BaseChannelSinkWithProperties, IClientChannelSink
	{
		/// <summary>
		/// Constructs an instance of the GenuineTcpClientTransportSink class.
		/// </summary>
		/// <param name="url">The Url of the remote object.</param>
		/// <param name="iTransportContext">The transport context.</param>
		public GenuineTcpClientTransportSink(string url, ITransportContext iTransportContext)
		{
			this.ITransportContext = iTransportContext;

			string objectURI;
			this._recipientUri = GenuineUtility.Parse(url, out objectURI);

			this._properties["GC_URI"] = this._recipientUri;
			this._properties["GC_TC"] = iTransportContext;

			// it's rather a trick, but works well
			this._properties["GC_TS"] = this;
		}

		/// <summary>
		/// The transport context to send messages through.
		/// </summary>
		public ITransportContext ITransportContext;

		/// <summary>
		/// Internal properties.
		/// </summary>
		private Hashtable _properties = new Hashtable();

		/// <summary>
		/// Gets an ICollection of keys that the channel object properties are associated with.
		/// </summary>
		public override ICollection Keys 
		{
			get
			{
				return this._properties.Keys;
			}
		}

		/// <summary>
		/// Gets or sets the property associated with the specified key.
		/// </summary>
		public override object this[object key]
		{
			get
			{
				return this._properties[key];
			}
			set
			{
				this._properties[key] = value;
			}
		}

		/// <summary>
		/// The uri of the recipient.
		/// </summary>
		protected string _recipientUri;

		#region IClientChannelSink

		/// <summary>
		/// Gets the next server channel sink in the server sink chain.
		/// </summary>
		public IClientChannelSink NextChannelSink
		{
			get
			{	// transport sink always is last
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
			ITransportContext iTransportContext = this.ITransportContext;

			if (! GenuineUtility.CheckUrlOnConnectivity(this._recipientUri))
			{
				// get the transport context from the Uri Storage
				iTransportContext = UriStorage.GetTransportContext(this._recipientUri);
				if (iTransportContext == null)
					iTransportContext = this.ITransportContext;
			}

			Message message = Message.CreateOutcomingMessage(iTransportContext, msg, headers, stream, false);
			message.Recipient = iTransportContext.KnownHosts[this._recipientUri];
			IMethodMessage iMethodMessage = (IMethodMessage) msg;
			message.IsOneWay = RemotingServices.IsOneWay(iMethodMessage.MethodBase);

			// LOG: put down the log record
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
			{
				binaryLogWriter.WriteMessageCreatedEvent("GenuineTcpClientTransportSink.AsyncProcessRequest",
					LogMessageType.MessageCreated, null, message, true, message.Recipient, 
					this.ITransportContext.BinaryLogWriter[LogCategory.MessageProcessing] > 1 ? message.Stream : null, 
					msg.Properties["__Uri"] as string, BinaryLogWriter.ParseInvocationMethod(msg.Properties["__MethodName"] as string, msg.Properties["__TypeName"] as string),
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, -1, -1, null, -1, null,
					"Asynchronous .NET Remoting invocaiton is being initiated.");

				message.ITransportHeaders[Message.TransportHeadersInvocationTarget] = msg.Properties["__Uri"] as string;
				message.ITransportHeaders[Message.TransportHeadersMethodName] = BinaryLogWriter.ParseInvocationMethod(msg.Properties["__MethodName"] as string, msg.Properties["__TypeName"] as string);

				binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "GenuineTcpClientTransportSink.AsyncProcessRequest",
					LogMessageType.MessageRequestInvoking, null, message, message.Recipient, 
					null,
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
					null, null, -1, 
					GenuineUtility.TickCount, 0, 0, null, null, null, null,
					"The .NET Remoting asynchronous invocation is being initiated.");
			}

			// register the response handler
			AsyncSinkStackResponseProcessor asyncSinkStackResponseProcessor = null;
			if (! message.IsOneWay)
			{
				asyncSinkStackResponseProcessor = new AsyncSinkStackResponseProcessor(iTransportContext, message, sinkStack);
				iTransportContext.IIncomingStreamHandler.RegisterResponseProcessor(message.MessageId, asyncSinkStackResponseProcessor);
			}

			try
			{
				// and try to send the message
				iTransportContext.ConnectionManager.Send(message);
			}
			catch(Exception ex)
			{
				asyncSinkStackResponseProcessor.DispatchException(ex);
				throw;
			}
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
			throw new NotSupportedException();
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
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			ITransportContext iTransportContext = this.ITransportContext;

			if (! GenuineUtility.CheckUrlOnConnectivity(this._recipientUri))
			{
				// get the transport context from the Uri Storage
				iTransportContext = UriStorage.GetTransportContext(this._recipientUri);
				if (iTransportContext == null)
					iTransportContext = this.ITransportContext;
			}

			Message message = Message.CreateOutcomingMessage(iTransportContext, msg, requestHeaders, requestStream, true);

			try
			{
				message.Recipient = iTransportContext.KnownHosts[this._recipientUri];

				// LOG: put down the log record
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
				{
					binaryLogWriter.WriteMessageCreatedEvent("GenuineTcpClientTransportSink.ProcessMessage",
						LogMessageType.MessageCreated, null, message, true, message.Recipient, 
						this.ITransportContext.BinaryLogWriter[LogCategory.MessageProcessing] > 1 ? message.Stream : null, 
						msg.Properties["__Uri"] as string, BinaryLogWriter.ParseInvocationMethod(msg.Properties["__MethodName"] as string, msg.Properties["__TypeName"] as string),
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, -1, -1, null, -1, null,
						"Synchronous .NET Remoting invocaiton is being initiated.");

					message.ITransportHeaders[Message.TransportHeadersInvocationTarget] = msg.Properties["__Uri"] as string;
					message.ITransportHeaders[Message.TransportHeadersMethodName] = BinaryLogWriter.ParseInvocationMethod(msg.Properties["__MethodName"] as string, msg.Properties["__TypeName"] as string);

					binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "GenuineTcpClientTransportSink.ProcessMessage",
						LogMessageType.MessageRequestInvoking, null, message, message.Recipient, 
						null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
						null, null, -1, GenuineUtility.TickCount, 0, 0, null, null, null, null,
						"The .NET Remoting synchronous invocation has been made.");
				}

				SyncSinkStackResponseProcessor syncSinkStackResponseProcessor = new SyncSinkStackResponseProcessor(iTransportContext, message);
				iTransportContext.IIncomingStreamHandler.RegisterResponseProcessor(message.MessageId, syncSinkStackResponseProcessor);
				message.CancelSending = syncSinkStackResponseProcessor.IsReceivedEvent;

				try
				{
					// send the message or initiate the sending
					iTransportContext.ConnectionManager.Send(message);
				}
				catch(Exception ex)
				{
					// if it's a response processor's problem, force its exception to be fired
					if (syncSinkStackResponseProcessor.DispatchedException != null)
						throw OperationException.WrapException(syncSinkStackResponseProcessor.DispatchedException);

					syncSinkStackResponseProcessor.DispatchException(ex);
					throw;
				}

				if (! GenuineUtility.WaitOne(syncSinkStackResponseProcessor.IsReceivedEvent, GenuineUtility.GetMillisecondsLeft(message.FinishTime)) )
					throw GenuineExceptions.Get_Send_ServerDidNotReply();
				if (syncSinkStackResponseProcessor.DispatchedException != null)
					throw OperationException.WrapException(syncSinkStackResponseProcessor.DispatchedException);

				// LOG: put down the log record
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "GenuineTcpClientTransportSink.ProcessMessage",
						LogMessageType.MessageDispatched, null, syncSinkStackResponseProcessor.Response, syncSinkStackResponseProcessor.Response.Sender, 
						null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
						null, null, -1, 
						GenuineUtility.TickCount, 0, message.SeqNo, null, null, null, null,
						"The .NET Remoting synchronous invocation has been completed.");

				}

				responseHeaders = syncSinkStackResponseProcessor.Response.ITransportHeaders;
				responseStream = syncSinkStackResponseProcessor.Response.Stream;
			}
			catch(Exception ex)
			{
				// LOG: put down the log record
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
					binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "GenuineTcpClientTransportSink.ProcessMessage",
						LogMessageType.MessageDispatched, ex, message, message.Recipient, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null, -1, 
						GenuineUtility.TickCount, 0, message.SeqNo, null, null, null, null,
						"The exception is dispatched to the caller context.");

				throw;
			}
		}

		#endregion
	}
}
