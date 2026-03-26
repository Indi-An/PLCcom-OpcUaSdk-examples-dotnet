Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Client
Imports PLCcom.Opc.Ua.Client.Sdk
Imports System
Imports System.Reflection
Imports TypeInfo = PLCcom.Opc.Ua.TypeInfo

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

                        'sample nodeIds to register
                        Dim nodetoRegister As NodeIdCollection = New NodeIdCollection()
                        nodetoRegister.Add(New NodeId("ns=2;i=10221")) 'Objects.Data.Static.Scalar.Int32Value by plccom demonstration dataaccess server
                        nodetoRegister.Add(client.GetNodeIdByPath("Objects.Data.Static.Scalar.Int16Value"))
                        'nodetoRegister.Add(your nodeid) 
                        'nodetoRegister.Add(your nodeid)
                        'nodetoRegister.Add(your nodeid)

                        Dim registeredNodeIds As NodeIdCollection = Nothing
                        Dim req As RegisterNodesRequest = New RegisterNodesRequest()
                        req.NodesToRegister = nodetoRegister

                        'register Nodes
                        Dim res As RegisterNodesResponse = client.RegisterNodes(req)
                        Console.WriteLine(Utils.Format("Nodes with Statuscode => {0} registered", res.ResponseHeader.ServiceResult))

                        If StatusCode.IsGood(res.ResponseHeader.ServiceResult) Then
                            registeredNodeIds = res.RegisteredNodeIds

                            'write your registered node
                            Dim sc As StatusCode = client.Write(registeredNodeIds(0), 12345, Attributes.Value)
                            Console.WriteLine(Utils.Format("write Node {0} Statuscode => {1}", registeredNodeIds(0).ToString(), sc.ToString()))

                            'copy NodeIdCollection to ReadValueIdCollection
                            Dim readValueIdCollection As ReadValueIdCollection = New ReadValueIdCollection()

                            For i As Integer = 0 To registeredNodeIds.Count - 1
                                Dim rvi As ReadValueId = New ReadValueId() With {
                                    .AttributeId = Attributes.Value,
                                    .NodeId = registeredNodeIds(i)
                                }
                                readValueIdCollection.Add(rvi)
                            Next

                            'read your registered nodes
                            Dim readresults As DataValueCollection = client.Read(readValueIdCollection)

                            'print the results
                            For ii As Integer = 0 To readresults.Count - 1

                                ' ignore attributes which are invalid for the node.
                                If readresults(ii).StatusCode = StatusCodes.BadAttributeIdInvalid Then
                                    Continue For
                                End If

                                Dim datatype As String = String.Empty
                                Dim value As String = String.Empty

                                ' display any unexpected error.
                                If StatusCode.IsBad(readresults(ii).StatusCode) Then
                                    value = Utils.Format("{0}", readresults(ii).StatusCode)
                                Else
                                    Dim typeInfo As TypeInfo = typeInfo.Construct(readresults(ii).Value)
                                    datatype = typeInfo.BuiltInType.ToString()

                                    If typeInfo.ValueRank >= ValueRanks.OneOrMoreDimensions Then
                                        datatype += "[]"
                                    End If

                                    value = Utils.Format("{0}", readresults(ii).Value)
                                End If

                                Console.WriteLine(Utils.Format("read value, DataType => {0}, Value => {1}", datatype, value))
                            Next
                        Else
                            Console.WriteLine(Utils.Format("Operation RegisterNode failed with StatusCode => {0}", res.ResponseHeader.ServiceResult.ToString()))
                        End If

                        'unregister Nodes
                        If registeredNodeIds IsNot Nothing Then
                            Dim ureq As UnregisterNodesRequest = New UnregisterNodesRequest()
                            ureq.NodesToUnregister = registeredNodeIds
                            Dim ures As UnregisterNodesResponse = client.UnregisterNodes(ureq)
                            Console.WriteLine(Utils.Format("Nodes with Statuscode => {0} unregistered", ures.ResponseHeader.ServiceResult.ToString()))
                            Console.WriteLine()
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
