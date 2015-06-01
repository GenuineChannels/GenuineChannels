/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;

using Belikov.Common.ThreadProcessing;

using Belikov.GenuineChannels.Connection;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.Logbook;
using Belikov.GenuineChannels.Messaging;
using Belikov.GenuineChannels.Parameters;
using Belikov.GenuineChannels.Receiving;
using Belikov.GenuineChannels.Security;
using Belikov.GenuineChannels.TransportContext;

namespace Belikov.GenuineChannels.Connection
{
	/// <summary>
	/// Represents a connection providing sending-receiving services via appropriate 
	/// Transport.
	/// </summary>
	internal abstract class PhysicalConnection
	{
		/// <summary>
		/// Constructs an instance of the PhysicalConnection class.
		/// </summary>
		public PhysicalConnection()
		{
#if DEBUG
			this._connectionNumber = Interlocked.Increment(ref _dbg_ConnectionCounter);
#endif
		}

		/// <summary>
		/// Connection-level Security Session.
		/// </summary>
		public SecuritySession ConnectionLevelSecurity;

		/// <summary>
		/// To guarantee atomic access to local members.
		/// </summary>
		protected object _accessToLocalMembers = new object();

		#region -- Debug information ---------------------------------------------------------------

#if DEBUG
		private static int _dbg_ConnectionCounter = 0;
		private int _connectionNumber = 0;
		public string TypeOfSocket;

		/// <summary>
		/// Returns a String that represents the current Object.
		/// </summary>
		/// <returns>A String that represents the current Object.</returns>
		public override string ToString()
		{
			return string.Format("(No: {0}. Type: {1}.)", this._connectionNumber, 
				this.TypeOfSocket == null ? "<not specified>" : this.TypeOfSocket);
		}

#endif

		#endregion

		#region -- Locking -------------------------------------------------------------------------

		/// <summary>
		/// This lock must be obtain during all operations that may change the state of the connection.
		/// </summary>
		public object PhysicalConnectionStateLock = new object();

		/// <summary>
		/// The state of the connection.
		/// True value of this member indicates that the connection is ready for the action.
		/// </summary>
		public bool ConnectionAvailable = false;

		/// <summary>
		/// Acquires access to the connection.
		/// </summary>
		/// <returns>True if access is acquired.</returns>
		public bool AcquireIfAvailable()
		{
			lock (this.PhysicalConnectionStateLock)
			{
				if (! this.ConnectionAvailable)
					return false;

				this.ConnectionAvailable = false;
				return true;
			}
		}

		/// <summary>
		/// Releases the connection.
		/// </summary>
		public void MarkAsAvailable()
		{
			lock (this.PhysicalConnectionStateLock)
			{
				this.ConnectionAvailable = true;
			}
		}
		
		#endregion

		#region -- Status of reestablishing --------------------------------------------------------

		/// <summary>
		/// Gets an indication whether the physical connection is being reestablished.
		/// </summary>
		public bool Reestablish_IsBeingReestablished
		{
			get
			{
				lock (this._accessToLocalMembers)
					return this._reestablish_IsBeingReestablished;
			}
		}
		private bool _reestablish_IsBeingReestablished = false;

		/// <summary>
		/// Answers true if a lock for connection reestablishing has been obtained.
		/// </summary>
		/// <returns>True if a lock for connection reestablishing has been obtained.</returns>
		public bool Reestablish_ObtainStatus()
		{
			lock (this._accessToLocalMembers)
			{
				if (_reestablish_IsBeingReestablished)
					return false;

				_reestablish_IsBeingReestablished = true;
				return true;
			}
		}

		/// <summary>
		/// Resets the status of reestablishing.
		/// </summary>
		public void Reestablish_ResetStatus()
		{
			lock (this._accessToLocalMembers)
			{
				if (! _reestablish_IsBeingReestablished)
				{
					BinaryLogWriter binaryLogWriter = GenuineLoggingServices.BinaryLogWriter;

					if (binaryLogWriter != null)
					{
						binaryLogWriter.WriteImplementationWarningEvent("PhysicalConnection.Reestablish_ResetStatus",
							LogMessageType.Error, GenuineExceptions.Get_Debugging_GeneralWarning("The connection is not being reestablished!"), 
							GenuineUtility.CurrentThreadId, Thread.CurrentThread.Name,
							"The connection is not being reestablished!");
					}
				}

				_reestablish_IsBeingReestablished = false;
			}
		}

		#endregion

		#region -- Disposing -----------------------------------------------------------------------

		/// <summary>
		/// Indicates whether this instance was disposed.
		/// </summary>
		internal bool IsDisposed
		{
			get
			{
				using (new ReaderAutoLocker(this.DisposeLock))
					return this.__disposed;
			}
			set
			{
				using (new WriterAutoLocker(this.DisposeLock))
					this.__disposed = value;
			}
		}
		private bool __disposed = false;

		/// <summary>
		/// The reason of the disposing.
		/// </summary>
		internal Exception _disposeReason = null;
		
		/// <summary>
		/// Dispose lock.
		/// </summary>
		internal ReaderWriterLock DisposeLock = new ReaderWriterLock();

		/// <summary>
		/// Releases all resources.
		/// </summary>
		/// <param name="reason">The reason of disposing.</param>
		public void Dispose(Exception reason)
		{
			if (this.IsDisposed)
				return ;

			if (reason == null)
				reason = GenuineExceptions.Get_Processing_TransportConnectionFailed();

			// stop the processing
			using (new WriterAutoLocker(this.DisposeLock))
			{
				if (this.IsDisposed)
					return ;

				this.IsDisposed = true;
				this._disposeReason = reason;
			}

			InternalDispose(reason);
		}

		/// <summary>
		/// Releases resources.
		/// </summary>
		/// <param name="reason">The reason of disposing.</param>
		public abstract void InternalDispose(Exception reason);

		#endregion

	}
}
