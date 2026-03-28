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

    ' a dictionary used to caching event filter types.
    Private eventFilterMappings As Dictionary(Of EventFilter, Dictionary(Of Integer, String)) = New Dictionary(Of EventFilter, Dictionary(Of Integer, String))()

    'the condition filter object
    Private filter As EventFilter

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
                    Console.WriteLine()

                    'set target NodeId
                    Dim nodeId As NodeId = New NodeId("ns=2;s=Area51") ''Objects.Server.Plaforms.Area51'
                    If nodeId IsNot Nothing Then

                        Try
                            Console.WriteLine("Start monitoring.....)")
                            filter = client.CreateFilter(ObjectTypeIds.ConditionType)
                            filter.WhereClause.Push(FilterOperator.OfType, ObjectTypeIds.ConditionType)
                            Dim subscription As Subscription = New Subscription()
                            subscription.PublishingInterval = 1000
                            subscription.PublishingEnabled = False
                            subscription.DisplayName = "mySubsription"

                            'Register subscription state change events
                            AddHandler subscription.StateChanged, AddressOf Subscription_StateChanged
                            subscription.PublishingEnabled = False

                            'Add the subscription to the client instance
                            client.AddSubscription(subscription)
                            Dim reference As ReferenceDescription = client.GetReferenceDescriptionByNodeId(ObjectIds.Server)

                            If reference Is Nothing Then
                                Console.WriteLine("cannot reading reference description for nodeid")
                                Return 'Create a monitoring item and add to the subscription
                            End If

                            Dim monitoredItem As MonitoredItem = New MonitoredItem(DirectCast(Nothing, ITelemetryContext))
                            monitoredItem.NodeClass = reference.NodeClass
                            monitoredItem.AttributeId = Attributes.EventNotifier
                            monitoredItem.MonitoringMode = MonitoringMode.Reporting
                            monitoredItem.StartNodeId = nodeId
                            monitoredItem.Filter = filter
                            monitoredItem.DisplayName = "event monitoring"
                            monitoredItem.QueueSize = UInteger.MaxValue
                            monitoredItem.DiscardOldest = True

                            'checking and creating event filter cache
                            If Not eventFilterMappings.ContainsKey(filter) Then
                                Dim d As Dictionary(Of Integer, String) = New Dictionary(Of Integer, String)()

                                For i As Integer = 0 To CType(monitoredItem.Filter, EventFilter).SelectClauses.Count - 1
                                    Dim clause As String = CType(monitoredItem.Filter, EventFilter).SelectClauses(i).ToString()
                                    d.Add(i, clause)
                                Next

                                eventFilterMappings.Add(filter, d)
                            End If

                            'Register the notification callback for value changes
                            AddHandler monitoredItem.Notification, AddressOf Client_MonitorNotification

                            'Add the monitored item to the subscription
                            subscription.AddItem(monitoredItem)

                            'Apply all pending changes to the subscription
                            subscription.ApplyChanges()

                            'Enable publishing mode and apply the configured PublishingInterval
                            subscription.SetPublishingMode(True)
                            subscription.Modify()
                        Catch ex As Exception
                            Console.WriteLine(ex)
                        End Try
                    End If

                    Console.WriteLine("press enter for exit")
                    Console.ReadLine()
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

    Private Sub Client_MonitorNotification(ByVal monitoredItem As MonitoredItem, ByVal e As MonitoredItemNotificationEventArgs)
        Try
            Dim notification As EventFieldList = TryCast(e.NotificationValue, EventFieldList)
            If notification Is Nothing Then Return
            Dim eventTypeId As NodeId = FindEventType(monitoredItem, notification)

            ' ignore unknown events.
            If NodeId.IsNull(eventTypeId) Then Return

            ' ignore for refresh start or end.
            If eventTypeId Is ObjectTypeIds.RefreshStartEventType OrElse eventTypeId Is ObjectTypeIds.RefreshEndEventType Then Return

            'show actual event alarm data in debug window
            Dim sb As StringBuilder = New StringBuilder()
            sb.Append($"{Date.Now.ToLocalTime()} new event notification received:")
            sb.Append(Environment.NewLine)

            For i As Integer = 0 To notification.EventFields.Count - 1

                If notification.EventFields(i).Value IsNot Nothing Then
                    sb.Append($" { GetEventFilterMappings(CType(monitoredItem.Filter, EventFilter))(i) } {notification.EventFields(i).Value.ToString()}")
                    sb.Append(Environment.NewLine)
                End If
            Next

            Dim EventIdIndex As Integer = -1

            For i As Integer = 0 To notification.EventFields.Count - 1

                If notification.EventFields(i).Value IsNot Nothing Then
                    'Important => method returns all timestamps in universal time format
                    Dim eventName As String = GetEventFilterMappings(filter)(i)

                    'store the index of eventid for eventual deleting the events
                    If EventIdIndex = -1 AndAlso eventName.Replace("/", "").ToLower().Equals("eventid") Then EventIdIndex = i
                    Dim value As Object = notification.EventFields(i).Value
                    'if value equals enetId, then convert value to hexstring
                    If EventIdIndex > -1 AndAlso EventIdIndex = i Then value = ByteArrayToString(CType(notification.EventFields(EventIdIndex).Value, Byte()))

                    If notification.EventFields(i).Value IsNot Nothing Then
                        sb.Append($" { eventName } {value.ToString()}")
                        sb.Append(Environment.NewLine)
                    End If
                End If
            Next

            sb.Append(Environment.NewLine)
            Console.WriteLine(sb.ToString())
        Catch exception As Exception
            Console.WriteLine(exception.Message)
        End Try
    End Sub

    ''' <summary>
    ''' returns cached eventfilter
    ''' </summary>
    ''' <param="filter">a EventFilter object</param>
    ''' <returns>a Dictionary with int as key and string als value, null if monitoredItem not member of a active subscription or not with a filter activated</returns>
    Public Function GetEventFilterMappings(ByVal filter As EventFilter) As Dictionary(Of Integer, String)
        If eventFilterMappings.ContainsKey(filter) Then
            Return eventFilterMappings(filter)
        Else
            Dim d As Dictionary(Of Integer, String) = New Dictionary(Of Integer, String)()

            For i As Integer = 0 To filter.SelectClauses.Count - 1
                Dim clause As String = filter.SelectClauses(i).ToString()
                d.Add(i, clause)
            Next

            eventFilterMappings.Add(filter, d)
            Return d
        End If
    End Function

    ''' <summary>
    ''' Finds the type of the event for the notification.
    ''' </summary>
    ''' <param="monitoredItem">The monitored item.</param>
    ''' <param="notification">The notification.</param>
    ''' <returns>The NodeId of the EventType.</returns>
    Public Shared Function FindEventType(ByVal monitoredItem As MonitoredItem, ByVal notification As EventFieldList) As NodeId
        Dim filter As EventFilter = TryCast(monitoredItem.Status.Filter, EventFilter)

        If filter IsNot Nothing Then

            For ii As Integer = 0 To filter.SelectClauses.Count - 1
                Dim clause As SimpleAttributeOperand = filter.SelectClauses(ii)

                If clause.BrowsePath.Count = 1 AndAlso clause.BrowsePath(0) = BrowseNames.EventType Then
                    Return TryCast(notification.EventFields(ii).Value, NodeId)
                End If
            Next
        End If

        Return Nothing
    End Function

    Private Sub Subscription_StateChanged(ByVal subscription As Subscription, ByVal e As SubscriptionStateChangedEventArgs)
        Console.WriteLine($"{Date.Now.ToLocalTime() } State of Subscription { UaClient.SubscriptionToString(subscription) } changed to => { e.Status.ToString()}")
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
End Class
