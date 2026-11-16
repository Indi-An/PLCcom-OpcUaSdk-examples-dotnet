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
' PLCcom OPC UA PubSub SDK - Workshop 43: Key Server (Security Key Service, SKS)
'
' In Workshop 41 and 42 both sides read the same key from a file. That works
' for two programs. It does not work for fifty machines, and it never changes
' the key. A Security Key Service (SKS) solves both problems (OPC UA Part 14,
' 5.4.5 and 8.2 to 8.8):
'
'   * The SKS is an OPC UA server. It keeps the keys of one or more
'     SecurityGroups. Publishers and Subscribers log in and call the Method
'     GetSecurityKeys (8.3.2) to get the keys of THEIR group.
'   * The keys change by themselves every KeyLifetime. The SecurityTokenId
'     counts up by one each time. Everybody who asks gets the current key and
'     the next ones, so nobody has to ask at the moment of the change.
'   * Who may get which keys is a question of ROLES: the role
'     SecurityKeyServerAccess may fetch keys, SecurityKeyServerAdmin manages
'     the groups (8.8). The server checks the role on every call.
'
' SAFE BY DEFAULT - what this program does and why:
'   * Only ENCRYPTED endpoints (SignAndEncrypt). GetSecurityKeys is refused on
'     any other channel with Bad_SecurityModeInsufficient.
'   * NO anonymous access. There are two users, 'reader' (role Access) and
'     'admin' (role Admin). A user with no key service role gets no key and
'     does not even learn which groups exist.
'   * NO password and NO key in the source code. The passwords come from the
'     environment variables PLCCOM_SKS_READER_PASSWORD and
'     PLCCOM_SKS_ADMIN_PASSWORD, or you type them (at least 10 characters).
'     The private key of the server certificate is protected by
'     PLCCOM_PKI_PASSWORD, or you type that too.
'   * Keys never appear on the screen or in a log. This program shows WHO
'     fetched WHAT (user, group, token number, result), never the key.
'   * The server listens on this computer only (127.0.0.1). To open it to the
'     network, change BaseAddresses on purpose, and put a firewall rule on it.
'
' TRUST: a client certificate is accepted only after YOU move it into the
' trusted folder. The first time a client connects, its certificate is filed
' in pki/rejected/certs and the client is refused. Compare the thumbprint,
' then move the file to pki/trusted/certs and let the client connect again.
'
' KEYS ARE KEPT IN MEMORY ONLY. When this program ends, the keys are gone, and
' after a restart the numbering starts again. The SDK can give you the state
' of a group to keep it in an encrypted store of your own (ExportState); this
' workshop leaves that out.
'
' What you will learn:
'   * How to put a Security Key Service on an OPC UA server (UaSksServer)
'   * How to create a SecurityGroup with a short key lifetime
'   * How roles and the encrypted channel protect the keys
'   * How to see who fetched keys (the KeysRetrieved event)
'
' Run Workshop 44 (Publisher) and 45 (Subscriber); they fetch their keys here.
' ==============================================================================

Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text
Imports System.Threading
Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.PubSub.Sdk
Imports PLCcom.Opc.Ua.Server
Imports PLCcom.Opc.Ua.Server.Sdk

Module Program

    ' The address of the key server and the name of the group the Workshops 44 and 45 use.
    Private Const Endpoint As String = "opc.tcp://127.0.0.1:48440"
    Private Const GroupId As String = "WorkshopGroup"

    ' A short lifetime so you can watch the keys change: every 30 seconds the token goes up by one.
    ' In real life this is hours or days.
    Private ReadOnly KeyLifetime As TimeSpan = TimeSpan.FromSeconds(30)

    Sub Main(args As String())
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 43                      ║")
        Console.WriteLine("║  Key Server (Security Key Service)                           ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Hands out the keys of the SecurityGroup 'WorkshopGroup'.    ║")
        Console.WriteLine("║  Encrypted channel only, no anonymous access.                ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Address: opc.tcp://127.0.0.1:48440  (this computer only)    ║")
        Console.WriteLine("║  Start Workshop 44 (Publisher) and 45 (Subscriber).          ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Try
            ' Important !!!!!!!!!!!!!!!!!!
            ' Enter your Username + Serial here! Please note: with blank fields the library runs
            ' for 15 minutes during a debug session. Both values can also come
            ' from configuration or an environment variable.
            ' Free trial license (14 days, uninterrupted): https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-download/
            Dim LicenseUserName As String = ""
            Dim LicenseSerial As String = ""
            ' Enter your license here, or set the environment variables PLCCOM_OPCUA_LICENSE_USER and PLCCOM_OPCUA_LICENSE_SERIAL.
            If LicenseUserName.Length = 0 Then LicenseUserName = If(System.Environment.GetEnvironmentVariable("PLCCOM_OPCUA_LICENSE_USER"), "")
            If LicenseSerial.Length = 0 Then LicenseSerial = If(System.Environment.GetEnvironmentVariable("PLCCOM_OPCUA_LICENSE_SERIAL"), "")

            ' -- Step 1: The secrets - from the environment or from you -------------
            Dim readerPassword As String = SecretFrom("PLCCOM_SKS_READER_PASSWORD", "Password for the user 'reader' (fetches keys)")
            Dim adminPassword As String = SecretFrom("PLCCOM_SKS_ADMIN_PASSWORD", "Password for the user 'admin' (manages groups)")
            Dim pkiPassword As String = SecretFrom("PLCCOM_PKI_PASSWORD", "Password that protects the private key of the server certificate")
            Console.WriteLine()

            ' -- Step 2: Configure the OPC UA server --------------------------------
            ' A Security Key Service lives on an ordinary OPC UA server of this SDK.
            Dim config As New UaServerConfiguration With {
                .ApplicationName = "PLCcom Workshop 43 - Key Server",
                .ApplicationUri = "urn:localhost:PLCcom:Workshop:43",
                .ProductUri = "https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-overview/",
                .NamespaceUri = "http://indi-an.com/opcua/workshop/key-server",
                .BaseAddresses = New List(Of String) From {Endpoint},
                .EndpointHostMode = EndpointHostMode.AsConfigured,
                .AutoAcceptUntrustedCertificates = False,
                .RegisterWithDiscoveryServer = False,
                .MaxSessionCount = 50,
                .ShutdownDelay = 2
            }

            ' ONLY encrypted endpoints. A key must never travel over a channel that is
            ' not encrypted. The service would refuse the call anyway; offering no weak
            ' endpoint at all means a client cannot even try.
            config.SecurityPolicies = UaServer.GetRecommendedSecurityPolicies() _
                .Where(Function(p) p.SecurityMode = MessageSecurityMode.SignAndEncrypt).ToList()

            ' NO Anonymous. Only a user with name and password can log in.
            config.UserTokenPolicies = New List(Of UserTokenPolicy) From {
                New UserTokenPolicy With {.TokenType = UserTokenType.UserName}
            }

            ' The private key of the server certificate is protected with a password.
            ' UaServerCertificate makes the certificate on the first start, in ./pki.
            Dim certificate As New UaServerCertificate("./pki", pkiPassword, "KeyServer", config.ApplicationUri, 365,
                                                       "Indi.An GmbH", UaServerCertificate.CertificateRole.Application)
            certificate.LoadWithResultInPlace()
            If Not certificate.IsReady OrElse Not certificate.CheckValidity() Then certificate.Build(True)
            config.SetCertificateStore(UaServerCertificateStore.Load("./pki", New List(Of UaServerCertificate) From {certificate}))

            ' -- Step 3: Create the server and its two users ------------------------
            ' The role decides what a user may do with the key service (Part 14, 8.8):
            '   UaSksRoles.Access  may fetch keys (GetSecurityKeys)
            '   UaSksRoles.Admin   may manage SecurityGroups, but gets no keys by that
            Using server As New UaServer(LicenseUserName, LicenseSerial)
                server.UserManager.AddUser("reader", readerPassword, UaSksRoles.Access)
                server.UserManager.AddUser("admin", adminPassword, UaSksRoles.Admin)

                Console.Write("  Starting the server ... ")
                server.Start(config)
                Console.WriteLine("OK")

                ' -- Step 4: Put the key service on the server ----------------------
                ' UaSksServer takes the standard object Server/PublishSubscribe of the server
                ' (the well-known NodeIds of Part 14), so every client that follows the
                ' standard finds GetSecurityKeys where it expects it.
                Using sks As New UaSksServer(LicenseUserName, LicenseSerial, server)

                    ' KeysRetrieved is raised for EVERY call of GetSecurityKeys, also for the
                    ' refused ones. It carries the user, the group, the token numbers and the
                    ' result - never a key.
                    AddHandler sks.KeysRetrieved, Sub(s, e)
                                                      Dim result As String = If(StatusCode.IsGood(e.Result), "OK", StatusCodes.GetBrowseName(e.Result.Code))
                                                      Console.WriteLine($"  [{e.TimeUtc.ToLocalTime():HH:mm:ss}] fetch  user={If(e.UserName, "-")}  group={e.SecurityGroupId}  " &
                                                                        $"token={e.FirstTokenId} (+{Math.Max(0, e.KeyCount - 1)} future)  -> {result}")
                                                  End Sub

                    ' One SecurityGroup. Parameters: name, SecurityPolicyUri (Nothing = the
                    ' default, Aes256-CTR), key lifetime, MaxFutureKeyCount (how many future keys
                    ' one answer carries at most), MaxPastKeyCount (how many old keys are kept
                    ' for late subscribers).
                    sks.AddSecurityGroup(GroupId, Nothing, KeyLifetime, 3UI, 3UI)

                    sks.Start()

                    Console.WriteLine("  Key service started.")
                    Console.WriteLine("  Group:    " & GroupId & "  (key lifetime " & KeyLifetime.TotalSeconds & " s)")
                    Console.WriteLine("  Address:  " & Endpoint)
                    Console.WriteLine("  Users:    reader (fetches keys), admin (manages groups)")
                    Console.WriteLine()
                    Console.WriteLine("  A client certificate must be trusted first: move it from")
                    Console.WriteLine("  pki/rejected/certs to pki/trusted/certs after you compared its thumbprint.")
                    Console.WriteLine()
                    Console.WriteLine("  Press ENTER to stop.")
                    Console.WriteLine()

                    ' -- Step 5: Show the numbering of the keys -----------------------
                    ' The group counts the token up each KeyLifetime, whether somebody asks or not.
                    Dim shown As UInteger = 0UI
                    While Not (Not Console.IsInputRedirected AndAlso Console.KeyAvailable)
                        Dim token As UInteger = sks.GetSecurityGroup(GroupId).CurrentTokenId
                        If token <> shown Then
                            Console.WriteLine($"  [{DateTime.Now:HH:mm:ss}] current token of '{GroupId}' is now {token}")
                            shown = token
                        End If
                        Thread.Sleep(500)
                    End While
                    Console.ReadKey(True)
                    Console.WriteLine()

                    ' -- Step 6: Stop -------------------------------------------------
                    sks.Stop()
                    server.Stop()
                    Console.WriteLine("  Key service stopped. The keys are gone with it.")
                End Using
            End Using

        Catch ex As Exception
            Console.WriteLine($"  Error: {ex.Message}")
        End Try

        Console.WriteLine()
        Console.WriteLine("  Press ENTER to exit.")
        Console.ReadLine()
    End Sub

    ' A secret comes from the environment variable, or you type it (the characters are not shown).
    ' At least 10 characters. Nothing is ever written to the screen or to a file.
    Private Function SecretFrom(variable As String, prompt As String) As String
        Dim value As String = Environment.GetEnvironmentVariable(variable)
        If Not String.IsNullOrEmpty(value) Then
            If value.Length < 10 Then Throw New InvalidOperationException(variable & " is shorter than 10 characters.")
            Return value
        End If

        While True
            Console.Write("  " & prompt & " (min. 10 characters): ")
            Dim typed As String = ReadHidden()
            If typed.Length >= 10 Then Return typed
            Console.WriteLine("  Too short.")
        End While
        Return Nothing
    End Function

    Private Function ReadHidden() As String
        Dim text As New StringBuilder()
        Dim key As ConsoleKeyInfo
        Do
            key = Console.ReadKey(True)
            If key.Key = ConsoleKey.Backspace AndAlso text.Length > 0 Then
                text.Remove(text.Length - 1, 1)
                Console.Write(vbBack & " " & vbBack)
            ElseIf key.Key <> ConsoleKey.Enter AndAlso key.Key <> ConsoleKey.Backspace Then
                text.Append(key.KeyChar)
                Console.Write("*")
            End If
        Loop While key.Key <> ConsoleKey.Enter
        Console.WriteLine()
        Return text.ToString()
    End Function

End Module
