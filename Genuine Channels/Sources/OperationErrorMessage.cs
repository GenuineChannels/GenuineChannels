/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;

namespace Belikov.GenuineChannels
{
	/// <summary>
	/// OperationErrorMessage represents an error fired by Genuine Channels solution.
	/// It contains a user friendly error string and an error identifier.
	/// </summary>
	[Serializable]
	public class OperationErrorMessage
	{
		/// <summary>
		/// For XML serialization.
		/// </summary>
		public OperationErrorMessage()
		{
		}

		/// <summary>
		/// The server-side constructor.
		/// </summary>
		/// <param name="errorIdentifier"></param>
		/// <param name="userFriendlyMessage"></param>
		public OperationErrorMessage(string errorIdentifier, string userFriendlyMessage)
		{
			this.ErrorIdentifier = errorIdentifier;
			this.UserFriendlyMessage = userFriendlyMessage;
		}

		/// <summary>
		/// Provides error identifier that can be used to recognize standard or predefined errors on
		/// the client side and output the message in regard to circumstances.
		/// It should look like: AssemblyFullPath.BusinessObject.Action.Error.
		/// </summary>
		public string ErrorIdentifier;

		/// <summary>
		/// The error string that can be shown to a user to explain the failure.
		/// </summary>
		public string UserFriendlyMessage;
	}
}
