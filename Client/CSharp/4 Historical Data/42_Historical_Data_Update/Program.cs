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
// PLCcom OPC UA Client SDK - Workshop 42: Historical Data Update
//
// OPC UA Historical Access (Part 11) also allows clients to modify the
// history stored on the server. This is useful for:
//   * Correcting wrong values recorded by a sensor
//   * Back-filling missing data (e.g. after a server restart)
//   * Removing erroneous entries
//
// This workshop demonstrates all HistoryUpdate operations:
//   Insert       - add a new value (fails if timestamp already exists)
//   Update       - insert or replace (upsert)
//   Replace      - replace an existing value (fails if not exists)
//   Remove       - remove a value by timestamp
//   DeleteRaw    - delete all values in a time range
//   DeleteModified - delete modified values in a time range
//   DeleteAtTime - delete values at specific timestamps
//
// For read operations see Workshop 41 (Historical Data Read).
//
// Required server: Server Workshop 32 (Historical Update)
// opc.tcp://localhost:48410
// ==============================================================================

using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Client;
using PLCcom.Opc.Ua.Client.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;

class Program
{
    static void Main(string[] args)
    {
        new Program().Start();
    }

    void Start()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 42: Historical Update   ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  OPC UA allows clients to modify history stored on the       ║");
        Console.WriteLine("║  server - useful for correcting values, back-filling         ║");
        Console.WriteLine("║  missing data or removing erroneous entries.                 ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  What you will learn:                                        ║");
        Console.WriteLine("║    * Insert: add a new value at a specific timestamp         ║");
        Console.WriteLine("║    * Update: insert or replace (upsert)                      ║");
        Console.WriteLine("║    * Replace: replace an existing value                      ║");
        Console.WriteLine("║    * Remove: remove a value by timestamp                     ║");
        Console.WriteLine("║    * DeleteRaw: delete all values in a time range            ║");
        Console.WriteLine("║    * DeleteModified: delete modified values in a range       ║");
        Console.WriteLine("║    * DeleteAtTime: delete values at specific timestamps      ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  For read operations see Workshop 41 (Historical Read)       ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Required server: Server Workshop 32 (Historical Update)     ║");
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
                Console.WriteLine("  No endpoints found. Is Server Workshop 32 running?");
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
            // Server 32 creates: Plant -> Sensor -> Temperature
            NodeId nodeId = client.GetNodeIdByPath("Objects.Plant.Sensor.Temperature");
            if (nodeId == null)
            {
                Console.WriteLine("  Could not find 'Objects.Plant.Sensor.Temperature'.");
                Console.WriteLine("  Is Server Workshop 32 running?");
                Console.ReadLine();
                return;
            }
            Console.WriteLine($"  Temperature NodeId: {nodeId}");
            Console.WriteLine();

            // -- Step 5: Command loop ---------------------------------------------
            while (true)
            {
                Console.WriteLine("  Select operation:");
                Console.WriteLine("  1 - Insert        (add new value, fails if timestamp exists)");
                Console.WriteLine("  2 - Update        (insert or replace - upsert)");
                Console.WriteLine("  3 - Replace       (replace existing, fails if not exists)");
                Console.WriteLine("  4 - Remove        (remove value at current timestamp)");
                Console.WriteLine("  5 - DeleteRaw     (delete all values in last 2 minutes)");
                Console.WriteLine("  6 - DeleteModified(delete modified values in last 2 minutes)");
                Console.WriteLine("  7 - DeleteAtTime  (delete values at 5 specific timestamps)");
                Console.WriteLine("  8 - ReadRaw       (verify: read back last 10 minutes)");
                Console.WriteLine("  9 - Exit");
                Console.Write("  > ");

                string input = Console.ReadLine();
                if (string.IsNullOrEmpty(input) || input == "9") break;

                try
                {
                    switch (input)
                    {
                        case "1": // Insert - add a new value, fails if timestamp already exists
                        {
                            Console.Write("  Value to insert: ");
                            var dv = new DataValue
                            {
                                SourceTimestamp = DateTime.UtcNow,
                                Value           = double.Parse(Console.ReadLine())
                            };
                            var result = client.Insert(nodeId, new List<DataValue> { dv });
                            PrintResult(result[0]);
                            break;
                        }

                        case "2": // Update - insert if not exists, replace if exists (upsert)
                        {
                            Console.Write("  Value to update: ");
                            var dv = new DataValue
                            {
                                SourceTimestamp = DateTime.UtcNow,
                                Value           = double.Parse(Console.ReadLine())
                            };
                            var result = client.Update(nodeId, new List<DataValue> { dv });
                            PrintResult(result[0]);
                            break;
                        }

                        case "3": // Replace - replace existing value at a stored timestamp
                        {
                            // Replace requires the exact timestamp of an existing entry.
                            // We read the last stored value and replace it.
                            var existing = client.ReadRaw(nodeId, DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow, false);
                            if (existing?.DataValues == null || existing.DataValues.Count == 0)
                            {
                                Console.WriteLine("  No existing values. Insert first.");
                                break;
                            }
                            var last = existing.DataValues[^1];
                            Console.WriteLine($"  Replacing value at {last.SourceTimestamp.ToLocalTime():T} (was {last.Value})");
                            Console.Write("  New value: ");
                            var dv = new DataValue
                            {
                                SourceTimestamp = last.SourceTimestamp,
                                Value           = double.Parse(Console.ReadLine())
                            };
                            var result = client.Replace(nodeId, new List<DataValue> { dv });
                            PrintResult(result[0]);
                            break;
                        }

                        case "4": // Remove - remove the last stored value
                        {
                            // Remove requires the exact timestamp of an existing entry.
                            var existing = client.ReadRaw(nodeId, DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow, false);
                            if (existing?.DataValues == null || existing.DataValues.Count == 0)
                            {
                                Console.WriteLine("  No existing values.");
                                break;
                            }
                            var last = existing.DataValues[^1];
                            Console.WriteLine($"  Removing value at {last.SourceTimestamp.ToLocalTime():T} (value={last.Value})");
                            var dv = new DataValue { SourceTimestamp = last.SourceTimestamp };
                            var result = client.Remove(nodeId, new List<DataValue> { dv });
                            PrintResult(result[0]);
                            break;
                        }

                        case "5": // DeleteRaw - delete all values in a time range
                        {
                            // isModified=false: delete original recorded values
                            var result = client.DeleteRaw(nodeId,
                                DateTime.UtcNow.AddMinutes(-2), DateTime.UtcNow,
                                isModified: false);
                            foreach (var r in result)
                                Console.WriteLine("  Result: " + r.StatusCode.ToString());
                            break;
                        }

                        case "6": // DeleteModified - delete modified values in a time range
                        {
                            // isModified=true: delete only values that were modified
                            // after original recording (e.g. via Insert/Update/Replace)
                            var result = client.DeleteRaw(nodeId,
                                DateTime.UtcNow.AddMinutes(-2), DateTime.UtcNow,
                                isModified: true);
                            foreach (var r in result)
                                Console.WriteLine("  Result: " + r.StatusCode.ToString());
                            break;
                        }

                        case "7": // DeleteAtTime - delete values at exact stored timestamps
                        {
                            // DeleteAtTime requires exact timestamps that exist in the history.
                            // We read the last stored values and delete those.
                            var existing = client.ReadRaw(nodeId, DateTime.UtcNow.AddMinutes(-2), DateTime.UtcNow, false);
                            if (existing?.DataValues == null || existing.DataValues.Count == 0)
                            {
                                Console.WriteLine("  No existing values.");
                                break;
                            }
                            int count = Math.Min(5, existing.DataValues.Count);
                            var times = existing.DataValues.Take(count).Select(v => v.SourceTimestamp).ToList();
                            Console.WriteLine($"  Before: {existing.DataValues.Count} values in last 2 minutes");
                            Console.WriteLine($"  Deleting {count} values at exact stored timestamps:");
                            for (int i = 0; i < times.Count; i++)
                                Console.WriteLine($"    [{i}] {times[i].ToLocalTime():HH:mm:ss.fff}");
                            var result = client.DeleteAtTime(nodeId, times);
                            PrintResult(result[0]);
                            var after = client.ReadRaw(nodeId, DateTime.UtcNow.AddMinutes(-2), DateTime.UtcNow, false);
                            Console.WriteLine($"  After:  {after?.DataValues?.Count ?? 0} values remaining");
                            break;
                        }

                        case "8": // ReadRaw - verify changes by reading back
                        {
                            var values = client.ReadRaw(nodeId,
                                DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow,
                                isReadModified: false);
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

    static void PrintResult(HistoryUpdateResult result)
    {
        Console.WriteLine("  Result: " + result.StatusCode.ToString());
        if (result.OperationResults != null)
            for (int i = 0; i < result.OperationResults.Count; i++)
                Console.WriteLine($"    [{i}] {(StatusCode.IsGood(result.OperationResults[i]) ? "OK" : result.OperationResults[i].ToString())}");
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
                              $"Value={v.Value,-10}  {v.StatusCode.ToDisplayString()}");
        Console.WriteLine($"  => {data.DataValues.Count} values");
    }

    void CertificateValidationHandler(CertificateValidator sender, CertificateValidationEventArgs e)
    {
        e.Accept = true;
    }
}
