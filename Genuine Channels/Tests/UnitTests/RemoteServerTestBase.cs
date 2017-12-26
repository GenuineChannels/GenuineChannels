using System;
using System.Collections.Generic;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using Belikov.GenuineChannels.GenuineTcp;
using Belikov.GenuineChannels.GenuineXHttp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GenuineChannels.UnitTests.UnitTests
{
	public enum GcChannelType
	{
		GTCP,
		GHTTP
	}

	public enum IpVersion
	{
		IPv4,
		IPv6
	}

	public abstract class RemoteServerTestBase
	{
		private readonly GcChannelType _channelType;
		private readonly IpVersion _ipVersion;
		private readonly Func<MarshalByRefObject> _serviceInstanceCreatorFunction;

		protected RemoteServerTestBase(GcChannelType channelType, Func<MarshalByRefObject> serviceInstanceCreatorFunction, IpVersion ipVersion = IpVersion.IPv4)
		{
			_channelType = channelType;
			_ipVersion = ipVersion;
			if (serviceInstanceCreatorFunction == null)
				throw new ArgumentNullException(nameof(serviceInstanceCreatorFunction));
			_serviceInstanceCreatorFunction = serviceInstanceCreatorFunction;
		}

		protected virtual string ServiceName => GetType().Name;
		protected virtual string ServiceNameRemPostfixed => string.Format("{0}.rem", ServiceName);
		protected virtual string GcChannelName => GcUriScheme;
		protected virtual int Port => 8737;
		protected virtual string ListenerInterface => _ipVersion == IpVersion.IPv4 ? null : "::1"; // IPv4: "0.0.0.0" to be reachable within local network / default
		protected virtual int Priority => 100;
		protected virtual string ServiceAddress => _ipVersion == IpVersion.IPv4
			? System.Net.IPAddress.Loopback.ToString()
			: System.Net.IPAddress.IPv6Loopback.ToString();


		protected string ServiceUri => string.Format("{0}/{1}", ServiceUriBase, ServiceName);
		protected string ServiceUriRem => string.Format("{0}/{1}", ServiceUriBase, ServiceNameRemPostfixed);

	
		/// <summary>
		/// Starts the server. Runs for every test method.
		/// </summary>
		[TestInitialize]
		public void StartServer()
		{
			ServerChannel = RegisterChannel(server: true);

			if (!string.IsNullOrEmpty(ServiceName))
			{
				ServiceObj = GuardedCreateService();
				ServiceObjRef = RemotingServices.Marshal(ServiceObj, ServiceName);
			}
			if (!string.IsNullOrEmpty(ServiceNameRemPostfixed))
			{
				ServiceWithPostfixObj = GuardedCreateService();
				ServiceWithPostfixObjRef = RemotingServices.Marshal(ServiceWithPostfixObj, ServiceNameRemPostfixed);
			}
		}

		/// <summary>
		/// Stops the server. Runs for every test method.
		/// </summary>
		[TestCleanup]
		public void StopServer()
		{
			if (ServiceObj != null)
			{
				RemotingServices.Unmarshal(ServiceObjRef);
				RemotingServices.Disconnect(ServiceObj);
			}
			if (ServiceWithPostfixObj != null)
			{
				RemotingServices.Unmarshal(ServiceWithPostfixObjRef);
				RemotingServices.Disconnect(ServiceWithPostfixObj);
			}

			ChannelServices.UnregisterChannel(ServerChannel);

			ServiceObj = null;
			ServiceWithPostfixObj = null;
			ServerChannel = null;
		}

	
		private string ServiceUriBase => string.Format("{0}://{1}:{2}", GcUriScheme, ServiceAddress, Port);
		private IChannel ServerChannel { get; set; }
		private MarshalByRefObject ServiceObj { get; set; }
		private MarshalByRefObject ServiceWithPostfixObj { get; set; }
		private ObjRef ServiceObjRef { get; set; }
		private ObjRef ServiceWithPostfixObjRef { get; set; }
		
		
		private string GcUriScheme
		{
			get
			{
				switch (_channelType)
				{
					case GcChannelType.GHTTP: return "ghttp";
					case GcChannelType.GTCP: return "gtcp";
					default: throw new NotImplementedException(_channelType.ToString());
				}
			}
		}

		private IChannel RegisterChannel(bool server = false)
		{
			var props = new Dictionary<string, string>
			{
				{ "name", GcChannelName },
				{ "priority", Priority.ToString() }
			};

			if (server)
			{
				props["port"] = Port.ToString();
				if (!string.IsNullOrEmpty(ListenerInterface))
					props["interface"] = ListenerInterface;
			}

			IChannel channel;
			switch (_channelType)
			{
				case GcChannelType.GHTTP: channel = new GenuineXHttpChannel(props, null, null);
					break;
				case GcChannelType.GTCP: channel = new GenuineTcpChannel(props, null, null);
					break;
				default: throw new NotImplementedException(_channelType.ToString());
			}
			
			ChannelServices.RegisterChannel(channel, false);
			return channel;
		}

		private MarshalByRefObject GuardedCreateService()
		{
			var service = _serviceInstanceCreatorFunction();
			if (service == null)
				throw new InvalidOperationException("Service creator function MUST return a service instance");
			return service;
		}
	}
}
