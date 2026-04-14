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
' PLCcom OPC UA Client SDK - Workshop 23: Monitoring Items (Subscriptions)
'
' OPC UA subscriptions let you monitor value changes without polling.
' The server pushes DataChange notifications to the client whenever a
' monitored value changes. This is the most efficient way to track
' live process data.
'
' What you will learn:
'   * How to create a Subscription with a publishing interval
'   * How to add MonitoredItems to a subscription
'   * How to receive DataChange notifications via events
'   * How to manage subscription lifecycle (enable, modify, dispose)
'
' Target server: opc.tcp://localhost:48410
' ==============================================================================

Imports System
Imports System.Reflection
Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Client
Imports PLCcom.Opc.Ua.Client.Sdk

Public Class Program

    'actual publishing state of subscription
    Private publishingState As n_PublishingState = n_PublishingState.UNDEFINED

    Public Shared Sub Main(ByVal args As String())
        Dim p As Program = New Program()
        p.Start()
    End Sub

    Private Sub Start()
        Try

         Console.WriteLine()

             Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
             Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 23: Monitoring Items    ║")
             Console.WriteLine("║                                                              ║")
             Console.WriteLine("║  OPC UA subscriptions push DataChange notifications to       ║")
             Console.WriteLine("║  the client whenever a monitored value changes.              ║")
             Console.WriteLine("║  No polling needed - the most efficient approach.            ║")
             Console.WriteLine("║                                                              ║")
             Console.WriteLine("║  What you will learn:                                        ║")
             Console.WriteLine("║    * Create a Subscription with a publishing interval        ║")
             Console.WriteLine("║    * Add MonitoredItems to a subscription                    ║")
             Console.WriteLine("║    * Receive DataChange notifications via events             ║")
             Console.WriteLine("║    * Manage subscription lifecycle                           ║")
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
                    Using client As UaClient = New UaClient(LicenseUserName, LicenseSerial, sessionConfiguration)
                        Console.WriteLine($"Info: license state => { client.GetLicenceMessage()}")
                        Console.WriteLine("")

                        'register events
                        AddHandler client.ServerConnectionLost, AddressOf Client_ServerConnectionLost
                        AddHandler client.ServerConnected, AddressOf Client_ServerConnected
                        AddHandler client.SessionClosing, AddressOf Client_SessionClosing
                        AddHandler client.KeepAlive, AddressOf Client_KeepAlive
                        AddHandler client.CertificateValidation, AddressOf client_CertificateValidation


                        'create a new subscription
                        Using subscription As Subscription = New Subscription()
                            subscription.PublishingInterval = 1000
                            subscription.PublishingEnabled = False
                            subscription.DisplayName = "mySubsription"

                            'register subscription events
                            AddHandler subscription.StateChanged, AddressOf Subscription_StateChanged
                            AddHandler subscription.PublishStatusChanged, AddressOf Subscription_PublishStatusChanged

                            'add new subscription to client
                            client.AddSubscription(subscription)

                            Try
                                'Create a monitoring item and add to the subscription
                                Dim nodeId As NodeId = client.GetNodeIdByPath("Objects.Plant.Line1.Machine1.Temperature")
                                Dim monitoredItem As MonitoredItem = New MonitoredItem(subscription.DefaultItem) With {
                                    .StartNodeId = nodeId,
                                    .SamplingInterval = 500,
                                    .QueueSize = UInteger.MaxValue,
                                    .DisplayName = nodeId.ToString()
                                }

                                'register monitoring event
                                AddHandler monitoredItem.Notification, AddressOf Client_MonitorNotification
                                'add Item to subscription
                                subscription.AddItem(monitoredItem)
                                nodeId = client.GetNodeIdByPath("Objects.Plant.Line1.Machine1.IsRunning")
                                monitoredItem = New MonitoredItem(subscription.DefaultItem) With {
                                    .StartNodeId = nodeId,
                                    .SamplingInterval = 500,
                                    .QueueSize = UInteger.MaxValue,
                                    .DisplayName = nodeId.ToString()
                                }

                                'register monitoring event
                                AddHandler monitoredItem.Notification, AddressOf Client_MonitorNotification
                                'add Item to subscription
                                subscription.AddItem(monitoredItem)

                                'apply changes
                                subscription.ApplyChanges()

                                'enable publishing mode of subscription and set PublishingInterval
                                subscription.SetPublishingMode(True)
                                subscription.Modify()
                            Catch ex As Exception
                                Console.WriteLine(ex)
                            End Try

                            Console.WriteLine()
                            Console.WriteLine("press enter for exit")
                            Console.ReadLine()
                        End Using
                    End Using
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
            Console.WriteLine("press enter for exit")
            Console.ReadLine()
        End Try
    End Sub

    Private Sub Client_MonitorNotification(ByVal monitoredItem As MonitoredItem, ByVal e As MonitoredItemNotificationEventArgs)
        Dim notification As MonitoredItemNotification = TryCast(e.NotificationValue, MonitoredItemNotification)
        Console.WriteLine($"{monitoredItem.StartNodeId.Identifier} Value: {notification.Value} Status: {notification.Value.StatusCode.ToString()}")
    End Sub

    Private Sub Subscription_StateChanged(ByVal subscription As Subscription, ByVal e As SubscriptionStateChangedEventArgs)
        Console.WriteLine($"{Date.Now.ToLocalTime()} State of Subscription { UaClient.SubscriptionToString(subscription) } changed to => { e.Status.ToString()}")
    End Sub

    Private Sub Subscription_PublishStatusChanged(ByVal sender As Object, ByVal e As EventArgs)
        ' 
        ' check your publish state of your subscription
        ' if the publish state permanent stopped, then you have to recreate your subscription with old subscription as template
        ' In this case, please have a look to the PublishingInterval setting, possibly be the value must be increased
        ' 

        Dim subscription As Subscription = TryCast(sender, Subscription)

        If subscription IsNot Nothing Then
            Dim currentpublishingState As n_PublishingState = If(subscription.PublishingStopped, n_PublishingState.STOPPED, n_PublishingState.RUNNING)
            If currentpublishingState <> publishingState OrElse currentpublishingState = n_PublishingState.STOPPED Then Console.WriteLine($"{Date.Now.ToLocalTime() } Publishing state of Subscription { UaClient.SubscriptionToString(CType(sender, Subscription)) } => { currentpublishingState.ToString()}")
            publishingState = currentpublishingState
        End If
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

    Private Enum n_PublishingState
        UNDEFINED
        RUNNING
        STOPPED
    End Enum
End Class
