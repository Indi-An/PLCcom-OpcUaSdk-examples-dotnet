Imports System
Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Client
Imports PLCcom.Opc.Ua.Client.Sdk

' ── Workshop 71: Reverse Connect ─────────────────────────────────────────────
'
' In a standard OPC UA connection the CLIENT connects to the SERVER.
' Reverse Connect turns this around: the SERVER connects to the CLIENT.
'
' This is useful when the server is behind a firewall or NAT and cannot
' accept incoming TCP connections, but is allowed to make outgoing ones.
'
' How it works:
'   1. The client opens a listening port (e.g. opc.tcp://localhost:48500).
'   2. The server periodically sends a ReverseHello message to that port.
'   3. The client receives the ReverseHello and establishes the OPC UA session
'      over the server-initiated TCP connection.
'
' From the application perspective the API is almost identical to a normal
' Connect() - just two extra calls:
'   - StartReverseConnectListener(listenUrl)   ... open the listening port
'   - ConnectReverse(timeout)                  ... wait for ReverseHello + open session
'
' Prerequisites:
'   - A server with Reverse Connect enabled pointing to this client's listen URL.
'     Use the ReverseConnect_Server test project or Workshop Server 61.
'
' ─────────────────────────────────────────────────────────────────────────────

Public Class Program

    Public Shared Sub Main(ByVal args As String())
        Dim p As Program = New Program()
        p.Start()
    End Sub

    Private Sub Start()
        Try
            ' TODO: Submit your license information from your license e-mail
            Dim LicenseUserName As String = "<Enter your UserName here>"
            Dim LicenseSerial As String = "<Enter your Serial here>"

            ' The URL where this client will listen for incoming ReverseHello messages.
            ' The server must be configured to connect to exactly this URL.
            Dim listenUrl As String = "opc.tcp://localhost:48500"

            ' The server's own endpoint URL - used to identify which server we expect
            ' and to configure the OPC UA session (security mode, policy, etc.).
            ' This is the server's normal endpoint, NOT the reverse-connect listen URL.
            Dim endpoint As EndpointDescription = New EndpointDescription With {
                .EndpointUrl = "opc.tcp://localhost:48460",
                .SecurityMode = MessageSecurityMode.None,
                .SecurityPolicyUri = SecurityPolicies.None,
                .TransportProfileUri = Profiles.UaTcpTransport
            }

            ' Build the session configuration.
            ' AutoConnect = False because we manage the connection manually via ConnectReverse().
            Dim sessionConfiguration As SessionConfiguration = SessionConfiguration.Build(
                sessionName:="71_ReverseConnect",
                endpoint:=endpoint)
            sessionConfiguration.AutoConnect = False
            sessionConfiguration.AutoAcceptUntrustedCertificates = True

            Console.WriteLine("Info: SessionConfiguration created.")
            Console.WriteLine()

            Using client As UaClient = New UaClient(LicenseUserName, LicenseSerial, sessionConfiguration)
                Console.WriteLine("Info: license state => " & client.GetLicenceMessage())
                Console.WriteLine()

                ' Register event handlers
                AddHandler client.ServerConnected, AddressOf Client_ServerConnected
                AddHandler client.ServerConnectionLost, AddressOf Client_ServerConnectionLost
                AddHandler client.SessionClosing, AddressOf Client_SessionClosing
                AddHandler client.KeepAlive, AddressOf Client_KeepAlive
                AddHandler client.CertificateValidation, AddressOf Client_CertificateValidation

                ' Step 1: Open the listening port.
                '         The server will connect here and send a ReverseHello.
                client.StartReverseConnectListener(listenUrl)
                Console.WriteLine("Listening for ReverseHello on: " & listenUrl)
                Console.WriteLine("Waiting for server to connect (timeout 60s)...")
                Console.WriteLine()

                ' Step 2: Wait for the ReverseHello and establish the session.
                '         This call blocks until the server connects or the timeout expires.
                '         Internally it mirrors Connect() - same certificate and security logic.
                client.ConnectReverse(timeout:=60000)

                Console.WriteLine("Session established: " & client.GetSession().SessionName)
                Console.WriteLine()

                ' Step 3: Subscribe to a node and monitor its value.
                '         After this point the client works exactly like after a normal Connect().
                Dim nodeId As NodeId = client.GetNodeIdByPath("Objects.Plant.Temperature")

                Using subscription As Subscription = New Subscription()
                    subscription.PublishingInterval = 1000
                    subscription.PublishingEnabled = False
                    subscription.DisplayName = "ReverseConnectSubscription"

                    AddHandler subscription.StateChanged, AddressOf Subscription_StateChanged
                    AddHandler subscription.PublishStatusChanged, AddressOf Subscription_PublishStatusChanged

                    client.AddSubscription(subscription)

                    Dim monitoredItem As MonitoredItem = New MonitoredItem(subscription.DefaultItem) With {
                        .StartNodeId = nodeId,
                        .SamplingInterval = 500,
                        .QueueSize = UInteger.MaxValue,
                        .DisplayName = "Temperature"
                    }

                    AddHandler monitoredItem.Notification, AddressOf Client_MonitorNotification
                    subscription.AddItem(monitoredItem)
                    subscription.ApplyChanges()
                    subscription.SetPublishingMode(True)
                    subscription.Modify()

                    Console.WriteLine("Monitoring Temperature - press ENTER to exit.")
                    Console.ReadLine()
                End Using

                client.Disconnect()
            End Using

        Catch ex As TimeoutException
            Console.WriteLine("Timeout: " & ex.Message)
            Console.WriteLine("Is the server running and configured for Reverse Connect?")
        Catch ex As Exception
            Console.WriteLine(ex)
        Finally
            Console.WriteLine("Press ENTER to exit.")
            Console.ReadLine()
        End Try
    End Sub

    Private Sub Client_MonitorNotification(ByVal monitoredItem As MonitoredItem, ByVal e As MonitoredItemNotificationEventArgs)
        Dim notification As MonitoredItemNotification = TryCast(e.NotificationValue, MonitoredItemNotification)
        If notification IsNot Nothing Then
            Console.WriteLine(monitoredItem.DisplayName & " = " & notification.Value.Value.ToString() _
                & "  (" & notification.Value.StatusCode.ToString() & ")" _
                & "  [" & notification.Value.SourceTimestamp.ToLocalTime().ToString("HH:mm:ss") & "]")
        End If
    End Sub

    Private Sub Subscription_StateChanged(ByVal subscription As Subscription, ByVal e As SubscriptionStateChangedEventArgs)
        Console.WriteLine(Date.Now.ToLocalTime() & " Subscription state => " & e.Status.ToString())
    End Sub

    Private Sub Subscription_PublishStatusChanged(ByVal sender As Object, ByVal e As EventArgs)
        ' If publishing stops permanently, recreate the subscription.
        ' Consider increasing PublishingInterval if this happens frequently.
        Dim subscription As Subscription = TryCast(sender, Subscription)
        If subscription IsNot Nothing AndAlso subscription.PublishingStopped Then
            Console.WriteLine(Date.Now.ToLocalTime() & " Publishing STOPPED for: " _
                & UaClient.SubscriptionToString(subscription))
        End If
    End Sub

    Private Sub Client_CertificateValidation(ByVal sender As CertificateValidator, ByVal e As CertificateValidationEventArgs)
        If ServiceResult.IsGood(e.Error) Then
            e.Accept = True
        ElseIf Not e.ContainsUnsuppressibleStatusCodes Then
            e.Accept = True
        ElseIf e.ContainsUnsuppressibleStatusCodes Then
            e.AcceptAll = True
        Else
            Throw New Exception(String.Format("Failed to validate certificate with error code {0}: {1}",
                e.Error.Code, e.Error.AdditionalInfo))
        End If
    End Sub

    Private Sub Client_ServerConnected(ByVal sender As Object, ByVal e As EventArgs)
        Console.WriteLine(Date.Now.ToLocalTime() & " Session connected.")
    End Sub

    Private Sub Client_ServerConnectionLost(ByVal sender As Object, ByVal e As EventArgs)
        Console.WriteLine(Date.Now.ToLocalTime() & " Session connection lost.")
    End Sub

    Private Sub Client_KeepAlive(ByVal session As ISession, ByVal e As KeepAliveEventArgs)
        ' Called periodically to confirm the server is still reachable.
    End Sub

    Private Sub Client_SessionClosing(ByVal sender As Object, ByVal e As EventArgs)
        Console.WriteLine(Date.Now.ToLocalTime() & " Session closing.")
    End Sub

End Class
