/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.Diagnostics;
using System.Text;
using System.Threading;

using Belikov.GenuineChannels.Logbook;

namespace Belikov.GenuineChannels.Utilities
{
	/// <summary>
	/// Implements a custom thread pool logic.
	/// All methods and properties are thread-safe.
	/// </summary>
	public class GenuineThreadPool
	{
		/// <summary>
		/// To prevent creating instances of the GenuineThreadPool class.
		/// </summary>
		private GenuineThreadPool()
		{
		}

		/// <summary>
		/// Gets or sets the minimum number of threads in the ThreadPool since that
		/// GenuineThreadPool will create threads.
		/// </summary>
		public static int ThreadPoolAvailableThreads
		{
			get
			{
				lock(_threadPoolAvailableThreadsLock)
					return _threadPoolAvailableThreads;
			}
			set
			{
				lock(_threadPoolAvailableThreadsLock)
					_threadPoolAvailableThreads = value;
			}
		}
		private static int _threadPoolAvailableThreads = 12;
		private static object _threadPoolAvailableThreadsLock = new object();

		/// <summary>
		/// The maximum number of allowed threads held by GenuineThreadPool.
		/// </summary>
		public static int MaximumThreads
		{
			get
			{
				lock(_maximumThreadsLock)
					return _maximumThreads;
			}
			set
			{
				lock(_maximumThreadsLock)
					_maximumThreads = value;
			}
		}
		private static int _maximumThreads = 500;
		private static object _maximumThreadsLock = new object();

		/// <summary>
		/// Working threads will be terminated after this time of inactiviy.
		/// </summary>
		public static TimeSpan CloseAfterInactivity
		{
			get
			{
				lock(_closeAfterInactivityLock)
					return _closeAfterInactivity;
			}
			set
			{
				lock(_closeAfterInactivityLock)
					_closeAfterInactivity = value;
			}
		}
		private static TimeSpan _closeAfterInactivity = TimeSpan.FromMinutes(4);
		private static object _closeAfterInactivityLock = new object();

		/// <summary>
		/// Gets or sets an indication of whether GenuineThreadPool will use ThreadPool.UnsafeQueueUserWorkItem 
		/// while working with the native ThreadPool.
		/// </summary>
		public static bool UnsafeQueuing
		{
			get
			{
				lock(_unsafeQueuingLock)
					return _unsafeQueuing;
			}
			set
			{
				lock(_unsafeQueuingLock)
					_unsafeQueuing = value;
			}
		}
		private static bool _unsafeQueuing = true;
		private static object _unsafeQueuingLock = new object();

		/// <summary>
		/// Represents an integer value indicating the maximum allowed number of queued requests if the number of the Genuine Thread 
		/// Pool's threads has exceeded GenuineThreadPool.MaximumThreads.
		/// </summary>
		public static int MaximumRequestsQueued
		{
			get
			{
				lock(_unsafeQueuingLock)
					return _maximumRequestsQueued;
			}
			set
			{
				lock(_unsafeQueuingLock)
					_maximumRequestsQueued = value;
			}
		}
		private static int _maximumRequestsQueued = 500;

		/// <summary>
		/// Represents a boolean value indicating whether requests may be queued when the number of the Genuine Thread 
		/// Pool's threads is close to GenuineThreadPool.MaximumThreads.
		/// </summary>
		public static bool AllowRequestQueuing
		{
			get
			{
				lock(_unsafeQueuingLock)
					return _allowRequestQueuing;
			}
			set
			{
				lock(_unsafeQueuingLock)
					_allowRequestQueuing = value;
			}
		}
		private static bool _allowRequestQueuing = true;

		/// <summary>
		/// Queued requests.
		/// </summary>
		internal static Queue _requestsQueued = new Queue();

		/// <summary>
		/// Work threads.
		/// </summary>
		private static ArrayList _threads = new ArrayList();

		/// <summary>
		/// A lock for accessing the thread array and each thread structure.
		/// </summary>
		private static object _threadsLock = new object();

		/// <summary>
		/// Specifies the Thread Pool Strategy used by Genuine Channels solution.
		/// </summary>
		public static GenuineThreadPoolStrategy GenuineThreadPoolStrategy
		{
			get
			{
				lock (_genuineThreadPoolStrategyLock)
					return _genuineThreadPoolStrategy;
			}
			set
			{
				lock (_genuineThreadPoolStrategyLock)
					_genuineThreadPoolStrategy = value;
			}
		}
		private static GenuineThreadPoolStrategy _genuineThreadPoolStrategy = GenuineThreadPoolStrategy.AlwaysThreads;
		private static object _genuineThreadPoolStrategyLock = new object();

		/// <summary>
		/// This variable contains the total number of attempts to queue a task through GenuineThreadPool.
		/// It is used to monitor the number of threads to the log file.
		/// </summary>
		private static int _QueueUserWorkItem_OperationNumber = 0;

		/// <summary>
		/// Queues a user work item to the thread pool.
		/// </summary>
		/// <param name="callback">A WaitCallback representing the method to execute.</param>
		/// <param name="state">An object containing data to be used by the method.</param>
		/// <param name="longDuration">Indicates whether the item depends on other working threads or can take a long period to finish.</param>
		public static void QueueUserWorkItem(WaitCallback callback, object state, bool longDuration)
		{
			GenuineThreadPoolStrategy theStrategy = GenuineThreadPool.GenuineThreadPoolStrategy;

			if (callback == null)
				throw new NullReferenceException("callback");
			CustomThreadInfo customThreadInfo = null;

			lock(_threadsLock)
			{
				BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;
				if (binaryLogWriter != null && binaryLogWriter[LogCategory.StatisticCounters] > 0 && (Interlocked.Increment(ref _QueueUserWorkItem_OperationNumber) % GenuineThreadPool.ThreadStatisticFrequency) == 0)
				{
					binaryLogWriter.WriteEvent(LogCategory.StatisticCounters, "GenuineThreadPool.QueueUserWorkItem",
						LogMessageType.ThreadPoolUsage, null, null, null, null,
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null,
						-1, _QueueUserWorkItem_OperationNumber, GenuineThreadPool._threads.Count, 0, null, null, null, null,
						"GenuineThreadPool has received a request #{0} (object \"{1}\" entry \"{2}\"). The current number of working threads: {3}. ThreadPoolAvailableThreads: {4}. MaximumThreads: {5}. CloseAfterInactivity: {6}. UnsafeQueuing: {7}. GenuineThreadPoolStrategy: {8}.",
						_QueueUserWorkItem_OperationNumber, callback.Method.ReflectedType.ToString(),
						callback.Method.ToString(),
						GenuineThreadPool._threads.Count,
						GenuineThreadPool.ThreadPoolAvailableThreads, GenuineThreadPool.MaximumThreads, 
						GenuineThreadPool.CloseAfterInactivity.TotalMilliseconds, GenuineThreadPool.UnsafeQueuing,
						Enum.Format(typeof(GenuineThreadPoolStrategy), GenuineThreadPool.GenuineThreadPoolStrategy, "g")
						);
				}

				if ( !( theStrategy == GenuineThreadPoolStrategy.AlwaysThreads ||
					(theStrategy == GenuineThreadPoolStrategy.OnlyLongDuration && longDuration ) ) )
				{
					// try to use native ThreadPool if it's possible
					int workerThreads = 0;
					int completionPortThreads = 0;

					if (theStrategy == GenuineThreadPoolStrategy.SwitchAfterExhaustion)
						ThreadPool.GetAvailableThreads(out workerThreads, out completionPortThreads);

					if (theStrategy == GenuineThreadPoolStrategy.AlwaysNative ||
						theStrategy == GenuineThreadPoolStrategy.OnlyLongDuration ||
						(Math.Min(workerThreads, completionPortThreads) >= GenuineThreadPool.ThreadPoolAvailableThreads && theStrategy == GenuineThreadPoolStrategy.SwitchAfterExhaustion) )
					{
						if (GenuineThreadPool.UnsafeQueuing)
							ThreadPool.UnsafeQueueUserWorkItem(callback, state);
						else
							ThreadPool.QueueUserWorkItem(callback, state);
						return ;
					}
				}

				// look for an available worker thread
				for ( int i = 0; i < _threads.Count; )
				{
					// get the next thread
					customThreadInfo = (CustomThreadInfo) _threads[i];

					// check if it's alive
					if (! customThreadInfo.IsAlive)
					{
						_threads.RemoveAt(i);
						customThreadInfo = null;
						continue;
					}

					if (customThreadInfo.IsIdle)
						break;

					customThreadInfo = null;
					i++;
				}

				// if the limit is exceeded
				if (customThreadInfo == null && _threads.Count >= GenuineThreadPool.MaximumThreads)
				{
					if (GenuineThreadPool.AllowRequestQueuing)
					{
						_requestsQueued.Enqueue( new GenuineThreadPoolWorkItem(callback, state) );
						return ;
					}
					else
						throw GenuineExceptions.Get_Processing_ThreadPoolLimitExceeded();
				}

				// create a new one, if it's necessary
				if (customThreadInfo == null)
				{
					customThreadInfo = new CustomThreadInfo(_threadsLock);
					_threads.Add(customThreadInfo);
				}

				// give out the work
				customThreadInfo.WorkCallback = callback;
				customThreadInfo.CallbackState = state;
				customThreadInfo.IsIdle = false;
				customThreadInfo.ManualResetEvent.Set();
			}
		}

		/// <summary>
		/// Gets a request from the queue or a null reference if the queue is empty.
		/// </summary>
		/// <returns>The request or a null reference.</returns>
		internal static GenuineThreadPoolWorkItem GetQueuedRequestIfPossible()
		{
			lock(_threadsLock)
			{
				if (_requestsQueued.Count > 0)
					return (GenuineThreadPoolWorkItem) _requestsQueued.Dequeue();
			}

			return null;
		}

		#region -- Debugging Stuff -----------------------------------------------------------------

		/// <summary>
		/// Gets or sets an integer value that determines how often Genuine Thread Pool will write log records describing the
		/// current state and tasks performed. 1 means that each GenuineThreadPool.QueueUserWorkItem will be saved in the log.
		/// 10 means that every 10th request will be indicated in the log file.
		/// </summary>
		public static int ThreadStatisticFrequency
		{
			get
			{
				lock (_threadStatisticFrequencyLock)
					return _threadStatisticFrequency;
			}
			set
			{
				if (value <= 0)
					throw new ArgumentOutOfRangeException("value");

				lock (_threadStatisticFrequencyLock)
					_threadStatisticFrequency = value;
			}
		}
		private static int _threadStatisticFrequency = 50;
		private static object _threadStatisticFrequencyLock = new object();

		/// <summary>
		/// Returns a string containing the list of active threads and their tasks.
		/// It is not recommended to invoke this method very often.
		/// </summary>
		/// <returns>A string containing the list of active threads and their tasks.</returns>
		public static string TakeSnapshot()
		{
			StringBuilder threadList = new StringBuilder();

			lock (_threadsLock)
			{
				// look for an available worker thread
				for ( int i = 0; i < _threads.Count; i++ )
				{
					// get the next thread
					CustomThreadInfo customThreadInfo = (CustomThreadInfo) _threads[i];

					// check if it's alive
					if (! customThreadInfo.IsAlive)
						continue;

					if (customThreadInfo.IsIdle)
						continue;

					threadList.AppendFormat("Thread {0} executes \"{1}\" entry \"{2}\".{3}", 
						customThreadInfo.Thread.Name, customThreadInfo.WorkCallback.Method.ReflectedType.ToString(),
						customThreadInfo.WorkCallback.Method.ToString(), Environment.NewLine);
				}
			}

			return threadList.ToString();
		}

		/// <summary>
		/// Calculates and returns the number of occupied threads.
		/// It is not recommended to invoke this method very often.
		/// </summary>
		/// <returns>The number of occupied threads.</returns>
		public static int CalculateOccupiedThreads()
		{
			int threadsOccupied = 0;

			lock (_threadsLock)
			{
				// look for an available worker thread
				for ( int i = 0; i < _threads.Count; i++ )
				{
					CustomThreadInfo customThreadInfo = (CustomThreadInfo) _threads[i];
					if ( ! (customThreadInfo.IsIdle || ! customThreadInfo.IsAlive) )
						threadsOccupied++;
				}
			}

			return threadsOccupied;
		}


		#endregion

	}
}
