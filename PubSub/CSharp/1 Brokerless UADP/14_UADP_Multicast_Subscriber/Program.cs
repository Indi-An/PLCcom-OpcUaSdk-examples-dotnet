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
// PLCcom OPC UA PubSub SDK - Workshop 14: UADP Multicast Subscriber
//
// This workshop is the receiving side of Workshop 13. The subscriber joins
// the multicast group 239.0.0.1 and receives all messages published to it.
//
// HOW MULTICAST RECEIVING WORKS:
//   The subscriber does not connect to the publisher directly. Instead it
//   tells the OS to "join" the multicast group 239.0.0.1 via IGMP. From
//   that point on, any multicast packet sent to 239.0.0.1:4840 on the
//   local network will be delivered to this process. When the subscriber
//   stops, it leaves the group automatically.
//
// MULTIPLE INSTANCES:
//   You can run multiple instances of this subscriber simultaneously -
//   on the same machine or on different machines in the same LAN.
//   All instances receive every message from the publisher.
//   Try it: start two instances of Workshop 14 while Workshop 13 is running.
//
// FIELD CONFIGURATION:
//   This subscriber uses AddField() to pre-configure the expected field layout.
//   This is the recommended approach for production use - it works immediately
//   without waiting for a discovery response.
//
//   ALTERNATIVE - dynamic discovery (no AddField()):
//   The stack automatically sends a DataSetMetaData probe to the IANA-registered
//   OPC UA discovery address (opc.udp://224.0.2.14:4840) on startup.
//   The publisher responds with the field layout. You can omit AddField():
//     .AddDataSetReader("opcua:Workshop13", "PressureReadings")
//   This was tested and works from the very first message.
//
// NETWORK SCOPE:
//   Multicast works within a single LAN segment only.
//   It does NOT work across routers, VPNs, or the Internet.
//   For cross-network scenarios use MQTT (Workshops 21-34).
//
// What you will learn:
//   * How to configure a UADP multicast subscriber
//   * How to join a multicast group for receiving
//   * How multiple subscribers can receive the same data stream
//   * Static vs. dynamic field configuration
//
// Start Workshop 13 (UADP Multicast Publisher) first to send data.
// ==============================================================================

using System;
using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.PubSub.Sdk;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 14                      ║");
        Console.WriteLine("║  UADP Multicast Subscriber                                   ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Joins multicast group 239.0.0.1:4840 and displays           ║");
        Console.WriteLine("║  pressure sensor data from Workshop 13 (Publisher).          ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  You can run multiple instances simultaneously -             ║");
        Console.WriteLine("║  all will receive the same data at the same time.            ║");
        Console.WriteLine("║  Scope: same LAN segment only (no routing across subnets).   ║");
        Console.WriteLine("║  Start Workshop 13 (Publisher) to send data.                 ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        try
        {
            //TODO
            //Submit your license information from your license e-mail
            string LicenseUserName = "<Enter your UserName here>";
            string LicenseSerial   = "<Enter your Serial here>";

            // -- Step 1: Configure the subscriber ----------------------------------
            // Join the same multicast group the publisher sends to.
            // The address and port must match exactly between publisher and subscriber.
            var config = UaSubscriberConfiguration.Build("PressureSubscriber")
                .WithNetworkInterface(NetworkInterfaces.All)
                // NetworkInterfaces.All joins the multicast group on every active adapter.
                // This ensures messages are received regardless of which NIC the publisher
                // uses to send. For production use, restrict to the relevant adapter:
                //   .WithNetworkInterface("Ethernet")
                .WithTransport(PubSubTransportMode.DirectMulticast, "opc.udp://239.0.0.1:4840")
                // AddField() pre-configures the expected field layout.
                // Names, types and order must match what the publisher sends.
                // See the file header for the dynamic discovery alternative.
                .AddDataSetReader("opcua:Workshop13", "PressureReadings", ds => ds
                    .AddField("Inlet",        BuiltInType.Double)
                    .AddField("Outlet",       BuiltInType.Double)
                    .AddField("Differential", BuiltInType.Double));

            // -- Step 2: Create subscriber and attach event handler ----------------
            using var subscriber = new UaSubscriber(LicenseUserName, LicenseSerial, config);

            Console.WriteLine("  License: " + subscriber.LicenceMessage);
            Console.WriteLine();

            int messageCount = 0;

            subscriber.DataReceived += (sender, e) =>
            {
                messageCount++;
                Console.WriteLine($"  [{messageCount:D5}] {e.DataSetName} | KeyFrame: {e.IsKeyFrame}");

                foreach (var field in e.Fields)
                {
                    Console.WriteLine($"         {field.Key} = {field.Value.Value:F3} bar");
                }
                Console.WriteLine();
            };

            // -- Step 3: Start listening -------------------------------------------
            // Start() joins the multicast group on all active NICs.
            // From this point on, all multicast packets sent to 239.0.0.1:4840
            // on the local network will trigger the DataReceived event above.
            subscriber.Start();

            Console.WriteLine("  Subscriber started. Joined multicast group. Waiting for messages...");
            Console.WriteLine("  Press ENTER to stop.");
            Console.WriteLine();

            Console.ReadLine();

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
