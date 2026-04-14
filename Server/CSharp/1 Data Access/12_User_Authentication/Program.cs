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
using System.Collections.Generic;

// -- License -------------------------------------------------------------------
// TODO: Replace with your license credentials from your license e-mail
string LicenseUserName = "<Enter your UserName here>";
string LicenseSerial = "<Enter your Serial here>";

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
// Step 1: Configure the server - no anonymous access
// =============================================================================
// Note: Anonymous is NOT in the UserTokenPolicies list.
// Clients that try to connect without credentials will be rejected.
var config = new UaServerConfiguration
{
    ApplicationName = "PLCcom Workshop 12 - User Authentication",
    ApplicationUri  = "urn:localhost:PLCcom:Workshop:12",
    ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
    BaseAddresses = new List<string>
    {
        "opc.tcp://localhost:48410",
        "opc.https://localhost:48411"
    },
    SecurityPolicies = UaServer.GetRecommendedSecurityPolicies(),

    // Only UserName and Certificate - no Anonymous!
    UserTokenPolicies = new List<UserTokenPolicy>
    {
        new UserTokenPolicy { TokenType = UserTokenType.UserName },
        new UserTokenPolicy { TokenType = UserTokenType.Certificate }
    },

    ManufacturerName = "My Company GmbH",
    ProductName      = "My OPC UA Server",
    SoftwareVersion  = "1.0.0",
    BuildNumber      = "42",
    NamespaceUri     = "http://indi-an.com/opcua/workshop/user-authentication",
    CertificateStorePath = @".\pki"
};

// =============================================================================
// Step 2: Create server and add users with roles
// =============================================================================
// Users are added before Start(). Each user gets a role:
//   Engineer  - full access (read, write, browse, call methods)
//   Operator  - read + write, no configuration changes
//   Observer  - read-only (any write attempt returns BadUserAccessDenied)
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
// Transport certificate: validates the client's X.509 certificate for the
// secure channel. In production, check against your PKI trust store.
server.CertificateValidation += (sender, e) =>
{
    Console.WriteLine($"  [CERT] {e.Certificate.Subject} -> Accepted");
    e.Accept = true;
};

// User certificate: validates X.509 certificates used for user authentication
// (TokenType.Certificate). Same principle - verify in production.
server.UserManager.CertificateValidation += (sender, e) =>
{
    Console.WriteLine($"  [USER CERT] {e.Certificate.Subject} -> Accepted");
    e.Accept = true;
};

// Session lifecycle - fires on every connect and disconnect.
// Useful for logging, auditing, or license seat counting.
server.SessionCreated += (s, e) =>
    Console.WriteLine($"  [SESSION+] {e.SessionName ?? "unknown"} from {e.ClientUri ?? "unknown"}");
server.SessionClosed += (s, e) =>
    Console.WriteLine($"  [SESSION-] {e.SessionName ?? "unknown"}");

// React to OPC UA client writes
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

var plant = server.CreateFolder("Plant");
var temp  = server.CreateVariable<double>(plant, "Temperature", initialValue: 22.0);
var rpm   = server.CreateVariable<int>(plant, "RPM", initialValue: 1500);

Console.WriteLine("── Address space ────────────────────────────────────────────");
Console.WriteLine($"  Double  {temp.Path,-40} {temp.NodeId}  = 22.0");
Console.WriteLine($"  Int32   {rpm.Path,-40} {rpm.NodeId}  = 1500");
Console.WriteLine();

// =============================================================================
// Step 5: Connect and test role-based access
// =============================================================================
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running - authentication required.                ║");
Console.WriteLine("║  Endpoint: opc.tcp://localhost:48410                         ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Try:                                                        ║");
Console.WriteLine("║  * Connect without credentials -> rejected                   ║");
Console.WriteLine("║  * Connect as viewer/viewer123 -> can read, cannot write     ║");
Console.WriteLine("║  * Connect as operator/operator123 -> can read and write     ║");
Console.WriteLine("║  * Connect as admin/admin123 -> full access                  ║");
Console.WriteLine("║  * Watch session events appear in this console               ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to exit.                                        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();
