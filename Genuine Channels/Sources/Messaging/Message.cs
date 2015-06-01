/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Principal;
using System.Threading;
using System.Web;

using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.GenuineHttp;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Parameters;
using Belikov.GenuineChannels.Receiving;
using Belikov.GenuineChannels.Security;
using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.Messaging
{
	/// <summary>
	/// Represents a message being sent by Connection Manager.
	/// </summary>
	public class Message
	{
		/// <summary>
		/// Constructs a blank instance of the Message class.
		/// </summary>
		public Message()
		{
#if TRIAL
			if (this.MessageId > 3003)
				throw GenuineExceptions.Get_Channel_TrialConditionExceeded("The maximum number of messages restriction has been exceeded. You can not send more than 3000 messages using TRIAL version.");
#endif
		}

		/// <summary>
		/// Constructs an instance of the Message class.
		/// </summary>
		/// <param name="iTransportContext">The Transport Context.</param>
		/// <param name="recipient">Recipient.</param>
		/// <param name="replyToId">Source id or zero.</param>
		/// <param name="iTransportHeaders">Transport headers.</param>
		/// <param name="stream">Message.</param>
		public Message(ITransportContext iTransportContext, HostInformation recipient, int replyToId, ITransportHeaders iTransportHeaders, Stream stream)
		{
			this.ITransportContext = iTransportContext;
			this.ITransportHeaders = iTransportHeaders;
			this.Recipient = recipient;
			this.ReplyToId = replyToId;
			this.Stream = stream;

			if (this.ITransportHeaders != null)
				this.ITransportHeaders[TransportHeadersSenderEntryName] = this.Sender;

			this.MessageId = Interlocked.Increment(ref _currentMessageId);

#if TRIAL
			if (this.MessageId > 3001)
				throw GenuineExceptions.Get_Channel_TrialConditionExceeded("The maximum number of messages restriction has been exceeded. You can not send more than 3000 messages using TRIAL version.");
#endif
		}

		/// <summary>
		/// Constructs the response to the specified message.
		/// </summary>
		/// <param name="sourceMessage">The source message.</param>
		/// <param name="iTransportHeaders">The transport headers.</param>
		/// <param name="stream">The message content.</param>
		public Message(Message sourceMessage, ITransportHeaders iTransportHeaders, Stream stream)
		{
			this.ITransportContext = sourceMessage.ITransportContext;
			this.ITransportHeaders = iTransportHeaders;
//			this.Sender = sourceMessage.Recipient;
			this.Recipient = sourceMessage.Sender;
			this.ReplyToId = sourceMessage.MessageId;
			this.GenuineMessageType = sourceMessage.GenuineMessageType;
			this.Stream = stream;
			this.ConnectionName = sourceMessage.ConnectionName;
			this.SecuritySessionParameters = this.ITransportContext.FixSecuritySessionParameters(sourceMessage.SecuritySessionParameters);
			this.IsSynchronous = (this.SecuritySessionParameters.Attributes & SecuritySessionAttributes.ForceSync) != 0 ||
				((bool) this.ITransportContext.IParameterProvider[GenuineParameter.SyncResponses] && (this.SecuritySessionParameters.Attributes & SecuritySessionAttributes.ForceAsync) == 0);

			if (this.ITransportHeaders != null)
				this.ITransportHeaders[TransportHeadersSenderEntryName] = this.Sender;

			this.MessageId = Interlocked.Increment(ref _currentMessageId);

#if TRIAL
			if (this.MessageId > 3010)
				throw GenuineExceptions.Get_Channel_TrialConditionExceeded("The maximum number of messages restriction has been exceeded. You can not send more than 3000 messages using TRIAL version.");
#endif
		}

		/// <summary>
		/// The name of the entry in Transport Headers that contains sender URI (channel URI).
		/// </summary>
		public const string TransportHeadersSenderEntryName = "_gens";

		/// <summary>
		/// The name of the entry in Transport Headers that contains MBR URI (receiver URI).
		/// Uris of the same MBR object may be different, because 
		/// the client can marshal already marshalled object to another URI. So 
		/// the server has to specify the uri it wants to receive back in the 
		/// response.
		/// </summary>
		public const string TransportHeadersMbrUriName = "_genu";

		/// <summary>
		/// The name of the entry in Transport Headers that contains GUID (string) 
		/// which is unique for each broadcast call.
		/// </summary>
		public const string TransportHeadersBroadcastSendGuid = "_genbg";

		/// <summary>
		/// The name of the entry in Transport Headers that contains serialized ObjRef
		/// or court name.
		/// </summary>
		public const string TransportHeadersBroadcastObjRefOrCourt = "__genbdc";

		/// <summary>
		/// The name of the entry in Transport Headers that contains serialized ObjRef
		/// or court name.
		/// </summary>
		public const string TransportHeadersSerializedException = "__gensrlex";

		/// <summary>
		/// The name of the entry in Transport Headers that contains the type of the message.
		/// </summary>
		public const string TransportHeadersGenuineMessageType = "__genmt";

		/// <summary>
		/// The name of the entry in Transport Headers that contains the target of invocation.
		/// </summary>
		public const string TransportHeadersInvocationTarget = "__genit";

		/// <summary>
		/// The name of the entry in Transport Headers that contains the name of method being invoked.
		/// </summary>
		public const string TransportHeadersMethodName = "__genmn";

		/// <summary>
		/// To guarantee atomic access to local members.
		/// </summary>
		private object _accessToLocalMembers = new object();

		/// <summary>
		/// Gets or sets a value indicating whether the message stream contains a serialized 
		/// exception to be dispatched to the sender.
		/// </summary>
		public bool ContainsSerializedException
		{
			get
			{
				return this.ITransportHeaders[TransportHeadersSerializedException] != null;
			}
			set
			{
				if (value)
					this.ITransportHeaders[TransportHeadersSerializedException] = "";
				else
					this.ITransportHeaders[TransportHeadersSerializedException] = null;
			}
		}

		/// <summary>
		/// The name of the header containing destination object uri.
		/// </summary>
		public static string TransportHeaderName_RemoteObjectUri = "__RequestUri";

		#region -- Message -------------------------------------------------------------------------

		/// <summary>
		/// The type of the message.
		/// </summary>
		public GenuineMessageType GenuineMessageType = GenuineMessageType.Ordinary;

		/// <summary>
		/// Each message has unique positive 32-bit number.
		/// </summary>
		public int MessageId;
		private static int _currentMessageId = 1;

		/// <summary>
		/// True if this message is one-way.
		/// </summary>
		public bool IsOneWay = false;

		/// <summary>
		/// The message id this message replies to.
		/// </summary>
		public int ReplyToId;

		/// <summary>
		/// Message transport headers.
		/// </summary>
		public ITransportHeaders ITransportHeaders;

		/// <summary>
		/// The content of the message.
		/// </summary>
		public Stream Stream;

		/// <summary>
		/// The effective size of the message in bytes.
		/// </summary>
		public int EffectiveMessageSize = 0;

		/// <summary>
		/// Final content that will be sent by the transport.
		/// </summary>
		public Stream SerializedContent;

		/// <summary>
		/// Connection Level Security Session Parameters.
		/// </summary>
		public SecuritySession ConnectionLevelSecuritySession;

		/// <summary>
		/// The Security Session being used on the Invocation Level.
		/// </summary>
		public SecuritySessionParameters SecuritySessionParameters;

		/// <summary>
		/// The target object for broadcast sending.
		/// </summary>
		public object DestinationMarshalByRef;

		/// <summary>
		/// The object that contains information about this message.
		/// Is used by Direct Exchange Manager only.
		/// </summary>
		public object Tag;

		/// <summary>
		/// The connection associated with this message.
		/// Is used by Shared Memory and GXHTTP Connection Managers.
		/// </summary>
		public object ConnectionInfo;

		#endregion

		#region -- Invocation parameters -----------------------------------------------------------

		/// <summary>
		/// Source IMessage object.
		/// </summary>
		public IMessage IMessage;

		/// <summary>
		/// The HTTP request the message was received through.
		/// </summary>
		public HttpServerRequestResult HttpServerRequestResult;

		/// <summary>
		/// A boolean value indicating whether this request has been answered.
		/// </summary>
		public bool HasBeenAsnwered = false;

		#endregion

		#region -- Transport parameters ------------------------------------------------------------

		/// <summary>
		/// Transport Context.
		/// </summary>
		public ITransportContext ITransportContext;

		/// <summary>
		/// Information about message sender.
		/// </summary>
		public HostInformation Sender;

		/// <summary>
		/// Wellknown object or channel GUID which is supposed to receive the message.
		/// </summary>
		public HostInformation Recipient;

		/// <summary>
		/// Specifies the moment when the request times out.
		/// </summary>
		public int FinishTime
		{
			get
			{
				lock (this._accessToLocalMembers)
					return this._finishTime;
			}
			set
			{
				lock (this._accessToLocalMembers)
				{
					this._finishTime = value;
					this.FinishTime_Initialized = true;
				}
			}
		}
		private int _finishTime = GenuineUtility.FurthestFuture;

		/// <summary>
		/// Indicates whether the FinishTime instance field was initialized.
		/// </summary>
		public bool FinishTime_Initialized
		{
			get
			{
				lock (this._accessToLocalMembers)
					return this._finishTime_Initialized;
			}
			set
			{
				lock (this._accessToLocalMembers)
					this._finishTime_Initialized = value;
			}
		}
		private bool _finishTime_Initialized = false;

		/// <summary>
		/// The connection name. Is used to direct the message to a particular connection.
		/// </summary>
		public string ConnectionName;

		/// <summary>
		/// True if the sending is synchronous.
		/// </summary>
		public bool IsSynchronous;

		/// <summary>
		/// Whether to re-send the message again if a connection was broken.
		/// </summary>
		public bool IsResendAfterFail = false;

		#endregion

		#region -- Sync wait slot ------------------------------------------------------------------

		/// <summary>
		/// Is set when a connection is available.
		/// </summary>
		internal ManualResetEvent ConnectionAvailable;

		/// <summary>
		/// Indicates whether the seding should be cancelled and the specified exception
		/// should be dispatched instead.
		/// </summary>
		internal ManualResetEvent CancelSending;

		/// <summary>
		/// Contains an exception if connection is not available.
		/// </summary>
		internal Exception SyncWaitException;

		/// <summary>
		/// Indicates whether the thread still awaits for the connection.
		/// </summary>
		internal bool IsValid = true;

		#endregion

		#region -- Debugging -----------------------------------------------------------------------

		/// <summary>
		/// The sequence unique number of the current instance. Is used in debugging and diagnostic purposes only.
		/// </summary>
		public int SeqNo
		{
			get
			{
				return this._seqNo;
			}
		}
		private int _seqNo = Interlocked.Increment(ref _globalSeqNo);
		private static int _globalSeqNo = 0;

		#endregion


		/// <summary>
		/// Makes message up from the IMessage.
		/// </summary>
		/// <param name="iTransportContext">The Transport Context.</param>
		/// <param name="iMessage">IMessage to make the message up from.</param>
		/// <param name="iTransportHeaders">Trasport headers.</param>
		/// <param name="stream">Message body.</param>
		/// <param name="isSynchronous">True if the invocation is synchronous.</param>
		/// <returns>An instance of the Message class.</returns>
		public static Message CreateOutcomingMessage(ITransportContext iTransportContext, IMessage iMessage, ITransportHeaders iTransportHeaders, Stream stream, bool isSynchronous)
		{
			// when microsoft guys start caring about detailed .NET remoting documentation???
			string url = (string) iMessage.Properties["__Uri"];
			string objectURI;

			string channelUri = GenuineUtility.Parse(url, out objectURI);
			if (objectURI == null)
				objectURI = url;	// not well-known object
			iTransportHeaders[TransportHeaderName_RemoteObjectUri] = objectURI;
			Message message = new Message(iTransportContext, null, 0, iTransportHeaders, stream);
			message.IMessage = iMessage;
			message.IsSynchronous = isSynchronous;

			if (iTransportHeaders[Message.TransportHeadersGenuineMessageType] != null)
				message.GenuineMessageType = (GenuineMessageType) iTransportHeaders[Message.TransportHeadersGenuineMessageType];

			return message;
		}

		/// <summary>
		/// Releases all memory resources.
		/// </summary>
		public void Dispose()
		{
			if (this.SerializedContent != null)
				this.SerializedContent.Close();
			if (this.Stream != null)
				this.Stream.Close();
		}

		/// <summary>
		/// Returns a string that represents the current Object.
		/// </summary>
		/// <returns>A string that represents the current Object.</returns>
		public override string ToString()
		{
			return String.Format("MESSAGE (Id: {0} Reply: {1} Sender: {2} Recipient: {3} Security Session Name: {4} Security Session Parameters: {5} One-way: {6} Synchronous: {7} Timeout: {8})",
				this.MessageId, this.ReplyToId, (this.Sender == null ? "<none>" : this.Sender.ToString()), 
				(this.Recipient == null ? "<none>" : this.Recipient.ToString()), 
				(this.SecuritySessionParameters == null ? "<none>" : this.SecuritySessionParameters.Name), 
				(this.SecuritySessionParameters == null ? "<none>" : Enum.Format(typeof(SecuritySessionAttributes), this.SecuritySessionParameters.Attributes, "G")), 
				this.IsOneWay, this.IsSynchronous.ToString(), this.FinishTime);
		}
	}
}
