using System;

namespace KnownObjects
{
	/// <summary>
	/// Describes the object that can return itself.
	/// </summary>
	public interface IReturnItself
	{
		/// <summary>
		/// Returns itself.
		/// </summary>
		MarshalByRefObject ReturnItself();
	}
}
