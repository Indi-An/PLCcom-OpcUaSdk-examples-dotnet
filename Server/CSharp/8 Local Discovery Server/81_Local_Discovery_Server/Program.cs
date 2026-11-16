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
// PLCcom OPC UA Server SDK - Workshop 81: Local Discovery Server (LDS)
//
// A Local Discovery Server is the telephone book of a computer. Servers on the
// computer register with it, clients ask it which servers there are and where
// to connect to them (FindServers, GetEndpoints). The client no longer needs
// to know the address of every server.
//
// This program is a Local Discovery Server. It has no address space and no
// sessions, only the discovery services.
//
// What you will learn:
//   * How to start a Local Discovery Server (UaDiscoveryServer)
//   * How a server registers with it (two lines in the server's configuration)
//   * Why nothing is trusted by itself: you confirm each certificate
//   * How a client finds the registered servers
//
// Steps to try it:
//   1. Start this program. It listens on opc.tcp://localhost:4840/UADiscovery.
//      Another Local Discovery Server may already use port 4840 on your
//      computer; then start this program with:  81_Local_Discovery_Server --port 4850
//   2. Start a server that registers with it, for example Workshop 11
//      (Simple Server) with these two lines in its UaServerConfiguration:
//
//          RegisterWithDiscoveryServer = true,
//          DiscoveryServerUrl          = "opc.tcp://localhost:4840",   // your port
//
//      The server must trust the certificate of the Local Discovery Server:
//      copy .\lds\pki\own\certs\*.der of this program into the folder
//      pki\trusted\certs of the server. (The CertificateValidation handler
//      of Workshop 11 is not asked for the registration, so it does not
//      replace this step.)
//   3. This program reports the certificate of the server and asks whether you
//      trust it. Answer y. The server registers within a few seconds.
//   4. Run Client Workshop 11 (Discover Server) with the address of this
//      program: it lists the Local Discovery Server and your server.
//
// Registrations only come from servers on this computer, through a secured
// channel (SignAndEncrypt), and only from a server whose certificate names the
// application URI it registers. A server ends its registration when it stops.
// ==============================================================================

using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Server.Sdk;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

// -- License -------------------------------------------------------------------
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

// -- Port ----------------------------------------------------------------------
// 4840 is the well-known port of a Local Discovery Server. --port <number> uses another one.
int port = 4840;
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--port" && int.TryParse(args[i + 1], out int p) && p > 0 && p < 65536)
        port = p;
}

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 81: Local Discovery     ║");
Console.WriteLine("║  Server                                                      ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This example demonstrates:                                  ║");
Console.WriteLine("║  * A Local Discovery Server (FindServers, GetEndpoints)      ║");
Console.WriteLine("║  * Servers that register with it (RegisterServer2)           ║");
Console.WriteLine("║  * Trust confirmed by you, certificate by certificate        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// -- Configuration -------------------------------------------------------------
// The defaults are the ones the specification names: the well-known address,
// secured registration, registrations from this computer only, an application
// certificate that the Local Discovery Server creates under StoragePath\pki.
var config = new UaDiscoveryServerConfiguration
{
    ApplicationName = "PLCcom Workshop 81 - Local Discovery Server",
    ApplicationUri  = "urn:localhost:PLCcom:Workshop:81",
    BaseAddresses   = new List<string> { $"opc.tcp://localhost:{port}/UADiscovery" },

    // Where the Local Discovery Server keeps its files: the certificate folders
    // (pki\own, trusted, issuer, rejected) and registrations that are held by a semaphore file.
    StoragePath = @".\lds",

    // Registrations from other computers are refused (default). Set true only if you
    // want servers on other computers to register here.
    AcceptRegistrationsFromOtherHosts = false,

    // A server registers again at least every 10 minutes; the Local Discovery Server
    // waits a minute longer (660 s) before it removes a registration.
    RegistrationTimeoutSeconds = 660,
    MaxRegistrations           = 256
};

using var lds = new UaDiscoveryServer(LicenseUserName, LicenseSerial);

// Report what the Local Discovery Server does.
lds.LogMessage += (s, e) => Console.WriteLine($"  [{e.Level}] {e.Message}");
lds.RegistrationChanged += (s, e) =>
{
    var r = e.Registration;
    string names = r.ServerNames != null ? string.Join(", ", r.ServerNames) : "";
    string urls  = r.DiscoveryUrls != null ? string.Join(", ", r.DiscoveryUrls) : "";
    Console.WriteLine($"  [REGISTRATION] {e.Change}: {r.ServerUri} {names} {urls}"
                      + (e.Reason != null ? $"  ({e.Reason})" : ""));
};

// A server whose certificate is not trusted yet is refused and reported here. The handler
// only notes it; the question is asked from the main loop below, because this event comes
// from a thread of the OPC UA stack.
var unknown = new ConcurrentQueue<UaDiscoveryCertificateEventArgs>();
var asked   = new HashSet<string>();
lds.CertificateRejected += (s, e) => unknown.Enqueue(e);

Console.Write("Starting Local Discovery Server ... ");
try
{
    lds.Start(config);
}
catch (UaDiscoveryPortInUseException ex)
{
    Console.WriteLine("FAILED");
    Console.WriteLine(ex.Message);
    Console.WriteLine($"Start this program with another port, for example:  81_Local_Discovery_Server --port {port + 10}");
    return;
}
catch (Exception ex)
{
    Console.WriteLine("FAILED");
    Console.WriteLine(ex.Message);
    return;
}
Console.WriteLine("OK");
Console.WriteLine();
Console.WriteLine("  Address            : " + string.Join(", ", lds.EndpointUrls));
Console.WriteLine("  Certificate folders: " + lds.PkiPath);
Console.WriteLine();
Console.WriteLine("  A server registers with these two lines in its UaServerConfiguration:");
Console.WriteLine("      RegisterWithDiscoveryServer = true,");
Console.WriteLine($"      DiscoveryServerUrl          = \"opc.tcp://localhost:{port}\",");
Console.WriteLine();
Console.WriteLine("  Press Q to quit.");
Console.WriteLine();

// -- Main loop -----------------------------------------------------------------
// Confirm certificates, quit on Q or Ctrl+C.
bool quit = false;
Console.CancelKeyPress += (s, e) => { e.Cancel = true; quit = true; };
bool interactive = !Console.IsInputRedirected;

while (!quit)
{
    if (unknown.TryDequeue(out var cert))
    {
        // The server tries again and again until it is trusted: ask once per certificate.
        if (!asked.Add(cert.Thumbprint))
            continue;

        Console.WriteLine();
        Console.WriteLine("  A server wants to register, but its certificate is not trusted:");
        Console.WriteLine($"    Application URI: {cert.ApplicationUri ?? "(none)"}");
        Console.WriteLine($"    Subject        : {cert.Subject}");
        Console.WriteLine($"    Thumbprint     : {cert.Thumbprint}");

        if (!interactive)
        {
            Console.WriteLine("    (The input is redirected, so there is nobody to ask. Run the program in a console to confirm.)");
            continue;
        }

        Console.Write("  Do you know this server and trust its certificate? [y/N] ");
        var key = Console.ReadKey(true);
        Console.WriteLine(key.KeyChar);

        if (char.ToLowerInvariant(key.KeyChar) != 'y')
        {
            Console.WriteLine("  Not trusted. The registration stays refused.");
            continue;
        }

        // The refused certificate is filed on a background task of the stack; allow it a moment.
        bool trusted = false;
        for (int attempt = 0; attempt < 50 && !trusted; attempt++)
        {
            trusted = lds.TrustCertificate(cert.Thumbprint);
            if (!trusted) Thread.Sleep(100);
        }

        Console.WriteLine(trusted
            ? "  Trusted. The server registers with its next attempt (within a few seconds)."
            : "  The certificate was not found among the refused ones. Start the server again.");
        asked.Remove(cert.Thumbprint);    // if it comes back (for example after another failure), ask again
        continue;
    }

    if (interactive && Console.KeyAvailable)
    {
        var k = Console.ReadKey(true);
        if (char.ToLowerInvariant(k.KeyChar) == 'q')
            quit = true;
        continue;
    }

    Thread.Sleep(200);
}

Console.WriteLine();
Console.Write("Stopping ... ");
lds.Stop();
Console.WriteLine("OK");
