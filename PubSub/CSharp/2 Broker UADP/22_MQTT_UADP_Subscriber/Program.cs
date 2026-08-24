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
// PLCcom OPC UA PubSub SDK - Workshop 22: MQTT UADP Subscriber
//
// This workshop is the receiving side of Workshop 21. The subscriber connects
// to the same MQTT broker and subscribes to the appropriate topics. The broker
// delivers messages to all connected subscribers simultaneously.
//
// MQTT RETAIN AND DYNAMIC FIELD DISCOVERY:
//   The publisher automatically publishes DataSetMetaData with the RETAIN flag
//   on the metadata topic (opcua/metadata/MotorData). When this subscriber
//   connects, the broker instantly delivers the retained metadata message.
//   This means the subscriber can decode fields WITHOUT pre-configuring them
//   via AddField() - the field layout is received from the broker automatically.
//
//   This was tested and confirmed: Fields are populated from the very first
//   received message (no timing issues).
//
//   RECOMMENDED approach (used here) - static field configuration:
//     .AddDataSetReader("opcua:Workshop21", "MotorData", ds => ds
//         .AddField("Speed",       BuiltInType.Double)
//         .AddField("Current",     BuiltInType.Double)
//         .AddField("Temperature", BuiltInType.Double))
//   Advantages: works for all transports, documents the expected structure,
//   does not depend on broker RETAIN support.
//
//   ALTERNATIVE - dynamic field discovery (omit AddField()):
//     .AddDataSetReader("opcua:Workshop21", "MotorData")
//   The field layout is received from the broker's retained metadata message.
//
// MULTIPLE SUBSCRIBERS:
//   Unlike UDP unicast, MQTT allows any number of subscribers to connect to
//   the same broker and receive the same messages simultaneously. Each
//   subscriber connects independently - no port conflicts, no configuration
//   changes needed on the publisher side.
//
// PREREQUISITES:
//   An MQTT broker must be running on localhost:1883.
//   Start Workshop 21 (MQTT UADP Publisher) first to send data.
//
// What you will learn:
//   * How to configure an MQTT subscriber with UADP decoding
//   * How the broker delivers messages to all connected subscribers
//   * How MQTT RETAIN enables instant metadata delivery
//   * Static vs. dynamic field configuration for MQTT
//
// Start Workshop 21 (MQTT UADP Publisher) first to send data.
// ==============================================================================

using System;
using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.PubSub.Sdk;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 22                      ║");
        Console.WriteLine("║  MQTT UADP Subscriber                                        ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Connects to MQTT broker and receives UADP-encoded motor     ║");
        Console.WriteLine("║  telemetry from Workshop 21 (Publisher).                     ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Broker: mqtt://localhost:1883                               ║");
        Console.WriteLine("║  Start Workshop 21 (Publisher) to send data.                 ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        try
        {
            // Important !!!!!!!!!!!!!!!!!!
            // Enter your Username + Serial here! Please note: with blank fields the library runs
            // for 15 minutes during a debug session. Both values can also come
            // from configuration or an environment variable.
            // Free trial license (14 days, uninterrupted): https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-download/
            string LicenseUserName = "";
            string LicenseSerial   = "";

            // -- Step 1: Configure the subscriber ----------------------------------
            // AddField() pre-configures the expected field layout. This is the
            // recommended approach - see the file header for the dynamic alternative.
            var config = UaSubscriberConfiguration.Build("MotorSubscriber")
                .WithNetworkInterface(NetworkInterfaces.All)
                .WithTransport(PubSubTransportMode.BrokerMqttUadp, "mqtt://localhost:1883")
                .AddDataSetReader("opcua:Workshop21", "MotorData", ds => ds
                    .AddField("Speed",       BuiltInType.Double)
                    .AddField("Current",     BuiltInType.Double)
                    .AddField("Temperature", BuiltInType.Double));

            // -- Step 2: Create subscriber and attach event handler ----------------
            using var subscriber = new UaSubscriber(LicenseUserName, LicenseSerial, config);

            Console.WriteLine("  License: " + subscriber.LicenceMessage);
            Console.WriteLine();

            int messageCount = 0;

            subscriber.DataReceived += (sender, e) =>
            {
                messageCount++;
                Console.WriteLine($"  [{messageCount:D5}] Motor Telemetry (Seq#{e.SequenceNumber}):");

                foreach (var field in e.Fields)
                {
                    string unit = field.Key switch
                    {
                        "Speed"       => "rpm",
                        "Current"     => "A",
                        "Temperature" => "°C",
                        _             => ""
                    };
                    Console.WriteLine($"         {field.Key} = {field.Value.Value} {unit}");
                }
                Console.WriteLine();
            };

            // -- Step 3: Start listening -------------------------------------------
            subscriber.Start();

            Console.WriteLine("  Subscriber started. Connected to MQTT broker.");
            Console.WriteLine("  Waiting for messages...");
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
