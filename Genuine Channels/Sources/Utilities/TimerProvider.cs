/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.Threading;

namespace Belikov.GenuineChannels.Utilities
{
	/// <summary>
	/// Contains a list of clients and provides a mechanism for executing client's method at specified intervals.
	/// </summary>
	public class TimerProvider
	{
		/// <summary>
		/// Runs system timer.
		/// </summary>
		static TimerProvider()
		{
			_timer = new Timer(new TimerCallback(TimerProvider.TimerCallback), null, TimeSpan.FromSeconds(CallFrequencyInSeconds), TimeSpan.FromSeconds(CallFrequencyInSeconds));
		}
		private static Timer _timer;

		/// <summary>
		/// Timer interval.
		/// </summary>
		public const int CallFrequencyInSeconds = 10;

		/// <summary>
		/// Is executed every 10 seconds.
		/// </summary>
		/// <param name="state">Useless parameter.</param>
		public static void TimerCallback(object state)
		{
			lock(_clients)
			{
				for ( int i = 0; i < _clients.Count; )
				{
					ITimerConsumer iTimerConsumer = null;

					WeakReference weakReference = _clients[i] as WeakReference;
					if (weakReference != null)
						iTimerConsumer = weakReference.Target as ITimerConsumer;
					if (iTimerConsumer == null)
					{
						_clients.RemoveAt(i);
						continue;
					}

					// safe call
					try
					{
						iTimerConsumer.TimerCallback();
					}
					catch(Exception)
					{
					}

					i++;
				}
			}
		}

		/// <summary>
		/// List of clients.
		/// </summary>
		private static ArrayList _clients = new ArrayList(5);

		/// <summary>
		/// Attaches timer consumer.
		/// </summary>
		/// <param name="iTimerConsumer">Timer consumer to attach.</param>
		public static void Attach(ITimerConsumer iTimerConsumer)
		{
			lock(_clients)
			{
				_clients.Add(new WeakReference(iTimerConsumer));
			}
		}

		/// <summary>
		/// Detaches timer consumer.
		/// </summary>
		/// <param name="iTimerConsumer">Timer consumer to attach.</param>
		public static void Detach(ITimerConsumer iTimerConsumer)
		{
			lock(_clients)
			{
				_clients.Remove(new WeakReference(iTimerConsumer));
			}
		}

	}
}
