Imports System
Imports System.Collections.Generic
Imports System.Reflection
Imports System.Text
Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Client
Imports PLCcom.Opc.Ua.Client.Sdk

Public Class Program

    'define the ua client object
    Private client As UaClient = Nothing

    'the default event filter object
    Private defaultFilter As EventFilter = Nothing

    ' a dictionary used to caching event filter types.
    Private mEventFilterMappings As Dictionary(Of EventFilter, Dictionary(Of Integer, String)) = New Dictionary(Of EventFilter, Dictionary(Of Integer, String))()

    Public Shared Sub Main(ByVal args As String())
        Dim program As Program = New Program()
        program.Start()
    End Sub

    Private Sub Start()
        Try

            'Submit your license information from your license e-mail
            Dim LicenseUserName As String = "<Enter your UserName here>"
            Dim LicenseSerial As String = "<Enter your Serial here>"

            Dim Endpoints As EndpointDescriptionCollection = UaClient.GetEndpoints(New Uri("opc.tcp://localhost:50540/PLCcom/HistoricalEventsServer"), 60000)

            'Sort endpoints by security level (highest security first)
            Endpoints = UaClient.SortEndpointsBySecurityLevel(Endpoints)

            If Endpoints.Count > 0 Then
                Console.WriteLine("endpoints found:")
                Dim counter As Integer = 0

                For Each Endpoint As EndpointDescription In Endpoints
                    Console.WriteLine($"{Math.Min(Threading.Interlocked.Increment(counter), counter - 1).ToString()} => { UaClient.EndpointToString(Endpoint)}")
                Next

                Console.WriteLine("please enter index of desired endpoint")
                Dim NumberOfEndpoint As String = Console.ReadLine()
                Console.WriteLine("")
                Dim iNumberOfEndpoint As Integer = -1

                If Integer.TryParse(NumberOfEndpoint, iNumberOfEndpoint) AndAlso iNumberOfEndpoint > -1 AndAlso iNumberOfEndpoint < Endpoints.Count Then
                    'Create a SessionConfiguration with the selected endpoint and application name
                    Dim sessionConfiguration As SessionConfiguration = SessionConfiguration.Build(Assembly.GetEntryAssembly().GetName().Name, Endpoints(iNumberOfEndpoint))

                    'Enable AutoConnect - the client will connect and reconnect automatically
                    sessionConfiguration.AutoConnect = True

                    'Display the certificate store path for debugging purposes
                    Console.WriteLine($"Info: Sessionconfiguration created, certificate store path => { sessionConfiguration.CertificateStorePath}")

                    'Create a new OPC UA client instance with license credentials
                    client = New UaClient(LicenseUserName, LicenseSerial, sessionConfiguration)
                    Console.WriteLine($"Info: license state => { client.GetLicenceMessage()}")

                    'Register event handlers to monitor the connection state
                    AddHandler client.ServerConnectionLost, AddressOf Client_ServerConnectionLost
                    AddHandler client.ServerConnected, AddressOf Client_ServerConnected
                    AddHandler client.SessionClosing, AddressOf Client_SessionClosing
                    AddHandler client.KeepAlive, AddressOf Client_KeepAlive
                    AddHandler client.CertificateValidation, AddressOf client_CertificateValidation
                    Console.WriteLine(client.GetSessionState().ToString())
                    Console.WriteLine()

                    'create the Defaultfilter
                    defaultFilter = client.CreateFilter(BrowseNames.EventType, BrowseNames.SourceNode, BrowseNames.SourceName, BrowseNames.Time, BrowseNames.ReceiveTime, BrowseNames.Message, BrowseNames.Severity, BrowseNames.EventId)

                    'set target NodeId
                    Dim nodeId As NodeId = New NodeId("ns=2;s=Area51") ''Objects.Server.Plaforms.Area51'
                    If nodeId IsNot Nothing Then

                        Try
                            Dim result As HistoryEvent = client.HistoryRead(nodeId, defaultFilter, Date.Now.AddDays(-1), Date.Now, 10)                'browse path from node
                            'filter with the reading structure
                            'starttime
                            'endtime
                            'max number of reading elements, 0 = unlimited

                            'show actual event alarm data in debug window
                            Dim sb As StringBuilder = New StringBuilder()
                            sb.Append(Environment.NewLine)
                            Dim EventIdIndex As Integer = -1

                            For Each ev As HistoryEventFieldList In result.Events

                                For i As Integer = 0 To ev.EventFields.Count - 1

                                    If ev.EventFields(i).Value IsNot Nothing Then
                                        'Important => method returns all timestamps in universal time format
                                        Dim eventName As String = GetEventFilterMappings(defaultFilter)(i)

                                        'store the index of eventid for eventual deleting the events
                                        If EventIdIndex = -1 AndAlso eventName.Replace("/", "").ToLower().Equals("eventid") Then EventIdIndex = i
                                        Dim value As Object = ev.EventFields(i).Value
                                        'if value equals enetId, then convert value to hexstring
                                        If EventIdIndex > -1 AndAlso EventIdIndex = i Then value = ByteArrayToString(CType(ev.EventFields(EventIdIndex).Value, Byte()))
                                        sb.Append($" { eventName } {value.ToString()}")
                                    End If
                                Next

                                sb.Append(Environment.NewLine)
                            Next

                            sb.Append(Environment.NewLine)
                            sb.Append(Environment.NewLine)
                            Console.WriteLine(sb.ToString())

                            If EventIdIndex > -1 Then 'the index of eventid is needed, 
                                Console.WriteLine("Do you want to delete all read events from the server? 'y'=yes, 'n'=not")

                                If Console.ReadLine().ToLower().Equals("y") Then
                                    ' Create Request data
                                    Dim deleteDetails As DeleteEventDetails = New DeleteEventDetails()
                                    deleteDetails.NodeId = nodeId

                                    'add the eventid for deleting
                                    For Each ev As HistoryEventFieldList In result.Events
                                        'delete event
                                        Dim deleteResult As HistoryUpdateResult = client.HistoryUpdate(nodeId, CType(ev.EventFields(EventIdIndex).Value, Byte()))
                                        Console.WriteLine($"delete event with eventId {ByteArrayToString(CType(ev.EventFields(CInt(EventIdIndex)).Value, Byte())) } result => {deleteResult.StatusCode}")
                                    Next

                                    Console.WriteLine("")
                                End If
                            End If

                        Catch ex As Exception
                            Console.WriteLine(ex)
                        End Try
                    End If
                Else
                    Console.WriteLine("invalid number of Endpoint")
                    Console.WriteLine()
                    Console.WriteLine("press enter for exit")
                    Console.ReadLine()
                End If
            Else
                Console.WriteLine("no endpoints found")
                Console.WriteLine()
                Console.WriteLine("press enter for exit")
                Console.ReadLine()
            End If

        Catch ex As Exception
            Console.WriteLine(ex)
            Console.WriteLine("press enter for exit")
            Console.ReadLine()
        Finally
            'Disconnect the current session
            If client IsNot Nothing AndAlso client.GetSessionState().Equals(SessionState.Connected) Then client.Disconnect()
        End Try
    End Sub

    Private Sub client_CertificateValidation(ByVal sender As CertificateValidator, ByVal e As CertificateValidationEventArgs)
        ' External certificate validation
        If ServiceResult.IsGood(e.Error) Then
            e.Accept = True
        ElseIf Not e.ContainsUnsuppressibleStatusCodes Then
            e.Accept = True
        ElseIf e.ContainsUnsuppressibleStatusCodes Then
            e.AcceptAll = True ' You can accept all unsuppressible status codes with this flag
        Else
            Throw New Exception(String.Format("Failed to validate certificate with error code {0}: {1}", e.Error.Code, e.Error.AdditionalInfo))
        End If
    End Sub

    Private Sub Client_ServerConnected(ByVal sender As Object, ByVal e As EventArgs)
        'Fired when the OPC UA session is successfully established
        Console.WriteLine($"{Date.Now.ToLocalTime()} Session connected")
    End Sub

    Private Sub Client_ServerConnectionLost(ByVal sender As Object, ByVal e As EventArgs)
        'Fired when the connection to the OPC UA server is lost
        Console.WriteLine($"{Date.Now.ToLocalTime()} Session connection lost")
    End Sub

    Private Sub Client_KeepAlive(ByVal session As ISession, ByVal e As KeepAliveEventArgs)
        'Fired periodically to indicate the server is still alive
    End Sub

    Private Sub Client_SessionClosing(ByVal sender As Object, ByVal e As EventArgs)
        Console.WriteLine($"{Date.Now.ToLocalTime()} Session closed")
    End Sub

    Public Shared Function StringToByteArray(ByVal hex As String) As Byte()
        Dim NumberChars = hex.Length
        Dim bytes = New Byte(NumberChars \ 2 - 1) {}

        For i = 0 To NumberChars - 1 Step 2
            bytes(i \ 2) = Convert.ToByte(hex.Substring(i, 2), 16)
        Next

        Return bytes
    End Function

    Public Shared Function ByteArrayToString(ByVal ba As Byte()) As String
        Dim hex As StringBuilder = New StringBuilder(ba.Length * 2)

        For Each b In ba
            hex.AppendFormat("{0:x2}", b)
        Next

        Return hex.ToString()
    End Function

    ''' <summary>
    ''' returns cached eventfilter
    ''' </summary>
    ''' <param="filter">a EventFilter object</param>
    ''' <returns>a Dictionary with int as key and string als value, null if monitoredItem not member of a active subscription or not with a filter activated</returns>
    Public Function GetEventFilterMappings(ByVal filter As EventFilter) As Dictionary(Of Integer, String)
        If mEventFilterMappings.ContainsKey(filter) Then
            Return mEventFilterMappings(filter)
        Else
            Dim d As Dictionary(Of Integer, String) = New Dictionary(Of Integer, String)()

            For i As Integer = 0 To filter.SelectClauses.Count - 1
                Dim clause As String = filter.SelectClauses(i).ToString()
                d.Add(i, clause)
            Next

            mEventFilterMappings.Add(filter, d)
            Return d
        End If
    End Function
End Class
