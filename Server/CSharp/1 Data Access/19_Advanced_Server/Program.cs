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
// PLCcom OPC UA Server SDK - Workshop 19: Advanced Server
//
// A realistic OPC UA server that combines every Data Access feature
// demonstrated in Workshops 11-17 into a single, production-grade application.
//
// This server models a small factory with two CNC machines. It demonstrates
// how all the individual features work together in a real-world scenario.
//
// Architecture:
//   * Company namespace (ns=3) for reusable ObjectTypes and StructTypes
//   * Application namespace (ns=2) for all instance nodes
//   * Anonymous access with full read/write permissions
//   * Certificate validation with auto-accept for development
//   * Session tracking with console output
//   * Continuous value push simulating live process data
//
// Address space:
//   Objects
//     +-- Factory
//     |     +-- CNC_Machine_01  (MachineType)
//     |     |     +-- MainMotor  (MotorType)
//     |     |     |     +-- Speed        (Double)  [0..6000 rpm]  ReadOnly
//     |     |     |     +-- Temperature  (Double)  [0..150 degC]  ReadOnly
//     |     |     |     +-- Running      (Boolean)                ReadOnly
//     |     |     +-- State        (String)                       ReadOnly
//     |     |     +-- CycleCount   (Int64)                        ReadOnly
//     |     |     +-- SerialNumber (String)                       ReadOnly
//     |     |     +-- Setpoints    (Double[4])  exposeElements    Writable
//     |     |     +-- Reset        (Method)
//     |     |
//     |     +-- CNC_Machine_02  (MachineType)
//     |     |     +-- (same structure as Machine_01)
//     |     |
//     |     +-- FactoryStatus  (FactoryStatusType - Struct)
//     |     |     +-- PlantName       (String)
//     |     |     +-- MachinesOnline  (Int32)
//     |     |     +-- TotalCycles     (Int64)
//     |     |
//     |     +-- EnvironmentData
//     |           +-- AmbientTemp     (Double)  [0..50 degC]
//     |           +-- Humidity        (Double)  [0..100 %]
//     |           +-- Readings        (Double[6])  exposeElements  ReadOnly
//     |
//     +-- Parameters
//           +-- MaxSpeed       (Double)  OnWrite validates 0..6000
//           +-- EmergencyStop  (Boolean) OnWrite logs to console
//           +-- BatchSize      (Int32)   OnWrite validates 1..1000
//
// Connect with any OPC UA client to: opc.tcp://localhost:48410
// ==============================================================================

using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Server;
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
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 19: Advanced Server     ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  A production-grade OPC UA server combining:                 ║");
Console.WriteLine("║    * Multiple namespaces (Company types + Application)       ║");
Console.WriteLine("║    * ObjectTypes with typed instances                        ║");
Console.WriteLine("║    * Scalar variables, arrays, exposeElements                ║");
Console.WriteLine("║    * Properties (EURange, EngineeringUnits)                  ║");
Console.WriteLine("║    * Structured DataTypes (Structs)                          ║");
Console.WriteLine("║    * Methods with input/output arguments                     ║");
Console.WriteLine("║    * OnRead/OnWrite callbacks with validation                ║");
Console.WriteLine("║    * User authentication with roles                          ║");
Console.WriteLine("║    * Session tracking and certificate validation             ║");
Console.WriteLine("║    * Continuous value push (simulated process data)          ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// =============================================================================
// Step 1: Configuration
// =============================================================================
// All server settings are defined in CreateConfig() below.
// See that function for a full description of every available option.
var config = CreateConfig();
PrintConfig(config);

// =============================================================================
// Step 2: Create server and configure users
// =============================================================================
using var server = new UaServer(LicenseUserName, LicenseSerial);


// Accept all certificates for development

// Accept all client certificates automatically.
// WARNING: Do NOT use this in production! Either implement your own validation
// logic here (inspect e.Certificate and e.Error, then set e.Accept = true or false),
// or remove this handler entirely -- the SDK will then automatically validate
// certificates against the PKI trust store (pki/trusted/certs/).
server.CertificateValidation += (sender, e) => e.Accept = true;

// Track client sessions
server.SessionCreated += (s, e) =>
    Console.WriteLine($"  >> Session opened: {e.SessionName} ({e.ClientUri})");
server.SessionClosed += (s, e) =>
    Console.WriteLine($"  >> Session closed: {e.SessionName}");

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
// Step 3: Register company namespace for reusable types
// =============================================================================
ushort nsCompany = server.AddNamespace("urn:mycompany:cnc:types");

Console.WriteLine($"  Namespace table:");
Console.WriteLine($"    ns=2  {config.NamespaceUri} (application)");
Console.WriteLine($"    ns={nsCompany}  urn:mycompany:cnc:types (company types)");
Console.WriteLine();

// =============================================================================
// Step 4: Define company-wide ObjectTypes
// =============================================================================
Console.WriteLine("-- Defining ObjectTypes ------------------------------------------");

var motorTypeId   = server.CreateObjectType("MotorType", ns: nsCompany);
var machineTypeId = server.CreateObjectType("MachineType", ns: nsCompany);

Console.WriteLine($"  MotorType    {motorTypeId}");
Console.WriteLine($"  MachineType  {machineTypeId}");

// =============================================================================
// Step 5: Define a StructType for factory status
// =============================================================================
var factoryStatusTypeId = server.CreateStructDataType("FactoryStatusType", nsCompany,
    ("PlantName",      DataTypeIds.String, null),
    ("MachinesOnline", DataTypeIds.Int32,  null),
    ("TotalCycles",    DataTypeIds.Int64,  null));

Console.WriteLine($"  FactoryStatusType  {factoryStatusTypeId}");
Console.WriteLine();

// =============================================================================
// Step 6: Build the address space
// =============================================================================
Console.WriteLine("-- Building address space ----------------------------------------");

var factory = server.CreateFolder("Factory", UaRolePermissions.WITHOUT_RESTRICTIONS);

// --- Helper: create a CNC machine instance ---
UaVariable<double>[] CreateMachine(
    UaFolder parent, string name, string serial,
    double initialSpeed, double initialTemp)
{
    var machine = server.CreateObject(parent, name, UaRolePermissions.WITHOUT_RESTRICTIONS, machineTypeId);

    // Motor sub-object with properties
    var motor = server.CreateObject(machine.NodeId, "MainMotor", UaRolePermissions.WITHOUT_RESTRICTIONS, motorTypeId);

    var speed = server.CreateVariable<double>(motor, "Speed",
        UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: initialSpeed, readOnly: true);
    speed.SetEURange(0, 6000);
    speed.SetEngineeringUnits("rpm", "Revolutions per minute");

    var temp = server.CreateVariable<double>(motor, "Temperature",
        UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: initialTemp, readOnly: true);
    temp.SetEURange(0, 150);
    temp.SetEngineeringUnits("degC", "Degrees Celsius");

    var running = server.CreateVariable<bool>(motor, "Running",
        UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: true, readOnly: true);

    // Machine-level variables
    var state = server.CreateVariable<string>(machine, "State",
        UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: "Running", readOnly: true);
    var cycles = server.CreateVariable<long>(machine, "CycleCount",
        UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 0L, readOnly: true);
    server.CreateVariable<string>(machine, "SerialNumber",
        UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: serial, readOnly: true);

    // Writable setpoints array with exposed elements
    var setpoints = server.CreateArrayVariable<double>(machine.NodeId, "Setpoints",
        initialValue: new double[] { 100.0, 200.0, 300.0, 400.0 },
        exposeElements: true);

    // Reset method
    var capturedName = name;
    var capturedState = state;
    var capturedRunning = running;
    server.CreateMethod(machine.NodeId, "Reset",
        (ISystemContext ctx, MethodState method, NodeId objectId,
         IList<object> inputArgs, IList<object> outputArgs) =>
        {
            server.SetValue($"Objects.Factory.{capturedName}.CycleCount", 0L);
            capturedState.Value = "Idle";
            capturedRunning.Value = false;
            Console.WriteLine($"  !! {capturedName} RESET by client");
            return ServiceResult.Good;
        }, UaRolePermissions.WITHOUT_RESTRICTIONS);

    Console.WriteLine($"  {machine.Path}");
    Console.WriteLine($"    Motor: Speed={speed.Value} rpm, Temp={temp.Value} degC");
    Console.WriteLine($"    Serial: {serial}, Setpoints: [100, 200, 300, 400]");

    return new[] { speed, temp };
}

var machine1Vars = CreateMachine(factory, "CNC_Machine_01", "SN-2025-001", 2400.0, 52.0);
var machine2Vars = CreateMachine(factory, "CNC_Machine_02", "SN-2025-002", 1800.0, 45.0);
Console.WriteLine();

// --- Factory status struct ---
var factoryStatus = server.CreateStructVariable(factory, "FactoryStatus", factoryStatusTypeId, UaRolePermissions.WITHOUT_RESTRICTIONS);
factoryStatus.SetField<string>("PlantName",      "MainFactory");
factoryStatus.SetField<int>   ("MachinesOnline", 2);
factoryStatus.SetField<long>  ("TotalCycles",    0L);

Console.WriteLine($"  {factoryStatus.Path}");
Console.WriteLine($"    PlantName=MainFactory, MachinesOnline=2");

// --- Environment data ---
var envFolder = server.CreateFolder(factory, "EnvironmentData", UaRolePermissions.WITHOUT_RESTRICTIONS);

var ambientTemp = server.CreateVariable<double>(envFolder, "AmbientTemp", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 21.5);
ambientTemp.SetEURange(0, 50);
ambientTemp.SetEngineeringUnits("degC", "Degrees Celsius");

var humidity = server.CreateVariable<double>(envFolder, "Humidity", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 45.0);
humidity.SetEURange(0, 100);
humidity.SetEngineeringUnits("%", "Percent relative humidity");

var readings = server.CreateArrayVariable<double>(envFolder, "Readings",
    initialValue: new double[] { 21.5, 21.3, 21.7, 21.4, 21.6, 21.5 },
    readOnly: true, exposeElements: true);

Console.WriteLine($"  {envFolder.Path}");
Console.WriteLine($"    AmbientTemp=21.5 degC, Humidity=45.0 %");
Console.WriteLine();

// =============================================================================
// Step 7: Writable parameters with validation
// =============================================================================
Console.WriteLine("-- Writable parameters with validation ---------------------------");

var paramFolder = server.CreateFolder("Parameters", UaRolePermissions.WITHOUT_RESTRICTIONS);

var maxSpeed = server.CreateVariable<double>(paramFolder, "MaxSpeed", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 3000.0);
maxSpeed.SetEURange(0, 6000);
maxSpeed.SetEngineeringUnits("rpm", "Revolutions per minute");
maxSpeed.OnWrite = (newValue) =>
{
    if (newValue < 0 || newValue > 6000)
    {
        Console.WriteLine($"  !! MaxSpeed rejected: {newValue} (must be 0..6000)");
        return false;
    }
    Console.WriteLine($"  >> MaxSpeed accepted: {newValue}");
    return true;
};

var emergencyStop = server.CreateVariable<bool>(paramFolder, "EmergencyStop", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: false);
emergencyStop.OnWrite = (newValue) =>
{
    if (newValue)
        Console.WriteLine("  !! EMERGENCY STOP ACTIVATED by client");
    else
        Console.WriteLine("  >> Emergency stop released");
    return true;
};

var batchSize = server.CreateVariable<int>(paramFolder, "BatchSize", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 100);
batchSize.OnWrite = (newValue) =>
{
    if (newValue < 1 || newValue > 1000)
    {
        Console.WriteLine($"  !! BatchSize rejected: {newValue} (must be 1..1000)");
        return false;
    }
    return true;
};

// Computed value: reads MaxSpeed and converts to m/s (assuming 0.1m radius)
var maxLinearSpeed = server.CreateVariable<double>(paramFolder, "MaxLinearSpeed",
    UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 0.0, readOnly: true);
maxLinearSpeed.SetEngineeringUnits("m/s", "Meters per second");
maxLinearSpeed.OnRead = (current) =>
    Math.Round(maxSpeed.Value * 2.0 * Math.PI * 0.1 / 60.0, 3);

Console.WriteLine($"  {maxSpeed.Path,-45} OnWrite validates 0..6000");
Console.WriteLine($"  {emergencyStop.Path,-45} OnWrite logs to console");
Console.WriteLine($"  {batchSize.Path,-45} OnWrite validates 1..1000");
Console.WriteLine($"  {maxLinearSpeed.Path,-45} OnRead computes from MaxSpeed");
Console.WriteLine();

// =============================================================================
// Step 8: Run the server
// =============================================================================
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║");
Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Connect anonymously - full read/write access                ║");
Console.WriteLine("║  Try:                                                        ║");
Console.WriteLine("║  * Browse Factory -> CNC_Machine_01 -> MainMotor             ║");
Console.WriteLine("║  * Check EURange and EngineeringUnits on Speed               ║");
Console.WriteLine("║  * Write Setpoints[2] = 999 (writable)                       ║");
Console.WriteLine("║  * Call CNC_Machine_01/Reset method                          ║");
Console.WriteLine("║  * Write Parameters/MaxSpeed = 5000 (accepted)               ║");
Console.WriteLine("║  * Write Parameters/MaxSpeed = 9999 (rejected)               ║");
Console.WriteLine("║  * Read Parameters/MaxLinearSpeed (computed)                 ║");
Console.WriteLine("║  * Write Parameters/EmergencyStop = true                     ║");
Console.WriteLine("║  * Browse FactoryStatus struct fields                        ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to start the simulation loop.                   ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();

// =============================================================================
// Step 9: Simulation loop
// =============================================================================
Console.WriteLine("Simulating process data... (CTRL+C to exit)");
Console.WriteLine();

var rng = new Random();

while (true)
{
    bool eStop = emergencyStop.Value;

    // Machine 1
    if (!eStop)
    {
        machine1Vars[0].Value = Math.Round(2200.0 + rng.NextDouble() * 400.0, 1);  // Speed
        machine1Vars[1].Value = Math.Round(48.0 + rng.NextDouble() * 10.0, 1);     // Temp
    }

    // Machine 2
    if (!eStop)
    {
        machine2Vars[0].Value = Math.Round(1600.0 + rng.NextDouble() * 400.0, 1);
        machine2Vars[1].Value = Math.Round(42.0 + rng.NextDouble() * 8.0, 1);
    }

    // Cycle count - read current value from node, increment, write back
    // This respects Reset (which sets the node to 0 via server.SetValue)
    if (!eStop)
    {
        long c1 = server.GetValue<long>("Objects.Factory.CNC_Machine_01.CycleCount");
        long c2 = server.GetValue<long>("Objects.Factory.CNC_Machine_02.CycleCount");
        server.SetValue("Objects.Factory.CNC_Machine_01.CycleCount", c1 + rng.Next(1, 5));
        server.SetValue("Objects.Factory.CNC_Machine_02.CycleCount", c2 + rng.Next(1, 3));
    }

    // Environment
    ambientTemp.Value = Math.Round(20.0 + rng.NextDouble() * 3.0, 1);
    humidity.Value    = Math.Round(40.0 + rng.NextDouble() * 20.0, 1);
    readings.Value = new double[]
    {
        Math.Round(20.0 + rng.NextDouble() * 3.0, 1),
        Math.Round(20.0 + rng.NextDouble() * 3.0, 1),
        Math.Round(20.0 + rng.NextDouble() * 3.0, 1),
        Math.Round(20.0 + rng.NextDouble() * 3.0, 1),
        Math.Round(20.0 + rng.NextDouble() * 3.0, 1),
        Math.Round(20.0 + rng.NextDouble() * 3.0, 1)
    };

    // Update factory status struct
    factoryStatus.SetField<int> ("MachinesOnline", eStop ? 0 : 2);
    factoryStatus.SetField<long>("TotalCycles",
        server.GetValue<long>("Objects.Factory.CNC_Machine_01.CycleCount") +
        server.GetValue<long>("Objects.Factory.CNC_Machine_02.CycleCount"));

    long displayCycles = server.GetValue<long>("Objects.Factory.CNC_Machine_01.CycleCount") +
                         server.GetValue<long>("Objects.Factory.CNC_Machine_02.CycleCount");

    Console.Write($"\r  M1: {machine1Vars[0].Value,7:F1}rpm {machine1Vars[1].Value,5:F1}C  " +
                  $"M2: {machine2Vars[0].Value,7:F1}rpm {machine2Vars[1].Value,5:F1}C  " +
                  $"Cycles={displayCycles,-8} {(eStop ? "E-STOP!" : "       ")}");
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
        ApplicationName  = "PLCcom Workshop 19 - Advanced Server",
        ApplicationUri   = "urn:localhost:PLCcom:Workshop:19",
        ProductUri       = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
        NamespaceUri     = "http://indi-an.com/opcua/workshop/advanced-server",

        // ── ServerStatus/BuildInfo ────────────────────────────────────────────
        ManufacturerName = "My Company GmbH",
        ProductName      = "CNC Factory Server",
        SoftwareVersion  = "2.0.0",
        BuildNumber      = "100",
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
        VendorProductName    = "CNC Factory Server",
        VendorProductVersion = "2.0.0",

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

    // One default HTTPS/TLS certificate (SubjectAltName auto-generated: localhost + machine + IPs).
    // This is also where you would plug in an officially issued certificate instead.
    var httpsDefault = new UaServerCertificate(
        pkiBase:        @".\pki",
        password:       "secretpassword",
        alias:          "https-default",
        applicationUri: "urn:https-default:https",
        validityDays:   720,
        organisation:   "Indi.An GmbH",
        role:           UaServerCertificate.CertificateRole.Https);
    certs.Add(httpsDefault);
    // Present this certificate on every opc.https port. To force a different certificate on a
    // specific port — e.g. an externally port-forwarded port that must present an official domain
    // certificate — use: config.AssignHttpsCertificateToPort(48443, officialCert);
    config.SetDefaultHttpsCertificate(httpsDefault);

    // Make every certificate ready *in place* (load existing or build a new one) so the object
    // identity is preserved.
    foreach (var cert in certs)
    {
        cert.LoadWithResultInPlace();
        if (!cert.IsReady || !cert.CheckValidity())
            cert.Build(overwrite: true);
    }

    config.SetCertificateStore(UaServerCertificateStore.Load(@".\pki", certs));

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
