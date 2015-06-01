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
	/// WriterAutoLocker tries to obtain write access in the constructor within 30 secs.
	/// If it can not obtain it, ApplicationException is thrown.
	/// The resource is automatically released when the Dispose method is called.
	/// </summary>
	public struct WriterAutoLocker : IDisposable
	{
		/// <summary>
		/// Acquires write access or throws ApplicationException.
		/// </summary>
		/// <param name="locker">ReaderWriterLock being locked.</param>
		public WriterAutoLocker(ReaderWriterLock locker)
		{
			this.wasFreed = false;
			this.locker = locker;
			this.wasHeld = true;
			this.upgraded = false;
			this.lockCookie = new LockCookie();

			if (this.locker.IsWriterLockHeld)
			{
				this.wasHeld = false;
				return ;
			}

			if (this.locker.IsReaderLockHeld)
			{
				this.upgraded = true;
				lockCookie = locker.UpgradeToWriterLock(30000);
			}
			else
				locker.AcquireWriterLock(30000);

			if (! locker.IsWriterLockHeld)
				throw new ApplicationException("Can't acquire writer rights. Server overloaded or deadlock.");
		}

		private bool wasFreed;
		private bool wasHeld;
		private bool upgraded;
		private LockCookie lockCookie;
		ReaderWriterLock locker;

		/// <summary>
		/// Disposes all acquired resources.
		/// </summary>
		public void Dispose()
		{
			if (wasFreed)
				return ;

			if (wasHeld)
			{
				if (upgraded)
					locker.DowngradeFromWriterLock(ref lockCookie);
				else
					locker.ReleaseWriterLock();
			}
			wasFreed = true;
		}

	}
}
