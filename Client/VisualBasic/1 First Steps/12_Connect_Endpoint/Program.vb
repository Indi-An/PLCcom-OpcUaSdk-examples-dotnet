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
' PLCcom OPC UA Client SDK - Workshop 12: Connect to Endpoint
'
' Demonstrates the full connect/disconnect lifecycle of a UaClient session.
' All available endpoints are discovered, displayed and the user selects one
' interactively. For secured endpoints an application instance certificate is
' created automatically on first run and reused on subsequent runs.
'
' What you will learn:
'   * How to discover and sort endpoints by security level
'   * How to select an endpoint interactively
'   * How to create a SessionConfiguration from an endpoint
'   * How to register event handlers (Connected, ConnectionLost, KeepAlive)
'   * How to handle server certificate validation
'   * How to connect and disconnect cleanly
'
' Target server: opc.tcp://localhost:48410
' (Start any Server SDK workshop first, e.g. Server Workshop 11)
' ==============================================================================

Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Client
Imports PLCcom.Opc.Ua.Client.Sdk
Imports System

Public Class Program

    Public Shared Sub Main(ByVal args As String())
        Dim p As New Program()
        p.Start()
    End Sub

    Private Sub Start()

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 12: Connect Endpoint    ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Connecting to an OPC UA server requires discovering its     ║")
        Console.WriteLine("║  endpoints first and selecting the right one. This workshop  ║")
        Console.WriteLine("║  shows the full connect/disconnect lifecycle.                ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  What you will learn:                                        ║")
        Console.WriteLine("║    * Discover and sort endpoints by security level           ║")
        Console.WriteLine("║    * Create a SessionConfiguration from an endpoint          ║")
        Console.WriteLine("║    * Register KeepAlive and ConnectionState events           ║")
        Console.WriteLine("║    * Handle server certificate validation                    ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Required server: Server Workshop 11 (Simple Server)         ║")
        Console.WriteLine("║  opc.tcp://localhost:48410                                   ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Try
            ' -- License ----------------------------------------------------------
            ' Important !!!!!!!!!!!!!!!!!!
            ' Enter your Username + Serial here! Please note: with blank fields the library runs
            ' for 15 minutes during a debug session. Both values can also come
            ' from configuration or an environment variable.
            ' Free trial license (14 days, uninterrupted): https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-download/
            Dim LicenseUserName As String = ""
            Dim LicenseSerial As String = ""

            ' -- Step 1: Discover and sort endpoints ------------------------------
            ' GetEndpoints() queries the server for all available endpoints.
            ' SortEndpointsBySecurityLevel() puts the least secure (None) first,
            ' making index 0 the easiest to connect to for testing.
            Dim serverUrl As String = "opc.tcp://localhost:48410"

            Console.WriteLine("  Server URL: " & serverUrl)
            Console.WriteLine("  Discovering endpoints...")
            Console.WriteLine()

            Dim endpoints As EndpointDescriptionCollection = UaClient.GetEndpoints(
                New Uri(serverUrl), certificateValidator:=AddressOf CertificateValidationHandler)
            endpoints = UaClient.SortEndpointsBySecurityLevel(endpoints)

            If endpoints.Count = 0 Then
                Console.WriteLine("  No endpoints found. Is the server running?")
                Console.ReadLine()
                Return
            End If

            ' -- Step 2: Display endpoints and let user choose --------------------
            Console.WriteLine($"  {endpoints.Count} endpoint(s) found:")
            Console.WriteLine()
            For i As Integer = 0 To endpoints.Count - 1
                Console.WriteLine($"  [{i}] {endpoints(i).ToDisplayString()}")
            Next

            Console.WriteLine()
            Console.Write("  Please enter index of desired endpoint: ")
            Dim input As String = Console.ReadLine()
            Dim index As Integer = -1
            If Not Integer.TryParse(input, index) OrElse index < 0 OrElse index >= endpoints.Count Then
                Console.WriteLine("  Invalid endpoint index.")
                Console.ReadLine()
                Return
            End If

            Console.WriteLine()
            Console.WriteLine($"  Selected: {endpoints(index).ToDisplayString()}")
            Console.WriteLine()

            ' -- Step 3: Build SessionConfiguration -------------------------------
            ' CreateConfig() builds the SessionConfiguration for the selected endpoint.
            ' It handles certificate creation/loading automatically based on the
            ' endpoint's security mode and transport protocol.
            Dim sessionConfig As SessionConfiguration = CreateConfig(endpoints(index))
            PrintConfig(sessionConfig)

            ' -- Step 4: Create client and register events ------------------------
            Dim client As New UaClient(LicenseUserName, LicenseSerial, sessionConfig)
            Console.WriteLine("  License: " & client.GetLicenceMessage())
            Console.WriteLine()

            ' ServerConnected fires when the session is established.
            AddHandler client.ServerConnected, Sub(s, e)
                Console.WriteLine($"  [Connected] {DateTime.Now:HH:mm:ss} Session established")
            End Sub

            ' ServerConnectionLost fires when the connection drops unexpectedly.
            ' The SDK will attempt automatic reconnection.
            AddHandler client.ServerConnectionLost, Sub(s, e)
                Console.WriteLine($"  [ConnectionLost] {DateTime.Now:HH:mm:ss} Connection lost")
            End Sub

            AddHandler client.KeepAlive, Sub(session, e)
            End Sub

            ' Accept all client certificates automatically.
            ' WARNING: Do NOT use this in production! Either implement your own validation
            ' logic here (inspect e.Certificate and e.Error, then set e.Accept = True or False),
            ' or remove this handler entirely -- the SDK will then automatically validate
            ' certificates against the PKI trust store (pki/trusted/certs/).
            AddHandler client.CertificateValidation, AddressOf CertificateValidationHandler

            ' -- Step 5: Connect --------------------------------------------------
            Console.Write("  Connecting ... ")
            client.Connect()
            Console.WriteLine("OK")
            Console.WriteLine($"  Session state: {client.GetSessionState()}")
            Console.WriteLine()

            ' -- Step 6: Disconnect -----------------------------------------------
            Console.WriteLine("  Press ENTER to disconnect and exit.")
            Console.ReadLine()

            If client.GetSessionState() = SessionState.Connected Then
                client.Disconnect()
            End If

            Console.WriteLine("  Disconnected.")

        Catch ex As Exception
            Console.WriteLine("  Error: " & ex.Message)
            Console.WriteLine()
            Console.WriteLine("  Press ENTER to exit.")
            Console.ReadLine()
        End Try

    End Sub

    ' ── Event handlers ────────────────────────────────────────────────────────

    Private Sub CertificateValidationHandler(ByVal sender As CertificateValidator, ByVal e As CertificateValidationEventArgs)
        ' Called when the server presents its certificate during the secure channel
        ' handshake. Inspect e.Certificate and e.Error, then set e.Accept accordingly.
        ' For development we accept all certificates here.
        e.Accept = True
        Console.WriteLine($"  [Certificate] Accepted: {e.Certificate.Subject}")
    End Sub

    ' ── Helpers ───────────────────────────────────────────────────────────────

    ' =============================================================================
    ' Helper: CreateConfig
    ' =============================================================================
    ' Builds the SessionConfiguration for the selected endpoint.
    '
    ' Certificate handling:
    '   Application certificate -- required for Sign / SignAndEncrypt endpoints.
    '   HTTPS certificate       -- required for opc.https:// endpoints (any SecurityMode).
    '
    ' UaClientCertificate derives file paths automatically from the PKI base directory:
    '   pki/own/certs/<alias>.der    <- certificate
    '   pki/own/private/<alias>.pem  <- private key
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
