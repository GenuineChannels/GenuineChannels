using System;
using System.Collections;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Lifetime;
using System.Runtime.Remoting.Messaging;

using KnownObjects;

namespace Server
{
	/// <summary>
	/// Configures Natice TCP Channel and implements chat server behavior.
	/// </summary>
	class ChatServer : MarshalByRefObject, IChatRoom
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main(string[] args)
		{
			try
			{
				// setup remoting
				System.Configuration.ConfigurationSettings.GetConfig("DNS");
				RemotingConfiguration.Configure("Server.exe.config");

				// bind the server
				RemotingServices.Marshal(new ChatServer(), "ChatRoom.rem");

				Console.WriteLine("Server has been started. Press enter to exit.");
				Console.ReadLine();
			}
			catch(Exception ex)
			{
				Console.WriteLine("Exception: {0}. Stack trace: {1}.", ex.Message, ex.StackTrace);
			}
		}

		/// <summary>
		/// Contains entries of MBR uri => client MBR implementing IChatClient interface.
		/// </summary>
		static Hashtable _clients = new Hashtable();

		/// <summary>
		/// Attaches the client.
		/// </summary>
		/// <param name="iChatClient">Client to be attached.</param>
		public void AttachClient(IChatClient iChatClient)
		{
			if (iChatClient == null)
				return ;

			lock(_clients)
			{
				_clients[RemotingServices.GetObjectUri((MarshalByRefObject) iChatClient)] = iChatClient;
			}
		}

		/// <summary>
		/// To initiate an async call.
		/// </summary>
		public delegate object ReceiveMessageEventHandler(string message);

		/// <summary>
		/// Sends the message to all clients.
		/// </summary>
		/// <param name="message">Message to be sent.</param>
		/// <returns>Number of clients having received this message.</returns>
		public void SendMessage(string message)
		{
			lock(_clients)
			{
				Console.WriteLine("\"{0}\" message will be sent to all clients.", message);
				AsyncCallback asyncCallback = new AsyncCallback(OurAsyncCallbackHandler);

				foreach (DictionaryEntry entry in _clients)
				{
					IChatClient iChatClient = (IChatClient) entry.Value;
					ReceiveMessageEventHandler remoteAsyncDelegate = new ReceiveMessageEventHandler(iChatClient.ReceiveMessage);

					AsyncCallBackData asyncCallBackData = new AsyncCallBackData();
					asyncCallBackData.RemoteAsyncDelegate = remoteAsyncDelegate;
					asyncCallBackData.MbrBeingCalled = (MarshalByRefObject) iChatClient;

					IAsyncResult RemAr = remoteAsyncDelegate.BeginInvoke(message, asyncCallback, asyncCallBackData);
				}
			}		
		}

		private class AsyncCallBackData
		{
			/// <summary>
			/// Source delegate to finish async call.
			/// </summary>
			public ReceiveMessageEventHandler RemoteAsyncDelegate;

			/// <summary>
			/// The object being called.
			/// </summary>
			public MarshalByRefObject MbrBeingCalled;
		}

		/// <summary>
		/// Called by .NET Remoting when an async call is finished.
		/// </summary>
		/// <param name="ar"></param>
		public static void OurAsyncCallbackHandler(IAsyncResult ar)
		{
			AsyncCallBackData asyncCallBackData = (AsyncCallBackData) ar.AsyncState;

			try
			{
				object result = asyncCallBackData.RemoteAsyncDelegate.EndInvoke(ar);
			}
			catch(Exception ex)
			{
				Console.WriteLine("Client call failed: {0}.", ex.Message);

				// remove the failed client from our list
				lock(_clients)
				{
					_clients.Remove( RemotingServices.GetObjectUri(asyncCallBackData.MbrBeingCalled) );
				}
			}
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
