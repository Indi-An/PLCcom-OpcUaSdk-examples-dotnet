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
' PLCcom OPC UA Server SDK - Workshop 13: Methods
'
' OPC UA Methods are callable functions in the server's address space.
' A client can invoke a method by sending a Call service request - similar
' to calling a remote procedure (RPC). Methods can have typed input
' arguments and return typed output arguments.
'
' What you will learn:
'   * How to create a method without arguments (Reset)
'   * How to create a method with input and output arguments (Add, Multiply)
'   * How to create a method that modifies server-side state (SetTemperature)
'   * How to receive a structured ExtensionObject argument (myMethodNode)
'     -> used by Client Workshop 24 (Simple Method Calls)
'   * How to receive nested structured arguments (myObjectNode_Advanced)
'     -> used by Client Workshop 25 (Advanced Calls with Structs)
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
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 13: Methods             ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Methods are callable functions in the address space.        ║")
        Console.WriteLine("║  Clients invoke them via the OPC UA Call service.            ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This example creates six methods:                           ║")
        Console.WriteLine("║    Reset()                   - resets CycleCount to 0        ║")
        Console.WriteLine("║    Add(A, B) -> Sum           - returns A + B                ║")
        Console.WriteLine("║    Multiply(A, B) -> Product  - returns A x B                ║")
        Console.WriteLine("║    SetTemperature(value)      - updates a server variable    ║")
        Console.WriteLine("║    myMethodNode(DataStructure_One) - for Client Workshop 24  ║")
        Console.WriteLine("║    myMethodNode(nested structs)     - for Client Workshop 25 ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  In UA Expert: right-click a method -> Call...               ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Dim config As New UaServerConfiguration With {
            .ApplicationName = "PLCcom Workshop 13 - Methods",
            .ApplicationUri = "urn:localhost:PLCcom:Workshop:13",
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
            .NamespaceUri = "http://indi-an.com/opcua/workshop/methods"
        }

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
            ' Step 2: Create the address space with variables
            ' =================================================================
            Dim plant = server.CreateFolder("Plant", UaRolePermissions.WITHOUT_RESTRICTIONS)
            Dim machine = server.CreateFolder(plant, "Machine1", UaRolePermissions.WITHOUT_RESTRICTIONS)

            Dim counter = server.CreateVariable(Of Long)(machine, "CycleCount", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=0L)
            Dim temp = server.CreateVariable(Of Double)(machine, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=22.0)

            Console.WriteLine("-- Address space ------------------------------------------------")
            Console.WriteLine($"  Int64   {counter.Path,-40} {counter.NodeId}  = 0")
            Console.WriteLine($"  Double  {temp.Path,-40} {temp.NodeId}  = 22.0")
            Console.WriteLine()

            ' =================================================================
            ' Step 3: Create methods
            ' =================================================================

            ' -- Method 1: Reset (no arguments) --------------------------------
            server.CreateMethod(machine, "Reset",
                handler:=Function(ctx, method, objectId, inputArgs, outputArgs)
                             counter.Value = 0L
                             Console.WriteLine("  [METHOD] Reset() -> CycleCount = 0")
                             Return ServiceResult.Good
                         End Function, UaRolePermissions.WITHOUT_RESTRICTIONS)

            ' -- Method 2: Add (two inputs, one output) ------------------------
            server.CreateMethod(machine, "Add",
                Function(ctx, method, objectId, inputArgs, outputArgs)
                    Dim a As Double = CDbl(inputArgs(0))
                    Dim b As Double = CDbl(inputArgs(1))
                    outputArgs(0) = a + b
                    Console.WriteLine($"  [METHOD] Add({a}, {b}) = {a + b}")
                    Return ServiceResult.Good
                End Function,
                UaRolePermissions.WITHOUT_RESTRICTIONS,
                inputArgs:=New Argument() {
                    New Argument With {.Name = "A", .DataType = DataTypeIds.Double,
                        .ValueRank = ValueRanks.Scalar, .Description = "First operand"},
                    New Argument With {.Name = "B", .DataType = DataTypeIds.Double,
                        .ValueRank = ValueRanks.Scalar, .Description = "Second operand"}
                },
                outputArgs:=New Argument() {
                    New Argument With {.Name = "Sum", .DataType = DataTypeIds.Double,
                        .ValueRank = ValueRanks.Scalar, .Description = "Result of A + B"}
                })

            ' -- Method 3: Multiply (two inputs, one output) -------------------
            server.CreateMethod(machine, "Multiply",
                Function(ctx, method, objectId, inputArgs, outputArgs)
                    Dim a As Double = CDbl(inputArgs(0))
                    Dim b As Double = CDbl(inputArgs(1))
                    outputArgs(0) = a * b
                    Console.WriteLine($"  [METHOD] Multiply({a}, {b}) = {a * b}")
                    Return ServiceResult.Good
                End Function,
                UaRolePermissions.WITHOUT_RESTRICTIONS,
                inputArgs:=New Argument() {
                    New Argument With {.Name = "A", .DataType = DataTypeIds.Double,
                        .ValueRank = ValueRanks.Scalar, .Description = "First factor"},
                    New Argument With {.Name = "B", .DataType = DataTypeIds.Double,
                        .ValueRank = ValueRanks.Scalar, .Description = "Second factor"}
                },
                outputArgs:=New Argument() {
                    New Argument With {.Name = "Product", .DataType = DataTypeIds.Double,
                        .ValueRank = ValueRanks.Scalar, .Description = "Result of A x B"}
                })

            ' -- Method 4: SetTemperature (modifies server state) --------------
            server.CreateMethod(machine, "SetTemperature",
                Function(ctx, method, objectId, inputArgs, outputArgs)
                    Dim newTemp As Double = CDbl(inputArgs(0))
                    temp.Value = newTemp
                    Console.WriteLine($"  [METHOD] SetTemperature({newTemp:F1}) -> Temperature updated")
                    Return ServiceResult.Good
                End Function,
                UaRolePermissions.WITHOUT_RESTRICTIONS,
                inputArgs:=New Argument() {
                    New Argument With {.Name = "NewTemperature", .DataType = DataTypeIds.Double,
                        .ValueRank = ValueRanks.Scalar, .Description = "New temperature value in Celsius"}
                })

            Console.WriteLine("-- Methods under Machine1 ---------------------------------------")
            Console.WriteLine("  Reset()                    -> resets CycleCount to 0")
            Console.WriteLine("  Add(A, B) -> Sum           -> returns A + B")
            Console.WriteLine("  Multiply(A, B) -> Product  -> returns A x B")
            Console.WriteLine("  SetTemperature(value)      -> updates Temperature variable")
            Console.WriteLine()

            ' =================================================================
            ' Step 4: myObjectNode / myMethodNode for Client Workshop 24
            ' =================================================================
            ' Client Workshop 24 calls a method that receives a structured argument
            ' encoded as an ExtensionObject (BinaryEncoder). The structure is:
            '   DataStructure_One = { int, string, int, int, string }
            Dim myObjectNode = server.CreateObject(plant.NodeId, "myObjectNode", UaRolePermissions.WITHOUT_RESTRICTIONS)
            Console.WriteLine($"  myObjectNode NodeId = {myObjectNode.NodeId}")

            server.CreateMethod(myObjectNode.NodeId, "myMethodNode",
                Function(ctx, method, objectId, inputArgs, outputArgs)
                    Try
                        Dim ext = TryCast(inputArgs(0), ExtensionObject)
                        Dim body = TryCast(ext?.Body, Byte())
                        If body IsNot Nothing Then
                            Dim ctx2 As New ServiceMessageContext(Nothing)
                            Using decoder As New BinaryDecoder(body, ctx2)
                                Dim v1 As Integer = decoder.ReadInt32("")
                                Dim v2 As String = decoder.ReadString("")
                                Dim v3 As Integer = decoder.ReadInt32("")
                                Dim v4 As Integer = decoder.ReadInt32("")
                                Dim v5 As String = decoder.ReadString("")
                                Console.WriteLine($"  [METHOD] myMethodNode called: {v1}, {v2}, {v3}, {v4}, {v5}")
                                outputArgs(0) = $"Received: {v1} | {v2} | {v3} | {v4} | {v5}"
                            End Using
                        Else
                            outputArgs(0) = "No input received"
                        End If
                    Catch ex As Exception
                        Console.WriteLine($"  [METHOD] myMethodNode error: {ex.Message}")
                        outputArgs(0) = $"Error: {ex.Message}"
                    End Try
                    Return ServiceResult.Good
                End Function,
                UaRolePermissions.WITHOUT_RESTRICTIONS,
                inputArgs:=New Argument() {
                    New Argument With {.Name = "DataStructure_One", .DataType = DataTypeIds.Structure,
                        .ValueRank = ValueRanks.Scalar, .Description = "Encoded struct: int, string, int, int, string"}
                },
                outputArgs:=New Argument() {
                    New Argument With {.Name = "Result", .DataType = DataTypeIds.String,
                        .ValueRank = ValueRanks.Scalar, .Description = "Confirmation string"}
                })

            Console.WriteLine("-- myObjectNode (for Client Workshop 24) ------------------------")
            Console.WriteLine("  myMethodNode(DataStructure_One) -> Result")
            Console.WriteLine("  Input: ExtensionObject with BinaryEncoded { int, string, int, int, string }")
            Console.WriteLine()

            ' =================================================================
            ' Step 5: myObjectNode_Advanced / myMethodNode for Client Workshop 25
            ' =================================================================
            ' Client Workshop 25 calls a method with a nested structure:
            '   DataStructure_One = { int, string, DataStructure_Two, int, DataStructure_Two[], int }
            '   DataStructure_Two = { int, string, int }
            Dim myObjectNodeAdv = server.CreateObject(plant.NodeId, "myObjectNode_Advanced", UaRolePermissions.WITHOUT_RESTRICTIONS)
            Console.WriteLine($"  myObjectNode_Advanced NodeId = {myObjectNodeAdv.NodeId}")

            server.CreateMethod(myObjectNodeAdv.NodeId, "myMethodNode",
                Function(ctx, method, objectId, inputArgs, outputArgs)
                    Try
                        Dim ext = TryCast(inputArgs(0), ExtensionObject)
                        Dim body = TryCast(ext?.Body, Byte())
                        If body IsNot Nothing Then
                            Dim ctx2 As New ServiceMessageContext(Nothing)
                            Using decoder As New BinaryDecoder(body, ctx2)
                                Dim v1 As Integer = decoder.ReadInt32("")
                                Dim v2 As String = decoder.ReadString("")

                                ' embedded DataStructure_Two
                                Dim embExt = decoder.ReadExtensionObject("")
                                Dim embSummary As String = "(empty)"
                                Dim embBody = TryCast(embExt?.Body, Byte())
                                If embBody IsNot Nothing Then
                                    Using d2 As New BinaryDecoder(embBody, ctx2)
                                        Dim e1 As Integer = d2.ReadInt32("")
                                        Dim e2 As String = d2.ReadString("")
                                        Dim e3 As Integer = d2.ReadInt32("")
                                        embSummary = $"{e1},{e2},{e3}"
                                    End Using
                                End If

                                Dim v3 As Integer = decoder.ReadInt32("")

                                ' array of DataStructure_Two
                                Dim arr = decoder.ReadExtensionObjectArray("")
                                Dim arrCount As Integer = If(arr IsNot Nothing, arr.Count, 0)

                                Dim v4 As Integer = decoder.ReadInt32("")

                                Console.WriteLine($"  [METHOD_ADV] myMethodNode: v1={v1} v2={v2} emb=[{embSummary}] v3={v3} arr={arrCount} items v4={v4}")
                                outputArgs(0) = $"Received: {v1} | {v2} | emb=[{embSummary}] | v3={v3} | arr={arrCount} | v4={v4}"
                            End Using
                        Else
                            outputArgs(0) = "No input received"
                        End If
                    Catch ex As Exception
                        Console.WriteLine($"  [METHOD_ADV] error: {ex.Message}")
                        outputArgs(0) = $"Error: {ex.Message}"
                    End Try
                    Return ServiceResult.Good
                End Function,
                UaRolePermissions.WITHOUT_RESTRICTIONS,
                inputArgs:=New Argument() {
                    New Argument With {.Name = "DataStructure_One", .DataType = DataTypeIds.Structure,
                        .ValueRank = ValueRanks.Scalar, .Description = "Nested struct: int, string, DataStructure_Two, int, DataStructure_Two[], int"}
                },
                outputArgs:=New Argument() {
                    New Argument With {.Name = "Result", .DataType = DataTypeIds.String,
                        .ValueRank = ValueRanks.Scalar, .Description = "Confirmation string"}
                })

            Console.WriteLine("-- myObjectNode_Advanced (for Client Workshop 25) ---------------")
            Console.WriteLine("  myMethodNode(DataStructure_One) -> Result")
            Console.WriteLine("  Input: nested struct { int, string, DataStructure_Two, int, DataStructure_Two[], int }")
            Console.WriteLine()

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running on: opc.tcp://localhost:48410             ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Try in UA Expert:                                           ║")
            Console.WriteLine("║  * Browse Objects -> Plant -> Machine1                       ║")
            Console.WriteLine("║  * Right-click Reset -> Call                                 ║")
            Console.WriteLine("║  * Right-click Add -> Call, enter A=10 and B=20              ║")
            Console.WriteLine("║  * Call SetTemperature(42.5) and watch Temperature change    ║")
            Console.WriteLine("║  * Subscribe to Temperature, then call SetTemperature again  ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Use Client Workshop 24 to call myMethodNode with a          ║")
            Console.WriteLine("║  structured DataStructure_One argument.                      ║")
            Console.WriteLine("║  Use Client Workshop 25 for nested struct arguments.         ║")
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
        cfg.ApplicationName = "PLCcom Workshop 13 - Methods"
        cfg.ApplicationUri  = "urn:localhost:PLCcom:Workshop:13"
        cfg.ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/"
        cfg.NamespaceUri    = "http://indi-an.com/opcua/workshop/methods"
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