/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved. 
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Lifetime;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Serialization.Formatters.Binary;

using Belikov.Common.ThreadProcessing;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Utilities;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.DotNetRemotingLayer;

namespace Belikov.GenuineChannels.BroadcastEngine
{
	/// <summary>
	/// Represents a method called when an invocation made via Broadcast Engine is done asynchronously.
	/// </summary>
	public delegate void BroadcastCallFinishedHandler(Dispatcher dispatcher, IMessage message, ResultCollector resultCollector);

	/// <summary>
	/// Represents a method called when a receiver is being excluded from the Dispatcher.
	/// </summary>
	public delegate void BroadcastReceiverHasBeenExcludedEventHandler(MarshalByRefObject marshalByRefObject, ReceiverInfo receiverInfo);

	/// <summary>
	/// Manages a collection of receivers and provides a way to send a message to all receivers
	/// concurrently.
	/// All methods and properties are thread-safe.
	/// </summary>
	public class Dispatcher : MarshalByRefObject
	{
		/// <summary>
		/// Constructs an instance of the Dispatcher class.
		/// </summary>
		/// <param name="interfaceToSupport">The interface which is supported by all receivers.</param>
		public Dispatcher(Type interfaceToSupport)
		{
			if (! interfaceToSupport.IsInterface)
				throw GenuineExceptions.Get_Broadcast_DestinationTypeMustBeAnInterface();
			this._broadcastProxy = new BroadcastProxy(interfaceToSupport, this);
			this._transparentProxy = _broadcastProxy.GetTransparentProxy();

			this._interfaceToSupport = interfaceToSupport;
		}

		private BroadcastProxy _broadcastProxy;
		private object _accessToPrivateVariables = new object();

		/// <summary>
		/// The interface supported by the transparent proxy provided by this instance.
		/// </summary>
		public Type SupportedInterface
		{
			get
			{
				return this._interfaceToSupport;
			}
		}
		private Type _interfaceToSupport;

		/// <summary>
		/// Dispatcher's tag. This member is not used by Genuine Channels,
		/// so you can use it on your own.
		/// </summary>
		public object Tag
		{
			get
			{
				lock (this._accessToPrivateVariables)
					return this._tag;
			}
			set
			{
				lock (this._accessToPrivateVariables)
					this._tag = value;
			}
		}
		private object _tag;

		/// <summary>
		/// Gets or sets a value indicating the maximum time span to receive responses from the receivers.
		/// If reply has not been received during this period of time, OperationException with 
		/// GenuineChannels.Exception.Broadcast.RemoteEndPointDidNotReplyForTimeOut
		/// error identifier will be fired.
		/// </summary>
		public TimeSpan ReceiveResultsTimeOut
		{
			get
			{
				lock (this._accessToPrivateVariables)
					return _receiveResultsTimeOut;
			}
			set
			{
				lock (this._accessToPrivateVariables)
					this._receiveResultsTimeOut = value;
			}
		}
		private TimeSpan _receiveResultsTimeOut = TimeSpan.FromSeconds(120);

		/// <summary>
		/// If this member is false, then broadcast call is synchronous. That is the caller's thread 
		/// waits until all receivers replies or timeout elapses.
		/// If this member is true and BroadcastCallFinishedHandler member is not null, then broadcast 
		/// call is asynchronous. Accordingly, control is returned to the caller as soon as broadcast invocation
		/// will be initiated. When all receivers reply or timeout is elapsed, the 
		/// BroadcastCallFinishedHandler callback is called.
		/// </summary>
		public bool CallIsAsync
		{
			get
			{
				lock (this._accessToPrivateVariables)
					return this._callIsAsync && this.BroadcastCallFinishedHandler != null;
			}
			set
			{
				lock (this._accessToPrivateVariables)
				{
					if (value == true && this.BroadcastCallFinishedHandler == null)
						throw GenuineExceptions.Get_Broadcast_HandlerUninitialized();

					this._callIsAsync = value;
				}
			}
		}
		private bool _callIsAsync = false;

		/// <summary>
		/// Delegate to be called when an async call is finished.
		/// </summary>
		public BroadcastCallFinishedHandler BroadcastCallFinishedHandler
		{
			get
			{
				lock (this._accessToPrivateVariables)
					return this._broadcastCallFinishedHandler;
			}
			set
			{
				lock (this._accessToPrivateVariables)
					this._broadcastCallFinishedHandler = value;
			}
		}
		private BroadcastCallFinishedHandler _broadcastCallFinishedHandler;

		/// <summary>
		/// Signals that a receiver has been excluded from the receiver list.
		/// The sponsor for the receiver is detached after this event.
		/// </summary>
		public event BroadcastReceiverHasBeenExcludedEventHandler BroadcastReceiverHasBeenExcluded;

		/// <summary>
		/// Gets or sets a value indicating the maximum allowed number of consecutive failures
		/// that can be made by a receiver. As soon as the receiver consecutively fails for this 
		/// number of time, it is automatically removed from the receiver list.
		/// You can set this value to zero in order to prevent automatic excluding.
		/// I would recommend to set a large value (for example, 100) in this case.
		/// Remember, each time the receiver sends successful response, counter for this receiver is resetted to zero.
		/// </summary>
		public int MaximumNumberOfConsecutiveFailsToExcludeReceiverAutomatically
		{
			get
			{
				lock (this._accessToPrivateVariables)
					return _maximumNumberOfConsecutiveFailsToExcludeReceiverAutomatically;
			}
			set
			{
				lock (this._accessToPrivateVariables)
					this._maximumNumberOfConsecutiveFailsToExcludeReceiverAutomatically = value;
			}
		}
		private int _maximumNumberOfConsecutiveFailsToExcludeReceiverAutomatically = 4;

		/// <summary>
		/// Represents a value indicating the maximum number of failures allowing to specific
		/// remote receiver before switching it to simulation mode.
		/// That is as soon as a true broadcast sender will not be able to deliver a message 
		/// consecutively for this number of tries to the specific receiver, it will automatically
		/// start sending messages to this specific receiver via the usual channel.
		/// Set it to zero to prevent switching to simulation mode.
		/// I would recommend to set a large value (for example, 100) in this case.
		/// Remember, each time the receiver sends successful response to the message sent via 
		/// true multicast channel, this counter is resetted to zero and receiver is switched back
		/// to normal mode when messages are delivered via "true" multicast channel.
		/// </summary>
		public int MaximumNumberOfConsecutiveFailsToEnableSimulationMode
		{
			get
			{
				lock (this._accessToPrivateVariables)
					return _maximumNumberOfConsecutiveFailsToEnableSimulationMode;
			}
			set
			{
				lock (this._accessToPrivateVariables)
					this._maximumNumberOfConsecutiveFailsToEnableSimulationMode = value;
			}
		}
		private int _maximumNumberOfConsecutiveFailsToEnableSimulationMode = 4;

		/// <summary>
		/// Gets or sets an value indicating whether the recurrent calls received via different 
		/// transports will be ignored.
		/// Also, if you register the same receiver twice and turn on this option, it will
		/// be invoked only once.
		/// </summary>
		public bool IgnoreRecurrentCalls
		{
			get
			{
				lock (this._accessToPrivateVariables)
					return _ignoreRecurrentCalls;
			}
			set
			{
				lock (this._accessToPrivateVariables)
					this._ignoreRecurrentCalls = value;
			}
		}
		private bool _ignoreRecurrentCalls = true;

		/// <summary>
		/// Gets an integer indicating how many senders and recipients have been added to the dispatcher collection.
		/// </summary>
		public int RecipientsSubscribed
		{
			get
			{
				using (ReaderAutoLocker reader = new ReaderAutoLocker(this._readerWriterLock))
				{
					return this._receivers.Count;
				}
			}
		}

		/// <summary>
		/// Sponsor that lives while Dispatcher is alive.
		/// </summary>
		private ClientSponsor GlobalSponsor = new ClientSponsor();

		/// <summary>
		/// Registers the receiver. Returns false if the receiver has already been registered.
		/// WARNING: does not check whether the receiver supports the required interface (via Reflection) 
		/// because this check requires client's dll.
		/// </summary>
		/// <param name="obj">The receiver being registered.</param>
		/// <returns>False if the receiver has been already registered.</returns>
		public bool Add(MarshalByRefObject obj)
		{
			return this.Add(obj, null);
		}

		/// <summary>
		/// Registers the receiver and associate the provided object with it.
		/// Returns false if the receiver has already been registered.
		/// WARNING: does not check whether the receiver supports the required interface (via Reflection) 
		/// because this check requires client's dll.
		/// </summary>
		/// <param name="obj">The receiver being registered.</param>
		/// <param name="tag">The object associated with the receiver. This object is accessible when receiver is being unregistered or during filtering.</param>
		/// <returns>False if the receiver has been already registered.</returns>
		public bool Add(MarshalByRefObject obj, object tag)
		{
			return this.Add(obj, tag, null, null);
		}

		/// <summary>
		/// Registers the receiver and associate the provided object with it.
		/// Returns false if the receiver has already been registered.
		/// WARNING: does not check whether the receiver supports the required interface (via Reflection) 
		/// because this check requires client's dll.
		/// </summary>
		/// <param name="obj">The receiver being registered.</param>
		/// <param name="tag">The object associated with the receiver. This object is accessible when receiver is being unregistered or during filtering.</param>
		/// <param name="remoteGenuineUri">The uri of the remote host provided by any of Genuine Channels.</param>
		/// <param name="transportContext">The transport context of the remote host.</param>
		/// <returns>False if the receiver has been already registered.</returns>
		public bool Add(MarshalByRefObject obj, object tag, string remoteGenuineUri, ITransportContext transportContext)
		{
			if (obj == null)
				throw new ArgumentNullException("obj");

			BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;

			// check if it is in the list
			string uri = RemotingServices.GetObjectUri(obj);
			if (uri == null && ! RemotingServices.IsObjectOutOfAppDomain(obj))
			{
				// it was not marshalled
				RemotingServices.Marshal(obj);
				uri = RemotingServices.GetObjectUri(obj);
			}

			using (ReaderAutoLocker reader = new ReaderAutoLocker(this._readerWriterLock))
			{
				if (this._receivers.ContainsKey(uri))
					return false;
			}

			// this check can not be performed because client's dll is required
//			// check on the interface
//			bool supportInterface = false;
//			foreach(Type interfaceType in obj.GetType().GetInterfaces())
//				if (interfaceType == this._interfaceToSupport)
//				{
//					supportInterface = true;
//					break;
//				}
//			if (! supportInterface)
//				throw GenuineExceptions.Get_Broadcast_ObjectDoesNotSupportDestinationInterface();

			// adds the object to the receiver list
			ReceiverInfo receiverInfo = new ReceiverInfo();
			receiverInfo.MbrObject = obj;
			receiverInfo.MbrUri = uri;
			receiverInfo.Tag = tag;

			if (binaryLogWriter != null)
			{
				try
				{
					if (receiverInfo.MbrObject != null)
						receiverInfo.DbgRemoteHost = GenuineUtility.FetchHostInformationFromMbr(receiverInfo.MbrObject);
				}
				catch(Exception ex)
				{
					binaryLogWriter.WriteImplementationWarningEvent("Dispatcher.Add",
						LogMessageType.Error, ex, GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						"Can't get HostInformation from MbrObject.");
				}
			}

			ObjRef objRef = receiverInfo.MbrObject.CreateObjRef(typeof(MarshalByRefObject));
			receiverInfo.Local = objRef.IsFromThisAppDomain();

			// cache object's info to speed up sending thru Genuine Channels
			if (! receiverInfo.Local)
			{
				if (remoteGenuineUri != null)
				{
					receiverInfo.IClientChannelSink = new GenuineTcpClientTransportSink(remoteGenuineUri, transportContext);
				}
				else
				{
					// check whether the client sink has registered itself on this MBR 
					receiverInfo.IClientChannelSink = ChannelServices.GetChannelSinkProperties(obj)["GC_TS"] as IClientChannelSink;
					if (receiverInfo.IClientChannelSink == null)
						throw GenuineExceptions.Get_Broadcast_ClientSinkIsUnknown();
				}

				// object uri
				receiverInfo.SerializedObjRef = objRef;

//				// and shell's uri
//				string shellUri;
//				ITransportContext iTransportContext;
//				GenuineUtility.FetchChannelUriFromMbr(obj, out shellUri, out iTransportContext);
//				if (shellUri == null)
//					throw GenuineExceptions.Get_Send_NoSender(objRef.URI);
//
//				receiverInfo.ReceiverUri = shellUri;
			}

			// LOG:
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
			{
				binaryLogWriter.WriteBroadcastEngineEvent(LogCategory.BroadcastEngine, "Dispatcher.Add",
					LogMessageType.BroadcastRecipientAdded, null, null, receiverInfo.DbgRemoteHost, null, 
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
					null, null, false, this, null, true, receiverInfo,
					null, null,
					"The broadcast recipient is added.");
			}

			// register the sponsor to prevent unexpected reclaiming
			ILease lease = (ILease) RemotingServices.GetLifetimeService(obj);
			if (lease != null)
				lease.Register(this.GlobalSponsor);

			// and register it
			using (WriterAutoLocker writer = new WriterAutoLocker(this._readerWriterLock))
			{
				this._cachedReceiversInfoArray = null;
				this._receivers[uri] = receiverInfo;
			}
			return true;
		}

		/// <summary>
		/// Registers the broadcast sender that sends a message to several receivers.
		/// Returns Guid (it's like URI for an MBR object) assigned for this sender that can be used
		/// to delete this broadcast sender later.
		/// </summary>
		/// <param name="generalBroadcastSender">The broadcast sender which sends a message via "true" multicast channel.</param>
		public string Add(GeneralBroadcastSender generalBroadcastSender)
		{
			// just add it as it would be the usual receiver
			ReceiverInfo receiverInfo = new ReceiverInfo();
			receiverInfo.GeneralBroadcastSender = generalBroadcastSender;
			receiverInfo.Local = false;

			string uri = Guid.NewGuid().ToString("N") + "/" + generalBroadcastSender.Court;
			receiverInfo.MbrUri = uri;

			// register it
			using (WriterAutoLocker writer = new WriterAutoLocker(this._readerWriterLock))
			{
				this._cachedReceiversInfoArray = null;
				this._receivers[uri] = receiverInfo;
			}

			// LOG:
			BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
			{
				binaryLogWriter.WriteBroadcastEngineEvent(LogCategory.BroadcastEngine, "Dispatcher.Add",
					LogMessageType.BroadcastRecipientAdded, null, null, null, null, 
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
					null, null, false, this, null, true, receiverInfo,
					generalBroadcastSender.Court, generalBroadcastSender.ITransportContext.HostIdentifier,
					"The \"true\" multicast sender is added.");
			}

			return uri;
		}

		/// <summary>
		/// Removes the receiver from the list of receivers.
		/// Returns false if there is no such receiver found in the list of receivers.
		/// </summary>
		/// <param name="obj">The receiver being exluded.</param>
		/// <returns>False if there is no such receiver in the list of receivers.</returns>
		public bool Remove(MarshalByRefObject obj)
		{
			string uri = RemotingServices.GetObjectUri(obj);
			return this.Remove(uri);
		}

		/// <summary>
		/// Removes the receiver or the broadcast sender associated with the specified uri.
		/// Returns false if there is no such receiver found in the list of receivers.
		/// </summary>
		/// <param name="uri">The uri of the receiver.</param>
		public bool Remove(string uri)
		{
			if (uri == null || uri.Length <= 0)
				return false;

			using (WriterAutoLocker writer = new WriterAutoLocker(this._readerWriterLock))
			{
				// check if it is in the list
				if (! this._receivers.ContainsKey(uri))
					return false;

				this._cachedReceiversInfoArray = null;

				ReceiverInfo receiverInfo = (ReceiverInfo) this._receivers[uri];
				this._receivers.Remove(uri);

				// LOG:
				BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
				{
					binaryLogWriter.WriteBroadcastEngineEvent(LogCategory.BroadcastEngine, "Dispatcher.Remove",
						LogMessageType.BroadcastRecipientRemoved, null, null, receiverInfo.DbgRemoteHost, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, false, this, null, true, receiverInfo,
						uri, null,
						"The broadcast recipient or \"true\" broadcast sender is removed from the list of recipients.");
				}

				// Sponsor is being deleted in another thread; otherwise client disconnection 
				// will cause dispatcher to be freezed for lengthy time out
				GenuineThreadPool.QueueUserWorkItem(new WaitCallback(this.DeleteSponsorFromTheObjectAndFireEvent), receiverInfo, false);
			}

			return true;
		}

		/// <summary>
		/// Fires the BroadcastReceiverHasBeenExcluded event and detach the sponsor from the specified receiver.
		/// </summary>
		/// <param name="state">The receiver being excluded.</param>
		public void DeleteSponsorFromTheObjectAndFireEvent(object state)
		{
			ReceiverInfo receiverInfo = (ReceiverInfo) state;
			MarshalByRefObject marshalByRefObject = (MarshalByRefObject) receiverInfo.MbrObject;

			// fire the event
			try
			{
				if (this.BroadcastReceiverHasBeenExcluded != null)
					this.BroadcastReceiverHasBeenExcluded(marshalByRefObject, receiverInfo);
			}
			catch
			{
				// ignore all exceptions
			}

			// detach sponsor
			try
			{
				ILease lease = (ILease) RemotingServices.GetLifetimeService(marshalByRefObject);
				if (lease != null)
					lease.Unregister(this.GlobalSponsor);
			}
			catch
			{
				// ignore all exceptions
			}
		}

		/// <summary>
		/// Removes all recipients and broadcast senders.
		/// </summary>
		public void Clear()
		{
			using (WriterAutoLocker writer = new WriterAutoLocker(this._readerWriterLock))
			{
				this._cachedReceiversInfoArray = null;

				// collect all receivers to unregister the sponsor
				object[] receiverInfoItems = new object[this._receivers.Count];
				this._receivers.Values.CopyTo(receiverInfoItems, 0);
				GenuineThreadPool.QueueUserWorkItem(new WaitCallback(this.UnregisterSponsor), receiverInfoItems, true);

				this._receivers.Clear();
			}
		}

		/// <summary>
		/// Unregisters the sponsor.
		/// </summary>
		/// <param name="receiverInfoItemsAsObject">Array of receivers.</param>
		private void UnregisterSponsor(object receiverInfoItemsAsObject)
		{
			object[] receiverInfoItems = (object[]) receiverInfoItemsAsObject;

			// go though the list of receivers to remove sponsors from all MBR objects
			foreach (ReceiverInfo receiverInfo in receiverInfoItems)
			{
				try
				{
					// remove the sponsor
					if (receiverInfo.MbrObject != null)
					{
						// LOG:
						BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;
						if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
						{
							binaryLogWriter.WriteBroadcastEngineEvent(LogCategory.BroadcastEngine, "Dispatcher.UnregisterSponsor",
								LogMessageType.BroadcastRecipientRemoved, null, null, receiverInfo.DbgRemoteHost, null, 
								GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
								null, null, false, this, null, true, receiverInfo,
								null, null,
								"The broadcast recipient is removed after Dispatcher.Clear call.");
						}

						ILease lease = (ILease) RemotingServices.GetLifetimeService(receiverInfo.MbrObject);
						if (lease != null)
							lease.Unregister(this.GlobalSponsor);
					}
				}
				catch
				{
				}
			}
		}

		/// <summary>
		/// Returns the receiver associated with the specified uri.
		/// </summary>
		/// <param name="uri">The uri associated with the receiver.</param>
		/// <returns>The receiver associated with the specified uri.</returns>
		public MarshalByRefObject FindObjectByUri(string uri)
		{
			using (ReaderAutoLocker reader = new ReaderAutoLocker(this._readerWriterLock))
			{
				ReceiverInfo receiverInfo = this._receivers[uri] as ReceiverInfo;
				if (receiverInfo == null)
					return null;

				return receiverInfo.MbrObject;
			}
		}

		/// <summary>
		/// Returns the receiver associated with the specified uri.
		/// </summary>
		/// <param name="mbrUri">The uri associated with the receiver.</param>
		/// <returns>The receiver associated with the specified uri.</returns>
		public ReceiverInfo GetReceiverInfo(string mbrUri)
		{
			using (ReaderAutoLocker reader = new ReaderAutoLocker(this._readerWriterLock))
			{
				return this._receivers[mbrUri] as ReceiverInfo;
			}
		}

		/// <summary>
		/// To synchronize access to _receivers.
		/// </summary>
		internal ReaderWriterLock _readerWriterLock = new ReaderWriterLock();

		/// <summary>
		/// Receiver hash that contains { Receiver URI (string) => ReceiverInfo (object) } records.
		/// </summary>
		internal Hashtable _receivers = new Hashtable();

		/// <summary>
		/// Is used to cache the list of the _receivers' values.
		/// </summary>
		internal object[] _cachedReceiversInfoArray;

		/// <summary>
		/// The transparent proxy that implements requested interface (or type).
		/// </summary>
		public object TransparentProxy
		{
			get
			{
				return _transparentProxy;
			}
		}
		private object _transparentProxy;

		/// <summary>
		/// Does absolutely nothing. If you want to enable async calls and do not need to process call results, use this stub as an event receiver.
		/// </summary>
		/// <param name="dispatcher">Ignored.</param>
		/// <param name="message">Ignored.</param>
		/// <param name="resultCollector">Ignored.</param>
		public void AsyncEventStub(Dispatcher dispatcher, IMessage message, ResultCollector resultCollector)
		{
		}


		#region -- Filtering the receivers ---------------------------------------------------------

		/// <summary>
		/// Returns the filtered array of the ReceiverInfo instances.
		/// </summary>
		/// <param name="arrayOfReceiverInfo">The list of receivers being filtered out.</param>
		/// <param name="iMessage">The invocation.</param>
		/// <param name="resultCollector">The Result Collector obtaining the list of receivers. Is used for debugging purposes only.</param>
		public void GetListOfReceivers(out object[] arrayOfReceiverInfo, IMessage iMessage, ResultCollector resultCollector)
		{
			arrayOfReceiverInfo = null;

			try
			{
				using (ReaderAutoLocker reader = new ReaderAutoLocker(this._readerWriterLock))
				{
					// check whether they were cached
					if (this._cachedReceiversInfoArray == null)
					{
						this._cachedReceiversInfoArray = new object[this._receivers.Values.Count];
						this._receivers.Values.CopyTo(this._cachedReceiversInfoArray, 0);
					}

					// get the cached array
					arrayOfReceiverInfo = this._cachedReceiversInfoArray;

					// and drive it thru the filter
					IMulticastFilter iMulticastFilter = Dispatcher.GetCurrentFilter();
					if (iMulticastFilter == null)
						iMulticastFilter = this.IMulticastFilter;
					if (iMulticastFilter != null)
					{
						// LOG:
						BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;
						if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
						{
							binaryLogWriter.WriteBroadcastEngineEvent(LogCategory.BroadcastEngine, "Dispatcher.GetListOfReceivers",
								LogMessageType.BroadcastFilterCalled, null, null, null, null, 
								GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
								null, null, false, this, resultCollector, false, null,
								iMulticastFilter.GetType().ToString(), null,
								"The broadcast filter is called. The type of the filter: {0}.", iMulticastFilter.GetType().ToString());
						}

						arrayOfReceiverInfo = iMulticastFilter.GetReceivers(arrayOfReceiverInfo, iMessage);
					}
				}
			}
			catch(Exception ex)
			{
				// LOG:
				BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.BroadcastEngine] > 0 )
				{
					binaryLogWriter.WriteBroadcastEngineEvent(LogCategory.BroadcastEngine, "Dispatcher.GetListOfReceivers",
						LogMessageType.BroadcastFilterCalled, ex, null, null, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, false, this, resultCollector, false, null,
						null, null,
						"The exception occurred while calling the broadcast filter.");
				}
			}
		}

		/// <summary>
		/// The filter that will be used if no filter is specified in the current thread.
		/// See Programming Guide for better explanation.
		/// </summary>
		public IMulticastFilter IMulticastFilter
		{
			get
			{
				lock(this._iMulticastFilterLock)
					return this._iMulticastFilter;
			}
			set
			{
				lock(this._iMulticastFilterLock)
					this._iMulticastFilter = value;

			}
		}
		private IMulticastFilter _iMulticastFilter;
		private object _iMulticastFilterLock = new object();

		/// <summary>
		/// Specifies the filter which will be used in the current thread.
		/// Returns the preceding filter specified in the current thread.
		/// </summary>
		/// <param name="iMulticastFilter">The filter being set.</param>
		/// <returns>The previous filter.</returns>
		public static IMulticastFilter SetCurrentFilter(IMulticastFilter iMulticastFilter)
		{
			LocalDataStoreSlot localDataStoreSlot = Thread.GetNamedDataSlot(OccupiedThreadSlots.DispatcherFilterUsed);

			object previousDataSlotValue = Thread.GetData(localDataStoreSlot);
			Thread.SetData(localDataStoreSlot, iMulticastFilter);

			return previousDataSlotValue as IMulticastFilter;
		}

		/// <summary>
		/// Gets the filter which is used for filtering calls made via all Dispatchers in the current thread.
		/// </summary>
		/// <returns></returns>
		public static IMulticastFilter GetCurrentFilter()
		{
			LocalDataStoreSlot localDataStoreSlot = Thread.GetNamedDataSlot(OccupiedThreadSlots.DispatcherFilterUsed);
			return Thread.GetData(localDataStoreSlot) as IMulticastFilter;
		}

		#endregion

		#region -- Debugging stuff -------------------------------------------------------------------------

		/// <summary>
		/// The uniqued identifier of the current instance. Is used for debugging purposes only.
		/// </summary>
		public int DbgDispatcherId
		{
			get
			{
				return this._dbgDispatcherId;
			}
		}
		private int _dbgDispatcherId = Interlocked.Increment(ref _dbgTotalDispatchers);
		private static int _dbgTotalDispatchers = 0;

		/// <summary>
		/// Gets or sets the name of the current instance. Is used for debugging purposes only.
		/// </summary>
		public string DbgDispatcherName
		{
			get
			{
				return this._dbgDispatcherName;
			}
			set
			{
				this._dbgDispatcherName = value;
			}
		}
		private string _dbgDispatcherName;

		#endregion

	}
}
