/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Remoting.Channels;
using System.Threading;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Serialization;

using Belikov.GenuineChannels;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Utilities;

namespace Belikov.GenuineChannels.Security.ZeroProofAuthorization
{
	/// <summary>
	/// Implements Zero Proof Authorization Client Security Session.
	/// </summary>
	public class SecuritySession_ZpaClient : SecuritySession_BaseZpaSession
	{
		/// <summary>
		/// Constructs an instance of the SecuritySession_ZpaClient class.
		/// </summary>
		/// <param name="name">Name of the SecuritySession being created.</param>
		/// <param name="remote">The remote host.</param>
		/// <param name="keyProvider_ZpaClient">The ZPA Client Key Provider containing a login and password.</param>
		public SecuritySession_ZpaClient(string name, HostInformation remote, KeyProvider_ZpaClient keyProvider_ZpaClient)
			: base(name, remote, keyProvider_ZpaClient.ZpaFeatureFlags)
		{
			this._keyProvider_ZpaClient = keyProvider_ZpaClient;
		}

		/// <summary>
		/// The ZPA Client Key Provider containing login and password.
		/// </summary>
		public KeyProvider_ZpaClient KeyProvider_ZpaClient
		{
			get
			{
				return this._keyProvider_ZpaClient;
			}
		}
		private KeyProvider_ZpaClient _keyProvider_ZpaClient;

		/// <summary>
		/// Initiates or continues establishing of the Security Session.
		/// </summary>
		/// <param name="input">A null reference or an incoming stream.</param>
		/// <param name="connectionLevel">Indicates whether the Security Session operates on connection level.</param>
		/// <returns>A stream containing data for sending to the remote host or a null reference if Security Session is established.</returns>
		public override GenuineChunkedStream EstablishSession(Stream input, bool connectionLevel)
		{
			BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;
			bool passException = false;

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

			// write session is being established flag
			BinaryWriter binaryWriter = new BinaryWriter(outputStream);
			binaryWriter.Write((byte) 0);

			try
			{
				lock(this)
				{
					if (input == Stream.Null)
					{
						// start a new session
						binaryWriter.Write((byte) ZpaPacketStatusFlag.ForceInitialization);
						binaryFormatter.Serialize(outputStream, this.KeyProvider_ZpaClient.Login);
						return outputStream;
					}

					ZpaPacketStatusFlag zpaPacketStatusFlag = (ZpaPacketStatusFlag) input.ReadByte();

					// LOG:
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Security] > 0 )
					{
						binaryLogWriter.WriteEvent(LogCategory.Security, "SecuritySession_ZpaClient.EstablishSession",
							LogMessageType.SecuritySessionEstablishing, null, null, this.Remote, null, 
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
							this, this.Name, -1, 
							0, 0, 0, Enum.Format(typeof(ZpaPacketStatusFlag), zpaPacketStatusFlag, "g"), null, null, null,
							"ZPA Client Session is being established. Status: {0}.", Enum.Format(typeof(ZpaPacketStatusFlag), zpaPacketStatusFlag, "g"));
					}

					switch (zpaPacketStatusFlag)
					{
						case ZpaPacketStatusFlag.ExceptionThrown:
							Exception receivedException = GenuineUtility.ReadException(input);

#if DEBUG
//							this.Remote.ITransportContext.IEventLogger.Log(LogMessageCategory.Security, receivedException, "SecuritySession_ZpaClient.EstablishSession",
//								null, "Zero Proof Authorization ends up with an exception at the remote host. Remote host: {0}.",
//								this.Remote.ToString());
#endif

							if (this.Remote != null)
								this.Remote.ITransportContext.IGenuineEventProvider.Fire(new GenuineEventArgs(
									GenuineEventType.SecuritySessionFailed, receivedException, this.Remote, this));

							this.DispatchException(receivedException);
							passException = true;
							throw receivedException;

						case ZpaPacketStatusFlag.ForceInitialization:
						case ZpaPacketStatusFlag.HashedPassword:
							throw GenuineExceptions.Get_Processing_LogicError(
								string.Format("The remote host must have the Security Session of the type SecuritySession_ZpaClient registered with the name {0}. SecuritySession_ZpaClient never sends {1} packet marker.",
								this.Name, Enum.GetName(typeof(ZpaPacketStatusFlag), zpaPacketStatusFlag)));

						case ZpaPacketStatusFlag.Salt:
							this.Salt = (byte[]) binaryFormatter.Deserialize(input);

							// server may immediately start sending the content
							this.SetupSecurityAlgorithms(this.KeyProvider_ZpaClient.Password);

							// the sault is received, sends the login and hashed password
							binaryWriter.Write((byte) ZpaPacketStatusFlag.HashedPassword);
							binaryFormatter.Serialize(outputStream, this.KeyProvider_ZpaClient.Login);
							binaryFormatter.Serialize(outputStream, ZeroProofAuthorizationUtility.CalculateDefaultKeyedHash(this.KeyProvider_ZpaClient.Password, this.Salt));
							return outputStream;

						case ZpaPacketStatusFlag.SessionEstablished:
							this.SessionEstablished();
							return null;
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

				binaryWriter.Write((byte) ZpaPacketStatusFlag.ExceptionThrown);
				binaryFormatter.Serialize(outputStream, opEx);
				return outputStream;
			}

			return null;
		}

		/// <summary>
		/// Initializes encryptor and decryptor.
		/// </summary>
		protected override void SessionEstablished()
		{
			base.SetupSecurityAlgorithms(this.KeyProvider_ZpaClient.Password);
			base.SessionEstablished();
		}

	}
}
