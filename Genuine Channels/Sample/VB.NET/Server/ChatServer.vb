Imports System
Imports System.Collections
Imports System.Runtime.Remoting
Imports System.Runtime.Remoting.Channels
Imports System.Runtime.Remoting.Lifetime
Imports System.Runtime.Remoting.Messaging

Imports KnownObjects
Imports Belikov.GenuineChannels
Imports Belikov.GenuineChannels.BroadcastEngine
Imports Belikov.GenuineChannels.DotNetRemotingLayer
Imports Belikov.GenuineChannels.Logbook

Module Server

    Sub Main()
        ChatServer.Main()
    End Sub

    ''' <summary>
    ''' Chat server implements server that configures Genuine Server TCP Channel and implements
    ''' chat server behavior.
    ''' </summary>
    Class ChatServer
        Inherits MarshalByRefObject
        Implements IChatServer

        ''' <summary>
        ''' The main entry point for the application.
        ''' </summary>
        Public Shared Sub Main()
            Try
                '' setup .NET remoting
                System.Configuration.ConfigurationSettings.GetConfig("DNS")
                AddHandler GenuineGlobalEventProvider.GenuineChannelsGlobalEvent, New GenuineChannelsGlobalEventHandler(AddressOf GenuineChannelsEventHandler)
                ''GlobalLoggerContainer.Logger = new BinaryLog(@"c:\tmp\server.log", false)
                RemotingConfiguration.Configure("Server.exe.config")

                '' bind the server
                RemotingServices.Marshal(New ChatServer(), "ChatServer.rem")

                Console.WriteLine("Server has been started. Press enter to exit.")
                Console.ReadLine()
            Catch ex As Exception
                Console.WriteLine("Exception: {0}. Stack trace: {1}.", ex.Message, ex.StackTrace)
            End Try
        End Sub

        ''' <summary>
        ''' Catches Genuine Channels events and removes client session when
        ''' user disconnects.
        ''' </summary>
        ''' <param name="sender"></param>
        ''' <param name="e"></param>
        Public Shared Sub GenuineChannelsEventHandler(ByVal sender As Object, ByVal e As GenuineEventArgs)
            Dim hostInfoString As String

            If e.HostInformation Is Nothing Then
                hostInfoString = "<unknown>"
            Else
                hostInfoString = e.HostInformation.ToString()
            End If

            If e.SourceException Is Nothing Then
                Console.WriteLine(vbCrLf & vbCrLf & "---Global event: {0}\r\nRemote host: {1}", e.EventType, hostInfoString)
            Else
                Console.WriteLine(vbCrLf & vbCrLf & "---Global event: {0}\r\nRemote host: {1}\r\nException: {2}", e.EventType, hostInfoString, e.SourceException)

            End If

            If e.EventType = GenuineEventType.GeneralConnectionClosed Then
                '' the client disconnected
                Dim nickname As String = DirectCast(e.HostInformation("Nickname"), String)
                If Not (nickname Is Nothing) Then
                    Console.WriteLine("Client ""{0}"" has been disconnected.", nickname)
                End If
            End If
        End Sub

        ''' <summary>
        ''' This example was designed to have the only chat room.
        ''' </summary>
        Public Shared GlobalRoom As ChatRoom = New ChatRoom()

        ''' <summary>
        ''' Logs into the chat room.
        ''' </summary>
        ''' <param name="nickname">Nickname.</param>
        ''' <returns>Chat room interface.</returns>
        Public Function EnterToChatRoom(ByVal nickname As String) As IChatRoom Implements IChatServer.EnterToChatRoom
            GlobalRoom.AttachClient(nickname)
            GenuineUtility.CurrentSession("Nickname") = nickname
            Return GlobalRoom
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