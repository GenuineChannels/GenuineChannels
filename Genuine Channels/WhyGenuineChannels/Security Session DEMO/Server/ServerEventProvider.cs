using System;
using System.Runtime.Remoting.Messaging;

using Known;
using Belikov.GenuineChannels.BroadcastEngine;
using Belikov.GenuineChannels.Security;

namespace Server
{
	/// <summary>
	/// ServerEventProvider.
	/// </summary>
	public class ServerEventProvider : MarshalByRefObject, IServerEventProvider
	{
		/// <summary>
		/// Constructs ServerEventProvider instance.
		/// </summary>
		/// <param name="form">Parent form to get settings from.</param>
		public ServerEventProvider(Form form)
		{
			this._form = form;

			this._dispatcher = new Dispatcher(typeof(IClientEventReceiver));
			this._dispatcher.BroadcastCallFinishedHandler = new BroadcastCallFinishedHandler(this.BroadcastCallFinishedHandler);
			this._dispatcher.CallIsAsync = true;
			this._iClientEventReceiver = (IClientEventReceiver) this._dispatcher.TransparentProxy;
		}

		private Dispatcher _dispatcher;
		private IClientEventReceiver _iClientEventReceiver;
		private Form _form;

		/// <summary>
		/// Subscribes to the event.
		/// </summary>
		/// <param name="iClientEventReceiver">An instance of the IClientEventReceiver class that will receive server events.</param>
		public void Subscribe(IClientEventReceiver iClientEventReceiver)
		{
			_dispatcher.Add((MarshalByRefObject) iClientEventReceiver);
		}

		/// <summary>
		/// Fires the event.
		/// </summary>
		public void FireEvent()
		{
			if (! this._form.IsEventFired)
				return ;

			this._iClientEventReceiver.ReceiveEvent("They have said that the next step for Longhorn will be at the Professional Developers Conference in Los Angeles next month.");
		}

		/// <summary>
		/// Is called by the _dispatcher when a call is finished. Does nothing.
		/// </summary>
		/// <param name="dispatcher"></param>
		/// <param name="message"></param>
		/// <param name="resultCollector"></param>
		public void BroadcastCallFinishedHandler(Dispatcher dispatcher, IMessage message,
			ResultCollector resultCollector)
		{
		}

		/// <summary>
		/// This is to insure that when created as a Singleton, the first instance never dies,
		/// regardless of the expired time.
		/// </summary>
		/// <returns></returns>
		public override object InitializeLifetimeService()
		{
			return null;
		}
	}
}
