using System;

using KnownObjects;

namespace Server
{
	/// <summary>
	/// ReturnItself.
	/// </summary>
	public class ReturnItselfImplementation : MarshalByRefObject, IReturnItself
	{
		/// <summary>
		/// Returns itself.
		/// </summary>
		/// <returns></returns>
		public MarshalByRefObject ReturnItself()
		{
			return this;
		}

		/// <summary>
		/// This is to insure that when created as a Singleton, the first instance never dies,
		/// regardless of the expired time.
		/// </summary>
		/// <returns></returns>
		public override object InitializeLifetimeService()
		{
			return null;
		}
	}
}
