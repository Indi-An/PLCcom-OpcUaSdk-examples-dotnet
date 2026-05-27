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
// PLCcom OPC UA PubSub SDK - Workshop 23: sMQTT UADP Publisher
//
// This workshop extends Workshop 21 (MQTT UADP) by adding TLS encryption
// to the broker connection. The data set, publishing interval and network
// interface configuration are identical to Workshop 21.
//
// WHAT IS sMQTT?
//   sMQTT (secure MQTT) is standard MQTT over TLS/SSL on port 8883.
//   The connection is encrypted and the broker's identity is verified
//   using X.509 certificates. This is the recommended approach for
//   production deployments and any communication over untrusted networks.
//
// CERTIFICATE MANAGEMENT - PKI STORE:
//   WithMqttTls() uses the standard OPC UA PKI directory convention:
//
//     ./pki/trusted/certs/   - directly trusted broker certificates
//     ./pki/trusted/crl/     - certificate revocation lists
//     ./pki/issuers/certs/   - trusted CA/issuer certificates
//     ./pki/issuers/crl/     - issuer revocation lists
//     ./pki/rejected/        - certificates rejected on first contact
//
//   FIRST RUN:
//     On the first connection attempt, the broker's certificate is placed
//     in ./pki/rejected/ and the connection is refused. You have two options:
//
//     Option A - Copy the CA certificate (recommended for production):
//       Copy C:\APL\mqtt\certs\ca.crt to ./pki/issuers/certs/
//       All certificates signed by this CA will then be trusted automatically.
//
//     Option B - Accept via CertificateValidation event (for testing):
//       Subscribe to publisher.CertificateValidation before calling Start():
//         publisher.CertificateValidation += (sender, e) => { e.Accept = true; };
//       This accepts any certificate without validation - use only for testing!
//
// MUTUAL TLS (mTLS):
//   If the broker requires client authentication (require_certificate true),
//   load a client certificate in PFX format and pass it to WithMqttTls():
//     var clientResult = UaPubSubCertificate.LoadOwnCertificate(
//         "./pki", "mqtt_client", "password",
//         UaPubSubCertificate.CertificateFormat.Pfx);
//     config.WithMqttTls("./pki", clientResult.Certificate);
//
// BROKER SETUP (Eclipse Mosquitto):
//   Certificate files are in C:\APL\mqtt\certs\ (generated with openssl).
//   The broker is configured in C:\APL\mqtt\mosquitto_tls.conf:
//     listener 8883
//     cafile   C:/APL/mqtt/certs/ca.crt
//     certfile C:/APL/mqtt/certs/broker.crt
//     keyfile  C:/APL/mqtt/certs/broker.key
//   The broker runs as a Windows Scheduled Task "Mosquitto TLS Broker".
//
// What you will learn:
//   * How to configure a sMQTT publisher with UADP encoding
//   * How the PKI store is used for broker certificate validation
//   * The difference between server-only TLS and mutual TLS (mTLS)
//   * How to set up a TLS-enabled MQTT broker
//
// Run Workshop 24 (sMQTT UADP Subscriber) to receive the data.
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
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 23                      ║");
        Console.WriteLine("║  sMQTT UADP Publisher (TLS)                                  ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Publishes motor telemetry via MQTT broker with TLS          ║");
        Console.WriteLine("║  encryption and UADP binary encoding.                        ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Broker: mqtts://localhost:8883                              ║");
        Console.WriteLine("║  Start Workshop 24 (Subscriber) to receive the data.         ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        try
        {
            //TODO
            //Submit your license information from your license e-mail
            string LicenseUserName = "<Enter your UserName here>";
            string LicenseSerial   = "<Enter your Serial here>";

            // -- Step 1: Configure the publisher -----------------------------------
            // Note the mqtts:// scheme and port 8883 instead of mqtt:// and 1883.
            // WithMqttTls() activates TLS and points to the PKI store directory.
            // See the file header for PKI setup instructions.
            var config = UaPublisherConfiguration.Build("MotorPublisher", "opcua:Workshop23")
                .WithNetworkInterface(NetworkInterfaces.All)
                .WithTransport(PubSubTransportMode.BrokerMqttUadp, "mqtts://localhost:8883")
                .WithMqttTls("./pki")
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

            // Subscribe to CertificateValidation to handle broker certificate trust.
            // This handler accepts any certificate - suitable for testing only.
            // For production, copy the CA certificate to ./pki/issuers/certs/ instead.
            publisher.CertificateValidation += (sender, e) =>
            {
                Console.WriteLine($"  [TLS] Broker certificate: {e.Certificate.Subject}");
                Console.WriteLine($"  [TLS] Issuer:             {e.Certificate.Issuer}");
                Console.WriteLine($"  [TLS] Valid until:        {e.Certificate.NotAfter:yyyy-MM-dd}");
                e.Accept = true;
            };

            publisher.Start();

            Console.WriteLine("  Publisher started. Connected to MQTT broker via TLS.");
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
                Console.Write($"\r  [{counter:D5}] Speed={speed:F0} rpm  Current={current:F1} A  Temp={temperature:F1}°C  [TLS]    ");

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
