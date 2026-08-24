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
// PLCcom OPC UA Server SDK - Workshop 12: User Authentication
//
// Workshop 11 allowed anonymous access - anyone could connect and write values.
// In production, you need to control who can connect and what they can do.
//
// OPC UA supports three authentication methods:
//   Anonymous   - no login required (disabled in this example)
//   UserName    - classic username + password
//   Certificate - X.509 client certificate (machine-to-machine)
//
// Each authenticated user is assigned one or more roles that control access:
//   Engineer  - full access (read, write, browse, call methods)
//   Operator  - read + write, no configuration changes
//   Observer  - read-only (writes are rejected with BadUserAccessDenied)
//
// This workshop demonstrates:
//   * How to require user authentication (no anonymous access)
//   * How to add users with different roles
//   * How roles affect write permissions on variables
//   * How to handle X.509 user certificate validation
//   * How to track session lifecycle (connect/disconnect)
//
// Test scenario:
//   1. Try connecting without credentials -> rejected
//   2. Connect as viewer/viewer123 -> can read, cannot write
//   3. Connect as operator/operator123 -> can read and write
//   4. Connect as admin/admin123 -> full access
//
// Connect with any OPC UA client to: opc.tcp://localhost:48410
// ==============================================================================

using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Server;
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
string LicenseSerial   = "";

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 12: User Authentication ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Workshop 11 allowed anonymous access - anyone could write.  ║");
Console.WriteLine("║  This example requires authentication and assigns roles:     ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║    admin    / admin123    -> Engineer  (full access)         ║");
Console.WriteLine("║    operator / operator123 -> Operator  (read + write)        ║");
Console.WriteLine("║    viewer   / viewer123   -> Observer  (read-only)           ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Anonymous access is disabled - you MUST log in.             ║");
Console.WriteLine("║  Try writing Temperature as viewer -> rejected.              ║");
Console.WriteLine("║  Try writing Temperature as admin  -> accepted.              ║");
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
// Step 2: Create server and add users with roles
// =============================================================================
// OPC UA defines well-known roles (Part 18). Each user is assigned one or more:
//
//   Role.Observer   - intended for read-only access (browse, read, subscribe)
//   Role.Operator   - intended for read + write + method calls
//   Role.Engineer   - intended for full access including configuration
//   Role.Supervisor - intended for read + method calls (no write)
//
//   Role.AuthenticatedUser - any successfully authenticated user,
//                            regardless of username or credentials
//   Role.Anonymous         - always assigned, even without login
//
// IMPORTANT: Roles are labels only. The OPC UA stack does NOT enforce
// permissions automatically unless RolePermissions are explicitly set
// on each node via SetRolePermissions() - see Step 4 below.
// Without SetRolePermissions(), all authenticated users have identical
// access regardless of their assigned role.
using var server = new UaServer(LicenseUserName, LicenseSerial);

server.AddUser("admin",    "admin123",    Role.Engineer);
server.AddUser("operator", "operator123", Role.Operator);
server.AddUser("viewer",   "viewer123",   Role.Observer);

Console.WriteLine("── Users ───────────────────────────────────────────────────");
Console.WriteLine("  admin    / admin123    -> Engineer  (full access)");
Console.WriteLine("  operator / operator123 -> Operator  (read + write)");
Console.WriteLine("  viewer   / viewer123   -> Observer  (read-only)");
Console.WriteLine();

// =============================================================================
// Step 3: Handle certificate validation and session events
// =============================================================================

// Accept all client certificates automatically.
// WARNING: Do NOT use this in production! Either implement your own validation
// logic here (inspect e.Certificate and e.Error, then set e.Accept = true or false),
// or remove this handler entirely -- the SDK will then automatically validate
// certificates against the PKI trust store (pki/trusted/certs/).

server.CertificateValidation += (sender, e) =>
{
    Console.WriteLine($"  [CERT] {e.Certificate.Subject} -> Accepted");
    e.Accept = true;
};

server.UserManager.CertificateValidation += (sender, e) =>
{
    Console.WriteLine($"  [USER CERT] {e.Certificate.Subject} -> Accepted");
    e.Accept = true;
};

server.SessionCreated += (s, e) =>
    Console.WriteLine($"  [SESSION+] {e.SessionName ?? "unknown"} from {e.ClientUri ?? "unknown"}");
server.SessionClosed += (s, e) =>
    Console.WriteLine($"  [SESSION-] {e.SessionName ?? "unknown"}");

server.ValuesWritten += (s, e) =>
{
    foreach (var item in e.Items)
        Console.WriteLine($"  << OPC Write: {item.Path} ({item.NodeId}) = {item.Value}");
};

// =============================================================================
// Step 4: Start server and create test variables
// =============================================================================
Console.Write("Starting server ... ");
try { server.Start(config); }
catch (Exception ex) { Console.WriteLine("FAILED"); Console.WriteLine(ex.Message); Console.ReadLine(); return; }
Console.WriteLine("OK");
Console.WriteLine();

// =============================================================================
// Step 4: Build address space with role-based permissions
// =============================================================================
// permissions parameter activates OPC UA role enforcement directly on creation.
// Without permissions, all authenticated users have identical access.
//
// AllowRead()      - grants Browse + Read + Subscribe
// AllowReadWrite() - grants Browse + Read + Write + Subscribe + Call
// AllowAll()       - grants all permissions
var rolePermissions = new UaRolePermissions()
    .AllowRead(Role.Observer)
    .AllowReadWrite(Role.Operator)
    .AllowAll(Role.Engineer);

var plant = server.CreateFolder("Plant", UaRolePermissions.WITHOUT_RESTRICTIONS);
var temp  = server.CreateVariable<double>(plant, "Temperature", rolePermissions, initialValue: 22.0);
var rpm   = server.CreateVariable<int>(plant, "RPM", rolePermissions, initialValue: 1500);

// Observer may browse Reset but not call it — AllowRead grants Browse without Call
NodeId resetMethodId = server.CreateMethod(plant, "Reset",
    (session, context, objectId, inputArgs, outputArgs) =>
    {
        temp.Value = 22.0;
        rpm.Value  = 1500;
        Console.WriteLine($"  << Reset called");
        return ServiceResult.Good;
    },
    rolePermissions);

Console.WriteLine("── Address space ────────────────────────────────────────────");
Console.WriteLine($"  Double  {temp.Path,-40} {temp.NodeId}  = 22.0");
Console.WriteLine($"  Int32   {rpm.Path,-40} {rpm.NodeId}  = 1500");
Console.WriteLine($"  Method  Objects.Plant.Reset                      {resetMethodId}  (Operator + Engineer only)");
Console.WriteLine();

// =============================================================================
// Step 5: Connect and test role-based access
// =============================================================================
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running - authentication required.                ║");
Console.WriteLine("║  Endpoint: opc.tcp://localhost:48410                         ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Test role-based access:                                     ║");
Console.WriteLine("║  * Connect without credentials        -> rejected            ║");
Console.WriteLine("║  * viewer/viewer123 -> read OK, write -> BadUserAccessDenied ║");
Console.WriteLine("║  * operator/operator123 -> read + write OK                   ║");
Console.WriteLine("║  * admin/admin123       -> full access                       ║");
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
        ApplicationName = "PLCcom Workshop 12 - User Authentication",
        ApplicationUri  = "urn:localhost:PLCcom:Workshop:12",
        ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
        NamespaceUri    = "http://indi-an.com/opcua/workshop/user-authentication",

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
        // IMPORTANT: Anonymous is intentionally NOT listed here.
        // Clients that try to connect without credentials will be rejected.
        // Users and their roles are added via server.AddUser() in the main code.
        //   UserName    - username + password (see server.AddUser() calls above)
        //   Certificate - X.509 client certificate (see server.UserManager)
        UserTokenPolicies = new List<UserTokenPolicy>
        {
            new UserTokenPolicy { TokenType = UserTokenType.UserName },
            new UserTokenPolicy { TokenType = UserTokenType.Certificate }
        },

        AutoAcceptUntrustedCertificates = false,

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
        MaxNodesPerRead                          = 1000,
        MaxNodesPerWrite                         = 1000,
        MaxNodesPerBrowse                        = 1000,
        MaxNodesPerHistoryReadData               = 100,
        MaxNodesPerHistoryReadEvents             = 100,
        MaxNodesPerHistoryUpdateData             = 100,
        MaxNodesPerHistoryUpdateEvents           = 100,
        MaxNodesPerMethodCall                    = 200,
        MaxNodesPerRegisterNodes                 = 1000,
        MaxNodesPerTranslateBrowsePathsToNodeIds = 1000,
        MaxNodesPerNodeManagement                = 1000,
        MaxMonitoredItemsPerCall                 = 1000,
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
