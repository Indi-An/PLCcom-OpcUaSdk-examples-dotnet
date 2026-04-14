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
' PLCcom OPC UA Server SDK - Workshop 22: Alarm Conditions
'
' OPC UA Alarms & Conditions (Part 9) extends the event model with stateful
' alarms that clients can acknowledge and confirm.
'
' An alarm has a lifecycle:
'   1. Inactive: process value is within normal range
'   2. Active + Unacknowledged: limit exceeded, operator must acknowledge
'   3. Active + Acknowledged: operator has seen the alarm
'   4. Inactive + Unacknowledged: condition cleared but not yet acknowledged
'   5. Inactive + Acknowledged: alarm fully resolved
'
' What you will learn:
'   * How to create alarms on a source node
'   * How to activate and deactivate alarms based on process values
'   * How to set alarm severity
'   * How clients acknowledge alarms in the Alarm & Conditions view
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
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 22: Alarm Conditions    ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This example demonstrates:                                  ║")
        Console.WriteLine("║  * Creating alarms on source nodes                           ║")
        Console.WriteLine("║  * Activating/deactivating alarms based on process values    ║")
        Console.WriteLine("║  * Alarm severity levels                                     ║")
        Console.WriteLine("║  * Clients can acknowledge alarms                            ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Dim config As New UaServerConfiguration With {
            .ApplicationName = "PLCcom Workshop 22 - Alarm Conditions",
            .ApplicationUri = "urn:localhost:PLCcom:Workshop:22",
            .ProductUri = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
            .BaseAddresses = New List(Of String) From {
                "opc.tcp://localhost:48410",
                "opc.https://localhost:48411"
            },
            .SecurityPolicies = UaServer.GetRecommendedSecurityPolicies(),
            .UserTokenPolicies = New List(Of UserTokenPolicy) From {
                New UserTokenPolicy With {.TokenType = UserTokenType.Anonymous}
            },
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

            ' EnableEvents() is required on the source node before creating alarms.
            server.EnableEvents(reactor)

            Dim temperature = server.CreateVariable(Of Double)(reactor, "Temperature", initialValue:=25.0)
            Dim pressure = server.CreateVariable(Of Double)(reactor, "Pressure", initialValue:=1.0)

            temperature.SetEURange(0, 200)
            temperature.SetEngineeringUnits("C")
            pressure.SetEURange(0, 10)
            pressure.SetEngineeringUnits("bar")

            ' -- Create alarms on the source node ------------------------------
            ' CreateAlarm() creates an AlarmConditionState node under the source node.
            ' The alarm is initially inactive and enabled.
            Dim tempAlarm = server.CreateAlarm(reactor, "TemperatureHighAlarm")
            Dim pressAlarm = server.CreateAlarm(reactor, "PressureHighAlarm")

            Console.WriteLine("  Reactor:")
            Console.WriteLine("    Temperature (0-200 C) with HighAlarm at > 80C")
            Console.WriteLine("    Pressure (0-10 bar) with HighAlarm at > 5 bar")
            Console.WriteLine()

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║")
            Console.WriteLine("║  opc.tcp://localhost:48410                                   ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  To see alarms:                                              ║")
            Console.WriteLine("║  1. Open Document -> Add -> Event View                       ║")
            Console.WriteLine("║  2. Click '+' and select Objects -> Server                   ║")
            Console.WriteLine("║  3. Press ENTER here to start the simulation                 ║")
            Console.WriteLine("║  4. When an alarm appears, right-click -> Acknowledge        ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Press ENTER to start simulation.                            ║")
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
            Console.ReadLine()

            Console.WriteLine("Simulating... (CTRL+C to exit)")
            Console.WriteLine("  Temperature alarm: > 80C ON, < 70C OFF")
            Console.WriteLine("  Pressure alarm:    > 5 bar ON, < 4 bar OFF")
            Console.WriteLine()

            Dim rng As New Random()
            Dim tempActive As Boolean = False
            Dim pressActive As Boolean = False

            While True
                ' Simulate oscillating process values
                Dim t As Double = 50.0 + Math.Sin(DateTime.UtcNow.Ticks * 0.0000001) * 40.0 + rng.NextDouble() * 5.0
                Dim p As Double = 1.0 + (t - 50.0) / 30.0 + rng.NextDouble() * 0.5
                temperature.Value = Math.Round(t, 1)
                pressure.Value = Math.Round(p, 2)

                ' -- Temperature alarm logic with hysteresis -------------------
                ' Hysteresis (ON at 80, OFF at 70) prevents rapid toggling near the limit.
                If t > 80.0 AndAlso Not tempActive Then
                    ' Activate() sets the alarm to Active + Unacknowledged and fires an event.
                    tempAlarm.Activate($"Temperature HIGH: {t:F1}C", EventSeverity.High)
                    tempActive = True
                    Console.WriteLine($"{vbLf}  ALARM ON:  Temperature = {t:F1}C")
                ElseIf t < 70.0 AndAlso tempActive Then
                    ' Deactivate() sets the alarm to Inactive and fires a return-to-normal event.
                    tempAlarm.Deactivate($"Temperature normal: {t:F1}C")
                    tempActive = False
                    Console.WriteLine($"{vbLf}  ALARM OFF: Temperature = {t:F1}C")
                End If

                ' -- Pressure alarm logic --------------------------------------
                If p > 5.0 AndAlso Not pressActive Then
                    pressAlarm.Activate($"Pressure HIGH: {p:F2} bar", EventSeverity.MediumHigh)
                    pressActive = True
                    Console.WriteLine($"{vbLf}  ALARM ON:  Pressure = {p:F2} bar")
                ElseIf p < 4.0 AndAlso pressActive Then
                    pressAlarm.Deactivate($"Pressure normal: {p:F2} bar")
                    pressActive = False
                    Console.WriteLine($"{vbLf}  ALARM OFF: Pressure = {p:F2} bar")
                End If

                Console.Write($"{vbCr}  T={temperature.Value:F1}C{If(tempActive, " !", "  ")}  " &
                              $"P={pressure.Value:F2}bar{If(pressActive, " !", "  ")}  ")
                Thread.Sleep(1000)
            End While

        End Using

    End Sub

End Module
