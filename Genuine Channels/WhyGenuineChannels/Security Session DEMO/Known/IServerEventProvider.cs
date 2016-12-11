using System;

namespace Known
{
	/// <summary>
	/// IServerEventProvider.
	/// </summary>
	public interface IServerEventProvider
	{
		/// <summary>
		/// Subscribes to the event.
		/// </summary>
		/// <param name="iClientEventReceiver">An instance of the IClientEventReceiver class that will receive server events.</param>
		void Subscribe(IClientEventReceiver iClientEventReceiver);
	}
}
