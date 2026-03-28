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
// Connect with any OPC UA client to: opc.tcp://localhost:48440
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

var config = new UaServerConfiguration
{
    ApplicationName = "PLCcom Workshop 41 - NodeSet Import",
    ApplicationUri  = "urn:localhost:PLCcom:Workshop:41",
    ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
    BaseAddresses   = new List<string> { "opc.tcp://localhost:48440" },
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
// PLCcom_Workshop_NodeSet.xml is included with this workshop.
// It defines:
//   MotorType  - Speed (Double), Running (Boolean), SerialNumber (String)
//   SensorType - Value (Double), Unit (String), InAlarm (Boolean)
// Plus two instances of each type under Motors/ and Sensors/.
string nodeSetPath = "PLCcom_Workshop_NodeSet.xml";

if (File.Exists(nodeSetPath))
{
    Console.WriteLine($"  Importing: {nodeSetPath}");
    int count = server.ImportNodeSet(nodeSetPath);
    Console.WriteLine($"  Imported {count} nodes successfully");
    Console.WriteLine();
    Console.WriteLine("  Nodes imported:");
    Console.WriteLine("    Types    -> Types/ObjectTypes/MotorType");
    Console.WriteLine("    Types    -> Types/ObjectTypes/SensorType");
    Console.WriteLine("    Instance -> Objects/Motors/Motor1, Motor2");
    Console.WriteLine("    Instance -> Objects/Sensors/TempSensor1, PressureSensor1");
}
else
{
    Console.WriteLine($"  ERROR: '{nodeSetPath}' not found.");
    Console.WriteLine("  Make sure PLCcom_Workshop_NodeSet.xml is in the same folder as this executable.");
}
Console.WriteLine();

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║");
Console.WriteLine("║  opc.tcp://localhost:48440                                   ║");
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
