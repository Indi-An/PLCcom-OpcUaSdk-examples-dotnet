using PLCcom.Opc.Ua.Client.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Client;

class Program
{
    //define the ua client
    UaClient client = null;

    static void Main(string[] args)
    {
        Program program = new Program();
        program.Start();
    }
    void Start()
    {
        try
        {

            //Submit your license information from your license e-mail
            string LicenseUserName = "<Enter your UserName here>";
            string LicenseSerial = "<Enter your Serial here>";

            EndpointDescriptionCollection Endpoints = UaClient.GetEndpoints(new Uri("opc.tcp://localhost:50530/PLCcom/HistoricalAccessServer"), 60000);

            // Sort endpoints by security level (highest security first)
            Endpoints = UaClient.SortEndpointsBySecurityLevel(Endpoints);

            if (Endpoints.Count > 0)
            {
                Console.WriteLine("endpoints found:");
                int counter = 0;
                foreach (EndpointDescription Endpoint in Endpoints)
                {
                    Console.WriteLine(counter++.ToString() + " => " + UaClient.EndpointToString(Endpoint));
                }

                Console.WriteLine("please enter index of desired endpoint");
                string NumberOfEndpoint = Console.ReadLine();
                Console.WriteLine("");

                int iNumberOfEndpoint = -1;
                if (int.TryParse(NumberOfEndpoint, out iNumberOfEndpoint) && iNumberOfEndpoint > -1 && iNumberOfEndpoint < Endpoints.Count)
                {
                    // Create a SessionConfiguration with the selected endpoint and application name
                    SessionConfiguration sessionConfiguration = SessionConfiguration.Build(System.Reflection.Assembly.GetEntryAssembly().GetName().Name,
                                                                                          Endpoints[iNumberOfEndpoint]);

                    // Enable AutoConnect - the client will connect and reconnect automatically
                    sessionConfiguration.AutoConnect = true;

                    // Display the certificate store path for debugging purposes
                    Console.WriteLine("Info: Sessionconfiguration created, certificate store path => " + sessionConfiguration.CertificateStorePath);

                    // Create a new OPC UA client instance with license credentials
                    client = new UaClient(LicenseUserName, LicenseSerial, sessionConfiguration);
                    Console.WriteLine("Info: license state => " + client.GetLicenceMessage());

                    // Register event handlers to monitor the connection state
                    client.ServerConnectionLost += Client_ServerConnectionLost;
                    client.ServerConnected += Client_ServerConnected;
                    client.SessionClosing += Client_SessionClosing;
                    client.KeepAlive += Client_KeepAlive;
                    client.CertificateValidation += client_CertificateValidation;

                    Console.WriteLine(client.GetSessionState().ToString());
                    Console.WriteLine();

                    //Create and add a subscription
                    Subscription subscription = new Subscription();
                    subscription.PublishingInterval = 1000;
                    subscription.PublishingEnabled = false;
                    subscription.DisplayName = "mySubsription";

                    // Register subscription state change events
                    subscription.StateChanged += Subscription_StateChanged;
                    subscription.PublishingEnabled = true;

                    // Add the subscription to the client instance
                    client.AddSubscription(subscription);

                    do
                    {
                        StringBuilder commandList = new StringBuilder();
                        commandList.Append("Please Enter a Command....");
                        commandList.Append(Environment.NewLine);
                        Array enumValueArray = Enum.GetValues(typeof(HistoryReadOperation));
                        foreach (int enumValue in enumValueArray)
                        {
                            commandList.Append(enumValue.ToString() + " - " + Enum.GetName(typeof(HistoryReadOperation), enumValue));
                            commandList.Append(Environment.NewLine);
                        }

                        Console.WriteLine(commandList);

                        string mode = Console.ReadLine();
                        if (string.IsNullOrEmpty(mode)) break;

                        //set target NodeId
                        NodeId nodeId = new NodeId("ns=2;s=1:PLCcom.HistoricalAccessServer.Data.Dynamic.Int64.txt");

                        if (nodeId != null)
                        {
                            try
                            {
                                HistoryData values = null;
                                switch (mode)
                                {
                                    case "1":// - Subscribe
                                        MonitoredItem monitoredItem = new MonitoredItem((ITelemetryContext)null);
                                        monitoredItem.StartNodeId = nodeId;
                                        monitoredItem.AttributeId = Attributes.Value;
                                        monitoredItem.MonitoringMode = MonitoringMode.Reporting;
                                        monitoredItem.SamplingInterval = 500;
                                        monitoredItem.QueueSize = UInt32.MaxValue;
                                        monitoredItem.DiscardOldest = true;
                                        monitoredItem.DisplayName = monitoredItem.StartNodeId.ToString();

                                        // Register the notification callback for value changes
                                        monitoredItem.Notification += Client_MonitorNotification;

                                        // Add the monitored item to the subscription
                                        subscription.AddItem(monitoredItem);

                                        // Apply all pending changes to the subscription (creates monitored items on the server)
                                        subscription.ApplyChanges();
                                        Console.ReadLine();
                                        break;
                                    case "2":// - Raw
                                        values = client.ReadRaw(nodeId,
                                                                DateTime.Now.AddDays(-1),
                                                                DateTime.Now,
                                                                false);
                                        foreach (DataValue value in values.DataValues)
                                        {
                                            Console.WriteLine(value.SourceTimestamp.ToLocalTime() + " Value => " + value.Value + " StatusCode => " + value.StatusCode);
                                        }
                                        Console.WriteLine(string.Empty);
                                        break;
                                    case "3":// - Modified
                                        values = client.ReadRaw(nodeId,
                                                                DateTime.Now.AddDays(-1),
                                                                DateTime.Now,
                                                                true);
                                        foreach (DataValue value in values.DataValues)
                                        {
                                            Console.WriteLine(value.SourceTimestamp.ToLocalTime() + " Value => " + value.Value + " StatusCode => " + value.StatusCode);
                                        }
                                        Console.WriteLine(string.Empty);
                                        break;
                                    case "4":// - AtTime
                                        values = client.ReadAtTime(nodeId,
                                                                    DateTime.Now.AddHours(-2),
                                                                    10,
                                                                    10000,
                                                                    false);
                                        foreach (DataValue value in values.DataValues)
                                        {
                                            Console.WriteLine(value.SourceTimestamp.ToLocalTime() + " Value => " + value.Value + " StatusCode => " + value.StatusCode);
                                        }
                                        Console.WriteLine(string.Empty);
                                        break;
                                    case "5":// - Processed
                                        values = client.ReadProcessed(nodeId,
                                                                    client.GetAvailableAggregates()["Interpolative"],
                                                                    DateTime.Now.AddHours(-4),
                                                                    DateTime.Now.AddHours(-2),
                                                                    5000);
                                        foreach (DataValue value in values.DataValues)
                                        {
                                            Console.WriteLine(value.SourceTimestamp.ToLocalTime() + " Value => " + value.Value + " StatusCode => " + value.StatusCode);
                                        }
                                        Console.WriteLine(string.Empty);
                                        break;
                                    case "6":// - Insert
                                        List<DataValue> HistoryValues = new List<DataValue>();
                                        DataValue historyData = new DataValue();
                                        historyData.SourceTimestamp = DateTime.Now.ToUniversalTime();
                                        historyData.ServerTimestamp = DateTime.Now.ToUniversalTime();
                                        historyData.StatusCode = new StatusCode(StatusCodes.GoodEntryInserted);
                                        Console.WriteLine("Please enter a value...");
                                        historyData.Value = Console.ReadLine();
                                        HistoryValues.Add(historyData);
                                        HistoryUpdateResultCollection UpdateResult = client.Insert(nodeId, HistoryValues);
                                        Console.WriteLine("StatusCode => " + UpdateResult[0].OperationResults[0].ToString());
                                        Console.WriteLine();
                                        break;
                                    case "7":// - Update
                                        HistoryValues = new List<DataValue>();
                                        historyData = new DataValue();
                                        historyData.SourceTimestamp = DateTime.Now.ToUniversalTime();
                                        historyData.ServerTimestamp = DateTime.Now.ToUniversalTime();
                                        historyData.StatusCode = new StatusCode(StatusCodes.GoodEntryInserted);
                                        Console.WriteLine("Please enter a value...");
                                        historyData.Value = Console.ReadLine();
                                        HistoryValues.Add(historyData);
                                        UpdateResult = client.Update(nodeId, HistoryValues);
                                        Console.WriteLine("StatusCode => " + UpdateResult[0].OperationResults[0].ToString());
                                        Console.WriteLine();
                                        break;
                                    case "8":// - Replace
                                        HistoryValues = new List<DataValue>();
                                        historyData = new DataValue();
                                        historyData.SourceTimestamp = DateTime.Now.ToUniversalTime();
                                        historyData.ServerTimestamp = DateTime.Now.ToUniversalTime();
                                        historyData.StatusCode = new StatusCode(StatusCodes.GoodEntryInserted);
                                        Console.WriteLine("Please enter a value...");
                                        historyData.Value = Console.ReadLine();
                                        HistoryValues.Add(historyData);
                                        UpdateResult = client.Replace(nodeId, HistoryValues);
                                        Console.WriteLine("StatusCode => " + UpdateResult[0].OperationResults[0].ToString());
                                        Console.WriteLine();
                                        break;
                                    case "9":// - Remove
                                        HistoryValues = new List<DataValue>();
                                        historyData = new DataValue();
                                        historyData.SourceTimestamp = DateTime.Now.ToUniversalTime();
                                        historyData.ServerTimestamp = DateTime.Now.ToUniversalTime();
                                        historyData.StatusCode = new StatusCode(StatusCodes.GoodEntryInserted);
                                        Console.WriteLine("Please enter a value...");
                                        historyData.Value = Console.ReadLine();
                                        HistoryValues.Add(historyData);
                                        UpdateResult = client.Remove(nodeId, HistoryValues);
                                        Console.WriteLine("StatusCode => " + UpdateResult[0].OperationResults[0].ToString());
                                        Console.WriteLine();
                                        break;
                                    case "10":// - DeleteRaw
                                        HistoryUpdateResultCollection results = client.DeleteRaw(nodeId,
                                                                                DateTime.Now.AddHours(-4),
                                                                                DateTime.Now.AddHours(-2),
                                                                                false);
                                        foreach (HistoryUpdateResult value in results)
                                        {
                                            Console.WriteLine("StatusCode => " + value.StatusCode.ToString());
                                        }
                                        Console.WriteLine(string.Empty);
                                        break;
                                    case "11":// - DeleteModified
                                        results = client.DeleteRaw(nodeId,
                                                                               DateTime.Now.AddHours(-4),
                                                                               DateTime.Now.AddHours(-2),
                                                                               true);
                                        foreach (HistoryUpdateResult value in results)
                                        {
                                            Console.WriteLine("StatusCode => " + value.StatusCode.ToString());
                                        }
                                        Console.WriteLine(string.Empty);
                                        break;
                                    case "12":// - DeleteAtTime
                                        results = client.DeleteAtTime(nodeId,
                                                                        DateTime.Now.AddHours(-4),
                                                                        10,
                                                                        5000);
                                        foreach (HistoryUpdateResult value in results)
                                        {
                                            Console.WriteLine("StatusCode => " + value.StatusCode.ToString());
                                        }
                                        Console.WriteLine(string.Empty);
                                        break;

                                    case "13"://Exit
                                        return;
                                }

                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex);
                                Console.WriteLine();
                            }
                        }
                    }
                    while (true);
                }
                else
                {
                    Console.WriteLine("invalid number of Endpoint");
                    Console.WriteLine();
                    Console.WriteLine("press enter for exit");
                    Console.ReadLine();
                }
            }
            else
            {
                Console.WriteLine("no endpoints found");
                Console.WriteLine();
                Console.WriteLine("press enter for exit");
                Console.ReadLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            Console.WriteLine("press enter for exit");
            Console.ReadLine();
        }
        finally
        {
            // Disconnect the current session
            if (client != null && client.GetSessionState().Equals(SessionState.Connected)) client.Disconnect();
        }
    }

    private void Client_MonitorNotification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
    {
        MonitoredItemNotification notification = e.NotificationValue as MonitoredItemNotification;
        Console.WriteLine(monitoredItem.StartNodeId.ToString() + " Value " + notification.Value + " Status: " + notification.Value.StatusCode.ToString());
    }

    void client_CertificateValidation(CertificateValidator sender, CertificateValidationEventArgs e)
    {
        // Handle server certificate validation
        if (ServiceResult.IsGood(e.Error))
            e.Accept = true;
        else if (!e.ContainsUnsuppressibleStatusCodes)
            e.Accept = true;
        else if (e.ContainsUnsuppressibleStatusCodes)
            e.AcceptAll = true; //you can accept all unsuppressible statuscode with this flag
        else
        {
            throw new Exception(string.Format("Failed to validate certificate with error code {0}: {1}", e.Error.Code, e.Error.AdditionalInfo));
        }
    }
    private void Client_ServerConnected(object sender, EventArgs e)
    {
        // Fired when the OPC UA session is successfully established
        Console.WriteLine(DateTime.Now.ToLocalTime() + " Session connected");
    }

    private void Client_ServerConnectionLost(object sender, EventArgs e)
    {
        // Fired when the connection to the OPC UA server is lost
        Console.WriteLine(DateTime.Now.ToLocalTime() + " Session connection lost");
    }

    void Client_KeepAlive(ISession session, KeepAliveEventArgs e)
    {
        // Fired periodically to indicate the server is still alive
    }

    private void Client_SessionClosing(object sender, EventArgs e)
    {
        Console.WriteLine(DateTime.Now.ToLocalTime() + " Session closed");
    }

    private void Subscription_StateChanged(Subscription subscription, SubscriptionStateChangedEventArgs e)
    {
        Console.WriteLine(DateTime.Now.ToLocalTime() + " State of Subscription " + UaClient.SubscriptionToString(subscription) + " changed to => " + e.Status.ToString());
    }

}
