// ==============================================================================
// PLCcom OPC UA Server SDK - Workshop 11: Simple Server
//
// This is the starting point for all server workshops.
// It shows the minimal code needed to run an OPC UA server with a real
// address space that any OPC UA client can connect to and browse.
//
// What you will learn:
//   * How to configure and start an OPC UA server
//   * How to create a folder hierarchy in the address space
//   * How to create variables of different data types
//   * How to push value changes to subscribed clients
//
// Connect with any OPC UA client to:
//   opc.tcp://localhost:48410
// ==============================================================================

using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Server.Sdk;
using System;
using System.Collections.Generic;
using System.Threading;

// -- License -------------------------------------------------------------------
//TODO
//Submit your license information from your license e-mail
string LicenseUserName = "<Enter your UserName here>";
string LicenseSerial = "<Enter your Serial here>";

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 11: Simple Server       ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This example creates a minimal OPC UA server with:          ║");
Console.WriteLine("║  * Folder hierarchy (Plant -> Line1 -> Machine1)             ║");
Console.WriteLine("║  * Scalar variables (Double, Int, Bool, String, DateTime)    ║");
Console.WriteLine("║  * Array variable (Double[])                                 ║");
Console.WriteLine("║  * Read-only variable (SerialNumber)                         ║");
Console.WriteLine("║  * Continuous value push loop (1 second interval)            ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// -- Step 1: Configure the server ----------------------------------------------
// UaServerConfiguration holds all server settings.
// The most important ones are:
//   ApplicationUri  - unique identifier for this server (used in certificates)
//   BaseAddresses   - the endpoint URL clients connect to
//   SecurityPolicies - which encryption algorithms to offer
//   CertificateStorePath - where PKI certificates are stored (auto-created)
var config = new UaServerConfiguration
{
    ApplicationName = "PLCcom Workshop 11 - Simple Server",
    ApplicationUri  = "urn:localhost:PLCcom:Workshop:11",
    ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",

    // The endpoint URL clients will connect to
    BaseAddresses = new List<string> { "opc.tcp://localhost:48410" },

    // GetRecommendedSecurityPolicies() returns None + Basic256Sha256,
    // Aes128_Sha256_RsaOaep and Aes256_Sha256_RsaPss (Sign + SignAndEncrypt each)
    SecurityPolicies = UaServer.GetRecommendedSecurityPolicies(),

    // Allow anonymous connections for this introductory workshop
    UserTokenPolicies = new List<UserTokenPolicy>
    {
        new UserTokenPolicy { TokenType = UserTokenType.Anonymous }
    },

    // PKI store for server certificate - created automatically on first start
    CertificateStorePath = @".\pki"
};

// -- Step 2: Create and start the server ---------------------------------------
using var server = new UaServer(LicenseUserName, LicenseSerial);

// Accept all client certificates automatically (do NOT use this in production!)
// In production, use the PKI trust store to control which clients are allowed.
server.CertificateValidation += (sender, e) => e.Accept = true;

Console.Write("Starting server ... ");
try { server.Start(config); }
catch (Exception ex) { Console.WriteLine("FAILED"); Console.WriteLine(ex.Message); Console.ReadLine(); return; }
Console.WriteLine("OK");
Console.WriteLine($"  Endpoint: {config.BaseAddresses[0]}");
Console.WriteLine();

// -- Step 3: Build the address space -------------------------------------------
// The address space is the tree of nodes that clients can browse.
// Folders organize the structure, Variables hold the actual data.
// All nodes created here are immediately visible to connected clients.

// Create a folder hierarchy: Objects -> Plant -> Line1 -> Machine1
var plant   = server.CreateFolder("Plant");           // under Objects folder
var line1   = server.CreateFolder(plant, "Line1");    // under Plant
var machine = server.CreateFolder(line1, "Machine1"); // under Line1

// Create scalar variables - each has a specific OPC UA data type
// The generic type parameter <T> determines the OPC UA DataType attribute
var temperature = server.CreateVariable<double>(machine, "Temperature", initialValue: 21.5);
var pressure    = server.CreateVariable<float>(machine, "Pressure",     initialValue: 1.013f);
var rpm         = server.CreateVariable<int>(machine, "RPM",            initialValue: 1500);
var running     = server.CreateVariable<bool>(machine, "IsRunning",     initialValue: true);
var status      = server.CreateVariable<string>(machine, "Status",      initialValue: "Idle");
var lastUpdate  = server.CreateVariable<DateTime>(machine, "LastUpdate", initialValue: DateTime.UtcNow);

// Read-only variable: clients can read but not write
// The server will return BadUserAccessDenied on any write attempt
var serialNo = server.CreateVariable<string>(machine, "SerialNumber",
    initialValue: "SN-2025-001", readOnly: true);

// Array variable: ValueRank is automatically set to OneDimension
var setpoints = server.CreateArrayVariable<double>(machine, "Setpoints",
    initialValue: new double[] { 20.0, 25.0, 30.0 });

Console.WriteLine("  Address Space:");
Console.WriteLine("  Objects -> Plant -> Line1 -> Machine1");
Console.WriteLine("    Temperature (Double)    = 21.5");
Console.WriteLine("    Pressure (Float)        = 1.013");
Console.WriteLine("    RPM (Int32)             = 1500");
Console.WriteLine("    IsRunning (Boolean)     = true");
Console.WriteLine("    Status (String)         = Idle");
Console.WriteLine("    LastUpdate (DateTime)   = now");
Console.WriteLine("    SerialNumber (String)   = SN-2025-001 [ReadOnly]");
Console.WriteLine("    Setpoints (Double[])    = [20, 25, 30]");
Console.WriteLine();

// -- Step 4: Connect a client and explore the address space --------------------
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║");
Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Try:                                                        ║");
Console.WriteLine("║  * Browse Objects -> Plant -> Line1 -> Machine1              ║");
Console.WriteLine("║  * Subscribe to Temperature, RPM, Status                     ║");
Console.WriteLine("║  * Try writing to SerialNumber (should fail - ReadOnly)      ║");
Console.WriteLine("║  * Check the DataType attribute of each variable             ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to start the value push loop.                   ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();

// -- Step 5: Push value changes to subscribed clients -------------------------
// Setting variable.Value triggers a DataChange notification to all clients
// that have an active subscription on that variable.
// This is the OPC UA publish/subscribe model - no polling needed on the client.
Console.WriteLine("Pushing values every second... (CTRL+C to exit)");
Console.WriteLine();

var rng = new Random();
long cycle = 0;

while (true)
{
    cycle++;

    // Each assignment automatically notifies subscribed clients
    temperature.Value = Math.Round(20.0 + rng.NextDouble() * 10.0, 2);
    pressure.Value    = (float)Math.Round(0.9 + rng.NextDouble() * 0.3, 3);
    rpm.Value         = 1400 + rng.Next(200);
    running.Value     = cycle % 30 != 0;  // simulate a stop every 30 seconds
    status.Value      = running.Value ? "Running" : "Stopped";
    lastUpdate.Value  = DateTime.UtcNow;

    Console.Write($"\r  Cycle={cycle}  Temp={temperature.Value:F1}C  " +
                  $"P={pressure.Value:F3}bar  RPM={rpm.Value}  {status.Value,-8}");
    Thread.Sleep(1000);
}
