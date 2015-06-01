/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.Threading;

using Belikov.GenuineChannels.Logbook;

namespace Belikov.GenuineChannels.Utilities
{
	/// <summary>
	/// Starts socket's asynchronous operation in the thread that never exit.
	/// </summary>
	internal class AsyncThreadStarter
	{
		private static ArrayList _workItems = new ArrayList();
		private static Thread _thread;
		private static ManualResetEvent _manualResetEvent = new ManualResetEvent(false);

		/// <summary>
		/// Queues the asynchronous operation.
		/// </summary>
		/// <param name="iAsyncWorkItem">The asynchronous operation.</param>
		public static void QueueTask(IAsyncWorkItem iAsyncWorkItem)
		{
			lock (_workItems.SyncRoot)
			{
				if (_thread == null)
				{
					_thread = new Thread(new ThreadStart(ServiceWorkItems));
					_thread.IsBackground = true;
					_thread.Name = "GC.AsyncThreadStarter";
					_thread.Start();
				}

				_workItems.Add(iAsyncWorkItem);
				_manualResetEvent.Set();
			}
		}

		/// <summary>
		/// Services the work items.
		/// </summary>
		public static void ServiceWorkItems()
		{
			for ( ; ; )
			{
				_manualResetEvent.WaitOne();

                lock (_workItems.SyncRoot)
                {
                    for ( int i = 0; i < _workItems.Count; i++)
					{
                        IAsyncWorkItem iAsyncWorkItem = (IAsyncWorkItem)_workItems[i];

                        try
						{
							iAsyncWorkItem.StartAsynchronousOperation();
						}
						catch (Exception ex)
						{
							// LOG:
							BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;
							if ( binaryLogWriter != null )
							{
								binaryLogWriter.WriteEvent(LogCategory.ImplementationWarning, "AsyncThreadStarter.ServiceWorkItems",
									LogMessageType.Error, ex, null, null, null, 
									GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
									null, null, -1, 0, 0, 0, null, null, null, null, 
									"Execution of \"{0}\" workitem has resulted in exception.", iAsyncWorkItem.GetType().FullName);
							}
						}
					}

					_workItems.Clear();
					_manualResetEvent.Reset();
				}
			}
		}
	}
}
