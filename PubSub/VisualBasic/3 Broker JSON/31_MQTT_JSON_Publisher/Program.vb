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
' PLCcom OPC UA PubSub SDK - Workshop 31: MQTT JSON Publisher
'
' This workshop demonstrates MQTT with JSON encoding - the most interoperable
' PubSub option. JSON-encoded messages can be consumed by ANY MQTT client,
' regardless of whether it implements OPC UA PubSub.
'
' UADP vs. JSON - when to choose which:
'
'   UADP (Workshops 21-24):
'     + Compact binary encoding - lower bandwidth
'     - Only readable by OPC UA PubSub clients
'     => Use for factory-floor, LAN, bandwidth-constrained scenarios
'
'   JSON (Workshops 31-34):
'     + Human-readable - easy to debug with standard MQTT tools
'     + Consumable by ANY MQTT client (cloud services, dashboards, scripts)
'     - Larger message size
'     => Use for cloud integration, cross-system communication, debugging
'
' DEBUGGING TIP:
'   Use MQTT Explorer (https://mqtt-explorer.com/) to inspect the JSON
'   messages live on the broker.
'
' PREREQUISITES:
'   An MQTT broker must be running on localhost:1883.
'
' What you will learn:
'   * How to configure an MQTT publisher with JSON encoding
'   * When to choose JSON over UADP encoding
'
' Run Workshop 32 (MQTT JSON Subscriber) to receive the data.
' ==============================================================================

Imports System
Imports System.Threading
Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.PubSub.Sdk

Module Program

    Sub Main(args As String())
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 31                      ║")
        Console.WriteLine("║  MQTT JSON Publisher                                         ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Publishes energy meter data via MQTT broker using JSON      ║")
        Console.WriteLine("║  encoding. Human-readable, cloud-ready, universally usable.  ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Broker: mqtt://localhost:1883                               ║")
        Console.WriteLine("║  Tip: Use MQTT Explorer to see the JSON messages live!       ║")
        Console.WriteLine("║  Start Workshop 32 (Subscriber) to receive the data.         ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Try
            'TODO
            'Submit your license information from your license e-mail
            Dim LicenseUserName As String = "<Enter your UserName here>"
            Dim LicenseSerial As String = "<Enter your Serial here>"

            ' -- Step 1: Configure the publisher -----------------------------------
            Dim config = UaPublisherConfiguration.Build("EnergyPublisher", "opcua:Workshop31") _
                .WithNetworkInterface(NetworkInterfaces.All) _
                .WithTransport(PubSubTransportMode.BrokerMqttJson, "mqtt://localhost:1883") _
                .WithPublishingInterval(2000) _
                .AddDataSet("EnergyMeter", Sub(ds)
                                               ds.AddField("Voltage", New NodeId(4001UI, 2US))
                                               ds.AddField("Current", New NodeId(4002UI, 2US))
                                               ds.AddField("Power", New NodeId(4003UI, 2US))
                                               ds.AddField("Energy", New NodeId(4004UI, 2US))
                                               ds.WithKeyFrameCount(5)
                                               ds.WithInterval(2000)
                                           End Sub)

            ' -- Step 2: Create and start the publisher ----------------------------
            Using publisher As New UaPublisher(LicenseUserName, LicenseSerial, config)

                Console.WriteLine("  License: " & publisher.LicenceMessage)
                Console.WriteLine()

                publisher.Start()

                Console.WriteLine("  Publisher started. Connected to MQTT broker.")
                Console.WriteLine("  Publishing energy data every 2000 ms as JSON.")
                Console.WriteLine("  Press ENTER to stop.")
                Console.WriteLine()

                ' -- Step 3: Simulate energy meter readings ------------------------
                Dim random As New Random()
                Dim counter As Integer = 0
                Dim totalEnergy As Double = 1000.0

                While Not Console.KeyAvailable
                    Dim voltage As Double = 230.0 + (random.NextDouble() - 0.5) * 10.0
                    Dim current As Double = 5.0 + random.NextDouble() * 3.0
                    Dim power As Double = voltage * current
                    totalEnergy += power * 2.0 / 3600.0

                    publisher.WriteValue("EnergyMeter", "Voltage", voltage)
                    publisher.WriteValue("EnergyMeter", "Current", current)
                    publisher.WriteValue("EnergyMeter", "Power", power)
                    publisher.WriteValue("EnergyMeter", "Energy", totalEnergy)

                    counter += 1
                    Console.Write($"  [{counter:D5}] {voltage:F1}V  {current:F2}A  {power:F0}W  {totalEnergy:F1}Wh    " & Chr(13))

                    Thread.Sleep(2000)
                End While

                Console.ReadKey(True)
                Console.WriteLine()
                Console.WriteLine()

                publisher.Stop()
                Console.WriteLine("  Publisher stopped. Disconnected from broker.")

            End Using

        Catch ex As Exception
            Console.WriteLine($"  Error: {ex.Message}")
        End Try

        Console.WriteLine()
        Console.WriteLine("  Press ENTER to exit.")
        Console.ReadLine()
    End Sub

End Module
