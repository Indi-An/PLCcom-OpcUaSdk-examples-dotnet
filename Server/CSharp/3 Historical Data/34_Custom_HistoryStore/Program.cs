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
// PLCcom OPC UA Server SDK - Workshop 34: Custom History Store
//
// By default the SDK keeps all historical values in RAM (InMemoryHistoryStore).
// This works well for testing but is not suitable for production:
//   * History is lost when the server restarts
//   * Memory grows with every recorded value
//   * No integration with existing data infrastructure
//
// The SDK solves this with the IHistoryStore interface.
// IHistoryStore is the extension point that lets YOU decide where history
// data is stored. You implement the interface once and the SDK calls it
// automatically whenever values are recorded or clients request history.
//
// Typical back-ends you can connect via IHistoryStore:
//   * Relational databases  (SQL Server, PostgreSQL, SQLite, MySQL, ...)
//   * Time-series databases (InfluxDB, TimescaleDB, Prometheus, ...)
//   * Cloud storage         (Azure Blob, AWS S3, ...)
//   * Message brokers       (Kafka, MQTT, ...)
//   * Custom binary files, CSV, Parquet, or any proprietary format
//
// This workshop demonstrates the pattern using CSV files as the back-end.
// CSV is chosen because it requires no external dependencies and is easy
// to inspect - NOT because it is recommended for production.
// Replace CsvHistoryStore with your own implementation for real use.
//
// How it works:
//   1. Implement IHistoryStore (see CsvHistoryStore below)
//   2. Assign it to server.HistoryStore before calling EnableHistory()
//      - OR - pass it directly to EnableHistory() for per-variable stores
//   3. The SDK calls Initialize(), Append(), Read(), InsertOrReplace(),
//      Delete() and DeleteAt() on your implementation
//
// What you will learn:
//   * How to implement IHistoryStore for any storage back-end
//   * How to register a global store via server.HistoryStore
//   * How to assign a per-variable store via EnableHistory(..., store: ...)
//
// Connect with any OPC UA client to: opc.tcp://localhost:48410
// ==============================================================================

using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Server.Sdk;
using System;
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
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 34: Custom History Store║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  IHistoryStore lets you connect ANY storage back-end:        ║");
Console.WriteLine("║    SQL Server, PostgreSQL, SQLite, InfluxDB, TimescaleDB,    ║");
Console.WriteLine("║    Azure Blob, AWS S3, Kafka, custom files, and more.        ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This workshop uses CSV files to demonstrate the pattern.    ║");
Console.WriteLine("║  Replace CsvHistoryStore with your own implementation.       ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

var config = CreateConfig();
PrintConfig(config);

using var server = new UaServer(LicenseUserName, LicenseSerial);
server.CertificateValidation += (s, e) => e.Accept = true;

// -- Register the custom history store BEFORE calling EnableHistory() ----------
server.HistoryStore = new CsvHistoryStore(@".\history");

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
var humidity    = server.CreateVariable<double>(sensor, "Humidity", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 50.0);

temperature.SetEURange(-40, 120);
temperature.SetEngineeringUnits("C");
humidity.SetEURange(0, 100);
humidity.SetEngineeringUnits("%RH");

server.EnableHistory(temperature, maxEntries: 500);
server.EnableHistory(humidity,    maxEntries: 500);

Console.WriteLine("  History store: CsvHistoryStore -> .\\history\\");
Console.WriteLine("  Variables with history enabled:");
Console.WriteLine("    Temperature: CSV file, max 500 entries");
Console.WriteLine("    Humidity:    CSV file, max 500 entries");
Console.WriteLine();

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║");
Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  History is written to CSV files in history folder           ║");
Console.WriteLine("║  Restart the server - history will still be available!       ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to start recording.                             ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();

Console.WriteLine("Recording history every second... (CTRL+C to exit)");
var rng   = new Random();
long cycle = 0;

while (true)
{
    cycle++;
    var now = DateTime.UtcNow;

    double t = 20.0 + Math.Sin(cycle * 0.1) * 10.0 + rng.NextDouble() * 2.0;
    double h = 50.0 + Math.Cos(cycle * 0.08) * 20.0 + rng.NextDouble() * 3.0;
    temperature.Value = Math.Round(t, 1);
    humidity.Value    = Math.Round(h, 1);

    server.RecordHistoryValue(temperature, now);
    server.RecordHistoryValue(humidity,    now);

    Console.Write($"\r  Cycle={cycle}  T={temperature.Value:F1}C  H={humidity.Value:F1}%RH  ");
    Thread.Sleep(1000);
}

// =============================================================================
// Helper: CreateConfig
// =============================================================================
static UaServerConfiguration CreateConfig()
{
    return new UaServerConfiguration
    {
        // ── Application Identity ──────────────────────────────────────────────
        ApplicationName  = "PLCcom Workshop 34 - Custom History Store",
        ApplicationUri   = "urn:localhost:PLCcom:Workshop:34",
        ProductUri       = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
        NamespaceUri     = "http://indi-an.com/opcua/workshop/custom-history-store",

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
        CertificateStorePath        = @".\pki",
        CertificateLifetimeInMonths = 60,
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

// ==============================================================================
// CsvHistoryStore - example IHistoryStore implementation using CSV files
//
// This class exists solely to demonstrate HOW to implement IHistoryStore.
// CSV is not recommended for production - use a database or time-series store.
//
// To connect your own back-end, create a class that implements IHistoryStore
// and replace "new CsvHistoryStore(...)" with "new YourStore(...)" above.
//
// One CSV file per variable, named by NodeId (e.g. "ns=1;i=3.csv").
// Format: timestamp (ISO 8601), value, statusCode
// ==============================================================================

public class CsvHistoryStore : IHistoryStore
{
    private readonly string m_directory;
    private readonly object m_lock = new object();

    public CsvHistoryStore(string directory)
    {
        m_directory = directory;
        Directory.CreateDirectory(directory);
    }

    public void Initialize(NodeId nodeId, int maxEntries) { }

    public void Append(NodeId nodeId, UaHistoryEntry entry)
    {
        lock (m_lock)
        {
            File.AppendAllText(
                FilePath(nodeId),
                $"{entry.Timestamp:O},{entry.Value},{entry.StatusCode}\n");
        }
    }

    public IReadOnlyList<UaHistoryEntry> Read(NodeId nodeId, DateTime start, DateTime end, int maxValues = 0)
    {
        lock (m_lock)
        {
            var path = FilePath(nodeId);
            if (!File.Exists(path)) return Array.Empty<UaHistoryEntry>();
            var result = new List<UaHistoryEntry>();
            foreach (var line in File.ReadLines(path))
            {
                var entry = ParseLine(line);
                if (entry == null) continue;
                if (start != DateTime.MinValue && entry.Timestamp < start) continue;
                if (end   != DateTime.MinValue && entry.Timestamp > end)   continue;
                result.Add(entry);
                if (maxValues > 0 && result.Count >= maxValues) break;
            }
            return result;
        }
    }

    public StatusCode InsertOrReplace(NodeId nodeId, UaHistoryEntry entry, PerformUpdateType mode)
    {
        lock (m_lock)
        {
            var all = LoadAll(nodeId);
            int idx = all.FindIndex(e => e.Timestamp == entry.Timestamp);
            switch (mode)
            {
                case PerformUpdateType.Insert:
                    if (idx >= 0) return StatusCodes.BadEntryExists;
                    all.Add(entry); all.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
                    SaveAll(nodeId, all); return StatusCodes.GoodEntryInserted;
                case PerformUpdateType.Replace:
                    if (idx < 0) return StatusCodes.BadNoEntryExists;
                    all[idx] = entry; SaveAll(nodeId, all); return StatusCodes.GoodEntryReplaced;
                case PerformUpdateType.Update:
                    if (idx >= 0) { all[idx] = entry; SaveAll(nodeId, all); return StatusCodes.GoodEntryReplaced; }
                    all.Add(entry); all.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
                    SaveAll(nodeId, all); return StatusCodes.GoodEntryInserted;
                case PerformUpdateType.Remove:
                    if (idx < 0) return StatusCodes.BadNoEntryExists;
                    all.RemoveAt(idx); SaveAll(nodeId, all); return StatusCodes.Good;
                default: return StatusCodes.BadHistoryOperationInvalid;
            }
        }
    }

    public void Delete(NodeId nodeId, DateTime start, DateTime end)
    {
        lock (m_lock)
        {
            var all = LoadAll(nodeId);
            all.RemoveAll(e => e.Timestamp >= start && e.Timestamp <= end);
            SaveAll(nodeId, all);
        }
    }

    public void Remove(NodeId nodeId)
    {
        lock (m_lock)
        {
            var path = FilePath(nodeId);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public IList<StatusCode> DeleteAt(NodeId nodeId, IEnumerable<DateTime> timestamps)
    {
        var results = new List<StatusCode>();
        lock (m_lock)
        {
            var all = LoadAll(nodeId);
            foreach (var ts in timestamps)
            {
                int idx = all.FindIndex(e => e.Timestamp == ts);
                if (idx >= 0) { all.RemoveAt(idx); results.Add(StatusCodes.Good); }
                else          { results.Add(StatusCodes.BadNoEntryExists); }
            }
            SaveAll(nodeId, all);
        }
        return results;
    }

    private string FilePath(NodeId nodeId)
        => Path.Combine(m_directory, $"{nodeId.ToString().Replace(":", "_").Replace(";", "_")}.csv");

    private List<UaHistoryEntry> LoadAll(NodeId nodeId)
    {
        var path = FilePath(nodeId);
        if (!File.Exists(path)) return new List<UaHistoryEntry>();
        return File.ReadLines(path).Select(ParseLine).Where(e => e != null).ToList();
    }

    private void SaveAll(NodeId nodeId, List<UaHistoryEntry> entries)
    {
        File.WriteAllLines(FilePath(nodeId), entries.Select(e => $"{e.Timestamp:O},{e.Value},{e.StatusCode}"));
    }

    private static UaHistoryEntry ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        var parts = line.Split(',');
        if (parts.Length < 3) return null;
        if (!DateTime.TryParse(parts[0], null, DateTimeStyles.RoundtripKind, out var ts)) return null;
        return new UaHistoryEntry(ts, parts[1], StatusCodes.Good);
    }
}
