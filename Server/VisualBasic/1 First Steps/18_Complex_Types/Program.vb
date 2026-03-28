Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Server.Sdk
Imports System
Imports System.Collections.Generic

' ==============================================================================
' PLCcom OPC UA Server SDK - Workshop 18: Complex Types
'
' Real-world industrial equipment is hierarchical:
'   A machine contains motors, bearings, sensors and actuators.
'   Each component has its own variables and state.
'
' OPC UA models this with nested Objects:
'   * Objects represent physical or logical components
'   * Each Object has a TypeDefinition that describes its structure
'   * Objects can contain child Variables and other child Objects
'
' This workshop models a CNC machine with:
'   CNC_Machine_01 (MachineType)
'     MainMotor (MotorType)   -> Speed, Temperature, Running
'     MainBearing (BearingType) -> Temperature, Vibration
'     State, CycleCount
'
' What you will learn:
'   * How to define a type hierarchy (MachineType, MotorType, BearingType)
'   * How to create nested object instances
'   * How to add variables to objects at any nesting level
'
' Connect with any OPC UA client to: opc.tcp://localhost:48417
' ==============================================================================

Module Program

    Sub Main(args As String())

        ' -- License -----------------------------------------------------------
        ' TODO: Submit your license information from your license e-mail
        Dim LicenseUserName As String = "<Enter your UserName here>"
        Dim LicenseSerial As String = "<Enter your Serial here>"

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 18: Complex Types       ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This example demonstrates:                                  ║")
        Console.WriteLine("║  * Custom ObjectTypes with structured children               ║")
        Console.WriteLine("║  * Nested object hierarchies (Machine -> Motor -> Bearing)   ║")
        Console.WriteLine("║  * Modeling real-world objects as OPC UA nodes               ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Dim config As New UaServerConfiguration With {
            .ApplicationName = "PLCcom Workshop 18 - Complex Types",
            .ApplicationUri = "urn:localhost:PLCcom:Workshop:18",
            .ProductUri = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
            .BaseAddresses = New List(Of String) From {"opc.tcp://localhost:48417"},
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

            ' -- Step 1: Define the type hierarchy -----------------------------
            ' Types are registered once and can be reused for multiple instances.
            ' They appear under Types -> ObjectTypes in the address space.
            Dim motorTypeId = server.CreateObjectType("MotorType")
            Dim bearingTypeId = server.CreateObjectType("BearingType")
            Dim machineTypeId = server.CreateObjectType("MachineType")

            Console.WriteLine("  Type Definitions:")
            Console.WriteLine($"    MotorType   -> {motorTypeId}")
            Console.WriteLine($"    BearingType -> {bearingTypeId}")
            Console.WriteLine($"    MachineType -> {machineTypeId}")
            Console.WriteLine()

            ' -- Step 2: Create the machine instance ---------------------------
            Dim plant = server.CreateFolder("Plant")
            Dim machine = server.CreateObject(plant, "CNC_Machine_01", typeDefinitionId:=machineTypeId)

            ' -- Step 3: Add nested components to the machine ------------------
            ' Objects can contain other Objects - this creates the component hierarchy.
            Dim motor = server.CreateObject(machine.NodeId, "MainMotor", typeDefinitionId:=motorTypeId)
            server.CreateVariable(Of Double)(motor.NodeId, "Speed", initialValue:=1500.0)
            server.CreateVariable(Of Double)(motor.NodeId, "Temperature", initialValue:=45.0)
            server.CreateVariable(Of Boolean)(motor.NodeId, "Running", initialValue:=True)

            Dim bearing = server.CreateObject(machine.NodeId, "MainBearing", typeDefinitionId:=bearingTypeId)
            server.CreateVariable(Of Double)(bearing.NodeId, "Temperature", initialValue:=38.0)
            server.CreateVariable(Of Double)(bearing.NodeId, "Vibration", initialValue:=0.5)

            ' Add top-level variables directly to the machine
            server.CreateVariable(Of String)(machine.NodeId, "State", initialValue:="Running")
            server.CreateVariable(Of Long)(machine.NodeId, "CycleCount", initialValue:=0L)

            Console.WriteLine("  Instance hierarchy:")
            Console.WriteLine("  Plant/")
            Console.WriteLine("    CNC_Machine_01 (MachineType)")
            Console.WriteLine("      State = Running")
            Console.WriteLine("      CycleCount = 0")
            Console.WriteLine("      MainMotor (MotorType)")
            Console.WriteLine("        Speed = 1500.0")
            Console.WriteLine("        Temperature = 45.0")
            Console.WriteLine("        Running = true")
            Console.WriteLine("      MainBearing (BearingType)")
            Console.WriteLine("        Temperature = 38.0")
            Console.WriteLine("        Vibration = 0.5")
            Console.WriteLine()

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║")
            Console.WriteLine("║  opc.tcp://localhost:48417                                   ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Try:                                                        ║")
            Console.WriteLine("║  * Browse Plant -> CNC_Machine_01 -> MainMotor               ║")
            Console.WriteLine("║  * Check the TypeDefinition attribute of CNC_Machine_01      ║")
            Console.WriteLine("║  * Check the TypeDefinition attribute of MainMotor           ║")
            Console.WriteLine("║  * Browse Types -> ObjectTypes to see the type hierarchy     ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Press ENTER to exit.                                        ║")
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
            Console.ReadLine()

        End Using

    End Sub

End Module
