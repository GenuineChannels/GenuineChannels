/* Genuine Channels product.
 *
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 *
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Principal;
using System.Web;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Serialization;

using Belikov.GenuineChannels.BufferPooling;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Security;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Utilities;

namespace Belikov.GenuineChannels
{
	/// <summary>
	/// Utility that incapsulates common operations for Genuine channel.
	/// </summary>
	public class GenuineUtility
	{
		/// <summary>
		/// Extracts the channel URI and the remote well-known object URI from the specified URL.
		/// </summary>
		/// <param name="url">The URL from which to extract the URI of the remote well known object.</param>
		/// <param name="objectURI">When this method returns, contains a String that holds the URI of the remote well known object. This parameter is passed uninitialized.</param>
		/// <returns>The URI of the current channel.</returns>
		public static string Parse(string url, out string objectURI)
		{
			objectURI = null;

			if (url == null || url.Length <= 0)
				return null;

			if (url.StartsWith("ghttp") || url.StartsWith("gxhttp"))
			{
				int pos = url.LastIndexOf('/');
				if (pos > 0)
					objectURI = url.Substring(pos + 1);
				var postfix = string.Empty;
				int postfixPos = objectURI.LastIndexOf('.');
				if (postfixPos >= 0)
					postfix = objectURI.Substring(postfixPos);
				return url.Substring(0, pos+1) + "fake" + postfix;
			}

			// look for _?g.+://
			if ( !( url[0] == 'g' || (url[0] == '_' && url[1] == 'g')) )
				return null;

			int startOfUri = url.IndexOf("://", 3);
			if (startOfUri < 0)
				return null;

			// read remote host uri
			int objectUriStartAt = url.IndexOf('/', startOfUri + 3);
			if (objectUriStartAt < 0)
				return url;

			objectURI = url.Substring(objectUriStartAt + 1);
			return url.Substring(0, objectUriStartAt);
//
//
//			Match match = _parseUrl.Match(url);
//			if (! match.Success)
//				return null;
//
//			objectURI = match.Groups["objectUri"].Value;
//			return match.Groups["channel"].Value;
		}
//		private static Regex _parseUrl = new Regex(@"^(?<channel>_?g(?<prefix>\w+)://[^/]+)/?(?<objectUri>.*)",
//			RegexOptions.IgnoreCase | RegexOptions.Compiled);

		/// <summary>
		/// Extracts the destination URI from the specified URL.
		/// </summary>
		/// <param name="url">The URL from which to extract the URI of the remote well known object.</param>
		/// <param name="objectURI">When this method returns, contains a String that holds the URI of the remote well known object. This parameter is passed uninitialized.</param>
		/// <returns>The URI of the current channel.</returns>
		public static string ParseUrl(string url, out string objectURI)
		{
			objectURI = null;

			if (url == null || url.Length <= 0)
				return null;

			// look for _?g.+://
			if ( !( url[0] == 'g' || (url[0] == '_' && url[1] == 'g')) )
				return null;

			int startOfUri = url.IndexOf("://", 3);
			if (startOfUri < 0)
				return null;

			// read remote host uri
			int objectUriStartAt = url.IndexOf('/', startOfUri + 3);
			if (objectUriStartAt < 0)
				return url;

			objectURI = url.Substring(objectUriStartAt + 1);
			return url.Substring(0, objectUriStartAt);
		}

		/// <summary>
		/// Returns channel prefix.
		/// </summary>
		/// <param name="url">Url to fetch prefix from.</param>
		/// <returns>The fetched prefix or a null reference.</returns>
		public static string FetchChannelPrefix(string url)
		{
			int colonIndex = url.IndexOf(':');
			if (colonIndex <= -1)
				return string.Empty;

			if (url.StartsWith("_g"))
				return url.Substring(2, colonIndex - 2);
			if (url[0] == 'g')
				return url.Substring(1, colonIndex - 1);
			return string.Empty;
		}

		/// <summary>
		/// Splits the given url to a host name and a port.
		/// </summary>
		/// <param name="url">The url to be splitted.</param>
		/// <param name="port">Out parameter. The fetched port.</param>
		/// <returns>The host name.</returns>
		public static string SplitToHostAndPort(string url, out int port)
		{
			port = 0;

			Match match = _UrlToHost.Match(url);
			if (!match.Success)
				return null;

			url = match.Groups["hostName"].Value;	// may include: port; but stripped scheme etc.
			int slashIndex = url.IndexOf('/');		// strip postfix chars
			if (slashIndex >= 0)
				url = url.Substring(0, slashIndex);

			int firstColonIndex = url.IndexOf(':');
			
			if (firstColonIndex < 0)
				return url; // no port at all

			int lastColonIndex = url.LastIndexOf(':');
			var isIpv6 = firstColonIndex < lastColonIndex;

			var namePart = url.Substring(0, lastColonIndex);
			var portPart = url.Substring(lastColonIndex + 1);

			if (isIpv6)
			{
				var name2Check = namePart;

				// see https://en.wikipedia.org/wiki/IPv6_address#Literal_IPv6_addresses_in_network_resource_identifiers
				var scopedLiteralAddressIndex = name2Check.IndexOf('%');
				if (scopedLiteralAddressIndex > 0)	// strip scope(s), not included in regex used
					name2Check = name2Check.Substring(0, scopedLiteralAddressIndex);

				if (!string.IsNullOrEmpty(portPart))
				{
					scopedLiteralAddressIndex = portPart.IndexOf('%');
					if (scopedLiteralAddressIndex >= 0)
					{	// port is not port, it is part of the address:
						name2Check += ":" + portPart.Substring(0, scopedLiteralAddressIndex);
						portPart = null;
						namePart = url;
					}
				}

				match = _Ipv6Address.Match(name2Check);	
				if (!match.Success && !string.IsNullOrEmpty(portPart))
				{
					//  append stripped port for check  (here port without scope(s))
					name2Check += ":" + portPart;

					match = _Ipv6Address.Match(name2Check); 
					if (!match.Success)
						return null;
					// the port is not a port: it is part of the IPv6 address
					portPart = null;
					namePart = url;
				}
			}

			if (!string.IsNullOrEmpty(portPart))	// convert
				port = Convert.ToInt32(portPart);

			return namePart;
		}
		private static readonly Regex _UrlToHost = new Regex(@"^g\w+://(?<hostName>.*)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled);
		// see: https://regex101.com/r/cT0hV4/5
		private static readonly Regex _Ipv6Address = new Regex(@"(?:^|(?<=\s))(([0-9a-fA-F]{1,4}:){7,7}[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,7}:|([0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,5}(:[0-9a-fA-F]{1,4}){1,2}|([0-9a-fA-F]{1,4}:){1,4}(:[0-9a-fA-F]{1,4}){1,3}|([0-9a-fA-F]{1,4}:){1,3}(:[0-9a-fA-F]{1,4}){1,4}|([0-9a-fA-F]{1,4}:){1,2}(:[0-9a-fA-F]{1,4}){1,5}|[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})|:((:[0-9a-fA-F]{1,4}){1,7}|:)|fe80:(:[0-9a-fA-F]{0,4}){0,4}%[0-9a-zA-Z]{1,}|::(ffff(:0{1,4}){0,1}:){0,1}((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])|([0-9a-fA-F]{1,4}:){1,4}:((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9]))(?=\s|$)",
			RegexOptions.IgnoreCase | RegexOptions.Compiled);

		

		/// <summary>
		/// Splits the given url to a host name and a port.
		/// </summary>
		/// <param name="url">The url to be splitted.</param>
		/// <param name="port">The fetched port.</param>
		/// <returns>The uri.</returns>
		public static string SplitHttpLinkToHostAndPort(string url, out int port)
		{
			//TODO: rework for IPv6
			port = 80;

			Match match = _splitHttpLinkToHostAndPort.Match(url);
			if (! match.Success)
				return null;

			if (match.Groups["port"].Value != null && match.Groups["port"].Value != string.Empty)
				port = Convert.ToInt32(match.Groups["port"].Value);
			return match.Groups["hostName"].Value;
		}
		private static Regex _splitHttpLinkToHostAndPort = new Regex(@"^g\w+://(?<hostName>[^:/\\]+)(:(?<port>\d+))?",
			RegexOptions.IgnoreCase | RegexOptions.Compiled);


		/// <summary>
		/// Determines whether [is i PV4 mapped to i PV6] [the specified address].
		/// </summary>
		/// <param name="address">The address.</param>
		/// <returns><c>true</c> if [is i PV4 mapped to i PV6] [the specified address]; otherwise, <c>false</c>.</returns>
		public static bool IsIPv4MappedToIPv6(IPAddress address)
		{
#if !FRM20
			return address.IsIPv4MappedToIPv6;
#else
			//DotPeek adopted code, keep CLR 3.5 compat.:
			if (address.AddressFamily != AddressFamily.InterNetworkV6)
			{
				return false;
			}

			int NumberOfLabels = 8;// = IPv6AddressBytes (= 16) / 2
			ushort[] mNumbers = new ushort[NumberOfLabels];

			var addressBytes = address.GetAddressBytes();
			// convert to internal IPv6 array structure:
			for (int i = 0; i < NumberOfLabels; i++)
			{
				mNumbers[i] = (ushort)(addressBytes[i * 2] * 256 + addressBytes[i * 2 + 1]);
			}
			// check:
			for (int i = 0; i < 5; i++)
			{
				if (mNumbers[i] != 0)
				{
					return false;
				}
			}
			return (mNumbers[5] == 0xFFFF);
#endif
		}

		// IPv4 192.168.1.1 maps as ::FFFF:192.168.1.1
		/// <summary>
		/// Maps to an IPV6 address.
		/// </summary>
		/// <param name="address">The address.</param>
		/// <returns>IPAddress.</returns>
		public static IPAddress MapToIPv6(IPAddress address)
		{
#if !FRM20
			return address.MapToIPv6();
#else
			//DotPeek adopted code, keep CLR 3.5 compat.:
			if (address.AddressFamily == AddressFamily.InterNetworkV6)
			{
				return address;
			}

			int NumberOfLabels = 8;// = IPv6AddressBytes (= 16) / 2
			ushort[] mNumbers = new ushort[NumberOfLabels];

			var addressBytes = address.GetAddressBytes();
			// convert to internal IPv6 array structure:
			for (int i = 0; i < NumberOfLabels; i++)
			{
				mNumbers[i] = (ushort)(addressBytes[i * 2] * 256 + addressBytes[i * 2 + 1]);
			}

			var bytes = new byte[NumberOfLabels * 2];

			int j = 0;
			for (int i = 0; i < NumberOfLabels; i++)
			{
				bytes[j++] = (byte)((mNumbers[i] >> 8) & 0xFF);
				bytes[j++] = (byte)((mNumbers[i]) & 0xFF);
			}
			return new IPAddress(bytes, 0);
#endif
		}


		/// <summary>
		/// Gets a value indicating whether the local system supports IPV6.
		/// </summary>
		/// <value><c>true</c> if [local system supports i PV6]; otherwise, <c>false</c>.</value>
		public static bool LocalSystemSupportsIPv6
		{
			get
			{
				if (Environment.OSVersion.Version.Major >= 6)
				{
					var he = Dns.GetHostEntry("localhost");
					foreach (var address in he.AddressList)
						if (address.AddressFamily == AddressFamily.InterNetworkV6)
							return true;
				}
				return false;
			}
		}
		/// <summary>
		/// Returns true if it is possible to connect to the given url.
		/// </summary>
		/// <param name="url">Url to check.</param>
		/// <returns>True if it is possible to connect to the given url.</returns>
		public static bool CheckUrlOnConnectivity(string url)
		{
			if (url.StartsWith("_g"))
				return false;
			return true;
		}

		/// <summary>
		/// Converts the specified TimeSpan into its integer representation expressed in milliseconds.
		/// </summary>
		/// <param name="timeSpanAsObject">The TimeSpan value to be converted.</param>
		/// <returns>Integer representation of the TimeSpan expressed in milliseconds.</returns>
		public static int ConvertToMilliseconds(object timeSpanAsObject)
		{
			TimeSpan timeSpan = (TimeSpan) timeSpanAsObject;
			return (int) timeSpan.TotalMilliseconds;
		}

		/// <summary>
		/// Tries to convert given parameter into Int32.
		/// Returns errorValue if it is not possible.
		/// </summary>
		/// <param name="str">The string to be converted.</param>
		/// <param name="errorValue">The integer value to be returned if a string can not be converted into integer value.</param>
		/// <returns>Integer representation of the string or given errorValue.</returns>
		public static int SafeConvertToInt32(object str, int errorValue)
		{
			if (str == null || (str as string) == string.Empty)
				return errorValue;

			try
			{
				return Convert.ToInt32(str);
			}
			catch(Exception)
			{
			}

			return errorValue;
		}

		/// <summary>
		/// Tries to convert given parameter into boolean.
		/// Returns errorValue if it is not possible.
		/// </summary>
		/// <param name="str">The string to be converted.</param>
		/// <param name="errorValue">The bool value returned if the string can not be converted into bool value.</param>
		/// <returns>Bool representation of the string or errorValue.</returns>
		public static bool SafeConvertToBool(object str, bool errorValue)
		{
			try
			{
				return Convert.ToBoolean(str);
			}
			catch(Exception)
			{
				return errorValue;
			}
		}

		/// <summary>
		/// Tries to convert given parameter into Int32 type.
		/// Returns a null reference if it's impossible.
		/// </summary>
		/// <param name="str">The string reperesented as the reference to an object.</param>
		/// <returns>Boxed Int32 value or a null reference.</returns>
		public static object SafeConvertToInt32(object str)
		{
			if (str == null || (str as string) == string.Empty)
				return null;

			try
			{
				return Convert.ToInt32(str);
			}
			catch(Exception)
			{
			}

			return null;
		}

		/// <summary>
		/// Tries to convert the given parameter into the boolean type.
		/// Returns a null reference if it is impossible.
		/// </summary>
		/// <param name="str">The string reperesented as the reference to an object.</param>
		/// <returns>Boxed boolean value or a null reference.</returns>
		public static object SafeConvertToBool(object str)
		{
			try
			{
				return Convert.ToBoolean(str);
			}
			catch(Exception)
			{
				return null;
			}
		}

		/// <summary>
		/// Tries to convert given parameter into Int32 type and treats it as a TimeSpan value specified in milliseconds.
		/// Returns a null reference if it is impossible.
		/// </summary>
		/// <param name="str">The string reperesented as the reference to an object.</param>
		/// <returns>Boxed TimeSpan value or a null reference.</returns>
		public static object SafeConvertToTimeSpan(object str)
		{
			if (str == null || (str as string) == string.Empty)
				return null;

			try
			{
				return TimeSpan.FromMilliseconds(Convert.ToInt32(str));
			}
			catch(Exception)
			{
			}

			return null;
		}

		/// <summary>
		/// Fetches the URI of the remote host that sent a request.
		/// </summary>
		/// <returns>The URI of the remote host or a null reference.</returns>
		public static string FetchCurrentRemoteUri()
		{
			return CurrentMessage.Sender.Uri;
		}

		/// <summary>
		/// Fetches the URI of the remote host that sent a request.
		/// </summary>
		/// <returns>The URI of the remote host or a null reference.</returns>
		public static string CurrentRemoteUri
		{
			get
			{
				return CurrentMessage.Sender.Uri;
			}
		}

		/// <summary>
		/// Fetches HostInformation of the remote host that sent a request.
		/// </summary>
		/// <returns>HostInformation or a null reference.</returns>
		public static HostInformation CurrentRemoteHost
		{
			get
			{
				return CurrentMessage.Sender;
			}
		}

		/// <summary>
		/// Gets a session belonging to the current user.
		/// Throws an exception if there is no current user.
		/// </summary>
		public static ISessionSupport CurrentSession
		{
			get
			{
				Message message = GenuineUtility.CurrentMessage;
				if (message == null)
					throw GenuineExceptions.Get_Processing_NoSessionAvailable();

				return message.Sender;
			}
		}

		/// <summary>
		/// Gets the object representing the message received from the remote host and initiated the current invocation.
		/// </summary>
		public static Message CurrentMessage
		{
			get
			{
				LocalDataStoreSlot slot = Thread.GetNamedDataSlot(OccupiedThreadSlots.CurrentMessage);
				return Thread.GetData(slot) as Message;
			}
		}

		/// <summary>
		/// Gets the socket while a connection is being accepted or CLSS is being established.
		/// </summary>
		public static Socket CurrentSocket
		{
			get
			{
				LocalDataStoreSlot slot = Thread.GetNamedDataSlot(OccupiedThreadSlots.SocketDuringEstablishing);
				return Thread.GetData(slot) as Socket;
			}
		}

		/// <summary>
		/// Gets the current HTTP Context.
		/// </summary>
		public static HttpContext CurrentHttpContext
		{
			get
			{
				return CurrentMessage.HttpServerRequestResult.HttpContext;
			}
		}

		/// <summary>
		/// Gets the current Security Session being used on invocation level.
		/// </summary>
		public static SecuritySessionParameters CurrentInvocationSecuritySessionParameters
		{
			get
			{
				return GenuineUtility.CurrentMessage.SecuritySessionParameters;
			}
		}

		/// <summary>
		/// Gets the current Security Session being used on connection level.
		/// </summary>
		public static SecuritySession CurrentConnectionSecuritySession
		{
			get
			{
				return GenuineUtility.CurrentMessage.ConnectionLevelSecuritySession;
			}
		}

		/// <summary>
		/// Gets the current Security Session being used on connection level.
		/// </summary>
		public static SecuritySession CurrentInvocationSecuritySession
		{
			get
			{
				try
				{
					return GenuineUtility.CurrentRemoteHost.GetSecuritySession(GenuineUtility.CurrentInvocationSecuritySessionParameters.Name, null);
				}
				catch
				{
					return null;
				}
			}
		}

		/// <summary>
		/// Gets the user credential provided during HTTP authorization (NTLM, digest or basic).
		/// </summary>
		public static IPrincipal HttpPrincipal
		{
			get
			{
				return GenuineUtility.CurrentMessage.HttpServerRequestResult.IPrincipal;
			}
		}

		/// <summary>
		/// Copies data from the stream to the buffer.
		/// If the stream does not contain specified number of bytes, an exception will be thrown.
		/// </summary>
		/// <param name="inputStream">The source stream to copy data from.</param>
		/// <param name="buffer">The buffer to copy data from.</param>
		/// <param name="offset">The zero-based byte offset in a buffer at which to begin storing the data read from the current stream.</param>
		/// <param name="sizeToRead">The size of the chunk being copied.</param>
		public static void ReadDataFromStream(Stream inputStream, byte[] buffer, int offset, int sizeToRead)
		{
			if (sizeToRead <= 0)
				return ;

			int readPortion = 0;
			int readSize = inputStream.Read(buffer, offset, sizeToRead);
			for ( ; readSize < sizeToRead; readSize += readPortion)
			{
				readPortion = inputStream.Read(buffer, offset + readSize, sizeToRead - readSize);
				if (readPortion <= 0)
					throw GenuineExceptions.Get_Receive_Portion();
			}
		}

		/// <summary>
		/// Copies data from the stream to the buffer.
		/// If the stream does not contain specified number of bytes, an exception will be thrown.
		/// </summary>
		/// <param name="inputStream">The source stream.</param>
		/// <param name="buffer">The buffer to copy to.</param>
		/// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream.</param>
		/// <param name="sizeToRead">Size of chunk being copied.</param>
		/// <returns>Bytes read.</returns>
		public static int TryToReadFromStream(Stream inputStream, byte[] buffer, int offset, int sizeToRead)
		{
			int readPortion = 0;
			int readSize = inputStream.Read(buffer, offset, sizeToRead);
			for ( ; readSize < sizeToRead; readSize += readPortion)
			{
				readPortion = inputStream.Read(buffer, offset + readSize, sizeToRead - readSize);
				if (readPortion <= 0)
					return readSize;
			}

			return readSize;
		}

		/// <summary>
		/// Copies data from one stream to another.
		/// </summary>
		/// <param name="inputStream">Stream to copy from.</param>
		/// <param name="outputStream">Stream to copy to.</param>
		public static void CopyStreamToStream(Stream inputStream, Stream outputStream)
		{
			GenuineChunkedStream inputGenuineChunkedStream = inputStream as GenuineChunkedStream;
			if (inputGenuineChunkedStream != null)
			{
				inputGenuineChunkedStream.WriteTo(outputStream);
				return ;
			}

			using(BufferKeeper bufferKeeper = new BufferKeeper(0))
			{
				int bufSize = bufferKeeper.Buffer.Length;
				int readSize = inputStream.Read(bufferKeeper.Buffer, 0, bufSize);
				while (readSize > 0)
				{
					outputStream.Write(bufferKeeper.Buffer, 0, readSize);
					readSize = inputStream.Read(bufferKeeper.Buffer, 0, bufSize);
				}
			}
		}

		/// <summary>
		/// Copies data from the input stream to the output stream.
		/// WARNING: sometimes it does not actually copy the content of the input stream, it attaches it to the output stream.
		/// </summary>
		/// <param name="inputStream">The stream to copy from.</param>
		/// <param name="outputStream">The stream to copy to.</param>
		/// <param name="buffer">The intermediate buffer used during copying.</param>
		public static void CopyStreamToStream(Stream inputStream, Stream outputStream, byte[] buffer)
		{
			GenuineChunkedStream inputGenuineChunkedStream = inputStream as GenuineChunkedStream;
			if (inputGenuineChunkedStream != null)
			{
				inputGenuineChunkedStream.WriteTo(outputStream);
				return ;
			}

			int bufSize = buffer.Length;
			int readSize = inputStream.Read(buffer, 0, bufSize);
			while (readSize > 0)
			{
				outputStream.Write(buffer, 0, readSize);
				readSize = inputStream.Read(buffer, 0, bufSize);
			}
		}

		/// <summary>
		/// Copies data from the input stream to the output stream.
		/// WARNING: sometimes it does not actually copy the content of the input stream, it attaches it to the output stream.
		/// </summary>
		/// <param name="inputStream">The stream to copy from.</param>
		/// <param name="outputStream">The stream to copy to.</param>
		/// <param name="buffer">The intermediate buffer used during copying.</param>
		public static void CopyStreamToStreamWithMinimumOperations(Stream inputStream, Stream outputStream, byte[] buffer)
		{
			GenuineChunkedStream inputGenuineChunkedStream = inputStream as GenuineChunkedStream;
			if (inputGenuineChunkedStream != null)
			{
				inputGenuineChunkedStream.WriteTo(outputStream);
				return ;
			}

			int bufSize = buffer.Length;
			int readSize = inputStream.Read(buffer, 0, bufSize);
			while (readSize > 0)
			{
				outputStream.Write(buffer, 0, readSize);
				readSize = inputStream.Read(buffer, 0, bufSize);
			}
		}

		/// <summary>
		/// Copies data from the input stream to output stream.
		/// </summary>
		/// <param name="inputStream">The stream to copy from.</param>
		/// <param name="outputStream">The stream to copy to.</param>
		/// <param name="size">The size to be copied.</param>
		/// <returns>Number of remained bytes.</returns>
		public static int CopyStreamToStream(Stream inputStream, Stream outputStream, int size)
		{
			if (size <= 0)
				return 0;

			using(BufferKeeper bufferKeeper = new BufferKeeper(0))
			{
				int bufSize = bufferKeeper.Buffer.Length;
				int readSize = inputStream.Read(bufferKeeper.Buffer, 0, Math.Min(bufSize, size));
				while (readSize > 0 && size > 0)
				{
					outputStream.Write(bufferKeeper.Buffer, 0, readSize);
					size -= readSize;

					if (size <= 0)
						return size;

					readSize = inputStream.Read(bufferKeeper.Buffer, 0, Math.Min(bufSize, size));
				}
			}

			return size;
		}

		/// <summary>
		/// Copies data from the input stream to the output stream.
		/// </summary>
		/// <param name="inputStream">The stream to copy from.</param>
		/// <param name="outputStream">The stream to copy to.</param>
		/// <param name="buffer">The intermediate buffer used during copying.</param>
		public static void CopyStreamToStreamPhysically(Stream inputStream, Stream outputStream, byte[] buffer)
		{
			int bufSize = buffer.Length;
			int readSize = inputStream.Read(buffer, 0, bufSize);
			while (readSize > 0)
			{
				outputStream.Write(buffer, 0, readSize);
				readSize = inputStream.Read(buffer, 0, bufSize);
			}
		}

		/// <summary>
		/// Copies the content of the input stream into the output stream without touching a part
		/// with the specified size in the end of the input stream.
		/// </summary>
		/// <param name="inputStream">The incoming data.</param>
		/// <param name="outputStream">The output data.</param>
		/// <param name="signSize">The size of the intact block.</param>
		/// <returns>The untouched block wrapped into a stream.</returns>
		public static byte[] CopyStreamToStreamExceptSign(Stream inputStream, Stream outputStream, int signSize)
		{
			using(BufferKeeper bufferKeeper = new BufferKeeper(0))
			{
				int bufSize = bufferKeeper.Buffer.Length;
				int readSize = inputStream.Read(bufferKeeper.Buffer, 0, bufSize);
				int validSize = 0;

				while (readSize > 0)
				{
					validSize += readSize;

					if (validSize > signSize)
					{
						// pour off the current content
						outputStream.Write(bufferKeeper.Buffer, 0, validSize - signSize);
						Buffer.BlockCopy(bufferKeeper.Buffer, validSize - signSize, bufferKeeper.Buffer, 0, signSize);
						validSize = signSize;
					}

					readSize = inputStream.Read(bufferKeeper.Buffer, validSize, bufSize - validSize);
				}

				byte[] sign = new byte[validSize];
				Buffer.BlockCopy(bufferKeeper.Buffer, 0, sign, 0, validSize);
				return sign;
			}
		}

		/// <summary>
		/// Returns .NET Remoting URI of the given MarshalByRefObject instance.
		/// </summary>
		/// <param name="marshalByRefObject">MarshalByRefObject instance to fetch URI for.</param>
		/// <returns>.NET Remoting URI of the given MarshalByRefObject instance.</returns>
		public static string FetchDotNetUriFromMbr(MarshalByRefObject marshalByRefObject)
		{
			ObjRef objRef = RemotingServices.Marshal(marshalByRefObject);
			return objRef.URI;
		}

		/// <summary>
		/// Answers Genuine Channels URI and Transport Context via which access to the remote
		/// MarshalByRefObject object is performed.
		/// </summary>
		/// <param name="marshalByRefObject">MarshalByRefObject instance to fetch the URI from.</param>
		/// <param name="uri">The uri.</param>
		/// <param name="iTransportContext">The transport context.</param>
		public static void FetchChannelUriFromMbr(MarshalByRefObject marshalByRefObject, out string uri, out ITransportContext iTransportContext)
		{
			IDictionary iDictionary = ChannelServices.GetChannelSinkProperties(marshalByRefObject);
			uri = iDictionary["GC_URI"] as string;
			iTransportContext = iDictionary["GC_TC"] as ITransportContext;
		}

		/// <summary>
		/// Gets a Transport Context servicing the specified transparent proxy.
		/// </summary>
		/// <param name="marshalByRefObject">The transparent proxy.</param>
		/// <returns>The Transport Context.</returns>
		public static ITransportContext FetchTransportContextFromMbr(MarshalByRefObject marshalByRefObject)
		{
			IDictionary iDictionary = ChannelServices.GetChannelSinkProperties(marshalByRefObject);
			return iDictionary["GC_TC"] as ITransportContext;
		}

		/// <summary>
		/// Gets a HostInformation representing the remote host where the specified marshalByRefObject resides.
		/// </summary>
		/// <param name="marshalByRefObject">The transparent proxy.</param>
		/// <returns>HostInformation representing the remote host where the specified marshalByRefObject resides.</returns>
		public static HostInformation FetchHostInformationFromMbr(MarshalByRefObject marshalByRefObject)
		{
			string uri;
			ITransportContext iTransportContext;
			GenuineUtility.FetchChannelUriFromMbr(marshalByRefObject, out uri, out iTransportContext);

			string ignored;
			return iTransportContext.KnownHosts.Get(GenuineUtility.Parse(uri, out ignored));
		}

		/// <summary>
		/// Fetches machine's guid from Genuine Channels URI.
		/// </summary>
		/// <param name="uri">The URI to fetch machine's guid from.</param>
		/// <returns>Machine's guid.</returns>
		public static string FetchMachineGuidFromUri(string uri)
		{
			Match match = _fetchMachineGuidFromUri.Match(uri);
			if (! match.Success)
				return null;

			return match.Groups["remoteGuid"].Value;
		}
		private static Regex _fetchMachineGuidFromUri = new Regex(@"^_g\w+://(?<remoteGuid>[^/]+)", RegexOptions.None);

		/// <summary>
		/// Resolves IP address. Throws the CanNotResolveHostName exception if resolving fails.
		/// </summary>
		/// <param name="url">Url to resolve.</param>
		/// <returns>Resolved IPAddress.</returns>
		public static IPAddress ResolveIPAddress(string url)
		{
			IPAddress ipAddress = null;

			try
			{
				if ( url.Length > 0 && url[0] >= '0' && url[0] <= '9' )
					ipAddress = IPAddress.Parse(url);
			}
			catch
			{
			}

			if (ipAddress == null)
			{
				IPAddress[] addresses = Dns.GetHostEntry(url).AddressList;
				if (addresses.Length <= 0)
					throw GenuineExceptions.Get_Connect_CanNotResolveHostName(url);
				ipAddress = addresses[0];
			}

			if (ipAddress == null)
				throw GenuineExceptions.Get_Connect_CanNotResolveHostName(url);
			return ipAddress;
		}

		/// <summary>
		/// Generates and answers a default server sink chain.
		/// </summary>
		/// <returns>The first sink provider in the chain.</returns>
		public static IServerChannelSinkProvider GetDefaultServerSinkChain()
		{
			BinaryServerFormatterSinkProvider srv = null;

			try
			{
				// server-side sinks
				IDictionary sinkProps = new Hashtable();
				sinkProps["typeFilterLevel"] = "Full";
				srv = new BinaryServerFormatterSinkProvider(sinkProps, null);
			}
			catch(Exception)
			{
				srv = new BinaryServerFormatterSinkProvider();
			}

			return srv;
		}

		/// <summary>
		/// Returns true if both buffers are equal.
		/// I can't believe that .NET framework doesn't have such means!
		/// </summary>
		/// <param name="first">First buffer to compare</param>
		/// <param name="second">Second buffer to compare</param>
		/// <returns>True if they equal</returns>
		public static bool AreBuffersEquals(byte[] first, byte[] second)
		{
			if (first.Length != second.Length)
				return false;

			int length = first.Length;
			for (int i=0; i<length; i++)
				if (first[i] != second[i])
					return false;

			return true;
		}

		/// <summary>
		/// Cuts out the content of the buffer.
		/// </summary>
		/// <param name="source">The source buffer.</param>
		/// <param name="offset">The offset to start from.</param>
		/// <param name="count">The size of the cut fragment.</param>
		/// <returns></returns>
		public static byte[] CutOutBuffer(byte[] source, int offset, int count)
		{
			byte[] result = new byte[count];
			Buffer.BlockCopy(source, offset, result, 0, count);
			return result;
		}

		/// <summary>
		/// Blocks the current thread until the event receives a signal.
		/// </summary>
		/// <param name="waitHandle">The event.</param>
		/// <param name="timeSpan">The time span specified in milliseconds.</param>
		/// <returns>True if the event receives a signal; otherwise, false.</returns>
		public static bool WaitOne(WaitHandle waitHandle, int timeSpan)
		{
			if (timeSpan < 0)
				timeSpan = 0;
			return waitHandle.WaitOne(timeSpan, false);
		}

		/// <summary>
		/// Deserializes an exception from the stream and throws it.
		/// </summary>
		/// <param name="stream">The stream containing serialized exception.</param>
		public static Exception ReadException(Stream stream)
		{
			BinaryFormatter binaryFormatter = new BinaryFormatter();
			return binaryFormatter.Deserialize(stream) as Exception;
		}

		/// <summary>
		/// Compares two Environment.TickCount values as they were CLS-complaint.
		/// </summary>
		/// <param name="finish">The end of the period.</param>
		/// <param name="start">The start of the period.</param>
		/// <returns>The difference in milliseconds.</returns>
		public static int CompareTickCounts(int finish, int start)
		{
			unchecked
			{
				int val = finish - start;
				if( ( val & 0x40000000 ) != 0 )
					val |= (int) 0x80000000;
				else
					val &= 0x7fffffff;
				return val;
			}
		}

		/// <summary>
		/// Checks whether the specified timeout is expired.
		/// </summary>
		/// <param name="timeout">The timeout.</param>
		/// <param name="now">The current moment.</param>
		/// <returns>True if the specified timeout is expired.</returns>
		public static bool IsTimeoutExpired(int timeout, int now)
		{
			return CompareTickCounts(timeout, now) <= 0;
		}

		/// <summary>
		/// Checks whether the specified timeout is expired.
		/// </summary>
		/// <param name="timeout">The timeout.</param>
		/// <returns>True if the specified timeout is expired.</returns>
		public static bool IsTimeoutExpired(int timeout)
		{
			return CompareTickCounts(timeout, GenuineUtility.TickCount) < 0;
		}

		/// <summary>
		/// Gets the number of milliseconds elapsed since the system started.
		/// </summary>
		public static int TickCount
		{
			get
			{
				return Environment.TickCount;
			}
		}

		/// <summary>
		/// Answers a moment at which the specified time span expires.
		/// </summary>
		/// <param name="timespan">The time span in milliseconds.</param>
		/// <returns>A moment at which the specified time span expires.</returns>
		public static int GetTimeout(int timespan)
		{
			return TickCount + timespan;
		}

		/// <summary>
		/// Answers a moment at which the specified time span expires.
		/// </summary>
		/// <param name="timespan">The time span in milliseconds.</param>
		/// <returns>A moment at which the specified time span expires.</returns>
		public static int GetTimeout(TimeSpan timespan)
		{
			return TickCount + GenuineUtility.ConvertToMilliseconds(timespan);
		}

		/// <summary>
		/// Answers a time span between the specified timeout and the current moment expressed in milliseconds.
		/// </summary>
		/// <param name="timeout">The timeout.</param>
		/// <returns>A time span between the specified timeout and the current moment expressed in milliseconds.</returns>
		public static int GetMillisecondsLeft(int timeout)
		{
			int millisecondsLeft = GenuineUtility.CompareTickCounts(timeout, GenuineUtility.TickCount);
			if (millisecondsLeft < 0)
				millisecondsLeft = 0;
			return millisecondsLeft;
		}

		/// <summary>
		/// Answers a moment specifying the maximum distant time moment from the current moment.
		/// </summary>
		/// <returns>A moment specifying the maximum distant time moment from the current moment.</returns>
		public static int FurthestFuture
		{
			get
			{
				unchecked
				{
					// 12.4276 days
					return int.MaxValue / 4 + Environment.TickCount - 1;
				}
			}
		}

		/// <summary>
		/// Gets the default identifier of the current host.
		/// </summary>
		public static Guid DefaultHostIdentifier
		{
			get
			{
				return _defaultHostIdentifier;
			}
		}
		private static Guid _defaultHostIdentifier = Guid.NewGuid();

		/// <summary>
		/// Gets the current thread identifier.
		/// </summary>
		/// <value>A 32-bit signed integer that is the identifier of the current thread.</value>
		public static int CurrentThreadId
		{
			get
			{
				return Thread.CurrentThread.ManagedThreadId;
			}
		}


#if DEBUG

		/// <summary>
		/// Gets or sets an indication of the current mode of execution. This property is thread-safe.
		/// </summary>
		public static bool IsDebuggingModeEnabled
		{
			get
			{
				lock (_isDebugModeEnabledLock)
				{
					return _isDebugModeEnabled;
				}
			}
			set
			{
				lock (_isDebugModeEnabledLock)
				{
					_isDebugModeEnabled = value;
				}
			}
		}
		private static bool _isDebugModeEnabled = false;
		private static object _isDebugModeEnabledLock = new object();

#endif

	}
}
