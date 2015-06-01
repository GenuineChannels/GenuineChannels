/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Runtime.Serialization;

namespace Belikov.GenuineChannels
{
	/// <summary>
	/// OperationException is an exception that contains OperationErrorMessage.
	/// </summary>
	[Serializable]
	public class OperationException : Exception, ICloneable
	{
		/// <summary>
		/// The error message.
		/// </summary>
		public OperationErrorMessage OperationErrorMessage;

		/// <summary>
		/// Contructs an instance of the OperationException.
		/// </summary>
		/// <param name="operationErrorMessage">The error message.</param>
		public OperationException(OperationErrorMessage operationErrorMessage) : base(operationErrorMessage.UserFriendlyMessage)
		{
			this.OperationErrorMessage = operationErrorMessage;
		}

		/// <summary>
		/// Initializes a new instance of the OperationException class with serialized data.
		/// </summary>
		/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
		/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
		public OperationException(SerializationInfo info, StreamingContext context) : base(info, context)
		{
			this.OperationErrorMessage = info.GetValue("oe", typeof(OperationErrorMessage)) as OperationErrorMessage;
		}

		/// <summary>
		/// Builds an instance of the OperationException class over the provided exception.
		/// </summary>
		/// <param name="exception">The source exception.</param>
		public static Exception WrapException(Exception exception)
		{
			ICloneable cloneable = exception as ICloneable;
			if (cloneable != null)
				return cloneable.Clone() as Exception;

			return new OperationException(new OperationErrorMessage(string.Empty, exception.Message));
		}

		/// <summary>
		/// Creates a new object that is a copy of the current instance.
		/// </summary>
		/// <returns>A new object that is a copy of the current instance.</returns>
		public virtual object Clone()
		{
			return new OperationException(this.OperationErrorMessage);
		}

		/// <summary>
		/// Constructs a new instance of the OperationException class.
		/// </summary>
		/// <param name="objToFetchTypeFrom">If object is Type, then its FullName will be taken as an errorIdentifier start substring. Otherwise GetType() operation will be executed first to fetch Type object.</param>
		/// <param name="errorSubIdentifier">ErrorSubIdentifier being added to the end of the object's type FullName.</param>
		/// <param name="userFriendlyMessage">User friendly message that can be shown to an end-user.</param>
		public OperationException(object objToFetchTypeFrom, string errorSubIdentifier, string userFriendlyMessage) : base(userFriendlyMessage)
		{
			string errorIdentifier = null;

			if (objToFetchTypeFrom is Type)
				errorIdentifier = ((Type) objToFetchTypeFrom).FullName + "." + errorSubIdentifier;
			else if (objToFetchTypeFrom != null)
				errorIdentifier = objToFetchTypeFrom.GetType().FullName + "." + errorSubIdentifier;
			else
				errorIdentifier = errorSubIdentifier;

			this.OperationErrorMessage = new OperationErrorMessage(errorIdentifier, userFriendlyMessage);
		}

		/// <summary>
		/// Gets a message that describes the current exception.
		/// </summary>
		public override string Message
		{
			get
			{
				return this.OperationErrorMessage.UserFriendlyMessage;
			}
		}

		/// <summary>
		/// Returns a String that represents the current Object.
		/// </summary>
		/// <returns>A String that represents the current Object.</returns>
		public override string ToString()
		{
			return "Genuine channels operation exception: " + this.Message;
		}

		/// <summary>
		/// Sets the SerializationInfo with information about the exception.
		/// </summary>
		/// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
		/// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue("oe", this.OperationErrorMessage);
		}
	}
}
