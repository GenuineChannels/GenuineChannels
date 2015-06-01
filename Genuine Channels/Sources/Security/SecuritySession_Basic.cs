/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.IO;
using System.Threading;

using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Logbook;

namespace Belikov.GenuineChannels.Security
{
	/// <summary>
	/// Represents a Security Session, which does not provide any security capabilities. 
	/// </summary>
	public class SecuritySession_Basic : SecuritySession
	{
		/// <summary>
		/// Constructs an instance of the SecuritySession_Basic class.
		/// </summary>
		/// <param name="name">The name of the security context.</param>
		public SecuritySession_Basic(string name) : base(name, null)
		{
			this.IsEstablishedEvent.Set();

			// LOG:
			BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;
			if (binaryLogWriter != null && binaryLogWriter[LogCategory.Security] > 0 )
				binaryLogWriter.WriteEvent(LogCategory.Security, "SecuritySession_Basic.SecuritySession_Basic",
					LogMessageType.SecuritySessionKey, null, null, this.Remote, null, 
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, this,
					this.Name, -1, 0, 0, 0, "No security features supported.", null, null, null,
					"This Security Session does not require establishing.");
		}

		/// <summary>
		/// Initiates or continues establishing of the Security Session.
		/// Is used to establish a connection on the connection level.
		/// </summary>
		/// <param name="input">A null reference or an incoming stream.</param>
		/// <param name="connectionLevel">Indicates whether it's a connection-level Security Session.</param>
		/// <returns>A stream containing data for sending to the remote host or a null reference if Security Session is established.</returns>
		public override GenuineChunkedStream EstablishSession(Stream input, bool connectionLevel)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Encrypts the message data and put a result into the specified output stream.
		/// </summary>
		/// <param name="input">The stream containing the serialized message.</param>
		/// <param name="output">The result stream with the data being sent to the remote host.</param>
		public override void Encrypt(Stream input, GenuineChunkedStream output)
		{
			output.WriteStream(input);
		}

		/// <summary>
		/// Creates and returns a stream containing decrypted data.
		/// </summary>
		/// <param name="input">The stream containing encrypted data.</param>
		/// <returns>The stream with decrypted data.</returns>
		public override Stream Decrypt(Stream input)
		{
			return input;
		}

	}
}
