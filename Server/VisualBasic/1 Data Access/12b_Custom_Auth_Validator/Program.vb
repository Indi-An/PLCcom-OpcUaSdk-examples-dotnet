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
' PLCcom OPC UA Server SDK - Workshop 12b: Custom Auth Validator
'
' Workshop 12a used the built-in user database (AddUser) and OPC UA RolePermissions
' to control access. This workshop demonstrates the alternative approach:
'
'   IUaCredentialValidator  - replaces username/password validation entirely.
'                             No AddUser() calls needed.
'
'   IUaPermissionValidator  - replaces the built-in RolePermissions enforcement.
'                             No SetRolePermissions() on nodes needed.
'                             Nodes are created with ALL_RESTRICTIONS by default.
'
' The same three users and the same access rules as Workshop 12a are implemented,
' but entirely in custom validator classes - no OPC UA role concepts involved.
'
' Users:
'   admin    / admin123    -> full access  (read, write, call)
'   operator / operator123 -> read + write + call
'   viewer   / viewer123   -> read only
'
' Connect with any OPC UA client to: opc.tcp://localhost:48410
' ==============================================================================

Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Server
Imports PLCcom.Opc.Ua.Server.Sdk
Imports System
Imports System.Collections.Generic
Imports System.Reflection
Imports System.Security.Cryptography.X509Certificates
Module Program

    Sub Main(args As String())

        Dim LicenseUserName As String = "<Enter your UserName here>"
        Dim LicenseSerial As String = "<Enter your Serial here>"

        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA Server SDK - Workshop 12b: Custom Validator   ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Same users and access rules as Workshop 12a, but using      ║")
        Console.WriteLine("║  IUaCredentialValidator and IUaPermissionValidator instead   ║")
        Console.WriteLine("║  of AddUser() and RolePermissions.                           ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║    admin    / admin123    -> full access                     ║")
        Console.WriteLine("║    operator / operator123 -> read + write + call             ║")
        Console.WriteLine("║    viewer   / viewer123   -> read only                       ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Anonymous access is disabled - you MUST log in.             ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Dim config = CreateConfig()
        PrintConfig(config)

        Using server As New UaServer(LicenseUserName, LicenseSerial)

            ' =================================================================
            ' Step 2: Assign custom validators
            ' =================================================================
            ' No AddUser() calls - authentication handled by CredentialValidator.
            ' No SetRolePermissions() on nodes - access handled by PermissionValidator.
            server.UserManager.CredentialValidator = New MyCredentialValidator()
            server.UserManager.PermissionValidator = New MyPermissionValidator()

            Console.WriteLine("-- Validators --------------------------------------------------")
            Console.WriteLine("  CredentialValidator : MyCredentialValidator (custom user/password check)")
            Console.WriteLine("  PermissionValidator : MyPermissionValidator (custom access control)")
            Console.WriteLine()

            ' =================================================================
            ' Step 3: Wire up events
            ' =================================================================

            ' Accept all client certificates automatically.
            ' WARNING: Do Not use this in production! Either implement your own validation
            ' logic here (inspect e.Certificate And e.Error, then set e.Accept = true Or false),
            ' Or remove this handler entirely -- the SDK will then automatically validate
            ' certificates against the PKI trust store (pki/trusted/certs/).
            AddHandler server.CertificateValidation, Sub(s, e)
                                                         Console.WriteLine($"  [CERT] {e.Certificate.Subject} -> Accepted")
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

            ' =================================================================
            ' Step 4: Start server and build address space
            ' =================================================================
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

            ' When using IUaPermissionValidator, nodes must be created WITHOUT_RESTRICTIONS.
            ' The PermissionValidator takes full control - the stack must not pre-filter
            ' via RolePermissions before ValidateRolePermissions is called.
            Dim plant = server.CreateFolder("Plant", UaRolePermissions.WITHOUT_RESTRICTIONS)
            Dim temp = server.CreateVariable(Of Double)(plant, "Temperature", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=22.0)
            Dim rpm = server.CreateVariable(Of Integer)(plant, "RPM", UaRolePermissions.WITHOUT_RESTRICTIONS, initialValue:=1500)

            Dim resetMethodId As NodeId = server.CreateMethod(plant, "Reset",
                Function(session, context, objectId, inputArgs, outputArgs)
                    temp.Value = 22.0
                    rpm.Value = 1500
                    Console.WriteLine($"  << Reset called")
                    Return ServiceResult.Good
                End Function,
                UaRolePermissions.WITHOUT_RESTRICTIONS)

            Console.WriteLine("-- Address space ------------------------------------------------")
            Console.WriteLine($"  Double  {temp.Path,-40} {temp.NodeId}  = 22.0")
            Console.WriteLine($"  Int32   {rpm.Path,-40} {rpm.NodeId}  = 1500")
            Console.WriteLine($"  Method  Objects.Plant.Reset                      {resetMethodId}")
            Console.WriteLine()

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
            Console.WriteLine("║  Server is running - authentication required.                ║")
            Console.WriteLine("║  Endpoint: opc.tcp://localhost:48410                         ║")
            Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Test custom validator access control:                       ║")
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
    ' Custom validator implementations
    ' ==========================================================================

    ''' <summary>
    ''' Replaces the built-in user database.
    ''' Validates username/password against a hardcoded user list.
    ''' In production this would query LDAP, Active Directory or a custom database.
    ''' </summary>
    Class MyCredentialValidator
        Implements IUaCredentialValidator

        Private Shared ReadOnly s_users As New Dictionary(Of String, String)(StringComparer.Ordinal) From {
            {"admin",    "admin123"},
            {"operator", "operator123"},
            {"viewer",   "viewer123"}
        }

        Public Function ValidateCredentials(userName As String, password As String) As Boolean _
            Implements IUaCredentialValidator.ValidateCredentials
            Dim expected As String = Nothing
            Dim ok As Boolean = s_users.TryGetValue(userName, expected) AndAlso expected = password
            Console.WriteLine($"  [AUTH] {userName} -> {If(ok, "accepted", "rejected")}")
            Return ok
        End Function

        Public Function ValidateCertificate(certificate As X509Certificate2) As Boolean _
            Implements IUaCredentialValidator.ValidateCertificate
            Console.WriteLine($"  [AUTH CERT] {certificate.Subject} -> accepted")
            Return True
        End Function
    End Class

    ''' <summary>
    ''' Replaces the built-in RolePermissions enforcement.
    ''' Implements the same access rules as Workshop 12a's UaRolePermissions setup,
    ''' but using plain username-based logic - no OPC UA role concepts needed.
    ''' </summary>
    Class MyPermissionValidator
        Implements IUaPermissionValidator

        Public Function ValidatePermission(session As UaSessionContext, node As UaNodeContext, check As UaPermissionCheck) As Boolean _
            Implements IUaPermissionValidator.ValidatePermission

            Dim user As String = session.UserName
            Dim allowed As Boolean

            If user = "admin" Then
                allowed = True
            ElseIf user = "operator" Then
                allowed = check <> UaPermissionCheck.HistoryWrite
            ElseIf user = "viewer" Then
                allowed = check = UaPermissionCheck.Browse OrElse
                          check = UaPermissionCheck.Read OrElse
                          check = UaPermissionCheck.Subscribe OrElse
                          check = UaPermissionCheck.ReadRolePermissions
            Else
                allowed = False
            End If

            Console.WriteLine($"  [PERM] {user,-10} {check,-25} {If(node.Path, node.NodeId.ToString()),-35} -> {If(allowed, "ALLOW", "DENY")}")
            Return allowed
        End Function
    End Class

    ' ==========================================================================
    ' Helper: CreateConfig
    ' ==========================================================================
    Private Function CreateConfig() As UaServerConfiguration
        Dim cfg As New UaServerConfiguration
        cfg.ApplicationName  = "PLCcom Workshop 12b - Custom Auth Validator"
        cfg.ApplicationUri   = "urn:localhost:PLCcom:Workshop:12b"
        cfg.ProductUri       = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/"
        cfg.NamespaceUri     = "http://indi-an.com/opcua/workshop/custom-auth-validator"
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