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
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Required server: Server Workshop 12 (User Authentication)   ║");
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
            string LicenseSerial = "";

            // -- Step 1: Discover and select endpoint -----------------------------
            string serverUrl = "opc.tcp://localhost:48410";

            Console.WriteLine("  Server URL: " + serverUrl);
            Console.WriteLine("  Discovering endpoints...");
            Console.WriteLine();

            EndpointDescriptionCollection endpoints = UaClient.GetEndpoints(new Uri(serverUrl), certificateValidator: CertificateValidationHandler);
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

            // -- Step 2: Build SessionConfiguration with user credentials ---------
            // SessionConfiguration.Build() creates the configuration from the endpoint.
            SessionConfiguration sessionConfig = CreateConfig(endpoints[index]);
            PrintConfig(sessionConfig);

            // Set username/password authentication.
            // The UserIdentity is sent to the server during the ActivateSession call.
            // The server validates the credentials and assigns a role to the session.
            //
            // Server Workshop 12 defines three users:
            //   viewer   / viewer123   -> Role.Observer  (read-only)
            //   operator / operator123 -> Role.Operator  (read + write)
            //   admin    / admin123    -> Role.Engineer  (full access)
            //
            // The role only has effect if the server has set RolePermissions on its
            // nodes via SetRolePermissions(). Server Workshop 12 does this - try
            // connecting as viewer and writing a value to see BadUserAccessDenied.
            Console.Write("  Username: ");
            string username = Console.ReadLine();
            Console.Write("  Password: ");
            string password = ReadPassword();
            Console.WriteLine();
            sessionConfig.Identity = new UserIdentity(username, password);

            Console.WriteLine();

            // -- Step 3: Create client and register events ------------------------
            UaClient client = new UaClient(LicenseUserName, LicenseSerial, sessionConfig);
            Console.WriteLine("  License: " + client.GetLicenceMessage());
            Console.WriteLine();

            client.ServerConnected += (s, e) =>
                Console.WriteLine($"  [Connected] {DateTime.Now:HH:mm:ss} Session established");
            client.ServerConnectionLost += (s, e) =>
                Console.WriteLine($"  [ConnectionLost] {DateTime.Now:HH:mm:ss} Connection lost");
            client.KeepAlive += (session, e) => { };
            client.CertificateValidation += CertificateValidationHandler;

            // -- Step 4: Connect --------------------------------------------------
            // If the credentials are wrong, Connect() throws a ServiceResultException
            // with StatusCode BadIdentityTokenRejected or BadUserAccessDenied.
            Console.Write("  Connecting with user credentials ... ");
            client.Connect();
            Console.WriteLine("OK");
            Console.WriteLine($"  Session state: {client.GetSessionState()}");
            Console.WriteLine();

            // -- Step 5: Test permission-based access ----------------------------
            // Server 12b uses IUaPermissionValidator (custom logic, no RolePermissions):
            //   viewer   -> read only  (Write returns BadUserAccessDenied)
            //   operator -> read + write + call
            //   admin    -> full access
            //
            // Read Temperature - allowed for all users
            NodeId temperatureId = client.GetNodeIdByPath("Objects.Plant.Temperature");
            if (NodeId.IsNull(temperatureId))
            {
                Console.WriteLine("  Node Objects.Plant.Temperature not found - is Server 12b running?");
                Console.ReadLine();
                return;
            }
            object value = client.ReadValue(temperatureId);
            Console.WriteLine($"  Read  Temperature = {value}  -> OK");

            // Write Temperature - viewer gets BadUserAccessDenied
            StatusCode writeResult = client.WriteValue(temperatureId, 99.9);
            if (StatusCode.IsGood(writeResult))
                Console.WriteLine($"  Write Temperature = 99.9   -> OK");
            else
                Console.WriteLine($"  Write Temperature = 99.9   -> {StatusCodes.GetBrowseName(writeResult.Code)}");
            Console.WriteLine();

            // -- Step 6: Test method call -----------------------------------------
            // Call Reset - viewer gets BadUserAccessDenied
            NodeId plantId = client.GetNodeIdByPath("Objects.Plant");
            NodeId resetId = client.GetNodeIdByPath("Objects.Plant.Reset");
            try
            {
                client.Call(plantId, resetId);
                Console.WriteLine($"  Call   Reset              -> OK");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Call   Reset              -> {ex.Message}");
            }
            Console.WriteLine();

            // -- Step 7: Disconnect -----------------------------------------------
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
    static string ReadPassword()
    {
        var password = new System.Text.StringBuilder();
        ConsoleKeyInfo key;
        do
        {
            key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password.Remove(password.Length - 1, 1);
                Console.Write("\b \b");
            }
            else if (key.Key != ConsoleKey.Enter && key.Key != ConsoleKey.Backspace)
            {
                password.Append(key.KeyChar);
                Console.Write("*");
            }
        } while (key.Key != ConsoleKey.Enter);
        Console.WriteLine();
        return password.ToString();
    }

    // =============================================================================
    // Helper: CreateConfig
    // =============================================================================
    // Builds the SessionConfiguration for the selected endpoint.
    //
    // Certificate handling:
    //   Application certificate -- required for Sign / SignAndEncrypt endpoints.
    //   HTTPS certificate       -- required for opc.https:// endpoints (any SecurityMode).
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

        // HTTPS certificate -- required for opc.https:// endpoints, independent of SecurityMode.
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

        // Application certificate -- required for secured endpoints (Sign or SignAndEncrypt).
        // Not needed for SecurityMode.None (unencrypted connections).
        UaClientCertificate appCert = null;
        if (!endpoint.SecurityMode.Equals(MessageSecurityMode.None))
        {
            appCert = UaClientCertificate.Load("./pki", alias, "secretpassword");
            if (appCert == null || !appCert.CheckValidity())
                appCert = new UaClientCertificate("./pki", "secretpassword", alias, 720, "Indi.An GmbH")
                    .Build(overwrite: true);
        }

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
        Console.WriteLine("-- Active Client Configuration ------------------------------");
        if (config.Endpoint != null)
        {
            Console.WriteLine($"  Endpoint  : {config.Endpoint.EndpointUrl}");
            Console.WriteLine($"  Security  : {config.Endpoint.ToDisplayString()}");
        }
        Console.WriteLine($"  PKI Store : {(config.CertificateStorePath != null ? config.CertificateStorePath : "(not set)")}");
        Console.WriteLine($"  Cert File : {(config.ApplicationCertificateFullPath != null ? config.ApplicationCertificateFullPath : "(none -- SecurityMode.None)")}");
        Console.WriteLine("-------------------------------------------------------------");
        Console.WriteLine();
    }
}