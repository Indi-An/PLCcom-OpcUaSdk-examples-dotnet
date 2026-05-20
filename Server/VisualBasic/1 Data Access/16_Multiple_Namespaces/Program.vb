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
' PLCcom OPC UA Server SDK - Workshop 16: Multiple Namespaces
'
' This workshop demonstrates:
'   * Registering additional namespaces with AddNamespace
'   * Creating nodes in specific namespaces using the ns parameter
'   * Sharing ObjectTypes across namespaces
'   * Two plants with identical structure but separate nodes
'
' Connect with any OPC UA client to: opc.tcp://localhost:48410
' ==============================================================================

Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Server.Sdk
Imports System
Imports System.Collections.Generic
Imports System.Reflection
Module Program

    Sub Main(args As String())

        Dim LicenseUserName As String = "<Enter your UserName here>"
        Dim LicenseSerial As String = "<Enter your Serial here>"

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 16: Multiple Namespaces ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This example demonstrates:                                  ║")
        Console.WriteLine("║    * Registering additional namespaces                       ║")
        Console.WriteLine("║    * Creating nodes in specific namespaces                   ║")
        Console.WriteLine("║    * Sharing ObjectTypes across namespaces                   ║")
        Console.WriteLine("║    * Two plants with identical structure but separate nodes  ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        ' All server settings are defined in CreateConfig() below.
        Dim config = CreateConfig()
        PrintConfig(config)

        Using server As New UaServer(LicenseUserName, LicenseSerial)

            ' Accept all client certificates automatically.
            ' WARNING: Do Not use this in production! Either implement your own validation
            ' logic here (inspect e.Certificate And e.Error, then set e.Accept = true Or false),
            ' Or remove this handler entirely -- the SDK will then automatically validate
            ' certificates against the PKI trust store (pki/trusted/certs/).
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

            ' Show namespace table
            Dim nsTable = server.NodeManager.Server.NamespaceUris
            Console.WriteLine("-- Namespace table after Start() --------------------------------")
            For i As Integer = 0 To nsTable.Count - 1
                Console.WriteLine($"  ns={i}  {nsTable.GetString(CUInt(i))}")
            Next
            Console.WriteLine()

            ' Default namespace nodes
            Dim defaultFolder = server.CreateFolder("DefaultNS", UaRolePermissions.WITHOUT_RESTRICTIONS)
            Dim testValue1 = server.CreateVariable(Of Double)(defaultFolder, "TestValue1", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=42.0)
            Dim testValue2 = server.CreateVariable(Of String)(defaultFolder, "TestValue2", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:="hello")
            Console.WriteLine("-- Default namespace nodes ----------------------------------------")
            Console.WriteLine($"  {defaultFolder.Path,-40} NodeId={defaultFolder.NodeId}  BrowseName={defaultFolder.BrowseName}")
            Console.WriteLine($"  {testValue1.Path,-40} NodeId={testValue1.NodeId}  BrowseName={testValue1.BrowseName}")
            Console.WriteLine($"  {testValue2.Path,-40} NodeId={testValue2.NodeId}  BrowseName={testValue2.BrowseName}")
            Console.WriteLine()

            ' =================================================================
            ' Register additional namespaces
            ' =================================================================
            Console.WriteLine("-- Registering namespaces ---------------------------------------")

            Dim nsCompany As UShort = server.AddNamespace("urn:mycompany:types")
            Dim nsPlantA As UShort = server.AddNamespace("urn:mycompany:plant-a")
            Dim nsPlantB As UShort = server.AddNamespace("urn:mycompany:plant-b")

            Console.WriteLine($"  ns={nsCompany}  urn:mycompany:types     (company-wide types)")
            Console.WriteLine($"  ns={nsPlantA}  urn:mycompany:plant-a   (Plant A instances)")
            Console.WriteLine($"  ns={nsPlantB}  urn:mycompany:plant-b   (Plant B instances)")
            Console.WriteLine()

            Dim check As UShort = server.GetNamespaceIndex("urn:mycompany:plant-a")
            Console.WriteLine($"  GetNamespaceIndex(""urn:mycompany:plant-a"") = {check}")
            Console.WriteLine()

            ' =================================================================
            ' Company-wide ObjectTypes
            ' =================================================================
            Console.WriteLine($"-- Company-wide ObjectTypes (ns={nsCompany}) ----------------------------")

            Dim reactorTypeId = server.CreateObjectType("ReactorType", ns:=nsCompany)
            Dim mixerTypeId = server.CreateObjectType("MixerType", ns:=nsCompany)

            Console.WriteLine($"  ReactorType  {reactorTypeId}")
            Console.WriteLine($"  MixerType    {mixerTypeId}")
            Console.WriteLine()

            ' =================================================================
            ' Plant A
            ' =================================================================
            Console.WriteLine($"-- Plant A (ns={nsPlantA}) ---------------------------------------------")

            Dim plantA = server.CreateFolder("PlantA", UaRolePermissions.WITHOUT_RESTRICTIONS, ns:=nsPlantA)

            Dim reactorA = server.CreateObject(plantA, "Reactor", UaRolePermissions.WITHOUT_RESTRICTIONS, reactorTypeId)
            Dim tempA = server.CreateVariable(Of Double)(reactorA, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=85.0)
            Dim pressA = server.CreateVariable(Of Double)(reactorA, "Pressure", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=2.5)

            Dim mixerA = server.CreateObject(plantA, "Mixer", UaRolePermissions.WITHOUT_RESTRICTIONS, mixerTypeId)
            Dim speedA = server.CreateVariable(Of Double)(mixerA, "Speed", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=120.0)

            Console.WriteLine($"  {plantA.Path,-40} NodeId={plantA.NodeId}  BrowseName={plantA.BrowseName}")
            Console.WriteLine($"  {tempA.Path,-40} NodeId={tempA.NodeId}  BrowseName={tempA.BrowseName}")
            Console.WriteLine($"  {pressA.Path,-40} NodeId={pressA.NodeId}  BrowseName={pressA.BrowseName}")
            Console.WriteLine($"  {speedA.Path,-40} NodeId={speedA.NodeId}  BrowseName={speedA.BrowseName}")
            Console.WriteLine()

            ' =================================================================
            ' Plant B
            ' =================================================================
            Console.WriteLine($"-- Plant B (ns={nsPlantB}) ---------------------------------------------")

            Dim plantB = server.CreateFolder("PlantB", UaRolePermissions.WITHOUT_RESTRICTIONS, ns:=nsPlantB)

            Dim reactorB = server.CreateObject(plantB, "Reactor", UaRolePermissions.WITHOUT_RESTRICTIONS, reactorTypeId)
            Dim tempB = server.CreateVariable(Of Double)(reactorB, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=92.0)
            Dim pressB = server.CreateVariable(Of Double)(reactorB, "Pressure", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=3.1)

            Dim mixerB = server.CreateObject(plantB, "Mixer", UaRolePermissions.WITHOUT_RESTRICTIONS, mixerTypeId)
            Dim speedB = server.CreateVariable(Of Double)(mixerB, "Speed", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=80.0)

            Console.WriteLine($"  {plantB.Path,-40} NodeId={plantB.NodeId}  BrowseName={plantB.BrowseName}")
            Console.WriteLine($"  {tempB.Path,-40} NodeId={tempB.NodeId}  BrowseName={tempB.BrowseName}")
            Console.WriteLine($"  {pressB.Path,-40} NodeId={pressB.NodeId}  BrowseName={pressB.BrowseName}")
            Console.WriteLine($"  {speedB.Path,-40} NodeId={speedB.NodeId}  BrowseName={speedB.BrowseName}")
            Console.WriteLine()

            ' =================================================================
            ' Cross-namespace GetValue
            ' =================================================================
            Console.WriteLine("-- Cross-namespace GetValue -------------------------------------")

            Dim tA As Double = server.GetValue(Of Double)("Objects.PlantA.Reactor.Temperature")
            Dim tB As Double = server.GetValue(Of Double)("Objects.PlantB.Reactor.Temperature")
            Console.WriteLine($"  PlantA Reactor Temperature = {tA}")
            Console.WriteLine($"  PlantB Reactor Temperature = {tB}")
            Console.WriteLine()

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║")
            Console.WriteLine("║  opc.tcp://localhost:48410                                   ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Press ENTER to exit.                                        ║")
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
            Console.ReadLine()

        End Using

    End Sub


    ' ==========================================================================
    ' Helper: CreateConfig
    ' ==========================================================================
    ' Returns the server configuration. Adjust to your needs.
    Private Function CreateConfig() As UaServerConfiguration
        Dim cfg As New UaServerConfiguration
        ' ── Application Identity ──────────────────────────────────────────────
        cfg.ApplicationName = "PLCcom Workshop 16 - Multiple Namespaces"
        cfg.ApplicationUri  = "urn:localhost:PLCcom:Workshop:16"
        cfg.ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/"
        cfg.NamespaceUri    = "http://indi-an.com/opcua/workshop/multiple-namespaces"

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

        ' -- PKI Certificate Store -----------------------------------------------
        ' UaServerCertificateStore manages all server certificates.
        ' Load() tries to load existing certificates from disk.
        ' GetMissingOrExpired() returns certificates that need to be (re)created.
        ' Build(overwrite:=True) creates a new self-signed certificate on disk.
        ''
        ' One Application certificate is required for the OPC UA secure channel.
        ' One HTTPS certificate is added per opc.https:// hostname automatically.
        Dim certs As New List(Of UaServerCertificate) From {
            New UaServerCertificate(
                pkiBase:=".\pki",
                password:="secretpassword",
                alias:=Assembly.GetEntryAssembly().GetName().Name,
                applicationUri:=cfg.ApplicationUri,
                validityDays:=720,
                organisation:="Indi.An GmbH",
                role:=UaServerCertificate.CertificateRole.Application)
        }

        For Each host In UaServerCertificateStore.ExtractHttpsHostnames(cfg.BaseAddresses)
            certs.Add(New UaServerCertificate(
                pkiBase:=".\pki",
                password:="secretpassword",
                alias:=host,
                applicationUri:=$"urn:{host}:https",
                validityDays:=720,
                organisation:="Indi.An GmbH",
                role:=UaServerCertificate.CertificateRole.Https))
        Next

        Dim store = UaServerCertificateStore.Load(".\pki", certs)
        For Each missing In store.GetMissingOrExpired()
            missing.Build(overwrite:=True)
        Next
        cfg.SetCertificateStore(store)
                Return cfg
    End Function

    ' ==========================================================================
    ' Helper: PrintConfig
    ' ==========================================================================
' ==========================================================================
    ' Helper: PrintConfig
    ' ==========================================================================
    Private Sub PrintConfig(config As UaServerConfiguration)
        Console.WriteLine("-- Active Server Configuration ------------------------------")
        Console.WriteLine("  ApplicationName  : " & config.ApplicationName)
        Console.WriteLine("  ApplicationUri   : " & config.ApplicationUri)
        Console.WriteLine("  NamespaceUri     : " & If(config.NamespaceUri, "(default: ApplicationUri + /nodes)"))
        Console.WriteLine("  ManufacturerName : " & If(config.ManufacturerName, "(not set)"))
        Console.WriteLine("  ProductName      : " & If(config.ProductName, "(not set)"))
        Console.WriteLine("  SoftwareVersion  : " & If(config.SoftwareVersion, "(auto-detect)"))
        Console.WriteLine("  BuildNumber      : " & If(config.BuildNumber, "(auto-detect)"))
        Console.WriteLine()
        Console.WriteLine("  Endpoints:")
        For Each addr In config.BaseAddresses
            Console.WriteLine("    " & addr)
        Next
        Console.WriteLine()
        Console.WriteLine("  EndpointHostMode : " & config.EndpointHostMode.ToString())
        Console.WriteLine()
        Console.WriteLine("  Certificate Store:")
        If config.CertificateStore IsNot Nothing Then
            Console.WriteLine("    " & config.CertificateStore.ToString())
        Else
            Console.WriteLine("    (not set)")
        End If
        Console.WriteLine()
        Console.WriteLine("  VendorServerInfo (Server/VendorServerInfo):")
        Console.WriteLine("    VendorName           = " & If(config.VendorName, "(not set)"))
        Console.WriteLine("    VendorProductName    = " & If(config.VendorProductName, "(not set)"))
        Console.WriteLine("    VendorProductVersion = " & If(config.VendorProductVersion, "(not set)"))
        Console.WriteLine()
        Console.WriteLine("  OperationLimits (Server/ServerCapabilities/OperationLimits):")
        Console.WriteLine($"    MaxNodesPerRead                          = {config.MaxNodesPerRead}")
        Console.WriteLine($"    MaxNodesPerWrite                         = {config.MaxNodesPerWrite}")
        Console.WriteLine($"    MaxNodesPerBrowse                        = {config.MaxNodesPerBrowse}")
        Console.WriteLine($"    MaxNodesPerHistoryReadData               = {config.MaxNodesPerHistoryReadData}")
        Console.WriteLine($"    MaxNodesPerHistoryReadEvents             = {config.MaxNodesPerHistoryReadEvents}")
        Console.WriteLine($"    MaxNodesPerHistoryUpdateData             = {config.MaxNodesPerHistoryUpdateData}")
        Console.WriteLine($"    MaxNodesPerHistoryUpdateEvents           = {config.MaxNodesPerHistoryUpdateEvents}")
        Console.WriteLine($"    MaxNodesPerMethodCall                    = {config.MaxNodesPerMethodCall}")
        Console.WriteLine($"    MaxNodesPerRegisterNodes                 = {config.MaxNodesPerRegisterNodes}")
        Console.WriteLine($"    MaxNodesPerTranslateBrowsePathsToNodeIds = {config.MaxNodesPerTranslateBrowsePathsToNodeIds}")
        Console.WriteLine($"    MaxNodesPerNodeManagement                = {config.MaxNodesPerNodeManagement}")
        Console.WriteLine($"    MaxMonitoredItemsPerCall                 = {config.MaxMonitoredItemsPerCall}")
        Console.WriteLine("-------------------------------------------------------------")
        Console.WriteLine()
    End Sub

End Module