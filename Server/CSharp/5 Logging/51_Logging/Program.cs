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
// PLCcom OPC UA Server SDK - Workshop 51: Logging
//
// The SDK exposes the OPC UA stack's internal trace messages through the
// LogMessage event. This lets you route SDK diagnostics to your own
// logging framework (NLog, Serilog, Microsoft.Extensions.Logging, etc.)
//
// Log levels (from least to most verbose):
//   Error   -> only errors that affect functionality
//   Warning -> errors + warnings (recommended for production)
//   Info    -> errors + warnings + service calls (connect, read, write, subscribe)
//   Debug   -> everything including internal stack details (very verbose)
//
// Use cases:
//   * Troubleshooting connection problems
//   * Auditing client access (who connected, what they read/wrote)
//   * Performance monitoring
//   * Integration with your application's logging infrastructure
//
// What you will learn:
//   * How to subscribe to SDK log messages
//   * How to set the log verbosity level
//   * How to filter and format log messages
//   * How to route logs to your own framework
//
// Connect with any OPC UA client to: opc.tcp://localhost:48410
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
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 51: Logging             ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This example demonstrates:                                  ║");
Console.WriteLine("║  * Subscribing to SDK log messages                           ║");
Console.WriteLine("║  * Setting log verbosity (Error, Warning, Info, Debug)       ║");
Console.WriteLine("║  * Filtering log messages by level                           ║");
Console.WriteLine("║  * Routing logs to your own framework                        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

var config = new UaServerConfiguration
{
    ApplicationName = "PLCcom Workshop 51 - Logging",
    ApplicationUri  = "urn:localhost:PLCcom:Workshop:51",
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

// -- Subscribe to log messages -------------------------------------------------
// The LogMessage event fires for every message that passes the current log level.
// Subscribe BEFORE calling Start() to capture startup messages.
// In production, replace Console.WriteLine with your logging framework call.
server.LogMessage += (sender, e) =>
{
    // Color-code by severity for easy reading in the console
    var color = e.Level switch
    {
        UaLogLevel.Error   => ConsoleColor.Red,
        UaLogLevel.Warning => ConsoleColor.Yellow,
        UaLogLevel.Info    => ConsoleColor.Cyan,
        _                  => ConsoleColor.Gray   // Debug
    };

    Console.ForegroundColor = color;
    Console.WriteLine($"  [{e.Level,-7}] {e.Message}");
    Console.ResetColor();

    // Example: forward to your logging framework
    // logger.Log(e.Level, e.Message, e.Exception);
};

// -- Set log level -------------------------------------------------------------
// SetLogLevel() controls which messages are generated by the OPC UA stack.
// Call this before Start() to capture startup messages at the desired level.
// Changing the level at runtime is also supported.
//
//   Error   -> only errors (minimal output, good for production)
//   Warning -> errors + warnings
//   Info    -> errors + warnings + service calls (good for development)
//   Debug   -> everything (very verbose, use only for troubleshooting)
server.SetLogLevel(UaLogLevel.Info);

Console.WriteLine("  Log level set to: Info");
Console.WriteLine("  (connect a client to see log messages appear here)");
Console.WriteLine();

Console.Write("Starting server ... ");
try { server.Start(config); }
catch (Exception ex) { Console.WriteLine("FAILED"); Console.WriteLine(ex.Message); Console.ReadLine(); return; }
Console.WriteLine("OK");
Console.WriteLine();

var plant = server.CreateFolder("Plant");
server.CreateVariable<double>(plant, "Temperature", initialValue: 22.0);

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running with logging enabled.                     ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Endpoint: opc.tcp://localhost:48410                         ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Try:                                                        ║");
Console.WriteLine("║  * Connect a client - see the session creation log entry     ║");
Console.WriteLine("║  * Read Temperature - see the Read service log entry         ║");
Console.WriteLine("║  * Subscribe to Temperature - see subscription log entries   ║");
Console.WriteLine("║  * Disconnect - see the session close log entry              ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to exit.                                        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();
