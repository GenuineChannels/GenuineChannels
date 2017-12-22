using System;
using System.Threading;

using KnownObjects;

namespace Server
{
	/// <summary>
	/// Summary description for OperationProvider.
	/// </summary>
	public class OperationProvider : MarshalByRefObject, IOperationProvider
	{
		/// <summary>
		/// Performs a long-duration operation.
		/// </summary>
		/// <returns>A string.</returns>
		public string Do()
		{
			Console.WriteLine("Start long-duration operation.");
			Thread.Sleep(TimeSpan.FromHours(24));

			return "Success.";
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
