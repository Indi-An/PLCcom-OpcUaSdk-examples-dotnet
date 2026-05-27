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
' PLCcom OPC UA PubSub SDK - Workshop 24: sMQTT UADP Subscriber
'
' This workshop is the receiving side of Workshop 23. The subscriber connects
' to the MQTT broker via TLS and receives UADP-encoded motor telemetry.
' From the API perspective this is identical to Workshop 22 - only the
' scheme (mqtts://) and WithMqttTls() differ.
'
' CERTIFICATE MANAGEMENT - PKI STORE:
'   WithMqttTls() uses the standard OPC UA PKI directory convention:
'
'     ./pki/trusted/certs/   - directly trusted broker certificates
'     ./pki/issuers/certs/   - trusted CA/issuer certificates (copy ca.crt here)
'     ./pki/rejected/        - certificates rejected on first contact
'
'   FIRST RUN:
'     Option A - Copy the CA certificate (recommended for production):
'       Copy C:\APL\mqtt\certs\ca.crt to ./pki/issuers/certs/
'
'     Option B - Accept via CertificateValidation event (for testing):
'       AddHandler subscriber.CertificateValidation, Sub(sender, e) e.Accept = True
'
' What you will learn:
'   * How to configure a sMQTT subscriber with UADP decoding
'   * How the PKI store is used for broker certificate validation
'
' Start Workshop 23 (sMQTT UADP Publisher) first to send data.
' ==============================================================================

Imports System
Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.PubSub.Sdk

Module Program

    Sub Main(args As String())
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 24                      ║")
        Console.WriteLine("║  sMQTT UADP Subscriber (TLS)                                 ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Connects to MQTT broker via TLS and receives UADP-encoded   ║")
        Console.WriteLine("║  motor telemetry from Workshop 23 (Publisher).               ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Broker: mqtts://localhost:8883                              ║")
        Console.WriteLine("║  Start Workshop 23 (Publisher) to send data.                 ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Try
            'TODO
            'Submit your license information from your license e-mail
            Dim LicenseUserName As String = "<Enter your UserName here>"
            Dim LicenseSerial As String = "<Enter your Serial here>"

            ' -- Step 1: Configure the subscriber ----------------------------------
            Dim config = UaSubscriberConfiguration.Build("MotorSubscriber") _
                .WithNetworkInterface(NetworkInterfaces.All) _
                .WithTransport(PubSubTransportMode.BrokerMqttUadp, "mqtts://localhost:8883") _
                .WithMqttTls("./pki") _
                .AddDataSetReader("opcua:Workshop23", "MotorData", Sub(ds)
                                                                       ds.AddField("Speed", BuiltInType.Double)
                                                                       ds.AddField("Current", BuiltInType.Double)
                                                                       ds.AddField("Temperature", BuiltInType.Double)
                                                                   End Sub)

            ' -- Step 2: Create subscriber and attach event handler ----------------
            Using subscriber As New UaSubscriber(LicenseUserName, LicenseSerial, config)

                Console.WriteLine("  License: " & subscriber.LicenceMessage)
                Console.WriteLine()

                ' Subscribe to CertificateValidation to handle broker certificate trust.
                ' This handler accepts any certificate - suitable for testing only.
                ' For production, copy the CA certificate to ./pki/issuers/certs/ instead.
                AddHandler subscriber.CertificateValidation, Sub(sender, e)
                                                                 Console.WriteLine($"  [TLS] Broker certificate: {e.Certificate.Subject}")
                                                                 Console.WriteLine($"  [TLS] Issuer:             {e.Certificate.Issuer}")
                                                                 Console.WriteLine($"  [TLS] Valid until:        {e.Certificate.NotAfter:yyyy-MM-dd}")
                                                                 e.Accept = True
                                                             End Sub

                Dim messageCount As Integer = 0

                AddHandler subscriber.DataReceived, Sub(sender, e)
                                                        messageCount += 1
                                                        Console.WriteLine($"  [{messageCount:D5}] Motor Telemetry (Seq#{e.SequenceNumber}) [TLS]:")
                                                        For Each field In e.Fields
                                                            Dim unit As String = ""
                                                            Select Case field.Key
                                                                Case "Speed" : unit = "rpm"
                                                                Case "Current" : unit = "A"
                                                                Case "Temperature" : unit = "°C"
                                                            End Select
                                                            Console.WriteLine($"         {field.Key} = {field.Value.Value} {unit}")
                                                        Next
                                                        Console.WriteLine()
                                                    End Sub

                ' -- Step 3: Start listening ---------------------------------------
                subscriber.Start()

                Console.WriteLine("  Subscriber started. Connected to MQTT broker via TLS.")
                Console.WriteLine("  Waiting for messages...")
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

End Module
