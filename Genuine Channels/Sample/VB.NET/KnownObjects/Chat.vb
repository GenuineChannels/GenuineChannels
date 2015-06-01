Imports System
''' <summary>
''' Describes a callback called when a message is received.
''' </summary>
Public Interface IMessageReceiver

    ''' <summary>
    ''' Is called by the server when a message is accepted.
    ''' </summary>
    ''' <param name="message">A message.</param>
    ''' <param name="nickname">Nickname of the client who sent the message.</param>
    Function ReceiveMessage(ByVal message As String, ByVal nickname As String) As Object


End Interface

''' <summary>
''' Server chat room factory.
''' </summary>
Public Interface IChatServer

    ''' <summary>
    ''' Performs log in to the chat room.
    ''' </summary>
    ''' <param name="nickname">Nickname.</param>
    ''' <param name="receiver">The receiver.</param>
    ''' <returns>Chat room interface.</returns>
    Function EnterToChatRoom(ByVal nickname As String) As IChatRoom


End Interface

''' <summary>
''' ChatRoom provides methods for the chatting.
''' </summary>
Public Interface IChatRoom

    ''' <summary>
    ''' Sends the message to all clients.
    ''' </summary>
    ''' <param name="message">Message being sent.</param>
    Function SendMessage(ByVal message As String)

End Interface