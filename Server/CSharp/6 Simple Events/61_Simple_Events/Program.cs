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
// PLCcom OPC UA Server SDK - Workshop 61: Simple Events
//
// OPC UA Events are notifications that something happened - not a value change,
// but a discrete occurrence like a state transition, a warning, or an action.
//
// Events are different from DataChange notifications:
//   DataChange: a variable's value changed (subscription-based polling)
//   Event:      something happened at a source node (event subscription)
//
// To use events:
//   1. Call EnableEvents() on the source node (folder or object)
//   2. Call FireEvent() to send an event to all subscribed clients
//   3. Clients subscribe to the source node's EventNotifier attribute
//
// Events propagate upward automatically:
//   Machine1 -> Plant -> Objects -> Server
//   A client subscribed to the Server node receives ALL events from all sources.
//   A client subscribed to Machine1 only receives events from Machine1.
//
// Events have a severity level (1-1000):
//   Low    (  1-333): informational, normal operation
//   Medium (334-666): warning, attention needed
//   High   (667-1000): critical, immediate action required
//
// What you will learn:
//   * How to enable event notifications on a node
//   * How to fire events with different severity levels
//   * How clients subscribe to events in the Event View
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
string LicenseSerial = "";

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 61: Simple Events       ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This example demonstrates:                                  ║");
Console.WriteLine("║  * Enabling event notifications on nodes                     ║");
Console.WriteLine("║  * Firing events with message and severity                   ║");
Console.WriteLine("║  * Event severity levels (Low, Medium, High)                 ║");
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
server.CertificateValidation += (s, e) => e.Accept = true;

Console.Write("Starting server ... ");
try { server.Start(config); }
catch (Exception ex) { Console.WriteLine("FAILED"); Console.WriteLine(ex.Message); Console.ReadLine(); return; }
Console.WriteLine("OK");
Console.WriteLine();

var plant   = server.CreateFolder("Plant", UaRolePermissions.WITHOUT_RESTRICTIONS);
var machine = server.CreateFolder(plant, "Machine1", UaRolePermissions.WITHOUT_RESTRICTIONS);
var temp    = server.CreateVariable<double>(machine, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 22.0);

// -- Enable events on the source node ------------------------------------------
// EnableEvents() sets the EventNotifier attribute on the node so that
// clients can subscribe to events from it.
// Without this call, FireEvent() has no effect for subscribed clients.
// Events fired on Machine1 automatically propagate up to Plant -> Server,
// so clients subscribed to the Server node receive them as well.
server.EnableEvents(machine);

// Fire an initial event to confirm the server started successfully.
// This event is delivered to any client that is already subscribed.
server.FireEvent(machine, "Machine1 started successfully", EventSeverity.Low);

Console.WriteLine("  Machine1: Events enabled");
Console.WriteLine("  Initial event fired: 'Machine1 started successfully'");
Console.WriteLine();

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║");
Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  To see events in the client:                                ║");
Console.WriteLine("║  1. Open Document -> Add -> Event View                       ║");
Console.WriteLine("║  2. In the Event View, click the '+' button and select       ║");
Console.WriteLine("║     Objects -> Server (to receive all events)                ║");
Console.WriteLine("║  3. Press ENTER here to start firing events                  ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to start firing events every 5 seconds.         ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();

Console.WriteLine("Firing events every 5 seconds... (CTRL+C to exit)");
Console.WriteLine("  Temperature > 30 -> High severity event");
Console.WriteLine("  Temperature > 25 -> Medium severity event");
Console.WriteLine("  Temperature <= 25 -> Low severity event");
Console.WriteLine();

var rng = new Random();

while (true)
{
    double t = 20.0 + rng.NextDouble() * 15.0;
    temp.Value = Math.Round(t, 1);

    // Fire events with different severity based on the temperature value.
    // The severity level is visible in the client's Event View as a color
    // or numeric value in the Severity column.
    if (t > 30.0)
    {
        server.FireEvent(machine, $"Temperature HIGH: {t:F1}C", EventSeverity.High);
        Console.WriteLine($"  [EVENT HIGH] Temperature = {t:F1}C");
    }
    else if (t > 25.0)
    {
        server.FireEvent(machine, $"Temperature warning: {t:F1}C", EventSeverity.Medium);
        Console.WriteLine($"  [EVENT MED]  Temperature = {t:F1}C");
    }
    else
    {
        server.FireEvent(machine, $"Temperature normal: {t:F1}C", EventSeverity.Low);
        Console.WriteLine($"  [EVENT LOW]  Temperature = {t:F1}C");
    }

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
        ApplicationName  = "PLCcom Workshop 61 - Simple Events",
        ApplicationUri   = "urn:localhost:PLCcom:Workshop:61",
        ProductUri       = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
        NamespaceUri     = "http://indi-an.com/opcua/workshop/simple-events",

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
