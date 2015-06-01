Imports System
Imports System.Runtime.Remoting
Imports System.Runtime.Remoting.Messaging
Imports System.Runtime.Remoting.Channels

Imports KnownObjects
Imports Belikov.GenuineChannels
Imports Belikov.GenuineChannels.BroadcastEngine

''' <summary>
''' Represents a chat room.
''' </summary>
Public Class ChatRoom
    Inherits MarshalByRefObject
    Implements IChatRoom

    ''' <summary>
    ''' Constructs ChatRoom instance.
    ''' </summary>
    Public Sub New()
        '' bind server's methods
        Me._dispatcher.BroadcastCallFinishedHandler = New BroadcastCallFinishedHandler(AddressOf Me.BroadcastCallFinishedHandler)
        Me._dispatcher.CallIsAsync = True
    End Sub

    ''' <summary>
    ''' Chat members.
    ''' </summary>
    Private _dispatcher As Dispatcher = New Dispatcher(GetType(IMessageReceiver))

    ''' <summary>
    ''' Attaches the client.
    ''' </summary>
    ''' <param name="nickname">Nickname.</param>
    Public Sub AttachClient(ByVal nickname As String)
        Dim receiverUri As String = GenuineUtility.FetchCurrentRemoteUri() + "/MessageReceiver.rem"
        Dim iMessageReceiver As IMessageReceiver = DirectCast(Activator.GetObject(GetType(IMessageReceiver), receiverUri), IMessageReceiver)
        Me._dispatcher.Add(DirectCast(iMessageReceiver, MarshalByRefObject))

        GenuineUtility.CurrentSession("Nickname") = nickname
        Console.WriteLine("Client with nickname ""{0}"" has been registered.", nickname)
    End Sub

    ''' <summary>
    ''' Sends message to all clients.
    ''' </summary>
    ''' <param name="message">Message to send.</param>
    ''' <returns>Number of clients having received this message.</returns>
    Public Function SendMessage(ByVal message As String) As Object Implements IChatRoom.SendMessage
        '' fetch the nickname
        Dim nickname As String = DirectCast(GenuineUtility.CurrentSession("Nickname"), String)
        Console.WriteLine("Message ""{0}"" will be sent to all clients from {1}.", message, nickname)

        Dim iMessageReceiver As IMessageReceiver = DirectCast(Me._dispatcher.TransparentProxy, IMessageReceiver)
        iMessageReceiver.ReceiveMessage(message, nickname)
    End Function

    ''' <summary>
    ''' Called by broadcast dispatcher when all calls are performed.
    ''' Does not undertake any actions.
    ''' </summary>
    ''' <param name="dispatcher">Source dipatcher.</param>
    ''' <param name="message">Source message.</param>
    '' <param name="resultCollector">Call results.</param>
    Public Sub BroadcastCallFinishedHandler(ByVal dispatcher As Dispatcher, ByVal message As IMessage, ByVal resultCollector As ResultCollector)
    End Sub
End Class
