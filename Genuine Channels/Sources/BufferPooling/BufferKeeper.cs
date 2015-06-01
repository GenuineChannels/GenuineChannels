/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

namespace Belikov.GenuineChannels.BufferPooling
{
	/// <summary>
	/// Obtains a buffer from Buffer Pool and returns the obtained buffer back to the pool 
	/// when the Dispose method is called.
	/// </summary>
	internal struct BufferKeeper : IDisposable
	{
		/// <summary>
		/// Obtains a buffer from Buffer Pool.
		/// </summary>
		/// <param name="ignored">Ignored.</param>
		public BufferKeeper(int ignored)
		{
			this.Buffer = BufferPool.ObtainBuffer();
		}

		/// <summary>
		/// The obtained buffer.
		/// </summary>
		public byte[] Buffer;

		/// <summary>
		/// Disposes all acquired resources.
		/// </summary>
		public void Dispose()
		{
			BufferPool.RecycleBuffer(this.Buffer);
		}
	}
}
