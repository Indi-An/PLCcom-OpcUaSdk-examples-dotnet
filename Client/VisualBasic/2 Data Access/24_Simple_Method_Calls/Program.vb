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
' PLCcom OPC UA Client SDK - Workshop 24: Simple Method Calls
'
' OPC UA Methods are callable functions in the server address space.
' A client invokes a method by sending a Call request with input
' arguments and receives output arguments in the response.
' This workshop demonstrates calling methods with structured input.
'
' What you will learn:
'   * How to encode structured input arguments with BinaryEncoder
'   * How to create an ExtensionObject for method input
'   * How to call a method and evaluate the result
'   * How to read output arguments from the CallMethodResult
'
' Required server: Server Workshop 13 (Methods)
' Target server:   opc.tcp://localhost:48410
' ==============================================================================

Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Client
Imports PLCcom.Opc.Ua.Client.Sdk
Imports System
Imports System.Reflection

Public Class Program

    'flag, accept all untrusted certicates or not
    Private autoAcceptUntrustedCertificates As Boolean = True

    Public Shared Sub Main(ByVal args As String())
        Dim p As Program = New Program()
        p.Start()
    End Sub

    Private Sub Start()
        Try

         Console.WriteLine()

             Console.WriteLine("╔══════════════════════════════════════════════════════════════╗")
             Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 24: Simple Method Calls ║")
             Console.WriteLine("║                                                              ║")
             Console.WriteLine("║  OPC UA Methods are callable functions in the server         ║")
             Console.WriteLine("║  address space. This workshop shows how to call methods      ║")
             Console.WriteLine("║  with structured input arguments using BinaryEncoder.        ║")
             Console.WriteLine("║                                                              ║")
             Console.WriteLine("║  What you will learn:                                        ║")
             Console.WriteLine("║    * Encode structured input with BinaryEncoder              ║")
             Console.WriteLine("║    * Create ExtensionObjects for method input                ║")
             Console.WriteLine("║    * Call a method and evaluate the result                   ║")
             Console.WriteLine("║                                                              ║")
             Console.WriteLine("║  Required server: Server Workshop 13 (Methods)               ║")
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

                    'output certificate store path
                    Console.WriteLine($"Info: Sessionconfiguration created, certificate store path => { sessionConfiguration.CertificateStorePath}")

                    'Create a new opc client instance and pass your license information
                    Using client As UaClient = New UaClient(LicenseUserName, LicenseSerial, sessionConfiguration)
                        Console.WriteLine($"Info: license state => { client.GetLicenceMessage()}")
                        Console.WriteLine("")

                        'register events
                        AddHandler client.ServerConnectionLost, AddressOf Client_ServerConnectionLost
                        AddHandler client.ServerConnected, AddressOf Client_ServerConnected
                        AddHandler client.SessionClosing, AddressOf Client_SessionClosing
                        AddHandler client.KeepAlive, AddressOf Client_KeepAlive
                        AddHandler client.CertificateValidation, AddressOf client_CertificateValidation

                        ' 
                        ' let´s starting a method call, step by step
                        ' In this simple case, we pass a simple structure named as 'DataStructure_One" constructed as follows:
                        ' 
                        ' structure DataStructure_One = 
                        ' {
                        '   int myIntValue1,
                        '   string myStringValue2,
                        '   int myIntValue3,
                        '   int myIntValue4,
                        '   string myStringValue5
                        ' }
                        ' 
                        ' Object to which the method should be applied is named as "myObjectNode"
                        ' Method is named as "myMethodNode"
                        ' 

                        Dim myIntValue1 As Integer = 1
                        Dim myStringValue2 As String = "testvalue"
                        Dim myIntValue3 As Integer = 3333
                        Dim myIntValue4 As Integer = 4444
                        Dim myStringValue5 As String = "a_string_value"

                        'create a Encoder instance
                        Dim encoder As BinaryEncoder = New BinaryEncoder(client.GetMessageContext())

                        'put objects to encoder with given order
                        encoder.WriteInt32("", myIntValue1)
                        encoder.WriteString("", myStringValue2)
                        encoder.WriteInt32("", myIntValue3)
                        encoder.WriteInt32("", myIntValue4)
                        encoder.WriteString("", myStringValue5)

                        'read byte array from encoder
                        Dim argumentByteArray As Byte() = encoder.CloseAndReturnBuffer()

                        'create an extension object and pass arguments to ExtensionObject.Body
                        Dim extensionObjectWithInputArguments As ExtensionObject = New ExtensionObject()
                        extensionObjectWithInputArguments.Body = argumentByteArray

                        'set type of structure, create a new ExpandedNodeId by name and namespace
                        extensionObjectWithInputArguments.TypeId = New ExpandedNodeId("DataStructure_One", Convert.ToUInt16(2))

                        'create your InputArguments with extensionObject
                        Dim inputArguments As VariantCollection = New VariantCollection()
                        inputArguments.Add(New [Variant](extensionObjectWithInputArguments))

                        'resolve object and method via browse
                        Dim objectNode As NodeId = client.GetNodeIdByPath("Objects.Plant.myObjectNode")
                        If objectNode Is Nothing Then
                            Console.WriteLine("myObjectNode not found - is Server Workshop 13 running?")
                            Return
                        End If

                        Dim methodNode As NodeId = Nothing
                        Dim browseDesc As New BrowseDescription With {
                            .NodeId = objectNode,
                            .BrowseDirection = BrowseDirection.Forward,
                            .ReferenceTypeId = ReferenceTypeIds.HasComponent,
                            .IncludeSubtypes = True,
                            .NodeClassMask = CUInt(NodeClass.Method),
                            .ResultMask = CUInt(BrowseResultMask.All)
                        }
                        Dim refs = client.BrowseFull(New BrowseDescriptionCollection From {browseDesc})
                        For Each r In refs
                            If r.BrowseName.Name = "myMethodNode" Then
                                methodNode = ExpandedNodeId.ToNodeId(r.NodeId, client.GetNamespaceUris())
                                Exit For
                            End If
                        Next
                        If methodNode Is Nothing Then
                            Console.WriteLine("myMethodNode not found under myObjectNode")
                            Return
                        End If

                        'create a CallMethodRequest instance and pass your arguments
                        Dim request As CallMethodRequest = New CallMethodRequest()
                        request.ObjectId = objectNode
                        request.MethodId = methodNode
                        request.InputArguments = inputArguments

                        'call your method 
                        Dim result As CallMethodResult = client.Call(request)

                        'finaly evaluate your results,
                        If StatusCode.IsGood(result.StatusCode) Then

                            For Each outputArgument As [Variant] In result.OutputArguments
                                If outputArgument <> [Variant].Null Then _
                                    Console.WriteLine($"output argument: {outputArgument.ToString()}")
                            Next
                        Else
                            Console.WriteLine($"Method call failed { result.StatusCode.ToString()}")
                        End If
                    End Using
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

    Private Sub Client_SessionClosing(ByVal sender As Object, ByVal e As EventArgs)
        Console.WriteLine($"{Date.Now.ToLocalTime()} Session closed")
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
