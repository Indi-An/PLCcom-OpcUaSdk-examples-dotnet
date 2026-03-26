using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Client;
using PLCcom.Opc.Ua.Client.Sdk;

class Program
{

    //define the ua client object
    private UaClient client = null;

    //the default event filter object
    private EventFilter defaultFilter = null;

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

            //Submit your license information from your license e-mail
            string LicenseUserName = "<Enter your UserName here>";
            string LicenseSerial = "<Enter your Serial here>";

            EndpointDescriptionCollection Endpoints = UaClient.GetEndpoints(new Uri("opc.tcp://localhost:50540/PLCcom/HistoricalEventsServer"), 60000);

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
                    Console.WriteLine();

                    //create the Defaultfilter
                    defaultFilter = client.CreateFilter(BrowseNames.EventType,
                                                         BrowseNames.SourceNode,
                                                         BrowseNames.SourceName,
                                                         BrowseNames.Time,
                                                         BrowseNames.ReceiveTime,
                                                         BrowseNames.Message,
                                                         BrowseNames.Severity,
                                                         BrowseNames.EventId);

                    //set target NodeId
                    NodeId nodeId = new NodeId("ns=2;s=Area51"); //'Objects.Server.Plaforms.Area51'

                    if (nodeId != null)
                    {
                        try
                        {

                            HistoryEvent result = client.HistoryRead(nodeId,                //browse path from node
                                                                    defaultFilter,       //filter with the reading structure
                                                                    DateTime.Now.AddDays(-1),  //starttime
                                                                    DateTime.Now,               //endtime
                                                                    10);                         //max number of reading elements, 0 = unlimited

                            //show actual event alarm data in debug window
                            StringBuilder sb = new StringBuilder();
                            sb.Append(Environment.NewLine);
                            int EventIdIndex = -1;

                            foreach (HistoryEventFieldList ev in result.Events)
                            {
                                for (int i = 0; i < ev.EventFields.Count; i++)
                                {
                                    if (ev.EventFields[i].Value != null)
                                    {
                                        //Important => method returns all timestamps in universal time format
                                        string eventName = GetEventFilterMappings(defaultFilter)[i];

                                        //store the index of eventid for eventual deleting the events
                                        if (EventIdIndex == -1 && eventName.Replace("/", "").ToLower().Equals("eventid")) EventIdIndex = i;

                                        object value = ev.EventFields[i].Value;
                                        //if value equals enetId, then convert value to hexstring
                                        if (EventIdIndex > -1 && EventIdIndex == i) value = ByteArrayToString((byte[])ev.EventFields[EventIdIndex].Value);

                                        sb.Append(String.Format(" " + eventName + " {0}", value.ToString()));
                                    }
                                }
                                sb.Append(Environment.NewLine);
                            }
                            sb.Append(Environment.NewLine);
                            sb.Append(Environment.NewLine);
                            Console.WriteLine(sb.ToString());

                            if (EventIdIndex > -1) //the index of eventid is needed, 
                            {
                                Console.WriteLine("Do you want to delete all read events from the server? 'y'=yes, 'n'=not");
                                if (Console.ReadLine().ToLower().Equals("y"))
                                {
                                    // Create Request data
                                    DeleteEventDetails deleteDetails = new DeleteEventDetails();
                                    deleteDetails.NodeId = nodeId;

                                    //add the eventid for deleting
                                    foreach (HistoryEventFieldList ev in result.Events)
                                    {
                                        //delete event
                                        HistoryUpdateResult deleteResult = client.HistoryUpdate(nodeId, (byte[])ev.EventFields[EventIdIndex].Value);
                                        Console.WriteLine("delete event with eventId " + ByteArrayToString((byte[])ev.EventFields[EventIdIndex].Value) + " result => " + deleteResult.StatusCode);
                                    }
                                    Console.WriteLine("");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("invalid number of Endpoint");
                    Console.WriteLine();
                    Console.WriteLine("press enter for exit");
                    Console.ReadLine();
                }
            }
            else
            {
                Console.WriteLine("no endpoints found");
                Console.WriteLine();
                Console.WriteLine("press enter for exit");
                Console.ReadLine();
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

    public static byte[] StringToByteArray(String hex)
    {
        int NumberChars = hex.Length;
        byte[] bytes = new byte[NumberChars / 2];
        for (int i = 0; i < NumberChars; i += 2)
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        return bytes;
    }

    public static string ByteArrayToString(byte[] ba)
    {
        StringBuilder hex = new StringBuilder(ba.Length * 2);
        foreach (byte b in ba)
            hex.AppendFormat("{0:x2}", b);
        return hex.ToString();
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
