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
' PLCcom OPC UA Server SDK - Workshop 35: Custom Event History Store
'
' Workshop 33 showed how to record historical events using the default
' in-memory store. This workshop shows how to replace that store with
' your own implementation using the IEventHistoryStore interface.
'
' This workshop demonstrates the pattern using CSV files as the back-end.
' Replace CsvEventHistoryStore with your own implementation for real use.
'
' Connect with any OPC UA client to: opc.tcp://localhost:48410
' ==============================================================================

Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Server.Sdk
Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Threading
Module Program

    Sub Main(args As String())

        Dim LicenseUserName As String = "<Enter your UserName here>"
        Dim LicenseSerial As String = "<Enter your Serial here>"

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 35:                     ║")
        Console.WriteLine("║                         Custom Event History Store           ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  IEventHistoryStore lets you connect ANY storage back-end:   ║")
        Console.WriteLine("║    SQL Server, PostgreSQL, SQLite, InfluxDB, TimescaleDB,    ║")
        Console.WriteLine("║    Azure Blob, AWS S3, Kafka, custom files, and more.        ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This workshop uses CSV files to demonstrate the pattern.    ║")
        Console.WriteLine("║  Replace CsvEventHistoryStore with your own implementation.  ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Dim config = CreateConfig()
        PrintConfig(config)

        Using server As New UaServer(LicenseUserName, LicenseSerial)

            ' Accept all client certificates automatically.
            ' WARNING: Do Not use this in production! Either implement your own validation
            ' logic here (inspect e.Certificate And e.Error, then set e.Accept = true Or false),
            ' Or remove this handler entirely -- the SDK will then automatically validate
            ' certificates against the PKI trust store (pki/trusted/certs/).
            AddHandler server.CertificateValidation, Sub(s, e) e.Accept = True

            server.EventHistoryStore = New CsvEventHistoryStore(".\event_history")

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

            server.EnableEvents(reactor)
            server.EnableHistoryEvents(reactor, maxEntries:=500)

            Console.WriteLine("  Event history store: CsvEventHistoryStore -> .\event_history\")
            Console.WriteLine("  Reactor:")
            Console.WriteLine("    Temperature (0-200 C)")
            Console.WriteLine("    Events: live + history enabled (max 500 entries)")
            Console.WriteLine()

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║")
            Console.WriteLine("║  opc.tcp://localhost:48410                                   ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Event history is written to CSV files in .\event_history\  ║")
            Console.WriteLine("║  Restart the server - event history will still be available! ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Press ENTER to start the simulation.                        ║")
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
            Console.ReadLine()

            Console.WriteLine("Simulating... events fire every 5 seconds (CTRL+C to exit)")
            Console.WriteLine("  Temperature > 80C -> High severity event")
            Console.WriteLine("  Temperature > 60C -> Medium severity event")
            Console.WriteLine("  Temperature <= 60C -> Low severity event")
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

                server.FireEvent(reactor, message, severity)

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
    Private Function CreateConfig() As UaServerConfiguration
        Dim cfg As New UaServerConfiguration
        ' ── Application Identity ──────────────────────────────────────────────
        cfg.ApplicationName = "PLCcom Workshop 35 - Custom Event History Store"
        cfg.ApplicationUri  = "urn:localhost:PLCcom:Workshop:35"
        cfg.ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/"
        cfg.NamespaceUri    = "http://indi-an.com/opcua/workshop/custom-event-history"

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
        cfg.MaxSessionCount = 100 : cfg.ShutdownDelay = 5

        ' ── VendorServerInfo ──────────────────────────────────────────────────
        cfg.VendorName = "My Company GmbH" : cfg.VendorProductName = "My OPC UA Server" : cfg.VendorProductVersion = "1.0.0"

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

' ==============================================================================
' CsvEventHistoryStore - example IEventHistoryStore implementation using CSV files
'
' This class exists solely to demonstrate HOW to implement IEventHistoryStore.
' CSV is not recommended for production - use a database or time-series store.
' ==============================================================================

Public Class CsvEventHistoryStore
    Implements IEventHistoryStore

    Private ReadOnly m_directory As String
    Private ReadOnly m_lock As New Object()

    Public Sub New(directory As String)
        m_directory = directory
        IO.Directory.CreateDirectory(directory)
    End Sub

    Public Sub Initialize(sourceNodeId As NodeId, maxEntries As Integer) Implements IEventHistoryStore.Initialize
    End Sub

    Public Sub Append(sourceNodeId As NodeId, entry As UaHistoryEventEntry) Implements IEventHistoryStore.Append
        SyncLock m_lock
            Dim eid As String = If(entry.EventId IsNot Nothing, Convert.ToBase64String(entry.EventId), "")
            File.AppendAllText(FilePath(sourceNodeId), $"{eid};{entry.Time:O};{entry.SourceName};{entry.Message};{entry.Severity}" & vbLf)
        End SyncLock
    End Sub

    Public Function Read(sourceNodeId As NodeId, start As DateTime, [end] As DateTime, Optional maxValues As Integer = 0) As IReadOnlyList(Of UaHistoryEventEntry) Implements IEventHistoryStore.Read
        SyncLock m_lock
            Dim path = FilePath(sourceNodeId)
            If Not File.Exists(path) Then Return Array.Empty(Of UaHistoryEventEntry)()
            Dim result As New List(Of UaHistoryEventEntry)
            For Each line In File.ReadLines(path)
                Dim entry = ParseLine(line)
                If entry Is Nothing Then Continue For
                If start <> DateTime.MinValue AndAlso entry.Time < start Then Continue For
                If [end] <> DateTime.MinValue AndAlso entry.Time > [end] Then Continue For
                result.Add(entry)
                If maxValues > 0 AndAlso result.Count >= maxValues Then Exit For
            Next
            Return result
        End SyncLock
    End Function

    Public Function Delete(sourceNodeId As NodeId, eventIds As IList(Of Byte())) As IReadOnlyList(Of StatusCode) Implements IEventHistoryStore.Delete
        Dim results As New List(Of StatusCode)
        If eventIds Is Nothing OrElse eventIds.Count = 0 Then Return results
        SyncLock m_lock
            Dim path = FilePath(sourceNodeId)
            If Not File.Exists(path) Then
                For Each id In eventIds
                    results.Add(StatusCodes.BadNoEntryExists)
                Next
                Return results
            End If
            Dim lines = File.ReadAllLines(path).ToList()
            For Each eid In eventIds
                Dim key As String = If(eid IsNot Nothing, Convert.ToBase64String(eid), "")
                Dim idx = lines.FindIndex(Function(l) l.StartsWith(key & ";"))
                If idx >= 0 Then
                    lines.RemoveAt(idx)
                    results.Add(StatusCodes.Good)
                Else
                    results.Add(StatusCodes.BadNoEntryExists)
                End If
            Next
            File.WriteAllLines(path, lines)
        End SyncLock
        Return results
    End Function

    Public Sub Remove(sourceNodeId As NodeId) Implements IEventHistoryStore.Remove
        SyncLock m_lock
            Dim path = FilePath(sourceNodeId)
            If File.Exists(path) Then File.Delete(path)
        End SyncLock
    End Sub

    Private Function FilePath(sourceNodeId As NodeId) As String
        Return Path.Combine(m_directory, sourceNodeId.ToString().Replace(":", "_").Replace(";", "_") & ".csv")
    End Function

    Private Shared Function ParseLine(line As String) As UaHistoryEventEntry
        If String.IsNullOrWhiteSpace(line) Then Return Nothing
        Dim parts = line.Split(";"c)
        If parts.Length < 5 Then Return Nothing
        Dim eventId As Byte() = If(Not String.IsNullOrEmpty(parts(0)), Convert.FromBase64String(parts(0)), Nothing)
        Dim time As DateTime
        If Not DateTime.TryParse(parts(1), Nothing, DateTimeStyles.RoundtripKind, time) Then Return Nothing
        Dim severity As UShort
        If Not UShort.TryParse(parts(4), severity) Then severity = 500
        Return New UaHistoryEventEntry(eventId, time, parts(2), parts(3), severity, Nothing)
    End Function

End Class