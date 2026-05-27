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
// PLCcom OPC UA PubSub SDK - Workshop 21: MQTT UADP Publisher
//
// This workshop introduces broker-based PubSub using MQTT. Unlike the
// brokerless UDP workshops (11-14), the publisher does not send directly
// to subscribers. Instead it publishes messages to an MQTT broker, and
// the broker distributes them to all connected subscribers.
//
// WHY USE A BROKER?
//   * Decoupling: publisher and subscribers don't need to know each other
//   * Scalability: any number of subscribers can connect independently
//   * Reliability: broker can buffer messages for offline subscribers
//   * Firewall-friendly: only outbound TCP connections needed
//   * Cross-network: works across routers, VPNs, and the Internet
//
// UADP ENCODING:
//   UADP (UA Binary) produces compact binary messages - ideal when bandwidth
//   is limited but you still want the benefits of broker-based messaging.
//   For human-readable JSON encoding see Workshops 31-34.
//
// MQTT TOPICS:
//   The SDK automatically manages MQTT topics following the OPC UA PubSub
//   topic convention:
//     opcua/data/<DataSetName>      - data messages
//     opcua/metadata/<DataSetName>  - metadata (published with RETAIN flag)
//   The RETAIN flag on metadata means new subscribers instantly receive the
//   field layout when they connect - no discovery probe needed.
//
// PREREQUISITES:
//   An MQTT broker must be running on localhost:1883.
//   For testing, Eclipse Mosquitto is recommended (see C:\APL\mqtt\).
//   The broker is configured in C:\APL\mqtt\mosquitto_plain.conf.
//
// What you will learn:
//   * How to configure an MQTT publisher with UADP encoding
//   * How broker-based PubSub differs from brokerless UDP
//   * How MQTT topics are used to route messages
//   * How the RETAIN flag enables instant metadata delivery
//
// Run Workshop 22 (MQTT UADP Subscriber) to receive the data.
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
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 21                      ║");
        Console.WriteLine("║  MQTT UADP Publisher                                         ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Publishes motor telemetry via MQTT broker using UADP        ║");
        Console.WriteLine("║  binary encoding. Compact and efficient for constrained      ║");
        Console.WriteLine("║  networks. Broker decouples publisher from subscribers.      ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Broker: mqtt://localhost:1883                               ║");
        Console.WriteLine("║  Start Workshop 22 (Subscriber) to receive the data.         ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        try
        {
            //TODO
            //Submit your license information from your license e-mail
            string LicenseUserName = "<Enter your UserName here>";
            string LicenseSerial   = "<Enter your Serial here>";

            // -- Step 1: Configure the publisher -----------------------------------
            // BrokerMqttUadp = UADP binary encoding over MQTT.
            // The broker URL uses the mqtt:// scheme for plain (unencrypted) MQTT.
            // For TLS-encrypted MQTT (sMQTT) see Workshop 23 (mqtts:// scheme).
            var config = UaPublisherConfiguration.Build("MotorPublisher", "opcua:Workshop21")
                .WithNetworkInterface(NetworkInterfaces.All)
                .WithTransport(PubSubTransportMode.BrokerMqttUadp, "mqtt://localhost:1883")
                .WithPublishingInterval(1000)
                .AddDataSet("MotorData", ds => ds
                    .AddField("Speed",       new NodeId(3001, 2))
                    .AddField("Current",     new NodeId(3002, 2))
                    .AddField("Temperature", new NodeId(3003, 2))
                    .WithKeyFrameCount(10)
                    .WithInterval(1000));

            // -- Step 2: Create and start the publisher ----------------------------
            using var publisher = new UaPublisher(LicenseUserName, LicenseSerial, config);

            Console.WriteLine("  License: " + publisher.LicenceMessage);
            Console.WriteLine();

            publisher.Start();

            Console.WriteLine("  Publisher started. Connected to MQTT broker.");
            Console.WriteLine("  Publishing motor data every 1000 ms.");
            Console.WriteLine("  Press ENTER to stop.");
            Console.WriteLine();

            // -- Step 3: Simulate motor telemetry ----------------------------------
            var random = new Random();
            int counter = 0;

            while (!Console.KeyAvailable)
            {
                double speed       = 1450.0 + random.NextDouble() * 50.0;
                double current     = 12.5   + random.NextDouble() * 2.0;
                double temperature = 55.0   + random.NextDouble() * 10.0;

                publisher.WriteValue("MotorData", "Speed",       speed);
                publisher.WriteValue("MotorData", "Current",     current);
                publisher.WriteValue("MotorData", "Temperature", temperature);

                counter++;
                Console.Write($"\r  [{counter:D5}] Speed={speed:F0} rpm  Current={current:F1} A  Temp={temperature:F1}°C    ");

                Thread.Sleep(1000);
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
