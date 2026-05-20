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
' PLCcom OPC UA Client SDK - Workshop 25: Advanced Method Calls with Structs
'
' Building on Workshop 24, this example shows how to pass complex
' nested structures as method arguments. The input structure contains
' embedded sub-structures and arrays of structures - a common pattern
' in industrial OPC UA servers.
'
' What you will learn:
'   * How to encode nested structures with BinaryEncoder
'   * How to embed ExtensionObjects inside other structures
'   * How to encode arrays of structures
'   * How to call methods with complex structured arguments
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
             Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 25: Advanced Calls      ║")
             Console.WriteLine("║                                                              ║")
             Console.WriteLine("║  Building on Workshop 24, this example passes complex        ║")
             Console.WriteLine("║  nested structures as method arguments: embedded             ║")
             Console.WriteLine("║  sub-structures and arrays of structures.                    ║")
             Console.WriteLine("║                                                              ║")
             Console.WriteLine("║  What you will learn:                                        ║")
             Console.WriteLine("║    * Encode nested structures with BinaryEncoder             ║")
             Console.WriteLine("║    * Embed ExtensionObjects inside other structures          ║")
             Console.WriteLine("║    * Encode arrays of structures                             ║")
             Console.WriteLine("║                                                              ║")
             Console.WriteLine("║  Required server: Server Workshop 13 (Methods)               ║")
             Console.WriteLine("║  opc.tcp://localhost:48410                                   ║")
             Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
             Console.WriteLine()

            'TODO
            'Submit your license information from your license e-mail
            Dim LicenseUserName As String = "<Enter your UserName here>"
            Dim LicenseSerial As String = "<Enter your Serial here>"
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

                    'enable auto connect functionality
                    sessionConfiguration.AutoConnect = True

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
                        ' In this case, we pass a structure named as 'DataStructure_One" constructed as follows:
                        ' 
                        ' structure DataStructure_One = 
                        ' {
                        '   int myIntValue1,
                        '   string myStringValue2,
                        '   DataStructure_two DataStructure_two,
                        '   int myIntValue3,
                        '   DataStructure_two[] DataStructure_twoArray
                        ' }
                        ' 
                        ' Structure DataStructure_One contains some flat data types, a structure of DataStructure_two 
                        ' and a array of DataStructure_two structures:
                        ' 
                        ' structure DataStructure_two = 
                        ' {
                        '   int myIntValue10,
                        '   string myStringValue11,
                        '   int myIntValue12
                        ' }
                        ' 
                        ' Object: "myObjectNode_Advanced"
                        ' Method: "myMethodNode"
                        ' 

                        'create Encoder instances
                        Dim encoderDataStructure_One As BinaryEncoder = New BinaryEncoder(client.GetMessageContext())

                        'put objects to encoderDataStructure_One with given order
                        encoderDataStructure_One.WriteInt32("", 1) 'myIntValue1
                        encoderDataStructure_One.WriteString("", "test_string") 'myStringValue2

#Region "create and add embedded structure"

                        'create encoderDataStructure_Two instance
                        Dim encoderDataStructure_Two As BinaryEncoder = New BinaryEncoder(client.GetMessageContext())

                        'put objects to encoderDataStructure_Two with given order
                        encoderDataStructure_Two.WriteInt32("", 222) 'myIntValue10
                        encoderDataStructure_Two.WriteString("", "test_string11") 'myStringValue11
                        encoderDataStructure_Two.WriteInt32("", 1212) 'myIntValue12

                        'read byte array from encoder
                        Dim argumentByteArray As Byte() = encoderDataStructure_Two.CloseAndReturnBuffer()

                        'create an extension object and pass arguments to ExtensionObject.Body 
                        Dim extensionObjectDataStructure_Two As ExtensionObject = New ExtensionObject()
                        extensionObjectDataStructure_Two.Body = argumentByteArray

                        'set type of structure, create a new ExpandedNodeId by name and namespace
                        extensionObjectDataStructure_Two.TypeId = New ExpandedNodeId("DataStructure_Two", Convert.ToUInt16(3))

                        'write structure to input arguments
                        encoderDataStructure_One.WriteExtensionObject("", extensionObjectDataStructure_Two)

#End Region

                        encoderDataStructure_One.WriteInt32("", 3333) 'myIntValue3

#Region "create and add embedded structure Array "

                        'create a Array of DataStructure_Two objects with three objects
                        Dim dataStructure_TwoCollection As ExtensionObjectCollection = New ExtensionObjectCollection()

                        For i As Integer = 0 To 3 - 1
                            encoderDataStructure_Two = New BinaryEncoder(client.GetMessageContext())
                            encoderDataStructure_Two.WriteInt32("", 555) 'myIntValue10
                            encoderDataStructure_Two.WriteString("", "test_stringArray365") 'myStringValue11
                            encoderDataStructure_Two.WriteInt32("", 1212) 'myIntValue12
                            argumentByteArray = encoderDataStructure_Two.CloseAndReturnBuffer()
                            extensionObjectDataStructure_Two = New ExtensionObject()
                            extensionObjectDataStructure_Two.Body = argumentByteArray
                            extensionObjectDataStructure_Two.TypeId = New ExpandedNodeId("DataStructure_Two", Convert.ToUInt16(3))
                            dataStructure_TwoCollection.Add(extensionObjectDataStructure_Two)
                        Next

                        'write structure array to input arguments
                        encoderDataStructure_One.WriteExtensionObjectArray("", dataStructure_TwoCollection)

#End Region
                        encoderDataStructure_One.WriteInt32("", 3333) 'myIntValue3

                        'read byte array from encoder
                        argumentByteArray = encoderDataStructure_One.CloseAndReturnBuffer()

                        'create an extension object and pass arguments to ExtensionObject.Body
                        Dim extensionObjectWithInputArguments As ExtensionObject = New ExtensionObject()
                        extensionObjectWithInputArguments.Body = argumentByteArray
                        extensionObjectWithInputArguments.TypeId = New ExpandedNodeId("DataStructure_One", Convert.ToUInt16(3))

                        'create your InputArguments with extensionObject
                        Dim inputArguments As VariantCollection = New VariantCollection()
                        inputArguments.Add(New [Variant](extensionObjectWithInputArguments))

                        'resolve object and method via browse
                        Dim objectNode As NodeId = client.GetNodeIdByPath("Objects.Plant.myObjectNode_Advanced")
                        If objectNode Is Nothing Then
                            Console.WriteLine("myObjectNode_Advanced not found - is Server Workshop 13 running?")
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
                            Console.WriteLine("myMethodNode not found under myObjectNode_Advanced")
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
                                    Console.WriteLine($"output argument: { outputArgument.ToString()}")
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
        If ServiceResult.IsGood(e.Error) Then
            e.Accept = True
        ElseIf Not e.ContainsUnsuppressibleStatusCodes Then
            e.Accept = True
        ElseIf e.ContainsUnsuppressibleStatusCodes Then
            e.AcceptAll = True
        Else
            Throw New Exception(String.Format("Failed to validate certificate with error code {0}: {1}", e.Error.Code, e.Error.AdditionalInfo))
        End If
    End Sub

    Private Sub Client_ServerConnected(ByVal sender As Object, ByVal e As EventArgs)
        Console.WriteLine($"{Date.Now.ToLocalTime()} Session connected")
    End Sub

    Private Sub Client_ServerConnectionLost(ByVal sender As Object, ByVal e As EventArgs)
        Console.WriteLine($"{Date.Now.ToLocalTime()} Session connection lost")
    End Sub

    Private Sub Client_KeepAlive(ByVal session As ISession, ByVal e As KeepAliveEventArgs)
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
