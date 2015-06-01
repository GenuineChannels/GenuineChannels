/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Net;
using System.Web;
using System.Threading;

using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Utilities;


namespace Belikov.GenuineChannels.GenuineHttp
{

#if (!FRM20)

	/// <summary>
	/// Makes sure that HttpWebRequest behaves as expected and does not try "forget" about the made request.
	/// </summary>
	public class HttpWebRequestCop : ITimerConsumer
	{
		/// <summary>
		/// Initiates the HTTP web request.
		/// </summary>
		/// <param name="httpWebRequest">The HttpWebRequest.</param>
		/// <param name="asyncCallback">The callback.</param>
		/// <param name="state">The additional parameter to the request.</param>
		/// <param name="timeoutSpan">The timeout of the operation.</param>
		public HttpWebRequestCop(HttpWebRequest httpWebRequest, AsyncCallback asyncCallback, object state, int timeoutSpan)
		{
			this._httpWebRequest = httpWebRequest;
			this._asyncCallback = asyncCallback;
			this._state = state;
			this._timeoutExpiresAt = GenuineUtility.GetTimeout(timeoutSpan);

			// asyncCallback
			this._httpWebRequest.BeginGetResponse(new AsyncCallback(this.HandleWebResponse), state);

			TimerProvider.Attach(this);
        }

		private HttpWebRequest _httpWebRequest;
		private AsyncCallback _asyncCallback;
		private object _state;
		private int _timeoutExpiresAt;
		private bool _isCompleted = false;
		private bool _isAborted = false;

		/// <summary>
		/// Is called upon completion of the HttpWebRequest.
		/// </summary>
		/// <param name="ar">The AsyncResult of the asynchronous operation.</param>
		private void HandleWebResponse(IAsyncResult ar)
		{
//			BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;
//			if ( binaryLogWriter != null )
//			{
//				binaryLogWriter.WriteEvent(LogCategory.Debugging, "HttpWebRequestCop.HandleWebResponse",
//					LogMessageType.DebuggingWarning, null, null, null, null,
//					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
//					-1, 0, 0, 0, null, null, null, null,
//					"Handling the web response. Remaining time: {0}.", GenuineUtility.GetMillisecondsLeft(this._timeoutExpiresAt));
//			}

			lock (this)
			{
				if (this._isAborted)
				{
					// ignore the result
					try
					{
						this._httpWebRequest.EndGetResponse(ar);
					}
					catch
					{
					}

					return ;
				}

				this._isCompleted = true;
			}

			this._asyncCallback(ar);
		}

		/// <summary>
		/// Checks whether the timeout has expired.
		/// </summary>
		public void TimerCallback()
		{
			lock (this)
			{
				if (this._isCompleted || this._isAborted)
					return ;

				if (GenuineUtility.IsTimeoutExpired(this._timeoutExpiresAt))
				{
					// TODO: remove this
					// LOG:
					BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;
					if ( binaryLogWriter != null )
					{
						binaryLogWriter.WriteEvent(LogCategory.Debugging, "HttpWebRequestCop.TimerCallback",
							LogMessageType.DebuggingWarning, null, null, null, null,
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null, null,
							-1, 
							0, 0, 0, null, null, null, null,
							"Stopping the web request.");
					}

					this._isAborted = true;
					this._httpWebRequest.Abort();
				}
			}
		}
	}

#else

    /// <summary>
    /// Implements async web requests on .Net Framework v2.0.
    /// </summary>
    public class HttpWebRequestCop
    {
        private class HttpWebRequestCopAsyncResult : IAsyncResult
        {
            public HttpWebRequestCopAsyncResult(object state)
            {
                this._state = state;
            }

            private object _state;
            private ManualResetEvent _waitHandle = new ManualResetEvent(true);

            /// <summary>
            /// Gets a user-defined object that qualifies or contains information about an asynchronous operation.
            /// </summary>
            public object AsyncState
            {
                get { return _state; }
            }

            /// <summary>
            /// Gets a System.Threading.WaitHandle that is used to wait for an asynchronous operation to complete.
            /// </summary>
            public WaitHandle AsyncWaitHandle
            {
                get { return _waitHandle; }
            }

            /// <summary>
            /// Gets an indication of whether the asynchronous operation completed synchronously.
            /// </summary>
            public bool CompletedSynchronously
            {
                get { return true; }
            }

            /// <summary>
            /// Gets an indication whether the asynchronous operation has completed.
            /// </summary>
            public bool IsCompleted
            {
                get { return true; }
            }

        }
        /// <summary>
        /// Initiates the HTTP web request.
        /// </summary>
        /// <param name="httpWebRequest">The HttpWebRequest.</param>
        /// <param name="asyncCallback">The callback.</param>
        /// <param name="state">The additional parameter to the request.</param>
        /// <param name="timeoutSpan">The timeout of the operation.</param>
        public HttpWebRequestCop(HttpWebRequest httpWebRequest, AsyncCallback asyncCallback, object state, int timeoutSpan)
        {
            this._asyncCallback = asyncCallback;
            httpWebRequest.Timeout = timeoutSpan;

            GenuineThreadPool.QueueUserWorkItem(new WaitCallback(Start), state, false);
        }

        private void Start(object state)
        {
            this._asyncCallback(new HttpWebRequestCopAsyncResult(state));
        }

        private AsyncCallback _asyncCallback;

    }
#endif
}
