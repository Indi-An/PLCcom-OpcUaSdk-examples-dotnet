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
// PLCcom OPC UA Server SDK - Workshop 15: Custom Types
//
// OPC UA allows servers to define custom structured DataTypes (Structs).
// A struct groups related fields into a single value that clients can
// read, write and subscribe to as one unit (an ExtensionObject).
//
// This workshop demonstrates the full range of struct features:
//
//   Part A — Object Hierarchy (the simple alternative to structs)
//   Part B — Flat Struct (MotorDataType with 3 scalar fields)
//   Part C — Nested Struct (PlantDataType containing MotorDataType)
//   Part D — Struct with Array fields (double[], string[])
//   Part E — Array of Structs (3 motors as MotorDataType[3])
//   Part F — Struct containing an Array-of-Structs field
//   Part G — Struct with a 2D Matrix field (multidimensional array)
//
// The address space built here:
//   Objects
//     +-- Hierarchy
//     |     +-- CNC_Machine_01  (MachineType)
//     |           +-- MainMotor    (MotorType)
//     |           |     +-- Speed, Temperature, Running
//     |           +-- MainBearing  (BearingType)
//     |           |     +-- Temperature, Vibration
//     |           +-- State, CycleCount
//     |
//     +-- StructData
//           +-- Motor_Struct      (MotorDataType)
//           +-- Machine_Struct    (MachineDataType)
//           +-- Plant_Struct      (PlantDataType - nested)
//           +-- Sensor_Struct     (SensorDataType - array fields)
//           +-- Motor_Array       (MotorDataType[3])
//           +-- Factory_Struct    (FactoryDataType - array-of-structs field)
//           +-- Grid_Struct       (GridDataType - 2D matrix field)
//
// What you will learn:
//   * Object hierarchy vs. structured DataTypes - when to use which
//   * How to define a struct DataType with CreateStructDataType
//   * How to create struct Variables and set field values
//   * How nested structs use dotted paths (e.g. "Motor.Speed")
//   * How array fields work inside structs
//   * How to create an array of structs with indexed access
//   * How structs with array-of-structs fields use "Field.[N].SubField"
//   * How multidimensional arrays (Matrix) work inside structs
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
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 15: Custom Types        ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This example demonstrates:                                  ║");
Console.WriteLine("║    * Object hierarchy (Objects with child Variables)         ║");
Console.WriteLine("║    * Flat structs (MotorDataType, MachineDataType)           ║");
Console.WriteLine("║    * Nested structs (PlantDataType contains MotorDataType)   ║");
Console.WriteLine("║    * Struct with array fields (double[], string[])           ║");
Console.WriteLine("║    * Array of structs (MotorDataType[3])                     ║");
Console.WriteLine("║    * Struct with array-of-structs field                      ║");
Console.WriteLine("║    * Struct with 2D matrix field                             ║");
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
server.CertificateValidation += (sender, e) => e.Accept = true;

server.SessionCreated += (s, e) =>
    Console.WriteLine($"  [SESSION+] {e.SessionName ?? "unknown"} from {e.ClientUri ?? "unknown"}");
server.SessionClosed += (s, e) =>
    Console.WriteLine($"  [SESSION-] {e.SessionName ?? "unknown"}");

server.ValuesWritten += (s, e) =>
{
    foreach (var item in e.Items)
    {
        if (item.Value is Dictionary<string, object> fields)
        {
            Console.WriteLine($"  << OPC Write: {item.Path} ({item.NodeId})");
            foreach (var kvp in fields)
                Console.WriteLine($"       {kvp.Key} = {kvp.Value}");
        }
        else
            Console.WriteLine($"  << OPC Write: {item.Path} ({item.NodeId}) = {item.Value}");
    }
};

Console.Write("Starting server ... ");
try { server.Start(config); }
catch (Exception ex) { Console.WriteLine("FAILED"); Console.WriteLine(ex.Message); Console.ReadLine(); return; }
Console.WriteLine("OK");
Console.WriteLine();

// =============================================================================
// Part A: Object Hierarchy
// =============================================================================
// The simplest way to model structured data in OPC UA is an Object hierarchy.
// Each component is an Object node with child Variables.
// Clients browse the tree: Hierarchy -> CNC_Machine_01 -> MainMotor -> Speed
//
// Advantages:
//   - Works with every OPC UA client
//   - Easy to browse and understand
//   - Each variable can be subscribed to individually
//
// Disadvantages:
//   - Many nodes for complex structures
//   - No atomic read/write of the whole structure
//   - No formal type definition that clients can introspect
Console.WriteLine("-- Part A: Object Hierarchy -------------------------------------");

var hierarchy = server.CreateFolder("Hierarchy", UaRolePermissions.WITHOUT_RESTRICTIONS);

// Define ObjectTypes (appear under Types -> ObjectTypes in the address space)
var motorTypeId   = server.CreateObjectType("MotorType");
var bearingTypeId = server.CreateObjectType("BearingType");
var machineTypeId = server.CreateObjectType("MachineType");

// Create the machine instance with typed components
var machine = server.CreateObject(hierarchy, "CNC_Machine_01", UaRolePermissions.WITHOUT_RESTRICTIONS, machineTypeId);

var motor = server.CreateObject(machine.NodeId, "MainMotor", UaRolePermissions.WITHOUT_RESTRICTIONS, motorTypeId);
server.CreateVariable<double>(motor, "Speed", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 1500.0);
server.CreateVariable<double>(motor, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 45.0);
server.CreateVariable<bool>  (motor, "Running",     UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: true);

var bearing = server.CreateObject(machine.NodeId, "MainBearing", UaRolePermissions.WITHOUT_RESTRICTIONS, bearingTypeId);
server.CreateVariable<double>(bearing, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 38.0);
server.CreateVariable<double>(bearing, "Vibration", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 0.5);

server.CreateVariable<string>(machine, "State", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: "Running");
server.CreateVariable<long>  (machine, "CycleCount", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue: 0L);

Console.WriteLine($"  {machine.Path}");
Console.WriteLine($"    MainMotor    (MotorType):   Speed=1500, Temp=45, Running=true");
Console.WriteLine($"    MainBearing  (BearingType): Temp=38, Vibration=0.5");
Console.WriteLine($"    State=Running, CycleCount=0");
Console.WriteLine();

// =============================================================================
// Part B: Flat Structs
// =============================================================================
// A structured DataType (Struct) groups related fields into one value.
// The struct appears under Types -> DataTypes -> BaseDataType -> Structure.
// Variables of this type hold an ExtensionObject that clients can decode.
//
// CreateStructDataType defines the type with its fields.
// Each field is a tuple: (FieldName, DataTypeNodeId, ArrayDimensions)
//   - ArrayDimensions = null  -> scalar field
//   - ArrayDimensions = [N]   -> 1D array field with N elements
//   - ArrayDimensions = [M,N] -> 2D matrix field
//
// CreateStructVariable creates a Variable of that type.
// SetField<T> / GetField<T> access individual fields by name.
Console.WriteLine("-- Part B: Flat Structs -----------------------------------------");

var structFolder = server.CreateFolder("StructData", UaRolePermissions.WITHOUT_RESTRICTIONS);

// Define two flat struct types
var motorDataTypeId = server.CreateStructDataType("MotorDataType",
    ("Speed",       DataTypeIds.Double,  null),
    ("Temperature", DataTypeIds.Double,  null),
    ("Running",     DataTypeIds.Boolean, null));

var machineDataTypeId = server.CreateStructDataType("MachineDataType",
    ("State",      DataTypeIds.String, null),
    ("CycleCount", DataTypeIds.Int64,  null),
    ("MotorSpeed", DataTypeIds.Double, null));

// Create struct variables and set initial values
var motorStruct = server.CreateStructVariable(structFolder, "Motor_Struct", motorDataTypeId, UaRolePermissions.WITHOUT_RESTRICTIONS);
motorStruct.SetField<double>("Speed",       1500.0);
motorStruct.SetField<double>("Temperature", 45.0);
motorStruct.SetField<bool>  ("Running",     true);

var machineStruct = server.CreateStructVariable(structFolder, "Machine_Struct", machineDataTypeId, UaRolePermissions.WITHOUT_RESTRICTIONS);
machineStruct.SetField<string>("State",      "Running");
machineStruct.SetField<long>  ("CycleCount", 0L);
machineStruct.SetField<double>("MotorSpeed", 1500.0);

Console.WriteLine($"  MotorDataType     {motorDataTypeId}");
Console.WriteLine($"  MachineDataType   {machineDataTypeId}");
Console.WriteLine($"  Motor_Struct      {motorStruct.Path}");
Console.WriteLine($"  Machine_Struct    {machineStruct.Path}");
Console.WriteLine();

// =============================================================================
// Part C: Nested Struct
// =============================================================================
// A struct can contain other structs as fields. The nested struct is encoded
// inline (not as a separate ExtensionObject) per OPC UA binary encoding rules.
//
// Access nested fields with dotted paths: "Motor.Speed", "Machine.State"
Console.WriteLine("-- Part C: Nested Struct ----------------------------------------");

var plantDataTypeId = server.CreateStructDataType("PlantDataType",
    ("PlantName",       DataTypeIds.String, null),
    ("ProductionCount", DataTypeIds.Int32,  null),
    ("Motor",           motorDataTypeId,    null),
    ("Machine",         machineDataTypeId,  null));

var plantStruct = server.CreateStructVariable(structFolder, "Plant_Struct", plantDataTypeId, UaRolePermissions.WITHOUT_RESTRICTIONS);
plantStruct.SetField<string>("PlantName",       "Factory_01");
plantStruct.SetField<int>   ("ProductionCount", 42);

// Nested fields use dotted paths
plantStruct.SetField<double>("Motor.Speed",       2200.0);
plantStruct.SetField<double>("Motor.Temperature", 55.5);
plantStruct.SetField<bool>  ("Motor.Running",     true);

plantStruct.SetField<string>("Machine.State",      "Producing");
plantStruct.SetField<long>  ("Machine.CycleCount", 12345L);
plantStruct.SetField<double>("Machine.MotorSpeed", 2200.0);

Console.WriteLine($"  PlantDataType     {plantDataTypeId}");
Console.WriteLine($"  Plant_Struct      {plantStruct.Path}");
Console.WriteLine($"    PlantName       = Factory_01");
Console.WriteLine($"    Motor.Speed     = 2200");
Console.WriteLine($"    Machine.State   = Producing");
Console.WriteLine();

// =============================================================================
// Part D: Struct with Array fields
// =============================================================================
// A struct field can be an array. Specify the array size in ArrayDimensions.
// The array is encoded as Int32 length + sequential values in binary encoding.
Console.WriteLine("-- Part D: Struct with Array fields -----------------------------");

var sensorDataTypeId = server.CreateStructDataType("SensorDataType",
    ("Name",       DataTypeIds.String, null),
    ("Readings",   DataTypeIds.Double, new uint[] { 4 }),
    ("Thresholds", DataTypeIds.Double, new uint[] { 2 }));

var sensorStruct = server.CreateStructVariable(structFolder, "Sensor_Struct", sensorDataTypeId, UaRolePermissions.WITHOUT_RESTRICTIONS);
sensorStruct.SetField<string>  ("Name",       "TempSensor_01");
sensorStruct.SetField<double[]>("Readings",   new double[] { 23.5, 24.1, 22.8, 25.0 });
sensorStruct.SetField<double[]>("Thresholds", new double[] { 50.0, 75.0 });

Console.WriteLine($"  SensorDataType    {sensorDataTypeId}");
Console.WriteLine($"  Sensor_Struct     {sensorStruct.Path}");
Console.WriteLine($"    Readings   = [23.5, 24.1, 22.8, 25.0]");
Console.WriteLine($"    Thresholds = [50.0, 75.0]");
Console.WriteLine();

// =============================================================================
// Part E: Array of Structs
// =============================================================================
// CreateStructArrayVariable creates a Variable whose value is an array of
// ExtensionObjects. Each element gets its own child nodes that can be
// browsed, read, written and subscribed to individually.
//
// Access elements by index: motorArray[0].SetField<double>("Speed", 1000.0)
Console.WriteLine("-- Part E: Array of Structs -------------------------------------");

var motorArray = server.CreateStructArrayVariable(structFolder, "Motor_Array", motorDataTypeId, 3);

motorArray[0].SetField<double>("Speed",       1000.0);
motorArray[0].SetField<double>("Temperature", 40.0);
motorArray[0].SetField<bool>  ("Running",     true);

motorArray[1].SetField<double>("Speed",       1500.0);
motorArray[1].SetField<double>("Temperature", 55.0);
motorArray[1].SetField<bool>  ("Running",     true);

motorArray[2].SetField<double>("Speed",       0.0);
motorArray[2].SetField<double>("Temperature", 22.0);
motorArray[2].SetField<bool>  ("Running",     false);

Console.WriteLine($"  Motor_Array       {motorArray.Path}");
Console.WriteLine($"    [0]: Speed=1000, Temp=40,  Running=true");
Console.WriteLine($"    [1]: Speed=1500, Temp=55,  Running=true");
Console.WriteLine($"    [2]: Speed=0,    Temp=22,  Running=false");
Console.WriteLine();

// =============================================================================
// Part F: Struct with Array-of-Structs field
// =============================================================================
// A struct field can itself be an array of another struct type.
// Access elements via: "Motors.[0].Speed", "Motors.[1].Temperature"
Console.WriteLine("-- Part F: Struct with Array-of-Structs field -------------------");

var factoryDataTypeId = server.CreateStructDataType("FactoryDataType",
    ("FactoryName", DataTypeIds.String,  null),
    ("Motors",      motorDataTypeId,     new uint[] { 2 }));

var factoryStruct = server.CreateStructVariable(structFolder, "Factory_Struct", factoryDataTypeId, UaRolePermissions.WITHOUT_RESTRICTIONS);
factoryStruct.SetField<string>("FactoryName", "MainFactory");

// Array-of-structs fields use "Field.[N].SubField" path syntax
factoryStruct.SetField<double>("Motors.[0].Speed",       1000.0);
factoryStruct.SetField<double>("Motors.[0].Temperature", 40.0);
factoryStruct.SetField<bool>  ("Motors.[0].Running",     true);

factoryStruct.SetField<double>("Motors.[1].Speed",       2000.0);
factoryStruct.SetField<double>("Motors.[1].Temperature", 60.0);
factoryStruct.SetField<bool>  ("Motors.[1].Running",     false);

Console.WriteLine($"  FactoryDataType   {factoryDataTypeId}");
Console.WriteLine($"  Factory_Struct    {factoryStruct.Path}");
Console.WriteLine($"    Motors[0]: Speed=1000, Temp=40, Running=true");
Console.WriteLine($"    Motors[1]: Speed=2000, Temp=60, Running=false");
Console.WriteLine();

// =============================================================================
// Part G: Struct with 2D Matrix field
// =============================================================================
// A struct field can be a multidimensional array (Matrix).
// Encoded per Spec Part 6 Table 27: Inline Matrix encoding
// (Int32 total elements, flat values, Int32 num dimensions, Int32[] dimensions)
//
// In UaExpert the Matrix appears as child nodes: Matrix[0][0], Matrix[0][1], ...
Console.WriteLine("-- Part G: Struct with 2D Matrix field --------------------------");

var gridDataTypeId = server.CreateStructDataType("GridDataType",
    ("Label",  DataTypeIds.String, null),
    ("Matrix", DataTypeIds.Double, new uint[] { 2, 3 }));

var gridStruct = server.CreateStructVariable(structFolder, "Grid_Struct", gridDataTypeId, UaRolePermissions.WITHOUT_RESTRICTIONS);
gridStruct.SetField<string>("Label", "HeatMap_01");
gridStruct.SetField("Matrix", new Matrix(
    new double[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 },
    BuiltInType.Double,
    new int[] { 2, 3 }));

Console.WriteLine($"  GridDataType      {gridDataTypeId}");
Console.WriteLine($"  Grid_Struct       {gridStruct.Path}");
Console.WriteLine($"    Matrix (2x3): [[1,2,3],[4,5,6]]");
Console.WriteLine();

// =============================================================================
// Step 2: Run the server
// =============================================================================
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║");
Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Try:                                                        ║");
Console.WriteLine("║  * Browse Hierarchy -> CNC_Machine_01 (Object hierarchy)     ║");
Console.WriteLine("║  * Browse StructData -> Motor_Struct (flat struct)           ║");
Console.WriteLine("║  * Browse StructData -> Plant_Struct (nested struct)         ║");
Console.WriteLine("║  * Browse StructData -> Sensor_Struct (array fields)         ║");
Console.WriteLine("║  * Browse StructData -> Motor_Array (array of structs)       ║");
Console.WriteLine("║  * Browse StructData -> Factory_Struct (array-of-structs)    ║");
Console.WriteLine("║  * Browse StructData -> Grid_Struct (2D matrix)              ║");
Console.WriteLine("║  * Browse Types -> DataTypes -> Structure -> MotorDataType   ║");
Console.WriteLine("║  * Write Motor_Struct/Speed = 2000 and check the struct value║");
Console.WriteLine("║  * Write the whole Motor_Struct as ExtensionObject           ║");
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
        ApplicationName  = "PLCcom Workshop 15 - Custom Types",
        ApplicationUri   = "urn:localhost:PLCcom:Workshop:15",
        ProductUri       = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
        NamespaceUri     = "http://indi-an.com/opcua/workshop/custom-types",

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
    foreach (var addr in config.BaseAddresses) Console.WriteLine($"    {addr}");
    Console.WriteLine();
        Console.WriteLine($"  EndpointHostMode : {config.EndpointHostMode}");
    Console.WriteLine("  VendorServerInfo:");
    Console.WriteLine($"    VendorName={config.VendorName ?? "(not set)"}  ProductName={config.VendorProductName ?? "(not set)"}  Version={config.VendorProductVersion ?? "(not set)"}");
    Console.WriteLine();
    Console.WriteLine("  OperationLimits:");
    Console.WriteLine($"    Read={config.MaxNodesPerRead}  Write={config.MaxNodesPerWrite}  Browse={config.MaxNodesPerBrowse}  Method={config.MaxNodesPerMethodCall}");
    Console.WriteLine($"    HistRD={config.MaxNodesPerHistoryReadData}  HistRE={config.MaxNodesPerHistoryReadEvents}  HistUD={config.MaxNodesPerHistoryUpdateData}  HistUE={config.MaxNodesPerHistoryUpdateEvents}");
    Console.WriteLine($"    Register={config.MaxNodesPerRegisterNodes}  Translate={config.MaxNodesPerTranslateBrowsePathsToNodeIds}  NodeMgmt={config.MaxNodesPerNodeManagement}  MonItems={config.MaxMonitoredItemsPerCall}");
    Console.WriteLine("─────────────────────────────────────────────────────────────");
    Console.WriteLine();
}
