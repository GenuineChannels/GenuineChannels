/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.Threading;

using Belikov.Common.ThreadProcessing;

namespace Belikov.GenuineChannels.BufferPooling
{
	/// <summary>
	/// Manages a pool of buffers.
	/// </summary>
	internal class BufferPool
	{
		/// <summary>
		/// Gets a buffer from the pool of buffers or creates a new one.
		/// Caller is not obliged to return the buffer back to the pool.
		/// </summary>
		/// <returns>The obtained buffer.</returns>
		public static byte[] ObtainBuffer()
		{
			lock(_lock)
			{
				if (_stack.Count > 0)
					return (byte[]) _stack.Pop();

				return new byte[GENERAL_BUFFER_SIZE];
			}
		}

		/// <summary>
		/// Returns the buffer back to Buffer Pool for the following reusing.
		/// </summary>
		/// <param name="buf">The buffer.</param>
		public static void RecycleBuffer(byte[] buf)
		{
			lock(_lock)
			{
				if (_stack.Count < _maxPooledBuffers && buf.Length == GENERAL_BUFFER_SIZE)
					_stack.Push(buf);
			}
		}

		/// <summary>
		/// Size of the buffers kept in the pool.
		/// All Buffers have exactly this size.
		/// </summary>
		public const int GENERAL_BUFFER_SIZE = 3000;

		/// <summary>
		/// The maximum number of buffers kept in the pool.
		/// </summary>
		private const int _maxPooledBuffers = 100;

		private static object _lock = new object();
		private static Stack _stack = new Stack(100);
	}
}
