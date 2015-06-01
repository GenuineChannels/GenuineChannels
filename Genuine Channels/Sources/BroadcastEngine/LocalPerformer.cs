/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved. 
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Messaging;

namespace Belikov.GenuineChannels.BroadcastEngine
{
	/// <summary>
	/// Executes specified invocation of the object localted in the current appdomain.
	/// </summary>
	public class LocalPerformer
	{
		/// <summary>
		/// Constructs an instance of the LocalPerformer class.
		/// </summary>
		/// <param name="msg">The invocation to be executed.</param>
		/// <param name="resultCollector">The Result Collector.</param>
		/// <param name="mbr">The target.</param>
		public LocalPerformer(IMessage msg, ResultCollector resultCollector, MarshalByRefObject mbr)
		{
			this._msg = msg;
			this._resultCollector = resultCollector;
			this._mbr = mbr;
			this._mbrUri = RemotingServices.GetObjectUri(mbr);
		}

		private IMessage _msg;
		private ResultCollector _resultCollector;
		private MarshalByRefObject _mbr;
		private string _mbrUri;

		/// <summary>
		/// Invokes the target.
		/// </summary>
		/// <param name="state">Ignored.</param>
		public void Call(object state)
		{
			try
			{
				IMethodReturnMessage ret = RemotingServices.ExecuteMessage(this._mbr, (IMethodCallMessage) this._msg);
				this._resultCollector.ParseResult(this._mbrUri, ret, null);
			}
			catch(Exception ex)
			{
				this._resultCollector.ParseResult(this._mbrUri, null, ex);
			}
		}
	}
}
