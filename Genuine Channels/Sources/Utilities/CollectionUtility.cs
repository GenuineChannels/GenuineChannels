/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.Text;
using System.Reflection;

using Belikov.GenuineChannels.Parameters;

namespace Belikov.GenuineChannels.Utilities
{
	/// <summary>
	/// Provides a set of methods operating on collections.
	/// </summary>
	public class CollectionUtility
	{
		/// <summary>
		/// To prevent creating instances of this class.
		/// </summary>
		private CollectionUtility()
		{
		}

		/// <summary>
		/// Thread-safe way to obtain an element from an ArrayList collection kept in the hashtable
		/// under the specified key.
		/// </summary>
		/// <param name="hashtable">The hashtable.</param>
		/// <param name="key">The key.</param>
		/// <returns>The obtained element.</returns>
		public static object ObtainObjectFromArrayListByKey(Hashtable hashtable, object key)
		{
			lock (hashtable.SyncRoot)
			{
				ArrayList arrayList = hashtable[key] as ArrayList;
				if (arrayList == null)
					return null;

				lock (arrayList)
				{
					if (arrayList.Count <= 0)
						return null;

					object result = arrayList[arrayList.Count - 1];
					arrayList.RemoveAt(arrayList.Count - 1);
					return result;
				}
			}
		}

		/// <summary>
		/// Thread-safe way to insert an object to an ArrayList collection kept in the hashtable
		/// under the specified key.
		/// </summary>
		/// <param name="hashtable">The hashtable.</param>
		/// <param name="key">The key.</param>
		/// <param name="obj">The object being inserted.</param>
		public static void InsertObjectToArrayListByKey(Hashtable hashtable, object key, object obj)
		{
			lock (hashtable.SyncRoot)
			{
				ArrayList connections = hashtable[key] as ArrayList;
				if (connections == null)
					hashtable[key] = connections = new ArrayList();
				lock (connections)
					connections.Add(obj);
			}
		}

//		/// <summary>
//		/// Builds up a string containing all parameters from the specified parameter collection.
//		/// </summary>
//		/// <param name="iParameterProvider">The collection containing parameters.</param>
//		/// <returns>A string containing all parameters in the specified parameter collection.</returns>
//		public static string GetValuesOfAllParameters(IParameterProvider iParameterProvider)
//		{
//			StringBuilder builder = new StringBuilder();
//			Type enumType = typeof(GenuineParameter);
//
//			foreach (FieldInfo fieldInfo in enumType.GetFields())
//				if (fieldInfo.FieldType == enumType)
//				{
//					GenuineParameter theCurrentParameter = (GenuineParameter) fieldInfo.GetValue(null);
//					builder.AppendFormat("{0} = {1}\r\n", Enum.Format(enumType, theCurrentParameter, "g"), iParameterProvider[theCurrentParameter] == null ? "<null>" : iParameterProvider[theCurrentParameter].ToString());
//				}
//
//			return builder.ToString();
//		}
	}
}
