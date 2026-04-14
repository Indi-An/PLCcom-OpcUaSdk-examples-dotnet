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
Console.WriteLine("║  This example creates four methods:                          ║");
Console.WriteLine("║    Reset()                   - resets CycleCount to 0        ║");
Console.WriteLine("║    Add(A, B) -> Sum           - returns A + B                ║");
Console.WriteLine("║    Multiply(A, B) -> Product  - returns A x B                ║");
Console.WriteLine("║    SetTemperature(value)      - updates a server variable    ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  In UA Expert: right-click a method -> Call...               ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// =============================================================================
// Step 1: Configure and start the server
// =============================================================================
var config = new UaServerConfiguration
{
    ApplicationName = "PLCcom Workshop 13 - Methods",
    ApplicationUri  = "urn:localhost:PLCcom:Workshop:13",
    ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
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
    NamespaceUri     = "http://indi-an.com/opcua/workshop/methods",
    CertificateStorePath = @".\pki"
};

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
var plant   = server.CreateFolder("Plant");
var machine = server.CreateFolder(plant, "Machine1");

// These variables will be read and modified by the methods below
var counter = server.CreateVariable<long>(machine, "CycleCount", initialValue: 0L);
var temp    = server.CreateVariable<double>(machine, "Temperature", initialValue: 22.0);

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
    handler: (ctx, method, objectId, inputArgs, outputArgs) =>
    {
        counter.Value = 0;
        Console.WriteLine("  [METHOD] Reset() -> CycleCount = 0");
        return ServiceResult.Good;
    });

// -- Method 2: Add (two inputs, one output) -----------------------------------
// Methods with arguments require Argument descriptors that define:
//   Name        - displayed in the client's call dialog
//   DataType    - OPC UA data type (Double, Int32, String, etc.)
//   ValueRank   - Scalar (-1) or array dimension
//   Description - tooltip shown in the client
server.CreateMethod(machine, "Add",
    handler: (ctx, method, objectId, inputArgs, outputArgs) =>
    {
        double a = (double)inputArgs[0];
        double b = (double)inputArgs[1];
        outputArgs[0] = a + b;
        Console.WriteLine($"  [METHOD] Add({a}, {b}) = {a + b}");
        return ServiceResult.Good;
    },
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
    handler: (ctx, method, objectId, inputArgs, outputArgs) =>
    {
        double a = (double)inputArgs[0];
        double b = (double)inputArgs[1];
        outputArgs[0] = a * b;
        Console.WriteLine($"  [METHOD] Multiply({a}, {b}) = {a * b}");
        return ServiceResult.Good;
    },
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
    handler: (ctx, method, objectId, inputArgs, outputArgs) =>
    {
        double newTemp = (double)inputArgs[0];
        temp.Value = newTemp;  // triggers DataChange for all subscribers
        Console.WriteLine($"  [METHOD] SetTemperature({newTemp:F1}) -> Temperature updated");
        return ServiceResult.Good;
    },
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
Console.WriteLine("║  Press ENTER to exit.                                        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();
