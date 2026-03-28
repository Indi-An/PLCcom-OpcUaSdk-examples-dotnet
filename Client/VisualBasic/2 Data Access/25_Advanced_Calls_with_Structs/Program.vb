Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Client
Imports PLCcom.Opc.Ua.Client.Sdk
Imports System
Imports System.Reflection

Public Class Program

    'flag, accept all untrusted certicates or not
    Public Shared Sub Main(ByVal args As String())
        Dim p As Program = New Program()
        p.Start()
    End Sub

    Private Sub Start()
        Try

            'Submit your license information from your license e-mail
            Dim LicenseUserName As String = "<Enter your UserName here>"
            Dim LicenseSerial As String = "<Enter your Serial here>"

            Dim Endpoints As EndpointDescriptionCollection = UaClient.GetEndpoints(New Uri("opc.tcp://localhost:50520/PLCcom/DataAccessServer"), 60000)

            'Sort endpoints by security level (highest security first)
            Endpoints = UaClient.SortEndpointsBySecurityLevel(Endpoints)

            If Endpoints.Count > 0 Then
                Console.WriteLine("endpoints found:")
                Dim counter As Integer = 0

                For Each Endpoint As EndpointDescription In Endpoints
                    Console.WriteLine($"{Math.Min(Threading.Interlocked.Increment(counter), counter - 1).ToString()} => { UaClient.EndpointToString(Endpoint)}")
                Next

                Console.WriteLine("please enter index of desired endpoint")
                Dim NumberOfEndpoint As String = Console.ReadLine()
                Console.WriteLine("")
                Dim iNumberOfEndpoint As Integer = -1

                If Integer.TryParse(NumberOfEndpoint, iNumberOfEndpoint) AndAlso iNumberOfEndpoint > -1 AndAlso iNumberOfEndpoint < Endpoints.Count Then
                    'Create a SessionConfiguration with the selected endpoint and application name
                    Dim sessionConfiguration As SessionConfiguration = SessionConfiguration.Build(Assembly.GetEntryAssembly().GetName().Name, Endpoints(iNumberOfEndpoint))

                    'Enable AutoConnect - the client will connect and reconnect automatically
                    sessionConfiguration.AutoConnect = True

                    'Display the certificate store path for debugging purposes
                    Console.WriteLine($"Info: Sessionconfiguration created, certificate store path => { sessionConfiguration.CertificateStorePath}")

                    'Create a new OPC UA client instance with license credentials
                    Using client As UaClient = New UaClient(LicenseUserName, LicenseSerial, sessionConfiguration)
                        Console.WriteLine($"Info: license state => { client.GetLicenceMessage()}")
                        Console.WriteLine("")

                        'Register event handlers to monitor the connection state
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
                        '     string myStringValue2,
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
                        ' Object to which the method should be applied is named as "myObjectNode"
                        ' Method is named as "myMethodNode"
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
                            'create encoderDataStructure_Two instance
                            encoderDataStructure_Two = New BinaryEncoder(client.GetMessageContext())

                            'put objects to encoderDataStructure_Two with given order
                            encoderDataStructure_Two.WriteInt32("", 555) 'myIntValue10
                            encoderDataStructure_Two.WriteString("", "test_stringArray365") 'myStringValue11
                            encoderDataStructure_Two.WriteInt32("", 1212) 'myIntValue12

                            'read byte array from encoder
                            argumentByteArray = encoderDataStructure_Two.CloseAndReturnBuffer()

                            'create an extension object and pass arguments to ExtensionObject.Body 
                            extensionObjectDataStructure_Two = New ExtensionObject()
                            extensionObjectDataStructure_Two.Body = argumentByteArray

                            'set type of structure, create a new ExpandedNodeId by name and namespace
                            extensionObjectDataStructure_Two.TypeId = New ExpandedNodeId("DataStructure_Two", Convert.ToUInt16(3))

                            'add structure to ExtensionObjectCollection
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

                        'set type of structure, create a new ExpandedNodeId by name and namespace
                        extensionObjectWithInputArguments.TypeId = New ExpandedNodeId("DataStructure_One", Convert.ToUInt16(3))

                        'create your InputArguments with extensionObject
                        Dim inputArguments As VariantCollection = New VariantCollection()
                        inputArguments.Add(New [Variant](extensionObjectWithInputArguments))

                        'create a new NodeId for the Object to which the method should be applied by name and namespace
                        Dim objectNode As NodeId = New NodeId("myObjectNode", 3)

                        'create a new NodeId for the Method by name and namespace
                        Dim methodNode As NodeId = New NodeId("myMethodNode", 3)

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
        'Fired when the OPC UA session is successfully established
        Console.WriteLine($"{Date.Now.ToLocalTime()} Session connected")
    End Sub

    Private Sub Client_ServerConnectionLost(ByVal sender As Object, ByVal e As EventArgs)
        'Fired when the connection to the OPC UA server is lost
        Console.WriteLine($"{Date.Now.ToLocalTime()} Session connection lost")
    End Sub

    Private Sub Client_KeepAlive(ByVal session As ISession, ByVal e As KeepAliveEventArgs)
        'Fired periodically to indicate the server is still alive
    End Sub

    Private Sub Client_SessionClosing(ByVal sender As Object, ByVal e As EventArgs)
        Console.WriteLine($"{Date.Now.ToLocalTime()} Session closed")
    End Sub
End Class
