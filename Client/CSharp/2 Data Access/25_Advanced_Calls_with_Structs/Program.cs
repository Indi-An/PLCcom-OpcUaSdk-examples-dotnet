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
// PLCcom OPC UA Client SDK - Workshop 25: Advanced Method Calls with Structs
//
// Building on Workshop 24, this example shows how to pass complex
// nested structures as method arguments. The input structure contains
// embedded sub-structures and arrays of structures - a common pattern
// in industrial OPC UA servers.
//
// What you will learn:
//   * How to encode nested structures with BinaryEncoder
//   * How to embed ExtensionObjects inside other structures
//   * How to encode arrays of structures
//   * How to call methods with complex structured arguments
//
// Required server: Server Workshop 13 (Methods)
// Target server:   opc.tcp://localhost:48410
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
            Console.WriteLine("║  PLCcom OPC UA Client SDK - Workshop 25: Advanced Calls      ║");
            Console.WriteLine("║                                                              ║");
            Console.WriteLine("║  Building on Workshop 24, this example passes complex        ║");
            Console.WriteLine("║  nested structures as method arguments: embedded             ║");
            Console.WriteLine("║  sub-structures and arrays of structures.                    ║");
            Console.WriteLine("║                                                              ║");
            Console.WriteLine("║  What you will learn:                                        ║");
            Console.WriteLine("║    * Encode nested structures with BinaryEncoder             ║");
            Console.WriteLine("║    * Embed ExtensionObjects inside other structures          ║");
            Console.WriteLine("║    * Encode arrays of structures                             ║");
            Console.WriteLine("║                                                              ║");
            Console.WriteLine("║  Required server: Server Workshop 13 (Methods)               ║");
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


                        /*
                        lets starting a method call, step by step
                        In this case, we pass a structure named as 'DataStructure_One" constructed as follows:

                        structure DataStructure_One = 
                        {
                            int myIntValue1,
                            string myStringValue2,
                            DataStructure_two DataStructure_two,
                            int myIntValue3,
                            DataStructure_two[] DataStructure_twoArray
                        }

                        Structure DataStructure_One contains some flat data types, a structure of DataStructure_two 
                        and a array of DataStructure_two structures:

                        structure DataStructure_two = 
                        {
                            int myIntValue10,
                            string myStringValue11,
                            int myIntValue12
                        }

                        Object to which the method should be applied is named as "myObjectNode_Advanced"
                        Method is named as "myMethodNode"
                        */


                        //create Encoder instances
                        BinaryEncoder encoderDataStructure_One = new BinaryEncoder(client.GetMessageContext());

                        //put objects to encoderDataStructure_One with given order
                        encoderDataStructure_One.WriteInt32("", 1); //myIntValue1
                        encoderDataStructure_One.WriteString("", "test_string"); //myStringValue2

                        #region create and add embedded structure

                        //create encoderDataStructure_Two instance
                        BinaryEncoder encoderDataStructure_Two = new BinaryEncoder(client.GetMessageContext());

                        //put objects to encoderDataStructure_Two with given order
                        encoderDataStructure_Two.WriteInt32("", 222);//myIntValue10
                        encoderDataStructure_Two.WriteString("", "test_string11"); //myStringValue11
                        encoderDataStructure_Two.WriteInt32("", 1212);//myIntValue12

                        //read byte array from encoder
                        byte[] argumentByteArray = encoderDataStructure_Two.CloseAndReturnBuffer();

                        //create an extension object and pass arguments to ExtensionObject.Body 
                        ExtensionObject extensionObjectDataStructure_Two = new ExtensionObject();
                        extensionObjectDataStructure_Two.Body = argumentByteArray;

                        //set type of structure, create a new ExpandedNodeId by name and namespace
                        extensionObjectDataStructure_Two.TypeId = new ExpandedNodeId("DataStructure_Two", 3);

                        //write structure to input arguments
                        encoderDataStructure_One.WriteExtensionObject("", extensionObjectDataStructure_Two);

                        #endregion

                        encoderDataStructure_One.WriteInt32("", 3333);//myIntValue3

                        #region create and add embedded structure Array 

                        //create a Array of DataStructure_Two objects with three objects

                        ExtensionObjectCollection dataStructure_TwoCollection = new ExtensionObjectCollection();

                        for (int i = 0; i < 3; i++)
                        {
                            //create encoderDataStructure_Two instance
                            encoderDataStructure_Two = new BinaryEncoder(client.GetMessageContext());

                            //put objects to encoderDataStructure_Two with given order
                            encoderDataStructure_Two.WriteInt32("", 555);//myIntValue10
                            encoderDataStructure_Two.WriteString("", "test_stringArray365"); //myStringValue11
                            encoderDataStructure_Two.WriteInt32("", 1212);//myIntValue12

                            //read byte array from encoder
                            argumentByteArray = encoderDataStructure_Two.CloseAndReturnBuffer();

                            //create an extension object and pass arguments to ExtensionObject.Body 
                            extensionObjectDataStructure_Two = new ExtensionObject();
                            extensionObjectDataStructure_Two.Body = argumentByteArray;

                            //set type of structure, create a new ExpandedNodeId by name and namespace
                            extensionObjectDataStructure_Two.TypeId = new ExpandedNodeId("DataStructure_Two", 3);

                            //add structure to ExtensionObjectCollection
                            dataStructure_TwoCollection.Add(extensionObjectDataStructure_Two);
                        }


                        //write structure array to input arguments
                        encoderDataStructure_One.WriteExtensionObjectArray("", dataStructure_TwoCollection);

                        #endregion
                        encoderDataStructure_One.WriteInt32("", 3333);//myIntValue3

                        //read byte array from encoder
                        argumentByteArray = encoderDataStructure_One.CloseAndReturnBuffer();

                        //create an extension object and pass arguments to ExtensionObject.Body
                        ExtensionObject extensionObjectWithInputArguments = new ExtensionObject();
                        extensionObjectWithInputArguments.Body = argumentByteArray;

                        //set type of structure, create a new ExpandedNodeId by name and namespace
                        extensionObjectWithInputArguments.TypeId = new ExpandedNodeId("DataStructure_One", 3);

                        //create your InputArguments with extensionObject
                        VariantCollection inputArguments = new VariantCollection();
                        inputArguments.Add(new Variant(extensionObjectWithInputArguments));

                        //resolve object and method via browse
                        NodeId objectNode = client.GetNodeIdByPath("Objects.Plant.myObjectNode_Advanced");
                        if (objectNode == null)
                        {
                            Console.WriteLine("myObjectNode_Advanced not found - is Server Workshop 13 running?");
                            return;
                        }

                        NodeId methodNode = null;
                        var browseDesc = new BrowseDescription
                        {
                            NodeId = objectNode,
                            BrowseDirection = BrowseDirection.Forward,
                            ReferenceTypeId = ReferenceTypeIds.HasComponent,
                            IncludeSubtypes = true,
                            NodeClassMask = (uint)NodeClass.Method,
                            ResultMask = (uint)BrowseResultMask.All
                        };
                        var refs = client.BrowseFull(new BrowseDescriptionCollection { browseDesc });
                        foreach (var r in refs)
                        {
                            if (r.BrowseName.Name == "myMethodNode")
                            {
                                methodNode = ExpandedNodeId.ToNodeId(r.NodeId, client.GetNamespaceUris());
                                break;
                            }
                        }
                        if (methodNode == null)
                        {
                            Console.WriteLine("myMethodNode not found under myObjectNode_Advanced");
                            return;
                        }

                        //create a CallMethodRequest instance and pass your arguments
                        CallMethodRequest request = new CallMethodRequest();
                        request.ObjectId = objectNode;
                        request.MethodId = methodNode;
                        request.InputArguments = inputArguments;

                        //call your method 
                        CallMethodResult result = client.Call(request);

                        //finaly evaluate your results,
                        if (StatusCode.IsGood(result.StatusCode))
                        {
                            foreach (Variant outputArgument in result.OutputArguments)
                            {
                                if (outputArgument != Variant.Null)
                                    Console.WriteLine("output argument: " + outputArgument.ToString());
                            }
                        }
                        else
                        {
                            Console.WriteLine("Method call failed " + result.StatusCode.ToString());
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