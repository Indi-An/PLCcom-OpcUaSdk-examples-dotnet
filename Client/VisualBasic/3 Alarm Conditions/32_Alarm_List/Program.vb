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
' PLCcom OPC UA Client SDK - Workshop 32: Alarm List
'
' While Workshop 31 shows incoming alarms one by one, this workshop
' maintains a live list of all active alarms. The list updates
' automatically as alarms appear, change state or disappear.
'
' What you will learn:
'   * How to maintain a live alarm list
'   * How to track alarm state changes
'   * How to identify alarms by ConditionId
'
' Target server: opc.tcp://localhost:48410
' ==============================================================================

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

         Console.WriteLine()


             Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
             Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 32: Alarm List          ║")
             Console.WriteLine("║                                                              ║")
             Console.WriteLine("║  Maintains a live list of all active alarms that updates     ║")
             Console.WriteLine("║  automatically as alarms appear, change or disappear.        ║")
             Console.WriteLine("║                                                              ║")
             Console.WriteLine("║  What you will learn:                                        ║")
             Console.WriteLine("║    * Maintain a live alarm list                              ║")
             Console.WriteLine("║    * Track alarm state changes                               ║")
             Console.WriteLine("║    * Identify alarms by ConditionId                          ║")
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
            Dim sessionConfiguration As SessionConfiguration = CreateConfig(Endpoints(iNumberOfEndpoint))
            PrintConfig(sessionConfiguration)

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

                    Try
                        Dim filter As EventFilter = client.CreateFilter(ObjectTypeIds.ConditionType)
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
        Console.WriteLine($"{Date.Now.ToLocalTime() } State of Subscription { subscription.ToDisplayString() }changed to => { e.Status.ToString()}")
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

            Dim condition As ConditionState = client.GetConditionState(monitoredItem, notification)
            If condition Is Nothing Then Return

            Dim sb As StringBuilder = New StringBuilder()
            sb.Append($"{Date.Now.ToLocalTime()} new Alarm notification:")
            sb.Append(Environment.NewLine)
            sb.Append($"  Source    = {condition.SourceName?.Value} ")
            sb.Append($"  Condition = {condition.ConditionName?.Value} ")
            sb.Append($"  Severity  = {condition.Severity?.Value} ")
            sb.Append($"  Time      = {condition.Time?.Value.ToLocalTime()} ")
            sb.Append($"  State     = {condition.EnabledState?.EffectiveDisplayName?.Value} ")
            sb.Append($"  Message   = {condition.Message?.Value} ")
            sb.Append($"  Retain    = {condition.Retain?.Value} ")
            sb.Append(Environment.NewLine)

            Dim ae As AlarmEvent = client.CreateAlarmEvent(condition.NodeId, condition)
            For i As Integer = 0 To notification.EventFields.Count - 1
                Dim filtername As String = GetEventFilterMappings(CType(monitoredItem.Filter, EventFilter))(i).Replace("/", "")
                Dim aei As AlarmEventItem = New AlarmEventItem(filtername, notification.EventFields(i).Value)
                ae.AlarmEventItems.Add(filtername, aei)
            Next

            Dim Identifier As String = $"NodeID:{condition.NodeId} BrancheID:{If(condition.BranchId?.Value IsNot Nothing, condition.BranchId.Value.ToString(), "")}"

            ' Retain=false means alarm is resolved - remove from list
            Dim retain As Boolean = If(condition.Retain?.Value, False)
            SyncLock mAlarmEventCache
                If retain Then
                    mAlarmEventCache(Identifier) = ae
                Else
                    mAlarmEventCache.Remove(Identifier)
                End If
            End SyncLock

            sb.Append("Current alarm list:")
            sb.Append(Environment.NewLine)
            For Each alarmEvent As AlarmEvent In GetEventCache(True)
                Dim alarmCondition As ConditionState = alarmEvent.GetConditionState()
                sb.Append($"  [{alarmCondition.ConditionName?.Value}] " &
                          $"Source={alarmCondition.SourceName?.Value} " &
                          $"Severity={alarmCondition.Severity?.Value} " &
                          $"Message={alarmCondition.Message?.Value}")
                sb.Append(Environment.NewLine)
            Next
            If Not GetEventCache(False).Any() Then sb.AppendLine("  (no active alarms)")

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

    ' =============================================================================
    ' Helper: CreateConfig
    ' =============================================================================
    ' Builds the SessionConfiguration for the selected endpoint.
    '
    ' Certificate handling:
    '   Application certificate -- required for Sign / SignAndEncrypt endpoints.
    '   HTTPS certificate       -- required for opc.https:// endpoints (any SecurityMode).
    '
    ' Load() returns Nothing if the certificate does not exist yet or cannot be read.
    ' Build(True) creates a new self-signed certificate, overwriting any existing file.
    Private Shared Function CreateConfig(ByVal endpoint As EndpointDescription) As SessionConfiguration
        Dim appAlias As String = System.Reflection.Assembly.GetEntryAssembly().GetName().Name
        Dim config As SessionConfiguration = SessionConfiguration.Build(appAlias, endpoint)
        config.AutoConnect = False

        ' HTTPS certificate -- required for opc.https:// endpoints, independent of SecurityMode.
        Dim httpsCert As UaClientCertificate = Nothing
        If endpoint.EndpointUrl IsNot Nothing AndAlso
           endpoint.EndpointUrl.StartsWith("opc.https://", StringComparison.OrdinalIgnoreCase) Then
            Dim host As String = New Uri(endpoint.EndpointUrl).Host
            httpsCert = UaClientCertificate.Load("./pki", host, "secretpassword")
            If httpsCert Is Nothing OrElse Not httpsCert.CheckValidity() Then
                httpsCert = New UaClientCertificate("./pki", "secretpassword", host, 720, "Indi.An GmbH") _
                    .Build(overwrite:=True)
            End If
        End If

        ' Application certificate -- required for secured endpoints (Sign or SignAndEncrypt).
        ' Not needed for SecurityMode.None (unencrypted connections).
        Dim appCert As UaClientCertificate = Nothing
        If Not endpoint.SecurityMode.Equals(MessageSecurityMode.None) Then
            appCert = UaClientCertificate.Load("./pki", appAlias, "secretpassword")
            If appCert Is Nothing OrElse Not appCert.CheckValidity() Then
                appCert = New UaClientCertificate("./pki", "secretpassword", appAlias, 720, "Indi.An GmbH") _
                    .Build(overwrite:=True)
            End If
        End If

        ' SetInstanceCertificate() sets CertificateStorePath and ApplicationCertificateFullPath.
        If appCert IsNot Nothing AndAlso httpsCert IsNot Nothing Then
            config.SetInstanceCertificate(appCert, httpsCert)
        ElseIf appCert IsNot Nothing Then
            config.SetInstanceCertificate(appCert)
        End If

        Return config
    End Function

    ' =============================================================================
    ' Helper: PrintConfig
    ' =============================================================================
    ' Prints the active client configuration to the console so you can verify
    ' all settings at a glance before connecting.
    Private Shared Sub PrintConfig(ByVal config As SessionConfiguration)
        Console.WriteLine("-- Active Client Configuration ------------------------------")
        If config.Endpoint IsNot Nothing Then
            Console.WriteLine("  Endpoint  : " & config.Endpoint.EndpointUrl)
            Console.WriteLine("  Security  : " & config.Endpoint.ToDisplayString())
        End If
        Console.WriteLine("  PKI Store : " & If(config.CertificateStorePath IsNot Nothing, config.CertificateStorePath, "(not set)"))
        Console.WriteLine("  Cert File : " & If(config.ApplicationCertificateFullPath IsNot Nothing, config.ApplicationCertificateFullPath, "(none -- SecurityMode.None)"))
        Console.WriteLine("-------------------------------------------------------------")
        Console.WriteLine()
    End Sub
End Class
