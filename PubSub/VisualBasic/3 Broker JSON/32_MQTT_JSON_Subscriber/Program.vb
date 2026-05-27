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
' PLCcom OPC UA PubSub SDK - Workshop 32: MQTT JSON Subscriber
'
' This workshop is the receiving side of Workshop 31. The subscriber connects
' to the MQTT broker and receives JSON-encoded energy meter data.
'
' TRANSPARENT ENCODING:
'   From the subscriber API perspective, JSON and UADP are completely
'   transparent. The DataReceived event, field access, and all other
'   subscriber operations work identically regardless of encoding.
'   The only configuration difference is BrokerMqttJson instead of
'   BrokerMqttUadp in WithTransport().
'
' PREREQUISITES:
'   An MQTT broker must be running on localhost:1883.
'   Start Workshop 31 (MQTT JSON Publisher) first to send data.
'
' What you will learn:
'   * How to configure an MQTT subscriber with JSON decoding
'   * How the SDK handles JSON and UADP transparently from the API
'
' Start Workshop 31 (MQTT JSON Publisher) first to send data.
' ==============================================================================

Imports System
Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.PubSub.Sdk

Module Program

    Sub Main(args As String())
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 32                      ║")
        Console.WriteLine("║  MQTT JSON Subscriber                                        ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Connects to MQTT broker and receives JSON-encoded energy    ║")
        Console.WriteLine("║  meter data from Workshop 31 (Publisher).                    ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Broker: mqtt://localhost:1883                               ║")
        Console.WriteLine("║  Start Workshop 31 (Publisher) to send data.                 ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Try
            'TODO
            'Submit your license information from your license e-mail
            Dim LicenseUserName As String = "<Enter your UserName here>"
            Dim LicenseSerial As String = "<Enter your Serial here>"

            ' -- Step 1: Configure the subscriber ----------------------------------
            Dim config = UaSubscriberConfiguration.Build("EnergySubscriber") _
                .WithNetworkInterface(NetworkInterfaces.All) _
                .WithTransport(PubSubTransportMode.BrokerMqttJson, "mqtt://localhost:1883") _
                .AddDataSetReader("opcua:Workshop31", "EnergyMeter", Sub(ds)
                                                                         ds.AddField("Voltage", BuiltInType.Double)
                                                                         ds.AddField("Current", BuiltInType.Double)
                                                                         ds.AddField("Power", BuiltInType.Double)
                                                                         ds.AddField("Energy", BuiltInType.Double)
                                                                     End Sub)

            ' -- Step 2: Create subscriber and attach event handler ----------------
            Using subscriber As New UaSubscriber(LicenseUserName, LicenseSerial, config)

                Console.WriteLine("  License: " & subscriber.LicenceMessage)
                Console.WriteLine()

                Dim messageCount As Integer = 0

                AddHandler subscriber.DataReceived, Sub(sender, e)
                                                        messageCount += 1
                                                        Console.WriteLine($"  [{messageCount:D5}] Energy Meter (Seq#{e.SequenceNumber}):")
                                                        For Each field In e.Fields
                                                            Dim unit As String = ""
                                                            Select Case field.Key
                                                                Case "Voltage" : unit = "V"
                                                                Case "Current" : unit = "A"
                                                                Case "Power" : unit = "W"
                                                                Case "Energy" : unit = "Wh"
                                                            End Select
                                                            Console.WriteLine($"         {field.Key} = {field.Value.Value} {unit}")
                                                        Next
                                                        Console.WriteLine()
                                                    End Sub

                ' -- Step 3: Start listening ---------------------------------------
                subscriber.Start()

                Console.WriteLine("  Subscriber started. Connected to MQTT broker.")
                Console.WriteLine("  Waiting for JSON messages...")
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
