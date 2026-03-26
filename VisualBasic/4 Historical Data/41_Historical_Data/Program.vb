Imports PLCcom.Opc.Ua.Client.Sdk
Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text
Imports PLCcom.Opc.Ua
Imports PLCcom.Opc.Ua.Client
Imports System.Reflection

Public Class Program
    'define the ua client
    Private client As UaClient = Nothing

    Public Shared Sub Main(ByVal args As String())
        Dim program As Program = New Program()
        program.Start()
    End Sub

    Private Sub Start()
        Try

            'Submit your license information from your license e-mail
            Dim LicenseUserName As String = "<Enter your UserName here>"
            Dim LicenseSerial As String = "<Enter your Serial here>"

            Dim Endpoints As EndpointDescriptionCollection = UaClient.GetEndpoints(New Uri("opc.tcp://localhost:50530/PLCcom/HistoricalAccessServer"), 60000)

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
                    client = New UaClient(LicenseUserName, LicenseSerial, sessionConfiguration)
                    Console.WriteLine($"Info: license state => { client.GetLicenceMessage()}")

                    'Register event handlers to monitor the connection state
                    AddHandler client.ServerConnectionLost, AddressOf Client_ServerConnectionLost
                    AddHandler client.ServerConnected, AddressOf Client_ServerConnected
                    AddHandler client.SessionClosing, AddressOf Client_SessionClosing
                    AddHandler client.KeepAlive, AddressOf Client_KeepAlive
                    AddHandler client.CertificateValidation, AddressOf client_CertificateValidation
                    Console.WriteLine(client.GetSessionState().ToString())
                    Console.WriteLine()

                    'Create and add a subscription
                    Dim subscription As Subscription = New Subscription()
                    subscription.PublishingInterval = 1000
                    subscription.PublishingEnabled = False
                    subscription.DisplayName = "mySubsription"

                    'Register subscription state change events
                    AddHandler subscription.StateChanged, AddressOf Subscription_StateChanged
                    subscription.PublishingEnabled = True

                    'Add the subscription to the client instance
                    client.AddSubscription(subscription)

                    Do
                        Dim commandList As StringBuilder = New StringBuilder()
                        commandList.Append("Please Enter a Command....")
                        commandList.Append(Environment.NewLine)
                        Dim enumValueArray As Array = [Enum].GetValues(GetType(HistoryReadOperation))

                        For Each enumValue As Integer In enumValueArray
                            commandList.Append($"{enumValue.ToString() } - { [Enum].GetName(GetType(HistoryReadOperation), enumValue)}")
                            commandList.Append(Environment.NewLine)
                        Next

                        Console.WriteLine(commandList)
                        Dim mode As String = Console.ReadLine()
                        If String.IsNullOrEmpty(mode) Then Exit Do

                        'set target NodeId
                        Dim nodeId As NodeId = New NodeId("ns=2;s=1:PLCcom.HistoricalAccessServer.Data.Dynamic.Int64.txt")

                        If nodeId IsNot Nothing Then

                            Try
                                Dim values As HistoryData = Nothing

                                Select Case mode
                                    Case "1" ' - Subscribe
                                        Dim monitoredItem As MonitoredItem = New MonitoredItem(DirectCast(Nothing, ITelemetryContext))
                                        monitoredItem.StartNodeId = nodeId
                                        monitoredItem.AttributeId = Attributes.Value
                                        monitoredItem.MonitoringMode = MonitoringMode.Reporting
                                        monitoredItem.SamplingInterval = 500
                                        monitoredItem.QueueSize = UInteger.MaxValue
                                        monitoredItem.DiscardOldest = True
                                        monitoredItem.DisplayName = monitoredItem.StartNodeId.ToString()

                                        'Register the notification callback for value changes
                                        AddHandler monitoredItem.Notification, AddressOf Client_MonitorNotification

                                        'Add the monitored item to the subscription
                                        subscription.AddItem(monitoredItem)

                                        'Apply all pending changes to the subscription
                                        subscription.ApplyChanges()
                                        Console.ReadLine()
                                    Case "2" ' - Raw
                                        values = client.ReadRaw(nodeId, Date.Now.AddDays(-1), Date.Now, False)

                                        For Each value As DataValue In values.DataValues
                                            Console.WriteLine($"{value.SourceTimestamp.ToLocalTime() } Value => { value.Value } StatusCode => { value.StatusCode}")
                                        Next

                                        Console.WriteLine(String.Empty)
                                    Case "3" ' - Modified
                                        values = client.ReadRaw(nodeId, Date.Now.AddDays(-1), Date.Now, True)

                                        For Each value As DataValue In values.DataValues
                                            Console.WriteLine($"{value.SourceTimestamp.ToLocalTime() } Value => { value.Value } StatusCode => { value.StatusCode}")
                                        Next

                                        Console.WriteLine(String.Empty)
                                    Case "4" ' - AtTime
                                        values = client.ReadAtTime(nodeId, Date.Now.AddHours(-2), 10, 10000, False)

                                        For Each value As DataValue In values.DataValues
                                            Console.WriteLine($"{value.SourceTimestamp.ToLocalTime() } Value => { value.Value } StatusCode => { value.StatusCode}")
                                        Next

                                        Console.WriteLine(String.Empty)
                                    Case "5" ' - Processed
                                        values = client.ReadProcessed(nodeId, client.GetAvailableAggregates()("Interpolative"), Date.Now.AddHours(-4), Date.Now.AddHours(-2), 5000)

                                        For Each value As DataValue In values.DataValues
                                            Console.WriteLine($"{value.SourceTimestamp.ToLocalTime() } Value => { value.Value } StatusCode => { value.StatusCode}")
                                        Next

                                        Console.WriteLine(String.Empty)
                                    Case "6" ' - Insert
                                        Dim HistoryValues As List(Of DataValue) = New List(Of DataValue)()
                                        Dim historyData As DataValue = New DataValue()
                                        Dim UpdateResult As HistoryUpdateResultCollection = client.Insert(nodeId, HistoryValues)
                                        historyData.SourceTimestamp = Date.Now.ToUniversalTime()
                                        historyData.ServerTimestamp = Date.Now.ToUniversalTime()
                                        historyData.StatusCode = New StatusCode(StatusCodes.GoodEntryInserted)
                                        Console.WriteLine("Please enter a value...")
                                        historyData.Value = Console.ReadLine()
                                        HistoryValues.Add(historyData)
                                        Console.WriteLine($"StatusCode => { UpdateResult(0).OperationResults(0).ToString()}")
                                        Console.WriteLine()
                                    Case "7" ' - Update
                                        Dim HistoryValues As List(Of DataValue) = New List(Of DataValue)()
                                        Dim historyData As DataValue = New DataValue()
                                        Dim UpdateResult As HistoryUpdateResultCollection = client.Insert(nodeId, HistoryValues)
                                        HistoryValues = New List(Of DataValue)()
                                        historyData.SourceTimestamp = Date.Now.ToUniversalTime()
                                        historyData.ServerTimestamp = Date.Now.ToUniversalTime()
                                        historyData.StatusCode = New StatusCode(StatusCodes.GoodEntryInserted)
                                        Console.WriteLine("Please enter a value...")
                                        historyData.Value = Console.ReadLine()
                                        HistoryValues.Add(historyData)
                                        UpdateResult = client.Update(nodeId, HistoryValues)
                                        Console.WriteLine($"StatusCode => { UpdateResult(CInt(0)).OperationResults(CInt(0)).ToString()}")
                                        Console.WriteLine()
                                    Case "8" ' - Replace
                                        Dim HistoryValues As List(Of DataValue) = New List(Of DataValue)()
                                        Dim historyData As DataValue = New DataValue()
                                        Dim UpdateResult As HistoryUpdateResultCollection = client.Insert(nodeId, HistoryValues)
                                        historyData.SourceTimestamp = Date.Now.ToUniversalTime()
                                        historyData.ServerTimestamp = Date.Now.ToUniversalTime()
                                        historyData.StatusCode = New StatusCode(StatusCodes.GoodEntryInserted)
                                        Console.WriteLine("Please enter a value...")
                                        historyData.Value = Console.ReadLine()
                                        HistoryValues.Add(historyData)
                                        UpdateResult = client.Replace(nodeId, HistoryValues)
                                        Console.WriteLine($"StatusCode => { UpdateResult(CInt(0)).OperationResults(CInt(0)).ToString()}")
                                        Console.WriteLine()
                                    Case "9" ' - Remove
                                        Dim HistoryValues As List(Of DataValue) = New List(Of DataValue)()
                                        Dim historyData As DataValue = New DataValue()
                                        Dim UpdateResult As HistoryUpdateResultCollection = client.Insert(nodeId, HistoryValues)
                                        historyData.SourceTimestamp = Date.Now.ToUniversalTime()
                                        historyData.ServerTimestamp = Date.Now.ToUniversalTime()
                                        historyData.StatusCode = New StatusCode(StatusCodes.GoodEntryInserted)
                                        Console.WriteLine("Please enter a value...")
                                        historyData.Value = Console.ReadLine()
                                        HistoryValues.Add(historyData)
                                        UpdateResult = client.Remove(nodeId, HistoryValues)
                                        Console.WriteLine($"StatusCode => { UpdateResult(CInt(0)).OperationResults(CInt(0)).ToString()}")
                                        Console.WriteLine()
                                    Case "10" ' - DeleteRaw
                                        Dim results As HistoryUpdateResultCollection = client.DeleteRaw(nodeId, Date.Now.AddHours(-4), Date.Now.AddHours(-2), False)

                                        For Each value As HistoryUpdateResult In results
                                            Console.WriteLine($"StatusCode => { value.StatusCode.ToString()}")
                                        Next

                                        Console.WriteLine(String.Empty)
                                    Case "11" ' - DeleteModified
                                        Dim results As HistoryUpdateResultCollection = client.DeleteRaw(nodeId, Date.Now.AddHours(-4), Date.Now.AddHours(-2), True)

                                        For Each value As HistoryUpdateResult In results
                                            Console.WriteLine($"StatusCode => { value.StatusCode.ToString()}")
                                        Next

                                        Console.WriteLine(String.Empty)
                                    Case "12" ' - DeleteAtTime
                                        Dim results As HistoryUpdateResultCollection = client.DeleteAtTime(nodeId, Date.Now.AddHours(-4), 10, 5000)

                                        For Each value As HistoryUpdateResult In results
                                            Console.WriteLine($"StatusCode => { value.StatusCode.ToString()}")
                                        Next

                                        Console.WriteLine(String.Empty)
                                    Case "13" 'Exit
                                        Return
                                End Select

                            Catch ex As Exception
                                Console.WriteLine(ex)
                                Console.WriteLine()
                            End Try
                        End If
                    Loop While True
                Else
                    Console.WriteLine("invalid number of Endpoint")
                    Console.WriteLine()
                    Console.WriteLine("press enter for exit")
                    Console.ReadLine()
                End If
            Else
                Console.WriteLine("no endpoints found")
                Console.WriteLine()
                Console.WriteLine("press enter for exit")
                Console.ReadLine()
            End If

        Catch ex As Exception
            Console.WriteLine(ex)
            Console.WriteLine("press enter for exit")
            Console.ReadLine()
        Finally
            'Disconnect the current session
            If client IsNot Nothing AndAlso client.GetSessionState().Equals(SessionState.Connected) Then client.Disconnect()
        End Try
    End Sub

    Private Sub Client_MonitorNotification(ByVal monitoredItem As MonitoredItem, ByVal e As MonitoredItemNotificationEventArgs)
        Dim notification As MonitoredItemNotification = TryCast(e.NotificationValue, MonitoredItemNotification)
        Console.WriteLine($"{monitoredItem.StartNodeId.ToString()} Value {notification.Value} Status: { notification.Value.StatusCode.ToString()}")
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

    Private Sub Subscription_StateChanged(ByVal subscription As Subscription, ByVal e As SubscriptionStateChangedEventArgs)
        Console.WriteLine($"{Date.Now.ToLocalTime() } State of Subscription { UaClient.SubscriptionToString(subscription) } changed to => { e.Status.ToString()}")
    End Sub
End Class
