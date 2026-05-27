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
// PLCcom OPC UA PubSub SDK - Workshop 13: UADP Multicast Publisher
//
// Multicast extends the unicast approach from Workshop 11 to one-to-many
// communication. Instead of sending to a specific IP address, the publisher
// sends to a multicast group address. Any subscriber that has joined this
// group will receive the messages - without the publisher knowing who is
// listening.
//
// MULTICAST ADDRESS:
//   239.0.0.1 is in the administratively scoped range (RFC 2365).
//   This range (239.0.0.0/8) is safe for private networks and will not
//   be routed beyond your LAN. Choose any address in this range for your
//   application - just make sure publisher and all subscribers use the
//   same address and port.
//   Avoid 224.0.0.x - those addresses are reserved for network protocols.
//
// NETWORK SCOPE - IMPORTANT:
//   Multicast works within a single LAN segment (Layer 2 broadcast domain).
//   It does NOT work across routers, VPNs, or the Internet by default.
//   If your publisher and subscribers span multiple subnets or sites,
//   use MQTT with a broker instead (see Workshops 21-34).
//
// MULTIPLE SUBSCRIBERS:
//   Unlike unicast, any number of subscribers can join the same multicast
//   group simultaneously - on the same machine or on different machines
//   in the same LAN. All of them receive every published message.
//   This is ideal for factory-floor scenarios where multiple HMIs,
//   dashboards, or loggers all need the same data stream.
//
// FIREWALL:
//   Make sure UDP port 4840 is allowed for incoming traffic on subscriber
//   machines. Windows Firewall may block it by default.
//   Quick fix (run as Administrator):
//     netsh advfirewall firewall add rule name="OPC UA PubSub"
//       protocol=UDP dir=in localport=4840 action=allow
//
// What you will learn:
//   * How to configure a UADP multicast publisher
//   * The difference between unicast and multicast addressing
//   * How multicast enables one-to-many communication without broker
//   * Network scope and limitations of UDP multicast
//
// Run Workshop 14 (UADP Multicast Subscriber) on one or more machines.
// ==============================================================================

using System;
using System.Threading;
using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.PubSub.Sdk;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 13                      ║");
        Console.WriteLine("║  UADP Multicast Publisher                                    ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Publishes pressure sensor data via UDP multicast using      ║");
        Console.WriteLine("║  UADP binary encoding. Multiple subscribers can receive.     ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Multicast group: opc.udp://239.0.0.1:4840                   ║");
        Console.WriteLine("║  Scope:           same LAN segment only                      ║");
        Console.WriteLine("║  Start Workshop 14 (Subscriber) to receive the data.         ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        try
        {
            //TODO
            //Submit your license information from your license e-mail
            string LicenseUserName = "<Enter your UserName here>";
            string LicenseSerial   = "<Enter your Serial here>";

            // -- Step 1: Configure the publisher -----------------------------------
            // DirectMulticast sends to a multicast group address instead of a
            // specific IP. All subscribers that have joined 239.0.0.1:4840 will
            // receive the messages.
            var config = UaPublisherConfiguration.Build("PressurePublisher", "opcua:Workshop13")
                .WithNetworkInterface(NetworkInterfaces.All)
                // NetworkInterfaces.All sends on all active NICs simultaneously.
                // This ensures delivery regardless of which NIC reaches the subscribers.
                // For production use, restrict to the relevant adapter:
                //   .WithNetworkInterface("Ethernet")
                .WithTransport(PubSubTransportMode.DirectMulticast, "opc.udp://239.0.0.1:4840")
                .WithPublishingInterval(500)
                .AddDataSet("PressureReadings", ds => ds
                    .AddField("Inlet",        new NodeId(2001, 2))
                    .AddField("Outlet",       new NodeId(2002, 2))
                    .AddField("Differential", new NodeId(2003, 2))
                    .WithKeyFrameCount(20)
                    .WithInterval(500));

            // -- Step 2: Create and start the publisher ----------------------------
            using var publisher = new UaPublisher(LicenseUserName, LicenseSerial, config);

            Console.WriteLine("  License: " + publisher.LicenceMessage);
            Console.WriteLine();

            publisher.Start();

            Console.WriteLine("  Publisher started. Publishing every 500 ms to multicast group.");
            Console.WriteLine("  Press ENTER to stop.");
            Console.WriteLine();

            // -- Step 3: Simulate pressure sensor readings -------------------------
            var random = new Random();
            int counter = 0;

            while (!Console.KeyAvailable)
            {
                double inlet        = 2.5 + random.NextDouble() * 0.5;
                double outlet       = 1.8 + random.NextDouble() * 0.3;
                double differential = inlet - outlet;

                publisher.WriteValue("PressureReadings", "Inlet",        inlet);
                publisher.WriteValue("PressureReadings", "Outlet",       outlet);
                publisher.WriteValue("PressureReadings", "Differential", differential);

                counter++;
                Console.Write($"\r  [{counter:D5}] Inlet={inlet:F3} bar  Outlet={outlet:F3} bar  Diff={differential:F3} bar    ");

                Thread.Sleep(500);
            }

            Console.ReadKey(true);
            Console.WriteLine();
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
