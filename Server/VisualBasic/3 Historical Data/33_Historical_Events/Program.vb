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
Imports System.Threading

Module Program

    Sub Main(args As String())

        Dim LicenseUserName As String = "<Enter your UserName here>"
        Dim LicenseSerial As String = "<Enter your Serial here>"

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 33: Historical Events   ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This example demonstrates:                                  ║")
        Console.WriteLine("║  * Enabling event history on source nodes                    ║")
        Console.WriteLine("║  * Recording events in the history store                     ║")
        Console.WriteLine("║  * Clients can query past events via HistoryRead             ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Dim config As New UaServerConfiguration With {
            .ApplicationName = "PLCcom Workshop 33 - Historical Events",
            .ApplicationUri = "urn:localhost:PLCcom:Workshop:33",
            .ProductUri = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
            .BaseAddresses = New List(Of String) From {
                "opc.tcp://localhost:48410",
                "opc.https://localhost:48411"
            },
            .SecurityPolicies = UaServer.GetRecommendedSecurityPolicies(),
            .UserTokenPolicies = New List(Of UserTokenPolicy) From {
                New UserTokenPolicy With {.TokenType = UserTokenType.Anonymous}
            },
            .ManufacturerName = "My Company GmbH",
            .ProductName = "My OPC UA Server",
            .SoftwareVersion = "1.0.0",
            .BuildNumber = "42",
            .NamespaceUri = "http://indi-an.com/opcua/workshop/historical-events",
            .CertificateStorePath = ".\pki"
        }

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

            Dim plant = server.CreateFolder("Plant")
            Dim reactor = server.CreateFolder(plant, "Reactor")

            Dim temperature = server.CreateVariable(Of Double)(reactor, "Temperature", initialValue:=25.0)
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

End Module
