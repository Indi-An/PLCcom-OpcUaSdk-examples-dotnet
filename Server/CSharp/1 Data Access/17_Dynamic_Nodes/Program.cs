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
// PLCcom OPC UA Server SDK - Workshop 17: Dynamic Nodes
//
// In most OPC UA servers the address space is static - built once at startup.
// However, the SDK also supports dynamic changes at runtime:
//   * Add new folders and variables while the server is running
//   * Remove nodes that are no longer needed
//   * Connected clients see the changes immediately on their next browse
//
// This workshop demonstrates:
//
//   Part A — Initial address space (created right after Start)
//   Part B — Path-based node lookup (GetNodeId, GetVariable)
//   Part C — Dynamic node creation (add nodes at runtime)
//   Part D — Dynamic node removal (RemoveNode)
//   Part E — Circular reference detection
//   Part F — Timer-based dynamic creation (simulates device discovery)
//
// The address space evolves at runtime:
//   Objects
//     +-- Plant
//     |     +-- Line1
//     |     |     +-- Temperature  (Double) = 22.0
//     |     +-- DynamicNodes                          <- added at runtime
//     |     |     +-- Message      (String) = "Hello" <- Counter removed
//     |     +-- Device_1                              <- added by timer
//     |     +-- Device_2                              <- added by timer
//     |     +-- ...
//
// What you will learn:
//   * How to add nodes after server.Start()
//   * How to remove nodes by NodeId (including all children)
//   * How to find nodes by dot-separated browse path
//   * How the SDK prevents circular references
//   * How to simulate device discovery with periodic node creation
//
// Connect with any OPC UA client to: opc.tcp://localhost:48410
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
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 17: Dynamic Nodes       ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This example demonstrates:                                  ║");
Console.WriteLine("║    * Adding nodes at runtime                                 ║");
Console.WriteLine("║    * Removing nodes dynamically                              ║");
Console.WriteLine("║    * Path-based node lookup (dot-separated)                  ║");
Console.WriteLine("║    * Circular reference detection                            ║");
Console.WriteLine("║    * Timer-based device discovery simulation                 ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// =============================================================================
// Step 1: Configure and start the server
// =============================================================================
var config = new UaServerConfiguration
{
    ApplicationName  = "PLCcom Workshop 17 - Dynamic Nodes",
    ApplicationUri   = "urn:localhost:PLCcom:Workshop:17",
    ProductUri       = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
    BaseAddresses = new List<string>
    {
        "opc.tcp://localhost:48410",
        "opc.https://localhost:48411"
    },
    SecurityPolicies = UaServer.GetRecommendedSecurityPolicies(),
    UserTokenPolicies = new List<UserTokenPolicy>
    {
        new UserTokenPolicy { TokenType = UserTokenType.Anonymous }
    },
    ManufacturerName = "My Company GmbH",
    ProductName      = "My OPC UA Server",
    SoftwareVersion  = "1.0.0",
    BuildNumber      = "42",
    NamespaceUri     = "http://indi-an.com/opcua/workshop/dynamic-nodes",
    CertificateStorePath = @".\pki"
};

using var server = new UaServer(LicenseUserName, LicenseSerial);
server.CertificateValidation += (sender, e) => e.Accept = true;

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
// Part A: Initial address space
// =============================================================================
// These nodes are created right after Start() - this is the normal pattern.
// All nodes are immediately visible to connected clients.
Console.WriteLine("-- Part A: Initial address space ---------------------------------");

var plant = server.CreateFolder("Plant");
var line1 = server.CreateFolder(plant, "Line1");
var temp  = server.CreateVariable<double>(line1, "Temperature", initialValue: 22.0);

Console.WriteLine($"  {plant.Path,-45} {plant.NodeId}");
Console.WriteLine($"  {line1.Path,-45} {line1.NodeId}");
Console.WriteLine($"  {temp.Path,-45} {temp.NodeId}  = {temp.Value}");
Console.WriteLine();

// =============================================================================
// Part B: Path-based node lookup
// =============================================================================
// GetNodeId() resolves a dot-separated browse path to a NodeId.
// GetVariable<T>() returns a typed wrapper for the variable at the given path.
// These are useful when you need to reference a node by its logical path
// rather than storing the NodeId from CreateFolder/CreateVariable.
Console.WriteLine("-- Part B: Path-based node lookup --------------------------------");

var nodeId = server.GetNodeId("Objects.Plant.Line1.Temperature");
Console.WriteLine($"  GetNodeId(\"Objects.Plant.Line1.Temperature\") = {nodeId}");

var variable = server.GetVariable<double>("Objects.Plant.Line1.Temperature");
Console.WriteLine($"  GetVariable -> Value = {variable?.Value}");

// GetValue/SetValue by path
double val = server.GetValue<double>("Objects.Plant.Line1.Temperature");
Console.WriteLine($"  GetValue(\"Objects.Plant.Line1.Temperature\") = {val}");

server.SetValue("Objects.Plant.Line1.Temperature", 25.5);
Console.WriteLine($"  SetValue(\"Objects.Plant.Line1.Temperature\", 25.5)");
Console.WriteLine($"  GetValue after SetValue = {server.GetValue<double>("Objects.Plant.Line1.Temperature")}");
Console.WriteLine();

// =============================================================================
// Part C: Dynamic node creation
// =============================================================================
// Nodes can be added at any time after Start().
// Connected clients will see the new nodes immediately on their next browse.
// The Path property is assigned automatically based on the parent hierarchy.
Console.WriteLine("-- Part C: Dynamic node creation ---------------------------------");

var dynFolder = server.CreateFolder(plant, "DynamicNodes");
var dynVar1   = server.CreateVariable<int>(dynFolder, "Counter", initialValue: 42);
var dynVar2   = server.CreateVariable<string>(dynFolder, "Message", initialValue: "Hello");

Console.WriteLine($"  Created: {dynVar1.Path,-40} = {dynVar1.Value}");
Console.WriteLine($"  Created: {dynVar2.Path,-40} = {dynVar2.Value}");
Console.WriteLine();

// =============================================================================
// Part D: Dynamic node removal
// =============================================================================
// RemoveNode() removes the node and all its children from the address space.
// The path index and write handlers are cleaned up automatically.
// Connected clients that have subscriptions on removed nodes will receive
// a BadNodeIdUnknown status on their next publish cycle.
Console.WriteLine("-- Part D: Dynamic node removal ----------------------------------");

Console.WriteLine($"  Removing {dynVar1.Path} ...");
bool removed = server.RemoveNode(dynVar1.NodeId);
Console.WriteLine($"  Result: {(removed ? "OK - node removed" : "FAILED")}");

// Verify the node is gone
var check = server.GetNodeId("Objects.Plant.DynamicNodes.Counter");
Console.WriteLine($"  GetNodeId after removal: {(check == null ? "null (correct)" : check.ToString())}");
Console.WriteLine();

// Remove an entire folder with all children
Console.WriteLine($"  Removing entire DynamicNodes folder ...");
removed = server.RemoveNode(dynFolder.NodeId);
Console.WriteLine($"  Result: {(removed ? "OK - folder and children removed" : "FAILED")}");

check = server.GetNodeId("Objects.Plant.DynamicNodes.Message");
Console.WriteLine($"  GetNodeId(\"...DynamicNodes.Message\"): {(check == null ? "null (correct)" : check.ToString())}");
Console.WriteLine();

// =============================================================================
// Part E: Circular reference detection
// =============================================================================
// The SDK prevents you from creating a folder with the same name as one of
// its ancestors, which would create a circular reference in the address space.
// This throws an ArgumentException with a descriptive message.
Console.WriteLine("-- Part E: Circular reference detection --------------------------");

Console.Write("  Creating \"Plant\" under Line1 (ancestor name): ");
try
{
    server.CreateFolder(line1, "Plant");
    Console.WriteLine("NOT DETECTED (unexpected)");
}
catch (ArgumentException ex)
{
    Console.WriteLine($"BLOCKED - {ex.Message}");
}
Console.WriteLine();

// =============================================================================
// Part F: Timer-based device discovery simulation
// =============================================================================
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║");
Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Try:                                                        ║");
Console.WriteLine("║  * Browse Plant - DynamicNodes was removed                   ║");
Console.WriteLine("║  * Watch new Device_N folders appear every 5 seconds         ║");
Console.WriteLine("║  * Each device has Temperature and Status variables          ║");
Console.WriteLine("║  * After 5 devices, the oldest is removed (sliding window)   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to start the device discovery simulation.       ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();

// Simulate device discovery: every 5 seconds a new "device" appears.
// After 5 devices, the oldest one is removed (sliding window).
Console.WriteLine("Simulating device discovery... (CTRL+C to exit)");
Console.WriteLine();

var rng = new Random();
int deviceNumber = 0;
var activeDevices = new Queue<(string Name, NodeId FolderId)>();
const int MaxDevices = 5;

while (true)
{
    deviceNumber++;
    string deviceName = $"Device_{deviceNumber}";

    // Create a new device folder with variables
    var deviceFolder = server.CreateFolder(plant, deviceName);
    var devTemp   = server.CreateVariable<double>(deviceFolder, "Temperature",
        initialValue: Math.Round(20.0 + rng.NextDouble() * 15.0, 1));
    var devStatus = server.CreateVariable<string>(deviceFolder, "Status",
        initialValue: "Online");

    activeDevices.Enqueue((deviceName, deviceFolder.NodeId));
    Console.WriteLine($"  + Discovered {deviceName}: Temp={devTemp.Value:F1}, Status={devStatus.Value}");

    // Remove the oldest device if we exceed the sliding window
    if (activeDevices.Count > MaxDevices)
    {
        var oldest = activeDevices.Dequeue();
        server.RemoveNode(oldest.FolderId);
        Console.WriteLine($"  - Removed {oldest.Name} (sliding window)");
    }

    Console.WriteLine($"    Active devices: {activeDevices.Count}");
    Thread.Sleep(5000);
}
