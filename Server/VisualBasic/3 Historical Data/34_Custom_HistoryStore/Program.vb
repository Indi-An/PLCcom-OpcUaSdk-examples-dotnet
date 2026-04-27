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
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Threading

' ==============================================================================
' PLCcom OPC UA Server SDK - Workshop 34: Custom History Store
'
' By default the SDK keeps all historical values in RAM (InMemoryHistoryStore).
' This works well for testing but is not suitable for production.
'
' The SDK solves this with the IHistoryStore interface.
' IHistoryStore is the extension point that lets YOU decide where history
' data is stored. You implement the interface once and the SDK calls it
' automatically whenever values are recorded or clients request history.
'
' This workshop demonstrates the pattern using CSV files as the back-end.
' Replace CsvHistoryStore with your own implementation for real use.
'
' Connect with any OPC UA client to: opc.tcp://localhost:48410
' ==============================================================================

Module Program

    Sub Main(args As String())

        Dim LicenseUserName As String = "<Enter your UserName here>"
        Dim LicenseSerial As String = "<Enter your Serial here>"

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 34: Custom History Store║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  IHistoryStore lets you connect ANY storage back-end:        ║")
        Console.WriteLine("║    SQL Server, PostgreSQL, SQLite, InfluxDB, TimescaleDB,    ║")
        Console.WriteLine("║    Azure Blob, AWS S3, Kafka, custom files, and more.        ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This workshop uses CSV files to demonstrate the pattern.    ║")
        Console.WriteLine("║  Replace CsvHistoryStore with your own implementation.       ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Dim config = CreateConfig()
        PrintConfig(config)

        Using server As New UaServer(LicenseUserName, LicenseSerial)
            AddHandler server.CertificateValidation, Sub(s, e) e.Accept = True

            server.HistoryStore = New CsvHistoryStore(".\history")

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
                Dim detail As String
                If e.Operation = UaHistoryUpdateOperation.DeleteAtTime Then
                    detail = $"deleted {e.Timestamps.Length} entries"
                ElseIf e.Timestamps.Length > 0 Then
                    Dim parts(e.Timestamps.Length - 1) As String
                    For i As Integer = 0 To e.Timestamps.Length - 1
                        Dim ts As String = e.Timestamps(i).ToLocalTime().ToString("HH:mm:ss.fff")
                        Dim val As String = If(e.Values IsNot Nothing AndAlso i < e.Values.Length AndAlso e.Values(i) IsNot Nothing,
                            $"  value={e.Values(i),-10}", String.Empty)
                        parts(i) = ts & val
                    Next
                    detail = String.Join(vbCrLf & "                          ", parts)
                Else
                    detail = "(range delete)"
                End If
                Console.WriteLine($"{vbCrLf}  << {e.Operation,-15}  {detail}  path={e.Path}")
            End Sub

            Dim plant = server.CreateFolder("Plant", UaRolePermissions.WITHOUT_RESTRICTIONS)
            Dim sensor = server.CreateFolder(plant, "Sensor", UaRolePermissions.WITHOUT_RESTRICTIONS)

            Dim temperature = server.CreateVariable(Of Double)(sensor, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=20.0)
            Dim humidity = server.CreateVariable(Of Double)(sensor, "Humidity", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=50.0)

            temperature.SetEURange(-40, 120)
            temperature.SetEngineeringUnits("C")
            humidity.SetEURange(0, 100)
            humidity.SetEngineeringUnits("%RH")

            server.EnableHistory(temperature, maxEntries:=500)
            server.EnableHistory(humidity, maxEntries:=500)

            Console.WriteLine("  History store: CsvHistoryStore -> .\history\")
            Console.WriteLine("  Variables with history enabled:")
            Console.WriteLine("    Temperature: CSV file, max 500 entries")
            Console.WriteLine("    Humidity:    CSV file, max 500 entries")
            Console.WriteLine()

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║")
            Console.WriteLine("║  opc.tcp://localhost:48410                                   ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  History is written to CSV files in .\history\              ║")
            Console.WriteLine("║  Restart the server - history will still be available!       ║")
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
                Dim t As Double = 20.0 + Math.Sin(cycle * 0.1) * 10.0 + rng.NextDouble() * 2.0
                Dim h As Double = 50.0 + Math.Cos(cycle * 0.08) * 20.0 + rng.NextDouble() * 3.0
                temperature.Value = Math.Round(t, 1)
                humidity.Value = Math.Round(h, 1)
                server.RecordHistoryValue(temperature, now)
                server.RecordHistoryValue(humidity, now)
                Console.Write($"{vbCr}  Cycle={cycle}  T={temperature.Value:F1}C  H={humidity.Value:F1}%RH  ")
                Thread.Sleep(1000)
            End While

        End Using

    End Sub

    ' ==========================================================================
    ' Helper: CreateConfig
    ' ==========================================================================
    Private Function CreateConfig() As UaServerConfiguration
        Dim cfg As New UaServerConfiguration
        ' ── Application Identity ──────────────────────────────────────────────
        cfg.ApplicationName = "PLCcom Workshop 34 - Custom History Store"
        cfg.ApplicationUri  = "urn:localhost:PLCcom:Workshop:34"
        cfg.ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/"
        cfg.NamespaceUri    = "http://indi-an.com/opcua/workshop/custom-history-store"

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
        cfg.ShutdownDelay   = 5

        ' ── VendorServerInfo ──────────────────────────────────────────────────
        cfg.VendorName           = "My Company GmbH"
        cfg.VendorProductName = "My OPC UA Server"
        cfg.VendorProductVersion = "1.0.0"
        cfg.MaxNodesPerRead = 1000
        cfg.MaxNodesPerWrite = 1000
        cfg.MaxNodesPerBrowse = 1000
        cfg.MaxNodesPerHistoryReadData = 100
        cfg.MaxNodesPerHistoryReadEvents = 100
        cfg.MaxNodesPerHistoryUpdateData = 100
        cfg.MaxNodesPerHistoryUpdateEvents = 100
        cfg.MaxNodesPerMethodCall = 200
        cfg.MaxNodesPerRegisterNodes = 1000
        cfg.MaxNodesPerTranslateBrowsePathsToNodeIds = 1000
        cfg.MaxNodesPerNodeManagement = 1000
        cfg.MaxMonitoredItemsPerCall = 1000
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
        For Each addr In config.BaseAddresses
            Console.WriteLine("    " & addr)
        Next
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

' ==============================================================================
' CsvHistoryStore - example IHistoryStore implementation using CSV files
' ==============================================================================

Public Class CsvHistoryStore
    Implements IHistoryStore

    Private ReadOnly m_directory As String
    Private ReadOnly m_lock As New Object()

    Public Sub New(directory As String)
        m_directory = directory
        IO.Directory.CreateDirectory(directory)
    End Sub

    Public Sub Initialize(nodeId As NodeId, maxEntries As Integer) Implements IHistoryStore.Initialize
    End Sub

    Public Sub Append(nodeId As NodeId, entry As UaHistoryEntry) Implements IHistoryStore.Append
        SyncLock m_lock
            File.AppendAllText(FilePath(nodeId), $"{entry.Timestamp:O},{entry.Value},{entry.StatusCode}" & vbLf)
        End SyncLock
    End Sub

    Public Function Read(nodeId As NodeId, start As DateTime, [end] As DateTime, Optional maxValues As Integer = 0) As IReadOnlyList(Of UaHistoryEntry) Implements IHistoryStore.Read
        SyncLock m_lock
            Dim path = FilePath(nodeId)
            If Not File.Exists(path) Then Return Array.Empty(Of UaHistoryEntry)()
            Dim result As New List(Of UaHistoryEntry)
            For Each line In File.ReadLines(path)
                Dim entry = ParseLine(line)
                If entry Is Nothing Then Continue For
                If start <> DateTime.MinValue AndAlso entry.Timestamp < start Then Continue For
                If [end] <> DateTime.MinValue AndAlso entry.Timestamp > [end] Then Continue For
                result.Add(entry)
                If maxValues > 0 AndAlso result.Count >= maxValues Then Exit For
            Next
            Return result
        End SyncLock
    End Function

    Public Function InsertOrReplace(nodeId As NodeId, entry As UaHistoryEntry, mode As PerformUpdateType) As StatusCode Implements IHistoryStore.InsertOrReplace
        SyncLock m_lock
            Dim all = LoadAll(nodeId)
            Dim idx = all.FindIndex(Function(e) e.Timestamp = entry.Timestamp)
            Select Case mode
                Case PerformUpdateType.Insert
                    If idx >= 0 Then Return StatusCodes.BadEntryExists
                    all.Add(entry)
                    all.Sort(Function(a, b) a.Timestamp.CompareTo(b.Timestamp))
                    SaveAll(nodeId, all)
                    Return StatusCodes.GoodEntryInserted
                Case PerformUpdateType.Replace
                    If idx < 0 Then Return StatusCodes.BadNoEntryExists
                    all(idx) = entry
                    SaveAll(nodeId, all)
                    Return StatusCodes.GoodEntryReplaced
                Case PerformUpdateType.Update
                    If idx >= 0 Then
                        all(idx) = entry
                        SaveAll(nodeId, all)
                        Return StatusCodes.GoodEntryReplaced
                    End If
                    all.Add(entry)
                    all.Sort(Function(a, b) a.Timestamp.CompareTo(b.Timestamp))
                    SaveAll(nodeId, all)
                    Return StatusCodes.GoodEntryInserted
                Case PerformUpdateType.Remove
                    If idx < 0 Then Return StatusCodes.BadNoEntryExists
                    all.RemoveAt(idx)
                    SaveAll(nodeId, all)
                    Return StatusCodes.Good
                Case Else
                    Return StatusCodes.BadHistoryOperationInvalid
            End Select
        End SyncLock
    End Function

    public Sub Delete(nodeId As NodeId, start As DateTime, [end] As DateTime) Implements IHistoryStore.Delete
        SyncLock m_lock
            Dim all = LoadAll(nodeId)
            all.RemoveAll(Function(e) e.Timestamp >= start AndAlso e.Timestamp <= [end])
            SaveAll(nodeId, all)
        End SyncLock
    End Sub

    Public Sub Remove(nodeId As NodeId) Implements IHistoryStore.Remove
        SyncLock m_lock
            Dim path = FilePath(nodeId)
            If File.Exists(path) Then File.Delete(path)
        End SyncLock
    End Sub

    Public Function DeleteAt(nodeId As NodeId, timestamps As IEnumerable(Of DateTime)) As IList(Of StatusCode) Implements IHistoryStore.DeleteAt
        Dim results As New List(Of StatusCode)
        SyncLock m_lock
            Dim all = LoadAll(nodeId)
            For Each ts In timestamps
                Dim idx = all.FindIndex(Function(e) e.Timestamp = ts)
                If idx >= 0 Then
                    all.RemoveAt(idx)
                    results.Add(StatusCodes.Good)
                Else
                    results.Add(StatusCodes.BadNoEntryExists)
                End If
            Next
            SaveAll(nodeId, all)
        End SyncLock
        Return results
    End Function

    Private Function FilePath(nodeId As NodeId) As String
        Return IO.Path.Combine(m_directory, nodeId.ToString().Replace(":", "_").Replace(";", "_") & ".csv")
    End Function

    Private Function LoadAll(nodeId As NodeId) As List(Of UaHistoryEntry)
        Dim path = FilePath(nodeId)
        If Not File.Exists(path) Then Return New List(Of UaHistoryEntry)
        Return File.ReadLines(path).Select(AddressOf ParseLine).Where(Function(e) e IsNot Nothing).ToList()
    End Function

    Private Sub SaveAll(nodeId As NodeId, entries As List(Of UaHistoryEntry))
        File.WriteAllLines(FilePath(nodeId), entries.Select(Function(e) $"{e.Timestamp:O},{e.Value},{e.StatusCode}"))
    End Sub

    Private Shared Function ParseLine(line As String) As UaHistoryEntry
        If String.IsNullOrWhiteSpace(line) Then Return Nothing
        Dim parts = line.Split(","c)
        If parts.Length < 3 Then Return Nothing
        Dim ts As DateTime
        If Not DateTime.TryParse(parts(0), Nothing, DateTimeStyles.RoundtripKind, ts) Then Return Nothing
        Return New UaHistoryEntry(ts, parts(1), StatusCodes.Good)
    End Function

End Class
