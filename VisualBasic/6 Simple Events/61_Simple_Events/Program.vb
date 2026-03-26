Imports System
Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Client
Imports PLCcom.Opc.Ua.Client.Sdk

Module Program

    ' Current publishing state of subscription
    Private currentPublishState As PublishingState = PublishingState.UNDEFINED

    Sub Main(args As String())
        Start()
    End Sub

    Sub Start()
        Try
            'Submit your license information from your license e-mail
            Dim LicenseUserName As String = "<Enter your UserName here>"
            Dim LicenseSerial As String = "<Enter your Serial here>"

            Dim Endpoints As EndpointDescriptionCollection = UaClient.GetEndpoints(New Uri("opc.tcp://localhost:50520/PLCcom/DataAccessServer"), 60000)

            ' Sort endpoints by security level (highest security first)
            Endpoints = UaClient.SortEndpointsBySecurityLevel(Endpoints)

            If Endpoints.Count > 0 Then
                Console.WriteLine("endpoints found:")
                Dim counter As Integer = 0
                For Each Endpoint As EndpointDescription In Endpoints
                    Console.WriteLine(counter.ToString() & " => " & UaClient.EndpointToString(Endpoint))
                    counter += 1
                Next

                Console.WriteLine("please enter index of desired endpoint")
                Dim NumberOfEndpoint As String = Console.ReadLine()
                Console.WriteLine("")

                Dim iNumberOfEndpoint As Integer = -1
                If Integer.TryParse(NumberOfEndpoint, iNumberOfEndpoint) AndAlso iNumberOfEndpoint > -1 AndAlso iNumberOfEndpoint < Endpoints.Count Then

                    ' Create a SessionConfiguration with the selected endpoint and application name
                    Dim sessionConfiguration As SessionConfiguration = SessionConfiguration.Build(
                        Reflection.Assembly.GetEntryAssembly().GetName().Name,
                        Endpoints(iNumberOfEndpoint))

                    ' Enable auto connect functionality
                    sessionConfiguration.AutoConnect = True

                    ' Output certificate store path
                    Console.WriteLine("Info: Sessionconfiguration created, certificate store path => " & sessionConfiguration.CertificateStorePath)

                    ' Create a new OPC UA client instance and pass your license information
                    Using client As New UaClient(LicenseUserName, LicenseSerial, sessionConfiguration)

                        Console.WriteLine("Info: license state => " & client.GetLicenceMessage())
                        Console.WriteLine("")

                        ' Register events
                        AddHandler client.ServerConnectionLost, AddressOf Client_ServerConnectionLost
                        AddHandler client.ServerConnected, AddressOf Client_ServerConnected
                        AddHandler client.SessionClosing, AddressOf Client_SessionClosing
                        AddHandler client.KeepAlive, AddressOf Client_KeepAlive
                        AddHandler client.CertificateValidation, AddressOf Client_CertificateValidation

                        ' Create a new subscription
                        Using subscription As New Subscription()

                            subscription.PublishingInterval = 1000
                            subscription.PublishingEnabled = False
                            subscription.DisplayName = "mySimpleEventClientSubsc"

                            ' Register subscription events
                            AddHandler subscription.StateChanged, AddressOf Subscription_StateChanged
                            AddHandler subscription.PublishStatusChanged, AddressOf Subscription_PublishStatusChanged

                            ' Add new subscription to client
                            client.AddSubscription(subscription)

                            Try
                                ' Create a monitoring item for server events and add to the subscription
                                Dim nodeId As NodeId = client.GetNodeIdByPath("Objects.Server")
                                Dim monitoredItem As New MonitoredItem(subscription.DefaultItem) With {
                                    .StartNodeId = nodeId,
                                    .AttributeId = Attributes.EventNotifier,
                                    .SamplingInterval = 0,
                                    .QueueSize = UInt32.MaxValue,
                                    .DisplayName = nodeId.ToString(),
                                    .DiscardOldest = True,
                                    .Filter = New EventFilter() With {
                                        .SelectClauses = New SimpleAttributeOperandCollection From {
                                            New SimpleAttributeOperand() With {
                                                .TypeDefinitionId = ObjectTypeIds.BaseEventType,
                                                .BrowsePath = New QualifiedNameCollection From {BrowseNames.Message},
                                                .AttributeId = Attributes.Value
                                            },
                                            New SimpleAttributeOperand() With {
                                                .TypeDefinitionId = ObjectTypeIds.BaseEventType,
                                                .BrowsePath = New QualifiedNameCollection From {BrowseNames.Severity},
                                                .AttributeId = Attributes.Value
                                            },
                                            New SimpleAttributeOperand() With {
                                                .TypeDefinitionId = ObjectTypeIds.BaseEventType,
                                                .BrowsePath = New QualifiedNameCollection From {BrowseNames.Time},
                                                .AttributeId = Attributes.Value
                                            }
                                        }
                                    }
                                }

                                ' Register monitoring event callback
                                AddHandler monitoredItem.Notification, AddressOf Client_MonitorNotification

                                ' Add item to subscription
                                subscription.AddItem(monitoredItem)

                                ' Apply changes to the subscription
                                subscription.ApplyChanges()

                                ' Enable publishing mode and apply modified settings
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

    ''' <summary>
    ''' Callback for event notifications from the server.
    ''' Processes EventFieldList (events) and MonitoredItemNotification (data changes).
    ''' </summary>
    Private Sub Client_MonitorNotification(monitoredItem As MonitoredItem, e As MonitoredItemNotificationEventArgs)
        ' Handle event notifications
        Dim ev As EventFieldList = TryCast(e.NotificationValue, EventFieldList)
        If ev IsNot Nothing Then
            ' Field sequence corresponds to SelectClauses: Message, Severity, Time
            Dim message As LocalizedText = TryCast(ev.EventFields(0).Value, LocalizedText)
            Dim severity As UShort = If(TypeOf ev.EventFields(1).Value Is UShort, CUShort(ev.EventFields(1).Value), CUShort(0))
            Dim time As DateTime = If(TypeOf ev.EventFields(2).Value Is DateTime, CDate(ev.EventFields(2).Value), DateTime.MinValue)

            Console.WriteLine($"[{time:O}] Sev={severity} | {message?.Text}")
            Return
        End If

        ' Handle data change notifications
        Dim dn As MonitoredItemNotification = TryCast(e.NotificationValue, MonitoredItemNotification)
        If dn IsNot Nothing Then
            Console.WriteLine($"{monitoredItem.StartNodeId} Value: {dn.Value} Status: {dn.Value.StatusCode}")
            Return
        End If

        Console.WriteLine($"Unexpected notification type: {If(e.NotificationValue?.GetType().Name, "null")}")
    End Sub

    Private Sub Subscription_StateChanged(subscription As Subscription, e As SubscriptionStateChangedEventArgs)
        Console.WriteLine(DateTime.Now.ToLocalTime().ToString() & " State of Subscription " & UaClient.SubscriptionToString(subscription) & " changed to => " & e.Status.ToString())
    End Sub

    Private Sub Subscription_PublishStatusChanged(sender As Object, e As EventArgs)
        ' Check your publish state of your subscription.
        ' If the publish state permanently stopped, then you have to recreate your subscription
        ' with old subscription as template.
        ' In this case, please have a look to the PublishingInterval setting,
        ' possibly the value must be increased.

        Dim subscription As Subscription = TryCast(sender, Subscription)
        If subscription IsNot Nothing Then
            Dim currentpublishingState As PublishingState = If(subscription.PublishingStopped, PublishingState.STOPPED, PublishingState.RUNNING)
            If currentpublishingState <> currentPublishState OrElse currentpublishingState = PublishingState.STOPPED Then
                Console.WriteLine(DateTime.Now.ToLocalTime().ToString() & " Publishing state of Subscription " & UaClient.SubscriptionToString(DirectCast(sender, Subscription)) & " => " & currentpublishingState.ToString())
            End If
            currentPublishState = currentpublishingState
        End If
    End Sub

    Private Sub Client_CertificateValidation(sender As CertificateValidator, e As CertificateValidationEventArgs)
        ' External certificate validation
        If ServiceResult.IsGood(e.Error) Then
            e.Accept = True
        ElseIf Not e.ContainsUnsuppressibleStatusCodes Then
            e.Accept = True
        ElseIf e.ContainsUnsuppressibleStatusCodes Then
            e.AcceptAll = True
        Else
            Throw New Exception(String.Format("Failed to validate certificate with error code {0}: {1}", e.Error.Code, e.Error.AdditionalInfo))
        End If
    End Sub

    Private Sub Client_ServerConnected(sender As Object, e As EventArgs)
        Console.WriteLine(DateTime.Now.ToLocalTime().ToString() & " Session connected")
    End Sub

    Private Sub Client_ServerConnectionLost(sender As Object, e As EventArgs)
        Console.WriteLine(DateTime.Now.ToLocalTime().ToString() & " Session connection lost")
    End Sub

    Private Sub Client_KeepAlive(session As ISession, e As KeepAliveEventArgs)
        ' Catch the keepalive event of OPC UA server
    End Sub

    Private Sub Client_SessionClosing(sender As Object, e As EventArgs)
        Console.WriteLine(DateTime.Now.ToLocalTime().ToString() & " Session closed")
    End Sub

    Private Enum PublishingState
        UNDEFINED
        RUNNING
        STOPPED
    End Enum

End Module
