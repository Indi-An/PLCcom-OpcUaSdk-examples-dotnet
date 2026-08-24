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
' PLCcom OPC UA Client SDK - Workshop 14: Connect with Certificate Authentication
'
' Workshop 13 used username/password. For machine-to-machine communication,
' X.509 certificate authentication is more secure and does not require
' storing passwords. The client presents a certificate and the server
' validates it against its trusted certificate store.
'
' OPC UA supports three user identity types:
'   Anonymous   - no credentials (see Workshop 12)
'   UserName    - classic username + password (see Workshop 13)
'   Certificate - X.509 client certificate (this workshop)
'
' What you will learn:
'   * How to load or create an X.509 user certificate with UaClientCertificate
'   * How to set certificate-based UserIdentity on a session
'   * How certificate authentication differs from username/password
'   * How the server validates the user certificate
'
' Target server: opc.tcp://localhost:48410
' (Start Server Workshop 12 for a server that accepts certificate authentication)
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
        Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 14: Certificate Auth    ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  For machine-to-machine communication, X.509 certificate     ║")
        Console.WriteLine("║  authentication is more secure than username/password.       ║")
        Console.WriteLine("║  The client presents a certificate that the server validates ║")
        Console.WriteLine("║  against its trusted certificate store.                      ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  What you will learn:                                        ║")
        Console.WriteLine("║    * Load or create an X.509 user certificate                ║")
        Console.WriteLine("║    * Set certificate-based UserIdentity on a session         ║")
        Console.WriteLine("║    * Difference to username/password authentication          ║")
        Console.WriteLine("║                                                              ║")
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

            ' -- Step 1: Discover and select endpoint -----------------------------
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

            ' -- Step 2: Load or create the user certificate ----------------------
            ' The user certificate identifies the user to the server.
            ' It is separate from the application instance certificate (which
            ' identifies the client application for the secure channel).
            ' The server must trust this certificate -- add it to its trusted store.
            Dim userCert As UaClientCertificate = UaClientCertificate.Load("./pki", "PLCcom_Workshop_14_User", "secretpassword")
            If userCert Is Nothing OrElse Not userCert.CheckValidity() Then
                userCert = New UaClientCertificate("./pki", "secretpassword", "PLCcom_Workshop_14_User", 720, "Indi.An GmbH") _
                    .Build(overwrite:=True)
            End If

            Console.WriteLine($"  User certificate: {userCert}")
            Console.WriteLine()

            ' -- Step 3: Build SessionConfiguration with certificate identity -----
            Dim sessionConfig As SessionConfiguration = CreateConfig(endpoints(index), userCert)
            PrintConfig(sessionConfig)

            ' -- Step 4: Create client and register events ------------------------
            Dim client As New UaClient(LicenseUserName, LicenseSerial, sessionConfig)
            Console.WriteLine("  License: " & client.GetLicenceMessage())
            Console.WriteLine()

            AddHandler client.ServerConnected, Sub(s, e)
                Console.WriteLine($"  [Connected] {DateTime.Now:HH:mm:ss} Session established")
            End Sub
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
            Console.Write("  Connecting with certificate ... ")
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
    ' Sets the UserIdentity to use the provided user certificate.
    Private Shared Function CreateConfig(ByVal endpoint As EndpointDescription,
                                          ByVal userCert As UaClientCertificate) As SessionConfiguration
        Dim appAlias As String = System.Reflection.Assembly.GetEntryAssembly().GetName().Name
        Dim config As SessionConfiguration = SessionConfiguration.Build(appAlias, endpoint)
        config.AutoConnect = False

        ' Set certificate-based user identity.
        ' The server validates this certificate against its trusted user certificate store.
        config.Identity = New UserIdentity(userCert.GetCertificate())

        ' HTTPS certificate -- required for opc.https:// endpoints.
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

        ' Application certificate -- required for secured endpoints.
        Dim appCert As UaClientCertificate = Nothing
        If Not endpoint.SecurityMode.Equals(MessageSecurityMode.None) Then
            appCert = UaClientCertificate.Load("./pki", appAlias, "secretpassword")
            If appCert Is Nothing OrElse Not appCert.CheckValidity() Then
                appCert = New UaClientCertificate("./pki", "secretpassword", appAlias, 720, "Indi.An GmbH") _
                    .Build(overwrite:=True)
            End If
        End If

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
    Private Shared Sub PrintConfig(ByVal config As SessionConfiguration)
        Console.WriteLine("-- Active Client Configuration ------------------------------")
        If config.Endpoint IsNot Nothing Then
            Console.WriteLine("  Endpoint  : " & config.Endpoint.EndpointUrl)
            Console.WriteLine("  Security  : " & config.Endpoint.ToDisplayString())
        End If
        Console.WriteLine("  Identity  : Certificate")
        Console.WriteLine("  PKI Store : " & If(config.CertificateStorePath IsNot Nothing, config.CertificateStorePath, "(not set)"))
        Console.WriteLine("  Cert File : " & If(config.ApplicationCertificateFullPath IsNot Nothing, config.ApplicationCertificateFullPath, "(none -- SecurityMode.None)"))
        Console.WriteLine("-------------------------------------------------------------")
        Console.WriteLine()
    End Sub

End Class
