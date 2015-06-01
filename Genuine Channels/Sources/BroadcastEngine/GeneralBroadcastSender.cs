/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved. 
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.BroadcastEngine
{
	/// <summary>
	/// Abstract class that supports all basic means to broadcast messages via "true" multicast channel.
	/// </summary>
	public abstract class GeneralBroadcastSender
	{
		/// <summary>
		/// Constructs an instance of the GeneralBroadcastSender class.
		/// </summary>
		/// <param name="court">The court.</param>
		/// <param name="iTransportContext">The Transport Ñontext.</param>
		public GeneralBroadcastSender(string court, ITransportContext iTransportContext)
		{
			this._court = court;
			this._iTransportContext = iTransportContext;
		}

		/// <summary>
		/// The broadcast court.
		/// </summary>
		public string Court
		{
			get
			{
				return _court;
			}
		}
		private string _court;

		/// <summary>
		/// The Transport context.
		/// </summary>
		public ITransportContext ITransportContext
		{
			get
			{
				return this._iTransportContext;
			}
		}
		private ITransportContext _iTransportContext;

		/// <summary>
		/// Sends the message via specific broadcast transport.
		/// </summary>
		/// <param name="message">The message being sent.</param>
		/// <param name="resultCollector">The Result Ñollector to gather results of the invocation.</param>
		public abstract void SendMessage(Message message, ResultCollector resultCollector);
	}
}
