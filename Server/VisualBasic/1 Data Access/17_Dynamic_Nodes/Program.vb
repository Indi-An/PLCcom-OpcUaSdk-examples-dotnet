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
Imports PLCcom.Opc.Ua.Server.Sdk
Imports System
Imports System.Collections.Generic
Imports System.Threading

' ==============================================================================
' PLCcom OPC UA Server SDK - Workshop 17: Dynamic Nodes
'
' This workshop demonstrates:
'   Part A - Initial address space (created right after Start)
'   Part B - Path-based node lookup (GetNodeId, GetVariable)
'   Part C - Dynamic node creation (add nodes at runtime)
'   Part D - Dynamic node removal (RemoveNode)
'   Part E - Circular reference detection
'   Part F - Timer-based dynamic creation (simulates device discovery)
'
' Connect with any OPC UA client to: opc.tcp://localhost:48410
' ==============================================================================

Module Program

    Sub Main(args As String())

        Dim LicenseUserName As String = "<Enter your UserName here>"
        Dim LicenseSerial As String = "<Enter your Serial here>"

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 17: Dynamic Nodes       ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This example demonstrates:                                  ║")
        Console.WriteLine("║    * Adding nodes at runtime                                 ║")
        Console.WriteLine("║    * Removing nodes dynamically                              ║")
        Console.WriteLine("║    * Path-based node lookup (dot-separated)                  ║")
        Console.WriteLine("║    * Circular reference detection                            ║")
        Console.WriteLine("║    * Timer-based device discovery simulation                 ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        ' All server settings are defined in CreateConfig() below.
        Dim config = CreateConfig()
        PrintConfig(config)

        Using server As New UaServer(LicenseUserName, LicenseSerial)
            AddHandler server.CertificateValidation, Sub(s, e) e.Accept = True

            AddHandler server.ValuesWritten, Sub(s, e)
                                                 For Each item In e.Items
                                                     Console.WriteLine($"  << OPC Write: {item.Path} ({item.NodeId}) = {item.Value}")
                                                 Next
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

            ' =================================================================
            ' Part A: Initial address space
            ' =================================================================
            Console.WriteLine("-- Part A: Initial address space ---------------------------------")

            Dim plant = server.CreateFolder("Plant", UaRolePermissions.WITHOUT_RESTRICTIONS)
            Dim line1 = server.CreateFolder(plant, "Line1", UaRolePermissions.WITHOUT_RESTRICTIONS)
            Dim temp = server.CreateVariable(Of Double)(line1, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=22.0)

            Console.WriteLine($"  {plant.Path,-45} {plant.NodeId}")
            Console.WriteLine($"  {line1.Path,-45} {line1.NodeId}")
            Console.WriteLine($"  {temp.Path,-45} {temp.NodeId}  = {temp.Value}")
            Console.WriteLine()

            ' =================================================================
            ' Part B: Path-based node lookup
            ' =================================================================
            Console.WriteLine("-- Part B: Path-based node lookup --------------------------------")

            Dim nodeId = server.GetNodeId("Objects.Plant.Line1.Temperature")
            Console.WriteLine($"  GetNodeId(""Objects.Plant.Line1.Temperature"") = {nodeId}")

            Dim variable = server.GetVariable(Of Double)("Objects.Plant.Line1.Temperature")
            Console.WriteLine($"  GetVariable -> Value = {variable?.Value}")

            Dim val As Double = server.GetValue(Of Double)("Objects.Plant.Line1.Temperature")
            Console.WriteLine($"  GetValue(""Objects.Plant.Line1.Temperature"") = {val}")

            server.SetValue("Objects.Plant.Line1.Temperature", 25.5)
            Console.WriteLine($"  SetValue(""Objects.Plant.Line1.Temperature"", 25.5)")
            Console.WriteLine($"  GetValue after SetValue = {server.GetValue(Of Double)("Objects.Plant.Line1.Temperature")}")
            Console.WriteLine()

            ' =================================================================
            ' Part C: Dynamic node creation
            ' =================================================================
            Console.WriteLine("-- Part C: Dynamic node creation ---------------------------------")

            Dim dynFolder = server.CreateFolder(plant, "DynamicNodes", UaRolePermissions.WITHOUT_RESTRICTIONS)
            Dim dynVar1 = server.CreateVariable(Of Integer)(dynFolder, "Counter", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=42)
            Dim dynVar2 = server.CreateVariable(Of String)(dynFolder, "Message", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:="Hello")

            Console.WriteLine($"  Created: {dynVar1.Path,-40} = {dynVar1.Value}")
            Console.WriteLine($"  Created: {dynVar2.Path,-40} = {dynVar2.Value}")
            Console.WriteLine()

            ' =================================================================
            ' Part D: Dynamic node removal
            ' =================================================================
            Console.WriteLine("-- Part D: Dynamic node removal ----------------------------------")

            Console.WriteLine($"  Removing {dynVar1.Path} ...")
            Dim removed As Boolean = server.RemoveNode(dynVar1.NodeId)
            Console.WriteLine($"  Result: {If(removed, "OK - node removed", "FAILED")}")

            Dim chk = server.GetNodeId("Objects.Plant.DynamicNodes.Counter")
            Console.WriteLine($"  GetNodeId after removal: {If(chk Is Nothing, "null (correct)", chk.ToString())}")
            Console.WriteLine()

            Console.WriteLine($"  Removing entire DynamicNodes folder ...")
            removed = server.RemoveNode(dynFolder.NodeId)
            Console.WriteLine($"  Result: {If(removed, "OK - folder and children removed", "FAILED")}")

            chk = server.GetNodeId("Objects.Plant.DynamicNodes.Message")
            Console.WriteLine($"  GetNodeId(""...DynamicNodes.Message""): {If(chk Is Nothing, "null (correct)", chk.ToString())}")
            Console.WriteLine()

            ' =================================================================
            ' Part E: Circular reference detection
            ' =================================================================
            Console.WriteLine("-- Part E: Circular reference detection --------------------------")

            Console.Write("  Creating ""Plant"" under Line1 (ancestor name): ")
            Try
                server.CreateFolder(line1, "Plant", UaRolePermissions.WITHOUT_RESTRICTIONS)
                Console.WriteLine("NOT DETECTED (unexpected)")
            Catch ex As ArgumentException
                Console.WriteLine($"BLOCKED - {ex.Message}")
            End Try
            Console.WriteLine()

            ' =================================================================
            ' Part F: Timer-based device discovery simulation
            ' =================================================================
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║")
            Console.WriteLine("║  opc.tcp://localhost:48410                                   ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Press ENTER to start the device discovery simulation.       ║")
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
            Console.ReadLine()

            Console.WriteLine("Simulating device discovery... (CTRL+C to exit)")
            Console.WriteLine()

            Dim rng As New Random()
            Dim deviceNumber As Integer = 0
            Dim activeDevices As New Queue(Of (Name As String, FolderId As NodeId))()
            Const MaxDevices As Integer = 5

            While True
                deviceNumber += 1
                Dim deviceName As String = $"Device_{deviceNumber}"

                Dim deviceFolder = server.CreateFolder(plant, deviceName, UaRolePermissions.WITHOUT_RESTRICTIONS)
                Dim devTemp = server.CreateVariable(Of Double)(deviceFolder, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=Math.Round(20.0 + rng.NextDouble() * 15.0, 1))
                Dim devStatus = server.CreateVariable(Of String)(deviceFolder, "Status", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:="Online")

                activeDevices.Enqueue((deviceName, deviceFolder.NodeId))
                Console.WriteLine($"  + Discovered {deviceName}: Temp={devTemp.Value:F1}, Status={devStatus.Value}")

                If activeDevices.Count > MaxDevices Then
                    Dim oldest = activeDevices.Dequeue()
                    server.RemoveNode(oldest.FolderId)
                    Console.WriteLine($"  - Removed {oldest.Name} (sliding window)")
                End If

                Console.WriteLine($"    Active devices: {activeDevices.Count}")
                Thread.Sleep(5000)
            End While

        End Using

    End Sub


    ' ==========================================================================
    ' Helper: CreateConfig
    ' ==========================================================================
    ' Returns the server configuration. Adjust to your needs.
    Private Function CreateConfig() As UaServerConfiguration
        Dim cfg As New UaServerConfiguration
        ' ── Application Identity ──────────────────────────────────────────────
        cfg.ApplicationName = "PLCcom Workshop 17 - Dynamic Nodes"
        cfg.ApplicationUri  = "urn:localhost:PLCcom:Workshop:17"
        cfg.ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/"
        cfg.NamespaceUri    = "http://indi-an.com/opcua/workshop/dynamic-nodes"

        ' ── ServerStatus/BuildInfo ────────────────────────────────────────────
        cfg.ManufacturerName = "My Company GmbH"
        cfg.ProductName      = "My OPC UA Server"
        cfg.SoftwareVersion  = "1.0.0"
        cfg.BuildNumber      = "42"

        ' ── Endpoints ────────────────────────────────────────────────────────
        cfg.BaseAddresses = New List(Of String) From {"opc.tcp://localhost:48410", "opc.https://localhost:48411"}

        ' ── Security Policies ────────────────────────────────────────────────
        cfg.SecurityPolicies = UaServer.GetRecommendedSecurityPolicies()

        ' ── User Authentication ───────────────────────────────────────────────
        cfg.UserTokenPolicies = New List(Of UserTokenPolicy) From {New UserTokenPolicy With {.TokenType = UserTokenType.Anonymous}}

        ' ── PKI Certificate Store ─────────────────────────────────────────────
        cfg.CertificateStorePath = ".\pki"
        cfg.CertificateLifetimeInMonths = 60
        cfg.AutoAcceptUntrustedCertificates = False

        ' ── Endpoint Host Normalization ───────────────────────────────────────
        ' AsConfigured (default) = endpoints use exactly the host from BaseAddresses
        ' NormalizeToHostname    = replace localhost/127.0.0.1 with the machine name
        ' None                   = no normalization, behavior depends on DNS and network settings
        cfg.EndpointHostMode = EndpointHostMode.AsConfigured
        cfg.MaxSessionCount = 100
        cfg.ShutdownDelay = 5

        ' ── VendorServerInfo ──────────────────────────────────────────────────
        cfg.VendorName = "My Company GmbH"
        cfg.VendorProductName = "My OPC UA Server"
        cfg.VendorProductVersion = "1.0.0"

        ' ── OperationLimits ───────────────────────────────────────────────────
        cfg.MaxNodesPerRead = 1000
        cfg.MaxNodesPerWrite = 1000
        cfg.MaxNodesPerBrowse = 1000
        cfg.MaxNodesPerHistoryReadData           = 100
        cfg.MaxNodesPerHistoryReadEvents         = 100
        cfg.MaxNodesPerHistoryUpdateData         = 100
        cfg.MaxNodesPerHistoryUpdateEvents       = 100
        cfg.MaxNodesPerMethodCall                = 200
        cfg.MaxNodesPerRegisterNodes             = 1000
        cfg.MaxNodesPerTranslateBrowsePathsToNodeIds = 1000
        cfg.MaxNodesPerNodeManagement            = 1000
        cfg.MaxMonitoredItemsPerCall             = 1000
        Return cfg
    End Function

    ' ==========================================================================
    ' Helper: PrintConfig
    ' ==========================================================================
    Private Sub PrintConfig(config As UaServerConfiguration)
        Console.WriteLine("-- Active Server Configuration ------------------------------")
        Console.WriteLine("  ApplicationName  : " & config.ApplicationName)
        Console.WriteLine("  ApplicationUri   : " & config.ApplicationUri)
        Console.WriteLine("  NamespaceUri     : " & If(config.NamespaceUri, "(default)"))
        Console.WriteLine("  ManufacturerName : " & If(config.ManufacturerName, "(not set)"))
        Console.WriteLine("  ProductName      : " & If(config.ProductName, "(not set)"))
        Console.WriteLine("  SoftwareVersion  : " & If(config.SoftwareVersion, "(auto-detect)"))
        Console.WriteLine("  BuildNumber      : " & If(config.BuildNumber, "(auto-detect)"))
        Console.WriteLine()
        Console.WriteLine("  Endpoints:")
        For Each addr In config.BaseAddresses : Console.WriteLine("    " & addr) : Next
        Console.WriteLine()
                Console.WriteLine("  EndpointHostMode : " & config.EndpointHostMode.ToString())
        Console.WriteLine("  VendorServerInfo:")
        Console.WriteLine("    VendorName=" & If(config.VendorName, "(not set)") & "  ProductName=" & If(config.VendorProductName, "(not set)") & "  Version=" & If(config.VendorProductVersion, "(not set)"))
        Console.WriteLine()
        Console.WriteLine("  OperationLimits:")
        Console.WriteLine("    Read=" & config.MaxNodesPerRead & "  Write=" & config.MaxNodesPerWrite & "  Browse=" & config.MaxNodesPerBrowse & "  Method=" & config.MaxNodesPerMethodCall)
        Console.WriteLine("    HistRD=" & config.MaxNodesPerHistoryReadData & "  HistRE=" & config.MaxNodesPerHistoryReadEvents & "  HistUD=" & config.MaxNodesPerHistoryUpdateData & "  HistUE=" & config.MaxNodesPerHistoryUpdateEvents)
        Console.WriteLine("    Register=" & config.MaxNodesPerRegisterNodes & "  Translate=" & config.MaxNodesPerTranslateBrowsePathsToNodeIds & "  NodeMgmt=" & config.MaxNodesPerNodeManagement & "  MonItems=" & config.MaxMonitoredItemsPerCall)
        Console.WriteLine("-------------------------------------------------------------")
        Console.WriteLine()
    End Sub

End Module
