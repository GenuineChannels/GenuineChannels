/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;

using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.DirectExchange;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Parameters;
using Belikov.GenuineChannels.Receiving;
using Belikov.GenuineChannels.Security;

using Belikov.GenuineChannels.GenuineSharedMemory;
using Belikov.GenuineChannels.GenuineTcp;
using Belikov.GenuineChannels.GenuineUdp;
using Belikov.GenuineChannels.GenuineHttp;
using Belikov.GenuineChannels.GenuineXHttp;

namespace Belikov.GenuineChannels.TransportContext
{
	/// <summary>
	/// Provides a set of static methods to aid with Transport Context.
	/// </summary>
	public class TransportContextServices
	{
		/// <summary>
		/// Creates TransportContext with the default settings.
		/// </summary>
		/// <param name="properties">Channel's parameters.</param>
		/// <param name="defaultTransportUser">Default Transport User.</param>
		/// <returns>The transport context with default transport capabilities.</returns>
		public static ITransportContext CreateDefaultTcpContext(IDictionary properties, ITransportUser defaultTransportUser)
		{
			TransportContext transportContext = new TransportContext();
			transportContext._iKeyStore = new KeyStore();
			transportContext._knownHosts = new KnownHosts(transportContext);
			transportContext._iParameterProvider = new ReadingCascadeParameterProvider(properties, new DefaultParameterProvider());
			transportContext._iGenuineEventProvider = new GenuineEventProvider(transportContext);
//			transportContext._iEventLogger = GenuineLoggingServices.BinaryLogWriter;
			transportContext._directExchangeManager = new DirectExchangeManager(transportContext);
			transportContext._connectionManager = new TcpConnectionManager(transportContext);
			transportContext._iIncomingStreamHandler = new GenuineReceivingHandler(transportContext, defaultTransportUser);

			return transportContext;
		}

		/// <summary>
		/// Creates TransportContext with the default settings.
		/// </summary>
		/// <param name="properties">Channel's parameters.</param>
		/// <param name="defaultTransportUser">Default Transport User.</param>
		/// <returns>The transport context with default transport capabilities.</returns>
		public static ITransportContext CreateDefaultSharedMemoryContext(IDictionary properties, ITransportUser defaultTransportUser)
		{
			TransportContext transportContext = new TransportContext();
			transportContext._iKeyStore = new KeyStore();
			transportContext._knownHosts = new KnownHosts(transportContext);
			transportContext._iParameterProvider = new ReadingCascadeParameterProvider(properties, new DefaultParameterProvider());
			transportContext._iGenuineEventProvider = new GenuineEventProvider(transportContext);
//			transportContext._iEventLogger = GenuineLoggingServices.BinaryLogWriter;
			transportContext._directExchangeManager = new DirectExchangeManager(transportContext);
			transportContext._connectionManager = new SharedMemoryConnectionManager(transportContext);
			transportContext._iIncomingStreamHandler = new GenuineReceivingHandler(transportContext, defaultTransportUser);

			return transportContext;
		}

		/// <summary>
		/// Creates TransportContext with the default settings.
		/// </summary>
		/// <param name="properties">Channel's parameters.</param>
		/// <param name="defaultTransportUser">Default Transport User.</param>
		/// <returns>The transport context with default transport capabilities.</returns>
		public static ITransportContext CreateDefaultUdpContext(IDictionary properties, ITransportUser defaultTransportUser)
		{
			TransportContext transportContext = new TransportContext();
			transportContext._iKeyStore = new KeyStore();
			transportContext._knownHosts = new KnownHosts(transportContext);
			transportContext._iParameterProvider = new ReadingCascadeParameterProvider(properties, new DefaultParameterProvider());
			transportContext._iGenuineEventProvider = new GenuineEventProvider(transportContext);
//			transportContext._iEventLogger = GenuineLoggingServices.BinaryLogWriter;
			transportContext._directExchangeManager = new DirectExchangeManager(transportContext);
			transportContext._connectionManager = new UdpConnectionManager(transportContext);
			transportContext._iIncomingStreamHandler = new GenuineReceivingHandler(transportContext, defaultTransportUser);

			return transportContext;
		}

		/// <summary>
		/// Creates TransportContext with the default settings.
		/// </summary>
		/// <param name="properties">Channel's parameters.</param>
		/// <param name="defaultTransportUser">Default Transport User.</param>
		/// <returns>The transport context with default transport capabilities.</returns>
		public static ITransportContext CreateDefaultClientHttpContext(IDictionary properties, ITransportUser defaultTransportUser)
		{
			TransportContext transportContext = new TransportContext();
			transportContext._iKeyStore = new KeyStore();
			transportContext._knownHosts = new KnownHosts(transportContext);
			transportContext._iParameterProvider = new ReadingCascadeParameterProvider(properties, new DefaultParameterProvider());
			transportContext._iGenuineEventProvider = new GenuineEventProvider(transportContext);
//			transportContext._iEventLogger = GenuineLoggingServices.BinaryLogWriter;
			transportContext._directExchangeManager = new DirectExchangeManager(transportContext);
			transportContext._connectionManager = new HttpClientConnectionManager(transportContext);
			transportContext._iIncomingStreamHandler = new GenuineReceivingHandler(transportContext, defaultTransportUser);

			return transportContext;
		}

		/// <summary>
		/// Creates TransportContext with the default settings.
		/// </summary>
		/// <param name="properties">Channel's parameters.</param>
		/// <param name="defaultTransportUser">Default Transport User.</param>
		/// <returns>The transport context with default transport capabilities.</returns>
		public static ITransportContext CreateDefaultServerHttpContext(IDictionary properties, ITransportUser defaultTransportUser)
		{
			TransportContext transportContext = new TransportContext();
			transportContext._iKeyStore = new KeyStore();
			transportContext._knownHosts = new KnownHosts(transportContext);
			transportContext._iParameterProvider = new ReadingCascadeParameterProvider(properties, new DefaultParameterProvider());
			transportContext._iGenuineEventProvider = new GenuineEventProvider(transportContext);
//			transportContext._iEventLogger = GenuineLoggingServices.BinaryLogWriter;
			transportContext._directExchangeManager = new DirectExchangeManager(transportContext);
			transportContext._connectionManager = new HttpServerConnectionManager(transportContext);
			transportContext._iIncomingStreamHandler = new GenuineReceivingHandler(transportContext, defaultTransportUser);

			return transportContext;
		}

		/// <summary>
		/// Creates TransportContext with the default settings.
		/// </summary>
		/// <param name="properties">Channel's parameters.</param>
		/// <param name="defaultTransportUser">Default Transport User.</param>
		/// <returns>The transport context with default transport capabilities.</returns>
		public static ITransportContext CreateDefaultXHttpContext(IDictionary properties, ITransportUser defaultTransportUser)
		{
			TransportContext transportContext = new TransportContext();
			transportContext._iKeyStore = new KeyStore();
			transportContext._knownHosts = new KnownHosts(transportContext);
			transportContext._iParameterProvider = new ReadingCascadeParameterProvider(properties, new DefaultParameterProvider());
			transportContext._iGenuineEventProvider = new GenuineEventProvider(transportContext);
			transportContext._directExchangeManager = new DirectExchangeManager(transportContext);
			transportContext._connectionManager = new XHttpConnectionManager(transportContext);
			transportContext._iIncomingStreamHandler = new GenuineReceivingHandler(transportContext, defaultTransportUser);

			return transportContext;
		}

	}
}
