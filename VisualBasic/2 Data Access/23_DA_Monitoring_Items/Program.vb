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
            'Submit your license information from your license e-mail
            Dim LicenseUserName As String = "<Enter your UserName here>"
            Dim LicenseSerial As String = "<Enter your Serial here>"

            Dim Endpoints As EndpointDescriptionCollection = UaClient.GetEndpoints(New Uri("opc.tcp://localhost:50520/PLCcom/DataAccessServer"), 60000)

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
                    Using client As UaClient = New UaClient(LicenseUserName, LicenseSerial, sessionConfiguration)
                        Console.WriteLine($"Info: license state => { client.GetLicenceMessage()}")
                        Console.WriteLine("")

                        'Register event handlers to monitor the connection state
                        AddHandler client.ServerConnectionLost, AddressOf Client_ServerConnectionLost
                        AddHandler client.ServerConnected, AddressOf Client_ServerConnected
                        AddHandler client.SessionClosing, AddressOf Client_SessionClosing
                        AddHandler client.KeepAlive, AddressOf Client_KeepAlive
                        AddHandler client.CertificateValidation, AddressOf client_CertificateValidation

                        'Create a new subscription for monitoring data changes
                        Using subscription As Subscription = New Subscription()
                            subscription.PublishingInterval = 1000
                            subscription.PublishingEnabled = False
                            subscription.DisplayName = "mySubsription"

                            'Register subscription state change events
                            AddHandler subscription.StateChanged, AddressOf Subscription_StateChanged
                            AddHandler subscription.PublishStatusChanged, AddressOf Subscription_PublishStatusChanged

                            'Add the subscription to the client instance
                            client.AddSubscription(subscription)

                            Try
                                'Create a monitoring item and add to the subscription
                                Dim nodeId As NodeId = client.GetNodeIdByPath("Objects.Data.Dynamic.Scalar.Int64Value")
                                Dim monitoredItem As MonitoredItem = New MonitoredItem(subscription.DefaultItem) With {
                                    .StartNodeId = nodeId,
                                    .SamplingInterval = 500,
                                    .QueueSize = UInteger.MaxValue,
                                    .DisplayName = nodeId.ToString()
                                }

                                'Register the notification callback for value changes
                                AddHandler monitoredItem.Notification, AddressOf Client_MonitorNotification
                                'Add the monitored item to the subscription
                                subscription.AddItem(monitoredItem)
                                nodeId = client.GetNodeIdByPath("Objects.Data.Dynamic.Scalar.BooleanValue")
                                monitoredItem = New MonitoredItem(subscription.DefaultItem) With {
                                    .StartNodeId = nodeId,
                                    .SamplingInterval = 500,
                                    .QueueSize = UInteger.MaxValue,
                                    .DisplayName = nodeId.ToString()
                                }

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

    Private Enum n_PublishingState
        UNDEFINED
        RUNNING
        STOPPED
    End Enum
End Class
