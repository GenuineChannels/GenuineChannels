/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Utilities;

namespace Belikov.GenuineChannels.GenuineTcp
{
	/// <summary>
	/// Initiates asynchronous sending to the socket.
	/// </summary>
	internal class Async_InitiateSocketSending : IAsyncWorkItem
	{
		/// <summary>
		/// Constructs an instance of the Async_InitiateSocketSending class.
		/// </summary>
		/// <param name="socket">The socket.</param>
		/// <param name="buffer">The buffer.</param>
		/// <param name="offset">The offset.</param>
		/// <param name="size">The size of the sending chunk.</param>
		/// <param name="asyncCallback">The callback.</param>
		/// <param name="state">The optional parameter.</param>
		public Async_InitiateSocketSending(Socket socket, byte[] buffer, int offset, int size, AsyncCallback asyncCallback, object state)
		{
			this._socket = socket;
			this._buffer = buffer;
			this._offset = offset;
			this._size = size;
			this._asyncCallback = asyncCallback;
			this._state = state;
		}

		private Socket _socket;
		private byte[] _buffer;
		private int _offset;
		private int _size;
		private AsyncCallback _asyncCallback;
		private object _state;

		/// <summary>
		/// Initiates an asynchronous operation.
		/// </summary>
		public void StartAsynchronousOperation()
		{
			// LOG:
			BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;

			try
			{
				this._socket.BeginSend(this._buffer, this._offset, this._size, SocketFlags.None, this._asyncCallback, this._state);

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.LowLevelTransport] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.LowLevelTransport, "Async_InitiateSocketSending.StartAsynchronousOperation",
						LogMessageType.LowLevelTransport_AsyncSendingInitiated, null, null, null, 
						new MemoryStream(this._buffer, this._offset, this._size), 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, -1, this._buffer.GetHashCode(), this._offset, this._size, null, null, null, null, 
						"Sending has been initiated. Socket: {0}.", (long) this._socket.Handle);
				}
			}
			catch(Exception ex)
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.LowLevelTransport] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.LowLevelTransport, "Async_InitiateSocketSending.StartAsynchronousOperation",
						LogMessageType.LowLevelTransport_AsyncSendingInitiated, ex, null, null, 
						new MemoryStream(this._buffer, this._offset, this._size), 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, -1, this._buffer.GetHashCode(), this._offset, this._size, null, null, null, null, 
						"Sending cannot be initiated. Socket: {0}.", (long) this._socket.Handle);
				}
			}

		}

	}
}
