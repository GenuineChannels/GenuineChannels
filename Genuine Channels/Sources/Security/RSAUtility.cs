/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Reflection;
using System.Security;
using System.Security.Cryptography;
using System.Runtime.InteropServices;

namespace Belikov.GenuineChannels.Security
{
	/// <summary>
	/// Provides all the necessary functionality to implement RSA encryption.
	/// </summary>
	public class RSAUtility
	{
		[DllImport("Advapi32", SetLastError=true)]
		static private extern int CryptEncrypt(IntPtr hKey, IntPtr hHash, bool final, Int32 dwFlags,
			byte[] pbData, ref Int32 pdwDataLen, Int32 dwBufLen);

		[DllImport("Advapi32", SetLastError=true)]
		static private extern int CryptDecrypt(IntPtr hKey, IntPtr hHash, bool final, Int32 dwFlags,
			byte[] pbData, ref Int32 pdwDataLen);

		/// <summary>
		/// A small trick, never never do so.
		/// </summary>
		private static FieldInfo HKeyField 
		{ 
			get
			{
				lock (_hKeyFieldLock)
				{
					if (_hKeyField == null)
					{
						FieldInfo[] myFieldInfo = typeof(RSACryptoServiceProvider).GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
						for(int i = 0; i < myFieldInfo.Length; i++)
							if (myFieldInfo[i].Name == "_hKey")
							{
								_hKeyField = myFieldInfo[i];
								break;
							}
					}

					return _hKeyField;
				}
			}
		}
		internal static FieldInfo _hKeyField;
		internal static object _hKeyFieldLock = new object();

		/// <summary>
		/// Encrypts content using provided RSA key.
		/// </summary>
		/// <param name="rsa">The RSA key to be used.</param>
		/// <param name="source">Source sequence of bytes.</param>
		/// <returns>Encrypted content.</returns>
		public static byte[] Encrypt(RSACryptoServiceProvider rsa, byte[] source)
		{
#if FRM20
            return rsa.Encrypt(source, false);
#else
			// calculate result size
			int keySizeInBytes = rsa.KeySize / 8;
			int bufferSize = ((source.Length / keySizeInBytes) + 1) * keySizeInBytes;
			byte[] result = new byte[bufferSize];
			Buffer.BlockCopy(source, 0, result, 0, source.Length);

			int dataLen = source.Length;
			int returnCode = CryptEncrypt((IntPtr) HKeyField.GetValue(rsa), IntPtr.Zero, true, 0, result, ref dataLen, result.Length);
			if (returnCode == 0)
				throw new ApplicationException("RSA encryption failed with error code = " + Marshal.GetLastWin32Error());

			if (dataLen != result.Length)
			{
				byte[] correctResult = new byte[dataLen];
				Buffer.BlockCopy(result, 0, correctResult, 0, dataLen);
				return correctResult;
			}

			return result;
#endif
		}

		/// <summary>
		/// Decrypts the content using the provided RSA key.
		/// </summary>
		/// <param name="rsa">The RSA key to be used.</param>
		/// <param name="source">The encrypted envelope.</param>
		/// <returns>Decrypted content.</returns>
		public static byte[] Decrypt(RSACryptoServiceProvider rsa, byte[] source)
		{
#if FRM20
            return rsa.Decrypt(source, false);
#else
			// calculate result size
			byte[] result = new byte[source.Length];
			Buffer.BlockCopy(source, 0, result, 0, source.Length);

			int dataLen = source.Length;
			int returnCode = CryptDecrypt((IntPtr) HKeyField.GetValue(rsa), IntPtr.Zero, true, 0, result, ref dataLen);
			if (returnCode == 0)
				throw new ApplicationException("RSA encryption failed with error code = " + Marshal.GetLastWin32Error());

			if (dataLen != result.Length)
			{
				byte[] correctResult = new byte[dataLen];
				Buffer.BlockCopy(result, 0, correctResult, 0, dataLen);
				return correctResult;
			}

			return result;
#endif
		}
	}
}
