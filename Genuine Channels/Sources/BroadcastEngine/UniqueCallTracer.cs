/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;

using Belikov.GenuineChannels.Utilities;

namespace Belikov.GenuineChannels.BroadcastEngine
{
	/// <summary>
	/// Registers broadcast calls in order to prevent calling the same
	/// target twice if an invocation arrives via different channels or channel may double the message.
	/// </summary>
	public class UniqueCallTracer : ITimerConsumer
	{
		/// <summary>
		/// Constructs an instance of the UniqueCallTracer class.
		/// </summary>
		private UniqueCallTracer()
		{
			TimerProvider.Attach(this);
		}

		/// <summary>
		/// Singleton.
		/// </summary>
		public static UniqueCallTracer Instance = new UniqueCallTracer();

		/// <summary>
		/// The period of time to keep guids. 180 seconds by default.
		/// </summary>
		public static TimeSpan TimeSpanToRememberCall
		{
			get
			{
				lock (_timeSpanToRememberCallLock)
					return _timeSpanToRememberCall;
			}
			set
			{
				lock (_timeSpanToRememberCallLock)
				{
					_timeSpanToRememberCall = value;
					_timeSpanToRememberCallInMilliseconds = GenuineUtility.ConvertToMilliseconds(value);
				}
			}
		}
		private static TimeSpan _timeSpanToRememberCall = TimeSpan.FromSeconds(180);
		private static int _timeSpanToRememberCallInMilliseconds = 180000;
		private static object _timeSpanToRememberCallLock = new object();

		/// <summary>
		/// Checks on whether the specified GUID has been registered before.
		/// Automatically registers GUID if it wasn't registered.
		/// </summary>
		/// <param name="guid">The GUID to check.</param>
		/// <returns>True if the specified GUID has been registered before.</returns>
		public bool WasGuidRegistered(string guid)
		{
			lock(this)
			{
				if (_guids.Contains(guid))
					return true;

				_guids[guid] = GenuineUtility.GetTimeout(_timeSpanToRememberCallInMilliseconds);
				return false;
			}
		}

		/// <summary>
		/// Contains all registered guids as keys and DateTime when they were registered as values.
		/// </summary>
		private Hashtable _guids = new Hashtable();

		/// <summary>
		/// Deletes all expired guids.
		/// </summary>
		public void TimerCallback()
		{
			lock(this)
			{
				int now = GenuineUtility.TickCount;
				ArrayList expiredRecords = new ArrayList();

				// collect expired records
				foreach (DictionaryEntry entry in this._guids)
					if ( GenuineUtility.IsTimeoutExpired( (int) entry.Value, now) )
						expiredRecords.Add(entry.Key);

				// and delete them
				foreach (object key in expiredRecords)
					this._guids.Remove(key);
			}
		}
	}
}
