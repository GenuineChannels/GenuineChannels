/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;

using Belikov.Common.ThreadProcessing;

using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Parameters;
using Belikov.GenuineChannels.Security;

namespace Belikov.GenuineChannels.TransportContext
{
	/// <summary>
	/// Keeps information regarding the specific host (not necessarily remote) and provides all the
	/// necessary means for releasing this information after a connection to this host
	/// is closed.
	/// All properties and methods are thread-safe.
	/// </summary>
	public class HostInformation : MarshalByRefObject, ISetSecuritySession, ISessionSupport, ITransportContextProvider
	{
		/// <summary>
		/// Constructs an instance of the HostInformation class.
		/// Instances of the class HostInformation must be constructed only by objects implementing 
		/// the IKnownHosts interface.
		/// </summary>
		/// <param name="uriOrUrl">Uri or Url of the remote host.</param>
		/// <param name="iTransportContext">Transport Context.</param>
		internal HostInformation(string uriOrUrl, ITransportContext iTransportContext)
		{
			this._iTransportContext = iTransportContext;

			if (uriOrUrl[0] == '_')
			{
				this._uri = uriOrUrl;
				UriStorage.RegisterConnection(uriOrUrl, this.ITransportContext);
			}
			else
				this._url = uriOrUrl;
		}

		/// <summary>
		/// To guarantee atomic access to local members.
		/// </summary>
		private object _accessToLocalMembers = new object();

		/// <summary>
		/// Transport Context.
		/// </summary>
		public ITransportContext ITransportContext
		{
			get
			{
				return this._iTransportContext;
			}
		}
		private ITransportContext _iTransportContext;

		/// <summary>
		/// Host's identifier.
		/// </summary>
		public string Uri 
		{ 
			get
			{
				lock (this._uriLock)
					return _uri;
			} 
		}
		private string _uri;
		private object _uriLock = new object();

		/// <summary>
		/// The physical address of the remote host.
		/// </summary>
		public object PhysicalAddress
		{
			get
			{
				lock (this._accessToLocalMembers)
					return this._physicalAddress;
			}
			set
			{
				lock (this._accessToLocalMembers)
					this._physicalAddress = value;
			}
		}
		private object _physicalAddress;

		/// <summary>
		/// Gets or sets an integer value that reflects the number of messages in the connection queue.
		/// Please note that if you have several persistent and/or named connections leading to the same host, this 
		/// value will reflect the queue length of the last used connection.
		/// </summary>
		public int QueueLength
		{
			get
			{
				lock (this._accessToLocalMembers)
					return this._queueLength;
			}
			set
			{
				lock (this._accessToLocalMembers)
					this._queueLength = value;
			}
		}
		private int _queueLength;

		/// <summary>
		/// Gets or sets a byte value indicating the protocol version supported by the remote host.
		/// </summary>
		public byte ProtocolVersion
		{
			get
			{
				lock (this._accessToLocalMembers)
					return this._protocolVersion;
			}
			set
			{
				lock (this._accessToLocalMembers)
					this._protocolVersion = value;
			}
		}
		private byte _protocolVersion = MessageCoder.PROTOCOL_VERSION;

		/// <summary>
		/// The local end point being used to connect to the remote host.
		/// Is set only by GTCP Connection Manager.
		/// </summary>
		public object LocalPhysicalAddress
		{
			get
			{
				lock (this._accessToLocalMembers)
					return this._localPhysicalAddress;
			}
			set
			{
				lock (this._accessToLocalMembers)
					this._localPhysicalAddress = value;
			}
		}
		private object _localPhysicalAddress;

		/// <summary>
		/// Updates the value of the Uri member.
		/// </summary>
		/// <param name="uri">The uri of this host.</param>
		/// <param name="remoteHostUniqueIdentifier">The unique identifier of the HostInformation used by the remote host.</param>
		/// <returns>The exception explaining why the remote host has lost its state.</returns>
		public Exception UpdateUri(string uri, int remoteHostUniqueIdentifier)
		{
			return this.UpdateUri(uri, remoteHostUniqueIdentifier, true);
		}

		/// <summary>
		/// Updates the value of the Uri member.
		/// </summary>
		/// <param name="uri">The uri of this host.</param>
		/// <param name="remoteHostUniqueIdentifier">The unique identifier of the HostInformation used by the remote host.</param>
		/// <param name="checkHostInfoVersion">The boolean value that determines whether the host version should be checked.</param>
		/// <returns>The exception explaining why the remote host has lost its state.</returns>
		public Exception UpdateUri(string uri, int remoteHostUniqueIdentifier, bool checkHostInfoVersion)
		{
			Exception exception = null;
			BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;

			lock (this._uriLock)
			{
				if (this._uri != uri)
				{
					if (this._uri != null && this._uri != uri)
					{
						exception = GenuineExceptions.Get_Receive_ServerHasBeenRestared();

						// LOG:
						if ( binaryLogWriter != null && binaryLogWriter[LogCategory.HostInformation] > 0 )
							binaryLogWriter.WriteEvent(LogCategory.HostInformation, "HostInformation.UpdateUri",
								LogMessageType.HostInformationUriUpdated, exception, null, this, 
								null, GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
								null, null, -1, 0, 0, 0, this._uri, uri, null, null,
								"The URI mismatch has been detected. The remote host has been restarted. Expected uri: \"{0}\". Provided uri: \"{1}\".", this._uri, uri);

//						if (! (bool) this.ITransportContext.IParameterProvider[GenuineParameter.IgnoreRemoteHostUriChanges])
						throw exception;
					}

					this._uri = uri;
					UriStorage.RegisterConnection(uri, this.ITransportContext);
					this.ITransportContext.KnownHosts.UpdateHost(uri, this);

					// LOG:
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.HostInformation] > 0 )
						binaryLogWriter.WriteEvent(LogCategory.HostInformation, "HostInformation.UpdateUri",
							LogMessageType.HostInformationUriUpdated, null, null, this, 
							null, GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
							null, null, -1, 0, 0, 0, this._uri, uri, null, null,
							"The URI of the HostInformation has been updated. Provided uri: \"{0}\".", this._uri);
				}

				if (checkHostInfoVersion && this.RemoteHostUniqueIdentifier != remoteHostUniqueIdentifier || (this._uri != null && this._uri != uri))
				{
					if (this.RemoteHostUniqueIdentifier != -1)
					{
						exception = GenuineExceptions.Get_Receive_NewSessionDetected();

						// release all security sessions
						lock(_securitySessions)
						{
							foreach (DictionaryEntry entry in this._securitySessions)
							{
								SecuritySession securitySession = (SecuritySession) entry.Value;
								securitySession.DispatchException(exception);
							}

							this._securitySessions = new Hashtable();
						}

						this.ITransportContext.IIncomingStreamHandler.DispatchException(this, exception);
					}

					this._remoteHostUniqueIdentifier = remoteHostUniqueIdentifier;
					this.ITransportContext.IGenuineEventProvider.Fire(new GenuineEventArgs(GenuineEventType.GeneralNewSessionDetected, null, this, null));
				}
			}

			return exception;
		}

		/// <summary>
		/// Physical address of the remote host for establishing a network connection.
		/// </summary>
		public string Url 
		{ 
			get 
			{
				lock (_urlLock)
					return _url; 
			} 
		}
		private string _url;
		private object _urlLock = new object();

		/// <summary>
		/// Updates the value of the url member.
		/// </summary>
		/// <param name="url">URL of the remote host.</param>
		public void UpdateUrl(string url)
		{
			lock (this._urlLock)
			{
				if (this._url == url)
					return ;

				this._url = url;
				this.ITransportContext.KnownHosts.UpdateHost(url, this);
			}
		}

		/// <summary>
		/// Gets the URL if the remote host is a server or the URI if it is a client.
		/// </summary>
		public string PrimaryUri
		{
			get
			{
				return this.GenuinePersistentConnectionState == GenuinePersistentConnectionState.Opened ? this.Url : this.Uri;
			}
		}

		/// <summary>
		/// Gets the time when all information regarding the remote host 
		/// becomes invalid and is a subject for disposing.
		/// </summary>
		public int ExpireTime 
		{ 
			get 
			{ 
				lock (this._accessToLocalMembers)
					return this._expireTime;
			}
		}
		private int _expireTime = GenuineUtility.FurthestFuture;

		/// <summary>
		/// Renews the Security Session life time.
		/// The first call of this method is always reset the lifetime value to the specified value.
		/// </summary>
		/// <param name="timeSpan">A time period to renew all host-related information.</param>
		/// <param name="canMakeShorter">Indicates whether this call may reduce the host expiration time.</param>
		public void Renew(int timeSpan, bool canMakeShorter)
		{
			if (this._firstRenewing)
			{
				this._firstRenewing = false;
				canMakeShorter = true;
			}

			lock (this.DisposeLock)
			{
				if (this.IsDisposed)
					throw OperationException.WrapException(this.DisposeReason);

				lock (this._accessToLocalMembers)
				{
					int proposedTime = GenuineUtility.GetTimeout(timeSpan); 
					if (canMakeShorter || GenuineUtility.IsTimeoutExpired(this._expireTime, proposedTime))
						this._expireTime = proposedTime;
				}
			}
		}

		private bool _firstRenewing = true;

		/// <summary>
		/// Gets a Security Session.
		/// </summary>
		/// <param name="securitySessionName">The name of the Security Session.</param>
		/// <param name="keyStore">The store of all keys.</param>
		/// <returns>An object implementing ISecuritySession interface.</returns>
		public SecuritySession GetSecuritySession(string securitySessionName, IKeyStore keyStore)
		{
			lock (this.DisposeLock)
			{
				if (this.IsDisposed)
					throw OperationException.WrapException(this.DisposeReason);

				lock(_securitySessions)
				{
					SecuritySession iSecuritySession = _securitySessions[securitySessionName] as SecuritySession;
					if (iSecuritySession == null)
					{
						IKeyProvider iKeyProvider = keyStore.GetKey(securitySessionName);
						if (iKeyProvider == null)
							throw GenuineExceptions.Get_Security_ContextNotFound(securitySessionName);
						iSecuritySession = iKeyProvider.CreateSecuritySession(securitySessionName, this);
						_securitySessions[securitySessionName] = iSecuritySession;
					}

					return iSecuritySession;
				}
			}
		}
		private Hashtable _securitySessions = new Hashtable();

		/// <summary>
		/// Destroys the Security Session with the specified name. 
		/// Releases all Security Session resources.
		/// </summary>
		/// <param name="securitySessionName">The name of the Security Session.</param>
		public void DestroySecuritySession(string securitySessionName)
		{
			lock (this.DisposeLock)
			{
				if (this.IsDisposed)
					throw OperationException.WrapException(this.DisposeReason);

				lock(_securitySessions)
				{
					SecuritySession iSecuritySession = _securitySessions[securitySessionName] as SecuritySession;
					if (iSecuritySession != null)
					{
						IDisposable iDisposable = iSecuritySession as IDisposable;
						if (iDisposable != null)
							iDisposable.Dispose();

						// LOG:
						BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
						if (binaryLogWriter != null && binaryLogWriter[LogCategory.Security] > 0 )
							binaryLogWriter.WriteEvent(LogCategory.Security, "HostInformation.DestroySecuritySession",
								LogMessageType.SecuritySessionDestroyed, null, null, iSecuritySession.Remote, null, 
								GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, iSecuritySession,
								iSecuritySession.Name, -1, 
								0, 0, 0, iSecuritySession.GetType().Name, iSecuritySession.Name, null, null,
								"Security Session has been destroyed manually.");

						_securitySessions.Remove(securitySessionName);
					}
				}
			}
		}

		/// <summary>
		/// This indexer provides the ability to access a specific element in the collection of key-and-value pairs (Client Session).
		/// </summary>
		public object this[object key] 
		{ 
			get
			{
				return this._clientSession[key];
			}
			set
			{
				if (value == null)
					this._clientSession.Remove(key);
				else
					this._clientSession[key] = value;
			}
		}
		private Hashtable _clientSession = Hashtable.Synchronized(new Hashtable());

		#region -- Persistent connection information -----------------------------------------------

		/// <summary>
		/// Indicates the algorithm used for servicing persistent connection to this remote host.
		/// </summary>
		public GenuinePersistentConnectionState GenuinePersistentConnectionState
		{
			get
			{
				return this._genuinePersistentConnectionState;
			}
			set
			{
				this._genuinePersistentConnectionState = value;
			}
		}
		private GenuinePersistentConnectionState _genuinePersistentConnectionState = GenuinePersistentConnectionState.NotEstablished;

		/// <summary>
		/// Connection manager locks this member while opening persistent connection to the remote host.
		/// </summary>
		internal object PersistentConnectionEstablishingLock = new object();

		/// <summary>
		/// Gets a value indicating the unique sequence number of this instance.
		/// </summary>
		/// <returns></returns>
		public int RemoteHostUniqueIdentifier
		{
			get
			{
				lock (this._accessToLocalMembers)
					return this._remoteHostUniqueIdentifier;
			}
		}
		private int _remoteHostUniqueIdentifier = -1;

		/// <summary>
		/// Gets a value indicating the unique sequence number of this instance.
		/// </summary>
		/// <returns></returns>
		public int LocalHostUniqueIdentifier
		{
			get
			{
				lock (this._accessToLocalMembers)
					return this._localHostUniqueIdentifier;
			}
		}
		private int _localHostUniqueIdentifier = Interlocked.Increment(ref _CurrentUniqueIdentifier);
		private static int _CurrentUniqueIdentifier = 0;

		#endregion

		#region -- Disposing -----------------------------------------------------------------------

		/// <summary>
		/// Indicates whether this object was disposed.
		/// </summary>
		public bool IsDisposed
		{
			get
			{
				lock (this.DisposeLock)
					return this._isDisposed;
			}
		}
		private bool _isDisposed = false;

		/// <summary>
		/// Dispose lock.
		/// </summary>
		internal object DisposeLock = new object();

		/// <summary>
		/// The reason of the disposing.
		/// </summary>
		public Exception DisposeReason
		{
			get
			{
				lock (this.DisposeLock)
					return this._disposeReason;
			}
			set
			{
				lock (this.DisposeLock)
					this._disposeReason = value;
			}
		}
		private Exception _disposeReason;

		/// <summary>
		/// Releases all acquired resources.
		/// Warning: must be called only by the instance of the KnownHosts class.
		/// </summary>
		/// <param name="reason">The reason of resource releasing.</param>
		/// <returns>False if host resources have already been released before this call.</returns>
		internal bool Dispose(Exception reason)
		{
			if (this.IsDisposed)
				return false;

			lock (this.DisposeLock)
			{
				if (this.IsDisposed)
					return false;
				this._isDisposed = true;
				this.DisposeReason = reason;

				// LOG:
				BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
				if (binaryLogWriter != null && binaryLogWriter[LogCategory.HostInformation] > 0 )
				{
					string stackTrace = String.Empty;
					try
					{
						stackTrace = Environment.StackTrace;
					}
					catch
					{
					}

					binaryLogWriter.WriteEvent(LogCategory.HostInformation, "HostInformation.Dispose",
						LogMessageType.HostInformationReleased, reason, null, this, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null,
						null, -1, 0, 0, 0, this.Uri, this.Url, null, null,
						"The HostInformation is released. The caller: {0}.", stackTrace);
				}

				// release all Security Sessions that need this
				lock (this._securitySessions)
				{
					foreach (DictionaryEntry entry in this._securitySessions)
					{
						IDisposable iDisposable = entry.Value as IDisposable;
						if (iDisposable != null)
							iDisposable.Dispose();
					}

					this._securitySessions.Clear();
				}
			}

			return true;
		}

		#endregion

		#region -- ISetSecuritySession Members -----------------------------------------------------

		/// <summary>
		/// Gets or sets the Security Session used in this context.
		/// </summary>
		public SecuritySessionParameters SecuritySessionParameters
		{
			get
			{
				lock (_securitySessionParametersLock)
					return this._securitySessionParameters;
			}
			set
			{
				lock (_securitySessionParametersLock)
					this._securitySessionParameters = value;
			}
		}
		private SecuritySessionParameters _securitySessionParameters;
		private object _securitySessionParametersLock = new object();

		#endregion

		#region -- Debugging -----------------------------------------------------------------------

#if DEBUG
		/// <summary>
		/// Constructs a fake instance of the HostInformation for debugging and diagnostic purposes.
		/// </summary>
		/// <returns>An instance of the HostInformation class filled with fake stuff.</returns>
		public static HostInformation ConstructInstanceForDebugging()
		{
			HostInformation hostInformation = new HostInformation("gtcp://fakehost:8737/fake.rem", null);
			hostInformation._uri = "gtcp://8089";
			hostInformation._remoteHostUniqueIdentifier = 15;
			return hostInformation;
		}
#endif

		#endregion

		/// <summary>
		/// Returns a string that represents the current Object.
		/// </summary>
		/// <returns>A string that represents the current Object.</returns>
		public override string ToString()
		{
			return String.Format("HOST (URL: {0} URI: {1} LocalNo: {2} RemoteNo: {3})",
				this.Url == null ? "<none>" : this.Url, 
				this.Uri == null ? "<none>" : this.Uri,
				this.LocalHostUniqueIdentifier, this.RemoteHostUniqueIdentifier);
		}

	}
}
