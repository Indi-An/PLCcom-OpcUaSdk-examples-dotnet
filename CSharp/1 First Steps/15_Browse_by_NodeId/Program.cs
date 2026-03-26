using PLCcom.Opc.Ua;
using PLCcom.Opc.Ua.Client.Sdk;
using System;
using PLCcom.Opc.Ua.Client;

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
                    Console.WriteLine(counter++.ToString() + " => " + UaClient.EndpointToString( Endpoint));
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
                    //enable autoconnect
                    sessionConfiguration.AutoConnect= true;

                    // Display the certificate store path for debugging purposes
                    Console.WriteLine("Info: Sessionconfiguration created, certificate store path => " + sessionConfiguration.CertificateStorePath);

                    // Create a new OPC UA client instance with license credentials
                    using (UaClient client = new UaClient(LicenseUserName, LicenseSerial, sessionConfiguration))
                    {
                        Console.WriteLine("Info: license state => " + client.GetLicenceMessage());

                        // Register event handlers to monitor the connection state
                        client.ServerConnectionLost += Client_ServerConnectionLost;
                        client.ServerConnected += Client_ServerConnected;
                        client.KeepAlive += Client_KeepAlive;
                        client.CertificateValidation += client_CertificateValidation;

                        Console.WriteLine("");
                        try
                        {

                            // Set start NodeId, in this case the ObjectsFolder (identifier ns=0:85, see ObjectIds.ObjectsFolder)
                            NodeId sourceNode = new NodeId(85, 0);

                            //Set start NodeId by path
                            // find all of the components of the node.
                            BrowseDescription nodeToBrowse1 = new BrowseDescription();

                            nodeToBrowse1.NodeId = sourceNode;
                            nodeToBrowse1.BrowseDirection = BrowseDirection.Forward;
                            nodeToBrowse1.ReferenceTypeId = ReferenceTypeIds.Aggregates;
                            nodeToBrowse1.IncludeSubtypes = true;
                            nodeToBrowse1.NodeClassMask = (uint)(NodeClass.Object | NodeClass.Variable);
                            nodeToBrowse1.ResultMask = (uint)BrowseResultMask.All;

                            // find all nodes organized by the node.
                            BrowseDescription nodeToBrowse2 = new BrowseDescription();

                            nodeToBrowse2.NodeId = sourceNode;
                            nodeToBrowse2.BrowseDirection = BrowseDirection.Forward;
                            nodeToBrowse2.ReferenceTypeId = ReferenceTypeIds.Organizes;
                            nodeToBrowse2.IncludeSubtypes = true;
                            nodeToBrowse2.NodeClassMask = (uint)(NodeClass.Object | NodeClass.Variable);
                            nodeToBrowse2.ResultMask = (uint)BrowseResultMask.All;

                            BrowseDescriptionCollection nodesToBrowse = new BrowseDescriptionCollection();
                            nodesToBrowse.Add(nodeToBrowse1);
                            nodesToBrowse.Add(nodeToBrowse2);

                            //now, browse the node
                            ReferenceDescriptionCollection rdc = client.BrowseFull(nodesToBrowse);

                            if (rdc.Count > 0)
                            {
                                foreach (ReferenceDescription rd in rdc)
                                {
                                    Console.WriteLine("Child NodeID found => " + rd.NodeId +
                                                " NodeClass => " + rd.NodeClass.ToString() +
                                                " BrowseName => " + rd.BrowseName.ToString() +
                                                " DisplayName => " + rd.DisplayName.ToString());
                                }
                            }else
                            {
                                Console.WriteLine("no references found");
                                Console.WriteLine();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
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

}
