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
// PLCcom OPC UA Client SDK - Workshop 13: Connect with User Authentication
//
// Workshop 12 connected anonymously. Many production servers require
// username/password authentication. This workshop shows how to set
// user credentials on the SessionConfiguration before connecting.
//
// OPC UA supports three user identity types:
//   Anonymous   - no credentials (see Workshop 12)
//   UserName    - classic username + password (this workshop)
//   Certificate - X.509 client certificate (see Workshop 14)
//
// What you will learn:
//   * How to set username/password credentials on a session
//   * How UserIdentity is passed to the server during ActivateSession
//   * How to handle authentication failures
//
// Target server: opc.tcp://localhost:48410
// (Start Server Workshop 12 for a server that requires authentication)
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
        Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 13: User Authentication ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Many servers require username/password authentication.      ║");
        Console.WriteLine("║  This workshop shows how to set user credentials before      ║");
        Console.WriteLine("║  connecting to the server.                                   ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  What you will learn:                                        ║");
        Console.WriteLine("║    * Set username/password on SessionConfiguration           ║");
        Console.WriteLine("║    * UserIdentity is sent during ActivateSession             ║");
        Console.WriteLine("║    * Handle authentication failures                          ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        try
        {
            // -- License ----------------------------------------------------------
            // TODO: Replace with your license credentials from your license e-mail
            string LicenseUserName = "<Enter your UserName here>";
            string LicenseSerial = "<Enter your Serial here>";

            // -- Step 1: Discover and select endpoint -----------------------------
            string serverUrl = "opc.tcp://localhost:48410";

            Console.WriteLine("  Server URL: " + serverUrl);
            Console.WriteLine("  Discovering endpoints...");
            Console.WriteLine();

            EndpointDescriptionCollection endpoints = UaClient.GetEndpoints(new Uri(serverUrl), 60000);
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
                Console.WriteLine($"  [{i}] {UaClient.EndpointToString(endpoints[i])}");

            Console.WriteLine();
            Console.Write("  Please enter index of desired endpoint: ");
            string input = Console.ReadLine();

            if (!int.TryParse(input, out int index) || index < 0 || index >= endpoints.Count)
            {
                Console.WriteLine("  Invalid endpoint index.");
                Console.ReadLine();
                return;
            }

            // -- Step 2: Build SessionConfiguration with user credentials ---------
            // SessionConfiguration.Build() creates the configuration from the endpoint.
            SessionConfiguration sessionConfig = SessionConfiguration.Build(
                "PLCcom_Workshop_13", endpoints[index]);
            sessionConfig.AutoConnect = false;

            // Set username/password authentication.
            // The UserIdentity is sent to the server during the ActivateSession call.
            // The server validates the credentials and assigns a role.
            // TODO: Replace with valid credentials for your server
            sessionConfig.Identity = new UserIdentity("<username>", "<password>");

            Console.WriteLine();
            Console.WriteLine("  Certificate store: " + sessionConfig.CertificateStorePath);

            // -- Step 3: Create client and register events ------------------------
            UaClient client = new UaClient(LicenseUserName, LicenseSerial, sessionConfig);
            Console.WriteLine("  License: " + client.GetLicenceMessage());
            Console.WriteLine();

            client.ServerConnected += (s, e) =>
                Console.WriteLine($"  [Connected] {DateTime.Now:HH:mm:ss} Session established");
            client.ServerConnectionLost += (s, e) =>
                Console.WriteLine($"  [ConnectionLost] {DateTime.Now:HH:mm:ss} Connection lost");
            client.KeepAlive += (session, e) => { };
            client.CertificateValidation += (sender, e) => { e.Accept = true; };

            // -- Step 4: Connect --------------------------------------------------
            // If the credentials are wrong, Connect() throws a ServiceResultException
            // with StatusCode BadIdentityTokenRejected or BadUserAccessDenied.
            Console.Write("  Connecting with user credentials ... ");
            client.Connect();
            Console.WriteLine("OK");
            Console.WriteLine($"  Session state: {client.GetSessionState()}");
            Console.WriteLine();

            // -- Step 5: Disconnect -----------------------------------------------
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
}
