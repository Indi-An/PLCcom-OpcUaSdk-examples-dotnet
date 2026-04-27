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
// PLCcom OPC UA Server SDK - Workshop 71: Reverse Connect
//
// In standard OPC UA, the CLIENT connects to the SERVER.
// With Reverse Connect, the SERVER connects to the CLIENT.
//
// Why use Reverse Connect?
//   * The server is behind a firewall that blocks incoming connections
//   * The server is in a protected network (OT/ICS) and the client is in IT/cloud
//   * The server has a dynamic IP address
//
// How it works:
//   1. The client opens a listening port (e.g. 48500)
//   2. The server periodically sends a ReverseHello message to the client
//   3. The client uses that connection to establish a normal OPC UA session
//   4. From the application's perspective, the session works exactly the same
//
// This server also keeps its normal endpoint (48460) for direct connections.
//
// What you will learn:
//   * How to add a reverse connection target to the server
//   * How the server periodically attempts to connect to the client
//   * How to use both normal and reverse connect simultaneously
//
// Normal endpoint:  opc.tcp://localhost:48410
// Reverse Connect:  -> opc.tcp://localhost:48500 (server connects to client)
// ==============================================================================

using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Server.Sdk;
using System;
using System.Collections.Generic;
using System.Threading;

// -- License -------------------------------------------------------------------
//TODO
//Submit your license information from your license e-mail
string LicenseUserName = "<Enter your UserName here>";
string LicenseSerial = "<Enter your Serial here>";

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 71: Reverse Connect     ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This example demonstrates:                                  ║");
Console.WriteLine("║  * Server initiates connection to client (firewall-safe)     ║");
Console.WriteLine("║  * ReverseHello message flow                                 ║");
Console.WriteLine("║  * Normal endpoint still available for direct connections    ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Use case: Server behind firewall, client in DMZ/cloud       ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// Use the machine hostname instead of 'localhost' in the base address.
// The OPC UA stack replaces 'localhost' with the real hostname when building
// EndpointDescriptions. For Reverse Connect this matters: the server sends
// its EndpointUrl in the ReverseHello message, and the client uses that URL
// to establish the session. If server and client resolve 'localhost' to
// different network adapters the connection will fail.
// Using the real hostname ensures both sides use the same network path.

var config = CreateConfig();
PrintConfig(config);

using var server = new UaServer(LicenseUserName, LicenseSerial);
server.CertificateValidation += (s, e) =>
{
    Console.WriteLine($"  [CERT] {e.Certificate.Subject} -> Accepted");
    e.Accept = true;
};

// Temporary: log all messages to see reverse connect activity
server.LogMessage += (s, e) =>
{
    Console.WriteLine($"  [{e.Level}] {e.Message}");
};

// Log session events to see when the reverse connection is established
server.SessionCreated += (s, e) =>
    Console.WriteLine($"\n  [SESSION+] {e.SessionName} from {e.ClientUri}");
server.SessionClosed += (s, e) =>
    Console.WriteLine($"\n  [SESSION-] {e.SessionName}");

server.ValuesWritten += (s, e) =>
{
    foreach (var item in e.Items)
        Console.WriteLine($"\n  << OPC Write: {item.Path} ({item.NodeId}) = {item.Value}");
};

Console.Write("Starting server ... ");
server.SetLogLevel(UaLogLevel.Debug);
try { server.Start(config); }
catch (Exception ex) { Console.WriteLine("FAILED"); Console.WriteLine(ex.Message); Console.ReadLine(); return; }
Console.WriteLine("OK");
Console.WriteLine();

// Create a variable to give the client something to read
var plant = server.CreateFolder("Plant", UaRolePermissions.WITHOUT_RESTRICTIONS);
var temp = server.CreateVariable<double>(plant, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 22.5);
temp.SetEURange(0, 100);
temp.SetEngineeringUnits("C");

// -- Add Reverse Connection ----------------------------------------------------
// AddReverseConnection() tells the server to periodically connect to this URL.
// The server will send a ReverseHello message and wait for the client to
// establish a session over that connection.
// timeout: how long to wait for the client to respond (milliseconds)
string clientUrl = "opc.tcp://localhost:48500";
server.AddReverseConnection(clientUrl, timeout: 30000);

Console.WriteLine($"  Normal endpoint:    opc.tcp://localhost:48410");
Console.WriteLine($"  Reverse Connect to: {clientUrl}");
Console.WriteLine();
Console.WriteLine("  The server will attempt to connect to the client every ~15 sec.");
Console.WriteLine("  Start a reverse-connect-capable client on port 48500 to test.");
Console.WriteLine("  (See Workshop ReverseConnect_Client for a matching client)");
Console.WriteLine();

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running with Reverse Connect enabled.             ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Normal endpoint (direct):                                   ║");
Console.WriteLine("║    opc.tcp://localhost:48410                                 ║");
Console.WriteLine("║    -> connect as usual, server is listening                  ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Reverse Connect endpoint:                                   ║");
Console.WriteLine("║    opc.tcp://localhost:48500                                 ║");
Console.WriteLine("║    -> the CLIENT must listen on this port                    ║");
Console.WriteLine("║    -> the SERVER connects to the client (not the other way)  ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  How to test with an OPC UA client that supports             ║");
Console.WriteLine("║  Reverse Connect:                                            ║");
Console.WriteLine("║    1. In the client, open 'Add Server' with Reverse Connect  ║");
Console.WriteLine("║       mode and enter: opc.tcp://localhost:48500              ║");
Console.WriteLine("║       The client will now LISTEN on port 48500               ║");
Console.WriteLine("║    2. This server sends a ReverseHello to port 48500         ║");
Console.WriteLine("║       every ~15 seconds                                      ║");
Console.WriteLine("║    3. The client receives the ReverseHello and establishes   ║");
Console.WriteLine("║       a normal OPC UA session over that connection           ║");
Console.WriteLine("║    4. Watch the [SESSION+] message appear here               ║");
Console.WriteLine("║    5. Browse and read Plant/Temperature as usual             ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to start value loop, CTRL+C to exit.            ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();

Console.WriteLine("Pushing values every second...");
var rng = new Random();
long cycle = 0;

while (true)
{
    cycle++;
    temp.Value = Math.Round(20.0 + rng.NextDouble() * 10.0, 1);
    Console.Write($"\r  Cycle={cycle}  Temperature={temp.Value:F1}C  ");
    Thread.Sleep(1000);
}

// =============================================================================
// Helper: CreateConfig
// =============================================================================
static UaServerConfiguration CreateConfig()
{
    return new UaServerConfiguration
    {
        ApplicationName = "PLCcom Workshop 71 - Reverse Connect",
        ApplicationUri = "urn:localhost:PLCcom:Workshop:71",
        ProductUri = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
        NamespaceUri = "http://indi-an.com/opcua/workshop/reverse-connect",

        // ── ServerStatus/BuildInfo ────────────────────────────────────────────
        ManufacturerName = "My Company GmbH",
        ProductName = "My OPC UA Server",
        SoftwareVersion = "1.0.0",
        BuildNumber = "42",
        // ── Endpoints ──────────────────────────────────────────────────────
        BaseAddresses = new List<string>
        {
            "opc.tcp://localhost:48410",
            "opc.https://localhost:48411"
        },

        // ── Security Policies ────────────────────────────────────────────────
        SecurityPolicies = UaServer.GetRecommendedSecurityPolicies(),

        // ── User Authentication ───────────────────────────────────────────────
        UserTokenPolicies = new List<UserTokenPolicy>
        {
            new UserTokenPolicy { TokenType = UserTokenType.Anonymous }
        },

        // ── PKI Certificate Store ─────────────────────────────────────────────
        CertificateStorePath        = @".\pki",
        CertificateLifetimeInMonths = 60,
        AutoAcceptUntrustedCertificates = false,

        // ── Endpoint Host Normalization ───────────────────────────────────────
        // AsConfigured (default) = endpoints use exactly the host from BaseAddresses
        // NormalizeToHostname    = replace localhost/127.0.0.1 with the machine name
        // None                   = no normalization, behavior depends on DNS and network settings
        EndpointHostMode = EndpointHostMode.AsConfigured,

        MaxSessionCount = 100,
        ShutdownDelay = 5,
        VendorName = "My Company GmbH",
        VendorProductName = "My OPC UA Server",
        VendorProductVersion = "1.0.0",
        MaxNodesPerRead = 1000,
        MaxNodesPerWrite = 1000,
        MaxNodesPerBrowse = 1000,
        MaxNodesPerHistoryReadData = 100,
        MaxNodesPerHistoryReadEvents = 100,
        MaxNodesPerHistoryUpdateData = 100,
        MaxNodesPerHistoryUpdateEvents = 100,
        MaxNodesPerMethodCall = 200,
        MaxNodesPerRegisterNodes = 1000,
        MaxNodesPerTranslateBrowsePathsToNodeIds = 1000,
        MaxNodesPerNodeManagement = 1000,
        MaxMonitoredItemsPerCall = 1000,
    };
}

// =============================================================================
// Helper: PrintConfig
// =============================================================================
static void PrintConfig(UaServerConfiguration config)
{
    Console.WriteLine("-- Active Server Configuration ------------------------------");
    Console.WriteLine("  ApplicationName  : " + config.ApplicationName);
    Console.WriteLine("  ApplicationUri   : " + config.ApplicationUri);
    Console.WriteLine("  NamespaceUri     : " + (config.NamespaceUri ?? "(default)"));
    Console.WriteLine("  ManufacturerName : " + (config.ManufacturerName ?? "(not set)"));
    Console.WriteLine("  ProductName      : " + (config.ProductName ?? "(not set)"));
    Console.WriteLine("  SoftwareVersion  : " + (config.SoftwareVersion ?? "(auto-detect)"));
    Console.WriteLine("  BuildNumber      : " + (config.BuildNumber ?? "(auto-detect)"));
    Console.WriteLine();
    Console.WriteLine("  Endpoints:");
    foreach (var addr in config.BaseAddresses) Console.WriteLine("    " + addr);
    Console.WriteLine();
        Console.WriteLine($"  EndpointHostMode : {config.EndpointHostMode}");
    Console.WriteLine("  VendorServerInfo:");
    Console.WriteLine("    VendorName=" + (config.VendorName ?? "(not set)") +
                      "  ProductName=" + (config.VendorProductName ?? "(not set)") +
                      "  Version=" + (config.VendorProductVersion ?? "(not set)"));
    Console.WriteLine();
    Console.WriteLine("  OperationLimits:");
    Console.WriteLine("    Read=" + config.MaxNodesPerRead + "  Write=" + config.MaxNodesPerWrite +
                      "  Browse=" + config.MaxNodesPerBrowse + "  Method=" + config.MaxNodesPerMethodCall);
    Console.WriteLine("    HistRD=" + config.MaxNodesPerHistoryReadData + "  HistRE=" + config.MaxNodesPerHistoryReadEvents +
                      "  HistUD=" + config.MaxNodesPerHistoryUpdateData + "  HistUE=" + config.MaxNodesPerHistoryUpdateEvents);
    Console.WriteLine("    Register=" + config.MaxNodesPerRegisterNodes +
                      "  Translate=" + config.MaxNodesPerTranslateBrowsePathsToNodeIds +
                      "  NodeMgmt=" + config.MaxNodesPerNodeManagement +
                      "  MonItems=" + config.MaxMonitoredItemsPerCall);
    Console.WriteLine("-------------------------------------------------------------");
    Console.WriteLine();
}
