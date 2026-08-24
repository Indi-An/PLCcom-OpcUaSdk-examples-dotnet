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
' PLCcom OPC UA Server SDK - Workshop 41: NodeSet Import
'
' OPC UA NodeSet2 XML is the standard format for sharing address space
' definitions. It is used by:
'   * OPC UA Companion Specifications (PackML, Euromap, DI, Machinery, etc.)
'   * Vendor-specific type libraries
'   * Pre-defined address space templates
'
' After importing, the types appear in the server's type hierarchy and
' can be used to create typed instances with CreateObject().
'
' This workshop includes a ready-to-use sample NodeSet:
'   PLCcom_Workshop_NodeSet.xml
' It defines MotorType and SensorType with two instances each.
'
' What you will learn:
'   * How to import a NodeSet2.xml file into the server
'   * How namespaces from the NodeSet are registered automatically
'   * How to verify the imported nodes in the address space
'
' Connect with any OPC UA client to: opc.tcp://localhost:48410
' ==============================================================================

Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Server.Sdk
Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Reflection
Module Program

    Sub Main(args As String())

        ' -- License -----------------------------------------------------------
        ' TODO: Submit your license information from your license e-mail
        ' Important !!!!!!!!!!!!!!!!!!
        ' Enter your Username + Serial here! Please note: with blank fields the library runs
        ' for 15 minutes during a debug session. Both values can also come
        ' from configuration or an environment variable.
        ' Free trial license (14 days, uninterrupted): https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-download/
        Dim LicenseUserName As String = ""
        Dim LicenseSerial As String = ""

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 41: NodeSet Import      ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This example demonstrates:                                  ║")
        Console.WriteLine("║  * Importing NodeSet2.xml files into the address space       ║")
        Console.WriteLine("║  * Automatic namespace registration                          ║")
        Console.WriteLine("║  * Types and instances from companion specifications         ║")
        Console.WriteLine("║  * Verifying imported nodes                                  ║")
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

            ' -- Import NodeSet XML --------------------------------------------
            ' ImportNodeSet() reads the XML file and adds all nodes to the address space.
            ' Namespaces defined in the NodeSet are automatically registered.
            ' The method returns the number of nodes imported.
            '
            ' The NodeSet2 XML format is defined by the OPC Foundation in:
            '   OPC UA Specification Part 6 - Mappings, Annex F (UANodeSet XML Schema)
            '
            ' PLCcom_Workshop_NodeSet.xml is included with this workshop.
            ' It defines:
            '   MotorType  - Speed (Double), Running (Boolean), SerialNumber (String)
            '   SensorType - Value (Double), Unit (String), InAlarm (Boolean)
            ' Plus two instances of each type under Motors/ and Sensors/.
            Dim nodeSetPath As String = "PLCcom_Workshop_NodeSet.xml"

            If File.Exists(nodeSetPath) Then
                Console.WriteLine($"  Importing: {nodeSetPath}")
                Dim count As Integer = server.ImportNodeSet(nodeSetPath)
                Console.WriteLine($"  Imported {count} nodes successfully")
                Console.WriteLine()
                Console.WriteLine("  Nodes imported:")
                Console.WriteLine("    Types    -> Types/ObjectTypes/MotorType")
                Console.WriteLine("    Types    -> Types/ObjectTypes/SensorType")
                Console.WriteLine("    Instance -> Objects/Motors/Motor1, Motor2")
                Console.WriteLine("    Instance -> Objects/Sensors/TempSensor1, PressureSensor1")
            Else
                Console.WriteLine($"  ERROR: '{nodeSetPath}' not found.")
                Console.WriteLine("  Make sure PLCcom_Workshop_NodeSet.xml is in the same folder as this executable.")
            End If
            Console.WriteLine()

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║")
            Console.WriteLine("║  opc.tcp://localhost:48410                                   ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Try:                                                        ║")
            Console.WriteLine("║  * Browse Objects -> Motors -> Motor1 -> Speed, Running      ║")
            Console.WriteLine("║  * Browse Objects -> Sensors -> TempSensor1 -> Value, Unit   ║")
            Console.WriteLine("║  * Browse Types -> ObjectTypes -> MotorType, SensorType      ║")
            Console.WriteLine("║  * Check Server -> NamespaceArray for the imported namespace ║")
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
        cfg.ApplicationName = "PLCcom Workshop 41 - NodeSet Import"
        cfg.ApplicationUri  = "urn:localhost:PLCcom:Workshop:41"
        cfg.ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/"
        cfg.NamespaceUri    = "http://indi-an.com/opcua/workshop/nodeset-import"

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
        cfg.ShutdownDelay   = 5

        ' ── VendorServerInfo ──────────────────────────────────────────────────
        cfg.VendorName           = "My Company GmbH"
        cfg.VendorProductName    = "My OPC UA Server"
        cfg.VendorProductVersion = "1.0.0"

        ' ── OperationLimits ───────────────────────────────────────────────────
        cfg.MaxNodesPerRead                      = 1000
        cfg.MaxNodesPerWrite                     = 1000
        cfg.MaxNodesPerBrowse                    = 1000
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
        ' One default HTTPS certificate is presented at every opc.https TLS handshake.
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

        ' One default HTTPS certificate for all opc.https ports. The SDK presents it at the
        ' TLS handshake for any opc.https port that has no specifically assigned certificate.
        ' To serve an official domain certificate on a port, create another HTTPS certificate
        ' and assign it: cfg.AssignHttpsCertificateToPort(port, cert).
        Dim httpsDefault As New UaServerCertificate(
            pkiBase:=".\pki",
            password:="secretpassword",
            alias:="https-default",
            applicationUri:="urn:https-default:https",
            validityDays:=720,
            organisation:="Indi.An GmbH",
            role:=UaServerCertificate.CertificateRole.Https)
        certs.Add(httpsDefault)
        cfg.SetDefaultHttpsCertificate(httpsDefault)

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