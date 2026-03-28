// ==============================================================================
// PLCcom OPC UA Server SDK - Workshop 14: Custom Types
//
// OPC UA has a rich type system. You can define your own ObjectTypes and
// VariableTypes that appear in the server's type hierarchy under:
//   Types -> ObjectTypes -> BaseObjectType -> YourType
//   Types -> VariableTypes -> BaseDataVariableType -> YourType
//
// Typed instances carry a TypeDefinition attribute that tells clients
// which type they are an instance of. This enables:
//   * Generic clients to understand the structure of your objects
//   * Companion specifications (e.g. PackML, Euromap) to define standard types
//   * Consistent modeling across multiple server instances
//
// What you will learn:
//   * How to define a custom ObjectType (SensorType)
//   * How to define a custom VariableType (MeasuredValueType)
//   * How to create typed instances from custom ObjectTypes
//
// Connect with any OPC UA client to:
//   opc.tcp://localhost:48413
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
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 14: Custom Types        ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This example demonstrates:                                  ║");
Console.WriteLine("║  * Defining a custom ObjectType (SensorType)                 ║");
Console.WriteLine("║  * Defining a custom VariableType (MeasuredValueType)        ║");
Console.WriteLine("║  * Creating typed instances with child variables             ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

var config = new UaServerConfiguration
{
    ApplicationName = "PLCcom Workshop 14 - Custom Types",
    ApplicationUri  = "urn:localhost:PLCcom:Workshop:14",
    ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
    BaseAddresses   = new List<string> { "opc.tcp://localhost:48413" },
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

// -- Step 1: Define custom types -----------------------------------------------
// Types are registered in the server's type hierarchy.
// They appear under Types -> ObjectTypes / VariableTypes in the address space.
// The returned NodeId is used when creating instances of this type.

// ObjectType: defines the structure of an object (like a class in OOP)
// Instances of SensorType will have TypeDefinition = SensorType
var sensorTypeId = server.CreateObjectType("SensorType");
Console.WriteLine($"  Created ObjectType: SensorType -> {sensorTypeId}");

// VariableType: defines the data type and structure of a variable
// DataTypeIds.Double means instances of this type hold a Double value
var measuredTypeId = server.CreateVariableType("MeasuredValueType", DataTypeIds.Double);
Console.WriteLine($"  Created VariableType: MeasuredValueType -> {measuredTypeId}");
Console.WriteLine();

// -- Step 2: Create typed instances --------------------------------------------
// When you pass typeDefinitionId, the object's TypeDefinition attribute
// is set to that type. Clients can use this to identify the object's role.
var plant   = server.CreateFolder("Plant");
var sensors = server.CreateFolder(plant, "Sensors");

// Both sensors are instances of SensorType - same type, different data
var sensor1 = server.CreateObject(sensors, "TemperatureSensor_01",
    typeDefinitionId: sensorTypeId);
var sensor2 = server.CreateObject(sensors, "PressureSensor_01",
    typeDefinitionId: sensorTypeId);

// Add child variables to each sensor instance
var s1Value = server.CreateVariable<double>(sensor1.NodeId, "Value", initialValue: 22.3);
var s1Unit  = server.CreateVariable<string>(sensor1.NodeId, "Unit",  initialValue: "C", readOnly: true);
var s1Alarm = server.CreateVariable<bool>(sensor1.NodeId, "AlarmActive", initialValue: false);

var s2Value = server.CreateVariable<double>(sensor2.NodeId, "Value", initialValue: 1.02);
var s2Unit  = server.CreateVariable<string>(sensor2.NodeId, "Unit",  initialValue: "bar", readOnly: true);

Console.WriteLine("  Instances:");
Console.WriteLine("  Sensors/");
Console.WriteLine("    TemperatureSensor_01 (TypeDef: SensorType)");
Console.WriteLine("      Value = 22.3");
Console.WriteLine("      Unit = C [ReadOnly]");
Console.WriteLine("      AlarmActive = false");
Console.WriteLine("    PressureSensor_01 (TypeDef: SensorType)");
Console.WriteLine("      Value = 1.02");
Console.WriteLine("      Unit = bar [ReadOnly]");
Console.WriteLine();

Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running. Connect with any OPC UA client to:        ║");
Console.WriteLine("║  opc.tcp://localhost:48413                                    ║");
Console.WriteLine("║                                                               ║");
Console.WriteLine("║  Try:                                                         ║");
Console.WriteLine("║  * Browse Types -> ObjectTypes -> BaseObjectType -> SensorType║");
Console.WriteLine("║  * Click a sensor and check its TypeDefinition attribute      ║");
Console.WriteLine("║  * Both sensors share the same SensorType definition          ║");
Console.WriteLine("║                                                               ║");
Console.WriteLine("║  Press ENTER to exit.                                         ║");
Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
Console.ReadLine();
