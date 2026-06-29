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
// PLCcom OPC UA Server SDK - Workshop 35: Custom Event History Store
//
// Workshop 33 showed how to record historical events using the default
// in-memory store. This workshop shows how to replace that store with
// your own implementation using the IEventHistoryStore interface.
//
// IEventHistoryStore is the extension point that lets YOU decide where
// event history is stored. You implement the interface once and the SDK
// calls it automatically whenever events are recorded or clients request
// event history via HistoryRead.
//
// Typical back-ends you can connect via IEventHistoryStore:
//   * Relational databases  (SQL Server, PostgreSQL, SQLite, MySQL, ...)
//   * Time-series databases (InfluxDB, TimescaleDB, ...)
//   * Cloud storage         (Azure Blob, AWS S3, ...)
//   * Message brokers       (Kafka, MQTT, ...)
//   * Custom binary files, CSV, Parquet, or any proprietary format
//
// The interface is intentionally minimal - only three methods:
//   * Initialize() - called once when EnableHistoryEvents() is invoked
//   * Append()     - called by RecordHistoryEvent()
//   * Read()       - called when a client requests historical events
//
// This workshop demonstrates the pattern using CSV files as the back-end.
// CSV is chosen because it requires no external dependencies and is easy
// to inspect - NOT because it is recommended for production.
// Replace CsvEventHistoryStore with your own implementation for real use.
//
// What you will learn:
//   * How to implement IEventHistoryStore for any storage back-end
//   * How to register a global event store via server.EventHistoryStore
//   * How to assign a per-node store via EnableHistoryEvents(..., store: ...)
//
// Connect with any OPC UA client to: opc.tcp://localhost:48410
// ==============================================================================

using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Server.Sdk;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

// -- License -------------------------------------------------------------------
// TODO: Replace with your license credentials from your license e-mail
string LicenseUserName = "<Enter your UserName here>";
string LicenseSerial   = "<Enter your Serial here>";

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 35:                     ║");
Console.WriteLine("║                         Custom Event History Store           ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  IEventHistoryStore lets you connect ANY storage back-end:   ║");
Console.WriteLine("║    SQL Server, PostgreSQL, SQLite, InfluxDB, TimescaleDB,    ║");
Console.WriteLine("║    Azure Blob, AWS S3, Kafka, custom files, and more.        ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This workshop uses CSV files to demonstrate the pattern.    ║");
Console.WriteLine("║  Replace CsvEventHistoryStore with your own implementation.  ║");
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

// -- Register the custom event history store BEFORE calling EnableHistoryEvents()
server.EventHistoryStore = new CsvEventHistoryStore(@".\event_history");

Console.Write("Starting server ... ");
try { server.Start(config); }
catch (Exception ex) { Console.WriteLine("FAILED"); Console.WriteLine(ex.Message); Console.ReadLine(); return; }
Console.WriteLine("OK");
Console.WriteLine();

server.HistoryUpdated += (s, e) =>
{
    string detail = e.Values != null && e.Values.Length > 0 && e.Values[0] is int count
        ? $"deleted {count} event(s)"
        : "(range delete)";
    Console.WriteLine($"  << {e.Operation,-15}  {detail}  path={e.Path}");
};

var plant   = server.CreateFolder("Plant", UaRolePermissions.WITHOUT_RESTRICTIONS);
var reactor = server.CreateFolder(plant, "Reactor", UaRolePermissions.WITHOUT_RESTRICTIONS);

var temperature = server.CreateVariable<double>(reactor, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 25.0);
temperature.SetEURange(0, 200);
temperature.SetEngineeringUnits("C", "Degrees Celsius");

server.EnableEvents(reactor);
server.EnableHistoryEvents(reactor, maxEntries: 500);

Console.WriteLine("  Event history store: CsvEventHistoryStore -> .\\event_history\\");
Console.WriteLine("  Reactor:");
Console.WriteLine("    Temperature (0-200 C)");
Console.WriteLine("    Events: live + history enabled (max 500 entries)");
Console.WriteLine();

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║");
Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Event history is written to CSV files in event_history      ║");
Console.WriteLine("║  Restart the server - event history will still be available! ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to start the simulation.                        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();

Console.WriteLine("Simulating... events fire every 5 seconds (CTRL+C to exit)");
Console.WriteLine("  Temperature > 80C -> High severity event");
Console.WriteLine("  Temperature > 60C -> Medium severity event");
Console.WriteLine("  Temperature <= 60C -> Low severity event");
Console.WriteLine();

var rng   = new Random();
long cycle = 0;

while (true)
{
    cycle++;

    double t = 50.0 + Math.Sin(cycle * 0.15) * 40.0 + rng.NextDouble() * 5.0;
    temperature.Value = Math.Round(t, 1);

    EventSeverity severity;
    string message;
    if (t > 80.0)
    {
        severity = EventSeverity.High;
        message  = $"Temperature HIGH: {t:F1}C";
    }
    else if (t > 60.0)
    {
        severity = EventSeverity.Medium;
        message  = $"Temperature warning: {t:F1}C";
    }
    else
    {
        severity = EventSeverity.Low;
        message  = $"Temperature normal: {t:F1}C";
    }

    server.FireEvent(reactor, message, severity);

    var eventState = new BaseEventState(null);
    eventState.Initialize(
        server.NodeManager.SystemContext,
        server.NodeManager.FindNodeInAddressSpace(reactor.NodeId),
        severity,
        new LocalizedText(message));
    eventState.Create(server.NodeManager.SystemContext, null, new QualifiedName("Event"), null, true);
    server.RecordHistoryEvent(reactor.NodeId, eventState);

    string severityLabel = severity == EventSeverity.High   ? "HIGH" :
                           severity == EventSeverity.Medium ? "MED " : "LOW ";
    Console.WriteLine($"  [{severityLabel}] {message}");

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
        ApplicationName  = "PLCcom Workshop 35 - Custom Event History Store",
        ApplicationUri   = "urn:localhost:PLCcom:Workshop:35",
        ProductUri       = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
        NamespaceUri     = "http://indi-an.com/opcua/workshop/custom-event-history",

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

// ==============================================================================
// CsvEventHistoryStore - example IEventHistoryStore implementation using CSV files
//
// This class exists solely to demonstrate HOW to implement IEventHistoryStore.
// CSV is not recommended for production - use a database or time-series store.
//
// To connect your own back-end, create a class that implements IEventHistoryStore
// and replace "new CsvEventHistoryStore(...)" with "new YourStore(...)" above.
//
// One CSV file per source node, named by NodeId.
// Format: eventId (Base64), time (ISO 8601), sourceName, message, severity
// ==============================================================================

public class CsvEventHistoryStore : IEventHistoryStore
{
    private readonly string m_directory;
    private readonly object m_lock = new object();

    public CsvEventHistoryStore(string directory)
    {
        m_directory = directory;
        Directory.CreateDirectory(directory);
    }

    public void Initialize(NodeId sourceNodeId, int maxEntries) { }

    public void Append(NodeId sourceNodeId, UaHistoryEventEntry entry)
    {
        lock (m_lock)
        {
            var eid = entry.EventId != null ? Convert.ToBase64String(entry.EventId) : "";
            File.AppendAllText(
                FilePath(sourceNodeId),
                $"{eid};{entry.Time:O};{entry.SourceName};{entry.Message};{entry.Severity}\n");
        }
    }

    public IReadOnlyList<UaHistoryEventEntry> Read(NodeId sourceNodeId, DateTime start, DateTime end, int maxValues = 0)
    {
        lock (m_lock)
        {
            var path = FilePath(sourceNodeId);
            if (!File.Exists(path)) return Array.Empty<UaHistoryEventEntry>();
            var result = new List<UaHistoryEventEntry>();
            foreach (var line in File.ReadLines(path))
            {
                var entry = ParseLine(line);
                if (entry == null) continue;
                if (start != DateTime.MinValue && entry.Time < start) continue;
                if (end   != DateTime.MinValue && entry.Time > end)   continue;
                result.Add(entry);
                if (maxValues > 0 && result.Count >= maxValues) break;
            }
            return result;
        }
    }

    private string FilePath(NodeId sourceNodeId)
        => Path.Combine(m_directory, $"{sourceNodeId.ToString().Replace(":", "_").Replace(";", "_")}.csv");

    public IReadOnlyList<StatusCode> Delete(NodeId sourceNodeId, IList<byte[]> eventIds)
    {
        var results = new List<StatusCode>();
        if (eventIds == null || eventIds.Count == 0) return results;
        lock (m_lock)
        {
            var path = FilePath(sourceNodeId);
            if (!File.Exists(path))
            {
                foreach (var _ in eventIds) results.Add(StatusCodes.BadNoEntryExists);
                return results;
            }
            var lines = File.ReadAllLines(path).ToList();
            foreach (var eid in eventIds)
            {
                var key = eid != null ? Convert.ToBase64String(eid) : "";
                int idx = lines.FindIndex(l => l.StartsWith(key + ";"));
                if (idx >= 0) { lines.RemoveAt(idx); results.Add(StatusCodes.Good); }
                else { results.Add(StatusCodes.BadNoEntryExists); }
            }
            File.WriteAllLines(path, lines);
        }
        return results;
    }

    public void Remove(NodeId sourceNodeId)
    {
        lock (m_lock)
        {
            var path = FilePath(sourceNodeId);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static UaHistoryEventEntry ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        var parts = line.Split(';');
        if (parts.Length < 5) return null;
        byte[] eventId = !string.IsNullOrEmpty(parts[0]) ? Convert.FromBase64String(parts[0]) : null;
        if (!DateTime.TryParse(parts[1], null, DateTimeStyles.RoundtripKind, out var time)) return null;
        if (!ushort.TryParse(parts[4], out var severity)) severity = 500;
        return new UaHistoryEventEntry(eventId, time, parts[2], parts[3], severity, null);
    }
}
