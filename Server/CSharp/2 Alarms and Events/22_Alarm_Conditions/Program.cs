// ==============================================================================
// PLCcom OPC UA Server SDK - Workshop 22: Alarm Conditions
//
// OPC UA Alarms & Conditions (Part 9) extends the event model with stateful
// alarms that clients can acknowledge and confirm.
//
// An alarm has a lifecycle:
//   1. Inactive: process value is within normal range
//   2. Active + Unacknowledged: limit exceeded, operator must acknowledge
//   3. Active + Acknowledged: operator has seen the alarm
//   4. Inactive + Unacknowledged: condition cleared but not yet acknowledged
//   5. Inactive + Acknowledged: alarm fully resolved
//
// The Retain flag controls visibility in the Alarm & Conditions view:
//   Retain=true:  alarm is visible (active or unacknowledged)
//   Retain=false: alarm is resolved and can be removed from the list
//
// What you will learn:
//   * How to create alarms on a source node
//   * How to activate and deactivate alarms based on process values
//   * How to set alarm severity
//   * How clients acknowledge alarms in the Alarm & Conditions view
//
// Connect with any OPC UA client to: opc.tcp://localhost:48421
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
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 22: Alarm Conditions    ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This example demonstrates:                                  ║");
Console.WriteLine("║  * Creating alarms on source nodes                           ║");
Console.WriteLine("║  * Activating/deactivating alarms based on process values    ║");
Console.WriteLine("║  * Alarm severity levels                                     ║");
Console.WriteLine("║  * Clients can acknowledge alarms                            ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

var config = new UaServerConfiguration
{
    ApplicationName = "PLCcom Workshop 22 - Alarm Conditions",
    ApplicationUri  = "urn:localhost:PLCcom:Workshop:22",
    ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
    BaseAddresses   = new List<string> { "opc.tcp://localhost:48421" },
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
var reactor = server.CreateFolder(plant, "Reactor");

// EnableEvents() is required on the source node before creating alarms.
// The reactor folder becomes the event source for all alarms below it.
server.EnableEvents(reactor);

// Create process variables with engineering units and ranges
var temperature = server.CreateVariable<double>(reactor, "Temperature", initialValue: 25.0);
var pressure    = server.CreateVariable<double>(reactor, "Pressure",    initialValue: 1.0);

temperature.SetEURange(0, 200);
temperature.SetEngineeringUnits("C");
pressure.SetEURange(0, 10);
pressure.SetEngineeringUnits("bar");

// -- Create alarms on the source node ------------------------------------------
// CreateAlarm() creates an AlarmConditionState node under the source node.
// The alarm is initially inactive and enabled.
// Use Activate() / Deactivate() to change the alarm state.
var tempAlarm  = server.CreateAlarm(reactor, "TemperatureHighAlarm");
var pressAlarm = server.CreateAlarm(reactor, "PressureHighAlarm");

Console.WriteLine("  Reactor:");
Console.WriteLine("    Temperature (0-200 C) with HighAlarm at > 80C");
Console.WriteLine("    Pressure (0-10 bar) with HighAlarm at > 5 bar");
Console.WriteLine();

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║");
Console.WriteLine("║  opc.tcp://localhost:48421                                   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  To see alarms:                                              ║");
Console.WriteLine("║  1. Open Document -> Add -> Event View                       ║");
Console.WriteLine("║  2. Click '+' and select Objects -> Server                   ║");
Console.WriteLine("║  3. Press ENTER here to start the simulation                 ║");
Console.WriteLine("║  4. When an alarm appears, right-click -> Acknowledge        ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to start simulation.                            ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();

Console.WriteLine("Simulating... (CTRL+C to exit)");
Console.WriteLine("  Temperature alarm: > 80C ON, < 70C OFF");
Console.WriteLine("  Pressure alarm:    > 5 bar ON, < 4 bar OFF");
Console.WriteLine();

var rng = new Random();
bool tempActive = false, pressActive = false;

while (true)
{
    // Simulate oscillating process values
    double t = 50.0 + Math.Sin(DateTime.UtcNow.Ticks * 0.0000001) * 40.0 + rng.NextDouble() * 5.0;
    double p = 1.0 + (t - 50.0) / 30.0 + rng.NextDouble() * 0.5;
    temperature.Value = Math.Round(t, 1);
    pressure.Value    = Math.Round(p, 2);

    // -- Temperature alarm logic with hysteresis --------------------------------
    // Hysteresis (ON at 80, OFF at 70) prevents rapid toggling near the limit.
    if (t > 80.0 && !tempActive)
    {
        // Activate() sets the alarm to Active + Unacknowledged and fires an event.
        // The alarm appears in the client's Alarm & Conditions view.
        tempAlarm.Activate($"Temperature HIGH: {t:F1}C", EventSeverity.High);
        tempActive = true;
        Console.WriteLine($"\n  ALARM ON:  Temperature = {t:F1}C");
    }
    else if (t < 70.0 && tempActive)
    {
        // Deactivate() sets the alarm to Inactive and fires a return-to-normal event.
        // The alarm stays visible until acknowledged (Retain=false after ack).
        tempAlarm.Deactivate($"Temperature normal: {t:F1}C");
        tempActive = false;
        Console.WriteLine($"\n  ALARM OFF: Temperature = {t:F1}C");
    }

    // -- Pressure alarm logic --------------------------------------------------
    if (p > 5.0 && !pressActive)
    {
        pressAlarm.Activate($"Pressure HIGH: {p:F2} bar", EventSeverity.MediumHigh);
        pressActive = true;
        Console.WriteLine($"\n  ALARM ON:  Pressure = {p:F2} bar");
    }
    else if (p < 4.0 && pressActive)
    {
        pressAlarm.Deactivate($"Pressure normal: {p:F2} bar");
        pressActive = false;
        Console.WriteLine($"\n  ALARM OFF: Pressure = {p:F2} bar");
    }

    Console.Write($"\r  T={temperature.Value:F1}C{(tempActive ? " !" : "  ")}  " +
                  $"P={pressure.Value:F2}bar{(pressActive ? " !" : "  ")}  ");
    Thread.Sleep(1000);
}
