/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.IO;
using System.Reflection;
using System.Threading;

using Belikov.GenuineChannels.BroadcastEngine;
using Belikov.GenuineChannels.BufferPooling;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Parameters;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Security;

namespace Belikov.GenuineChannels.Logbook
{
	/// <summary>
	/// Writes down debug records into the provided stream writer.
	/// </summary>
	public class BinaryLogWriter
	{
		/// <summary>
		/// Constructs an instance of the BinaryLogWriter class.
		/// </summary>
		/// <param name="stream">The destination stream.</param>
		public BinaryLogWriter(Stream stream)
		{
			this._stream = stream;
			this._binaryWriter = new BinaryWriter(this._stream);

			this.levels = new int[(int) LogCategory.TotalCategories];
			for ( int i = 0; i < this.levels.Length; i++ )
				this.levels[i] = 2;

			this.WriteVersionInformationEvent();
		}

		/// <summary>
		/// The target stream.
		/// </summary>
		public Stream Stream
		{
			get
			{
				return this._stream;
			}
		}
		private Stream _stream;

		/// <summary>
		/// Gets or sets a level of details of event representation.
		/// </summary>
		public int this[LogCategory logCategory]
		{
			get
			{
				return this.levels[(int) logCategory];
			}
			set
			{
				this.levels[(int) logCategory] = value;
			}
		}
		private int[] levels;

		/// <summary>
		/// The binary writer created on the stream.
		/// </summary>
		public BinaryWriter BinaryWriter
		{
			get
			{
				return this._binaryWriter;
			}
		}
		private BinaryWriter _binaryWriter;


		/// <summary>
		/// The lock to the stream.
		/// </summary>
		private object _streamLock
		{
			get
			{
				return this._stream;
			}
		}

		/// <summary>
		/// The header magic code: 0xAA, 0x4B, 0xE7, 0x78
		/// </summary>
		public const int StreamHeaderMagicCode = 0x78E74BAA;

		#region -- Helper operations ---------------------------------------------------------------

		/// <summary>
		/// Puts down a string or an empty string if the specified value is null.
		/// </summary>
		/// <param name="str">The string or a null reference.</param>
		private void WriteString(string str)
		{
			if (str == null)
				this.BinaryWriter.Write(string.Empty);
			else
				this.BinaryWriter.Write(str);
		}

		/// <summary>
		/// Puts down a string with parameters or an empty string if the specified value is null.
		/// </summary>
		/// <param name="str">The string or a null reference.</param>
		/// <param name="parameters">String parameters.</param>
		private void WriteStringWithParameters(string str, params object[] parameters)
		{
			this.WriteString(string.Format(str, parameters));
		}

		/// <summary>
		/// Puts down a string representation of the provided object or an empty string if the specified value is null.
		/// </summary>
		/// <param name="obj">The object or a null reference.</param>
		private void WriteObjectAsString(object obj)
		{
			if (obj == null)
				this.BinaryWriter.Write(string.Empty);
			else
				this.BinaryWriter.Write(obj.ToString());
		}

		/// <summary>
		/// Puts down a record represending the specified exception and all inner exceptions.
		/// </summary>
		/// <param name="exception">The exception.</param>
		private void WriteException(Exception exception)
		{
			if (exception == null)
				this.BinaryWriter.Write( (bool) false );
			else
			{
				this.BinaryWriter.Write( (bool) true );

				this.WriteString( exception.GetType().FullName );
				this.WriteString( exception.Message );
				this.WriteString( exception.StackTrace );

				this.WriteException(exception.InnerException);
			}
		}

		/// <summary>
		/// Puts down a structure describing the specified remote host.
		/// </summary>
		/// <param name="remote">The HostInformation.</param>
		private void WriteHostInformation(HostInformation remote)
		{
			if (remote == null)
				this.BinaryWriter.Write( (bool) false );
			else
			{
				this.BinaryWriter.Write( (bool) true );

				this.BinaryWriter.Write((int) remote.LocalHostUniqueIdentifier);
				this.BinaryWriter.Write((int) remote.RemoteHostUniqueIdentifier);
				this.WriteString(remote.Url);
				this.WriteString(remote.Uri);
				this.WriteString(remote.PrimaryUri);
				this.BinaryWriter.Write((int) GenuineUtility.GetMillisecondsLeft(remote.ExpireTime));
				this.WriteObjectAsString(remote.LocalPhysicalAddress);
				this.WriteObjectAsString(remote.PhysicalAddress);
			}
		}

		/// <summary>
		/// Puts down the ID of the remote host.
		/// </summary>
		/// <param name="remote">The remote host.</param>
		private void WriteHostInformationId(HostInformation remote)
		{
			this.BinaryWriter.Write( (bool) (remote != null) );
			if (remote != null)
				this.BinaryWriter.Write( (int) remote.LocalHostUniqueIdentifier );
		}

		/// <summary>
		/// Puts down a structure describing the specified message.
		/// </summary>
		/// <param name="message">The message.</param>
		/// <param name="isSent">Whether it will be executed on a remote host.</param>
		/// <param name="invocationTarget">The target of the invocation.</param>
		/// <param name="methodName">The name of the method.</param>
		private void WriteMessage(Message message, bool isSent, string invocationTarget, string methodName)
		{
			this.BinaryWriter.Write((int) message.SeqNo);
			this.BinaryWriter.Write((int) message.MessageId);
			this.BinaryWriter.Write((int) message.ReplyToId);
			this.BinaryWriter.Write( (bool) isSent);
			this.BinaryWriter.Write( (bool) message.IsOneWay);
			this.BinaryWriter.Write( (bool) message.IsSynchronous);
			this.BinaryWriter.Write( (int) message.GenuineMessageType);
			this.BinaryWriter.Write( (int) GenuineUtility.GetMillisecondsLeft(message.FinishTime));
			this.WriteString(invocationTarget);
			this.WriteString(methodName);

			// the size of the message
			if (message.Stream != null && message.Stream.CanSeek)
			{
				try
				{
					this.BinaryWriter.Write( (int) message.Stream.Length);
				}
				catch
				{
					this.BinaryWriter.Write( (int) 0);
				}
			}
			else
				this.BinaryWriter.Write( (int) 0);
		}

		/// <summary>
		/// Puts down the message id.
		/// </summary>
		/// <param name="message">The message.</param>
		private void WriteMessageSeqNo(Message message)
		{
			this.BinaryWriter.Write( (bool) (message != null) );
			if (message != null)
				this.BinaryWriter.Write( (int) message.SeqNo );
		}

		/// <summary>
		/// Puts down Security Session Parameters.
		/// </summary>
		/// <param name="parameters">The Security Session Parameters.</param>
		private void WriteSecuritySessionParameters(SecuritySessionParameters parameters)
		{
			this.BinaryWriter.Write( (bool) (parameters != null));
			if (parameters != null)
			{
				this.WriteString( parameters.Name);
				this.BinaryWriter.Write( (int) parameters.GenuineConnectionType);
				this.WriteString( parameters.ConnectionName);
				this.BinaryWriter.Write( (long) parameters.Timeout.Ticks);
				this.BinaryWriter.Write( (int) parameters.Attributes );
				this.BinaryWriter.Write( (long) parameters.CloseAfterInactivity.Ticks );
			}
		}

		/// <summary>
		/// Puts down the Security Session Identifier.
		/// </summary>
		/// <param name="securitySession">The Security Session.</param>
		private void WriteSecuritySessionId(SecuritySession securitySession)
		{
			this.BinaryWriter.Write( (bool) (securitySession != null) );
			if (securitySession != null)
				this.BinaryWriter.Write( (int) securitySession.SecuritySessionId );
		}

		/// <summary>
		/// Puts down a content of the stream.
		/// </summary>
		/// <param name="stream">The stream containing content.</param>
		private void WriteBinaryContent(Stream stream)
		{
			if (stream == null)
				this.BinaryWriter.Write( (bool) false );
			else
			{
				this.BinaryWriter.Write( (bool) true );

				using (BufferKeeper bufferKeeper = new BufferKeeper(0))
				{
					for ( ; ; )
					{
						int bytesRead = stream.Read(bufferKeeper.Buffer, 0, bufferKeeper.Buffer.Length);

						// if the end of the stream has been reached
						if (bytesRead <= 0)
						{
							this.BinaryWriter.Write( (int) 0);
							stream.Position = 0;
							return ;
						}

						this.BinaryWriter.Write( (int) bytesRead);
						this.BinaryWriter.Write(bufferKeeper.Buffer, 0, bytesRead);
					}
				}

			}
		}


		/// <summary>
		/// The lifetime id of the current execution.
		/// </summary>
		public static long LifetimeId
		{
			get
			{
				return _lifetimeId;
			}
		}
		private static long _lifetimeId = DateTime.Now.Ticks;

		/// <summary>
		/// Puts down a record header containing the magic code, type of the record, and current DateTime.
		/// </summary>
		/// <param name="binaryRecordVersion">The version of the record.</param>
		/// <param name="logCategory">The log category.</param>
		/// <param name="type">The type of the event.</param>
		/// <param name="author">The author.</param>
		public void WriteRecordHeader(BinaryRecordVersion binaryRecordVersion, LogCategory logCategory, LogMessageType type, string author)
		{
			this.BinaryWriter.Write(StreamHeaderMagicCode);
			this.BinaryWriter.Write((long) BinaryLogWriter.LifetimeId);
			this.BinaryWriter.Write((int) binaryRecordVersion);
			this.BinaryWriter.Write((int) logCategory);
			this.BinaryWriter.Write((long) DateTime.Now.Ticks);
			this.BinaryWriter.Write((int) type);
			this.WriteString( author);
		}

		/// <summary>
		/// Puts down all parameters from the specified parameter provider.
		/// </summary>
		/// <param name="iParameterProvider">The parameter provider.</param>
		public void WriteConnectionParameters(IParameterProvider iParameterProvider)
		{
			Type enumType = typeof(GenuineParameter);

			foreach (FieldInfo fieldInfo in enumType.GetFields())
				if (fieldInfo.FieldType == enumType)
				{
					GenuineParameter theCurrentParameter = (GenuineParameter) fieldInfo.GetValue(null);

					this.BinaryWriter.Write(true);
					this.WriteString( Enum.Format(enumType, theCurrentParameter, "g") );
					this.WriteString( iParameterProvider[theCurrentParameter] == null ? "<<null>>" : iParameterProvider[theCurrentParameter].ToString() );
				}

			this.BinaryWriter.Write(false);
		}

		/// <summary>
		/// Puts down a record describing a broadcast dispatcher.
		/// </summary>
		/// <param name="writeDispatcherSettings">A value indicating whether it is necessary to put down dispatcher's settings.</param>
		/// <param name="dispatcher">The broadcast dispatcher.</param>
		public void WriteDispatcherSettings(bool writeDispatcherSettings, Dispatcher dispatcher)
		{
			this.BinaryWriter.Write( (bool) writeDispatcherSettings );
			this.BinaryWriter.Write( (int) dispatcher.DbgDispatcherId );
			this.WriteString( dispatcher.DbgDispatcherName );

			if (! writeDispatcherSettings)
				return ;

			this.BinaryWriter.Write( true );
			this.WriteString("Interface");
			this.WriteString( dispatcher.SupportedInterface.ToString() );

			this.BinaryWriter.Write( true );
			this.WriteString("ReceiveResultsTimeOut");
			this.WriteString( dispatcher.ReceiveResultsTimeOut.TotalMilliseconds.ToString() );

			this.BinaryWriter.Write( true );
			this.WriteString("CallIsAsync");
			this.WriteString( dispatcher.CallIsAsync.ToString() );

			this.BinaryWriter.Write( true );
			this.WriteString("MaximumNumberOfConsecutiveFailsToExcludeReceiverAutomatically");
			this.WriteString( dispatcher.MaximumNumberOfConsecutiveFailsToExcludeReceiverAutomatically.ToString() );

			this.BinaryWriter.Write( true );
			this.WriteString("MaximumNumberOfConsecutiveFailsToEnableSimulationMode");
			this.WriteString( dispatcher.MaximumNumberOfConsecutiveFailsToEnableSimulationMode.ToString() );

			this.BinaryWriter.Write( true );
			this.WriteString("IgnoreRecurrentCalls");
			this.WriteString( dispatcher.IgnoreRecurrentCalls.ToString() );

			this.BinaryWriter.Write( true );
			this.WriteString("Local URI base");
			this.WriteString( GenuineUtility.DefaultHostIdentifier.ToString("N") );

			this.BinaryWriter.Write( false );
		}

		/// <summary>
		/// Puts down a record containing a reference to the specified ResultCollected.
		/// </summary>
		/// <param name="resultCollector">ResultCollector.</param>
		public void WriteResultCollectorId(ResultCollector resultCollector)
		{
			this.BinaryWriter.Write( (bool) (resultCollector != null) );
			if (resultCollector != null)
				this.BinaryWriter.Write( (int) resultCollector.DbgResultCollectorId);
		}

		/// <summary>
		/// Puts down a record containing information about the specified broadcast recipient.
		/// </summary>
		/// <param name="writeFullInformation">True if it is necessary to write all settings of the ReceiverInfo.</param>
		/// <param name="receiverInfo">The ReceiverInfo.</param>
		public void WriteReceiverInfo(bool writeFullInformation, ReceiverInfo receiverInfo)
		{
			this.BinaryWriter.Write( receiverInfo != null );
			if (receiverInfo == null)
				return ;

			this.BinaryWriter.Write( (int) receiverInfo.DbgRecipientId );
			this.BinaryWriter.Write( (bool) writeFullInformation );

			if (! writeFullInformation)
				return ;

			this.WriteString( receiverInfo.MbrUri);
			this.BinaryWriter.Write((bool) receiverInfo.Local);

			if (receiverInfo.GeneralBroadcastSender != null)
				this.WriteString( receiverInfo.GeneralBroadcastSender.Court);
			else
				this.BinaryWriter.Write(string.Empty);
		}

		#endregion

		#region -- Events --------------------------------------------------------------------------

		/// <summary>
		/// Puts down a record describing an implementation warning.
		/// </summary>
		/// <param name="author">The author.</param>
		/// <param name="type">The type of the event.</param>
		/// <param name="exception">The exception associated with the event.</param>
		/// <param name="sourceThreadId">The id of the thread.</param>
		/// <param name="sourceThreadName">The name of the thread.</param>
		/// <param name="description">The description of the event.</param>
		/// <param name="parameters">Parameters to the description.</param>
		public void WriteImplementationWarningEvent(string author, LogMessageType type, Exception exception, 
			int sourceThreadId, string sourceThreadName, string description, params object[] parameters)
		{
			lock (this._streamLock)
			{
				this.WriteRecordHeader(BinaryRecordVersion.ImplementationWarning, LogCategory.ImplementationWarning, type, author);

				this.WriteException( exception );
				this.BinaryWriter.Write( (int) sourceThreadId);
				this.WriteString(sourceThreadName);

				this.WriteStringWithParameters( description, parameters);

				this.BinaryWriter.Flush();
			}
		}

		/// <summary>
		/// Puts down a record describing a general Genuine Channels event.
		/// </summary>
		/// <param name="logCategory">The category of the event.</param>
		/// <param name="author">The author.</param>
		/// <param name="type">The type of the event(Subcategory).</param>
		/// <param name="exception">The exception associated with the event.</param>
		/// <param name="message">The message associated with the event.</param>
		/// <param name="remote">The remote host participating in the event.</param>
		/// <param name="content">The content associated with the record.</param>
		/// <param name="sourceThreadId">The id of the thread where the invocation was made.</param>
		/// <param name="sourceThreadName">The name of the thread.</param>
		/// <param name="securitySession">The Security Session.</param>
		/// <param name="securitySessionName">The name of the Security Session</param>
		/// <param name="connectionId">The identifier of the connection.</param>
		/// <param name="number1">An additional integer parameter.</param>
		/// <param name="number2">An additional integer parameter.</param>
		/// <param name="number3">An additional integer parameter.</param>
		/// <param name="string1">The first string that elaborates the current event.</param>
		/// <param name="string2">The second string that elaborates the current event.</param>
		/// <param name="string3">The third string that elaborates the current event.</param>
		/// <param name="string4">The fourth string that elaborates the current event.</param>
		/// <param name="description">The description of the event.</param>
		/// <param name="parameters">Parameters to the description.</param>
		public void WriteEvent(LogCategory logCategory, string author, LogMessageType type, Exception exception, 
			Message message, HostInformation remote, Stream content, int sourceThreadId, string sourceThreadName, 
			SecuritySession securitySession, string securitySessionName,
			int connectionId, int number1, int number2, int number3, string string1, string string2, string string3, string string4, string description, params object[] parameters)
		{
			lock (this._streamLock)
			{
				this.WriteRecordHeader(BinaryRecordVersion.GeneralRecord, logCategory, type, author);
				this.WriteException( exception );

				this.WriteMessageSeqNo( message );
				this.WriteHostInformationId( remote );
				this.WriteBinaryContent( content );

				this.BinaryWriter.Write( (int) sourceThreadId);
				this.WriteString( sourceThreadName );

				this.WriteSecuritySessionId( securitySession );
				this.WriteString( securitySessionName );

				this.BinaryWriter.Write( (int) connectionId );

				this.BinaryWriter.Write( (int) number1 );
				this.BinaryWriter.Write( (int) number2 );
				this.BinaryWriter.Write( (int) number3 );

				this.WriteString( string1 );
				this.WriteString( string2 );
				this.WriteString( string3 );
				this.WriteString( string4 );

				this.WriteStringWithParameters( description, parameters);

				this.BinaryWriter.Flush();
			}
		}

		/// <summary>
		/// Puts down a record representing an event describing a Message Processing event.
		/// </summary>
		/// <param name="author">The author.</param>
		/// <param name="type">The type of the event(Subcategory).</param>
		/// <param name="exception">The exception associated with the event.</param>
		/// <param name="message">The message associated with the event.</param>
		/// <param name="isSent">Whether the message is a request to the remote service (or a response from that service).</param>
		/// <param name="remote">The remote host participating in the event.</param>
		/// <param name="content">The content associated with the record.</param>
		/// <param name="invocationTarget">The target of the request (a null reference if it's a response).</param>
		/// <param name="methodName">The name of the method.</param>
		/// <param name="sourceThreadId">The id of the thread where the invocation was made.</param>
		/// <param name="sourceThreadName">The name of the thread.</param>
		/// <param name="transportName">The name of the transport.</param>
		/// <param name="connectionId">The identifier of the connection.</param>
		/// <param name="clssId">The identifier of the Security Session used on the connection level.</param>
		/// <param name="clssName">The name of the Security Session used on the connection level.</param>
		/// <param name="ilssId">The identifier of the Security Session used on the invocation level.</param>
		/// <param name="ilssName">The name of the Security Session used on the invocation level.</param>
		/// <param name="description">The description of the event.</param>
		/// <param name="parameters">Parameters to the description.</param>
		public void WriteMessageCreatedEvent(string author, LogMessageType type, Exception exception, Message message, bool isSent,
			HostInformation remote, Stream content, string invocationTarget, string methodName, int sourceThreadId, 
			string sourceThreadName, string transportName, int connectionId, 
			int clssId, string clssName, int ilssId, string ilssName, string description, params object[] parameters)
		{
			if (message == null)
			{
				this.WriteImplementationWarningEvent("BinaryLogWriter.WriteMessageCreatedEvent", LogMessageType.Error, null, 
					sourceThreadId, sourceThreadName, "The message is not provided. Stack trace: " + Environment.StackTrace);
				return ;
			}

			lock (this._streamLock)
			{
				this.WriteRecordHeader(BinaryRecordVersion.MessageRecord, LogCategory.MessageProcessing, type, author);
				this.WriteException( exception );

				this.WriteMessage( message, isSent, invocationTarget, methodName );
				this.WriteHostInformationId( remote );
				this.WriteBinaryContent( content );

				this.BinaryWriter.Write( (int) sourceThreadId);
				this.WriteString( sourceThreadName );

				this.WriteString( transportName);
				this.BinaryWriter.Write( (int) connectionId );
				this.BinaryWriter.Write( (int) clssId );
				this.WriteString( clssName );
				this.BinaryWriter.Write( (int) ilssId );
				this.WriteString( ilssName );
				this.WriteStringWithParameters( description, parameters);

				this.BinaryWriter.Flush();
			}
		}

		/// <summary>
		/// Puts down a record representing an event connected with the specified Security Session Parameters.
		/// </summary>
		/// <param name="author">The author.</param>
		/// <param name="type">The type of the event(Subcategory).</param>
		/// <param name="exception">The exception associated with the event.</param>
		/// <param name="message">The message associated with the event.</param>
		/// <param name="remote">The remote host participating in the event.</param>
		/// <param name="sourceThreadId">The id of the thread where the invocation was made.</param>
		/// <param name="sourceThreadName">The name of the thread.</param>
		/// <param name="securitySessionParameters">The Security Session Parameters.</param>
		/// <param name="description">The description of the event.</param>
		/// <param name="parameters">Parameters to the description.</param>
		public void WriteSecuritySessionParametersEvent(string author, LogMessageType type, Exception exception, 
			Message message, HostInformation remote, int sourceThreadId, string sourceThreadName,
			SecuritySessionParameters securitySessionParameters, string description, params object[] parameters)
		{
			if (message == null || securitySessionParameters == null)
			{
				this.WriteImplementationWarningEvent("BinaryLogWriter.WriteMessageCreatedEvent", LogMessageType.Error, null, 
					sourceThreadId, sourceThreadName, "The message or SSP is not provided. Stack trace: " + Environment.StackTrace);
				return ;
			}

			lock (this._streamLock)
			{
				this.WriteRecordHeader(BinaryRecordVersion.SecuritySessionParameters, LogCategory.Security, type, author);
				this.WriteException( exception );

				this.WriteMessageSeqNo(message);
				this.WriteHostInformationId(remote);

				this.BinaryWriter.Write( (int) sourceThreadId);
				this.WriteString( sourceThreadName );

				this.WriteSecuritySessionParameters( securitySessionParameters );
				this.WriteStringWithParameters( description, parameters);

				this.BinaryWriter.Flush();
			}

		}

		/// <summary>
		/// Puts down detailed information about the specified instance of the HostInformation class.
		/// </summary>
		/// <param name="author">The author.</param>
		/// <param name="type">The type of the event(Subcategory).</param>
		/// <param name="exception">The exception associated with the event.</param>
		/// <param name="remote">The remote host participating in the event.</param>
		/// <param name="sourceThreadId">The id of the thread where the invocation was made.</param>
		/// <param name="sourceThreadName">The name of the thread.</param>
		/// <param name="securitySession">The Security Session.</param>
		/// <param name="securitySessionName">The name of the Security Session</param>
		/// <param name="connectionId">The connection identifier.</param>
		/// <param name="description">The description of the event.</param>
		/// <param name="parameters">Parameters to the description.</param>
		public void WriteHostInformationEvent(string author, LogMessageType type, Exception exception, 
			HostInformation remote, int sourceThreadId, string sourceThreadName,
			SecuritySession securitySession, string securitySessionName, int connectionId, 
			string description, params object[] parameters)
		{
			if (remote == null)
			{
				this.WriteImplementationWarningEvent("BinaryLogWriter.WriteHostInformationCreatedEvent", LogMessageType.Error, null, 
					sourceThreadId, sourceThreadName, "The reference is null. Stack trace: " + Environment.StackTrace);
				return ;
			}

			lock (this._streamLock)
			{
				this.WriteRecordHeader(BinaryRecordVersion.HostInformationInfo, LogCategory.HostInformation, type, author);
				this.WriteException( exception );

				this.WriteHostInformation(remote);

				this.BinaryWriter.Write( (int) sourceThreadId);
				this.WriteString( sourceThreadName );

				this.WriteSecuritySessionId( securitySession );
				this.WriteString( securitySessionName );

				this.BinaryWriter.Write( (int) connectionId );
				this.WriteStringWithParameters( description, parameters);

				this.BinaryWriter.Flush();
			}
		}

		/// <summary>
		/// Puts down a record containing Connection Parameters.
		/// </summary>
		/// <param name="logCategory">The category of the event.</param>
		/// <param name="author">The author.</param>
		/// <param name="type">The type of the event(Subcategory).</param>
		/// <param name="exception">The exception associated with the event.</param>
		/// <param name="remote">The remote host participating in the event.</param>
		/// <param name="iParameterProvider">The connection parameters.</param>
		/// <param name="sourceThreadId">The id of the thread where the invocation was made.</param>
		/// <param name="sourceThreadName">The name of the thread.</param>
		/// <param name="connectionId">The identifier of the connection.</param>
		/// <param name="description">The description of the event.</param>
		/// <param name="parameters">Parameters to the description.</param>
		public void WriteConnectionParameterEvent(LogCategory logCategory, string author, LogMessageType type, Exception exception,
			HostInformation remote, IParameterProvider iParameterProvider, int sourceThreadId, string sourceThreadName, 
			int connectionId, string description, params object[] parameters)
		{
			lock (this._streamLock)
			{
				this.WriteRecordHeader(BinaryRecordVersion.ConnectionParametersRecord, logCategory, type, author);
				this.WriteException( exception );

				this.WriteHostInformationId( remote );
				this.BinaryWriter.Write( (int) connectionId );
				this.WriteConnectionParameters(iParameterProvider);

				this.BinaryWriter.Write( (int) sourceThreadId);
				this.WriteString( sourceThreadName );

				this.WriteStringWithParameters( description, parameters);

				this.BinaryWriter.Flush();
			}
		}

		/// <summary>
		/// Puts down a record describing the transport sending or receiving content actions.
		/// </summary>
		/// <param name="logCategory">The category of the event.</param>
		/// <param name="author">The author.</param>
		/// <param name="type">The type of the event(Subcategory).</param>
		/// <param name="exception">The exception associated with the event.</param>
		/// <param name="message">The message associated with the event.</param>
		/// <param name="remote">The remote host participating in the event.</param>
		/// <param name="content">The content associated with the record.</param>
		/// <param name="sourceThreadId">The id of the thread where the invocation was made.</param>
		/// <param name="sourceThreadName">The name of the thread.</param>
		/// <param name="connectionId">The identifier of the connection.</param>
		/// <param name="contentSize">The size of the content.</param>
		/// <param name="remoteHostAddress">The address of the remote host.</param>
		/// <param name="string1">The first string that elaborates the current event.</param>
		/// <param name="string2">The second string that elaborates the current event.</param>
		/// <param name="description">The description of the event.</param>
		/// <param name="parameters">Parameters to the description.</param>
		public void WriteTransportContentEvent(LogCategory logCategory, string author, LogMessageType type, Exception exception, 
			Message message, HostInformation remote, Stream content, int sourceThreadId, string sourceThreadName, 
			int connectionId, int contentSize, string remoteHostAddress, string string1, string string2, 
			string description, params object[] parameters)
		{
			lock (this._streamLock)
			{
				this.WriteRecordHeader(BinaryRecordVersion.TransportContentRecord, logCategory, type, author);
				this.WriteException( exception );

				this.WriteMessageSeqNo( message );
				this.WriteHostInformationId( remote );
				this.WriteBinaryContent( content );

				this.BinaryWriter.Write( (int) sourceThreadId);
				this.WriteString( sourceThreadName );

				this.BinaryWriter.Write( (int) connectionId );
				this.BinaryWriter.Write( (int) contentSize );
				this.WriteString( remoteHostAddress );

				this.WriteString( string1 );
				this.WriteString( string2 );
				this.WriteStringWithParameters( description, parameters);

				this.BinaryWriter.Flush();
			}
		}

		/// <summary>
		/// Puts down a record describing a general Genuine Channels event.
		/// </summary>
		/// <param name="logCategory">The category of the event.</param>
		/// <param name="author">The author.</param>
		/// <param name="type">The type of the event(Subcategory).</param>
		/// <param name="exception">The exception associated with the event.</param>
		/// <param name="message">The message associated with the event.</param>
		/// <param name="remote">The remote host participating in the event.</param>
		/// <param name="content">The content associated with the record.</param>
		/// <param name="sourceThreadId">The id of the thread where the invocation was made.</param>
		/// <param name="sourceThreadName">The name of the thread.</param>
		/// <param name="securitySession">The Security Session.</param>
		/// <param name="securitySessionName">The name of the Security Session</param>
		/// <param name="writeDispatcherSettings">A value indicating whether it is necessary to put down broadcast dispatcher's settings.</param>
		/// <param name="dispatcher">The broadcast dispatcher.</param>
		/// <param name="resultCollector">The broadcast result collector.</param>
		/// <param name="writeReceiverInfoSettings">A value indicating whether it is necessary to put down information about the specified broadcast recipient.</param>
		/// <param name="receiverInfo">The broadcast recipient.</param>
		/// <param name="string1">The first string that elaborates the current event.</param>
		/// <param name="string2">The second string that elaborates the current event.</param>
		/// <param name="description">The description of the event.</param>
		/// <param name="parameters">Parameters to the description.</param>
		public void WriteBroadcastEngineEvent(LogCategory logCategory, string author, LogMessageType type, Exception exception, 
			Message message, HostInformation remote, Stream content, int sourceThreadId, string sourceThreadName, 
			SecuritySession securitySession, string securitySessionName,
			bool writeDispatcherSettings, Dispatcher dispatcher, ResultCollector resultCollector, bool writeReceiverInfoSettings, 
			ReceiverInfo receiverInfo, string string1, string string2, string description, params object[] parameters)
		{
			if (dispatcher == null)
			{
				this.WriteImplementationWarningEvent("BinaryLogWriter.WriteBroadcastEngineEvent", LogMessageType.Error, null, 
					sourceThreadId, sourceThreadName, "The reference is null. Stack trace: " + Environment.StackTrace);
				return ;
			}

			lock (this._streamLock)
			{
				this.WriteRecordHeader(BinaryRecordVersion.TransportBroadcastEngineRecord, logCategory, type, author);
				this.WriteException( exception );

				this.WriteMessageSeqNo( message );
				this.WriteHostInformationId( remote );
				this.WriteBinaryContent( content );

				this.BinaryWriter.Write( (int) sourceThreadId);
				this.WriteString( sourceThreadName );

				this.WriteSecuritySessionId( securitySession );
				this.WriteString( securitySessionName );

				this.WriteResultCollectorId(resultCollector);
				this.WriteDispatcherSettings(writeDispatcherSettings, dispatcher);
				this.WriteReceiverInfo(writeReceiverInfoSettings, receiverInfo);

				this.WriteString( string1 );
				this.WriteString( string2 );
				this.WriteStringWithParameters( description, parameters);

				this.BinaryWriter.Flush();
			}
		}

		/// <summary>
		/// Puts down a record containing information about the current environment.
		/// </summary>
		protected void WriteVersionInformationEvent()
		{
			lock (this._streamLock)
			{
				this.WriteRecordHeader(BinaryRecordVersion.VersionRecord, LogCategory.Version, LogMessageType.Warning, "BinaryLogWriter.WriteVersionInformationEvent");

				this.BinaryWriter.Write( (bool) true );
				this.WriteString("Genuine Channels Version");
				this.WriteString(this.GetType().AssemblyQualifiedName);

				this.BinaryWriter.Write( (bool) true );
				this.WriteString("Is Genuine Channels in GAC");
				this.WriteString(this.GetType().Assembly.GlobalAssemblyCache.ToString());

				this.BinaryWriter.Write( (bool) true );
				this.WriteString("Location of Genuine Channels");
				this.WriteString(this.GetType().Assembly.Location);

				// Framework 1.1 only
//				this.BinaryWriter.Write( (bool) true );
//				this.WriteString("Image Runtime Version");
//				this.WriteString(this.GetType().Assembly.ImageRuntimeVersion);

				this.BinaryWriter.Write( (bool) true );
				this.WriteString("OS version");
				this.WriteString(Environment.OSVersion.ToString());

				this.BinaryWriter.Write( (bool) true );
				this.WriteString("CLR version");
				this.WriteString(Environment.Version.ToString());

				this.BinaryWriter.Write( (bool) false );

				this.BinaryWriter.Flush();
			}
		}

		#endregion

		#region -- Utility functions ---------------------------------------------------------------

		/// <summary>
		/// Parses and returns the invocation target.
		/// </summary>
		/// <param name="methodName">The name of the method.</param>
		/// <param name="typeName">The name of the type.</param>
		/// <returns>The invocation target.</returns>
		public static string ParseInvocationMethod(string methodName, string typeName)
		{
			if (methodName == null)
				methodName = string.Empty;
			if (typeName == null)
				typeName = string.Empty;

			int commaPos = typeName.IndexOf(',');

			if (commaPos > 0)
				return typeName.Substring(0, commaPos) + "." + methodName;
			return typeName + "; " + methodName;
		}

		#endregion

	}
}
