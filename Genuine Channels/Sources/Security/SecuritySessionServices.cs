/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.IO;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Messaging;
using System.Threading;

using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Parameters;
using Belikov.GenuineChannels.TransportContext;
using Belikov.GenuineChannels.Utilities;

namespace Belikov.GenuineChannels.Security
{
	/// <summary>
	/// Provides several methods and properties for using Security Sessions.
	/// </summary>
	public class SecuritySessionServices
	{
		/// <summary>
		/// Defines the basic Security Session sending plain content.
		/// </summary>
		public static readonly SecuritySessionParameters DefaultContext = new SecuritySessionParameters("D");

		/// <summary>
		/// Defines the basic Security Session with enabled compression.
		/// </summary>
		public static readonly SecuritySessionParameters DefaultContextWithCompression = new SecuritySessionParameters("D", SecuritySessionAttributes.EnableCompression, TimeSpan.MinValue);

		/// <summary>
		/// The name of the default security-less Key Provider.
		/// </summary>
		public static SecuritySessionParameters DefaultSecuritySession
		{
			get
			{
				lock(_defaultSecuritySessionObject)
					return _defaultSecuritySession;
			}
			set
			{
				lock(_defaultSecuritySessionObject)
					_defaultSecuritySession = value;
			}
		}
		private static SecuritySessionParameters _defaultSecuritySession = DefaultContext;
		private static object _defaultSecuritySessionObject = new object();

		#region -- Current Context Management ------------------------------------------------------

		/// <summary>
		/// Specifies the security context used for all invocations made in the current thread.
		/// </summary>
		/// <param name="securitySessionParameters">Security Session parameters.</param>
		/// <returns>The name of the previous security context or null.</returns>
		public static SecuritySessionParameters SetCurrentSecurityContext(SecuritySessionParameters securitySessionParameters)
		{
			LocalDataStoreSlot localDataStoreSlot = Thread.GetNamedDataSlot(OccupiedThreadSlots.CurrentSecuritySessionParameters);

			object previousDataSlotValue = Thread.GetData(localDataStoreSlot);
			Thread.SetData(localDataStoreSlot, securitySessionParameters);

			return previousDataSlotValue as SecuritySessionParameters;
		}

		/// <summary>
		/// Gets the security session being used in the current thread context.
		/// </summary>
		/// <returns>The name of the current security context.</returns>
		public static SecuritySessionParameters GetCurrentSecurityContext()
		{
			LocalDataStoreSlot localDataStoreSlot = Thread.GetNamedDataSlot(OccupiedThreadSlots.CurrentSecuritySessionParameters);
			return Thread.GetData(localDataStoreSlot) as SecuritySessionParameters;
		}

		#endregion

		#region -- Global Security Key Providers ---------------------------------------------------

		/// <summary>
		/// Contains security session factories [name] => [IKeyProvider].
		/// </summary>
		private static Hashtable _globalKeys = Hashtable.Synchronized(new Hashtable());

		/// <summary>
		/// Gets a key provider.
		/// </summary>
		/// <param name="name">The name of key provider.</param>
		/// <returns>Created key provider.</returns>
		public static IKeyProvider GetGlobalKey(string name)
		{
			return _globalKeys[name] as IKeyProvider;
		}

		/// <summary>
		/// Associates the provided key provider with the specified Security Session name.
		/// Removes the record if iKeyProvider is a null reference.
		/// </summary>
		/// <param name="name">The name of Security Context.</param>
		/// <param name="iKeyProvider">The key provider or a null reference.</param>
		public static void SetGlobalKey(string name, IKeyProvider iKeyProvider)
		{
			BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;

			if (iKeyProvider == null)
			{
				// LOG:
				if (binaryLogWriter != null && binaryLogWriter[LogCategory.Security] > 0 )
					binaryLogWriter.WriteEvent(LogCategory.Security, "SecuritySessionServices.SetGlobalKey",
						LogMessageType.KeyProviderDissociated, null, null, null, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, 
						null, null, -1, 
						0, 0, 0, iKeyProvider.ToString(), name, null, null,
						"The Key Provider (\"{0}\") has been dissociated from the \"{1}\" name.", iKeyProvider.ToString(), name);

				_globalKeys.Remove(name);
			}
			else
			{
				if (binaryLogWriter != null && binaryLogWriter[LogCategory.Security] > 0 )
					binaryLogWriter.WriteEvent(LogCategory.Security, "SecuritySessionServices.SetGlobalKey",
						LogMessageType.KeyProviderAssociated, null, null, null, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, -1, 
						0, 0, 0, iKeyProvider.ToString(), name, null, null,
						"The Key Provider (\"{0}\") has been associated with the \"{1}\" name.", iKeyProvider.ToString(), name);

				_globalKeys[name] = iKeyProvider;
			}
		}

		#endregion

	}
}
