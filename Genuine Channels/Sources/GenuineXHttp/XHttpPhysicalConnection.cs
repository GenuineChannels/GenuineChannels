/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;

using Belikov.Common.ThreadProcessing;

using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Parameters;
using Belikov.GenuineChannels.Receiving;
using Belikov.GenuineChannels.Security;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Utilities;

namespace Belikov.GenuineChannels.GenuineXHttp
{
	/// <summary>
	/// Represents a connection based on Socket.
	/// </summary>
	internal class XHttpPhysicalConnection : PhysicalConnection
	{
		/// <summary>
		/// Constructs an instance of the XHttpPhysicalConnection class.
		/// </summary>
		/// <param name="xHttpConnection">The physical connection.</param>
		/// <param name="sender">The sender.</param>
		public XHttpPhysicalConnection(XHttpConnection xHttpConnection, bool sender)
		{
			this.XHttpConnection = xHttpConnection;
			this.IsSender = sender;

			this.AsyncSendBuffer = new byte[35000];
			this.AsyncReceiveBuffer = new byte[35000];

#if DEBUG
			this._connectionNumber = Interlocked.Increment(ref _TotalConnections);
#endif
		}

		/// <summary>
		/// The socket.
		/// </summary>
		public Socket Socket;

		/// <summary>
		/// The URI of the remote entry.
		/// </summary>
		public string EntryUri;

		/// <summary>
		/// The remote host.
		/// </summary>
		public HostInformation Remote
		{
			get
			{
				return this.XHttpConnection.Remote;
			}
		}

		/// <summary>
		/// The parent logical connection.
		/// </summary>
		public XHttpConnection XHttpConnection;

		/// <summary>
		/// Gets an indication whether this inatance represents sender connection.
		/// </summary>
		public bool IsSender;

		/// <summary>
		/// The current number of sequence.
		/// </summary>
		public int SequenceNo = 0;

		#region -- Debug info ----------------------------------------------------------------------

#if DEBUG
		private static int _TotalConnections = 0;
		private int _connectionNumber;

		/// <summary>
		/// Returns a String that represents the current Object.
		/// </summary>
		/// <returns>A String that represents the current Object.</returns>
		public override string ToString()
		{
			return string.Format("(No: {0}. Type: {1}. Sender: {2}. Client: {3}. Is disposed: {4})", this._connectionNumber, 
				this.TypeOfSocket == null ? "<not specified>" : this.TypeOfSocket,
				this.IsSender, this.XHttpConnection.IsClient, this.IsDisposed);
		}

#endif

		#endregion

		#region -- Sending -------------------------------------------------------------------------

		/// <summary>
		/// Cached local end point of the client.
		/// </summary>
		public string LocalEndPoint;

		/// <summary>
		/// Contains all messages currently being sent to the remote host.
		/// </summary>
		public ArrayList MessagesBeingSent = new ArrayList();

		/// <summary>
		/// The content being sent to the remote host.
		/// </summary>
		public Stream SentContent;

		/// <summary>
		/// The buffer for intermediate serialization.
		/// </summary>
		public byte[] AsyncSendBuffer;

		/// <summary>
		/// Size of the valid content being contained in SendingBuffer.
		/// </summary>
		public int AsyncSendBufferSizeOfValidContent;

		/// <summary>
		/// Current position in SendingBuffer.
		/// </summary>
		public int AsyncSendBufferCurrentPosition;

		/// <summary>
		/// The stream containing the data being sent to the remote host.
		/// </summary>
		public Stream AsyncSendStream;

		/// <summary>
		/// An indication of the end of the current sequence.
		/// </summary>
		public bool AsyncSendBufferIsLastPacket;

		#endregion

		#region -- Receiving -----------------------------------------------------------------------

		/// <summary>
		/// The buffer for analyzing incoming content.
		/// </summary>
		public byte[] AsyncReceiveBuffer;

		#endregion

		#region -- Connection Management -----------------------------------------------------------

		/// <summary>
		/// Closes the socket.
		/// </summary>
		public void CloseSocket()
		{
			lock (this._accessToLocalMembers)
			{
				GenuineThreadPool.QueueUserWorkItem(new WaitCallback(SocketUtility.CloseSocket), this.Socket, false);
				this.Socket = null;
			}
		}

		/// <summary>
		/// Opens the socket if it is not opened or not connected.
		/// </summary>
		public void CheckConnectionStatus()
		{
			if (this.IsDisposed)
				throw new ObjectDisposedException("XHttpPhysicalConnection");

			using (new ReaderAutoLocker(this.DisposeLock))
			{
				lock (this.PhysicalConnectionStateLock)
				{
					if (this.EntryUri == null)
						GenuineUtility.ParseUrl(this.Remote.Url, out this.EntryUri);

					if (this.Socket != null && ! this.Socket.Connected)
						this.CloseSocket();

					if (this.Socket == null && ! this.XHttpConnection.IsClient)
						throw GenuineExceptions.Get_Processing_TransportConnectionFailed();

					if (this.Socket != null && this.LocalEndPoint == null)
						this.LocalEndPoint = this.Socket.LocalEndPoint.ToString();

					if (this.Socket != null)
						return ;
				}

				string objectUri;
				string url = GenuineUtility.Parse(this.Remote.Url, out objectUri);

				// parse provided url and fetch a port and IP address
				int portNumber;
				string hostName = GenuineUtility.SplitHttpLinkToHostAndPort(url, out portNumber);

				// resolve host name to IP address
				IPAddress ipAddress = GenuineUtility.ResolveIPAddress(hostName);

				// get the address of the remote host
				IPEndPoint ipEndPoint = new IPEndPoint(ipAddress, portNumber);
				if (ipEndPoint == null)
					return;

				// create and setup a socket
				Socket socket = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
				LingerOption lingerOption = new LingerOption(true, 3);
				socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, lingerOption);

//				socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoDelay, 1);

				// connect to the remote host
				socket.Connect(ipEndPoint);
				if (! socket.Connected)
					throw GenuineExceptions.Get_Connect_CanNotConnectToRemoteHost(url, "The value of the Socket.Connected property is false after connecting.");

				this.Remote.PhysicalAddress = socket.RemoteEndPoint;
				this.Remote.LocalPhysicalAddress = socket.LocalEndPoint;

				if (this.LocalEndPoint == null)
					this.LocalEndPoint = socket.LocalEndPoint.ToString();

				lock (this._accessToLocalMembers)
				{
					this.Socket = socket;
				}

#if DEBUG
				lock (this.PhysicalConnectionStateLock)
				{
					this.TypeOfSocket = "Opened";
				}
#endif

			}
		}

		#endregion

		#region -- Listener ------------------------------------------------------------------------

//		/// <summary>
//		/// The time when the listening request was received.
//		/// </summary>
//		public int Listener_Opened;

		#endregion

		#region -- Disposing -----------------------------------------------------------------------

		/// <summary>
		/// Releases resources.
		/// </summary>
		/// <param name="reason">The reason of disposing.</param>
		public override void InternalDispose(Exception reason)
		{
			if (this.Socket != null)
			{
				GenuineThreadPool.QueueUserWorkItem(new WaitCallback(SocketUtility.CloseSocket), this.Socket, true);
				this.Socket = null;
			}

			lock (this.PhysicalConnectionStateLock)
			{
				if (this.SentContent != null)
				{
					this.SentContent.Close();
					this.SentContent = null;
				}

				if (this.MessagesBeingSent != null)
					foreach (Message message in this.MessagesBeingSent)
						this.XHttpConnection.ITransportContext.IIncomingStreamHandler.DispatchException(message, reason);
				this.MessagesBeingSent.Clear();
			}
		}

		#endregion
	}
}
