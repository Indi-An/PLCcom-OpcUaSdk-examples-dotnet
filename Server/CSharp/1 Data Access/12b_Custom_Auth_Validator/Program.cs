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
// PLCcom OPC UA Server SDK - Workshop 12b: Custom Auth Validator
//
// Workshop 12a used the built-in user database (AddUser) and OPC UA RolePermissions
// to control access. This workshop demonstrates the alternative approach:
//
//   IUaCredentialValidator  — replaces username/password validation entirely.
//                             No AddUser() calls needed.
//
//   IUaPermissionValidator  — replaces the built-in RolePermissions enforcement.
//                             No SetRolePermissions() on nodes needed.
//                             Nodes are created with ALL_RESTRICTIONS by default.
//
// The same three users and the same access rules as Workshop 12a are implemented,
// but entirely in custom validator classes — no OPC UA role concepts involved.
//
// Users:
//   admin    / admin123    -> full access  (read, write, call)
//   operator / operator123 -> read + write + call
//   viewer   / viewer123   -> read only
//
// Connect with any OPC UA client to: opc.tcp://localhost:48410
// ==============================================================================

using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Server;
using PLCcom.Opc.Ua.Server.Sdk;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

// -- License -------------------------------------------------------------------
string LicenseUserName = "<Enter your UserName here>";
string LicenseSerial   = "<Enter your Serial here>";

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 12b: Custom Validator   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Same users and access rules as Workshop 12a, but using      ║");
Console.WriteLine("║  IUaCredentialValidator and IUaPermissionValidator instead   ║");
Console.WriteLine("║  of AddUser() and RolePermissions.                           ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║    admin    / admin123    -> full access                     ║");
Console.WriteLine("║    operator / operator123 -> read + write + call             ║");
Console.WriteLine("║    viewer   / viewer123   -> read only                       ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Anonymous access is disabled - you MUST log in.             ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

var config = CreateConfig();
PrintConfig(config);

using var server = new UaServer(LicenseUserName, LicenseSerial);
server.UserManager.CredentialValidator = new MyCredentialValidator();
server.UserManager.PermissionValidator = new MyPermissionValidator();

// Accept all client certificates automatically.
// WARNING: Do NOT use this in production! Either implement your own validation
// logic here (inspect e.Certificate and e.Error, then set e.Accept = true or false),
// or remove this handler entirely -- the SDK will then automatically validate
// certificates against the PKI trust store (pki/trusted/certs/).
server.CertificateValidation += (sender, e) => { e.Accept = true; };

server.SessionCreated += (s, e) => Console.WriteLine($"  [SESSION+] {e.SessionName ?? "unknown"}");
server.SessionClosed  += (s, e) => Console.WriteLine($"  [SESSION-] {e.SessionName ?? "unknown"}");
server.ValuesWritten  += (s, e) =>
{
    foreach (var item in e.Items)
        Console.WriteLine($"  << OPC Write: {item.Path} = {item.Value}");
};

Console.Write("Starting server ... ");
try { server.Start(config); }
catch (Exception ex) { Console.WriteLine("FAILED"); Console.WriteLine(ex.Message); Console.ReadLine(); return; }
Console.WriteLine("OK");
Console.WriteLine();

// When using IUaPermissionValidator, nodes must be created WITHOUT_RESTRICTIONS.
// The PermissionValidator takes full control of access decisions — the stack
// must not pre-filter via RolePermissions before ValidateRolePermissions is called.
var plant = server.CreateFolder("Plant", UaRolePermissions.WITHOUT_RESTRICTIONS);
var temp  = server.CreateVariable<double>(plant, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 22.0);
var rpm   = server.CreateVariable<int>(plant, "RPM", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 1500);

server.CreateMethod(plant, "Reset",
    (session, context, objectId, inputArgs, outputArgs) =>
    {
        temp.Value = 22.0;
        rpm.Value  = 1500;
        Console.WriteLine("  << Reset called");
        return ServiceResult.Good;
    },
    UaRolePermissions.WITHOUT_RESTRICTIONS);

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running - authentication required.                ║");
Console.WriteLine("║  * viewer/viewer123   -> read only                           ║");
Console.WriteLine("║  * operator/operator123 -> read + write + call               ║");
Console.WriteLine("║  * admin/admin123     -> full access                         ║");
Console.WriteLine("║  Press ENTER to exit.                                        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();

// =============================================================================
// Helper: CreateConfig
// NOTE: Must appear before class declarations (CS8803).
// =============================================================================
static UaServerConfiguration CreateConfig()
{
    var config = new UaServerConfiguration
    {
        ApplicationName  = "PLCcom Workshop 12b - Custom Auth Validator",
        ApplicationUri   = "urn:localhost:PLCcom:Workshop:12b",
        ProductUri       = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
        NamespaceUri     = "http://indi-an.com/opcua/workshop/custom-auth-validator",
        ManufacturerName = "My Company GmbH",
        ProductName      = "My OPC UA Server",
        SoftwareVersion  = "1.0.0",
        BuildNumber      = "42",
        BaseAddresses = new List<string>
        {
            "opc.tcp://localhost:48410",
            "opc.https://localhost:48411"
        },
        SecurityPolicies  = UaServer.GetRecommendedSecurityPolicies(),
        UserTokenPolicies = new List<UserTokenPolicy>
        {
            new UserTokenPolicy { TokenType = UserTokenType.UserName },
            new UserTokenPolicy { TokenType = UserTokenType.Certificate }
        },
        AutoAcceptUntrustedCertificates = false,
        // AsConfigured (default) = endpoints use exactly the host from BaseAddresses
        // NormalizeToHostname    = replace localhost/127.0.0.1 with the machine name
        // None = no normalization, behavior depends on DNS and network settings
        EndpointHostMode = EndpointHostMode.AsConfigured,
        MaxSessionCount = 100,
        ShutdownDelay   = 5,
        VendorName           = "My Company GmbH",
        VendorProductName    = "My OPC UA Server",
        VendorProductVersion = "1.0.0",
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

// =============================================================================
// Custom validator implementations
// NOTE: class declarations must come after all top-level statements (CS8803).
// =============================================================================

class MyCredentialValidator : IUaCredentialValidator
{
    private static readonly Dictionary<string, string> s_users = new(StringComparer.Ordinal)
    {
        { "admin",    "admin123"    },
        { "operator", "operator123" },
        { "viewer",   "viewer123"   }
    };

    public bool ValidateCredentials(string userName, string password)
    {
        bool ok = s_users.TryGetValue(userName, out string expected) && expected == password;
        Console.WriteLine($"  [AUTH] {userName} -> {(ok ? "accepted" : "rejected")}");
        return ok;
    }

    public bool ValidateCertificate(X509Certificate2 certificate)
    {
        Console.WriteLine($"  [AUTH CERT] {certificate.Subject} -> accepted");
        return true;
    }
}

class MyPermissionValidator : IUaPermissionValidator
{
    public bool ValidatePermission(UaSessionContext session, UaNodeContext node, UaPermissionCheck check)
    {
        string user = session.UserName;
        bool allowed;

        if (user == "admin")
            allowed = true;
        else if (user == "operator")
            allowed = check != UaPermissionCheck.HistoryWrite;
        else if (user == "viewer")
            allowed = check == UaPermissionCheck.Browse
                   || check == UaPermissionCheck.Read
                   || check == UaPermissionCheck.Subscribe
                   || check == UaPermissionCheck.ReadRolePermissions;
        else
            allowed = false;

        Console.WriteLine($"  [PERM] {user,-10} {check,-25} {node.Path ?? node.NodeId.ToString(),-35} -> {(allowed ? "ALLOW" : "DENY")}");
        return allowed;
    }
}
