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
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Utilities;

namespace Belikov.GenuineChannels.TransportContext
{
	/// <summary>
	/// GenuineEventProvider is a simple implementation of IGenuineEventProvider interface
	/// meant for local process use.
	/// </summary>
	public class GenuineEventProvider : MarshalByRefObject, IGenuineEventProvider
	{
		/// <summary>
		/// Constructs an instance of the GenuineEventProvider class.
		/// </summary>
		/// <param name="iTransportContext">The Transport Context.</param>
		public GenuineEventProvider(ITransportContext iTransportContext)
		{
			this.ITransportContext = iTransportContext;
		}

		/// <summary>
		/// The parent Transport Context.
		/// </summary>
		public ITransportContext ITransportContext;

		/// <summary>
		/// Registers the event consumer.
		/// </summary>
		/// <param name="iGenuineEventHandler">The event handler to be registered.</param>
		public void Register(IGenuineEventHandler iGenuineEventHandler)
		{
			using(new WriterAutoLocker(this._handlersLock))
			{
				if (! this._handlers.Contains(iGenuineEventHandler))
					this._handlers.Add(iGenuineEventHandler);
			}
		}

		/// <summary>
		/// Unregisters the event consumer.
		/// </summary>
		/// <param name="iGenuineEventHandler">The event handler to be registered.</param>
		public void Unregister(IGenuineEventHandler iGenuineEventHandler)
		{
			using(new WriterAutoLocker(this._handlersLock))
				this._handlers.Remove(iGenuineEventHandler);
		}

		/// <summary>
		/// A set of event handlers.
		/// </summary>
		private ArrayList _handlers = new ArrayList();

		/// <summary>
		/// Event handlers lock.
		/// </summary>
		private ReaderWriterLock _handlersLock = new ReaderWriterLock();

		/// <summary>
		/// Fires the event.
		/// </summary>
		/// <param name="genuineEventArgs">The event parameters.</param>
		public void Fire(GenuineEventArgs genuineEventArgs)
		{
			// acquire read access and get a list of the event receivers
			object[] handlers;
			using(new ReaderAutoLocker(this._handlersLock))
				handlers = this._handlers.ToArray();

			foreach (IGenuineEventHandler iGenuineEventHandler in handlers)
			{
				try
				{
					iGenuineEventHandler.OnGenuineEvent(genuineEventArgs);
				}
				catch(Exception)
				{
					using(new WriterAutoLocker(this._handlersLock))
						this._handlers.Remove(iGenuineEventHandler);
				}
			}

			// fire the global event as well
			if (genuineEventArgs.EventType == GenuineEventType.GTcpConnectionAccepted)
				this.FireGlobalEvent(genuineEventArgs);
			else
				GenuineThreadPool.QueueUserWorkItem(new WaitCallback(this.FireGlobalEvent), genuineEventArgs, false);
		}

		/// <summary>
		/// Fires the event.
		/// </summary>
		/// <param name="genuineEventArgsAsObject">Event parameters.</param>
		internal void FireGlobalEvent(object genuineEventArgsAsObject)
		{
			GenuineEventArgs genuineEventArgs = (GenuineEventArgs) genuineEventArgsAsObject;
			GenuineGlobalEventProvider.FireGlobalEvent(this.ITransportContext, genuineEventArgs);
		}

	}
}
