/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Serialization;

using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Receiving;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Utilities;

namespace Belikov.GenuineChannels.DirectExchange
{
	/// <summary>
	/// Implements synchronous response receiver.
	/// </summary>
	internal class SyncResponseProcessorWithEvent : IResponseProcessor
	{
		/// <summary>
		/// Constructs an instance of the SyncResponseProcessorWithEvent class.
		/// </summary>
		/// <param name="message">The request.</param>
		public SyncResponseProcessorWithEvent(Message message)
		{
			this._message = message;
			this.IsReceivedEvent = EventPool.ObtainEvent();
		}

		/// <summary>
		/// Releases acquired resources.
		/// </summary>
		public void Dispose()
		{
			lock (this)
			{
				EventPool.RecycleEvent(this.IsReceivedEvent);
				this.IsReceivedEvent = EventPool.GlobalStubEvent;
			}
		}

		private Message _message;
		private bool _isDispatched = false;

		/// <summary>
		/// Is set when the response is received or an exception is dispatched.
		/// </summary>
		public ManualResetEvent IsReceivedEvent
		{
			get
			{
				lock (this)
					return this._isReceivedEvent;
			}
			set
			{
				lock (this)
					this._isReceivedEvent = value;
			}
		}
		private ManualResetEvent _isReceivedEvent;

		/// <summary>
		/// The received response.
		/// </summary>
		public Message Response;

		/// <summary>
		/// The exception dispatched instead of the response.
		/// </summary>
		public Exception DispatchedException;

		/// <summary>
		/// Directs the message to the message receiver.
		/// </summary>
		/// <param name="message">The response.</param>
		public void ProcessRespose(Message message)
		{
			lock (this)
			{
				if (this._isDispatched)
					return ;

				this._isDispatched = true;
			}

			if (message.ContainsSerializedException)
			{
				BinaryFormatter binaryFormatter = new BinaryFormatter();
				this.DispatchedException = (Exception) binaryFormatter.Deserialize(message.Stream);
			}
			else
				this.Response = message;
			this.IsReceivedEvent.Set();
		}

		/// <summary>
		/// Dispatches the exception to the caller.
		/// </summary>
		/// <param name="exceptionAsObject">The exception.</param>
		public void DispatchException(object exceptionAsObject)
		{
			lock (this)
			{
				if (this._isDispatched)
					return ;

				this._isDispatched = true;
			}

			this.DispatchedException = exceptionAsObject as Exception;
			this.IsReceivedEvent.Set();
		}

		/// <summary>
		/// Indicates whether this message processor still waits for the response(s).
		/// </summary>
		/// <param name="now">The current time elapsed since the system started.</param>
		/// <returns>True if the message processor still waits for the response(s).</returns>
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
				return this._message.Sender;
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
