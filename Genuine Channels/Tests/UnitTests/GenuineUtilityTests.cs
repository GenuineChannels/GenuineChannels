using System;
using System.Collections.Generic;
using System.Net;
using Belikov.GenuineChannels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GenuineChannels.UnitTests.UnitTests
{
	[TestClass]
	public class GenuineUtilityTests
	{
		[TestMethod]
		public void Localhost_Resolves2IPv6()
		{
			var he = Dns.GetHostEntry("localhost");
			Console.WriteLine("localhost: {0}", he.AddressList[0]);
		}

		[TestMethod]
		public void SplitToHostAndPort_HostNames()
		{
			var testDict = new Dictionary<string, Tuple<string, int>>
			{
				{ "gtcp://localhost:1235", new Tuple<string, int>("localhost", 1235)},
				{ "gtcp://myserver:1236", new Tuple<string, int>("myserver", 1236)},
				{ "gtcp://server.domain.com:1237", new Tuple<string, int>("server.domain.com", 1237)},
				{ "gtcp://localhost:1235/subdomain", new Tuple<string, int>("localhost", 1235)},
				{ "gtcp://myserver:1236/a/b/c", new Tuple<string, int>("myserver", 1236)},
				{ "gtcp://server.domain.com:1237/default.aspx", new Tuple<string, int>("server.domain.com", 1237)}
			};

			RunSplitToHostAndPortWithInputDictionary(testDict);
		}

		[TestMethod]
		public void SplitToHostAndPort_IPv4()
		{
			var testDict = new Dictionary<string,Tuple<string,int>>
			{
				{ "gtcp://127.0.0.1:1234", new Tuple<string, int>("127.0.0.1", 1234)},
				{ "gtcp://198.11.34.2:1235", new Tuple<string, int>("198.11.34.2", 1235)},
				{ "gtcp://127.0.0.1:1234/subdomain", new Tuple<string, int>("127.0.0.1", 1234)},
				{ "gtcp://198.11.34.2:1235/a/b/c", new Tuple<string, int>("198.11.34.2", 1235)}
			};

			RunSplitToHostAndPortWithInputDictionary(testDict);
		}

		[TestMethod]
		public void SplitToHostAndPort_IPv6()
		{
			var testDict = new Dictionary<string, Tuple<string, int>>
			{
				{ "gtcp://::1:1233", new Tuple<string, int>("::1", 1233)},
				{ "gtcp://fec0:0:0:ffff::1:1234", new Tuple<string, int>("fec0:0:0:ffff::1", 1234)},
				{ "gtcp://fec0:0:0:ffff::1%1:1235", new Tuple<string, int>("fec0:0:0:ffff::1%1", 1235)},
				{ "gtcp://fe80::1d2b:147c:c69a:26ef%30:1236", new Tuple<string, int>("fe80::1d2b:147c:c69a:26ef%30", 1236)},
				{ "gtcp://::1", new Tuple<string, int>("::1", 0)},
				{ "gtcp://fec0:0:0:ffff::1%1", new Tuple<string, int>("fec0:0:0:ffff::1%1", 0)},
				{ "gtcp://fe80::1d2b:147c:c69a:26ef%30", new Tuple<string, int>("fe80::1d2b:147c:c69a:26ef%30", 0)},
				{ "gtcp://::1:1233/subdomain", new Tuple<string, int>("::1", 1233)},
				{ "gtcp://fec0:0:0:ffff::1:1234/a/b/c", new Tuple<string, int>("fec0:0:0:ffff::1", 1234)},
				{ "gtcp://fec0:0:0:ffff::1%1:1235/index.rem", new Tuple<string, int>("fec0:0:0:ffff::1%1", 1235)},
				{ "gtcp://fe80::1d2b:147c:c69a:26ef%30:1236/service", new Tuple<string, int>("fe80::1d2b:147c:c69a:26ef%30", 1236)},
				{ "gtcp://::1/myservices", new Tuple<string, int>("::1", 0)},
				{ "gtcp://fec0:0:0:ffff::1%1/a/b/c", new Tuple<string, int>("fec0:0:0:ffff::1%1", 0)},
				{ "gtcp://fe80::1d2b:147c:c69a:26ef%30/index.rem", new Tuple<string, int>("fe80::1d2b:147c:c69a:26ef%30", 0)}
			};

			RunSplitToHostAndPortWithInputDictionary(testDict);
		}

		private static void RunSplitToHostAndPortWithInputDictionary(Dictionary<string, Tuple<string, int>> testDict)
		{
			foreach (var entry in testDict)
			{
				try
				{
					int port;
					Assert.AreEqual(entry.Value.Item1, GenuineUtility.SplitToHostAndPort(entry.Key, out port), true,
						"-> Host name of Entry Key: {0}", entry.Key);
					Assert.AreEqual(entry.Value.Item2, port, "-> Port of Entry Key: {0}", entry.Key);
				}
				catch (Exception ex)
				{
					Assert.Fail("{0}: {1} -> Port of Entry Key: {2}", ex.GetType().Name, ex.Message, entry.Key);
				}
			}
		}
	}
}
