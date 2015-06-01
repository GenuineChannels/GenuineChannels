/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Serialization;

using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Receiving;
using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.DirectExchange
{
	/// <summary>
	/// Implements universal asynchronous response processor that redirects the response to the 
	/// specified recipient.
	/// </summary>
	internal class UniversalAsyncResponseProcessor : IResponseProcessor
	{
		/// <summary>
		/// Initializes the instance of the UniversalAsyncResponseProcessor class.
		/// </summary>
		/// <param name="message">The source message.</param>
		/// <param name="streamResponseEventHandler">The response handler represented as a callback or a null reference.</param>
		/// <param name="iStreamResponseHandler">The response handler represented as a object supporting the IStreamResponseHandler interface or a null reference.</param>
		public UniversalAsyncResponseProcessor(Message message, StreamResponseEventHandler streamResponseEventHandler, IStreamResponseHandler iStreamResponseHandler)
		{
			this._message = message;
			this._streamResponseEventHandler = streamResponseEventHandler;
			this._iStreamResponseHandler = iStreamResponseHandler;
		}

		/// <summary>
		/// Releases acquired resources.
		/// </summary>
		public void Dispose()
		{
			this._message = null;
			this._streamResponseEventHandler = null;
			this._iStreamResponseHandler = null;
		}

		private Message _message;
		private bool _isDispatched = false;
		private StreamResponseEventHandler _streamResponseEventHandler;
		private IStreamResponseHandler _iStreamResponseHandler;

		/// <summary>
		/// Directs the message to the message receiver.
		/// </summary>
		/// <param name="message">The response.</param>
		public void ProcessRespose(Message message)
		{
			lock(this)
			{
				if (this._isDispatched)
					return ;

				this._isDispatched = true;
			}

			if (message.ContainsSerializedException)
			{
				BinaryFormatter binaryFormatter = new BinaryFormatter();
				Exception exception = (Exception) binaryFormatter.Deserialize(message.Stream);
				if (this._streamResponseEventHandler != null)
					this._streamResponseEventHandler(exception, this._message.Recipient, this._message.Tag);
				if (_iStreamResponseHandler != null)
					_iStreamResponseHandler.HandleException(exception, this._message.Recipient, this._message.Tag);
			}
			else
			{
				if (this._streamResponseEventHandler != null)
					this._streamResponseEventHandler(message.Stream, this._message.Recipient, this._message.Tag);
				if (_iStreamResponseHandler != null)
					_iStreamResponseHandler.HandleResponse(message.Stream, this._message.Recipient, this._message.Tag);
			}
		}

		/// <summary>
		/// Dispatches the exception to the handler.
		/// </summary>
		/// <param name="exceptionAsObject">The exception.</param>
		public void DispatchException(object exceptionAsObject)
		{
			lock(this)
			{
				if (this._isDispatched)
					return ;

				this._isDispatched = true;
			}

			if (this._streamResponseEventHandler != null)
				this._streamResponseEventHandler(exceptionAsObject, this._message.Recipient, this._message.Tag);
			if (_iStreamResponseHandler != null)
				_iStreamResponseHandler.HandleException(exceptionAsObject as Exception, this._message.Recipient, this._message.Tag);
		}

		/// <summary>
		/// Indicates whether this message processor still waits for the response(s).
		/// </summary>
		/// <param name="now">The current time.</param>
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
		/// Gets the uri of the remote host which is expected to send a response.
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
