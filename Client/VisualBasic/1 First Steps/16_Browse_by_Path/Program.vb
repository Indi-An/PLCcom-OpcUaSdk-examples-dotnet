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
' PLCcom OPC UA Client SDK - Workshop 16: Browse by Path
'
' Workshop 15 browsed from a numeric NodeId (i=85). In practice, you often
' know the logical path to a node (e.g. "Objects.Plant.Line1.Machine1")
' but not its numeric NodeId. GetNodeIdByPath() resolves a dot-separated
' browse path to a NodeId, then you can browse from there.
'
' What you will learn:
'   * How to resolve a dot-separated path to a NodeId (GetNodeIdByPath)
'   * How to browse from a path-resolved NodeId
'   * The difference between browsing by NodeId vs. by path
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
        Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 16: Browse by Path      ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Instead of using numeric NodeIds, you can resolve a         ║")
        Console.WriteLine("║  dot-separated browse path to a NodeId and then browse       ║")
        Console.WriteLine("║  from there. This is more readable and maintainable.         ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  What you will learn:                                        ║")
        Console.WriteLine("║    * Resolve a path to a NodeId (GetNodeIdByPath)            ║")
        Console.WriteLine("║    * Browse from a path-resolved NodeId                      ║")
        Console.WriteLine("║    * Difference between NodeId vs. path browsing             ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Required server: Server Workshop 11 (Simple Server)         ║")
        Console.WriteLine("║  opc.tcp://localhost:48410                                   ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Try
            ' -- License ----------------------------------------------------------
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

            ' -- Step 2: Connect --------------------------------------------------
            Dim sessionConfig As SessionConfiguration = CreateConfig(endpoints(index))

            PrintConfig(sessionConfig)

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

                ' -- Step 3: Resolve path to NodeId -------------------------------
                ' TODO: Adjust this path to match your server's address space
                Dim browsePath As String = "Objects.Plant.Line1.Machine1"

                Console.WriteLine($"  Resolving path: {browsePath}")
                Dim sourceNode As NodeId = client.GetNodeIdByPath(browsePath)
                Console.WriteLine($"  Resolved NodeId: {sourceNode}")
                Console.WriteLine()

                ' -- Step 4: Browse from the resolved NodeId ----------------------
                Dim nodeToBrowse1 As New BrowseDescription()
                nodeToBrowse1.NodeId = sourceNode
                nodeToBrowse1.BrowseDirection = BrowseDirection.Forward
                nodeToBrowse1.ReferenceTypeId = ReferenceTypeIds.Aggregates
                nodeToBrowse1.IncludeSubtypes = True
                nodeToBrowse1.NodeClassMask = CUInt(NodeClass.Object Or NodeClass.Variable)
                nodeToBrowse1.ResultMask = CUInt(BrowseResultMask.All)

                Dim nodeToBrowse2 As New BrowseDescription()
                nodeToBrowse2.NodeId = sourceNode
                nodeToBrowse2.BrowseDirection = BrowseDirection.Forward
                nodeToBrowse2.ReferenceTypeId = ReferenceTypeIds.Organizes
                nodeToBrowse2.IncludeSubtypes = True
                nodeToBrowse2.NodeClassMask = CUInt(NodeClass.Object Or NodeClass.Variable)
                nodeToBrowse2.ResultMask = CUInt(BrowseResultMask.All)

                Dim nodesToBrowse As New BrowseDescriptionCollection()
                nodesToBrowse.Add(nodeToBrowse1)
                nodesToBrowse.Add(nodeToBrowse2)

                Console.WriteLine($"  Browsing children of {browsePath}...")
                Console.WriteLine()

                Dim results As ReferenceDescriptionCollection = client.BrowseFull(nodesToBrowse)

                If results.Count > 0 Then
                    Console.WriteLine($"  {results.Count} child node(s) found:")
                    Console.WriteLine()

                    For Each rd As ReferenceDescription In results
                        Console.WriteLine($"  {rd.DisplayName.ToString(),-30} NodeId={rd.NodeId}  Class={rd.NodeClass}  BrowseName={rd.BrowseName}")
                    Next
                Else
                    Console.WriteLine("  No child nodes found.")
                End If
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
