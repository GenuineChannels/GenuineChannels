/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Serialization.Formatters.Binary;
using System.Web;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Serialization;
using System.Threading;

using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.DotNetRemotingLayer
{
	/// <summary>
	/// Implements the server transport sink.
	/// </summary>
	public class GenuineUniversalServerTransportSink : IServerChannelSink
	{
		/// <summary>
		/// To avoid a problem inside MS code that can happen once per 380 thousand requests on multiCPU machines.
		/// </summary>
		static GenuineUniversalServerTransportSink()
		{
			// remove the damned pool
			BinaryServerFormatterSinkProvider binaryServerFormatterSinkProvider = new BinaryServerFormatterSinkProvider();
			Type coreChannelType = binaryServerFormatterSinkProvider.GetType().Assembly.GetType("System.Runtime.Remoting.Channels.CoreChannel");
			Type byteBufferAllocator = binaryServerFormatterSinkProvider.GetType().Assembly.GetType("System.IO.ByteBufferAllocator");

			FieldInfo _bufferPool = coreChannelType.GetField("_bufferPool", BindingFlags.NonPublic | BindingFlags.Static);
			ConstructorInfo constructorInfo = byteBufferAllocator.GetConstructor(new Type[] { typeof(Int32) } );

			_bufferPool.SetValue(null, constructorInfo.Invoke(new object[] { 0x1000 } ));
		}

		/// <summary>
		/// Creates an instance of the GenuineTcpServerTransportSink class.
		/// </summary>
		/// <param name="channel">The parent channel.</param>
		/// <param name="nextChannelSink">The next channel sink for futher processing.</param>
		/// <param name="iTransportContext">The Transport Context.</param>
		public GenuineUniversalServerTransportSink(BasicChannelWithSecurity channel, IServerChannelSink nextChannelSink, ITransportContext iTransportContext)
		{
			this.ITransportContext = iTransportContext;
			this._channel = channel;
			this._nextChannelSink = nextChannelSink;
		}

		/// <summary>
		/// The transport context to send messages through.
		/// </summary>
		public ITransportContext ITransportContext;

		private BasicChannelWithSecurity _channel;

		#region IServerChannelSink 

		/// <summary>
		/// Gets the next server channel sink in the server sink chain.
		/// </summary>
		public IServerChannelSink NextChannelSink
		{
			get
			{
				return _nextChannelSink;
			}
		}
		private IServerChannelSink _nextChannelSink;

		/// <summary>
		/// Requests processing from the current sink of the response from a method call sent asynchronously.
		/// It's a reply to a previously processed async call.
		/// Parameter state is expected to contain source message.
		/// </summary>
		/// <param name="sinkStack">A stack of sinks leading back to the server transport sink.</param>
		/// <param name="state">Information generated on the request side that is associated with this sink.</param>
		/// <param name="msg">The response message.</param>
		/// <param name="headers">The headers to add to the return message heading to the client.</param>
		/// <param name="stream">The stream heading back to the transport sink.</param>
		public void AsyncProcessResponse(IServerResponseChannelSinkStack sinkStack,
			object state, IMessage msg, ITransportHeaders headers, Stream stream)
		{
			Message message = (Message) state;
			BinaryLogWriter binaryLogWriter = message.ITransportContext.BinaryLogWriter;

			Message reply = new Message(message, headers, stream);

			// LOG: put down the log record
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
			{
				string invocationTarget = msg.Properties["__Uri"] as string;
				string methodName = BinaryLogWriter.ParseInvocationMethod(msg.Properties["__MethodName"] as string, msg.Properties["__TypeName"] as string);

				binaryLogWriter.WriteMessageCreatedEvent("GenuineUniversalServerTransportSink.AsyncProcessResponse",
					LogMessageType.MessageCreated, null, reply, false, reply.Recipient, 
					this.ITransportContext.BinaryLogWriter[LogCategory.MessageProcessing] > 1 ? reply.Stream : null, 
					invocationTarget, methodName,
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, -1, -1, null, -1, null,
					"The response message has been created.");

				reply.ITransportHeaders[Message.TransportHeadersInvocationTarget] = invocationTarget;
				reply.ITransportHeaders[Message.TransportHeadersMethodName] = methodName;

				binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "GenuineUniversalServerTransportSink.AsyncProcessResponse",
					LogMessageType.MessageRequestInvoked, null, reply, message.Sender, 
					null,
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
					null, null, -1, GenuineUtility.TickCount, 0, message.SeqNo, null, null, null, null,
					"The .NET Remoting invocation has been performed.");
			}

			message.ITransportContext.ConnectionManager.Send(reply);
		}

		/// <summary>
		/// Returns the Stream onto which the provided response message is to be serialized.
		/// </summary>
		/// <param name="sinkStack">A stack of sinks leading back to the server transport sink.</param>
		/// <param name="state">The state that has been pushed to the stack by this sink.</param>
		/// <param name="msg">The response message to serialize.</param>
		/// <param name="headers">The headers to put in the response stream to the client.</param>
		/// <returns>The Stream onto which the provided response message is to be serialized.</returns>
		public Stream GetResponseStream(IServerResponseChannelSinkStack sinkStack, object state, IMessage msg,
			ITransportHeaders headers)
		{
			return null;
		}

		/// <summary>
		/// Requests message processing from the current sink.
		/// </summary>
		/// <param name="sinkStack">A stack of channel sinks that called the current sink.</param>
		/// <param name="requestMsg">The message that contains the request.</param>
		/// <param name="requestHeaders">Headers retrieved from the incoming message from the client.</param>
		/// <param name="requestStream">The stream that needs to be to processed and passed on to the deserialization sink.</param>
		/// <param name="responseMsg">When this method returns, contains an IMessage that holds the response message. This parameter is passed uninitialized.</param>
		/// <param name="responseHeaders">When this method returns, contains an ITransportHeaders that holds the headers that are to be added to return message heading to the client. This parameter is passed uninitialized.</param>
		/// <param name="responseStream">When this method returns, contains a Stream that is heading back to the transport sink. This parameter is passed uninitialized.</param>
		/// <returns>A ServerProcessing status value that provides information about how message was processed.</returns>
		public ServerProcessing ProcessMessage(IServerChannelSinkStack sinkStack, IMessage requestMsg,
			ITransportHeaders requestHeaders, Stream requestStream, out IMessage responseMsg,
			out ITransportHeaders responseHeaders, out Stream responseStream )
		{
			// It's a transport sink.
			throw new NotSupportedException();
		}

		#endregion

		#region IChannelSinkBase

		/// <summary>
		/// Just to avoid compiler error.
		/// </summary>
		public System.Collections.IDictionary Properties 
		{
			get 
			{
				// not necessary
				return null;
			}
		}

		#endregion

		/// <summary>
		/// Handles incoming message (message, response or event).
		/// </summary>
		/// <param name="message">The message to handle.</param>
		public void HandleIncomingMessage(Message message)
		{
			BinaryLogWriter binaryLogWriter = message.ITransportContext.BinaryLogWriter;

			try
			{
				ServerChannelSinkStack stack = new ServerChannelSinkStack();
				stack.Push(this, message);

				ITransportHeaders responseHeaders;
				Stream responseStream;
				IMessage responseMsg;

				// FIX: 2.5.8 removing the application name from the object URI
				string applicationName = RemotingConfiguration.ApplicationName;
				if (applicationName != null)
				{
					string uri = (string) message.ITransportHeaders["__RequestUri"];
					if (uri.Length > applicationName.Length && uri.StartsWith(applicationName))
					{
						int sizeToBeCut = applicationName.Length + (uri[applicationName.Length] == '/' ? 1 : 0);
						uri = uri.Substring(sizeToBeCut);
						message.ITransportHeaders["__RequestUri"] = uri;
					}
				}

				message.ITransportHeaders["__CustomErrorsEnabled"] = false;
				message.ITransportHeaders[CommonTransportKeys.IPAddress] = message.Sender.PhysicalAddress is IPEndPoint ? ((IPEndPoint) message.Sender.PhysicalAddress).Address : message.Sender.PhysicalAddress;

				// LOG: put down the log record
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "GenuineUniversalServerTransportSink.HandleIncomingMessage",
						LogMessageType.MessageRequestInvoking, null, message, message.Sender, 
						null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
						null, null, -1, GenuineUtility.TickCount, 0, 0, null, null, null, null,
						"The .NET Remoting request is being invoked.");
				}

				ServerProcessing serverProcessing = this._nextChannelSink.ProcessMessage(stack, null, message.ITransportHeaders, message.Stream, out responseMsg, out responseHeaders, out responseStream);

				switch (serverProcessing)
				{
					case ServerProcessing.Complete:
						Message reply = new Message(message, responseHeaders, responseStream);

						// LOG: put down the log record
						if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
						{
							string invocationTarget = responseMsg.Properties["__Uri"] as string;
							string methodName = BinaryLogWriter.ParseInvocationMethod(responseMsg.Properties["__MethodName"] as string, responseMsg.Properties["__TypeName"] as string);
							binaryLogWriter.WriteMessageCreatedEvent("GenuineUniversalServerTransportSink.HandleIncomingMessage",
								LogMessageType.MessageCreated, null, reply, false, reply.Recipient, 
								this.ITransportContext.BinaryLogWriter[LogCategory.MessageProcessing] > 1 ? reply.Stream : null, 
								invocationTarget, methodName,
								GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, -1, -1, null, -1, null,
								"The response message has been created.");

							reply.ITransportHeaders[Message.TransportHeadersInvocationTarget] = invocationTarget;
							reply.ITransportHeaders[Message.TransportHeadersMethodName] = methodName;

							binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "GenuineUniversalServerTransportSink.HandleIncomingMessage",
								LogMessageType.MessageRequestInvoked, null, reply, message.Sender, 
								null,
								GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
								null, null, -1, GenuineUtility.TickCount, 0, message.SeqNo, null, null, null, null,
								"The .NET Remoting invocation has been performed.");
						}

						message.ITransportContext.ConnectionManager.Send(reply);
						break;

					case ServerProcessing.Async:
						// asyncProcessResponse will be called later
						break;

					case ServerProcessing.OneWay:
						// LOG: put down the log record
						if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
						{
							binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "GenuineUniversalServerTransportSink.HandleIncomingMessage",
								LogMessageType.MessageRequestInvoked, null, null, message.Sender, 
								null,
								GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
								null, null, -1, GenuineUtility.TickCount, 0, message.SeqNo, null, null, null, null,
								"One-way .NET Remoting invocation has been performed. No response is available.");
						}
						break;

				}
			}
			catch(Exception ex)
			{
				try
				{
					// LOG: put down the log record
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
					{
						binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "GenuineUniversalServerTransportSink.HandleIncomingMessage",
							LogMessageType.MessageRequestInvoking, ex, message, message.Sender, 
							null,
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
							null, null, -1, 0, 0, 0, null, null, null, null,
							"The .NET Remoting request resulted in exception. The exception is being sent back.");
					}

					// return this exception as a result
					BinaryFormatter binaryFormatter = new BinaryFormatter();
					GenuineChunkedStream serializedException = new GenuineChunkedStream(false);
					binaryFormatter.Serialize(serializedException, ex);

					Message reply = new Message(message, new TransportHeaders(), serializedException);
					reply.ContainsSerializedException = true;
					this.ITransportContext.ConnectionManager.Send(reply);
				}
				catch(Exception internalEx)
				{
					// It's a destiny not to deliver an exception back to the caller

					// LOG: put down the log record
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
					{
						binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "GenuineUniversalServerTransportSink.HandleIncomingMessage",
							LogMessageType.MessageRequestInvoking, internalEx, message, message.Sender, 
							null,
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
							null, null, -1, 0, 0, 0, null, null, null, null,
							"The source exception cannot be sent over the network. Both exceptions are ignored.");
					}

				}
			}
		}

	}
}
