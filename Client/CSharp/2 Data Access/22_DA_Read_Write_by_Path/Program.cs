using System;
using System.Threading.Tasks;
using PLCcom.Opc.Ua.Client.Sdk;
using PLCcom.Opc.Ua.Client;
using PLCcom.Opc.Ua;

class Program
{

    UaClient client = null;
    static async Task Main(string[] args)
    {
        Program program = new Program();
        await program.Start();
    }
    async Task Start()
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

                    //enable autoconnect
                    sessionConfiguration.AutoConnect = true;

                    // Display the certificate store path for debugging purposes
                    Console.WriteLine("Info: Sessionconfiguration created, certificate store path => " + sessionConfiguration.CertificateStorePath);

                    // Create a new OPC UA client instance with license credentials
                    client = new UaClient(LicenseUserName, LicenseSerial, sessionConfiguration);

                    Console.WriteLine("Info: license state => " + client.GetLicenceMessage());
                    Console.WriteLine("");

                    // Register event handlers to monitor the connection state
                    client.ServerConnectionLost += Client_ServerConnectionLost;
                    client.ServerConnected += Client_ServerConnected;
                    client.KeepAlive += Client_KeepAlive;
                    client.CertificateValidation += client_CertificateValidation;

                    client.Connect();

                    Console.WriteLine("press enter to reading synchronous..");
                    Console.ReadLine();

                    //Read multiple Nodes within one call 

                    // Create a collection of nodes to read
                    ReadValueIdCollection nodesToRead = new ReadValueIdCollection();
                    ReadValueId nodeToRead = new ReadValueId();
                    nodeToRead.NodeId = client.GetNodeIdByPath("Objects.Data.Static.Scalar.Int16Value");
                    nodeToRead.AttributeId = Attributes.Value;
                    nodesToRead.Add(nodeToRead);

                    nodeToRead = new ReadValueId();
                    nodeToRead.NodeId = client.GetNodeIdByPath("Objects.Data.Static.Scalar.Int32Value");
                    nodeToRead.AttributeId = Attributes.Value;
                    nodesToRead.Add(nodeToRead);

                    nodeToRead = new ReadValueId();
                    nodeToRead.NodeId = client.GetNodeIdByPath("Objects.Data.Static.Scalar.Int64Value");
                    nodeToRead.AttributeId = Attributes.Value;
                    nodesToRead.Add(nodeToRead);

                    // Read the node values synchronously
                    DataValueCollection readresults = client.Read(nodesToRead);

                    for (int i = 0; i < readresults.Count; i++)
                    {
                        DataValue res = readresults[i];
                        if (StatusCode.IsGood(res.StatusCode))
                            Console.WriteLine("synchronous read result " + nodesToRead[i].NodeId.ToString() + " Value => " + res.Value.ToString() + " StatusCode => " + res.StatusCode.ToString());
                        else
                            Console.WriteLine("read failed for " + nodesToRead[i].NodeId.ToString() + " StatusCode => " + res.StatusCode.ToString());
                    }

                    Console.WriteLine();
                    Console.WriteLine("press enter to reading asynchronous..");
                    Console.ReadLine();

                    //reading the nodes asynchronous
                    ReadResponse readResponse = await client.ReadAsync(nodesToRead);

                    for (int i = 0; i < readResponse.Results.Count; i++)
                    {
                        if (StatusCode.IsGood(readResponse.Results[i].StatusCode))
                            Console.WriteLine("asynchronous read result " + nodesToRead[i].NodeId.ToString() + " Value => " + readResponse.Results[i].ToString() + " StatusCode => " + readResponse.Results[i].StatusCode.ToString());
                        else
                            Console.WriteLine("async read failed for " + nodesToRead[i].NodeId.ToString() + " StatusCode => " + readResponse.Results[i].StatusCode.ToString());
                    }

                    Console.WriteLine();
                    Console.WriteLine("press enter to writing synchronous..");
                    Console.ReadLine();

                    // Create a collection of nodes to write
                    WriteValueCollection nodesToWrite = new WriteValueCollection();
                    WriteValue writeValue = new WriteValue();
                    writeValue.NodeId = client.GetNodeIdByPath("Objects.Data.Static.Scalar.Int16Value");
                    writeValue.Value = new DataValue((Int16)(-16));
                    writeValue.AttributeId = Attributes.Value;
                    nodesToWrite.Add(writeValue);

                    writeValue = new WriteValue();
                    writeValue.NodeId = client.GetNodeIdByPath("Objects.Data.Static.Scalar.Int32Value");
                    writeValue.AttributeId = Attributes.Value;
                    writeValue.Value = new DataValue(-3232);
                    nodesToWrite.Add(writeValue);

                    writeValue = new WriteValue();
                    writeValue.NodeId = client.GetNodeIdByPath("Objects.Data.Static.Scalar.Int64Value");
                    writeValue.AttributeId = Attributes.Value;
                    writeValue.Value = new DataValue((Int64)(-64646464));
                    nodesToWrite.Add(writeValue);

                    // Write the node values synchronously
                    StatusCodeCollection writeResults = client.Write(nodesToWrite);

                    for (int i = 0; i < writeResults.Count; i++)
                    {
                        if (StatusCode.IsGood(writeResults[i]))
                            Console.WriteLine("synchronous write result " + nodesToWrite[i].NodeId.ToString() + " Value => " + nodesToWrite[i].Value.ToString() + " StatusCode => " + writeResults[i].ToString());
                        else
                            Console.WriteLine("write failed for " + nodesToWrite[i].NodeId.ToString() + " StatusCode => " + writeResults[i].ToString());
                    }

                    Console.WriteLine();
                    Console.WriteLine("press enter to writing asynchronous..");
                    Console.ReadLine();

                    //writing the nodes asynchronous
                    WriteResponse writeResponse = await client.WriteAsync(nodesToWrite);

                    for (int i = 0; i < writeResponse.Results.Count; i++)
                    {
                        if (StatusCode.IsGood(writeResponse.Results[i]))
                            Console.WriteLine("asynchronous write result " + nodesToWrite[i].NodeId.ToString() + " Value => " + nodesToWrite[i].Value.ToString() + " StatusCode => " + writeResponse.Results[i].ToString());
                        else
                            Console.WriteLine("async write failed for " + nodesToWrite[i].NodeId.ToString() + " StatusCode => " + writeResponse.Results[i].ToString());
                    }

                    Console.WriteLine();
                    Console.WriteLine("press enter for exit..");
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

            try
            {
                //disconnect session
                client.Disconnect();

                //unregister events
                client.ServerConnectionLost -= Client_ServerConnectionLost;
                client.ServerConnected -= Client_ServerConnected;
                client.KeepAlive -= Client_KeepAlive;
                client.CertificateValidation -= client_CertificateValidation;
            }
            catch { }
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
