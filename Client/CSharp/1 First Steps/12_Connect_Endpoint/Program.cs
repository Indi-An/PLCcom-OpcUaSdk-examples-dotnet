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
// PLCcom OPC UA Client SDK - Workshop 12: Connect to Endpoint
//
// Demonstrates the full connect/disconnect lifecycle of a UaClient session.
// All available endpoints are discovered, displayed and the user selects one
// interactively. For secured endpoints an application instance certificate is
// created automatically on first run and reused on subsequent runs.
//
// What you will learn:
//   * How to discover and sort endpoints by security level
//   * How to select an endpoint interactively
//   * How to create a SessionConfiguration from an endpoint
//   * How to register event handlers (Connected, ConnectionLost, KeepAlive)
//   * How to handle server certificate validation
//   * How to connect and disconnect cleanly
//
// Target server: opc.tcp://localhost:48410
// (Start any Server SDK workshop first, e.g. Server Workshop 11)
// ==============================================================================

using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Client;
using PLCcom.Opc.Ua.Client.Sdk;
using System;

class Program
{
    static void Main(string[] args) => new Program().Start();

    void Start()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 12: Connect Endpoint    ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Connecting to an OPC UA server requires discovering its     ║");
        Console.WriteLine("║  endpoints first and selecting the right one. This workshop  ║");
        Console.WriteLine("║  shows the full connect/disconnect lifecycle.                ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  What you will learn:                                        ║");
        Console.WriteLine("║    * Discover and sort endpoints by security level           ║");
        Console.WriteLine("║    * Create a SessionConfiguration from an endpoint          ║");
        Console.WriteLine("║    * Register KeepAlive and ConnectionState events           ║");
        Console.WriteLine("║    * Handle server certificate validation                    ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Required server: Server Workshop 11 (Simple Server)         ║");
        Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        try
        {
            // -- License ----------------------------------------------------------
            // Important !!!!!!!!!!!!!!!!!!
            // Enter your Username + Serial here! Please note: with blank fields the library runs
            // for 15 minutes during a debug session. Both values can also come
            // from configuration or an environment variable.
            // Free trial license (14 days, uninterrupted): https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-download/
            string LicenseUserName = "";
            string LicenseSerial   = "";

            // -- Step 1: Discover and sort endpoints ------------------------------
            // GetEndpoints() queries the server for all available endpoints.
            // SortEndpointsBySecurityLevel() puts the least secure (None) first,
            // making index 0 the easiest to connect to for testing.
            string serverUrl = "opc.tcp://localhost:48410";

            Console.WriteLine("  Server URL: " + serverUrl);
            Console.WriteLine("  Discovering endpoints...");
            Console.WriteLine();

            EndpointDescriptionCollection endpoints = UaClient.GetEndpoints(
                new Uri(serverUrl), certificateValidator: CertificateValidationHandler);
            endpoints = UaClient.SortEndpointsBySecurityLevel(endpoints);

            if (endpoints.Count == 0)
            {
                Console.WriteLine("  No endpoints found. Is the server running?");
                Console.ReadLine();
                return;
            }

            // -- Step 2: Display endpoints and let user choose --------------------
            Console.WriteLine($"  {endpoints.Count} endpoint(s) found:");
            Console.WriteLine();
            for (int i = 0; i < endpoints.Count; i++)
                Console.WriteLine($"  [{i}] {endpoints[i].ToDisplayString()}");

            Console.WriteLine();
            Console.Write("  Please enter index of desired endpoint: ");
            string input = Console.ReadLine();

            if (!int.TryParse(input, out int index) || index < 0 || index >= endpoints.Count)
            {
                Console.WriteLine("  Invalid endpoint index.");
                Console.ReadLine();
                return;
            }

            Console.WriteLine();
            Console.WriteLine($"  Selected: {endpoints[index].ToDisplayString()}");
            Console.WriteLine();

            // -- Step 3: Build SessionConfiguration -------------------------------
            // CreateConfig() builds the SessionConfiguration for the selected endpoint.
            // It handles certificate creation/loading automatically based on the
            // endpoint's security mode and transport protocol.
            SessionConfiguration sessionConfig = CreateConfig(endpoints[index]);
            PrintConfig(sessionConfig);

            // -- Step 4: Create client and register events ------------------------
            UaClient client = new UaClient(LicenseUserName, LicenseSerial, sessionConfig);
            Console.WriteLine("  License: " + client.GetLicenceMessage());
            Console.WriteLine();

            // ServerConnected fires when the session is established.
            client.ServerConnected += (s, e) =>
                Console.WriteLine($"  [Connected] {DateTime.Now:HH:mm:ss} Session established");

            // ServerConnectionLost fires when the connection drops unexpectedly.
            // The SDK will attempt automatic reconnection.
            client.ServerConnectionLost += (s, e) =>
                Console.WriteLine($"  [ConnectionLost] {DateTime.Now:HH:mm:ss} Connection lost");

            client.KeepAlive += (session, e) => { };

            // Accept all client certificates automatically.
            // WARNING: Do NOT use this in production! Either implement your own validation
            // logic here (inspect e.Certificate and e.Error, then set e.Accept = true or false),
            // or remove this handler entirely -- the SDK will then automatically validate
            // certificates against the PKI trust store (pki/trusted/certs/).
            client.CertificateValidation += CertificateValidationHandler;

            // -- Step 5: Connect --------------------------------------------------
            Console.Write("  Connecting ... ");
            client.Connect();
            Console.WriteLine("OK");
            Console.WriteLine($"  Session state: {client.GetSessionState()}");
            Console.WriteLine();

            // -- Step 6: Disconnect -----------------------------------------------
            Console.WriteLine("  Press ENTER to disconnect and exit.");
            Console.ReadLine();

            if (client.GetSessionState() == SessionState.Connected)
                client.Disconnect();

            Console.WriteLine("  Disconnected.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("  Error: " + ex.Message);
            Console.WriteLine();
            Console.WriteLine("  Press ENTER to exit.");
            Console.ReadLine();
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    void CertificateValidationHandler(CertificateValidator sender, CertificateValidationEventArgs e)
    {
        // Called when the server presents its certificate during the secure channel
        // handshake. Inspect e.Certificate and e.Error, then set e.Accept accordingly.
        // For development we accept all certificates here.
        e.Accept = true;
        Console.WriteLine($"  [Certificate] Accepted: {e.Certificate.Subject}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // =============================================================================
    // Helper: CreateConfig
    // =============================================================================
    // Builds the SessionConfiguration for the selected endpoint.
    //
    // Certificate handling:
    //   Application certificate — required for Sign / SignAndEncrypt endpoints.
    //   HTTPS certificate       — required for opc.https:// endpoints (any SecurityMode).
    //
    // UaClientCertificate derives file paths automatically from the PKI base directory:
    //   pki/own/certs/<alias>.der    <- certificate
    //   pki/own/private/<alias>.pem  <- private key
    //
    // Load() returns null if the certificate does not exist yet or cannot be read.
    // Build(true) creates a new self-signed certificate, overwriting any existing file.
    static SessionConfiguration CreateConfig(EndpointDescription endpoint)
    {
        string alias = System.Reflection.Assembly.GetEntryAssembly().GetName().Name;
        SessionConfiguration config = SessionConfiguration.Build(alias, endpoint);
        config.AutoConnect = false;

        // HTTPS certificate — required for opc.https:// endpoints, independent of SecurityMode.
        // The hostname is extracted from the endpoint URL and used as the certificate alias.
        UaClientCertificate httpsCert = null;
        if (endpoint.EndpointUrl != null &&
            endpoint.EndpointUrl.StartsWith("opc.https://", StringComparison.OrdinalIgnoreCase))
        {
            string host = new Uri(endpoint.EndpointUrl).Host;
            httpsCert = UaClientCertificate.Load("./pki", host, "secretpassword");
            if (httpsCert == null || !httpsCert.CheckValidity())
                httpsCert = new UaClientCertificate("./pki", "secretpassword", host, 720, "Indi.An GmbH")
                    .Build(overwrite: true);
        }

        // Application certificate — required for secured endpoints (Sign or SignAndEncrypt).
        // Not needed for SecurityMode.None (unencrypted connections).
        UaClientCertificate appCert = null;
        if (!endpoint.SecurityMode.Equals(MessageSecurityMode.None))
        {
            appCert = UaClientCertificate.Load("./pki", alias, "secretpassword");
            if (appCert == null || !appCert.CheckValidity())
                appCert = new UaClientCertificate("./pki", "secretpassword", alias, 720, "Indi.An GmbH")
                    .Build(overwrite: true);
        }

        // Apply certificates to the configuration.
        // SetInstanceCertificate() sets CertificateStorePath and ApplicationCertificateFullPath.
        if (appCert != null && httpsCert != null)
            config.SetInstanceCertificate(appCert, httpsCert);
        else if (appCert != null)
            config.SetInstanceCertificate(appCert);

        return config;
    }

    // =============================================================================
    // Helper: PrintConfig
    // =============================================================================
    // Prints the active client configuration to the console so you can verify
    // all settings at a glance before connecting.
    static void PrintConfig(SessionConfiguration config)
    {
        Console.WriteLine("── Active Client Configuration ──────────────────────────────────────────────");
        if (config.Endpoint != null)
        {
            Console.WriteLine($"  Endpoint  : {config.Endpoint.EndpointUrl}");
            Console.WriteLine($"  Security  : {config.Endpoint.ToDisplayString()}");
        }
        Console.WriteLine($"  PKI Store : {(config.CertificateStorePath != null ? config.CertificateStorePath : "(not set)")}");
        Console.WriteLine($"  Cert File : {(config.ApplicationCertificateFullPath != null ? config.ApplicationCertificateFullPath : "(none — SecurityMode.None)")}");
        Console.WriteLine("─────────────────────────────────────────────────────────────────────────────");
        Console.WriteLine();
    }
}
