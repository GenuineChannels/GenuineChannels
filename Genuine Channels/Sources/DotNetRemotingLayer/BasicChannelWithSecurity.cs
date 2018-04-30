/* Genuine Channels product.
 *
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 *
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Messaging;
using System.Threading;

using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Parameters;
using Belikov.GenuineChannels.Receiving;
using Belikov.GenuineChannels.Security;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Utilities;
using Zyan.SafeDeserializationHelpers.Channels;

namespace Belikov.GenuineChannels.DotNetRemotingLayer
{
	/// <summary>
	/// Represents an abstract .NET Remoting channel working via Transport Context.
	/// </summary>
	public abstract class BasicChannelWithSecurity : BaseChannelWithProperties, ISetSecuritySession, IChannelReceiver, IChannelSender, ITransportUser, IGenuineChannelEventProvider, IGenuineEventHandler, ITransportContextProvider
	{
		/// <summary>
		/// Constructs an instance of the BasicChannelWithSecurity class.
		/// </summary>
		/// <param name="iClientChannelSinkProvider">The client channel sink provider.</param>
		/// <param name="iServerChannelSinkProvider">The server channel sink provider.</param>
		public BasicChannelWithSecurity(IClientChannelSinkProvider iClientChannelSinkProvider, IServerChannelSinkProvider iServerChannelSinkProvider)
		{
			this._iClientChannelSinkProvider = iClientChannelSinkProvider;
			this._iServerChannelSinkProvider = iServerChannelSinkProvider;
		}

		/// <summary>
		/// Constructs an instance of the BasicChannelWithSecurity class.
		/// </summary>
		/// <param name="properties">Channel properties.</param>
		protected void InitializeInstance(IDictionary properties)
		{
			// properties
			if (properties == null)
				this._properties = new Hashtable();
			else
				this._properties = properties;

			this.ReadSettings(properties);

			// create default client formatter sink
			if (this._iClientChannelSinkProvider == null)
				this._iClientChannelSinkProvider = new SafeBinaryClientFormatterSinkProvider();

			// connect client sink provider to the end
			IClientChannelSinkProvider currentProvider = this._iClientChannelSinkProvider;
			while (currentProvider.Next != null)
				currentProvider = currentProvider.Next;
			currentProvider.Next = new GenuineTcpClientTransportSinkProvider(this.ITransportContext);

			// create default server formatter sink
			if (this._iServerChannelSinkProvider == null)
				this._iServerChannelSinkProvider = GenuineUtility.GetDefaultServerSinkChain();

			// collect sink providers' cookies
			IServerChannelSinkProvider sinkProvider = this._iServerChannelSinkProvider;
			while(sinkProvider != null)
			{
				sinkProvider.GetChannelData((IChannelDataStore) this.ChannelData);
				sinkProvider = sinkProvider.Next;
			}

			// register channel sink
			this._iServerChannelSink = new GenuineUniversalServerTransportSink(this, ChannelServices.CreateServerChannelSinkChain(this._iServerChannelSinkProvider, this),
				this.ITransportContext);
		}

		/// <summary>
		/// Gets or sets the underlying transport context.
		/// </summary>
		public ITransportContext ITransportContext
		{
			get
			{
				return this._iTransportContext;
			}
			set
			{
				if (this._iTransportContext != null)
					this._iTransportContext.IGenuineEventProvider.Unregister(this);

				this._iTransportContext = value;
				this._iTransportContext.IGenuineEventProvider.Register(this);
			}
		}
		private ITransportContext _iTransportContext;

		#region -- Sinks ---------------------------------------------------------------------------

		/// <summary>
		/// Gets the server channel sink.
		/// </summary>
		public IServerChannelSink IServerChannelSink
		{
			get
			{
				return this._iServerChannelSink;
			}
		}
		private IServerChannelSink _iServerChannelSink;

		/// <summary>
		/// Client channel sink provider.
		/// </summary>
		protected IClientChannelSinkProvider _iClientChannelSinkProvider;

		/// <summary>
		/// Server channel sink provider.
		/// </summary>
		private IServerChannelSinkProvider _iServerChannelSinkProvider;

		/// <summary>
		/// Gets the client transport sink.
		/// </summary>
		/// <returns>Returns client transport sink.</returns>
		protected virtual IClientChannelSinkProvider CreateClientChannelSinkProvider()
		{
			return new GenuineTcpClientTransportSinkProvider(this.ITransportContext);
		}

		#endregion

		#region -- BaseChannelWithProperties -------------------------------------------------------

		/// <summary>
		/// To implement Keys and Items members. See BaseChannelWithProperties for details.
		/// </summary>
		protected IDictionary _properties;

		/// <summary>
		/// Gets an ICollection of keys that the channel object properties are associated with.
		/// </summary>
		public override ICollection Keys
		{
			get
			{
				return _properties.Keys;
			}
		}

		/// <summary>
		/// Gets or sets the property associated with the specified key.
		/// </summary>
		public override object this[object key]
		{
			get
			{
				return _properties[key];
			}
			set
			{
				_properties[key] = value;
			}
		}

		#endregion

		#region -- IChannel ------------------------------------------------------------------------

		/// <summary>
		/// Gets the name of the channel.
		/// </summary>
		public string ChannelName
		{
			get
			{
				return _channelName;
			}
		}

		/// <summary>
		/// Channel name which is available via ChannelName member.
		/// </summary>
		protected string _channelName;

		/// <summary>
		/// Gets the priority of the channel.
		/// </summary>
		public int ChannelPriority
		{
			get
			{
				return _channelPriority;
			}
		}

		/// <summary>
		/// The channel priority which is available via the ChannelName member.
		/// </summary>
		protected int _channelPriority;

		/// <summary>
		/// Extracts the channel URI and the remote well known object URI from the specified URL.
		/// </summary>
		/// <param name="url">The URL from which to extract the URI of the remote well known object.</param>
		/// <param name="objectURI">When this method returns, contains a String that holds the URI of the remote well known object. This parameter is passed uninitialized.</param>
		/// <returns>The URI of the current channel.</returns>
		public string Parse(string url, out string objectURI)
		{
			objectURI = null;
			return null;
		}

		#endregion

		#region -- IChannelReceiver ----------------------------------------------------------------

		/// <summary>
		/// Gets the channel-specific data.
		/// </summary>
		public object ChannelData
		{
			get
			{
				if (_channelData == null)
					_channelData = new ChannelDataStore(this.GetUrlsForUri(string.Empty));
				return _channelData;
			}
		}

		/// <summary>
		/// Contains channel's information available via ChannelData property.
		/// </summary>
		protected ChannelDataStore _channelData;

		/// <summary>
		/// Returns an array of all the URLs for a URI.
		/// </summary>
		/// <param name="objectUri">The URI for which URLs are required.</param>
		/// <returns>An array of the URLs.</returns>
		public abstract string[] GetUrlsForUri(string objectUri);

		/// <summary>
		/// Instructs the current channel to start listening for requests.
		/// </summary>
		/// <param name="data">Optional initialization information.</param>
		public virtual void StartListening(object data)
		{
			this.ITransportContext.IGenuineEventProvider.Register(this);
			this.ReadSettings(this._properties);
		}

		/// <summary>
		/// Instructs the current channel to stop listening for requests.
		/// </summary>
		/// <param name="data">Optional state information for the channel.</param>
		public virtual void StopListening(object data)
		{
			this.ITransportContext.IGenuineEventProvider.Unregister(this);
		}

		#endregion

		#region -- IChannelSender ------------------------------------------------------------------

		/// <summary>
		/// Returns a channel message sink that delivers messages to the specified URL or channel data object.
		/// </summary>
		/// <param name="url">The url.</param>
		/// <param name="remoteChannelData">The information associated with the channel.</param>
		/// <param name="objectURI">The uri of the target.</param>
		/// <returns>A channel message sink that delivers messages to the specified URL or channel data object.</returns>
		public IMessageSink CreateMessageSink(string url, object remoteChannelData, out string objectURI)
		{
			objectURI = null;
			string channelUri = null;

			// if it's a well-known object
			if (url == null && remoteChannelData is IChannelDataStore)
				url = ((IChannelDataStore) remoteChannelData).ChannelUris[0];

			if (url == null)
				return null;

			// check whether we can service this url
			string[] channelPrefixes = this._possibleChannelPrefixes;
			int i = 0;
			for ( ; i < channelPrefixes.Length; i++)
				if (url.StartsWith(channelPrefixes[i]))
					break;

			// if the requested url wasn't recognized, it should be
			// directed to another channel
			if (i == channelPrefixes.Length)
				return null;

			channelUri = GenuineUtility.Parse(url, out objectURI);

			// if the provided URI is not registered at common URI Storage, nothing will be able
			// to send anything there
			// if it's registered, it does not matter whose sink services the connection.
			if (! GenuineUtility.CheckUrlOnConnectivity(url) && UriStorage.GetTransportContext(channelUri) == null)
				return null;

			if (channelUri == null)
				return null;

			return this._iClientChannelSinkProvider.CreateSink(this, url, remoteChannelData) as IMessageSink;
		}

		#endregion

		#region -- ISetSecurityContext -------------------------------------------------------------

		/// <summary>
		/// Gets or sets the current security context being used on the channel level.
		/// </summary>
		public SecuritySessionParameters SecuritySessionParameters
		{
			get
			{
				return this.ITransportContext.SecuritySessionParameters;
			}
			set
			{
				this.ITransportContext.SecuritySessionParameters = value;
			}
		}

		#endregion

		#region -- Channel Prefixes ----------------------------------------------------------------

		/// <summary>
		/// Gets the URL prefix of the channel.
		/// </summary>
		public string UrlPrefix
		{
			get
			{
				return _urlPrefix;
			}
		}

		/// <summary>
		/// The URL prefix of the channel.
		/// </summary>
		protected string _urlPrefix;

		/// <summary>
		/// Gets the internal URI prefix of the channel.
		/// </summary>
		public abstract string UriPrefix
		{
			get;
		}

		/// <summary>
		/// Contains possible channel prefixes (such as gtcp, ghttp) supported by this channel.
		/// </summary>
		protected string[] _possibleChannelPrefixes;

		#endregion

		#region -- IEventProvider ------------------------------------------------------------------

		/// <summary>
		/// This event is fired on any global event related to the current channel.
		/// </summary>
		public event GenuineChannelsGlobalEventHandler GenuineChannelsEvent
		{
			add
			{
				lock (_genuineChannelsEventLock)
					_genuineChannelsEvent += value;
			}
			remove
			{
				lock (_genuineChannelsEventLock)
					_genuineChannelsEvent += value;
			}
		}
		private event GenuineChannelsGlobalEventHandler _genuineChannelsEvent;
		private object _genuineChannelsEventLock = new object();

		/// <summary>
		/// Fires the GenuineChannelsGlobalEvent event with the specified parameters.
		/// </summary>
		/// <param name="e">Event arguments.</param>
		public void FireGenuineEvent(GenuineEventArgs e)
		{
			// LOG:
			BinaryLogWriter binaryLogWriter = this.ITransportContext.BinaryLogWriter;
			if ( binaryLogWriter != null && binaryLogWriter[LogCategory.ChannelEvent] > 0 )
			{
				binaryLogWriter.WriteEvent(LogCategory.ChannelEvent, "BasicChannelWithSecurity.FireGenuineEvent",
					LogMessageType.ChannelEvent, e.SourceException, null, e.HostInformation, null,
					GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
					null, null, -1, (int) e.EventType, 0, 0, null, null, null, null,
					"The channel event is raised.");
			}

			if (e.EventType == GenuineEventType.GTcpConnectionAccepted || e.EventType == GenuineEventType.GHttpConnectionAccepted)
				this.PerformEventSending(e);
			else
				GenuineThreadPool.QueueUserWorkItem(new WaitCallback(this.PerformEventSending), e, false);
		}

		/// <summary>
		/// Fires the event.
		/// </summary>
		/// <param name="genuineEventArgsAsObject">Event parameters.</param>
		public void PerformEventSending(object genuineEventArgsAsObject)
		{
			GenuineEventArgs genuineEventArgs = (GenuineEventArgs) genuineEventArgsAsObject;
			GenuineChannelsGlobalEventHandler clonedEvent = null;

			lock (_genuineChannelsEventLock)
			{
				if (_genuineChannelsEvent == null)
					return ;

				clonedEvent = (GenuineChannelsGlobalEventHandler) _genuineChannelsEvent.Clone();
			}

			clonedEvent(this, genuineEventArgs);
		}

		/// <summary>
		/// Genuine Event handler.
		/// </summary>
		/// <param name="genuineEventArgs">The event arguments.</param>
		public void OnGenuineEvent(GenuineEventArgs genuineEventArgs)
		{
			this.FireGenuineEvent(genuineEventArgs);
		}

		#endregion

		#region -- Settings ------------------------------------------------------------------------

		/// <summary>
		/// Reads settings.
		/// </summary>
		/// <param name="properties">Settings container to read from.</param>
		protected virtual void ReadSettings(IDictionary properties)
		{
			// retrieve settings
			foreach (DictionaryEntry entry in properties)
			{
				if (string.Compare(entry.Key.ToString(), "name", true) == 0)
					this._channelName = entry.Value.ToString();

				if (string.Compare(entry.Key.ToString(), "priority", true) == 0)
					this._channelPriority = GenuineUtility.SafeConvertToInt32(entry.Value, this._channelPriority);

				if (string.Compare(entry.Key.ToString(), "prefix", true) == 0)
				{
					this._urlPrefix = entry.Value.ToString();
					if (! this._urlPrefix.StartsWith("g") || this._urlPrefix.Length < 4 || this._urlPrefix.Length > 8)
						GenuineExceptions.Get_Channel_InvalidParameter("prefix");
				}
			}

			// enable compresion if such an option is specified
			if ((bool) this.ITransportContext.IParameterProvider[GenuineParameter.Compression])
				this.SecuritySessionParameters = SecuritySessionServices.DefaultContextWithCompression;

#if DEBUG
			try
			{
				// enable logging
				if (this.ITransportContext.IParameterProvider[GenuineParameter.EnableGlobalLoggingToFile] is string &&
					((string) this.ITransportContext.IParameterProvider[GenuineParameter.EnableGlobalLoggingToFile]).Length > 0)
				{
					GenuineLoggingServices.SetUpLoggingToFile(this.ITransportContext.IParameterProvider[GenuineParameter.EnableGlobalLoggingToFile] as string,
						this.ITransportContext.IParameterProvider[GenuineParameter.LoggingParameters] as string);
				}
				else if (this.ITransportContext.IParameterProvider[GenuineParameter.EnableGlobalLoggingToMemory] is int &&
					((int) this.ITransportContext.IParameterProvider[GenuineParameter.EnableGlobalLoggingToMemory]) > 0 )
				{
					GenuineLoggingServices.SetUpLoggingToMemory((int) this.ITransportContext.IParameterProvider[GenuineParameter.EnableGlobalLoggingToMemory],
						this.ITransportContext.IParameterProvider[GenuineParameter.LoggingParameters] as string);
				}
			}
			catch
			{
			}
#endif
		}

		#endregion

		#region -- ITransportUser ------------------------------------------------------------------

		/// <summary>
		/// Handles the incoming message.
		/// </summary>
		/// <param name="message">The message.</param>
		public void HandleMessage(Message message)
		{
			((GenuineUniversalServerTransportSink) this._iServerChannelSink).HandleIncomingMessage(message);
		}

		#endregion
	}
}
