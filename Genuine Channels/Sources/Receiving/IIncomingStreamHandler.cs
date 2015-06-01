/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.IO;
using System.Web;

using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.GenuineHttp;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Security;
using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.Receiving
{
	/// <summary>
	/// Provides means to read incoming requests and responses and informs the consumer about
	/// transport and security problems and timeouts.
	/// </summary>
	public interface IIncomingStreamHandler
	{
		/// <summary>
		/// Processes incoming requests and responses.
		/// Automatically closes the provided stream after using.
		/// </summary>
		/// <param name="stream">The stream containing a request or a response.</param>
		/// <param name="remote">The remote host.</param>
		/// <param name="genuineConnectionType">Type of the connection.</param>
		/// <param name="connectionName">Connection id to send a response through.</param>
		/// <param name="dbgConnectionId">The identifier of the connection, which is used for debugging purposes only.</param>
		/// <param name="useThisThread">True to invoke the target in the current thread.</param>
		/// <param name="iMessageRegistrator">The message registrator.</param>
		/// <param name="connectionLevelSecuritySession">Connection Level Security Session.</param>
		/// <param name="httpServerRequestResult">The HTTP request through which the message was received.</param>
		/// <returns>True if it was a one-way message.</returns>
		bool HandleMessage(Stream stream, HostInformation remote, GenuineConnectionType genuineConnectionType, string connectionName, int dbgConnectionId, bool useThisThread, IMessageRegistrator iMessageRegistrator, SecuritySession connectionLevelSecuritySession, HttpServerRequestResult httpServerRequestResult);

		/// <summary>
		/// Invokes the target or dispatches the response according to message content.
		/// Throws exception on any errors.
		/// </summary>
		/// <param name="message">The message being processed.</param>
		void HandleMessage_AfterCLSS(Message message);

		/// <summary>
		/// Invokes the target or dispatches the response according to message content.
		/// Throws exception on any errors.
		/// </summary>
		/// <param name="message">The message being processed.</param>
		void HandleMessage_Final(Message message);

		/// <summary>
		/// Dispatches the exception to all response processors awaiting something from the specific remote host.
		/// </summary>
		/// <param name="hostInformation">The remote host.</param>
		/// <param name="exception">The exception.</param>
		void DispatchException(HostInformation hostInformation, Exception exception);

		/// <summary>
		/// Dispatches the exception to a response processor specified by the replyId value.
		/// </summary>
		/// <param name="sourceMessage">The source message id.</param>
		/// <param name="exception">The exception</param>
		void DispatchException(Message sourceMessage, Exception exception);

		/// <summary>
		/// Registers a response processor that waits for messages containing a response to the 
		/// message with the specified identifier.
		/// </summary>
		/// <param name="replyId">ID of the source message.</param>
		/// <param name="iResponseProcessor">A response processor instance.</param>
		void RegisterResponseProcessor(int replyId, IResponseProcessor iResponseProcessor);

		/// <summary>
		/// True if there is a handler awaiting for the response to the message with the specified id.
		/// </summary>
		/// <param name="replyId">The id of the source message.</param>
		/// <returns>True if there is a handler awaiting for the response to the message with the specified id.</returns>
		bool IsHandlerAlive(int replyId);

		/// <summary>
		/// Associates the specified transport user with the specified name.
		/// </summary>
		/// <param name="name">The name to associate the transport user with.</param>
		/// <param name="transportUser">The transport user.</param>
		void RegisterTransportUser(string name, ITransportUser transportUser);
	}
}
