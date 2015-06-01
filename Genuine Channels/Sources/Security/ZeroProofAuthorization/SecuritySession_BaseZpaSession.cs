/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security;
using System.Security.Cryptography;
using System.Threading;

using Belikov.GenuineChannels.BufferPooling;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Utilities;

namespace Belikov.GenuineChannels.Security.ZeroProofAuthorization
{
	/// <summary>
	/// Implements basic functionality for Zero Proof Authorization Security Sessions.
	/// </summary>
	public abstract class SecuritySession_BaseZpaSession : SecuritySession
	{
		/// <summary>
		/// Constructs an instance of the SecuritySession_BaseZpaSession class.
		/// </summary>
		/// <param name="name">Name of the SecuritySession being created.</param>
		/// <param name="remote">The remote host.</param>
		/// <param name="zpaFeatureFlags">The requested features.</param>
		public SecuritySession_BaseZpaSession(string name, HostInformation remote, ZpaFeatureFlags zpaFeatureFlags)
			: base(name, remote)
		{
			this.ZpaFeatureFlags = zpaFeatureFlags;
		}

		/// <summary>
		/// The symmetric algorithm being used for encryption.
		/// </summary>
		public SymmetricAlgorithm SymmetricAlgorithm;

		/// <summary>
		/// Cryptographic encryptor.
		/// </summary>
		private ICryptoTransform _encryptor;

		/// <summary>
		/// Cryptographic decryptor.
		/// </summary>
		private ICryptoTransform _decryptor;

		/// <summary>
		/// The hash algorithm.
		/// </summary>
		public KeyedHashAlgorithm KeyedHashAlgorithm;

		/// <summary>
		/// The salt.
		/// </summary>
		public byte[] Salt;

		/// <summary>
		/// The requested security options.
		/// </summary>
		public ZpaFeatureFlags ZpaFeatureFlags;

        /// <summary>
        /// Encrypts the message data and put a result into the specified output stream.
        /// </summary>
        /// <param name="input">The stream containing the serialized message.</param>
        /// <param name="output">The result stream with the data being sent to the remote host.</param>
        public override void Encrypt(Stream input, GenuineChunkedStream output)
        {
#if DEBUG
            input.Position = 0;
#endif

            output.WriteByte(1);

            lock (this)
            {
                // write encrypted content
                if (this._encryptor != null)
                    GenuineUtility.CopyStreamToStream(new CryptoStream(new FinishReadingStream(input), this._encryptor, CryptoStreamMode.Read), output);
                else
                    output.WriteStream(input);

                if (this.KeyedHashAlgorithm != null)
                {
                    // and write down the calculated message hash
                    input.Position = 0;
                    output.WriteBuffer(this.KeyedHashAlgorithm.ComputeHash(input), -1);

                    // it's in the content, reset its position
                    if (this._encryptor == null)
                        input.Position = 0;
                }
            }
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

            lock (this)
            {
                GenuineChunkedStream output = new GenuineChunkedStream(false);
                byte[] sign = null;

                int signSize = 0;
                if (this.KeyedHashAlgorithm != null)
                    signSize = this.KeyedHashAlgorithm.HashSize / 8;

                // decrypt the content and fetch the sign
                if (this._decryptor != null)
                {
                    CryptoStream cryptoStream = new CryptoStream(new FinishReadingStream(output), this._decryptor, CryptoStreamMode.Write);
                    sign = GenuineUtility.CopyStreamToStreamExceptSign(input, cryptoStream, signSize);
                    cryptoStream.FlushFinalBlock();
                }
                else
                    sign = GenuineUtility.CopyStreamToStreamExceptSign(input, output, signSize);

                // check the sign
                if (this.KeyedHashAlgorithm != null)
                {
                    if (!ZeroProofAuthorizationUtility.CompareBuffers(sign, this.KeyedHashAlgorithm.ComputeHash(output), sign.Length))
                        throw GenuineExceptions.Get_Security_WrongSignature();
                    output.Position = 0;
                }

                output.ReleaseOnReadMode = true;
                return output;
            }
        }

		/// <summary>
		/// Sets up all security stuff for encrypting content and checking integrity.
		/// </summary>
		/// <param name="password">The password.</param>
		protected void SetupSecurityAlgorithms(string password)
		{
			lock(this)
			{
				if ((this.ZpaFeatureFlags & ZpaFeatureFlags.ElectronicCodebookEncryption) != 0 
					|| (this.ZpaFeatureFlags & ZpaFeatureFlags.CipherBlockChainingEncryption) != 0)
				{
					// encryption 
                    this.SymmetricAlgorithm = Rijndael.Create();
					this.SymmetricAlgorithm.Key = ZeroProofAuthorizationUtility.GeneratePasswordBasedSequence("Key" + password, this.Salt, 32);                    
					this.SymmetricAlgorithm.IV = ZeroProofAuthorizationUtility.GeneratePasswordBasedSequence("IV" + password, this.Salt, 16);

					this.SymmetricAlgorithm.Mode = (this.ZpaFeatureFlags & ZpaFeatureFlags.ElectronicCodebookEncryption) != 0 ? CipherMode.ECB : CipherMode.CBC;

					this._encryptor = this.SymmetricAlgorithm.CreateEncryptor();
					this._decryptor = this.SymmetricAlgorithm.CreateDecryptor();
				}

				// and integrity checking
				if ( (this.ZpaFeatureFlags & ZpaFeatureFlags.Mac3DesCbcSigning) != 0)
					this.KeyedHashAlgorithm = MACTripleDES.Create();
				if ( (this.ZpaFeatureFlags & ZpaFeatureFlags.HmacSha1Signing) != 0 )
					this.KeyedHashAlgorithm = HMACSHA1.Create();

				if (this.KeyedHashAlgorithm != null)
					this.KeyedHashAlgorithm.Key = ZeroProofAuthorizationUtility.GeneratePasswordBasedSequence("M3D" + password, this.Salt, 24);

				// LOG:
				BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;
				if (binaryLogWriter != null && binaryLogWriter[LogCategory.Security] > 0 )
				{
					binaryLogWriter.WriteEvent(LogCategory.Security, "SecuritySession_BaseZpaSession.SetupSecurityAlgorithms",
						LogMessageType.SecuritySessionKey, null, null, this.Remote, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, this,
						this.Name, -1, 
						0, 0, 0, string.Format("Zero Proof Authorization Flags: {0} Encryption: {1} Data Integrity: {2}", Enum.Format(typeof(ZpaFeatureFlags), this.ZpaFeatureFlags, "g"), this.SymmetricAlgorithm == null ? "No" : this.SymmetricAlgorithm.GetType().ToString(), this.KeyedHashAlgorithm == null ? "No" : this.KeyedHashAlgorithm.GetType().ToString()),
						null, null, null,
						"Security Session security information is initialized.");
				}

			}
		}

	}
}
