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
	/// Enumerates all possible record versions.
	/// </summary>
	public enum BinaryRecordVersion
	{
		/// <summary>
		/// General record.
		/// </summary>
		GeneralRecord = 0x100,

		/// <summary>
		/// Indicates that the record contains only implementation warning fields.
		/// </summary>
		ImplementationWarning = 0x101,

		/// <summary>
		/// The record containing detailed message information.
		/// </summary>
		MessageRecord = 0x102,

		/// <summary>
		/// The record containing Security Session Parameters.
		/// </summary>
		SecuritySessionParameters = 0x103,

		/// <summary>
		/// The record containing Security Session Parameters.
		/// </summary>
		HostInformationInfo = 0x104,

		/// <summary>
		/// The record containing Connection parameters.
		/// </summary>
		ConnectionParametersRecord = 0x105,

		/// <summary>
		/// The record containing content sent or received.
		/// </summary>
		TransportContentRecord = 0x106,

		/// <summary>
		/// The record containing information about Broadcast Engine actions.
		/// </summary>
		TransportBroadcastEngineRecord = 0x107,

		/// <summary>
		/// Information about the current versions of Genuine Channels, .NET Framework, Operation System and so on.
		/// </summary>
		VersionRecord = 0x108,
	}
}
