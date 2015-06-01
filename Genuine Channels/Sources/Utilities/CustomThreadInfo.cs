/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Threading;

using Belikov.GenuineChannels.Logbook;

namespace Belikov.GenuineChannels.Utilities
{
	/// <summary>
	/// Contains information on the working thread.
	/// </summary>
	internal class CustomThreadInfo
	{
		/// <summary>
		/// Initializes an instance of the CustomThreadInfo class.
		/// </summary>
		/// <param name="commonThreadInfoLock">The common lock.</param>
		public CustomThreadInfo(object commonThreadInfoLock)
		{
			this.CommonThreadInfoLock = commonThreadInfoLock;
			this.Thread = new Thread(new ThreadStart(this.ThreadEntry));
			this.Thread.IsBackground = true;
			this.Thread.Name = "GC.GTP.#" + Interlocked.Increment(ref _threadOrderNumber);
			this.Thread.Start();
		}

		/// <summary>
		/// Is used to provide unique thread numbers for debugging purposes.
		/// </summary>
		private static int _threadOrderNumber = 0;

		/// <summary>
		/// The thread.
		/// </summary>
		public Thread Thread;

		/// <summary>
		/// Is set when it is necessary to perform a task.
		/// </summary>
		public ManualResetEvent ManualResetEvent = new ManualResetEvent(false);

		/// <summary>
		/// Indicates whether the thread performs a request.
		/// </summary>
		public bool IsIdle = true;

		/// <summary>
		/// Indicates whether this thread is alive.
		/// </summary>
		public bool IsAlive = true;

		/// <summary>
		/// A WaitCallback representing the method to be executed.
		/// </summary>
		public WaitCallback WorkCallback;

		/// <summary>
		/// Parameter for the worker callback.
		/// </summary>
		public object CallbackState;

		/// <summary>
		/// Common lock for all CustomThreadInfo cells.
		/// </summary>
		public object CommonThreadInfoLock;

		/// <summary>
		/// Implements a GenuineThreadPool's thread logic.
		/// </summary>
		public void ThreadEntry()
		{
			BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;

			try
			{
				for ( ; ; )
				{
					// wait for a event or timeout
					bool signalled = this.ManualResetEvent.WaitOne(GenuineThreadPool.CloseAfterInactivity, false);

					// if time outs
					if (! signalled)
					{
						lock(this.CommonThreadInfoLock)
						{
							if (this.WorkCallback == null)
							{
								this.IsAlive = false;
								return ;
							}
						}
					}

					// execute the request
					try
					{
						for ( ; ; )
						{
							try
							{
								this.WorkCallback(this.CallbackState);
							}
							catch (Exception ex)
							{
								if (binaryLogWriter != null && binaryLogWriter[LogCategory.StatisticCounters] > 0)
								{
									binaryLogWriter.WriteEvent(LogCategory.StatisticCounters, "CustomThreadInfo.ThreadEntry",
										LogMessageType.Error, ex, null, null, null,
										GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
										null, null,
										-1, 0, 0, 0, null, null, null, null,
										"The execution of the workitem has resulted in the exception."
										);
								}
							}

							// try to get the next workitem
							lock (this.CommonThreadInfoLock)
							{
								GenuineThreadPoolWorkItem nextWorkItem = GenuineThreadPool.GetQueuedRequestIfPossible();
								if (nextWorkItem != null)
								{
									this.WorkCallback = nextWorkItem.WorkCallback;
									this.CallbackState = nextWorkItem.CallbackState;
									continue;
								}
							}

							break;
						} // fetching workitem loop
					}
					finally
					{
						// reset values in a transaction
						lock (this.CommonThreadInfoLock)
						{
							this.ManualResetEvent.Reset();
							this.WorkCallback = null;
							this.CallbackState = null;
							this.IsIdle = true;
						}
					}
				}
			}
			catch(Exception)
			{
			}
			finally
			{
				// if the manager has managed to give out a request, redispatch the request again
				lock (this.CommonThreadInfoLock)
				{
					// we are not to be chosen
					this.IsAlive = false;

					if (this.WorkCallback != null)
					{
						GenuineThreadPool.QueueUserWorkItem(this.WorkCallback, this.CallbackState, true);
						this.WorkCallback = null;
						this.CallbackState = null;
					}
				}
			}
		}
	}
}
