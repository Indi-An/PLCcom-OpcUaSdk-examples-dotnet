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

' ==============================================================================
' PLCcom OPC UA Server SDK - Workshop 51: Logging
'
' The SDK exposes the OPC UA stack's internal trace messages through the
' LogMessage event. This lets you route SDK diagnostics to your own
' logging framework (NLog, Serilog, Microsoft.Extensions.Logging, etc.)
'
' Log levels (from least to most verbose):
'   None    -> logging disabled, no messages generated
'   Error   -> only errors that affect functionality
'   Warning -> errors + warnings (recommended for production)
'   Info    -> errors + warnings + service calls (connect, read, write, subscribe)
'   Debug   -> everything including internal stack details (very verbose)
'
' What you will learn:
'   * How to subscribe to SDK log messages
'   * How to set the log verbosity level
'   * How to filter and format log messages
'   * How to route logs to your own framework
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
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 51: Logging             ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This example demonstrates:                                  ║")
        Console.WriteLine("║  * Subscribing to SDK log messages                           ║")
        Console.WriteLine("║  * Setting log verbosity (None, Error, Warning, Info, Debug) ║")
        Console.WriteLine("║  * Filtering log messages by level                           ║")
        Console.WriteLine("║  * Routing logs to your own framework                        ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        ' All server settings are defined in CreateConfig() below.
        Dim config = CreateConfig()
        PrintConfig(config)

        Using server As New UaServer(LicenseUserName, LicenseSerial)
            AddHandler server.CertificateValidation, Sub(s, e) e.Accept = True

            ' -- Subscribe to log messages -------------------------------------
            ' The LogMessage event fires for every message that passes the current log level.
            ' Subscribe BEFORE calling Start() to capture startup messages.
            ' In production, replace Console.WriteLine with your logging framework call.
            AddHandler server.LogMessage, Sub(sender, e)
                                              ' Color-code by severity for easy reading in the console
                                              Select Case e.Level
                                                  Case UaLogLevel.Error
                                                      Console.ForegroundColor = ConsoleColor.Red
                                                  Case UaLogLevel.Warning
                                                      Console.ForegroundColor = ConsoleColor.Yellow
                                                  Case UaLogLevel.Info
                                                      Console.ForegroundColor = ConsoleColor.Cyan
                                                  Case Else
                                                      Console.ForegroundColor = ConsoleColor.Gray ' Debug
                                              End Select

                                              Console.WriteLine($"  [{e.Level,-7}] {e.Message}")
                                              Console.ResetColor()

                                              ' Example: forward to your logging framework
                                              ' logger.Log(e.Level, e.Message, e.Exception)
                                          End Sub

            ' -- Set log level -------------------------------------------------
            ' SetLogLevel() controls which messages are generated by the OPC UA stack.
            ' Call this before Start() to capture startup messages at the desired level.
            '   None    -> logging disabled (no messages at all)
            '   Error   -> only errors (minimal output, good for production)
            '   Warning -> errors + warnings
            '   Info    -> errors + warnings + service calls (good for development)
            '   Debug   -> everything (very verbose, use only for troubleshooting)
            server.SetLogLevel(UaLogLevel.Info)

            Console.WriteLine("  Log level set to: Info")
            Console.WriteLine("  (connect a client to see log messages appear here)")
            Console.WriteLine()

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
            server.CreateVariable(Of Double)(plant, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=22.0)

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running with logging enabled.                     ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Endpoint: opc.tcp://localhost:48410                         ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Try:                                                        ║")
            Console.WriteLine("║  * Connect a client - see the session creation log entry     ║")
            Console.WriteLine("║  * Read Temperature - see the Read service log entry         ║")
            Console.WriteLine("║  * Subscribe to Temperature - see subscription log entries   ║")
            Console.WriteLine("║  * Disconnect - see the session close log entry              ║")
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
        cfg.ApplicationName = "PLCcom Workshop 51 - Logging"
        cfg.ApplicationUri  = "urn:localhost:PLCcom:Workshop:51"
        cfg.ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/"
        cfg.NamespaceUri    = "http://indi-an.com/opcua/workshop/logging"

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
