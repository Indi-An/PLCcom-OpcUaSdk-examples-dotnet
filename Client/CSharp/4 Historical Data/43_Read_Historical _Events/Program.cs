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
// PLCcom OPC UA Client SDK - Workshop 43: Read Historical Events
//
// In addition to historical data values, OPC UA servers can store
// historical events (alarms, state changes, operator actions).
// This workshop reads past events from the server using HistoryRead.
//
// What you will learn:
//   * How to read historical events for a time range
//   * How to specify event filter fields (which properties to retrieve)
//   * How to interpret historical event results
//   * How to delete historical events by EventId
//
// Required server: Server Workshop 33 (Historical Events)
// opc.tcp://localhost:48410
// ==============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Client;
using PLCcom.Opc.Ua.Client.Sdk;

class Program
{
    private UaClient client = null;

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
            Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 43: Read Hist. Events   ║");
            Console.WriteLine("║                                                              ║");
            Console.WriteLine("║  OPC UA servers can store historical events (alarms, state   ║");
            Console.WriteLine("║  changes, operator actions). This workshop reads past        ║");
            Console.WriteLine("║  events from the server using HistoryRead.                   ║");
            Console.WriteLine("║                                                              ║");
            Console.WriteLine("║  What you will learn:                                        ║");
            Console.WriteLine("║    * Read historical events for a time range                 ║");
            Console.WriteLine("║    * Specify event filter fields                             ║");
            Console.WriteLine("║    * Interpret historical event results                      ║");
            Console.WriteLine("║    * Delete historical events by EventId                     ║");
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
            // Server 33 creates: Plant -> Reactor with EnableHistoryEvents()
            NodeId nodeId = client.GetNodeIdByPath("Objects.Plant.Reactor");
            if (nodeId == null)
            {
                Console.WriteLine("Could not find 'Objects.Plant.Reactor'. Is Server Workshop 33 running?");
                Console.ReadLine();
                return;
            }
            Console.WriteLine($"  Reactor NodeId: {nodeId}");
            Console.WriteLine();

            // -- Build the event filter ----------------------------------------
            // For HistoryRead we build a simple EventFilter manually.
            // SelectClauses define which fields to retrieve per event.
            // TypeDefinitionId = BaseEventType means: apply to all event types.
            var filter = new EventFilter();
            filter.SelectClauses.Add(MakeField(BrowseNames.EventType));
            filter.SelectClauses.Add(MakeField(BrowseNames.SourceName));
            filter.SelectClauses.Add(MakeField(BrowseNames.Time));
            filter.SelectClauses.Add(MakeField(BrowseNames.Message));
            filter.SelectClauses.Add(MakeField(BrowseNames.Severity));
            filter.SelectClauses.Add(MakeField(BrowseNames.EventId));
            const int eventIdIndex = 5;

            // -- Command loop --------------------------------------------------
            var lastEvents = new List<HistoryEventFieldList>();

            while (true)
            {
                Console.WriteLine("  Select operation:");
                Console.WriteLine("  1 - Read    (read historical events, last 24 hours)");
                Console.WriteLine("  2 - Delete  (delete all last-read events by EventId)");
                Console.WriteLine("  3 - Exit");
                Console.Write("  > ");

                string input = Console.ReadLine();
                if (string.IsNullOrEmpty(input) || input == "3") break;

                switch (input)
                {
                    case "1":
                        {
                            lastEvents = ReadHistoricalEvents(client, nodeId, filter, eventIdIndex);
                            break;
                        }
                    case "2":
                        {
                            if (lastEvents.Count == 0)
                                Console.WriteLine("  No events loaded yet. Use option 1 first.");
                            else
                                DeleteEvents(client, nodeId, lastEvents, eventIdIndex);
                            break;
                        }
                    default:
                        Console.WriteLine("  Unknown option.");
                        break;
                }
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        finally
        {
            Console.WriteLine();
            Console.WriteLine("press enter for exit");
            Console.ReadLine();
            if (client != null && client.GetSessionState().Equals(SessionState.Connected))
                client.Disconnect();
        }
    }

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

    static SimpleAttributeOperand MakeField(string browseName)
    {
        return new SimpleAttributeOperand
        {
            TypeDefinitionId = ObjectTypeIds.BaseEventType,
            BrowsePath = new QualifiedNameCollection { new QualifiedName(browseName) },
            AttributeId = Attributes.Value
        };
    }

    static List<HistoryEventFieldList> ReadHistoricalEvents(
        UaClient client, NodeId nodeId, EventFilter filter, int eventIdIndex)
    {
        var result = new List<HistoryEventFieldList>();
        try
        {
            var history = client.HistoryRead(nodeId, filter,
                DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, 100);

            if (history?.Events == null || history.Events.Count == 0)
            {
                Console.WriteLine("  (no historical events found)");
                Console.WriteLine("  Tip: Let Server Workshop 33 run for a while to accumulate events.");
                return result;
            }

            Console.WriteLine($"  {history.Events.Count} historical event(s) found:");
            Console.WriteLine();

            foreach (HistoryEventFieldList ev in history.Events)
            {
                var f = ev.EventFields;
                string time = f[2].Value is DateTime dt ? dt.ToLocalTime().ToString("HH:mm:ss.fff") : "";
                string message = f[3].Value?.ToString() ?? "";
                string severity = f[4].Value?.ToString() ?? "";
                string eventId = f[eventIdIndex].Value is byte[] id ? ByteArrayToString(id) : "";
                Console.WriteLine($"  {time}  [{severity,4}]  {message}");
                Console.WriteLine($"           EventId={eventId}");
                result.Add(ev);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("  Error: " + ex.Message);
        }
        return result;
    }

    static void DeleteEvents(
        UaClient client, NodeId nodeId,
        List<HistoryEventFieldList> events, int eventIdIndex)
    {
        try
        {
            var eventIds = new List<byte[]>();
            foreach (var ev in events)
            {
                if (ev.EventFields[eventIdIndex].Value is byte[] id)
                    eventIds.Add(id);
            }
            if (eventIds.Count == 0) { Console.WriteLine("  No EventIds found."); return; }

            HistoryUpdateResult r = client.HistoryUpdate(nodeId, eventIds);
            Console.WriteLine($"  Deleted {eventIds.Count} event(s)  Result={r?.StatusCode.ToString() ?? "(null)"}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("  Error: " + ex.Message);
        }
    }

    public static string ByteArrayToString(byte[] ba)
    {
        var hex = new StringBuilder(ba.Length * 2);
        foreach (byte b in ba) hex.AppendFormat("{0:x2}", b);
        return hex.ToString();
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