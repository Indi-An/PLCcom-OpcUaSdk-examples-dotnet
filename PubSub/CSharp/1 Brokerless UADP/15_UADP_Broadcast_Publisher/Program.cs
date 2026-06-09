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
// PLCcom OPC UA PubSub SDK - Workshop 15: UADP Broadcast Publisher
//
// Broadcast is another brokerless UDP PubSub variant. The publisher sends UADP
// datagrams to an IPv4 broadcast address. Every subscriber listening on the UDP
// port in the same broadcast domain can receive the messages.
//
// BROADCAST ADDRESS:
//   255.255.255.255 is the limited IPv4 broadcast address. It is useful for
//   local workshop tests, but it stays inside the local broadcast domain and is
//   not routed. In production systems, prefer an explicit subnet broadcast
//   address or a dedicated multicast group when the network design allows it.
//
// NETWORK SCOPE - IMPORTANT:
//   Broadcast is a Layer-2/local-subnet mechanism. Routers normally do not
//   forward broadcast packets. For routed networks, VPNs, WAN links or cloud
//   scenarios, use MQTT with a broker instead (see Workshops 21-34).
//
// MULTIPLE SUBSCRIBERS:
//   Any number of subscribers can listen on the broadcast port. They do not
//   connect to the publisher; they simply receive UDP datagrams sent to the
//   broadcast address.
//
// FIREWALL:
//   Make sure UDP port 4840 is allowed for incoming traffic on subscriber
//   machines. Windows Firewall may block it by default.
//
// What you will learn:
//   * How to configure a UADP broadcast publisher
//   * How broadcast differs from unicast and multicast
//   * How to publish one-to-many data without an MQTT broker
//   * Why broadcast should be used deliberately and locally
//
// Run Workshop 16 (UADP Broadcast Subscriber) to receive the published data.
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
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 15                      ║");
        Console.WriteLine("║  UADP Broadcast Publisher                                    ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Publishes pressure sensor data via UDP broadcast using      ║");
        Console.WriteLine("║  UADP binary encoding. No broker required.                   ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Broadcast: opc.udp://255.255.255.255:4840                   ║");
        Console.WriteLine("║  Scope:     same broadcast domain only                       ║");
        Console.WriteLine("║  Start Workshop 16 (Subscriber) to receive the data.         ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        try
        {
            // TODO
            // Submit your license information from your license e-mail.
            string LicenseUserName = "<Enter your UserName here>";
            string LicenseSerial   = "<Enter your Serial here>";

            // -- Step 1: Configure the publisher -----------------------------------
            // Build() requires a readable publisher name and the PublisherId that is
            // encoded into every NetworkMessage. Subscribers use this PublisherId to
            // accept data only from the intended source.
            var config = UaPublisherConfiguration.Build("BroadcastPressurePublisher", "opcua:Workshop15")
                .WithNetworkInterface(NetworkInterfaces.All)
                // DirectBroadcast uses brokerless UDP/UADP and sends datagrams to
                // the configured IPv4 broadcast address. The low-level transport
                // enables the broadcast socket option and sends to the endpoint.
                .WithTransport(PubSubTransportMode.DirectBroadcast, "opc.udp://255.255.255.255:4840")
                .WithPublishingInterval(1000)
                .AddDataSet("PressureReadings", ds => ds
                    // Each field maps a logical PubSub name to an OPC UA NodeId in
                    // the publisher data store. The workshop writes values by field
                    // name; the NodeIds keep the data model OPC UA compatible.
                    .AddField("Inlet",        new NodeId(1101, 2))
                    .AddField("Outlet",       new NodeId(1102, 2))
                    .AddField("Differential", new NodeId(1103, 2))
                    // Every 10th message is a full KeyFrame. Messages in between
                    // may be DeltaFrames with only changed fields.
                    .WithKeyFrameCount(10)
                    .WithInterval(1000));

            // -- Step 2: Create and start the publisher ----------------------------
            using var publisher = new UaPublisher(LicenseUserName, LicenseSerial, config);

            Console.WriteLine("  License: " + publisher.LicenceMessage);
            Console.WriteLine();

            publisher.Start();

            Console.WriteLine("  Publisher started. Publishing every 1000 ms to broadcast.");
            Console.WriteLine("  Press ENTER to stop.");
            Console.WriteLine();

            using var stopRequested = new ManualResetEventSlim(false);
            if (!Console.IsInputRedirected)
            {
                var inputThread = new Thread(() =>
                {
                    Console.ReadLine();
                    stopRequested.Set();
                })
                {
                    IsBackground = true
                };
                inputThread.Start();
            }

            // -- Step 3: Simulate pressure sensor readings -------------------------
            // A real application would write process values from its own data model.
            // Here we generate changing values so Workshop 16 can show live updates.
            var random = new Random();
            int counter = 0;

            while (!stopRequested.IsSet)
            {
                double inlet        = 5.0 + random.NextDouble();
                double outlet       = 4.2 + random.NextDouble();
                double differential = inlet - outlet;

                publisher.WriteValue("PressureReadings", "Inlet",        inlet);
                publisher.WriteValue("PressureReadings", "Outlet",       outlet);
                publisher.WriteValue("PressureReadings", "Differential", differential);

                counter++;
                Console.WriteLine(
                    $"  [{counter:D5}] Inlet={inlet:F2} bar  Outlet={outlet:F2} bar  Differential={differential:F2} bar");

                Thread.Sleep(1000);
            }

            Console.WriteLine();

            publisher.Stop();
            Console.WriteLine("  Publisher stopped.");
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
