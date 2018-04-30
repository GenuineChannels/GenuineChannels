/* Genuine Channels product.
 *
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 *
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Utilities;
using Zyan.SafeDeserializationHelpers;

namespace Belikov.GenuineChannels.Security.SSPI
{
	/// <summary>
	/// Implements client-side SSPI Security Session that
	/// establishes SSPI security context and can encrypt and/or sign content.
	/// </summary>
	public class SecuritySession_SspiClient : SecuritySession
	{
		/// <summary>
		/// Constructs an instance of the SecuritySession_SspiClient class.
		/// </summary>
		/// <param name="name">The name of the Security Session.</param>
		/// <param name="remote">The remote host.</param>
		/// <param name="keyProvider_SspiClient">Parent KeyProvider_SspiClient instance to get settings from.</param>
		public SecuritySession_SspiClient(string name, HostInformation remote, KeyProvider_SspiClient keyProvider_SspiClient)
			: base(name, remote)
		{
			this.KeyProvider_SspiClient = keyProvider_SspiClient;
		}

		/// <summary>
		/// Parent KeyProvider_SspiClient instance to get settings from.
		/// </summary>
		public KeyProvider_SspiClient KeyProvider_SspiClient;

		/// <summary>
		/// SSPI security context.
		/// </summary>
		public SspiClientSecurityContext SspiSecurityContext;

		/// <summary>
		/// Initialization process will be restarted if authentication was not finished within this
		/// time span.
		/// </summary>
		public static TimeSpan MaxSpanToPerformAuthentication = TimeSpan.FromMinutes(3);

		#region -- Establish security context ------------------------------------------------------

		/// <summary>
		/// Initiates or continues establishing of the Security Session.
		/// Implementation notes: receiving of exceptions no more breaks up the connection like
		/// it was in the previous versions.
		/// </summary>
		/// <param name="input">A null reference or an incoming stream.</param>
		/// <param name="connectionLevel">Indicates whether the Security Session operates on connection level.</param>
		/// <returns>A stream containing data for sending to the remote host or a null reference if Security Session is established.</returns>
		public override GenuineChunkedStream EstablishSession(Stream input, bool connectionLevel)
		{
			bool passException = false;

			// a dance is over
			if (this.IsEstablished)
				return null;

			GenuineChunkedStream outputStream = null;
			var binaryFormatter = new BinaryFormatter().Safe();

			// skip the status flag
			if (connectionLevel)
			{
				if (input != null)
					input.ReadByte();
				outputStream = new GenuineChunkedStream(false);
			}
			else
				outputStream = this.CreateOutputStream();

			// write session is being established flag
			BinaryWriter binaryWriter = new BinaryWriter(outputStream);
			binaryWriter.Write((byte) 0);

			try
			{
				lock(this)
				{
					if (input == Stream.Null)
					{
						if (this.KeyProvider_SspiClient.DelegatedContext != null)
						{
							try
							{
								SspiApi.ImpersonateSecurityContext(this.KeyProvider_SspiClient.DelegatedContext.SspiSecurityContext._phContext);

								// start new session
								this.SspiSecurityContext = new SspiClientSecurityContext(this.KeyProvider_SspiClient);
								binaryWriter.Write((byte) SspiPacketStatusFlags.InitializeFromScratch);
								this.SspiSecurityContext.BuildUpSecurityContext(null, outputStream);
								return outputStream;
							}
							finally
							{
								SspiApi.RevertSecurityContext(this.KeyProvider_SspiClient.DelegatedContext.SspiSecurityContext._phContext);
							}
						}

						// start new session
						this.SspiSecurityContext = new SspiClientSecurityContext(this.KeyProvider_SspiClient);
						binaryWriter.Write((byte) SspiPacketStatusFlags.InitializeFromScratch);
						this.SspiSecurityContext.BuildUpSecurityContext(null, outputStream);
						return outputStream;
					}

					SspiPacketStatusFlags sspiPacketStatusFlags = (SspiPacketStatusFlags) input.ReadByte();
					switch (sspiPacketStatusFlags)
					{
						case SspiPacketStatusFlags.ContinueAuthentication:
							// continue building a security context
							GenuineChunkedStream sspiData = new GenuineChunkedStream(false);
							this.SspiSecurityContext.BuildUpSecurityContext(input, sspiData);

							if (sspiData.Length == 0)
							{
								// SSPI session has been built up
								outputStream.WriteByte((byte) SspiPacketStatusFlags.SessionEstablished);
								this.SessionEstablished();
							}
							else
							{
								outputStream.WriteByte((byte) SspiPacketStatusFlags.ContinueAuthentication);
								outputStream.WriteStream(sspiData);
							}
							return outputStream;

						case SspiPacketStatusFlags.ExceptionThrown:
							Exception receivedException = GenuineUtility.ReadException(input);

#if DEBUG
//							this.Remote.ITransportContext.IEventLogger.Log(LogMessageCategory.Security, receivedException, "SecuritySession_SspiServer.EstablishSession",
//								null, "SSPI initialization ends up with an exception at the remote host. Remote host: {0}.",
//								this.Remote.ToString());
#endif

							if (this.Remote != null)
								this.Remote.ITransportContext.IGenuineEventProvider.Fire(new GenuineEventArgs(
									GenuineEventType.SecuritySessionFailed, receivedException, this.Remote, this));

							this.DispatchException(receivedException);
							passException = true;
							throw receivedException;

						case SspiPacketStatusFlags.ForceInitialization:
							return this.EstablishSession(Stream.Null, connectionLevel);

						case SspiPacketStatusFlags.InitializeFromScratch:
							throw GenuineExceptions.Get_Processing_LogicError(
								string.Format("The remote host must have the Security Session of the type SecuritySession_SspiServer registered with the name {0}. SecuritySession_SspiServer never sends SspiPacketMark.InitializeFromScratch packet marker.",
								this.Name));

						case SspiPacketStatusFlags.SessionEstablished:
							this.SessionEstablished();
							break;
					}
				}
			}
			catch(Exception opEx)
			{
#if DEBUG
//				this.Remote.ITransportContext.IEventLogger.Log(LogMessageCategory.Security, opEx, "SecuritySession_SspiServer.EstablishSession",
//					null, "Exception was thrown while establishing security context.");
#endif

				if (this.Remote != null)
					this.Remote.ITransportContext.IGenuineEventProvider.Fire(new GenuineEventArgs(
						GenuineEventType.SecuritySessionFailed, opEx, this.Remote, this));

				this.DispatchException(opEx);
				if (passException)
					throw;

				binaryWriter.Write((byte) SspiPacketStatusFlags.ExceptionThrown);
				binaryFormatter.Serialize(outputStream, opEx);
				return outputStream;
			}

			return null;
		}

		#endregion

		#region -- Cryptography --------------------------------------------------------------------

		/// <summary>
		/// Encrypts the message data and put a result into the specified output stream.
		/// </summary>
		/// <param name="input">The stream containing the serialized message.</param>
		/// <param name="output">The result stream with the data being sent to the remote host.</param>
		public override void Encrypt(Stream input, GenuineChunkedStream output)
		{
			// write session established flag
			output.WriteByte(1);

			// serialize messages into separate stream
			this.SspiSecurityContext.EncryptMessage(input, output, this.KeyProvider_SspiClient.RequiredFeatures);
		}

		/// <summary>
		/// Creates and returns a stream containing decrypted data.
		/// </summary>
		/// <param name="input">A stream containing encrypted data.</param>
		/// <returns>A stream with decrypted data.</returns>
		public override Stream Decrypt(Stream input)
		{
			if (input.ReadByte() == 0)
			{
				Stream outputStream = this.EstablishSession(input, false);
				if (outputStream != null)
					GenuineThreadPool.QueueUserWorkItem(new WaitCallback(this.SendMessage), outputStream, false);
				return null;
			}

			return this.SspiSecurityContext.DecryptMessage(input, this.KeyProvider_SspiClient.RequiredFeatures);
		}

		#endregion

	}
}
