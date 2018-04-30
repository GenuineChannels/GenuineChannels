/* Genuine Channels product.
 *
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 *
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;

using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Parameters;
using Belikov.GenuineChannels.Receiving;
using Belikov.GenuineChannels.Security;
using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.GenuineTcp
{
	/// <summary>
	/// Incapsulates information about a socket connection.
	/// </summary>
	internal class TcpSocketInfo : Stream
	{
#if DEBUG
		/// <summary>
		/// Constructs an instance of the TcpSocketInfo class.
		/// </summary>
		/// <param name="socket">The socket.</param>
		/// <param name="iTransportContext">Transport Context.</param>
		/// <param name="connectionName">The name of the connection.</param>
		/// <param name="typeOfSocket">The type of the socket used for debugging.</param>
		internal TcpSocketInfo(Socket socket, ITransportContext iTransportContext, string connectionName, string typeOfSocket)
#else
		/// <summary>
		/// Constructs an instance of the TcpSocketInfo class.
		/// </summary>
		/// <param name="socket">The socket.</param>
		/// <param name="iTransportContext">Transport Context.</param>
		/// <param name="connectionName">The name of the connection.</param>
		internal TcpSocketInfo(Socket socket, ITransportContext iTransportContext, string connectionName)
#endif
		{
			this.Socket = socket;
			this.ITransportContext = iTransportContext;
			this.ConnectionName = connectionName == null ? "~" + Guid.NewGuid().ToString("N") : connectionName;
			this.MaxSendSize = (int) this.ITransportContext.IParameterProvider[GenuineParameter.TcpMaxSendSize];

#if DEBUG
			this._typeOfSocket = typeOfSocket;
			this._connectionNumber = Interlocked.Increment(ref _dbg_ConnectionCounter);
#endif
		}

		/// <summary>
		/// To guarantee atomic access to local members.
		/// </summary>
		private object _accessToLocalMembers = new object();

		#region -- Debug information ---------------------------------------------------------------

#if DEBUG

		private static int _dbg_ConnectionCounter = 0;
		private int _connectionNumber = 0;
		private string _typeOfSocket;

		/// <summary>
		/// Returns a String that represents the current Object.
		/// </summary>
		/// <returns>A String that represents the current Object.</returns>
		public override string ToString()
		{
			return string.Format("(No: {0}. Type: {1}. Valid: {2}. Type: {3}. Name: {4}. Identifier: {5}.)",
				this._connectionNumber, this._typeOfSocket, this.IsValid,
				Enum.Format(typeof(GenuineConnectionType), this.GenuineConnectionType, "g"),
				this.ConnectionName, this.DbgConnectionId);
		}

#endif

		/// <summary>
		/// Gets the identifier of the connection.
		/// </summary>
		public int DbgConnectionId
		{
			get
			{
				return this._connectionId;
			}
		}
		private int _connectionId = ConnectionManager.GetUniqueConnectionId();

		#endregion

		#region -- Connection information ----------------------------------------------------------

		/// <summary>
		/// The socket.
		/// </summary>
		public Socket Socket;

		/// <summary>
		/// The name of this connection.
		/// </summary>
		public string ConnectionName;

		/// <summary>
		/// The host the connection is directed to.
		/// </summary>
		public HostInformation Remote;

		/// <summary>
		/// Gets the URL of the remote host if it's a server or the URI of the remote host if it's a client.
		/// </summary>
		public string PrimaryRemoteUri
		{
			get
			{
				// if we're server => return remote's uri
				return this.IsServer ? this.Remote.Uri : this.Remote.Url;
			}
		}

		/// <summary>
		/// Transport Context.
		/// </summary>
		public ITransportContext ITransportContext;

		/// <summary>
		/// Indicates whether this connection is capable of sending or receiving messages.
		/// </summary>
		public bool IsValid = true;

		/// <summary>
		/// The type of the connection.
		/// </summary>
		public GenuineConnectionType GenuineConnectionType;

		/// <summary>
		/// Connection-level Security Session.
		/// </summary>
		public SecuritySession ConnectionLevelSecurity;

		/// <summary>
		/// The message being sent asynchronously.
		/// </summary>
		public Message Message;

		/// <summary>
		/// Maximum size of the chunk being sent through the socket.
		/// </summary>
		public int MaxSendSize;

		/// <summary>
		/// The message registrator to prevent processing the same request twice after connection
		/// reestablishing.
		/// </summary>
		public IMessageRegistrator _iMessageRegistrator = new MessageRegistratorWithLimitedTime();

		#endregion

		#region -- Persistent or Named connection --------------------------------------------------

		/// <summary>
		/// Gets or sets an indication whether this socket is available for sending.
		/// </summary>
		public bool LockForSending = false;

		/// <summary>
		/// A queue of the messages arranged to be sent through this connection.
		/// </summary>
		public MessageContainer MessageContainer;

		/// <summary>
		/// The most recent moment the message was sent to the remote host.
		/// To manage the ping sending.
		/// </summary>
		public int LastTimeContentWasSent
		{
			get
			{
				lock (_lastTimeContentWasSentLock)
					return _lastTimeContentWasSent;
			}
			set
			{
				lock (_lastTimeContentWasSentLock)
					_lastTimeContentWasSent = value;
			}
		}
		private int _lastTimeContentWasSent = GenuineUtility.TickCount;
		private object _lastTimeContentWasSentLock = new object();

		#endregion

		#region -- Invocation or One-Way connection ------------------------------------------------

		/// <summary>
		/// The message to receive a response for.
		/// </summary>
		public Message InitialMessage;

		/// <summary>
		/// The time span to close invocation connection after this period of inactivity.
		/// </summary>
		public int CloseConnectionAfterInactivity;

		/// <summary>
		/// The time after which the socket will be shut down automatically.
		/// </summary>
		public int ShutdownTime
		{
			get
			{
				lock (this._accessToLocalMembers)
					return this._shutdownTime;
			}
		}
		private int _shutdownTime = GenuineUtility.FurthestFuture;

		/// <summary>
		/// Renews socket activity for the CloseConnectionAfterInactivity value.
		/// </summary>
		public void Renew()
		{
			lock (this._accessToLocalMembers)
				this._shutdownTime = GenuineUtility.GetTimeout(this.CloseConnectionAfterInactivity);
			this.Remote.Renew(this.CloseConnectionAfterInactivity, false);
		}

		/// <summary>
		/// Indicates whether the connection was opened by the remote host.
		/// </summary>
		public bool IsServer;

		/// <summary>
		/// Indicates the current state of the connection.
		/// </summary>
		public TcpInvocationFiniteAutomatonState TcpInvocationFiniteAutomatonState;

		#endregion

		#region -- Synchronous sending -------------------------------------------------------------

		private int _syncWritingTimeout;
		internal byte[] _sendBuffer;
		internal MessageList MessagesSentSynchronously = new MessageList(1);

		/// <summary>
		/// Sets up the synchronous reading from the socket.
		/// </summary>
		/// <param name="operationTimeout">Operation timeout.</param>
		public void SetupWriting(int operationTimeout)
		{
			if (this._sendBuffer == null)
				this._sendBuffer = new byte[this.MaxSendSize];
			this._syncWritingTimeout = operationTimeout;
		}

		/// <summary>
		/// Writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
		/// </summary>
		/// <param name="buffer">An array of bytes.</param>
		/// <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes to the current stream.</param>
		/// <param name="count">The number of bytes to be written to the current stream.</param>
		public override void Write(byte[] buffer, int offset, int count)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			int milliseconds = GenuineUtility.GetMillisecondsLeft(this._syncWritingTimeout);
			if (milliseconds <= 0)
				throw GenuineExceptions.Get_Send_Timeout();

			try
			{
				this.Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, milliseconds);

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Transport] > 0 )
				{
					binaryLogWriter.WriteTransportContentEvent(LogCategory.Transport, "TcpSocketInfo.Write",
						LogMessageType.SynchronousSendingStarted, null, null, this.Remote == null ? null : this.Remote,
						binaryLogWriter[LogCategory.Transport] > 1 ? new MemoryStream(GenuineUtility.CutOutBuffer(buffer, offset, count)) : null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						this.DbgConnectionId, count, this.Remote == null || this.Remote.PhysicalAddress == null ? null : this.Remote.PhysicalAddress.ToString(),
						null, null,
						"The content is being sent synchronously. Size: {0}.", count);
				}

				for ( ; ; )
				{
					int bytesSent = 0;
					try
					{
						bytesSent = this.Socket.Send(buffer, offset, count, SocketFlags.None);

						// LOG:
						if ( binaryLogWriter != null && binaryLogWriter[LogCategory.LowLevelTransport] > 0 )
						{
							binaryLogWriter.WriteTransportContentEvent(LogCategory.LowLevelTransport, "TcpSocketInfo.Write",
								LogMessageType.LowLevelTransport_SyncSendingCompleted, null, null, this.Remote,
								binaryLogWriter[LogCategory.LowLevelTransport] > 1 ? new MemoryStream(GenuineUtility.CutOutBuffer(buffer, offset, bytesSent)) : null,
								GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
								this.DbgConnectionId, bytesSent, this.Socket.RemoteEndPoint.ToString(),
								null, null,
								"Socket.Send(). Size: {0}. Sent: {1}.", count, bytesSent);
						}
					}
					catch (Exception ex)
					{
						// LOG:
						if ( binaryLogWriter != null && binaryLogWriter[LogCategory.LowLevelTransport] > 0 )
						{
							binaryLogWriter.WriteTransportContentEvent(LogCategory.LowLevelTransport, "TcpSocketInfo.Write",
								LogMessageType.LowLevelTransport_SyncSendingCompleted, ex, null, this.Remote == null ? null : this.Remote,
								binaryLogWriter[LogCategory.LowLevelTransport] > 1 ? new MemoryStream(GenuineUtility.CutOutBuffer(buffer, offset, bytesSent)) : null,
								GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
								this.DbgConnectionId, bytesSent, this.Socket.RemoteEndPoint.ToString(),
								null, null,
								"Socket.Send() failed. Size: {0}. Sent: {1}.", count, bytesSent);
						}
					}

					if (bytesSent == 0)
						throw GenuineExceptions.Get_Send_TransportProblem();

					offset += bytesSent;
					count -= bytesSent;

					if (count <= 0)
						break;
				}
			}
			catch(Exception ex)
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Transport] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Transport, "TcpSocketInfo.Write",
						LogMessageType.SynchronousSendingStarted, ex, null, this.Remote == null ? null : this.Remote,
						binaryLogWriter[LogCategory.Transport] > 1 ? new MemoryStream(GenuineUtility.CutOutBuffer(buffer, offset, count)) : null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, this.DbgConnectionId, 0, 0, 0, count.ToString(), null, null, null,
						"An exception is raised during synchronous sending.");
				}

				throw GenuineExceptions.Get_Send_TransportProblem();
			}
		}

		#endregion

		#region -- Asynchronous sending ------------------------------------------------------------

		// WARNING: the stream is taken directly from the message

		/// <summary>
		/// The data being sent to the socket asynchronously.
		/// </summary>
		public byte[] AsyncSendBuffer;

		/// <summary>
		/// Size of the valid content being contained in SendingBuffer.
		/// </summary>
		public int AsyncSendBufferSizeOfValidContent;

		/// <summary>
		/// Current position in SendingBuffer.
		/// </summary>
		public int AsyncSendBufferCurrentPosition;

		/// <summary>
		/// Indicates whether the stream was read and last packet flag was sent.
		/// </summary>
		public bool AsyncSendBufferIsLastPacket;

		/// <summary>
		/// The stream containing the data being sent to the remote host.
		/// </summary>
		public Stream AsyncSendStream;

		#endregion

		#region -- Asynchronous receiving ----------------------------------------------------------

		/// <summary>
		/// The header.
		/// </summary>
		public byte[] ReceivingHeaderBuffer;

//		/// <summary>
//		/// The data being received from the socket asynchronously.
//		/// </summary>
//		public byte[] ReceivingBuffer;

		/// <summary>
		/// The size of the content being read to ReceivingBuffer.
		/// </summary>
		public int ReceivingBufferExpectedSize;

		/// <summary>
		/// The current position in ReceivingBuffer.
		/// </summary>
		public int ReceivingBufferCurrentPosition;

		/// <summary>
		/// True if a header is being currently received.
		/// </summary>
		public bool IsHeaderIsBeingReceived;

//		/// <summary>
//		/// Indicates whether the current packet being receivied is a last one.
//		/// </summary>
//		public bool IsLastPacket;

//		/// <summary>
//		/// The received content.
//		/// </summary>
//		public GenuineChunkedStream ReceivingMessageStream;

		#endregion

		#region -- Insignificant stream members ----------------------------------------------------

		/// <summary>
		/// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
		/// </summary>
		/// <param name="buffer">An array of bytes.</param>
		/// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream.</param>
		/// <param name="count">The maximum number of bytes to be read from the current stream.</param>
		/// <returns>The total number of bytes read into the buffer.</returns>
		public override int Read(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException();
		}

		/// <summary>
		/// Gets a value indicating whether the current stream supports reading.
		/// </summary>
		public override bool CanRead
		{
			get
			{
				return false;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the current stream supports seeking.
		/// </summary>
		public override bool CanSeek
		{
			get
			{
				return false;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the current stream supports writing.
		/// </summary>
		public override bool CanWrite
		{
			get
			{
				return true;
			}
		}

		/// <summary>
		/// Gets the length in bytes of the stream.
		/// </summary>
		public override long Length
		{
			get
			{
				throw new NotSupportedException();
			}
		}

		/// <summary>
		/// Gets or sets the position within the current stream.
		/// Always fires NotSupportedException exception.
		/// </summary>
		public override long Position
		{
			get
			{
				throw new NotSupportedException();
			}
			set
			{
				throw new NotSupportedException();
			}
		}

		/// <summary>
		/// Begins an asynchronous read operation.
		/// </summary>
		/// <param name="buffer">The buffer to read the data into.</param>
		/// <param name="offset">The byte offset in buffer at which to begin writing data read from the stream.</param>
		/// <param name="count">The maximum number of bytes to read.</param>
		/// <param name="callback">An optional asynchronous callback, to be called when the read is complete.</param>
		/// <param name="state">A user-provided object that distinguishes this particular asynchronous read request from other requests.</param>
		/// <returns>An IAsyncResult that represents the asynchronous read, which could still be pending.</returns>
		public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
		{
			throw new NotSupportedException();
		}

		/// <summary>
		/// Begins an asynchronous write operation.
		/// </summary>
		/// <param name="buffer">The buffer to write data from.</param>
		/// <param name="offset">The byte offset in buffer from which to begin writing.</param>
		/// <param name="count">The maximum number of bytes to write.</param>
		/// <param name="callback">An optional asynchronous callback, to be called when the write is complete.</param>
		/// <param name="state">A user-provided object that distinguishes this particular asynchronous write request from other requests.</param>
		/// <returns>An IAsyncResult that represents the asynchronous write, which could still be pending.</returns>
		public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
		{
			throw new NotSupportedException();
		}

		/// <summary>
		/// Waits for the pending asynchronous read to complete.
		/// </summary>
		/// <param name="asyncResult">The reference to the pending asynchronous request to finish.</param>
		/// <returns>The number of bytes read from the stream, between zero (0) and the number of bytes you requested. Streams only return zero (0) at the end of the stream, otherwise, they should block until at least one byte is available.</returns>
		public override int EndRead(IAsyncResult asyncResult)
		{
			throw new NotSupportedException();
		}

		/// <summary>
		/// Ends an asynchronous write operation.
		/// </summary>
		/// <param name="asyncResult">A reference to the outstanding asynchronous I/O request.</param>
		public override void EndWrite(IAsyncResult asyncResult)
		{
			throw new NotSupportedException();
		}

		/// <summary>
		/// Clears all buffers for this stream and causes any buffered data to be written to the underlying device.
		/// </summary>
		public override void Flush()
		{
		}

		/// <summary>
		/// Sets the position within the current stream.
		/// </summary>
		/// <param name="offset">A byte offset relative to the origin parameter.</param>
		/// <param name="origin">A value of type SeekOrigin indicating the reference point used to obtain the new position.</param>
		/// <returns>The new position within the current stream.</returns>
		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException();
		}

		/// <summary>
		/// Sets the length of the current stream.
		/// </summary>
		/// <param name="val">The desired length of the current stream in bytes.</param>
		public override void SetLength(long val)
		{
			throw new NotSupportedException();
		}

		#endregion

		#region -- Connection state ----------------------------------------------------------------

		/// <summary>
		/// The state controller.
		/// </summary>
		private ConnectionStateSignaller _connectionStateSignaller;

		/// <summary>
		/// The state controller lock.
		/// </summary>
		private object _connectionStateSignallerLock = new object();

		/// <summary>
		/// Sets the state of the connection.
		/// </summary>
		/// <param name="genuineEventType">The state of the connection.</param>
		/// <param name="reason">The exception.</param>
		/// <param name="additionalInfo">The additional info.</param>
		public void SignalState(GenuineEventType genuineEventType, Exception reason, object additionalInfo)
		{
			lock (this._connectionStateSignallerLock)
			{
				if (this._connectionStateSignaller == null)
					this._connectionStateSignaller = new ConnectionStateSignaller(this.Remote, this.ITransportContext.IGenuineEventProvider);

				this._connectionStateSignaller.SetState(genuineEventType, reason, additionalInfo);
			}
		}

		#endregion

	}
}
