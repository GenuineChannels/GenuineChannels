/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Belikov.GenuineChannels.GenuineSharedMemory
{
	/// <summary>
	/// Contains declarations of windows API stuff related to shared memory functionality.
	/// </summary>
	public class WindowsAPI
	{
		[DllImport("kernel32", SetLastError=true, CharSet=CharSet.Auto)]
		static internal extern IntPtr CreateFileMapping(IntPtr hFile, _SECURITY_ATTRIBUTES lpSecurityAttributes, uint flProtect, uint dwMaximumSizeHigh, uint dwMaximumSizeLow, string lpName);

		[DllImport("kernel32", SetLastError=true, CharSet=CharSet.Auto)]
		static internal extern IntPtr OpenFileMapping(uint dwDesiredAccess, int bInheritHandle, string lpName);

		[DllImport("kernel32", SetLastError=true, CharSet=CharSet.Auto)]
		static internal extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow, uint dwNumberOfBytesToMap);

		[DllImport("kernel32", SetLastError=true, CharSet=CharSet.Auto)]
		static internal extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

		[DllImport("kernel32", SetLastError=true, CharSet=CharSet.Auto)]
		static internal extern bool CloseHandle(IntPtr hObject);

		[DllImport("kernel32", SetLastError=true, CharSet=CharSet.Auto)]
		static internal extern int FormatMessage(uint dwFlags, IntPtr lpSource, uint dwMessageId, uint dwLanguageId, IntPtr lpBuffer, uint nSize, IntPtr pArguments);

		[DllImport("kernel32", SetLastError=true, CharSet=CharSet.Auto)]
		static internal extern IntPtr LocalFree(IntPtr hMem);

		[DllImport("kernel32", SetLastError=true, CharSet=CharSet.Auto)]
		static internal extern IntPtr CreateEvent(_SECURITY_ATTRIBUTES lpEventAttributes, int bManualReset, int bInitialState, string lpName);

		[DllImport("kernel32", SetLastError=true, CharSet=CharSet.Auto)]
		static internal extern bool SetEvent(IntPtr hEvent);

		[DllImport("kernel32", SetLastError=true, CharSet=CharSet.Auto)]
		static internal extern bool ResetEvent(IntPtr hEvent);

		[DllImport("kernel32", SetLastError=true, CharSet=CharSet.Auto)]
		static internal extern uint WaitForSingleObject(IntPtr hHandle, int dwMilliseconds);

		[DllImport("kernel32", SetLastError=true, CharSet=CharSet.Auto)]
		static internal extern IntPtr OpenEvent(int dwDesiredAccess, int bInheritHandle, string lpName);

		/// <summary>
		/// Page read/write constant.
		/// </summary>
		internal const int PAGE_READWRITE = 0x04;

		/// <summary>
		/// See Win API docs.
		/// </summary>
		internal const uint SECTION_QUERY = 0x0001;

		/// <summary>
		/// See Win API docs.
		/// </summary>
		internal const uint SECTION_MAP_WRITE = 0x0002;

		/// <summary>
		/// See Win API docs.
		/// </summary>
		internal const uint SECTION_MAP_READ = 0x0004;

		/// <summary>
		/// See Win API docs.
		/// </summary>
		internal const uint SECTION_MAP_EXECUTE = 0x0008;

		/// <summary>
		/// See Win API docs.
		/// </summary>
		internal const uint SECTION_EXTEND_SIZE = 0x0010;

		/// <summary>
		/// See Win API docs.
		/// </summary>
		internal const uint FILE_MAP_ALL_ACCESS = SECTION_QUERY | SECTION_MAP_WRITE | SECTION_MAP_READ | SECTION_MAP_EXECUTE | SECTION_EXTEND_SIZE;

		/// <summary>
		/// See Win API docs.
		/// </summary>
		internal const uint FORMAT_MESSAGE_ALLOCATE_BUFFER = 0x00000100;

		/// <summary>
		/// See Win API docs.
		/// </summary>
		internal const uint FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200;

		/// <summary>
		/// See Win API docs.
		/// </summary>
		internal const uint FORMAT_MESSAGE_FROM_STRING = 0x00000400;

		/// <summary>
		/// See Win API docs.
		/// </summary>
		internal const uint FORMAT_MESSAGE_FROM_HMODULE = 0x00000800;

		/// <summary>
		/// See Win API docs.
		/// </summary>
		internal const uint FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000;

		/// <summary>
		/// See Win API docs.
		/// </summary>
		internal const uint FORMAT_MESSAGE_ARGUMENT_ARRAY = 0x00002000;

		/// <summary>
		/// See Win API docs.
		/// </summary>
		internal const uint FORMAT_MESSAGE_MAX_WIDTH_MASK = 0x000000FF;

		/// <summary>
		/// See Win API docs.
		/// </summary>
		internal const uint WAIT_TIMEOUT = 258;

		/// <summary>
		/// See Win API docs.
		/// </summary>
		internal const int INFINITE = -1;

		/// <summary>
		/// See Win API docs.
		/// </summary>
		internal const int SYNCHRONIZE = 0x00100000;

		/// <summary>
		/// See Win API docs.
		/// </summary>
		internal const int STANDARD_RIGHTS_REQUIRED = 0x000F0000;

		/// <summary>
		/// See Win API docs.
		/// </summary>
		internal const int EVENT_ALL_ACCESS = STANDARD_RIGHTS_REQUIRED | SYNCHRONIZE | 0x3;

		/// <summary>
		/// Security attributes.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		internal class _SECURITY_ATTRIBUTES
		{
			public int nLength;
			public IntPtr lpSecurityDescriptor;
			public int bInheritHandle;
		}

		[DllImport("Advapi32", SetLastError=true, CharSet=CharSet.Auto)]
		static private extern bool InitializeSecurityDescriptor(IntPtr pSecurityDescriptor, int dwRevision);

		[DllImport("Advapi32", SetLastError=true, CharSet=CharSet.Auto)]
		static private extern bool SetSecurityDescriptorDacl(IntPtr pSecurityDescriptor, bool bDaclPresent, IntPtr pDacl, bool bDaclDefaulted);

		[DllImport("Kernel32", SetLastError=true, CharSet=CharSet.Auto)]
		static private extern IntPtr CreateMutex(_SECURITY_ATTRIBUTES lpMutexAttributes, bool bInitialOwner, string lpName);

		[DllImport("Kernel32", SetLastError=true, CharSet=CharSet.Auto)]
		static private extern IntPtr OpenMutex(int dwDesiredAccess, bool bInheritHandle, string lpName);

		private const int SECURITY_DESCRIPTOR_MIN_LENGTH = 20;
		private const int SECURITY_DESCRIPTOR_REVISION = 1;
		private const int MUTEX_ALL_ACCESS = 0x1F0001;
		private const int MUTEX_MODIFY_STATE = 0x1;
		private const int ERROR_ALREADY_EXISTS = 183;

		static WindowsAPI()
		{
			NullAttribute = Marshal.AllocHGlobal(SECURITY_DESCRIPTOR_MIN_LENGTH);

			if (NullAttribute == IntPtr.Zero)
				return ;

			try
			{
				if (! InitializeSecurityDescriptor(NullAttribute, SECURITY_DESCRIPTOR_REVISION))
					throw GenuineExceptions.Get_Windows_SharedMemoryError(Marshal.GetLastWin32Error());

				if (! SetSecurityDescriptorDacl(NullAttribute, true, IntPtr.Zero, false))
					throw GenuineExceptions.Get_Windows_SharedMemoryError(Marshal.GetLastWin32Error());

				AttributesWithNullDACL = new _SECURITY_ATTRIBUTES();
				AttributesWithNullDACL.nLength = 12;
				AttributesWithNullDACL.lpSecurityDescriptor = NullAttribute;
				AttributesWithNullDACL.bInheritHandle = 1;
			}
			catch(Exception ex)
			{
				FailureReason = ex;
				try
				{
					Marshal.FreeHGlobal(NullAttribute);
				}
				catch(Exception)
				{
				}

				NullAttribute = IntPtr.Zero;
			}
		}

		static private IntPtr NullAttribute = IntPtr.Zero;
		static internal _SECURITY_ATTRIBUTES AttributesWithNullDACL;
		static internal Exception FailureReason;

//		/// <summary>
//		/// Creates a new mutex with NULL DACL.
//		/// </summary>
//		/// <param name="mutex">The mutex.</param>
//		/// <param name="name">The name of the mutex.</param>
//		static internal void UpgrageMutexSecurity(Mutex mutex, string name)
//		{
//			CloseHandle(mutex.Handle);
//			mutex.Handle = CreateMutex(AttributesWithNullDACL, false, name);
//		}

		/// <summary>
		/// Opens an existent mutex.
		/// </summary>
		/// <param name="name">The name of the mutex.</param>
		/// <returns>The opened mutex.</returns>
		static internal Mutex OpenMutex(string name)
		{
			IntPtr result = OpenMutex(MUTEX_ALL_ACCESS, false, name);
			if (result == IntPtr.Zero)
				throw GenuineExceptions.Get_Windows_SharedMemoryError(Marshal.GetLastWin32Error());

			Mutex mutex = new Mutex();
			mutex.Handle = result;

			return mutex;
		}

		static internal Mutex CreateMutex(string name)
		{
			IntPtr result = CreateMutex(AttributesWithNullDACL, false, name);
			if (result == IntPtr.Zero)
				throw GenuineExceptions.Get_Windows_SharedMemoryError(Marshal.GetLastWin32Error());

			if (Marshal.GetLastWin32Error() == ERROR_ALREADY_EXISTS)
				throw GenuineExceptions.Get_Windows_SharedMemoryError(Marshal.GetLastWin32Error());

			Mutex mutex = new Mutex();
			mutex.Handle = result;

			return mutex;
		}
	}
}
