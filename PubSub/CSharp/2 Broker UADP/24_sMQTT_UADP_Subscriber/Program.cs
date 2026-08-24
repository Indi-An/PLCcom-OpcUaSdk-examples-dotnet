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
// PLCcom OPC UA PubSub SDK - Workshop 24: sMQTT UADP Subscriber
//
// This workshop is the receiving side of Workshop 23. The subscriber connects
// to the MQTT broker via TLS and receives UADP-encoded motor telemetry.
// From the API perspective this is identical to Workshop 22 - only the
// scheme (mqtts://) and WithMqttTls() differ.
//
// CERTIFICATE MANAGEMENT - PKI STORE:
//   WithMqttTls() uses the standard OPC UA PKI directory convention:
//
//     ./pki/trusted/certs/   - directly trusted broker certificates
//     ./pki/issuer/certs/    - trusted CA/issuer certificates
//     ./pki/rejected/        - refused certificates (no trust anchor, or not time-valid); review, then move to trusted/certs/
//
//   FIRST RUN:
//     The broker certificate is accepted when it lies in ./pki/trusted/certs/, or
//     when a CA of its chain lies in ./pki/trusted/certs/ or ./pki/issuer/certs/,
//     and every certificate of that chain is time-valid. Anything refused - no
//     trust anchor, or expired / not yet valid - is copied to ./pki/rejected/ and
//     the connection fails; review it there and move the certificate to
//     ./pki/trusted/certs/ (or its CA to ./pki/issuer/certs/) to trust it.
//     On a fresh PKI, options:
//
//     Option A - Copy the CA certificate (recommended for production):
//       Copy C:\APL\mqtt\certs\ca.crt to ./pki/issuer/certs/
//
//     Option B - Accept via CertificateValidation event (for testing):
//       subscriber.CertificateValidation += (sender, e) => { e.Accept = true; };
//
// MULTIPLE SUBSCRIBERS:
//   Like all MQTT-based workshops, multiple subscribers can connect to the
//   same broker simultaneously. Each connects independently - no port
//   conflicts, no configuration changes needed. This works identically
//   for plain MQTT and sMQTT.
//
// What you will learn:
//   * How to configure a sMQTT subscriber with UADP decoding
//   * How the PKI store is used for broker certificate validation
//   * How multiple subscribers can connect simultaneously via sMQTT
//
// Start Workshop 23 (sMQTT UADP Publisher) first to send data.
// ==============================================================================

using System;
using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.PubSub.Sdk;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 24                      ║");
        Console.WriteLine("║  sMQTT UADP Subscriber (TLS)                                 ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Connects to MQTT broker via TLS and receives UADP-encoded   ║");
        Console.WriteLine("║  motor telemetry from Workshop 23 (Publisher).               ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Broker: mqtts://localhost:8883                              ║");
        Console.WriteLine("║  Start Workshop 23 (Publisher) to send data.                 ║");
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
            // Note the mqtts:// scheme and port 8883 instead of mqtt:// and 1883.
            // WithMqttTls() activates TLS and points to the PKI store directory.
            // See the file header for PKI setup instructions.
            var config = UaSubscriberConfiguration.Build("MotorSubscriber")
                .WithNetworkInterface(NetworkInterfaces.All)
                .WithTransport(PubSubTransportMode.BrokerMqttUadp, "mqtts://localhost:8883")
                .WithMqttTls("./pki")
                .AddDataSetReader("opcua:Workshop23", "MotorData", ds => ds
                    .AddField("Speed",       BuiltInType.Double)
                    .AddField("Current",     BuiltInType.Double)
                    .AddField("Temperature", BuiltInType.Double));

            // -- Step 2: Create subscriber and attach event handler ----------------
            using var subscriber = new UaSubscriber(LicenseUserName, LicenseSerial, config);

            Console.WriteLine("  License: " + subscriber.LicenceMessage);
            Console.WriteLine();

            // Subscribe to CertificateValidation to handle broker certificate trust.
            // This handler accepts any certificate - suitable for testing only.
            // For production, copy the CA certificate to ./pki/issuer/certs/ instead.
            subscriber.CertificateValidation += (sender, e) =>
            {
                Console.WriteLine($"  [TLS] Broker certificate: {e.Certificate.Subject}");
                Console.WriteLine($"  [TLS] Issuer:             {e.Certificate.Issuer}");
                Console.WriteLine($"  [TLS] Valid until:        {e.Certificate.NotAfter:yyyy-MM-dd}");
                e.Accept = true;
            };

            int messageCount = 0;

            subscriber.DataReceived += (sender, e) =>
            {
                messageCount++;
                Console.WriteLine($"  [{messageCount:D5}] Motor Telemetry (Seq#{e.SequenceNumber}) [TLS]:");

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

            Console.WriteLine("  Subscriber started. Connected to MQTT broker via TLS.");
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
