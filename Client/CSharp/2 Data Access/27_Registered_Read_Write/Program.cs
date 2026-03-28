using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Client;
using PLCcom.Opc.Ua.Client.Sdk;
using System;

class Program
{

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

                        //sample nodeIds to register
                        NodeIdCollection nodetoRegister = new NodeIdCollection();
                        nodetoRegister.Add(new NodeId("ns=2;i=10221")); //Objects.Data.Static.Scalar.Int32Value by plccom demonstration dataaccess server
                        nodetoRegister.Add(client.GetNodeIdByPath("Objects.Data.Static.Scalar.Int16Value"));
                        //nodetoRegister.Add(your nodeid) 
                        //nodetoRegister.Add(your nodeid)
                        //nodetoRegister.Add(your nodeid)

                        NodeIdCollection registeredNodeIds = null;
                        RegisterNodesRequest req = new RegisterNodesRequest();
                        req.NodesToRegister = nodetoRegister;

                        //register Nodes
                        RegisterNodesResponse res = client.RegisterNodes(req);
                        Console.WriteLine(Utils.Format("Nodes with Statuscode => {0} registered", res.ResponseHeader.ServiceResult));

                        if (StatusCode.IsGood(res.ResponseHeader.ServiceResult))
                        {

                             registeredNodeIds = res.RegisteredNodeIds;

                            //write your registered node
                            StatusCode sc = client.Write(registeredNodeIds[0], 12345, Attributes.Value);
                            if (StatusCode.IsGood(sc))
                                Console.WriteLine(Utils.Format("write Node {0} Statuscode => {1}", registeredNodeIds[0].ToString(), sc.ToString()));
                            else
                                Console.WriteLine(Utils.Format("write failed for Node {0} Statuscode => {1}", registeredNodeIds[0].ToString(), sc.ToString()));

                            //copy NodeIdCollection to ReadValueIdCollection
                            ReadValueIdCollection readValueIdCollection = new ReadValueIdCollection();
                            for (int i = 0; i < registeredNodeIds.Count; i++)
                            {
                                ReadValueId rvi = new ReadValueId()
                                {
                                    AttributeId = Attributes.Value,
                                    NodeId = registeredNodeIds[i]
                                };
                                readValueIdCollection.Add(rvi);
                            }

                            //read your registered nodes
                            DataValueCollection readresults = client.Read(readValueIdCollection);

                            //print the results
                            for (int ii = 0; ii < readresults.Count; ii++)
                            {

                                // ignore attributes which are invalid for the node.
                                if (readresults[ii].StatusCode == StatusCodes.BadAttributeIdInvalid)
                                {
                                    continue;
                                }

                                string datatype = string.Empty;
                                string value = string.Empty;

                                // display any unexpected error.
                                if (StatusCode.IsBad(readresults[ii].StatusCode))
                                {
                                    value = Utils.Format("{0}", readresults[ii].StatusCode);
                                }
                                else
                                {
                                    TypeInfo typeInfo = TypeInfo.Construct(readresults[ii].Value);

                                    datatype = typeInfo.BuiltInType.ToString();

                                    if (typeInfo.ValueRank >= ValueRanks.OneOrMoreDimensions)
                                    {
                                        datatype += "[]";
                                    }

                                    value = Utils.Format("{0}", readresults[ii].Value);
                                }

                                Console.WriteLine(Utils.Format("read value, DataType => {0}, Value => {1}", datatype, value));
                            }

                        }
                        else
                        {
                            Console.WriteLine(Utils.Format("Operation RegisterNode failed with StatusCode => {0}", res.ResponseHeader.ServiceResult.ToString()));
                        }
                        //unregister Nodes
                        if (registeredNodeIds != null)
                        {
                            UnregisterNodesRequest ureq = new UnregisterNodesRequest();
                            ureq.NodesToUnregister = registeredNodeIds;
                            UnregisterNodesResponse ures = client.UnregisterNodes(ureq);
                            Console.WriteLine(Utils.Format("Nodes with Statuscode => {0} unregistered", ures.ResponseHeader.ServiceResult.ToString()));
                            Console.WriteLine();
                        }
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
}
