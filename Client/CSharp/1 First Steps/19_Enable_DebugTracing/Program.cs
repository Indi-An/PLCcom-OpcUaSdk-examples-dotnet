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
// PLCcom OPC UA Client SDK - Workshop 19: Enable Debug Tracing
//
// When troubleshooting OPC UA communication issues, the built-in trace
// system is invaluable. It logs all OPC UA stack activity to a file:
// service calls, security handshakes, errors and more.
//
// This workshop shows how to enable tracing before connecting. The trace
// file is written to the application's Logs folder and can be inspected
// with any text editor.
//
// What you will learn:
//   * How to create and configure a TraceConfiguration
//   * How to set the trace output file path
//   * How to control trace verbosity with TraceMasks
//   * How to bind the trace configuration to a session
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
        Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 19: Debug Tracing       ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  The built-in trace system logs all OPC UA stack activity    ║");
        Console.WriteLine("║  to a file: service calls, security handshakes, errors.      ║");
        Console.WriteLine("║  Essential for troubleshooting communication issues.         ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  What you will learn:                                        ║");
        Console.WriteLine("║    * Create and configure a TraceConfiguration               ║");
        Console.WriteLine("║    * Set the trace output file path                          ║");
        Console.WriteLine("║    * Control trace verbosity with TraceMasks                 ║");
        Console.WriteLine("║    * Bind the trace configuration to a session               ║");
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

            // -- Step 2: Configure tracing ----------------------------------------
            // TraceConfiguration controls what the OPC UA stack logs and where.
            // OutputFilePath: the log file location (created automatically)
            // DeleteOnLoad:   if true, the log file is cleared on each start
            // TraceMasks:     controls verbosity (Error, Information, Service, All)
            SessionConfiguration sessionConfig = CreateConfig(endpoints[index]);
            PrintConfig(sessionConfig);

            string logFile = AppDomain.CurrentDomain.BaseDirectory
                + "Logs\\" + System.Diagnostics.Process.GetCurrentProcess().ProcessName + ".trace.log";

            TraceConfiguration traceConfig = new TraceConfiguration();
            traceConfig.OutputFilePath = logFile;
            traceConfig.DeleteOnLoad = true;
            traceConfig.TraceMasks = Utils.TraceMasks.All;
            traceConfig.ApplyTraceSettings();

            // Bind the trace configuration to the session.
            // All OPC UA stack activity for this session will be logged.
            sessionConfig.TraceConfiguration = traceConfig;

            Console.WriteLine();
            Console.WriteLine("  Trace file: " + logFile);
            Console.WriteLine("  TraceMasks:  All (maximum verbosity)");
            Console.WriteLine();

            // -- Step 3: Connect and browse ---------------------------------------
            using (UaClient client = new UaClient(LicenseUserName, LicenseSerial, sessionConfig))
            {
                Console.WriteLine("  License: " + client.GetLicenceMessage());

                client.CertificateValidation += CertificateValidationHandler;
                client.ServerConnected += (s, e) =>
                    Console.WriteLine($"  [Connected] {DateTime.Now:HH:mm:ss}");
                client.ServerConnectionLost += (s, e) =>
                    Console.WriteLine($"  [ConnectionLost] {DateTime.Now:HH:mm:ss}");
                client.KeepAlive += (session, e) => { };

                Console.WriteLine();

                // Browse ObjectsFolder to generate some trace output
                // TODO: Adjust this path to match your server's address space
                string browsePath = "Objects.Plant.Line1.Machine1";
                Console.WriteLine($"  Browsing: {browsePath}");

                try
                {
                    NodeId sourceNode = client.GetNodeIdByPath(browsePath);

                    BrowseDescription nodeToBrowse = new BrowseDescription();
                    nodeToBrowse.NodeId = sourceNode;
                    nodeToBrowse.BrowseDirection = BrowseDirection.Forward;
                    nodeToBrowse.ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences;
                    nodeToBrowse.IncludeSubtypes = true;
                    nodeToBrowse.NodeClassMask = (uint)(NodeClass.Object | NodeClass.Variable);
                    nodeToBrowse.ResultMask = (uint)BrowseResultMask.All;

                    BrowseDescriptionCollection nodesToBrowse = new BrowseDescriptionCollection();
                    nodesToBrowse.Add(nodeToBrowse);

                    ReferenceDescriptionCollection results = client.BrowseFull(nodesToBrowse);

                    Console.WriteLine($"  {results.Count} child node(s) found.");
                    Console.WriteLine();

                    foreach (ReferenceDescription rd in results)
                    {
                        Console.WriteLine($"  {rd.DisplayName,-30} NodeId={rd.NodeId}  Class={rd.NodeClass}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("  Browse error: " + ex.Message);
                }

                Console.WriteLine();
                Console.WriteLine("  Check the trace file for detailed OPC UA stack logs:");
                Console.WriteLine("  " + logFile);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("  Error: " + ex.Message);
        }

        Console.WriteLine();
        Console.WriteLine("  Press ENTER to exit.");
        Console.ReadLine();
    }
    void CertificateValidationHandler(CertificateValidator sender, CertificateValidationEventArgs e)
    {
        // Called when the server presents its certificate - both during opc.https
        // discovery (TLS) and when a security policy other than None is used.
        // Inspect e.Certificate and e.Error, then set e.Accept accordingly.
        e.Accept = true;
        Console.WriteLine($"  [Certificate] Accepted: {e.Certificate.Subject}");
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