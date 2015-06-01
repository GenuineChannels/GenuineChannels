/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.IO;

namespace Belikov.GenuineChannels.Logbook
{
	/// <summary>
	/// Implements a transactional writing to the file.
	/// </summary>
	public class FileWritingStream : Stream
	{
		/// <summary>
		/// Constructs an instance of the FileWritingStream class.
		/// </summary>
		/// <param name="baseFileName">The base part of the file name.</param>
		/// <param name="addSuffixToBaseFileName">true to add suffix (date, time, and extension) to the file name.</param>
		public FileWritingStream(string baseFileName, bool addSuffixToBaseFileName)
		{
			if (baseFileName == null)
				throw new NullReferenceException();

			this._addSuffixToBaseFileName = addSuffixToBaseFileName;
			this._baseFileName = baseFileName;
		}
		
		/// <summary>
		/// Constructs an instance of the FileWritingStream class.
		/// </summary>
		/// <param name="baseFileName">The base part of the file name.</param>
		public FileWritingStream(string baseFileName) : this(baseFileName, true) {}

		private bool _addSuffixToBaseFileName;

		private string _baseFileName;
		
		#region -- Writing methods -----------------------------------------------------------------

		private DateTime _lastDateTimeValue = DateTime.MinValue;
		private Stream _fileStream;
		private bool _isRecordStarted = false;
		private int nextTry = GenuineUtility.TickCount;

		/// <summary>
		/// Ensures that the file is opened. Opens the file if necessary.
		/// </summary>
		private void CheckThatCorrectFileIsOpened()
		{
			try
			{
				DateTime now = DateTime.Today;
				if ( (this._lastDateTimeValue != now || _fileStream == null) && GenuineUtility.IsTimeoutExpired(nextTry) )
				{
					if (this._fileStream != null)
						this._fileStream.Close();

					this._lastDateTimeValue = DateTime.Today;

					string filename = this._baseFileName;
					if (this._addSuffixToBaseFileName)
						filename += "." + this._lastDateTimeValue.ToString("yyyy-MM-dd") + ".genchlog";

					this._fileStream = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
					this._fileStream.Seek(0, SeekOrigin.End);
				}
			}
			catch
			{
				this._fileStream = null;
				nextTry = GenuineUtility.GetTimeout(15000);
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
			if (! this._isRecordStarted)
			{
				this.CheckThatCorrectFileIsOpened();
				this._isRecordStarted = true;
			}

			if (this._fileStream != null)
				this._fileStream.Write(buffer, offset, count);
		}

		/// <summary>
		/// Writes a byte to the current position in the stream and advances the position within the stream by one byte.
		/// </summary>
		/// <param name="val">The byte to write to the stream.</param>
		public override void WriteByte(byte val)
		{
			if (! this._isRecordStarted)
			{
				this.CheckThatCorrectFileIsOpened();
				this._isRecordStarted = true;
			}

			if (this._fileStream != null)
				this._fileStream.WriteByte(val);
		}

		/// <summary>
		/// Clears all buffers for this stream and causes any buffered data to be written to the underlying device.
		/// </summary>
		public override void Flush()
		{
			if (this._fileStream != null)
				this._fileStream.Flush();
			this._isRecordStarted = false;
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
				return this._fileStream.Position;
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
