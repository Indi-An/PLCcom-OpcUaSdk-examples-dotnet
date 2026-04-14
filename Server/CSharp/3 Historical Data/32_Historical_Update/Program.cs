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
// PLCcom OPC UA Server SDK - Workshop 32: Historical Update
//
// Workshop 31 demonstrated reading historical data. This workshop extends
// the server to also accept HistoryUpdate requests from clients:
//   Insert  - add a new value at a specific timestamp
//   Update  - insert or replace (upsert)
//   Replace - replace an existing value (fails if not exists)
//   Remove  - remove a value by timestamp
//   DeleteRaw    - delete all values in a time range
//   DeleteAtTime - delete values at specific timestamps
//
// The server uses the same in-memory history store as Workshop 31.
// Clients can use the PLCcom Client SDK methods (Insert, Update, Replace,
// Remove, DeleteRaw, DeleteAtTime) or any OPC UA compliant client.
//
// What you will learn:
//   * How EnableHistory automatically enables HistoryWrite access
//   * How clients can insert, update, replace and delete history values
//   * How the server validates operations (BadEntryExists, BadNoEntryExists)
//   * How to verify history changes by reading back
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
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 32: Historical Update   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This example demonstrates:                                  ║");
Console.WriteLine("║  * History recording with read AND write access              ║");
Console.WriteLine("║  * Clients can Insert, Update, Replace, Remove values        ║");
Console.WriteLine("║  * Clients can DeleteRaw (by range) and DeleteAtTime         ║");
Console.WriteLine("║  * Server validates each operation and returns StatusCodes   ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

var config = new UaServerConfiguration
{
    ApplicationName = "PLCcom Workshop 32 - Historical Update",
    ApplicationUri  = "urn:localhost:PLCcom:Workshop:32",
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
    NamespaceUri     = "http://indi-an.com/opcua/workshop/historical-update",
    CertificateStorePath = @".\pki"
};

using var server = new UaServer(LicenseUserName, LicenseSerial);
server.CertificateValidation += (s, e) => e.Accept = true;

Console.Write("Starting server ... ");
try { server.Start(config); }
catch (Exception ex) { Console.WriteLine("FAILED"); Console.WriteLine(ex.Message); Console.ReadLine(); return; }
Console.WriteLine("OK");
Console.WriteLine();

var plant  = server.CreateFolder("Plant");
var sensor = server.CreateFolder(plant, "Sensor");

var temperature = server.CreateVariable<double>(sensor, "Temperature", initialValue: 20.0);
temperature.SetEURange(-40, 120);
temperature.SetEngineeringUnits("C", "Degrees Celsius");

var pressure = server.CreateVariable<double>(sensor, "Pressure", initialValue: 1.0);
pressure.SetEURange(0, 10);
pressure.SetEngineeringUnits("bar", "Bar");

// EnableHistory sets Historizing=true AND AccessLevel includes HistoryRead + HistoryWrite.
// This means clients can both read AND modify the history.
server.EnableHistory(temperature, maxEntries: 500);
server.EnableHistory(pressure,    maxEntries: 500);

Console.WriteLine("  Variables with history enabled (read + write):");
Console.WriteLine("    Temperature: Historizing=true, HistoryRead + HistoryWrite");
Console.WriteLine("    Pressure:    Historizing=true, HistoryRead + HistoryWrite");
Console.WriteLine();

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║");
Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  The server records values every second. Clients can:        ║");
Console.WriteLine("║  * Read history (HistoryRead / ReadRaw)                      ║");
Console.WriteLine("║  * Insert new values at specific timestamps                  ║");
Console.WriteLine("║  * Update (upsert) existing values                           ║");
Console.WriteLine("║  * Replace existing values                                   ║");
Console.WriteLine("║  * Remove values by timestamp                                ║");
Console.WriteLine("║  * Delete all values in a time range (DeleteRaw)             ║");
Console.WriteLine("║  * Delete values at specific timestamps (DeleteAtTime)       ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Use Client Workshop 41 to test all operations.              ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to start recording.                             ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();

Console.WriteLine("Recording history every second... (CTRL+C to exit)");
var rng = new Random();
long cycle = 0;

while (true)
{
    cycle++;
    var now = DateTime.UtcNow;

    double t = 20.0 + Math.Sin(cycle * 0.1) * 10.0 + rng.NextDouble() * 2.0;
    double p = 1.0 + Math.Cos(cycle * 0.08) * 0.5 + rng.NextDouble() * 0.2;
    temperature.Value = Math.Round(t, 1);
    pressure.Value    = Math.Round(p, 2);

    server.RecordHistoryValue(temperature, now);
    server.RecordHistoryValue(pressure,    now);

    var hist = server.GetHistory(temperature.NodeId);
    Console.Write($"\r  Cycle={cycle}  T={temperature.Value:F1}C  " +
                  $"P={pressure.Value:F2}bar  History={hist.Count} entries  ");
    Thread.Sleep(1000);
}
