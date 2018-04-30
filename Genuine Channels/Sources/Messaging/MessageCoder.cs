/* Genuine Channels product.
 *
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 *
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Runtime.Remoting.Channels;
using System.Runtime.Serialization.Formatters.Binary;

using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.Security;
using Zyan.SafeDeserializationHelpers;

namespace Belikov.GenuineChannels.Messaging
{
	/// <summary>
	/// Utility class that contains several packing algorithms.
	/// </summary>
	internal class MessageCoder
	{
		/// <summary>
		/// Prevents from creating instances of this class.
		/// </summary>
		private MessageCoder()
		{
		}

		/// <summary>
		/// Magic code to check remote channel.
		/// </summary>
		public const byte COMMAND_MAGIC_CODE = 0x37;

		/// <summary>
		/// Serializes a message to the GenuineChunkedStream stream.
		/// </summary>
		/// <param name="stream">Stream to serialize the message to.</param>
		/// <param name="message">The message being serialized.</param>
		/// <param name="compress">Indicates whether content should be compressed.</param>
		public static void Serialize(GenuineChunkedStream stream, Message message, bool compress)
		{
			// write a mark whether the content will be compressed
			BinaryWriter binaryWriter = new BinaryWriter(stream);
			binaryWriter.Write((bool) compress);

			// gather messages into a separate stream if compression is required
			GenuineChunkedStream usedStream = stream;
			if (compress)
				usedStream = new GenuineChunkedStream(false);
			binaryWriter = new BinaryWriter(usedStream);

			// all simple values
			binaryWriter.Write((byte) message.GenuineMessageType);
			binaryWriter.Write(message.MessageId);
			binaryWriter.Write(message.ReplyToId);
			//			binaryWriter.Write(message.Recipient.Uri);
			binaryWriter.Write((bool) message.IsOneWay);

			// set DestinationMarshalByRef if it is specified in headers
			object broadcastObjRefOrCourt = message.ITransportHeaders[Message.TransportHeadersBroadcastObjRefOrCourt];
			if (broadcastObjRefOrCourt != null)
				message.DestinationMarshalByRef = broadcastObjRefOrCourt;

			// objref if it exists
			if (message.DestinationMarshalByRef != null)
			{
				var binaryFormatter = new BinaryFormatter().Safe();
				binaryWriter.Write( true );
				binaryFormatter.Serialize(binaryWriter.BaseStream, message.DestinationMarshalByRef);
			}
			else
				binaryWriter.Write( false );

			// Security Session parameters
			binaryWriter.Write((Int16) message.SecuritySessionParameters.Attributes);
			binaryWriter.Write(message.SecuritySessionParameters.RemoteTransportUser);

			// now headers
			foreach (DictionaryEntry entry in message.ITransportHeaders)
			{
				string key = entry.Key as string;
				if (key == null || key == "__" || key == Message.TransportHeadersBroadcastObjRefOrCourt
					|| entry.Value == null || key == Message.TransportHeadersGenuineMessageType)
					continue;

				string val = entry.Value.ToString();

				// now write these strings
				binaryWriter.Write(key);
				binaryWriter.Write(val);
			}

			// headers end tag
			binaryWriter.Write("__");

			// and finally the content
			usedStream.WriteStream(message.Stream);

			// compress the content
			if (compress)
			{
				var compressingStream = new GZipStream(new NonClosableStream(stream), CompressionMode.Compress, leaveOpen: true);
				GenuineUtility.CopyStreamToStream(usedStream, compressingStream);
				compressingStream.Close();
			}
		}

		/// <summary>
		/// Deserializes the message from the stream.
		/// Automatically decompress a message if it was compressed.
		/// </summary>
		/// <param name="stream">The source stream.</param>
		/// <param name="securitySessionName">The name of the Security Session.</param>
		/// <returns>Deserialized message.</returns>
		public static Message Deserialize(Stream stream, string securitySessionName)
		{
			BinaryReader binaryReader = new BinaryReader(stream);
			bool contentWasCompressed = binaryReader.ReadBoolean();

			// decompress the stream
			if (contentWasCompressed)
				stream = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: false);

			binaryReader = new BinaryReader(stream);
			Message message = new Message();

			// all simple values
			message.GenuineMessageType = (GenuineMessageType) binaryReader.ReadByte();
			message.MessageId = binaryReader.ReadInt32();
			message.ReplyToId = binaryReader.ReadInt32();
			message.IsOneWay = binaryReader.ReadBoolean();

			// deserialize MBR
			bool mbrWritten = binaryReader.ReadBoolean();
			if (mbrWritten)
			{
				var binaryFormatter = new BinaryFormatter().Safe();
				message.DestinationMarshalByRef = binaryFormatter.Deserialize(stream);
			}

			// Security Session parameters
			Int16 securitySessionAttributesAsInteger = binaryReader.ReadInt16();
			string securitySessionRemoteTransportUser = binaryReader.ReadString();
			message.SecuritySessionParameters = new SecuritySessionParameters(securitySessionName,
				(SecuritySessionAttributes) securitySessionAttributesAsInteger, TimeSpan.MinValue, securitySessionRemoteTransportUser);

			// read headers
			message.ITransportHeaders = new TransportHeaders();
			for ( ; ; )
			{
				string key = binaryReader.ReadString();
				if (key == "__")
					break;

				// read and set value
				string val = binaryReader.ReadString();
				message.ITransportHeaders[key] = val;
			}

			// and the content
			message.Stream = stream;
			return message;
		}

		/// <summary>
		/// This operation helps fetching Int32 value without constructing BinaryReader object.
		/// </summary>
		/// <param name="content">Buffer to fetch Int32 from.</param>
		/// <param name="position">Position.</param>
		/// <returns>Fetched value.</returns>
		public static int ReadInt32(byte[] content, int position)
		{
			return ((int) Buffer.GetByte(content, position)) | ( ((int) Buffer.GetByte(content, position + 1)) << 8 ) |
				( ((int) Buffer.GetByte(content, position + 2)) << 16 ) | ( ((int) Buffer.GetByte(content, position + 3)) << 24 );
		}

		/// <summary>
		/// Writes Int32 value to the buffer.
		/// </summary>
		/// <param name="content">Buffer to write Int32 to.</param>
		/// <param name="position">Position.</param>
		/// <param name="val">Value to save.</param>
		public static void WriteInt32(byte[] content, int position, int val)
		{
			Buffer.SetByte(content, position, (byte) val);
			Buffer.SetByte(content, position + 1, (byte) (val >> 8));
			Buffer.SetByte(content, position + 2, (byte) (val >> 16));
			Buffer.SetByte(content, position + 3, (byte) (val >> 24));
		}

		/// <summary>
		/// Protocol version for the release 2.5.0.
		/// </summary>
		public const byte PROTOCOL_VERSION = 0x3;

		/// <summary>
		/// Returns a stream containing version of the using protocol and type of the connection being established.
		/// </summary>
		/// <param name="protocolVersion">The version of the protocol.</param>
		/// <param name="genuineConnectionType">Type of the connection.</param>
		/// <param name="connectionId">The identifier of the connection.</param>
		/// <returns>BinaryWriter containing a stream with serialized data.</returns>
		public static BinaryWriter SerializeConnectionHeader(byte protocolVersion, GenuineConnectionType genuineConnectionType, string connectionId)
		{
			GenuineChunkedStream stream = new GenuineChunkedStream(false);
			BinaryWriter binaryWriter = new BinaryWriter(stream);
			binaryWriter.Write((byte) protocolVersion);
			binaryWriter.Write((byte) genuineConnectionType);
			binaryWriter.Write((string) connectionId);

			return binaryWriter;
		}

		/// <summary>
		/// Analyzes the version of the requested protocol and reads type of the connection.
		/// Throws OperationException on any errors.
		/// </summary>
		/// <param name="binaryReader">BinaryReader created on the stream with data.</param>
		/// <param name="protocolVersion">The version of the protocol.</param>
		/// <param name="genuineConnectionType">Read type of the connection.</param>
		/// <param name="connectionId">The connection identifier.</param>
		public static void DeserializeConnectionHeader(BinaryReader binaryReader, out byte protocolVersion, out GenuineConnectionType genuineConnectionType, out string connectionId)
		{
			connectionId = "/__GC/Default";

			protocolVersion = binaryReader.ReadByte();

			genuineConnectionType = (GenuineConnectionType) binaryReader.ReadByte();
			if (! Enum.IsDefined(typeof(GenuineConnectionType), genuineConnectionType) || genuineConnectionType == GenuineConnectionType.None || genuineConnectionType == GenuineConnectionType.All)
				throw GenuineExceptions.Get_Connect_CanNotAcceptIncomingConnection("Incorrect type of the connection.");

			if (protocolVersion > 0)
			{
				connectionId = binaryReader.ReadString();
				if (connectionId == null || connectionId.Length <= 0)
					throw GenuineExceptions.Get_Connect_CanNotAcceptIncomingConnection("Incorrect name of the connection.");
			}
		}

		/// <summary>
		/// The size of the label in the labelled stream.
		/// </summary>
		public const int LABEL_HEADER_SIZE = 6;

		/// <summary>
		/// Copies the input stream to the output stream with intermediate size labels.
		/// </summary>
		/// <param name="inputStream">The source stream.</param>
		/// <param name="outputStream">The destination stream.</param>
		/// <param name="writer">The writer created on destination stream.</param>
		/// <param name="intermediateBuffer">The intermediate buffer.</param>
		public static void WriteLabelledStream(Stream inputStream, GenuineChunkedStream outputStream, BinaryWriter writer, byte[] intermediateBuffer)
		{
			if (inputStream.CanSeek)
			{
				writer.Write((byte) MessageCoder.COMMAND_MAGIC_CODE);
				writer.Write((int) inputStream.Length);
				writer.Write((byte) 1);
				outputStream.WriteStream(inputStream);
				return ;
			}

			for ( ; ; )
			{
				int bytesRead = inputStream.Read(intermediateBuffer, 0, intermediateBuffer.Length);
				writer.Write((byte) MessageCoder.COMMAND_MAGIC_CODE);
				writer.Write((int) bytesRead);
				writer.Write((byte) (bytesRead < intermediateBuffer.Length? 1 : 0));
				writer.BaseStream.Write(intermediateBuffer, 0, bytesRead);

				if (bytesRead < intermediateBuffer.Length)
					return ;
			}
		}

		/// <summary>
		/// Fills in the provided stream with the specified messages that the total size as close to the recommended size as possible.
		/// </summary>
		/// <param name="message">The message.</param>
		/// <param name="messageContainer">Accumulated messages or a null reference.</param>
		/// <param name="packedMessages">This instance will contain all packed messages after this function completes.</param>
		/// <param name="resultContent">The filled stream.</param>
		/// <param name="intermediateBuffer">The intermediate buffer.</param>
		/// <param name="recommendedSize">The recommended size.</param>
		public static void FillInLabelledStream(Message message, MessageContainer messageContainer, ArrayList packedMessages, GenuineChunkedStream resultContent, byte[] intermediateBuffer, int recommendedSize)
		{
			BinaryWriter binaryWriter = new BinaryWriter(resultContent);

			if (packedMessages != null)
				packedMessages.Clear();

			while ( message != null )
			{
				// not finished yet
				binaryWriter.Write((byte) 0);

				if (packedMessages != null)
					packedMessages.Add(message);

				MessageCoder.WriteLabelledStream(message.SerializedContent, resultContent, binaryWriter, intermediateBuffer);
				message = null;

				if (resultContent.Length > recommendedSize)
					break;

				if (messageContainer != null)
					message = messageContainer.GetMessage();

				if (message == null)
					break;
			}

			// "finish" flag
			binaryWriter.Write((byte) 1);
		}

	}
}
