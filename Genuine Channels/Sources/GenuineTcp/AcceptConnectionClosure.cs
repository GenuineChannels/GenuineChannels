/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using Belikov.Common.ThreadProcessing;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Parameters;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Utilities;

namespace Belikov.GenuineChannels.GenuineTcp
{
	/// <summary>
	/// Services a listening socket.
	/// </summary>
	internal class AcceptConnectionClosure
	{
		/// <summary>
		/// Constructs an instance of the AcceptConnectionClosure class.
		/// </summary>
		/// <param name="iTransportContext">The transport context.</param>
		/// <param name="listeningEndPoint">The end point.</param>
		/// <param name="socket">The socket.</param>
		/// <param name="iAcceptConnectionConsumer"></param>
		public AcceptConnectionClosure(ITransportContext iTransportContext, string listeningEndPoint, Socket socket, IAcceptConnectionConsumer iAcceptConnectionConsumer)
		{
			this.ITransportContext = iTransportContext;
			this.ListeningEndPoint = listeningEndPoint;
			this.Socket = socket;
			this.IAcceptConnectionConsumer = iAcceptConnectionConsumer;
		}

		/// <summary>
		/// The transport context.
		/// </summary>
		public ITransportContext ITransportContext;

		/// <summary>
		/// The socket serviced.
		/// </summary>
		public Socket Socket;

		/// <summary>
		/// The master manager.
		/// </summary>
		public IAcceptConnectionConsumer IAcceptConnectionConsumer;

		/// <summary>
		/// End point.
		/// </summary>
		public string ListeningEndPoint;

		/// <summary>
		/// Indicates whether the listening should be stopped.
		/// </summary>
		public ManualResetEvent StopListening = new ManualResetEvent(false);

		/// <summary>
		/// Accepts incoming connections.
		/// </summary>
		public void AcceptConnections()
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			Exception exception = null;
			int numberOfFailure = 0;

			try
			{
				for ( ; ; )
				{
					try
					{
						if (this.IAcceptConnectionConsumer.IsDisposed())
							return ;
						if (this.StopListening.WaitOne(0, false))
							return ;

						Socket clientSocket = this.Socket.Accept();
						GenuineThreadPool.QueueUserWorkItem(new WaitCallback(this.AcceptConnection), clientSocket, true);
					}
					catch(Exception ex)
					{
						numberOfFailure ++;

						// LOG:
						if ( binaryLogWriter != null && binaryLogWriter[LogCategory.AcceptingConnection] > 0 )
						{
							binaryLogWriter.WriteEvent(LogCategory.AcceptingConnection, "GenuineTcp.AcceptConnectionClosure.AcceptConnections",
								LogMessageType.ConnectionAccepting, ex, null, null, null, 
								GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
								null, null, -1, 0, 0, 0, null, null, null, null,
								"An inbound TCP connection has not been accepted. Number of failure: {0}.", numberOfFailure);
						}

						this.ITransportContext.IGenuineEventProvider.Fire(new GenuineEventArgs(GenuineEventType.GeneralListenerFailure, ex, null, ListeningEndPoint));
					}
				}
			}
			catch(Exception ex)
			{
				exception = ex;

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.AcceptingConnection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.AcceptingConnection, "GenuineTcp.AcceptConnectionClosure.AcceptConnections",
						LogMessageType.ListeningStopped, ex, null, null, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, -1, 0, 0, 0, null, null, null, null,
						"Fatal Error. The socket does not accept inbound connections any longer.");
				}

				this.ITransportContext.IGenuineEventProvider.Fire(new GenuineEventArgs(GenuineEventType.GeneralListenerFailure, ex, null, this.ListeningEndPoint));
			}
			finally
			{
				SocketUtility.CloseSocket(this.Socket);
			}

		}

		/// <summary>
		/// Accepts incoming connection.
		/// </summary>
		/// <param name="socketAsObject">The socket.</param>
		public void AcceptConnection(object socketAsObject)
		{
			Socket socket = (Socket) socketAsObject;
			this.IAcceptConnectionConsumer.AcceptConnection(socket);
		}
	}
}
