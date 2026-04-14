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
// PLCcom OPC UA Server SDK - Workshop 21: Simple Events
//
// OPC UA Events are notifications that something happened - not a value change,
// but a discrete occurrence like a state transition, a warning, or an action.
//
// Events are different from DataChange notifications:
//   DataChange: a variable's value changed (subscription-based)
//   Event:      something happened at a source node (event subscription)
//
// To use events:
//   1. Call EnableEvents() on the source node (folder or object)
//   2. Call FireEvent() to send an event to all subscribed clients
//   3. Clients subscribe to the source node's EventNotifier attribute
//
// Events have a severity level (1-1000):
//   Low (1-333):    informational, normal operation
//   Medium (334-666): warning, attention needed
//   High (667-1000): critical, immediate action required
//
// What you will learn:
//   * How to enable event notifications on a node
//   * How to fire events with different severity levels
//   * How clients subscribe to events in the Event View
//
// Connect with any OPC UA client to: opc.tcp://localhost:48410
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
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 21: Simple Events       ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This example demonstrates:                                  ║");
Console.WriteLine("║  * Enabling event notifications on nodes                     ║");
Console.WriteLine("║  * Firing events with message and severity                   ║");
Console.WriteLine("║  * Event severity levels (Low, Medium, High)                 ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

var config = new UaServerConfiguration
{
    ApplicationName = "PLCcom Workshop 21 - Simple Events",
    ApplicationUri  = "urn:localhost:PLCcom:Workshop:21",
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
var temp    = server.CreateVariable<double>(machine, "Temperature", initialValue: 22.0);

// -- Enable events on the source node ------------------------------------------
// EnableEvents() sets the EventNotifier attribute on the node.
// Without this, clients cannot subscribe to events from this node.
// Events fired on a node propagate up to the Server node automatically,
// so clients can also subscribe to the Server node to receive all events.
server.EnableEvents(machine);

// Fire an initial event to confirm the server started successfully
server.FireEvent(machine, "Machine1 started successfully", EventSeverity.Low);

Console.WriteLine("  Machine1: Events enabled");
Console.WriteLine("  Initial event fired: 'Machine1 started successfully'");
Console.WriteLine();

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║");
Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  To see events in the client:                                ║");
Console.WriteLine("║  1. Open Document -> Add -> Event View                       ║");
Console.WriteLine("║  2. In the Event View, click the '+' button and select       ║");
Console.WriteLine("║     Objects -> Server (to receive all events)                ║");
Console.WriteLine("║  3. Press ENTER here to start firing events                  ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to start firing events every 5 seconds.         ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();

Console.WriteLine("Firing events every 5 seconds... (CTRL+C to exit)");
Console.WriteLine("  Temperature > 30 -> High severity event");
Console.WriteLine("  Temperature > 25 -> Medium severity event");
Console.WriteLine("  Temperature <= 25 -> Low severity event");
Console.WriteLine();

var rng = new Random();

while (true)
{
    double t = 20.0 + rng.NextDouble() * 15.0;
    temp.Value = Math.Round(t, 1);

    // Fire events with different severity based on the temperature value.
    // The severity level is visible in the client's Event View as a color
    // or numeric value in the Severity column.
    if (t > 30.0)
    {
        server.FireEvent(machine, $"Temperature HIGH: {t:F1}C", EventSeverity.High);
        Console.WriteLine($"  [EVENT HIGH] Temperature = {t:F1}C");
    }
    else if (t > 25.0)
    {
        server.FireEvent(machine, $"Temperature warning: {t:F1}C", EventSeverity.Medium);
        Console.WriteLine($"  [EVENT MED]  Temperature = {t:F1}C");
    }
    else
    {
        server.FireEvent(machine, $"Temperature normal: {t:F1}C", EventSeverity.Low);
        Console.WriteLine($"  [EVENT LOW]  Temperature = {t:F1}C");
    }

    Thread.Sleep(5000);
}
