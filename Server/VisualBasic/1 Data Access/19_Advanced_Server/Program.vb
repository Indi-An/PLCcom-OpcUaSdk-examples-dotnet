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
Imports PLCcom.Opc.Ua.Server
Imports PLCcom.Opc.Ua.Server.Sdk
Imports System
Imports System.Collections.Generic
Imports System.Threading

' ==============================================================================
' PLCcom OPC UA Server SDK - Workshop 19: Advanced Server
'
' A realistic OPC UA server that combines every Data Access feature
' demonstrated in Workshops 11-17 into a single, production-grade application.
'
' This server models a small factory with two CNC machines. It demonstrates
' how all the individual features work together in a real-world scenario.
'
' Connect with any OPC UA client to: opc.tcp://localhost:48410
' ==============================================================================

Module Program

    Private Function CreateMachine(server As UaServer, parent As UaFolder,
                                   name As String, serial As String,
                                   machineTypeId As NodeId, motorTypeId As NodeId,
                                   initialSpeed As Double, initialTemp As Double) As UaVariable(Of Double)()

        Dim machine = server.CreateObject(parent, name, UaRolePermissions.WITHOUT_RESTRICTIONS, machineTypeId)

        Dim motor = server.CreateObject(machine.NodeId, "MainMotor", UaRolePermissions.WITHOUT_RESTRICTIONS, motorTypeId)

        Dim speed = server.CreateVariable(Of Double)(motor, "Speed", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=initialSpeed, readOnly:=True)
        speed.SetEURange(0, 6000)
        speed.SetEngineeringUnits("rpm", "Revolutions per minute")

        Dim temp = server.CreateVariable(Of Double)(motor, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=initialTemp, readOnly:=True)
        temp.SetEURange(0, 150)
        temp.SetEngineeringUnits("degC", "Degrees Celsius")

        Dim running = server.CreateVariable(Of Boolean)(motor, "Running", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=True, readOnly:=True)

        Dim state = server.CreateVariable(Of String)(machine, "State", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:="Running", readOnly:=True)
        Dim cycles = server.CreateVariable(Of Long)(machine, "CycleCount", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=0L, readOnly:=True)
        server.CreateVariable(Of String)(machine, "SerialNumber", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=serial, readOnly:=True)

        Dim setpoints = server.CreateArrayVariable(Of Double)(machine.NodeId, "Setpoints",
            initialValue:=New Double() {100.0, 200.0, 300.0, 400.0},
            exposeElements:=True)

        Dim capturedName = name
        Dim capturedState = state
        Dim capturedRunning = running
        server.CreateMethod(machine.NodeId, "Reset",
            handler:=Function(ctx, method, objectId, inputArgs, outputArgs)
                         server.SetValue($"Objects.Factory.{capturedName}.CycleCount", 0L)
                         capturedState.Value = "Idle"
                         capturedRunning.Value = False
                         Console.WriteLine($"  !! {capturedName} RESET by client")
                         Return ServiceResult.Good
                     End Function, UaRolePermissions.WITHOUT_RESTRICTIONS)

        Console.WriteLine($"  {machine.Path}")
        Console.WriteLine($"    Motor: Speed={speed.Value} rpm, Temp={temp.Value} degC")
        Console.WriteLine($"    Serial: {serial}, Setpoints: [100, 200, 300, 400]")

        Return New UaVariable(Of Double)() {speed, temp}
    End Function

    Sub Main(args As String())

        Dim LicenseUserName As String = "<Enter your UserName here>"
        Dim LicenseSerial As String = "<Enter your Serial here>"

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 19: Advanced Server     ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  A production-grade OPC UA server combining:                 ║")
        Console.WriteLine("║    * Multiple namespaces (Company types + Application)       ║")
        Console.WriteLine("║    * ObjectTypes with typed instances                        ║")
        Console.WriteLine("║    * Scalar variables, arrays, exposeElements                ║")
        Console.WriteLine("║    * Properties (EURange, EngineeringUnits)                  ║")
        Console.WriteLine("║    * Structured DataTypes (Structs)                          ║")
        Console.WriteLine("║    * Methods with input/output arguments                     ║")
        Console.WriteLine("║    * OnRead/OnWrite callbacks with validation                ║")
        Console.WriteLine("║    * Session tracking and certificate validation             ║")
        Console.WriteLine("║    * Continuous value push (simulated process data)          ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        ' =====================================================================
        ' Step 1: Configuration
        ' =====================================================================
        ' All server settings are defined in CreateConfig() below.
        Dim config = CreateConfig()
        PrintConfig(config)

        ' =====================================================================
        ' Step 2: Create server
        ' =====================================================================
        Using server As New UaServer(LicenseUserName, LicenseSerial)

            AddHandler server.CertificateValidation, Sub(s, e) e.Accept = True

            AddHandler server.SessionCreated, Sub(s, e)
                                                  Console.WriteLine($"  >> Session opened: {e.SessionName} ({e.ClientUri})")
                                              End Sub
            AddHandler server.SessionClosed, Sub(s, e)
                                                 Console.WriteLine($"  >> Session closed: {e.SessionName}")
                                             End Sub

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
            ' Step 3: Register company namespace
            ' =================================================================
            Dim nsCompany As UShort = server.AddNamespace("urn:mycompany:cnc:types")

            Console.WriteLine($"  Namespace table:")
            Console.WriteLine($"    ns=2  {config.NamespaceUri} (application)")
            Console.WriteLine($"    ns={nsCompany}  urn:mycompany:cnc:types (company types)")
            Console.WriteLine()

            ' =================================================================
            ' Step 4: Define ObjectTypes
            ' =================================================================
            Console.WriteLine("-- Defining ObjectTypes ------------------------------------------")

            Dim motorTypeId = server.CreateObjectType("MotorType", ns:=nsCompany)
            Dim machineTypeId = server.CreateObjectType("MachineType", ns:=nsCompany)

            Console.WriteLine($"  MotorType    {motorTypeId}")
            Console.WriteLine($"  MachineType  {machineTypeId}")

            ' =================================================================
            ' Step 5: Define StructType
            ' =================================================================
            Dim factoryStatusTypeId = server.CreateStructDataType("FactoryStatusType", nsCompany,
                ("PlantName", DataTypeIds.String, Nothing),
                ("MachinesOnline", DataTypeIds.Int32, Nothing),
                ("TotalCycles", DataTypeIds.Int64, Nothing))

            Console.WriteLine($"  FactoryStatusType  {factoryStatusTypeId}")
            Console.WriteLine()

            ' =================================================================
            ' Step 6: Build the address space
            ' =================================================================
            Console.WriteLine("-- Building address space ----------------------------------------")

            Dim factory = server.CreateFolder("Factory", UaRolePermissions.WITHOUT_RESTRICTIONS)

            Dim machine1Vars = CreateMachine(server, factory, "CNC_Machine_01", "SN-2025-001",
                machineTypeId, motorTypeId, 2400.0, 52.0)
            Dim machine2Vars = CreateMachine(server, factory, "CNC_Machine_02", "SN-2025-002",
                machineTypeId, motorTypeId, 1800.0, 45.0)
            Console.WriteLine()

            ' Factory status struct
            Dim factoryStatus = server.CreateStructVariable(factory, "FactoryStatus", factoryStatusTypeId, UaRolePermissions.WITHOUT_RESTRICTIONS)
            factoryStatus.SetField(Of String)("PlantName", "MainFactory")
            factoryStatus.SetField(Of Integer)("MachinesOnline", 2)
            factoryStatus.SetField(Of Long)("TotalCycles", 0L)

            Console.WriteLine($"  {factoryStatus.Path}")
            Console.WriteLine($"    PlantName=MainFactory, MachinesOnline=2")

            ' Environment data
            Dim envFolder = server.CreateFolder(factory, "EnvironmentData", UaRolePermissions.WITHOUT_RESTRICTIONS)

            Dim ambientTemp = server.CreateVariable(Of Double)(envFolder, "AmbientTemp", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=21.5)
            ambientTemp.SetEURange(0, 50)
            ambientTemp.SetEngineeringUnits("degC", "Degrees Celsius")

            Dim humidity = server.CreateVariable(Of Double)(envFolder, "Humidity", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=45.0)
            humidity.SetEURange(0, 100)
            humidity.SetEngineeringUnits("%", "Percent relative humidity")

            Dim readings = server.CreateArrayVariable(Of Double)(envFolder, "Readings",
                initialValue:=New Double() {21.5, 21.3, 21.7, 21.4, 21.6, 21.5},
                readOnly:=True, exposeElements:=True)

            Console.WriteLine($"  {envFolder.Path}")
            Console.WriteLine($"    AmbientTemp=21.5 degC, Humidity=45.0 %")
            Console.WriteLine()

            ' =================================================================
            ' Step 7: Writable parameters with validation
            ' =================================================================
            Console.WriteLine("-- Writable parameters with validation ---------------------------")

            Dim paramFolder = server.CreateFolder("Parameters", UaRolePermissions.WITHOUT_RESTRICTIONS)

            Dim maxSpeed = server.CreateVariable(Of Double)(paramFolder, "MaxSpeed", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=3000.0)
            maxSpeed.SetEURange(0, 6000)
            maxSpeed.SetEngineeringUnits("rpm", "Revolutions per minute")
            maxSpeed.OnWrite = Function(newValue)
                                   If newValue < 0 OrElse newValue > 6000 Then
                                       Console.WriteLine($"  !! MaxSpeed rejected: {newValue} (must be 0..6000)")
                                       Return False
                                   End If
                                   Console.WriteLine($"  >> MaxSpeed accepted: {newValue}")
                                   Return True
                               End Function

            Dim emergencyStop = server.CreateVariable(Of Boolean)(paramFolder, "EmergencyStop", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=False)
            emergencyStop.OnWrite = Function(newValue)
                                        If newValue Then
                                            Console.WriteLine("  !! EMERGENCY STOP ACTIVATED by client")
                                        Else
                                            Console.WriteLine("  >> Emergency stop released")
                                        End If
                                        Return True
                                    End Function

            Dim batchSize = server.CreateVariable(Of Integer)(paramFolder, "BatchSize", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=100)
            batchSize.OnWrite = Function(newValue)
                                    If newValue < 1 OrElse newValue > 1000 Then
                                        Console.WriteLine($"  !! BatchSize rejected: {newValue} (must be 1..1000)")
                                        Return False
                                    End If
                                    Return True
                                End Function

            Dim maxLinearSpeed = server.CreateVariable(Of Double)(paramFolder, "MaxLinearSpeed", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=0.0, readOnly:=True)
            maxLinearSpeed.SetEngineeringUnits("m/s", "Meters per second")
            maxLinearSpeed.OnRead = Function(current)
                                        Return Math.Round(maxSpeed.Value * 2.0 * Math.PI * 0.1 / 60.0, 3)
                                    End Function

            Console.WriteLine($"  {maxSpeed.Path,-45} OnWrite validates 0..6000")
            Console.WriteLine($"  {emergencyStop.Path,-45} OnWrite logs to console")
            Console.WriteLine($"  {batchSize.Path,-45} OnWrite validates 1..1000")
            Console.WriteLine($"  {maxLinearSpeed.Path,-45} OnRead computes from MaxSpeed")
            Console.WriteLine()

            ' =================================================================
            ' Step 8: Run the server
            ' =================================================================
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║")
            Console.WriteLine("║  opc.tcp://localhost:48410                                   ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Connect anonymously - full read/write access                ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Press ENTER to start the simulation loop.                   ║")
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
            Console.ReadLine()

            ' =================================================================
            ' Step 9: Simulation loop
            ' =================================================================
            Console.WriteLine("Simulating process data... (CTRL+C to exit)")
            Console.WriteLine()

            Dim rng As New Random()

            While True
                Dim eStop As Boolean = emergencyStop.Value

                If Not eStop Then
                    machine1Vars(0).Value = Math.Round(2200.0 + rng.NextDouble() * 400.0, 1)
                    machine1Vars(1).Value = Math.Round(48.0 + rng.NextDouble() * 10.0, 1)
                End If

                If Not eStop Then
                    machine2Vars(0).Value = Math.Round(1600.0 + rng.NextDouble() * 400.0, 1)
                    machine2Vars(1).Value = Math.Round(42.0 + rng.NextDouble() * 8.0, 1)
                End If

                If Not eStop Then
                    Dim c1 As Long = server.GetValue(Of Long)("Objects.Factory.CNC_Machine_01.CycleCount")
                    Dim c2 As Long = server.GetValue(Of Long)("Objects.Factory.CNC_Machine_02.CycleCount")
                    server.SetValue("Objects.Factory.CNC_Machine_01.CycleCount", c1 + rng.Next(1, 5))
                    server.SetValue("Objects.Factory.CNC_Machine_02.CycleCount", c2 + rng.Next(1, 3))
                End If

                ambientTemp.Value = Math.Round(20.0 + rng.NextDouble() * 3.0, 1)
                humidity.Value = Math.Round(40.0 + rng.NextDouble() * 20.0, 1)
                readings.Value = New Double() {
                    Math.Round(20.0 + rng.NextDouble() * 3.0, 1),
                    Math.Round(20.0 + rng.NextDouble() * 3.0, 1),
                    Math.Round(20.0 + rng.NextDouble() * 3.0, 1),
                    Math.Round(20.0 + rng.NextDouble() * 3.0, 1),
                    Math.Round(20.0 + rng.NextDouble() * 3.0, 1),
                    Math.Round(20.0 + rng.NextDouble() * 3.0, 1)
                }

                factoryStatus.SetField(Of Integer)("MachinesOnline", If(eStop, 0, 2))
                factoryStatus.SetField(Of Long)("TotalCycles",
                    server.GetValue(Of Long)("Objects.Factory.CNC_Machine_01.CycleCount") +
                    server.GetValue(Of Long)("Objects.Factory.CNC_Machine_02.CycleCount"))

                Dim displayCycles As Long =
                    server.GetValue(Of Long)("Objects.Factory.CNC_Machine_01.CycleCount") +
                    server.GetValue(Of Long)("Objects.Factory.CNC_Machine_02.CycleCount")

                Console.Write($"{vbCr}  M1: {machine1Vars(0).Value,7:F1}rpm {machine1Vars(1).Value,5:F1}C  " &
                              $"M2: {machine2Vars(0).Value,7:F1}rpm {machine2Vars(1).Value,5:F1}C  " &
                              $"Cycles={displayCycles,-8} {If(eStop, "E-STOP!", "       ")}")
                Thread.Sleep(1000)
            End While

        End Using

    End Sub


    ' ==========================================================================
    ' Helper: CreateConfig
    ' ==========================================================================
    ' Returns the server configuration. Adjust to your needs.
    Private Function CreateConfig() As UaServerConfiguration
        Dim cfg As New UaServerConfiguration
        ' ── Application Identity ──────────────────────────────────────────────
        cfg.ApplicationName = "PLCcom Workshop 19 - Advanced Server"
        cfg.ApplicationUri  = "urn:localhost:PLCcom:Workshop:19"
        cfg.ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/"
        cfg.NamespaceUri    = "http://indi-an.com/opcua/workshop/advanced-server"

        ' ── ServerStatus/BuildInfo ────────────────────────────────────────────
        cfg.ManufacturerName = "My Company GmbH"
        cfg.ProductName      = "CNC Factory Server"
        cfg.SoftwareVersion  = "2.0.0"
        cfg.BuildNumber      = "100"

        ' ── Endpoints ────────────────────────────────────────────────────────
        cfg.BaseAddresses = New List(Of String) From {"opc.tcp://localhost:48410", "opc.https://localhost:48411"}

        ' ── Security Policies ────────────────────────────────────────────────
        cfg.SecurityPolicies = UaServer.GetRecommendedSecurityPolicies()

        ' ── User Authentication ───────────────────────────────────────────────
        cfg.UserTokenPolicies = New List(Of UserTokenPolicy) From {New UserTokenPolicy With {.TokenType = UserTokenType.Anonymous}}

        ' ── PKI Certificate Store ─────────────────────────────────────────────
        cfg.CertificateStorePath = ".\pki"
        cfg.CertificateLifetimeInMonths = 60
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
        cfg.VendorProductName = "CNC Factory Server"
        cfg.VendorProductVersion = "2.0.0"

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
        Return cfg
    End Function

    ' ==========================================================================
    ' Helper: PrintConfig
    ' ==========================================================================
    Private Sub PrintConfig(config As UaServerConfiguration)
        Console.WriteLine("-- Active Server Configuration ------------------------------")
        Console.WriteLine("  ApplicationName  : " & config.ApplicationName)
        Console.WriteLine("  ApplicationUri   : " & config.ApplicationUri)
        Console.WriteLine("  NamespaceUri     : " & If(config.NamespaceUri, "(default)"))
        Console.WriteLine("  ManufacturerName : " & If(config.ManufacturerName, "(not set)"))
        Console.WriteLine("  ProductName      : " & If(config.ProductName, "(not set)"))
        Console.WriteLine("  SoftwareVersion  : " & If(config.SoftwareVersion, "(auto-detect)"))
        Console.WriteLine("  BuildNumber      : " & If(config.BuildNumber, "(auto-detect)"))
        Console.WriteLine()
        Console.WriteLine("  Endpoints:")
        For Each addr In config.BaseAddresses : Console.WriteLine("    " & addr) : Next
        Console.WriteLine()
                Console.WriteLine("  EndpointHostMode : " & config.EndpointHostMode.ToString())
        Console.WriteLine("  VendorServerInfo:")
        Console.WriteLine("    VendorName=" & If(config.VendorName, "(not set)") & "  ProductName=" & If(config.VendorProductName, "(not set)") & "  Version=" & If(config.VendorProductVersion, "(not set)"))
        Console.WriteLine()
        Console.WriteLine("  OperationLimits:")
        Console.WriteLine("    Read=" & config.MaxNodesPerRead & "  Write=" & config.MaxNodesPerWrite & "  Browse=" & config.MaxNodesPerBrowse & "  Method=" & config.MaxNodesPerMethodCall)
        Console.WriteLine("    HistRD=" & config.MaxNodesPerHistoryReadData & "  HistRE=" & config.MaxNodesPerHistoryReadEvents & "  HistUD=" & config.MaxNodesPerHistoryUpdateData & "  HistUE=" & config.MaxNodesPerHistoryUpdateEvents)
        Console.WriteLine("    Register=" & config.MaxNodesPerRegisterNodes & "  Translate=" & config.MaxNodesPerTranslateBrowsePathsToNodeIds & "  NodeMgmt=" & config.MaxNodesPerNodeManagement & "  MonItems=" & config.MaxMonitoredItemsPerCall)
        Console.WriteLine("-------------------------------------------------------------")
        Console.WriteLine()
    End Sub

End Module
