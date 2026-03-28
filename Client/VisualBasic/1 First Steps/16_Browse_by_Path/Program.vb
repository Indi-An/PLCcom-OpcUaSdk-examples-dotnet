Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Client.Sdk
Imports System
Imports PLCcom.Opc.Ua.Client
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
                    Console.WriteLine($"{Math.Min(Threading.Interlocked.Increment(counter), counter - 1).ToString() } => { UaClient.EndpointToString(Endpoint)}")
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
                    Using client As UaClient = New UaClient(LicenseUserName, LicenseSerial, sessionConfiguration)
                        Console.WriteLine($"Info: license state => {client.GetLicenceMessage()}")

                        'Register event handlers to monitor the connection state
                        AddHandler client.ServerConnectionLost, AddressOf Client_ServerConnectionLost
                        AddHandler client.ServerConnected, AddressOf Client_ServerConnected
                        AddHandler client.KeepAlive, AddressOf Client_KeepAlive
                        AddHandler client.CertificateValidation, AddressOf client_CertificateValidation
                        Console.WriteLine("")
                        Console.WriteLine("please enter browse path (nothing for root or 'exit' for exit program)")

                        Try
                            Dim sourceNode As NodeId = client.GetNodeIdByPath("Objects.Data.Dynamic.Scalar")

                            'Set start NodeId by path
                            ' find all of the components of the node.
                            Dim nodeToBrowse1 As BrowseDescription = New BrowseDescription()
                            nodeToBrowse1.NodeId = sourceNode
                            nodeToBrowse1.BrowseDirection = BrowseDirection.Forward
                            nodeToBrowse1.ReferenceTypeId = ReferenceTypeIds.Aggregates
                            nodeToBrowse1.IncludeSubtypes = True
                            nodeToBrowse1.NodeClassMask = CUInt(NodeClass.Object Or NodeClass.Variable)
                            nodeToBrowse1.ResultMask = CUInt(BrowseResultMask.All)

                            ' find all nodes organized by the node.
                            Dim nodeToBrowse2 As BrowseDescription = New BrowseDescription()
                            nodeToBrowse2.NodeId = sourceNode
                            nodeToBrowse2.BrowseDirection = BrowseDirection.Forward
                            nodeToBrowse2.ReferenceTypeId = ReferenceTypeIds.Organizes
                            nodeToBrowse2.IncludeSubtypes = True
                            nodeToBrowse2.NodeClassMask = CUInt(NodeClass.Object Or NodeClass.Variable)
                            nodeToBrowse2.ResultMask = CUInt(BrowseResultMask.All)
                            Dim nodesToBrowse As BrowseDescriptionCollection = New BrowseDescriptionCollection()
                            nodesToBrowse.Add(nodeToBrowse1)
                            nodesToBrowse.Add(nodeToBrowse2)

                            'now, browse the node
                            Dim rdc As ReferenceDescriptionCollection = client.BrowseFull(nodesToBrowse)

                            If rdc.Count > 0 Then

                                For Each rd As ReferenceDescription In rdc
                                    Console.WriteLine($"Child NodeID found => {rd.NodeId} NodeClass => {rd.NodeClass.ToString()} BrowseName => {rd.BrowseName.ToString()} DisplayName => {rd.DisplayName.ToString()}")
                                Next
                            Else
                                Console.WriteLine("no references found")
                            End If

                        Catch ex As Exception
                            Console.WriteLine(ex)
                            Console.WriteLine()
                        End Try
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
End Class
