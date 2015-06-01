/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Diagnostics;
using System.Collections;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Channels;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Utilities;

namespace Belikov.GenuineChannels.Security
{
	/// <summary>
	/// SecuritySession_EstablishingSymmetric creates a RSA key and sends public key
	/// to the remote host. Then receives encrypted 256-bit Rijndael key that will be used for
	/// further encryption.
	/// </summary>
	public class SecuritySession_SelfEstablishingSymmetric : SecuritySession
	{
		/// <summary>
		/// Constructs an instance of the SecuritySession_SelfEstablishingSymmetric class.
		/// </summary>
		/// <param name="name">Name of the SecuritySession being created.</param>
		/// <param name="remote">The remote host.</param>
		public SecuritySession_SelfEstablishingSymmetric(string name, HostInformation remote)
			: base (name, remote)
		{
			// to avoid an issue described in Q322371
			try
			{
				// try the usual way, and if fails, use the patch in the catch block
				this._rsaCryptoServiceProviderDecryptor = new RSACryptoServiceProvider();
			}
			catch
			{
				CspParameters _CSPParam = new CspParameters();
				_CSPParam.Flags = CspProviderFlags.UseMachineKeyStore;
				this._rsaCryptoServiceProviderDecryptor = new RSACryptoServiceProvider(_CSPParam);
			}
		}

		/// <summary>
		/// The parent factory.
		/// </summary>
		public KeyProvider_SelfEstablishingSymmetric KeyProvider_SelfEstablishingSymmetric;

		/// <summary>
		/// The RSA decryptor.
		/// </summary>
		private RSACryptoServiceProvider _rsaCryptoServiceProviderDecryptor;

		/// <summary>
		/// Sent or received Rijndael Key.
		/// </summary>
		private byte[] RijndaelKey;

		/// <summary>
		/// An identifier of the local Security Session.
		/// </summary>
		private string _localInstanceGuid = Guid.NewGuid().ToString("N");

		/// <summary>
		/// An identifier of the remote Security Session.
		/// </summary>
		private string _remoteInstanceGuid;

		/// <summary>
		/// Cryptographic encryptor.
		/// </summary>
		private ICryptoTransform _encryptor;

		/// <summary>
		/// Cryptographic decryptor.
		/// </summary>
		private ICryptoTransform _decryptor;

		/// <summary>
		/// Initiates or continues establishing of the Security Session.
		/// </summary>
		/// <param name="input">A null reference or an incoming stream.</param>
		/// <param name="connectionLevel">Indicates whether the Security Session operates on connection level.</param>
		/// <returns>A stream containing data for sending to the remote host or a null reference if Security Session is established.</returns>
		public override GenuineChunkedStream EstablishSession(Stream input, bool connectionLevel)
		{
			// a dance is over
			if (this.IsEstablished)
				return null;

			GenuineChunkedStream outputStream = null;
			BinaryFormatter binaryFormatter = new BinaryFormatter();

			// skip the status flag
			if (connectionLevel)
			{
				if (input != null)
					input.ReadByte();

				outputStream = new GenuineChunkedStream(false);
			}
			else
				outputStream = this.CreateOutputStream();

			lock (this)
			{
				// write session is being established flag
				BinaryWriter binaryWriter = new BinaryWriter(outputStream);
				binaryWriter.Write((bool) false);

				// remote host sent nothing, send a RSA public key
				if (input == null || input == Stream.Null)
				{
					// serialize RSA public key
					RSAParameters rsaParameters = this._rsaCryptoServiceProviderDecryptor.ExportParameters(false);
					binaryFormatter.Serialize(outputStream, rsaParameters);
					binaryFormatter.Serialize(outputStream, this._localInstanceGuid);

					return outputStream;
				}

				// deserialize incoming data
				Rijndael rijndael = null;
				object receivedObject = binaryFormatter.Deserialize(input);

				// RSA public key has been received
				if (receivedObject is RSAParameters)
				{
					this._remoteInstanceGuid = (string) binaryFormatter.Deserialize(input);
					if (string.Compare(this._remoteInstanceGuid, this._localInstanceGuid, false) > 0)
						return this.EstablishSession(Stream.Null, connectionLevel);

					if (this.RijndaelKey == null)
					{
						// create Rijndael key
						rijndael = Rijndael.Create();
						this.RijndaelKey = rijndael.Key;
					}

					// encrypt it with public rsa key
					RSAParameters rsaParameters = (RSAParameters) receivedObject;
					RSACryptoServiceProvider rsaCryptoServiceProvider;
 					try
 					{
 						// try the usual way, and if fails, use the patch in the catch block
 						rsaCryptoServiceProvider = new RSACryptoServiceProvider();
 					}
 					catch
 					{
 						CspParameters CSPParam = new CspParameters();
 						CSPParam.Flags = CspProviderFlags.UseMachineKeyStore;
 						rsaCryptoServiceProvider = new RSACryptoServiceProvider(CSPParam);
 					}
					rsaCryptoServiceProvider.ImportParameters(rsaParameters);

					// serialize
					byte[] encryptedContent = RSAUtility.Encrypt(rsaCryptoServiceProvider, this.RijndaelKey);
					binaryFormatter.Serialize(outputStream, encryptedContent);

					return outputStream;
				}

				// Rijndael key has been received
				if (receivedObject is byte[])
				{
					// first of all, retrieve it
					byte[] receivedRijndaelKey = RSAUtility.Decrypt(this._rsaCryptoServiceProviderDecryptor, (byte[]) receivedObject);

					// accept received key
					this.RijndaelKey = receivedRijndaelKey;

					// confirm that the session has been established
					binaryFormatter.Serialize(outputStream, "OK");
					this.SessionEstablished();
					return outputStream;
				}

				// a confirmation received that the session is established
				if (receivedObject is string)
				{
					this.SessionEstablished();
					return null;
				}
			}

			throw GenuineExceptions.Get_Receive_IncorrectData();
		}

		/// <summary>
		/// Encrypts the message data and put a result into the specified output stream.
		/// </summary>
		/// <param name="input">The stream containing the serialized message.</param>
		/// <param name="output">The result stream with the data being sent to the remote host.</param>
		public override void Encrypt(Stream input, GenuineChunkedStream output)
		{
			output.WriteByte(1);
			lock (this._encryptor)
				GenuineUtility.CopyStreamToStream(new CryptoStream(new FinishReadingStream(input), this._encryptor, CryptoStreamMode.Read), output);
		}

		/// <summary>
		/// Creates and returns a stream containing decrypted data.
		/// </summary>
		/// <param name="input">A stream containing encrypted data.</param>
		/// <returns>A stream with decrypted data.</returns>
		public override Stream Decrypt(Stream input)
		{
			// check on view whether it's session's packet
			if (input.ReadByte() == 0)
			{
				// continue the Security Session establishing
				Stream outputStream = this.EstablishSession(input, false);

				if (outputStream != null)
					GenuineThreadPool.QueueUserWorkItem(new WaitCallback(this.SendMessage), outputStream, false);
				return null;
			}

			if (this.RijndaelKey == null)
				throw GenuineExceptions.Get_Security_ContextWasNotEstablished(this.Name);

			lock (this.RijndaelKey)
			{
				if (this._decryptor == null)
				{
					Rijndael rijndael = Rijndael.Create();
					rijndael.Key = this.RijndaelKey;
					rijndael.Mode = CipherMode.ECB;

					this._encryptor = rijndael.CreateEncryptor();
					this._decryptor = rijndael.CreateDecryptor();
				}
			}

			lock (this._decryptor)
			{
				GenuineChunkedStream output = new GenuineChunkedStream(true);
				GenuineUtility.CopyStreamToStream(new CryptoStream(new FinishReadingStream(input), this._decryptor, CryptoStreamMode.Read), output);

				GenuineUtility.CopyStreamToStream(input, Stream.Null);
				input.Close();

				return output;
			}
		}

		/// <summary>
		/// Initializes encryptor and decryptor.
		/// </summary>
		protected override void SessionEstablished()
		{
			lock(this)
			{
				Rijndael rijndael = Rijndael.Create();
				rijndael.Key = this.RijndaelKey;
				rijndael.Mode = CipherMode.ECB;

				if (this._encryptor == null)
				{
					this._encryptor = rijndael.CreateEncryptor();
					this._decryptor = rijndael.CreateDecryptor();
				}

				// LOG:
				BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;
				if (binaryLogWriter != null && binaryLogWriter[LogCategory.Security] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Security, "SecuritySession_SelfEstablishingSymmetric.SessionEstablished",
						LogMessageType.SecuritySessionKey, null, null, this.Remote, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, this,
						this.Name, -1, 
						0, 0, 0, "Encryption with " + rijndael.GetType().ToString(), null, null, null,
						"Security Session security information is initialized.");
				}
			}

			base.SessionEstablished();
		}

	}
}
