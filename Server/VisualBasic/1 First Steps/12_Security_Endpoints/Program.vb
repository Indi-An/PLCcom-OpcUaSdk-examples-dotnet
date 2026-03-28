Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Server
Imports PLCcom.Opc.Ua.Server.Sdk
Imports System
Imports System.Collections.Generic

' ==============================================================================
' PLCcom OPC UA Server SDK - Workshop 12: Security & Endpoints
'
' OPC UA security works on two levels:
'   1. Transport security: encrypts and signs the communication channel
'      using X.509 certificates and configurable security policies.
'   2. User authentication: verifies who is connecting (Anonymous,
'      Username/Password, or X.509 user certificate).
'
' What you will learn:
'   * How to configure security policies (encryption algorithms)
'   * How to add users with different roles (Engineer, Operator, Observer)
'   * How to handle certificate validation events
'   * How to track session lifecycle (connect/disconnect)
'
' Connect with any OPC UA client to:
'   opc.tcp://localhost:48411
' ==============================================================================

Module Program

    Sub Main(args As String())

        ' -- License -----------------------------------------------------------
        ' TODO: Submit your license information from your license e-mail
        Dim LicenseUserName As String = "<Enter your UserName here>"
        Dim LicenseSerial As String = "<Enter your Serial here>"

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 12: Security            ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This example demonstrates:                                  ║")
        Console.WriteLine("║  * Security policies (None, RSA, optionally ECC)             ║")
        Console.WriteLine("║  * User authentication (Anonymous, Username, Certificate)    ║")
        Console.WriteLine("║  * User roles (Engineer, Operator, Observer)                 ║")
        Console.WriteLine("║  * Certificate validation events                             ║")
        Console.WriteLine("║  * Session lifecycle events                                  ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        ' -- Step 1: Configure security policies -------------------------------
        ' Security policies define which encryption algorithms the server offers.
        ' Each policy creates one endpoint in the server's endpoint list.
        ' Clients choose the endpoint that matches their security requirements.
        Dim config As New UaServerConfiguration With {
            .ApplicationName = "PLCcom Workshop 12 - Security",
            .ApplicationUri = "urn:localhost:PLCcom:Workshop:12",
            .ProductUri = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
            .BaseAddresses = New List(Of String) From {"opc.tcp://localhost:48411"},
            .SecurityPolicies = UaServer.GetRecommendedSecurityPolicies(),
            .UserTokenPolicies = New List(Of UserTokenPolicy) From {
                New UserTokenPolicy With {.TokenType = UserTokenType.Anonymous},
                New UserTokenPolicy With {.TokenType = UserTokenType.UserName},
                New UserTokenPolicy With {.TokenType = UserTokenType.Certificate}
            },
            .CertificateStorePath = ".\pki",
            .CertificateLifetimeInMonths = 60
        }

        Console.WriteLine($"  Security Policies ({config.SecurityPolicies.Count} endpoints):")
        For Each sp In config.SecurityPolicies
            Console.WriteLine($"    * {sp.SecurityMode,-18} {UaServer.GetSecurityPolicyName(sp.SecurityPolicyUri)}")
        Next
        Console.WriteLine()

        ' -- Step 2: Create server and add users -------------------------------
        Using server As New UaServer(LicenseUserName, LicenseSerial)

            ' Add users with roles - roles control what the user can do:
            '   Engineer  -> full access (read, write, browse, call methods)
            '   Operator  -> read + write, no configuration changes
            '   Observer  -> read-only access
            server.AddUser("admin", "admin123", Role.Engineer)
            server.AddUser("operator", "operator123", Role.Operator)
            server.AddUser("viewer", "viewer123", Role.Observer)

            Console.WriteLine("  Users:")
            Console.WriteLine("    admin    / admin123    -> Engineer (full access)")
            Console.WriteLine("    operator / operator123 -> Operator (read + write)")
            Console.WriteLine("    viewer   / viewer123   -> Observer (read-only)")
            Console.WriteLine()

            ' -- Step 3: Handle certificate validation -------------------------
            ' This event fires when a client presents its X.509 certificate.
            ' In production: check the certificate against your trust store.
            AddHandler server.CertificateValidation, Sub(sender, e)
                Console.WriteLine($"  [CERT] Transport: {e.Certificate.Subject} -> Accepted")
                e.Accept = True
            End Sub

            AddHandler server.UserManager.CertificateValidation, Sub(sender, e)
                Console.WriteLine($"  [CERT] User: {e.Certificate.Subject} -> Accepted")
                e.Accept = True
            End Sub

            ' -- Step 4: Track session lifecycle -------------------------------
            ' These events fire whenever a client connects or disconnects.
            AddHandler server.SessionCreated, Sub(s, e)
                Console.WriteLine($"  [SESSION+] {If(e.SessionName, "unknown")} from {If(e.ClientUri, "unknown")}")
            End Sub
            AddHandler server.SessionClosed, Sub(s, e)
                Console.WriteLine($"  [SESSION-] {If(e.SessionName, "unknown")}")
            End Sub

            ' -- Step 5: Start server ------------------------------------------
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
            server.CreateVariable(Of Double)(plant, "Temperature", initialValue:=22.0)

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running with security enabled.                    ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Endpoint: opc.tcp://localhost:48411                         ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Try with any OPC UA client:                                 ║")
            Console.WriteLine("║  * Connect with Security Mode = None (anonymous)             ║")
            Console.WriteLine("║  * Connect with Sign + Basic256Sha256 as admin/admin123      ║")
            Console.WriteLine("║  * Connect as viewer/viewer123 and try to write Temperature  ║")
            Console.WriteLine("║  * Watch session events appear in this console               ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Press ENTER to exit.                                        ║")
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
            Console.ReadLine()

        End Using

    End Sub

End Module
