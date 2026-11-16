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
' PLCcom OPC UA PubSub SDK - Workshop 41: Secure UADP Publisher (fixed key)
'
' Workshop 11 sends its data in the clear. This publisher signs and encrypts
' every NetworkMessage (OPC UA Part 14, 7.2.4.4 "UADP message security"), so a
' third party on the network sees only ciphertext and cannot change a value
' without the receiver noticing.
'
' THE IDEA IN FOUR WORDS: group, policy, token, key.
'   * SecurityGroup  All publishers and subscribers that share one key belong to
'                    one SecurityGroup (Part 14, 5.4.5).
'   * SecurityPolicy The algorithms and key lengths. "PubSub-Aes256-CTR" needs
'                    68 bytes of key data: SigningKey (32) + EncryptingKey (32)
'                    + KeyNonce (4), one after the other (Part 14, 7.2.4.4.3,
'                    Table 155).
'   * SecurityTokenId The number of the key. It stands in the header of every
'                    message, so the receiver knows which key to use. A key
'                    that is replaced gets the next number.
'   * Key            Random bytes, the same on both sides.
'
' A FIXED KEY IS THE SIMPLEST START, NOT THE END OF THE ROAD:
'   Both sides must hold the same key from a file. Nobody replaces it, so it
'   never ends its life and every change means a visit to every machine.
'   Workshops 43 to 45 show the better way: a Security Key Service (SKS) hands
'   out keys that change by themselves.
'
' KEEP THE KEY OUT OF THE SOURCE CODE:
'   * The key is read from a FILE. The path comes from the environment variable
'     PLCCOM_PUBSUB_KEYFILE (default: pubsub-group.key next to the program).
'   * If the file does not exist, this program makes it with random bytes.
'     Workshop 42 reads the same file: run both in the same folder, or copy the
'     file once.
'   * Protect the file like a password: only the account that runs the program
'     may read it. Never check it in, never send it by e-mail, never write it
'     into a log. This program prints a short key ID (such as "3F9A-C210") so
'     you can see that both sides hold the same key, but never the key itself.
'
' What you will learn:
'   * How to turn on UADP message security (SignAndEncrypt)
'   * What the key data is made of and how long it must be
'   * Why a key belongs in a file or a key service, not in the code
'
' Run Workshop 42 (Secure UADP Subscriber) to receive the published data.
' ==============================================================================

Imports System
Imports System.IO
Imports System.Security.Cryptography
Imports System.Threading
Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.PubSub.Encoding.Security
Imports PLCcom.Opc.Ua.PubSub.Sdk

Module Program

    Private Const KeyFileVariable As String = "PLCCOM_PUBSUB_KEYFILE"
    Private Const DefaultKeyFile As String = "pubsub-group.key"

    ' The number of the key. Both sides must use the same one. A fixed key keeps it for good.
    Private Const SecurityTokenId As Long = 1

    Sub Main(args As String())
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 41                      ║")
        Console.WriteLine("║  Secure UADP Publisher (fixed key from a file)               ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Signs and encrypts every message (SignAndEncrypt).          ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Target: opc.udp://localhost:4840                            ║")
        Console.WriteLine("║  Start Workshop 42 (Subscriber) to receive the data.         ║")
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

            ' -- Step 1: Get the key ------------------------------------------------
            ' The policy decides the algorithms and the length of the key data.
            Dim policy As UadpMessageSecurityPolicy = UadpMessageSecurityPolicy.PubSubAes256Ctr
            Dim keyFile As String = Environment.GetEnvironmentVariable(KeyFileVariable)
            If String.IsNullOrWhiteSpace(keyFile) Then keyFile = DefaultKeyFile
            Dim keyData As Byte() = LoadOrCreateKey(keyFile, policy)
            Console.WriteLine("  Key file: " & Path.GetFullPath(keyFile))
            Console.WriteLine("  Key ID:   " & KeyId(keyData) & "   (both sides must show the same ID)")
            Console.WriteLine()

            ' -- Step 2: Describe the message security ------------------------------
            ' WithConcatenatedKeyData: SecurityTokenId, policy and the key data
            ' (SigningKey + EncryptingKey + KeyNonce). SignAndEncrypt signs the
            ' whole message and encrypts the data. "Sign" alone would leave the
            ' values readable.
            Dim security As UaUadpSecurityConfiguration = UaUadpSecurityConfiguration _
                .WithConcatenatedKeyData(SecurityTokenId, policy, keyData) _
                .SecurityMode(UaUadpSecurityMode.SignAndEncrypt) _
                .Build()

            ' -- Step 3: Configure the publisher (as in Workshop 11) ----------------
            ' The only new line is WithUadpSecurity(). UADP message security works on
            ' every UDP transport - unicast, multicast and broadcast - because it
            ' protects the message, not a connection.
            Dim config = UaPublisherConfiguration.Build("SecureTemperaturePublisher", "opcua:Workshop41") _
                .WithNetworkInterface(NetworkInterfaces.All) _
                .WithTransport(PubSubTransportMode.DirectUnicast, "opc.udp://localhost:4840") _
                .WithUadpSecurity(security) _
                .WithPublishingInterval(1000) _
                .AddDataSet("SecureTemperatures", Sub(ds)
                                                      ds.AddField("Sensor1", New NodeId(1001UI, 2US))
                                                      ds.AddField("Sensor2", New NodeId(1002UI, 2US))
                                                      ds.AddField("Sensor3", New NodeId(1003UI, 2US))
                                                      ds.WithKeyFrameCount(10)
                                                      ds.WithInterval(1000)
                                                  End Sub)

            ' -- Step 4: Create and start the publisher -----------------------------
            Using publisher As New UaPublisher(LicenseUserName, LicenseSerial, config)

                Console.WriteLine("  License: " & publisher.LicenceMessage)
                Console.WriteLine()

                publisher.Start()

                Console.WriteLine("  Secure publisher started. Publishing every 1000 ms.")
                Console.WriteLine("  Security mode: SignAndEncrypt, " & policy.SecurityPolicyUri)
                Console.WriteLine("  Press ENTER to stop.")
                Console.WriteLine()

                ' -- Step 5: Simulate changing sensor values ------------------------
                Dim random As New Random()
                Dim counter As Integer = 0
                While Not (Not Console.IsInputRedirected AndAlso Console.KeyAvailable)
                    counter += 1
                    Dim temp1 As Double = 20.0 + random.NextDouble() * 10.0
                    Dim temp2 As Double = 30.0 + random.NextDouble() * 5.0
                    Dim temp3 As Double = 40.0 + random.NextDouble() * 5.0
                    publisher.WriteValue("SecureTemperatures", "Sensor1", temp1)
                    publisher.WriteValue("SecureTemperatures", "Sensor2", temp2)
                    publisher.WriteValue("SecureTemperatures", "Sensor3", temp3)

                    Console.WriteLine($"  [{counter:D5}] Sensor1={temp1:F2}°C  Sensor2={temp2:F2}°C  Sensor3={temp3:F2}°C")
                    Thread.Sleep(1000)
                End While

                Console.ReadKey(True)
                Console.WriteLine()

                ' -- Step 6: Stop the publisher -------------------------------------
                publisher.Stop()
                Console.WriteLine("  Publisher stopped.")

            End Using

        Catch ex As Exception
            Console.WriteLine($"  Error: {ex.Message}")
        End Try

        Console.WriteLine()
        Console.WriteLine("  Press ENTER to exit.")
        Console.ReadLine()
    End Sub

    ' Reads the key from the file; makes the file with random bytes when it is not there.
    ' The random generator of the operating system is the right source for a key - never a
    ' pattern, a password or Random().
    Private Function LoadOrCreateKey(path As String, policy As UadpMessageSecurityPolicy) As Byte()
        Dim length As Integer = policy.SigningKeyLength + policy.EncryptingKeyLength + policy.KeyNonceLength
        If File.Exists(path) Then
            Dim existing As Byte() = File.ReadAllBytes(path)
            If existing.Length <> length Then
                Throw New InvalidOperationException($"The key file has {existing.Length} bytes, the policy needs {length}.")
            End If
            Return existing
        End If

        Dim created(length - 1) As Byte
        Using generator As RandomNumberGenerator = RandomNumberGenerator.Create()
            generator.GetBytes(created)
        End Using
        File.WriteAllBytes(path, created)
        Console.WriteLine("  The key file did not exist: a new random key was made.")
        Console.WriteLine("  Protect it like a password. Workshop 42 needs the same file.")
        Return created
    End Function

    ' A short identification of the key for the screen, like "3F9A-C210": the first four
    ' bytes of a SHA-256 over the key. It cannot be turned back into the key.
    Private Function KeyId(keyData As Byte()) As String
        Using sha As SHA256 = SHA256.Create()
            Dim hash As Byte() = sha.ComputeHash(keyData)
            Return BitConverter.ToString(hash, 0, 2) & "-" & BitConverter.ToString(hash, 2, 2)
        End Using
    End Function

End Module
