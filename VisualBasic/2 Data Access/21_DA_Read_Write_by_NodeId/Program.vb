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

                    'enable autoconnect
                    sessionConfiguration.AutoConnect = True

                    'Display the certificate store path for debugging purposes
                    Console.WriteLine($"Info: Sessionconfiguration created, certificate store path => { sessionConfiguration.CertificateStorePath}")

                    'Create a new OPC UA client instance with license credentials
                    client = New UaClient(LicenseUserName, LicenseSerial, sessionConfiguration)
                    Console.WriteLine($"Info: license state => { client.GetLicenceMessage()}")
                    Console.WriteLine("")

                    'Register event handlers to monitor the connection state
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
                    nodeToRead.NodeId = New NodeId("ns=2;i=10219") 'Objects.Data.Static.Scalar.Int16Value
                    nodeToRead.AttributeId = Attributes.Value
                    nodesToRead.Add(nodeToRead)

                    nodeToRead = New ReadValueId()
                    nodeToRead.NodeId = New NodeId("ns=2;i=10221") 'Objects.Data.Static.Scalar.Int32Value
                    nodeToRead.AttributeId = Attributes.Value
                    nodesToRead.Add(nodeToRead)

                    nodeToRead = New ReadValueId()
                    nodeToRead.NodeId = New NodeId("ns=2;i=10223") 'Objects.Data.Static.Scalar.Int64Value
                    nodeToRead.AttributeId = Attributes.Value
                    nodesToRead.Add(nodeToRead)

                    'Read the node values synchronously
                    Dim readresults As DataValueCollection = client.Read(nodesToRead)

                    For i As Integer = 0 To readresults.Count - 1
                        Dim res As DataValue = readresults(i)
                        If StatusCode.IsGood(res.StatusCode) Then
                            Console.WriteLine($"synchronous read result { nodesToRead(i).NodeId.ToString() } Value => { res.Value.ToString() } StatusCode => { res.StatusCode.ToString()}")
                        Else
                            Console.WriteLine($"read failed for { nodesToRead(i).NodeId.ToString() } StatusCode => { res.StatusCode.ToString()}")
                        End If
                    Next

                    Console.WriteLine()
                    Console.WriteLine("press enter to reading asynchronous..")
                    Console.ReadLine()

                    'reading the nodes asynchronous
                    Dim asyncReadResult = client.ReadAsync(nodesToRead).GetAwaiter().GetResult()
                    Dim asyncReadValues As DataValueCollection = asyncReadResult.Results

                    For i As Integer = 0 To asyncReadValues.Count - 1
                        Console.WriteLine($"asynchronous read result { nodesToRead(i).NodeId.ToString() } Value => { asyncReadValues(i).ToString() } StatusCode => { asyncReadValues(i).StatusCode.ToString()}")
                    Next

                    Console.WriteLine()
                    Console.WriteLine("press enter to writing synchronous..")
                    Console.ReadLine()

                    'create a WriteValueCollection and fill this with WriteValue objects
                    Dim nodesToWrite As WriteValueCollection = New WriteValueCollection()
                    Dim writeValue As WriteValue = New WriteValue()
                    writeValue.NodeId = New NodeId("ns=2;i=10219") 'Objects.Data.Static.Scalar.Int16Value
                    writeValue.Value = New DataValue(CShort(-16))
                    writeValue.AttributeId = Attributes.Value
                    nodesToWrite.Add(writeValue)
                    writeValue = New WriteValue()
                    writeValue.NodeId = New NodeId("ns=2;i=10221") 'Objects.Data.Static.Scalar.Int32Value
                    writeValue.AttributeId = Attributes.Value
                    writeValue.Value = New DataValue(-3232)
                    nodesToWrite.Add(writeValue)
                    writeValue = New WriteValue()
                    writeValue.NodeId = New NodeId("ns=2;i=10223") 'Objects.Data.Static.Scalar.Int64Value
                    writeValue.AttributeId = Attributes.Value
                    writeValue.Value = New DataValue(CLng(-64646464))
                    nodesToWrite.Add(writeValue)

                    'Write the node values synchronously
                    Dim writeResults As StatusCodeCollection = client.Write(nodesToWrite)

                    For i As Integer = 0 To writeResults.Count - 1
                        If StatusCode.IsGood(writeResults(i)) Then
                            Console.WriteLine($"synchronous write result { nodesToWrite(i).NodeId.ToString() } Value => { nodesToWrite(i).Value.ToString() } StatusCode => { writeResults(i).ToString()}")
                        Else
                            Console.WriteLine($"write failed for { nodesToWrite(i).NodeId.ToString() } StatusCode => { writeResults(i).ToString()}")
                        End If
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
End Class
