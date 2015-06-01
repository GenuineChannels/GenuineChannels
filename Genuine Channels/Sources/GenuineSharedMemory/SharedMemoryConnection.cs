/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Parameters;
using Belikov.GenuineChannels.Security;
using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.GenuineSharedMemory
{
	/// <summary>
	/// Represents a Shared Memory connection.
	/// </summary>
	internal class SharedMemoryConnection : Stream, IDisposable
	{
		/// <summary>
		/// Constructs an instance of the SharedMemoryConnection class.
		/// </summary>
		/// <param name="iTransportContext">The transport context.</param>
		/// <param name="name">The name of the shared chunk.</param>
		/// <param name="isServer">The role.</param>
		/// <param name="setCloseStatusOnExit">Indicates whether it is necessary to set the "closed" status on exit.</param>
		internal SharedMemoryConnection(ITransportContext iTransportContext, string name, bool isServer, bool setCloseStatusOnExit)
		{
			this.ITransportContext = iTransportContext;
			this.ShareName = "GenuineChannels_GShMem_" + name;
			this.IsServer = isServer;
			this._setCloseStatusOnExit = setCloseStatusOnExit;

			this._shareSize = (int) iTransportContext.IParameterProvider[GenuineParameter.SMShareSize];
			this._pingTimeOut = GenuineUtility.ConvertToMilliseconds(iTransportContext.IParameterProvider[GenuineParameter.PersistentConnectionSendPingAfterInactivity]);
			this._closeAfterInactivity = GenuineUtility.ConvertToMilliseconds(iTransportContext.IParameterProvider[GenuineParameter.ClosePersistentConnectionAfterInactivity]);

			string localSideName = (isServer ? "Server" : "Client");
			string remoteSideName = (isServer ? "Client" : "Server");

			IParameterProvider parameters = this.ITransportContext.IParameterProvider;

			// construct shared object names for the local side
			string readCompletedEventName = GenuineSharedMemoryChannel.ConstructSharedObjectName(
				this.ShareName + localSideName + "ReadCompleted", parameters);
			string writeCompletedEventName = GenuineSharedMemoryChannel.ConstructSharedObjectName(
				this.ShareName + localSideName + "WriteCompleted", parameters);

			// construct shared object names for the remote side
			string remoteReadCompletedEventName = GenuineSharedMemoryChannel.ConstructSharedObjectName(
				this.ShareName + remoteSideName + "ReadCompleted", parameters);
			string remoteWriteCompletedEventName = GenuineSharedMemoryChannel.ConstructSharedObjectName(
				this.ShareName + remoteSideName + "WriteCompleted", parameters);

			if (isServer)
			{
				if (this._shareSize < MIN_SHARE_SIZE || this._shareSize > MAX_SHARE_SIZE)
					throw GenuineExceptions.Get_Channel_InvalidParameter("SMShareSize");

				this.LowLevel_CreateSharedMemory();
				this._closed = 0;
				this._writtenShareSize = this._shareSize;

				this._receiveOffset = 5;
				this._sendOffset = (this._shareSize - 5) / 2;
				this._receiveSpaceSize = this._sendOffset - 5 - 8;
				this._sendSpaceSize = this._shareSize - this._sendOffset - 8;

				this._namedEventReadCompleted = NamedEvent.CreateNamedEvent(readCompletedEventName, false, true);
				this._namedEventWriteCompleted = NamedEvent.CreateNamedEvent(writeCompletedEventName, false, true);
				this._namedEventRemoteReadCompleted = NamedEvent.CreateNamedEvent(remoteReadCompletedEventName, false, true);
				this._namedEventRemoteWriteCompleted = NamedEvent.CreateNamedEvent(remoteWriteCompletedEventName, false, true);
			}
			else
			{
				this.OpenSharedMemory();

				if (this._closed != 0)
					throw GenuineExceptions.Get_Connect_CanNotConnectToRemoteHost(name, "Remote host has already closed the connection.");

				this._shareSize = this._writtenShareSize;
				if (this._shareSize < MIN_SHARE_SIZE || this._shareSize > MAX_SHARE_SIZE)
					throw GenuineExceptions.Get_Channel_InvalidParameter("SMShareSize");

				this._receiveOffset = (this._shareSize - 5) / 2;
				this._sendOffset = 5;
				this._receiveSpaceSize = this._shareSize - this._receiveOffset - 8;
				this._sendSpaceSize = this._receiveOffset - 5 - 8;

				this._namedEventReadCompleted = NamedEvent.OpenNamedEvent(readCompletedEventName);
				this._namedEventWriteCompleted = NamedEvent.OpenNamedEvent(writeCompletedEventName);
				this._namedEventRemoteReadCompleted = NamedEvent.OpenNamedEvent(remoteReadCompletedEventName);
				this._namedEventRemoteWriteCompleted = NamedEvent.OpenNamedEvent(remoteWriteCompletedEventName);
			}

			this._sendBuffer = new byte[this._sendSpaceSize];
		}

		/// <summary>
		/// To guarantee atomic access to local members.
		/// </summary>
		private object _accessToLocalMembers = new object();

		/// <summary>
		/// Whether to set the "closed" status of the share on exit.
		/// </summary>
		private bool _setCloseStatusOnExit;

		/// <summary>
		/// Releases resources.
		/// </summary>
		~SharedMemoryConnection()
		{
			this.ReleaseUnmanagedResources();
		}

		/// <summary>
		/// Releases unmanaged resources.
		/// </summary>
		public void ReleaseUnmanagedResources()
		{
			if (this._pointer != IntPtr.Zero)
			{
				if (_setCloseStatusOnExit)
					this._closed = 1;
				WindowsAPI.UnmapViewOfFile(this._pointer);
				WindowsAPI.CloseHandle(this._mapHandle);

				this._pointer = IntPtr.Zero;
				this._mapHandle = IntPtr.Zero;
			}
		}

		/// <summary>
		/// The name of the share.
		/// </summary>
		public string ShareName;

		/// <summary>
		/// The transport context.
		/// </summary>
		public ITransportContext ITransportContext;

		/// <summary>
		/// The role.
		/// </summary>
		public bool IsServer;

		/// <summary>
		/// Whether this connection is valid.
		/// </summary>
		public bool IsValid
		{
			get
			{
				return this._isValid;
			}
			set
			{
				this._isValid = value;
			}
		}
		private object _isValidLock = new object();
		private bool _isValid = true;

		/// <summary>
		/// Connection-level Security Session.
		/// </summary>
		public SecuritySession ConnectionLevelSecurity;

		/// <summary>
		/// The unique connection identifier, which is used for debugging purposes only.
		/// </summary>
		public int DbgConnectionId = ConnectionManager.GetUniqueConnectionId();

		/// <summary>
		/// The remote host.
		/// </summary>
		public HostInformation Remote;

		/// <summary>
		/// Writer must acquire a lock on this object in order to send something.
		/// </summary>
		public object WriteAccess = new object();

		/// <summary>
		/// Size of the share in bytes.
		/// </summary>
		private int _shareSize;

		/// <summary>
		/// Minimum size of the share.
		/// </summary>
		private const int MIN_SHARE_SIZE = 20000;

		/// <summary>
		/// Maximum size of the share.
		/// </summary>
		private const int MAX_SHARE_SIZE = 2000000;

		/// <summary>
		/// The size of the share header.
		/// </summary>
		private const int SHARE_CONTENT_OFFSET = 8;

		/// <summary>
		/// File mapping handle.
		/// </summary>
		private IntPtr _mapHandle = IntPtr.Zero;

		/// <summary>
		/// Unmanaged pointer to memory share.
		/// </summary>
		private IntPtr _pointer = IntPtr.Zero;

		internal NamedEvent _namedEventReadCompleted;
		private NamedEvent _namedEventRemoteReadCompleted;
		private NamedEvent _namedEventWriteCompleted;
		private NamedEvent _namedEventRemoteWriteCompleted;
		private int _receiveOffset;
		private int _receiveSpaceSize;
		private int _sendOffset;
		private int _sendSpaceSize;

		/// <summary>
		/// It's impossible to write stream content to the share directly.
		/// </summary>
		private byte[] _sendBuffer;

		#region -- Memory projections --------------------------------------------------------------

		/// <summary>
		/// Indicates whether the connection was closed.
		/// </summary>
		private byte _closed
		{
			get
			{
				return Marshal.ReadByte(this._pointer);
			}
			set
			{
				Marshal.WriteByte(this._pointer, value);
			}
		}

		/// <summary>
		/// The size of the share.
		/// </summary>
		private int _writtenShareSize
		{
			get
			{
				return Marshal.ReadInt32(this._pointer, 1);
			}
			set
			{
				Marshal.WriteInt32(this._pointer, 1, value);
			}
		}

		private int _MessageToSendTotalSize
		{
			set
			{
				Marshal.WriteInt32(this._pointer, this._sendOffset, value);
			}
		}

		private int _MessageToSendFinishFlag
		{
			set
			{
				Marshal.WriteInt32(this._pointer, this._sendOffset + 4, value);
			}
		}

		private int _MessageToReceiveTotalSize
		{
			get
			{
				return Marshal.ReadInt32(this._pointer, this._receiveOffset);
			}
		}

		private int _MessageToReceiveFinishFlag
		{
			get
			{
				return Marshal.ReadInt32(this._pointer, this._receiveOffset + 4);
			}
		}

		#endregion

		#region -- Low level operations ------------------------------------------------------------

		/// <summary>
		/// Creates shared memory object.
		/// </summary>
		public void LowLevel_CreateSharedMemory()
		{
			if (WindowsAPI.FailureReason != null)
				throw OperationException.WrapException(WindowsAPI.FailureReason);

			IParameterProvider parameters = this.ITransportContext.IParameterProvider;
			string fileMappingName = GenuineSharedMemoryChannel.ConstructSharedObjectName(
				this.ShareName, parameters);

			this._mapHandle = WindowsAPI.CreateFileMapping((IntPtr) (int) -1, WindowsAPI.AttributesWithNullDACL, WindowsAPI.PAGE_READWRITE, 0, (uint) this._shareSize, fileMappingName);
			if (this._mapHandle == IntPtr.Zero)
				throw GenuineExceptions.Get_Windows_CanNotCreateOrOpenSharedMemory(Marshal.GetLastWin32Error());

			this._pointer = WindowsAPI.MapViewOfFile(this._mapHandle, WindowsAPI.SECTION_MAP_READ | WindowsAPI.SECTION_MAP_WRITE, 0, 0, 0);
			if (this._pointer == IntPtr.Zero)
			{
				int lastWinError = Marshal.GetLastWin32Error();
				WindowsAPI.CloseHandle(this._mapHandle);
				throw GenuineExceptions.Get_Windows_SharedMemoryError(lastWinError);
			}
		}

		/// <summary>
		/// Opens the handle of an existent share.
		/// </summary>
		public void OpenSharedMemory()
		{
			IParameterProvider parameters = this.ITransportContext.IParameterProvider;
			string fileMappingName = GenuineSharedMemoryChannel.ConstructSharedObjectName(
				this.ShareName, parameters);

			this._mapHandle = WindowsAPI.OpenFileMapping(WindowsAPI.FILE_MAP_ALL_ACCESS, 0, fileMappingName);
			if (this._mapHandle == IntPtr.Zero)
				throw GenuineExceptions.Get_Windows_CanNotCreateOrOpenSharedMemory(Marshal.GetLastWin32Error());

			this._pointer = WindowsAPI.MapViewOfFile(this._mapHandle, WindowsAPI.SECTION_MAP_READ | WindowsAPI.SECTION_MAP_WRITE, 0, 0, 0);
			if (this._pointer == IntPtr.Zero)
			{
				int lastWinError = Marshal.GetLastWin32Error();
				WindowsAPI.CloseHandle(this._mapHandle);
				throw GenuineExceptions.Get_Windows_SharedMemoryError(lastWinError);
			}
		}

		/// <summary>
		/// Sends a message synchronously. Does not process exceptions!
		/// </summary>
		/// <param name="content">The content being sent to the remote host.</param>
		/// <param name="timeout">The sending must be completed before this moment.</param>
		public void LowLevel_SendSync(Stream content, int timeout)
		{
			// apply the connection-level security session
			if (this.ConnectionLevelSecurity != null)
			{
				GenuineChunkedStream outputStream = new GenuineChunkedStream(true);
				this.ConnectionLevelSecurity.Encrypt(content, outputStream);
				content = outputStream;
			}

			for ( ; ; )
			{
				if (! this.IsValid)
					throw GenuineExceptions.Get_Processing_TransportConnectionFailed();
				if (! GenuineUtility.WaitOne(_namedEventRemoteReadCompleted.ManualResetEvent, GenuineUtility.GetMillisecondsLeft(timeout)) )
					throw GenuineExceptions.Get_Send_Timeout();
				if (this._closed != 0)
					throw GenuineExceptions.Get_Receive_ConnectionClosed();
				this._namedEventRemoteReadCompleted.ManualResetEvent.Reset();

				// read the next portion
				int bytesRead = content.Read(this._sendBuffer, 0, this._sendSpaceSize);

				// LOG:
				BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.LowLevelTransport] > 0 )
				{
					binaryLogWriter.WriteTransportContentEvent(LogCategory.LowLevelTransport, "GenuineSaredMemoryConnection.LowLevel_SendSync",
						LogMessageType.LowLevelTransport_AsyncSendingInitiating, null, null, this.Remote, 
						binaryLogWriter[LogCategory.LowLevelTransport] > 1 ? new MemoryStream(this._sendBuffer, 0, bytesRead) : null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						this.DbgConnectionId, bytesRead, 
						null, null, null,
						"Content sending.");
				}

				Marshal.Copy( this._sendBuffer, 0,
					(IntPtr) (this._pointer.ToInt32() + this._sendOffset + SHARE_CONTENT_OFFSET),
					bytesRead );
				this._MessageToSendTotalSize = bytesRead;
				this._MessageToSendFinishFlag = bytesRead < this._sendSpaceSize ? 1 : 0;

				this._namedEventWriteCompleted.ManualResetEvent.Set();

				if (bytesRead < this._sendSpaceSize)
				{
					this.UpdateLastTimeAMessageWasSent();
					return ;
				}
			}
		}

		/// <summary>
		/// Provides a stream reading synchronously from the given connection.
		/// </summary>
		/// <param name="finishTime">Read timeout.</param>
		/// <returns>A stream.</returns>
		public Stream LowLevel_ReadSync(int finishTime)
		{
			if (! this.IsValid)
				throw GenuineExceptions.Get_Processing_TransportConnectionFailed();

			this._currentPosition = this._validLength;
			this._messageRead = false;
			this._readDeadline = finishTime;

			if (this.ConnectionLevelSecurity != null)
				return this.ConnectionLevelSecurity.Decrypt(this);
			return this;
		}

		#endregion

		#region -- Synchronous reading -------------------------------------------------------------

		private int _readDeadline;

		private int _validLength;
		private int _currentPosition;
		private bool _messageRead;

		/// <summary>
		/// Reads a sequence of bytes from the current connection.
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
				if (! this.IsValid)
					throw GenuineExceptions.Get_Processing_TransportConnectionFailed();

				// check whether we have the next portion
				if (this._currentPosition < this._validLength)
				{
					size = Math.Min(this._validLength - this._currentPosition, count);
					Marshal.Copy( (IntPtr) (this._pointer.ToInt32() + this._receiveOffset + this._currentPosition + SHARE_CONTENT_OFFSET),
						buffer, offset, size);

					this._currentPosition += size;
					count -= size;
					resultSize += size;
					offset += size;
				}

				if (count <= 0 || this._messageRead)
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
				return Marshal.ReadByte(this._pointer, this._receiveOffset + this._currentPosition++ + SHARE_CONTENT_OFFSET);

			ReadNextPortion();

			if (this._currentPosition < this._validLength)
				return Marshal.ReadByte(this._pointer, this._receiveOffset + this._currentPosition++ + SHARE_CONTENT_OFFSET);

			return -1;
		}

		/// <summary>
		/// Synchronously reads the next network packet if it is available.
		/// </summary>
		private void ReadNextPortion()
		{
			if (! this.IsValid)
				throw GenuineExceptions.Get_Processing_TransportConnectionFailed();

			this._namedEventReadCompleted.ManualResetEvent.Set();
			if (! GenuineUtility.WaitOne(this._namedEventRemoteWriteCompleted.ManualResetEvent, GenuineUtility.GetMillisecondsLeft(this._readDeadline)) )
				throw GenuineExceptions.Get_Send_Timeout();
			this._namedEventRemoteWriteCompleted.ManualResetEvent.Reset();

			this._validLength = this._MessageToReceiveTotalSize;
			this._currentPosition = 0;
			this._messageRead = this._MessageToReceiveFinishFlag != 0;

			// LOG:
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.LowLevelTransport] > 0 )
			{
				bool writeContent = binaryLogWriter[LogCategory.LowLevelTransport] > 1;
				byte[] receivedData = new byte[this._validLength];
				Marshal.Copy( (IntPtr) (this._pointer.ToInt32() + this._receiveOffset + this._currentPosition + SHARE_CONTENT_OFFSET),
					receivedData, 0, this._validLength);

				binaryLogWriter.WriteTransportContentEvent(LogCategory.LowLevelTransport, "GenuineSaredMemoryConnection.ReadNextPortion",
					LogMessageType.LowLevelTransport_SyncReceivingCompleted, null, null, this.Remote, 
					writeContent ? new MemoryStream(receivedData, 0, this._validLength) : null, 
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
					this.DbgConnectionId, this._validLength, 
					null, null, null,
					"Content received.");
			}
		}

		/// <summary>
		/// Closes the current stream and releases any resources associated with the current stream.
		/// </summary>
		public override void Close()
		{
			this.SkipMessage();
		}

		/// <summary>
		/// Skip the current message in the transport stream.
		/// </summary>
		public void SkipMessage()
		{
			//			GenuineUtility.CopyStreamToStream(this, Stream.Null);
			while (! this._messageRead)
				ReadNextPortion();
			this._validLength = 0;
		}


		#endregion

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

		#endregion

		#region -- Connection state ----------------------------------------------------------------

		/// <summary>
		/// The state controller.
		/// </summary>
		private ConnectionStateSignaller _connectionStateSignaller;

		/// <summary>
		/// The state controller lock.
		/// </summary>
		private object _connectionStateSignallerLock = new object();

		/// <summary>
		/// Sets the state of the connection.
		/// </summary>
		/// <param name="genuineEventType">The state of the connection.</param>
		/// <param name="reason">The exception.</param>
		/// <param name="additionalInfo">The additional info.</param>
		public void SignalState(GenuineEventType genuineEventType, Exception reason, object additionalInfo)
		{
			lock (this._connectionStateSignallerLock)
			{
				if (this._connectionStateSignaller == null)
					this._connectionStateSignaller = new ConnectionStateSignaller(this.Remote, this.ITransportContext.IGenuineEventProvider);

				this._connectionStateSignaller.SetState(genuineEventType, reason, additionalInfo);
			}
		}

		#endregion

		#region -- Lifetime and ping Management ----------------------------------------------------

		/// <summary>
		/// A time span value indicating how often Connect Manager should check the connection.
		/// </summary>
		internal int _pingTimeOut;

		/// <summary>
		/// Time span during which the remote host must send at least one valid message.
		/// </summary>
		internal int _closeAfterInactivity;

		/// <summary>
		/// Renews connection activity for the CloseConnectionAfterInactivity value.
		/// </summary>
		public void Renew()
		{
			this.Remote.Renew(this._closeAfterInactivity, true);
		}

		/// <summary>
		/// Gets a DateTime representing a moment when last message was sent to the remote host.
		/// </summary>
		public int LastTimeAMessageWasSent
		{
			get
			{
				lock (this._accessToLocalMembers)
					return this._lastTimeAMessageWasSent;
			}
		}
		private int _lastTimeAMessageWasSent;

		/// <summary>
		/// Updates the DateTime when last message was sent.
		/// </summary>
		public void UpdateLastTimeAMessageWasSent()
		{
			lock (this._accessToLocalMembers)
				this._lastTimeAMessageWasSent = GenuineUtility.TickCount;
		}

		#endregion

	}
}
