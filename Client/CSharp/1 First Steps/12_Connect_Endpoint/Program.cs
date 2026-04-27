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
// interactively. For secured endpoints the SDK creates an application
// instance certificate automatically.
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
// ==============================================================================

using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Client;
using PLCcom.Opc.Ua.Client.Sdk;
using System;

class Program
{
    static void Main(string[] args)
    {
        new Program().Start();
    }

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
            // TODO: Replace with your license credentials from your license e-mail
            string LicenseUserName = "<Enter your UserName here>";
            string LicenseSerial = "<Enter your Serial here>";

            // -- Step 1: Discover and sort endpoints ------------------------------
            // GetEndpoints() queries the server for all available endpoints.
            // SortEndpointsBySecurityLevel() puts the least secure (None) first,
            // making index 0 the easiest to connect to for testing.
            string serverUrl = "opc.tcp://localhost:48410";

            Console.WriteLine("  Server URL: " + serverUrl);
            Console.WriteLine("  Discovering endpoints...");
            Console.WriteLine();

            // Certificate validation is called when the server presents its certificate -
            // both during opc.https discovery (TLS) and during session connect when a
            // security policy other than None is used (opc.tcp and opc.https).
            // The same handler is reused for both GetEndpoints and the session below.
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
            // SessionConfiguration.Build() creates a configuration from the selected
            // endpoint. It sets up the application name, certificate store path and
            // security settings automatically.
            SessionConfiguration sessionConfig = SessionConfiguration.Build(
                "PLCcom_Workshop_12", endpoints[index]);

            // AutoConnect = false means we call Connect() explicitly below.
            // Set to true if you want the client to connect as soon as it is created.
            sessionConfig.AutoConnect = false;

            Console.WriteLine("  Certificate store: " + sessionConfig.CertificateStorePath);

            // -- Step 4: Create client and register events ------------------------
            // The UaClient manages the OPC UA session. Pass your license credentials
            // and the session configuration.
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

            // KeepAlive fires periodically to confirm the server is still alive.
            client.KeepAlive += (session, e) => { };

            // CertificateValidation is called when the server presents its certificate.
            // Accept all certificates for development. In production, verify against
            // a trusted certificate store.
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

    void CertificateValidationHandler(CertificateValidator sender, CertificateValidationEventArgs e)
    {
        // Called when the server presents its certificate - both during opc.https
        // discovery (TLS) and when a security policy other than None is used.
        // Inspect e.Certificate and e.Error, then set e.Accept accordingly.
        e.Accept = true;
        Console.WriteLine($"  [Certificate] Accepted: {e.Certificate.Subject}");
    }
}
