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
// PLCcom OPC UA PubSub SDK - Workshop 11: UADP Unicast Publisher
//
// This is the simplest PubSub scenario: a publisher sends data directly to
// a single subscriber via UDP unicast - no broker, no multicast group,
// just point-to-point UDP communication.
//
// HOW IT WORKS:
//   The publisher sends UADP-encoded UDP datagrams to a fixed IP address
//   and port. Only the subscriber listening on that exact address will
//   receive the messages. This is ideal for simple, direct machine-to-machine
//   communication on a local network.
//
// DISCOVERY:
//   WithDiscovery() configures a separate UDP port (4841) where the publisher
//   listens for metadata requests. When the subscriber starts, it sends a
//   DataSetMetaData probe to this address. The publisher responds with the
//   field layout, so the subscriber does not need to pre-configure fields.
//   See Workshop 12 for the subscriber side.
//
// NETWORK INTERFACE:
//   NetworkInterfaces.All lets the OS choose the outgoing network adapter.
//   For production use, specify the exact adapter name instead:
//     .WithNetworkInterface("Ethernet")
//   List available adapters: netsh interface show interface
//
// What you will learn:
//   * How to configure a UADP unicast publisher
//   * How to define a data set with multiple fields
//   * How to write values and control the publishing lifecycle
//   * How the Delta Frame mechanism reduces bandwidth
//
// Run Workshop 12 (UADP Unicast Subscriber) to receive the published data.
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
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 11                      ║");
        Console.WriteLine("║  UADP Unicast Publisher                                      ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Publishes temperature sensor data via UDP unicast using     ║");
        Console.WriteLine("║  UADP binary encoding. No broker required.                   ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Target:    opc.udp://localhost:4840                         ║");
        Console.WriteLine("║  Discovery: opc.udp://localhost:4841                         ║");
        Console.WriteLine("║  Start Workshop 12 (Subscriber) to receive the data.         ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        try
        {
            //TODO
            //Submit your license information from your license e-mail
            string LicenseUserName = "<Enter your UserName here>";
            string LicenseSerial   = "<Enter your Serial here>";

            // -- Step 1: Configure the publisher -----------------------------------
            // UaPublisherConfiguration uses a fluent builder pattern.
            // Build() requires a publisher name (for logging) and a publisher ID
            // (transmitted in every message so subscribers can filter by source).
            var config = UaPublisherConfiguration.Build("TemperaturePublisher", "opcua:Workshop11")
                .WithNetworkInterface(NetworkInterfaces.All)
                .WithTransport(PubSubTransportMode.DirectUnicast, "opc.udp://localhost:4840")
                // WithDiscovery() enables metadata exchange on a separate port.
                // The subscriber can request the field layout without pre-configuration.
                .WithDiscovery("opc.udp://localhost:4841")
                .WithPublishingInterval(1000)
                .AddDataSet("Temperatures", ds => ds
                    // Each field maps a logical name to an OPC UA NodeId.
                    // The NodeId identifies the variable in the publisher's data store.
                    // Subscribers reference fields by their logical name (e.g. "Sensor1").
                    .AddField("Sensor1", new NodeId(1001, 2))
                    .AddField("Sensor2", new NodeId(1002, 2))
                    .AddField("Sensor3", new NodeId(1003, 2))
                    // KeyFrameCount controls the Delta Frame mechanism:
                    // Every 10th message is a full KeyFrame (all fields).
                    // Messages in between are DeltaFrames (only changed fields).
                    // This reduces bandwidth significantly when values change rarely.
                    .WithKeyFrameCount(10)
                    .WithInterval(1000));

            // -- Step 2: Create and start the publisher ----------------------------
            // Pass your license information to the constructor.
            // The license is validated immediately - an InvalidOperationException
            // is thrown if the license is invalid.
            using var publisher = new UaPublisher(LicenseUserName, LicenseSerial, config);

            Console.WriteLine("  License: " + publisher.LicenceMessage);
            Console.WriteLine();

            publisher.Start();

            Console.WriteLine("  Publisher started. Publishing every 1000 ms.");
            Console.WriteLine("  Press ENTER to stop.");
            Console.WriteLine();

            // -- Step 3: Simulate changing sensor values ---------------------------
            // Sensor1 changes every second (every tick).
            // Sensor2 changes every 3 seconds - demonstrates delta frames.
            // Sensor3 changes every 5 seconds - demonstrates delta frames.
            //
            // WriteValue() stores the value in the publisher's internal data store.
            // The publishing engine picks it up at the next interval tick and
            // includes it in the outgoing network message.
            var random = new Random();
            int counter = 0;
            double temp2 = 30.0 + random.NextDouble() * 5.0;
            double temp3 = 40.0 + random.NextDouble() * 5.0;

            while (!Console.KeyAvailable)
            {
                counter++;

                double temp1 = 20.0 + random.NextDouble() * 10.0;
                publisher.WriteValue("Temperatures", "Sensor1", temp1);

                if (counter % 3 == 0)
                {
                    temp2 = 30.0 + random.NextDouble() * 5.0;
                    publisher.WriteValue("Temperatures", "Sensor2", temp2);
                }

                if (counter % 5 == 0)
                {
                    temp3 = 40.0 + random.NextDouble() * 5.0;
                    publisher.WriteValue("Temperatures", "Sensor3", temp3);
                }

                Console.WriteLine($"  [{counter:D5}] Sensor1={temp1:F2}°C  Sensor2={temp2:F2}°C  Sensor3={temp3:F2}°C");

                Thread.Sleep(1000);
            }

            Console.ReadKey(true);
            Console.WriteLine();
            Console.WriteLine();

            // -- Step 4: Stop the publisher ----------------------------------------
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
