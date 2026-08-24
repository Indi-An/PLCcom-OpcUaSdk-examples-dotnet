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
' PLCcom OPC UA Client SDK - Workshop 19: Enable Debug Tracing
'
' When troubleshooting OPC UA communication issues, the built-in trace
' system is invaluable. It logs all OPC UA stack activity to a file:
' service calls, security handshakes, errors and more.
'
' What you will learn:
'   * How to create and configure a TraceConfiguration
'   * How to set the trace output file path
'   * How to control trace verbosity with TraceMasks
'   * How to bind the trace configuration to a session
'
' Target server: opc.tcp://localhost:48410
' ==============================================================================

Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Client
Imports PLCcom.Opc.Ua.Client.Sdk
Imports System
Imports System.Diagnostics

Public Class Program

    Public Shared Sub Main(ByVal args As String())
        Dim p As New Program()
        p.Start()
    End Sub

    Private Sub Start()

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 19: Debug Tracing       ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  The built-in trace system logs all OPC UA stack activity    ║")
        Console.WriteLine("║  to a file: service calls, security handshakes, errors.      ║")
        Console.WriteLine("║  Essential for troubleshooting communication issues.         ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  What you will learn:                                        ║")
        Console.WriteLine("║    * Create and configure a TraceConfiguration               ║")
        Console.WriteLine("║    * Set the trace output file path                          ║")
        Console.WriteLine("║    * Control trace verbosity with TraceMasks                 ║")
        Console.WriteLine("║    * Bind the trace configuration to a session               ║")
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

            ' -- Step 2: Configure tracing ----------------------------------------
            Dim sessionConfig As SessionConfiguration = CreateConfig(endpoints(index))

            PrintConfig(sessionConfig)

            Dim logFile As String = AppDomain.CurrentDomain.BaseDirectory &
                "Logs\" & Process.GetCurrentProcess().ProcessName & ".trace.log"

            Dim traceConfig As New TraceConfiguration()
            traceConfig.OutputFilePath = logFile
            traceConfig.DeleteOnLoad = True
            traceConfig.TraceMasks = Utils.TraceMasks.All
            traceConfig.ApplyTraceSettings()

            ' Bind the trace configuration to the session
            sessionConfig.TraceConfiguration = traceConfig

            Console.WriteLine()
            Console.WriteLine("  Trace file: " & logFile)
            Console.WriteLine("  TraceMasks:  All (maximum verbosity)")
            Console.WriteLine()

            ' -- Step 3: Connect and browse ---------------------------------------
            Using client As New UaClient(LicenseUserName, LicenseSerial, sessionConfig)
                Console.WriteLine("  License: " & client.GetLicenceMessage())

                AddHandler client.CertificateValidation, AddressOf CertificateValidationHandler
                AddHandler client.ServerConnected, Sub(s, e)
                                                       Console.WriteLine($"  [Connected] {DateTime.Now:HH:mm:ss}")
                                                   End Sub
                AddHandler client.ServerConnectionLost, Sub(s, e)
                                                            Console.WriteLine($"  [ConnectionLost] {DateTime.Now:HH:mm:ss}")
                                                        End Sub
                AddHandler client.KeepAlive, Sub(session, e)
                                             End Sub

                Console.WriteLine()

                ' Browse to generate some trace output
                ' TODO: Adjust this path to match your server's address space
                Dim browsePath As String = "Objects.Plant.Line1.Machine1"
                Console.WriteLine($"  Browsing: {browsePath}")

                Try
                    Dim sourceNode As NodeId = client.GetNodeIdByPath(browsePath)

                    Dim nodeToBrowse As New BrowseDescription()
                    nodeToBrowse.NodeId = sourceNode
                    nodeToBrowse.BrowseDirection = BrowseDirection.Forward
                    nodeToBrowse.ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences
                    nodeToBrowse.IncludeSubtypes = True
                    nodeToBrowse.NodeClassMask = CUInt(NodeClass.Object Or NodeClass.Variable)
                    nodeToBrowse.ResultMask = CUInt(BrowseResultMask.All)

                    Dim nodesToBrowse As New BrowseDescriptionCollection()
                    nodesToBrowse.Add(nodeToBrowse)

                    Dim results As ReferenceDescriptionCollection = client.BrowseFull(nodesToBrowse)

                    Console.WriteLine($"  {results.Count} child node(s) found.")
                    Console.WriteLine()

                    For Each rd As ReferenceDescription In results
                        Console.WriteLine($"  {rd.DisplayName.ToString(),-30} NodeId={rd.NodeId}  Class={rd.NodeClass}")
                    Next

                Catch ex As Exception
                    Console.WriteLine("  Browse error: " & ex.Message)
                End Try

                Console.WriteLine()
                Console.WriteLine("  Check the trace file for detailed OPC UA stack logs:")
                Console.WriteLine("  " & logFile)
            End Using

        Catch ex As Exception
            Console.WriteLine("  Error: " & ex.Message)
        End Try

        Console.WriteLine()
        Console.WriteLine("  Press ENTER to exit.")
        Console.ReadLine()

    End Sub

    Private Sub CertificateValidationHandler(ByVal sender As CertificateValidator, ByVal e As CertificateValidationEventArgs)
        ' Called when the server presents its certificate - both during opc.https
        ' discovery (TLS) and when a security policy other than None is used.
        ' Inspect e.Certificate and e.Error, then set e.Accept accordingly.
        e.Accept = True
        Console.WriteLine($"  [Certificate] Accepted: {e.Certificate.Subject}")
    End Sub

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
