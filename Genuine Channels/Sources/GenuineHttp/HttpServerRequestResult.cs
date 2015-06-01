/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Security.Principal;
using System.Threading;
using System.Web;

using Belikov.GenuineChannels.Logbook;

namespace Belikov.GenuineChannels.GenuineHttp
{
	/// <summary>
	/// Represents the result of the HTTP request processed asynchronously.
	/// </summary>
	public class HttpServerRequestResult : IAsyncResult
	{
		/// <summary>
		/// Constructs an instance of the HttpServerRequestResult class.
		/// </summary>
		/// <param name="httpContext">The http context.</param>
		/// <param name="asyncCallback">The async callback.</param>
		/// <param name="asyncState">The async state.</param>
		public HttpServerRequestResult(HttpContext httpContext, AsyncCallback asyncCallback, object asyncState)
		{
			this.HttpContext = httpContext;
			this.AsyncCallback = asyncCallback;
			this._asyncState = asyncState;
		}

		/// <summary>
		/// The http context.
		/// </summary>
		public HttpContext HttpContext;

		/// <summary>
		/// The threads' current principal.
		/// </summary>
		public IPrincipal IPrincipal;

		/// <summary>
		/// The listener's callback.
		/// </summary>
		public AsyncCallback AsyncCallback;

//		/// <summary>
//		/// The moment when this request was received.
//		/// </summary>
//		public int Received = GenuineUtility.TickCount;

		/// <summary>
		/// Completes the asynchronous invocation.
		/// </summary>
		/// <param name="completedSynchronously">True if the request was completed synchronously.</param>
		public void Complete(bool completedSynchronously)
		{
			try
			{
				this._completedSynchronously = completedSynchronously;
				this._asyncWaitHandle.Set();

				if (this.AsyncCallback != null)
					this.AsyncCallback.DynamicInvoke(new object[] { (IAsyncResult) this } );
			}
			catch(Exception ex)
			{
				// LOG:
				BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;
				if ( binaryLogWriter != null )
				{
					binaryLogWriter.WriteImplementationWarningEvent("HttpServerRequestResult.Complete",
						LogMessageType.CriticalError, ex, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						"Fatal logic error.");
				}
			}
		}

		#region -- IAsyncResult Members ------------------------------------------------------------

		/// <summary>
		/// Gets a user-defined object that qualifies or contains information about an asynchronous operation.
		/// </summary>
		public object AsyncState
		{
			get
			{
				return this._asyncState;
			}
		}
		private object _asyncState;

		/// <summary>
		/// Gets an indication of whether the asynchronous operation completed synchronously.
		/// </summary>
		public bool CompletedSynchronously
		{
			get
			{
				return this._completedSynchronously;
			}
		}
		private bool _completedSynchronously = false;

		/// <summary>
		/// Gets a WaitHandle that is used to wait for an asynchronous operation to complete.
		/// </summary>
		public WaitHandle AsyncWaitHandle
		{
			get
			{
				return this._asyncWaitHandle;
			}
		}
		private ManualResetEvent _asyncWaitHandle = new ManualResetEvent(false);

		/// <summary>
		/// Gets an indication whether the asynchronous operation has completed.
		/// </summary>
		public bool IsCompleted
		{
			get
			{
				return this._asyncWaitHandle.WaitOne(0, false);
			}
		}

		#endregion
	}
}
