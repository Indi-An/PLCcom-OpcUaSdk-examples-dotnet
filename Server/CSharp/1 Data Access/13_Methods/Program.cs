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
// PLCcom OPC UA Server SDK - Workshop 13: Methods
//
// OPC UA Methods are callable functions in the server's address space.
// A client can invoke a method by sending a Call service request - similar
// to calling a remote procedure (RPC). Methods can have typed input
// arguments and return typed output arguments.
//
// Typical use cases:
//   * Reset a counter or clear an alarm
//   * Start/stop a machine or process
//   * Calculate a value on the server side (e.g. unit conversion)
//   * Trigger a firmware update or configuration change
//   * Write a value with server-side validation and side effects
//
// Methods appear in the address space as child nodes of an Object or Folder.
// Clients can browse to them and see their input/output argument definitions.
// In UA Expert: right-click a method node -> "Call..." to invoke it.
//
// What you will learn:
//   * How to create a method without arguments (Reset)
//   * How to create a method with input and output arguments (Add, Multiply)
//   * How to create a method that modifies server-side state (SetTemperature)
//   * How to define argument types and descriptions
//   * How method calls interact with variables and subscriptions
//   * How to receive a structured ExtensionObject argument (myMethodNode)
//     -> used by Client Workshop 24 (Simple Method Calls)
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
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 13: Methods             ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Methods are callable functions in the address space.        ║");
Console.WriteLine("║  Clients invoke them via the OPC UA Call service.            ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This example creates five methods:                          ║");
Console.WriteLine("║    Reset()                   - resets CycleCount to 0        ║");
Console.WriteLine("║    Add(A, B) -> Sum           - returns A + B                ║");
Console.WriteLine("║    Multiply(A, B) -> Product  - returns A x B                ║");
Console.WriteLine("║    SetTemperature(value)      - updates a server variable    ║");
Console.WriteLine("║    myMethodNode(DataStructure_One) - for Client Workshop 24  ║");
Console.WriteLine("║    myMethodNode(nested structs)     - for Client Workshop 25 ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  In UA Expert: right-click a method -> Call...               ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// =============================================================================
// Step 1: Configure and start the server
// =============================================================================
// All server settings are defined in CreateConfig() below.
// See that function for a full description of every available option.
var config = CreateConfig();
PrintConfig(config);

using var server = new UaServer(LicenseUserName, LicenseSerial);
server.CertificateValidation += (s, e) => e.Accept = true;

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
// Step 2: Create the address space with variables
// =============================================================================
var plant   = server.CreateFolder("Plant", UaRolePermissions.WITHOUT_RESTRICTIONS);
var machine = server.CreateFolder(plant, "Machine1", UaRolePermissions.WITHOUT_RESTRICTIONS);

// These variables will be read and modified by the methods below
var counter = server.CreateVariable<long>(machine, "CycleCount", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 0L);
var temp    = server.CreateVariable<double>(machine, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 22.0);

Console.WriteLine("-- Address space ------------------------------------------------");
Console.WriteLine($"  Int64   {counter.Path,-40} {counter.NodeId}  = 0");
Console.WriteLine($"  Double  {temp.Path,-40} {temp.NodeId}  = 22.0");
Console.WriteLine();

// =============================================================================
// Step 3: Create methods
// =============================================================================
// Methods are created under an Object or Folder node.
// The handler lambda is called when a client invokes the method.
// Return ServiceResult.Good to indicate success.

// -- Method 1: Reset (no arguments) ------------------------------------------
// The simplest form - no inputs, no outputs.
// Resets the CycleCount variable to zero.
server.CreateMethod(machine, "Reset",
    (ctx, method, objectId, inputArgs, outputArgs) =>
    {
        counter.Value = 0;
        Console.WriteLine("  [METHOD] Reset() -> CycleCount = 0");
        return ServiceResult.Good;
    }, UaRolePermissions.WITHOUT_RESTRICTIONS);

// -- Method 2: Add (two inputs, one output) -----------------------------------
// Methods with arguments require Argument descriptors that define:
//   Name        - displayed in the client's call dialog
//   DataType    - OPC UA data type (Double, Int32, String, etc.)
//   ValueRank   - Scalar (-1) or array dimension
//   Description - tooltip shown in the client
server.CreateMethod(machine, "Add",
    (ctx, method, objectId, inputArgs, outputArgs) =>
    {
        double a = (double)inputArgs[0];
        double b = (double)inputArgs[1];
        outputArgs[0] = a + b;
        Console.WriteLine($"  [METHOD] Add({a}, {b}) = {a + b}");
        return ServiceResult.Good;
    },
    UaRolePermissions.WITHOUT_RESTRICTIONS,
    inputArgs: new Argument[]
    {
        new Argument { Name = "A", DataType = DataTypeIds.Double,
            ValueRank = ValueRanks.Scalar, Description = "First operand" },
        new Argument { Name = "B", DataType = DataTypeIds.Double,
            ValueRank = ValueRanks.Scalar, Description = "Second operand" }
    },
    outputArgs: new Argument[]
    {
        new Argument { Name = "Sum", DataType = DataTypeIds.Double,
            ValueRank = ValueRanks.Scalar, Description = "Result of A + B" }
    });

// -- Method 3: Multiply (two inputs, one output) -----------------------------
server.CreateMethod(machine, "Multiply",
    (ctx, method, objectId, inputArgs, outputArgs) =>
    {
        double a = (double)inputArgs[0];
        double b = (double)inputArgs[1];
        outputArgs[0] = a * b;
        Console.WriteLine($"  [METHOD] Multiply({a}, {b}) = {a * b}");
        return ServiceResult.Good;
    },
    UaRolePermissions.WITHOUT_RESTRICTIONS,
    inputArgs: new Argument[]
    {
        new Argument { Name = "A", DataType = DataTypeIds.Double,
            ValueRank = ValueRanks.Scalar, Description = "First factor" },
        new Argument { Name = "B", DataType = DataTypeIds.Double,
            ValueRank = ValueRanks.Scalar, Description = "Second factor" }
    },
    outputArgs: new Argument[]
    {
        new Argument { Name = "Product", DataType = DataTypeIds.Double,
            ValueRank = ValueRanks.Scalar, Description = "Result of A x B" }
    });

// -- Method 4: SetTemperature (modifies server state) -------------------------
// Methods can read and write server-side variables.
// After this call, all clients subscribed to Temperature will receive
// a DataChange notification with the new value - automatically.
server.CreateMethod(machine, "SetTemperature",
    (ctx, method, objectId, inputArgs, outputArgs) =>
    {
        double newTemp = (double)inputArgs[0];
        temp.Value = newTemp;
        Console.WriteLine($"  [METHOD] SetTemperature({newTemp:F1}) -> Temperature updated");
        return ServiceResult.Good;
    },
    UaRolePermissions.WITHOUT_RESTRICTIONS,
    inputArgs: new Argument[]
    {
        new Argument { Name = "NewTemperature", DataType = DataTypeIds.Double,
            ValueRank = ValueRanks.Scalar, Description = "New temperature value in Celsius" }
    });

Console.WriteLine("-- Methods under Machine1 ---------------------------------------");
Console.WriteLine("  Reset()                    -> resets CycleCount to 0");
Console.WriteLine("  Add(A, B) -> Sum           -> returns A + B");
Console.WriteLine("  Multiply(A, B) -> Product  -> returns A x B");
Console.WriteLine("  SetTemperature(value)      -> updates Temperature variable");
Console.WriteLine();

// =============================================================================
// Step 4: myObjectNode / myMethodNode for Client Workshop 24
// =============================================================================
// Client Workshop 24 calls a method that receives a structured argument
// encoded as an ExtensionObject (BinaryEncoder). The structure is:
//
//   DataStructure_One = { int, string, int, int, string }
//
// The method decodes the fields and returns a confirmation string.
// Node names and namespace index 2 must match exactly what the client sends.
// The client uses string-based NodeIds: new NodeId("myObjectNode", 2)
// so we must register the object under that exact string NodeId.
var myObjectNode = server.CreateObject(plant.NodeId, "myObjectNode", UaRolePermissions.WITHOUT_RESTRICTIONS);
Console.WriteLine($"  myObjectNode NodeId = {myObjectNode.NodeId}");

server.CreateMethod(myObjectNode.NodeId, "myMethodNode",
    (ctx, method, objectId, inputArgs, outputArgs) =>
    {
        try
        {
            // The client sends a single ExtensionObject whose Body is a byte[]
            // encoded with BinaryEncoder in the order: int, string, int, int, string
            var ext = inputArgs[0] as ExtensionObject;
            if (ext?.Body is byte[] body)
            {
                var ctx2 = new ServiceMessageContext(null);
                using var decoder = new BinaryDecoder(body, ctx2);
                int    v1 = decoder.ReadInt32("");
                string v2 = decoder.ReadString("");
                int    v3 = decoder.ReadInt32("");
                int    v4 = decoder.ReadInt32("");
                string v5 = decoder.ReadString("");

                Console.WriteLine($"  [METHOD] myMethodNode called: {v1}, {v2}, {v3}, {v4}, {v5}");
                outputArgs[0] = $"Received: {v1} | {v2} | {v3} | {v4} | {v5}";
            }
            else
            {
                outputArgs[0] = "No input received";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [METHOD] myMethodNode error: {ex.Message}");
            outputArgs[0] = $"Error: {ex.Message}";
        }
        return ServiceResult.Good;
    },
    UaRolePermissions.WITHOUT_RESTRICTIONS,
    inputArgs: new Argument[]
    {
        new Argument { Name = "DataStructure_One", DataType = DataTypeIds.Structure,
            ValueRank = ValueRanks.Scalar, Description = "Encoded struct: int, string, int, int, string" }
    },
    outputArgs: new Argument[]
    {
        new Argument { Name = "Result", DataType = DataTypeIds.String,
            ValueRank = ValueRanks.Scalar, Description = "Confirmation string" }
    });

Console.WriteLine("-- myObjectNode (for Client Workshop 24) ------------------------");
Console.WriteLine("  myMethodNode(DataStructure_One) -> Result");
Console.WriteLine("  Input: ExtensionObject with BinaryEncoded { int, string, int, int, string }");
Console.WriteLine();

// =============================================================================
// Step 5: myObjectNode_Advanced / myMethodNode for Client Workshop 25
// =============================================================================
// Client Workshop 25 calls a method with a nested structure:
//
//   DataStructure_One = {
//     int,
//     string,
//     DataStructure_Two (embedded ExtensionObject),
//     int,
//     DataStructure_Two[] (array of ExtensionObjects)
//     int
//   }
//
//   DataStructure_Two = { int, string, int }
var myObjectNodeAdv = server.CreateObject(plant.NodeId, "myObjectNode_Advanced", UaRolePermissions.WITHOUT_RESTRICTIONS);
Console.WriteLine($"  myObjectNode_Advanced NodeId = {myObjectNodeAdv.NodeId}");

server.CreateMethod(myObjectNodeAdv.NodeId, "myMethodNode",
    (ctx, method, objectId, inputArgs, outputArgs) =>
    {
        try
        {
            var ext = inputArgs[0] as ExtensionObject;
            if (ext?.Body is byte[] body)
            {
                var ctx2 = new ServiceMessageContext(null);
                using var decoder = new BinaryDecoder(body, ctx2);

                int    v1  = decoder.ReadInt32("");    // myIntValue1
                string v2  = decoder.ReadString("");   // myStringValue2

                // embedded DataStructure_Two
                var embExt = decoder.ReadExtensionObject("");
                string embSummary = "(empty)";
                if (embExt?.Body is byte[] embBody)
                {
                    using var d2 = new BinaryDecoder(embBody, ctx2);
                    int e1 = d2.ReadInt32(""); string e2 = d2.ReadString(""); int e3 = d2.ReadInt32("");
                    embSummary = $"{e1},{e2},{e3}";
                }

                int    v3  = decoder.ReadInt32("");    // myIntValue3

                // array of DataStructure_Two
                var arr = decoder.ReadExtensionObjectArray("");
                int arrCount = arr?.Count ?? 0;

                int    v4  = decoder.ReadInt32("");    // trailing int

                Console.WriteLine($"  [METHOD_ADV] myMethodNode: v1={v1} v2={v2} emb=[{embSummary}] v3={v3} arr={arrCount} items v4={v4}");
                outputArgs[0] = $"Received: {v1} | {v2} | emb=[{embSummary}] | v3={v3} | arr={arrCount} | v4={v4}";
            }
            else
            {
                outputArgs[0] = "No input received";
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [METHOD_ADV] error: {ex.Message}");
            outputArgs[0] = $"Error: {ex.Message}";
        }
        return ServiceResult.Good;
    },
    UaRolePermissions.WITHOUT_RESTRICTIONS,
    inputArgs: new Argument[]
    {
        new Argument { Name = "DataStructure_One", DataType = DataTypeIds.Structure,
            ValueRank = ValueRanks.Scalar, Description = "Nested struct: int, string, DataStructure_Two, int, DataStructure_Two[], int" }
    },
    outputArgs: new Argument[]
    {
        new Argument { Name = "Result", DataType = DataTypeIds.String,
            ValueRank = ValueRanks.Scalar, Description = "Confirmation string" }
    });

Console.WriteLine("-- myObjectNode_Advanced (for Client Workshop 25) ---------------");
Console.WriteLine("  myMethodNode(DataStructure_One) -> Result");
Console.WriteLine("  Input: nested struct { int, string, DataStructure_Two, int, DataStructure_Two[], int }");
Console.WriteLine();

// =============================================================================
// Step 4: Connect a client and call the methods
// =============================================================================
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running on: opc.tcp://localhost:48410             ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Try in UA Expert:                                           ║");
Console.WriteLine("║  * Browse Objects -> Plant -> Machine1                       ║");
Console.WriteLine("║  * Right-click Reset -> Call                                 ║");
Console.WriteLine("║  * Right-click Add -> Call, enter A=10 and B=20              ║");
Console.WriteLine("║  * Call SetTemperature(42.5) and watch Temperature change    ║");
Console.WriteLine("║  * Subscribe to Temperature, then call SetTemperature again  ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Use Client Workshop 24 to call myMethodNode with a          ║");
Console.WriteLine("║  structured DataStructure_One argument.                      ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to exit.                                        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();

// =============================================================================
// Helper: CreateConfig
// =============================================================================
static UaServerConfiguration CreateConfig()
{
    return new UaServerConfiguration
    {
        // ── Application Identity ──────────────────────────────────────────────
        ApplicationName = "PLCcom Workshop 13 - Methods",
        ApplicationUri  = "urn:localhost:PLCcom:Workshop:13",
        ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
        NamespaceUri    = "http://indi-an.com/opcua/workshop/methods",

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
        UserTokenPolicies = new List<UserTokenPolicy>
        {
            new UserTokenPolicy { TokenType = UserTokenType.Anonymous }
        },

        // ── PKI Certificate Store ─────────────────────────────────────────────
        CertificateStorePath        = @".\pki",
        CertificateLifetimeInMonths = 60,
        AutoAcceptUntrustedCertificates = false,
        // AsConfigured (default) = endpoints use exactly the host from BaseAddresses
        // NormalizeToHostname    = replace localhost/127.0.0.1 with the machine name
        // None = no normalization, behavior depends on DNS and network settings
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
}

// =============================================================================
// Helper: PrintConfig
// =============================================================================
static void PrintConfig(UaServerConfiguration config)
{
    Console.WriteLine("── Active Server Configuration ──────────────────────────────");
    Console.WriteLine($"  ApplicationName  : {config.ApplicationName}");
    Console.WriteLine($"  ApplicationUri   : {config.ApplicationUri}");
    Console.WriteLine($"  NamespaceUri     : {config.NamespaceUri ?? "(default)"}");
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
    Console.WriteLine("  VendorServerInfo (Server/VendorServerInfo):");
    Console.WriteLine($"    VendorName           = {config.VendorName ?? "(not set)"}");
    Console.WriteLine($"    VendorProductName    = {config.VendorProductName ?? "(not set)"}");
    Console.WriteLine($"    VendorProductVersion = {config.VendorProductVersion ?? "(not set)"}");
    Console.WriteLine();
    Console.WriteLine("  OperationLimits (Server/ServerCapabilities/OperationLimits):");
    Console.WriteLine($"    MaxNodesPerRead                      = {config.MaxNodesPerRead}");
    Console.WriteLine($"    MaxNodesPerWrite                     = {config.MaxNodesPerWrite}");
    Console.WriteLine($"    MaxNodesPerBrowse                    = {config.MaxNodesPerBrowse}");
    Console.WriteLine($"    MaxNodesPerHistoryReadData           = {config.MaxNodesPerHistoryReadData}");
    Console.WriteLine($"    MaxNodesPerHistoryReadEvents         = {config.MaxNodesPerHistoryReadEvents}");
    Console.WriteLine($"    MaxNodesPerHistoryUpdateData         = {config.MaxNodesPerHistoryUpdateData}");
    Console.WriteLine($"    MaxNodesPerHistoryUpdateEvents       = {config.MaxNodesPerHistoryUpdateEvents}");
    Console.WriteLine($"    MaxNodesPerMethodCall                = {config.MaxNodesPerMethodCall}");
    Console.WriteLine($"    MaxNodesPerRegisterNodes             = {config.MaxNodesPerRegisterNodes}");
    Console.WriteLine($"    MaxNodesPerTranslateBrowsePathsToNodeIds = {config.MaxNodesPerTranslateBrowsePathsToNodeIds}");
    Console.WriteLine($"    MaxNodesPerNodeManagement            = {config.MaxNodesPerNodeManagement}");
    Console.WriteLine($"    MaxMonitoredItemsPerCall             = {config.MaxMonitoredItemsPerCall}");
    Console.WriteLine("─────────────────────────────────────────────────────────────");
    Console.WriteLine();
}
