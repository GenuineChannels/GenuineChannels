/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.Threading;

using Belikov.GenuineChannels.Logbook;

namespace Belikov.GenuineChannels.Security
{
	/// <summary>
	/// Implements the key store functionality.
	/// </summary>
	public class KeyStore : MarshalByRefObject, IKeyStore
	{
		/// <summary>
		/// Constructs an instance of the KeyStore class.
		/// </summary>
		public KeyStore()
		{
			this._keys[SecuritySessionServices.DefaultContext.Name] = new KeyProvider_Basic();
		}

		/// <summary>
		/// Contains security session factories [name] => [IKeyProvider].
		/// </summary>
		private Hashtable _keys = Hashtable.Synchronized(new Hashtable());

		/// <summary>
		/// Gets a local or global key provider associated with the specified name.
		/// If there is no local key provider associated with the specified name, this method returns the corresponding Key Provider
		/// from the global Security Session Key Providers collection.
		/// </summary>
		/// <param name="name">The name of key provider.</param>
		/// <returns>Created key provider.</returns>
		public IKeyProvider GetKey(string name)
		{
			IKeyProvider keyProvider = this._keys[name] as IKeyProvider;

			if (keyProvider == null)
				keyProvider = SecuritySessionServices.GetGlobalKey(name);

			return keyProvider;
		}

		/// <summary>
		/// Associates the provided key provider with the specified Security Session name.
		/// Removes the record if iKeyProvider is a null reference.
		/// </summary>
		/// <param name="name">The name of Security Context.</param>
		/// <param name="iKeyProvider">The key provider or a null reference.</param>
		public void SetKey(string name, IKeyProvider iKeyProvider)
		{
			BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;

			if (iKeyProvider == null)
			{
				// LOG:
				if (binaryLogWriter != null && binaryLogWriter[LogCategory.Security] > 0 )
					binaryLogWriter.WriteEvent(LogCategory.Security, "KeyStore.SetKey",
						LogMessageType.KeyProviderDissociated, null, null, null, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null,
						name, -1, 
						0, 0, 0, iKeyProvider.ToString(), name, null, null,
						"The Key Provider (\"{0}\") has been dissociated from the \"{1}\" name.", iKeyProvider.ToString(), name);

				this._keys.Remove(name);
			}
			else
			{
				// LOG:
				if (binaryLogWriter != null && binaryLogWriter[LogCategory.Security] > 0 )
					binaryLogWriter.WriteEvent(LogCategory.Security, "SecuritySessionServices.SetGlobalKey",
						LogMessageType.KeyProviderAssociated, null, null, null, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name, null,
						name, -1, 
						0, 0, 0, iKeyProvider.ToString(), name, null, null,
						"The Key Provider (\"{0}\") has been associated with the \"{1}\" name.", iKeyProvider.ToString(), name);

				this._keys[name] = iKeyProvider;
			}
		}
	}
}
