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
' PLCcom OPC UA PubSub SDK - Workshop 14: UADP Multicast Subscriber
'
' This workshop is the receiving side of Workshop 13. The subscriber joins
' the multicast group 239.0.0.1 and receives all messages published to it.
'
' MULTIPLE INSTANCES:
'   You can run multiple instances of this subscriber simultaneously -
'   on the same machine or on different machines in the same LAN.
'   All instances receive every message from the publisher.
'
' FIELD CONFIGURATION:
'   This subscriber uses AddField() to pre-configure the expected field layout.
'   This is the recommended approach for production use.
'
' What you will learn:
'   * How to configure a UADP multicast subscriber
'   * How to join a multicast group for receiving
'   * How multiple subscribers can receive the same data stream
'
' Start Workshop 13 (UADP Multicast Publisher) first to send data.
' ==============================================================================

Imports System
Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.PubSub.Sdk

Module Program

    Sub Main(args As String())
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 14                      ║")
        Console.WriteLine("║  UADP Multicast Subscriber                                   ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Joins multicast group 239.0.0.1:4840 and displays           ║")
        Console.WriteLine("║  pressure sensor data from Workshop 13 (Publisher).          ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  You can run multiple instances simultaneously -             ║")
        Console.WriteLine("║  all will receive the same data at the same time.            ║")
        Console.WriteLine("║  Start Workshop 13 (Publisher) to send data.                 ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Try
            'TODO
            'Submit your license information from your license e-mail
            Dim LicenseUserName As String = "<Enter your UserName here>"
            Dim LicenseSerial As String = "<Enter your Serial here>"

            ' -- Step 1: Configure the subscriber ----------------------------------
            Dim config = UaSubscriberConfiguration.Build("PressureSubscriber") _
                .WithNetworkInterface(NetworkInterfaces.All) _
                .WithTransport(PubSubTransportMode.DirectMulticast, "opc.udp://239.0.0.1:4840") _
                .AddDataSetReader("opcua:Workshop13", "PressureReadings", Sub(ds)
                                                                              ds.AddField("Inlet", BuiltInType.Double)
                                                                              ds.AddField("Outlet", BuiltInType.Double)
                                                                              ds.AddField("Differential", BuiltInType.Double)
                                                                          End Sub)

            ' -- Step 2: Create subscriber and attach event handler ----------------
            Using subscriber As New UaSubscriber(LicenseUserName, LicenseSerial, config)

                Console.WriteLine("  License: " & subscriber.LicenceMessage)
                Console.WriteLine()

                Dim messageCount As Integer = 0

                AddHandler subscriber.DataReceived, Sub(sender, e)
                                                        messageCount += 1
                                                        Console.WriteLine($"  [{messageCount:D5}] {e.DataSetName} | KeyFrame: {e.IsKeyFrame}")
                                                        For Each field In e.Fields
                                                            Console.WriteLine($"         {field.Key} = {field.Value.Value:F3} bar")
                                                        Next
                                                        Console.WriteLine()
                                                    End Sub

                ' -- Step 3: Start listening ---------------------------------------
                subscriber.Start()

                Console.WriteLine("  Subscriber started. Joined multicast group. Waiting for messages...")
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
