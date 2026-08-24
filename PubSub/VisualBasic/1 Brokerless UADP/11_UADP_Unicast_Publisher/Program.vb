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
' PLCcom OPC UA PubSub SDK - Workshop 11: UADP Unicast Publisher
'
' This is the simplest PubSub scenario: a publisher sends data directly to
' a single subscriber via UDP unicast - no broker, no multicast group,
' just point-to-point UDP communication.
'
' HOW IT WORKS:
'   The publisher sends UADP-encoded UDP datagrams to a fixed IP address
'   and port. Only the subscriber listening on that exact address will
'   receive the messages. This is ideal for simple, direct machine-to-machine
'   communication on a local network.
'
' DISCOVERY:
'   WithDiscovery() configures a separate UDP port (4841) where the publisher
'   listens for metadata requests. When the subscriber starts, it sends a
'   DataSetMetaData probe to this address. The publisher responds with the
'   field layout, so the subscriber does not need to pre-configure fields.
'   See Workshop 12 for the subscriber side.
'
' NETWORK INTERFACE:
'   NetworkInterfaces.All lets the OS choose the outgoing network adapter.
'   For production use, specify the exact adapter name instead:
'     .WithNetworkInterface("Ethernet")
'   List available adapters: netsh interface show interface
'
' What you will learn:
'   * How to configure a UADP unicast publisher
'   * How to define a data set with multiple fields
'   * How to write values and control the publishing lifecycle
'   * How the Delta Frame mechanism reduces bandwidth
'
' Run Workshop 12 (UADP Unicast Subscriber) to receive the published data.
' ==============================================================================

Imports System
Imports System.Threading
Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.PubSub.Sdk

Module Program

    Sub Main(args As String())
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 11                      ║")
        Console.WriteLine("║  UADP Unicast Publisher                                      ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Publishes temperature sensor data via UDP unicast using     ║")
        Console.WriteLine("║  UADP binary encoding. No broker required.                   ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Target:    opc.udp://localhost:4840                         ║")
        Console.WriteLine("║  Discovery: opc.udp://localhost:4841                         ║")
        Console.WriteLine("║  Start Workshop 12 (Subscriber) to receive the data.         ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

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

            ' -- Step 1: Configure the publisher -----------------------------------
            Dim config = UaPublisherConfiguration.Build("TemperaturePublisher", "opcua:Workshop11") _
                .WithNetworkInterface(NetworkInterfaces.All) _
                .WithTransport(PubSubTransportMode.DirectUnicast, "opc.udp://localhost:4840") _
                .WithDiscovery("opc.udp://localhost:4841") _
                .WithPublishingInterval(1000) _
                .AddDataSet("Temperatures", Sub(ds)
                                                ds.AddField("Sensor1", New NodeId(1001UI, 2US))
                                                ds.AddField("Sensor2", New NodeId(1002UI, 2US))
                                                ds.AddField("Sensor3", New NodeId(1003UI, 2US))
                                                ds.WithKeyFrameCount(10)
                                                ds.WithInterval(1000)
                                            End Sub)

            ' -- Step 2: Create and start the publisher ----------------------------
            Using publisher As New UaPublisher(LicenseUserName, LicenseSerial, config)

                Console.WriteLine("  License: " & publisher.LicenceMessage)
                Console.WriteLine()

                publisher.Start()

                Console.WriteLine("  Publisher started. Publishing every 1000 ms.")
                Console.WriteLine("  Press ENTER to stop.")
                Console.WriteLine()

                ' -- Step 3: Simulate changing sensor values -----------------------
                Dim random As New Random()
                Dim counter As Integer = 0
                Dim temp2 As Double = 30.0 + random.NextDouble() * 5.0
                Dim temp3 As Double = 40.0 + random.NextDouble() * 5.0

                While Not Console.KeyAvailable
                    counter += 1

                    Dim temp1 As Double = 20.0 + random.NextDouble() * 10.0
                    publisher.WriteValue("Temperatures", "Sensor1", temp1)

                    If counter Mod 3 = 0 Then
                        temp2 = 30.0 + random.NextDouble() * 5.0
                        publisher.WriteValue("Temperatures", "Sensor2", temp2)
                    End If

                    If counter Mod 5 = 0 Then
                        temp3 = 40.0 + random.NextDouble() * 5.0
                        publisher.WriteValue("Temperatures", "Sensor3", temp3)
                    End If

                    Console.WriteLine($"  [{counter:D5}] Sensor1={temp1:F2}°C  Sensor2={temp2:F2}°C  Sensor3={temp3:F2}°C")

                    Thread.Sleep(1000)
                End While

                Console.ReadKey(True)
                Console.WriteLine()
                Console.WriteLine()

                ' -- Step 4: Stop the publisher ------------------------------------
                publisher.Stop()
                Console.WriteLine("  Publisher stopped.")

            End Using

        Catch ex As Exception
            Console.WriteLine($"  Error: {ex.Message}")
        End Try

        Console.WriteLine()
        Console.WriteLine("  Press ENTER to exit.")
        Console.ReadLine()
    End Sub

End Module
