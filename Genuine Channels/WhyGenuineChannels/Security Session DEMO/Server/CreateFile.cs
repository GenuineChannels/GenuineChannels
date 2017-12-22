using System;
using System.IO;
using System.Security.Cryptography;

using Known;
using Belikov.GenuineChannels;
using Belikov.GenuineChannels.Security;
using Belikov.GenuineChannels.Security.SSPI;

namespace Server
{
	/// <summary>
	/// CreateFile.
	/// </summary>
	public class CreateFile : MarshalByRefObject, ICreateFile
	{
		/// <summary>
		/// Creates a file.
		/// </summary>
		/// <param name="name">File name.</param>
		/// <param name="content">File content.</param>
		public void Create(string name, string content)
		{
			using(StreamWriter writer = File.CreateText(name))
				writer.Write(content);
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
