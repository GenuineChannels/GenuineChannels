using System;

namespace Known
{
	/// <summary>
	/// IClientEventReceiver.
	/// </summary>
	public interface IClientEventReceiver
	{
		/// <summary>
		/// Is invocated by server at all clients subscribed to events.
		/// </summary>
		/// <param name="message">A message.</param>
		/// <returns>Null.</returns>
		object ReceiveEvent(string message);
	}
}
