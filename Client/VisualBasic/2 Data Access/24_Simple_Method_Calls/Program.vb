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
