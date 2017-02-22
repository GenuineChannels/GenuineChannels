/* Genuine Channels product.
 *
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 *
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.IO;
using System.Security;
using Belikov.GenuineChannels.DirectExchange;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.Logbook
{
	/// <summary>
	/// Implements a log service allowing to enable and disable logging and download the log records.
	/// </summary>
	public class RemoteLogService : MarshalByRefObject, IServerServiceEntry
	{
		/// <summary>
		/// Constructs an instance of the LogService class.
		/// </summary>
		/// <param name="memoryWritingStream">The stream containing log records.</param>
		public RemoteLogService(MemoryWritingStream memoryWritingStream)
		{
			this._memoryWritingStream = memoryWritingStream;
		}

		private MemoryWritingStream _memoryWritingStream;

		private bool _stopped = false;

		/// <summary>
		/// Release the currently accumulated content.
		/// </summary>
		public void StopLogging()
		{
			_stopped = true;
			this._memoryWritingStream.StopLogging();
		}

		/// <summary>
		/// Sends the content of the log to the remote host.
		/// </summary>
		/// <param name="stream">The stream containing a request or a response.</param>
		/// <param name="sender">The remote host that sent this request.</param>
		/// <returns>The response.</returns>
		public Stream HandleMessage(Stream stream, HostInformation sender)
		{
			if (_stopped)
				return Stream.Null;

			int expectedSize = 640000;

			// copy to an intermediate stream
			GenuineChunkedStream intermediateStream = new GenuineChunkedStream(true);
			GenuineUtility.CopyStreamToStream(this._memoryWritingStream, intermediateStream, expectedSize);

			// and send it as one chunk
			return intermediateStream;
		}

		/// <summary>
		/// Gets or sets an indication of whether the log records can be written into this instance.
		/// </summary>
		public bool Enabled
		{
			get
			{
				return this._memoryWritingStream.Enabled;
			}
			set
			{
				if (_stopped)
					return ;

				this._memoryWritingStream.Enabled = value;
			}
		}

		/// <summary>
		/// This is to insure that when created as a Singleton, the first instance never dies,
		/// regardless of the expired time.
		/// </summary>
		/// <returns>A null reference.</returns>
		[SecurityCritical]
		public override object InitializeLifetimeService()
		{
			return null;
		}
	}
}
