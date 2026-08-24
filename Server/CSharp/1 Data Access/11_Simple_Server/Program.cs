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
// PLCcom OPC UA Server SDK - Workshop 11: Simple Server
//
// The starting point for all server workshops. This example creates a fully
// functional OPC UA server that any compliant client can connect to, browse,
// read, write and subscribe to.
//
// The key concepts demonstrated here form the foundation for every OPC UA
// server application:
//
//   1. Configuration — set up endpoints, security and certificates
//   2. Address space — create folders and variables that clients can see
//   3. Data types    — each variable has a specific OPC UA data type
//   4. Value push    — update values from code; subscribed clients are
//                      notified automatically (no polling needed)
//   5. Client writes — react to values written by OPC UA clients
//
// The address space built here is intentionally simple:
//   Objects
//     └─ Plant
//         └─ Line1
//             └─ Machine1
//                 ├─ Temperature   (Double)     = 21.5
//                 ├─ Pressure      (Float)      = 1.013
//                 ├─ RPM           (Int32)      = 1500
//                 ├─ IsRunning     (Boolean)    = true
//                 ├─ Status        (String)     = "Idle"
//                 ├─ LastUpdate    (DateTime)   = now
//                 ├─ SerialNumber  (String)     = "SN-2025-001"  [ReadOnly]
//                 └─ Setpoints     (Double[])   = [20, 25, 30]
//
// What you will learn:
//   • How to configure and start an OPC UA server
//   • How to create a folder hierarchy in the address space
//   • How to create scalar and array variables of different data types
//   • How to mark a variable as read-only
//   • How to react when an OPC UA client writes a value (ValuesWritten)
//   • How to push value changes to subscribed clients from a background loop
//   • How to use the Path property to identify nodes by their browse path
//
// Connect with any OPC UA client to: opc.tcp://localhost:48410
//                                or: opc.https://localhost:48411
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
string LicenseSerial = "";

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 11: Simple Server       ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This example creates a minimal OPC UA server with:          ║");
Console.WriteLine("║    * Folder hierarchy  (Plant -> Line1 -> Machine1)          ║");
Console.WriteLine("║    * Scalar variables  (Double, Float, Int, Bool, String)    ║");
Console.WriteLine("║    * Array variable    (Double[])                            ║");
Console.WriteLine("║    * Read-only variable (SerialNumber)                       ║");
Console.WriteLine("║    * Client write notifications (ValuesWritten event)        ║");
Console.WriteLine("║    * Continuous value push loop (1-second interval)          ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Every node created here is immediately visible to any       ║");
Console.WriteLine("║  connected OPC UA client. The Path property shows the        ║");
Console.WriteLine("║  dot-separated browse path from the Objects root.            ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// =============================================================================
// Step 1: Configure the server
// =============================================================================
// All server settings are defined in CreateConfig() below.
// See that function for a full description of every available option.
var config = CreateConfig();
PrintConfig(config);

// =============================================================================
// Step 2: Create the server and wire up events
// =============================================================================
using var server = new UaServer(LicenseUserName, LicenseSerial);

// Accept all client certificates automatically.
// WARNING: Do NOT use this in production! Either implement your own validation
// logic here (inspect e.Certificate and e.Error, then set e.Accept = true or false),
// or remove this handler entirely -- the SDK will then automatically validate
// certificates against the PKI trust store (pki/trusted/certs/).
server.CertificateValidation += (sender, e) => e.Accept = true;


// WriteValidation — called BEFORE any client write is committed to the address space.
// All internal checks (AccessLevel, DataType, Permissions) have already passed.
// The handler receives ALL items of the write request as a batch.
// Set item.StatusCode to any Bad_* value to reject that specific item.
// If not handled or StatusCode remains Good, the write proceeds normally.
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
// Use this for logging, synchronization, or triggering side effects.
// Note: If WriteValidation rejects an item, ValuesWritten does NOT fire for that item.
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
// Step 3: Build the address space
// =============================================================================
// The address space is the tree of nodes that clients can browse.
// Folders organize the structure, Variables hold the actual data.
// All nodes are immediately visible to connected clients.
//
// Every node has a Path property — the dot-separated browse path from root.
// Example: "Objects.Plant.Line1.Machine1.Temperature"
// This path is useful for logging, debugging and the server.Read/Write API.
Console.WriteLine("── Building address space ───────────────────────────────────");

// Create a folder hierarchy: Objects -> Plant -> Line1 -> Machine1
var plant   = server.CreateFolder("Plant", UaRolePermissions.WITHOUT_RESTRICTIONS);
var line1   = server.CreateFolder(plant, "Line1", UaRolePermissions.WITHOUT_RESTRICTIONS);
var machine = server.CreateFolder(line1, "Machine1", UaRolePermissions.WITHOUT_RESTRICTIONS);

Console.WriteLine($"  Folder    {plant.Path,-40} {plant.NodeId}");
Console.WriteLine($"  Folder    {line1.Path,-40} {line1.NodeId}");
Console.WriteLine($"  Folder    {machine.Path,-40} {machine.NodeId}");

// Create scalar variables — each has a specific OPC UA data type.
// The generic type parameter <T> determines the DataType attribute:
//   double -> Double, float -> Float, int -> Int32, bool -> Boolean,
//   string -> String, DateTime -> DateTime
var temperature = server.CreateVariable<double>(machine, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 21.5);
var pressure    = server.CreateVariable<float>(machine, "Pressure", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 1.013f);
var rpm         = server.CreateVariable<int>(machine, "RPM", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 1500);
var running     = server.CreateVariable<bool>(machine, "IsRunning", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: true);
var status      = server.CreateVariable<string>(machine, "Status", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: "Idle");
var lastUpdate  = server.CreateVariable<DateTime>(machine, "LastUpdate", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: DateTime.UtcNow);

// Read-only variable: clients can read but not write.
// The server returns BadNotWritable on any write attempt.
var serialNo = server.CreateVariable<string>(machine, "SerialNumber",
    UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: "SN-2025-001", readOnly: true);

// Array variable: ValueRank is automatically set to OneDimension.
// Clients see a Double[] value with 3 elements.
var setpoints = server.CreateArrayVariable<double>(machine, "Setpoints",
    initialValue: new double[] { 20.0, 25.0, 30.0 });

Console.WriteLine($"  Double    {temperature.Path,-40} {temperature.NodeId}  = 21.5");
Console.WriteLine($"  Float     {pressure.Path,-40} {pressure.NodeId}  = 1.013");
Console.WriteLine($"  Int32     {rpm.Path,-40} {rpm.NodeId}  = 1500");
Console.WriteLine($"  Boolean   {running.Path,-40} {running.NodeId}  = true");
Console.WriteLine($"  String    {status.Path,-40} {status.NodeId}  = Idle");
Console.WriteLine($"  DateTime  {lastUpdate.Path,-40} {lastUpdate.NodeId}  = now");
Console.WriteLine($"  String    {serialNo.Path,-40} {serialNo.NodeId}  = SN-2025-001 [ReadOnly]");
Console.WriteLine($"  Double[]  {setpoints.Path,-40} {setpoints.NodeId}  = [20, 25, 30]");
Console.WriteLine();

// =============================================================================
// Step 4: Connect a client and explore
// =============================================================================
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║");
Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
Console.WriteLine("║  opc.https://localhost:48411                                 ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Try:                                                        ║");
Console.WriteLine("║  * Browse Objects -> Plant -> Line1 -> Machine1              ║");
Console.WriteLine("║  * Subscribe to Temperature, RPM, Status                     ║");
Console.WriteLine("║  * Write a new value to RPM or Status                        ║");
Console.WriteLine("║  * Try writing to SerialNumber (should fail — ReadOnly)      ║");
Console.WriteLine("║  * Watch the ValuesWritten output in this console            ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to start the value push loop.                   ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();

// =============================================================================
// Step 5: Push value changes to subscribed clients
// =============================================================================
// Setting variable.Value automatically triggers a DataChange notification
// to all clients that have an active subscription on that variable.
// This is the OPC UA publish/subscribe model — no polling needed on the client.
//
// The value push runs in the main thread here for simplicity.
// In production, you would typically update values from a PLC driver,
// a database poller, or any other data source running on a background thread.
Console.WriteLine("Pushing values every second... (CTRL+C to exit)");
Console.WriteLine();

var rng = new Random();
long cycle = 0;

while (true)
{
    cycle++;

    // Each assignment to .Value notifies all subscribed clients immediately
    temperature.Value = Math.Round(20.0 + rng.NextDouble() * 10.0, 2);
    pressure.Value    = (float)Math.Round(0.9 + rng.NextDouble() * 0.3, 3);
    rpm.Value         = 1400 + rng.Next(200);
    running.Value     = cycle % 30 != 0;  // simulate a stop every 30 seconds
    status.Value      = running.Value ? "Running" : "Stopped";
    lastUpdate.Value  = DateTime.UtcNow;

    Console.Write($"\r  Cycle={cycle}  Temp={temperature.Value:F1}C  " +
                  $"P={pressure.Value:F3}bar  RPM={rpm.Value}  {status.Value,-8}");
    Thread.Sleep(1000);
}

// =============================================================================
// Helper: CreateConfig
// =============================================================================
// Returns the server configuration. All available options are listed here
// with a description and the default value. Adjust to your needs.
static UaServerConfiguration CreateConfig()
{
    var config = new UaServerConfiguration
    {
        // ── Application Identity ──────────────────────────────────────────────
        // ApplicationName: human-readable name shown to connecting clients
        //   and embedded in the auto-generated server certificate.
        ApplicationName = "PLCcom Workshop 11 - Simple Server",

        // ApplicationUri: globally unique identifier for this server instance.
        //   Must match the URI in the server certificate.
        //   Recommended format: urn:<host>:<company>:<product>
        ApplicationUri  = "urn:localhost:PLCcom:Workshop:11",

        // ProductUri: URI identifying the software product (not the instance).
        //   Typically a URL pointing to the product page.
        ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",

        // NamespaceUri: URI for this server's application address space (ns=2).
        //   Use a stable URI based on your company domain.
        //   If null, defaults to ApplicationUri + "/nodes".
        NamespaceUri    = "http://indi-an.com/opcua/workshop/simple-server",

        // ── ServerStatus/BuildInfo ────────────────────────────────────────────
        // These values appear under Server/ServerStatus/BuildInfo in the
        // OPC UA address space and identify the software to connecting clients.
        // Null = auto-detect from the assembly.
        ManufacturerName = "My Company GmbH",
        ProductName      = "My OPC UA Server",
        SoftwareVersion  = "1.0.0",
        BuildNumber      = "42",

        // ── Endpoints ────────────────────────────────────────────────────────
        // The URLs clients connect to. Multiple endpoints are supported.
        //   opc.tcp  — binary protocol, best performance, recommended
        //   opc.https — SOAP/XML over HTTPS, for firewall-friendly scenarios
        BaseAddresses = new List<string>
        {
            "opc.tcp://localhost:48410",
            "opc.https://localhost:48411"
        },

        // ── Security Policies ────────────────────────────────────────────────
        // Which encryption algorithms to offer on the endpoints.
        // GetRecommendedSecurityPolicies() returns:
        //   None (no encryption, for development only)
        //   Basic256Sha256     Sign + SignAndEncrypt
        //   Aes128_Sha256_RsaOaep  Sign + SignAndEncrypt
        //   Aes256_Sha256_RsaPss   Sign + SignAndEncrypt
        SecurityPolicies = UaServer.GetRecommendedSecurityPolicies(),

        // ── User Authentication ───────────────────────────────────────────────
        // Which authentication methods to accept from connecting clients.
        //   Anonymous   — no credentials required
        //   UserName    — username + password (see server.UserManager)
        //   Certificate — X.509 client certificate (see server.UserManager)
        UserTokenPolicies = new List<UserTokenPolicy>
        {
            new UserTokenPolicy { TokenType = UserTokenType.Anonymous }
        },

        // AutoAcceptUntrustedCertificates: skip client certificate validation.
        // WARNING: only for development/testing — never use in production!
        // Default: false.
        AutoAcceptUntrustedCertificates = false,

        // AsConfigured (default) = endpoints use exactly the host from BaseAddresses
        // NormalizeToHostname    = replace localhost/127.0.0.1 with the machine name
        // None = no normalization, behavior depends on DNS and network settings
        EndpointHostMode = EndpointHostMode.AsConfigured,

        MaxSessionCount = 100,

        // ShutdownDelay: seconds the server waits for clients to disconnect
        // gracefully when Stop() is called. Default: 5.
        ShutdownDelay = 5,

        // HttpsMutualTls: require the client TLS certificate to match the OPC UA
        // application certificate sent in CreateSession. Default: false.
        HttpsMutualTls = false,

        // ── Local Discovery Server (LDS) ──────────────────────────────────────
        // RegisterWithDiscoveryServer: register with a LDS so that clients can
        // discover this server via FindServers without knowing its URL.
        // Default: false.
        RegisterWithDiscoveryServer = false,

        // DiscoveryServerUrl: URL of the LDS to register with.
        // Only used when RegisterWithDiscoveryServer = true.
        // Null = use the standard LDS at opc.tcp://localhost:4840.
        // DiscoveryServerUrl = "opc.tcp://localhost:4840",

        // ── VendorServerInfo ──────────────────────────────────────────────────
        // These values appear under Server/VendorServerInfo in the OPC UA
        // address space and identify your product to connecting clients.
        // Null = the corresponding node is not created.
        VendorName           = "My Company GmbH",
        VendorProductName    = "My OPC UA Server",
        VendorProductVersion = "1.0.0",

        // ── OperationLimits ───────────────────────────────────────────────────
        // These values appear under Server/ServerCapabilities/OperationLimits.
        // All 12 nodes are always present in the address space.
        // Clients read these values to size their request batches correctly.
        // 0 = no limit imposed by this server (not recommended for production).
        MaxNodesPerRead                          = 1000,  // max nodes per Read request
        MaxNodesPerWrite                         = 1000,  // max nodes per Write request
        MaxNodesPerBrowse                        = 1000,  // max nodes per Browse/BrowseNext
        MaxNodesPerHistoryReadData               = 100,   // max nodes per HistoryRead (data)
        MaxNodesPerHistoryReadEvents             = 100,   // max nodes per HistoryRead (events)
        MaxNodesPerHistoryUpdateData             = 100,   // max nodes per HistoryUpdate (data)
        MaxNodesPerHistoryUpdateEvents           = 100,   // max nodes per HistoryUpdate (events)
        MaxNodesPerMethodCall                    = 200,   // max nodes per Method Call
        MaxNodesPerRegisterNodes                 = 1000,  // max nodes per RegisterNodes
        MaxNodesPerTranslateBrowsePathsToNodeIds = 1000,  // max nodes per TranslateBrowsePaths
        MaxNodesPerNodeManagement                = 1000,  // max nodes per AddNodes/DeleteNodes
        MaxMonitoredItemsPerCall                 = 1000,  // max items per CreateMonitoredItems
    };

    // ── PKI Certificate Store ─────────────────────────────────────────────────
    // Build the certificate store: one Application cert for the OPC UA secure channel,
    // plus one default HTTPS certificate presented at every opc.https TLS handshake.
    //
    // UaServerCertificateStore.Load() tries to load all certs from disk.
    // Certificates that are missing or cannot be read remain in the store
    // but are marked as not ready (IsReady = false).
    //
    // GetMissingOrExpired() returns all certificates that are either:
    //   - not present on disk (first run)
    //   - expired (NotAfter < now)
    //   - could not be loaded (wrong password, corrupt file)
    // Each of these is rebuilt as a new self-signed certificate.
    // Build(true) overwrites any existing file — safe because we only
    // reach this for certs that are missing or no longer valid.
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

    // Hand the fully populated store to the configuration.
    // UaServer.Start() will use it to set up the secure channel and
    // the PKI directory structure (trusted/, rejected/, issuer/).

    config.SetCertificateStore(store);

    return config;
}

// =============================================================================
// Helper: PrintConfig
// =============================================================================
// Prints the active server configuration to the console so you can verify
// all settings at a glance before the server starts accepting connections.
static void PrintConfig(UaServerConfiguration config)
{
    Console.WriteLine("── Active Server Configuration ──────────────────────────────────────────────");
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
    Console.WriteLine("─────────────────────────────────────────────────────────────────────────────");
    Console.WriteLine();
}
