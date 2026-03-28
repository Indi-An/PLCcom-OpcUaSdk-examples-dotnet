Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Server.Sdk
Imports System
Imports System.Collections.Generic

' ==============================================================================
' PLCcom OPC UA Server SDK - Workshop 19: Dynamic Nodes
'
' In most OPC UA servers the address space is static - built once at startup.
' However, the SDK also supports dynamic changes at runtime:
'   * Add new folders and variables while the server is running
'   * Remove nodes that are no longer needed
'   * Connected clients see the changes immediately
'
' What you will learn:
'   * How to add nodes after server.Start()
'   * How to remove nodes by NodeId
'   * How to find nodes by browse path (e.g. "Plant.Line1.Temperature")
'   * How the SDK prevents circular references
'
' Connect with any OPC UA client to: opc.tcp://localhost:48418
' ==============================================================================

Module Program

    Sub Main(args As String())

        ' -- License -----------------------------------------------------------
        ' TODO: Submit your license information from your license e-mail
        Dim LicenseUserName As String = "<Enter your UserName here>"
        Dim LicenseSerial As String = "<Enter your Serial here>"

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 19: Dynamic Nodes       ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This example demonstrates:                                  ║")
        Console.WriteLine("║  * Adding nodes at runtime                                   ║")
        Console.WriteLine("║  * Removing nodes dynamically                                ║")
        Console.WriteLine("║  * Path-based node lookup (dot-separated)                    ║")
        Console.WriteLine("║  * Circular reference detection                              ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Dim config As New UaServerConfiguration With {
            .ApplicationName = "PLCcom Workshop 19 - Dynamic Nodes",
            .ApplicationUri = "urn:localhost:PLCcom:Workshop:19",
            .ProductUri = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
            .BaseAddresses = New List(Of String) From {"opc.tcp://localhost:48418"},
            .SecurityPolicies = UaServer.GetRecommendedSecurityPolicies(),
            .UserTokenPolicies = New List(Of UserTokenPolicy) From {
                New UserTokenPolicy With {.TokenType = UserTokenType.Anonymous}
            },
            .CertificateStorePath = ".\pki"
        }

        Using server As New UaServer(LicenseUserName, LicenseSerial)
            AddHandler server.CertificateValidation, Sub(s, e) e.Accept = True

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

            ' -- Create initial structure --------------------------------------
            Dim plant = server.CreateFolder("Plant")
            Dim line1 = server.CreateFolder(plant, "Line1")
            Dim temp = server.CreateVariable(Of Double)(line1, "Temperature", initialValue:=22.0)

            Console.WriteLine("  Initial: Plant -> Line1 -> Temperature")
            Console.WriteLine()

            ' -- Path-based lookup --------------------------------------------
            ' GetNodeId() resolves a dot-separated browse path to a NodeId.
            Dim nodeId = server.GetNodeId("Plant.Line1.Temperature")
            Console.WriteLine($"  Path lookup: 'Plant.Line1.Temperature' -> {nodeId}")

            Dim variable = server.GetVariable(Of Double)("Plant.Line1.Temperature")
            Console.WriteLine($"  Variable lookup: Value = {variable?.Value}")
            Console.WriteLine()

            ' -- Dynamic node creation ----------------------------------------
            ' Nodes can be added at any time after Start().
            ' Connected clients will see the new nodes immediately on their next browse.
            Console.WriteLine("  Adding dynamic nodes...")
            Dim dynFolder = server.CreateFolder(plant, "DynamicNodes")
            Dim dynVar1 = server.CreateVariable(Of Integer)(dynFolder, "Counter", initialValue:=42)
            Dim dynVar2 = server.CreateVariable(Of String)(dynFolder, "Message", initialValue:="Hello")
            Console.WriteLine($"    Created: DynamicNodes/Counter = {dynVar1.Value}")
            Console.WriteLine($"    Created: DynamicNodes/Message = {dynVar2.Value}")
            Console.WriteLine()

            ' -- Dynamic node removal -----------------------------------------
            ' RemoveNode() removes the node and all its children from the address space.
            Console.WriteLine("  Removing DynamicNodes/Counter...")
            Dim removed As Boolean = server.RemoveNode(dynVar1.NodeId)
            Console.WriteLine($"    Removed: {If(removed, "OK", "FAILED")}")
            Console.WriteLine()

            ' -- Circular reference detection ---------------------------------
            ' The SDK prevents you from creating a folder with the same name as
            ' one of its ancestors, which would create a circular reference.
            Console.Write("  Circular reference check: ")
            Try
                server.CreateFolder(line1, "Plant")
                Console.WriteLine("NOT DETECTED (unexpected)")
            Catch ex As ArgumentException
                Console.WriteLine($"OK - {ex.Message}")
            End Try
            Console.WriteLine()

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║")
            Console.WriteLine("║  opc.tcp://localhost:48418                                   ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Try:                                                        ║")
            Console.WriteLine("║  * Browse Plant -> DynamicNodes                              ║")
            Console.WriteLine("║  * Counter was removed - only Message exists                 ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Press ENTER to exit.                                        ║")
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
            Console.ReadLine()

        End Using

    End Sub

End Module
