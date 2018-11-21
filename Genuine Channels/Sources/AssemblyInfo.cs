/* Genuine Channels product.
 *
 * Copyright (c) 2002-2007 Dmitry Belikov. All rights reserved.
 *
 * This source code comes under and must be used and distributed according to the Genuine Channels license agreement.
 */

using System;
using System.Reflection;
using System.Security;
using System.Runtime.CompilerServices;

//
// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
//
#if TRIAL
[assembly: AssemblyTitle("Genuine Channels v2.5.9 Trial")]
#else
[assembly: AssemblyTitle("Genuine Channels v2.5.9")]
#endif
[assembly: AssemblyDescription("Genuine Channels v2.5.9 is an extension pack for .Net Remoting. NAT, FireWall and proxy-friendly GTCP, GHTTP, GXHTTP, Shared Memory channels and the GUDP channel with IP multicasting support. Visit us at www.genuinechannels.com.")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Genrix Software, Inc.")]
[assembly: AssemblyProduct("Genuine Channels")]
[assembly: AssemblyCopyright("Copyright (c) 2002-2007 Genrix Software, Inc. All rights reserved.")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

//
// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Revision and Build Numbers
// by using the '*' as shown below:

// Keep this as is until v3.0 is published, then change to "3.0.0.0"
[assembly: AssemblyVersion("2.5.9.9")]

// Keep this in sync with the Nuget package version
[assembly: AssemblyFileVersion("2.5.9.11")]

//
// In order to sign your assembly you must specify a key to use. Refer to the
// Microsoft .NET Framework documentation for more information on assembly signing.
//
// Use the attributes below to control which key is used for signing.
//
// Notes:
//   (*) If no key is specified, the assembly is not signed.
//   (*) KeyName refers to a key that has been installed in the Crypto Service
//       Provider (CSP) on your machine. KeyFile refers to a file which contains
//       a key.
//   (*) If the KeyFile and the KeyName values are both specified, the
//       following processing occurs:
//       (1) If the KeyName can be found in the CSP, that key is used.
//       (2) If the KeyName does not exist and the KeyFile does exist, the key
//           in the KeyFile is installed into the CSP and used.
//   (*) In order to create a KeyFile, you can use the sn.exe (Strong Name) utility.
//       When specifying the KeyFile, the location of the KeyFile should be
//       relative to the project output directory which is
//       %Project Directory%\obj\<configuration>. For example, if your KeyFile is
//       located in the project directory, you would specify the AssemblyKeyFile
//       attribute as [assembly: AssemblyKeyFile("..\\..\\mykey.snk")]
//   (*) Delay Signing is an advanced option - see the Microsoft .NET Framework
//       documentation for more information on this.
//
//[assembly: AssemblyDelaySign(false)]
// RELEASE TODO: not forget about snk
[assembly: AssemblyKeyFile(@"GenuineChannels.snk")]
//[assembly: AssemblyKeyName("")]

[assembly: AllowPartiallyTrustedCallers]

#if FRM40
[assembly: System.Security.SecurityRules(System.Security.SecurityRuleSet.Level1)]
#endif