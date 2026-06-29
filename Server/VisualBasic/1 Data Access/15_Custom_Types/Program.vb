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


' ==============================================================================
' PLCcom OPC UA Server SDK - Workshop 15: Custom Types
'
' OPC UA allows servers to define custom structured DataTypes (Structs).
' This workshop demonstrates:
'   Part A - Object Hierarchy (the simple alternative to structs)
'   Part B - Flat Struct (MotorDataType with 3 scalar fields)
'   Part C - Nested Struct (PlantDataType containing MotorDataType)
'   Part D - Struct with Array fields (double[], string[])
'   Part E - Array of Structs (3 motors as MotorDataType[3])
'   Part F - Struct containing an Array-of-Structs field
'   Part G - Struct with a 2D Matrix field
'
' Connect with any OPC UA client to: opc.tcp://localhost:48410
' ==============================================================================

Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Server.Sdk
Imports System
Imports System.Collections.Generic
Imports System.Reflection
Module Program

    Sub Main(args As String())

        Dim LicenseUserName As String = "<Enter your UserName here>"
        Dim LicenseSerial As String = "<Enter your Serial here>"

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 15: Custom Types        ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This example demonstrates:                                  ║")
        Console.WriteLine("║    * Object hierarchy (Objects with child Variables)         ║")
        Console.WriteLine("║    * Flat structs, nested structs, array fields              ║")
        Console.WriteLine("║    * Array of structs, struct with array-of-structs field    ║")
        Console.WriteLine("║    * Struct with 2D matrix field                             ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        ' All server settings are defined in CreateConfig() below.
        Dim config = CreateConfig()
        PrintConfig(config)

        Using server As New UaServer(LicenseUserName, LicenseSerial)

            ' Accept all client certificates automatically.
            ' WARNING: Do Not use this in production! Either implement your own validation
            ' logic here (inspect e.Certificate And e.Error, then set e.Accept = true Or false),
            ' Or remove this handler entirely -- the SDK will then automatically validate
            ' certificates against the PKI trust store (pki/trusted/certs/).
            AddHandler server.CertificateValidation, Sub(s, e) e.Accept = True

            ' WriteValidation — called BEFORE any client write is committed to the address space.
            ' All internal checks (AccessLevel, DataType, Permissions) have already passed.
            ' The handler receives ALL items of the write request as a batch.
            ' Set item.StatusCode to any Bad_* value to reject that specific item.
            '
            ' You can also MODIFY the value before it is written by setting item.Value.
            ' The modified value is then stored in the address space instead of the original.
            '
            ' !! IMPORTANT — PERFORMANCE WARNING !!
            ' This handler runs synchronously on the server's write thread.
            ' Any blocking operation (device I/O, database, slow network) will stall
            ' the entire write request and can block other clients as well.
            '
            ' If you need to forward the value to a device, prefer one of these patterns:
            '   a) Accept immediately (Good) and forward asynchronously via Task.Run or a queue.
            '      The OPC UA client gets a fast response; the device update happens in the background.
            '   b) If you must wait for the device, always use a short timeout (e.g. 500 ms)
            '      and return BadTimeout or BadNoCommunication if the device does not respond in time.
            '
            ' Never await or block indefinitely inside this handler.
            AddHandler server.WriteValidation, Sub(s, e)
                                                   For Each item In e.Items
                                                       ' Example: accept immediately and forward to device asynchronously
                                                       ' Task.Run(Sub() plc.WriteValue(item.Path, item.Value))
                                                       '
                                                       ' Example: forward synchronously with timeout, reject on failure
                                                       ' If Not plc.WriteValue(item.Path, item.Value, timeoutMs:=500) Then item.StatusCode = StatusCodes.BadNoCommunication
                                                       item.StatusCode = StatusCodes.Good
                                                       Console.WriteLine($"  >> WriteValidation: {item.Path} = {item.Value}")
                                                   Next
                                               End Sub

            ' ValuesWritten — called AFTER a successful write. The client already received Good.
            ' Use this for logging, synchronization, or triggering side effects.
            ' Note: If WriteValidation rejects an item, ValuesWritten does NOT fire for that item.
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

            ' =================================================================
            ' Part A: Object Hierarchy
            ' =================================================================
            Console.WriteLine("-- Part A: Object Hierarchy -------------------------------------")

            Dim hierarchy = server.CreateFolder("Hierarchy", UaRolePermissions.WITHOUT_RESTRICTIONS)

            Dim motorTypeId = server.CreateObjectType("MotorType")
            Dim bearingTypeId = server.CreateObjectType("BearingType")
            Dim machineTypeId = server.CreateObjectType("MachineType")

            Dim machine = server.CreateObject(hierarchy, "CNC_Machine_01", UaRolePermissions.WITHOUT_RESTRICTIONS, machineTypeId)

            Dim motor = server.CreateObject(machine.NodeId, "MainMotor", UaRolePermissions.WITHOUT_RESTRICTIONS, motorTypeId)
            server.CreateVariable(Of Double)(motor, "Speed", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=1500.0)
            server.CreateVariable(Of Double)(motor, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=45.0)
            server.CreateVariable(Of Boolean)(motor, "Running", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=True)

            Dim bearing = server.CreateObject(machine.NodeId, "MainBearing", UaRolePermissions.WITHOUT_RESTRICTIONS, bearingTypeId)
            server.CreateVariable(Of Double)(bearing, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=38.0)
            server.CreateVariable(Of Double)(bearing, "Vibration", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=0.5)

            server.CreateVariable(Of String)(machine, "State", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:="Running")
            server.CreateVariable(Of Long)(machine, "CycleCount", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=0L)

            Console.WriteLine($"  {machine.Path}")
            Console.WriteLine($"    MainMotor    (MotorType):   Speed=1500, Temp=45, Running=true")
            Console.WriteLine($"    MainBearing  (BearingType): Temp=38, Vibration=0.5")
            Console.WriteLine()

            ' =================================================================
            ' Part B: Flat Structs
            ' =================================================================
            Console.WriteLine("-- Part B: Flat Structs -----------------------------------------")

            Dim structFolder = server.CreateFolder("StructData", UaRolePermissions.WITHOUT_RESTRICTIONS)

            Dim motorDataTypeId = server.CreateStructDataType("MotorDataType",
                ("Speed", DataTypeIds.Double, Nothing),
                ("Temperature", DataTypeIds.Double, Nothing),
                ("Running", DataTypeIds.Boolean, Nothing))

            Dim machineDataTypeId = server.CreateStructDataType("MachineDataType",
                ("State", DataTypeIds.String, Nothing),
                ("CycleCount", DataTypeIds.Int64, Nothing),
                ("MotorSpeed", DataTypeIds.Double, Nothing))

            Dim motorStruct = server.CreateStructVariable(structFolder, "Motor_Struct", motorDataTypeId, UaRolePermissions.WITHOUT_RESTRICTIONS)
            motorStruct.SetField(Of Double)("Speed", 1500.0)
            motorStruct.SetField(Of Double)("Temperature", 45.0)
            motorStruct.SetField(Of Boolean)("Running", True)

            Dim machineStruct = server.CreateStructVariable(structFolder, "Machine_Struct", machineDataTypeId, UaRolePermissions.WITHOUT_RESTRICTIONS)
            machineStruct.SetField(Of String)("State", "Running")
            machineStruct.SetField(Of Long)("CycleCount", 0L)
            machineStruct.SetField(Of Double)("MotorSpeed", 1500.0)

            Console.WriteLine($"  MotorDataType     {motorDataTypeId}")
            Console.WriteLine($"  MachineDataType   {machineDataTypeId}")
            Console.WriteLine($"  Motor_Struct      {motorStruct.Path}")
            Console.WriteLine($"  Machine_Struct    {machineStruct.Path}")
            Console.WriteLine()

            ' =================================================================
            ' Part C: Nested Struct
            ' =================================================================
            Console.WriteLine("-- Part C: Nested Struct ----------------------------------------")

            Dim plantDataTypeId = server.CreateStructDataType("PlantDataType",
                ("PlantName", DataTypeIds.String, Nothing),
                ("ProductionCount", DataTypeIds.Int32, Nothing),
                ("Motor", motorDataTypeId, Nothing),
                ("Machine", machineDataTypeId, Nothing))

            Dim plantStruct = server.CreateStructVariable(structFolder, "Plant_Struct", plantDataTypeId, UaRolePermissions.WITHOUT_RESTRICTIONS)
            plantStruct.SetField(Of String)("PlantName", "Factory_01")
            plantStruct.SetField(Of Integer)("ProductionCount", 42)
            plantStruct.SetField(Of Double)("Motor.Speed", 2200.0)
            plantStruct.SetField(Of Double)("Motor.Temperature", 55.5)
            plantStruct.SetField(Of Boolean)("Motor.Running", True)
            plantStruct.SetField(Of String)("Machine.State", "Producing")
            plantStruct.SetField(Of Long)("Machine.CycleCount", 12345L)
            plantStruct.SetField(Of Double)("Machine.MotorSpeed", 2200.0)

            Console.WriteLine($"  PlantDataType     {plantDataTypeId}")
            Console.WriteLine($"  Plant_Struct      {plantStruct.Path}")
            Console.WriteLine()

            ' =================================================================
            ' Part D: Struct with Array fields
            ' =================================================================
            Console.WriteLine("-- Part D: Struct with Array fields -----------------------------")

            Dim sensorDataTypeId = server.CreateStructDataType("SensorDataType",
                ("Name", DataTypeIds.String, Nothing),
                ("Readings", DataTypeIds.Double, New UInteger() {4}),
                ("Thresholds", DataTypeIds.Double, New UInteger() {2}))

            Dim sensorStruct = server.CreateStructVariable(structFolder, "Sensor_Struct", sensorDataTypeId, UaRolePermissions.WITHOUT_RESTRICTIONS)
            sensorStruct.SetField(Of String)("Name", "TempSensor_01")
            sensorStruct.SetField(Of Double())("Readings", New Double() {23.5, 24.1, 22.8, 25.0})
            sensorStruct.SetField(Of Double())("Thresholds", New Double() {50.0, 75.0})

            Console.WriteLine($"  SensorDataType    {sensorDataTypeId}")
            Console.WriteLine($"  Sensor_Struct     {sensorStruct.Path}")
            Console.WriteLine()

            ' =================================================================
            ' Part E: Array of Structs
            ' =================================================================
            Console.WriteLine("-- Part E: Array of Structs -------------------------------------")

            Dim motorArray = server.CreateStructArrayVariable(structFolder, "Motor_Array", motorDataTypeId, 3)

            motorArray(0).SetField(Of Double)("Speed", 1000.0)
            motorArray(0).SetField(Of Double)("Temperature", 40.0)
            motorArray(0).SetField(Of Boolean)("Running", True)

            motorArray(1).SetField(Of Double)("Speed", 1500.0)
            motorArray(1).SetField(Of Double)("Temperature", 55.0)
            motorArray(1).SetField(Of Boolean)("Running", True)

            motorArray(2).SetField(Of Double)("Speed", 0.0)
            motorArray(2).SetField(Of Double)("Temperature", 22.0)
            motorArray(2).SetField(Of Boolean)("Running", False)

            Console.WriteLine($"  Motor_Array       {motorArray.Path}")
            Console.WriteLine()

            ' =================================================================
            ' Part F: Struct with Array-of-Structs field
            ' =================================================================
            Console.WriteLine("-- Part F: Struct with Array-of-Structs field -------------------")

            Dim factoryDataTypeId = server.CreateStructDataType("FactoryDataType",
                ("FactoryName", DataTypeIds.String, Nothing),
                ("Motors", motorDataTypeId, New UInteger() {2}))

            Dim factoryStruct = server.CreateStructVariable(structFolder, "Factory_Struct", factoryDataTypeId, UaRolePermissions.WITHOUT_RESTRICTIONS)
            factoryStruct.SetField(Of String)("FactoryName", "MainFactory")
            factoryStruct.SetField(Of Double)("Motors.[0].Speed", 1000.0)
            factoryStruct.SetField(Of Double)("Motors.[0].Temperature", 40.0)
            factoryStruct.SetField(Of Boolean)("Motors.[0].Running", True)
            factoryStruct.SetField(Of Double)("Motors.[1].Speed", 2000.0)
            factoryStruct.SetField(Of Double)("Motors.[1].Temperature", 60.0)
            factoryStruct.SetField(Of Boolean)("Motors.[1].Running", False)

            Console.WriteLine($"  FactoryDataType   {factoryDataTypeId}")
            Console.WriteLine($"  Factory_Struct    {factoryStruct.Path}")
            Console.WriteLine()

            ' =================================================================
            ' Part G: Struct with 2D Matrix field
            ' =================================================================
            Console.WriteLine("-- Part G: Struct with 2D Matrix field --------------------------")

            Dim gridDataTypeId = server.CreateStructDataType("GridDataType",
                ("Label", DataTypeIds.String, Nothing),
                ("Matrix", DataTypeIds.Double, New UInteger() {2, 3}))

            Dim gridStruct = server.CreateStructVariable(structFolder, "Grid_Struct", gridDataTypeId, UaRolePermissions.WITHOUT_RESTRICTIONS)
            gridStruct.SetField(Of String)("Label", "HeatMap_01")
            gridStruct.SetField("Matrix", New Matrix(
                New Double() {1.0, 2.0, 3.0, 4.0, 5.0, 6.0},
                BuiltInType.Double,
                New Integer() {2, 3}))

            Console.WriteLine($"  GridDataType      {gridDataTypeId}")
            Console.WriteLine($"  Grid_Struct       {gridStruct.Path}")
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


    ' ==========================================================================
    ' Helper: CreateConfig
    ' ==========================================================================
    ' Returns the server configuration. Adjust to your needs.
    Private Function CreateConfig() As UaServerConfiguration
        Dim cfg As New UaServerConfiguration
        ' ── Application Identity ──────────────────────────────────────────────
        cfg.ApplicationName = "PLCcom Workshop 15 - Custom Types"
        cfg.ApplicationUri  = "urn:localhost:PLCcom:Workshop:15"
        cfg.ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/"
        cfg.NamespaceUri    = "http://indi-an.com/opcua/workshop/custom-types"

        ' ── ServerStatus/BuildInfo ────────────────────────────────────────────
        cfg.ManufacturerName = "My Company GmbH"
        cfg.ProductName      = "My OPC UA Server"
        cfg.SoftwareVersion  = "1.0.0"
        cfg.BuildNumber      = "42"

        ' ── Endpoints ────────────────────────────────────────────────────────
        cfg.BaseAddresses = New List(Of String) From {"opc.tcp://localhost:48410", "opc.https://localhost:48411"}

        ' ── Security Policies ────────────────────────────────────────────────
        cfg.SecurityPolicies = UaServer.GetRecommendedSecurityPolicies()

        ' ── User Authentication ───────────────────────────────────────────────
        cfg.UserTokenPolicies = New List(Of UserTokenPolicy) From {New UserTokenPolicy With {.TokenType = UserTokenType.Anonymous}}

        ' ── PKI Certificate Store ─────────────────────────────────────────────
        cfg.AutoAcceptUntrustedCertificates = False

        ' ── Endpoint Host Normalization ───────────────────────────────────────
        ' AsConfigured (default) = endpoints use exactly the host from BaseAddresses
        ' NormalizeToHostname    = replace localhost/127.0.0.1 with the machine name
        ' None                   = no normalization, behavior depends on DNS and network settings
        cfg.EndpointHostMode = EndpointHostMode.AsConfigured
        cfg.MaxSessionCount = 100
        cfg.ShutdownDelay = 5

        ' ── VendorServerInfo ──────────────────────────────────────────────────
        cfg.VendorName = "My Company GmbH"
        cfg.VendorProductName = "My OPC UA Server"
        cfg.VendorProductVersion = "1.0.0"

        ' ── OperationLimits ───────────────────────────────────────────────────
        cfg.MaxNodesPerRead = 1000
        cfg.MaxNodesPerWrite = 1000
        cfg.MaxNodesPerBrowse = 1000
        cfg.MaxNodesPerHistoryReadData           = 100
        cfg.MaxNodesPerHistoryReadEvents         = 100
        cfg.MaxNodesPerHistoryUpdateData         = 100
        cfg.MaxNodesPerHistoryUpdateEvents       = 100
        cfg.MaxNodesPerMethodCall                = 200
        cfg.MaxNodesPerRegisterNodes             = 1000
        cfg.MaxNodesPerTranslateBrowsePathsToNodeIds = 1000
        cfg.MaxNodesPerNodeManagement            = 1000
        cfg.MaxMonitoredItemsPerCall             = 1000

        ' -- PKI Certificate Store -----------------------------------------------
        ' UaServerCertificateStore manages all server certificates.
        ' Load() tries to load existing certificates from disk.
        ' GetMissingOrExpired() returns certificates that need to be (re)created.
        ' Build(overwrite:=True) creates a new self-signed certificate on disk.
        ''
        ' One Application certificate is required for the OPC UA secure channel.
        ' One default HTTPS certificate is presented at every opc.https TLS handshake.
        Dim certs As New List(Of UaServerCertificate) From {
            New UaServerCertificate(
                pkiBase:=".\pki",
                password:="secretpassword",
                alias:=Assembly.GetEntryAssembly().GetName().Name,
                applicationUri:=cfg.ApplicationUri,
                validityDays:=720,
                organisation:="Indi.An GmbH",
                role:=UaServerCertificate.CertificateRole.Application)
        }

        ' One default HTTPS certificate for all opc.https ports. The SDK presents it at the
        ' TLS handshake for any opc.https port that has no specifically assigned certificate.
        ' To serve an official domain certificate on a port, create another HTTPS certificate
        ' and assign it: cfg.AssignHttpsCertificateToPort(port, cert).
        Dim httpsDefault As New UaServerCertificate(
            pkiBase:=".\pki",
            password:="secretpassword",
            alias:="https-default",
            applicationUri:="urn:https-default:https",
            validityDays:=720,
            organisation:="Indi.An GmbH",
            role:=UaServerCertificate.CertificateRole.Https)
        certs.Add(httpsDefault)
        cfg.SetDefaultHttpsCertificate(httpsDefault)

        Dim store = UaServerCertificateStore.Load(".\pki", certs)
        For Each missing In store.GetMissingOrExpired()
            missing.Build(overwrite:=True)
        Next

        cfg.SetCertificateStore(store)
                Return cfg
    End Function

    ' ==========================================================================
    ' Helper: PrintConfig
    ' ==========================================================================
' ==========================================================================
    ' Helper: PrintConfig
    ' ==========================================================================
    Private Sub PrintConfig(config As UaServerConfiguration)
        Console.WriteLine("-- Active Server Configuration ------------------------------")
        Console.WriteLine("  ApplicationName  : " & config.ApplicationName)
        Console.WriteLine("  ApplicationUri   : " & config.ApplicationUri)
        Console.WriteLine("  NamespaceUri     : " & If(config.NamespaceUri, "(default: ApplicationUri + /nodes)"))
        Console.WriteLine("  ManufacturerName : " & If(config.ManufacturerName, "(not set)"))
        Console.WriteLine("  ProductName      : " & If(config.ProductName, "(not set)"))
        Console.WriteLine("  SoftwareVersion  : " & If(config.SoftwareVersion, "(auto-detect)"))
        Console.WriteLine("  BuildNumber      : " & If(config.BuildNumber, "(auto-detect)"))
        Console.WriteLine()
        Console.WriteLine("  Endpoints:")
        For Each addr In config.BaseAddresses
            Console.WriteLine("    " & addr)
        Next
        Console.WriteLine()
        Console.WriteLine("  EndpointHostMode : " & config.EndpointHostMode.ToString())
        Console.WriteLine()
        Console.WriteLine("  Certificate Store:")
        If config.CertificateStore IsNot Nothing Then
            Console.WriteLine("    " & config.CertificateStore.ToString())
        Else
            Console.WriteLine("    (not set)")
        End If
        Console.WriteLine()
        Console.WriteLine("  VendorServerInfo (Server/VendorServerInfo):")
        Console.WriteLine("    VendorName           = " & If(config.VendorName, "(not set)"))
        Console.WriteLine("    VendorProductName    = " & If(config.VendorProductName, "(not set)"))
        Console.WriteLine("    VendorProductVersion = " & If(config.VendorProductVersion, "(not set)"))
        Console.WriteLine()
        Console.WriteLine("  OperationLimits (Server/ServerCapabilities/OperationLimits):")
        Console.WriteLine($"    MaxNodesPerRead                          = {config.MaxNodesPerRead}")
        Console.WriteLine($"    MaxNodesPerWrite                         = {config.MaxNodesPerWrite}")
        Console.WriteLine($"    MaxNodesPerBrowse                        = {config.MaxNodesPerBrowse}")
        Console.WriteLine($"    MaxNodesPerHistoryReadData               = {config.MaxNodesPerHistoryReadData}")
        Console.WriteLine($"    MaxNodesPerHistoryReadEvents             = {config.MaxNodesPerHistoryReadEvents}")
        Console.WriteLine($"    MaxNodesPerHistoryUpdateData             = {config.MaxNodesPerHistoryUpdateData}")
        Console.WriteLine($"    MaxNodesPerHistoryUpdateEvents           = {config.MaxNodesPerHistoryUpdateEvents}")
        Console.WriteLine($"    MaxNodesPerMethodCall                    = {config.MaxNodesPerMethodCall}")
        Console.WriteLine($"    MaxNodesPerRegisterNodes                 = {config.MaxNodesPerRegisterNodes}")
        Console.WriteLine($"    MaxNodesPerTranslateBrowsePathsToNodeIds = {config.MaxNodesPerTranslateBrowsePathsToNodeIds}")
        Console.WriteLine($"    MaxNodesPerNodeManagement                = {config.MaxNodesPerNodeManagement}")
        Console.WriteLine($"    MaxMonitoredItemsPerCall                 = {config.MaxMonitoredItemsPerCall}")
        Console.WriteLine("-------------------------------------------------------------")
        Console.WriteLine()
    End Sub

End Module