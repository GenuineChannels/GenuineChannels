Imports System
Imports System.Configuration
Imports System.Runtime.Remoting
Imports System.Threading

Imports KnownObjects
Imports Belikov.GenuineChannels
Imports Belikov.GenuineChannels.BroadcastEngine
Imports Belikov.GenuineChannels.DotNetRemotingLayer
Imports Belikov.GenuineChannels.Logbook

Module Chat

    Sub Main()
        ChatClient.Main()
    End Sub

    ''' <summary>
    ''' ChatClient demostrates simple client application.
    ''' </summary>
    Class ChatClient
        Inherits MarshalByRefObject
        Implements IMessageReceiver

        ''' <summary>
        ''' The only instance.
        ''' </summary>
        Public Shared Instance As ChatClient = New ChatClient()

        ''' <summary>
        ''' Nickname.
        ''' </summary>
        Public Shared Nickname As String

        ''' <summary>
        ''' Chat room.
        ''' </summary>
        Public Shared IChatRoom As IChatRoom

        ''' <summary>
        ''' A proxy to server business object.
        ''' </summary>
        Public Shared IChatServer As IChatServer

        ''' <summary>
        ''' To provide thread-safe access to ChatClient.IChatServer member.
        ''' </summary>
        Public Shared IChatServerLock As Object = New Object()

        ''' <summary>
        ''' The main entry point for the application.
        ''' </summary>
        Public Shared Sub Main()
            '' wait for the server
            Console.WriteLine("Sleep for 3 seconds.")
            Thread.Sleep(TimeSpan.FromSeconds(3))

            '' setup .NET Remoting
            Console.WriteLine("Configuring Remoting environment...")
            System.Configuration.ConfigurationSettings.GetConfig("DNS")
            AddHandler GenuineGlobalEventProvider.GenuineChannelsGlobalEvent, New GenuineChannelsGlobalEventHandler(AddressOf GenuineChannelsEventHandler)

            RemotingConfiguration.Configure("Client.exe.config")

            ''GlobalLoggerContainer.Logger = new BinaryLog(@"c:\tmp\client.log", false);
            Console.WriteLine(".NET Remoting has been configured from Client.exe.config file.")

            Console.WriteLine("Please enter a nickname:")
            ChatClient.Nickname = Console.ReadLine()

            '' bind client's receiver
            RemotingServices.Marshal(ChatClient.Instance, "MessageReceiver.rem")

            While True
                Try
                    '' subscribe to the chat event
                    SyncLock ChatClient.IChatServerLock
                        ChatClient.IChatServer = DirectCast(Activator.GetObject(GetType(IChatRoom), (ConfigurationSettings.AppSettings("RemoteHostUri") + "/ChatServer.rem")), IChatServer)
                        ChatClient.IChatRoom = ChatClient.IChatServer.EnterToChatRoom(ChatClient.Nickname)
                    End SyncLock

                    '' until user sends messages
                    While True
                        Console.WriteLine("Enter a message to send or an empty string to exit.")

                        Dim str As String = Console.ReadLine()
                        If (str.Length <= 0) Then
                            Exit Sub
                        End If

                        ChatClient.IChatRoom.SendMessage(str)
                    End While
                Catch ex As Exception
                    Console.WriteLine(vbCrLf & vbCrLf & "---Exception: {0}. Stack trace: {1}.", ex.Message, ex.StackTrace)
                End Try

                Console.WriteLine("Next attempt to connect to the server will be in 3 seconds.")
                Thread.Sleep(3000)
            End While
        End Sub

        ''' <summary>
        ''' GenuineChannelsEventHandler.
        ''' </summary>
        Public Shared Sub GenuineChannelsEventHandler(ByVal sender As Object, ByVal e As GenuineEventArgs)
            Dim hostInfoString As String

            If e.HostInformation Is Nothing Then
                hostInfoString = "<unknown>"
            Else
                hostInfoString = e.HostInformation.ToString()
            End If

            If (e.SourceException Is Nothing) Then
                Console.WriteLine(vbCrLf & vbCrLf & "---Global event: {0}\r\nRemote host: {1}", e.EventType, hostInfoString)
            Else
                Console.WriteLine(vbCrLf & vbCrLf & "---Global event: {0}\r\nRemote host: {1}\r\nException: {2}", e.EventType, hostInfoString, e.SourceException)
            End If

            If (e.EventType = GenuineEventType.GeneralServerRestartDetected) Then
                '' server has been restarted so it does not know that we have been subscribed to
                '' messages and ours nickname
                SyncLock ChatClient.IChatServerLock
                    ChatClient.IChatServer = DirectCast(Activator.GetObject(GetType(IChatRoom), (ConfigurationSettings.AppSettings("RemoteHostUri") + "/ChatServer.rem")), IChatServer)
                    ChatClient.IChatRoom = ChatClient.IChatServer.EnterToChatRoom(ChatClient.Nickname)
                End SyncLock
            End If
        End Sub

        ''' <summary>
        ''' Message receiver.
        ''' It receives messages async and writes them separately from the main thread.
        ''' But it does not matter for console application.
        ''' </summary>
        ''' <param name="message">The message.</param>
        Public Function ReceiveMessage(ByVal message As String, ByVal nickname As String) As Object Implements IMessageReceiver.ReceiveMessage
            Console.WriteLine("Message ""{0}"" from ""{1}"".", message, nickname)
            Return Nothing
        End Function

        ''' <summary>
        ''' This is to insure that when created as a Singleton, the first instance never dies,
        ''' regardless of the expired time.
        ''' </summary>
        ''' <returns></returns>
        Public Overloads Function InitializeLifetimeService() As Object
            Return Nothing
        End Function
    End Class
End Module
