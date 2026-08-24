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
' PLCcom OPC UA PubSub SDK - Workshop 22: MQTT UADP Subscriber
'
' This workshop is the receiving side of Workshop 21. The subscriber connects
' to the same MQTT broker and subscribes to the appropriate topics. The broker
' delivers messages to all connected subscribers simultaneously.
'
' MQTT RETAIN AND DYNAMIC FIELD DISCOVERY:
'   The publisher automatically publishes DataSetMetaData with the RETAIN flag.
'   This subscriber can omit AddField() and rely on the RETAIN metadata.
'
'   RECOMMENDED approach (used here) - static field configuration:
'     .AddDataSetReader("opcua:Workshop21", "MotorData", Sub(ds)
'         ds.AddField("Speed",       BuiltInType.Double)
'         ds.AddField("Current",     BuiltInType.Double)
'         ds.AddField("Temperature", BuiltInType.Double)
'     End Sub)
'
'   ALTERNATIVE - dynamic field discovery (omit AddField()):
'     .AddDataSetReader("opcua:Workshop21", "MotorData")
'
' PREREQUISITES:
'   An MQTT broker must be running on localhost:1883.
'   Start Workshop 21 (MQTT UADP Publisher) first to send data.
'
' What you will learn:
'   * How to configure an MQTT subscriber with UADP decoding
'   * How the broker delivers messages to all connected subscribers
'
' Start Workshop 21 (MQTT UADP Publisher) first to send data.
' ==============================================================================

Imports System
Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.PubSub.Sdk

Module Program

    Sub Main(args As String())
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 22                      ║")
        Console.WriteLine("║  MQTT UADP Subscriber                                        ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Connects to MQTT broker and receives UADP-encoded motor     ║")
        Console.WriteLine("║  telemetry from Workshop 21 (Publisher).                     ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Broker: mqtt://localhost:1883                               ║")
        Console.WriteLine("║  Start Workshop 21 (Publisher) to send data.                 ║")
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
            Dim config = UaSubscriberConfiguration.Build("MotorSubscriber") _
                .WithNetworkInterface(NetworkInterfaces.All) _
                .WithTransport(PubSubTransportMode.BrokerMqttUadp, "mqtt://localhost:1883") _
                .AddDataSetReader("opcua:Workshop21", "MotorData", Sub(ds)
                                                                       ds.AddField("Speed", BuiltInType.Double)
                                                                       ds.AddField("Current", BuiltInType.Double)
                                                                       ds.AddField("Temperature", BuiltInType.Double)
                                                                   End Sub)

            ' -- Step 2: Create subscriber and attach event handler ----------------
            Using subscriber As New UaSubscriber(LicenseUserName, LicenseSerial, config)

                Console.WriteLine("  License: " & subscriber.LicenceMessage)
                Console.WriteLine()

                Dim messageCount As Integer = 0

                AddHandler subscriber.DataReceived, Sub(sender, e)
                                                        messageCount += 1
                                                        Console.WriteLine($"  [{messageCount:D5}] Motor Telemetry (Seq#{e.SequenceNumber}):")
                                                        For Each field In e.Fields
                                                            Dim unit As String = ""
                                                            Select Case field.Key
                                                                Case "Speed" : unit = "rpm"
                                                                Case "Current" : unit = "A"
                                                                Case "Temperature" : unit = "°C"
                                                            End Select
                                                            Console.WriteLine($"         {field.Key} = {field.Value.Value} {unit}")
                                                        Next
                                                        Console.WriteLine()
                                                    End Sub

                ' -- Step 3: Start listening ---------------------------------------
                subscriber.Start()

                Console.WriteLine("  Subscriber started. Connected to MQTT broker.")
                Console.WriteLine("  Waiting for messages...")
                Console.WriteLine("  Press ENTER to stop.")
                Console.WriteLine()

                Console.ReadLine()

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
