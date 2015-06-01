/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Serialization;

using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Receiving;
using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.DotNetRemotingLayer
{
	/// <summary>
	/// Processes replies to the .NET Remoting invocations sent synchronously.
	/// </summary>
	internal class SyncSinkStackResponseProcessor : IResponseProcessor
	{
		/// <summary>
		/// Initializes the instance of the SyncSinkStackResponseProcessor class.
		/// </summary>
		/// <param name="iTransportContext">The Transport Context.</param>
		/// <param name="message">The source message.</param>
		public SyncSinkStackResponseProcessor(ITransportContext iTransportContext, Message message)
		{
			this._iTransportContext = iTransportContext;
			this._message = message;
		}

		/// <summary>
		/// Releases acquired resources.
		/// </summary>
		public void Dispose()
		{
		}

		private ITransportContext _iTransportContext;
		private Message _message;
		private bool _isDispatched = false;

		/// <summary>
		/// Is set when the response has been received.
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
		private ManualResetEvent _isReceivedEvent = new ManualResetEvent(false);

		/// <summary>
		/// The received response.
		/// </summary>
		public Message Response
		{
			get
			{
				lock (this)
					return this._response;
			}
			set
			{
				lock (this)
					this._response = value;
			}
		}
		private Message _response;

		/// <summary>
		/// The exception dispatched instead of the response.
		/// </summary>
		public Exception DispatchedException
		{
			get
			{
				lock (this)
					return this._dispatchedException;
			}
			set
			{
				lock (this)
					this._dispatchedException = value;
			}
		}
		private Exception _dispatchedException;

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

				if (message.ContainsSerializedException)
				{

					try
					{
						BinaryFormatter binaryFormatter = new BinaryFormatter();
						this.DispatchedException = (Exception) binaryFormatter.Deserialize(message.Stream);
					}
					catch(Exception ex)
					{
						this.DispatchedException = ex;
					}
				}
				else
					this.Response = message;
				this.IsReceivedEvent.Set();
			}
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
		/// <returns>True if the message processor still waits for the response.</returns>
		public bool IsExpired (int now)
		{
			lock (this)
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
				return true;
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
