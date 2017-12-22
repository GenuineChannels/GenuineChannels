using System;

using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.BroadcastEngine;

namespace Server
{
	/// <summary>
	/// UserFilter implements a filter that filters out user with the specified userId.
	/// </summary>
	public class UserFilter : IMulticastFilter
	{
		/// <summary>
		/// Constructs UserFilter instance with known userId.
		/// </summary>
		/// <param name="userId"></param>
		public UserFilter(string userId)
		{
			this._userId = userId;
		}

		private string _userId;

		/// <summary>
		/// Filters out the sender.
		/// </summary>
		/// <param name="cachedReceiverList">List of all potential recipients.</param>
		/// <param name="iMessage">The message being sent.</param>
		/// <returns>List of all recipients being called.</returns>
		public object[] GetReceivers(object[] cachedReceiverList, System.Runtime.Remoting.Messaging.IMessage iMessage)
		{
			object[] resultList = new object[cachedReceiverList.Length];
			int resultListPosition = 0;

			// by all receivers
			for ( int i = 0; i < cachedReceiverList.Length; i++ )
			{
				// get ReceiverInfo instance
				ReceiverInfo receiverInfo = cachedReceiverList[i] as ReceiverInfo;
				if (receiverInfo == null)
					continue;

				// fetch the session
				ISessionSupport session = (ISessionSupport) receiverInfo.Tag;

				// and check on the call condition
				if ( (string) session["UserId"] != this._userId )
					resultList[ resultListPosition++ ] = receiverInfo;
			}

			return resultList;
		}

	}
}
