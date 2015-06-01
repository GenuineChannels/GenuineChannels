/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Collections;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace Belikov.GenuineChannels.Security.ZeroProofAuthorization
{
	/// <summary>
	/// Provides a set of functions simplifying implementation of the Zero Proof Authorization.
	/// </summary>
	internal class ZeroProofAuthorizationUtility
	{
		/// <summary>
		/// Prevents the constructing an instance of the ZeroProofAuthorizationUtility class.
		/// </summary>
		private ZeroProofAuthorizationUtility()
		{
		}

		/// <summary>
		/// Generates a random byte sequence with the specified size.
		/// </summary>
		/// <param name="size">The number of bytes in the sequence.</param>
		/// <returns>The generated sequence.</returns>
		public static byte[] GenerateArbitrarySequence(int size)
		{
			RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create();
			byte[] buffer = new byte[size];
			randomNumberGenerator.GetNonZeroBytes(buffer);
			return buffer;
		}

		/// <summary>
		/// Generates a sequence based on specified string password and salt.
		/// </summary>
		/// <param name="password">The string password.</param>
		/// <param name="salt">The salt.</param>
		/// <param name="size">The size of the result sequence.</param>
		/// <returns>The generated sequence.</returns>
		public static byte[] GeneratePasswordBasedSequence(string password, byte[] salt, int size)
		{
			PasswordDeriveBytes passwordDeriveBytes = new PasswordDeriveBytes(password, salt);
			return passwordDeriveBytes.GetBytes(size);
		}

		/// <summary>
		/// Calculates a Hash-based Message Authentication Code of the password using the specified salt.
		/// </summary>
		/// <param name="password">The password.</param>
		/// <param name="salt">The salt.</param>
		/// <returns>The hash.</returns>
		public static byte[] CalculateDefaultKeyedHash(string password, byte[] salt)
		{
			HMACSHA1 hmacsha1 = new HMACSHA1();
			hmacsha1.Key = salt;
			return hmacsha1.ComputeHash(UnicodeEncoding.Unicode.GetBytes(password));
		}

		/// <summary>
		/// Checks two buffers on identity.
		/// </summary>
		/// <param name="first">The first buffer.</param>
		/// <param name="second">The second buffer.</param>
		/// <param name="size">The size.</param>
		/// <returns>True if buffers contains equal content.</returns>
		public static bool CompareBuffers(byte[] first, byte[] second, int size)
		{
			if (first.Length < size || second.Length < size)
				return false;
			for ( int i = 0; i < first.Length; i++ )
				if (first[i] != second[i])
					return false;
			return true;
		}
	}
}
