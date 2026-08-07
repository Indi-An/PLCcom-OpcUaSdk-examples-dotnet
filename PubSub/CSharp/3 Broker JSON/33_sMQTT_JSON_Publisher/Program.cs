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
// PLCcom OPC UA PubSub SDK - Workshop 33: sMQTT JSON Publisher
//
// This workshop combines the two concepts from the previous workshops:
//   * JSON encoding from Workshop 31 (human-readable, universally consumable)
//   * TLS security from Workshop 23 (encrypted broker connection)
//
// This is the recommended configuration for cloud and cross-network deployments
// where both security and interoperability are required. JSON over sMQTT is
// the standard approach for connecting OPC UA PubSub to cloud IoT platforms
// like AWS IoT Core, Azure IoT Hub, or Google Cloud IoT.
//
// UADP vs. JSON - when to choose which:
//
//   UADP + TLS (Workshops 23/24):
//     + Compact binary encoding - lower bandwidth
//     - Only readable by OPC UA PubSub clients
//     => Use for factory-floor, LAN, bandwidth-constrained scenarios
//
//   JSON + TLS (Workshops 33/34):
//     + Human-readable - easy to debug with standard MQTT tools
//     + Consumable by ANY MQTT client (cloud services, dashboards, scripts)
//     - Larger message size
//     => Use for cloud integration, cross-system communication
//
// CERTIFICATE MANAGEMENT - PKI STORE:
//   Same as Workshop 23. See that workshop for full PKI setup instructions.
//
//     ./pki/trusted/certs/   - directly trusted broker certificates
//     ./pki/issuer/certs/    - trusted CA/issuer certificates (copy ca.crt here)
//     ./pki/rejected/        - refused certificates (no trust anchor, or not time-valid); review, then move to trusted/certs/
//
// BROKER SETUP:
//   Same as Workshop 23 - see that workshop for full Mosquitto TLS setup.
//   The broker runs as a Windows Scheduled Task "Mosquitto TLS Broker".
//
// What you will learn:
//   * How to combine JSON encoding with TLS security
//   * When to use JSON+TLS vs. UADP+TLS
//   * How to inspect JSON messages with MQTT Explorer over a TLS connection
//
// Run Workshop 34 (sMQTT JSON Subscriber) to receive the data.
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
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 33                      ║");
        Console.WriteLine("║  sMQTT JSON Publisher (TLS + JSON)                           ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Publishes energy meter data via MQTT broker with TLS        ║");
        Console.WriteLine("║  encryption and JSON encoding. Secure and interoperable.     ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Broker: mqtts://localhost:8883                              ║");
        Console.WriteLine("║  Start Workshop 34 (Subscriber) to receive the data.         ║");
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
            // WithMqttTls() activates TLS and points to the PKI store directory.
            // See the file header for PKI setup instructions.
            var config = UaPublisherConfiguration.Build("EnergyPublisher", "opcua:Workshop33")
                .WithNetworkInterface(NetworkInterfaces.All)
                .WithTransport(PubSubTransportMode.BrokerMqttJson, "mqtts://localhost:8883")
                .WithMqttTls("./pki")
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

            // Subscribe to CertificateValidation to handle broker certificate trust.
            // This handler accepts any certificate - suitable for testing only.
            // For production, copy the CA certificate to ./pki/issuer/certs/ instead.
            publisher.CertificateValidation += (sender, e) =>
            {
                Console.WriteLine($"  [TLS] Broker certificate: {e.Certificate.Subject}");
                Console.WriteLine($"  [TLS] Issuer:             {e.Certificate.Issuer}");
                Console.WriteLine($"  [TLS] Valid until:        {e.Certificate.NotAfter:yyyy-MM-dd}");
                e.Accept = true;
            };

            publisher.Start();

            Console.WriteLine("  Publisher started. Connected to MQTT broker via TLS.");
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
                totalEnergy   += power * 2.0 / 3600.0;

                publisher.WriteValue("EnergyMeter", "Voltage", voltage);
                publisher.WriteValue("EnergyMeter", "Current", current);
                publisher.WriteValue("EnergyMeter", "Power",   power);
                publisher.WriteValue("EnergyMeter", "Energy",  totalEnergy);

                counter++;
                Console.Write($"\r  [{counter:D5}] {voltage:F1}V  {current:F2}A  {power:F0}W  {totalEnergy:F1}Wh  [TLS+JSON]    ");

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
