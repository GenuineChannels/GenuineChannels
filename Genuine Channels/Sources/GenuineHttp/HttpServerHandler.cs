/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.IO;
using System.Runtime.Remoting.Channels;
using System.Security.Principal;
using System.Threading;
using System.Web;

using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Utilities;

namespace Belikov.GenuineChannels.GenuineHttp
{
	/// <summary>
	/// Implements ASP.NET Web handler which services incoming GHTTP requests.
	/// </summary>
	public class HttpServerHandler : IHttpAsyncHandler
	{
		/// <summary>
		/// Constructs an instance of the HttpServerHandler class.
		/// </summary>
		public HttpServerHandler()
		{
		}

		/// <summary>
		/// The HTTP context.
		/// </summary>
		public HttpContext HttpContext;

		/// <summary>
		/// The provided async callback.
		/// </summary>
		public AsyncCallback AsyncCallback;

		#region  -- IHttpAsyncHandler Memebers -----------------------------------------------------

		/// <summary>
		/// Enables processing of HTTP Web requests by a custom HttpHandler that implements the IHttpHandler interface.
		/// </summary>
		/// <param name="context">The http context.</param>
		public void ProcessRequest(HttpContext context)
		{
			IAsyncResult iAsyncResult = this.BeginProcessRequest(context, null, null);
			iAsyncResult.AsyncWaitHandle.WaitOne();
			this.EndProcessRequest(iAsyncResult);
		}

		/// <summary>
		/// Initiates an asynchronous call to the HTTP handler.
		/// </summary>
		/// <param name="context">An HttpContext object that provides references to intrinsic server objects (for example, Request, Response, Session, and Server) used to service HTTP requests.</param>
		/// <param name="cb">The AsyncCallback to call when the asynchronous method call is complete. If cb is a null reference (Nothing in Visual Basic), the delegate is not called.</param>
		/// <param name="extraData">Any extra data needed to process the request.</param>
		/// <returns>An IAsyncResult that contains information about the status of the process.</returns>
		public IAsyncResult BeginProcessRequest(HttpContext context, AsyncCallback cb, object extraData)
		{
			try
			{
				HttpServerRequestResult httpServerRequestResult = new HttpServerRequestResult(context, cb, extraData);
				httpServerRequestResult.IPrincipal = Thread.CurrentPrincipal;

				lock (this._httpServerConnectionManagerLock)
					if (this._httpServerConnectionManager == null)
					{
						GenuineHttpServerChannel serverChannel = ChannelServices.GetChannel("ghttp") as GenuineHttpServerChannel;
						if (serverChannel == null)
							throw GenuineExceptions.Get_Receive_NoServerChannel();

						this._httpServerConnectionManager = serverChannel.ITransportContext.ConnectionManager as HttpServerConnectionManager;
					}

//				GenuineThreadPool.QueueUserWorkItem(new WaitCallback(this._httpServerConnectionManager.HandleIncomingRequest), httpServerRequestResult, true);
				this._httpServerConnectionManager.HandleIncomingRequest(httpServerRequestResult);
				return httpServerRequestResult;
			}
			catch(Exception ex)
			{
				// LOG:
				BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.AcceptingConnection] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.AcceptingConnection, "HttpServerHandler.BeginProcessRequest",
						LogMessageType.ConnectionAccepting, ex, null, null, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, -1, 0, 0, 0, null, null, null, null,
						"Can't process an incoming connection.");
				}
				throw;
			}
		}
		private HttpServerConnectionManager _httpServerConnectionManager;
		private object _httpServerConnectionManagerLock = new object();

		/// <summary>
		/// Executes a clean-up code when the process ends.
		/// </summary>
		/// <param name="result">An IAsyncResult that contains information about the status of the process.</param>
		public void EndProcessRequest(IAsyncResult result)
		{
			result.AsyncWaitHandle.WaitOne();
		}

		/// <summary>
		/// Gets a value indicating whether another request can use the IHttpHandler instance.
		/// </summary>
		public bool IsReusable 
		{
			get
			{
				return true;
			}
		}

		#endregion

	}
}
