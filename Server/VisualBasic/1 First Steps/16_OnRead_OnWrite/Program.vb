Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Server.Sdk
Imports System
Imports System.Collections.Generic

' ==============================================================================
' PLCcom OPC UA Server SDK - Workshop 16: OnRead / OnWrite Callbacks
'
' By default, OPC UA variables cache their value in memory.
' Callbacks let you intercept reads and writes to add custom logic:
'   OnRead:  Called every time a client reads the variable.
'   OnWrite: Called before a client write is accepted.
'            Return True to accept, False to reject (BadOutOfRange).
'
' What you will learn:
'   * How to use OnRead to deliver a live value on every read
'   * How to use OnWrite to validate and accept/reject client writes
'   * How to use OnWrite to log all changes
'
' Connect with any OPC UA client to: opc.tcp://localhost:48415
' ==============================================================================

Module Program

    Sub Main(args As String())

        ' -- License -----------------------------------------------------------
        ' TODO: Submit your license information from your license e-mail
        Dim LicenseUserName As String = "<Enter your UserName here>"
        Dim LicenseSerial As String = "<Enter your Serial here>"

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 16: OnRead / OnWrite    ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This example demonstrates:                                  ║")
        Console.WriteLine("║  * OnRead - fresh value on every client read                 ║")
        Console.WriteLine("║  * OnWrite - validate and accept/reject client writes        ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Dim config As New UaServerConfiguration With {
            .ApplicationName = "PLCcom Workshop 16 - OnRead/OnWrite",
            .ApplicationUri = "urn:localhost:PLCcom:Workshop:16",
            .ProductUri = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
            .BaseAddresses = New List(Of String) From {"opc.tcp://localhost:48415"},
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
            Dim rng As New Random()

            ' -- OnRead: Deliver a fresh value on every read -------------------
            ' Without OnRead, the server returns the cached value.
            ' With OnRead, the lambda is called on every Read or Subscription sample.
            ' This is ideal for variables that map directly to hardware registers.
            Dim cpuLoad = server.CreateVariable(Of Double)(machine, "CpuLoad",
                initialValue:=0.0, readOnly:=True)

            cpuLoad.OnRead = Function(currentValue)
                                 Dim value As Double = Math.Round(rng.NextDouble() * 100.0, 1)
                                 Console.WriteLine($"  [OnRead] CpuLoad -> {value}%")
                                 Return value
                             End Function

            ' -- OnWrite: Validate before accepting ----------------------------
            ' Return True to accept (value is stored and clients are notified).
            ' Return False to reject (client receives BadOutOfRange status code).
            Dim targetTemp = server.CreateVariable(Of Double)(machine, "TargetTemperature",
                initialValue:=22.0)

            targetTemp.OnWrite = Function(newValue)
                                     Dim accepted As Boolean = CDbl(newValue) >= 10.0 AndAlso CDbl(newValue) <= 50.0
                                     If accepted Then
                                         Console.WriteLine($"  [OnWrite] TargetTemperature = {newValue:F1} -> ACCEPTED")
                                     Else
                                         Console.WriteLine($"  [OnWrite] TargetTemperature = {newValue:F1} -> REJECTED (must be 10..50)")
                                     End If
                                     Return accepted
                                 End Function

            ' -- OnWrite: Log all changes --------------------------------------
            ' You can also use OnWrite just for side effects (logging, forwarding to PLC)
            ' while always returning True to accept the write.
            Dim speed = server.CreateVariable(Of Integer)(machine, "SpeedSetpoint", initialValue:=1000)

            speed.OnWrite = Function(newValue)
                                Console.WriteLine($"  [OnWrite] SpeedSetpoint changed: {speed.Value} -> {newValue}")
                                Return True
                            End Function

            Console.WriteLine("  Variables:")
            Console.WriteLine("    CpuLoad           [ReadOnly, OnRead]  -> random 0-100 on every read")
            Console.WriteLine("    TargetTemperature [OnWrite]           -> accepts 10.0 .. 50.0 only")
            Console.WriteLine("    SpeedSetpoint     [OnWrite]           -> logs all changes")
            Console.WriteLine()

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║")
            Console.WriteLine("║  opc.tcp://localhost:48415                                   ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Try:                                                        ║")
            Console.WriteLine("║  * Read CpuLoad multiple times - value changes each time     ║")
            Console.WriteLine("║  * Subscribe to CpuLoad - new value on every sample          ║")
            Console.WriteLine("║  * Write 25.0 to TargetTemperature -> accepted               ║")
            Console.WriteLine("║  * Write 99.0 to TargetTemperature -> rejected (BadRange)    ║")
            Console.WriteLine("║  * Write any value to SpeedSetpoint -> logged in console     ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Press ENTER to exit.                                        ║")
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
            Console.ReadLine()

        End Using

    End Sub

End Module
