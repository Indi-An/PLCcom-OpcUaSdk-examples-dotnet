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
// PLCcom OPC UA Server SDK - Workshop 14: Variables and Arrays
//
// This workshop demonstrates the full range of variable features available
// in the PLCcom Server SDK. While Workshop 11 introduced basic variables,
// this example goes deeper into:
//
//   1. All scalar data types supported by OPC UA
//   2. Properties — EURange and EngineeringUnits (metadata for HMI/SCADA)
//   3. OnRead / OnWrite callbacks for custom validation and computed values
//   4. Arrays with exposeElements — each array element as a browsable child
//   5. Read-only variables and write rejection via OnWrite
//
// The address space built here:
//   Objects
//     +-- Scalars
//     |     +-- MyBool       (Boolean)    = true
//     |     +-- MyByte       (Byte)       = 42
//     |     +-- MySByte      (SByte)      = -7
//     |     +-- MyInt16      (Int16)      = -1000
//     |     +-- MyUInt16     (UInt16)     = 5000
//     |     +-- MyInt32      (Int32)      = 100000
//     |     +-- MyUInt32     (UInt32)     = 200000
//     |     +-- MyInt64      (Int64)      = 9876543210
//     |     +-- MyUInt64     (UInt64)     = 1234567890
//     |     +-- MyFloat      (Float)      = 3.14
//     |     +-- MyDouble     (Double)     = 2.71828
//     |     +-- MyString     (String)     = "Hello OPC UA"
//     |     +-- MyDateTime   (DateTime)   = now
//     |     +-- MyGuid       (Guid)       = random
//     |     +-- MyByteString (ByteString) = [0xDE, 0xAD, 0xBE, 0xEF]
//     |
//     +-- Properties
//     |     +-- Temperature  (Double)     = 22.5
//     |     |     +-- EURange            [0 .. 100]
//     |     |     +-- EngineeringUnits   "degC"
//     |     +-- Pressure     (Double)     = 1.013
//     |     |     +-- EURange            [0 .. 10]
//     |     |     +-- EngineeringUnits   "bar"
//     |     +-- Speed        (Double)     = 1500
//     |           +-- EURange            [0 .. 3000]
//     |           +-- EngineeringUnits   "rpm"
//     |
//     +-- Callbacks
//     |     +-- Computed     (Double)     OnRead returns Temperature * 1.8 + 32
//     |     +-- Validated    (Int32)      OnWrite rejects values outside 0..100
//     |     +-- Counter      (Int32)      [ReadOnly] incremented by server
//     |
//     +-- Arrays
//           +-- Temperatures (Double[5])  plain array
//           +-- Setpoints    (Double[4])  exposeElements -> V[0]..V[3]
//           +-- Flags        (Boolean[3]) exposeElements -> V[0]..V[2]
//
// What you will learn:
//   * All OPC UA scalar data types and how to create them
//   * How EURange and EngineeringUnits help HMI/SCADA clients
//   * How OnRead computes values on-the-fly when a client reads
//   * How OnWrite validates and rejects invalid client writes
//   * How exposeElements creates browsable child nodes for array elements
//   * How to push array values from a background loop
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
// TODO: Replace with your license credentials from your license e-mail
string LicenseUserName = "<Enter your UserName here>";
string LicenseSerial = "<Enter your Serial here>";

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 14: Variables & Arrays  ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This example demonstrates:                                  ║");
Console.WriteLine("║    * All OPC UA scalar data types                            ║");
Console.WriteLine("║    * EURange and EngineeringUnits properties                 ║");
Console.WriteLine("║    * OnRead / OnWrite callbacks                              ║");
Console.WriteLine("║    * Arrays with exposeElements (browsable child nodes)      ║");
Console.WriteLine("║    * Read-only variables and write validation                ║");
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

// WriteValidation — called BEFORE any client write is committed to the address space.
// All internal checks (AccessLevel, DataType, Permissions) have already passed.
// The handler receives ALL items of the write request as a batch.
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
// Step 2: Scalar data types
// =============================================================================
Console.WriteLine("-- Part A: Scalar data types ------------------------------------");

var scalars = server.CreateFolder("Scalars", UaRolePermissions.WITHOUT_RESTRICTIONS);

var vBool       = server.CreateVariable<bool>(scalars,     "MyBool", UaRolePermissions.WITHOUT_RESTRICTIONS, true);
var vByte       = server.CreateVariable<byte>(scalars,     "MyByte", UaRolePermissions.WITHOUT_RESTRICTIONS, 42);
var vSByte      = server.CreateVariable<sbyte>(scalars,    "MySByte", UaRolePermissions.WITHOUT_RESTRICTIONS, -7);
var vInt16      = server.CreateVariable<short>(scalars,    "MyInt16", UaRolePermissions.WITHOUT_RESTRICTIONS, -1000);
var vUInt16     = server.CreateVariable<ushort>(scalars,   "MyUInt16", UaRolePermissions.WITHOUT_RESTRICTIONS, 5000);
var vInt32      = server.CreateVariable<int>(scalars,      "MyInt32", UaRolePermissions.WITHOUT_RESTRICTIONS, 100000);
var vUInt32     = server.CreateVariable<uint>(scalars,     "MyUInt32", UaRolePermissions.WITHOUT_RESTRICTIONS, 200000u);
var vInt64      = server.CreateVariable<long>(scalars,     "MyInt64", UaRolePermissions.WITHOUT_RESTRICTIONS, 9876543210L);
var vUInt64     = server.CreateVariable<ulong>(scalars,    "MyUInt64", UaRolePermissions.WITHOUT_RESTRICTIONS, 1234567890UL);
var vFloat      = server.CreateVariable<float>(scalars,    "MyFloat", UaRolePermissions.WITHOUT_RESTRICTIONS, 3.14f);
var vDouble     = server.CreateVariable<double>(scalars,   "MyDouble", UaRolePermissions.WITHOUT_RESTRICTIONS, 2.71828);
var vString     = server.CreateVariable<string>(scalars,   "MyString", UaRolePermissions.WITHOUT_RESTRICTIONS, "Hello OPC UA");
var vDateTime   = server.CreateVariable<DateTime>(scalars, "MyDateTime", UaRolePermissions.WITHOUT_RESTRICTIONS, DateTime.UtcNow);
var vGuid       = server.CreateVariable<Guid>(scalars,     "MyGuid",       UaRolePermissions.WITHOUT_RESTRICTIONS, Guid.NewGuid());
var vByteString = server.CreateVariable<byte[]>(scalars,   "MyByteString", UaRolePermissions.WITHOUT_RESTRICTIONS, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

Console.WriteLine($"  Boolean     {vBool.Path,-40} = {vBool.Value}");
Console.WriteLine($"  Byte        {vByte.Path,-40} = {vByte.Value}");
Console.WriteLine($"  SByte       {vSByte.Path,-40} = {vSByte.Value}");
Console.WriteLine($"  Int16       {vInt16.Path,-40} = {vInt16.Value}");
Console.WriteLine($"  UInt16      {vUInt16.Path,-40} = {vUInt16.Value}");
Console.WriteLine($"  Int32       {vInt32.Path,-40} = {vInt32.Value}");
Console.WriteLine($"  UInt32      {vUInt32.Path,-40} = {vUInt32.Value}");
Console.WriteLine($"  Int64       {vInt64.Path,-40} = {vInt64.Value}");
Console.WriteLine($"  UInt64      {vUInt64.Path,-40} = {vUInt64.Value}");
Console.WriteLine($"  Float       {vFloat.Path,-40} = {vFloat.Value}");
Console.WriteLine($"  Double      {vDouble.Path,-40} = {vDouble.Value}");
Console.WriteLine($"  String      {vString.Path,-40} = {vString.Value}");
Console.WriteLine($"  DateTime    {vDateTime.Path,-40} = {vDateTime.Value:u}");
Console.WriteLine($"  Guid        {vGuid.Path,-40} = {vGuid.Value}");
Console.WriteLine($"  ByteString  {vByteString.Path,-40} = {BitConverter.ToString(vByteString.Value)}");
Console.WriteLine();

// =============================================================================
// Step 3: Properties — EURange and EngineeringUnits
// =============================================================================
Console.WriteLine("-- Part B: Properties (EURange, EngineeringUnits) --------------");

var props = server.CreateFolder("Properties", UaRolePermissions.WITHOUT_RESTRICTIONS);

var temperature = server.CreateVariable<double>(props, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, 22.5);
temperature.SetEURange(0, 100);
temperature.SetEngineeringUnits("degC", "Degrees Celsius");

var pressure = server.CreateVariable<double>(props, "Pressure", UaRolePermissions.WITHOUT_RESTRICTIONS, 1.013);
pressure.SetEURange(0, 10);
pressure.SetEngineeringUnits("bar", "Bar");

var speed = server.CreateVariable<double>(props, "Speed", UaRolePermissions.WITHOUT_RESTRICTIONS, 1500.0);
speed.SetEURange(0, 3000);
speed.SetEngineeringUnits("rpm", "Revolutions per minute");

Console.WriteLine($"  {temperature.Path,-45} = {temperature.Value}  [0..100 degC]");
Console.WriteLine($"  {pressure.Path,-45} = {pressure.Value}  [0..10 bar]");
Console.WriteLine($"  {speed.Path,-45} = {speed.Value}  [0..3000 rpm]");
Console.WriteLine();

// =============================================================================
// Step 4: OnRead / OnWrite callbacks
// =============================================================================
Console.WriteLine("-- Part C: OnRead / OnWrite callbacks ---------------------------");

var callbacks = server.CreateFolder("Callbacks", UaRolePermissions.WITHOUT_RESTRICTIONS);

var computed = server.CreateVariable<double>(callbacks, "Computed", UaRolePermissions.WITHOUT_RESTRICTIONS, 0.0, readOnly: true);
computed.OnRead = (currentValue) =>
{
    return Math.Round(temperature.Value * 1.8 + 32.0, 2);
};

var validated = server.CreateVariable<int>(callbacks, "Validated", UaRolePermissions.WITHOUT_RESTRICTIONS, 50);
validated.OnWrite = (newValue) =>
{
    if (newValue < 0 || newValue > 100)
    {
        Console.WriteLine($"  !! Rejected write: {newValue} (must be 0..100)");
        return false;
    }
    return true;
};

var counter = server.CreateVariable<int>(callbacks, "Counter", UaRolePermissions.WITHOUT_RESTRICTIONS, 0, readOnly: true);

Console.WriteLine($"  {computed.Path,-45} OnRead -> Fahrenheit");
Console.WriteLine($"  {validated.Path,-45} OnWrite -> reject if not 0..100");
Console.WriteLine($"  {counter.Path,-45} [ReadOnly] server-incremented");
Console.WriteLine();

// =============================================================================
// Step 5: Arrays and exposeElements
// =============================================================================
Console.WriteLine("-- Part D: Arrays and exposeElements ----------------------------");

var arrays = server.CreateFolder("Arrays", UaRolePermissions.WITHOUT_RESTRICTIONS);

var temps = server.CreateArrayVariable<double>(arrays, "Temperatures",
    initialValue: new double[] { 20.0, 21.5, 22.0, 23.5, 24.0 });

var setpoints = server.CreateArrayVariable<double>(arrays, "Setpoints",
    initialValue: new double[] { 100.0, 200.0, 300.0, 400.0 },
    exposeElements: true);

var flags = server.CreateArrayVariable<bool>(arrays, "Flags",
    initialValue: new bool[] { true, false, true },
    exposeElements: true);

Console.WriteLine($"  {temps.Path,-45} Double[5]  (plain array)");
Console.WriteLine($"  {setpoints.Path,-45} Double[4]  (exposeElements)");
Console.WriteLine($"  {flags.Path,-45} Bool[3]    (exposeElements)");
Console.WriteLine();

// =============================================================================
// Step 6: Run the server
// =============================================================================
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║");
Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Try:                                                        ║");
Console.WriteLine("║  * Browse Scalars - all 15 OPC UA data types                 ║");
Console.WriteLine("║  * Browse Properties - check EURange and EngineeringUnits    ║");
Console.WriteLine("║  * Read Callbacks/Computed - shows Fahrenheit conversion     ║");
Console.WriteLine("║  * Write Callbacks/Validated - try 50 (OK) and 200 (reject)  ║");
Console.WriteLine("║  * Write Callbacks/Counter - should fail (ReadOnly)          ║");
Console.WriteLine("║  * Browse Arrays/Setpoints - see V[0]..V[3] child nodes      ║");
Console.WriteLine("║  * Subscribe to V[1] only - get changes for one element      ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to start the value push loop.                   ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();

// =============================================================================
// Step 7: Push value changes
// =============================================================================
Console.WriteLine("Pushing values every second... (CTRL+C to exit)");
Console.WriteLine();

var rng = new Random();
long cycle = 0;

while (true)
{
    cycle++;

    temperature.Value = Math.Round(18.0 + rng.NextDouble() * 12.0, 2);
    pressure.Value    = Math.Round(0.8 + rng.NextDouble() * 0.5, 3);
    speed.Value       = 1200.0 + rng.Next(600);
    counter.Value     = (int)cycle;

    temps.Value = new double[]
    {
        Math.Round(19.0 + rng.NextDouble() * 3.0, 1),
        Math.Round(20.0 + rng.NextDouble() * 3.0, 1),
        Math.Round(21.0 + rng.NextDouble() * 3.0, 1),
        Math.Round(22.0 + rng.NextDouble() * 3.0, 1),
        Math.Round(23.0 + rng.NextDouble() * 3.0, 1)
    };

    setpoints.Value = new double[]
    {
        100.0 + rng.Next(50),
        200.0 + rng.Next(50),
        300.0 + rng.Next(50),
        400.0 + rng.Next(50)
    };

    Console.Write($"\r  Cycle={cycle}  Temp={temperature.Value:F1}C " +
                  $"({computed.Value:F1}F)  P={pressure.Value:F3}bar  " +
                  $"Counter={counter.Value}  ");
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
        ApplicationName = "PLCcom Workshop 14 - Variables and Arrays",
        ApplicationUri  = "urn:localhost:PLCcom:Workshop:14",
        ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
        NamespaceUri    = "http://indi-an.com/opcua/workshop/variables-and-arrays",

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
