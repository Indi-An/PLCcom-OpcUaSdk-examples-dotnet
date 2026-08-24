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
' PLCcom OPC UA PubSub SDK - Workshop 12: UADP Unicast Subscriber
'
' This workshop is the receiving side of Workshop 11. The subscriber listens
' on a UDP port for incoming UADP network messages and raises a DataReceived
' event for each decoded data set message.
'
' DYNAMIC FIELD DISCOVERY:
'   This subscriber uses AddDataSetReader() WITHOUT AddField() - the field
'   layout is discovered automatically at runtime via the OPC UA PubSub
'   Discovery mechanism (spec Part 14, section 7.2.4.6).
'
'   ALTERNATIVE - static field configuration (more robust):
'     .AddDataSetReader("opcua:Workshop11", "Temperatures", Sub(ds)
'         ds.AddField("Sensor1", BuiltInType.Double)
'         ds.AddField("Sensor2", BuiltInType.Double)
'         ds.AddField("Sensor3", BuiltInType.Double)
'     End Sub)
'
' What you will learn:
'   * How to configure a UADP unicast subscriber
'   * How dynamic field discovery works via the OPC UA PubSub spec
'   * How to handle the DataReceived event
'   * The difference between KeyFrames and DeltaFrames
'
' Start Workshop 11 (UADP Unicast Publisher) first to send data.
' ==============================================================================

Imports System
Imports System.Linq
Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.PubSub.Sdk

Module Program

    Sub Main(args As String())
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 12                      ║")
        Console.WriteLine("║  UADP Unicast Subscriber                                     ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Listens for UADP messages on UDP and displays received      ║")
        Console.WriteLine("║  temperature sensor data from Workshop 11 (Publisher).       ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Listening on: opc.udp://localhost:4840                      ║")
        Console.WriteLine("║  Discovery:    opc.udp://localhost:4841                      ║")
        Console.WriteLine("║  Start Workshop 11 (Publisher) to send data.                 ║")
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

            ' -- Step 1: Configure the subscriber ----------------------------------
            Dim config = UaSubscriberConfiguration.Build("TemperatureSubscriber") _
                .WithNetworkInterface(NetworkInterfaces.All) _
                .WithTransport(PubSubTransportMode.DirectUnicast, "opc.udp://localhost:4840") _
                .WithDiscovery("opc.udp://localhost:4841") _
                .AddDataSetReader("opcua:Workshop11", "Temperatures")

            ' -- Step 2: Create subscriber and attach event handler ----------------
            Using subscriber As New UaSubscriber(LicenseUserName, LicenseSerial, config)

                Console.WriteLine("  License: " & subscriber.LicenceMessage)
                Console.WriteLine()

                Dim messageCount As Integer = 0

                AddHandler subscriber.DataReceived, Sub(sender, e)
                                                        messageCount += 1
                                                        Dim keyFrame As String = If(e.IsKeyFrame, "[KEY]", "[DEL]")
                                                        Dim fieldStr As String = String.Join("  ", e.Fields.Select(Function(f) $"{f.Key}={f.Value.Value:F2}"))
                                                        Console.WriteLine($"  [{messageCount:D5}] {keyFrame} {e.DataSetName} | {fieldStr}")
                                                    End Sub

                ' -- Step 3: Start listening ---------------------------------------
                subscriber.Start()

                Console.WriteLine("  Subscriber started. Waiting for messages...")
                Console.WriteLine("  Press ENTER to stop.")
                Console.WriteLine()

                Console.ReadLine()

                ' -- Step 4: Stop the subscriber -----------------------------------
                subscriber.Stop()
                Console.WriteLine($"  Subscriber stopped. Received {messageCount} messages total.")

            End Using

        Catch ex As Exception
            Console.WriteLine($"  Error: {ex.Message}")
        End Try

        Console.WriteLine()
        Console.WriteLine("  Press ENTER to exit.")
        Console.ReadLine()
    End Sub

End Module
