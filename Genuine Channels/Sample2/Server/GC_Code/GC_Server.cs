using Belikov.GenuineChannels.GenuineHttp;
using Belikov.GenuineChannels.GenuineTcp;
using KnownObjects;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Web;

namespace Server.GC_Code
{
    internal static class GC_Server
    {
        internal static void StartServer(enumGC_Mode GC_Mode)
        {
            try
            {
                IDictionary props = new Hashtable();
                switch (GC_Mode)
                {
                    case enumGC_Mode.GC_TCP:
                        Debug.WriteLine(" Starting GC_TCP Server");
                        props["name"] = "GTCP1";
                        props["priority"] = "100";
                        props["port"] = "8737";
                        // Null entries specify the default formatters. 
                        GenuineTcpChannel channelTCP = new GenuineTcpChannel(props, null, null);
                        ChannelServices.RegisterChannel(channelTCP, false);
                        break;
                    case enumGC_Mode.GC_HTTP:
                        Debug.WriteLine(" Starting GC_HTTP Server");
                        props["name"] = "ghttp";
                        props["priority"] = "100";
                        // Null entries specify the default formatters. 
                        GenuineHttpServerChannel channelHttp = new GenuineHttpServerChannel(props, null, null);
                        ChannelServices.RegisterChannel(channelHttp, false);
                        break;

                }
                // bind the server
                RemotingServices.Marshal(new ChatServer(), "ChatServer.rem");
                Debug.WriteLine("Server has been started.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception: {0}. Stack trace: {1}.", ex.Message, ex.StackTrace);
            }
        }

    }
}