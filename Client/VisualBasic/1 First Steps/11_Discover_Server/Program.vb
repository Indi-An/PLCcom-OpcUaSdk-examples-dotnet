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
' PLCcom OPC UA Client SDK - Workshop 11: Discover Server
'
' Before connecting to an OPC UA server, you need to know which endpoints
' it offers. The discovery process queries the server for all available
' endpoints including their security policies and transport protocols.
'
' This is always the first step when working with a new OPC UA server.
' No session is created - discovery is an anonymous, lightweight call.
'
' What you will learn:
'   * How to discover all registered servers at a URL (FindServers)
'   * How to query the available endpoints (GetEndpoints)
'   * How to read endpoint details (URL, security mode, security policy)
'
' Target server: opc.tcp://localhost:48410
' (Start any of the Server SDK workshops first, e.g. Workshop 11)
' ==============================================================================

Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Client.Sdk

Public Class Program
    Public Shared Sub Main(ByVal args As String())

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 11: Discover Server     ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Before connecting to an OPC UA server, you need to know     ║")
        Console.WriteLine("║  which endpoints it offers. This workshop queries the        ║")
        Console.WriteLine("║  server for all registered applications and their endpoints. ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  What you will learn:                                        ║")
        Console.WriteLine("║    * How to discover servers at a URL (FindServers)          ║")
        Console.WriteLine("║    * How to query endpoints (GetEndpoints)                   ║")
        Console.WriteLine("║    * How to read endpoint security details                   ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Required server: Server Workshop 11 (Simple Server)         ║")
        Console.WriteLine("║  opc.tcp://localhost:48410                                   ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Try
            ' -- Step 1: Define the discovery URL ---------------------------------
            ' This is the base URL of the OPC UA server you want to discover.
            ' Start any of the Server SDK workshops first (e.g. Server Workshop 11).
            Dim url As String = "opc.tcp://localhost:48410"

            Console.WriteLine("  Discovery URL: " & url)
            Console.WriteLine("  Querying servers...")
            Console.WriteLine()

            ' -- Step 2: Find all registered servers ------------------------------
            ' FindServers() sends a FindServers request to the discovery endpoint.
            ' The server returns all applications it knows about, including their
            ' application name, URI and discovery URLs.
            ' The timeout (60000 ms) limits how long we wait for a response.
            Dim servers As ApplicationDescriptionCollection = UaClient.FindServers(New Uri(url), 60000, certificateValidator:=AddressOf CertificateValidationHandler)

            Console.WriteLine($"  Found {servers.Count} server(s).")
            Console.WriteLine()

            For Each server As ApplicationDescription In servers

                ' Skip discovery servers - we only want application servers
                If server.ApplicationType = ApplicationType.DiscoveryServer Then
                    Continue For
                End If

                Console.WriteLine($"  Server: {server.ApplicationName}")
                Console.WriteLine($"  URI:    {server.ApplicationUri}")
                Console.WriteLine()

                ' -- Step 3: Get endpoints for each server ------------------------
                ' GetEndpoints() returns all endpoints the server supports.
                ' Each endpoint describes a URL, security mode, security policy
                ' and the user token policies it accepts.
                For Each discoveryUrl As String In server.DiscoveryUrls
                    Console.WriteLine()
                    Console.WriteLine($"  Querying endpoints for: {discoveryUrl}")
                    Dim endpoints As EndpointDescriptionCollection = UaClient.GetEndpoints(
                        New Uri(discoveryUrl),
                        certificateValidator:=AddressOf CertificateValidationHandler)

                    If endpoints.Count > 0 Then
                        Console.WriteLine($"  {endpoints.Count} endpoint(s) found:")
                        Console.WriteLine()

                        Dim counter As Integer = 0
                        For Each endpoint As EndpointDescription In endpoints
                            Console.WriteLine($"  [{counter}] {endpoint.ToDisplayString()}")
                            counter += 1
                        Next
                    Else
                        Console.WriteLine("  No endpoints found.")
                    End If
                Next
            Next

            Console.WriteLine()
            Console.WriteLine("  Discovery complete.")

        Catch ex As Exception
            Console.WriteLine("  Error: " & ex.Message)
        End Try

        Console.WriteLine()
        Console.WriteLine("  Press ENTER to exit.")
        Console.ReadLine()

    End Sub

    Shared Sub CertificateValidationHandler(ByVal sender As CertificateValidator, ByVal e As CertificateValidationEventArgs)
        ' Called when the server presents its certificate - both during opc.https
        ' discovery (TLS) and when a security policy other than None is used.
        ' Inspect e.Certificate and e.Error, then set e.Accept accordingly.
        Console.WriteLine($"  Validating certificate: {e.Certificate.Subject}")
        Console.WriteLine($"  Validation result:      {If(ServiceResult.IsGood(e.Error), "OK", e.Error.ToString())}")
        e.Accept = True ' accept anyway - replace with your logic
        Console.WriteLine($"  Decision:               accepted")
    End Sub

End Class
