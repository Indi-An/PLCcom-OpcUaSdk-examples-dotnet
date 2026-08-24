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
' PLCcom OPC UA Client SDK - Workshop 22: Read and Write by Path
'
' Instead of using numeric NodeIds, you can address nodes by their
' dot-separated browse path (e.g. "Objects.Plant.Line1.Machine1.Temperature").
' GetNodeIdByPath() resolves the path to a NodeId, then you read/write
' as usual. This is more readable and maintainable than raw NodeIds.
'
' What you will learn:
'   * How to resolve a browse path to a NodeId (GetNodeIdByPath)
'   * How to read and write values using path-resolved NodeIds
'   * Synchronous and asynchronous read/write operations
'
' Target server: opc.tcp://localhost:48410
' ==============================================================================

Imports System
Imports PLCcom.Opc.Ua.Client.Sdk
Imports PLCcom.Opc.Ua.Client
Imports PLCcom.Opc.Ua
Imports System.Reflection

Public Class Program

    Private client As UaClient = Nothing

    Public Shared Sub Main(ByVal args As String())
        Dim p As Program = New Program()
        p.Start()
    End Sub

    Private Sub Start()
        Try

         Console.WriteLine()


             Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
             Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 22: Read/Write by Path  ║")
             Console.WriteLine("║                                                              ║")
             Console.WriteLine("║  Instead of numeric NodeIds, address nodes by their          ║")
             Console.WriteLine("║  dot-separated browse path. GetNodeIdByPath() resolves       ║")
             Console.WriteLine("║  the path to a NodeId, then you read/write as usual.         ║")
             Console.WriteLine("║                                                              ║")
             Console.WriteLine("║  What you will learn:                                        ║")
             Console.WriteLine("║    * Resolve browse paths to NodeIds (GetNodeIdByPath)       ║")
             Console.WriteLine("║    * Read and write using path-resolved NodeIds              ║")
             Console.WriteLine("║    * Synchronous and asynchronous operations                 ║")
             Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Required server: Server Workshop 11 (Simple Server)         ║")
            Console.WriteLine("║  opc.tcp://localhost:48410                                   ║")
             Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
             Console.WriteLine()

            'TODO
            'Submit your license information from your license e-mail
            ' Important !!!!!!!!!!!!!!!!!!
            ' Enter your Username + Serial here! Please note: with blank fields the library runs
            ' for 15 minutes during a debug session. Both values can also come
            ' from configuration or an environment variable.
            ' Free trial license (14 days, uninterrupted): https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-download/
            Dim LicenseUserName As String = ""
            Dim LicenseSerial As String = ""
            Dim Endpoints As EndpointDescriptionCollection = UaClient.GetEndpoints(New Uri("opc.tcp://localhost:48410"), certificateValidator:=AddressOf client_CertificateValidation)

            'sort endpoints by security level
            Endpoints = UaClient.SortEndpointsBySecurityLevel(Endpoints)

            If Endpoints.Count > 0 Then
                Console.WriteLine("endpoints found:")
                Dim counter As Integer = 0

                For Each Endpoint As EndpointDescription In Endpoints
                    Console.WriteLine($"{Math.Min(Threading.Interlocked.Increment(counter), counter - 1).ToString()} => { Endpoint.ToDisplayString()}")
                Next

                Console.WriteLine("please enter index of desired endpoint")
                Dim NumberOfEndpoint As String = Console.ReadLine()
                Console.WriteLine("")
                Dim iNumberOfEndpoint As Integer = -1

                If Integer.TryParse(NumberOfEndpoint, iNumberOfEndpoint) AndAlso iNumberOfEndpoint > -1 AndAlso iNumberOfEndpoint < Endpoints.Count Then
                    'create a a SessionConfiguration with the selected endpoint and application name
            Dim sessionConfiguration As SessionConfiguration = CreateConfig(Endpoints(iNumberOfEndpoint))
            PrintConfig(sessionConfiguration)

                    'enable autoconnect
                    sessionConfiguration.AutoConnect = True

                    'output certificate store path
                    Console.WriteLine($"Info: Sessionconfiguration created, certificate store path => { sessionConfiguration.CertificateStorePath}")

                    'Create a new opc client instance and pass your license information
                    client = New UaClient(LicenseUserName, LicenseSerial, sessionConfiguration)
                    Console.WriteLine($"Info: license state => { client.GetLicenceMessage()}")
                    Console.WriteLine("")

                    'register events
                    AddHandler client.ServerConnectionLost, AddressOf Client_ServerConnectionLost
                    AddHandler client.ServerConnected, AddressOf Client_ServerConnected
                    AddHandler client.KeepAlive, AddressOf Client_KeepAlive
                    AddHandler client.CertificateValidation, AddressOf client_CertificateValidation
                    client.Connect()
                    Console.WriteLine("press enter to reading synchronous..")
                    Console.ReadLine()

                    'Read multiple Nodes within one call 

                    'first create a ReadValueIdCollection and fill this with ReadValueId objects
                    Dim nodesToRead As ReadValueIdCollection = New ReadValueIdCollection()
                    Dim nodeToRead As ReadValueId = New ReadValueId()
                    nodeToRead.NodeId = client.GetNodeIdByPath("Objects.Plant.Line1.Machine1.Temperature")
                    nodeToRead.AttributeId = Attributes.Value
                    nodesToRead.Add(nodeToRead)
                    nodeToRead = New ReadValueId()
                    nodeToRead.NodeId = client.GetNodeIdByPath("Objects.Plant.Line1.Machine1.RPM")
                    nodeToRead.AttributeId = Attributes.Value
                    nodesToRead.Add(nodeToRead)
                    nodeToRead = New ReadValueId()
                    nodeToRead.NodeId = client.GetNodeIdByPath("Objects.Plant.Line1.Machine1.RPM")
                    nodeToRead.AttributeId = Attributes.Value
                    nodesToRead.Add(nodeToRead)

                    'reading the nodes synchronous
                    Dim readresults As DataValueCollection = client.Read(nodesToRead)

                    For i As Integer = 0 To readresults.Count - 1
                        Dim res As DataValue = readresults(i)
                        Console.WriteLine($"synchronous read result { nodesToRead(i).NodeId.ToString() } Value => { res.Value.ToString() } StatusCode => { res.StatusCode.ToString()}")
                    Next

                    Console.WriteLine()
                    Console.WriteLine("press enter to reading asynchronous..")
                    Console.ReadLine()

                    'reading the nodes asynchronous
                    Dim asyncReadResult = client.ReadAsync(nodesToRead).GetAwaiter().GetResult()
                    Dim asyncReadValues As DataValueCollection = asyncReadResult.Results

                    For i As Integer = 0 To asyncReadValues.Count - 1
                        Console.WriteLine($"asynchronous read result {asyncReadValues(i).ToString()} Value => { asyncReadValues(i).ToString() } StatusCode => { asyncReadValues(i).StatusCode.ToString()}")
                    Next

                    Console.WriteLine()
                    Console.WriteLine("press enter to writing synchronous..")
                    Console.ReadLine()

                    'create a WriteValueCollection and fill this with WriteValue objects
                    Dim nodesToWrite As WriteValueCollection = New WriteValueCollection()
                    Dim writeValue As WriteValue = New WriteValue()
                    writeValue.NodeId = client.GetNodeIdByPath("Objects.Plant.Line1.Machine1.Temperature")
                    writeValue.Value = New DataValue(25.5)
                    writeValue.AttributeId = Attributes.Value
                    nodesToWrite.Add(writeValue)
                    writeValue = New WriteValue()
                    writeValue.NodeId = client.GetNodeIdByPath("Objects.Plant.Line1.Machine1.RPM")
                    writeValue.AttributeId = Attributes.Value
                    writeValue.Value = New DataValue(1750)
                    nodesToWrite.Add(writeValue)
                    writeValue = New WriteValue()
                    writeValue.NodeId = client.GetNodeIdByPath("Objects.Plant.Line1.Machine1.Pressure")
                    writeValue.AttributeId = Attributes.Value
                    writeValue.Value = New DataValue(1.05F)
                    nodesToWrite.Add(writeValue)

                    'writing the nodes synchronous
                    Dim writeResults As StatusCodeCollection = client.Write(nodesToWrite)

                    For i As Integer = 0 To writeResults.Count - 1
                        Console.WriteLine($"synchronous write result { nodesToWrite(i).NodeId.ToString() } Value => { nodesToWrite(i).Value.ToString() } StatusCode => { writeResults(i).ToString()}")
                    Next

                    Console.WriteLine()
                    Console.WriteLine("press enter to writing asynchronous..")
                    Console.ReadLine()

                    'writing the nodes asynchronous
                    Dim asyncWriteResult = client.WriteAsync(nodesToWrite).GetAwaiter().GetResult()
                    Dim asyncWriteStatuscodes As StatusCodeCollection = asyncWriteResult.Results

                    For i As Integer = 0 To asyncWriteStatuscodes.Count - 1
                        Console.WriteLine($"asynchronous write result { nodesToWrite(i).NodeId.ToString() } Value => { nodesToWrite(i).Value.ToString() } StatusCode => { asyncWriteStatuscodes(i).ToString()}")
                    Next

                    Console.WriteLine()
                    Console.WriteLine("press enter for exit..")
                Else
                    Console.WriteLine("invalid number of Endpoint")
                    Console.WriteLine()
                End If
            Else
                Console.WriteLine("no endpoints found")
                Console.WriteLine()
            End If

        Catch ex As Exception
            Console.WriteLine(ex)
            Console.WriteLine()
        Finally
            Console.WriteLine("press enter for exit")
            Console.ReadLine()

            Try
                'disconnect session
                client.Disconnect()

                'unregister events
                RemoveHandler client.ServerConnectionLost, AddressOf Client_ServerConnectionLost
                RemoveHandler client.ServerConnected, AddressOf Client_ServerConnected
                RemoveHandler client.KeepAlive, AddressOf Client_KeepAlive
                RemoveHandler client.CertificateValidation, AddressOf client_CertificateValidation
            Catch
            End Try
        End Try
    End Sub


    Private Sub client_CertificateValidation(ByVal sender As CertificateValidator, ByVal e As CertificateValidationEventArgs)
        ' External certificate validation
        If ServiceResult.IsGood(e.Error) Then
            e.Accept = True
        ElseIf Not e.ContainsUnsuppressibleStatusCodes Then
            e.Accept = True
        ElseIf e.ContainsUnsuppressibleStatusCodes Then
            e.AcceptAll = True ' You can accept all unsuppressible status codes with this flag
        Else
            Throw New Exception(String.Format("Failed to validate certificate with error code {0}: {1}", e.Error.Code, e.Error.AdditionalInfo))
        End If
    End Sub

    Private Sub Client_ServerConnected(ByVal sender As Object, ByVal e As EventArgs)
        'event opc ua server is connected
        Console.WriteLine($"{Date.Now.ToLocalTime()} Session connected")
    End Sub

    Private Sub Client_ServerConnectionLost(ByVal sender As Object, ByVal e As EventArgs)
        'event connection to opc ua server lost
        Console.WriteLine($"{Date.Now.ToLocalTime()} Session connection lost")
    End Sub

    Private Sub Client_KeepAlive(ByVal session As ISession, ByVal e As KeepAliveEventArgs)
        'catch the keepalive event of opc ua server
    End Sub

    ' =============================================================================
    ' Helper: CreateConfig
    ' =============================================================================
    ' Builds the SessionConfiguration for the selected endpoint.
    '
    ' Certificate handling:
    '   Application certificate -- required for Sign / SignAndEncrypt endpoints.
    '   HTTPS certificate       -- required for opc.https:// endpoints (any SecurityMode).
    '
    ' Load() returns Nothing if the certificate does not exist yet or cannot be read.
    ' Build(True) creates a new self-signed certificate, overwriting any existing file.
    Private Shared Function CreateConfig(ByVal endpoint As EndpointDescription) As SessionConfiguration
        Dim appAlias As String = System.Reflection.Assembly.GetEntryAssembly().GetName().Name
        Dim config As SessionConfiguration = SessionConfiguration.Build(appAlias, endpoint)
        config.AutoConnect = False

        ' HTTPS certificate -- required for opc.https:// endpoints, independent of SecurityMode.
        Dim httpsCert As UaClientCertificate = Nothing
        If endpoint.EndpointUrl IsNot Nothing AndAlso
           endpoint.EndpointUrl.StartsWith("opc.https://", StringComparison.OrdinalIgnoreCase) Then
            Dim host As String = New Uri(endpoint.EndpointUrl).Host
            httpsCert = UaClientCertificate.Load("./pki", host, "secretpassword")
            If httpsCert Is Nothing OrElse Not httpsCert.CheckValidity() Then
                httpsCert = New UaClientCertificate("./pki", "secretpassword", host, 720, "Indi.An GmbH") _
                    .Build(overwrite:=True)
            End If
        End If

        ' Application certificate -- required for secured endpoints (Sign or SignAndEncrypt).
        ' Not needed for SecurityMode.None (unencrypted connections).
        Dim appCert As UaClientCertificate = Nothing
        If Not endpoint.SecurityMode.Equals(MessageSecurityMode.None) Then
            appCert = UaClientCertificate.Load("./pki", appAlias, "secretpassword")
            If appCert Is Nothing OrElse Not appCert.CheckValidity() Then
                appCert = New UaClientCertificate("./pki", "secretpassword", appAlias, 720, "Indi.An GmbH") _
                    .Build(overwrite:=True)
            End If
        End If

        ' SetInstanceCertificate() sets CertificateStorePath and ApplicationCertificateFullPath.
        If appCert IsNot Nothing AndAlso httpsCert IsNot Nothing Then
            config.SetInstanceCertificate(appCert, httpsCert)
        ElseIf appCert IsNot Nothing Then
            config.SetInstanceCertificate(appCert)
        End If

        Return config
    End Function

    ' =============================================================================
    ' Helper: PrintConfig
    ' =============================================================================
    ' Prints the active client configuration to the console so you can verify
    ' all settings at a glance before connecting.
    Private Shared Sub PrintConfig(ByVal config As SessionConfiguration)
        Console.WriteLine("-- Active Client Configuration ------------------------------")
        If config.Endpoint IsNot Nothing Then
            Console.WriteLine("  Endpoint  : " & config.Endpoint.EndpointUrl)
            Console.WriteLine("  Security  : " & config.Endpoint.ToDisplayString())
        End If
        Console.WriteLine("  PKI Store : " & If(config.CertificateStorePath IsNot Nothing, config.CertificateStorePath, "(not set)"))
        Console.WriteLine("  Cert File : " & If(config.ApplicationCertificateFullPath IsNot Nothing, config.ApplicationCertificateFullPath, "(none -- SecurityMode.None)"))
        Console.WriteLine("-------------------------------------------------------------")
        Console.WriteLine()
    End Sub
End Class
