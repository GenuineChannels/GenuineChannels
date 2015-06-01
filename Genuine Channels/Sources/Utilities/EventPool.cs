/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.Threading;

using Belikov.Common.ThreadProcessing;

namespace Belikov.GenuineChannels.Utilities
{
	/// <summary>
	/// A storage containing instances of the ManualResetEvent class.
	/// </summary>
	public class EventPool
	{
		/// <summary>
		/// Gets an instance of the ManualResetEvent class, which initial state is false.
		/// </summary>
		/// <returns>The obtained buffer.</returns>
		public static ManualResetEvent ObtainEvent()
		{
			lock(_lock)
			{
				if (_stack.Count > 0)
					return (ManualResetEvent) _stack.Pop();

				return new ManualResetEvent(false);
			}
		}

		/// <summary>
		/// Returns the buffer back to Buffer Pool for the following reusing.
		/// </summary>
		/// <param name="manualResetEvent">The event.</param>
		public static void RecycleEvent(ManualResetEvent manualResetEvent)
		{
			lock(_lock)
			{
				if (_stack.Count < _maxPooledObjects)
				{
					manualResetEvent.Reset();
					_stack.Push(manualResetEvent);
				}
			}
		}

		/// <summary>
		/// Gets an instance of the ManualResetEvent class that can be used as a stub.
		/// </summary>
		public static ManualResetEvent GlobalStubEvent
		{
			get
			{
				return _globalStubEvent;
			}
		}
		private static ManualResetEvent _globalStubEvent = new ManualResetEvent(false);

		/// <summary>
		/// The maximum number of objects kept in the pool.
		/// </summary>
		private const int _maxPooledObjects = 100;

		private static object _lock = new object();
		private static Stack _stack = new Stack(100);
	}
}
