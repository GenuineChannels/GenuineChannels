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
		private static int _number = 0;

		/// <summary>
		/// Performs a long-duration operation.
		/// </summary>
		/// <returns>A string.</returns>
		public string Do()
		{
			int operationNumber = Interlocked.Increment(ref _number);
			Console.WriteLine("Start long-duration operation N: {0}.", operationNumber);
			Thread.Sleep(TimeSpan.FromSeconds(20));
			Console.WriteLine("Operation N {0} is finished.", operationNumber);

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
