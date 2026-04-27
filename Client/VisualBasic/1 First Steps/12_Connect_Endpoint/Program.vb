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
' interactively. For secured endpoints the SDK creates an application
' instance certificate automatically.
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
            ' TODO: Replace with your license credentials from your license e-mail
            Dim LicenseUserName As String = "<Enter your UserName here>"
            Dim LicenseSerial As String = "<Enter your Serial here>"

            ' -- Step 1: Discover and sort endpoints ------------------------------
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
            Dim sessionConfig As SessionConfiguration = SessionConfiguration.Build(
                "PLCcom_Workshop_12", endpoints(index))
            sessionConfig.AutoConnect = False

            Console.WriteLine("  Certificate store: " & sessionConfig.CertificateStorePath)

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

            ' Accept all certificates for development.
            ' In production, verify against a trusted certificate store.
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

    Private Sub CertificateValidationHandler(ByVal sender As CertificateValidator, ByVal e As CertificateValidationEventArgs)
        ' Called when the server presents its certificate - both during opc.https
        ' discovery (TLS) and when a security policy other than None is used.
        ' Inspect e.Certificate and e.Error, then set e.Accept accordingly.
        e.Accept = True
        Console.WriteLine($"  [Certificate] Accepted: {e.Certificate.Subject}")
    End Sub
End Class
