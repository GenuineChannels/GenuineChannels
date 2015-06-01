/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;

using Belikov.GenuineChannels.BufferPooling;
using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.GenuineTcp
{
	/// <summary>
	/// Implements a stream writing to the socket synchronously.
	/// </summary>
	internal class SyncSocketWritingStream : Stream
	{
		/// <summary>
		/// Constructs an instance of the SyncSocketWritingStream class.
		/// </summary>
		/// <param name="connectionManager">The Connection Manager.</param>
		/// <param name="socket">The socket.</param>
		/// <param name="writeTimeout">The timeout of the current operation.</param>
		/// <param name="dbgConnectionId">The identifier of the connection.</param>
		/// <param name="remote">Information about the remote host.</param>
		public SyncSocketWritingStream(ConnectionManager connectionManager, Socket socket, int writeTimeout, int dbgConnectionId, HostInformation remote)
		{
			this._connectionManager = connectionManager;
			this._socket = socket;
			this._writeTimeout = writeTimeout;

			this._dbgConnectionId = dbgConnectionId;
			this._remote = remote;
		}

		private ConnectionManager _connectionManager;
		private Socket _socket;
		private int _writeTimeout;

		private int _dbgConnectionId;
		private HostInformation _remote;

		/// <summary>
		/// Writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
		/// </summary>
		/// <param name="buffer">An array of bytes.</param>
		/// <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes to the current stream.</param>
		/// <param name="count">The number of bytes to be written to the current stream.</param>
		public override void Write(byte[] buffer, int offset, int count)
		{
			BinaryLogWriter binaryLogWriter = this._remote.ITransportContext.BinaryLogWriter;

			try
			{
				while ( count > 0 )
				{
					int milliseconds = GenuineUtility.GetMillisecondsLeft(this._writeTimeout);
					if (milliseconds <= 0)
						throw GenuineExceptions.Get_Send_Timeout();

					this._socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, milliseconds);

					int bytesSent = this._socket.Send(buffer, offset, count, SocketFlags.None);

					// LOG:
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.LowLevelTransport] > 0 )
					{
						binaryLogWriter.WriteTransportContentEvent(LogCategory.LowLevelTransport, "SyncSocketWritingStream.Write",
							LogMessageType.LowLevelTransport_SyncSendingCompleted, null, null, this._remote, 
							binaryLogWriter[LogCategory.LowLevelTransport] > 1 ? new MemoryStream(GenuineUtility.CutOutBuffer(buffer, offset, count)) : null, 
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
							this._dbgConnectionId, count, this._socket.RemoteEndPoint.ToString(),
							null, null,
							"Immediately after Socket.Send(). Size: {0}. Sent: {1}.", count, bytesSent);
					}

					if (bytesSent == 0)
						throw GenuineExceptions.Get_Send_TransportProblem();

					offset += bytesSent;
					count -= bytesSent;
					this._connectionManager.IncreaseBytesSent(bytesSent);
				}
			}
			catch(Exception ex)
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.LowLevelTransport] > 0 )
				{
					binaryLogWriter.WriteTransportContentEvent(LogCategory.LowLevelTransport, "SyncSocketWritingStream.Write",
						LogMessageType.LowLevelTransport_SyncSendingCompleted, ex, null, this._remote, 
						binaryLogWriter[LogCategory.LowLevelTransport] > 1 ? new MemoryStream(GenuineUtility.CutOutBuffer(buffer, offset, count)) : null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						this._dbgConnectionId, count, null, null, null,
						"Socket.Send(); ERROR.");
				}

				throw GenuineExceptions.Get_Send_TransportProblem();
			}
		}

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

	}
}
