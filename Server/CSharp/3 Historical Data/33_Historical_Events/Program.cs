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
// PLCcom OPC UA Server SDK - Workshop 33: Historical Events
//
// OPC UA servers can store events in a history that clients can query later.
// This is useful for audit trails, alarm history and post-mortem analysis.
//
// This workshop demonstrates:
//   1. EnableEvents() on a source node (required for live events)
//   2. EnableHistoryEvents() on the same node (enables HistoryRead for events)
//   3. FireEvent() to send live events to subscribed clients
//   4. RecordHistoryEvent() to store the event in the history
//   5. Clients use HistoryRead with ReadEventDetails to query past events
//
// The event history is stored in memory with a configurable maximum size.
// For production use, you would store events in a database.
//
// What you will learn:
//   * How to enable event history on a source node
//   * How to record events in the history store
//   * How clients read historical events via HistoryRead
//   * The difference between live events and historical events
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
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 33: Historical Events   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This example demonstrates:                                  ║");
Console.WriteLine("║  * Enabling event history on source nodes                    ║");
Console.WriteLine("║  * Recording events in the history store                     ║");
Console.WriteLine("║  * Clients can query past events via HistoryRead             ║");
Console.WriteLine("║  * Live events AND historical events from the same source    ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

var config = new UaServerConfiguration
{
    ApplicationName = "PLCcom Workshop 33 - Historical Events",
    ApplicationUri  = "urn:localhost:PLCcom:Workshop:33",
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
    NamespaceUri     = "http://indi-an.com/opcua/workshop/historical-events",
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

var temperature = server.CreateVariable<double>(reactor, "Temperature", initialValue: 25.0);
temperature.SetEURange(0, 200);
temperature.SetEngineeringUnits("C", "Degrees Celsius");

// Step 1: Enable live events on the reactor node.
// This allows clients to subscribe to events from this node.
server.EnableEvents(reactor);

// Step 2: Enable event history on the same node.
// This sets EventNotifier.HistoryRead so clients know they can query past events.
// maxEntries limits the in-memory buffer (oldest events are discarded).
server.EnableHistoryEvents(reactor, maxEntries: 500);

Console.WriteLine("  Reactor:");
Console.WriteLine("    Temperature (0-200 C)");
Console.WriteLine("    Events: live + history enabled (max 500 entries)");
Console.WriteLine();

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║");
Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  To see live events:                                         ║");
Console.WriteLine("║  1. Open Document -> Add -> Event View                       ║");
Console.WriteLine("║  2. Click '+' and select Objects -> Server                   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  To read historical events:                                  ║");
Console.WriteLine("║  1. Use Client Workshop 42 (Read Historical Events)          ║");
Console.WriteLine("║  2. Or use HistoryRead with ReadEventDetails in any client   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to start the simulation.                        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();

Console.WriteLine("Simulating... events fire every 5 seconds (CTRL+C to exit)");
Console.WriteLine("  Temperature > 80C -> High severity event");
Console.WriteLine("  Temperature > 60C -> Medium severity event");
Console.WriteLine("  Temperature <= 60C -> Low severity event");
Console.WriteLine();

var rng = new Random();
long cycle = 0;

while (true)
{
    cycle++;

    double t = 50.0 + Math.Sin(cycle * 0.15) * 40.0 + rng.NextDouble() * 5.0;
    temperature.Value = Math.Round(t, 1);

    // Determine severity based on temperature
    EventSeverity severity;
    string message;
    if (t > 80.0)
    {
        severity = EventSeverity.High;
        message = $"Temperature HIGH: {t:F1}C";
    }
    else if (t > 60.0)
    {
        severity = EventSeverity.Medium;
        message = $"Temperature warning: {t:F1}C";
    }
    else
    {
        severity = EventSeverity.Low;
        message = $"Temperature normal: {t:F1}C";
    }

    // Step 3: Fire a live event.
    // Clients with an active event subscription will receive this immediately.
    server.FireEvent(reactor, message, severity);

    // Step 4: Record the same event in the history store.
    // This creates a BaseEventState, initializes it and stores it.
    // Clients can later query this via HistoryRead with ReadEventDetails.
    var eventState = new BaseEventState(null);
    eventState.Initialize(
        server.NodeManager.SystemContext,
        server.NodeManager.FindNodeInAddressSpace(reactor.NodeId),
        severity,
        new LocalizedText(message));
    eventState.Create(server.NodeManager.SystemContext, null, new QualifiedName("Event"), null, true);
    server.RecordHistoryEvent(reactor.NodeId, eventState);

    string severityLabel = severity == EventSeverity.High ? "HIGH" :
                           severity == EventSeverity.Medium ? "MED " : "LOW ";
    Console.WriteLine($"  [{severityLabel}] {message}");

    Thread.Sleep(5000);
}
