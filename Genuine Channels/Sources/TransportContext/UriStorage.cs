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
using Belikov.GenuineChannels.Utilities;

using Belikov.Common.ThreadProcessing;

namespace Belikov.GenuineChannels.TransportContext
{
	/// <summary>
	/// Thread-safe collections of references associating URIs with appropriate Transport Contexts.
	/// </summary>
	internal class UriStorage : ITimerConsumer
	{
		/// <summary>
		/// Constructs an instance of the UriStorage class.
		/// </summary>
		public UriStorage()
		{
			TimerProvider.Attach(this);
		}

		/// <summary>
		/// To collect unavailable Transport Contexts.
		/// </summary>
		private static UriStorage _sigleton = new UriStorage();

		/// <summary>
		/// Thread-safe collections of weak references associating URIs with appropriate Transport Contexts.
		/// </summary>
		private static Hashtable _knownUris = Hashtable.Synchronized(new Hashtable());

		/// <summary>
		/// Gets a Transport Context responsible for the connection to the specified remoteUri.
		/// </summary>
		/// <param name="remoteUri">The uri of the remote host.</param>
		/// <returns>Transport Context or a null reference.</returns>
		public static ITransportContext GetTransportContext(string remoteUri)
		{
			ITransportContext iTransportContext = null;

			WeakReference weakReference = _knownUris[remoteUri] as WeakReference;
			if (weakReference != null)
				iTransportContext = weakReference.Target as ITransportContext;

			return iTransportContext;
		}

		/// <summary>
		/// Registers message handler.
		/// Overrides old message handlers with the same names.
		/// </summary>
		/// <param name="remoteUri">The URI of the remote host.</param>
		/// <param name="iTransportContext">Transport Context servicing a connection to the remote host.</param>
		public static void RegisterConnection(string remoteUri, ITransportContext iTransportContext)
		{
			_knownUris[remoteUri] = new WeakReference(iTransportContext);
		}

		/// <summary>
		/// Unregisters unavailable Transport Contexts.
		/// Does not throw any exceptions.
		/// </summary>
		public static void UnregisterUnavailableTransportContexts()
		{
			try
			{
				ArrayList keysToRemove = new ArrayList();

				lock (_knownUris.SyncRoot)
				{
					foreach ( DictionaryEntry dictionaryEntry in _knownUris )
					{
						WeakReference weakReference = dictionaryEntry.Value as WeakReference;
						if (weakReference == null || ! weakReference.IsAlive)
							keysToRemove.Add(dictionaryEntry.Key);
					}

					for ( int i = 0; i < keysToRemove.Count; i++)
						_knownUris.Remove(keysToRemove[i]);
				}
			}
			catch(Exception ex)
			{
				// LOG:
				BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;
				if ( binaryLogWriter != null && binaryLogWriter[LogCategory.MessageProcessing] > 0 )
					binaryLogWriter.WriteEvent(LogCategory.MessageProcessing, "UriStorage.UnregisterUnavailableTransportContexts",
						LogMessageType.Error, ex, null, null, null, 
						GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
						null, null, -1, 0, 0, 0, null, null, null, null,
						"Unexpected exception occurred while unregistering unavailable transport contexts.");
			}
		}

		/// <summary>
		/// Is called at specified intervals.
		/// The procedure being called must not do any long-duration perfomance during this call.
		/// </summary>
		public void TimerCallback()
		{
			UnregisterUnavailableTransportContexts();
		}


	}
}
