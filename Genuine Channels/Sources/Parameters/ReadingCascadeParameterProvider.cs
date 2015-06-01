/* Genuine Channels product.
 * 
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 * 
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Net;
using System.Collections;

using Belikov.GenuineChannels.Logbook;

namespace Belikov.GenuineChannels.Parameters
{
	/// <summary>
	/// Provides a set of parameters read from the dictionary.
	/// Missing parameters are got from the underlying IParameterProvider provider.
	/// Does not cache missing parameters.
	/// </summary>
	public class ReadingCascadeParameterProvider : MarshalByRefObject, IParameterProvider
	{
		/// <summary>
		/// Initializes parameters from the provided dictionary.
		/// </summary>
		/// <param name="parameters">A dictionary containing parameters to be read.</param>
		/// <param name="underlyingProvider">Underlying parameter provider.</param>
		public ReadingCascadeParameterProvider(IDictionary parameters, IParameterProvider underlyingProvider)
		{
			this._readParameters = new object[(int) GenuineParameter.LastParameter + 1];
			this.UnderlyingProvider = underlyingProvider;

			// read parameters from the dictionary
			foreach (DictionaryEntry entry in (IDictionary) parameters)
			{
				string parameterName = entry.Key.ToString().ToUpper();
				switch(parameterName)
				{
						// Queue parameters --------------------------------------------------------
					case "MAXCONTENTSIZE":
						this._readParameters[(int) GenuineParameter.MaxContentSize] = GenuineUtility.SafeConvertToInt32(entry.Value);
						break;

					case "MAXQUEUEDITEMS":
						this._readParameters[(int) GenuineParameter.MaxQueuedItems] = GenuineUtility.SafeConvertToInt32(entry.Value);
						break;

					case "MAXTOTALSIZE":
						this._readParameters[(int) GenuineParameter.MaxTotalSize] = GenuineUtility.SafeConvertToInt32(entry.Value);
						break;

					case "NOSIZECHECKING":
						this._readParameters[(int) GenuineParameter.NoSizeChecking] = GenuineUtility.SafeConvertToBool(entry.Value);
						break;

					case "COMPRESSION":
					case "COMPRESS":
						this._readParameters[(int) GenuineParameter.Compression] = GenuineUtility.SafeConvertToBool(entry.Value);
						break;

					case "INVOCATIONTIMEOUT":
						this._readParameters[(int) GenuineParameter.InvocationTimeout] = GenuineUtility.SafeConvertToTimeSpan(entry.Value);
						break;

					case "SYNCRESPONSES":
						this._readParameters[(int) GenuineParameter.SyncResponses] = GenuineUtility.SafeConvertToBool(entry.Value);
						break;

						// Common parameters -------------------------------------------------------
					case "CONNECTTIMEOUT":
						this._readParameters[(int) GenuineParameter.ConnectTimeout] = GenuineUtility.SafeConvertToTimeSpan(entry.Value);
						break;

					case "SECURITYSESSIONFORPERSISTENTCONNECTIONS":
						this._readParameters[(int) GenuineParameter.SecuritySessionForPersistentConnections] = entry.Value.ToString();
						break;
					case "SECURITYSESSIONFORNAMEDCONNECTIONS":
						this._readParameters[(int) GenuineParameter.SecuritySessionForNamedConnections] = entry.Value.ToString();
						break;
					case "SECURITYSESSIONFORINVOCATIONCONNECTIONS":
						this._readParameters[(int) GenuineParameter.SecuritySessionForInvocationConnections] = entry.Value.ToString();
						break;

					case "CLOSEPERSISTENTCONNECTIONAFTERINACTIVITY":
						this._readParameters[(int) GenuineParameter.ClosePersistentConnectionAfterInactivity] = GenuineUtility.SafeConvertToTimeSpan(entry.Value);
						break;
					case "CLOSENAMEDCONNECTIONAFTERINACTIVITY":
						this._readParameters[(int) GenuineParameter.CloseNamedConnectionAfterInactivity] = GenuineUtility.SafeConvertToTimeSpan(entry.Value);
						break;
					case "CLOSEINVOCATIONCONNECTIONAFTERINACTIVITY":
						this._readParameters[(int) GenuineParameter.CloseInvocationConnectionAfterInactivity] = GenuineUtility.SafeConvertToTimeSpan(entry.Value);
						break;
					case "CLOSEONEWAYCONNECTIONAFTERINACTIVITY":
						this._readParameters[(int) GenuineParameter.CloseOneWayConnectionAfterInactivity] = GenuineUtility.SafeConvertToTimeSpan(entry.Value);
						break;

					case "PERSISTENTCONNECTIONSENDPINGAFTERINACTIVITY":
						this._readParameters[(int) GenuineParameter.PersistentConnectionSendPingAfterInactivity] = GenuineUtility.SafeConvertToTimeSpan(entry.Value);
						break;

					case "MAXTIMESPANTORECONNECT":
						this._readParameters[(int) GenuineParameter.MaxTimeSpanToReconnect] = GenuineUtility.SafeConvertToTimeSpan(entry.Value);
						break;

					case "RECONNECTIONTRIES":
						this._readParameters[(int) GenuineParameter.ReconnectionTries] = GenuineUtility.SafeConvertToInt32(entry.Value);
						break;

					case "SLEEPBETWEENRECONNECTIONS":
						this._readParameters[(int) GenuineParameter.SleepBetweenReconnections] = GenuineUtility.SafeConvertToTimeSpan(entry.Value);
						break;

//					case "IGNOREREMOTEHOSTURICHANGES":
//						this._readParameters[(int) GenuineParameter.IgnoreRemoteHostUriChanges] = GenuineUtility.SafeConvertToBool(entry.Value);
//						break;

						// TCP parameters ----------------------------------------------------------

					case "TCPMAXSENDSIZE":
						this._readParameters[(int) GenuineParameter.TcpMaxSendSize] = GenuineUtility.SafeConvertToInt32(entry.Value);
						break;

					case "TCPREADREQUESTBEFOREPROCESSING":
						this._readParameters[(int) GenuineParameter.TcpReadRequestBeforeProcessing] = GenuineUtility.SafeConvertToBool(entry.Value);
						break;

					case "TCPDONOTRESENDMESSAGES":
						this._readParameters[(int) GenuineParameter.TcpDoNotResendMessages] = GenuineUtility.SafeConvertToBool(entry.Value);
						break;

					case "TCPDISABLENAGGLING":
						this._readParameters[(int) GenuineParameter.TcpDisableNagling] = GenuineUtility.SafeConvertToBool(entry.Value);
						break;

					case "TCPPREVENTDELAYEDACK":
						this._readParameters[(int) GenuineParameter.TcpPreventDelayedAck] = GenuineUtility.SafeConvertToBool(entry.Value);
						break;

					case "TCPRECEIVEBUFFERSIZE":
						this._readParameters[(int) GenuineParameter.TcpReceiveBufferSize] = GenuineUtility.SafeConvertToInt32(entry.Value);
						break;

					case "TCPSENDBUFFERSIZE":
						this._readParameters[(int) GenuineParameter.TcpSendBufferSize] = GenuineUtility.SafeConvertToInt32(entry.Value);
						break;

						// Shared memory -----------------------------------------------------------
					case "SMSHARESIZE":
						this._readParameters[(int) GenuineParameter.SMShareSize] = GenuineUtility.SafeConvertToInt32(entry.Value);
						break;
					case "SMSENDTIMEOUT":
						this._readParameters[(int) GenuineParameter.SMSendTimeout] = GenuineUtility.SafeConvertToTimeSpan(entry.Value);
						break;
					case "SMSESSIONLOCAL":
						this._readParameters[(int) GenuineParameter.SMSessionLocal] = GenuineUtility.SafeConvertToBool(entry.Value);
						break;

						// UDP transport -----------------------------------------------------------
					case "UDPJOINTO":
						this._readParameters[(int) GenuineParameter.UdpJoinTo] = entry.Value.ToString();
						break;
					case "UDPTTL":
						this._readParameters[(int) GenuineParameter.UdpTtl] = GenuineUtility.SafeConvertToInt32(entry.Value);
						break;
					case "UDPRECEIVEBUFFER":
						this._readParameters[(int) GenuineParameter.UdpReceiveBuffer] = GenuineUtility.SafeConvertToInt32(entry.Value);
						break;
					case "UDPPACKETSIZE":
						this._readParameters[(int) GenuineParameter.UdpPacketSize] = GenuineUtility.SafeConvertToInt32(entry.Value);
						break;
					case "UDPMTU":
						this._readParameters[(int) GenuineParameter.UdpMtu] = GenuineUtility.SafeConvertToInt32(entry.Value);
						break;
					case "UDPMULTICASTTO":
						this._readParameters[(int) GenuineParameter.UdpMulticastTo] = entry.Value.ToString();
						break;

					case "UDPASSEMBLETIMESPAN":
						this._readParameters[(int) GenuineParameter.UdpAssembleTimeSpan] = GenuineUtility.SafeConvertToTimeSpan(entry.Value);
						break;

						// HTTP transport ----------------------------------------------------------
					case "HTTPPROXYURI":
						this._readParameters[(int) GenuineParameter.HttpProxyUri] = entry.Value.ToString();
						break;
					case "HTTPWEBUSERAGENT":
						this._readParameters[(int) GenuineParameter.HttpWebUserAgent] = entry.Value.ToString();
						break;
					case "HTTPAUTHUSERNAME":
						this._readParameters[(int) GenuineParameter.HttpAuthUserName] = entry.Value.ToString();
						break;
					case "HTTPAUTHPASSWORD":
						this._readParameters[(int) GenuineParameter.HttpAuthPassword] = entry.Value.ToString();
						break;
					case "HTTPAUTHDOMAIN":
						this._readParameters[(int) GenuineParameter.HttpAuthDomain] = entry.Value.ToString();
						break;

					case "HTTPAUTHCREDENTIAL":
						this._readParameters[(int) GenuineParameter.HttpAuthCredential] = entry.Value as NetworkCredential;
						break;

					case "HTTPUSEGLOBALPROXY":
						this._readParameters[(int) GenuineParameter.HttpUseGlobalProxy] = GenuineUtility.SafeConvertToBool(entry.Value);
						break;
					case "HTTPBYPASSONLOCAL":
						this._readParameters[(int) GenuineParameter.HttpBypassOnLocal] = GenuineUtility.SafeConvertToBool(entry.Value);
						break;
					case "HTTPUSEDEFAULTCREDENTIALS":
						this._readParameters[(int) GenuineParameter.HttpUseDefaultCredentials] = GenuineUtility.SafeConvertToBool(entry.Value);
						break;
					case "HTTPALLOWWRITESTREAMBUFFERING":
						this._readParameters[(int) GenuineParameter.HttpAllowWriteStreamBuffering] = GenuineUtility.SafeConvertToBool(entry.Value);
						break;
					case "HTTPUNSAFECONNECTIONSHARING":
						this._readParameters[(int) GenuineParameter.HttpUnsafeConnectionSharing] = GenuineUtility.SafeConvertToBool(entry.Value);
						break;

					case "HTTPRECOMMENDEDPACKETSIZE":
						this._readParameters[(int) GenuineParameter.HttpRecommendedPacketSize] = GenuineUtility.SafeConvertToInt32(entry.Value);
						break;

					case "HTTPKEEPALIVE":
						this._readParameters[(int) GenuineParameter.HttpKeepAlive] = GenuineUtility.SafeConvertToBool(entry.Value);
						break;

					case "HTTPAUTHENTICATION":
						this._readParameters[(int) GenuineParameter.HttpAuthentication] = GenuineUtility.SafeConvertToBool(entry.Value);
						break;

					case "HTTPSTOREANDPROVIDEHTTPCONTEXT":
						this._readParameters[(int) GenuineParameter.HttpStoreAndProvideHttpContext] = GenuineUtility.SafeConvertToBool(entry.Value);
						break;

					case "HTTPWEBREQUESTINITIATIONTIMEOUT":
						this._readParameters[(int) GenuineParameter.HttpWebRequestInitiationTimeout] = GenuineUtility.SafeConvertToTimeSpan(entry.Value);
						break;

					case "HTTPASYNCHRONOUSREQUESTTIMEOUT":
						this._readParameters[(int) GenuineParameter.HttpAsynchronousRequestTimeout] = GenuineUtility.SafeConvertToTimeSpan(entry.Value);
						break;

					case "HTTPMIMEMEDIATYPE":
						this._readParameters[(int) GenuineParameter.HttpMimeMediaType] = entry.Value.ToString();
						break;

						// XHTTP transport ---------------------------------------------------------
					case "XHTTPREADHEADERTIMEOUT":
						this._readParameters[(int) GenuineParameter.XHttpReadHttpMessageTimeout] = GenuineUtility.SafeConvertToTimeSpan(entry.Value);
						break;

						// Security Session---------------------------------------------------------
					case "HOLDTHREADDURINGSECURITYSESSIONESTABLISHING":
						this._readParameters[(int) GenuineParameter.HoldThreadDuringSecuritySessionEstablishing] = GenuineUtility.SafeConvertToBool(entry.Value);
						break;


						// Logging -----------------------------------------------------------------
					case "ENABLEGLOBALLOGGINGTOMEMORY":
						this._readParameters[(int) GenuineParameter.EnableGlobalLoggingToMemory] = GenuineUtility.SafeConvertToInt32(entry.Value);
						break;

					case "ENABLEGLOBALLOGGINGTOFILE":
						this._readParameters[(int) GenuineParameter.EnableGlobalLoggingToFile] = entry.Value.ToString();
						break;

					case "LOGGINGPARAMETERS":
						this._readParameters[(int) GenuineParameter.LoggingParameters] = entry.Value.ToString();
						break;

						// Versioning --------------------------------------------------------------
					case "COMPATIBILITYLEVEL":
						this._readParameters[(int) GenuineParameter.CompatibilityLevel] = GenuineUtility.SafeConvertToInt32(entry.Value);
						break;

					default:
//						GenuineLoggingServices.BinaryLogWriter.Log(LogMessageCategory.Notification, null, "ReadingCascadeParameterProvider.ReadingCascadeParameterProvider",
//							null, "An unknown parameter was found. Parameter name: {0}. Parameter value: {1}. If this parameter is not processed on the channel level, it will be ignored.", entry.Key.ToString(), entry.Value.ToString());
						break;
				}
			}
		}

		/// <summary>
		/// Underlying transport parameters.
		/// </summary>
		public IParameterProvider UnderlyingProvider
		{
			get
			{
				lock (this._underlyingProviderLock)
					return this._underlyingProvider;
			}
			set
			{
				lock (this._underlyingProviderLock)
					this._underlyingProvider = value;
			}
		}
		private IParameterProvider _underlyingProvider;
		private object _underlyingProviderLock = new object();

		/// <summary>
		/// Read parameters.
		/// </summary>
		private object[] _readParameters;

		/// <summary>
		/// Gets a parameter's value.
		/// </summary>
		/// <param name="genuineParameter">The name of the parameter.</param>
		/// <returns>The value of the parameter.</returns>
		public object this[GenuineParameter genuineParameter] 
		{ 
			get
			{
				int parameterIndex = (int) genuineParameter;
				if (this._readParameters[parameterIndex] != null)
					return this._readParameters[parameterIndex];

				return this._underlyingProvider[genuineParameter];
			}
			set
			{
				this._readParameters[(int) genuineParameter] = value;
			}
		}

	}
}
