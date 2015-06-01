/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Runtime.InteropServices;

namespace Belikov.GenuineChannels
{
	/// <summary>
	/// GCHandleKeeper holds on GCHandle structure and implements the IDisposable interface
	/// for releasing resources obtained by GCHandle.
	/// </summary>
	public class GCHandleKeeper : IDisposable
	{
		/// <summary>
		/// Constructs an instance of the GCHandleKeeper class.
		/// </summary>
		/// <param name="val"></param>
		/// <param name="type"></param>
		public GCHandleKeeper(object val, GCHandleType type)
		{
			this.GCHandle = GCHandle.Alloc(val, type);
		}

		/// <summary>
		/// GCHandle instance.
		/// </summary>
		public readonly GCHandle GCHandle;

		#region IDisposable Members

#if TRIAL
#else
		private bool wasFreed = false;
#endif

		/// <summary>
		/// Releases the pin.
		/// </summary>
		public void Dispose()
		{
#if TRIAL
#else
			if (! wasFreed)
				this.GCHandle.Free();

			wasFreed = true;
#endif
		}

		#endregion
	}
}
