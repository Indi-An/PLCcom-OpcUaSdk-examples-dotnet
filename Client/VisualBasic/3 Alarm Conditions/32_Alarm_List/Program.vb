Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Client
Imports PLCcom.Opc.Ua.Client.Sdk
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Reflection
Imports System.Text
Imports System.Threading

Public Class Program
    Private client As UaClient = Nothing

    ' a dictionary used to caching event filter types.
    Private mEventFilterMappings As Dictionary(Of EventFilter, Dictionary(Of Integer, String)) = New Dictionary(Of EventFilter, Dictionary(Of Integer, String))()

    'a local AlarmCache
    Private mAlarmEventCache As Dictionary(Of String, AlarmEvent) = New Dictionary(Of String, AlarmEvent)()

    Public Shared Sub Main(ByVal args As String())
        Dim program As Program = New Program()
        program.Start()
    End Sub

    Private Sub Start()
        Try

            'Submit your license information from your license e-mail
            Dim LicenseUserName As String = "<Enter your UserName here>"
            Dim LicenseSerial As String = "<Enter your Serial here>"

            Dim Endpoints As EndpointDescriptionCollection = UaClient.GetEndpoints(New Uri("opc.tcp://localhost:50510/PLCcom/AlarmConditionServer"), 60000)

            'Sort endpoints by security level (highest security first)
            Endpoints = UaClient.SortEndpointsBySecurityLevel(Endpoints)

            If Endpoints.Count > 0 Then
                Console.WriteLine("endpoints found:")
                Dim counter As Integer = 0

                For Each Endpoint As EndpointDescription In Endpoints
                    Console.WriteLine($"{Math.Min(Interlocked.Increment(counter), counter - 1).ToString() } => { UaClient.EndpointToString(Endpoint)}")
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

                    Try
                        Dim filter As EventFilter = client.CreateFilter(ObjectTypeIds.ConditionType)
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
                        monitoredItem.StartNodeId = ObjectIds.Server
                        monitoredItem.Filter = filter
                        monitoredItem.DisplayName = "event monitoring"
                        monitoredItem.QueueSize = UInteger.MaxValue
                        monitoredItem.DiscardOldest = True

                        'checking and creating event filter cache
                        If Not mEventFilterMappings.ContainsKey(filter) Then
                            Dim d As Dictionary(Of Integer, String) = New Dictionary(Of Integer, String)()

                            For i As Integer = 0 To CType(monitoredItem.Filter, EventFilter).SelectClauses.Count - 1
                                Dim clause As String = CType(monitoredItem.Filter, EventFilter).SelectClauses(i).ToString()
                                d.Add(i, clause)
                            Next

                            mEventFilterMappings.Add(filter, d)
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
                        client.Refresh_Conditions(subscription)
                        Console.WriteLine("Start monitoring.....)")
                    Catch ex As Exception
                        Console.WriteLine(ex)
                        Console.WriteLine()
                    End Try
                Else
                    Console.WriteLine("invalid number of Endpoint")
                    Console.WriteLine()
                End If
            Else
                Console.WriteLine("no endpoints found")
                Console.WriteLine()
            End If

            Console.WriteLine()
            Console.WriteLine("press enter for exit")
            Console.ReadLine()
        Catch ex As Exception
            Console.WriteLine(ex)
            Console.WriteLine()
        Finally
            Console.WriteLine("press enter for exit")
            Console.ReadLine()

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

    Private Sub Subscription_StateChanged(ByVal subscription As Subscription, ByVal e As SubscriptionStateChangedEventArgs)
        Console.WriteLine($"{Date.Now.ToLocalTime() } State of Subscription { UaClient.SubscriptionToString(subscription) }changed to => { e.Status.ToString()}")
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

            'Create output string
            Dim sb As StringBuilder = New StringBuilder()
            sb.Append($"{Date.Now.ToLocalTime() } new Alarm notification:")
            sb.Append(Environment.NewLine)
            Dim actualAlarmcondition As ConditionState = client.GetConditionState(monitoredItem, notification)
            sb.Append($"Source={actualAlarmcondition.SourceName.Value} ")
            sb.Append($"Condition={actualAlarmcondition.ConditionName.Value} ")
            sb.Append($"Severity={actualAlarmcondition.Severity.Value} ")
            sb.Append($"Time={actualAlarmcondition.Time.Value.ToLocalTime()} ")
            sb.Append($"State={actualAlarmcondition.EnabledState.EffectiveDisplayName.Value} ")
            sb.Append($"Message={actualAlarmcondition.Message.Value} ")
            sb.Append($"Comment={actualAlarmcondition.Comment.Value} ")
            sb.Append(Environment.NewLine)
            sb.Append("Current alarm list:")
            sb.Append(Environment.NewLine)
            Dim condition As ConditionState = client.GetConditionState(monitoredItem, notification)
            Dim ae As AlarmEvent = client.CreateAlarmEvent(condition.NodeId, condition)

            'AlarmEventListe aufbauen und aktualisieren
            For i As Integer = 0 To notification.EventFields.Count - 1
                Dim filtername As String = GetEventFilterMappings(CType(monitoredItem.Filter, EventFilter))(i).Replace("/", "")
                Dim aei As AlarmEventItem = New AlarmEventItem(filtername, notification.EventFields(i).Value)
                ae.AlarmEventItems.Add(filtername, aei)
            Next

            Dim Identifier As String = $"NodeID:{condition.NodeId.ToString() } BrancheID:{ If(condition.BranchId IsNot Nothing, condition.BranchId.Value.ToString(), "")}"

            'Update Alarm cache
            If mAlarmEventCache.ContainsKey(Identifier) Then
                mAlarmEventCache(Identifier) = ae
            Else
                mAlarmEventCache.Add(Identifier, ae)
            End If

            For Each alarmEvent As AlarmEvent In GetEventCache(True)
                Dim alarmCondition As ConditionState = alarmEvent.GetConditionState()
                sb.Append(String.Format("Source={0} ", alarmCondition.SourceName.Value))
                sb.Append(String.Format("Condition={0} ", alarmCondition.ConditionName.Value))
                sb.Append(String.Format("Severity={0} ", alarmCondition.Severity.Value))
                sb.Append(String.Format("Time={0} ", alarmCondition.Time.Value.ToLocalTime()))
                sb.Append(String.Format("State={0} ", alarmCondition.EnabledState.EffectiveDisplayName.Value))
                sb.Append(String.Format("Message={0} ", alarmCondition.Message.Value))
                sb.Append(String.Format("Comment={0} ", alarmCondition.Comment.Value))
                sb.Append(Environment.NewLine)
            Next

            sb.Append(Environment.NewLine)
            Console.WriteLine(sb.ToString())
        Catch exception As Exception
            Console.WriteLine(exception.Message)
        End Try
    End Sub

    ''' <summary>
    ''' returns internal event cache
    ''' </summary>
    ''' <param="waitForEndingConditionRefresh">If sets parameter to true and condition refresh is in progress, the function will waiting on end of a eventualy running condition refresh. 
    '''                                            If a maximum wait time of 5000 milliseconds exceeded, a InvalidOperationException will be raise.
    '''                                            If sets parameter to false and condition refresh is in progress, the function returns the actual partitial result</param>
    ''' <returns>List of alarm event objects</returns>
    ''' <exception="T:System.InvalidOperationException">Deathlook detected => Operation Not possible, condition refresh In progress! Please Try again Or Set parameter waitForEndingConditionRefresh To False...</exception>
    Public Function GetEventCache(ByVal waitForEndingConditionRefresh As Boolean) As List(Of AlarmEvent)
        If Monitor.TryEnter(mAlarmEventCache, 5000) Then

            Try
                Return mAlarmEventCache.Values.ToList()
            Finally
                Monitor.Exit(mAlarmEventCache)
            End Try
        Else
            Throw New InvalidOperationException("Operation not possible, condition refresh in progress! Please try again...")
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
