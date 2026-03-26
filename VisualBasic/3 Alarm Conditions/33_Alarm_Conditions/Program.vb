Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Reflection
Imports System.Text
Imports System.Threading
Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Client
Imports PLCcom.Opc.Ua.Client.Sdk

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
                    Console.WriteLine("Would you like to enter a filter? y/n")
                    Dim filter As EventFilter = Nothing

                    If Console.ReadLine().ToLower().Equals("y") Then
                        Console.WriteLine($"Please enter filter level... { Microsoft.VisualBasic.Constants.vbCrLf }List of commands: { Microsoft.VisualBasic.Constants.vbCrLf }1    - All { Microsoft.VisualBasic.Constants.vbCrLf }2    - Dialogs { Microsoft.VisualBasic.Constants.vbCrLf }3    - Alarms { Microsoft.VisualBasic.Constants.vbCrLf }4    - Limit alarms { Microsoft.VisualBasic.Constants.vbCrLf }5    - Discrete alarms{ Microsoft.VisualBasic.Constants.vbCrLf}")

                        'create eventfilter for monitoring
                        Select Case Console.ReadLine().ToLower()
                            Case "1"
                                filter = client.CreateFilter(ObjectTypeIds.ConditionType)
                            Case "2"
                                filter = client.CreateFilter(ObjectTypeIds.DialogConditionType)
                            Case "3"
                                filter = client.CreateFilter(ObjectTypeIds.AlarmConditionType)
                            Case "4"
                                filter = client.CreateFilter(ObjectTypeIds.ExclusiveLimitAlarmType, ObjectTypeIds.NonExclusiveLimitAlarmType)
                            Case "5"
                                filter = client.CreateFilter(ObjectTypeIds.DiscreteAlarmType)
                            Case Else
                                Console.WriteLine("Unknown command...")
                                'create standard eventfilter for monitoring
                                filter = client.CreateFilter(ObjectTypeIds.ConditionType)
                                filter.WhereClause.Push(FilterOperator.OfType, ObjectTypeIds.ConditionType)
                        End Select
                    Else
                        'create standard eventfilter for monitoring
                        filter = client.CreateFilter(ObjectTypeIds.ConditionType)
                        filter.WhereClause.Push(FilterOperator.OfType, ObjectTypeIds.ConditionType)
                    End If

                    Console.WriteLine("Start monitoring.....)")
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
                    Dim command As String = String.Empty

                    Do
                        Console.WriteLine()
                        Dim commandList As String = $"List of commands: { Microsoft.VisualBasic.Constants.vbCrLf }1 - List all alarms { Microsoft.VisualBasic.Constants.vbCrLf }2 - Refresh active alarms { Microsoft.VisualBasic.Constants.vbCrLf }3 - Enable alarm { Microsoft.VisualBasic.Constants.vbCrLf }4 - Disable alarm { Microsoft.VisualBasic.Constants.vbCrLf }5 - Acknowledge alarm { Microsoft.VisualBasic.Constants.vbCrLf }6 - Add comment { Microsoft.VisualBasic.Constants.vbCrLf }7 - Confirm alarm { Microsoft.VisualBasic.Constants.vbCrLf }8 - Shelve alarm { Microsoft.VisualBasic.Constants.vbCrLf }9 - Respond { Microsoft.VisualBasic.Constants.vbCrLf }0 - Close the application { Microsoft.VisualBasic.Constants.vbCrLf}"
                        Console.WriteLine(commandList)
                        Console.WriteLine($"Enter Commands:{ Microsoft.VisualBasic.Constants.vbLf}")
                        command = Console.ReadLine()
                        Dim AlarmNumber As UInteger = 0

                        Select Case command.ToLower()
                            Case "1"
                                ListAlarms()
                            Case "2"
                                client.Refresh_Conditions(subscription)
                                ListAlarms()
                            Case "3"
                                Console.WriteLine($"Enter alarm number: { Microsoft.VisualBasic.Constants.vbCrLf}")

                                If Not UInteger.TryParse(Console.ReadLine(), AlarmNumber) Then
                                    Console.WriteLine($"wrong alarm number: { Microsoft.VisualBasic.Constants.vbCrLf}")
                                    Continue Do
                                End If

                                EnableDisableCondition(AlarmNumber, True)
                            Case "4"
                                Console.WriteLine($"Enter alarm number: { Microsoft.VisualBasic.Constants.vbCrLf}")

                                If Not UInteger.TryParse(Console.ReadLine(), AlarmNumber) Then
                                    Console.WriteLine($"wrong alarm number: { Microsoft.VisualBasic.Constants.vbCrLf}")
                                    Continue Do
                                End If

                                EnableDisableCondition(AlarmNumber, False)
                            Case "5"
                                Console.WriteLine($"Enter alarm number: { Microsoft.VisualBasic.Constants.vbCrLf}")

                                If Not UInteger.TryParse(Console.ReadLine(), AlarmNumber) Then
                                    Console.WriteLine($"wrong alarm number: { Microsoft.VisualBasic.Constants.vbCrLf}")
                                    Continue Do
                                End If

                                Console.WriteLine($"Enter comment: { Microsoft.VisualBasic.Constants.vbCrLf}")
                                Dim Comment As String = Console.ReadLine()
                                Acknowledge(AlarmNumber, Comment)
                            Case "6"
                                Console.WriteLine($"Enter alarm number: { Microsoft.VisualBasic.Constants.vbCrLf}")

                                If Not UInteger.TryParse(Console.ReadLine(), AlarmNumber) Then
                                    Console.WriteLine($"wrong alarm number: { Microsoft.VisualBasic.Constants.vbCrLf}")
                                    Continue Do
                                End If

                                Console.WriteLine($"Enter comment: { Microsoft.VisualBasic.Constants.vbCrLf}")
                                Dim Comment As String = Console.ReadLine()
                                Me.AddComment(AlarmNumber, Comment)
                            Case "7"
                                Console.WriteLine($"Enter alarm number: { Microsoft.VisualBasic.Constants.vbCrLf}")

                                If Not UInteger.TryParse(Console.ReadLine(), AlarmNumber) Then
                                    Console.WriteLine($"wrong alarm number: { Microsoft.VisualBasic.Constants.vbCrLf}")
                                    Continue Do
                                End If

                                Console.WriteLine($"Enter comment: { Microsoft.VisualBasic.Constants.vbCrLf}")
                                Dim Comment As String = Console.ReadLine()
                                Me.Confirm(AlarmNumber, Comment)
                            Case "8"
                                Console.WriteLine($"Enter dialog number: { Microsoft.VisualBasic.Constants.vbCrLf}")

                                If Not UInteger.TryParse(Console.ReadLine(), AlarmNumber) Then
                                    Console.WriteLine($"wrong dialog number: { Microsoft.VisualBasic.Constants.vbCrLf}")
                                    Continue Do
                                End If

                                Console.WriteLine($"Enter subcommand: { Microsoft.VisualBasic.Constants.vbCrLf }on    - Online { Microsoft.VisualBasic.Constants.vbCrLf }off    - Offline { Microsoft.VisualBasic.Constants.vbCrLf }exit - Abort shelving { Microsoft.VisualBasic.Constants.vbCrLf}")
                                Dim SubCommand As String = Console.ReadLine()

                                Select Case SubCommand.ToLower()
                                    Case "on"
                                        Console.WriteLine("on => Online..")
                                        Respond(AlarmNumber, True)
                                    Case "off"
                                        Console.WriteLine("off => Offline..")
                                        Respond(AlarmNumber, False)
                                    Case "exit"
                                        Console.WriteLine("exit => Abort shelving..")
                                    Case Else
                                        Console.WriteLine("Unknown command => Abort shelving..")
                                End Select

                            Case "9"
                                Console.WriteLine($"Enter alarm number: { Microsoft.VisualBasic.Constants.vbCrLf}")

                                If Not UInteger.TryParse(Console.ReadLine(), AlarmNumber) Then
                                    Console.WriteLine($"wrong alarm number: { Microsoft.VisualBasic.Constants.vbCrLf}")
                                    Continue Do
                                End If

                                Console.WriteLine($"Enter subcommand: { Microsoft.VisualBasic.Constants.vbCrLf }u    - Unshelve { Microsoft.VisualBasic.Constants.vbCrLf }o    - One shot shelve { Microsoft.VisualBasic.Constants.vbCrLf }t    - Timedshelve { Microsoft.VisualBasic.Constants.vbCrLf }exit - Abort shelving {Microsoft.VisualBasic.Constants.vbCrLf}")

                                Select Case Console.ReadLine()
                                    Case "u"
                                        Console.WriteLine("u => Unshelve..")
                                        Shelve(AlarmNumber, AlarmEvent.ShelvingMethod.UnShelve, 0)
                                    Case "o"
                                        Console.WriteLine("o => One shot shelve..")
                                        Shelve(AlarmNumber, AlarmEvent.ShelvingMethod.OneShot, 0)
                                    Case "t"
                                        Console.WriteLine("t => Timedshelve..")
                                        Console.WriteLine("please enter desired shelving time...")
                                        Dim ShelvingTime As Double = 0

                                        If Double.TryParse(Console.ReadLine(), ShelvingTime) Then
                                            Shelve(AlarmNumber, AlarmEvent.ShelvingMethod.TimedShelve, ShelvingTime)
                                        Else
                                            Console.WriteLine($"invalid number => Abort shelving.. { Microsoft.VisualBasic.Constants.vbCrLf}")
                                            Continue Do
                                        End If

                                    Case "exit"
                                        Console.WriteLine("exit => Abort shelving..")
                                    Case Else
                                        Console.WriteLine("Unknown command => Abort shelving..")
                                End Select
                        End Select
                    Loop While Not command.ToUpper().StartsWith("0")
                Else
                    Console.WriteLine("invalid number of Endpoint")
                End If
            Else
                Console.WriteLine("no endpoints found")
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

    Private Sub Subscription_StateChanged(ByVal subscription As Subscription, ByVal e As SubscriptionStateChangedEventArgs)
        Console.WriteLine($"{Date.Now.ToLocalTime() } State of Subscription { UaClient.SubscriptionToString(subscription) } changed to => { e.Status.ToString()}")
    End Sub

    Private Sub Client_MonitorNotification(ByVal monitoredItem As MonitoredItem, ByVal e As MonitoredItemNotificationEventArgs)
        Try
            Dim notification As EventFieldList = TryCast(e.NotificationValue, EventFieldList)
            If notification Is Nothing Then Return

            'catch the MonitorNotification event
            Dim condition As ConditionState = client.GetConditionState(monitoredItem, notification)
            If condition Is Nothing Then Return
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

        Catch exception As Exception
            Console.WriteLine(exception.Message)
        End Try
    End Sub

    Private Sub ListAlarms()
        Try
            Dim sb As StringBuilder = New StringBuilder()
            sb.Append(Environment.NewLine)
            sb.Append("Current alarm list:")
            sb.Append(Environment.NewLine)
            Dim counter As Integer = 0

            For Each alarmEvent As AlarmEvent In GetEventCache(True)
                sb.Append($"{Math.Min(Interlocked.Increment(counter), counter - 1).ToString()} ")
                Dim condition As ConditionState = alarmEvent.GetConditionState()
                sb.Append($"Source={condition.SourceName.Value} ")
                sb.Append($"Condition={condition.ConditionName.Value} ")
                If condition.BranchId IsNot Nothing Then sb.Append("Branch={condition.BranchId.Value} ")
                sb.Append($"Severity={condition.Severity.Value} ")
                sb.Append($"Time={condition.Time.Value.ToLocalTime()} ")
                sb.Append($"State={condition.EnabledState.EffectiveDisplayName.Value} ")
                sb.Append($"Message={condition.Message.Value} ")
                sb.Append($"Comment={condition.Comment.Value} ")
                sb.Append(Environment.NewLine)
            Next

            sb.Append(Environment.NewLine)
            Console.WriteLine(sb.ToString())
        Catch ex As Exception
            Console.WriteLine(ex)
        End Try
    End Sub

    Private Sub Acknowledge(ByVal AlarmNumber As UInteger, ByVal comment As String)
        Try

            If AlarmNumber > mEventFilterMappings.Count Then
                Console.WriteLine($"AlarmNumber { AlarmNumber.ToString() } is out of range")
            End If

            Dim alarmEvent As AlarmEvent = GetEventCache(False).ToArray()(CInt(AlarmNumber))
            alarmEvent.Acknowledge(comment)
            Console.WriteLine("method successfully")
        Catch ex As Exception
            Console.WriteLine(ex)
        End Try
    End Sub

    Private Sub EnableDisableCondition(ByVal AlarmNumber As UInteger, ByVal enable As Boolean)
        Try

            If AlarmNumber > GetEventCache(False).Count Then
                Console.WriteLine($"AlarmNumber { AlarmNumber.ToString() } is out of range")
            End If

            Dim alarmEvent As AlarmEvent = GetEventCache(False).ToArray()(CInt(AlarmNumber))
            alarmEvent.EnableDisableCondition(enable)
            Console.WriteLine("method successfully")
        Catch ex As Exception
            Console.WriteLine(ex)
        End Try
    End Sub

    Private Sub AddComment(ByVal AlarmNumber As UInteger, ByVal comment As String)
        Try

            If AlarmNumber > GetEventCache(False).Count Then
                Console.WriteLine($"AlarmNumber { AlarmNumber.ToString() } is out of range")
            End If

            Dim alarmEvent As AlarmEvent = GetEventCache(False).ToArray()(CInt(AlarmNumber))
            alarmEvent.AddComment(comment)
            Console.WriteLine("method successfully")
        Catch ex As Exception
            Console.WriteLine(ex)
        End Try
    End Sub

    Private Sub Confirm(ByVal AlarmNumber As UInteger, ByVal comment As String)
        Try

            If AlarmNumber > GetEventCache(False).Count Then
                Console.WriteLine($"AlarmNumber { AlarmNumber.ToString() } is out of range")
            End If

            Dim alarmEvent As AlarmEvent = GetEventCache(False).ToArray()(CInt(AlarmNumber))
            alarmEvent.Confirm(comment)
            Console.WriteLine("method successfully")
        Catch ex As Exception
            Console.WriteLine(ex)
        End Try
    End Sub

    Private Sub Shelve(ByVal AlarmNumber As UInteger, ByVal shelvingMethod As AlarmEvent.ShelvingMethod, ByVal shelvingTime As Double)
        Try

            If AlarmNumber > GetEventCache(False).Count Then
                Console.WriteLine($"AlarmNumber { AlarmNumber.ToString() } is out of range")
            End If

            Dim alarmEvent As AlarmEvent = GetEventCache(False).ToArray()(CInt(AlarmNumber))
            alarmEvent.Shelve(shelvingMethod, shelvingTime)
            Console.WriteLine("method successfully")
        Catch ex As Exception
            Console.WriteLine(ex)
        End Try
    End Sub

    Private Sub Respond(ByVal AlarmNumber As UInteger, ByVal OnlineState As Boolean)
        Try

            If AlarmNumber > GetEventCache(False).Count Then
                Console.WriteLine($"AlarmNumber { AlarmNumber.ToString() } is out of range")
            End If

            Dim alarmEvent As AlarmEvent = GetEventCache(False).ToArray()(CInt(AlarmNumber))
            alarmEvent.Respond(OnlineState)
            Console.WriteLine("method successfully")
        Catch ex As Exception
            Console.WriteLine(ex)
        End Try
    End Sub

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

    ''' <summary>
    ''' returns internal event cache
    ''' </summary>
    ''' <param="waitForEndingConditionRefresh">If sets parameter to true and condition refresh is in progress, the function will waiting on end of a eventualy running condition refresh. 
    '''                                            If a maximum wait time of 5000 milliseconds exceeded, a InvalidOperationException will be raise.
    '''                                            If sets parameter to false and condition refresh is in progress, the function returns the actual partitial result</param>
    ''' <returns>List of alarm event objects</returns>
    ''' <exception="T:System.InvalidOperationException">Deathlook detected => Operation not possible, condition refresh in progress! Please try again or set parameter waitForEndingConditionRefresh to false...</exception>
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
End Class
