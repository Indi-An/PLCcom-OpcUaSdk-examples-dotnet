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

Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Server
Imports PLCcom.Opc.Ua.Server.Sdk
Imports System
Imports System.Collections.Generic

' ==============================================================================
' PLCcom OPC UA Server SDK - Workshop 12: User Authentication
'
' Workshop 11 allowed anonymous access - anyone could connect and write values.
' In production, you need to control who can connect and what they can do.
'
' This workshop demonstrates:
'   * How to require user authentication (no anonymous access)
'   * How to add users with different roles
'   * How roles affect write permissions on variables
'   * How to track session lifecycle (connect/disconnect)
'
' Test scenario:
'   1. Try connecting without credentials -> rejected
'   2. Connect as viewer/viewer123 -> can read, cannot write
'   3. Connect as operator/operator123 -> can read and write
'   4. Connect as admin/admin123 -> full access
'
' Connect with any OPC UA client to: opc.tcp://localhost:48410
' ==============================================================================

Module Program

    Sub Main(args As String())

        Dim LicenseUserName As String = "<Enter your UserName here>"
        Dim LicenseSerial As String = "<Enter your Serial here>"

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 12: User Authentication ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║    admin    / admin123    -> Engineer  (full access)         ║")
        Console.WriteLine("║    operator / operator123 -> Operator  (read + write)        ║")
        Console.WriteLine("║    viewer   / viewer123   -> Observer  (read-only)           ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Anonymous access is disabled - you MUST log in.             ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        ' =====================================================================
        ' Step 1: Configure the server - no anonymous access
        ' =====================================================================
        Dim config As New UaServerConfiguration With {
            .ApplicationName = "PLCcom Workshop 12 - User Authentication",
            .ApplicationUri = "urn:localhost:PLCcom:Workshop:12",
            .ProductUri = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
            .BaseAddresses = New List(Of String) From {
                "opc.tcp://localhost:48410",
                "opc.https://localhost:48411"
            },
            .SecurityPolicies = UaServer.GetRecommendedSecurityPolicies(),
            .UserTokenPolicies = New List(Of UserTokenPolicy) From {
                New UserTokenPolicy With {.TokenType = UserTokenType.UserName},
                New UserTokenPolicy With {.TokenType = UserTokenType.Certificate}
            },
            .ManufacturerName = "My Company GmbH",
            .ProductName = "My OPC UA Server",
            .SoftwareVersion = "1.0.0",
            .BuildNumber = "42",
            .NamespaceUri = "http://indi-an.com/opcua/workshop/user-authentication",
            .CertificateStorePath = ".\pki"
        }

        ' =====================================================================
        ' Step 2: Create server and add users with roles
        ' =====================================================================
        Using server As New UaServer(LicenseUserName, LicenseSerial)

            server.AddUser("admin", "admin123", Role.Engineer)
            server.AddUser("operator", "operator123", Role.Operator)
            server.AddUser("viewer", "viewer123", Role.Observer)

            Console.WriteLine("-- Users --------------------------------------------------------")
            Console.WriteLine("  admin    / admin123    -> Engineer  (full access)")
            Console.WriteLine("  operator / operator123 -> Operator  (read + write)")
            Console.WriteLine("  viewer   / viewer123   -> Observer  (read-only)")
            Console.WriteLine()

            AddHandler server.CertificateValidation, Sub(s, e)
                                                         Console.WriteLine($"  [CERT] {e.Certificate.Subject} -> Accepted")
                                                         e.Accept = True
                                                     End Sub

            AddHandler server.UserManager.CertificateValidation, Sub(s, e)
                                                                     Console.WriteLine($"  [USER CERT] {e.Certificate.Subject} -> Accepted")
                                                                     e.Accept = True
                                                                 End Sub

            AddHandler server.SessionCreated, Sub(s, e)
                                                  Console.WriteLine($"  [SESSION+] {If(e.SessionName, "unknown")} from {If(e.ClientUri, "unknown")}")
                                              End Sub
            AddHandler server.SessionClosed, Sub(s, e)
                                                 Console.WriteLine($"  [SESSION-] {If(e.SessionName, "unknown")}")
                                             End Sub

            AddHandler server.ValuesWritten, Sub(s, e)
                                                 For Each item In e.Items
                                                     Console.WriteLine($"  << OPC Write: {item.Path} ({item.NodeId}) = {item.Value}")
                                                 Next
                                             End Sub

            ' =================================================================
            ' Step 3: Start server and create test variables
            ' =================================================================
            Console.Write("Starting server ... ")
            Try
                server.Start(config)
            Catch ex As Exception
                Console.WriteLine("FAILED")
                Console.WriteLine(ex.Message)
                Console.ReadLine()
                Return
            End Try
            Console.WriteLine("OK")
            Console.WriteLine()

            Dim plant = server.CreateFolder("Plant")
            Dim temp = server.CreateVariable(Of Double)(plant, "Temperature", initialValue:=22.0)
            Dim rpm = server.CreateVariable(Of Integer)(plant, "RPM", initialValue:=1500)

            Console.WriteLine("-- Address space ------------------------------------------------")
            Console.WriteLine($"  Double  {temp.Path,-40} {temp.NodeId}  = 22.0")
            Console.WriteLine($"  Int32   {rpm.Path,-40} {rpm.NodeId}  = 1500")
            Console.WriteLine()

            ' =================================================================
            ' Step 4: Connect and test role-based access
            ' =================================================================
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running - authentication required.                ║")
            Console.WriteLine("║  Endpoint: opc.tcp://localhost:48410                         ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Try:                                                        ║")
            Console.WriteLine("║  * Connect without credentials -> rejected                   ║")
            Console.WriteLine("║  * Connect as viewer/viewer123 -> can read, cannot write     ║")
            Console.WriteLine("║  * Connect as operator/operator123 -> can read and write     ║")
            Console.WriteLine("║  * Connect as admin/admin123 -> full access                  ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Press ENTER to exit.                                        ║")
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
            Console.ReadLine()

        End Using

    End Sub

End Module
