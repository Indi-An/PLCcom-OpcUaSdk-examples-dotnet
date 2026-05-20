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
' PLCcom OPC UA Client SDK - Workshop 13: Connect with User Authentication
'
' Workshop 12 connected anonymously. Many production servers require
' username/password authentication. This workshop shows how to set
' user credentials on the SessionConfiguration before connecting.
'
' OPC UA supports three user identity types:
'   Anonymous   - no credentials (see Workshop 12)
'   UserName    - classic username + password (this workshop)
'   Certificate - X.509 client certificate (see Workshop 14)
'
' What you will learn:
'   * How to set username/password credentials on a session
'   * How UserIdentity is passed to the server during ActivateSession
'   * How to handle authentication failures
'
' Target server: opc.tcp://localhost:48410
' (Start Server Workshop 12 for a server that requires authentication)
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
        Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 13: User Authentication ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Many servers require username/password authentication.      ║")
        Console.WriteLine("║  This workshop shows how to set user credentials before      ║")
        Console.WriteLine("║  connecting to the server.                                   ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  What you will learn:                                        ║")
        Console.WriteLine("║    * Set username/password on SessionConfiguration           ║")
        Console.WriteLine("║    * UserIdentity is sent during ActivateSession             ║")
        Console.WriteLine("║    * Handle authentication failures                          ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Required server: Server Workshop 12 (User Authentication)   ║")
        Console.WriteLine("║  opc.tcp://localhost:48410                                   ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Try
            ' -- License ----------------------------------------------------------
            ' TODO: Replace with your license credentials from your license e-mail
            Dim LicenseUserName As String = "<Enter your UserName here>"
            Dim LicenseSerial As String = "<Enter your Serial here>"

            ' -- Step 1: Discover and select endpoint -----------------------------
            Dim serverUrl As String = "opc.tcp://localhost:48410"

            Console.WriteLine("  Server URL: " & serverUrl)
            Console.WriteLine("  Discovering endpoints...")
            Console.WriteLine()

            Dim endpoints As EndpointDescriptionCollection = UaClient.GetEndpoints(New Uri(serverUrl), certificateValidator:=AddressOf CertificateValidationHandler)
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

            ' -- Step 2: Build SessionConfiguration with user credentials ---------
            Dim sessionConfig As SessionConfiguration = CreateConfig(endpoints(index))

            PrintConfig(sessionConfig)

            ' Set username/password authentication.
            ' The UserIdentity is sent to the server during the ActivateSession call.
            ' The server validates the credentials and assigns a role to the session.
            '
            ' Server Workshop 12 defines three users:
            '   viewer   / viewer123   -> Role.Observer  (read-only)
            '   operator / operator123 -> Role.Operator  (read + write)
            '   admin    / admin123    -> Role.Engineer  (full access)
            '
            ' The role only has effect if the server has set RolePermissions on its
            ' nodes via SetRolePermissions(). Server Workshop 12 does this - try
            ' connecting as viewer and writing a value to see BadUserAccessDenied.
            Console.Write("  Username: ")
            Dim username As String = Console.ReadLine()
            Console.Write("  Password: ")
            Dim password As String = ReadPassword()
            Console.WriteLine()
            sessionConfig.Identity = New UserIdentity(username, password)

            Console.WriteLine()

            ' -- Step 3: Create client and register events ------------------------
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
            AddHandler client.CertificateValidation, AddressOf CertificateValidationHandler

            ' -- Step 4: Connect --------------------------------------------------
            Console.Write("  Connecting with user credentials ... ")
            client.Connect()
            Console.WriteLine("OK")
            Console.WriteLine($"  Session state: {client.GetSessionState()}")
            Console.WriteLine()

            ' -- Step 5: Test role-based access -----------------------------------
            ' Read Temperature - allowed for all roles (Observer, Operator, Engineer)
            Dim temperatureId As NodeId = client.GetNodeIdByPath("Objects.Plant.Temperature")
            Dim value As Object = client.ReadValue(temperatureId)
            Console.WriteLine($"  Read  Temperature = {value}  -> OK")

            ' Write Temperature - allowed for Operator and Engineer, rejected for Observer
            ' Observer gets BadUserAccessDenied because SetRolePermissions() on the server
            ' only grants Write to Operator and Engineer.
            Dim writeResult As StatusCode = client.WriteValue(temperatureId, 99.9)
            If StatusCode.IsGood(writeResult) Then
                Console.WriteLine($"  Write Temperature = 99.9   -> OK (role allows write)")
            Else
                Console.WriteLine($"  Write Temperature = 99.9   -> {writeResult} (role does not allow write)")
            End If
            Console.WriteLine()

            ' -- Step 6: Test method call -----------------------------------------
            ' Call Reset - allowed for Operator and Engineer, rejected for Observer
            Dim resetId As NodeId = client.GetNodeIdByPath("Objects.Plant.Reset")
            Dim plantId As NodeId = client.GetNodeIdByPath("Objects.Plant")
            Try
                client.Call(plantId, resetId)
                Console.WriteLine($"  Call   Reset              -> OK (role allows call)")
            Catch ex As Exception
                Console.WriteLine($"  Call   Reset              -> {ex.Message} (role does not allow call)")
            End Try
            Console.WriteLine()

            ' -- Step 7: Disconnect -----------------------------------------------
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

    Private Sub CertificateValidationHandler(ByVal sender As CertificateValidator, ByVal e As CertificateValidationEventArgs)
        ' Called when the server presents its certificate - both during opc.https
        ' discovery (TLS) and when a security policy other than None is used.
        ' Inspect e.Certificate and e.Error, then set e.Accept accordingly.
        e.Accept = True
        Console.WriteLine($"  [Certificate] Accepted: {e.Certificate.Subject}")
    End Sub

    Private Shared Function ReadPassword() As String
        Dim password As New System.Text.StringBuilder()
        Dim key As ConsoleKeyInfo
        Do
            key = Console.ReadKey(intercept:=True)
            If key.Key = ConsoleKey.Backspace AndAlso password.Length > 0 Then
                password.Remove(password.Length - 1, 1)
                Console.Write(Chr(8) & " " & Chr(8))
            ElseIf key.Key <> ConsoleKey.Enter AndAlso key.Key <> ConsoleKey.Backspace Then
                password.Append(key.KeyChar)
                Console.Write("*")
            End If
        Loop While key.Key <> ConsoleKey.Enter
        Console.WriteLine()
        Return password.ToString()
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
