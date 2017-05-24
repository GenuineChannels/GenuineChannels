using Belikov.GenuineChannels.DotNetRemotingLayer;
using Belikov.GenuineChannels.GenuineHttp;
using Belikov.GenuineChannels.GenuineTcp;
using KnownObjects;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Client
{


    class ChatClient : MarshalByRefObject, IMessageReceiver
    {
        /// <summary>
        /// The only instance.
        /// </summary>
        public static ChatClient Instance = new ChatClient();

        /// <summary>
        /// Nickname.
        /// </summary>
        public static string Nickname;

        /// <summary>
        /// Chat room.
        /// </summary>
        public static IChatRoom IChatRoom;

        /// <summary>
        /// A proxy to server business object.
        /// </summary>
        public static IChatServer IChatServer;

        /// <summary>
        /// To provide thread-safe access to ChatClient.IChatServer member.
        /// </summary>
        public static object IChatServerLock = new object();

        /// <summary>
        /// URI of GC Server, varies with protocol
        /// </summary>     
        private static string RemoteHostUri = "NA";

        static void Main(string[] args)
        {

            enumGC_Mode GC_Mode = enumGC_Mode.GC_HTTP;
            // setup .NET Remoting
            Console.WriteLine("Configuring Remoting environment...");
            //System.Configuration.ConfigurationSettings.GetConfig("DNS");
            GenuineGlobalEventProvider.GenuineChannelsGlobalEvent += new GenuineChannelsGlobalEventHandler(GenuineChannelsEventHandler);

            IDictionary props = new Hashtable();
            switch (GC_Mode)
            {
                case enumGC_Mode.GC_TCP:
                    Console.WriteLine("GC_TCP Client connecting to Server...");
                    GenuineTcpChannel channelTCP = new GenuineTcpChannel(props, null, null);
                    ChannelServices.RegisterChannel(channelTCP, false);

                    RemoteHostUri = "gtcp://127.0.0.1:8737";
                    ChatClient.Nickname = "GC_TCP_ChatClient";
                    break;
                case enumGC_Mode.GC_HTTP:
                    Console.WriteLine("GC_HTTP Client connecting to Server...");
                    props["name"] = "ghttp";
                    props["priority"] = "100";
                    GenuineHttpClientChannel channelHttp = new GenuineHttpClientChannel(props, null, null);
                    ChannelServices.RegisterChannel(channelHttp, false);

                    RemoteHostUri = "ghttp://localhost:49834"; // Note the full address will be determined by the server
                    ChatClient.Nickname = "GC_HTTP_ChatClient";
                    break;
            }

            // bind client's receiver
            RemotingServices.Marshal(ChatClient.Instance, "MessageReceiver.rem");
            for (;;)
            {
                try
                {
                    // subscribe to the chat event
                    lock (ChatClient.IChatServerLock)
                    {
                        ChatClient.IChatServer = (IChatServer)Activator.GetObject(typeof(IChatRoom),
                            RemoteHostUri + "/ChatServer.rem");
                        ChatClient.IChatRoom = ChatClient.IChatServer.EnterToChatRoom(ChatClient.Nickname);
                    }

                    for (;;)
                    {
                        Console.WriteLine("Enter a message to send or an empty string to exit.");

                        string str = Console.ReadLine();
                        if (str.Length <= 0)
                            return;

                        ChatClient.IChatRoom.SendMessage(str);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("\r\n\r\n---Exception: {0}. Stack trace: {1}.", ex.Message, ex.StackTrace);
                }

                Console.WriteLine("Next attempt to connect to the server will be in 3 seconds.");
                Thread.Sleep(3000);
            }
        }

        public static void GenuineChannelsEventHandler(object sender, GenuineEventArgs e)
        {
            if (e.SourceException == null)
                Console.WriteLine("\r\n\r\n---Global event: {0}\r\nRemote host: {1}",
                    e.EventType,
                    e.HostInformation == null ? "<unknown>" : e.HostInformation.ToString());
            else
                Console.WriteLine("\r\n\r\n---Global event: {0}\r\nRemote host: {1}\r\nException: {2}",
                    e.EventType,
                    e.HostInformation == null ? "<unknown>" : e.HostInformation.ToString(),
                    e.SourceException);

            if (e.EventType == GenuineEventType.GeneralServerRestartDetected)
            {
                // server has been restarted so it does not know that we have been subscribed to
                // messages and ours nickname
                lock (ChatClient.IChatServerLock)
                {
                    ChatClient.IChatServer = (IChatServer)Activator.GetObject(typeof(IChatRoom),
                        RemoteHostUri + "/ChatServer.rem");
                    ChatClient.IChatRoom = ChatClient.IChatServer.EnterToChatRoom(ChatClient.Nickname);
                }
            }
        }

        /// <summary>
        /// Message receiver.
        /// It receives messages async and writes them separately from the main thread.
        /// But it does not matter for console application.
        /// </summary>
        /// <param name="message">The message.</param>
        public object ReceiveMessage(string message, string nickname)
        {
            Console.WriteLine("Message \"{0}\" from \"{1}\".", message, nickname);
            return null;
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
