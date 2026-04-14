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
// PLCcom OPC UA Server SDK - Workshop 14: Variables and Arrays
//
// This workshop demonstrates the full range of variable features available
// in the PLCcom Server SDK. While Workshop 11 introduced basic variables,
// this example goes deeper into:
//
//   1. All scalar data types supported by OPC UA
//   2. Properties — EURange and EngineeringUnits (metadata for HMI/SCADA)
//   3. OnRead / OnWrite callbacks for custom validation and computed values
//   4. Arrays with exposeElements — each array element as a browsable child
//   5. Read-only variables and write rejection via OnWrite
//
// The address space built here:
//   Objects
//     +-- Scalars
//     |     +-- MyBool       (Boolean)    = true
//     |     +-- MyByte       (Byte)       = 42
//     |     +-- MySByte      (SByte)      = -7
//     |     +-- MyInt16      (Int16)      = -1000
//     |     +-- MyUInt16     (UInt16)     = 5000
//     |     +-- MyInt32      (Int32)      = 100000
//     |     +-- MyUInt32     (UInt32)     = 200000
//     |     +-- MyInt64      (Int64)      = 9876543210
//     |     +-- MyUInt64     (UInt64)     = 1234567890
//     |     +-- MyFloat      (Float)      = 3.14
//     |     +-- MyDouble     (Double)     = 2.71828
//     |     +-- MyString     (String)     = "Hello OPC UA"
//     |     +-- MyDateTime   (DateTime)   = now
//     |     +-- MyGuid       (Guid)       = random
//     |     +-- MyByteString (ByteString) = [0xDE, 0xAD, 0xBE, 0xEF]
//     |
//     +-- Properties
//     |     +-- Temperature  (Double)     = 22.5
//     |     |     +-- EURange            [0 .. 100]
//     |     |     +-- EngineeringUnits   "degC"
//     |     +-- Pressure     (Double)     = 1.013
//     |     |     +-- EURange            [0 .. 10]
//     |     |     +-- EngineeringUnits   "bar"
//     |     +-- Speed        (Double)     = 1500
//     |           +-- EURange            [0 .. 3000]
//     |           +-- EngineeringUnits   "rpm"
//     |
//     +-- Callbacks
//     |     +-- Computed     (Double)     OnRead returns Temperature * 1.8 + 32
//     |     +-- Validated    (Int32)      OnWrite rejects values outside 0..100
//     |     +-- Counter      (Int32)      [ReadOnly] incremented by server
//     |
//     +-- Arrays
//           +-- Temperatures (Double[5])  plain array
//           +-- Setpoints    (Double[4])  exposeElements -> V[0]..V[3]
//           +-- Flags        (Boolean[3]) exposeElements -> V[0]..V[2]
//
// What you will learn:
//   * All OPC UA scalar data types and how to create them
//   * How EURange and EngineeringUnits help HMI/SCADA clients
//   * How OnRead computes values on-the-fly when a client reads
//   * How OnWrite validates and rejects invalid client writes
//   * How exposeElements creates browsable child nodes for array elements
//   * How to push array values from a background loop
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
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 14: Variables & Arrays  ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This example demonstrates:                                  ║");
Console.WriteLine("║    * All OPC UA scalar data types                            ║");
Console.WriteLine("║    * EURange and EngineeringUnits properties                 ║");
Console.WriteLine("║    * OnRead / OnWrite callbacks                              ║");
Console.WriteLine("║    * Arrays with exposeElements (browsable child nodes)      ║");
Console.WriteLine("║    * Read-only variables and write validation                ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// =============================================================================
// Step 1: Configure and start the server
// =============================================================================
var config = new UaServerConfiguration
{
    ApplicationName  = "PLCcom Workshop 14 - Variables and Arrays",
    ApplicationUri   = "urn:localhost:PLCcom:Workshop:14",
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
    NamespaceUri     = "http://indi-an.com/opcua/workshop/variables-and-arrays",
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
// Step 2: Scalar data types
// =============================================================================
// OPC UA defines a rich set of built-in data types. The generic type parameter
// of CreateVariable<T> maps directly to the OPC UA DataType attribute:
//
//   C# Type     ->  OPC UA DataType
//   --------        ---------------
//   bool        ->  Boolean
//   byte        ->  Byte
//   sbyte       ->  SByte
//   short       ->  Int16
//   ushort      ->  UInt16
//   int         ->  Int32
//   uint        ->  UInt32
//   long        ->  Int64
//   ulong       ->  UInt64
//   float       ->  Float
//   double      ->  Double
//   string      ->  String
//   DateTime    ->  DateTime
//   Guid        ->  Guid
//   byte[]      ->  ByteString
Console.WriteLine("-- Part A: Scalar data types ------------------------------------");

var scalars = server.CreateFolder("Scalars");

var vBool       = server.CreateVariable<bool>(scalars,     "MyBool",       true);
var vByte       = server.CreateVariable<byte>(scalars,     "MyByte",       42);
var vSByte      = server.CreateVariable<sbyte>(scalars,    "MySByte",      -7);
var vInt16      = server.CreateVariable<short>(scalars,    "MyInt16",      -1000);
var vUInt16     = server.CreateVariable<ushort>(scalars,   "MyUInt16",     5000);
var vInt32      = server.CreateVariable<int>(scalars,      "MyInt32",      100000);
var vUInt32     = server.CreateVariable<uint>(scalars,     "MyUInt32",     200000u);
var vInt64      = server.CreateVariable<long>(scalars,     "MyInt64",      9876543210L);
var vUInt64     = server.CreateVariable<ulong>(scalars,    "MyUInt64",     1234567890UL);
var vFloat      = server.CreateVariable<float>(scalars,    "MyFloat",      3.14f);
var vDouble     = server.CreateVariable<double>(scalars,   "MyDouble",     2.71828);
var vString     = server.CreateVariable<string>(scalars,   "MyString",     "Hello OPC UA");
var vDateTime   = server.CreateVariable<DateTime>(scalars, "MyDateTime",   DateTime.UtcNow);
var vGuid       = server.CreateVariable<Guid>(scalars,     "MyGuid",       Guid.NewGuid());
var vByteString = server.CreateVariable<byte[]>(scalars,   "MyByteString", new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

Console.WriteLine($"  Boolean     {vBool.Path,-40} = {vBool.Value}");
Console.WriteLine($"  Byte        {vByte.Path,-40} = {vByte.Value}");
Console.WriteLine($"  SByte       {vSByte.Path,-40} = {vSByte.Value}");
Console.WriteLine($"  Int16       {vInt16.Path,-40} = {vInt16.Value}");
Console.WriteLine($"  UInt16      {vUInt16.Path,-40} = {vUInt16.Value}");
Console.WriteLine($"  Int32       {vInt32.Path,-40} = {vInt32.Value}");
Console.WriteLine($"  UInt32      {vUInt32.Path,-40} = {vUInt32.Value}");
Console.WriteLine($"  Int64       {vInt64.Path,-40} = {vInt64.Value}");
Console.WriteLine($"  UInt64      {vUInt64.Path,-40} = {vUInt64.Value}");
Console.WriteLine($"  Float       {vFloat.Path,-40} = {vFloat.Value}");
Console.WriteLine($"  Double      {vDouble.Path,-40} = {vDouble.Value}");
Console.WriteLine($"  String      {vString.Path,-40} = {vString.Value}");
Console.WriteLine($"  DateTime    {vDateTime.Path,-40} = {vDateTime.Value:u}");
Console.WriteLine($"  Guid        {vGuid.Path,-40} = {vGuid.Value}");
Console.WriteLine($"  ByteString  {vByteString.Path,-40} = {BitConverter.ToString(vByteString.Value)}");
Console.WriteLine();

// =============================================================================
// Step 3: Properties — EURange and EngineeringUnits
// =============================================================================
// OPC UA Properties are metadata attached to a variable. The two most common
// properties for analog values are:
//
//   EURange           — the expected value range (Low, High)
//                       HMI clients use this to scale gauges and bar graphs
//
//   EngineeringUnits  — the physical unit of measurement (e.g. "degC", "bar")
//                       HMI clients display this next to the value
//
// In UaExpert: select a variable, look at the Attributes panel.
// EURange and EngineeringUnits appear as child properties.
Console.WriteLine("-- Part B: Properties (EURange, EngineeringUnits) --------------");

var props = server.CreateFolder("Properties");

var temperature = server.CreateVariable<double>(props, "Temperature", 22.5);
temperature.SetEURange(0, 100);
temperature.SetEngineeringUnits("degC", "Degrees Celsius");

var pressure = server.CreateVariable<double>(props, "Pressure", 1.013);
pressure.SetEURange(0, 10);
pressure.SetEngineeringUnits("bar", "Bar");

var speed = server.CreateVariable<double>(props, "Speed", 1500.0);
speed.SetEURange(0, 3000);
speed.SetEngineeringUnits("rpm", "Revolutions per minute");

Console.WriteLine($"  {temperature.Path,-45} = {temperature.Value}  [0..100 degC]");
Console.WriteLine($"  {pressure.Path,-45} = {pressure.Value}  [0..10 bar]");
Console.WriteLine($"  {speed.Path,-45} = {speed.Value}  [0..3000 rpm]");
Console.WriteLine();

// =============================================================================
// Step 4: OnRead / OnWrite callbacks
// =============================================================================
// OnRead is called every time a client reads the variable.
//   -> Return a computed value (e.g. unit conversion, live calculation)
//   -> The returned value is sent to the client and cached in the node
//
// OnWrite is called every time a client writes a new value.
//   -> Return true to accept the write
//   -> Return false to reject it (client receives BadOutOfRange)
//
// These callbacks run inside the OPC UA stack's lock, so keep them fast.
Console.WriteLine("-- Part C: OnRead / OnWrite callbacks ---------------------------");

var callbacks = server.CreateFolder("Callbacks");

// Computed variable: OnRead converts Temperature from Celsius to Fahrenheit
var computed = server.CreateVariable<double>(callbacks, "Computed", 0.0, readOnly: true);
computed.OnRead = (currentValue) =>
{
    return Math.Round(temperature.Value * 1.8 + 32.0, 2);
};

// Validated variable: OnWrite rejects values outside 0..100
var validated = server.CreateVariable<int>(callbacks, "Validated", 50);
validated.OnWrite = (newValue) =>
{
    if (newValue < 0 || newValue > 100)
    {
        Console.WriteLine($"  !! Rejected write: {newValue} (must be 0..100)");
        return false;  // -> client receives BadOutOfRange
    }
    return true;  // -> accept the write
};

// Read-only counter: incremented by the server, clients cannot write
var counter = server.CreateVariable<int>(callbacks, "Counter", 0, readOnly: true);

Console.WriteLine($"  {computed.Path,-45} OnRead -> Fahrenheit");
Console.WriteLine($"  {validated.Path,-45} OnWrite -> reject if not 0..100");
Console.WriteLine($"  {counter.Path,-45} [ReadOnly] server-incremented");
Console.WriteLine();

// =============================================================================
// Step 5: Arrays and exposeElements
// =============================================================================
// CreateArrayVariable<T> creates a variable with ValueRank = OneDimension.
// The value is a T[] array that clients read/write as a whole.
//
// With exposeElements: true, the SDK additionally creates child nodes
// for each array element: V[0], V[1], V[2], ...
// Each child is a separate OPC UA Variable that clients can:
//   - Browse individually
//   - Subscribe to individually (get DataChange for just one element)
//   - Read/Write individually (without touching the whole array)
//
// The parent array and the child elements stay synchronized automatically.
Console.WriteLine("-- Part D: Arrays and exposeElements ----------------------------");

var arrays = server.CreateFolder("Arrays");

// Plain array — no child nodes, read/write the whole array at once
var temps = server.CreateArrayVariable<double>(arrays, "Temperatures",
    initialValue: new double[] { 20.0, 21.5, 22.0, 23.5, 24.0 });

// Array with exposeElements — each element is a browsable child node
// In UaExpert: browse Arrays -> Setpoints -> V[0], V[1], V[2], V[3]
var setpoints = server.CreateArrayVariable<double>(arrays, "Setpoints",
    initialValue: new double[] { 100.0, 200.0, 300.0, 400.0 },
    exposeElements: true);

// Boolean array with exposeElements
var flags = server.CreateArrayVariable<bool>(arrays, "Flags",
    initialValue: new bool[] { true, false, true },
    exposeElements: true);

Console.WriteLine($"  {temps.Path,-45} Double[5]  (plain array)");
Console.WriteLine($"  {setpoints.Path,-45} Double[4]  (exposeElements)");
Console.WriteLine($"  {flags.Path,-45} Bool[3]    (exposeElements)");
Console.WriteLine();

// =============================================================================
// Step 6: Run the server
// =============================================================================
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║");
Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Try:                                                        ║");
Console.WriteLine("║  * Browse Scalars - all 15 OPC UA data types                 ║");
Console.WriteLine("║  * Browse Properties - check EURange and EngineeringUnits    ║");
Console.WriteLine("║  * Read Callbacks/Computed - shows Fahrenheit conversion     ║");
Console.WriteLine("║  * Write Callbacks/Validated - try 50 (OK) and 200 (reject)  ║");
Console.WriteLine("║  * Write Callbacks/Counter - should fail (ReadOnly)          ║");
Console.WriteLine("║  * Browse Arrays/Setpoints - see V[0]..V[3] child nodes      ║");
Console.WriteLine("║  * Subscribe to V[1] only - get changes for one element      ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to start the value push loop.                   ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();

// =============================================================================
// Step 7: Push value changes
// =============================================================================
Console.WriteLine("Pushing values every second... (CTRL+C to exit)");
Console.WriteLine();

var rng = new Random();
long cycle = 0;

while (true)
{
    cycle++;

    // Update scalar values
    temperature.Value = Math.Round(18.0 + rng.NextDouble() * 12.0, 2);
    pressure.Value    = Math.Round(0.8 + rng.NextDouble() * 0.5, 3);
    speed.Value       = 1200.0 + rng.Next(600);

    // Increment the read-only counter from server side
    counter.Value = (int)cycle;

    // Update the plain array (whole array at once)
    temps.Value = new double[]
    {
        Math.Round(19.0 + rng.NextDouble() * 3.0, 1),
        Math.Round(20.0 + rng.NextDouble() * 3.0, 1),
        Math.Round(21.0 + rng.NextDouble() * 3.0, 1),
        Math.Round(22.0 + rng.NextDouble() * 3.0, 1),
        Math.Round(23.0 + rng.NextDouble() * 3.0, 1)
    };

    // Update individual elements of the exposed array
    // Each assignment triggers a DataChange only for that element's subscribers
    setpoints.Value = new double[]
    {
        100.0 + rng.Next(50),
        200.0 + rng.Next(50),
        300.0 + rng.Next(50),
        400.0 + rng.Next(50)
    };

    Console.Write($"\r  Cycle={cycle}  Temp={temperature.Value:F1}C " +
                  $"({computed.Value:F1}F)  P={pressure.Value:F3}bar  " +
                  $"Counter={counter.Value}  ");
    Thread.Sleep(1000);
}
