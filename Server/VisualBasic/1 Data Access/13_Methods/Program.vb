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
' PLCcom OPC UA Server SDK - Workshop 13: Methods
'
' OPC UA Methods are callable functions in the server's address space.
' A client can invoke a method by sending a Call service request.
'
' This workshop demonstrates:
'   * How to create a method without arguments (Reset)
'   * How to create a method with input and output arguments (Add, Multiply)
'   * How to create a method that modifies server-side state (SetTemperature)
'
' Connect with any OPC UA client to: opc.tcp://localhost:48410
' ==============================================================================

Module Program

    Sub Main(args As String())

        Dim LicenseUserName As String = "<Enter your UserName here>"
        Dim LicenseSerial As String = "<Enter your Serial here>"

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 13: Methods             ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This example creates four methods:                          ║")
        Console.WriteLine("║    Reset()                   - resets CycleCount to 0        ║")
        Console.WriteLine("║    Add(A, B) -> Sum           - returns A + B                ║")
        Console.WriteLine("║    Multiply(A, B) -> Product  - returns A x B                ║")
        Console.WriteLine("║    SetTemperature(value)      - updates a server variable    ║")
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
            .NamespaceUri = "http://indi-an.com/opcua/workshop/methods",
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

            ' =================================================================
            ' Step 2: Create the address space with variables
            ' =================================================================
            Dim plant = server.CreateFolder("Plant")
            Dim machine = server.CreateFolder(plant, "Machine1")

            Dim counter = server.CreateVariable(Of Long)(machine, "CycleCount", initialValue:=0L)
            Dim temp = server.CreateVariable(Of Double)(machine, "Temperature", initialValue:=22.0)

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
                         End Function)

            ' -- Method 2: Add (two inputs, one output) ------------------------
            server.CreateMethod(machine, "Add",
                handler:=Function(ctx, method, objectId, inputArgs, outputArgs)
                             Dim a As Double = CDbl(inputArgs(0))
                             Dim b As Double = CDbl(inputArgs(1))
                             outputArgs(0) = a + b
                             Console.WriteLine($"  [METHOD] Add({a}, {b}) = {a + b}")
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
                             Console.WriteLine($"  [METHOD] Multiply({a}, {b}) = {a * b}")
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
            server.CreateMethod(machine, "SetTemperature",
                handler:=Function(ctx, method, objectId, inputArgs, outputArgs)
                             Dim newTemp As Double = CDbl(inputArgs(0))
                             temp.Value = newTemp
                             Console.WriteLine($"  [METHOD] SetTemperature({newTemp:F1}) -> Temperature updated")
                             Return ServiceResult.Good
                         End Function,
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

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running on: opc.tcp://localhost:48410             ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Try in UA Expert:                                           ║")
            Console.WriteLine("║  * Browse Objects -> Plant -> Machine1                       ║")
            Console.WriteLine("║  * Right-click Reset -> Call                                 ║")
            Console.WriteLine("║  * Right-click Add -> Call, enter A=10 and B=20              ║")
            Console.WriteLine("║  * Call SetTemperature(42.5) and watch Temperature change    ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Press ENTER to exit.                                        ║")
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
            Console.ReadLine()

        End Using

    End Sub

End Module
