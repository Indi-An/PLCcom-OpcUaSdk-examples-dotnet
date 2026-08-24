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
// PLCcom OPC UA Client SDK - Workshop 61: Simple Events
//
// OPC UA Events are notifications about discrete occurrences -
// not value changes, but things that happened (state transitions,
// warnings, operator actions). This workshop subscribes to events
// and displays them as they arrive.
//
// What you will learn:
//   * How to create an event subscription
//   * How to define event filters (which fields to receive)
//   * How to receive and display event notifications
//   * How to read event properties (message, severity, source)
//
// Target server: opc.tcp://localhost:48410
// ==============================================================================

using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Client;
using PLCcom.Opc.Ua.Client.Sdk;
using System;
using System.Net.Sockets;

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

            Console.WriteLine();

            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 61: Simple Events       ║");
            Console.WriteLine("║                                                              ║");
            Console.WriteLine("║  OPC UA Events are notifications about discrete              ║");
            Console.WriteLine("║  occurrences - not value changes, but things that            ║");
            Console.WriteLine("║  happened. Subscribe to events and display them.             ║");
            Console.WriteLine("║                                                              ║");
            Console.WriteLine("║  What you will learn:                                        ║");
            Console.WriteLine("║    * Create an event subscription with filters               ║");
            Console.WriteLine("║    * Receive and display event notifications                 ║");
            Console.WriteLine("║    * Read event properties (message, severity, source)       ║");
            Console.WriteLine("║                                                              ║");
            Console.WriteLine("║  Required server: Server Workshop 61 (Simple Events)         ║");
            Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            // Important !!!!!!!!!!!!!!!!!!
            // Enter your Username + Serial here! Please note: with blank fields the library runs
            // for 15 minutes during a debug session. Both values can also come
            // from configuration or an environment variable.
            // Free trial license (14 days, uninterrupted): https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-download/
            string LicenseUserName = "";
            string LicenseSerial = "";

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

                    //enable auto connect functionality
                    sessionConfiguration.AutoConnect = true;

                    //output certificate store path
                    Console.WriteLine("Info: Sessionconfiguration created, certificate store path => " + sessionConfiguration.CertificateStorePath);

                    //Create a new opc client instance and pass your license information
                    using (UaClient client = new UaClient(LicenseUserName, LicenseSerial, sessionConfiguration))
                    {
                        Console.WriteLine("Info: license state => " + client.GetLicenceMessage());
                        Console.WriteLine("");

                        //register events
                        client.ServerConnectionLost += Client_ServerConnectionLost;
                        client.ServerConnected += Client_ServerConnected;
                        client.SessionClosing += Client_SessionClosing;
                        client.KeepAlive += Client_KeepAlive;
                        client.CertificateValidation += client_CertificateValidation;

                        //create a new subscription
                        using (Subscription subscription = new Subscription())
                        {
                            subscription.PublishingInterval = 1000;
                            subscription.PublishingEnabled = false;
                            subscription.DisplayName = "mySimpleEventClientSubsc";

                            //register subscription events
                            subscription.StateChanged += Subscription_StateChanged;
                            subscription.PublishStatusChanged += Subscription_PublishStatusChanged;

                            //add new subscription to client
                            client.AddSubscription(subscription);
                            try
                            {
                                //Create a monitoring item and add to the subscription
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
                                        // Which fields we want delivered:
                                        // BaseEventType: Message, Severity, Time, SourceName
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
                                        },
                                        new SimpleAttributeOperand() {
                                            TypeDefinitionId = ObjectTypeIds.BaseEventType,
                                            BrowsePath = new QualifiedNameCollection { BrowseNames.SourceName },
                                            AttributeId = Attributes.Value
                                        }
                                    }
                                    }
                                };

                                //register monitoring event
                                monitoredItem.Notification += Client_MonitorNotification;
                                //add Item to subscription
                                subscription.AddItem(monitoredItem);



                                //apply changes
                                subscription.ApplyChanges();

                                //enable publishing mode of subscription and set PublishingInterval
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
        // Events:
        if (e.NotificationValue is EventFieldList ev)
        {
            // Sequence corresponds to SelectClauses (Message, Severity, Time, SourceName)
            var message = ev.EventFields[0].Value as LocalizedText;
            var severity = ev.EventFields[1].Value is ushort u ? u : (ushort)0;
            var time = ev.EventFields[2].Value is DateTime dt ? dt : DateTime.MinValue;
            var source = ev.EventFields[3].Value as string ?? "";

            Console.WriteLine($"  [EVENT] {time:HH:mm:ss.fff} UTC  Source={source,-16} Severity={severity,-6} {message?.Text}");
            return;
        }

        if (e.NotificationValue is MonitoredItemNotification dn)
        {
            Console.WriteLine($"{monitoredItem.StartNodeId} Value: {dn.Value} Status: {dn.Value.StatusCode}");
            return;
        }

        Console.WriteLine($"Unerwarteter Notification-Typ: {e.NotificationValue?.GetType().Name ?? "null"}");
    }


    private void Subscription_StateChanged(Subscription subscription, SubscriptionStateChangedEventArgs e)
    {
        Console.WriteLine(DateTime.Now.ToLocalTime() + " State of Subscription " + subscription.ToDisplayString() + " changed to => " + e.Status.ToString());
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
                Console.WriteLine(DateTime.Now.ToLocalTime() + "Publishing state of Subscription " + subscription.ToDisplayString() + " => " + currentpublishingState.ToString());

            publishingState = currentpublishingState;
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

    private enum PublishingState
    {
        UNDEFINED,
        RUNNING,
        STOPPED
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
    {        string alias = System.Reflection.Assembly.GetEntryAssembly().GetName().Name;
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