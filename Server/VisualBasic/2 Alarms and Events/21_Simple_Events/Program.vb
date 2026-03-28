Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Server.Sdk
Imports System
Imports System.Collections.Generic
Imports System.Threading

' ==============================================================================
' PLCcom OPC UA Server SDK - Workshop 21: Simple Events
'
' OPC UA Events are notifications that something happened - not a value change,
' but a discrete occurrence like a state transition, a warning, or an action.
'
' Events have a severity level (1-1000):
'   Low (1-333):    informational, normal operation
'   Medium (334-666): warning, attention needed
'   High (667-1000): critical, immediate action required
'
' What you will learn:
'   * How to enable event notifications on a node
'   * How to fire events with different severity levels
'   * How clients subscribe to events in the Event View
'
' Connect with any OPC UA client to: opc.tcp://localhost:48420
' ==============================================================================

Module Program

    Sub Main(args As String())

        ' -- License -----------------------------------------------------------
        ' TODO: Submit your license information from your license e-mail
        Dim LicenseUserName As String = "<Enter your UserName here>"
        Dim LicenseSerial As String = "<Enter your Serial here>"

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 21: Simple Events       ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This example demonstrates:                                  ║")
        Console.WriteLine("║  * Enabling event notifications on nodes                     ║")
        Console.WriteLine("║  * Firing events with message and severity                   ║")
        Console.WriteLine("║  * Event severity levels (Low, Medium, High)                 ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Dim config As New UaServerConfiguration With {
            .ApplicationName = "PLCcom Workshop 21 - Simple Events",
            .ApplicationUri = "urn:localhost:PLCcom:Workshop:21",
            .ProductUri = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
            .BaseAddresses = New List(Of String) From {"opc.tcp://localhost:48420"},
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
            Dim temp = server.CreateVariable(Of Double)(machine, "Temperature", initialValue:=22.0)

            ' -- Enable events on the source node ------------------------------
            ' EnableEvents() sets the EventNotifier attribute on the node.
            ' Without this, clients cannot subscribe to events from this node.
            ' Events fired on a node propagate up to the Server node automatically.
            server.EnableEvents(machine)
            server.FireEvent(machine, "Machine1 started successfully", EventSeverity.Low)

            Console.WriteLine("  Machine1: Events enabled")
            Console.WriteLine("  Initial event fired: 'Machine1 started successfully'")
            Console.WriteLine()

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║")
            Console.WriteLine("║  opc.tcp://localhost:48420                                   ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  To see events in the client:                                ║")
            Console.WriteLine("║  1. Open Document -> Add -> Event View                       ║")
            Console.WriteLine("║  2. In the Event View, click the '+' button and select       ║")
            Console.WriteLine("║     Objects -> Server (to receive all events)                ║")
            Console.WriteLine("║  3. Press ENTER here to start firing events                  ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Press ENTER to start firing events every 5 seconds.         ║")
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
            Console.ReadLine()

            Console.WriteLine("Firing events every 5 seconds... (CTRL+C to exit)")
            Console.WriteLine("  Temperature > 30 -> High severity event")
            Console.WriteLine("  Temperature > 25 -> Medium severity event")
            Console.WriteLine("  Temperature <= 25 -> Low severity event")
            Console.WriteLine()

            Dim rng As New Random()

            While True
                Dim t As Double = 20.0 + rng.NextDouble() * 15.0
                temp.Value = Math.Round(t, 1)

                ' Fire events with different severity based on the temperature value.
                ' The severity level is visible in the client's Event View.
                If t > 30.0 Then
                    server.FireEvent(machine, $"Temperature HIGH: {t:F1}C", EventSeverity.High)
                    Console.WriteLine($"  [EVENT HIGH] Temperature = {t:F1}C")
                ElseIf t > 25.0 Then
                    server.FireEvent(machine, $"Temperature warning: {t:F1}C", EventSeverity.Medium)
                    Console.WriteLine($"  [EVENT MED]  Temperature = {t:F1}C")
                Else
                    server.FireEvent(machine, $"Temperature normal: {t:F1}C", EventSeverity.Low)
                    Console.WriteLine($"  [EVENT LOW]  Temperature = {t:F1}C")
                End If

                Thread.Sleep(5000)
            End While

        End Using

    End Sub

End Module
