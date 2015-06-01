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
	/// Represents a "true" broadcast sender that can broadcast messages to
	/// the specified court.
	/// </summary>
	public interface IBroadcastSenderProvider
	{
		/// <summary>
		/// Answers broadcast sender which sends messages to the specified court.
		/// </summary>
		GeneralBroadcastSender GetBroadcastSender(string court);
	}
}
