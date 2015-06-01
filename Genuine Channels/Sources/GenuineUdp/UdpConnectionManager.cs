/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Channels;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;

using Belikov.Common.ThreadProcessing;
using Belikov.GenuineChannels.BufferPooling;
using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Parameters;
using Belikov.GenuineChannels.Receiving;
using Belikov.GenuineChannels.Security;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Utilities;

namespace Belikov.GenuineChannels.GenuineUdp
{
	/// <summary>
	/// Implements a Connection Manager servicing UDP connections.
	/// </summary>
	internal class UdpConnectionManager : ConnectionManager, ITimerConsumer
	{
		/// <summary>
		/// Constructs an instance of the UdpConnectionManager class.
		/// </summary>
		/// <param name="iTransportContext">The transport context.</param>
		public UdpConnectionManager(ITransportContext iTransportContext) : base(iTransportContext)
		{
			this.Local = new HostInformation("_gudp://" + iTransportContext.HostIdentifier, iTransportContext);
			this._sendBuffer = new byte[(int) iTransportContext.IParameterProvider[GenuineParameter.UdpPacketSize]];
			TimerProvider.Attach(this);
			this._closeInvocationConnectionAfterInactivity = GenuineUtility.ConvertToMilliseconds(iTransportContext.IParameterProvider[GenuineParameter.CloseInvocationConnectionAfterInactivity]);
		}

		/// <summary>
		/// Sends the message to the remote host.
		/// </summary>
		/// <param name="message">The message to be sent.</param>
		protected override void InternalSend(Message message)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;

			// get IP end point of the remote host
			IPEndPoint remoteEndPoint;

			if (message.Recipient.Uri != null && message.Recipient.Uri.StartsWith("_gb"))
				remoteEndPoint = this._multicastTo;
			else
				remoteEndPoint = message.Recipient.PhysicalAddress as IPEndPoint;
			if (remoteEndPoint == null)
			{
				try
				{
					int port;
					string baseUrl = GenuineUtility.SplitToHostAndPort(message.Recipient.Url, out port);
					message.Recipient.PhysicalAddress = remoteEndPoint = new IPEndPoint(GenuineUtility.ResolveIPAddress(baseUrl), port);
				}
				catch(Exception)
				{
					throw GenuineExceptions.Get_Send_DestinationIsUnreachable(message.Recipient.ToString());
				}
			}

			Stream streamToSend = message.SerializedContent;

			// write the host URI
			if ((int) this.ITransportContext.IParameterProvider[GenuineParameter.CompatibilityLevel] > 0)
			{
				GenuineChunkedStream streamWith250Header = new GenuineChunkedStream(false);
				BinaryWriter binaryWriter = new BinaryWriter(streamWith250Header);
				streamWith250Header.Write(this.ITransportContext.BinaryHostIdentifier, 0, this.ITransportContext.BinaryHostIdentifier.Length);
				binaryWriter.Write((int) message.Recipient.LocalHostUniqueIdentifier);
				binaryWriter.Write((Int16) 0);

				streamWith250Header.WriteStream(streamToSend);

				streamToSend = streamWith250Header;
			}

			// LOG:
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
			{
				binaryLogWriter.WriteEvent(LogCategory.Connection, "UdpConnectionManager.InternalSend",
					LogMessageType.MessageIsSentSynchronously, null, message, message.Recipient, null, 
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
					message.ConnectionLevelSecuritySession, message.ConnectionLevelSecuritySession == null ? null : message.ConnectionLevelSecuritySession.Name, 
					-1, 0, 0, 0, remoteEndPoint.ToString(), null, null, null,
					"The message is being sent synchronously to {0}.", remoteEndPoint.ToString());
			}

			// send the message
			byte[] streamId = Guid.NewGuid().ToByteArray();

			lock (_socketLock)
			{
				for ( int chunkNumber = 1; ; chunkNumber++ )
				{
					// read the next chunk
					int chunkSize = streamToSend.Read(this._sendBuffer, HEADER_SIZE, this._sendBuffer.Length - HEADER_SIZE);

					// fill in the header
					this._sendBuffer[0] = MessageCoder.COMMAND_MAGIC_CODE;
					Buffer.BlockCopy(streamId, 0, this._sendBuffer, 1, 16);
					if (chunkSize < this._sendBuffer.Length - HEADER_SIZE)
						chunkNumber = - chunkNumber;
					MessageCoder.WriteInt32(this._sendBuffer, 17, chunkNumber);

					// and just send it!

					// LOG:
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Transport] > 0 )
					{
						binaryLogWriter.WriteTransportContentEvent(LogCategory.Transport, "UdpConnectionManager.InternalSend",
							LogMessageType.SynchronousSendingStarted, null, message, message.Recipient, 
							binaryLogWriter[LogCategory.Transport] > 1 ? new MemoryStream(GenuineUtility.CutOutBuffer(this._sendBuffer, 0, chunkSize + HEADER_SIZE)) : null, 
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
							this.DbgConnectionId, chunkSize + HEADER_SIZE, remoteEndPoint.ToString(),
							null, null,
							"Content is sent synchronously to {0}.", remoteEndPoint.ToString());
					}

					this._socket.SendTo(this._sendBuffer, 0, chunkSize + HEADER_SIZE, SocketFlags.None, remoteEndPoint);

					if (chunkNumber < 0)
						break;
				}
			}
		}

		#region -- Low-level socket stuff ----------------------------------------------------------

		/// <summary>
		/// The message registrator.
		/// </summary>
		public IMessageRegistrator _iMessageRegistrator = new MessageRegistratorWithLimitedTime();

		/// <summary>
		/// Specifies a value by which the remote host's information is extended after every received message.
		/// </summary>
		private int _closeInvocationConnectionAfterInactivity;

		private Socket _socket;
		private object _socketLock = new object();
		private IPEndPoint _multicastTo;
		private byte[] _sendBuffer;
		private bool _closing;
		private ManualResetEvent _receivingThreadClosed = new ManualResetEvent(true);
		private Hashtable _streams = Hashtable.Synchronized(new Hashtable());

		/// <summary>
		/// Gets the connection identifier.
		/// </summary>
		public int DbgConnectionId
		{
			get
			{
				return this._dbgConnectionId;
			}
		}
		private int _dbgConnectionId = ConnectionManager.GetUniqueConnectionId();


		/// <summary>
		/// (1)		Magic code
		/// (16)	The unique stream id
		/// (4)		The sequence number of the current chunk (negative if it's the last chunk).
		/// </summary>
		private const int HEADER_SIZE = 21;

		/// <summary>
		/// Receives the content synchronously.
		/// </summary>
		private void ReceiveSynchronously()
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			byte[] receiveBuffer = null;
			this._receivingThreadClosed.Reset();

			try
			{
				int mtu = (int) this.ITransportContext.IParameterProvider[GenuineParameter.UdpMtu];
				for ( ; ; )
				{
					if (this._closing)
						return ;

					if (BufferPool.GENERAL_BUFFER_SIZE >= mtu)
						receiveBuffer = BufferPool.ObtainBuffer();
					else
						receiveBuffer = new byte[mtu];

					try
					{
						IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, 0);
						EndPoint endPointReference = ipEndPoint;
						int bytesReceived = this._socket.ReceiveFrom(receiveBuffer, 0, receiveBuffer.Length, SocketFlags.None, ref endPointReference);
						ipEndPoint = (IPEndPoint) endPointReference;

						// LOG:
						if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Transport] > 0 )
						{
							binaryLogWriter.WriteTransportContentEvent(LogCategory.Transport, "UdpConnectionManager.ReceiveSynchronously",
								LogMessageType.ReceivingFinished, null, null, null,
								binaryLogWriter[LogCategory.Transport] > 1 ? new MemoryStream(GenuineUtility.CutOutBuffer(receiveBuffer, 0, bytesReceived)) : null, 
								GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
								this.DbgConnectionId, bytesReceived, ipEndPoint.ToString(),
								null, null,
								"Content is received from {0}.", ipEndPoint.ToString());
						}

						// parse the header
						if (bytesReceived < HEADER_SIZE)
							throw GenuineExceptions.Get_Receive_IncorrectData();
						if (receiveBuffer[0] != MessageCoder.COMMAND_MAGIC_CODE)
							throw GenuineExceptions.Get_Receive_IncorrectData();

						// get the packet identifier
						byte[] guidBuffer = new byte[16];
						Buffer.BlockCopy(receiveBuffer, 1, guidBuffer, 0, 16);
						Guid packetGuid = new Guid(guidBuffer);

						// and chunk number
						int chunkNumber = MessageCoder.ReadInt32(receiveBuffer, 17);
						bool isLast = chunkNumber < 0;
						if (chunkNumber < 0)
							chunkNumber = - chunkNumber;
						chunkNumber --;

						// process the chunk
						StreamAssembled streamAssembled;
						lock (this._streams.SyncRoot)
						{
							streamAssembled = this._streams[packetGuid] as StreamAssembled;
							if (streamAssembled == null)
								this._streams[packetGuid] = streamAssembled = new StreamAssembled(ipEndPoint, HEADER_SIZE);
							if (streamAssembled.IsProcessed)
								continue;
						}

						string uri = "gudp://" + ipEndPoint.ToString();
						HostInformation remote = this.ITransportContext.KnownHosts[uri];
						remote.Renew(this._closeInvocationConnectionAfterInactivity, false);

						if ((int) this.ITransportContext.IParameterProvider[GenuineParameter.CompatibilityLevel] <= 0)
							remote.UpdateUri(uri, 0, false);

						if (streamAssembled.BufferReceived(chunkNumber, receiveBuffer, bytesReceived, isLast))
						{
							// prepare it for processing
							this._streams.Remove(packetGuid);
							streamAssembled.IsProcessed = true;

							// read the remote host URI
							if ((int) this.ITransportContext.IParameterProvider[GenuineParameter.CompatibilityLevel] > 0)
							{
								BinaryReader binaryReader = new BinaryReader(streamAssembled);

								// read the URI
								byte[] uriBuffer = new byte[16];
								GenuineUtility.ReadDataFromStream(streamAssembled, uriBuffer, 0, uriBuffer.Length);
								Guid remoteHostUriGuid = new Guid(uriBuffer);
								string receivedUri = "_gudp://" + remoteHostUriGuid.ToString("N");

								// read the remote host unique identifier
								int remoteHostUniqueIdentifier = binaryReader.ReadInt32();

								// update the host information
								remote.UpdateUri(receivedUri, remoteHostUniqueIdentifier, false);

								// and skip the skip space
								GenuineUtility.CopyStreamToStream(streamAssembled, Stream.Null, binaryReader.ReadInt16());
							}

							// LOG:
							if ( remote.PhysicalAddress == null && binaryLogWriter != null && binaryLogWriter[LogCategory.HostInformation] > 0 )
							{
								binaryLogWriter.WriteHostInformationEvent("UdpConnectionManager.ReceiveSynchronously",
									LogMessageType.HostInformationCreated, null, remote,
									GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
									null, null, 
									this.DbgConnectionId,
									"HostInformation is ready for actions.");
							}

							remote.PhysicalAddress = endPointReference;
							this.ITransportContext.IIncomingStreamHandler.HandleMessage(streamAssembled, remote, GenuineConnectionType.Persistent, string.Empty, -1, false, this._iMessageRegistrator, null, null);
						}
					}
					catch(Exception ex)
					{
						// LOG:
						if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
						{
							binaryLogWriter.WriteEvent(LogCategory.Connection, "UdpConnectionManager.ReceiveSynchronously",
								LogMessageType.ReceivingFinished, ex, null, null, null, 
								GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
								null, null, 
								this.DbgConnectionId, 0, 0, 0, null, null, null, null,
								"UDP socket failure.");
						}

						if (this._closing)
							return ;

						this.ITransportContext.IGenuineEventProvider.Fire(new GenuineEventArgs(GenuineEventType.GUdpSocketException, ex, this.Local, null));
					}
				}
			}	
			finally
			{
				this._receivingThreadClosed.Set();
			}
		}

		#endregion

		#region -- Socket initialization -----------------------------------------------------------

		/// <summary>
		/// Starts listening to the specified end point and accepting incoming connections.
		/// </summary>
		/// <param name="endPoint">The end point.</param>
		public override void StartListening(object endPoint)
		{
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			this._closing = false;

			// LOG:
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
			{
				binaryLogWriter.WriteConnectionParameterEvent(LogCategory.Connection, "UdpConnectionManager.StartListening",
					LogMessageType.ConnectionParameters, null, null, this.ITransportContext.IParameterProvider,
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, this.DbgConnectionId, 
					"UDP socket is being associated with the end point: {0}.", endPoint.ToString());
			}

			// get the ip end point
			IPEndPoint ipEndPoint = null;
			int port;
			string url;
			try
			{
				url = (string) endPoint;
				url = GenuineUtility.SplitToHostAndPort(url, out port);
				ipEndPoint = new IPEndPoint(GenuineUtility.ResolveIPAddress(url), port);
			}
			catch(Exception ex)
			{
				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "UdpConnectionManager.StartListening",
						LogMessageType.ListeningStarted, ex, null, null, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, this.DbgConnectionId, 0, 0, 0, null, null, null, null,
						"The listening socket cannot be associated with the {0} local end point.", endPoint.ToString());
				}

				throw GenuineExceptions.Get_Server_IncorrectAddressToListen(endPoint as string);
			}

			lock (this)
			{
				if (this._socket != null)
					throw GenuineExceptions.Get_Server_EndPointIsAlreadyBeingListenedTo(this._socket.LocalEndPoint.ToString());

				// initialize the socket
				this._socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
				if (this.ITransportContext.IParameterProvider[GenuineParameter.UdpTtl] != null)
					this._socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, (int) this.ITransportContext.IParameterProvider[GenuineParameter.UdpTtl]);

				// Receive buffer size
				this._socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, (int) this.ITransportContext.IParameterProvider[GenuineParameter.UdpReceiveBuffer]);
				this._socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);

				this._socket.Bind(ipEndPoint);

				// if it's an IP multicast sender
				if (this.ITransportContext.IParameterProvider[GenuineParameter.UdpMulticastTo] != null)
				{
					try
					{
						url = (string) this.ITransportContext.IParameterProvider[GenuineParameter.UdpMulticastTo];
						url = GenuineUtility.SplitToHostAndPort(url, out port);
						this._multicastTo = new IPEndPoint(GenuineUtility.ResolveIPAddress(url), port);

						this._socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
						if (this.ITransportContext.IParameterProvider[GenuineParameter.UdpTtl] != null)
							this._socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, (int) this.ITransportContext.IParameterProvider[GenuineParameter.UdpTtl]);
					}
					catch(Exception)
					{
						throw GenuineExceptions.Get_Channel_InvalidParameter("UdpMulticastTo");
					}
				}

				// and join to the specified broadcast network
				if (this.ITransportContext.IParameterProvider[GenuineParameter.UdpJoinTo] != null)
				{
					string joinTo = (string) this.ITransportContext.IParameterProvider[GenuineParameter.UdpJoinTo];
					this._socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);

					IPAddress ipAddressToJoinTo = GenuineUtility.ResolveIPAddress(joinTo);
					MulticastOption multicastOption = new MulticastOption(ipAddressToJoinTo, ipEndPoint.Address);
					this._socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, multicastOption);
				}

				// initiate receiving
				Thread receivingThread = new Thread(new ThreadStart(this.ReceiveSynchronously));
				receivingThread.IsBackground = true;
				receivingThread.Start();

				// LOG:
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "UdpConnectionManager.StartListening",
						LogMessageType.ConnectionEstablished, null, null, null, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
						null, null,
						this.DbgConnectionId, (int) GenuineConnectionType.Invocation, 0, 0, this.GetType().Name, this._socket.LocalEndPoint.ToString(), null, null,
						"The UDP socket is ready for action.");
				}
			}
		}

		/// <summary>
		/// Stops listening to the specified end point. Does not close any connections.
		/// </summary>
		/// <param name="endPoint">The end point</param>
		public override void StopListening(object endPoint)
		{
			lock (this)
			{
				if (this._socket == null)
					return ;

				// LOG:
				BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Connection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Connection, "UdpConnectionManager.StopListening",
						LogMessageType.ListeningStopped, null, null, null, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, this.DbgConnectionId, 0, 0, 0, null, null, null, null,
						"The listening socket is not longer associated with the {0} local end point.", endPoint == null ? string.Empty : endPoint.ToString());
				}

				SocketUtility.CloseSocket(this._socket);
				this._socket = null;

				this._closing = true;
			}

			if (! this._receivingThreadClosed.WaitOne(TimeSpan.FromMinutes(2), false))
				throw GenuineExceptions.Get_Processing_LogicError("Receiving thread didn't exit within 2 minutes.");
		}

		/// <summary>
		/// Closes the specified connections to the remote host and releases acquired resources.
		/// </summary>
		/// <param name="hostInformation">Host information.</param>
		/// <param name="genuineConnectionType">What kind of connections will be affected by this operation.</param>
		/// <param name="reason">Reason of resource releasing.</param>
		public override void ReleaseConnections(HostInformation hostInformation, GenuineConnectionType genuineConnectionType, Exception reason)
		{
		}

		#endregion

		#region -- Closing and disposing -----------------------------------------------------------

		/// <summary>
		/// Releases all resources.
		/// </summary>
		/// <param name="reason">The reason of disposing.</param>
		public override void InternalDispose(Exception reason)
		{
			this.StopListening(null);
		}

		#endregion

		#region -- ITimerConsumer Members ----------------------------------------------------------

		/// <summary>
		/// Releases expired streams.
		/// </summary>
		public void TimerCallback()
		{
			int now = GenuineUtility.TickCount;
			int releaseAfter = GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.UdpAssembleTimeSpan]);

			lock (this._streams.SyncRoot)
			{
				// gather expired stream
				ArrayList itemsDeleted = new ArrayList();
				foreach (DictionaryEntry entry in this._streams)
				{
					StreamAssembled streamAssembled = (StreamAssembled) entry.Value;
					if (GenuineUtility.IsTimeoutExpired(streamAssembled.Started + releaseAfter, now) 
						&& ! streamAssembled.IsProcessed)
					{
						itemsDeleted.Add(entry.Key);
						streamAssembled.Close();
					}
				}

				// and remove them
				foreach (object key in itemsDeleted)
					this._streams.Remove(key);
			}
		}

		#endregion
	}
}
