Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Server.Sdk
Imports System
Imports System.Collections.Generic

' ==============================================================================
' PLCcom OPC UA Server SDK - Workshop 14: Custom Types
'
' OPC UA has a rich type system. You can define your own ObjectTypes and
' VariableTypes that appear in the server's type hierarchy under:
'   Types -> ObjectTypes -> BaseObjectType -> YourType
'   Types -> VariableTypes -> BaseDataVariableType -> YourType
'
' What you will learn:
'   * How to define a custom ObjectType (SensorType)
'   * How to define a custom VariableType (MeasuredValueType)
'   * How to create typed instances from custom ObjectTypes
'
' Connect with any OPC UA client to:
'   opc.tcp://localhost:48413
' ==============================================================================

Module Program

    Sub Main(args As String())

        ' -- License -----------------------------------------------------------
        ' TODO: Submit your license information from your license e-mail
        Dim LicenseUserName As String = "<Enter your UserName here>"
        Dim LicenseSerial As String = "<Enter your Serial here>"

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 14: Custom Types        ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This example demonstrates:                                  ║")
        Console.WriteLine("║  * Defining a custom ObjectType (SensorType)                 ║")
        Console.WriteLine("║  * Defining a custom VariableType (MeasuredValueType)        ║")
        Console.WriteLine("║  * Creating typed instances with child variables             ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Dim config As New UaServerConfiguration With {
            .ApplicationName = "PLCcom Workshop 14 - Custom Types",
            .ApplicationUri = "urn:localhost:PLCcom:Workshop:14",
            .ProductUri = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
            .BaseAddresses = New List(Of String) From {"opc.tcp://localhost:48413"},
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

            ' -- Step 1: Define custom types -----------------------------------
            ' Types are registered in the server's type hierarchy.
            ' They appear under Types -> ObjectTypes / VariableTypes.
            ' The returned NodeId is used when creating instances of this type.

            ' ObjectType: defines the structure of an object (like a class in OOP)
            Dim sensorTypeId = server.CreateObjectType("SensorType")
            Console.WriteLine($"  Created ObjectType: SensorType -> {sensorTypeId}")

            ' VariableType: defines the data type and structure of a variable
            Dim measuredTypeId = server.CreateVariableType("MeasuredValueType", DataTypeIds.Double)
            Console.WriteLine($"  Created VariableType: MeasuredValueType -> {measuredTypeId}")
            Console.WriteLine()

            ' -- Step 2: Create typed instances --------------------------------
            ' When you pass typeDefinitionId, the object's TypeDefinition attribute
            ' is set to that type. Clients can use this to identify the object's role.
            Dim plant = server.CreateFolder("Plant")
            Dim sensors = server.CreateFolder(plant, "Sensors")

            Dim sensor1 = server.CreateObject(sensors, "TemperatureSensor_01",
                typeDefinitionId:=sensorTypeId)
            Dim sensor2 = server.CreateObject(sensors, "PressureSensor_01",
                typeDefinitionId:=sensorTypeId)

            ' Add child variables to each sensor instance
            server.CreateVariable(Of Double)(sensor1.NodeId, "Value", initialValue:=22.3)
            server.CreateVariable(Of String)(sensor1.NodeId, "Unit", initialValue:="C", readOnly:=True)
            server.CreateVariable(Of Boolean)(sensor1.NodeId, "AlarmActive", initialValue:=False)

            server.CreateVariable(Of Double)(sensor2.NodeId, "Value", initialValue:=1.02)
            server.CreateVariable(Of String)(sensor2.NodeId, "Unit", initialValue:="bar", readOnly:=True)

            Console.WriteLine("  Instances:")
            Console.WriteLine("  Sensors/")
            Console.WriteLine("    TemperatureSensor_01 (TypeDef: SensorType)")
            Console.WriteLine("      Value = 22.3")
            Console.WriteLine("      Unit = C [ReadOnly]")
            Console.WriteLine("      AlarmActive = false")
            Console.WriteLine("    PressureSensor_01 (TypeDef: SensorType)")
            Console.WriteLine("      Value = 1.02")
            Console.WriteLine("      Unit = bar [ReadOnly]")
            Console.WriteLine()

            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running. Connect with any OPC UA client to:        ║")
            Console.WriteLine("║  opc.tcp://localhost:48413                                    ║")
            Console.WriteLine("║                                                               ║")
            Console.WriteLine("║  Try:                                                         ║")
            Console.WriteLine("║  * Browse Types -> ObjectTypes -> BaseObjectType -> SensorType║")
            Console.WriteLine("║  * Click a sensor and check its TypeDefinition attribute      ║")
            Console.WriteLine("║  * Both sensors share the same SensorType definition          ║")
            Console.WriteLine("║                                                               ║")
            Console.WriteLine("║  Press ENTER to exit.                                         ║")
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝")
            Console.ReadLine()

        End Using

    End Sub

End Module
