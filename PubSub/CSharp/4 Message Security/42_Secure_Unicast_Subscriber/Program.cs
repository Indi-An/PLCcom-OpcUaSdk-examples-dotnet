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
// PLCcom OPC UA PubSub SDK - Workshop 42: Secure UADP Subscriber (fixed key)
//
// The receiving side of Workshop 41. The subscriber checks the signature of every
// NetworkMessage and decrypts the data (OPC UA Part 14, 7.2.4.4). A message with
// a wrong signature - changed on the way, or sent by someone who does not hold
// the key - is dropped, and it never reaches your DataReceived handler.
//
// THE KEY MUST BE THE SAME ON BOTH SIDES:
//   Group, policy, SecurityTokenId and key bytes must match those of the
//   publisher. This program reads the key from the same file as Workshop 41:
//   the path comes from PLCCOM_PUBSUB_KEYFILE (default: pubsub-group.key). Run
//   both in one folder, or copy the file once to the other machine over a
//   channel you trust. The program prints a short key ID, never the key. If the
//   IDs differ, the keys differ, and nothing will arrive.
//
// FIELDS:
//   The field layout is configured here (AddField with the type), because the
//   metadata of a secured publisher is secured too, and a key mismatch would
//   leave the subscriber without fields as well as without data.
//
// What you will learn:
//   * How to configure a secured UADP subscriber
//   * That a message that fails the check never arrives
//   * What to look at when nothing arrives (key ID, token number, policy)
// ==============================================================================

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.PubSub.Encoding.Security;
using PLCcom.Opc.Ua.PubSub.Sdk;

class Program
{
    const string KeyFileVariable = "PLCCOM_PUBSUB_KEYFILE";
    const string DefaultKeyFile = "pubsub-group.key";

    // Must be the number the publisher uses (Workshop 41).
    const long SecurityTokenId = 1;

    static void Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 42                      ║");
        Console.WriteLine("║  Secure UADP Subscriber (fixed key from a file)              ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Checks the signature and decrypts the data of Workshop 41.  ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Listening on: opc.udp://localhost:4840                      ║");
        Console.WriteLine("║  Start Workshop 41 (Publisher) to send data.                 ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        try
        {
            string LicenseUserName = "";
            string LicenseSerial   = "";
            if (LicenseUserName.Length == 0) LicenseUserName = System.Environment.GetEnvironmentVariable("PLCCOM_OPCUA_LICENSE_USER") ?? "";
            if (LicenseSerial.Length == 0) LicenseSerial = System.Environment.GetEnvironmentVariable("PLCCOM_OPCUA_LICENSE_SERIAL") ?? "";

            // -- Step 1: Read the key (the same file as the publisher) --------------
            UadpMessageSecurityPolicy policy = UadpMessageSecurityPolicy.PubSubAes256Ctr;
            string keyFile = Environment.GetEnvironmentVariable(KeyFileVariable);
            if (string.IsNullOrWhiteSpace(keyFile)) keyFile = DefaultKeyFile;
            if (!File.Exists(keyFile))
            {
                Console.WriteLine("  The key file '" + Path.GetFullPath(keyFile) + "' does not exist.");
                Console.WriteLine("  Start Workshop 41 first in this folder, or copy its key file here.");
                Console.WriteLine("  Press ENTER to exit.");
                Console.ReadLine();
                return;
            }
            byte[] keyData = File.ReadAllBytes(keyFile);
            int length = policy.SigningKeyLength + policy.EncryptingKeyLength + policy.KeyNonceLength;
            if (keyData.Length != length)
                throw new InvalidOperationException($"The key file has {keyData.Length} bytes, the policy needs {length}.");
            Console.WriteLine("  Key file: " + Path.GetFullPath(keyFile));
            Console.WriteLine("  Key ID:   " + KeyId(keyData) + "   (must be the one the publisher shows)");
            Console.WriteLine();

            // -- Step 2: The message security, as on the publisher side -------------
            UaUadpSecurityConfiguration security = UaUadpSecurityConfiguration
                .WithConcatenatedKeyData(SecurityTokenId, policy, keyData)
                .SecurityMode(UaUadpSecurityMode.SignAndEncrypt)
                .Build();

            // -- Step 3: Configure the subscriber (as in Workshop 12) ---------------
            var config = UaSubscriberConfiguration.Build("SecureTemperatureSubscriber")
                .WithNetworkInterface(NetworkInterfaces.All)
                .WithTransport(PubSubTransportMode.DirectUnicast, "opc.udp://localhost:4840")
                .WithUadpSecurity(security)
                .AddDataSetReader("opcua:Workshop41", "SecureTemperatures", ds => ds
                    .AddField("Sensor1", BuiltInType.Double)
                    .AddField("Sensor2", BuiltInType.Double)
                    .AddField("Sensor3", BuiltInType.Double));

            using var subscriber = new UaSubscriber(LicenseUserName, LicenseSerial, config);

            Console.WriteLine("  License: " + subscriber.LicenceMessage);
            Console.WriteLine();

            int messageCount = 0;
            subscriber.DataReceived += (sender, e) =>
            {
                messageCount++;
                string keyFrame = e.IsKeyFrame ? "[KEY]" : "[DEL]";
                var fieldStr = string.Join("  ", e.Fields.Select(f => $"{f.Key}={f.Value.Value:F2}"));
                Console.WriteLine($"  [{messageCount:D5}] {keyFrame} {e.DataSetName} | {fieldStr}");
            };

            subscriber.Start();

            Console.WriteLine("  Secure subscriber started. Waiting for messages...");
            Console.WriteLine("  If nothing arrives: compare the key IDs, the SecurityTokenId and the policy.");
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

    // The same short key ID as in Workshop 41; it cannot be turned back into the key.
    static string KeyId(byte[] keyData)
    {
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(keyData);
        return BitConverter.ToString(hash, 0, 2) + "-" + BitConverter.ToString(hash, 2, 2);
    }
}
