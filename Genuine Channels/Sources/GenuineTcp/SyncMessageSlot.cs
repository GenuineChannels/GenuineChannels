/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.IO;
using System.Threading;

using Belikov.GenuineChannels.Messaging;

namespace Belikov.GenuineChannels.GenuineTcp
{
	/// <summary>
	/// Represents a message that allows to acquire socket's resources in another thread.
	/// </summary>
	public class SyncMessageSlot : Message
	{
		/// <summary>
		/// Constructs an instance of the SyncMessageSlot class.
		/// </summary>
		public SyncMessageSlot()
		{
			this.Stream = Stream.Null;
			this.SerializedContent = Stream.Null;
			this.ConnectionAvailable = new ManualResetEvent(false);
		}

		/// <summary>
		/// Is not released automatically.
		/// </summary>
		internal TcpSocketInfo TcpSocketInfo;
	}
}
