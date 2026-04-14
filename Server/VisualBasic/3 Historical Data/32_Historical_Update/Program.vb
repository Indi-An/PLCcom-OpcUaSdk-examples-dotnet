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
' PLCcom OPC UA Server SDK - Workshop 32: Historical Update
'
' Extends Workshop 31 to accept HistoryUpdate requests from clients:
' Insert, Update, Replace, Remove, DeleteRaw, DeleteAtTime.
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
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 32: Historical Update   ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This example demonstrates:                                  ║")
        Console.WriteLine("║  * History recording with read AND write access              ║")
        Console.WriteLine("║  * Clients can Insert, Update, Replace, Remove values        ║")
        Console.WriteLine("║  * Clients can DeleteRaw (by range) and DeleteAtTime         ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Dim config As New UaServerConfiguration With {
            .ApplicationName = "PLCcom Workshop 32 - Historical Update",
            .ApplicationUri = "urn:localhost:PLCcom:Workshop:32",
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
            .NamespaceUri = "http://indi-an.com/opcua/workshop/historical-update",
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
            Dim sensor = server.CreateFolder(plant, "Sensor")

            Dim temperature = server.CreateVariable(Of Double)(sensor, "Temperature", initialValue:=20.0)
            temperature.SetEURange(-40, 120)
            temperature.SetEngineeringUnits("C", "Degrees Celsius")

            Dim pressure = server.CreateVariable(Of Double)(sensor, "Pressure", initialValue:=1.0)
            pressure.SetEURange(0, 10)
            pressure.SetEngineeringUnits("bar", "Bar")

            server.EnableHistory(temperature, maxEntries:=500)
            server.EnableHistory(pressure, maxEntries:=500)

            Console.WriteLine("  Variables with history enabled (read + write):")
            Console.WriteLine("    Temperature: HistoryRead + HistoryWrite")
            Console.WriteLine("    Pressure:    HistoryRead + HistoryWrite")
            Console.WriteLine()

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running on: opc.tcp://localhost:48410             ║")
            Console.WriteLine("║  Use Client Workshop 41 to test all operations.              ║")
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
                Dim p As Double = 1.0 + Math.Cos(cycle * 0.08) * 0.5 + rng.NextDouble() * 0.2
                temperature.Value = Math.Round(t, 1)
                pressure.Value = Math.Round(p, 2)

                server.RecordHistoryValue(temperature, now)
                server.RecordHistoryValue(pressure, now)

                Dim hist = server.GetHistory(temperature.NodeId)
                Console.Write($"{vbCr}  Cycle={cycle}  T={temperature.Value:F1}C  " &
                              $"P={pressure.Value:F2}bar  History={hist.Count} entries  ")
                Thread.Sleep(1000)
            End While

        End Using

    End Sub

End Module
