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
// PLCcom OPC UA Client SDK - Workshop 44: Monitor Historical Events
//
// This workshop subscribes to live events from a node that also has
// event history enabled. New events arrive in real-time via subscription
// and are also stored in the server's event history for later retrieval.
//
// What you will learn:
//   * How to subscribe to live events from a history-enabled source node
//   * How to receive and display event notifications in real-time
//   * The difference between live events (subscription) and
//     historical events (HistoryRead) - see Workshop 43
//
// Required server: Server Workshop 33 (Historical Events)
// opc.tcp://localhost:48410
// ==============================================================================

using System;
using System.Text;
using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Client;
using PLCcom.Opc.Ua.Client.Sdk;

class Program
{
    private UaClient client = null;

    // Field names match the SelectClauses order below — used for readable output.
    private static readonly string[] FieldNames =
        { "EventId", "EventType", "SourceNode", "SourceName", "Time", "Message", "Severity" };
    private const int IDX_EVENTID = 0;

    static void Main(string[] args)
    {
        new Program().Start();
    }

    void Start()
    {
        try
        {
            Console.WriteLine();
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 44: Monitor Hist. Events║");
            Console.WriteLine("║                                                              ║");
            Console.WriteLine("║  Subscribes to live events from a node that also has event   ║");
            Console.WriteLine("║  history enabled. New events arrive in real-time and are     ║");
            Console.WriteLine("║  also stored in the server's history for later retrieval.    ║");
            Console.WriteLine("║                                                              ║");
            Console.WriteLine("║  What you will learn:                                        ║");
            Console.WriteLine("║    * Subscribe to live events from a history-enabled node    ║");
            Console.WriteLine("║    * Receive and display event notifications in real-time    ║");
            Console.WriteLine("║    * Difference: live events vs. HistoryRead (WS 43)         ║");
            Console.WriteLine("║                                                              ║");
            Console.WriteLine("║  Required server: Server Workshop 33 (Historical Events)     ║");
            Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            //TODO
            //Submit your license information from your license e-mail
            string LicenseUserName = "<Enter your UserName here>";
            string LicenseSerial = "<Enter your Serial here>";

            var endpoints = UaClient.GetEndpoints(new Uri("opc.tcp://localhost:48410"),
                certificateValidator: client_CertificateValidation);
            endpoints = UaClient.SortEndpointsBySecurityLevel(endpoints);

            if (endpoints.Count == 0)
            {
                Console.WriteLine("No endpoints found. Is Server Workshop 33 running?");
                Console.ReadLine();
                return;
            }

            Console.WriteLine("endpoints found:");
            for (int i = 0; i < endpoints.Count; i++)
                Console.WriteLine($"{i} => {endpoints[i].ToDisplayString()}");

            Console.WriteLine("please enter index of desired endpoint");
            if (!int.TryParse(Console.ReadLine(), out int idx) || idx < 0 || idx >= endpoints.Count)
            {
                Console.WriteLine("invalid number of Endpoint");
                Console.ReadLine();
                return;
            }
            Console.WriteLine();

            var sessionConfig = CreateConfig(endpoints[idx]);

            client = new UaClient(LicenseUserName, LicenseSerial, sessionConfig);
            Console.WriteLine("Info: license state => " + client.GetLicenceMessage());

            client.ServerConnectionLost += Client_ServerConnectionLost;
            client.ServerConnected += Client_ServerConnected;
            client.SessionClosing += Client_SessionClosing;
            client.KeepAlive += Client_KeepAlive;
            client.CertificateValidation += client_CertificateValidation;

            Console.Write("  Connecting ... ");
            client.Connect();
            Console.WriteLine("OK");
            Console.WriteLine();

            // -- Resolve the reactor node (event source) via browse path ------
            // Server 33 creates: Plant -> Reactor with EnableEvents() + EnableHistoryEvents()
            NodeId nodeId = client.GetNodeIdByPath("Objects.Plant.Reactor");
            if (nodeId == null)
            {
                Console.WriteLine("Could not find 'Objects.Plant.Reactor'. Is Server Workshop 33 running?");
                Console.ReadLine();
                return;
            }
            Console.WriteLine($"  Reactor NodeId: {nodeId}");
            Console.WriteLine();

            // -- Create event filter ------------------------------------------
            // Explicit SelectClauses so field names and order are known.
            // FieldNames array above must match this order exactly.
            var filter = new EventFilter();
            foreach (var name in FieldNames)
                filter.SelectClauses.Add(new SimpleAttributeOperand
                {
                    TypeDefinitionId = ObjectTypeIds.BaseEventType,
                    BrowsePath = new QualifiedNameCollection { new QualifiedName(name) },
                    AttributeId = Attributes.Value
                });

            // -- Create subscription ------------------------------------------
            var subscription = new Subscription
            {
                PublishingInterval = 1000,
                PublishingEnabled = true,
                DisplayName = "myEventSubscription"
            };
            subscription.StateChanged += Subscription_StateChanged;
            client.AddSubscription(subscription);

            // -- Create monitored item for events -----------------------------
            var reference = client.GetReferenceDescriptionByNodeId(nodeId);
            if (reference == null)
            {
                Console.WriteLine("Cannot read reference description for reactor node.");
                Console.ReadLine();
                return;
            }

            var monitoredItem = new MonitoredItem((ITelemetryContext)null)
            {
                NodeClass = reference.NodeClass,
                AttributeId = Attributes.EventNotifier,
                MonitoringMode = MonitoringMode.Reporting,
                StartNodeId = nodeId,
                Filter = filter,
                DisplayName = "reactor event monitoring",
                QueueSize = uint.MaxValue,
                DiscardOldest = true
            };

            monitoredItem.Notification += Client_MonitorNotification;
            subscription.AddItem(monitoredItem);
            subscription.ApplyChanges();

            Console.WriteLine("Start monitoring... (press ENTER to exit)");
            Console.ReadLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        finally
        {
            Console.WriteLine("press enter for exit");
            Console.ReadLine();
            if (client != null && client.GetSessionState().Equals(SessionState.Connected))
                client.Disconnect();
        }
    }

    private void Client_MonitorNotification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
    {
        try
        {
            if (e.NotificationValue is not EventFieldList notification) return;

            var sb = new StringBuilder();
            sb.AppendLine($"{DateTime.Now.ToLocalTime():T} new event notification:");
            for (int i = 0; i < notification.EventFields.Count && i < FieldNames.Length; i++)
            {
                object val = notification.EventFields[i].Value;
                if (val == null) continue;
                string display = (i == IDX_EVENTID && val is byte[] ba) ? ToHex(ba) : val.ToString();
                sb.AppendLine($"  {FieldNames[i]} = {display}");
            }
            Console.WriteLine(sb.ToString());
        }
        catch (Exception ex) { Console.WriteLine(ex.Message); }
    }

    private void Subscription_StateChanged(Subscription subscription, SubscriptionStateChangedEventArgs e)
        => Console.WriteLine($"{DateTime.Now.ToLocalTime()} Subscription state => {e.Status}");

    void client_CertificateValidation(CertificateValidator sender, CertificateValidationEventArgs e)
    {
        if (ServiceResult.IsGood(e.Error)) e.Accept = true;
        else if (!e.ContainsUnsuppressibleStatusCodes) e.Accept = true;
        else if (e.ContainsUnsuppressibleStatusCodes) e.AcceptAll = true;
        else throw new Exception($"Certificate validation failed: {e.Error.Code} {e.Error.AdditionalInfo}");
    }

    private void Client_ServerConnected(object sender, EventArgs e)
        => Console.WriteLine(DateTime.Now.ToLocalTime() + " Session connected");

    private void Client_ServerConnectionLost(object sender, EventArgs e)
        => Console.WriteLine(DateTime.Now.ToLocalTime() + " Session connection lost");

    void Client_KeepAlive(ISession session, KeepAliveEventArgs e) { }

    private void Client_SessionClosing(object sender, EventArgs e)
        => Console.WriteLine(DateTime.Now.ToLocalTime() + " Session closed");

    static string ToHex(byte[] ba)
    {
        var sb = new StringBuilder(ba.Length * 2);
        foreach (byte b in ba) sb.AppendFormat("{0:x2}", b);
        return sb.ToString();
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