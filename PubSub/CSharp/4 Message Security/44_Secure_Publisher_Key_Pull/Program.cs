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
// PLCcom OPC UA PubSub SDK - Workshop 44: Secure Publisher, keys from the Key Server
//
// The publisher of Workshop 41, but without a key file: it logs in at the Key
// Server of Workshop 43 and fetches the key of its SecurityGroup with the
// Method GetSecurityKeys (OPC UA Part 14, 8.3.2; the publisher side is
// described in 5.4.5).
//
// HOW IT WORKS:
//   1. The publisher connects to the key server over an ENCRYPTED channel
//      (SignAndEncrypt) with the user 'reader'. The key server refuses every
//      other kind of channel.
//   2. It calls GetSecurityKeys for the group 'WorkshopGroup' with the
//      StartingTokenId 0 ("the current key") and asks for two future keys.
//   3. UaUadpSecurityKeyRuntime holds the keys and knows which one is current.
//   4. UaSksKeyRefreshScheduler fetches again before the keys run out
//      (Part 14 recommends half the KeyLifetime). When the key server changes the
//      key, the publisher switches to the new one by itself - you can watch the
//      token number go up in the output.
//
// NOTHING SECRET IN THE CODE:
//   The password of the user 'reader' comes from PLCCOM_SKS_READER_PASSWORD, the
//   password of the private key of this program's certificate from
//   PLCCOM_PKI_PASSWORD; or you type them. The output shows the token number
//   of the key in use, never the key.
//
// TRUST: the first time, the program shows you the certificate of the key server
// and asks whether to trust it. Compare the thumbprint with the one the key
// server's folder pki/own/certs holds before you answer 'y'. The key server in
// turn needs your certificate in its pki/trusted/certs (see Workshop 43).
//
// TRY THIS:
//   * Let it run for a minute: the token changes every 30 seconds.
//   * Start with a wrong password: the key server refuses the login.
//   * Stop Workshop 43 for a short moment: the publisher keeps sending with the
//     keys it has (it fetched future keys), and the refresh error shows in the
//     output.
//
// What you will learn:
//   * How to fetch keys with UaSksClient and the encrypted OPC UA channel
//   * How UaUadpSecurityKeyRuntime and UaSksKeyRefreshScheduler keep keys fresh
//   * How a publisher uses WithUadpSecurity(security, runtime)
//
// Run Workshop 45 (Secure Subscriber, keys from the Key Server) to receive the data.
// ==============================================================================

using System;
using System.Linq;
using System.Text;
using System.Threading;
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
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 44                      ║");
        Console.WriteLine("║  Secure Publisher, keys from the Key Server                  ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Fetches its key from Workshop 43 and follows the changes.   ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Key server: opc.tcp://127.0.0.1:48440                       ║");
        Console.WriteLine("║  Target:     opc.udp://localhost:4840                        ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        UaClient keyClient = null;
        UaSksKeyRefreshScheduler scheduler = null;
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

            // -- Step 1: Log in at the key server (encrypted channel, user 'reader') --
            string readerPassword = SecretFrom("PLCCOM_SKS_READER_PASSWORD", "Password of the user 'reader'");
            string pkiPassword    = SecretFrom("PLCCOM_PKI_PASSWORD", "Password for the private key of this program's certificate");
            Console.WriteLine();
            keyClient = ConnectToKeyServer(LicenseUserName, LicenseSerial, readerPassword, pkiPassword);
            Console.WriteLine("  Connected to the key server (SignAndEncrypt).");

            // -- Step 2: Fetch the first keys ------------------------------------------
            // UaClientSksMethodCaller carries the Method call over the session.
            // UaSksClient turns the answer into a UadpSecurityKeyUpdate.
            // Current(group, 2): the current key, and two future keys.
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
            // Every second the scheduler looks whether a fetch is due. A failed fetch is
            // reported here and tried again; the keys you have keep working until they end.
            scheduler.Start(1000, ex => Console.WriteLine("  Key refresh failed: " + ex.Message));

            // -- Step 4: The publisher, as in Workshop 41, with the runtime ------------
            var config = UaPublisherConfiguration.Build("SecureTemperaturePublisher", "opcua:Workshop44")
                .WithNetworkInterface(NetworkInterfaces.All)
                .WithTransport(PubSubTransportMode.DirectUnicast, "opc.udp://localhost:4840")
                .WithUadpSecurity(security, runtime)
                .WithPublishingInterval(1000)
                .AddDataSet("SecureTemperatures", ds => ds
                    .AddField("Sensor1", new NodeId(1001, 2))
                    .AddField("Sensor2", new NodeId(1002, 2))
                    .AddField("Sensor3", new NodeId(1003, 2))
                    .WithKeyFrameCount(10)
                    .WithInterval(1000));

            using var publisher = new UaPublisher(LicenseUserName, LicenseSerial, config);
            Console.WriteLine("  License: " + publisher.LicenceMessage);
            publisher.Start();
            Console.WriteLine("  Secure publisher started. Press ENTER to stop.");
            Console.WriteLine();

            var random = new Random();
            int counter = 0;
            long lastToken = -1;
            while (!(!Console.IsInputRedirected && Console.KeyAvailable))
            {
                counter++;
                double temp1 = 20.0 + random.NextDouble() * 10.0;
                double temp2 = 30.0 + random.NextDouble() * 5.0;
                double temp3 = 40.0 + random.NextDouble() * 5.0;
                publisher.WriteValue("SecureTemperatures", "Sensor1", temp1);
                publisher.WriteValue("SecureTemperatures", "Sensor2", temp2);
                publisher.WriteValue("SecureTemperatures", "Sensor3", temp3);

                // The number of the key in use - the key itself is never shown.
                long token = CurrentToken(runtime);
                string change = token != lastToken && lastToken >= 0 ? "   <-- new key" : string.Empty;
                lastToken = token;
                Console.WriteLine($"  [{counter:D5}] token {token}  Sensor1={temp1:F2}°C  Sensor2={temp2:F2}°C  Sensor3={temp3:F2}°C{change}");
                Thread.Sleep(1000);
            }

            Console.ReadKey(true);
            Console.WriteLine();
            publisher.Stop();
            Console.WriteLine("  Publisher stopped.");
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
