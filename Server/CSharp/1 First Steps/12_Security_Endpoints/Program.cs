// ==============================================================================
// PLCcom OPC UA Server SDK - Workshop 12: Security & Endpoints
//
// OPC UA security works on two levels:
//   1. Transport security: encrypts and signs the communication channel
//      using X.509 certificates and configurable security policies.
//   2. User authentication: verifies who is connecting (Anonymous,
//      Username/Password, or X.509 user certificate).
//
// What you will learn:
//   * How to configure security policies (encryption algorithms)
//   * How to add users with different roles (Engineer, Operator, Observer)
//   * How to handle certificate validation events
//   * How to track session lifecycle (connect/disconnect)
//
// Connect with any OPC UA client to:
//   opc.tcp://localhost:48411
// ==============================================================================

using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Server;
using PLCcom.Opc.Ua.Server.Sdk;
using System;
using System.Collections.Generic;

// -- License -------------------------------------------------------------------
//TODO
//Submit your license information from your license e-mail
string LicenseUserName = "<Enter your UserName here>";
string LicenseSerial = "<Enter your Serial here>";

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 12: Security            ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This example demonstrates:                                  ║");
Console.WriteLine("║  * Security policies (None, RSA, optionally ECC)             ║");
Console.WriteLine("║  * User authentication (Anonymous, Username, Certificate)    ║");
Console.WriteLine("║  * User roles (Engineer, Operator, Observer)                 ║");
Console.WriteLine("║  * Certificate validation events                             ║");
Console.WriteLine("║  * Session lifecycle events                                  ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// -- Step 1: Configure security policies ---------------------------------------
// Security policies define which encryption algorithms the server offers.
// Each policy creates one endpoint in the server's endpoint list.
// Clients choose the endpoint that matches their security requirements.
var config = new UaServerConfiguration
{
    ApplicationName = "PLCcom Workshop 12 - Security",
    ApplicationUri  = "urn:localhost:PLCcom:Workshop:12",
    ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
    BaseAddresses   = new List<string> { "opc.tcp://localhost:48411" },

    // GetRecommendedSecurityPolicies() returns:
    //   None (no encryption - for testing only)
    //   Basic256Sha256, Aes128_Sha256_RsaOaep, Aes256_Sha256_RsaPss
    //   each with Sign and SignAndEncrypt modes.
    //
    // Other options:
    //   UaServer.GetAllSecurityPolicies()        - includes ECC (platform-dependent)
    //   UaServer.GetDeprecatedSecurityPolicies() - Basic128Rsa15, Basic256 (legacy only)
    SecurityPolicies = UaServer.GetRecommendedSecurityPolicies(),

    // User token policies define which authentication methods clients can use
    UserTokenPolicies = new List<UserTokenPolicy>
    {
        new UserTokenPolicy { TokenType = UserTokenType.Anonymous },   // no login
        new UserTokenPolicy { TokenType = UserTokenType.UserName },    // user + password
        new UserTokenPolicy { TokenType = UserTokenType.Certificate }  // X.509 user cert
    },

    CertificateStorePath = @".\pki",

    // Server certificate lifetime - default is 60 months (5 years)
    CertificateLifetimeInMonths = 60
};

// Print all configured endpoints so you can see what the server offers
Console.WriteLine($"  Security Policies ({config.SecurityPolicies.Count} endpoints):");
foreach (var sp in config.SecurityPolicies)
    Console.WriteLine($"    * {sp.SecurityMode,-18} {UaServer.GetSecurityPolicyName(sp.SecurityPolicyUri)}");
Console.WriteLine();

// -- Step 2: Create server and add users ---------------------------------------
using var server = new UaServer(LicenseUserName, LicenseSerial);

// Add users with roles - roles control what the user can do:
//   Engineer  -> full access (read, write, browse, call methods)
//   Operator  -> read + write, no configuration changes
//   Observer  -> read-only access
server.AddUser("admin",    "admin123",    Role.Engineer);
server.AddUser("operator", "operator123", Role.Operator);
server.AddUser("viewer",   "viewer123",   Role.Observer);

Console.WriteLine("  Users:");
Console.WriteLine("    admin    / admin123    -> Engineer (full access)");
Console.WriteLine("    operator / operator123 -> Operator (read + write)");
Console.WriteLine("    viewer   / viewer123   -> Observer (read-only)");
Console.WriteLine();

// -- Step 3: Handle certificate validation -------------------------------------
// This event fires when a client presents its X.509 certificate.
// In production: check the certificate against your trust store.
// Here: accept all certificates for simplicity.
server.CertificateValidation += (sender, e) =>
{
    Console.WriteLine($"  [CERT] Transport: {e.Certificate.Subject} -> Accepted");
    e.Accept = true;
};

// This event fires for X.509 user token authentication
server.UserManager.CertificateValidation += (sender, e) =>
{
    Console.WriteLine($"  [CERT] User: {e.Certificate.Subject} -> Accepted");
    e.Accept = true;
};

// -- Step 4: Track session lifecycle -------------------------------------------
// These events fire whenever a client connects or disconnects.
// Useful for logging, auditing, or resource management.
server.SessionCreated += (s, e) =>
    Console.WriteLine($"  [SESSION+] {e.SessionName ?? "unknown"} from {e.ClientUri ?? "unknown"}");
server.SessionClosed += (s, e) =>
    Console.WriteLine($"  [SESSION-] {e.SessionName ?? "unknown"}");

// -- Step 5: Start server ------------------------------------------------------
Console.Write("Starting server ... ");
try { server.Start(config); }
catch (Exception ex) { Console.WriteLine("FAILED"); Console.WriteLine(ex.Message); Console.ReadLine(); return; }
Console.WriteLine("OK");
Console.WriteLine();

// Create a simple variable to give clients something to read
var plant = server.CreateFolder("Plant");
var temp  = server.CreateVariable<double>(plant, "Temperature", initialValue: 22.0);

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running with security enabled.                    ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Endpoint: opc.tcp://localhost:48411                         ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Try with any OPC UA client:                                 ║");
Console.WriteLine("║  * Connect with Security Mode = None (anonymous)             ║");
Console.WriteLine("║  * Connect with Sign + Basic256Sha256 as admin/admin123      ║");
Console.WriteLine("║  * Connect as viewer/viewer123 and try to write Temperature  ║");
Console.WriteLine("║  * Watch session events appear in this console               ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to exit.                                        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();
