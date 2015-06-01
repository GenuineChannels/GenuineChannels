/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

namespace Belikov.GenuineChannels.Security
{
	/// <summary>
	/// An interface allowing to specify the name of default Security Session in the specific context.
	/// </summary>
	public interface ISetSecuritySession
	{
		/// <summary>
		/// Gets or sets the Security Session used in this context.
		/// </summary>
		SecuritySessionParameters SecuritySessionParameters { get; set; }
	}
}
