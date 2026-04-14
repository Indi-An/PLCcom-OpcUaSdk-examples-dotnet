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
' PLCcom OPC UA Client SDK - Workshop 27: Registered Read and Write
'
' RegisterNodes tells the server to optimize access to specific nodes.
' The server may cache internal references, making subsequent read/write
' operations faster. This is useful for high-frequency data access.
' Always call UnregisterNodes when done.
'
' What you will learn:
'   * How to register nodes for optimized access (RegisterNodes)
'   * How to read and write using registered NodeIds
'   * How to unregister nodes when done (UnregisterNodes)
'   * When registered access improves performance
'
' Target server: opc.tcp://localhost:48410
' ==============================================================================

Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Client
Imports PLCcom.Opc.Ua.Client.Sdk
Imports System
Imports System.Reflection
Imports TypeInfo = PLCcom.Opc.Ua.TypeInfo

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
             Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 27: Registered R/W      ║")
             Console.WriteLine("║                                                              ║")
             Console.WriteLine("║  RegisterNodes tells the server to optimize access to        ║")
             Console.WriteLine("║  specific nodes. The server caches internal references,      ║")
             Console.WriteLine("║  making subsequent read/write operations faster.             ║")
             Console.WriteLine("║                                                              ║")
             Console.WriteLine("║  What you will learn:                                        ║")
             Console.WriteLine("║    * Register nodes for optimized access                     ║")
             Console.WriteLine("║    * Read and write using registered NodeIds                 ║")
             Console.WriteLine("║    * Unregister nodes when done                              ║")
             Console.WriteLine("╚══════════════════════════════════════════════════════════════╝")
             Console.WriteLine()

            'TODO
            'Submit your license information from your license e-mail
            Dim LicenseUserName As String = "<Enter your UserName here>"
            Dim LicenseSerial As String = "<Enter your Serial here>"
            Dim Endpoints As EndpointDescriptionCollection = UaClient.GetEndpoints(New Uri("opc.tcp://localhost:48410"), 60000)

            'sort endpoints by security level
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
                    'create a a SessionConfiguration with the selected endpoint and application name
                    Dim sessionConfiguration As SessionConfiguration = SessionConfiguration.Build(Assembly.GetEntryAssembly().GetName().Name, Endpoints(iNumberOfEndpoint))

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

                        'sample nodeIds to register
                        Dim nodetoRegister As NodeIdCollection = New NodeIdCollection()
                        nodetoRegister.Add(client.GetNodeIdByPath("Objects.Plant.Line1.Machine1.RPM")) 'Objects.Plant.Line1.Machine1.RPM
                        nodetoRegister.Add(client.GetNodeIdByPath("Objects.Plant.Line1.Machine1.Temperature"))
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
                            Dim sc As StatusCode = client.Write(registeredNodeIds(0), 1750, Attributes.Value)
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
End Class
