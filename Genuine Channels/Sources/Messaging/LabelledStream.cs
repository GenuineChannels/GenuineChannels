/* Genuine Channels product.
 *
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 *
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.IO;

using Belikov.GenuineChannels.BufferPooling;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.Messaging
{
	/// <summary>
	/// Implements a stream reading labelled content from the specified source.
	/// </summary>
	public class LabelledStream : Stream, IDisposable
	{
		/// <summary>
		/// Constructs the instance of the LabelledStream class.
		/// </summary>
		/// <param name="iTransportContext">The transport context.</param>
		/// <param name="underlyingStream">The source stream containing labelled content.</param>
		/// <param name="intermediateBuffer">The intermediate buffer.</param>
		public LabelledStream(ITransportContext iTransportContext, Stream underlyingStream, byte[] intermediateBuffer)
		{
			this.ITransportContext = iTransportContext;
			this._underlyingStream = underlyingStream;

			this._readBuffer = intermediateBuffer;

			this._validLength = 0;
			this._currentPosition = 0;
			this._currentPacketSize = 0;
			this._currentPacketBytesRead = 0;
			this._messageRead = false;
		}

		/// <summary>
		/// The transport context.
		/// </summary>
		public ITransportContext ITransportContext;

		private Stream _underlyingStream;

		private byte[] _readBuffer;
		private int _validLength;
		private int _currentPosition;
		private int _currentPacketSize;
		private int _currentPacketBytesRead;
		private bool _messageRead;

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

				if (count <= 0 || (this._currentPacketBytesRead >= this._currentPacketSize && this._messageRead))
					return resultSize;

				ReadNextPortion();
			}
		}

		/// <summary>
		/// Reads a byte from the stream and advances the position within the stream by one byte, or returns -1 if at the end of the stream.
		/// </summary>
		/// <returns>The unsigned byte cast to an Int32, or -1 if at the end of the stream.</returns>
		public override int ReadByte()
		{
			// get a byte
			if (this._currentPosition < this._validLength)
				return this._readBuffer[ this._currentPosition++ ];

			ReadNextPortion();

			if (this._currentPosition < this._validLength)
				return this._readBuffer[ this._currentPosition++ ];

			return -1;
		}

		/// <summary>
		/// Synchronously reads the next network packet if it is available.
		/// </summary>
		private void ReadNextPortion()
		{
			int bytesRead = 0;
			int lengthToRead = 0;

			// try to read the remaining part of the packet
			if (this._currentPacketBytesRead < this._currentPacketSize)
			{
				// read the next part of the packet
				lengthToRead = Math.Min(this._currentPacketSize - this._currentPacketBytesRead, this._readBuffer.Length);

				this._validLength = this._underlyingStream.Read(this._readBuffer, 0, lengthToRead);
				if (this._validLength == 0)
					throw GenuineExceptions.Get_Receive_Portion();

				this.ITransportContext.ConnectionManager.IncreaseBytesReceived(this._validLength);
				this._currentPacketBytesRead += this._validLength;
				this._currentPosition = 0;
				return ;
			}

			// the underlying stream ends
			if (this._messageRead)
				return ;

			// prepare for reading a header
			lengthToRead = MessageCoder.LABEL_HEADER_SIZE;
			this._currentPosition = 0;

			// read the header
			while (this._currentPosition < lengthToRead)
			{
				bytesRead = this._underlyingStream.Read(this._readBuffer, this._currentPosition, lengthToRead - this._currentPosition);

				if (bytesRead == 0)
					throw GenuineExceptions.Get_Receive_Portion();

				this.ITransportContext.ConnectionManager.IncreaseBytesReceived(bytesRead);
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

//				GenuineUtility.ReadDataFromStream(this._underlyingStream, this._readBuffer, 0, lengthToRead);
//				this._validLength = lengthToRead;

				this._validLength = this._underlyingStream.Read(this._readBuffer, 0, lengthToRead);
				if (this._validLength == 0)
					throw GenuineExceptions.Get_Receive_Portion();

				this.ITransportContext.ConnectionManager.IncreaseBytesReceived(this._validLength);
				this._currentPacketBytesRead += this._validLength;
				this._currentPosition = 0;
			}
		}

		/// <summary>
		/// Closes the current stream and releases any resources associated with the current stream.
		/// </summary>
		public override void Close()
		{
			// read the remaining bytes
			while (this._currentPacketBytesRead < this._currentPacketSize || ! this._messageRead)
			{
				ReadNextPortion();
			}
		}

		/// <summary>
		/// Gets a value indicating whether the current stream supports reading.
		/// </summary>
		public override bool CanRead
		{
			get
			{
				return this._underlyingStream.CanRead || this._currentPosition < this._validLength;
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
		/// Clears all buffers for this stream and causes any buffered data to be written to the underlying device.
		/// </summary>
		public override void Flush()
		{
			throw new NotSupportedException();
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

		/// <summary>
		/// Writes a byte to the current position in the stream and advances the position within the stream by one byte.
		/// </summary>
		/// <param name="val">The byte to write to the stream.</param>
		public override void WriteByte(byte val)
		{
			throw new NotSupportedException();
		}

		/// <summary>
		/// Releases acquired resources.
		/// </summary>
		public new void Dispose()
		{
			this.Close();
		}
	}
}
