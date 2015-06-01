/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Threading;

namespace Belikov.GenuineChannels.Utilities
{
	/// <summary>
	/// ThreadDataSlotKeeper initializes thread slot with the specified values and
	/// release a value on disposing.
	/// Does not restore previous slot value, always delete slot value.
	/// </summary>
	public struct ThreadDataSlotKeeper : IDisposable
	{
		/// <summary>
		/// Initializes the thread slot with the specified value.
		/// </summary>
		/// <param name="slotName">The name of the slot.</param>
		/// <param name="val">The value to be stored in the slot.</param>
		public ThreadDataSlotKeeper(string slotName, object val)
		{
			this._localDataStoreSlot = Thread.GetNamedDataSlot(slotName);
			Thread.SetData(this._localDataStoreSlot, val);
		}

		private LocalDataStoreSlot _localDataStoreSlot;

		/// <summary>
		/// Releases the set object.
		/// </summary>
		public void Dispose()
		{
			Thread.SetData(this._localDataStoreSlot, null);
		}
	}
}
