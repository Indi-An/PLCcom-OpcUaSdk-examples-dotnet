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
// PLCcom OPC UA Client SDK - Workshop 41: Historical Data
//
// OPC UA Historical Access lets you read past values of a variable.
// The server stores timestamped values and returns them on request.
// This is essential for trend analysis and reporting.
//
// What you will learn:
//   * How to read historical values for a time range
//   * How to handle continuation points for large result sets
//   * How to interpret historical DataValues with timestamps
//
// Target server: opc.tcp://localhost:48410
// ==============================================================================

// ==============================================================================
// PLCcom OPC UA Client SDK - Workshop 41: Historical Data
//
// OPC UA Historical Access (Part 11) lets clients read past values of variables
// using the HistoryRead service. The server must have history enabled on the
// variable (Historizing = true) - see Server Workshop 31.
//
// This workshop demonstrates all HistoryRead and HistoryUpdate operations:
//   ReadRaw      - read recorded values as-is
//   ReadModified - read values that were changed after recording
//   ReadAtTime   - read values at specific evenly-spaced timestamps
//   ReadProcessed- read aggregated values (Average, Min, Max, ...)
//   Insert       - add a new value into the history
//   Update       - insert or replace a value
//   Replace      - replace an existing value
//   Remove       - remove a value by timestamp
//   DeleteRaw    - delete all values in a time range
//   DeleteModified - delete modified values in a time range
//   DeleteAtTime - delete values at specific timestamps
//
// Requires Server Workshop 31 running on: opc.tcp://localhost:48410
// ==============================================================================

using PLCcom.Opc.Ua.Client.Sdk;
using System;
using System.Collections.Generic;
using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Client;

//TODO
//Submit your license information from your license e-mail
string LicenseUserName = "<Enter your UserName here>";
string LicenseSerial   = "<Enter your Serial here>";

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 41: Historical Data     ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  OPC UA Historical Access lets you read past values.         ║");
Console.WriteLine("║  The server stores timestamped values and returns them       ║");
Console.WriteLine("║  on request - essential for trend analysis.                  ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  What you will learn:                                        ║");
Console.WriteLine("║    * Read historical values for a time range                 ║");
Console.WriteLine("║    * Handle continuation points for large result sets        ║");
Console.WriteLine("║    * Interpret historical DataValues with timestamps         ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Requires Server Workshop 31 running on port 48430           ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// -- Connect -------------------------------------------------------------------
var endpoints = UaClient.GetEndpoints(new Uri("opc.tcp://localhost:48410"), 60000);
endpoints = UaClient.SortEndpointsBySecurityLevel(endpoints);

if (endpoints.Count == 0)
{
    Console.WriteLine("No endpoints found. Is Server Workshop 31 running?");
    Console.ReadLine();
    return;
}

Console.WriteLine("Endpoints found:");
for (int i = 0; i < endpoints.Count; i++)
    Console.WriteLine($"  {i} => {UaClient.EndpointToString(endpoints[i])}");

Console.Write("Select endpoint index: ");
if (!int.TryParse(Console.ReadLine(), out int idx) || idx < 0 || idx >= endpoints.Count)
{
    Console.WriteLine("Invalid selection."); Console.ReadLine(); return;
}

var sessionConfig = SessionConfiguration.Build(
    System.Reflection.Assembly.GetEntryAssembly().GetName().Name,
    endpoints[idx]);
sessionConfig.AutoConnect = true;

using var client = new UaClient(LicenseUserName, LicenseSerial, sessionConfig);
client.CertificateValidation += (s, e) => e.Accept = true;
client.ServerConnected       += (s, e) => Console.WriteLine($"{DateTime.Now:T} Connected");
client.ServerConnectionLost  += (s, e) => Console.WriteLine($"{DateTime.Now:T} Connection lost");

// Connect by reading a value - AutoConnect triggers the session
Console.Write("Connecting ... ");
client.Connect();
Console.WriteLine("OK");
Console.WriteLine();

// -- Resolve NodeId by browse path ---------------------------------------------
// Instead of hardcoding a numeric NodeId, we resolve it by path.
// This is more robust - the NodeId may change, the browse path won't.
NodeId nodeId = client.GetNodeIdByPath("Objects.Plant.Sensor.Temperature");
Console.WriteLine($"Temperature NodeId: {nodeId}");
Console.WriteLine();

// -- Command loop --------------------------------------------------------------
while (true)
{
    Console.WriteLine("Select operation:");
    foreach (int v in Enum.GetValues(typeof(HistoryReadOperation)))
        Console.WriteLine($"  {v} - {Enum.GetName(typeof(HistoryReadOperation), v)}");
    Console.Write("> ");

    string input = Console.ReadLine();
    if (string.IsNullOrEmpty(input)) break;

    try
    {
        HistoryData values = null;

        switch (input)
        {
            case "1": // Subscribe - monitor live values via subscription
                var subscription = new Subscription { PublishingInterval = 1000 };
                subscription.StateChanged += (sub, e) =>
                    Console.WriteLine($"Subscription state: {e.Status}");
                client.AddSubscription(subscription);

                var item = new MonitoredItem((ITelemetryContext)null)
                {
                    StartNodeId      = nodeId,
                    AttributeId      = Attributes.Value,
                    MonitoringMode   = MonitoringMode.Reporting,
                    SamplingInterval = 500,
                    QueueSize        = uint.MaxValue,
                    DiscardOldest    = true,
                    DisplayName      = "Temperature"
                };
                item.Notification += (mi, e) =>
                {
                    var n = e.NotificationValue as MonitoredItemNotification;
                    Console.WriteLine($"  {n.Value.SourceTimestamp:T}  T={n.Value.Value}  {n.Value.StatusCode}");
                };
                subscription.AddItem(item);
                subscription.ApplyChanges();
                Console.WriteLine("Monitoring... press ENTER to stop.");
                Console.ReadLine();
                break;

            case "2": // ReadRaw - all recorded values as stored
                values = client.ReadRaw(nodeId, DateTime.Now.AddMinutes(-10), DateTime.Now, false);
                PrintValues(values);
                break;

            case "3": // ReadModified - only values that were changed after recording
                values = client.ReadRaw(nodeId, DateTime.Now.AddMinutes(-10), DateTime.Now, true);
                PrintValues(values);
                break;

            case "4": // ReadAtTime - values at 10 evenly-spaced timestamps, 30s apart
                values = client.ReadAtTime(nodeId, DateTime.Now.AddMinutes(-5), 10, 30000, false);
                PrintValues(values);
                break;

            case "5": // ReadProcessed - server computes aggregate (e.g. Average) per interval
                // GetAvailableAggregates() queries the server for supported aggregate functions
                var aggregates = client.GetAvailableAggregates();
                Console.WriteLine("Available aggregates: " + string.Join(", ", aggregates.Keys));
                values = client.ReadProcessed(nodeId,
                    aggregates.ContainsKey("Average") ? aggregates["Average"] : aggregates["Interpolative"],
                    DateTime.Now.AddMinutes(-5),
                    DateTime.Now,
                    60000); // one aggregate value per 60 seconds
                PrintValues(values);
                break;

            case "6": // Insert - add a new value at current timestamp
            {
                Console.Write("Value to insert: ");
                var dv = new DataValue
                {
                    SourceTimestamp = DateTime.UtcNow,
                    ServerTimestamp = DateTime.UtcNow,
                    StatusCode      = new StatusCode(StatusCodes.GoodEntryInserted),
                    Value           = Console.ReadLine()
                };
                var result = client.Insert(nodeId, new List<DataValue> { dv });
                Console.WriteLine("Result: " + result[0].OperationResults[0]);
                break;
            }

            case "7": // Update - insert if not exists, replace if exists
            {
                Console.Write("Value to update: ");
                var dv = new DataValue
                {
                    SourceTimestamp = DateTime.UtcNow,
                    ServerTimestamp = DateTime.UtcNow,
                    StatusCode      = new StatusCode(StatusCodes.GoodEntryInserted),
                    Value           = Console.ReadLine()
                };
                var result = client.Update(nodeId, new List<DataValue> { dv });
                Console.WriteLine("Result: " + result[0].OperationResults[0]);
                break;
            }

            case "8": // Replace - replace existing value at timestamp (fails if not exists)
            {
                Console.Write("Value to replace: ");
                var dv = new DataValue
                {
                    SourceTimestamp = DateTime.UtcNow,
                    ServerTimestamp = DateTime.UtcNow,
                    StatusCode      = new StatusCode(StatusCodes.GoodEntryInserted),
                    Value           = Console.ReadLine()
                };
                var result = client.Replace(nodeId, new List<DataValue> { dv });
                Console.WriteLine("Result: " + result[0].OperationResults[0]);
                break;
            }

            case "9": // Remove - remove value at current timestamp
            {
                Console.Write("Value (timestamp marker): ");
                var dv = new DataValue
                {
                    SourceTimestamp = DateTime.UtcNow,
                    ServerTimestamp = DateTime.UtcNow,
                    Value           = Console.ReadLine()
                };
                var result = client.Remove(nodeId, new List<DataValue> { dv });
                Console.WriteLine("Result: " + result[0].OperationResults[0]);
                break;
            }

            case "10": // DeleteRaw - delete all values in a time range
            {
                var result = client.DeleteRaw(nodeId, DateTime.Now.AddMinutes(-2), DateTime.Now, false);
                foreach (var r in result) Console.WriteLine("Result: " + r.StatusCode);
                break;
            }

            case "11": // DeleteModified - delete modified values in a time range
            {
                var result = client.DeleteRaw(nodeId, DateTime.Now.AddMinutes(-2), DateTime.Now, true);
                foreach (var r in result) Console.WriteLine("Result: " + r.StatusCode);
                break;
            }

            case "12": // DeleteAtTime - delete values at 5 specific timestamps, 30s apart
            {
                var result = client.DeleteAtTime(nodeId, DateTime.Now.AddMinutes(-2), 5, 30000);
                foreach (var r in result) Console.WriteLine("Result: " + r.StatusCode);
                break;
            }

            case "13": // Exit
                goto done;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("Error: " + ex.Message);
    }

    Console.WriteLine();
}

done:
client.Disconnect();

static void PrintValues(HistoryData data)
{
    if (data?.DataValues == null || data.DataValues.Count == 0)
    {
        Console.WriteLine("  (no values)");
        return;
    }
    foreach (var v in data.DataValues)
        Console.WriteLine($"  {v.SourceTimestamp.ToLocalTime():T}  Value={v.Value,-10}  {v.StatusCode}");
    Console.WriteLine($"  => {data.DataValues.Count} values");
}
