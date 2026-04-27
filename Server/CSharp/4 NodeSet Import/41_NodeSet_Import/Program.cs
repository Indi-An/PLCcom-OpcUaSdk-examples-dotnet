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
// PLCcom OPC UA Server SDK - Workshop 41: NodeSet Import
//
// OPC UA NodeSet2 XML is the standard format for sharing address space
// definitions. It is used by:
//   * OPC UA Companion Specifications (PackML, Euromap, DI, Machinery, etc.)
//   * Vendor-specific type libraries
//   * Pre-defined address space templates
//
// A NodeSet XML file contains:
//   * Type definitions (ObjectTypes, VariableTypes, DataTypes)
//   * Namespace URIs
//   * Optionally: pre-built instances
//
// After importing, the types appear in the server's type hierarchy and
// can be used to create typed instances with CreateObject().
//
// This workshop includes a ready-to-use sample NodeSet:
//   PLCcom_Workshop_NodeSet.xml
// It defines MotorType and SensorType with two instances each.
//
// What you will learn:
//   * How to import a NodeSet2.xml file into the server
//   * How namespaces from the NodeSet are registered automatically
//   * How to verify the imported nodes in the address space
//
// Connect with any OPC UA client to: opc.tcp://localhost:48410
// ==============================================================================

using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Server.Sdk;
using System;
using System.Collections.Generic;
using System.IO;

// -- License -------------------------------------------------------------------
//TODO
//Submit your license information from your license e-mail
string LicenseUserName = "<Enter your UserName here>";
string LicenseSerial = "<Enter your Serial here>";

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 41: NodeSet Import      ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This example demonstrates:                                  ║");
Console.WriteLine("║  * Importing NodeSet2.xml files into the address space       ║");
Console.WriteLine("║  * Automatic namespace registration                          ║");
Console.WriteLine("║  * Types and instances from companion specifications         ║");
Console.WriteLine("║  * Verifying imported nodes                                  ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

var config = CreateConfig();
PrintConfig(config);

using var server = new UaServer(LicenseUserName, LicenseSerial);
server.CertificateValidation += (s, e) => e.Accept = true;

Console.Write("Starting server ... ");
try { server.Start(config); }
catch (Exception ex) { Console.WriteLine("FAILED"); Console.WriteLine(ex.Message); Console.ReadLine(); return; }
Console.WriteLine("OK");
Console.WriteLine();

// -- Import NodeSet XML --------------------------------------------------------
// ImportNodeSet() reads the XML file and adds all nodes to the address space.
// Namespaces defined in the NodeSet are automatically registered.
// The method returns the number of nodes imported.
//
// The NodeSet2 XML format is defined by the OPC Foundation in:
//   OPC UA Specification Part 6 - Mappings, Annex F (UANodeSet XML Schema)
//   Schema file: UANodeSet.xsd  (included in the OPC UA specification download
//   at https://opcfoundation.org/developer-tools/specifications-unified-architecture)
//
// ── Namespace rules for NodeSet XML files ─────────────────────────────────────
//
//   Server namespace layout:
//     ns=0  OPC UA Standard (http://opcfoundation.org/UA/)
//     ns=1  SDK internal – not available for user nodes
//     ns=2  Server Application Namespace (from config.NamespaceUri)
//     ns=3+ Additional namespaces (registered via <NamespaceUris> in the XML)
//
//   Rules for authoring NodeSet XML files:
//     1. Namespace indices in the XML are ABSOLUTE server indices.
//        ns=2 in the file means ns=2 on the server. ns=3 means ns=3.
//     2. Every namespace used in the file (except ns=0) MUST be declared
//        in <NamespaceUris>. The server application namespace (ns=2) must
//        also be listed if nodes use it.
//     3. If a namespace referenced by a node is NOT declared in
//        <NamespaceUris>, the node falls back to ns=2.
//     4. AccessLevel="3" (CurrentRead | CurrentWrite) must be set explicitly
//        on writable UAVariable nodes. Without it, the OPC UA spec default
//        is ReadOnly (Part 6, Table F.8).
//     5. Properties (HasProperty) like SerialNumber or Unit are typically
//        left without AccessLevel → ReadOnly by design.
//
//   The SDK's ImportNodeSet() handles the namespace mapping internally
//   by padding the NamespaceUris array so the OPC UA stack's file-relative
//   remapping produces the correct absolute server indices.
//
// ── Sample NodeSet ─────────────────────────────────────────────────────────────
//
// PLCcom_Workshop_NodeSet.xml is included with this workshop.
// It defines two namespaces:
//   ns=2  Server App Namespace → SensorType, TempSensor1, PressureSensor1
//   ns=3  urn:plccom:workshop:nodeset → MotorType, Motor1, Motor2
//
// Types:
//   SensorType (ns=2) - Value (Double), Unit (String), InAlarm (Boolean)
//   MotorType  (ns=3) - Speed (Double), Running (Boolean), SerialNumber (String)
// Instances:
//   Sensors/ (ns=2) - TempSensor1, PressureSensor1
//   Motors/  (ns=3) - Motor1, Motor2
string nodeSetPath = "PLCcom_Workshop_NodeSet.xml";

if (File.Exists(nodeSetPath))
{
    Console.WriteLine($"  Importing: {nodeSetPath}");
    int count = server.ImportNodeSet(nodeSetPath);
    Console.WriteLine($"  Imported {count} nodes successfully");
    Console.WriteLine();
    Console.WriteLine("  Nodes imported:");
    Console.WriteLine("    Types    -> Types/ObjectTypes/SensorType  (ns=2)");
    Console.WriteLine("    Types    -> Types/ObjectTypes/MotorType   (ns=3)");
    Console.WriteLine("    Instance -> Objects/Sensors/TempSensor1, PressureSensor1  (ns=2)");
    Console.WriteLine("    Instance -> Objects/Motors/Motor1, Motor2                (ns=3)");
}
else
{
    Console.WriteLine($"  ERROR: '{nodeSetPath}' not found.");
    Console.WriteLine("  Make sure PLCcom_Workshop_NodeSet.xml is in the same folder as this executable.");
}
Console.WriteLine();

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║");
Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Try:                                                        ║");
Console.WriteLine("║  * Browse Objects -> Motors -> Motor1 -> Speed, Running      ║");
Console.WriteLine("║  * Browse Objects -> Sensors -> TempSensor1 -> Value, Unit   ║");
Console.WriteLine("║  * Browse Types -> ObjectTypes -> MotorType, SensorType      ║");
Console.WriteLine("║  * Check Server -> NamespaceArray for the imported namespace ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to exit.                                        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();

// =============================================================================
// Helper: CreateConfig
// =============================================================================
static UaServerConfiguration CreateConfig()
{
    return new UaServerConfiguration
    {
        // ── Application Identity ──────────────────────────────────────────────
        ApplicationName  = "PLCcom Workshop 41 - NodeSet Import",
        ApplicationUri   = "urn:localhost:PLCcom:Workshop:41",
        ProductUri       = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
        NamespaceUri     = "http://indi-an.com/opcua/workshop/nodeset-import",

        // ── ServerStatus/BuildInfo ────────────────────────────────────────────
        ManufacturerName = "My Company GmbH",
        ProductName      = "My OPC UA Server",
        SoftwareVersion  = "1.0.0",
        BuildNumber      = "42",
        // ── Endpoints ──────────────────────────────────────────────────────
        BaseAddresses = new List<string>
        {
            "opc.tcp://localhost:48410",
            "opc.https://localhost:48411"
        },

        // ── Security Policies ────────────────────────────────────────────────
        SecurityPolicies = UaServer.GetRecommendedSecurityPolicies(),

        // ── User Authentication ───────────────────────────────────────────────
        UserTokenPolicies = new List<UserTokenPolicy>
        {
            new UserTokenPolicy { TokenType = UserTokenType.Anonymous }
        },

        // ── PKI Certificate Store ─────────────────────────────────────────────
        CertificateStorePath        = @".\pki",
        CertificateLifetimeInMonths = 60,
        AutoAcceptUntrustedCertificates = false,
        // ── Endpoint Host Normalization ───────────────────────────────────────
        // AsConfigured (default) = endpoints use exactly the host from BaseAddresses
        // NormalizeToHostname    = replace localhost/127.0.0.1 with the machine name
        // None                   = no normalization, behavior depends on DNS and network settings
        EndpointHostMode = EndpointHostMode.AsConfigured,
        MaxSessionCount = 100,
        ShutdownDelay   = 5,

        // ── VendorServerInfo ──────────────────────────────────────────────────
        VendorName           = "My Company GmbH",
        VendorProductName    = "My OPC UA Server",
        VendorProductVersion = "1.0.0",

        // ── OperationLimits ───────────────────────────────────────────────────
        MaxNodesPerRead                      = 1000,
        MaxNodesPerWrite                     = 1000,
        MaxNodesPerBrowse                    = 1000,
        MaxNodesPerHistoryReadData           = 100,
        MaxNodesPerHistoryReadEvents         = 100,
        MaxNodesPerHistoryUpdateData         = 100,
        MaxNodesPerHistoryUpdateEvents       = 100,
        MaxNodesPerMethodCall                = 200,
        MaxNodesPerRegisterNodes             = 1000,
        MaxNodesPerTranslateBrowsePathsToNodeIds = 1000,
        MaxNodesPerNodeManagement            = 1000,
        MaxMonitoredItemsPerCall             = 1000,
    };
}

// =============================================================================
// Helper: PrintConfig
// =============================================================================
static void PrintConfig(UaServerConfiguration config)
{
    Console.WriteLine("-- Active Server Configuration ------------------------------");
    Console.WriteLine("  ApplicationName  : " + config.ApplicationName);
    Console.WriteLine("  ApplicationUri   : " + config.ApplicationUri);
    Console.WriteLine("  NamespaceUri     : " + (config.NamespaceUri ?? "(default)"));
    Console.WriteLine("  ManufacturerName : " + (config.ManufacturerName ?? "(not set)"));
    Console.WriteLine("  ProductName      : " + (config.ProductName ?? "(not set)"));
    Console.WriteLine("  SoftwareVersion  : " + (config.SoftwareVersion ?? "(auto-detect)"));
    Console.WriteLine("  BuildNumber      : " + (config.BuildNumber ?? "(auto-detect)"));
    Console.WriteLine();
    Console.WriteLine("  Endpoints:");
    foreach (var addr in config.BaseAddresses) Console.WriteLine("    " + addr);
    Console.WriteLine();
        Console.WriteLine($"  EndpointHostMode : {config.EndpointHostMode}");
    Console.WriteLine("  VendorServerInfo:");
    Console.WriteLine("    VendorName=" + (config.VendorName ?? "(not set)") +
                      "  ProductName=" + (config.VendorProductName ?? "(not set)") +
                      "  Version=" + (config.VendorProductVersion ?? "(not set)"));
    Console.WriteLine();
    Console.WriteLine("  OperationLimits:");
    Console.WriteLine("    Read=" + config.MaxNodesPerRead + "  Write=" + config.MaxNodesPerWrite +
                      "  Browse=" + config.MaxNodesPerBrowse + "  Method=" + config.MaxNodesPerMethodCall);
    Console.WriteLine("    HistRD=" + config.MaxNodesPerHistoryReadData + "  HistRE=" + config.MaxNodesPerHistoryReadEvents +
                      "  HistUD=" + config.MaxNodesPerHistoryUpdateData + "  HistUE=" + config.MaxNodesPerHistoryUpdateEvents);
    Console.WriteLine("    Register=" + config.MaxNodesPerRegisterNodes +
                      "  Translate=" + config.MaxNodesPerTranslateBrowsePathsToNodeIds +
                      "  NodeMgmt=" + config.MaxNodesPerNodeManagement +
                      "  MonItems=" + config.MaxMonitoredItemsPerCall);
    Console.WriteLine("-------------------------------------------------------------");
    Console.WriteLine();
}
