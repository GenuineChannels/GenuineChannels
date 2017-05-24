using Belikov.GenuineChannels;
using Belikov.GenuineChannels.DotNetRemotingLayer;
using KnownObjects;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;

namespace Server.GC_Code
{
    public class ChatServer : MarshalByRefObject, IChatServer
    {
        public static ChatRoom GlobalRoom = new ChatRoom();

        internal ChatServer()
        {
            GenuineGlobalEventProvider.GenuineChannelsGlobalEvent += new GenuineChannelsGlobalEventHandler(GenuineChannelsEventHandler);

        }
        /// <summary>
        /// Catches Genuine Channels events and removes client session when
        /// user disconnects.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public static void GenuineChannelsEventHandler(object sender, GenuineEventArgs e)
        {
            if (e.SourceException == null)
                Debug.WriteLine("\r\n\r\n---Global event: {0}\r\nRemote host: {1}",
                    e.EventType,
                    e.HostInformation == null ? "<unknown>" : e.HostInformation.ToString());
            else
                Debug.WriteLine("\r\n\r\n---Global event: {0}\r\nRemote host: {1}\r\nException: {2}",
                    e.EventType,
                    e.HostInformation == null ? "<unknown>" : e.HostInformation.ToString(),
                    e.SourceException);

            if (e.EventType == GenuineEventType.GeneralConnectionClosed)
            {
                // the client disconnected
                string nickname = e.HostInformation["Nickname"] as string;
                if (nickname != null)
                    Debug.WriteLine("Client \"{0}\" has been disconnected.", nickname);
            }
        }

        public IChatRoom EnterToChatRoom(string nickname)
        {
            GlobalRoom.AttachClient(nickname);
            GenuineUtility.CurrentSession["Nickname"] = nickname;
            return GlobalRoom;
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