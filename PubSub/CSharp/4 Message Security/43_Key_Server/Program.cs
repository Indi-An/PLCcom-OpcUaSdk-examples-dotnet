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
// PLCcom OPC UA PubSub SDK - Workshop 43: Key Server (Security Key Service, SKS)
//
// In Workshop 41 and 42 both sides read the same key from a file. That works
// for two programs. It does not work for fifty machines, and it never changes
// the key. A Security Key Service (SKS) solves both problems (OPC UA Part 14,
// 5.4.5 and 8.2 to 8.8):
//
//   * The SKS is an OPC UA server. It keeps the keys of one or more
//     SecurityGroups. Publishers and Subscribers log in and call the Method
//     GetSecurityKeys (8.3.2) to get the keys of THEIR group.
//   * The keys change by themselves every KeyLifetime. The SecurityTokenId
//     counts up by one each time. Everybody who asks gets the current key and
//     the next ones, so nobody has to ask at the moment of the change.
//   * Who may get which keys is a question of ROLES: the role
//     SecurityKeyServerAccess may fetch keys, SecurityKeyServerAdmin manages
//     the groups (8.8). The server checks the role on every call.
//
// SAFE BY DEFAULT - what this program does and why:
//   * Only ENCRYPTED endpoints (SignAndEncrypt). GetSecurityKeys is refused on
//     any other channel with Bad_SecurityModeInsufficient.
//   * NO anonymous access. There are two users, 'reader' (role Access) and
//     'admin' (role Admin). A user with no key service role gets no key and
//     does not even learn which groups exist.
//   * NO password and NO key in the source code. The passwords come from the
//     environment variables PLCCOM_SKS_READER_PASSWORD and
//     PLCCOM_SKS_ADMIN_PASSWORD, or you type them (at least 10 characters).
//     The private key of the server certificate is protected by
//     PLCCOM_PKI_PASSWORD, or you type that too.
//   * Keys never appear on the screen or in a log. This program shows WHO
//     fetched WHAT (user, group, token number, result), never the key.
//   * The server listens on this computer only (127.0.0.1). To open it to the
//     network, change BaseAddresses on purpose, and put a firewall rule on it.
//
// TRUST: a client certificate is accepted only after YOU move it into the
// trusted folder. The first time a client connects, its certificate is filed
// in pki/rejected/certs and the client is refused. Compare the thumbprint,
// then move the file to pki/trusted/certs and let the client connect again.
//
// KEYS ARE KEPT IN MEMORY ONLY. When this program ends, the keys are gone, and
// after a restart the numbering starts again. The SDK can give you the state
// of a group to keep it in an encrypted store of your own (ExportState); this
// workshop leaves that out.
//
// What you will learn:
//   * How to put a Security Key Service on an OPC UA server (UaSksServer)
//   * How to create a SecurityGroup with a short key lifetime
//   * How roles and the encrypted channel protect the keys
//   * How to see who fetched keys (the KeysRetrieved event)
//
// Run Workshop 44 (Publisher) and 45 (Subscriber); they fetch their keys here.
// ==============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.PubSub.Sdk;
using PLCcom.Opc.Ua.Server;
using PLCcom.Opc.Ua.Server.Sdk;

class Program
{
    // The address of the key server and the name of the group the Workshops 44 and 45 use.
    const string Endpoint = "opc.tcp://127.0.0.1:48440";
    const string GroupId = "WorkshopGroup";

    // A short lifetime so you can watch the keys change: every 30 seconds the token goes up by one.
    // In real life this is hours or days.
    static readonly TimeSpan KeyLifetime = TimeSpan.FromSeconds(30);

    static void Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 43                      ║");
        Console.WriteLine("║  Key Server (Security Key Service)                           ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Hands out the keys of the SecurityGroup 'WorkshopGroup'.    ║");
        Console.WriteLine("║  Encrypted channel only, no anonymous access.                ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Address: opc.tcp://127.0.0.1:48440  (this computer only)    ║");
        Console.WriteLine("║  Start Workshop 44 (Publisher) and 45 (Subscriber).          ║");
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

            // -- Step 1: The secrets - from the environment or from you -------------
            string readerPassword = SecretFrom("PLCCOM_SKS_READER_PASSWORD", "Password for the user 'reader' (fetches keys)");
            string adminPassword  = SecretFrom("PLCCOM_SKS_ADMIN_PASSWORD",  "Password for the user 'admin' (manages groups)");
            string pkiPassword    = SecretFrom("PLCCOM_PKI_PASSWORD",        "Password that protects the private key of the server certificate");
            Console.WriteLine();

            // -- Step 2: Configure the OPC UA server --------------------------------
            // A Security Key Service lives on an ordinary OPC UA server of this SDK.
            var config = new UaServerConfiguration
            {
                ApplicationName = "PLCcom Workshop 43 - Key Server",
                ApplicationUri  = "urn:localhost:PLCcom:Workshop:43",
                ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
                NamespaceUri    = "http://indi-an.com/opcua/workshop/key-server",

                // 127.0.0.1 binds this one address only ("localhost" would bind all of them).
                BaseAddresses    = new List<string> { Endpoint },
                EndpointHostMode = EndpointHostMode.AsConfigured,

                // ONLY encrypted endpoints. A key must never travel over a channel that is
                // not encrypted. The service would refuse the call anyway; offering no weak
                // endpoint at all means a client cannot even try.
                SecurityPolicies = UaServer.GetRecommendedSecurityPolicies()
                    .Where(p => p.SecurityMode == MessageSecurityMode.SignAndEncrypt).ToList(),

                // NO Anonymous. Only a user with name and password can log in.
                UserTokenPolicies = new List<UserTokenPolicy>
                {
                    new UserTokenPolicy { TokenType = UserTokenType.UserName }
                },

                // A client certificate that is not in pki/trusted/certs is refused.
                AutoAcceptUntrustedCertificates = false,
                RegisterWithDiscoveryServer     = false,
                MaxSessionCount                 = 50,
                ShutdownDelay                   = 2
            };

            // The private key of the server certificate is protected with a password.
            // UaServerCertificate makes the certificate on the first start, in ./pki.
            var certificate = new UaServerCertificate("./pki", pkiPassword, "KeyServer", config.ApplicationUri, 365,
                "Indi.An GmbH", UaServerCertificate.CertificateRole.Application);
            certificate.LoadWithResultInPlace();
            if (!certificate.IsReady || !certificate.CheckValidity()) certificate.Build(overwrite: true);
            config.SetCertificateStore(UaServerCertificateStore.Load("./pki", new List<UaServerCertificate> { certificate }));

            // -- Step 3: Create the server and its two users ------------------------
            // The role decides what a user may do with the key service (Part 14, 8.8):
            //   UaSksRoles.Access  may fetch keys (GetSecurityKeys)
            //   UaSksRoles.Admin   may manage SecurityGroups, but gets no keys by that
            using var server = new UaServer(LicenseUserName, LicenseSerial);
            server.UserManager.AddUser("reader", readerPassword, UaSksRoles.Access);
            server.UserManager.AddUser("admin",  adminPassword,  UaSksRoles.Admin);

            Console.Write("  Starting the server ... ");
            server.Start(config);
            Console.WriteLine("OK");

            // -- Step 4: Put the key service on the server --------------------------
            // UaSksServer takes the standard object Server/PublishSubscribe of the server
            // (the well-known NodeIds of Part 14), so every client that follows the
            // standard finds GetSecurityKeys where it expects it.
            using var sks = new UaSksServer(LicenseUserName, LicenseSerial, server);

            // KeysRetrieved is raised for EVERY call of GetSecurityKeys, also for the
            // refused ones. It carries the user, the group, the token numbers and the
            // result - never a key.
            sks.KeysRetrieved += (s, e) =>
            {
                string result = StatusCode.IsGood(e.Result) ? "OK" : StatusCodes.GetBrowseName(e.Result.Code);
                Console.WriteLine($"  [{e.TimeUtc.ToLocalTime():HH:mm:ss}] fetch  user={e.UserName ?? "-"}  group={e.SecurityGroupId}  " +
                                  $"token={e.FirstTokenId} (+{Math.Max(0, e.KeyCount - 1)} future)  -> {result}");
            };

            // One SecurityGroup. Parameters: name, SecurityPolicyUri (null = the default,
            // Aes256-CTR), key lifetime, MaxFutureKeyCount (how many future keys one answer
            // carries at most), MaxPastKeyCount (how many old keys are kept for late
            // subscribers).
            sks.AddSecurityGroup(GroupId, null, KeyLifetime, 3, 3);

            sks.Start();

            Console.WriteLine("  Key service started.");
            Console.WriteLine("  Group:    " + GroupId + "  (key lifetime " + KeyLifetime.TotalSeconds + " s)");
            Console.WriteLine("  Address:  " + Endpoint);
            Console.WriteLine("  Users:    reader (fetches keys), admin (manages groups)");
            Console.WriteLine();
            Console.WriteLine("  A client certificate must be trusted first: move it from");
            Console.WriteLine("  pki/rejected/certs to pki/trusted/certs after you compared its thumbprint.");
            Console.WriteLine();
            Console.WriteLine("  Press ENTER to stop.");
            Console.WriteLine();

            // -- Step 5: Show the numbering of the keys -----------------------------
            // The group counts the token up each KeyLifetime, whether somebody asks or not.
            uint shown = 0;
            while (!(!Console.IsInputRedirected && Console.KeyAvailable))
            {
                uint token = sks.GetSecurityGroup(GroupId).CurrentTokenId;
                if (token != shown)
                {
                    Console.WriteLine($"  [{DateTime.Now:HH:mm:ss}] current token of '{GroupId}' is now {token}");
                    shown = token;
                }
                Thread.Sleep(500);
            }
            Console.ReadKey(true);
            Console.WriteLine();

            // -- Step 6: Stop -------------------------------------------------------
            sks.Stop();
            server.Stop();
            Console.WriteLine("  Key service stopped. The keys are gone with it.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Error: {ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("  Press ENTER to exit.");
        Console.ReadLine();
    }

    // A secret comes from the environment variable, or you type it (the characters are not shown).
    // At least 10 characters. Nothing is ever written to the screen or to a file.
    static string SecretFrom(string variable, string prompt)
    {
        string value = Environment.GetEnvironmentVariable(variable);
        if (!string.IsNullOrEmpty(value))
        {
            if (value.Length < 10) throw new InvalidOperationException(variable + " is shorter than 10 characters.");
            return value;
        }

        while (true)
        {
            Console.Write("  " + prompt + " (min. 10 characters): ");
            string typed = ReadHidden();
            if (typed.Length >= 10) return typed;
            Console.WriteLine("  Too short.");
        }
    }

    static string ReadHidden()
    {
        var text = new StringBuilder();
        ConsoleKeyInfo key;
        do
        {
            key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Backspace && text.Length > 0)
            {
                text.Remove(text.Length - 1, 1);
                Console.Write("\b \b");
            }
            else if (key.Key != ConsoleKey.Enter && key.Key != ConsoleKey.Backspace)
            {
                text.Append(key.KeyChar);
                Console.Write("*");
            }
        } while (key.Key != ConsoleKey.Enter);
        Console.WriteLine();
        return text.ToString();
    }
}
