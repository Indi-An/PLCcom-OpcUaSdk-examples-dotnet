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
' PLCcom OPC UA Client SDK - Workshop 31: Incoming Alarms
'
' OPC UA Alarms and Conditions notify clients about abnormal states.
' This workshop subscribes to alarm events and displays them as they
' arrive from the server.
'
' What you will learn:
'   * How to subscribe to alarm events
'   * How to receive and display incoming alarms
'   * How to read alarm properties (severity, message, source)
'
' Target server: opc.tcp://localhost:48410
' ==============================================================================

Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Client
Imports PLCcom.Opc.Ua.Client.Sdk
Imports System
Imports System.Collections.Generic
Imports System.Reflection
Imports System.Text

Public Class Program
    Private client As UaClient = Nothing

    ' a dictionary used to caching event filter types.
    Private mEventFilterMappings As Dictionary(Of EventFilter, Dictionary(Of Integer, String)) = New Dictionary(Of EventFilter, Dictionary(Of Integer, String))()

    Public Shared Sub Main(ByVal args As String())
        Dim program As Program = New Program()
        program.Start()
    End Sub

    Private Sub Start()
        Try

         Console.WriteLine()


             Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
             Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 31: Incoming Alarms     ║")
             Console.WriteLine("║                                                              ║")
             Console.WriteLine("║  OPC UA Alarms notify clients about abnormal states.         ║")
             Console.WriteLine("║  This workshop subscribes to alarm events and displays       ║")
             Console.WriteLine("║  them as they arrive from the server.                        ║")
             Console.WriteLine("║                                                              ║")
             Console.WriteLine("║  What you will learn:                                        ║")
             Console.WriteLine("║    * Subscribe to alarm events                               ║")
             Console.WriteLine("║    * Receive and display incoming alarms                     ║")
             Console.WriteLine("║    * Read alarm properties (severity, message, source)       ║")
             Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
             Console.WriteLine()

            'TODO
            'Submit your license information from your license e-mail
            Dim LicenseUserName As String = "<Enter your UserName here>"
            Dim LicenseSerial As String = "<Enter your Serial here>"
            Dim Endpoints As EndpointDescriptionCollection = UaClient.GetEndpoints(New Uri("opc.tcp://localhost:48410"), 60000)

            'sort endpoints by security level
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
                    'create a a SessionConfiguration with the selected endpoint and application name
                    Dim sessionConfiguration As SessionConfiguration = SessionConfiguration.Build(Assembly.GetEntryAssembly().GetName().Name, Endpoints(iNumberOfEndpoint))

                    'enable auto connect functionality
                    sessionConfiguration.AutoConnect = True

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
                    Console.WriteLine(client.GetSessionState().ToString())
                    Console.WriteLine()

                    Try
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

                        'register subscription events
                        AddHandler subscription.StateChanged, AddressOf Subscription_StateChanged
                        subscription.PublishingEnabled = False

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

                        'enable publishing mode of subscription and set PublishingInterval
                        subscription.SetPublishingMode(True)
                        subscription.Modify()
                        client.Refresh_Conditions(subscription)
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
        Console.WriteLine($"{Date.Now.ToLocalTime()} State of Subscription { UaClient.SubscriptionToString(subscription) } changed to => { e.Status.ToString()}")
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
            sb.Append("new notification received:")
            sb.Append(Environment.NewLine)

            For i As Integer = 0 To notification.EventFields.Count - 1

                If notification.EventFields(i).Value IsNot Nothing Then
                    sb.Append($" {GetEventFilterMappings(CType(monitoredItem.Filter, EventFilter))(i)} {notification.EventFields(i).Value.ToString()}")
                    sb.Append(Environment.NewLine)
                End If
            Next

            sb.Append(Environment.NewLine)
            Console.WriteLine(sb.ToString())
        Catch exception As Exception
            Console.WriteLine(exception.Message)
        End Try
    End Sub


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
