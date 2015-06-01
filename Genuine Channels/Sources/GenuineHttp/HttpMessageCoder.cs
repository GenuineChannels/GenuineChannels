/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.IO;

using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.Messaging;

namespace Belikov.GenuineChannels.GenuineHttp
{
	/// <summary>
	/// Provides a set of operations for writing and reading HTTP headers.
	/// </summary>
	internal class HttpMessageCoder
	{
		/// <summary>
		/// To prevent creating instances of the HttpMessageCoder class.
		/// </summary>
		private HttpMessageCoder()
		{
		}

		/// <summary>
		/// Writes HTTP header to the specified binary writer.
		/// </summary>
		/// <param name="binaryWriter">The binary writer.</param>
		/// <param name="protocolVersion">The version of the protocol.</param>
		/// <param name="genuineConnectionType">The type of connection.</param>
		/// <param name="hostId">The id of the remote host.</param>
		/// <param name="httpPacketType">The type of the packet.</param>
		/// <param name="sequenceNo">The packet number.</param>
		/// <param name="connectionName">The name of the connection.</param>
		/// <param name="localHostUniqueIdentifier">The unique identifier of the local HostInformation.</param>
		public static void WriteRequestHeader(BinaryWriter binaryWriter, byte protocolVersion, GenuineConnectionType genuineConnectionType, byte[] hostId, HttpPacketType httpPacketType, int sequenceNo, string connectionName, int localHostUniqueIdentifier)
		{
			binaryWriter.Write((byte) MessageCoder.COMMAND_MAGIC_CODE);
			binaryWriter.Write((byte) protocolVersion);
			binaryWriter.Write((byte) genuineConnectionType);
			binaryWriter.Write(hostId);
			binaryWriter.Write((byte) httpPacketType);
			binaryWriter.Write((int) sequenceNo);
			binaryWriter.Write(connectionName);
			binaryWriter.Write((int) localHostUniqueIdentifier);
		}

		/// <summary>
		/// Parses the HTTP header.
		/// </summary>
		/// <param name="binaryReader">The source binary reader.</param>
		/// <param name="protocolVersion">The version of the protocol.</param>
		/// <param name="genuineConnectionType">The type of connection.</param>
		/// <param name="hostId">The identifier of the remote host.</param>
		/// <param name="httpPacketType">The type of the packet.</param>
		/// <param name="sequenceNo">The packet number.</param>
		/// <param name="connectionName">The name of the connection.</param>
		/// <param name="remoteHostUniqueIdentifier">The unique identifier of the HostInformation used by the remote host.</param>
		public static void ReadRequestHeader(BinaryReader binaryReader, out byte protocolVersion, out GenuineConnectionType genuineConnectionType, out Guid hostId, out HttpPacketType httpPacketType, out int sequenceNo, out string connectionName, out int remoteHostUniqueIdentifier)
		{
			if (binaryReader.ReadByte() != MessageCoder.COMMAND_MAGIC_CODE)
				throw GenuineExceptions.Get_Receive_IncorrectData();

			protocolVersion = binaryReader.ReadByte();
			genuineConnectionType = (GenuineConnectionType) binaryReader.ReadByte();
			hostId = new Guid(binaryReader.ReadBytes(16));
			httpPacketType = (HttpPacketType) binaryReader.ReadByte();
			sequenceNo = binaryReader.ReadInt32();

			connectionName = "$/__GC/" + hostId;
			remoteHostUniqueIdentifier = 0;

			if (protocolVersion >= 0x1)
			{
				connectionName = binaryReader.ReadString();
				remoteHostUniqueIdentifier = binaryReader.ReadInt32();
			}
		}

		/// <summary>
		/// Writes the HTTP response header.
		/// </summary>
		/// <param name="binaryWriter">The destination.</param>
		/// <param name="protocolVersion">The version of the protocol.</param>
		/// <param name="serverUri">The server uri.</param>
		/// <param name="sequenceNo">The packet number.</param>
		/// <param name="httpPacketType">The type of the packet.</param>
		/// <param name="localHostUniqueIdentifier">The unique identifier of the local HostInformation.</param>
		public static void WriteResponseHeader(BinaryWriter binaryWriter, byte protocolVersion, string serverUri, int sequenceNo, HttpPacketType httpPacketType, int localHostUniqueIdentifier)
		{
			binaryWriter.Write((byte) MessageCoder.COMMAND_MAGIC_CODE);
			binaryWriter.Write(serverUri);
			binaryWriter.Write((int) sequenceNo);
			binaryWriter.Write((byte) httpPacketType);

			if (protocolVersion > 0)
				binaryWriter.Write((int) localHostUniqueIdentifier);
		}

		/// <summary>
		/// Reads the response from the stream.
		/// </summary>
		/// <param name="binaryReader">The source.</param>
		/// <param name="serverUri">The fetched server uri.</param>
		/// <param name="sequenceNo">The packet number.</param>
		/// <param name="httpPacketType">The type of the packet.</param>
		/// <param name="remoteHostUniqueIdentifier">The unique identifier of the HostInformation used by the remote host.</param>
		public static void ReadResponseHeader(BinaryReader binaryReader, out string serverUri, out int sequenceNo, out HttpPacketType httpPacketType, out int remoteHostUniqueIdentifier)
		{
			if (binaryReader.ReadByte() != MessageCoder.COMMAND_MAGIC_CODE)
				throw GenuineExceptions.Get_Receive_IncorrectData();
			serverUri = binaryReader.ReadString();
			sequenceNo = binaryReader.ReadInt32();
			httpPacketType = (HttpPacketType) binaryReader.ReadByte();
			remoteHostUniqueIdentifier = binaryReader.ReadInt32();
		}
	}
}
