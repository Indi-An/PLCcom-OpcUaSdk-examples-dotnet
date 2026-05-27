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
' PLCcom OPC UA PubSub SDK - Workshop 33: sMQTT JSON Publisher
'
' This workshop combines the two concepts from the previous workshops:
'   * JSON encoding from Workshop 31 (human-readable, universally consumable)
'   * TLS security from Workshop 23 (encrypted broker connection)
'
' This is the recommended configuration for cloud and cross-network deployments
' where both security and interoperability are required.
'
' UADP vs. JSON - when to choose which:
'
'   UADP + TLS (Workshops 23/24):
'     + Compact binary encoding - lower bandwidth
'     - Only readable by OPC UA PubSub clients
'     => Use for factory-floor, LAN, bandwidth-constrained scenarios
'
'   JSON + TLS (Workshops 33/34):
'     + Human-readable - easy to debug with standard MQTT tools
'     + Consumable by ANY MQTT client (cloud services, dashboards, scripts)
'     - Larger message size
'     => Use for cloud integration, cross-system communication
'
' CERTIFICATE MANAGEMENT - PKI STORE:
'   Same as Workshop 23. See that workshop for full PKI setup instructions.
'
' What you will learn:
'   * How to combine JSON encoding with TLS security
'   * When to use JSON+TLS vs. UADP+TLS
'
' Run Workshop 34 (sMQTT JSON Subscriber) to receive the data.
' ==============================================================================

Imports System
Imports System.Threading
Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.PubSub.Sdk

Module Program

    Sub Main(args As String())
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
        Console.WriteLine("║  PLCcom OPC UA PubSub SDK - Workshop 33                      ║")
        Console.WriteLine("║  sMQTT JSON Publisher (TLS + JSON)                           ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Publishes energy meter data via MQTT broker with TLS        ║")
        Console.WriteLine("║  encryption and JSON encoding. Secure and interoperable.     ║")
        Console.WriteLine("║                                                              ║")
        Console.WriteLine("║  Broker: mqtts://localhost:8883                              ║")
        Console.WriteLine("║  Start Workshop 34 (Subscriber) to receive the data.         ║")
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
        Console.WriteLine()

        Try
            'TODO
            'Submit your license information from your license e-mail
            Dim LicenseUserName As String = "<Enter your UserName here>"
            Dim LicenseSerial As String = "<Enter your Serial here>"

            ' -- Step 1: Configure the publisher -----------------------------------
            ' BrokerMqttJson = JSON encoding over MQTT.
            ' WithMqttTls() activates TLS and points to the PKI store directory.
            ' See the file header for PKI setup instructions.
            Dim config = UaPublisherConfiguration.Build("EnergyPublisher", "opcua:Workshop33") _
                .WithNetworkInterface(NetworkInterfaces.All) _
                .WithTransport(PubSubTransportMode.BrokerMqttJson, "mqtts://localhost:8883") _
                .WithMqttTls("./pki") _
                .WithPublishingInterval(2000) _
                .AddDataSet("EnergyMeter", Sub(ds)
                                               ds.AddField("Voltage", New NodeId(4001UI, 2US))
                                               ds.AddField("Current", New NodeId(4002UI, 2US))
                                               ds.AddField("Power", New NodeId(4003UI, 2US))
                                               ds.AddField("Energy", New NodeId(4004UI, 2US))
                                               ds.WithKeyFrameCount(5)
                                               ds.WithInterval(2000)
                                           End Sub)

            ' -- Step 2: Create and start the publisher ----------------------------
            Using publisher As New UaPublisher(LicenseUserName, LicenseSerial, config)

                Console.WriteLine("  License: " & publisher.LicenceMessage)
                Console.WriteLine()

                ' Subscribe to CertificateValidation to handle broker certificate trust.
                ' This handler accepts any certificate - suitable for testing only.
                ' For production, copy the CA certificate to ./pki/issuers/certs/ instead.
                AddHandler publisher.CertificateValidation, Sub(sender, e)
                                                                Console.WriteLine($"  [TLS] Broker certificate: {e.Certificate.Subject}")
                                                                Console.WriteLine($"  [TLS] Issuer:             {e.Certificate.Issuer}")
                                                                Console.WriteLine($"  [TLS] Valid until:        {e.Certificate.NotAfter:yyyy-MM-dd}")
                                                                e.Accept = True
                                                            End Sub

                publisher.Start()

                Console.WriteLine("  Publisher started. Connected to MQTT broker via TLS.")
                Console.WriteLine("  Publishing energy data every 2000 ms as JSON.")
                Console.WriteLine("  Press ENTER to stop.")
                Console.WriteLine()

                ' -- Step 3: Simulate energy meter readings ------------------------
                Dim random As New Random()
                Dim counter As Integer = 0
                Dim totalEnergy As Double = 1000.0

                While Not Console.KeyAvailable
                    Dim voltage As Double = 230.0 + (random.NextDouble() - 0.5) * 10.0
                    Dim current As Double = 5.0 + random.NextDouble() * 3.0
                    Dim power As Double = voltage * current
                    totalEnergy += power * 2.0 / 3600.0

                    publisher.WriteValue("EnergyMeter", "Voltage", voltage)
                    publisher.WriteValue("EnergyMeter", "Current", current)
                    publisher.WriteValue("EnergyMeter", "Power", power)
                    publisher.WriteValue("EnergyMeter", "Energy", totalEnergy)

                    counter += 1
                    Console.Write($"  [{counter:D5}] {voltage:F1}V  {current:F2}A  {power:F0}W  {totalEnergy:F1}Wh  [TLS+JSON]    " & Chr(13))

                    Thread.Sleep(2000)
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
