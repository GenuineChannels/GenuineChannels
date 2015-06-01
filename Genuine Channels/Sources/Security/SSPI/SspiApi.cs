/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Runtime.InteropServices;
using System.Security.Principal;

using Belikov.GenuineChannels.Logbook;

namespace Belikov.GenuineChannels.Security.SSPI
{
	/// <summary>
	/// Utility class that contains SSPI API (Secur32.lib).
	/// </summary>
	public class SspiApi
	{
		/// <summary>
		/// To prevent creating an instance of this class.
		/// </summary>
		private SspiApi()
		{
		}

		static SspiApi()
		{
			_isNT = Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version.Major == 4;
		}

		private static readonly bool _isNT;

		[DllImport("kernel32", SetLastError=true, CharSet=CharSet.Auto)]
		static private extern bool CloseHandle(IntPtr hObject);

		static private uint DUPLICATE_SAME_ACCESS = 0x00000002;
		static private uint PROCESS_DUP_HANDLE = 0x0040;

		[DllImport("kernel32.dll")]
		static private extern bool DuplicateHandle(IntPtr sourceProcessHandle,
			IntPtr sourceHandle, IntPtr targetProcessHandle,
			out IntPtr targetHandle, uint desiredAccess, bool inheritHandle,
			uint dwOptions);

		[DllImport("kernel32.dll")]
		static private extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

		#region -- All except NT -------------------------------------------------------------------

		[DllImport("secur32", EntryPoint="GetUserNameEx", CharSet=CharSet.Auto, SetLastError=true)]
		static extern int GetUserNameEx__(int NameFormat, StringBuilder lpNameBuffer, ref int nSize);

		[DllImport("secur32", EntryPoint="AcquireCredentialsHandle", SetLastError=false, CharSet=CharSet.Auto)]
		static private extern int AcquireCredentialsHandleIdentity__(IntPtr pszPrincipal, string pszPackage, int fCredentialUse,
			IntPtr pvLogonId, ref SEC_WINNT_AUTH_IDENTITY pAuthData, IntPtr pGetKeyFn, IntPtr pvGetKeyArgument, 
			SecHandle phCredential, ref Int64 ptsExpiry);

		[DllImport("secur32", EntryPoint="AcquireCredentialsHandle", SetLastError=false, CharSet=CharSet.Auto)]
		static private extern int AcquireCredentialsHandleIdentityNull__(IntPtr pszPrincipal, string pszPackage, int fCredentialUse,
			IntPtr pvLogonId, IntPtr pAuthData, IntPtr pGetKeyFn, IntPtr pvGetKeyArgument, 
			SecHandle phCredential, ref Int64 ptsExpiry);

		[DllImport("secur32", EntryPoint="FreeCredentialsHandle", SetLastError=false)]
		static private extern int FreeCredentialsHandle__(SecHandle phContext);

		[DllImport("secur32", EntryPoint="InitializeSecurityContext", SetLastError=false, CharSet=CharSet.Auto)]
		static private extern int InitializeSecurityContext__(SecHandle phCredential,
			SecHandle phContext, string pszTargetName, int fContextReq, int Reserved1,
			int TargetDataRep, ref SecBufferDescNative pInput, int Reserved2, SecHandle phNewContext,
			ref SecBufferDescNative pOutput, ref int pfContextAttr, ref Int64 ptsExpiry);

#if TRIAL
#else

		[DllImport("secur32", EntryPoint="DeleteSecurityContext")]
		static private extern int DeleteSecurityContext__(SecHandle phContext);

		[DllImport("secur32", EntryPoint="FreeContextBuffer")]
		static private extern int FreeContextBuffer__(IntPtr pvContextBuffer);

#endif

		[DllImport("secur32", EntryPoint="AcceptSecurityContext")]
		static private extern int AcceptSecurityContext__(SecHandle phCredential,
			SecHandle phContext, ref SecBufferDescNative pInput, int fContextReq, int TargetDataRep,
			SecHandle phNewContext, ref SecBufferDescNative pOutput, ref int pfContextAttr,
			ref Int64 ptsExpiry);

		[DllImport("secur32", EntryPoint="QueryContextAttributes")]
		static private extern int QueryContextAttributes__(SecHandle phContext,
			int ulAttribute, ref SecPkgContext_Sizes pBuffer);

		[DllImport("secur32", EntryPoint="QuerySecurityContextToken")]
		private static extern int QuerySecurityContextToken__(SecHandle phContext, out IntPtr phToken);

		[DllImport("secur32", EntryPoint="ImpersonateSecurityContext")]
		static private extern int ImpersonateSecurityContext__(SecHandle phContext);

		[DllImport("secur32", EntryPoint="RevertSecurityContext")]
		static private extern int RevertSecurityContext__(SecHandle phContext);

		[DllImport("secur32", EntryPoint="MakeSignature")]
		static private extern int MakeSignature__(SecHandle phContext, int fQOP, 
			ref SecBufferDescNative pMessage, int MessageSeqNo);

		[DllImport("secur32", EntryPoint="VerifySignature")]
		static private extern int VerifySignature__(SecHandle phContext,
			ref SecBufferDescNative pMessage, int MessageSeqNo,
			ref int pfQOP);

		[DllImport("secur32", EntryPoint="EncryptMessage")]
		static private extern int EncryptMessage__(SecHandle phContext,
			int pfQOP, ref SecBufferDescNative pMessage, int MessageSeqNo);

		[DllImport("secur32", EntryPoint="DecryptMessage")]
		static private extern int DecryptMessage__(SecHandle phContext,
			ref SecBufferDescNative pMessage, int MessageSeqNo,
			ref int pfQOP);

		#endregion

		#region -- NT ------------------------------------------------------------------------------

		[DllImport("security", EntryPoint="GetUserNameEx", CharSet=CharSet.Unicode, SetLastError=true)]
		static extern int GetUserNameEx_NT(int NameFormat, StringBuilder lpNameBuffer, ref int nSize);

		[DllImport("security", EntryPoint="AcquireCredentialsHandle", SetLastError=false, CharSet=CharSet.Auto)]
		static private extern int AcquireCredentialsHandleIdentity_NT(IntPtr pszPrincipal, string pszPackage, int fCredentialUse,
			IntPtr pvLogonId, ref SEC_WINNT_AUTH_IDENTITY pAuthData, IntPtr pGetKeyFn, IntPtr pvGetKeyArgument, 
			SecHandle phCredential, ref Int64 ptsExpiry);

		[DllImport("security", EntryPoint="AcquireCredentialsHandle", SetLastError=false, CharSet=CharSet.Auto)]
		static private extern int AcquireCredentialsHandleIdentityNull_NT(IntPtr pszPrincipal, string pszPackage, int fCredentialUse,
			IntPtr pvLogonId, IntPtr pAuthData, IntPtr pGetKeyFn, IntPtr pvGetKeyArgument, 
			SecHandle phCredential, ref Int64 ptsExpiry);

		[DllImport("security", SetLastError=false, EntryPoint="FreeCredentialsHandle")]
		static private extern int FreeCredentialsHandle_NT(SecHandle phContext);

		[DllImport("security", SetLastError=false, CharSet=CharSet.Auto, EntryPoint="InitializeSecurityContext")]
		static private extern int InitializeSecurityContext_NT(SecHandle phCredential,
			SecHandle phContext, string pszTargetName, int fContextReq, int Reserved1,
			int TargetDataRep, ref SecBufferDescNative pInput, int Reserved2, SecHandle phNewContext,
			ref SecBufferDescNative pOutput, ref int pfContextAttr, ref Int64 ptsExpiry);

#if TRIAL
#else

		[DllImport("security", EntryPoint="DeleteSecurityContext")]
		static private extern int DeleteSecurityContext_NT(SecHandle phContext);

		[DllImport("security", EntryPoint="FreeContextBuffer")]
		static private extern int FreeContextBuffer_NT(IntPtr pvContextBuffer);

#endif


		[DllImport("security", SetLastError=false, CharSet=CharSet.Auto, EntryPoint="AcceptSecurityContext")]
		static private extern int AcceptSecurityContext_NT(SecHandle phCredential,
			SecHandle phContext, ref SecBufferDescNative pInput, int fContextReq, int TargetDataRep,
			SecHandle phNewContext, ref SecBufferDescNative pOutput, ref int pfContextAttr,
			ref Int64 ptsExpiry);

		[DllImport("security", EntryPoint="QueryContextAttributes")]
		static private extern int QueryContextAttributes_NT(SecHandle phContext,
			int ulAttribute, ref SecPkgContext_Sizes pBuffer);

		[DllImport("security", EntryPoint="QuerySecurityContextToken")]
		private static extern int QuerySecurityContextToken_NT(SecHandle phContext, out IntPtr phToken);

		[DllImport("security", EntryPoint="ImpersonateSecurityContext")]
		static private extern int ImpersonateSecurityContext_NT(SecHandle phContext);

		[DllImport("security", EntryPoint="RevertSecurityContext")]
		static private extern int RevertSecurityContext_NT(SecHandle phContext);

		[DllImport("security", EntryPoint="MakeSignature")]
		static private extern int MakeSignature_NT(SecHandle phContext, int fQOP, 
			ref SecBufferDescNative pMessage, int MessageSeqNo);

		[DllImport("security", EntryPoint="VerifySignature")]
		static private extern int VerifySignature_NT(SecHandle phContext,
			ref SecBufferDescNative pMessage, int MessageSeqNo,
			ref int pfQOP);

		// actually, NT doesn't have such a function
		[DllImport("security", EntryPoint="EncryptMessage")]
		static private extern int EncryptMessage_NT(SecHandle phContext,
			int pfQOP, ref SecBufferDescNative pMessage, int MessageSeqNo);

		// actually, NT doesn't have such a function
		[DllImport("security", EntryPoint="DecryptMessage")]
		static private extern int DecryptMessage_NT(SecHandle phContext,
			ref SecBufferDescNative pMessage, int MessageSeqNo,
			ref int pfQOP);

		#endregion

		#region -- Structures and constants --------------------------------------------------------

		/// <summary>
		/// Exposes SSPI SecHandle structure.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public class SecHandle
		{
			/// <summary>
			/// See SSPI API.
			/// </summary>
			public int dwLower;

			/// <summary>
			/// See SSPI API.
			/// </summary>
			public int dwUpper;
		}

		private const int SEC_WINNT_AUTH_IDENTITY_ANSI = 0x1;
		private const int SEC_WINNT_AUTH_IDENTITY_UNICODE = 0x2;

		/// <summary>
		/// See SSPI API.
		/// </summary>
		public const int SECPKG_CRED_INBOUND = 0x00000001;

		/// <summary>
		/// See SSPI API.
		/// </summary>
		public const int SECPKG_CRED_OUTBOUND = 0x00000002;

		/// <summary>
		/// See SSPI API.
		/// </summary>
		public const int SECPKG_CRED_BOTH = 0x00000003;

		private const int SEC_E_OK = 0;
		private const int SEC_I_CONTINUE_NEEDED = 0x00090312;
		private const int SEC_I_COMPLETE_NEEDED = 0x00090313;
		private const int SEC_I_COMPLETE_AND_CONTINUE = 0x00090314;
		private const int SEC_I_LOCAL_LOGON = 0x00090315;

		private const int ISC_REQ_REPLAY_DETECT = 0x00000004;
		private const int ISC_REQ_SEQUENCE_DETECT = 0x00000008;
		private const int ISC_REQ_CONNECTION = 0x00000800;
		private const int ISC_REQ_ALLOCATE_MEMORY = 0x00000100;
		private const int ISC_REQ_CONFIDENTIALITY = 0x00000010;
		private const int ISC_REQ_INTEGRITY = 0x00010000;
		private const int ISC_REQ_MUTUAL_AUTH = 0x00000002;
		private const int ISC_REQ_DELEGATE = 0x00000001;
		private const int ASC_REQ_REPLAY_DETECT = 0x00000004;
		private const int ASC_REQ_SEQUENCE_DETECT = 0x00000008;
		private const int ASC_REQ_CONNECTION = 0x00000800;
		private const int ASC_REQ_ALLOCATE_MEMORY = 0x00000100;
		private const int ASC_REQ_CONFIDENTIALITY = 0x00000010;
		private const int ASC_REQ_INTEGRITY = 0x00020000;
		private const int ASC_REQ_MUTUAL_AUTH = 0x00000002;
		private const int ASC_REQ_DELEGATE = 0x00000001;

		private const int SECURITY_NATIVE_DREP = 0x00000010;

		private const int SECBUFFER_EMPTY = 0;
		private const int SECBUFFER_DATA = 1;
		private const int SECBUFFER_TOKEN = 2;
		private const int SECBUFFER_PADDING = 9;
		private const int SECBUFFER_VERSION = 0;

		private const int SECPKG_ATTR_SIZES = 0;

		[StructLayout(LayoutKind.Sequential)]
		private struct SEC_WINNT_AUTH_IDENTITY
		{
			/// <summary>
			/// User name.
			/// </summary>
			public IntPtr User;

			/// <summary>
			/// Length of the User field.
			/// </summary>
			public int UserLength;

			/// <summary>
			/// Domain name.
			/// </summary>
			public IntPtr Domain;

			/// <summary>
			/// Length of the Domain field.
			/// </summary>
			public int DomainLength;

			/// <summary>
			/// Password.
			/// </summary>
			public IntPtr Password;

			/// <summary>
			/// Length of the Password field.
			/// </summary>
			public int PasswordLength;

			/// <summary>
			/// Text encoding.
			/// </summary>
			public int Flags;
		}

		[StructLayout(LayoutKind.Sequential)]
		private class SecBuffer
		{
			public int cbBuffer;
			public int BufferType;
			public IntPtr pvBuffer;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct SecBufferNative
		{
			public int cbBuffer;
			public int BufferType;
			public IntPtr pvBuffer;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct SecBufferDescNative
		{
			/// <summary>
			/// Version number.
			/// </summary>
			public int ulVersion;

			/// <summary>
			/// Number of buffers.
			/// </summary>
			public int cBuffers;

			/// <summary>
			/// Pointer to array of buffers.
			/// </summary>
			public IntPtr pBuffers;
		}

		/// <summary>
		/// Exposes SSPI SecPkgContext_Sizes structure.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct SecPkgContext_Sizes
		{
			/// <summary>
			/// See SSPI API.
			/// </summary>
			public int cbMaxToken;

			/// <summary>
			/// See SSPI API.
			/// </summary>
			public int cbMaxSignature;
			
			/// <summary>
			/// See SSPI API.
			/// </summary>
			public int cbBlockSize;
			
			/// <summary>
			/// See SSPI API.
			/// </summary>
			public int cbSecurityTrailer;
		}

		enum EXTENDED_NAME_FORMAT
		{
			NameUnknown = 0,
			NameFullyQualifiedDN = 1,
			NameSamCompatible = 2,
			NameDisplay = 3,
			NameUniqueId = 6,
			NameCanonical = 7,
			NameUserPrincipal = 8,
			NameCanonicalEx = 9,
			NameServicePrincipal = 10,
			NameDnsDomain = 12,
		}

		#endregion

		#region -- Implementation ------------------------------------------------------------------

		/// <summary>
		/// Calls GetUserNameEx Security API function.
		/// Throws an exception if the call fails.
		/// </summary>
		/// <returns>The name of the user or other security principal associated with the calling thread.</returns>
		public static string GetUserNameEx()
		{
			// 256 is the maximum
			StringBuilder userName = new StringBuilder(256);
			int nSize = userName.Capacity;
			int result;
			if (_isNT)
				result = SspiApi.GetUserNameEx_NT((int) EXTENDED_NAME_FORMAT.NameSamCompatible, userName, ref nSize);
			else
				result = SspiApi.GetUserNameEx__((int) EXTENDED_NAME_FORMAT.NameSamCompatible, userName, ref nSize);

			if (result == 0)
				throw GenuineExceptions.Get_Windows_SspiError(Marshal.GetLastWin32Error());
			return userName.ToString();
		}

		private static IntPtr sspiAuthIdentityPointer = IntPtr.Zero;

		/// <summary>
		/// Calls AcquireCredentialsHandle SSPI API function.
		/// Throws an exception if the call fails.
		/// </summary>
		/// <param name="authIdentity">User credential.</param>
		/// <param name="packageName">Package name.</param>
		/// <param name="fCredentialUse">Exchange direction.</param>
		/// <param name="credHandle">Where to store credential handle.</param>
		/// <param name="ptsExpiry">Lifetime span.</param>
		public static void AcquireCredentialsHandle(NetworkCredential authIdentity, 
			string packageName, int fCredentialUse, SspiApi.SecHandle credHandle,
			ref Int64 ptsExpiry)
		{
			SspiApi.SEC_WINNT_AUTH_IDENTITY sspiAuthIdentity = new SspiApi.SEC_WINNT_AUTH_IDENTITY();

#if TRIAL
#else
			try
			{
#endif
				// prepare identity structure
				if (authIdentity != null)
				{
					sspiAuthIdentity.Domain = Marshal.StringToCoTaskMemAuto(authIdentity.Domain);
					sspiAuthIdentity.User = Marshal.StringToCoTaskMemAuto(authIdentity.UserName);
					sspiAuthIdentity.Password = Marshal.StringToCoTaskMemAuto(authIdentity.Password);

					sspiAuthIdentity.DomainLength = authIdentity.Domain.Length;
					sspiAuthIdentity.UserLength = authIdentity.UserName.Length;
					sspiAuthIdentity.PasswordLength = authIdentity.Password.Length;

					sspiAuthIdentity.Flags = Marshal.SystemDefaultCharSize == 2 ? SspiApi.SEC_WINNT_AUTH_IDENTITY_UNICODE : SspiApi.SEC_WINNT_AUTH_IDENTITY_ANSI;
				}

				int result = 0;

				// make the call
				if (authIdentity != null)
				{
					if (_isNT)
						result = SspiApi.AcquireCredentialsHandleIdentity_NT(IntPtr.Zero, 
							packageName, fCredentialUse, IntPtr.Zero, ref sspiAuthIdentity,
							IntPtr.Zero, IntPtr.Zero, credHandle, ref ptsExpiry);
					else
						result = SspiApi.AcquireCredentialsHandleIdentity__(IntPtr.Zero, 
							packageName, fCredentialUse, IntPtr.Zero, ref sspiAuthIdentity,
							IntPtr.Zero, IntPtr.Zero, credHandle, ref ptsExpiry);
				}
				else
				{
					if (_isNT)
						result = SspiApi.AcquireCredentialsHandleIdentityNull_NT(IntPtr.Zero, 
							packageName, fCredentialUse, IntPtr.Zero, IntPtr.Zero,
							IntPtr.Zero, IntPtr.Zero, credHandle, ref ptsExpiry);
					else
						result = SspiApi.AcquireCredentialsHandleIdentityNull__(IntPtr.Zero, 
							packageName, fCredentialUse, IntPtr.Zero, IntPtr.Zero,
							IntPtr.Zero, IntPtr.Zero, credHandle, ref ptsExpiry);
				}

				if (result != SspiApi.SEC_E_OK)
					throw GenuineExceptions.Get_Windows_SspiError(result);
#if TRIAL
#else
			}
			finally
			{
				// release allocated memory
				if (sspiAuthIdentity.Domain != IntPtr.Zero)
					Marshal.FreeCoTaskMem(sspiAuthIdentity.Domain);
				if (sspiAuthIdentity.User != IntPtr.Zero)
					Marshal.FreeCoTaskMem(sspiAuthIdentity.User);
				if (sspiAuthIdentity.Password != IntPtr.Zero)
					Marshal.FreeCoTaskMem(sspiAuthIdentity.Password);
			}
#endif
		}


		/// <summary>
		/// Calls InitializeSecurityContext SSPI API function.
		/// Throws an exception if the call fails.
		/// receivedData parameter must be null for the first call.
		/// </summary>
		/// <param name="credHandle">Credential handle.</param>
		/// <param name="inputPhContext">Source security context.</param>
		/// <param name="outputPhContext">Created security context.</param>
		/// <param name="targetName">Target name.</param>
		/// <param name="requiredFeatures">Requested features.</param>
		/// <param name="ptsExpiry">Expiry.</param>
		/// <param name="receivedData">Input data.</param>
		/// <param name="outputStream">Output data.</param>
		/// <returns>True if security context has been succesfully built up.</returns>
		public static bool InitializeSecurityContext(SspiApi.SecHandle credHandle, 
			SspiApi.SecHandle inputPhContext, SspiApi.SecHandle outputPhContext, string targetName, 
			SspiFeatureFlags requiredFeatures, ref Int64 ptsExpiry,
			Stream receivedData, Stream outputStream)
		{
			bool finalResult = false;

			// get the size of the received chunk
			int inputContentSize = 0;
			if (receivedData != null)
			{
				BinaryReader binaryReader = new BinaryReader(receivedData);
				inputContentSize = binaryReader.ReadInt32();
			}

			SspiApi.SecBufferDescNative secInputDesc = new SspiApi.SecBufferDescNative();
			SspiApi.SecBuffer secInputBuf = new SspiApi.SecBuffer();
			SspiApi.SecBufferDescNative secOutputDesc = new SspiApi.SecBufferDescNative();
			SspiApi.SecBuffer secOutputBuf = new SspiApi.SecBuffer();
			IntPtr inputDescBuffer = IntPtr.Zero;
			IntPtr outputDescBuffer = IntPtr.Zero;

#if TRIAL
#else
			try
			{
#endif
				inputDescBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SspiApi.SecBufferDescNative)));
				outputDescBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SspiApi.SecBufferDescNative)));

				// setup input buffer
				secInputDesc.ulVersion = SspiApi.SECBUFFER_VERSION;
				secInputDesc.cBuffers = 1;
				secInputDesc.pBuffers = inputDescBuffer;

				secInputBuf.BufferType = SspiApi.SECBUFFER_TOKEN;
				secInputBuf.cbBuffer = inputContentSize;
				if (inputContentSize > 0)
				{
					secInputBuf.pvBuffer = Marshal.AllocHGlobal(inputContentSize);

					// read from the stream
					byte[] content = new byte[inputContentSize];
					GenuineUtility.ReadDataFromStream(receivedData, content, 0, inputContentSize);
					Marshal.Copy(content, 0, secInputBuf.pvBuffer, inputContentSize);
				}
				else
					secInputBuf.pvBuffer = IntPtr.Zero;

				Marshal.StructureToPtr(secInputBuf, inputDescBuffer, false);

				// setup output buffer
				secOutputDesc.ulVersion = SspiApi.SECBUFFER_VERSION;
				secOutputDesc.cBuffers = 1;
				secOutputDesc.pBuffers = outputDescBuffer;

				secOutputBuf.BufferType = SspiApi.SECBUFFER_TOKEN;
				secOutputBuf.cbBuffer = 0;
				secOutputBuf.pvBuffer = IntPtr.Zero;
				Marshal.StructureToPtr(secOutputBuf, outputDescBuffer, false);

				int fContextReq = SspiApi.ISC_REQ_REPLAY_DETECT | SspiApi.ISC_REQ_CONNECTION | SspiApi.ISC_REQ_ALLOCATE_MEMORY;
				if ((requiredFeatures & SspiFeatureFlags.Encryption) != 0)
					fContextReq |= SspiApi.ISC_REQ_CONFIDENTIALITY;
				if ((requiredFeatures & SspiFeatureFlags.Signing) != 0)
					fContextReq |= SspiApi.ISC_REQ_INTEGRITY;
				if ((requiredFeatures & SspiFeatureFlags.Delegation) != 0)
					fContextReq |= SspiApi.ISC_REQ_MUTUAL_AUTH | SspiApi.ISC_REQ_DELEGATE;

				int fContextAttr = 0;

				// initialize context
				int result = 0;

				if (_isNT)
					result = SspiApi.InitializeSecurityContext_NT(credHandle, 
						inputPhContext, targetName, fContextReq, 0, 
						SspiApi.SECURITY_NATIVE_DREP, ref secInputDesc, 0, outputPhContext, 
						ref secOutputDesc, ref fContextAttr, ref ptsExpiry);
				else
					result = SspiApi.InitializeSecurityContext__(credHandle, 
						inputPhContext, targetName, fContextReq, 0, 
						SspiApi.SECURITY_NATIVE_DREP, ref secInputDesc, 0, outputPhContext, 
						ref secOutputDesc, ref fContextAttr, ref ptsExpiry);

#if DEBUG
//				GenuineLoggingServices.BinaryLogWriter.Log(LogMessageCategory.Notification, null, "SspiApi.InitializeSecurityContext", 
//					null, "SSPI InitializeSecurityContext function returned {0:X}.", result);
#endif

				// analyze the result
				if (result == SspiApi.SEC_E_OK)
					finalResult = true;
				else if (result == SspiApi.SEC_I_CONTINUE_NEEDED)
					finalResult = false;
				else
					throw GenuineExceptions.Get_Windows_SspiError(result);

				// bring out feature checking into security session built event
				if (finalResult)
				{
					// check whether all the requested features will be available
					if ((requiredFeatures & SspiFeatureFlags.Encryption) != 0 && 
						(fContextAttr & SspiApi.ISC_REQ_CONFIDENTIALITY) == 0)
						throw GenuineExceptions.Get_Windows_SspiDidNotProvideRequestedFeature("SspiFeatureFlags.Encryption");

					if ((requiredFeatures & SspiFeatureFlags.Signing) != 0 && 
						(fContextAttr & SspiApi.ISC_REQ_INTEGRITY) == 0)
						throw GenuineExceptions.Get_Windows_SspiDidNotProvideRequestedFeature("SspiFeatureFlags.Signing");

					if ((requiredFeatures & SspiFeatureFlags.Delegation) != 0 && 
						(fContextAttr & SspiApi.ISC_REQ_DELEGATE) == 0)
						throw GenuineExceptions.Get_Windows_SspiDidNotProvideRequestedFeature("SspiFeatureFlags.Delegation");
				}

				// copy received data from the buffer
				Marshal.PtrToStructure(outputDescBuffer, secOutputBuf);
				if (secOutputBuf.cbBuffer > 0)
				{
					// allocate memory chunk and write content
					byte[] content = new byte[secOutputBuf.cbBuffer];
					Marshal.Copy(secOutputBuf.pvBuffer, content, 0, secOutputBuf.cbBuffer);

					// write it to the stream
					BinaryWriter binaryWriter = new BinaryWriter(outputStream);
					binaryWriter.Write((int) secOutputBuf.cbBuffer);
					binaryWriter.Write(content);
				}
#if TRIAL
#else
			}
			finally
			{
				// release allocated resources
				if (secInputBuf.pvBuffer != IntPtr.Zero)
					Marshal.FreeHGlobal(secInputBuf.pvBuffer);
				if (secOutputBuf.pvBuffer != IntPtr.Zero)
					SspiApi.FreeContextBuffer(secOutputBuf.pvBuffer);

				if (inputDescBuffer != IntPtr.Zero)
					Marshal.FreeHGlobal(inputDescBuffer);

				if (outputDescBuffer != IntPtr.Zero)
					Marshal.FreeHGlobal(outputDescBuffer);
			}
#endif
			return finalResult;
		}


		/// <summary>
		/// Calls AcceptSecurityContext SSPI API function.
		/// Throws an exception if the call fails.
		/// </summary>
		/// <param name="credHandle">Credential handle.</param>
		/// <param name="phContext">Security context.</param>
		/// <param name="requiredFeatures">Requested features.</param>
		/// <param name="ptsExpiry">Expiry.</param>
		/// <param name="receivedData">Input data.</param>
		/// <param name="outputStream">Output data.</param>
		/// <param name="firstCall">Whether phContext parameter has been initialized by previous AcceptSecurityContext call.</param>
		/// <returns>True if security context has been succesfully built up.</returns>
		public static bool AcceptSecurityContext(SspiApi.SecHandle credHandle, 
			SspiApi.SecHandle phContext, SspiFeatureFlags requiredFeatures, 
			ref Int64 ptsExpiry, Stream receivedData, Stream outputStream,
			bool firstCall)
		{
			bool finalResult = false;

			// get the size of the received chunk
			int inputContentSize = 0;
			if (receivedData != null)
			{
				BinaryReader binaryReader = new BinaryReader(receivedData);
				inputContentSize = binaryReader.ReadInt32();
			}

			SspiApi.SecBufferDescNative secInputDesc = new SspiApi.SecBufferDescNative();
			SspiApi.SecBuffer secInputBuf = new SspiApi.SecBuffer();
			SspiApi.SecBufferDescNative secOutputDesc = new SspiApi.SecBufferDescNative();
			SspiApi.SecBuffer secOutputBuf = new SspiApi.SecBuffer();
			IntPtr inputDescBuffer = IntPtr.Zero;
			IntPtr outputDescBuffer = IntPtr.Zero;

#if TRIAL
#else
			try
			{
#endif

				inputDescBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SspiApi.SecBufferDescNative)));
				outputDescBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SspiApi.SecBufferDescNative)));

				// setup input buffer
				secInputDesc.ulVersion = SspiApi.SECBUFFER_VERSION;
				secInputDesc.cBuffers = 1;
				secInputDesc.pBuffers = inputDescBuffer;

				secInputBuf.BufferType = SspiApi.SECBUFFER_TOKEN;
				secInputBuf.cbBuffer = inputContentSize;
				if (inputContentSize > 0)
				{
					secInputBuf.pvBuffer = Marshal.AllocHGlobal(inputContentSize);

					// read from the stream
					byte[] content = new byte[inputContentSize];
					receivedData.Read(content, 0, inputContentSize);
					Marshal.Copy(content, 0, secInputBuf.pvBuffer, inputContentSize);
				}
				else
					secInputBuf.pvBuffer = IntPtr.Zero;

				Marshal.StructureToPtr(secInputBuf, inputDescBuffer, false);

				// setup output buffer
				secOutputDesc.ulVersion = SspiApi.SECBUFFER_VERSION;
				secOutputDesc.cBuffers = 1;
				secOutputDesc.pBuffers = outputDescBuffer;

				secOutputBuf.BufferType = SspiApi.SECBUFFER_TOKEN;
				secOutputBuf.cbBuffer = 0;
				secOutputBuf.pvBuffer = IntPtr.Zero;
				Marshal.StructureToPtr(secOutputBuf, outputDescBuffer, false);

				int fContextReq = SspiApi.ASC_REQ_REPLAY_DETECT | SspiApi.ASC_REQ_CONNECTION | SspiApi.ASC_REQ_ALLOCATE_MEMORY;
				if ((requiredFeatures & SspiFeatureFlags.Encryption) != 0)
					fContextReq |= SspiApi.ASC_REQ_CONFIDENTIALITY;
				if ((requiredFeatures & SspiFeatureFlags.Signing) != 0)
					fContextReq |= SspiApi.ASC_REQ_INTEGRITY;
				if ((requiredFeatures & SspiFeatureFlags.Delegation) != 0)
					fContextReq |= SspiApi.ASC_REQ_MUTUAL_AUTH | SspiApi.ASC_REQ_DELEGATE;

				int fContextAttr = 0;

				// accept context
				int result = 0;

				if (_isNT)
					result = SspiApi.AcceptSecurityContext_NT(credHandle, 
						firstCall ? null : phContext, ref secInputDesc, fContextReq, 
						SspiApi.SECURITY_NATIVE_DREP, phContext,
						ref secOutputDesc, ref fContextAttr, ref ptsExpiry);
				else
					result = SspiApi.AcceptSecurityContext__(credHandle, 
						firstCall ? null : phContext, ref secInputDesc, fContextReq, 
						SspiApi.SECURITY_NATIVE_DREP, phContext,
						ref secOutputDesc, ref fContextAttr, ref ptsExpiry);

#if DEBUG
//				GenuineLoggingServices.BinaryLogWriter.Log(LogMessageCategory.Notification, null, "SspiApi.AcceptSecurityContext", 
//					null, "SSPI AcceptSecurityContext function returned {0:X}.", result);
#endif

				// analyze the result
				if (result == SspiApi.SEC_E_OK)
					finalResult = true;
				else if (result == SspiApi.SEC_I_CONTINUE_NEEDED)
					finalResult = false;
				else
					throw GenuineExceptions.Get_Windows_SspiError(result);

				// bring out feature checking into security session built event
				if (finalResult)
				{
					// check whether all the requested features will be available
					if ((requiredFeatures & SspiFeatureFlags.Encryption) != 0 && 
						(fContextAttr & SspiApi.ASC_REQ_CONFIDENTIALITY) == 0)
						throw GenuineExceptions.Get_Windows_SspiDidNotProvideRequestedFeature("SspiFeatureFlags.Encryption");

					if ((requiredFeatures & SspiFeatureFlags.Signing) != 0 && 
						(fContextAttr & SspiApi.ASC_REQ_INTEGRITY) == 0)
						throw GenuineExceptions.Get_Windows_SspiDidNotProvideRequestedFeature("SspiFeatureFlags.Signing");

					if ((requiredFeatures & SspiFeatureFlags.Delegation) != 0 && 
						(fContextAttr & SspiApi.ASC_REQ_DELEGATE) == 0 )
						throw GenuineExceptions.Get_Windows_SspiDidNotProvideRequestedFeature("SspiFeatureFlags.Delegation");
				}

				// copy received data from the buffer
				Marshal.PtrToStructure(secOutputDesc.pBuffers, secOutputBuf);

				// construct output buffer
				if (secOutputBuf.cbBuffer > 0)
				{
					// allocate memory chunk and write content
					byte[] content = new byte[secOutputBuf.cbBuffer];
					Marshal.Copy(secOutputBuf.pvBuffer, content, 0, secOutputBuf.cbBuffer);

					// write it to the stream
					BinaryWriter binaryWriter = new BinaryWriter(outputStream);
					binaryWriter.Write((int) secOutputBuf.cbBuffer);
					binaryWriter.Write(content);
				}

#if TRIAL
#else
			}
			finally
			{
				if (secInputBuf.pvBuffer != IntPtr.Zero)
					Marshal.FreeHGlobal(secInputBuf.pvBuffer);
				if (secOutputBuf.pvBuffer != IntPtr.Zero)
					SspiApi.FreeContextBuffer(secOutputBuf.pvBuffer);

				if (inputDescBuffer != IntPtr.Zero)
					Marshal.FreeHGlobal(inputDescBuffer);

				if (outputDescBuffer != IntPtr.Zero)
					Marshal.FreeHGlobal(outputDescBuffer);
			}
#endif

			return finalResult;
		}

#if TRIAL
#else

		/// <summary>
		/// Calls DeleteSecurityContext SSPI API function.
		/// Ignores all possible returned errors.
		/// </summary>
		/// <param name="phContext">Security context being deleted.</param>
		public static void DeleteSecurityContext(SecHandle phContext)
		{
			if (_isNT)
				DeleteSecurityContext_NT(phContext);
			else
				DeleteSecurityContext__(phContext);

			phContext.dwLower = 0;
			phContext.dwUpper = 0;
		}

		/// <summary>
		/// Calls FreeCredentialsHandle SSPI API function.
		/// Ignores all possible returned errors.
		/// </summary>
		/// <param name="credHandle">Credential handle being freed.</param>
		public static void FreeCredentialsHandle(SecHandle credHandle)
		{
			if (_isNT)
				FreeCredentialsHandle_NT(credHandle);
			else
				FreeCredentialsHandle__(credHandle);

			credHandle.dwLower = 0;
			credHandle.dwUpper = 0;
		}

		/// <summary>
		/// Calls FreeContextBuffer SSPI API function.
		/// Ignores all possible returned errors.
		/// </summary>
		/// <param name="pvContextBuffer">Context buffer being released.</param>
		public static void FreeContextBuffer(IntPtr pvContextBuffer)
		{
			if (_isNT)
				FreeContextBuffer_NT(pvContextBuffer);
			else
				FreeContextBuffer__(pvContextBuffer);
		}

#endif

		/// <summary>
		/// Gets SecPkgContext_Sizes via QueryContextAttributes call.
		/// Throws an exception if the call fails.
		/// </summary>
		/// <param name="phContext">Security context.</param>
		/// <param name="secPkgContext_Sizes">Reference to SecPkgContext_Sizes instance.</param>
		public static void QueryContextSizes(SecHandle phContext, ref SecPkgContext_Sizes secPkgContext_Sizes)
		{
			int result = 0;

			if (_isNT)
				result = QueryContextAttributes_NT(phContext, SECPKG_ATTR_SIZES, ref secPkgContext_Sizes);
			else
				result = QueryContextAttributes__(phContext, SECPKG_ATTR_SIZES, ref secPkgContext_Sizes);

			if (result != SspiApi.SEC_E_OK)
				throw GenuineExceptions.Get_Windows_SspiError(result);
		}

		/// <summary>
		/// Calls QuerySecurityContextToken SSPI API function.
		/// Throws an exception if the call fails.
		/// </summary>
		/// <param name="phContext">Security context handle.</param>
		/// <returns>WindowsIdentity representing the user account.</returns>
		public static WindowsIdentity QuerySecurityContextToken(SecHandle phContext)
		{
			int result = 0;
			IntPtr phToken = IntPtr.Zero;

			if (_isNT)
				result = QuerySecurityContextToken_NT(phContext, out phToken);
			else
				result = QuerySecurityContextToken__(phContext, out phToken);

			if (result != SspiApi.SEC_E_OK)
				throw GenuineExceptions.Get_Windows_SspiError(result);

			try
			{
				return new WindowsIdentity(phToken);
			}
			finally
			{
				CloseHandle(phToken);
			}
		}

		/// <summary>
		/// Duplicates a security token for another process.
		/// </summary>
		/// <param name="phContext">Security context handle.</param>
		/// <param name="remoteProcessId">The identifier of the remote process.</param>
		/// <returns>The handle of security token for another process.</returns>
		public static IntPtr CloneSecurityToken(SecHandle phContext, int remoteProcessId)
		{
			int result = 0;

			// get the security token
			IntPtr phToken = IntPtr.Zero;
			if (_isNT)
				result = QuerySecurityContextToken_NT(phContext, out phToken);
			else
				result = QuerySecurityContextToken__(phContext, out phToken);

			if (result != SspiApi.SEC_E_OK)
				throw GenuineExceptions.Get_Windows_SspiError(result);

			try
			{
				return CloneSecurityToken(phToken, remoteProcessId);
			}
			finally
			{
				// close security token
				CloseHandle(phToken);
			}
		}

		/// <summary>
		/// Duplicates a security token for another process.
		/// </summary>
		/// <param name="phToken">The security token to be duplicated.</param>
		/// <param name="remoteProcessId">The identifier of another process located within the same host.</param>
		/// <returns>The handle of security token for another process.</returns>
		public static IntPtr CloneSecurityToken(IntPtr phToken, int remoteProcessId)
		{
			IntPtr sspiTokenToDuplicate = IntPtr.Zero;
			IntPtr currToken = IntPtr.Zero;
 
#if FRM20
			// check for impersonated call context
			WindowsIdentity wI = WindowsIdentity.GetCurrent();
			if( ( wI.ImpersonationLevel == TokenImpersonationLevel.Impersonation ) ||
				( wI.ImpersonationLevel == TokenImpersonationLevel.Delegation ) )
			{
				// ttore the current token
				currToken = WindowsIdentity.GetCurrent().Token;
 
				// temporarily remove the impersonation from the call context so the 
				// OpenProcess call gets the process-tokens privileges
				WindowsIdentity.Impersonate( IntPtr.Zero );
			} 
#endif

			// retrieve the process handle
			IntPtr remoteProcessHandle = OpenProcess(PROCESS_DUP_HANDLE, true, remoteProcessId);
			if (remoteProcessHandle == IntPtr.Zero)
			{
				int lastWin32Error = Marshal.GetHRForLastWin32Error();

				// if we were impersonating put it back now
				if( currToken != IntPtr.Zero )
				{
					// Put the token back as we've obtained a handle to the 'remote' process
					WindowsIdentity.Impersonate( currToken );
				}

				Marshal.ThrowExceptionForHR(lastWin32Error);
			}
 
			try
			{
				// if we were impersonating put it back now
				if( currToken != IntPtr.Zero )
				{
					// Put the token back as we've obtained a handle to the 'remote' process
					WindowsIdentity.Impersonate( currToken );
				}
 
				// duplicate the source security token
				IntPtr targetHandle = IntPtr.Zero;
				bool successful = DuplicateHandle((IntPtr) (int) -1, phToken, remoteProcessHandle, out targetHandle, 0, true, DUPLICATE_SAME_ACCESS);
				if (! successful)
					Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());

				return targetHandle;
			}
			finally
			{
				// close remote process handle
				CloseHandle(remoteProcessHandle);
			}
		}

		/// <summary>
		/// Releases the security token.
		/// </summary>
		/// <param name="phToken">The security token passed to another process.</param>
		public static void CloseClonedSecurityToken(IntPtr phToken)
		{
			if (! CloseHandle(phToken))
				Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
		}

		/// <summary>
		/// Calls ImpersonateSecurityContext SSPI API function.
		/// Throws an exception if the call fails.
		/// </summary>
		/// <param name="phContext">Security context handle.</param>
		public static void ImpersonateSecurityContext(SecHandle phContext)
		{
			int result = 0;

			if (_isNT)
				result = ImpersonateSecurityContext_NT(phContext);
			else
				result = ImpersonateSecurityContext__(phContext);

			if (result != SspiApi.SEC_E_OK)
				throw GenuineExceptions.Get_Windows_SspiError(result);
		}

		/// <summary>
		/// Calls RevertSecurityContext_NT SSPI API function.
		/// Throws an exception if the call fails.
		/// </summary>
		/// <param name="phContext">Security context handle.</param>
		public static void RevertSecurityContext(SecHandle phContext)
		{
			int result = 0;

			if (_isNT)
				result = RevertSecurityContext_NT(phContext);
			else
				result = RevertSecurityContext__(phContext);

			if (result != SspiApi.SEC_E_OK)
				throw GenuineExceptions.Get_Windows_SspiError(result);
		}

		/// <summary>
		/// Calls MakeSignature SSPI API function and saves obtained signature to 
		/// outputSignature parameter.
		/// Throws an exception if the call fails.
		/// </summary>
		/// <param name="phContext">Established security context handle.</param>
		/// <param name="contentBuffer">Content to make signature for.</param>
		/// <param name="outputSignature">Output stream to write signature to.</param>
		/// <param name="secPkgContext_Sizes">Security context size constants.</param>
		public static void MakeSignature(SspiApi.SecHandle phContext, 
			byte[] contentBuffer, BinaryWriter outputSignature, ref SecPkgContext_Sizes secPkgContext_Sizes)
		{
			SspiApi.SecBufferDescNative secDesc = new SspiApi.SecBufferDescNative();
			SspiApi.SecBufferNative[] secBuffers = new SspiApi.SecBufferNative[2];

			byte[] signBuffer = new byte[secPkgContext_Sizes.cbMaxSignature];
			using (GCHandleKeeper pinnedSignBuffer = new GCHandleKeeper(signBuffer, GCHandleType.Pinned))
			{
				// the first is for getting a sign
				secBuffers[0].BufferType = SspiApi.SECBUFFER_TOKEN;
				secBuffers[0].cbBuffer = secPkgContext_Sizes.cbMaxSignature;
				secBuffers[0].pvBuffer = pinnedSignBuffer.GCHandle.AddrOfPinnedObject();

				// pin contentBuffer
				secBuffers[1].BufferType = SspiApi.SECBUFFER_DATA;
				secBuffers[1].cbBuffer = (int) contentBuffer.Length;
				using (GCHandleKeeper pinnedContentBuffer = new GCHandleKeeper(contentBuffer, GCHandleType.Pinned))
				{
					secBuffers[1].pvBuffer = pinnedContentBuffer.GCHandle.AddrOfPinnedObject();

					// setup descriptor
					secDesc.ulVersion = SspiApi.SECBUFFER_VERSION;
					secDesc.cBuffers = 2;
					using (GCHandleKeeper pinnedSecBuffers = new GCHandleKeeper(secBuffers, GCHandleType.Pinned))
					{
						secDesc.pBuffers = pinnedSecBuffers.GCHandle.AddrOfPinnedObject();

						int result = 0;
						if (_isNT)
							result = MakeSignature_NT(phContext, 0, ref secDesc, 0);
						else
							result = MakeSignature__(phContext, 0, ref secDesc, 0);
						if (result != SspiApi.SEC_E_OK)
							throw GenuineExceptions.Get_Windows_SspiError(result);

						outputSignature.Write(signBuffer, 0, secBuffers[0].cbBuffer);
					}	// pin sec buffers
				}	// pin the content buffer
			}	// pin the signature buffer
		}

		/// <summary>
		/// Calls VerifySignature SSPI API function to ensure that given signature 
		/// is correct.
		/// Throws an exception if the call fails.
		/// </summary>
		/// <param name="phContext">Established security context handle.</param>
		/// <param name="contentBuffer">Content to check the signature for.</param>
		/// <param name="signature">Stream containing the signature.</param>
		public static void VerifySignature(SspiApi.SecHandle phContext, 
			byte[] contentBuffer, byte[] signature)
		{
			SspiApi.SecBufferDescNative secDesc = new SspiApi.SecBufferDescNative();
			SspiApi.SecBufferNative[] secBuffers = new SspiApi.SecBufferNative[2];

			using (GCHandleKeeper pinnedSignature = new GCHandleKeeper(signature, GCHandleType.Pinned))
			{
				// the first contains the signature
				secBuffers[0].BufferType = SspiApi.SECBUFFER_TOKEN;
				secBuffers[0].cbBuffer = signature.Length;
				secBuffers[0].pvBuffer = pinnedSignature.GCHandle.AddrOfPinnedObject();

				using (GCHandleKeeper pinnedContentBuffer = new GCHandleKeeper(contentBuffer, GCHandleType.Pinned))
				{
					// and pin it for signing
					secBuffers[1].BufferType = SspiApi.SECBUFFER_DATA;
					secBuffers[1].cbBuffer = contentBuffer.Length;
					secBuffers[1].pvBuffer = pinnedContentBuffer.GCHandle.AddrOfPinnedObject();

					using (GCHandleKeeper pinnedSecBuffers = new GCHandleKeeper(secBuffers, GCHandleType.Pinned))
					{
						// setup descriptor
						secDesc.ulVersion = SspiApi.SECBUFFER_VERSION;
						secDesc.cBuffers = 2;
						secDesc.pBuffers = pinnedSecBuffers.GCHandle.AddrOfPinnedObject();

						int result = 0;
						int pfQOP = 0;
						if (_isNT)
							result = VerifySignature_NT(phContext, ref secDesc, 0, ref pfQOP);
						else
							result = VerifySignature__(phContext, ref secDesc, 0, ref pfQOP);
						if (result != SspiApi.SEC_E_OK)
							throw GenuineExceptions.Get_Windows_SspiError(result);
					}	// pin sec buffers
				}	// pin the content buffer
			}	// pin the signature buffer
		}

		/// <summary>
		/// Calls EncryptMessage SSPI API function.
		/// Throws an exception if the call fails.
		/// </summary>
		/// <param name="phContext">Established security context handle.</param>
		/// <param name="sourceContent">Source data.</param>
		/// <param name="outputContent">Writer to write encrypted data to.</param>
		/// <param name="secPkgContext_Sizes">Current package's sizes.</param>
		public static void EncryptMessage(SspiApi.SecHandle phContext, 
			Stream sourceContent, BinaryWriter outputContent, 
			ref SecPkgContext_Sizes secPkgContext_Sizes)
		{
			SspiApi.SecBufferDescNative secDesc = new SspiApi.SecBufferDescNative();
			// for token, data and padding
			SspiApi.SecBufferNative[] secBuffers = new SspiApi.SecBufferNative[3];

			byte[] signature = new byte[secPkgContext_Sizes.cbSecurityTrailer];
			byte[] message = new byte[(int) sourceContent.Length];
			byte[] padding = new byte[secPkgContext_Sizes.cbBlockSize];

			GenuineUtility.ReadDataFromStream(sourceContent, message, 0, message.Length);

			using (GCHandleKeeper pinnedSignature = new GCHandleKeeper(signature, GCHandleType.Pinned))
			{
				// the first is for signature
				secBuffers[0].BufferType = SspiApi.SECBUFFER_TOKEN;
				secBuffers[0].cbBuffer = signature.Length;
				secBuffers[0].pvBuffer = pinnedSignature.GCHandle.AddrOfPinnedObject();

				using (GCHandleKeeper pinnedMessage = new GCHandleKeeper(message, GCHandleType.Pinned))
				{
					// the second contains data
					secBuffers[1].BufferType = SspiApi.SECBUFFER_DATA;
					secBuffers[1].cbBuffer = message.Length;
					secBuffers[1].pvBuffer = pinnedMessage.GCHandle.AddrOfPinnedObject();

					using (GCHandleKeeper pinnedPadding = new GCHandleKeeper(padding, GCHandleType.Pinned))
					{
						// the third is for padding
						secBuffers[2].BufferType = SspiApi.SECBUFFER_PADDING;
						secBuffers[2].cbBuffer = padding.Length;
						secBuffers[2].pvBuffer = pinnedPadding.GCHandle.AddrOfPinnedObject();

						// setup descriptor
						secDesc.ulVersion = SspiApi.SECBUFFER_VERSION;
						secDesc.cBuffers = 3;
						using (GCHandleKeeper pinnedSecBuffers = new GCHandleKeeper(secBuffers, GCHandleType.Pinned))
						{
							secDesc.pBuffers = pinnedSecBuffers.GCHandle.AddrOfPinnedObject();

							// make the call
							int result = 0;
							if (_isNT)
								result = EncryptMessage_NT(phContext, 0, ref secDesc, 0);
							else
								result = EncryptMessage__(phContext, 0, ref secDesc, 0);
							if (result != SspiApi.SEC_E_OK)
								throw GenuineExceptions.Get_Windows_SspiError(result);

						}
					}

				}
			}

			// write sizes
			outputContent.Write( (int) secBuffers[0].cbBuffer );
			outputContent.Write( (int) secBuffers[1].cbBuffer );
			outputContent.Write( (int) secBuffers[2].cbBuffer );

			// and content
			outputContent.Write( signature, 0, secBuffers[0].cbBuffer );
			outputContent.Write( message, 0, secBuffers[1].cbBuffer );
			outputContent.Write( padding, 0, secBuffers[2].cbBuffer );
		}

		/// <summary>
		/// Calls DecryptMessage SSPI API function.
		/// Throws an exception if the call fails.
		/// </summary>
		/// <param name="phContext">Established security context handle.</param>
		/// <param name="sourceContent">Stream containing encrypted data.</param>
		/// <returns>Stream containing decrypted data.</returns>
		public static Stream DecryptMessage(SspiApi.SecHandle phContext, 
			BinaryReader sourceContent)
		{
			SspiApi.SecBufferDescNative secDesc = new SspiApi.SecBufferDescNative();
			// for token, data and padding
			SspiApi.SecBufferNative[] secBuffers = new SspiApi.SecBufferNative[3];

			// read sizes
			int signatureSize = sourceContent.ReadInt32();
			int messageSize = sourceContent.ReadInt32();
			int paddingSize = sourceContent.ReadInt32();

			// allocate buffers
			byte[] signature = new byte[signatureSize];
			byte[] message = new byte[messageSize];
			byte[] padding = new byte[paddingSize];

			// read content
			GenuineUtility.ReadDataFromStream(sourceContent.BaseStream, signature, 0, signatureSize);
			GenuineUtility.ReadDataFromStream(sourceContent.BaseStream, message, 0, messageSize);
			GenuineUtility.ReadDataFromStream(sourceContent.BaseStream, padding, 0, paddingSize);

			using (GCHandleKeeper pinnedSignature = new GCHandleKeeper(signature, GCHandleType.Pinned))
			{
				// the first is for signature
				secBuffers[0].BufferType = SspiApi.SECBUFFER_TOKEN;
				secBuffers[0].cbBuffer = signatureSize;
				secBuffers[0].pvBuffer = pinnedSignature.GCHandle.AddrOfPinnedObject();

				using (GCHandleKeeper pinnedMessage = new GCHandleKeeper(message, GCHandleType.Pinned))
				{
					// the second contains data
					secBuffers[1].BufferType = SspiApi.SECBUFFER_DATA;
					secBuffers[1].cbBuffer = messageSize;
					secBuffers[1].pvBuffer = pinnedMessage.GCHandle.AddrOfPinnedObject();

					using (GCHandleKeeper pinnedPadding = new GCHandleKeeper(padding, GCHandleType.Pinned))
					{
						// the third is for padding
						secBuffers[2].BufferType = SspiApi.SECBUFFER_PADDING;
						secBuffers[2].cbBuffer = paddingSize;
						secBuffers[2].pvBuffer = pinnedPadding.GCHandle.AddrOfPinnedObject();

						// setup descriptor
						secDesc.ulVersion = SspiApi.SECBUFFER_VERSION;
						secDesc.cBuffers = 3;
						using (GCHandleKeeper pinnedSecBuffers = new GCHandleKeeper(secBuffers, GCHandleType.Pinned))
						{
							secDesc.pBuffers = pinnedSecBuffers.GCHandle.AddrOfPinnedObject();

							// make the call
							int result = 0;
							int pfQOP = 0;
							if (_isNT)
								result = DecryptMessage_NT(phContext, ref secDesc, 0, ref pfQOP);
							else
								result = DecryptMessage__(phContext, ref secDesc, 0, ref pfQOP);
							if (result != SspiApi.SEC_E_OK)
								throw GenuineExceptions.Get_Windows_SspiError(result);
						}
					}

				}
			}

			// return the result
			return new MemoryStream(message, 0, secBuffers[1].cbBuffer, false, true);
		}

		#endregion
	}
}
