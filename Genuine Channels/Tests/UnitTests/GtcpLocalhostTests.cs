using System;
using Belikov.GenuineChannels.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GenuineChannels.UnitTests.UnitTests
{
	[TestClass]
	public class GtcpLocalhostTests: RemoteServerTestBase
	{

		#region Test Setup
		public GtcpLocalhostTests()
			: base(GcChannelType.GTCP, () => new Service(), IpVersion.IPv4)
		{
		}

		protected override string ServiceAddress => "localhost";	// !!! This IS under test here. Should work for both IPv4 systems, and IPv6, incl. Dual stack systems
		protected override int Port => 8731;

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
			// note: localhost url should now work, although it may be resolved to IPv6 address
			var proxy = (IService)Activator.GetObject(typeof(IService), ServiceUri);
			var greeting = proxy.Greeting("World");

			Assert.AreEqual("Hello, World!", greeting);
		}

		[TestMethod]
		public void RemoteInvocationWithPostfix()
		{
			// note: localhost url should now work, although it may be resolved to IPv6 address
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

			// note: localhost url should now work, although it may be resolved to IPv6 address
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

			// note: localhost url should now work, although it may be resolved to IPv6 address
			var proxy = (IService)Activator.GetObject(typeof(IService), ServiceUriRem);

			using (new SecurityContextKeeper(parameters))
			{
				var greeting = proxy.Greeting("Compressed World");
				Assert.AreEqual("Hello, Compressed World!", greeting);
			}
		}
	}
}
