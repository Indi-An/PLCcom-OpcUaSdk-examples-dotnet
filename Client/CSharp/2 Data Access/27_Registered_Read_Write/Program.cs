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
// PLCcom OPC UA Client SDK - Workshop 27: Registered Read and Write
//
// RegisterNodes tells the server to optimize access to specific nodes.
// The server may cache internal references, making subsequent read/write
// operations faster. This is useful for high-frequency data access.
// Always call UnregisterNodes when done.
//
// What you will learn:
//   * How to register nodes for optimized access (RegisterNodes)
//   * How to read and write using registered NodeIds
//   * How to unregister nodes when done (UnregisterNodes)
//   * When registered access improves performance
//
// Target server: opc.tcp://localhost:48410
// ==============================================================================

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

             Console.WriteLine();


             Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
             Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 27: Registered R/W      ║");
             Console.WriteLine("║                                                              ║");
             Console.WriteLine("║  RegisterNodes tells the server to optimize access to        ║");
             Console.WriteLine("║  specific nodes. The server caches internal references,      ║");
             Console.WriteLine("║  making subsequent read/write operations faster.             ║");
             Console.WriteLine("║                                                              ║");
             Console.WriteLine("║  What you will learn:                                        ║");
             Console.WriteLine("║    * Register nodes for optimized access                     ║");
             Console.WriteLine("║    * Read and write using registered NodeIds                 ║");
             Console.WriteLine("║    * Unregister nodes when done                              ║");
             Console.WriteLine("║                                                              ║");
             Console.WriteLine("║  Required server: Server Workshop 11 (Simple Server)         ║");
             Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
             Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
             Console.WriteLine();

            //TODO
            //Submit your license information from your license e-mail
            string LicenseUserName = "<Enter your UserName here>";
            string LicenseSerial = "<Enter your Serial here>";

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
                    SessionConfiguration sessionConfiguration = SessionConfiguration.Build(System.Reflection.Assembly.GetEntryAssembly().GetName().Name,
                                                                                          Endpoints[iNumberOfEndpoint]);

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

                        //sample nodeIds to register
                        NodeIdCollection nodetoRegister = new NodeIdCollection();
                        nodetoRegister.Add(client.GetNodeIdByPath("Objects.Plant.Line1.Machine1.RPM")); //Objects.Plant.Line1.Machine1.RPM
                        nodetoRegister.Add(client.GetNodeIdByPath("Objects.Plant.Line1.Machine1.Temperature"));
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
                            StatusCode sc = client.Write(registeredNodeIds[0], 1750, Attributes.Value);
                            Console.WriteLine(Utils.Format("write Node {0} Statuscode => {1}", registeredNodeIds[0].ToString(), sc.ToString()));

                            
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
}
