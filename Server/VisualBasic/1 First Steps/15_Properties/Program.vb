Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Server.Sdk
Imports System
Imports System.Collections.Generic
Imports System.Threading

' ==============================================================================
' PLCcom OPC UA Server SDK - Workshop 15: Properties
'
' OPC UA variables can have Properties - child nodes that describe the variable.
' The most important standard properties are:
'   EURange (Engineering Unit Range): defines the physical min/max.
'   EngineeringUnits: the unit label displayed next to the value.
'   StatusCode: every OPC UA variable has a quality stamp (Good, Uncertain, Bad).
'
' What you will learn:
'   * How to add EURange and EngineeringUnits to variables
'   * How to validate writes against the EURange
'   * How to set and change StatusCodes
'   * How to use UpdateValue for atomic quality updates
'
' Connect with any OPC UA client to: opc.tcp://localhost:48414
' ==============================================================================

Module Program

    Sub Main(args As String())

        ' -- License -----------------------------------------------------------
        ' TODO: Submit your license information from your license e-mail
        Dim LicenseUserName As String = "<Enter your UserName here>"
        Dim LicenseSerial As String = "<Enter your Serial here>"

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 15: Properties          ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This example demonstrates:                                  ║")
        Console.WriteLine("║  * EURange - min/max limits for gauges and bar graphs        ║")
        Console.WriteLine("║  * EngineeringUnits - unit labels (C, bar, rpm)              ║")
        Console.WriteLine("║  * StatusCode - per-variable quality reporting               ║")
        Console.WriteLine("║  * UpdateValue - atomic value + status + timestamp update    ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Dim config As New UaServerConfiguration With {
            .ApplicationName = "PLCcom Workshop 15 - Properties",
            .ApplicationUri = "urn:localhost:PLCcom:Workshop:15",
            .ProductUri = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
            .BaseAddresses = New List(Of String) From {"opc.tcp://localhost:48414"},
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
            Dim machine = server.CreateFolder(plant, "Machine1")

            Dim temperature = server.CreateVariable(Of Double)(machine, "Temperature", initialValue:=22.0)
            Dim pressure = server.CreateVariable(Of Double)(machine, "Pressure", initialValue:=1.0)
            Dim rpm = server.CreateVariable(Of Integer)(machine, "RPM", initialValue:=1500)

            ' -- EURange: defines the physical measurement range ---------------
            ' HMI clients use this to scale gauges and bar graphs automatically.
            temperature.SetEURange(-40.0, 120.0)
            pressure.SetEURange(0.0, 10.0)
            rpm.SetEURange(0, 3000)

            ' -- EngineeringUnits: the unit label shown in HMI clients ---------
            temperature.SetEngineeringUnits("C", "degree Celsius")
            pressure.SetEngineeringUnits("bar")
            rpm.SetEngineeringUnits("rpm", "revolutions per minute")

            ' -- OnWrite: validate writes against the EURange ------------------
            ' EURange is informational only - the server does NOT automatically
            ' reject out-of-range writes. Use OnWrite to enforce the range.
            ' Return False to reject the write (client receives BadOutOfRange).
            temperature.OnWrite = Function(value)
                                      If CDbl(value) < -40.0 OrElse CDbl(value) > 120.0 Then
                                          Console.WriteLine($"{vbLf}  [REJECTED] Temperature={value} is outside EURange [-40..120]")
                                          Return False
                                      End If
                                      Console.WriteLine($"{vbLf}  [ACCEPTED] Temperature={value}")
                                      Return True
                                  End Function

            pressure.OnWrite = Function(value)
                                   If CDbl(value) < 0.0 OrElse CDbl(value) > 10.0 Then
                                       Console.WriteLine($"{vbLf}  [REJECTED] Pressure={value} is outside EURange [0..10]")
                                       Return False
                                   End If
                                   Console.WriteLine($"{vbLf}  [ACCEPTED] Pressure={value}")
                                   Return True
                               End Function

            Console.WriteLine("  Variables with properties:")
            Console.WriteLine("    Temperature: EURange [-40..120], Unit: C    (write validated)")
            Console.WriteLine("    Pressure:    EURange [0..10],    Unit: bar  (write validated)")
            Console.WriteLine("    RPM:         EURange [0..3000],  Unit: rpm")
            Console.WriteLine()

            ' -- StatusCode: set initial quality -------------------------------
            temperature.StatusCode = StatusCodes.Good
            pressure.StatusCode = StatusCodes.UncertainSensorNotAccurate

            Console.WriteLine("  StatusCodes:")
            Console.WriteLine($"    Temperature: {temperature.StatusCode} (Good)")
            Console.WriteLine($"    Pressure:    {pressure.StatusCode} (UncertainSensorNotAccurate)")
            Console.WriteLine()

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║")
            Console.WriteLine("║  opc.tcp://localhost:48414                                   ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Try:                                                        ║")
            Console.WriteLine("║  * Browse Temperature -> expand to see EURange and           ║")
            Console.WriteLine("║    EngineeringUnits as child properties                      ║")
            Console.WriteLine("║  * Write 122 to Temperature -> rejected (out of range)       ║")
            Console.WriteLine("║  * Write 50 to Temperature -> accepted                       ║")
            Console.WriteLine("║  * Check the quality indicator of Pressure (Uncertain)       ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Press ENTER to start simulation with StatusCode changes.    ║")
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
            Console.ReadLine()

            ' -- Simulation: demonstrate StatusCode changes --------------------
            ' UpdateValue() sets value + StatusCode + timestamp atomically.
            Console.WriteLine("Simulating... sensor failure every 20 cycles. (CTRL+C to exit)")
            Dim rng As New Random()
            Dim cycle As Long = 0

            While True
                cycle += 1
                temperature.Value = Math.Round(20.0 + rng.NextDouble() * 10.0, 1)

                If cycle Mod 20 = 0 Then
                    pressure.UpdateValue(0.0, StatusCodes.BadSensorFailure, DateTime.UtcNow)
                Else
                    pressure.UpdateValue(Math.Round(0.9 + rng.NextDouble() * 0.3, 3),
                        StatusCodes.Good, DateTime.UtcNow)
                End If

                rpm.Value = 1400 + rng.Next(200)
                Console.Write($"{Chr(13)}  Cycle={cycle}  T={temperature.Value:F1}C  " &
                    $"P={pressure.Value:F3}bar [{If(cycle Mod 20 = 0, "FAIL", "OK  ")}]  RPM={rpm.Value}  ")
                Thread.Sleep(1000)
            End While

        End Using

    End Sub

End Module
