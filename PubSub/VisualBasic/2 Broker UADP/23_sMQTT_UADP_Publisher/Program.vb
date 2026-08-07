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
' PLCcom OPC UA PubSub SDK - Workshop 23: sMQTT UADP Publisher
'
' This workshop extends Workshop 21 (MQTT UADP) by adding TLS encryption
' to the broker connection. The data set, publishing interval and network
' interface configuration are identical to Workshop 21.
'
' WHAT IS sMQTT?
'   sMQTT (secure MQTT) is standard MQTT over TLS/SSL on port 8883.
'   The connection is encrypted and the broker's identity is verified
'   using X.509 certificates.
'
' CERTIFICATE MANAGEMENT - PKI STORE:
'   WithMqttTls() uses the standard OPC UA PKI directory convention:
'
'     ./pki/trusted/certs/   - directly trusted broker certificates
'     ./pki/issuer/certs/    - trusted CA/issuer certificates (copy ca.crt here)
'     ./pki/rejected/        - refused certificates (no trust anchor, or not time-valid); review, then move to trusted/certs/
'
'   FIRST RUN:
'     The broker certificate is accepted when it lies in ./pki/trusted/certs/, or
'     when a CA of its chain lies in ./pki/trusted/certs/ or ./pki/issuer/certs/,
'     and every certificate of that chain is time-valid. Anything refused - no
'     trust anchor, or expired / not yet valid - is copied to ./pki/rejected/ and
'     the connection fails; review it there and move the certificate to
'     ./pki/trusted/certs/ (or its CA to ./pki/issuer/certs/) to trust it.
'     On a fresh PKI, options:
'
'     Option A - Copy the CA certificate (recommended for production):
'       Copy C:\APL\mqtt\certs\ca.crt to ./pki/issuer/certs/
'
'     Option B - Accept via CertificateValidation event (for testing):
'       AddHandler publisher.CertificateValidation, Sub(sender, e) e.Accept = True
'
' What you will learn:
'   * How to configure a sMQTT publisher with UADP encoding
'   * How the PKI store is used for broker certificate validation
'   * The difference between server-only TLS and mutual TLS (mTLS)
'
' Run Workshop 24 (sMQTT UADP Subscriber) to receive the data.
' ==============================================================================

Imports System
Imports System.Threading
Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.PubSub.Sdk

Module Program

    Sub Main(args As String())
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 23                      ║")
        Console.WriteLine("║  sMQTT UADP Publisher (TLS)                                  ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Publishes motor telemetry via MQTT broker with TLS          ║")
        Console.WriteLine("║  encryption and UADP binary encoding.                        ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Broker: mqtts://localhost:8883                              ║")
        Console.WriteLine("║  Start Workshop 24 (Subscriber) to receive the data.         ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Try
            'TODO
            'Submit your license information from your license e-mail
            Dim LicenseUserName As String = "<Enter your UserName here>"
            Dim LicenseSerial As String = "<Enter your Serial here>"

            ' -- Step 1: Configure the publisher -----------------------------------
            ' Note the mqtts:// scheme and port 8883 instead of mqtt:// and 1883.
            ' WithMqttTls() activates TLS and points to the PKI store directory.
            ' See the file header for PKI setup instructions.
            Dim config = UaPublisherConfiguration.Build("MotorPublisher", "opcua:Workshop23") _
                .WithNetworkInterface(NetworkInterfaces.All) _
                .WithTransport(PubSubTransportMode.BrokerMqttUadp, "mqtts://localhost:8883") _
                .WithMqttTls("./pki") _
                .WithPublishingInterval(1000) _
                .AddDataSet("MotorData", Sub(ds)
                                             ds.AddField("Speed", New NodeId(3001UI, 2US))
                                             ds.AddField("Current", New NodeId(3002UI, 2US))
                                             ds.AddField("Temperature", New NodeId(3003UI, 2US))
                                             ds.WithKeyFrameCount(10)
                                             ds.WithInterval(1000)
                                         End Sub)

            ' -- Step 2: Create and start the publisher ----------------------------
            Using publisher As New UaPublisher(LicenseUserName, LicenseSerial, config)

                Console.WriteLine("  License: " & publisher.LicenceMessage)
                Console.WriteLine()

                ' Subscribe to CertificateValidation to handle broker certificate trust.
                ' This handler accepts any certificate - suitable for testing only.
                ' For production, copy the CA certificate to ./pki/issuer/certs/ instead.
                AddHandler publisher.CertificateValidation, Sub(sender, e)
                                                                Console.WriteLine($"  [TLS] Broker certificate: {e.Certificate.Subject}")
                                                                Console.WriteLine($"  [TLS] Issuer:             {e.Certificate.Issuer}")
                                                                Console.WriteLine($"  [TLS] Valid until:        {e.Certificate.NotAfter:yyyy-MM-dd}")
                                                                e.Accept = True
                                                            End Sub

                publisher.Start()

                Console.WriteLine("  Publisher started. Connected to MQTT broker via TLS.")
                Console.WriteLine("  Publishing motor data every 1000 ms.")
                Console.WriteLine("  Press ENTER to stop.")
                Console.WriteLine()

                ' -- Step 3: Simulate motor telemetry ------------------------------
                Dim random As New Random()
                Dim counter As Integer = 0

                While Not Console.KeyAvailable
                    Dim speed As Double = 1450.0 + random.NextDouble() * 50.0
                    Dim current As Double = 12.5 + random.NextDouble() * 2.0
                    Dim temperature As Double = 55.0 + random.NextDouble() * 10.0

                    publisher.WriteValue("MotorData", "Speed", speed)
                    publisher.WriteValue("MotorData", "Current", current)
                    publisher.WriteValue("MotorData", "Temperature", temperature)

                    counter += 1
                    Console.Write($"  [{counter:D5}] Speed={speed:F0} rpm  Current={current:F1} A  Temp={temperature:F1}°C  [TLS]    " & Chr(13))

                    Thread.Sleep(1000)
                End While

                Console.ReadKey(True)
                Console.WriteLine()
                Console.WriteLine()

                publisher.Stop()
                Console.WriteLine("  Publisher stopped. Disconnected from broker.")

            End Using

        Catch ex As Exception
            Console.WriteLine($"  Error: {ex.Message}")
        End Try

        Console.WriteLine()
        Console.WriteLine("  Press ENTER to exit.")
        Console.ReadLine()
    End Sub

End Module
