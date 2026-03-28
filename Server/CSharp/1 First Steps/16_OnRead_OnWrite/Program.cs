// ==============================================================================
// PLCcom OPC UA Server SDK - Workshop 16: OnRead / OnWrite Callbacks
//
// By default, OPC UA variables cache their value in memory.
// Callbacks let you intercept reads and writes to add custom logic:
//
//   OnRead:  Called every time a client reads the variable.
//            Return a fresh value from hardware, a database, or any source.
//            Useful for variables that must always reflect the current state.
//
//   OnWrite: Called before a client write is accepted.
//            Return true to accept the new value, false to reject it.
//            Useful for validation, range checking, or write-through to hardware.
//
// What you will learn:
//   * How to use OnRead to deliver a live value on every read
//   * How to use OnWrite to validate and accept/reject client writes
//   * How to use OnWrite to log all changes
//
// Connect with any OPC UA client to: opc.tcp://localhost:48415
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
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 16: OnRead / OnWrite    ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This example demonstrates:                                  ║");
Console.WriteLine("║  * OnRead - fresh value on every client read                 ║");
Console.WriteLine("║  * OnWrite - validate and accept/reject client writes        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

var config = new UaServerConfiguration
{
    ApplicationName = "PLCcom Workshop 16 - OnRead/OnWrite",
    ApplicationUri  = "urn:localhost:PLCcom:Workshop:16",
    ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
    BaseAddresses   = new List<string> { "opc.tcp://localhost:48415" },
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
var rng     = new Random();

// -- OnRead: Deliver a fresh value on every read -------------------------------
// Without OnRead, the server returns the cached value.
// With OnRead, the lambda is called on every Read or Subscription sample.
// The parameter 'currentValue' is the cached value - you can use it or ignore it.
// This is ideal for variables that map directly to hardware registers.
var cpuLoad = server.CreateVariable<double>(machine, "CpuLoad",
    initialValue: 0.0, readOnly: true);

cpuLoad.OnRead = (currentValue) =>
{
    // Simulate reading from hardware - returns a new value every time
    double value = Math.Round(rng.NextDouble() * 100.0, 1);
    Console.WriteLine($"  [OnRead] CpuLoad -> {value}%");
    return value;
};

// -- OnWrite: Validate before accepting ----------------------------------------
// The lambda receives the value the client wants to write.
// Return true to accept (value is stored and clients are notified).
// Return false to reject (client receives BadOutOfRange status code).
var targetTemp = server.CreateVariable<double>(machine, "TargetTemperature",
    initialValue: 22.0);

targetTemp.OnWrite = (newValue) =>
{
    bool accepted = newValue >= 10.0 && newValue <= 50.0;
    if (accepted)
        Console.WriteLine($"  [OnWrite] TargetTemperature = {newValue:F1} -> ACCEPTED");
    else
        Console.WriteLine($"  [OnWrite] TargetTemperature = {newValue:F1} -> REJECTED (must be 10..50)");
    return accepted;
};

// -- OnWrite: Log all changes --------------------------------------------------
// You can also use OnWrite just for side effects (logging, forwarding to PLC)
// while always returning true to accept the write.
var speed = server.CreateVariable<int>(machine, "SpeedSetpoint", initialValue: 1000);

speed.OnWrite = (newValue) =>
{
    // Log the change - old value is still in speed.Value at this point
    Console.WriteLine($"  [OnWrite] SpeedSetpoint changed: {speed.Value} -> {newValue}");
    return true; // always accept
};

Console.WriteLine("  Variables:");
Console.WriteLine("    CpuLoad           [ReadOnly, OnRead]  -> random 0-100 on every read");
Console.WriteLine("    TargetTemperature [OnWrite]           -> accepts 10.0 .. 50.0 only");
Console.WriteLine("    SpeedSetpoint     [OnWrite]           -> logs all changes");
Console.WriteLine();

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║");
Console.WriteLine("║  opc.tcp://localhost:48415                                   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Try:                                                        ║");
Console.WriteLine("║  * Read CpuLoad multiple times - value changes each time     ║");
Console.WriteLine("║  * Subscribe to CpuLoad - new value on every sample          ║");
Console.WriteLine("║  * Write 25.0 to TargetTemperature -> accepted               ║");
Console.WriteLine("║  * Write 99.0 to TargetTemperature -> rejected (BadRange)    ║");
Console.WriteLine("║  * Write any value to SpeedSetpoint -> logged in console     ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to exit.                                        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();
