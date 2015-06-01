/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.Threading;

using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Parameters;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Utilities;

namespace Belikov.GenuineChannels.TransportContext
{
	/// <summary>
	/// Manages a collection of hosts.
	/// </summary>
	public class KnownHosts : MarshalByRefObject, ITimerConsumer
	{
		/// <summary>
		/// Constructs an instance of the KnownHosts class.
		/// </summary>
		public KnownHosts(ITransportContext iTransportContext)
		{
			this.ITransportContext = iTransportContext;
			TimerProvider.Attach(this);
		}

		/// <summary>
		/// Transport Context.
		/// </summary>
		public ITransportContext ITransportContext;

		/// <summary>
		/// Gets HostInformation associated with the specified URI.
		/// </summary>
		/// <param name="uri">The URI of the remote host.</param>
		/// <returns>The HostInformation associated with the specified URI or a null reference.</returns>
		public HostInformation Get(string uri)
		{
			return this.GetHostInformation(uri);
		}

		/// <summary>
		/// Gets information about the remote host.
		/// Automatically creates and initializes a new instance of the HostInformation class when
		/// it is necessary.
		/// </summary>
		public HostInformation this[string uri]
		{
			get
			{
				lock (this.SyncRoot)
				{
					HostInformation hostInformation = this.Get(uri);

					if (hostInformation == null)
					{
						hostInformation = new HostInformation(uri, this.ITransportContext);
						this.UpdateHost(uri, hostInformation);

						// set a reasonable start up lifetime property value
						int expiration = GenuineUtility.ConvertToMilliseconds(this.ITransportContext.IParameterProvider[GenuineParameter.ConnectTimeout]);
						hostInformation.Renew(expiration, true);

						// LOG:
						BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;
						if ( binaryLogWriter != null && binaryLogWriter[LogCategory.HostInformation] > 0 )
							binaryLogWriter.WriteEvent(LogCategory.HostInformation, "KnownHosts.this[string]",
								LogMessageType.HostInformationCreated, null, null, hostInformation, 
								null, 
								GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
								null, null, -1, 0, 0, 0, null, null, null, null,
								"The HostInformation has been created for the remote host: {0}.", uri);
					}
					return hostInformation;
				}
			}
		}

		/// <summary>
		/// Updates a reference to the host.
		/// </summary>
		/// <param name="uriOrUrl">The reference.</param>
		/// <param name="hostInformation">The host.</param>
		internal void UpdateHost(string uriOrUrl, HostInformation hostInformation)
		{
			this.SetHostInformation(uriOrUrl, hostInformation);
		}

		/// <summary>
		/// Renews the host information.
		/// </summary>
		/// <param name="uriOrUrl">Uri or Url of the remote host.</param>
		/// <param name="timeSpan">The time period specified in milliseconds to renew host-related information.</param>
		/// <param name="canMakeShorter">Indicates whether this call may reduce the host expiration time.</param>
		public void Renew(string uriOrUrl, int timeSpan, bool canMakeShorter)
		{
			HostInformation hostInformation = null;

			hostInformation = this.Get(uriOrUrl);

			if (hostInformation != null)
				hostInformation.Renew(timeSpan, canMakeShorter);
		}

		/// <summary>
		/// Releases expired host structures.
		/// </summary>
		public void TimerCallback()
		{
			int now = GenuineUtility.TickCount;
			HostInformation hostInformation = null;

			// the released host
			ArrayList hostsToDelete = new ArrayList();

			// the entries being deleted
			ArrayList urisToDelete = new ArrayList();

			lock (this.SyncRoot)
			{
				// through all registered hosts
				foreach (DictionaryEntry entry in this._hashtable)
				{
					ArrayList hosts = (ArrayList) entry.Value;
					for ( int i = 0; i < hosts.Count; )
					{
						hostInformation = (HostInformation) hosts[i];

						// if the time has run out
						if (GenuineUtility.IsTimeoutExpired(hostInformation.ExpireTime, now) || hostInformation.IsDisposed)
						{
							// exclude the host
							hosts.RemoveAt(i);
							hostsToDelete.Add(hostInformation);

							// check on entry excluding
							if (hosts.Count <= 0)
								urisToDelete.Add(entry.Key);
							continue;
						}

						i++;
					}
				}

				// it is very important to remove all references to the host before disposing it
				foreach (string key in urisToDelete)
					this._hashtable.Remove(key);

				// dispose all hosts
				foreach (HostInformation hostInformationExcluded in hostsToDelete)
				{
					// LOG:
					BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;
					if ( binaryLogWriter != null && binaryLogWriter[LogCategory.HostInformation] > 0 )
						binaryLogWriter.WriteEvent(LogCategory.HostInformation, "KnownHosts.this[string]",
							LogMessageType.HostInformationReferencesDisassociated, GenuineExceptions.Get_Debugging_GeneralWarning("The association between HostInformation and its URL or URI has been broken."),
							null, hostInformationExcluded, null, 
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
							null, null, -1, 0, 0, 0, hostInformationExcluded.Uri, hostInformationExcluded.Url, null, null,
							"The current HostInformation does not refer to \"{0}\" and \"{1}\" any longer.", 
							hostInformationExcluded.Uri == null ? string.Empty : hostInformationExcluded.Uri,
							hostInformationExcluded.Url == null ? string.Empty : hostInformationExcluded.Url);

					GenuineThreadPool.QueueUserWorkItem(new WaitCallback(this.ReleaseHostResources), new HostInformationAndReason(hostInformationExcluded, GenuineExceptions.Get_Channel_ClientDidNotReconnectWithinTimeOut(hostInformationExcluded.ToString())), true);
				}
			}
		}

		/// <summary>
		/// Returns a collection containing all registered hosts.
		/// </summary>
		/// <returns>A collection containing all registered hosts.</returns>
		public ArrayList GetKnownHosts()
		{
			lock (this.SyncRoot)
			{
				// usually their amount will be equal
				ArrayList knownHosts = new ArrayList(this._hashtable.Count);
				foreach (DictionaryEntry entry in this._hashtable)
				{
					ArrayList hosts = (ArrayList) entry.Value;
					foreach (HostInformation hostInformation in hosts)
					{
						// avoid duplication
						if (hostInformation.Uri == null || hostInformation.Url == null || (hostInformation.Uri != null && hostInformation.Url != null && (string) entry.Key == hostInformation.Uri))
							knownHosts.Add(hostInformation);
					}
				}
				return knownHosts;
			}
		}

		/// <summary>
		/// Gets an integer indicating the total number of hosts registered in the current Transport Context.
		/// </summary>
		public int TotalHosts
		{
			get
			{
				lock (this.SyncRoot)
				{
					return this._hashtable.Count;
				}
			}
		}

		/// <summary>
		/// Releases resources belonging to the remote host.
		/// </summary>
		/// <param name="hostInformation">The remote host.</param>
		/// <param name="reason">The exception.</param>
		/// <returns>True if host resources have been released by this call. False if host resources have been already released.</returns>
		public bool ReleaseHostResources(HostInformation hostInformation, Exception reason)
		{
			if (hostInformation.Uri != null)
				this.RemoveHostInformation(hostInformation.Uri, hostInformation);
			if (hostInformation.Url != null)
				this.RemoveHostInformation(hostInformation.Url, hostInformation);

			// release all resources associated with this host
			bool cleanup = hostInformation.Dispose(reason);
			if (cleanup)
			{
				this.ITransportContext.ConnectionManager.ReleaseConnections(hostInformation, GenuineConnectionType.All, reason);
				this.ITransportContext.IIncomingStreamHandler.DispatchException(hostInformation, reason);

				// and fire a warning
				this.ITransportContext.IGenuineEventProvider.Fire(new GenuineEventArgs(GenuineEventType.HostResourcesReleased, reason, hostInformation, null));
			}

			return cleanup;
		}

		/// <summary>
		/// Is used to deliver the reason of disposing to a working thread.
		/// </summary>
		private class HostInformationAndReason
		{
			/// <summary>
			/// Constructs an instance of the HostInformationAndReason class.
			/// </summary>
			/// <param name="hostInformation">The remote host.</param>
			/// <param name="reason">The exception.</param>
			public HostInformationAndReason(HostInformation hostInformation, Exception reason)
			{
				this.HostInformation = hostInformation;
				this.Reason = reason;
			}

			/// <summary>
			/// The reason of the disposing.
			/// </summary>
			public Exception Reason;

			/// <summary>
			/// The exception causes disposing.
			/// </summary>
			public HostInformation HostInformation;
		}

		/// <summary>
		/// Executes the ReleaseHostResources method in the ThreadPool's thread.
		/// </summary>
		/// <param name="hostInformationAndReasonAsObject"></param>
		private void ReleaseHostResources(object hostInformationAndReasonAsObject)
		{
			HostInformationAndReason hostInformationAndReason = (HostInformationAndReason) hostInformationAndReasonAsObject;
			this.ReleaseHostResources(hostInformationAndReason.HostInformation, hostInformationAndReason.Reason);
		}

		#region -- Collection Management -----------------------------------------------------------

		private Hashtable _hashtable = new Hashtable();

		/// <summary>
		/// Adds an element to the collection associated with the specified key.
		/// Ensures that there is the only instance of the element in the collection.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="hostInformation">The element being stored.</param>
		private void SetHostInformation(string key, HostInformation hostInformation)
		{
			lock (this.SyncRoot)
			{
				ArrayList arrayList = this._hashtable[key] as ArrayList;

				// if there is no collection corresponding to the key
				if (arrayList == null)
				{
					arrayList = new ArrayList();
					this._hashtable[key] = arrayList;
					arrayList.Add(hostInformation);
					return ;
				}

				// check whether this element is already in the collection
				for ( int i = 0; i < arrayList.Count; i++ )
				{
					if (object.ReferenceEquals(arrayList[i], hostInformation))
						return ;

					// TODO: What to do with this fix?! It does work, but only in really unusual circumstances.
					// remove all HostInformations with empty URI, if the current hostInformation contains the URI
					HostInformation currentHostInformation = (HostInformation) arrayList[i];
					if (hostInformation.Url != null && currentHostInformation.Url == null && 
						hostInformation.Uri == currentHostInformation.Uri && currentHostInformation.RemoteHostUniqueIdentifier == -1)
					{
						arrayList.RemoveAt(i);
						arrayList.Add(hostInformation);
						currentHostInformation.Dispose(GenuineExceptions.Get_Receive_ConflictOfConnections());
						return ;
					}
				}

				arrayList.Add(hostInformation);
			}
		}

		/// <summary>
		/// Retrieves the first entry from the collection associated with the specified key.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <returns>The first entry from the collection associated with the specified key.</returns>
		public HostInformation GetHostInformation(string key)
		{
			lock (this.SyncRoot)
			{
				ArrayList arrayList = this._hashtable[key] as ArrayList;

				if (arrayList == null)
					return null;

				return (HostInformation) arrayList[0];
			}			
		}

		/// <summary>
		/// Removes the specified element from the collection associated with the specified key.
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="hostInformation">The element being removed.</param>
		private void RemoveHostInformation(string key, HostInformation hostInformation)
		{
			lock (this.SyncRoot)
			{
				ArrayList arrayList = this._hashtable[key] as ArrayList;

				// if there is no collection corresponding to the key
				if (arrayList == null)
					return ;

				// check whether this element is already in the collection
				for ( int i = 0; i < arrayList.Count; i++ )
					if (object.ReferenceEquals(arrayList[i], hostInformation))
					{
						arrayList.RemoveAt(i);
						break;
					}

				// if the collection is empty, delete it with the entry.
				if (arrayList.Count <= 0)
					this._hashtable.Remove(key);
			}
		}

		/// <summary>
		/// Collects expired elements.
		/// </summary>
		/// <param name="collectedHosts">The collected items.</param>
		/// <param name="now">The current moment.</param>
		private void CollectExpiredHostInformation(ArrayList collectedHosts, int now)
		{
#if DEBUG
			if (GenuineUtility.IsDebuggingModeEnabled)
				return ;
#endif

			lock (this.SyncRoot)
			{
				// by all uris
				foreach (DictionaryEntry entry in this._hashtable)
				{
					ArrayList arrayList = (ArrayList) entry.Value;

					// all available hosts
					for ( int i = 0; i < arrayList.Count; )
					{
						HostInformation hostInformation = (HostInformation) arrayList[i];

						if (GenuineUtility.IsTimeoutExpired(hostInformation.ExpireTime, now) || hostInformation.IsDisposed)
						{
							collectedHosts.Add(hostInformation);
							arrayList.RemoveAt(i);
							continue;
						}

						i++;
					}
				}
			}
		}

		/// <summary>
		/// Gets an object that can be used to synchronize access to this instance.
		/// </summary>
		public object SyncRoot
		{
			get
			{
				return this._syncRoot;
			}
		}
		private object _syncRoot = new object();

		#endregion

	}
}
