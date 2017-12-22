using System;
using System.Net;

using Belikov.GenuineChannels.Security.SSPI;

namespace Known
{
	/// <summary>
	/// ICreateFile.
	/// </summary>
	public interface ICreateFile
	{
		/// <summary>
		/// Creates a file.
		/// </summary>
		/// <param name="name">File name.</param>
		/// <param name="content">File content.</param>
		void Create(string name, string content);
	}
}
