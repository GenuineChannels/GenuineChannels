/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Threading;
using System.Net;
using System.Net.Sockets;

using Belikov.GenuineChannels.Parameters;
using Belikov.GenuineChannels.Utilities;

namespace Belikov.GenuineChannels.GenuineTcp
{
	/// <summary>
	/// A closure that establishes a connection to the remote host.
	/// </summary>
	internal class ConnectionEstablishingClosure : IDisposable
	{
		public ManualResetEvent Completed;
		public Exception Exception
		{
			get
			{
				lock (this._syncRoot)
					return this._exception;
			}
			set
			{
				lock (this._syncRoot)
					this._exception = value;
			}
		}
		private Exception _exception;

		private int _portNumber;
		private bool _continue;
		private Socket socket;
		private string hostName;
		private IParameterProvider _parameterProvider;
		private object _syncRoot;

		/// <summary>
		/// Constructs an instance of the ConnectionEstablishingClosure class.
		/// </summary>
		/// <param name="__hostName">The host name.</param>
		/// <param name="portNumber">The port.</param>
		/// <param name="parameterProvider">The current Transport Context parameter collection.</param>
		public ConnectionEstablishingClosure(string __hostName, int portNumber, IParameterProvider parameterProvider)
		{
			this.hostName = __hostName;
			this._portNumber = portNumber;
			this._parameterProvider = parameterProvider;
			this.Completed = new ManualResetEvent(false);
			this._continue = true;
			this._syncRoot = new object();
			this.socket = null;
			this._exception = null;
		}

		/// <summary>
		/// Starts connection establishing.
		/// </summary>
		public void StartOperation()
		{
			GenuineThreadPool.QueueUserWorkItem(new WaitCallback(this.StartConnecting), null, true);
		}

		/// <summary>
		/// Resolves the remote host IP address and establishes the connection.
		/// </summary>
		/// <param name="ignored">This parameter is ignored.</param>
		private void StartConnecting(object ignored)
		{
			try
			{
				lock (this._syncRoot)
				{
					if (! this._continue)
						return ;
				}

				// resolve host name to IP address
				IPAddress ipAddress = GenuineUtility.ResolveIPAddress(hostName);

				if (ipAddress == null)
					throw GenuineExceptions.Get_Connect_CanNotResolveHostName(hostName);

				// get the address of the remote host
				IPEndPoint ipEndPoint = new IPEndPoint(ipAddress, this._portNumber);
				if (ipEndPoint == null)
					throw GenuineExceptions.Get_Connect_CanNotConnectToRemoteHost(this.hostName, "The IPEndPoint instance cannot be created.");

				// create and setup a socket
				socket = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
				LingerOption lingerOption = new LingerOption(true, 3);
				socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, lingerOption);

				if ((bool) this._parameterProvider[GenuineParameter.TcpDisableNagling])
					socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoDelay, 1);

				int tcpReceiveBufferSize = (int) this._parameterProvider[GenuineParameter.TcpReceiveBufferSize];
				int tcpSendBufferSize = (int) this._parameterProvider[GenuineParameter.TcpSendBufferSize];
				if ( tcpReceiveBufferSize >= 0)
					socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, tcpReceiveBufferSize);
				if ( tcpSendBufferSize >= 0)
					socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, tcpSendBufferSize);

				lock (this._syncRoot)
				{
					if (! this._continue)
						return ;
				}

				// connect to the remote host
				socket.Connect(ipEndPoint);

				this.Completed.Set();
			}
			catch(Exception ex)
			{
				this.CancelConnecting(ex);
			}
		}

		/// <summary>
		/// Completes the connection establishing operation.
		/// </summary>
		/// <returns>The connected socket.</returns>
		public Socket CompleteOperation()
		{
			lock (this._syncRoot)
			{
				Socket connectedSocket = this.socket;
				this.socket = null;
				return connectedSocket;
			}
		}

		/// <summary>
		/// Cancels the connection establishing operation.
		/// </summary>
		public void CancelConnecting(Exception reason)
		{
			lock (this._syncRoot)
			{
				this._continue = false;
				this.Exception = reason;
				this.Completed.Set();
			}
		}

		/// <summary>
		/// Closes the socket.
		/// </summary>
		public void Dispose()
		{
			try
			{
				lock (this._syncRoot)
				{
					if (this.socket != null)
					{
						GenuineThreadPool.QueueUserWorkItem(new WaitCallback(SocketUtility.CloseSocket), this.socket, true);
						this.socket = null;
					}
				}
			}
			catch
			{
			}
		}
	}
}
