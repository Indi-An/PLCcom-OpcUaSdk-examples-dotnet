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
            Console.WriteLine("║                                                              ║");
            Console.WriteLine("║  Required server: Server Workshop 11 (Simple Server)         ║");
            Console.WriteLine("║  opc.tcp://localhost:48410                                   ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            // Important !!!!!!!!!!!!!!!!!!
            // Enter your Username + Serial here! Please note: with blank fields the library runs
            // for 15 minutes during a debug session. Both values can also come
            // from configuration or an environment variable.
            // Free trial license (14 days, uninterrupted): https://www.indi-an.com/en/plccom/opc-ua-sdk/opcua-download/
            string LicenseUserName = "";
            string LicenseSerial = "";

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
                    SessionConfiguration sessionConfiguration = CreateConfig(Endpoints[iNumberOfEndpoint]);
                    PrintConfig(sessionConfiguration);

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

                    // NodeIds are fixed for Server Workshop 11 - assigned in creation order.
                    // ns=2 is the application namespace; i= is the sequential node counter.
                    // The counter starts after internal SDK nodes (VendorServerInfo, Namespaces etc.).
                    // Plant=9, Line1=10, Machine1=11, Temperature=12, Pressure=13, RPM=14
                    NodeId temperatureId = new NodeId("ns=2;i=12");  // Double
                    NodeId pressureId = new NodeId("ns=2;i=13");  // Float
                    NodeId rpmId = new NodeId("ns=2;i=14");  // Int32

                    //first create a ReadValueIdCollection and fill this with ReadValueId objects
                    ReadValueIdCollection nodesToRead = new ReadValueIdCollection();
                    ReadValueId nodeToRead = new ReadValueId();
                    nodeToRead.NodeId = temperatureId; // ns=2;i=12
                    nodeToRead.AttributeId = Attributes.Value;
                    nodesToRead.Add(nodeToRead);

                    nodeToRead = new ReadValueId();
                    nodeToRead.NodeId = rpmId; // ns=2;i=14
                    nodeToRead.AttributeId = Attributes.Value;
                    nodesToRead.Add(nodeToRead);

                    nodeToRead = new ReadValueId();
                    nodeToRead.NodeId = pressureId; // ns=2;i=13
                    nodeToRead.AttributeId = Attributes.Value;
                    nodesToRead.Add(nodeToRead);

                    //reading the nodes synchronous
                    DataValueCollection readresults = client.Read(nodesToRead);

                    for (int i = 0; i < readresults.Count; i++)
                    {
                        DataValue res = readresults[i];
                        string val = StatusCode.IsGood(res.StatusCode) ? res.Value?.ToString() ?? "null" : "(no value)";
                        Console.WriteLine("synchronous read result " + nodesToRead[i].NodeId.ToString() + " Value => " + val + " StatusCode => " + res.StatusCode.ToString());
                    }

                    Console.WriteLine();
                    Console.WriteLine("press enter to reading asynchronous..");
                    Console.ReadLine();

                    //reading the nodes asynchronous
                    ReadResponse readResponse = await client.ReadAsync(nodesToRead);

                    for (int i = 0; i < readResponse.Results.Count; i++)
                    {
                        DataValue res = readResponse.Results[i];
                        string val = StatusCode.IsGood(res.StatusCode) ? res.Value?.ToString() ?? "null" : "(no value)";
                        Console.WriteLine("asynchronous read result " + nodesToRead[i].NodeId.ToString() + " Value => " + val + " StatusCode => " + res.StatusCode.ToString());
                    }

                    Console.WriteLine();
                    Console.WriteLine("press enter to writing synchronous..");
                    Console.ReadLine();

                    //create a WriteValueCollection and fill this with WriteValue objects
                    WriteValueCollection nodesToWrite = new WriteValueCollection();
                    WriteValue writeValue = new WriteValue();
                    writeValue.NodeId = temperatureId; // ns=2;i=12
                    writeValue.Value = new DataValue(25.5);
                    writeValue.AttributeId = Attributes.Value;
                    nodesToWrite.Add(writeValue);

                    writeValue = new WriteValue();
                    writeValue.NodeId = rpmId; // ns=2;i=14
                    writeValue.AttributeId = Attributes.Value;
                    writeValue.Value = new DataValue(1750);
                    nodesToWrite.Add(writeValue);

                    writeValue = new WriteValue();
                    writeValue.NodeId = pressureId; // ns=2;i=13
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