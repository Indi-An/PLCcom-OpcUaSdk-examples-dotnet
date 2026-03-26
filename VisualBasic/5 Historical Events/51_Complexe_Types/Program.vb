Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Client
Imports PLCcom.Opc.Ua.Client.Sdk
Imports System.Linq
Imports PLCcom.Opc.Ua.Client.ComplexTypes
Imports System.Reflection

Public Class Program
    Private client As UaClient = Nothing

    Private Property Verbose As Boolean = True
    Private Property WriteComplexInt As Boolean = False
    Private allCustomTypeVariables As List(Of INode) = Nothing
    Private allVariableNodes As IList(Of INode) = Nothing

    'actual publishing state of subscription
    Private publishingState As n_PublishingState = n_PublishingState.UNDEFINED

    Public Shared Sub Main(ByVal args As String())
        Dim program As Program = New Program()
        program.Start()
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

                    'disable auto connect functionality
                    sessionConfiguration.AutoConnect = False

                    'Display the certificate store path for debugging purposes
                    Console.WriteLine($"Info: Sessionconfiguration created, certificate store path => { sessionConfiguration.CertificateStorePath}")

                    'Create a new OPC UA client instance with license credentials
                    client = New UaClient(LicenseUserName, LicenseSerial, sessionConfiguration)
                    Console.WriteLine($"Info: license state => { client.GetLicenceMessage()}")
                    Console.WriteLine("")

                    'Register event handlers to monitor the connection state
                    AddHandler client.ServerConnectionLost, AddressOf Client_ServerConnectionLost
                    AddHandler client.ServerConnected, AddressOf Client_ServerConnected
                    AddHandler client.SessionClosing, AddressOf Client_SessionClosing
                    AddHandler client.KeepAlive, AddressOf Client_KeepAlive
                    AddHandler client.CertificateValidation, AddressOf client_CertificateValidation
                    Console.WriteLine("Connect the opc ua client")
                    client.Connect()

                    Try
                        Dim stopWatch As Stopwatch = New Stopwatch()
                        Console.WriteLine("Begin browse all nodes")
                        stopWatch.Start()
                        allVariableNodes = BrowseAllVariables()
                        allCustomTypeVariables = allVariableNodes.Where(Function(n) CType(n, VariableNode).DataType Is DataTypeIds.Structure).ToList()
                        allCustomTypeVariables.AddRange(allVariableNodes.Where(Function(n) CType(n, VariableNode).DataType.NamespaceIndex <> 0).ToList())
                        stopWatch.Stop()
                        Console.WriteLine($" Browse all nodes took {stopWatch.ElapsedMilliseconds}ms.")
                        Console.WriteLine($" Browsed {allVariableNodes.Count} nodes, from which {allCustomTypeVariables.Count} are custom type variables.")
                        Console.WriteLine("Begin load the server type dictionary. This will make all user-defined types known.")
                        stopWatch.Reset()
                        stopWatch.Start()
                        Dim complexTypeSystem = client.GetComplexTypeSystem()
                        complexTypeSystem.Load()
                        stopWatch.Stop()
                        Console.WriteLine($" Load type system took {stopWatch.ElapsedMilliseconds}ms.")
                        Console.WriteLine("Custom types defined for this session:")

                        For Each type In complexTypeSystem.GetDefinedTypes()
                            Console.WriteLine($"{type.Namespace}.{type.Name}")
                        Next

                        Console.WriteLine($" Loaded {client.GetSessionDataTypeSystem().Count} dictionaries:")

                        For Each dictionary In client.GetSessionDataTypeSystem()
                            Console.WriteLine($" + {dictionary.Value.Name}")

                            For Each type In dictionary.Value.DataTypes
                                Console.WriteLine($" -- {type.Key}:{type.Value}")
                            Next
                        Next

                        Console.WriteLine("Begin read all variables with custom type")

                        For Each variableNode As VariableNode In allCustomTypeVariables

                            Try
                                Console.WriteLine($" read variable {variableNode.NodeId.ToString()}")
                                Dim value = client.ReadValue(variableNode.NodeId)
                                CastInt32ToEnum(variableNode, value)
                                Console.WriteLine($" -- {variableNode}:{value}")

                                'get all Extension objects from value
                                Dim allExtensionObjects = GetExtensionObjects(value)

                                For Each extensionObject As ExtensionObject In allExtensionObjects

                                    If extensionObject IsNot Nothing Then
                                        Dim complexType = TryCast(extensionObject.Body, BaseComplexType)

                                        If complexType IsNot Nothing Then

                                            For Each item In complexType.GetPropertyEnumerator()

                                                If Verbose Then
                                                    Console.WriteLine($" -- -- {item.Name}:{complexType(item.Name)}")
                                                End If

                                                If WriteComplexInt AndAlso item.PropertyType Is GetType(Integer) Then
                                                    Dim data = complexType(item.Name)

                                                    If data IsNot Nothing Then
                                                        complexType(item.Name) = CInt(data) + 1
                                                    End If

                                                    Console.WriteLine($" -- -- Write: {item.Name}, {complexType(item.Name)}")
                                                    client.WriteValue(variableNode.NodeId, value)
                                                End If
                                            Next
                                        End If
                                    End If
                                Next

                            Catch sre As ServiceResultException

                                If sre.StatusCode = StatusCodes.BadUserAccessDenied Then
                                    Console.WriteLine($" -- {variableNode}: Access denied!")
                                End If
                            End Try
                        Next

                        Console.WriteLine("Begin monitoring all nodes with custom data type")

                        'Create a new subscription for monitoring data changes
                        Using subscription As Subscription = New Subscription()
                            subscription.PublishingEnabled = True
                            subscription.PublishingInterval = 5000
                            subscription.DisplayName = "mySubsription"

                            'Register subscription state change events
                            AddHandler subscription.StateChanged, AddressOf Subscription_StateChanged
                            AddHandler subscription.PublishStatusChanged, AddressOf Subscription_PublishStatusChanged

                            'Add the subscription to the client instance
                            client.AddSubscription(subscription)
                            Dim list As List(Of MonitoredItem) = New List(Of MonitoredItem)()

                            For Each customVariable In allCustomTypeVariables
                                Dim newItem = New MonitoredItem(subscription.DefaultItem) With {
                                    .DisplayName = customVariable.DisplayName.Text,
                                    .StartNodeId = ExpandedNodeId.ToNodeId(customVariable.NodeId, client.GetNamespaceUris()),
                                    .SamplingInterval = 500,
                                    .QueueSize = UInteger.MaxValue
                                }
                                AddHandler newItem.Notification, AddressOf OnComplexTypeNotification
                                list.Add(newItem)
                            Next

                            subscription.AddItems(list)

                            'Apply all pending changes to the subscription
                            subscription.ApplyChanges()
                            'enable publishing mode of subscription
                            'subscription.SetPublishingMode(true);
                            'subscription.Modify();

                            Console.WriteLine()
                            Console.WriteLine("press enter for exit")
                            Console.ReadLine()
                        End Using

                    Catch ex As Exception
                        Console.WriteLine(ex)
                        Console.WriteLine("press enter for exit")
                        Console.ReadLine()
                    End Try
                Else
                    Console.WriteLine("invalid number of Endpoint")
                    Console.WriteLine("press enter for exit")
                    Console.ReadLine()
                End If
            Else
                Console.WriteLine("no endpoints found")
                Console.WriteLine("press enter for exit")
                Console.ReadLine()
            End If

        Catch ex As Exception
            Console.WriteLine(ex)
            Console.WriteLine("press enter for exit")
            Console.ReadLine()
        End Try
    End Sub

    Private Sub OnComplexTypeNotification(ByVal monitoredItem As MonitoredItem, ByVal e As MonitoredItemNotificationEventArgs)
        Try
            'lock prozedure
            Threading.Monitor.Enter(Me)

            'take variableNode from cache
            Dim variableNode = TryCast(allCustomTypeVariables.Where(Function(n) CType(n, VariableNode).NodeId Is monitoredItem.StartNodeId).FirstOrDefault(), VariableNode)

            If variableNode IsNot Nothing Then

                'loop over all values 
                For Each value In monitoredItem.DequeueValues()
                    Dim successfullyProcessed As Boolean = False

                    If value IsNot Nothing AndAlso value.Value IsNot Nothing AndAlso StatusCode.IsGood(value.StatusCode) Then
                        'cast eventual enum types
                        CastInt32ToEnum(variableNode, value)
                        Console.WriteLine($" -- {variableNode}:{value}")
                        Dim allExtensionObjects = GetExtensionObjects(value)

                        For Each extensionObject As ExtensionObject In allExtensionObjects
                            'check if value a BaseComplexType
                            Dim complexType = TryCast(extensionObject.Body, BaseComplexType)

                            If complexType IsNot Nothing Then

                                'loop over all known propertys
                                For Each item In complexType.GetPropertyEnumerator()
                                    Console.WriteLine($" -- --{monitoredItem.DisplayName} : {item.Name} : Value => {complexType(item.Name)} : SourceTimestamp => {value.SourceTimestamp} : StatusCode => {value.StatusCode}")
                                Next

                                successfullyProcessed = True
                            End If

                            If Not successfullyProcessed Then
                                'simple print, value is not a known BaseComplexType 
                                Console.WriteLine("{0}: {1}, {2}", monitoredItem.DisplayName, value.SourceTimestamp, value.StatusCode)
                                If Verbose Then Console.WriteLine(value)
                            End If
                        Next
                    End If

                    'simple print, value is not a known BaseComplexType 
                    Dim notification As MonitoredItemNotification = TryCast(e.NotificationValue, MonitoredItemNotification)
                    Console.WriteLine("{0}: {1}, {2}", monitoredItem.DisplayName, notification.Value.SourceTimestamp, notification.Value.StatusCode)
                    If Verbose Then Console.WriteLine(notification.Value)
                Next
            Else
                'simple print, value is not a known variableNode 
                Dim notification As MonitoredItemNotification = TryCast(e.NotificationValue, MonitoredItemNotification)
                Console.WriteLine("{0}: {1}, {2}", monitoredItem.DisplayName, notification.Value.SourceTimestamp, notification.Value.StatusCode)
                If Verbose Then Console.WriteLine(notification.Value)
            End If

        Catch ex As Exception
            Console.WriteLine(ex)
        Finally
            Threading.Monitor.Exit(Me)
        End Try
    End Sub

    Private Function GetExtensionObjects(ByVal value As DataValue) As List(Of ExtensionObject)
        Dim allExtensionObjects As List(Of ExtensionObject) = New List(Of ExtensionObject)()

        If value IsNot Nothing AndAlso value.Value IsNot Nothing AndAlso StatusCode.IsGood(value.StatusCode) Then

            'check if value a ExtensionObject or a array of ExtensionObject
            If value.Value.GetType().IsArray Then
                Dim extensionObjects = TryCast(value.Value, ExtensionObject())
                If extensionObjects IsNot Nothing Then allExtensionObjects.AddRange(extensionObjects)
            Else
                Dim extensionObject = TryCast(value.Value, ExtensionObject)
                If extensionObject IsNot Nothing Then allExtensionObjects.Add(extensionObject)
            End If
        End If

        Return allExtensionObjects
    End Function

    Private Sub Subscription_StateChanged(ByVal subscription As Subscription, ByVal e As SubscriptionStateChangedEventArgs)
        Console.WriteLine($"{Date.Now.ToLocalTime() } State of Subscription { UaClient.SubscriptionToString(subscription) } changed to => { e.Status.ToString()}")
    End Sub

    Private Sub Subscription_PublishStatusChanged(ByVal sender As Object, ByVal e As EventArgs)
        ' 
        ' check your publish state of your subscription
        ' if the publish state permanent stopped, then you have to recreate your subscription with old subscription as template
        ' In this case, please have a look to the PublishingInterval setting, possibly be the value must be increased
        ' 

        Dim subscription As Subscription = TryCast(sender, Subscription)

        If subscription IsNot Nothing Then
            Dim currentpublishingState As n_PublishingState = If(subscription.PublishingStopped, n_PublishingState.STOPPED, n_PublishingState.RUNNING)
            If currentpublishingState <> publishingState OrElse currentpublishingState = n_PublishingState.STOPPED Then _
                Console.WriteLine($"{Date.Now.ToLocalTime() } Publishing state of Subscription { UaClient.SubscriptionToString(CType(sender, Subscription)) } => { currentpublishingState.ToString()}")
            publishingState = currentpublishingState
        End If
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

    ''' <summary>
    ''' Helper to cast a enumeration node value to an enumeration type.
    ''' </summary>
    Private Sub CastInt32ToEnum(ByVal variableNode As VariableNode, ByVal value As DataValue)
        If value.Value?.GetType() Is GetType(Integer) Then
            ' test if this is an enum datatype?
            Dim systemType As Type = client.GetSession().Factory.GetSystemType(NodeId.ToExpandedNodeId(variableNode.DataType, client.GetNamespaceUris()))

            If systemType IsNot Nothing Then
                value.Value = [Enum].ToObject(systemType, value.Value)
            End If
        End If
    End Sub

    ''' <summary>
    ''' Browse all variables in the objects folder.
    ''' </summary>
    Private Function BrowseAllVariables() As IList(Of INode)
        Dim result = New List(Of INode)()
        Dim nodesToBrowse = New ExpandedNodeIdCollection()
        nodesToBrowse.Add(ObjectIds.ObjectsFolder)

        While nodesToBrowse.Count > 0
            Dim nextNodesToBrowse = New ExpandedNodeIdCollection()

            For Each node In nodesToBrowse

                Try
                    Dim organizers = client.GetNodeCache().FindReferencesAsync(node, ReferenceTypeIds.Organizes, False, False).GetAwaiter().GetResult()
                    Dim components = client.GetNodeCache().FindReferencesAsync(node, ReferenceTypeIds.HasComponent, False, False).GetAwaiter().GetResult()
                    Dim properties = client.GetNodeCache().FindReferencesAsync(node, ReferenceTypeIds.HasProperty, False, False).GetAwaiter().GetResult()
                    nextNodesToBrowse.AddRange(organizers.Where(Function(n) TypeOf n Is ObjectNode).[Select](Function(n) n.NodeId).ToList())
                    nextNodesToBrowse.AddRange(components.Where(Function(n) TypeOf n Is ObjectNode).[Select](Function(n) n.NodeId).ToList())
                    result.AddRange(organizers.Where(Function(n) TypeOf n Is VariableNode))
                    result.AddRange(components.Where(Function(n) TypeOf n Is VariableNode))
                    result.AddRange(properties.Where(Function(n) TypeOf n Is VariableNode))
                Catch sre As ServiceResultException

                    If sre.StatusCode = StatusCodes.BadUserAccessDenied Then
                        Console.WriteLine($"Access denied: Skip node {node}.")
                    End If
                End Try
            Next

            nodesToBrowse = nextNodesToBrowse
        End While

        Return result
    End Function

    Private Enum n_PublishingState
        UNDEFINED
        RUNNING
        STOPPED
    End Enum
End Class
