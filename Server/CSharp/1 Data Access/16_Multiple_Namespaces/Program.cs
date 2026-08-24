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
// PLCcom OPC UA Server SDK - Workshop 16: Multiple Namespaces
//
// Every node in OPC UA has a NodeId and a BrowseName, both of which belong
// to a namespace. Namespaces prevent naming collisions when multiple vendors,
// standards or subsystems share the same server.
//
// The OPC UA specification (Part 5) reserves two namespace indexes:
//   ns=0  Reserved for the OPC UA standard namespace
//         ("http://opcfoundation.org/UA/")
//   ns=1  Reserved for the local Server
//         (ApplicationUri + "/nodes" by convention)
//
// Additional namespaces are registered with AddNamespace(). Each returns a
// namespace index (ns=2, ns=3, ...) that is used in NodeIds and BrowseNames.
//
// This workshop demonstrates a realistic scenario:
//
//   ns=0  OPC UA standard        - standard nodes (always present)
//   ns=1  Local server            - server's own nodes (always present)
//   ns=2  Company namespace       - company-wide type definitions
//   ns=3  Plant A namespace       - nodes for Plant A
//   ns=4  Plant B namespace       - nodes for Plant B
//
// Both plants use the same company-defined types but have separate node trees.
// A client can filter by namespace to see only one plant's data.
//
// The address space built here:
//   Objects
//     +-- CompanyTypes  (ns=2)          folder for company-wide types
//     +-- PlantA  (ns=3)
//     |     +-- Reactor  (ns=3)
//     |     |     +-- Temperature  (ns=3)  = 85.0
//     |     |     +-- Pressure     (ns=3)  = 2.5
//     |     +-- Mixer    (ns=3)
//     |           +-- Speed        (ns=3)  = 120.0
//     +-- PlantB  (ns=4)
//           +-- Reactor  (ns=4)
//           |     +-- Temperature  (ns=4)  = 92.0
//           |     +-- Pressure     (ns=4)  = 3.1
//           +-- Mixer    (ns=4)
//                 +-- Speed        (ns=4)  = 80.0
//
// What you will learn:
//   * Why namespaces matter in OPC UA
//   * How to register additional namespaces with AddNamespace
//   * How to create nodes in a specific namespace using the ns parameter
//   * How NodeIds and BrowseNames reflect the namespace index
//   * How ObjectTypes in one namespace can be used across other namespaces
//   * How to look up a namespace index with GetNamespaceIndex
//
// Connect with any OPC UA client to: opc.tcp://localhost:48410
// ==============================================================================

using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Server.Sdk;
using System;
using System.Reflection;
using System.Collections.Generic;

// -- License -------------------------------------------------------------------
// Important !!!!!!!!!!!!!!!!!!
// Enter your Username + Serial here! Please note: with blank fields the library runs
// for 15 minutes during a debug session. Both values can also come
// from configuration or an environment variable.
// Free trial license (14 days, uninterrupted): https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-download/
string LicenseUserName = "";
string LicenseSerial = "";

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 16: Multiple Namespaces ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This example demonstrates:                                  ║");
Console.WriteLine("║    * Registering additional namespaces                       ║");
Console.WriteLine("║    * Creating nodes in specific namespaces                   ║");
Console.WriteLine("║    * Sharing ObjectTypes across namespaces                   ║");
Console.WriteLine("║    * Two plants with identical structure but separate nodes  ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// =============================================================================
// Step 1: Configure and start the server
// =============================================================================
// All server settings are defined in CreateConfig() below.
// See that function for a full description of every available option.
var config = CreateConfig();
PrintConfig(config);

using var server = new UaServer(LicenseUserName, LicenseSerial);

// Accept all client certificates automatically.
// WARNING: Do NOT use this in production! Either implement your own validation
// logic here (inspect e.Certificate and e.Error, then set e.Accept = true or false),
// or remove this handler entirely -- the SDK will then automatically validate
// certificates against the PKI trust store (pki/trusted/certs/).
server.CertificateValidation += (sender, e) => e.Accept = true;

server.ValuesWritten += (s, e) =>
{
    foreach (var item in e.Items)
        Console.WriteLine($"  << OPC Write: {item.Path} ({item.NodeId}) = {item.Value}");
};

Console.Write("Starting server ... ");
try { server.Start(config); }
catch (Exception ex) { Console.WriteLine("FAILED"); Console.WriteLine(ex.Message); Console.ReadLine(); return; }
Console.WriteLine("OK");
Console.WriteLine();

// Show the namespace table after server start
var nsTable = server.NodeManager.Server.NamespaceUris;
Console.WriteLine("-- Namespace table after Start() --------------------------------");
for (int i = 0; i < nsTable.Count; i++)
    Console.WriteLine($"  ns={i}  {nsTable.GetString((uint)i)}");
Console.WriteLine($"  NodeManager.NamespaceIndex = {server.NodeManager.NamespaceIndex}");
Console.WriteLine();

// Create two variables in default namespace for comparison
var defaultFolder = server.CreateFolder("DefaultNS", UaRolePermissions.WITHOUT_RESTRICTIONS);
var testValue1 = server.CreateVariable<double>(defaultFolder, "TestValue1", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 42.0);
var testValue2 = server.CreateVariable<string>(defaultFolder, "TestValue2", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: "hello");
Console.WriteLine("-- Default namespace nodes ----------------------------------------");
Console.WriteLine($"  {defaultFolder.Path,-40} NodeId={defaultFolder.NodeId}  BrowseName={defaultFolder.BrowseName}");
Console.WriteLine($"  {testValue1.Path,-40} NodeId={testValue1.NodeId}  BrowseName={testValue1.BrowseName}");
Console.WriteLine($"  {testValue2.Path,-40} NodeId={testValue2.NodeId}  BrowseName={testValue2.BrowseName}");
Console.WriteLine();

// =============================================================================
// Step 2: Register additional namespaces
// =============================================================================
// After Start(), the server has two namespaces (per OPC UA Spec Part 5):
//   ns=0  OPC UA standard namespace (reserved, always present)
//   ns=1  Local server namespace (reserved for this server's own nodes)
//
// AddNamespace() registers a new URI and returns its index (ns=2, 3, ...).
// The URI should be a globally unique identifier for the namespace owner.
// Convention: use a URI based on your company domain or project.
Console.WriteLine("-- Registering namespaces ---------------------------------------");

ushort nsCompany = server.AddNamespace("urn:mycompany:types");
ushort nsPlantA  = server.AddNamespace("urn:mycompany:plant-a");
ushort nsPlantB  = server.AddNamespace("urn:mycompany:plant-b");

Console.WriteLine($"  ns={nsCompany}  urn:mycompany:types     (company-wide types)");
Console.WriteLine($"  ns={nsPlantA}  urn:mycompany:plant-a   (Plant A instances)");
Console.WriteLine($"  ns={nsPlantB}  urn:mycompany:plant-b   (Plant B instances)");
Console.WriteLine();

// Show the full namespace table after AddNamespace
Console.WriteLine("-- Namespace table after AddNamespace() -------------------------");
for (int i = 0; i < nsTable.Count; i++)
    Console.WriteLine($"  ns={i}  {nsTable.GetString((uint)i)}");
Console.WriteLine();

// You can also look up a namespace index later:
ushort check = server.GetNamespaceIndex("urn:mycompany:plant-a");
Console.WriteLine($"  GetNamespaceIndex(\"urn:mycompany:plant-a\") = {check}");
Console.WriteLine();

// =============================================================================
// Step 3: Define company-wide ObjectTypes in the company namespace
// =============================================================================
// ObjectTypes are reusable blueprints. By placing them in the company namespace,
// they are clearly identified as belonging to your organization.
// Instances in different plant namespaces can reference the same type.
Console.WriteLine("-- Company-wide ObjectTypes (ns={0}) ----------------------------", nsCompany);

var reactorTypeId = server.CreateObjectType("ReactorType", ns: nsCompany);
var mixerTypeId   = server.CreateObjectType("MixerType", ns: nsCompany);

Console.WriteLine($"  ReactorType  {reactorTypeId}");
Console.WriteLine($"  MixerType    {mixerTypeId}");
Console.WriteLine();

// =============================================================================
// Step 4: Build Plant A in its own namespace
// =============================================================================
// All folders, objects and variables for Plant A use ns=nsPlantA.
// This means their NodeIds and BrowseNames carry the Plant A namespace index.
// A client filtering by namespace can isolate Plant A's data.
Console.WriteLine("-- Plant A (ns={0}) ---------------------------------------------", nsPlantA);

// Only the top-level folder needs the ns: parameter.
// All children inherit the namespace from their parent automatically.
var plantA = server.CreateFolder("PlantA", UaRolePermissions.WITHOUT_RESTRICTIONS, ns: nsPlantA);

var reactorA = server.CreateObject(plantA, "Reactor", UaRolePermissions.WITHOUT_RESTRICTIONS, reactorTypeId);
var tempA    = server.CreateVariable<double>(reactorA, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 85.0);
var pressA   = server.CreateVariable<double>(reactorA, "Pressure", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 2.5);

var mixerA = server.CreateObject(plantA, "Mixer", UaRolePermissions.WITHOUT_RESTRICTIONS, mixerTypeId);
var speedA = server.CreateVariable<double>(mixerA, "Speed", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 120.0);

Console.WriteLine($"  {plantA.Path,-40} NodeId={plantA.NodeId}  BrowseName={plantA.BrowseName}");
Console.WriteLine($"  {tempA.Path,-40} NodeId={tempA.NodeId}  BrowseName={tempA.BrowseName}");
Console.WriteLine($"  {pressA.Path,-40} NodeId={pressA.NodeId}  BrowseName={pressA.BrowseName}");
Console.WriteLine($"  {speedA.Path,-40} NodeId={speedA.NodeId}  BrowseName={speedA.BrowseName}");
Console.WriteLine();

// =============================================================================
// Step 5: Build Plant B in its own namespace
// =============================================================================
// Plant B has the exact same structure as Plant A, but all nodes are in ns=4.
// The BrowseNames "Reactor", "Temperature" etc. are the same strings,
// but qualified with a different namespace index -> no collision.
Console.WriteLine("-- Plant B (ns={0}) ---------------------------------------------", nsPlantB);

// Same for Plant B — only the root folder specifies the namespace.
var plantB = server.CreateFolder("PlantB", UaRolePermissions.WITHOUT_RESTRICTIONS, ns: nsPlantB);

var reactorB = server.CreateObject(plantB, "Reactor", UaRolePermissions.WITHOUT_RESTRICTIONS, reactorTypeId);
var tempB    = server.CreateVariable<double>(reactorB, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 92.0);
var pressB   = server.CreateVariable<double>(reactorB, "Pressure", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 3.1);

var mixerB = server.CreateObject(plantB, "Mixer", UaRolePermissions.WITHOUT_RESTRICTIONS, mixerTypeId);
var speedB = server.CreateVariable<double>(mixerB, "Speed", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 80.0);

Console.WriteLine($"  {plantB.Path,-40} NodeId={plantB.NodeId}  BrowseName={plantB.BrowseName}");
Console.WriteLine($"  {tempB.Path,-40} NodeId={tempB.NodeId}  BrowseName={tempB.BrowseName}");
Console.WriteLine($"  {pressB.Path,-40} NodeId={pressB.NodeId}  BrowseName={pressB.BrowseName}");
Console.WriteLine($"  {speedB.Path,-40} NodeId={speedB.NodeId}  BrowseName={speedB.BrowseName}");
Console.WriteLine();

// =============================================================================
// Step 6: Demonstrate cross-namespace reading
// =============================================================================
// The server.GetValue/SetValue API works across namespaces using the dot-separated path.
// The path uses BrowseName strings (without namespace prefix).
Console.WriteLine("-- Cross-namespace GetValue -------------------------------------");

double tA = server.GetValue<double>("Objects.PlantA.Reactor.Temperature");
double tB = server.GetValue<double>("Objects.PlantB.Reactor.Temperature");
Console.WriteLine($"  PlantA Reactor Temperature = {tA}");
Console.WriteLine($"  PlantB Reactor Temperature = {tB}");
Console.WriteLine();

// =============================================================================
// Step 7: Run the server
// =============================================================================
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║");
Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Try:                                                        ║");
Console.WriteLine("║  * Browse Objects -> PlantA -> Reactor -> Temperature        ║");
Console.WriteLine("║  * Browse Objects -> PlantB -> Reactor -> Temperature        ║");
Console.WriteLine("║  * Compare NodeIds: both have numeric IDs but different ns   ║");
Console.WriteLine("║  * Compare BrowseNames: same name, different namespace index ║");
Console.WriteLine("║  * Browse Types -> ObjectTypes -> ReactorType, MixerType     ║");
Console.WriteLine("║  * Write PlantA/Reactor/Temperature and PlantB independently ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to exit.                                        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();

// =============================================================================
// Helper: CreateConfig
// =============================================================================
static UaServerConfiguration CreateConfig()
{
    var config = new UaServerConfiguration
    {
        // ── Application Identity ──────────────────────────────────────────────
        ApplicationName  = "PLCcom Workshop 16 - Multiple Namespaces",
        ApplicationUri   = "urn:localhost:PLCcom:Workshop:16",
        ProductUri       = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
        NamespaceUri     = "http://indi-an.com/opcua/workshop/multiple-namespaces",

        // ── ServerStatus/BuildInfo ────────────────────────────────────────────
        ManufacturerName = "My Company GmbH",
        ProductName      = "My OPC UA Server",
        SoftwareVersion  = "1.0.0",
        BuildNumber      = "42",

        // ── Endpoints ────────────────────────────────────────────────────────
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
        // AsConfigured (default) = endpoints use exactly the host from BaseAddresses
        // NormalizeToHostname    = replace localhost/127.0.0.1 with the machine name
        // None = no normalization, behavior depends on DNS and network settings
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
