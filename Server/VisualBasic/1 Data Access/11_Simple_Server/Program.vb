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
' PLCcom OPC UA Server SDK - Workshop 11: Simple Server
'
' The starting point for all server workshops. This example creates a fully
' functional OPC UA server that any compliant client can connect to, browse,
' read, write and subscribe to.
'
' The key concepts demonstrated here form the foundation for every OPC UA
' server application:
'
'   1. Configuration - set up endpoints, security and certificates
'   2. Address space - create folders and variables that clients can see
'   3. Data types    - each variable has a specific OPC UA data type
'   4. Value push    - update values from code; subscribed clients are
'                      notified automatically (no polling needed)
'   5. Client writes - react to values written by OPC UA clients
'
' The address space built here is intentionally simple:
'   Objects
'     +-- Plant
'         +-- Line1
'             +-- Machine1
'                 +-- Temperature   (Double)     = 21.5
'                 +-- Pressure      (Float)      = 1.013
'                 +-- RPM           (Int32)      = 1500
'                 +-- IsRunning     (Boolean)    = true
'                 +-- Status        (String)     = "Idle"
'                 +-- LastUpdate    (DateTime)   = now
'                 +-- SerialNumber  (String)     = "SN-2025-001"  [ReadOnly]
'                 +-- Setpoints     (Double[])   = [20, 25, 30]
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

        ' -- License -----------------------------------------------------------
        ' Important !!!!!!!!!!!!!!!!!!
        ' Enter your Username + Serial here! Please note: with blank fields the library runs
        ' for 15 minutes during a debug session. Both values can also come
        ' from configuration or an environment variable.
        ' Free trial license (14 days, uninterrupted): https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-download/
        Dim LicenseUserName As String = ""
        Dim LicenseSerial As String = ""

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 11: Simple Server       ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This example creates a minimal OPC UA server with:          ║")
        Console.WriteLine("║    * Folder hierarchy  (Plant -> Line1 -> Machine1)          ║")
        Console.WriteLine("║    * Scalar variables  (Double, Float, Int, Bool, String)    ║")
        Console.WriteLine("║    * Array variable    (Double[])                            ║")
        Console.WriteLine("║    * Read-only variable (SerialNumber)                       ║")
        Console.WriteLine("║    * Client write notifications (ValuesWritten event)        ║")
        Console.WriteLine("║    * Continuous value push loop (1-second interval)          ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        ' =====================================================================
        ' Step 1: Configure the server
        ' =====================================================================
        ' All server settings are defined in CreateConfig() below.
        Dim config = CreateConfig()
        PrintConfig(config)

        ' =====================================================================
        ' Step 2: Create the server and wire up events
        ' =====================================================================
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
            ' If not handled or StatusCode remains Good, the write proceeds normally.
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
            ' Step 3: Build the address space
            ' =================================================================
            Console.WriteLine("-- Building address space ----------------------------------------")

            Dim plant = server.CreateFolder("Plant", UaRolePermissions.WITHOUT_RESTRICTIONS)
            Dim line1 = server.CreateFolder(plant, "Line1", UaRolePermissions.WITHOUT_RESTRICTIONS)
            Dim machine = server.CreateFolder(line1, "Machine1", UaRolePermissions.WITHOUT_RESTRICTIONS)

            Console.WriteLine($"  Folder    {plant.Path,-40} {plant.NodeId}")
            Console.WriteLine($"  Folder    {line1.Path,-40} {line1.NodeId}")
            Console.WriteLine($"  Folder    {machine.Path,-40} {machine.NodeId}")

            Dim temperature = server.CreateVariable(Of Double)(machine, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=21.5)
            Dim pressure = server.CreateVariable(Of Single)(machine, "Pressure", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=1.013F)
            Dim rpm = server.CreateVariable(Of Integer)(machine, "RPM", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=1500)
            Dim running = server.CreateVariable(Of Boolean)(machine, "IsRunning", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=True)
            Dim status = server.CreateVariable(Of String)(machine, "Status", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:="Idle")
            Dim lastUpdate = server.CreateVariable(Of DateTime)(machine, "LastUpdate", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=DateTime.UtcNow)

            Dim serialNo = server.CreateVariable(Of String)(machine, "SerialNumber", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:="SN-2025-001", readOnly:=True)

            Dim setpoints = server.CreateArrayVariable(Of Double)(machine, "Setpoints",
                initialValue:=New Double() {20.0, 25.0, 30.0})

            Console.WriteLine($"  Double    {temperature.Path,-40} {temperature.NodeId}  = 21.5")
            Console.WriteLine($"  Float     {pressure.Path,-40} {pressure.NodeId}  = 1.013")
            Console.WriteLine($"  Int32     {rpm.Path,-40} {rpm.NodeId}  = 1500")
            Console.WriteLine($"  Boolean   {running.Path,-40} {running.NodeId}  = true")
            Console.WriteLine($"  String    {status.Path,-40} {status.NodeId}  = Idle")
            Console.WriteLine($"  DateTime  {lastUpdate.Path,-40} {lastUpdate.NodeId}  = now")
            Console.WriteLine($"  String    {serialNo.Path,-40} {serialNo.NodeId}  = SN-2025-001 [ReadOnly]")
            Console.WriteLine($"  Double[]  {setpoints.Path,-40} {setpoints.NodeId}  = [20, 25, 30]")
            Console.WriteLine()

            ' =================================================================
            ' Step 4: Connect a client and explore
            ' =================================================================
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║")
            Console.WriteLine("║  opc.tcp://localhost:48410                                   ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Try:                                                        ║")
            Console.WriteLine("║  * Browse Objects -> Plant -> Line1 -> Machine1              ║")
            Console.WriteLine("║  * Subscribe to Temperature, RPM, Status                     ║")
            Console.WriteLine("║  * Write a new value to RPM or Status                        ║")
            Console.WriteLine("║  * Try writing to SerialNumber (should fail - ReadOnly)      ║")
            Console.WriteLine("║  * Watch the ValuesWritten output in this console            ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Press ENTER to start the value push loop.                   ║")
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
            Console.ReadLine()

            ' =================================================================
            ' Step 5: Push value changes to subscribed clients
            ' =================================================================
            Console.WriteLine("Pushing values every second... (CTRL+C to exit)")
            Console.WriteLine()

            Dim rng As New Random()
            Dim cycle As Long = 0

            While True
                cycle += 1

                temperature.Value = Math.Round(20.0 + rng.NextDouble() * 10.0, 2)
                pressure.Value = CSng(Math.Round(0.9 + rng.NextDouble() * 0.3, 3))
                rpm.Value = 1400 + rng.Next(200)
                running.Value = (cycle Mod 30 <> 0)
                status.Value = If(running.Value, "Running", "Stopped")
                lastUpdate.Value = DateTime.UtcNow

                Console.Write($"{vbCr}  Cycle={cycle}  Temp={temperature.Value:F1}C  " &
                              $"P={pressure.Value:F3}bar  RPM={rpm.Value}  {status.Value,-8}")
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
        cfg.ApplicationName = "PLCcom Workshop 11 - Simple Server"
        cfg.ApplicationUri = "urn:localhost:PLCcom:Workshop:11"
        cfg.ProductUri = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/"
        cfg.NamespaceUri = "http://indi-an.com/opcua/workshop/simple-server"
        cfg.ManufacturerName = "My Company GmbH"
        cfg.ProductName = "My OPC UA Server"
        cfg.SoftwareVersion = "1.0.0"
        cfg.BuildNumber = "42"
        cfg.BaseAddresses = New List(Of String) From {"opc.tcp://localhost:48410", "opc.https://localhost:48411"}
        cfg.SecurityPolicies = UaServer.GetRecommendedSecurityPolicies()
        cfg.UserTokenPolicies = New List(Of UserTokenPolicy) From {New UserTokenPolicy With {.TokenType = UserTokenType.Anonymous}}
        cfg.AutoAcceptUntrustedCertificates = False
        ' AsConfigured (default) = endpoints use exactly the host from BaseAddresses
        ' NormalizeToHostname    = replace localhost/127.0.0.1 with the machine name
        ' None = no normalization, behavior depends on DNS and network settings
        cfg.EndpointHostMode = EndpointHostMode.AsConfigured
        cfg.MaxSessionCount = 100
        cfg.ShutdownDelay = 5
        cfg.VendorName = "My Company GmbH"
        cfg.VendorProductName = "My OPC UA Server"
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