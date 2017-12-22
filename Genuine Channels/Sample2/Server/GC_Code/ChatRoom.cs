using Belikov.GenuineChannels;
using Belikov.GenuineChannels.BroadcastEngine;
using KnownObjects;
using Server.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Web;

namespace Server.GC_Code
{
    public class ChatRoom : MarshalByRefObject, IChatRoom
    {
        /// <summary>
        /// Constructs ChatRoom instance.
        /// </summary>
        public ChatRoom()
        {
            // bind server's methods
            this._dispatcher.BroadcastCallFinishedHandler += new BroadcastCallFinishedHandler(this.BroadcastCallFinishedHandler);
            this._dispatcher.CallIsAsync = true;
        }

        /// <summary>
        /// Chat members.
        /// </summary>
        private Dispatcher _dispatcher = new Dispatcher(typeof(IMessageReceiver));

        /// <summary>
        /// Attaches the client.
        /// </summary>
        /// <param name="nickname">Nickname.</param>
        public void AttachClient(string nickname)
        {
            string receiverUri = GenuineUtility.FetchCurrentRemoteUri() + "/MessageReceiver.rem";
            IMessageReceiver iMessageReceiver = (IMessageReceiver)Activator.GetObject(typeof(IMessageReceiver), receiverUri);
            this._dispatcher.Add((MarshalByRefObject)iMessageReceiver);

            GenuineUtility.CurrentSession["Nickname"] = nickname;
            Debug.WriteLine("Client with nickname \"{0}\" has been registered.", nickname);
        }

        /// <summary>
        /// Sends message to all clients.
        /// </summary>
        /// <param name="message">Message to send.</param>
        /// <returns>Number of clients having received this message.</returns>
        public void SendMessage(string message)
        {
            // fetch the nickname
            string nickname = GenuineUtility.CurrentSession["Nickname"] as string;
            Debug.WriteLine("Message \"{0}\" will be sent to all clients from {1}.", message, nickname);

            IMessageReceiver iMessageReceiver = (IMessageReceiver)this._dispatcher.TransparentProxy;
            iMessageReceiver.ReceiveMessage(message, nickname);
        }

        /// <summary>
        /// Called by broadcast dispatcher when all calls are performed.
        /// Does not undertake any actions.
        /// </summary>
        /// <param name="dispatcher">Source dipatcher.</param>
        /// <param name="message">Source message.</param>
        /// <param name="resultCollector">Call results.</param>
        public void BroadcastCallFinishedHandler(Dispatcher dispatcher, IMessage message, ResultCollector resultCollector)
        {
        }

    }
}