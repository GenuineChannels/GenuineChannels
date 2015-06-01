/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.IO;
using System.Security;
using System.Security.Cryptography;

namespace Belikov.GenuineChannels.Security
{
	/// <summary>
	/// Implements a resettable stream re-creating an underlying stream for the message
	/// re-sending.
	/// </summary>
	public class ResettableCryptoStream : Stream
	{
		/// <summary>
		/// Constructs an instance of the ResettableCryptoStream class.
		/// </summary>
		/// <param name="underlyingStream">The source stream.</param>
		/// <param name="encryptor">The encryptor.</param>
		public ResettableCryptoStream(Stream underlyingStream, ICryptoTransform encryptor)
		{
			this._underlyingStream = underlyingStream;
			this._encryptor = encryptor;
			this._currentStream = new CryptoStream(new FinishReadingStream(underlyingStream), this._encryptor, CryptoStreamMode.Read);
		}

		/// <summary>
		/// The underlying stream.
		/// </summary>
		private Stream _underlyingStream;

		/// <summary>
		/// The cryptographic encryptor.
		/// </summary>
		private ICryptoTransform _encryptor;

		/// <summary>
		/// The current encryptor.
		/// </summary>
		private Stream _currentStream;

		/// <summary>
		/// Gets a value indicating whether the current stream supports reading.
		/// </summary>
		public override bool CanRead 
		{
			get
			{
				return this._currentStream.CanRead;
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
				return this._currentStream.CanWrite;
			}
		}

		/// <summary>
		/// Gets the length in bytes of the stream.
		/// </summary>
		public override long Length 
		{
			get
			{
				return this._currentStream.Length;
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
				if (value != 0)
					throw new NotSupportedException("Only setting position to zero is supported.");

				this.Seek(0, SeekOrigin.Begin);
			}
		}

		/// <summary>
		/// Closes the current stream and releases any resources associated with the current stream.
		/// </summary>
		public override void Close()
		{
			this._currentStream.Close();
			this._underlyingStream.Close();
		}

		/// <summary>
		/// Clears all buffers for this stream and causes any buffered data to be written to the underlying device.
		/// </summary>
		public override void Flush()
		{
			this._currentStream.Close();
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
			return this._currentStream.Read(buffer, offset, count);
		}

		/// <summary>
		/// Reads a byte from the stream and advances the position within the stream by one byte, or returns -1 if at the end of the stream.
		/// </summary>
		/// <returns>The unsigned byte cast to an Int32, or -1 if at the end of the stream.</returns>
		public override int ReadByte()
		{
			return this._currentStream.ReadByte();
		}

		/// <summary>
		/// Sets the position within the current stream.
		/// </summary>
		/// <param name="offset">A byte offset relative to the origin parameter.</param>
		/// <param name="origin">A value of type SeekOrigin indicating the reference point used to obtain the new position.</param>
		/// <returns>The new position within the current stream.</returns>
		public override long Seek(long offset, SeekOrigin origin)
		{
			if (offset != 0 || origin != SeekOrigin.Begin)
				throw new NotSupportedException("Only setting position to the very beginning is supported.");

			this._underlyingStream.Position = 0;
			this._currentStream = new CryptoStream(new FinishReadingStream(this._underlyingStream), this._encryptor, CryptoStreamMode.Read);
			return 0;
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
			this._underlyingStream.Write(buffer, offset, count);
		}

		/// <summary>
		/// Writes a byte to the current position in the stream and advances the position within the stream by one byte.
		/// </summary>
		/// <param name="val">The byte to write to the stream.</param>
		public override void WriteByte(byte val)
		{
			this._underlyingStream.WriteByte(val);
		}

	}
}
