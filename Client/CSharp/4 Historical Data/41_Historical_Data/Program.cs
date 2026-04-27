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
// PLCcom OPC UA Client SDK - Workshop 41: Historical Data Read
//
// OPC UA Historical Access (Part 11) lets clients read past values of variables
// using the HistoryRead service. The server must have history enabled on the
// variable (Historizing = true) - see Server Workshop 31.
//
// This workshop demonstrates all HistoryRead operations:
//   Subscribe    - monitor live values via subscription
//   ReadRaw      - read recorded values as-is
//   ReadModified - read values that were changed after recording
//   ReadAtTime   - read values at specific evenly-spaced timestamps
//   ReadProcessed- read aggregated values (Average, Min, Max, ...)
//
// For history write operations (Insert, Update, Replace, Delete)
// see Workshop 42 (Historical Data Update).
//
// Required server: Server Workshop 31 (Historical Access)
// opc.tcp://localhost:48410
// ==============================================================================

using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Client;
using PLCcom.Opc.Ua.Client.Sdk;
using System;
using System.Collections.Generic;

class Program
{
    static void Main(string[] args)
    {
        new Program().Start();
    }

    void Start()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 41: Historical Data     ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  OPC UA Historical Access lets you read past values.         ║");
        Console.WriteLine("║  The server stores timestamped values and returns them       ║");
        Console.WriteLine("║  on request - essential for trend analysis and reporting.    ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  What you will learn:                                        ║");
        Console.WriteLine("║    * Subscribe to live data changes                          ║");
        Console.WriteLine("║    * ReadRaw: read recorded values as-is                     ║");
        Console.WriteLine("║    * ReadModified: values changed after recording            ║");
        Console.WriteLine("║    * ReadAtTime: values at specific timestamps               ║");
        Console.WriteLine("║    * ReadProcessed: aggregated values (Average, Min, Max)    ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  For write operations see Workshop 42 (Historical Update)    ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Required server: Server Workshop 31 (Historical Access)     ║");
        Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        try
        {
            // -- License ----------------------------------------------------------
            // TODO: Replace with your license credentials from your license e-mail
            string LicenseUserName = "<Enter your UserName here>";
            string LicenseSerial   = "<Enter your Serial here>";

            // -- Step 1: Discover endpoints ---------------------------------------
            var endpoints = UaClient.GetEndpoints(new Uri("opc.tcp://localhost:48410"),
                certificateValidator: CertificateValidationHandler);
            endpoints = UaClient.SortEndpointsBySecurityLevel(endpoints);

            if (endpoints.Count == 0)
            {
                Console.WriteLine("  No endpoints found. Is Server Workshop 31 running?");
                Console.ReadLine();
                return;
            }

            Console.WriteLine($"  {endpoints.Count} endpoint(s) found:");
            for (int i = 0; i < endpoints.Count; i++)
                Console.WriteLine($"  [{i}] {endpoints[i].ToDisplayString()}");

            Console.WriteLine();
            Console.Write("  Please enter index of desired endpoint: ");
            if (!int.TryParse(Console.ReadLine(), out int idx) || idx < 0 || idx >= endpoints.Count)
            {
                Console.WriteLine("  Invalid selection.");
                Console.ReadLine();
                return;
            }

            // -- Step 2: Build SessionConfiguration -------------------------------
            var sessionConfig = SessionConfiguration.Build(
                System.Reflection.Assembly.GetEntryAssembly().GetName().Name,
                endpoints[idx]);
            sessionConfig.AutoConnect = false;

            // -- Step 3: Create client and connect --------------------------------
            using var client = new UaClient(LicenseUserName, LicenseSerial, sessionConfig);
            client.CertificateValidation += CertificateValidationHandler;
            client.ServerConnected       += (s, e) => Console.WriteLine($"  {DateTime.Now:T} Connected");
            client.ServerConnectionLost  += (s, e) => Console.WriteLine($"  {DateTime.Now:T} Connection lost");

            Console.Write("  Connecting ... ");
            client.Connect();
            Console.WriteLine("OK");
            Console.WriteLine();

            // -- Step 4: Resolve NodeId by browse path ----------------------------
            // Server 31 creates: Plant -> Sensor -> Temperature
            NodeId nodeId = client.GetNodeIdByPath("Objects.Plant.Sensor.Temperature");
            if (nodeId == null)
            {
                Console.WriteLine("  Could not find 'Objects.Plant.Sensor.Temperature'.");
                Console.WriteLine("  Is Server Workshop 31 running and recording history?");
                Console.ReadLine();
                return;
            }
            Console.WriteLine($"  Temperature NodeId: {nodeId}");
            Console.WriteLine();

            // -- Step 5: Command loop ---------------------------------------------
            while (true)
            {
                Console.WriteLine("  Select operation:");
                Console.WriteLine("  1 - Subscribe    (live data changes via subscription)");
                Console.WriteLine("  2 - ReadRaw      (all recorded values as stored)");
                Console.WriteLine("  3 - ReadModified (values changed after recording)");
                Console.WriteLine("  4 - ReadAtTime   (values at evenly-spaced timestamps)");
                Console.WriteLine("  5 - ReadProcessed(aggregated values: Average, Min, Max)");
                Console.WriteLine("  6 - Exit");
                Console.Write("  > ");

                string input = Console.ReadLine();
                if (string.IsNullOrEmpty(input) || input == "6") break;

                try
                {
                    switch (input)
                    {
                        case "1": // Subscribe - monitor live values via subscription
                        {
                            var subscription = new Subscription
                            {
                                PublishingInterval = 1000,
                                PublishingEnabled  = true
                            };
                            subscription.StateChanged += (sub, e) =>
                                Console.WriteLine($"  Subscription state: {e.Status}");
                            client.AddSubscription(subscription);

                            var item = new MonitoredItem((ITelemetryContext)null)
                            {
                                StartNodeId    = nodeId,
                                AttributeId    = Attributes.Value,
                                MonitoringMode = MonitoringMode.Reporting,
                                SamplingInterval = 500,
                                QueueSize      = uint.MaxValue,
                                DiscardOldest  = true,
                                DisplayName    = "Temperature"
                            };
                            item.Notification += (mi, e) =>
                            {
                                var n = e.NotificationValue as MonitoredItemNotification;
                                Console.WriteLine($"  {n.Value.SourceTimestamp.ToLocalTime():T}  " +
                                                  $"T={n.Value.Value}  {n.Value.StatusCode}");
                            };
                            subscription.AddItem(item);
                            subscription.ApplyChanges();
                            Console.WriteLine("  Monitoring... press ENTER to stop.");
                            Console.ReadLine();
                            break;
                        }

                        case "2": // ReadRaw - all recorded values as stored
                        {
                            // isReadModified=false: return original recorded values
                            var values = client.ReadRaw(nodeId,
                                DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow,
                                isReadModified: false);
                            PrintValues(values);
                            break;
                        }

                        case "3": // ReadModified - only values changed after recording
                        {
                            // isReadModified=true: return only values that were modified
                            // after they were originally recorded (e.g. via HistoryUpdate)
                            var values = client.ReadRaw(nodeId,
                                DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow,
                                isReadModified: true);
                            PrintValues(values);
                            break;
                        }

                        case "4": // ReadAtTime - 10 timestamps, 5s apart, ending now
                        {
                            Console.WriteLine("  ReadAtTime: 10 timestamps, 5s apart, ending now.");
                            Console.WriteLine("  Raw          = exact stored value");
                            Console.WriteLine("  Interpolated = calculated from surrounding values (OPC UA Part 11 §6.5.5)");
                            Console.WriteLine("  BadNoData    = no usable value found before this timestamp (OPC UA Part 11 §6.5.5)");
                            Console.WriteLine();
                            var values = client.ReadAtTime(nodeId,
                                DateTime.UtcNow.AddSeconds(-45), numValuesPerNode: 10, timeStep: 5000,
                                useSimpleBounds: false);
                            PrintValues(values);
                            break;
                        }

                        case "5": // ReadProcessed - server computes aggregate per interval
                        {
                            // The server calculates aggregates (Average, Min, Max, etc.)
                            // over each processing interval. Reduces data volume for long ranges.
                            var aggregates = client.GetAvailableAggregates();
                            Console.WriteLine("  Available aggregates: " +
                                              string.Join(", ", aggregates.Keys));
                            NodeId aggregateId = aggregates.ContainsKey("Average")
                                ? aggregates["Average"]
                                : aggregates["Interpolative"];
                            var values = client.ReadProcessed(nodeId, aggregateId,
                                DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow,
                                processingInterval: 60000);
                            PrintValues(values);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("  Error: " + ex.Message);
                }

                Console.WriteLine();
            }

            client.Disconnect();
        }
        catch (Exception ex)
        {
            Console.WriteLine("  Error: " + ex.Message);
            Console.WriteLine();
            Console.WriteLine("  Press ENTER to exit.");
            Console.ReadLine();
        }
    }

    static void PrintValues(HistoryData data)
    {
        if (data?.DataValues == null || data.DataValues.Count == 0)
        {
            Console.WriteLine("  (no values)");
            return;
        }
        foreach (var v in data.DataValues)
            Console.WriteLine($"  {v.SourceTimestamp.ToLocalTime():HH:mm:ss.fff}  " +
                              $"Value={v.Value,-12}  {v.StatusCode.ToDisplayString()}");
        Console.WriteLine($"  => {data.DataValues.Count} values");
    }

    void CertificateValidationHandler(CertificateValidator sender, CertificateValidationEventArgs e)
    {
        e.Accept = true;
    }
}
