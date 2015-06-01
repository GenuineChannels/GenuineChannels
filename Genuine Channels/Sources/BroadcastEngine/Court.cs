/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved. 
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

namespace Belikov.GenuineChannels.BroadcastEngine
{
	/// <summary>
	/// Represents the court containing information about destination receiver.
	/// </summary>
	public class Court
	{
		/// <summary>
		/// The receiver.
		/// </summary>
		public MarshalByRefObject Receiver;

		/// <summary>
		/// The MBR object obtained via a channel through which the response will be sent.
		/// </summary>
		public MarshalByRefObject Sender;

		/// <summary>
		/// Indicates whether this object has received something via broadcast channel.
		/// </summary>
		public bool HasEverReceived = false;
	}
}
