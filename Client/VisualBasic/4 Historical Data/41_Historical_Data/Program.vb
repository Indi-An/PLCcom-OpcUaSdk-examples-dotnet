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
' PLCcom OPC UA Client SDK - Workshop 41: Historical Data Read
'
' OPC UA Historical Access (Part 11) lets clients read past values of variables
' using the HistoryRead service. The server must have history enabled on the
' variable (Historizing = true) - see Server Workshop 31.
'
' This workshop demonstrates all HistoryRead operations:
'   Subscribe    - monitor live values via subscription
'   ReadRaw      - read recorded values as-is
'   ReadModified - read values that were changed after recording
'   ReadAtTime   - read values at specific evenly-spaced timestamps
'   ReadProcessed- read aggregated values (Average, Min, Max, ...)
'
' For history write operations (Insert, Update, Replace, Delete)
' see Workshop 42 (Historical Data Update).
'
' Required server: Server Workshop 31 (Historical Access)
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
        Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 41: Historical Data     ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  OPC UA Historical Access lets you read past values.         ║")
        Console.WriteLine("║  The server stores timestamped values and returns them       ║")
        Console.WriteLine("║  on request - essential for trend analysis and reporting.    ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  What you will learn:                                        ║")
        Console.WriteLine("║    * Subscribe to live data changes                          ║")
        Console.WriteLine("║    * ReadRaw: read recorded values as-is                     ║")
        Console.WriteLine("║    * ReadModified: values changed after recording            ║")
        Console.WriteLine("║    * ReadAtTime: values at specific timestamps               ║")
        Console.WriteLine("║    * ReadProcessed: aggregated values (Average, Min, Max)    ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  For write operations see Workshop 42 (Historical Update)    ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Required server: Server Workshop 31 (Historical Access)     ║")
        Console.WriteLine("║  opc.tcp://localhost:48410                                   ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Dim client As UaClient = Nothing
        Try
            'TODO
            'Submit your license information from your license e-mail
            ' Important !!!!!!!!!!!!!!!!!!
            ' Enter your Username + Serial here! Please note: with blank fields the library runs
            ' for 15 minutes during a debug session. Both values can also come
            ' from configuration or an environment variable.
            ' Free trial license (14 days, uninterrupted): https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-download/
            Dim LicenseUserName As String = ""
            Dim LicenseSerial As String = ""

            Dim endpoints As EndpointDescriptionCollection = UaClient.GetEndpoints(
                New Uri("opc.tcp://localhost:48410"),
                certificateValidator:=AddressOf CertificateValidationHandler)
            endpoints = UaClient.SortEndpointsBySecurityLevel(endpoints)

            If endpoints.Count = 0 Then
                Console.WriteLine("  No endpoints found. Is Server Workshop 31 running?")
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

            Dim sessionConfig As SessionConfiguration = CreateConfig(endpoints(idx))


            PrintConfig(sessionConfig)

            client = New UaClient(LicenseUserName, LicenseSerial, sessionConfig)
            AddHandler client.CertificateValidation, AddressOf CertificateValidationHandler
            AddHandler client.ServerConnected, Sub(s, e) Console.WriteLine($"  {DateTime.Now:T} Connected")
            AddHandler client.ServerConnectionLost, Sub(s, e) Console.WriteLine($"  {DateTime.Now:T} Connection lost")

            Console.Write("  Connecting ... ")
            client.Connect()
            Console.WriteLine("OK")
            Console.WriteLine()

            ' -- Resolve NodeId by browse path ----------------------------------
            ' Server 31 creates: Plant -> Sensor -> Temperature
            Dim nodeId As NodeId = client.GetNodeIdByPath("Objects.Plant.Sensor.Temperature")
            If nodeId Is Nothing Then
                Console.WriteLine("  Could not find 'Objects.Plant.Sensor.Temperature'.")
                Console.WriteLine("  Is Server Workshop 31 running and recording history?")
                Console.ReadLine()
                Return
            End If
            Console.WriteLine($"  Temperature NodeId: {nodeId}")
            Console.WriteLine()

            ' -- Command loop --------------------------------------------------
            Dim subscription As Subscription = Nothing

            While True
                Console.WriteLine("  Select operation:")
                Console.WriteLine("  1 - Subscribe    (live data changes via subscription)")
                Console.WriteLine("  2 - ReadRaw      (all recorded values as stored)")
                Console.WriteLine("  3 - ReadModified (values changed after recording)")
                Console.WriteLine("  4 - ReadAtTime   (values at evenly-spaced timestamps)")
                Console.WriteLine("  5 - ReadProcessed(aggregated values: Average, Min, Max)")
                Console.WriteLine("  6 - Exit")
                Console.Write("  > ")

                Dim input As String = Console.ReadLine()
                If String.IsNullOrEmpty(input) OrElse input = "6" Then Exit While

                Try
                    Select Case input
                        Case "1" ' Subscribe - monitor live values
                            If subscription Is Nothing Then
                                subscription = New Subscription() With {
                                    .PublishingInterval = 1000,
                                    .PublishingEnabled = True
                                }
                                AddHandler subscription.StateChanged,
                                    Sub(s2, e2) Console.WriteLine($"  Subscription state: {e2.Status}")
                                client.AddSubscription(subscription)
                            End If

                            Dim item As New MonitoredItem(DirectCast(Nothing, ITelemetryContext)) With {
                                .StartNodeId = nodeId,
                                .AttributeId = Attributes.Value,
                                .MonitoringMode = MonitoringMode.Reporting,
                                .SamplingInterval = 500,
                                .QueueSize = UInteger.MaxValue,
                                .DiscardOldest = True,
                                .DisplayName = "Temperature"
                            }
                            AddHandler item.Notification, Sub(mi, e)
                                Dim n = TryCast(e.NotificationValue, MonitoredItemNotification)
                                Console.WriteLine($"  {n.Value.SourceTimestamp.ToLocalTime():T}  " &
                                                  $"T={n.Value.Value}  {n.Value.StatusCode}")
                            End Sub
                            subscription.AddItem(item)
                            subscription.ApplyChanges()
                            Console.WriteLine("  Monitoring... press ENTER to stop.")
                            Console.ReadLine()

                        Case "2" ' ReadRaw - all recorded values as stored
                            ' isReadModified=False: return original recorded values
                            Dim values As HistoryData = client.ReadRaw(nodeId,
                                Date.Now.AddMinutes(-10), Date.Now, isReadModified:=False)
                            PrintValues(values)

                        Case "3" ' ReadModified - only values changed after recording
                            ' isReadModified=True: return only values that were modified
                            Dim values As HistoryData = client.ReadRaw(nodeId,
                                Date.Now.AddMinutes(-10), Date.Now, isReadModified:=True)
                            PrintValues(values)

                        Case "4" ' ReadAtTime - values at 10 evenly-spaced timestamps, 30s apart
                            ' Returns the value closest to each requested timestamp.
                            Dim values As HistoryData = client.ReadAtTime(nodeId,
                                Date.Now.AddMinutes(-5), numValuesPerNode:=10,
                                timeStep:=30000, useSimpleBounds:=False)
                            PrintValues(values)

                        Case "5" ' ReadProcessed - server computes aggregate per interval
                            Dim aggregates As Dictionary(Of String, NodeId) = client.GetAvailableAggregates()
                            Console.WriteLine("  Available aggregates: " & String.Join(", ", aggregates.Keys))
                            Dim aggregateId As NodeId = If(aggregates.ContainsKey("Average"),
                                aggregates("Average"), aggregates("Interpolative"))
                            Dim values As HistoryData = client.ReadProcessed(nodeId, aggregateId,
                                Date.Now.AddMinutes(-5), Date.Now, processingInterval:=60000)
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


    ' =============================================================================
    ' Helper: CreateConfig
    ' =============================================================================
    ' Builds the SessionConfiguration for the selected endpoint.
    '
    ' Certificate handling:
    '   Application certificate -- required for Sign / SignAndEncrypt endpoints.
    '   HTTPS certificate       -- required for opc.https:// endpoints (any SecurityMode).
    '
    ' Load() returns Nothing if the certificate does not exist yet or cannot be read.
    ' Build(True) creates a new self-signed certificate, overwriting any existing file.
    Private Shared Function CreateConfig(ByVal endpoint As EndpointDescription) As SessionConfiguration
        Dim appAlias As String = System.Reflection.Assembly.GetEntryAssembly().GetName().Name
        Dim config As SessionConfiguration = SessionConfiguration.Build(appAlias, endpoint)
        config.AutoConnect = False

        ' HTTPS certificate -- required for opc.https:// endpoints, independent of SecurityMode.
        Dim httpsCert As UaClientCertificate = Nothing
        If endpoint.EndpointUrl IsNot Nothing AndAlso
           endpoint.EndpointUrl.StartsWith("opc.https://", StringComparison.OrdinalIgnoreCase) Then
            Dim host As String = New Uri(endpoint.EndpointUrl).Host
            httpsCert = UaClientCertificate.Load("./pki", host, "secretpassword")
            If httpsCert Is Nothing OrElse Not httpsCert.CheckValidity() Then
                httpsCert = New UaClientCertificate("./pki", "secretpassword", host, 720, "Indi.An GmbH") _
                    .Build(overwrite:=True)
            End If
        End If

        ' Application certificate -- required for secured endpoints (Sign or SignAndEncrypt).
        ' Not needed for SecurityMode.None (unencrypted connections).
        Dim appCert As UaClientCertificate = Nothing
        If Not endpoint.SecurityMode.Equals(MessageSecurityMode.None) Then
            appCert = UaClientCertificate.Load("./pki", appAlias, "secretpassword")
            If appCert Is Nothing OrElse Not appCert.CheckValidity() Then
                appCert = New UaClientCertificate("./pki", "secretpassword", appAlias, 720, "Indi.An GmbH") _
                    .Build(overwrite:=True)
            End If
        End If

        ' SetInstanceCertificate() sets CertificateStorePath and ApplicationCertificateFullPath.
        If appCert IsNot Nothing AndAlso httpsCert IsNot Nothing Then
            config.SetInstanceCertificate(appCert, httpsCert)
        ElseIf appCert IsNot Nothing Then
            config.SetInstanceCertificate(appCert)
        End If

        Return config
    End Function

    ' =============================================================================
    ' Helper: PrintConfig
    ' =============================================================================
    ' Prints the active client configuration to the console so you can verify
    ' all settings at a glance before connecting.
    Private Shared Sub PrintConfig(ByVal config As SessionConfiguration)
        Console.WriteLine("-- Active Client Configuration ------------------------------")
        If config.Endpoint IsNot Nothing Then
            Console.WriteLine("  Endpoint  : " & config.Endpoint.EndpointUrl)
            Console.WriteLine("  Security  : " & config.Endpoint.ToDisplayString())
        End If
        Console.WriteLine("  PKI Store : " & If(config.CertificateStorePath IsNot Nothing, config.CertificateStorePath, "(not set)"))
        Console.WriteLine("  Cert File : " & If(config.ApplicationCertificateFullPath IsNot Nothing, config.ApplicationCertificateFullPath, "(none -- SecurityMode.None)"))
        Console.WriteLine("-------------------------------------------------------------")
        Console.WriteLine()
    End Sub
End Class
