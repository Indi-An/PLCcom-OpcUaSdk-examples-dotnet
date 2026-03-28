using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Client;
using PLCcom.Opc.Ua.Client.Sdk;

class Program
{
    //define the ua client object
    private UaClient client = null;

    // a dictionary used to caching event filter types.
    private Dictionary<EventFilter, Dictionary<int, string>> eventFilterMappings = new Dictionary<EventFilter, Dictionary<int, string>>();

    //the condition filter object
    private EventFilter filter;

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

            EndpointDescriptionCollection Endpoints = UaClient.GetEndpoints(new Uri("opc.tcp://localhost:50540/PLCcom/HistoricalEventsServer"), 60000);

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

                    Console.WriteLine();

                    //set target NodeId
                    NodeId nodeId = new NodeId("ns=2;s=Area51"); //'Objects.Server.Plaforms.Area51'

                    if (nodeId != null)
                    {
                        try
                        {
                            Console.WriteLine("Start monitoring.....)");

                            filter = client.CreateFilter(ObjectTypeIds.ConditionType);

                            filter.WhereClause.Push(FilterOperator.OfType, ObjectTypeIds.ConditionType);

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
                                return;// Create a monitored item for the specified node
                            }

                            MonitoredItem monitoredItem = new MonitoredItem((ITelemetryContext)null);
                            monitoredItem.NodeClass = reference.NodeClass;
                            monitoredItem.AttributeId = Attributes.EventNotifier;
                            monitoredItem.MonitoringMode = MonitoringMode.Reporting;
                            monitoredItem.StartNodeId = nodeId;
                            monitoredItem.Filter = filter;
                            monitoredItem.DisplayName = "event monitoring";
                            monitoredItem.QueueSize = UInt32.MaxValue;
                            monitoredItem.DiscardOldest = true;

                            //checking and creating event filter cache
                            if (!eventFilterMappings.ContainsKey(filter))
                            {
                                Dictionary<int, string> d = new Dictionary<int, string>();
                                for (int i = 0; i < ((EventFilter)monitoredItem.Filter).SelectClauses.Count; i++)
                                {
                                    string clause = ((EventFilter)monitoredItem.Filter).SelectClauses[i].ToString();
                                    d.Add(i, clause);
                                }
                                eventFilterMappings.Add(filter, d);
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

                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                        }
                    }
                    Console.WriteLine("press enter for exit");
                    Console.ReadLine();
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
            sb.Append(DateTime.Now.ToLocalTime() + " new event notification received:");
            sb.Append(Environment.NewLine);
            for (int i = 0; i < notification.EventFields.Count; i++)
            {
                if (notification.EventFields[i].Value != null)
                {
                    sb.Append(String.Format(" " + GetEventFilterMappings((EventFilter)monitoredItem.Filter)[i] + " {0}", notification.EventFields[i].Value.ToString()));
                    sb.Append(Environment.NewLine);
                }
            }

            int EventIdIndex = -1;
            for (int i = 0; i < notification.EventFields.Count; i++)
            {
                if (notification.EventFields[i].Value != null)
                {
                    //Important => method returns all timestamps in universal time format
                    string eventName = GetEventFilterMappings(filter)[i];

                    //store the index of eventid for eventual deleting the events
                    if (EventIdIndex == -1 && eventName.Replace("/", "").ToLower().Equals("eventid")) EventIdIndex = i;

                    object value = notification.EventFields[i].Value;
                    //if value equals enetId, then convert value to hexstring
                    if (EventIdIndex > -1 && EventIdIndex == i) value = ByteArrayToString((byte[])notification.EventFields[EventIdIndex].Value);

                    if (notification.EventFields[i].Value != null)
                    {
                        sb.Append(String.Format(" " + eventName + " {0}", value.ToString()));
                        sb.Append(Environment.NewLine);
                    }
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
    /// returns cached eventfilter
    /// </summary>
    /// <param name="filter">a EventFilter object</param>
    /// <returns>a Dictionary with int as key and string als value, null if monitoredItem not member of a active subscription or not with a filter activated</returns>
    public Dictionary<int, string> GetEventFilterMappings(EventFilter filter)
    {
        if (eventFilterMappings.ContainsKey(filter))
        {
            return eventFilterMappings[filter];
        }
        else
        {
            Dictionary<int, string> d = new Dictionary<int, string>();
            for (int i = 0; i < ((EventFilter)filter).SelectClauses.Count; i++)
            {
                string clause = ((EventFilter)filter).SelectClauses[i].ToString();
                d.Add(i, clause);
            }
            eventFilterMappings.Add(filter, d);
            return d;
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

    private void Subscription_StateChanged(Subscription subscription, SubscriptionStateChangedEventArgs e)
    {
        Console.WriteLine(DateTime.Now.ToLocalTime() + " State of Subscription " + UaClient.SubscriptionToString(subscription) + " changed to => " + e.Status.ToString());
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

    public static byte[] StringToByteArray(String hex)
    {
        int NumberChars = hex.Length;
        byte[] bytes = new byte[NumberChars / 2];
        for (int i = 0; i < NumberChars; i += 2)
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        return bytes;
    }

    public static string ByteArrayToString(byte[] ba)
    {
        StringBuilder hex = new StringBuilder(ba.Length * 2);
        foreach (byte b in ba)
            hex.AppendFormat("{0:x2}", b);
        return hex.ToString();
    }
}
