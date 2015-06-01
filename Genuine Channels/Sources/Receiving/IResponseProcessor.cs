/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.Receiving
{
	/// <summary>
	/// Describes a role of a message processor that processes incoming messages.
	/// </summary>
	public interface IResponseProcessor : IDisposable
	{
		/// <summary>
		/// Directs the message to the message receiver.
		/// </summary>
		/// <param name="message">The response.</param>
		void ProcessRespose(Message message);

		/// <summary>
		/// An exception occurred during processing.
		/// </summary>
		/// <param name="exceptionAsObject">Error reason.</param>
		void DispatchException(object exceptionAsObject);

		/// <summary>
		/// Indicates whether this message processor still waits for the response/responses.
		/// </summary>
		/// <param name="now">The current time elapsed since the system started.</param>
		/// <returns>True if the message processor still waits for the response.</returns>
		bool IsExpired (int now);

		/// <summary>
		/// Gets the uri of the remote host expected to send a response.
		/// </summary>
		HostInformation Remote { get; }

		/// <summary>
		/// Gets an indication whether the response processor does not require a separate thread for processing.
		/// </summary>
		bool IsShortInProcessing { get; }

		/// <summary>
		/// Gets the initial message for which the response is expected. Is used only for debugging purposes to track down
		/// the source message.
		/// </summary>
		Message Message { get; }
	}
}
