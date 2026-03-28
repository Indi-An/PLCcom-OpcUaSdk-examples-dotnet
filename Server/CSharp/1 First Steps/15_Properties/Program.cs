// ==============================================================================
// PLCcom OPC UA Server SDK - Workshop 15: Properties
//
// OPC UA variables can have Properties - child nodes that describe the variable.
// The most important standard properties are:
//
//   EURange (Engineering Unit Range):
//     Defines the physical min/max of the measured value.
//     HMI/SCADA clients use this to scale gauges, bar graphs and trend axes.
//     Example: Temperature sensor range -40..120 C
//
//   EngineeringUnits:
//     The unit label displayed next to the value in HMI clients.
//     Example: "C" for Celsius, "bar" for pressure, "rpm" for speed.
//
//   StatusCode:
//     Every OPC UA variable has a quality stamp (Good, Uncertain, Bad).
//     Clients display this as a color indicator or quality flag.
//     Use UpdateValue() to atomically set value + quality + timestamp.
//
// What you will learn:
//   * How to add EURange and EngineeringUnits to variables
//   * How to validate writes against the EURange
//   * How to set and change StatusCodes
//   * How to use UpdateValue for atomic quality updates
//
// Connect with any OPC UA client to: opc.tcp://localhost:48414
// ==============================================================================

using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Server.Sdk;
using System;
using System.Collections.Generic;
using System.Threading;

// -- License -------------------------------------------------------------------
//TODO
//Submit your license information from your license e-mail
string LicenseUserName = "<Enter your UserName here>";
string LicenseSerial = "<Enter your Serial here>";

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 15: Properties          ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This example demonstrates:                                  ║");
Console.WriteLine("║  * EURange - min/max limits for gauges and bar graphs        ║");
Console.WriteLine("║  * EngineeringUnits - unit labels (C, bar, rpm)              ║");
Console.WriteLine("║  * StatusCode - per-variable quality reporting               ║");
Console.WriteLine("║  * UpdateValue - atomic value + status + timestamp update    ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

var config = new UaServerConfiguration
{
    ApplicationName = "PLCcom Workshop 15 - Properties",
    ApplicationUri  = "urn:localhost:PLCcom:Workshop:15",
    ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
    BaseAddresses   = new List<string> { "opc.tcp://localhost:48414" },
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

var plant   = server.CreateFolder("Plant");
var machine = server.CreateFolder(plant, "Machine1");

var temperature = server.CreateVariable<double>(machine, "Temperature", initialValue: 22.0);
var pressure    = server.CreateVariable<double>(machine, "Pressure",    initialValue: 1.0);
var rpm         = server.CreateVariable<int>(machine, "RPM",            initialValue: 1500);

// -- EURange: defines the physical measurement range ---------------------------
// HMI clients use this to scale gauges and bar graphs automatically.
// The range does NOT automatically reject out-of-range writes - see OnWrite below.
temperature.SetEURange(low: -40.0, high: 120.0);
pressure.SetEURange(low: 0.0, high: 10.0);
rpm.SetEURange(low: 0, high: 3000);

// -- EngineeringUnits: the unit label shown in HMI clients ---------------------
// The second parameter is the long description (optional).
temperature.SetEngineeringUnits("C", "degree Celsius");
pressure.SetEngineeringUnits("bar");
rpm.SetEngineeringUnits("rpm", "revolutions per minute");

// -- OnWrite: validate writes against the EURange ------------------------------
// EURange is informational only - the server does NOT automatically reject
// out-of-range writes. Use OnWrite to enforce the range server-side.
// Return false to reject the write (client receives BadOutOfRange).
temperature.OnWrite = (value) =>
{
    if (value < -40.0 || value > 120.0)
    {
        Console.WriteLine($"\n  [REJECTED] Temperature={value} is outside EURange [-40..120]");
        return false;
    }
    Console.WriteLine($"\n  [ACCEPTED] Temperature={value}");
    return true;
};
pressure.OnWrite = (value) =>
{
    if (value < 0.0 || value > 10.0)
    {
        Console.WriteLine($"\n  [REJECTED] Pressure={value} is outside EURange [0..10]");
        return false;
    }
    Console.WriteLine($"\n  [ACCEPTED] Pressure={value}");
    return true;
};

Console.WriteLine("  Variables with properties:");
Console.WriteLine("    Temperature: EURange [-40..120], Unit: C    (write validated)");
Console.WriteLine("    Pressure:    EURange [0..10],    Unit: bar  (write validated)");
Console.WriteLine("    RPM:         EURange [0..3000],  Unit: rpm");
Console.WriteLine();

// -- StatusCode: set initial quality -------------------------------------------
// Good = sensor is working normally
// UncertainSensorNotAccurate = sensor is connected but reading may be off
temperature.StatusCode = StatusCodes.Good;
pressure.StatusCode    = StatusCodes.UncertainSensorNotAccurate;

Console.WriteLine("  StatusCodes:");
Console.WriteLine($"    Temperature: {temperature.StatusCode} (Good)");
Console.WriteLine($"    Pressure:    {pressure.StatusCode} (UncertainSensorNotAccurate)");
Console.WriteLine();

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║");
Console.WriteLine("║  opc.tcp://localhost:48414                                   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Try:                                                        ║");
Console.WriteLine("║  * Browse Temperature -> expand to see EURange and           ║");
Console.WriteLine("║    EngineeringUnits as child properties                      ║");
Console.WriteLine("║  * Write 122 to Temperature -> rejected (out of range)       ║");
Console.WriteLine("║  * Write 50 to Temperature -> accepted                       ║");
Console.WriteLine("║  * Check the quality indicator of Pressure (Uncertain)       ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to start simulation with StatusCode changes.    ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();

// -- Simulation: demonstrate StatusCode changes --------------------------------
// UpdateValue() sets value + StatusCode + timestamp in one atomic operation.
// This is important for SCADA systems that need consistent quality stamps.
Console.WriteLine("Simulating... sensor failure every 20 cycles. (CTRL+C to exit)");
var rng = new Random();
long cycle = 0;

while (true)
{
    cycle++;
    double t = 20.0 + rng.NextDouble() * 10.0;
    temperature.Value = Math.Round(t, 1);

    if (cycle % 20 == 0)
        // Simulate sensor failure: set Bad quality with zero value
        pressure.UpdateValue(0.0, StatusCodes.BadSensorFailure, DateTime.UtcNow);
    else
        // Normal operation: Good quality with measured value
        pressure.UpdateValue(Math.Round(0.9 + rng.NextDouble() * 0.3, 3),
            StatusCodes.Good, DateTime.UtcNow);

    rpm.Value = 1400 + rng.Next(200);

    Console.Write($"\r  Cycle={cycle}  T={temperature.Value:F1}C  " +
        $"P={pressure.Value:F3}bar [{(cycle % 20 == 0 ? "FAIL" : "OK  ")}]  RPM={rpm.Value}  ");
    Thread.Sleep(1000);
}
