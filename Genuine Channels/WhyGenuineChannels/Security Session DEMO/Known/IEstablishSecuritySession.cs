using System;

using Belikov.GenuineChannels.Security.SSPI;

namespace Known
{
	public enum SecuritySessionType
	{
		Basic,
		KnownSymmetric,
		SelfEstablishingSymmetric,
		Sspi
	}

	/// <summary>
	/// IEstablishSecuritySession.
	/// </summary>
	public interface IEstablishSecuritySession
	{
		/// <summary>
		/// Creates a Security Session.
		/// </summary>
		/// <param name="name">Security Session name.</param>
		/// <param name="securitySessionType">Security Session type.</param>
		/// <param name="sspiFeatureFlags">Requested features.</param>
		/// <param name="sspiPackage">SSPI package.</param>
		void CreateSecuritySession(string name, SecuritySessionType securitySessionType, 
			SspiFeatureFlags sspiFeatureFlags, SupportedSspiPackages sspiPackage);
	}
}
