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
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;

namespace Belikov.GenuineChannels.GenuineTcp
{
	/// <summary>
	/// Implements a stream reading data from a socket synchronously.
	/// Automatically initiates receiving after the current message is read up entirely.
	/// </summary>
	internal class SyncSocketReadingStream : Stream, IDisposable
	{
		/// <summary>
		/// Constructs an instance of the SyncSocketReadingStream class.
		/// </summary>
		/// <param name="tcpConnectionManager">TCP Connection Manager.</param>
		/// <param name="tcpSocketInfo">The connection.</param>
		/// <param name="receiveTimeout">The moment at which the message must be received entirely.</param>
		/// <param name="automaticallyContinueReading">Indicates whether this instance will automatically initiate reading of the next message from the specified connection.</param>
		public SyncSocketReadingStream(TcpConnectionManager tcpConnectionManager, TcpSocketInfo tcpSocketInfo, int receiveTimeout, bool automaticallyContinueReading)
		{
			this._readBuffer = BufferPool.ObtainBuffer();

			this._tcpConnectionManager = tcpConnectionManager;
			this._tcpSocketInfo = tcpSocketInfo;
			this._receiveTimeout = receiveTimeout;
			this._automaticallyContinueReading = automaticallyContinueReading;

			// first, complete receiving of the first header
			// it may read up the entire message and release the underlying connection
			ReadNextPortion(true);
		}

		private TcpConnectionManager _tcpConnectionManager;
		private TcpSocketInfo _tcpSocketInfo;
		private int _receiveTimeout;
		private byte[] _readBuffer;
		private bool _automaticallyContinueReading;

		private int _validLength;
		private int _currentPosition;
		private int _currentPacketSize;
		private int _currentPacketBytesRead;
		private bool _messageRead;

		/// <summary>
		/// The unique identifier of the current stream.
		/// </summary>
		public int DbgStreamId
		{
			get
			{
				return this._dbgStreamId;
			}
		}
		private int _dbgStreamId = Interlocked.Increment(ref _totalDbgStreamId);
		private static int _totalDbgStreamId = 0;

		/// <summary>
		/// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
		/// </summary>
		/// <param name="buffer">An array of bytes.</param>
		/// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream.</param>
		/// <param name="count">The maximum number of bytes to be read from the current stream.</param>
		/// <returns>The total number of bytes read into the buffer.</returns>
		public override int Read(byte[] buffer, int offset, int count)
		{
			int size = 0;
			int resultSize = 0;

			for ( ; ; )
			{
				// check whether we have the next portion
				if (this._currentPosition < this._validLength)
				{
					size = Math.Min(this._validLength - this._currentPosition, count);
					Buffer.BlockCopy(this._readBuffer, this._currentPosition, buffer, offset, size);

					this._currentPosition += size;
					count -= size;
					resultSize += size;
					offset += size;
				}

				// recycle the buffer if possible
				if (this._readBuffer != null && this._messageRead && this._currentPacketBytesRead >= this._currentPacketSize && this._currentPosition >= this._validLength)
				{
					BufferPool.RecycleBuffer(this._readBuffer);
					this._readBuffer = null;
				}

				if (count <= 0 || (this._messageRead && this._currentPacketBytesRead >= this._currentPacketSize))
					return resultSize;

				ReadNextPortion(false);
			}
		}

		/// <summary>
		/// Reads a byte from the stream and advances the position within the stream by one byte, or returns -1 if at the end of the stream.
		/// </summary>
		/// <returns>The unsigned byte cast to an Int32, or -1 if at the end of the stream.</returns>
		public override int ReadByte()
		{
			try
			{
				// get a byte
				if (this._currentPosition < this._validLength)
					return this._readBuffer[ this._currentPosition++ ];

				ReadNextPortion(false);

				if (this._currentPosition < this._validLength)
					return this._readBuffer[ this._currentPosition++ ];

				return -1;
			}
			finally
			{
				// recycle the buffer if possible
				if (this._readBuffer != null && this._messageRead && this._currentPacketBytesRead >= this._currentPacketSize && this._currentPosition >= this._validLength)
				{
					BufferPool.RecycleBuffer(this._readBuffer);
					this._readBuffer = null;
				}
			}
		}

		/// <summary>
		/// Synchronously reads the next network packet if it is available.
		/// </summary>
		/// <param name="deriveHeader">Indicates whether it is necessary to take header from the provided connection.</param>
		private void ReadNextPortion(bool deriveHeader)
		{
			int bytesRead = 0;
			int lengthToRead = 0;

			if (! deriveHeader)
			{
				// try to read the remaining part of the packet
				if (this._currentPacketBytesRead < this._currentPacketSize)
				{
					// read the next part of the packet
					lengthToRead = Math.Min(this._currentPacketSize - this._currentPacketBytesRead, this._readBuffer.Length);

					this._validLength = this.ReadFromSocket(this._readBuffer, 0, lengthToRead);
					if (this._validLength == 0)
						throw GenuineExceptions.Get_Receive_Portion();

					// Fixed in 2.5.9.7
					//this._tcpSocketInfo.ITransportContext.ConnectionManager.IncreaseBytesReceived(this._validLength);
					this._currentPacketBytesRead += this._validLength;
					this._currentPosition = 0;

					if (this._currentPacketBytesRead == this._currentPacketSize && this._messageRead)
					{
						this.ReadingCompleted();
					}
					return ;
				}

				// the underlying stream ends
				if (this._messageRead)
					return ;

				// prepare for reading a header
				this._currentPosition = 0;
			}

			lengthToRead = TcpConnectionManager.HEADER_SIZE;

			if (deriveHeader)
			{
				if (this._tcpSocketInfo.ReceivingBufferCurrentPosition > 0)
					Buffer.BlockCopy(this._tcpSocketInfo.ReceivingHeaderBuffer, 0, this._readBuffer, 0, this._tcpSocketInfo.ReceivingBufferCurrentPosition);
				this._currentPosition = this._tcpSocketInfo.ReceivingBufferCurrentPosition;
			}

			// read the header
			while (this._currentPosition < lengthToRead)
			{
				bytesRead = this.ReadFromSocket(this._readBuffer, this._currentPosition, lengthToRead - this._currentPosition);

				if (bytesRead == 0)
					throw GenuineExceptions.Get_Receive_Portion();

				// Fixed in 2.5.9.7
				//this._tcpSocketInfo.ITransportContext.ConnectionManager.IncreaseBytesReceived(bytesRead);
				this._currentPosition += bytesRead;
			}

			// parse the header
			if (this._readBuffer[0] != MessageCoder.COMMAND_MAGIC_CODE)
				throw GenuineExceptions.Get_Receive_IncorrectData();
			this._currentPacketSize = MessageCoder.ReadInt32(this._readBuffer, 1);
			this._messageRead = this._readBuffer[5] != 0;
			this._currentPacketBytesRead = 0;

			// and read the part of the packet
			if (this._currentPacketBytesRead < this._currentPacketSize)
			{
				// read the next part of the packet
				lengthToRead = Math.Min(this._currentPacketSize - this._currentPacketBytesRead, this._readBuffer.Length);

				this._validLength = this.ReadFromSocket(this._readBuffer, 0, lengthToRead);
				if (this._validLength == 0)
					throw GenuineExceptions.Get_Receive_Portion();

				// Fixed in 2.5.9.7
				//this._tcpSocketInfo.ITransportContext.ConnectionManager.IncreaseBytesReceived(this._validLength);
				this._currentPacketBytesRead += this._validLength;
				this._currentPosition = 0;
			}

			if (this._currentPacketBytesRead == this._currentPacketSize && this._messageRead)
			{
				this.ReadingCompleted();
			}
		}

		/// <summary>
		/// Completes reading from the connection.
		/// </summary>
		private void ReadingCompleted()
		{
			if (this._automaticallyContinueReading)
				this._tcpConnectionManager.LowLevel_HalfSync_StartReceiving(this._tcpSocketInfo);
		}

		/// <summary>
		/// Reads data from the socket.
		/// </summary>
		/// <param name="buffer">An array of type Byte that is the storage location for received data.</param>
		/// <param name="offset">The location in buffer to store the received data.</param>
		/// <param name="count">The number of bytes to receive.</param>
		/// <returns>The number of bytes read.</returns>
		public int ReadFromSocket(byte[] buffer, int offset, int count)
		{
			BinaryLogWriter binaryLogWriter = this._tcpConnectionManager.ITransportContext.BinaryLogWriter;

			try
			{
				int millisecondsRemained = GenuineUtility.GetMillisecondsLeft(this._receiveTimeout);
				if (millisecondsRemained <= 0)
					throw GenuineExceptions.Get_Send_ServerDidNotReply();

				this._tcpSocketInfo.Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, millisecondsRemained);
				int bytesReceived = this._tcpSocketInfo.Socket.Receive(buffer, offset, count, SocketFlags.None);

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.LowLevelTransport] > 0 )
					binaryLogWriter.WriteTransportContentEvent(LogCategory.LowLevelTransport, "SyncSocketReadingStream.ReadFromSocket",
						LogMessageType.LowLevelTransport_SyncReceivingCompleted, null, null, this._tcpSocketInfo.Remote, 
						binaryLogWriter[LogCategory.LowLevelTransport] > 1 ? new MemoryStream(buffer, offset, bytesReceived) : null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						this._tcpSocketInfo.DbgConnectionId, bytesReceived, 
						this._tcpSocketInfo.Socket.RemoteEndPoint.ToString(), null, null,
						"Socket.Receive(). Bytes received: {0}.", bytesReceived);

				this._tcpConnectionManager.IncreaseBytesReceived(bytesReceived);
				return bytesReceived;
			}
			catch (Exception ex)
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.LowLevelTransport] > 0 )
					binaryLogWriter.WriteEvent(LogCategory.LowLevelTransport, "SyncSocketReadingStream.ReadFromSocket",
						LogMessageType.LowLevelTransport_SyncReceivingCompleted, ex, null, this._tcpSocketInfo.Remote, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, this._tcpSocketInfo.DbgConnectionId, 0, 0, 0, null, null, null, null,
						"Socket.Receive() failed.");

				throw;
			}
		}

		/// <summary>
		/// Skips the remaining part of the message.
		/// </summary>
		public void Dispose()
		{
			this.Close();
		}

		/// <summary>
		/// Closes the current stream and releases all resources associated with the current stream.
		/// </summary>
		public override void Close()
		{
			this.SkipMessage();
		}

		/// <summary>
		/// Skips the current message in the transport stream.
		/// </summary>
		public void SkipMessage()
		{
			while (! this.IsReadingFinished)
				ReadNextPortion(false);
		}

		/// <summary>
		/// Gets an indication whether the message reading from the underlying provider was completed.
		/// </summary>
		public bool IsReadingFinished
		{
			get
			{
				return this._currentPacketBytesRead >= this._currentPacketSize && this._messageRead;
			}
		}

		/// <summary>
		/// Gets an indication whether the message has been read from this stream.
		/// </summary>
		public bool IsMessageProcessed
		{
			get
			{
				return this._currentPacketBytesRead >= this._currentPacketSize && this._messageRead && this._currentPosition >= this._validLength;
			}
		}

		#region -- Insignificant stream members ----------------------------------------------------

		/// <summary>
		/// Writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
		/// </summary>
		/// <param name="buffer">An array of bytes.</param>
		/// <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes to the current stream.</param>
		/// <param name="count">The number of bytes to be written to the current stream.</param>
		public override void Write(byte[] buffer, int offset, int count)
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
				return true;
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
