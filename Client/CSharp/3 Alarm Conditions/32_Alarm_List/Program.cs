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
// PLCcom OPC UA Client SDK - Workshop 32: Alarm List
//
// While Workshop 31 shows incoming alarms one by one, this workshop
// maintains a live list of all active alarms. The list updates
// automatically as alarms appear, change state or disappear.
//
// What you will learn:
//   * How to maintain a live alarm list
//   * How to track alarm state changes
//   * How to identify alarms by ConditionId
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
using System.Threading;
using System.Threading.Tasks;


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
             Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 32: Alarm List          ║");
             Console.WriteLine("║                                                              ║");
             Console.WriteLine("║  Maintains a live list of all active alarms that updates     ║");
             Console.WriteLine("║  automatically as alarms appear, change or disappear.        ║");
             Console.WriteLine("║                                                              ║");
             Console.WriteLine("║  What you will learn:                                        ║");
             Console.WriteLine("║    * Maintain a live alarm list                              ║");
             Console.WriteLine("║    * Track alarm state changes                               ║");
             Console.WriteLine("║    * Identify alarms by ConditionId                          ║");
             Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
             Console.WriteLine();

            //TODO
            //Submit your license information from your license e-mail
            string LicenseUserName = "<Enter your UserName here>";
            string LicenseSerial = "<Enter your Serial here>";

            EndpointDescriptionCollection Endpoints = UaClient.GetEndpoints(new Uri("opc.tcp://localhost:48410"), 60000);

            //sort endpoints by security level
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
                    //create a a SessionConfiguration with the selected endpoint and application name
                    SessionConfiguration sessionConfiguration = SessionConfiguration.Build(System.Reflection.Assembly.GetEntryAssembly().GetName().Name,
                                                                                          Endpoints[iNumberOfEndpoint]);

                    //enable auto connect functionality
                    sessionConfiguration.AutoConnect = true;

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

                    Console.WriteLine(client.GetSessionState().ToString());
                    Console.WriteLine();

                    try
                    {
                        EventFilter filter = client.CreateFilter(ObjectTypeIds.ConditionType);

                        filter.WhereClause.Push(FilterOperator.OfType, ObjectTypeIds.ConditionType);

                        Subscription subscription = new Subscription();
                        subscription.PublishingInterval = 1000;
                        subscription.PublishingEnabled = false;
                        subscription.DisplayName = "mySubsription";

                        //register subscription events
                        subscription.StateChanged += Subscription_StateChanged;
                        subscription.PublishingEnabled = false;

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

                        //enable publishing mode of subscription and set PublishingInterval
                        subscription.SetPublishingMode(true);
                        subscription.Modify();

                        client.Refresh_Conditions(subscription);

                        Console.WriteLine("Start monitoring.....)");
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
            Console.WriteLine("press enter for exit");
            Console.ReadLine();
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

            //Create output string
            StringBuilder sb = new StringBuilder();
            sb.Append(DateTime.Now.ToLocalTime() + " new Alarm notification:");
            sb.Append(Environment.NewLine);
            ConditionState actualAlarmcondition = client.GetConditionState(monitoredItem, notification);
            sb.Append(String.Format("Source={0} ", actualAlarmcondition.SourceName.Value));
            sb.Append(String.Format("Condition={0} ", actualAlarmcondition.ConditionName.Value));
            sb.Append(String.Format("Severity={0} ", actualAlarmcondition.Severity.Value));
            sb.Append(String.Format("Time={0} ", actualAlarmcondition.Time.Value.ToLocalTime()));
            sb.Append(String.Format("State={0} ", actualAlarmcondition.EnabledState.EffectiveDisplayName.Value));
            sb.Append(String.Format("Message={0} ", actualAlarmcondition.Message.Value));
            sb.Append(String.Format("Comment={0} ", actualAlarmcondition.Comment.Value));

            sb.Append(Environment.NewLine);
            sb.Append("Current alarm list:");
            sb.Append(Environment.NewLine);

            ConditionState condition = client.GetConditionState(monitoredItem, notification);

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

            foreach (AlarmEvent alarmEvent in GetEventCache(true))
            {
                ConditionState alarmCondition = alarmEvent.GetConditionState();
                sb.Append(String.Format("Source={0} ", alarmCondition.SourceName.Value));
                sb.Append(String.Format("Condition={0} ", alarmCondition.ConditionName.Value));
                sb.Append(String.Format("Severity={0} ", alarmCondition.Severity.Value));
                sb.Append(String.Format("Time={0} ", alarmCondition.Time.Value.ToLocalTime()));
                sb.Append(String.Format("State={0} ", alarmCondition.EnabledState.EffectiveDisplayName.Value));
                sb.Append(String.Format("Message={0} ", alarmCondition.Message.Value));
                sb.Append(String.Format("Comment={0} ", alarmCondition.Comment.Value));
                sb.Append(Environment.NewLine);
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
