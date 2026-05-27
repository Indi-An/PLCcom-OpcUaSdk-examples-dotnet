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
' PLCcom OPC UA PubSub SDK - Workshop 21: MQTT UADP Publisher
'
' This workshop introduces broker-based PubSub using MQTT. Unlike the
' brokerless UDP workshops (11-14), the publisher does not send directly
' to subscribers. Instead it publishes messages to an MQTT broker, and
' the broker distributes them to all connected subscribers.
'
' WHY USE A BROKER?
'   * Decoupling: publisher and subscribers don't need to know each other
'   * Scalability: any number of subscribers can connect independently
'   * Reliability: broker can buffer messages for offline subscribers
'   * Firewall-friendly: only outbound TCP connections needed
'
' UADP ENCODING:
'   UADP (UA Binary) produces compact binary messages - ideal when bandwidth
'   is limited but you still want the benefits of broker-based messaging.
'   For human-readable JSON encoding see Workshops 31-34.
'
' PREREQUISITES:
'   An MQTT broker must be running on localhost:1883.
'
' What you will learn:
'   * How to configure an MQTT publisher with UADP encoding
'   * How broker-based PubSub differs from brokerless UDP
'
' Run Workshop 22 (MQTT UADP Subscriber) to receive the data.
' ==============================================================================

Imports System
Imports System.Threading
Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.PubSub.Sdk

Module Program

    Sub Main(args As String())
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 21                      ║")
        Console.WriteLine("║  MQTT UADP Publisher                                         ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Publishes motor telemetry via MQTT broker using UADP        ║")
        Console.WriteLine("║  binary encoding. Compact and efficient for constrained      ║")
        Console.WriteLine("║  networks. Broker decouples publisher from subscribers.      ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Broker: mqtt://localhost:1883                               ║")
        Console.WriteLine("║  Start Workshop 22 (Subscriber) to receive the data.         ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Try
            'TODO
            'Submit your license information from your license e-mail
            Dim LicenseUserName As String = "<Enter your UserName here>"
            Dim LicenseSerial As String = "<Enter your Serial here>"

            ' -- Step 1: Configure the publisher -----------------------------------
            Dim config = UaPublisherConfiguration.Build("MotorPublisher", "opcua:Workshop21") _
                .WithNetworkInterface(NetworkInterfaces.All) _
                .WithTransport(PubSubTransportMode.BrokerMqttUadp, "mqtt://localhost:1883") _
                .WithPublishingInterval(1000) _
                .AddDataSet("MotorData", Sub(ds)
                                             ds.AddField("Speed", New NodeId(3001UI, 2US))
                                             ds.AddField("Current", New NodeId(3002UI, 2US))
                                             ds.AddField("Temperature", New NodeId(3003UI, 2US))
                                             ds.WithKeyFrameCount(10)
                                             ds.WithInterval(1000)
                                         End Sub)

            ' -- Step 2: Create and start the publisher ----------------------------
            Using publisher As New UaPublisher(LicenseUserName, LicenseSerial, config)

                Console.WriteLine("  License: " & publisher.LicenceMessage)
                Console.WriteLine()

                publisher.Start()

                Console.WriteLine("  Publisher started. Connected to MQTT broker.")
                Console.WriteLine("  Publishing motor data every 1000 ms.")
                Console.WriteLine("  Press ENTER to stop.")
                Console.WriteLine()

                ' -- Step 3: Simulate motor telemetry ------------------------------
                Dim random As New Random()
                Dim counter As Integer = 0

                While Not Console.KeyAvailable
                    Dim speed As Double = 1450.0 + random.NextDouble() * 50.0
                    Dim current As Double = 12.5 + random.NextDouble() * 2.0
                    Dim temperature As Double = 55.0 + random.NextDouble() * 10.0

                    publisher.WriteValue("MotorData", "Speed", speed)
                    publisher.WriteValue("MotorData", "Current", current)
                    publisher.WriteValue("MotorData", "Temperature", temperature)

                    counter += 1
                    Console.Write($"  [{counter:D5}] Speed={speed:F0} rpm  Current={current:F1} A  Temp={temperature:F1}°C    " & Chr(13))

                    Thread.Sleep(1000)
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
