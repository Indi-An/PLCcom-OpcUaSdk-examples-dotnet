// ==============================================================================
// PLCcom OPC UA Server SDK - Workshop 17: Multiple Namespaces
//
// Every node in an OPC UA address space has a NodeId that consists of:
//   * A NamespaceIndex (number) that identifies the namespace
//   * An Identifier (number, string, or GUID) that is unique within that namespace
//
// The OPC UA namespace table is fixed for the first two entries:
//   ns=0  OPC UA standard types (defined by the OPC Foundation)
//   ns=1  Server-local diagnostics and configuration
//   ns=2+ Application-specific namespaces
//
// Why use multiple namespaces?
//   * Companion specifications (PackML, Euromap, DI) define their own namespace
//   * Vendor extensions can be separated from the base application namespace
//   * Multiple independent subsystems can coexist without NodeId conflicts
//
// What you will learn:
//   * How to register additional namespace URIs
//   * How to create nodes in a specific namespace
//   * How to look up namespace indices by URI
//
// Connect with any OPC UA client to: opc.tcp://localhost:48416
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
Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 17: Namespaces          ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  This example demonstrates:                                  ║");
Console.WriteLine("║  * OPC UA namespace table (ns=0 UA, ns=1 local, ns=2+ app)   ║");
Console.WriteLine("║  * Registering additional namespace URIs                     ║");
Console.WriteLine("║  * Creating nodes in specific namespaces                     ║");
Console.WriteLine("║  * Looking up namespace indices by URI                       ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

var config = new UaServerConfiguration
{
    ApplicationName = "PLCcom Workshop 17 - Namespaces",
    ApplicationUri  = "urn:localhost:PLCcom:Workshop:17",
    ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
    BaseAddresses   = new List<string> { "opc.tcp://localhost:48416" },
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

// NodeManager gives access to advanced address space operations
var mgr = server.NodeManager;

// -- Namespace table overview --------------------------------------------------
Console.WriteLine("  OPC UA Namespace Table:");
Console.WriteLine($"    ns=0  OPC UA standard types (fixed)");
Console.WriteLine($"    ns=1  Server diagnostics (fixed)");
Console.WriteLine($"    ns={mgr.NamespaceIndex}  Server application namespace (default)");
Console.WriteLine();

// -- Register additional namespaces --------------------------------------------
// AddNamespace() registers the URI in the server's namespace table and
// returns the assigned index. The index is dynamic - it depends on the
// order of registration and other namespaces already present.
// Always use the URI (not the index) to identify a namespace reliably.
ushort nsCompany = server.AddNamespace("urn:mycompany:myproduct");
ushort nsSite    = server.AddNamespace("urn:mycompany:site:berlin");

Console.WriteLine($"  Registered: urn:mycompany:myproduct  -> ns={nsCompany}");
Console.WriteLine($"  Registered: urn:mycompany:site:berlin -> ns={nsSite}");
Console.WriteLine();

// -- Default namespace: nodes in the application namespace (ns=2) --------------
// CreateFolder and CreateVariable use the default namespace automatically
var plant = server.CreateFolder("Plant");
var temp  = server.CreateVariable<double>(plant, "Temperature", initialValue: 22.0);
var rpm   = server.CreateVariable<int>(plant, "RPM", initialValue: 1500);

// -- Custom namespaces: nodes with NodeId and BrowseName in a specific namespace
// Pass the namespace index to CreateFolder/CreateVariable to place nodes there.
// Both the NodeId and BrowseName will use the specified namespace.
var companyFolder = mgr.CreateFolder(ObjectIds.ObjectsFolder, "MyProduct", nsCompany);
var siteFolder    = mgr.CreateFolder(ObjectIds.ObjectsFolder, "BerlinSite", nsSite);

// Variables under namespace folders also use the namespace index
var version  = server.CreateVariable<string>(companyFolder.NodeId, "Version", nsCompany, initialValue: "2.1.0", readOnly: true);
var serialNr = server.CreateVariable<string>(companyFolder.NodeId, "SerialNumber", nsCompany, initialValue: "SN-2025-0042", readOnly: true);

var hallTemp = server.CreateVariable<double>(siteFolder.NodeId, "HallTemperature", nsSite, initialValue: 19.5);
var machines = server.CreateVariable<int>(siteFolder.NodeId, "MachineCount", nsSite, initialValue: 12, readOnly: true);

Console.WriteLine("  Address space:");
Console.WriteLine($"    Plant/                          (ns={mgr.NamespaceIndex} - default namespace)");
Console.WriteLine($"      Temperature = 22.0");
Console.WriteLine($"      RPM = 1500");
Console.WriteLine($"    MyProduct/                      (ns={nsCompany} - company namespace)");
Console.WriteLine($"      Version = 2.1.0");
Console.WriteLine($"      SerialNumber = SN-2025-0042");
Console.WriteLine($"    BerlinSite/                     (ns={nsSite} - site namespace)");
Console.WriteLine($"      HallTemperature = 19.5");
Console.WriteLine($"      MachineCount = 12");
Console.WriteLine();

// -- Look up namespace index by URI -------------------------------------------
// Use GetNamespaceIndex() to resolve a URI to its current index.
// This is the safe way to work with namespaces - never hardcode the index.
ushort lookup = server.GetNamespaceIndex("urn:mycompany:myproduct");
Console.WriteLine($"  Lookup 'urn:mycompany:myproduct'  -> ns={lookup}");

ushort lookup2 = server.GetNamespaceIndex("urn:mycompany:site:berlin");
Console.WriteLine($"  Lookup 'urn:mycompany:site:berlin' -> ns={lookup2}");

ushort notFound = server.GetNamespaceIndex("urn:does:not:exist");
Console.WriteLine($"  Lookup 'urn:does:not:exist'       -> {(notFound == ushort.MaxValue ? "NOT FOUND" : "ns=" + notFound)}");
Console.WriteLine();

Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║");
Console.WriteLine("║  opc.tcp://localhost:48416                                   ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Try:                                                        ║");
Console.WriteLine("║  * Browse Objects - you see Plant, MyProduct and BerlinSite  ║");
Console.WriteLine("║  * Click MyProduct and check its NodeId namespace index      ║");
Console.WriteLine("║  * Compare the namespace index of Plant vs MyProduct nodes   ║");
Console.WriteLine("║  * Check the NamespaceArray attribute on the Server node     ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Press ENTER to exit.                                        ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.ReadLine();
