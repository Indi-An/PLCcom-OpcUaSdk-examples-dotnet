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
' PLCcom OPC UA PubSub SDK - Workshop 44: Secure Publisher, keys from the Key Server
'
' The publisher of Workshop 41, but without a key file: it logs in at the Key
' Server of Workshop 43 and fetches the key of its SecurityGroup with the
' Method GetSecurityKeys (OPC UA Part 14, 8.3.2; the publisher side is
' described in 5.4.5).
'
' HOW IT WORKS:
'   1. The publisher connects to the key server over an ENCRYPTED channel
'      (SignAndEncrypt) with the user 'reader'. The key server refuses every
'      other kind of channel.
'   2. It calls GetSecurityKeys for the group 'WorkshopGroup' with the
'      StartingTokenId 0 ("the current key") and asks for two future keys.
'   3. UaUadpSecurityKeyRuntime holds the keys and knows which one is current.
'   4. UaSksKeyRefreshScheduler fetches again before the keys run out
'      (Part 14 recommends half the KeyLifetime). When the key server changes the
'      key, the publisher switches to the new one by itself - you can watch the
'      token number go up in the output.
'
' NOTHING SECRET IN THE CODE:
'   The password of the user 'reader' comes from PLCCOM_SKS_READER_PASSWORD, the
'   password of the private key of this program's certificate from
'   PLCCOM_PKI_PASSWORD; or you type them. The output shows the token number
'   of the key in use, never the key.
'
' TRUST: the first time, the program shows you the certificate of the key server
' and asks whether to trust it. Compare the thumbprint with the one the key
' server's folder pki/own/certs holds before you answer 'y'. The key server in
' turn needs your certificate in its pki/trusted/certs (see Workshop 43).
'
' TRY THIS:
'   * Let it run for a minute: the token changes every 30 seconds.
'   * Start with a wrong password: the key server refuses the login.
'   * Stop Workshop 43 for a short moment: the publisher keeps sending with the
'     keys it has (it fetched future keys), and the refresh error shows in the
'     output.
'
' What you will learn:
'   * How to fetch keys with UaSksClient and the encrypted OPC UA channel
'   * How UaUadpSecurityKeyRuntime and UaSksKeyRefreshScheduler keep keys fresh
'   * How a publisher uses WithUadpSecurity(security, runtime)
'
' Run Workshop 45 (Secure Subscriber, keys from the Key Server) to receive the data.
' ==============================================================================

Imports System
Imports System.Linq
Imports System.Text
Imports System.Threading
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
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 44                      ║")
        Console.WriteLine("║  Secure Publisher, keys from the Key Server                  ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Fetches its key from Workshop 43 and follows the changes.   ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Key server: opc.tcp://127.0.0.1:48440                       ║")
        Console.WriteLine("║  Target:     opc.udp://localhost:4840                        ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Dim keyClient As UaClient = Nothing
        Dim scheduler As UaSksKeyRefreshScheduler = Nothing
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

            ' -- Step 1: Log in at the key server (encrypted channel, user 'reader') --
            Dim readerPassword As String = SecretFrom("PLCCOM_SKS_READER_PASSWORD", "Password of the user 'reader'")
            Dim pkiPassword As String = SecretFrom("PLCCOM_PKI_PASSWORD", "Password for the private key of this program's certificate")
            Console.WriteLine()
            keyClient = ConnectToKeyServer(LicenseUserName, LicenseSerial, readerPassword, pkiPassword)
            Console.WriteLine("  Connected to the key server (SignAndEncrypt).")

            ' -- Step 2: Fetch the first keys ------------------------------------------
            ' UaClientSksMethodCaller carries the Method call over the session.
            ' UaSksClient turns the answer into a UadpSecurityKeyUpdate.
            ' Current(group, 2): the current key, and two future keys.
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
            ' Every second the scheduler looks whether a fetch is due. A failed fetch is
            ' reported here and tried again; the keys you have keep working until they end.
            scheduler.Start(1000, Sub(ex) Console.WriteLine("  Key refresh failed: " & ex.Message))

            ' -- Step 4: The publisher, as in Workshop 41, with the runtime ------------
            Dim config = UaPublisherConfiguration.Build("SecureTemperaturePublisher", "opcua:Workshop44") _
                .WithNetworkInterface(NetworkInterfaces.All) _
                .WithTransport(PubSubTransportMode.DirectUnicast, "opc.udp://localhost:4840") _
                .WithUadpSecurity(security, runtime) _
                .WithPublishingInterval(1000) _
                .AddDataSet("SecureTemperatures", Sub(ds)
                                                      ds.AddField("Sensor1", New NodeId(1001UI, 2US))
                                                      ds.AddField("Sensor2", New NodeId(1002UI, 2US))
                                                      ds.AddField("Sensor3", New NodeId(1003UI, 2US))
                                                      ds.WithKeyFrameCount(10)
                                                      ds.WithInterval(1000)
                                                  End Sub)

            Using publisher As New UaPublisher(LicenseUserName, LicenseSerial, config)
                Console.WriteLine("  License: " & publisher.LicenceMessage)
                publisher.Start()
                Console.WriteLine("  Secure publisher started. Press ENTER to stop.")
                Console.WriteLine()

                Dim random As New Random()
                Dim counter As Integer = 0
                Dim lastToken As Long = -1
                While Not (Not Console.IsInputRedirected AndAlso Console.KeyAvailable)
                    counter += 1
                    Dim temp1 As Double = 20.0 + random.NextDouble() * 10.0
                    Dim temp2 As Double = 30.0 + random.NextDouble() * 5.0
                    Dim temp3 As Double = 40.0 + random.NextDouble() * 5.0
                    publisher.WriteValue("SecureTemperatures", "Sensor1", temp1)
                    publisher.WriteValue("SecureTemperatures", "Sensor2", temp2)
                    publisher.WriteValue("SecureTemperatures", "Sensor3", temp3)

                    ' The number of the key in use - the key itself is never shown.
                    Dim token As Long = CurrentToken(runtime)
                    Dim change As String = If(token <> lastToken AndAlso lastToken >= 0, "   <-- new key", String.Empty)
                    lastToken = token
                    Console.WriteLine($"  [{counter:D5}] token {token}  Sensor1={temp1:F2}°C  Sensor2={temp2:F2}°C  Sensor3={temp3:F2}°C{change}")
                    Thread.Sleep(1000)
                End While

                Console.ReadKey(True)
                Console.WriteLine()
                publisher.Stop()
                Console.WriteLine("  Publisher stopped.")
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
