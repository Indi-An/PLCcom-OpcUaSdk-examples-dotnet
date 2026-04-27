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
' PLCcom OPC UA Server SDK - Workshop 31: Historical Access
'
' OPC UA Historical Access (Part 11) allows clients to read past values
' of variables using the HistoryRead service.
'
' How it works:
'   1. Call EnableHistory() to start recording values for a variable
'   2. Call RecordHistoryValue() each time you want to store a value
'   3. Clients use HistoryRead to retrieve values for a time range
'
' The SDK stores history in memory (in-process).
' For production use, you would typically store history in a database.
'
' What you will learn:
'   * How to enable history recording on variables
'   * How to record values with timestamps
'   * How clients read historical data
'
' Connect with any OPC UA client to: opc.tcp://localhost:48410
' ==============================================================================

Module Program

    Sub Main(args As String())

        ' -- License -----------------------------------------------------------
        ' TODO: Submit your license information from your license e-mail
        Dim LicenseUserName As String = "<Enter your UserName here>"
        Dim LicenseSerial As String = "<Enter your Serial here>"

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 31: Historical Access   ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This example demonstrates:                                  ║")
        Console.WriteLine("║  * Enabling history on variables (Historizing = true)        ║")
        Console.WriteLine("║  * Recording values every second                             ║")
        Console.WriteLine("║  * Clients can read history via HistoryRead service          ║")
        Console.WriteLine("║  * In-memory store with max 500 entries per variable         ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        ' All server settings are defined in CreateConfig() below.
        Dim config = CreateConfig()
        PrintConfig(config)

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

            Dim plant = server.CreateFolder("Plant", UaRolePermissions.WITHOUT_RESTRICTIONS)
            Dim sensor = server.CreateFolder(plant, "Sensor", UaRolePermissions.WITHOUT_RESTRICTIONS)

            Dim temperature = server.CreateVariable(Of Double)(sensor, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=20.0)
            Dim humidity = server.CreateVariable(Of Double)(sensor, "Humidity", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=50.0)

            temperature.SetEURange(-40, 120)
            temperature.SetEngineeringUnits("C")
            humidity.SetEURange(0, 100)
            humidity.SetEngineeringUnits("%RH")

            ' -- Enable history recording --------------------------------------
            ' EnableHistory() sets the Historizing attribute to true on the variable.
            ' This tells clients that historical data is available for this variable.
            ' maxEntries limits the in-memory buffer size - oldest entries are discarded
            ' when the buffer is full (circular buffer behavior).
            server.EnableHistory(temperature, maxEntries:=500)
            server.EnableHistory(humidity, maxEntries:=500)

            Console.WriteLine("  Variables with history enabled:")
            Console.WriteLine("    Temperature: Historizing=true, max 500 entries")
            Console.WriteLine("    Humidity:    Historizing=true, max 500 entries")
            Console.WriteLine()

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║")
            Console.WriteLine("║  opc.tcp://localhost:48410                                   ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  To view history:                                            ║")
            Console.WriteLine("║  1. Press ENTER to start recording values                    ║")
            Console.WriteLine("║  2. Wait 30+ seconds to accumulate some history              ║")
            Console.WriteLine("║  3. In the client, right-click Temperature -> History        ║")
            Console.WriteLine("║     or add it to a History Trend View                        ║")
            Console.WriteLine("║  4. Set a time range and read the historical values          ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Press ENTER to start recording.                             ║")
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
            Console.ReadLine()

            Console.WriteLine("Recording history every second... (CTRL+C to exit)")
            Dim rng As New Random()
            Dim cycle As Long = 0

            While True
                cycle += 1
                Dim now As DateTime = DateTime.UtcNow

                ' Simulate sinusoidal sensor values with some noise
                Dim t As Double = 20.0 + Math.Sin(cycle * 0.1) * 10.0 + rng.NextDouble() * 2.0
                Dim h As Double = 50.0 + Math.Cos(cycle * 0.08) * 20.0 + rng.NextDouble() * 3.0
                temperature.Value = Math.Round(t, 1)
                humidity.Value = Math.Round(h, 1)

                ' RecordHistoryValue() stores the current value with the given timestamp.
                ' Always use the same timestamp for the variable update and the history record
                ' to ensure consistency between the current value and the history.
                server.RecordHistoryValue(temperature, now)
                server.RecordHistoryValue(humidity, now)

                Dim hist = server.GetHistory(temperature.NodeId)
                Console.Write($"{vbCr}  Cycle={cycle}  T={temperature.Value:F1}C  " &
                              $"H={humidity.Value:F1}%RH  History={hist.Count} entries  ")
                Thread.Sleep(1000)
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
        cfg.ApplicationName = "PLCcom Workshop 31 - Historical Access"
        cfg.ApplicationUri  = "urn:localhost:PLCcom:Workshop:31"
        cfg.ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/"
        cfg.NamespaceUri    = "http://indi-an.com/opcua/workshop/historical-access"

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
