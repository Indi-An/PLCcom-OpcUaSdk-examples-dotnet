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
' PLCcom OPC UA PubSub SDK - Workshop 34: sMQTT JSON Subscriber
'
' This workshop is the receiving side of Workshop 33. It combines JSON
' decoding with TLS security - the recommended configuration for cloud
' and cross-network deployments.
'
' TRANSPARENT API:
'   From the subscriber API perspective, this workshop is identical to
'   Workshop 32 (plain MQTT JSON). The only differences are:
'     * mqtts:// scheme instead of mqtt://
'     * Port 8883 instead of 1883
'     * WithMqttTls() call
'     * CertificateValidation event handler
'
' CERTIFICATE MANAGEMENT - PKI STORE:
'   Same as Workshop 23/24. See Workshop 23 for full PKI setup instructions.
'
'     ./pki/trusted/certs/   - directly trusted broker certificates
'     ./pki/issuer/certs/    - trusted CA/issuer certificates (copy ca.crt here)
'     ./pki/rejected/        - refused certificates (no trust anchor, or not time-valid); review, then move to trusted/certs/
'
' What you will learn:
'   * How to combine JSON decoding with TLS security
'   * How the PKI store is used for broker certificate validation
'
' Start Workshop 33 (sMQTT JSON Publisher) first to send data.
' ==============================================================================

Imports System
Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.PubSub.Sdk

Module Program

    Sub Main(args As String())
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 34                      ║")
        Console.WriteLine("║  sMQTT JSON Subscriber (TLS + JSON)                          ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Connects to MQTT broker via TLS and receives JSON-encoded   ║")
        Console.WriteLine("║  energy meter data from Workshop 33 (Publisher).             ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Broker: mqtts://localhost:8883                              ║")
        Console.WriteLine("║  Start Workshop 33 (Publisher) to send data.                 ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Try
            'TODO
            'Submit your license information from your license e-mail
            Dim LicenseUserName As String = "<Enter your UserName here>"
            Dim LicenseSerial As String = "<Enter your Serial here>"

            ' -- Step 1: Configure the subscriber ----------------------------------
            ' BrokerMqttJson = JSON decoding over MQTT.
            ' WithMqttTls() activates TLS and points to the PKI store directory.
            ' See the file header for PKI setup instructions.
            Dim config = UaSubscriberConfiguration.Build("EnergySubscriber") _
                .WithNetworkInterface(NetworkInterfaces.All) _
                .WithTransport(PubSubTransportMode.BrokerMqttJson, "mqtts://localhost:8883") _
                .WithMqttTls("./pki") _
                .AddDataSetReader("opcua:Workshop33", "EnergyMeter", Sub(ds)
                                                                         ds.AddField("Voltage", BuiltInType.Double)
                                                                         ds.AddField("Current", BuiltInType.Double)
                                                                         ds.AddField("Power", BuiltInType.Double)
                                                                         ds.AddField("Energy", BuiltInType.Double)
                                                                     End Sub)

            ' -- Step 2: Create subscriber and attach event handler ----------------
            Using subscriber As New UaSubscriber(LicenseUserName, LicenseSerial, config)

                Console.WriteLine("  License: " & subscriber.LicenceMessage)
                Console.WriteLine()

                ' Subscribe to CertificateValidation to handle broker certificate trust.
                ' This handler accepts any certificate - suitable for testing only.
                ' For production, copy the CA certificate to ./pki/issuer/certs/ instead.
                AddHandler subscriber.CertificateValidation, Sub(sender, e)
                                                                 Console.WriteLine($"  [TLS] Broker certificate: {e.Certificate.Subject}")
                                                                 Console.WriteLine($"  [TLS] Issuer:             {e.Certificate.Issuer}")
                                                                 Console.WriteLine($"  [TLS] Valid until:        {e.Certificate.NotAfter:yyyy-MM-dd}")
                                                                 e.Accept = True
                                                             End Sub

                Dim messageCount As Integer = 0

                AddHandler subscriber.DataReceived, Sub(sender, e)
                                                        messageCount += 1
                                                        Console.WriteLine($"  [{messageCount:D5}] Energy Meter (Seq#{e.SequenceNumber}) [TLS+JSON]:")
                                                        For Each field In e.Fields
                                                            Dim unit As String = ""
                                                            Select Case field.Key
                                                                Case "Voltage" : unit = "V"
                                                                Case "Current" : unit = "A"
                                                                Case "Power" : unit = "W"
                                                                Case "Energy" : unit = "Wh"
                                                            End Select
                                                            Console.WriteLine($"         {field.Key} = {field.Value.Value} {unit}")
                                                        Next
                                                        Console.WriteLine()
                                                    End Sub

                ' -- Step 3: Start listening ---------------------------------------
                subscriber.Start()

                Console.WriteLine("  Subscriber started. Connected to MQTT broker via TLS.")
                Console.WriteLine("  Waiting for JSON messages...")
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
