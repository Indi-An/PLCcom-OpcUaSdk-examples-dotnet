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
' PLCcom OPC UA Server SDK - Workshop 14: Variables and Arrays
'
' This workshop demonstrates the full range of variable features:
'   1. All scalar data types supported by OPC UA
'   2. Properties - EURange and EngineeringUnits
'   3. OnRead / OnWrite callbacks
'   4. Arrays with exposeElements
'   5. Read-only variables and write rejection via OnWrite
'
' Connect with any OPC UA client to: opc.tcp://localhost:48410
' ==============================================================================

Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Server.Sdk
Imports System
Imports System.Collections.Generic
Imports System.Reflection
Imports System.Threading
Module Program

    Sub Main(args As String())

        Dim LicenseUserName As String = "<Enter your UserName here>"
        Dim LicenseSerial As String = "<Enter your Serial here>"

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 14: Variables & Arrays  ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This example demonstrates:                                  ║")
        Console.WriteLine("║    * All OPC UA scalar data types                            ║")
        Console.WriteLine("║    * EURange and EngineeringUnits properties                 ║")
        Console.WriteLine("║    * OnRead / OnWrite callbacks                              ║")
        Console.WriteLine("║    * Arrays with exposeElements (browsable child nodes)      ║")
        Console.WriteLine("║    * Read-only variables and write validation                ║")
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
                                                     Console.WriteLine($"  << Written: {item.Path} ({item.NodeId}) = {item.Value}")
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
            ' Part A: Scalar data types
            ' =================================================================
            Console.WriteLine("-- Part A: Scalar data types ------------------------------------")

            Dim scalars = server.CreateFolder("Scalars", UaRolePermissions.WITHOUT_RESTRICTIONS)

            Dim vBool = server.CreateVariable(Of Boolean)(scalars, "MyBool", UaRolePermissions.WITHOUT_RESTRICTIONS, True)
            Dim vByte = server.CreateVariable(Of Byte)(scalars, "MyByte", UaRolePermissions.WITHOUT_RESTRICTIONS, CByte(42))
            Dim vSByte = server.CreateVariable(Of SByte)(scalars, "MySByte", UaRolePermissions.WITHOUT_RESTRICTIONS, CSByte(-7))
            Dim vInt16 = server.CreateVariable(Of Short)(scalars, "MyInt16", UaRolePermissions.WITHOUT_RESTRICTIONS, CShort(-1000))
            Dim vUInt16 = server.CreateVariable(Of UShort)(scalars, "MyUInt16", UaRolePermissions.WITHOUT_RESTRICTIONS, CUShort(5000))
            Dim vInt32 = server.CreateVariable(Of Integer)(scalars, "MyInt32", UaRolePermissions.WITHOUT_RESTRICTIONS, 100000)
            Dim vUInt32 = server.CreateVariable(Of UInteger)(scalars, "MyUInt32", UaRolePermissions.WITHOUT_RESTRICTIONS, CUInt(200000))
            Dim vInt64 = server.CreateVariable(Of Long)(scalars, "MyInt64", UaRolePermissions.WITHOUT_RESTRICTIONS, 9876543210L)
            Dim vUInt64 = server.CreateVariable(Of ULong)(scalars, "MyUInt64", UaRolePermissions.WITHOUT_RESTRICTIONS, CULng(1234567890))
            Dim vFloat = server.CreateVariable(Of Single)(scalars, "MyFloat", UaRolePermissions.WITHOUT_RESTRICTIONS, 3.14F)
            Dim vDouble = server.CreateVariable(Of Double)(scalars, "MyDouble", UaRolePermissions.WITHOUT_RESTRICTIONS, 2.71828)
            Dim vString = server.CreateVariable(Of String)(scalars, "MyString", UaRolePermissions.WITHOUT_RESTRICTIONS, "Hello OPC UA")
            Dim vDateTime = server.CreateVariable(Of DateTime)(scalars, "MyDateTime", UaRolePermissions.WITHOUT_RESTRICTIONS, DateTime.UtcNow)
            Dim vGuid = server.CreateVariable(Of Guid)(scalars, "MyGuid", UaRolePermissions.WITHOUT_RESTRICTIONS, Guid.NewGuid())
            Dim vByteString = server.CreateVariable(Of Byte())(scalars, "MyByteString", UaRolePermissions.WITHOUT_RESTRICTIONS, New Byte() {&HDE, &HAD, &HBE, &HEF})

            Console.WriteLine($"  Boolean     {vBool.Path,-40} = {vBool.Value}")
            Console.WriteLine($"  Byte        {vByte.Path,-40} = {vByte.Value}")
            Console.WriteLine($"  SByte       {vSByte.Path,-40} = {vSByte.Value}")
            Console.WriteLine($"  Int16       {vInt16.Path,-40} = {vInt16.Value}")
            Console.WriteLine($"  UInt16      {vUInt16.Path,-40} = {vUInt16.Value}")
            Console.WriteLine($"  Int32       {vInt32.Path,-40} = {vInt32.Value}")
            Console.WriteLine($"  UInt32      {vUInt32.Path,-40} = {vUInt32.Value}")
            Console.WriteLine($"  Int64       {vInt64.Path,-40} = {vInt64.Value}")
            Console.WriteLine($"  UInt64      {vUInt64.Path,-40} = {vUInt64.Value}")
            Console.WriteLine($"  Float       {vFloat.Path,-40} = {vFloat.Value}")
            Console.WriteLine($"  Double      {vDouble.Path,-40} = {vDouble.Value}")
            Console.WriteLine($"  String      {vString.Path,-40} = {vString.Value}")
            Console.WriteLine($"  DateTime    {vDateTime.Path,-40} = {vDateTime.Value:u}")
            Console.WriteLine($"  Guid        {vGuid.Path,-40} = {vGuid.Value}")
            Console.WriteLine($"  ByteString  {vByteString.Path,-40} = {BitConverter.ToString(vByteString.Value)}")
            Console.WriteLine()

            ' =================================================================
            ' Part B: Properties - EURange and EngineeringUnits
            ' =================================================================
            Console.WriteLine("-- Part B: Properties (EURange, EngineeringUnits) --------------")

            Dim props = server.CreateFolder("Properties", UaRolePermissions.WITHOUT_RESTRICTIONS)

            Dim temperature = server.CreateVariable(Of Double)(props, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, 22.5)
            temperature.SetEURange(0, 100)
            temperature.SetEngineeringUnits("degC", "Degrees Celsius")

            Dim pressure = server.CreateVariable(Of Double)(props, "Pressure", UaRolePermissions.WITHOUT_RESTRICTIONS, 1.013)
            pressure.SetEURange(0, 10)
            pressure.SetEngineeringUnits("bar", "Bar")

            Dim speed = server.CreateVariable(Of Double)(props, "Speed", UaRolePermissions.WITHOUT_RESTRICTIONS, 1500.0)
            speed.SetEURange(0, 3000)
            speed.SetEngineeringUnits("rpm", "Revolutions per minute")

            Console.WriteLine($"  {temperature.Path,-45} = {temperature.Value}  [0..100 degC]")
            Console.WriteLine($"  {pressure.Path,-45} = {pressure.Value}  [0..10 bar]")
            Console.WriteLine($"  {speed.Path,-45} = {speed.Value}  [0..3000 rpm]")
            Console.WriteLine()

            ' =================================================================
            ' Part C: OnRead / OnWrite callbacks
            ' =================================================================
            Console.WriteLine("-- Part C: OnRead / OnWrite callbacks ---------------------------")

            Dim callbacks = server.CreateFolder("Callbacks", UaRolePermissions.WITHOUT_RESTRICTIONS)

            Dim computed = server.CreateVariable(Of Double)(callbacks, "Computed", UaRolePermissions.WITHOUT_RESTRICTIONS, 0.0, readOnly:=True)
            computed.OnRead = Function(currentValue)
                                  Return Math.Round(temperature.Value * 1.8 + 32.0, 2)
                              End Function

            Dim validated = server.CreateVariable(Of Integer)(callbacks, "Validated", UaRolePermissions.WITHOUT_RESTRICTIONS, 50)
            validated.OnWrite = Function(newValue)
                                    If newValue < 0 OrElse newValue > 100 Then
                                        Console.WriteLine($"  !! Rejected write: {newValue} (must be 0..100)")
                                        Return False
                                    End If
                                    Return True
                                End Function

            Dim counter = server.CreateVariable(Of Integer)(callbacks, "Counter", UaRolePermissions.WITHOUT_RESTRICTIONS, 0, readOnly:=True)

            Console.WriteLine($"  {computed.Path,-45} OnRead -> Fahrenheit")
            Console.WriteLine($"  {validated.Path,-45} OnWrite -> reject if not 0..100")
            Console.WriteLine($"  {counter.Path,-45} [ReadOnly] server-incremented")
            Console.WriteLine()

            ' =================================================================
            ' Part D: Arrays and exposeElements
            ' =================================================================
            Console.WriteLine("-- Part D: Arrays and exposeElements ----------------------------")

            Dim arrays = server.CreateFolder("Arrays", UaRolePermissions.WITHOUT_RESTRICTIONS)

            Dim temps = server.CreateArrayVariable(Of Double)(arrays, "Temperatures",
                initialValue:=New Double() {20.0, 21.5, 22.0, 23.5, 24.0})

            Dim setpoints = server.CreateArrayVariable(Of Double)(arrays, "Setpoints",
                initialValue:=New Double() {100.0, 200.0, 300.0, 400.0},
                exposeElements:=True)

            Dim flags = server.CreateArrayVariable(Of Boolean)(arrays, "Flags",
                initialValue:=New Boolean() {True, False, True},
                exposeElements:=True)

            Console.WriteLine($"  {temps.Path,-45} Double[5]  (plain array)")
            Console.WriteLine($"  {setpoints.Path,-45} Double[4]  (exposeElements)")
            Console.WriteLine($"  {flags.Path,-45} Bool[3]    (exposeElements)")
            Console.WriteLine()

            ' =================================================================
            ' Step 6: Run the server
            ' =================================================================
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║")
            Console.WriteLine("║  opc.tcp://localhost:48410                                   ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Press ENTER to start the value push loop.                   ║")
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
            Console.ReadLine()

            Console.WriteLine("Pushing values every second... (CTRL+C to exit)")
            Console.WriteLine()

            Dim rng As New Random()
            Dim cycle As Long = 0

            While True
                cycle += 1

                temperature.Value = Math.Round(18.0 + rng.NextDouble() * 12.0, 2)
                pressure.Value = Math.Round(0.8 + rng.NextDouble() * 0.5, 3)
                speed.Value = 1200.0 + rng.Next(600)

                counter.Value = CInt(cycle)

                temps.Value = New Double() {
                    Math.Round(19.0 + rng.NextDouble() * 3.0, 1),
                    Math.Round(20.0 + rng.NextDouble() * 3.0, 1),
                    Math.Round(21.0 + rng.NextDouble() * 3.0, 1),
                    Math.Round(22.0 + rng.NextDouble() * 3.0, 1),
                    Math.Round(23.0 + rng.NextDouble() * 3.0, 1)
                }

                setpoints.Value = New Double() {
                    100.0 + rng.Next(50),
                    200.0 + rng.Next(50),
                    300.0 + rng.Next(50),
                    400.0 + rng.Next(50)
                }

                Console.Write($"{vbCr}  Cycle={cycle}  Temp={temperature.Value:F1}C " &
                              $"({computed.Value:F1}F)  P={pressure.Value:F3}bar  " &
                              $"Counter={counter.Value}  ")
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
        cfg.ApplicationName = "PLCcom Workshop 14 - Variables and Arrays"
        cfg.ApplicationUri  = "urn:localhost:PLCcom:Workshop:14"
        cfg.ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/"
        cfg.NamespaceUri    = "http://indi-an.com/opcua/workshop/variables-and-arrays"
        cfg.ManufacturerName = "My Company GmbH"
        cfg.ProductName      = "My OPC UA Server"
        cfg.SoftwareVersion  = "1.0.0"
        cfg.BuildNumber      = "42"
        cfg.BaseAddresses = New List(Of String) From {"opc.tcp://localhost:48410", "opc.https://localhost:48411"}
        cfg.SecurityPolicies = UaServer.GetRecommendedSecurityPolicies()
        cfg.UserTokenPolicies = New List(Of UserTokenPolicy) From {New UserTokenPolicy With {.TokenType = UserTokenType.Anonymous}}
        cfg.AutoAcceptUntrustedCertificates = False
        ' AsConfigured (default) = endpoints use exactly the host from BaseAddresses
        ' NormalizeToHostname    = replace localhost/127.0.0.1 with the machine name
        ' None = no normalization, behavior depends on DNS and network settings
        cfg.EndpointHostMode = EndpointHostMode.AsConfigured
        cfg.MaxSessionCount = 100
        cfg.ShutdownDelay   = 5
        cfg.VendorName           = "My Company GmbH"
        cfg.VendorProductName    = "My OPC UA Server"
        cfg.VendorProductVersion = "1.0.0"
        cfg.MaxNodesPerRead = 1000 : cfg.MaxNodesPerWrite = 1000 : cfg.MaxNodesPerBrowse = 1000
        cfg.MaxNodesPerHistoryReadData = 100 : cfg.MaxNodesPerHistoryReadEvents = 100
        cfg.MaxNodesPerHistoryUpdateData = 100 : cfg.MaxNodesPerHistoryUpdateEvents = 100
        cfg.MaxNodesPerMethodCall = 200 : cfg.MaxNodesPerRegisterNodes = 1000
        cfg.MaxNodesPerTranslateBrowsePathsToNodeIds = 1000
        cfg.MaxNodesPerNodeManagement = 1000 : cfg.MaxMonitoredItemsPerCall = 1000

        ' -- PKI Certificate Store -----------------------------------------------
        ' UaServerCertificateStore manages all server certificates.
        ' Load() tries to load existing certificates from disk.
        ' GetMissingOrExpired() returns certificates that need to be (re)created.
        ' Build(overwrite:=True) creates a new self-signed certificate on disk.
        ''
        ' One Application certificate is required for the OPC UA secure channel.
        ' One HTTPS certificate is added per opc.https:// hostname automatically.
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

        For Each host In UaServerCertificateStore.ExtractHttpsHostnames(cfg.BaseAddresses)
            certs.Add(New UaServerCertificate(
                pkiBase:=".\pki",
                password:="secretpassword",
                alias:=host,
                applicationUri:=$"urn:{host}:https",
                validityDays:=720,
                organisation:="Indi.An GmbH",
                role:=UaServerCertificate.CertificateRole.Https))
        Next

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