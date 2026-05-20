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
' PLCcom OPC UA Server SDK - Workshop 21: Alarm Conditions
'
' OPC UA Alarms & Conditions (Part 9) extends the event model with stateful
' alarms that clients can acknowledge and confirm.
'
' This workshop demonstrates all alarm types that OPC UA supports:
'
'   AlarmConditionType      - General alarm (active/inactive, ack/confirm)
'   ExclusiveLimitAlarmType - Limit alarm with levels: Low / High / HighHigh
'   DiscreteAlarmType       - Alarm triggered by a discrete (boolean) state
'   DialogConditionType     - Dialog asking the operator to choose a response
'
' Each type maps to a filter option in the client workshops (31/32/33):
'   Client filter "3 - Alarms"       -> AlarmConditionType
'   Client filter "4 - Limit alarms" -> ExclusiveLimitAlarmType
'   Client filter "5 - Discrete"     -> DiscreteAlarmType
'   Client filter "2 - Dialogs"      -> DialogConditionType
'   Client filter "1 - All"          -> all of the above
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

        'TODO
        'Submit your license information from your license e-mail
        Dim LicenseUserName As String = "<Enter your UserName here>"
        Dim LicenseSerial As String = "<Enter your Serial here>"

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 21: Alarm Conditions    ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Demonstrates all OPC UA alarm types:                        ║")
        Console.WriteLine("║  * AlarmConditionType     - general alarm (ack/confirm)      ║")
        Console.WriteLine("║  * ExclusiveLimitAlarmType - limit levels Low/High/HighHigh  ║")
        Console.WriteLine("║  * DiscreteAlarmType      - boolean state alarm              ║")
        Console.WriteLine("║  * DialogConditionType    - operator response dialog         ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Dim config As New UaServerConfiguration With {
            .ApplicationName   = "PLCcom Workshop 21 - Alarm Conditions",
            .ApplicationUri    = "urn:localhost:PLCcom:Workshop:21",
            .ProductUri        = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
            .BaseAddresses     = New List(Of String) From {"opc.tcp://localhost:48410", "opc.https://localhost:48411"},
            .SecurityPolicies  = UaServer.GetRecommendedSecurityPolicies(),
            .UserTokenPolicies = New List(Of UserTokenPolicy) From {New UserTokenPolicy With {.TokenType = UserTokenType.Anonymous}}
        }

        Using server As New UaServer(LicenseUserName, LicenseSerial)

            ' Accept all client certificates automatically.
            ' WARNING: Do Not use this in production! Either implement your own validation
            ' logic here (inspect e.Certificate And e.Error, then set e.Accept = true Or false),
            ' Or remove this handler entirely -- the SDK will then automatically validate
            ' certificates against the PKI trust store (pki/trusted/certs/).
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

            ' -- Address space -----------------------------------------------------
            Dim plant = server.CreateFolder("Plant", UaRolePermissions.WITHOUT_RESTRICTIONS)
            Dim reactor = server.CreateFolder(plant, "Reactor", UaRolePermissions.WITHOUT_RESTRICTIONS)
            server.EnableEvents(reactor)

            ' Process variables
            Dim temperature = server.CreateVariable(Of Double)(reactor, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=25.0)
            Dim pressure = server.CreateVariable(Of Double)(reactor, "Pressure", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=1.0)
            Dim pumpRunning = server.CreateVariable(Of Boolean)(reactor, "PumpRunning", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=True)

            temperature.SetEURange(0, 200)
            temperature.SetEngineeringUnits("C")
            pressure.SetEURange(0, 10)
            pressure.SetEngineeringUnits("bar")

            ' -- Alarm type 1: AlarmConditionType (general alarm) ------------------
            ' Triggered when temperature exceeds 80C (hysteresis: off at 70C)
            Dim tempAlarm = server.CreateAlarm(reactor, "TemperatureHighAlarm")

            ' -- Alarm type 2: ExclusiveLimitAlarmType (limit levels) --------------
            ' Pressure with two escalating levels: High (> 6 bar), HighHigh (> 8 bar)
            Dim pressLimitAlarm = server.CreateLimitAlarm(reactor, "PressureLimitAlarm")

            ' -- Alarm type 3: DiscreteAlarmType (boolean state) ------------------
            ' Triggered when the pump stops unexpectedly
            Dim pumpAlarm = server.CreateDiscreteAlarm(reactor, "PumpFailureAlarm")

            ' -- Alarm type 4: DialogConditionType (operator response) ------------
            ' Periodically asks the operator to confirm a maintenance check
            Dim maintenanceDialog = server.CreateDialog(reactor, "MaintenanceDialog",
                prompt:="Scheduled maintenance check required. Confirm to proceed.",
                options:=New String() {"Confirm", "Postpone 1h", "Postpone 4h"})

            Console.WriteLine("  Reactor alarms:")
            Console.WriteLine("    [AlarmCondition]      TemperatureHighAlarm  - active when T > 80C")
            Console.WriteLine("    [ExclusiveLimitAlarm] PressureLimitAlarm    - High > 6bar, HighHigh > 8bar")
            Console.WriteLine("    [DiscreteAlarm]       PumpFailureAlarm      - active when pump stops")
            Console.WriteLine("    [DialogCondition]     MaintenanceDialog     - every 30s operator prompt")
            Console.WriteLine()

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running. Connect with any OPC UA client to:       ║")
            Console.WriteLine("║  opc.tcp://localhost:48410                                   ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Press ENTER to start the simulation.                        ║")
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
            Console.ReadLine()

            Console.WriteLine("Simulating... (CTRL+C to exit)")
            Console.WriteLine()

            Dim rng As New Random()
            Dim tempActive As Boolean = False
            Dim pressHigh As Boolean = False
            Dim pressHH As Boolean = False
            Dim pumpActive As Boolean = False
            Dim dialogActive As Boolean = False
            Dim tick As Integer = 0

            While True
                tick += 1

                ' -- Simulate process values ---------------------------------------
                Dim t As Double = 50.0 + Math.Sin(DateTime.UtcNow.Ticks * 0.0000001) * 40.0 + rng.NextDouble() * 5.0
                Dim p As Double = 1.0 + (t - 50.0) / 20.0 + rng.NextDouble() * 0.5
                Dim pump As Boolean = (tick Mod 20 <> 0)

                temperature.Value = Math.Round(t, 1)
                pressure.Value = Math.Round(p, 2)
                pumpRunning.Value = pump

                ' -- AlarmConditionType: temperature high alarm --------------------
                If t > 80.0 AndAlso Not tempActive Then
                    tempAlarm.Activate($"Temperature HIGH: {t:F1}C", EventSeverity.High)
                    tempActive = True
                    Console.WriteLine($"  [AlarmCondition  ON ] Temperature = {t:F1}C")
                ElseIf t < 70.0 AndAlso tempActive Then
                    tempAlarm.Deactivate($"Temperature normal: {t:F1}C")
                    tempActive = False
                    Console.WriteLine($"  [AlarmCondition  OFF] Temperature = {t:F1}C")
                End If

                ' -- ExclusiveLimitAlarmType: pressure with escalating levels ------
                If p > 8.0 AndAlso Not pressHH Then
                    pressLimitAlarm.Activate(LimitAlarmStates.HighHigh, $"Pressure HIGHHIGH: {p:F2} bar", EventSeverity.High)
                    pressHH = True : pressHigh = True
                    Console.WriteLine($"  [LimitAlarm HighHigh] Pressure = {p:F2} bar")
                ElseIf p > 6.0 AndAlso Not pressHigh AndAlso Not pressHH Then
                    pressLimitAlarm.Activate(LimitAlarmStates.High, $"Pressure HIGH: {p:F2} bar", EventSeverity.MediumHigh)
                    pressHigh = True
                    Console.WriteLine($"  [LimitAlarm High    ] Pressure = {p:F2} bar")
                ElseIf p < 5.0 AndAlso (pressHigh OrElse pressHH) Then
                    pressLimitAlarm.Deactivate($"Pressure normal: {p:F2} bar")
                    pressHigh = False : pressHH = False
                    Console.WriteLine($"  [LimitAlarm OFF     ] Pressure = {p:F2} bar")
                End If

                ' -- DiscreteAlarmType: pump failure ------------------------------
                If Not pump AndAlso Not pumpActive Then
                    pumpAlarm.Activate("Pump stopped unexpectedly", EventSeverity.High)
                    pumpActive = True
                    Console.WriteLine("  [DiscreteAlarm   ON ] Pump stopped")
                ElseIf pump AndAlso pumpActive Then
                    pumpAlarm.Deactivate("Pump running normally")
                    pumpActive = False
                    Console.WriteLine("  [DiscreteAlarm   OFF] Pump running")
                End If

                ' -- DialogConditionType: maintenance prompt every 30 ticks -------
                If tick Mod 30 = 0 AndAlso Not dialogActive Then
                    maintenanceDialog.Activate("Scheduled maintenance check required. Confirm to proceed.", EventSeverity.Medium)
                    dialogActive = True
                    Console.WriteLine("  [Dialog          ON ] Maintenance check requested")
                ElseIf dialogActive AndAlso tick Mod 30 = 5 Then
                    maintenanceDialog.Respond(0)
                    dialogActive = False
                    Console.WriteLine("  [Dialog          OFF] Operator confirmed maintenance")
                End If

                Console.Write($"{vbCr}  T={temperature.Value:F1}C{If(tempActive, "!", " ")}  " &
                              $"P={pressure.Value:F2}bar{If(pressHH, "!!", If(pressHigh, "! ", "  "))}  " &
                              $"Pump={pumpRunning.Value}  ")

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
        cfg.ApplicationName = "PLCcom Workshop 21 - Alarm Conditions"
        cfg.ApplicationUri  = "urn:localhost:PLCcom:Workshop:21"
        cfg.ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/"
        cfg.NamespaceUri    = "http://indi-an.com/opcua/workshop/alarm-conditions"

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