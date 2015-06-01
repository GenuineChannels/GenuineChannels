/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.IO;

using Belikov.GenuineChannels.Messaging;

namespace Belikov.GenuineChannels.Logbook
{
	/// <summary>
	/// Implements writing of log messages into a memory stream. The implementation will ignore all messages if the summary size of
	/// put messages exceed the specified boundary.
	/// </summary>
	public class MemoryWritingStream : Stream
	{
		/// <summary>
		/// Constructs an instance of the MemoryWritingStream class.
		/// </summary>
		/// <param name="maximumSize">The maximum size of space allocated for debugging messages.</param>
		public MemoryWritingStream(int maximumSize)
		{
			this._maximumSize = maximumSize;
		}

		/// <summary>
		/// Gets an object that can be used to synchronize access to this instance.
		/// </summary>
		public object SyncRoot
		{
			get
			{
				return this;
			}
		}

		/// <summary>
		/// Gets or sets an indication of whether the log records can be written into this instance.
		/// </summary>
		public bool Enabled
		{
			get
			{
				lock (this.SyncRoot)
				{
					return this._enabled;
				}
			}
			set
			{
				lock (this.SyncRoot)
				{
					this._enabled = value;
				}
			}
		}
		private bool _enabled = true;

		/// <summary>
		/// Stops logging and releases all memory chunks.
		/// </summary>
		public void StopLogging()
		{
			this.Enabled = false;
			this._baseStream.WriteTo(Stream.Null);
		}

		#region -- Allocated Size counting ---------------------------------------------------------

		private int _bytesWritten = 0;
		private int _maximumSize;
		private bool _isCurrentRecordIgnored = true;

		/// <summary>
		/// Checks whether the current record must be ignored.
		/// </summary>
		private void CheckIfAllowedSizeExceeded()
		{
			this._isCurrentRecordIgnored = ( ! this.Enabled ) || this._bytesWritten >= this._maximumSize;
		}

		#endregion

		#region -- Writing -------------------------------------------------------------------------

		private GenuineChunkedStream _baseStream = new GenuineChunkedStream(true);
		private bool _isRecordStarted = false;
		private int _sizeOfRecord = 0;

		/// <summary>
		/// Writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
		/// </summary>
		/// <param name="buffer">An array of bytes.</param>
		/// <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes to the current stream.</param>
		/// <param name="count">The number of bytes to be written to the current stream.</param>
		public override void Write(byte[] buffer, int offset, int count)
		{
			lock (this.SyncRoot)
			{
				if (! this._isRecordStarted)
				{
					this.CheckIfAllowedSizeExceeded();
					this._isRecordStarted = true;
				}

				if (! this._isCurrentRecordIgnored)
				{
					this._baseStream.Write(buffer, offset, count);
					this._sizeOfRecord += count;
				}
			}
		}

		/// <summary>
		/// Writes a byte to the current position in the stream and advances the position within the stream by one byte.
		/// </summary>
		/// <param name="val">The byte to write to the stream.</param>
		public override void WriteByte(byte val)
		{
			lock (this.SyncRoot)
			{
				if (! this._isRecordStarted)
				{
					this.CheckIfAllowedSizeExceeded();
					this._isRecordStarted = true;
				}

				if (! this._isCurrentRecordIgnored)
				{
					this._baseStream.WriteByte(val);
					this._sizeOfRecord ++;
				}
			}
		}

		/// <summary>
		/// Clears all buffers for this stream and causes any buffered data to be written to the underlying device.
		/// </summary>
		public override void Flush()
		{
			lock (this.SyncRoot)
			{
				this._baseStream.Flush();
				this._isRecordStarted = false;
				this._bytesWritten += this._sizeOfRecord;
				this._sizeOfRecord = 0;
			}
		}

		#endregion

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
			lock (this.SyncRoot)
			{
				int bytesRead = this._baseStream.Read(buffer, offset, Math.Min(this._bytesWritten - this._sizeOfRecord, count));
				this._bytesWritten -= bytesRead;
				return bytesRead;
			}		
		}

		#endregion

		#region -- Insignificat Stream members -----------------------------------------------------

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
				// is used for tests
				return this._bytesWritten;
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
		/// Closes the stream.
		/// </summary>
		public override void Close()
		{
		}

		/// <summary>
		/// Reads a byte from the stream and advances the position within the stream by one byte, or returns -1 if at the end of the stream.
		/// </summary>
		/// <returns>The unsigned byte cast to an Int32, or -1 if at the end of the stream.</returns>
		public override int ReadByte()
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

		#endregion
	}
}
