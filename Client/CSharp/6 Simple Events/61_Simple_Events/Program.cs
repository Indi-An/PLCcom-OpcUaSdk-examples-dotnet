using System;
using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Client;
using PLCcom.Opc.Ua.Client.Sdk;

class Program
{
    // Current publishing state of subscription
    private PublishingState publishingState = PublishingState.UNDEFINED;

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

            EndpointDescriptionCollection Endpoints = UaClient.GetEndpoints(new Uri("opc.tcp://localhost:50520/PLCcom/DataAccessServer"), 60000);

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
                    SessionConfiguration sessionConfiguration = SessionConfiguration.Build(
                        System.Reflection.Assembly.GetEntryAssembly().GetName().Name,
                        Endpoints[iNumberOfEndpoint]);

                    // Enable auto connect functionality
                    sessionConfiguration.AutoConnect = true;

                    // Output certificate store path
                    Console.WriteLine("Info: Sessionconfiguration created, certificate store path => " + sessionConfiguration.CertificateStorePath);

                    // Create a new OPC UA client instance and pass your license information
                    using (UaClient client = new UaClient(LicenseUserName, LicenseSerial, sessionConfiguration))
                    {
                        Console.WriteLine("Info: license state => " + client.GetLicenceMessage());
                        Console.WriteLine("");

                        // Register events
                        client.ServerConnectionLost += Client_ServerConnectionLost;
                        client.ServerConnected += Client_ServerConnected;
                        client.SessionClosing += Client_SessionClosing;
                        client.KeepAlive += Client_KeepAlive;
                        client.CertificateValidation += Client_CertificateValidation;

                        // Create a new subscription
                        using (Subscription subscription = new Subscription())
                        {
                            subscription.PublishingInterval = 1000;
                            subscription.PublishingEnabled = false;
                            subscription.DisplayName = "mySimpleEventClientSubsc";

                            // Register subscription events
                            subscription.StateChanged += Subscription_StateChanged;
                            subscription.PublishStatusChanged += Subscription_PublishStatusChanged;

                            // Add new subscription to client
                            client.AddSubscription(subscription);

                            try
                            {
                                // Create a monitoring item for server events and add to the subscription
                                NodeId nodeId = client.GetNodeIdByPath("Objects.Server");
                                MonitoredItem monitoredItem = new MonitoredItem(subscription.DefaultItem)
                                {
                                    StartNodeId = nodeId,
                                    AttributeId = Attributes.EventNotifier,
                                    SamplingInterval = 0,
                                    QueueSize = 100,
                                    DisplayName = nodeId.ToString(),
                                    DiscardOldest = true,
                                    Filter = new EventFilter
                                    {
                                        // Select which event fields to receive: Message, Severity, Time
                                        SelectClauses = new SimpleAttributeOperandCollection {
                                            new SimpleAttributeOperand() {
                                                TypeDefinitionId = ObjectTypeIds.BaseEventType,
                                                BrowsePath = new QualifiedNameCollection { BrowseNames.Message },
                                                AttributeId = Attributes.Value
                                            },
                                            new SimpleAttributeOperand() {
                                                TypeDefinitionId = ObjectTypeIds.BaseEventType,
                                                BrowsePath = new QualifiedNameCollection { BrowseNames.Severity },
                                                AttributeId = Attributes.Value
                                            },
                                            new SimpleAttributeOperand() {
                                                TypeDefinitionId = ObjectTypeIds.BaseEventType,
                                                BrowsePath = new QualifiedNameCollection { BrowseNames.Time },
                                                AttributeId = Attributes.Value
                                            }
                                        }
                                    }
                                };

                                // Register monitoring event callback
                                monitoredItem.Notification += Client_MonitorNotification;

                                // Add item to subscription
                                subscription.AddItem(monitoredItem);

                                // Apply changes to the subscription
                                subscription.ApplyChanges();

                                // Enable publishing mode and apply modified settings
                                subscription.SetPublishingMode(true);
                                subscription.Modify();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex);
                            }

                            Console.WriteLine();
                            Console.WriteLine("press enter for exit");
                            Console.ReadLine();
                        }
                    }
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
            Console.WriteLine("press enter for exit");
            Console.ReadLine();
        }
    }

    /// <summary>
    /// Callback for event notifications from the server.
    /// Processes EventFieldList (events) and MonitoredItemNotification (data changes).
    /// </summary>
    private void Client_MonitorNotification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
    {
        // Handle event notifications
        if (e.NotificationValue is EventFieldList ev)
        {
            // Field sequence corresponds to SelectClauses: Message, Severity, Time
            var message = ev.EventFields[0].Value as LocalizedText;
            var severity = ev.EventFields[1].Value is ushort u ? u : (ushort)0;
            var time = ev.EventFields[2].Value is DateTime dt ? dt : DateTime.MinValue;

            Console.WriteLine($"[{time:O}] Sev={severity} | {message?.Text}");
            return;
        }

        // Handle data change notifications
        if (e.NotificationValue is MonitoredItemNotification dn)
        {
            Console.WriteLine($"{monitoredItem.StartNodeId} Value: {dn.Value} Status: {dn.Value.StatusCode}");
            return;
        }

        Console.WriteLine($"Unexpected notification type: {e.NotificationValue?.GetType().Name ?? "null"}");
    }

    private void Subscription_StateChanged(Subscription subscription, SubscriptionStateChangedEventArgs e)
    {
        Console.WriteLine(DateTime.Now.ToLocalTime() + " State of Subscription " + UaClient.SubscriptionToString(subscription) + " changed to => " + e.Status.ToString());
    }

    private void Subscription_PublishStatusChanged(object sender, EventArgs e)
    {
        // Check your publish state of your subscription.
        // If the publish state permanently stopped, then you have to recreate your subscription
        // with old subscription as template.
        // In this case, please have a look to the PublishingInterval setting,
        // possibly the value must be increased.

        Subscription subscription = sender as Subscription;
        if (subscription != null)
        {
            PublishingState currentpublishingState = subscription.PublishingStopped ? PublishingState.STOPPED : PublishingState.RUNNING;
            if (currentpublishingState != publishingState || currentpublishingState == PublishingState.STOPPED)
                Console.WriteLine(DateTime.Now.ToLocalTime() + " Publishing state of Subscription " + UaClient.SubscriptionToString((Subscription)sender) + " => " + currentpublishingState.ToString());

            publishingState = currentpublishingState;
        }
    }

    void Client_CertificateValidation(CertificateValidator sender, CertificateValidationEventArgs e)
    {
        // External certificate validation
        if (ServiceResult.IsGood(e.Error))
            e.Accept = true;
        else if (!e.ContainsUnsuppressibleStatusCodes)
            e.Accept = true;
        else if (e.ContainsUnsuppressibleStatusCodes)
            e.AcceptAll = true;
        else
        {
            throw new Exception(string.Format("Failed to validate certificate with error code {0}: {1}", e.Error.Code, e.Error.AdditionalInfo));
        }
    }

    private void Client_ServerConnected(object sender, EventArgs e)
    {
        Console.WriteLine(DateTime.Now.ToLocalTime() + " Session connected");
    }

    private void Client_ServerConnectionLost(object sender, EventArgs e)
    {
        Console.WriteLine(DateTime.Now.ToLocalTime() + " Session connection lost");
    }

    void Client_KeepAlive(ISession session, KeepAliveEventArgs e)
    {
        // Catch the keepalive event of OPC UA server
    }

    private void Client_SessionClosing(object sender, EventArgs e)
    {
        Console.WriteLine(DateTime.Now.ToLocalTime() + " Session closed");
    }

    private enum PublishingState
    {
        UNDEFINED,
        RUNNING,
        STOPPED
    }
}
