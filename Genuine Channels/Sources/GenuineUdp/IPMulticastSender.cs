/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

using Belikov.GenuineChannels.BroadcastEngine;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.GenuineUdp
{
	/// <summary>
	/// Implements a multicast sender, which sends broadcast messages via IP multicasting.
	/// </summary>
	public class IPMulticastSender : GeneralBroadcastSender
	{
		/// <summary>
		/// Initializes an instance of the IPMulticastSender class.
		/// </summary>
		/// <param name="court">The name of the destination court.</param>
		/// <param name="iTransportContext">The Transport Context.</param>
		public IPMulticastSender(string court, ITransportContext iTransportContext) : base(court, iTransportContext)
		{
		}

		/// <summary>
		/// Sends a broadcast message thru IP multicasting.
		/// </summary>
		/// <param name="message">The message being sent.</param>
		/// <param name="resultCollector">The result collector that gathers results of the invocation.</param>
		public override void SendMessage(Message message, ResultCollector resultCollector)
		{
			message.Recipient = this.ITransportContext.KnownHosts["_gbudp://" + this.Court];
			message.DestinationMarshalByRef = this.Court;
			this.ITransportContext.ConnectionManager.Send(message);
		}

	}
}
