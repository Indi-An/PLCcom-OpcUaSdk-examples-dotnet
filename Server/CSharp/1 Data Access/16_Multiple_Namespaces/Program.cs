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
// PLCcom OPC UA Server SDK - Workshop 16: Multiple Namespaces
//
// Every node in OPC UA has a NodeId and a BrowseName, both of which belong
// to a namespace. Namespaces prevent naming collisions when multiple vendors,
// standards or subsystems share the same server.
//
// The OPC UA specification (Part 5) reserves two namespace indexes:
//   ns=0  Reserved for the OPC UA standard namespace
//         ("http://opcfoundation.org/UA/")
//   ns=1  Reserved for the local Server
//         (ApplicationUri + "/nodes" by convention)
//
// Additional namespaces are registered with AddNamespace(). Each returns a
// namespace index (ns=2, ns=3, ...) that is used in NodeIds and BrowseNames.
//
// This workshop demonstrates a realistic scenario:
//
//   ns=0  OPC UA standard        - standard nodes (always present)
//   ns=1  Local server            - server's own nodes (always present)
//   ns=2  Company namespace       - company-wide type definitions
//   ns=3  Plant A namespace       - nodes for Plant A
//   ns=4  Plant B namespace       - nodes for Plant B
//
// Both plants use the same company-defined types but have separate node trees.
// A client can filter by namespace to see only one plant's data.
//
// The address space built here:
//   Objects
//     +-- CompanyTypes  (ns=2)          folder for company-wide types
//     +-- PlantA  (ns=3)
//     |     +-- Reactor  (ns=3)
//     |     |     +-- Temperature  (ns=3)  = 85.0
//     |     |     +-- Pressure     (ns=3)  = 2.5
//     |     +-- Mixer    (ns=3)
//     |           +-- Speed        (ns=3)  = 120.0
//     +-- PlantB  (ns=4)
//           +-- Reactor  (ns=4)
//           |     +-- Temperature  (ns=4)  = 92.0
//           |     +-- Pressure     (ns=4)  = 3.1
//           +-- Mixer    (ns=4)
//                 +-- Speed        (ns=4)  = 80.0
//
// What you will learn:
//   * Why namespaces matter in OPC UA
//   * How to register additional namespaces with AddNamespace
//   * How to create nodes in a specific namespace using the ns parameter
//   * How NodeIds and BrowseNames reflect the namespace index
//   * How ObjectTypes in one namespace can be used across other namespaces
//   * How to look up a namespace index with GetNamespaceIndex
//
// Connect with any OPC UA client to: opc.tcp://localhost:48410
// ==============================================================================

using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Server.Sdk;
using System;
using System.Collections.Generic;

// -- License -------------------------------------------------------------------
// TODO: Replace with your license credentials from your license e-mail
string LicenseUserName = "<Enter your UserName here>";
string LicenseSerial = "<Enter your Serial here>";

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 16: Multiple Namespaces ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This example demonstrates:                                  ║");
Console.WriteLine("║    * Registering additional namespaces                       ║");
Console.WriteLine("║    * Creating nodes in specific namespaces                   ║");
Console.WriteLine("║    * Sharing ObjectTypes across namespaces                   ║");
Console.WriteLine("║    * Two plants with identical structure but separate nodes  ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// =============================================================================
// Step 1: Configure and start the server
// =============================================================================
var config = new UaServerConfiguration
{
    ApplicationName  = "PLCcom Workshop 16 - Multiple Namespaces",
    ApplicationUri   = "urn:localhost:PLCcom:Workshop:16",
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
    NamespaceUri     = "http://indi-an.com/opcua/workshop/multiple-namespaces",
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

// Show the namespace table after server start
var nsTable = server.NodeManager.Server.NamespaceUris;
Console.WriteLine("-- Namespace table after Start() --------------------------------");
for (int i = 0; i < nsTable.Count; i++)
    Console.WriteLine($"  ns={i}  {nsTable.GetString((uint)i)}");
Console.WriteLine($"  NodeManager.NamespaceIndex = {server.NodeManager.NamespaceIndex}");
Console.WriteLine();

// Create two variables in default namespace for comparison
var defaultFolder = server.CreateFolder("DefaultNS");
var testValue1 = server.CreateVariable<double>(defaultFolder, "TestValue1", initialValue: 42.0);
var testValue2 = server.CreateVariable<string>(defaultFolder, "TestValue2", initialValue: "hello");
Console.WriteLine("-- Default namespace nodes ----------------------------------------");
Console.WriteLine($"  {defaultFolder.Path,-40} NodeId={defaultFolder.NodeId}  BrowseName={defaultFolder.BrowseName}");
Console.WriteLine($"  {testValue1.Path,-40} NodeId={testValue1.NodeId}  BrowseName={testValue1.BrowseName}");
Console.WriteLine($"  {testValue2.Path,-40} NodeId={testValue2.NodeId}  BrowseName={testValue2.BrowseName}");
Console.WriteLine();

// =============================================================================
// Step 2: Register additional namespaces
// =============================================================================
// After Start(), the server has two namespaces (per OPC UA Spec Part 5):
//   ns=0  OPC UA standard namespace (reserved, always present)
//   ns=1  Local server namespace (reserved for this server's own nodes)
//
// AddNamespace() registers a new URI and returns its index (ns=2, 3, ...).
// The URI should be a globally unique identifier for the namespace owner.
// Convention: use a URI based on your company domain or project.
Console.WriteLine("-- Registering namespaces ---------------------------------------");

ushort nsCompany = server.AddNamespace("urn:mycompany:types");
ushort nsPlantA  = server.AddNamespace("urn:mycompany:plant-a");
ushort nsPlantB  = server.AddNamespace("urn:mycompany:plant-b");

Console.WriteLine($"  ns={nsCompany}  urn:mycompany:types     (company-wide types)");
Console.WriteLine($"  ns={nsPlantA}  urn:mycompany:plant-a   (Plant A instances)");
Console.WriteLine($"  ns={nsPlantB}  urn:mycompany:plant-b   (Plant B instances)");
Console.WriteLine();

// Show the full namespace table after AddNamespace
Console.WriteLine("-- Namespace table after AddNamespace() -------------------------");
for (int i = 0; i < nsTable.Count; i++)
    Console.WriteLine($"  ns={i}  {nsTable.GetString((uint)i)}");
Console.WriteLine();

// You can also look up a namespace index later:
ushort check = server.GetNamespaceIndex("urn:mycompany:plant-a");
Console.WriteLine($"  GetNamespaceIndex(\"urn:mycompany:plant-a\") = {check}");
Console.WriteLine();

// =============================================================================
// Step 3: Define company-wide ObjectTypes in the company namespace
// =============================================================================
// ObjectTypes are reusable blueprints. By placing them in the company namespace,
// they are clearly identified as belonging to your organization.
// Instances in different plant namespaces can reference the same type.
Console.WriteLine("-- Company-wide ObjectTypes (ns={0}) ----------------------------", nsCompany);

var reactorTypeId = server.CreateObjectType("ReactorType", ns: nsCompany);
var mixerTypeId   = server.CreateObjectType("MixerType", ns: nsCompany);

Console.WriteLine($"  ReactorType  {reactorTypeId}");
Console.WriteLine($"  MixerType    {mixerTypeId}");
Console.WriteLine();

// =============================================================================
// Step 4: Build Plant A in its own namespace
// =============================================================================
// All folders, objects and variables for Plant A use ns=nsPlantA.
// This means their NodeIds and BrowseNames carry the Plant A namespace index.
// A client filtering by namespace can isolate Plant A's data.
Console.WriteLine("-- Plant A (ns={0}) ---------------------------------------------", nsPlantA);

// Only the top-level folder needs the ns: parameter.
// All children inherit the namespace from their parent automatically.
var plantA = server.CreateFolder("PlantA", ns: nsPlantA);

var reactorA = server.CreateObject(plantA, "Reactor", typeDefinitionId: reactorTypeId);
var tempA    = server.CreateVariable<double>(reactorA, "Temperature", initialValue: 85.0);
var pressA   = server.CreateVariable<double>(reactorA, "Pressure",    initialValue: 2.5);

var mixerA = server.CreateObject(plantA, "Mixer", typeDefinitionId: mixerTypeId);
var speedA = server.CreateVariable<double>(mixerA, "Speed", initialValue: 120.0);

Console.WriteLine($"  {plantA.Path,-40} NodeId={plantA.NodeId}  BrowseName={plantA.BrowseName}");
Console.WriteLine($"  {tempA.Path,-40} NodeId={tempA.NodeId}  BrowseName={tempA.BrowseName}");
Console.WriteLine($"  {pressA.Path,-40} NodeId={pressA.NodeId}  BrowseName={pressA.BrowseName}");
Console.WriteLine($"  {speedA.Path,-40} NodeId={speedA.NodeId}  BrowseName={speedA.BrowseName}");
Console.WriteLine();

// =============================================================================
// Step 5: Build Plant B in its own namespace
// =============================================================================
// Plant B has the exact same structure as Plant A, but all nodes are in ns=4.
// The BrowseNames "Reactor", "Temperature" etc. are the same strings,
// but qualified with a different namespace index -> no collision.
Console.WriteLine("-- Plant B (ns={0}) ---------------------------------------------", nsPlantB);

// Same for Plant B — only the root folder specifies the namespace.
var plantB = server.CreateFolder("PlantB", ns: nsPlantB);

var reactorB = server.CreateObject(plantB, "Reactor", typeDefinitionId: reactorTypeId);
var tempB    = server.CreateVariable<double>(reactorB, "Temperature", initialValue: 92.0);
var pressB   = server.CreateVariable<double>(reactorB, "Pressure",    initialValue: 3.1);

var mixerB = server.CreateObject(plantB, "Mixer", typeDefinitionId: mixerTypeId);
var speedB = server.CreateVariable<double>(mixerB, "Speed", initialValue: 80.0);

Console.WriteLine($"  {plantB.Path,-40} NodeId={plantB.NodeId}  BrowseName={plantB.BrowseName}");
Console.WriteLine($"  {tempB.Path,-40} NodeId={tempB.NodeId}  BrowseName={tempB.BrowseName}");
Console.WriteLine($"  {pressB.Path,-40} NodeId={pressB.NodeId}  BrowseName={pressB.BrowseName}");
Console.WriteLine($"  {speedB.Path,-40} NodeId={speedB.NodeId}  BrowseName={speedB.BrowseName}");
Console.WriteLine();

// =============================================================================
// Step 6: Demonstrate cross-namespace reading
// =============================================================================
// The server.GetValue/SetValue API works across namespaces using the dot-separated path.
// The path uses BrowseName strings (without namespace prefix).
Console.WriteLine("-- Cross-namespace GetValue -------------------------------------");

double tA = server.GetValue<double>("Objects.PlantA.Reactor.Temperature");
double tB = server.GetValue<double>("Objects.PlantB.Reactor.Temperature");
Console.WriteLine($"  PlantA Reactor Temperature = {tA}");
Console.WriteLine($"  PlantB Reactor Temperature = {tB}");
Console.WriteLine();

// =============================================================================
// Step 7: Run the server
// =============================================================================
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║");
Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Try:                                                        ║");
Console.WriteLine("║  * Browse Objects -> PlantA -> Reactor -> Temperature        ║");
Console.WriteLine("║  * Browse Objects -> PlantB -> Reactor -> Temperature        ║");
Console.WriteLine("║  * Compare NodeIds: both have numeric IDs but different ns   ║");
Console.WriteLine("║  * Compare BrowseNames: same name, different namespace index ║");
Console.WriteLine("║  * Browse Types -> ObjectTypes -> ReactorType, MixerType     ║");
Console.WriteLine("║  * Write PlantA/Reactor/Temperature and PlantB independently ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to exit.                                        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();
