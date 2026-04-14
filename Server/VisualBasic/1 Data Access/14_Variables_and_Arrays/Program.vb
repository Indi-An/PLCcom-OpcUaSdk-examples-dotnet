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
Imports System.Threading

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

        Dim config As New UaServerConfiguration With {
            .ApplicationName = "PLCcom Workshop 14 - Variables and Arrays",
            .ApplicationUri = "urn:localhost:PLCcom:Workshop:14",
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
            .NamespaceUri = "http://indi-an.com/opcua/workshop/variables-and-arrays",
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
            ' Part A: Scalar data types
            ' =================================================================
            Console.WriteLine("-- Part A: Scalar data types ------------------------------------")

            Dim scalars = server.CreateFolder("Scalars")

            Dim vBool = server.CreateVariable(Of Boolean)(scalars, "MyBool", True)
            Dim vByte = server.CreateVariable(Of Byte)(scalars, "MyByte", CByte(42))
            Dim vSByte = server.CreateVariable(Of SByte)(scalars, "MySByte", CSByte(-7))
            Dim vInt16 = server.CreateVariable(Of Short)(scalars, "MyInt16", CShort(-1000))
            Dim vUInt16 = server.CreateVariable(Of UShort)(scalars, "MyUInt16", CUShort(5000))
            Dim vInt32 = server.CreateVariable(Of Integer)(scalars, "MyInt32", 100000)
            Dim vUInt32 = server.CreateVariable(Of UInteger)(scalars, "MyUInt32", CUInt(200000))
            Dim vInt64 = server.CreateVariable(Of Long)(scalars, "MyInt64", 9876543210L)
            Dim vUInt64 = server.CreateVariable(Of ULong)(scalars, "MyUInt64", CULng(1234567890))
            Dim vFloat = server.CreateVariable(Of Single)(scalars, "MyFloat", 3.14F)
            Dim vDouble = server.CreateVariable(Of Double)(scalars, "MyDouble", 2.71828)
            Dim vString = server.CreateVariable(Of String)(scalars, "MyString", "Hello OPC UA")
            Dim vDateTime = server.CreateVariable(Of DateTime)(scalars, "MyDateTime", DateTime.UtcNow)
            Dim vGuid = server.CreateVariable(Of Guid)(scalars, "MyGuid", Guid.NewGuid())
            Dim vByteString = server.CreateVariable(Of Byte())(scalars, "MyByteString", New Byte() {&HDE, &HAD, &HBE, &HEF})

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

            Dim props = server.CreateFolder("Properties")

            Dim temperature = server.CreateVariable(Of Double)(props, "Temperature", 22.5)
            temperature.SetEURange(0, 100)
            temperature.SetEngineeringUnits("degC", "Degrees Celsius")

            Dim pressure = server.CreateVariable(Of Double)(props, "Pressure", 1.013)
            pressure.SetEURange(0, 10)
            pressure.SetEngineeringUnits("bar", "Bar")

            Dim speed = server.CreateVariable(Of Double)(props, "Speed", 1500.0)
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

            Dim callbacks = server.CreateFolder("Callbacks")

            Dim computed = server.CreateVariable(Of Double)(callbacks, "Computed", 0.0, readOnly:=True)
            computed.OnRead = Function(currentValue)
                                  Return Math.Round(temperature.Value * 1.8 + 32.0, 2)
                              End Function

            Dim validated = server.CreateVariable(Of Integer)(callbacks, "Validated", 50)
            validated.OnWrite = Function(newValue)
                                    If newValue < 0 OrElse newValue > 100 Then
                                        Console.WriteLine($"  !! Rejected write: {newValue} (must be 0..100)")
                                        Return False
                                    End If
                                    Return True
                                End Function

            Dim counter = server.CreateVariable(Of Integer)(callbacks, "Counter", 0, readOnly:=True)

            Console.WriteLine($"  {computed.Path,-45} OnRead -> Fahrenheit")
            Console.WriteLine($"  {validated.Path,-45} OnWrite -> reject if not 0..100")
            Console.WriteLine($"  {counter.Path,-45} [ReadOnly] server-incremented")
            Console.WriteLine()

            ' =================================================================
            ' Part D: Arrays and exposeElements
            ' =================================================================
            Console.WriteLine("-- Part D: Arrays and exposeElements ----------------------------")

            Dim arrays = server.CreateFolder("Arrays")

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

End Module
