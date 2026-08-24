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
' PLCcom OPC UA Server SDK - Workshop 33: Historical Events
'
' OPC UA servers can store events in a history that clients can query later.
' This workshop demonstrates enabling event history, recording events,
' and serving them to clients via HistoryRead.
'
' Connect with any OPC UA client to: opc.tcp://localhost:48410
' ==============================================================================


Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Server.Sdk
Imports System
Imports System.Collections.Generic
Imports System.Reflection
Imports System.Threading
Module Program

    Sub Main(args As String())

        ' Important !!!!!!!!!!!!!!!!!!
        ' Enter your Username + Serial here! Please note: with blank fields the library runs
        ' for 15 minutes during a debug session. Both values can also come
        ' from configuration or an environment variable.
        ' Free trial license (14 days, uninterrupted): https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-download/
        Dim LicenseUserName As String = ""
        Dim LicenseSerial As String = ""

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 33: Historical Events   ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This example demonstrates:                                  ║")
        Console.WriteLine("║  * Enabling event history on source nodes                    ║")
        Console.WriteLine("║  * Recording events in the history store                     ║")
        Console.WriteLine("║  * Clients can query past events via HistoryRead             ║")
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

            AddHandler server.HistoryUpdated, Sub(s, e)
                                                  Dim count As Object = If(e.Values IsNot Nothing AndAlso e.Values.Length > 0, e.Values(0), Nothing)
                                                  Dim detail As String = If(TypeOf count Is Integer,
                                                      $"deleted {count} event(s)", "(range delete)")
                                                  Console.WriteLine($"  << {e.Operation,-15}  {detail}  path={e.Path}")
                                              End Sub

            Dim plant = server.CreateFolder("Plant", UaRolePermissions.WITHOUT_RESTRICTIONS)
            Dim reactor = server.CreateFolder(plant, "Reactor", UaRolePermissions.WITHOUT_RESTRICTIONS)

            Dim temperature = server.CreateVariable(Of Double)(reactor, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=25.0)
            temperature.SetEURange(0, 200)
            temperature.SetEngineeringUnits("C", "Degrees Celsius")

            ' Enable live events + event history
            server.EnableEvents(reactor)
            server.EnableHistoryEvents(reactor, maxEntries:=500)

            Console.WriteLine("  Reactor: Events live + history enabled (max 500)")
            Console.WriteLine()

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running on: opc.tcp://localhost:48410             ║")
            Console.WriteLine("║  Use Client Workshop 42 to read historical events.           ║")
            Console.WriteLine("║  Press ENTER to start the simulation.                        ║")
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
            Console.ReadLine()

            Console.WriteLine("Simulating... events fire every 5 seconds (CTRL+C to exit)")
            Console.WriteLine()

            Dim rng As New Random()
            Dim cycle As Long = 0

            While True
                cycle += 1

                Dim t As Double = 50.0 + Math.Sin(cycle * 0.15) * 40.0 + rng.NextDouble() * 5.0
                temperature.Value = Math.Round(t, 1)

                Dim severity As EventSeverity
                Dim message As String
                If t > 80.0 Then
                    severity = EventSeverity.High
                    message = $"Temperature HIGH: {t:F1}C"
                ElseIf t > 60.0 Then
                    severity = EventSeverity.Medium
                    message = $"Temperature warning: {t:F1}C"
                Else
                    severity = EventSeverity.Low
                    message = $"Temperature normal: {t:F1}C"
                End If

                ' Fire live event
                server.FireEvent(reactor, message, severity)

                ' Record in history
                Dim eventState As New BaseEventState(Nothing)
                eventState.Initialize(
                    server.NodeManager.SystemContext,
                    server.NodeManager.FindNodeInAddressSpace(reactor.NodeId),
                    severity,
                    New LocalizedText(message))
                eventState.Create(server.NodeManager.SystemContext, Nothing, New QualifiedName("Event"), Nothing, True)
                server.RecordHistoryEvent(reactor.NodeId, eventState)

                Dim severityLabel As String = If(severity = EventSeverity.High, "HIGH",
                    If(severity = EventSeverity.Medium, "MED ", "LOW "))
                Console.WriteLine($"  [{severityLabel}] {message}")

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
        cfg.ApplicationName = "PLCcom Workshop 33 - Historical Events"
        cfg.ApplicationUri  = "urn:localhost:PLCcom:Workshop:33"
        cfg.ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/"
        cfg.NamespaceUri    = "http://indi-an.com/opcua/workshop/historical-events"

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