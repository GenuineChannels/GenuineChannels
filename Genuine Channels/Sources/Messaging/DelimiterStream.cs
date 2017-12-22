/* Genuine Channels product.
 *
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 *
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.IO;

using Belikov.GenuineChannels.BufferPooling;

namespace Belikov.GenuineChannels.Messaging
{
	/// <summary>
	/// DelimiterStream applies length constraint to the underlying stream.
	/// The chunk with the specified size is automatically read up to the end after this stream is closed.
	/// DelimiterStream does not close the underlying stream.
	/// </summary>
	public class DelimiterStream : Stream, IDisposable
	{
		/// <summary>
		/// Constructs an instance of the DelimiterStream class.
		/// </summary>
		/// <param name="underlyingStream">The underlying stream.</param>
		/// <param name="assumedLength">The size allowed to be read.</param>
		public DelimiterStream(Stream underlyingStream, long assumedLength)
		{
			this._underlyingStream = underlyingStream;
			this._assumedLength = assumedLength;
		}

		private Stream _underlyingStream;
		private long _assumedLength;

		/// <summary>
		/// Gets a value indicating whether the current stream supports reading.
		/// </summary>
		public override bool CanRead
		{
			get
			{
				return this._underlyingStream.CanRead;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the current stream supports seeking.
		/// </summary>
		public override bool CanSeek
		{
			get
			{
				return this._underlyingStream.CanSeek;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the current stream supports writing.
		/// </summary>
		public override bool CanWrite
		{
			get
			{
				return this._underlyingStream.CanWrite;
			}
		}

		/// <summary>
		/// Gets the length in bytes of the stream.
		/// </summary>
		public override long Length
		{
			get
			{
				return this._assumedLength;
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
				return this._underlyingStream.Position;
			}
			set
			{
				throw new NotSupportedException("DelimiterStream does not support Position property.");
			}
		}

		/// <summary>
		/// Completes reading from the underlying stream.
		/// </summary>
		public new void Dispose()
		{
			this.Close();
		}

		/// <summary>
		/// Closes the current stream and releases any resources associated with the current stream.
		/// </summary>
		public override void Close()
		{
			// read the remaining bytes
			if (this._assumedLength > 0)
				using ( BufferKeeper bufferKeeper = new BufferKeeper(0) )
				{
					while (this._assumedLength > 0)
					{
						int bytesRead = this._underlyingStream.Read(bufferKeeper.Buffer, 0, (int) this._assumedLength);
						this._assumedLength -= bytesRead;
						if (bytesRead == 0)
							throw GenuineExceptions.Get_Receive_IncorrectData();
					}
				}
		}

		/// <summary>
		/// Clears all buffers for this stream and causes any buffered data to be written to the underlying device.
		/// </summary>
		public override void Flush()
		{
			this._underlyingStream.Flush();
		}

		/// <summary>
		/// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
		/// </summary>
		/// <param name="buffer">An array of bytes.</param>
		/// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream.</param>
		/// <param name="count">The maximum number of bytes to be read from the current stream.</param>
		/// <returns>The total number of bytes read into the buffer.</returns>
		public override int Read(byte[] buffer, int offset, int count)
		{
			count = Math.Min(count, (int) this._assumedLength);

			if (count <= 0)
				return 0;

			int bytesRead = this._underlyingStream.Read(buffer, offset, count);
			this._assumedLength -= bytesRead;
			return bytesRead;
		}

		/// <summary>
		/// Reads a byte from the stream and advances the position within the stream by one byte, or returns -1 if at the end of the stream.
		/// </summary>
		/// <returns>The unsigned byte cast to an Int32, or -1 if at the end of the stream.</returns>
		public override int ReadByte()
		{
			int result = this._assumedLength > 0 ? this._underlyingStream.ReadByte() : -1;

			if (result != -1)
				this._assumedLength --;

			return result;
		}

		/// <summary>
		/// Sets the position within the current stream.
		/// </summary>
		/// <param name="offset">A byte offset relative to the origin parameter.</param>
		/// <param name="origin">A value of type SeekOrigin indicating the reference point used to obtain the new position.</param>
		/// <returns>The new position within the current stream.</returns>
		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException("DelimiterStream does not support Seek method.");
		}

		/// <summary>
		/// Sets the length of the current stream.
		/// </summary>
		/// <param name="val">The desired length of the current stream in bytes.</param>
		public override void SetLength(long val)
		{
			throw new NotSupportedException("DelimiterStream does not support SetLength method.");
		}

		/// <summary>
		/// Writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
		/// </summary>
		/// <param name="buffer">An array of bytes.</param>
		/// <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes to the current stream.</param>
		/// <param name="count">The number of bytes to be written to the current stream.</param>
		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException("DelimiterStream does not support Write method.");
		}

		/// <summary>
		/// Writes a byte to the current position in the stream and advances the position within the stream by one byte.
		/// </summary>
		/// <param name="val">The byte to write to the stream.</param>
		public override void WriteByte(byte val)
		{
			throw new NotSupportedException("DelimiterStream does not support WriteByte method.");
		}
	}
}
