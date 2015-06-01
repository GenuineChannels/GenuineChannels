/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.IO;
using System.Runtime.Remoting.Channels;

using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.DirectExchange
{
	/// <summary>
	/// Provides methods for using and publishing remoted objects and stream handlers.
	/// </summary>
	public class DirectExchangeManager : MarshalByRefObject
	{
		/// <summary>
		/// Constructs an instance of the DirectExchangeManager class.
		/// </summary>
		/// <param name="iTransportContext">The Transport Context.</param>
		public DirectExchangeManager(ITransportContext iTransportContext)
		{
			this.ITransportContext = iTransportContext;
		}

		/// <summary>
		/// The Transport Context.
		/// </summary>
		public ITransportContext ITransportContext;

		#region -- Server local service support ----------------------------------------------------

		/// <summary>
		/// Associates the server service provider with the specified entry.
		/// </summary>
		/// <param name="entryName">The name of the entry.</param>
		/// <param name="iServerServiceEntry">The server service provider.</param>
		/// <returns>The server service provider previously associated with this entry or a null reference.</returns>
		public IServerServiceEntry RegisterServerService(string entryName, IServerServiceEntry iServerServiceEntry)
		{
			IServerServiceEntry previousServerServiceEntry = this._services[entryName] as IServerServiceEntry;
			this._services[entryName] = iServerServiceEntry;
			return previousServerServiceEntry;
		}

		/// <summary>
		/// Unregisters the server service entry.
		/// </summary>
		/// <param name="entryName">The name of the entry.</param>
		/// <returns>The server service provider previously associated with this entry or a null reference.</returns>
		public IServerServiceEntry UnregisterServerService(string entryName)
		{
			IServerServiceEntry previousServerServiceEntry = this._services[entryName] as IServerServiceEntry;
			this._services.Remove(entryName);
			return previousServerServiceEntry;
		}

		/// <summary>
		/// Gets a service associated with the specified entry name.
		/// </summary>
		/// <param name="entryName">The name of the entry.</param>
		/// <returns>The service associated with the specified entry name.</returns>
		public IServerServiceEntry GetServerService(string entryName)
		{
			return this._services[entryName] as IServerServiceEntry;
		}

		/// <summary>
		/// Returns a list cotaining the names of all registered server service entries.
		/// Is used in debugging purposes.
		/// </summary>
		/// <returns>A list cotaining the names of all registered server service entries.</returns>
		public string[] GetListOfRegisteredServices()
		{
			lock (this._services.SyncRoot)
			{
				string[] services = new string[this._services.Count];
				this._services.Keys.CopyTo(services, 0);
				return services;
			}
		}

		/// <summary>
		/// All registered services.
		/// </summary>
		private Hashtable _services = Hashtable.Synchronized(new Hashtable());

		#endregion

		#region -- Server global service support ---------------------------------------------------

		/// <summary>
		/// Associates the global server service provider with the specified entry.
		/// </summary>
		/// <param name="entryName">The name of the entry.</param>
		/// <param name="iServerServiceEntry">The server service provider.</param>
		/// <returns>The server service provider previously associated with this entry or a null reference.</returns>
		public static IServerServiceEntry RegisterGlobalServerService(string entryName, IServerServiceEntry iServerServiceEntry)
		{
			IServerServiceEntry previousServerServiceEntry = _globalServices[entryName] as IServerServiceEntry;
			_globalServices[entryName] = iServerServiceEntry;
			return previousServerServiceEntry;
		}

		/// <summary>
		/// Unregisters the global server service entry.
		/// </summary>
		/// <param name="entryName">The name of the entry.</param>
		/// <returns>The global server service provider previously associated with this entry or a null reference.</returns>
		public static IServerServiceEntry UnregisterGlobalServerService(string entryName)
		{
			IServerServiceEntry previousServerServiceEntry = _globalServices[entryName] as IServerServiceEntry;
			_globalServices.Remove(entryName);
			return previousServerServiceEntry;
		}

		/// <summary>
		/// Returns a list cotaining the names of all registered global server service entries.
		/// Is used in debugging purposes.
		/// </summary>
		/// <returns>A list cotaining the names of all registered global server service entries.</returns>
		public static string[] GetListOfGlobalRegisteredServices()
		{
			lock (_globalServices.SyncRoot)
			{
				string[] services = new string[_globalServices.Count];
				_globalServices.Keys.CopyTo(services, 0);
				return services;
			}
		}

		/// <summary>
		/// All registered services.
		/// </summary>
		private static Hashtable _globalServices = Hashtable.Synchronized(new Hashtable());

		#endregion

		#region -- Direct sending support ----------------------------------------------------------

		/// <summary>
		/// Sends the content to the specified remote host and returns a response sent by the remote
		/// host in reply.
		/// </summary>
		/// <param name="destination">The remote host.</param>
		/// <param name="serviceName">The name of the service.</param>
		/// <param name="content">The content.</param>
		/// <returns>The response.</returns>
		public Stream SendSync(HostInformation destination, string serviceName, Stream content)
		{
			// create the message
			Message message = new Message(this.ITransportContext, destination, 0, new TransportHeaders(), content);
			message.IsSynchronous = true;
			message.GenuineMessageType = GenuineMessageType.ExternalStreamConsumer;
			message.DestinationMarshalByRef = serviceName;

			// register the response catcher
			SyncResponseProcessorWithEvent syncResponseProcessorWithEvent = new SyncResponseProcessorWithEvent(message);
			this.ITransportContext.IIncomingStreamHandler.RegisterResponseProcessor(message.MessageId, syncResponseProcessorWithEvent);

			// and send the message
			this.ITransportContext.ConnectionManager.Send(message);

			int timeSpanInMilliseconds = GenuineUtility.GetMillisecondsLeft(message.FinishTime);
			if (timeSpanInMilliseconds <= 0)
				throw GenuineExceptions.Get_Send_ServerDidNotReply();
			if (! syncResponseProcessorWithEvent.IsReceivedEvent.WaitOne(timeSpanInMilliseconds, false))
				throw GenuineExceptions.Get_Send_ServerDidNotReply();

			if (syncResponseProcessorWithEvent.DispatchedException != null)
				throw OperationException.WrapException(syncResponseProcessorWithEvent.DispatchedException);
			return syncResponseProcessorWithEvent.Response.Stream;
		}

		/// <summary>
		/// Sends the content to the specified remote host and redirects a response to the callback.
		/// </summary>
		/// <param name="destination">The remote host.</param>
		/// <param name="serviceName">The name of the service.</param>
		/// <param name="content">The content.</param>
		/// <param name="streamResponseEventHandler">The callback.</param>
		public void SendAsync(HostInformation destination, string serviceName, Stream content, StreamResponseEventHandler streamResponseEventHandler)
		{
			this.SendAsync(destination, serviceName, content, streamResponseEventHandler, null);
		}

		/// <summary>
		/// Sends the content to the specified remote host and redirects a response to the callback.
		/// </summary>
		/// <param name="destination">The remote host.</param>
		/// <param name="serviceName">The name of the service.</param>
		/// <param name="content">The content.</param>
		/// <param name="streamResponseEventHandler">The callback.</param>
		/// <param name="tag">The object that contains data about this invocation.</param>
		public void SendAsync(HostInformation destination, string serviceName, Stream content, StreamResponseEventHandler streamResponseEventHandler, object tag)
		{
			// create the message
			Message message = new Message(this.ITransportContext, destination, 0, new TransportHeaders(), content);
			message.IsSynchronous = false;
			message.GenuineMessageType = GenuineMessageType.ExternalStreamConsumer;
			message.DestinationMarshalByRef = serviceName;
			message.Tag = tag;

			// register the response catcher
			UniversalAsyncResponseProcessor universalAsyncResponseProcessor = new UniversalAsyncResponseProcessor(message, streamResponseEventHandler, null);
			this.ITransportContext.IIncomingStreamHandler.RegisterResponseProcessor(message.MessageId, universalAsyncResponseProcessor);

			// and send the message
			this.ITransportContext.ConnectionManager.Send(message);
		}

		/// <summary>
		/// Sends the content to the specified remote host and redirects a response to the callback.
		/// </summary>
		/// <param name="destination">The remote host.</param>
		/// <param name="serviceName">The name of the service.</param>
		/// <param name="content">The content.</param>
		/// <param name="iStreamResponseHandler">The response handler.</param>
		public void SendAsync(HostInformation destination, string serviceName, Stream content, IStreamResponseHandler iStreamResponseHandler)
		{
			this.SendAsync(destination, serviceName, content, iStreamResponseHandler, null);
		}

		/// <summary>
		/// Sends the content to the specified remote host and redirects a response to the callback.
		/// </summary>
		/// <param name="destination">The remote host.</param>
		/// <param name="serviceName">The name of the service.</param>
		/// <param name="content">The content.</param>
		/// <param name="iStreamResponseHandler">The response handler.</param>
		/// <param name="tag">The object that contains data about this invocation.</param>
		public void SendAsync(HostInformation destination, string serviceName, Stream content, IStreamResponseHandler iStreamResponseHandler, object tag)
		{
			// create the message
			Message message = new Message(this.ITransportContext, destination, 0, new TransportHeaders(), content);
			message.IsSynchronous = false;
			message.GenuineMessageType = GenuineMessageType.ExternalStreamConsumer;
			message.DestinationMarshalByRef = serviceName;
			message.Tag = tag;

			// register the response catcher
			UniversalAsyncResponseProcessor universalAsyncResponseProcessor = new UniversalAsyncResponseProcessor(message, null, iStreamResponseHandler);
			this.ITransportContext.IIncomingStreamHandler.RegisterResponseProcessor(message.MessageId, universalAsyncResponseProcessor);

			// and send the message
			this.ITransportContext.ConnectionManager.Send(message);
		}

		/// <summary>
		/// Sends one-way message to the remote host. Ignores all exceptions and does not 
		/// receive the response.
		/// </summary>
		/// <param name="destination">The remote host.</param>
		/// <param name="serviceName">The name of the entry.</param>
		/// <param name="content">The content.</param>
		public void SendOneWay(HostInformation destination, string serviceName, Stream content)
		{
			try
			{
				// create the message
				Message message = new Message(this.ITransportContext, destination, 0, new TransportHeaders(), content);
				message.IsOneWay = true;
				message.GenuineMessageType = GenuineMessageType.ExternalStreamConsumer;
				message.DestinationMarshalByRef = serviceName;

				// and send the message
				this.ITransportContext.ConnectionManager.Send(message);
			}
			catch (Exception)
			{
			}
		}

		#endregion

		#region -- Direct receiving support --------------------------------------------------------

		/// <summary>
		/// Handles the incoming request.
		/// </summary>
		/// <param name="message">The response.</param>
		/// <returns>The response.</returns>
		public Stream HandleRequest(Message message)
		{
			// fetch the name of the server service
			IServerServiceEntry iServerServiceEntry = null;
			string entryName = message.DestinationMarshalByRef as string;
			if (entryName != null)
				iServerServiceEntry = this._services[entryName] as IServerServiceEntry;

			// there is no service registered in the local collection, try the global collection
			if (entryName != null && iServerServiceEntry == null)
				iServerServiceEntry = _globalServices[entryName] as IServerServiceEntry;

			if (iServerServiceEntry == null)
			{
				// no services are registered to handle this request
//				message.ITransportContext.IEventLogger.Log(LogMessageCategory.Error, null, "DirectExchangeManager.HandlerRequest",
//					null, "There are no services associated with the \"{0}\" name. Incoming request is ignored.", entryName == null ? "<null!>" : entryName);
				message.ITransportContext.IGenuineEventProvider.Fire(new GenuineEventArgs(GenuineEventType.UnknownServerService, null, message.Sender, entryName));

				throw GenuineExceptions.Get_Receive_NoServices(entryName);
			}

			return iServerServiceEntry.HandleMessage(message.Stream, message.Sender);
		}

		#endregion

	}
}
