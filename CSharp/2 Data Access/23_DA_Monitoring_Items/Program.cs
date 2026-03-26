using System;
using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Client;
using PLCcom.Opc.Ua.Client.Sdk;

class Program
{

    //actual publishing state of subscription
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
                    SessionConfiguration sessionConfiguration = SessionConfiguration.Build(System.Reflection.Assembly.GetEntryAssembly().GetName().Name,
                                                                                         Endpoints[iNumberOfEndpoint]);

                    // Enable AutoConnect - the client will connect and reconnect automatically
                    sessionConfiguration.AutoConnect = true;

                    // Display the certificate store path for debugging purposes
                    Console.WriteLine("Info: Sessionconfiguration created, certificate store path => " + sessionConfiguration.CertificateStorePath);

                    // Create a new OPC UA client instance with license credentials
                    using (UaClient client = new UaClient(LicenseUserName, LicenseSerial, sessionConfiguration))
                    {
                        Console.WriteLine("Info: license state => " + client.GetLicenceMessage());
                        Console.WriteLine("");

                        // Register event handlers to monitor the connection state
                        client.ServerConnectionLost += Client_ServerConnectionLost;
                        client.ServerConnected += Client_ServerConnected;
                        client.SessionClosing += Client_SessionClosing;
                        client.KeepAlive += Client_KeepAlive;
                        client.CertificateValidation += client_CertificateValidation;

                        // Create a new subscription for monitoring data changes
                        using (Subscription subscription = new Subscription())
                        {
                            subscription.PublishingInterval = 1000;
                            subscription.PublishingEnabled = false;
                            subscription.DisplayName = "mySubsription";

                            // Register subscription state change events
                            subscription.StateChanged += Subscription_StateChanged;
                            subscription.PublishStatusChanged += Subscription_PublishStatusChanged;

                            // Add the subscription to the client instance
                            client.AddSubscription(subscription);
                            try
                            {
                                // Create a monitored item for the specified node
                                NodeId nodeId = client.GetNodeIdByPath("Objects.Simulation.Random");
                                MonitoredItem monitoredItem = new MonitoredItem(subscription.DefaultItem)
                                {
                                    StartNodeId = nodeId,
                                    SamplingInterval = 500,
                                    QueueSize = UInt32.MaxValue,
                                    DisplayName = nodeId.ToString()
                                };

                                // Register the notification callback for value changes
                                monitoredItem.Notification += Client_MonitorNotification;
                                // Add the monitored item to the subscription
                                subscription.AddItem(monitoredItem);

                                 nodeId = client.GetNodeIdByPath("Objects.Simulation.Counter");
                                monitoredItem = new MonitoredItem(subscription.DefaultItem)
                                {
                                    StartNodeId = nodeId,
                                    SamplingInterval = 500,
                                    QueueSize = UInt32.MaxValue,
                                    DisplayName = nodeId.ToString()
                                };

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

    private void Client_MonitorNotification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
    {
        MonitoredItemNotification notification = e.NotificationValue as MonitoredItemNotification;
        Console.WriteLine(monitoredItem.StartNodeId.Identifier + " Value: " + notification.Value + " Status: " + notification.Value.StatusCode.ToString());
    }

    private void Subscription_StateChanged(Subscription subscription, SubscriptionStateChangedEventArgs e)
    {
        Console.WriteLine(DateTime.Now.ToLocalTime() + " State of Subscription " + UaClient.SubscriptionToString(subscription) + " changed to => " + e.Status.ToString());
    }

    private void Subscription_PublishStatusChanged(object sender, EventArgs e)
    {
        /*
        check your publish state of your subscription
        if the publish state permanent stopped, then you have to recreate your subscription with old subscription as template
        In this case, please have a look to the PublishingInterval setting, possibly be the value must be increased
        */

        Subscription subscription = sender as Subscription;
        if (subscription != null)
        {
            PublishingState currentpublishingState = subscription.PublishingStopped ? PublishingState.STOPPED : PublishingState.RUNNING;
            if (currentpublishingState != publishingState || currentpublishingState == PublishingState.STOPPED)
                Console.WriteLine(DateTime.Now.ToLocalTime() + "Publishing state of Subscription " + UaClient.SubscriptionToString((Subscription)sender) + " => " + currentpublishingState.ToString());

            publishingState = currentpublishingState;
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

    private enum PublishingState
    {
        UNDEFINED,
        RUNNING,
        STOPPED
    }
}
