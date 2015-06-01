/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved. 
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Runtime.Remoting.Channels;
using System.Threading;

using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.BroadcastEngine
{
	/// <summary>
	/// Contains info about a remote recipient.
	/// </summary>
	public class ReceiverInfo
	{
		/// <summary>
		/// Uri of the receiver's MBR object.
		/// </summary>
		public string MbrUri;

		/// <summary>
		/// Is true if object is within local appdomain.
		/// </summary>
		public bool Local = false;

		/// <summary>
		/// Locally created MBR.
		/// </summary>
		public bool LocallyCreated = false;

		/// <summary>
		/// Whether this receiver available via broadcast means (IP multicasting).
		/// </summary>
		public bool NeedsBroadcastSimulation = true;

		/// <summary>
		/// Destination object.
		/// </summary>
		public MarshalByRefObject MbrObject;

		/// <summary>
		/// Broadcast sender or null if MbrObject is specified.
		/// </summary>
		public GeneralBroadcastSender GeneralBroadcastSender;

		/// <summary>
		/// MarshalByRefObject destination object.
		/// </summary>
		public object SerializedObjRef;

		/// <summary>
		/// Client channel sink thru that broadcast message will be sent when object
		/// is unreachable via true multicast channel.
		/// </summary>
		public IClientChannelSink IClientChannelSink;

		/// <summary>
		/// Number of consecutive failes.
		/// If object send successful result, this number is resetted to zero.
		/// Is used to detach a receiver automatically if the receiver stops replying.
		/// to sent messages.
		/// </summary>
		public int NumberOfFails = 0;

		/// <summary>
		/// How much times does true multicast channel cause consecutively fails.
		/// If object send successful result, this number is resetted to zero.
		/// Is used to switch sending to simulation mode if the receiver stops receiving
		/// messages via true multicast channel.
		/// </summary>
		public int NumberOfMulticastFails = 0;

		/// <summary>
		/// Thread-safe tag property.
		/// I would recommend to keep Client Session here.
		/// </summary>
		public object Tag
		{
			get
			{
				lock (this._tagLock)
					return this._tag;
			}
			set
			{
				lock (this._tagLock)
					this._tag = value;
			}
		}
		private object _tag;
		private object _tagLock = new object();

		/// <summary>
		/// The recipient identifier.
		/// </summary>
		public int DbgRecipientId
		{
			get
			{
				return this._dbgRecipientId;
			}
		}
		private int _dbgRecipientId = Interlocked.Increment(ref _dbgTotalRecipients);
		private static int _dbgTotalRecipients = 0;

		/// <summary>
		/// Gets or sets the remote host where this broadcast recipient is located. Is used for debugging purposes only.
		/// </summary>
		public HostInformation DbgRemoteHost
		{
			get
			{
				return this._dbgRemoteHost;
			}
			set
			{
				this._dbgRemoteHost = value;
			}
		}
		private HostInformation _dbgRemoteHost;

	}
}
