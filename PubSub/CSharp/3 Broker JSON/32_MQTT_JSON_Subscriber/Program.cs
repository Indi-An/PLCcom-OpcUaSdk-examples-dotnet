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
// PLCcom OPC UA PubSub SDK - Workshop 32: MQTT JSON Subscriber
//
// This workshop is the receiving side of Workshop 31. The subscriber connects
// to the MQTT broker and receives JSON-encoded energy meter data.
//
// TRANSPARENT ENCODING:
//   From the subscriber API perspective, JSON and UADP are completely
//   transparent. The DataReceived event, field access, and all other
//   subscriber operations work identically regardless of encoding.
//   The only configuration difference is BrokerMqttJson instead of
//   BrokerMqttUadp in WithTransport().
//
// MQTT RETAIN AND DYNAMIC FIELD DISCOVERY:
//   Like Workshop 22, this subscriber can omit AddField() and rely on
//   the MQTT RETAIN metadata mechanism for dynamic field discovery.
//   The publisher publishes DataSetMetaData with RETAIN on the metadata
//   topic. The broker delivers it instantly when the subscriber connects.
//
//   RECOMMENDED approach (used here) - static field configuration:
//     .AddDataSetReader("opcua:Workshop31", "EnergyMeter", ds => ds
//         .AddField("Voltage", BuiltInType.Double) ...)
//
//   ALTERNATIVE - dynamic field discovery (omit AddField()):
//     .AddDataSetReader("opcua:Workshop31", "EnergyMeter")
//   Works for both UADP and JSON encoding - the metadata mechanism is
//   independent of the message encoding.
//
// PREREQUISITES:
//   An MQTT broker must be running on localhost:1883.
//   Start Workshop 31 (MQTT JSON Publisher) first to send data.
//
// What you will learn:
//   * How to configure an MQTT subscriber with JSON decoding
//   * How the SDK handles JSON and UADP transparently from the API
//   * How MQTT RETAIN enables instant metadata delivery for JSON too
//
// Start Workshop 31 (MQTT JSON Publisher) first to send data.
// ==============================================================================

using System;
using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.PubSub.Sdk;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 32                      ║");
        Console.WriteLine("║  MQTT JSON Subscriber                                        ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Connects to MQTT broker and receives JSON-encoded energy    ║");
        Console.WriteLine("║  meter data from Workshop 31 (Publisher).                    ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Broker: mqtt://localhost:1883                               ║");
        Console.WriteLine("║  Start Workshop 31 (Publisher) to send data.                 ║");
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
            // BrokerMqttJson = JSON decoding over MQTT.
            // See the file header for the dynamic field discovery alternative.
            var config = UaSubscriberConfiguration.Build("EnergySubscriber")
                .WithNetworkInterface(NetworkInterfaces.All)
                .WithTransport(PubSubTransportMode.BrokerMqttJson, "mqtt://localhost:1883")
                .AddDataSetReader("opcua:Workshop31", "EnergyMeter", ds => ds
                    .AddField("Voltage", BuiltInType.Double)
                    .AddField("Current", BuiltInType.Double)
                    .AddField("Power",   BuiltInType.Double)
                    .AddField("Energy",  BuiltInType.Double));

            // -- Step 2: Create subscriber and attach event handler ----------------
            using var subscriber = new UaSubscriber(LicenseUserName, LicenseSerial, config);

            Console.WriteLine("  License: " + subscriber.LicenceMessage);
            Console.WriteLine();

            int messageCount = 0;

            subscriber.DataReceived += (sender, e) =>
            {
                messageCount++;
                Console.WriteLine($"  [{messageCount:D5}] Energy Meter (Seq#{e.SequenceNumber}):");

                foreach (var field in e.Fields)
                {
                    string unit = field.Key switch
                    {
                        "Voltage" => "V",
                        "Current" => "A",
                        "Power"   => "W",
                        "Energy"  => "Wh",
                        _         => ""
                    };
                    Console.WriteLine($"         {field.Key} = {field.Value.Value} {unit}");
                }
                Console.WriteLine();
            };

            // -- Step 3: Start listening -------------------------------------------
            subscriber.Start();

            Console.WriteLine("  Subscriber started. Connected to MQTT broker.");
            Console.WriteLine("  Waiting for JSON messages...");
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
