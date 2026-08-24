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
// PLCcom OPC UA Client SDK - Workshop 15: Browse by NodeId
//
// Browsing is how you explore the OPC UA address space. Starting from a
// known NodeId (e.g. the ObjectsFolder i=85), you request all child
// references and discover what the server exposes.
//
// This workshop browses from the ObjectsFolder using two reference types:
//   Aggregates - finds HasComponent, HasProperty and similar references
//   Organizes  - finds folder-like organizational references
//
// What you will learn:
//   * How to construct a BrowseDescription with filters
//   * How to browse from a known NodeId (ObjectsFolder = i=85)
//   * How to read NodeId, NodeClass, BrowseName and DisplayName
//   * How BrowseFull handles continuation points automatically
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
        Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 15: Browse by NodeId    ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Browsing is how you explore the OPC UA address space.       ║");
        Console.WriteLine("║  Starting from a known NodeId, you request all child         ║");
        Console.WriteLine("║  references to discover what the server exposes.             ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  What you will learn:                                        ║");
        Console.WriteLine("║    * Construct a BrowseDescription with filters              ║");
        Console.WriteLine("║    * Browse from ObjectsFolder (i=85)                        ║");
        Console.WriteLine("║    * Read NodeId, NodeClass, BrowseName, DisplayName         ║");
        Console.WriteLine("║    * BrowseFull handles continuation points automatically    ║");
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

            // -- Step 2: Connect --------------------------------------------------
            SessionConfiguration sessionConfig = CreateConfig(endpoints[index]);
            PrintConfig(sessionConfig);

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

                // -- Step 3: Build browse request ----------------------------------
                // Start from the ObjectsFolder (NodeId i=85, namespace 0).
                // This is the root of all application-defined nodes.
                NodeId sourceNode = new NodeId(85, 0);

                // BrowseDescription 1: find all components (HasComponent, HasProperty)
                BrowseDescription nodeToBrowse1 = new BrowseDescription();
                nodeToBrowse1.NodeId = sourceNode;
                nodeToBrowse1.BrowseDirection = BrowseDirection.Forward;
                nodeToBrowse1.ReferenceTypeId = ReferenceTypeIds.Aggregates;
                nodeToBrowse1.IncludeSubtypes = true;
                nodeToBrowse1.NodeClassMask = (uint)(NodeClass.Object | NodeClass.Variable);
                nodeToBrowse1.ResultMask = (uint)BrowseResultMask.All;

                // BrowseDescription 2: find all organized children (Organizes)
                BrowseDescription nodeToBrowse2 = new BrowseDescription();
                nodeToBrowse2.NodeId = sourceNode;
                nodeToBrowse2.BrowseDirection = BrowseDirection.Forward;
                nodeToBrowse2.ReferenceTypeId = ReferenceTypeIds.Organizes;
                nodeToBrowse2.IncludeSubtypes = true;
                nodeToBrowse2.NodeClassMask = (uint)(NodeClass.Object | NodeClass.Variable);
                nodeToBrowse2.ResultMask = (uint)BrowseResultMask.All;

                BrowseDescriptionCollection nodesToBrowse = new BrowseDescriptionCollection();
                nodesToBrowse.Add(nodeToBrowse1);
                nodesToBrowse.Add(nodeToBrowse2);

                // -- Step 4: Execute browse and display results --------------------
                // BrowseFull() handles continuation points automatically.
                // If the server returns more results than fit in one response,
                // BrowseFull sends BrowseNext requests until all results are collected.
                Console.WriteLine($"  Browsing ObjectsFolder (i=85)...");
                Console.WriteLine();

                ReferenceDescriptionCollection results = client.BrowseFull(nodesToBrowse);

                if (results.Count > 0)
                {
                    Console.WriteLine($"  {results.Count} child node(s) found:");
                    Console.WriteLine();

                    foreach (ReferenceDescription rd in results)
                    {
                        Console.WriteLine($"  {rd.DisplayName,-30} NodeId={rd.NodeId}  Class={rd.NodeClass}  BrowseName={rd.BrowseName}");
                    }
                }
                else
                {
                    Console.WriteLine("  No child nodes found.");
                }
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