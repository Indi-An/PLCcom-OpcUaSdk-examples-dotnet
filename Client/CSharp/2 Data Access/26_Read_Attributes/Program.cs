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

                        //Read multiple attributes of Node within one call 

                        //define the source NodeId, in  this case s=2:Int16Value 
                        NodeId sourceId = client.GetNodeIdByPath("Objects.Data.Static.Scalar.Int16Value");

                        //create a ReadValueIdCollection and fill this with ReadValueId objects
                        ReadValueIdCollection nodesToRead = new ReadValueIdCollection();

                       for (uint ii = Attributes.NodeClass; ii <= Attributes.AccessLevelEx; ii++)
                        {
                            ReadValueId nodeToRead = new ReadValueId();
                            nodeToRead.NodeId = sourceId;
                            nodeToRead.AttributeId = ii;
                            nodesToRead.Add(nodeToRead);
                        }

                        // Read the node values synchronously
                        Console.WriteLine("Begin reading all attributes of NodeId " + sourceId.ToString());
                        DataValueCollection readresults = client.Read(nodesToRead);

                        //print the results
                        for (int ii = 0; ii < readresults.Count; ii++)
                        {

                            // ignore attributes which are invalid for the node.
                            if (readresults[ii].StatusCode == StatusCodes.BadAttributeIdInvalid)
                            {
                                continue;
                            }

                            // get the name of the attribute.
                            string attributeName = Attributes.GetBrowseName(nodesToRead[ii].AttributeId);
                            string datatype = string.Empty;
                            string value = string.Empty;

                            // display any unexpected error.
                            if (StatusCode.IsBad(readresults[ii].StatusCode))
                            {
                                datatype = Utils.Format("{0}", Attributes.GetDataTypeId(nodesToRead[ii].AttributeId));
                                value = Utils.Format("{0}", readresults[ii].StatusCode);
                            }

                            // display the value.
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

                            Console.WriteLine(Utils.Format( "read Attribute {0}, DataType => {1}, Value => {2}", attributeName, datatype, value));
                        }
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
