' MIT License
' Copyright (c) Indi.An GmbH
'
' Permission is hereby granted, free of charge, to any person obtaining a copy
' of this software and associated documentation files (the "Software"), to deal
' in the Software without restriction, including without limitation the rights
' to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
' copies of the Software, and to permit persons to whom the Software is
' furnished to do so, subject to the following conditions:
'
' The above copyright notice and this permission notice shall be included in all
' copies or substantial portions of the Software.
'
' THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
' IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
' FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
' AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
' LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
' OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
' SOFTWARE.

' ==============================================================================
' PLCcom OPC UA Client SDK - Workshop 33: Alarm Conditions
'
' OPC UA Conditions are the foundation of the alarm system. This
' workshop demonstrates how to acknowledge, confirm and comment
' on alarm conditions - the typical operator workflow.
'
' What you will learn:
'   * How to acknowledge an alarm condition
'   * How to confirm an alarm condition
'   * How to add comments to conditions
'   * The alarm lifecycle (Active -> Acknowledged -> Confirmed)
'
' Target server: opc.tcp://localhost:48410
' ==============================================================================

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

         Console.WriteLine()


             Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
             Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 33: Alarm Conditions    ║")
             Console.WriteLine("║                                                              ║")
             Console.WriteLine("║  OPC UA Conditions are the foundation of the alarm system.   ║")
             Console.WriteLine("║  This workshop demonstrates how to acknowledge, confirm      ║")
             Console.WriteLine("║  and comment on alarm conditions.                            ║")
             Console.WriteLine("║                                                              ║")
             Console.WriteLine("║  What you will learn:                                        ║")
             Console.WriteLine("║    * Acknowledge an alarm condition                          ║")
             Console.WriteLine("║    * Confirm an alarm condition                              ║")
             Console.WriteLine("║    * Add comments to conditions                              ║")
             Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Required server: Server Workshop 21 (Alarm Conditions)      ║")
            Console.WriteLine("║  opc.tcp://localhost:48410                                   ║")
             Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
             Console.WriteLine()

            'TODO
            'Submit your license information from your license e-mail
            Dim LicenseUserName As String = "<Enter your UserName here>"
            Dim LicenseSerial As String = "<Enter your Serial here>"
            Dim Endpoints As EndpointDescriptionCollection = UaClient.GetEndpoints(New Uri("opc.tcp://localhost:48410"), certificateValidator:=AddressOf client_CertificateValidation)

            'sort endpoints by security level
            Endpoints = UaClient.SortEndpointsBySecurityLevel(Endpoints)

            If Endpoints.Count > 0 Then
                Console.WriteLine("endpoints found:")
                Dim counter As Integer = 0

                For Each Endpoint As EndpointDescription In Endpoints
                    Console.WriteLine($"{Math.Min(Interlocked.Increment(counter), counter - 1).ToString() } => { Endpoint.ToDisplayString()}")
                Next

                Console.WriteLine("please enter index of desired endpoint")
                Dim NumberOfEndpoint As String = Console.ReadLine()
                Console.WriteLine("")
                Dim iNumberOfEndpoint As Integer = -1

                If Integer.TryParse(NumberOfEndpoint, iNumberOfEndpoint) AndAlso iNumberOfEndpoint > -1 AndAlso iNumberOfEndpoint < Endpoints.Count Then

                    'create a a SessionConfiguration with the selected endpoint and application name
                    Dim sessionConfiguration As SessionConfiguration = SessionConfiguration.Build(Assembly.GetEntryAssembly().GetName().Name, Endpoints(iNumberOfEndpoint))

                    'disable auto connect - we connect explicitly below
                    sessionConfiguration.AutoConnect = False

                    'output certificate store path
                    Console.WriteLine($"Info: Sessionconfiguration created, certificate store path => { sessionConfiguration.CertificateStorePath}")

                    'Create a new opc client instance and pass your license information
                    client = New UaClient(LicenseUserName, LicenseSerial, sessionConfiguration)
                    Console.WriteLine($"Info: license state => { client.GetLicenceMessage()}")

                    'register events
                    AddHandler client.ServerConnectionLost, AddressOf Client_ServerConnectionLost
                    AddHandler client.ServerConnected, AddressOf Client_ServerConnected
                    AddHandler client.SessionClosing, AddressOf Client_SessionClosing
                    AddHandler client.KeepAlive, AddressOf Client_KeepAlive
                    AddHandler client.CertificateValidation, AddressOf client_CertificateValidation
                    Console.Write("  Connecting ... ")
                    client.Connect()
                    Console.WriteLine("OK")
                    Console.WriteLine()

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
                        End Select
                    Else
                        filter = client.CreateFilter(ObjectTypeIds.ConditionType)
                    End If

                    Console.WriteLine("Start monitoring...")
                    Dim subscription As Subscription = New Subscription()
                    subscription.PublishingInterval = 1000
                    subscription.PublishingEnabled = True
                    subscription.DisplayName = "mySubsription"

                    'register subscription events
                    AddHandler subscription.StateChanged, AddressOf Subscription_StateChanged

                    'add subscription to client
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


                    'register monitoring event
                    AddHandler monitoredItem.Notification, AddressOf Client_MonitorNotification

                    'add item to subscription
                    subscription.AddItem(monitoredItem)

                    'apply changes
                    subscription.ApplyChanges()

                    client.Refresh_Conditions(subscription)
                    Console.WriteLine("Start monitoring...")
                    Dim command As String = String.Empty

                    Do
                        Console.WriteLine()
                        Dim commandList As String = "List of commands:" & vbCrLf &
                                "1 - List all alarms" & vbCrLf &
                                "2 - Refresh active alarms" & vbCrLf &
                                "3 - Enable alarm" & vbCrLf &
                                "4 - Disable alarm" & vbCrLf &
                                "5 - Acknowledge alarm" & vbCrLf &
                                "6 - Add comment" & vbCrLf &
                                "7 - Confirm alarm" & vbCrLf &
                                "8 - Shelve alarm" & vbCrLf &
                                "9 - Respond" & vbCrLf &
                                "0 - Close the application"
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
                                        Respond(AlarmNumber, 0)
                                    Case "off"
                                        Console.WriteLine("off => Offline..")
                                        Respond(AlarmNumber, 1)
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
            'disconnect actual session
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
        'event opc ua server is connected
        Console.WriteLine($"{Date.Now.ToLocalTime()} Session connected")
    End Sub

    Private Sub Client_ServerConnectionLost(ByVal sender As Object, ByVal e As EventArgs)
        'event connection to opc ua server lost
        Console.WriteLine($"{Date.Now.ToLocalTime()} Session connection lost")
    End Sub

    Private Sub Client_KeepAlive(ByVal session As ISession, ByVal e As KeepAliveEventArgs)
        'catch the keepalive event of opc ua server
    End Sub

    Private Sub Client_SessionClosing(ByVal sender As Object, ByVal e As EventArgs)
        Console.WriteLine($"{Date.Now.ToLocalTime()} Session closed")
    End Sub

    Private Sub Subscription_StateChanged(ByVal subscription As Subscription, ByVal e As SubscriptionStateChangedEventArgs)
        Console.WriteLine($"{Date.Now.ToLocalTime() } State of Subscription { subscription.ToDisplayString() } changed to => { e.Status.ToString()}")
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

            Dim Identifier As String = $"NodeID:{condition.NodeId} BrancheID:{If(condition.BranchId?.Value IsNot Nothing, condition.BranchId.Value.ToString(), "")}"

            ' Retain=false means alarm resolved - remove from cache
            Dim retain As Boolean = If(condition.Retain?.Value, False)
            SyncLock mAlarmEventCache
                If retain Then
                    mAlarmEventCache(Identifier) = ae
                Else
                    mAlarmEventCache.Remove(Identifier)
                End If
            End SyncLock

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
                sb.Append($"Source={condition.SourceName?.Value} ")
                sb.Append($"Condition={condition.ConditionName?.Value} ")
                If condition.BranchId IsNot Nothing Then sb.Append($"Branch={condition.BranchId.Value} ")
                sb.Append($"Severity={condition.Severity?.Value} ")
                sb.Append($"Time={condition.Time?.Value.ToLocalTime()} ")
                sb.Append($"State={condition.EnabledState?.EffectiveDisplayName?.Value} ")
                sb.Append($"Message={condition.Message?.Value} ")
                sb.Append($"Retain={condition.Retain?.Value} ")
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

            If AlarmNumber >= CUInt(GetEventCache(False).Count) Then
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

            If AlarmNumber >= CUInt(GetEventCache(False).Count) Then
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

            If AlarmNumber >= CUInt(GetEventCache(False).Count) Then
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

            If AlarmNumber >= CUInt(GetEventCache(False).Count) Then
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

            If AlarmNumber >= CUInt(GetEventCache(False).Count) Then
                Console.WriteLine($"AlarmNumber { AlarmNumber.ToString() } is out of range")
            End If

            Dim alarmEvent As AlarmEvent = GetEventCache(False).ToArray()(CInt(AlarmNumber))
            alarmEvent.Shelve(shelvingMethod, shelvingTime)
            Console.WriteLine("method successfully")
        Catch ex As Exception
            Console.WriteLine(ex)
        End Try
    End Sub

    Private Sub Respond(ByVal AlarmNumber As UInteger, ByVal responseIndex As Integer)
        Try

            If AlarmNumber >= CUInt(GetEventCache(False).Count) Then
                Console.WriteLine($"AlarmNumber { AlarmNumber.ToString() } is out of range")
            End If

            Dim alarmEvent As AlarmEvent = GetEventCache(False).ToArray()(CInt(AlarmNumber))
            alarmEvent.Respond(responseIndex)
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
