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
// PLCcom OPC UA PubSub SDK - Workshop 12: UADP Unicast Subscriber
//
// This workshop is the receiving side of Workshop 11. The subscriber listens
// on a UDP port for incoming UADP network messages and raises a DataReceived
// event for each decoded data set message.
//
// DYNAMIC FIELD DISCOVERY:
//   This subscriber uses AddDataSetReader() WITHOUT AddField() - the field
//   layout is discovered automatically at runtime via the OPC UA PubSub
//   Discovery mechanism (spec Part 14, section 7.2.4.6):
//
//   1. On startup, the subscriber sends a DataSetMetaData probe to the
//      publisher's discovery address (opc.udp://localhost:4841).
//   2. The publisher responds with a DataSetMetaData announcement containing
//      the complete field layout (names, types, order).
//   3. The subscriber uses this metadata to decode incoming messages.
//
//   This approach requires WithDiscovery() to be configured and the publisher
//   to be running with a matching WithDiscovery() address.
//
//   ALTERNATIVE - static field configuration (more robust):
//   If you know the field layout in advance, you can pre-configure it:
//     .AddDataSetReader("opcua:Workshop11", "Temperatures", ds => ds
//         .AddField("Sensor1", BuiltInType.Double)
//         .AddField("Sensor2", BuiltInType.Double)
//         .AddField("Sensor3", BuiltInType.Double))
//   This works without discovery and is recommended for production use.
//
// DELTA FRAMES:
//   The publisher sends KeyFrames (all fields) every 10 messages and
//   DeltaFrames (only changed fields) in between. The e.IsKeyFrame property
//   in the DataReceived event tells you which type was received.
//
// What you will learn:
//   * How to configure a UADP unicast subscriber
//   * How dynamic field discovery works via the OPC UA PubSub spec
//   * How to handle the DataReceived event
//   * The difference between KeyFrames and DeltaFrames
//
// Start Workshop 11 (UADP Unicast Publisher) first to send data.
// ==============================================================================

using System;
using System.Linq;
using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.PubSub.Sdk;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 12                      ║");
        Console.WriteLine("║  UADP Unicast Subscriber                                     ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Listens for UADP messages on UDP and displays received      ║");
        Console.WriteLine("║  temperature sensor data from Workshop 11 (Publisher).       ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Listening on: opc.udp://localhost:4840                      ║");
        Console.WriteLine("║  Discovery:    opc.udp://localhost:4841                      ║");
        Console.WriteLine("║  Start Workshop 11 (Publisher) to send data.                 ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        try
        {
            //TODO
            //Submit your license information from your license e-mail
            string LicenseUserName = "<Enter your UserName here>";
            string LicenseSerial   = "<Enter your Serial here>";

            // -- Step 1: Configure the subscriber ----------------------------------
            // WithDiscovery() points to the publisher's discovery listener.
            // The subscriber will send a metadata probe on startup and receive
            // the field layout dynamically - no AddField() needed here.
            var config = UaSubscriberConfiguration.Build("TemperatureSubscriber")
                .WithNetworkInterface(NetworkInterfaces.All)
                .WithTransport(PubSubTransportMode.DirectUnicast, "opc.udp://localhost:4840")
                .WithDiscovery("opc.udp://localhost:4841")
                // No AddField() - field layout is discovered from the publisher.
                // The publisher ID and data set name must match Workshop 11 exactly.
                .AddDataSetReader("opcua:Workshop11", "Temperatures");

            // -- Step 2: Create subscriber and attach event handler ----------------
            using var subscriber = new UaSubscriber(LicenseUserName, LicenseSerial, config);

            Console.WriteLine("  License: " + subscriber.LicenceMessage);
            Console.WriteLine();

            int messageCount = 0;

            subscriber.DataReceived += (sender, e) =>
            {
                messageCount++;
                // e.IsKeyFrame = true  -> all fields are present (every 10th message)
                // e.IsKeyFrame = false -> only changed fields are present (DeltaFrame)
                string keyFrame = e.IsKeyFrame ? "[KEY]" : "[DEL]";
                var fieldStr = string.Join("  ", e.Fields.Select(f => $"{f.Key}={f.Value.Value:F2}"));
                Console.WriteLine($"  [{messageCount:D5}] {keyFrame} {e.DataSetName} | {fieldStr}");
            };

            // -- Step 3: Start listening -------------------------------------------
            subscriber.Start();

            Console.WriteLine("  Subscriber started. Waiting for messages...");
            Console.WriteLine("  Press ENTER to stop.");
            Console.WriteLine();

            Console.ReadLine();

            // -- Step 4: Stop the subscriber ---------------------------------------
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
