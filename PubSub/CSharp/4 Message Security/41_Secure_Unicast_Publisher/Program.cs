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
// PLCcom OPC UA PubSub SDK - Workshop 41: Secure UADP Publisher (fixed key)
//
// Workshop 11 sends its data in the clear. This publisher signs and encrypts
// every NetworkMessage (OPC UA Part 14, 7.2.4.4 "UADP message security"), so a
// third party on the network sees only ciphertext and cannot change a value
// without the receiver noticing.
//
// THE IDEA IN FOUR WORDS: group, policy, token, key.
//   * SecurityGroup  All publishers and subscribers that share one key belong to
//                    one SecurityGroup (Part 14, 5.4.5).
//   * SecurityPolicy The algorithms and key lengths. "PubSub-Aes256-CTR" needs
//                    68 bytes of key data: SigningKey (32) + EncryptingKey (32)
//                    + KeyNonce (4), one after the other (Part 14, 7.2.4.4.3,
//                    Table 155).
//   * SecurityTokenId The number of the key. It stands in the header of every
//                    message, so the receiver knows which key to use. A key
//                    that is replaced gets the next number.
//   * Key            Random bytes, the same on both sides.
//
// A FIXED KEY IS THE SIMPLEST START, NOT THE END OF THE ROAD:
//   Both sides must hold the same key from a file. Nobody replaces it, so it
//   never ends its life and every change means a visit to every machine.
//   Workshops 43 to 45 show the better way: a Security Key Service (SKS) hands
//   out keys that change by themselves.
//
// KEEP THE KEY OUT OF THE SOURCE CODE:
//   * The key is read from a FILE. The path comes from the environment variable
//     PLCCOM_PUBSUB_KEYFILE (default: pubsub-group.key next to the program).
//   * If the file does not exist, this program makes it with random bytes.
//     Workshop 42 reads the same file: run both in the same folder, or copy the
//     file once.
//   * Protect the file like a password: only the account that runs the program
//     may read it. Never check it in, never send it by e-mail, never write it
//     into a log. This program prints a short key ID (such as "3F9A-C210") so
//     you can see that both sides hold the same key, but never the key itself.
//
// What you will learn:
//   * How to turn on UADP message security (SignAndEncrypt)
//   * What the key data is made of and how long it must be
//   * Why a key belongs in a file or a key service, not in the code
//
// Run Workshop 42 (Secure UADP Subscriber) to receive the published data.
// ==============================================================================

using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.PubSub.Encoding.Security;
using PLCcom.Opc.Ua.PubSub.Sdk;

class Program
{
    const string KeyFileVariable = "PLCCOM_PUBSUB_KEYFILE";
    const string DefaultKeyFile = "pubsub-group.key";

    // The number of the key. Both sides must use the same one. A fixed key keeps it for good.
    const long SecurityTokenId = 1;

    static void Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 41                      ║");
        Console.WriteLine("║  Secure UADP Publisher (fixed key from a file)               ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Signs and encrypts every message (SignAndEncrypt).          ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Target: opc.udp://localhost:4840                            ║");
        Console.WriteLine("║  Start Workshop 42 (Subscriber) to receive the data.         ║");
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
            // Enter your license here, or set the environment variables PLCCOM_OPCUA_LICENSE_USER and PLCCOM_OPCUA_LICENSE_SERIAL.
            if (LicenseUserName.Length == 0) LicenseUserName = System.Environment.GetEnvironmentVariable("PLCCOM_OPCUA_LICENSE_USER") ?? "";
            if (LicenseSerial.Length == 0) LicenseSerial = System.Environment.GetEnvironmentVariable("PLCCOM_OPCUA_LICENSE_SERIAL") ?? "";

            // -- Step 1: Get the key ------------------------------------------------
            // The policy decides the algorithms and the length of the key data.
            UadpMessageSecurityPolicy policy = UadpMessageSecurityPolicy.PubSubAes256Ctr;
            string keyFile = Environment.GetEnvironmentVariable(KeyFileVariable);
            if (string.IsNullOrWhiteSpace(keyFile)) keyFile = DefaultKeyFile;
            byte[] keyData = LoadOrCreateKey(keyFile, policy);
            Console.WriteLine("  Key file: " + Path.GetFullPath(keyFile));
            Console.WriteLine("  Key ID:   " + KeyId(keyData) + "   (both sides must show the same ID)");
            Console.WriteLine();

            // -- Step 2: Describe the message security ------------------------------
            // WithConcatenatedKeyData: SecurityTokenId, policy and the key data
            // (SigningKey + EncryptingKey + KeyNonce). SignAndEncrypt signs the
            // whole message and encrypts the data. "Sign" alone would leave the
            // values readable.
            UaUadpSecurityConfiguration security = UaUadpSecurityConfiguration
                .WithConcatenatedKeyData(SecurityTokenId, policy, keyData)
                .SecurityMode(UaUadpSecurityMode.SignAndEncrypt)
                .Build();

            // -- Step 3: Configure the publisher (as in Workshop 11) ----------------
            // The only new line is WithUadpSecurity(). UADP message security works on
            // every UDP transport - unicast, multicast and broadcast - because it
            // protects the message, not a connection.
            var config = UaPublisherConfiguration.Build("SecureTemperaturePublisher", "opcua:Workshop41")
                .WithNetworkInterface(NetworkInterfaces.All)
                .WithTransport(PubSubTransportMode.DirectUnicast, "opc.udp://localhost:4840")
                .WithUadpSecurity(security)
                .WithPublishingInterval(1000)
                .AddDataSet("SecureTemperatures", ds => ds
                    .AddField("Sensor1", new NodeId(1001, 2))
                    .AddField("Sensor2", new NodeId(1002, 2))
                    .AddField("Sensor3", new NodeId(1003, 2))
                    .WithKeyFrameCount(10)
                    .WithInterval(1000));

            // -- Step 4: Create and start the publisher -----------------------------
            using var publisher = new UaPublisher(LicenseUserName, LicenseSerial, config);

            Console.WriteLine("  License: " + publisher.LicenceMessage);
            Console.WriteLine();

            publisher.Start();

            Console.WriteLine("  Secure publisher started. Publishing every 1000 ms.");
            Console.WriteLine("  Security mode: SignAndEncrypt, " + policy.SecurityPolicyUri);
            Console.WriteLine("  Press ENTER to stop.");
            Console.WriteLine();

            // -- Step 5: Simulate changing sensor values ----------------------------
            var random = new Random();
            int counter = 0;
            while (!(!Console.IsInputRedirected && Console.KeyAvailable))
            {
                counter++;
                double temp1 = 20.0 + random.NextDouble() * 10.0;
                double temp2 = 30.0 + random.NextDouble() * 5.0;
                double temp3 = 40.0 + random.NextDouble() * 5.0;
                publisher.WriteValue("SecureTemperatures", "Sensor1", temp1);
                publisher.WriteValue("SecureTemperatures", "Sensor2", temp2);
                publisher.WriteValue("SecureTemperatures", "Sensor3", temp3);

                Console.WriteLine($"  [{counter:D5}] Sensor1={temp1:F2}°C  Sensor2={temp2:F2}°C  Sensor3={temp3:F2}°C");
                Thread.Sleep(1000);
            }

            Console.ReadKey(true);
            Console.WriteLine();

            // -- Step 6: Stop the publisher -----------------------------------------
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

    // Reads the key from the file; makes the file with random bytes when it is not there.
    // The random generator of the operating system is the right source for a key - never a
    // pattern, a password or Random().
    static byte[] LoadOrCreateKey(string path, UadpMessageSecurityPolicy policy)
    {
        int length = policy.SigningKeyLength + policy.EncryptingKeyLength + policy.KeyNonceLength;
        if (File.Exists(path))
        {
            byte[] existing = File.ReadAllBytes(path);
            if (existing.Length != length)
                throw new InvalidOperationException($"The key file has {existing.Length} bytes, the policy needs {length}.");
            return existing;
        }

        byte[] created = new byte[length];
        using (var generator = RandomNumberGenerator.Create()) generator.GetBytes(created);
        File.WriteAllBytes(path, created);
        Console.WriteLine("  The key file did not exist: a new random key was made.");
        Console.WriteLine("  Protect it like a password. Workshop 42 needs the same file.");
        return created;
    }

    // A short identification of the key for the screen, like "3F9A-C210": the first four
    // bytes of a SHA-256 over the key. It cannot be turned back into the key.
    static string KeyId(byte[] keyData)
    {
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(keyData);
        return BitConverter.ToString(hash, 0, 2) + "-" + BitConverter.ToString(hash, 2, 2);
    }
}
