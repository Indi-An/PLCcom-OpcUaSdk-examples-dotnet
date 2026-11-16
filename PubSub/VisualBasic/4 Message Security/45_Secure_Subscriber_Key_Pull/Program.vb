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
' PLCcom OPC UA PubSub SDK - Workshop 45: Secure Subscriber, keys from the Key Server
'
' The receiving side of Workshop 44. Like the publisher, the subscriber logs in
' at the Key Server (Workshop 43), fetches the keys of the SecurityGroup
' 'WorkshopGroup' with GetSecurityKeys, and follows every change of the key.
'
' WHY THE SUBSCRIBER ASKS TOO:
'   Publisher and Subscriber never exchange a key with each other. Both ask the
'   key server for the keys of their group, so the key server is the one place
'   that decides who is in the group (OPC UA Part 14, 5.4.5.2). Take the role
'   'SecurityKeyServerAccess' away from a user, and from the next fetch on this
'   user can no longer join. Call ForceKeyRotation on the key server (any OPC UA
'   client with the role Admin can), and every member gets the new key without a
'   visit to any machine.
'
' OVERLAP:
'   The publisher switches to a new key at its own moment. For a short time the
'   subscriber sees messages with the previous or the next key (network delay,
'   different clocks). The key runtime keeps both ready, so no message is lost
'   at the change. You can watch the token number go up in the output.
'
' WHAT IF THE SUBSCRIBER CANNOT ASK?
'   A device without an OPC UA client cannot call GetSecurityKeys. For such
'   devices the key server can PUSH the keys instead: it calls the Method
'   SetSecurityKeys (Part 14, 9.1.3.3) on the device, which then needs an OPC UA
'   server of its own. In the SDK: UaSksServer.AddPushTarget on the key server
'   side and UaSksSetSecurityKeysReceiver on the device. This workshop uses the
'   normal way (pull), which is what an application with an OPC UA client does.
'
' TRY THIS:
'   * Start 43, 44 and 45 and watch the token change every 30 seconds.
'   * Start with the user 'admin' instead of 'reader' (change the code below):
'     the key server answers Bad_UserAccessDenied - an administrator is not a
'     member of the group by that.
'   * Stop Workshop 44: the subscriber shows nothing new, and no error.
'
' What you will learn:
'   * How a subscriber follows keys that change
'   * Why the key server is the one place that controls the membership
'   * What push is for (and where to look for it)
' ==============================================================================

Imports System
Imports System.Linq
Imports System.Text
Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Client.Sdk
Imports PLCcom.Opc.Ua.PubSub.Encoding.Security
Imports PLCcom.Opc.Ua.PubSub.Sdk

Module Program

    Private Const KeyServerUrl As String = "opc.tcp://127.0.0.1:48440"
    Private Const GroupId As String = "WorkshopGroup"

    ' The thumbprint of the key server certificate you answered 'y' for.
    Private s_trustedThumbprint As String

    Sub Main(args As String())
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 45                      ║")
        Console.WriteLine("║  Secure Subscriber, keys from the Key Server                 ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Fetches its key from Workshop 43 and follows the changes.   ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Key server:  opc.tcp://127.0.0.1:48440                      ║")
        Console.WriteLine("║  Listening on: opc.udp://localhost:4840                      ║")
        Console.WriteLine("║  Start Workshop 44 (Publisher) to send data.                 ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Dim keyClient As UaClient = Nothing
        Dim scheduler As UaSksKeyRefreshScheduler = Nothing
        Try
            Dim LicenseUserName As String = ""
            Dim LicenseSerial As String = ""
            If LicenseUserName.Length = 0 Then LicenseUserName = If(System.Environment.GetEnvironmentVariable("PLCCOM_OPCUA_LICENSE_USER"), "")
            If LicenseSerial.Length = 0 Then LicenseSerial = If(System.Environment.GetEnvironmentVariable("PLCCOM_OPCUA_LICENSE_SERIAL"), "")

            ' -- Step 1: Log in at the key server (encrypted channel, user 'reader') --
            Dim readerPassword As String = SecretFrom("PLCCOM_SKS_READER_PASSWORD", "Password of the user 'reader'")
            Dim pkiPassword As String = SecretFrom("PLCCOM_PKI_PASSWORD", "Password for the private key of this program's certificate")
            Console.WriteLine()
            keyClient = ConnectToKeyServer(LicenseUserName, LicenseSerial, readerPassword, pkiPassword)
            Console.WriteLine("  Connected to the key server (SignAndEncrypt).")

            ' -- Step 2: Fetch the first keys ------------------------------------------
            Dim sks As UaSksClient = UaSksClient.PublishSubscribe(New UaClientSksMethodCaller(keyClient))
            Dim request As UaSksGetSecurityKeysRequest = UaSksGetSecurityKeysRequest.Current(GroupId, 2)
            Dim first As UadpSecurityKeyUpdate = sks.GetSecurityKeys(request)
            Console.WriteLine($"  Keys received for '{GroupId}': first token {first.FirstTokenId}, {first.FutureKeys.Count} future key(s),")
            Console.WriteLine($"  key lifetime {first.KeyLifetimeMillis / 1000.0:F0} s.")
            Console.WriteLine()

            ' -- Step 3: Keep the keys fresh -------------------------------------------
            Dim runtime = UaUadpSecurityKeyRuntime.FromUpdate(first)
            Dim security As UaUadpSecurityConfiguration = UaUadpSecurityConfiguration _
                .WithKeySequence(first.Sequence) _
                .SecurityMode(UaUadpSecurityMode.SignAndEncrypt) _
                .Build()
            scheduler = New UaSksKeyRefreshScheduler(sks, runtime, request)
            scheduler.Start(1000, Sub(ex) Console.WriteLine("  Key refresh failed: " & ex.Message))

            ' -- Step 4: The subscriber, as in Workshop 42, with the runtime -----------
            Dim config = UaSubscriberConfiguration.Build("SecureTemperatureSubscriber") _
                .WithNetworkInterface(NetworkInterfaces.All) _
                .WithTransport(PubSubTransportMode.DirectUnicast, "opc.udp://localhost:4840") _
                .WithUadpSecurity(security, runtime) _
                .AddDataSetReader("opcua:Workshop44", "SecureTemperatures", Sub(ds)
                                                                                ds.AddField("Sensor1", BuiltInType.Double)
                                                                                ds.AddField("Sensor2", BuiltInType.Double)
                                                                                ds.AddField("Sensor3", BuiltInType.Double)
                                                                            End Sub)

            Using subscriber As New UaSubscriber(LicenseUserName, LicenseSerial, config)
                Console.WriteLine("  License: " & subscriber.LicenceMessage)

                Dim messageCount As Integer = 0
                Dim lastToken As Long = -1
                AddHandler subscriber.DataReceived, Sub(sender, e)
                                                        messageCount += 1
                                                        ' The number of the key in use - the key itself is never shown.
                                                        Dim token As Long = CurrentToken(runtime)
                                                        Dim change As String = If(token <> lastToken AndAlso lastToken >= 0, "   <-- new key", String.Empty)
                                                        lastToken = token
                                                        Dim keyFrame As String = If(e.IsKeyFrame, "[KEY]", "[DEL]")
                                                        Dim fieldStr As String = String.Join("  ", e.Fields.Select(Function(f) $"{f.Key}={f.Value.Value:F2}"))
                                                        Console.WriteLine($"  [{messageCount:D5}] token {token} {keyFrame} {e.DataSetName} | {fieldStr}{change}")
                                                    End Sub

                subscriber.Start()
                Console.WriteLine("  Secure subscriber started. Waiting for messages...")
                Console.WriteLine("  Press ENTER to stop.")
                Console.WriteLine()

                Console.ReadLine()

                subscriber.Stop()
                Console.WriteLine($"  Subscriber stopped. Received {messageCount} messages total.")
            End Using

        Catch ex As Exception
            Console.WriteLine($"  Error: {ex.Message}")
            ' Typical reasons: wrong password (the login is refused), a certificate that the
            ' key server has not trusted yet (look in its pki/rejected/certs), a user without
            ' the role SecurityKeyServerAccess (Bad_UserAccessDenied), a group that does not
            ' exist (Bad_NotFound).
        Finally
            If scheduler IsNot Nothing Then scheduler.Stop()
            If keyClient IsNot Nothing Then
                Try
                    keyClient.Disconnect()
                Catch
                End Try
            End If
        End Try

        Console.WriteLine()
        Console.WriteLine("  Press ENTER to exit.")
        Console.ReadLine()
    End Sub

    Private Function CurrentToken(runtime As UaUadpSecurityKeyRuntime) As Long
        Try
            Return runtime.GetCurrentPublisherKeyAtMillis(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).SecurityTokenId
        Catch
            Return -1   ' no usable key right now
        End Try
    End Function

    ' Connects to the key server over a SignAndEncrypt endpoint, as the user 'reader'.
    Private Function ConnectToKeyServer(licenseUser As String, licenseSerial As String, readerPassword As String, pkiPassword As String) As UaClient
        Dim endpoints As EndpointDescriptionCollection = UaClient.GetEndpoints(New Uri(KeyServerUrl), certificateValidator:=AddressOf AskForServerCertificate)
        Dim endpoint As EndpointDescription = UaClient.SortEndpointsBySecurityLevel(endpoints) _
            .FirstOrDefault(Function(e) e.SecurityMode = MessageSecurityMode.SignAndEncrypt)
        If endpoint Is Nothing Then Throw New InvalidOperationException("The key server offers no SignAndEncrypt endpoint. Is Workshop 43 running?")

        Dim alias_ As String = System.Reflection.Assembly.GetEntryAssembly().GetName().Name
        Dim session As SessionConfiguration = SessionConfiguration.Build(alias_, endpoint)
        Dim application As UaClientCertificate = UaClientCertificate.Load("./pki", alias_, pkiPassword)
        If application Is Nothing OrElse Not application.CheckValidity() Then
            application = New UaClientCertificate("./pki", pkiPassword, alias_, 365, "Indi.An GmbH").Build(True)
        End If
        session.SetInstanceCertificate(application)
        session.Identity = New UserIdentity("reader", readerPassword)

        Dim client As New UaClient(licenseUser, licenseSerial, session)
        AddHandler client.CertificateValidation, AddressOf AskForServerCertificate
        client.Connect()
        Return client
    End Function

    ' The key server's certificate: shown once, accepted only if you say so.
    Private Sub AskForServerCertificate(sender As CertificateValidator, e As CertificateValidationEventArgs)
        If s_trustedThumbprint IsNot Nothing AndAlso s_trustedThumbprint = e.Certificate.Thumbprint Then
            e.Accept = True
            Return
        End If
        Console.WriteLine()
        Console.WriteLine("  The key server presents this certificate:")
        Console.WriteLine("    Subject:    " & e.Certificate.Subject)
        Console.WriteLine("    Thumbprint: " & e.Certificate.Thumbprint)
        Console.Write("  Trust it? (y/N): ")
        Dim answer As String = Console.ReadLine()
        e.Accept = String.Equals(If(answer, "").Trim(), "y", StringComparison.OrdinalIgnoreCase)
        If e.Accept Then s_trustedThumbprint = e.Certificate.Thumbprint
    End Sub

    ' A secret comes from the environment variable, or you type it (the characters are not shown).
    Private Function SecretFrom(variable As String, prompt As String) As String
        Dim value As String = Environment.GetEnvironmentVariable(variable)
        If Not String.IsNullOrEmpty(value) Then Return value
        Console.Write("  " & prompt & ": ")
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
