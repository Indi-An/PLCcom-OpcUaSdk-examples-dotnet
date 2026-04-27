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
' PLCcom OPC UA Client SDK - Workshop 42: Historical Data Update
'
' OPC UA Historical Access (Part 11) also allows clients to modify the
' history stored on the server. This is useful for:
'   * Correcting wrong values recorded by a sensor
'   * Back-filling missing data (e.g. after a server restart)
'   * Removing erroneous entries
'
' This workshop demonstrates all HistoryUpdate operations:
'   Insert       - add a new value (fails if timestamp already exists)
'   Update       - insert or replace (upsert)
'   Replace      - replace an existing value (fails if not exists)
'   Remove       - remove a value by timestamp
'   DeleteRaw    - delete all values in a time range
'   DeleteModified - delete modified values in a time range
'   DeleteAtTime - delete values at specific timestamps
'
' For read operations see Workshop 41 (Historical Data Read).
'
' Required server: Server Workshop 32 (Historical Update)
' opc.tcp://localhost:48410
' ==============================================================================

Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Client
Imports PLCcom.Opc.Ua.Client.Sdk
Imports System
Imports System.Collections.Generic
Imports System.Reflection

Public Class Program

    Public Shared Sub Main(ByVal args As String())
        Dim program As New Program()
        program.Start()
    End Sub

    Private Sub Start()
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 42: Historical Update   ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  OPC UA allows clients to modify history stored on the      ║")
        Console.WriteLine("║  server - useful for correcting values, back-filling         ║")
        Console.WriteLine("║  missing data or removing erroneous entries.                ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  What you will learn:                                        ║")
        Console.WriteLine("║    * Insert: add a new value at a specific timestamp         ║")
        Console.WriteLine("║    * Update: insert or replace (upsert)                     ║")
        Console.WriteLine("║    * Replace: replace an existing value                     ║")
        Console.WriteLine("║    * Remove: remove a value by timestamp                    ║")
        Console.WriteLine("║    * DeleteRaw: delete all values in a time range           ║")
        Console.WriteLine("║    * DeleteModified: delete modified values in a range      ║")
        Console.WriteLine("║    * DeleteAtTime: delete values at specific timestamps     ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  For read operations see Workshop 41 (Historical Read)      ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Required server: Server Workshop 32 (Historical Update)    ║")
        Console.WriteLine("║  opc.tcp://localhost:48410                                   ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Dim client As UaClient = Nothing
        Try
            'TODO
            'Submit your license information from your license e-mail
            Dim LicenseUserName As String = "<Enter your UserName here>"
            Dim LicenseSerial As String = "<Enter your Serial here>"

            Dim endpoints As EndpointDescriptionCollection = UaClient.GetEndpoints(
                New Uri("opc.tcp://localhost:48410"),
                certificateValidator:=AddressOf CertificateValidationHandler)
            endpoints = UaClient.SortEndpointsBySecurityLevel(endpoints)

            If endpoints.Count = 0 Then
                Console.WriteLine("  No endpoints found. Is Server Workshop 32 running?")
                Console.ReadLine()
                Return
            End If

            Console.WriteLine($"  {endpoints.Count} endpoint(s) found:")
            For i As Integer = 0 To endpoints.Count - 1
                Console.WriteLine($"  [{i}] {endpoints(i).ToDisplayString()}")
            Next

            Console.WriteLine()
            Console.Write("  Please enter index of desired endpoint: ")
            Dim idx As Integer
            If Not Integer.TryParse(Console.ReadLine(), idx) OrElse idx < 0 OrElse idx >= endpoints.Count Then
                Console.WriteLine("  Invalid selection.")
                Console.ReadLine()
                Return
            End If

            Dim sessionConfig As SessionConfiguration = SessionConfiguration.Build(
                Assembly.GetEntryAssembly().GetName().Name, endpoints(idx))
            sessionConfig.AutoConnect = False

            client = New UaClient(LicenseUserName, LicenseSerial, sessionConfig)
            AddHandler client.CertificateValidation, AddressOf CertificateValidationHandler
            AddHandler client.ServerConnected, Sub(s, e) Console.WriteLine($"  {DateTime.Now:T} Connected")
            AddHandler client.ServerConnectionLost, Sub(s, e) Console.WriteLine($"  {DateTime.Now:T} Connection lost")

            Console.Write("  Connecting ... ")
            client.Connect()
            Console.WriteLine("OK")
            Console.WriteLine()

            ' -- Resolve NodeId by browse path ----------------------------------
            ' Server 32 creates: Plant -> Sensor -> Temperature
            Dim nodeId As NodeId = client.GetNodeIdByPath("Objects.Plant.Sensor.Temperature")
            If nodeId Is Nothing Then
                Console.WriteLine("  Could not find 'Objects.Plant.Sensor.Temperature'.")
                Console.WriteLine("  Is Server Workshop 32 running?")
                Console.ReadLine()
                Return
            End If
            Console.WriteLine($"  Temperature NodeId: {nodeId}")
            Console.WriteLine()

            ' -- Command loop --------------------------------------------------
            While True
                Console.WriteLine("  Select operation:")
                Console.WriteLine("  1 - Insert        (add new value, fails if timestamp exists)")
                Console.WriteLine("  2 - Update        (insert or replace - upsert)")
                Console.WriteLine("  3 - Replace       (replace existing, fails if not exists)")
                Console.WriteLine("  4 - Remove        (remove value at current timestamp)")
                Console.WriteLine("  5 - DeleteRaw     (delete all values in last 2 minutes)")
                Console.WriteLine("  6 - DeleteModified(delete modified values in last 2 minutes)")
                Console.WriteLine("  7 - DeleteAtTime  (delete values at 5 specific timestamps)")
                Console.WriteLine("  8 - ReadRaw       (verify: read back last 10 minutes)")
                Console.WriteLine("  9 - Exit")
                Console.Write("  > ")

                Dim input As String = Console.ReadLine()
                If String.IsNullOrEmpty(input) OrElse input = "9" Then Exit While

                Try
                    Select Case input
                        Case "1" ' Insert - add a new value, fails if timestamp already exists
                            Console.Write("  Value to insert: ")
                            Dim dv As New DataValue() With {
                                .SourceTimestamp = DateTime.UtcNow,
                                .ServerTimestamp = DateTime.UtcNow,
                                .StatusCode = New StatusCode(StatusCodes.GoodEntryInserted),
                                .Value = Double.Parse(Console.ReadLine())
                            }
                            Dim result = client.Insert(nodeId, New List(Of DataValue) From {dv})
                            Console.WriteLine("  Result: " & result(0).OperationResults(0).ToString())

                        Case "2" ' Update - insert or replace (upsert)
                            Console.Write("  Value to update: ")
                            Dim dv As New DataValue() With {
                                .SourceTimestamp = DateTime.UtcNow,
                                .ServerTimestamp = DateTime.UtcNow,
                                .StatusCode = New StatusCode(StatusCodes.GoodEntryInserted),
                                .Value = Double.Parse(Console.ReadLine())
                            }
                            Dim result = client.Update(nodeId, New List(Of DataValue) From {dv})
                            Console.WriteLine("  Result: " & result(0).OperationResults(0).ToString())

                        Case "3" ' Replace - replace existing value, fails if not exists
                            Console.Write("  Value to replace: ")
                            Dim dv As New DataValue() With {
                                .SourceTimestamp = DateTime.UtcNow,
                                .ServerTimestamp = DateTime.UtcNow,
                                .StatusCode = New StatusCode(StatusCodes.GoodEntryInserted),
                                .Value = Double.Parse(Console.ReadLine())
                            }
                            Dim result = client.Replace(nodeId, New List(Of DataValue) From {dv})
                            Console.WriteLine("  Result: " & result(0).OperationResults(0).ToString())

                        Case "4" ' Remove - remove value at current timestamp
                            Dim dv As New DataValue() With {
                                .SourceTimestamp = DateTime.UtcNow,
                                .ServerTimestamp = DateTime.UtcNow
                            }
                            Dim result = client.Remove(nodeId, New List(Of DataValue) From {dv})
                            Console.WriteLine("  Result: " & result(0).OperationResults(0).ToString())

                        Case "5" ' DeleteRaw - delete all values in a time range
                            ' isModified=False: delete original recorded values
                            Dim results = client.DeleteRaw(nodeId,
                                Date.Now.AddMinutes(-2), Date.Now, isModified:=False)
                            For Each r As HistoryUpdateResult In results
                                Console.WriteLine("  Result: " & r.StatusCode.ToString())
                            Next

                        Case "6" ' DeleteModified - delete modified values in a time range
                            ' isModified=True: delete only values modified after original recording
                            Dim results = client.DeleteRaw(nodeId,
                                Date.Now.AddMinutes(-2), Date.Now, isModified:=True)
                            For Each r As HistoryUpdateResult In results
                                Console.WriteLine("  Result: " & r.StatusCode.ToString())
                            Next

                        Case "7" ' DeleteAtTime - delete values at 5 specific timestamps, 30s apart
                            Dim results = client.DeleteAtTime(nodeId,
                                Date.Now.AddMinutes(-2), numValuesPerNode:=5, timeStep:=30000)
                            For Each r As HistoryUpdateResult In results
                                Console.WriteLine("  Result: " & r.StatusCode.ToString())
                            Next

                        Case "8" ' ReadRaw - verify changes by reading back
                            Dim values As HistoryData = client.ReadRaw(nodeId,
                                Date.Now.AddMinutes(-10), Date.Now, isReadModified:=False)
                            PrintValues(values)
                    End Select
                Catch ex As Exception
                    Console.WriteLine("  Error: " & ex.Message)
                End Try

                Console.WriteLine()
            End While

            client.Disconnect()

        Catch ex As Exception
            Console.WriteLine("  Error: " & ex.Message)
            Console.WriteLine()
            Console.WriteLine("  Press ENTER to exit.")
            Console.ReadLine()
        End Try
    End Sub

    Private Shared Sub PrintValues(data As HistoryData)
        If data?.DataValues Is Nothing OrElse data.DataValues.Count = 0 Then
            Console.WriteLine("  (no values)")
            Return
        End If
        For Each v As DataValue In data.DataValues
            Console.WriteLine($"  {v.SourceTimestamp.ToLocalTime():T}  " &
                              $"Value={v.Value,-10}  {v.StatusCode}")
        Next
        Console.WriteLine($"  => {data.DataValues.Count} values")
    End Sub

    Private Sub CertificateValidationHandler(ByVal sender As CertificateValidator,
                                             ByVal e As CertificateValidationEventArgs)
        e.Accept = True
    End Sub

End Class
