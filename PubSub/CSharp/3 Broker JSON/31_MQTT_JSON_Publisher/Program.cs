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
// PLCcom OPC UA PubSub SDK - Workshop 31: MQTT JSON Publisher
//
// This workshop demonstrates MQTT with JSON encoding - the most interoperable
// PubSub option. JSON-encoded messages can be consumed by ANY MQTT client,
// regardless of whether it implements OPC UA PubSub.
//
// UADP vs. JSON - when to choose which:
//
//   UADP (Workshops 21-24):
//     + Compact binary encoding - lower bandwidth
//     + Faster encoding/decoding
//     - Only readable by OPC UA PubSub clients
//     => Use for factory-floor, LAN, bandwidth-constrained scenarios
//
//   JSON (Workshops 31-34):
//     + Human-readable - easy to debug with standard MQTT tools
//     + Consumable by ANY MQTT client (cloud services, dashboards, scripts)
//     + Better interoperability with non-OPC UA systems
//     - Larger message size
//     => Use for cloud integration, cross-system communication, debugging
//
// DEBUGGING TIP:
//   Use MQTT Explorer (https://mqtt-explorer.com/) to inspect the JSON
//   messages live on the broker. You can see the exact topic structure
//   and message content without writing any code.
//
// CLOUD INTEGRATION:
//   JSON over MQTT is the standard format for cloud IoT platforms:
//     * AWS IoT Core
//     * Azure IoT Hub
//     * Google Cloud IoT
//   This makes Workshop 31/32 the ideal starting point for cloud scenarios.
//
// PREREQUISITES:
//   An MQTT broker must be running on localhost:1883.
//   For testing, Eclipse Mosquitto is recommended (see C:\APL\mqtt\).
//
// What you will learn:
//   * How to configure an MQTT publisher with JSON encoding
//   * How JSON messages look on the wire (try MQTT Explorer!)
//   * When to choose JSON over UADP encoding
//
// Run Workshop 32 (MQTT JSON Subscriber) to receive the data.
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
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 31                      ║");
        Console.WriteLine("║  MQTT JSON Publisher                                         ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Publishes energy meter data via MQTT broker using JSON      ║");
        Console.WriteLine("║  encoding. Human-readable, cloud-ready, universally usable.  ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Broker: mqtt://localhost:1883                               ║");
        Console.WriteLine("║  Tip: Use MQTT Explorer to see the JSON messages live!       ║");
        Console.WriteLine("║  Start Workshop 32 (Subscriber) to receive the data.         ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        try
        {
            //TODO
            //Submit your license information from your license e-mail
            string LicenseUserName = "<Enter your UserName here>";
            string LicenseSerial   = "<Enter your Serial here>";

            // -- Step 1: Configure the publisher -----------------------------------
            // BrokerMqttJson = JSON encoding over MQTT.
            // The only difference from Workshop 21 is BrokerMqttJson instead of
            // BrokerMqttUadp. Everything else - data sets, intervals, network
            // interface - works exactly the same.
            var config = UaPublisherConfiguration.Build("EnergyPublisher", "opcua:Workshop31")
                .WithNetworkInterface(NetworkInterfaces.All)
                .WithTransport(PubSubTransportMode.BrokerMqttJson, "mqtt://localhost:1883")
                .WithPublishingInterval(2000)
                .AddDataSet("EnergyMeter", ds => ds
                    .AddField("Voltage", new NodeId(4001, 2))
                    .AddField("Current", new NodeId(4002, 2))
                    .AddField("Power",   new NodeId(4003, 2))
                    .AddField("Energy",  new NodeId(4004, 2))
                    .WithKeyFrameCount(5)
                    .WithInterval(2000));

            // -- Step 2: Create and start the publisher ----------------------------
            using var publisher = new UaPublisher(LicenseUserName, LicenseSerial, config);

            Console.WriteLine("  License: " + publisher.LicenceMessage);
            Console.WriteLine();

            publisher.Start();

            Console.WriteLine("  Publisher started. Connected to MQTT broker.");
            Console.WriteLine("  Publishing energy data every 2000 ms as JSON.");
            Console.WriteLine("  Press ENTER to stop.");
            Console.WriteLine();

            // -- Step 3: Simulate energy meter readings ----------------------------
            var random = new Random();
            int counter = 0;
            double totalEnergy = 1000.0;

            while (!Console.KeyAvailable)
            {
                double voltage = 230.0 + (random.NextDouble() - 0.5) * 10.0;
                double current = 5.0   + random.NextDouble() * 3.0;
                double power   = voltage * current;
                totalEnergy   += power * 2.0 / 3600.0; // Wh accumulated over 2s

                publisher.WriteValue("EnergyMeter", "Voltage", voltage);
                publisher.WriteValue("EnergyMeter", "Current", current);
                publisher.WriteValue("EnergyMeter", "Power",   power);
                publisher.WriteValue("EnergyMeter", "Energy",  totalEnergy);

                counter++;
                Console.Write($"\r  [{counter:D5}] {voltage:F1}V  {current:F2}A  {power:F0}W  {totalEnergy:F1}Wh    ");

                Thread.Sleep(2000);
            }

            Console.ReadKey(true);
            Console.WriteLine();
            Console.WriteLine();

            publisher.Stop();
            Console.WriteLine("  Publisher stopped. Disconnected from broker.");
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
