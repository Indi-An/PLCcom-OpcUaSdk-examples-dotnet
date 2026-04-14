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
// PLCcom OPC UA Server SDK - Workshop 11: Simple Server
//
// The starting point for all server workshops. This example creates a fully
// functional OPC UA server that any compliant client can connect to, browse,
// read, write and subscribe to.
//
// The key concepts demonstrated here form the foundation for every OPC UA
// server application:
//
//   1. Configuration — set up endpoints, security and certificates
//   2. Address space — create folders and variables that clients can see
//   3. Data types    — each variable has a specific OPC UA data type
//   4. Value push    — update values from code; subscribed clients are
//                      notified automatically (no polling needed)
//   5. Client writes — react to values written by OPC UA clients
//
// The address space built here is intentionally simple:
//   Objects
//     └─ Plant
//         └─ Line1
//             └─ Machine1
//                 ├─ Temperature   (Double)     = 21.5
//                 ├─ Pressure      (Float)      = 1.013
//                 ├─ RPM           (Int32)      = 1500
//                 ├─ IsRunning     (Boolean)    = true
//                 ├─ Status        (String)     = "Idle"
//                 ├─ LastUpdate    (DateTime)   = now
//                 ├─ SerialNumber  (String)     = "SN-2025-001"  [ReadOnly]
//                 └─ Setpoints     (Double[])   = [20, 25, 30]
//
// What you will learn:
//   • How to configure and start an OPC UA server
//   • How to create a folder hierarchy in the address space
//   • How to create scalar and array variables of different data types
//   • How to mark a variable as read-only
//   • How to react when an OPC UA client writes a value (ValuesWritten)
//   • How to push value changes to subscribed clients from a background loop
//   • How to use the Path property to identify nodes by their browse path
//
// Connect with any OPC UA client to: opc.tcp://localhost:48410
//                                or: opc.https://localhost:48411
// ==============================================================================

using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Server.Sdk;
using System;
using System.Collections.Generic;
using System.Threading;

// -- License -------------------------------------------------------------------
// TODO: Replace with your license credentials from your license e-mail
string LicenseUserName = "<Enter your UserName here>";
string LicenseSerial = "<Enter your Serial here>";

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 11: Simple Server       ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This example creates a minimal OPC UA server with:          ║");
Console.WriteLine("║    * Folder hierarchy  (Plant -> Line1 -> Machine1)          ║");
Console.WriteLine("║    * Scalar variables  (Double, Float, Int, Bool, String)    ║");
Console.WriteLine("║    * Array variable    (Double[])                            ║");
Console.WriteLine("║    * Read-only variable (SerialNumber)                       ║");
Console.WriteLine("║    * Client write notifications (ValuesWritten event)        ║");
Console.WriteLine("║    * Continuous value push loop (1-second interval)          ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Every node created here is immediately visible to any       ║");
Console.WriteLine("║  connected OPC UA client. The Path property shows the        ║");
Console.WriteLine("║  dot-separated browse path from the Objects root.            ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// =============================================================================
// Step 1: Configure the server
// =============================================================================
// UaServerConfiguration holds all server settings.
// The most important ones are:
//   ApplicationUri    — unique identifier for this server (used in certificates)
//   BaseAddresses     — the endpoint URL(s) clients connect to
//   SecurityPolicies  — which encryption algorithms to offer
//   CertificateStorePath — where PKI certificates are stored (auto-created)
var config = new UaServerConfiguration
{
    ApplicationName = "PLCcom Workshop 11 - Simple Server",
    ApplicationUri  = "urn:localhost:PLCcom:Workshop:11",
    ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",

    // The endpoint URLs clients will connect to
    BaseAddresses = new List<string>
    {
        "opc.tcp://localhost:48410",
         "opc.https://localhost:48411"  
    },

    // GetRecommendedSecurityPolicies() returns None + Basic256Sha256,
    // Aes128_Sha256_RsaOaep and Aes256_Sha256_RsaPss (Sign + SignAndEncrypt)
    SecurityPolicies = UaServer.GetRecommendedSecurityPolicies(),

    // Allow anonymous connections for this introductory workshop
    UserTokenPolicies = new List<UserTokenPolicy>
    {
        new UserTokenPolicy { TokenType = UserTokenType.Anonymous }
    },

    // PKI store for server certificate — created automatically on first start
    ManufacturerName = "My Company GmbH",
    ProductName      = "My OPC UA Server",
    SoftwareVersion  = "1.0.0",
    BuildNumber      = "42",
    NamespaceUri     = "http://indi-an.com/opcua/workshop/simple-server",
    CertificateStorePath = @".\pki"
};

// =============================================================================
// Step 2: Create the server and wire up events
// =============================================================================
using var server = new UaServer(LicenseUserName, LicenseSerial);

// Accept all client certificates automatically.
// WARNING: Do NOT use this in production! Use the PKI trust store instead.
server.CertificateValidation += (sender, e) => e.Accept = true;

// ValuesWritten fires whenever an OPC UA client writes one or more values.
// Each item in e.Items contains:
//   Path     — dot-separated browse path (e.g. "Objects.Plant.Line1.Machine1.RPM")
//   NodeId   — the OPC UA NodeId (e.g. ns=2;i=6)
//   Value    — the value written by the client
// This is the primary way to react to client writes from your application code.
server.ValuesWritten += (s, e) =>
{
    foreach (var item in e.Items)
        Console.WriteLine($"  << OPC Write: {item.Path} ({item.NodeId}) = {item.Value}");
};

Console.Write("Starting server ... ");
try { server.Start(config); }
catch (Exception ex) { Console.WriteLine("FAILED"); Console.WriteLine(ex.Message); Console.ReadLine(); return; }
Console.WriteLine("OK");
Console.WriteLine();

// =============================================================================
// Step 3: Build the address space
// =============================================================================
// The address space is the tree of nodes that clients can browse.
// Folders organize the structure, Variables hold the actual data.
// All nodes are immediately visible to connected clients.
//
// Every node has a Path property — the dot-separated browse path from root.
// Example: "Objects.Plant.Line1.Machine1.Temperature"
// This path is useful for logging, debugging and the server.Read/Write API.
Console.WriteLine("── Building address space ───────────────────────────────────");

// Create a folder hierarchy: Objects -> Plant -> Line1 -> Machine1
var plant   = server.CreateFolder("Plant");
var line1   = server.CreateFolder(plant, "Line1");
var machine = server.CreateFolder(line1, "Machine1");

Console.WriteLine($"  Folder    {plant.Path,-40} {plant.NodeId}");
Console.WriteLine($"  Folder    {line1.Path,-40} {line1.NodeId}");
Console.WriteLine($"  Folder    {machine.Path,-40} {machine.NodeId}");

// Create scalar variables — each has a specific OPC UA data type.
// The generic type parameter <T> determines the DataType attribute:
//   double -> Double, float -> Float, int -> Int32, bool -> Boolean,
//   string -> String, DateTime -> DateTime
var temperature = server.CreateVariable<double>(machine, "Temperature", initialValue: 21.5);
var pressure    = server.CreateVariable<float>(machine, "Pressure",     initialValue: 1.013f);
var rpm         = server.CreateVariable<int>(machine, "RPM",            initialValue: 1500);
var running     = server.CreateVariable<bool>(machine, "IsRunning",     initialValue: true);
var status      = server.CreateVariable<string>(machine, "Status",      initialValue: "Idle");
var lastUpdate  = server.CreateVariable<DateTime>(machine, "LastUpdate", initialValue: DateTime.UtcNow);

// Read-only variable: clients can read but not write.
// The server returns BadNotWritable on any write attempt.
var serialNo = server.CreateVariable<string>(machine, "SerialNumber",
    initialValue: "SN-2025-001", readOnly: true);

// Array variable: ValueRank is automatically set to OneDimension.
// Clients see a Double[] value with 3 elements.
var setpoints = server.CreateArrayVariable<double>(machine, "Setpoints",
    initialValue: new double[] { 20.0, 25.0, 30.0 });

Console.WriteLine($"  Double    {temperature.Path,-40} {temperature.NodeId}  = 21.5");
Console.WriteLine($"  Float     {pressure.Path,-40} {pressure.NodeId}  = 1.013");
Console.WriteLine($"  Int32     {rpm.Path,-40} {rpm.NodeId}  = 1500");
Console.WriteLine($"  Boolean   {running.Path,-40} {running.NodeId}  = true");
Console.WriteLine($"  String    {status.Path,-40} {status.NodeId}  = Idle");
Console.WriteLine($"  DateTime  {lastUpdate.Path,-40} {lastUpdate.NodeId}  = now");
Console.WriteLine($"  String    {serialNo.Path,-40} {serialNo.NodeId}  = SN-2025-001 [ReadOnly]");
Console.WriteLine($"  Double[]  {setpoints.Path,-40} {setpoints.NodeId}  = [20, 25, 30]");
Console.WriteLine();

// =============================================================================
// Step 4: Connect a client and explore
// =============================================================================
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║");
Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
Console.WriteLine("║  opc.https://localhost:48411                                 ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Try:                                                        ║");
Console.WriteLine("║  * Browse Objects -> Plant -> Line1 -> Machine1              ║");
Console.WriteLine("║  * Subscribe to Temperature, RPM, Status                     ║");
Console.WriteLine("║  * Write a new value to RPM or Status                        ║");
Console.WriteLine("║  * Try writing to SerialNumber (should fail — ReadOnly)      ║");
Console.WriteLine("║  * Watch the ValuesWritten output in this console            ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to start the value push loop.                   ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();

// =============================================================================
// Step 5: Push value changes to subscribed clients
// =============================================================================
// Setting variable.Value automatically triggers a DataChange notification
// to all clients that have an active subscription on that variable.
// This is the OPC UA publish/subscribe model — no polling needed on the client.
//
// The value push runs in the main thread here for simplicity.
// In production, you would typically update values from a PLC driver,
// a database poller, or any other data source running on a background thread.
Console.WriteLine("Pushing values every second... (CTRL+C to exit)");
Console.WriteLine();

var rng = new Random();
long cycle = 0;

while (true)
{
    cycle++;

    // Each assignment to .Value notifies all subscribed clients immediately
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
