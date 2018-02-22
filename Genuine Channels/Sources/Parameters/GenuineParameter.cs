/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

namespace Belikov.GenuineChannels.Parameters
{
	/// <summary>
	/// Enumerates all parameters used in Genuine Channel solution.
	/// </summary>
	public enum GenuineParameter
	{
		#region -- Message parameters --------------------------------------------------------------

		/// <summary>
		/// [INT] The maximum size of the content being sent at once.
		/// </summary>
		MaxContentSize,

		/// <summary>
		/// [INT] The maximum number of items stored in a queue.
		/// </summary>
		MaxQueuedItems,

		/// <summary>
		/// [INT] The maximum summary size of all messages stored in a queue.
		/// </summary>
		MaxTotalSize,

		/// <summary>
		/// [BOOL] Cancels message size checking. As a result, Genuine Channels message engine does
		/// not check the size of the stream and this increases performance for messages containing
		/// streams without Stream.Length property support (no exceptions are thrown and caught).
		/// </summary>
		NoSizeChecking,

		/// <summary>
		/// [BOOL] Whether to enable a Security Session with compression at the channel level.
		/// </summary>
		Compression,

		/// <summary>
		/// [TimeSpan] The message timeout. An exception will be dispatched to the invocation context
		/// if a response to the message is not received within time period specified by this value.
		/// </summary>
		InvocationTimeout,

		/// <summary>
		/// [BOOL] Indicates whether to force synchronous mode for delivering all responses.
		/// </summary>
		SyncResponses,

		#endregion

		#region -- Common transport parameters -----------------------------------------------------

		/// <summary>
		/// [TimeSpan] The maximum time span to establish or accept a connection.
		/// </summary>
		ConnectTimeout,

		/// <summary>
		/// [STR] The name of the key provider to create a Security Session used at the connection level.
		/// </summary>
		SecuritySessionForPersistentConnections,

		/// <summary>
		/// [STR] The name of the key provider to create a Security Session used at the connection level.
		/// </summary>
		SecuritySessionForNamedConnections,

		/// <summary>
		/// [STR] The name of the key provider to create a Security Session used at the connection level.
		/// </summary>
		SecuritySessionForInvocationConnections,
		
		/// <summary>
		/// [TimeSpan] The time span value to close opened or accepted persistent connections after this period of inactivity.
		/// </summary>
		ClosePersistentConnectionAfterInactivity,

		/// <summary>
		/// [TimeSpan] The time span value to close opened or accepted named connections after this period of inactivity. TimeSpan.Zero value means immediate connection closing after receiving a response.
		/// </summary>
		CloseNamedConnectionAfterInactivity,

		/// <summary>
		/// [TimeSpan] The time span value to close opened or accepted invocation connections after this period of inactivity. TimeSpan.Zero value means immediate connection closing after receiving a response.
		/// </summary>
		CloseInvocationConnectionAfterInactivity,

		/// <summary>
		/// [TimeSpan] The time span value to close opened or accepted one-way connection after, doesn't matter whether something is being sent or received through it.
		/// </summary>
		CloseOneWayConnectionAfterInactivity,

		/// <summary>
		/// [TimeSpan] An empty message (6 byte) is sent to the remote host if there were no messages sent to the remote host within this time span.
		/// </summary>
		PersistentConnectionSendPingAfterInactivity,

		/// <summary>
		/// [TimeSpan] The time span before considering the persistent connection to be broken if it wasn't reestablished.
		/// </summary>
		MaxTimeSpanToReconnect,

		/// <summary>
		/// [INT] The maximum number of reconnection tries before declaring a connection to be closed.
		/// </summary>
		ReconnectionTries,

		/// <summary>
		/// [TimeSpan] Time span to sleep for after each reconnection failure.
		/// </summary>
		SleepBetweenReconnections,

//		/// <summary>
//		/// [Boolean] The true value of this parameter turns off the check on whether the remote host has been restarted.
//		/// It is not recommended to set this parameter to true.
//		/// </summary>
//		IgnoreRemoteHostUriChanges,

		#endregion

		#region -- TCP transport parameters --------------------------------------------------------

		/// <summary>
		/// [INT] The maximum size of packet being sent at once.
		/// </summary>
		TcpMaxSendSize,

		/// <summary>
		/// [BOOL] Indicates whether a connection manager should read the entire incoming request 
		/// into an intermediate storage before processing. This leads to increased memory 
		/// comsuption and reduced performance.
		/// </summary>
		TcpReadRequestBeforeProcessing,

		/// <summary>
		/// [BOOL] Represents a value indicating whether the synchronous message is to be resent if a connection
		/// is reestablished.
		/// </summary>
		TcpDoNotResendMessages,

		/// <summary>
		/// [BOOL] Represents a value indicating whether the GTCP Connection Manager must disable naggling for opened and accepted connections.
		/// </summary>
		TcpDisableNagling,

		/// <summary>
		/// [BOOL] Represents a value indicating whether the GTCP Connection Manager must send short messages to the server in order to prevent delayed acknowledgement.
		/// </summary>
		TcpPreventDelayedAck,

		/// <summary>
		/// [INT] Specifies the size of the receive buffer of the TCP socket.
		/// </summary>
		TcpReceiveBufferSize,

		/// <summary>
		/// [INT] Specifies the size of the send buffer of the TCP socket.
		/// </summary>
		TcpSendBufferSize,

        /// <summary>
        /// [BOOL] Indicates whether to use TCP port sharing. If set the listener port will be shared (other apps can use the same one).
        /// In this case the socket option <seealso cref="System.Net.Sockets.SocketOptionName.ReuseAddress"/> will be applied.
        /// Default value is false.
        /// </summary>
        TcpReuseAddressPort,

        /// <summary>
        /// [BOOL] The TCP dual socket mode will enable both IPv4 and IPv6 for the socket listener (Vista and Longhorn above only).
        /// This will be true by default. If you like to force IPv4 or IPv6 listening only, set this option to false.
        /// </summary>
        TcpDualSocketMode,

        #endregion

        #region -- Shared memory -------------------------------------------------------------------

        /// <summary>
        /// [INT] Size of the share in bytes.
        /// </summary>
        SMShareSize,

		/// <summary>
		/// [TimeSpan] The maximum time span within which the message must be completely received by a remote host.
		/// </summary>
		SMSendTimeout,

		/// <summary>
		/// [BOOL] Represents a value indicating whether the GShMem channel must use session-local objects.
		/// </summary>
		SMSessionLocal,

		#endregion

		#region -- UDP transport parameters --------------------------------------------------------

		/// <summary>
		/// [String] The address to join the multicast socket to. For example, 227.37.37.37.
		/// </summary>
		UdpJoinTo,

		/// <summary>
		/// [INT] TTL of IP packets.
		/// </summary>
		UdpTtl,

		/// <summary>
		/// [INT] Socket's low water mark specifying the size of the receive buffer.
		/// </summary>
		UdpReceiveBuffer,

		/// <summary>
		/// [INT] The maximum size of a packet.
		/// </summary>
		UdpPacketSize,

		/// <summary>
		/// [INT] The maximum possible size of the packet.
		/// </summary>
		UdpMtu,

		/// <summary>
		/// [String] The network to direct the IGMP packets to. For example, gbudp://227.37.37.37:11000.
		/// </summary>
		UdpMulticastTo,

		/// <summary>
		/// [TimeSpan] The time period to accumulate the incoming stream. If all packets aren't received within this time span, the message is lost.
		/// </summary>
		UdpAssembleTimeSpan,

		#endregion

		#region -- HTTP transport parameters -------------------------------------------------------

		/// <summary>
		/// [String] The uri of the proxy.
		/// </summary>
		HttpProxyUri,

		/// <summary>
		/// [String] The name of the web user agent.
		/// </summary>
		HttpWebUserAgent,

		/// <summary>
		/// [String] The user name associated with the credentials.
		/// </summary>
		HttpAuthUserName,

		/// <summary>
		/// [String] The password for the user associated with the credentials.
		/// </summary>
		HttpAuthPassword,

		/// <summary>
		/// [String] The domain associated with the credentials.
		/// </summary>
		HttpAuthDomain,

		/// <summary>
		/// [NetworkCredential] The network credential provided for HTTP authentication.
		/// </summary>
		HttpAuthCredential,

		/// <summary>
		/// [BOOL] Indicates whether to use proxy server specified by GlobalProxySelection.Select property value.
		/// </summary>
		HttpUseGlobalProxy,

		/// <summary>
		/// [BOOL] Indicates the state of the WebProxy.BypassProxyOnLocal property.
		/// </summary>
		HttpBypassOnLocal,

		/// <summary>
		/// [BOOL] Indicates if it is necessary to use the default credential.
		/// </summary>
		HttpUseDefaultCredentials,

		/// <summary>
		/// [BOOL] Indicates if it is necessary to enable the write stream buffering.
		/// </summary>
		HttpAllowWriteStreamBuffering,

		/// <summary>
		/// [BOOL] Framework 1.1 only option allowing to enable high-speed NTLM-authenticated connection sharing.
		/// </summary>
		HttpUnsafeConnectionSharing,

		/// <summary>
		/// [INT] The size of the recommended HTTP request sent to the server at a time. Http client connection manager stops adding messages to the request as soon as its size exceeds this value.
		/// </summary>
		HttpRecommendedPacketSize,

		/// <summary>
		/// [BOOL] Enables or disables HTTP keep-alive connection mode (HTTP 1.1 only). It is highly recommended to specify the true value always.
		/// </summary>
		HttpKeepAlive,

		/// <summary>
		/// [BOOL] Enables or disables security context impersonation if HTTP Basic or Integrated Windows authentication is used.
		/// </summary>
		HttpAuthentication,

		/// <summary>
		/// [BOOL] Enables or disables the storing and providing HttpContext during invocations processed by GHTTP server channel hosted inside IIS.
		/// </summary>
		HttpStoreAndProvideHttpContext,

		/// <summary>
		/// [TimeSpan] Defines the maximum time period of WebRequest initialization.
		/// </summary>
		HttpWebRequestInitiationTimeout,

		/// <summary>
		/// [TimeSpan] Defines the maximum period of time taken by an asynchronous I/O operation.
		/// </summary>
		HttpAsynchronousRequestTimeout,

		/// <summary>
		/// [String] The MIME Media type of the request.
		/// </summary>
		HttpMimeMediaType,

		#endregion

		#region -- XHTTP transport parameters ------------------------------------------------------

		/// <summary>
		/// [TimeSpan] Specifies the maximum period of time within which an entire HTTP message must be processed.
		/// </summary>
		XHttpReadHttpMessageTimeout,

		#endregion

		#region -- Security Parameters -------------------------------------------------------------

		/// <summary>
		/// [BOOL] If true, Connection Manager will hold on a separate thread for establishing
		/// Security Session by means of synchronous messages.
		/// </summary>
		HoldThreadDuringSecuritySessionEstablishing,

		#endregion

		#region -- Logging -------------------------------------------------------------------------

		/// <summary>
		/// [INT] Creates and starts a log record writer that writes records into MemoryWritingStream.
		/// The value of this parameter determines the maximum possible amount of memory occupied by log records.
		/// </summary>
		EnableGlobalLoggingToMemory,

		/// <summary>
		/// [STR] Creates and starts a log writer that writes log records into the specified file.
		/// </summary>
		EnableGlobalLoggingToFile,

		/// <summary>
		/// [STR] Specifies what logging records will be written.
		/// </summary>
		LoggingParameters,

		#endregion

		#region -- Versioning ----------------------------------------------------------------------

		/// <summary>
		/// [INT] The level of compatibility determines what features will be used.
		/// 0 means that 2.4.x clients are supported.
		/// 1 means that only 2.5.x clients are supported.
		/// </summary>
		CompatibilityLevel,

		#endregion

		/// <summary>
		/// Indicates number of parameters.
		/// </summary>
		LastParameter,
	}
}
