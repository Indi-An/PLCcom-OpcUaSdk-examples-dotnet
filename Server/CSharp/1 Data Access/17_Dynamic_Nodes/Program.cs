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
// PLCcom OPC UA Server SDK - Workshop 17: Dynamic Nodes
//
// In most OPC UA servers the address space is static - built once at startup.
// This SDK supports dynamic changes at runtime using the same API as at startup:
//   * CreateFolder / CreateVariable / CreateObject work before AND after Start()
//   * RemoveNode removes a node and all its children at any time
//   * Connected clients see changes immediately on their next browse
//
// This workshop demonstrates step by step — pause between each step to inspect
// the address space with your OPC UA client:
//
//   Step 1 — Initial address space (Plant/Line1/Temperature)
//   Step 2 — Add nodes at runtime (DynamicNodes folder with Counter + Message)
//   Step 3 — Remove a single node (Counter removed, Message stays)
//   Step 4 — Remove an entire subtree (DynamicNodes folder + all children)
//   Step 5 — Path-based lookup (GetNodeId, GetValue, SetValue)
//   Step 6 — Timer-based device discovery (new Device_N every 5 seconds)
//
// Connect with any OPC UA client to: opc.tcp://localhost:48410
// ==============================================================================

using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Server.Sdk;
using System;
using System.Reflection;
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

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 17: Dynamic Nodes       ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Demonstrates live address space changes at runtime.         ║");
Console.WriteLine("║  Use an OPC UA client to inspect the address space between   ║");
Console.WriteLine("║  each step.                                                  ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Endpoint: opc.tcp://localhost:48410                         ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

var config = CreateConfig();
PrintConfig(config);

using var server = new UaServer(LicenseUserName, LicenseSerial);

// Accept all client certificates automatically.
// WARNING: Do NOT use this in production! Either implement your own validation
// logic here (inspect e.Certificate and e.Error, then set e.Accept = true or false),
// or remove this handler entirely -- the SDK will then automatically validate
// certificates against the PKI trust store (pki/trusted/certs/).
server.CertificateValidation += (sender, e) => e.Accept = true;

server.SessionCreated += (s, e) =>
    Console.WriteLine($"  [SESSION+] {e.SessionName ?? "unknown"} from {e.ClientUri ?? "unknown"}");
server.SessionClosed += (s, e) =>
    Console.WriteLine($"  [SESSION-] {e.SessionName ?? "unknown"}");

// WriteValidation — called BEFORE any client write is committed to the address space.
// All internal checks (AccessLevel, DataType, Permissions) have already passed.
// Set item.StatusCode to any Bad_* value to reject that specific item.
//
// You can also MODIFY the value before it is written by setting item.Value.
// The modified value is then stored in the address space instead of the original.
//
// !! IMPORTANT — PERFORMANCE WARNING !!
// This handler runs synchronously on the server's write thread.
// Any blocking operation (device I/O, database, slow network) will stall
// the entire write request and can block other clients as well.
//
// If you need to forward the value to a device, prefer one of these patterns:
//   a) Accept immediately (Good) and forward asynchronously via Task.Run or a queue.
//      The OPC UA client gets a fast response; the device update happens in the background.
//   b) If you must wait for the device, always use a short timeout (e.g. 500 ms)
//      and return BadTimeout or BadNoCommunication if the device does not respond in time.
//
// Never await or block indefinitely inside this handler.
server.WriteValidation += (s, e) =>
{
    foreach (var item in e.Items)
    {
        // Example: accept immediately and forward to device asynchronously
        // Task.Run(() => plc.WriteValue(item.Path, item.Value));
        //
        // Example: forward synchronously with timeout, reject on failure
        // bool ok = plc.WriteValue(item.Path, item.Value, timeoutMs: 500);
        // if (!ok) item.StatusCode = StatusCodes.BadNoCommunication;
        item.StatusCode = StatusCodes.Good;
        Console.WriteLine($"  >> WriteValidation: {item.Path} = {item.Value}");
    }
};

// ValuesWritten — called AFTER a successful write. The client already received Good.
server.ValuesWritten += (s, e) =>
{
    foreach (var item in e.Items)
        Console.WriteLine($"  << Written: {item.Path} ({item.NodeId}) = {item.Value}");
};

Console.Write("Starting server ... ");
try { server.Start(config); }
catch (Exception ex) { Console.WriteLine("FAILED"); Console.WriteLine(ex.Message); Console.ReadLine(); return; }
Console.WriteLine("OK");
Console.WriteLine();

// =============================================================================
// Step 1: Initial address space
// =============================================================================
var plant = server.CreateFolder("Plant", UaRolePermissions.WITHOUT_RESTRICTIONS);
var line1 = server.CreateFolder(plant, "Line1", UaRolePermissions.WITHOUT_RESTRICTIONS);
var temp  = server.CreateVariable<double>(line1, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 22.0);

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Step 1/5 — Initial address space                            ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  The server is running with a static address space:          ║");
Console.WriteLine("║    Objects                                                   ║");
Console.WriteLine("║      └── Plant                                               ║");
Console.WriteLine("║            └── Line1                                         ║");
Console.WriteLine("║                  └── Temperature = 22.0                      ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Connect your OPC UA client and browse the address space.    ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to add new nodes at runtime.                    ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();

// =============================================================================
// Step 2: Add nodes at runtime
// =============================================================================
// CreateFolder and CreateVariable work exactly the same after Start() as before.
// Connected clients see the new nodes immediately on their next browse.
var dynFolder = server.CreateFolder(plant, "DynamicNodes", UaRolePermissions.WITHOUT_RESTRICTIONS);
var dynCounter = server.CreateVariable<int>(dynFolder, "Counter", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 42);
var dynMessage = server.CreateVariable<string>(dynFolder, "Message", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: "Hello");

Console.WriteLine($"  + Created: {dynCounter.Path} = {dynCounter.Value}");
Console.WriteLine($"  + Created: {dynMessage.Path} = {dynMessage.Value}");
Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Step 2/5 — Nodes added at runtime                           ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Two new nodes were added while the server was running:      ║");
Console.WriteLine("║    Objects                                                   ║");
Console.WriteLine("║      └── Plant                                               ║");
Console.WriteLine("║            ├── Line1/Temperature                             ║");
Console.WriteLine("║            └── DynamicNodes          ← NEW                   ║");
Console.WriteLine("║                  ├── Counter = 42    ← NEW                   ║");
Console.WriteLine("║                  └── Message = Hello ← NEW                   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Refresh your client browser — the new nodes are visible.    ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to remove the Counter node.                     ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();

// =============================================================================
// Step 3: Remove a single node
// =============================================================================
// RemoveNode removes exactly the specified node. Sibling nodes are unaffected.
bool removed = server.RemoveNode(dynCounter.NodeId);
Console.WriteLine($"  - Removed: {dynCounter.Path}  →  {(removed ? "OK" : "FAILED")}");
Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Step 3/5 — Single node removed                              ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Counter was removed. Message is still there:                ║");
Console.WriteLine("║    Objects                                                   ║");
Console.WriteLine("║      └── Plant                                               ║");
Console.WriteLine("║            ├── Line1/Temperature                             ║");
Console.WriteLine("║            └── DynamicNodes                                  ║");
Console.WriteLine("║                  └── Message = Hello  (Counter is gone)      ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Refresh your client — Counter has disappeared.              ║");
Console.WriteLine("║  Subscriptions on Counter now receive BadNodeIdUnknown.      ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to remove the entire DynamicNodes subtree.      ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();

// =============================================================================
// Step 4: Remove an entire subtree
// =============================================================================
// RemoveNode on a folder removes the folder AND all its children recursively.
removed = server.RemoveNode(dynFolder.NodeId);
Console.WriteLine($"  - Removed: {dynFolder.Path} (including all children)  →  {(removed ? "OK" : "FAILED")}");
Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Step 4/5 — Entire subtree removed                           ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  DynamicNodes folder and all remaining children are gone:    ║");
Console.WriteLine("║    Objects                                                   ║");
Console.WriteLine("║      └── Plant                                               ║");
Console.WriteLine("║            └── Line1                                         ║");
Console.WriteLine("║                  └── Temperature = 22.0                      ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  The address space is back to its initial state.             ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to demonstrate path-based lookup.               ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();

// =============================================================================
// Step 5: Path-based lookup
// =============================================================================
// GetNodeId / GetValue / SetValue let you access nodes by dot-separated path
// without storing the NodeId or UaVariable wrapper from creation time.
Console.WriteLine("-- Step 5/5: Path-based lookup -----------------------------------");

var nodeId = server.GetNodeId("Objects.Plant.Line1.Temperature");
Console.WriteLine($"  GetNodeId(\"Objects.Plant.Line1.Temperature\") = {nodeId}");

double currentVal = server.GetValue<double>("Objects.Plant.Line1.Temperature");
Console.WriteLine($"  GetValue  = {currentVal}");

server.SetValue("Objects.Plant.Line1.Temperature", 99.9);
Console.WriteLine($"  SetValue  → 99.9");
Console.WriteLine($"  GetValue  = {server.GetValue<double>("Objects.Plant.Line1.Temperature")}");
Console.WriteLine();

// =============================================================================
// Step 6: Timer-based device discovery
// =============================================================================
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Step 5/5 — Timer-based device discovery                     ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Every 5 seconds a new Device_N folder appears under Plant.  ║");
Console.WriteLine("║  After 5 devices the oldest is removed (sliding window).     ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Watch your OPC UA client — devices appear and disappear     ║");
Console.WriteLine("║  in real time without restarting the server.                 ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to start the simulation (CTRL+C to exit).       ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();

Console.WriteLine("Simulating device discovery...");
Console.WriteLine();

var rng = new Random();
int deviceNumber = 0;
var activeDevices = new Queue<(string Name, NodeId FolderId)>();
const int MaxDevices = 5;

while (true)
{
    deviceNumber++;
    string deviceName = $"Device_{deviceNumber}";

    var deviceFolder = server.CreateFolder(plant, deviceName, UaRolePermissions.WITHOUT_RESTRICTIONS);
    var devTemp      = server.CreateVariable<double>(deviceFolder, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: Math.Round(20.0 + rng.NextDouble() * 15.0, 1));
    var devStatus    = server.CreateVariable<string>(deviceFolder, "Status",      UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: "Online");

    activeDevices.Enqueue((deviceName, deviceFolder.NodeId));
    Console.WriteLine($"  + {deviceName}: Temp={devTemp.Value:F1}  Status={devStatus.Value}");

    if (activeDevices.Count > MaxDevices)
    {
        var oldest = activeDevices.Dequeue();
        server.RemoveNode(oldest.FolderId);
        Console.WriteLine($"  - Removed {oldest.Name} (sliding window, max={MaxDevices})");
    }

    Console.WriteLine($"    Active: {activeDevices.Count}/{MaxDevices}");
    Thread.Sleep(5000);
}

// =============================================================================
// Helper: CreateConfig
// =============================================================================
static UaServerConfiguration CreateConfig()
{
    var config = new UaServerConfiguration
    {
        // ── Application Identity ──────────────────────────────────────────────
        ApplicationName  = "PLCcom Workshop 17 - Dynamic Nodes",
        ApplicationUri   = "urn:localhost:PLCcom:Workshop:17",
        ProductUri       = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
        NamespaceUri     = "http://indi-an.com/opcua/workshop/dynamic-nodes",

        // ── ServerStatus/BuildInfo ────────────────────────────────────────────
        ManufacturerName = "My Company GmbH",
        ProductName      = "My OPC UA Server",
        SoftwareVersion  = "1.0.0",
        BuildNumber      = "42",
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
        AutoAcceptUntrustedCertificates = false,
        // ── Endpoint Host Normalization ───────────────────────────────────────
        // AsConfigured (default) = endpoints use exactly the host from BaseAddresses
        // NormalizeToHostname    = replace localhost/127.0.0.1 with the machine name
        // None                   = no normalization, behavior depends on DNS and network settings
        EndpointHostMode = EndpointHostMode.AsConfigured,
        MaxSessionCount = 100,
        ShutdownDelay   = 5,

        // ── VendorServerInfo ──────────────────────────────────────────────────
        VendorName           = "My Company GmbH",
        VendorProductName    = "My OPC UA Server",
        VendorProductVersion = "1.0.0",

        // ── OperationLimits ───────────────────────────────────────────────────
        MaxNodesPerRead                      = 1000,
        MaxNodesPerWrite                     = 1000,
        MaxNodesPerBrowse                    = 1000,
        MaxNodesPerHistoryReadData           = 100,
        MaxNodesPerHistoryReadEvents         = 100,
        MaxNodesPerHistoryUpdateData         = 100,
        MaxNodesPerHistoryUpdateEvents       = 100,
        MaxNodesPerMethodCall                = 200,
        MaxNodesPerRegisterNodes             = 1000,
        MaxNodesPerTranslateBrowsePathsToNodeIds = 1000,
        MaxNodesPerNodeManagement            = 1000,
        MaxMonitoredItemsPerCall             = 1000,
    };

    // -- PKI Certificate Store ------------------------------------------------
    // UaServerCertificateStore manages all server certificates.
    // Load() tries to load existing certificates from disk.
    // GetMissingOrExpired() returns all missing or expired certificates.
    // Build(true) creates a new self-signed certificate.
    var certs = new List<UaServerCertificate>
    {
        new UaServerCertificate(
            pkiBase:        @".\pki",
            password:       "secretpassword",
            alias:          Assembly.GetEntryAssembly().GetName().Name,
            applicationUri: config.ApplicationUri,
            validityDays:   720,
            organisation:   "Indi.An GmbH",
            role:           UaServerCertificate.CertificateRole.Application)
    };

    // One default HTTPS certificate for all opc.https ports. The SDK presents it at the
    // TLS handshake for any opc.https port that has no specifically assigned certificate.
    // To serve an official domain certificate on a port, create another HTTPS certificate
    // and assign it: config.AssignHttpsCertificateToPort(port, cert).
    var httpsDefault = new UaServerCertificate(
        pkiBase:        @".\pki",
        password:       "secretpassword",
        alias:          "https-default",
        applicationUri: "urn:https-default:https",
        validityDays:   720,
        organisation:   "Indi.An GmbH",
        role:           UaServerCertificate.CertificateRole.Https);
    certs.Add(httpsDefault);
    config.SetDefaultHttpsCertificate(httpsDefault);

    var store = UaServerCertificateStore.Load(@".\pki", certs);
    foreach (var missing in store.GetMissingOrExpired())
        missing.Build(overwrite: true);

    config.SetCertificateStore(store);

    return config;
}

// =============================================================================
// Helper: PrintConfig
// =============================================================================
static void PrintConfig(UaServerConfiguration config)
{
    Console.WriteLine("-- Active Server Configuration ------------------------------");
    Console.WriteLine($"  ApplicationName  : {config.ApplicationName}");
    Console.WriteLine($"  ApplicationUri   : {config.ApplicationUri}");
    Console.WriteLine($"  NamespaceUri     : {config.NamespaceUri ?? "(default: ApplicationUri + /nodes)"}");
    Console.WriteLine($"  ManufacturerName : {config.ManufacturerName ?? "(not set)"}");
    Console.WriteLine($"  ProductName      : {config.ProductName ?? "(not set)"}");
    Console.WriteLine($"  SoftwareVersion  : {config.SoftwareVersion ?? "(auto-detect)"}");
    Console.WriteLine($"  BuildNumber      : {config.BuildNumber ?? "(auto-detect)"}");
    Console.WriteLine();
    Console.WriteLine("  Endpoints:");
    foreach (var addr in config.BaseAddresses)
        Console.WriteLine($"    {addr}");
    Console.WriteLine();
    Console.WriteLine($"  EndpointHostMode : {config.EndpointHostMode}");
    Console.WriteLine();
    Console.WriteLine("  Certificate Store:");
    if (config.CertificateStore != null)
        Console.WriteLine($"    {config.CertificateStore}");
    else
        Console.WriteLine("    (not set)");
    Console.WriteLine();
    Console.WriteLine("  VendorServerInfo (Server/VendorServerInfo):");
    Console.WriteLine($"    VendorName           = {config.VendorName ?? "(not set)"}");
    Console.WriteLine($"    VendorProductName    = {config.VendorProductName ?? "(not set)"}");
    Console.WriteLine($"    VendorProductVersion = {config.VendorProductVersion ?? "(not set)"}");
    Console.WriteLine();
    Console.WriteLine("  OperationLimits (Server/ServerCapabilities/OperationLimits):");
    Console.WriteLine($"    MaxNodesPerRead                          = {config.MaxNodesPerRead}");
    Console.WriteLine($"    MaxNodesPerWrite                         = {config.MaxNodesPerWrite}");
    Console.WriteLine($"    MaxNodesPerBrowse                        = {config.MaxNodesPerBrowse}");
    Console.WriteLine($"    MaxNodesPerHistoryReadData               = {config.MaxNodesPerHistoryReadData}");
    Console.WriteLine($"    MaxNodesPerHistoryReadEvents             = {config.MaxNodesPerHistoryReadEvents}");
    Console.WriteLine($"    MaxNodesPerHistoryUpdateData             = {config.MaxNodesPerHistoryUpdateData}");
    Console.WriteLine($"    MaxNodesPerHistoryUpdateEvents           = {config.MaxNodesPerHistoryUpdateEvents}");
    Console.WriteLine($"    MaxNodesPerMethodCall                    = {config.MaxNodesPerMethodCall}");
    Console.WriteLine($"    MaxNodesPerRegisterNodes                 = {config.MaxNodesPerRegisterNodes}");
    Console.WriteLine($"    MaxNodesPerTranslateBrowsePathsToNodeIds = {config.MaxNodesPerTranslateBrowsePathsToNodeIds}");
    Console.WriteLine($"    MaxNodesPerNodeManagement                = {config.MaxNodesPerNodeManagement}");
    Console.WriteLine($"    MaxMonitoredItemsPerCall                 = {config.MaxMonitoredItemsPerCall}");
    Console.WriteLine("-------------------------------------------------------------");
    Console.WriteLine();
}
