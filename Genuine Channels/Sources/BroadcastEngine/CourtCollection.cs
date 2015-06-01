/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved. 
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;

namespace Belikov.GenuineChannels.BroadcastEngine
{
	/// <summary>
	/// Singleton class that provides means to associate receviers with courts.
	/// </summary>
	public class CourtCollection
	{
		/// <summary>
		/// Singleton.
		/// </summary>
		private CourtCollection()
		{
		}

		/// <summary>
		/// Associates the receiver with the specified court.
		/// The response will be directed to the channel through which the sender object has been obtained.
		/// </summary>
		/// <param name="courtName">The name of the court.</param>
		/// <param name="receiver">The receiver implementing the corresponding interface.</param>
		/// <param name="sender">An arbitrary MBR object obtained via a channel through which the response will be sent.
		/// Can be a null reference if this court has been already occupied or if you do not need guaranteed delivery.</param>
		public static void Attach(string courtName, MarshalByRefObject receiver, MarshalByRefObject sender)
		{
			if (courtName == null || courtName.Length <= 0)
				throw new ArgumentException("CourtName can not be null or contains an empty string.", "courtName", null);
			if (receiver == null)
				throw new ArgumentNullException("receiver");

			lock(_courts)
			{
				Court court = null;
				if (! _courts.ContainsKey(courtName))
				{
					court = new Court();
					_courts[courtName] = court;
				}
				else
					court = (Court) _courts[courtName];

				court.Receiver = receiver;
				if (sender != null)
					court.Sender = sender;
			}
		}

		/// <summary>
		/// Disassociate the receiver with the specified court.
		/// Does not throw any exceptions if the court has not been occupied.
		/// </summary>
		/// <param name="courtName">The name of the court.</param>
		public static void Detach(string courtName)
		{
			if (courtName == null || courtName.Length <= 0)
				throw new ArgumentException("CourtName must not be empty.", "courtName", null);

			lock(_courts)
			{
				_courts.Remove(courtName);
			}
		}

		/// <summary>
		/// Court name(string) => Court (class).
		/// </summary>
		private static Hashtable _courts = new Hashtable();

		/// <summary>
		/// Looks for the court associated with the specified string and returns its parameters.
		/// Automatically sets Court.HasEverReceived member to true.
		/// </summary>
		/// <param name="courtName">The name of the court.</param>
		/// <param name="receiver">The receiver.</param>
		/// <param name="sender">The MBR object obtained via a channel through which the response will be sent.
		/// Can be null if this court has been already occupied.</param>
		internal static void Find(string courtName, out MarshalByRefObject receiver, out MarshalByRefObject sender)
		{
			receiver = null;
			sender = null;

			lock(_courts)
			{
				Court court = _courts[courtName] as Court;
				if (court == null)
					return ;

				court.HasEverReceived = true;
				receiver = court.Receiver;
				sender = court.Sender;
			}
		}

	}
}
