# GenuineChannels Change Log

## v2.5.9.10 (work in progress)


### Implementation enhancements:

* IPv6 support (.NET framework version 3.5 and 4.6 only)
* new parameter **`GenuineParameter.TcpReuseAddressPort`**.
	  [BOOL] Indicates whether to use TCP port sharing. If set the listener port will be shared (other apps can use the same one).
        In this case the socket option `System.Net.Sockets.SocketOptionName.ReuseAddress` will be applied. See [MSDN Doc](https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.socketoptionname?f1url=https%3A%2F%2Fmsdn.microsoft.com%2Fquery%2Fdev15.query%3FappId%3DDev15IDEF1%26l%3DEN-US%26k%3Dk(System.Net.Sockets.SocketOptionName.ReuseAddress);k(vs.objectbrowser);k(TargetFrameworkMoniker-.NETFramework,Version%3Dv4.6)%26rd%3Dtrue&view=netframework-4.7.1 "MSDN Doc").
        Default value is **false**.
* new parameter **`GenuineParameter.TcpDualSocketMode`**.
	  [BOOL] The TCP dual socket mode will enable both IPv4 and IPv6 for the socket listener (Vista and Longhorn above only).
      This will be **true** by default. If you like to force IPv4 or IPv6 listening only, set this option to false.


## v2.5.9.9 (initial github release)
