/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.IO;
using System.Collections;
using System.Threading;
using System.Diagnostics;

using Belikov.Common.ThreadProcessing;
using Belikov.GenuineChannels;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Parameters;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Security;

namespace Belikov.GenuineChannels.Messaging
{
	/// <summary>
	/// MessageContainer manages the set of the messages.
	/// WARNING: it doesn't dispose any messages automatically!
	/// </summary>
	internal class MessageContainer
	{
		/// <summary>
		/// Constructs an instance of the MessageContainer class.
		/// </summary>
		public MessageContainer(ITransportContext iTransportContext)
		{
			this._iTransportContext = iTransportContext;

			this._maxQueuedItems = (int) iTransportContext.IParameterProvider[GenuineParameter.MaxQueuedItems];
			this._maxTotalSize = (int) iTransportContext.IParameterProvider[GenuineParameter.MaxTotalSize];
			this._maxContentSize = (int) iTransportContext.IParameterProvider[GenuineParameter.MaxContentSize];
			this._noSizeChecking = (bool) iTransportContext.IParameterProvider[GenuineParameter.NoSizeChecking];
		}

		#region -- Declarations --------------------------------------------------------------------

		/// <summary>
		/// The queue containing messages being sent.
		/// </summary>
		internal Queue _queue = new Queue(20);

		/// <summary>
		/// Gets an indication whether a message is available for sending.
		/// </summary>
		public bool IsMessageAvailable
		{
			get
			{
				lock (this._queue)
				{
					return this._queue.Count > 0;
				}
			}
		}

		/// <summary>
		/// The Transport Context.
		/// </summary>
		private ITransportContext _iTransportContext;

		/// <summary>
		/// The total size of all messages in bytes.
		/// Does not include messages from _queueWithFailedMessages queue.
		/// </summary>
		private int _currentTotalSize = 0;

		/// <summary>
		/// The total number of registered messages (including synchronous messages).
		/// </summary>
		private int _currentTotalMessages = 0;

		/// <summary>
		/// The maximum allowable size of a message.
		/// </summary>
		private int _maxContentSize;

		/// <summary>
		/// The maximum number of items in the queue.
		/// </summary>
		private int _maxQueuedItems;

		/// <summary>
		/// The maximum summary size of all messages in the queue.
		/// </summary>
		private int _maxTotalSize;

		/// <summary>
		/// Represents a value indicating whether to cancel message size checking.
		/// </summary>
		private bool _noSizeChecking;

		#endregion

		#region -- Methods -------------------------------------------------------------------------

		/// <summary>
		/// Decreases all queue counters according to the provided message.
		/// WARNING: Releases all message resoruces.
		/// </summary>
		/// <param name="message">The message.</param>
		public void UnregisterSyncMessage(Message message)
		{
			lock (this._queue)
			{
				this._currentTotalMessages --;
				this._currentTotalSize -= message.EffectiveMessageSize;

				if (this._currentTotalMessages < 0 || this._currentTotalSize < 0)
				{
					// LOG:
					BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;
					if ( binaryLogWriter != null )
					{
						binaryLogWriter.WriteImplementationWarningEvent("MessageContainer.UnregisterSyncMessage",
							LogMessageType.CriticalError, GenuineExceptions.Get_Debugging_GeneralWarning("Implementation bug."),
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
							"Incorrect queue state. CurrentTotalMessages = {0}. CurrentTotalSize = {1}.", 
							this._currentTotalMessages, this._currentTotalSize);
					}
				}
			}
		}

		/// <summary>
		/// Increases all queue counters according to the provided message and 
		/// puts a message into the queue.
		/// </summary>
		/// <param name="message">The message being put into the queue.</param>
		/// <param name="onlyCheckIn">Check the message in without adding it to any queues.</param>
		public void AddMessage(Message message, bool onlyCheckIn)
		{
			BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;

			using (new ReaderAutoLocker(this._disposeLock))
			{
				if (this._disposed)
					throw OperationException.WrapException(this._disposeException);

				int maxContentSize = 0;
				if (message.SecuritySessionParameters != null)
					maxContentSize = message.SecuritySessionParameters.MaxContentSize;
				if (maxContentSize == 0)
					maxContentSize = this._maxContentSize;
				if (maxContentSize > 0 && ! this._noSizeChecking)
				{
					// calculate message effective size
					try
					{
						if (message.SerializedContent != null && message.SerializedContent.CanSeek)
							message.EffectiveMessageSize = (int) message.SerializedContent.Length;
//						else if (message.Stream != null && message.Stream.CanSeek)
//							message.EffectiveMessageSize = (int) message.Stream.Length;
					}
					catch(Exception ex)
					{
						// LOG:
						if ( binaryLogWriter != null && binaryLogWriter[LogCategory.ImplementationWarning] > 0 )
						{
							binaryLogWriter.WriteEvent(LogCategory.ImplementationWarning, "MessageContainer.AddMessage",
								LogMessageType.Warning, ex, message, message.Recipient,
								null, GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
								null, null, -1, 0, 0, 0, null, null, null, null,
								"Message stream doesn't support the Length property. Set the NoSizeChecking parameter to true to increase performance");
						}
					}

					try
					{
						if (message.SerializedContent != null && message.EffectiveMessageSize == 0 && message.SerializedContent.CanSeek)
							message.EffectiveMessageSize = Math.Min(message.EffectiveMessageSize, (int) message.SerializedContent.Length);
					}
					catch(Exception ex)
					{
						// LOG:
						if ( binaryLogWriter != null && binaryLogWriter[LogCategory.ImplementationWarning] > 0 )
						{
							binaryLogWriter.WriteEvent(LogCategory.ImplementationWarning, "MessageContainer.AddMessage",
								LogMessageType.Warning, ex, message, message.Recipient,
								null, GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
								null, null, -1, 0, 0, 0, null, null, null, null,
								"Message stream doesn't support the Length property. Set the NoSizeChecking parameter to true to increase performance");
						}
					}

					// check the message size
					if (message.EffectiveMessageSize > maxContentSize)
						throw GenuineExceptions.Get_Send_TooLargePacketSize((int) message.EffectiveMessageSize, maxContentSize);
				}

				bool overloaded = false;

				lock (this._queue)
				{
					// check on total size
					if (this._currentTotalMessages + 1 > this._maxQueuedItems || 
						message.EffectiveMessageSize + this._currentTotalSize > this._maxTotalSize )
					{
						// to prevent a deadlock
						overloaded = true;
					}
					else
					{
						this._currentTotalMessages ++;
						this._currentTotalSize += message.EffectiveMessageSize;

						// put it into the queue or awaiting list
						if (! onlyCheckIn)
						{
							this._queue.Enqueue(message);

							// reflect the change in the queue state
							if (message.Recipient != null)
								message.Recipient.QueueLength = this._queue.Count;

							// LOG:
							if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
								binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "MessageContainer.AddMessage",
									LogMessageType.MessageIsEnqueued, null, message, message.Recipient, null,
									GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null, -1, 0, 0, 0, null, null, null, null,
									"The message has been enqueued because no connections are available. Messages in the queue: {0}. Queue size: {1}.",
									this._currentTotalMessages, this._currentTotalSize);
						}
					}
				} // lock (this._queue)

				if (overloaded)
				{
					Exception exception = GenuineExceptions.Get_Send_QueueIsOverloaded(this._maxQueuedItems, this._currentTotalMessages + 1, this._maxTotalSize, message.EffectiveMessageSize + this._currentTotalSize);

					// LOG:
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
					{
						binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "MessageContainer.AddMessage",
							LogMessageType.MessageIsEnqueued, exception, message, message.Recipient, null,
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null, -1, 0, 0, 0, null, null, null, null,
							"Queue is overrun.");
					}

					this.Dispose(exception);
					throw exception;
				}
			}
		}

		/// <summary>
		/// Exludes the message from the queue.
		/// </summary>
		/// <returns>The message or a null reference.</returns>
		public Message GetMessage()
		{
			Message message = null;

			using (new ReaderAutoLocker(this._disposeLock))
			{
				if (this._disposed)
					return null;

				lock (this._queue)
				{
					if (this._queue.Count > 0)
						message = (Message) this._queue.Dequeue();

					// correct queue total size
					if (message != null)
					{
						this._currentTotalMessages --;
						this._currentTotalSize -= message.EffectiveMessageSize;

						// reflect the change in the queue state
						if (message.Recipient != null)
							message.Recipient.QueueLength = this._queue.Count;

						// LOG:
						BinaryLogWriter binaryLogWriter = this._iTransportContext.BinaryLogWriter;
						if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
							binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "MessageContainer.GetMessage",
								LogMessageType.MessageHasBeenDequeued, null, message, message.Recipient, null,
								GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null, -1, 0, 0, 0, null, null, null, null,
								"The message has been dequeued. Messages in the queue: {0}. Queue size: {1}.",
								this._currentTotalMessages, this._currentTotalSize);
					}
				}
			}

			return message;
		}

		private bool _disposed = false;
		private ReaderWriterLock _disposeLock = new ReaderWriterLock();
		private Exception _disposeException;

		/// <summary>
		/// Releases all messages.
		/// </summary>
		/// <param name="reason">Exception that causes disposing.</param>
		public void Dispose(Exception reason)
		{
			if (reason == null)
				reason = GenuineExceptions.Get_Processing_TransportConnectionFailed();

			// stop adding messages to this container
			using(new WriterAutoLocker(this._disposeLock))
			{
				if (this._disposed)
					return ;

				this._disposed = true;
				this._disposeException = reason;
			}

			// and release all wait cell with the provided reason
			while(this._queue.Count > 0)
			{
				Message message = (Message) this._queue.Dequeue();

				message.SyncWaitException = reason;
				if (message.ReplyToId < 0)
					message.ITransportContext.IIncomingStreamHandler.DispatchException(message, reason);

				if (message.ConnectionAvailable != null)
					message.ConnectionAvailable.Set();
				message.Dispose();
			}
		}

		#endregion

	}
}
