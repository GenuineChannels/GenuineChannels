# GenuineChannels Change Log

## v2.5.9.10 (work in progress)

This version no longer supports .NET Framework 3.0 and below. The minimum supported version is .NET 3.5.

### Implementation enhancements:

* IPv6 support (.NET Framework version 3.5 and 4.6 only)
* New parameter **`GenuineParameter.TcpReuseAddressPort`** (bool).  
  Indicates whether to use TCP port sharing. If set the listener port will be shared (other apps can use the same one).
  In this case the socket option `System.Net.Sockets.SocketOptionName.ReuseAddress` will be applied. See [MSDN Doc](https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.socketoptionname?f1url=https%3A%2F%2Fmsdn.microsoft.com%2Fquery%2Fdev15.query%3FappId%3DDev15IDEF1%26l%3DEN-US%26k%3Dk(System.Net.Sockets.SocketOptionName.ReuseAddress);k(vs.objectbrowser);k(TargetFrameworkMoniker-.NETFramework,Version%3Dv4.6)%26rd%3Dtrue&view=netframework-4.7.1 "MSDN Doc").
  Default value is **false**.
* New parameter **`GenuineParameter.TcpDualSocketMode`** (bool).  
  The TCP dual socket mode will enable both IPv4 and IPv6 for the socket listener (Vista and Longhorn above only).
  This will be **true** by default. If you like to force IPv4 or IPv6 listening only, set this option to false.

### ICSharpCode.SharpZipLib.dll:

* Using built-in compression classes instead of the external ICSharpCode.SharpZipLib.dll library for traffic compression.

### BinaryFormatter security fixes:

* Deserialization of dangerous delegates (i.e., `Process.Start`, `File.Delete`, etc).
* Unsafe deserialization of `DataSet` and `WindowsIdentity` classes.
* See [Zyan.SafeDeserializationHelpers](https://github.com/zyanfx/SafeDeserializationHelpers) and [ysoserial.net](https://github.com/pwntester/ysoserial.net) for more details.

## v2.5.9.9

* The initial release of the MIT-licensed open-source version of GenuineChannels.
