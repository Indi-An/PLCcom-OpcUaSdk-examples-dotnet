' MIT License
' Copyright (c) Indi.An GmbH
'
' Permission is hereby granted, free of charge, to any person obtaining a copy
' of this software and associated documentation files (the "Software"), to deal
' in the Software without restriction, including without limitation the rights
' to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
' copies of the Software, and to permit persons to whom the Software is
' furnished to do so, subject to the following conditions:
'
' The above copyright notice and this permission notice shall be included in all
' copies or substantial portions of the Software.
'
' THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
' IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
' FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
' AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
' LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
' OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
' SOFTWARE.

Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Server.Sdk
Imports System
Imports System.Collections.Generic

' ==============================================================================
' PLCcom OPC UA Server SDK - Workshop 16: Multiple Namespaces
'
' This workshop demonstrates:
'   * Registering additional namespaces with AddNamespace
'   * Creating nodes in specific namespaces using the ns parameter
'   * Sharing ObjectTypes across namespaces
'   * Two plants with identical structure but separate nodes
'
' Connect with any OPC UA client to: opc.tcp://localhost:48410
' ==============================================================================

Module Program

    Sub Main(args As String())

        Dim LicenseUserName As String = "<Enter your UserName here>"
        Dim LicenseSerial As String = "<Enter your Serial here>"

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 16: Multiple Namespaces ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This example demonstrates:                                  ║")
        Console.WriteLine("║    * Registering additional namespaces                       ║")
        Console.WriteLine("║    * Creating nodes in specific namespaces                   ║")
        Console.WriteLine("║    * Sharing ObjectTypes across namespaces                   ║")
        Console.WriteLine("║    * Two plants with identical structure but separate nodes  ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Dim config As New UaServerConfiguration With {
            .ApplicationName = "PLCcom Workshop 16 - Multiple Namespaces",
            .ApplicationUri = "urn:localhost:PLCcom:Workshop:16",
            .ProductUri = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
            .BaseAddresses = New List(Of String) From {
                "opc.tcp://localhost:48410",
                "opc.https://localhost:48411"
            },
            .SecurityPolicies = UaServer.GetRecommendedSecurityPolicies(),
            .UserTokenPolicies = New List(Of UserTokenPolicy) From {
                New UserTokenPolicy With {.TokenType = UserTokenType.Anonymous}
            },
            .ManufacturerName = "My Company GmbH",
            .ProductName = "My OPC UA Server",
            .SoftwareVersion = "1.0.0",
            .BuildNumber = "42",
            .NamespaceUri = "http://indi-an.com/opcua/workshop/multiple-namespaces",
            .CertificateStorePath = ".\pki"
        }

        Using server As New UaServer(LicenseUserName, LicenseSerial)
            AddHandler server.CertificateValidation, Sub(s, e) e.Accept = True

            AddHandler server.ValuesWritten, Sub(s, e)
                                                 For Each item In e.Items
                                                     Console.WriteLine($"  << OPC Write: {item.Path} ({item.NodeId}) = {item.Value}")
                                                 Next
                                             End Sub

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

            ' Show namespace table
            Dim nsTable = server.NodeManager.Server.NamespaceUris
            Console.WriteLine("-- Namespace table after Start() --------------------------------")
            For i As Integer = 0 To nsTable.Count - 1
                Console.WriteLine($"  ns={i}  {nsTable.GetString(CUInt(i))}")
            Next
            Console.WriteLine()

            ' Default namespace nodes
            Dim defaultFolder = server.CreateFolder("DefaultNS")
            Dim testValue1 = server.CreateVariable(Of Double)(defaultFolder, "TestValue1", initialValue:=42.0)
            Dim testValue2 = server.CreateVariable(Of String)(defaultFolder, "TestValue2", initialValue:="hello")
            Console.WriteLine("-- Default namespace nodes ----------------------------------------")
            Console.WriteLine($"  {defaultFolder.Path,-40} NodeId={defaultFolder.NodeId}  BrowseName={defaultFolder.BrowseName}")
            Console.WriteLine($"  {testValue1.Path,-40} NodeId={testValue1.NodeId}  BrowseName={testValue1.BrowseName}")
            Console.WriteLine($"  {testValue2.Path,-40} NodeId={testValue2.NodeId}  BrowseName={testValue2.BrowseName}")
            Console.WriteLine()

            ' =================================================================
            ' Register additional namespaces
            ' =================================================================
            Console.WriteLine("-- Registering namespaces ---------------------------------------")

            Dim nsCompany As UShort = server.AddNamespace("urn:mycompany:types")
            Dim nsPlantA As UShort = server.AddNamespace("urn:mycompany:plant-a")
            Dim nsPlantB As UShort = server.AddNamespace("urn:mycompany:plant-b")

            Console.WriteLine($"  ns={nsCompany}  urn:mycompany:types     (company-wide types)")
            Console.WriteLine($"  ns={nsPlantA}  urn:mycompany:plant-a   (Plant A instances)")
            Console.WriteLine($"  ns={nsPlantB}  urn:mycompany:plant-b   (Plant B instances)")
            Console.WriteLine()

            Dim check As UShort = server.GetNamespaceIndex("urn:mycompany:plant-a")
            Console.WriteLine($"  GetNamespaceIndex(""urn:mycompany:plant-a"") = {check}")
            Console.WriteLine()

            ' =================================================================
            ' Company-wide ObjectTypes
            ' =================================================================
            Console.WriteLine($"-- Company-wide ObjectTypes (ns={nsCompany}) ----------------------------")

            Dim reactorTypeId = server.CreateObjectType("ReactorType", ns:=nsCompany)
            Dim mixerTypeId = server.CreateObjectType("MixerType", ns:=nsCompany)

            Console.WriteLine($"  ReactorType  {reactorTypeId}")
            Console.WriteLine($"  MixerType    {mixerTypeId}")
            Console.WriteLine()

            ' =================================================================
            ' Plant A
            ' =================================================================
            Console.WriteLine($"-- Plant A (ns={nsPlantA}) ---------------------------------------------")

            Dim plantA = server.CreateFolder("PlantA", ns:=nsPlantA)

            Dim reactorA = server.CreateObject(plantA, "Reactor", typeDefinitionId:=reactorTypeId)
            Dim tempA = server.CreateVariable(Of Double)(reactorA, "Temperature", initialValue:=85.0)
            Dim pressA = server.CreateVariable(Of Double)(reactorA, "Pressure", initialValue:=2.5)

            Dim mixerA = server.CreateObject(plantA, "Mixer", typeDefinitionId:=mixerTypeId)
            Dim speedA = server.CreateVariable(Of Double)(mixerA, "Speed", initialValue:=120.0)

            Console.WriteLine($"  {plantA.Path,-40} NodeId={plantA.NodeId}  BrowseName={plantA.BrowseName}")
            Console.WriteLine($"  {tempA.Path,-40} NodeId={tempA.NodeId}  BrowseName={tempA.BrowseName}")
            Console.WriteLine($"  {pressA.Path,-40} NodeId={pressA.NodeId}  BrowseName={pressA.BrowseName}")
            Console.WriteLine($"  {speedA.Path,-40} NodeId={speedA.NodeId}  BrowseName={speedA.BrowseName}")
            Console.WriteLine()

            ' =================================================================
            ' Plant B
            ' =================================================================
            Console.WriteLine($"-- Plant B (ns={nsPlantB}) ---------------------------------------------")

            Dim plantB = server.CreateFolder("PlantB", ns:=nsPlantB)

            Dim reactorB = server.CreateObject(plantB, "Reactor", typeDefinitionId:=reactorTypeId)
            Dim tempB = server.CreateVariable(Of Double)(reactorB, "Temperature", initialValue:=92.0)
            Dim pressB = server.CreateVariable(Of Double)(reactorB, "Pressure", initialValue:=3.1)

            Dim mixerB = server.CreateObject(plantB, "Mixer", typeDefinitionId:=mixerTypeId)
            Dim speedB = server.CreateVariable(Of Double)(mixerB, "Speed", initialValue:=80.0)

            Console.WriteLine($"  {plantB.Path,-40} NodeId={plantB.NodeId}  BrowseName={plantB.BrowseName}")
            Console.WriteLine($"  {tempB.Path,-40} NodeId={tempB.NodeId}  BrowseName={tempB.BrowseName}")
            Console.WriteLine($"  {pressB.Path,-40} NodeId={pressB.NodeId}  BrowseName={pressB.BrowseName}")
            Console.WriteLine($"  {speedB.Path,-40} NodeId={speedB.NodeId}  BrowseName={speedB.BrowseName}")
            Console.WriteLine()

            ' =================================================================
            ' Cross-namespace GetValue
            ' =================================================================
            Console.WriteLine("-- Cross-namespace GetValue -------------------------------------")

            Dim tA As Double = server.GetValue(Of Double)("Objects.PlantA.Reactor.Temperature")
            Dim tB As Double = server.GetValue(Of Double)("Objects.PlantB.Reactor.Temperature")
            Console.WriteLine($"  PlantA Reactor Temperature = {tA}")
            Console.WriteLine($"  PlantB Reactor Temperature = {tB}")
            Console.WriteLine()

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║")
            Console.WriteLine("║  opc.tcp://localhost:48410                                   ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Press ENTER to exit.                                        ║")
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
            Console.ReadLine()

        End Using

    End Sub

End Module
