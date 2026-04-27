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
' PLCcom OPC UA Client SDK - Workshop 26: Read Attributes
'
' Every OPC UA node has attributes beyond just its Value: NodeClass,
' BrowseName, DisplayName, DataType, AccessLevel and many more.
' This workshop reads all attributes of a node in a single call.
'
' What you will learn:
'   * How to read all attributes of a node (NodeClass through AccessLevelEx)
'   * How to interpret attribute values and data types
'   * How to handle BadAttributeIdInvalid for unsupported attributes
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
             Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 26: Read Attributes     ║")
             Console.WriteLine("║                                                              ║")
             Console.WriteLine("║  Every OPC UA node has attributes beyond just its Value:     ║")
             Console.WriteLine("║  NodeClass, BrowseName, DisplayName, DataType, AccessLevel.  ║")
             Console.WriteLine("║  This workshop reads all attributes of a node in one call.   ║")
             Console.WriteLine("║                                                              ║")
             Console.WriteLine("║  What you will learn:                                        ║")
             Console.WriteLine("║    * Read all attributes (NodeClass through AccessLevelEx)   ║")
             Console.WriteLine("║    * Interpret attribute values and data types               ║")
             Console.WriteLine("║    * Handle BadAttributeIdInvalid for unsupported attrs      ║")
             Console.WriteLine("║                                                              ║")
            Console.WriteLine("║  Required server: Server Workshop 11 (Simple Server)          ║")
            Console.WriteLine("║  opc.tcp://localhost:48410                                    ║")
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝")
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


                        'Read multiple attributes of Node within one call 

                        'define the source NodeId, in  this case s=2:Int16Value 
                        Dim sourceId As NodeId = client.GetNodeIdByPath("Objects.Plant.Line1.Machine1.Temperature")

                        'create a ReadValueIdCollection and fill this with ReadValueId objects
                        Dim nodesToRead As ReadValueIdCollection = New ReadValueIdCollection()

                        For ii As UInteger = Attributes.NodeClass To Attributes.AccessLevelEx
                            Dim nodeToRead As ReadValueId = New ReadValueId()
                            nodeToRead.NodeId = sourceId
                            nodeToRead.AttributeId = ii
                            nodesToRead.Add(nodeToRead)
                        Next


                        'reading the nodes synchronous
                        Console.WriteLine($"Begin reading all attributes of NodeId { sourceId.ToString()}")
                        Dim readresults As DataValueCollection = client.Read(nodesToRead)


                        'print the results
                        For ii As Integer = 0 To readresults.Count - 1


                            ' ignore attributes which are invalid for the node.
                            If readresults(ii).StatusCode = StatusCodes.BadAttributeIdInvalid Then
                                Continue For
                            End If


                            ' get the name of the attribute.
                            Dim attributeName As String = Attributes.GetBrowseName(nodesToRead(ii).AttributeId)
                            Dim datatype As String = String.Empty
                            Dim value As String = String.Empty


                            ' display any unexpected error.
                            If StatusCode.IsBad(readresults(ii).StatusCode) Then
                                datatype = Utils.Format("{0}", Attributes.GetDataTypeId(nodesToRead(ii).AttributeId))

                                ' display the value.
                                value = Utils.Format("{0}", readresults(ii).StatusCode)
                            Else
                                Dim typeInfo As TypeInfo = typeInfo.Construct(readresults(ii).Value)
                                datatype = typeInfo.BuiltInType.ToString()

                                If typeInfo.ValueRank >= ValueRanks.OneOrMoreDimensions Then
                                    datatype += "[]"
                                End If

                                value = Utils.Format("{0}", readresults(ii).Value)
                            End If

                            Console.WriteLine(Utils.Format("read Attribute {0}, DataType => {1}, Value => {2}", attributeName, datatype, value))
                        Next

                        Console.WriteLine()
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
