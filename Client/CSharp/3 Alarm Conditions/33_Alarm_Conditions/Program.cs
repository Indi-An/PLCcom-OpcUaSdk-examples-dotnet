// MIT License
// Copyright (c) Indi.An GmbH
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

// ==============================================================================
// PLCcom OPC UA Client SDK - Workshop 33: Alarm Conditions
//
// OPC UA Conditions are the foundation of the alarm system. This
// workshop demonstrates how to acknowledge, confirm and comment
// on alarm conditions - the typical operator workflow.
//
// What you will learn:
//   * How to acknowledge an alarm condition
//   * How to confirm an alarm condition
//   * How to add comments to conditions
//   * The alarm lifecycle (Active -> Acknowledged -> Confirmed)
//
// Target server: opc.tcp://localhost:48410
// ==============================================================================

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

            Console.WriteLine();


            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 33: Alarm Conditions    ║");
            Console.WriteLine("║                                                              ║");
            Console.WriteLine("║  OPC UA Conditions are the foundation of the alarm system.   ║");
            Console.WriteLine("║  This workshop demonstrates how to acknowledge, confirm      ║");
            Console.WriteLine("║  and comment on alarm conditions.                            ║");
            Console.WriteLine("║                                                              ║");
            Console.WriteLine("║  What you will learn:                                        ║");
            Console.WriteLine("║    * Acknowledge an alarm condition                          ║");
            Console.WriteLine("║    * Confirm an alarm condition                              ║");
            Console.WriteLine("║    * Add comments to conditions                              ║");
            Console.WriteLine("║                                                              ║");
            Console.WriteLine("║  Required server: Server Workshop 21 (Alarm Conditions)      ║");
            Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            //TODO
            //Submit your license information from your license e-mail
            string LicenseUserName = "<Enter your UserName here>";
            string LicenseSerial = "<Enter your Serial here>";

            EndpointDescriptionCollection Endpoints = UaClient.GetEndpoints(new Uri("opc.tcp://localhost:48410"), certificateValidator: client_CertificateValidation);

            //sort endpoints by security level
            Endpoints = UaClient.SortEndpointsBySecurityLevel(Endpoints);

            if (Endpoints.Count > 0)
            {
                Console.WriteLine("endpoints found:");
                int counter = 0;
                foreach (EndpointDescription Endpoint in Endpoints)
                {
                    Console.WriteLine(counter++.ToString() + " => " + Endpoint.ToDisplayString());
                }

                Console.WriteLine("please enter index of desired endpoint");
                string NumberOfEndpoint = Console.ReadLine();
                Console.WriteLine("");

                int iNumberOfEndpoint = -1;
                if (int.TryParse(NumberOfEndpoint, out iNumberOfEndpoint) && iNumberOfEndpoint > -1 && iNumberOfEndpoint < Endpoints.Count)
                {

                    //create a a SessionConfiguration with the selected endpoint and application name
                    SessionConfiguration sessionConfiguration = CreateConfig(Endpoints[iNumberOfEndpoint]);
                    PrintConfig(sessionConfiguration);

                    //disable auto connect - we connect explicitly below
                    sessionConfiguration.AutoConnect = false;

                    //output certificate store path
                    Console.WriteLine("Info: Sessionconfiguration created, certificate store path => " + sessionConfiguration.CertificateStorePath);

                    //Create a new opc client instance and pass your license information
                    client = new UaClient(LicenseUserName, LicenseSerial, sessionConfiguration);
                    Console.WriteLine("Info: license state => " + client.GetLicenceMessage());

                    //register events
                    client.ServerConnectionLost += Client_ServerConnectionLost;
                    client.ServerConnected += Client_ServerConnected;
                    client.SessionClosing += Client_SessionClosing;
                    client.KeepAlive += Client_KeepAlive;
                    client.CertificateValidation += client_CertificateValidation;

                    Console.Write("  Connecting ... ");
                    client.Connect();
                    Console.WriteLine("OK");
                    Console.WriteLine();

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
                                break;
                        }

                    }
                    else
                    {
                        filter = client.CreateFilter(ObjectTypeIds.ConditionType);
                    }

                    Console.WriteLine("Start monitoring...");

                    Subscription subscription = new Subscription();
                    subscription.PublishingInterval = 1000;
                    subscription.PublishingEnabled = true;
                    subscription.DisplayName = "mySubsription";

                    //register subscription events
                    subscription.StateChanged += Subscription_StateChanged;

                    //add subscription to client
                    client.AddSubscription(subscription);

                    ReferenceDescription reference = client.GetReferenceDescriptionByNodeId(ObjectIds.Server);
                    if (reference == null)
                    {
                        Console.WriteLine("cannot reading reference description for nodeid");
                        return;//Create a monitoring item and add to the subscription
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

                    //register monitoring event
                    monitoredItem.Notification += Client_MonitorNotification;

                    //add item to subscription
                    subscription.AddItem(monitoredItem);

                    //apply changes
                    subscription.ApplyChanges();

                    client.Refresh_Conditions(subscription);

                    Console.WriteLine("Start monitoring...");

                    string command = string.Empty;

                    do
                    {
                        Console.WriteLine();
                        string commandList = "List of commands: \r\n" +
                                "1 - List all alarms \r\n" +
                                "2 - Refresh active alarms \r\n" +
                                "3 - Enable alarm \r\n" +
                                "4 - Disable alarm \r\n" +
                                "5 - Acknowledge alarm\r\n" +
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
                                        Respond(AlarmNumber, 0);
                                        break;
                                    case "off":
                                        Console.WriteLine("off => Offline..");
                                        Respond(AlarmNumber, 1);
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
            //disconnect actual session
            if (client != null && client.GetSessionState().Equals(SessionState.Connected)) client.Disconnect();
        }

    }

    void client_CertificateValidation(CertificateValidator sender, CertificateValidationEventArgs e)
    {
        //external certificate validation
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
        //event opc ua server is connected
        Console.WriteLine(DateTime.Now.ToLocalTime() + " Session connected");
    }

    private void Client_ServerConnectionLost(object sender, EventArgs e)
    {
        //event connection to opc ua server lost
        Console.WriteLine(DateTime.Now.ToLocalTime() + " Session connection lost");
    }

    void Client_KeepAlive(ISession session, KeepAliveEventArgs e)
    {
        //catch the keepalive event of opc ua server
    }

    private void Client_SessionClosing(object sender, EventArgs e)
    {
        Console.WriteLine(DateTime.Now.ToLocalTime() + " Session closed");
    }

    private void Subscription_StateChanged(Subscription subscription, SubscriptionStateChangedEventArgs e)
    {
        Console.WriteLine(DateTime.Now.ToLocalTime() + " State of Subscription " + subscription.ToDisplayString() + " changed to => " + e.Status.ToString());
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

            string Identifier = "NodeID:" + condition.NodeId.ToString() +
                                " BrancheID:" + (condition.BranchId?.Value?.ToString() ?? "");

            // Retain=false means alarm resolved - remove from cache
            bool retain = condition.Retain?.Value ?? false;
            lock (mAlarmEventCache)
            {
                if (retain)
                    mAlarmEventCache[Identifier] = ae;
                else
                    mAlarmEventCache.Remove(Identifier);
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
                sb.Append(String.Format("Source={0} ", condition.SourceName?.Value));
                sb.Append(String.Format("Condition={0} ", condition.ConditionName?.Value));
                if (condition.BranchId != null) sb.Append(String.Format("Branch={0} ", condition.BranchId.Value));
                sb.Append(String.Format("Severity={0} ", condition.Severity?.Value));
                sb.Append(String.Format("Time={0} ", condition.Time?.Value.ToLocalTime()));
                sb.Append(String.Format("State={0} ", condition.EnabledState?.EffectiveDisplayName?.Value));
                sb.Append(String.Format("Message={0} ", condition.Message?.Value));
                sb.Append(String.Format("Retain={0} ", condition.Retain?.Value));
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
            if (AlarmNumber >= (uint)GetEventCache(false).Count)
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
            if (AlarmNumber >= (uint)GetEventCache(false).Count)
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
            if (AlarmNumber >= (uint)GetEventCache(false).Count)
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
            if (AlarmNumber >= (uint)GetEventCache(false).Count)
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
            if (AlarmNumber >= (uint)GetEventCache(false).Count)
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

    private void Respond(uint AlarmNumber, int responseIndex)
    {
        try
        {
            if (AlarmNumber >= (uint)GetEventCache(false).Count)
            {
                Console.WriteLine("AlarmNumber " + AlarmNumber.ToString() + " is out of range");
                return;
            }

            AlarmEvent alarmEvent = GetEventCache(false).ToArray()[AlarmNumber];
            alarmEvent.Respond(responseIndex);
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

    // =============================================================================
    // Helper: CreateConfig
    // =============================================================================
    // Builds the SessionConfiguration for the selected endpoint.
    //
    // Certificate handling:
    //   Application certificate -- required for Sign / SignAndEncrypt endpoints.
    //   HTTPS certificate       -- required for opc.https:// endpoints (any SecurityMode).
    //
    // UaClientCertificate derives file paths automatically from the PKI base directory:
    //   pki/own/certs/<alias>.der    <- certificate
    //   pki/own/private/<alias>.pem  <- private key
    //
    // Load() returns null if the certificate does not exist yet or cannot be read.
    // Build(true) creates a new self-signed certificate, overwriting any existing file.
    static SessionConfiguration CreateConfig(EndpointDescription endpoint)
    {
        string alias = System.Reflection.Assembly.GetEntryAssembly().GetName().Name;
        SessionConfiguration config = SessionConfiguration.Build(alias, endpoint);
        config.AutoConnect = false;

        // HTTPS certificate -- required for opc.https:// endpoints, independent of SecurityMode.
        UaClientCertificate httpsCert = null;
        if (endpoint.EndpointUrl != null &&
            endpoint.EndpointUrl.StartsWith("opc.https://", StringComparison.OrdinalIgnoreCase))
        {
            string host = new Uri(endpoint.EndpointUrl).Host;
            httpsCert = UaClientCertificate.Load("./pki", host, "secretpassword");
            if (httpsCert == null || !httpsCert.CheckValidity())
                httpsCert = new UaClientCertificate("./pki", "secretpassword", host, 720, "Indi.An GmbH")
                    .Build(overwrite: true);
        }

        // Application certificate -- required for secured endpoints (Sign or SignAndEncrypt).
        // Not needed for SecurityMode.None (unencrypted connections).
        UaClientCertificate appCert = null;
        if (!endpoint.SecurityMode.Equals(MessageSecurityMode.None))
        {
            appCert = UaClientCertificate.Load("./pki", alias, "secretpassword");
            if (appCert == null || !appCert.CheckValidity())
                appCert = new UaClientCertificate("./pki", "secretpassword", alias, 720, "Indi.An GmbH")
                    .Build(overwrite: true);
        }

        // SetInstanceCertificate() sets CertificateStorePath and ApplicationCertificateFullPath.
        if (appCert != null && httpsCert != null)
            config.SetInstanceCertificate(appCert, httpsCert);
        else if (appCert != null)
            config.SetInstanceCertificate(appCert);

        return config;
    }

    // =============================================================================
    // Helper: PrintConfig
    // =============================================================================
    // Prints the active client configuration to the console so you can verify
    // all settings at a glance before connecting.
    static void PrintConfig(SessionConfiguration config)
    {
        Console.WriteLine("-- Active Client Configuration ------------------------------");
        if (config.Endpoint != null)
        {
            Console.WriteLine($"  Endpoint  : {config.Endpoint.EndpointUrl}");
            Console.WriteLine($"  Security  : {config.Endpoint.ToDisplayString()}");
        }
        Console.WriteLine($"  PKI Store : {(config.CertificateStorePath != null ? config.CertificateStorePath : "(not set)")}");
        Console.WriteLine($"  Cert File : {(config.ApplicationCertificateFullPath != null ? config.ApplicationCertificateFullPath : "(none -- SecurityMode.None)")}");
        Console.WriteLine("-------------------------------------------------------------");
        Console.WriteLine();
    }
}