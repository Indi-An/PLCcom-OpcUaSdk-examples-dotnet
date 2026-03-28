Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Server.Sdk
Imports System
Imports System.Collections.Generic

' ==============================================================================
' PLCcom OPC UA Server SDK - Workshop 17: Multiple Namespaces
'
' Every node in an OPC UA address space has a NodeId that consists of:
'   * A NamespaceIndex (number) that identifies the namespace
'   * An Identifier (number, string, or GUID) unique within that namespace
'
' The OPC UA namespace table is fixed for the first two entries:
'   ns=0  OPC UA standard types (defined by the OPC Foundation)
'   ns=1  Server-local diagnostics and configuration
'   ns=2+ Application-specific namespaces
'
' What you will learn:
'   * How to register additional namespace URIs
'   * How to create nodes in a specific namespace
'   * How to look up namespace indices by URI
'
' Connect with any OPC UA client to: opc.tcp://localhost:48416
' ==============================================================================

Module Program

    Sub Main(args As String())

        ' -- License -----------------------------------------------------------
        ' TODO: Submit your license information from your license e-mail
        Dim LicenseUserName As String = "<Enter your UserName here>"
        Dim LicenseSerial As String = "<Enter your Serial here>"

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 17: Namespaces          ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This example demonstrates:                                  ║")
        Console.WriteLine("║  * OPC UA namespace table (ns=0 UA, ns=1 local, ns=2+ app)   ║")
        Console.WriteLine("║  * Registering additional namespace URIs                     ║")
        Console.WriteLine("║  * Creating nodes in specific namespaces                     ║")
        Console.WriteLine("║  * Looking up namespace indices by URI                       ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Dim config As New UaServerConfiguration With {
            .ApplicationName = "PLCcom Workshop 17 - Namespaces",
            .ApplicationUri = "urn:localhost:PLCcom:Workshop:17",
            .ProductUri = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
            .BaseAddresses = New List(Of String) From {"opc.tcp://localhost:48416"},
            .SecurityPolicies = UaServer.GetRecommendedSecurityPolicies(),
            .UserTokenPolicies = New List(Of UserTokenPolicy) From {
                New UserTokenPolicy With {.TokenType = UserTokenType.Anonymous}
            },
            .CertificateStorePath = ".\pki"
        }

        Using server As New UaServer(LicenseUserName, LicenseSerial)
            AddHandler server.CertificateValidation, Sub(s, e) e.Accept = True

            Console.Write("Starting server ... ")
            Try
                server.Start(config)
            Catch ex As Exception
                Console.WriteLine("FAILED")
                Console.WriteLine(ex.Message)
                Console.ReadLine()
                Return
            End Try
            Console.WriteLine("OK")
            Console.WriteLine()

            Dim mgr = server.NodeManager

            Console.WriteLine("  OPC UA Namespace Table:")
            Console.WriteLine("    ns=0  OPC UA standard types (fixed)")
            Console.WriteLine("    ns=1  Server diagnostics (fixed)")
            Console.WriteLine($"    ns={mgr.NamespaceIndex}  Server application namespace (default)")
            Console.WriteLine()

            ' -- Register additional namespaces --------------------------------
            ' AddNamespace() registers the URI in the server's namespace table and
            ' returns the assigned index. Always use the URI (not the index) to
            ' identify a namespace reliably.
            Dim nsCompany As UShort = server.AddNamespace("urn:mycompany:myproduct")
            Dim nsSite As UShort = server.AddNamespace("urn:mycompany:site:berlin")

            Console.WriteLine($"  Registered: urn:mycompany:myproduct  -> ns={nsCompany}")
            Console.WriteLine($"  Registered: urn:mycompany:site:berlin -> ns={nsSite}")
            Console.WriteLine()

            ' -- Default namespace: nodes in the application namespace ---------
            Dim plant = server.CreateFolder("Plant")
            server.CreateVariable(Of Double)(plant, "Temperature", initialValue:=22.0)
            server.CreateVariable(Of Integer)(plant, "RPM", initialValue:=1500)

            ' -- Custom namespaces: nodes with NodeId and BrowseName in a specific namespace
            Dim companyFolder = mgr.CreateFolder(ObjectIds.ObjectsFolder, "MyProduct", nsCompany)
            Dim siteFolder = mgr.CreateFolder(ObjectIds.ObjectsFolder, "BerlinSite", nsSite)

            server.CreateVariable(Of String)(companyFolder.NodeId, "Version", nsCompany, initialValue:="2.1.0", readOnly:=True)
            server.CreateVariable(Of String)(companyFolder.NodeId, "SerialNumber", nsCompany, initialValue:="SN-2025-0042", readOnly:=True)
            server.CreateVariable(Of Double)(siteFolder.NodeId, "HallTemperature", nsSite, initialValue:=19.5)
            server.CreateVariable(Of Integer)(siteFolder.NodeId, "MachineCount", nsSite, initialValue:=12, readOnly:=True)

            Console.WriteLine("  Address space:")
            Console.WriteLine($"    Plant/                          (ns={mgr.NamespaceIndex} - default namespace)")
            Console.WriteLine($"    MyProduct/                      (ns={nsCompany} - company namespace)")
            Console.WriteLine($"    BerlinSite/                     (ns={nsSite} - site namespace)")
            Console.WriteLine()

            ' -- Look up namespace index by URI --------------------------------
            ' Use GetNamespaceIndex() to resolve a URI to its current index.
            ' This is the safe way to work with namespaces - never hardcode the index.
            Dim lookup As UShort = server.GetNamespaceIndex("urn:mycompany:myproduct")
            Console.WriteLine($"  Lookup 'urn:mycompany:myproduct'  -> ns={lookup}")

            Dim lookup2 As UShort = server.GetNamespaceIndex("urn:mycompany:site:berlin")
            Console.WriteLine($"  Lookup 'urn:mycompany:site:berlin' -> ns={lookup2}")

            Dim notFound As UShort = server.GetNamespaceIndex("urn:does:not:exist")
            Console.WriteLine($"  Lookup 'urn:does:not:exist'       -> {If(notFound = UShort.MaxValue, "NOT FOUND", "ns=" & notFound)}")
            Console.WriteLine()

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║")
            Console.WriteLine("║  opc.tcp://localhost:48416                                   ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Try:                                                        ║")
            Console.WriteLine("║  * Browse Objects - you see Plant, MyProduct and BerlinSite  ║")
            Console.WriteLine("║  * Click MyProduct and check its NodeId namespace index      ║")
            Console.WriteLine("║  * Compare the namespace index of Plant vs MyProduct nodes   ║")
            Console.WriteLine("║  * Check the NamespaceArray attribute on the Server node     ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Press ENTER to exit.                                        ║")
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
            Console.ReadLine()

        End Using

    End Sub

End Module
