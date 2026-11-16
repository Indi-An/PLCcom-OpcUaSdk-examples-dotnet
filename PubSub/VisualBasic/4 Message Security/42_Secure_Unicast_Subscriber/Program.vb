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
' PLCcom OPC UA PubSub SDK - Workshop 42: Secure UADP Subscriber (fixed key)
'
' The receiving side of Workshop 41. The subscriber checks the signature of every
' NetworkMessage and decrypts the data (OPC UA Part 14, 7.2.4.4). A message with
' a wrong signature - changed on the way, or sent by someone who does not hold
' the key - is dropped, and it never reaches your DataReceived handler.
'
' THE KEY MUST BE THE SAME ON BOTH SIDES:
'   Group, policy, SecurityTokenId and key bytes must match those of the
'   publisher. This program reads the key from the same file as Workshop 41:
'   the path comes from PLCCOM_PUBSUB_KEYFILE (default: pubsub-group.key). Run
'   both in one folder, or copy the file once to the other machine over a
'   channel you trust. The program prints a short key ID, never the key. If the
'   IDs differ, the keys differ, and nothing will arrive.
'
' FIELDS:
'   The field layout is configured here (AddField with the type), because the
'   metadata of a secured publisher is secured too, and a key mismatch would
'   leave the subscriber without fields as well as without data.
'
' What you will learn:
'   * How to configure a secured UADP subscriber
'   * That a message that fails the check never arrives
'   * What to look at when nothing arrives (key ID, token number, policy)
' ==============================================================================

Imports System
Imports System.IO
Imports System.Linq
Imports System.Security.Cryptography
Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.PubSub.Encoding.Security
Imports PLCcom.Opc.Ua.PubSub.Sdk

Module Program

    Private Const KeyFileVariable As String = "PLCCOM_PUBSUB_KEYFILE"
    Private Const DefaultKeyFile As String = "pubsub-group.key"

    ' Must be the number the publisher uses (Workshop 41).
    Private Const SecurityTokenId As Long = 1

    Sub Main(args As String())
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 42                      ║")
        Console.WriteLine("║  Secure UADP Subscriber (fixed key from a file)              ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Checks the signature and decrypts the data of Workshop 41.  ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Listening on: opc.udp://localhost:4840                      ║")
        Console.WriteLine("║  Start Workshop 41 (Publisher) to send data.                 ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Try
            Dim LicenseUserName As String = ""
            Dim LicenseSerial As String = ""
            If LicenseUserName.Length = 0 Then LicenseUserName = If(System.Environment.GetEnvironmentVariable("PLCCOM_OPCUA_LICENSE_USER"), "")
            If LicenseSerial.Length = 0 Then LicenseSerial = If(System.Environment.GetEnvironmentVariable("PLCCOM_OPCUA_LICENSE_SERIAL"), "")

            ' -- Step 1: Read the key (the same file as the publisher) --------------
            Dim policy As UadpMessageSecurityPolicy = UadpMessageSecurityPolicy.PubSubAes256Ctr
            Dim keyFile As String = Environment.GetEnvironmentVariable(KeyFileVariable)
            If String.IsNullOrWhiteSpace(keyFile) Then keyFile = DefaultKeyFile
            If Not File.Exists(keyFile) Then
                Console.WriteLine("  The key file '" & Path.GetFullPath(keyFile) & "' does not exist.")
                Console.WriteLine("  Start Workshop 41 first in this folder, or copy its key file here.")
                Console.WriteLine("  Press ENTER to exit.")
                Console.ReadLine()
                Return
            End If
            Dim keyData As Byte() = File.ReadAllBytes(keyFile)
            Dim length As Integer = policy.SigningKeyLength + policy.EncryptingKeyLength + policy.KeyNonceLength
            If keyData.Length <> length Then
                Throw New InvalidOperationException($"The key file has {keyData.Length} bytes, the policy needs {length}.")
            End If
            Console.WriteLine("  Key file: " & Path.GetFullPath(keyFile))
            Console.WriteLine("  Key ID:   " & KeyId(keyData) & "   (must be the one the publisher shows)")
            Console.WriteLine()

            ' -- Step 2: The message security, as on the publisher side -------------
            Dim security As UaUadpSecurityConfiguration = UaUadpSecurityConfiguration _
                .WithConcatenatedKeyData(SecurityTokenId, policy, keyData) _
                .SecurityMode(UaUadpSecurityMode.SignAndEncrypt) _
                .Build()

            ' -- Step 3: Configure the subscriber (as in Workshop 12) ---------------
            Dim config = UaSubscriberConfiguration.Build("SecureTemperatureSubscriber") _
                .WithNetworkInterface(NetworkInterfaces.All) _
                .WithTransport(PubSubTransportMode.DirectUnicast, "opc.udp://localhost:4840") _
                .WithUadpSecurity(security) _
                .AddDataSetReader("opcua:Workshop41", "SecureTemperatures", Sub(ds)
                                                                                ds.AddField("Sensor1", BuiltInType.Double)
                                                                                ds.AddField("Sensor2", BuiltInType.Double)
                                                                                ds.AddField("Sensor3", BuiltInType.Double)
                                                                            End Sub)

            Using subscriber As New UaSubscriber(LicenseUserName, LicenseSerial, config)

                Console.WriteLine("  License: " & subscriber.LicenceMessage)
                Console.WriteLine()

                Dim messageCount As Integer = 0
                AddHandler subscriber.DataReceived, Sub(sender, e)
                                                        messageCount += 1
                                                        Dim keyFrame As String = If(e.IsKeyFrame, "[KEY]", "[DEL]")
                                                        Dim fieldStr As String = String.Join("  ", e.Fields.Select(Function(f) $"{f.Key}={f.Value.Value:F2}"))
                                                        Console.WriteLine($"  [{messageCount:D5}] {keyFrame} {e.DataSetName} | {fieldStr}")
                                                    End Sub

                subscriber.Start()

                Console.WriteLine("  Secure subscriber started. Waiting for messages...")
                Console.WriteLine("  If nothing arrives: compare the key IDs, the SecurityTokenId and the policy.")
                Console.WriteLine("  Press ENTER to stop.")
                Console.WriteLine()

                Console.ReadLine()

                subscriber.Stop()
                Console.WriteLine($"  Subscriber stopped. Received {messageCount} messages total.")

            End Using

        Catch ex As Exception
            Console.WriteLine($"  Error: {ex.Message}")
        End Try

        Console.WriteLine()
        Console.WriteLine("  Press ENTER to exit.")
        Console.ReadLine()
    End Sub

    ' The same short key ID as in Workshop 41; it cannot be turned back into the key.
    Private Function KeyId(keyData As Byte()) As String
        Using sha As SHA256 = SHA256.Create()
            Dim hash As Byte() = sha.ComputeHash(keyData)
            Return BitConverter.ToString(hash, 0, 2) & "-" & BitConverter.ToString(hash, 2, 2)
        End Using
    End Function

End Module
