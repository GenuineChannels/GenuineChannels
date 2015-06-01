/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved. 
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.Collections.Specialized;
using System.IO;
using System.Threading;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Proxies;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Serialization.Formatters.Binary;

using Belikov.Common.ThreadProcessing;
using Belikov.GenuineChannels.Messaging;

namespace Belikov.GenuineChannels.BroadcastEngine
{
	/// <summary>
	/// Implements RealProxy that sends messages to all registered receivers.
	/// </summary>
	internal class BroadcastProxy : RealProxy
	{
		/// <summary>
		/// Constructs an instance of the BroadcastProxy class.
		/// </summary>
		/// <param name="interfaceToSupport">The object representing a well-known interface which is supported by all receivers.</param>
		/// <param name="dispatcher">The instance of the Dispatcher class containing a list of the receivers.</param>
		public BroadcastProxy(Type interfaceToSupport, Dispatcher dispatcher) : base(interfaceToSupport)
		{
			this._dispatcher = dispatcher;
		}

		private Dispatcher _dispatcher;

		/// <summary>
		/// Broadcasts message to all registered receivers.
		/// </summary>
		/// <param name="msg">The message to be broadcasted.</param>
		/// <returns></returns>
		public override IMessage Invoke(IMessage msg)
		{
			ResultCollector resultCollector = null;

			// broadcasting
			using (ReaderAutoLocker reader = new ReaderAutoLocker(this._dispatcher._readerWriterLock))
			{
				resultCollector = new ResultCollector(this._dispatcher, this._dispatcher._receivers.Count + 3, msg);
			}

			resultCollector.PerformBroadcasting(msg);
			return new ReturnMessage(resultCollector, null, 0, (LogicalCallContext) msg.Properties["__CallContext"], (IMethodCallMessage) msg);
		}

	}
}
