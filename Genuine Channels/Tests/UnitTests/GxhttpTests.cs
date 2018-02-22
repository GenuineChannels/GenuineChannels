using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Text;
using Belikov.GenuineChannels;
using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.GenuineTcp;
using Belikov.GenuineChannels.GenuineXHttp;
using Belikov.GenuineChannels.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GenuineChannels.UnitTests.UnitTests
{
	[TestClass]
	public class GxhttpTests
	{
		[ClassInitialize]
		public static void StartServer(TestContext ctx)
		{
			ServerChannel = RegisterChannel(server: true);

			var service = new Service();
			ServiceObjRef = RemotingServices.Marshal(service, "GxttpTestService");

			var serviceWithPostfix = new Service();
			ServiceWithPostfixObjRef = RemotingServices.Marshal(serviceWithPostfix, "GxttpTestService.rem");
		}

		[ClassCleanup]
		public static void StopServer()
		{
			RemotingServices.Unmarshal(ServiceObjRef);
			RemotingServices.Unmarshal(ServiceWithPostfixObjRef);
			ChannelServices.UnregisterChannel(ServerChannel);
		}

		public static IChannel ServerChannel { get; set; }

		public static ObjRef ServiceObjRef { get; set; }

		public static ObjRef ServiceWithPostfixObjRef { get; set; }

		private static IChannel RegisterChannel(bool server = false)
		{
			var props = new Dictionary<string, string>
			{
				{ "name", "GXHTTP" },
				{ "priority", "100" }
			};

			if (server)
			{
				props["port"] = "8739";
			}

			var channel = new GenuineXHttpChannel(props, null, null);
			ChannelServices.RegisterChannel(channel, false);
			return channel;
		}

		internal class Service : MarshalByRefObject, IService
		{
			public string Greeting(string s)
			{
				var result = $"Goodbye, {s}!";
				Console.WriteLine("Replying to client: {0}", result);
				return result;
			}
		}

		public interface IService
		{
			string Greeting(string s);
		}

		[TestMethod]
		public void RemoteInvocation()
		{
			// note: localhost url doesn't work because it resolves to IPv6 address
			var proxy = (IService)Activator.GetObject(typeof(IService), "ghttp://127.0.0.1:8739/GxttpTestService");
			var greeting = proxy.Greeting("World");

			Assert.AreEqual("Goodbye, World!", greeting);
		}

		[TestMethod]
		public void RemoteInvocationWithPostfix()
		{
			// note: localhost url doesn't work because it resolves to IPv6 address
			var proxy = (IService)Activator.GetObject(typeof(IService), "ghttp://127.0.0.1:8739/GxttpTestService.rem");
			var greeting = proxy.Greeting("World");

			Assert.AreEqual("Goodbye, World!", greeting);
		}

		[TestMethod, Ignore] // GenuineTcpClientTransportSink throws an exception while it shouldn't take part in the xhttp sink chain
		public void RemoteInvocationWithCompression()
		{
			// test compression
			var parameters = new SecuritySessionParameters(
				SecuritySessionServices.DefaultContext.Name,
				SecuritySessionAttributes.EnableCompression,
				TimeSpan.FromSeconds(5));

			// note: localhost url doesn't work because it resolves to IPv6 address
			var proxy = (IService)Activator.GetObject(typeof(IService), "ghttp://127.0.0.1:8739/GxttpTestService");

			using (new SecurityContextKeeper(parameters))
			{
				var greeting = proxy.Greeting("Compressed World");
				Assert.AreEqual("Goodbye, Compressed World!", greeting);
			}
		}

		[TestMethod]
		public void RemoteInvocationWithCompressionAndPostfix()
		{
			// test compression
			var parameters = new SecuritySessionParameters(
				SecuritySessionServices.DefaultContext.Name,
				SecuritySessionAttributes.EnableCompression,
				TimeSpan.FromSeconds(5));

			// note: localhost url doesn't work because it resolves to IPv6 address
			var proxy = (IService)Activator.GetObject(typeof(IService), "ghttp://127.0.0.1:8739/GxttpTestService.rem");

			using (new SecurityContextKeeper(parameters))
			{
				var greeting = proxy.Greeting("Compressed World");
				Assert.AreEqual("Goodbye, Compressed World!", greeting);
			}
		}
	}
}
