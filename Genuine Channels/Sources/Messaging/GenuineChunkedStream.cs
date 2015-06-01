/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Belikov.GenuineChannels.BufferPooling;

namespace Belikov.GenuineChannels.Messaging
{
	/// <summary>
	/// GenuineChunkedStream uses chunks from Buffer Pool and allows adding chunks and streams 
	/// directly, without additional copying.
	/// </summary>
	public class GenuineChunkedStream : Stream, ICloneable, IDisposable
	{
		/// <summary>
		/// Constructs an instance of the GenuineChunkedStream class.
		/// </summary>
		public GenuineChunkedStream() : this(false)
		{
		}

		/// <summary>
		/// Constructs an instance of the GenuineChunkedStream class.
		/// </summary>
		/// <param name="releaseOnReadMode">Enables release-on-read mode.</param>
		public GenuineChunkedStream(bool releaseOnReadMode)
		{
			this._releaseOnReadMode = releaseOnReadMode;
		}

		/// <summary>
		/// Gets or sets an indications of whether the release-on-read mode is enabled.
		/// </summary>
		public bool ReleaseOnReadMode
		{
			get
			{
				return this._releaseOnReadMode;
			}
			set
			{
				if (value == false)
					throw new NotSupportedException("Only setting to true is supported.");
				this._releaseOnReadMode = true;
			}
		}
		private bool _releaseOnReadMode = false;

		/// <summary>
		/// Destructor returns all allocated buffers back to the buffer pool.
		/// </summary>
		~GenuineChunkedStream()
		{
			this.Close();
		}

		/// <summary>
		/// Represents a value indicating whether the stream has been disposed.
		/// </summary>
		protected bool _disposed = false;

		/// <summary>
		/// Represents a value indicating whether the array of chunks is shared and buffers may not be returned to Buffer Pool.
		/// </summary>
		protected bool _cloned = false;

		/// <summary>
		/// Contains chunks/used size and streams/null in linear sequence.
		/// </summary>
		private ArrayList _chunksAndStreams = new ArrayList(10);

		/// <summary>
		/// The total size of the stream.
		/// </summary>
		private long _length = 0;

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
				return ! (this._releaseOnReadMode || this._length == -1);
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
				if (this._length >= 0)
					return this._length;
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
				return this.Length;
			}
			set
			{
				if (value != 0)
					throw new NotSupportedException();

				this.Seek(0, SeekOrigin.Begin);
			}
		}

		/// <summary>
		/// Closes the current stream and releases any resources associated with the current stream.
		/// </summary>
		public override void Close()
		{
			if (this._disposed)
				return ;

			for ( int i = 0; i < this._chunksAndStreams.Count; i += 2)
			{
				Stream stream = this._chunksAndStreams[i] as Stream;
				if (stream != null)
					stream.Close();
				else
				{
					byte[] buffer = (byte[]) this._chunksAndStreams[i];
					if (! this._cloned)
						BufferPool.RecycleBuffer(buffer);
				}
			}

			this._disposed = true;
		}

		/// <summary>
		/// Clears all buffers for this stream and causes any buffered data to be written to the underlying device.
		/// </summary>
		public override void Flush()
		{
		}

		private int _readBlockNumber = 0;
		private int _readPosition = 0;

		/// <summary>
		/// Sets the position within the current stream.
		/// </summary>
		/// <param name="offset">A byte offset relative to the origin parameter.</param>
		/// <param name="origin">A value of type SeekOrigin indicating the reference point used to obtain the new position.</param>
		/// <returns>The new position within the current stream.</returns>
		public override long Seek(long offset, SeekOrigin origin)
		{
			if (offset != 0 || origin != SeekOrigin.Begin || this._releaseOnReadMode)
				throw new NotSupportedException();

			this._readBlockNumber = 0;
			this._readPosition = 0;

			for ( int i = 0; i < this._chunksAndStreams.Count; i += 2)
			{
				Stream stream = this._chunksAndStreams[i] as Stream;
				if (stream != null)
					stream.Seek(0, SeekOrigin.Begin);
			}

			return 0;
		}

		/// <summary>
		/// Sets the length of the current stream.
		/// </summary>
		/// <param name="val">The desired length of the current stream in bytes.</param>
		public override void SetLength(long val)
		{
			throw new NotSupportedException("GenuineChunkedStream does not support SetLength method.");
		}

		private int _writePosition = 0;
		private byte[] _currentWriteBlock = null;

		#region -- Reading -------------------------------------------------------------------------

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
				if (this._readBlockNumber >= this._chunksAndStreams.Count || count <= 0)
					return totalReadBytes;

				// locate the next available buffer
				Stream stream = this._chunksAndStreams[this._readBlockNumber] as Stream;
				if (stream != null)
				{
					// it's a stream
					int portionSize = stream.Read(buffer, offset, count);
					offset += portionSize;
					count -= portionSize;
					totalReadBytes += portionSize;

					if (portionSize <= 0)
					{
						// the chunk ends
						this.AdvanceReadPointer();
						continue;
					}
				}
				else
				{
					byte[] sourceBuffer = (byte[]) this._chunksAndStreams[this._readBlockNumber];
					int sourceBufferSize = (int) this._chunksAndStreams[this._readBlockNumber + 1];

					// if the current buffer has been read, move pointer to the next one
					if (sourceBufferSize <= this._readPosition)
					{
						this.AdvanceReadPointer();
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
				if (this._readBlockNumber >= this._chunksAndStreams.Count)
					return -1;

				// locate the next available buffer
				Stream stream = this._chunksAndStreams[this._readBlockNumber] as Stream;
				if (stream != null)
				{
					int byteVal = stream.ReadByte();
					if (byteVal >= 0)
						return byteVal;

					// stream ended up
					this.AdvanceReadPointer();
					continue;
				}
				else
				{
					byte[] sourceBuffer = (byte[]) this._chunksAndStreams[this._readBlockNumber];
					int sourceBufferSize = (int) this._chunksAndStreams[this._readBlockNumber + 1];

					// if the current buffer has been read, move pointer to the next one
					if (sourceBufferSize <= this._readPosition)
					{
						this.AdvanceReadPointer();
						continue;
					}

					return sourceBuffer[this._readPosition ++];
				}
			}
		}

		/// <summary>
		/// Advances the read pointer.
		/// </summary>
		private void AdvanceReadPointer()
		{
			if (this._releaseOnReadMode)
			{
#if ASSERT
				Debug.Assert(this._readBlockNumber == 0);
#endif

				byte[] buffer = this._chunksAndStreams[this._readBlockNumber] as byte[];
				if (buffer != null)
					BufferPool.RecycleBuffer(buffer);
				else
				{
					Stream stream = (Stream) this._chunksAndStreams[this._readBlockNumber];
					stream.Close();
				}

				this._chunksAndStreams.RemoveRange(0, 2);
				this._readPosition = 0;
				return ;
			}

			this._readBlockNumber += 2;
			this._readPosition = 0;
		}

		#endregion

		#region -- Writing -------------------------------------------------------------------------

		/// <summary>
		/// Copies the entire content of this instance to the stream specified directly.
		/// Pretty fast implementation.
		/// </summary>
		/// <param name="outputStream">Stream to write content to.</param>
		public void WriteTo(Stream outputStream)
		{
			if (this._disposed)
				throw new ObjectDisposedException("GenuineChunkedStream");

			for ( ; ; )
			{
				if (this._readBlockNumber >= this._chunksAndStreams.Count)
					return ;

				// locate the next available buffer
				Stream stream = this._chunksAndStreams[this._readBlockNumber] as Stream;
				if (stream != null)
				{
					GenuineUtility.CopyStreamToStream(stream, outputStream);

					// switch to the next block
					this._readBlockNumber += 2;
					this._readPosition = 0;
					continue;
				}
				else
				{
					byte[] sourceBuffer = (byte[]) this._chunksAndStreams[this._readBlockNumber];
					int sourceBufferSize = (int) this._chunksAndStreams[this._readBlockNumber + 1];

					// if the current buffer has been read, move pointer to the next one
					if (sourceBufferSize > this._readPosition)
					{
						int readLen = sourceBufferSize - this._readPosition;
						outputStream.Write(sourceBuffer, this._readPosition, readLen);
					}

					// switch to the next block
					this._readBlockNumber += 2;
					this._readPosition = 0;
					continue;
				}
			}
		}

		/// <summary>
		/// Writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
		/// </summary>
		/// <param name="buffer">An array of bytes.</param>
		/// <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes to the current stream.</param>
		/// <param name="count">The number of bytes to be written to the current stream.</param>
		public override void Write(byte[] buffer, int offset, int count)
		{
			if (this._disposed)
				throw new ObjectDisposedException("GenuineChunkedStream");

			if (this._length >= 0)
				this._length += count;
			for ( ; ; )
			{
				// no data to write
				if (count <= 0)
					return ;

				if (this._currentWriteBlock == null || this._writePosition >= this._currentWriteBlock.Length - 1)
					this.ObtainNextBuffer();

				// write data
				int writeLen = Math.Min(count, this._currentWriteBlock.Length - this._writePosition);
				Buffer.BlockCopy(buffer, offset, this._currentWriteBlock, this._writePosition, writeLen);

				count -= writeLen;
				offset += writeLen;
				this._writePosition += writeLen;
				this._chunksAndStreams[this._chunksAndStreams.Count - 1] = this._writePosition;
			}
		}

#if UNSAFE

		[DllImport("Kernel32", EntryPoint="RtlMoveMemory", SetLastError=false, CharSet=CharSet.Auto)]
		static private unsafe extern void RtlCopyMemory32(byte *destination, byte *source, int size);

		/// <summary>
		/// Writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
		/// </summary>
		/// <param name="source">The sequence of bytes being written to the stream.</param>
		/// <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes to the current stream.</param>
		/// <param name="count">The number of bytes to be written to the current stream.</param>
		public unsafe void Write(byte *source, int offset, int count)
		{
			if (this._disposed)
				throw new ObjectDisposedException("GenuineChunkedStream");

			if (this._length >= 0)
				this._length += count;

			while ( count > 0 )
			{
				if (this._currentWriteBlock == null || this._writePosition >= this._currentWriteBlock.Length - 1)
					this.ObtainNextBuffer();

				// write data
				int writeLen = Math.Min(count, this._currentWriteBlock.Length - this._writePosition);

				fixed (byte *destination = this._currentWriteBlock)
					RtlCopyMemory32(destination + this._writePosition, source + offset, writeLen);

				count -= writeLen;
				offset += writeLen;
				this._writePosition += writeLen;
				this._chunksAndStreams[this._chunksAndStreams.Count - 1] = this._writePosition;
			}
		}

		/// <summary>
		/// Fills up the destination with a sequence of bytes read from the source stream.
		/// </summary>
		/// <param name="destination">The destination.</param>
		/// <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes to the current stream.</param>
		/// <param name="count">The number of bytes to be written to the destination.</param>
		/// <param name="sourceStream">The source.</param>
		public unsafe static void Read(byte *destination, int offset, int count, Stream sourceStream)
		{
			using (BufferKeeper bufferKeeper = new BufferKeeper(0))
			{
				while ( count > 0 )
				{
					// read data to the intermediate buffer
					int sizeOfTheNextPortion = Math.Min(bufferKeeper.Buffer.Length, count);
					GenuineUtility.ReadDataFromStream(sourceStream, bufferKeeper.Buffer, 0, sizeOfTheNextPortion);
					fixed (byte *source = bufferKeeper.Buffer)
						RtlCopyMemory32(destination + offset, source, sizeOfTheNextPortion);

					offset += sizeOfTheNextPortion;
					count -= sizeOfTheNextPortion;
				}
			}
		}

#endif

		/// <summary>
		/// Obtaines and attaches the next buffer from the buffer pool.
		/// </summary>
		private void ObtainNextBuffer()
		{
			this._currentWriteBlock = BufferPool.ObtainBuffer();
			this._writePosition = 0;
			this._chunksAndStreams.Add(this._currentWriteBlock);
			this._chunksAndStreams.Add(0);
		}

		/// <summary>
		/// Writes a byte to the current position in the stream and advances the position within the stream by one byte.
		/// </summary>
		/// <param name="val">The byte to write to the stream.</param>
		public override void WriteByte(byte val)
		{
			if (this._disposed)
				throw new ObjectDisposedException("GenuineChunkedStream");

			if (this._currentWriteBlock == null || this._writePosition >= this._currentWriteBlock.Length - 1)
				this.ObtainNextBuffer();

			this._currentWriteBlock[this._writePosition] = val;
			this._writePosition ++;
			this._chunksAndStreams[this._chunksAndStreams.Count - 1] = this._writePosition;
			if (this._length >= 0)
				this._length ++;
		}

		/// <summary>
		/// Adds a stream to the current stream without copying source stream content.
		/// Added stream must support Length property.
		/// Does not add streams with zero size.
		/// WARNING: if a sending is failed, then stream pointer is being resetted to the very beginning of
		/// the stream. So the provided stream ought to be resettable.
		/// </summary>
		/// <param name="stream">Stream being attached.</param>
		public void WriteStream(Stream stream)
		{
			if (this._disposed)
				throw new ObjectDisposedException("GenuineChunkedStream");

			if (stream.CanSeek)
			{
				if (stream.Length <= 0)
					return ;
				if (this._length >= 0)
					this._length += stream.Length;
			}
			else
				this._length = -1;
			
			this._currentWriteBlock = null;
			this._writePosition = 0;

			this._chunksAndStreams.Add(stream);
			this._chunksAndStreams.Add(0);
		}

		/// <summary>
		/// Adds a buffer to this stream without copying buffer content.
		/// </summary>
		/// <param name="buffer">Buffer being added.</param>
		/// <param name="validContentSize">The size of the valid content in the buffer or -1.</param>
		public void WriteBuffer(byte[] buffer, int validContentSize)
		{
			if (validContentSize == -1)
				validContentSize = buffer.Length;

			this._currentWriteBlock = buffer;
			this._writePosition = validContentSize;
			this._chunksAndStreams.Add(buffer);
			this._chunksAndStreams.Add(validContentSize);

			if (this._length >= 0)
				this._length += validContentSize;
		}

		/// <summary>
		/// Writes an Int32 value type content and returns its location for possible further update.
		/// </summary>
		/// <param name="number">Int32 value to write.</param>
		/// <param name="chunk">Chunk the value was stored at.</param>
		/// <param name="position">Chunk location where the value was saved.</param>
		public void WriteInt32AndRememberItsLocation(int number, out byte[] chunk, out int position)
		{
			if (this._disposed)
				throw new ObjectDisposedException("GenuineChunkedStream");

			if (this._currentWriteBlock == null || this._writePosition >= this._currentWriteBlock.Length - 4)
				this.ObtainNextBuffer();

			// set returned values
			chunk = this._currentWriteBlock;
			position = this._writePosition;

			// write the value
			MessageCoder.WriteInt32(this._currentWriteBlock, this._writePosition, number);

			// and advance the pointer
			this._writePosition += 4;
			this._chunksAndStreams[this._chunksAndStreams.Count - 1] = this._writePosition;
			if (this._length >= 0)
				this._length += 4;
		}

		#endregion

		#region -- IDisposable Members -------------------------------------------------------------

		/// <summary>
		/// Returns all chunks back to the buffer pool.
		/// </summary>
		public void Dispose()
		{
			this.Close();
		}

		#endregion

		#region -- ICloneable Members --------------------------------------------------------------

		/// <summary>
		/// Creates a new object that is a copy of the current instance.
		/// </summary>
		/// <returns>A new object that is a copy of this instance.</returns>
		public object Clone()
		{
			if (this._disposed)
				throw new ObjectDisposedException("GenuineChunkedStream");
			if (this._releaseOnReadMode)
				throw new InvalidOperationException();

			GenuineChunkedStream copy = new GenuineChunkedStream(false);
			copy._chunksAndStreams = this._chunksAndStreams;
			copy._currentWriteBlock = this._currentWriteBlock;
			copy._length = this._length;
			copy._writePosition = this._writePosition;

			// prevents buffers from being returned to Buffer Pool
			this._cloned = true;
			copy._cloned = true;

			return copy;
		}

		#endregion

	}
}
