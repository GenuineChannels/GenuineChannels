/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

namespace Belikov.GenuineChannels.Logbook
{
	/// <summary>
	/// The IEventLogger interface supports writing messages into event log.
	/// </summary>
	public interface IEventLogger
	{
		/// <summary>
		/// Gets the log writer that can be used for writing debug messages.
		/// </summary>
		BinaryLogWriter BinaryLogWriter { get; }
	}
}
