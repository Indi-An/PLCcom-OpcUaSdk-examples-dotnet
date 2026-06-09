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
' PLCcom OPC UA PubSub SDK - Workshop 16: UADP Broadcast Subscriber
'
' This workshop is the receiving side of Workshop 15. The subscriber listens for
' UADP datagrams sent to the local UDP broadcast port.
'
' MULTIPLE INSTANCES:
'   Broadcast is a one-to-many transport. Multiple subscribers can receive the
'   same publisher stream when the operating system allows them to bind the UDP
'   port. If a second instance cannot bind the port, stop the first subscriber
'   or use another machine in the same broadcast domain.
'
' FIELD CONFIGURATION:
'   This subscriber uses AddField() to pre-configure the expected field layout.
'   This keeps the workshop focused on broadcast transport. Discovery-based
'   field negotiation is shown in Workshop 12.
'
' What you will learn:
'   * How to configure a UADP broadcast subscriber
'   * How to receive one-to-many data without an MQTT broker
'   * How to read named field values from high-level PubSub events
'   * Why broadcast traffic stays inside the local network segment
'
' Start Workshop 15 (UADP Broadcast Publisher) first to send data.
' ==============================================================================

Imports System
Imports System.Threading
Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.PubSub.Sdk

Module Program

    Sub Main(args As String())
        Console.OutputEncoding = System.Text.Encoding.UTF8

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 16                      ║")
        Console.WriteLine("║  UADP Broadcast Subscriber                                   ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Receives pressure sensor data via UDP broadcast using       ║")
        Console.WriteLine("║  UADP binary encoding. No broker required.                   ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Listening on: opc.udp://255.255.255.255:4840               ║")
        Console.WriteLine("║  Scope:        same broadcast domain only                    ║")
        Console.WriteLine("║  Start Workshop 15 (Publisher) to send data.                 ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Try
            ' TODO
            ' Submit your license information from your license e-mail.
            Dim LicenseUserName As String = "<Enter your UserName here>"
            Dim LicenseSerial As String = "<Enter your Serial here>"

            ' -- Step 1: Configure the subscriber ----------------------------------
            ' The PublisherId and DataSet name must match Workshop 15. The endpoint
            ' uses the same broadcast address and UDP port as the publisher.
            Dim config = UaSubscriberConfiguration.Build("BroadcastPressureSubscriber") _
                .WithNetworkInterface(NetworkInterfaces.All) _
                .WithTransport(PubSubTransportMode.DirectBroadcast, "opc.udp://255.255.255.255:4840") _
                .AddDataSetReader("opcua:Workshop15", "PressureReadings", Sub(ds)
                                                                              ' Static fields are useful when the DataSet layout is known.
                                                                              ' The subscriber can then decode the values immediately without
                                                                              ' waiting for a separate discovery exchange.
                                                                              ds.AddField("Inlet", BuiltInType.Double)
                                                                              ds.AddField("Outlet", BuiltInType.Double)
                                                                              ds.AddField("Differential", BuiltInType.Double)
                                                                          End Sub)

            ' -- Step 2: Create subscriber and attach event handler ----------------
            Using subscriber As New UaSubscriber(LicenseUserName, LicenseSerial, config)

                Console.WriteLine("  License: " & subscriber.LicenceMessage)
                Console.WriteLine()

                Dim messageCount As Integer = 0
                Dim consoleLock As New Object()

                AddHandler subscriber.DataReceived, Sub(sender, e)
                                                        SyncLock consoleLock
                                                            messageCount += 1
                                                            Dim frameKind As String = If(e.IsKeyFrame, "KeyFrame", "DeltaFrame")
                                                            Console.WriteLine($"  [{messageCount:D5}] {e.DataSetName} ({frameKind})")

                                                            For Each field In e.Fields
                                                                Console.WriteLine($"         {field.Key} = {field.Value.Value:F2} bar")
                                                            Next

                                                            Console.WriteLine()
                                                        End SyncLock
                                                    End Sub

                ' -- Step 3: Start listening ---------------------------------------
                subscriber.Start()

                Console.WriteLine("  Subscriber started. Listening for broadcast messages.")
                Console.WriteLine("  Press ENTER to stop.")
                Console.WriteLine()

                If Console.IsInputRedirected Then
                    Thread.Sleep(Timeout.Infinite)
                Else
                    Console.ReadLine()
                End If

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
