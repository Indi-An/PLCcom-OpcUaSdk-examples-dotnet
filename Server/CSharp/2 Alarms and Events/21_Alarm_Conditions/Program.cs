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
// PLCcom OPC UA Server SDK - Workshop 21: Alarm Conditions
//
// OPC UA Alarms & Conditions (Part 9) extends the event model with stateful
// alarms that clients can acknowledge and confirm.
//
// This workshop demonstrates all alarm types that OPC UA supports:
//
//   AlarmConditionType     - General alarm (active/inactive, ack/confirm)
//   ExclusiveLimitAlarmType - Limit alarm with levels: Low / High / HighHigh
//   DiscreteAlarmType      - Alarm triggered by a discrete (boolean) state
//   DialogConditionType    - Dialog asking the operator to choose a response
//
// Each type maps to a filter option in the client workshops (31/32/33):
//   Client filter "3 - Alarms"       -> AlarmConditionType
//   Client filter "4 - Limit alarms" -> ExclusiveLimitAlarmType
//   Client filter "5 - Discrete"     -> DiscreteAlarmType
//   Client filter "2 - Dialogs"      -> DialogConditionType
//   Client filter "1 - All"          -> all of the above
//
// Connect with any OPC UA client to: opc.tcp://localhost:48410
// ==============================================================================

using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Server.Sdk;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;

//TODO
//Submit your license information from your license e-mail
string LicenseUserName = "<Enter your UserName here>";
string LicenseSerial   = "<Enter your Serial here>";

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 21: Alarm Conditions    ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Demonstrates all OPC UA alarm types:                        ║");
Console.WriteLine("║  * AlarmConditionType     - general alarm (ack/confirm)      ║");
Console.WriteLine("║  * ExclusiveLimitAlarmType - limit levels Low/High/HighHigh  ║");
Console.WriteLine("║  * DiscreteAlarmType      - boolean state alarm              ║");
Console.WriteLine("║  * DialogConditionType    - operator response dialog         ║");
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

// -- Address space ---------------------------------------------------------
var plant   = server.CreateFolder("Plant", UaRolePermissions.WITHOUT_RESTRICTIONS);
var reactor = server.CreateFolder(plant, "Reactor", UaRolePermissions.WITHOUT_RESTRICTIONS);
server.EnableEvents(reactor);

// Process variables
var temperature = server.CreateVariable<double>(reactor, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 25.0);
var pressure    = server.CreateVariable<double>(reactor, "Pressure", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 1.0);
var pumpRunning = server.CreateVariable<bool>  (reactor, "PumpRunning", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: true);

temperature.SetEURange(0, 200);
temperature.SetEngineeringUnits("C");
pressure.SetEURange(0, 10);
pressure.SetEngineeringUnits("bar");

// -- Alarm type 1: AlarmConditionType (general alarm) ----------------------
// Triggered when temperature exceeds 80C (hysteresis: off at 70C)
var tempAlarm = server.CreateAlarm(reactor, "TemperatureHighAlarm");

// -- Alarm type 2: ExclusiveLimitAlarmType (limit levels) ------------------
// Pressure with three escalating levels:
//   High     (> 6 bar)  - warning
//   HighHigh (> 8 bar)  - critical, immediate action required
var pressLimitAlarm = server.CreateLimitAlarm(reactor, "PressureLimitAlarm");

// -- Alarm type 3: DiscreteAlarmType (boolean state) -----------------------
// Triggered when the pump stops unexpectedly
var pumpAlarm = server.CreateDiscreteAlarm(reactor, "PumpFailureAlarm");

// -- Alarm type 4: DialogConditionType (operator response) -----------------
// Periodically asks the operator to confirm a maintenance check
var maintenanceDialog = server.CreateDialog(reactor, "MaintenanceDialog",
    prompt:  "Scheduled maintenance check required. Confirm to proceed.",
    options: new[] { "Confirm", "Postpone 1h", "Postpone 4h" });

Console.WriteLine("  Reactor alarms:");
Console.WriteLine("    [AlarmCondition]     TemperatureHighAlarm  - active when T > 80C");
Console.WriteLine("    [ExclusiveLimitAlarm] PressureLimitAlarm   - High > 6bar, HighHigh > 8bar");
Console.WriteLine("    [DiscreteAlarm]      PumpFailureAlarm      - active when pump stops");
Console.WriteLine("    [DialogCondition]    MaintenanceDialog     - every 30s operator prompt");
Console.WriteLine();

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║");
Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to start the simulation.                        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();

Console.WriteLine("Simulating... (CTRL+C to exit)");
Console.WriteLine();

var rng = new Random();
bool tempActive  = false;
bool pressHigh   = false;
bool pressHH     = false;
bool pumpActive  = false;
bool dialogActive = false;
int  tick = 0;

while (true)
{
    tick++;

    // -- Simulate process values -------------------------------------------
    double t = 50.0 + Math.Sin(DateTime.UtcNow.Ticks * 0.0000001) * 40.0 + rng.NextDouble() * 5.0;
    double p = 1.0 + (t - 50.0) / 20.0 + rng.NextDouble() * 0.5;
    bool   pump = (tick % 20 != 0);   // pump stops briefly every 20 ticks

    temperature.Value = Math.Round(t, 1);
    pressure.Value    = Math.Round(p, 2);
    pumpRunning.Value = pump;

    // -- AlarmConditionType: temperature high alarm ------------------------
    if (t > 80.0 && !tempActive)
    {
        tempAlarm.Activate($"Temperature HIGH: {t:F1}C", EventSeverity.High);
        tempActive = true;
        Console.WriteLine($"  [AlarmCondition  ON ] Temperature = {t:F1}C");
    }
    else if (t < 70.0 && tempActive)
    {
        tempAlarm.Deactivate($"Temperature normal: {t:F1}C");
        tempActive = false;
        Console.WriteLine($"  [AlarmCondition  OFF] Temperature = {t:F1}C");
    }

    // -- ExclusiveLimitAlarmType: pressure with escalating levels ----------
    if (p > 8.0 && !pressHH)
    {
        pressLimitAlarm.Activate(LimitAlarmStates.HighHigh, $"Pressure HIGHHIGH: {p:F2} bar", EventSeverity.High);
        pressHH = true; pressHigh = true;
        Console.WriteLine($"  [LimitAlarm HighHigh] Pressure = {p:F2} bar");
    }
    else if (p > 6.0 && !pressHigh && !pressHH)
    {
        pressLimitAlarm.Activate(LimitAlarmStates.High, $"Pressure HIGH: {p:F2} bar", EventSeverity.MediumHigh);
        pressHigh = true;
        Console.WriteLine($"  [LimitAlarm High    ] Pressure = {p:F2} bar");
    }
    else if (p < 5.0 && (pressHigh || pressHH))
    {
        pressLimitAlarm.Deactivate($"Pressure normal: {p:F2} bar");
        pressHigh = false; pressHH = false;
        Console.WriteLine($"  [LimitAlarm OFF     ] Pressure = {p:F2} bar");
    }

    // -- DiscreteAlarmType: pump failure -----------------------------------
    if (!pump && !pumpActive)
    {
        pumpAlarm.Activate("Pump stopped unexpectedly", EventSeverity.High);
        pumpActive = true;
        Console.WriteLine("  [DiscreteAlarm   ON ] Pump stopped");
    }
    else if (pump && pumpActive)
    {
        pumpAlarm.Deactivate("Pump running normally");
        pumpActive = false;
        Console.WriteLine("  [DiscreteAlarm   OFF] Pump running");
    }

    // -- DialogConditionType: maintenance prompt every 30 ticks -----------
    if (tick % 30 == 0 && !dialogActive)
    {
        maintenanceDialog.Activate("Scheduled maintenance check required. Confirm to proceed.", EventSeverity.Medium);
        dialogActive = true;
        Console.WriteLine("  [Dialog          ON ] Maintenance check requested");
    }
    else if (dialogActive && tick % 30 == 5)
    {
        // Simulate operator responding "Confirm" (option 0) after 5 ticks
        maintenanceDialog.Respond(0);
        dialogActive = false;
        Console.WriteLine("  [Dialog          OFF] Operator confirmed maintenance");
    }

    Console.Write($"\r  T={temperature.Value:F1}C{(tempActive ? "!" : " ")}  " +
                  $"P={pressure.Value:F2}bar{(pressHH ? "!!" : pressHigh ? "! " : "  ")}  " +
                  $"Pump={pumpRunning.Value}  ");

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
        ApplicationName  = "PLCcom Workshop 21 - Alarm Conditions",
        ApplicationUri   = "urn:localhost:PLCcom:Workshop:21",
        ProductUri       = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
        NamespaceUri     = "http://indi-an.com/opcua/workshop/alarm-conditions",

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
