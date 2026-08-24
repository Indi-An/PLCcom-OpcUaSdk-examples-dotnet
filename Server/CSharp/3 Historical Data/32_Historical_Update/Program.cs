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
// PLCcom OPC UA Server SDK - Workshop 32: Historical Update
//
// Workshop 31 demonstrated reading historical data. This workshop extends
// the server to also accept HistoryUpdate requests from clients:
//   Insert  - add a new value at a specific timestamp
//   Update  - insert or replace (upsert)
//   Replace - replace an existing value (fails if not exists)
//   Remove  - remove a value by timestamp
//   DeleteRaw    - delete all values in a time range
//   DeleteAtTime - delete values at specific timestamps
//
// The server uses the same in-memory history store as Workshop 31.
// Clients can use the PLCcom Client SDK methods (Insert, Update, Replace,
// Remove, DeleteRaw, DeleteAtTime) or any OPC UA compliant client.
//
// What you will learn:
//   * How EnableHistory automatically enables HistoryWrite access
//   * How clients can insert, update, replace and delete history values
//   * How the server validates operations (BadEntryExists, BadNoEntryExists)
//   * How to verify history changes by reading back
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
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 32: Historical Update   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This example demonstrates:                                  ║");
Console.WriteLine("║  * History recording with read AND write access              ║");
Console.WriteLine("║  * Clients can Insert, Update, Replace, Remove values        ║");
Console.WriteLine("║  * Clients can DeleteRaw (by range) and DeleteAtTime         ║");
Console.WriteLine("║  * Server validates each operation and returns StatusCodes   ║");
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

server.HistoryUpdated += (s, e) =>
{
    string detail;
    if (e.Operation == UaHistoryUpdateOperation.DeleteAtTime)
    {
        detail = $"deleted {e.Timestamps.Length} entries";
    }
    else if (e.Timestamps.Length > 0)
    {
        var parts = new string[e.Timestamps.Length];
        for (int i = 0; i < e.Timestamps.Length; i++)
        {
            string ts  = e.Timestamps[i].ToLocalTime().ToString("HH:mm:ss.fff");
            string val = e.Values != null && i < e.Values.Length && e.Values[i] != null
                ? $"  value={e.Values[i],-10}" : string.Empty;
            parts[i] = ts + val;
        }
        detail = string.Join("\n                          ", parts);
    }
    else
    {
        detail = "(range delete)";
    }
    Console.WriteLine($"\n  << {e.Operation,-15}  {detail}  path={e.Path}");
};

var plant  = server.CreateFolder("Plant", UaRolePermissions.WITHOUT_RESTRICTIONS);
var sensor = server.CreateFolder(plant, "Sensor", UaRolePermissions.WITHOUT_RESTRICTIONS);

var temperature = server.CreateVariable<double>(sensor, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 20.0);
temperature.SetEURange(-40, 120);
temperature.SetEngineeringUnits("C", "Degrees Celsius");

var pressure = server.CreateVariable<double>(sensor, "Pressure", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 1.0);
pressure.SetEURange(0, 10);
pressure.SetEngineeringUnits("bar", "Bar");

// EnableHistory sets Historizing=true AND AccessLevel includes HistoryRead + HistoryWrite.
// This means clients can both read AND modify the history.
server.EnableHistory(temperature, maxEntries: 500);
server.EnableHistory(pressure,    maxEntries: 500);

Console.WriteLine("  Variables with history enabled (read + write):");
Console.WriteLine("    Temperature: Historizing=true, HistoryRead + HistoryWrite");
Console.WriteLine("    Pressure:    Historizing=true, HistoryRead + HistoryWrite");
Console.WriteLine();

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║");
Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  The server records values every second. Clients can:        ║");
Console.WriteLine("║  * Read history (HistoryRead / ReadRaw)                      ║");
Console.WriteLine("║  * Insert new values at specific timestamps                  ║");
Console.WriteLine("║  * Update (upsert) existing values                           ║");
Console.WriteLine("║  * Replace existing values                                   ║");
Console.WriteLine("║  * Remove values by timestamp                                ║");
Console.WriteLine("║  * Delete all values in a time range (DeleteRaw)             ║");
Console.WriteLine("║  * Delete values at specific timestamps (DeleteAtTime)       ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Use Client Workshop 41 to test all operations.              ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to start recording.                             ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();

Console.WriteLine("Recording history every second... (CTRL+C to exit)");
var rng = new Random();
long cycle = 0;

while (true)
{
    cycle++;
    var now = DateTime.UtcNow;

    double t = 20.0 + Math.Sin(cycle * 0.1) * 10.0 + rng.NextDouble() * 2.0;
    double p = 1.0 + Math.Cos(cycle * 0.08) * 0.5 + rng.NextDouble() * 0.2;
    temperature.Value = Math.Round(t, 1);
    pressure.Value    = Math.Round(p, 2);

    server.RecordHistoryValue(temperature, now);
    server.RecordHistoryValue(pressure,    now);

    var hist = server.GetHistory(temperature.NodeId);
    Console.Write($"\r  Cycle={cycle}  T={temperature.Value:F1}C  " +
                  $"P={pressure.Value:F2}bar  History={hist.Count} entries  ");
    Thread.Sleep(1000);
}

// =============================================================================
// Helper: CreateConfig
// =============================================================================
static UaServerConfiguration CreateConfig()
{
    var config = new UaServerConfiguration
    {
        // ── Application Identity ──────────────────────────────────────────────
        ApplicationName  = "PLCcom Workshop 32 - Historical Update",
        ApplicationUri   = "urn:localhost:PLCcom:Workshop:32",
        ProductUri       = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
        NamespaceUri     = "http://indi-an.com/opcua/workshop/historical-update",

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
    // ── PKI Certificate Store ─────────────────────────────────────────────────
    // UaServerCertificateStore manages all server certificates.
    // Load() tries to load existing certificates from disk.
    // GetMissingOrExpired() returns certificates that need to be (re)created.
    // Build(overwrite: true) creates a new self-signed certificate on disk.
    //
    // One Application certificate is required for the OPC UA secure channel.
    // One default HTTPS certificate is presented at every opc.https TLS handshake.
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
