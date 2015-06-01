/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.IO;
using System.Net;

using Belikov.GenuineChannels.BufferPooling;

namespace Belikov.GenuineChannels.GenuineUdp
{
	/// <summary>
	/// Represents a stream being assembled from UDP packets.
	/// </summary>
	public class StreamAssembled : Stream, IDisposable
	{
		/// <summary>
		/// Constructs an instance of the StreamAssembled class.
		/// </summary>
		/// <param name="remoteIPEndPoint">The IPEndPoint of the sender.</param>
		/// <param name="headerSize">The size of the header.</param>
		public StreamAssembled(IPEndPoint remoteIPEndPoint, int headerSize)
		{
			this._started = GenuineUtility.TickCount;
			this.RemoteIPEndPoint = remoteIPEndPoint;
			this._headerSize = headerSize;
			this._readPosition = headerSize;
		}

		/// <summary>
		/// To guarantee atomic access to local members.
		/// </summary>
		private object _accessToLocalMembers = new object();

		/// <summary>
		/// The moment when the first chunk was received.
		/// </summary>
		public int Started
		{
			get
			{
				lock (this._accessToLocalMembers)
					return this._started;
			}
		}
		private int _started;

		/// <summary>
		/// The received content.
		/// </summary>
		public ArrayList _chunks = new ArrayList();

		/// <summary>
		/// The IPEndPoint of the remote host.
		/// </summary>
		public IPEndPoint RemoteIPEndPoint;

		/// <summary>
		/// Gets or sets an indication whether the message has been already processed.
		/// </summary>
		public bool IsProcessed
		{
			get
			{
				lock (this)
					return this._isProcessed;
			}
			set
			{
				lock (this)
					this._isProcessed = value;
					
			}
		}
		private bool _isProcessed;

		/// <summary>
		/// The size of the header in the provided chunks.
		/// </summary>
		private int _headerSize;

		/// <summary>
		/// True if the last packet was received.
		/// </summary>
		private bool _wasLastPacketReceived;

		/// <summary>
		/// Local variable indicated whether stream has been disposed.
		/// </summary>
		private bool _disposed = false;

		/// <summary>
		/// Puts the received buffer into the chunk store.
		/// </summary>
		/// <param name="chunkNumber">The sequence number of the current chunk.</param>
		/// <param name="buffer">The chunk.</param>
		/// <param name="size">The size of valid content.</param>
		/// <param name="isLast">Whether it is the last packet in the sequence.</param>
		/// <returns>True if the stream is gathered.</returns>
		public bool BufferReceived(int chunkNumber, byte[] buffer, int size, bool isLast)
		{
			if (isLast)
				this._wasLastPacketReceived = true;

			// fill it up to make up the sequence
			while (this._chunks.Count < chunkNumber * 2 + 1)
			{
				this._chunks.Add(null);
				this._chunks.Add(0);
			}

			// update the sequence
			if (0 == (int) this._chunks[chunkNumber * 2 + 1])
			{
				this._chunks[chunkNumber * 2] = buffer;
				this._chunks[chunkNumber * 2 + 1] = size;
			}

			// check whether all chunks are gathered
			if (! this._wasLastPacketReceived)
				return false;
			for ( int i = 0; i < this._chunks.Count / 2; i++)
				if (0 == (int) this._chunks[i * 2 + 1] )
					return false;
			return true;
		}


		#region -- Reading -------------------------------------------------------------------------

		private int _readBlockNumber = 0;
		private int _readPosition;

		/// <summary>
		/// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
		/// </summary>
		/// <param name="buffer">An array of bytes.</param>
		/// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream.</param>
		/// <param name="count">The maximum number of bytes to be read from the current stream.</param>
		/// <returns>The total number of bytes read into the buffer.</returns>
		public override int Read(byte[] buffer, int offset, int count)
		{
			if (this._disposed)
				throw new ObjectDisposedException("GenuineChunkedStream");

			int totalReadBytes = 0;

			for ( ; ; )
			{
				if (this._readBlockNumber >= this._chunks.Count || count <= 0)
					return totalReadBytes;

				// locate the next available buffer
				byte[] sourceBuffer = (byte[]) this._chunks[this._readBlockNumber];
				int sourceBufferSize = (int) this._chunks[this._readBlockNumber + 1];

				// if the current buffer has been read, move pointer to the next one
				if (sourceBufferSize <= this._readPosition)
				{
					BufferPool.RecycleBuffer(sourceBuffer);
					this._chunks.RemoveRange(0, 2);
					this._readPosition = this._headerSize;
					continue;
				}

				int readLen = Math.Min(count, sourceBufferSize - this._readPosition);
				Buffer.BlockCopy(sourceBuffer, this._readPosition, buffer, offset, readLen);

				offset += readLen;
				count -= readLen;
				totalReadBytes += readLen;
				this._readPosition += readLen;
			}
		}

		/// <summary>
		/// Reads a byte from the stream and advances the position within the stream by one byte, or returns -1 if at the end of the stream.
		/// </summary>
		/// <returns>The unsigned byte cast to an Int32, or -1 if at the end of the stream.</returns>
		public override int ReadByte()
		{
			if (this._disposed)
				throw new ObjectDisposedException("GenuineChunkedStream");

			for ( ; ; )
			{
				if (this._readBlockNumber >= this._chunks.Count)
					return -1;

				// locate the next available buffer
				byte[] sourceBuffer = (byte[]) this._chunks[this._readBlockNumber];
				int sourceBufferSize = (int) this._chunks[this._readBlockNumber + 1];

				// if the current buffer has been read, move the pointer to the next one
				if (sourceBufferSize <= this._readPosition)
				{
					BufferPool.RecycleBuffer(sourceBuffer);
					this._chunks.RemoveRange(0, 2);
					this._readPosition = this._headerSize;
					continue;
				}

				return sourceBuffer[this._readPosition ++];
			}
		}

		/// <summary>
		/// Closes the current stream and releases any resources associated with the current stream.
		/// </summary>
		public void Dispose()
		{
			this.Close();
		}

		/// <summary>
		/// Closes the current stream and releases any resources associated with the current stream.
		/// </summary>
		public override void Close()
		{
			if (this._disposed)
				return ;

			for ( int i = 0; i < this._chunks.Count; i += 2)
			{
				byte[] buffer = (byte[]) this._chunks[i];
				BufferPool.RecycleBuffer(buffer);
			}

			this._chunks.Clear();
			this._disposed = true;
		}

		#endregion

		#region -- Insignificant stream members ----------------------------------------------------

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
				return false;
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

		#endregion

	}
}
