using System;

namespace KnownObjects
{
	/// <summary>
	/// IOperationProvider.
	/// </summary>
	public interface IOperationProvider
	{
		/// <summary>
		/// Performs a long-duration operation.
		/// </summary>
		/// <returns>A string.</returns>
		string Do();
	}
}
