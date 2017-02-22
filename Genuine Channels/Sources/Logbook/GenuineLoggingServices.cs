/* Genuine Channels product.
 *
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 *
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Runtime.Remoting;
using System.Security;

namespace Belikov.GenuineChannels.Logbook
{
	/// <summary>
	/// Provides a set of methods allowing to set up logging objects.
	/// </summary>
	public class GenuineLoggingServices
	{
		/// <summary>
		/// It's a singleton.
		/// </summary>
		private GenuineLoggingServices()
		{
		}

		/// <summary>
		/// The binary log writer that puts down debug information.
		/// </summary>
		public static BinaryLogWriter BinaryLogWriter
		{
			get
			{
				lock (_accessToLocalMembers)
					return _logger;
			}
			set
			{
				lock (_accessToLocalMembers)
				{
					_logger = value;
				}
			}
		}
		private static BinaryLogWriter _logger = null;

		/// <summary>
		/// Gets an object that can be used to synchronize access to members of GenuineLoggicServices class.
		/// </summary>
		public static object SyncRoot
		{
			get
			{
				return _accessToLocalMembers;
			}
		}

		/// <summary>
		/// To guarantee atomic access to local members.
		/// </summary>
		private static object _accessToLocalMembers = new object();

		/// <summary>
		/// Gets a string specifying the logging options used by default.
		/// </summary>
		public const string DefaultLoggingOptions = "C1E1M2I3S1B2T1X1H1A1D2V1L0N1";

		/// <summary>
		/// Gets a string indicating the name of the SES Key Provider that is used for encrypting log records while
		/// sending them over a network.
		/// </summary>
		public const string NameOfLoggingSES = "/GenuineChannels/__LoggingSES";

		/// <summary>
		/// The object URI of the service providing logging records remotely if MemoryWritingStream is used.
		/// </summary>
		public const string RemoteLogServiceObjectUri = "__GenuineChannels__RemoteLogServiceObjectUri.rem";

		/// <summary>
		/// The name of the Security Key Provider used to download log information.
		/// </summary>
		public const string LoggingSESSPName = "/__GC/Logging/SES";

		/// <summary>
		/// Stops putting down log records.
		/// </summary>
		public static void StopLogging()
		{
			lock (_accessToLocalMembers)
			{
				GenuineLoggingServices.BinaryLogWriter = null;

				if (_memoryWritingWriter != null)
				{
					RemotingServices.Disconnect(_remoteLogService);
					_remoteLogService.StopLogging();
					_remoteLogService = null;

					_memoryWritingStream = null;
					_memoryWritingWriter = null;
				}
			}
		}

		/// <summary>
		/// Gets a service that provides the content of the log via .NET Remoting and DXM.
		/// </summary>
		public static RemoteLogService RemoteLogService
		{
			get
			{
				lock (_accessToLocalMembers)
				{
					return _remoteLogService;
				}
			}
		}
		private static RemoteLogService _remoteLogService;
		private static MemoryWritingStream _memoryWritingStream = null;
		private static BinaryLogWriter _memoryWritingWriter = null;

		/// <summary>
		/// Creates and starts a binary log writer putting records down to the memory stream, which can be obtained remotely.
		/// </summary>
		/// <param name="maximumMemorySize">The maximum possible size of memory space occupied by log records.</param>
		/// <param name="logOptions">The options specifying what information will be saved into the log.</param>
		[SecuritySafeCritical]
		public static void SetUpLoggingToMemory(int maximumMemorySize, string logOptions)
		{
			lock (_accessToLocalMembers)
			{
				if (_memoryWritingWriter == null)
				{
					_memoryWritingStream = new MemoryWritingStream(maximumMemorySize);
					_memoryWritingWriter = new BinaryLogWriter(_memoryWritingStream);

					SetUpLoggingOptionsFromString(_memoryWritingWriter, GenuineLoggingServices.DefaultLoggingOptions);
					SetUpLoggingOptionsFromString(_memoryWritingWriter, logOptions);

					_remoteLogService = new RemoteLogService(_memoryWritingStream);
					RemotingServices.Marshal(_remoteLogService, GenuineLoggingServices.RemoteLogServiceObjectUri);
					Belikov.GenuineChannels.DirectExchange.DirectExchangeManager.RegisterGlobalServerService(GenuineLoggingServices.RemoteLogServiceObjectUri, _remoteLogService);
					Belikov.GenuineChannels.Security.SecuritySessionServices.SetGlobalKey(GenuineLoggingServices.LoggingSESSPName, new Belikov.GenuineChannels.Security.KeyProvider_SelfEstablishingSymmetric());
				}

				GenuineLoggingServices.BinaryLogWriter = _memoryWritingWriter;
			}
		}

		/// <summary>
		/// Creates and starts a binary log writer putting records down to the specified file.
		/// </summary>
		/// <param name="baseFileName">The basic part of the file name.</param>
		/// <param name="logOptions">The options specifying what information will be saved into the log.</param>
		public static void SetUpLoggingToFile(string baseFileName, string logOptions)
		{
			SetUpLoggingToFile(baseFileName, logOptions, true);
		}

		/// <summary>
		/// Creates and starts a binary log writer putting records down to the specified file. The name
		/// of the file will not be modified if addSuffixToBaseFileName is false.
		/// </summary>
		/// <param name="baseFileName">The basic part of the file name.</param>
		/// <param name="logOptions">The options specifying what information will be saved into the log.</param>
		/// <param name="addSuffixToBaseFileName">true to add suffix (date, time, and extension) to the file name.</param>
		public static void SetUpLoggingToFile(string baseFileName, string logOptions, bool addSuffixToBaseFileName)
		{
			lock (_accessToLocalMembers)
			{
				BinaryLogWriter binaryLogWriter = new BinaryLogWriter(new FileWritingStream(baseFileName, addSuffixToBaseFileName));
				SetUpLoggingOptionsFromString(binaryLogWriter, GenuineLoggingServices.DefaultLoggingOptions);
				SetUpLoggingOptionsFromString(binaryLogWriter, logOptions);
				GenuineLoggingServices.BinaryLogWriter = binaryLogWriter;
			}
		}

		/// <summary>
		/// Initializes the specified BinaryLogWriter with the logging options from the specified string.
		/// </summary>
		/// <param name="binaryLogWriter">The BinaryLogWriter.</param>
		/// <param name="logOptions">The logging options.</param>
		public static void SetUpLoggingOptionsFromString(BinaryLogWriter binaryLogWriter, string logOptions)
		{
			if (logOptions == null || logOptions.Length <= 0)
				return ;

			for ( int i = 0; i < logOptions.Length; i+= 2)
			{
				if (logOptions.Length - i < 2 || ! Char.IsDigit(logOptions[i+1]))
					continue;

				int level = (int) ( (short) logOptions[i+1] - '0' );

				switch (Char.ToUpper(logOptions[i]))
				{
					case 'C':
						binaryLogWriter[LogCategory.Connection] = level;
						break;

					case 'E':
						binaryLogWriter[LogCategory.ChannelEvent] = level;
						break;

					case 'M':
						binaryLogWriter[LogCategory.MessageProcessing] = level;
						break;

					case 'I':
						binaryLogWriter[LogCategory.ImplementationWarning] = level;
						break;

					case 'S':
						binaryLogWriter[LogCategory.Security] = level;
						break;

					case 'B':
						binaryLogWriter[LogCategory.BroadcastEngine] = level;
						break;

					case 'T':
						binaryLogWriter[LogCategory.Transport] = level;
						break;

					case 'X':
						binaryLogWriter[LogCategory.DXM] = level;
						break;

					case 'H':
						binaryLogWriter[LogCategory.HostInformation] = level;
						break;

					case 'A':
						binaryLogWriter[LogCategory.AcceptingConnection] = level;
						break;

					case 'D':
						binaryLogWriter[LogCategory.Debugging] = level;
						break;

					case 'V':
						binaryLogWriter[LogCategory.Version] = level;
						break;

					case 'L':
						binaryLogWriter[LogCategory.LowLevelTransport] = level;
						break;

					case 'N':
						binaryLogWriter[LogCategory.StatisticCounters] = level;
						break;
				}
			}
		}

	}
}
