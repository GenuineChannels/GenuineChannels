/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.Parameters;

namespace Belikov.GenuineChannels.Security
{
	/// <summary>
	/// Enumerates Security Session attributes. These flags specify run-time behavior, 
	/// serialization/deserialization, security context switching and connection management.
	/// WARNING: Serialized as a word (16-bit) value.
	/// </summary>
	[Flags]
	public enum SecuritySessionAttributes
	{
		/// <summary>
		/// No parameters requested.
		/// </summary>
		None = 0x0000,

		/// <summary>
		/// Forces content compression.
		/// </summary>
		EnableCompression = 0x0001,

//		/// <summary>
//		/// Forces the invocation to be executed in the separate thread.
//		/// </summary>
//		ThreadExecution = 0x0002,
//
//		/// <summary>
//		/// Forces Genuine Pipe channel impersonation. Valid only if you use Genuine Pipe Channel.
//		/// </summary>
//		PipeImpersonation = 0x0004,
//
//		/// <summary>
//		/// Is reserved for future use.
//		/// </summary>
//		Reserved1 = 0x0008,
//
//		/// <summary>
//		/// Is reserved for future use.
//		/// </summary>
//		Reserved2 = 0x0010,

		/// <summary>
		/// Forces asynchonous sending of the message.
		/// </summary>
		ForceAsync = 0x0020,

		/// <summary>
		/// Forces synchonous sending of the message.
		/// </summary>
		ForceSync = 0x0040,

		/// <summary>
		/// Invocation must be sent through the established named connection.
		/// An exception is thrown if there is no such connection, or it was closed.
		/// </summary>
		UseExistentConnection = 0x0100,
	}

	/// <summary>
	/// Contains read-only Security Session name and read-only run-time parameters.
	/// </summary>
	public class SecuritySessionParameters
	{
		/// <summary>
		/// Constructs an instance of the SecuritySessionParameters class.
		/// </summary>
		/// <param name="securitySessionName">The name of the Security Session.</param>
		public SecuritySessionParameters(string securitySessionName)
		{
			this.Name = securitySessionName;

			this._genuineConnectionType = GenuineConnectionType.Persistent;
		}

		/// <summary>
		/// Constructs an instance of the SecuritySessionParameters class.
		/// </summary>
		/// <param name="securitySessionName">The name of the Security Session.</param>
		/// <param name="securitySessionAttributes">Security Session parameters.</param>
		/// <param name="timeout">The invocation timeout or the TimeSpan.MinValue value to inherit the timeout from channel or Transport Context settings.</param>
		public SecuritySessionParameters(string securitySessionName, SecuritySessionAttributes securitySessionAttributes, TimeSpan timeout)
		{
			this.Name = securitySessionName;
			this.Attributes = securitySessionAttributes;
			this.Timeout = timeout;

			this._genuineConnectionType = GenuineConnectionType.Persistent;
		}

		/// <summary>
		/// Constructs an instance of the SecuritySessionParameters class.
		/// </summary>
		/// <param name="securitySessionName">The name of the Security Session.</param>
		/// <param name="securitySessionAttributes">Security Session parameters.</param>
		/// <param name="timeout">The invocation timeout or the TimeSpan.MinValue value to inherit the timeout from channel or Transport Context settings.</param>
		/// <param name="remoteTransportUser">For load balancing or dispatching the invocation to the specific known appdomain.</param>
		public SecuritySessionParameters(string securitySessionName, SecuritySessionAttributes securitySessionAttributes, TimeSpan timeout, string remoteTransportUser)
		{
			this.Name = securitySessionName;
			this.Attributes = securitySessionAttributes;
			this.Timeout = timeout;

			this._genuineConnectionType = GenuineConnectionType.Persistent;
			this.RemoteTransportUser = remoteTransportUser;
		}

		/// <summary>
		/// Constructs an instance of the SecuritySessionParameters class.
		/// </summary>
		/// <param name="securitySessionName">The name of the Security Session.</param>
		/// <param name="securitySessionAttributes">Security Session parameters.</param>
		/// <param name="timeout">The invocation timeout or the TimeSpan.MinValue value to inherit the timeout from channel or Transport Context settings.</param>
		/// <param name="genuineConnectionType">Type of the connection being used. The connection type must be support by the corresponding connection manager.</param>
		/// <param name="connecitonName">The name of the connection if named connection type is specified.</param>
		/// <param name="closeAfterInactivity">The time of inactivity to close a connection or TimeSpan.MinValue to inherit this value from channel or Transport Context parameters.</param>
		public SecuritySessionParameters(string securitySessionName, SecuritySessionAttributes securitySessionAttributes, TimeSpan timeout, GenuineConnectionType genuineConnectionType, string connecitonName, TimeSpan closeAfterInactivity)
		{
			this.Name = securitySessionName;
			this.Attributes = securitySessionAttributes;
			this.Timeout = timeout;

			this._genuineConnectionType = genuineConnectionType;
			this._connectionName = connecitonName;
			this.CloseAfterInactivity = closeAfterInactivity;
		}

		/// <summary>
		/// Constructs an instance of the SecuritySessionParameters class.
		/// </summary>
		/// <param name="securitySessionName">The name of the Security Session.</param>
		/// <param name="securitySessionAttributes">Security Session parameters.</param>
		/// <param name="timeout">The invocation timeout or the TimeSpan.MinValue value to inherit the timeout from channel or Transport Context settings.</param>
		/// <param name="genuineConnectionType">Type of the connection being used. The connection type must be support by the corresponding connection manager.</param>
		/// <param name="connecitonName">The name of the connection if named connection type is specified.</param>
		/// <param name="closeAfterInactivity">The time of inactivity to close a connection or TimeSpan.MinValue to inherit this value from channel or Transport Context parameters.</param>
		/// <param name="remoteTransportUser">For load balancing or dispatching the invocation to the specific known appdomain.</param>
		public SecuritySessionParameters(string securitySessionName, SecuritySessionAttributes securitySessionAttributes, TimeSpan timeout, GenuineConnectionType genuineConnectionType, string connecitonName, TimeSpan closeAfterInactivity, string remoteTransportUser)
		{
			this.Name = securitySessionName;
			this.Attributes = securitySessionAttributes;
			this.Timeout = timeout;

			this._genuineConnectionType = genuineConnectionType;
			this._connectionName = connecitonName;
			this.CloseAfterInactivity = closeAfterInactivity;
			this.RemoteTransportUser = remoteTransportUser;
		}

		/// <summary>
		/// The name of the Security Session.
		/// </summary>
		public readonly string Name;

		/// <summary>
		/// Security Session attributes.
		/// </summary>
		public readonly SecuritySessionAttributes Attributes;

		/// <summary>
		/// Type of the used connection.
		/// </summary>
		public GenuineConnectionType GenuineConnectionType
		{
			get
			{
				return this._genuineConnectionType;
			}
		}
		internal GenuineConnectionType _genuineConnectionType;

		/// <summary>
		/// Defines a connection to be used.
		/// </summary>
		public string ConnectionName
		{
			get
			{
				return this._connectionName;
			}
		}
		internal string _connectionName;

		/// <summary>
		/// For load balancing or dispatching the invocation to the specific known appdomain.
		/// </summary>
		public readonly string RemoteTransportUser = string.Empty;

		/// <summary>
		/// Time span to wait for a response. If the response is not received for this time,
		/// OperationException with "GenuineChannels.Exception.Send.ServerDidNotReply" identifier
		/// is thrown. The TimeSpan.MinValue value indicating that the value will be inherited from the
		/// Transport Context parameters.
		/// </summary>
		public readonly TimeSpan Timeout = TimeSpan.MinValue;

		/// <summary>
		/// Opened separate connection will not be closed and can be reused for this time span.
		/// The TimeSpan.MinValue value indicating that the value will be inherited from the
		/// Transport Context parameters.
		/// </summary>
		public TimeSpan CloseAfterInactivity = TimeSpan.MinValue;

		/// <summary>
		/// Specify the maximum allowed size of the content. The zero value indicating that 
		/// the value will be inherited from Transport Context parameters.
		/// </summary>
		public int MaxContentSize = 0;

		/// <summary>
		/// Returns a String that represents the current Object.
		/// </summary>
		/// <returns>A String that represents the current Object.</returns>
		public override string ToString()
		{
			return string.Format("SecuritySessionParameters (Name: {0}; Attributes: {1}; Connection name: {2}; Remote transport user: {3}; Timeout: {4} ms; Close after inactivity: {5} ms; Max content size: {6}",
				this.Name, Enum.Format(typeof(GenuineConnectionType), this.GenuineConnectionType, "g"), this.ConnectionName, this.RemoteTransportUser, this.Timeout.TotalMilliseconds, this.CloseAfterInactivity.TotalMilliseconds, this.MaxContentSize);
		}

	}
}
