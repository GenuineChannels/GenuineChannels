using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Threading.Tasks;
using Belikov.GenuineChannels.GenuineTcp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GenuineChannels.UnitTests.UnitTests
{
	[TestClass]
	public class GtcpTests
	{
		[ClassInitialize]
		public static void StartServer(TestContext ctx)
		{
			ServerChannel = RegisterChannel(server: true);

			var service = new Service();
			ServiceObjRef = RemotingServices.Marshal(service, "GtcpTestService");
		}

		[ClassCleanup]
		public static void StopServer()
		{
			RemotingServices.Unmarshal(ServiceObjRef);
			ChannelServices.UnregisterChannel(ServerChannel);
		}

		public static IChannel ServerChannel { get; set; }

		public static ObjRef ServiceObjRef { get; set; }

		private static IChannel RegisterChannel(bool server = false)
		{
			var props = new Dictionary<string, string>
			{
				{ "name", "GTCP" },
				{ "priority", "100" }
			};

			if (server)
			{
				props["port"] = "8737";
			}

			var channel = new GenuineTcpChannel(props, null, null);
			ChannelServices.RegisterChannel(channel, false);
			return channel;
		}

		internal class Service : MarshalByRefObject, IService
		{
			public string Greeting(string s)
			{
				var result = $"Hello, {s}!";
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
			var proxy = (IService)Activator.GetObject(typeof(IService), "gtcp://127.0.0.1:8737/GtcpTestService");
			var greeting = proxy.Greeting("World");

			Assert.AreEqual("Hello, World!", greeting);
		}
	}
}
