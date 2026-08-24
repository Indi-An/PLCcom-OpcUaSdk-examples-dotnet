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
' PLCcom OPC UA Server SDK - Workshop 12: User Authentication
'
' Workshop 11 allowed anonymous access - anyone could connect and write values.
' In production, you need to control who can connect and what they can do.
'
' OPC UA supports three authentication methods:
'   Anonymous   - no login required (disabled in this example)
'   UserName    - classic username + password
'   Certificate - X.509 client certificate (machine-to-machine)
'
' Each authenticated user is assigned one or more roles that control access:
'   Engineer  - full access (read, write, browse, call methods)
'   Operator  - read + write, no configuration changes
'   Observer  - read-only (writes are rejected with BadUserAccessDenied)
'
' Test scenario:
'   1. Try connecting without credentials -> rejected
'   2. Connect as viewer/viewer123 -> can read, cannot write
'   3. Connect as operator/operator123 -> can read and write
'   4. Connect as admin/admin123 -> full access
'
' Connect with any OPC UA client to: opc.tcp://localhost:48410
' ==============================================================================

Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Server
Imports PLCcom.Opc.Ua.Server.Sdk
Imports System
Imports System.Collections.Generic
Imports System.Reflection
Module Program

    Sub Main(args As String())

        ' Important !!!!!!!!!!!!!!!!!!
        ' Enter your Username + Serial here! Please note: with blank fields the library runs
        ' for 15 minutes during a debug session. Both values can also come
        ' from configuration or an environment variable.
        ' Free trial license (14 days, uninterrupted): https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-download/
        Dim LicenseUserName As String = ""
        Dim LicenseSerial As String = ""

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 12: User Authentication ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Workshop 11 allowed anonymous access - anyone could write.  ║")
        Console.WriteLine("║  This example requires authentication and assigns roles:     ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║    admin    / admin123    -> Engineer  (full access)         ║")
        Console.WriteLine("║    operator / operator123 -> Operator  (read + write)        ║")
        Console.WriteLine("║    viewer   / viewer123   -> Observer  (read-only)           ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Anonymous access is disabled - you MUST log in.             ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        ' All server settings are defined in CreateConfig() below.
        Dim config = CreateConfig()
        PrintConfig(config)

        Using server As New UaServer(LicenseUserName, LicenseSerial)

            ' OPC UA defines well-known roles (Part 18). Each user is assigned one or more:
            '
            '   Role.Observer   - intended for read-only access (browse, read, subscribe)
            '   Role.Operator   - intended for read + write + method calls
            '   Role.Engineer   - intended for full access including configuration
            '   Role.Supervisor - intended for read + method calls (no write)
            '
            '   Role.AuthenticatedUser - any successfully authenticated user,
            '                            regardless of username or credentials
            '   Role.Anonymous         - always assigned, even without login
            '
            ' IMPORTANT: Roles are labels only. The OPC UA stack does NOT enforce
            ' permissions automatically unless RolePermissions are explicitly set
            ' on each node via SetRolePermissions() - see Step 4 below.
            ' Without SetRolePermissions(), all authenticated users have identical
            ' access regardless of their assigned role.
            server.AddUser("admin",    "admin123",    Role.Engineer)
            server.AddUser("operator", "operator123", Role.Operator)
            server.AddUser("viewer",   "viewer123",   Role.Observer)

            Console.WriteLine("-- Users --------------------------------------------------------")
            Console.WriteLine("  admin    / admin123    -> Engineer  (full access)")
            Console.WriteLine("  operator / operator123 -> Operator  (read + write)")
            Console.WriteLine("  viewer   / viewer123   -> Observer  (read-only)")
            Console.WriteLine()


            ' Accept all client certificates automatically.
            ' WARNING: Do Not use this in production! Either implement your own validation
            ' logic here (inspect e.Certificate And e.Error, then set e.Accept = true Or false),
            ' Or remove this handler entirely -- the SDK will then automatically validate
            ' certificates against the PKI trust store (pki/trusted/certs/).
            AddHandler server.CertificateValidation, Sub(s, e)
                                                         Console.WriteLine($"  [CERT] {e.Certificate.Subject} -> Accepted")
                                                         e.Accept = True
                                                     End Sub

            ' Accept all client certificates automatically.
            ' WARNING: Do Not use this in production! Either implement your own validation
            ' logic here (inspect e.Certificate And e.Error, then set e.Accept = true Or false),
            ' Or remove this handler entirely -- the SDK will then automatically validate
            ' certificates against the PKI trust store (pki/trusted/certs/).
            AddHandler server.UserManager.CertificateValidation, Sub(s, e)
                                                                     Console.WriteLine($"  [USER CERT] {e.Certificate.Subject} -> Accepted")
                                                                     e.Accept = True
                                                                 End Sub

            AddHandler server.SessionCreated, Sub(s, e)
                                                  Console.WriteLine($"  [SESSION+] {If(e.SessionName, "unknown")} from {If(e.ClientUri, "unknown")}")
                                              End Sub
            AddHandler server.SessionClosed, Sub(s, e)
                                                 Console.WriteLine($"  [SESSION-] {If(e.SessionName, "unknown")}")
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

            ' ======================================================================
            ' Step 4: Build address space with role-based permissions
            ' ======================================================================
            ' permissions parameter activates OPC UA role enforcement directly on creation.
            ' Without permissions, all authenticated users have identical access.
            '
            ' AllowRead()      - grants Browse + Read + Subscribe
            ' AllowReadWrite() - grants Browse + Read + Write + Subscribe + Call
            ' AllowAll()       - grants all permissions
            Dim rolePermissions = New UaRolePermissions() _
                .AllowRead(Role.Observer) _
                .AllowReadWrite(Role.Operator) _
                .AllowAll(Role.Engineer)

            Dim plant = server.CreateFolder("Plant", UaRolePermissions.WITHOUT_RESTRICTIONS)
            Dim temp = server.CreateVariable(Of Double)(plant, "Temperature", rolePermissions, initialValue:=22.0)
            Dim rpm = server.CreateVariable(Of Integer)(plant, "RPM", rolePermissions, initialValue:=1500)

            ' Observer may browse Reset but not call it - AllowRead grants Browse without Call
            Dim resetMethodId As NodeId = server.CreateMethod(plant, "Reset",
                Function(session, context, objectId, inputArgs, outputArgs)
                    temp.Value = 22.0
                    rpm.Value = 1500
                    Console.WriteLine($"  << Reset called")
                    Return ServiceResult.Good
                End Function,
                rolePermissions)

            Console.WriteLine("-- Address space ------------------------------------------------")
            Console.WriteLine($"  Double  {temp.Path,-40} {temp.NodeId}  = 22.0")
            Console.WriteLine($"  Int32   {rpm.Path,-40} {rpm.NodeId}  = 1500")
            Console.WriteLine($"  Method  Objects.Plant.Reset                      {resetMethodId}  (Operator + Engineer only)")
            Console.WriteLine()

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running - authentication required.                ║")
            Console.WriteLine("║  Endpoint: opc.tcp://localhost:48410                         ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Test role-based access:                                     ║")
            Console.WriteLine("║  * Connect without credentials        -> rejected            ║")
            Console.WriteLine("║  * viewer/viewer123 -> read OK, write -> BadUserAccessDenied ║")
            Console.WriteLine("║  * operator/operator123 -> read + write OK                   ║")
            Console.WriteLine("║  * admin/admin123       -> full access                       ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Press ENTER to exit.                                        ║")
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
            Console.ReadLine()

        End Using

    End Sub

    ' ==========================================================================
    ' Helper: CreateConfig
    ' ==========================================================================
    ' Returns the server configuration. All available options are listed here.
    ' IMPORTANT: Anonymous is NOT in UserTokenPolicies - clients must log in.
    ' Users and their roles are added via server.AddUser() in Main.
    Private Function CreateConfig() As UaServerConfiguration
        Dim cfg As New UaServerConfiguration
        cfg.ApplicationName = "PLCcom Workshop 12 - User Authentication"
        cfg.ApplicationUri  = "urn:localhost:PLCcom:Workshop:12"
        cfg.ProductUri      = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/"
        cfg.NamespaceUri    = "http://indi-an.com/opcua/workshop/user-authentication"
        cfg.ManufacturerName = "My Company GmbH"
        cfg.ProductName      = "My OPC UA Server"
        cfg.SoftwareVersion  = "1.0.0"
        cfg.BuildNumber      = "42"
        cfg.BaseAddresses = New List(Of String) From {"opc.tcp://localhost:48410", "opc.https://localhost:48411"}
        cfg.SecurityPolicies = UaServer.GetRecommendedSecurityPolicies()
        cfg.UserTokenPolicies = New List(Of UserTokenPolicy) From {
            New UserTokenPolicy With {.TokenType = UserTokenType.UserName},
            New UserTokenPolicy With {.TokenType = UserTokenType.Certificate}
        }
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
        cfg.MaxNodesPerRead                      = 1000
        cfg.MaxNodesPerWrite                     = 1000
        cfg.MaxNodesPerBrowse                    = 1000
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