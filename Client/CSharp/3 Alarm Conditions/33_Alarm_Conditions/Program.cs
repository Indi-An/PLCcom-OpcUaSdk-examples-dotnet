using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Client;
using PLCcom.Opc.Ua.Client.Sdk;

class Program
{
    private UaClient client = null;

    // a dictionary used to caching event filter types.
    private Dictionary<EventFilter, Dictionary<int, string>> mEventFilterMappings = new Dictionary<EventFilter, Dictionary<int, string>>();

    //a local AlarmCache
    private Dictionary<string, AlarmEvent> mAlarmEventCache = new Dictionary<string, AlarmEvent>();

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
                                filter = client.CreateFilter(ObjectTypeIds.ExclusiveLimitAlarmType,
                                                             ObjectTypeIds.NonExclusiveLimitAlarmType);
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
                        return;// Create a monitored item for the specified node
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

                    Console.WriteLine("Start monitoring.....)");

                    string command = string.Empty;

                    do
                    {
                        Console.WriteLine();
                        string commandList = "List of commands: \r\n" +
                                "1    - List all alarms \r\n" +
                                "2    - Refresh active alarms \r\n" +
                                "3    - Enable alarm \r\n" +
                                "4    - Disable alarm \r\n" +
                                "5  - Acknowledge alarm\r\n" +
                                "6 - Add comment\r\n" +
                                "7 - Confirm alarm\r\n" +
                                "8 - Shelve alarm \r\n" +
                                "9 - Respond \r\n" +
                                "0 - Close the application \r\n";

                        Console.WriteLine(commandList);
                        Console.WriteLine("Enter Commands:\n");

                        command = Console.ReadLine();
                        uint AlarmNumber = 0;

                        switch (command.ToLower())
                        {
                            case "1":
                                ListAlarms();
                                break;
                            case "2":
                                client.Refresh_Conditions(subscription);
                                ListAlarms();
                                break;
                            case "3":
                                Console.WriteLine("Enter alarm number: \r\n");
                                if (!uint.TryParse(Console.ReadLine(), out AlarmNumber))
                                {
                                    Console.WriteLine("wrong alarm number: \r\n");
                                    continue;
                                }
                                EnableDisableCondition(AlarmNumber, true);
                                break;
                            case "4":
                                Console.WriteLine("Enter alarm number: \r\n");
                                if (!uint.TryParse(Console.ReadLine(), out AlarmNumber))
                                {
                                    Console.WriteLine("wrong alarm number: \r\n");
                                    continue;
                                }
                                EnableDisableCondition(AlarmNumber, false);
                                break;
                            case "5":
                                Console.WriteLine("Enter alarm number: \r\n");
                                if (!uint.TryParse(Console.ReadLine(), out AlarmNumber))
                                {
                                    Console.WriteLine("wrong alarm number: \r\n");
                                    continue;
                                }
                                Console.WriteLine("Enter comment: \r\n");
                                string Comment = Console.ReadLine();
                                Acknowledge(AlarmNumber, Comment);
                                break;
                            case "6":
                                Console.WriteLine("Enter alarm number: \r\n");
                                if (!uint.TryParse(Console.ReadLine(), out AlarmNumber))
                                {
                                    Console.WriteLine("wrong alarm number: \r\n");
                                    continue;
                                }
                                Console.WriteLine("Enter comment: \r\n");
                                Comment = Console.ReadLine();
                                AddComment(AlarmNumber, Comment);
                                break;
                            case "7":
                                Console.WriteLine("Enter alarm number: \r\n");
                                if (!uint.TryParse(Console.ReadLine(), out AlarmNumber))
                                {
                                    Console.WriteLine("wrong alarm number: \r\n");
                                    continue;
                                }
                                Console.WriteLine("Enter comment: \r\n");
                                Comment = Console.ReadLine();
                                Confirm(AlarmNumber, Comment);
                                break;
                            case "8":
                                Console.WriteLine("Enter dialog number: \r\n");
                                if (!uint.TryParse(Console.ReadLine(), out AlarmNumber))
                                {
                                    Console.WriteLine("wrong dialog number: \r\n");
                                    continue;
                                }
                                Console.WriteLine("Enter subcommand: \r\n" +
                                                    "on    - Online \r\n" +
                                                    "off    - Offline \r\n" +
                                                    "exit - Abort shelving \r\n");
                                string SubCommand = Console.ReadLine();

                                switch (SubCommand.ToLower())
                                {
                                    case "on":
                                        Console.WriteLine("on => Online..");
                                        Respond(AlarmNumber, true);
                                        break;
                                    case "off":
                                        Console.WriteLine("off => Offline..");
                                        Respond(AlarmNumber, false);
                                        break;
                                    case "exit":
                                        Console.WriteLine("exit => Abort shelving..");
                                        break;
                                    default:
                                        Console.WriteLine("Unknown command => Abort shelving..");
                                        break;
                                }
                                break;
                            case "9":
                                Console.WriteLine("Enter alarm number: \r\n");
                                if (!uint.TryParse(Console.ReadLine(), out AlarmNumber))
                                {
                                    Console.WriteLine("wrong alarm number: \r\n");
                                    continue;
                                }
                                Console.WriteLine("Enter subcommand: \r\n" +
                                                    "u    - Unshelve \r\n" +
                                                    "o    - One shot shelve \r\n" +
                                                    "t    - Timedshelve \r\n" +
                                                    "exit - Abort shelving \r\n");

                                switch (Console.ReadLine())
                                {
                                    case "u":
                                        Console.WriteLine("u => Unshelve..");
                                        Shelve(AlarmNumber, AlarmEvent.ShelvingMethod.UnShelve, 0);
                                        break;
                                    case "o":
                                        Console.WriteLine("o => One shot shelve..");
                                        Shelve(AlarmNumber, AlarmEvent.ShelvingMethod.OneShot, 0);
                                        break;
                                    case "t":
                                        Console.WriteLine("t => Timedshelve..");
                                        Console.WriteLine("please enter desired shelving time...");
                                        double ShelvingTime = 0;
                                        if (double.TryParse(Console.ReadLine(), out ShelvingTime))
                                        {
                                            Shelve(AlarmNumber, AlarmEvent.ShelvingMethod.TimedShelve, ShelvingTime);
                                        }
                                        else
                                        {
                                            Console.WriteLine("invalid number => Abort shelving.. \r\n");
                                            continue;
                                        }
                                        break;
                                    case "exit":
                                        Console.WriteLine("exit => Abort shelving..");
                                        break;
                                    default:
                                        Console.WriteLine("Unknown command => Abort shelving..");
                                        break;
                                }
                                break;
                        }

                    } while (!command.ToUpper().StartsWith("0"));
                }
                else
                {
                    Console.WriteLine("invalid number of Endpoint");

                }
            }
            else
            {
                Console.WriteLine("no endpoints found");
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

            //catch the MonitorNotification event
            ConditionState condition = client.GetConditionState(monitoredItem, notification);
            if (condition == null)
                return;

            AlarmEvent ae = client.CreateAlarmEvent(condition.NodeId, condition);
            //AlarmEventListe aufbauen und aktualisieren
            for (int i = 0; i < notification.EventFields.Count; i++)
            {
                string filtername = GetEventFilterMappings((EventFilter)monitoredItem.Filter)[i].Replace("/", "");
                AlarmEventItem aei = new AlarmEventItem(filtername, notification.EventFields[i].Value);
                ae.AlarmEventItems.Add(filtername, aei);
            }

            string Identifier = "NodeID:" + condition.NodeId.ToString() + " BrancheID:" + (condition.BranchId != null ? condition.BranchId.Value.ToString() : "");

            //Update Alarm cache
            if (mAlarmEventCache.ContainsKey(Identifier))
            {
                mAlarmEventCache[Identifier] = ae;
            }
            else
            {
                mAlarmEventCache.Add(Identifier, ae);
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception.Message);
        }
    }

    private void ListAlarms()
    {
        try
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Environment.NewLine);
            sb.Append("Current alarm list:");
            sb.Append(Environment.NewLine);
            int counter = 0;
            foreach (AlarmEvent alarmEvent in GetEventCache(true))
            {
                sb.Append(String.Format("{0} ", counter++.ToString()));
                ConditionState condition = alarmEvent.GetConditionState();
                sb.Append(String.Format("Source={0} ", condition.SourceName.Value));
                sb.Append(String.Format("Condition={0} ", condition.ConditionName.Value));
                if (condition.BranchId != null) sb.Append(String.Format("Branch={0} ", condition.BranchId.Value));
                sb.Append(String.Format("Severity={0} ", condition.Severity.Value));
                sb.Append(String.Format("Time={0} ", condition.Time.Value.ToLocalTime()));
                sb.Append(String.Format("State={0} ", condition.EnabledState.EffectiveDisplayName.Value));
                sb.Append(String.Format("Message={0} ", condition.Message.Value));
                sb.Append(String.Format("Comment={0} ", condition.Comment.Value));
                sb.Append(Environment.NewLine);
            }

            sb.Append(Environment.NewLine);

            Console.WriteLine(sb.ToString());
        }
        catch (Exception ex)
        {

            Console.WriteLine(ex);
        }
    }

    private void Acknowledge(uint AlarmNumber, string comment)
    {
        try
        {
            if (AlarmNumber > mEventFilterMappings.Count)
            {
                Console.WriteLine("AlarmNumber " + AlarmNumber.ToString() + " is out of range");
            }

            AlarmEvent alarmEvent = GetEventCache(false).ToArray()[AlarmNumber];
            alarmEvent.Acknowledge(comment);
            Console.WriteLine("method successfully");
        }
        catch (Exception ex)
        {

            Console.WriteLine(ex);
        }
    }

    private void EnableDisableCondition(uint AlarmNumber, bool enable)
    {
        try
        {
            if (AlarmNumber > GetEventCache(false).Count)
            {
                Console.WriteLine("AlarmNumber " + AlarmNumber.ToString() + " is out of range");
            }

            AlarmEvent alarmEvent = GetEventCache(false).ToArray()[AlarmNumber];
            alarmEvent.EnableDisableCondition(enable);
            Console.WriteLine("method successfully");
        }
        catch (Exception ex)
        {

            Console.WriteLine(ex);
        }
    }

    private void AddComment(uint AlarmNumber, string comment)
    {
        try
        {
            if (AlarmNumber > GetEventCache(false).Count)
            {
                Console.WriteLine("AlarmNumber " + AlarmNumber.ToString() + " is out of range");
            }

            AlarmEvent alarmEvent = GetEventCache(false).ToArray()[AlarmNumber];
            alarmEvent.AddComment(comment);
            Console.WriteLine("method successfully");
        }
        catch (Exception ex)
        {

            Console.WriteLine(ex);
        }
    }

    private void Confirm(uint AlarmNumber, string comment)
    {
        try
        {
            if (AlarmNumber > GetEventCache(false).Count)
            {
                Console.WriteLine("AlarmNumber " + AlarmNumber.ToString() + " is out of range");
            }

            AlarmEvent alarmEvent = GetEventCache(false).ToArray()[AlarmNumber];
            alarmEvent.Confirm(comment);
            Console.WriteLine("method successfully");
        }
        catch (Exception ex)
        {

            Console.WriteLine(ex);
        }
    }

    private void Shelve(uint AlarmNumber, AlarmEvent.ShelvingMethod shelvingMethod, double shelvingTime)
    {
        try
        {
            if (AlarmNumber > GetEventCache(false).Count)
            {
                Console.WriteLine("AlarmNumber " + AlarmNumber.ToString() + " is out of range");
            }

            AlarmEvent alarmEvent = GetEventCache(false).ToArray()[AlarmNumber];
            alarmEvent.Shelve(shelvingMethod, shelvingTime);
            Console.WriteLine("method successfully");
        }
        catch (Exception ex)
        {

            Console.WriteLine(ex);
        }
    }

    private void Respond(uint AlarmNumber, bool OnlineState)
    {
        try
        {
            if (AlarmNumber > GetEventCache(false).Count)
            {
                Console.WriteLine("AlarmNumber " + AlarmNumber.ToString() + " is out of range");
            }

            AlarmEvent alarmEvent = GetEventCache(false).ToArray()[AlarmNumber];
            alarmEvent.Respond(OnlineState);
            Console.WriteLine("method successfully");
        }
        catch (Exception ex)
        {

            Console.WriteLine(ex);
        }
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

    /// <summary>
    /// returns internal event cache
    /// </summary>
    ///<param name="waitForEndingConditionRefresh">If sets parameter to true and condition refresh is in progress, the function will waiting on end of a eventualy running condition refresh. 
    ///                                            If a maximum wait time of 5000 milliseconds exceeded, a InvalidOperationException will be raise.
    ///                                            If sets parameter to false and condition refresh is in progress, the function returns the actual partitial result</param>
    /// <returns>List of alarm event objects</returns>
    /// <exception cref="T:System.InvalidOperationException">Deathlook detected => Operation not possible, condition refresh in progress! Please try again or set parameter waitForEndingConditionRefresh to false...</exception>
    public List<AlarmEvent> GetEventCache(bool waitForEndingConditionRefresh)
    {
        if (Monitor.TryEnter(mAlarmEventCache, 5000))
        {
            try
            {
                return mAlarmEventCache.Values.ToList();
            }
            finally
            {
                Monitor.Exit(mAlarmEventCache);
            }
        }
        else
        {
            throw new InvalidOperationException("Operation not possible, condition refresh in progress! Please try again...");
        }
    }
}
