/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

namespace Belikov.GenuineChannels.Security.ZeroProofAuthorization
{
	/// <summary>
	/// Enumerates features provided by Zero Proof Authorization Security Provider.
	/// WARNING: serialized as a byte.
	/// </summary>
	[Flags]
	public enum ZpaFeatureFlags
	{
		/// <summary>
		/// Simple authentication without content encryption.
		/// </summary>
		None = 0,

		/// <summary>
		/// Enables Rijndael 256-bit encryption in the electronic codebook (ECB, no chaining
		/// between blocks) mode.
		/// </summary>
		ElectronicCodebookEncryption = 1,

		/// <summary>
		/// Enables Rijndael 256-bit encryption with the chaining mode of operation of the cipher.
		/// This is the strongest generally available mode of operation.
		/// Unless you explicitly have a reason to choose another chaining mode, 
		/// you probably want to use this type of cipher block chaining.
		/// </summary>
		CipherBlockChainingEncryption = 2,

		/// <summary>
		/// Enables the packet integrity checking based on MAC-3DES-CBC (64-bit) keyed hash algorithm.
		/// </summary>
		Mac3DesCbcSigning = 4,

		/// <summary>
		/// Enables the packet integrity checking based on HMAC-SHA1 (160-bit) keyed hash algorithm.
		/// </summary>
		HmacSha1Signing = 8,
	}
}
