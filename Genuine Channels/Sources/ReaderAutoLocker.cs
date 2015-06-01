/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Threading;

namespace Belikov.Common.ThreadProcessing
{
	/// <summary>
	/// ReaderAutoLocker tries to obtain read access in the constructor within 30 secs.
	/// If it can not obtain it, ApplicationException is thrown.
	/// The resource is automatically released when dispose method is called.
	/// </summary>
	public struct ReaderAutoLocker : IDisposable
	{
		/// <summary>
		/// Obtain read access for the given object.
		/// Throws ApplicationException if access has not been acquired within 30 secs.
		/// </summary>
		/// <param name="locker">Locker to acquire rights.</param>
		public ReaderAutoLocker(ReaderWriterLock locker)
		{
			this.wasFreed = false;
			this.locker = locker;
			this.wasHeld = true;

			if (this.locker.IsReaderLockHeld || this.locker.IsWriterLockHeld)
			{
				this.wasHeld = false;
				return ;
			}

			locker.AcquireReaderLock(30000);
			if (! locker.IsReaderLockHeld)
				throw new ApplicationException("Can't acquire reader rights. CPU is overloaded or deadlock.");
		}

		private bool wasFreed;
		private bool wasHeld;
		ReaderWriterLock locker;

		/// <summary>
		/// Disposes all acquired resources.
		/// </summary>
		public void Dispose()
		{
			if (wasFreed)
				return ;

			if (this.wasHeld)
				locker.ReleaseReaderLock();
			wasFreed = true;
		}
	}
}
