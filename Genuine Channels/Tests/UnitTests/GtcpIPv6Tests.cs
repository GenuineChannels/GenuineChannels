using System;
using Belikov.GenuineChannels.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GenuineChannels.UnitTests.UnitTests
{
	[TestClass]
	public class GtcpIPv6Tests: RemoteServerTestBase
	{
		#region Test Setup

		public GtcpIPv6Tests()
			: base(GcChannelType.GTCP, () => new Service(), IpVersion.IPv6)
		{
		} 
		#endregion

		#region Service def.

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
		#endregion

		[TestMethod]
		public void RemoteInvocation()
		{
			var proxy = (IService) Activator.GetObject(typeof(IService), ServiceUri); 
			var greeting = proxy.Greeting("World");

			Assert.AreEqual("Hello, World!", greeting);
		}

		[TestMethod]
		public void RemoteInvocationWithPostfix()
		{
			var proxy = (IService)Activator.GetObject(typeof(IService), ServiceUriRem);
			var greeting = proxy.Greeting("World");

			Assert.AreEqual("Hello, World!", greeting);
		}

		[TestMethod]
		public void RemoteInvocationWithCompression()
		{
			// test compression
			var parameters = new SecuritySessionParameters(
				SecuritySessionServices.DefaultContext.Name,
				SecuritySessionAttributes.EnableCompression,
				TimeSpan.FromSeconds(5));

			var proxy = (IService)Activator.GetObject(typeof(IService), ServiceUri); 

			using (new SecurityContextKeeper(parameters))
			{
				var greeting = proxy.Greeting("Compressed World");
				Assert.AreEqual("Hello, Compressed World!", greeting);
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

			var proxy = (IService)Activator.GetObject(typeof(IService), ServiceUriRem);

			using (new SecurityContextKeeper(parameters))
			{
				var greeting = proxy.Greeting("Compressed World");
				Assert.AreEqual("Hello, Compressed World!", greeting);
			}
		}
	}
}
