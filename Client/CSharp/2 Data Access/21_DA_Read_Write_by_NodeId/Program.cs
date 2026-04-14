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
// PLCcom OPC UA Client SDK - Workshop 21: Read and Write by NodeId
//
// A NodeId is the unique address of a node in the OPC UA address space.
// It consists of a namespace index and an identifier (numeric, string,
// GUID or opaque). This is the low-level approach to reading and writing.
// See Workshop 22 for the more readable path-based approach.
//
// What you will learn:
//   * How to construct NodeIds from string notation (ns=2;i=X)
//   * How to resolve browse paths to NodeIds (GetNodeIdByPath)
//   * How to read single and multiple values by NodeId
//   * How to read values asynchronously (ReadAsync)
//   * How to write values synchronously and asynchronously
//
// Target server: opc.tcp://localhost:48410
// ==============================================================================

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

             Console.WriteLine();


             Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
             Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 21: Read/Write by NodeId║");
             Console.WriteLine("║                                                              ║");
             Console.WriteLine("║  A NodeId is the unique address of a node in the OPC UA      ║");
             Console.WriteLine("║  address space (e.g. ns=2;i=10219). This workshop shows      ║");
             Console.WriteLine("║  how to read and write values using NodeIds directly.        ║");
             Console.WriteLine("║                                                              ║");
             Console.WriteLine("║  What you will learn:                                        ║");
             Console.WriteLine("║    * Construct NodeIds from string notation                  ║");
             Console.WriteLine("║    * Read single and multiple values (sync and async)        ║");
             Console.WriteLine("║    * Write values and check the StatusCode                   ║");
             Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
             Console.WriteLine();

            //TODO
            //Submit your license information from your license e-mail
            string LicenseUserName = "<Enter your UserName here>";
            string LicenseSerial = "<Enter your Serial here>";

            EndpointDescriptionCollection Endpoints = UaClient.GetEndpoints(new Uri("opc.tcp://localhost:48410"), 60000);

            //sort endpoints by security level
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
                    //create a a SessionConfiguration with the selected endpoint and application name
                    SessionConfiguration sessionConfiguration = SessionConfiguration.Build(System.Reflection.Assembly.GetEntryAssembly().GetName().Name,
                                                                                          Endpoints[iNumberOfEndpoint]);

                    //enable autoconnect
                    sessionConfiguration.AutoConnect = true;

                    //output certificate store path
                    Console.WriteLine("Info: Sessionconfiguration created, certificate store path => " + sessionConfiguration.CertificateStorePath);

                    //Create a new opc client instance and pass your license information
                    client = new UaClient(LicenseUserName, LicenseSerial, sessionConfiguration);

                    Console.WriteLine("Info: license state => " + client.GetLicenceMessage());
                    Console.WriteLine("");

                    //register events
                    client.ServerConnectionLost += Client_ServerConnectionLost;
                    client.ServerConnected += Client_ServerConnected;
                    client.KeepAlive += Client_KeepAlive;
                    client.CertificateValidation += client_CertificateValidation;

                    client.Connect();

                    Console.WriteLine("press enter to reading synchronous..");
                    Console.ReadLine();

                    //Read multiple Nodes within one call 

                    //Resolve browse paths to NodeIds first
                    NodeId temperatureId = client.GetNodeIdByPath("Objects.Plant.Line1.Machine1.Temperature");
                    NodeId rpmId = client.GetNodeIdByPath("Objects.Plant.Line1.Machine1.RPM");
                    NodeId pressureId = client.GetNodeIdByPath("Objects.Plant.Line1.Machine1.Pressure");

                    //first create a ReadValueIdCollection and fill this with ReadValueId objects
                    ReadValueIdCollection nodesToRead = new ReadValueIdCollection();
                    ReadValueId nodeToRead = new ReadValueId();
                    nodeToRead.NodeId = temperatureId;//Objects.Plant.Line1.Machine1.Temperature
                    nodeToRead.AttributeId = Attributes.Value;
                    nodesToRead.Add(nodeToRead);

                    nodeToRead = new ReadValueId();
                    nodeToRead.NodeId = rpmId;//Objects.Plant.Line1.Machine1.RPM
                    nodeToRead.AttributeId = Attributes.Value;
                    nodesToRead.Add(nodeToRead);

                    nodeToRead = new ReadValueId();
                    nodeToRead.NodeId = pressureId; //Objects.Plant.Line1.Machine1.Pressure
                    nodeToRead.AttributeId = Attributes.Value;
                    nodesToRead.Add(nodeToRead);

                    //reading the nodes synchronous
                    DataValueCollection readresults = client.Read(nodesToRead);

                    for (int i = 0; i < readresults.Count; i++)
                    {
                        DataValue res = readresults[i];
                        Console.WriteLine("synchronous read result " + nodesToRead[i].NodeId.ToString() + " Value => " + res.Value.ToString() + " StatusCode => " + res.StatusCode.ToString());
                    }

                    Console.WriteLine();
                    Console.WriteLine("press enter to reading asynchronous..");
                    Console.ReadLine();

                    //reading the nodes asynchronous
                    ReadResponse readResponse = await client.ReadAsync(nodesToRead);

                    for (int i = 0; i < readResponse.Results.Count; i++)
                    {
                        Console.WriteLine("asynchronous read result " + nodesToRead[i].NodeId.ToString() + " Value => " + readResponse.Results[i].ToString() + " StatusCode => " + readResponse.Results[i].StatusCode.ToString());
                    }

                    Console.WriteLine();
                    Console.WriteLine("press enter to writing synchronous..");
                    Console.ReadLine();

                    //create a WriteValueCollection and fill this with WriteValue objects
                    WriteValueCollection nodesToWrite = new WriteValueCollection();
                    WriteValue writeValue = new WriteValue();
                    writeValue.NodeId = temperatureId;//Objects.Plant.Line1.Machine1.Temperature
                    writeValue.Value = new DataValue(25.5);
                    writeValue.AttributeId = Attributes.Value;
                    nodesToWrite.Add(writeValue);

                    writeValue = new WriteValue();
                    writeValue.NodeId = rpmId;//Objects.Plant.Line1.Machine1.RPM
                    writeValue.AttributeId = Attributes.Value;
                    writeValue.Value = new DataValue(1750);
                    nodesToWrite.Add(writeValue);

                    writeValue = new WriteValue();
                    writeValue.NodeId = pressureId; //Objects.Plant.Line1.Machine1.Pressure
                    writeValue.AttributeId = Attributes.Value;
                    writeValue.Value = new DataValue(1.05f);
                    nodesToWrite.Add(writeValue);

                    //writing the nodes synchronous
                    StatusCodeCollection writeResults = client.Write(nodesToWrite);

                    for (int i = 0; i < writeResults.Count; i++)
                    {
                        Console.WriteLine("synchronous write result " + nodesToWrite[i].NodeId.ToString() + " Value => " + nodesToWrite[i].Value.ToString() + " StatusCode => " + writeResults[i].ToString());
                    }

                    Console.WriteLine();
                    Console.WriteLine("press enter to writing asynchronous..");
                    Console.ReadLine();

                    //writing the nodes asynchronous
                    WriteResponse writeResponse = await client.WriteAsync(nodesToWrite);

                    for (int i = 0; i < writeResponse.Results.Count; i++)
                    {
                        Console.WriteLine("asynchronous write result " + nodesToWrite[i].NodeId.ToString() + " Value => " + nodesToWrite[i].Value.ToString() + " StatusCode => " + writeResponse.Results[i].ToString());
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

}
