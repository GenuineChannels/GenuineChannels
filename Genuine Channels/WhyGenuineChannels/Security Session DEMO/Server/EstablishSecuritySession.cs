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
	/// EstablishSecuritySession.
	/// </summary>
	public class EstablishSecuritySession : MarshalByRefObject, IEstablishSecuritySession
	{

		/// <summary>
		/// Returns current established security session.
		/// </summary>
		public string CurrentSecuritySessionName
		{
			get
			{
				lock(_currentSecuritySessionNameLock)
					return this._currentSecuritySessionName;
			}
		}
		public string _currentSecuritySessionName = Belikov.GenuineChannels.Security.SecuritySessionServices.DefaultContext.Name;
		public object _currentSecuritySessionNameLock = new object();

		/// <summary>
		/// Creates a Security Session.
		/// </summary>
		/// <param name="name">Security Session name.</param>
		/// <param name="securitySessionType">Security Session type.</param>
		/// <param name="sspiFeatureFlags">Requested features.</param>
		/// <param name="sspiPackage">SSPI package.</param>
		public void CreateSecuritySession(string name, SecuritySessionType securitySessionType, 
			SspiFeatureFlags sspiFeatureFlags, SupportedSspiPackages sspiPackage)
		{
			IKeyProvider iKeyProvider = null;

			switch(securitySessionType)
			{
				case SecuritySessionType.Basic:
					iKeyProvider = new KeyProvider_Basic();
					break;

				case SecuritySessionType.KnownSymmetric:
					SymmetricAlgorithm symmetricAlgorithm = SymmetricAlgorithm.Create();

					// read the key
					Stream stream = typeof(IEstablishSecuritySession).Assembly.GetManifestResourceStream("Known.written_key");
					byte[] key = new byte[32];
					stream.Read(key, 0, key.Length);
					stream.Close();

					// initialize the key
					symmetricAlgorithm.Key = key;
					symmetricAlgorithm.Mode = CipherMode.ECB;

					iKeyProvider = new KeyProvider_KnownSymmetric(symmetricAlgorithm);
					break;

				case SecuritySessionType.SelfEstablishingSymmetric:
					iKeyProvider = new KeyProvider_SelfEstablishingSymmetric();
					break;

				case SecuritySessionType.Sspi:
					iKeyProvider = new KeyProvider_SspiServer(sspiFeatureFlags, sspiPackage);
					break;
			}

			// register the Security Session in the current Transport Context
			GenuineUtility.CurrentMessage.ITransportContext.IKeyStore.SetKey(name, iKeyProvider);
			this._currentSecuritySessionName = name;
		}

		/// <summary>
		/// This is to ensure that when created as a Singleton, the first instance never dies,
		/// regardless of the expired time.
		/// </summary>
		/// <returns></returns>
		public override object InitializeLifetimeService()
		{
			return null;
		}
	}
}
