/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.DotNetRemotingLayer
{
	/// <summary>
	/// Represents an event related to Genuine Channels solution.
	/// </summary>
	[Serializable]
	public class GenuineEventArgs : EventArgs
	{
		/// <summary>
		/// Initializes an instance of the GenuineEventArgs class.
		/// </summary>
		/// <param name="genuineEventType">Event type.</param>
		/// <param name="sourceException">Source exception.</param>
		/// <param name="hostInformation">Information about remote host.</param>
		/// <param name="additionalInfo">Additional information related to the event or a null reference.</param>
		public GenuineEventArgs(GenuineEventType genuineEventType, Exception sourceException, HostInformation hostInformation, object additionalInfo)
		{
			this.EventType = genuineEventType;
			this.SourceException = sourceException;
			this.HostInformation = hostInformation;
			this.AdditionalInfo = additionalInfo;
		}

		/// <summary>
		/// The type of the event.
		/// </summary>
		public readonly GenuineEventType EventType;

		/// <summary>
		/// The exception that causes this event or a null reference.
		/// </summary>
		public readonly Exception SourceException;

		/// <summary>
		/// The information about the remote host.
		/// </summary>
		public readonly HostInformation HostInformation;

		/// <summary>
		/// The optional information available for the event.
		/// </summary>
		public readonly object AdditionalInfo;
	}
}
