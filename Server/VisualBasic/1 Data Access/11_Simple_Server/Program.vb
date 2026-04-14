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

Module Program

    Sub Main(args As String())

        ' -- License -----------------------------------------------------------
        ' TODO: Replace with your license credentials from your license e-mail
        Dim LicenseUserName As String = "<Enter your UserName here>"
        Dim LicenseSerial As String = "<Enter your Serial here>"

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
        Dim config As New UaServerConfiguration With {
            .ApplicationName = "PLCcom Workshop 11 - Simple Server",
            .ApplicationUri = "urn:localhost:PLCcom:Workshop:11",
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
            .NamespaceUri = "http://indi-an.com/opcua/workshop/simple-server",
            .CertificateStorePath = ".\pki"
        }

        ' =====================================================================
        ' Step 2: Create the server and wire up events
        ' =====================================================================
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
            ' Step 3: Build the address space
            ' =================================================================
            Console.WriteLine("-- Building address space ----------------------------------------")

            Dim plant = server.CreateFolder("Plant")
            Dim line1 = server.CreateFolder(plant, "Line1")
            Dim machine = server.CreateFolder(line1, "Machine1")

            Console.WriteLine($"  Folder    {plant.Path,-40} {plant.NodeId}")
            Console.WriteLine($"  Folder    {line1.Path,-40} {line1.NodeId}")
            Console.WriteLine($"  Folder    {machine.Path,-40} {machine.NodeId}")

            Dim temperature = server.CreateVariable(Of Double)(machine, "Temperature", initialValue:=21.5)
            Dim pressure = server.CreateVariable(Of Single)(machine, "Pressure", initialValue:=1.013F)
            Dim rpm = server.CreateVariable(Of Integer)(machine, "RPM", initialValue:=1500)
            Dim running = server.CreateVariable(Of Boolean)(machine, "IsRunning", initialValue:=True)
            Dim status = server.CreateVariable(Of String)(machine, "Status", initialValue:="Idle")
            Dim lastUpdate = server.CreateVariable(Of DateTime)(machine, "LastUpdate", initialValue:=DateTime.UtcNow)

            Dim serialNo = server.CreateVariable(Of String)(machine, "SerialNumber",
                initialValue:="SN-2025-001", readOnly:=True)

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

End Module
