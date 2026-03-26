using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Client;
using PLCcom.Opc.Ua.Client.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

class Program
{
    private UaClient client = null;

    // a dictionary used to caching event filter types.
    private Dictionary<EventFilter, Dictionary<int, string>> mEventFilterMappings = new Dictionary<EventFilter, Dictionary<int, string>>();

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

            EndpointDescriptionCollection Endpoints = UaClient.GetEndpoints(new Uri("opc.tcp://localhost:50510/PLCcom/AlarmConditionServer"), 60000);

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

                    try
                    {

                        Console.WriteLine("Would you like to enter a filter? y/n");

                        EventFilter filter = null;
                        if (Console.ReadLine().ToLower().Equals("y"))
                        {
                            Console.WriteLine("Please enter filter level... \r\n" +
                                                         "List of commands: \r\n" +
                                                         "1    - All \r\n" +
                                                         "2    - Dialogs \r\n" +
                                                         "3    - Alarms \r\n" +
                                                         "4    - Limit alarms \r\n" +
                                                         "5    - Discrete alarms\r\n");

                            //create eventfilter for monitoring
                            switch (Console.ReadLine().ToLower())
                            {
                                case "1":
                                    filter = client.CreateFilter(ObjectTypeIds.ConditionType);
                                    break;
                                case "2":
                                    filter = client.CreateFilter(ObjectTypeIds.DialogConditionType);
                                    break;
                                case "3":
                                    filter = client.CreateFilter(ObjectTypeIds.AlarmConditionType);
                                    break;
                                case "4":
                                    filter = client.CreateFilter(ObjectTypeIds.ExclusiveLimitAlarmType, ObjectTypeIds.NonExclusiveLimitAlarmType);
                                    break;
                                case "5":
                                    filter = client.CreateFilter(ObjectTypeIds.DiscreteAlarmType);
                                    break;
                                default:
                                    Console.WriteLine("Unknown command...");
                                    //create standard eventfilter for monitoring
                                    filter = client.CreateFilter(ObjectTypeIds.ConditionType);
                                    filter.WhereClause.Push(FilterOperator.OfType, ObjectTypeIds.ConditionType);
                                    break;
                            }

                        }
                        else
                        {
                            //create standard eventfilter for monitoring
                            filter = client.CreateFilter(ObjectTypeIds.ConditionType);
                            filter.WhereClause.Push(FilterOperator.OfType, ObjectTypeIds.ConditionType);
                        }

                        Console.WriteLine("Start monitoring.....)");

                        Subscription subscription = new Subscription();
                        subscription.PublishingInterval = 1000;
                        subscription.PublishingEnabled = false;
                        subscription.DisplayName = "mySubsription";

                        // Register subscription state change events
                        subscription.StateChanged += Subscription_StateChanged;
                        subscription.PublishingEnabled = false;

                        // Add the subscription to the client instance
                        client.AddSubscription(subscription);

                        ReferenceDescription reference = client.GetReferenceDescriptionByNodeId(ObjectIds.Server);
                        if (reference == null)
                        {
                            Console.WriteLine("cannot reading reference description for nodeid");
                            return ;// Create a monitored item for the specified node
                        }

                        MonitoredItem monitoredItem = new MonitoredItem((ITelemetryContext)null);
                        monitoredItem.NodeClass = reference.NodeClass;
                        monitoredItem.AttributeId = Attributes.EventNotifier;
                        monitoredItem.MonitoringMode = MonitoringMode.Reporting;
                        monitoredItem.StartNodeId = ObjectIds.Server;
                        monitoredItem.Filter = filter;
                        monitoredItem.DisplayName = "event monitoring";
                        monitoredItem.QueueSize = UInt32.MaxValue;
                        monitoredItem.DiscardOldest = true;

                        //checking and creating event filter cache
                        if (!mEventFilterMappings.ContainsKey(filter))
                        {
                            Dictionary<int, string> d = new Dictionary<int, string>();
                            for (int i = 0; i < ((EventFilter)monitoredItem.Filter).SelectClauses.Count; i++)
                            {
                                string clause = ((EventFilter)monitoredItem.Filter).SelectClauses[i].ToString();
                                d.Add(i, clause);
                            }
                            mEventFilterMappings.Add(filter, d);
                        }

                        // Register the notification callback for value changes
                        monitoredItem.Notification += Client_MonitorNotification;

                        // Add the monitored item to the subscription
                        subscription.AddItem(monitoredItem);

                        // Apply all pending changes to the subscription (creates monitored items on the server)
                        subscription.ApplyChanges();

                        // Enable publishing mode and apply the configured PublishingInterval
                        subscription.SetPublishingMode(true);
                        subscription.Modify();

                        client.Refresh_Conditions(subscription);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        Console.WriteLine();
                    }

                }
                else
                {
                    Console.WriteLine("invalid number of Endpoint");
                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine("no endpoints found");
                Console.WriteLine();
            }

            Console.WriteLine();

        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            Console.WriteLine();
        }
        finally
        {
            Console.WriteLine("press enter for exit");
            Console.ReadLine();
            // Disconnect the current session
            if (client != null && client.GetSessionState().Equals(SessionState.Connected)) client.Disconnect();
        }
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

    private void Client_MonitorNotification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
    {
        try
        {
            EventFieldList notification = e.NotificationValue as EventFieldList;
            if (notification == null) return;

            NodeId eventTypeId = FindEventType(monitoredItem, notification);

            // ignore unknown events.
            if (NodeId.IsNull(eventTypeId)) return;

            // ignore for refresh start or end.
            if (eventTypeId == ObjectTypeIds.RefreshStartEventType ||
                eventTypeId == ObjectTypeIds.RefreshEndEventType) return;

            //show actual event alarm data in debug window
            StringBuilder sb = new StringBuilder();
            sb.Append("new notification received:");
            sb.Append(Environment.NewLine);
            for (int i = 0; i < notification.EventFields.Count; i++)
            {
                if (notification.EventFields[i].Value != null)
                {
                    sb.Append(String.Format(" " + GetEventFilterMappings((EventFilter)monitoredItem.Filter)[i] + " {0}", notification.EventFields[i].Value.ToString()));
                    sb.Append(Environment.NewLine);
                }
            }
            sb.Append(Environment.NewLine);

            Console.WriteLine(sb.ToString());

        }
        catch (Exception exception)
        {
            Console.WriteLine(exception.Message);
        }
    }

    /// <summary>
    /// Finds the type of the event for the notification.
    /// </summary>
    /// <param name="monitoredItem">The monitored item.</param>
    /// <param name="notification">The notification.</param>
    /// <returns>The NodeId of the EventType.</returns>
    public static NodeId FindEventType(MonitoredItem monitoredItem, EventFieldList notification)
    {

        EventFilter filter = monitoredItem.Status.Filter as EventFilter;

        if (filter != null)
        {
            for (int ii = 0; ii < filter.SelectClauses.Count; ii++)
            {
                SimpleAttributeOperand clause = filter.SelectClauses[ii];

                if (clause.BrowsePath.Count == 1 && clause.BrowsePath[0] == BrowseNames.EventType)
                {
                    return notification.EventFields[ii].Value as NodeId;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// returns cached eventfilter
    /// </summary>
    /// <param name="filter">a EventFilter object</param>
    /// <returns>a Dictionary with int as key and string als value, null if monitoredItem not member of a active subscription or not with a filter activated</returns>
    public Dictionary<int, string> GetEventFilterMappings(EventFilter filter)
    {
        if (mEventFilterMappings.ContainsKey(filter))
        {
            return mEventFilterMappings[filter];
        }
        else
        {
            Dictionary<int, string> d = new Dictionary<int, string>();
            for (int i = 0; i < ((EventFilter)filter).SelectClauses.Count; i++)
            {
                string clause = ((EventFilter)filter).SelectClauses[i].ToString();
                d.Add(i, clause);
            }
            mEventFilterMappings.Add(filter, d);
            return d;
        }
    }

}
