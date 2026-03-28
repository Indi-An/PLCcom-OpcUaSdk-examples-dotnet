Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Server.Sdk
Imports System
Imports System.Collections.Generic

' ==============================================================================
' PLCcom OPC UA Server SDK - Workshop 13: Methods
'
' OPC UA Methods are callable functions in the address space.
' A client can invoke a method by sending a Call service request.
' Methods can have typed input arguments and return typed output arguments.
'
' What you will learn:
'   * How to create a method without arguments (Reset)
'   * How to create a method with input and output arguments (Add, Multiply)
'   * How to create a method that modifies server-side state (SetTemperature)
'   * How to define argument types and descriptions
'
' Connect with any OPC UA client to:
'   opc.tcp://localhost:48412
' ==============================================================================

Module Program

    Sub Main(args As String())

        ' -- License -----------------------------------------------------------
        ' TODO: Submit your license information from your license e-mail
        Dim LicenseUserName As String = "<Enter your UserName here>"
        Dim LicenseSerial As String = "<Enter your Serial here>"

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 13: Methods             ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This example demonstrates:                                  ║")
        Console.WriteLine("║  * Simple method without arguments (Reset)                   ║")
        Console.WriteLine("║  * Method with input/output arguments (Add, Multiply)        ║")
        Console.WriteLine("║  * Method that modifies server state (SetTemperature)        ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Dim config As New UaServerConfiguration With {
            .ApplicationName = "PLCcom Workshop 13 - Methods",
            .ApplicationUri = "urn:localhost:PLCcom:Workshop:13",
            .ProductUri = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
            .BaseAddresses = New List(Of String) From {"opc.tcp://localhost:48412"},
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

            Dim plant = server.CreateFolder("Plant")
            Dim machine = server.CreateFolder(plant, "Machine1")
            Dim counter = server.CreateVariable(Of Long)(machine, "CycleCount", initialValue:=0L)
            Dim temp = server.CreateVariable(Of Double)(machine, "Temperature", initialValue:=22.0)

            ' -- Method 1: Reset (no arguments) --------------------------------
            ' The simplest form of a method - no inputs, no outputs.
            ' The handler is called when a client invokes the method.
            server.CreateMethod(machine, "Reset",
                handler:=Function(ctx, method, objectId, inputArgs, outputArgs)
                             counter.Value = 0L
                             Console.WriteLine(vbLf & "  [METHOD] Reset called -> CycleCount = 0")
                             Return ServiceResult.Good
                         End Function)

            ' -- Method 2: Add (two inputs, one output) ------------------------
            ' Methods with arguments require Argument descriptors that define:
            '   Name        - displayed in the client's call dialog
            '   DataType    - OPC UA data type (Double, Int32, String, etc.)
            '   ValueRank   - Scalar (-1) or array dimension
            '   Description - tooltip shown in the client
            server.CreateMethod(machine, "Add",
                handler:=Function(ctx, method, objectId, inputArgs, outputArgs)
                             Dim a As Double = CDbl(inputArgs(0))
                             Dim b As Double = CDbl(inputArgs(1))
                             outputArgs(0) = a + b
                             Console.WriteLine($"{vbLf}  [METHOD] Add({a}, {b}) = {a + b}")
                             Return ServiceResult.Good
                         End Function,
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
                handler:=Function(ctx, method, objectId, inputArgs, outputArgs)
                             Dim a As Double = CDbl(inputArgs(0))
                             Dim b As Double = CDbl(inputArgs(1))
                             outputArgs(0) = a * b
                             Console.WriteLine($"{vbLf}  [METHOD] Multiply({a}, {b}) = {a * b}")
                             Return ServiceResult.Good
                         End Function,
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
            ' Methods can read and write server-side variables.
            ' After this call, all clients subscribed to Temperature will receive
            ' a DataChange notification with the new value.
            server.CreateMethod(machine, "SetTemperature",
                handler:=Function(ctx, method, objectId, inputArgs, outputArgs)
                             Dim newTemp As Double = CDbl(inputArgs(0))
                             temp.Value = newTemp
                             Console.WriteLine($"{vbLf}  [METHOD] SetTemperature({newTemp:F1}) -> Temperature updated")
                             Return ServiceResult.Good
                         End Function,
                inputArgs:=New Argument() {
                    New Argument With {.Name = "NewTemperature", .DataType = DataTypeIds.Double,
                        .ValueRank = ValueRanks.Scalar, .Description = "New temperature value in Celsius"}
                })

            Console.WriteLine("  Methods created under Machine1:")
            Console.WriteLine("    * Reset()                    -> resets CycleCount to 0")
            Console.WriteLine("    * Add(A, B) -> Sum           -> returns A + B")
            Console.WriteLine("    * Multiply(A, B) -> Product  -> returns A x B")
            Console.WriteLine("    * SetTemperature(value)      -> updates Temperature variable")
            Console.WriteLine()

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║")
            Console.WriteLine("║  opc.tcp://localhost:48412                                   ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Try:                                                        ║")
            Console.WriteLine("║  * Browse to Machine1, right-click Reset -> Call             ║")
            Console.WriteLine("║  * Right-click Add -> Call, enter A=10 and B=20              ║")
            Console.WriteLine("║  * Call SetTemperature(42.5) and watch Temperature change    ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Press ENTER to exit.                                        ║")
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
            Console.ReadLine()

        End Using

    End Sub

End Module
