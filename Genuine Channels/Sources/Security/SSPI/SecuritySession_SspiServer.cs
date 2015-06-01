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
using System.Security.Principal;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Remoting.Channels;
using System.Threading;

using Belikov.GenuineChannels;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Utilities;

namespace Belikov.GenuineChannels.Security.SSPI
{
	/// <summary>
	/// Implements server-side SSPI security session that provides
	/// authentication, encryption and impersonation security features.
	/// </summary>
	public class SecuritySession_SspiServer : SecuritySession
	{
		/// <summary>
		/// Constructs an instance of the SecuritySession_SspiClient class.
		/// </summary>
		/// <param name="name">Name of the SecuritySession being created.</param>
		/// <param name="remote">The remote host.</param>
		/// <param name="keyProvider_SspiServer">Parent KeyProvider_SspiServer instance to get settings from.</param>
		public SecuritySession_SspiServer(string name, HostInformation remote, KeyProvider_SspiServer keyProvider_SspiServer)
			: base(name, remote)
		{
			this.KeyProvider_SspiServer = keyProvider_SspiServer;
		}

		/// <summary>
		/// The parent KeyProvider_SspiClient instance to get settings from.
		/// </summary>
		public KeyProvider_SspiServer KeyProvider_SspiServer;

		/// <summary>
		/// SSPI security context.
		/// </summary>
		public SspiServerSecurityContext SspiSecurityContext;

		/// <summary>
		/// Gets a value representing WindowsIdentity corresponding to the established security context.
		/// </summary>
		public WindowsIdentity WindowsIdentity
		{
			get
			{
				return SspiApi.QuerySecurityContextToken(this.SspiSecurityContext._phContext);
			}
		}

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
						// request establishing the Security Session
						binaryWriter.Write((byte) SspiPacketStatusFlags.ForceInitialization);
						return outputStream;
					}

					SspiPacketStatusFlags sspiPacketStatusFlags = (SspiPacketStatusFlags) input.ReadByte();
					switch (sspiPacketStatusFlags)
					{
						case SspiPacketStatusFlags.InitializeFromScratch:
						case SspiPacketStatusFlags.ContinueAuthentication:
							if (sspiPacketStatusFlags == SspiPacketStatusFlags.InitializeFromScratch)
								this.SspiSecurityContext = new SspiServerSecurityContext(this.KeyProvider_SspiServer);

							// continue building a security context
							GenuineChunkedStream sspiData = new GenuineChunkedStream(false);
							this.SspiSecurityContext.BuildUpSecurityContext(input, sspiData);
 
							if (sspiData.Length == 0)
							{
								// SSPI session is built up
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
							Exception receivedException = this.ReadException(input);

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
							throw GenuineExceptions.Get_Processing_LogicError(
								string.Format("The remote host must have the Security Session of the type SecuritySession_SspiClient registered with the name {0}. SecuritySession_SspiClient never sends SspiPacketMark.ForceInitialization packet mark.",
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

		/// <summary>
		/// Deserializes an exception from the stream and throws it.
		/// </summary>
		/// <param name="stream">Stream containing serialized exception.</param>
		private Exception ReadException(Stream stream)
		{
			BinaryFormatter binaryFormatter = new BinaryFormatter();
			return binaryFormatter.Deserialize(stream) as Exception;
		}

		/// <summary>
		/// Is used to serialize all security algorithms into a single stream.
		/// Only for debug purposes.
		/// </summary>
		public class _Debugging_SspiServer_SecurityAlgorithm
		{
			/// <summary>
			/// The type of authentication used to identify the user.
			/// </summary>
			public string AuthenticationType;

			/// <summary>
			/// A value indicating whether the user account is identified as an anonymous account by the system.
			/// </summary>
			public bool IsAnonymous;

			/// <summary>
			/// A value indicating whether the user has been authenticated by Windows.
			/// </summary>
			public bool IsAuthenticated;

			/// <summary>
			/// A value indicating whether the user account is identified as a Guest account by the system.
			/// </summary>
			public bool IsGuest;

			/// <summary>
			/// A value indicating whether the user account is identified as a System account by the system.
			/// </summary>
			public bool IsSystem;

			/// <summary>
			/// The user's Windows logon name.
			/// </summary>
			public string Name;
		}

		/// <summary>
		/// Informs all dependent entities that the Security Session has been established.
		/// </summary>
		protected override void SessionEstablished()
		{
			// LOG:
			BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;
			if (binaryLogWriter != null && binaryLogWriter[LogCategory.Security] > 0 )
			{
				WindowsIdentity windowsIdentity = this.WindowsIdentity;

				binaryLogWriter.WriteEvent(LogCategory.Security, "SecuritySession_SspiServer.SessionEstablished",
					LogMessageType.SecuritySessionKey, null, null, this.Remote, null, 
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, this,
					this.Name, -1, 
					0, 0, 0, 
					string.Format("SSPI Package: {0} Features: {1} Win Auth Type: {2} IsAnonymous: {3} IsGuest: {4} IsSystem: {5} Name: {6}", 
						this.KeyProvider_SspiServer.PackageName, Enum.Format(typeof(SspiFeatureFlags), this.KeyProvider_SspiServer.RequiredFeatures, "g"), 
						windowsIdentity.AuthenticationType,
						windowsIdentity.IsAnonymous, windowsIdentity.IsGuest, windowsIdentity.IsSystem, windowsIdentity.Name),
					null, null, null,
					"Security Session security information is initialized.");
			}

			this._isEstablished.Set();
			SendAssociatedMessages();
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

			// serialize messages into the output stream
			this.SspiSecurityContext.EncryptMessage(input, output, this.KeyProvider_SspiServer.RequiredFeatures);
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

			return this.SspiSecurityContext.DecryptMessage(input, this.KeyProvider_SspiServer.RequiredFeatures);
		}

		/// <summary>
		/// Sets up correct security context and invokes a target.
		/// This method may not throw any exceptions.
		/// </summary>
		/// <param name="message">The message to be performed.</param>
		/// <param name="connectionLevel">Indicates whether Security Session is used on the connection level.</param>
		public override void Invoke(Message message, bool connectionLevel)
		{
			bool impersonateContext = (this.KeyProvider_SspiServer.RequiredFeatures & SspiFeatureFlags.Impersonation) != 0 ||
				(this.KeyProvider_SspiServer.RequiredFeatures & SspiFeatureFlags.Delegation) != 0;

			try
			{
				if (impersonateContext)
					SspiApi.ImpersonateSecurityContext(this.SspiSecurityContext._phContext);

				base.Invoke(message, connectionLevel);
			}
			finally
			{
				if (impersonateContext)
					SspiApi.RevertSecurityContext(this.SspiSecurityContext._phContext);
			}
		}


		#endregion

	}
}
