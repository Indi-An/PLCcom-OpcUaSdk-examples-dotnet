Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Server.Sdk
Imports System
Imports System.Collections.Generic
Imports System.Threading

' ==============================================================================
' PLCcom OPC UA Server SDK - Workshop 11: Simple Server
'
' This is the starting point for all server workshops.
' It shows the minimal code needed to run an OPC UA server with a real
' address space that any OPC UA client can connect to and browse.
'
' What you will learn:
'   * How to configure and start an OPC UA server
'   * How to create a folder hierarchy in the address space
'   * How to create variables of different data types
'   * How to push value changes to subscribed clients
'
' Connect with any OPC UA client to:
'   opc.tcp://localhost:48410
' ==============================================================================

Module Program

    Sub Main(args As String())

        ' -- License -----------------------------------------------------------
        ' TODO: Submit your license information from your license e-mail
        Dim LicenseUserName As String = "<Enter your UserName here>"
        Dim LicenseSerial As String = "<Enter your Serial here>"

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 11: Simple Server       ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  This example creates a minimal OPC UA server with:          ║")
        Console.WriteLine("║  * Folder hierarchy (Plant -> Line1 -> Machine1)             ║")
        Console.WriteLine("║  * Scalar variables (Double, Int, Bool, String, DateTime)    ║")
        Console.WriteLine("║  * Array variable (Double[])                                 ║")
        Console.WriteLine("║  * Read-only variable (SerialNumber)                         ║")
        Console.WriteLine("║  * Continuous value push loop (1 second interval)            ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        ' -- Step 1: Configure the server --------------------------------------
        ' UaServerConfiguration holds all server settings.
        ' The most important ones are:
        '   ApplicationUri  - unique identifier for this server (used in certificates)
        '   BaseAddresses   - the endpoint URL clients connect to
        '   SecurityPolicies - which encryption algorithms to offer
        '   CertificateStorePath - where PKI certificates are stored (auto-created)
        Dim config As New UaServerConfiguration With {
            .ApplicationName = "PLCcom Workshop 11 - Simple Server",
            .ApplicationUri = "urn:localhost:PLCcom:Workshop:11",
            .ProductUri = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
            .BaseAddresses = New List(Of String) From {"opc.tcp://localhost:48410"},
            .SecurityPolicies = UaServer.GetRecommendedSecurityPolicies(),
            .UserTokenPolicies = New List(Of UserTokenPolicy) From {
                New UserTokenPolicy With {.TokenType = UserTokenType.Anonymous}
            },
            .CertificateStorePath = ".\pki"
        }

        ' -- Step 2: Create and start the server -------------------------------
        Using server As New UaServer(LicenseUserName, LicenseSerial)

            ' Accept all client certificates automatically (do NOT use this in production!)
            AddHandler server.CertificateValidation, Sub(sender, e) e.Accept = True

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
            Console.WriteLine($"  Endpoint: {config.BaseAddresses(0)}")
            Console.WriteLine()

            ' -- Step 3: Build the address space -------------------------------
            ' The address space is the tree of nodes that clients can browse.
            ' Folders organize the structure, Variables hold the actual data.
            ' All nodes created here are immediately visible to connected clients.

            ' Create a folder hierarchy: Objects -> Plant -> Line1 -> Machine1
            Dim plant = server.CreateFolder("Plant")
            Dim line1 = server.CreateFolder(plant, "Line1")
            Dim machine = server.CreateFolder(line1, "Machine1")

            ' Create scalar variables - each has a specific OPC UA data type
            Dim temperature = server.CreateVariable(Of Double)(machine, "Temperature", initialValue:=21.5)
            Dim pressure = server.CreateVariable(Of Single)(machine, "Pressure", initialValue:=1.013F)
            Dim rpm = server.CreateVariable(Of Integer)(machine, "RPM", initialValue:=1500)
            Dim running = server.CreateVariable(Of Boolean)(machine, "IsRunning", initialValue:=True)
            Dim status = server.CreateVariable(Of String)(machine, "Status", initialValue:="Idle")
            Dim lastUpdate = server.CreateVariable(Of DateTime)(machine, "LastUpdate", initialValue:=DateTime.UtcNow)

            ' Read-only variable: clients can read but not write
            Dim serialNo = server.CreateVariable(Of String)(machine, "SerialNumber",
                initialValue:="SN-2025-001", readOnly:=True)

            ' Array variable: ValueRank is automatically set to OneDimension
            Dim setpoints = server.CreateArrayVariable(Of Double)(machine, "Setpoints",
                initialValue:=New Double() {20.0, 25.0, 30.0})

            Console.WriteLine("  Address Space:")
            Console.WriteLine("  Objects -> Plant -> Line1 -> Machine1")
            Console.WriteLine("    Temperature (Double)    = 21.5")
            Console.WriteLine("    Pressure (Float)        = 1.013")
            Console.WriteLine("    RPM (Int32)             = 1500")
            Console.WriteLine("    IsRunning (Boolean)     = true")
            Console.WriteLine("    Status (String)         = Idle")
            Console.WriteLine("    LastUpdate (DateTime)   = now")
            Console.WriteLine("    SerialNumber (String)   = SN-2025-001 [ReadOnly]")
            Console.WriteLine("    Setpoints (Double[])    = [20, 25, 30]")
            Console.WriteLine()

            ' -- Step 4: Connect a client and explore the address space --------
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║")
            Console.WriteLine("║  opc.tcp://localhost:48410                                   ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Try:                                                        ║")
            Console.WriteLine("║  * Browse Objects -> Plant -> Line1 -> Machine1              ║")
            Console.WriteLine("║  * Subscribe to Temperature, RPM, Status                     ║")
            Console.WriteLine("║  * Try writing to SerialNumber (should fail - ReadOnly)      ║")
            Console.WriteLine("║  * Check the DataType attribute of each variable             ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Press ENTER to start the value push loop.                   ║")
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
            Console.ReadLine()

            ' -- Step 5: Push value changes to subscribed clients --------------
            ' Setting variable.Value triggers a DataChange notification to all clients
            ' that have an active subscription on that variable.
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
                status.Value = If(CBool(running.Value), "Running", "Stopped")
                lastUpdate.Value = DateTime.UtcNow

                Console.Write($"{Chr(13)}  Cycle={cycle}  Temp={temperature.Value:F1}C  " &
                              $"P={pressure.Value:F3}bar  RPM={rpm.Value}  {status.Value,-8}")
                Thread.Sleep(1000)
            End While

        End Using

    End Sub

End Module
