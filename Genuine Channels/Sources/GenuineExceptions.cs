/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Runtime.Serialization;

namespace Belikov.GenuineChannels
{
	/// <summary>
	/// Utility that centralizes all exceptions.
	/// </summary>
	public class GenuineExceptions
	{
		#region -- Exceptions ----------------------------------------------------------------------

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		/// <param name="packetSize">Size of the packet being sent.</param>
		/// <param name="maxContentSize">Maximum allowable packet size.</param>
		public static Exception Get_Send_TooLargePacketSize(int packetSize, int maxContentSize)
		{
			string errorIdentifier = "GenuineChannels.Exception.Send.TooLargePacketSize";
			return new TooLargePacketSize(null, errorIdentifier, GetLocalizedErrorOrDefaultText( errorIdentifier, 
				"Transport sink error: too large packet size to send. Packet size: {0}. MaxContentSize: {1}. You should increase the MaxContentSize channel parameter or use the chunk-by-chunk delivery algorithm to avoid this exception.", packetSize, maxContentSize));
		}

		/// <summary>
		/// The exception that is thrown when a too large message is being sent.
		/// </summary>
		[Serializable]
			public class TooLargePacketSize : OperationException
		{
			/// <summary>
			/// Contructs an instance of the TooLargePacketSize class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public TooLargePacketSize(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new TooLargePacketSize(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the TooLargePacketSize class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public TooLargePacketSize(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the TooLargePacketSize class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public TooLargePacketSize(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		public static Exception Get_Send_TransportProblem()
		{
			string errorIdentifier = "GenuineChannels.Exception.Send.TransportProblem";
			return new SendTransportProblem(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "Transport level error. Can not send the packet through the transport."));
		}

		/// <summary>
		/// The exception that is thrown when the message cannot be delivered
		/// because the socket.Send operation informs that zero bytes have been sent.
		/// </summary>
		[Serializable]
			public class SendTransportProblem : OperationException
		{
			/// <summary>
			/// Contructs an instance of the SendTransportProblem class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public SendTransportProblem(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new SendTransportProblem(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the SendTransportProblem class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public SendTransportProblem(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the SendTransportProblem class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public SendTransportProblem(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		public static Exception Get_Send_ServerDidNotReply()
		{
			string errorIdentifier = "GenuineChannels.Exception.Send.ServerDidNotReply";
			return new ServerDidNotReply(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The response has not been received within the specified period of time."));
		}

		/// <summary>
		/// The exception that is thrown when the server does not reply within the specified period of time.
		/// </summary>
		[Serializable]
			public class ServerDidNotReply : OperationException
		{
			/// <summary>
			/// Contructs an instance of the ServerDidNotReply class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public ServerDidNotReply(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new ServerDidNotReply(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the ServerDidNotReply class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public ServerDidNotReply(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the ServerDidNotReply class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public ServerDidNotReply(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		public static Exception Get_Send_Timeout()
		{
			string errorIdentifier = "GenuineChannels.Exception.Send.Timeout";
			return new SendTimeout(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The message has not been sent within the specified period of time."));
		}

		/// <summary>
		/// The exception that is thrown when the timeout of the send operation elapses.
		/// </summary>
		[Serializable]
			public class SendTimeout : OperationException
		{
			/// <summary>
			/// Contructs an instance of the SendTimeout class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public SendTimeout(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new SendTimeout(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the SendTimeout class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public SendTimeout(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the SendTimeout class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public SendTimeout(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		/// <param name="destination">Destination</param>
		public static Exception Get_Send_NoSender(string destination)
		{
			// may be it's a good idea not to show channel uri to an end user?
			string errorIdentifier = "GenuineChannels.Exception.Send.NoSender";
			return new SendNoSender(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "Connection to the remote host {0} has been closed.", destination));
		}

		/// <summary>
		/// The exception that is thrown when no connection available that can deliver the message to the specified destination.
		/// </summary>
		[Serializable]
			public class SendNoSender : OperationException
		{
			/// <summary>
			/// Contructs an instance of the SendNoSender class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public SendNoSender(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new SendNoSender(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the SendNoSender class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public SendNoSender(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the SendNoSender class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public SendNoSender(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		/// <param name="destination">Destination</param>
		public static Exception Get_Send_DestinationIsUnreachable(string destination)
		{
			string errorIdentifier = "GenuineChannels.Exception.Send.DestinationIsUnreachable";
			return new DestinationIsUnreachable(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "Can not establish a connection to the remote host \"{0}\".", destination));
		}

		/// <summary>
		/// The exception that is thrown when the destination is unreachable.
		/// </summary>
		[Serializable]
			public class DestinationIsUnreachable : OperationException
		{
			/// <summary>
			/// Contructs an instance of the DestinationIsUnreachable class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public DestinationIsUnreachable(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new DestinationIsUnreachable(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the DestinationIsUnreachable class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public DestinationIsUnreachable(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the DestinationIsUnreachable class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public DestinationIsUnreachable(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		/// <param name="connectionName">Name of the connection</param>
		public static Exception Get_Send_NoNamedConnectionFound(string connectionName)
		{
			string errorIdentifier = "GenuineChannels.Exception.Send.NoNamedConnectionFound";
			return new NoNamedConnectionFound(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The requested connection is not found. Probably, it has been closed. The name of the requested connection: {0}", connectionName));
		}

		/// <summary>
		/// The exception that is thrown when the specified named connection cannot be found.
		/// </summary>
		[Serializable]
			public class NoNamedConnectionFound : OperationException
		{
			/// <summary>
			/// Contructs an instance of the NoNamedConnectionFound class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public NoNamedConnectionFound(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new NoNamedConnectionFound(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the NoNamedConnectionFound class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public NoNamedConnectionFound(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the NoNamedConnectionFound class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public NoNamedConnectionFound(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		public static Exception Get_Send_QueueIsOverloaded(int maxQueuedItems, int currentlyQueuedItems, int maxTotalSize, int currentTotalSize)
		{
			string errorIdentifier = "GenuineChannels.Exception.Send.QueueIsOverloaded";
			return new QueueIsOverloaded(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The message queue has been overrun. Transport connection(s) to this remote host will be closed. MaxQueuedItems: {0}. The current number of queued messages: {1}. MaxTotalSize: {2}. The current size of the queue: {3}.",
				maxQueuedItems, currentlyQueuedItems, maxTotalSize, currentTotalSize));
		}

		/// <summary>
		/// The exception that is thrown when a queue is overrun.
		/// </summary>
		[Serializable]
			public class QueueIsOverloaded : OperationException
		{
			/// <summary>
			/// Contructs an instance of the QueueIsOverloaded class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public QueueIsOverloaded(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new QueueIsOverloaded(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the QueueIsOverloaded class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public QueueIsOverloaded(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the QueueIsOverloaded class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public QueueIsOverloaded(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		/// <param name="url">Url.</param>
		/// <param name="systemReason">Windows error message.</param>
		public static Exception Get_Connect_CanNotConnectToRemoteHost(string url, string systemReason)
		{
			string errorIdentifier = "GenuineChannels.Exception.CanNotConnectToRemoteHost";
			return new CanNotConnectToRemoteHost(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "Can not connect to the remote host \"{0}\". System error message: {1}.", url, systemReason));
		}

		/// <summary>
		/// The exception that is thrown when the connection to the remote host can not be established.
		/// </summary>
		[Serializable]
			public class CanNotConnectToRemoteHost : OperationException
		{
			/// <summary>
			/// Contructs an instance of the CanNotConnectToRemoteHost class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public CanNotConnectToRemoteHost(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new CanNotConnectToRemoteHost(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the CanNotConnectToRemoteHost class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public CanNotConnectToRemoteHost(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the CanNotConnectToRemoteHost class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public CanNotConnectToRemoteHost(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		/// <param name="hostName">Host name.</param>
		public static Exception Get_Connect_CanNotResolveHostName(string hostName)
		{
			string errorIdentifier = "GenuineChannels.Exception.Connect.CanNotResolveHostName";
			return new CanNotResolveHostName(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "Can not resolve the specified host name to the valid IP address. The specified host name: \"{0}\".", hostName));
		}

		/// <summary>
		/// The exception that is thrown when the Dns.Resolve method fails.
		/// </summary>
		[Serializable]
			public class CanNotResolveHostName : OperationException
		{
			/// <summary>
			/// Contructs an instance of the CanNotResolveHostName class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public CanNotResolveHostName(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new CanNotResolveHostName(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the CanNotResolveHostName class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public CanNotResolveHostName(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the CanNotResolveHostName class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public CanNotResolveHostName(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		public static Exception Get_Connect_RemoteHostDoesNotRespond()
		{
			string errorIdentifier = "GenuineChannels.Exception.Connect.RemoteHostDoesNotRespond";
			return new RemoteHostDoesNotRespond(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The remote host does not respond."));
		}

		/// <summary>
		/// The exception that is thrown when the remote host does not respond to the request.
		/// </summary>
		[Serializable]
			public class RemoteHostDoesNotRespond : OperationException
		{
			/// <summary>
			/// Contructs an instance of the RemoteHostDoesNotRespond class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public RemoteHostDoesNotRespond(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new RemoteHostDoesNotRespond(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the RemoteHostDoesNotRespond class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public RemoteHostDoesNotRespond(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the RemoteHostDoesNotRespond class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public RemoteHostDoesNotRespond(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		/// <param name="reason">Error source.</param>
		public static Exception Get_Connect_CanNotAcceptIncomingConnection(string reason)
		{
			string errorIdentifier = "GenuineChannels.Exception.CanNotAcceptIncomingConnection";
			return new CanNotAcceptIncomingConnection(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "Can not accept an incoming connection: \"{0}\"", reason));
		}

		/// <summary>
		/// The exception that is thrown when a server can not accept the inbound connection.
		/// </summary>
		[Serializable]
			public class CanNotAcceptIncomingConnection : OperationException
		{
			/// <summary>
			/// Contructs an instance of the CanNotAcceptIncomingConnection class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public CanNotAcceptIncomingConnection(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new CanNotAcceptIncomingConnection(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the CanNotAcceptIncomingConnection class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public CanNotAcceptIncomingConnection(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the CanNotAcceptIncomingConnection class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public CanNotAcceptIncomingConnection(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		public static Exception Get_Connect_ConnectionReestablished()
		{
			string errorIdentifier = "GenuineChannels.Exception.Connect.ConnectionReestablished";
			return new ConnectionReestablished(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The remote host has opened another connection and enforced connection reestablishing. The current established connection is being shut down."));
		}

		/// <summary>
		/// The exception that is thrown when a connection can not be reestablished.
		/// </summary>
		[Serializable]
			public class ConnectionReestablished : OperationException
		{
			/// <summary>
			/// Contructs an instance of the ConnectionReestablished class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public ConnectionReestablished(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new ConnectionReestablished(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the ConnectionReestablished class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public ConnectionReestablished(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the ConnectionReestablished class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public ConnectionReestablished(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Returns a predefined exception.
		/// </summary>
		public static Exception Get_Connection_LifetimeCycleEnded()
		{
			string errorIdentifier = "GenuineChannels.Exception.Connection.LifetimeCycleEnded";
			return new LifetimeCycleEnded(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The invocation or one-way connection has been shut down according to Finite Automaton Transition Diagram."));
		}

		/// <summary>
		/// The exception that is thrown when a connection is closed due to expired lifetime.
		/// </summary>
		[Serializable]
			public class LifetimeCycleEnded : OperationException
		{
			/// <summary>
			/// Contructs an instance of the LifetimeCycleEnded class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public LifetimeCycleEnded(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new LifetimeCycleEnded(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the LifetimeCycleEnded class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public LifetimeCycleEnded(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the LifetimeCycleEnded class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public LifetimeCycleEnded(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		public static Exception Get_Host_IncorrectHostUpdate()
		{
			string errorIdentifier = "GenuineChannels.Exception.Receive.IncorrectHostUpdate";
			return new IncorrectHostUpdate(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "It is impossible to update the client's URI or the server's URL if the connection is established."));
		}

		/// <summary>
		/// The exception that is thrown when a host cannot update its URI or URL.
		/// </summary>
		[Serializable]
			public class IncorrectHostUpdate : OperationException
		{
			/// <summary>
			/// Contructs an instance of the IncorrectHostUpdate class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public IncorrectHostUpdate(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new IncorrectHostUpdate(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the IncorrectHostUpdate class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public IncorrectHostUpdate(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the IncorrectHostUpdate class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public IncorrectHostUpdate(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Returns an instance of the predefined exception.
		/// </summary>
		public static Exception Get_Receive_IncorrectData()
		{
			string errorIdentifier = "GenuineChannels.Exception.Receive.IncorrectData";
			return new IncorrectData(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The incoming stream is incorrect. Probably, the remote host uses an incorrect channel or an unexpected Security Session."));
		}

		/// <summary>
		/// The exception that is thrown when an incorrect information is received.
		/// </summary>
		[Serializable]
			public class IncorrectData : OperationException
		{
			/// <summary>
			/// Contructs an instance of the IncorrectData class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public IncorrectData(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new IncorrectData(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the IncorrectData class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public IncorrectData(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the IncorrectData class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public IncorrectData(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Returns an instance of the predefined exception.
		/// </summary>
		public static Exception Get_Receive_IncorrectData(string reason)
		{
			string errorIdentifier = "GenuineChannels.Exception.Receive.IncorrectData";
			return new IncorrectData(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The incoming stream is incorrect. Probably, the remote host uses an incorrect channel or an unexpected Security Session. The reason of the error: {0}.", reason));
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		public static Exception Get_Receive_TooLargePortionToReceive()
		{
			string errorIdentifier = "GenuineChannels.Exception.Receive.TooLargePortionToReceive";
			return new TooLargePortionToReceive(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The client has sent a portion of data, which is too large. The server has declined the request because it requires a larger memory buffer than it is allowed. The better way to do it is to provide Marshal-By-Ref object (for example, Stream) and take small portions from it."));
		}

		/// <summary>
		/// The exception that is thrown when the size of the message exceeds the set boundaries.
		/// </summary>
		[Serializable]
			public class TooLargePortionToReceive : OperationException
		{
			/// <summary>
			/// Contructs an instance of the TooLargePortionToReceive class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public TooLargePortionToReceive(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new TooLargePortionToReceive(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the TooLargePortionToReceive class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public TooLargePortionToReceive(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the TooLargePortionToReceive class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public TooLargePortionToReceive(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		public static Exception Get_Receive_Portion()
		{
			string errorIdentifier = "GenuineChannels.Exception.Receive.IncorrectData";
			return new IncorrectData(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "Incorrect incoming stream. Only part of the expected data has been received. Probably, the connection has been unexpectedly closed."));
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		public static Exception Get_Receive_ConnectionClosed()
		{
			string errorIdentifier = "GenuineChannels.Exception.Receive.ConnectionClosed";
			return new ConnectionClosed(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The connection has been forcibly closed."));
		}

		/// <summary>
		/// The exception that is thrown when the connection is closed.
		/// </summary>
		[Serializable]
			public class ConnectionClosed : OperationException
		{
			/// <summary>
			/// Contructs an instance of the ConnectionClosed class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public ConnectionClosed(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new ConnectionClosed(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the ConnectionClosed class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public ConnectionClosed(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the ConnectionClosed class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public ConnectionClosed(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		public static Exception Get_Receive_ConnectionClosed(string reason)
		{
			string errorIdentifier = "GenuineChannels.Exception.Receive.ConnectionClosed";
			return new ConnectionClosed(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The connection has been forcibly closed. Specified reason: {0}.", reason));
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		/// <param name="destination">Destination.</param>
		public static Exception Get_Receive_NoServices(string destination)
		{
			string errorIdentifier = "GenuineChannels.Exception.Receive.NoServices";

			// may be it's a good idea not to show channel uri to end user?
			return new NoServices(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "There are no services associated with the requested service name (\"{0}\").", destination));
		}

		/// <summary>
		/// The exception that is thrown when the request service is not found.
		/// </summary>
		[Serializable]
			public class NoServices : OperationException
		{
			/// <summary>
			/// Contructs an instance of the NoServices class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public NoServices(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new NoServices(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the NoServices class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public NoServices(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the NoServices class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public NoServices(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		public static Exception Get_Receive_NoServerChannel()
		{
			string errorIdentifier = "GenuineChannels.Exception.Receive.NoServerChannel";
			return new NoServerChannel(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "Can not find a channel with the name \"ghttp\". You must create Genuine HTTP Server channel in order to receive client requests."));
		}

		/// <summary>
		/// The exception that is thrown when there is no channel registered with the name "ghttp".
		/// </summary>
		[Serializable]
			public class NoServerChannel : OperationException
		{
			/// <summary>
			/// Contructs an instance of the NoServerChannel class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public NoServerChannel(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new NoServerChannel(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the NoServerChannel class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public NoServerChannel(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the NoServerChannel class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public NoServerChannel(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		public static Exception Get_Receive_ServerHasBeenRestared()
		{
			string errorIdentifier = "GenuineChannels.Exception.Receive.ServerHasBeenRestarted";
			return new ServerHasBeenRestarted(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The server application has been restarted. It is highly recommended to refresh all data recevied from the server."));
		}

		/// <summary>
		/// The exception that is thrown if a restart of the remote host is detected.
		/// </summary>
		[Serializable]
			public class ServerHasBeenRestarted : OperationException
		{
			/// <summary>
			/// Contructs an instance of the ServerHasBeenRestarted class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public ServerHasBeenRestarted(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new ServerHasBeenRestarted(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the ServerHasBeenRestarted class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public ServerHasBeenRestarted(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the ServerHasBeenRestarted class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public ServerHasBeenRestarted(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		public static Exception Get_Receive_NewSessionDetected()
		{
			string errorIdentifier = "GenuineChannels.Exception.Receive.NewSessionDetected";
			return new ServerHasBeenRestarted(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The remote host has lost all information about us. It is recommended to resubscribe to all remote host's events."));
		}

		/// <summary>
		/// The exception that is thrown if a restart of the remote host is detected.
		/// </summary>
		[Serializable]
			public class NewSessionDetected : OperationException
		{
			/// <summary>
			/// Contructs an instance of the NewSessionDetected class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public NewSessionDetected(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new NewSessionDetected(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the NewSessionDetected class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public NewSessionDetected(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the NewSessionDetected class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public NewSessionDetected(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		public static Exception Get_Receive_ConflictOfConnections()
		{
			string errorIdentifier = "GenuineChannels.Exception.Receive.ConflictOfConnections";
			return new ConflictOfConnections(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The server has reported about several connections with the same type opened by the same client. This connection will be terminated."));
		}

		/// <summary>
		/// The exception that is thrown when a conflict of connections has been detected.
		/// </summary>
		[Serializable]
			public class ConflictOfConnections : OperationException
		{
			/// <summary>
			/// Contructs an instance of the ConflictOfConnections class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public ConflictOfConnections(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new ConflictOfConnections(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the ConflictOfConnections class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public ConflictOfConnections(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the ConflictOfConnections class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public ConflictOfConnections(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		/// <param name="reason">Error source.</param>
		/// <returns>An instance of the object representing the exception.</returns>
		public static Exception Get_Processing_LogicError(string reason)
		{
			string errorIdentifier = "GenuineChannels.Exception.Processing.LogicError";
			return new LogicError(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The logic assertion has failed: \"{0}\".", reason));
		}

		/// <summary>
		/// The exception that is thrown when a false assertion is found.
		/// </summary>
		[Serializable]
			public class LogicError : OperationException
		{
			/// <summary>
			/// Contructs an instance of the LogicError class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public LogicError(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new LogicError(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the LogicError class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public LogicError(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the LogicError class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public LogicError(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		public static Exception Get_Processing_NoSessionAvailable()
		{
			string errorIdentifier = "GenuineChannels.Exception.Processing.NoSessionAvailable";
			return new NoSessionAvailable(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The session context is not established. Either it was a one-way message or you are not processing a client request."));
		}

		/// <summary>
		/// The exception that is thrown when the requested Security Session is not available.
		/// </summary>
		[Serializable]
			public class NoSessionAvailable : OperationException
		{
			/// <summary>
			/// Contructs an instance of the NoSessionAvailable class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public NoSessionAvailable(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new NoSessionAvailable(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the NoSessionAvailable class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public NoSessionAvailable(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the NoSessionAvailable class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public NoSessionAvailable(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		public static Exception Get_Processing_ThreadPoolLimitExceeded()
		{
			string errorIdentifier = "GenuineChannels.Exception.Processing.ThreadPoolLimitExceeded";
			return new ThreadPoolLimitExceeded(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The specified limit of threads has been exceeded."));
		}

		/// <summary>
		/// The exception that is thrown when a Thread Pool is exceeded.
		/// </summary>
		[Serializable]
			public class ThreadPoolLimitExceeded : OperationException
		{
			/// <summary>
			/// Contructs an instance of the ThreadPoolLimitExceeded class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public ThreadPoolLimitExceeded(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new ThreadPoolLimitExceeded(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the ThreadPoolLimitExceeded class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public ThreadPoolLimitExceeded(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the ThreadPoolLimitExceeded class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public ThreadPoolLimitExceeded(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Returns a predefined exception to throw.
		/// </summary>
		public static Exception Get_Processing_TransportConnectionFailed()
		{
			string errorIdentifier = "GenuineChannels.Exception.Processing.TransportConnectionFailed";
			return new TransportConnectionFailed(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The connection to the remote host has been closed."));
		}

		/// <summary>
		/// The exception that is thrown when the transport connection fails.
		/// </summary>
		[Serializable]
			public class TransportConnectionFailed : OperationException
		{
			/// <summary>
			/// Contructs an instance of the TransportConnectionFailed class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public TransportConnectionFailed(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new TransportConnectionFailed(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the TransportConnectionFailed class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public TransportConnectionFailed(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the TransportConnectionFailed class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public TransportConnectionFailed(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Returns a predefined exception to throw.
		/// </summary>
		/// <param name="uri">The URI of the remote host.</param>
		/// <param name="url">The URL of the remote host.</param>
		public static Exception Get_Processing_HostResourcesReleased(string uri, string url)
		{
			string errorIdentifier = "GenuineChannels.Exception.Processing.HostResourcesReleased";
			return new HostResourcesReleased(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "All resources associated with the remote host have been released. URI: {0}. URL: {1}.", uri, url));
		}

		/// <summary>
		/// The exception that is thrown when the host resources were released.
		/// </summary>
		[Serializable]
			public class HostResourcesReleased : OperationException
		{
			/// <summary>
			/// Contructs an instance of the HostResourcesReleased class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public HostResourcesReleased(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new HostResourcesReleased(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the HostResourcesReleased class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public HostResourcesReleased(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the HostResourcesReleased class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public HostResourcesReleased(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		/// <param name="url">Url.</param>
		public static Exception Get_Server_IncorrectAddressToListen(string url)
		{
			if (url == null)
				url = string.Empty;

			string errorIdentifier = "GenuineChannels.Exception.Server.IncorrectUrl";
			return new IncorrectUrl(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "An incorrect URI to listen to: \"{0}\".", url));
		}

		/// <summary>
		/// The exception that is thrown when an incorrect url is provided.
		/// </summary>
		[Serializable]
			public class IncorrectUrl : OperationException
		{
			/// <summary>
			/// Contructs an instance of the IncorrectUrl class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public IncorrectUrl(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new IncorrectUrl(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the IncorrectUrl class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public IncorrectUrl(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the IncorrectUrl class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public IncorrectUrl(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		/// <param name="url">Url.</param>
		public static Exception Get_Server_EndPointIsAlreadyBeingListenedTo(string url)
		{
			string errorIdentifier = "GenuineChannels.Exception.Server.EndPointListened";
			return new EndPointListened(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The specified end point is already in use or it has not finished shutting down yet: \"{0}\".", url));
		}

		/// <summary>
		/// The exception that is thrown when .
		/// </summary>
		[Serializable]
			public class EndPointListened : OperationException
		{
			/// <summary>
			/// Contructs an instance of the EndPointListened class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public EndPointListened(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new EndPointListened(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the EndPointListened class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public EndPointListened(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the EndPointListened class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public EndPointListened(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		/// <param name="url">Url.</param>
		public static Exception Get_Channel_ClientDidNotReconnectWithinTimeOut(string url)
		{
			string errorIdentifier = "GenuineChannels.Exception.Server.ClientDidNotReconnectWithinTimeOut";
			return new ClientDidNotReconnectWithinTimeOut(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The client application {0} has not reconnected within the specified timeout. The information associated with that remote host will be released.", url));
		}

		/// <summary>
		/// The exception that is thrown when the client did not reestablish a connection within the specified period of time.
		/// </summary>
		[Serializable]
			public class ClientDidNotReconnectWithinTimeOut : OperationException
		{
			/// <summary>
			/// Contructs an instance of the ClientDidNotReconnectWithinTimeOut class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public ClientDidNotReconnectWithinTimeOut(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new ClientDidNotReconnectWithinTimeOut(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the ClientDidNotReconnectWithinTimeOut class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public ClientDidNotReconnectWithinTimeOut(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the ClientDidNotReconnectWithinTimeOut class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public ClientDidNotReconnectWithinTimeOut(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		/// <param name="errorCode">Windows' error code.</param>
		public static Exception Get_Windows_CanNotCreateOrOpenNamedEvent(int errorCode)
		{
			string errorIdentifier = "GenuineChannels.Exception.Windows.CanNotCreateOrOpenNamedEvent";
			return new CanNotCreateOrOpenNamedEvent(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "Can not create or open a global named event. Error code: {0}. Use the Visual C++ Error Lookup utility to see the text of the error message.", errorCode));
		}

		/// <summary>
		/// The exception that is thrown when creation or opening of the global event fails.
		/// </summary>
		[Serializable]
			public class CanNotCreateOrOpenNamedEvent : OperationException
		{
			/// <summary>
			/// Contructs an instance of the CanNotCreateOrOpenNamedEvent class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public CanNotCreateOrOpenNamedEvent(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new CanNotCreateOrOpenNamedEvent(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the CanNotCreateOrOpenNamedEvent class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public CanNotCreateOrOpenNamedEvent(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the CanNotCreateOrOpenNamedEvent class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public CanNotCreateOrOpenNamedEvent(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		/// <param name="errorCode">Windows' error code.</param>
		public static Exception Get_Windows_NamedEventError(int errorCode)
		{
			string errorIdentifier = "GenuineChannels.Exception.Windows.NamedEventError";
			return new NamedEventError(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "An error has occurred while using a named event. Error code: {0}. Use the Visual C++ Error Lookup utility to see the text of the error message.", errorCode));
		}

		/// <summary>
		/// The exception that is thrown when an operation on a named event fails.
		/// </summary>
		[Serializable]
			public class NamedEventError : OperationException
		{
			/// <summary>
			/// Contructs an instance of the NamedEventError class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public NamedEventError(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new NamedEventError(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the NamedEventError class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public NamedEventError(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the NamedEventError class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public NamedEventError(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		/// <param name="errorCode">Windows' error code.</param>
		public static Exception Get_Windows_CanNotCreateOrOpenSharedMemory(int errorCode)
		{
			string errorIdentifier = "GenuineChannels.Exception.Windows.CanNotCreateOrOpenSharedMemory";
			return new CanNotCreateOrOpenSharedMemory(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "Can not create or open a memory file mapping. Error code: {0}. Use the Visual C++ Error Lookup utility to see the text of the error message.", errorCode));
		}

		/// <summary>
		/// The exception that is thrown when the memory share cannot be created or opened.
		/// </summary>
		[Serializable]
			public class CanNotCreateOrOpenSharedMemory : OperationException
		{
			/// <summary>
			/// Contructs an instance of the CanNotCreateOrOpenSharedMemory class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public CanNotCreateOrOpenSharedMemory(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new CanNotCreateOrOpenSharedMemory(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the CanNotCreateOrOpenSharedMemory class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public CanNotCreateOrOpenSharedMemory(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the CanNotCreateOrOpenSharedMemory class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public CanNotCreateOrOpenSharedMemory(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		/// <param name="errorCode">Windows' error code.</param>
		public static Exception Get_Windows_SharedMemoryError(int errorCode)
		{
			string errorIdentifier = "GenuineChannels.Exception.Windows.SharedMemoryError";
			return new SharedMemoryError(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "Error has occurred while using shared memory. Error code: {0}. Use the Visual C++ Error Lookup utility to see the text of the error message.", errorCode));
		}

		/// <summary>
		/// The exception that is thrown when an error occurs during work with shared memory.
		/// </summary>
		[Serializable]
			public class SharedMemoryError : OperationException
		{
			/// <summary>
			/// Contructs an instance of the SharedMemoryError class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public SharedMemoryError(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new SharedMemoryError(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the SharedMemoryError class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public SharedMemoryError(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the SharedMemoryError class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public SharedMemoryError(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		/// <param name="errorCode">Windows' error code.</param>
		public static Exception Get_Windows_SspiError(int errorCode)
		{
			string errorIdentifier = "GenuineChannels.Exception.Windows.SspiError";
			return new SspiError(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The SSPI API has failed with 0x{0:X} error code. Please, look at WinError.h or use the Visual C++ Error Lookup utility to see the text of the error message.", errorCode));
		}

		/// <summary>
		/// The exception that is thrown when an SSPI error occurs.
		/// </summary>
		[Serializable]
			public class SspiError : OperationException
		{
			/// <summary>
			/// Contructs an instance of the SspiError class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public SspiError(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new SspiError(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the SspiError class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public SspiError(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the SspiError class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public SspiError(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		/// <param name="feature">Feature.</param>
		public static Exception Get_Windows_SspiDidNotProvideRequestedFeature(string feature)
		{
			string errorIdentifier = "GenuineChannels.Exception.Windows.SspiDidNotProvideRequestedFeature";
			return new SspiDidNotProvideRequestedFeature(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The SSPI package has not provided the requested feature: \"{0}\".", feature));
		}

		/// <summary>
		/// The exception that is thrown if the established SSPI context does not support a requested feature.
		/// </summary>
		[Serializable]
			public class SspiDidNotProvideRequestedFeature : OperationException
		{
			/// <summary>
			/// Contructs an instance of the SspiDidNotProvideRequestedFeature class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public SspiDidNotProvideRequestedFeature(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new SspiDidNotProvideRequestedFeature(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the SspiDidNotProvideRequestedFeature class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public SspiDidNotProvideRequestedFeature(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the SspiDidNotProvideRequestedFeature class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public SspiDidNotProvideRequestedFeature(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		/// <param name="parameterName">Name of parameter.</param>
		public static Exception Get_Channel_InvalidParameter(string parameterName)
		{
			string errorIdentifier = "GenuineChannels.Exception.Channel.InvalidParameter";
			return new InvalidParameter(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "Invalid value of the \"{0}\" parameter. Please, refer to the Genuine Channels documentation.", parameterName));
		}

		/// <summary>
		/// The exception that is thrown when an invalid parameter value is found.
		/// </summary>
		[Serializable]
			public class InvalidParameter : OperationException
		{
			/// <summary>
			/// Contructs an instance of the InvalidParameter class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public InvalidParameter(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new InvalidParameter(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the InvalidParameter class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public InvalidParameter(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the InvalidParameter class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public InvalidParameter(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Returns a predefined exception.
		/// </summary>
		public static Exception Get_Channel_ReconnectionFailed()
		{
			string errorIdentifier = "GenuineChannels.Exception.Channel.ReconnectionFailed";
			return new ReconnectionFailed(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The client channel has not reconnected to the server for the specified time and number of attempts. The channel will try to connect to the server again only when you invoke the SAO or CAO object of the remote host."));
		}

		/// <summary>
		/// The exception that is thrown when a client channel was not able to reconnect to the server.
		/// </summary>
		[Serializable]
			public class ReconnectionFailed : OperationException
		{
			/// <summary>
			/// Contructs an instance of the ReconnectionFailed class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public ReconnectionFailed(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new ReconnectionFailed(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the ReconnectionFailed class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public ReconnectionFailed(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the ReconnectionFailed class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public ReconnectionFailed(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Returns a predefined exception.
		/// </summary>
		public static Exception Get_Channel_ConnectionGracefullyClosed()
		{
			string errorIdentifier = "GenuineChannels.Exception.Channel.ConnectionGracefullyClosed";
			return new ConnectionGracefullyClosed(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The connection to the remote host has been closed according to a mutual agreement."));
		}

		/// <summary>
		/// The exception signals that the connection has been correctly closed.
		/// </summary>
		[Serializable]
			public class ConnectionGracefullyClosed : OperationException
		{
			/// <summary>
			/// Contructs an instance of the ConnectionGracefullyClosed class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public ConnectionGracefullyClosed(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new ConnectionGracefullyClosed(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the ConnectionGracefullyClosed class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public ConnectionGracefullyClosed(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the ConnectionGracefullyClosed class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public ConnectionGracefullyClosed(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Returns a predefined exception.
		/// </summary>
		/// <param name="reason">The reason of the connection shutdown.</param>
		public static Exception Get_Channel_ConnectionShutDown(Exception reason)
		{
			string errorIdentifier = "GenuineChannels.Exception.Channel.ConnectionShutDown";
			return new ConnectionShutDown(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The connection is forcibly closed because the ConnectionManager.ReleaseConnections method has been invoked. The specified reason: {0}.", reason.Message));
		}

		/// <summary>
		/// The exception that is thrown when a connection is forcibly shut down.
		/// </summary>
		[Serializable]
			public class ConnectionShutDown : OperationException
		{
			/// <summary>
			/// Contructs an instance of the ConnectionShutDown class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public ConnectionShutDown(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new ConnectionShutDown(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the ConnectionShutDown class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public ConnectionShutDown(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the ConnectionShutDown class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public ConnectionShutDown(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Returns a predefined exception.
		/// </summary>
		public static Exception Get_Channel_ConnectionClosedAfterTimeout()
		{
			string errorIdentifier = "GenuineChannels.Exception.Channel.ConnectionClosedAfterTimeout";
			return new ConnectionClosedAfterTimeout(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "No messages have been received from the remote host for the specified time period. The connection is considered to be needless or broken."));
		}

		/// <summary>
		/// The exception that is thrown when a connection is closed due to its inactivity.
		/// </summary>
		[Serializable]
			public class ConnectionClosedAfterTimeout : OperationException
		{
			/// <summary>
			/// Contructs an instance of the ConnectionClosedAfterTimeout class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public ConnectionClosedAfterTimeout(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new ConnectionClosedAfterTimeout(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the ConnectionClosedAfterTimeout class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public ConnectionClosedAfterTimeout(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the ConnectionClosedAfterTimeout class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public ConnectionClosedAfterTimeout(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Returns a predefined exception.
		/// </summary>
		public static Exception Get_Channel_Closed()
		{
			string errorIdentifier = "GenuineChannels.Exception.Channel.Closed";
			return new ChannelClosed(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The connection(s) has been closed manually."));
		}

		/// <summary>
		/// The exception that is thrown if the requested channel is closed.
		/// </summary>
		[Serializable]
			public class ChannelClosed : OperationException
		{
			/// <summary>
			/// Contructs an instance of the ChannelClosed class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public ChannelClosed(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new ChannelClosed(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the ChannelClosed class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public ChannelClosed(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the ChannelClosed class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public ChannelClosed(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Returns a predefined exception.
		/// </summary>
		public static Exception Get_Channel_Desynchronization()
		{
			string errorIdentifier = "GenuineChannels.Exception.Channel.Desynchronization";
			return new ChannelDesynchronization(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The incoming packet does not contain the correct packet identifier. It means that the connection is out of order and must be closed."));
		}

		/// <summary>
		/// The exception that is thrown when a connection is closed due to its inactivity.
		/// </summary>
		[Serializable]
			public class ChannelDesynchronization : OperationException
		{
			/// <summary>
			/// Contructs an instance of the ChannelDesynchronization class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public ChannelDesynchronization(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new ChannelDesynchronization(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the ChannelDesynchronization class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public ChannelDesynchronization(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the ChannelDesynchronization class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public ChannelDesynchronization(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		public static Exception Get_Broadcast_DestinationTypeMustBeAnInterface()
		{
			string errorIdentifier = "GenuineChannels.Exception.Broadcast.DestinationTypeMustBeAnInterface";
			return new OperationException(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "You must specify an interface while constructing a broadcast dispatcher."));
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		public static Exception Get_Broadcast_ClientSinkIsUnknown()
		{
			string errorIdentifier = "GenuineChannels.Exception.Broadcast.ClientSinkIsUnknown";
			return new OperationException(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "If you want to use the MBR object obtained via a native channel, you should override the client sink and return it by request. Alternatively, you can explicitly specify the URI and Transport Context of the remote host while adding the transparent proxy to the Dispatcher."));
		}

		/// <summary>
		/// Returns a predefined exception.
		/// </summary>
		public static Exception Get_Broadcast_RemoteEndPointDidNotReplyForTimeOut()
		{
			string errorIdentifier = "GenuineChannels.Exception.Broadcast.RemoteEndPointDidNotReplyForTimeOut";
			return new OperationException(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The remote host has not replied to the message for the specified time."));
		}

		/// <summary>
		/// Returns a predefined exception.
		/// </summary>
		public static Exception Get_Broadcast_CallHasAlreadyBeenMade()
		{
			string errorIdentifier = "GenuineChannels.Exception.Broadcast.CallHasAlreadyBeenMade";
			return new OperationException(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The invocation has already been made on this recipient."));
		}

		/// <summary>
		/// Returns a predefined exception.
		/// </summary>
		public static Exception Get_Broadcast_HandlerUninitialized()
		{
			string errorIdentifier = "GenuineChannels.Exception.Broadcast.HandlerUninitialized";
			return new OperationException(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "It is impossible to activate the asynchronous mode while the Dispatcher.BroadcastCallFinishedHandler property is not initialized."));
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		/// <param name="keyName">Context's name.</param>
		public static Exception Get_Security_ContextNotFound(string keyName)
		{
			string errorIdentifier = "GenuineChannels.Exception.Security.ContextNotFound";
			return new ContextNotFound(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The key provider with the name \"{0}\" is not found.", keyName));
		}

		/// <summary>
		/// The exception that is thrown when a Security Key Provider is not found.
		/// </summary>
		[Serializable]
			public class ContextNotFound : OperationException
		{
			/// <summary>
			/// Contructs an instance of the ContextNotFound class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public ContextNotFound(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new ContextNotFound(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the ContextNotFound class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public ContextNotFound(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the ContextNotFound class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public ContextNotFound(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		/// <param name="keyName">Context's name.</param>
		public static Exception Get_Security_ContextWasNotEstablished(string keyName)
		{
			string errorIdentifier = "GenuineChannels.Exception.Security.ContextWasNotEstablished";
			return new ContextWasNotEstablished(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The Security Session can not be established due to an undetermined reason. Please, check the log files to reveal the source of the problem. Security Provider Name: {0}.", keyName));
		}

		/// <summary>
		/// The exception that is thrown when then requested Security Session is not established.
		/// </summary>
		[Serializable]
			public class ContextWasNotEstablished : OperationException
		{
			/// <summary>
			/// Contructs an instance of the ContextWasNotEstablished class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public ContextWasNotEstablished(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new ContextWasNotEstablished(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the ContextWasNotEstablished class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public ContextWasNotEstablished(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the ContextWasNotEstablished class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public ContextWasNotEstablished(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		/// <param name="keyName">The requested context name.</param>
		/// <param name="expectedKeyName">The expected context name.</param>
		public static Exception Get_Security_UnexpectedContext(string keyName, string expectedKeyName)
		{
			string errorIdentifier = "GenuineChannels.Exception.Security.UnexpectedContext";
			return new OperationException(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "Remote host has requested the \"{0}\" security context while the expected security context is \"{1}\".", keyName));
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		/// <param name="val">Incorrect value.</param>
		public static Exception Get_Security_InvalidTimeoutParameter(string val)
		{
			string errorIdentifier = "GenuineChannels.Exception.Security.InvalidTimeoutParameter";
			return new OperationException(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "Can not parse the timeout parameter: \"{0}\". Timeout parameter must contain the time in milliseconds: \"Timeout=240000\".", val));
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		public static Exception Get_Security_PasswordKnowledgeIsNotProved()
		{
			string errorIdentifier = "GenuineChannels.Exception.Security.PasswordKnowledgeIsNotProved";
			return new PasswordKnowledgeIsNotProved(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The knowledge of the password is not proved."));
		}

		/// <summary>
		/// The exception that is thrown when the received hash sum doesn't match the expected sequence.
		/// </summary>
		[Serializable]
			public class PasswordKnowledgeIsNotProved : OperationException
		{
			/// <summary>
			/// Contructs an instance of the PasswordKnowledgeIsNotProved class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public PasswordKnowledgeIsNotProved(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new PasswordKnowledgeIsNotProved(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the PasswordKnowledgeIsNotProved class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public PasswordKnowledgeIsNotProved(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the PasswordKnowledgeIsNotProved class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public PasswordKnowledgeIsNotProved(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		public static Exception Get_Security_WrongSignature()
		{
			string errorIdentifier = "GenuineChannels.Exception.Security.WrongSignature";
			return new WrongSignature(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The message integrity checking failure: the message has a wrong signature."));
		}

		/// <summary>
		/// The exception that is thrown when the received message doesn't pass checking on signature.
		/// </summary>
		[Serializable]
			public class WrongSignature : OperationException
		{
			/// <summary>
			/// Contructs an instance of the WrongSignature class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public WrongSignature(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new WrongSignature(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the WrongSignature class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public WrongSignature(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the WrongSignature class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public WrongSignature(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

		/// <summary>
		/// Returns a predefined exception.
		/// </summary>
		public static Exception Get_Debugging_GeneralWarning(string text, params object[] parameters)
		{
			string errorIdentifier = "GenuineChannels.Exception.Debugging.GeneralWarning";
			return new DebuggingGeneralWarning(null, errorIdentifier, 
				GetLocalizedErrorOrDefaultText( errorIdentifier, text, parameters));
		}

		/// <summary>
		/// The exception that is thrown when the application falls into an exception flow.
		/// </summary>
		[Serializable]
			public class DebuggingGeneralWarning : OperationException
		{
			/// <summary>
			/// Contructs an instance of the DebuggingGeneralWarning class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public DebuggingGeneralWarning(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new ReconnectionFailed(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the DebuggingGeneralWarning class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public DebuggingGeneralWarning(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the DebuggingGeneralWarning class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public DebuggingGeneralWarning(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage)
			{
				this._stackTrace = Environment.StackTrace;
			}

			/// <summary>
			/// Gets a string representation of the frames on the call stack at the time the current exception was thrown.
			/// </summary>
			public override string StackTrace
			{
				get
				{
					return this._stackTrace;
				}
			}
			private string _stackTrace = string.Empty;
		}

		#endregion

		#region -- Trial ---------------------------------------------------------------------------

#if TRIAL

		/// <summary>
		/// Gets a predefined exception.
		/// </summary>
		/// <param name="clause">Name of condition.</param>
		public static Exception Get_Channel_TrialConditionExceeded(string clause)
		{
			string errorIdentifier = "GenuineChannels.Exception.Channel.TrialConditionExceeded";
			return new TrialConditionExceeded(null, errorIdentifier,
				GetLocalizedErrorOrDefaultText( errorIdentifier, "The trial version limitation has been reach: \"{0}\". You can address to author at dim@chel.com.ru (or dima@genuinechannels.com), web-site: http://www.genuinechannels.com", clause));
		}

		/// <summary>
		/// The exception that is thrown when the trial condition is exceeded.
		/// </summary>
		[Serializable]
		public class TrialConditionExceeded : OperationException
		{
			/// <summary>
			/// Contructs an instance of the TrialConditionExceeded class.
			/// </summary>
			/// <param name="operationErrorMessage"></param>
			public TrialConditionExceeded(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage) { }

			/// <summary>
			/// Creates a new object that is a copy of the current instance.
			/// </summary>
			/// <returns>A new object that is a copy of the current instance.</returns>
			public override object Clone() { return new TrialConditionExceeded(this.OperationErrorMessage); }

			/// <summary>
			/// Initializes a new instance of the TrialConditionExceeded class with serialized data.
			/// </summary>
			/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
			/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
			public TrialConditionExceeded(SerializationInfo info, StreamingContext context) : base(info, context) { }

			/// <summary>
			/// Constructs a new instance of the TrialConditionExceeded class.
			/// </summary>
			/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
			/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
			/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
			public TrialConditionExceeded(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base (objToFetchTypeFrom, errorSubIdentifier, userFriendlyMessage) { }
		}

#endif

		#endregion

		#region -- Localizer -----------------------------------------------------------------------

		/// <summary>
		/// Gets or sets the localizer that can provide localized error messages.
		/// This property is thread-safe.
		/// </summary>
		public static IGenuineExceptionLocalizer IGenuineExceptionLocalizer
		{
			get
			{
				lock (_genuineExceptionLocalizerLock)
					return _iGenuineExceptionLocalizer;
			}
			set
			{
				lock (_genuineExceptionLocalizerLock)
					_iGenuineExceptionLocalizer = value;
			}
		}
		private static IGenuineExceptionLocalizer _iGenuineExceptionLocalizer;

		private static object _genuineExceptionLocalizerLock = new object();

		/// <summary>
		/// Gets the error message corresponding to the specified error identifier.
		/// The default error message is used if the currently available localizer does not provide the localized error message.
		/// </summary>
		/// <param name="errorIdentifier">The error identifier.</param>
		/// <param name="errorMessageParameters">The error message parameters.</param>
		/// <param name="defaultErrorMessage">The default error message.</param>
		/// <returns>The error message corresponding to the specified error identifier.</returns>
		public static string GetLocalizedErrorOrDefaultText(string errorIdentifier, string defaultErrorMessage, params object[] errorMessageParameters)
		{
			lock (_genuineExceptionLocalizerLock)
			{
				string errorMessage = null;

				// get it once
				IGenuineExceptionLocalizer iGenuineExceptionLocalizer = _iGenuineExceptionLocalizer;

				try
				{
					if (iGenuineExceptionLocalizer != null)
						errorMessage = iGenuineExceptionLocalizer.Localize(errorIdentifier, errorMessageParameters);
				}
				catch
				{
				}

				if (errorMessage == null)
					errorMessage = string.Format(defaultErrorMessage, errorMessageParameters);

				return errorMessage;
			}
		}

		#endregion
	}
}
