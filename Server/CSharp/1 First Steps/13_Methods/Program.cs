// ==============================================================================
// PLCcom OPC UA Server SDK - Workshop 13: Methods
//
// OPC UA Methods are callable functions in the address space.
// A client can invoke a method by sending a Call service request.
// Methods can have typed input arguments and return typed output arguments.
//
// What you will learn:
//   * How to create a method without arguments (Reset)
//   * How to create a method with input and output arguments (Add, Multiply)
//   * How to create a method that modifies server-side state (SetTemperature)
//   * How to define argument types and descriptions
//
// Connect with any OPC UA client to:
//   opc.tcp://localhost:48412
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
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 13: Methods             ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This example demonstrates:                                  ║");
Console.WriteLine("║  * Simple method without arguments (Reset)                   ║");
Console.WriteLine("║  * Method with input/output arguments (Add, Multiply)        ║");
Console.WriteLine("║  * Method that modifies server state (SetTemperature)        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

var config = new UaServerConfiguration
{
    ApplicationName = "PLCcom Workshop 13 - Methods",
    ApplicationUri  = "urn:localhost:PLCcom:Workshop:13",
    ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
    BaseAddresses   = new List<string> { "opc.tcp://localhost:48412" },
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

// Create the address space structure
var plant   = server.CreateFolder("Plant");
var machine = server.CreateFolder(plant, "Machine1");

// These variables will be read and modified by the methods below
var counter = server.CreateVariable<long>(machine, "CycleCount", initialValue: 0L);
var temp    = server.CreateVariable<double>(machine, "Temperature", initialValue: 22.0);

// -- Method 1: Reset (no arguments) -------------------------------------------
// The simplest form of a method - no inputs, no outputs.
// The handler lambda is called when a client invokes the method.
// Return ServiceResult.Good to indicate success.
server.CreateMethod(machine, "Reset",
    handler: (ctx, method, objectId, inputArgs, outputArgs) =>
    {
        counter.Value = 0;
        Console.WriteLine("\n  [METHOD] Reset called -> CycleCount = 0");
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
        outputArgs[0] = a + b;  // write result to output argument
        Console.WriteLine($"\n  [METHOD] Add({a}, {b}) = {a + b}");
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

// -- Method 3: Multiply (two inputs, one output) ------------------------------
server.CreateMethod(machine, "Multiply",
    handler: (ctx, method, objectId, inputArgs, outputArgs) =>
    {
        double a = (double)inputArgs[0];
        double b = (double)inputArgs[1];
        outputArgs[0] = a * b;
        Console.WriteLine($"\n  [METHOD] Multiply({a}, {b}) = {a * b}");
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
// a DataChange notification with the new value.
server.CreateMethod(machine, "SetTemperature",
    handler: (ctx, method, objectId, inputArgs, outputArgs) =>
    {
        double newTemp = (double)inputArgs[0];
        temp.Value = newTemp;  // this notifies all subscribed clients
        Console.WriteLine($"\n  [METHOD] SetTemperature({newTemp:F1}) -> Temperature updated");
        return ServiceResult.Good;
    },
    inputArgs: new Argument[]
    {
        new Argument { Name = "NewTemperature", DataType = DataTypeIds.Double,
            ValueRank = ValueRanks.Scalar, Description = "New temperature value in Celsius" }
    });

Console.WriteLine("  Methods created under Machine1:");
Console.WriteLine("    * Reset()                    -> resets CycleCount to 0");
Console.WriteLine("    * Add(A, B) -> Sum           -> returns A + B");
Console.WriteLine("    * Multiply(A, B) -> Product  -> returns A x B");
Console.WriteLine("    * SetTemperature(value)      -> updates Temperature variable");
Console.WriteLine();

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║");
Console.WriteLine("║  opc.tcp://localhost:48412                                   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Try:                                                        ║");
Console.WriteLine("║  * Browse to Machine1, right-click Reset -> Call             ║");
Console.WriteLine("║  * Right-click Add -> Call, enter A=10 and B=20              ║");
Console.WriteLine("║  * Call SetTemperature(42.5) and watch Temperature change    ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to exit.                                        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();
