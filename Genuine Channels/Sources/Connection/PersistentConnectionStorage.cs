/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;

namespace Belikov.GenuineChannels.Connection
{
	/// <summary>
	/// Implements a storage of connections classified by URIs and connection identifiers.
	/// </summary>
	internal class PersistentConnectionStorage
	{
		/// <summary>
		/// Constructs an instance of the ConnectionStorage.
		/// </summary>
		public PersistentConnectionStorage()
		{
		}

		/// <summary>
		/// A storage containing all connections to one remote host.
		/// </summary>
		private class ObjectStorageWithDefaultEntry
		{
			/// <summary>
			/// The collection of named objects.
			/// </summary>
			public Hashtable Collection = new Hashtable();

			/// <summary>
			/// The default object.
			/// </summary>
			public object DefaultObject;
		}

		/// <summary>
		/// Associates host URIs with ObjectStorageWithDefaultEntry.
		/// </summary>
		private Hashtable _byUri = new Hashtable();

		/// <summary>
		/// Associates the provided connection with the specified URI and name.
		/// </summary>
		/// <param name="primaryUri">The primary URI of the remote host.</param>
		/// <param name="name">The name of the connection.</param>
		/// <param name="connection">The connection.</param>
		public void Set(string primaryUri, string name, object connection)
		{
			lock (this.SyncRoot)
			{
				ObjectStorageWithDefaultEntry objectStorageWithDefaultEntry = this._byUri[primaryUri] as ObjectStorageWithDefaultEntry;
				if (objectStorageWithDefaultEntry == null)
					this._byUri[primaryUri] = objectStorageWithDefaultEntry = new ObjectStorageWithDefaultEntry();

				objectStorageWithDefaultEntry.Collection[name] = connection;
				objectStorageWithDefaultEntry.DefaultObject = connection;
			}
		}

		/// <summary>
		/// Removes information about an object associated with the specified name.
		/// </summary>
		/// <param name="primaryUri">The primary URI of the remote host.</param>
		/// <param name="name">The name of the object.</param>
		public void Remove(string primaryUri, string name)
		{
			lock (this.SyncRoot)
			{
				ObjectStorageWithDefaultEntry objectStorageWithDefaultEntry = this._byUri[primaryUri] as ObjectStorageWithDefaultEntry;
				if (objectStorageWithDefaultEntry == null)
					return ;

				bool changeDefaultObject = (name == null ? true : object.ReferenceEquals(objectStorageWithDefaultEntry.Collection[name], objectStorageWithDefaultEntry.DefaultObject));

				if (name != null)
					objectStorageWithDefaultEntry.Collection.Remove(name);
					
				if (objectStorageWithDefaultEntry.Collection.Count <= 0)
					this._byUri.Remove(primaryUri);
				else
				{
					if (changeDefaultObject)
					{
						objectStorageWithDefaultEntry.DefaultObject = null;

						// just get another one
						foreach (DictionaryEntry entry in objectStorageWithDefaultEntry.Collection)
						{
							objectStorageWithDefaultEntry.DefaultObject = entry.Value;
							break;
						}
					}
				}
			}
		}

		/// <summary>
		/// Returns a connection associated with the specified URI and connection name..
		/// </summary>
		/// <param name="primaryUri">The primary URI.</param>
		/// <param name="name">The name of the connection or a null reference to get the default connection.</param>
		/// <returns>A connection associated with the specified name.</returns>
		public object Get(string primaryUri, string name)
		{
			if (primaryUri == null)
				return null;
				
			lock (this.SyncRoot)
			{
				ObjectStorageWithDefaultEntry objectStorageWithDefaultEntry = this._byUri[primaryUri] as ObjectStorageWithDefaultEntry;
				if (objectStorageWithDefaultEntry == null)
					return null;

				if (name == null || name.Length <= 0)
					return objectStorageWithDefaultEntry.DefaultObject;

				return objectStorageWithDefaultEntry.Collection[name];
			}
		}

		/// <summary>
		/// Returns a list of connection names associated with the specified URI.
		/// </summary>
		/// <param name="primaryUri">The primary URI.</param>
		/// <returns></returns>
		public string[] GetAll(string primaryUri)
		{
			lock (this.SyncRoot)
			{
				ObjectStorageWithDefaultEntry objectStorageWithDefaultEntry = this._byUri[primaryUri] as ObjectStorageWithDefaultEntry;
				if (objectStorageWithDefaultEntry == null)
					return null;

				// returns all keys (connection names)
				string[] connectionNames = new string[objectStorageWithDefaultEntry.Collection.Count];
				objectStorageWithDefaultEntry.Collection.Keys.CopyTo(connectionNames, 0);

				return connectionNames;
			}
		}

		/// <summary>
		/// Gets an object that can be used to synchronize access to the collection.
		/// </summary>
		public object SyncRoot 
		{
			get
			{
				return this;
			}
		}

		#region -- IEnumerable Members -------------------------------------------------------------

		/// <summary>
		/// Represents a method analyzing connection's stuff.
		/// </summary>
		public delegate void ProcessConnectionEventHandler(object connection, object parameter);

		/// <summary>
		/// Inspects all available connections. Used the specified method to analyze each connection.
		/// </summary>
		/// <param name="processConnectionEventHandler">The late-bound method analyzing the state of the connection.</param>
		/// <param name="parameter">A parameter provided to the handler.</param>
		public void InspectAllConnections(ProcessConnectionEventHandler processConnectionEventHandler, object parameter)
		{
			ArrayList allConnectionsToAllHosts = new ArrayList();

			lock (this.SyncRoot)
			{
                // go through all available hosts
                foreach (DictionaryEntry entry in this._byUri)
                {
                    ObjectStorageWithDefaultEntry objectStorageWithDefaultEntry = (ObjectStorageWithDefaultEntry)entry.Value;

                    // and collect all connections
                    foreach (DictionaryEntry connectionEntry in objectStorageWithDefaultEntry.Collection)
                        allConnectionsToAllHosts.Add(connectionEntry.Value);
                }

			}
        
			foreach (object connection in allConnectionsToAllHosts)
				processConnectionEventHandler(connection, parameter);

		}

		#endregion


	}
}
