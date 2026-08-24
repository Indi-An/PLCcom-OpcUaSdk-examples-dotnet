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
// PLCcom OPC UA Client SDK - Workshop 14: Connect with Certificate Authentication
//
// Workshop 13 used username/password. For machine-to-machine communication,
// X.509 certificate authentication is more secure and does not require
// storing passwords. The client presents a certificate and the server
// validates it against its trusted certificate store.
//
// OPC UA supports three user identity types:
//   Anonymous   - no credentials (see Workshop 12)
//   UserName    - classic username + password (see Workshop 13)
//   Certificate - X.509 client certificate (this workshop)
//
// What you will learn:
//   * How to load or create an X.509 user certificate with UaClientCertificate
//   * How to set certificate-based UserIdentity on a session
//   * How certificate authentication differs from username/password
//   * How the server validates the user certificate
//
// Target server: opc.tcp://localhost:48410
// (Start Server Workshop 12 for a server that accepts certificate authentication)
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
        Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 14: Certificate Auth    ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  For machine-to-machine communication, X.509 certificate     ║");
        Console.WriteLine("║  authentication is more secure than username/password.       ║");
        Console.WriteLine("║  The client presents a certificate that the server validates ║");
        Console.WriteLine("║  against its trusted certificate store.                      ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  What you will learn:                                        ║");
        Console.WriteLine("║    * Load or create an X.509 user certificate                ║");
        Console.WriteLine("║    * Set certificate-based UserIdentity on a session         ║");
        Console.WriteLine("║    * Difference to username/password authentication          ║");
        Console.WriteLine("║                                                              ║");
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

            // -- Step 1: Discover and select endpoint -----------------------------
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

            // -- Step 2: Load or create the user certificate ----------------------
            // The user certificate identifies the user to the server.
            // It is separate from the application instance certificate (which
            // identifies the client application for the secure channel).
            // The server must trust this certificate — add it to its trusted store.
            var userCert = UaClientCertificate.Load("./pki", "PLCcom_Workshop_14_User", "secretpassword");
            if (userCert == null || !userCert.CheckValidity())
                userCert = new UaClientCertificate("./pki", "secretpassword", "PLCcom_Workshop_14_User", 720, "Indi.An GmbH")
                    .Build(overwrite: true);

            Console.WriteLine($"  User certificate: {userCert}");
            Console.WriteLine();

            // -- Step 3: Build SessionConfiguration with certificate identity -----
            // CreateConfig() builds the configuration and sets the application cert.
            // We then set the UserIdentity to use the user certificate.
            SessionConfiguration sessionConfig = CreateConfig(endpoints[index], userCert);
            PrintConfig(sessionConfig);

            // -- Step 4: Create client and register events ------------------------
            UaClient client = new UaClient(LicenseUserName, LicenseSerial, sessionConfig);
            Console.WriteLine("  License: " + client.GetLicenceMessage());
            Console.WriteLine();

            client.ServerConnected += (s, e) =>
                Console.WriteLine($"  [Connected] {DateTime.Now:HH:mm:ss} Session established");
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
            Console.Write("  Connecting with certificate ... ");
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
    // Sets the UserIdentity to use the provided user certificate.
    //
    // Certificate handling:
    //   Application certificate — required for Sign / SignAndEncrypt endpoints.
    //   HTTPS certificate       — required for opc.https:// endpoints.
    //   User certificate        — passed as UserIdentity for certificate-based auth.
    static SessionConfiguration CreateConfig(EndpointDescription endpoint, UaClientCertificate userCert)
    {
        string alias = System.Reflection.Assembly.GetEntryAssembly().GetName().Name;
        SessionConfiguration config = SessionConfiguration.Build(alias, endpoint);
        config.AutoConnect = false;

        // Set certificate-based user identity.
        // The server validates this certificate against its trusted user certificate store.
        config.Identity = new UserIdentity(userCert.GetCertificate());

        // HTTPS certificate — required for opc.https:// endpoints.
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

        // Application certificate — required for secured endpoints.
        UaClientCertificate appCert = null;
        if (!endpoint.SecurityMode.Equals(MessageSecurityMode.None))
        {
            appCert = UaClientCertificate.Load("./pki", alias, "secretpassword");
            if (appCert == null || !appCert.CheckValidity())
                appCert = new UaClientCertificate("./pki", "secretpassword", alias, 720, "Indi.An GmbH")
                    .Build(overwrite: true);
        }

        if (appCert != null && httpsCert != null)
            config.SetInstanceCertificate(appCert, httpsCert);
        else if (appCert != null)
            config.SetInstanceCertificate(appCert);

        return config;
    }

    // =============================================================================
    // Helper: PrintConfig
    // =============================================================================
    static void PrintConfig(SessionConfiguration config)
    {
        Console.WriteLine("── Active Client Configuration ──────────────────────────────────────────────");
        if (config.Endpoint != null)
        {
            Console.WriteLine($"  Endpoint  : {config.Endpoint.EndpointUrl}");
            Console.WriteLine($"  Security  : {config.Endpoint.ToDisplayString()}");
        }
        Console.WriteLine($"  Identity  : Certificate");
        Console.WriteLine($"  PKI Store : {(config.CertificateStorePath != null ? config.CertificateStorePath : "(not set)")}");
        Console.WriteLine($"  Cert File : {(config.ApplicationCertificateFullPath != null ? config.ApplicationCertificateFullPath : "(none — SecurityMode.None)")}");
        Console.WriteLine("─────────────────────────────────────────────────────────────────────────────");
        Console.WriteLine();
    }
}
