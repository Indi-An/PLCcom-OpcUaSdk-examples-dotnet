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
// PLCcom OPC UA PubSub SDK - Workshop 45: Secure Subscriber, keys from the Key Server
//
// The receiving side of Workshop 44. Like the publisher, the subscriber logs in
// at the Key Server (Workshop 43), fetches the keys of the SecurityGroup
// 'WorkshopGroup' with GetSecurityKeys, and follows every change of the key.
//
// WHY THE SUBSCRIBER ASKS TOO:
//   Publisher and Subscriber never exchange a key with each other. Both ask the
//   key server for the keys of their group, so the key server is the one place
//   that decides who is in the group (OPC UA Part 14, 5.4.5.2). Take the role
//   'SecurityKeyServerAccess' away from a user, and from the next fetch on this
//   user can no longer join. Call ForceKeyRotation on the key server (any OPC UA
//   client with the role Admin can), and every member gets the new key without a
//   visit to any machine.
//
// OVERLAP:
//   The publisher switches to a new key at its own moment. For a short time the
//   subscriber sees messages with the previous or the next key (network delay,
//   different clocks). The key runtime keeps both ready, so no message is lost
//   at the change. You can watch the token number go up in the output.
//
// WHAT IF THE SUBSCRIBER CANNOT ASK?
//   A device without an OPC UA client cannot call GetSecurityKeys. For such
//   devices the key server can PUSH the keys instead: it calls the Method
//   SetSecurityKeys (Part 14, 9.1.3.3) on the device, which then needs an OPC UA
//   server of its own. In the SDK: UaSksServer.AddPushTarget on the key server
//   side and UaSksSetSecurityKeysReceiver on the device. This workshop uses the
//   normal way (pull), which is what an application with an OPC UA client does.
//
// TRY THIS:
//   * Start 43, 44 and 45 and watch the token change every 30 seconds.
//   * Start with the user 'admin' instead of 'reader' (change the code below):
//     the key server answers Bad_UserAccessDenied - an administrator is not a
//     member of the group by that.
//   * Stop Workshop 44: the subscriber shows nothing new, and no error.
//
// What you will learn:
//   * How a subscriber follows keys that change
//   * Why the key server is the one place that controls the membership
//   * What push is for (and where to look for it)
// ==============================================================================

using System;
using System.Linq;
using System.Text;
using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Client.Sdk;
using PLCcom.Opc.Ua.PubSub.Encoding.Security;
using PLCcom.Opc.Ua.PubSub.Sdk;

class Program
{
    const string KeyServerUrl = "opc.tcp://127.0.0.1:48440";
    const string GroupId = "WorkshopGroup";

    // The thumbprint of the key server certificate you answered 'y' for.
    static string s_trustedThumbprint;

    static void Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 45                      ║");
        Console.WriteLine("║  Secure Subscriber, keys from the Key Server                 ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Fetches its key from Workshop 43 and follows the changes.   ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Key server:  opc.tcp://127.0.0.1:48440                      ║");
        Console.WriteLine("║  Listening on: opc.udp://localhost:4840                      ║");
        Console.WriteLine("║  Start Workshop 44 (Publisher) to send data.                 ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        UaClient keyClient = null;
        UaSksKeyRefreshScheduler scheduler = null;
        try
        {
            string LicenseUserName = "";
            string LicenseSerial   = "";
            if (LicenseUserName.Length == 0) LicenseUserName = System.Environment.GetEnvironmentVariable("PLCCOM_OPCUA_LICENSE_USER") ?? "";
            if (LicenseSerial.Length == 0) LicenseSerial = System.Environment.GetEnvironmentVariable("PLCCOM_OPCUA_LICENSE_SERIAL") ?? "";

            // -- Step 1: Log in at the key server (encrypted channel, user 'reader') --
            string readerPassword = SecretFrom("PLCCOM_SKS_READER_PASSWORD", "Password of the user 'reader'");
            string pkiPassword    = SecretFrom("PLCCOM_PKI_PASSWORD", "Password for the private key of this program's certificate");
            Console.WriteLine();
            keyClient = ConnectToKeyServer(LicenseUserName, LicenseSerial, readerPassword, pkiPassword);
            Console.WriteLine("  Connected to the key server (SignAndEncrypt).");

            // -- Step 2: Fetch the first keys ------------------------------------------
            UaSksClient sks = UaSksClient.PublishSubscribe(new UaClientSksMethodCaller(keyClient));
            UaSksGetSecurityKeysRequest request = UaSksGetSecurityKeysRequest.Current(GroupId, 2);
            UadpSecurityKeyUpdate first = sks.GetSecurityKeys(request);
            Console.WriteLine($"  Keys received for '{GroupId}': first token {first.FirstTokenId}, {first.FutureKeys.Count} future key(s),");
            Console.WriteLine($"  key lifetime {first.KeyLifetimeMillis / 1000.0:F0} s.");
            Console.WriteLine();

            // -- Step 3: Keep the keys fresh -------------------------------------------
            var runtime = UaUadpSecurityKeyRuntime.FromUpdate(first);
            UaUadpSecurityConfiguration security = UaUadpSecurityConfiguration
                .WithKeySequence(first.Sequence)
                .SecurityMode(UaUadpSecurityMode.SignAndEncrypt)
                .Build();
            scheduler = new UaSksKeyRefreshScheduler(sks, runtime, request);
            scheduler.Start(1000, ex => Console.WriteLine("  Key refresh failed: " + ex.Message));

            // -- Step 4: The subscriber, as in Workshop 42, with the runtime -----------
            var config = UaSubscriberConfiguration.Build("SecureTemperatureSubscriber")
                .WithNetworkInterface(NetworkInterfaces.All)
                .WithTransport(PubSubTransportMode.DirectUnicast, "opc.udp://localhost:4840")
                .WithUadpSecurity(security, runtime)
                .AddDataSetReader("opcua:Workshop44", "SecureTemperatures", ds => ds
                    .AddField("Sensor1", BuiltInType.Double)
                    .AddField("Sensor2", BuiltInType.Double)
                    .AddField("Sensor3", BuiltInType.Double));

            using var subscriber = new UaSubscriber(LicenseUserName, LicenseSerial, config);
            Console.WriteLine("  License: " + subscriber.LicenceMessage);

            int messageCount = 0;
            long lastToken = -1;
            subscriber.DataReceived += (sender, e) =>
            {
                messageCount++;
                // The number of the key in use - the key itself is never shown.
                long token = CurrentToken(runtime);
                string change = token != lastToken && lastToken >= 0 ? "   <-- new key" : string.Empty;
                lastToken = token;
                string keyFrame = e.IsKeyFrame ? "[KEY]" : "[DEL]";
                var fieldStr = string.Join("  ", e.Fields.Select(f => $"{f.Key}={f.Value.Value:F2}"));
                Console.WriteLine($"  [{messageCount:D5}] token {token} {keyFrame} {e.DataSetName} | {fieldStr}{change}");
            };

            subscriber.Start();
            Console.WriteLine("  Secure subscriber started. Waiting for messages...");
            Console.WriteLine("  Press ENTER to stop.");
            Console.WriteLine();

            Console.ReadLine();

            subscriber.Stop();
            Console.WriteLine($"  Subscriber stopped. Received {messageCount} messages total.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Error: {ex.Message}");
            // Typical reasons: wrong password (the login is refused), a certificate that the
            // key server has not trusted yet (look in its pki/rejected/certs), a user without
            // the role SecurityKeyServerAccess (Bad_UserAccessDenied), a group that does not
            // exist (Bad_NotFound).
        }
        finally
        {
            scheduler?.Stop();
            if (keyClient != null)
            {
                try { keyClient.Disconnect(); } catch { }
            }
        }

        Console.WriteLine();
        Console.WriteLine("  Press ENTER to exit.");
        Console.ReadLine();
    }

    static long CurrentToken(UaUadpSecurityKeyRuntime runtime)
    {
        try { return runtime.GetCurrentPublisherKeyAtMillis(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).SecurityTokenId; }
        catch (Exception) { return -1; }   // no usable key right now
    }

    // Connects to the key server over a SignAndEncrypt endpoint, as the user 'reader'.
    static UaClient ConnectToKeyServer(string licenseUser, string licenseSerial, string readerPassword, string pkiPassword)
    {
        EndpointDescriptionCollection endpoints = UaClient.GetEndpoints(new Uri(KeyServerUrl), certificateValidator: AskForServerCertificate);
        EndpointDescription endpoint = UaClient.SortEndpointsBySecurityLevel(endpoints)
            .FirstOrDefault(e => e.SecurityMode == MessageSecurityMode.SignAndEncrypt);
        if (endpoint == null) throw new InvalidOperationException("The key server offers no SignAndEncrypt endpoint. Is Workshop 43 running?");

        string alias = System.Reflection.Assembly.GetEntryAssembly().GetName().Name;
        SessionConfiguration session = SessionConfiguration.Build(alias, endpoint);
        UaClientCertificate application = UaClientCertificate.Load("./pki", alias, pkiPassword);
        if (application == null || !application.CheckValidity())
            application = new UaClientCertificate("./pki", pkiPassword, alias, 365, "Indi.An GmbH").Build(overwrite: true);
        session.SetInstanceCertificate(application);
        session.Identity = new UserIdentity("reader", readerPassword);

        var client = new UaClient(licenseUser, licenseSerial, session);
        client.CertificateValidation += AskForServerCertificate;
        client.Connect();
        return client;
    }

    // The key server's certificate: shown once, accepted only if you say so.
    static void AskForServerCertificate(CertificateValidator sender, CertificateValidationEventArgs e)
    {
        if (s_trustedThumbprint != null && s_trustedThumbprint == e.Certificate.Thumbprint) { e.Accept = true; return; }
        Console.WriteLine();
        Console.WriteLine("  The key server presents this certificate:");
        Console.WriteLine("    Subject:    " + e.Certificate.Subject);
        Console.WriteLine("    Thumbprint: " + e.Certificate.Thumbprint);
        Console.Write("  Trust it? (y/N): ");
        string answer = Console.ReadLine();
        e.Accept = string.Equals(answer?.Trim(), "y", StringComparison.OrdinalIgnoreCase);
        if (e.Accept) s_trustedThumbprint = e.Certificate.Thumbprint;
    }

    // A secret comes from the environment variable, or you type it (the characters are not shown).
    static string SecretFrom(string variable, string prompt)
    {
        string value = Environment.GetEnvironmentVariable(variable);
        if (!string.IsNullOrEmpty(value)) return value;
        Console.Write("  " + prompt + ": ");
        var text = new StringBuilder();
        ConsoleKeyInfo key;
        do
        {
            key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Backspace && text.Length > 0) { text.Remove(text.Length - 1, 1); Console.Write("\b \b"); }
            else if (key.Key != ConsoleKey.Enter && key.Key != ConsoleKey.Backspace) { text.Append(key.KeyChar); Console.Write("*"); }
        } while (key.Key != ConsoleKey.Enter);
        Console.WriteLine();
        return text.ToString();
    }
}
