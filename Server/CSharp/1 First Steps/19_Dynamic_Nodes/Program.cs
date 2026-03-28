// ==============================================================================
// PLCcom OPC UA Server SDK - Workshop 19: Dynamic Nodes
//
// In most OPC UA servers the address space is static - built once at startup.
// However, the SDK also supports dynamic changes at runtime:
//   * Add new folders and variables while the server is running
//   * Remove nodes that are no longer needed
//   * Connected clients see the changes immediately
//
// Additional features shown here:
//   * Path-based node lookup using dot-separated browse paths
//   * Circular reference detection (prevents invalid address space structures)
//
// What you will learn:
//   * How to add nodes after server.Start()
//   * How to remove nodes by NodeId
//   * How to find nodes by browse path (e.g. "Plant.Line1.Temperature")
//   * How the SDK prevents circular references
//
// Connect with any OPC UA client to: opc.tcp://localhost:48418
// ==============================================================================

using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Server.Sdk;
using System;
using System.Collections.Generic;

// -- License -------------------------------------------------------------------
//TODO
//Submit your license information from your license e-mail
string LicenseUserName = "<Enter your UserName here>";
string LicenseSerial = "<Enter your Serial here>";

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 19: Dynamic Nodes       ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This example demonstrates:                                  ║");
Console.WriteLine("║  * Adding nodes at runtime                                   ║");
Console.WriteLine("║  * Removing nodes dynamically                                ║");
Console.WriteLine("║  * Path-based node lookup (dot-separated)                    ║");
Console.WriteLine("║  * Circular reference detection                              ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

var config = new UaServerConfiguration
{
    ApplicationName = "PLCcom Workshop 19 - Dynamic Nodes",
    ApplicationUri  = "urn:localhost:PLCcom:Workshop:19",
    ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
    BaseAddresses   = new List<string> { "opc.tcp://localhost:48418" },
    SecurityPolicies = UaServer.GetRecommendedSecurityPolicies(),
    UserTokenPolicies = new List<UserTokenPolicy>
    {
        new UserTokenPolicy { TokenType = UserTokenType.Anonymous }
    },
    CertificateStorePath = @".\pki"
};

using var server = new UaServer(LicenseUserName, LicenseSerial);
server.CertificateValidation += (s, e) => e.Accept = true;

Console.Write("Starting server ... ");
try { server.Start(config); }
catch (Exception ex) { Console.WriteLine("FAILED"); Console.WriteLine(ex.Message); Console.ReadLine(); return; }
Console.WriteLine("OK");
Console.WriteLine();

// -- Create initial structure --------------------------------------------------
// These nodes are created right after Start() - this is the normal pattern.
var plant = server.CreateFolder("Plant");
var line1 = server.CreateFolder(plant, "Line1");
var temp  = server.CreateVariable<double>(line1, "Temperature", initialValue: 22.0);

Console.WriteLine("  Initial: Plant -> Line1 -> Temperature");
Console.WriteLine();

// -- Path-based lookup ---------------------------------------------------------
// GetNodeId() resolves a dot-separated browse path to a NodeId.
// This is useful when you need to reference a node by its logical path
// rather than storing the NodeId from CreateFolder/CreateVariable.
var nodeId = server.GetNodeId("Plant.Line1.Temperature");
Console.WriteLine($"  Path lookup: 'Plant.Line1.Temperature' -> {nodeId}");

// GetVariable<T>() returns a typed wrapper for the variable at the given path
var variable = server.GetVariable<double>("Plant.Line1.Temperature");
Console.WriteLine($"  Variable lookup: Value = {variable?.Value}");
Console.WriteLine();

// -- Dynamic node creation -----------------------------------------------------
// Nodes can be added at any time after Start().
// Connected clients will see the new nodes immediately on their next browse.
Console.WriteLine("  Adding dynamic nodes...");
var dynFolder = server.CreateFolder(plant, "DynamicNodes");
var dynVar1   = server.CreateVariable<int>(dynFolder, "Counter", initialValue: 42);
var dynVar2   = server.CreateVariable<string>(dynFolder, "Message", initialValue: "Hello");
Console.WriteLine($"    Created: DynamicNodes/Counter = {dynVar1.Value}");
Console.WriteLine($"    Created: DynamicNodes/Message = {dynVar2.Value}");
Console.WriteLine();

// -- Dynamic node removal ------------------------------------------------------
// RemoveNode() removes the node and all its children from the address space.
// Connected clients that have subscriptions on removed nodes will receive
// a BadNodeIdUnknown status on their next publish cycle.
Console.WriteLine("  Removing DynamicNodes/Counter...");
bool removed = server.RemoveNode(dynVar1.NodeId);
Console.WriteLine($"    Removed: {(removed ? "OK" : "FAILED")}");
Console.WriteLine();

// -- Circular reference detection ----------------------------------------------
// The SDK prevents you from creating a folder with the same name as one of
// its ancestors, which would create a circular reference in the address space.
// This throws an ArgumentException with a descriptive message.
Console.Write("  Circular reference check: ");
try
{
    // "Plant" already exists as an ancestor of line1 - this must be rejected
    server.CreateFolder(line1, "Plant");
    Console.WriteLine("NOT DETECTED (unexpected)");
}
catch (ArgumentException ex)
{
    Console.WriteLine($"OK - {ex.Message}");
}
Console.WriteLine();

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║");
Console.WriteLine("║  opc.tcp://localhost:48418                                   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Try:                                                        ║");
Console.WriteLine("║  * Browse Plant -> DynamicNodes                              ║");
Console.WriteLine("║  * Counter was removed - only Message exists                 ║");
Console.WriteLine("║  * Subscribe to Temperature, then disconnect and reconnect   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to exit.                                        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();
