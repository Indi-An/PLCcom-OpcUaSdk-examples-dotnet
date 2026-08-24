// MIT License
// Copyright (c) Indi.An GmbH
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

// ==============================================================================
// PLCcom OPC UA PubSub SDK - Workshop 16: UADP Broadcast Subscriber
//
// This workshop is the receiving side of Workshop 15. The subscriber opens a UDP
// broadcast-capable socket on port 4840 and receives UADP DataSetMessages sent to
// the local IPv4 broadcast domain.
//
// HOW BROADCAST RECEIVING WORKS:
//   The subscriber does not connect to the publisher. It listens on the UDP port.
//   When the publisher sends a datagram to 255.255.255.255:4840, the operating
//   system can deliver that datagram to subscribers in the same broadcast domain.
//
// MULTIPLE INSTANCES:
//   Broadcast is designed for one-to-many distribution. You can run multiple
//   subscribers on different machines in the same subnet. Running multiple
//   subscribers on the same machine depends on the operating system socket rules.
//
// FIELD CONFIGURATION:
//   This subscriber uses AddField() to pre-configure the expected field layout.
//   That makes the workshop deterministic and immediately readable. Discovery is
//   shown in the unicast pair 11/12; static fields are often the clearer choice
//   for broadcast scenarios.
//
// NETWORK SCOPE:
//   Broadcast stays local. For routed or cross-site PubSub communication, use
//   MQTT with a broker instead (Workshops 21-34).
//
// What you will learn:
//   * How to configure a UADP broadcast subscriber
//   * How broadcast receives one-to-many data without an MQTT broker
//   * How to read named fields from high-level PubSub events
//   * How to stop the subscriber cleanly with ENTER
//
// Start Workshop 15 (UADP Broadcast Publisher) first to send data.
// ==============================================================================

using System;
using System.Threading;
using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.PubSub.Sdk;

class Program
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 16                      ║");
        Console.WriteLine("║  UADP Broadcast Subscriber                                   ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Receives pressure sensor data via UDP broadcast using       ║");
        Console.WriteLine("║  UADP binary encoding. No broker required.                   ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Listening: opc.udp://255.255.255.255:4840                   ║");
        Console.WriteLine("║  Start Workshop 15 (Publisher) to send data.                 ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        try
        {
            // TODO
            // Submit your license information from your license e-mail.
            // Important !!!!!!!!!!!!!!!!!!
            // Enter your Username + Serial here! Please note: with blank fields the library runs
            // for 15 minutes during a debug session. Both values can also come
            // from configuration or an environment variable.
            // Free trial license (14 days, uninterrupted): https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-download/
            string LicenseUserName = "";
            string LicenseSerial   = "";

            // -- Step 1: Configure the subscriber ----------------------------------
            // The endpoint is the same broadcast address used by the publisher. This
            // lets the underlying transport create a broadcast-capable UDP socket.
            var config = UaSubscriberConfiguration.Build("BroadcastPressureSubscriber")
                .WithNetworkInterface(NetworkInterfaces.All)
                .WithTransport(PubSubTransportMode.DirectBroadcast, "opc.udp://255.255.255.255:4840")
                // The DataSetReader filters by PublisherId and DataSet name. Messages
                // from other publishers or other data sets are ignored.
                .AddDataSetReader("opcua:Workshop15", "PressureReadings", ds => ds
                    // Names, order and types must match the publisher data set.
                    .AddField("Inlet",        BuiltInType.Double)
                    .AddField("Outlet",       BuiltInType.Double)
                    .AddField("Differential", BuiltInType.Double));

            // -- Step 2: Create subscriber and attach event handler ----------------
            using var subscriber = new UaSubscriber(LicenseUserName, LicenseSerial, config);

            Console.WriteLine("  License: " + subscriber.LicenceMessage);
            Console.WriteLine();

            int messageCount = 0;
            object consoleLock = new object();

            subscriber.DataReceived += (sender, e) =>
            {
                lock (consoleLock)
                {
                    messageCount++;

                    Console.WriteLine($"  [{messageCount:D5}] {e.DataSetName} | KeyFrame: {e.IsKeyFrame}");
                    foreach (var field in e.Fields)
                    {
                        Console.WriteLine($"         {field.Key} = {field.Value.Value:F2} bar");
                    }
                    Console.WriteLine();
                }
            };

            // -- Step 3: Start listening -------------------------------------------
            // Start() opens the UDP receive path. Incoming DataSetMessages are
            // decoded and forwarded to the DataReceived event above.
            subscriber.Start();

            Console.WriteLine("  Subscriber started. Waiting for broadcast messages...");
            Console.WriteLine("  Press ENTER to stop.");
            Console.WriteLine();

            if (Console.IsInputRedirected)
            {
                Thread.Sleep(Timeout.Infinite);
            }
            else
            {
                Console.ReadLine();
            }

            subscriber.Stop();
            Console.WriteLine($"  Subscriber stopped. Received {messageCount} messages total.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Error: {ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("  Press ENTER to exit.");
        Console.ReadLine();
    }
}
