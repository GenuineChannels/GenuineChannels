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
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.TransportContext;
using Zyan.SafeDeserializationHelpers;

namespace Belikov.GenuineChannels.Security.ZeroProofAuthorization
{
	/// <summary>
	/// Implements Zero Proof Authorization Server Security Session.
	/// </summary>
	public class SecuritySession_ZpaServer : SecuritySession_BaseZpaSession
	{
		/// <summary>
		/// Constructs an instance of the SecuritySession_ZpaServer class.
		/// </summary>
		/// <param name="name">Name of the SecuritySession being created.</param>
		/// <param name="remote">The remote host.</param>
		/// <param name="keyProvider_ZpaServer">The server key provider.</param>
		public SecuritySession_ZpaServer(string name, HostInformation remote, KeyProvider_ZpaServer keyProvider_ZpaServer)
			: base(name, remote, keyProvider_ZpaServer.ZpaFeatureFlags)
		{
			this.KeyProvider_ZpaServer = keyProvider_ZpaServer;
			Random random = new Random();
			this.Salt = ZeroProofAuthorizationUtility.GenerateArbitrarySequence(128 + random.Next(128));
		}

		/// <summary>
		/// The key provider.
		/// </summary>
		public KeyProvider_ZpaServer KeyProvider_ZpaServer;

		/// <summary>
		/// The login provided by the client host.
		/// </summary>
		public object Login
		{
			get
			{
				return this._login;
			}
		}
		private object _login;


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
					ZpaPacketStatusFlag zpaPacketStatusFlag;

					if (input == Stream.Null)
						zpaPacketStatusFlag = ZpaPacketStatusFlag.ForceInitialization;
					else
						zpaPacketStatusFlag = (ZpaPacketStatusFlag) input.ReadByte();

					// LOG:
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.Security] > 0 )
					{
						binaryLogWriter.WriteEvent(LogCategory.Security, "SecuritySession_ZpaServer.EstablishSession",
							LogMessageType.SecuritySessionEstablishing, null, null, this.Remote, null,
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
							this, this.Name, -1,
							0, 0, 0, Enum.Format(typeof(ZpaPacketStatusFlag), zpaPacketStatusFlag, "g"), null, null, null,
							"ZPA Server Session is being established. Status: {0}.", Enum.Format(typeof(ZpaPacketStatusFlag), zpaPacketStatusFlag, "g"));
					}

					switch (zpaPacketStatusFlag)
					{
						case ZpaPacketStatusFlag.ExceptionThrown:
							Exception receivedException = GenuineUtility.ReadException(input);

#if DEBUG
//							this.Remote.ITransportContext.IEventLogger.Log(LogMessageCategory.Security, receivedException, "SecuritySession_ZpaServer.EstablishSession",
//								null, "Zero Proof Authorization ends up with an exception at the remote host. Remote host: {0}.",
//								this.Remote.ToString());
#endif

							this.Remote.ITransportContext.IGenuineEventProvider.Fire(new GenuineEventArgs(
								GenuineEventType.SecuritySessionFailed, receivedException, this.Remote, this));

							this.DispatchException(receivedException);
							passException = true;
							throw receivedException;

						case ZpaPacketStatusFlag.ForceInitialization:
							// send the sault to the remote host
							binaryWriter.Write((byte) ZpaPacketStatusFlag.Salt);
							binaryFormatter.Serialize(outputStream, this.Salt);
							return outputStream;

						case ZpaPacketStatusFlag.HashedPassword:
							// the the password
							this._login = binaryFormatter.Deserialize(input);
							string password = this.KeyProvider_ZpaServer.IAuthorizationManager.GetPassword(this._login);

							// and check on the hash
							byte[] calculatedHash = (byte[]) binaryFormatter.Deserialize(input);
							byte[] expectedHash = ZeroProofAuthorizationUtility.CalculateDefaultKeyedHash(password, this.Salt);

							if (! ZeroProofAuthorizationUtility.CompareBuffers(calculatedHash, expectedHash, expectedHash.Length))
								throw GenuineExceptions.Get_Security_PasswordKnowledgeIsNotProved();

							// ok the hash is correct. Complete the authorization

							this.SetupSecurityAlgorithms(password);
							this.SessionEstablished();

							binaryWriter.Write((byte) ZpaPacketStatusFlag.SessionEstablished);
							return outputStream;

						case ZpaPacketStatusFlag.Salt:
						case ZpaPacketStatusFlag.SessionEstablished:
							throw GenuineExceptions.Get_Processing_LogicError(
								string.Format("The remote host must have the Security Session of the type SecuritySession_ZpaServer registered with the name {0}. SecuritySession_ZpaServer never sends {1} packet marker.",
								this.Name, Enum.GetName(typeof(ZpaPacketStatusFlag), zpaPacketStatusFlag)));
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

	}
}
