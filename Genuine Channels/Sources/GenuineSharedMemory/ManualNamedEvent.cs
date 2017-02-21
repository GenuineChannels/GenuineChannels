/* Genuine Channels product.
 *
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 *
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

using Belikov.GenuineChannels.Parameters;

namespace Belikov.GenuineChannels.GenuineSharedMemory
{
	/// <summary>
	/// NamedEvent wraps Windows API to represent a named event, which can be used for interprocess synchronization.
	/// </summary>
	public class NamedEvent
	{
		/// <summary>
		/// Initialization only via static constructors.
		/// </summary>
		/// <param name="handle">Event handle.</param>
		private NamedEvent(IntPtr handle)
		{
			this._manualResetEvent = new ManualResetEvent(false);
			this._manualResetEvent.SafeWaitHandle = new SafeWaitHandle(handle, true);
		}

		/// <summary>
		/// Creates named event object.
		/// </summary>
		/// <param name="name">The name of the event object.</param>
		/// <param name="initialState">If this parameter is TRUE, the initial state of the event object is signaled; otherwise, it is nonsignaled.</param>
		/// <param name="manualReset">If this parameter is FALSE, the function creates an auto-reset event object, and system automatically resets the state to nonsignaled after a single waiting thread has been released.</param>
		/// <returns>Created event object.</returns>
		public static NamedEvent CreateNamedEvent(string name, bool initialState, bool manualReset)
		{
			if (WindowsAPI.FailureReason != null)
				throw OperationException.WrapException(WindowsAPI.FailureReason);

			IntPtr handler = WindowsAPI.CreateEvent(WindowsAPI.AttributesWithNullDACL,
				manualReset ? 1 : 0,
				(initialState ? 1 : 0), name);
			if (handler == IntPtr.Zero)
				throw GenuineExceptions.Get_Windows_CanNotCreateOrOpenNamedEvent(Marshal.GetLastWin32Error());
			return new NamedEvent(handler);
		}

		/// <summary>
		/// Opens an existent event object with the specified name.
		/// </summary>
		/// <param name="name">The name of the global Event.</param>
		/// <returns>The opened event object.</returns>
		public static NamedEvent OpenNamedEvent(string name)
		{
			IntPtr handler = WindowsAPI.OpenEvent(WindowsAPI.EVENT_ALL_ACCESS, 0, name);
			if (handler == IntPtr.Zero)
				throw GenuineExceptions.Get_Windows_CanNotCreateOrOpenNamedEvent(Marshal.GetLastWin32Error());
			return new NamedEvent(handler);
		}

		/// <summary>
		/// Gets ManualResetEvent containing current object's handler.
		/// </summary>
		public ManualResetEvent ManualResetEvent
		{
			get
			{
				return this._manualResetEvent;
			}
		}
		private ManualResetEvent _manualResetEvent;
	}
}
