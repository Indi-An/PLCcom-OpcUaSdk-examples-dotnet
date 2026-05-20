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
// PLCcom OPC UA Client SDK - Workshop 31: Incoming Alarms
//
// OPC UA Alarms and Conditions notify clients about abnormal states.
// This workshop subscribes to alarm events and displays them as they
// arrive from the server.
//
// What you will learn:
//   * How to subscribe to alarm events
//   * How to receive and display incoming alarms
//   * How to read alarm properties (severity, message, source)
//
// Target server: opc.tcp://localhost:48410
// ==============================================================================

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

            Console.WriteLine();


            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 31: Incoming Alarms     ║");
            Console.WriteLine("║                                                              ║");
            Console.WriteLine("║  OPC UA Alarms notify clients about abnormal states.         ║");
            Console.WriteLine("║  This workshop subscribes to alarm events and displays       ║");
            Console.WriteLine("║  them as they arrive from the server.                        ║");
            Console.WriteLine("║                                                              ║");
            Console.WriteLine("║  What you will learn:                                        ║");
            Console.WriteLine("║    * Subscribe to alarm events                               ║");
            Console.WriteLine("║    * Receive and display incoming alarms                     ║");
            Console.WriteLine("║    * Read alarm properties (severity, message, source)       ║");
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
                    Console.WriteLine($"  Session state: {client.GetSessionState()}");
                    Console.WriteLine();

                    try
                    {

                        Console.WriteLine("Would you like to enter a filter? y/n");

                        EventFilter filter = null;
                        if (Console.ReadLine().ToLower().Equals("y"))
                        {
                            Console.WriteLine("Please enter filter level... \r\n" +
                                                         "List of commands: \r\n" +
                                                         "1    - All conditions \r\n" +
                                                         "2    - Dialogs \r\n" +
                                                         "3    - Alarms \r\n" +
                                                         "4    - Limit alarms \r\n" +
                                                         "5    - Discrete alarms\r\n");

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

                        //refresh conditions to get current alarm states
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

            NodeId eventTypeId = FindEventType(monitoredItem, notification);

            // ignore unknown events.
            if (NodeId.IsNull(eventTypeId)) return;

            // ignore for refresh start or end.
            if (eventTypeId == ObjectTypeIds.RefreshStartEventType ||
                eventTypeId == ObjectTypeIds.RefreshEndEventType) return;


            // Use GetConditionState to decode the alarm fields into a typed object.
            // This is the recommended way to read alarm properties.
            ConditionState condition = client.GetConditionState(monitoredItem, notification);
            if (condition == null) return;

            // Retain=true:  alarm is active or unacknowledged (needs attention)
            // Retain=false: alarm is resolved (returned to normal and acknowledged)
            bool isActive = condition.Retain?.Value ?? false;
            string status = isActive ? "ALARM ON " : "ALARM OFF";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"[{status}] {DateTime.Now.ToLocalTime():HH:mm:ss}");
            sb.AppendLine($"  Source   : {condition.SourceName?.Value}");
            sb.AppendLine($"  Alarm    : {condition.ConditionName?.Value}");
            sb.AppendLine($"  Message  : {condition.Message?.Value}");
            sb.AppendLine($"  Severity : {condition.Severity?.Value}");
            sb.AppendLine($"  Retain   : {condition.Retain?.Value}  (true=active/unacked, false=resolved)");

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