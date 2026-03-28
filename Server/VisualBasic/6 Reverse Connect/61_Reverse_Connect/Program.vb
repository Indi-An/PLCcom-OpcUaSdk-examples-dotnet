Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Server.Sdk
Imports System
Imports System.Collections.Generic
Imports System.Threading

' ==============================================================================
' PLCcom OPC UA Server SDK - Workshop 61: Reverse Connect
'
' In standard OPC UA, the CLIENT connects to the SERVER.
' With Reverse Connect, the SERVER connects to the CLIENT.
'
' Why use Reverse Connect?
'   * The server is behind a firewall that blocks incoming connections
'   * The server is in a protected network (OT/ICS) and the client is in IT/cloud
'   * The server has a dynamic IP address
'
' How it works:
'   1. The client opens a listening port (e.g. 48500)
'   2. The server periodically sends a ReverseHello message to the client
'   3. The client uses that connection to establish a normal OPC UA session
'   4. From the application's perspective, the session works exactly the same
'
' This server also keeps its normal endpoint (48460) for direct connections.
'
' What you will learn:
'   * How to add a reverse connection target to the server
'   * How the server periodically attempts to connect to the client
'   * How to use both normal and reverse connect simultaneously
'
' Normal endpoint:  opc.tcp://localhost:48460
' Reverse Connect:  -> opc.tcp://localhost:48500 (server connects to client)
' ==============================================================================

Module Program

    Sub Main(args As String())

        ' -- License -----------------------------------------------------------
        ' TODO: Submit your license information from your license e-mail
        Dim LicenseUserName As String = "<Enter your UserName here>"
        Dim LicenseSerial As String = "<Enter your Serial here>"

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 61: Reverse Connect     ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This example demonstrates:                                  ║")
        Console.WriteLine("║  * Server initiates connection to client (firewall-safe)     ║")
        Console.WriteLine("║  * ReverseHello message flow                                 ║")
        Console.WriteLine("║  * Normal endpoint still available for direct connections    ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Use case: Server behind firewall, client in DMZ/cloud       ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Dim config As New UaServerConfiguration With {
            .ApplicationName = "PLCcom Workshop 61 - Reverse Connect",
            .ApplicationUri = "urn:localhost:PLCcom:Workshop:61",
            .ProductUri = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
            .BaseAddresses = New List(Of String) From {"opc.tcp://localhost:48460"},
            .SecurityPolicies = UaServer.GetRecommendedSecurityPolicies(),
            .UserTokenPolicies = New List(Of UserTokenPolicy) From {
                New UserTokenPolicy With {.TokenType = UserTokenType.Anonymous}
            },
            .CertificateStorePath = ".\pki"
        }

        Using server As New UaServer(LicenseUserName, LicenseSerial)
            AddHandler server.CertificateValidation, Sub(s, e) e.Accept = True

            ' Log session events to see when the reverse connection is established
            AddHandler server.SessionCreated, Sub(s, e)
                Console.WriteLine($"{vbLf}  [SESSION+] {e.SessionName} from {e.ClientUri}")
            End Sub
            AddHandler server.SessionClosed, Sub(s, e)
                Console.WriteLine($"{vbLf}  [SESSION-] {e.SessionName}")
            End Sub

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

            ' Create a variable to give the client something to read
            Dim plant = server.CreateFolder("Plant")
            Dim temp = server.CreateVariable(Of Double)(plant, "Temperature", initialValue:=22.5)
            temp.SetEURange(0, 100)
            temp.SetEngineeringUnits("C")

            ' -- Add Reverse Connection ----------------------------------------
            ' AddReverseConnection() tells the server to periodically connect to this URL.
            ' The server will send a ReverseHello message and wait for the client to
            ' establish a session over that connection.
            ' timeout: how long to wait for the client to respond (milliseconds)
            Dim clientUrl As String = "opc.tcp://localhost:48500"
            server.AddReverseConnection(clientUrl, timeout:=30000)

            Console.WriteLine($"  Normal endpoint:    opc.tcp://localhost:48460")
            Console.WriteLine($"  Reverse Connect to: {clientUrl}")
            Console.WriteLine()
            Console.WriteLine("  The server will attempt to connect to the client every ~15 sec.")
            Console.WriteLine("  Start a reverse-connect-capable client on port 48500 to test.")
            Console.WriteLine("  (See Workshop 71 Reverse Connect for a matching client)")
            Console.WriteLine()

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running with Reverse Connect enabled.             ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Normal endpoint (direct):                                   ║")
            Console.WriteLine("║    opc.tcp://localhost:48460                                 ║")
            Console.WriteLine("║    -> connect as usual, server is listening                  ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Reverse Connect endpoint:                                   ║")
            Console.WriteLine("║    opc.tcp://localhost:48500                                 ║")
            Console.WriteLine("║    -> the CLIENT must listen on this port                    ║")
            Console.WriteLine("║    -> the SERVER connects to the client (not the other way)  ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Press ENTER to start value loop, CTRL+C to exit.            ║")
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
            Console.ReadLine()

            Console.WriteLine("Pushing values every second...")
            Dim rng As New Random()
            Dim cycle As Long = 0

            While True
                cycle += 1
                temp.Value = Math.Round(20.0 + rng.NextDouble() * 10.0, 1)
                Console.Write($"{Chr(13)}  Cycle={cycle}  Temperature={temp.Value:F1}C  ")
                Thread.Sleep(1000)
            End While

        End Using

    End Sub

End Module
