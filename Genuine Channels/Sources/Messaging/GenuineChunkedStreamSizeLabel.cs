/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

namespace Belikov.GenuineChannels.Messaging
{
	/// <summary>
	/// Writes the difference of the length property into the provided stream.
	/// </summary>
	public struct GenuineChunkedStreamSizeLabel : IDisposable
	{
		/// <summary>
		/// Constructs an instance of the GenuineChunkedStreamSizeLabel class.
		/// </summary>
		/// <param name="outputStream"></param>
		public GenuineChunkedStreamSizeLabel(GenuineChunkedStream outputStream)
		{
			this._outputStream = outputStream;
			this._outputStream.WriteInt32AndRememberItsLocation(0, out this._chunk, out this._position);
			this._initialLength = this._outputStream.Length;
		}

		private GenuineChunkedStream _outputStream;
		private byte[] _chunk;
		private int _position;
		private long _initialLength;

		/// <summary>
		/// Updates the difference of the length property.
		/// </summary>
		public void Dispose()
		{
			MessageCoder.WriteInt32(this._chunk, this._position, (int) (this._outputStream.Length - this._initialLength) );
		}
	}
}
