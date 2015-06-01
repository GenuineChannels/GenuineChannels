/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.IO;
using System.Runtime.InteropServices;

using Belikov.GenuineChannels.Messaging;

namespace Belikov.GenuineChannels.Security.SSPI
{
	/// <summary>
	/// SspiSecurityContext.
	/// </summary>
	public abstract class SspiSecurityContext : IDisposable
	{
		/// <summary>
		/// Constructs an instance of the SspiSecurityContext class.
		/// </summary>
		public SspiSecurityContext()
		{
		}

		/// <summary>
		/// Releases resources.
		/// </summary>
		~SspiSecurityContext()
		{
			this.Dispose();
		}

		/// <summary>
		/// Credential handle obtained via AcquireCredentialsHandle call.
		/// </summary>
		public SspiApi.SecHandle _credHandle = new SspiApi.SecHandle();

		/// <summary>
		/// Security context lifetime span.
		/// </summary>
		public Int64 _ptsExpiry = 0;

		/// <summary>
		/// Established security context handle.
		/// </summary>
		public SspiApi.SecHandle _phContext = new SspiApi.SecHandle();

		#region IDisposable Members

		private bool _disposed = false;

		/// <summary>
		/// Releases all acquired SSPI resources.
		/// </summary>
		public void Dispose()
		{
			if (this._disposed)
				return ;

			this._disposed = true;

#if TRIAL
#else
			SspiApi.DeleteSecurityContext(this._phContext);
			SspiApi.FreeCredentialsHandle(this._credHandle);
#endif
		}

		#endregion

		/// <summary>
		/// SSPI package size informations.
		/// </summary>
		private SspiApi.SecPkgContext_Sizes _secPkgContext_Sizes = new SspiApi.SecPkgContext_Sizes();
		private bool _secPkgContext_SizesInitialized = false;
		private object _secPkgContext_SizesLock = new object();

		/// <summary>
		/// Encrypts the stream under current security conditions.
		/// </summary>
		/// <param name="messageStream">Data to be encrypted.</param>
		/// <param name="outputStream">Stream to write encrypted content to.</param>
		/// <param name="sspiFeatureFlags">Requested features.</param>
		public void EncryptMessage(Stream messageStream, GenuineChunkedStream outputStream, SspiFeatureFlags sspiFeatureFlags)
		{
			// get package sizes
			lock(_secPkgContext_SizesLock)
			{
				if (! _secPkgContext_SizesInitialized)
				{
					SspiApi.QueryContextSizes(this._phContext, ref this._secPkgContext_Sizes);
					_secPkgContext_SizesInitialized = true;
				}
			}

			byte[] chunk = null;
			int position = 0;
			BinaryWriter outputWriter = new BinaryWriter(outputStream);

			if ( (sspiFeatureFlags & SspiFeatureFlags.Encryption) != 0)
				// it'll write signature automatically as well as encrypt content
				SspiApi.EncryptMessage(this._phContext, messageStream, outputWriter, ref this._secPkgContext_Sizes);
			else if ( (sspiFeatureFlags & SspiFeatureFlags.Signing) != 0)
			{
				// remember position to write signature size later
				outputStream.WriteInt32AndRememberItsLocation(0, out chunk, out position);
				long currentLength = outputStream.Length;

				// anyway will have to read this into buffer
				byte[] contentBuffer = new byte[(int) messageStream.Length];
				GenuineUtility.ReadDataFromStream(messageStream, contentBuffer, 0, contentBuffer.Length);

				// write signature
				SspiApi.MakeSignature(this._phContext, contentBuffer, outputWriter, ref this._secPkgContext_Sizes);

				// update signature size
				MessageCoder.WriteInt32(chunk, position, (int) (outputStream.Length - currentLength) );

				// write the content
				outputWriter.Write( (int) contentBuffer.Length );
				outputWriter.Write( contentBuffer, 0, contentBuffer.Length);
			}
			else
			{
				// just copy the source content
				//outputWriter.Write( (int) messageStream.Length );
				GenuineUtility.CopyStreamToStream(messageStream, outputStream);
			}
		}

		/// <summary>
		/// Decrypts the data.
		/// </summary>
		/// <param name="sourceStream">Stream containing encrypted data.</param>
		/// <param name="sspiFeatureFlags">Requested SSPI features.</param>
		/// <returns>Stream containing decrypted data.</returns>
		public Stream DecryptMessage(Stream sourceStream, SspiFeatureFlags sspiFeatureFlags)
		{
			// get package sizes
			lock(_secPkgContext_SizesLock)
			{
				if (! _secPkgContext_SizesInitialized)
				{
					SspiApi.QueryContextSizes(this._phContext, ref this._secPkgContext_Sizes);
					_secPkgContext_SizesInitialized = true;
				}
			}

			BinaryReader messageReader = new BinaryReader(sourceStream);

			if ( (sspiFeatureFlags & SspiFeatureFlags.Encryption) != 0)
				// decrypt it
				return SspiApi.DecryptMessage(this._phContext, messageReader);
			else if ( (sspiFeatureFlags & SspiFeatureFlags.Signing) != 0)
			{
				// read signature
				int signatureSize = messageReader.ReadInt32();
				byte[] signature = new byte[signatureSize];
				GenuineUtility.ReadDataFromStream(sourceStream, signature, 0, signatureSize);

				// read content
				int contentSize = messageReader.ReadInt32();
				byte[] content = new byte[contentSize];
				GenuineUtility.ReadDataFromStream(sourceStream, content, 0, contentSize);

				// verify signature
				SspiApi.VerifySignature(this._phContext, content, signature);

				// return verified content
				return new MemoryStream(content, 0, content.Length, false, true);
			}

			return sourceStream;
		}
	}
}
