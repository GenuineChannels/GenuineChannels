/* Genuine Channels product.
 *
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 *
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Runtime.Remoting.Channels;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Receiving;
using Belikov.GenuineChannels.TransportContext;
using Zyan.SafeDeserializationHelpers;

namespace Belikov.GenuineChannels.DotNetRemotingLayer
{
	/// <summary>
	/// Processes replies to the .NET Remoting invocations sent asynchronously.
	/// </summary>
	public class AsyncSinkStackResponseProcessor : IResponseProcessor
	{
		/// <summary>
		/// Initializes an instance of the AsyncSinkStackResponseProcessor class.
		/// </summary>
		/// <param name="iTransportContext">The Transport Context.</param>
		/// <param name="message">Source message.</param>
		/// <param name="iClientChannelSinkStack">The client channel sink stack.</param>
		public AsyncSinkStackResponseProcessor(ITransportContext iTransportContext, Message message, IClientChannelSinkStack iClientChannelSinkStack)
		{
			this._iTransportContext = iTransportContext;
			this._message = message;
			this._iClientChannelSinkStack = iClientChannelSinkStack;
		}

		/// <summary>
		/// Releases acquired resources.
		/// </summary>
		public void Dispose()
		{
		}

		private ITransportContext _iTransportContext;
		private Message _message;
		private IClientChannelSinkStack _iClientChannelSinkStack;
		private bool _isDispatched = false;

		/// <summary>
		/// Directs the message to the message receiver.
		/// </summary>
		/// <param name="message">The response.</param>
		public void ProcessRespose(Message message)
		{
			BinaryLogWriter binaryLogWriter = this._iTransportContext.BinaryLogWriter;

			lock(this)
			{
				if (this._isDispatched)
					return ;

				this._isDispatched = true;
			}

			if (message.ContainsSerializedException)
			{
				try
				{
					var binaryFormatter = new BinaryFormatter().Safe();
					var exception = (Exception) binaryFormatter.Deserialize(message.Stream);

					// LOG: put down the log record
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
						binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "AsyncSinkStackResponseProcessor.ProcessRespose",
							LogMessageType.MessageDispatched, exception, message, message.Sender, null,
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null, -1,
							GenuineUtility.TickCount, 0, this._message.SeqNo, null, null, null, null,
							"The exception received as a response to the asynchronous message is dispatched back to the caller context.");

					this._iClientChannelSinkStack.DispatchException(exception);
				}
				catch(Exception ex)
				{
					// LOG: put down the log record
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
						binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "AsyncSinkStackResponseProcessor.ProcessRespose",
							LogMessageType.MessageDispatched, ex, message, message.Sender, null,
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null, -1,
							GenuineUtility.TickCount, 0, this._message.SeqNo, null, null, null, null,
							"The received exception cannot be deserialized. This exception is dispatched instead.");

					this._iClientChannelSinkStack.DispatchException(ex);
				}
			}
			else
			{
				// LOG: put down the log record
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
					binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "AsyncSinkStackResponseProcessor.ProcessRespose",
						LogMessageType.MessageDispatched, null, message, message.Sender, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null, -1,
						GenuineUtility.TickCount, 0, this._message.SeqNo, null, null, null, null,
						"The response to the asynchronous message is dispatched back to the caller context.");

				this._iClientChannelSinkStack.AsyncProcessResponse(message.ITransportHeaders, message.Stream);
			}
		}

		/// <summary>
		/// Dispatches the exception to the caller.
		/// </summary>
		/// <param name="exceptionAsObject">The reason of the error represented as the reference to an instance of the Object class.</param>
		public void DispatchException(object exceptionAsObject)
		{
			lock(this)
			{
				if (this._isDispatched)
					return ;

				this._isDispatched = true;
			}

			this._iClientChannelSinkStack.DispatchException(exceptionAsObject as Exception);
		}

		/// <summary>
		/// Indicates whether this message processor still waits for the response(s).
		/// </summary>
		/// <param name="now">The current time elapsed since the system started.</param>
		/// <returns>True if the message processor still waits for the response.</returns>
		public bool IsExpired (int now)
		{
			lock(this)
			{
				if (this._isDispatched)
					return true;
			}

			if (GenuineUtility.IsTimeoutExpired(this._message.FinishTime, now))
				return true;

			return false;
		}

		/// <summary>
		/// Gets the uri of the remote host expected to send a response.
		/// </summary>
		public HostInformation Remote
		{
			get
			{
				return this._message.Recipient;
			}
		}

		/// <summary>
		/// Gets an indication whether the response processor does not require a separate thread for processing.
		/// </summary>
		public bool IsShortInProcessing
		{
			get
			{
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
				return this._message;
			}
		}

	}
}
