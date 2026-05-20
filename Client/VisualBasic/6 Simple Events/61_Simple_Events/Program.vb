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
' PLCcom OPC UA Client SDK - Workshop 61: Simple Events
'
' OPC UA Events are notifications about discrete occurrences -
' not value changes, but things that happened (state transitions,
' warnings, operator actions). This workshop subscribes to events
' and displays them as they arrive.
'
' What you will learn:
'   * How to create an event subscription
'   * How to define event filters (which fields to receive)
'   * How to receive and display event notifications
'   * How to read event properties (message, severity, source)
'
' Target server: opc.tcp://localhost:48410
' ==============================================================================

Imports System
Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Client
Imports PLCcom.Opc.Ua.Client.Sdk

Module Program

    'actual publishing state of subscription
    Private currentPublishState As PublishingState = PublishingState.UNDEFINED

    Sub Main(args As String())
        Start()
    End Sub

    Sub Start()
        Try
            Console.WriteLine()

            Console.WriteLine()

             Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
             Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 61: Simple Events       ║")
             Console.WriteLine("║                                                              ║")
             Console.WriteLine("║  OPC UA Events are notifications about discrete              ║")
             Console.WriteLine("║  occurrences - not value changes, but things that            ║")
             Console.WriteLine("║  happened. Subscribe to events and display them.             ║")
             Console.WriteLine("║                                                              ║")
             Console.WriteLine("║  What you will learn:                                        ║")
             Console.WriteLine("║    * Create an event subscription with filters               ║")
             Console.WriteLine("║    * Receive and display event notifications                 ║")
             Console.WriteLine("║    * Read event properties (message, severity, source)       ║")
             Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Required server: Server Workshop 61 (Simple Events)         ║")
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
                    Console.WriteLine(counter.ToString() & " => " & Endpoint.ToDisplayString())
                    counter += 1
                Next

                Console.WriteLine("please enter index of desired endpoint")
                Dim NumberOfEndpoint As String = Console.ReadLine()
                Console.WriteLine("")

                Dim iNumberOfEndpoint As Integer = -1
                If Integer.TryParse(NumberOfEndpoint, iNumberOfEndpoint) AndAlso iNumberOfEndpoint > -1 AndAlso iNumberOfEndpoint < Endpoints.Count Then

                    'create a SessionConfiguration with the selected endpoint and application name
                    Dim sessionConfiguration As SessionConfiguration = CreateConfig(Endpoints(iNumberOfEndpoint))
                    PrintConfig(sessionConfiguration)

                    'enable auto connect functionality
                    sessionConfiguration.AutoConnect = True

                    'output certificate store path
                    Console.WriteLine("Info: Sessionconfiguration created, certificate store path => " & sessionConfiguration.CertificateStorePath)

                    'Create a new opc client instance and pass your license information
                    Using client As New UaClient(LicenseUserName, LicenseSerial, sessionConfiguration)

                        Console.WriteLine("Info: license state => " & client.GetLicenceMessage())
                        Console.WriteLine("")

                        'register events
                        AddHandler client.ServerConnectionLost, AddressOf Client_ServerConnectionLost
                        AddHandler client.ServerConnected, AddressOf Client_ServerConnected
                        AddHandler client.SessionClosing, AddressOf Client_SessionClosing
                        AddHandler client.KeepAlive, AddressOf Client_KeepAlive
                        AddHandler client.CertificateValidation, AddressOf Client_CertificateValidation

                        'create a new subscription
                        Using subscription As New Subscription()

                            subscription.PublishingInterval = 1000
                            subscription.PublishingEnabled = False
                            subscription.DisplayName = "mySimpleEventClientSubsc"

                            'register subscription events
                            AddHandler subscription.StateChanged, AddressOf Subscription_StateChanged
                            AddHandler subscription.PublishStatusChanged, AddressOf Subscription_PublishStatusChanged

                            'add new subscription to client
                            client.AddSubscription(subscription)

                            Try
                                'Create a monitoring item and add to the subscription
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
                                            },
                                            New SimpleAttributeOperand() With {
                                                .TypeDefinitionId = ObjectTypeIds.BaseEventType,
                                                .BrowsePath = New QualifiedNameCollection From {BrowseNames.SourceName},
                                                .AttributeId = Attributes.Value
                                            }
                                        }
                                    }
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

    Private Sub Client_MonitorNotification(monitoredItem As MonitoredItem, e As MonitoredItemNotificationEventArgs)
        'Events:
        Dim ev As EventFieldList = TryCast(e.NotificationValue, EventFieldList)
        If ev IsNot Nothing Then
            'Sequence corresponds to SelectClauses (Message, Severity, Time, SourceName)
            Dim message As LocalizedText = TryCast(ev.EventFields(0).Value, LocalizedText)
            Dim severity As UShort = If(TypeOf ev.EventFields(1).Value Is UShort, CUShort(ev.EventFields(1).Value), CUShort(0))
            Dim time As DateTime = If(TypeOf ev.EventFields(2).Value Is DateTime, CDate(ev.EventFields(2).Value), DateTime.MinValue)
            Dim source As String = If(TryCast(ev.EventFields(3).Value, String), "")

            Console.WriteLine($"  [EVENT] {time:HH:mm:ss.fff} UTC  Source={source,-16} Severity={severity,-6} {message?.Text}")
            Return
        End If

        Dim dn As MonitoredItemNotification = TryCast(e.NotificationValue, MonitoredItemNotification)
        If dn IsNot Nothing Then
            Console.WriteLine($"{monitoredItem.StartNodeId} Value: {dn.Value} Status: {dn.Value.StatusCode}")
            Return
        End If

        Console.WriteLine($"Unexpected notification type: {If(e.NotificationValue?.GetType().Name, "null")}")
    End Sub

    Private Sub Subscription_StateChanged(subscription As Subscription, e As SubscriptionStateChangedEventArgs)
        Console.WriteLine(DateTime.Now.ToLocalTime().ToString() & " State of Subscription " & subscription.ToDisplayString() & " changed to => " & e.Status.ToString())
    End Sub

    Private Sub Subscription_PublishStatusChanged(sender As Object, e As EventArgs)
        '
        'check your publish state of your subscription
        'if the publish state permanent stopped, then you have to recreate your subscription with old subscription as template
        'In this case, please have a look to the PublishingInterval setting, possibly be the value must be increased
        '

        Dim subscription As Subscription = TryCast(sender, Subscription)
        If subscription IsNot Nothing Then
            Dim currentpublishingState As PublishingState = If(subscription.PublishingStopped, PublishingState.STOPPED, PublishingState.RUNNING)
            If currentpublishingState <> currentPublishState OrElse currentpublishingState = PublishingState.STOPPED Then
                Console.WriteLine(DateTime.Now.ToLocalTime().ToString() & "Publishing state of Subscription " & subscription.ToDisplayString() & " => " & currentpublishingState.ToString())
            End If
            currentPublishState = currentpublishingState
        End If
    End Sub

    Private Sub Client_CertificateValidation(sender As CertificateValidator, e As CertificateValidationEventArgs)
        'external certificate validation
        If ServiceResult.IsGood(e.Error) Then
            e.Accept = True
        ElseIf Not e.ContainsUnsuppressibleStatusCodes Then
            e.Accept = True
        ElseIf e.ContainsUnsuppressibleStatusCodes Then
            e.AcceptAll = True 'you can accept all unsuppressible statuscode with this flag
        Else
            Throw New Exception(String.Format("Failed to validate certificate with error code {0}: {1}", e.Error.Code, e.Error.AdditionalInfo))
        End If
    End Sub

    Private Sub Client_ServerConnected(sender As Object, e As EventArgs)
        'event opc ua server is connected
        Console.WriteLine(DateTime.Now.ToLocalTime().ToString() & " Session connected")
    End Sub

    Private Sub Client_ServerConnectionLost(sender As Object, e As EventArgs)
        'event connection to opc ua server lost
        Console.WriteLine(DateTime.Now.ToLocalTime().ToString() & " Session connection lost")
    End Sub

    Private Sub Client_KeepAlive(session As ISession, e As KeepAliveEventArgs)
        'catch the keepalive event of opc ua server
    End Sub

    Private Sub Client_SessionClosing(sender As Object, e As EventArgs)
        Console.WriteLine(DateTime.Now.ToLocalTime().ToString() & " Session closed")
    End Sub

    Private Enum PublishingState
        UNDEFINED
        RUNNING
        STOPPED
    End Enum

    ' =============================================================================
    ' Helper: CreateConfig
    ' =============================================================================
    Private Function CreateConfig(ByVal endpoint As EndpointDescription) As SessionConfiguration
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
    Private Sub PrintConfig(ByVal config As SessionConfiguration)
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

End Module
