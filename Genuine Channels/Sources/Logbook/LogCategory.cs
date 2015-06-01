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
	/// The category of the log record.
	/// </summary>
	public enum LogCategory
	{
		/// <summary>
		/// Information about connections.
		/// </summary>
		Connection = 0,

		/// <summary>
		/// Information about raised channel events.
		/// </summary>
		ChannelEvent = 1,

		/// <summary>
		/// Message processing.
		/// </summary>
		MessageProcessing = 2,

		/// <summary>
		/// Implementation warning.
		/// </summary>
		ImplementationWarning = 3,

		/// <summary>
		/// Security events.
		/// </summary>
		Security = 4,

		/// <summary>
		/// A Broadcast Engine messages.
		/// </summary>
		BroadcastEngine = 5,

		/// <summary>
		/// Information which is sent by a specific transport.
		/// </summary>
		Transport = 6,

		/// <summary>
		/// A DXM Usage message.
		/// </summary>
		DXM = 7,

		/// <summary>
		/// HostInformation lifetime stages.
		/// </summary>
		HostInformation = 8,

		/// <summary>
		/// Connection accepting.
		/// </summary>
		AcceptingConnection = 9,

		/// <summary>
		/// Debugging messages.
		/// </summary>
		Debugging = 10,

		/// <summary>
		/// The version of Genuine Channels and environment.
		/// </summary>
		Version = 11,

		/// <summary>
		/// The low-level transport notification.
		/// </summary>
		LowLevelTransport = 12,

		/// <summary>
		/// Represents records exposing buffer pool/thread pool usage information.
		/// </summary>
		StatisticCounters = 13,

		/// <summary>
		/// Indicates the total number of log categories.
		/// </summary>
		TotalCategories = 14,
	}
}
