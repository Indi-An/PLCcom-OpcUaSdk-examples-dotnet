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
' PLCcom OPC UA Client SDK - Workshop 44: Monitor Historical Events
'
' This workshop subscribes to live events from a node that also has
' event history enabled. New events arrive in real-time via subscription
' and are also stored in the server's event history for later retrieval.
'
' What you will learn:
'   * How to subscribe to live events from a history-enabled source node
'   * How to receive and display event notifications in real-time
'   * The difference between live events (subscription) and
'     historical events (HistoryRead) - see Workshop 43
'
' Required server: Server Workshop 33 (Historical Events)
' opc.tcp://localhost:48410
' ==============================================================================

Imports System
Imports System.Reflection
Imports System.Text
Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Client
Imports PLCcom.Opc.Ua.Client.Sdk

Public Class Program

    Private client As UaClient = Nothing

    ' Field names match the SelectClauses order below — used for readable output.
    Private Shared ReadOnly FieldNames As String() =
        {"EventId", "EventType", "SourceNode", "SourceName", "Time", "Message", "Severity"}
    Private Const IDX_EVENTID As Integer = 0

    Public Shared Sub Main(ByVal args As String())
        Dim program As New Program()
        program.Start()
    End Sub

    Private Sub Start()
        Try
            Console.WriteLine()
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 44: Monitor Hist. Events║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Subscribes to live events from a node that also has event   ║")
            Console.WriteLine("║  history enabled. New events arrive in real-time and are     ║")
            Console.WriteLine("║  also stored in the server's history for later retrieval.    ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  What you will learn:                                        ║")
            Console.WriteLine("║    * Subscribe to live events from a history-enabled node    ║")
            Console.WriteLine("║    * Receive and display event notifications in real-time    ║")
            Console.WriteLine("║    * Difference: live events vs. HistoryRead (WS 43)         ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Required server: Server Workshop 33 (Historical Events)     ║")
            Console.WriteLine("║  opc.tcp://localhost:48410                                   ║")
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
            Console.WriteLine()

            'TODO
            'Submit your license information from your license e-mail
            Dim LicenseUserName As String = "<Enter your UserName here>"
            Dim LicenseSerial As String = "<Enter your Serial here>"

            Dim endpoints As EndpointDescriptionCollection = UaClient.GetEndpoints(
                New Uri("opc.tcp://localhost:48410"),
                certificateValidator:=AddressOf client_CertificateValidation)
            endpoints = UaClient.SortEndpointsBySecurityLevel(endpoints)

            If endpoints.Count = 0 Then
                Console.WriteLine("No endpoints found. Is Server Workshop 33 running?")
                Console.ReadLine()
                Return
            End If

            Console.WriteLine("endpoints found:")
            For i As Integer = 0 To endpoints.Count - 1
                Console.WriteLine($"{i} => {endpoints(i).ToDisplayString()}")
            Next

            Console.WriteLine("please enter index of desired endpoint")
            Dim idx As Integer
            If Not Integer.TryParse(Console.ReadLine(), idx) OrElse idx < 0 OrElse idx >= endpoints.Count Then
                Console.WriteLine("invalid number of Endpoint")
                Console.ReadLine()
                Return
            End If
            Console.WriteLine()

            Dim sessionConfig As SessionConfiguration = SessionConfiguration.Build(
                Assembly.GetEntryAssembly().GetName().Name, endpoints(idx))
            sessionConfig.AutoConnect = False

            Console.WriteLine("Info: Sessionconfiguration created, certificate store path => " &
                              sessionConfig.CertificateStorePath)

            client = New UaClient(LicenseUserName, LicenseSerial, sessionConfig)
            Console.WriteLine("Info: license state => " & client.GetLicenceMessage())

            AddHandler client.ServerConnectionLost, AddressOf Client_ServerConnectionLost
            AddHandler client.ServerConnected, AddressOf Client_ServerConnected
            AddHandler client.SessionClosing, AddressOf Client_SessionClosing
            AddHandler client.KeepAlive, AddressOf Client_KeepAlive
            AddHandler client.CertificateValidation, AddressOf client_CertificateValidation

            Console.Write("  Connecting ... ")
            client.Connect()
            Console.WriteLine("OK")
            Console.WriteLine()

            ' -- Resolve the reactor node (event source) via browse path ------
            ' Server 33 creates: Plant -> Reactor with EnableEvents() + EnableHistoryEvents()
            Dim nodeId As NodeId = client.GetNodeIdByPath("Objects.Plant.Reactor")
            If nodeId Is Nothing Then
                Console.WriteLine("Could not find 'Objects.Plant.Reactor'. Is Server Workshop 33 running?")
                Console.ReadLine()
                Return
            End If
            Console.WriteLine($"  Reactor NodeId: {nodeId}")
            Console.WriteLine()

            ' -- Create event filter ------------------------------------------
            ' Explicit SelectClauses so field names and order are known.
            ' FieldNames array above must match this order exactly.
            Dim filter As New EventFilter()
            For Each name As String In FieldNames
                filter.SelectClauses.Add(New SimpleAttributeOperand With {
                    .TypeDefinitionId = ObjectTypeIds.BaseEventType,
                    .BrowsePath = New QualifiedNameCollection From {New QualifiedName(name)},
                    .AttributeId = Attributes.Value
                })
            Next

            ' -- Create subscription ------------------------------------------
            Dim subscription As New Subscription() With {
                .PublishingInterval = 1000,
                .PublishingEnabled = True,
                .DisplayName = "myEventSubscription"
            }
            AddHandler subscription.StateChanged, AddressOf Subscription_StateChanged
            client.AddSubscription(subscription)

            ' -- Create monitored item for events -----------------------------
            Dim reference As ReferenceDescription = client.GetReferenceDescriptionByNodeId(nodeId)
            If reference Is Nothing Then
                Console.WriteLine("Cannot read reference description for reactor node.")
                Console.ReadLine()
                Return
            End If

            Dim monitoredItem As New MonitoredItem(DirectCast(Nothing, ITelemetryContext)) With {
                .NodeClass = reference.NodeClass,
                .AttributeId = Attributes.EventNotifier,
                .MonitoringMode = MonitoringMode.Reporting,
                .StartNodeId = nodeId,
                .Filter = filter,
                .DisplayName = "reactor event monitoring",
                .QueueSize = UInteger.MaxValue,
                .DiscardOldest = True
            }

            AddHandler monitoredItem.Notification, AddressOf Client_MonitorNotification
            subscription.AddItem(monitoredItem)
            subscription.ApplyChanges()

            Console.WriteLine("Start monitoring... (press ENTER to exit)")
            Console.ReadLine()

        Catch ex As Exception
            Console.WriteLine(ex)
        Finally
            Console.WriteLine("press enter for exit")
            Console.ReadLine()
            If client IsNot Nothing AndAlso client.GetSessionState().Equals(SessionState.Connected) Then
                client.Disconnect()
            End If
        End Try
    End Sub

    Private Sub Client_MonitorNotification(ByVal monitoredItem As MonitoredItem,
                                            ByVal e As MonitoredItemNotificationEventArgs)
        Try
            Dim notification As EventFieldList = TryCast(e.NotificationValue, EventFieldList)
            If notification Is Nothing Then Return

            Dim sb As New StringBuilder()
            sb.AppendLine($"{Date.Now.ToLocalTime():T} new event notification:")
            For i As Integer = 0 To Math.Min(notification.EventFields.Count, FieldNames.Length) - 1
                Dim val As Object = notification.EventFields(i).Value
                If val Is Nothing Then Continue For
                Dim display As String = If(i = IDX_EVENTID AndAlso TypeOf val Is Byte(),
                                           ToHex(CType(val, Byte())), val.ToString())
                sb.AppendLine($"  {FieldNames(i)} = {display}")
            Next
            Console.WriteLine(sb.ToString())
        Catch ex As Exception
            Console.WriteLine(ex.Message)
        End Try
    End Sub

    Private Sub Subscription_StateChanged(ByVal subscription As Subscription,
                                          ByVal e As SubscriptionStateChangedEventArgs)
        Console.WriteLine($"{Date.Now.ToLocalTime()} Subscription state => {e.Status}")
    End Sub

    Private Sub client_CertificateValidation(ByVal sender As CertificateValidator,
                                             ByVal e As CertificateValidationEventArgs)
        If ServiceResult.IsGood(e.Error) Then
            e.Accept = True
        ElseIf Not e.ContainsUnsuppressibleStatusCodes Then
            e.Accept = True
        ElseIf e.ContainsUnsuppressibleStatusCodes Then
            e.AcceptAll = True
        Else
            Throw New Exception($"Certificate validation failed: {e.Error.Code} {e.Error.AdditionalInfo}")
        End If
    End Sub

    Private Sub Client_ServerConnected(ByVal sender As Object, ByVal e As EventArgs)
        Console.WriteLine($"{Date.Now.ToLocalTime()} Session connected")
    End Sub

    Private Sub Client_ServerConnectionLost(ByVal sender As Object, ByVal e As EventArgs)
        Console.WriteLine($"{Date.Now.ToLocalTime()} Session connection lost")
    End Sub

    Private Sub Client_KeepAlive(ByVal session As ISession, ByVal e As KeepAliveEventArgs)
    End Sub

    Private Sub Client_SessionClosing(ByVal sender As Object, ByVal e As EventArgs)
        Console.WriteLine($"{Date.Now.ToLocalTime()} Session closed")
    End Sub

    Public Shared Function ToHex(ByVal ba As Byte()) As String
        Dim sb As New StringBuilder(ba.Length * 2)
        For Each b As Byte In ba
            sb.AppendFormat("{0:x2}", b)
        Next
        Return sb.ToString()
    End Function

End Class
