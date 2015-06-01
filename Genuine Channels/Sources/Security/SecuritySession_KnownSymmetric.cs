/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;

using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;

namespace Belikov.GenuineChannels.Security
{
	/// <summary>
	/// SecuritySession_KnownSymmetric uses the provided symmetric cryptography algorithm 
	/// to encrypt or decrypt traffic being sent in this security context.
	/// </summary>
	public class SecuritySession_KnownSymmetric : SecuritySession
	{
		/// <summary>
		/// Initializes an instance of the SecuritySession_KnownSymmetric class.
		/// </summary>
		/// <param name="symmetricAlgorithm">The symmetricAlgorithm to be used.</param>
		/// <param name="name">The name of the security context.</param>
		public SecuritySession_KnownSymmetric(SymmetricAlgorithm symmetricAlgorithm, string name)
			: base(name, null)
		{
			// LOG:
			BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;
			if (binaryLogWriter != null && binaryLogWriter[LogCategory.Security] > 0 )
			{
				binaryLogWriter.WriteEvent(LogCategory.Security, "SecuritySession_KnownSymmetric.SecuritySession_KnownSymmetric",
					LogMessageType.SecuritySessionKey, null, null, this.Remote, null, 
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, this,
					name, -1, 
					0, 0, 0, "Encryption using " + symmetricAlgorithm.GetType().ToString(), null, null, null,
					"Security Session security information is initialized.");
			}

			this.SymmetricAlgorithm = symmetricAlgorithm;
			this._encryptor = this.SymmetricAlgorithm.CreateEncryptor();
			this._decryptor = this.SymmetricAlgorithm.CreateDecryptor();
			this.IsEstablishedEvent.Set();
		}

		/// <summary>
		/// Cryptographic encryptor.
		/// </summary>
		private ICryptoTransform _encryptor;

		/// <summary>
		/// Cryptographic decryptor.
		/// </summary>
		private ICryptoTransform _decryptor;

		/// <summary>
		/// The used symmetric algorithm.
		/// </summary>
		public SymmetricAlgorithm SymmetricAlgorithm;

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
			// there are two good approaches here: either encrypt the stream directly into the target
			lock (this._encryptor)
				GenuineUtility.CopyStreamToStream(new CryptoStream(new FinishReadingStream(input), this._encryptor, CryptoStreamMode.Read), output);
			// or create separate encryptor instance for encrypting at run-time
//			stream.WriteStream(new ResettableCryptoStream(input, this.SymmetricAlgorithm.CreateEncryptor()));
		}

		/// <summary>
		/// Creates and returns a stream containing decrypted data.
		/// </summary>
		/// <param name="input">A stream containing encrypted data.</param>
		/// <returns>A stream with decrypted data.</returns>
		public override Stream Decrypt(Stream input)
		{
			// the first approach
			lock (this._decryptor)
			{
				GenuineChunkedStream output = new GenuineChunkedStream(true);
				GenuineUtility.CopyStreamToStream(new CryptoStream(new FinishReadingStream(input), this._decryptor, CryptoStreamMode.Read), output);
				return output;
			}

			// the second approach
//			return new CryptoStream(new FinishReadingStream(input), this.SymmetricAlgorithm.CreateDecryptor(), CryptoStreamMode.Read);
		}

	}
}
