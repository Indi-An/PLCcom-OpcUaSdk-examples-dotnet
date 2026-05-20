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
// PLCcom OPC UA Client SDK - Workshop 71: Reverse Connect
//
// In standard OPC UA, the client connects to the server. With
// Reverse Connect, the server initiates the TCP connection to the
// client. This is useful when the server is behind a firewall
// and cannot accept incoming connections.
//
// What you will learn:
//   * How Reverse Connect differs from standard connections
//   * How to configure the client for Reverse Connect
//   * How to wait for the server to connect back
//
// Target server: opc.tcp://localhost:48410
// ==============================================================================

using System;
using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Client;
using PLCcom.Opc.Ua.Client.Sdk;

// ── Workshop 71: Reverse Connect ─────────────────────────────────────────────
//
// In a standard OPC UA connection the CLIENT connects to the SERVER.
// Reverse Connect turns this around: the SERVER connects to the CLIENT.
//
// This is useful when the server is behind a firewall or NAT and cannot
// accept incoming TCP connections, but is allowed to make outgoing ones.
//
// How it works:
//   1. The client opens a listening port (e.g. opc.tcp://localhost:48500).
//   2. The server periodically sends a ReverseHello message to that port.
//   3. The client receives the ReverseHello and establishes the OPC UA session
//      over the server-initiated TCP connection.
//
// From the application perspective the API is almost identical to a normal
// Connect() - just two extra calls:
//   - StartReverseConnectListener(listenUrl)   ... open the listening port
//   - ConnectReverse(timeout)                  ... wait for ReverseHello + open session
//
// Prerequisites:
//   - A server with Reverse Connect enabled pointing to this client's listen URL.
//     Use the ReverseConnect_Server test project or Workshop Server 61.
//
// ─────────────────────────────────────────────────────────────────────────────

class Program
{
    static void Main(string[] args)
    {
        var p = new Program();
        p.Start();
    }

    void Start()
    {
        try
        {
            Console.WriteLine();
             Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
             Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 71: Reverse Connect     ║");
             Console.WriteLine("║                                                              ║");
             Console.WriteLine("║  With Reverse Connect, the server initiates the TCP          ║");
             Console.WriteLine("║  connection to the client. Useful when the server is         ║");
             Console.WriteLine("║  behind a firewall and cannot accept connections.            ║");
             Console.WriteLine("║                                                              ║");
             Console.WriteLine("║  What you will learn:                                        ║");
             Console.WriteLine("║    * How Reverse Connect differs from standard mode          ║");
             Console.WriteLine("║    * Configure the client for Reverse Connect                ║");
             Console.WriteLine("║    * Wait for the server to connect back                     ║");
             Console.WriteLine("║                                                              ║");
             Console.WriteLine("║  Required server: Server Workshop 71 (Reverse Connect)       ║");
             Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
             Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
             Console.WriteLine();

            // TODO: Submit your license information from your license e-mail
            string LicenseUserName = "<Enter your UserName here>";
            string LicenseSerial   = "<Enter your Serial here>";

            // The URL where this client will listen for incoming ReverseHello messages.
            // The server must be configured to connect to exactly this URL.
            string listenUrl = "opc.tcp://localhost:48500";

            // The server's own endpoint URL - used to identify which server we expect
            // and to configure the OPC UA session (security mode, policy, etc.).
            // This is the server's normal endpoint, NOT the reverse-connect listen URL.
            var endpoint = new EndpointDescription
            {
                EndpointUrl         = "opc.tcp://localhost:48410",
                SecurityMode        = MessageSecurityMode.None,
                SecurityPolicyUri   = SecurityPolicies.None,
                TransportProfileUri = Profiles.UaTcpTransport
            };

            // Build the session configuration.
            // AutoConnect = false because we manage the connection manually via ConnectReverse().
            SessionConfiguration sessionConfiguration = SessionConfiguration.Build(
                sessionName: System.Reflection.Assembly.GetEntryAssembly().GetName().Name,
                endpoint:    endpoint);
            sessionConfiguration.AutoConnect = false;
            sessionConfiguration.AutoAcceptUntrustedCertificates = true;

            Console.WriteLine("Info: SessionConfiguration created.");
            Console.WriteLine();

            using (UaClient client = new UaClient(LicenseUserName, LicenseSerial, sessionConfiguration))
            {
                Console.WriteLine("Info: license state => " + client.GetLicenceMessage());
                Console.WriteLine();

                // Register event handlers
                client.ServerConnected      += Client_ServerConnected;
                client.ServerConnectionLost += Client_ServerConnectionLost;
                client.SessionClosing       += Client_SessionClosing;
                client.KeepAlive            += Client_KeepAlive;
                client.CertificateValidation += Client_CertificateValidation;

                // Step 1: Open the listening port.
                //         The server will connect here and send a ReverseHello.
                client.StartReverseConnectListener(listenUrl);
                Console.WriteLine("Listening for ReverseHello on: " + listenUrl);
                Console.WriteLine("Waiting for server to connect (timeout 60s)...");
                Console.WriteLine();

                // Step 2: Wait for the ReverseHello and establish the session.
                //         This call blocks until the server connects or the timeout expires.
                //         Internally it mirrors Connect() - same certificate and security logic.
                client.ConnectReverse(timeout: 60000);

                Console.WriteLine("Session established: " + client.GetSession().SessionName);
                Console.WriteLine();

                // Step 3: Subscribe to a node and monitor its value.
                //         After this point the client works exactly like after a normal Connect().
                NodeId nodeId = client.GetNodeIdByPath("Objects.Plant.Temperature");

                using (Subscription subscription = new Subscription())
                {
                    subscription.PublishingInterval = 1000;
                    subscription.PublishingEnabled  = true;
                    subscription.DisplayName        = "ReverseConnectSubscription";

                    subscription.StateChanged       += Subscription_StateChanged;
                    subscription.PublishStatusChanged += Subscription_PublishStatusChanged;

                    client.AddSubscription(subscription);

                    MonitoredItem monitoredItem = new MonitoredItem(subscription.DefaultItem)
                    {
                        StartNodeId      = nodeId,
                        SamplingInterval = 500,
                        QueueSize        = uint.MaxValue,
                        DisplayName      = "Temperature"
                    };

                    monitoredItem.Notification += Client_MonitorNotification;
                    subscription.AddItem(monitoredItem);
                    subscription.ApplyChanges();

                    Console.WriteLine("Monitoring Temperature - press ENTER to exit.");
                    Console.ReadLine();
                }

                client.Disconnect();
            }
        }
        catch (TimeoutException ex)
        {
            Console.WriteLine("Timeout: " + ex.Message);
            Console.WriteLine("Is the server running and configured for Reverse Connect?");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        finally
        {
            Console.WriteLine("Press ENTER to exit.");
            Console.ReadLine();
        }
    }

    private void Client_MonitorNotification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
    {
        MonitoredItemNotification notification = e.NotificationValue as MonitoredItemNotification;
        if (notification != null)
            Console.WriteLine(monitoredItem.DisplayName + " = " + notification.Value.Value
                + "  (" + notification.Value.StatusCode + ")"
                + "  [" + notification.Value.SourceTimestamp.ToLocalTime().ToString("HH:mm:ss") + "]");
    }

    private void Subscription_StateChanged(Subscription subscription, SubscriptionStateChangedEventArgs e)
    {
        Console.WriteLine(DateTime.Now.ToLocalTime() + " Subscription state => " + e.Status);
    }

    private void Subscription_PublishStatusChanged(object sender, EventArgs e)
    {
        // If publishing stops permanently, recreate the subscription.
        // Consider increasing PublishingInterval if this happens frequently.
        Subscription subscription = sender as Subscription;
        if (subscription != null && subscription.PublishingStopped)
            Console.WriteLine(DateTime.Now.ToLocalTime() + " Publishing STOPPED for: "
                + subscription.ToDisplayString());
    }

    private void Client_CertificateValidation(CertificateValidator sender, CertificateValidationEventArgs e)
    {
        if (ServiceResult.IsGood(e.Error))
            e.Accept = true;
        else if (!e.ContainsUnsuppressibleStatusCodes)
            e.Accept = true;
        else if (e.ContainsUnsuppressibleStatusCodes)
            e.AcceptAll = true;
        else
            throw new Exception(string.Format("Failed to validate certificate with error code {0}: {1}",
                e.Error.Code, e.Error.AdditionalInfo));
    }

    private void Client_ServerConnected(object sender, EventArgs e)
    {
        Console.WriteLine(DateTime.Now.ToLocalTime() + " Session connected.");
    }

    private void Client_ServerConnectionLost(object sender, EventArgs e)
    {
        Console.WriteLine(DateTime.Now.ToLocalTime() + " Session connection lost.");
    }

    private void Client_KeepAlive(ISession session, KeepAliveEventArgs e)
    {
        // Called periodically to confirm the server is still reachable.
    }

    private void Client_SessionClosing(object sender, EventArgs e)
    {
        Console.WriteLine(DateTime.Now.ToLocalTime() + " Session closing.");
    }
}
